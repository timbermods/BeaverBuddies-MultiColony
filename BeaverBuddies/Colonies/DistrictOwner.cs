using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The player slot a district center belongs to, saved with it. Everything else a colony owns is found through its
    /// district: a building by the district it belongs to, a beaver by the district it lives in. Added to every
    /// district center (see ColonyConfigurator); outside a separate-colonies game every district center is slot 0's.
    /// </summary>
    public class DistrictOwner : BaseComponent, IPersistentEntity, IInitializableEntity
    {
        private static readonly ComponentKey DistrictOwnerKey = new ComponentKey("BeaverBuddies.DistrictOwner");
        private static readonly PropertyKey<int> SlotKey = new PropertyKey<int>("Slot");

        /// <summary>
        /// The owner for the district center being created right now, set around a replayed placement or founding
        /// (on every computer, at the same moment). Null otherwise.
        /// </summary>
        public static int? PendingSlot { get; set; }

        private int slot = -1;

        /// <summary>The owner's slot, 0 to ColonySlotTable.MaxSlots-1.</summary>
        public int Slot => slot < 0 ? 0 : slot;

        public void Save(IEntitySaver entitySaver)
        {
            if (slot >= 0) entitySaver.GetComponent(DistrictOwnerKey).Set(SlotKey, slot);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (entityLoader.TryGetComponent(DistrictOwnerKey, out IObjectLoader loader) && loader.Has(SlotKey))
                slot = loader.Get(SlotKey);
        }

        public void InitializeEntity()
        {
            if (slot >= 0) return;
            if (PendingSlot.HasValue)
            {
                slot = PendingSlot.Value;
                return;
            }
            // A save from the land-split alphas: the colony whose land the district center stands on.
            ColonyTerritory legacy = ColonyModeService.Instance?.LegacyTerritory;
            BlockObject blockObject = GetComponent<BlockObject>();
            if (legacy != null && blockObject != null)
            {
                slot = System.Math.Max(0, legacy.OwnerOf(ColonyGameWorld.TileOf(blockObject.Coordinates)) - 1);
                return;
            }
            // Older saves, shared games and the game's own starting building: the first player's.
            slot = 0;
        }

        /// <summary>For code that creates a district center and knows its owner (starting locations, founding).</summary>
        public void SetSlot(int newSlot)
        {
            slot = newSlot;
            ColonyDigest.Note("owner", GetComponent<EntityComponent>()?.EntityId.GetHashCode() ?? 0, newSlot);
        }

        // ---- owner of anything ----

        public static int? OwnerOfDistrict(DistrictCenter districtCenter) =>
            districtCenter ? districtCenter.GetComponent<DistrictOwner>()?.Slot : null;

        /// <summary>
        /// The slot owning an entity: a district center directly, a beaver or bot by the district it lives in, a
        /// finished building by its district, otherwise the colony that placed it (a construction site, a building cut
        /// off from its roads, a building with no entrance such as a dam). So a finished District Crossing half is its
        /// district's colony's, whoever placed it. Null when none of these applies: nobody owns it.
        /// </summary>
        /// <param name="useConstructionDistrict">
        /// Last, for a building from an older save that nobody placed in this mode: the district building it. The game
        /// works that out from its instant map, so simulation code passes false.
        /// </param>
        public static int? OwnerOf(BaseComponent component, bool useConstructionDistrict = true)
        {
            if (!component) return null;
            DistrictOwner owner = component.GetComponent<DistrictOwner>();
            if (owner != null) return owner.Slot;
            Citizen citizen = component.GetComponent<Citizen>();
            if (citizen != null) return OwnerOfDistrict(citizen.AssignedDistrict);
            DistrictBuilding districtBuilding = component.GetComponent<DistrictBuilding>();
            if (districtBuilding != null && districtBuilding.District) return OwnerOfDistrict(districtBuilding.District);
            ColonyStamp stamp = component.GetComponent<ColonyStamp>();
            if (stamp != null && stamp.IsStamped) return stamp.Slot;
            if (districtBuilding != null && useConstructionDistrict)
                return OwnerOfDistrict(districtBuilding.GetDistrictOrConstructionDistrict());
            return null;
        }
    }
}
