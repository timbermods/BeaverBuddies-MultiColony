#nullable enable
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: reviewer E: the colony lifecycle at late-game size (founding, stamps, handover, stewards, absence, slots) and the simulation events outside automation (placement, demolition, planting and cutting, migration, distribution).
internal static class RcColonyRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");

        // Game objects made without their constructors, holding only the fields a check sets (the way WonderChecks and
        // PlantingLevelChecks run the game's code outside the game).
        object Blank(Type type) => RuntimeHelpers.GetUninitializedObject(type);
        FieldInfo FieldOf(Type type, string name)
        {
            for (Type? t = type; t != null; t = t.BaseType)
            {
                FieldInfo? field = t.GetField(name, All);
                if (field != null) return field;
            }
            throw new Exception($"{type.FullName}.{name} is gone");
        }
        void Set(object target, string field, object? value) => FieldOf(target.GetType(), field).SetValue(target, value);
        object? Get(object target, string field) => FieldOf(target.GetType(), field).GetValue(target);
        Exception? Thrown(Action run)
        {
            try { run(); return null; }
            catch (TargetInvocationException e) { return e.InnerException ?? e; }
            catch (Exception e) { return e; }
        }

        // Plugin.Log* would otherwise reach Unity's native logger.
        FieldInfo pluginLogger = mod.GetType("BeaverBuddies.Plugin", true)!.GetField("logger", BindingFlags.NonPublic | BindingFlags.Static)!;
        Type loggerType = mod.GetType("BeaverBuddies.Util.Logging.ILogger", true)!;
        void Quietly(Action run)
        {
            object? previous = pluginLogger.GetValue(null);
            pluginLogger.SetValue(null, DispatchProxy.Create(loggerType, typeof(QuietLoggerProxy)));
            try { run(); } finally { pluginLogger.SetValue(null, previous); }
        }

        // The mod's singletons (SingletonManager) as a test wants them, and as they were afterwards.
        var singletonMap = (IDictionary)mod.GetType("BeaverBuddies.SingletonManager", true)!.GetField("map", All)!.GetValue(null)!;
        void WithSingletons(Action<IDictionary> run)
        {
            var saved = singletonMap.Keys.Cast<object>().Select(key => (key, value: singletonMap[key])).ToList();
            singletonMap.Clear();
            try { run(singletonMap); }
            finally
            {
                singletonMap.Clear();
                foreach (var (key, value) in saved) singletonMap[key] = value;
            }
        }

        Type contextType = mod.GetType("BeaverBuddies.Events.IReplayContext", true)!;
        object Event(string name, params (string field, object? value)[] fields)
        {
            Type type = mod.GetType("BeaverBuddies.Events." + name) ?? mod.GetType("BeaverBuddies.Colonies." + name, true)!;
            object e = Activator.CreateInstance(type, true)!;
            foreach (var (field, value) in fields) type.GetField(field, All)!.SetValue(e, value);
            return e;
        }
        void Replay(object replayEvent, object context) =>
            replayEvent.GetType().GetMethod("Replay", BindingFlags.Public | BindingFlags.Instance)!.Invoke(replayEvent, new[] { context });

        // ---- E-1: a planting mark of a plant the host's game does not have ----

        // The game's planting check as the replay reaches it (PlantingSelectionService, PlantingAreaValidator,
        // SpawnValidationService, TemplateNameMapper), knowing only these plants, with nothing standing anywhere.
        Type mapperType = Game("Timberborn.TemplateSystem", "Timberborn.TemplateSystem.TemplateNameMapper");
        Type templateSpecType = Game("Timberborn.TemplateSystem", "Timberborn.TemplateSystem.TemplateSpec");
        Type spawnType = Game("Timberborn.NaturalResources", "Timberborn.NaturalResources.SpawnValidationService");
        Type plantingValidatorType = Game("Timberborn.Planting", "Timberborn.Planting.PlantingAreaValidator");
        Type selectionType = Game("Timberborn.PlantingUI", "Timberborn.PlantingUI.PlantingSelectionService");
        Type blockServiceType = Game("Timberborn.BlockSystem", "Timberborn.BlockSystem.IBlockService");
        Type vector3Int = Assembly.Load("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int", true)!;
        object PlantingSelection(params string[] plants)
        {
            object mapper = Blank(mapperType);
            var templates = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), templateSpecType))!;
            foreach (string plant in plants) templates[plant] = Blank(templateSpecType);
            Set(mapper, "_templates", templates);
            object spawn = Blank(spawnType);
            Set(spawn, "_templateNameMapper", mapper);
            object validator = Blank(plantingValidatorType);
            Set(validator, "_spawnValidationService", spawn);
            // Answers null to everything: no plant stands on any tile.
            Set(validator, "_blockService", DispatchProxy.Create(blockServiceType, typeof(QuietLoggerProxy)));
            object selection = Blank(selectionType);
            Set(selection, "_plantingAreaValidator", validator);
            return selection;
        }

        test("E-1: the game's planting check throws for a plant it does not know (why the host must refuse one)", () =>
        {
            object validator = Get(PlantingSelection("Carrot"), "_plantingAreaValidator")!;
            Exception? error = Thrown(() => plantingValidatorType.GetMethod("CanPlant")!.Invoke(validator,
                new[] { Activator.CreateInstance(vector3Int, 1, 1, 1)!, "ModCrop" }));
            if (error?.GetType().Name != "TemplateMappingException")
                throw new Exception($"the game's CanPlant gave {error?.GetType().Name ?? "no exception"} for an unknown plant: E-1 may be unneeded");
        });

        test("E-1: the host refuses a mark of a plant its game lacks, and a guest meeting one leaves quietly", () => Quietly(() =>
        {
            object selection = PlantingSelection("Carrot");
            var context = (SingletonContextProxy)DispatchProxy.Create(contextType, typeof(SingletonContextProxy));
            context.Singleton = selection;
            object Mark(string? plant) => Event("PlantingAreaMarkedEvent", ("prefabName", plant),
                ("coordinates", Activator.CreateInstance(typeof(List<>).MakeGenericType(vector3Int))));
            Type eventType = Mark("Carrot").GetType();
            MethodInfo names = eventType.GetMethod("NamesUnknownPlant", All)
                ?? throw new Exception("PlantingAreaMarkedEvent.NamesUnknownPlant is missing: a plant the host lacks is played");
            bool Unknown(string? plant) => (bool)names.Invoke(Mark(plant), new object[] { context })!;
            if (!Unknown("ModCrop")) throw new Exception("a plant this game lacks is not noticed");
            if (Unknown("Carrot")) throw new Exception("a plant this game has is taken for unknown");
            if (Unknown("Unmark")) throw new Exception("unmarking, which names no plant, is taken for an unknown plant");
            if (!Unknown(null)) throw new Exception("a mark naming no plant is played (the game throws on it too)");

            // A guest: found before anything is marked, as missing content, so only this guest leaves (ReplayService).
            Exception? error = Thrown(() => Replay(Mark("ModCrop"), context));
            if (error?.GetType().Name != "MissingContentException")
                throw new Exception($"a guest's replay of a plant it lacks gave {error?.GetType().Name ?? "no exception"}, not MissingContentException");

            // The host: refused before it is played, like a building it lacks.
            WithSingletons(map =>
            {
                Type rulesType = mod.GetType("BeaverBuddies.Colonies.ColonyRulesService", true)!;
                Activator.CreateInstance(rulesType, new object?[rulesType.GetConstructors().Single().GetParameters().Length]);
                Type replayServiceType = mod.GetType("BeaverBuddies.ReplayService", true)!;
                object replayService = Blank(replayServiceType);
                Set(replayService, "singletons", new List<object> { selection });
                map[replayServiceType] = replayService;
                MethodInfo allow = rulesType.GetMethod("AllowOnHost", All)!;
                object?[] args = { Mark("ModCrop"), null };
                if ((bool)allow.Invoke(null, args)!) throw new Exception("the host plays a mark of a plant its game lacks");
                if (args[1]?.ToString() != "HostRefused") throw new Exception("refused as " + args[1]);
                args = new object?[] { Mark("Carrot"), null };
                if (!(bool)allow.Invoke(null, args)!) throw new Exception("the host refuses a plant it has: " + args[1]);
                args = new object?[] { Mark("Unmark"), null };
                if (!(bool)allow.Invoke(null, args)!) throw new Exception("the host refuses unmarking: " + args[1]);
            });
        }));

        // ---- E-2: a good distribution change for a good the host's game does not have ----

        Type districtSettingType = Game("Timberborn.DistributionSystem", "Timberborn.DistributionSystem.DistrictDistributionSetting");
        Type goodSettingType = Game("Timberborn.DistributionSystem", "Timberborn.DistributionSystem.GoodDistributionSetting");
        Type goodSpecType = Game("Timberborn.Goods", "Timberborn.Goods.GoodSpec");
        Type importOptionType = Game("Timberborn.DistributionSystem", "Timberborn.DistributionSystem.ImportOption");
        object DistrictSettings(params string[] goods)
        {
            object district = Blank(districtSettingType);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(goodSettingType))!;
            foreach (string good in goods)
            {
                object spec = Blank(goodSpecType);
                goodSpecType.GetProperty("Id")!.GetSetMethod(true)!.Invoke(spec, new object[] { good });
                list.Add(goodSettingType.GetMethod("Create")!.Invoke(null, new[] { spec, 0f, Enum.Parse(importOptionType, "Auto"), 0f }));
            }
            Set(district, "_goodDistributionSettings", list);
            return district;
        }

        test("E-2: the game's district lookup throws for a good it does not have (why the replay must not use it)", () =>
        {
            Exception? error = Thrown(() => districtSettingType.GetMethod("GetGoodDistributionSetting")!.Invoke(DistrictSettings("Water"), new object[] { "ModGood" }));
            if (!(error is ArgumentException))
                throw new Exception($"the game's GetGoodDistributionSetting gave {error?.GetType().Name ?? "no exception"} for an unknown good: E-2 may be unneeded");
        });

        test("E-2: a distribution change for a good this game lacks is skipped, not thrown, on every computer", () =>
        {
            Type eventType = mod.GetType("BeaverBuddies.Events.GoodDistributionSettingChangedEvent", true)!;
            MethodInfo settingFor = eventType.GetMethod("SettingFor", All)
                ?? throw new Exception("GoodDistributionSettingChangedEvent.SettingFor is missing: the replay throws for a good the host lacks");
            object district = DistrictSettings("Water", "Logs");
            if (settingFor.Invoke(null, new[] { district, "ModGood" }) != null) throw new Exception("a good this game lacks was found");
            object? logs = settingFor.Invoke(null, new[] { district, "Logs" });
            if (logs == null || (string)goodSettingType.GetProperty("GoodId")!.GetValue(logs)! != "Logs") throw new Exception("a good this game has was not found");
            // The replay looks its good up through it, never through the game's throwing lookup.
            var calls = IlScan.Members(eventType.GetMethod("Replay")!);
            if (IlScan.Names(calls, districtSettingType.FullName!, "GetGoodDistributionSetting"))
                throw new Exception("the replay still calls DistrictDistributionSetting.GetGoodDistributionSetting, which throws for an unknown good");
            if (!calls.Any(m => m.Name == "SettingFor")) throw new Exception("the replay does not look the good up with SettingFor");
        });

        // ---- E-6: the host's placement check read its own tool previews ----

        Type blockObjectType = Game("Timberborn.BlockSystem", "Timberborn.BlockSystem.BlockObject");
        Type blockValidatorType = Game("Timberborn.BlockSystem", "Timberborn.BlockSystem.BlockValidator");
        Type validationServiceType = Game("Timberborn.BlockSystem", "Timberborn.BlockSystem.BlockObjectValidationService");
        Type validatorType = Game("Timberborn.BlockSystem", "Timberborn.BlockSystem.IBlockObjectValidator");
        Type positionedBlocksType = Game("Timberborn.BlockSystem", "Timberborn.BlockSystem.PositionedBlocks");
        Type previewsValidatorType = Game("Timberborn.GameDistrictsUI", "Timberborn.GameDistrictsUI.DistrictPreviewsValidator");

        test("E-6: the game's district validator asks the preview road graph, which holds the host's own previews", () =>
        {
            // BlockObject.IsValid, whose body the mod repeats less one validator (ToolEvents, IsValidWithoutHostPreviews).
            var isValid = IlScan.Members(blockObjectType.GetMethod("IsValid", Type.EmptyTypes)!);
            if (!IlScan.Names(isValid, blockValidatorType.FullName!, "BlocksValid") || !IlScan.Names(isValid, validationServiceType.FullName!, "IsValid"))
                throw new Exception("BlockObject.IsValid is no longer the blocks check then every validator: review IsValidWithoutHostPreviews");
            var conflict = IlScan.Members(Only(previewsValidatorType, "IsPreviewDistrictInConflict"));
            if (!conflict.Any(m => m.Name == "IsPreviewDistrictInConflict" && m.DeclaringType?.Name == "IDistrictService"))
                throw new Exception("DistrictPreviewsValidator no longer asks IDistrictService.IsPreviewDistrictInConflict: E-6 may be unneeded");
        });

        test("E-6: the host's check of a played placement asks every validator but the one that reads its own previews", () =>
        {
            Type placed = mod.GetType("BeaverBuddies.Events.BuildingPlacedEvent", true)!;
            MethodInfo check = placed.GetMethod("IsValidWithoutHostPreviews", All)
                ?? throw new Exception("BuildingPlacedEvent.IsValidWithoutHostPreviews is missing: the host's check reads its own tool previews");
            object BlockObject(params object[] validators)
            {
                object blockObject = Blank(blockObjectType);
                Set(blockObject, "_blockValidator", Blank(blockValidatorType));
                // No blocks: the blocks check passes without looking at the map.
                object positioned = Blank(positionedBlocksType);
                FieldInfo all = FieldOf(positionedBlocksType, "_all");
                all.SetValue(positioned, all.FieldType.GetField("Empty", BindingFlags.Public | BindingFlags.Static)!.GetValue(null));
                blockObjectType.GetProperty("PositionedBlocks")!.GetSetMethod(true)!.Invoke(blockObject, new[] { positioned });
                object service = Blank(validationServiceType);
                FieldInfo list = FieldOf(validationServiceType, "_blockObjectValidators");
                var items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(validatorType))!;
                foreach (object validator in validators) items.Add(validator);
                MethodInfo createRange = list.FieldType.Assembly.GetType("System.Collections.Immutable.ImmutableArray", true)!
                    .GetMethods().Single(m => m.Name == "CreateRange" && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType.IsGenericType
                        && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)).MakeGenericMethod(validatorType);
                list.SetValue(service, createRange.Invoke(null, new object[] { items }));
                Set(blockObject, "_blockObjectValidationService", service);
                return blockObject;
            }
            FixedValidatorProxy Validator(bool answer)
            {
                var validator = (FixedValidatorProxy)DispatchProxy.Create(validatorType, typeof(FixedValidatorProxy));
                validator.Answer = answer;
                return validator;
            }
            // A district validator made without its services: asked, it throws (as it would say "in conflict" while the
            // host hovers a preview that joins two districts' roads).
            var yes = Validator(true);
            if (!(bool)check.Invoke(null, new[] { BlockObject(Blank(previewsValidatorType), yes) })!)
                throw new Exception("refused with the district validator left out");
            if (yes.Asked != 1) throw new Exception("the other validators were not asked");
            var no = Validator(false);
            if ((bool)check.Invoke(null, new[] { BlockObject(Validator(true), no, Blank(previewsValidatorType)) })!)
                throw new Exception("a validator's refusal was ignored");
            // The replay's check uses it, not the game's BlockObject.IsValid.
            var calls = IlScan.Members(Only(placed, "IsPlacementValidTimed"));
            if (IlScan.Names(calls, blockObjectType.FullName!, "IsValid")) throw new Exception("the host's check still calls BlockObject.IsValid");
            if (!calls.Any(m => m.Name == "IsValidWithoutHostPreviews")) throw new Exception("the host's check does not use IsValidWithoutHostPreviews");
        });

        // ---- E-5: a shared game's unlock paid twice ----

        test("E-5: the game's Unlock pays again for a building already unlocked; a shared game's replay skips it", () =>
        {
            Type unlocking = Game("Timberborn.ScienceSystem", "Timberborn.ScienceSystem.BuildingUnlockingService");
            var unlock = IlScan.Members(Only(unlocking, "Unlock"));
            if (unlock.Any(m => m.Name == "Unlocked") || !unlock.Any(m => m.Name == "SubtractPoints"))
                throw new Exception("the game's BuildingUnlockingService.Unlock changed: review E-5");
            Type replayEvent = mod.GetType("BeaverBuddies.Events.BuildingUnlockedEvent", true)!;
            var replay = IlScan.Instructions(replayEvent.GetMethod("Replay")!);
            // Unlocked is asked outside the per-colony lambda too (the shared set), for all but the profile-remembered buildings.
            bool asksUnlockedDirectly = replay.Any(i => i.Calls && i.Member?.Name == "Unlocked" && i.Member.DeclaringType == unlocking);
            bool excludesOnce = replay.Any(i => i.Calls && i.Member is MethodInfo m && m.Name == "HasSpec" && m.IsGenericMethod
                && m.GetGenericArguments()[0].Name == "UnlockableOnceSpec");
            if (!asksUnlockedDirectly || !excludesOnce)
                throw new Exception("a shared game's unlock replay does not skip a building already unlocked (all but UnlockableOnceSpec)");
        });

        // ---- E-7: a shared game split by a founding left its marks nobody's ----

        test("E-7: splitting a shared game makes its standing marks the first colony's, and a new game's changes none", () => Quietly(() => WithSingletons(_ =>
        {
            Type plantingServiceType = Game("Timberborn.Planting", "Timberborn.Planting.PlantingService");
            Type plantingMapType = Game("Timberborn.Planting", "Timberborn.Planting.PlantingMap");
            Type cuttingAreaType = Game("Timberborn.Forestry", "Timberborn.Forestry.TreeCuttingArea");
            Type marksType = mod.GetType("BeaverBuddies.Colonies.ColonyMarks", true)!;
            Type modeType = mod.GetType("BeaverBuddies.Colonies.ColonyModeService", true)!;
            FieldInfo separateNow = modeType.GetField("separateNow", All)!;
            object wasSeparate = separateNow.GetValue(null)!;
            object V(int x, int y, int z) => Activator.CreateInstance(vector3Int, x, y, z)!;
            try
            {
                foreach (bool newGame in new[] { false, true })
                {
                    // A shared colony's two fields and two trees marked for cutting; one field already a colony's.
                    object map = Activator.CreateInstance(plantingMapType, V(8, 8, 4))!;
                    foreach (object tile in new[] { V(1, 1, 1), V(2, 2, 1) })
                        plantingMapType.GetMethod("SetResource", new[] { vector3Int, typeof(string) })!.Invoke(map, new[] { tile, "Carrot" });
                    object planting = Blank(plantingServiceType);
                    Set(planting, "_plantingMap", map);
                    object cutting = Blank(cuttingAreaType);
                    var area = (IEnumerable)Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(vector3Int))!;
                    foreach (object tile in new[] { V(3, 3, 1), V(4, 4, 1) }) area.GetType().GetMethod("Add")!.Invoke(area, new[] { tile });
                    Set(cutting, "_cuttingArea", area);
                    object marks = Activator.CreateInstance(marksType, null, planting, cutting)!;
                    marksType.GetMethod("SetPlanting", All)!.Invoke(marks, new[] { V(2, 2, 1), (object)1 });
                    object mode = Activator.CreateInstance(modeType, new object?[] { null })!;
                    modeType.GetMethod("Enable")!.Invoke(mode, new object?[] { null, "a check", false, newGame });
                    int? Owner(string of, object tile) => (int?)marksType.GetMethod(of)!.Invoke(marks, new[] { tile });
                    int? expected = newGame ? null : 0;
                    if (Owner("PlantingOwner", V(1, 1, 1)) != expected || Owner("CuttingOwner", V(3, 3, 1)) != expected || Owner("CuttingOwner", V(4, 4, 1)) != expected)
                        throw new Exception(newGame ? "a new game's start gave marks an owner"
                            : "a split shared game's marks are still nobody's: the new colonies work the first colony's fields and forests");
                    if (Owner("PlantingOwner", V(2, 2, 1)) != 1) throw new Exception("a mark that had a colony was taken from it");
                }
            }
            finally { separateNow.SetValue(null, wasSeparate); }
        })));

        // ---- H1 (sweep 7): the id-naming events of this area, played after what they name is gone ----

        Type registryType = Game("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityRegistry");
        test("E-H1: every district, deletion and duplication event naming things deleted earlier in the tick skips them", () => Quietly(() =>
        {
            string Id() => Guid.NewGuid().ToString();
            var events = new[]
            {
                Event("BuildingsDeconstructedEvent", ("entityIDs", new List<string> { Id(), Id(), "not an id" })),
                Event("ManualMigrationEvent", ("fromDistrictID", Id()), ("toDistrictID", Id()), ("amount", 10)),
                Event("SetDistrictMinimumPopulationEvent", ("districtEntityID", Id()), ("minimumPopulation", 5)),
                Event("SetDistrictMigrationToggledEvent", ("districtEntityID", Id()), ("allow", true)),
                Event("GoodDistributionSettingChangedEvent", ("districtEntityID", Id()), ("goodID", "Logs")),
                Event("DuplicationEvent", ("sourceEntityID", Id()), ("targetEntityID", Id())),
            };
            foreach (object replayEvent in events)
            {
                var context = (ReplayContextProxy)DispatchProxy.Create(contextType, typeof(ReplayContextProxy));
                context.Registry = Activator.CreateInstance(registryType)!;
                context.RegistryType = registryType;
                Exception? error = Thrown(() => Replay(replayEvent, context));
                if (error != null) throw new Exception($"{replayEvent.GetType().Name} threw {error.GetType().Name}: {error.Message}");
            }
        }));

        test("E-H1: the colony lifecycle's events skip without their services (a load, a scene without them)", () => Quietly(() => WithSingletons(_ =>
        {
            var events = new[]
            {
                Event("ColonyHandoverEvent", ("fromSlot", 1), ("toSlot", 0)),
                Event("ColonyPresenceEvent", ("day", 3), ("presentSlots", null)),
                Event("StewardGrantedEvent", ("colonySlot", 1), ("stewardPlayerId", "local:x")),
                Event("StewardRevokedEvent", ("colonySlot", 1)),
                Event("ActAsColonyEvent", ("colonySlot", 1)),
                Event("PlayerHelloEvent", ("playerId", "local:x")),
                Event("FoundColonyEvent"),
            };
            var context = (ReplayContextProxy)DispatchProxy.Create(contextType, typeof(ReplayContextProxy));
            context.RegistryType = registryType;
            foreach (object replayEvent in events)
            {
                Exception? error = Thrown(() => Replay(replayEvent, context));
                if (error != null) throw new Exception($"{replayEvent.GetType().Name} threw {error.GetType().Name}: {error.Message}");
            }
        })));
    }
}

/// <summary>A placement validator with a fixed answer, counting how often it is asked.</summary>
public class FixedValidatorProxy : DispatchProxy
{
    public bool Answer;
    public int Asked;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Asked++;
        return Answer;
    }
}
