using BeaverBuddies.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystemUI;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.DistributionSystemUI;
using Timberborn.EntityNaming;
using Timberborn.EntityPanelSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A District Crossing between two colonies is only a trading post, on screen as in play: the game's panels for
    /// its district-to-district distribution (the imported goods beside the panel with "Manage distribution", and the
    /// crossing's own stock) are hidden there, and it is named and described as a trading post. The trading post's
    /// panel shows what it needs of them. A crossing between one colony's own districts keeps all of the game's.
    /// Display only.
    /// </summary>
    static class TradingPostPanel
    {
        /// <summary>The entity is a trading post (never throws: a panel must not break the game).</summary>
        public static bool IsTradingPost(BaseComponent entity)
        {
            if (!ColonyModeService.IsSeparateColonies || !entity) return false;
            try
            {
                DistrictCrossing crossing = entity.GetComponent<DistrictCrossing>();
                return crossing && TradingPosts.IsTradingPost(crossing);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not tell whether a crossing is a trading post: " + error.Message);
                return false;
            }
        }

        public static string T(string key) => RegisteredLocalizationService.T(key);
    }

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the crossing's side panel ("Imported goods", "Manage distribution") shows itself
     * on every update while its district has distribution settings:
        if ((bool)_districtCrossing && (bool)_districtCrossing.DistrictDistributableGoodProvider)
        {
            _root.ToggleDisplayStyle(visible: true);
            ... every import icon updated
     */
    // Import settings move nothing across a trading post (ColonyTrading.cs), so their panel is not shown there.
    [HarmonyPatch(typeof(DistrictCrossingFragment), nameof(DistrictCrossingFragment.UpdateRootAndIcons))]
    static class TradingPostImportPanelPatcher
    {
        static bool Prefix(DistrictCrossingFragment __instance)
        {
            if (!TradingPostPanel.IsTradingPost(__instance._districtCrossing)) return true;
            __instance._root.ToggleDisplayStyle(visible: false);
            return false;
        }
    }

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the crossing's stock ("No goods in stock", or each good with its limit):
        _districtCrossingInventory = entity.GetComponent<DistrictCrossingInventory>();
        if ((bool)_districtCrossingInventory)
        {
            _root.ToggleDisplayStyle(visible: true);
     */
    // At a trading post the trading post's panel lists what waits on the half (only when something does).
    [HarmonyPatch(typeof(DistrictCrossingInventoryFragment), nameof(DistrictCrossingInventoryFragment.ShowFragment))]
    static class TradingPostStockPanelPatcher
    {
        static void Postfix(DistrictCrossingInventoryFragment __instance, BaseComponent entity)
        {
            if (TradingPostPanel.IsTradingPost(entity)) __instance._root.ToggleDisplayStyle(visible: false);
        }
    }

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the panel's title is written on every update, from the one read of the name:
        NamedEntity component = _shownEntity.GetComponent<NamedEntity>();
        string entityName = component.EntityName;
        ...
        _entityNameText.text = entityName;
     */
    // That read asks here instead, so a trading post's panel is titled "Trading Post" (written once, not written and
    // overwritten every frame). The building keeps its name everywhere else.
    [HarmonyPatch(typeof(EntityPanel), nameof(EntityPanel.UpdateEntityBadge))]
    static class TradingPostTitlePatcher
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo entityName = AccessTools.PropertyGetter(typeof(NamedEntity), nameof(NamedEntity.EntityName));
            MethodInfo title = AccessTools.Method(typeof(TradingPostTitlePatcher), nameof(Title));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(entityName))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = title;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) Plugin.LogWarning($"[Colony] The panel title's name was read {replaced} times; trading posts keep the crossing's title");
        }

        public static string Title(NamedEntity namedEntity) =>
            TradingPostPanel.IsTradingPost(namedEntity) ? TradingPostPanel.T("BeaverBuddies.Colony.Trade.BuildingName") : namedEntity.EntityName;
    }

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): a building's description is its spec's text, first (order -1):
        string descriptionLocKey = _labeledEntitySpec.DescriptionLocKey;
        if (!string.IsNullOrEmpty(descriptionLocKey))
            yield return EntityDescription.CreateTextSection(_loc.T(descriptionLocKey), -1);
     */
    // The crossing's ("Balances goods between the two connected districts...") becomes the trading post's. The rest
    // (carrying capacity, the flavour line) still holds.
    [HarmonyPatch(typeof(PlaceableBlockObjectDescriber), nameof(PlaceableBlockObjectDescriber.DescribeEntity))]
    static class TradingPostDescriptionPatcher
    {
        private const int BuildingTextOrder = -1;

        static void Postfix(PlaceableBlockObjectDescriber __instance, ref IEnumerable<EntityDescription> __result)
        {
            if (TradingPostPanel.IsTradingPost(__instance)) __result = AsTradingPost(__result);
        }

        static IEnumerable<EntityDescription> AsTradingPost(IEnumerable<EntityDescription> descriptions)
        {
            foreach (EntityDescription description in descriptions)
            {
                yield return description.TextSection && description.Order == BuildingTextOrder
                    ? EntityDescription.CreateTextSection(TradingPostPanel.T("BeaverBuddies.Colony.Trade.BuildingDescription"), BuildingTextOrder)
                    : description;
            }
        }
    }
}
