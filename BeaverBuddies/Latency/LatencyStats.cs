using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Latency
{
    /// <summary>
    /// The numbers behind a guest's delay, for the diagnostics report:
    ///  - how long its own actions take to come back from the host, by game speed;
    ///  - how many the host refused, and how many never came back;
    ///  - how far behind the host it runs, sampled once a second, by game speed;
    ///  - how often, and how long, it waited for the host at the start of a tick (a guest in step with the host waits a
    ///    little at most ticks; long waits mean the host or the network is late).
    /// Bookkeeping only, with no game types, so the checks can run it.
    /// </summary>
    public sealed class LatencyStats
    {
        private const int RecentEchoes = 200;

        private sealed class Echoes
        {
            public int Count;
            public double TotalMs, TotalTicks, MaxMs;
            public readonly Queue<double> RecentMs = new Queue<double>();
        }

        private readonly SortedDictionary<int, Echoes> echoes = new SortedDictionary<int, Echoes>();
        private readonly SortedDictionary<int, int[]> behind = new SortedDictionary<int, int[]>();

        public int Refused { get; private set; }
        public int Unanswered { get; private set; }
        public int Waits { get; private set; }
        public double WaitTotalMs { get; private set; }
        public double WaitMaxMs { get; private set; }

        /// <summary>The speed an action or sample counts under: the chosen speed, to the nearest whole step.</summary>
        public static int SpeedKey(float speed) => Math.Max(0, (int)Math.Round(speed));

        public void Echoed(double ms, int ticks, float speed)
        {
            int key = SpeedKey(speed);
            if (!echoes.TryGetValue(key, out Echoes e)) echoes[key] = e = new Echoes();
            e.Count++;
            e.TotalMs += ms;
            e.TotalTicks += ticks;
            e.MaxMs = Math.Max(e.MaxMs, ms);
            e.RecentMs.Enqueue(ms);
            while (e.RecentMs.Count > RecentEchoes) e.RecentMs.Dequeue();
        }

        public void Refusal() => Refused++;

        public void NoAnswer() => Unanswered++;

        public void SampleBehind(int ticksBehind, float speed)
        {
            int key = SpeedKey(speed);
            if (!behind.TryGetValue(key, out int[] counts)) behind[key] = counts = new int[4];
            counts[Math.Min(3, Math.Max(0, ticksBehind))]++;
        }

        public void Waited(double ms)
        {
            Waits++;
            WaitTotalMs += ms;
            WaitMaxMs = Math.Max(WaitMaxMs, ms);
        }

        /// <summary>The average round trip at a speed, in milliseconds; null if nothing came back at that speed.</summary>
        public double? AverageMs(int speedKey) =>
            echoes.TryGetValue(speedKey, out Echoes e) && e.Count > 0 ? e.TotalMs / e.Count : (double?)null;

        public IEnumerable<string> Lines(double timeoutSeconds)
        {
            if (echoes.Count == 0) yield return "Your actions: none has come back from the host yet";
            foreach (var pair in echoes)
            {
                Echoes e = pair.Value;
                yield return $"Your actions {SpeedName(pair.Key)}, from click to the host's answer: {e.Count}, "
                    + $"average {e.TotalMs / e.Count:0} ms ({e.TotalTicks / e.Count:0.0} ticks), "
                    + $"median of the last {e.RecentMs.Count} {Median(e.RecentMs):0} ms, slowest {e.MaxMs:0} ms";
            }
            if (Refused > 0 || Unanswered > 0)
                yield return $"Refused by the host: {Refused}; no answer within {timeoutSeconds:0} s: {Unanswered}";
            foreach (var pair in behind)
            {
                int[] c = pair.Value;
                int total = c.Sum();
                yield return $"Ticks behind the host {SpeedName(pair.Key)} (once a second, {total} samples): "
                    + $"0: {Percent(c[0], total)}, 1: {Percent(c[1], total)}, 2: {Percent(c[2], total)}, 3 or more: {Percent(c[3], total)}";
            }
            yield return Waits == 0
                ? "Waited for the host at the start of a tick: never"
                : $"Waited for the host at the start of a tick: {Waits} times, average {WaitTotalMs / Waits:0} ms, longest {WaitMaxMs:0} ms";
        }

        private static string SpeedName(int key) => key == 0 ? "while paused" : $"at speed {key}";

        private static string Percent(int count, int total) => total == 0 ? "-" : $"{100.0 * count / total:0}%";

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return 0;
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
    }
}
