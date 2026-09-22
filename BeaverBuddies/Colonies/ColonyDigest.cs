using System;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A running 64-bit digest of every change to colony state: ownership, marks, science, exchanges, the ledger, land,
    /// absence, working hours. Colony code draws no random numbers, so a colony state that differed between two
    /// computers showed only once it changed some beaver's random draw, possibly days later and far from its cause,
    /// or never. The host writes this digest into every heartbeat, and a guest whose own differs at that tick stops
    /// at once, with the number of changes each side counted in the log.
    ///
    /// Only changes made inside the simulation (a tick, or a replayed action) are counted, on every computer alike;
    /// loading and display never touch it. It is order-sensitive on purpose: two computers that made the same changes
    /// in a different order have already diverged. The mixing is plain .NET so the headless checks can run it.
    /// </summary>
    public static class ColonyDigest
    {
        private const ulong Seed = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <summary>Whether a change made now counts. The mod sets it to "in a tick or a replay, once loaded".</summary>
        public static Func<bool> Gate = () => true;

        public static ulong Value { get; private set; } = Seed;

        /// <summary>How many changes were counted since the last reset (to tell "one missing" from "different").</summary>
        public static int Changes { get; private set; }

        /// <summary>A game is loaded: every computer starts from the same value.</summary>
        public static void Reset()
        {
            Value = Seed;
            Changes = 0;
        }

        /// <summary>A change to colony state: what it was, and the numbers that describe it.</summary>
        public static void Note(string what, long a = 0, long b = 0, long c = 0, long d = 0)
        {
            if (!Gate()) return;
            Changes++;
            ulong h = Value;
            foreach (char ch in what) h = Mix(h, ch);
            h = Mix(h, (ulong)a);
            h = Mix(h, (ulong)b);
            h = Mix(h, (ulong)c);
            h = Mix(h, (ulong)d);
            Value = h;
        }

        /// <summary>A stable number for a name (string.GetHashCode may differ between runtimes).</summary>
        public static long Of(string text)
        {
            if (text == null) return 0;
            ulong h = Seed;
            foreach (char ch in text) h = Mix(h, ch);
            return (long)h;
        }

        private static ulong Mix(ulong h, ulong x) => (h ^ x) * Prime;

        public static string Describe() => $"{Value:x16}/{Changes}";
    }
}
