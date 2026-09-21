using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The rules' view of the running game. Reads only: footprints come from the building's spec and the placement
    /// (no preview object is created), and entities are looked up in the registry. Nothing here touches the random
    /// state or posts an event, so the host can judge an action without changing anything.
    /// </summary>
    public class ColonyGameWorld : IColonyWorld
    {
        private readonly EntityRegistry _entityRegistry;
        private readonly BuildingService _buildingService;
        private readonly BlockService _blockService;
        private readonly bool checkCrossings;

        // Crossing halves allowed in the current tick, which may not stand in the world yet when the second half of
        // their pair is judged: (tick, tile, height).
        private int rememberedTick = -1;
        private readonly HashSet<(ColonyTile, int)> rememberedCrossings = new HashSet<(ColonyTile, int)>();

        /// <param name="checkCrossings">
        /// True on the host, which judges with the world as it is. False for a player's own check before sending,
        /// where the other half of a pair has not been placed yet because both halves are placed together.
        /// </param>
        public ColonyGameWorld(EntityRegistry entityRegistry, BuildingService buildingService, BlockService blockService,
            bool checkCrossings)
        {
            _entityRegistry = entityRegistry;
            _buildingService = buildingService;
            _blockService = blockService;
            this.checkCrossings = checkCrossings;
        }

        public ColonyTile? EntityTile(string entityId)
        {
            if (!Guid.TryParse(entityId, out Guid guid)) return null;
            EntityComponent entity = _entityRegistry.GetEntity(guid);
            if (entity == null) return null;
            BlockObject blockObject = entity.GetComponent<BlockObject>();
            if (blockObject == null) return null;
            return TileOf(blockObject.Coordinates);
        }

        public IReadOnlyList<ColonyTile> Footprint(ColonyPlacement placement)
        {
            BlockObjectSpec spec = Spec(placement.TemplateName)?.GetSpec<BlockObjectSpec>();
            if (spec == null) return null;
            return FootprintOf(spec.GetBlocks(ToPlacement(placement)).Select(b => b.Coordinates));
        }

        public bool IsCrossing(string templateName) =>
            Spec(templateName)?.Blueprint?.HasSpec<DistrictCrossingSpec>() == true;

        public ColonyTile? BackStep(ColonyPlacement placement) => BackStepOf((Orientation)placement.Orientation);

        public bool HasCrossingAt(ColonyTile tile, int z)
        {
            if (!checkCrossings) return true;
            if (rememberedCrossings.Contains((tile, z))) return true;
            return _blockService.GetBottomObjectComponentAt<DistrictCrossing>(new Vector3Int(tile.X, tile.Y, z)) != null;
        }

        /// <summary>The host allowed a crossing half: its pair, judged next, may rely on it standing there.</summary>
        public void RememberCrossing(ColonyPlacement placement, int tick)
        {
            if (tick != rememberedTick)
            {
                rememberedCrossings.Clear();
                rememberedTick = tick;
            }
            IReadOnlyList<ColonyTile> footprint = Footprint(placement);
            if (footprint == null) return;
            foreach (ColonyTile tile in footprint) rememberedCrossings.Add((tile, placement.Z));
        }

        private BuildingSpec Spec(string templateName)
        {
            if (string.IsNullOrEmpty(templateName)) return null;
            try { return _buildingService.GetBuildingTemplate(templateName); }
            catch (Exception) { return null; }
        }

        public static ColonyTile TileOf(Vector3Int coordinates) => new ColonyTile(coordinates.x, coordinates.y);

        /// <summary>The distinct X/Y tiles of a set of blocks, in first-seen order. Every level of a column counts once.</summary>
        public static List<ColonyTile> FootprintOf(IEnumerable<Vector3Int> blocks)
        {
            var seen = new HashSet<ColonyTile>();
            var result = new List<ColonyTile>();
            foreach (Vector3Int block in blocks)
            {
                ColonyTile tile = TileOf(block);
                if (seen.Add(tile)) result.Add(tile);
            }
            return result;
        }

        /// <summary>The step to the tile behind a building, as the game's BlockObject.CoordinatesBehind sees it.</summary>
        public static ColonyTile BackStepOf(Orientation orientation)
        {
            Vector3Int step = orientation.Transform(new Vector3Int(0, 1, 0));
            return new ColonyTile(step.x, step.y);
        }

        public static Placement ToPlacement(ColonyPlacement placement) =>
            new Placement(new Vector3Int(placement.X, placement.Y, placement.Z), (Orientation)placement.Orientation,
                placement.IsFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
    }

    /// <summary>A world of one building preview, for judging what the player is about to place.</summary>
    public class ColonyPreviewWorld : IColonyWorld
    {
        private readonly IReadOnlyList<ColonyTile> footprint;
        private readonly bool crossing;
        private readonly ColonyTile backStep;

        public ColonyPreviewWorld(BlockObject preview)
        {
            footprint = ColonyGameWorld.FootprintOf(preview.PositionedBlocks.GetAllCoordinates());
            crossing = preview.GetComponent<DistrictCrossing>() != null;
            backStep = ColonyGameWorld.BackStepOf(preview.Orientation);
        }

        public ColonyTile? EntityTile(string entityId) => null;
        public IReadOnlyList<ColonyTile> Footprint(ColonyPlacement placement) => footprint;
        public bool IsCrossing(string templateName) => crossing;
        public ColonyTile? BackStep(ColonyPlacement placement) => backStep;
        // The game places a pair of previews together; the host checks for the real half.
        public bool HasCrossingAt(ColonyTile tile, int z) => true;
    }
}
