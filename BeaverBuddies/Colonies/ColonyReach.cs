using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.Navigation;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The colony that placed a building, saved with it. Every building a player places carries the placer's colony
    /// from the moment it is a construction site, so it has an owner before it joins a district, and keeps one if its
    /// road is ever cut. A district center carries its owner's from the start. The rest take an owner later (see
    /// <see cref="ColonyReach"/>): buildings from older saves, those the game creates itself, and those placed before
    /// the game was hosted (placed directly, not as an action, so nothing named the placer).
    /// </summary>
    public class ColonyStamp : BaseComponent, IPersistentEntity, IInitializableEntity
    {
        private static readonly ComponentKey StampKey = new ComponentKey("BeaverBuddies.ColonyStamp");
        private static readonly PropertyKey<int> SlotKey = new PropertyKey<int>("Slot");

        private int slot = -1;

        public int Slot => slot;
        public bool IsStamped => slot >= 0;

        public void Save(IEntitySaver entitySaver)
        {
            if (slot >= 0) entitySaver.GetComponent(StampKey).Set(SlotKey, slot);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (entityLoader.TryGetComponent(StampKey, out IObjectLoader loader) && loader.Has(SlotKey))
                slot = loader.Get(SlotKey);
        }

        public void InitializeEntity()
        {
            // A building placed by a player's action: the host wrote the actor's colony into the action, and the
            // placement sets it here for the moment the entity is made (the same on every computer).
            if (slot < 0 && DistrictOwner.PendingSlot.HasValue) slot = DistrictOwner.PendingSlot.Value;
        }

        internal void Stamp(int newSlot) => slot = newSlot;
    }

    /// <summary>
    /// How far each colony reaches (see <see cref="ColonyReachGrid"/>), kept up to date as buildings are made and
    /// removed. Everything it reads is the same on every computer (which buildings stand, and whose they are, in the
    /// order they came), and it only changes in the simulation, so the answers are the same on every computer at every
    /// tick. The owners of tiles two colonies reach depend on that order, so they are saved.
    /// Trading Posts count for nobody: they stand between colonies. A District Crossing is its colony's, like any
    /// building.
    /// </summary>
    public class ColonyReach : RegisteredSingleton, ILoadableSingleton, IPostLoadableSingleton, ITickableSingleton, ISaveableSingleton
    {
        private static readonly SingletonKey ReachKey = new SingletonKey("BeaverBuddies.ColonyReach");
        private static readonly ListKey<string> ContestedKey = new ListKey<string>("Contested");
        // Buildings without a colony are looked at again this often (ticks), not every tick.
        private const int UnstampedCheckInterval = 16;

        private readonly IBlockService _blockService;
        private readonly EventBus _eventBus;
        private readonly EntityRegistry _entityRegistry;
        private readonly ISingletonLoader _singletonLoader;
        private readonly IDistrictService _districtService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;

        private readonly List<(int x, int y, int slot)> savedOwners = new List<(int, int, int)>();
        private int ticks;

        private ColonyReachGrid map;

        // Made on first use: by then every service has loaded (entities load after them), so the map's size is known.
        // A map whose size was still unknown is made again, once the size is.
        private ColonyReachGrid grid
        {
            get
            {
                Vector3Int size = _blockService.Size;
                if (map == null || (map.Width == 0 && size.x > 0)) map = new ColonyReachGrid(size.x, size.y);
                return map;
            }
        }
        // What each building added, so removing it takes exactly that away again.
        private readonly Dictionary<EntityComponent, (int slot, List<(int, int)> tiles)> added =
            new Dictionary<EntityComponent, (int, List<(int, int)>)>();
        // Buildings with no colony yet (older saves, buildings the game made, buildings placed before hosting): stamped
        // once they have a district, a district's road at their entrance, or else one colony's land under them.
        private readonly List<ColonyStamp> unstamped = new List<ColonyStamp>();

        public static ColonyReach Instance => SingletonManager.GetSingleton<ColonyReach>();

        public ColonyReach(IBlockService blockService, EventBus eventBus, EntityRegistry entityRegistry,
            ISingletonLoader singletonLoader, IDistrictService districtService, DistrictCenterRegistry districtCenterRegistry)
        {
            _blockService = blockService;
            _eventBus = eventBus;
            _entityRegistry = entityRegistry;
            _singletonLoader = singletonLoader;
            _districtService = districtService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        public void Load()
        {
            _eventBus.Register(this);
            if (!_singletonLoader.TryGetSingleton(ReachKey, out IObjectLoader loader) || !loader.Has(ContestedKey)) return;
            foreach (string entry in loader.Get(ContestedKey))
            {
                string[] parts = entry.Split('|');
                if (parts.Length == 3 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y)
                    && int.TryParse(parts[2], out int slot))
                    savedOwners.Add((x, y, slot));
            }
        }

        public void PostLoad()
        {
            // Buildings loaded from the save (tracking twice is harmless: each entity is counted once).
            foreach (EntityComponent entity in _entityRegistry.Entities.ToList()) Track(entity);
            // Rebuilding from the buildings cannot tell who reached a shared tile first; the save can.
            foreach (var (x, y, slot) in savedOwners) grid.RestoreOwner(x, y, slot);
            savedOwners.Clear();
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            List<string> contested = grid.ContestedTiles().Select(t => $"{t.x}|{t.y}|{t.slot}").ToList();
            if (contested.Count > 0) singletonSaver.GetSingleton(ReachKey).Set(ContestedKey, contested);
        }

        [OnEvent]
        public void OnEntityInitialized(EntityInitializedEvent entityInitializedEvent) => Track(entityInitializedEvent.Entity);

        [OnEvent]
        public void OnEntityDeleted(EntityDeletedEvent entityDeletedEvent)
        {
            EntityComponent entity = entityDeletedEvent.Entity;
            if (added.TryGetValue(entity, out var contribution))
            {
                added.Remove(entity);
                grid.Apply(contribution.slot, contribution.tiles, -1);
            }
            ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
            if (stamp != null) unstamped.Remove(stamp);
        }

        public void Tick()
        {
            if (unstamped.Count == 0) return;
            // At the first tick as well, so a save's unstamped buildings give their colony its land before anyone can
            // found a colony beside them (founding waits for the first tick).
            if (++ticks != 1 && ticks % UnstampedCheckInterval != 0) return;
            // A building without a colony takes the owner of its finished district, or else of the district whose road
            // is at its entrance (a construction site has no district until it is finished) or, for a path, whose road
            // it is. Read from the tick-updated district map, in the simulation. Failing those (no road reaches it),
            // the colony whose land it stands on. It keeps waiting while it has none of these.
            for (int i = unstamped.Count - 1; i >= 0; i--)
            {
                ColonyStamp stamp = unstamped[i];
                if (!stamp)
                {
                    unstamped.RemoveAt(i);
                    continue;
                }
                int? owner = DistrictOwner.OwnerOfDistrict(stamp.GetComponent<DistrictBuilding>()?.District)
                    ?? RoadOwner(stamp) ?? LandOwner(stamp);
                if (owner == null) continue;
                unstamped.RemoveAt(i);
                stamp.Stamp(owner.Value);
                Add(stamp.GetComponent<EntityComponent>(), owner.Value);
            }
        }

        private int? RoadOwner(ColonyStamp stamp)
        {
            BlockObject blockObject = stamp.GetComponent<BlockObject>();
            if (!blockObject || !blockObject.Positioned) return null;
            // A building with an entrance: the tile its road must reach, the one the game picks a construction site's
            // builders by (DistrictBuilding.ShouldBeAssignedToConstructionDistrict), worked out from its placement.
            // Anything else (a path): its own tile.
            BuildingAccessible accessible = stamp.GetComponent<BuildingAccessible>();
            Vector3 position = accessible != null
                ? accessible.CalculateAccess()
                : CoordinateSystem.GridToWorldCentered(blockObject.Coordinates);
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                if (districtCenter.District == null) continue;
                if (_districtService.IsOnDistrictRoad(districtCenter.District, position))
                    return DistrictOwner.OwnerOfDistrict(districtCenter);
            }
            return null;
        }

        // A building no road reaches (a construction site its builders cannot get to, or one cut off from its roads):
        // the colony whose land it stands on, if it is one colony's. Land is the same on every computer at every tick.
        private int? LandOwner(ColonyStamp stamp)
        {
            BlockObject blockObject = stamp.GetComponent<BlockObject>();
            if (!blockObject || !blockObject.Positioned) return null;
            return grid.SoleOwner(blockObject.PositionedBlocks.GetAllCoordinates().Select(c => (c.x, c.y)));
        }

        private void Track(EntityComponent entity)
        {
            if (entity == null || added.ContainsKey(entity)) return;
            ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
            if (stamp == null || unstamped.Contains(stamp)) return;
            BlockObject blockObject = entity.GetComponent<BlockObject>();
            if (blockObject == null || blockObject.IsPreview || !blockObject.Positioned) return;
            if (TradingPosts.IsTradingPostBuilding(entity)) return;
            // A district center is its owner's from the moment it is made (saved with it), whether it was founded, a
            // map's start, or the game's own starting building, which no action places.
            DistrictOwner districtOwner = entity.GetComponent<DistrictOwner>();
            if (!stamp.IsStamped && districtOwner != null) stamp.Stamp(districtOwner.Slot);
            if (stamp.IsStamped) Add(entity, stamp.Slot);
            else unstamped.Add(stamp);
        }

        private void Add(EntityComponent entity, int slot)
        {
            BlockObject blockObject = entity ? entity.GetComponent<BlockObject>() : null;
            if (blockObject == null || !blockObject.Positioned || added.ContainsKey(entity)) return;
            long started = ColonyProfiler.Start();
            var tiles = blockObject.PositionedBlocks.GetAllCoordinates()
                .Select(c => (c.x, c.y)).Distinct().ToList();
            grid.Apply(slot, tiles, +1);
            added[entity] = (slot, tiles);
            ColonyProfiler.Stop("Land bookkeeping", started);
        }

        // ---- questions ----

        public bool Reaches(int slot, Vector3Int tile) => grid != null && grid.Reaches(slot, tile.x, tile.y);

        public int LandSize(int slot) => grid?.LandSize(slot) ?? 0;

        /// <summary>Buildings counted, and buildings still waiting for a colony (diagnostics).</summary>
        public (int counted, int unstamped) Counts => (added.Count, unstamped.Count);

        /// <summary>Changes whenever any colony's land may have changed (for displays).</summary>
        public int Version => grid?.Version ?? 0;

        /// <summary>A colony may use its own land and land nobody holds.</summary>
        public bool MayUse(int slot, Vector3Int tile) => grid == null || grid.MayUse(slot, tile.x, tile.y);

        /// <summary>Whether another colony reaches within 10 tiles of these: too close to found a colony.</summary>
        public bool OthersReachNear(int slot, IEnumerable<Vector3Int> tiles) =>
            grid != null && grid.OthersReachNear(slot, tiles.Select(t => (t.x, t.y)));

        /// <summary>The outline of a colony's land (display).</summary>
        public IEnumerable<(int x, int y)> BorderTiles(int slot) => grid?.BorderTiles(slot) ?? Enumerable.Empty<(int, int)>();

        /// <summary>
        /// A colony handed over: its land and its buildings' reach become <paramref name="to"/>'s. Played on every
        /// computer, with the buildings' stamps changed alongside (see ColonyHandover).
        /// </summary>
        internal void Transfer(int from, int to)
        {
            if (grid == null) return;
            grid.Transfer(from, to);
            foreach (EntityComponent entity in added.Keys.ToList())
            {
                var contribution = added[entity];
                if (contribution.slot == from) added[entity] = (to, contribution.tiles);
            }
        }

        /// <summary>Whose land the tile is (the colony that reached it first), or null.</summary>
        public int? Owner(Vector3Int tile) => grid?.Owner(tile.x, tile.y);
    }
}
