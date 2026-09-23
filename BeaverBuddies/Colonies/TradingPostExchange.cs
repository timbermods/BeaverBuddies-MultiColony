using BeaverBuddies.Events;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BlockingSystem;
using Timberborn.Carrying;
using Timberborn.DistributionSystem;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.GameCycleSystem;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.NotificationSystem;
using Timberborn.Persistence;
using Timberborn.ResourceCountingSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    public enum ExchangeState { None = 0, Proposed = 1, Active = 2 }

    /// <summary>
    /// One side of an exchange at a Trading Post, saved with its half: the item this half's colony gives each round and
    /// how many, how much of it already waits on the half this round, the rounds agreed and crossed, and the half's
    /// ledger of rounds that crossed. The partner half holds the other side. Both sides are set, and cleared, together,
    /// by the same action or tick on every computer.
    /// </summary>
    public class CrossingExchange : BaseComponent, IAwakableComponent, IPersistentEntity, IPostInitializableEntity, IDeletableEntity
    {
        /// <summary>How many crossed rounds a half remembers.</summary>
        public const int LedgerLength = 20;

        private static readonly ComponentKey ExchangeKey = new ComponentKey("BeaverBuddies.CrossingExchange");
        private static readonly PropertyKey<int> StateKey = new PropertyKey<int>("State");
        private static readonly PropertyKey<int> ProposedHereKey = new PropertyKey<int>("ProposedHere");
        private static readonly PropertyKey<string> GoodKey = new PropertyKey<string>("Good");
        private static readonly PropertyKey<int> TotalKey = new PropertyKey<int>("Total");
        private static readonly PropertyKey<int> HeldKey = new PropertyKey<int>("Held");
        private static readonly PropertyKey<int> SerialKey = new PropertyKey<int>("Serial");
        private static readonly PropertyKey<int> RepeatKey = new PropertyKey<int>("Repeat");
        private static readonly PropertyKey<int> RoundsKey = new PropertyKey<int>("Rounds");
        private static readonly PropertyKey<int> DoneKey = new PropertyKey<int>("Done");
        private static readonly PropertyKey<int> CancelAskedKey = new PropertyKey<int>("CancelAsked");
        private static readonly PropertyKey<int> ColonyKey = new PropertyKey<int>("Colony");
        private static readonly PropertyKey<int> KeepKey = new PropertyKey<int>("Keep");
        private static readonly PropertyKey<string> LastTermsKey = new PropertyKey<string>("LastTerms");
        private static readonly ListKey<string> LedgerKey = new ListKey<string>("Ledger");

        private readonly List<TradeRecord> ledger = new List<TradeRecord>();

        /// <summary>This half is a Trading Post's (only those hold exchanges).</summary>
        public bool AtTradingPost { get; private set; }
        public ExchangeState State { get; private set; }
        /// <summary>This half's colony made the offer (the other one accepts or declines).</summary>
        public bool ProposedHere { get; private set; }
        /// <summary>What this half's colony gives each round, or null for nothing.</summary>
        public string GoodId { get; private set; }
        /// <summary>How many of it each round (0 to <see cref="ExchangeTerms.MaxAmount"/>).</summary>
        public int Total { get; private set; }
        /// <summary>How many of this round's goods already wait on this half (reserved there, so nobody takes them).</summary>
        public int Held { get; private set; }
        /// <summary>
        /// Which exchange at this crossing this is (both halves agree). It keeps counting after an exchange ends, so an
        /// answer or a cancel meant for one exchange never reaches the next.
        /// </summary>
        public int Serial { get; private set; }
        /// <summary>Starts again on the same terms after each round, until both colonies agree to end it.</summary>
        public bool Repeat { get; private set; }
        /// <summary>How many rounds were agreed (a repeating exchange ignores it).</summary>
        public int Rounds { get; private set; }
        /// <summary>How many rounds have crossed.</summary>
        public int Done { get; private set; }
        /// <summary>This half's colony asked to end the running exchange; it ends once the other colony agrees.</summary>
        public bool CancelAsked { get; private set; }
        /// <summary>The colony this half belonged to when the exchange was offered (-1 when none is open).</summary>
        public int Colony { get; private set; } = -1;
        /// <summary>
        /// What this half's colony keeps back: its side is brought (or paid) only while the colony has at least this
        /// much left after the round (ExchangeTerms.CanSpare). 0 for no floor. Each side sets its own.
        /// </summary>
        public int Keep { get; private set; }
        /// <summary>The last exchange offered or accepted here, from this half's side (ExchangeTerms.EncodeTerms), to offer again.</summary>
        public string LastTerms { get; private set; }
        /// <summary>Rounds that crossed here, oldest first.</summary>
        public IReadOnlyList<TradeRecord> Ledger => ledger;

        public void Awake() => AtTradingPost = GetComponent<MultiColonyTradingPostSpec>() != null;
        public bool IsOpen => State != ExchangeState.None;
        public bool IsActive => State == ExchangeState.Active;
        public bool GivesGoods => Total > 0 && GoodId != null && !ExchangeTerms.IsSpecial(GoodId);

        internal void Propose(int serial, bool proposedHere, string goodId, int total, int rounds, bool repeat, int colony, int keep)
        {
            Serial = serial;
            State = ExchangeState.Proposed;
            ProposedHere = proposedHere;
            GoodId = ExchangeTerms.GoodOf(goodId, total);
            Total = total;
            Held = 0;
            Repeat = repeat;
            Rounds = repeat ? 1 : rounds;
            Done = 0;
            CancelAsked = false;
            Colony = colony;
            Keep = ExchangeTerms.IsValidKeep(keep) ? keep : 0;
            Changed("propose");
        }

        internal void SetKeep(int keep)
        {
            Keep = Math.Max(0, Math.Min(ExchangeTerms.MaxKeep, keep));
            Changed("keep");
        }

        /// <summary>The terms just offered or accepted, from this side, kept after the exchange ends so they can be offered again.</summary>
        internal void RememberTerms(string encoded)
        {
            LastTerms = encoded;
            ColonyDigest.Note("last-terms", Hash(), ColonyDigest.Of(encoded));
        }

        internal void Activate()
        {
            State = ExchangeState.Active;
            Changed("activate");
        }

        internal void Hold(int amount)
        {
            Held += amount;
            Changed("hold");
        }

        /// <summary>The round's goods crossed: the next round (if any) starts with nothing on the half.</summary>
        internal void Crossed()
        {
            Done++;
            Held = 0;
            Changed("crossed");
        }

        internal void AskCancel(bool asked)
        {
            CancelAsked = asked;
            Changed("cancel-asked");
        }

        internal void Record(TradeRecord record)
        {
            ledger.Add(record);
            if (ledger.Count > LedgerLength) ledger.RemoveRange(0, ledger.Count - LedgerLength);
            ColonyDigest.Note("ledger", Hash(), ColonyDigest.Of(record.Encode()));
        }

        /// <summary>Diagnostics: every field, open or not (a serial that differs makes Accept or Cancel skip on one computer only).</summary>
        public long Fingerprint() =>
            1 + (int)State + 3L * Held + 7919L * Total + 104729L * Done + 15485863L * Serial + 1299709L * Rounds
            + (CancelAsked ? 2 : 0) + (Repeat ? 4 : 0) + (ProposedHere ? 8 : 0) + 16L * (Colony + 2)
            + 17L * ColonyDigest.Of(GoodId) + 19L * ledger.Count + (ledger.Count > 0 ? ColonyDigest.Of(ledger[ledger.Count - 1].Encode()) : 0)
            + 23L * Keep + 29L * ColonyDigest.Of(LastTerms);

        private void Changed(string what) => ColonyDigest.Note("exchange-" + what, Hash(), Fingerprint());

        private long Hash() => GetComponent<EntityComponent>()?.EntityId.GetHashCode() ?? 0;

        /// <summary>No exchange here any more; the serial and the ledger stay.</summary>
        internal void Clear()
        {
            Changed("clear");
            State = ExchangeState.None;
            ProposedHere = false;
            GoodId = null;
            Total = 0;
            Held = 0;
            Repeat = false;
            Rounds = 0;
            Done = 0;
            CancelAsked = false;
            Colony = -1;
            Keep = 0;
        }

        public void Save(IEntitySaver entitySaver)
        {
            // Separate colonies only (a shared game's District Crossings save only what the game's do).
            if (!ColonyModeService.IsSeparateColonies) return;
            if (!IsOpen && Serial == 0 && ledger.Count == 0 && LastTerms == null) return;
            IObjectSaver saver = entitySaver.GetComponent(ExchangeKey);
            saver.Set(SerialKey, Serial);
            if (ledger.Count > 0) saver.Set(LedgerKey, ledger.Select(r => r.Encode()).ToList());
            if (!string.IsNullOrEmpty(LastTerms)) saver.Set(LastTermsKey, LastTerms);
            if (!IsOpen) return;
            if (Keep > 0) saver.Set(KeepKey, Keep);
            saver.Set(StateKey, (int)State);
            saver.Set(ProposedHereKey, ProposedHere ? 1 : 0);
            if (!string.IsNullOrEmpty(GoodId)) saver.Set(GoodKey, GoodId);
            saver.Set(TotalKey, Total);
            if (Held > 0) saver.Set(HeldKey, Held);
            if (Repeat) saver.Set(RepeatKey, 1);
            saver.Set(RoundsKey, Rounds);
            if (Done > 0) saver.Set(DoneKey, Done);
            if (CancelAsked) saver.Set(CancelAskedKey, 1);
            saver.Set(ColonyKey, Colony);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (!entityLoader.TryGetComponent(ExchangeKey, out IObjectLoader loader)) return;
            if (loader.Has(SerialKey)) Serial = Math.Max(0, loader.Get(SerialKey));
            if (loader.Has(LedgerKey))
            {
                foreach (string entry in loader.Get(LedgerKey))
                    if (TradeRecord.TryDecode(entry, out TradeRecord record)) Record(record);
            }
            if (loader.Has(LastTermsKey))
            {
                string last = loader.Get(LastTermsKey);
                if (ExchangeTerms.TryDecodeTerms(last, out _, out _, out _, out _, out _, out _, out _)) LastTerms = last;
            }
            int state = loader.Has(StateKey) ? loader.Get(StateKey) : 0;
            string good = loader.Has(GoodKey) ? loader.Get(GoodKey) : null;
            int total = loader.Has(TotalKey) ? loader.Get(TotalKey) : 0;
            int rounds = loader.Has(RoundsKey) ? loader.Get(RoundsKey) : 1;
            bool repeat = loader.Has(RepeatKey) && loader.Get(RepeatKey) != 0;
            // Only a state an exchange can be in (a side that gives nothing has no good and 0).
            if (state < (int)ExchangeState.Proposed || state > (int)ExchangeState.Active || total < 0 || total > ExchangeTerms.MaxAmount
                || (total > 0 && string.IsNullOrEmpty(good)) || (!repeat && !ExchangeTerms.AreValidRounds(rounds)))
                return;
            State = (ExchangeState)state;
            ProposedHere = loader.Has(ProposedHereKey) && loader.Get(ProposedHereKey) != 0;
            GoodId = ExchangeTerms.GoodOf(good, total);
            Total = total;
            Held = loader.Has(HeldKey) ? Math.Max(0, Math.Min(total, loader.Get(HeldKey))) : 0;
            Repeat = repeat;
            Rounds = repeat ? 1 : rounds;
            Done = loader.Has(DoneKey) ? Math.Max(0, loader.Get(DoneKey)) : 0;
            CancelAsked = loader.Has(CancelAskedKey) && loader.Get(CancelAskedKey) != 0;
            Colony = loader.Has(ColonyKey) ? loader.Get(ColonyKey) : -1;
            Keep = loader.Has(KeepKey) && ExchangeTerms.IsValidKeep(loader.Get(KeepKey)) ? loader.Get(KeepKey) : 0;
        }

        /// <summary>
        /// A loaded round's goods wait on the half again: the game does not save reservations, so they are made again
        /// here, before anything ticks (on every computer, from the save alone).
        /// </summary>
        public void PostInitializeEntity()
        {
            if (!IsActive || !GivesGoods || Held <= 0) return;
            Inventory inventory = GetComponent<DistrictCrossingInventory>()?.Inventory;
            if (inventory == null) return;
            int held = Math.Min(Held, inventory.UnreservedAmountInStock(GoodId));
            if (held < Held)
            {
                Plugin.LogWarning($"[Colony] Exchange {Serial}: {Held} {GoodId} should wait on a half, {held} do");
                Held = held;
            }
            if (held > 0) inventory.ReserveStock(new GoodAmount(GoodId, held));
        }

        /// <summary>
        /// The Trading Post is being removed with an exchange open (a player's deletion, a blast, the ground taken from
        /// under it): both halves go at once, and what waited on them is left as recovered goods where they stood (the
        /// game's own, which reads the halves' stock after this). The offering half says so, once for the post (C5).
        /// </summary>
        public void DeleteEntity()
        {
            if (!ColonyModeService.IsSeparateColonies || !IsOpen || !ProposedHere) return;
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;
            exchanges?.OnPostRemoved(GetComponent<DistrictCrossing>(), this);
        }
    }

    /// <summary>
    /// Barter at Trading Posts. One colony offers "this many of my item for that many of yours, so many times", the
    /// other accepts. Each round, each colony's Trading Post workers bring its goods to its own half, where they wait;
    /// once both sides are in (and a side of science or beavers can be paid), everything crosses at once: the goods to
    /// the other half, for the other colony's workers to haul away, the science from pool to pool, the beavers to the
    /// other colony's district. A running exchange ends early only when both colonies agree; what waits on a half then
    /// goes back into its own colony's storage. The rules live here; the carrying is patched in ColonyTrading.cs, and the
    /// offers and answers arrive as the actions at the end of this file. Everything here runs in actions and ticks that
    /// every computer plays, reading only saved state.
    /// </summary>
    public class ColonyExchangeService : RegisteredSingleton, ILoadableSingleton, ITickableSingleton
    {
        // How often (ticks) the Trading Posts check whether a round can cross.
        private const int CrossingInterval = 8;

        private readonly IGoodService _goodService;
        private readonly ColonyRulesService _colonyRulesService;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private readonly NotificationBus _notificationBus;
        private readonly GameCycleService _gameCycleService;
        private readonly MigrationService _migrationService;
        private readonly ResourceCountingService _resourceCountingService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private int ticks;
        // The crossings checked this time (kept, so the check allocates no list; the registry's order, the same on
        // every computer), and the adults counted or moved (the game's main thread only).
        private readonly List<DistrictCrossing> halves = new List<DistrictCrossing>();
        private readonly List<Beaver> movable = new List<Beaver>();
        // The daily trade check: the day last checked, and the totals then (for the detailed log's "crossed today").
        private int checkedDay = int.MinValue;
        private TradeTotals totalsAtDayStart;
        /// <summary>Diagnostics: the crossing phase (not saved; the same on every computer that loaded together).</summary>
        public int Ticks => ticks;

        /// <summary>True while a round's goods are being moved across by this service (nothing else moves any).</summary>
        public static bool Crossing { get; private set; }

        public static ColonyExchangeService Instance => SingletonManager.GetSingleton<ColonyExchangeService>();

        public ColonyExchangeService(IGoodService goodService, ColonyRulesService colonyRulesService,
            EntityComponentRegistry entityComponentRegistry, NotificationBus notificationBus, GameCycleService gameCycleService,
            MigrationService migrationService, ResourceCountingService resourceCountingService, DistrictCenterRegistry districtCenterRegistry)
        {
            _goodService = goodService;
            _colonyRulesService = colonyRulesService;
            _entityComponentRegistry = entityComponentRegistry;
            _notificationBus = notificationBus;
            _gameCycleService = gameCycleService;
            _migrationService = migrationService;
            _resourceCountingService = resourceCountingService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        // Loadable only so the game builds it at load: it is found through SingletonManager, not injected.
        public void Load() { }

        public static CrossingExchange Of(DistrictCrossing half) => half ? half.GetComponent<CrossingExchange>() : null;

        public static int OwnerOf(DistrictCrossing half) => DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(half)) ?? -1;

        /// <summary>
        /// Whether a colony has an offer or exchange open at any post, on its own half or as the side it was offered to
        /// (a mixed game's faction switch waits for it to end, D14). Saved state only.
        /// </summary>
        public bool HasOpenExchange(int slot)
        {
            foreach (DistrictCrossing half in _entityComponentRegistry.GetEnabled<DistrictCrossing>())
            {
                CrossingExchange side = Of(half);
                if (side != null && side.IsOpen && (OwnerOf(half) == slot || side.Colony == slot)) return true;
            }
            return false;
        }

        // ---- carrying (in the tick, every computer) ----

        /// <summary>
        /// How many more of its good this half's workers should set out to bring now: what is still missing on the half
        /// this round, less <paramref name="onTheWay"/>. Nothing while the post does not trade, or while a colony has asked
        /// to end the exchange.
        /// </summary>
        public static int StillToBring(DistrictCrossing half, int onTheWay)
        {
            if (!TradingPosts.IsTradingPost(half)) return 0;
            CrossingExchange mine = Of(half), theirs = Of(TradingPosts.Partner(half));
            if (mine == null || theirs == null || !mine.IsActive || !theirs.IsActive || !mine.GivesGoods) return 0;
            if (mine.CancelAsked || theirs.CancelAsked) return 0;
            // A colony that keeps a reserve brings nothing while the round would eat into it (the goods on its half count as had).
            if (mine.Keep > 0)
            {
                int have = Instance?.StockOf(half, mine.GoodId) ?? int.MaxValue;
                return ExchangeTerms.StillToBringKeeping(mine.Total, mine.Held, onTheWay, have, mine.Keep);
            }
            return ExchangeTerms.StillToBring(mine.Total, mine.Held, onTheWay);
        }

        /// <summary>How much of a good the half's district has now (the game's own count, read in the tick).</summary>
        public int StockOf(DistrictCrossing half, string goodId)
        {
            DistrictCenter district = TradingPosts.DistrictOf(half);
            if (!district || string.IsNullOrEmpty(goodId) || !_goodService.HasGood(goodId)) return 0;
            return _resourceCountingService.GetDistrictResourceCounter(district).GetResourceCount(goodId).AvailableStock;
        }

        /// <summary>What a side has of what it gives: its stock of the good, its science to spare, or its adults to spare.</summary>
        public int HaveOf(DistrictCrossing half, CrossingExchange side)
        {
            if (side.GoodId == ExchangeTerms.Science) return ScienceToSpare(OwnerOf(half));
            if (side.GoodId == ExchangeTerms.Beavers) return BeaversToSpare(half);
            return StockOf(half, side.GoodId);
        }

        /// <summary>Display: the side is not in because its colony keeps a reserve the round would eat into.</summary>
        public bool IsHeldByFloor(DistrictCrossing half, CrossingExchange side) =>
            side.Total > 0 && side.Keep > 0 && !IsIn(half, side) && !ExchangeTerms.CanSpare(HaveOf(half, side), side.Total, side.Keep);

        /// <summary>
        /// Display and the diagnostics report: why a side giving goods is not in yet (T5: a stall says why). Reads the
        /// half as it is; changes nothing.
        /// </summary>
        public ExchangeTerms.GoodsWait WhyWaiting(DistrictCrossing half, CrossingExchange side)
        {
            DistrictCrossingInventory crossingInventory = half ? half.GetComponent<DistrictCrossingInventory>() : null;
            Inventory inventory = crossingInventory?.Inventory;
            if (inventory == null || !side.GivesGoods) return ExchangeTerms.GoodsWait.Bringing;
            bool blocked = !(half.GetComponent<BlockableObject>()?.IsUnblocked ?? true);
            int workers = half.GetComponent<Workplace>()?.NumberOfAssignedWorkers ?? 0;
            int onTheWay = TradingPosts.Partner(half) ? crossingInventory.IncomingStock(side.GoodId) : 0;
            int room = inventory.UnreservedCapacity(side.GoodId);
            int elsewhere = StockOf(half, side.GoodId) - inventory.AmountInStock(side.GoodId);
            return ExchangeTerms.WhyGoodsWait(blocked, workers, onTheWay, room, elsewhere);
        }

        /// <summary>The diagnostics report: why a side of a running exchange is not in, in a few words, or "" when it is.</summary>
        public string WhyNotIn(DistrictCrossing half, CrossingExchange side)
        {
            if (side.Total <= 0 || side.GoodId == null || IsIn(half, side)) return "";
            if (side.GoodId == ExchangeTerms.Science) return $"has {ScienceToSpare(OwnerOf(half))} of {side.Total} science";
            if (side.GoodId == ExchangeTerms.Beavers)
                return $"can spare {BeaversToSpare(half)} of {side.Total} beavers (free to go: carrying nothing, able to walk there)";
            string held = $"{side.Held} of {side.Total} delivered";
            if (IsHeldByFloor(half, side)) return $"keeps {side.Keep} back; {held}";
            switch (WhyWaiting(half, side))
            {
                case ExchangeTerms.GoodsWait.Blocked: return "its half is paused or flooded; " + held;
                case ExchangeTerms.GoodsWait.NoWorkers: return "no workers on its half; " + held;
                case ExchangeTerms.GoodsWait.NoRoom: return "no room on its half (the other colony's goods are not being hauled away); " + held;
                case ExchangeTerms.GoodsWait.NoStock: return "none left in its district; " + held;
                default: return held;
            }
        }

        /// <summary>The good this half's workers bring in a running exchange, or null.</summary>
        public static string GoodGiven(DistrictCrossing half)
        {
            CrossingExchange mine = Of(half);
            return mine != null && mine.IsActive && mine.GivesGoods ? mine.GoodId : null;
        }

        /// <summary>
        /// A load arrived on a half (the game's delivery, in the tick). What the round still needs of the exchange's good
        /// is held there, reserved so no beaver takes it; anything else is carried home by the half's workers.
        /// </summary>
        public void OnArrival(DistrictCrossing half, Inventory inventory, string goodId, int amount)
        {
            if (!TradingPosts.IsTradingPost(half) || inventory == null) return;
            CrossingExchange mine = Of(half), theirs = Of(TradingPosts.Partner(half));
            if (mine == null || theirs == null || !mine.IsActive || !theirs.IsActive || !mine.GivesGoods || mine.GoodId != goodId) return;
            int held = Math.Min(ExchangeTerms.ToHold(mine.Total, mine.Held, amount), inventory.UnreservedAmountInStock(goodId));
            if (held <= 0) return;
            inventory.ReserveStock(new GoodAmount(goodId, held));
            mine.Hold(held);
        }

        // ---- rounds crossing (in the tick, every computer) ----

        private static readonly ColonyProfiler.Spot Exchanges = ColonyProfiler.Declare("Trading post exchanges");

        public void Tick()
        {
            if (!ColonyModeService.IsSeparateColonies) return;
            if (++ticks % CrossingInterval == 0)
            {
                long started = ColonyProfiler.Start();
                try
                {
                    CheckTradingPosts();
                }
                finally
                {
                    ColonyProfiler.Stop(Exchanges, started);
                }
            }
            int day = _gameCycleService.Cycle * 1000 + _gameCycleService.CycleDay;
            if (day == checkedDay) return;
            bool first = checkedDay == int.MinValue;
            checkedDay = day;
            if (first) totalsAtDayStart = ColonyTradeLedger.Instance?.Totals.Copy();
            else DailyCheck();
        }

        private void CheckTradingPosts()
        {
            // The registry's order is the same on every computer; each post is checked once, from its offering half.
            halves.Clear();
            foreach (DistrictCrossing crossing in _entityComponentRegistry.GetEnabled<DistrictCrossing>()) halves.Add(crossing);
            for (int i = 0; i < halves.Count; i++)
            {
                DistrictCrossing half = halves[i];
                CrossingExchange mine = Of(half);
                if (mine == null || !mine.IsOpen) continue;
                DistrictCrossing partner = TradingPosts.Partner(half);
                CrossingExchange theirs = Of(partner);
                bool partnerOpen = theirs != null && theirs.IsOpen;
                // The other half speaks for the post (ExchangeTerms.Ending: NotThisHalf); nothing to work out here.
                if (partnerOpen && !mine.ProposedHere) continue;
                int owner = OwnerOf(half), partnerOwner = OwnerOf(partner);
                bool trading = partnerOpen && TradingPosts.IsTradingPost(half);
                // A mixed-factions game: terms the factions no longer allow (a colony founded again as another faction),
                // judged only while the post trades (C1).
                bool factionsAllow = !trading || FactionsAllow(owner, partnerOwner, mine.Total > 0 ? mine.GoodId : null,
                    theirs.Total > 0 ? theirs.GoodId : null);
                switch (ExchangeTerms.Ending(partnerOpen, mine.ProposedHere, mine.Colony, owner, theirs?.Colony ?? -1, partnerOwner,
                    trading, factionsAllow))
                {
                    case ExchangeTerms.PostCheck.EndAlone:
                        // One half cannot hold an exchange alone (only a damaged or older save leaves one so).
                        End(half, partner, "the other half has no exchange");
                        continue;
                    case ExchangeTerms.PostCheck.EndColonies:
                    {
                        int a = mine.Colony, b = theirs.Colony;
                        End(half, partner, $"the halves now belong to slots {owner} and {partnerOwner}");
                        Tell(() => a, () => b, () => T("BeaverBuddies.Colony.Trade.Notice.Void"), warning: true);
                        continue;
                    }
                    case ExchangeTerms.PostCheck.EndFactions:
                    {
                        int a = mine.Colony, b = theirs.Colony;
                        End(half, partner, "the factions no longer allow its terms");
                        Tell(() => a, () => b, () => T("BeaverBuddies.Colony.Trade.Notice.VoidFaction"), warning: true);
                        continue;
                    }
                    case ExchangeTerms.PostCheck.GoesOn:
                        break;
                    default:
                        continue;
                }
                if (!ExchangeTerms.RoundMayCross(mine.IsActive, trading, mine.CancelAsked || theirs.CancelAsked)) continue;
                // What already waits on a giving half counts toward its round (C4), before the sides are judged.
                HoldWaiting(half, mine);
                HoldWaiting(partner, theirs);
                if (IsIn(half, mine, owner) && IsIn(partner, theirs, partnerOwner)) Cross(half, partner, owner, partnerOwner);
            }
            halves.Clear();
        }

        /// <summary>
        /// Goods of the round's item already waiting unreserved on the giving half are held for the round now, as an
        /// arriving load is (ExchangeTerms.ToHoldWaiting): what an ended exchange left there, what was left over from a
        /// load, what arrived while the post was paused, or what the other colony sent earlier. Workers otherwise only
        /// ever hold what they bring, so goods filling the half's room were never held, and the round never filled (C4).
        /// </summary>
        private void HoldWaiting(DistrictCrossing half, CrossingExchange side)
        {
            if (!side.GivesGoods || side.Held >= side.Total) return;
            Inventory inventory = half.GetComponent<DistrictCrossingInventory>()?.Inventory;
            if (inventory == null) return;
            int waiting = inventory.UnreservedAmountInStock(side.GoodId);
            if (waiting <= 0) return;
            int have = side.Keep > 0 ? StockOf(half, side.GoodId) : 0;
            int held = Math.Min(ExchangeTerms.ToHoldWaiting(side.Total, side.Held, waiting, have, side.Keep), waiting);
            if (held <= 0) return;
            inventory.ReserveStock(new GoodAmount(side.GoodId, held));
            side.Hold(held);
        }

        /// <summary>
        /// A side is in when all its goods wait on its half; a side of science or beavers when its colony can pay it now.
        /// A side giving nothing always is.
        /// </summary>
        public bool IsIn(DistrictCrossing half, CrossingExchange side) => IsIn(half, side, OwnerOf(half));

        private bool IsIn(DistrictCrossing half, CrossingExchange side, int owner)
        {
            if (side.Total <= 0) return true;
            // Science and beavers are paid as the round crosses: a reserve holds the payment back.
            if (side.GoodId == ExchangeTerms.Science) return ExchangeTerms.CanSpare(ScienceToSpare(owner), side.Total, side.Keep);
            if (side.GoodId == ExchangeTerms.Beavers) return ExchangeTerms.CanSpare(BeaversToSpare(half), side.Total, side.Keep);
            return ExchangeTerms.IsDelivered(side.Total, side.Held);
        }

        /// <summary>How much science a colony can give now (0 without separate science).</summary>
        public static int ScienceToSpare(int slot)
        {
            ColonyScienceService science = ColonyScienceService.Instance;
            return science != null && science.Enabled && slot >= 0 ? science.PointsOf(slot) : 0;
        }

        /// <summary>
        /// How many adults the half's district can give now (the last adult always stays): exactly those a crossing round
        /// moves (Movable), so a round that is in moves all it agreed to (C2: beavers carrying something, or unable to walk
        /// to the other district, were counted but not moved, and the round crossed short). In a mixed-factions game only
        /// beavers of the other colony's faction count: no other may join it (D20).
        /// </summary>
        public int BeaversToSpare(DistrictCrossing half)
        {
            DistrictPopulation population = TradingPosts.DistrictOf(half)?.DistrictPopulation;
            if (population == null) return 0;
            Movable(half, movable);
            int spare = ExchangeTerms.BeaversToSpare(population.NumberOfAdults, movable.Count);
            movable.Clear();
            return spare;
        }

        /// <summary>
        /// The adults of the half's district who could move to the other half's district now, in the district's order:
        /// not contaminated; of the receiving colony's faction (a mixed game, D20); carrying nothing (what a beaver
        /// carries would cross uncounted); and able to walk to the new district (the game reassigns one who cannot to
        /// the nearest district of any colony, possibly its old one).
        /// </summary>
        private void Movable(DistrictCrossing half, List<Beaver> into)
        {
            into.Clear();
            DistrictCenter source = TradingPosts.DistrictOf(half);
            DistrictCrossing partner = TradingPosts.Partner(half);
            DistrictCenter target = TradingPosts.DistrictOf(partner);
            if (!source || !target) return;
            int targetSlot = OwnerOf(partner);
            var adults = source.DistrictPopulation.Adults;
            for (int i = 0; i < adults.Count; i++)
            {
                Beaver beaver = adults[i];
                if (beaver.GetComponent<GoodCarrier>()?.IsCarrying ?? false) continue;
                if (!_migrationService.IsNotContaminated(beaver)) continue;
                if (!BeaverBuddies.Factions.FactionTrade.BeaverMayJoin(beaver, targetSlot)) continue;
                Citizen citizen = beaver.GetComponent<Citizen>();
                if (citizen == null || !target.IsGloballyReachableFromCitizen(citizen)) continue;
                into.Add(beaver);
            }
        }

        /// <summary>
        /// A mixed-factions game: whether what each side gives may go to the other (D2, D3). Always outside one. Read from
        /// saved state only (each colony's faction), so every computer agrees.
        /// </summary>
        private static bool FactionsAllow(int a, int b, string aGives, string bGives) =>
            (aGives == null || BeaverBuddies.Factions.FactionTrade.Allows(aGives, a, b))
            && (bGives == null || BeaverBuddies.Factions.FactionTrade.Allows(bGives, b, a));

        /// <summary>The reason <see cref="WhyNotPropose"/> gives for terms the factions do not allow.</summary>
        public const string FactionsRefuse = "the factions do not allow these terms";

        /// <summary>Both sides are in: everything crosses at once, the ledgers note the round, and the next begins (or it ends).</summary>
        private void Cross(DistrictCrossing a, DistrictCrossing b, int aSlot, int bSlot)
        {
            CrossingExchange ax = Of(a), bx = Of(b);
            // The state is settled first; the moves below cannot fail the round once they start.
            string aGood = ax.GoodId, bGood = bx.GoodId;
            int aTotal = ax.Total, bTotal = bx.Total, aHeld = ax.Held, bHeld = bx.Held;
            ax.Crossed();
            bx.Crossed();
            Crossing = true;
            try
            {
                MoveGoods(a, aGood, aHeld);
                MoveGoods(b, bGood, bHeld);
            }
            finally
            {
                Crossing = false;
            }
            // What actually moved goes in the ledgers (a round that is in moves all of it; see BeaversToSpare).
            aTotal = MoveSpecial(a, b, aSlot, bSlot, aGood, aTotal);
            bTotal = MoveSpecial(b, a, bSlot, aSlot, bGood, bTotal);
            ColonyTradeLedger totals = ColonyTradeLedger.Instance;
            if (aTotal > 0) totals?.Record(aSlot, bSlot, aGood, aTotal);
            if (bTotal > 0) totals?.Record(bSlot, aSlot, bGood, bTotal);
            int cycle = _gameCycleService.Cycle, day = _gameCycleService.CycleDay;
            ax.Record(new TradeRecord(cycle, day, aGood, aTotal, bGood, bTotal));
            bx.Record(new TradeRecord(cycle, day, bGood, bTotal, aGood, aTotal));
            Plugin.Log($"[Colony] Exchange {ax.Serial} round {ax.Done} crossed: slot {aSlot} gave {aTotal} {aGood}, slot {bSlot} gave {bTotal} {bGood}");
            if (ExchangeTerms.HasAnotherRound(ax.Rounds, ax.Done, ax.Repeat)) return;
            int rounds = ax.Done;
            ax.Clear();
            bx.Clear();
            Tell(() => aSlot, () => bSlot, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.Complete"),
                ColonyName(aSlot), ColonyName(bSlot), rounds), warning: false);
        }

        // A round's goods held on a half cross to the partner half in one go (the game's own crossing, which also frees
        // the partner's room the half had reserved for them).
        private static void MoveGoods(DistrictCrossing half, string goodId, int held)
        {
            if (held <= 0 || goodId == null || ExchangeTerms.IsSpecial(goodId)) return;
            DistrictCrossingInventory crossingInventory = half.GetComponent<DistrictCrossingInventory>();
            Inventory inventory = crossingInventory?.Inventory;
            if (inventory == null) return;
            int reserved = Math.Min(held, inventory._reservedStock.Amount(goodId));
            if (reserved > 0) inventory.UnreserveStock(new GoodAmount(goodId, reserved));
            crossingInventory.TransferStock(goodId, held);
        }

        /// <summary>Science or beavers pass as the round crosses; returns how much did (goods, already moved, pass as agreed).</summary>
        private int MoveSpecial(DistrictCrossing from, DistrictCrossing to, int fromSlot, int toSlot, string item, int amount)
        {
            if (amount <= 0) return amount;
            if (item == ExchangeTerms.Science)
            {
                ColonyScienceService science = ColonyScienceService.Instance;
                if (science == null || !science.Enabled || fromSlot < 0 || toSlot < 0) return 0;
                science.Subtract(fromSlot, amount);
                science.Add(toSlot, amount);
            }
            else if (item == ExchangeTerms.Beavers)
            {
                return MoveBeavers(from, to, fromSlot, amount);
            }
            return amount;
        }

        /// <summary>
        /// Adults move to the receiving half's district, chosen from those able to move (Movable, as BeaversToSpare counts
        /// them) as the game chooses who migrates (those who work, and those with a home, last; the youngest first).
        /// Each one's arrival goes in the population log. Returns how many moved.
        /// </summary>
        private int MoveBeavers(DistrictCrossing from, DistrictCrossing to, int fromSlot, int amount)
        {
            DistrictCenter source = TradingPosts.DistrictOf(from), target = TradingPosts.DistrictOf(to);
            if (!source || !target) return 0;
            Movable(from, movable);
            int count = Math.Min(amount, ExchangeTerms.BeaversToSpare(source.DistrictPopulation.NumberOfAdults, movable.Count));
            List<Beaver> movers = movable
                .OrderBy(_migrationService.RefusesWork).ThenBy(_migrationService.IsEmployed).ThenBy(_migrationService.HasHome)
                .ThenByDescending(_migrationService.GetDayOfBirth)
                .Take(count).ToList();
            movable.Clear();
            if (movers.Count < amount)
                Plugin.LogWarning($"[Colony] Only {movers.Count} of {amount} beavers could move from slot {fromSlot} (the round was judged in)");
            string colony = ColonyName(fromSlot);
            ColonyDigest.Note("beavers", fromSlot, movers.Count, target.GetComponent<EntityComponent>()?.EntityId.GetHashCode() ?? 0);
            foreach (Beaver beaver in movers)
            {
                beaver.GetComponent<Citizen>().AssignDistrict(target);
                string name = beaver.GetComponent<NamedEntity>()?.EntityName ?? "";
                _notificationBus.Post(string.Format(T("BeaverBuddies.Colony.Trade.Notification.Joined"), name, colony), beaver);
            }
            return movers.Count;
        }

        // ---- the actions, played on every computer (only saved state is read) ----

        /// <summary>Why an offer cannot be made from this half, or null.</summary>
        public string WhyNotPropose(DistrictCrossing half, int actorSlot, string giveGood, int giveAmount, string getGood, int getAmount,
            int rounds, bool repeat, int keep = 0)
        {
            if (!half) return "no such crossing";
            if (!TradingPosts.IsTradingPost(half)) return "the crossing is not a Trading Post between two colonies";
            if (actorSlot < 0 || OwnerOf(half) != actorSlot) return $"the half is slot {OwnerOf(half)}'s, not slot {actorSlot}'s";
            if (!ExchangeTerms.AreValid(giveGood, giveAmount, getGood, getAmount)) return "the terms are not valid";
            if (!repeat && !ExchangeTerms.AreValidRounds(rounds)) return $"{rounds} rounds";
            if (!ExchangeTerms.IsValidKeep(keep)) return $"a reserve of {keep}";
            if ((giveAmount > 0 && !IsKnownItem(giveGood)) || (getAmount > 0 && !IsKnownItem(getGood)))
                return "a good is unknown in this game, or science is not separate";
            if (!FactionsAllow(actorSlot, OwnerOf(TradingPosts.Partner(half)), giveAmount > 0 ? giveGood : null, getAmount > 0 ? getGood : null))
                return FactionsRefuse;
            CrossingExchange mine = Of(half), theirs = Of(TradingPosts.Partner(half));
            if (mine == null || theirs == null) return "the crossing cannot hold an exchange";
            if (mine.IsOpen || theirs.IsOpen) return "an exchange is already open here";
            return null;
        }

        /// <summary>A good of this game, science (only when each colony has its own), or beavers.</summary>
        public bool IsKnownItem(string item) =>
            item == ExchangeTerms.Beavers || (item == ExchangeTerms.Science ? ColonyScienceService.IsEnabled : _goodService.HasGood(item));

        public void Propose(DistrictCrossing half, int actorSlot, string giveGood, int giveAmount, string getGood, int getAmount,
            int rounds, bool repeat, int keep = 0)
        {
            string why = WhyNotPropose(half, actorSlot, giveGood, giveAmount, getGood, getAmount, rounds, repeat, keep);
            if (why != null)
            {
                Plugin.LogWarning($"[Colony] Exchange offer skipped: {why}");
                string notice = why == FactionsRefuse ? "BeaverBuddies.Colony.Trade.Notice.OfferFailedFaction" : "BeaverBuddies.Colony.Trade.Notice.OfferFailed";
                Tell(() => actorSlot, null, () => T(notice), warning: true);
                return;
            }
            DistrictCrossing partner = TradingPosts.Partner(half);
            CrossingExchange mine = Of(half), theirs = Of(partner);
            int from = OwnerOf(half), to = OwnerOf(partner);
            // Both halves agree on the number; it only ever grows.
            int serial = Math.Max(mine.Serial, theirs.Serial) + 1;
            mine.Propose(serial, true, giveGood, giveAmount, rounds, repeat, from, keep);
            theirs.Propose(serial, false, getGood, getAmount, rounds, repeat, to, 0);
            // Each half remembers the terms from its own side, to offer them again later.
            mine.RememberTerms(ExchangeTerms.EncodeTerms(giveGood, giveAmount, getGood, getAmount, rounds, repeat, keep));
            theirs.RememberTerms(ExchangeTerms.EncodeTerms(getGood, getAmount, giveGood, giveAmount, rounds, repeat, 0));
            Plugin.Log($"[Colony] Slot {from} offers {giveAmount} {giveGood} for {getAmount} {getGood} from slot {to}, "
                + $"{(repeat ? "repeating" : rounds + " rounds")}{(keep > 0 ? $", keeping {keep}" : "")} (exchange {serial})");
            Tell(() => to, null, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.Proposed"),
                ColonyName(from), Amount(giveAmount, giveGood), Amount(getAmount, getGood)), warning: false);
        }

        /// <summary>
        /// The colony of <paramref name="half"/> accepts the offer made to it, as its player saw it: the exchange's
        /// number and terms are checked again, so an offer withdrawn and made again is never accepted by mistake.
        /// </summary>
        public void Accept(DistrictCrossing half, int actorSlot, int serial, string giveGood, int giveAmount, string getGood, int getAmount,
            int rounds, bool repeat)
        {
            CrossingExchange mine = Of(half);
            DistrictCrossing partner = TradingPosts.Partner(half);
            CrossingExchange theirs = Of(partner);
            string why = null;
            if (!TradingPosts.IsTradingPost(half) || mine == null || theirs == null
                || mine.State != ExchangeState.Proposed || theirs.State != ExchangeState.Proposed || mine.ProposedHere)
                why = "no offer is waiting here";
            else if (actorSlot < 0 || OwnerOf(half) != actorSlot)
                why = $"the half is slot {OwnerOf(half)}'s, not slot {actorSlot}'s";
            else if (mine.Serial != serial || mine.Repeat != repeat || (!repeat && mine.Rounds != rounds) || mine.Total != giveAmount
                || theirs.Total != getAmount || mine.GoodId != ExchangeTerms.GoodOf(giveGood, giveAmount)
                || theirs.GoodId != ExchangeTerms.GoodOf(getGood, getAmount))
                why = "the offer changed";
            // A mixed game only (the faction check below needs the colony it was offered to); otherwise the tick's
            // check ends such an exchange, as before.
            else if (Factions.MixedFactions.IsOn && theirs.Colony >= 0 && OwnerOf(partner) != theirs.Colony)
                why = "the other half has changed colony";
            else if (!FactionsAllow(OwnerOf(half), OwnerOf(partner), mine.Total > 0 ? mine.GoodId : null, theirs.Total > 0 ? theirs.GoodId : null))
                why = FactionsRefuse;
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
        /// A colony wants exchange <paramref name="serial"/> at its half to end. An offer is declined or withdrawn at once.
        /// A running exchange ends when both colonies want it to (the second one's answer ends it), or at once when the
        /// other colony can no longer answer (the post no longer joins the two). A late cancel does nothing.
        /// </summary>
        public void Cancel(DistrictCrossing half, int actorSlot, int serial)
        {
            if (!TryGetOwnOpen(half, actorSlot, serial, "cancel", out CrossingExchange mine, out DistrictCrossing partner, out CrossingExchange theirs))
                return;
            int me = OwnerOf(half), them = theirs?.Colony ?? OwnerOf(partner);
            if (mine.State == ExchangeState.Proposed)
            {
                string key = mine.ProposedHere ? "BeaverBuddies.Colony.Trade.Notice.Withdrawn" : "BeaverBuddies.Colony.Trade.Notice.Declined";
                End(half, partner, mine.ProposedHere ? "offer withdrawn" : "offer declined");
                Tell(() => them, null, () => string.Format(T(key), ColonyName(me)), warning: true);
                return;
            }
            bool otherCanAnswer = theirs != null && TradingPosts.IsTradingPost(half) && OwnerOf(partner) == theirs.Colony;
            if (theirs != null && theirs.CancelAsked || !otherCanAnswer)
            {
                End(half, partner, otherCanAnswer ? "both colonies agreed" : "the other colony cannot answer");
                Tell(() => me, () => them, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.Cancelled"), ColonyName(me), ColonyName(them)),
                    warning: true);
                return;
            }
            if (mine.CancelAsked) return;
            mine.AskCancel(true);
            Plugin.Log($"[Colony] Slot {me} asks to end exchange {serial}");
            Tell(() => them, null, () => string.Format(T("BeaverBuddies.Colony.Trade.Notice.CancelAsked"), ColonyName(me)), warning: true);
        }

        /// <summary>
        /// A colony sets what it keeps back in exchange <paramref name="serial"/> at its half (0 for no floor): its side
        /// is brought or paid only while the colony can spare the round. Changed at any time while the exchange is open.
        /// </summary>
        public void SetKeep(DistrictCrossing half, int actorSlot, int serial, int keep)
        {
            if (!TryGetOwnOpen(half, actorSlot, serial, "reserve", out CrossingExchange mine, out _, out _)) return;
            if (!ExchangeTerms.IsValidKeep(keep))
            {
                Plugin.LogWarning($"[Colony] Exchange reserve skipped: {keep} is not a valid reserve");
                return;
            }
            mine.SetKeep(keep);
            Plugin.Log($"[Colony] Slot {OwnerOf(half)} keeps {keep} {mine.GoodId} back in exchange {serial}");
        }

        /// <summary>
        /// A colony wants exchange <paramref name="serial"/> to go on: it withdraws its own request to end it, or turns
        /// down the other colony's.
        /// </summary>
        public void Keep(DistrictCrossing half, int actorSlot, int serial)
        {
            if (!TryGetOwnOpen(half, actorSlot, serial, "keep", out CrossingExchange mine, out DistrictCrossing partner, out CrossingExchange theirs))
                return;
            if (!mine.IsActive || theirs == null || (!mine.CancelAsked && !theirs.CancelAsked)) return;
            bool theyAsked = theirs.CancelAsked;
            mine.AskCancel(false);
            theirs.AskCancel(false);
            int me = OwnerOf(half), them = theirs.Colony;
            Plugin.Log($"[Colony] Slot {me} keeps exchange {serial} going");
            string key = theyAsked ? "BeaverBuddies.Colony.Trade.Notice.CancelRefused" : "BeaverBuddies.Colony.Trade.Notice.CancelWithdrawn";
            Tell(() => them, null, () => string.Format(T(key), ColonyName(me)), warning: false);
        }

        private bool TryGetOwnOpen(DistrictCrossing half, int actorSlot, int serial, string what, out CrossingExchange mine,
            out DistrictCrossing partner, out CrossingExchange theirs)
        {
            mine = Of(half);
            partner = TradingPosts.Partner(half);
            theirs = Of(partner);
            if (mine == null || !mine.IsOpen || mine.Serial != serial)
            {
                Plugin.LogWarning($"[Colony] Exchange {what} skipped: exchange {serial} is not open here");
                return false;
            }
            if (actorSlot < 0 || OwnerOf(half) != actorSlot)
            {
                Plugin.LogWarning($"[Colony] Exchange {what} skipped: the half is slot {OwnerOf(half)}'s, not slot {actorSlot}'s");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Ends the exchange at this post (either half) on every computer: the goods waiting on each half are no longer
        /// held, so each colony's workers carry its own home. Used by the actions, the tick and a colony's handover.
        /// </summary>
        public void End(DistrictCrossing half, DistrictCrossing partner, string why)
        {
            CrossingExchange mine = Of(half), theirs = Of(partner);
            Plugin.Log($"[Colony] Exchange {mine?.Serial ?? theirs?.Serial ?? 0} ended ({why}): "
                + $"{mine?.Held ?? 0}/{mine?.Total ?? 0} {mine?.GoodId} and {theirs?.Held ?? 0}/{theirs?.Total ?? 0} {theirs?.GoodId} were waiting");
            Release(half, mine);
            Release(partner, theirs);
        }

        private static void Release(DistrictCrossing half, CrossingExchange side)
        {
            if (side == null) return;
            if (side.IsActive && side.GivesGoods && side.Held > 0 && half)
            {
                Inventory inventory = half.GetComponent<DistrictCrossingInventory>()?.Inventory;
                int reserved = inventory == null ? 0 : Math.Min(side.Held, inventory._reservedStock.Amount(side.GoodId));
                if (reserved > 0) inventory.UnreserveStock(new GoodAmount(side.GoodId, reserved));
            }
            side.Clear();
        }

        /// <summary>
        /// A Trading Post with an open exchange is being removed (CrossingExchange.DeleteEntity, on its offering half): it
        /// ends with the post. What waited on each half is left as recovered goods where it stood, as the game leaves any
        /// building's stock, for whichever colony's workers reach it first. Played on every computer as the deletion is;
        /// it only logs what was waiting and tells the two colonies (C5).
        /// </summary>
        internal void OnPostRemoved(DistrictCrossing half, CrossingExchange side)
        {
            CrossingExchange other = Of(TradingPosts.Partner(half));
            int a = side.Colony, b = other?.Colony ?? -1;
            Plugin.Log($"[Colony] Exchange {side.Serial} ended with its Trading Post, which was removed: {side.Held}/{side.Total} {side.GoodId} "
                + $"and {other?.Held ?? 0}/{other?.Total ?? 0} {other?.GoodId} were waiting (left as recovered goods)");
            Tell(() => a, () => b, () => T("BeaverBuddies.Colony.Trade.Notice.PostRemoved"), warning: true);
        }

        // ---- once a day (every computer): the trade's own check, and with detailed logging its line (T3) ----

        private static readonly ColonyProfiler.Spot DailyTrade = ColonyProfiler.Declare("Trading post daily check");

        /// <summary>
        /// Once a day, cheap (every crossing half once): that the goods each running exchange holds are on its half and
        /// reserved there, and that no post holds more of a good on its two halves than its room. A broken one is a bug:
        /// it is logged as a warning (the game would throw when such a round crossed). With detailed logging on, a line
        /// adds up the trade: exchanges, goods held and waiting to be hauled away, what crossed since yesterday, and each
        /// colony's stock of those goods. It reads the simulation and changes nothing, so it is the same on every
        /// computer that agrees.
        /// </summary>
        private void DailyCheck()
        {
            long started = ColonyProfiler.Start();
            try
            {
                int postHalves = 0, running = 0, offered = 0, problems = 0;
                var held = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var waiting = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (DistrictCrossing half in _entityComponentRegistry.GetEnabled<DistrictCrossing>())
                {
                    CrossingExchange side = Of(half);
                    Inventory inventory = half.GetComponent<DistrictCrossingInventory>()?.Inventory;
                    if (side == null || !side.AtTradingPost || inventory == null) continue;
                    DistrictCrossing partner = TradingPosts.Partner(half);
                    Inventory partnerInventory = partner ? partner.GetComponent<DistrictCrossingInventory>()?.Inventory : null;
                    postHalves++;
                    if (side.ProposedHere && side.IsActive) running++;
                    if (side.ProposedHere && side.State == ExchangeState.Proposed) offered++;
                    int heldHere = side.IsActive && side.GivesGoods ? side.Held : 0;
                    if (heldHere > 0 && (inventory.AmountInStock(side.GoodId) < heldHere || inventory._reservedStock.Amount(side.GoodId) < heldHere))
                    {
                        problems++;
                        Plugin.LogWarning($"[Colony] Trade check: exchange {side.Serial} holds {heldHere} {side.GoodId} on slot {OwnerOf(half)}'s half, "
                            + $"which has {inventory.AmountInStock(side.GoodId)} ({inventory._reservedStock.Amount(side.GoodId)} reserved)");
                    }
                    var stock = inventory.Stock;
                    for (int i = 0; i < stock.Count; i++)
                    {
                        string good = stock[i].GoodId;
                        int amount = stock[i].Amount;
                        int ours = good == side.GoodId ? heldHere : 0;
                        if (ours > 0) Add(held, good, Math.Min(ours, amount));
                        if (amount > ours) Add(waiting, good, amount - ours);
                        // Each pair once: from the half whose partner has the higher id.
                        int theirs = partnerInventory?.AmountInStock(good) ?? 0;
                        if (amount + theirs > ExchangeTerms.MaxAmount && string.CompareOrdinal(Id(half), Id(partner)) < 0)
                        {
                            problems++;
                            Plugin.LogWarning($"[Colony] Trade check: a Trading Post's halves hold {amount} and {theirs} {good}, more than its room of {ExchangeTerms.MaxAmount}");
                        }
                    }
                }
                TradeTotals totals = ColonyTradeLedger.Instance?.Totals;
                if (Settings.Debug)
                {
                    var crossed = totals?.Since(totalsAtDayStart) ?? new List<(int, int, string, int)>();
                    string stockOfCrossed = string.Join(", ", crossed.Select(c => c.Item3).Distinct().OrderBy(g => g, StringComparer.Ordinal)
                        .Select(good => good + " " + string.Join(" ", Enumerable.Range(0, ColonySlotTable.MaxSlots)
                            .Where(slot => OwnsDistrict(slot)).Select(slot => $"{slot}:{ColonyStockOf(slot, good)}"))));
                    Plugin.Log($"[Colony] Day {_gameCycleService.Cycle}-{_gameCycleService.CycleDay} trade: {postHalves / 2} posts, {running} exchanges running, "
                        + $"{offered} offered; held {Describe(held)}; waiting to be hauled away {Describe(waiting)}; crossed since yesterday "
                        + $"{(crossed.Count == 0 ? "nothing" : string.Join(", ", crossed.Select(c => $"{c.Item1}>{c.Item2} {c.Item3} {c.Item4}")))}; "
                        + $"stock {(stockOfCrossed.Length == 0 ? "-" : stockOfCrossed)}; checks {(problems == 0 ? "ok" : problems + " broken")}");
                }
                totalsAtDayStart = totals?.Copy();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Trade check failed: " + error.Message);
            }
            finally
            {
                ColonyProfiler.Stop(DailyTrade, started);
            }
        }

        private static void Add(SortedDictionary<string, int> tally, string good, int amount)
        {
            tally.TryGetValue(good, out int total);
            tally[good] = total + amount;
        }

        private static string Describe(SortedDictionary<string, int> tally) =>
            tally.Count == 0 ? "nothing" : string.Join(", ", tally.Select(t => $"{t.Key} {t.Value}"));

        private static string Id(DistrictCrossing half) => ReplayEvent.GetEntityID(half) ?? "";

        private bool OwnsDistrict(int slot)
        {
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
                if (DistrictOwner.OwnerOfDistrict(districtCenter) == slot) return true;
            return false;
        }

        private int ColonyStockOf(int slot, string goodId)
        {
            if (!_goodService.HasGood(goodId)) return 0;
            int total = 0;
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                if (DistrictOwner.OwnerOfDistrict(districtCenter) != slot) continue;
                total += _resourceCountingService.GetDistrictResourceCounter(districtCenter).GetResourceCount(goodId).AvailableStock;
            }
            return total;
        }

        // ---- notices (display only: shown to whichever player the news is for) ----

        /// <summary>
        /// Shows a notice if the local player plays one of the colonies named. Called from actions and ticks that every
        /// computer plays, so it is built only where shown and can never throw into the simulation.
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

        /// <summary>"100 Planks", "1 Beaver", or "nothing" for a side that gives nothing.</summary>
        public string Amount(int amount, string goodId) =>
            amount > 0 ? $"{amount} {GoodName(goodId, one: amount == 1)}" : T("BeaverBuddies.Colony.Trade.NothingInReturn");

        /// <summary>An item's name as the game writes it: the plural, or the singular for one.</summary>
        public string GoodName(string goodId, bool one = false)
        {
            if (goodId == ExchangeTerms.Science) return T("BeaverBuddies.Colony.Trade.ItemScience");
            if (goodId == ExchangeTerms.Beavers) return T(one ? "BeaverBuddies.Colony.Trade.ItemBeaver" : "BeaverBuddies.Colony.Trade.ItemBeavers");
            try
            {
                var good = _goodService.GetGood(goodId);
                return one ? good.DisplayName.Value : good.PluralDisplayName.Value;
            }
            catch (Exception) { return goodId; }
        }

        public static string ColonyName(int slot)
        {
            string name = ColonySlotService.Instance?.Table.NameOf(slot);
            return string.IsNullOrEmpty(name) ? string.Format(T("BeaverBuddies.Colony.Trade.ColonyN"), slot + 1) : name;
        }

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
        public int rounds = 1;
        public bool repeat;
        /// <summary>What the offering colony keeps back of what it gives (0 for no floor); the other side sets its own later.</summary>
        public int keep;

        // The offering half must be the actor's (checked again when played).
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            ColonyExchangeService.Instance?.Propose(half, slot, giveGood, giveAmount, getGood, getAmount, rounds, repeat, keep);
        }

        public override string ToActionString() => $"Offering {giveAmount} {giveGood} for {getAmount} {getGood}, {(repeat ? "repeating" : rounds + " times")}";
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
        public int rounds = 1;
        public bool repeat;

        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            if (half == null) return;
            ColonyExchangeService.Instance?.Accept(half, slot, serial, giveGood, giveAmount, getGood, getAmount, rounds, repeat);
        }

        public override string ToActionString() => $"Accepting {getAmount} {getGood} for {giveAmount} {giveGood}";
    }

    /// <summary>
    /// A colony declines or withdraws an offer at its half of a trading post, or asks to end (or agrees to end) the
    /// exchange running there.
    /// </summary>
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

    /// <summary>A colony sets what it keeps back of what it gives in the exchange at its half of a trading post.</summary>
    [Serializable]
    public class ExchangeFloorSetEvent : ReplayEvent
    {
        public string crossingID;
        public int serial;
        public int keep;

        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            if (half == null) return;
            ColonyExchangeService.Instance?.SetKeep(half, slot, serial, keep);
        }

        public override string ToActionString() => $"Keeping {keep} back in an exchange";
    }

    /// <summary>A colony wants the exchange at its half of a trading post to go on (no longer asks, or refuses, to end it).</summary>
    [Serializable]
    public class ExchangeKeptEvent : ReplayEvent
    {
        public string crossingID;
        public int serial;

        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var half = GetComponent<DistrictCrossing>(context, crossingID);
            if (half == null) return;
            ColonyExchangeService.Instance?.Keep(half, slot, serial);
        }

        public override string ToActionString() => "Keeping an exchange";
    }
}
