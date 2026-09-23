namespace BeaverBuddies.Connect
{
    /// <summary>What a guest's join does about a host's waiting room this frame (ClientConnectionService.CheckWaitingRoom).</summary>
    public enum WaitingRoomStep
    {
        /// <summary>Nothing to do: no waiting room, the connection is gone, or its save has already come.</summary>
        Nothing,
        /// <summary>The guest is in the main menu, where its page (LobbyGuestPanel) shows the room.</summary>
        ShowRoom,
        /// <summary>The guest is in a game, which a waiting room can't be entered from (D20): leave it and say why.</summary>
        LeaveForGame,
    }

    /// <summary>
    /// The join's decisions, apart from the game's UI so that StabilityTests can check them (System only).
    /// </summary>
    public static class JoinFlowRules
    {
        /// <summary>
        /// CheckWaitingRoom's decision. <paramref name="saveReceived"/>: LoadMap has been given the host's save. From then on
        /// the room is over for this guest, whatever the registry says: LoadMap empties it (SingletonManager.Reset) as the
        /// game loads, so the guest page is no longer found, and taking that for "in a game" dropped every waiting-room
        /// guest in the frame its save arrived (1.4.0-beta18 to beta20).
        /// </summary>
        public static WaitingRoomStep CheckWaitingRoom(bool connected, bool welcomed, bool saveReceived, bool guestPageBound)
        {
            if (!connected || !welcomed || saveReceived) return WaitingRoomStep.Nothing;
            return guestPageBound ? WaitingRoomStep.ShowRoom : WaitingRoomStep.LeaveForGame;
        }

        /// <summary>
        /// Whether a join that failed is reported by the join's own dialog. Not when a waiting room ended it and the guest's
        /// page said why; a room that ended before its page could open (its welcome and its end read in one frame) has said
        /// nothing, so the join reports it.
        /// </summary>
        public static bool ReportJoinError(bool waitingRoomEnded, bool pageShowedEnd) => !(waitingRoomEnded && pageShowedEnd);

        /// <summary>
        /// Whether "Connecting to …" (D21) is shown now. Only for a join still under way: not once its waiting room has
        /// welcomed it (its page shows instead), and not for one that has already ended (a Steam overlay closing after the
        /// join failed, or after the room's page opened, asked for it late).
        /// </summary>
        public static bool ShowConnectingBox(bool joining, bool welcomed) => joining && !welcomed;

        /// <summary>The start of the message a host sends a guest whose mod build differs (CompatibilityHandshake).</summary>
        public const string BuildMismatchMarker = "Multiplayer build mismatch";
        /// <summary>A full waiting room's refusal (TimberNet's LobbyRoom.FullMessage).</summary>
        public const string RoomFullMessage = "The waiting room is full.";

        /// <summary>
        /// A rejoin's quiet try failed with <paramref name="error"/>: whether the rejoin gives up and says why. Only when
        /// waiting can't help: another build of the mod, or a full room. Anything else (nobody listening yet, the host's
        /// game still running and refusing newcomers, a connection cut as the host rehosts) is waited out quietly.
        /// </summary>
        public static bool RejoinGivesUp(string error) =>
            !string.IsNullOrEmpty(error)
            && (error.IndexOf(BuildMismatchMarker, System.StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf(RoomFullMessage, System.StringComparison.Ordinal) >= 0);

        /// <summary>
        /// A Steam guest's rejoin, given what the host's lobby says: joined only once it is a waiting room of this build
        /// that is still letting players in. The host's old lobby (its game started, bb_open "0") and one Steam hasn't
        /// described yet are waited out.
        /// </summary>
        public static bool RejoinEntersLobby(FriendGameState state) => state == FriendGameState.WaitingRoom;
    }

    /// <summary>The main menu's band, grown to fit its panel with the mod's two buttons (ClientConnectionUI.FitMainMenu).</summary>
    public static class MainMenuFit
    {
        /// <summary>
        /// The band's height: the game's <paramref name="band"/> (720 px), or, when the panel and the logo above it need
        /// more with <paramref name="gap"/> above and below the panel, that much, but never taller than the screen
        /// (<paramref name="screen"/>) the band is centred on. Before the first layout (no sizes yet) the game's height.
        /// </summary>
        public static float Band(float panel, float logo, float screen, float band, float gap)
        {
            if (!(panel > 0) || !(logo > 0)) return band;
            float need = panel + logo + 2 * gap;
            if (screen > 0 && need > screen) need = screen;
            return need > band ? need : band;
        }
    }

    /// <summary>What the game menu's hosting button is (ClientConnectionUI).</summary>
    public enum HostButtonKind
    {
        /// <summary>Not shown: a guest's game, or a session a failed action stopped.</summary>
        Hidden,
        /// <summary>Host co-op game: a game played alone.</summary>
        HostCoopGame,
        /// <summary>Save and Rehost: the host of a co-op game, while its session lasts or after it ended.</summary>
        SaveAndRehost,
    }

    public static class HostButtonRules
    {
        /// <summary>
        /// The game menu's hosting button. Decided by how this game was loaded, not by whether its session is still live:
        /// a lost connection or a desync ends the session, and a guest's copy of the game then is not a game to host (it
        /// may be out of step), while the host still rehosts theirs. After a failed action nothing is rehosted.
        /// </summary>
        /// <param name="coopGame">The game was loaded into a co-op session (its ReplayService exists).</param>
        /// <param name="loadedAsHost">It was loaded as that session's host.</param>
        /// <param name="replayFailure">An action failed partway and stopped the session.</param>
        public static HostButtonKind Decide(bool coopGame, bool loadedAsHost, bool replayFailure)
        {
            if (!coopGame) return HostButtonKind.HostCoopGame;
            if (replayFailure || !loadedAsHost) return HostButtonKind.Hidden;
            return HostButtonKind.SaveAndRehost;
        }
    }
}
