using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using Timberborn.BlockSystem;
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
            DevModeManager devModeManager, IBlockService blockService)
        {
            world = new ColonyGameWorld(entityRegistry, buildingService, districtService, districtCenterRegistry, blockService);
            _quickNotificationService = quickNotificationService;
            _devModeManager = devModeManager;
        }

        // Loadable only so the game builds it at load: it is found through SingletonManager, not injected.
        public void Load() { }

        /// <summary>The rules' view of the game, for previews that check the same things.</summary>
        public ColonyGameWorld World => world;

        /// <summary>
        /// Host only, just before an event is replayed. Writes the actor's slot into the event (every computer's
        /// replay then uses it), seats a player saying hello (or refuses a hello its connection contradicts, see
        /// ColonySlotTable.CheckHello), and judges the action. False means refuse: do not replay it and do not send
        /// it on. A list event may be shortened in place to what the actor may change.
        /// <paramref name="refusal"/> says why, for the player told (see ActionRefusedEvent).
        /// </summary>
        public static bool AllowOnHost(ReplayEvent replayEvent, out ColonyRefusal refusal)
        {
            refusal = ColonyRefusal.None;
            var service = SingletonManager.GetSingleton<ColonyRulesService>();
            if (service == null) return true;
            try
            {
                if (replayEvent is PlayerHelloEvent hello)
                {
                    // Who the guest's connection proved to be: a Steam connection's Steam ID; nothing over direct TCP.
                    string verifiedId = (EventIO.Get() as ServerEventIO)?.NetBase?.VerifiedIdOf(hello.player);
                    ColonySlotService slots = ColonySlotService.Instance;
                    if (slots != null && !slots.HostSeat(hello, verifiedId, out string helloWhy))
                    {
                        Plugin.LogWarning($"[Colony] Refused {replayEvent.type} from player {replayEvent.player} ({ColonySlotTable.ForLog(hello.playerName)}): {helloWhy}");
                        refusal = ColonyRefusal.HostRefused;
                        return false;
                    }
                }
                replayEvent.slot = ColonySession.SlotOfPlayer(replayEvent.player);
            }
            catch (Exception error)
            {
                // Not the sender's own value, which a guest could have written itself.
                replayEvent.slot = -1;
                Plugin.LogError($"[Colony] Could not seat or stamp {replayEvent.type}: {error}");
            }

            // The other half of a Trading Post placed together was refused: this one goes with it (see JudgePairs).
            if (service.refusedPartners.TryGetValue(replayEvent, out ColonyRefusal partnerRefusal))
            {
                service.refusedPartners.Remove(replayEvent);
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: the other half of the Trading Post was refused");
                refusal = partnerRefusal;
                return false;
            }

            // Who is playing, handing a colony over, and telling a player an action was refused, are the host's to say.
            if ((replayEvent is ColonyPresenceEvent || replayEvent is ColonyHandoverEvent || replayEvent is ActionRefusedEvent)
                && replayEvent.player != ColonySession.HostPlayer)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: only the host sends it");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }

            int hostTicks = SingletonManager.GetSingleton<ReplayService>()?.TicksSinceLoad ?? 1;
            // Founding, handing over and switching colonies wait for the first tick, unless the game began from a waiting
            // room with joining already closed (ColonyRules.WaitsForStart).
            if (ColonyRules.WaitsForStart(replayEvent is FoundColonyEvent || replayEvent is ColonyHandoverEvent || replayEvent is ActAsColonyEvent
                || replayEvent is BeaverBuddies.Factions.ColonyFactionSwitchEvent,
                hostTicks, ColonySession.JoiningClosedAtStart))
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: the game has not started, players can still join");
                refusal = ColonyRefusal.NotStartedYet;
                return false;
            }

            // While the host waits at the start for players to join, a guest's change would close joining unseen (the
            // host's own is held for the host's word: HostStartGate). Refused with the same notice as a founding.
            if (replayEvent.player != ColonySession.HostPlayer && hostTicks == 0 && replayEvent.ChangesGame()
                && (EventIO.Get() as ServerEventIO)?.IsAcceptingClients == true)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: the host is still waiting for players");
                refusal = ColonyRefusal.NotStartedYet;
                return false;
            }

            // Looking after a colony: who may ask whom, and who may switch into which colony (ColonyStewardRules).
            if (replayEvent is StewardGrantedEvent || replayEvent is StewardRevokedEvent || replayEvent is ActAsColonyEvent)
            {
                ColonyStewards stewards = ColonyStewards.Instance;
                string stewardWhy = "no steward service";
                if (stewards == null || !stewards.HostAllows(replayEvent, out stewardWhy))
                {
                    Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: {stewardWhy}");
                    refusal = ColonyRefusal.HostRefused;
                    return false;
                }
            }

            // Dev mode's shortcuts that every computer plays (a free unlock, Finish now, Add 1000 Science), in every game:
            // only while the host has dev mode on. The host decides whether the game is being tested; a guest can't cheat alone.
            if (IsDevShortcut(replayEvent) && !service._devModeManager.Enabled)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: the host's dev mode is off");
                refusal = ColonyRefusal.DevModeOff;
                return false;
            }

            // A building the host's game does not have, in every game: from a mod a guest runs and the host does not. Played,
            // it threw on the host and stopped everyone; refused, no computer plays it and the guest is told.
            string building = replayEvent is BuildingPlacedEvent placing ? placing.prefabName
                : replayEvent is BuildingUnlockedEvent unlocking ? unlocking.buildingName : null;
            if (building != null && !service.world.HasBuilding(building))
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: this game has no building {building}");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }
            // The same for a workshop's recipe (a mod can add recipes to the game's own buildings), 1.4.0-rc1 (H1).
            Timberborn.Workshops.RecipeSpecService recipes = replayEvent is ManufactoryRecipeSelectedEvent
                ? SingletonManager.GetSingleton<ReplayService>()?.GetSingleton<Timberborn.Workshops.RecipeSpecService>() : null;
            if (recipes != null && replayEvent is ManufactoryRecipeSelectedEvent recipeChoice && recipeChoice.itemID != null
                && !ManufactoryRecipeSelectedEvent.TryGetRecipe(recipes, recipeChoice.itemID, out _))
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: this game has no recipe {recipeChoice.itemID}");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }

            // A plant the host's game does not have (a crop from a guest's mod), in every game: refused like a building, for
            // the same reason (the game's planting check throws on it). E-1 of the 1.4.0-rc1 review.
            if (replayEvent is PlantingAreaMarkedEvent planting && planting.NamesUnknownPlant(SingletonManager.GetSingleton<ReplayService>()))
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: this game has no plant {planting.prefabName}");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }

            // Zipline links, in every game: the game's own check, made here once instead of in every computer's replay.
            if (replayEvent is ZiplineConnectionChangedEvent zipline && ColonyRoadNetworks.Instance != null
                && !ColonyRoadNetworks.Instance.HostAllowsZipline(zipline, out string why))
            {
                Plugin.Log($"[Colony] Refused a zipline link from player {replayEvent.player}: {why}");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }

            // A player the host has not seated yet has no colony to spend science from, give from or found for.
            if (replayEvent.slot < 0 && ColonyModeService.IsSeparateColonies && (replayEvent is BuildingUnlockedEvent
                || replayEvent is WorkerTypeUnlockedEvent || replayEvent is ExchangeCancelledEvent || replayEvent is ExchangeKeptEvent || replayEvent is ExchangeProposedEvent
                || replayEvent is ExchangeAcceptedEvent || replayEvent is BuildingPlacedEvent || replayEvent is FoundColonyEvent
                || replayEvent is WorkingHoursChangedEvent || replayEvent is PlantingAreaMarkedEvent
                || replayEvent is TreeCuttingAreaEvent || replayEvent is ClearResourcesMarkedEvent
                || replayEvent is StewardGrantedEvent || replayEvent is StewardRevokedEvent || replayEvent is ActAsColonyEvent
                || replayEvent is WishlistChangedEvent || replayEvent is ExchangeFloorSetEvent || replayEvent is ScienceAddedEvent
                || replayEvent is BeaverBuddies.Factions.ColonyFactionSwitchEvent))
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player}: not seated yet");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }

            // Mixed factions (D1, D14, D15, D17): a founding's faction, a colony's switch, and each colony builds its own
            // faction's buildings (and common ones). The host decides, and writes what it decided into the event.
            if (!JudgeFactions(replayEvent, out refusal)) return false;

            // Founding is judged in every game: it is how a shared game becomes a separate-colonies one.
            if (!ColonyModeService.IsSeparateColonies && !(replayEvent is FoundColonyEvent)) return true;
            // A building placed as a copy of another colony's takes that building's settings, and with them its
            // automation links: it would be wired to the other colony's sensor. Placed plain instead.
            if (replayEvent is BuildingPlacedEvent copy && !string.IsNullOrEmpty(copy.duplicationSourceID)
                && !ColonyRules.MayChange(replayEvent.slot, service.world.OwnerOf(copy.duplicationSourceID)))
            {
                Plugin.Log($"[Colony] Placing {copy.prefabName} for slot {replayEvent.slot} without the settings of {copy.duplicationSourceID}: another colony's building");
                copy.duplicationSourceID = null;
            }
            ColonyVerdict verdict;
            try
            {
                verdict = service.Judge(replayEvent, replayEvent.slot, rewrite: true);
            }
            catch (Exception error)
            {
                // Thrown here, it would count as a failed replay and stop the session. One refused action is better.
                Plugin.LogError($"[Colony] Could not judge {replayEvent.type}; refusing it: {error}");
                refusal = ColonyRefusal.HostRefused;
                return false;
            }
            if (!verdict.IsAllowed)
            {
                Plugin.Log($"[Colony] Refused {replayEvent.type} from player {replayEvent.player} (slot {replayEvent.slot}): {verdict.Refusal}, {verdict.Detail}");
                refusal = verdict.Refusal;
                return false;
            }
            if (verdict.Removed > 0)
                Plugin.Log($"[Colony] Kept only slot {replayEvent.slot}'s part of {replayEvent.type} from player {replayEvent.player}: removed {verdict.Removed}");
            return true;
        }

        /// <summary>
        /// Host only: the mixed-factions part of judging an action. A founding's faction must be one the game has and the
        /// host has unlocked (the event is then given its faction, the base faction when it names none, and none at all
        /// outside a mixed game). A switch must be the colony's own seated player's, to an available faction, while the
        /// colony is untouched. A building of one faction alone may only be placed by a colony of that faction; Trading
        /// Posts of any faction may (the host gives each half its faction, JudgePairs).
        /// </summary>
        private static bool JudgeFactions(ReplayEvent replayEvent, out ColonyRefusal refusal)
        {
            refusal = ColonyRefusal.None;
            if (replayEvent is FoundColonyEvent found)
            {
                if (!BeaverBuddies.Factions.MixedFactions.IsOn)
                {
                    found.faction = null;
                    return true;
                }
                var verdict = BeaverBuddies.Factions.FactionRules.JudgeFoundingFaction(true, found.faction,
                    BeaverBuddies.Factions.ColonyFactionService.FactionIds.ToList(),
                    BeaverBuddies.Factions.FactionChoice.Available().Select(f => f.Id).ToList());
                if (verdict != BeaverBuddies.Factions.FactionChoiceVerdict.Allowed)
                {
                    Plugin.Log($"[Factions] Refused a founding as {found.faction} from player {replayEvent.player}: {verdict}");
                    refusal = ColonyRefusal.FactionUnavailable;
                    return false;
                }
                found.faction = BeaverBuddies.Factions.FactionRules.FoundingFaction(true, found.faction,
                    BeaverBuddies.Factions.MixedFactions.BaseFaction);
                return true;
            }
            if (replayEvent is BeaverBuddies.Factions.ColonyFactionSwitchEvent switching)
            {
                var verdict = BeaverBuddies.Factions.FactionChoice.HostJudgeSwitch(switching);
                if (verdict == BeaverBuddies.Factions.FactionSwitchVerdict.Allowed)
                {
                    // The colony is remade with what the host says, on every computer (as a founding).
                    switching.startingSettings = SingletonManager.GetSingleton<ColonyFoundingService>()?.HostStartingSettings();
                    return true;
                }
                Plugin.Log($"[Factions] Refused colony {switching.slot + 1}'s switch to {switching.faction} from player {replayEvent.player}: {verdict}");
                refusal = verdict == BeaverBuddies.Factions.FactionSwitchVerdict.Unavailable || verdict == BeaverBuddies.Factions.FactionSwitchVerdict.Unknown
                    ? ColonyRefusal.FactionUnavailable
                    : verdict == BeaverBuddies.Factions.FactionSwitchVerdict.Touched ? ColonyRefusal.FactionSwitchNotAllowed : ColonyRefusal.HostRefused;
                return false;
            }
            if (replayEvent is BuildingPlacedEvent placed && BeaverBuddies.Factions.MixedFactions.IsOn && replayEvent.slot >= 0)
            {
                var service = SingletonManager.GetSingleton<ColonyRulesService>();
                string templateFaction = BeaverBuddies.Factions.FactionCatalog.Instance?.FactionOfTemplate(placed.prefabName);
                bool tradingPost = service != null && service.world.IsTradingPostTemplate(placed.prefabName);
                if (!BeaverBuddies.Factions.FactionRules.MayPlace(true, templateFaction,
                    BeaverBuddies.Factions.ColonyFactionService.FactionOfSlot(replayEvent.slot), tradingPost))
                {
                    Plugin.Log($"[Factions] Refused {placed.prefabName} for colony {replayEvent.slot + 1}: a {templateFaction} building");
                    refusal = ColonyRefusal.OtherFactionBuilding;
                    return false;
                }
            }
            return true;
        }

        // Host only: the halves of a Trading Post whose other half was refused, with the reason.
        private readonly Dictionary<ReplayEvent, ColonyRefusal> refusedPartners = new Dictionary<ReplayEvent, ColonyRefusal>();

        /// <summary>
        /// Host only, before a tick's actions are judged one by one: a Trading Post is two placements, one per half,
        /// and each is judged on its own. One half could be accepted and the other refused, leaving a lone half that
        /// can never finish (its construction waits to be linked). Two halves placed together by one player (side by
        /// side, in the same batch) are judged here first, and if either fails, both are refused with that reason.
        /// </summary>
        public static void JudgePairs(List<ReplayEvent> events)
        {
            var service = SingletonManager.GetSingleton<ColonyRulesService>();
            if (service == null || !ColonyModeService.IsSeparateColonies) return;
            try
            {
                var halves = new List<BuildingPlacedEvent>();
                foreach (ReplayEvent e in events)
                {
                    if (e is BuildingPlacedEvent placed && service.world.IsTradingPostTemplate(placed.prefabName)) halves.Add(placed);
                }
                var paired = new HashSet<BuildingPlacedEvent>();
                for (int i = 0; i < halves.Count; i++)
                {
                    if (paired.Contains(halves[i])) continue;
                    for (int j = i + 1; j < halves.Count; j++)
                    {
                        BuildingPlacedEvent a = halves[i], b = halves[j];
                        if (paired.Contains(b) || a.player != b.player || a.coordinates.z != b.coordinates.z) continue;
                        if (Math.Abs(a.coordinates.x - b.coordinates.x) + Math.Abs(a.coordinates.y - b.coordinates.y) != 1) continue;
                        paired.Add(a);
                        paired.Add(b);
                        int slot = ColonySession.SlotOfPlayer(a.player);
                        ColonyVerdict first = service.Judge(a, slot, rewrite: false), second = service.Judge(b, slot, rewrite: false);
                        if (first.IsAllowed && second.IsAllowed) break;
                        ColonyRefusal why = first.IsAllowed ? second.Refusal : first.Refusal;
                        service.refusedPartners[a] = why;
                        service.refusedPartners[b] = why;
                        Plugin.Log($"[Colony] Both halves of a Trading Post from player {a.player} will be refused: {why}, {(first.IsAllowed ? second : first).Detail}");
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                // Judged one by one, as before.
                Plugin.LogWarning("[Colony] Could not judge the Trading Post halves together: " + error.Message);
            }
        }

        private static bool IsDevShortcut(ReplayEvent replayEvent) =>
            (replayEvent is BuildingUnlockedEvent building && building.free)
            || (replayEvent is WorkerTypeUnlockedEvent workerType && workerType.free)
            || replayEvent is ConstructionSiteFinishedNowEvent || replayEvent is ScienceAddedEvent;

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

        // Another mod's event types already warned about (once each: the warning is for the log, not for every click).
        private static readonly HashSet<Type> warnedScopeless = new HashSet<Type>();

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
                // RuntimeChecks fails the build for one of this mod's events without a scope, so this is another mod's
                // (MixedStorage's StorageAllocationEvent). One that names a building in an entityID field changes that
                // building: judged like this mod's own, so nobody sets another colony's warehouse through it.
                scope = ColonyRules.ScopeByEntityField(replayEvent);
                if (warnedScopeless.Add(replayEvent.GetType()))
                    Plugin.LogWarning(scope != null
                        ? $"[Colony] {replayEvent.type} declares no colony scope; judged as a change to the building in its entityID"
                        : $"[Colony] {replayEvent.type} declares no colony scope and names no building; allowing it");
                if (scope == null) return ColonyVerdict.Allow;
            }
            if (scope.Kind == ColonyScopeKind.Founding)
            {
                var founding = SingletonManager.GetSingleton<ColonyFoundingService>();
                if (founding == null) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "no founding service");
                ColonyVerdict verdict = founding.Judge(slot, ColonyGameWorld.ToPlacement(scope.Placement));
                // The host, allowing it: the colony starts with what the host says, on every computer.
                if (rewrite && verdict.IsAllowed && replayEvent is FoundColonyEvent found)
                    found.startingSettings = founding.HostStartingSettings();
                return verdict;
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
            ColonyRefusal.TouchesOtherColony => "BeaverBuddies.Colony.Refused.TouchesOtherColony",
            ColonyRefusal.DevModeOff => "BeaverBuddies.Colony.Refused.DevModeOff",
            ColonyRefusal.HostRefused => "BeaverBuddies.Colony.Refused.HostRefused",
            ColonyRefusal.FactionUnavailable => "BeaverBuddies.Colony.Refused.FactionUnavailable",
            ColonyRefusal.FactionSwitchNotAllowed => "BeaverBuddies.Colony.Refused.FactionSwitch",
            ColonyRefusal.OtherFactionBuilding => "BeaverBuddies.Colony.Refused.OtherFactionBuilding",
            ColonyRefusal.NotStartedYet => "BeaverBuddies.Colony.Refused.NotStartedYet",
            _ => "BeaverBuddies.Colony.Refused.OtherColony",
        });
    }
}
