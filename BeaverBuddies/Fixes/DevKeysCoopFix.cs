using BeaverBuddies.IO;
using HarmonyLib;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.DeconstructionSystem;
using Timberborn.PlantingUI;
using Timberborn.RecoveredGoodSystem;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Two dev mode keys, both on Ctrl, are read by the game where it changes the world, not where the player clicks:
    /// "place finished" inside BuildingPlacer.Place, and "don't recover goods" whenever a building is deconstructed.
    /// In co-op both run on every computer (a replayed placement or deletion, or beavers finishing a demolition in
    /// the tick), so they read the keyboard of whoever's computer it is: with dev mode on, a player only holding Ctrl
    /// (for the mod's own Ctrl shortcuts, or the shared free unlock) got a finished building, or no recovered goods,
    /// on their computer alone. In a co-op game neither key is read: the building's own spec decides, and goods are
    /// always recovered. Single player is unchanged.
    /// </summary>
    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.ShouldBePlacedFinished))]
    static class PlaceFinishedKeyCoopPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(BuildingSpec buildingSpec, ref bool __result)
        {
            if (EventIO.IsNull) return true;
            __result = buildingSpec.PlaceFinished;
            return false;
        }
    }

    [ManualMethodOverwrite]
    /*
     * 9/21/2026 (Timberborn 1.1.2.4)
        if (!_inputService.IsKeyHeld(DontRecoverGoodsKey))
        {
            PrepareToSpawning(buildingDeconstructedEvent.Deconstructible, buildingDeconstructedEvent.Coordinates);
        }
     */
    [HarmonyPatch(typeof(BuildingGoodsRecoveryService), nameof(BuildingGoodsRecoveryService.OnBuildingDeconstructed))]
    static class DontRecoverGoodsKeyCoopPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(BuildingGoodsRecoveryService __instance, BuildingDeconstructedEvent buildingDeconstructedEvent)
        {
            if (EventIO.IsNull) return true;
            __instance.PrepareToSpawning(buildingDeconstructedEvent.Deconstructible, buildingDeconstructedEvent.Coordinates);
            return false;
        }
    }

    /// <summary>
    /// A third dev key on Ctrl: planting with it held (dev mode on) also spawns the plants at once, grown with Shift, with
    /// their yield with Alt (DevModePlantableSpawner, called by the planting tool beside the marking it records). The
    /// marking is shared, the spawning ran on the planting player's computer alone: plants that exist on one computer
    /// only, from a player holding Ctrl for the shared free unlock (1.4.0-rc1, A2). In a co-op game the planting tool
    /// only marks. Single player is unchanged.
    /// </summary>
    [ManualMethodOverwrite]
    /*
     * 2026-09-23 (Timberborn 1.1.2.4, DevModePlantableSpawner.SpawnPlantables)
        if (!_inputService.IsKeyHeld(PlantSpawnedKey))
        {
            return;
        }
        foreach (Vector3Int block in blocks) { ...SpawnIgnoringConstraints, IncreaseGrowthProgress, FastForwardGrowth... }
     */
    [HarmonyPatch(typeof(DevModePlantableSpawner), nameof(DevModePlantableSpawner.SpawnPlantables))]
    static class DevPlantSpawnKeyCoopPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix() => EventIO.IsNull;
    }
}
