#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TimberNet;

/// <summary>
/// Reviewer A's rigs for the 1.4.0-beta18..20 review (the join and the waiting room): J1, J2/J4, J4 (the thread pool),
/// J8, J10 (hostile frames, the hello flood) and X4's frame parser. Everything here compiles against beta20's API, so the
/// same file runs against the unfixed tree (to show the problems) and the fixed one. Each check stays under 5 s.
/// </summary>
static class JoinReviewChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static bool Until(Func<bool> condition, int ms = 3000) => SpinWait.SpinUntil(condition, ms);

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        // ---- J1 ----

        yield return ("J1: LoadMap words the loading tip before it empties the registry, and the room check stops once the save has come", () =>
        {
            string source = File.ReadAllText(Path.Combine(Root(), "BeaverBuddies", "Connect", "ClientConnectionService.cs"));
            string loadMap = Body(source, "private void LoadMap(");
            int reset = loadMap.IndexOf("SingletonManager.Reset()", StringComparison.Ordinal);
            Check(reset >= 0, "LoadMap no longer resets the registry; update this check");
            // Nothing in LoadMap may read the registry after the reset: the menu's singletons are gone by then (the tip read
            // RegisteredLocalizationService and threw, so the save was never loaded).
            string afterReset = loadMap.Substring(reset);
            Check(!Regex.IsMatch(afterReset, @"RegisteredLocalizationService\.T\(|SingletonManager\.GetSingleton<"),
                "LoadMap reads the singleton registry after SingletonManager.Reset()");
            // The waiting-room check must not treat the frame the save is delivered in (registry already emptied) as "in a game".
            string check = Body(source, "private void CheckWaitingRoom(");
            Check(Regex.IsMatch(check, @"saveReceived|SaveArrived|MapDelivered|JoinFlowRules\."),
                "CheckWaitingRoom has no guard for a save that has already come (it drops the guest in the frame its save loads)");
            // The main menu's own panel must survive an empty registry (a page popped over it re-runs GetPanel).
            string ui = File.ReadAllText(Path.Combine(Root(), "BeaverBuddies", "Connect", "ClientConnectionUI.cs"));
            Check(!ui.Contains("GetSingleton<ClientConnectionUI>().AddJoinButton"),
                "MainMenuPanel.GetPanel's postfix throws when the registry is empty, which leaves the main menu without a panel");
        });

        yield return ("J1 premise: in the frame a waiting-room guest's save is handed to the game it is still 'welcomed'", () =>
        {
            byte[] welcome = CompressionUtils.Compress(LobbyFrames.Welcome(1, new LobbySummary("Folktails", "Map", null, "Town", "Kyler", true)).ToString());
            var bytes = new List<byte>();
            bytes.AddRange(Length(LobbyFrames.Sentinel)); bytes.AddRange(Length(welcome.Length)); bytes.AddRange(welcome);
            bytes.AddRange(Length(3)); bytes.AddRange(new byte[] { 7, 8, 9 });
            var (hostSide, guestSide) = PipeStream.Pair();
            hostSide.Write(bytes.ToArray(), 0, bytes.Count);
            var client = new TimberClient(guestSide);
            bool? welcomedAtSave = null;
            client.OnMapReceived += _ => welcomedAtSave = client.Lobby.View().Welcomed;
            try
            {
                client.Start();
                Check(Until(() => { client.Update(); return welcomedAtSave != null; }), "no save");
                // So CheckWaitingRoom, which runs right after client.Update() in the same UpdateSingleton, sees a welcomed
                // guest whose registry LoadMap has just emptied, and takes the in-a-game branch (D20).
                Check(welcomedAtSave == true);
            }
            finally { client.Close(); hostSide.Close(); }
        });

        // ---- J2 / J4: the straggler removal against StartQueuing ----

        yield return ("J2/J4: a guest taken out of the room as its join reaches StartQueuing is never let in as a new player", () => WithRoom(rig =>
        {
            var guest = rig.Join();
            Check(Until(() => guest.Lobby.View().Welcomed), "not welcomed");
            int number = guest.Lobby.View().You;
            object queuedMessages = Field(rig.Host, "queuedMessages");
            rig.Host.CloseLobbyToNewcomers("closed");
            rig.Host.SetLobbyStage(LobbyStage.SendingWorld);
            // Hold the lock StartQueuing takes, so the join stops right after SendMap's "still a member" check.
            Monitor.Enter(queuedMessages);
            bool held = true;
            try
            {
                rig.Host.ReleaseLobby(Enumerable.Range(0, 20000).Select(i => (byte)i).ToArray());
                Thread.Sleep(600);
                // LobbySession.Update's 10 s straggler removal, at that moment.
                Check(rig.Host.RemoveFromLobby(number, LobbyEndReason.Failed, "late"), "the member was already in the game");
                Thread.Sleep(100);
                Monitor.Exit(queuedMessages);
                held = false;
                Thread.Sleep(400);
                int lastPlayerId = (int)Field(rig.Host, "lastPlayerId");
                // beta20: StartQueuing no longer finds it in the room, IsAcceptingClients is still true (a waiting room never
                // sets TimberServer's error), so it is numbered afresh (player 2) and added to the game's clients.
                Check(lastPlayerId == number, $"a guest removed from the room was admitted as new player {lastPlayerId}");
            }
            finally { if (held) Monitor.Exit(queuedMessages); }
        }));

        // ---- J4: every guest's paced save at once, with a starved thread pool ----

        yield return ("J4: seven guests' paced saves, with the thread pool starved, all queue well inside the 10 s", () =>
        {
            int previousFlush = TimberServer.AbortFlushMs;
            TimberServer.AbortFlushMs = 300;
            ThreadPool.GetMinThreads(out int minWorkers, out _);
            using var release = new ManualResetEventSlim();
            var blockers = new List<Task>();
            try
            {
                WithRoom(rig =>
                {
                    var guests = new List<TimberClient>();
                    for (int i = 0; i < LobbyRoom.MaxGuests; i++)
                    {
                        var guest = rig.Join(wrap: s => new PacedStream(s, 1024 * 1024));
                        Check(Until(() => guest.Lobby.View().Welcomed), $"guest {i} not welcomed");
                        guests.Add(guest);
                    }
                    rig.Host.CloseLobbyToNewcomers("closed");
                    rig.Host.SetLobbyStage(LobbyStage.SendingWorld);
                    // Leave two of the pool's threads (the rig's accept loop holds one of them): beyond them a new pool thread
                    // only comes from the pool's starvation injection (about two a second), as on a 4-core host whose three accept
                    // loops hold most of its pool. With every thread but one taken it took 3.9 s here.
                    // Counted from the threads the pool has now (earlier checks may have grown it past its minimum).
                    int toBlock = Math.Max(minWorkers, ThreadPool.ThreadCount) - 2;
                    for (int i = 0; i < toBlock; i++) blockers.Add(Task.Factory.StartNew(() => release.Wait(4000)));
                    Thread.Sleep(100);
                    var clock = Stopwatch.StartNew();
                    rig.Host.ReleaseLobby(new byte[300 * 1024]);
                    Check(Until(() => rig.Host.LobbyGuestsQueued, 4500), "not every guest queued within 4.5 s");
                    long ms = clock.ElapsedMilliseconds;
                    Console.WriteLine($"    (J4: all {LobbyRoom.MaxGuests} guests queued {ms} ms after the release, with {toBlock} pool threads taken, two left, and the accept loop holding one of those)");
                    Check(ms < 8000);
                    release.Set();
                });
            }
            finally
            {
                release.Set();
                Task.WaitAll(blockers.ToArray(), 2000);
                TimberServer.AbortFlushMs = previousFlush;
            }
        });

        // ---- J10: hostile frames before admission ----

        yield return ("J10: junk, gzip bombs, deep JSON and wrong types from a waiting guest are dropped; the room and the others go on", () => WithRoom(rig =>
        {
            var other = rig.Join();
            Check(Until(() => other.Lobby.View().Welcomed));
            PipeStream raw = rig.JoinRaw();
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 3));
            var net = new TestNet();
            var random = new Random(12345);
            for (int i = 0; i < 300; i++)
            {
                byte[] junk = new byte[random.Next(1, 4096)];
                random.NextBytes(junk);
                net.Send(raw, junk);
            }
            // A bomb: 10 MB of zeros in a few KB.
            net.Send(raw, CompressionUtils.Compress(new string('0', 10 * 1024 * 1024)));
            // Deep JSON inside the 64 KB cap.
            net.Send(raw, CompressionUtils.Compress(new string('[', 20000) + new string(']', 20000)));
            string[] wrong =
            {
                "[1,2,3]", "\"LobbyHello\"", "{\"type\":5}", "{\"type\":\"LobbyHello\",\"id\":7,\"name\":\"x\"}",
                "{\"type\":\"LobbyHello\",\"id\":\"" + new string('a', 65) + "\",\"name\":\"x\"}",
                "{\"type\":\"LobbyHello\",\"id\":\"local:a\\nb\",\"name\":\"x\"}",
                "{\"type\":\"LobbyReady\",\"ready\":\"yes\"}", "{\"type\":\"LobbyReady\",\"ready\":1}",
                "{\"type\":\"LobbyFaction\",\"faction\":123}", "{\"type\":\"LobbyFaction\",\"faction\":[\"IronTeeth\"]}",
                "{\"type\":\"LobbyWelcome\",\"you\":1}", "{\"type\":\"LobbyEnd\",\"reason\":\"removed\"}",
                "{\"type\":\"LobbyRoster\",\"players\":[]}", "{\"type\":\"LobbyState\",\"seq\":99999999999,\"state\":\"open\"}",
                "{\"type\":\"SessionFault\",\"reason\":\"x\"}", "{\"type\":\"PlayerActivity\",\"player\":0}",
                "{\"type\":\"ChatMessage\",\"text\":\"hi\"}", "{\"type\":\"Heartbeat\",\"ticksSinceLoad\":0}",
            };
            foreach (string text in wrong) net.Send(raw, CompressionUtils.Compress(text));
            net.Send(raw, CompressionUtils.Compress(LobbyFrames.Hello("local:raw", "Raw").ToString()));
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Any(p => p.Name == "Raw")), "the hello after the junk was not read");
            LobbySnapshot snapshot = rig.Host.Lobby!.Snapshot();
            Check(snapshot.Guests.All(g => g.Faction == null) && !snapshot.Players[1].Ready && !snapshot.Players[2].Ready, "a bad frame changed the room");
            Check(!rig.Host.IsStopped && rig.Host.ReadEvents(0).Count == 0, "a waiting guest's frame reached the game");
            Check(RttTracker.NowMs - other.Lobby.View().LastFrameAtMs < 500, "the other guest stopped hearing the host");
            Check(!other.IsStopped);
        }));

        yield return ("J10: every length a waiting guest may not send closes only that guest", () => WithRoom(rig =>
        {
            var other = rig.Join();
            Check(Until(() => other.Lobby.View().Welcomed));
            foreach (int length in new[] { 0, -1, -5, int.MinValue, LobbyFrames.MaxFrameBytes + 1, int.MaxValue })
            {
                PipeStream raw = rig.JoinRaw();
                Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 3), $"length {length}: not admitted");
                raw.Write(Length(length), 0, 4);
                Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 2), $"length {length}: the guest stayed");
            }
            Check(!rig.Host.IsStopped, "the host stopped");
            Check(!other.IsStopped, "the other guest was closed");
            Check(Until(() => other.Lobby.View().Players.Count == 2), "the other guest's roster did not settle: " + other.Lobby.View().Players.Count);
        }));

        yield return ("J10: a waiting guest flooding hellos cannot get a slower guest taken out of the room", () =>
        {
            long previousMax = TimberServer.MaxLobbyQueuedBytes;
            // A tenth of the real 1 MB, for a slow guest a tenth as slow as a real one might be: the same ratio, in a test's time.
            TimberServer.MaxLobbyQueuedBytes = 100 * 1024;
            try
            {
                WithRoom(rig =>
                {
                    var slow = rig.Join(wrap: s => new PacedStream(s, 20 * 1024, sleepEveryWrite: true));
                    Check(Until(() => slow.Lobby.View().Welcomed, 4000), "not welcomed");
                    PipeStream raw = rig.JoinRaw();
                    Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 3));
                    // Drain what the host sends the attacker, as a real one would to keep its own lane from filling.
                    var drain = Task.Run(() => { var b = new byte[4096]; try { while (raw.Read(b, 0, 1) == 1) { } } catch (Exception) { } });
                    var net = new TestNet();
                    var clock = Stopwatch.StartNew();
                    int sent = 0;
                    bool slowLeftFirst = false;
                    try
                    {
                        while (clock.ElapsedMilliseconds < 1500)
                        {
                            net.Send(raw, CompressionUtils.Compress(LobbyFrames.Hello("local:flood", "Flood" + (sent % 2)).ToString()));
                            if (++sent % 256 == 0 && !slowLeftFirst && !rig.Host.Lobby!.Snapshot().Guests.Any(g => g.Number == 1)) slowLeftFirst = true;
                        }
                    }
                    catch (IOException) { /* the flooder itself was taken out (its own lane overflowed) */ }
                    Thread.Sleep(300);
                    bool slowStayed = !slowLeftFirst && rig.Host.Lobby!.Snapshot().Guests.Any(g => g.Number == 1);
                    bool flooderStayed = rig.Host.Lobby!.Snapshot().Guests.Any(g => g.Number == 2);
                    Console.WriteLine($"    (J10 flood: {sent} hellos in {clock.ElapsedMilliseconds} ms; the slow guest {(slowStayed ? "stayed" : "was taken out")}, the flooder {(flooderStayed ? "stayed" : "was taken out")})");
                    Check(slowStayed, "an honest guest was taken out of the room by another guest's flood of hellos");
                    raw.Close();
                });
            }
            finally { TimberServer.MaxLobbyQueuedBytes = previousMax; }
        });

        yield return ("A-new-3: a guest the room has let go is still gated until its connection closes: no action, no session fault", () =>
        {
            int previousFlush = TimberServer.AbortFlushMs;
            TimberServer.AbortFlushMs = 1500;
            try
            {
                WithRoom(rig =>
                {
                    var stall = new StallingStream();
                    PipeStream raw = rig.JoinRaw(stall.Wrap);
                    Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 2), "not admitted");
                    int number = rig.Host.Lobby!.Snapshot().Guests[0].Number;
                    int faults = 0;
                    rig.Host.OnSessionFault += _ => Interlocked.Increment(ref faults);
                    // It stops reading, so its LobbyEnd waits and RemoveFromLobby closes it only after AbortFlushMs.
                    stall.Stall();
                    Check(rig.Host.RemoveFromLobby(number, LobbyEndReason.Removed, null));
                    var net = new TestNet();
                    net.Send(raw, CompressionUtils.Compress(new JObject { ["type"] = "FakeAction", ["ticksSinceLoad"] = 0 }.ToString()));
                    net.Send(raw, CompressionUtils.Compress(new JObject { ["type"] = "SessionFault", ["reason"] = "x" }.ToString()));
                    Thread.Sleep(400);
                    // beta20: ForgetLobbyMember took it out of lobbyMembers at once, so IsInWaitingRoom was false and both frames
                    // went down the game's path: the action queued as player -1 (every action is allowed in a shared game),
                    // and the fault queued for the next Update, which is the first one of the co-op game this room starts.
                    List<JObject> events = rig.Host.ReadEvents(0);
                    Check(events.Count == 0 && faults == 0, $"a removed guest reached the game: {events.Count} action(s) as player " +
                        $"{events.FirstOrDefault()?["player"]}, {faults} session fault(s), which would end the host's co-op game at its first update");
                });
            }
            finally { TimberServer.AbortFlushMs = previousFlush; }
        });

        // ---- X4: the frame parser against hostile field values ----

        yield return ("X4: hostile numbers and faction fields in waiting-room frames are refused or dropped, never fatal", () =>
        {
            JObject Row(Action<JObject> change)
            {
                var row = new LobbyPlayer(1, "Anna", true, false, false, 2, "IronTeeth").ToJson();
                change(row);
                return new JObject { ["type"] = LobbyFrames.RosterType, ["players"] = new JArray(row) };
            }
            var cases = new[]
            {
                Row(r => r["n"] = long.MaxValue), Row(r => r["colony"] = 4294967296L), Row(r => r["faction"] = "Iron Teeth"),
                Row(r => r["faction"] = new JArray("IronTeeth")), Row(r => r["faction"] = new string('a', 65)),
                Row(r => r["n"] = 1.5), Row(r => r["colony"] = -1), Row(r => r["name"] = new JObject()),
            };
            int threw = 0;
            foreach (JObject frame in cases)
            {
                bool taken;
                // TryParseRoster throws (rather than returning false) for an int out of range; the guest's reader catches it.
                try { taken = LobbyFrames.TryParseRoster(frame, out _); }
                catch (OverflowException) { taken = false; threw++; }
                Check(!taken, "a bad row was taken: " + frame.ToString(Newtonsoft.Json.Formatting.None));
            }
            Console.WriteLine($"    (X4: {threw} of {cases.Length} bad rosters threw OverflowException instead of returning false)");
            // Through a real inbox, as the guest's reader delivers it: nothing is taken and the inbox stays usable.
            var inbox = new LobbyInbox();
            var receive = typeof(LobbyInbox).GetMethod("Receive", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (JObject frame in cases)
            {
                try { receive.Invoke(inbox, new object[] { LobbyFrames.RosterType, frame, 1.0 }); }
                catch (TargetInvocationException e) when (e.InnerException is OverflowException) { }
            }
            Check(inbox.View().Players.Count == 0, "the inbox took a bad roster");
            receive.Invoke(inbox, new object[] { LobbyFrames.RosterType, LobbyFrames.Roster(new[] { new LobbyPlayer(0, "K", true, true, false, 1) }), 2.0 });
            Check(inbox.View().Players.Count == 1, "the inbox stopped working after a frame that threw");
            var summary = new LobbySummary("Folktails", "Map", null, "Town", "Host", true, true, new[] { "Folktails", "IronTeeth" }).ToJson();
            summary["factions"] = new JArray(Enumerable.Range(0, 9).Select(i => "F" + i));
            Check(!LobbySummary.TryParse(summary, out _), "nine factions were taken");
        });

        // ---- J8: exits after Start ----

        yield return ("J8: a guest that leaves after Start is out of the room, the others still get the save, and the host is not held up", () => WithRoom(rig =>
        {
            var a = rig.Join(); var b = rig.Join();
            Check(Until(() => a.Lobby.View().Welcomed && b.Lobby.View().Welcomed));
            byte[]? map = null;
            a.OnMapReceived += bytes => map = bytes;
            rig.Host.CloseLobbyToNewcomers("closed");
            rig.Host.SetLobbyStage(LobbyStage.CreatingWorld);
            b.Close();
            Check(Until(() => rig.Host.Lobby!.Snapshot().Guests.Count == 1), "the leaver stayed in the room");
            rig.Host.SetLobbyStage(LobbyStage.SendingWorld);
            rig.Host.ReleaseLobby(new byte[] { 1, 2, 3 });
            Check(Until(() => { a.Update(); return map != null; }), "the one who stayed got no save");
            Check(Until(() => rig.Host.LobbyGuestsQueued), "the host waits for the one who left");
        }));

        yield return ("J8 premise: a welcome and an end read together, as beta20 reads them, open no page and report no error", () =>
        {
            // What the host sends when it closes the room (or removes the guest) just as the guest came in.
            byte[] welcome = CompressionUtils.Compress(LobbyFrames.Welcome(1, new LobbySummary("Folktails", "Map", null, "Town", "Kyler", true)).ToString());
            byte[] end = CompressionUtils.Compress(LobbyFrames.End(LobbyEndReason.Cancelled, null).ToString());
            var bytes = new List<byte>();
            bytes.AddRange(Length(LobbyFrames.Sentinel)); bytes.AddRange(Length(welcome.Length)); bytes.AddRange(welcome);
            bytes.AddRange(Length(LobbyFrames.Sentinel)); bytes.AddRange(Length(end.Length)); bytes.AddRange(end);
            var (hostSide, guestSide) = PipeStream.Pair();
            hostSide.Write(bytes.ToArray(), 0, bytes.Count);
            var client = new TimberClient(guestSide);
            string? error = null;
            client.OnError += e => error = e;
            try
            {
                client.Start();
                Check(Until(() => client.Lobby.View().Ended));
                hostSide.Close();
                Check(Until(() => { client.Update(); return error != null; }), "no error");
                LobbyView view = client.Lobby.View();
                // LobbyGuestPanel opens only on Welcomed && !Ended, and ClientEventIO says nothing when Ended: so neither
                // speaks, and the Connecting box (closed only by one of them) stays up with no reason given.
                bool pageOpens = view.Welcomed && !view.Ended;
                bool errorShown = !(view.Ended);
                Console.WriteLine($"    (J8: page opens {pageOpens}, join error shown {errorShown}: the guest is told nothing)");
                Check(!pageOpens && !errorShown);
            }
            finally { client.Close(); hostSide.Close(); }
        });
    }

    // ---- helpers ----

    static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    static string Root()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root);
        Check(root != null, "could not find the repository root");
        return root!;
    }

    // The text of a method from its signature to its matching closing brace.
    static string Body(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Check(start >= 0, "no " + signature);
        int open = source.IndexOf('{', start), depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
        }
        throw new Exception("unbalanced " + signature);
    }

    static byte[] Length(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    static void WithRoom(Action<Rig> test)
    {
        int previous = TimberServer.LobbyIntervalMs;
        TimberServer.LobbyIntervalMs = 50;
        var rig = new Rig();
        try { test(rig); }
        finally
        {
            rig.Dispose();
            TimberServer.LobbyIntervalMs = previous;
        }
    }

    sealed class Rig : IDisposable
    {
        readonly RigListener listener = new();
        readonly List<TimberClient> guests = new();
        readonly List<PipeStream> raws = new();
        public TimberServer Host { get; }

        public Rig()
        {
            Host = new TimberServer(listener, () => throw new InvalidOperationException("a waiting room never asks for a map"),
                () => new JObject { [TimberNetBase.TYPE_KEY] = "InitProbe", [TimberNetBase.TICKS_KEY] = 0 })
            { CompatibilityIdentity = "same" };
            Host.OpenLobby(new LobbyRoom(new LobbySummary("Folktails", "Diorama", "NewGameMode.Normal", "Beaverton", "Kyler", true,
                true, new[] { "Folktails", "IronTeeth" })));
            Host.Start();
        }

        public TimberClient Join(Func<ISocketStream, ISocketStream>? wrap = null)
        {
            var (hostSide, guestSide) = PipeStream.Pair();
            listener.Add(wrap == null ? hostSide : wrap(hostSide));
            var guest = new TimberClient(guestSide) { CompatibilityIdentity = "same" };
            guests.Add(guest);
            guest.Start();
            return guest;
        }

        public PipeStream JoinRaw(Func<ISocketStream, ISocketStream>? wrap = null)
        {
            var (hostSide, guestSide) = PipeStream.Pair();
            listener.Add(wrap == null ? hostSide : wrap(hostSide));
            raws.Add(guestSide);
            CompatibilityHandshake.Run(guestSide, "same", false);
            return guestSide;
        }

        public void Dispose()
        {
            Host.Close();
            foreach (var guest in guests) guest.Close();
            foreach (var raw in raws) raw.Close();
        }
    }

    sealed class RigListener : ISocketListener
    {
        readonly BlockingCollection<ISocketStream> pending = new();
        public void Add(ISocketStream stream) => pending.Add(stream);
        public void Start() { }
        public ISocketStream AcceptClient() => pending.Take();
        public void Stop() => pending.CompleteAdding();
    }

    /// <summary>A guest's connection that stops taking data when told: writes wait until it is closed.</summary>
    sealed class StallingStream
    {
        volatile bool stalled;
        public void Stall() => stalled = true;
        public ISocketStream Wrap(ISocketStream inner) => new Stream(inner, this);

        sealed class Stream : ISocketStream
        {
            readonly ISocketStream inner;
            readonly StallingStream owner;
            public Stream(ISocketStream inner, StallingStream owner) { this.inner = inner; this.owner = owner; }
            public bool Connected => inner.Connected;
            public string? Name => inner.Name;
            public int MaxChunkSize => inner.MaxChunkSize;
            public int MaxBytesPerSecond => inner.MaxBytesPerSecond;
            public int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public void Write(byte[] buffer, int offset, int count)
            {
                while (owner.stalled && inner.Connected) Thread.Sleep(10);
                if (!inner.Connected) throw new IOException("closed");
                inner.Write(buffer, offset, count);
            }
            public void Close() => inner.Close();
            public Task ConnectAsync() => inner.ConnectAsync();
        }
    }

    /// <summary>
    /// A host-side stream with a direct link's pacing (32 KB chunks, <paramref name="bytesPerSecond"/>): the paced save
    /// sleeps between chunks as it does over TCP. With sleepEveryWrite, every write also takes its time (a slow reader).
    /// </summary>
    sealed class PacedStream : ISocketStream
    {
        readonly ISocketStream inner;
        readonly int rate;
        readonly bool sleepEveryWrite;
        public PacedStream(ISocketStream inner, int bytesPerSecond, bool sleepEveryWrite = false)
        { this.inner = inner; rate = bytesPerSecond; this.sleepEveryWrite = sleepEveryWrite; }
        public bool Connected => inner.Connected;
        public string? Name => inner.Name;
        public int MaxChunkSize => 32 * 1024;
        public int MaxBytesPerSecond => rate;
        public int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count)
        {
            if (sleepEveryWrite) Thread.Sleep(Math.Max(1, count * 1000 / rate));
            inner.Write(buffer, offset, count);
        }
        public void Close() => inner.Close();
        public Task ConnectAsync() => inner.ConnectAsync();
    }
}
