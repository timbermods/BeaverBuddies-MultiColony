using HarmonyLib;
using System;
using Timberborn.DistributionSystem;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;

namespace BeaverBuddies.Colonies
{
    // These change the simulation, so they run the same way on every computer and read only saved state: the mode
    // (ColonyModeService) and each district center's owner (DistrictOwner). With the mode off they do nothing.

    static class ColonyDistricts
    {
        /// <summary>Whether automatic migration may move beavers between these two districts: only within a colony.</summary>
        public static bool SameColony(DistrictCenter a, DistrictCenter b) =>
            ColonyModeState.SameOwner(DistrictOwner.OwnerOfDistrict(a), DistrictOwner.OwnerOfDistrict(b));
    }

    [ManualMethodOverwrite]
    /*
     * 9/20/2026 (Timberborn 1.1.2.4)
        PopulationDistributor populationDistributor2 = null;
        foreach (DistrictCenter item in _districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
        {
            PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
            if (other.CanEmigrate && (populationDistributor2 == null || populationDistributor2.Spare < other.Spare))
                populationDistributor2 = other;
        }
        return populationDistributor2;
     */
    // Districts joined by a District Crossing are connected, so the game would move beavers between two players'
    // colonies on its own. Same loop, same order; a district of another colony is skipped. (Moving beavers by hand
    // to another colony is refused too, by the migration rule: beavers change colony only through a Trading Post.)
    [HarmonyPatch(typeof(MigrationNeighbours), nameof(MigrationNeighbours.GetHighestSpareNeighbour))]
    static class MigrationNeighboursHighestSparePatcher
    {
        static bool Prefix(MigrationNeighbours __instance, PopulationDistributor populationDistributor, ref PopulationDistributor __result)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            PopulationDistributor best = null;
            foreach (DistrictCenter item in __instance._districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
            {
                if (!ColonyDistricts.SameColony(populationDistributor.DistrictCenter, item)) continue;
                PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
                if (other.CanEmigrate && (best == null || best.Spare < other.Spare))
                    best = other;
            }
            __result = best;
            return false;
        }
    }

    [ManualMethodOverwrite]
    /*
     * 9/20/2026 (Timberborn 1.1.2.4)
        PopulationDistributor populationDistributor2 = null;
        foreach (DistrictCenter item in _districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
        {
            PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
            if (other.CanImmigrate && (populationDistributor2 == null || populationDistributor2.Spare > other.Spare))
                populationDistributor2 = other;
        }
        return populationDistributor2;
     */
    [HarmonyPatch(typeof(MigrationNeighbours), nameof(MigrationNeighbours.GetLowestSpareNeighbour))]
    static class MigrationNeighboursLowestSparePatcher
    {
        static bool Prefix(MigrationNeighbours __instance, PopulationDistributor populationDistributor, ref PopulationDistributor __result)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            PopulationDistributor best = null;
            foreach (DistrictCenter item in __instance._districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
            {
                if (!ColonyDistricts.SameColony(populationDistributor.DistrictCenter, item)) continue;
                PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
                if (other.CanImmigrate && (best == null || best.Spare > other.Spare))
                    best = other;
            }
            __result = best;
            return false;
        }
    }
}
