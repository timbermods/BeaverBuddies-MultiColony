using System;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The arithmetic of a barter at a trading post, kept free of the game so it can be checked headless. Each colony
    /// gives a number of one good ("1000 logs for 250 gears"); one side may be 0, which makes it a gift ("1000 logs for
    /// nothing") or a request. The goods move in step: neither colony's beavers bring more than a tenth of their side
    /// (at least 10) ahead of what the other colony has delivered, so an exchange cannot end up filled one way only.
    /// </summary>
    public static class ExchangeTerms
    {
        public const int MaxAmount = 9999;

        /// <summary>Science points as an exchange item: moved from pool to pool, with separate science.</summary>
        public const string Science = "BeaverBuddies.Science";
        /// <summary>Adult beavers as an exchange item: they move to the other colony's district.</summary>
        public const string Beavers = "BeaverBuddies.Beavers";

        /// <summary>Science and beavers are not carried by workers: they move by themselves, in step.</summary>
        public static bool IsSpecial(string item) => item == Science || item == Beavers;
        public const int LeadPercent = 10;
        public const int MinLead = 10;

        /// <summary>
        /// Amounts from 0 to <see cref="MaxAmount"/>, not both 0; a side with an amount names its good; two sides that
        /// both give something give different goods.
        /// </summary>
        public static bool AreValid(string giveGood, int giveAmount, string getGood, int getAmount)
        {
            if (giveAmount < 0 || getAmount < 0 || giveAmount > MaxAmount || getAmount > MaxAmount) return false;
            if (giveAmount == 0 && getAmount == 0) return false;
            if (giveAmount > 0 && string.IsNullOrEmpty(giveGood)) return false;
            if (getAmount > 0 && string.IsNullOrEmpty(getGood)) return false;
            return giveAmount == 0 || getAmount == 0 || !string.Equals(giveGood, getGood, StringComparison.Ordinal);
        }

        /// <summary>The good a side gives, or null for a side that gives nothing.</summary>
        public static string GoodOf(string good, int amount) => amount > 0 ? good : null;

        /// <summary>How far ahead of the other side a side of <paramref name="total"/> may run.</summary>
        public static int Lead(int total) =>
            Math.Min(total, Math.Max(MinLead, (int)(((long)total * LeadPercent + 99) / 100)));

        /// <summary>
        /// How many of its good a side may have delivered by now: its share of what the other side has delivered, plus
        /// its lead. Once the other side is done, all of it.
        /// </summary>
        public static int Allowed(int total, int otherTotal, int otherSent)
        {
            if (total <= 0) return 0;
            if (otherTotal <= 0 || otherSent >= otherTotal) return total;
            long matched = ((long)Math.Max(0, otherSent) * total + otherTotal - 1) / otherTotal;
            return (int)Math.Min(total, matched + Lead(total));
        }

        /// <summary>How many more a side may deliver now; 0 while it waits for the other side to catch up.</summary>
        public static int StillToBring(int total, int sent, int otherTotal, int otherSent) =>
            Math.Max(0, Allowed(total, otherTotal, otherSent) - sent);

        /// <summary>How much of a delivery counts towards a side; anything over its amount is ordinary trade.</summary>
        public static int Counted(int total, int sent, int amount) => Math.Max(0, Math.Min(amount, total - sent));

        public static bool IsComplete(int total, int sent, int otherTotal, int otherSent) =>
            sent >= total && otherSent >= otherTotal;
    }
}
