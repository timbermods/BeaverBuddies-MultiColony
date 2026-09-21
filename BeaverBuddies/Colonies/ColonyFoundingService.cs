using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Beavers;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
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
using Timberborn.SelectionSystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Founds colony 2 on a map with one start. The game's own starting building starts colony 1 as usual. The player
    /// of colony 2 then places a district center anywhere colony 1 has not built; it appears finished, with the same
    /// starting food, water, adults and children the new game gave colony 1, and from then on the land is divided
    /// between the two district centers exactly as on a map with two starts.
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

        private BlockObjectTool foundingTool;

        public ColonyFoundingService(ColonyModeService colonyModeService, StartingBuildingSpawner startingBuildingSpawner,
            StartingBuildingToolDescriber startingBuildingToolDescriber, BlockObjectToolFactory blockObjectToolFactory,
            ConstructionFactory constructionFactory, BeaverFactory beaverFactory,
            EntityComponentRegistry entityComponentRegistry, ToolService toolService, InputService inputService,
            CameraTargeter cameraTargeter, DialogBoxShower dialogBoxShower, BlockValidator blockValidator)
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
        }

        /// <summary>True while this computer's player is using the founding tool (not the ordinary build menu).</summary>
        public bool FoundingToolActive => foundingTool != null && _toolService.ActiveTool == foundingTool;

        public void Load() { }

        public void PostLoad()
        {
            _inputService.AddInputProcessor(this);
        }

        /// <summary>True when this computer's player is the one who should found colony 2 now.</summary>
        public static bool LocalPlayerMayFound =>
            ColonyModeService.FoundingPending && !EventIO.IsNull && ColonySession.LocalColony == 2;

        public bool ProcessInput()
        {
            if (!_inputService.IsKeyDown(FoundKeyBindingId)) return false;
            if (LocalPlayerMayFound) StartPlacing();
            else Notice(ColonyModeService.FoundingPending
                ? "BeaverBuddies.Colony.Founding.NotYours"
                : "BeaverBuddies.Colony.Founding.NotNeeded");
            return false;
        }

        /// <summary>
        /// A guest learned its seat: if it plays the colony still to be founded, say so and offer the tool at once.
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
        /// Where colony 1's land is measured from: its starting building, as recorded when the game placed it. For a
        /// save without that record, the district center with the lowest entity id (arbitrary, but the same on every
        /// computer).
        /// </summary>
        public ColonyTile? FirstColonyStart(out Vector3Int coordinates)
        {
            if (_colonyModeService.FirstColonyStart.HasValue)
            {
                coordinates = _colonyModeService.FirstColonyStart.Value;
                return ColonyGameWorld.TileOf(coordinates);
            }
            coordinates = default;
            DistrictCenter first = _entityComponentRegistry.GetEnabled<DistrictCenter>()
                .Where(dc => dc.GetComponent<EntityComponent>() != null && dc.GetComponent<BlockObject>() != null)
                .OrderBy(dc => dc.GetComponent<EntityComponent>().EntityId)
                .FirstOrDefault();
            if (first == null) return null;
            coordinates = first.GetComponent<BlockObject>().Coordinates;
            return ColonyGameWorld.TileOf(coordinates);
        }

        private IEnumerable<IReadOnlyList<ColonyTile>> ExistingBuildingFootprints()
        {
            foreach (Building building in _entityComponentRegistry.GetEnabled<Building>())
            {
                BlockObject blockObject = building.GetComponent<BlockObject>();
                if (blockObject == null || !blockObject.Positioned || blockObject.IsPreview) continue;
                yield return ColonyGameWorld.FootprintOf(blockObject.PositionedBlocks.GetAllCoordinates());
            }
        }

        public IReadOnlyList<ColonyTile> DistrictCenterFootprint(Placement placement) =>
            ColonyGameWorld.FootprintOf(_startingBuildingSpawner.StartingBuildingTemplateSpec
                .GetSpec<BlockObjectSpec>().GetBlocks(placement).Select(b => b.Coordinates));

        /// <param name="checkBlocks">
        /// Also check that the ground is free and allows the building. Previews skip it: the game checks those itself.
        /// </param>
        public ColonyVerdict Judge(int actorColony, Placement placement, IReadOnlyList<ColonyTile> footprint = null,
            bool checkBlocks = true)
        {
            if (!_colonyModeService.AwaitingFounding)
                return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "no colony is waiting to be founded");
            if (_colonyModeService.StartingSettings == null)
                return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "the new game's starting settings were not recorded");
            if (checkBlocks && !_blockValidator.BlocksValid(
                    _startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>(), placement))
                return ColonyVerdict.Refuse(ColonyRefusal.Blocked, $"the ground at {placement.Coordinates} is taken or unsuitable");
            return ColonyRules.JudgeFounding(actorColony, FirstColonyStart(out _),
                ColonyGameWorld.TileOf(placement.Coordinates), footprint ?? DistrictCenterFootprint(placement),
                ExistingBuildingFootprints());
        }

        // ---- founding (replayed on every computer) ----

        public void Found(Placement placement)
        {
            if (!_colonyModeService.AwaitingFounding)
            {
                Plugin.LogWarning("[Colony] Ignoring a founding: no colony is waiting to be founded");
                return;
            }
            // Judged again here, at the tick it happens: the host judged it when it arrived, but colony 1 may have
            // built or blasted there since. The world is the same on every computer now, so is the answer, and an
            // invalid spot is skipped everywhere instead of failing to place the building.
            ColonyVerdict verdict = Judge(2, placement);
            if (!verdict.IsAllowed || FirstColonyStart(out Vector3Int firstStart) == null)
            {
                Plugin.LogWarning($"[Colony] Founding at {placement.Coordinates} skipped: {verdict.Refusal}, {verdict.Detail}");
                Notice("BeaverBuddies.Colony.Founding.Failed");
                return;
            }
            ColonyStartingSettings start = _colonyModeService.StartingSettings;

            // Colony 1's districts were made before the colonies existed, with the game's open imports. Close them,
            // as a separate-colonies game does for every new district, so trade starts closed on both sides.
            foreach (DistrictCenter districtCenter in _entityComponentRegistry.GetEnabled<DistrictCenter>().ToList())
            {
                DistrictDistributionSetting setting = districtCenter.GetComponent<DistrictDistributionSetting>();
                if (setting == null) continue;
                foreach (GoodDistributionSetting good in setting.GoodDistributionSettings.ToList())
                    good.SetImportOption(ImportOption.Disabled);
            }

            // Divide the land first: the new district center then takes the separate-colonies trade defaults.
            _colonyModeService.CompleteFounding(firstStart, placement.Coordinates);

            var builder = new EntitySetup.Builder(_startingBuildingSpawner.StartingBuildingTemplateSpec.GetSpec<BlockObjectSpec>().Blueprint);
            BlockObject blockObject = _constructionFactory.CreateAsFinished(builder, placement);
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
            Plugin.Log($"[Colony] Colony 2 founded at {placement.Coordinates} with {start}");

            // Display only, on this computer.
            if (ColonySession.LocalColony == 2)
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

    /// <summary>Colony 2's player founds their colony: a finished district center, starting goods and beavers.</summary>
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
            service.Found(Placement);
        }

        public override string ToActionString() => $"Founding colony 2 at {coordinates}";
    }
}
