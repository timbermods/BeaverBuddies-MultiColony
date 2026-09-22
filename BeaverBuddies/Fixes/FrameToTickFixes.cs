using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.DistributionSystem;
using Timberborn.Navigation;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityEngine;

namespace BeaverBuddies.Fixes
{
    // Three places where the game changes or caches simulation state once per frame. A tick is spread over several
    // frames, a different number on each computer, so a change made at a frame boundary lands after a different
    // share of the tick on each computer, and a beaver ticking in between sees it on one and not the other. In a
    // co-op game each of these happens at the tick instead, at the same moment everywhere.

    // ---- gates ----

    /// <summary>
    /// Gates open and close in GateUpdater.LateUpdateSingleton, once per frame, and whether a gate may open (it must
    /// not join two districts) is read from the preview road graph and district map: the real ones plus whatever the
    /// local player is hovering with a tool. In co-op the gates are updated at the start of each tick instead, and
    /// the question is answered from the real road graph and district map (the same walk the game makes, on the
    /// graphs the tick keeps), so no computer's hovering and no frame boundary can make a gate open on one computer
    /// only.
    /// </summary>
    public class GateTickRunner : RegisteredSingleton, ILoadableSingleton, ITickableSingleton
    {
        private readonly GateUpdater _gateUpdater;

        /// <summary>True while the tick runs the game's per-frame gate update on purpose.</summary>
        internal static bool RunNow;

        public GateTickRunner(GateUpdater gateUpdater)
        {
            _gateUpdater = gateUpdater;
        }

        public void Load() { }

        public void Tick()
        {
            if (EventIO.IsNull) return;
            RunNow = true;
            try { _gateUpdater.LateUpdateSingleton(); }
            finally { RunNow = false; }
        }
    }

    [HarmonyPatch(typeof(GateUpdater), nameof(GateUpdater.LateUpdateSingleton))]
    static class GateUpdaterFramePatcher
    {
        static bool Prefix() => EventIO.IsNull || GateTickRunner.RunNow;
    }

    [ManualMethodOverwrite]
    /*
     * 9/21/2026 (Timberborn 1.1.2.4), GateConflictDetector.CanOpenGateWithoutConflict, FindDistrictId and VisitNode:
     * the ends and the centre of the gate are ignorable; each district center's node is numbered; from each end, a
     * breadth-first walk over the preview road graph (plus the open gates' crossings, skipping set obstacles) finds
     * the first district center node; the gate may open if either end reaches none, or both reach the same.
     */
    /// <summary>The game's walk, on the real graph and map instead of the preview ones.</summary>
    public class RealGateConflict : RegisteredSingleton, ILoadableSingleton
    {
        private readonly NodeIdService _nodeIdService;
        private readonly DistrictMap _districtMap;
        private readonly RoadNavMeshGraph _roadNavMeshGraph;
        private readonly DistrictObstacleService _districtObstacleService;

        private readonly Queue<int> toVisit = new Queue<int>();
        private readonly HashSet<int> visited = new HashSet<int>();
        private readonly HashSet<int> ignorable = new HashSet<int>();
        private readonly Dictionary<int, int> nodeToDistrict = new Dictionary<int, int>();
        private readonly List<int> neighbours = new List<int>();

        public static RealGateConflict Instance => SingletonManager.GetSingleton<RealGateConflict>();

        public RealGateConflict(NodeIdService nodeIdService, DistrictMap districtMap, RoadNavMeshGraph roadNavMeshGraph,
            DistrictObstacleService districtObstacleService)
        {
            _nodeIdService = nodeIdService;
            _districtMap = districtMap;
            _roadNavMeshGraph = roadNavMeshGraph;
            _districtObstacleService = districtObstacleService;
        }

        public void Load() { }

        public bool CanOpenGateWithoutConflict(Vector3Int from, Vector3Int to, Vector3Int center, Dictionary<Vector3Int, Vector3Int> openGateCrossings)
        {
            int a = _nodeIdService.GridToId(from), b = _nodeIdService.GridToId(to);
            ignorable.Add(a);
            ignorable.Add(b);
            ignorable.Add(_nodeIdService.GridToId(center));
            int number = 0;
            foreach (int node in _districtMap.DistrictCenterNodeIds()) nodeToDistrict.Add(node, number++);
            int? districtA = FindDistrictId(a, openGateCrossings);
            int? districtB = FindDistrictId(b, openGateCrossings);
            ignorable.Clear();
            nodeToDistrict.Clear();
            if (!districtA.HasValue || !districtB.HasValue) return true;
            return districtA.Value == districtB.Value;
        }

        private int? FindDistrictId(int start, Dictionary<Vector3Int, Vector3Int> openGateCrossings)
        {
            toVisit.Clear();
            visited.Clear();
            if (_districtObstacleService.IsSetObstacle(start)) return null;
            visited.Add(start);
            toVisit.Enqueue(start);
            while (toVisit.Count > 0)
            {
                int node = toVisit.Dequeue();
                if (nodeToDistrict.TryGetValue(node, out int district)) return district;
                foreach (NavMeshNode neighbour in _roadNavMeshGraph.GetNeighbors(node)) neighbours.Add(neighbour.Id);
                if (openGateCrossings.TryGetValue(_nodeIdService.IdToGrid(node), out Vector3Int across))
                    neighbours.Add(_nodeIdService.GridToId(across));
                foreach (int next in neighbours)
                {
                    if (visited.Contains(next) || ignorable.Contains(next) || _districtObstacleService.IsSetObstacle(next)) continue;
                    visited.Add(next);
                    toVisit.Enqueue(next);
                }
                neighbours.Clear();
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(GateConflictDetector), nameof(GateConflictDetector.CanOpenGateWithoutConflict))]
    static class GateConflictRealGraphPatcher
    {
        static bool Prefix(Vector3Int from, Vector3Int to, Vector3Int center, Dictionary<Vector3Int, Vector3Int> openGateCrossings, ref bool __result)
        {
            if (EventIO.IsNull) return true;
            RealGateConflict real = RealGateConflict.Instance;
            if (real == null) return true;
            __result = real.CanOpenGateWithoutConflict(from, to, center, openGateCrossings);
            return false;
        }
    }

    // ---- automation ----

    /// <summary>
    /// The game evaluates automation whose inputs changed once per frame (AutomationRunner.UpdateSingleton), as well
    /// as at the start and the end of each tick. Evaluated between two buckets of a tick, a switch's effect (a paused
    /// building, a changed output) lands mid-tick at a point that differs per computer. In co-op only the tick's own
    /// evaluations run: a change shows at the end of the tick it was made in, the same everywhere.
    /// </summary>
    [HarmonyPatch(typeof(AutomationRunner), nameof(AutomationRunner.UpdateSingleton))]
    static class AutomationFramePatcher
    {
        static bool Prefix() => EventIO.IsNull;
    }

    // ---- the district crossing's snapshot ----

    /*
     * 9/21/2026 (Timberborn 1.1.2.4), DistrictDistributableGoodProvider: GetImportableGood and
     * GetDistributableGoodForExport compute a snapshot the first time a good is asked about and keep it in
     * _importCache / _exportCache until a setting or the district's storage changes.
     */
    /// <summary>
    /// The crossing panel's import icons ask the same provider the workers' export decision reads, and the first
    /// asker fills the cache: a panel open on one computer froze a snapshot taken at a frame boundary that the
    /// other computer's simulation took at its tick. In co-op an ask from outside the simulation gets its answer but
    /// leaves the cache as the simulation left it.
    /// </summary>
    [HarmonyPatch(typeof(DistrictDistributableGoodProvider), "GetImportableGood")]
    static class ImportSnapshotPatcher
    {
        static void Prefix(DistrictDistributableGoodProvider __instance, string goodId, out bool __state) =>
            __state = FrameReads.Outside() && !__instance._importCache.ContainsKey(goodId);

        static void Postfix(DistrictDistributableGoodProvider __instance, string goodId, bool __state)
        {
            if (__state) __instance._importCache.Remove(goodId);
        }
    }

    [HarmonyPatch(typeof(DistrictDistributableGoodProvider), nameof(DistrictDistributableGoodProvider.GetDistributableGoodForExport))]
    static class ExportSnapshotPatcher
    {
        static void Prefix(DistrictDistributableGoodProvider __instance, string goodId, out bool __state) =>
            __state = FrameReads.Outside() && !__instance._exportCache.ContainsKey(goodId);

        static void Postfix(DistrictDistributableGoodProvider __instance, string goodId, bool __state)
        {
            if (__state) __instance._exportCache.Remove(goodId);
        }
    }

    static class FrameReads
    {
        /// <summary>A co-op game, and this read is not the simulation's (not in a tick, not in a replayed action).</summary>
        internal static bool Outside() => !EventIO.IsNull && !DeterminismService.IsTicking && !ReplayService.IsReplayingEvents;
    }
}
