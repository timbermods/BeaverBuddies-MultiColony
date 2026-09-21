using HarmonyLib;
using System;
using Timberborn.BlockSystem;
using Timberborn.DistributionSystem;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;

namespace BeaverBuddies.Colonies
{
    // These change the simulation, so they run the same way on every computer and read only saved state: the mode
    // and start coordinates (ColonyModeService) and entity positions. With the mode off they do nothing.

    static class ColonyDistricts
    {
        /// <summary>Whether automatic migration may move beavers between these two districts.</summary>
        public static bool SameColony(ColonyTerritory territory, DistrictCenter a, DistrictCenter b)
        {
            BlockObject blockA = a?.GetComponent<BlockObject>();
            BlockObject blockB = b?.GetComponent<BlockObject>();
            // Without positions there is nothing to divide by; the game's behaviour stands.
            if (blockA == null || blockB == null) return true;
            return ColonyModeState.SameColony(territory,
                ColonyGameWorld.TileOf(blockA.Coordinates), ColonyGameWorld.TileOf(blockB.Coordinates));
        }
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
    // Districts joined by a District Crossing are connected, so the game would move beavers between the two
    // colonies on its own. Same loop, same order; a district of another colony is skipped.
    [HarmonyPatch(typeof(MigrationNeighbours), nameof(MigrationNeighbours.GetHighestSpareNeighbour))]
    static class MigrationNeighboursHighestSparePatcher
    {
        static bool Prefix(MigrationNeighbours __instance, PopulationDistributor populationDistributor, ref PopulationDistributor __result)
        {
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null) return true;
            PopulationDistributor best = null;
            foreach (DistrictCenter item in __instance._districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
            {
                if (!ColonyDistricts.SameColony(territory, populationDistributor.DistrictCenter, item)) continue;
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
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null) return true;
            PopulationDistributor best = null;
            foreach (DistrictCenter item in __instance._districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
            {
                if (!ColonyDistricts.SameColony(territory, populationDistributor.DistrictCenter, item)) continue;
                PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
                if (other.CanImmigrate && (best == null || best.Spare > other.Spare))
                    best = other;
            }
            __result = best;
            return false;
        }
    }

    // Records where colony 1's starting building went, while colony 2 is awaited, so founding measures colony 1's land
    // from its start and not from whichever district center it built later. Also sees the game's relocate option,
    // which deletes the starting building and places it again. New game only; every computer loads the result.
    [HarmonyPatch(typeof(Timberborn.GameStartup.StartingBuildingSpawner), "PlaceStartingBuilding")]
    static class StartingBuildingSpawnerPlacePatcher
    {
        static void Postfix(Timberborn.Coordinates.Placement placement)
        {
            SingletonManager.GetSingleton<ColonyModeService>()?.RecordFirstColonyStart(placement.Coordinates);
        }
    }

    static class ColonyTradeDefaults
    {
        /// <summary>The import option a good starts with: the game's, or Disabled in a separate-colonies game.</summary>
        public static ImportOption DefaultImportOption(bool forceImport) =>
            ColonyModeService.ActiveTerritory != null ? ImportOption.Disabled
            : forceImport ? ImportOption.Forced : ImportOption.Auto;
    }

    /*
     * 9/20/2026 (Timberborn 1.1.2.4)
        ExportThreshold = 0f;
        ImportOption = ((!_goodSpec.ForceImport) ? ImportOption.Auto : ImportOption.Forced);
        this.SettingChanged?.Invoke(this, EventArgs.Empty);
     */
    // Trade is closed until a player opens it: in a separate-colonies game every good of a new district starts with
    // import Disabled, so a crossing moves nothing until the receiving colony's player chooses a good. Settings loaded
    // from a save do not pass through here. The Reset button is recorded by GoodDistributionSettingSetDefaultPatcher
    // (with this same default) and replayed as ordinary setting changes; then the original does not run here, and
    // nothing may change locally, which is what __runOriginal guards.
    [HarmonyPatch(typeof(GoodDistributionSetting), nameof(GoodDistributionSetting.SetDefault))]
    static class GoodDistributionSettingColonyDefaultPatcher
    {
        static readonly AccessTools.FieldRef<GoodDistributionSetting, EventHandler> settingChanged =
            AccessTools.FieldRefAccess<GoodDistributionSetting, EventHandler>("SettingChanged");

        static void Postfix(GoodDistributionSetting __instance, bool __runOriginal)
        {
            if (!__runOriginal || ColonyModeService.ActiveTerritory == null) return;
            if (__instance.ImportOption == ImportOption.Disabled) return;
            __instance.ImportOption = ImportOption.Disabled;
            // Listeners already heard about the game's default; tell them about the one that stands.
            settingChanged(__instance)?.Invoke(__instance, EventArgs.Empty);
        }
    }
}
