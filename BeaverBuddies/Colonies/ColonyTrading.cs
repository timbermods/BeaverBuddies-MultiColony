using BeaverBuddies.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Buildings;
using Timberborn.Carrying;
using Timberborn.DistributionSystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The Trading Post is its own building (Buildings/DistrictManagement/MultiColonyTradingPost, marked by
    /// <see cref="MultiColonyTradingPostSpec"/>): a District Crossing's model and two halves, each run by its own
    /// district's beavers, that colonies barter through. Goods cross it only by exchanges agreed between the two
    /// colonies (see ColonyExchangeService), never by import and export settings. The game's District Crossing is left
    /// as it is, for a colony's own districts; one that ends up joining two colonies anyway moves nothing between them.
    /// Whether a post trades is worked out from its districts' owners, never saved.
    /// </summary>
    public static class TradingPosts
    {
        // Per building spec (specs are shared objects): whether it is a Trading Post.
        private static readonly Dictionary<BuildingSpec, bool> isTradingPostTemplate = new Dictionary<BuildingSpec, bool>();

        public static DistrictCenter DistrictOf(BaseComponent half) =>
            half ? half.GetComponent<DistrictBuilding>()?.District : null;

        public static DistrictCrossing Partner(DistrictCrossing half) => half ? half._linked : null;

        /// <summary>A half of a Trading Post building, in any game, whatever it joins.</summary>
        public static bool IsTradingPostBuilding(DistrictCrossing half) => ColonyExchangeService.Of(half)?.AtTradingPost == true;

        /// <summary>An entity, or a building's preview, is a Trading Post half.</summary>
        public static bool IsTradingPostBuilding(BaseComponent entity) =>
            entity && entity.GetComponent<MultiColonyTradingPostSpec>() != null;

        /// <summary>A building template is the Trading Post (either faction's).</summary>
        public static bool IsTradingPostTemplate(BuildingSpec spec)
        {
            if (spec == null) return false;
            lock (isTradingPostTemplate)
            {
                if (!isTradingPostTemplate.TryGetValue(spec, out bool tradingPost))
                {
                    tradingPost = spec.HasSpec<MultiColonyTradingPostSpec>();
                    isTradingPostTemplate[spec] = tradingPost;
                }
                return tradingPost;
            }
        }

        /// <summary>The two halves stand in districts of different players (whatever the crossing is).</summary>
        public static bool JoinsTwoColonies(DistrictCrossing half)
        {
            if (!ColonyModeService.IsSeparateColonies || !half) return false;
            int? mine = DistrictOwner.OwnerOfDistrict(DistrictOf(half));
            int? theirs = DistrictOwner.OwnerOfDistrict(DistrictOf(Partner(half)));
            return mine != null && theirs != null && mine.Value != theirs.Value;
        }

        /// <summary>A Trading Post (both halves) between two colonies: it can hold an exchange.</summary>
        public static bool IsTradingPost(DistrictCrossing half) =>
            IsTradingPostBuilding(half) && IsTradingPostBuilding(Partner(half)) && JoinsTwoColonies(half);

        /// <summary>
        /// Nothing crosses by import and export settings: never at a Trading Post, and never between two colonies
        /// (a District Crossing that ends up joining two colonies moves nothing between them).
        /// </summary>
        public static bool TradesOnlyByExchange(DistrictCrossing half) => IsTradingPostBuilding(half) || JoinsTwoColonies(half);
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

    // ---- the crossing's buffer ----

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): every good may wait in a crossing half up to a fixed number:
        StorableGood storableGood = StorableGood.CreateAsTakeable(good2);
        StorableGoodAmount good = new StorableGoodAmount(storableGood, DistrictCrossingCapacity);   // 30
        inventoryInitializer.AddAllowedGood(good);
     */
    // A crossing buffers up to 100 of each good instead of 30, in every game (the number is read when a crossing is
    // made or loaded, so it must not depend on the mode). The buffer is what one side can have delivered and not yet
    // hauled away by the other.
    [HarmonyPatch(typeof(DistrictCrossingInventoryInitializer), nameof(DistrictCrossingInventoryInitializer.AllowEveryGoodAsTakeable))]
    static class TradingPostCapacityPatcher
    {
        public const int Capacity = 100;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo field
                    && field.Name == "DistrictCrossingCapacity")
                {
                    instruction.opcode = OpCodes.Ldc_I4;
                    instruction.operand = Capacity;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced == 0) Plugin.LogWarning("[Colony] The crossing capacity was not found; crossings keep the game's own");
        }
    }

    // ---- where goods pass from one half to the other ----

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the only place goods cross (TransferStock -> linked half's GiveStock):
        using (_mirrorOperationLock.Lock())
        {
            Inventory.GiveImported(goodAmount);
        }
     */
    // __instance is the half receiving the goods; its partner sent them. At a trading post: counts the exchange and
    // the ledger.
    [HarmonyPatch(typeof(DistrictCrossingInventory), nameof(DistrictCrossingInventory.GiveStock))]
    static class TradingPostTransferPatcher
    {
        static void Postfix(DistrictCrossingInventory __instance, GoodAmount goodAmount)
        {
            if (!ColonyModeService.IsSeparateColonies || goodAmount.Amount <= 0) return;
            DistrictCrossingInventory sender = __instance._linked;
            if (!sender) return;
            int? from = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(sender));
            int? to = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(__instance));
            if (from == null || to == null || from.Value == to.Value) return;
            ColonyExchangeService.Instance?.CountDelivery(sender.GetComponent<DistrictCrossing>(), goodAmount.GoodId, goodAmount.Amount);
            ColonyTradeLedger.Instance?.Record(from.Value, to.Value, goodAmount.GoodId, goodAmount.Amount);
        }
    }

    // At a Trading Post goods pass only for a running exchange, up to what the sending colony still owes (and a
    // crossing that joins two colonies passes nothing). A load still on the way when the exchange ends (or one over
    // the amount) stays on its own half, and its colony's workers carry it home (the game empties a crossing half of
    // what nobody takes across).
    [HarmonyPatch(typeof(DistrictCrossingInventory), nameof(DistrictCrossingInventory.TransferStock))]
    static class TradingPostPassPatcher
    {
        static void Prefix(DistrictCrossingInventory __instance, string goodId, ref int amount)
        {
            DistrictCrossing half = __instance.GetComponent<DistrictCrossing>();
            if (!TradingPosts.TradesOnlyByExchange(half)) return;
            amount = TradingPosts.IsTradingPost(half) ? Math.Min(amount, ColonyExchangeService.MayPass(half, goodId)) : 0;
        }
    }

    // At a Trading Post a half's workers bring only their colony's side of a running exchange, as far as the pace
    // allows; nothing moves by import and export settings. The other colony's workers haul away what arrives on their
    // half as the game already does (emptying). A crossing that joins two colonies brings nothing. District Crossings
    // between one colony's own districts are the game's.
    [HarmonyPatch(typeof(DistrictCrossingWorkplaceBehavior), nameof(DistrictCrossingWorkplaceBehavior.TryExport))]
    static class TradingPostCarryPatcher
    {
        static bool Prefix(DistrictCrossingWorkplaceBehavior __instance, BehaviorAgent agent, ref bool __result)
        {
            long started = ColonyProfiler.Start();
            try
            {
                return Carry(__instance, agent, ref __result);
            }
            finally
            {
                ColonyProfiler.Stop("Trading post workers", started);
            }
        }

        static bool Carry(DistrictCrossingWorkplaceBehavior __instance, BehaviorAgent agent, ref bool __result)
        {
            DistrictCrossing crossing = __instance._districtCrossing;
            if (!TradingPosts.TradesOnlyByExchange(crossing)) return true;
            __result = false;
            if (!TradingPosts.IsTradingPost(crossing)) return false;
            DistrictCrossingInventory crossingInventory = __instance._districtCrossingInventory;
            string goodId = ColonyExchangeService.GoodGiven(crossing);
            if (goodId == null || ExchangeTerms.IsSpecial(goodId) || !crossing.CanExport || !crossingInventory) return false;
            // Goods already being carried in count towards the pace (IncomingStock leaves out what has passed across).
            int wanted = ColonyExchangeService.StillToBring(crossing) - crossingInventory.IncomingStock(goodId);
            if (wanted <= 0) return false;
            Inventory inventory = crossingInventory.Inventory;
            int carry = Math.Min(wanted, inventory.UnreservedCapacity(goodId));
            CarrierInventoryFinder finder = agent.GetComponent<CarrierInventoryFinder>();
            __result = carry > 0 && finder != null && finder.TryCarryFromAnyInventoryLimited(goodId, inventory, carry);
            return false;
        }
    }

    // Distribution settings never send goods across a Trading Post (or between two colonies), neither by workers nor
    // by the crossing's own exporter of goods left waiting on a half (which would otherwise send an exchange's goods
    // straight back).
    [HarmonyPatch(typeof(DistrictCrossing), nameof(DistrictCrossing.CanExportGood))]
    static class TradingPostNoSettingsTradePatcher
    {
        static bool Prefix(DistrictCrossing __instance, ref bool __result)
        {
            if (!TradingPosts.TradesOnlyByExchange(__instance)) return true;
            __result = false;
            return false;
        }
    }

    // ---- science gifts, as actions every computer plays ----

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
