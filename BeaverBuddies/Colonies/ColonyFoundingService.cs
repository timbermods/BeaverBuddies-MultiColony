using BeaverBuddies.Events;
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
using Timberborn.GameDistricts;
using Timberborn.GameStartup;
using Timberborn.Goods;
using Timberborn.InputSystem;
using Timberborn.InventorySystem;
using Timberborn.NewGameConfigurationSystem;
using Timberborn.SelectionSystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.SingletonSystem;
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
    public class ColonyFoundingService : RegisteredSingleton, ILoadableSingleton, IPostLoadableSingleton, IInputProcessor
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
        private readonly BlockValidator _blockValidator;
        private readonly ISpecService _specService;

        private BlockObjectTool foundingTool;

        public ColonyFoundingService(ColonyModeService colonyModeService, StartingBuildingSpawner startingBuildingSpawner,
            StartingBuildingToolDescriber startingBuildingToolDescriber, BlockObjectToolFactory blockObjectToolFactory,
            ConstructionFactory constructionFactory, BeaverFactory beaverFactory,
            EntityComponentRegistry entityComponentRegistry, ToolService toolService, InputService inputService,
            CameraTargeter cameraTargeter, DialogBoxShower dialogBoxShower, BlockValidator blockValidator,
            ISpecService specService)
        {
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
        }

        /// <summary>True while this computer's player is using the founding tool (not the ordinary build menu).</summary>
        public bool FoundingToolActive => foundingTool != null && _toolService.ActiveTool == foundingTool;

        public void Load() { }

        public void PostLoad()
        {
            _inputService.AddInputProcessor(this);
        }

        /// <summary>
        /// True when this computer's player may found a colony now: in a session, seated, with no district center of
        /// their own, and founding allowed (a separate-colonies game, or the host allows it).
        /// </summary>
        public static bool LocalPlayerMayFound
        {
            get
            {
                var service = SingletonManager.GetSingleton<ColonyFoundingService>();
                int slot = ColonySession.LocalSlot;
                return service != null && !EventIO.IsNull && slot >= 0 && !service.SlotOwnsDistrict(slot) && FoundingAllowed;
            }
        }

        private static bool FoundingAllowed => ColonyModeService.IsSeparateColonies || ColonySession.HostAllowsFounding;

        /// <summary>Whether this slot owns a district center (finished or not). Saved state: the same on every computer.</summary>
        public bool SlotOwnsDistrict(int slot) =>
            _entityComponentRegistry.GetEnabled<DistrictCenter>().Any(dc => DistrictOwner.OwnerOfDistrict(dc) == slot);

        public bool ProcessInput()
        {
            if (!_inputService.IsKeyDown(FoundKeyBindingId)) return false;
            if (LocalPlayerMayFound) StartPlacing();
            else Notice(WhyNot());
            return false;
        }

        /// <summary>
        /// A player has just been seated: if they have no colony and may found one, say so and offer the tool at once.
        /// </summary>
        public void OfferFounding()
        {
            if (!LocalPlayerMayFound) return;
            try
            {
                _dialogBoxShower.Create()
                    .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Founding.Prompt"))
                    .SetConfirmButton(StartPlacing, RegisteredLocalizationService.T("BeaverBuddies.Colony.Founding.PlaceButton"))
                    .SetDefaultCancelButton()
                    .Show();
            }
            catch (Exception error)
            {
                // The key still opens the tool; a missing dialog must not break joining.
                Plugin.LogError("[Colony] Could not show the founding prompt: " + error);
            }
        }

        /// <summary>The notice for a player who may not found a colony right now.</summary>
        private string WhyNot()
        {
            if (EventIO.IsNull) return "BeaverBuddies.Colony.Founding.HostFirst";
            int slot = ColonySession.LocalSlot;
            if (slot >= 0 && SlotOwnsDistrict(slot)) return "BeaverBuddies.Colony.Founding.NotNeeded";
            if (!FoundingAllowed) return "BeaverBuddies.Colony.Founding.HostOff";
            return "BeaverBuddies.Colony.Founding.NotYours";
        }

        private void StartPlacing()
        {
            if (!LocalPlayerMayFound) return;
            foundingTool ??= _blockObjectToolFactory.Create(
                _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<PlaceableBlockObjectSpec>(),
                new FoundingPlacer(this), _startingBuildingToolDescriber);
            _toolService.SwitchTool(foundingTool);
        }

        private void PlacedByTool(Placement placement)
        {
            _toolService.SwitchToDefaultTool();
            bool playedHere = ReplayEvent.DoPrefix(() => new FoundColonyEvent
            {
                coordinates = placement.Coordinates,
                orientation = placement.Orientation,
                isFlipped = placement.FlipMode.IsFlipped,
            });
            // Founding exists only in a hosted game, where the action always goes through the host.
            if (playedHere) Notice("BeaverBuddies.Colony.Founding.HostFirst");
        }

        // ---- judging (host verdict, local check, preview) ----

        /// <summary>
        /// What a founded colony starts with: the new game's settings when the save recorded them, otherwise the game's default
        /// difficulty (the same specs on every computer).
        /// </summary>
        private ColonyStartingSettings StartingSettings()
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
        /// colonies' road networks; see ColonyRoadNetworks.
        /// </summary>
        private bool TouchesAnotherDistrict(Placement placement) =>
            ColonyRoadNetworks.Instance?.WouldJoinAnyDistrict(
                _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>(), placement) ?? false;

        /// <param name="checkBlocks">
        /// Also check that the ground is free and allows the building. Previews skip it: the game checks those itself.
        /// </param>
        /// <param name="atReplay">
        /// The check every computer makes as the founding happens. It reads saved state only; whether the host allows
        /// founding this session was decided when the host judged the request.
        /// </param>
        public ColonyVerdict Judge(int actorSlot, Placement placement, bool checkBlocks = true, bool atReplay = false)
        {
            bool blocksValid = !checkBlocks || _blockValidator.BlocksValid(
                _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>(), placement);
            return ColonyRules.JudgeFounding(
                actorHasSlot: actorSlot >= 0 && actorSlot < ColonySlotTable.MaxSlots,
                actorOwnsDistrict: actorSlot >= 0 && SlotOwnsDistrict(actorSlot),
                foundingAllowed: atReplay || FoundingAllowed,
                blocksValid: blocksValid,
                touchesOtherDistrict: TouchesAnotherDistrict(placement));
        }

        // ---- founding (replayed on every computer) ----

        public void Found(Placement placement, int slot)
        {
            // Judged again here, at the tick it happens: the host judged it when it arrived, but someone may have built
            // or blasted there since. The world is the same on every computer now, so is the answer, and an invalid
            // spot is skipped everywhere instead of failing to place the building.
            ColonyVerdict verdict = Judge(slot, placement, atReplay: true);
            if (!verdict.IsAllowed)
            {
                Plugin.LogWarning($"[Colony] Founding at {placement.Coordinates} skipped: {verdict.Refusal}, {verdict.Detail}");
                Notice("BeaverBuddies.Colony.Founding.Failed");
                return;
            }
            ColonyStartingSettings start = StartingSettings();

            if (!_colonyModeService.Enabled)
            {
                // A shared game becomes a separate-colonies game. Its districts were made with the game's open imports;
                // close them, as a separate-colonies game does for every new district, so trade starts closed.
                foreach (DistrictCenter districtCenter in _entityComponentRegistry.GetEnabled<DistrictCenter>().ToList())
                {
                    DistrictDistributionSetting setting = districtCenter.GetComponent<DistrictDistributionSetting>();
                    if (setting == null) continue;
                    foreach (GoodDistributionSetting good in setting.GoodDistributionSettings.ToList())
                        good.SetImportOption(ImportOption.Disabled);
                }
                // Every computer founds it, so the choice of separate science is the host's (told to every guest).
                _colonyModeService.Enable(start, $"slot {slot} founded a colony in a shared game",
                    ColonySession.HostSeparateScience, newGame: false);
            }

            var builder = new EntitySetup.Builder(_startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>().Blueprint);
            BlockObject blockObject;
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
            Building building = blockObject.GetComponent<Building>();

            Inventory inventory = building.GetComponent<SimpleOutputInventory>()?.Inventory;
            if (inventory != null)
            {
                // The game's own starting goods (StartingGoodsProvider).
                if (start.Food > 0) inventory.GiveExistingIgnoringCapacity(new GoodAmount("Berries", start.Food));
                if (start.Water > 0) inventory.GiveExistingIgnoringCapacity(new GoodAmount("Water", start.Water));
            }

            Vector3 position = SpawnPosition(building, blockObject);
            SpawnBeavers(position, adults: true, start.Adults, start.AdultAgeMin, start.AdultAgeMax);
            SpawnBeavers(position, adults: false, start.Children, start.ChildAgeMin, start.ChildAgeMax);
            Plugin.Log($"[Colony] Slot {slot} founded a colony at {placement.Coordinates} with {start}");

            // Display only, on this computer.
            if (ColonySession.LocalSlot == slot)
            {
                try { _cameraTargeter.CenterCameraOn(building.GetComponent<SelectableObject>()); }
                catch (Exception error) { Plugin.LogWarning("[Colony] Could not move the camera: " + error.Message); }
            }
            Notice("BeaverBuddies.Colony.Founding.Done");
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
            // The host wrote the founder's slot into the event before playing it.
            service.Found(Placement, slot);
        }

        public override string ToActionString() => $"Founding a colony for slot {slot} at {coordinates}";
    }
}
