using System;
using System.Globalization;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The trading post's offer form, kept free of the game so it can be checked headless: reading its boxes, what one
    /// click of − or + does, and which of the form's messages applies. An offer the form calls an exchange, a gift or a
    /// request is exactly one <see cref="ExchangeTerms.AreValid"/> and <see cref="ExchangeTerms.AreValidRounds"/> accept.
    /// </summary>
    public static class TradeOfferForm
    {
        public enum Verdict
        {
            /// <summary>Something each way.</summary>
            Exchange,
            /// <summary>The offering colony gives and asks nothing back.</summary>
            Gift,
            /// <summary>The offering colony asks and gives nothing.</summary>
            Request,
            /// <summary>An amount box does not hold a whole number from 0 to <see cref="ExchangeTerms.MaxAmount"/>.</summary>
            BadAmount,
            /// <summary>The rounds box does not hold a whole number from 1 to <see cref="ExchangeTerms.MaxRounds"/>.</summary>
            BadRounds,
            /// <summary>Both amounts are 0.</summary>
            NothingEitherWay,
            /// <summary>A side with an amount has no item chosen.</summary>
            NoItem,
            /// <summary>Both sides give the same item.</summary>
            SameItem,
        }

        public static bool IsOffer(Verdict verdict) =>
            verdict == Verdict.Exchange || verdict == Verdict.Gift || verdict == Verdict.Request;

        /// <summary>An amount box: empty means 0; otherwise digits only, up to <see cref="ExchangeTerms.MaxAmount"/>.</summary>
        public static bool TryReadAmount(string text, out int amount)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                amount = 0;
                return true;
            }
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount <= ExchangeTerms.MaxAmount;
        }

        /// <summary>The "keep at least" box: empty means 0; otherwise digits only, up to <see cref="ExchangeTerms.MaxKeep"/>.</summary>
        public static bool TryReadKeep(string text, out int keep)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                keep = 0;
                return true;
            }
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out keep) && keep <= ExchangeTerms.MaxKeep;
        }

        /// <summary>How far one click of − or + moves the reserve: fifty at a time (Shift: ten).</summary>
        public static int KeepStep(bool shift) => shift ? 10 : 50;

        /// <summary>The rounds box: digits only, from 1 to <see cref="ExchangeTerms.MaxRounds"/>.</summary>
        public static bool TryReadRounds(string text, out int rounds) =>
            int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out rounds) && ExchangeTerms.AreValidRounds(rounds);

        /// <summary>
        /// The form as a whole. A repeating offer does not read the rounds box (it counts as 1 and is ignored).
        /// </summary>
        public static Verdict Judge(string giveItem, string giveText, string getItem, string getText, string roundsText, bool repeat,
            out int give, out int get, out int rounds)
        {
            bool amountsRead = TryReadAmount(giveText, out give) & TryReadAmount(getText, out get);
            rounds = 1;
            if (!amountsRead) return Verdict.BadAmount;
            if (give == 0 && get == 0) return Verdict.NothingEitherWay;
            if ((give > 0 && string.IsNullOrEmpty(giveItem)) || (get > 0 && string.IsNullOrEmpty(getItem))) return Verdict.NoItem;
            if (give > 0 && get > 0 && string.Equals(giveItem, getItem, StringComparison.Ordinal)) return Verdict.SameItem;
            if (!repeat && !TryReadRounds(roundsText, out rounds))
            {
                rounds = 1;
                return Verdict.BadRounds;
            }
            if (get == 0) return Verdict.Gift;
            if (give == 0) return Verdict.Request;
            return Verdict.Exchange;
        }

        /// <summary>
        /// How far one click of − or + moves an amount: ten at a time (Shift: one), beavers one at a time (Shift: ten).
        /// </summary>
        public static int Step(string item, bool shift) => item == ExchangeTerms.Beavers ? (shift ? 10 : 1) : (shift ? 1 : 10);

        /// <summary>How far one click moves the rounds: one (Shift: ten).</summary>
        public static int RoundsStep(bool shift) => shift ? 10 : 1;

        /// <summary>
        /// A number after one click: on to the next whole step (from 95, + gives 100 and − gives 90), within
        /// <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        public static int Stepped(int value, int step, bool up, int min, int max)
        {
            if (step <= 0) step = 1;
            value = Math.Max(min, Math.Min(max, value));
            long next = up ? ((long)value / step + 1) * step : ((long)value + step - 1) / step * step - step;
            return (int)Math.Max(min, Math.Min(max, next));
        }

        /// <summary>An amount after one click, within 0 and <see cref="ExchangeTerms.MaxAmount"/>.</summary>
        public static int Stepped(int amount, int step, bool up) => Stepped(amount, step, up, 0, ExchangeTerms.MaxAmount);
    }
}
