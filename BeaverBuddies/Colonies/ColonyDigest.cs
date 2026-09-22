using System;
using System.Text;

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
    ///
    /// The last <see cref="RecentSize"/> changes are kept as well, each with its number and the digest it left, and
    /// every computer logs them when a player desyncs (ClientDesyncedEvent): lined up by number, the first line that
    /// differs between two players' logs is the change they did not make alike. The host logs a tick or more after the
    /// guest, so its list can already have moved past the guest's change count (one mark notes a change per tile).
    /// Nothing kept is hashed or sent.
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

        /// <summary>How many of the last changes are kept for the log.</summary>
        public const int RecentSize = 256;

        /// <summary>A change as it was counted: its number (Changes just after it), what Note was given, and the digest after it.</summary>
        public struct Change
        {
            public int Number;
            public string What;
            public long A, B, C, D;
            public ulong After;
        }

        // A ring, filled once and then overwritten oldest first, so a change allocates nothing.
        private static readonly Change[] recent = new Change[RecentSize];
        private static int recentNext, recentCount;

        /// <summary>A game is loaded: every computer starts from the same value.</summary>
        public static void Reset()
        {
            Value = Seed;
            Changes = 0;
            Array.Clear(recent, 0, RecentSize);
            recentNext = 0;
            recentCount = 0;
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
            recent[recentNext] = new Change { Number = Changes, What = what, A = a, B = b, C = c, D = d, After = h };
            recentNext = recentNext + 1 == RecentSize ? 0 : recentNext + 1;
            if (recentCount < RecentSize) recentCount++;
        }

        /// <summary>The changes kept, oldest first (a copy).</summary>
        public static Change[] Recent()
        {
            var changes = new Change[recentCount];
            int first = recentCount < RecentSize ? 0 : recentNext;
            for (int i = 0; i < recentCount; i++) changes[i] = recent[(first + i) % RecentSize];
            return changes;
        }

        /// <summary>
        /// The changes kept, a line each, oldest first: "#number what a b c d -> digest after". Written the same on
        /// every computer (no culture), so two players' lines can be compared as text.
        /// </summary>
        public static string DescribeRecent()
        {
            Change[] changes = Recent();
            var text = new StringBuilder($"digest {Describe()}; the last {changes.Length} changes counted, oldest first:");
            foreach (Change change in changes)
                text.Append(FormattableString.Invariant(
                    $"\n  #{change.Number} {change.What} {change.A} {change.B} {change.C} {change.D} -> {change.After:x16}"));
            return text.ToString();
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
