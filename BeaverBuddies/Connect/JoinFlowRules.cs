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

    /// <summary>What an accepted Steam invite does (SteamOverlayConnectionService).</summary>
    public enum InviteStep
    {
        /// <summary>In the main menu: join the host's page now.</summary>
        Join,
        /// <summary>In a game played alone: ask, then save it and join from the main menu.</summary>
        OfferFromGame,
        /// <summary>In a co-op game: its session is not ended for an invite; the player leaves it first.</summary>
        LeaveCoopGameFirst,
        /// <summary>Hosting a Co-op Game page (or loading its game): closed first, not replaced by a join.</summary>
        StopHostingFirst,
    }

    public static class InviteRules
    {
        /// <summary>
        /// An accepted invite to an open Co-op Game page. A page is joined only from the main menu (D20); a game played
        /// alone is saved and left for it after asking (1.4.0-rc6); a co-op game or a page this player hosts is never
        /// ended or replaced by a join (the in-game join used to take over the running session's connection).
        /// </summary>
        public static InviteStep Decide(bool inMainMenu, bool inCoopSession, bool hostingPage)
        {
            if (hostingPage) return InviteStep.StopHostingFirst;
            if (inMainMenu) return InviteStep.Join;
            return inCoopSession ? InviteStep.LeaveCoopGameFirst : InviteStep.OfferFromGame;
        }
    }

    /// <summary>
    /// The Load game box made wide enough for Host co-op game beside its Load (1.4.0-rc7). The game's numbers (UI.zip,
    /// read again by RuntimeChecks): <c>.load-box</c> is 800 px wide (OptionsStyle), its <c>.box__content-container</c> has
    /// 45 px of padding each side (CoreStyle), which leaves 710 px, and a <c>.menu-button--medium</c> is at least 184 px
    /// (CoreStyle): four are 736 px in a centred row (<c>.box-buttons</c>). The box grows; the game's buttons stay its size.
    /// </summary>
    public static class LoadBoxFit
    {
        public const float Box = 800;
        public const float Padding = 45;
        public const float ButtonMinWidth = 184;
        public const float Extra = 70;
        public static float WidenedBox => Box + Extra;

        /// <summary>The room inside a box <paramref name="width"/> wide, for its row of buttons.</summary>
        public static float Inside(float width) => width - 2 * Padding;

        /// <summary>Whether <paramref name="buttons"/> medium buttons fit side by side in a box <paramref name="width"/> wide.</summary>
        public static bool Fits(int buttons, float width) => buttons * ButtonMinWidth <= Inside(width);
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

        /// <summary>
        /// The Load game box's Host co-op game (1.4.0-rc7): wherever a Co-op Game room can be opened
        /// (<paramref name="roomPanelBound"/>: the room's panel is bound in this scene), and in a game as the game menu's
        /// hosting button is shown (<see cref="Decide"/>): never for a guest's game, nor after a failed action. It hosts the
        /// selected save, whatever the game menu's button is called.
        /// </summary>
        public static bool ShowOnLoadBox(bool roomPanelBound, HostButtonKind kind) => roomPanelBound && kind != HostButtonKind.Hidden;
    }
}
