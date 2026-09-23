using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BeaversUI;
using Timberborn.BlueprintSystem;
using Timberborn.Bots;
using Timberborn.BotsUI;
using Timberborn.Characters;
using Timberborn.CharactersUI;
using Timberborn.FactionSystem;
using Timberborn.NeedSpecs;
using Timberborn.NeedSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.Wellbeing;
using Timberborn.WorkerOutfitSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace BeaverBuddies.Factions
{
    /*
     * 2026-09-22, Timberborn 1.1.2.4, NeedSystem: NeedManager.GetNeeds (from InitializeNeeds, in Awake)
        if (!HasComponent<BotSpec>()) return _factionNeedService.GetBeaverNeeds();
        return _factionNeedService.GetBotNeeds();
     * In a mixed game the faction's needs are both factions', and every bot would need both Biofuel and Energy, which
     * are critical: each character gets its own faction's (D19), the same list vanilla gives that faction.
     */
    [HarmonyPatch(typeof(NeedManager), "GetNeeds")]
    static class FactionNeedsPatcher
    {
        static bool Prefix(NeedManager __instance, ref IEnumerable<NeedSpec> __result)
        {
            if (!MixedFactions.IsOn) return true;
            FactionCatalog catalog = FactionCatalog.Instance;
            if (catalog == null) return true;
            bool bot = __instance.HasComponent<BotSpec>();
            string faction = ColonyFactionService.SimFactionOf(__instance);
            if (faction == null) FactionCreationContext.WarnOnce(bot ? "A bot" : "A beaver");
            __result = catalog.NeedsFor(faction ?? MixedFactions.BaseFaction, bot);
            return false;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Wellbeing: WellbeingLimitService.GetMaxWellbeing(WellbeingTracker)
        return !tracker.HasComponent<BotSpec>() ? MaxBeaverWellbeing : _maxBotWellbeing;   (sums of every loaded need)
     * Display and achievements only (tiers are absolute): a character's maximum is its own faction's.
     */
    [HarmonyPatch(typeof(WellbeingLimitService), nameof(WellbeingLimitService.GetMaxWellbeing), typeof(WellbeingTracker))]
    static class FactionMaxWellbeingPatcher
    {
        private static readonly Dictionary<(string, bool), int> cache = new Dictionary<(string, bool), int>();

        static bool Prefix(WellbeingTracker wellbeingTracker, ref int __result)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null) return true;
            bool bot = wellbeingTracker.HasComponent<BotSpec>();
            string faction = ColonyFactionService.SimFactionOf(wellbeingTracker) ?? MixedFactions.BaseFaction;
            if (!cache.TryGetValue((faction, bot), out int max))
            {
                max = FactionCatalog.Instance.NeedsFor(faction, bot).Sum(spec => spec.GetFavorableWellbeing());
                cache[(faction, bot)] = max;
            }
            __result = max;
            return false;
        }

        internal static void Reset() => cache.Clear();
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Beavers: BeaverTextureSetter.InitializeEntity
        FactionSpec current = _factionService.Current;
        ImmutableArray<AssetRef<Texture2D>> array = (child ? current.ChildTextures : current.Textures);
        component.SetTexture(texture: _randomNumberGenerator.GetEnumerableElement(array).Asset, propertyId: BaseMapId);
     * A beaver's fur is its own faction's. Exactly one draw from the synced random generator, as vanilla makes.
     */
    [HarmonyPatch(typeof(BeaverTextureSetter), nameof(BeaverTextureSetter.InitializeEntity))]
    static class FactionBeaverTexturePatcher
    {
        static bool Prefix(BeaverTextureSetter __instance)
        {
            if (!MixedFactions.IsOn) return true;
            FactionSpec spec = MixedFactions.Spec(__instance.GetComponent<CharacterFaction>()?.FactionId);
            if (spec == null) return true;
            CharacterMaterialModifier modifier = __instance.GetComponent<CharacterMaterialModifier>();
            Child child = __instance.GetComponent<Child>();
            var textures = child ? spec.ChildTextures : spec.Textures;
            modifier.SetTexture(BeaverTextureSetter.BaseMapId, __instance._randomNumberGenerator.GetEnumerableElement(textures).Asset);
            return false;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Bots: BotFactory.Load
        _botTemplate = _templateService.GetSingle<BotSpec>().Blueprint;   (throws with two bot templates)
     * and BotFactory.Create(position, rotation, initComponent) builds _botTemplate. A mixed game keeps every faction's
     * bot, and each bot is made from the template of the faction making it (FactionCreationContext).
     */
    [HarmonyPatch(typeof(BotFactory), nameof(BotFactory.Load))]
    static class FactionBotFactoryLoadPatcher
    {
        static bool Prefix(BotFactory __instance)
        {
            if (!MixedFactions.IsOn) return true;
            FactionCatalog catalog = FactionCatalog.Instance;
            Blueprint first = null;
            foreach (FactionSpec faction in MixedFactions.AllFactions)
            {
                Blueprint bot = catalog?.BotOf(faction.Id);
                if (bot == null) continue;
                __instance._templateInstantiator.CacheInstance(bot);
                first ??= bot;
            }
            __instance._botTemplate = catalog?.BotOf(MixedFactions.BaseFaction) ?? first;
            if (__instance._botTemplate == null) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(BotFactory), nameof(BotFactory.Create), typeof(Vector3), typeof(Quaternion), typeof(object))]
    static class FactionBotFactoryCreatePatcher
    {
        static void Prefix(BotFactory __instance)
        {
            if (!MixedFactions.IsOn) return;
            string faction = FactionCreationContext.Current;
            if (faction == null) FactionCreationContext.WarnOnce("A bot");
            Blueprint bot = FactionCatalog.Instance?.BotOf(faction ?? MixedFactions.BaseFaction);
            if (bot != null) __instance._botTemplate = bot;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, WorkerOutfitSystem: WorkerOutfitService.TryGetOutfitSpec(Worker, out spec)
        keyed (outfit id, worker type) among the specs of FactionService.Current only
     * Each worker wears its own faction's outfit: a bot of the other faction would otherwise get a hat its template
     * lacks (GetAttachmentDefinition throws).
     */
    [HarmonyPatch(typeof(WorkerOutfitService), nameof(WorkerOutfitService.TryGetOutfitSpec))]
    static class FactionWorkerOutfitPatcher
    {
        static bool Prefix(Worker worker, ref WorkerOutfitSpec workerOutfitSpec, ref bool __result)
        {
            if (!MixedFactions.IsOn || FactionCatalog.Instance == null) return true;
            workerOutfitSpec = null;
            __result = false;
            WorkplaceWorkerOutfitSpec outfit = worker.Workplace?.GetComponent<WorkplaceWorkerOutfitSpec>();
            if (outfit == null || string.IsNullOrWhiteSpace(outfit.WorkerOutfit)) return false;
            string faction = ColonyFactionService.SimFactionOf(worker) ?? MixedFactions.BaseFaction;
            __result = FactionCatalog.Instance.TryGetOutfit(faction, outfit.WorkerOutfit, worker.WorkerType, out workerOutfitSpec);
            return false;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, BeaversUI: BeaverEntityBadge.GetEntityAvatar — the faction's (contaminated) adult or
     * child avatar; BotsUI: BotEntityBadge.GetEntityAvatar — the faction's bot avatar. Each is its own faction's.
     */
    [HarmonyPatch(typeof(BeaverEntityBadge), nameof(BeaverEntityBadge.GetEntityAvatar))]
    static class FactionBeaverAvatarPatcher
    {
        static void Postfix(BeaverEntityBadge __instance, ref Sprite __result)
        {
            if (!MixedFactions.IsOn) return;
            FactionSpec spec = MixedFactions.Spec(ColonyFactionService.SimFactionOf(__instance));
            if (spec == null) return;
            bool child = (BaseComponent)(object)__instance._child;
            bool contaminated = (bool)__instance._contaminable && __instance._contaminable.IsContaminated;
            __result = contaminated
                ? (child ? spec.ContaminatedChildAvatar.Asset : spec.ContaminatedAdultAvatar.Asset)
                : (child ? spec.ChildAvatar.Asset : spec.Avatar.Asset);
        }
    }

    [HarmonyPatch(typeof(BotEntityBadge), nameof(BotEntityBadge.GetEntityAvatar))]
    static class FactionBotAvatarPatcher
    {
        static void Postfix(BotEntityBadge __instance, ref Sprite __result)
        {
            if (!MixedFactions.IsOn) return;
            FactionSpec spec = MixedFactions.Spec(ColonyFactionService.SimFactionOf(__instance));
            if (spec != null) __result = spec.BotAvatar.Asset;
        }
    }

    /// <summary>
    /// Display only: the faction of what is selected, for the empty dweller and worker slots of its panel (they show the
    /// faction's avatar, and have no character to take it from).
    /// </summary>
    public class FactionSelection : ILoadableSingleton
    {
        private readonly EventBus _eventBus;

        public static string SelectedFaction { get; private set; }

        // FactionService: loaded first, and with it the decision whether this game is mixed.
        public FactionSelection(EventBus eventBus, Timberborn.GameFactionSystem.FactionService factionService)
        {
            _eventBus = eventBus;
        }

        public void Load()
        {
            SelectedFaction = null;
            FactionMaxWellbeingPatcher.Reset();
            if (MixedFactions.IsOn) _eventBus.Register(this);
        }

        [OnEvent]
        public void OnSelected(SelectableObjectSelectedEvent selected) =>
            SelectedFaction = ColonyFactionService.DisplayFactionOf(selected.SelectableObject);

        [OnEvent]
        public void OnUnselected(SelectableObjectUnselectedEvent unselected) => SelectedFaction = null;
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, CharactersUI: CharacterButton.ShowAdultEmpty / ShowChildEmpty / ShowBotEmpty
        SetBackground(_factionService.Current.Avatar.Asset); ChangeOnClickAction();
     * An empty slot shows the avatar of the faction whose building is selected.
     */
    [HarmonyPatch(typeof(CharacterButton))]
    static class FactionEmptySlotPatcher
    {
        [HarmonyPatch(nameof(CharacterButton.ShowAdultEmpty)), HarmonyPrefix]
        static bool Adult(CharacterButton __instance) => Show(__instance, spec => spec.Avatar.Asset);

        [HarmonyPatch(nameof(CharacterButton.ShowChildEmpty)), HarmonyPrefix]
        static bool Child(CharacterButton __instance) => Show(__instance, spec => spec.ChildAvatar.Asset);

        [HarmonyPatch(nameof(CharacterButton.ShowBotEmpty)), HarmonyPrefix]
        static bool Bot(CharacterButton __instance) => Show(__instance, spec => spec.BotAvatar.Asset);

        private static bool Show(CharacterButton button, System.Func<FactionSpec, Sprite> avatar)
        {
            if (!MixedFactions.IsOn) return true;
            FactionSpec spec = MixedFactions.Spec(FactionSelection.SelectedFaction ?? ColonyFactionService.LocalFaction);
            if (spec == null) return true;
            button.SetBackground(avatar(spec));
            button.ChangeOnClickAction();
            return false;
        }
    }
}
