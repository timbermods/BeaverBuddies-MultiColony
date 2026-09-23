using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Synchronised fill valves, throttling valves and floodgates keep in step only with their own colony's (1.4.0-rc1,
    /// W1). The game copies a synchronised building's settings, and the lever or sensor it listens to, to every
    /// synchronised building of its kind it touches, and on through them (FillValveSynchronizer,
    /// ThrottlingValveSynchronizer, FloodgateSynchronizer; synchronising is on by default). A colony changing its own
    /// floodgate's height, wiring it to its own lever, placing one beside the border or copying settings onto one, so
    /// also changed, and rewired, the other colony's floodgates that touched it. In a separate-colonies game each
    /// synchroniser now works through its own colony's buildings only: the colony of the building it starts from, found
    /// the same way on every computer (ColonySeparation.SimOwnerOf). Another colony's building is neither changed nor
    /// copied from, and a run of them is not passed through. A shared-colony game is unchanged, and so is a building
    /// no colony owns (a save from before the colonies were split).
    /// </summary>
    static class ColonyWaterSync
    {
        /// <summary>The colony whose building a synchroniser is working from, or null when nothing is filtered.</summary>
        internal static int? Owner;

        internal static int? Enter(BaseComponent building)
        {
            int? previous = Owner;
            Owner = ColonyModeService.IsSeparateColonies ? ColonySeparation.SimOwnerOf(building) : null;
            return previous;
        }

        internal static void Exit(int? previous) => Owner = previous;

        /// <summary>Whether the synchroniser that is running may change or copy from <paramref name="neighbour"/>.</summary>
        internal static bool Allows(BaseComponent neighbour) => Owner == null || ColonySeparation.SimOwnerOf(neighbour) == Owner;
    }

    // Every way into the three synchronisers: a setter "...AndSynchronize", switching synchronising on, placing one
    // (pulling from its neighbours) or copying settings onto one (pushing to them), a new input wire.
    [HarmonyPatch]
    static class ColonyWaterSyncEntryPatcher
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (System.Type synchronizer in new[] { typeof(FillValveSynchronizer), typeof(ThrottlingValveSynchronizer), typeof(FloodgateSynchronizer) })
            {
                yield return AccessTools.Method(synchronizer, "SynchronizeAllNeighbors");
                yield return AccessTools.Method(synchronizer, "SynchronizeWithAllNeighbors");
                yield return AccessTools.Method(synchronizer, "SynchronizeWithUnfinishedNeighbors");
            }
        }

        static void Prefix(BaseComponent __0, out int? __state) => __state = ColonyWaterSync.Enter(__0);

        static void Finalizer(int? __state) => ColonyWaterSync.Exit(__state);
    }

    // The valves look every neighbour up through one method, both to copy from and to copy to.
    [HarmonyPatch(typeof(FillValveSynchronizer), nameof(FillValveSynchronizer.GetValve))]
    static class ColonyFillValveSyncPatcher
    {
        static void Postfix(ref FillValve __result)
        {
            if (__result != null && !ColonyWaterSync.Allows(__result)) __result = null;
        }
    }

    [HarmonyPatch(typeof(ThrottlingValveSynchronizer), nameof(ThrottlingValveSynchronizer.GetThrottlingValve))]
    static class ColonyThrottlingValveSyncPatcher
    {
        static void Postfix(ref ThrottlingValve __result)
        {
            if (__result != null && !ColonyWaterSync.Allows(__result)) __result = null;
        }
    }

    // A floodgate's neighbours are looked up in two places: the one copied to...
    [ManualMethodOverwrite]
    /*
     * 2026-09-23 (Timberborn 1.1.2.4, FloodgateSynchronizer.SynchronizeNeighbor): starts with
        Floodgate bottomObjectComponentAt = _blockService.GetBottomObjectComponentAt<Floodgate>(neighborCoords);
        if ((bool)bottomObjectComponentAt && bottomObjectComponentAt.IsSynchronized && !_visitedNeighbors.Contains(bottomObjectComponentAt))
        { ...copy the heights and the input, enqueue it... }
     */
    [HarmonyPatch(typeof(FloodgateSynchronizer), nameof(FloodgateSynchronizer.SynchronizeNeighbor))]
    static class ColonyFloodgateSyncNeighborPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(FloodgateSynchronizer __instance, Vector3Int neighborCoords)
        {
            if (ColonyWaterSync.Owner == null) return true;
            Floodgate neighbour = __instance._blockService.GetBottomObjectComponentAt<Floodgate>(neighborCoords);
            return neighbour == null || ColonyWaterSync.Allows(neighbour);
        }
    }

    // ...and the one copied from, when a floodgate joins its neighbours (placed, or synchronising switched on).
    [ManualMethodOverwrite]
    /*
     * 2026-09-23 (Timberborn 1.1.2.4, FloodgateSynchronizer.SynchronizeWithNeighbors)
        BlockObject component = floodgate.GetComponent<BlockObject>();
        Vector3Int[] neighbors4Vector3Int = Deltas.Neighbors4Vector3Int;
        foreach (Vector3Int vector3Int in neighbors4Vector3Int)
        {
            Vector3Int vector3Int2 = component.Coordinates + vector3Int;
            for (int j = 0; j < floodgate.MaxHeight; j++)
            {
                Vector3Int coordinates = vector3Int2 + new Vector3Int(0, 0, j);
                Floodgate bottomObjectComponentAt = _blockService.GetBottomObjectComponentAt<Floodgate>(coordinates);
                if (bottomObjectComponentAt != null && bottomObjectComponentAt.IsSynchronized)
                {
                    SynchronizeNeighbors(bottomObjectComponentAt, unfinishedOnly);
                    break;
                }
            }
        }
     */
    [HarmonyPatch(typeof(FloodgateSynchronizer), nameof(FloodgateSynchronizer.SynchronizeWithNeighbors))]
    static class ColonyFloodgateSyncWithNeighborsPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(FloodgateSynchronizer __instance, Floodgate floodgate, bool unfinishedOnly)
        {
            if (ColonyWaterSync.Owner == null) return true;
            BlockObject component = floodgate.GetComponent<BlockObject>();
            Vector3Int[] neighbors4Vector3Int = Deltas.Neighbors4Vector3Int;
            foreach (Vector3Int vector3Int in neighbors4Vector3Int)
            {
                Vector3Int vector3Int2 = component.Coordinates + vector3Int;
                for (int j = 0; j < floodgate.MaxHeight; j++)
                {
                    Vector3Int coordinates = vector3Int2 + new Vector3Int(0, 0, j);
                    Floodgate neighbour = __instance._blockService.GetBottomObjectComponentAt<Floodgate>(coordinates);
                    if (neighbour != null && neighbour.IsSynchronized && ColonyWaterSync.Allows(neighbour))
                    {
                        __instance.SynchronizeNeighbors(neighbour, unfinishedOnly);
                        break;
                    }
                }
            }
            return false;
        }
    }
}
