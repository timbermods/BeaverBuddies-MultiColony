using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.Demolishing;
using Timberborn.DistributionSystem;
using Timberborn.GameDistricts;
using Timberborn.GoodStackSystem;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.Planting;
using Timberborn.RecoveredGoodSystem;
using Timberborn.YielderFinding;
using Timberborn.Yielding;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Keeps each colony's beavers to their own colony's work. The game hands out some work map-wide, to any beaver
    /// who can walk there: trees to cut, bushes and crops to harvest, ruins to scavenge, planting spots, construction
    /// sites, things to demolish and piles of goods. Two colonies' beavers can walk to the same places, so what a colony
    /// marked or placed is its own workers' alone. What nobody marked or placed (a wild bush, a ruin, a pile) is there
    /// for whichever colony's workers get to it: there is no land.
    ///
    /// Everything these read is the same on every computer (districts, saved owners and marks), and they run inside
    /// the simulation, so every computer makes the same choices. With separate colonies off they do nothing.
    /// </summary>
    public static class ColonySeparation
    {
        public static bool Active => ColonyModeService.IsSeparateColonies;

        internal static readonly ColonyProfiler.Spot ResourceSearches = ColonyProfiler.Declare("Resource searches (lumberjacks, gatherers...)");
        internal static readonly ColonyProfiler.Spot BuilderChecks = ColonyProfiler.Declare("Builder job checks");

        /// <summary>
        /// The colony of a worker, workplace or site, from simulation state only: a beaver by its district, a building
        /// by its finished district, else the colony that placed it.
        /// </summary>
        public static int? SimOwnerOf(BaseComponent component) => DistrictOwner.OwnerOf(component, useConstructionDistrict: false);

        /// <summary>
        /// Whose a thing with no district is (trees, bushes, crops, ruins, piles): the colony whose planting or cutting
        /// mark it stands on, else nobody's.
        /// </summary>
        public static int? NaturalOwnerOf(BlockObject blockObject)
        {
            if (!blockObject) return null;
            Vector3Int tile = blockObject.Coordinates;
            ColonyMarks marks = ColonyMarks.Instance;
            return marks?.PlantingOwner(tile) ?? marks?.CuttingOwner(tile);
        }

        /// <summary>
        /// Whether colony <paramref name="slot"/>'s workers may take this tree, bush, crop or ruin: every mark on its
        /// tile must be the colony's own. An unmarked one is anyone's.
        /// </summary>
        public static bool MayTake(int slot, BaseComponent resource)
        {
            BlockObject blockObject = resource ? resource.GetComponent<BlockObject>() : null;
            if (!blockObject) return true;
            Vector3Int tile = blockObject.Coordinates;
            ColonyMarks marks = ColonyMarks.Instance;
            int? planted = marks?.PlantingOwner(tile);
            if (planted != null && planted.Value != slot) return false;
            int? marked = marks?.CuttingOwner(tile);
            return marked == null || marked.Value == slot;
        }

        internal static void FilterYielders(Inventory receivingInventory, ref IEnumerable<Yielder> yielders)
        {
            if (!Active || yielders == null) return;
            int? slot = SimOwnerOf(receivingInventory);
            if (slot == null) return;
            int worker = slot.Value;
            yielders = yielders.Where(yielder => !yielder || MayTake(worker, yielder));
        }
    }

    // Lumberjacks, gatherers, scavengers and farmhouse harvesters all look for their work through these two.
    // Timed whole (the game's search and the colony filter), since the filter only runs as the game searches; with
    // separate colonies off there is nothing to filter and nothing is timed (__state stays 0).
    [HarmonyPatch(typeof(YielderFinder), nameof(YielderFinder.FindLivingYielderWithoutAccessible))]
    static class ColonyLivingYielderPatcher
    {
        static void Prefix(Inventory receivingInventory, ref IEnumerable<Yielder> yielders, out long __state)
        {
            __state = ColonySeparation.Active ? ColonyProfiler.Start() : 0;
            if (__state != 0) ColonySeparation.FilterYielders(receivingInventory, ref yielders);
        }

        static void Finalizer(long __state)
        {
            if (__state != 0) ColonyProfiler.Stop(ColonySeparation.ResourceSearches, __state);
        }
    }

    [HarmonyPatch(typeof(YielderFinder), nameof(YielderFinder.FindYielderWithAccessible))]
    static class ColonyAccessibleYielderPatcher
    {
        static void Prefix(Inventory receivingInventory, ref IEnumerable<Yielder> yielders, out long __state)
        {
            __state = ColonySeparation.Active ? ColonyProfiler.Start() : 0;
            if (__state != 0) ColonySeparation.FilterYielders(receivingInventory, ref yielders);
        }

        static void Finalizer(long __state)
        {
            if (__state != 0) ColonyProfiler.Stop(ColonySeparation.ResourceSearches, __state);
        }
    }

    // Foresters and farmhouses plant only on their own colony's planting marks (and on marks of nobody's, from before).
    [HarmonyPatch(typeof(PlantingSpotFinder), "CanPlantAt")]
    static class ColonyPlantingSpotPatcher
    {
        static void Postfix(PlantingSpotFinder __instance, PlantingSpot plantingSpot, ref bool __result)
        {
            if (!__result || !ColonySeparation.Active) return;
            int? slot = ColonySeparation.SimOwnerOf(__instance);
            if (slot == null) return;
            int? owner = ColonyMarks.Instance?.PlantingOwner(plantingSpot.Coordinates);
            __result = owner == null || owner.Value == slot.Value;
        }
    }

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): every builder hub looks through one map-wide list of construction sites and
     * takes any its district can reach, bringing goods from its own district:
        DistrictCenter district = workplaceAccessible.GetComponent<DistrictBuilding>().District;
        if (!_constructionSite.IsOn || !district || !workplaceAccessible.FindRoadToTerrainPath(...)) ...
     */
    // A colony's builders build only their own colony's sites, so nobody spends goods on another colony's buildings.
    // A Trading Post is built by the colony that placed it (both halves are stamped with it).
    [HarmonyPatch(typeof(ConstructionJob), nameof(ConstructionJob.StartConstructionJob))]
    static class ColonyConstructionJobPatcher
    {
        static bool Prefix(ConstructionJob __instance, Accessible workplaceAccessible, ref (Behavior, Decision) __result)
        {
            if (!ColonySeparation.Active) return true;
            long started = ColonyProfiler.Start();
            try
            {
                return Check(__instance, workplaceAccessible, ref __result);
            }
            finally
            {
                ColonyProfiler.Stop(ColonySeparation.BuilderChecks, started);
            }
        }

        static bool Check(ConstructionJob __instance, Accessible workplaceAccessible, ref (Behavior, Decision) __result)
        {
            int? site = ColonySeparation.SimOwnerOf(__instance);
            int? builder = DistrictOwner.OwnerOfDistrict(workplaceAccessible ? workplaceAccessible.GetComponent<DistrictBuilding>()?.District : null);
            if (site == null || builder == null || site.Value == builder.Value) return true;
            __result = (null, Decision.ReleaseNow());
            return false;
        }
    }

    // A colony's builders demolish only their own colony's things (and things nobody owns). Either colony's may take
    // down a trading post.
    [HarmonyPatch(typeof(DemolishJob), nameof(DemolishJob.CanStartJob))]
    static class ColonyDemolishJobPatcher
    {
        static void Postfix(DemolishJob __instance, Demolisher demolisher, ref bool __result)
        {
            if (!__result || !ColonySeparation.Active || TradingPosts.IsTradingPost(__instance.GetComponent<DistrictCrossing>())) return;
            int? builder = ColonySeparation.SimOwnerOf(demolisher);
            if (builder == null) return;
            int? target = ColonySeparation.SimOwnerOf(__instance)
                ?? ColonySeparation.NaturalOwnerOf(__instance.GetComponent<BlockObject>());
            if (target != null && target.Value != builder.Value) __result = false;
        }
    }

    // Log piles and other good stacks (left by lumberjacks and gatherers) are collected only by the colony whose marks
    // they lie on, if any: the behavior walks the map-wide list.
    [HarmonyPatch(typeof(GoodStackRetrieverBehavior), "RetrieveGoodStack")]
    static class ColonyGoodStackPatcher
    {
        static bool Prefix(GoodStackRetrieverBehavior __instance, GoodStack goodStack, ref bool __result)
        {
            if (!ColonySeparation.Active || !goodStack) return true;
            int? slot = ColonySeparation.SimOwnerOf(__instance);
            if (slot == null || ColonySeparation.MayTake(slot.Value, goodStack)) return true;
            __result = false;
            return false;
        }
    }

    // Recovered goods (left where something was demolished) are anyone's: whichever colony's builders get to them.
}
