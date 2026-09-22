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
    /// BeaverBuddies-Reports, and copied to the clipboard, ready to paste into a bug report.
    ///
    /// Once a day, every computer also logs a one-line fingerprint of the colony state (owners, marks, science,
    /// exchanges, population...). The simulation is the same everywhere, so two players' fingerprints for the same day
    /// must match; the first part that differs shows where their computers stopped agreeing.
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
        private int ticks, checkedDay = int.MinValue;
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
            // In a separate-colonies co-op game: a shared game has no colony state to check.
            if (EventIO.IsNull || !ColonyModeService.IsSeparateColonies) return;
            int day = _dayNightCycle.DayNumber;
            if (day == checkedDay) return;
            bool first = checkedDay == int.MinValue;
            checkedDay = day;
            // Only at the turn of a day, so every computer's line for a day is taken at the same moment (not when each
            // happened to load).
            if (first) return;
            try
            {
                string line = $"day {day} tick {ticks}: {Fingerprint()}";
                fingerprints.Enqueue(line);
                while (fingerprints.Count > Fingerprints) fingerprints.Dequeue();
                Plugin.Log("[Colony] Check " + line);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not take the daily colony check: " + error.Message);
            }
        }

        /// <summary>
        /// The colony state in a few short numbers, each the same on every computer that agrees. Sums over entities,
        /// so the order things are found in does not matter. Reads saved and tick-aligned state only, never anything
        /// of this computer's own (its slot, its view), so it may be compared between computers (ColonyPresenceEvent).
        /// </summary>
        public string Fingerprint()
        {
            long owners = 0, stamps = 0, exchanges = 0, districts = 0, stock = 0;
            var population = new int[ColonySlotTable.MaxSlots];
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
                if (stamp != null) stamps += Hash(entity) * (stamp.Slot + 2);
                // Which district each building and construction site is joined to (what haulers and builders go by).
                DistrictBuilding districtBuilding = entity.GetComponent<DistrictBuilding>();
                if (districtBuilding != null)
                    districts += Hash(entity) * (3 * Hash(districtBuilding.ConstructionDistrict) + 5 * Hash(districtBuilding.InstantDistrict)
                        + 7 * Hash(districtBuilding.District) + 1);
                // Every exchange, open or closed (a closed one keeps its serial and ledger), with every field.
                CrossingExchange exchange = entity.GetComponent<CrossingExchange>();
                if (exchange != null) exchanges += Hash(entity) * exchange.Fingerprint();
                // What waits on each half of a crossing.
                DistrictCrossingInventory crossingInventory = entity.GetComponent<DistrictCrossingInventory>();
                if (crossingInventory != null && crossingInventory.Inventory != null)
                {
                    foreach (GoodAmount good in crossingInventory.Inventory.Stock)
                        stock += Hash(entity) * (ColonyDigest.Of(good.GoodId) * 7 + good.Amount);
                }
            }
            ColonyModeService mode = ColonyModeService.Instance;
            string flags = $"{(mode?.Enabled == true ? "sep" : "shared")}/{(ColonyScienceService.IsEnabled ? "sci" : "-")}"
                + $"/{(uint)ColonyDigest.Of(mode?.StartingSettings?.ToString()):x}";
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
                text = $"BeaverBuddies MultiColony diagnostics report\nThe report could not be built: {error}";
            }
            string path = null;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "BeaverBuddies-Reports");
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
            r.AppendLine("BeaverBuddies MultiColony diagnostics report");
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
            CoopDelay(r);
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

        /// <summary>Why one side of a running exchange is not in yet, if it can tell.</summary>
        private static string Stall(DistrictCrossing half, CrossingExchange side, ColonyExchangeService exchanges)
        {
            if (side.Total <= 0 || side.GoodId == null || exchanges.IsIn(half, side)) return "";
            string who = $" [{ColonyExchangeService.ColonyName(ColonyExchangeService.OwnerOf(half))}]";
            if (side.GoodId == ExchangeTerms.Science)
                return who + $" has {ColonyExchangeService.ScienceToSpare(ColonyExchangeService.OwnerOf(half))} of {side.Total} science";
            if (side.GoodId == ExchangeTerms.Beavers) return who + $" can spare {exchanges.BeaversToSpare(half)} of {side.Total} beavers";
            if (half.GetComponent<Workplace>()?.NumberOfAssignedWorkers == 0) return who + " no workers on its half";
            Inventory inventory = half.GetComponent<DistrictCrossingInventory>()?.Inventory;
            if (inventory != null && inventory.UnreservedCapacity(side.GoodId) == 0)
                return who + " no room on its half (the other colony's goods are not being hauled away)";
            return who + $" {side.Held} of {side.Total} delivered";
        }

        private static string On(bool value) => value ? "on" : "off";
    }
}
