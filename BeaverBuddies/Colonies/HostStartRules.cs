namespace BeaverBuddies.Colonies
{
    /// <summary>When the host's own action is held back for a word from the host, kept free of the game for the checks.</summary>
    public static class HostStartRules
    {
        /// <summary>
        /// The host, at tick 0, with players still able to join, about to play something that changes the game (which
        /// would close joining), and not yet told to go ahead.
        /// </summary>
        public static bool ShouldHold(bool isHost, bool acceptingClients, int ticksSinceLoad, bool changesGame, bool confirmed) =>
            isHost && acceptingClients && ticksSinceLoad == 0 && changesGame && !confirmed;
    }
}
