using System;
using System.Globalization;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The trading post's offer form, kept free of the game so it can be checked headless: reading an amount box, what
    /// one click of − or + does, and which of the form's messages applies. An offer the form calls an exchange, a gift
    /// or a request is exactly one <see cref="ExchangeTerms.AreValid"/> accepts.
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

        public static Verdict Judge(string giveItem, string giveText, string getItem, string getText, out int give, out int get)
        {
            bool amountsRead = TryReadAmount(giveText, out give) & TryReadAmount(getText, out get);
            if (!amountsRead) return Verdict.BadAmount;
            if (give == 0 && get == 0) return Verdict.NothingEitherWay;
            if ((give > 0 && string.IsNullOrEmpty(giveItem)) || (get > 0 && string.IsNullOrEmpty(getItem))) return Verdict.NoItem;
            if (give > 0 && get > 0 && string.Equals(giveItem, getItem, StringComparison.Ordinal)) return Verdict.SameItem;
            if (get == 0) return Verdict.Gift;
            if (give == 0) return Verdict.Request;
            return Verdict.Exchange;
        }

        /// <summary>How far one click of − or + moves an amount: a beaver at a time, other items ten at a time; Shift ten times that.</summary>
        public static int Step(string item, bool shift) => (item == ExchangeTerms.Beavers ? 1 : 10) * (shift ? 10 : 1);

        /// <summary>
        /// An amount after one click: on to the next whole step (from 95, + gives 100 and − gives 90), within 0 and
        /// <see cref="ExchangeTerms.MaxAmount"/>.
        /// </summary>
        public static int Stepped(int amount, int step, bool up)
        {
            if (step <= 0) step = 1;
            amount = Math.Max(0, Math.Min(ExchangeTerms.MaxAmount, amount));
            long next = up ? ((long)amount / step + 1) * step : ((long)amount + step - 1) / step * step - step;
            return (int)Math.Max(0, Math.Min(ExchangeTerms.MaxAmount, next));
        }
    }
}
