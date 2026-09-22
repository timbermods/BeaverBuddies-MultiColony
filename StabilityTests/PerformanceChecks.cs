using System.Diagnostics;
using System.Reflection;
using BeaverBuddies.Colonies;
using Newtonsoft.Json.Linq;
using TimberNet;

static class PerformanceChecks
{
    static void Check(bool condition) { if (!condition) throw new Exception("Performance regression changed event semantics"); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, got {actual}");
    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("Binary insertion preserves stable order with duplicate and out-of-order ticks", () =>
        {
            var net = new InsertNet(); var expected = new List<JObject>(); var actual = new List<JObject>();
            var random = new Random(41);
            for(int i=0;i<1000;i++)
            {
                var item = new JObject { [TimberNetBase.TICKS_KEY] = random.Next(30), ["id"] = i };
                int index = expected.FindIndex(e => TimberNetBase.GetTick(e) > TimberNetBase.GetTick(item));
                if(index < 0) expected.Add(item); else expected.Insert(index,item);
                net.Insert(item,actual);
            }
            Check(expected.SequenceEqual(actual));
        });
        yield return ("Bulk dequeue preserves due events and leaves future backlog untouched", () =>
        {
            foreach(int cutoff in new[] {-1,0,4,9,10})
            {
                var items = Enumerable.Range(0,1000).Select(i => i/100).ToList();
                var expected = items.Where(t=>t<=cutoff).ToList();
                var future = items.Where(t=>t>cutoff).ToList();
                var ready = TimberNetBase.PopEventsForTick(cutoff,items,x=>x);
                Check(ready.SequenceEqual(expected) && items.SequenceEqual(future));
            }
        });
        yield return ("Incoming queue retains parsed messages without reparsing", () =>
        {
            var queue = typeof(TimberNetBase).GetField("receivedEventQueue",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(new InsertNet())!;
            Check(queue.GetType().GetGenericArguments().Single()==typeof(JObject));
        });
        yield return ("Routine event logging is off but detailed logging can be enabled", () =>
        {
            var net = new InsertNet(); int logs=0; net.OnLog += _ => logs++;
            var item = new JObject { [TimberNetBase.TICKS_KEY]=1, [TimberNetBase.TYPE_KEY]="Heartbeat" };
            net.DoUserInitiatedEvent(item); Check(logs==0);
            net.DetailedLoggingEnabled=()=>true;
            net.DoUserInitiatedEvent(item); Check(logs==1);
        });
        yield return ("Synthetic ordered-backlog benchmark keeps identical results", () =>
        {
            var net = new InsertNet();
            var values = Enumerable.Range(0,4000).Select(i => new JObject{[TimberNetBase.TICKS_KEY]=i}).ToArray();
            var old = new List<JObject>(); var current = new List<JObject>();
            var clock=Stopwatch.StartNew();
            foreach(var item in values)
            {
                int tick=TimberNetBase.GetTick(item);
                int index=old.FindIndex(e=>TimberNetBase.GetTick(e)>tick);
                if(index<0) old.Add(item); else old.Insert(index,item);
            }
            double before=clock.Elapsed.TotalMilliseconds; clock.Restart();
            foreach(var item in values) net.Insert(item,current);
            double after=clock.Elapsed.TotalMilliseconds;
            Check(old.SequenceEqual(current));
            Console.WriteLine($"  Ordered backlog: old {before:F2} ms, optimized {after:F2} ms for 4,000 events (synthetic, not game FPS)");
        });
        yield return ("An event sent as text hashes as one sent parsed", () =>
        {
            // The game sends each event as the text it serialized, with its type and tick; the tests, and the host's
            // own heartbeat, send parsed objects. Both must join the running hash the same way.
            var parsed = new InsertNet(); var asText = new InsertNet();
            var item = new JObject { [TimberNetBase.TICKS_KEY] = 3, [TimberNetBase.TYPE_KEY] = "Probe", ["n"] = 1, ["data"] = "a\"b\\c é" };
            parsed.DoUserInitiatedEvent(item);
            asText.DoUserInitiatedEvent(item.ToString(Newtonsoft.Json.Formatting.None), "Probe", 3);
            Equal(parsed.Hash, asText.Hash);
            Check(parsed.Hash != 17, "nothing was hashed");
        });
        yield return ("A guest's hash is taken from the bytes it received, and matches the host's once every event is read", () =>
        {
            using var s = new ActivityTransportChecks.Session(1);
            TimberClient guest = s.Guests[0];
            Check(SpinWait.SpinUntil(() => guest.HasEventsForTick(0), 2000), "init event never arrived");
            for (int i = 0; i < 20; i++)
                s.Host.DoUserInitiatedEvent(new JObject { [TimberNetBase.TYPE_KEY] = "Seq", [TimberNetBase.TICKS_KEY] = 0, ["n"] = i, ["data"] = new string('x', i * 37) + "é" });
            var received = new List<int>();
            Check(SpinWait.SpinUntil(() =>
            {
                foreach (var e in guest.ReadEvents(0)) if ((string?)e["type"] == "Seq") received.Add((int)e["n"]!);
                return received.Count == 20;
            }, 4000), "received " + received.Count);
            Equal(s.Host.Hash, guest.Hash);
            Check(guest.Hash != 17, "the guest hashed nothing");
        });
        yield return ("Colony: a profiler spot is timed without a lookup or a lock, and the report sees every call", () =>
        {
            ColonyProfiler.Spot spot = ColonyProfiler.Declare("check spot");
            Check(ReferenceEquals(spot, ColonyProfiler.Declare("check spot")), "one name, one spot");
            ColonyProfiler.Reset();
            for (int i = 0; i < 1000; i++) ColonyProfiler.Stop(spot, ColonyProfiler.Start());
            var row = ColonyProfiler.Snapshot().Single(r => r.name == "check spot");
            Check(row.calls == 1000 && row.totalMs >= 0 && row.maxMs >= row.totalMs / 1000, $"{row.calls} calls, {row.totalMs} ms, longest {row.maxMs} ms");
            ColonyProfiler.Reset();
            Check(!ColonyProfiler.Snapshot().Any(r => r.name == "check spot"), "a reset spot is left out of the report");
            // Against what it replaced: a global lock and a dictionary keyed by the spot's name, on every call, on
            // paths such as every beaver's working-hours check every tick.
            var totals = new Dictionary<string, long[]>(); var gate = new object();
            void Old(string name, long start)
            {
                long elapsed = Stopwatch.GetTimestamp() - start;
                lock (gate)
                {
                    if (!totals.TryGetValue(name, out long[]? entry)) totals[name] = entry = new long[3];
                    entry[0]++; entry[1] += elapsed; if (elapsed > entry[2]) entry[2] = elapsed;
                }
            }
            const int n = 2_000_000; const string name = "Working hours checks";
            for (int i = 0; i < 20000; i++) { Old(name, ColonyProfiler.Start()); ColonyProfiler.Stop(spot, ColonyProfiler.Start()); }
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) Old(name, ColonyProfiler.Start());
            double before = clock.Elapsed.TotalMilliseconds; clock.Restart();
            for (int i = 0; i < n; i++) ColonyProfiler.Stop(spot, ColonyProfiler.Start());
            double after = clock.Elapsed.TotalMilliseconds;
            Console.WriteLine($"      Profiler: {n:N0} timed calls took {before:F0} ms keyed by name under a lock, {after:F0} ms as a spot");
            Check(after < before, $"a spot ({after:F0} ms) should cost less than a name lookup under a lock ({before:F0} ms)");
            ColonyProfiler.Reset();
        });
        yield return ("Activity: an empty mailbox hands out the same empty array every frame, and a full one still delivers", () =>
        {
            var mailbox = new ActivityMailbox();
            Check(ReferenceEquals(mailbox.Take(0), mailbox.Take(0)) && mailbox.Take(0).Length == 0, "an empty take should cost nothing");
            mailbox.Put(new PlayerActivity(1, "A", "112233", true, 1, 2, 3), 10);
            mailbox.Put(new PlayerActivity(2, "B", "445566", false, 0, 0, 0), 10);
            var taken = mailbox.Take(11);
            Check(taken.Length == 2 && taken.Any(a => a.PlayerId == 1 && a.X == 1) && taken.Any(a => a.PlayerId == 2), "both frames delivered");
            Equal(0, mailbox.Take(11).Length);
            // A frame older than a player's lifetime is dropped.
            mailbox.Put(new PlayerActivity(3, "C", "778899", true, 0, 0, 0), 0);
            Equal(0, mailbox.Take(PlayerActivity.LifetimeSeconds + 1).Length);
        });
        yield return ("Activity: a frame that changed nothing is the same frame; any change is not", () =>
        {
            var a = new PlayerActivity(0, "Kyler", "FFAA00", true, 1.5f, 2, 3, "", "");
            Check(a.SameAs(new PlayerActivity(0, "Kyler", "FFAA00", true, 1.5f, 2, 3, "", "")), "the same frame");
            Check(!a.SameAs(null), "nothing sent yet");
            Check(!a.SameAs(new PlayerActivity(0, "Kyler", "FFAA00", true, 1.5f, 2, 3.001f, "", "")), "the cursor moved");
            Check(!a.SameAs(new PlayerActivity(0, "Kyler", "FFAA00", false, 1.5f, 2, 3, "", "")), "the cursor hid");
            Check(!a.SameAs(new PlayerActivity(0, "Kyler", "FFAA01", true, 1.5f, 2, 3, "", "")), "the colour changed");
            Check(!a.SameAs(new PlayerActivity(0, "Kyler", "FFAA00", true, 1.5f, 2, 3, Guid.NewGuid().ToString("D"), "")), "something was selected");
            Check(!a.SameAs(new PlayerActivity(1, "Kyler", "FFAA00", true, 1.5f, 2, 3, "", "")), "another player");
        });
    }
    sealed class InsertNet : TimberNetBase
    {
        public void Insert(JObject value,List<JObject> list)=>InsertInScript(value,list);
    }
}
