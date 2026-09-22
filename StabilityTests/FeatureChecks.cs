using BeaverBuddies.Colonies;
using BeaverBuddies.Panel;
using System.Globalization;

// The beta2 features, checked without a game: food and water days, the reserve on a standing deal, the wishlist, who
// may look after whose colony, the host's start gate, the hand-over warning, and the connection panel's joining line.
static class FeatureChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"expected [{expected}], got [{actual}]");

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        // ---- food and water days ----

        yield return ("Supplies: days last at yesterday's use, at today's once a quarter of the day has passed, else unknown", () =>
        {
            Equal(3f, SupplyDays.Estimate(300, 100, 0, 0.9f));
            // Today so far: 20 used in a quarter day is 80 a day.
            Equal(2.5f, SupplyDays.Estimate(200, 0, 20, 0.25f));
            Check(SupplyDays.Estimate(200, 0, 20, 0.2f) == null, "too early in the day to say");
            Check(SupplyDays.Estimate(200, 0, 0, 0.9f) == null, "nothing used: no rate");
            Equal(0f, SupplyDays.Estimate(0, 50, 0, 0.5f));
            Equal(0f, SupplyDays.Estimate(-5, 50, 0, 0.5f));
            // Yesterday's rate wins over today's.
            Equal(1f, SupplyDays.Estimate(100, 100, 400, 0.5f));
        });

        yield return ("Supplies: days read as 3.4, 34 or 99+, and under a day is low", () =>
        {
            Equal("3.4", SupplyDays.Format(3.44f));
            Equal("0.5", SupplyDays.Format(0.5f));
            Equal("34", SupplyDays.Format(34.9f));
            Equal("99+", SupplyDays.Format(120f));
            Check(SupplyDays.Format(null) == null);
            Check(SupplyDays.IsLow(0.9f) && !SupplyDays.IsLow(1f) && !SupplyDays.IsLow(null));
        });

        // ---- the reserve ----

        yield return ("Exchange: a side gives a round only while what it has, less the round, is at least what it keeps", () =>
        {
            Check(ExchangeTerms.CanSpare(300, 100, 200));
            Check(!ExchangeTerms.CanSpare(299, 100, 200));
            Check(ExchangeTerms.CanSpare(100, 100, 0));
            Check(ExchangeTerms.CanSpare(100, 100, -5), "a negative reserve counts as none");
            Check(!ExchangeTerms.CanSpare(50, 100, 0));
            Check(ExchangeTerms.IsValidKeep(0) && ExchangeTerms.IsValidKeep(ExchangeTerms.MaxKeep));
            Check(!ExchangeTerms.IsValidKeep(-1) && !ExchangeTerms.IsValidKeep(ExchangeTerms.MaxKeep + 1));
        });

        yield return ("Exchange: workers bring nothing while the round would eat into the reserve, and what waits stays", () =>
        {
            // 230 in stock (the 40 on the half count), keep 200, round of 100: the round is not affordable.
            Equal(0, ExchangeTerms.StillToBringKeeping(100, 40, 0, 230, 200));
            // 300 in stock: bring the 60 missing.
            Equal(60, ExchangeTerms.StillToBringKeeping(100, 40, 0, 300, 200));
            Equal(50, ExchangeTerms.StillToBringKeeping(100, 40, 10, 300, 200));
            // No reserve: as before.
            Equal(60, ExchangeTerms.StillToBringKeeping(100, 40, 0, 50, 0));
        });

        yield return ("Exchange: an exchange's terms survive as one string, and a broken one is refused", () =>
        {
            string text = ExchangeTerms.EncodeTerms("Log", 100, "Gear", 25, 4, false, 200);
            Check(ExchangeTerms.TryDecodeTerms(text, out string give, out int giveAmount, out string get, out int getAmount, out int rounds, out bool repeat, out int keep));
            Equal("Log", give); Equal(100, giveAmount); Equal("Gear", get); Equal(25, getAmount); Equal(4, rounds); Check(!repeat); Equal(200, keep);
            // A gift: the empty side has no good.
            Check(ExchangeTerms.TryDecodeTerms(ExchangeTerms.EncodeTerms("Log", 100, null, 0, 1, true, 0), out give, out giveAmount, out get, out getAmount, out rounds, out repeat, out keep));
            Check(get == null && getAmount == 0 && repeat);
            Check(!ExchangeTerms.TryDecodeTerms("Log|100|Log|25|4|0|0", out _, out _, out _, out _, out _, out _, out _), "the same good both ways");
            Check(!ExchangeTerms.TryDecodeTerms("Log|100|Gear|25|0|0|0", out _, out _, out _, out _, out _, out _, out _), "0 rounds");
            Check(!ExchangeTerms.TryDecodeTerms("Log|100|Gear|25|4|0|10000", out _, out _, out _, out _, out _, out _, out _), "a reserve past the most");
            Check(!ExchangeTerms.TryDecodeTerms("", out _, out _, out _, out _, out _, out _, out _));
            Check(!ExchangeTerms.TryDecodeTerms(null, out _, out _, out _, out _, out _, out _, out _));
        });

        yield return ("Exchange: the keep box holds a whole number from 0 to 9999, empty means 0, and steps by 50 (Shift: 10)", () =>
        {
            Check(TradeOfferForm.TryReadKeep("", out int keep) && keep == 0);
            Check(TradeOfferForm.TryReadKeep(" 250 ", out keep) && keep == 250);
            Check(TradeOfferForm.TryReadKeep("9999", out keep) && keep == 9999);
            Check(!TradeOfferForm.TryReadKeep("10000", out _));
            Check(!TradeOfferForm.TryReadKeep("-5", out _));
            Check(!TradeOfferForm.TryReadKeep("2a", out _));
            Equal(50, TradeOfferForm.KeepStep(false));
            Equal(10, TradeOfferForm.KeepStep(true));
            Equal(250, TradeOfferForm.Stepped(230, 50, true, 0, ExchangeTerms.MaxKeep));
            Equal(200, TradeOfferForm.Stepped(230, 50, false, 0, ExchangeTerms.MaxKeep));
            Equal(0, TradeOfferForm.Stepped(0, 50, false, 0, ExchangeTerms.MaxKeep));
        });

        // ---- the wishlist ----

        yield return ("Wishlist: distinct known items only, three at most, in the order given", () =>
        {
            var known = new HashSet<string> { "Log", "Gear", "Plank", "Bread" };
            var wishes = WishlistTerms.Normalize(new[] { "Log", "", null, "Log", "Unknown", "Gear", "Plank", "Bread" }, known.Contains);
            Check(wishes.SequenceEqual(new[] { "Log", "Gear", "Plank" }), string.Join(",", wishes));
            Equal(0, WishlistTerms.Normalize(null, known.Contains).Count);
            Equal(2, WishlistTerms.Normalize(new[] { "A", "B" }, null).Count);
        });

        yield return ("Wishlist: a colony's wishes survive as one line, and a bad line is refused", () =>
        {
            string line = WishlistTerms.Encode(2, new[] { "Log", "Gear" });
            Equal("2|Log,Gear", line);
            Check(WishlistTerms.TryDecode(line, out int slot, out List<string> items));
            Equal(2, slot);
            Check(items.SequenceEqual(new[] { "Log", "Gear" }));
            Check(WishlistTerms.TryDecode("1|", out slot, out items) && items.Count == 0);
            Check(!WishlistTerms.TryDecode("9|Log", out _, out _), "no such slot");
            Check(!WishlistTerms.TryDecode("Log", out _, out _));
            Check(!WishlistTerms.TryDecode(null, out _, out _));
        });

        // ---- looking after a colony ----

        yield return ("Steward: an owner may ask any other known player; the host only for an absent owner; never the owner themself", () =>
        {
            const string sarah = "steam:2", kyler = "steam:1";
            // The owner (seat 1) asks Sarah.
            Check(ColonyStewardRules.MayGrant(actorIsHost: false, actorSeat: 1, slot: 1, ownerPresent: true, stewardId: sarah, ownerId: kyler, stewardKnown: true));
            // Not for another colony.
            Check(!ColonyStewardRules.MayGrant(false, 0, 1, true, sarah, kyler, true));
            // The host, for an absent owner's colony: yes; for a present owner's: no.
            Check(ColonyStewardRules.MayGrant(true, 0, 1, false, sarah, kyler, true));
            Check(!ColonyStewardRules.MayGrant(true, 0, 1, true, sarah, kyler, true));
            // The owner themself, an unknown player, no id, no such slot.
            Check(!ColonyStewardRules.MayGrant(false, 1, 1, true, kyler, kyler, true));
            Check(!ColonyStewardRules.MayGrant(false, 1, 1, true, sarah, kyler, false));
            Check(!ColonyStewardRules.MayGrant(false, 1, 1, true, "", kyler, true));
            Check(!ColonyStewardRules.MayGrant(false, 1, 4, true, sarah, kyler, true));
            // A colony nobody plays yet (no owner id) may still be given a steward by the host.
            Check(ColonyStewardRules.MayGrant(true, 0, 2, false, sarah, null, true));
        });

        yield return ("Steward: the owner, the steward or the host ends it; nobody else", () =>
        {
            const string sarah = "steam:2", bob = "steam:3";
            Check(ColonyStewardRules.MayRevoke(actorIsHost: false, actorSeat: 1, actorId: "steam:1", slot: 1, stewardId: sarah));
            Check(ColonyStewardRules.MayRevoke(false, 2, sarah, 1, sarah));
            Check(ColonyStewardRules.MayRevoke(true, 0, "steam:0", 1, sarah));
            Check(!ColonyStewardRules.MayRevoke(false, 3, bob, 1, sarah));
            Check(!ColonyStewardRules.MayRevoke(false, 1, "steam:1", 1, null), "nothing to end");
            Check(!ColonyStewardRules.MayRevoke(true, 0, "steam:0", -1, sarah));
        });

        yield return ("Steward: a player acts as their own seat, or as a colony they look after, and nothing else", () =>
        {
            const string sarah = "steam:2";
            Check(ColonyStewardRules.MayActAs(actorSeat: 2, actorId: sarah, slot: -1, stewardId: null));
            Check(ColonyStewardRules.MayActAs(2, sarah, 2, null));
            Check(ColonyStewardRules.MayActAs(2, sarah, 1, sarah));
            Check(!ColonyStewardRules.MayActAs(2, sarah, 1, "steam:3"));
            Check(!ColonyStewardRules.MayActAs(2, sarah, 1, null));
            Check(!ColonyStewardRules.MayActAs(2, null, 1, sarah));
            Check(!ColonyStewardRules.MayActAs(2, sarah, 4, sarah));
        });

        // ---- the host's start gate ----

        yield return ("Start gate: only the host, at tick 0, with joining open, for a change, before saying yes", () =>
        {
            Check(HostStartRules.ShouldHold(isHost: true, acceptingClients: true, ticksSinceLoad: 0, changesGame: true, confirmed: false));
            Check(!HostStartRules.ShouldHold(false, true, 0, true, false), "a guest is never held here (the host refuses it instead)");
            Check(!HostStartRules.ShouldHold(true, false, 0, true, false), "joining already closed");
            Check(!HostStartRules.ShouldHold(true, true, 1, true, false), "the game has started");
            Check(!HostStartRules.ShouldHold(true, true, 0, false, false), "the speed or a greeting changes nothing");
            Check(!HostStartRules.ShouldHold(true, true, 0, true, true), "the host already said to start");
        });

        // ---- the hand-over warning ----

        yield return ("Absence: the day the count reaches the limit is the day to warn; the next check hands over; 0 is never", () =>
        {
            Check(!ColonyAbsence.IsDueTomorrow(6, 7) && ColonyAbsence.IsDueTomorrow(7, 7) && !ColonyAbsence.IsDueTomorrow(8, 7));
            Check(!ColonyAbsence.IsDue(6, 7) && ColonyAbsence.IsDue(7, 7) && ColonyAbsence.IsDue(8, 7));
            Check(!ColonyAbsence.IsDue(30, 0) && !ColonyAbsence.IsDueTomorrow(0, 0));
            Equal(1, ColonyAbsence.DaysLeft(6, 7));
            Equal(0, ColonyAbsence.DaysLeft(9, 7));
            Check(ColonyAbsence.DaysLeft(3, 0) == null);
        });

        // ---- the connection panel ----

        yield return ("Panel: the host sees that joining is open, and only while it is; a guest never does", () =>
        {
            var english = new Dictionary<string, string>
            {
                ["BeaverBuddies.Panel.Host"] = "Host", ["BeaverBuddies.Panel.Guest"] = "Guest",
                ["BeaverBuddies.Panel.PlayersOne"] = "{0} player", ["BeaverBuddies.Panel.PlayersMany"] = "{0} players",
                ["BeaverBuddies.Panel.PingValue"] = "{0} ms", ["BeaverBuddies.Panel.Measuring"] = "...",
                ["BeaverBuddies.Panel.StatusInSync"] = "In sync", ["BeaverBuddies.Panel.TickRateValue"] = "{0} ticks/s",
                ["BeaverBuddies.Panel.SpeedValue"] = "{0}x", ["BeaverBuddies.Panel.Paused"] = "Paused",
                ["BeaverBuddies.Panel.TicksOne"] = "{0} tick", ["BeaverBuddies.Panel.TicksMany"] = "{0} ticks",
                ["BeaverBuddies.Panel.FpsFloorOff"] = "Off", ["BeaverBuddies.Panel.JoiningOpen"] = "open",
            };
            string T(string key, object[] args) => string.Format(CultureInfo.InvariantCulture, english[key], args);
            var host = new PanelInputs { IsHost = true, Speed = 0, JoiningOpen = true };
            host.Players.Add(new PanelPlayer { Id = 0, Name = "Kyler", IsYou = true, IsHost = true });
            Equal("open", PanelModelBuilder.Build(host, T).JoiningText);
            host.JoiningOpen = false;
            Check(PanelModelBuilder.Build(host, T).JoiningText == null);
            var guest = new PanelInputs { IsHost = false, Speed = 1, JoiningOpen = true };
            guest.Players.Add(new PanelPlayer { Id = 0, Name = "Kyler", IsHost = true });
            guest.Players.Add(new PanelPlayer { Id = 1, Name = "Sarah", IsYou = true });
            Check(PanelModelBuilder.Build(guest, T).JoiningText == null);
            // A row carries its player's number, so a click can lead to them.
            Check(PanelModelBuilder.Build(guest, T).Rows.Select(r => r.Id).SequenceEqual(new[] { 0, 1 }));
        });
    }
}
