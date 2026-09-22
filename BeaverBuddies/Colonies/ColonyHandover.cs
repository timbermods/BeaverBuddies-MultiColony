using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    public enum HandoverReason { Died = 0, Abandoned = 1, ByHost = 2 }

    /// <summary>
    /// What happens to a colony whose player can't run it any more. A colony handed over becomes another colony's: its
    /// district centers, buildings, land, marks, stock and science pool (its unlocks are shared, not taken away). Its
    /// player, left with no colony, may found a new one at once (Ctrl+K).
    ///
    /// - A colony with no beavers or bots left for a whole day goes to the nearest living colony. Decided in the
    ///   simulation, the same on every computer.
    /// - A colony whose player has missed a number of in-game days of hosted co-op play (host setting, 7 by default)
    ///   goes to the nearest colony whose player is playing. Decided by the host and played everywhere as an action.
    ///   Only days of a hosted game count, from the day after it was loaded (so a returning player has time to join),
    ///   and a day the player is in the game starts the count again.
    /// - The host may hand over any colony whose player is away, or that has no beavers, by hand (the trading posts
    ///   and colonies window, Ctrl+T), for example to a player whose Steam account changed.
    ///
    /// The count of missed days is saved: the host tells every computer once a day who is playing.
    /// </summary>
    public class ColonyLifecycle : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton, ITickableSingleton
    {
        private static readonly SingletonKey LifecycleKey = new SingletonKey("BeaverBuddies.ColonyLifecycle");
        private static readonly ListKey<int> AwayDaysKey = new ListKey<int>("AwayDays");
        private static readonly ListKey<int> DeadSinceKey = new ListKey<int>("DeadSince");
        private const int Unknown = -1;

        private readonly ISingletonLoader _singletonLoader;
        private readonly IDayNightCycle _dayNightCycle;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly EntityRegistry _entityRegistry;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private readonly ColonyRulesService _colonyRulesService;

        // Days of hosted play each colony's player has missed in a row.
        private readonly int[] awayDays = new int[ColonySlotTable.MaxSlots];
        private readonly int[] deadSince = Enumerable.Repeat(Unknown, ColonySlotTable.MaxSlots).ToArray();
        private int checkedDay = int.MinValue;
        // Host only: handovers asked for and not yet played, so each is asked for once.
        private readonly HashSet<int> requested = new HashSet<int>();

        public static ColonyLifecycle Instance => SingletonManager.GetSingleton<ColonyLifecycle>();

        public ColonyLifecycle(ISingletonLoader singletonLoader, IDayNightCycle dayNightCycle,
            DistrictCenterRegistry districtCenterRegistry, EntityRegistry entityRegistry,
            EntityComponentRegistry entityComponentRegistry, ColonyRulesService colonyRulesService)
        {
            _singletonLoader = singletonLoader;
            _dayNightCycle = dayNightCycle;
            _districtCenterRegistry = districtCenterRegistry;
            _entityRegistry = entityRegistry;
            _entityComponentRegistry = entityComponentRegistry;
            _colonyRulesService = colonyRulesService;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(LifecycleKey, out IObjectLoader loader)) return;
            Read(loader, AwayDaysKey, awayDays);
            Read(loader, DeadSinceKey, deadSince);
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            IObjectSaver saver = singletonSaver.GetSingleton(LifecycleKey);
            saver.Set(AwayDaysKey, awayDays.ToList());
            saver.Set(DeadSinceKey, deadSince.ToList());
        }

        private static void Read(IObjectLoader loader, ListKey<int> key, int[] into)
        {
            if (!loader.Has(key)) return;
            List<int> saved = loader.Get(key);
            for (int i = 0; i < into.Length && i < saved.Count; i++) into[i] = saved[i];
        }

        // ---- questions (the same on every computer) ----

        public bool OwnsDistrict(int slot) =>
            _districtCenterRegistry.AllDistrictCenters.Any(dc => DistrictOwner.OwnerOfDistrict(dc) == slot);

        /// <summary>Beavers and bots living in a colony's districts.</summary>
        public int PopulationOf(int slot)
        {
            int population = 0;
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.AllDistrictCenters)
            {
                if (DistrictOwner.OwnerOfDistrict(districtCenter) != slot) continue;
                DistrictPopulation districtPopulation = districtCenter.GetComponent<DistrictPopulation>();
                if (districtPopulation != null)
                    population += districtPopulation.NumberOfAdults + districtPopulation.NumberOfChildren + districtPopulation.NumberOfBots;
            }
            return population;
        }

        /// <summary>Days of hosted play the colony's player has missed in a row.</summary>
        public int? DaysAway(int slot) => slot >= 0 && slot < awayDays.Length ? awayDays[slot] : (int?)null;

        public bool IsDead(int slot) => OwnsDistrict(slot) && PopulationOf(slot) == 0;

        /// <summary>Diagnostics: days each colony's player has missed, and since when a colony has had nobody.</summary>
        public string Fingerprint() => string.Join(" ",
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(i => $"{i}:{awayDays[i]}a{(deadSince[i] == Unknown ? "" : "d" + deadSince[i])}"));

        /// <summary>
        /// The living colony nearest to <paramref name="from"/> (district center to district center), among
        /// <paramref name="candidates"/>; the lowest numbered on a tie. Null when none.
        /// </summary>
        public int? NearestLiving(int from, IEnumerable<int> candidates)
        {
            var mine = CentersOf(from);
            int? best = null;
            long bestDistance = long.MaxValue;
            foreach (int slot in candidates.Distinct().OrderBy(s => s))
            {
                if (slot == from || slot < 0 || slot >= ColonySlotTable.MaxSlots || PopulationOf(slot) == 0) continue;
                long distance = long.MaxValue;
                foreach (Vector3Int a in mine)
                {
                    foreach (Vector3Int b in CentersOf(slot))
                    {
                        long dx = a.x - b.x, dy = a.y - b.y;
                        distance = Math.Min(distance, dx * dx + dy * dy);
                    }
                }
                if (distance < bestDistance)
                {
                    best = slot;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private List<Vector3Int> CentersOf(int slot) =>
            _districtCenterRegistry.AllDistrictCenters.Where(dc => DistrictOwner.OwnerOfDistrict(dc) == slot)
                .Select(dc => dc.GetComponent<BlockObject>().Coordinates).ToList();

        // ---- once a day ----

        private static readonly ColonyProfiler.Spot DailyChecks = ColonyProfiler.Declare("Daily colony checks");

        public void Tick()
        {
            if (!ColonyModeService.IsSeparateColonies) return;
            int day = _dayNightCycle.DayNumber;
            if (day == checkedDay) return;
            bool firstCheck = checkedDay == int.MinValue;
            checkedDay = day;
            // Nothing is decided on the first check after a load: populations are still being counted, and players are
            // still joining (a guest is only known once its hello has been played).
            if (firstCheck) return;
            long started = ColonyProfiler.Start();
            HandOverDeadColonies(day);
            if (EventIO.Get() is ServerEventIO) HostDaily(day);
            ColonyProfiler.Stop(DailyChecks, started);
            if (Settings.Debug) LogDiagnostics(day);
        }

        // With detailed logging on: one line a day per colony, for reports from long and large games.
        private void LogDiagnostics(int day)
        {
            try
            {
                ColonyReach reach = ColonyReach.Instance;
                var (counted, unstamped) = reach?.Counts ?? (0, 0);
                int exchanges = _entityComponentRegistry.GetEnabled<DistrictCrossing>()
                    .Count(half => ColonyExchangeService.Of(half)?.IsActive == true && ColonyExchangeService.Of(half).ProposedHere);
                for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
                {
                    if (!OwnsDistrict(slot)) continue;
                    Plugin.Log($"[Colony] Day {day}: slot {slot} has {PopulationOf(slot)} beavers and bots, {reach?.LandSize(slot) ?? 0} tiles of land, "
                        + $"its player missed {awayDays[slot]} days");
                }
                Plugin.Log($"[Colony] Day {day}: {counted} buildings counted for land ({unstamped} waiting for a colony), {exchanges} exchanges running");
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Diagnostics failed: " + error.Message);
            }
        }

        // Every computer: a colony with nobody left for a whole day goes to its nearest living neighbour.
        private void HandOverDeadColonies(int day)
        {
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                if (!OwnsDistrict(slot) || PopulationOf(slot) > 0)
                {
                    deadSince[slot] = Unknown;
                    continue;
                }
                if (deadSince[slot] == Unknown)
                {
                    deadSince[slot] = day;
                    continue;
                }
                if (day - deadSince[slot] < 1) continue;
                int? to = NearestLiving(slot, Enumerable.Range(0, ColonySlotTable.MaxSlots));
                if (to != null) Transfer(slot, to.Value, HandoverReason.Died);
            }
        }

        // The host: who is playing today (played everywhere), and whether a colony has been away too long.
        private void HostDaily(int day)
        {
            // Testing alone (debug, nobody connected), the host plays every colony (Ctrl+Shift+K): every colony counts
            // as present, so none is abandoned and none silently runs up missed days for the moment a guest connects.
            // With a guest connected, detailed logging changes nothing.
            bool alone = Settings.Debug && ((EventIO.Get() as ServerEventIO)?.NetBase?.ClientCount ?? 0) == 0;
            List<int> present = alone
                ? Enumerable.Range(0, ColonySlotTable.MaxSlots).Where(OwnsDistrict).ToList()
                : PresentSlots();
            ReplayEvent.DoPrefix(() => new ColonyPresenceEvent { day = day, presentSlots = present });
            // The host's setting, read each day, so it can be changed during a game.
            int limit = Settings.AbandonedColonyDaysValue;
            if (limit <= 0 || alone) return;
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                if (present.Contains(slot) || !OwnsDistrict(slot) || PopulationOf(slot) == 0 || requested.Contains(slot)) continue;
                int? away = DaysAway(slot);
                if (away == null || away.Value < limit) continue;
                int? to = NearestLiving(slot, present);
                if (to == null) continue;
                Plugin.Log($"[Colony] Slot {slot}'s player has missed {away} days: handing the colony to slot {to}");
                int from = slot, target = to.Value;
                // Asked for once; if it could not be sent (no replay service yet), it is asked for again tomorrow.
                bool notSent = ReplayEvent.DoPrefix(() => new ColonyHandoverEvent { fromSlot = from, toSlot = target, reason = (int)HandoverReason.Abandoned });
                if (!notSent) requested.Add(slot);
            }
        }

        /// <summary>
        /// Host: the colonies of the players in this session now: the host's, and every guest's that is still
        /// connected (one that left is away from that day on).
        /// </summary>
        public static List<int> PresentSlots()
        {
            List<int> connected = (EventIO.Get() as ServerEventIO)?.NetBase?.ConnectedPlayerIds;
            return (ColonySlotService.Instance?.Session ?? Enumerable.Empty<KeyValuePair<int, int>>())
                .Where(p => p.Key == ColonySession.HostPlayer || connected == null || connected.Contains(p.Key))
                .Select(p => p.Value)
                .Where(slot => slot >= 0).Distinct().OrderBy(slot => slot).ToList();
        }

        /// <summary>
        /// Played on every computer, once a day of hosted play: these colonies' players are in the game; every other
        /// colony's player has missed another day.
        /// </summary>
        public void Seen(IEnumerable<int> slots, int day)
        {
            var present = new HashSet<int>(slots);
            for (int slot = 0; slot < awayDays.Length; slot++)
            {
                if (present.Contains(slot) || !OwnsDistrict(slot)) awayDays[slot] = 0;
                else awayDays[slot]++;
            }
            ColonyDigest.Note("seen", day, present.Sum(slot => 1L << slot), awayDays.Sum(days => (long)days));
        }

        /// <summary>
        /// Host, from the colonies window: whether it may hand this colony over by hand (its player is away, or it has
        /// no beavers left).
        /// </summary>
        public bool HostMayHandOver(int from, int to) =>
            from != to && from >= 0 && to >= 0 && from < ColonySlotTable.MaxSlots && to < ColonySlotTable.MaxSlots
            && OwnsDistrict(from) && (!PresentSlots().Contains(from) || PopulationOf(from) == 0)
            // Not before the first tick: a player still joining would keep the old owner (ColonyRules.WaitsForStart).
            // Before it, every colony whose player is still loading looks away, which is exactly when this is wrong.
            && !ColonyRules.WaitsForStart(true, SingletonManager.GetSingleton<ReplayService>()?.TicksSinceLoad ?? 1);

        // ---- the handover itself (every computer, the same way) ----

        public void Transfer(int from, int to, HandoverReason reason)
        {
            if (from == to || from < 0 || to < 0 || from >= ColonySlotTable.MaxSlots || to >= ColonySlotTable.MaxSlots) return;
            if (!OwnsDistrict(from))
            {
                Plugin.LogWarning($"[Colony] Handover of slot {from} skipped: it has no districts");
                return;
            }
            if (reason != HandoverReason.ByHost && PopulationOf(to) == 0)
            {
                Plugin.LogWarning($"[Colony] Handover of slot {from} skipped: slot {to} has no beavers either");
                return;
            }
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.AllDistrictCenters.ToList())
            {
                DistrictOwner owner = districtCenter.GetComponent<DistrictOwner>();
                if (owner != null && owner.Slot == from) owner.SetSlot(to);
            }
            foreach (EntityComponent entity in _entityRegistry.Entities.ToList())
            {
                ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
                if (stamp != null && stamp.Slot == from) stamp.Stamp(to);
            }
            ColonyReach.Instance?.Transfer(from, to);
            ColonyMarks.Instance?.Transfer(from, to);
            ColonyScienceService.Instance?.Transfer(from, to);
            ColonyDigest.Note("handover", from, to, (int)reason);
            // A trading post between the two is now a crossing within one colony: its exchange ends, and what waited on
            // each half goes back home. (One that now joins other colonies than those that agreed ends at the next check.)
            foreach (DistrictCrossing crossing in _entityComponentRegistry.GetEnabled<DistrictCrossing>().ToList())
            {
                CrossingExchange exchange = ColonyExchangeService.Of(crossing);
                if (exchange == null || !exchange.IsOpen || TradingPosts.IsTradingPost(crossing)) continue;
                ColonyExchangeService.Instance?.End(crossing, TradingPosts.Partner(crossing), $"slot {from}'s colony was handed to slot {to}");
            }
            deadSince[from] = Unknown;
            awayDays[from] = 0;
            requested.Remove(from);
            Plugin.Log($"[Colony] Slot {from}'s colony handed to slot {to} ({reason})");
            ColonyScienceService.Instance?.RefreshToolLocks();
            Tell(from, to, reason);
        }

        private void Tell(int from, int to, HandoverReason reason)
        {
            try
            {
                int local = ColonySession.LocalSlot;
                string key;
                if (local == from)
                    key = reason == HandoverReason.Died ? "BeaverBuddies.Colony.Handover.YoursDied"
                        : reason == HandoverReason.Abandoned ? "BeaverBuddies.Colony.Handover.YoursAbandoned"
                        : "BeaverBuddies.Colony.Handover.YoursByHost";
                else if (local == to)
                    key = reason == HandoverReason.Died ? "BeaverBuddies.Colony.Handover.ReceivedDied"
                        : reason == HandoverReason.Abandoned ? "BeaverBuddies.Colony.Handover.ReceivedAbandoned"
                        : "BeaverBuddies.Colony.Handover.ReceivedByHost";
                else return;
                string other = ColonyExchangeService.ColonyName(local == from ? to : from);
                _colonyRulesService.ShowNotice(string.Format(RegisteredLocalizationService.T(key), other), warning: local == from);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not show a handover notice: " + error.Message);
            }
        }
    }

    /// <summary>
    /// The host, once a day: which colonies' players are in the game. Kept so absence can be counted. It also carries
    /// the host's colony check for the day (ColonyDiagnostics.Fingerprint), taken as the host plays it, and every
    /// guest takes its own at the same point and compares: colony state (owners, marks, science, exchanges, land) draws
    /// no random numbers, so a difference in it would otherwise show only once it changed some beaver's random draw,
    /// possibly days later and far from its cause, and might never show at all.
    /// </summary>
    [Serializable]
    public class ColonyPresenceEvent : ReplayEvent
    {
        public int day;
        public List<int> presentSlots;
        /// <summary>The host's colony check for the day, written as the host plays this; null from an older host.</summary>
        public string check;

        // Only the host sends it (see ColonyRulesService).
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context)
        {
            ColonyLifecycle.Instance?.Seen(presentSlots ?? new List<int>(), day);
            Compare();
        }

        private void Compare()
        {
            string here;
            try
            {
                here = ColonyDiagnostics.Instance?.Fingerprint();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not take the colony check for the day: " + error.Message);
                return;
            }
            if (here == null) return;
            if (EventIO.Get() is ServerEventIO || check == null)
            {
                // The host: its own is what the guests compare with. Taken after Seen, so both sides count the same.
                if (check == null) check = here;
                return;
            }
            if (check == here) return;
            Plugin.LogWarning($"[Colony] Colony state differs from the host's on day {day}: host [{check}] here [{here}]");
            SingletonManager.GetSingleton<ReplayService>()?.HandleDesync();
        }

        public override string ToActionString() => $"Colonies playing on day {day}: {string.Join(", ", presentSlots ?? new List<int>())}";
    }

    /// <summary>The host hands one colony to another (its player away too long, or by hand from the colonies window).</summary>
    [Serializable]
    public class ColonyHandoverEvent : ReplayEvent
    {
        public int fromSlot;
        public int toSlot;
        public int reason;

        // Only the host sends it (see ColonyRulesService).
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context) =>
            ColonyLifecycle.Instance?.Transfer(fromSlot, toSlot, (HandoverReason)reason);

        public override string ToActionString() => $"Handing slot {fromSlot}'s colony to slot {toSlot}";
    }
}
