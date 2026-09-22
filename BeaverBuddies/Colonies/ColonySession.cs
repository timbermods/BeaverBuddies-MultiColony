using BeaverBuddies.IO;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The seats of the current session, as the rest of the mod asks for them. Not simulation state: the host judges
    /// actions with the slots (and writes the actor's slot into each action it plays, so replays never ask), and every
    /// computer uses its own slot for display. The slots themselves come from ColonySlotService.
    /// </summary>
    public static class ColonySession
    {
        public const int HostPlayer = 0;

        /// <summary>
        /// The host has separate colonies on for this session: a player without a colony may found one in any save.
        /// Latched by the host when it starts hosting; told to guests.
        /// </summary>
        public static bool HostAllowsFounding { get; private set; }

        /// <summary>
        /// The host's choice of separate science and unlocks, used when a colony is founded in a shared game (every
        /// computer founds it, so every computer must use the host's choice). Latched when hosting; told to guests.
        /// </summary>
        public static bool HostSeparateScience { get; private set; } = true;

        /// <summary>Debug only: how many slots the host's own actions are shifted by, to test other colonies alone.</summary>
        public static int HostSlotShift { get; private set; }

        /// <summary>The host starts hosting: its settings are fixed for the whole session.</summary>
        public static void BeginHostSession()
        {
            HostAllowsFounding = Settings.SeparateColoniesForNewGames;
            HostSeparateScience = Settings.SeparateScienceForNewColonies;
            HostSlotShift = 0;
            Plugin.Log($"[Colony] Hosting; founding a colony {(HostAllowsFounding ? "allowed" : "off")}");
        }

        /// <summary>A guest learns the host's choice from the host's first message.</summary>
        public static void AdoptHostChoice(bool hostAllowsFounding, bool hostSeparateScience)
        {
            HostAllowsFounding = hostAllowsFounding;
            HostSeparateScience = hostSeparateScience;
            HostSlotShift = 0;
        }

        private static ColonySlotService Slots => ColonySlotService.Instance;

        /// <summary>The seat a connection was given (its own colony), whatever it acts as now; -1 before it has said hello.</summary>
        public static int SeatOfPlayer(int player) => Slots?.SlotOfPlayer(player) ?? (player == HostPlayer ? 0 : -1);

        /// <summary>
        /// The slot a connection's actions count as, as the host judges it: its seat, or a colony it looks after and
        /// has switched into (see ColonyStewards); -1 before it has said hello.
        /// </summary>
        public static int SlotOfPlayer(int player)
        {
            int slot = SeatOfPlayer(player);
            if (slot >= 0)
            {
                int? acting = ColonyStewards.Instance?.ActingSlotOf(player);
                if (acting != null && acting.Value >= 0 && acting.Value < ColonySlotTable.MaxSlots) slot = acting.Value;
            }
            // The shift is a debug aid: it ends with detailed logging, whatever it was set to.
            if (player == HostPlayer && slot >= 0 && HostSlotShift != 0 && Settings.Debug)
                slot = (slot + HostSlotShift) % ColonySlotTable.MaxSlots;
            return slot;
        }

        /// <summary>This computer's connection number: 0 on the host; on a guest -1 until the host has seated it.</summary>
        public static int LocalPlayer => EventIO.Get() is ClientEventIO ? Slots?.LocalPlayer ?? -1 : HostPlayer;

        /// <summary>This computer's slot, the colony its actions count as; -1 on a guest until the host has seated it.</summary>
        public static int LocalSlot
        {
            get
            {
                int local = LocalPlayer;
                return local < 0 ? -1 : SlotOfPlayer(local);
            }
        }

        /// <summary>This computer's own seat, whatever colony it acts as now; -1 on a guest until the host has seated it.</summary>
        public static int LocalSeat
        {
            get
            {
                int local = LocalPlayer;
                return local < 0 ? -1 : SeatOfPlayer(local);
            }
        }

        /// <summary>
        /// Debug only, host only, alone only: the host's own actions count as the next slot's. Returns false (and
        /// changes nothing) anywhere else, so a guest can never use it, and a guest never sees the host act as another
        /// colony.
        /// </summary>
        public static bool TryFlipHostSeat()
        {
            if (!Settings.Debug || !(EventIO.Get() is ServerEventIO server)) return false;
            if ((server.NetBase?.ClientCount ?? 0) > 0)
            {
                Plugin.Log("[Colony] Debug: the host's seat is not flipped while guests are connected");
                return false;
            }
            HostSlotShift = (HostSlotShift + 1) % ColonySlotTable.MaxSlots;
            Plugin.Log($"[Colony] Debug: the host's actions now count as slot {SlotOfPlayer(HostPlayer)}");
            ColonyScienceService.Instance?.RefreshToolLocks();
            return true;
        }
    }
}
