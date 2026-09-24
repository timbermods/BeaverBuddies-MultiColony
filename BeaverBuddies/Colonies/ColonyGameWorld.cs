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
using Timberborn.PathSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The rules' view of the running game. Reads only: entities are looked up in the registry and owners found
    /// through their districts or the colony that placed them. Nothing here touches the random state, posts an event
    /// or fills a cache the simulation reads (district roads are read from the game's instant map, the one its
    /// placement tools use), so the host can judge an action without changing anything.
    /// </summary>
    public class ColonyGameWorld : IColonyWorld, IColonyRoadMap
    {
        private readonly EntityRegistry _entityRegistry;
        private readonly BuildingService _buildingService;
        private readonly IDistrictService _districtService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly IBlockService _blockService;

        public ColonyGameWorld(EntityRegistry entityRegistry, BuildingService buildingService,
            IDistrictService districtService, DistrictCenterRegistry districtCenterRegistry, IBlockService blockService)
        {
            _entityRegistry = entityRegistry;
            _buildingService = buildingService;
            _districtService = districtService;
            _districtCenterRegistry = districtCenterRegistry;
            _blockService = blockService;
        }

        private EntityComponent Entity(string entityId) =>
            Guid.TryParse(entityId, out Guid guid) ? _entityRegistry.GetEntity(guid) : null;

        /// <summary>A building, district or beaver by its colony; a tree, crop or bush by the mark it stands on.</summary>
        public int? OwnerOf(string entityId)
        {
            EntityComponent entity = Entity(entityId);
            if (entity == null) return null;
            return DistrictOwner.OwnerOf(entity) ?? ColonySeparation.NaturalOwnerOf(entity.GetComponent<BlockObject>());
        }

        /// <summary>
        /// A half of a Trading Post between two colonies, one of which is <paramref name="slot"/>'s: either partner may
        /// remove it. A third colony may not, and a half not (yet) trading is an ordinary building of its owner.
        /// </summary>
        public bool IsCrossingOf(int slot, string entityId)
        {
            DistrictCrossing half = Entity(entityId)?.GetComponent<DistrictCrossing>();
            if (!TradingPosts.IsTradingPost(half)) return false;
            return DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(half)) == slot
                || DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(TradingPosts.Partner(half))) == slot;
        }

        /// <summary>Whether a template is the Trading Post (whose two halves are judged together, see ColonyRulesService).</summary>
        public bool IsTradingPostTemplate(string templateName)
        {
            try { return TradingPosts.IsTradingPostTemplate(_buildingService.GetBuildingTemplate(templateName)); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Whether two placements of a Trading Post are the two halves its placement tool puts down together, as the
        /// game lays them out for a building of that template's size (see <see cref="SecondHalf"/>).
        /// </summary>
        public bool AreHalvesOfOnePost(string templateName, Placement a, Placement b)
        {
            BlockObjectSpec spec;
            try { spec = _buildingService.GetBuildingTemplate(templateName)?.GetSpec<BlockObjectSpec>(); }
            catch (Exception) { return false; }
            return spec != null && AreHalvesOfOnePost(a, b, spec.Size);
        }

        /// <summary>
        /// Where a Trading Post's second half goes when its first goes at <paramref name="first"/>: the tool lays the
        /// two down together, the second at the pair's far corner and turned round (the game's
        /// AreaPicker.HalvesCoordinates, compared by RuntimeChecks). <paramref name="size"/> is one half's size.
        /// </summary>
        public static Placement SecondHalf(Placement first, Vector3Int size) =>
            new Placement(first.Coordinates + first.Orientation.Transform(new Vector3Int(size.x - 1, size.y * 2 - 1, 0)),
                first.Orientation.Flip(), first.FlipMode);

        /// <summary>Whether <paramref name="a"/> and <paramref name="b"/>, in either order, are one post's two halves.</summary>
        public static bool AreHalvesOfOnePost(Placement a, Placement b, Vector3Int size) =>
            IsSecondHalf(a, b, size) || IsSecondHalf(b, a, size);

        private static bool IsSecondHalf(Placement first, Placement other, Vector3Int size)
        {
            Placement second = SecondHalf(first, size);
            return second.Coordinates == other.Coordinates && second.Orientation == other.Orientation;
        }

        /// <summary>Whether this game has a building of that name (another player's mod may add some it does not).</summary>
        public bool HasBuilding(string templateName)
        {
            try { return _buildingService.GetBuildingTemplate(templateName) != null; }
            catch (Exception) { return false; }
        }

        public bool IsUnlockedFor(int slot, string templateName) =>
            ColonyScienceService.Instance?.IsUnlockedFor(slot, templateName) ?? true;

        /// <summary>
        /// Whether the building would join another colony's roads (see <see cref="ColonyRoadRule"/>). A Trading Post
        /// never does: it may go anywhere, and trades once two colonies' roads reach it. Worked out from the game's own
        /// positioning of the building, as for its preview.
        /// </summary>
        public ColonyRefusal PlacementConflict(int slot, ColonyPlacement colonyPlacement, out string detail)
        {
            detail = null;
            BuildingSpec building = _buildingService.GetBuildingTemplate(colonyPlacement.TemplateName);
            BlockObjectSpec spec = building?.GetSpec<BlockObjectSpec>();
            if (spec == null) return ColonyRefusal.None;
            Placement placement = ToPlacement(colonyPlacement);
            List<ColonyCell> cells = spec.GetBlocks(placement).Select(block => Cell(block.Coordinates)).ToList();
            ColonyRefusal refusal = RoadConflict(slot, cells, EntranceOf(spec, placement), TradingPosts.IsTradingPostTemplate(building),
                building.HasSpec<PathSpec>(), out detail);
            if (detail != null) detail = colonyPlacement.TemplateName + " " + detail;
            return refusal;
        }

        /// <summary>
        /// Where a building's road must be: the cell outside its door (the game's PositionedEntrance.Coordinates, the one
        /// its path connection checks; the game's "doorstep" is the building's own cell inside the door). Null for a
        /// building without an entrance.
        /// </summary>
        public static Vector3Int? EntranceOf(BlockObjectSpec spec, Placement placement) =>
            spec.Entrance != null && spec.Entrance.HasEntrance
                ? PositionedEntrance.From(spec.GetBlocks(), spec.Entrance, placement)?.Coordinates
                : null;

        /// <summary>
        /// The road rule for a building's blocks and entrance, as the host judges it and a preview shows it.
        /// <paramref name="pathLike"/>: it carries a road itself (a path, stairs...).
        /// </summary>
        public ColonyRefusal RoadConflict(int slot, IReadOnlyList<ColonyCell> footprint, Vector3Int? entrance, bool tradingPost,
            bool pathLike, out string detail)
        {
            detail = null;
            // A Trading Post carries no road, and its doors are meant for two colonies' roads.
            if (tradingPost) return ColonyRefusal.None;
            ColonyCell? door = entrance == null ? (ColonyCell?)null : Cell(entrance.Value);
            return ColonyRoadRule.Conflict(slot, footprint, door, pathLike, this, out detail);
        }

        public static ColonyCell Cell(Vector3Int coordinates) => new ColonyCell(coordinates.x, coordinates.y, coordinates.z);

        private static Vector3Int Tile(ColonyCell cell) => new Vector3Int(cell.X, cell.Y, cell.Z);

        /// <summary>
        /// A path (or stairs...) on this cell, finished or still being built, by the colony that placed it; else the
        /// colony whose district has a finished road here. The district map only knows finished roads, brought up to
        /// date at the tick: another colony's path still being built was invisible to it, and a path laid beside it
        /// joined the two colonies' roads once both were done.
        /// </summary>
        public int? RoadOwnerAt(ColonyCell cell)
        {
            Vector3Int tile = Tile(cell);
            BlockObject path = _blockService.GetPathObjectAt(tile);
            if (path && !path.IsPreview && path.HasComponent<PathSpec>())
            {
                int? owner = DistrictOwner.OwnerOf(path);
                if (owner != null) return owner;
            }
            Vector3 position = CoordinateSystem.GridToWorldCentered(tile);
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                if (districtCenter.District == null) continue;
                if (_districtService.IsOnInstantDistrictRoad(districtCenter.District, position))
                    return DistrictOwner.OwnerOfDistrict(districtCenter);
            }
            return null;
        }

        // The four neighbours at the same height.
        private static readonly Vector3Int[] Sides = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };

        /// <summary>
        /// The colony of a building, finished or not, whose entrance is this cell: its door faces the cell from beside.
        /// A Trading Post's halves are left out: each half's entrance takes its own colony's road.
        /// </summary>
        public int? EntranceOwnerAt(ColonyCell cell)
        {
            Vector3Int tile = Tile(cell);
            for (int i = 0; i < Sides.Length; i++)
            {
                foreach (BlockObject blockObject in _blockService.GetObjectsAt(tile + Sides[i]))
                {
                    if (!blockObject || blockObject.IsPreview || !blockObject.HasEntrance) continue;
                    if (blockObject.PositionedEntrance.Coordinates != tile) continue;
                    if (TradingPosts.IsTradingPostBuilding(blockObject.GetComponent<EntityComponent>())) continue;
                    int? owner = DistrictOwner.OwnerOf(blockObject);
                    if (owner != null) return owner;
                }
            }
            return null;
        }

        public static Placement ToPlacement(ColonyPlacement placement) =>
            new Placement(new Vector3Int(placement.X, placement.Y, placement.Z), (Orientation)placement.Orientation,
                placement.IsFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
    }
}
