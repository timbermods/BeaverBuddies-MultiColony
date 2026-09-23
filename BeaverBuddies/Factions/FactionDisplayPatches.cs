using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Timberborn.AutomationBuildingsUI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.DistributionSystemBatchControl;
using Timberborn.DistributionSystemUI;
using Timberborn.EntitySystem;
using Timberborn.FactionSystem;
using Timberborn.GameOverUI;
using Timberborn.GameSound;
using Timberborn.GameWonderCompletion;
using Timberborn.GameWonderCompletionUI;
using Timberborn.GoodStatisticsBatchControl;
using Timberborn.GoodsUI;
using Timberborn.StockpilesUI;
using Timberborn.TutorialSystem;
using Timberborn.WellbeingUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// What a player sees follows their colony's faction (D24): the top left faction icon, the population's wellbeing
    /// box, lists of goods, the game-over and Wonder screens. Display only.
    /// </summary>
    public static class FactionDisplay
    {
        private static Image factionIcon;

        internal static void KeepFactionIcon(Image icon) => factionIcon = icon;

        /// <summary>The top left faction icon shows the local colony's faction.</summary>
        public static void RefreshFactionIcon()
        {
            if (!MixedFactions.IsOn || factionIcon == null) return;
            FactionSpec spec = MixedFactions.Spec(ColonyFactionService.LocalFaction);
            if (spec != null) factionIcon.sprite = spec.Logo.Asset;
        }

        /// <summary>The goods of the faction this computer's colony plays.</summary>
        internal static HashSet<string> LocalGoods() => FactionCatalog.Instance?.GoodsOf(ColonyFactionService.LocalFaction);

        internal static void Reset() => factionIcon = null;
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, WellbeingUI: BasicStatisticsPanelFactory.Create
        visualElement.Q<Image>("FactionIcon").sprite = _factionService.Current.Logo.Asset;
     */
    [HarmonyPatch(typeof(BasicStatisticsPanelFactory), nameof(BasicStatisticsPanelFactory.Create))]
    static class FactionStatisticsIconPatcher
    {
        static void Postfix(VisualElement __result)
        {
            if (!MixedFactions.IsOn || __result == null) return;
            FactionDisplay.KeepFactionIcon(__result.Q<Image>("FactionIcon"));
            FactionDisplay.RefreshFactionIcon();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, WellbeingUI: PopulationWellbeingBox — one counter per beaver need of every loaded
     * faction, built once. Opened, it shows the needs of the local colony's faction.
     */
    [HarmonyPatch(typeof(PopulationWellbeingBox), nameof(PopulationWellbeingBox.GetPanel))]
    static class FactionWellbeingBoxPatcher
    {
        static void Postfix(PopulationWellbeingBox __instance)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null) return;
            string faction = ColonyFactionService.LocalFaction;
            foreach (PopulationWellbeingCounter counter in __instance._counters)
                counter._root.ToggleDisplayStyle(FactionCatalog.Instance.HasNeed(faction, counter.NeedId));
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, DistributionSystemBatchControl: DistributionSettingGroupFactory.CreateItems — one row
     * per good of the group. A district's distribution lists its colony's faction's goods.
     */
    [HarmonyPatch(typeof(DistributionSettingGroupFactory), "CreateItems")]
    static class FactionDistributionItemsPatcher
    {
        static bool Prefix(DistributionSettingGroupFactory __instance, DistrictDistributionSetting districtDistributionSetting,
            string groupId, VisualElement parent, ref List<GoodDistributionSettingItem> __result)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null) return true;
            HashSet<string> goods = FactionCatalog.Instance.GoodsOf(ColonyFactionService.DisplayFactionOf(districtDistributionSetting));
            var list = new List<GoodDistributionSettingItem>();
            DistrictDistributableGoodProvider provider = districtDistributionSetting.GetComponent<DistrictDistributableGoodProvider>();
            foreach (GoodDistributionSetting setting in districtDistributionSetting.GetGoodDistributionSettingsForGroup(groupId))
            {
                if (!goods.Contains(setting.GoodId)) continue;
                GoodDistributionSettingItem item = __instance._goodDistributionSettingItemFactory.Create(provider, setting);
                list.Add(item);
                parent.Add(item.Root);
            }
            __result = list;
            return false;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, DistributionSystemUI: ImportGoodIconFactory.CreateImportGoodIcon(parent, goodId) —
     * a District Crossing's import icons, one per good, made once. Shown for the crossing's own faction's goods.
     */
    [HarmonyPatch(typeof(ImportGoodIconFactory), nameof(ImportGoodIconFactory.CreateImportGoodIcon))]
    static class FactionImportIconPatcher
    {
        internal static readonly ConditionalWeakTable<ImportGoodIcon, VisualElement> Roots = new ConditionalWeakTable<ImportGoodIcon, VisualElement>();

        static void Postfix(VisualElement parent, ImportGoodIcon __result)
        {
            if (!MixedFactions.IsOn || parent == null || parent.childCount == 0 || __result == null) return;
            Roots.Remove(__result);
            Roots.Add(__result, parent[parent.childCount - 1]);
        }
    }

    [HarmonyPatch(typeof(DistrictCrossingFragment), nameof(DistrictCrossingFragment.ShowFragment))]
    static class FactionImportIconsShowPatcher
    {
        static void Postfix(DistrictCrossingFragment __instance, BaseComponent entity)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null || entity == null) return;
            HashSet<string> goods = FactionCatalog.Instance.GoodsOf(ColonyFactionService.DisplayFactionOf(entity));
            foreach (ImportGoodIcon icon in __instance._importGoodIcons)
            {
                if (FactionImportIconPatcher.Roots.TryGetValue(icon, out VisualElement root))
                    root.ToggleDisplayStyle(goods.Contains(icon._goodId));
            }
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, GoodStatisticsBatchControl: GoodStatisticsGroupFactory.CreateItems(groupId, parent,
     * registry) — a row per good of the group (made each time the tab opens). The local colony's faction's goods.
     */
    [HarmonyPatch(typeof(GoodStatisticsGroupFactory), "CreateItems")]
    static class FactionGoodStatisticsPatcher
    {
        static bool Prefix(GoodStatisticsGroupFactory __instance, string groupId, VisualElement parent,
            Timberborn.GoodsSampling.GoodSamplingRegistry goodSamplingRegistry, ref IEnumerable<GoodStatisticsBatchControlItem> __result)
        {
            if (!MixedFactions.IsOn) return true;
            HashSet<string> goods = FactionDisplay.LocalGoods();
            if (goods == null) return true;
            __result = Items(__instance, groupId, parent, goodSamplingRegistry, goods).ToList();
            return false;
        }

        private static IEnumerable<GoodStatisticsBatchControlItem> Items(GoodStatisticsGroupFactory factory, string groupId,
            VisualElement parent, Timberborn.GoodsSampling.GoodSamplingRegistry registry, HashSet<string> goods)
        {
            foreach (string good in factory._goodService.GetGoodsForGroup(groupId))
            {
                if (!goods.Contains(good)) continue;
                GoodStatisticsBatchControlItem item = factory._goodStatisticsBatchControlItemFactory.Create(registry.GetGoodSampleHistory(good));
                parent.Add(item.Root);
                yield return item;
            }
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, StockpilesUI: GoodStockpilesTooltipFactory.AddIcons(parent, goodType) — a good's
     * tooltip shows every stockpile that can hold its type. The local colony's faction's stockpiles (and common ones).
     */
    [HarmonyPatch(typeof(GoodStockpilesTooltipFactory), "AddIcons")]
    static class FactionStockpileTooltipPatcher
    {
        static bool Prefix(GoodStockpilesTooltipFactory __instance, VisualElement parent, string goodType)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null) return true;
            string local = ColonyFactionService.LocalFaction;
            if (!__instance._templates.TryGetValue(goodType, out var templates)) return false;
            foreach (LabeledEntitySpec item in templates)
            {
                string faction = FactionCatalog.Instance.FactionOfTemplate(item.GetSpec<Timberborn.TemplateSystem.TemplateSpec>()?.TemplateName);
                if (faction != null && faction != local) continue;
                var image = new Image { name = item.DisplayNameLocKey, sprite = item.Icon.Asset };
                image.AddToClassList(GoodStockpilesTooltipFactory.IconClass);
                parent.Add(image);
            }
            return false;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, AutomationBuildingsUI: ResourceCounterGoodsDropdownProvider.InitializeEntity
        Items = _goodService.Goods.OrderBy(...)
     * A resource counter offers its colony's faction's goods.
     */
    [HarmonyPatch(typeof(ResourceCounterGoodsDropdownProvider), nameof(ResourceCounterGoodsDropdownProvider.InitializeEntity))]
    static class FactionResourceCounterPatcher
    {
        static void Postfix(ResourceCounterGoodsDropdownProvider __instance)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null) return;
            HashSet<string> goods = FactionCatalog.Instance.GoodsOf(ColonyFactionService.DisplayFactionOf(__instance));
            AccessTools.PropertySetter(typeof(ResourceCounterGoodsDropdownProvider), nameof(ResourceCounterGoodsDropdownProvider.Items))
                ?.Invoke(__instance, new object[] { __instance.Items.Where(goods.Contains).ToImmutableArray() });
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, GameOverUI: GameOverBox.Load sets the faction's game-over flavour and message once.
     * Shown when the game ends, in the words of the local colony's faction.
     */
    [HarmonyPatch(typeof(GameOverBox), nameof(GameOverBox.OnGameOverEvent))]
    static class FactionGameOverPatcher
    {
        static void Prefix(GameOverBox __instance)
        {
            if (!MixedFactions.IsOn || __instance._root == null) return;
            FactionSpec spec = MixedFactions.Spec(ColonyFactionService.LocalFaction);
            if (spec == null) return;
            __instance._root.Q<Label>("Flavor").text = spec.GameOverFlavor.Value;
            __instance._root.Q<Label>("Info").text = spec.GameOverMessage.Value;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, GameWonderCompletion: GameWonderCompletionService.CompleteWonder and
     * IsWonderCompletedWithCurrentFaction record the map's mastery for FactionService.Current in this player's profile.
     * Each player's profile records their own colony's faction.
     */
    [HarmonyPatch(typeof(GameWonderCompletionService), nameof(GameWonderCompletionService.CompleteWonder))]
    static class FactionWonderCompletionPatcher
    {
        static bool Prefix(GameWonderCompletionService __instance)
        {
            if (!MixedFactions.IsOn) return true;
            string faction = ColonyFactionService.LocalFaction;
            if (!__instance._mapNameService.HasMapName || Completed(__instance, faction)) return false;
            bool anyFaction = __instance.IsWonderCompletedWithAnyFaction();
            __instance.WasCompletedFirstTimeForFaction = anyFaction;
            __instance.WasCompletedFirstTimeForMap = !anyFaction;
            __instance._wonderCompletionService.CompleteWonder(__instance._mapNameService.Name, __instance._mapNameService.IsResource, faction);
            return false;
        }

        private static bool Completed(GameWonderCompletionService service, string faction) =>
            service._wonderCompletionService.GetWonderCompletionFactionIds(service._mapNameService.Name, service._mapNameService.IsResource)
                .Contains(faction);
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, GameWonderCompletionUI: WonderCompletionPanel.ShowMainSection / ShowMapPanel show the
     * Current faction's Wonder picture, words and logo. The local colony's faction's.
     */
    [HarmonyPatch(typeof(WonderCompletionPanel))]
    static class FactionWonderPanelPatcher
    {
        [HarmonyPatch("ShowMainSection"), HarmonyPostfix]
        static void MainSection(WonderCompletionPanel __instance)
        {
            if (!MixedFactions.IsOn) return;
            FactionSpec faction = MixedFactions.Spec(ColonyFactionService.LocalFaction);
            FactionWonderSpec spec = faction?.GetSpec<FactionWonderSpec>();
            if (spec == null) return;
            __instance._root.Q<Label>("Flavor").text = spec.WonderCompletionFlavor.Value;
            __instance._root.Q<Label>("Congratulations").text = spec.WonderCompletionMessage.Value;
            __instance._root.Q<Image>("WonderCompletionImage").style.backgroundImage = new StyleBackground(spec.WonderCompletionImage.Asset);
        }

        [HarmonyPatch("ShowMapPanel"), HarmonyPostfix]
        static void MapPanel(WonderCompletionPanel __instance)
        {
            if (!MixedFactions.IsOn) return;
            FactionSpec faction = MixedFactions.Spec(ColonyFactionService.LocalFaction);
            if (faction == null || __instance._mapMasteryFactionIcon == null) return;
            __instance._mapMasteryFactionIcon.style.backgroundImage = new StyleBackground(faction.Logo.Asset);
            __instance._mapMasteryLabel.text = __instance._loc.T(WonderCompletionPanel.WonderCompletedLocKey, faction.DisplayName.Value);
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, WondersUI: WonderFragment.ActivateWonder → GameUISoundController.PlayWonderLaunchSound
     * (the Current faction's launch sound). The Wonder is the selected entity (its panel's button or key launched it), so
     * the sound is the selected Wonder's faction's. ActivateWonder itself is left to its recording patch alone.
     */
    [HarmonyPatch(typeof(GameUISoundController), nameof(GameUISoundController.PlayWonderLaunchSound))]
    static class FactionWonderSoundPatcher
    {
        static bool Prefix(GameUISoundController __instance)
        {
            if (!MixedFactions.IsOn) return true;
            FactionWonderSpec spec = MixedFactions.Spec(FactionSelection.SelectedFaction ?? ColonyFactionService.LocalFaction)
                ?.GetSpec<FactionWonderSpec>();
            if (spec == null) return true;
            __instance.PlaySound2D(spec.WonderLaunchSound);
            return false;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, TutorialSystem: TutorialService.GetConfigurations and TutorialTriggers.Load run only
     * for a starting faction (Folktails), and the tutorial's steps name Folktails buildings. Off in a mixed game (D24).
     */
    [HarmonyPatch(typeof(TutorialService), "GetConfigurations")]
    static class FactionTutorialConfigurationsPatcher
    {
        static bool Prefix() => !MixedFactions.IsOn;
    }

    [HarmonyPatch(typeof(TutorialTriggers), nameof(TutorialTriggers.Load))]
    static class FactionTutorialTriggersPatcher
    {
        static bool Prefix(TutorialTriggers __instance)
        {
            if (!MixedFactions.IsOn) return true;
            __instance._canTrigger = false;
            return false;
        }
    }
}
