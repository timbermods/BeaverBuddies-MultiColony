using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace TimberNet
{ 

    public class TimberServer : TimberNetBase
    {

        private readonly List<ISocketStream> clients = new List<ISocketStream>();
        // An event as it goes on the wire, once for every guest: compressed, with its type and tick for the log.
        private readonly struct Outgoing
        {
            public readonly byte[] Wire;
            public readonly string Type;
            public readonly int Tick;
            public Outgoing(byte[] wire, string type, int tick) { Wire = wire; Type = type; Tick = tick; }
        }
        private readonly ConcurrentDictionary<ISocketStream, ConcurrentQueue<Outgoing>> queuedMessages =
            new ConcurrentDictionary<ISocketStream, ConcurrentQueue<Outgoing>>();

        // Guests whose connection can block (IBlockingWrites: a direct link) each get an ordered send lane of their own as
        // their join finishes (FinishQueuing), and the host's game thread only queues for them. A guest that stopped
        // reading used to stop the host in the middle of its tick's broadcast, and with it every other guest, until its
        // connection gave up (up to about 20 s). Guests over Steam, whose writes only queue, are written to directly.
        private readonly ConcurrentDictionary<ISocketStream, SendLane> sendLanes = new ConcurrentDictionary<ISocketStream, SendLane>();

        /// <summary>How long a guest's connection may take to accept one frame before the guest is dropped (as over Steam).</summary>
        public static int SendStallLimitMs = 30000;

        /// <summary>How much may wait for one guest before it is dropped.</summary>
        public static long MaxQueuedBytesPerGuest = 16L * 1024 * 1024;

        /// <summary>How long ending the session waits for the guests' lanes to deliver the reason.</summary>
        public static int AbortFlushMs = 2000;

        // Player activity is presentation-only and deliberately kept out of queuedMessages' lock,
        // so a slow gameplay send can never stall a guest's receive thread.
        private readonly ConcurrentDictionary<ISocketStream, int> playerIds = new ConcurrentDictionary<ISocketStream, int>();
        private readonly ConcurrentDictionary<ISocketStream, ActivityChannel> activityChannels =
            new ConcurrentDictionary<ISocketStream, ActivityChannel>();
        private int lastPlayerId;
        // Player number -> the identity its connection proved, for the whole session (numbers are never reused).
        private readonly ConcurrentDictionary<int, string> verifiedIds = new ConcurrentDictionary<int, string>();

        // Chat: the host numbers every message and keeps the whole conversation, so a guest who joins late can be
        // sent all of it. chatGate makes "publish a message" and "send the history to a new guest" one step each, so
        // a message is either in the history a joining guest gets or queued behind it, never both and never neither.
        private readonly object chatGate = new object();
        private readonly ConcurrentDictionary<ISocketStream, ChatRateLimiter> chatLimits =
            new ConcurrentDictionary<ISocketStream, ChatRateLimiter>();
        private int lastChatSequence;

        // How often the host pings each guest and publishes the roster. Adjustable so tests need not wait.
        public static int StatusIntervalMs = 1000;
        private readonly ConcurrentDictionary<ISocketStream, RttTracker> trackers = new ConcurrentDictionary<ISocketStream, RttTracker>();
        // How many ticks behind the host each guest was at its last reply. Written on network threads.
        private readonly ConcurrentDictionary<ISocketStream, int> guestTicksBehind = new ConcurrentDictionary<ISocketStream, int>();
        // The frames per second each guest last reported. Removed again when a reply carries none.
        private readonly ConcurrentDictionary<ISocketStream, int> guestFps = new ConcurrentDictionary<ISocketStream, int>();
        private double nextStatusAtMs;

        private readonly ISocketListener listener;

        private Func<Task<byte[]>> mapProvider;
        private Func<JObject>? initEventProvider;

        public int ClientCount { get { lock (queuedMessages) return clients.Count; } }

        // Set on the game thread, read on the accept threads.
        private volatile string? errorMessage = null;
        public bool IsAcceptingClients => errorMessage == null;

        public List<string?> GetConnectedClients()
        {
            lock (queuedMessages) return clients.Select(c => c.Name).ToList();
        }

        /// <summary>
        /// Who a guest is as its connection proved it (<see cref="IVerifiedIdentity"/>: a Steam connection's Steam ID),
        /// or null for a connection that proves nothing (direct TCP), the host itself (0) or no guest at all. Kept after
        /// the guest leaves, so an event it sent just before is still judged by who it was. Any thread.
        /// </summary>
        public string? VerifiedIdOf(int player) => verifiedIds.TryGetValue(player, out string? id) ? id : null;

        /// <summary>The player numbers of the guests still connected, as the host numbered them.</summary>
        public List<int> ConnectedPlayerIds
        {
            get
            {
                lock (queuedMessages)
                    return clients.Where(c => c.Connected).Select(c => playerIds.TryGetValue(c, out int id) ? id : -1).Where(id => id >= 0).ToList();
            }
        }

        public TimberServer(ISocketListener listener, Func<Task<byte[]>> mapProvider, Func<JObject>? initEventProvider)
        {
            this.listener = listener;
            this.mapProvider = mapProvider;
            this.initEventProvider = initEventProvider;
        }

        public void UpdateProviders(Func<Task<byte[]>> mapProvider, Func<JObject>? initEventProvider)
        {
            this.mapProvider = mapProvider;
            this.initEventProvider = initEventProvider;
        }

        protected override void StampReceivedEvent(ISocketStream source, JObject message)
        {
            // Same source of truth as chat and activity: the host numbered this connection when it joined. A
            // connection without a number is stamped -1, which controls no colony.
            StampPlayer(message, playerIds.TryGetValue(source, out int id) ? id : -1);
        }

        protected override void ReceiveEvent(JObject message)
        {
            message[TICKS_KEY] = TickCount;
            base.ReceiveEvent(message);
        }

        // A guest's event is numbered with the tick the host is in when it arrives, and read at the start of the next
        // tick: one tick late is how every guest event is read, not a sign of trouble.
        protected override int ExpectedLateness => 1;

        public override void Start()
        {
            base.Start();

            listener.Start();
            Log("Server started listening");
            
            Task.Run(() =>
            {
                // TODO: I have a suspicion that this while plus the catch/continue below
                // is responsible for the server hanging sometimes on a connection that's dropped.
                // Logging now to see if I can catch it.
                while (!IsStopped)
                {
                    ISocketStream client;
                    try
                    {
                        Log("Accepting client...");
                        client = listener.AcceptClient();
                    } catch (Exception e)
                    {
                        Log("Error accepting client.");
                        Log(e.StackTrace);
                        continue;
                    }
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (!IsAcceptingClients)
                            {
                                SendErrorMessage(client);
                                client.Close();
                                return;
                            }

                            if (CompatibilityIdentity != null) RunCompatibilityHandshake(client, true);
                            if (IsStopped) { client.Close(); return; }
                            // Joining closed while the build check ran: say why, where the guest expects the save.
                            if (!IsAcceptingClients)
                            {
                                SendErrorMessage(client);
                                client.Close();
                                return;
                            }
                            await SendMap(client);
                            SendState(client);
                            if (initEventProvider != null)
                            {
                                JObject initEvent = initEventProvider();
                                // Written to this guest at once, before what was queued for it while its save went
                                // out, so it arrives first. The other guests get it the usual way: queued for one still
                                // receiving its save, written to the rest (see SendEventToClients).
                                DoUserInitiatedEvent(initEvent, sendNowTo: client);
                            }
                            FinishQueuing(client);

                            // Reads until the guest disconnects, on a thread of its own (see StartNetworkThread).
                            StartNetworkThread("BeaverBuddies receive from a guest", () => StartListening(client, false));
                        }
                        catch (Exception error) { HandleConnectionFailure(client, "Connection rejected: " + error.Message); }
                    });
                }
            });
        }

        public void StopAcceptingClients(string errorMessage)
        {
            this.errorMessage = errorMessage;
        }

        private void StartQueuing (ISocketStream client)
        {
            lock (queuedMessages)
            {
                if (IsStopped) { client.Close(); throw new IOException("Session closed while joining."); }
                // Checked again here, under the lock every broadcast takes: joining closed while this client's
                // handshake ran (the host acted, see ReplayService), and it would miss what was just played. Once
                // closed it never reopens.
                if (IsAcceptingClients)
                {
                    queuedMessages.TryAdd(client, new ConcurrentQueue<Outgoing>());
                    clients.Add(client);
                    // The host is player 0; the host, not the guest, chooses each guest's id.
                    int player = Interlocked.Increment(ref lastPlayerId);
                    playerIds[client] = player;
                    string? verified = (client as IVerifiedIdentity)?.VerifiedPlayerId;
                    if (!string.IsNullOrEmpty(verified)) verifiedIds[player] = verified!;
                    trackers[client] = new RttTracker(RttTracker.NowMs);
                    return;
                }
            }
            // Refused outside the lock, so a slow guest cannot hold up what the host sends everyone else.
            SendErrorMessage(client);
            client.Close();
            throw new IOException("Joining closed while the map was being prepared.");
        }

        private void RemoveActivity(ISocketStream client)
        {
            if (sendLanes.TryRemove(client, out SendLane? lane)) lane.Close();
            playerIds.TryRemove(client, out _);
            trackers.TryRemove(client, out _);
            guestTicksBehind.TryRemove(client, out _);
            guestFps.TryRemove(client, out _);
            chatLimits.TryRemove(client, out _);
            if (activityChannels.TryRemove(client, out ActivityChannel? channel)) channel.Close();
        }

        protected override void HandleStatusFrame(ISocketStream source, string type, JObject message)
        {
            // Only a guest's reply to our probe means anything to the host.
            if (type != StatusFrames.ReplyType || !StatusFrames.TryParseReply(message, out int sequence, out int? tick, out int? fps)) return;
            if (!trackers.TryGetValue(source, out RttTracker? tracker)) return;
            tracker.OnReply(sequence, RttTracker.NowMs);
            // The reply left the guest about half a round trip ago, which is a fraction of a tick.
            // A guest that has not ticked yet is loading, not behind. Without this a rehost compared the old
            // session's tick count with the joining guest's zero and made the host wait for it (1.0.6).
            if (tick != null && tick.Value > 0) guestTicksBehind[source] = Math.Max(0, TickCount - tick.Value);
            // A guest leaves the frame rate out while its window is in the background, so an old figure must go.
            if (fps != null) guestFps[source] = fps.Value; else guestFps.TryRemove(source, out _);
        }

        /// <summary>
        /// The lowest frame rate any connected guest last reported, or null if none has reported one. Used to ease
        /// the host's speed when the host has chosen a frame rate floor.
        /// </summary>
        public int? WorstGuestFps
        {
            get
            {
                int? worst = null;
                foreach (var pair in guestFps)
                {
                    if (!pair.Key.Connected) continue;
                    if (worst == null || pair.Value < worst.Value) worst = pair.Value;
                }
                return worst;
            }
        }

        /// <summary>
        /// The largest number of ticks any connected guest was behind the host at its last reply, or null if no
        /// guest has reported one. Used to ease the host's speed when a guest cannot keep up.
        /// </summary>
        public int? WorstGuestTicksBehind
        {
            get
            {
                int? worst = null;
                foreach (var pair in guestTicksBehind)
                {
                    if (!pair.Key.Connected) continue;
                    if (worst == null || pair.Value > worst.Value) worst = pair.Value;
                }
                return worst;
            }
        }

        protected override void OnUpdate()
        {
            double now = RttTracker.NowMs;
            if (now < nextStatusAtMs) return;
            nextStatusAtMs = now + StatusIntervalMs;
            // Only guests that have finished joining have a channel, so nothing is sent mid-transfer.
            foreach (var pair in activityChannels)
            {
                ISocketStream stream = pair.Key;
                if (!stream.Connected || !trackers.TryGetValue(stream, out RttTracker? tracker)) continue;
                if (!playerIds.TryGetValue(stream, out int id)) continue;
                // Built at write time, so the probe's timestamp is when it really leaves.
                pair.Value.PostFrame("probe", () => StatusFrames.Probe(tracker.BeginProbe(RttTracker.NowMs)));
                pair.Value.PostFrame("roster", () => StatusFrames.Roster(id, BuildPeerStatuses()));
            }
        }

        private List<PeerStatus> BuildPeerStatuses()
        {
            double now = RttTracker.NowMs;
            var peers = new List<PeerStatus>();
            foreach (var pair in activityChannels)
            {
                ISocketStream stream = pair.Key;
                if (!stream.Connected || !playerIds.TryGetValue(stream, out int id) || !trackers.TryGetValue(stream, out RttTracker? tracker)) continue;
                PeerStatus peer = tracker.Snapshot(id, (stream as ITransportInfo)?.TransportName ?? "", now);
                if (guestTicksBehind.TryGetValue(stream, out int behind)) peer = peer.WithTicksBehind(behind);
                if (guestFps.TryGetValue(stream, out int fps)) peer = peer.WithFps(fps);
                peers.Add(peer);
            }
            peers.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            return peers;
        }

        public override NetworkStatus GetNetworkStatus() =>
            new NetworkStatus(true, IsStopped, 0, null, BuildPeerStatuses());

        protected override void HandleActivity(ISocketStream source, PlayerActivity activity)
        {
            // Frames from a connection that has not been admitted are ignored.
            if (!playerIds.TryGetValue(source, out int id)) return;
            PlayerActivity assigned = activity.WithPlayerId(id);
            base.HandleActivity(source, assigned);
            RelayActivity(assigned, source);
        }

        public override void SendActivity(PlayerActivity activity)
        {
            if (IsStopped) return;
            RelayActivity(activity.WithPlayerId(0), null);
        }

        private void RelayActivity(PlayerActivity activity, ISocketStream? except)
        {
            foreach (var pair in activityChannels)
            {
                if (ReferenceEquals(pair.Key, except)) continue;
                if (!pair.Key.Connected) { RemoveActivity(pair.Key); continue; }
                pair.Value.Post(activity);
            }
        }

        protected override void HandleChat(ISocketStream source, IReadOnlyList<ChatMessage> messages, bool isHistory)
        {
            // History only flows from the host to a guest, and a guest sends one message at a time.
            if (isHistory || messages.Count != 1) return;
            // Frames from a connection that has not been admitted are ignored.
            if (!playerIds.TryGetValue(source, out int id)) return;
            // A guest that talks too fast is dropped, not disconnected: chat is optional.
            if (!chatLimits.GetOrAdd(source, _ => new ChatRateLimiter()).TryTake(RttTracker.NowMs)) return;
            Publish(messages[0], id);
        }

        public override bool SendChat(string name, string color, string text)
        {
            if (IsStopped || !ChatMessage.TryCreate(name, color, text, out ChatMessage? message) || message == null) return false;
            Publish(message, 0);
            return true;
        }

        /// <summary>Numbers a message, keeps it, and queues it for every guest that has finished joining.</summary>
        private void Publish(ChatMessage message, int playerId)
        {
            lock (chatGate)
            {
                ChatMessage stamped = message.Stamped(++lastChatSequence, playerId);
                Chat.Add(stamped);
                JObject frame = stamped.ToJson();
                foreach (var pair in activityChannels)
                {
                    if (!pair.Key.Connected) { RemoveActivity(pair.Key); continue; }
                    pair.Value.PostOrdered(frame);
                }
            }
        }

        // The map, state and init event are already written, so this guest can now receive activity and chat.
        private void OpenActivityLane(ISocketStream client)
        {
            ActivityChannel channel = CreateActivityChannel(client);
            lock (chatGate)
            {
                // Opened and given the history in one step: a message published after this is queued behind it.
                activityChannels[client] = channel;
                foreach (JObject frame in ChatMessage.HistoryFrames(Chat.All())) channel.PostOrdered(frame);
            }
        }

        private void FinishQueuing(ISocketStream client)
        {
            // Log("finishing queuing");
            lock(queuedMessages)
            {
                if (queuedMessages.TryGetValue(client, out ConcurrentQueue<Outgoing> queue))
                {
                    // Log($"Found {queue.Count} messages");
                    while (queue.TryDequeue(out Outgoing message))
                    {
                        SendBytes(client, message.Wire, message.Type, message.Tick);
                    }
                    queuedMessages.TryRemove(client, out _);
                    // From now on what is sent to this guest goes after what was just written, in order: through its
                    // own lane for a direct link, directly otherwise.
                    if (!IsStopped && client is IBlockingWrites) OpenSendLane(client);
                    if (!IsStopped) OpenActivityLane(client);
                }
                else
                {
                    Log("Warning! Missing client!");
                }
            }
        }

        private void OpenSendLane(ISocketStream client)
        {
            string name = playerIds.TryGetValue(client, out int id) ? $"player {id}" : "a guest";
            sendLanes[client] = new SendLane(name, (wire, type, tick) => SendBytes(client, wire, type, tick));
        }

        // A guest that took nothing for SendStallLimitMs, or has too much waiting: dropped, as a Steam connection that stops
        // taking data is. Closing its stream also ends a write stuck on it. The other guests and the host play on.
        private void DropStalled(ISocketStream client, SendLane lane)
        {
            sendLanes.TryRemove(client, out _);
            lane.Close();
            HandleConnectionFailure(client, $"A player's connection took nothing for {SendStallLimitMs / 1000} seconds, so they were " +
                "dropped from the game (their game may have frozen, or their connection is gone).");
        }

        private void SendErrorMessage(ISocketStream client)
        {
            SendLength(client, 0);
            byte[] bytes = MessageToBuffer(errorMessage!);
            // TODO: Not sure this makes sense for Steam
            SendDataWithLength(client, bytes);
        }

        private async Task SendMap(ISocketStream client)
        { 
            Task<byte[]> task = mapProvider();
            Log("Waiting for map...");
            byte[] mapBytes = await task;

            // TODO: This may happen a bit early - it seems possible for
            // events from a prior frame to get queued. Maybe just need to filter
            // them on the client side.
            // Start recording messages as soon as the map is saved,
            // while the map is sending
            StartQueuing(client);

            Log($"Sending map with length {mapBytes.Length}");
            // The one paced frame: this runs on the joining guest's own thread, never the game thread.
            SendDataWithLength(client, mapBytes, paced: true);

            Log($"Sent map with length {mapBytes.Length} and Hash: {GetHashCode(mapBytes).ToString("X8")}");
        }

        private void SendState(ISocketStream client)
        {
            JObject message = new JObject();
            message[TICKS_KEY] = 0;
            message[TYPE_KEY] = SET_STATE_EVENT;
            message["hash"] = Hash;
            // Send directly - don't queue
            SendEvent(client, message);
        }

        void DoUserInitiatedEvent(JObject message, ISocketStream sendNowTo)
        {
            string type = (string?)message[TYPE_KEY] ?? "?";
            int tick = message[TICKS_KEY]?.Type == JTokenType.Integer ? (int)message[TICKS_KEY]! : -1;
            Send(message.ToString(Newtonsoft.Json.Formatting.None), type, tick, sendNowTo);
        }

        public override void DoUserInitiatedEvent(string json, string type, int tick)
        {
            Send(json, type, tick, null);
        }

        // Encoded, hashed and compressed once, whatever the number of guests: each is sent the same bytes. Before,
        // every event was written out as text and compressed again for each guest, and once more for the hash.
        private void Send(string json, string type, int tick, ISocketStream? sendNowTo)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(json);
            NoteInitiatedEvent(utf8, type);
            SendEventToClients(new Outgoing(CompressionUtils.Compress(utf8), type, tick), sendNowTo);
        }

        /// <summary>
        /// Every guest gets the message: queued for one still receiving its save (FinishQueuing sends it in order after
        /// the save), written at once to the others. <paramref name="sendNowTo"/>, a guest whose join is finishing, gets
        /// it written at once although it is still queued, ahead of its queue. Never write to another queued guest here:
        /// its join thread holds its stream for the whole paced save, and this runs under the lock every broadcast takes,
        /// so the host's game thread would wait for the rest of that save (two guests joining over direct IP at once).
        /// </summary>
        private void SendEventToClients(Outgoing message, ISocketStream? sendNowTo)
        {
            lock (queuedMessages)
            {
                for (int i = clients.Count - 1; i >= 0; i--)
                {
                    if (!clients[i].Connected)
                    {
                        queuedMessages.TryRemove(clients[i], out _);
                        RemoveActivity(clients[i]);
                        clients.RemoveAt(i);
                    }
                }
                // Share the join/close lock across enumeration and mutation.
                clients.ForEach(client =>
                {
                    if (client == sendNowTo)
                    {
                        SendBytes(client, message.Wire, message.Type, message.Tick);
                    }
                    else
                    {
                        QueueOrSentToClient(client, message);
                    }
                });
            }
        }

        private void QueueOrSentToClient(ISocketStream client, Outgoing message)
        {
            if (!client.Connected) return;

            if (queuedMessages.TryGetValue(client, out ConcurrentQueue<Outgoing> queue))
            {
                queue.Enqueue(message);
            }
            else if (sendLanes.TryGetValue(client, out SendLane? lane))
            {
                if (lane.IsStalled(SendLane.NowMs, SendStallLimitMs, MaxQueuedBytesPerGuest)) DropStalled(client, lane);
                else lane.Post(message.Wire, message.Type, message.Tick);
            }
            else
            {
                SendBytes(client, message.Wire, message.Type, message.Tick);
            }
        }

        public override void AbortSession(string reason)
        {
            var lanes = new List<SendLane>();
            try
            {
                lock (queuedMessages)
                    foreach (var client in clients.ToArray())
                    {
                        // A guest still receiving the save gets no reason, only the close below. Its join thread holds
                        // its stream for the whole paced save (about 1 MB/s over a direct connection), and this runs on
                        // the host's game thread (ReplayService.AbortReplay), which would wait for the rest of the save.
                        if (queuedMessages.ContainsKey(client)) continue;
                        // A guest with a send lane gets the reason after what is already queued for it, from its lane.
                        if (sendLanes.TryGetValue(client, out SendLane? lane))
                        {
                            lane.Post(MessageToBuffer(SessionFaultFrame(reason)), "SessionFault", TickCount);
                            lanes.Add(lane);
                        }
                        else SendSessionFault(client, reason);
                    }
                // Outside the lock, and briefly: a guest that takes nothing must not hold up the end of the session.
                long deadline = SendLane.NowMs + AbortFlushMs;
                foreach (SendLane lane in lanes) lane.WaitUntilEmpty((int)Math.Max(0, deadline - SendLane.NowMs));
            }
            finally { Close(); }
        }

        public override void Close()
        {
            base.Close();
            foreach (var pair in activityChannels) pair.Value.Close();
            foreach (var pair in sendLanes) pair.Value.Close();
            sendLanes.Clear();
            try
            {
                lock (queuedMessages) clients.ForEach(client => client.Close());
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            try
            {
                listener.Stop();
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }  
        }

        public void SendHeartbeat()
        {
            JObject message = new JObject();
            message[TICKS_KEY] = TickCount;
            message[TYPE_KEY] = HEARTBEAT_EVENT;
            // Simulate the user doing this
            DoUserInitiatedEvent(message);
        }
    }
}
