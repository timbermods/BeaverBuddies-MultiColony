using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
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
        private readonly ColonyGameWorld hostWorld;
        private readonly ColonyGameWorld localWorld;
        private readonly QuickNotificationService _quickNotificationService;

        public ColonyRulesService(EntityRegistry entityRegistry, BuildingService buildingService, BlockService blockService,
            QuickNotificationService quickNotificationService)
        {
            hostWorld = new ColonyGameWorld(entityRegistry, buildingService, blockService, checkCrossings: true);
            localWorld = new ColonyGameWorld(entityRegistry, buildingService, blockService, checkCrossings: false);
            _quickNotificationService = quickNotificationService;
        }

        // Loadable only so the game builds it at load: it is found through SingletonManager, not injected.
        public void Load() { }

        /// <summary>
        /// Host only, before a set of actions is judged one by one. A District Crossing arrives as two placements,
        /// and the half across the border is only allowed when the placer's own half stands behind it. The own half
        /// may come second, so every crossing half that stands wholly on its placer's own land is noted first.
        /// </summary>
        public static void BeginHostBatch(System.Collections.Generic.IReadOnlyList<ReplayEvent> replayEvents)
        {
            var service = SingletonManager.GetSingleton<ColonyRulesService>();
            if (service == null) return;
            service.hostWorld.ForgetCrossings();
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null) return;
            foreach (ReplayEvent replayEvent in replayEvents)
            {
                try
                {
                    ColonyScope scope = replayEvent.GetColonyScope();
                    if (scope?.Kind != ColonyScopeKind.Placement || scope.Placement == null) continue;
                    if (!service.hostWorld.IsCrossing(scope.Placement.TemplateName)) continue;
                    var footprint = service.hostWorld.Footprint(scope.Placement);
                    if (footprint == null || footprint.Count == 0) continue;
                    int colony = ColonySession.ColonyOfPlayer(replayEvent.player);
                    bool own = true;
                    foreach (ColonyTile tile in footprint) own &= territory.OwnerOf(tile) == colony;
                    if (own) service.hostWorld.RememberCrossing(scope.Placement);
                }
                catch (Exception error)
                {
                    // Without the note the half across the border is refused, which is the safe side.
                    Plugin.LogError($"[Colony] Could not note a crossing half of {replayEvent.type}: {error}");
                }
            }
        }

        /// <summary>
        /// Host only, just before an event is replayed. False means refuse: do not replay it and do not send it on.
        /// A list event may be shortened in place to the actor's own tiles or entities.
        /// </summary>
        public static bool AllowOnHost(ReplayEvent replayEvent)
        {
            // Founding is judged in every game: it is how a shared game becomes a separate-colonies one.
            if (!ColonyModeService.IsSeparateColonies && !(replayEvent is FoundColonyEvent)) return true;
            var service = SingletonManager.GetSingleton<ColonyRulesService>();
            if (service == null) return true;
            int colony = ColonySession.ColonyOfPlayer(replayEvent.player);
            ColonyVerdict verdict;
            try
            {
                verdict = service.Judge(replayEvent, colony, service.hostWorld, rewrite: true);
            }
            catch (Exception error)
            {
                // Thrown here, it would count as a failed replay and stop the session. One refused action is better.
                Plugin.LogError($"[Colony] Could not judge {replayEvent.type}; refusing it: {error}");
                return false;
            }
            if (!verdict.IsAllowed)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player} (colony {colony}): {verdict.Refusal}, {verdict.Detail}");
                return false;
            }
            if (verdict.Removed > 0)
                Plugin.Log($"[Colony] Kept only colony {colony}'s part of {replayEvent.type} from player {replayEvent.player}: removed {verdict.Removed}");
            return true;
        }

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
                verdict = service.Judge(replayEvent, ColonySession.LocalColony, service.localWorld, rewrite: false);
            }
            catch (Exception error)
            {
                // This check is a courtesy; the host judges again. Send the action and let the host decide.
                Plugin.LogError($"[Colony] Could not judge {replayEvent.type} locally: {error}");
                return false;
            }
            if (verdict.IsAllowed) return false;
            Plugin.Log($"[Colony] Not sending {replayEvent.type}: {verdict.Refusal}, {verdict.Detail}");
            service.Notify(verdict.Refusal);
            return true;
        }

        private ColonyVerdict Judge(ReplayEvent replayEvent, int colony, ColonyGameWorld world, bool rewrite)
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
                return ColonyVerdict.Refuse(ColonyRefusal.UnknownFootprint, "scope error");
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
                return founding.Judge(colony, ColonyGameWorld.ToPlacement(scope.Placement));
            }
            if (ColonyModeService.FoundingPending) return ColonyRules.JudgeWhileFounding(scope, colony);
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null) return ColonyVerdict.Allow;
            return ColonyRules.Judge(scope, colony, territory, world, rewrite);
        }

        /// <summary>Shows the refusal in the game's own notification line.</summary>
        public void Notify(ColonyRefusal refusal)
        {
            string key = refusal switch
            {
                ColonyRefusal.OutsideLand => "BeaverBuddies.Colony.Refused.OutsideLand",
                ColonyRefusal.BorderStrip => "BeaverBuddies.Colony.Refused.BorderStrip",
                ColonyRefusal.NothingOwn => "BeaverBuddies.Colony.Refused.NothingOwn",
                ColonyRefusal.NotFounded => "BeaverBuddies.Colony.Refused.NotFounded",
                ColonyRefusal.FoundingTooClose => "BeaverBuddies.Colony.Refused.FoundingTooClose",
                ColonyRefusal.CannotFound => "BeaverBuddies.Colony.Founding.NotYours",
                ColonyRefusal.Blocked => "BeaverBuddies.Colony.Refused.Blocked",
                _ => "BeaverBuddies.Colony.Refused.OtherColony",
            };
            ShowNotice(RegisteredLocalizationService.T(key));
        }

        public void ShowNotice(string text)
        {
            try
            {
                _quickNotificationService.SendWarningNotification(text);
            }
            catch (Exception error)
            {
                // A notice is a courtesy; losing it must never break the action that caused it.
                Plugin.LogWarning("[Colony] Could not show a notice: " + error.Message);
            }
        }

        /// <summary>The message a placement preview shows, in the player's language.</summary>
        public static string RefusalMessage(ColonyRefusal refusal) => refusal switch
        {
            ColonyRefusal.BorderStrip => RegisteredLocalizationService.T("BeaverBuddies.Colony.Refused.BorderStrip"),
            ColonyRefusal.NotFounded => RegisteredLocalizationService.T("BeaverBuddies.Colony.Refused.NotFounded"),
            ColonyRefusal.FoundingTooClose => RegisteredLocalizationService.T("BeaverBuddies.Colony.Refused.FoundingTooClose"),
            ColonyRefusal.CannotFound => RegisteredLocalizationService.T("BeaverBuddies.Colony.Founding.NotYours"),
            ColonyRefusal.Blocked => RegisteredLocalizationService.T("BeaverBuddies.Colony.Refused.Blocked"),
            _ => RegisteredLocalizationService.T("BeaverBuddies.Colony.Refused.OutsideLand"),
        };
    }
}
