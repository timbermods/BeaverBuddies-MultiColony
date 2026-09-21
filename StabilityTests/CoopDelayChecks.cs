using System.Reflection;
using BeaverBuddies.Latency;
using TimberNet;

static class CoopDelayChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, got {actual}");

    // Records every write, as the socket would see it.
    sealed class RecordingStream : ISocketStream
    {
        public readonly List<byte[]> Writes = new();
        public bool Connected => true;
        public string Name => "recording";
        public int MaxChunkSize => 64;
        public int MaxBytesPerSecond => int.MaxValue;
        public Task ConnectAsync() => Task.CompletedTask;
        public int Read(byte[] buffer, int offset, int count) => 0;
        public void Write(byte[] buffer, int offset, int count)
        {
            if (count > MaxChunkSize) throw new ArgumentException($"write of {count} is over the chunk size");
            Writes.Add(buffer.Skip(offset).Take(count).ToArray());
        }
        public void Close() { }
    }

    static List<byte[]> Send(byte[] data)
    {
        var stream = new RecordingStream();
        var net = new TimberClient(stream);
        typeof(TimberNetBase).GetMethod("SendDataWithLength", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(net, new object[] { stream, data });
        return stream.Writes;
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("Framing: the length and the start of a message go out in one write, within the chunk size", () =>
        {
            foreach (int size in new[] { 0, 1, 59, 60, 61, 64, 200, 64 * 3 + 7 })
            {
                byte[] data = Enumerable.Range(0, size).Select(i => (byte)(i * 7 + 3)).ToArray();
                var writes = Send(data);
                byte[] all = writes.SelectMany(w => w).ToArray();
                Equal(4 + size, all.Length);
                // Big-endian length first, as the reader expects.
                Equal(size, (all[0] << 24) | (all[1] << 16) | (all[2] << 8) | all[3]);
                Check(all.Skip(4).SequenceEqual(data), $"payload of {size} bytes changed");
                Check(writes[0].Length == Math.Min(4 + size, 64), $"first write for {size} bytes was {writes[0].Length}");
                Check(writes.All(w => w.Length <= 64), "a write went over the chunk size");
                Equal(size <= 60 ? 1 : 1 + (size - 60 + 63) / 64, writes.Count);
            }
        });
        yield return ("Delay report: round trips are averaged per speed, with the median and slowest", () =>
        {
            var stats = new LatencyStats();
            stats.Echoed(100, 0, 1); stats.Echoed(300, 1, 1); stats.Echoed(800, 2, 1);
            stats.Echoed(50, 0, 3);
            Equal(400.0, stats.AverageMs(1)!.Value);
            Equal(50.0, stats.AverageMs(3)!.Value);
            Check(stats.AverageMs(7) == null, "nothing at speed 7");
            var lines = stats.Lines(8).ToList();
            Check(lines.Any(l => l.Contains("at speed 1") && l.Contains(": 3,") && l.Contains("average 400 ms (1.0 ticks)")
                && l.Contains("median of the last 3 300 ms") && l.Contains("slowest 800 ms")), string.Join(" | ", lines));
            Check(lines.Any(l => l.Contains("at speed 3") && l.Contains("average 50 ms")), string.Join(" | ", lines));
        });
        yield return ("Delay report: refusals, unanswered actions, lag samples and waits are counted", () =>
        {
            var stats = new LatencyStats();
            Check(stats.Lines(8).First().Contains("none has come back"), "an empty report says so");
            stats.Refusal(); stats.NoAnswer(); stats.NoAnswer();
            foreach (int behind in new[] { 0, 0, 0, 1, 2, 5 }) stats.SampleBehind(behind, 1);
            stats.SampleBehind(0, 0.6f);   // counted under speed 1
            stats.Waited(10); stats.Waited(30);
            var lines = stats.Lines(8).ToList();
            Check(lines.Any(l => l == "Refused by the host: 1; no answer within 8 s: 2"), string.Join(" | ", lines));
            Check(lines.Any(l => l.Contains("Ticks behind the host at speed 1 (once a second, 7 samples): 0: 57%, 1: 14%, 2: 14%, 3 or more: 14%")),
                string.Join(" | ", lines));
            Check(lines.Any(l => l == "Waited for the host at the start of a tick: 2 times, average 20 ms, longest 30 ms"), string.Join(" | ", lines));
            Equal(0, LatencyStats.SpeedKey(-1)); Equal(0, LatencyStats.SpeedKey(0)); Equal(7, LatencyStats.SpeedKey(7));
        });
        yield return ("Delay report: only the last 200 round trips make the median", () =>
        {
            var stats = new LatencyStats();
            for (int i = 0; i < 300; i++) stats.Echoed(i < 100 ? 10000 : 100, 0, 1);
            string line = stats.Lines(8).First();
            Check(line.Contains("median of the last 200 100 ms") && line.Contains("slowest 10000 ms"), line);
        });
    }
}
