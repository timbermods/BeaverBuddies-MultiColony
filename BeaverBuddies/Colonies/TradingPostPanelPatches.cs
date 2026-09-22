using HarmonyLib;
using System;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.DistributionSystemUI;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The Trading Post is a District Crossing underneath (its model and workings), so the game gives its panel the
    /// crossing's district-distribution panels too: the imported goods beside it with "Manage distribution", and the
    /// half's stock list. Import settings move nothing across a Trading Post, and it is no store (its own panel says what
    /// waits on the half), so both are hidden there. The game's District Crossing keeps them. Display only.
    /// </summary>
    static class TradingPostPanel
    {
        /// <summary>A Trading Post half (never throws: a panel must not break the game).</summary>
        public static bool IsTradingPost(BaseComponent entity)
        {
            try
            {
                return TradingPosts.IsTradingPostBuilding(entity);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not tell whether a building is a Trading Post: " + error.Message);
                return false;
            }
        }
    }

    /*
     * 9/21/2026 (Timberborn 1.1.2.4): the crossing's side panel ("Imported goods", "Manage distribution") shows itself
     * on every update while its district has distribution settings:
        if ((bool)_districtCrossing && (bool)_districtCrossing.DistrictDistributableGoodProvider)
        {
            _root.ToggleDisplayStyle(visible: true);
            ... every import icon updated
     */
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
     * 9/21/2026 (Timberborn 1.1.2.4): the crossing's stock ("No goods in stock", or each good with its limit) shows
     * itself when shown, and again on every update (InventoryFragment.UpdateFragment: _root.ToggleDisplayStyle(true)
     * while the inventory is enabled), so hiding it once is not enough:
        _districtCrossingInventory = entity.GetComponent<DistrictCrossingInventory>();
        if ((bool)_districtCrossingInventory)
        {
            _root.ToggleDisplayStyle(visible: true);
            _inventoryFragment.ShowFragment(_districtCrossingInventory.Inventory);
        }
     */
    // A Trading Post is not a store: the fragment is never shown for one (it stays hidden, and with no inventory set its
    // updates do nothing). Its own panel says what waits on the half, and why.
    [HarmonyPatch(typeof(DistrictCrossingInventoryFragment), nameof(DistrictCrossingInventoryFragment.ShowFragment))]
    static class TradingPostStockPanelPatcher
    {
        static bool Prefix(DistrictCrossingInventoryFragment __instance, BaseComponent entity)
        {
            if (!TradingPostPanel.IsTradingPost(entity)) return true;
            __instance._districtCrossingInventory = null;
            __instance._root.ToggleDisplayStyle(visible: false);
            return false;
        }
    }
}
