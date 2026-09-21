using BeaverBuddies.IO;
using BeaverBuddies.Util;
using HarmonyLib;
using System;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// The speed panel's "pause or tick once" key (period by default, not dev mode only) calls Ticker.TickOnce when
    /// the game is paused. That runs a whole tick through TickableBucketService.TickOnce, which never goes through
    /// TickBuckets, so the ReplayService does not tick: the tick counter and the shared actions are skipped, and the
    /// tick happens on that computer only, desyncing the game. In a co-op game it is refused with a notice; in single
    /// player the game's own method runs.
    /// </summary>
    public class TickOnceCoopNotice : RegisteredSingleton, ILoadableSingleton
    {
        private readonly QuickNotificationService _quickNotificationService;

        public TickOnceCoopNotice(QuickNotificationService quickNotificationService)
        {
            _quickNotificationService = quickNotificationService;
        }

        // Loadable only so the game creates it with the map, and the patch below can find it.
        public void Load() { }

        public void Show()
        {
            try
            {
                _quickNotificationService.SendWarningNotification(RegisteredLocalizationService.T("BeaverBuddies.TickOnce.CoopBlocked"));
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not show the tick once notice: " + error.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Ticker), nameof(Ticker.TickOnce))]
    static class TickerTickOncePatcher
    {
        static bool Prefix()
        {
            if (EventIO.IsNull) return true;
            Plugin.Log("Refused tick once in a co-op game: it would tick this computer only");
            SingletonManager.GetSingleton<TickOnceCoopNotice>()?.Show();
            return false;
        }
    }
}
