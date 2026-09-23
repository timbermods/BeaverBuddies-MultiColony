using BeaverBuddies.IO;
using System.Collections.Generic;
using System.Linq;

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
        /// The host allows founding a colony in a shared game this session (Mod Settings), which makes it a
        /// separate-colonies game for good. A separate-colonies game allows founding whatever this says. Latched by the
        /// host when it starts hosting; told to guests.
        /// </summary>
        public static bool HostAllowsFounding { get; private set; }

        /// <summary>
        /// The host's choice of separate science and unlocks, used when a colony is founded in a shared game (every
        /// computer founds it, so every computer must use the host's choice). Latched when hosting; told to guests.
        /// </summary>
        public static bool HostSeparateScience { get; private set; } = true;

        /// <summary>Debug only: how many slots the host's own actions are shifted by, to test other colonies alone.</summary>
        public static int HostSlotShift { get; private set; }

        private static volatile bool joiningClosedAtStart;

        /// <summary>
        /// This session began with joining already closed: the host started it from a new game's waiting room
        /// (BeaverBuddies.Lobby), where everyone came in before the world was made. Nobody can join late, so nothing waits
        /// for the first tick on their account (ColonyRules.WaitsForStart). Set by the host when it presses Start; told to
        /// guests in the host's first message. Read from any thread (the init event is built on a network thread).
        /// </summary>
        public static bool JoiningClosedAtStart => joiningClosedAtStart;

        private static volatile string[] hostFactions;

        /// <summary>
        /// A mixed-factions game: the factions a colony may take, the ones unlocked on the host's computer (D1). Latched
        /// by the host when the game loads (on the main thread: the profile is read there), told to guests in the host's
        /// first message. Null while unknown, which allows every faction (the host still judges every choice).
        /// </summary>
        public static IReadOnlyList<string> HostFactions => hostFactions;

        public static bool HostHasFaction(string faction) => hostFactions == null || System.Array.IndexOf(hostFactions, faction) >= 0;

        /// <summary>The host (or a game played alone) records the factions unlocked on its own profile.</summary>
        public static void LatchHostFactions(IEnumerable<string> factions)
        {
            hostFactions = factions?.ToArray();
            Plugin.Log($"[Factions] Factions this game may take: {(hostFactions == null ? "every one" : string.Join(", ", hostFactions))}");
        }

        /// <summary>A guest learns the host's factions from the host's first message (null from a game that is not mixed).</summary>
        public static void AdoptHostFactions(List<string> factions)
        {
            if (factions == null) return;
            hostFactions = factions.ToArray();
        }

        /// <summary>The host starts hosting: its settings are fixed for the whole session.</summary>
        public static void BeginHostSession()
        {
            HostAllowsFounding = Settings.FoundingInSharedGamesAllowed;
            HostSeparateScience = Settings.SeparateScienceForNewColonies;
            HostSlotShift = 0;
            joiningClosedAtStart = false;
            Plugin.Log($"[Colony] Hosting; founding a colony in a shared game {(HostAllowsFounding ? "allowed" : "off")}");
        }

        /// <summary>The host pressed Start in a new game's waiting room: this session never waits for late joiners.</summary>
        public static void CloseJoiningAtStart()
        {
            joiningClosedAtStart = true;
            Plugin.Log("[Colony] This session started from a waiting room: joining is closed from the start");
        }

        /// <summary>A guest learns the host's choice from the host's first message.</summary>
        public static void AdoptHostChoice(bool hostAllowsFounding, bool hostSeparateScience, bool hostClosedJoiningAtStart)
        {
            HostAllowsFounding = hostAllowsFounding;
            HostSeparateScience = hostSeparateScience;
            HostSlotShift = 0;
            joiningClosedAtStart = hostClosedJoiningAtStart;
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
