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
    /// district centers, buildings, marks, stock and science pool (its unlocks are shared, not taken away). Its
    /// player, left with no colony, may found a new one at once (Ctrl+K).
    ///
    /// - A colony with no beavers or bots left for a whole day goes to the nearest living colony. Decided in the
    ///   simulation, the same on every computer.
    /// - A colony whose player has missed a number of in-game days of hosted co-op play (host setting, 7 by default)
    ///   goes to the nearest colony whose player is playing. Decided by the host and played everywhere as an action.
    ///   Only days of a hosted game count, from the day after it was loaded (so a returning player has time to join),
    ///   and a day the player is in the game starts the count again. It is always announced the day before, in the
    ///   same session, also when the count passed the limit while a steward in the game looked after it.
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
        // Session: the players in the game as the host last said, by stable id, and the host's hand-over limit.
        private readonly HashSet<string> presentPlayerIds = new HashSet<string>();
        // Session: the colonies whose hand-over for absence the last day's presence announced (every computer works it out
        // alike as the presence is played; the host hands over only these, the next day). Not saved: after a load the
        // first presence announces again, so the players in this session are warned first.
        private readonly bool[] announced = new bool[ColonySlotTable.MaxSlots];

        /// <summary>The host's "hand over after days away" setting as it last told everyone; -1 until it has, 0 for never.</summary>
        public int HandoverLimit { get; private set; } = -1;

        /// <summary>Whether a player (by stable id) was in the game when the host last said who is.</summary>
        public bool IsPlayerPresent(string playerId) => playerId != null && presentPlayerIds.Contains(playerId);

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
            // Separate colonies only: a shared game's save holds only what the Stability Fork's does.
            if (!ColonyModeService.IsSeparateColonies) return;
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
            var living = new List<(int slot, long distance)>();
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
                living.Add((slot, distance));
                if (distance < bestDistance)
                {
                    best = slot;
                    bestDistance = distance;
                }
            }
            // A mixed-factions game: the nearest colony of the same faction first (D21), whose beavers and buildings are
            // that faction's; else the nearest. Each colony's faction is saved state, the same on every computer.
            if (BeaverBuddies.Factions.MixedFactions.IsOn)
                return BeaverBuddies.Factions.FactionRules.PreferSameFaction(living, BeaverBuddies.Factions.ColonyFactionService.FactionOfSlot,
                    BeaverBuddies.Factions.ColonyFactionService.FactionOfSlot(from));
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
                int unstamped = ColonyStamps.Instance?.Unstamped ?? 0;
                int exchanges = _entityComponentRegistry.GetEnabled<DistrictCrossing>()
                    .Count(half => ColonyExchangeService.Of(half)?.IsActive == true && ColonyExchangeService.Of(half).ProposedHere);
                for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
                {
                    if (!OwnsDistrict(slot)) continue;
                    Plugin.Log($"[Colony] Day {day}: slot {slot} has {PopulationOf(slot)} beavers and bots, its player missed {awayDays[slot]} days");
                }
                Plugin.Log($"[Colony] Day {day}: {unstamped} buildings waiting for a colony, {exchanges} exchanges running");
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
            List<string> presentIds = PresentPlayerIds();
            // The host's setting, read each day, so it can be changed during a game; told to everyone with the day.
            int limit = Settings.AbandonedColonyDaysValue;
            ReplayEvent.DoPrefix(() => new ColonyPresenceEvent { day = day, presentSlots = present, presentPlayerIds = presentIds, limit = limit });
            if (limit <= 0 || alone) return;
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                if (present.Contains(slot) || !OwnsDistrict(slot) || PopulationOf(slot) == 0 || requested.Contains(slot)) continue;
                // A colony looked after by a player who is in the game is not handed over for its own player's absence.
                bool keptBySteward = ColonyStewards.Instance?.IsLookedAfterBy(slot, presentIds) == true;
                int? away = DaysAway(slot);
                // Only a colony the last day's presence announced (E-3): a steward's colony whose count passed the limit
                // while they looked after it used to go, unwarned, on the first day they did not play.
                if (away == null || !ColonyAbsence.IsHandedOver(away.Value, limit, keptBySteward, announced[slot])) continue;
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

        /// <summary>Host: the stable ids of the players in this session now (the host's, and every guest still connected).</summary>
        public static List<string> PresentPlayerIds()
        {
            List<int> connected = (EventIO.Get() as ServerEventIO)?.NetBase?.ConnectedPlayerIds;
            ColonySlotService slots = ColonySlotService.Instance;
            if (slots == null) return new List<string>();
            return slots.Players
                .Where(p => p.player == ColonySession.HostPlayer || connected == null || connected.Contains(p.player))
                .Select(p => p.id).Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Played on every computer, once a day of hosted play: these colonies' players are in the game; every other
        /// colony's player has missed another day. A colony whose count has reached the host's limit, and that nobody keeps
        /// today (its player or its steward), is announced, so the hand-over the next day surprises nobody.
        /// </summary>
        public void Seen(IEnumerable<int> slots, int day, IEnumerable<string> playerIds = null, int limit = -1)
        {
            var present = new HashSet<int>(slots);
            presentPlayerIds.Clear();
            if (playerIds != null) foreach (string id in playerIds) presentPlayerIds.Add(id);
            HandoverLimit = limit;
            for (int slot = 0; slot < awayDays.Length; slot++)
            {
                if (present.Contains(slot) || !OwnsDistrict(slot)) awayDays[slot] = 0;
                else awayDays[slot]++;
            }
            ColonyDigest.Note("seen", day, present.Sum(slot => 1L << slot), awayDays.Sum(days => (long)days));
            // Which colonies the next day's check hands over (the host) and which to warn about now (everyone): those due
            // that nobody keeps today, newly so. Read from what this presence says, the same on every computer.
            var newlyAnnounced = new bool[announced.Length];
            for (int slot = 0; slot < announced.Length; slot++)
            {
                bool kept = present.Contains(slot) || ColonyStewards.Instance?.IsLookedAfter(slot) == true;
                bool now = OwnsDistrict(slot) && PopulationOf(slot) > 0 && ColonyAbsence.IsAnnounced(awayDays[slot], limit, kept);
                newlyAnnounced[slot] = now && !announced[slot];
                announced[slot] = now;
            }
            WarnBeforeHandover(newlyAnnounced);
        }

        // Display only: the hand-over comes with the next day's check (ColonyAbsence.IsHandedOver).
        private void WarnBeforeHandover(bool[] newlyAnnounced)
        {
            try
            {
                int local = ColonySession.LocalSeat;
                if (local < 0) return;
                for (int slot = 0; slot < newlyAnnounced.Length; slot++)
                {
                    if (slot == local || !newlyAnnounced[slot]) continue;
                    _colonyRulesService.ShowNotice(string.Format(RegisteredLocalizationService.T("BeaverBuddies.Colony.Handover.Tomorrow"),
                        ColonyExchangeService.ColonyName(slot), awayDays[slot]), warning: true);
                }
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not warn of a hand-over: " + error.Message);
            }
        }

        /// <summary>
        /// Host, from the colonies window: whether it may hand this colony over by hand (its player is away, or it has
        /// no beavers left).
        /// </summary>
        public bool HostMayHandOver(int from, int to) =>
            from != to && from >= 0 && to >= 0 && from < ColonySlotTable.MaxSlots && to < ColonySlotTable.MaxSlots
            && OwnsDistrict(from) && (!PresentSlots().Contains(from) || PopulationOf(from) == 0)
            // Not before the first tick: a player still joining would keep the old owner (ColonyRules.WaitsForStart).
            // Before it, every colony whose player is still loading looks away, which is exactly when this is wrong. That
            // holds after a waiting room too (its guests are still loading at tick 0), so these buttons always wait.
            && !ColonyRules.WaitsForStart(true, SingletonManager.GetSingleton<ReplayService>()?.TicksSinceLoad ?? 1,
                joiningClosedAtStart: false);

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
            ColonyMarks.Instance?.Transfer(from, to);
            // Its beavers without a district join the new owner's districts (E-8).
            ColonyCitizens.Instance?.Transfer(from, to);
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
            // Whoever looked after it looks after nothing now: it is another player's colony.
            ColonyStewards.Instance?.Revoke(from, "the colony was handed over");
            deadSince[from] = Unknown;
            awayDays[from] = 0;
            announced[from] = false;
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
    /// guest takes its own at the same point and compares: colony state (owners, marks, science, exchanges) draws
    /// no random numbers, so a difference in it would otherwise show only once it changed some beaver's random draw,
    /// possibly days later and far from its cause, and might never show at all.
    /// </summary>
    [Serializable]
    public class ColonyPresenceEvent : ReplayEvent
    {
        public int day;
        public List<int> presentSlots;
        /// <summary>The same players by stable id, so every computer knows whether a colony's steward is in the game.</summary>
        public List<string> presentPlayerIds;
        /// <summary>The host's "hand over after days away" setting (0 for never), so every computer can say how close a hand-over is.</summary>
        public int limit = -1;
        /// <summary>The host's colony check for the day, written as the host plays this; null from an older host.</summary>
        public string check;

        // Only the host sends it (see ColonyRulesService).
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context)
        {
            ColonyLifecycle.Instance?.Seen(presentSlots ?? new List<int>(), day, presentPlayerIds, limit);
            Compare();
        }

        private void Compare()
        {
            string here;
            try
            {
                // The day's only walk over every entity on each computer, logged there too (1.4.0-rc1 review, D-S12).
                here = ColonyDiagnostics.Instance?.DailyCheck(day);
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
            string line = $"[Colony] Colony state differs from the host's on day {day}: host [{check}] here [{here}]";
            Plugin.LogWarning(line);
            // The line travels as the desync's trace, so the host's log and a report say what differed.
            SingletonManager.GetSingleton<ReplayService>()?.HandleDesync(line);
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
