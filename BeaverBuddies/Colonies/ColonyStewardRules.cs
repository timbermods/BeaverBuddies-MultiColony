namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Who may look after whose colony, kept free of the game so it can be checked headless. A steward is a player
    /// (by their stable id, as the slot table knows players), never a colony: a helper without a colony of their own
    /// can be one too.
    /// </summary>
    public static class ColonyStewardRules
    {
        /// <summary>
        /// A colony's owner may ask any other player in the session to look after it; the host may do so for a colony
        /// whose player is away. Never the owner themself, and never a player the session does not know.
        /// </summary>
        public static bool MayGrant(bool actorIsHost, int actorSeat, int slot, bool ownerPresent, string stewardId, string ownerId,
            bool stewardKnown)
        {
            if (slot < 0 || slot >= ColonySlotTable.MaxSlots || string.IsNullOrEmpty(stewardId) || !stewardKnown) return false;
            if (ownerId != null && stewardId == ownerId) return false;
            if (actorSeat == slot) return true;
            return actorIsHost && !ownerPresent;
        }

        /// <summary>The owner, the steward themself, or the host ends a stewardship.</summary>
        public static bool MayRevoke(bool actorIsHost, int actorSeat, string actorId, int slot, string stewardId)
        {
            if (slot < 0 || slot >= ColonySlotTable.MaxSlots || stewardId == null) return false;
            return actorIsHost || actorSeat == slot || (actorId != null && actorId == stewardId);
        }

        /// <summary>A player acts as their own seat (-1 or the seat itself), or as a colony they look after.</summary>
        public static bool MayActAs(int actorSeat, string actorId, int slot, string stewardId)
        {
            if (slot < 0 || slot == actorSeat) return true;
            if (slot >= ColonySlotTable.MaxSlots || actorId == null) return false;
            return stewardId != null && stewardId == actorId;
        }
    }
}
