using System;
using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The rules' view of the running game. Reads only: entities are looked up in the registry and owners found
    /// through their districts. Nothing here touches the random state or posts an event, so the host can judge an
    /// action without changing anything.
    /// </summary>
    public class ColonyGameWorld : IColonyWorld
    {
        private readonly EntityRegistry _entityRegistry;

        public ColonyGameWorld(EntityRegistry entityRegistry)
        {
            _entityRegistry = entityRegistry;
        }

        private EntityComponent Entity(string entityId) =>
            Guid.TryParse(entityId, out Guid guid) ? _entityRegistry.GetEntity(guid) : null;

        public int? OwnerOf(string entityId)
        {
            EntityComponent entity = Entity(entityId);
            return entity == null ? null : DistrictOwner.OwnerOf(entity);
        }

        public bool IsCrossing(string entityId) => Entity(entityId)?.GetComponent<DistrictCrossing>() != null;

        public bool IsUnlockedFor(int slot, string templateName) =>
            ColonyScienceService.Instance?.IsUnlockedFor(slot, templateName) ?? true;

        public static ColonyTile TileOf(Vector3Int coordinates) => new ColonyTile(coordinates.x, coordinates.y);

        public static Placement ToPlacement(ColonyPlacement placement) =>
            new Placement(new Vector3Int(placement.X, placement.Y, placement.Z), (Orientation)placement.Orientation,
                placement.IsFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
    }
}
