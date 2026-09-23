namespace BeaverBuddies.Colonies
{
    /// <summary>The decisions around the saved mode that need no game types, kept apart so they can be tested headless.</summary>
    public static class ColonyModeState
    {
        /// <summary>
        /// Whether automatic migration may pair two districts, given their owners' slots: only within one colony. A
        /// district without an owner (not possible for a district center) pairs with anyone, as in the game.
        /// </summary>
        public static bool SameOwner(int? a, int? b) => a == null || b == null || a.Value == b.Value;

        /// <summary>
        /// Whether a beaver or bot with no district may join a district of <paramref name="districtOwner"/>'s colony:
        /// only its own colony's, the one it was in when it last left a district (<paramref name="lastColony"/>). One that
        /// never had a district (a new game's start) joins the nearest, as in the game.
        /// </summary>
        public static bool MayJoin(int? lastColony, int? districtOwner) => lastColony == null || districtOwner == lastColony;
    }
}
