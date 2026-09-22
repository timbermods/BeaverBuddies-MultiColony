using BeaverBuddies.Editor;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.PathSystem;
using Timberborn.Rendering;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Draws each colony's roads on the map in its colour: the paths (and stairs...) it placed, finished or still being
    /// built. Two colonies' roads never join except through a Trading Post, which needs one colony's road at each end,
    /// so this shows whose road runs where and whose reaches a spot. Shown while any tool other than the default one is
    /// in hand (building, planting, cutting, demolishing, founding), and at any time with its key. Display only: it reads
    /// the placed paths and their owners and never touches the simulation.
    ///
    /// Also listens for the debug key that lets a host act as the next colony, for testing alone.
    /// </summary>
    public class ColonyRoadOverlay : ILoadableSingleton, IPostLoadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        public const string ToggleKeyBindingId = "BeaverBuddies.KeyBind.ToggleColonyRoads";
        public const string FlipSeatKeyBindingId = "BeaverBuddies.KeyBind.FlipColonySeat";

        // Drawn again at most this often when a path comes or goes, and this often anyway (a finished road's district,
        // and so its colony, can change without a path coming or going).
        private const float RedrawInterval = 0.5f;
        private const float RefreshInterval = 3f;

        private readonly AreaTileDrawerFactory _areaTileDrawerFactory;
        private readonly InputService _inputService;
        private readonly ToolService _toolService;
        private readonly EventBus _eventBus;
        private readonly EntityRegistry _entityRegistry;

        private GameObject root;
        private readonly List<AreaTileDrawer> drawers = new List<AreaTileDrawer>();
        private bool failed, toggledOn, shown, dirty = true;
        private float nextRedraw, nextRefresh;

        public ColonyRoadOverlay(AreaTileDrawerFactory areaTileDrawerFactory, InputService inputService, ToolService toolService,
            EventBus eventBus, EntityRegistry entityRegistry)
        {
            _areaTileDrawerFactory = areaTileDrawerFactory;
            _inputService = inputService;
            _toolService = toolService;
            _eventBus = eventBus;
            _entityRegistry = entityRegistry;
        }

        public void Load() => _eventBus.Register(this);

        public void PostLoad() => _inputService.AddInputProcessor(this);

        [OnEvent]
        public void OnEntityInitialized(EntityInitializedEvent entityInitializedEvent)
        {
            if (entityInitializedEvent.Entity.HasComponent<PathSpec>()) dirty = true;
        }

        [OnEvent]
        public void OnEntityDeleted(EntityDeletedEvent entityDeletedEvent)
        {
            if (entityDeletedEvent.Entity.HasComponent<PathSpec>()) dirty = true;
        }

        public bool ProcessInput()
        {
            if (_inputService.IsKeyDown(ToggleKeyBindingId)) toggledOn = !toggledOn;
            if (_inputService.IsKeyDown(FlipSeatKeyBindingId) && ColonySession.TryFlipHostSeat())
            {
                string text = string.Format(RegisteredLocalizationService.T("BeaverBuddies.Colony.DebugSeat"), ColonySession.LocalSlot + 1);
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(text, warning: false);
            }
            return false;
        }

        private static readonly ColonyProfiler.Spot RoadDrawing = ColonyProfiler.Declare("Road overlay drawing");

        public void UpdateSingleton()
        {
            try
            {
                bool wanted = ColonyModeService.IsSeparateColonies
                    && (toggledOn || (_toolService.ActiveTool != null && !_toolService.IsDefaultToolActive));
                if (!wanted)
                {
                    if (shown) root?.SetActive(false);
                    shown = false;
                    return;
                }
                if (!EnsureDrawers()) return;
                if (!shown)
                {
                    root.SetActive(true);
                    shown = true;
                    dirty = true;
                    nextRedraw = 0;
                }
                float now = Time.unscaledTime;
                if (!(dirty && now >= nextRedraw) && now < nextRefresh) return;
                nextRedraw = now + RedrawInterval;
                nextRefresh = now + RefreshInterval;
                dirty = false;
                long started = ColonyProfiler.Start();
                Redraw();
                ColonyProfiler.Stop(RoadDrawing, started);
            }
            catch (Exception error)
            {
                // Seeing the roads is a help, not a requirement: previews and refusals still explain themselves.
                if (!failed) Plugin.LogError("[Colony] Could not draw colony roads: " + error);
                failed = true;
                root?.SetActive(false);
            }
        }

        private bool EnsureDrawers()
        {
            if (root != null) return true;
            if (failed) return false;
            root = new GameObject("BeaverBuddies_ColonyRoads");
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                var holder = new GameObject("Colony" + (slot + 1));
                holder.transform.parent = root.transform;
                drawers.Add(_areaTileDrawerFactory.Create(ColorOf(slot), holder));
            }
            root.SetActive(false);
            return true;
        }

        private void Redraw()
        {
            var tiles = new List<Vector3Int>[drawers.Count];
            for (int slot = 0; slot < tiles.Length; slot++) tiles[slot] = new List<Vector3Int>();
            foreach (EntityComponent entity in _entityRegistry.Entities)
            {
                if (!entity.HasComponent<PathSpec>()) continue;
                BlockObject blockObject = entity.GetComponent<BlockObject>();
                if (!blockObject || blockObject.IsPreview || !blockObject.Positioned) continue;
                int? owner = DistrictOwner.OwnerOf(blockObject);
                if (owner == null || owner.Value < 0 || owner.Value >= tiles.Length) continue;
                foreach (Vector3Int tile in blockObject.PositionedBlocks.GetAllCoordinates()) tiles[owner.Value].Add(tile);
            }
            for (int slot = 0; slot < drawers.Count; slot++) drawers[slot].UpdateArea(tiles[slot]);
        }

        private static Color ColorOf(int slot)
        {
            Color color = slot < StartingLocationPlayer.PLAYER_COLORS.Length ? StartingLocationPlayer.PLAYER_COLORS[slot] : Color.white;
            color.a = 0.75f;
            return color;
        }
    }
}
