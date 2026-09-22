using System;
using System.Globalization;

namespace BeaverBuddies.Colonies
{
    /// <summary>How long a stock lasts at the rate it is used, kept free of the game so it can be checked headless.</summary>
    public static class SupplyDays
    {
        /// <summary>
        /// Days the stock lasts: at yesterday's use, or, before a full day has been seen, at today's use so far once a
        /// quarter of the day has passed. Null while nothing says how fast it is used.
        /// </summary>
        public static float? Estimate(int stock, int usedYesterday, int usedToday, float dayProgress)
        {
            float rate;
            if (usedYesterday > 0) rate = usedYesterday;
            else if (dayProgress >= 0.25f && usedToday > 0) rate = usedToday / dayProgress;
            else return null;
            return Math.Max(0, stock) / rate;
        }

        /// <summary>"3.4" under ten days, "34" under a hundred, "99+" beyond, or null for an unknown rate.</summary>
        public static string Format(float? days)
        {
            if (days == null) return null;
            float d = days.Value;
            if (d >= 100) return "99+";
            return d < 10 ? d.ToString("0.0", CultureInfo.InvariantCulture) : ((int)Math.Floor(d)).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Less than a day left is worth a warning.</summary>
        public static bool IsLow(float? days) => days != null && days.Value < 1f;
    }
}
