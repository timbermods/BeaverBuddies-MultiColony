using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.InventorySystem;
using Timberborn.Planting;
using Timberborn.Stockpiles;
using Timberborn.Yielding;

namespace BeaverBuddies.Factions
{
    /*
     * D18: in a mixed game every faction's goods and plants are loaded, and the game would let any building use any of
     * them (a Folktails warehouse set to Corn, a Folktails farmhouse planting Coffee, a Folktails gatherer picking Mangrove
     * fruit). Each building keeps to its own faction's, keyed only by its template, so every computer decides the same.
     * A common building (no faction of its own) keeps the game's behaviour.
     */

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Stockpiles: StockpileInventoryInitializer.Initialize(stockpile, inventory)
        inventoryInitializer.AddAllowedGoodType(component.WhitelistedGoodType);
     * InventorySystem: InventoryInitializer.AddAllowedGoodType → GetGoods(goodType): every loaded good of that type.
     * A faction's stockpile holds that faction's goods (the common ones and its own), and so its good picker lists those.
     */
    [HarmonyPatch(typeof(StockpileInventoryInitializer), nameof(StockpileInventoryInitializer.Initialize))]
    static class FactionStockpilePatcher
    {
        [ThreadStatic] internal static string StockpileFaction;

        static void Prefix(Stockpile subject)
        {
            if (!MixedFactions.IsOn) return;
            StockpileFaction = ColonyFactionService.SimFactionOf(subject);
        }

        static void Finalizer()
        {
            if (MixedFactions.IsOn) StockpileFaction = null;
        }
    }

    [HarmonyPatch(typeof(InventoryInitializer), "GetGoods")]
    static class FactionStockpileGoodsPatcher
    {
        static void Postfix(ref IEnumerable<string> __result)
        {
            if (!MixedFactions.IsOn) return;
            string faction = FactionStockpilePatcher.StockpileFaction;
            FactionCatalog catalog = FactionCatalog.Instance;
            if (faction == null || catalog == null) return;
            HashSet<string> goods = catalog.GoodsOf(faction);
            __result = __result.Where(goods.Contains).ToList();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Planting: PlanterBuilding.GetAllowedPlantables (in Awake)
        every PlantableSpec of the building's PlantableResourceGroup ("Farmhouse", "Forester", ...)
     * A faction's farmhouse or forester plants the common plants and its own faction's.
     */
    [HarmonyPatch(typeof(PlanterBuilding), "GetAllowedPlantables")]
    static class FactionPlanterPatcher
    {
        static void Postfix(PlanterBuilding __instance, ref IEnumerable<PlantableSpec> __result)
        {
            if (!MixedFactions.IsOn) return;
            FactionCatalog catalog = FactionCatalog.Instance;
            string faction = ColonyFactionService.SimFactionOf(__instance);
            if (faction == null || catalog == null) return;
            __result = __result.Where(plantable =>
            {
                string own = catalog.FactionOfTemplate(plantable.TemplateName);
                return own == null || own == faction;
            }).ToList();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Yielding: YieldRemovingBuilding.IsAllowed(YielderSpec)
        _goodService.GetGoodOrNull(yielderSpec.Yield.Id) != null && yielderSpec.ResourceGroup == _spec.ResourceGroup
     * A faction's gatherer, lumberjack or scavenger takes only yields whose good its faction has (logs are common, so
     * every lumberjack still cuts every tree).
     */
    [HarmonyPatch(typeof(YieldRemovingBuilding), nameof(YieldRemovingBuilding.IsAllowed))]
    static class FactionYieldRemoverPatcher
    {
        static void Postfix(YieldRemovingBuilding __instance, YielderSpec yielderSpec, ref bool __result)
        {
            if (!__result || !MixedFactions.IsOn) return;
            FactionCatalog catalog = FactionCatalog.Instance;
            string faction = ColonyFactionService.SimFactionOf(__instance);
            if (faction == null || catalog == null) return;
            __result = catalog.HasGood(faction, yielderSpec.Yield.Id);
        }
    }
}
