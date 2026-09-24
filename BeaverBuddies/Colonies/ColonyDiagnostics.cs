using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Debugging;
using Timberborn.DistributionSystem;
using Timberborn.DwellingSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InputSystem;
using Timberborn.InventorySystem;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using Timberborn.StatusSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.Versioning;
using Timberborn.WorkSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A diagnostics report for performance, desync and colony problems, written on request (Ctrl+Shift+J, or the
    /// button in the Ctrl+T window) and by itself when this computer desyncs. It is saved next to Player.log, in
    /// TimberTogether-Reports, and copied to the clipboard, ready to paste into a bug report.
    ///
    /// Once a day, every computer also logs a one-line fingerprint of the colony state (owners, marks, science,
    /// exchanges, population...), as the host's day plays (ColonyPresenceEvent, which compares it). The simulation is the
    /// same everywhere, so two players' fingerprints for the same day must match; the first part that differs shows where
    /// their computers stopped agreeing.
    ///
    /// Reads only: nothing here changes the game.
    /// </summary>
    public class ColonyDiagnostics : IPostLoadableSingleton, IUpdatableSingleton, ITickableSingleton, IInputProcessor
    {
        public const string KeyBindingId = "BeaverBuddies.KeyBind.DiagnosticsReport";
        private const int FrameSamples = 600, TickSamples = 60, Fingerprints = 10, LogLines = 80;

        private static readonly Queue<string> recentLog = new Queue<string>();

        private readonly InputService _inputService;
        private readonly IDayNightCycle _dayNightCycle;
        private readonly SpeedManager _speedManager;
        private readonly EntityRegistry _entityRegistry;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly ModRepository _modRepository;
        private readonly DevModeManager _devModeManager;

        private readonly float[] frames = new float[FrameSamples];
        private int frameCount;
        private readonly float[] tickRates = new float[TickSamples];
        private int tickRateCount, ticksThisSecond;
        private float secondStart;
        private int ticks;
        private readonly Queue<string> fingerprints = new Queue<string>();
        private bool desyncReported;
        // The session ends as a desync is found, before the report is written: what this computer was, and whether dev
        // mode was used this game (its tools, bar two, change one computer only).
        private string lastRole;
        private bool devModeUsed;

        public static ColonyDiagnostics Instance { get; private set; }

        public ColonyDiagnostics(InputService inputService, IDayNightCycle dayNightCycle, SpeedManager speedManager,
            EntityRegistry entityRegistry, EntityComponentRegistry entityComponentRegistry,
            DistrictCenterRegistry districtCenterRegistry, ModRepository modRepository, DevModeManager devModeManager)
        {
            _inputService = inputService;
            _dayNightCycle = dayNightCycle;
            _speedManager = speedManager;
            _entityRegistry = entityRegistry;
            _entityComponentRegistry = entityComponentRegistry;
            _districtCenterRegistry = districtCenterRegistry;
            _modRepository = modRepository;
            _devModeManager = devModeManager;
        }

        public void PostLoad()
        {
            Instance = this;
            ColonyProfiler.Reset();
            _inputService.AddInputProcessor(this);
        }

        /// <summary>Keeps the last colony log lines for the report (called by Plugin's logging).</summary>
        public static void Remember(string line)
        {
            lock (recentLog)
            {
                recentLog.Enqueue(line);
                while (recentLog.Count > LogLines) recentLog.Dequeue();
            }
        }

        public bool ProcessInput()
        {
            if (_inputService.IsKeyDown(KeyBindingId)) WriteReport("asked for");
            return false;
        }

        // ---- measuring ----

        public void UpdateSingleton()
        {
            frames[frameCount++ % FrameSamples] = Time.unscaledDeltaTime;
            if (Time.unscaledTime - secondStart >= 1f)
            {
                tickRates[tickRateCount++ % TickSamples] = ticksThisSecond / (Time.unscaledTime - secondStart);
                ticksThisSecond = 0;
                secondStart = Time.unscaledTime;
            }
            EventIO io = EventIO.Get();
            if (io != null) lastRole = RoleOf(io);
            if (_devModeManager.Enabled) devModeUsed = true;
            ReplayService replay = SingletonManager.GetSingleton<ReplayService>();
            // Written by itself (and copied) only in a separate-colonies game; in a shared game the Stability Fork's own
            // desync dialog is all there is, and the key still writes one.
            if (replay != null && replay.IsDesynced && !desyncReported && ColonyModeService.IsSeparateColonies)
            {
                desyncReported = true;
                WriteReport("this computer desynced");
            }
        }

        public void Tick()
        {
            ticks++;
            ticksThisSecond++;
            if (EventIO.IsNull) return;
            try
            {
                DailyPerformanceLine();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Perf] Could not write the daily performance line: " + error.Message);
            }
        }

        private static readonly ColonyProfiler.Spot DailyFingerprint = ColonyProfiler.Declare("Daily colony check (every entity)");

        /// <summary>
        /// The day's colony check on this computer: taken once, as the host's day (ColonyPresenceEvent) plays, at the same
        /// point of the same tick on every computer; logged, kept for the report, and returned for the comparison. Until
        /// 1.4.0-rc1 every computer also took one of its own at the turn of the day, a second walk over every entity a
        /// day in a separate tick (review D-S12).
        /// </summary>
        public string DailyCheck(int day)
        {
            long started = ColonyProfiler.Start();
            string fingerprint = Fingerprint();
            ColonyProfiler.Stop(DailyFingerprint, started);
            try
            {
                string line = $"day {day} tick {ticks}: {fingerprint}";
                fingerprints.Enqueue(line);
                while (fingerprints.Count > Fingerprints) fingerprints.Dequeue();
                Plugin.Log("[Colony] Check " + line);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not log the daily colony check: " + error.Message);
            }
            return fingerprint;
        }

        /// <summary>
        /// The colony state in a few short numbers, each the same on every computer that agrees. Sums over entities,
        /// so the order things are found in does not matter. Reads saved and tick-aligned state only, never anything
        /// of this computer's own (its slot, its view), so it may be compared between computers (ColonyPresenceEvent).
        /// One pass over every entity with two component lookups each; the few crossings are read from the game's registry
        /// of them, and each district is hashed once (1.4.0-rc1 review, D-S12).
        /// </summary>
        public string Fingerprint()
        {
            long owners = 0, stamps = 0, exchanges = 0, districts = 0, stock = 0, characters = 0;
            bool mixed = BeaverBuddies.Factions.MixedFactions.IsOn;
            var population = new int[ColonySlotTable.MaxSlots];
            var districtHashes = new Dictionary<DistrictCenter, long>();
            long DistrictHash(DistrictCenter center)
            {
                if (center == null) return 0;
                if (!districtHashes.TryGetValue(center, out long hash)) districtHashes[center] = hash = Hash(center);
                return hash;
            }
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.AllDistrictCenters)
            {
                int slot = DistrictOwner.OwnerOfDistrict(districtCenter) ?? -1;
                owners += Hash(districtCenter) * (slot + 2);
                DistrictPopulation people = districtCenter.GetComponent<DistrictPopulation>();
                if (slot >= 0 && slot < population.Length && people != null)
                    population[slot] += people.NumberOfAdults * 10000 + people.NumberOfChildren * 100 + people.NumberOfBots;
            }
            foreach (EntityComponent entity in _entityRegistry.Entities)
            {
                ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
                if (stamp != null) stamps += IdHash(entity) * (stamp.Slot + 2);
                // Mixed factions: each character's faction and how many needs it has (a beaver's is fixed when it is made).
                if (mixed)
                {
                    Timberborn.NeedSystem.NeedManager needs = entity.GetComponent<Timberborn.NeedSystem.NeedManager>();
                    if (needs != null)
                        characters += IdHash(entity) * (ColonyDigest.Of(BeaverBuddies.Factions.ColonyFactionService.SimFactionOf(entity)) * 31
                            + needs.NeedSpecs.Length + 1);
                }
                // Which district each building and construction site is joined to (what haulers and builders go by).
                DistrictBuilding districtBuilding = entity.GetComponent<DistrictBuilding>();
                if (districtBuilding != null)
                    districts += IdHash(entity) * (3 * DistrictHash(districtBuilding.ConstructionDistrict)
                        + 5 * DistrictHash(districtBuilding.InstantDistrict) + 7 * DistrictHash(districtBuilding.District) + 1);
            }
            // Both components below come only with a District Crossing (their decorators), which the game registers.
            foreach (DistrictCrossing crossing in _entityComponentRegistry.GetAll<DistrictCrossing>())
            {
                long entity = Hash(crossing);
                // Every exchange, open or closed (a closed one keeps its serial and ledger), with every field.
                CrossingExchange exchange = crossing.GetComponent<CrossingExchange>();
                if (exchange != null) exchanges += entity * exchange.Fingerprint();
                // What waits on each half of a crossing.
                DistrictCrossingInventory crossingInventory = crossing.GetComponent<DistrictCrossingInventory>();
                if (crossingInventory != null && crossingInventory.Inventory != null)
                {
                    foreach (GoodAmount good in crossingInventory.Inventory.Stock)
                        stock += entity * (ColonyDigest.Of(good.GoodId) * 7 + good.Amount);
                }
            }
            ColonyModeService mode = ColonyModeService.Instance;
            string flags = $"{(mode?.Enabled == true ? "sep" : "shared")}/{(ColonyScienceService.IsEnabled ? "sci" : "-")}"
                + $"/{(uint)ColonyDigest.Of(mode?.StartingSettings?.ToString()):x}"
                // Mixed factions: each colony's faction (nothing is added in any other game, whose line stays as it was).
                + (mixed ? $"/mixed:{BeaverBuddies.Factions.ColonyFactionService.Fingerprint()}/chars:{(uint)characters:x}" : "")
                // Mixed factions: each colony's beavers and bots by faction (review of 1.4.0-beta24, F6).
                + (mixed ? $"/census:{BeaverBuddies.Factions.ColonyFactionService.Census(_districtCenterRegistry.AllDistrictCenters)}" : "");
            // Not the table of who plays which colony: that is the host's bookkeeping, which it changes as it loads (its own
            // seat, SeatHost) and hands to everyone only inside the next hello. A guest whose hello was refused kept the
            // save's table and was stopped at its next daily check, although no guest simulates anything from it.
            string phases = $"{ColonyStamps.Instance?.Ticks ?? 0}/{ColonyExchangeService.Instance?.Ticks ?? 0}";
            return $"owners={(uint)owners:x} stamps={(uint)stamps:x} districts={(uint)districts:x} people={string.Join("/", population)} "
                + $"exchanges={(uint)exchanges:x} stock={(uint)stock:x} totals={(uint)(ColonyTradeLedger.Instance?.Fingerprint() ?? 0):x} "
                + $"marks=[{ColonyMarks.Instance?.Fingerprint()}] science=[{ColonyScienceService.Instance?.Fingerprint()}] "
                + $"hours=[{ColonyWorkingHours.Instance?.Fingerprint()}] away=[{ColonyLifecycle.Instance?.Fingerprint()}] "
                + $"stewards=[{ColonyStewards.Instance?.Fingerprint()}] wishes=[{ColonyWishlist.Instance?.Fingerprint()}] "
                + $"flags={flags} phases={phases} digest={ColonyDigest.Describe()}";
        }

        private static string RoleOf(EventIO io) => io is ServerEventIO ? "host" : io is ClientEventIO ? "guest" : io.GetType().Name;

        private static long Hash(BaseComponent component)
        {
            if (!component) return 0;
            EntityComponent entity = component.GetComponent<EntityComponent>();
            return entity == null ? 0 : entity.EntityId.GetHashCode();
        }

        // Hash of an entity from the registry, which is its own EntityComponent: what Hash gives, without the lookup.
        private static long IdHash(EntityComponent entity) => !entity ? 0 : entity.EntityId.GetHashCode();

        // ---- the report ----

        public void WriteReport(string reason)
        {
            string text;
            try
            {
                text = BuildReport(reason);
            }
            catch (Exception error)
            {
                text = $"Timber Together diagnostics report\nThe report could not be built: {error}";
            }
            string path = null;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "TimberTogether-Reports");
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, $"colony-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(path, text);
                GUIUtility.systemCopyBuffer = text;
                Plugin.Log("[Colony] Diagnostics report written to " + path);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not save the diagnostics report: " + error.Message);
            }
            SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(
                string.Format(RegisteredLocalizationService.T("BeaverBuddies.Colony.Diagnostics.Written"), path ?? "-"), warning: false);
        }

        private string BuildReport(string reason)
        {
            var r = new StringBuilder();
            r.AppendLine("Timber Together diagnostics report");
            r.AppendLine($"Version {Plugin.Version} | Timberborn {GameVersions.CurrentVersion} | "
                + $"written {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({reason})");
            EventIO io = EventIO.Get();
            string role = io != null ? RoleOf(io) : lastRole != null ? lastRole + " (the session has ended)" : "single player";
            ReplayService replay = SingletonManager.GetSingleton<ReplayService>();
            r.AppendLine($"Role: {role} | local colony slot {ColonySession.LocalSlot} (seat {ColonySession.LocalSeat}) | separate colonies {On(ColonyModeService.IsSeparateColonies)}, "
                + $"separate science {On(ColonyScienceService.IsEnabled)} | detailed logging {On(Settings.Debug)}, away days setting {Settings.AbandonedColonyDaysValue} | "
                + $"dev mode {On(_devModeManager.Enabled)}" + (devModeUsed && !_devModeManager.Enabled ? " (was on this game)" : ""));
            r.AppendLine($"Game: day {_dayNightCycle.DayNumber}, {_dayNightCycle.HoursPassedToday:0.0} h | ticks since load {ticks}"
                + (replay != null ? $" (replay {replay.TicksSinceLoad})" : "") + $" | speed {_speedManager.CurrentSpeed} | desynced: {(replay?.IsDesynced == true ? "YES" : "no")}");
            var session = ColonySlotService.Instance?.Session.Select(p => $"player {p.Key} -> slot {p.Value}") ?? Enumerable.Empty<string>();
            r.AppendLine("Session: " + string.Join(", ", session));
            r.AppendLine("Mods: " + string.Join(", ", _modRepository.EnabledMods.Where(m => m?.Manifest != null)
                .Select(m => $"{m.Manifest.Id} {m.Manifest.Version.Formatted}")));

            Performance(r);
            Colonies(r);
            TradingPostReport(r);

            r.AppendLine();
            r.AppendLine("== Daily colony checks (two players' lines for the same day must match; the first part that differs is where they disagree) ==");
            foreach (string line in fingerprints) r.AppendLine(line);
            if (ColonyModeService.IsSeparateColonies) r.AppendLine($"now tick {ticks}: {Fingerprint()}");

            r.AppendLine();
            r.AppendLine($"== Recent colony log (last {LogLines}) ==");
            lock (recentLog)
            {
                foreach (string line in recentLog) r.AppendLine(line);
            }
            return r.ToString();
        }

        private void Performance(StringBuilder r)
        {
            r.AppendLine();
            r.AppendLine("== Performance ==");
            int count = Math.Min(frameCount, FrameSamples);
            if (count > 0)
            {
                var sorted = frames.Take(count).OrderBy(f => f).ToList();
                float average = sorted.Average(), slow = sorted[(int)(count * 0.95f)], worst = sorted[count - 1];
                r.AppendLine($"Frames (last {count}): average {1 / Math.Max(average, 1e-4f):0} fps, slowest 5% {1 / Math.Max(slow, 1e-4f):0} fps, "
                    + $"slowest frame {worst * 1000:0} ms");
            }
            int seconds = Math.Min(tickRateCount, TickSamples);
            if (seconds > 0)
            {
                var rates = tickRates.Take(seconds).ToList();
                r.AppendLine($"Ticks per second (last {seconds} s): average {rates.Average():0.0}, lowest {rates.Min():0.0}, highest {rates.Max():0.0}");
            }
            int beavers = _districtCenterRegistry.AllDistrictCenters.Sum(dc => dc.GetComponent<DistrictPopulation>()?.NumberOfAdults + dc.GetComponent<DistrictPopulation>()?.NumberOfChildren ?? 0);
            int bots = _districtCenterRegistry.AllDistrictCenters.Sum(dc => dc.GetComponent<DistrictPopulation>()?.NumberOfBots ?? 0);
            int buildings = _entityRegistry.Entities.Count(e => e.GetComponent<Building>() != null);
            r.AppendLine($"Entities: {_entityRegistry.Entities.Count} | beavers {beavers}, bots {bots} (in districts) | buildings and paths {buildings}");
            r.AppendLine("Colony code since load (name: calls, total ms, average µs, slowest ms):");
            foreach (var (name, calls, totalMs, maxMs) in ColonyProfiler.Snapshot())
                r.AppendLine($"  {name}: {calls}, {totalMs:0.0}, {(calls > 0 ? totalMs * 1000 / calls : 0):0.0}, {maxMs:0.00}");
            // What Script P and a long session read (1.4.0-rc1 review, D-S2 and D-S7).
            r.AppendLine(InterruptLine(0, 0));
            r.AppendLine("Memory: " + MemoryLine());
            CoopDelay(r);
        }

        // ---- counters for the report and the daily performance line: read only, nothing simulated reads them ----

        private int perfDay = int.MinValue, perfTicks;
        private long perfFramesTicking, perfCutShort, perfLost;
        private readonly int[] perfCollections = new int[3];

        /// <summary>
        /// How often a creation or deletion in a tick ended a frame's ticking (co-op; TickingService), since load or since
        /// the given counts. Each such frame leaves the rest of the tick to the next frame, and the buckets it gives back
        /// are capped at one tick: what the cap throws away is game time lost.
        /// </summary>
        private string InterruptLine(long framesTickingBefore, long cutShortBefore, long lostBefore = 0, int ticksBefore = 0)
        {
            TickingService ticking = SingletonManager.GetSingleton<TickingService>();
            if (ticking == null) return "Frames cut short: no co-op ticking in this game";
            int span = ticks - ticksBefore;
            long cut = ticking.FramesCutShort - cutShortBefore, lost = ticking.BucketsLost - lostBefore;
            return $"Frames cut short by a creation or deletion in a tick: {cut} in {span} ticks ({(span > 0 ? (double)cut / span : 0):0.00} a tick), "
                + $"of {ticking.FramesTicking - framesTickingBefore} frames that ticked | buckets lost to the one-tick cap {lost} "
                + $"({lost / 129.0:0.0} ticks of game time), given back since load {ticking.BucketsGivenBack}";
        }

        /// <summary>Beavers and bots in every district: how large the game is, for the desync dialog (DesyncDialogPlan).</summary>
        public int CharactersInDistricts()
        {
            int characters = 0;
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.AllDistrictCenters)
            {
                DistrictPopulation people = districtCenter.GetComponent<DistrictPopulation>();
                if (people != null) characters += people.NumberOfAdults + people.NumberOfChildren + people.NumberOfBots;
            }
            return characters;
        }

        /// <summary>The heap, garbage collections, and the mod's own collections that grow with a session.</summary>
        private static string MemoryLine()
        {
            return $"heap {GC.GetTotalMemory(false) / 1048576.0:0} MB, collections gen0 {GC.CollectionCount(0)} gen1 {GC.CollectionCount(1)} "
                + $"gen2 {GC.CollectionCount(2)} | detailed-logging traces {DesyncDetecter.DesyncDetecterService.KeptTicks} ticks "
                + $"({DesyncDetecter.DesyncDetecterService.KeptTraces}), water snapshots {DesyncDetecter.WaterDiagnostics.KeptBytes / 1048576.0:0.0} MB, "
                + $"walker records {DesyncDetecter.WalkerDiagnostics.KeptTicks} ticks | colony changes counted {ColonyDigest.Changes}, "
                + $"marked tiles {ColonyMarks.Instance?.MarkedTiles ?? 0}";
        }

        // Once a game day, in a co-op game: one line in the log, for long sessions and Script P (1.4.0-rc1 review, D-S7).
        private void DailyPerformanceLine()
        {
            int day = _dayNightCycle.DayNumber;
            if (day == perfDay) return;
            bool first = perfDay == int.MinValue;
            perfDay = day;
            TickingService ticking = SingletonManager.GetSingleton<TickingService>();
            if (!first && ticking != null)
            {
                string gcs = $"gen0 +{GC.CollectionCount(0) - perfCollections[0]} gen1 +{GC.CollectionCount(1) - perfCollections[1]} gen2 +{GC.CollectionCount(2) - perfCollections[2]}";
                Plugin.Log($"[Perf] Day {day} (tick {ticks}): {InterruptLine(perfFramesTicking, perfCutShort, perfLost, perfTicks)} | "
                    + $"collections today {gcs} | {MemoryLine()}");
            }
            perfTicks = ticks;
            perfFramesTicking = ticking?.FramesTicking ?? 0;
            perfCutShort = ticking?.FramesCutShort ?? 0;
            perfLost = ticking?.BucketsLost ?? 0;
            for (int generation = 0; generation < perfCollections.Length; generation++) perfCollections[generation] = GC.CollectionCount(generation);
        }

        // A guest's delay: the link to each player, how long its own actions take to come back, how far behind the host
        // it runs and how often it waits for the host (see Latency.PendingActions).
        private static void CoopDelay(StringBuilder r)
        {
            EventIO io = EventIO.Get();
            TimberNet.TimberNetBase net = io is ServerEventIO host ? host.NetBase : io is ClientEventIO guest ? guest.NetBase : null;
            if (net == null) return;
            r.AppendLine("Co-op:");
            try
            {
                var peers = net.GetNetworkStatus().Peers.Select(peer => $"player {peer.PlayerId} over {peer.Transport}"
                    + (peer.RttMs.HasValue ? $", ping {peer.RttMs:0} ms" : "")
                    + (peer.TicksBehind.HasValue ? $", {peer.TicksBehind} ticks behind" : "")
                    + (peer.Fps.HasValue ? $", {peer.Fps} fps" : "")).ToList();
                r.AppendLine("  Links: " + (peers.Count == 0 ? "none" : string.Join("; ", peers)));
            }
            catch (Exception error)
            {
                r.AppendLine("  Links: could not be read (" + error.Message + ")");
            }
            if (io is ClientEventIO && Latency.PendingActions.Instance != null)
            {
                r.AppendLine($"  Now {io.TicksBehind} ticks behind the host");
                foreach (string line in Latency.PendingActions.Instance.ReportLines()) r.AppendLine("  " + line);
            }
        }

        private void Colonies(StringBuilder r)
        {
            r.AppendLine();
            r.AppendLine("== Colonies ==");
            ColonyLifecycle lifecycle = ColonyLifecycle.Instance;
            List<int> present = ColonyLifecycle.PresentSlots();
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                var districts = _districtCenterRegistry.AllDistrictCenters.Where(dc => DistrictOwner.OwnerOfDistrict(dc) == slot).ToList();
                if (districts.Count == 0) continue;
                int homeless = 0, jobless = 0, buildings = 0, unfinished = 0;
                var statuses = new Dictionary<string, int>();
                foreach (DistrictCenter districtCenter in districts)
                {
                    DistrictPopulation people = districtCenter.GetComponent<DistrictPopulation>();
                    if (people == null) continue;
                    foreach (var beaver in people.Beavers)
                    {
                        if (beaver.GetComponent<Dweller>()?.HasHome == false) homeless++;
                    }
                    foreach (var adult in people.Adults)
                    {
                        if (adult.GetComponent<Worker>()?.Employed == false) jobless++;
                    }
                }
                foreach (EntityComponent entity in _entityRegistry.Entities)
                {
                    if (entity.GetComponent<Building>() == null || DistrictOwner.OwnerOf(entity) != slot) continue;
                    buildings++;
                    BlockObject blockObject = entity.GetComponent<BlockObject>();
                    if (blockObject != null && !blockObject.IsFinished) unfinished++;
                    StatusSubject subject = entity.GetComponent<StatusSubject>();
                    if (subject == null) continue;
                    foreach (StatusInstance status in subject.ActiveStatuses)
                    {
                        string key = status.StatusDescription ?? "?";
                        statuses.TryGetValue(key, out int n);
                        statuses[key] = n + 1;
                    }
                }
                string state = lifecycle == null ? "" : lifecycle.PopulationOf(slot) == 0 ? "no beavers"
                    : present.Contains(slot) ? "playing" : $"away (missed {lifecycle.DaysAway(slot)} days)";
                r.AppendLine($"{ColonyExchangeService.ColonyName(slot)} (slot {slot}): {state} | {districts.Count} districts | "
                    + $"homeless {homeless}, adults without a job {jobless} | "
                    + $"{buildings} buildings and paths ({unfinished} unfinished) | working hours {ColonyWorkingHours.Instance?.HoursOf(slot)}");
                foreach (DistrictCenter districtCenter in districts)
                {
                    DistrictPopulation people = districtCenter.GetComponent<DistrictPopulation>();
                    Vector3Int at = districtCenter.GetComponent<BlockObject>().Coordinates;
                    r.AppendLine($"  district at {at}: {people?.NumberOfAdults} adults, {people?.NumberOfChildren} children, {people?.NumberOfBots} bots"
                        + (districtCenter.GetComponent<BlockObject>().IsFinished ? "" : " (unfinished)"));
                }
                if (statuses.Count > 0)
                    r.AppendLine("  building statuses: " + string.Join(", ", statuses.OrderByDescending(s => s.Value).Take(12).Select(s => $"{s.Key} x{s.Value}")));
            }
            int noDistrict = _entityRegistry.Entities.Count(e => e.GetComponent<Citizen>() is Citizen c && !c.AssignedDistrict);
            r.AppendLine($"Beavers and bots in no district: {noDistrict}");
            r.AppendLine($"Buildings waiting for a colony: {ColonyStamps.Instance?.Unstamped ?? 0}");
        }

        private void TradingPostReport(StringBuilder r)
        {
            r.AppendLine();
            r.AppendLine("== Trading posts ==");
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;
            var seen = new HashSet<DistrictCrossing>();
            foreach (DistrictCrossing half in _entityComponentRegistry.GetEnabled<DistrictCrossing>())
            {
                DistrictCrossing partner = TradingPosts.Partner(half);
                if (!TradingPosts.IsTradingPost(half) || seen.Contains(partner)) continue;
                seen.Add(half);
                int a = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(half)) ?? -1;
                int b = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(partner)) ?? -1;
                r.AppendLine($"{ColonyExchangeService.ColonyName(a)} <-> {ColonyExchangeService.ColonyName(b)} at {half.GetComponent<BlockObject>().Coordinates} | "
                    + $"workers {Workers(half)} | {Workers(partner)}");
                CrossingExchange ax = ColonyExchangeService.Of(half), bx = ColonyExchangeService.Of(partner);
                if (ax == null || bx == null || !ax.IsOpen || exchanges == null)
                {
                    r.AppendLine("  no exchange");
                    continue;
                }
                r.AppendLine($"  exchange {ax.Serial} {ax.State}, round {ax.Done + 1} of {(ax.Repeat ? "until cancelled" : ax.Rounds.ToString())}: "
                    + $"{ax.GoodId ?? "-"} {ax.Held}/{ax.Total} for {bx.GoodId ?? "-"} {bx.Held}/{bx.Total}"
                    + (ax.CancelAsked || bx.CancelAsked ? ", a colony asked to end it" : ""));
                if (ax.IsActive)
                {
                    string stall = Stall(half, ax, exchanges) + Stall(partner, bx, exchanges);
                    if (stall.Length > 0) r.AppendLine("  held up:" + stall);
                }
            }
            if (seen.Count == 0) r.AppendLine("none");
        }

        private static string Workers(DistrictCrossing half) =>
            half.GetComponent<Workplace>() is Workplace workplace ? $"{workplace.NumberOfAssignedWorkers}/{workplace.MaxWorkers}" : "-";

        /// <summary>Why one side of a running exchange is not in yet, as the trading post's panel says it (ColonyExchangeService.WhyNotIn).</summary>
        private static string Stall(DistrictCrossing half, CrossingExchange side, ColonyExchangeService exchanges)
        {
            string why = exchanges.WhyNotIn(half, side);
            return why.Length == 0 ? "" : $" [{ColonyExchangeService.ColonyName(ColonyExchangeService.OwnerOf(half))}] {why}";
        }

        private static string On(bool value) => value ? "on" : "off";
    }
}
