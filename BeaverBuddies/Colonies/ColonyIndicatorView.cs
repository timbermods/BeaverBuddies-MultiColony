using HarmonyLib;
using System;
using Timberborn.AutomationBuildings;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// An Indicator set to warn shows its warning (a notice at the top of the screen, as its input comes on) to its own
    /// colony's player only in a separate-colonies game, as its journal entry already is (ColonyViewNotificationPatcher)
    /// and its status icons (ColonyView). Until 1.4.0-rc1 every player saw every colony's indicator warnings (O4). The
    /// warning is display only: nothing is saved and nothing in the simulation reads it, so leaving it out on the other
    /// computers changes nothing they simulate. A shared-colony game, and a player not seated yet, see them all.
    /// </summary>
    [ManualMethodOverwrite]
    /*
     * 2026-09-23 (Timberborn 1.1.2.4, Indicator.ShowWarning)
        _quickNotificationService.SendWarningNotification(IndicatorName);
     */
    [HarmonyPatch(typeof(Indicator), nameof(Indicator.ShowWarning))]
    static class ColonyIndicatorWarningPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(Indicator __instance)
        {
            if (!ColonyViewService.Active) return true;
            // This runs in the tick (the indicator's rising edge), on the computers that filter: it must never throw.
            try
            {
                return ColonyViewService.IsOwn(__instance);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not decide whose an indicator is, so its warning is shown: " + error.Message);
                return true;
            }
        }
    }
}
