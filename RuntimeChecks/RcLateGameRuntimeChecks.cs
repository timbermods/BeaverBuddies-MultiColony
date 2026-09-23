#nullable enable
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: reviewer A: the late-game systems against the game (automation, the HTTP API, water, power, blasts and terrain, fireworks, dev tools, crash paths).
internal static class RcLateGameRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");
        Type Mod(string name) => mod.GetType(name, false) ?? throw new Exception(name + " is missing");
        List<IlScan.Instruction> Code(MethodBase method) => IlScan.Instructions(method);
        bool Calls(MethodBase method, string declaringType, string name) =>
            Code(method).Any(i => i.Calls && i.Member?.Name == name && i.Member.DeclaringType?.FullName == declaringType);
        // The (type, method) a patch class's [HarmonyPatch] names, and its prefix's [HarmonyPriority] (Harmony's 400 if none).
        (Type type, string name) Target(Type patch)
        {
            var arguments = patch.GetCustomAttributesData().Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
                .SelectMany(a => a.ConstructorArguments).ToList();
            return ((Type)arguments.First(a => a.Value is Type).Value!, (string)arguments.First(a => a.Value is string).Value!);
        }
        int Priority(MethodInfo method) => (int?)method.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPriority")
            ?.ConstructorArguments[0].Value ?? 400;
        MethodInfo Prefix(Type patch) => patch.GetMethod("Prefix", All) ?? throw new Exception(patch.Name + " has no prefix");
        void ReplacesInCoop(Type patch, string gameType, string gameMethod)
        {
            var (type, name) = Target(patch);
            if (type.FullName != gameType || name != gameMethod) throw new Exception($"{patch.Name} patches {type.FullName}.{name}, not {gameType}.{gameMethod}");
            MethodInfo prefix = Prefix(patch);
            if (Priority(prefix) != 0) throw new Exception($"{patch.Name}'s prefix replaces the game's method but is not Priority.Last");
            if (!Calls(prefix, "BeaverBuddies.IO.EventIO", "get_IsNull")) throw new Exception($"{patch.Name} does not leave single player alone");
        }

        UiWrites.Run(mod, test);

        test("A2: the pumps' flow rate, the throttling valve's limit toggle and the dev generator's panel are shared", () =>
        {
            var recorded = UiWrites.Recorded(mod);
            var cases = new (string Assembly, string Panel, string Method, string Setter, string Name)[]
            {
                ("Timberborn.WaterBuildingsUI", "Timberborn.WaterBuildingsUI.WaterMoverFragment", "SetFlowRate", "Timberborn.WaterBuildings.WaterMover", "SetFlowRate"),
                ("Timberborn.WaterBuildingsUI", "Timberborn.WaterBuildingsUI.ThrottlingValveFragment", "SetOutflowLimit", "Timberborn.WaterBuildings.ThrottlingValve", "SetOutflowLimitEnabledAndSynchronize"),
                ("Timberborn.PowerGenerationUI", "Timberborn.PowerGenerationUI.AdjustableStrengthPowerGeneratorFragment", "ChangeValue", "Timberborn.PowerGeneration.AdjustableStrengthPowerGenerator", "set_GeneratorStrength"),
                ("Timberborn.PowerGenerationUI", "Timberborn.PowerGenerationUI.AdjustableStrengthPowerGeneratorFragment", "FlipRotation", "Timberborn.PowerGeneration.AdjustableStrengthPowerGenerator", "FlipRotation"),
            };
            foreach (var c in cases)
            {
                MethodInfo panel = Only(Game(c.Assembly, c.Panel), c.Method);
                if (!Calls(panel, c.Setter, c.Name)) throw new Exception($"the game's {c.Panel}.{c.Method} no longer calls {c.Setter}.{c.Name}");
                if (!recorded.Contains(c.Setter + "." + c.Name)) throw new Exception($"{c.Setter}.{c.Name} is not shared: it changes the dragging player's game alone");
            }
            // What they change is simulation: the pump moves FlowRate's worth of water every tick, and the valve limits the
            // flow only while OutflowLimitEnabled is on.
            Type mover = Game("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.WaterMover");
            if (!Code(Only(mover, "Tick")).Any(i => i.Calls && i.Member?.Name == "get_EffectiveFlowRate")) throw new Exception("WaterMover.Tick no longer moves EffectiveFlowRate");
            Type valve = Game("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.ThrottlingValve");
            if (!Code(Only(valve, "GetTargetOutflowLimit")).Any(i => i.Calls && i.Member?.Name == "get_OutflowLimitEnabled")) throw new Exception("the valve's limit no longer depends on OutflowLimitEnabled");
            // The dev generator is placed with dev mode, but its panel shows without it.
            Type fragment = Game("Timberborn.PowerGenerationUI", "Timberborn.PowerGenerationUI.AdjustableStrengthPowerGeneratorFragment");
            if (fragment.GetFields(All).Any(f => f.FieldType.Name == "DevModeManager")) throw new Exception("the dev generator's panel now asks for dev mode (no longer needs sharing)");
        });

        test("A1: in co-op a Detonator goes off on a pulse that begins and ends in one tick (a spring-return lever), as in single player", () =>
        {
            // The game disarms only when the input drops at the same Time.time it armed.
            Type detonator = Game("Timberborn.AutomationBuildings", "Timberborn.AutomationBuildings.Detonator");
            MethodInfo evaluate = Only(detonator, "Evaluate");
            if (!Calls(evaluate, "UnityEngine.Time", "get_time") || !Calls(evaluate, detonator.FullName!, "Disarm"))
                throw new Exception("Detonator.Evaluate no longer disarms on a same-time drop");
            // In co-op Time.time is the tick's, and the tick evaluates before and after its commit (where a spring-return
            // lever switches itself off): the lever's pulse armed and disarmed at one Time.time.
            Type runner = Game("Timberborn.Automation", "Timberborn.Automation.AutomationRunner");
            var tick = Code(Only(runner, "Tick")).Where(i => i.Calls && i.Member?.DeclaringType == runner).Select(i => i.Member!.Name).ToList();
            int first = tick.IndexOf("EvaluateScheduled"), commit = tick.IndexOf("CommitTickSingletons"), last = tick.LastIndexOf("EvaluateScheduled");
            if (first < 0 || commit < first || last < commit) throw new Exception("AutomationRunner.Tick no longer evaluates before and after its commit: " + string.Join(", ", tick));
            Type patch = Mod("BeaverBuddies.Fixes.DetonatorPulseCoopPatcher");
            ReplacesInCoop(patch, detonator.FullName!, "Evaluate");
            MethodInfo prefix = Prefix(patch);
            if (!Calls(prefix, detonator.FullName!, "Arm")) throw new Exception("the co-op Detonator no longer arms");
            if (Calls(prefix, detonator.FullName!, "Disarm")) throw new Exception("the co-op Detonator still disarms on a pulse within one tick");
        });

        test("A3: an HTTP API request switches an HTTP lever as the player's shared action, and a colour request is dropped in co-op", () =>
        {
            var recorded = UiWrites.Recorded(mod);
            Type intermediary = Game("Timberborn.HttpApiSystem", "Timberborn.HttpApiSystem.HttpApiIntermediary");
            Type lever = Game("Timberborn.HttpApiSystem", "Timberborn.HttpApiSystem.HttpLever");
            MethodInfo update = Only(intermediary, "UpdateSingleton");
            if (!Calls(update, lever.FullName!, "SetState") || !Calls(update, lever.FullName!, "SetColor"))
                throw new Exception("HttpApiIntermediary.UpdateSingleton no longer applies the requests");
            if (!Calls(Only(lever, "SetState"), "Timberborn.AutomationBuildings.Lever", "SwitchState")) throw new Exception("HttpLever.SetState no longer calls Lever.SwitchState");
            if (!recorded.Contains("Timberborn.AutomationBuildings.Lever.SwitchState")) throw new Exception("Lever.SwitchState is not shared");
            // The colour is saved with the lever's light.
            if (!Calls(Only(lever, "SetColor"), "Timberborn.Illumination.CustomizableIlluminator", "SetCustomColor")) throw new Exception("HttpLever.SetColor no longer sets the light");
            Type illuminator = Game("Timberborn.Illumination", "Timberborn.Illumination.CustomizableIlluminator");
            if (!Code(Only(illuminator, "Save")).Any(i => i.Member?.Name == "CustomColorKey")) throw new Exception("a light's colour is no longer saved (then it could be left alone)");
            ReplacesInCoop(Mod("BeaverBuddies.Events.HttpLeverSetColorCoopPatcher"), lever.FullName!, "SetColor");
        });

        test("A2: dev mode's plant spawning while planting (Ctrl held) is off in co-op, as its other Ctrl keys are", () =>
        {
            Type tool = Game("Timberborn.PlantingUI", "Timberborn.PlantingUI.PlantingTool");
            Type spawner = Game("Timberborn.PlantingUI", "Timberborn.PlantingUI.DevModePlantableSpawner");
            MethodInfo plant = Only(tool, "Plant");
            if (!Calls(plant, "Timberborn.PlantingUI.PlantingSelectionService", "MarkArea") || !Calls(plant, spawner.FullName!, "SpawnPlantables"))
                throw new Exception("the planting tool no longer marks and spawns in one click");
            if (!Calls(Only(spawner, "SpawnPlantables"), "Timberborn.NaturalResources.NaturalResourceFactory", "SpawnIgnoringConstraints"))
                throw new Exception("DevModePlantableSpawner no longer spawns plants");
            ReplacesInCoop(Mod("BeaverBuddies.Fixes.DevPlantSpawnKeyCoopPatcher"), spawner.FullName!, "SpawnPlantables");
        });

        test("A4: a Population Counter set to the whole map counts its own colony in a separate-colonies game", () =>
        {
            Type counter = Game("Timberborn.AutomationBuildings", "Timberborn.AutomationBuildings.PopulationCounter");
            Type sampling = Game("Timberborn.AutomationBuildings", "Timberborn.AutomationBuildings.SamplingPopulationService");
            // The game's global count is every colony's: the population service's global figures.
            if (!Calls(Only(counter, "Sample"), sampling.FullName!, "get_GlobalPopulationData")) throw new Exception("PopulationCounter.Sample no longer reads the global figures");
            if (!Calls(Only(sampling, "Sample"), "Timberborn.Population.PopulationService", "get_GlobalPopulationData")) throw new Exception("the sampled global figures no longer come from PopulationService");
            Type patch = Mod("BeaverBuddies.Colonies.ColonyPopulationCounterPatcher");
            var (type, name) = Target(patch);
            if (type != counter || name != "Sample") throw new Exception("the colony counter patch no longer targets PopulationCounter.Sample");
            MethodInfo prefix = Prefix(patch);
            if (Priority(prefix) != 0) throw new Exception("the colony counter replaces the game's Sample but is not Priority.Last");
            var prefixCode = Code(prefix);
            int separate = prefixCode.FindIndex(i => i.Calls && i.Member?.Name == "get_IsSeparateColonies");
            int global = prefixCode.FindIndex(i => i.Calls && i.Member?.Name == "get_GlobalMode");
            int owner = prefixCode.FindIndex(i => i.Calls && i.Member?.Name == "SimOwnerOf");
            if (separate < 0 || global < 0 || owner < separate) throw new Exception("the colony counter does not first check separate colonies and the global toggle");
            MethodInfo addUp = patch.GetMethod("AddUp", All)!;
            if (!Calls(addUp, "BeaverBuddies.Colonies.DistrictOwner", "OwnerOfDistrict") || !Calls(addUp, sampling.FullName!, "GetDistrictData")
                || !Calls(addUp, "Timberborn.Population.PopulationData", "Update"))
                throw new Exception("the colony counter no longer adds up its own colony's sampled districts");
        });

        test("W1: synchronised fill valves, throttling valves and floodgates keep in step with their own colony's only", () =>
        {
            // The game copies settings and the input wire to every synchronised neighbour, through each other.
            foreach (var (synchronizer, getter) in new[] { ("FillValveSynchronizer", "GetValve"), ("ThrottlingValveSynchronizer", "GetThrottlingValve") })
            {
                Type type = Game("Timberborn.WaterBuildings", "Timberborn.WaterBuildings." + synchronizer);
                MethodInfo neighbour = Only(type, "SynchronizeNeighbor");
                if (!Calls(neighbour, type.FullName!, getter) || !Calls(neighbour, "Timberborn.Automation.Automatable", "SetInput"))
                    throw new Exception($"{synchronizer}.SynchronizeNeighbor no longer copies to the neighbour {getter} finds");
                if (!Calls(Only(type, "SynchronizeWithNeighbors"), type.FullName!, getter)) throw new Exception($"{synchronizer} no longer finds its source with {getter}");
            }
            Type floodgates = Game("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.FloodgateSynchronizer");
            foreach (string method in new[] { "SynchronizeNeighbor", "SynchronizeWithNeighbors" })
                if (!Code(Only(floodgates, method)).Any(i => i.Calls && i.Member?.Name == "GetBottomObjectComponentAt"))
                    throw new Exception($"FloodgateSynchronizer.{method} no longer looks its neighbours up itself");
            // The mod: every entry sets whose colony it works for, and each lookup filters by it.
            Type entry = Mod("BeaverBuddies.Colonies.ColonyWaterSyncEntryPatcher");
            var targets = ((System.Collections.IEnumerable)entry.GetMethod("TargetMethods", All)!.Invoke(null, null)!).Cast<MethodBase?>().ToList();
            if (targets.Count != 9 || targets.Any(t => t == null)) throw new Exception($"the synchronisers' entry points resolved to {targets.Count(t => t != null)} of 9");
            if (!Calls(Mod("BeaverBuddies.Colonies.ColonyWaterSync").GetMethod("Enter", All)!, "BeaverBuddies.Colonies.ColonyModeService", "get_IsSeparateColonies"))
                throw new Exception("the synchronisers are filtered in a shared-colony game too");
            foreach (var (patch, type, method) in new[] { ("ColonyFillValveSyncPatcher", "FillValveSynchronizer", "GetValve"),
                ("ColonyThrottlingValveSyncPatcher", "ThrottlingValveSynchronizer", "GetThrottlingValve"),
                ("ColonyFloodgateSyncNeighborPatcher", "FloodgateSynchronizer", "SynchronizeNeighbor"),
                ("ColonyFloodgateSyncWithNeighborsPatcher", "FloodgateSynchronizer", "SynchronizeWithNeighbors") })
            {
                Type patchType = Mod("BeaverBuddies.Colonies." + patch);
                var (targetType, targetName) = Target(patchType);
                if (targetType.Name != type || targetName != method) throw new Exception($"{patch} targets {targetType.Name}.{targetName}");
                MethodInfo body = patchType.GetMethod("Prefix", All) ?? patchType.GetMethod("Postfix", All)!;
                if (!Calls(body, "BeaverBuddies.Colonies.ColonyWaterSync", "Allows")) throw new Exception($"{patch} does not filter by colony");
            }
        });

        test("R8: a game update that changes the water seep method or renames a shared setter no longer stops the mod's start; a co-op game that needs it is stopped at load", () =>
        {
            Type guard = Mod("BeaverBuddies.Fixes.CoopFixGuard");
            MethodInfo missing = guard.GetMethod("Missing", All)!;
            string? Missing(string? water, bool seeps, params string[] recorders) => (string?)missing.Invoke(null, new object?[] { water, seeps, recorders.ToList() });
            if (Missing(null, true) != null || Missing("changed", false) != null) throw new Exception("co-op is stopped although nothing it needs is missing");
            if (Missing("changed", true)?.Contains("water seep") != true) throw new Exception("a game with water seeps plays on without the seep timing fix");
            if (Missing(null, false, "Lever.SwitchState")?.Contains("Lever.SwitchState") != true) throw new Exception("a game plays on with a setter no longer shared");
            MethodInfo update = guard.GetMethod("UpdateSingleton", All)!;
            if (!Calls(update, "BeaverBuddies.ReplayService", "EndSession")) throw new Exception("the guard no longer ends the session");
            if (!Code(update).Any(i => i.Member?.Name == "MissingRecorders") || !Code(update).Any(i => i.Member?.Name == "get_Unavailable"))
                throw new Exception("the guard no longer asks both fixes");
            // Bound in every co-op game.
            MethodInfo configure = mod.GetType("BeaverBuddies.ReplayConfigurator", true)!.GetMethod("Configure", All)!;
            if (!Code(configure).Any(i => i.Member is MethodInfo m && m.IsGenericMethod && m.GetGenericArguments()[0] == guard))
                throw new Exception("CoopFixGuard is not bound in a co-op game");
            // The shared setters' patching catches what a game update takes away.
            MethodInfo apply = Mod("BeaverBuddies.Events.AutomationEvent").GetMethod("ApplyAutomationPatches", All)!;
            if (apply.GetMethodBody()!.ExceptionHandlingClauses.Count == 0 || !Code(apply).Any(i => i.Member?.Name == "MissingRecorders"))
                throw new Exception("ApplyAutomationPatches can still throw out of the mod's start on a missing setter");
        });

        test("H1: a recipe this game does not have no longer throws out of a replay: the host refuses it, a guest leaves quietly", () =>
        {
            // The game's lookup is a dictionary's indexer.
            Type service = Game("Timberborn.Workshops", "Timberborn.Workshops.RecipeSpecService");
            FieldInfo specs = service.GetField("_recipeSpecs", All)!;
            Type recipeSpec = specs.FieldType.GetGenericArguments()[1];
            Type frozen = specs.FieldType.Assembly.GetType("System.Collections.Frozen.FrozenDictionary", true)!;
            MethodInfo toFrozen = frozen.GetMethods().Single(m => m.Name == "ToFrozenDictionary" && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.Name.StartsWith("IEnumerable"));
            Type pair = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), recipeSpec);
            object empty = toFrozen.MakeGenericMethod(typeof(string), recipeSpec)
                .Invoke(null, new object?[] { Array.CreateInstance(pair, 0), null })!;
            object recipes = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(service);
            specs.SetValue(recipes, empty);
            bool threw = false;
            try { service.GetMethod("GetRecipe", All)!.Invoke(recipes, new object[] { "NoSuchRecipe" }); }
            catch (TargetInvocationException e) when (e.InnerException is KeyNotFoundException) { threw = true; }
            if (!threw) throw new Exception("the game's GetRecipe no longer throws for an unknown recipe (the guard can go)");
            Type recipeEvent = Mod("BeaverBuddies.Events.ManufactoryRecipeSelectedEvent");
            object?[] arguments = { recipes, "NoSuchRecipe", null };
            if ((bool)recipeEvent.GetMethod("TryGetRecipe", All)!.Invoke(null, arguments)!) throw new Exception("an unknown recipe was found");
            if (!Code(recipeEvent.GetMethod("SetValue", All)!).Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "MissingContentException"))
                throw new Exception("a guest meeting an unknown recipe does not leave quietly");
            MethodInfo allow = Mod("BeaverBuddies.Colonies.ColonyRulesService").GetMethod("AllowOnHost", All)!;
            if (!Code(allow).Any(i => i.Calls && i.Member?.Name == "TryGetRecipe")) throw new Exception("the host does not refuse a recipe it does not have");
        });

        test("O4: an Indicator's warning shows to its own colony's player only, as its journal entry does", () =>
        {
            Type indicator = Game("Timberborn.AutomationBuildings", "Timberborn.AutomationBuildings.Indicator");
            if (!Calls(Only(indicator, "EvaluateRisingEdge"), indicator.FullName!, "ShowWarning")) throw new Exception("the indicator no longer warns on its rising edge");
            if (!Calls(Only(indicator, "ShowWarning"), "Timberborn.QuickNotificationSystem.QuickNotificationService", "SendWarningNotification"))
                throw new Exception("the indicator's warning is no longer only a notice (it may now change something)");
            Type patch = Mod("BeaverBuddies.Colonies.ColonyIndicatorWarningPatcher");
            var (type, name) = Target(patch);
            if (type != indicator || name != "ShowWarning") throw new Exception("the warning filter no longer targets Indicator.ShowWarning");
            MethodInfo prefix = Prefix(patch);
            if (Priority(prefix) != 0) throw new Exception("the warning filter skips the game's method but is not Priority.Last");
            if (!Calls(prefix, "BeaverBuddies.Colonies.ColonyViewService", "get_Active") || !Calls(prefix, "BeaverBuddies.Colonies.ColonyViewService", "IsOwn"))
                throw new Exception("the warning filter does not ask whose the indicator is, only where colonies are shown apart");
            if (prefix.GetMethodBody()!.ExceptionHandlingClauses.Count == 0) throw new Exception("the warning filter, which runs in the tick, can throw");
        });

        test("X4: a deletion the mod records clears the terrain the tool picked with it, so none is destroyed later on one computer", () =>
        {
            Type tool = Game("Timberborn.BlockObjectTools", "Timberborn.BlockObjectTools.BlockObjectDeletionTool`1");
            MethodInfo delete = Only(tool, "DeleteBlockObjects");
            if (!Calls(delete, "Timberborn.TerrainPhysics.TerrainDestroyer", "DestroyTerrain")) throw new Exception("the game's deletion no longer destroys the picked terrain");
            MethodInfo prefix = Prefix(Mod("BeaverBuddies.Events.BuildingDeconstructionPatcher"));
            var code = Code(prefix);
            if (!code.Any(i => i.Member?.Name == "_temporaryTerrainCoords")) throw new Exception("the recording prefix leaves the picked terrain in the tool");
        });
    }

    /// <summary>
    /// Sweep 1 of the review (lead A2), kept as a check. A player's click changes the game on every computer only if the
    /// mod records it (a prefix through ReplayEvent.DoPrefix, or AutomationEvent's list of setters): a UI path into the
    /// simulation that skips every recorded method changes one computer's game, as dev mode's Add 1000 Science did in
    /// 1.4.0-beta12. This reads every method of the game's UI (the *UI assemblies, the tool, input and panel plumbing, and
    /// every tool, input processor, entity panel fragment and dev module wherever it lives) and lists each call it makes
    /// into a simulation assembly that is not a query by its name. Each must be recorded (the call, or the UI method it is
    /// made from), or be on the list below with the reason it is harmless: display only, dev mode's (warned about by
    /// DevModeCoopWarning), the map editor's, or state that is only this player's. A game update that adds a UI path
    /// fails here until someone has looked at it.
    /// </summary>
    internal static class UiWrites
    {
        // Assemblies whose state is the UI's or this computer's only: the tool and selection state, the camera, input,
        // settings, rendering, files, Steam, modding, the map editor's own.
        static readonly HashSet<string> Infrastructure = new(("CameraSystem ConstructionGuidelines ToolSystem Common SelectionSystem InputSystem "
            + "WaterSystemRendering Rendering AreaSelectionSystem Coordinates Modding SteamWorkshop GameSaveRepositorySystem KeyBindingSystem "
            + "Localization BlueprintSystem MapRepositorySystem Versioning GraphicsQualitySystem FileSystem TooltipSystem CameraWorldState "
            + "WebNavigation SettingsSystem SerializationSystem ScreenSystem PlatformUtilities BottomBarSystem AlertPanelSystem TextureOperations "
            + "SoundSettingsSystem TerrainSystemRendering ErrorReporting AssetSystem Particles SkySystem RootProviders SteamOverlaySystem "
            + "ToolPanelSystem ThumbnailSystem SteamStoreSystem QuickNotificationSystem PrefabOptimization ModdingAssets ModManagerScene "
            + "MapThumbnailOverlaySystem MapThumbnailCapturing MapThumbnail MainMenuSceneLoading MapEditorSceneLoading IntroSettingsSystem "
            + "CommandLine Benchmarking Analytics Timbermesh TimbermeshAnimations LevelVisibilitySystem SingletonSystem TemplateInstantiation "
            + "Debugging SoundSystem GameSound StatusSystem BonusSystem Effects NeedSpecs GridTraversing MapIndexSystem "
            + "SteamWorkshopContent SteamWorkshopModDownloading MultithreadingAnalysis Diagnostics SceneLoading ApplicationLifetime "
            + "FileBrowsing Language PlayerDataSystem AccessibilitySettingsSystem CameraSettingsSystem TutorialSettingsSystem")
            .Split(' ').Select(n => "Timberborn." + n));

        // The UI's own plumbing, outside the *UI assemblies: tools, panels, buttons.
        static readonly HashSet<string> UiPlumbing = new(new[] { "BatchControl", "BlockObjectTools", "BuildingTools", "CursorToolSystem",
            "SelectionToolSystem", "TimeSpeedButtonSystem", "ToolButtonSystem", "DropdownSystem", "SliderToggleSystem", "EntityPanelSystem" }
            .Select(n => "Timberborn." + n));

        static readonly string[] RootInterfaces = { "IInputProcessor", "ITool", "IEntityPanelFragment", "IDevModule" };

        static readonly Regex Query = new(@"^(get_|op_|add_|remove_|Get|Is|Has|Can|TryGet|Contains|Find|Count|Equals|ToString|Format|Compare|Calculate|Should|Where|Select|Any|Validate|Check|Describe|DistanceIsValid|InclinationIsValid|DistrictCentersAreCompatible|DestinationIsReachable|InfoAt|Unlockable$|Unlocked$|AmountInStock|UnreservedAmountInStock|UnreservedCapacity|ReservedCapacity|LimitedAmount|HoursUntilNoSupply|NeedIs|NeedPointsToMax$|TicksToHours|CellToIndex|Contamination$|SoilMoisture$|ColumnOutflows|WaterHeightOrFloor|Multiplier$|TransputsWithConnections|RotationMatches|DistanceToColor|ColorNegative|ColorPositive|DistrictAppliedNeeds|GlobalAppliedNeeds)");

        static bool IsUi(string assembly) => assembly.Contains("UI") || UiPlumbing.Contains(assembly);

        public static void Run(Assembly mod, Action<string, Action> test)
        {
            test("A2: every call the game's panels, tools and keys make into its simulation is shared, dev mode's, the map editor's or display only", () =>
            {
                string managed = Path.GetDirectoryName(Assembly.Load("Timberborn.Automation").Location)!;
                var assemblies = new List<Assembly>();
                foreach (string file in Directory.GetFiles(managed, "Timberborn.*.dll"))
                {
                    try { assemblies.Add(Assembly.Load(Path.GetFileNameWithoutExtension(file))); } catch { }
                }
                if (assemblies.Count < 400) throw new Exception($"only {assemblies.Count} game assemblies loaded");

                HashSet<string> recorded = Recorded(mod);
                if (recorded.Count < 120) throw new Exception($"only {recorded.Count} recorded game methods were found; the search is broken");

                var calls = new SortedDictionary<string, SortedSet<string>>();
                int scanned = 0;
                foreach (Assembly assembly in assemblies)
                {
                    // The map editor's own assemblies never run in a game.
                    if (assembly.GetName().Name!.Contains("MapEditor")) continue;
                    bool ui = IsUi(assembly.GetName().Name!);
                    foreach (Type type in Types(assembly))
                    {
                        if (!ui && !IsRoot(type)) continue;
                        Type outer = type;
                        while (outer.DeclaringType != null) outer = outer.DeclaringType;
                        foreach (MethodBase method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
                        {
                            if (method.GetMethodBody() == null) continue;
                            scanned++;
                            string caller = Key(method);
                            if (recorded.Contains(caller)) continue;
                            foreach (string callee in Writes(method, outer))
                            {
                                if (recorded.Contains(callee)) continue;
                                string pair = Name(outer) + " -> " + callee;
                                if (!calls.TryGetValue(pair, out var from)) calls[pair] = from = new SortedSet<string>();
                                from.Add(method.Name);
                            }
                        }
                    }
                }
                if (scanned < 8000) throw new Exception($"only {scanned} UI methods were read; the search is broken");
                string? dump = Environment.GetEnvironmentVariable("RC_A2_DUMP");
                if (!string.IsNullOrEmpty(dump))
                    File.WriteAllLines(dump, calls.Select(c => c.Key + "\t" + string.Join(",", c.Value)));
                var unknown = calls.Keys.Where(k => !Allowed.ContainsKey(k)).ToList();
                if (unknown.Count > 0)
                    throw new Exception($"{unknown.Count} UI calls into the simulation that no recorded method covers: " + string.Join("; ", unknown.Take(40)));
                Console.WriteLine($"  A2 sweep: {scanned} UI methods read, {calls.Count} unrecorded calls into the simulation, each classified; {recorded.Count} recorded game methods");
            });
        }

        static bool IsRoot(Type type)
        {
            for (Type? t = type; t != null; t = t.DeclaringType)
            {
                try
                {
                    if (t.GetInterfaces().Any(i => RootInterfaces.Contains(i.Name))) return true;
                }
                catch { }
            }
            return false;
        }

        static IEnumerable<Type> Types(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
        }

        static string Name(Type type) => (type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type).FullName ?? type.Name;

        static string Key(MethodBase method) => Name(method.DeclaringType!) + "." + method.Name;

        // The simulation calls (and field writes) a UI method makes, by name.
        static IEnumerable<string> Writes(MethodBase method, Type outer)
        {
            List<IlScan.Instruction> code;
            try { code = IlScan.Instructions(method); }
            catch { yield break; }
            foreach (var i in code)
            {
                if (i.Member == null) continue;
                Type? declaring = i.Member.DeclaringType;
                if (declaring == null) continue;
                string? assembly = declaring.Assembly.GetName().Name;
                if (assembly == null || !assembly.StartsWith("Timberborn.") || IsUi(assembly) || Infrastructure.Contains(assembly)) continue;
                if (i.Member is MethodBase called && (i.Calls || i.Op == OpCodes.Ldftn))
                {
                    if (called.IsConstructor || Query.IsMatch(called.Name)) continue;
                    yield return Key(called);
                }
                else if (i.Member is FieldInfo field && i.Stores)
                {
                    Type owner = declaring;
                    while (owner.DeclaringType != null) owner = owner.DeclaringType;
                    if (owner == outer) continue;
                    yield return "STORE " + Name(declaring) + "." + field.Name;
                }
            }
        }

        /// <summary>
        /// The game methods a player's action is recorded from: the targets of the mod's recording prefixes (a prefix that
        /// reaches ReplayEvent.DoPrefix, DoEntityPrefix or ReplayService.RecordEvent), and AutomationEvent's list.
        /// </summary>
        internal static HashSet<string> Recorded(Assembly mod)
        {
            Type replayEvent = mod.GetType("BeaverBuddies.Events.ReplayEvent", true)!;
            Type replayService = mod.GetType("BeaverBuddies.ReplayService", true)!;
            var sinks = new List<MethodBase> { replayEvent.GetMethod("DoPrefix", All)!, replayEvent.GetMethod("DoEntityPrefix", All)!, replayService.GetMethod("RecordEvent", All)! };
            var memo = new Dictionary<MethodBase, bool>();
            bool Records(MethodBase method, HashSet<MethodBase> path)
            {
                if (sinks.Contains(method)) return true;
                if (memo.TryGetValue(method, out bool known)) return known;
                if (!path.Add(method)) return false;
                bool result = false;
                try
                {
                    result = IlScan.Instructions(method).Any(i => i.Member is MethodBase called && called.DeclaringType?.Assembly == mod
                        && (i.Calls || i.Op == OpCodes.Ldftn || i.Op == OpCodes.Newobj) && Records(called, path));
                }
                catch { }
                path.Remove(method);
                return memo[method] = result;
            }

            var recorded = new HashSet<string>();
            foreach (Type type in Types(mod))
            {
                var attributes = type.GetCustomAttributesData().Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToList();
                if (attributes.Count == 0) continue;
                MethodInfo? prefix = type.GetMethods(All).FirstOrDefault(m => m.Name == "Prefix"
                    || m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPrefix"));
                if (prefix == null || !Records(prefix, new HashSet<MethodBase>())) continue;
                Type? target = null; string? name = null; string accessor = "";
                foreach (var argument in attributes.SelectMany(a => a.ConstructorArguments))
                {
                    if (argument.Value is Type t) target = t;
                    else if (argument.Value is string s) name = s;
                    else if (argument.ArgumentType.Name == "MethodType")
                        accessor = Convert.ToInt32(argument.Value) switch { 1 => "get_", 2 => "set_", _ => "" };
                }
                if (target != null && name != null) recorded.Add(Name(target) + "." + accessor + name);
            }
            // AutomationEvent's list: (typeof(T), "Name") pairs in ApplyAutomationPatches.
            MethodInfo apply = mod.GetType("BeaverBuddies.Events.AutomationEvent", true)!.GetMethod("ApplyAutomationPatches", All)!;
            byte[] body = apply.GetMethodBody()!.GetILAsByteArray()!;
            var code = IlScan.Instructions(apply);
            for (int n = 0; n < code.Count; n++)
            {
                if (code[n].Op != OpCodes.Ldtoken) continue;
                Type? type = null;
                try { type = apply.Module.ResolveType(BitConverter.ToInt32(body, code[n].Offset + 1)); } catch { }
                if (type == null || type.Assembly == mod) continue;
                string? name = code.Skip(n + 1).Take(3).FirstOrDefault(i => i.Text != null)?.Text;
                if (name != null) recorded.Add(Name(type) + "." + name);
            }
            return recorded;
        }

        /// <summary>
        /// Unrecorded UI calls into the simulation, each looked at (1.4.0-rc1 review, sweep 1), as "UI type -> called
        /// method": why each one changes nothing another computer must also have.
        /// </summary>
        /// Kinds: "covered" (the call reaches a recorded method, or runs inside one), "dev mode" (a dev panel module, dev
        /// tool or debug panel, shown only in dev mode: DevModeCoopWarning), "map editor", "preview" (previews and picking,
        /// never entities), "display" (models, lights, markers, tooltips; a few are saved, but nothing in the simulation
        /// reads them), "the panel's own choice" (which district a panel shows), "this computer's own" (its HTTP server, its
        /// camera, its custom files), "start" (a new game's first building, before any co-op game starts).
        internal static readonly Dictionary<string, string> Allowed = new()
        {
            ["Timberborn.ActivatorSystemUI.TimedComponentActivatorSettingsFragment -> Timberborn.ActivatorSystem.TimedComponentActivator.DisableActivator"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.ActivatorSystemUI.TimedComponentActivatorSettingsFragment -> Timberborn.ActivatorSystem.TimedComponentActivator.EnableActivator"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.ActivatorSystemUI.TimedComponentActivatorSettingsFragment -> Timberborn.ActivatorSystem.TimedComponentActivator.SetCyclesUntilCountdownActivation"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.ActivatorSystemUI.TimedComponentActivatorSettingsFragment -> Timberborn.ActivatorSystem.TimedComponentActivator.SetDaysUntilActivation"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.ActivatorSystemUI.TimedComponentActivatorSettingsFragment -> Timberborn.EntityUndoSystem.EntityChangeRecorderFactory.CreateChangeRecorder"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.Automation.AutomationDevModule -> Timberborn.Automation.AutomationDevModule.LogPartitions"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.AutomationBuildingsUI.DepthSensorMarker -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.AutomationBuildingsUI.DepthSensorMarker -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.AutomationBuildingsUI.GateToggle -> Timberborn.AutomationBuildings.Gate.Automate"] = "covered: calls the recorded Gate.SetOpeningMode",
            ["Timberborn.AutomationBuildingsUI.GateToggle -> Timberborn.AutomationBuildings.Gate.Close"] = "covered: calls the recorded Gate.SetOpeningMode",
            ["Timberborn.AutomationBuildingsUI.GateToggle -> Timberborn.AutomationBuildings.Gate.Open"] = "covered: calls the recorded Gate.SetOpeningMode",
            ["Timberborn.AutomationBuildingsUI.LeverFragment -> Timberborn.AutomationBuildings.Lever.Press"] = "covered: calls the recorded Lever.SwitchState; spring return runs as the simulation's (TickTimingFixes)",
            ["Timberborn.AutomationBuildingsUI.LeverFragment -> Timberborn.AutomationBuildings.Lever.Release"] = "covered: calls the recorded Lever.SwitchState; spring return runs as the simulation's (TickTimingFixes)",
            ["Timberborn.AutomationBuildingsUI.PinnedLeversPanel -> Timberborn.AutomationBuildings.Lever.Press"] = "covered: calls the recorded Lever.SwitchState; spring return runs as the simulation's (TickTimingFixes)",
            ["Timberborn.AutomationBuildingsUI.PinnedLeversPanel -> Timberborn.AutomationBuildings.Lever.Release"] = "covered: calls the recorded Lever.SwitchState; spring return runs as the simulation's (TickTimingFixes)",
            ["Timberborn.AutomationBuildingsUI.SpeakerFragment -> Timberborn.AutomationBuildings.SpeakerSoundService.ReloadCustomSounds"] = "this computer's own: this computer's custom files",
            ["Timberborn.BeaversUI.BeaverGeneratorTool -> Timberborn.Beavers.BeaverFactory.CreateAdult"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.BeaversUI.BeaverGeneratorTool -> Timberborn.Beavers.BeaverFactory.CreateChild"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.BlockObjectTools.BlockObjectDeletionTool`1 -> Timberborn.BlockObjectPickingSystem.BlockObjectModelBlockadeIgnorer.Clear"] = "preview: picking through models while the tool is open",
            ["Timberborn.BlockObjectTools.BlockObjectDeletionTool`1 -> Timberborn.BlockObjectPickingSystem.BlockObjectModelBlockadeIgnorer.IgnoreModelBlockades"] = "preview: picking through models while the tool is open",
            ["Timberborn.BlockObjectTools.BlockObjectDeletionTool`1 -> Timberborn.BlockObjectPickingSystem.BlockObjectModelBlockadeIgnorer.UnignoreModelBlockades"] = "preview: picking through models while the tool is open",
            ["Timberborn.BlockObjectTools.BlockObjectTool -> Timberborn.EntitySystem.EntitySetup+Builder.AddInitComponent"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.BlockObjectTools.BlockObjectTool -> Timberborn.UndoSystem.IUndoRegistry.CommitStack"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.BlockObjectTools.DefaultBlockObjectPlacer -> Timberborn.BlockSystem.BlockObjectFactory.CreateFinished"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.BlockObjectTools.PreviewFactory -> Timberborn.BlockSystem.BlockObject.MarkAsPreviewAndInitialize"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewPlacer -> Timberborn.BlockObjectModelSystem.BlockObjectModelController.UpdateModel"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewPlacer -> Timberborn.BlockSystem.BlockObjectValidationService.AreValid"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewPlacer -> Timberborn.BlockSystem.Preview.AddToPreviewServices"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewPlacer -> Timberborn.BlockSystem.Preview.Hide"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewPlacer -> Timberborn.BlockSystem.Preview.RemoveFromPreviewServices"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewPlacer -> Timberborn.BlockSystem.Preview.Reposition"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewShower -> Timberborn.BlockSystem.IPrePreviewShownListener.OnPrePreviewShown"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewShower -> Timberborn.BlockSystem.IPreviewValidator.InvalidatedObjects"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewShower -> Timberborn.BlockSystem.Preview.Hide"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewShower -> Timberborn.BlockSystem.Preview.Show"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewTerrainCutoutService -> Timberborn.TerrainSystem.ITerrainService.SetCutout"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockObjectTools.PreviewTerrainCutoutService -> Timberborn.TerrainSystem.ITerrainService.UnsetCutout"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.BlockSystemUI.EntranceMarkerDrawer -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.BlockSystemUI.EntranceMarkerDrawer -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.BotsUI.BotGeneratorTool -> Timberborn.Bots.BotFactory.Create"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.BrushesUI.BrushDirectionPanel -> Timberborn.Brushes.IBrushWithDirection.set_Increase"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.BrushesUI.BrushDirectionPanel -> Timberborn.Brushes.IBrushWithDirection.set_Inverse"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.BrushesUI.BrushHeightPanel -> Timberborn.Brushes.IBrushWithHeight.set_BrushHeight"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.BrushesUI.BrushShapePanel -> Timberborn.Brushes.IBrushWithShape.set_BrushShape"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.BrushesUI.BrushSizePanel -> Timberborn.Brushes.IBrushWithSize.set_BrushSize"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.BuildingsUI.BuildingSoundControllerFragment -> Timberborn.Buildings.BuildingSoundController.DisableSound"] = "display: the bell's sound: saved, read only to play it",
            ["Timberborn.BuildingsUI.BuildingSoundControllerFragment -> Timberborn.Buildings.BuildingSoundController.EnableSound"] = "display: the bell's sound: saved, read only to play it",
            ["Timberborn.CameraSystem.CameraStateRestorer -> Timberborn.Persistence.IObjectSaver.Set"] = "this computer's own: the camera",
            ["Timberborn.CameraWorldState.CameraWorldStateResetter -> Timberborn.TerrainQueryingSystem.TerrainPicker.PickTerrainCoordinates"] = "covered: a query (the planting and cutting marks are levelled by the actor, ToolEvents)",
            ["Timberborn.CharacterControlSystemUI.CharacterControlDestinationPicker -> Timberborn.TerrainQueryingSystem.TerrainPicker.PickTerrainCoordinates"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.CharacterControlSystemUI.CharacterControlFragment -> Timberborn.CharacterControlSystem.ControllableCharacter.DisableForcedWalking"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.CharacterControlSystemUI.CharacterControlFragment -> Timberborn.CharacterControlSystem.ControllableCharacter.EnableForcedWalking"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.CharacterControlSystemUI.CharacterControlFragment -> Timberborn.CharacterControlSystem.ControllableCharacter.ReleaseControl"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.CharacterControlSystemUI.CharacterControlFragment -> Timberborn.CharacterControlSystem.ControllableCharacter.TakeControlAndMoveTo"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.CharacterControlSystemUI.ControllableCharacterDropdownProvider -> Timberborn.CharacterControlSystem.ControllableCharacter.ChangeAnimation"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.CharactersUI.CharactersModelToggler -> Timberborn.CharacterModelSystem.CharacterModel.Hide"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.CharactersUI.CharactersModelToggler -> Timberborn.CharacterModelSystem.CharacterModel.Show"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.CursorToolSystem.CursorCoordinatesPicker -> Timberborn.TerrainQueryingSystem.TerrainPicker.PickTerrainCoordinates"] = "covered: a query (the planting and cutting marks are levelled by the actor, ToolEvents)",
            ["Timberborn.CursorToolSystem.CursorTool -> Timberborn.Options.IOptionsBox.Show"] = "covered: GameOptionsBox.Show is recorded",
            ["Timberborn.DecalSystemUI.DecalButton -> Timberborn.DecalSystem.DecalSupplier.SetActiveDecal"] = "display: a building's decal: saved, display only (custom decals are local files)",
            ["Timberborn.DecalSystemUI.DecalSupplierFragment -> Timberborn.DecalSystem.IDecalService.ReloadCustomDecals"] = "this computer's own: this computer's custom files",
            ["Timberborn.DecalSystemUI.FlippableDecalFragment -> Timberborn.DecalSystem.FlippableDecal.SetFlip"] = "display: a building's decal: saved, display only (custom decals are local files)",
            ["Timberborn.DemolishingUI.DemolishableSelectionTool -> Timberborn.Planting.PlantingService.UnsetPlantingCoordinates"] = "covered: from the recorded ActionCallback",
            ["Timberborn.DemolishingUI.DemolitionBlockedStatus -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.DemolishingUI.DemolitionBlockedStatus -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.DeteriorationSystemUI.DeteriorableDebugFragment -> Timberborn.DeteriorationSystem.Deteriorable.SetDeteriorationToZero"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.DuplicationSystemUI.DuplicateSettingsTool -> Timberborn.EntityUndoSystem.EntityChangeRecorderFactory.CreateChangeRecorder"] = "map editor: the map editor's undo; the duplication itself is recorded",
            ["Timberborn.DwellingSystemUI.DwellingDebugFragment -> Timberborn.Reproduction.NewbornSpawner.SpawnChild"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.ExplosionsUI.UnstableCoreDebugFragment -> Timberborn.EntitySystem.EntityService.Delete"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.ExplosionsUI.UnstableCoreDebugFragment -> Timberborn.Explosions.UnstableCore.Activate"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.ExplosionsUI.UnstableCoreDebugFragment -> Timberborn.Explosions.UnstableCore.ActivateDelayed"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.ExplosionsUI.UnstableCoreDebugFragment -> Timberborn.Explosions.UnstableCoreExplosionBlocker.BlockExplosion"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.ExplosionsUI.UnstableCoreDebugFragment -> Timberborn.Explosions.UnstableCoreExplosionBlocker.Disable"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.ExplosionsUI.UnstableCoreFragment -> Timberborn.EntityUndoSystem.EntityChangeRecorderFactory.CreateChangeRecorder"] = "dev mode: the core's radius shows only in dev mode or the map editor",
            ["Timberborn.ExplosionsUI.UnstableCoreFragment -> Timberborn.Explosions.UnstableCore.SetRadius"] = "dev mode: the core's radius shows only in dev mode or the map editor",
            ["Timberborn.ForestryUI.TreeCuttingAreaSelectionTool -> Timberborn.TerrainQueryingSystem.TerrainAreaService.InMapLeveledCoordinates"] = "covered: a query (the planting and cutting marks are levelled by the actor, ToolEvents)",
            ["Timberborn.ForestryUI.TreeCuttingAreaUnselectionTool -> Timberborn.TerrainQueryingSystem.TerrainAreaService.InMapLeveledCoordinates"] = "covered: a query (the planting and cutting marks are levelled by the actor, ToolEvents)",
            ["Timberborn.GameDistrictsUI.CitizenTint -> Timberborn.Characters.CharacterTint.DisableTint"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.GameDistrictsUI.CitizenTint -> Timberborn.Characters.CharacterTint.SetTint"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.GameDistrictsUI.DistrictCenterFragment -> Timberborn.GameDistrictsMigration.ManualMigrationDistrictSetter.SetLeftDistrictWithHighlight"] = "the panel's own choice: the panels' district; the automation samples its own per-district figures (A4)",
            ["Timberborn.GameDistrictsUI.DistrictCenterFragment -> Timberborn.GameDistrictsMigration.ManualMigrationDistrictSetter.SetRightDistrictWithHighlight"] = "the panel's own choice: the panels' district; the automation samples its own per-district figures (A4)",
            ["Timberborn.GameDistrictsUI.DistrictConnectionDrawingService -> Timberborn.BlockSystem.BlockObject.TransformCoordinates"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.GameDistrictsUI.PreviewDistrictObstacle -> Timberborn.GameDistricts.DistrictObstacle.AddToPreviewDistricts"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.GameDistrictsUI.PreviewDistrictObstacle -> Timberborn.GameDistricts.DistrictObstacle.RemoveFromPreviewDistricts"] = "preview: previews: not entities, and the preview maps are the UI's",
            ["Timberborn.GameDistrictsUI.SelectableDistrictBuilding -> Timberborn.GameDistricts.DistrictContextService.UnselectDistrict"] = "the panel's own choice: the panels' district; the automation samples its own per-district figures (A4)",
            ["Timberborn.GameSaveRepositorySystemUI.ValidatingGameLoader -> Timberborn.GameSceneLoading.GameSceneLoader.StartSaveGame"] = "covered: saving (the mod's save patches) or loading another game",
            ["Timberborn.GameSaveRuntimeSystemUI.GameSaverDevModule -> Timberborn.GameSaveRuntimeSystem.GameSaver.BenchmarkSavingToMemory"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.GameSaveRuntimeSystemUI.SaveGameBox -> Timberborn.GameSaveRuntimeSystem.GameSaver.QueueSave"] = "covered: saving (the mod's save patches) or loading another game",
            ["Timberborn.GameStartup.StartingBuildingToolShower -> Timberborn.GameStartup.ISettlementNamePromptShower.PromptDisallowingCancelling"] = "start: a new game's starting building, placed before a co-op game starts (the waiting room makes the world alone)",
            ["Timberborn.GameStartup.StartingBuildingToolShower -> Timberborn.GameStartup.StartingBuildingSpawner.DeleteStartingBuilding"] = "start: a new game's starting building, placed before a co-op game starts (the waiting room makes the world alone)",
            ["Timberborn.GameStartup.StartingBuildingToolShower -> Timberborn.GameStartup.StartingBuildingSpawner.Place"] = "start: a new game's starting building, placed before a co-op game starts (the waiting room makes the world alone)",
            ["Timberborn.GameStartup.StartingBuildingToolShower -> Timberborn.GameStartup.StartingBuildingToolFactory.Create"] = "start: a new game's starting building, placed before a co-op game starts (the waiting room makes the world alone)",
            ["Timberborn.GameStartup.StartingBuildingToolShower -> Timberborn.GameStartup.StartingBuildingToolShower.Place"] = "start: a new game's starting building, placed before a co-op game starts (the waiting room makes the world alone)",
            ["Timberborn.GameStartup.StartingBuildingToolShower -> Timberborn.GameStartup.StartingBuildingToolShower.PlaceStartingBuildingOnPreviousCoordinates"] = "start: a new game's starting building, placed before a co-op game starts (the waiting room makes the world alone)",
            ["Timberborn.GameWonderCompletionUI.WonderCompletionDevModule -> Timberborn.GameWonderCompletion.GameWonderCompletionService.CompleteWonder"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.GameWonderCompletionUI.WonderCompletionDevModule -> Timberborn.GameWonderCompletion.GameWonderCompletionService.RevokeWonderCompletionForAllFactions"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.HttpApiSystemUI.HttpAdapterFragment -> Timberborn.HttpApiSystem.HttpAdapter.set_Method"] = "this computer's own: the adapter's webhook settings: saved, but only what this computer's HTTP calls do (A3)",
            ["Timberborn.HttpApiSystemUI.HttpAdapterFragment -> Timberborn.HttpApiSystem.HttpAdapter.set_SwitchedOffWebhookEnabled"] = "this computer's own: the adapter's webhook settings: saved, but only what this computer's HTTP calls do (A3)",
            ["Timberborn.HttpApiSystemUI.HttpAdapterFragment -> Timberborn.HttpApiSystem.HttpAdapter.set_SwitchedOffWebhookUrl"] = "this computer's own: the adapter's webhook settings: saved, but only what this computer's HTTP calls do (A3)",
            ["Timberborn.HttpApiSystemUI.HttpAdapterFragment -> Timberborn.HttpApiSystem.HttpAdapter.set_SwitchedOnWebhookEnabled"] = "this computer's own: the adapter's webhook settings: saved, but only what this computer's HTTP calls do (A3)",
            ["Timberborn.HttpApiSystemUI.HttpAdapterFragment -> Timberborn.HttpApiSystem.HttpAdapter.set_SwitchedOnWebhookUrl"] = "this computer's own: the adapter's webhook settings: saved, but only what this computer's HTTP calls do (A3)",
            ["Timberborn.HttpApiSystemUI.HttpApiFragment -> Timberborn.HttpApiSystem.HttpApi.SetPort"] = "this computer's own: this computer's own HTTP server (its port is saved)",
            ["Timberborn.HttpApiSystemUI.HttpApiFragment -> Timberborn.HttpApiSystem.HttpApi.Start"] = "this computer's own: this computer's own HTTP server (its port is saved)",
            ["Timberborn.HttpApiSystemUI.HttpApiFragment -> Timberborn.HttpApiSystem.HttpApi.Stop"] = "this computer's own: this computer's own HTTP server (its port is saved)",
            ["Timberborn.IlluminationUI.CustomizableIlluminatorFragment -> Timberborn.Illumination.CustomizableIlluminator.SetCustomColor"] = "display: a light's colour: saved, read only by lights and indicators",
            ["Timberborn.IlluminationUI.CustomizeIlluminationFragment -> Timberborn.Illumination.CustomizableIlluminator.SetIsCustomized"] = "display: a light's colour: saved, read only by lights and indicators",
            ["Timberborn.IlluminationUI.NightTimeLightController -> Timberborn.Illumination.Illuminator.CreateToggle"] = "display: models, markers, lights and status icons",
            ["Timberborn.IlluminationUI.NightTimeLightController -> Timberborn.Illumination.IlluminatorToggle.TurnOff"] = "display: models, markers, lights and status icons",
            ["Timberborn.IlluminationUI.NightTimeLightController -> Timberborn.Illumination.IlluminatorToggle.TurnOn"] = "display: models, markers, lights and status icons",
            ["Timberborn.InventorySystemUI.InventoryFillerDevModule -> Timberborn.InventorySystem.Inventory.GiveExisting"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.InventorySystemUI.ModifyInventoryBox -> Timberborn.InventorySystem.Inventory.GiveExisting"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.InventorySystemUI.ModifyInventoryBox -> Timberborn.InventorySystem.Inventory.GiveExistingIgnoringCapacity"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.InventorySystemUI.ModifyInventoryBox -> Timberborn.InventorySystem.Inventory.TakeConsumed"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.MapMetadataSystemUI.MapMetadataPanel -> Timberborn.UndoSystem.IUndoRegistry.RegisterSingleUndoable"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.MapMetadataSystemUI.MapMetadataSaveEntryWriter -> Timberborn.MapMetadataSystem.MapMetadataSerializer.WriteToSaveEntryStream"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.MapThumbnailCapturingUI.ThumbnailCapturingTool -> Timberborn.ThumbnailCapturing.ThumbnailRenderer.Render"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.MechanicalSystemUI.BatteryFragment -> Timberborn.MechanicalSystem.IBattery.ModifyCharge"] = "dev mode: the charge slider shows only in dev mode",
            ["Timberborn.MechanicalSystemUI.MechanicalGraphModelUpdater -> Timberborn.MechanicalSystem.MechanicalNode.ResetAllTransputRotations"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.MechanicalSystemUI.MechanicalGraphModelUpdater -> Timberborn.MechanicalSystem.Transput.ReverseRotation"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeAnimator -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeAnimator -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeFacingMarkerDrawer -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeFacingMarkerDrawer -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeIlluminator -> Timberborn.Illumination.Illuminator.CreateToggle"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeIlluminator -> Timberborn.Illumination.IlluminatorToggle.TurnOff"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeIlluminator -> Timberborn.Illumination.IlluminatorToggle.TurnOn"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeSelfMarkerDrawer -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.MechanicalNodeSelfMarkerDrawer -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.NoPowerStatus -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MechanicalSystemUI.NoPowerStatus -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller.<GetDefinition>b__11_0"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller.KillAllExceptSelected"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller.KillAllPopulation"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller.KillCharacter"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller.KillPartOfPopulation"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller.KillSelectedCharacter"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller+<>c.<GetKillableCharacters>b__19_0"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.CharacterKiller+<>c.<GetKillableCharacters>b__19_1"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.Mortal.DieInstantly"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.CharacterKiller -> Timberborn.MortalSystem.Mortal.DiePubliclyAsSoonAsPossible"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.LongLastingCorpsesDevModule -> Timberborn.MortalSystem.LongLastingCorpsesDevModule.ToggleLongLastingCorpses"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.MortalSystem.LongLastingCorpsesDevModule -> Timberborn.MortalSystem.LongLastingCorpsesService.Toggle"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.NaturalResourcesReproduction.PotentialSpotsToggler -> Timberborn.NaturalResourcesReproduction.PotentialSpotsToggler.DrawPotentialSpots"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.NaturalResourcesReproduction.PotentialSpotsToggler -> Timberborn.NaturalResourcesReproduction.PotentialSpotsToggler.TogglePotentialSpots"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.NaturalResourcesUI.NaturalResourcesModelToggler -> Timberborn.NaturalResourcesModelSystem.NaturalResourceModel.Hide"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.NaturalResourcesUI.NaturalResourcesModelToggler -> Timberborn.NaturalResourcesModelSystem.NaturalResourceModel.Show"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.NavigationUI.NavMeshDrawerController -> Timberborn.Navigation.INavMeshDrawer.DrawForOneFrameAroundCoordinates"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.PlantingUI.DevModePlantableSpawner -> Timberborn.Gathering.GatherableYieldGrower.FastForwardGrowth"] = "dev mode: dev keys while planting; off in co-op (DevPlantSpawnKeyCoopPatcher)",
            ["Timberborn.PlantingUI.DevModePlantableSpawner -> Timberborn.Growing.Growable.IncreaseGrowthProgress"] = "dev mode: dev keys while planting; off in co-op (DevPlantSpawnKeyCoopPatcher)",
            ["Timberborn.PlantingUI.DevModePlantableSpawner -> Timberborn.NaturalResources.NaturalResourceFactory.SpawnIgnoringConstraints"] = "dev mode: dev keys while planting; off in co-op (DevPlantSpawnKeyCoopPatcher)",
            ["Timberborn.PlantingUI.PlantingSelectionService -> Timberborn.Planting.PlantingService.SetPlantingCoordinates"] = "covered: inside the recorded MarkArea / UnmarkArea",
            ["Timberborn.PlantingUI.PlantingSelectionService -> Timberborn.Planting.PlantingService.UnsetPlantingCoordinates"] = "covered: inside the recorded MarkArea / UnmarkArea",
            ["Timberborn.PlantingUI.PlantingSelectionService -> Timberborn.TerrainQueryingSystem.TerrainAreaService.InMapLeveledCoordinates"] = "covered: a query (the planting and cutting marks are levelled by the actor, ToolEvents)",
            ["Timberborn.PopulationUI.PopulationServiceDistrictSwitcher -> Timberborn.Population.PopulationService.SwitchDistrict"] = "the panel's own choice: the panels' district; the automation samples its own per-district figures (A4)",
            ["Timberborn.PowerGenerationUI.GoodPoweredGeneratorAnimator -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.PowerGenerationUI.GoodPoweredGeneratorAnimator -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.PowerGenerationUI.WindPoweredGeneratorAnimator -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.PowerGenerationUI.WindPoweredGeneratorAnimator -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.PowerGenerationUI.WindPoweredGeneratorAnimator -> Timberborn.WindSystem.WindRotationAnimator.SuspendAnimation"] = "display: models, markers, lights and status icons",
            ["Timberborn.PowerGenerationUI.WindPoweredGeneratorAnimator -> Timberborn.WindSystem.WindRotationAnimator.UnsuspendAnimation"] = "display: models, markers, lights and status icons",
            ["Timberborn.PrioritySystemUI.PriorityToggle -> Timberborn.PrioritySystem.IPrioritizable.SetPriority"] = "covered: both implementations (WorkplacePriority, BuilderPrioritizable) are recorded",
            ["Timberborn.PrioritySystemUI.PriorityToggleGroup -> Timberborn.PrioritySystem.IPrioritizable.SetPriority"] = "covered: both implementations (WorkplacePriority, BuilderPrioritizable) are recorded",
            ["Timberborn.RecoverableGoodSystemUI.RecoverableGoodTooltip -> Timberborn.RecoverableGoodSystem.RecoverableGoodRegistry.Clear"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.RelationSystemUI.RelationHighlighter -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.RelationSystemUI.RelationHighlighter -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.ReproductionUI.BreedingPodInventoryBatchControlRowItemFactory -> Timberborn.InventorySystemBatchControl.InventoryCapacityBatchControlRowItemFactory.Create"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.ResourceCountingSystemUI.ContextualResourceCountingService -> Timberborn.ResourceCountingSystem.ResourceCountingService.SwitchDistrict"] = "the panel's own choice: the panels' district; the automation samples its own per-district figures (A4)",
            ["Timberborn.RuinsModelShuffling.RuinModelShufflingFragment -> Timberborn.Ruins.RuinReplacer.Shuffle"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.RuinsModelShuffling.RuinModelShufflingFragment -> Timberborn.RuinsModelShuffling.RuinModelShufflingFragment.ShuffleModel"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.RuinsModelShuffling.RuinModelShufflingFragment -> Timberborn.UndoSystem.IUndoRegistry.CommitStack"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.SettingsSystemUI.DevModeSettingsController -> Timberborn.FactionSystem.FactionUnlockingService.LockAllFactions"] = "dev mode: dev settings: the player profile's faction unlocks",
            ["Timberborn.SettingsSystemUI.DevModeSettingsController -> Timberborn.FactionSystem.FactionUnlockingService.UnlockAllFactions"] = "dev mode: dev settings: the player profile's faction unlocks",
            ["Timberborn.SteamWorkshopMapUploadingUI.SteamWorkshopMapDataService -> Timberborn.Persistence.IObjectSaver.Set"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.StockpilesUI.NoGoodAllowedStatus -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.StockpilesUI.NoGoodAllowedStatus -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.StockpilesUI.StockpileInventoryDebugFragment -> Timberborn.InventorySystem.Inventory.GiveProduced"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.StockpilesUI.UnwantedStockStatus -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.StockpilesUI.UnwantedStockStatus -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.StockpileVisualizationUI.StockpileGoodColumnVisualizerDebugFragment -> Timberborn.StockpileVisualization.StockpileGoodColumnVisualizer.OverrideColor"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.TimeSystemUI.TimeFastForwarderDevModule -> Timberborn.TimeSystem.TimeFastForwarder.JumpToNextDaytime"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.TutorialSystemUI.AchievedStepsController -> Timberborn.TutorialSystem.ITutorialService.StartNextStage"] = "display: the tutorial panel",
            ["Timberborn.TutorialSystemUI.TutorialPanel -> Timberborn.TutorialSystem.ITutorialService.StartNextStage"] = "display: the tutorial panel",
            ["Timberborn.TutorialSystemUI.TutorialStepView -> Timberborn.TutorialSystem.ITutorialStep.Achieved"] = "display: the tutorial panel",
            ["Timberborn.TutorialSystemUI.TutorialStepView -> Timberborn.TutorialSystem.ITutorialStep.Description"] = "display: the tutorial panel",
            ["Timberborn.UILayoutSystem.OverlayPanelSpeedLocker -> Timberborn.TimeSystem.SpeedManager.ChangeAndLockSpeed"] = "covered: the mod's SpeedLockPatcher / SpeedUnlockPatcher",
            ["Timberborn.UILayoutSystem.OverlayPanelSpeedLocker -> Timberborn.TimeSystem.SpeedManager.UnlockSpeed"] = "covered: the mod's SpeedLockPatcher / SpeedUnlockPatcher",
            ["Timberborn.WaterBrushesUI.WaterHeightBrushTool -> Timberborn.Brushes.BrushShapeIterator.IterateShape"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.WaterBrushesUI.WaterHeightBrushTool -> Timberborn.WaterSystem.IWaterService.AddCleanWater"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.WaterBrushesUI.WaterHeightBrushTool -> Timberborn.WaterSystem.IWaterService.AddContaminatedWater"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.WaterBrushesUI.WaterHeightBrushTool -> Timberborn.WaterSystem.IWaterService.RemoveCleanWater"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.WaterBrushesUI.WaterHeightBrushTool -> Timberborn.WaterSystem.IWaterService.RemoveContaminatedWater"] = "dev mode: a dev mode tool (IDevModeTool; every non-building placeable is DevModeTool)",
            ["Timberborn.WaterBuildingsUI.FillValveMarker -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.WaterBuildingsUI.FillValveMarker -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.WaterBuildingsUI.StreamGaugeFragment -> Timberborn.WaterBuildings.StreamGauge.ResetHighestWaterLevel"] = "display: the gauge's highest-level marker: saved, read by nothing in the simulation",
            ["Timberborn.WaterBuildingsUI.WaterDirectionPreviewMarker -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.WaterBuildingsUI.WaterDirectionPreviewMarker -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.WaterBuildingsUI.WaterOutputParticleLength -> Timberborn.BaseComponentSystem.BaseComponent.DisableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.WaterBuildingsUI.WaterOutputParticleLength -> Timberborn.BaseComponentSystem.BaseComponent.EnableComponent"] = "display: models, markers, lights and status icons",
            ["Timberborn.WaterSourceSystemUI.WaterSourceFragment -> Timberborn.EntityUndoSystem.EntityChangeRecorderFactory.CreateChangeRecorder"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.WaterSourceSystemUI.WaterSourceFragment -> Timberborn.WaterSourceSystem.WaterSource.SetSpecifiedStrength"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.WaterSourceSystemUI.WaterSourceFragment -> Timberborn.WaterSourceSystem.WaterSourceContamination.SetContamination"] = "map editor: bound in the map editor only (or a brush of a dev tool)",
            ["Timberborn.WaterSystemUI.WaterColumnDebuggingPanel -> Timberborn.WaterSystem.INonThreadSafeWaterService.UpdateOutflowsData"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.WaterSystemUI.WaterSystemDevModule -> Timberborn.SimulationSystem.SimulationController.ResetSimulation"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.WeatherSystemUI.WeatherFastForwarderDevModule -> Timberborn.WeatherSystem.WeatherFastForwarder.JumpToNextSeason"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.WellbeingUI.NeedViewFactory -> Timberborn.NeedSystem.NeedManager.ApplyEffect"] = "dev mode: the need edit buttons show only in dev mode",
            ["Timberborn.WellbeingUI.WellbeingFragment -> Timberborn.NeedSystem.NeedManager.ApplyEffect"] = "dev mode: the need edit buttons show only in dev mode",
            ["Timberborn.WellbeingUI.WellbeingFragment -> Timberborn.NeedSystem.NeedManager.DisableUpdate"] = "dev mode: the need edit buttons show only in dev mode",
            ["Timberborn.WellbeingUI.WellbeingFragment -> Timberborn.NeedSystem.NeedManager.EnableUpdate"] = "dev mode: the need edit buttons show only in dev mode",
            ["Timberborn.WellbeingUI.WellbeingServiceDistrictSwitcher -> Timberborn.Wellbeing.WellbeingService.SwitchDistrict"] = "the panel's own choice: the panels' district; the automation samples its own per-district figures (A4)",
            ["Timberborn.WindSystemUI.DebugWindDevModule -> Timberborn.WindSystem.WindService.ToggleForcedWind"] = "dev mode: a dev panel module (DevModeCoopWarning)",
            ["Timberborn.WondersUI.WonderDebugFragment -> Timberborn.ConstructionSites.ConstructionSite.IncreaseBuildTime"] = "dev mode: a dev mode debug panel (DiagnosticFragmentController shows them only in dev mode)",
            ["Timberborn.WorkSystemUI.WorkerTypeIlluminator -> Timberborn.Illumination.Illuminator.CreateColorizer"] = "display: models, markers, lights and status icons",
            ["Timberborn.WorkSystemUI.WorkerTypeIlluminator -> Timberborn.Illumination.IlluminatorColorizer.ClearColor"] = "display: models, markers, lights and status icons",
            ["Timberborn.WorkSystemUI.WorkerTypeIlluminator -> Timberborn.Illumination.IlluminatorColorizer.SetColor"] = "display: models, markers, lights and status icons",
            ["Timberborn.WorkSystemUI.WorkplaceIlluminator -> Timberborn.Illumination.Illuminator.CreateToggle"] = "display: models, markers, lights and status icons",
            ["Timberborn.WorkSystemUI.WorkplaceIlluminator -> Timberborn.Illumination.IlluminatorToggle.TurnOff"] = "display: models, markers, lights and status icons",
            ["Timberborn.WorkSystemUI.WorkplaceIlluminator -> Timberborn.Illumination.IlluminatorToggle.TurnOn"] = "display: models, markers, lights and status icons",
            ["Timberborn.ZiplineSystemUI.ZiplineConnectionButtonFactory -> Timberborn.ZiplineSystem.ZiplineCableRenderer.HighlightConnection"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.ZiplineSystemUI.ZiplineConnectionButtonFactory -> Timberborn.ZiplineSystem.ZiplineCableRenderer.UnhighlightConnection"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.ZiplineSystemUI.ZiplinePreviewCableRenderer -> Timberborn.ZiplineSystem.ZiplineCableModel.Highlight"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.ZiplineSystemUI.ZiplinePreviewCableRenderer -> Timberborn.ZiplineSystem.ZiplineCableModel.SetVisibility"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.ZiplineSystemUI.ZiplinePreviewCableRenderer -> Timberborn.ZiplineSystem.ZiplineCableModel.UpdateModel"] = "display: display: tints, cables, shaft rotation, tooltips",
            ["Timberborn.ZiplineSystemUI.ZiplinePreviewCableRenderer -> Timberborn.ZiplineSystem.ZiplineCableRenderer.CreateCableModel"] = "display: display: tints, cables, shaft rotation, tooltips",
        };
    }
}
