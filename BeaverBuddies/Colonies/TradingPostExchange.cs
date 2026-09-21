using BeaverBuddies.Events;
using BeaverBuddies.Util;
using System;
using Timberborn.BaseComponentSystem;
using Timberborn.DistributionSystem;
using Timberborn.TickSystem;
using Timberborn.GameDistricts;
using Timberborn.EntitySystem;
using System.Linq;
using Timberborn.Goods;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    public enum ExchangeState { None = 0, Proposed = 1, Active = 2 }

    /// <summary>
    /// One side of a barter at a trading post, saved with its half of the crossing: the good this half's colony gives,
    /// how many, and how many have crossed so far. The partner half holds the other side. Both sides are set, and
    /// cleared, together, by the same action or delivery on every computer.
    /// </summary>
    public class CrossingExchange : BaseComponent, IPersistentEntity
    {
        private static readonly ComponentKey ExchangeKey = new ComponentKey("BeaverBuddies.CrossingExchange");
        private static readonly PropertyKey<int> StateKey = new PropertyKey<int>("State");
        private static readonly PropertyKey<int> ProposedHereKey = new PropertyKey<int>("ProposedHere");
        private static readonly PropertyKey<string> GoodKey = new PropertyKey<string>("Good");
        private static readonly PropertyKey<int> TotalKey = new PropertyKey<int>("Total");
        private static readonly PropertyKey<int> SentKey = new PropertyKey<int>("Sent");
        private static readonly PropertyKey<int> SerialKey = new PropertyKey<int>("Serial");
        private static readonly PropertyKey<int> RepeatKey = new PropertyKey<int>("Repeat");
        private static readonly PropertyKey<int> RoundsKey = new PropertyKey<int>("Rounds");

        public ExchangeState State { get; private set; }
        /// <summary>This half's colony made the offer (the other one accepts or declines).</summary>
        public bool ProposedHere { get; private set; }
        /// <summary>What this half's colony gives.</summary>
        public string GoodId { get; private set; }
        public int Total { get; private set; }
        public int Sent { get; private set; }
        /// <summary>
        /// Which exchange at this crossing this is (both halves agree). It keeps counting after an exchange ends, so an
        /// answer or a cancel meant for one exchange never reaches the next.
        /// </summary>
        public int Serial { get; private set; }
        /// <summary>Starts again, on the same terms, each time both sides have delivered, until a colony cancels it.</summary>
        public bool Repeat { get; private set; }
        /// <summary>How many times a repeating exchange has completed.</summary>
        public int Rounds { get; private set; }

        public int Remaining => Math.Max(0, Total - Sent);
        public bool IsOpen => State != ExchangeState.None;
        public bool IsActive => State == ExchangeState.Active;

        internal void Propose(int serial, bool proposedHere, string goodId, int total, bool repeat)
        {
            Serial = serial;
            State = ExchangeState.Proposed;
            ProposedHere = proposedHere;
            GoodId = goodId;
            Total = total;
            Sent = 0;
            Repeat = repeat;
            Rounds = 0;
        }

        /// <summary>A repeating exchange completed a round: again from the start.</summary>
        internal void NextRound()
        {
            Sent = 0;
            Rounds++;
        }

        internal void Activate() => State = ExchangeState.Active;

        internal void Clear()
        {
            State = ExchangeState.None;
            ProposedHere = false;
            GoodId = null;
            Total = 0;
            Sent = 0;
            Repeat = false;
            Rounds = 0;
        }

        /// <summary>Counts goods that passed to the partner; returns how many counted towards this side.</summary>
        internal int Count(int amount)
        {
            int counted = ExchangeTerms.Counted(Total, Sent, amount);
            Sent += counted;
            return counted;
        }

        public void Save(IEntitySaver entitySaver)
        {
            if (!IsOpen && Serial == 0) return;
            IObjectSaver saver = entitySaver.GetComponent(ExchangeKey);
            saver.Set(SerialKey, Serial);
            if (!IsOpen) return;
            saver.Set(StateKey, (int)State);
            saver.Set(ProposedHereKey, ProposedHere ? 1 : 0);
            if (!string.IsNullOrEmpty(GoodId)) saver.Set(GoodKey, GoodId);
            saver.Set(TotalKey, Total);
            saver.Set(SentKey, Sent);
            if (Repeat) saver.Set(RepeatKey, 1);
            if (Rounds > 0) saver.Set(RoundsKey, Rounds);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (!entityLoader.TryGetComponent(ExchangeKey, out IObjectLoader loader)) return;
            if (loader.Has(SerialKey)) Serial = Math.Max(0, loader.Get(SerialKey));
            int state = loader.Has(StateKey) ? loader.Get(StateKey) : 0;
            string good = loader.Has(GoodKey) ? loader.Get(GoodKey) : null;
            int total = loader.Has(TotalKey) ? loader.Get(TotalKey) : 0;
            // A side that gives nothing (a gift the other way) has no good and 0.
            if (state < (int)ExchangeState.Proposed || state > (int)ExchangeState.Active || total < 0
                || (total > 0 && string.IsNullOrEmpty(good)))
                return;
            State = (ExchangeState)state;
            ProposedHere = loader.Has(ProposedHereKey) && loader.Get(ProposedHereKey) != 0;
            GoodId = total > 0 ? good : null;
            Total = total;
            Sent = loader.Has(SentKey) ? Math.Max(0, Math.Min(total, loader.Get(SentKey))) : 0;
            Repeat = loader.Has(RepeatKey) && loader.Get(RepeatKey) != 0;
            Rounds = loader.Has(RoundsKey) ? Math.Max(0, loader.Get(RoundsKey)) : 0;
        }
    }

    /// <summary>
    /// Barter at trading posts: one colony offers "this many of my good for that many of yours", the other accepts,
    /// and each colony's beavers bring their side to the crossing while the other colony's beavers haul it away,
    /// in step, until both amounts have crossed. The rules live here; the carrying and counting are the patches in
    /// ColonyTrading.cs, and the offers arrive as the actions below.
    /// </summary>
    public class ColonyExchangeService : RegisteredSingleton, ILoadableSingleton, ITickableSingleton
    {
        // Science and beavers move by themselves, this often (ticks), as far as the pace allows.
        private const int SpecialItemInterval = 16;
        // At most this many beavers move at once, so a colony has time to house them.
        private const int BeaversAtOnce = 1;
        // At most this much science moves at once: a large or repeating science deal runs over a while instead of
        // emptying a pool in a moment.
        private const int ScienceAtOnce = 25;

        private readonly IGoodService _goodService;
        private readonly ColonyRulesService _colonyRulesService;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private int ticks;

        public static ColonyExchangeService Instance => SingletonManager.GetSingleton<ColonyExchangeService>();

        public ColonyExchangeService(IGoodService goodService, ColonyRulesService colonyRulesService,
            EntityComponentRegistry entityComponentRegistry)
        {
            _goodService = goodService;
            _colonyRulesService = colonyRulesService;
            _entityComponentRegistry = entityComponentRegistry;
        }

        // Loadable only so the game builds it at load: it is found through SingletonManager, not injected.
        public void Load() { }

        public static CrossingExchange Of(DistrictCrossing half) => half ? half.GetComponent<CrossingExchange>() : null;

        /// <summary>
        /// How many more of its good this half's colony may bring now: nothing without a running exchange, and nothing
        /// while it waits for the other colony to keep pace.
        /// </summary>
        public static int StillToBring(DistrictCrossing half)
        {
            if (!TradingPosts.IsTradingPost(half)) return 0;
            CrossingExchange mine = Of(half);
            CrossingExchange theirs = Of(TradingPosts.Partner(half));
            if (mine == null || theirs == null || !mine.IsActive || !theirs.IsActive) return 0;
            return ExchangeTerms.StillToBring(mine.Total, mine.Sent, theirs.Total, theirs.Sent);
        }

        /// <summary>
        /// How many of <paramref name="goodId"/> may pass from this half to its partner now: what its colony still owes
        /// in a running exchange of that good, else nothing. Loads still on the way when an exchange ends stay on
        /// their own half, and their colony's workers carry them home.
        /// </summary>
        public static int MayPass(DistrictCrossing half, string goodId)
        {
            CrossingExchange mine = Of(half);
            CrossingExchange theirs = Of(TradingPosts.Partner(half));
            if (mine == null || theirs == null || !mine.IsActive || !theirs.IsActive || mine.GoodId != goodId) return 0;
            return mine.Remaining;
        }

        /// <summary>The good this half's colony is giving in a running exchange, or null.</summary>
        public static string GoodGiven(DistrictCrossing half)
        {
            CrossingExchange mine = Of(half);
            return mine != null && mine.IsActive ? mine.GoodId : null;
        }

        /// <summary>
        /// Goods of <paramref name="goodId"/> passed from <paramref name="sender"/> to its partner. Returns how many
        /// counted towards the exchange (the rest counts as ordinary trade), and completes it when both sides are done.
        /// </summary>
        public int CountDelivery(DistrictCrossing sender, string goodId, int amount)
        {
            CrossingExchange mine = Of(sender);
            DistrictCrossing receiver = TradingPosts.Partner(sender);
            CrossingExchange theirs = Of(receiver);
            if (mine == null || theirs == null || !mine.IsActive || !theirs.IsActive || mine.GoodId != goodId) return 0;
            int counted = mine.Count(amount);
            if (ExchangeTerms.IsComplete(mine.Total, mine.Sent, theirs.Total, theirs.Sent) && mine.Repeat && theirs.Repeat)
            {
                // A repeating exchange: the next round starts at once, on the same terms.
                mine.NextRound();
                theirs.NextRound();
                Plugin.Log($"[Colony] Exchange {mine.Serial} round {mine.Rounds} complete: slot {OwnerOf(sender)} gave {mine.Total} {mine.GoodId}, "
                    + $"slot {OwnerOf(receiver)} gave {theirs.Total} {theirs.GoodId}");
            }
            else if (ExchangeTerms.IsComplete(mine.Total, mine.Sent, theirs.Total, theirs.Sent))
            {
                int from = OwnerOf(sender), to = OwnerOf(receiver);
                int myTotal = mine.Total, theirTotal = theirs.Total;
                string myGood = mine.GoodId, theirGood = theirs.GoodId;
                // The state changes first: this runs inside the game's delivery, and nothing after it may fail.
                mine.Clear();
                theirs.Clear();
                Plugin.Log($"[Colony] Exchange complete: slot {from} gave {myTotal} {myGood}, slot {to} gave {theirTotal} {theirGood}");
                Tell(() => from, () => to, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.Complete"),
                    Amount(myTotal, myGood), Amount(theirTotal, theirGood)), warning: false);
            }
            return counted;
        }

        // ---- the three actions, played on every computer (only saved state is read) ----

        /// <summary>Why an offer cannot be made from this half, or null.</summary>
        public string WhyNotPropose(DistrictCrossing half, int actorSlot, string giveGood, int giveAmount, string getGood, int getAmount)
        {
            if (!half) return "no such crossing";
            if (!TradingPosts.IsTradingPost(half)) return "the crossing is not between two colonies";
            if (OwnerOf(half) != actorSlot) return $"the half is slot {OwnerOf(half)}'s, not slot {actorSlot}'s";
            if (!ExchangeTerms.AreValid(giveGood, giveAmount, getGood, getAmount)) return "the terms are not valid";
            if ((giveAmount > 0 && !IsKnownItem(giveGood)) || (getAmount > 0 && !IsKnownItem(getGood)))
                return "a good is unknown in this game, or science is not separate";
            CrossingExchange mine = Of(half), theirs = Of(TradingPosts.Partner(half));
            if (mine == null || theirs == null) return "the crossing cannot hold an exchange";
            if (mine.IsOpen || theirs.IsOpen) return "an exchange is already open here";
            return null;
        }

        /// <summary>A good of this game, science (only when each colony has its own), or beavers.</summary>
        public bool IsKnownItem(string item) =>
            item == ExchangeTerms.Beavers || (item == ExchangeTerms.Science ? ColonyScienceService.IsEnabled : _goodService.HasGood(item));

        public void Propose(DistrictCrossing half, int actorSlot, string giveGood, int giveAmount, string getGood, int getAmount,
            bool repeat)
        {
            string why = WhyNotPropose(half, actorSlot, giveGood, giveAmount, getGood, getAmount);
            if (why != null)
            {
                Plugin.LogWarning($"[Colony] Exchange offer skipped: {why}");
                Tell(() => actorSlot, null, () => T("BeaverBuddies.Colony.Trade.Notice.OfferFailed"), warning: true);
                return;
            }
            DistrictCrossing partner = TradingPosts.Partner(half);
            CrossingExchange mine = Of(half), theirs = Of(partner);
            // Both halves agree on the number; it only ever grows.
            int serial = Math.Max(mine.Serial, theirs.Serial) + 1;
            mine.Propose(serial, true, ExchangeTerms.GoodOf(giveGood, giveAmount), giveAmount, repeat);
            theirs.Propose(serial, false, ExchangeTerms.GoodOf(getGood, getAmount), getAmount, repeat);
            int from = OwnerOf(half), to = OwnerOf(partner);
            Plugin.Log($"[Colony] Slot {from} offers {giveAmount} {giveGood} for {getAmount} {getGood} from slot {to} (exchange {serial})");
            Tell(() => to, null, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.Proposed"),
                ColonyName(from), Amount(giveAmount, giveGood), Amount(getAmount, getGood)), warning: false);
        }

        /// <summary>
        /// The colony of <paramref name="half"/> accepts the offer made to it, as its player saw it: the exchange's
        /// number and terms are checked again, so an offer withdrawn and made again is never accepted by mistake.
        /// </summary>
        public void Accept(DistrictCrossing half, int actorSlot, int serial, string giveGood, int giveAmount, string getGood, int getAmount,
            bool repeat)
        {
            CrossingExchange mine = Of(half);
            DistrictCrossing partner = TradingPosts.Partner(half);
            CrossingExchange theirs = Of(partner);
            string why = null;
            if (!TradingPosts.IsTradingPost(half) || mine == null || theirs == null
                || mine.State != ExchangeState.Proposed || theirs.State != ExchangeState.Proposed || mine.ProposedHere)
                why = "no offer is waiting here";
            else if (OwnerOf(half) != actorSlot)
                why = $"the half is slot {OwnerOf(half)}'s, not slot {actorSlot}'s";
            else if (mine.Serial != serial || mine.Repeat != repeat || mine.Total != giveAmount || theirs.Total != getAmount
                || mine.GoodId != ExchangeTerms.GoodOf(giveGood, giveAmount) || theirs.GoodId != ExchangeTerms.GoodOf(getGood, getAmount))
                why = "the offer changed";
            if (why != null)
            {
                Plugin.LogWarning($"[Colony] Exchange acceptance skipped: {why}");
                Tell(() => actorSlot, null, () => T("BeaverBuddies.Colony.Trade.Notice.AcceptFailed"), warning: true);
                return;
            }
            mine.Activate();
            theirs.Activate();
            int me = OwnerOf(half), them = OwnerOf(partner);
            Plugin.Log($"[Colony] Slot {me} accepted exchange {serial}: {getAmount} {getGood} for {giveAmount} {giveGood}");
            Tell(() => them, null, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.Accepted"),
                ColonyName(me), Amount(theirs.Total, theirs.GoodId), Amount(mine.Total, mine.GoodId)), warning: false);
        }

        /// <summary>
        /// Either colony ends exchange <paramref name="serial"/> from its own half: declines or withdraws an offer, or
        /// stops a running exchange. A late cancel for an exchange that already ended does nothing.
        /// </summary>
        public void Cancel(DistrictCrossing half, int actorSlot, int serial)
        {
            CrossingExchange mine = Of(half);
            DistrictCrossing partner = TradingPosts.Partner(half);
            CrossingExchange theirs = Of(partner);
            if (mine == null || !mine.IsOpen || mine.Serial != serial)
            {
                Plugin.LogWarning($"[Colony] Exchange cancel skipped: exchange {serial} is not open here");
                return;
            }
            if (OwnerOf(half) != actorSlot)
            {
                Plugin.LogWarning($"[Colony] Exchange cancel skipped: the half is slot {OwnerOf(half)}'s, not slot {actorSlot}'s");
                return;
            }
            int me = OwnerOf(half), them = OwnerOf(partner);
            string key = mine.State == ExchangeState.Proposed
                ? mine.ProposedHere ? "BeaverBuddies.Colony.Trade.Notice.Withdrawn" : "BeaverBuddies.Colony.Trade.Notice.Declined"
                : "BeaverBuddies.Colony.Trade.Notice.Cancelled";
            Plugin.Log($"[Colony] Slot {me} ended exchange {serial} ({mine.State}; sent {mine.Sent}/{mine.Total} {mine.GoodId}, "
                + $"received {theirs?.Sent ?? 0}/{theirs?.Total ?? 0} {theirs?.GoodId})");
            mine.Clear();
            theirs?.Clear();
            Tell(() => them, null, () => string.Format(T(key), ColonyName(me)), warning: true);
        }

        // ---- notices (display only: shown to whichever player the news is for) ----

        /// <summary>
        /// Shows a notice if the local player plays one of the colonies named. Called from actions and deliveries that
        /// every computer plays, so it is built only where shown and can never throw into the simulation.
        /// </summary>
        private void Tell(Func<int> a, Func<int> b, Func<string> text, bool warning)
        {
            try
            {
                int local = ColonySession.LocalSlot;
                if (local < 0 || (local != a() && (b == null || local != b()))) return;
                _colonyRulesService.ShowNotice(text(), warning);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not show an exchange notice: " + error.Message);
            }
        }

        /// <summary>"250 Gears", or "nothing" for a side that gives nothing.</summary>
        public string Amount(int amount, string goodId) =>
            amount > 0 ? $"{amount} {GoodName(goodId)}" : T("BeaverBuddies.Colony.Trade.NothingInReturn");

        public string GoodName(string goodId)
        {
            if (goodId == ExchangeTerms.Science) return T("BeaverBuddies.Colony.Trade.ItemScience");
            if (goodId == ExchangeTerms.Beavers) return T("BeaverBuddies.Colony.Trade.ItemBeavers");
            try { return _goodService.GetGood(goodId).PluralDisplayName.Value; }
            catch (Exception) { return goodId; }
        }

        // ---- science and beavers move by themselves (every computer, in the simulation) ----

        public void Tick()
        {
            if (!ColonyModeService.IsSeparateColonies || ++ticks % SpecialItemInterval != 0) return;
            long started = ColonyProfiler.Start();
            try
            {
                MoveSpecialItems();
            }
            finally
            {
                ColonyProfiler.Stop("Science and beaver exchanges", started);
            }
        }

        private void MoveSpecialItems()
        {
            foreach (DistrictCrossing half in _entityComponentRegistry.GetEnabled<DistrictCrossing>().ToList())
            {
                string item = GoodGiven(half);
                if (!ExchangeTerms.IsSpecial(item)) continue;
                int owed = StillToBring(half);
                if (owed <= 0) continue;
                DistrictCrossing partner = TradingPosts.Partner(half);
                int from = OwnerOf(half), to = OwnerOf(partner);
                int moved = item == ExchangeTerms.Science ? MoveScience(from, to, owed) : MoveBeavers(half, partner, owed);
                if (moved <= 0) continue;
                ColonyTradeLedger.Instance?.Record(from, to, item, moved);
                CountDelivery(half, item, moved);
            }
        }

        private static int MoveScience(int from, int to, int owed)
        {
            ColonyScienceService science = ColonyScienceService.Instance;
            if (science == null || !science.Enabled || from < 0 || to < 0) return 0;
            int moved = Math.Min(Math.Min(owed, ScienceAtOnce), science.PointsOf(from));
            if (moved <= 0) return 0;
            science.Subtract(from, moved);
            science.Add(to, moved);
            return moved;
        }

        // Adults move from the giving half's district to the other's, one at a time; the last adult always stays.
        private static int MoveBeavers(DistrictCrossing half, DistrictCrossing partner, int owed)
        {
            DistrictCenter source = TradingPosts.DistrictOf(half), target = TradingPosts.DistrictOf(partner);
            if (!source || !target) return 0;
            var adults = BeaverBuddies.Events.DistributorUtils.GetDistributor(BeaverBuddies.Events.DistributorType.Adults, source);
            if (adults == null) return 0;
            int moved = Math.Min(Math.Min(owed, BeaversAtOnce), adults.Current - 1);
            if (moved <= 0) return 0;
            adults.MigrateTo(target, moved);
            return moved;
        }

        public static string ColonyName(int slot)
        {
            string name = ColonySlotService.Instance?.Table.NameOf(slot);
            return string.IsNullOrEmpty(name) ? string.Format(T("BeaverBuddies.Colony.Trade.ColonyN"), slot + 1) : name;
        }

        private static int OwnerOf(DistrictCrossing half) => DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(half)) ?? -1;

        private static string T(string key) => RegisteredLocalizationService.T(key);
    }

    /// <summary>A colony offers an exchange through its own half of a trading post.</summary>
    [Serializable]
    public class ExchangeProposedEvent : ReplayEvent
    {
        public string crossingID;
        public string giveGood;
        public int giveAmount;
        public string getGood;
        public int getAmount;
        public bool repeat;

        // The offering half must be the actor's (checked again when played).
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            ColonyExchangeService.Instance?.Propose(half, slot, giveGood, giveAmount, getGood, getAmount, repeat);
        }

        public override string ToActionString() => $"Offering {giveAmount} {giveGood} for {getAmount} {getGood}";
    }

    /// <summary>A colony accepts the offer made to its half of a trading post, as it saw it.</summary>
    [Serializable]
    public class ExchangeAcceptedEvent : ReplayEvent
    {
        public string crossingID;
        public int serial;
        // From the accepting colony's side: what it gives and what it gets.
        public string giveGood;
        public int giveAmount;
        public string getGood;
        public int getAmount;
        public bool repeat;

        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            if (half == null) return;
            ColonyExchangeService.Instance?.Accept(half, slot, serial, giveGood, giveAmount, getGood, getAmount, repeat);
        }

        public override string ToActionString() => $"Accepting {getAmount} {getGood} for {giveAmount} {giveGood}";
    }

    /// <summary>A colony declines, withdraws or stops the exchange at its half of a trading post.</summary>
    [Serializable]
    public class ExchangeCancelledEvent : ReplayEvent
    {
        public string crossingID;
        public int serial;

        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            if (half == null) return;
            ColonyExchangeService.Instance?.Cancel(half, slot, serial);
        }

        public override string ToActionString() => "Ending an exchange";
    }
}
