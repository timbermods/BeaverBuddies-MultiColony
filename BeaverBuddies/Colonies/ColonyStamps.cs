using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
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
    /// <see cref="ColonyStamps"/>): buildings from older saves, those the game creates itself, and those placed before
    /// the game was hosted (placed directly, not as an action, so nothing named the placer).
    /// Separate colonies only: in a shared game no building is stamped, and nothing is saved.
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
            // Separate colonies only (a shared save from an earlier build may still carry a stamp: it is not written again).
            if (!ColonyModeService.IsSeparateColonies) return;
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
            // A path looks like its colony's faction in a mixed game (one template serves both factions).
            if (slot >= 0) BeaverBuddies.Factions.FactionModels.RepaintIfPath(this);
        }

        /// <param name="counted">Counted in the colony digest, one change per building. A shared game being split
        /// stamps all its buildings at once and counts that as one change instead (ColonyStamps.Begin).</param>
        internal void Stamp(int newSlot, bool counted = true)
        {
            slot = newSlot;
            if (counted) ColonyDigest.Note("stamp", GetComponent<EntityComponent>()?.EntityId.GetHashCode() ?? 0, newSlot);
            BeaverBuddies.Factions.FactionModels.RepaintIfPath(this);
        }
    }

    /// <summary>
    /// Gives every building a colony in a separate-colonies game. A building placed as an action is stamped as it is
    /// made (see <see cref="ColonyStamp"/>); the rest wait here until they have a district, or a district's road at
    /// their entrance, and take its owner. Everything it reads is the same on every computer (which buildings stand,
    /// whose districts are whose, the tick-updated district map), in the order the buildings came, and it only changes
    /// in the simulation, so every computer stamps the same buildings at the same tick.
    /// Trading Posts are left alone: they stand between colonies, and their halves go by their districts.
    ///
    /// Separate colonies only. A shared game stamps nothing, as in the Stability Fork, until a separate-colonies game
    /// is loaded or made, or a founding splits the shared game (<see cref="Begin"/>).
    /// </summary>
    public class ColonyStamps : RegisteredSingleton, ILoadableSingleton, IPostLoadableSingleton, ITickableSingleton
    {
        // Buildings without a colony are looked at again this often (ticks), not every tick.
        private const int UnstampedCheckInterval = 16;

        private readonly EventBus _eventBus;
        private readonly EntityRegistry _entityRegistry;
        private readonly IDistrictService _districtService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;

        private int ticks;
        /// <summary>Diagnostics: the stamping phase (not saved; the same on every computer that loaded together).</summary>
        public int Ticks => ticks;

        // Whether buildings are stamped: from Begin (a separate-colonies game) on. Not saved: a save's mode says it again.
        private bool active;

        // Buildings with no colony yet (older saves, buildings the game made, buildings placed before hosting), in the
        // order they came in, so every computer stamps them in the same order (each stamp is a change in the colony
        // digest); the set answers "is it waiting?" without a walk down the list.
        private readonly List<ColonyStamp> unstamped = new List<ColonyStamp>();
        private readonly HashSet<ColonyStamp> unstampedSet = new HashSet<ColonyStamp>();

        public static ColonyStamps Instance => SingletonManager.GetSingleton<ColonyStamps>();

        public ColonyStamps(EventBus eventBus, EntityRegistry entityRegistry, IDistrictService districtService,
            DistrictCenterRegistry districtCenterRegistry)
        {
            _eventBus = eventBus;
            _entityRegistry = entityRegistry;
            _districtService = districtService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        public void Load() => _eventBus.Register(this);

        public void PostLoad()
        {
            // Buildings loaded from the save (a new game's mode may have begun already: tracking twice is harmless,
            // each building waits once).
            if (ColonyModeService.IsSeparateColonies) Begin(splittingSharedGame: false);
        }

        /// <summary>
        /// Starts stamping: a separate-colonies game loaded or made (ColonyModeService), or a shared game split by a
        /// founding, played on every computer at the same tick. A shared game has one colony, so every building in it is
        /// that colony's (slot 0): being split, its buildings are stamped so at once, without the usual search, and the
        /// digest counts the stamps as one change (the number of buildings).
        /// </summary>
        internal void Begin(bool splittingSharedGame)
        {
            active = true;
            List<EntityComponent> entities = _entityRegistry.Entities.ToList();
            if (splittingSharedGame)
            {
                int stamped = 0;
                foreach (EntityComponent entity in entities)
                {
                    ColonyStamp stamp = PlacedBuilding(entity);
                    if (stamp == null || stamp.IsStamped) continue;
                    stamp.Stamp(0, counted: false);
                    stamped++;
                }
                ColonyDigest.Note("shared colony", stamped);
                Plugin.Log($"[Colony] The shared colony's {stamped} buildings are colony 0's");
            }
            foreach (EntityComponent entity in entities) Track(entity);
        }

        [OnEvent]
        public void OnEntityInitialized(EntityInitializedEvent entityInitializedEvent)
        {
            if (active) Track(entityInitializedEvent.Entity);
        }

        [OnEvent]
        public void OnEntityDeleted(EntityDeletedEvent entityDeletedEvent)
        {
            ColonyStamp stamp = entityDeletedEvent.Entity.GetComponent<ColonyStamp>();
            if (stamp != null && unstampedSet.Remove(stamp)) unstamped.Remove(stamp);
        }

        public void Tick()
        {
            if (!active || unstamped.Count == 0) return;
            // At the first tick as well, so a save's unstamped buildings have their colony before anyone can act on them.
            if (++ticks != 1 && ticks % UnstampedCheckInterval != 0) return;
            // A building without a colony takes the owner of its finished district, or else of the district whose road
            // is at its entrance (a construction site has no district until it is finished) or, for a path, whose road
            // it is. Read from the tick-updated district map, in the simulation. It keeps waiting while it has neither.
            for (int i = unstamped.Count - 1; i >= 0; i--)
            {
                ColonyStamp stamp = unstamped[i];
                if (!stamp)
                {
                    unstamped.RemoveAt(i);
                    unstampedSet.Remove(stamp);
                    continue;
                }
                int? owner = DistrictOwner.OwnerOfDistrict(stamp.GetComponent<DistrictBuilding>()?.District) ?? RoadOwner(stamp);
                if (owner == null) continue;
                unstamped.RemoveAt(i);
                unstampedSet.Remove(stamp);
                stamp.Stamp(owner.Value);
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

        /// <summary>A placed building that takes a colony (not a preview, not a Trading Post): its stamp. Else null.</summary>
        private static ColonyStamp PlacedBuilding(EntityComponent entity)
        {
            if (entity == null) return null;
            ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
            if (stamp == null) return null;
            BlockObject blockObject = entity.GetComponent<BlockObject>();
            if (blockObject == null || blockObject.IsPreview || !blockObject.Positioned) return null;
            if (TradingPosts.IsTradingPostBuilding(entity)) return null;
            return stamp;
        }

        private void Track(EntityComponent entity)
        {
            ColonyStamp stamp = PlacedBuilding(entity);
            if (stamp == null || stamp.IsStamped || unstampedSet.Contains(stamp)) return;
            // A district center is its owner's from the moment it is made (saved with it), whether it was founded, a
            // map's start, or the game's own starting building, which no action places.
            DistrictOwner districtOwner = entity.GetComponent<DistrictOwner>();
            if (districtOwner != null)
            {
                stamp.Stamp(districtOwner.Slot);
                return;
            }
            unstamped.Add(stamp);
            unstampedSet.Add(stamp);
        }

        /// <summary>Buildings still waiting for a colony (diagnostics).</summary>
        public int Unstamped => unstamped.Count;
    }
}
