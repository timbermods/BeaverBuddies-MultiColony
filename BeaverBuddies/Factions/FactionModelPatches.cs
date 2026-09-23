using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.DecalSystem;
using Timberborn.DecalSystemUI;
using Timberborn.FactionSystem;
using Timberborn.ModularShafts;
using Timberborn.PathSystem;
using UnityEngine;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// Every building looks like its own faction in a mixed game (D12, D24): paths and gates, the driveways at building
    /// entrances, banners, and power shafts. Models only: nothing here reaches the simulation.
    /// </summary>
    public static class FactionModels
    {
        private static readonly string[] PathVariants = { "0000", "0010", "1010", "0011", "0111", "1111" };

        /// <summary>
        /// Paints a path's (or gate's) ground and roof pieces in a faction's materials, as DynamicPathModel does with the
        /// game's faction in Awake.
        /// </summary>
        public static void PaintPath(DynamicPathModel model, string faction)
        {
            if (!MixedFactions.IsOn || model == null) return;
            FactionSpec spec = MixedFactions.Spec(faction);
            DynamicPathModelSpec pathSpec = model._dynamicPathModelSpec;
            if (spec == null || pathSpec == null) return;
            try
            {
                Paint(model, pathSpec.GroundModelPrefix, spec.PathMaterial.Asset);
                Paint(model, pathSpec.RoofModelPrefix, spec.BaseWoodMaterial.Asset);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Factions] Could not paint a path in its faction's materials: " + error.Message);
            }
        }

        private static void Paint(DynamicPathModel model, string prefix, Material material)
        {
            if (string.IsNullOrWhiteSpace(prefix) || material == null) return;
            foreach (string variant in PathVariants)
            {
                GameObject piece = model.GameObject.FindChild(prefix + variant);
                Renderer renderer = piece != null ? piece.GetComponentInChildren<Renderer>(true) : null;
                if (renderer != null) renderer.sharedMaterial = material;
            }
        }

        /// <summary>A path's colony changed (it was stamped, or handed over): it takes that colony's faction's look.</summary>
        public static void RepaintIfPath(BaseComponent component)
        {
            if (!MixedFactions.IsOn || component == null) return;
            DynamicPathModel model = component.GetComponent<DynamicPathModel>();
            if (model != null) PaintPath(model, ColonyFactionService.DisplayFactionOf(model));
        }

        // ---- Power shafts: one set of models per faction ----

        private static readonly Dictionary<string, ModularShaftModelService> shaftModels = new Dictionary<string, ModularShaftModelService>();
        private static readonly FieldInfo UpdaterServiceField = AccessTools.Field(typeof(ModularShaftModelUpdater), "_modularShaftModelService");

        internal static void BuildOtherShaftModels(ModularShaftModelService baseService)
        {
            shaftModels.Clear();
            if (!MixedFactions.IsOn) return;
            shaftModels[MixedFactions.BaseFaction] = baseService;
            ShaftModelFactory baseModels = baseService._shaftModelFactory;
            ShaftFrameFactory baseFrames = baseModels._shaftFrameFactory;
            foreach (FactionSpec faction in MixedFactions.AllFactions)
            {
                if (faction.Id == MixedFactions.BaseFaction || FactionCatalog.Instance?.ShaftPartsOf(faction.Id) == null) continue;
                MixedFactions.ShaftBuildFaction = faction.Id;
                try
                {
                    var frames = new ShaftFrameFactory(baseFrames._rootObjectProvider, baseFrames._templateService, baseFrames._optimizedPrefabInstantiator);
                    frames.Load();
                    var models = new ShaftModelFactory(baseModels._optimizedPrefabInstantiator, frames, baseModels._templateService);
                    models.Load();
                    var service = new ModularShaftModelService(baseService._rootObjectProvider, models);
                    service.Load();
                    shaftModels[faction.Id] = service;
                }
                catch (Exception error)
                {
                    // Fallback (plan D12, Phase 4): the base faction's shafts for everyone.
                    Plugin.LogWarning($"[Factions] Could not build {faction.Id} power shafts (they use {MixedFactions.BaseFaction}'s): {error.Message}");
                }
                finally
                {
                    MixedFactions.ShaftBuildFaction = null;
                }
            }
            Plugin.Log($"[Factions] Power shaft models for {string.Join(", ", shaftModels.Keys)}");
        }

        internal static void UseFactionShaftModels(ModularShaftModelUpdater updater)
        {
            if (!MixedFactions.IsOn || shaftModels.Count == 0 || UpdaterServiceField == null) return;
            string faction = ColonyFactionService.SimFactionOf(updater);
            if (faction != null && shaftModels.TryGetValue(faction, out ModularShaftModelService service))
                UpdaterServiceField.SetValue(updater, service);
        }

        internal static ModularShaftPartsSpec ShaftParts()
        {
            string faction = MixedFactions.ShaftBuildFaction ?? MixedFactions.BaseFaction;
            return FactionCatalog.Instance?.ShaftPartsOf(faction);
        }

        // ---- Decals ----

        /// <summary>The faction a decal belongs to (null: every faction's).</summary>
        internal static string DecalFaction(DecalService service, Decal decal)
        {
            if (service?._decalCategories == null || decal.Category == null) return null;
            if (!service._decalCategories.TryGetValue(decal.Category, out DecalCategory category)) return null;
            DecalSpec spec = category.CategorySpecs.FirstOrDefault(s => s.Id == decal.Id);
            return string.IsNullOrWhiteSpace(spec?.FactionId) ? null : spec.FactionId;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, ModularShafts: ShaftFrameFactory.Load / ShaftModelFactory.Load
        ModularShaftPartsSpec single = _templateService.GetSingle<ModularShaftPartsSpec>();   (throws with two factions)
     * Each is built for one faction's parts: the base faction's by the game, the others' by FactionModels.
     */
    [HarmonyPatch(typeof(ShaftFrameFactory), nameof(ShaftFrameFactory.Load))]
    static class FactionShaftFramesPatcher
    {
        static bool Prefix(ShaftFrameFactory __instance)
        {
            if (!MixedFactions.IsOn) return true;
            ModularShaftPartsSpec parts = FactionModels.ShaftParts();
            if (parts == null) return true;
            __instance._root = __instance._rootObjectProvider.CreateRootObject("ShaftFrameFactory").transform;
            __instance._shaftBase = __instance.Instantiate(parts.ShaftBase.Asset, __instance._root);
            __instance._shaftLowerFrame = __instance.Instantiate(parts.ShaftLowerFrame.Asset, __instance._root);
            __instance._shaftSupport = __instance.Instantiate(parts.ShaftSupport.Asset, __instance._root);
            __instance._shaftFrame = __instance.Instantiate(parts.ShaftFrame.Asset, __instance._root);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShaftModelFactory), nameof(ShaftModelFactory.Load))]
    static class FactionShaftModelsPatcher
    {
        static bool Prefix(ShaftModelFactory __instance)
        {
            if (!MixedFactions.IsOn) return true;
            ModularShaftPartsSpec parts = FactionModels.ShaftParts();
            if (parts == null) return true;
            __instance._modularShaftPartsSpec = parts;
            return false;
        }
    }

    [HarmonyPatch(typeof(ModularShaftModelService), nameof(ModularShaftModelService.Load))]
    static class FactionShaftServicePatcher
    {
        static void Postfix(ModularShaftModelService __instance)
        {
            if (!MixedFactions.IsOn || MixedFactions.ShaftBuildFaction != null) return;
            FactionModels.BuildOtherShaftModels(__instance);
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, ModularShafts: ModularShaftModelUpdater.SpawnModelInstance clones its model from
     * _modularShaftModelService (a readonly field, set here after Awake): a shaft uses its own faction's models.
     */
    [HarmonyPatch(typeof(ModularShaftModelUpdater), nameof(ModularShaftModelUpdater.Awake))]
    static class FactionShaftUpdaterPatcher
    {
        static void Postfix(ModularShaftModelUpdater __instance)
        {
            if (!MixedFactions.IsOn) return;
            FactionModels.UseFactionShaftModels(__instance);
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, PathSystem: DynamicPathModel.Awake → AddModel → GetModelVariant(prefix, variant,
     * _factionService.Current.PathMaterial / BaseWoodMaterial). A gate is its template's faction; a path (one template for
     * both factions) its colony's, painted again once its colony is known (ColonyStamp).
     */
    [HarmonyPatch(typeof(DynamicPathModel), nameof(DynamicPathModel.Awake))]
    static class FactionPathModelPatcher
    {
        static void Postfix(DynamicPathModel __instance)
        {
            if (!MixedFactions.IsOn) return;
            BlockObject blockObject = __instance.GetComponent<BlockObject>();
            string faction = blockObject != null && blockObject.IsPreview
                ? ColonyFactionService.SimFactionOf(__instance) ?? ColonyFactionService.LocalFaction
                : ColonyFactionService.DisplayFactionOf(__instance);
            FactionModels.PaintPath(__instance, faction);
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, PathSystem: DrivewayModelInstantiator.InstantiateModel(drivewayModel, blockObject, ...)
        gameObject.GetComponentInChildren<Renderer>().sharedMaterial = _pathMaterial;   (the game's faction's, cached at Load)
     * A building's driveway is its own faction's path.
     */
    [HarmonyPatch(typeof(DrivewayModelInstantiator), nameof(DrivewayModelInstantiator.InstantiateModel))]
    static class FactionDrivewayPatcher
    {
        static void Postfix(BlockObject blockObject, GameObject __result)
        {
            if (!MixedFactions.IsOn || __result == null) return;
            FactionSpec spec = MixedFactions.Spec(ColonyFactionService.DisplayFactionOf(blockObject));
            Renderer renderer = __result.GetComponentInChildren<Renderer>(true);
            if (spec != null && renderer != null) renderer.sharedMaterial = spec.PathMaterial.Asset;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, DecalSystem: DecalService.Load
        where string.IsNullOrWhiteSpace(decalSpec.FactionId) || decalSpec.FactionId == _factionService.Current.Id
     * A mixed game keeps every faction's decals (ids never collide), so any saved banner resolves; each building's picker
     * and default show its own faction's.
     */
    [HarmonyPatch(typeof(DecalService), nameof(DecalService.Load))]
    static class FactionDecalServicePatcher
    {
        static bool Prefix(DecalService __instance)
        {
            if (!MixedFactions.IsOn) return true;
            __instance._decalCategories = __instance._specService.GetSpecs<DecalSpec>()
                .GroupBy(spec => spec.Category)
                .ToDictionary(group => group.Key, group => new DecalCategory(group));
            foreach (string key in __instance._decalCategories.Keys.ToList()) __instance.LoadCustomDecals(key);
            return false;
        }
    }

    [HarmonyPatch(typeof(DecalButtonContainer), nameof(DecalButtonContainer.Show))]
    static class FactionDecalPickerPatcher
    {
        static bool Prefix(DecalButtonContainer __instance, DecalSupplier decalSupplier)
        {
            if (!MixedFactions.IsOn || !(__instance._decalService is DecalService service)) return true;
            string faction = ColonyFactionService.DisplayFactionOf(decalSupplier);
            __instance.RemoveButtons();
            foreach (Decal decal in service.GetDecals(decalSupplier.Category))
            {
                string decalFaction = FactionModels.DecalFaction(service, decal);
                if (decalFaction != null && decalFaction != faction) continue;
                DecalButton button = __instance._decalButtonFactory.CreateButton(decal);
                button.Show(decalSupplier);
                __instance._decalButtons.Add(button);
                __instance._root.Add(button.Root);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(DecalSupplier), nameof(DecalSupplier.InitializeEntity))]
    static class FactionDecalDefaultPatcher
    {
        static void Prefix(DecalSupplier __instance)
        {
            if (!MixedFactions.IsOn || !__instance.ActiveDecal.IsEmpty || !(__instance._decalService is DecalService service)) return;
            string faction = ColonyFactionService.DisplayFactionOf(__instance);
            if (service._decalCategories == null || !service._decalCategories.TryGetValue(__instance.Category, out DecalCategory category)) return;
            DecalSpec first = category.CategorySpecs.FirstOrDefault(s => s.FactionId == faction)
                ?? category.CategorySpecs.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.FactionId));
            if (first != null) __instance.ActiveDecal = new Decal(first.Id, first.Category);
        }
    }
}
