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
        Type registryType = Game("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityRegistry");
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

        // ---- E-6 (refuted): the host's placement check and its own tool previews ----

        test("E-6: the host's check of a played placement runs only inside a replay, where the district preview validator passes", () =>
        {
            // DistrictPreviewsValidator asks whether the previews shown now join two districts' roads (the host's own tool).
            // The mod's prefix answers yes (valid) while a replay runs, and the host checks a played placement only in its
            // replay, so what the host hovers never refuses anyone's placement.
            Type patcher = mod.GetType("BeaverBuddies.Colonies.DistrictPreviewsValidatorReplayPatcher", true)!;
            MethodInfo prefix = patcher.GetMethod("Prefix", All) ?? throw new Exception("DistrictPreviewsValidatorReplayPatcher.Prefix is gone");
            if (!IlScan.Members(prefix).Any(m => m.Name == "get_IsReplayingEvents"))
                throw new Exception("the district preview validator's prefix no longer passes while a replay runs");
            Type placed = mod.GetType("BeaverBuddies.Events.BuildingPlacedEvent", true)!;
            if (!IlScan.Members(Only(placed, "Replay")).Any(m => m.Name == "MayPlace"))
                throw new Exception("the placement's check is no longer made in its replay");
            foreach (MethodInfo method in placed.GetMethods(All).Where(m => m.Name != "Replay" && m.Name != "MayPlace"))
                if (IlScan.Members(method).Any(m => m.Name == "MayPlace")) throw new Exception("MayPlace is also reached from " + method.Name);
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

        // ---- E-8: a beaver with no district joined the nearest district center, whoever's ----

        Type citizenType = Game("Timberborn.GameDistricts", "Timberborn.GameDistricts.Citizen");
        Type assignerType = Game("Timberborn.GameDistricts", "Timberborn.GameDistricts.DistrictCitizenAssigner");

        test("E-8: the game gives a beaver with no district to the nearest district center it can walk to, whoever's", () =>
        {
            var assign = IlScan.Members(Only(assignerType, "AssignToClosestDistrict"));
            foreach (string name in new[] { "get_FinishedDistrictCenters", "IsGloballyReachableFromCitizen", "DistanceToCitizen", "AssignDistrict" })
                if (!assign.Any(m => m.Name == name)) throw new Exception($"AssignToClosestDistrict no longer calls {name}: review the mod's copy of it");
            // It runs each tick for every beaver without a district, right after those cut off lose theirs.
            var tick = IlScan.Members(Only(assignerType, "Tick"));
            if (!tick.Any(m => m.Name == "AssignCharactersWithoutDistricts")) throw new Exception("the assigner's tick changed");
            // Leaving a district drops the beaver's home and job at once: nothing else says whose it was afterwards.
            foreach (var (assembly, method) in new[] { ("Timberborn.DwellingSystem", "UnassignFromHomeIfNotInDistrict"),
                ("Timberborn.WorkSystem", "UnemployIfWorkplaceIsNotInDistrict") })
            {
                if (!Assembly.Load(assembly).GetTypes().Any(t => t.GetMethod(method, All) != null))
                    throw new Exception($"{method} is gone from {assembly}: review E-8's premise");
            }
        });

        test("E-8: a beaver with no district joins only its own colony's district centers, and a hand-over takes it along", () => Quietly(() => WithSingletons(_ =>
        {
            Type citizensType = mod.GetType("BeaverBuddies.Colonies.ColonyCitizens")
                ?? throw new Exception("ColonyCitizens is missing: a beaver with no district joins whichever colony is nearest");
            object citizens = Activator.CreateInstance(citizensType, null, Activator.CreateInstance(registryType))!;
            MethodInfo left = citizensType.GetMethod("Left", All)!, lastOf = citizensType.GetMethod("LastColonyOf", All)!, transfer = citizensType.GetMethod("Transfer", All)!;
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            left.Invoke(citizens, new object?[] { a, 1 });
            left.Invoke(citizens, new object?[] { b, 2 });
            int? Last(Guid id) => (int?)lastOf.Invoke(citizens, new object[] { id });
            if (Last(a) != 1 || Last(b) != 2 || Last(Guid.NewGuid()) != null) throw new Exception("the colony a beaver left is not kept");
            transfer.Invoke(citizens, new object[] { 1, 0 });
            if (Last(a) != 0 || Last(b) != 2) throw new Exception("a hand-over does not take the colony's beavers without a district along");
            left.Invoke(citizens, new object?[] { b, null });
            if (Last(b) != null) throw new Exception("a district without an owner left a record");

            // The copy of the game's assigner: its own colony's district centers only, asked before the walk check.
            Type patcher = mod.GetType("BeaverBuddies.Colonies.ColonyCitizenAssignerPatcher", true)!;
            MethodInfo prefix = patcher.GetMethod("Prefix", All)!;
            var code = IlScan.Instructions(prefix);
            int gate = code.FindIndex(i => i.Calls && i.Member?.Name == "get_IsSeparateColonies");
            int join = code.FindIndex(i => i.Calls && i.Member?.Name == "MayJoin");
            int reach = code.FindIndex(i => i.Calls && i.Member?.Name == "IsGloballyReachableFromCitizen");
            int firstLookup = code.FindIndex(i => i.Calls && (i.Member?.Name == "GetSingleton" || i.Member?.Name == "get_Instance"));
            if (gate < 0 || (firstLookup >= 0 && firstLookup < gate)) throw new Exception("the assigner patch must read the mode's static flag first");
            if (join < 0 || reach < 0 || join > reach) throw new Exception("the assigner patch does not keep a beaver to its own colony's district centers");
            if (!patcher.GetCustomAttributesData().Any(d => d.AttributeType.Name == "HarmonyPatch" && d.ConstructorArguments.Count == 2
                && Equals(d.ConstructorArguments[0].Value, assignerType) && Equals(d.ConstructorArguments[1].Value, "AssignToClosestDistrict")))
                throw new Exception("the assigner patch does not target DistrictCitizenAssigner.AssignToClosestDistrict");
            // The colony left is read from the district center itself: a deleted one's (OwnerOfDistrict says nobody's).
            Type leave = mod.GetType("BeaverBuddies.Colonies.ColonyCitizenLeavePatcher", true)!;
            if (!leave.GetCustomAttributesData().Any(d => d.AttributeType.Name == "HarmonyPatch" && d.ConstructorArguments.Count == 2
                && Equals(d.ConstructorArguments[0].Value, citizenType) && Equals(d.ConstructorArguments[1].Value, "UnassignDistrict")))
                throw new Exception("the colony a beaver leaves is not read in Citizen.UnassignDistrict");
            var reads = IlScan.Members(leave.GetMethod("Prefix", All)!);
            if (reads.Any(m => m.Name == "OwnerOfDistrict")) throw new Exception("OwnerOfDistrict is null for a deleted district center: read its DistrictOwner");
        })));

        // ---- H1 (sweep 7): the id-naming events of this area, played after what they name is gone ----

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
