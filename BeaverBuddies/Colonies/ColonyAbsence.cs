namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// When an absent player's colony is handed over, kept free of the game so it can be checked headless. The host
    /// counts the days of hosted play a player has missed in a row (ColonyLifecycle): once the count has reached the
    /// host's limit and nobody keeps the colony, the day's check announces it, and the next day's check hands it over.
    /// </summary>
    public static class ColonyAbsence
    {
        /// <summary>The next daily check hands the colony over (0 means never).</summary>
        public static bool IsDue(int daysAway, int limit) => limit > 0 && daysAway >= limit;

        /// <summary>The count has just reached the limit: unless the player is in the game tomorrow, it is handed over then.</summary>
        public static bool IsDueTomorrow(int daysAway, int limit) => limit > 0 && daysAway == limit;

        /// <summary>Days left before a hand-over, or null when there is no limit.</summary>
        public static int? DaysLeft(int daysAway, int limit) => limit > 0 ? (int?)System.Math.Max(0, limit - daysAway) : null;

        /// <summary>
        /// Whether today's check announces the colony's hand-over for the next one: its player has missed at least the
        /// host's limit of days, nobody keeps it today (its player, or its steward, in the game), and a colony is there to
        /// take it (<paramref name="hasReceiver"/>: in a mixed game only one of its own faction, since 1.4.0-rc2; with
        /// none it waits, and nobody is warned of a hand-over that could not come). A colony whose count passed the limit
        /// while a steward kept it is announced on the first day nobody does, not handed over unwarned.
        /// </summary>
        public static bool IsAnnounced(int daysAway, int limit, bool kept, bool hasReceiver = true) =>
            IsDue(daysAway, limit) && !kept && hasReceiver;

        /// <summary>
        /// Whether today's check hands the colony over: the last check announced it, and it is still due, kept by nobody,
        /// with a colony to take it. So a hand-over for absence always comes the day after its warning, in the same session
        /// (the first check after a load announces; E-3 of the 1.4.0-rc1 review).
        /// </summary>
        public static bool IsHandedOver(int daysAway, int limit, bool kept, bool announcedAtLastCheck, bool hasReceiver = true) =>
            announcedAtLastCheck && IsAnnounced(daysAway, limit, kept, hasReceiver);
    }
}
