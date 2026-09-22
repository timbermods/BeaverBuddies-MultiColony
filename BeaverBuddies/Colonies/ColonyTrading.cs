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
            ColonyDigest.Note("totals", from, to, ColonyDigest.Of(goodId), total + amount);
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
    }

    // ---- the crossing's buffer ----

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): every good may wait in a crossing half up to a fixed number:
        StorableGood storableGood = StorableGood.CreateAsTakeable(good2);
        StorableGoodAmount good = new StorableGoodAmount(storableGood, DistrictCrossingCapacity);   // 30
        inventoryInitializer.AddAllowedGood(good);
     */
    // A crossing half has room for 100 of each good instead of 30, in every game (the number is read when a crossing is
    // made or loaded, before anything tells a Trading Post from a District Crossing, so it must not depend on either).
    // At a Trading Post the room is only ever used for a round under way: one side's goods waiting on their half (at
    // most ExchangeTerms.MaxAmount, which equals this), or the other colony's waiting to be hauled away after crossing.
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

    // ---- goods arriving on a half, and crossing ----

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the game passes goods across as soon as they arrive on a half
     * (DistrictCrossingInventory.OnInventoryStockChanged, for a positive change):
        _linked.Reserve(e.GoodAmount);
        TransferStock(e.GoodAmount.GoodId, e.GoodAmount.Amount);
     * and TransferStock (not while the half is itself receiving, _mirrorOperationLock):
        amount = Math.Min(amount, Inventory.UnreservedAmountInStock(goodId));
        if (amount > 0) { Inventory.TakeExported(goodAmount); _linked.GiveStock(goodAmount); }
     */
    // At a Trading Post nothing passes by itself: a load of the exchange's good arriving on a half is held there for the
    // round (reserved, so no beaver takes it), and a round crosses only when both sides are in, all at once
    // (ColonyExchangeService.Cross, which is the one caller let through). Anything else arriving stays unreserved, and
    // the half's own workers carry it home. A District Crossing that ends up joining two colonies passes nothing.
    // District Crossings within one colony are the game's.
    [HarmonyPatch(typeof(DistrictCrossingInventory), nameof(DistrictCrossingInventory.TransferStock))]
    static class TradingPostPassPatcher
    {
        static void Prefix(DistrictCrossingInventory __instance, string goodId, ref int amount)
        {
            DistrictCrossing half = __instance.GetComponent<DistrictCrossing>();
            if (!TradingPosts.TradesOnlyByExchange(half) || ColonyExchangeService.Crossing) return;
            // The game calls this, unlocked, right after a load arrived on the half; locked while the half receives.
            if (amount > 0 && __instance._mirrorOperationLock.IsUnlocked)
                ColonyExchangeService.Instance?.OnArrival(half, __instance.Inventory, goodId, amount);
            amount = 0;
        }
    }

    // At a Trading Post a half's workers bring only their colony's goods for the round under way, up to what is still
    // missing on the half; nothing moves by import and export settings. What waits on a half unreserved (the other
    // colony's goods after a round crossed, or its own after an exchange ended) is carried off as the game already does
    // (emptying). A crossing that joins two colonies brings nothing. District Crossings within one colony are the game's.
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
            DistrictCrossingInventory crossingInventory = __instance._districtCrossingInventory;
            string goodId = ColonyExchangeService.GoodGiven(crossing);
            if (goodId == null || !crossing.CanExport || !crossingInventory) return false;
            // Loads already being carried in (IncomingStock leaves out the room the half keeps for the other half's goods).
            int wanted = ColonyExchangeService.StillToBring(crossing, crossingInventory.IncomingStock(goodId));
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
}
