using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Keeps two players' road networks apart: they may only meet through a District Crossing. Filled in from the
    /// road-network research (Phase 2).
    /// </summary>
    public class ColonyRoadNetworks : RegisteredSingleton, ILoadableSingleton
    {
        public static ColonyRoadNetworks Instance => SingletonManager.GetSingleton<ColonyRoadNetworks>();

        public void Load() { }

        /// <summary>Whether a building placed here would join the roads of an existing district.</summary>
        public bool WouldJoinAnyDistrict(BlockObjectSpec spec, Placement placement) => false;
    }
}
