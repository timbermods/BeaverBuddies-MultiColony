using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.Buildings;
using Timberborn.Debugging;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Applies the colony rules to actions. The host's verdict is the one that counts: it judges every action, its
    /// own and its guests', just before replaying it, and an action it refuses is neither played nor sent on. A
    /// player's own computer also judges its actions before sending them, only to explain a refusal at once.
    /// </summary>
    public class ColonyRulesService : RegisteredSingleton, ILoadableSingleton
    {
        private readonly ColonyGameWorld world;
        private readonly QuickNotificationService _quickNotificationService;
        private readonly DevModeManager _devModeManager;

        public ColonyRulesService(EntityRegistry entityRegistry, QuickNotificationService quickNotificationService,
            BuildingService buildingService, IDistrictService districtService, DistrictCenterRegistry districtCenterRegistry,
            DevModeManager devModeManager)
        {
            world = new ColonyGameWorld(entityRegistry, buildingService, districtService, districtCenterRegistry);
            _quickNotificationService = quickNotificationService;
            _devModeManager = devModeManager;
        }

        // Loadable only so the game builds it at load: it is found through SingletonManager, not injected.
        public void Load() { }

        /// <summary>The rules' view of the game, for previews that check the same things.</summary>
        public ColonyGameWorld World => world;

        /// <summary>
        /// Host only, just before an event is replayed. Writes the actor's slot into the event (every computer's
        /// replay then uses it), seats a player saying hello, and judges the action. False means refuse: do not
        /// replay it and do not send it on. A list event may be shortened in place to what the actor may change.
        /// </summary>
        public static bool AllowOnHost(ReplayEvent replayEvent)
        {
            var service = SingletonManager.GetSingleton<ColonyRulesService>();
            if (service == null) return true;
            try
            {
                if (replayEvent is PlayerHelloEvent hello)
                {
                    ColonySlotService.Instance?.HostSeat(hello);
                }
                replayEvent.slot = ColonySession.SlotOfPlayer(replayEvent.player);
            }
            catch (Exception error)
            {
                Plugin.LogError($"[Colony] Could not seat or stamp {replayEvent.type}: {error}");
            }

            // Who is playing, and handing a colony over, are the host's to say.
            if ((replayEvent is ColonyPresenceEvent || replayEvent is ColonyHandoverEvent) && replayEvent.player != ColonySession.HostPlayer)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: only the host sends it");
                return false;
            }

            // Dev mode's shortcuts that every computer plays (a free unlock, Finish now), in every game: only while the
            // host has dev mode on. The host decides whether the game is being tested; a guest can't cheat alone.
            if (IsDevShortcut(replayEvent) && !service._devModeManager.Enabled)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: the host's dev mode is off");
                return false;
            }

            // Zipline links, in every game: the game's own check, made here once instead of in every computer's replay.
            if (replayEvent is ZiplineConnectionChangedEvent zipline && ColonyRoadNetworks.Instance != null
                && !ColonyRoadNetworks.Instance.HostAllowsZipline(zipline, out string why))
            {
                Plugin.Log($"[Colony] Refused a zipline link from player {replayEvent.player}: {why}");
                return false;
            }

            // A player the host has not seated yet has no colony to spend science from, give from or found for.
            if (replayEvent.slot < 0 && ColonyModeService.IsSeparateColonies && (replayEvent is BuildingUnlockedEvent
                || replayEvent is WorkerTypeUnlockedEvent || replayEvent is GiftScienceEvent || replayEvent is ExchangeProposedEvent
                || replayEvent is ExchangeAcceptedEvent || replayEvent is BuildingPlacedEvent || replayEvent is FoundColonyEvent
                || replayEvent is WorkingHoursChangedEvent || replayEvent is PlantingAreaMarkedEvent
                || replayEvent is TreeCuttingAreaEvent || replayEvent is ClearResourcesMarkedEvent))
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: not seated yet");
                return false;
            }

            // Founding is judged in every game: it is how a shared game becomes a separate-colonies one.
            if (!ColonyModeService.IsSeparateColonies && !(replayEvent is FoundColonyEvent)) return true;
            ColonyVerdict verdict;
            try
            {
                verdict = service.Judge(replayEvent, replayEvent.slot, rewrite: true);
            }
            catch (Exception error)
            {
                // Thrown here, it would count as a failed replay and stop the session. One refused action is better.
                Plugin.LogError($"[Colony] Could not judge {replayEvent.type}; refusing it: {error}");
                return false;
            }
            if (!verdict.IsAllowed)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player} (slot {replayEvent.slot}): {verdict.Refusal}, {verdict.Detail}");
                return false;
            }
            if (verdict.Removed > 0)
                Plugin.Log($"[Colony] Kept only slot {replayEvent.slot}'s part of {replayEvent.type} from player {replayEvent.player}: removed {verdict.Removed}");
            return true;
        }

        private static bool IsDevShortcut(ReplayEvent replayEvent) =>
            (replayEvent is BuildingUnlockedEvent building && building.free)
            || (replayEvent is WorkerTypeUnlockedEvent workerType && workerType.free)
            || replayEvent is ConstructionSiteFinishedNowEvent;

        /// <summary>
        /// Before a player's own action is recorded. True means refuse it here and say why. A list event that is only
        /// partly the player's is sent whole; the host keeps the player's part.
        /// </summary>
        public static bool RefuseLocally(ReplayEvent replayEvent)
        {
            if ((!ColonyModeService.IsSeparateColonies && !(replayEvent is FoundColonyEvent)) || EventIO.IsNull) return false;
            var service = SingletonManager.GetSingleton<ColonyRulesService>();
            if (service == null) return false;
            ColonyVerdict verdict;
            try
            {
                verdict = service.Judge(replayEvent, ColonySession.LocalSlot, rewrite: false);
            }
            catch (Exception error)
            {
                // This check is a courtesy; the host judges again. Send the action and let the host decide.
                Plugin.LogError($"[Colony] Could not judge {replayEvent.type} locally: {error}");
                return false;
            }
            if (verdict.IsAllowed) return false;
            // Unlocking and placing with a locked tool sends the unlock first; the host plays it before the placement.
            if (verdict.Refusal == ColonyRefusal.Locked) return false;
            Plugin.Log($"[Colony] Not sending {replayEvent.type}: {verdict.Refusal}, {verdict.Detail}");
            service.Notify(verdict.Refusal);
            return true;
        }

        private ColonyVerdict Judge(ReplayEvent replayEvent, int slot, bool rewrite)
        {
            ColonyScope scope;
            try
            {
                scope = replayEvent.GetColonyScope();
            }
            catch (Exception error)
            {
                // A scope that cannot be read is treated as nobody's: better one refused action than a free one.
                Plugin.LogError($"[Colony] Could not read the scope of {replayEvent.type}: {error}");
                return ColonyVerdict.Refuse(ColonyRefusal.OtherColony, "scope error");
            }
            if (scope == null)
            {
                // RuntimeChecks fails the build for an event without a scope, so this is only reached by an event
                // added without one. Shared is the behaviour of a game without colonies.
                Plugin.LogWarning($"[Colony] {replayEvent.type} declares no colony scope; allowing it");
                return ColonyVerdict.Allow;
            }
            if (scope.Kind == ColonyScopeKind.Founding)
            {
                var founding = SingletonManager.GetSingleton<ColonyFoundingService>();
                if (founding == null) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "no founding service");
                return founding.Judge(slot, ColonyGameWorld.ToPlacement(scope.Placement));
            }
            return ColonyRules.Judge(scope, slot, world, rewrite);
        }

        /// <summary>Shows the refusal in the game's own notification line.</summary>
        public void Notify(ColonyRefusal refusal) => ShowNotice(RefusalMessage(refusal));

        public void ShowNotice(string text) => ShowNotice(text, warning: true);

        public void ShowNotice(string text, bool warning)
        {
            try
            {
                if (warning) _quickNotificationService.SendWarningNotification(text);
                else _quickNotificationService.SendNotification(text);
            }
            catch (Exception error)
            {
                // A notice is a courtesy; losing it must never break the action that caused it.
                Plugin.LogWarning("[Colony] Could not show a notice: " + error.Message);
            }
        }

        /// <summary>The message for a refusal, in the player's language.</summary>
        public static string RefusalMessage(ColonyRefusal refusal) => RegisteredLocalizationService.T(refusal switch
        {
            ColonyRefusal.NothingOwn => "BeaverBuddies.Colony.Refused.NothingOwn",
            ColonyRefusal.Locked => "BeaverBuddies.Colony.Refused.Locked",
            ColonyRefusal.CannotFound => "BeaverBuddies.Colony.Founding.NotYours",
            ColonyRefusal.Blocked => "BeaverBuddies.Colony.Refused.Blocked",
            ColonyRefusal.FoundingConflict => "BeaverBuddies.Colony.Refused.FoundingConflict",
            ColonyRefusal.NotEnoughScience => "BeaverBuddies.Colony.Refused.NotEnoughScience",
            ColonyRefusal.OtherColonyArea => "BeaverBuddies.Colony.Refused.OtherColonyArea",
            ColonyRefusal.TouchesOtherColony => "BeaverBuddies.Colony.Refused.TouchesOtherColony",
            ColonyRefusal.TooCloseToColony => "BeaverBuddies.Colony.Refused.TooCloseToColony",
            _ => "BeaverBuddies.Colony.Refused.OtherColony",
        });
    }
}
