using Newtonsoft.Json.Linq;
using System;
using TimberNet;
using System.Threading.Tasks;
using BeaverBuddies.Connect;
using BeaverBuddies.Events;
using BeaverBuddies.Steam;
using System.Net.Sockets;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.IO
{
    // With TimberBorn's current architecture, we cannot feasibly
    // support joining after the game has started. This is because a number
    // of variables are randomly initialized during load (e.g. tree lifespans)
    // rather than serialized, since they aren't that important. As a result, the
    // save won't create the same gamestate as a currently loaded game. The only
    // way to ensure that the gamestate is the same is to have all clients join
    // at load time.
    // Barring a complete overhaul of how loading works, I should probably disable
    // joining after the play button is first pressed.
    public class ServerEventIO : NetIOBase<TimberServer>
    {
        // Anything that happens on the server should be recorded and
        // sent to the clients.
        public override bool RecordReplayedEvents => true;

        // Servers need to send heartbeats so clients know to progress.
        public override bool ShouldSendHeartbeat => true;

        // The server should wait until the next update to play a
        // user-initiated event, to make sure that the events
        // happen in the same order for the server and clients.
        public override UserEventBehavior UserEventBehavior => UserEventBehavior.QueuePlay;

        public ISocketListener SocketListener { get; private set; }

        /// <summary>
        /// What Steam friends see of this game in their Join co-op game box (SteamListener.SetDetails): a one-line
        /// description, set by whoever starts hosting before it does.
        /// </summary>
        public string SteamDescription { get; set; }

        // We only support a static map; see note above
        public void Start(byte[] mapBytes)
        {
            StartServer(() =>
            {
                // TODO: Probably don't need to hold it in memory after the first tick...
                Task<byte[]> task = new Task<byte[]>(() => mapBytes);
                task.Start();
                return task;
            }, null);
        }

        /// <summary>
        /// Hosts a new game's waiting room (BeaverBuddies.Lobby): the same listeners as hosting a save, but guests wait in
        /// <paramref name="room"/> until the host has made and saved the world (TimberServer.ReleaseLobby). Not installed
        /// as EventIO until that save is loaded (LobbySession), so the scene that makes the world is a single-player one.
        /// </summary>
        public void StartLobby(LobbyRoom room)
        {
            StartServer(() => Task.FromException<byte[]>(new InvalidOperationException("A waiting room sends the saved world it is given.")), room);
        }

        /// <summary>
        /// The host pressed Start in the waiting room: nobody new comes in, for good, and the game that follows never
        /// waits for joiners (no "Joining: open", no "Start the game?", founding at once). Those in the room still come in
        /// (TimberServer lets them through <c>StartQueuing</c>), so the server's own error message is not set.
        /// </summary>
        public void CloseLobby(string message)
        {
            if (stoppedAccepting) return;
            stoppedAccepting = true;
            Plugin.Log("[Lobby] Closed to newcomers: the host started the game");
            NetBase?.CloseLobbyToNewcomers(message);
            try
            {
                (SocketListener as MultiSocketListener)?.GetListener<SteamListener>()?.CloseToNewGuests();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not close the Steam lobby to new guests: " + error.Message);
            }
        }

        /// <summary>The Steam listener, when this host is reachable over Steam (for Invite Friends).</summary>
        public SteamListener SteamListener => (SocketListener as MultiSocketListener)?.GetListener<SteamListener>();

        private bool moving;

        /// <summary>The host has told this session's guests it is moving the game to a waiting room (<see cref="MoveToRoom"/>).</summary>
        public bool IsMoving => moving;

        /// <summary>
        /// The host moves this running game's players to a waiting room (1.4.0-rc7, K1): every guest is told (MoveFrames,
        /// naming the Steam lobby the room will keep), and the word is handed to Steam at once. The caller then ends the
        /// session, handing the lobby to the room (<see cref="HandLobbyToRoom"/>), and opens the room; a guest follows it
        /// by itself. Once per session.
        /// </summary>
        public void MoveToRoom()
        {
            if (moving || NetBase == null || NetBase.IsStopped) return;
            moving = true;
            SteamListener steam = SteamListener;
            ulong? lobby = steam != null && steam.LobbyID.IsValid() ? steam.LobbyID.m_SteamID : (ulong?)null;
            int told = NetBase.SendMoveNotice(lobby);
            // A Steam guest's copy waits for the next pump otherwise, and its connection closes before then (it lingers to
            // drain what it has, so it is sent).
            SteamNet.PumpBetweenTicks(force: true);
            Plugin.Log($"[Lobby] Moving to a waiting room: told {told} guest(s){(lobby.HasValue ? $" (Steam lobby {lobby})" : "")}");
        }

        /// <summary>
        /// Just before this moving session's server closes for the room that opens next: its Steam lobby goes to the room's
        /// listener instead of being left (the guests are still members). Only then, so a move that fails before its room
        /// opens leaves the lobby as any end does.
        /// </summary>
        public void HandLobbyToRoom()
        {
            if (!moving) return;
            SteamListener steam = SteamListener;
            if (steam == null) return;
            steam.KeepLobbyForNextServer();
            Plugin.Log($"[Lobby] Steam lobby {steam.LobbyID} kept for the room");
        }

        private void StartServer(Func<Task<byte[]>> mapProvider, LobbyRoom room)
        {
            // Seats for separate colonies are fixed for the whole session, like the other host choices.
            BeaverBuddies.Colonies.ColonySession.BeginHostSession();
            // A new session's boost is 0 (ReplayService's constructor says so too, but only once the host's game loads: a
            // guest whose start message is built before that, as every waiting-room guest's is, got the last session's).
            ReplayService.ResetSessionBoost();
            try
            {
                List<ISocketListener> listeners = [
                    new TCPListenerWrapper(Settings.Port)
                ];
                if (SteamOverlayConnectionService.IsSteamEnabled && Settings.EnableSteam)
                {
                    try { listeners.Add(new SteamListener()); }
                    catch (Exception e)
                    {
                        // Steam is an optional extra; never let it prevent hosting over direct IP.
                        Plugin.LogError("Steam invites are unavailable this session (direct IP still works): " + e.Message);
                    }
                }
                foreach (SteamListener steam in listeners.OfType<SteamListener>()) steam.SetDetails(room != null, SteamDescription);
                // A lobby kept for a room is this server's Steam listener's to reopen; without one, nobody takes it.
                if (!listeners.OfType<SteamListener>().Any()) BeaverBuddies.Steam.SteamListener.LeaveHandedOverLobby();
                SocketListener = new MultiSocketListener(listeners.ToArray());
                NetBase = new TimberServer(SocketListener, mapProvider, CreateInitEvent());
                if (room != null) NetBase.OpenLobby(room);
            }
            catch (Exception e)
            {
                Plugin.Log("Failed to start server");
                Plugin.Log(e.ToString());
                return;
            }
            //netBase = new TimberServer(port, mapProvider, null);
            NetBase.CompatibilityIdentity = BuildCompatibility.CreateIdentity();
            // Mod lists are swapped with each joining player and compared; a difference is only a warning.
            ModWarnings.Clear();
            NetBase.CompatibilityAdvisory = ModCompatibility.CreateAdvisory();
            NetBase.OnPeerAdvisory += ModCompatibility.OnPeerAdvisory;
            NetBase.DetailedLoggingEnabled = () => Settings.Debug && Settings.VerboseLogging;
            NetBase.OnSessionFault += reason => SingletonManager.GetSingleton<ReplayService>()?.AbortReplay(reason);
            NetBase.OnLog += Plugin.Log;
            NetBase.OnMapReceived += NetBase_OnClientConnected;
            NetBase.Start();
        }

        private Func<JObject> CreateInitEvent()
        {
            // It should be ok to send an init event even if the client is joining before
            // the server, since a) it won't do much on the Host (just set the random seed)
            // and b) the client will overwrite these values later whent he Host finished
            // loading the map.
            return () =>
            {
                var message = InitializeClientEvent.Create(this);
                message.ticksSinceLoad = 0;
                Plugin.Log($"Sending start state: {JsonSettings.Serialize(message)}");
                return JObject.Parse(JsonSettings.Serialize(message));
            };
        }

        // The tags (requestId) of guest actions in frames that could not be read, in the order they arrived.
        private readonly List<string> unreadableRequestIds = new List<string>();

        // A guest sends a tick's actions as one group: one action this game cannot read (from a mod only that guest has)
        // loses only itself, not the tick's other actions. What the host keeps is played and sent on like any group.
        protected override bool KeepsReadableActions => true;

        // A guest's action only happens once the host has read it, played it and sent it back, so one the host cannot
        // read is lost for every player alike and nobody goes out of step. Ending the session for it would let any
        // guest end it. The guest is told its actions were refused (TakeUnreadableRequestIds), so it clears its marks
        // and does not wait for an answer that never comes.
        protected override bool HandleUnreadableFrame(JObject frame, string problem)
        {
            // The host numbered the frame by the connection it came from (TimberServer.StampReceivedEvent).
            string guest = frame[TimberNetBase.PLAYER_KEY] is JValue { Type: JTokenType.Integer } player ? $"player {player}" : "a guest";
            Plugin.LogWarning($"Ignored an action from {guest} that could not be read: {problem}");
            try
            {
                unreadableRequestIds.AddRange(RequestIdsIn(frame));
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not read which actions that guest sent: " + error.Message);
            }
            return true;
        }

        // The frame's own tag and those of the actions grouped in it, one level down, where TimberNetBase.StampPlayer
        // stamps the sender. Only text counts: whatever else is there was written by the guest.
        private static IEnumerable<string> RequestIdsIn(JObject frame)
        {
            if (frame[nameof(ReplayEvent.requestId)] is JValue { Type: JTokenType.String } own) yield return (string)own;
            JToken events = frame[nameof(GroupedEvent.events)];
            JArray children = events as JArray ?? (events as JObject)?["$values"] as JArray;
            if (children == null) yield break;
            foreach (JToken child in children)
            {
                if (child is JObject action && action[nameof(ReplayEvent.requestId)] is JValue { Type: JTokenType.String } tag)
                    yield return (string)tag;
            }
        }

        /// <summary>
        /// Host: the tags of guest actions that arrived in frames this game could not read, since this was last called.
        /// ReplayService refuses each, as it refuses an action the colony rules turn down (ActionRefusedEvent).
        /// </summary>
        public List<string> TakeUnreadableRequestIds()
        {
            List<string> taken = new List<string>(unreadableRequestIds);
            unreadableRequestIds.Clear();
            return taken;
        }

        private bool stoppedAccepting;

        /// <summary>Players can still join: the first tick has not run and nothing has changed the game yet.</summary>
        public bool IsAcceptingClients => !stoppedAccepting && (NetBase?.IsAcceptingClients ?? false);

        /// <summary>
        /// No more players from now on: the first tick has run, or (<paramref name="gameChanged"/>) an action that
        /// changed the game was played before it. Either way a player joining later would be missing something.
        /// </summary>
        public void StopAcceptingClients(bool gameChanged = false)
        {
            if (stoppedAccepting) return;
            stoppedAccepting = true;
            Plugin.Log(gameChanged ? "The game was changed before the first tick: no longer accepting clients" : "Game started: no longer accepting clients");
            string message = gameChanged
                ? "The host has already changed the game (placed, marked or founded something), so it can't be joined now. " +
                  "Ask the host to save and rehost, then join before they change anything."
                : "The host has already started the game, so it can't be joined now. " +
                  "Ask the host to rehost, then join before they unpause.";
            // Called from inside a replay, so a server that never started must not throw here.
            NetBase?.StopAcceptingClients(message);
            // Tell Steam friends too, so an old invite explains itself instead of hanging. This can run inside a
            // replay, where a throw would end the session. The server above already refuses guests, so the lobby
            // is only a courtesy and a Steam failure is logged instead.
            try
            {
                (SocketListener as MultiSocketListener)?.GetListener<SteamListener>()?.CloseToNewGuests();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not close the Steam lobby to new guests: " + error.Message);
            }
            // TODO: remove map from memory
        }

        private void NetBase_OnClientConnected(byte[] mapBytes)
        {

        }
    }
}
