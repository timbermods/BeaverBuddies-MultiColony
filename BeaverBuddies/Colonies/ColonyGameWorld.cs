using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The rules' view of the running game. Reads only: entities are looked up in the registry and owners found
    /// through their districts, the colony that placed them, or the land they stand on. Nothing here touches the random
    /// state, posts an event or fills a cache the simulation reads (district roads are read from the game's instant map,
    /// the one its placement tools use), so the host can judge an action without changing anything.
    /// </summary>
    public class ColonyGameWorld : IColonyWorld
    {
        private readonly EntityRegistry _entityRegistry;
        private readonly BuildingService _buildingService;
        private readonly IDistrictService _districtService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;

        public ColonyGameWorld(EntityRegistry entityRegistry, BuildingService buildingService,
            IDistrictService districtService, DistrictCenterRegistry districtCenterRegistry)
        {
            _entityRegistry = entityRegistry;
            _buildingService = buildingService;
            _districtService = districtService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        private EntityComponent Entity(string entityId) =>
            Guid.TryParse(entityId, out Guid guid) ? _entityRegistry.GetEntity(guid) : null;

        /// <summary>
        /// A building, district or beaver by its colony; a tree, crop, bush, ruin or pile by the mark it stands on, or
        /// else by the only colony whose land it is on.
        /// </summary>
        public int? OwnerOf(string entityId)
        {
            EntityComponent entity = Entity(entityId);
            if (entity == null) return null;
            return DistrictOwner.OwnerOf(entity) ?? ColonySeparation.NaturalOwnerOf(entity.GetComponent<BlockObject>());
        }

        /// <summary>A half of a trading post (a District Crossing between two colonies): either colony may remove it.</summary>
        public bool IsCrossing(string entityId) => TradingPosts.IsTradingPost(Entity(entityId)?.GetComponent<DistrictCrossing>());

        public bool IsUnlockedFor(int slot, string templateName) =>
            ColonyScienceService.Instance?.IsUnlockedFor(slot, templateName) ?? true;

        public bool MayUseTile(int slot, string tileId) =>
            !TryParseTile(tileId, out Vector3Int tile) || (ColonyReach.Instance?.MayUse(slot, tile) ?? true);

        /// <summary>
        /// A building may not stand on another colony's land, and neither it nor its doorstep may touch another
        /// colony's roads, which would join it to that colony or block it. A District Crossing links two colonies at the
        /// edge of their lands: it needs only to touch the placer's own land (or free land).
        /// </summary>
        public ColonyRefusal PlacementConflict(int slot, ColonyPlacement colonyPlacement, out string detail)
        {
            detail = null;
            BuildingSpec building = _buildingService.GetBuildingTemplate(colonyPlacement.TemplateName);
            BlockObjectSpec spec = building?.GetSpec<BlockObjectSpec>();
            if (spec == null) return ColonyRefusal.None;
            Placement placement = ToPlacement(colonyPlacement);
            // The game's own positioning, as for its preview.
            var tiles = spec.GetBlocks(placement).Select(block => block.Coordinates).ToList();
            Vector3Int? doorstep = spec.Entrance != null && spec.Entrance.HasEntrance
                ? PositionedEntrance.From(spec.GetBlocks(), spec.Entrance, placement)?.DoorstepCoordinates
                : null;
            ColonyRefusal refusal = TilesConflict(slot, tiles, doorstep, TradingPostCost.IsCrossing(building), out detail);
            if (detail != null) detail = colonyPlacement.TemplateName + " " + detail;
            return refusal;
        }

        /// <summary>
        /// The check itself, for a building's blocks and doorstep: none on another colony's land, and none on or beside
        /// another colony's roads. A crossing needs only one block on the placer's own or free land.
        /// </summary>
        public ColonyRefusal TilesConflict(int slot, IEnumerable<Vector3Int> footprint, Vector3Int? doorstep, bool crossing,
            out string detail)
        {
            detail = null;
            var tiles = new List<Vector3Int>(footprint);
            ColonyReach reach = ColonyReach.Instance;
            if (reach == null) return ColonyRefusal.None;
            if (crossing)
            {
                if (tiles.Count == 0 || tiles.Any(tile => reach.MayUse(slot, tile))) return ColonyRefusal.None;
                detail = $"at {tiles[0]} is on slot {reach.Owner(tiles[0])}'s land";
                return ColonyRefusal.OtherColonyArea;
            }
            foreach (Vector3Int tile in tiles)
            {
                if (!reach.MayUse(slot, tile))
                {
                    detail = $"at {tile} is on slot {reach.Owner(tile)}'s land";
                    return ColonyRefusal.OtherColonyArea;
                }
            }
            if (doorstep != null) tiles.Add(doorstep.Value);
            foreach (Vector3Int tile in tiles)
            {
                foreach (Vector3Int near in new[] { tile, tile + Vector3Int.right, tile + Vector3Int.left, tile + Vector3Int.up, tile + Vector3Int.down })
                {
                    int? owner = OtherColonyRoadAt(slot, near);
                    if (owner != null)
                    {
                        detail = $"would touch slot {owner}'s road at {near}";
                        return ColonyRefusal.TouchesOtherColony;
                    }
                }
            }
            return ColonyRefusal.None;
        }

        /// <summary>The colony, other than <paramref name="slot"/>, whose district has a road on this tile, or null.</summary>
        private int? OtherColonyRoadAt(int slot, Vector3Int tile)
        {
            Vector3 position = CoordinateSystem.GridToWorldCentered(tile);
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                int? owner = DistrictOwner.OwnerOfDistrict(districtCenter);
                if (owner == null || owner.Value == slot || districtCenter.District == null) continue;
                if (_districtService.IsOnInstantDistrictRoad(districtCenter.District, position)) return owner;
            }
            return null;
        }

        // ---- tiles in events ----

        /// <summary>How an event's tile is named for the rules.</summary>
        public static string TileKey(Vector3Int tile) => $"{tile.x}|{tile.y}|{tile.z}";

        private static bool TryParseTile(string tileId, out Vector3Int tile)
        {
            tile = default;
            string[] parts = tileId?.Split('|');
            if (parts == null || parts.Length != 3 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)
                || !int.TryParse(parts[2], out int z))
                return false;
            tile = new Vector3Int(x, y, z);
            return true;
        }

        public static ColonyTile TileOf(Vector3Int coordinates) => new ColonyTile(coordinates.x, coordinates.y);

        public static Placement ToPlacement(ColonyPlacement placement) =>
            new Placement(new Vector3Int(placement.X, placement.Y, placement.Z), (Orientation)placement.Orientation,
                placement.IsFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
    }
}
