using BeaverBuddies.Events;
using BeaverBuddies.Factions;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Beavers;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.FactionSystem;
using Timberborn.GameDistricts;
using Timberborn.GameStartup;
using Timberborn.Goods;
using Timberborn.InputSystem;
using Timberborn.InventorySystem;
using Timberborn.NewGameConfigurationSystem;
using Timberborn.SelectionSystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Founds a colony for a player who has none: on a map with one start the game's starting building is the first
    /// player's, and every other player places their own district center, once, anywhere that does not join another
    /// colony's roads. It appears finished, owned by that player, with the new game's starting food, water, adults
    /// and children. Founding in a shared game turns it into a separate-colonies game.
    ///
    /// The placement is an ordinary replayed action (FoundColonyEvent): the host judges it like any other and every
    /// computer founds the colony at the same tick, so beaver creation and its random numbers line up.
    /// </summary>
    public class ColonyFoundingService : RegisteredSingleton, ILoadableSingleton, IPostLoadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        public const string FoundKeyBindingId = "BeaverBuddies.KeyBind.FoundColony";

        private readonly ColonyModeService _colonyModeService;
        private readonly StartingBuildingSpawner _startingBuildingSpawner;
        private readonly StartingBuildingToolDescriber _startingBuildingToolDescriber;
        private readonly BlockObjectToolFactory _blockObjectToolFactory;
        private readonly ConstructionFactory _constructionFactory;
        private readonly BeaverFactory _beaverFactory;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private readonly ToolService _toolService;
        private readonly InputService _inputService;
        private readonly CameraTargeter _cameraTargeter;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly BlockValidator _blockValidator;
        private readonly ISpecService _specService;
        private readonly EntityRegistry _entityRegistry;
        private readonly EntityService _entityService;

        // One founding tool per faction (a mixed game places each faction's own district center).
        private readonly Dictionary<string, BlockObjectTool> foundingTools = new Dictionary<string, BlockObjectTool>();
        private string placingFaction;

        public ColonyFoundingService(ColonyModeService colonyModeService, StartingBuildingSpawner startingBuildingSpawner,
            StartingBuildingToolDescriber startingBuildingToolDescriber, BlockObjectToolFactory blockObjectToolFactory,
            ConstructionFactory constructionFactory, BeaverFactory beaverFactory,
            EntityComponentRegistry entityComponentRegistry, ToolService toolService, InputService inputService,
            CameraTargeter cameraTargeter, DialogBoxShower dialogBoxShower, BlockValidator blockValidator,
            ISpecService specService, DistrictCenterRegistry districtCenterRegistry, EntityRegistry entityRegistry,
            EntityService entityService)
        {
            _entityRegistry = entityRegistry;
            _entityService = entityService;
            _colonyModeService = colonyModeService;
            _startingBuildingSpawner = startingBuildingSpawner;
            _startingBuildingToolDescriber = startingBuildingToolDescriber;
            _blockObjectToolFactory = blockObjectToolFactory;
            _constructionFactory = constructionFactory;
            _beaverFactory = beaverFactory;
            _entityComponentRegistry = entityComponentRegistry;
            _toolService = toolService;
            _inputService = inputService;
            _cameraTargeter = cameraTargeter;
            _dialogBoxShower = dialogBoxShower;
            _blockValidator = blockValidator;
            _specService = specService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        /// <summary>True while this computer's player is using the founding tool (not the ordinary build menu).</summary>
        public bool FoundingToolActive => foundingTools.Values.Any(tool => _toolService.ActiveTool == tool);

        /// <summary>
        /// The district center a colony of this faction starts with: in a mixed game that faction's own (the two have the
        /// same footprint and entrance, which RuntimeChecks pins), otherwise the game's.
        /// </summary>
        public TemplateSpec CenterOf(string faction)
        {
            TemplateSpec own = MixedFactions.IsOn ? FactionCatalog.Instance?.DistrictCenterOf(faction) : null;
            return own ?? _startingBuildingSpawner.StartingBuildingTemplateSpec;
        }

        public void Load() { }

        public void PostLoad()
        {
            _inputService.AddInputProcessor(this);
        }

        /// <summary>
        /// True when this computer's player may found a colony now: in a session, seated, with no district center of
        /// their own, and founding allowed (a separate-colonies game, or a shared game whose host allows founding in it).
        /// </summary>
        public static bool LocalPlayerMayFound
        {
            get
            {
                var service = SingletonManager.GetSingleton<ColonyFoundingService>();
                int slot = ColonySession.LocalSlot;
                return service != null && !EventIO.IsNull && slot >= 0 && !service.SlotOwnsDistrict(slot) && FoundingAllowed
                    && !WaitingForStart;
            }
        }

        /// <summary>
        /// Founding waits for the host's first tick, while other players can still join (ColonyRules.WaitsForStart); in a
        /// game started from a waiting room it doesn't. A guest's own tick count is the host's: it loads the same save and
        /// plays the host's ticks; it learns the host's flag from the host's first message.
        /// </summary>
        private static bool WaitingForStart =>
            ColonyRules.WaitsForStart(true, SingletonManager.GetSingleton<ReplayService>()?.TicksSinceLoad ?? 1,
                ColonySession.JoiningClosedAtStart);

        private static bool FoundingAllowed => ColonyModeService.IsSeparateColonies || ColonySession.HostAllowsFounding;

        /// <summary>Whether this slot owns a district center (finished or not). Saved state: the same on every computer.</summary>
        public bool SlotOwnsDistrict(int slot) =>
            _districtCenterRegistry.AllDistrictCenters.Any(dc => DistrictOwner.OwnerOfDistrict(dc) == slot);

        public bool ProcessInput()
        {
            if (!_inputService.IsKeyDown(FoundKeyBindingId)) return false;
            if (LocalPlayerMayFound) ChooseAndPlace();
            else Notice(WhyNot());
            return false;
        }

        /// <summary>
        /// A player has just been seated: if they have no colony and may found one, say so and offer the tool at once.
        /// </summary>
        public void OfferFounding()
        {
            // Seated while the game is still paused at tick 0: the offer is made at the first tick instead, when no
            // one else can join and miss the founding. Until then the key explains why (see WhyNot).
            if (WaitingForStart && !EventIO.IsNull && ColonySession.LocalSlot >= 0)
            {
                offerPending = true;
                return;
            }
            if (!LocalPlayerMayFound)
            {
                // A mixed game's colony that already stands (a multi-start game's start): the chance to switch faction.
                OfferFactionSwitch();
                return;
            }
            try
            {
                if (MixedFactions.IsOn)
                {
                    FactionChoice.ShowFoundingDialog(_dialogBoxShower, StartPlacing);
                    return;
                }
                _dialogBoxShower.Create()
                    .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Founding.Prompt"))
                    .SetConfirmButton(() => StartPlacing(null), RegisteredLocalizationService.T("BeaverBuddies.Colony.Founding.PlaceButton"))
                    .SetDefaultCancelButton()
                    .Show();
            }
            catch (Exception error)
            {
                // The key still opens the tool; a missing dialog must not break joining.
                Plugin.LogError("[Colony] Could not show the founding prompt: " + error);
            }
        }

        private bool offerPending;

        /// <summary>Every frame: an offer held back at tick 0 is made once the game has started.</summary>
        public void UpdateSingleton()
        {
            if (!offerPending || WaitingForStart) return;
            offerPending = false;
            if (EventIO.IsNull) return;
            OfferFounding();
        }

        /// <summary>The notice for a player who may not found a colony right now.</summary>
        private string WhyNot()
        {
            if (EventIO.IsNull) return "BeaverBuddies.Colony.Founding.HostFirst";
            int slot = ColonySession.LocalSlot;
            if (slot >= 0 && SlotOwnsDistrict(slot)) return "BeaverBuddies.Colony.Founding.NotNeeded";
            if (!FoundingAllowed) return "BeaverBuddies.Colony.Founding.HostOff";
            if (slot >= 0 && WaitingForStart) return "BeaverBuddies.Colony.Founding.NotStartedYet";
            return "BeaverBuddies.Colony.Founding.NotYours";
        }

        // Ctrl+K: in a mixed game the faction first (the waiting room's pick, or a card each), then the tool.
        private void ChooseAndPlace()
        {
            if (!MixedFactions.IsOn) StartPlacing(null);
            else FactionChoice.ShowFoundingDialog(_dialogBoxShower, StartPlacing);
        }

        private void StartPlacing(string faction)
        {
            if (!LocalPlayerMayFound) return;
            faction = MixedFactions.IsOn ? faction ?? MixedFactions.BaseFaction : null;
            string key = faction ?? "";
            if (!foundingTools.TryGetValue(key, out BlockObjectTool tool))
            {
                tool = _blockObjectToolFactory.Create(CenterOf(faction).GetSpec<PlaceableBlockObjectSpec>(), new FoundingPlacer(this),
                    _startingBuildingToolDescriber);
                foundingTools[key] = tool;
            }
            placingFaction = faction;
            _toolService.SwitchTool(tool);
        }

        private void PlacedByTool(Placement placement)
        {
            _toolService.SwitchToDefaultTool();
            string faction = placingFaction;
            bool playedHere = ReplayEvent.DoPrefix(() => new FoundColonyEvent
            {
                coordinates = placement.Coordinates,
                orientation = placement.Orientation,
                isFlipped = placement.FlipMode.IsFlipped,
                faction = faction,
            });
            // Founding exists only in a hosted game, where the action always goes through the host.
            if (playedHere) Notice("BeaverBuddies.Colony.Founding.HostFirst");
        }

        // ---- judging (host verdict, local check, preview) ----

        /// <summary>
        /// Host only, when it judges a founding: what the colony will start with. The save's recorded settings when
        /// it has them; otherwise the host's own default difficulty. Written into the event, so a guest whose mods
        /// change the default difficulty founds the same colony as the host (a beaver more or less would desync).
        /// </summary>
        public ColonyStartingSettings HostStartingSettings()
        {
            if (_colonyModeService.StartingSettings != null) return _colonyModeService.StartingSettings;
            GameModeSpec mode = null;
            try
            {
                mode = _specService.GetSpecs<GameModeSpec>().OrderBy(m => m.IsDefault ? 0 : 1).ThenBy(m => m.Order).FirstOrDefault();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not read the game's difficulty settings: " + error.Message);
            }
            if (mode == null)
            {
                // The game's Normal difficulty in 1.1.2.4.
                return new ColonyStartingSettings { Adults = 9, AdultAgeMin = 0.1f, AdultAgeMax = 0.7f, Children = 4,
                    ChildAgeMin = 0.1f, ChildAgeMax = 0.8f, Food = 130, Water = 0 };
            }
            return new ColonyStartingSettings
            {
                Adults = mode.StartingAdults,
                AdultAgeMin = mode.AdultAgeProgress.Min,
                AdultAgeMax = mode.AdultAgeProgress.Max,
                Children = mode.StartingChildren,
                ChildAgeMin = mode.ChildAgeProgress.Min,
                ChildAgeMax = mode.ChildAgeProgress.Max,
                Food = mode.StartingFood,
                Water = mode.StartingWater,
            };
        }

        /// <summary>
        /// Whether a district center here would join the roads of an existing district. Founding must not merge two
        /// colonies' road networks; see ColonyRoadNetworks. Before it is played (the host's judgement, the preview), in
        /// a separate-colonies game, also another colony's paths still being built (the district map knows only
        /// finished roads; see <see cref="ColonyRoadRule"/>). Not as it is played: that reads the frame's road map, and
        /// the check every computer makes then must read only what is the same on all of them.
        /// </summary>
        private bool TouchesAnotherDistrict(int actorSlot, Placement placement, bool atReplay)
        {
            BlockObjectSpec spec = _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>();
            if (ColonyRoadNetworks.Instance?.WouldJoinAnyDistrict(spec, placement) == true) return true;
            ColonyGameWorld world = SingletonManager.GetSingleton<ColonyRulesService>()?.World;
            if (atReplay || !ColonyModeService.IsSeparateColonies || world == null || spec == null) return false;
            var cells = Footprint(placement).Select(ColonyGameWorld.Cell).ToList();
            return world.RoadConflict(actorSlot, cells, ColonyGameWorld.EntranceOf(spec, placement), tradingPost: false, pathLike: false,
                out _) != ColonyRefusal.None;
        }

        /// <param name="checkBlocks">
        /// Also check that the ground is free and allows the building. Previews skip it: the game checks those itself.
        /// </param>
        /// <param name="atReplay">
        /// The check every computer makes as the founding happens. It reads saved state only; whether the host allows
        /// founding this session was decided when the host judged the request.
        /// </param>
        public ColonyVerdict Judge(int actorSlot, Placement placement, bool checkBlocks = true, bool atReplay = false)
        {
            bool foundingAllowed = atReplay || FoundingAllowed;
            bool blocksValid = !checkBlocks || _blockValidator.BlocksValid(
                _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>(), placement);
            return ColonyRules.JudgeFounding(
                actorHasSlot: actorSlot >= 0 && actorSlot < ColonySlotTable.MaxSlots,
                actorOwnsDistrict: actorSlot >= 0 && SlotOwnsDistrict(actorSlot),
                foundingAllowed: foundingAllowed,
                blocksValid: blocksValid,
                touchesOtherDistrict: TouchesAnotherDistrict(actorSlot, placement, atReplay));
        }

        private List<Vector3Int> Footprint(Placement placement)
        {
            BlockObjectSpec spec = _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>();
            var tiles = new List<Vector3Int>();
            for (int x = 0; x < spec.Size.x; x++)
            {
                for (int y = 0; y < spec.Size.y; y++)
                    tiles.Add(placement.Orientation.Transform(placement.FlipMode.Transform(new Vector3Int(x, y, 0), spec.Size.x))
                        + placement.Coordinates);
            }
            return tiles;
        }

        // ---- founding (replayed on every computer) ----

        public void Found(Placement placement, int slot, ColonyStartingSettings settings, string faction = null)
        {
            // Judged again here, at the tick it happens: the host judged it when it arrived, but someone may have built
            // or blasted there since. The world is the same on every computer now, so is the answer, and an invalid
            // spot is skipped everywhere instead of failing to place the building.
            ColonyVerdict verdict = Judge(slot, placement, atReplay: true);
            if (!verdict.IsAllowed)
            {
                Plugin.LogWarning($"[Colony] Founding at {placement.Coordinates} skipped: {verdict.Refusal}, {verdict.Detail}");
                TellFounding(slot, founded: false);
                return;
            }
            // The event's own (the host's), never this computer's specs: see HostStartingSettings. An event from an
            // older host carries none; then the save's, and failing that this computer's, as before.
            ColonyStartingSettings start = settings ?? HostStartingSettings();

            if (!_colonyModeService.Enabled)
            {
                // A shared game becomes a separate-colonies game. Every computer founds it, so the choice of separate
                // science is the host's (told to every guest).
                _colonyModeService.Enable(start, $"slot {slot} founded a colony in a shared game",
                    ColonySession.HostSeparateScience, newGame: false);
            }

            // A mixed game's colony plays the faction the host allowed (the event's), recorded before anything is made.
            string colonyFaction = FactionRules.FoundingFaction(MixedFactions.IsOn, faction, MixedFactions.BaseFaction);
            if (MixedFactions.IsOn) ColonyFactionService.Set(slot, colonyFaction);
            Building building = PlaceCenter(placement, slot, colonyFaction, out BlockObject blockObject);

            Inventory inventory = building.GetComponent<SimpleOutputInventory>()?.Inventory;
            if (inventory != null)
            {
                // The game's own starting goods (StartingGoodsProvider).
                if (start.Food > 0) inventory.GiveExistingIgnoringCapacity(new GoodAmount("Berries", start.Food));
                if (start.Water > 0) inventory.GiveExistingIgnoringCapacity(new GoodAmount("Water", start.Water));
            }

            Vector3 position = SpawnPosition(building, blockObject);
            using (MixedFactions.IsOn ? FactionCreationContext.Push(colonyFaction) : default)
            {
                SpawnBeavers(position, adults: true, start.Adults, start.AdultAgeMin, start.AdultAgeMax);
                SpawnBeavers(position, adults: false, start.Children, start.ChildAgeMin, start.ChildAgeMax);
            }
            if (ColonySession.LocalSlot == slot) LocalFactionPick.Clear();
            Plugin.Log($"[Colony] Slot {slot} founded a{(MixedFactions.IsOn ? " " + colonyFaction : "")} colony at {placement.Coordinates} with {start}");

            // Display only, on this computer.
            if (ColonySession.LocalSlot == slot)
            {
                try { _cameraTargeter.CenterCameraOn(building.GetComponent<SelectableObject>()); }
                catch (Exception error) { Plugin.LogWarning("[Colony] Could not move the camera: " + error.Message); }
            }
            TellFounding(slot, founded: true);
        }

        // A finished district center of this faction, owned by the slot from the moment it is made.
        private Building PlaceCenter(Placement placement, int slot, string faction, out BlockObject blockObject)
        {
            var builder = new EntitySetup.Builder(CenterOf(faction).GetSpec<BlockObjectSpec>().Blueprint);
            DistrictOwner.PendingSlot = slot;
            try
            {
                blockObject = _constructionFactory.CreateAsFinished(builder, placement);
            }
            finally
            {
                DistrictOwner.PendingSlot = null;
            }
            blockObject.GetComponent<DistrictOwner>()?.SetSlot(slot);
            return blockObject.GetComponent<Building>();
        }

        // ---- a mixed game's faction switch (D14) ----

        /// <summary>
        /// Whether a colony is still untouched, as every computer counts it (saved state only): its district centers, and
        /// the buildings of a faction of their own it owns besides them, finished or not. Common buildings (paths, which a
        /// map may already have) look right in either faction, and a colony keeps them.
        /// </summary>
        public UntouchedFacts UntouchedFacts(int slot)
        {
            int centers = _districtCenterRegistry.AllDistrictCenters.Count(dc => DistrictOwner.OwnerOfDistrict(dc) == slot);
            int others = 0;
            FactionCatalog catalog = FactionCatalog.Instance;
            foreach (EntityComponent entity in _entityRegistry.Entities)
            {
                ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
                if (stamp == null || stamp.Slot != slot || entity.GetComponent<DistrictCenter>() != null) continue;
                if (catalog?.FactionOfTemplate(entity.GetComponent<TemplateSpec>()?.TemplateName) != null) others++;
            }
            return new UntouchedFacts(centers, others, marks: 0, tradeOpen: false, unlocks: 0);
        }

        /// <summary>Display: whether a colony is untouched, stopping at the first building of its own faction.</summary>
        public bool IsUntouched(int slot)
        {
            FactionCatalog catalog = FactionCatalog.Instance;
            foreach (EntityComponent entity in _entityRegistry.Entities)
            {
                ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
                if (stamp == null || stamp.Slot != slot || entity.GetComponent<DistrictCenter>() != null) continue;
                if (catalog?.FactionOfTemplate(entity.GetComponent<TemplateSpec>()?.TemplateName) != null) return false;
            }
            return true;
        }

        /// <summary>
        /// Played on every computer (ColonyFactionSwitchEvent): an untouched colony becomes another faction. Each of its
        /// district centers is replaced in place by that faction's (the same footprint), its stock moved across, and its
        /// beavers by as many of that faction's (up to the starting numbers). Judged again here, from saved state.
        /// </summary>
        public void SwitchFaction(int slot, string faction)
        {
            FactionSwitchVerdict verdict = FactionRules.JudgeSwitch(MixedFactions.IsOn, isSeatOwner: true,
                ColonyFactionService.FactionOfSlot(slot), faction, ColonyFactionService.FactionIds.ToList(), null, UntouchedFacts(slot));
            if (verdict != FactionSwitchVerdict.Allowed)
            {
                Plugin.LogWarning($"[Factions] Colony {slot + 1}'s switch to {faction} skipped: {verdict}");
                return;
            }
            ColonyStartingSettings start = HostStartingSettings();
            List<DistrictCenter> centers = _districtCenterRegistry.AllDistrictCenters
                .Where(dc => DistrictOwner.OwnerOfDistrict(dc) == slot).ToList();
            int adults = 0, children = 0;
            var stock = new List<GoodAmount>();
            var placements = new List<Placement>();
            foreach (DistrictCenter center in centers)
            {
                DistrictPopulation population = center.GetComponent<DistrictPopulation>();
                List<Beaver> beavers = population != null ? population.Beavers.ToList() : new List<Beaver>();
                adults += population?.NumberOfAdults ?? 0;
                children += population?.NumberOfChildren ?? 0;
                foreach (Beaver beaver in beavers) _entityService.Delete(beaver);
                Inventory inventory = center.GetComponent<SimpleOutputInventory>()?.Inventory;
                if (inventory != null) stock.AddRange(inventory.Stock.Where(good => good.Amount > 0));
                placements.Add(center.GetComponent<BlockObject>().Placement);
                _entityService.Delete(center);
            }
            ColonyFactionService.Set(slot, faction);
            HashSet<string> keeps = FactionCatalog.Instance?.GoodsOf(faction);
            bool first = true;
            foreach (Placement placement in placements)
            {
                Building building = PlaceCenter(placement, slot, faction, out BlockObject blockObject);
                if (!first) continue;
                first = false;
                Inventory inventory = building.GetComponent<SimpleOutputInventory>()?.Inventory;
                foreach (GoodAmount good in stock)
                {
                    if (inventory != null && (keeps == null || keeps.Contains(good.GoodId))) inventory.GiveExistingIgnoringCapacity(good);
                }
                Vector3 position = SpawnPosition(building, blockObject);
                using (FactionCreationContext.Push(faction))
                {
                    SpawnBeavers(position, adults: true, Math.Min(adults, start.Adults), start.AdultAgeMin, start.AdultAgeMax);
                    SpawnBeavers(position, adults: false, Math.Min(children, start.Children), start.ChildAgeMin, start.ChildAgeMax);
                }
                if (ColonySession.LocalSlot == slot)
                {
                    try { _cameraTargeter.CenterCameraOn(building.GetComponent<SelectableObject>()); }
                    catch (Exception error) { Plugin.LogWarning("[Factions] Could not move the camera: " + error.Message); }
                }
            }
            if (ColonySession.LocalSlot == slot) LocalFactionPick.Clear();
            Plugin.Log($"[Factions] Colony {slot + 1} switched to {faction}: {placements.Count} district center(s), {adults} adults, {children} children");
            try
            {
                FactionSpec spec = MixedFactions.Spec(faction);
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(RegisteredLocalizationService.T(
                    "BeaverBuddies.Colony.Faction.Switched", ColonyExchangeService.ColonyName(slot), spec?.DisplayName.Value ?? faction), warning: false);
            }
            catch (Exception error) { Plugin.LogWarning("[Factions] Could not show the switch: " + error.Message); }
        }

        /// <summary>A seated player whose mixed game's colony already stands: offered the switch while it is untouched.</summary>
        private void OfferFactionSwitch()
        {
            if (!MixedFactions.IsOn || EventIO.IsNull) return;
            int slot = ColonySession.LocalSlot;
            if (slot < 0 || ColonySession.LocalSeat != slot || !SlotOwnsDistrict(slot)) return;
            try { FactionChoice.OfferSwitch(_dialogBoxShower, slot, UntouchedFacts(slot)); }
            catch (Exception error) { Plugin.LogWarning("[Factions] Could not offer the faction switch: " + error.Message); }
        }

        private static Vector3 SpawnPosition(Building building, BlockObject blockObject)
        {
            // Where the game puts starting beavers: the building's free entrance. Straight after placing, that may not
            // be known yet, so fall back to the tile in front of the entrance.
            Vector3? access = building.GetComponent<BuildingAccessible>()?.Accessible?.UnblockedSingleAccess;
            if (access.HasValue) return access.Value;
            return CoordinateSystem.GridToWorldCentered(blockObject.PositionedEntrance.Coordinates);
        }

        /*
         * 9/21/2026 (Timberborn 1.1.2.4), StartingBeaversInitializer.SpawnBeavers
            float num = ((numberOfBeavers > 1) ? ((lifeStageProgressRange.Max - lifeStageProgressRange.Min) / (float)(numberOfBeavers - 1)) : 0f);
            for (int i = 0; i < numberOfBeavers; i++)
            {
                float lifeStageProgress = lifeStageProgressRange.Min + num * (float)i;
                SpawnBeaver(position, adults, lifeStageProgress);
            }
         */
        private void SpawnBeavers(Vector3 position, bool adults, int count, float min, float max)
        {
            float step = count > 1 ? (max - min) / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                float progress = min + step * i;
                if (adults) _beaverFactory.CreateAdult(position, progress);
                else _beaverFactory.CreateChild(position, progress);
            }
        }

        private static void Notice(string key) =>
            SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(RegisteredLocalizationService.T(key));

        /// <summary>
        /// Display only, on this computer, as a founding is played or skipped: the founder hears how it went, everyone
        /// else only that a colony was founded. Which notice, which text and whether it is a warning are decided in
        /// ColonyRules (FoundingNoticeFor, FoundingNoticeKey, FoundingNoticeWarns), where StabilityTests checks them.
        /// </summary>
        private static void TellFounding(int slot, bool founded)
        {
            try
            {
                FoundingNotice notice = ColonyRules.FoundingNoticeFor(ColonySession.LocalSlot, slot, founded);
                string key = ColonyRules.FoundingNoticeKey(notice);
                if (key == null) return;
                ColonyRulesService rules = SingletonManager.GetSingleton<ColonyRulesService>();
                if (rules == null) return;
                string text = RegisteredLocalizationService.T(key);
                if (notice == FoundingNotice.Founded) text = string.Format(text, ColonyExchangeService.ColonyName(slot));
                rules.ShowNotice(text, warning: ColonyRules.FoundingNoticeWarns(notice));
            }
            catch (Exception error)
            {
                // A notice is a courtesy; it must never break the founding every computer is playing.
                Plugin.LogWarning("[Colony] Could not show a founding notice: " + error.Message);
            }
        }

        /// <summary>The founding tool's placer: the click becomes a founding action instead of a construction site.</summary>
        private sealed class FoundingPlacer : IBlockObjectPlacer
        {
            private readonly ColonyFoundingService service;
            public FoundingPlacer(ColonyFoundingService service) => this.service = service;
            public void Place(EntitySetup.Builder entitySetupBuilder, Placement placement) => service.PlacedByTool(placement);
            public void Describe(BlockObjectTool tool, ToolDescription.Builder builder, Preview preview) { }
            public bool CanHandle(BlockObjectSpec template) => true;
        }
    }

    /// <summary>A player without a colony founds one: a finished district center, starting goods and beavers.</summary>
    [Serializable]
    public class FoundColonyEvent : ReplayEvent
    {
        public Vector3Int coordinates;
        public Orientation orientation;
        public bool isFlipped;
        /// <summary>What the colony starts with, written by the host when it allows the founding (null from an older host).</summary>
        public ColonyStartingSettings startingSettings;
        /// <summary>A mixed game's colony's faction: the founder's choice, which the host checked is available (D1, D15).</summary>
        public string faction;

        public Placement Placement => new Placement(coordinates, orientation, isFlipped ? FlipMode.Flipped : FlipMode.Unflipped);

        public override ColonyScope GetColonyScope() => ColonyScope.Found(new ColonyPlacement
        {
            X = coordinates.x,
            Y = coordinates.y,
            Z = coordinates.z,
            Orientation = (int)orientation,
            IsFlipped = isFlipped,
        });

        public override void Replay(IReplayContext context)
        {
            var service = SingletonManager.GetSingleton<ColonyFoundingService>();
            if (service == null)
            {
                Plugin.LogWarning("[Colony] Cannot found a colony: the founding service is missing");
                return;
            }
            // The host wrote the founder's slot and the starting settings into the event before playing it.
            service.Found(Placement, slot, startingSettings, faction);
        }

        public override string ToActionString() => $"Founding a colony for slot {slot} at {coordinates}";
    }
}
