using System.Collections.Generic;
using System.Diagnostics;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Times the colony code's busy spots for the diagnostics report. A spot is declared once, as a static field, and
    /// timing it is two timestamps and three additions: no lookup by name and no lock, which is what it was before
    /// (a global lock and a string-keyed dictionary on every call, on paths such as every beaver's working-hours check
    /// every tick). Every spot is timed on the game thread, where the patches and ticks that use them run, and the
    /// report reads them there too. Plain .NET, so the headless checks can run it.
    /// </summary>
    public static class ColonyProfiler
    {
        public sealed class Spot
        {
            public readonly string Name;
            internal readonly bool Sampled;
            internal long Calls, Total, Longest;
            internal Spot(string name, bool sampled) { Name = name; Sampled = sampled; }
        }

        private static readonly List<Spot> spots = new List<Spot>();

        /// <summary>Declares a spot (once, as a static field). Two declarations of one name share the spot.</summary>
        public static Spot Declare(string name) => Declare(name, sampled: false);

        /// <summary>
        /// Declares a spot on a path called so often that two timestamps a call cost about as much as the work (every
        /// beaver's working-hours check every tick, every walker's animation every frame): one call in
        /// <see cref="SampleEvery"/> is timed (StartSampled, StopSampled) and every call counted; the report scales the
        /// timed ones up and marks the row (1.4.0-rc1 review, D-S1).
        /// </summary>
        public static Spot DeclareSampled(string name) => Declare(name, sampled: true);

        private static Spot Declare(string name, bool sampled)
        {
            lock (spots)
            {
                foreach (Spot spot in spots)
                {
                    if (spot.Name == name) return spot;
                }
                Spot made = new Spot(name, sampled);
                spots.Add(made);
                return made;
            }
        }

        public static long Start() => Stopwatch.GetTimestamp();

        public static void Stop(Spot spot, long start)
        {
            long elapsed = Stopwatch.GetTimestamp() - start;
            spot.Calls++;
            spot.Total += elapsed;
            if (elapsed > spot.Longest) spot.Longest = elapsed;
        }

        /// <summary>One call in this many to a sampled spot is timed.</summary>
        public const int SampleEvery = 16;

        /// <summary>Starts a call to a sampled spot: a timestamp for one call in <see cref="SampleEvery"/>, else 0 (not timed).</summary>
        public static long StartSampled(Spot spot) => ((spot.Calls + 1) & (SampleEvery - 1)) == 0 ? Stopwatch.GetTimestamp() : 0;

        /// <summary>Ends a call to a sampled spot: counted always, timed if its start was.</summary>
        public static void StopSampled(Spot spot, long start)
        {
            spot.Calls++;
            if (start == 0) return;
            long elapsed = Stopwatch.GetTimestamp() - start;
            spot.Total += elapsed * SampleEvery;
            if (elapsed > spot.Longest) spot.Longest = elapsed;
        }

        /// <summary>
        /// Name, calls, total ms, largest single call in ms; most time first. Spots never reached are left out. A sampled
        /// spot's name ends "(~)": its total is its timed calls scaled up, and its largest is the largest timed.
        /// </summary>
        public static List<(string name, long calls, double totalMs, double maxMs)> Snapshot()
        {
            double toMs = 1000.0 / Stopwatch.Frequency;
            var result = new List<(string name, long calls, double totalMs, double maxMs)>();
            lock (spots)
            {
                foreach (Spot spot in spots)
                {
                    if (spot.Calls > 0) result.Add((spot.Sampled ? spot.Name + " (~)" : spot.Name, spot.Calls, spot.Total * toMs, spot.Longest * toMs));
                }
            }
            result.Sort((a, b) => b.totalMs.CompareTo(a.totalMs));
            return result;
        }

        /// <summary>A game is loaded: the counts start again (the spots stay declared).</summary>
        public static void Reset()
        {
            lock (spots)
            {
                foreach (Spot spot in spots)
                {
                    spot.Calls = 0;
                    spot.Total = 0;
                    spot.Longest = 0;
                }
            }
        }
    }
}
