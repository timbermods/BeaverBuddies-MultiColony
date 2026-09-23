using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.BuildingsReachability;
using Timberborn.Navigation;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Colonies
{
    /*
     * 9/22/2026 (Timberborn 1.1.2.4), ReachableConstructionSite:
        public bool IsReachableByBuilders()
        {
            if (_constructionSiteAccessible.Accessible.Enabled && !IsReachableFromBuilderHub())
                return IsReachableByExpandedConstructionSite();
            return true;
        }
        private bool IsReachableFromBuilderHub() => _districtService.IsOnInstantDistrictRoadSpill(_constructionSiteAccessible.Accessible);
     */
    // The game says a construction site no builder can reach "is too far from a district and cannot be reached" (a red
    // status in its panel while it is selected, and a warning on its preview), asking whether any district's roads
    // reach it. With separate colonies another colony's may, and its builders never build it (ColonyConstructionJobPatcher),
    // so a site placed beside the other colony's roads sat unbuilt with no word why. Here a site counts as reachable only
    // when one of its own colony's districts reaches it, or its linked other half is reachable (a Trading Post or
    // District Crossing half, built from the half its colony's roads reach). A preview is the placing player's own, so it
    // warns when this player's roads do not reach it. Display only: the status, which only its own colony sees
    // (ColonyView), and the preview's warning colour; builders choose their work as before.
    [HarmonyPatch(typeof(ReachableConstructionSite), nameof(ReachableConstructionSite.IsReachableByBuilders))]
    static class ColonyConstructionSiteReachabilityPatcher
    {
        static void Postfix(ReachableConstructionSite __instance, ref bool __result)
        {
            if (!__result || !ColonySeparation.Active) return;
            Accessible accessible = __instance._constructionSiteAccessible ? __instance._constructionSiteAccessible.Accessible : null;
            // The game counts a site without an access of its own as reachable; so does this.
            if (accessible == null || !accessible.Enabled) return;
            int? slot = ColonyOf(__instance);
            ColonyGameWorld world = SingletonManager.GetSingleton<ColonyRulesService>()?.World;
            if (slot == null || world == null || world.ColonyReaches(slot.Value, accessible)) return;
            // Reached through its linked half, which its own colony's roads reach (the game's own mirror lock keeps the
            // two halves from asking each other forever).
            if (__instance._expandedConstructionSiteReachability?.IsReachable() == true) return;
            __result = false;
        }

        // A placed site: the colony that placed it. A preview: this player's (it is the one placing it).
        private static int? ColonyOf(ReachableConstructionSite site)
        {
            BlockObject blockObject = site.GetComponent<BlockObject>();
            if (blockObject && blockObject.IsPreview)
                return ColonySession.LocalSlot >= 0 && !BeaverBuddies.IO.EventIO.IsNull ? ColonySession.LocalSlot : (int?)null;
            ColonyStamp stamp = site.GetComponent<ColonyStamp>();
            return stamp != null && stamp.IsStamped ? stamp.Slot : (int?)null;
        }
    }
}
