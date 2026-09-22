using BeaverBuddies.Editor;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.MapStateSystem;
using Timberborn.PathSystem;
using Timberborn.Rendering;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Draws each colony's roads on the map in its colour: every cell of the paths (and stairs, bridges, district
    /// centers...) it placed, finished or still being built, as a filled square (the game's own tile marker, the one it
    /// draws a building's range with) in a strong version of the colony's colour, so whose road is whose reads at a
    /// glance, by day or night. Two colonies' roads never join except through a Trading Post, which trades once one
    /// colony's road reaches each end, so this shows whose road runs where and whose reaches a spot. Shown while a
    /// building (or founding) tool is in hand, and at any time with its key. Display only: it reads the placed paths and
    /// their owners and never touches the simulation. The squares are built into a mesh when something changes, not
    /// drawn again every frame.
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
        private readonly MarkerDrawerFactory _markerDrawerFactory;
        private readonly MapSize _mapSize;
        private readonly InputService _inputService;
        private readonly ToolService _toolService;
        private readonly EventBus _eventBus;
        private readonly EntityRegistry _entityRegistry;

        private GameObject root;
        private readonly List<AreaTileDrawer> drawers = new List<AreaTileDrawer>();
        private bool failed, toggledOn, shown, dirty = true;
        private float nextRedraw, nextRefresh;

        public ColonyRoadOverlay(AreaTileDrawerFactory areaTileDrawerFactory, MarkerDrawerFactory markerDrawerFactory, MapSize mapSize,
            InputService inputService, ToolService toolService, EventBus eventBus, EntityRegistry entityRegistry)
        {
            _areaTileDrawerFactory = areaTileDrawerFactory;
            _markerDrawerFactory = markerDrawerFactory;
            _mapSize = mapSize;
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
                bool wanted = ColonyModeService.IsSeparateColonies && (toggledOn || PlacingSomething());
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

        /// <summary>A building's or the founding tool is in hand: where roads run matters for what is being placed.</summary>
        private bool PlacingSomething() =>
            _toolService.ActiveTool is BlockObjectTool
            || SingletonManager.GetSingleton<ColonyFoundingService>()?.FoundingToolActive == true;

        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
        private const float AbovePaths = 0.12f;

        private bool EnsureDrawers()
        {
            if (root != null) return true;
            if (failed) return false;
            root = new GameObject("BeaverBuddies_ColonyRoads");
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                var holder = new GameObject("Colony" + (slot + 1));
                holder.transform.parent = root.transform;
                drawers.Add(FilledDrawer(ColorOf(slot), holder) ?? _areaTileDrawerFactory.Create(ColorOf(slot), holder));
            }
            root.SetActive(false);
            return true;
        }

        /// <summary>
        /// A drawer of filled squares: the game's area drawer (a mesh built once per change) with the game's tile marker
        /// (the filled square of a building's range) instead of its outline tile. Null if the game has no marker to lend,
        /// and the outline drawer is used.
        /// </summary>
        private AreaTileDrawer FilledDrawer(Color color, GameObject holder)
        {
            try
            {
                var spec = _markerDrawerFactory._markerDrawerFactorySpec;
                Mesh mesh = spec?.TileMesh.Asset;
                Material source = spec?.TileMaterial.Asset;
                if (mesh == null || source == null) return null;
                var material = new Material(source);
                material.SetColor(ColorProperty, color);
                Vector2Int tileCount = WorldTiling.TileCount2D(_mapSize.TerrainSize.x, _mapSize.TerrainSize.y);
                var parent = new GameObject(holder.name + "Tiles");
                parent.transform.parent = holder.transform;
                // The drawer lays its squares just above the ground, which a path's own model would cover.
                parent.transform.localPosition = new Vector3(0f, AbovePaths, 0f);
                var drawer = new AreaTileDrawer(mesh, material, tileCount, parent);
                // On the layer the game draws this marker on (MeshDrawer), as it draws a building's range.
                foreach (Transform part in parent.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = Layers.UILayer;
                return drawer;
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Colony roads are drawn as outlines: " + error.Message);
                return null;
            }
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
                // Its bottom level only: a district center or stairs would otherwise get squares floating inside them.
                int bottom = int.MaxValue;
                foreach (Vector3Int tile in blockObject.PositionedBlocks.GetAllCoordinates()) bottom = Math.Min(bottom, tile.z);
                foreach (Vector3Int tile in blockObject.PositionedBlocks.GetAllCoordinates())
                {
                    if (tile.z == bottom) tiles[owner.Value].Add(tile);
                }
            }
            for (int slot = 0; slot < drawers.Count; slot++) drawers[slot].UpdateArea(tiles[slot]);
        }

        // Strong versions of the colonies' colours (StartingLocationPlayer.PLAYER_COLORS lightens them for text, which on a
        // beige path read as nearly nothing): the same hues, bright enough for grass, sand and night. Kept apart from the
        // overlay's own statics, which ask Unity for things.
        internal static class Palette
        {
            internal static readonly Color[] Roads =
            {
                new Color(1f, 0.25f, 0.36f),   // colony 1: #770018, brightened
                new Color(0.1f, 0.95f, 0.72f), // colony 2: #00775f, brightened
                new Color(0.73f, 0.4f, 1f),    // colony 3: #5f0077, brightened
                new Color(1f, 0.78f, 0.05f),   // colony 4: #f5bd02
            };
        }

        private static Color ColorOf(int slot)
        {
            Color color = slot >= 0 && slot < Palette.Roads.Length ? Palette.Roads[slot] : Color.white;
            color.a = 0.7f;
            return color;
        }
    }
}
