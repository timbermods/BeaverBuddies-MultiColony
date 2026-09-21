using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.Buildings;
using Timberborn.Demolishing;
using Timberborn.Persistence;
using Timberborn.ScienceSystem;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
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
    /// stay unlocked for everyone (their unlocks are read while loading, before buildings know their district); their
    /// cost is paid from the pool of whoever unlocks them.
    /// </summary>
    public class ColonyScienceService : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey ScienceKey = new SingletonKey("BeaverBuddies.ColonyScience");
        private static readonly PropertyKey<bool> EnabledKey = new PropertyKey<bool>("Enabled");
        private static readonly ListKey<int> PointsKey = new ListKey<int>("Points");
        private static readonly ListKey<string>[] UnlockedKeys =
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(i => new ListKey<string>("Unlocked" + i)).ToArray();

        private readonly ISingletonLoader _singletonLoader;
        private readonly ScienceService _scienceService;
        private readonly BuildingUnlockingService _buildingUnlockingService;
        private readonly BuildingService _buildingService;
        private readonly ToolButtonService _toolButtonService;
        private readonly ToolUnlockingService _toolUnlockingService;

        private readonly int[] points = new int[ColonySlotTable.MaxSlots];
        // Sorted, so saving never depends on the order things were unlocked in.
        private readonly SortedSet<string>[] unlocked =
            Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(_ => new SortedSet<string>(StringComparer.Ordinal)).ToArray();

        public bool Enabled { get; private set; }

        public static ColonyScienceService Instance => SingletonManager.GetSingleton<ColonyScienceService>();

        public static bool IsEnabled => Instance?.Enabled == true;

        /// <summary>
        /// The slot the science being earned, spent or read right now belongs to. Set around simulation code and around
        /// replayed unlocks; null means "display", which is the local player's.
        /// </summary>
        [ThreadStatic] public static int? Context;

        /// <summary>The slot science is shown for on this computer.</summary>
        public static int DisplaySlot => Math.Max(0, ColonySession.LocalSlot);

        public ColonyScienceService(ISingletonLoader singletonLoader, ScienceService scienceService,
            BuildingUnlockingService buildingUnlockingService, BuildingService buildingService,
            ToolButtonService toolButtonService, ToolUnlockingService toolUnlockingService)
        {
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
                if (loader.Has(UnlockedKeys[i])) unlocked[i].UnionWith(loader.Get(UnlockedKeys[i]));
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
            Enabled = true;
            points[0] = shared;
            for (int i = 0; i < unlocked.Length; i++)
            {
                if (i == 0 || newGame) unlocked[i].UnionWith(sharedUnlocked);
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

        public void Add(int slot, int amount)
        {
            if (slot < 0 || slot >= points.Length) slot = 0;
            points[slot] += amount;
        }

        public void Subtract(int slot, int amount)
        {
            if (slot < 0 || slot >= points.Length) slot = 0;
            if (points[slot] - amount < 0)
                throw new ArgumentException($"Can't subtract {amount} science points from slot {slot}, there are only {points[slot]}");
            points[slot] -= amount;
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
            return unlocked[slot].Add(_buildingService.GetTemplateName(spec));
        }

        /// <summary>
        /// Display: lock and unlock the toolbar for the local player's colony. The game sets tool locks once, while
        /// loading; the local slot is known later (a guest is seated after joining) and can change (debug), so this
        /// sets them again.
        /// </summary>
        public void RefreshToolLocks()
        {
            try
            {
                foreach (ToolButton toolButton in _toolButtonService.ToolButtons)
                {
                    if (!(toolButton.Tool is BlockObjectTool tool)) continue;
                    BuildingSpec spec = tool.Template?.GetSpec<BuildingSpec>();
                    // Buildings the game locks for science, and the District Crossing, which needs none in a
                    // separate-colonies game (the toolbar locked it while loading, before the mode began).
                    if (spec == null || (spec.ScienceCost == 0 && !TradingPostCost.IsCrossing(spec))) continue;
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
            __result = service.PointsOf(ColonyScienceService.Context ?? ColonyScienceService.DisplaySlot);
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
            // Earned outside any colony's building (a dev tool): this player's. Simulation callers name their colony.
            service.Add(ColonyScienceService.Context ?? ColonyScienceService.DisplaySlot, amount);
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
            service.Subtract(ColonyScienceService.Context ?? ColonyScienceService.DisplaySlot, amount);
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

    // ---- unlocks per slot ----

    [HarmonyPatch(typeof(BuildingUnlockingService), nameof(BuildingUnlockingService.Unlocked))]
    static class ColonyUnlockedPatcher
    {
        static bool Prefix(BuildingSpec buildingSpec, ref bool __result)
        {
            var service = ColonyScienceService.Instance;
            if (service == null || !service.Enabled) return true;
            __result = service.IsUnlockedFor(ColonyScienceService.Context ?? ColonyScienceService.DisplaySlot, buildingSpec);
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
            int slot = ColonyScienceService.Context ?? ColonyScienceService.DisplaySlot;
            service.RecordUnlock(slot, buildingSpec);
            return slot == ColonyScienceService.DisplaySlot;
        }
    }
}
