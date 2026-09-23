using BeaverBuddies.Colonies;

/// <summary>
/// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
/// that need no game: reviewer C: Trading Posts at scale. One check per finding, named after it.
/// </summary>
static class RcTradingChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual, string what = "") =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{what} expected [{expected}], got [{actual}]");

    static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root)!;
        Check(root != null, "could not find the repository root");
        return root!;
    }

    static string Source(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    /// <summary>The body of a method, from its signature to the matching closing brace.</summary>
    static string Body(string text, string signature)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        Check(start >= 0, "not found: " + signature);
        int open = text.IndexOf('{', start), depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text.Substring(start, i - start + 1);
        }
        throw new Exception("unbalanced braces after " + signature);
    }

    static string Engine() => Source("BeaverBuddies", "Colonies", "TradingPostExchange.cs");

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("C1: a paused Trading Post's exchange waits for its road; the factions are judged only while the post trades", () =>
        {
            // beta24 judged the factions of a half in no colony (-1) as the host's faction's, so a paused post between two
            // colonies of the other faction ended its exchange ("the factions no longer allow its terms").
            foreach (int owner in new[] { -1, 0 })
                foreach (int partnerOwner in new[] { -1, 1 })
                {
                    bool trading = owner >= 0 && partnerOwner >= 0;
                    var paused = ExchangeTerms.Ending(true, true, 0, owner, 1, partnerOwner, trading, factionsAllow: false);
                    Equal(trading ? ExchangeTerms.PostCheck.EndFactions : ExchangeTerms.PostCheck.GoesOn, paused, $"owners {owner}/{partnerOwner}:");
                }
            // Every case: an exchange ends only for a reason, judged from the half that offered it.
            int[] colonies = { -1, 0, 1, 2 };
            foreach (bool partnerOpen in new[] { false, true })
            foreach (bool offeredHere in new[] { false, true })
            foreach (int agreed in colonies)
            foreach (int owner in colonies)
            foreach (int partnerAgreed in colonies)
            foreach (int partnerOwner in colonies)
            foreach (bool factions in new[] { false, true })
            {
                bool trading = owner >= 0 && partnerOwner >= 0 && owner != partnerOwner;
                var result = ExchangeTerms.Ending(partnerOpen, offeredHere, agreed, owner, partnerAgreed, partnerOwner, trading, factions);
                bool changed = ExchangeTerms.ColonyChanged(agreed, owner) || ExchangeTerms.ColonyChanged(partnerAgreed, partnerOwner);
                string at = $"open {partnerOpen} here {offeredHere} {agreed}>{owner} {partnerAgreed}>{partnerOwner} factions {factions}:";
                if (!partnerOpen) Equal(ExchangeTerms.PostCheck.EndAlone, result, at);
                else if (!offeredHere) Equal(ExchangeTerms.PostCheck.NotThisHalf, result, at);
                else if (changed) Equal(ExchangeTerms.PostCheck.EndColonies, result, at);
                else if (trading && !factions) Equal(ExchangeTerms.PostCheck.EndFactions, result, at);
                else Equal(ExchangeTerms.PostCheck.GoesOn, result, at);
                // A half in no colony never ends an exchange by itself: it pauses until a road reaches it again.
                if (partnerOpen && offeredHere && (owner < 0 || partnerOwner < 0) && !changed) Equal(ExchangeTerms.PostCheck.GoesOn, result, at);
            }
            Check(!ExchangeTerms.ColonyChanged(0, -1) && !ExchangeTerms.ColonyChanged(-1, 1) && ExchangeTerms.ColonyChanged(0, 2));
            Check(ExchangeTerms.RoundMayCross(true, true, false));
            Check(!ExchangeTerms.RoundMayCross(false, true, false) && !ExchangeTerms.RoundMayCross(true, false, false) && !ExchangeTerms.RoundMayCross(true, true, true));
            // The engine decides by this (and computes the factions' answer only while the post trades).
            string check = Body(Engine(), "private void CheckTradingPosts()");
            Check(check.Contains("ExchangeTerms.Ending("), "CheckTradingPosts no longer decides endings with ExchangeTerms.Ending");
            Check(check.Contains("!trading || FactionsAllow("), "CheckTradingPosts asks the factions about a post that does not trade");
            Check(check.Contains("ExchangeTerms.RoundMayCross("), "CheckTradingPosts no longer asks ExchangeTerms.RoundMayCross");
        });

        yield return ("C4: goods already waiting on a giving half count toward its round, so a half whose room they fill still fills its round", () =>
        {
            // Held like an arriving load: up to what the round still misses.
            Equal(40, ExchangeTerms.ToHoldWaiting(100, 60, 70, 0, 0));
            Equal(25, ExchangeTerms.ToHoldWaiting(100, 0, 25, 0, 0));
            Equal(0, ExchangeTerms.ToHoldWaiting(100, 100, 30, 0, 0));
            // A reserve: nothing more is held while the round would eat into it.
            Equal(0, ExchangeTerms.ToHoldWaiting(100, 0, 100, have: 150, keep: 100));
            Equal(100, ExchangeTerms.ToHoldWaiting(100, 0, 100, have: 200, keep: 100));
            var random = new Random(4);
            for (int i = 0; i < 20000; i++)
            {
                int total = random.Next(0, 101), held = random.Next(0, total + 1), waiting = random.Next(0, 150);
                int have = random.Next(0, 500), keep = random.Next(0, 3) == 0 ? 0 : random.Next(0, 400);
                int hold = ExchangeTerms.ToHoldWaiting(total, held, waiting, have, keep);
                Check(hold >= 0 && hold <= waiting && held + hold <= total, $"{hold} of {waiting} waiting for {held}/{total}");
                if (keep <= 0 || ExchangeTerms.CanSpare(have, total, keep)) Equal(Math.Min(waiting, total - held), hold);
                else Equal(0, hold);
            }
            // beta24: a half's room for a good was filled by that good waiting unreserved (what an ended exchange left,
            // what the other colony sent before), its colony had no storage room for it, and only arriving loads were
            // held: the round never filled. The engine now holds what waits, before it judges the round.
            Check(Stuck(holdWaiting: false), "the model no longer shows beta24's stuck round");
            Check(!Stuck(holdWaiting: true), "a round whose goods already wait on its half does not fill");
            string check = Body(Engine(), "private void CheckTradingPosts()");
            int hold1 = check.IndexOf("HoldWaiting(half, mine)", StringComparison.Ordinal), judge = check.IndexOf("IsIn(half, mine, owner)", StringComparison.Ordinal);
            Check(hold1 >= 0 && judge > hold1, "CheckTradingPosts no longer holds what waits on the halves before it judges the round");
            string holder = Body(Engine(), "private void HoldWaiting(");
            Check(holder.Contains("ExchangeTerms.ToHoldWaiting(") && holder.Contains("UnreservedAmountInStock") && holder.Contains("ReserveStock"),
                "HoldWaiting no longer reserves what waits, by ExchangeTerms.ToHoldWaiting");
        });

        yield return ("C3: a round that waits says why: paused or flooded, no workers, no room, nothing left to bring", () =>
        {
            Equal(ExchangeTerms.GoodsWait.Blocked, ExchangeTerms.WhyGoodsWait(true, 0, 0, 0, 0));
            Equal(ExchangeTerms.GoodsWait.NoWorkers, ExchangeTerms.WhyGoodsWait(false, 0, 5, 0, 0));
            // Loads on the way: they are bringing it, whatever else.
            Equal(ExchangeTerms.GoodsWait.Bringing, ExchangeTerms.WhyGoodsWait(false, 2, 5, 0, 0));
            Equal(ExchangeTerms.GoodsWait.NoRoom, ExchangeTerms.WhyGoodsWait(false, 2, 0, 0, 500));
            Equal(ExchangeTerms.GoodsWait.NoStock, ExchangeTerms.WhyGoodsWait(false, 2, 0, 40, 0));
            Equal(ExchangeTerms.GoodsWait.Bringing, ExchangeTerms.WhyGoodsWait(false, 2, 0, 40, 500));
            // The panel has a line for every reason, from either side, and the report a phrase.
            string fragment = Body(Source("BeaverBuddies", "Colonies", "TradingPostFragment.cs"), "internal static string StatusLine(");
            foreach (string reason in new[] { "Blocked", "NoWorkers", "NoRoom", "NoStock" })
                Check(fragment.Split("ExchangeTerms.GoodsWait." + reason).Length - 1 == 2, "the panel does not say both sides' " + reason);
            foreach (string key in new[] { "StatusYourHalfBlocked", "StatusNoWorkers", "StatusNoRoom", "StatusNoStock", "StatusTheirHalfBlocked",
                "StatusTheirNoWorkers", "StatusHaulAway", "StatusTheirNoStock" })
                Check(fragment.Contains("\"BeaverBuddies.Colony.Trade." + key + "\""), "the panel no longer says " + key);
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
            foreach (string key in new[] { "StatusYourHalfBlocked", "StatusNoRoom", "StatusNoStock", "StatusTheirHalfBlocked", "StatusTheirNoWorkers",
                "StatusTheirNoStock", "StatusHaulAway", "Notice.PostRemoved" })
                Check(csv.Contains("\nBeaverBuddies.Colony.Trade." + key + ",\""), "no English line for " + key);
            string report = Body(Source("BeaverBuddies", "Colonies", "ColonyDiagnostics.cs"), "private static string Stall(");
            Check(report.Contains("WhyNotIn("), "the diagnostics report no longer says why a round waits as the panel does");
        });

        yield return ("C8: the Ctrl+T window says why a post's round is held up, shows a paused exchange as paused, and keeps its post listed", () =>
        {
            // beta24 read an exchange at a paused post as "Not trading yet", showed a stalled round as "60/100" only, and
            // dropped a post whose half of this colony had lost its road (it is in no colony then) while goods waited on it.
            string window = Body(Source("BeaverBuddies", "Colonies", "TradeOverviewPanel.cs"), "private void Describe(");
            Check(window.Contains("TradingPostFragment.StatusLine(") && window.Contains("\"BeaverBuddies.Colony.Trade.PausedTitle\""),
                "the trading window no longer says why a post's round waits, or that it is paused");
            Check(Body(Source("BeaverBuddies", "Colonies", "TradeOverviewPanel.cs"), "private void RefreshLists()").Contains("open.Colony == me"),
                "the trading window drops a paused post whose half of this colony lost its road");
        });

        yield return ("C7: the tick's check of every post allocates no list, and counts beavers without LINQ", () =>
        {
            string check = Body(Engine(), "private void CheckTradingPosts()");
            Check(!check.Contains(".ToList()"), "CheckTradingPosts copies the crossings into a new list every 8 ticks");
            Check(Body(Engine(), "public int BeaversToSpare(").IndexOf(".Count(", StringComparison.Ordinal) < 0, "BeaversToSpare counts with LINQ");
            string movable = Body(Engine(), "private void Movable(");
            Check(!movable.Contains(".Where(") && !movable.Contains("=>"), "Movable allocates a closure per call");
        });

        yield return ("T3: the trade totals and the ledger survive a save in any order, and a damaged entry is skipped", () =>
        {
            var random = new Random(23);
            string[] goods = { "Log", "Plank", "Gear", "Water", ExchangeTerms.Science, ExchangeTerms.Beavers, "SomeMod.Good" };
            var a = new TradeTotals();
            var b = new TradeTotals();
            var expected = new Dictionary<(int, int, string), int>();
            var entries = new List<(int from, int to, string good, int amount)>();
            for (int i = 0; i < 2000; i++) entries.Add((random.Next(4), random.Next(4), goods[random.Next(goods.Length)], random.Next(1, 101)));
            TradeTotals earlier = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int total = a.Record(e.from, e.to, e.good, e.amount);
                expected[(e.from, e.to, e.good)] = expected.GetValueOrDefault((e.from, e.to, e.good)) + e.amount;
                Equal(expected[(e.from, e.to, e.good)], total);
                if (i == 1000) earlier = a.Copy();
            }
            // The same trades in another order: the same totals, the same save, the same hash.
            foreach (var e in entries.OrderBy(_ => random.Next())) b.Record(e.from, e.to, e.good, e.amount);
            Check(a.Encode().SequenceEqual(b.Encode()), "the saved totals depend on the order trades happened in");
            Equal(a.Fingerprint(), b.Fingerprint());
            var loaded = new TradeTotals();
            loaded.Decode(a.Encode().Concat(new[] { "x|1|Log|5", "1|2|Log", "1|2||5", null, "" }));
            Check(loaded.Encode().SequenceEqual(a.Encode()), "the totals do not survive a save");
            Equal(a.Fingerprint(), loaded.Fingerprint());
            foreach (var t in expected) Equal(t.Value, loaded.Of(t.Key.Item1, t.Key.Item2, t.Key.Item3));
            // What crossed since a copy is the difference, and only that.
            int since = a.Since(earlier).Sum(c => c.amount);
            Equal(entries.Skip(1001).Sum(e => e.amount), since);
            Check(a.Since(a.Copy()).Count == 0);
            // "Most first" for the panel.
            var sent = a.Sent(0, 1);
            for (int i = 1; i < sent.Count; i++) Check(sent[i - 1].Value >= sent[i].Value);
            // A round's record, from each side.
            foreach (string good in goods)
            {
                var record = new TradeRecord(12, 3, good, random.Next(0, 101), goods[random.Next(goods.Length)], random.Next(0, 101));
                Check(TradeRecord.TryDecode(record.Encode(), out TradeRecord back));
                Equal(record.Encode(), back.Encode());
                Check(record.GaveAmount > 0 || record.Gave == null);
            }
            Check(!TradeRecord.TryDecode("1|2|Log|x|Gear|3", out _) && !TradeRecord.TryDecode(null, out _));
            // The terms a half remembers, for every item.
            foreach (string give in goods)
                foreach (string get in goods)
                {
                    if (give == get) continue;
                    string terms = ExchangeTerms.EncodeTerms(give, 100, get, 7, 12, false, 250);
                    Check(ExchangeTerms.TryDecodeTerms(terms, out string g1, out int a1, out string g2, out int a2, out int r, out bool rep, out int k));
                    Check(g1 == give && a1 == 100 && g2 == get && a2 == 7 && r == 12 && !rep && k == 250, terms);
                }
        });

        yield return ("T1/T3: over many cycles of exchanges, saves, pauses, hand-overs, cancels and a post blown up, nothing is created or lost and nothing sticks", () =>
        {
            var crossed = new List<int>();
            World.SavedMidRound = World.RemovedMidRound = World.HandedOverMidRound = World.PausedMidRound = 0;
            for (int seed = 1; seed <= 12; seed++) crossed.Add(Simulate(seed, steps: 6000));
            Check(World.SavedMidRound > 20 && World.RemovedMidRound > 0 && World.HandedOverMidRound > 0 && World.PausedMidRound > 5,
                $"the runs met too few rounds under way at a save ({World.SavedMidRound}), a removal ({World.RemovedMidRound}), "
                + $"a hand-over ({World.HandedOverMidRound}) or a pause ({World.PausedMidRound})");
            // Every run traded, and together they crossed many rounds of every kind.
            Check(crossed.All(c => c > 0) && crossed.Sum() > 150, "rounds crossed per run: " + string.Join(", ", crossed));
        });
    }

    // ---- a model of one Trading Post, driven by the pure parts the engine uses ----
    //
    // It follows ColonyExchangeService step by step (StillToBring, OnArrival, CheckTradingPosts with HoldWaiting, IsIn
    // and Cross, End and Release, the handover's End, PostInitializeEntity after a load, a removal's recovered goods),
    // with a half's room for a good shared by both halves and the loads on their way (the game mirrors every capacity
    // reservation onto the linked half, so each half's unreserved room is MaxAmount less both halves' stock and the
    // loads being carried in). Every decision is ExchangeTerms'; only the game's inventories are modelled.

    sealed class Side
    {
        public int State, Serial, Total, Held, Rounds, Done, Keep, Colony = -1;
        public bool ProposedHere, Repeat, CancelAsked;
        public string Good, LastTerms;
        public readonly List<TradeRecord> Ledger = new List<TradeRecord>();
        public bool IsOpen => State != 0;
        public bool IsActive => State == 2;
        public bool GivesGoods => Total > 0 && Good != null && !ExchangeTerms.IsSpecial(Good);
        public void Clear() { State = 0; ProposedHere = Repeat = CancelAsked = false; Good = null; Total = Held = Rounds = Done = Keep = 0; Colony = -1; }
    }

    sealed class Half
    {
        public int Owner, Road;
        public int Workers = 2;
        public bool Blocked;
        public Side X = new Side();
        public readonly Dictionary<string, int> Stock = new Dictionary<string, int>(), Reserved = new Dictionary<string, int>();
        public int Amount(string good) => Stock.GetValueOrDefault(good);
        public int Unreserved(string good) => Amount(good) - Reserved.GetValueOrDefault(good);
    }

    sealed class World
    {
        public Half A = new Half(), B = new Half();
        public bool Post = true;
        public bool HoldWaiting = true;
        public readonly Dictionary<(int, string), int> Storage = new Dictionary<(int, string), int>();
        public readonly HashSet<(int, string)> NoStorage = new HashSet<(int, string)>();
        public readonly Dictionary<int, int> Science = new Dictionary<int, int>(), Adults = new Dictionary<int, int>();
        public readonly List<(Half to, string good, int amount, int colony)> OnTheWay = new List<(Half, string, int, int)>();
        public readonly Dictionary<string, int> Recovered = new Dictionary<string, int>();
        public TradeTotals Totals = new TradeTotals();
        public readonly Dictionary<(int, int, string), int> Crossed = new Dictionary<(int, int, string), int>();
        // What happened while goods waited on a half for a round (the test's own coverage).
        public static int SavedMidRound, RemovedMidRound, HandedOverMidRound, PausedMidRound;
        public bool MidRound => (A.X.IsActive && A.X.Held > 0) || (B.X.IsActive && B.X.Held > 0);
        public Half Partner(Half half) => half == A ? B : A;
        public bool Trading => Post && A.Owner >= 0 && B.Owner >= 0 && A.Owner != B.Owner;
        // Loads being carried anywhere (what exists), and those to this post's halves (what its room keeps for).
        public int InFlight(string good) => OnTheWay.Where(l => l.good == good).Sum(l => l.amount);
        public int Coming(string good) => OnTheWay.Where(l => (l.to == A || l.to == B) && l.good == good).Sum(l => l.amount);
        public int Coming(Half half, string good) => OnTheWay.Where(l => l.to == half && l.good == good).Sum(l => l.amount);
        public int Room(string good) => ExchangeTerms.MaxAmount - A.Amount(good) - B.Amount(good) - Coming(good);
        public int StockOf(Half half, string good) => half.Owner < 0 ? 0 : Storage.GetValueOrDefault((half.Owner, good)) + half.Amount(good);
    }

    static readonly string[] Goods = { "Log", "Plank", "Gear", "Water" };

    static void Add(Dictionary<string, int> tally, string good, int amount) => tally[good] = tally.GetValueOrDefault(good) + amount;
    static void Add(Dictionary<(int, string), int> tally, (int, string) key, int amount) => tally[key] = tally.GetValueOrDefault(key) + amount;

    // ColonyExchangeService.StillToBring.
    static int StillToBring(World w, Half half)
    {
        Side mine = half.X, theirs = w.Partner(half).X;
        if (!w.Trading || !mine.IsActive || !theirs.IsActive || !mine.GivesGoods || mine.CancelAsked || theirs.CancelAsked) return 0;
        int onTheWay = w.Coming(half, mine.Good!);
        return mine.Keep > 0
            ? ExchangeTerms.StillToBringKeeping(mine.Total, mine.Held, onTheWay, w.StockOf(half, mine.Good!), mine.Keep)
            : ExchangeTerms.StillToBring(mine.Total, mine.Held, onTheWay);
    }

    // TradingPostCarryPatcher: a worker sets out with a load for the round (from the colony's storage).
    static void Trip(World w, Half half, Random random)
    {
        if (half.Workers <= 0 || half.Blocked || half.Owner < 0 || !half.X.GivesGoods) return;
        string good = half.X.Good!;
        int carry = Math.Min(Math.Min(StillToBring(w, half), w.Room(good)), Math.Min(random.Next(1, 16), w.Storage.GetValueOrDefault((half.Owner, good))));
        if (carry <= 0) return;
        Add(w.Storage, (half.Owner, good), -carry);
        w.OnTheWay.Add((half, good, carry, half.Owner));
    }

    // A load arrives (DistrictCrossingInventory's stock change, then OnArrival); at a removed post it goes back home.
    static void Arrive(World w, int index)
    {
        var load = w.OnTheWay[index];
        w.OnTheWay.RemoveAt(index);
        if (!w.Post || (load.to != w.A && load.to != w.B)) { Add(w.Storage, (load.colony, load.good), load.amount); return; }
        Half half = load.to;
        Add(half.Stock, load.good, load.amount);
        Side mine = half.X, theirs = w.Partner(half).X;
        if (!w.Trading || !mine.IsActive || !theirs.IsActive || !mine.GivesGoods || mine.Good != load.good) return;
        int held = Math.Min(ExchangeTerms.ToHold(mine.Total, mine.Held, load.amount), half.Unreserved(load.good));
        if (held <= 0) return;
        Add(half.Reserved, load.good, held);
        mine.Held += held;
    }

    // The half's workers carry what waits unreserved on it into their colony's storage (the game's emptying).
    static void Empty(World w, Half half, Random random)
    {
        if (half.Owner < 0 || half.Workers <= 0 || half.Blocked) return;
        foreach (string good in Goods)
        {
            int free = half.Unreserved(good);
            if (free <= 0 || w.NoStorage.Contains((half.Owner, good))) continue;
            int load = Math.Min(free, random.Next(1, 16));
            Add(half.Stock, good, -load);
            Add(w.Storage, (half.Owner, good), load);
            return;
        }
    }

    // ColonyExchangeService.End / Release.
    static void End(World w)
    {
        foreach (Half half in new[] { w.A, w.B })
        {
            Side side = half.X;
            if (side.IsActive && side.GivesGoods && side.Held > 0)
            {
                int reserved = Math.Min(side.Held, half.Reserved.GetValueOrDefault(side.Good!));
                Add(half.Reserved, side.Good!, -reserved);
            }
            side.Clear();
        }
    }

    // HoldWaiting.
    static void HoldWaiting(World w, Half half)
    {
        Side side = half.X;
        if (!side.GivesGoods || side.Held >= side.Total) return;
        int waiting = half.Unreserved(side.Good!);
        if (waiting <= 0) return;
        int held = ExchangeTerms.ToHoldWaiting(side.Total, side.Held, waiting, side.Keep > 0 ? w.StockOf(half, side.Good!) : 0, side.Keep);
        if (held <= 0) return;
        Add(half.Reserved, side.Good!, held);
        side.Held += held;
    }

    // IsIn, with a sample of the district's adults free to go this tick (the engine counts and moves the same ones).
    static bool IsIn(World w, Half half, int movable)
    {
        Side side = half.X;
        if (side.Total <= 0) return true;
        if (side.Good == ExchangeTerms.Science) return ExchangeTerms.CanSpare(w.Science.GetValueOrDefault(half.Owner), side.Total, side.Keep);
        if (side.Good == ExchangeTerms.Beavers)
            return ExchangeTerms.CanSpare(ExchangeTerms.BeaversToSpare(w.Adults.GetValueOrDefault(half.Owner), movable), side.Total, side.Keep);
        return ExchangeTerms.IsDelivered(side.Total, side.Held);
    }

    // CheckTradingPosts for the post (from its offering half) and Cross. Returns the rounds crossed.
    static int CheckPost(World w, Random random)
    {
        if (!w.Post || (!w.A.X.IsOpen && !w.B.X.IsOpen)) return 0;
        // The engine walks every half: an open one alone ends it; otherwise the offering half decides.
        Half half = w.A.X.IsOpen ? w.A : w.B;
        if (w.Partner(half).X.IsOpen && !half.X.ProposedHere) half = w.Partner(half);
        Half partner = w.Partner(half);
        Side mine = half.X, theirs = partner.X;
        bool partnerOpen = theirs.IsOpen;
        var ending = ExchangeTerms.Ending(partnerOpen, mine.ProposedHere, mine.Colony, half.Owner, theirs.IsOpen ? theirs.Colony : -1, partner.Owner,
            partnerOpen && w.Trading, true);
        if (ending == ExchangeTerms.PostCheck.EndAlone || ending == ExchangeTerms.PostCheck.EndColonies || ending == ExchangeTerms.PostCheck.EndFactions)
        {
            End(w);
            return 0;
        }
        if (ending != ExchangeTerms.PostCheck.GoesOn) return 0;
        if (!ExchangeTerms.RoundMayCross(mine.IsActive, w.Trading, mine.CancelAsked || theirs.CancelAsked)) return 0;
        if (w.HoldWaiting)
        {
            HoldWaiting(w, half);
            HoldWaiting(w, partner);
        }
        int movableHere = random.Next(0, w.Adults.GetValueOrDefault(half.Owner) + 1);
        int movableThere = random.Next(0, w.Adults.GetValueOrDefault(partner.Owner) + 1);
        if (!IsIn(w, half, movableHere) || !IsIn(w, partner, movableThere)) return 0;
        // Cross: the state first, then the moves, then the ledgers.
        string aGood = mine.Good!, bGood = theirs.Good!;
        int aTotal = mine.Total, bTotal = theirs.Total, aHeld = mine.Held, bHeld = theirs.Held;
        mine.Done++; mine.Held = 0;
        theirs.Done++; theirs.Held = 0;
        MoveGoods(half, partner, aGood, aHeld);
        MoveGoods(partner, half, bGood, bHeld);
        aTotal = MoveSpecial(w, half, partner, aGood, aTotal, movableHere);
        bTotal = MoveSpecial(w, partner, half, bGood, bTotal, movableThere);
        if (aTotal > 0) { w.Totals.Record(half.Owner, partner.Owner, aGood, aTotal); Add2(w.Crossed, (half.Owner, partner.Owner, aGood), aTotal); }
        if (bTotal > 0) { w.Totals.Record(partner.Owner, half.Owner, bGood, bTotal); Add2(w.Crossed, (partner.Owner, half.Owner, bGood), bTotal); }
        Record(mine, new TradeRecord(1, 1, aGood, aTotal, bGood, bTotal));
        Record(theirs, new TradeRecord(1, 1, bGood, bTotal, aGood, aTotal));
        if (!ExchangeTerms.HasAnotherRound(mine.Rounds, mine.Done, mine.Repeat)) { mine.Clear(); theirs.Clear(); }
        return 1;
    }

    static void Add2(Dictionary<(int, int, string), int> tally, (int, int, string) key, int amount) => tally[key] = tally.GetValueOrDefault(key) + amount;

    static void Record(Side side, TradeRecord record)
    {
        side.Ledger.Add(record);
        if (side.Ledger.Count > 20) side.Ledger.RemoveRange(0, side.Ledger.Count - 20);
    }

    // MoveGoods: the held goods pass to the other half (the game's TransferStock, which needs the room the other half keeps).
    static void MoveGoods(Half from, Half to, string good, int held)
    {
        if (held <= 0 || ExchangeTerms.IsSpecial(good)) return;
        int reserved = Math.Min(held, from.Reserved.GetValueOrDefault(good));
        Add(from.Reserved, good, -reserved);
        int amount = Math.Min(held, from.Unreserved(good));
        Check(amount == held, $"only {amount} of {held} {good} were on the half to cross");
        Add(from.Stock, good, -amount);
        Add(to.Stock, good, amount);
    }

    static int MoveSpecial(World w, Half from, Half to, string item, int amount, int movable)
    {
        if (amount <= 0) return amount;
        if (item == ExchangeTerms.Science)
        {
            w.Science[from.Owner] -= amount;
            w.Science[to.Owner] = w.Science.GetValueOrDefault(to.Owner) + amount;
        }
        else if (item == ExchangeTerms.Beavers)
        {
            int moved = Math.Min(amount, ExchangeTerms.BeaversToSpare(w.Adults[from.Owner], movable));
            w.Adults[from.Owner] -= moved;
            w.Adults[to.Owner] = w.Adults.GetValueOrDefault(to.Owner) + moved;
            return moved;
        }
        return amount;
    }

    static void Propose(World w, Half half, Random random)
    {
        Half partner = w.Partner(half);
        if (!w.Trading || half.X.IsOpen || partner.X.IsOpen) return;
        string[] items = Goods.Concat(new[] { ExchangeTerms.Science, ExchangeTerms.Beavers }).ToArray();
        string give = items[random.Next(items.Length)], get = items[random.Next(items.Length)];
        int giveAmount = give == ExchangeTerms.Beavers ? random.Next(0, 4) : random.Next(0, 101);
        int getAmount = get == ExchangeTerms.Beavers ? random.Next(0, 4) : random.Next(0, 101);
        int rounds = random.Next(1, 6);
        bool repeat = random.Next(5) == 0;
        int keep = random.Next(5) == 0 ? random.Next(0, 300) : 0;
        if (!ExchangeTerms.AreValid(give, giveAmount, get, getAmount) || !ExchangeTerms.AreValidRounds(rounds) || !ExchangeTerms.IsValidKeep(keep)) return;
        int serial = Math.Max(half.X.Serial, partner.X.Serial) + 1;
        Set(half.X, serial, true, give, giveAmount, rounds, repeat, half.Owner, keep);
        Set(partner.X, serial, false, get, getAmount, rounds, repeat, partner.Owner, 0);
        half.X.LastTerms = ExchangeTerms.EncodeTerms(give, giveAmount, get, getAmount, rounds, repeat, keep);
        partner.X.LastTerms = ExchangeTerms.EncodeTerms(get, getAmount, give, giveAmount, rounds, repeat, 0);
    }

    static void Set(Side side, int serial, bool here, string good, int total, int rounds, bool repeat, int colony, int keep)
    {
        side.Serial = serial; side.State = 1; side.ProposedHere = here; side.Good = ExchangeTerms.GoodOf(good, total); side.Total = total;
        side.Held = 0; side.Repeat = repeat; side.Rounds = repeat ? 1 : rounds; side.Done = 0; side.CancelAsked = false; side.Colony = colony;
        side.Keep = keep;
    }

    // A save and a load: every side through its saved form; reservations are not saved and are made again
    // (CrossingExchange.PostInitializeEntity); the totals through their saved form. Loads on their way stay on their way.
    static void SaveAndLoad(World w)
    {
        foreach (Half half in new[] { w.A, w.B })
        {
            Side side = half.X;
            var ledger = side.Ledger.Select(r => r.Encode()).ToList();
            side.Ledger.Clear();
            foreach (string entry in ledger) { Check(TradeRecord.TryDecode(entry, out TradeRecord record)); side.Ledger.Add(record); }
            if (side.LastTerms != null) Check(ExchangeTerms.TryDecodeTerms(side.LastTerms, out _, out _, out _, out _, out _, out _, out _), "saved terms do not load");
            half.Reserved.Clear();
            if (side.IsActive && side.GivesGoods && side.Held > 0)
            {
                int held = Math.Min(side.Held, half.Unreserved(side.Good!));
                Equal(side.Held, held, "after a load, the goods held on the half:");
                Add(half.Reserved, side.Good!, held);
            }
        }
        var totals = new TradeTotals();
        totals.Decode(w.Totals.Encode());
        Equal(w.Totals.Fingerprint(), totals.Fingerprint());
        w.Totals = totals;
    }

    // The post is removed (a deletion, a blast): both halves go, their stock is left as recovered goods.
    static void Remove(World w)
    {
        foreach (Half half in new[] { w.A, w.B })
            foreach (var good in half.Stock.ToList())
                if (good.Value > 0) Add(w.Recovered, good.Key, good.Value);
        w.Post = false;
        w.A = new Half { Owner = -1 };
        w.B = new Half { Owner = -1 };
    }

    static void Rebuild(World w)
    {
        w.A = new Half { Owner = 0, Road = 0 };
        w.B = new Half { Owner = 1, Road = 1 };
        w.Post = true;
    }

    static Dictionary<string, int> Count(World w)
    {
        var all = new Dictionary<string, int>();
        foreach (string good in Goods)
        {
            int total = w.Storage.Where(s => s.Key.Item2 == good).Sum(s => s.Value) + w.A.Amount(good) + w.B.Amount(good) + w.InFlight(good)
                + w.Recovered.GetValueOrDefault(good);
            all[good] = total;
        }
        all[ExchangeTerms.Science] = w.Science.Values.Sum();
        all[ExchangeTerms.Beavers] = w.Adults.Values.Sum();
        return all;
    }

    static void Invariants(World w, Dictionary<string, int> start, string at)
    {
        var now = Count(w);
        foreach (var item in start) Equal(item.Value, now[item.Key], $"{at} {item.Key} created or lost:");
        foreach (string good in Goods)
        {
            Check(w.Room(good) >= 0, $"{at} the post holds more {good} than its room: {w.A.Amount(good)} + {w.B.Amount(good)} + {w.Coming(good)} on the way");
            foreach (Half half in new[] { w.A, w.B })
            {
                Check(half.Amount(good) >= 0 && half.Reserved.GetValueOrDefault(good) >= 0 && half.Unreserved(good) >= 0, $"{at} {good} on a half went negative");
                Check(w.Storage.GetValueOrDefault((0, good)) >= 0 && w.Storage.GetValueOrDefault((1, good)) >= 0 && w.Storage.GetValueOrDefault((2, good)) >= 0,
                    $"{at} a colony's {good} went negative");
            }
        }
        foreach (Half half in new[] { w.A, w.B })
        {
            Side side = half.X;
            Check(side.Held >= 0 && side.Held <= Math.Max(0, side.Total), $"{at} held {side.Held} of {side.Total}");
            if (side.IsActive && side.GivesGoods)
                Check(half.Reserved.GetValueOrDefault(side.Good!) >= side.Held && half.Amount(side.Good!) >= side.Held, $"{at} the held goods are not reserved on the half");
            Check(side.Ledger.Count <= 20);
            Check(side.IsOpen == w.Partner(half).X.IsOpen, $"{at} one half holds an exchange alone");
        }
        Check(w.Science.Values.All(v => v >= 0) && w.Adults.Values.All(v => v >= 0), $"{at} a pool went negative");
        foreach (var crossed in w.Crossed) Equal(crossed.Value, w.Totals.Of(crossed.Key.Item1, crossed.Key.Item2, crossed.Key.Item3), $"{at} the totals of {crossed.Key}:");
    }

    static World NewWorld()
    {
        var w = new World();
        w.A.Owner = w.A.Road = 0;
        w.B.Owner = w.B.Road = 1;
        foreach (int colony in new[] { 0, 1, 2 })
        {
            foreach (string good in Goods) w.Storage[(colony, good)] = colony == 2 ? 0 : 1500;
            w.Science[colony] = colony == 2 ? 0 : 2000;
            w.Adults[colony] = colony == 2 ? 0 : 12;
        }
        return w;
    }

    static int Simulate(int seed, int steps)
    {
        var random = new Random(seed);
        World w = NewWorld();
        var start = Count(w);
        int crossed = 0;
        for (int step = 0; step < steps; step++)
        {
            string at = $"seed {seed} step {step}:";
            int roll = random.Next(1000);
            Half half = random.Next(2) == 0 ? w.A : w.B;
            Side mine = half.X, theirs = w.Partner(half).X;
            if (roll < 80) Propose(w, half, random);
            else if (roll < 140)
            {
                // The colony it was offered to accepts (as it saw it), or declines; the offering one may withdraw.
                if (mine.State == 1 && theirs.State == 1 && w.Trading)
                {
                    if (random.Next(5) > 0) { if (!mine.ProposedHere) { mine.State = 2; theirs.State = 2; } }
                    else End(w);
                }
            }
            else if (roll < 155)
            {
                // Cancel: a colony asks; the other agrees (it ends) or keeps trading; at a paused post a colony ends it alone.
                if (mine.IsActive)
                {
                    bool otherCanAnswer = w.Trading && w.Partner(half).Owner == theirs.Colony;
                    if (!otherCanAnswer) End(w);
                    // The other colony agrees, or either keeps trading (the asker withdraws, or the other turns it down).
                    else if (theirs.CancelAsked && random.Next(2) == 0) End(w);
                    else if (mine.CancelAsked || theirs.CancelAsked) { mine.CancelAsked = false; theirs.CancelAsked = false; }
                    else if (random.Next(4) == 0) mine.CancelAsked = true;
                }
            }
            else if (roll < 165) { if (mine.IsActive) mine.Keep = random.Next(3) == 0 ? 0 : random.Next(0, 300); }
            else if (roll < 470) Trip(w, half, random);
            else if (roll < 720) { if (w.OnTheWay.Count > 0) Arrive(w, random.Next(w.OnTheWay.Count)); }
            else if (roll < 820) Empty(w, half, random);
            else if (roll < 940) crossed += CheckPost(w, random);
            // Disruptions, each short-lived: a road removed, a half paused or flooded, its workers gone, no storage room.
            else if (roll < 945) { if (w.Post) { if (w.MidRound) World.PausedMidRound++; half.Owner = -1; } }
            else if (roll < 975)
            {
                if (!w.Post) { if (random.Next(4) == 0) Rebuild(w); }
                else if (half.Owner < 0) half.Owner = half.Road;
                else if (random.Next(2) == 0) half.Blocked = false;
                else half.Workers = 2;
            }
            else if (roll < 978) half.Blocked = true;
            else if (roll < 980) half.Workers = 0;
            else if (roll < 983) w.NoStorage.Add((random.Next(2), Goods[random.Next(Goods.Length)]));
            else if (roll < 988) w.NoStorage.Clear();
            else if (roll < 995) { if (w.MidRound) World.SavedMidRound++; SaveAndLoad(w); }
            else if (roll < 996 && random.Next(3) == 0) { if (w.MidRound) World.HandedOverMidRound++; HandOver(w); }
            else if (roll < 997 && random.Next(3) == 0) { if (w.Post) { if (w.MidRound) World.RemovedMidRound++; Remove(w); } }
            else if (roll < 999)
            {
                // Recovered goods are picked up by whichever colony's workers get there first.
                foreach (var good in w.Recovered.ToList())
                    if (good.Value > 0) { Add(w.Storage, (random.Next(2), good.Key), good.Value); w.Recovered[good.Key] = 0; }
            }
            else if (!w.Post) Rebuild(w);
            Invariants(w, start, at);
        }
        // Settle: nothing in the way any more. A running exchange crosses again, and one of rounds finishes.
        Settle(w);
        start = Count(w);
        int serial = Math.Max(w.A.X.Serial, w.B.X.Serial);
        bool open = w.A.X.IsOpen;
        bool repeat = w.A.X.Repeat || w.B.X.Repeat;
        int crossedBefore = crossed;
        for (int step = 0; step < 20000 && (w.A.X.IsOpen || w.B.X.IsOpen); step++)
        {
            if (w.A.X.State == 1) { (w.A.X.ProposedHere ? w.B : w.A).X.State = 2; (w.A.X.ProposedHere ? w.A : w.B).X.State = 2; }
            Trip(w, w.A, random); Trip(w, w.B, random);
            while (w.OnTheWay.Count > 0) Arrive(w, 0);
            Empty(w, w.A, random); Empty(w, w.B, random);
            crossed += CheckPost(w, random);
            Invariants(w, start, $"seed {seed} settling:");
            if (repeat && crossed > crossedBefore + 2) break;
        }
        Check(!open || crossed > crossedBefore, $"seed {seed}: exchange {serial} never crossed again once nothing was in its way");
        Check(repeat || !(w.A.X.IsOpen || w.B.X.IsOpen), $"seed {seed}: exchange {serial} of {w.A.X.Rounds} rounds stuck at {w.A.X.Done} done");
        return crossed;
    }

    static void HandOver(World w)
    {
        // Colony 1 goes to colony 2: its districts (the half its road reaches), its storage, science and beavers.
        foreach (string good in Goods) { Add(w.Storage, (2, good), w.Storage.GetValueOrDefault((1, good))); w.Storage[(1, good)] = 0; }
        w.Science[2] += w.Science[1]; w.Science[1] = 0;
        w.Adults[2] += w.Adults[1]; w.Adults[1] = 0;
        foreach (Half half in new[] { w.A, w.B })
        {
            if (half.Road == 1) half.Road = 2;
            if (half.Owner == 1) half.Owner = 2;
        }
        // ColonyLifecycle.Transfer ends an exchange at a post that no longer joins two colonies.
        if (w.A.X.IsOpen && !w.Trading) End(w);
    }

    // Everything back in place: both roads, workers, storage room, stock, science and beavers.
    static void Settle(World w)
    {
        if (!w.Post) Rebuild(w);
        foreach (Half half in new[] { w.A, w.B })
        {
            half.Owner = half.Road;
            half.Blocked = false;
            half.Workers = 2;
            half.X.CancelAsked = false;
            half.X.Keep = 0;
        }
        w.NoStorage.Clear();
        foreach (int colony in new[] { 0, 1, 2 })
        {
            foreach (string good in Goods) Add(w.Storage, (colony, good), 5000);
            w.Science[colony] += 100000;
            w.Adults[colony] += 1000;
        }
        // (The totals of what exists changed: the invariants count from here.)
    }

    // beta24's stuck round (C4): colony 0 received 100 Water through the post, has no tank room for it, and now gives 100
    // Water in a new exchange. Its half's room for Water is full of that Water, which was never held.
    static bool Stuck(bool holdWaiting)
    {
        var random = new Random(9);
        World w = NewWorld();
        w.HoldWaiting = holdWaiting;
        w.A.Stock["Water"] = 100;
        w.NoStorage.Add((0, "Water"));
        Set(w.A.X, 1, true, "Water", 100, 1, false, 0, 0);
        Set(w.B.X, 1, false, "Log", 50, 1, false, 1, 0);
        w.A.X.State = w.B.X.State = 2;
        for (int step = 0; step < 2000; step++)
        {
            Trip(w, w.A, random); Trip(w, w.B, random);
            while (w.OnTheWay.Count > 0) Arrive(w, 0);
            Empty(w, w.A, random); Empty(w, w.B, random);
            if (CheckPost(w, random) > 0) return false;
        }
        return true;
    }
}
