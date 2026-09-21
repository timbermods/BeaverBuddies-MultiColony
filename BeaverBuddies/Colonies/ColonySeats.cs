namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Which colony a player number controls. The host is player 0 and plays the colony chosen in its settings; every
    /// guest plays the other one. Seats are not part of the simulation: only the host judges actions with them, and a
    /// guest uses its own seat for display and for refusing an action before it is sent.
    /// </summary>
    public static class ColonySeats
    {
        public const int HostPlayer = 0;

        /// <summary>The colony guests play when the host plays <paramref name="hostColony"/>.</summary>
        public static int GuestColony(int hostColony) => hostColony == 1 ? 2 : 1;

        /// <summary>0 (no colony) for a negative number: an event the host could not attribute to a connection.</summary>
        public static int ColonyOfPlayer(int player, int hostColony) =>
            player < 0 ? 0 : player == HostPlayer ? hostColony : GuestColony(hostColony);

        /// <summary>Settings and messages may hold anything; only 1 and 2 are seats.</summary>
        public static int Normalize(int colony) => colony == 2 ? 2 : 1;
    }
}
