using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System.Collections.Generic;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;
using Timberborn.WaterSourceSystem;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Surviving a game update (1.4.0-rc1, R8). Two of the mod's co-op fixes are applied by name to what the game had in
    /// Timberborn 1.1.2.4, and a later version can take them away without the mod noticing at build time: the water seep
    /// timing fix (WaterSourceTimingFix, when the game's method changes) and the shared settings of the automation and
    /// water buildings (AutomationEvent's list, when a setter is renamed). Neither stops the mod's start any more; each
    /// says what it is missing. A co-op game that needs a missing one would go out of step on its own (water seeps on
    /// each computer's frame time, or a setting changed on one computer only), which is the worst way for it to go: so
    /// such a game is stopped at load, on every computer, with a message saying why. Only then: without water seeps the
    /// seep fix is not needed, and single player is never affected.
    /// </summary>
    public class CoopFixGuard : IUpdatableSingleton
    {
        private readonly EntityRegistry _entityRegistry;
        private bool _checked;

        public CoopFixGuard(EntityRegistry entityRegistry)
        {
            _entityRegistry = entityRegistry;
        }

        // The first frame of the loaded game, once every singleton (the ReplayService's session among them) is loaded.
        public void UpdateSingleton()
        {
            if (_checked) return;
            _checked = true;
            string waterFix = WaterSourceTimingFix.Unavailable;
            string missing = Missing(waterFix, waterFix != null && HasWaterSeeps(), AutomationEvent.MissingRecorders);
            if (missing == null) return;
            Plugin.LogError("[Fixes] This co-op game needs a fix this game version does not allow: " + missing);
            if (EventIO.IsNull) return;
            string message;
            try { message = RegisteredLocalizationService.T("BeaverBuddies.CoopFix.Stopped", missing); }
            catch { message = "Co-op has stopped: MultiColony can't keep this game in step on this version of Timberborn (" + missing + "). Update MultiColony."; }
            ReplayService replayService = SingletonManager.GetSingleton<ReplayService>();
            if (replayService != null) replayService.EndSession(message);
            else EventIO.Reset();
        }

        private bool HasWaterSeeps()
        {
            foreach (EntityComponent entity in _entityRegistry.Entities)
            {
                if (entity.GetComponent<WaterDepthStrengthModifier>() != null) return true;
            }
            return false;
        }

        /// <summary>
        /// What a co-op game is missing, for the message, or null when nothing it needs is missing:
        /// <paramref name="waterFixUnavailable"/> matters only when the game has water seeps.
        /// </summary>
        internal static string Missing(string waterFixUnavailable, bool hasWaterSeeps, IReadOnlyCollection<string> missingRecorders)
        {
            var parts = new List<string>();
            if (waterFixUnavailable != null && hasWaterSeeps) parts.Add("water seep timing: " + waterFixUnavailable);
            if (missingRecorders != null && missingRecorders.Count > 0) parts.Add("settings no longer shared: " + string.Join(", ", missingRecorders));
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }
    }
}
