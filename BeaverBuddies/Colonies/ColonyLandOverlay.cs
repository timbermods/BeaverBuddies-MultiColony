using BeaverBuddies.Editor;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using Timberborn.InputSystem;
using Timberborn.Rendering;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Draws each colony's land on the map: the outline of every colony's land in its colour, so players see where
    /// they may build and where their land meets a neighbour's (the place for a trading post). Shown while any tool
    /// other than the default one is in hand (building, planting, cutting, demolishing, founding), and at any time
    /// with its key. Display only: it reads <see cref="ColonyReach"/> and never touches the simulation.
    ///
    /// Also listens for the debug key that lets a host act as the next colony, for testing alone.
    /// </summary>
    public class ColonyLandOverlay : IPostLoadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        public const string ToggleKeyBindingId = "BeaverBuddies.KeyBind.ToggleColonyLand";
        public const string FlipSeatKeyBindingId = "BeaverBuddies.KeyBind.FlipColonySeat";

        private const float RedrawInterval = 0.5f;

        private readonly AreaTileDrawerFactory _areaTileDrawerFactory;
        private readonly ITerrainService _terrainService;
        private readonly InputService _inputService;
        private readonly ToolService _toolService;

        private GameObject root;
        private readonly List<AreaTileDrawer> drawers = new List<AreaTileDrawer>();
        private bool failed, toggledOn, shown;
        private int drawnVersion = -1;
        private float nextRedraw;

        public ColonyLandOverlay(AreaTileDrawerFactory areaTileDrawerFactory, ITerrainService terrainService,
            InputService inputService, ToolService toolService)
        {
            _areaTileDrawerFactory = areaTileDrawerFactory;
            _terrainService = terrainService;
            _inputService = inputService;
            _toolService = toolService;
        }

        public void PostLoad() => _inputService.AddInputProcessor(this);

        public bool ProcessInput()
        {
            if (_inputService.IsKeyDown(ToggleKeyBindingId)) toggledOn = !toggledOn;
            if (_inputService.IsKeyDown(FlipSeatKeyBindingId) && ColonySession.TryFlipHostSeat())
            {
                string text = string.Format(RegisteredLocalizationService.T("BeaverBuddies.Colony.DebugSeat"), ColonySession.LocalSlot + 1);
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(text, warning: false);
                drawnVersion = -1;
            }
            return false;
        }

        public void UpdateSingleton()
        {
            try
            {
                bool wanted = ColonyModeService.IsSeparateColonies && ColonyReach.Instance != null
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
                    nextRedraw = 0;
                }
                // Land changes as colonies build; drawn again at most twice a second.
                if (ColonyReach.Instance.Version == drawnVersion || Time.unscaledTime < nextRedraw) return;
                nextRedraw = Time.unscaledTime + RedrawInterval;
                drawnVersion = ColonyReach.Instance.Version;
                Redraw();
            }
            catch (Exception error)
            {
                // Seeing the land is a help, not a requirement: previews and refusals still explain themselves.
                if (!failed) Plugin.LogError("[Colony] Could not draw colony land: " + error);
                failed = true;
                root?.SetActive(false);
            }
        }

        private bool EnsureDrawers()
        {
            if (root != null) return true;
            if (failed) return false;
            root = new GameObject("BeaverBuddies_ColonyLand");
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
            for (int slot = 0; slot < drawers.Count; slot++)
            {
                var tiles = new List<Vector3Int>();
                foreach (var (x, y) in ColonyReach.Instance.BorderTiles(slot))
                {
                    // Every surface in the column, so the line shows on terraces and under overhangs too.
                    foreach (Vector3Int surface in _terrainService.GetAllHeightsInCell(new Vector2Int(x, y)))
                        tiles.Add(surface);
                }
                drawers[slot].UpdateArea(tiles);
            }
        }

        private static Color ColorOf(int slot)
        {
            Color color = slot < StartingLocationPlayer.PLAYER_COLORS.Length ? StartingLocationPlayer.PLAYER_COLORS[slot] : Color.white;
            color.a = 0.75f;
            return color;
        }
    }
}
