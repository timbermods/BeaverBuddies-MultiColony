using BeaverBuddies.IO;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The seats of the current session: which colony the host plays (guests play the other). Not simulation state:
    /// only the host judges actions with it, and a guest uses it for notices and for refusing an action early.
    /// </summary>
    public static class ColonySession
    {
        /// <summary>The host's colony for this session. Latched by the host when it starts hosting; told to guests.</summary>
        public static int HostColony { get; private set; } = 1;

        /// <summary>Debug only: the host's own actions count as the other colony's, so one person can test both sides.</summary>
        public static bool HostSeatFlipped { get; private set; }

        /// <summary>The host starts hosting: its settings choose its colony for the whole session.</summary>
        public static void BeginHostSession()
        {
            HostColony = Settings.HostColonyValue;
            HostSeatFlipped = false;
            Plugin.Log($"[Colony] Hosting as colony {HostColony}; guests play colony {ColonySeats.GuestColony(HostColony)}");
        }

        /// <summary>A guest learns the host's colony from the host's first message.</summary>
        public static void AdoptHostColony(int hostColony)
        {
            HostColony = ColonySeats.Normalize(hostColony);
            HostSeatFlipped = false;
            Plugin.Log($"[Colony] The host plays colony {HostColony}; this computer plays colony {ColonySeats.GuestColony(HostColony)}");
        }

        /// <summary>The colony the host's own actions count as, taking the debug flip into account.</summary>
        public static int EffectiveHostColony => HostSeatFlipped ? ColonySeats.GuestColony(HostColony) : HostColony;

        /// <summary>The colony a player number controls, as the host judges it.</summary>
        public static int ColonyOfPlayer(int player) =>
            player == ColonySeats.HostPlayer ? EffectiveHostColony : ColonySeats.ColonyOfPlayer(player, HostColony);

        /// <summary>The colony this computer's player controls.</summary>
        public static int LocalColony =>
            EventIO.Get() is ClientEventIO ? ColonySeats.GuestColony(HostColony) : EffectiveHostColony;

        /// <summary>
        /// Debug only, host only: flips which colony the host's own actions count as. Returns false (and changes
        /// nothing) anywhere else, so a guest can never use it.
        /// </summary>
        public static bool TryFlipHostSeat()
        {
            if (!Settings.Debug || !(EventIO.Get() is ServerEventIO)) return false;
            HostSeatFlipped = !HostSeatFlipped;
            Plugin.Log($"[Colony] Debug: the host's actions now count as colony {EffectiveHostColony}");
            return true;
        }
    }
}
