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
    }
}
