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
        public void SetSlot(int newSlot) => slot = newSlot;

        // ---- owner of anything ----

        public static int? OwnerOfDistrict(DistrictCenter districtCenter) =>
            districtCenter ? districtCenter.GetComponent<DistrictOwner>()?.Slot : null;

        /// <summary>
        /// The slot owning an entity: a district center directly, a beaver or bot by the district it lives in, a
        /// building by its district (or, while under construction, the district building it). Null when it has no
        /// district: nobody owns it.
        /// </summary>
        public static int? OwnerOf(BaseComponent component)
        {
            if (!component) return null;
            DistrictOwner owner = component.GetComponent<DistrictOwner>();
            if (owner != null) return owner.Slot;
            Citizen citizen = component.GetComponent<Citizen>();
            if (citizen != null) return OwnerOfDistrict(citizen.AssignedDistrict);
            DistrictBuilding districtBuilding = component.GetComponent<DistrictBuilding>();
            if (districtBuilding != null) return OwnerOfDistrict(districtBuilding.GetDistrictOrConstructionDistrict());
            return null;
        }
    }
}
