using BeaverBuddies.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Buildings;
using Timberborn.Carrying;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A District Crossing between two players' districts is a trading post. Nothing new is built: the crossing keeps
    /// its two halves, each run by its own district's beavers, and goods still move by each district's own import and
    /// export settings. On top: a ledger of what passed each way, and gifts (a fixed number of a good, or science).
    /// Whether a crossing is a trading post is worked out from its districts' owners, never saved.
    /// </summary>
    public static class TradingPosts
    {
        public static DistrictCenter DistrictOf(BaseComponent half) =>
            half ? half.GetComponent<DistrictBuilding>()?.District : null;

        public static DistrictCrossing Partner(DistrictCrossing half) => half ? half._linked : null;

        /// <summary>Both halves stand in districts of different players.</summary>
        public static bool IsTradingPost(DistrictCrossing half)
        {
            if (!ColonyModeService.IsSeparateColonies || !half) return false;
            int? mine = DistrictOwner.OwnerOfDistrict(DistrictOf(half));
            int? theirs = DistrictOwner.OwnerOfDistrict(DistrictOf(Partner(half)));
            return mine != null && theirs != null && mine.Value != theirs.Value;
        }
    }

    /// <summary>
    /// A gift under way from this half's colony to the partner half's, saved with the crossing: the good and how much is
    /// still to go. Counted down as the goods actually pass (see the GiveStock patch).
    /// </summary>
    public class CrossingGift : BaseComponent, IPersistentEntity
    {
        private static readonly ComponentKey GiftKey = new ComponentKey("BeaverBuddies.CrossingGift");
        private static readonly PropertyKey<string> GoodKey = new PropertyKey<string>("Good");
        private static readonly PropertyKey<int> RemainingKey = new PropertyKey<int>("Remaining");

        public string GoodId { get; private set; }
        public int Remaining { get; private set; }
        public bool IsActive => !string.IsNullOrEmpty(GoodId) && Remaining > 0;

        public void Start(string goodId, int amount)
        {
            if (IsActive && GoodId == goodId) Remaining += amount;
            else
            {
                GoodId = goodId;
                Remaining = amount;
            }
        }

        public void Delivered(int amount)
        {
            Remaining -= amount;
            if (Remaining <= 0)
            {
                Remaining = 0;
                GoodId = null;
            }
        }

        public void Save(IEntitySaver entitySaver)
        {
            if (!IsActive) return;
            IObjectSaver saver = entitySaver.GetComponent(GiftKey);
            saver.Set(GoodKey, GoodId);
            saver.Set(RemainingKey, Remaining);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (!entityLoader.TryGetComponent(GiftKey, out IObjectLoader loader)) return;
            if (loader.Has(GoodKey)) GoodId = loader.Get(GoodKey);
            if (loader.Has(RemainingKey)) Remaining = loader.Get(RemainingKey);
        }
    }

    /// <summary>
    /// What has passed between each pair of colonies, per good. Simulation state: updated where goods actually move,
    /// saved, and identical on every computer.
    /// </summary>
    public class ColonyTradeLedger : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey LedgerKey = new SingletonKey("BeaverBuddies.ColonyTradeLedger");
        private static readonly ListKey<string> EntriesKey = new ListKey<string>("Entries");

        private readonly ISingletonLoader _singletonLoader;
        // (from, to, good) -> amount; sorted so saving never depends on the order trades happened in.
        private readonly SortedDictionary<(int, int, string), int> totals =
            new SortedDictionary<(int, int, string), int>(Comparer<(int, int, string)>.Create((a, b) =>
            {
                int c = a.Item1.CompareTo(b.Item1);
                if (c == 0) c = a.Item2.CompareTo(b.Item2);
                return c != 0 ? c : string.CompareOrdinal(a.Item3, b.Item3);
            }));

        public static ColonyTradeLedger Instance => SingletonManager.GetSingleton<ColonyTradeLedger>();

        public ColonyTradeLedger(ISingletonLoader singletonLoader)
        {
            _singletonLoader = singletonLoader;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(LedgerKey, out IObjectLoader loader) || !loader.Has(EntriesKey)) return;
            foreach (string entry in loader.Get(EntriesKey))
            {
                string[] parts = entry.Split('|');
                if (parts.Length == 4 && int.TryParse(parts[0], out int from) && int.TryParse(parts[1], out int to)
                    && int.TryParse(parts[3], out int amount))
                    totals[(from, to, parts[2])] = amount;
            }
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            if (totals.Count == 0) return;
            singletonSaver.GetSingleton(LedgerKey).Set(EntriesKey,
                totals.Select(t => $"{t.Key.Item1}|{t.Key.Item2}|{t.Key.Item3}|{t.Value}").ToList());
        }

        public void Record(int from, int to, string goodId, int amount)
        {
            totals.TryGetValue((from, to, goodId), out int total);
            totals[(from, to, goodId)] = total + amount;
        }

        /// <summary>Goods that went from <paramref name="from"/> to <paramref name="to"/>, most first.</summary>
        public List<KeyValuePair<string, int>> Sent(int from, int to) =>
            totals.Where(t => t.Key.Item1 == from && t.Key.Item2 == to)
                .Select(t => new KeyValuePair<string, int>(t.Key.Item3, t.Value))
                .OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).ToList();
    }

    // ---- where goods pass from one half to the other ----

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the only place goods cross (TransferStock -> linked half's GiveStock):
        using (_mirrorOperationLock.Lock())
        {
            Inventory.GiveImported(goodAmount);
        }
     */
    // __instance is the half receiving the goods; its partner sent them. Counts the ledger at a trading post, and
    // counts down a gift under way from the sender.
    [HarmonyPatch(typeof(DistrictCrossingInventory), nameof(DistrictCrossingInventory.GiveStock))]
    static class TradingPostTransferPatcher
    {
        static void Postfix(DistrictCrossingInventory __instance, GoodAmount goodAmount)
        {
            if (!ColonyModeService.IsSeparateColonies || goodAmount.Amount <= 0) return;
            DistrictCrossingInventory sender = __instance._linked;
            if (!sender) return;
            CrossingGift gift = sender.GetComponent<CrossingGift>();
            if (gift != null && gift.IsActive && gift.GoodId == goodAmount.GoodId) gift.Delivered(goodAmount.Amount);
            int? from = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(sender));
            int? to = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(__instance));
            if (from != null && to != null && from.Value != to.Value)
                ColonyTradeLedger.Instance?.Record(from.Value, to.Value, goodAmount.GoodId, goodAmount.Amount);
        }
    }

    // A gift: the giving half's workers bring the gifted good whatever the partner's import settings, until the amount
    // has passed. Everything else is the game's own export (the prefix falls through to it).
    [HarmonyPatch("Timberborn.DistributionSystem.DistrictCrossingWorkplaceBehavior", "TryExport")]
    static class TradingPostGiftCarryPatcher
    {
        static readonly AccessTools.FieldRef<object, DistrictCrossing> crossingField =
            AccessTools.FieldRefAccess<DistrictCrossing>(AccessTools.TypeByName("Timberborn.DistributionSystem.DistrictCrossingWorkplaceBehavior"), "_districtCrossing");
        static readonly AccessTools.FieldRef<object, DistrictCrossingInventory> inventoryField =
            AccessTools.FieldRefAccess<DistrictCrossingInventory>(AccessTools.TypeByName("Timberborn.DistributionSystem.DistrictCrossingWorkplaceBehavior"), "_districtCrossingInventory");

        static bool Prefix(object __instance, BehaviorAgent agent, ref bool __result)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            DistrictCrossing crossing = crossingField(__instance);
            DistrictCrossingInventory crossingInventory = inventoryField(__instance);
            CrossingGift gift = crossing ? crossing.GetComponent<CrossingGift>() : null;
            if (gift == null || !gift.IsActive || !crossing.CanExport || !crossingInventory) return true;
            Inventory inventory = crossingInventory.Inventory;
            string goodId = gift.GoodId;
            // Goods already being carried in count towards the gift (IncomingStock leaves out what has passed across).
            int wanted = gift.Remaining - crossingInventory.IncomingStock(goodId);
            if (wanted <= 0) return true;
            if (inventory.UnreservedAmountInStock(goodId) > 0)
            {
                crossingInventory.TransferStock(goodId, Math.Min(wanted, inventory.UnreservedAmountInStock(goodId)));
                // Passing across counts the gift down at once; carry only what is still missing.
                if (!gift.IsActive || gift.GoodId != goodId) return true;
                wanted = gift.Remaining - crossingInventory.IncomingStock(goodId);
                if (wanted <= 0) return true;
            }
            int carry = Math.Min(wanted, inventory.UnreservedCapacity(goodId));
            CarrierInventoryFinder finder = agent.GetComponent<CarrierInventoryFinder>();
            if (carry > 0 && finder != null && finder.TryCarryFromAnyInventoryLimited(goodId, inventory, carry))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    // While a gift is under way, the receiving side must not send that good straight back.
    [HarmonyPatch(typeof(DistrictCrossing), nameof(DistrictCrossing.CanExportGood))]
    static class TradingPostNoBounceBackPatcher
    {
        static bool Prefix(DistrictCrossing __instance, DistributableGood myDistributableGood, ref bool __result)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            CrossingGift partnerGift = TradingPosts.Partner(__instance)?.GetComponent<CrossingGift>();
            if (partnerGift == null || !partnerGift.IsActive || partnerGift.GoodId != myDistributableGood.GoodId) return true;
            __result = false;
            return false;
        }
    }

    // ---- a District Crossing is available from the start, and cheap, in a separate-colonies game ----

    static class TradingPostCost
    {
        // Per building spec (specs are shared objects): whether it is a District Crossing.
        private static readonly Dictionary<BuildingSpec, bool> isCrossing = new Dictionary<BuildingSpec, bool>();

        public static bool IsCrossing(BuildingSpec spec)
        {
            lock (isCrossing)
            {
                if (!isCrossing.TryGetValue(spec, out bool crossing))
                {
                    crossing = spec.Blueprint?.HasSpec<DistrictCrossingSpec>() == true;
                    isCrossing[spec] = crossing;
                }
                return crossing;
            }
        }

        public static readonly ImmutableArray<GoodAmountSpec> CheapCost =
            ImmutableArray.Create(new GoodAmountSpec { Id = "Log", Amount = 10 });
    }

    // Players need a crossing most when they are short of everything; it must not wait for planks or 600 science.
    [HarmonyPatch(typeof(BuildingSpec), nameof(BuildingSpec.ScienceCost), MethodType.Getter)]
    static class TradingPostScienceCostPatcher
    {
        static void Postfix(BuildingSpec __instance, ref int __result)
        {
            if (__result != 0 && ColonyModeService.IsSeparateColonies && TradingPostCost.IsCrossing(__instance)) __result = 0;
        }
    }

    [HarmonyPatch(typeof(BuildingSpec), nameof(BuildingSpec.BuildingCost), MethodType.Getter)]
    static class TradingPostBuildingCostPatcher
    {
        static void Postfix(BuildingSpec __instance, ref ImmutableArray<GoodAmountSpec> __result)
        {
            if (ColonyModeService.IsSeparateColonies && TradingPostCost.IsCrossing(__instance)) __result = TradingPostCost.CheapCost;
        }
    }

    // ---- gifts, as actions every computer plays ----

    /// <summary>
    /// A player gives a number of a good through a trading post: their own half's workers bring it whatever the
    /// partner's import settings, until that many have passed.
    /// </summary>
    [Serializable]
    public class GiftGoodsEvent : ReplayEvent
    {
        public string crossingID;
        public string goodId;
        public int amount;

        // The giving half must be the actor's (or an absent player's).
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(crossingID);

        public override void Replay(IReplayContext context)
        {
            var crossing = GetComponent<DistrictCrossing>(context, crossingID);
            if (crossing == null || amount <= 0 || string.IsNullOrEmpty(goodId)) return;
            if (!TradingPosts.IsTradingPost(crossing))
            {
                Plugin.LogWarning($"[Colony] Gift of {amount} {goodId} skipped: the crossing is not between two colonies");
                return;
            }
            crossing.GetComponent<CrossingGift>()?.Start(goodId, Math.Min(amount, 1000));
            Plugin.Log($"[Colony] Gift of {amount} {goodId} started through {crossingID}");
        }

        public override string ToActionString() => $"Gifting {amount} {goodId}";
    }

    /// <summary>A player gives science to another colony (separate science only).</summary>
    [Serializable]
    public class GiftScienceEvent : ReplayEvent
    {
        public int toSlot;
        public int amount;

        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context)
        {
            var science = ColonyScienceService.Instance;
            int from = Math.Max(0, slot);
            if (science == null || !science.Enabled || amount <= 0 || toSlot == from) return;
            if (toSlot < 0 || toSlot >= ColonySlotTable.MaxSlots) return;
            // Checked here, when every computer plays it: science may have been spent since the click.
            if (science.PointsOf(from) < amount)
            {
                Plugin.LogWarning($"[Colony] Science gift of {amount} from slot {from} skipped: only {science.PointsOf(from)} left");
                return;
            }
            science.Subtract(from, amount);
            science.Add(toSlot, amount);
            Plugin.Log($"[Colony] Slot {from} gave {amount} science to slot {toSlot}");
        }

        public override string ToActionString() => $"Gifting {amount} science to slot {toSlot}";
    }
}
