using BeaverBuddies.IO;
using HarmonyLib;
using Timberborn.Navigation;

namespace BeaverBuddies.Fixes
{
    // The game keeps an "instant" copy of the road and terrain graphs, which construction sites and buildings are given
    // their district from (DistrictConstructionAssigner, InstantDistrict), and haulers and builders then read those
    // districts to choose where to carry goods. The game brings that copy up to date at the end of every frame. A tick is
    // spread over several frames, a different number on each computer (it depends on the frame rate), so a road
    // finishing in the middle of a tick joined a construction site to its district after a different share of the tick
    // on each computer, and a beaver ticking in between could choose differently: a desync (seen with 1.4.0-alpha3, a
    // District Crossing joined to its district on the host only). In a multiplayer game the copy is brought up to date
    // at the start of each tick instead, with the regular graphs, at the same moment on every computer.

    [ManualMethodOverwrite]
    /*
        9/21/2026 (Timberborn 1.1.2.4)
		ProcessPreviewChanges();
		ProcessInstantChanges();
		NotifyAllNavmeshChanges();
     */
    [HarmonyPatch(typeof(NavigationSynchronizer), nameof(NavigationSynchronizer.LateUpdateSingleton))]
    static class NavigationSynchronizerLateUpdatePatcher
    {
        static bool Prefix(NavigationSynchronizer __instance)
        {
            if (EventIO.IsNull) return true;
            // The previews (what the local player is placing) stay per frame: they are this computer's own.
            __instance.ProcessPreviewChanges();
            __instance.NotifyAllNavmeshChanges();
            return false;
        }
    }

    // A singleton ticks at the start of each tick, after the tick's events were played and before any beaver. The
    // instant changes are applied and their listeners told first (as at the end of a frame, where the game did it), then
    // the game's own Tick applies the regular ones. The regular and preview updates are empty at this point, so only
    // the instant listeners hear anything here.
    [HarmonyPatch(typeof(NavigationSynchronizer), nameof(NavigationSynchronizer.Tick))]
    static class NavigationSynchronizerTickPatcher
    {
        static void Prefix(NavigationSynchronizer __instance)
        {
            if (EventIO.IsNull) return;
            __instance.ProcessInstantChanges();
            __instance.NotifyAllNavmeshChanges();
        }
    }
}
