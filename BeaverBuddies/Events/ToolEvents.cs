using BeaverBuddies.Colonies;
using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.Coordinates;
using Timberborn.DemolishingUI;
using Timberborn.DuplicationSystem;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.PlantingUI;
using Timberborn.ScienceSystem;
using Timberborn.ScienceSystemUI;
using Timberborn.TerrainQueryingSystem;
using Timberborn.TemplateInstantiation;
using Timberborn.ToolButtonSystem;
using Timberborn.WorkSystemUI;
using UnityEngine;

namespace BeaverBuddies.Events
{

    [Serializable]
    class BuildingPlacedEvent : ReplayEvent
    {
        // The duplication source may be anyone's; the footprint decides.
        public override ColonyScope GetColonyScope() => ColonyScope.Place(new ColonyPlacement
        {
            TemplateName = prefabName,
            X = coordinates.x,
            Y = coordinates.y,
            Z = coordinates.z,
            Orientation = (int)orientation,
            IsFlipped = isFlipped,
        });

        public string prefabName;
        public Vector3Int coordinates;
        public Orientation orientation;
        public bool isFlipped;
        public string duplicationSourceID;
        /// <summary>
        /// Whether the building could still be placed when the host played this: written by the host (the event is
        /// sent on after it is played) and read by guests, which then place or skip without checking again. Null until
        /// the host has played it.
        /// </summary>
        public bool? placed;

        public override void Replay(IReplayContext context)
        {
            var buildingSpec = GetBuilding(context, prefabName);
            var blockObjectSpec = buildingSpec.GetSpec<BlockObjectSpec>();
            var placer = context.GetSingleton<BlockObjectPlacerService>().GetMatchingPlacer(blockObjectSpec);
            Placement placement = new Placement(coordinates, orientation,
                isFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
            if (!MayPlace(context, placement, buildingSpec))
            {
                Plugin.LogWarning($"Invalid placement for {prefabName} at {coordinates}");
                return;
            }

            BaseComponent duplicationSource = null;
            if (!string.IsNullOrEmpty(duplicationSourceID))
            {
                duplicationSource = GetEntityComponent(context, duplicationSourceID);
            }

            EntitySetup.Builder builder = new EntitySetup.Builder(buildingSpec.Blueprint);
            if ((bool)duplicationSource)
            {
                builder.AddInitComponent(new DuplicationInit(duplicationSource));
            }
            // In a separate-colonies game a building belongs to whoever placed it: the slot was written into the event by
            // the host, so every computer gives it the same owner. A shared game gives it none, as the Stability Fork.
            if (Colonies.ColonyModeService.IsSeparateColonies) Colonies.DistrictOwner.PendingSlot = System.Math.Max(0, slot);
            try
            {
                placer.Place(builder, placement);
            }
            finally
            {
                Colonies.DistrictOwner.PendingSlot = null;
            }
        }

        /// <summary>
        /// The check that the spot is still free and allowed, made once, by the host, at the tick the placement is
        /// played: the answer is written into the event (<see cref="placed"/>) and the guests take it. Every computer
        /// checked for itself before, by making and destroying a whole copy of the building each time (the game's
        /// check needs an instance), so a guest paid that for every building coming back from the host, on top of
        /// placing it: a dragged path of thirty tiles was thirty copies made and thrown away in one frame. Both
        /// computers hold the same world at that tick, so the host's answer is the guest's, and if it were not, the
        /// game's own placing throws here and the session stops with a message, instead of one computer placing and
        /// the other silently skipping.
        /// </summary>
        private bool MayPlace(IReplayContext context, Placement placement, BuildingSpec spec)
        {
            // A guest takes the host's answer, which the host writes as it plays the event, before sending it on. A
            // guest given an event without one checks for itself as before.
            if (placed.HasValue && EventIO.Get() is ClientEventIO) return placed.Value;
            // A district center gets the block check only (below): its preview copy interferes with the nav mesh. It
            // used to get no check at all, so two placed on the same tiles in one tick threw and stopped the session.
            bool valid = IsPlacementValid(context, placement, spec, blocksOnly: prefabName != null && prefabName.StartsWith("DistrictCenter."));
            placed = valid;
            return valid;
        }

        // Note: This may not catch every possible invalid placement (e.g. if terrain height changes or something)
        // but I think it should catch the vast majority of cases due to double placement.
        // Where the check's throwaway copies are made. Made once per game instead of asking the scene for all its root
        // objects at every replayed placement (Unity destroys it with the scene, and it is made again).
        private static GameObject checkParent;

        private static readonly Colonies.ColonyProfiler.Spot PlacementChecks = Colonies.ColonyProfiler.Declare("Placement checks in replays");

        private static bool IsPlacementValid(IReplayContext context, Placement placement, BuildingSpec spec, bool blocksOnly = false)
        {
            long started = Colonies.ColonyProfiler.Start();
            try
            {
                return IsPlacementValidTimed(context, placement, spec, blocksOnly);
            }
            finally
            {
                Colonies.ColonyProfiler.Stop(PlacementChecks, started);
            }
        }

        private static bool IsPlacementValidTimed(IReplayContext context, Placement placement, BuildingSpec spec, bool blocksOnly)
        {
            // The blocks themselves first (the spot was taken since the click, the usual reason): the game's own
            // check, from the spec, without an instance. Only a placement that passes it needs the full check below.
            if (!context.GetSingleton<BlockValidator>().BlocksValid(spec.GetSpec<BlockObjectSpec>(), placement)) return false;
            if (blocksOnly) return true;
            var templateInstantiator = context.GetSingleton<TemplateInstantiator>();
            if (checkParent == null) checkParent = new GameObject("BeaverBuddies_PlacementChecks");
            // It's a bit wasteful to instantiate the object just to check if it's valid,
            // but this is likely the best choice because:
            // 1) This only happens occasionally, based on UI actions, and
            // 2) There's no easy way to get at the cache of previews the UI uses,
            //    and each blueprint requires a different GameObject, so we can't cache just one.
            // TODO: Check if this is still the case with the new blueprint system.
            // The copy's components wake up (Awake) as it is made. The game's own draw no game random numbers there, but
            // another mod's building might, and only the host makes this copy (the guests take its answer): the random
            // state is put back as it was, whatever the copy drew, so the host's stays the guests'. Not
            // DeterminismService.GetNonGameRandom: Guid.NewGuid and direct UnityEngine.Random calls draw from this state
            // whatever it says. (Destroy runs OnDestroy later, outside this; none of the game's draws there.)
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            GameObject gameObject = null;
            try
            {
                gameObject = templateInstantiator.Instantiate(spec.Blueprint, checkParent.transform);
                gameObject.SetActive(value: false);
                var blockObject = gameObject.GetComponentSlow<BlockObject>();
                blockObject.MarkAsPreviewAndInitialize();
                blockObject.Reposition(placement);
                // The game's own check. Its DistrictPreviewsValidator, which reads the host's own tool previews, passes while a
                // replay runs (DistrictPreviewsValidatorReplayPatcher), so what the host hovers never refuses a played
                // placement (checked again in the 1.4.0-rc1 review, E-6).
                return blockObject.IsValid();
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
            }
        }

        public override string ToActionString()
        {
            return $"Placing {prefabName}, {coordinates}, {orientation}, {isFlipped}";
        }
    }

    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.Place))]
    class PlacePatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(EntitySetup.Builder entitySetupBuilder, Placement placement)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                string prefabName = ReplayEvent.GetBuildingName(entitySetupBuilder);
                // If there's a duplication source, get the source's EntityID
                var dupInit = (DuplicationInit)entitySetupBuilder._initComponents.Find(c => c is DuplicationInit);
                string duplicationSourceID = dupInit == null ? null : ReplayEvent.GetEntityID(dupInit.DuplicationSource);
                if (!string.IsNullOrEmpty(duplicationSourceID))
                {
                    Plugin.Log($"Found duplication source: {duplicationSourceID} for {prefabName}");
                }

                return new BuildingPlacedEvent()
                {
                    prefabName = prefabName,
                    coordinates = placement.Coordinates,
                    orientation = placement.Orientation,
                    isFlipped = placement.FlipMode.IsFlipped,
                    duplicationSourceID = duplicationSourceID,
                };
            });
        }
    }

    class BuildingsDeconstructedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.EntityList(entityIDs, id => id, demolition: true);

        public List<string> entityIDs = new List<string>();

        public override void Replay(IReplayContext context)
        {
            var entityService = context.GetSingleton<EntityService>();
            foreach (string entityID in entityIDs)
            {
                var entity = GetEntityComponent(context, entityID);
                if (entity == null) continue;
                entityService.Delete(entity);
            }
        }

        public override string ToActionString()
        {
            return $"Deconstructing: {string.Join(", ", entityIDs)}";
        }
    }

    [HarmonyPatch(typeof(BlockObjectDeletionTool<BuildingSpec>), nameof(BlockObjectDeletionTool<BuildingSpec>.DeleteBlockObjects))]
    class BuildingDeconstructionPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(BlockObjectDeletionTool<BuildingSpec> __instance)
        {
            bool result = ReplayEvent.DoPrefix(() =>
            {
                // TODO: If this does work, it may affect other deletions too :(
                List<string> entityIDs = __instance._temporaryBlockObjects
                        .Select(ReplayEvent.GetEntityID)
                        .ToList();

                return new BuildingsDeconstructedEvent()
                {
                    entityIDs = entityIDs,
                };
            });

            if (!result)
            {
                // If we cancel the event, clean up the tool. The terrain it picked too (terrain held up by what is
                // deleted, G9): left there it piled up with every deletion, raised the view's level to its old heights
                // (the tool's SetVisibleLayerToShowAllObjects) and, the first time the game's own method ran again on this
                // computer (a session that ended), was all destroyed at once (1.4.0-rc1, X4 and E-4).
                __instance._temporaryBlockObjects.Clear();
                __instance._temporaryTerrainCoords.Clear();
            }

            return result;
        }
    }

    [Serializable]
    class PlantingAreaMarkedEvent : ReplayEvent
    {
        // Marking plants anywhere (there is no land); a tile another colony marked stays theirs, and unmarking only ever
        // removes the actor's own marks (both checked tile by tile when it is played, see ColonyMarks).
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public List<Vector3Int> inputBlocks;
        public Ray ray;
        public string prefabName;
        // The tiles to mark, levelled by the player who marked them. The game levels the dragged area with its terrain
        // picker, which stops at the layer each player has sliced the view to: played again on another computer, the
        // same ray could give another height and the marks would differ (a desync). Null in events from before.
        public List<Vector3Int> coordinates;

        public const string UNMARK = "Unmark";

        public override void Replay(IReplayContext context)
        {
            // A plant this game does not have: the host refuses one (ColonyRulesService.AllowOnHost), so only a guest
            // meets it, when the host marks a crop from a mod this guest does not run. Found before anything is marked,
            // so this guest leaves quietly and the others play on (ReplayService), as for a building (E-1).
            if (NamesUnknownPlant(context))
                throw new MissingContentException($"The host marked {prefabName} for planting, which this game does not have " +
                    "(it comes from a mod that is not installed here).");
            var plantingService = context.GetSingleton<PlantingSelectionService>();
            List<Vector3Int> leveled = coordinates
                ?? LevelAbove(inputBlocks, plantingService._terrainAreaService._terrainService.OnGround);
            // Separate colonies: every mark made or removed is the actor's colony's (see ColonyMarks).
            if (Colonies.ColonyModeService.IsSeparateColonies) Colonies.ColonyMarks.ActingSlot = System.Math.Max(0, slot);
            PlantingLeveledCoordinatesPatcher.Recorded = leveled;
            try
            {
                if (prefabName == UNMARK)
                {
                    plantingService.UnmarkArea(inputBlocks, ray);
                }
                else
                {
                    plantingService.MarkArea(inputBlocks, ray, prefabName);
                }
            }
            finally
            {
                PlantingLeveledCoordinatesPatcher.Recorded = null;
                Colonies.ColonyMarks.ActingSlot = null;
            }
        }

        /// <summary>
        /// Whether this marks a plant this game does not know: a crop from a mod that only another player runs (the mod
        /// lists only warn when they differ). The game's planting check looks the plant up by name for the first free
        /// tile (SpawnValidationService.IsUnobstructed, TemplateNameMapper.GetTemplate) and throws for an unknown one,
        /// which inside a replay stopped the session for everyone. Unmarking names no plant. False when it can't tell.
        /// </summary>
        internal bool NamesUnknownPlant(IReplayContext context)
        {
            if (prefabName == UNMARK) return false;
            if (string.IsNullOrEmpty(prefabName)) return true;
            var names = context?.GetSingleton<PlantingSelectionService>()?._plantingAreaValidator?._spawnValidationService?._templateNameMapper;
            return names != null && !names.TryGetTemplate(prefabName, out _);
        }

        /// <summary>
        /// An event from before <see cref="coordinates"/>, levelled without the view. The planting tool drags its
        /// rectangle on the level of the terrain it picked first, and the game marks one level above that, where the
        /// ground is: so the marker's own levelling gave the tile above each dragged block that stands on ground.
        /// </summary>
        internal static List<Vector3Int> LevelAbove(IEnumerable<Vector3Int> inputBlocks, Func<Vector3Int, bool> onGround) =>
            (inputBlocks ?? Enumerable.Empty<Vector3Int>())
                .Select(block => new Vector3Int(block.x, block.y, block.z + 1))
                .Where(onGround)
                .ToList();

        public override string ToActionString()
        {
            return $"Planting {(coordinates ?? inputBlocks)?.Count ?? 0} of {prefabName}";
        }

        internal static PlantingAreaMarkedEvent Record(PlantingSelectionService service, IEnumerable<Vector3Int> inputBlocks,
            Ray ray, string prefabName)
        {
            var blocks = new List<Vector3Int>(inputBlocks);
            return new PlantingAreaMarkedEvent()
            {
                prefabName = prefabName,
                ray = ray,
                // The game only levels the dragged blocks, and the replay marks the levelled tiles below instead
                // (PlantingLeveledCoordinatesPatcher), so the blocks themselves are not sent: an area is hundreds of
                // tiles, and this halves what every computer serializes, sends and reads for it.
                inputBlocks = new List<Vector3Int>(),
                // Levelled here, with this player's view: the tiles they saw highlighted.
                coordinates = service._terrainAreaService.InMapLeveledCoordinates(blocks, ray).ToList(),
            };
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.MarkArea))]
    class PlantingAreaMarkedPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(PlantingSelectionService __instance, IEnumerable<Vector3Int> inputBlocks, Ray ray, string templateName)
        {
            return ReplayEvent.DoPrefix(() => PlantingAreaMarkedEvent.Record(__instance, inputBlocks, ray, templateName));
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.UnmarkArea))]
    class PlantingAreaUnmarkedPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(PlantingSelectionService __instance, IEnumerable<Vector3Int> inputBlocks, Ray ray)
        {
            return ReplayEvent.DoPrefix(() =>
                PlantingAreaMarkedEvent.Record(__instance, inputBlocks, ray, PlantingAreaMarkedEvent.UNMARK));
        }
    }

    // While a planting event is played, the game's own MarkArea / UnmarkArea act on the tiles the event carries instead
    // of levelling the area again with this computer's view (see PlantingAreaMarkedEvent.coordinates). Everything else
    // they do (which tiles may be planted, the colony checks on each mark) runs as in the game. Outside a replay it does
    // nothing, so the tools' highlighting and every other caller level as before. Priority.Last, the rule for a prefix
    // that replaces the original (a prefix that records an action runs first instead, see ReplayEvent.DoPrefix):
    // another mod's prefix on the levelling runs first. If that prefix skips the original
    // itself, Harmony skips this one too and the replay uses that mod's tiles instead, so a mod that replaces this
    // levelling needs a lockstep review (none is known to).
    [HarmonyPatch(typeof(TerrainAreaService), nameof(TerrainAreaService.InMapLeveledCoordinates))]
    static class PlantingLeveledCoordinatesPatcher
    {
        internal static List<Vector3Int> Recorded;

        [HarmonyPriority(Priority.Last)]
        static bool Prefix(ref IEnumerable<Vector3Int> __result)
        {
            if (Recorded == null) return true;
            __result = Recorded.ToList();
            return false;
        }
    }

    [Serializable]
    class ClearResourcesMarkedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.EntityList(blocks, id => id.ToString(), demolition: markForDemolition);

        public List<Guid> blocks;
        public Vector3Int start;
        public Vector3Int end;
        public bool markForDemolition;

        public override void Replay(IReplayContext context)
        {
            // The selection was made against the sender's state a few ticks ago,
            // so some entities may already be gone here (e.g. builders finished
            // demolishing an object the same area selection also covered).
            // Skip those instead of failing the whole session; both sides
            // replay at the same tick, so they skip the same entities.
            var blockObjects = ResolveBlockObjects(context, blocks);
            if (blocks.Count > 0 && blockObjects.Count == 0)
            {
                Plugin.LogWarning($"Skipping {ToActionString()}: none of the {blocks.Count} entities exist anymore");
                return;
            }
            // The game's demolish tool also clears planting marks in the dragged rectangle: with separate colonies, only
            // the actor's own (see ColonyMarks).
            if (Colonies.ColonyModeService.IsSeparateColonies) Colonies.ColonyMarks.ActingSlot = System.Math.Max(0, slot);
            try
            {
                if (markForDemolition)
                {
                    context.GetSingleton<DemolishableSelectionTool>().ActionCallback(blockObjects, start, end, false, false);
                }
                else
                {
                    context.GetSingleton<DemolishableUnselectionTool>().ActionCallback(blockObjects, start, end, false, false);
                }
            }
            finally
            {
                Colonies.ColonyMarks.ActingSlot = null;
            }
        }

        internal static List<BlockObject> ResolveBlockObjects(IReplayContext context, IEnumerable<Guid> ids)
        {
            var result = new List<BlockObject>();
            foreach (Guid id in ids)
            {
                // Logs a warning if the entity or component is missing.
                var blockObject = GetComponent<BlockObject>(context, id.ToString());
                if (blockObject == null) continue;
                result.Add(blockObject);
            }
            return result;
        }

        public override string ToActionString()
        {
            return $"Setting {blocks.Count()} as marked: {markForDemolition}";
        }

        public static bool DoPrefix(IEnumerable<BlockObject> blockObjects, Vector3Int start, Vector3Int end, bool forDemolition)
        {
            return DoPrefix(() =>
            {
                var ids = blockObjects.Select(obj => obj.GetComponent<EntityComponent>().EntityId);
                return new ClearResourcesMarkedEvent()
                {
                    blocks = ids.ToList(),
                    start = start,
                    end = end,
                    markForDemolition = forDemolition
                };
            });
        }
    }

    [HarmonyPatch(typeof(DemolishableSelectionTool), nameof(DemolishableSelectionTool.ActionCallback))]
    class DemolishableSelectionServiceMarkPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(IEnumerable<BlockObject> blockObjects, Vector3Int start, Vector3Int end, bool selectionStarted, bool selectingArea)
        {
            return ClearResourcesMarkedEvent.DoPrefix(blockObjects, start, end, true);
        }
    }

    [HarmonyPatch(typeof(DemolishableUnselectionTool), nameof(DemolishableUnselectionTool.ActionCallback))]
    class DemolishableSelectionServiceUnmarkPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(IEnumerable<BlockObject> blockObjects, Vector3Int start, Vector3Int end, bool selectionStarted, bool selectingArea)
        {
            return ClearResourcesMarkedEvent.DoPrefix(blockObjects, start, end, false);
        }
    }

    [Serializable]
    class TreeCuttingAreaEvent : ReplayEvent
    {
        // Marking anywhere (there is no land); unmarking only ever removes the actor's own marks (see ColonyMarks).
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public List<Vector3Int> coordinates;
        public bool wasAdded;

        public override void Replay(IReplayContext context)
        {
            var treeService = context.GetSingleton<TreeCuttingArea>();
            // Separate colonies: each colony's marks are its own (only its lumberjacks cut them).
            var marks = Colonies.ColonyMarks.Instance;
            if (Colonies.ColonyModeService.IsSeparateColonies && marks != null)
            {
                marks.MarkCutting(coordinates, System.Math.Max(0, slot), wasAdded);
                return;
            }
            if (wasAdded)
            {
                treeService.AddCoordinates(coordinates);
            }
            else
            {
                treeService.RemoveCoordinates(coordinates);
            }
        }

        public override string ToActionString()
        {
            string verb = wasAdded ? "Added" : "Removed";
            return $"{verb} tree planting coordinate {coordinates.Count()}";
        }
    }

    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.AddCoordinates))]
    class TreeCuttingAreaAddedPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new TreeCuttingAreaEvent()
                {
                    coordinates = new List<Vector3Int>(coordinates),
                    wasAdded = true,
                };
            });
        }
    }

    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.RemoveCoordinates))]
    class TreeCuttingAreaRemovedPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new TreeCuttingAreaEvent()
                {
                    coordinates = new List<Vector3Int>(coordinates),
                    wasAdded = false,
                };
            });
        }
    }

    [Serializable]
    class BuildingUnlockedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public string buildingName;
        // Dev mode's instant unlock (Ctrl-click on a locked building): no science is spent.
        public bool free;

        public override void Replay(IReplayContext context)
        {
            var building = GetBuilding(context, buildingName);
            if (building == null) return;
            var unlocking = context.GetSingleton<BuildingUnlockingService>();
            // With separate science, the unlock and its cost are the actor's colony's (the host wrote the actor's slot
            // into the event). Otherwise the one shared pool and set, as in the game.
            int actorSlot = System.Math.Max(0, slot);
            bool separate = Colonies.ColonyScienceService.IsEnabled;
            // Two players of one colony unlocking the same building at once must not pay twice. The per-colony sets are the
            // same on every computer, and so is the game's own set but for the buildings each player's profile remembers
            // (UnlockableOnceSpec: the HTTP Lever and Adapter), which a shared game does not check (it paid twice, E-5).
            bool alreadyUnlocked = separate
                ? Colonies.ColonyScienceService.InSlot(actorSlot, () => unlocking.Unlocked(building))
                : !building.HasSpec<UnlockableOnceSpec>() && unlocking.Unlocked(building);
            if (alreadyUnlocked)
            {
                Plugin.Log($"Already unlocked for slot {actorSlot}: {buildingName}");
                return;
            }
            if (free)
            {
                if (separate) Colonies.ColonyScienceService.InSlot(actorSlot, () => unlocking.UnlockIgnoringCost(building));
                else unlocking.UnlockIgnoringCost(building);
            }
            else
            {
                bool affordable = separate
                    ? Colonies.ColonyScienceService.InSlot(actorSlot, () => unlocking.Unlockable(building))
                    : unlocking.Unlockable(building);
                if (!affordable)
                {
                    // Science was spent elsewhere between the click and now. The game would throw here, which would
                    // stop the session; the same answer on every computer is to skip it.
                    Plugin.LogWarning($"Not enough science to unlock {buildingName} for slot {actorSlot} any more; skipped");
                    return;
                }
                if (separate) Colonies.ColonyScienceService.InSlot(actorSlot, () => unlocking.Unlock(building));
                else unlocking.Unlock(building);
            }
            // The toolbar below is this computer's: another colony's unlock changes nothing on it.
            if (separate && actorSlot != Colonies.ColonyScienceService.DisplaySlot) return;
            // Display code inside a replay: whatever it asks about science is this colony's (the local player's).
            if (separate) Colonies.ColonyScienceService.InSlot(actorSlot, () => UnlockTool(context, building));
            else UnlockTool(context, building);
        }

        private void UnlockTool(IReplayContext context, BuildingSpec building)
        {
            var toolButtonService = context.GetSingleton<ToolButtonService>();
            var toolUnlockingService = toolButtonService._toolUnlockingService;

            foreach (ToolButton toolButton in toolButtonService.ToolButtons)
            {
                var tool = toolButton.Tool;
                BlockObjectTool blockObjectTool = tool as BlockObjectTool;
                if (blockObjectTool == null)
                {
                    continue;
                }
                BuildingSpec toolBuilding = blockObjectTool.Template.GetSpec<BuildingSpec>();
                if (toolBuilding == building)
                {
                    Plugin.Log("Unlocking tool for building: " + buildingName);
                    context.GetSingleton<UnlockedPlantableGroupsRegistry>().AddUnlockedPlantableGroups(toolBuilding);
                    // Call Unlock to remove from _activeLockers and post ToolUnlockedEvent
                    if (toolUnlockingService != null && toolUnlockingService.IsLocked(tool))
                    {
                        toolUnlockingService.Unlock(tool);
                    }
                }
            }
        }

        public override string ToActionString()
        {
            return free ? $"Unlocking building for free (dev mode): {buildingName}" : $"Unlocking building: {buildingName}";
        }
    }

    [HarmonyPatch(typeof(BuildingUnlockingService), nameof(BuildingUnlockingService.Unlock))]
    class BuildingUnlockingServiceUnlockPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(BuildingSpec buildingSpec)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new BuildingUnlockedEvent()
                {
                    buildingName = buildingSpec.Blueprint.Name,
                };
            });
        }
    }

    // Dev mode's instant unlock (Ctrl-click on a locked building) unlocked it on this computer alone: in a separate-
    // science game, one colony then had a building on the host that it did not have on the guest. It is now an unlock
    // like any other, played on every computer, without the science cost. The tool opens at once, as for a paid unlock.
    [HarmonyPatch(typeof(BuildingToolLocker), nameof(BuildingToolLocker.UnlockIgnoringScienceCost))]
    class BuildingToolLockerInstantUnlockPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(BuildingSpec buildingSpec, Action successCallback)
        {
            if (EventIO.IsNull) return true;
            bool playHere = ReplayEvent.DoPrefix(() => new BuildingUnlockedEvent()
            {
                buildingName = buildingSpec.Blueprint.Name,
                free = true,
            });
            if (playHere) return true;
            // The host's own opens at once. A guest's tool opens when the unlock comes back from the host (the replay
            // unlocks the tool); if the host's dev mode is off the host refuses it and says so, instead of the tool
            // opening on the guest and every placement made with it being refused.
            if (!(EventIO.Get() is ClientEventIO)) successCallback?.Invoke();
            return false;
        }
    }

    // Dev mode's "Add 1000 Science" (the dev panel) added the science on this computer alone: the next unlock paid with
    // it was skipped on the other computers for want of science, and the game desynced. It is now played on every
    // computer; with separate science the points go to the colony of the player who clicked.
    [Serializable]
    class ScienceAddedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public int amount;

        public override void Replay(IReplayContext context)
        {
            var scienceService = context.GetSingleton<ScienceService>();
            if (Colonies.ColonyScienceService.IsEnabled)
                Colonies.ColonyScienceService.InSlot(System.Math.Max(0, slot), () => scienceService.AddPoints(amount));
            else scienceService.AddPoints(amount);
        }

        public override string ToActionString()
        {
            return $"Adding {amount} science (dev mode)";
        }
    }

    [HarmonyPatch(typeof(ScienceAdder), nameof(ScienceAdder.AddScience))]
    class ScienceAdderPatcher
    {
        // The amount the game's AddScience adds (a RuntimeCheck reads it from the game).
        internal const int Amount = 1000;

        [HarmonyPriority(Priority.First)]
        static bool Prefix()
        {
            return ReplayEvent.DoPrefix(() => new ScienceAddedEvent()
            {
                amount = Amount,
            });
        }
    }

    [Serializable]
    class WorkingHoursChangedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public int hours;

        public override void Replay(IReplayContext context)
        {
            // Separate colonies: the actor's colony's hours only (the game's own setting stays as it is).
            var colonyHours = Colonies.ColonyWorkingHours.Instance;
            if (Colonies.ColonyModeService.IsSeparateColonies && colonyHours != null)
            {
                colonyHours.Set(System.Math.Max(0, slot), hours);
                return;
            }
            var panel = context.GetSingleton<WorkingHoursPanel>();
            panel._hours = hours;
            panel.OnHoursChanged();
        }

        public override string ToActionString()
        {
            return $"Setting working hours: {hours}";
        }
    }

    [HarmonyPatch(typeof(WorkingHoursPanel), nameof(WorkingHoursPanel.OnHoursChanged))]
    class WorkingHoursPanelOnHoursChangedPatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(WorkingHoursPanel __instance)
        {
            bool value = ReplayEvent.DoPrefix(() =>
            {
                return new WorkingHoursChangedEvent()
                {
                    hours = __instance._hours,
                };
            });

            // Update the title if we're actually calling this event
            if (!value) __instance.UpdateTitle();
            return value;
        }
    }

    [Serializable]
    class DuplicationEvent : ReplayEvent
    {
        // Both must be yours (or nobody's): copying takes the automation links too, which would wire your building to
        // another colony's sensor.
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(sourceEntityID, targetEntityID);

        public string sourceEntityID;
        public string targetEntityID;

        public override void Replay(IReplayContext context)
        {
            var duplicator = new Duplicator();
            duplicator.Duplicate(
                GetEntityComponent(context, sourceEntityID),
                GetEntityComponent(context, targetEntityID)
            );
        }

        public override string ToActionString()
        {
            return $"Duplicating properties from {sourceEntityID} to {targetEntityID}";
        }
    }

    [ManualMethodOverwrite]
    /*
     * 11/26/2025
     * This isn't a real manual method overwrite, but it still needs
     * to be reviewed when the Timberborn code updates. It makes a strong
     * assumption that Duplicator.Duplicate is only called by UI events
     * and that it's the only callback that gets called when a building
     * is placed (see BuildingPlacedEvent).
     * Check
     * * IBlockObjectPlacer.Place: Make sure it's only called with
     *   a callback that calls this method.
     * * Duplicator.Duplicate: Make sure it's only called by UI actions.
     * * DuplicateSettingsTool: Make sure this continues not to do anything
     *   other than other than registering the change so it can be undone and
     *   that undos are still only supported in the map editor.
     */
    [HarmonyPatch(typeof(Duplicator), nameof(Duplicator.Duplicate))]
    class DuplicatorDuplicatePatcher
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(BaseComponent sourceEntity, BaseComponent targetEntity)
        {
            return ReplayEvent.DoPrefix(() =>
            {
                return new DuplicationEvent()
                {
                    sourceEntityID = ReplayEvent.GetEntityID(sourceEntity),
                    targetEntityID = ReplayEvent.GetEntityID(targetEntity),
                };
            });
        }
    }
}