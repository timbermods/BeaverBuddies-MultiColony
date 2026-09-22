using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Demolishing;
using Timberborn.Persistence;
using Timberborn.ScienceSystem;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using Timberborn.WorkSystem;
using Timberborn.Workshops;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Separate science and building unlocks per colony. Each slot has its own science pool and its own set of
    /// unlocked buildings, saved with the game and identical on every computer; only what is shown follows the local
    /// player. Chosen when a separate-colonies game begins (new game, or the first founding in a shared game) and fixed
    /// for the life of the save. Off: the game's single pool and single set, exactly as before.
    ///
    /// The game keeps one pool in ScienceService. Every read, add and subtract goes through it, so each is sent to a
    /// slot: simulation code that earns, spends or reads science names the slot of the building doing it (a context
    /// set around the call, below); everything else is display and reads the local player's pool. Bot worker types
    /// ("bots may work here") are unlocked per colony too: a workplace asks for its own colony's.
    /// </summary>
    public class ColonyScienceService : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton, IPostLoadableSingleton, IResettableSingleton
    {
        private static readonly SingletonKey ScienceKey = new SingletonKey("BeaverBuddies.ColonyScience");
        private static readonly PropertyKey<bool> EnabledKey = new PropertyKey<bool>("Enabled");
        private static readonly ListKey<int> PointsKey = new ListKey<int>("Points");
        private static readonly ListKey<string>[] UnlockedKeys =
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(i => new ListKey<string>("Unlocked" + i)).ToArray();
        private static readonly ListKey<string>[] WorkerKeys =
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(i => new ListKey<string>("Workers" + i)).ToArray();

        /// <summary>A context meaning "any colony": a workplace whose colony is not known (an older save, loading).</summary>
        public const int AnyColony = -2;

        private readonly ISingletonLoader _singletonLoader;
        private readonly ScienceService _scienceService;
        private readonly BuildingUnlockingService _buildingUnlockingService;
        private readonly BuildingService _buildingService;
        private readonly ToolButtonService _toolButtonService;
        private readonly ToolUnlockingService _toolUnlockingService;
        private readonly WorkplaceUnlockingService _workplaceUnlockingService;

        private readonly int[] points = new int[ColonySlotTable.MaxSlots];
        // Sorted, so saving never depends on the order things were unlocked in.
        private readonly SortedSet<string>[] unlocked =
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(_ => new SortedSet<string>(StringComparer.Ordinal)).ToArray();
        // Bot worker types per colony, as "workplace|worker type".
        private readonly SortedSet<string>[] workerUnlocked =
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(_ => new SortedSet<string>(StringComparer.Ordinal)).ToArray();
        private bool workerSetsLoaded;

        // Read around every producing workshop's tick and every bot worker-type check: a static read, following the
        // running game's service (see ColonyModeService.IsSeparateColonies for the same arrangement).
        private static bool enabledNow;
        private bool enabled;

        public bool Enabled
        {
            get => enabled;
            private set
            {
                enabled = value;
                enabledNow = value;
            }
        }

        /// <summary>
        /// The per-colony bot worker types exist: loaded, or made when separate science began. A save from before
        /// them has none until PostLoad copies the game's in, and workplaces read theirs while loading, before that:
        /// until then the game's own set answers (the one the copies come from).
        /// </summary>
        public bool WorkerSetsReady => workerSetsLoaded;

        public static ColonyScienceService Instance => SingletonManager.GetSingleton<ColonyScienceService>();

        public static bool IsEnabled => enabledNow;

        public void Reset()
        {
            enabledNow = false;
        }

        /// <summary>
        /// The slot the science being earned, spent or read right now belongs to. Set around simulation code and around
        /// replayed unlocks; null means "display", which is the local player's.
        /// </summary>
        [ThreadStatic] public static int? Context;

        /// <summary>The slot science is shown for on this computer.</summary>
        public static int DisplaySlot => Math.Max(0, ColonySession.LocalSlot);

        /// <summary>
        /// The slot for science named by no one. Outside the simulation that is the display's: this computer's player.
        /// Inside it (a tick, or a replayed action) the local player must never decide, or each computer would pay its
        /// own colony: there it is <see cref="UnnamedSimulationSlot"/> on every computer, with a warning, since every
        /// simulation caller is meant to name its colony.
        /// </summary>
        public static int ActingSlot =>
            Context ?? SlotWithoutContext(DeterminismService.IsTicking || ReplayService.IsReplayingEvents, DisplaySlot);

        /// <summary>Whose science it is when simulation code names no colony: the first colony's, on every computer.</summary>
        public const int UnnamedSimulationSlot = 0;

        // One warning is enough to find the caller; more would flood the log from inside a tick.
        private static bool warnedUnnamed;

        internal static int SlotWithoutContext(bool inSimulation, int displaySlot)
        {
            if (!inSimulation) return displaySlot;
            if (!warnedUnnamed)
            {
                warnedUnnamed = true;
                Plugin.LogWarning("[Colony] Science was read or changed in the simulation without naming a colony; " +
                    $"counted as slot {UnnamedSimulationSlot}'s on every computer. Caller: " + Environment.StackTrace);
            }
            return UnnamedSimulationSlot;
        }

        private readonly TemplateNameMapper _templateNameMapper;

        public ColonyScienceService(ISingletonLoader singletonLoader, ScienceService scienceService,
            BuildingUnlockingService buildingUnlockingService, BuildingService buildingService,
            ToolButtonService toolButtonService, ToolUnlockingService toolUnlockingService,
            WorkplaceUnlockingService workplaceUnlockingService, TemplateNameMapper templateNameMapper)
        {
            enabledNow = false;
            _templateNameMapper = templateNameMapper;
            _workplaceUnlockingService = workplaceUnlockingService;
            _singletonLoader = singletonLoader;
            _scienceService = scienceService;
            _buildingUnlockingService = buildingUnlockingService;
            _buildingService = buildingService;
            _toolButtonService = toolButtonService;
            _toolUnlockingService = toolUnlockingService;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(ScienceKey, out IObjectLoader loader)) return;
            Enabled = loader.Has(EnabledKey) && loader.Get(EnabledKey);
            if (loader.Has(PointsKey))
            {
                List<int> saved = loader.Get(PointsKey);
                for (int i = 0; i < points.Length && i < saved.Count; i++) points[i] = saved[i];
            }
            for (int i = 0; i < UnlockedKeys.Length; i++)
            {
                // Through the game's name mapper, as the game reads its own set: a building renamed by a game
                // update keeps its unlock.
                if (loader.Has(UnlockedKeys[i])) unlocked[i].UnionWith(loader.Get(UnlockedKeys[i]).Select(CurrentName));
            }
            for (int i = 0; i < WorkerKeys.Length; i++)
            {
                if (!loader.Has(WorkerKeys[i])) continue;
                workerUnlocked[i].UnionWith(loader.Get(WorkerKeys[i]));
                workerSetsLoaded = true;
            }
            if (Enabled) Plugin.Log($"[Colony] Separate science: pools {string.Join(", ", points)}");
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            if (!Enabled) return;
            IObjectSaver saver = singletonSaver.GetSingleton(ScienceKey);
            saver.Set(EnabledKey, true);
            saver.Set(PointsKey, points.ToList());
            for (int i = 0; i < UnlockedKeys.Length; i++) saver.Set(UnlockedKeys[i], unlocked[i].ToList());
            for (int i = 0; i < WorkerKeys.Length; i++) saver.Set(WorkerKeys[i], workerUnlocked[i].ToList());
        }

        // A save made before bot worker types were per colony: every colony keeps the ones the game had.
        public void PostLoad()
        {
            if (!Enabled || workerSetsLoaded) return;
            List<string> shared = SharedWorkerTypes();
            foreach (SortedSet<string> set in workerUnlocked) set.UnionWith(shared);
            workerSetsLoaded = true;
        }

        private List<string> SharedWorkerTypes() =>
            _workplaceUnlockingService._unlockedWorkerTypes.Select(WorkerKey).OrderBy(key => key, StringComparer.Ordinal).ToList();

        private string CurrentName(string templateName)
        {
            try { return _templateNameMapper.TryGetTemplate(templateName, out TemplateSpec spec) ? spec.TemplateName : templateName; }
            catch (Exception) { return templateName; }
        }

        private static string WorkerKey(UnlockableWorkerType workerType) => workerType.WorkplaceTemplateName + "|" + workerType.WorkerType;

        /// <summary>Whether a colony (or <see cref="AnyColony"/>) has unlocked this worker type for this workplace.</summary>
        public bool IsWorkerTypeUnlocked(int slot, UnlockableWorkerType workerType)
        {
            string key = WorkerKey(workerType);
            if (slot == AnyColony) return workerUnlocked.Any(set => set.Contains(key));
            return slot >= 0 && slot < workerUnlocked.Length && workerUnlocked[slot].Contains(key);
        }

        /// <summary>
        /// A colony handed over: its science pool goes with it, and the new owner may build whatever either had
        /// unlocked. The old owner keeps its unlocks (what its player learned stays theirs if they found again).
        /// </summary>
        public void Transfer(int from, int to)
        {
            if (!Enabled || from == to || from < 0 || to < 0 || from >= points.Length || to >= points.Length) return;
            points[to] += points[from];
            points[from] = 0;
            unlocked[to].UnionWith(unlocked[from]);
            workerUnlocked[to].UnionWith(workerUnlocked[from]);
            ColonyDigest.Note("science-transfer", from, to, points[to]);
        }

        public void UnlockWorkerType(int slot, UnlockableWorkerType workerType)
        {
            if (slot < 0 || slot >= workerUnlocked.Length) slot = 0;
            workerUnlocked[slot].Add(WorkerKey(workerType));
            ColonyDigest.Note("worker-unlock", slot, ColonyDigest.Of(WorkerKey(workerType)));
        }

        /// <summary>
        /// Separate science begins: the game's pool and unlocks so far become slot 0's (the first colony's). In a new
        /// game every colony starts with the same unlocks; in a shared game being split, the new colonies start with none.
        /// </summary>
        public void Enable(bool newGame)
        {
            if (Enabled) return;
            int shared = _scienceService.SciencePoints;
            // Not the buildings the game remembers in each player's own profile (UnlockableOnceSpec): those differ
            // between computers, and this runs on every computer.
            List<string> sharedUnlocked = _buildingUnlockingService._unlockedBuildings
                .Where(name => !IsUnlockableOnce(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();
            List<string> sharedWorkers = SharedWorkerTypes();
            Enabled = true;
            workerSetsLoaded = true;
            points[0] = shared;
            for (int i = 0; i < unlocked.Length; i++)
            {
                if (i == 0 || newGame) unlocked[i].UnionWith(sharedUnlocked);
                if (i == 0 || newGame) workerUnlocked[i].UnionWith(sharedWorkers);
            }
            Plugin.Log($"[Colony] Separate science switched on: slot 0 keeps {shared} science and {sharedUnlocked.Count} unlocks");
            RefreshToolLocks();
        }

        private bool IsUnlockableOnce(string templateName)
        {
            try { return _buildingService.GetBuildingTemplate(templateName)?.HasSpec<UnlockableOnceSpec>() == true; }
            catch (Exception) { return false; }
        }

        public int PointsOf(int slot) => slot >= 0 && slot < points.Length ? points[slot] : 0;

        /// <summary>Diagnostics: each colony's science, and how many buildings and bot worker types it has unlocked.</summary>
        public string Fingerprint() => !Enabled ? "shared" : string.Join(" ",
            Enumerable.Range(0, points.Length).Select(i => $"{i}:{points[i]}/{unlocked[i].Count}u{(uint)Names(unlocked[i]):x}/{workerUnlocked[i].Count}w{(uint)Names(workerUnlocked[i]):x}"));

        // The sets are sorted, so the hash is of the names, not of the order they were unlocked in.
        private static long Names(SortedSet<string> names)
        {
            long hash = 0;
            foreach (string name in names) hash = hash * 31 + ColonyDigest.Of(name);
            return hash;
        }

        public void Add(int slot, int amount)
        {
            if (slot < 0 || slot >= points.Length) slot = 0;
            points[slot] += amount;
            ColonyDigest.Note("science", slot, amount, points[slot]);
        }

        public void Subtract(int slot, int amount)
        {
            if (slot < 0 || slot >= points.Length) slot = 0;
            if (points[slot] - amount < 0)
                throw new ArgumentException($"Can't subtract {amount} science points from slot {slot}, there are only {points[slot]}");
            points[slot] -= amount;
            ColonyDigest.Note("science-spend", slot, amount, points[slot]);
        }

        public bool IsUnlockedFor(int slot, string templateName)
        {
            if (!Enabled || string.IsNullOrEmpty(templateName)) return true;
            BuildingSpec spec;
            try { spec = _buildingService.GetBuildingTemplate(templateName); }
            catch (Exception) { return true; }
            return spec == null || IsUnlockedFor(slot, spec);
        }

        public bool IsUnlockedFor(int slot, BuildingSpec spec)
        {
            if (!Enabled || spec.ScienceCost == 0) return true;
            if (slot < 0 || slot >= unlocked.Length) return false;
            return unlocked[slot].Contains(_buildingService.GetTemplateName(spec));
        }

        /// <summary>Records an unlock for a slot. True if it was new.</summary>
        public bool RecordUnlock(int slot, BuildingSpec spec)
        {
            if (slot < 0 || slot >= unlocked.Length) slot = 0;
            string name = _buildingService.GetTemplateName(spec);
            bool added = unlocked[slot].Add(name);
            if (added) ColonyDigest.Note("unlock", slot, ColonyDigest.Of(name));
            return added;
        }

        /// <summary>
        /// Display: lock and unlock the toolbar for the local player's colony. The game sets tool locks once, while
        /// loading; the local slot is known later (a guest is seated after joining) and can change (debug), so this
        /// sets them again. A shared game keeps the game's own locks, as the Stability Fork.
        /// </summary>
        public void RefreshToolLocks()
        {
            if (!ColonyModeService.IsSeparateColonies) return;
            // Display code that also runs inside replays (a hello, a founding, a handover): it names the local
            // player's colony itself, since nothing unnamed is the local player's there (see ActingSlot).
            int? previous = Context;
            Context = DisplaySlot;
            try
            {
                foreach (ToolButton toolButton in _toolButtonService.ToolButtons)
                {
                    if (!(toolButton.Tool is BlockObjectTool tool)) continue;
                    BuildingSpec spec = tool.Template?.GetSpec<BuildingSpec>();
                    // Buildings the game locks for science (the Trading Post needs none).
                    if (spec == null || spec.ScienceCost == 0) continue;
                    bool unlockedHere = _buildingUnlockingService.Unlocked(spec);
                    bool locked = _toolUnlockingService.IsLocked(tool);
                    if (unlockedHere && locked) _toolUnlockingService.Unlock(tool);
                    else if (!unlockedHere && !locked) _toolUnlockingService.LockIfNeeded(tool);
                }
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not refresh the toolbar's locks: " + error.Message);
            }
            finally
            {
                Context = previous;
            }
        }

        /// <summary>Runs <paramref name="action"/> with science counted as <paramref name="slot"/>'s.</summary>
        public static T InSlot<T>(int slot, Func<T> action)
        {
            int? previous = Context;
            Context = slot;
            try { return action(); }
            finally { Context = previous; }
        }

        public static void InSlot(int slot, Action action) => InSlot(slot, () => { action(); return 0; });
    }

    // ---- the game's single pool, sent to slots ----

    [HarmonyPatch(typeof(ScienceService), nameof(ScienceService.SciencePoints), MethodType.Getter)]
    static class ColonySciencePointsPatcher
    {
        static bool Prefix(ref int __result)
        {
            var service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            __result = service.PointsOf(ColonyScienceService.ActingSlot);
            return false;
        }
    }

    [HarmonyPatch(typeof(ScienceService), nameof(ScienceService.AddPoints))]
    static class ColonyScienceAddPatcher
    {
        static bool Prefix(int amount)
        {
            var service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            // Earned outside any colony's building (a dev tool): this player's. Simulation callers name their colony
            // (see ActingSlot for one that does not).
            service.Add(ColonyScienceService.ActingSlot, amount);
            return false;
        }
    }

    [HarmonyPatch(typeof(ScienceService), nameof(ScienceService.SubtractPoints))]
    static class ColonyScienceSubtractPatcher
    {
        static bool Prefix(int amount)
        {
            var service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            service.Subtract(ColonyScienceService.ActingSlot, amount);
            return false;
        }
    }

    // ---- whose science it is, around the simulation code that earns, spends or reads it ----

    static class ColonyScienceContext
    {
        public static void Enter(BaseComponent component, out int? previous)
        {
            previous = ColonyScienceService.Context;
            if (ColonyScienceService.IsEnabled) ColonyScienceService.Context = DistrictOwner.OwnerOf(component) ?? 0;
        }

        public static void Exit(int? previous) => ColonyScienceService.Context = previous;
    }

    // Science produced by a building (inventors, the Numbercruncher, the observatory) goes to its colony.
    [HarmonyPatch(typeof(Manufactory), nameof(Manufactory.IncreaseProductionProgress))]
    static class ColonyScienceManufactoryPatcher
    {
        static void Prefix(Manufactory __instance, out int? __state) => ColonyScienceContext.Enter(__instance, out __state);
        static void Finalizer(int? __state) => ColonyScienceContext.Exit(__state);
    }

    // A building that uses up science (the Iron Teeth control tower) draws on its colony's pool.
    [HarmonyPatch(typeof(ScienceNeedingBuilding), nameof(ScienceNeedingBuilding.Tick))]
    static class ColonyScienceNeedingBuildingPatcher
    {
        static void Prefix(ScienceNeedingBuilding __instance, out int? __state) => ColonyScienceContext.Enter(__instance, out __state);
        static void Finalizer(int? __state) => ColonyScienceContext.Exit(__state);
    }

    // An automation science counter reads its colony's pool.
    [HarmonyPatch(typeof(ScienceCounter), nameof(ScienceCounter.Sample))]
    static class ColonyScienceCounterPatcher
    {
        static void Prefix(ScienceCounter __instance, out int? __state) => ColonyScienceContext.Enter(__instance, out __state);
        static void Finalizer(int? __state) => ColonyScienceContext.Exit(__state);
    }

    // A relic's science reward goes to the colony of the beaver who demolished it.
    [HarmonyPatch(typeof(Demolisher), nameof(Demolisher.Demolish))]
    static class ColonyScienceDemolisherPatcher
    {
        static void Prefix(Demolisher __instance, out int? __state) => ColonyScienceContext.Enter(__instance, out __state);
        static void Finalizer(int? __state) => ColonyScienceContext.Exit(__state);
    }

    // A relic's reward is paid whenever it is deleted fully demolished, not only by its demolisher: a tunnel's blast
    // or a collapse can delete one that stands at 100 %. Then no beaver names the colony, so the relic does: the
    // colony whose mark or land it stands on, else the first. A demolisher's own context, when there is one, stays.
    [HarmonyPatch(typeof(DemolishableScienceReward), nameof(DemolishableScienceReward.DeleteEntity))]
    static class ColonyScienceRelicRewardPatcher
    {
        static void Prefix(DemolishableScienceReward __instance, out int? __state)
        {
            __state = ColonyScienceService.Context;
            if (__state != null || !ColonyScienceService.IsEnabled) return;
            ColonyScienceService.Context = ColonySeparation.NaturalOwnerOf(__instance.GetComponent<BlockObject>())
                ?? ColonyScienceService.UnnamedSimulationSlot;
        }

        static void Finalizer(int? __state) => ColonyScienceService.Context = __state;
    }

    // ---- unlocks per slot ----

    [HarmonyPatch(typeof(BuildingUnlockingService), nameof(BuildingUnlockingService.Unlocked))]
    static class ColonyUnlockedPatcher
    {
        static bool Prefix(BuildingSpec buildingSpec, ref bool __result)
        {
            var service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            __result = service.IsUnlockedFor(ColonyScienceService.ActingSlot, buildingSpec);
            return false;
        }
    }

    // An unlock is recorded for the slot doing it. Only the local player's own unlocks reach the game's shared set
    // (and its event, which the toolbar and planting tools listen to): another colony's unlock must not unlock
    // anything on this screen.
    [HarmonyPatch(typeof(BuildingUnlockingService), nameof(BuildingUnlockingService.UnlockIgnoringCost))]
    static class ColonyUnlockIgnoringCostPatcher
    {
        static bool Prefix(BuildingSpec buildingSpec)
        {
            var service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            int slot = ColonyScienceService.ActingSlot;
            service.RecordUnlock(slot, buildingSpec);
            return slot == ColonyScienceService.DisplaySlot;
        }
    }

    // ---- bot worker types, per colony ----

    // Whether bots may work in a workplace: the colony asking's own unlocks (a workplace, below, or a replayed unlock),
    // else the local player's (display). Free worker types stay free, as in the game.
    [HarmonyPatch(typeof(WorkplaceUnlockingService), nameof(WorkplaceUnlockingService.Unlocked))]
    static class ColonyWorkerTypeUnlockedPatcher
    {
        static bool Prefix(WorkplaceUnlockingService __instance, UnlockableWorkerType unlockableWorkerType, ref bool __result)
        {
            ColonyScienceService service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled || !service.WorkerSetsReady
                || __instance.GetUnlockCost(unlockableWorkerType) <= 0) return true;
            __result = service.IsWorkerTypeUnlocked(ColonyScienceService.ActingSlot, unlockableWorkerType);
            return false;
        }
    }

    // An unlock goes to the colony paying for it (the replayed unlock sets it), never to everyone.
    [HarmonyPatch(typeof(WorkplaceUnlockingService), nameof(WorkplaceUnlockingService.UnlockIgnoringCost))]
    static class ColonyWorkerTypeUnlockPatcher
    {
        static bool Prefix(UnlockableWorkerType unlockableWorkerType)
        {
            ColonyScienceService service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            service.UnlockWorkerType(ColonyScienceService.ActingSlot, unlockableWorkerType);
            return false;
        }
    }

    // A workplace asks for its own colony's unlocks (from simulation state: its district, or the colony that placed
    // it). One whose colony is not known yet, from an older save while loading, keeps what any colony unlocked.
    [HarmonyPatch(typeof(WorkplaceWorkerType), nameof(WorkplaceWorkerType.IsWorkerTypeUnlocked))]
    static class ColonyWorkplaceWorkerTypePatcher
    {
        static void Prefix(WorkplaceWorkerType __instance, out int? __state)
        {
            __state = ColonyScienceService.Context;
            if (ColonyScienceService.IsEnabled)
                ColonyScienceService.Context = ColonySeparation.SimOwnerOf(__instance) ?? ColonyScienceService.AnyColony;
        }

        static void Finalizer(int? __state) => ColonyScienceService.Context = __state;
    }
}
