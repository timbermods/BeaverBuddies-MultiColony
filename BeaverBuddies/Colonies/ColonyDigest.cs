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
    /// The last <see cref="RecentSize"/> changes are kept as well, each with its number and the digest it left. A guest
    /// notes how many changes it had counted when the host's digest last matched its own (<see cref="Agreed"/>); the
    /// change that differed comes after that. When a player desyncs (ClientDesyncedEvent, which carries that count),
    /// every computer logs its changes from the next one on: lined up by number, the first line that differs between
    /// two players' logs is the change they did not make alike. One tick can count thousands of changes (a mark notes
    /// one per tile), hence the size; the log says so if the first change that could differ is no longer kept.
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

        /// <summary>How many of the last changes are kept for the log (about 900 KB, made once).</summary>
        public const int RecentSize = 16384;

        /// <summary>
        /// A guest: how many changes it had counted when the host's digest last matched its own (0 from load). The host
        /// compares nothing and keeps 0.
        /// </summary>
        public static int Agreed { get; private set; }

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
            Agreed = 0;
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

        /// <summary>A guest whose digest matched the host's: every change counted so far was made alike.</summary>
        public static void NoteAgreed() => Agreed = Changes;

        /// <summary>The changes kept, oldest first (a copy).</summary>
        public static Change[] Recent() => Since(0);

        /// <summary>The changes kept that come after change number <paramref name="after"/>, oldest first (a copy).</summary>
        public static Change[] Since(int after)
        {
            int first = recentCount < RecentSize ? 0 : recentNext;
            // The kept changes are numbered one after another, the oldest Changes - recentCount + 1.
            int skip = Math.Min(recentCount, Math.Max(0, after - (Changes - recentCount)));
            var changes = new Change[recentCount - skip];
            for (int i = 0; i < changes.Length; i++) changes[i] = recent[(first + skip + i) % RecentSize];
            return changes;
        }

        /// <summary>
        /// The changes kept after change number <paramref name="agreed"/> (the last count the colony checks agreed on),
        /// a line each, oldest first: "#number what a b c d -> digest after". <paramref name="hostChanges"/>, when
        /// known, is how many the host had counted at the check that differed: a line marks where that was. Written the
        /// same on every computer (no culture), so two players' lines can be compared as text.
        /// </summary>
        public static string DescribeSince(int agreed, int? hostChanges = null)
        {
            Change[] changes = Since(agreed);
            var text = new StringBuilder(FormattableString.Invariant(
                $"digest {Describe()}; the changes after #{agreed}, the last count the colony checks agreed on"));
            if (hostChanges != null) text.Append(FormattableString.Invariant($" (the host's check that differed: #{hostChanges})"));
            text.Append(", oldest first:");
            int oldest = Changes - recentCount + 1;
            if (Changes > agreed && oldest > agreed + 1)
                text.Append(FormattableString.Invariant(
                    $"\n  (#{agreed + 1} to #{oldest - 1} are no longer kept: more than {RecentSize} changes were counted since)"));
            if (changes.Length == 0) text.Append("\n  (none)");
            foreach (Change change in changes)
            {
                text.Append(FormattableString.Invariant(
                    $"\n  #{change.Number} {change.What} {change.A} {change.B} {change.C} {change.D} -> {change.After:x16}"));
                if (change.Number == hostChanges) text.Append("\n  (the host's check that differed came here)");
            }
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
