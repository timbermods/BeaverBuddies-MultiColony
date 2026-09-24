using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TimberNet
{
    // TODO: I should create a method here that attempts to operate on a steam,
    // and handles errors uniformly if it fails.
    // Pretty much any error means the session is over, but the game should show
    // the error rather than crashing.
    public abstract class TimberNetBase
    {
        public const int HEADER_SIZE = 4;
        public string? CompatibilityIdentity { get; set; }
        /// <summary>Optional information sent to the other player once builds match (see CompatibilityHandshake).</summary>
        public string? CompatibilityAdvisory { get; set; }
        public delegate void PeerAdvisoryReceived(string? peerName, string advisory);
        /// <summary>Raised on the update thread with the other player's advisory after a successful handshake.</summary>
        public event PeerAdvisoryReceived? OnPeerAdvisory;
        public Func<bool>? DetailedLoggingEnabled { get; set; }
        protected bool ShouldLogDetails => DetailedLoggingEnabled?.Invoke() == true;
        public event MessageReceived? OnSessionFault;
        private readonly ConcurrentQueue<string> sessionFaults = new ConcurrentQueue<string>();
        public virtual void AbortSession(string reason) { Close(); }
        /// <summary>
        /// Ends the session over something found on this side, the way a fault reported by the other player does:
        /// <see cref="OnSessionFault"/> is raised with <paramref name="reason"/> now, on the caller's thread. A handler
        /// that fails is logged and never reaches the caller.
        /// </summary>
        public void RaiseSessionFault(string reason)
        {
            NotifyEach(OnSessionFault, handler => ((MessageReceived)handler)(reason), "a session fault");
        }
        protected void SendSessionFault(ISocketStream stream, string reason)
        {
            SendEvent(stream, SessionFaultFrame(reason));
        }

        protected JObject SessionFaultFrame(string reason) =>
            new JObject { [TYPE_KEY] = "SessionFault", [TICKS_KEY] = TickCount, ["reason"] = reason };

        /// <summary>
        /// Starts a long-lived network thread of its own, above normal priority, for a loop that reads a connection. It
        /// was a thread-pool task: while the game's own workers (water, soil, the job system) kept every core busy, a
        /// pool task could wait tens of milliseconds for a turn before it read what had already arrived, and a guest's
        /// next tick waited with it.
        /// </summary>
        protected static Thread StartNetworkThread(string name, ThreadStart run)
        {
            var thread = new Thread(run) { IsBackground = true, Name = name };
            try { thread.Priority = ThreadPriority.AboveNormal; } catch (Exception) { }
            thread.Start();
            return thread;
        }
        public const string TICKS_KEY = "ticksSinceLoad";
        public const string TYPE_KEY = "type";
        public const string SET_STATE_EVENT = "SetState";
        public const string HEARTBEAT_EVENT = "Heartbeat";
        public const int MAX_BUFFER_SIZE = 8192 * 4; // 32K

        public delegate void MessageReceived(string message);
        public delegate void MapReceived(byte[] mapBytes);

        public event MessageReceived? OnLog;
        public event MessageReceived? OnError;
        public event MapReceived? OnMapReceived;

        /// <summary>
        /// A guest's host said it is moving its game to a waiting room (MoveFrames, 1.4.0-rc7), and the connection has
        /// ended: raised once on the update thread, instead of <see cref="OnError"/>.
        /// </summary>
        public event Action? OnHostMoved;
        private int hostMovePending;

        /// <summary>Queues <see cref="OnHostMoved"/> for the next Update (any thread).</summary>
        protected void QueueHostMoved() => Interlocked.Exchange(ref hostMovePending, 1);

        private readonly ConcurrentQueue<JObject> receivedEventQueue = new ConcurrentQueue<JObject>();
        // A guest hashes every event it receives, as the host hashed it when it sent it: the same bytes. The hash is
        // taken from the message's bytes on the receive thread, as it arrives, and kept here until the game thread
        // reads the event, so the game thread no longer writes each event out as text again only to hash it.
        private readonly ConditionalWeakTable<JObject, ReceivedHash> receivedHashes = new ConditionalWeakTable<JObject, ReceivedHash>();
        private sealed class ReceivedHash { public int Value; }
        private readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> errorQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<(string? Peer, string Advisory)> peerAdvisories = new ConcurrentQueue<(string?, string)>();
        private byte[]? mapBytes = null;

        private volatile bool isStopped;
        public bool IsStopped => isStopped;

        public int Hash { get; private set; } = 17;

        public int TickCount { get; private set; }

        private volatile int reportedFps;
        /// <summary>
        /// This player's own frames per second, set by the game every frame and sent to the host with each reply
        /// to its ping probe (which happens on a network thread). Zero or less means there is nothing to report.
        /// </summary>
        public int ReportedFps { get => reportedFps; set => reportedFps = value; }

        public int TicksBehind
        {
            get
            {
                if (receivedEvents.Count == 0)
                    return 0;
                return Math.Max(0, GetTick(receivedEvents.Last()) - TickCount);
            }
        }

        public bool Started { get; private set; }

        public virtual bool ShouldTick => Started && !IsStopped;

        protected List<JObject> receivedEvents = new List<JObject>();

        public virtual void Close()
        {
            isStopped = true;
        }

        // ---- Player activity (presentation only) ----
        // Activity frames never touch receivedEventQueue, the replay script or Hash.

        private readonly ActivityMailbox activityMailbox = new ActivityMailbox();

        /// <summary>Takes the newest state of each remote player; call from the game thread.</summary>
        public PlayerActivity[] TakeActivity() => activityMailbox.Take(ActivityMailbox.Now);

        public void ClearActivity() => activityMailbox.Clear();

        /// <summary>Shares this player's activity. The sender's id is ignored: the host assigns identity.</summary>
        public virtual void SendActivity(PlayerActivity activity) { }

        // The map is always the first frame a client reads, so nothing else may be written
        // to the host before this point.
        protected virtual void OnMapFrameReceived(ISocketStream stream) { }

        protected virtual void HandleActivity(ISocketStream source, PlayerActivity activity)
        {
            activityMailbox.Put(activity, ActivityMailbox.Now);
        }

        // ---- Chat (presentation only) ----
        // Chat shares the activity lane: never replayed, never hashed, handled before it can reach the event queue.

        /// <summary>Every chat message of this session, oldest first. Safe to read from the game thread.</summary>
        public ChatLog Chat { get; } = new ChatLog();

        /// <summary>
        /// Says something in chat as this player. The host assigns the sender's id and the message's place in
        /// the conversation, so the name and color are all a guest gets to choose. False if it could not be sent:
        /// nothing left of the text once cleaned, or not connected yet.
        /// </summary>
        public virtual bool SendChat(string name, string color, string text) => false;

        /// <summary>A chat frame arrived on the receive thread. <paramref name="isHistory"/> is true for a host's catch-up batch.</summary>
        protected virtual void HandleChat(ISocketStream source, IReadOnlyList<ChatMessage> messages, bool isHistory) { }

        private void ReceiveChat(ISocketStream source, JObject frame, string type)
        {
            if (type == ChatMessage.HistoryType)
            {
                if (ChatMessage.TryParseHistory(frame, out List<ChatMessage> history)) HandleChat(source, history, true);
            }
            else if (ChatMessage.TryParse(frame, out ChatMessage? message) && message != null)
            {
                HandleChat(source, new[] { message }, false);
            }
        }

        // ---- Connection status feed (presentation only) ----
        // Ping probes and the player roster share the activity lane: never replayed, never hashed.

        protected virtual void HandleStatusFrame(ISocketStream source, string type, JObject message) { }

        /// <summary>The JSON key of the player number the host writes onto each event a guest sends.</summary>
        public const string PLAYER_KEY = "player";

        /// <summary>
        /// Called for each event received from <paramref name="source"/> before it is queued. The host overrides it to
        /// write who sent the event; a guest trusts whatever the host sends it.
        /// </summary>
        protected virtual void StampReceivedEvent(ISocketStream source, JObject message) { }

        /// <summary>
        /// Writes <paramref name="player"/> onto an event and onto every event grouped inside it, replacing any value
        /// the sender wrote, so a guest can never claim to be someone else.
        /// </summary>
        public static void StampPlayer(JObject message, int player)
        {
            message[PLAYER_KEY] = player;
            // The mod writes a grouped list with its type name ({"$type": ..., "$values": [...]}); accept a plain
            // array as well.
            JToken? events = message["events"];
            JArray? children = events as JArray ?? (events as JObject)?["$values"] as JArray;
            if (children != null)
            {
                foreach (JToken child in children)
                {
                    if (child is JObject childObject) childObject[PLAYER_KEY] = player;
                }
            }
        }

        /// <summary>Called on the game thread from <see cref="Update"/> while the session is running.</summary>
        protected virtual void OnUpdate() { }

        /// <summary>A snapshot of who is connected and how well, for display.</summary>
        public virtual NetworkStatus GetNetworkStatus() => NetworkStatus.None(IsStopped);

        protected ActivityChannel CreateActivityChannel(ISocketStream stream) =>
            new ActivityChannel(stream, WriteActivityFrame, HandleConnectionFailure);

        private void WriteActivityFrame(ISocketStream stream, JObject message)
        {
            SendDataWithLength(stream, MessageToBuffer(message));
        }

        public TimberNetBase()
        {
            Log("Started");
        }

        protected void Log(string message)
        {
            Log(message, TickCount, Hash);
        }

        protected void Log(string message, int ticks, int hash)
        {
            // Should be threadsafe
            OnLog?.Invoke($"T{ticks.ToString("D4")} [{hash.ToString("X8")}] : {message}");
            //logQueue.Enqueue($"T{ticks.ToString("D4")} [{hash.ToString("X8")}] : {message}");
        }

        public virtual void Start()
        {
            Started = true;
        }

        public static int GetTick(JObject message)
        {
            if (message[TICKS_KEY] == null)
                throw new Exception($"Message does not contain {TICKS_KEY} key");
            return message[TICKS_KEY]!.ToObject<int>();
        }

        /// <summary>
        /// The frame's "type", or null if it has none or it is not a string. Frames come from the other player and
        /// this is read inside a tick (ReadEvents), where an exception would stop the tick halfway: a frame without
        /// a type is passed on like an action, and one that cannot be read is dealt with there.
        /// </summary>
        public static string? GetType(JObject message)
        {
            return message[TYPE_KEY] is JValue { Type: JTokenType.String } type ? (string?)type : null;
        }

        protected void InsertInScript(JObject message, List<JObject> script)
        {
            int tick = GetTick(message);
            // The common path is already in tick order. Equal ticks append,
            // preserving the host's action order.
            if (script.Count == 0 || GetTick(script[script.Count - 1]) <= tick)
            {
                script.Add(message);
                return;
            }
            int low = 0, high = script.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (GetTick(script[middle]) <= tick) low = middle + 1;
                else high = middle;
            }
            script.Insert(low, message);
        }

        public static List<T> PopEventsForTick<T>(int tick, List<T> events, Func<T, int> getTick)
        {
            int count = 0;
            while (count < events.Count && getTick(events[count]) <= tick) count++;
            if (count == 0) return new List<T>();
            var ready = events.GetRange(0, count);
            // Shift the remaining backlog once, rather than once per event.
            events.RemoveRange(0, count);
            return ready;
        }

        /// <summary>How many ticks late an event may normally be read (see TimberServer).</summary>
        protected virtual int ExpectedLateness => 0;

        private List<JObject> PopEventsToProcess(List<JObject> events)
        {
            if (events.Count == 0) return new List<JObject>();
            JObject firstEvent = events[0];
            int firstEventTick = GetTick(firstEvent);
            if (firstEventTick < TickCount - ExpectedLateness)
                Log($"Warning: late event {GetType(firstEvent)}: {firstEventTick} < {TickCount}");

            return PopEventsForTick(TickCount, events, GetTick);
        }

        /**
         * Process an event that the user initiated.
         */
        public virtual void DoUserInitiatedEvent(JObject message)
        {
            // The type and tick only name the event in the detailed log: a frame without them (a test's malformed
            // frame) is still sent, as it was.
            string type = (string?)message[TYPE_KEY] ?? "?";
            int tick = message[TICKS_KEY]?.Type == JTokenType.Integer ? (int)message[TICKS_KEY]! : -1;
            DoUserInitiatedEvent(message.ToString(Newtonsoft.Json.Formatting.None), type, tick);
        }

        /// <summary>
        /// An event this player initiated, as the compact JSON the game serialized it to, with its type and tick, so
        /// that nothing here parses it or writes it out again: its bytes are hashed once and (by a host) compressed
        /// once, and every guest is sent those same bytes.
        /// </summary>
        public virtual void DoUserInitiatedEvent(string json, string type, int tick)
        {
            NoteInitiatedEvent(Encoding.UTF8.GetBytes(json), type);
        }

        /// <summary>What every initiated event does first: it joins this player's running hash, and is logged in detail.</summary>
        protected void NoteInitiatedEvent(byte[] utf8, string type)
        {
            AddToHash(utf8);
            if (ShouldLogDetails) Log($"Event: {type}");
        }

        /**
        * Process a validated event from a peer that is ready to happen on
        * the Update() thread.
        */
        protected virtual void ProcessReceivedEvent(JObject message)
        {
        }

        protected void AddEventToHash(JObject message)
        {
            if (GetType(message) == SET_STATE_EVENT)
            {
                Hash = message["hash"]!.ToObject<int>();
            }
            else if (receivedHashes.TryGetValue(message, out ReceivedHash? received))
            {
                // Hashed from its bytes as it arrived (see ReceiveMessages).
                receivedHashes.Remove(message);
                Hash = CombineHash(Hash, received.Value);
            }
            else
            {
                AddToHash(message.ToString(Newtonsoft.Json.Formatting.None));
            }
            if (ShouldLogDetails) Log($"Event: {GetType(message)}");
        }

        protected void SendLength(ISocketStream stream, int length)
        {
            byte[] buffer = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            stream.Write(buffer, 0, buffer.Length);
        }

        protected void SendDataWithLength(ISocketStream stream, byte[] data)
        {
            SendDataWithLength(stream, data, paced: false);
        }

        /// <summary>
        /// Writes one frame. <paramref name="paced"/> spreads its chunks out to the stream's MaxBytesPerSecond by
        /// sleeping between them. Only the save sent to a joining guest is paced: it is written on that guest's own
        /// join thread. Every other frame is written by whichever thread sends it, often the game thread (a tick's
        /// events), which must never sleep here: a gameplay frame over one chunk used to stall the host's game
        /// thread about 31 ms per extra 32 KB over a direct connection.
        /// </summary>
        protected void SendDataWithLength(ISocketStream stream, byte[] data, bool paced)
        {
            // A frame includes both its header and every payload chunk. Join
            // workers and the game thread can otherwise interleave their writes.
            lock (stream)
            {
                int chunkSize = stream.MaxChunkSize;
                // The length and the start of the message go out in one write: two small writes in a row are what
                // makes a TCP link hold the second back (Nagle and delayed acknowledgements), and on a Steam link it
                // is one message instead of two. The first write stays within the stream's chunk size (a stream whose
                // chunks are smaller than the length gets the length alone, as before).
                int first = Math.Max(0, Math.Min(data.Length, chunkSize - 4));
                byte[] head = new byte[4 + first];
                byte[] length = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(length);
                Buffer.BlockCopy(length, 0, head, 0, 4);
                Buffer.BlockCopy(data, 0, head, 4, first);
                stream.Write(head, 0, head.Length);
                // Only the paced save sleeps between chunks (see above); a gameplay frame goes out at once.
                int sleepMS = paced ? stream.MaxChunkSize * 1000 / stream.MaxBytesPerSecond : 0;
                for (int i = first; i < data.Length; i += chunkSize)
                {
                    if (sleepMS > 0) Thread.Sleep(sleepMS);
                    int count = Math.Min(chunkSize, data.Length - i);
                    stream.Write(data, i, count);
                }
            }
        }

        protected void SendEvent(ISocketStream client, JObject message)
        {
            if (ShouldLogDetails) Log($"Sending: {GetType(message)} for tick {GetTick(message)}");
            SendWire(client, MessageToBuffer(message));
        }

        /// <summary>Sends an event already compressed for the wire (a host sends every guest the same bytes).</summary>
        protected void SendBytes(ISocketStream client, byte[] wire, string type, int tick)
        {
            if (ShouldLogDetails) Log($"Sending: {type} for tick {tick}");
            SendWire(client, wire);
        }

        private void SendWire(ISocketStream client, byte[] wire)
        {
            try
            {
                SendDataWithLength(client, wire);
            } catch (Exception e)
            {
                HandleConnectionFailure(client, $"Error sending event: {e.Message}");
            }
        }

        protected virtual void HandleConnectionFailure(ISocketStream stream, string message)
        {
            // Read the reason before closing: closing may replace it with a generic one.
            message = DescribeFailure(stream, message);
            // After a partial write the framing cannot safely be reused.
            stream.Close();
            Log(message);
        }

        /// <summary>Adds the transport's own explanation (for example Steam's end reason), if it has one.</summary>
        protected static string DescribeFailure(ISocketStream stream, string message)
        {
            string? reason = (stream as IFailureDescriber)?.FailureReason;
            return string.IsNullOrEmpty(reason) || message.Contains(reason) ? message : message + "\n" + reason;
        }

        protected void QueueError(string message) => errorQueue.Enqueue(message);

        /// <summary>
        /// Runs the build check with this player's identity, and swaps advisories if one is set. The other
        /// player's advisory is delivered to OnPeerAdvisory on the next Update.
        /// </summary>
        protected void RunCompatibilityHandshake(ISocketStream stream, bool server)
        {
            string? remote = CompatibilityHandshake.Run(stream, CompatibilityIdentity!, server, advisory: CompatibilityAdvisory);
            if (remote != null) peerAdvisories.Enqueue((stream.Name, remote));
        }

        protected bool TryReadLength(ISocketStream stream, out int length)
        {
            byte[] headerBuffer;
            try
            {
                headerBuffer = stream.ReadUntilComplete(HEADER_SIZE);
            }
            catch
            {
                length = 0;
                return false;
            }
            if (BitConverter.IsLittleEndian)
                Array.Reverse(headerBuffer);

            length = BitConverter.ToInt32(headerBuffer, 0);
            return true;
        }

        protected void StartListening(ISocketStream client, bool isClient)
        {
            try
            {
                ReceiveMessages(client, isClient);
            }
            catch (Exception e)
            {
                if (!IsStopped) HandleConnectionFailure(client, $"Error receiving data: {e.Message}");
            }
            finally
            {
                if (!IsStopped) HandleConnectionFailure(client, "The multiplayer connection was closed.");
                client.Close();
            }
        }

        private void ReceiveMessages(ISocketStream client, bool isClient)
        {
            //Log("Client connected");
            int messageCount = 0;
            // A guest reads on until its stream ends, not while it says it is connected: a Steam link marks itself closed as
            // soon as the host's close arrives, with the host's last frames (its move notice, say) still waiting to be read.
            while ((isClient || client.Connected) && !IsStopped)
            {
                if (!TryReadLength(client, out int messageLength)) break;

                // A waiting-room frame (see LobbyFrames): only a host that opened a waiting room writes the marker, and
                // only before the guest's save. A guest reads one anywhere and uses it only before its save: a
                // keep-alive can race the save frame. The marker never comes from a guest.
                if (messageLength == LobbyFrames.Sentinel)
                {
                    if (!isClient)
                    {
                        HandleConnectionFailure(client, "A guest sent a waiting-room frame, which only a host sends.");
                        break;
                    }
                    if (!TryReadLength(client, out int lobbyLength) || lobbyLength <= 0 || lobbyLength > LobbyFrames.MaxFrameBytes) break;
                    byte[] lobbyFrame = client.ReadUntilComplete(lobbyLength);
                    if (messageCount == 0) ReceiveLobbyFrame(client, lobbyFrame);
                    continue;
                }

                // A guest still in the host's waiting room may send only its hello and its ready, and small ones; nothing
                // it sends reaches the game before it has the save (IsInWaitingRoom).
                if (!isClient && IsInWaitingRoom(client))
                {
                    if (messageLength <= 0 || messageLength > LobbyFrames.MaxFrameBytes)
                    {
                        HandleConnectionFailure(client, $"A guest in the waiting room sent a frame of {messageLength} bytes.");
                        break;
                    }
                    ReceiveLobbyFrame(client, client.ReadUntilComplete(messageLength));
                    continue;
                }

                // First message is always the file
                if (messageCount == 0 && isClient)
                {
                    if (messageLength == 0)
                    {
                        ReadErrorMessage(client);
                        return;
                    }

                    ReceiveFile(client, messageLength);
                    OnMapFrameReceived(client);
                    messageCount++;
                    continue;
                }

                if (messageLength == 0)
                {
                    Log("Received message of length 0; aborting listen");
                    break;
                }

                //Log($"Starting to read {messageLength} bytes");
                // TODO: How should this fail and not hang if map stops sending?
                byte[] buffer = client.ReadUntilComplete(messageLength);

                byte[] text = CompressionUtils.DecompressToBytes(buffer);
                string message = Encoding.UTF8.GetString(text);
                var control = JObject.Parse(message);
                if ((string?)control[TYPE_KEY] == PlayerActivity.MessageType)
                {
                    // Optional display data: a malformed frame is dropped, never fatal to the session.
                    if (PlayerActivity.TryParse(control, out PlayerActivity? activity) && activity != null)
                        HandleActivity(client, activity);
                    continue;
                }
                string? controlType = (string?)control[TYPE_KEY];
                if (StatusFrames.IsStatusType(controlType))
                {
                    // Optional display data: a bad frame is ignored, never fatal to the session.
                    try { HandleStatusFrame(client, controlType!, control); }
                    catch (Exception e) { Log("Ignoring a bad status frame: " + e.Message); }
                    continue;
                }
                if (ChatMessage.IsChatType(controlType))
                {
                    // Optional display data: a bad frame is ignored, never fatal to the session.
                    try { ReceiveChat(client, control, controlType!); }
                    catch (Exception e) { Log("Ignoring a bad chat frame: " + e.Message); }
                    continue;
                }
                // Waiting-room frames are never actions: one that comes late (a ready toggled as the save went out) is
                // dropped.
                if (LobbyFrames.IsLobbyType(controlType)) continue;
                // The host moving its game to a waiting room (MoveFrames): a guest stops reading here, before the host
                // closes its end, and its connection's end is no error. Only a host says it: a guest's is dropped.
                if (MoveFrames.IsMoveType(controlType))
                {
                    if (!isClient) continue;
                    OnHostMoving(MoveFrames.TryParse(control, out ulong? lobby) ? lobby : null);
                    return;
                }
                if ((string?)control[TYPE_KEY] == "SessionFault")
                {
                    sessionFaults.Enqueue("A peer could not replay a multiplayer action. Reload a known-good save before rehosting.");
                    return;
                }
                //Log($"Queuing message of length {messageLength} bytes");
                StampReceivedEvent(client, control);
                // A guest hashes what it receives as the host hashed what it sent: these bytes (see AddEventToHash).
                if (isClient) receivedHashes.Add(control, new ReceivedHash { Value = GetHashCode(text) });
                receivedEventQueue.Enqueue(control);
                messageCount++;
            }
        }

        /// <summary>A guest read its host's move notice (MoveFrames), on its receive thread; it stops reading after this.</summary>
        protected virtual void OnHostMoving(ulong? steamLobby) { }

        // ---- Waiting room (presentation only; see LobbyFrames) ----

        /// <summary>Host: the connection is a guest still in the waiting room, which may send only waiting-room frames.</summary>
        protected virtual bool IsInWaitingRoom(ISocketStream source) => false;

        /// <summary>A waiting-room frame arrived on the receive thread (a guest's on the host, the host's on a guest).</summary>
        protected virtual void HandleLobbyFrame(ISocketStream source, string type, JObject frame) { }

        private void ReceiveLobbyFrame(ISocketStream source, byte[] compressed)
        {
            try
            {
                JObject frame = JObject.Parse(CompressionUtils.Decompress(compressed, LobbyFrames.MaxFrameBytes));
                string? type = GetType(frame);
                if (type != null && LobbyFrames.IsLobbyType(type)) HandleLobbyFrame(source, type, frame);
            }
            // Display only: a frame that can't be read is dropped, never fatal.
            catch (Exception e) { Log("Ignoring a bad waiting-room frame: " + e.Message); }
        }

        protected byte[] MessageToBuffer(JObject message)
        {
            string json = message.ToString(Newtonsoft.Json.Formatting.None);
            return MessageToBuffer(json);
        }

        protected byte[] MessageToBuffer(string message)
        {
            return CompressionUtils.Compress(message);
        }

        protected string BufferToStringMessage(byte[] buffer)
        {
            return CompressionUtils.Decompress(buffer);
        }

        private void ReadErrorMessage(ISocketStream stream)
        {
            if (TryReadLength(stream, out int length))
            {
                byte[] bytes = stream.ReadUntilComplete(length);
                string message = BufferToStringMessage(bytes);
                HandleConnectionFailure(stream, message);
            }
        }

        public static int CombineHash(int h1, int h2)
        {
            return h1 * 31 + h2;
        }

        private void AddToHash(string str)
        {
            AddToHash(Encoding.UTF8.GetBytes(str));
        }

        protected void AddToHash(byte[] bytes)
        {
            Hash = CombineHash(Hash, GetHashCode(bytes));
        }

        public static int GetHashCode(byte[] bytes)
        {
            int code = 0;
            foreach (byte b in bytes)
            {
                code = CombineHash(code, b);
            }
            return code;
        }

        private void AddFileToHash(byte[] bytes)
        {
            AddToHash(bytes);
        }

        private void ReceiveFile(ISocketStream stream, int messageLength)
        {
            byte[] mapBytes = stream.ReadUntilComplete(messageLength);
            AddFileToHash(mapBytes);
            Log($"Received map with length {mapBytes.Length} and Hash: {GetHashCode(mapBytes).ToString("X8")}");
            this.mapBytes = mapBytes;
        }

        private void ProcessReceivedEventsQueue()
        {
            while (receivedEventQueue.TryDequeue(out JObject? message))
            {
                try
                {
                    ReceiveEvent(message);
                } catch (Exception e)
                {
                    Log($"Error receiving event: {e.Message}");
                }
            }
        }

        /**
         * Called when an event is received from a connected Net
         * and ready to be added to the queue for processing.
         */
        protected virtual void ReceiveEvent(JObject message)
        {
            InsertInScript(message, receivedEvents);
        }

        private void ProcessLogs()
        {
            while (logQueue.TryDequeue(out string? log))
            {
                OnLog?.Invoke(log);
            }
        }

        private void ProcessReceivedMap()
        {
            if (mapBytes == null) return;
            byte[] received = mapBytes;
            mapBytes = null;
            if (!NotifyEach(OnMapReceived, handler => ((MapReceived)handler)(received), "the received save"))
            {
                // Without this the guest would sit in the menu with no explanation.
                QueueError("The save from the host arrived but could not be loaded. See Player.log for details.");
            }
        }

        /// <summary>
        /// Calls every subscriber by itself and never lets one throw into the caller. These handlers show
        /// dialogs and load scenes, and they are reached from the game's update loop: in 1.0.4 one of them
        /// threw while reporting a lost connection and the uncaught exception crashed the game. A failing
        /// handler is logged; the others still run. Returns false if any subscriber failed.
        /// </summary>
        private bool NotifyEach(Delegate? handlers, Action<Delegate> call, string what)
        {
            if (handlers == null) return true;
            bool allSucceeded = true;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try { call(handler); }
                catch (Exception e)
                {
                    allSucceeded = false;
                    Log($"Ignoring an error in a handler for {what}: {e}");
                }
            }
            return allSucceeded;
        }

        /**
         * Updates, processing queued logs, maps and events.
         */
        public void Update()
        {
            ProcessLogs();
            while (sessionFaults.TryDequeue(out string? fault))
            {
                string message = fault;
                NotifyEach(OnSessionFault, handler => ((MessageReceived)handler)(message), "a session fault");
            }
            // The host moved its game to a waiting room: the connection's end, which would otherwise be an error.
            if (Interlocked.Exchange(ref hostMovePending, 0) == 1)
                NotifyEach(OnHostMoved, handler => ((Action)handler)(), "the host's move to a waiting room");
            // UI subscribers must only run on the caller's update thread.
            while (errorQueue.TryDequeue(out string? error))
            {
                string message = error;
                NotifyEach(OnError, handler => ((MessageReceived)handler)(message), "a connection error");
            }
            while (peerAdvisories.TryDequeue(out var advisory))
            {
                // Optional information: a handler that fails must never end the session.
                try { OnPeerAdvisory?.Invoke(advisory.Peer, advisory.Advisory); }
                catch (Exception e) { Log("Ignoring an error handling the other player's compatibility information: " + e.Message); }
            }
            if (!Started || IsStopped) return;
            ProcessReceivedMap();
            ProcessReceivedEventsQueue();
            OnUpdate();

        }

        private List<JObject> FilterEvents(List<JObject> events)
        {
            return events.Where(ShouldReadEvent).ToList();
        }

        private bool ShouldReadEvent(JObject message)
        { 
            string? type = GetType(message);
            return !(type == SET_STATE_EVENT || type == HEARTBEAT_EVENT);
        }

        /**
         * Reads received events that should be processed by the game
         * and deletes and returns.
         * Will call update before processing events.
         */
        public virtual List<JObject> ReadEvents(int ticksSinceLoad)
        {
            //if (ticksSinceLoad != TickCount) Log($"Setting ticks from {TickCount} to {ticksSinceLoad}");
            TickCount = ticksSinceLoad;
            Update();
            if (IsStopped) return new List<JObject>();
            List<JObject> toProcess = PopEventsToProcess(receivedEvents);
            toProcess.ForEach(e => ProcessReceivedEvent(e));
            return FilterEvents(toProcess);
        }

        public bool HasEventsForTick(int tickSinceLoad)
        {
            Update();
            return !IsStopped && receivedEvents.Any(e => GetTick(e) == tickSinceLoad);
        }
    }
}
