using BeaverBuddies.Colonies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockObjectTools;
using Timberborn.Localization;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// The toolbar shows the local colony's faction's buildings and crops, and the common ones (D16). "Local" is the
    /// colony this computer acts as, a steward's included; dev mode shows everything, as the game does. Display only.
    /// </summary>
    public class FactionToolDisabler : IToolDisabler
    {
        private static readonly Dictionary<ITool, string> factions = new Dictionary<ITool, string>();

        public bool IsEnabled(ITool tool)
        {
            if (!MixedFactions.IsOn) return true;
            string faction = FactionOf(tool);
            return faction == null || faction == ColonyFactionService.LocalFaction;
        }

        /// <summary>The faction a tool's building or plant is alone (null: common, or not a building or plant tool).</summary>
        internal static string FactionOf(ITool tool)
        {
            if (tool == null) return null;
            if (factions.TryGetValue(tool, out string faction)) return faction;
            FactionCatalog catalog = FactionCatalog.Instance;
            if (catalog == null) return null;
            if (tool is BlockObjectTool blockObjectTool)
                faction = catalog.FactionOfTemplate(blockObjectTool.Template?.GetSpec<TemplateSpec>()?.TemplateName);
            else if (tool is PlantingTool plantingTool)
                faction = catalog.FactionOfTemplate(plantingTool.PlantableSpec?.TemplateName);
            factions[tool] = faction;
            return faction;
        }

        internal static void Reset() => factions.Clear();
    }

    /// <summary>
    /// Shows the toolbar again for the local colony's faction when it changes: a seat or steward switch, a handover, a
    /// founding or a faction switch. The game sets each button's visibility at load, on a dev-mode toggle and when a
    /// group opens; this asks the same question of every button at once.
    /// </summary>
    public class FactionToolbar : RegisteredSingleton, ILoadableSingleton
    {
        private readonly ToolButtonService _toolButtonService;
        private readonly ToolService _toolService;
        private readonly ToolGroupService _toolGroupService;
        private string shownFaction;

        public static FactionToolbar Instance => SingletonManager.GetSingleton<FactionToolbar>();

        // FactionService: loaded first, and with it the decision whether this game is mixed.
        public FactionToolbar(ToolButtonService toolButtonService, ToolService toolService, ToolGroupService toolGroupService,
            Timberborn.GameFactionSystem.FactionService factionService)
        {
            _toolButtonService = toolButtonService;
            _toolService = toolService;
            _toolGroupService = toolGroupService;
        }

        public void Load()
        {
            FactionToolDisabler.Reset();
            if (!MixedFactions.IsOn) return;
            ColonyFactionService.Changed += _ => Refresh();
        }

        /// <summary>Every tool button and group checked again; a tool or group of another faction that is open closes.</summary>
        public static void Refresh()
        {
            if (!MixedFactions.IsOn) return;
            Instance?.RefreshNow();
        }

        private void RefreshNow()
        {
            try
            {
                string faction = ColonyFactionService.LocalFaction;
                foreach (ToolButton button in _toolButtonService.ToolButtons) button.OnDevModeToggledEvent(null);
                foreach (ToolGroupButton group in _toolButtonService._toolGroupButtons) group.OnDevModeToggledEvent(null);
                ITool active = _toolService.ActiveTool;
                if (active != null && _toolButtonService.ToolButtons.Any(b => b.Tool == active && !b.ToolEnabled))
                    _toolService.SwitchToDefaultTool();
                if (_toolGroupService.ActiveToolGroup != null
                    && _toolButtonService._toolGroupButtons.Any(g => g.IsActive && !g.IsVisible))
                    _toolGroupService.ExitToolGroup();
                if (faction != shownFaction) Plugin.Log($"[Factions] The toolbar shows {faction}");
                shownFaction = faction;
                FactionDisplay.RefreshFactionIcon();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Factions] Could not refresh the toolbar for the colony's faction: " + error.Message);
            }
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, PlantingUI: PlantingToolButtonFactory.GetPlanterBuildingName(plantableSpec)
        _templateService.GetAll<PlanterBuildingSpec>().Single(b => b.PlantableResourceGroup == plantableSpec.ResourceGroup)
     * Throws with two factions loaded (each has a Farmhouse and a Forester): the planter of the plant's own faction, else
     * the base faction's, else the first. It only names the building in the "unlock ... to plant" line.
     */
    [HarmonyPatch(typeof(PlantingToolButtonFactory), "GetPlanterBuildingName")]
    static class FactionPlanterNamePatcher
    {
        static bool Prefix(PlantingToolButtonFactory __instance, PlantableSpec plantableSpec, ref string __result)
        {
            if (!MixedFactions.IsOn) return true;
            FactionCatalog catalog = FactionCatalog.Instance;
            string faction = catalog?.FactionOfTemplate(plantableSpec.TemplateName) ?? MixedFactions.BaseFaction;
            List<PlanterBuildingSpec> planters = __instance._templateService.GetAll<PlanterBuildingSpec>()
                .Where(b => b.PlantableResourceGroup == plantableSpec.ResourceGroup).ToList();
            PlanterBuildingSpec planter =
                planters.FirstOrDefault(b => catalog?.FactionOfTemplate(b.GetSpec<TemplateSpec>()?.TemplateName) == faction)
                ?? planters.FirstOrDefault(b => catalog?.FactionOfTemplate(b.GetSpec<TemplateSpec>()?.TemplateName) == MixedFactions.BaseFaction)
                ?? planters.FirstOrDefault();
            if (planter == null) return true;
            __result = __instance._loc.T(planter.GetSpec<Timberborn.EntitySystem.LabeledEntitySpec>().DisplayNameLocKey);
            return false;
        }
    }
}
