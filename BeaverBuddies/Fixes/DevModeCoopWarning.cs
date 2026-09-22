using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.Debugging;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Fixes
{
    /// <summary>
    /// Dev mode's tools change the game on the computer they are used on. In a co-op game its instant unlock
    /// (Ctrl-click on a locked building or bot toggle), a construction site's "Finish now" and the dev panel's "Add
    /// 1000 Science" are played on every computer; the rest (deleting any object, the dev panel's other buttons...) are
    /// not, and desync the game. This says so when dev mode is on in a co-op game.
    /// </summary>
    public class DevModeCoopWarning : ILoadableSingleton, IPostLoadableSingleton
    {
        private readonly EventBus _eventBus;
        private readonly DevModeManager _devModeManager;
        private readonly QuickNotificationService _quickNotificationService;

        public DevModeCoopWarning(EventBus eventBus, DevModeManager devModeManager, QuickNotificationService quickNotificationService)
        {
            _eventBus = eventBus;
            _devModeManager = devModeManager;
            _quickNotificationService = quickNotificationService;
        }

        public void Load()
        {
            _eventBus.Register(this);
        }

        public void PostLoad()
        {
            if (_devModeManager.Enabled) Warn();
        }

        [OnEvent]
        public void OnDevModeToggled(DevModeToggledEvent devModeToggledEvent)
        {
            if (devModeToggledEvent.Enabled) Warn();
        }

        private void Warn()
        {
            if (EventIO.IsNull) return;
            Plugin.LogWarning("Dev mode is on in a co-op game: its instant unlock, Finish now and Add 1000 Science are shared, its other tools desync the game");
            try
            {
                _quickNotificationService.SendWarningNotification(RegisteredLocalizationService.T("BeaverBuddies.DevMode.CoopWarning"));
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not show the dev mode notice: " + error.Message);
            }
        }
    }
}
