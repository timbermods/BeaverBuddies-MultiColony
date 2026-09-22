#nullable enable
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using TimberNet;

// 1.4.0-beta12: the host writes to a direct (TCP) guest from that guest's own send lane, never from its game thread, and
// every connection is read by a thread of its own above normal priority. A guest that stopped reading used to stop the
// host's tick broadcast, and every other guest with it, until its connection gave up.
static class SendLaneChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }

    sealed class LoopbackListener : ISocketListener
    {
        public readonly TcpListener Listener = new TcpListener(IPAddress.Loopback, 0);
        public int Port => ((IPEndPoint)Listener.LocalEndpoint).Port;
        public void Start() { }
        public ISocketStream AcceptClient() => new TCPClientWrapper(Listener.AcceptTcpClient());
        public void Stop() => Listener.Stop();
    }

    // A stream that remembers which thread reads it.
    sealed class WatchedStream : ISocketStream
    {
        readonly ISocketStream inner;
        public volatile bool ReadOnPool = true;
        public ThreadPriority ReadPriority = ThreadPriority.Lowest;
        public volatile bool WasRead;
        public WatchedStream(ISocketStream inner) => this.inner = inner;
        public bool Connected => inner.Connected;
        public string? Name => inner.Name;
        public int MaxChunkSize => inner.MaxChunkSize;
        public int MaxBytesPerSecond => inner.MaxBytesPerSecond;
        public Task ConnectAsync() => inner.ConnectAsync();
        public int Read(byte[] buffer, int offset, int count)
        {
            ReadOnPool = Thread.CurrentThread.IsThreadPoolThread;
            ReadPriority = Thread.CurrentThread.Priority;
            WasRead = true;
            return inner.Read(buffer, offset, count);
        }
        public void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public void Close() => inner.Close();
    }

    // A frame of about 4 KB that gzip cannot shrink much, so a stalled socket fills quickly.
    static string Frame(int tick, Random random)
    {
        var noise = new byte[3000];
        random.NextBytes(noise);
        return new JObject
        {
            [TimberNetBase.TYPE_KEY] = "Test",
            [TimberNetBase.TICKS_KEY] = tick,
            ["noise"] = Convert.ToBase64String(noise),
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("Send lane: frames are written in the order posted, and posting never waits for the network", () =>
        {
            using var release = new ManualResetEventSlim(false);
            var written = new List<int>();
            var lane = new SendLane("test", (wire, type, tick) => { release.Wait(); lock (written) written.Add(tick); });
            var clock = Stopwatch.StartNew();
            for (int i = 1; i <= 50; i++) lane.Post(new byte[100], "Test", i);
            Check(clock.ElapsedMilliseconds < 200, "posting waited for a write that could not go out");
            release.Set();
            Check(lane.WaitUntilEmpty(3000), "the lane never wrote what was posted");
            lock (written) Check(written.SequenceEqual(Enumerable.Range(1, 50)), "the frames went out of order");
            lane.Close();
        });

        yield return ("Send lane: a guest stuck on one write past the limit, or with too much waiting, is stalled; a busy one is not", () =>
        {
            using var release = new ManualResetEventSlim(false);
            var lane = new SendLane("test", (wire, type, tick) => release.Wait());
            lane.Post(new byte[10], "Test", 1);
            Check(SpinWait.SpinUntil(() => lane.IsStalled(SendLane.NowMs + 20_000, 10_000, long.MaxValue), 1000), "the write never started");
            Check(!lane.IsStalled(SendLane.NowMs, 10_000, 1_000_000), "a write under way for moments counted as stalled");
            Check(lane.IsStalled(SendLane.NowMs + 20_000, 10_000, 1_000_000), "a write stuck past the limit did not count as stalled");
            for (int i = 0; i < 20; i++) lane.Post(new byte[1000], "Test", i);
            Check(lane.IsStalled(SendLane.NowMs, 10_000, 5_000), "20 KB waiting past a 5 KB limit did not count as stalled");
            Check(!lane.WaitUntilEmpty(100), "a lane whose guest takes nothing claimed to be empty");
            lane.Close();
            release.Set();
            Check(lane.WaitUntilEmpty(1000), "a closed lane still waited");
        });

        yield return ("Direct TCP: a guest that stops reading no longer stops the host's broadcasts, and is dropped after the limit", () =>
        {
            int limitBefore = TimberServer.SendStallLimitMs;
            TimberServer.SendStallLimitMs = 1500;
            var listener = new LoopbackListener();
            listener.Listener.Start();
            var host = new TimberServer(listener, () => Task.FromResult(new byte[] { 1, 2, 3 }), null);
            TimberClient? guest = null;
            TcpClient? stalled = null;
            try
            {
                host.Start();
                // A guest that plays along.
                guest = new TimberClient(new TCPClientWrapper("127.0.0.1", listener.Port));
                bool map = false;
                guest.OnMapReceived += _ => map = true;
                guest.Start();
                Check(SpinWait.SpinUntil(() => { host.Update(); guest.Update(); return map; }, 5000), "the guest never got the save");
                // A guest that joins and then reads nothing more: a frozen game, or a link that died without a word.
                stalled = new TcpClient { ReceiveBufferSize = 4096, NoDelay = true };
                stalled.Connect(IPAddress.Loopback, listener.Port);
                Check(SpinWait.SpinUntil(() => { host.Update(); return host.ClientCount == 2; }, 5000), "the second guest was never admitted");
                Thread.Sleep(300); // its join finishes and its lane opens

                // The host's game thread broadcasts 2 MB, far more than both sockets' buffers hold.
                var random = new Random(12);
                var frames = Enumerable.Range(1, 500).Select(tick => Frame(tick, random)).ToList();
                long worstMs = 0;
                var broadcast = new Thread(() =>
                {
                    for (int tick = 1; tick <= frames.Count; tick++)
                    {
                        var one = Stopwatch.StartNew();
                        host.DoUserInitiatedEvent(frames[tick - 1], "Test", tick);
                        worstMs = Math.Max(worstMs, one.ElapsedMilliseconds);
                    }
                }) { IsBackground = true };
                broadcast.Start();
                Check(broadcast.Join(10_000), "the host's broadcast waited for the guest that stopped reading");
                Check(worstMs < 500, $"one broadcast took {worstMs} ms");
                Check(SpinWait.SpinUntil(() => guest.HasEventsForTick(500), 10_000), "the guest that reads never got the last frame");

                // Past the limit the next broadcast drops the stalled guest, and the one after forgets it.
                Thread.Sleep(TimberServer.SendStallLimitMs + 300);
                host.DoUserInitiatedEvent(Frame(501, random), "Test", 501);
                host.DoUserInitiatedEvent(Frame(502, random), "Test", 502);
                Check(host.ClientCount == 1, $"the stalled guest was not dropped ({host.ClientCount} guests)");
                Check(SpinWait.SpinUntil(() => guest.HasEventsForTick(502), 5000), "the guest that reads stopped getting frames");
            }
            finally
            {
                TimberServer.SendStallLimitMs = limitBefore;
                try { stalled?.Close(); } catch { }
                guest?.Close();
                host.Close();
            }
        });

        yield return ("Direct TCP: host and guest read their connections on threads of their own, above normal priority", () =>
        {
            var (hostSide, guestSide) = PipeStream.Pair();
            var hostStream = new WatchedStream(hostSide);
            var guestStream = new WatchedStream(guestSide);
            var host = new TimberServer(new PipeListener(hostStream), () => Task.FromResult(new byte[] { 4, 5 }), null);
            var guest = new TimberClient(guestStream);
            try
            {
                bool map = false;
                guest.OnMapReceived += _ => map = true;
                host.Start(); guest.Start();
                Check(SpinWait.SpinUntil(() => { host.Update(); guest.Update(); return map && hostStream.WasRead; }, 5000), "the join never finished");
                Check(!guestStream.ReadOnPool, "the guest reads the host on a thread-pool thread");
                Check(!hostStream.ReadOnPool, "the host reads its guest on a thread-pool thread");
                if (OperatingSystem.IsWindows())
                {
                    Check(guestStream.ReadPriority == ThreadPriority.AboveNormal, $"the guest reads at {guestStream.ReadPriority} priority");
                    Check(hostStream.ReadPriority == ThreadPriority.AboveNormal, $"the host reads at {hostStream.ReadPriority} priority");
                }
            }
            finally { guest.Close(); host.Close(); }
        });
    }
}
