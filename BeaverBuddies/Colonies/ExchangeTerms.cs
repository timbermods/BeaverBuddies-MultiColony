using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BeaverBuddies.Colonies
{
    /// <summary>One round of an exchange that crossed a Trading Post, seen from one half: what its colony gave and got.</summary>
    public readonly struct TradeRecord
    {
        public readonly int Cycle, Day;
        public readonly string Gave, Got;
        public readonly int GaveAmount, GotAmount;

        public TradeRecord(int cycle, int day, string gave, int gaveAmount, string got, int gotAmount)
        {
            Cycle = cycle;
            Day = day;
            Gave = gaveAmount > 0 ? gave : null;
            GaveAmount = Math.Max(0, gaveAmount);
            Got = gotAmount > 0 ? got : null;
            GotAmount = Math.Max(0, gotAmount);
        }

        public string Encode() => string.Join("|", Cycle.ToString(CultureInfo.InvariantCulture), Day.ToString(CultureInfo.InvariantCulture),
            Gave ?? "", GaveAmount.ToString(CultureInfo.InvariantCulture), Got ?? "", GotAmount.ToString(CultureInfo.InvariantCulture));

        public static bool TryDecode(string text, out TradeRecord record)
        {
            record = default;
            string[] parts = (text ?? "").Split('|');
            if (parts.Length != 6
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cycle)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int gave)
                || !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int got))
                return false;
            record = new TradeRecord(cycle, day, parts[2], gave, parts[4], got);
            return true;
        }
    }

    /// <summary>
    /// What has passed from each colony to each other, per good: ColonyTradeLedger's totals, kept free of the game so it
    /// can be checked headless. Recorded where goods actually move (a round crossing), saved sorted and hashed as a sum,
    /// so neither depends on the order trades happened in.
    /// </summary>
    public sealed class TradeTotals
    {
        private static readonly IComparer<(int, int, string)> Order = Comparer<(int, int, string)>.Create((a, b) =>
        {
            int c = a.Item1.CompareTo(b.Item1);
            if (c == 0) c = a.Item2.CompareTo(b.Item2);
            return c != 0 ? c : string.CompareOrdinal(a.Item3, b.Item3);
        });

        // (from, to, good) -> amount; sorted so saving never depends on the order trades happened in.
        private readonly SortedDictionary<(int, int, string), int> totals = new SortedDictionary<(int, int, string), int>(Order);

        public int Count => totals.Count;

        public int Of(int from, int to, string goodId) => goodId != null && totals.TryGetValue((from, to, goodId), out int total) ? total : 0;

        /// <summary>Adds what crossed; returns the new total.</summary>
        public int Record(int from, int to, string goodId, int amount)
        {
            totals.TryGetValue((from, to, goodId), out int total);
            total += amount;
            totals[(from, to, goodId)] = total;
            return total;
        }

        /// <summary>The saved form: "from|to|good|amount", in order.</summary>
        public List<string> Encode() => totals.Select(t => string.Join("|", t.Key.Item1.ToString(CultureInfo.InvariantCulture),
            t.Key.Item2.ToString(CultureInfo.InvariantCulture), t.Key.Item3, t.Value.ToString(CultureInfo.InvariantCulture))).ToList();

        /// <summary>Reads the saved form; a damaged entry is skipped.</summary>
        public void Decode(IEnumerable<string> entries)
        {
            foreach (string entry in entries ?? Enumerable.Empty<string>())
            {
                string[] parts = (entry ?? "").Split('|');
                if (parts.Length == 4 && parts[2].Length > 0
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int to)
                    && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                    totals[(from, to, parts[2])] = amount;
            }
        }

        /// <summary>Diagnostics: a hash of every total (sorted, so the order trades happened in plays no part).</summary>
        public long Fingerprint()
        {
            long hash = 0;
            foreach (var t in totals) hash = hash * 31 + (t.Key.Item1 * 7 + t.Key.Item2 * 13 + ColonyDigest.Of(t.Key.Item3) * 17 + t.Value);
            return hash;
        }

        /// <summary>Goods that went from <paramref name="from"/> to <paramref name="to"/>, most first.</summary>
        public List<KeyValuePair<string, int>> Sent(int from, int to) =>
            totals.Where(t => t.Key.Item1 == from && t.Key.Item2 == to)
                .Select(t => new KeyValuePair<string, int>(t.Key.Item3, t.Value))
                .OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).ToList();

        public TradeTotals Copy()
        {
            var copy = new TradeTotals();
            foreach (var t in totals) copy.totals[t.Key] = t.Value;
            return copy;
        }

        /// <summary>What crossed since <paramref name="earlier"/> (a copy taken then), in order.</summary>
        public List<(int from, int to, string good, int amount)> Since(TradeTotals earlier)
        {
            var crossed = new List<(int, int, string, int)>();
            foreach (var t in totals)
            {
                int before = earlier == null ? 0 : earlier.Of(t.Key.Item1, t.Key.Item2, t.Key.Item3);
                if (t.Value > before) crossed.Add((t.Key.Item1, t.Key.Item2, t.Key.Item3, t.Value - before));
            }
            return crossed;
        }
    }

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
        /// <summary>The most a side can say it keeps back (see <see cref="CanSpare"/>).</summary>
        public const int MaxKeep = 9999;

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

        /// <summary>A floor a side may set: 0 (none) to <see cref="MaxKeep"/>.</summary>
        public static bool IsValidKeep(int keep) => keep >= 0 && keep <= MaxKeep;

        /// <summary>
        /// A side gives a round's amount only while what its colony has, less the round, is at least what it keeps
        /// back: a standing deal never starves the giver. What already waits on the half counts as had.
        /// </summary>
        public static bool CanSpare(int have, int amount, int keep) => have - amount >= System.Math.Max(0, keep);

        /// <summary>
        /// <see cref="StillToBring"/>, or nothing while the colony cannot spare the round (<see cref="CanSpare"/>): what
        /// already waits on the half stays there, and the round goes on once the colony has more.
        /// </summary>
        public static int StillToBringKeeping(int total, int held, int onTheWay, int have, int keep) =>
            keep <= 0 || CanSpare(have, total, keep) ? StillToBring(total, held, onTheWay) : 0;

        /// <summary>An exchange's terms, from one side, as one string (what a half remembers to offer again).</summary>
        public static string EncodeTerms(string giveGood, int giveAmount, string getGood, int getAmount, int rounds, bool repeat, int keep) =>
            string.Join("|", giveGood ?? "", giveAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), getGood ?? "",
                getAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), rounds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                repeat ? "1" : "0", keep.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public static bool TryDecodeTerms(string text, out string giveGood, out int giveAmount, out string getGood, out int getAmount,
            out int rounds, out bool repeat, out int keep)
        {
            giveGood = getGood = null;
            giveAmount = getAmount = rounds = keep = 0;
            repeat = false;
            string[] parts = (text ?? "").Split('|');
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (parts.Length != 7
                || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, culture, out giveAmount)
                || !int.TryParse(parts[3], System.Globalization.NumberStyles.Integer, culture, out getAmount)
                || !int.TryParse(parts[4], System.Globalization.NumberStyles.Integer, culture, out rounds)
                || !int.TryParse(parts[6], System.Globalization.NumberStyles.Integer, culture, out keep))
                return false;
            giveGood = GoodOf(parts[0], giveAmount);
            getGood = GoodOf(parts[2], getAmount);
            repeat = parts[5] == "1";
            return AreValid(giveGood, giveAmount, getGood, getAmount) && (repeat || AreValidRounds(rounds)) && IsValidKeep(keep);
        }

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
        /// Beavers a district can give: its adults able to move now (not contaminated, of the receiving colony's faction,
        /// able to walk to its district and carrying nothing: exactly those a crossing round moves), as long as one adult
        /// always stays.
        /// </summary>
        public static int BeaversToSpare(int adults, int movableAdults) => Math.Max(0, Math.Min(movableAdults, adults - 1));

        /// <summary>
        /// How many of the round's goods already waiting unreserved on the giving half are held for it now (goods an
        /// ended exchange left there, what was left over from a load, what arrived while the post was paused, or what the
        /// other colony sent earlier): as an arriving load is held (<see cref="ToHold"/>), and nothing while the colony
        /// keeps a reserve the round would eat into (<see cref="StillToBringKeeping"/>).
        /// </summary>
        public static int ToHoldWaiting(int total, int held, int waiting, int have, int keep) =>
            keep <= 0 || CanSpare(have, total, keep) ? ToHold(total, held, waiting) : 0;

        // ---- the tick's check of an open exchange (ColonyExchangeService.CheckTradingPosts) ----

        /// <summary>What the tick's check does with an open exchange at a post.</summary>
        public enum PostCheck
        {
            /// <summary>It goes on: the round crosses once both sides are in (see <see cref="RoundMayCross"/>).</summary>
            GoesOn,
            /// <summary>The offering half speaks for the post: nothing is decided from the other one.</summary>
            NotThisHalf,
            /// <summary>The other half holds no exchange (only a damaged or older save leaves one so): it ends.</summary>
            EndAlone,
            /// <summary>A half belongs to another colony than the one that agreed (a handover, other roads): it ends.</summary>
            EndColonies,
            /// <summary>A mixed-factions game: the two colonies' factions no longer allow its terms: it ends.</summary>
            EndFactions,
        }

        /// <summary>
        /// Whether an open exchange ends at the tick's check, seen from one half of its post. A colony is -1 for a half in
        /// no colony's district (its road was removed: the post pauses and its exchange waits). The factions are judged
        /// only while the post trades (<paramref name="trading"/>: each half in a different colony's district), so a
        /// paused post waits for its road instead of ending: a half in no colony counted as the host's faction, which
        /// ended a paused exchange between two colonies of the other faction (C1). <paramref name="factionsAllow"/> is
        /// only read while it trades.
        /// </summary>
        public static PostCheck Ending(bool partnerOpen, bool offeredHere, int agreed, int owner, int partnerAgreed, int partnerOwner,
            bool trading, bool factionsAllow)
        {
            if (!partnerOpen) return PostCheck.EndAlone;
            if (!offeredHere) return PostCheck.NotThisHalf;
            if (ColonyChanged(agreed, owner) || ColonyChanged(partnerAgreed, partnerOwner)) return PostCheck.EndColonies;
            if (trading && !factionsAllow) return PostCheck.EndFactions;
            return PostCheck.GoesOn;
        }

        /// <summary>A half belongs to another colony than the one that agreed (a half in no colony, -1, has not changed).</summary>
        public static bool ColonyChanged(int agreed, int owner) => owner >= 0 && agreed >= 0 && owner != agreed;

        /// <summary>A round of an exchange that goes on may cross (once both sides are in): it runs, the post trades, and nobody asked to end it.</summary>
        public static bool RoundMayCross(bool active, bool trading, bool cancelAsked) => active && trading && !cancelAsked;

        // ---- why a round waits (the panel's status line and the diagnostics report) ----

        /// <summary>Why a side giving goods is not in yet.</summary>
        public enum GoodsWait
        {
            /// <summary>Its workers are bringing it (or will, as nothing stops them).</summary>
            Bringing,
            /// <summary>Its half is paused or flooded (blocked): its workers cannot work there.</summary>
            Blocked,
            /// <summary>Its half has no workers.</summary>
            NoWorkers,
            /// <summary>
            /// Its half has no room for more of the good: the other colony has not hauled away what crossed to its half
            /// earlier (the post's room for a good is shared by its two halves).
            /// </summary>
            NoRoom,
            /// <summary>Its district has none of the good left to bring (besides what already waits on the half).</summary>
            NoStock,
        }

        /// <summary>
        /// Why a goods side is not in (T5: a stall says why). <paramref name="onTheWay"/> loads being carried in count as
        /// bringing; <paramref name="room"/> is the half's unreserved room for the good; <paramref name="stockElsewhere"/>
        /// the district's stock of it outside the half.
        /// </summary>
        public static GoodsWait WhyGoodsWait(bool blocked, int workers, int onTheWay, int room, int stockElsewhere)
        {
            if (blocked) return GoodsWait.Blocked;
            if (workers <= 0) return GoodsWait.NoWorkers;
            if (onTheWay > 0) return GoodsWait.Bringing;
            if (room <= 0) return GoodsWait.NoRoom;
            if (stockElsewhere <= 0) return GoodsWait.NoStock;
            return GoodsWait.Bringing;
        }
    }
}
