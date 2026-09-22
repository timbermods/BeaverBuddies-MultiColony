namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// When an absent player's colony is handed over, kept free of the game so it can be checked headless. The host
    /// counts the days of hosted play a player has missed in a row (ColonyLifecycle): once the count reaches the
    /// host's limit, the next day's check hands the colony over, so the day the count reaches the limit is the day
    /// to say so.
    /// </summary>
    public static class ColonyAbsence
    {
        /// <summary>The next daily check hands the colony over (0 means never).</summary>
        public static bool IsDue(int daysAway, int limit) => limit > 0 && daysAway >= limit;

        /// <summary>The count has just reached the limit: unless the player is in the game tomorrow, it is handed over then.</summary>
        public static bool IsDueTomorrow(int daysAway, int limit) => limit > 0 && daysAway == limit;

        /// <summary>Days left before a hand-over, or null when there is no limit.</summary>
        public static int? DaysLeft(int daysAway, int limit) => limit > 0 ? (int?)System.Math.Max(0, limit - daysAway) : null;
    }
}
