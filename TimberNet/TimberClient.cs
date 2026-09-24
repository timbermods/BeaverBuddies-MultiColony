using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace TimberNet
{
    public class ConnectionFailureException : Exception
    {
        public ConnectionFailureException() : base("Client connection timed out") { }
        public ConnectionFailureException(string message) : base(message) { }
    }

    public class TimberClient : TimberNetBase
    {

        private readonly ISocketStream client;
        private int connectionFailed;

        public override bool ShouldTick => base.ShouldTick && receivedEvents.Count > 0;

        public TimberClient(ISocketStream client) : base()
        {
            this.client = client;
        }

        public override void DoUserInitiatedEvent(string json, string type, int tick)
        {
            // Don't actually do the event (i.e. add it to the hash)
            // Wait for the server to confirm w/ adjusted Tick
            SendBytes(client, MessageToBuffer(json), type, tick);
        }

        // Long enough for a relayed connection to be established, but bounded.
        private const int BackgroundConnectTimeoutMilliseconds = 45000;

        protected override void HandleConnectionFailure(ISocketStream stream, string message)
        {
            if (IsStopped || Interlocked.Exchange(ref connectionFailed, 1) != 0) return;
            // The host said it is moving its game to a waiting room: the connection ends as it does, and that is no error.
            // Queued before the close, so the game never sees the session over with nothing to say why.
            if (hostMoved)
            {
                QueueHostMoved();
                Close();
                return;
            }
            // Capture the reason first: Close() tears the stream down.
            message = DescribeFailure(stream, message);
            Close();
            QueueError(message);
        }

        // ---- The host's move to a waiting room (see MoveFrames) ----

        private volatile bool hostMoved;
        // Written before hostMoved, read after it.
        private ulong movedToLobby;

        /// <summary>The host said it is moving its game to a waiting room (read on the receive thread, before the connection's end).</summary>
        public bool HostMoved => hostMoved;

        /// <summary>The Steam lobby the host keeps for its room, as its notice said; null if it named none.</summary>
        public ulong? MovedToSteamLobby => hostMoved && movedToLobby != 0 ? movedToLobby : (ulong?)null;

        protected override void OnHostMoving(ulong? steamLobby)
        {
            movedToLobby = steamLobby ?? 0;
            hostMoved = true;
            Log("The host is moving this game to a waiting room" + (steamLobby.HasValue ? $" (Steam lobby {steamLobby})" : ""));
        }

        private ActivityChannel? activityChannel;
        private volatile bool saveArrived;

        protected override void OnMapFrameReceived(ISocketStream stream)
        {
            saveArrived = true;
            activityChannel = CreateActivityChannel(stream);
        }

        // ---- Waiting room (see LobbyFrames) ----

        /// <summary>What the host's waiting room has said, if the host opened one. Written on the receive thread.</summary>
        public LobbyInbox Lobby { get; } = new LobbyInbox();

        protected override void HandleLobbyFrame(ISocketStream source, string type, JObject frame)
        {
            // Only the host's frames mean anything here.
            if (LobbyFrames.IsGuestType(type)) return;
            Lobby.Receive(type, frame, RttTracker.NowMs);
        }

        /// <summary>Says who this guest is to the host's waiting room. False once the save has come, or if not connected.</summary>
        public bool SendLobbyHello(string id, string name) => SendLobbyFrame(LobbyFrames.Hello(id, name));

        /// <summary>Tells the host's waiting room this guest is ready, or no longer is.</summary>
        public bool SendLobbyReady(bool ready) => SendLobbyFrame(LobbyFrames.Ready(ready));

        /// <summary>Tells the host's mixed-factions waiting room which faction this guest picked.</summary>
        public bool SendLobbyFaction(string factionId) => SendLobbyFrame(LobbyFrames.Faction(factionId));

        private bool SendLobbyFrame(JObject frame)
        {
            if (IsStopped || saveArrived) return false;
            try
            {
                SendDataWithLength(client, MessageToBuffer(frame));
                return true;
            }
            catch (Exception e)
            {
                HandleConnectionFailure(client, "Could not reach the host's waiting room: " + e.Message);
                return false;
            }
        }

        private sealed class RosterSnapshot
        {
            public int You;
            public List<PeerStatus> Peers = new List<PeerStatus>();
            public double ReceivedAtMs;
        }

        private volatile RosterSnapshot? roster;
        private readonly double startedAtMs = RttTracker.NowMs;

        protected override void HandleStatusFrame(ISocketStream source, string type, JObject message)
        {
            if (type == StatusFrames.ProbeType && StatusFrames.TryParseSequence(message, out int sequence))
            {
                // Answered on the network thread, not the game thread, so the host measures the network
                // and not how busy this player's game happens to be.
                try { SendDataWithLength(client, MessageToBuffer(StatusFrames.Reply(sequence, TickCount, ReportedFps))); }
                catch (Exception) { /* a dead connection is reported by the reader */ }
            }
            else if (type == StatusFrames.RosterType && StatusFrames.TryParseRoster(message, out int you, out List<PeerStatus> peers))
            {
                roster = new RosterSnapshot { You = you, Peers = peers, ReceivedAtMs = RttTracker.NowMs };
            }
        }

        public override NetworkStatus GetNetworkStatus()
        {
            RosterSnapshot? latest = roster;
            double now = RttTracker.NowMs;
            double silence = Math.Max(0, now - (latest?.ReceivedAtMs ?? startedAtMs)) / 1000.0;
            return new NetworkStatus(false, IsStopped, latest?.You ?? -1, silence,
                (IReadOnlyList<PeerStatus>?)latest?.Peers ?? Array.Empty<PeerStatus>());
        }

        public override void SendActivity(PlayerActivity activity)
        {
            if (IsStopped) return;
            // The host replaces the id with the one it assigned to this connection.
            activityChannel?.Post(activity);
        }

        public override bool SendChat(string name, string color, string text)
        {
            // Like activity, chat waits until the map has arrived. The host numbers it and sends it back to
            // everyone, this guest included, so all players see the same order.
            if (IsStopped || activityChannel == null || !ChatMessage.TryCreate(name, color, text, out ChatMessage? message) || message == null)
                return false;
            activityChannel.PostOrdered(message.ToJson());
            return true;
        }

        protected override void HandleChat(ISocketStream source, IReadOnlyList<ChatMessage> messages, bool isHistory)
        {
            // Only the host numbers messages; an unnumbered one is not from the host.
            foreach (ChatMessage message in messages)
                if (message.Sequence > 0) Chat.Add(message);
        }

        protected override void ProcessReceivedEvent(JObject message)
        {
            base.ProcessReceivedEvent(message);
            if (ShouldLogDetails) Log($"Received event: {message[TYPE_KEY]?.ToString() ?? "<null>"}");
            AddEventToHash(message);
        }

        // How long a direct connection may take to be accepted.
        public const int ConnectTimeoutMilliseconds = 3000;

        public override void Start()
        {
            base.Start();
            // The connection is started here (a transport that can't even start throws to the caller) and waited for on
            // the network thread, never on the game thread: a direct join to an address where nothing answered held the
            // game's menu for up to 3 s (1.4.0-rc6). A failure is reported as any later one is (HandleConnectionFailure).
            Task connecting = client.ConnectAsync();
            // Then read from it until it closes, on a thread of its own (see StartNetworkThread).
            StartNetworkThread("BeaverBuddies receive from the host", () =>
            {
                try
                {
                    bool connected;
                    try { connected = connecting.Wait(ConnectTimeoutMilliseconds); }
                    catch (AggregateException error) { throw new ConnectionFailureException((error.InnerException ?? error).Message); }
                    if (!connected) throw new ConnectionFailureException();
                    // Transports that connect in the background finish before the handshake clock starts.
                    (client as IConnectionAwaitable)?.WaitForConnection(BackgroundConnectTimeoutMilliseconds);
                    if (CompatibilityIdentity != null) RunCompatibilityHandshake(client, false);
                    if (!IsStopped) StartListening(client, true);
                }
                catch (Exception error) { HandleConnectionFailure(client, error.Message); }
            });
        }


        public override void AbortSession(string reason)
        {
            try { SendSessionFault(client, reason); }
            finally { Close(); }
        }

        public override void Close()
        {
            base.Close();
            activityChannel?.Close();
            client.Close();
        }
    }
}
