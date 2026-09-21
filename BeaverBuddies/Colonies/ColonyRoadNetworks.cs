using BeaverBuddies.Events;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using Timberborn.SingletonSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Keeps road networks apart: two district centers may never share one, so two players' colonies only meet through
    /// a District Crossing. The game's placement tool already refuses a building whose roads would join two districts
    /// (checked in the placing player's own interface); this closes the gaps a two-player game opens:
    ///  - a zipline link is judged by the host alone, before anyone plays it (the game's check reads state that
    ///    differs between computers, so judging it in the replay could connect on one computer and not the other);
    ///  - founding a colony must not put its district center on another district's roads (checked from the
    ///    tick-updated district map, the same on every computer);
    ///  - if two districts' roads end up joined anyway (two placements each fine alone, finished together), every
    ///    computer notices at the same tick and says so, so the players can remove the link.
    /// </summary>
    /*
     * 9/21/2026 (Timberborn 1.1.2.4), DistrictPreviewsValidator.IsValid:
        if (blockObject.IsPreview && _districtService.IsPreviewDistrictInConflict(blockObject.GetComponent<DistrictCenter>()?.CenterCoordinates))
            { errorMessage = _loc.T(DistrictsInConflictLocKey); return false; }
     */
    // A replayed placement is checked on every computer (BuildingPlacedEvent.IsPlacementValid). This validator reads
    // the preview road graph, which holds whatever the local player is hovering and is updated once per frame, so it
    // could refuse the building on one computer and not another. While events replay it passes; the placing player's
    // own tool already refused a joining road before the click, and ColonyRoadNetworks warns if two joined anyway.
    [HarmonyLib.HarmonyPatch(typeof(Timberborn.GameDistrictsUI.DistrictPreviewsValidator), nameof(Timberborn.GameDistrictsUI.DistrictPreviewsValidator.IsValid))]
    static class DistrictPreviewsValidatorReplayPatcher
    {
        static bool Prefix(ref bool __result, ref string errorMessage)
        {
            if (!ReplayService.IsReplayingEvents) return true;
            errorMessage = null;
            __result = true;
            return false;
        }
    }

    public class ColonyRoadNetworks : RegisteredSingleton, ILoadableSingleton, ISingletonNavMeshListener
    {
        private readonly IDistrictService _districtService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly EntityRegistry _entityRegistry;
        private readonly ZiplineConnectionService _ziplineConnectionService;
        private readonly RoadNavMeshGraph _roadNavMeshGraph;
        private readonly DistrictObstacleService _districtObstacleService;

        private bool conflictReported;

        public static ColonyRoadNetworks Instance => SingletonManager.GetSingleton<ColonyRoadNetworks>();

        public ColonyRoadNetworks(IDistrictService districtService, DistrictCenterRegistry districtCenterRegistry,
            EntityRegistry entityRegistry, ZiplineConnectionService ziplineConnectionService,
            RoadNavMeshGraph roadNavMeshGraph, DistrictObstacleService districtObstacleService)
        {
            _districtService = districtService;
            _districtCenterRegistry = districtCenterRegistry;
            _entityRegistry = entityRegistry;
            _ziplineConnectionService = ziplineConnectionService;
            _roadNavMeshGraph = roadNavMeshGraph;
            _districtObstacleService = districtObstacleService;
        }

        public void Load() { }

        /// <summary>
        /// Whether a building placed here would join the roads of an existing district: its entrance (where its road
        /// begins) or a tile beside it is already a district's road. Reads the tick-updated district map only.
        /// </summary>
        public bool WouldJoinAnyDistrict(BlockObjectSpec spec, Placement placement)
        {
            if (spec?.Entrance == null || !spec.Entrance.HasEntrance) return false;
            Vector3Int entrance = placement.Orientation.Transform(placement.FlipMode.Transform(spec.Entrance.Coordinates, spec.Size.x))
                + placement.Coordinates;
            var tiles = new[]
            {
                entrance,
                entrance + new Vector3Int(1, 0, 0), entrance + new Vector3Int(-1, 0, 0),
                entrance + new Vector3Int(0, 1, 0), entrance + new Vector3Int(0, -1, 0),
            };
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                if (districtCenter.District == null) continue;
                foreach (Vector3Int tile in tiles)
                {
                    try
                    {
                        if (_districtService.IsOnDistrictRoad(districtCenter.District, CoordinateSystem.GridToWorldCentered(tile)))
                            return true;
                    }
                    catch (Exception)
                    {
                        // The district map is already in conflict somewhere: do not add to it.
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Host only, before a zipline link is played: the game's own check. The replay then only guards against a
        /// link that already exists, which depends on saved state alone.
        /// </summary>
        internal bool HostAllowsZipline(ZiplineConnectionChangedEvent zipline, out string why)
        {
            why = null;
            if (!zipline.add) return true;
            ZiplineTower a = Tower(zipline.currentTowerEntityID);
            ZiplineTower b = Tower(zipline.otherTowerEntityID);
            if (!a || !b) return true;
            if (_ziplineConnectionService.CanBeConnected(a, b)) return true;
            why = "the game refuses this link (it would join two districts' roads, or the towers cannot reach)";
            return false;
        }

        private ZiplineTower Tower(string entityId) =>
            Guid.TryParse(entityId, out Guid guid) ? _entityRegistry.GetEntity(guid)?.GetComponent<ZiplineTower>() : null;

        /// <summary>
        /// After the roads change (in the tick, on every computer): are any two district centers now on one road
        /// network? That breaks the game's district map; say so once, until it is resolved.
        /// </summary>
        public void OnNavMeshUpdated(NavMeshUpdate navMeshUpdate)
        {
            if (!ColonyModeService.IsSeparateColonies) return;
            bool conflict;
            try
            {
                var districtService = _districtService as DistrictService;
                if (districtService == null) return;
                IReadOnlyCollection<int> centers = districtService._districtMap.DistrictCenterNodeIds();
                conflict = centers.Count > 1 && districtService._districtConflictDetector
                    .AreDistrictsInConflict(_roadNavMeshGraph, _districtObstacleService, centers);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not check the road networks: " + error.Message);
                return;
            }
            if (conflict && !conflictReported)
            {
                Plugin.LogWarning("[Colony] Two districts' roads are joined without a District Crossing");
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(
                    RegisteredLocalizationService.T("BeaverBuddies.Colony.RoadsJoined"));
            }
            conflictReported = conflict;
        }
    }
}
