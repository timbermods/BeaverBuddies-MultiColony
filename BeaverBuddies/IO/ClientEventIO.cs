using System;
using BeaverBuddies.Connect;
using Newtonsoft.Json.Linq;
using TimberNet;
using static TimberNet.TimberNetBase;

namespace BeaverBuddies.IO
{
    public class ClientEventIO : NetIOBase<TimberClient>
    {
        // If the client receives an event to replay, no matter where it
        // originated, it shouldn't send it *back* to the server, since the
        // server is what sent the event.
        public override bool RecordReplayedEvents => false;

        // Clients don't need to send heartbeats
        public override bool ShouldSendHeartbeat => false;

        // The client doesn't get to do anything from the user directly.
        // The client should send user-initiated events to the server.
        // It has to wait until an event is received from the server.
        public override UserEventBehavior UserEventBehavior => UserEventBehavior.Send;

        private MapReceived mapReceivedCallback;
        private bool FailedToConnect = false;
        // True once the host's save arrived and the game was told to load it. Before that this is only a join
        // attempt; after it, a game exists that belongs to this session.
        private bool mapDelivered = false;

        /// <summary>
        /// A join made in a game, held apart from it (not the game's EventIO) until its save arrives (1.4.0-rc7,
        /// ClientConnectionService.LoadMap): the game played alone stays a single-player game while the guest waits in the
        /// host's room. Its errors are still reported as a join's (ConnectionErrorPlanner), and nothing it hears reaches
        /// the game in this scene. Set and cleared on the game's thread.
        /// </summary>
        public bool HeldJoin { get; set; }

        private ClientEventIO(ISocketStream socket, MapReceived mapReceivedCallback,
            Action<string, TimberClient> onError)
        {
            this.mapReceivedCallback = mapReceivedCallback;

            NetBase = new TimberClient(socket)
            {
                CompatibilityIdentity = BuildCompatibility.CreateIdentity(),
                // Mod lists are swapped with the host and compared; a difference is only a warning.
                CompatibilityAdvisory = ModCompatibility.CreateAdvisory(),
            };
            NetBase.DetailedLoggingEnabled = () => Settings.Debug && Settings.VerboseLogging;
            // A held join has no game yet: the game in this scene is not its to stop.
            NetBase.OnSessionFault += reason =>
            {
                if (!HeldJoin) SingletonManager.GetSingleton<ReplayService>()?.AbortReplay(reason, leaveQuietly: LeftOverUnreadableAction);
            };
            NetBase.OnMapReceived += OnMapReceivedByNet;
            ModWarnings.Clear();
            NetBase.OnPeerAdvisory += ModCompatibility.OnPeerAdvisory;
            NetBase.OnLog += Plugin.Log;
            NetBase.OnError += (error) => OnConnectionError(error, onError);
            NetBase.OnHostMoved += OnHostMoved;
            try
            {
                NetBase.Start();
            }
            catch (Exception ex)
            {
                onError(ex.Message, null);
                Plugin.LogError(ex.ToString());
                CleanUp();
                FailedToConnect = true;
            }
        }

        /// <summary>
        /// This guest stopped because it could not read an action from the host. Nothing of it was played here, and the
        /// host's game is whole: this guest leaves, as if it had quit, and the host and the other players play on.
        /// </summary>
        public bool LeftOverUnreadableAction { get; private set; }

        // Everything a guest plays comes from the host, which has already played it, so an action this game cannot
        // read leaves it behind the host's for good. This game stops, and nothing more of that tick is played; the
        // others are not stopped (ReplayService.AbortReplay with leaveQuietly), since nothing went wrong for them.
        protected override bool HandleUnreadableFrame(JObject frame, string problem)
        {
            Plugin.LogError("Could not read an action from the host: " + problem);
            LeftOverUnreadableAction = true;
            NetBase?.RaiseSessionFault("An action from the host could not be read, so this game would no longer " +
                "match the host's. " + problem);
            return false;
        }

        // Only once the game has been told to load the save: if that throws, this stays a join attempt that failed,
        // and the error that follows is reported to whoever is joining.
        private void OnMapReceivedByNet(byte[] mapBytes)
        {
            mapReceivedCallback(mapBytes);
            mapDelivered = true;
        }

        /// <summary>
        /// The connection failed or dropped. What that means depends on how far the join got (see
        /// ConnectionErrorPlanner); this is reached on the update thread, from wherever the session is being updated.
        /// </summary>
        private void OnConnectionError(string error, Action<string, TimberClient> onError)
        {
            Plugin.LogError(error);
            // One failure is one report: a second error queued behind the first (a save that then failed to load, say)
            // must not show a second dialog.
            if (FailedToConnect) return;
            // Kept for the report: a host's waiting room that ended (closed, this guest removed, the start failed) says why
            // itself, on its page, and the join says nothing more (ClientConnectionService).
            TimberClient net = NetBase;
            CleanUp();
            FailedToConnect = true;

            bool isCurrent = ReferenceEquals(EventIO.Get(), this);
            switch (ConnectionErrorPlanner.Decide(isCurrent, HeldJoin, mapDelivered, ReplayService.HasReplayFailure))
            {
                case ConnectionErrorPlan.ReportWhileJoining:
                    // Nothing came of this join, so nothing should stay installed: a session that is over but still
                    // installed turns the next game loaded from this menu into one that is paused for good. (A held
                    // join never was, and is closed above.)
                    EventIO.ResetIf(this);
                    HeldJoin = false;
                    onError(error, net);
                    break;
                case ConnectionErrorPlan.EndRunningGame:
                    // The game's own dialog: the join attempt's belongs to a menu that no longer exists. With no
                    // game yet (the save is still loading) the new game finds the session over and reports it itself,
                    // see ReplayService.UpdateSingleton.
                    SingletonManager.GetSingleton<ReplayService>()?.EndSession(SessionEndMessages.ConnectionLost(error), offerRejoin: true);
                    break;
            }
        }

        /// <summary>
        /// The host said it is moving this game to a waiting room (TimberNet's MoveFrames), and the connection has ended as
        /// it closed (1.4.0-rc7, K1). No error: the game's session ends quietly (no connection lost, no Rejoin box) and this
        /// player follows the host into its room at once, in this game, its window opening when the room welcomes it
        /// (ClientConnectionService.FollowHost). Reached on the update thread, from wherever the session is updated.
        /// </summary>
        private void OnHostMoved()
        {
            if (FailedToConnect) return;
            ulong? keptLobby = NetBase?.MovedToSteamLobby;
            bool isCurrent = ReferenceEquals(EventIO.Get(), this);
            bool held = HeldJoin;
            CleanUp();
            FailedToConnect = true;
            HeldJoin = false;
            // A session something newer replaced has nobody to follow for.
            if (!isCurrent && !held) return;
            Plugin.Log("[Lobby] The host is moving this game to a waiting room; following it");
            // Quietly (no message): the game stays, played alone, until the room's Start. (A join whose save had not been
            // loaded yet, the moment after its host's Start, has no game of the host's here: it follows all the same.)
            if (isCurrent && mapDelivered) SingletonManager.GetSingleton<ReplayService>()?.EndSession(null);
            EventIO.ResetIf(this);
            ClientConnectionService.FollowHost(keptLobby);
        }

        private void CleanUp()
        {
            if (NetBase == null) return;
            NetBase.Close();
            NetBase.OnMapReceived -= OnMapReceivedByNet;
            NetBase.OnHostMoved -= OnHostMoved;
            NetBase.OnLog -= Plugin.Log;
            NetBase.OnPeerAdvisory -= ModCompatibility.OnPeerAdvisory;
            NetBase = null;
        }

        /// <param name="onError">A join that failed before its save loaded: the error, and the connection (null if it never started).</param>
        public static ClientEventIO Create(ISocketStream socket, MapReceived mapReceivedCallback, Action<string, TimberClient> onError)
        {
            ClientEventIO eventIO = new ClientEventIO(socket, mapReceivedCallback, onError);
            if (eventIO.FailedToConnect) return null;
            return eventIO;
        }
    }
}
