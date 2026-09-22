using System;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The arithmetic of a barter at a trading post, kept free of the game so it can be checked headless.
    /// <para>
    /// An exchange is "this many of one item for that many of another", at most <see cref="MaxAmount"/> of each (the room
    /// a Trading Post half has for a good), repeated a number of times (<see cref="MaxRounds"/> at most) or until
    /// cancelled. One side may be 0: a gift one way, or a request.
    /// </para>
    /// <para>
    /// Each round, each colony's beavers bring its goods to its own half, where they wait. Nothing crosses until both
    /// sides are in; then both sides cross at once, science passes and beavers move. So nothing is ever given without
    /// what it was exchanged for, and a round ended early leaves every good on its own colony's half.
    /// </para>
    /// </summary>
    public static class ExchangeTerms
    {
        /// <summary>Most of an item one side gives in one round.</summary>
        public const int MaxAmount = 100;
        /// <summary>Most rounds an exchange is agreed for (besides "until cancelled").</summary>
        public const int MaxRounds = 99;

        /// <summary>Science points as an exchange item: moved from pool to pool, with separate science.</summary>
        public const string Science = "BeaverBuddies.Science";
        /// <summary>Adult beavers as an exchange item: they move to the other colony's district.</summary>
        public const string Beavers = "BeaverBuddies.Beavers";

        /// <summary>Science and beavers are not carried: they move when the round's goods have crossed.</summary>
        public static bool IsSpecial(string item) => item == Science || item == Beavers;

        /// <summary>
        /// Amounts from 0 to <see cref="MaxAmount"/>, not both 0; a side with an amount names its item; two sides that
        /// both give something give different items.
        /// </summary>
        public static bool AreValid(string giveGood, int giveAmount, string getGood, int getAmount)
        {
            if (giveAmount < 0 || getAmount < 0 || giveAmount > MaxAmount || getAmount > MaxAmount) return false;
            if (giveAmount == 0 && getAmount == 0) return false;
            if (giveAmount > 0 && string.IsNullOrEmpty(giveGood)) return false;
            if (getAmount > 0 && string.IsNullOrEmpty(getGood)) return false;
            return giveAmount == 0 || getAmount == 0 || !string.Equals(giveGood, getGood, StringComparison.Ordinal);
        }

        /// <summary>A number of rounds an exchange can be agreed for (a repeating one ignores it).</summary>
        public static bool AreValidRounds(int rounds) => rounds >= 1 && rounds <= MaxRounds;

        /// <summary>The good a side gives, or null for a side that gives nothing.</summary>
        public static string GoodOf(string good, int amount) => amount > 0 ? good : null;

        /// <summary>
        /// How many more of its good a side's beavers should set out to bring this round: what is still missing on its
        /// half, less what is already on the way.
        /// </summary>
        public static int StillToBring(int total, int held, int onTheWay) => Math.Max(0, total - Math.Max(0, held) - Math.Max(0, onTheWay));

        /// <summary>How much of a load arriving on a side's half is held for the round (the rest is carried home).</summary>
        public static int ToHold(int total, int held, int arriving) => Math.Max(0, Math.Min(arriving, total - held));

        /// <summary>A goods side is in once all of it waits on its half.</summary>
        public static bool IsDelivered(int total, int held) => held >= total;

        /// <summary>After a round crossed, whether another one starts.</summary>
        public static bool HasAnotherRound(int rounds, int done, bool repeat) => repeat || done < rounds;

        /// <summary>
        /// Beavers a district can give: its adults able to move (not contaminated), as long as one adult always stays.
        /// </summary>
        public static int BeaversToSpare(int adults, int movableAdults) => Math.Max(0, Math.Min(movableAdults, adults - 1));
    }
}
