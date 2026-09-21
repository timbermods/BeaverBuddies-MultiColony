using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using Timberborn.InputSystem;
using Timberborn.MapStateSystem;
using Timberborn.Rendering;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Shows the border strip of a separate-colonies game on the terrain, in each colony's colour, so players can see
    /// where their land ends. Shown while any tool other than the default one is active (building, planting, cutting,
    /// demolishing), and at any time with its key. Display only: it reads the saved territory and never touches the
    /// simulation. Also handles the debug key that lets a host act as the other colony, for testing alone.
    /// </summary>
    public class ColonyBorderOverlay : IPostLoadableSingleton, IInputProcessor
    {
        public const string ToggleKeyBindingId = "BeaverBuddies.KeyBind.ToggleColonyBorder";
        public const string FlipSeatKeyBindingId = "BeaverBuddies.KeyBind.FlipColonySeat";

        // One colour per colony number; the strip on each side is drawn in its owner's colour.
        private static readonly Color[] ColonyColors =
        {
            new Color(0.25f, 0.55f, 1f, 0.6f),
            new Color(1f, 0.55f, 0.15f, 0.6f),
            new Color(0.35f, 0.85f, 0.35f, 0.6f),
            new Color(0.85f, 0.35f, 0.85f, 0.6f),
        };

        private readonly AreaTileDrawerFactory _areaTileDrawerFactory;
        private readonly ITerrainService _terrainService;
        private readonly MapSize _mapSize;
        private readonly InputService _inputService;
        private readonly EventBus _eventBus;
        private readonly ToolService _toolService;

        private GameObject root;
        private readonly List<AreaTileDrawer> drawers = new List<AreaTileDrawer>();
        private bool toggledOn;
        private bool failed;
        private bool shown;

        public ColonyBorderOverlay(AreaTileDrawerFactory areaTileDrawerFactory, ITerrainService terrainService,
            MapSize mapSize, InputService inputService, EventBus eventBus, ToolService toolService)
        {
            _areaTileDrawerFactory = areaTileDrawerFactory;
            _terrainService = terrainService;
            _mapSize = mapSize;
            _inputService = inputService;
            _eventBus = eventBus;
            _toolService = toolService;
        }

        public void PostLoad()
        {
            // A one-start game gets its border only once colony 2 is founded, so the drawers are made when first shown.
            if (!ColonyModeService.IsSeparateColonies) return;
            _inputService.AddInputProcessor(this);
            _eventBus.Register(this);
        }

        private bool EnsureDrawers(ColonyTerritory territory)
        {
            if (root != null) return true;
            if (failed) return false;
            try
            {
                root = new GameObject("BeaverBuddies_ColonyBorder");
                for (int colony = 1; colony <= territory.ColonyCount; colony++)
                {
                    var holder = new GameObject("Colony" + colony);
                    holder.transform.parent = root.transform;
                    drawers.Add(_areaTileDrawerFactory.Create(ColonyColors[(colony - 1) % ColonyColors.Length], holder));
                }
                root.SetActive(false);
                return true;
            }
            catch (Exception error)
            {
                // Seeing the border is a help, not a requirement: refusals still explain themselves.
                Plugin.LogError("[Colony] Could not create the border display: " + error);
                failed = true;
                root = null;
                return false;
            }
        }

        [OnEvent]
        public void OnToolEntered(ToolEnteredEvent toolEnteredEvent) => Refresh();

        [OnEvent]
        public void OnToolExited(ToolExitedEvent toolExitedEvent) => Refresh();

        public bool ProcessInput()
        {
            if (_inputService.IsKeyDown(ToggleKeyBindingId))
            {
                toggledOn = !toggledOn;
                Refresh();
            }
            if (_inputService.IsKeyDown(FlipSeatKeyBindingId) && ColonySession.TryFlipHostSeat())
            {
                string text = string.Format(RegisteredLocalizationService.T("BeaverBuddies.Colony.DebugSeat"), ColonySession.EffectiveHostColony);
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(text);
            }
            return false;
        }

        private void Refresh()
        {
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null || !EnsureDrawers(territory)) return;
            bool visible = toggledOn || (_toolService.ActiveTool != null && !_toolService.IsDefaultToolActive);
            if (visible == shown) return;
            shown = visible;
            // Terrain can change between showings (dynamite, terraforming), so the tiles are worked out each time.
            if (visible) Rebuild();
            root.SetActive(visible);
        }

        private void Rebuild()
        {
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null) return;
            var byColony = new List<Vector3Int>[drawers.Count];
            for (int i = 0; i < byColony.Length; i++) byColony[i] = new List<Vector3Int>();
            Vector3Int size = _mapSize.TerrainSize;
            foreach (ColonyTile tile in territory.StripTiles(size.x, size.y))
            {
                int colony = territory.OwnerOf(tile);
                if (colony < 1 || colony > byColony.Length) continue;
                // Every surface in the column: the strip applies at every height, under overhangs too.
                foreach (Vector3Int surface in _terrainService.GetAllHeightsInCell(new Vector2Int(tile.X, tile.Y)))
                    byColony[colony - 1].Add(surface);
            }
            for (int i = 0; i < drawers.Count; i++) drawers[i].UpdateArea(byColony[i]);
        }
    }
}
