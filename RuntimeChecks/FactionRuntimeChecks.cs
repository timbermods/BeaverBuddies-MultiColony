#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Mixed factions (design/MIXED-FACTIONS-PLAN.md), against the compiled mod, the game's assemblies and the game's own data
// (StreamingAssets/Modding/Blueprints.zip and UI.zip): the facts about the two factions the design rests on, every game
// member the feature hooks or reads, the classes of the waiting room's faction switcher, and the new event fields.
internal static class FactionRuntimeChecks
{
    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly string[] Factions = { "Folktails", "IronTeeth" };

    public static void Run(Assembly mod, string managedPath, string modDirectory, Action<string, Action> test)
    {
        string blueprintsPath = Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "Blueprints.zip"));
        var cache = new Dictionary<string, JsonNode?>();
        ZipArchive? zip = null;
        JsonNode? Blueprint(string path)
        {
            if (cache.TryGetValue(path, out JsonNode? node)) return node;
            zip ??= ZipFile.OpenRead(blueprintsPath);
            ZipArchiveEntry? entry = zip.GetEntry(path.EndsWith(".json") ? path : path + ".json");
            if (entry == null) return cache[path] = null;
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            return cache[path] = JsonNode.Parse(reader.ReadToEnd());
        }
        JsonNode Need(string path) => Blueprint(path) ?? throw new Exception("the game has no " + path);
        IEnumerable<string> Entries(string folder)
        {
            zip ??= ZipFile.OpenRead(blueprintsPath);
            return zip.Entries.Select(e => e.FullName).Where(n => n.StartsWith(folder) && n.EndsWith(".json")).ToList();
        }
        JsonNode Faction(string id) => Need($"Factions/Faction.{id}.blueprint.json")["FactionSpec"]!;
        List<string> Strings(JsonNode? array) => array?.AsArray().Select(n => n!.GetValue<string>()).ToList() ?? new List<string>();
        // Every blueprint path a faction's template collections list.
        List<string> TemplatePaths(string faction)
        {
            var ids = Strings(Faction(faction)["TemplateCollectionIds"]);
            var paths = new List<string>();
            foreach (string file in Entries("TemplateCollections/"))
            {
                JsonNode spec = Need(file)["TemplateCollectionSpec"]!;
                if (ids.Contains(spec["CollectionId"]!.GetValue<string>())) paths.AddRange(Strings(spec["Blueprints"]));
            }
            return paths.Distinct().ToList();
        }
        List<string> Collection(string folder, string spec, string list, IEnumerable<string> ids)
        {
            var items = new List<string>();
            foreach (string file in Entries(folder))
            {
                JsonNode s = Need(file)[spec]!;
                if (ids.Contains(s["CollectionId"]!.GetValue<string>())) items.AddRange(Strings(s[list]));
            }
            return items.Distinct().ToList();
        }
        List<string> GoodsOf(string faction) =>
            Collection("GoodCollections/", "GoodCollectionSpec", "Goods", Strings(Faction(faction)["GoodCollectionIds"]).Append("Common"));

        test("Factions: the game has Folktails and Iron Teeth, and their district centers have the same footprint and entrance", () =>
        {
            var ids = Entries("Factions/").Select(f => Need(f)["FactionSpec"]!["Id"]!.GetValue<string>()).OrderBy(i => i).ToList();
            if (!ids.SequenceEqual(Factions)) throw new Exception("the factions are " + string.Join(", ", ids));
            JsonNode? first = null;
            foreach (string id in Factions)
            {
                string start = Faction(id)["StartingBuildingId"]!.GetValue<string>();
                string file = Entries("Buildings/").FirstOrDefault(f => Need(f)["TemplateSpec"]?["TemplateName"]?.GetValue<string>() == start)
                    ?? throw new Exception("no blueprint is " + start);
                JsonNode block = Need(file)["BlockObjectSpec"]!;
                var shape = new JsonObject { ["Size"] = block["Size"]!.DeepClone(), ["Entrance"] = block["Entrance"]!.DeepClone() };
                if (first == null) first = shape;
                else if (!JsonNode.DeepEquals(first, shape)) throw new Exception("the district centers differ: " + first + " / " + shape);
            }
        });

        test("Factions: only the two beavers and the two dev buildings are listed by both factions, and no template name repeats", () =>
        {
            var both = TemplatePaths("Folktails").Intersect(TemplatePaths("IronTeeth")).OrderBy(p => p).ToList();
            var expected = new[] { "Buildings/Power/DevPowerGenerator/DevPowerGenerator.blueprint", "Buildings/Water/DevWaterSource/DevWaterSource.blueprint",
                "Characters/Beaver/BeaverAdult.blueprint", "Characters/Beaver/BeaverChild.blueprint" };
            if (!both.SequenceEqual(expected)) throw new Exception("shared: " + string.Join(", ", both));
            var names = new Dictionary<string, string>();
            foreach (string path in TemplatePaths("Folktails").Concat(TemplatePaths("IronTeeth")).Distinct())
            {
                string? name = Blueprint(path)?["TemplateSpec"]?["TemplateName"]?.GetValue<string>();
                if (name == null) continue;
                if (names.TryGetValue(name, out string? other) && other != path) throw new Exception($"{name} is both {other} and {path}");
                names[name] = path;
            }
        });

        test("Factions: each faction has one bot, one set of power-shaft parts and its critical bot need, and no other", () =>
        {
            foreach (string id in Factions)
            {
                var paths = TemplatePaths(id);
                if (paths.Count(p => Blueprint(p)?["BotSpec"] != null) != 1) throw new Exception(id + " has not one bot");
                if (paths.Count(p => Blueprint(p)?["ModularShaftPartsSpec"] != null) != 1) throw new Exception(id + " has not one set of shaft parts");
            }
            var folktails = Collection("NeedCollections/", "NeedCollectionSpec", "Needs", Strings(Faction("Folktails")["NeedCollectionIds"]));
            var ironTeeth = Collection("NeedCollections/", "NeedCollectionSpec", "Needs", Strings(Faction("IronTeeth")["NeedCollectionIds"]));
            if (!folktails.Contains("Biofuel") || ironTeeth.Contains("Biofuel") || !ironTeeth.Contains("Energy") || folktails.Contains("Energy"))
                throw new Exception("the critical bot needs moved (why each character gets its own faction's needs)");
        });

        test("Factions: the materials, decals and worker outfits of both factions can all load together", () =>
        {
            var materials = Entries("MaterialCollections/").SelectMany(f => Strings(Need(f)["MaterialCollectionSpec"]!["Materials"])).ToList();
            var byName = materials.GroupBy(p => p.Split('/').Last()).Where(g => g.Distinct().Count() > 1).Select(g => g.Key).ToList();
            if (byName.Count > 0) throw new Exception("material names repeat: " + string.Join(", ", byName));
            var outfits = new HashSet<string>();
            foreach (string file in Entries("WorkerOutfits/"))
            {
                JsonNode? spec = Need(file)["WorkerOutfitSpec"];
                if (spec == null) continue;
                string key = spec["FactionId"]!.GetValue<string>() + "|" + spec["Id"]!.GetValue<string>() + "|" + spec["WorkerType"]!.GetValue<string>();
                if (!outfits.Add(key)) throw new Exception("two worker outfits are " + key);
            }
            if (outfits.Count == 0) throw new Exception("found no worker outfits");
        });

        test("Factions: each faction stores boxes, piles and liquids, and exactly 17 goods may cross between them", () =>
        {
            foreach (string id in Factions)
            {
                var types = TemplatePaths(id).Select(p => Blueprint(p)?["StockpileSpec"]?["WhitelistedGoodType"]?.GetValue<string>())
                    .Where(t => t != null).Distinct().OrderBy(t => t).ToList();
                if (!types.SequenceEqual(new[] { "Box", "Liquid", "Pileable" })) throw new Exception(id + " stores " + string.Join(", ", types));
            }
            // The mod's own FactionSets on the game's data (FactionCatalog builds it the same way).
            Type sets = mod.GetType("BeaverBuddies.Factions.FactionSets", true)!;
            var byFaction = Factions.ToDictionary(f => f, f => (IEnumerable<string>)Strings(Faction(f)["GoodCollectionIds"]));
            var byCollection = Entries("GoodCollections/").Select(f => Need(f)["GoodCollectionSpec"]!)
                .GroupBy(s => s["CollectionId"]!.GetValue<string>()).ToDictionary(g => g.Key, g => (IEnumerable<string>)g.SelectMany(s => Strings(s["Goods"])).ToList());
            object built = sets.GetMethod("FromCollections")!.Invoke(null, new object[] { Factions, new[] { "Common" },
                (IReadOnlyDictionary<string, IEnumerable<string>>)byFaction, (IReadOnlyDictionary<string, IEnumerable<string>>)byCollection })!;
            var shared = ((IEnumerable<string>)sets.GetMethod("Shared")!.Invoke(built, new object[] { "Folktails", "IronTeeth" })!).OrderBy(g => g).ToList();
            var expected = new[] { "Badwater", "Berries", "BotChassis", "BotHead", "BotLimb", "Dirt", "Explosives", "Extract", "Fireworks", "Gear",
                "Log", "MetalBlock", "PineResin", "Plank", "ScrapMetal", "TreatedPlank", "Water" };
            if (!shared.SequenceEqual(expected)) throw new Exception("the goods both factions have: " + string.Join(", ", shared));
            var direct = GoodsOf("Folktails").Intersect(GoodsOf("IronTeeth")).OrderBy(g => g).ToList();
            if (!direct.SequenceEqual(expected)) throw new Exception("the collections disagree with FactionSets: " + string.Join(", ", direct));
        });

        test("Factions: every plantable is one faction's or common, and both factions' Trading Posts ship with the mod", () =>
        {
            var folktails = TemplatePaths("Folktails").Where(p => Blueprint(p)?["PlantableSpec"] != null).ToList();
            var ironTeeth = TemplatePaths("IronTeeth").Where(p => Blueprint(p)?["PlantableSpec"] != null).ToList();
            if (folktails.Intersect(ironTeeth).Any()) throw new Exception("a plantable is listed by both factions");
            if (!folktails.Any(p => p.Contains("Carrot")) || !ironTeeth.Any(p => p.Contains("Corn"))) throw new Exception("the crops moved");
            foreach (string id in Factions)
            {
                string file = Path.Combine(modDirectory, "Buildings", "DistrictManagement", "MultiColonyTradingPost", $"MultiColonyTradingPost.{id}.blueprint.json");
                if (!File.Exists(file)) throw new Exception("the built mod has no Trading Post for " + id);
            }
        });

        test("Factions: every game member the feature hooks or reads is still there", () =>
        {
            var members = new (string assembly, string type, string[] names)[]
            {
                ("Timberborn.GameFactionSystem", "Timberborn.GameFactionSystem.FactionService", new[] { "Load", "Current", "_factionSpecService", "_mapEditorMode", "_sceneLoader", "_singletonLoader" }),
                ("Timberborn.GameFactionSystem", "Timberborn.GameFactionSystem.FactionBlueprintModifierProvider", new[] { "Initialize" }),
                ("Timberborn.GameFactionSystem", "Timberborn.GameFactionSystem.FactionNeedService", new[] { "GetBeaverNeeds", "GetBotNeeds" }),
                ("Timberborn.FactionSystem", "Timberborn.FactionSystem.FactionUnlockingService", new[] { "IsLocked" }),
                ("Timberborn.FactionSystem", "Timberborn.FactionSystem.FactionSpec", new[] { "Logo", "Avatar", "ChildAvatar", "BotAvatar", "Textures", "ChildTextures",
                    "PathMaterial", "BaseWoodMaterial", "StartingBuildingId", "TemplateCollectionIds", "GoodCollectionIds", "NeedCollectionIds",
                    "MaterialCollectionIds", "BlueprintModifiers", "DescriptionLocKey", "GameOverFlavor", "GameOverMessage" }),
                ("Timberborn.TemplateCollectionSystem", "Timberborn.TemplateCollectionSystem.TemplateCollectionService", new[] { "Load", "AllTemplates" }),
                ("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.WorldEntitiesLoader", new[] { "InstantiateEntity" }),
                ("Timberborn.NeedSystem", "Timberborn.NeedSystem.NeedManager", new[] { "GetNeeds" }),
                ("Timberborn.Wellbeing", "Timberborn.Wellbeing.WellbeingLimitService", new[] { "GetMaxWellbeing" }),
                ("Timberborn.Beavers", "Timberborn.Beavers.BeaverTextureSetter", new[] { "InitializeEntity", "BaseMapId", "_randomNumberGenerator" }),
                ("Timberborn.Beavers", "Timberborn.Beavers.BeaverFactory", new[] { "CreateAdultFromChild", "CreateAdult", "CreateChild" }),
                ("Timberborn.Bots", "Timberborn.Bots.BotFactory", new[] { "Load", "Create", "_botTemplate", "_templateInstantiator" }),
                ("Timberborn.Reproduction", "Timberborn.Reproduction.NewbornSpawner", new[] { "SpawnAdult", "SpawnChild" }),
                ("Timberborn.BotsUpkeep", "Timberborn.BotUpkeep.BotManufactory", new[] { "OnProductionFinished" }),
                ("Timberborn.BeaversUI", "Timberborn.BeaversUI.BeaverGeneratorTool", new[] { "PlaceBeavers" }),
                ("Timberborn.BotsUI", "Timberborn.BotsUI.BotGeneratorTool", new[] { "PlaceBots" }),
                ("Timberborn.BeaversUI", "Timberborn.BeaversUI.BeaverEntityBadge", new[] { "GetEntityAvatar", "_child", "_contaminable" }),
                ("Timberborn.BotsUI", "Timberborn.BotsUI.BotEntityBadge", new[] { "GetEntityAvatar" }),
                ("Timberborn.CharactersUI", "Timberborn.CharactersUI.CharacterButton", new[] { "ShowAdultEmpty", "ShowChildEmpty", "ShowBotEmpty", "SetBackground", "ChangeOnClickAction" }),
                ("Timberborn.WorkerOutfitSystem", "Timberborn.WorkerOutfitSystem.WorkerOutfitService", new[] { "TryGetOutfitSpec" }),
                ("Timberborn.ModularShafts", "Timberborn.ModularShafts.ShaftFrameFactory", new[] { "Load", "Instantiate", "_root", "_rootObjectProvider", "_templateService",
                    "_optimizedPrefabInstantiator", "_shaftBase", "_shaftLowerFrame", "_shaftSupport", "_shaftFrame" }),
                ("Timberborn.ModularShafts", "Timberborn.ModularShafts.ShaftModelFactory", new[] { "Load", "_modularShaftPartsSpec", "_shaftFrameFactory", "_templateService", "_optimizedPrefabInstantiator" }),
                ("Timberborn.ModularShafts", "Timberborn.ModularShafts.ModularShaftModelService", new[] { "Load", "_shaftModelFactory", "_rootObjectProvider" }),
                ("Timberborn.ModularShafts", "Timberborn.ModularShafts.ModularShaftModelUpdater", new[] { "Awake", "_modularShaftModelService" }),
                ("Timberborn.PathSystem", "Timberborn.PathSystem.DynamicPathModel", new[] { "Awake", "_dynamicPathModelSpec" }),
                ("Timberborn.PathSystem", "Timberborn.PathSystem.DrivewayModelInstantiator", new[] { "InstantiateModel" }),
                ("Timberborn.DecalSystem", "Timberborn.DecalSystem.DecalService", new[] { "Load", "_decalCategories", "_specService", "LoadCustomDecals" }),
                ("Timberborn.DecalSystem", "Timberborn.DecalSystem.DecalSupplier", new[] { "InitializeEntity", "ActiveDecal", "_decalService", "Category" }),
                ("Timberborn.DecalSystemUI", "Timberborn.DecalSystemUI.DecalButtonContainer", new[] { "Show", "RemoveButtons", "_decalService", "_decalButtonFactory", "_decalButtons", "_root" }),
                ("Timberborn.Stockpiles", "Timberborn.Stockpiles.StockpileInventoryInitializer", new[] { "Initialize" }),
                ("Timberborn.InventorySystem", "Timberborn.InventorySystem.InventoryInitializer", new[] { "GetGoods", "AddAllowedGoodType" }),
                ("Timberborn.Planting", "Timberborn.Planting.PlanterBuilding", new[] { "GetAllowedPlantables" }),
                ("Timberborn.Yielding", "Timberborn.Yielding.YieldRemovingBuilding", new[] { "IsAllowed" }),
                ("Timberborn.PlantingUI", "Timberborn.PlantingUI.PlantingToolButtonFactory", new[] { "GetPlanterBuildingName", "_templateService", "_loc" }),
                ("Timberborn.ToolButtonSystem", "Timberborn.ToolButtonSystem.ToolButtonService", new[] { "ToolButtons", "_toolGroupButtons" }),
                ("Timberborn.ToolButtonSystem", "Timberborn.ToolButtonSystem.ToolButton", new[] { "OnDevModeToggledEvent", "ToolEnabled", "Tool" }),
                ("Timberborn.ToolButtonSystem", "Timberborn.ToolButtonSystem.ToolGroupButton", new[] { "OnDevModeToggledEvent", "IsVisible", "IsActive" }),
                ("Timberborn.WellbeingUI", "Timberborn.WellbeingUI.PopulationWellbeingBox", new[] { "GetPanel", "_counters" }),
                ("Timberborn.WellbeingUI", "Timberborn.WellbeingUI.PopulationWellbeingCounter", new[] { "_root", "NeedId" }),
                ("Timberborn.WellbeingUI", "Timberborn.WellbeingUI.BasicStatisticsPanelFactory", new[] { "Create" }),
                ("Timberborn.DistributionSystemBatchControl", "Timberborn.DistributionSystemBatchControl.DistributionSettingGroupFactory", new[] { "CreateItems", "_goodDistributionSettingItemFactory" }),
                ("Timberborn.DistributionSystemUI", "Timberborn.DistributionSystemUI.ImportGoodIconFactory", new[] { "CreateImportGoodIcon" }),
                ("Timberborn.DistributionSystemUI", "Timberborn.DistributionSystemUI.DistrictCrossingFragment", new[] { "ShowFragment", "_importGoodIcons" }),
                ("Timberborn.DistributionSystemUI", "Timberborn.DistributionSystemUI.ImportGoodIcon", new[] { "_goodId" }),
                ("Timberborn.GoodStatisticsBatchControl", "Timberborn.GoodStatisticsBatchControl.GoodStatisticsGroupFactory", new[] { "CreateItems", "_goodService", "_goodStatisticsBatchControlItemFactory" }),
                ("Timberborn.StockpilesUI", "Timberborn.StockpilesUI.GoodStockpilesTooltipFactory", new[] { "AddIcons", "_templates", "IconClass" }),
                ("Timberborn.AutomationBuildingsUI", "Timberborn.AutomationBuildingsUI.ResourceCounterGoodsDropdownProvider", new[] { "InitializeEntity", "Items" }),
                ("Timberborn.GameOverUI", "Timberborn.GameOverUI.GameOverBox", new[] { "OnGameOverEvent", "_root" }),
                ("Timberborn.GameWonderCompletion", "Timberborn.GameWonderCompletion.GameWonderCompletionService", new[] { "CompleteWonder", "IsWonderCompletedWithAnyFaction",
                    "_mapNameService", "_wonderCompletionService", "WasCompletedFirstTimeForFaction", "WasCompletedFirstTimeForMap" }),
                ("Timberborn.GameWonderCompletionUI", "Timberborn.GameWonderCompletionUI.WonderCompletionPanel", new[] { "ShowMainSection", "ShowMapPanel", "_root",
                    "_mapMasteryFactionIcon", "_mapMasteryLabel", "_loc", "WonderCompletedLocKey" }),
                ("Timberborn.GameSound", "Timberborn.GameSound.GameUISoundController", new[] { "PlayWonderLaunchSound", "PlaySound2D" }),
                ("Timberborn.TutorialSystem", "Timberborn.TutorialSystem.TutorialService", new[] { "GetConfigurations" }),
                ("Timberborn.TutorialSystem", "Timberborn.TutorialSystem.TutorialTriggers", new[] { "Load", "_canTrigger" }),
                ("Timberborn.GameStartup", "Timberborn.GameStartup.StartingBuildingSpawner", new[] { "StartingBuildingTemplateSpec", "Place" }),
                ("Timberborn.GameSceneLoading", "Timberborn.GameSceneLoading.GameSceneLoader", new[] { "StartNewGame" }),
                ("Timberborn.SelectionSystem", "Timberborn.SelectionSystem.SelectableObjectSelectedEvent", new[] { "SelectableObject" }),
            };
            var missing = new List<string>();
            foreach (var (assembly, typeName, names) in members)
            {
                Type? type = Assembly.Load(assembly).GetType(typeName);
                if (type == null)
                {
                    missing.Add(typeName);
                    continue;
                }
                foreach (string name in names)
                    if (type.GetMember(name, all).Length == 0) missing.Add(typeName.Split('.').Last() + "." + name);
            }
            if (missing.Count > 0) throw new Exception("gone: " + string.Join(", ", missing));
            if (Assembly.Load("Timberborn.WorldPersistence").GetType("Timberborn.WorldPersistence.EntityLoader", true)!
                .GetConstructor(new[] { Assembly.Load("Timberborn.WorldSerialization").GetType("Timberborn.WorldSerialization.SerializedEntity", true)! }) == null)
                throw new Exception("EntityLoader no longer reads a SerializedEntity (a loaded beaver's faction before Awake)");
        });

        test("Factions: its Harmony patches still find their game methods", () =>
        {
            int checkedPatches = 0;
            foreach (Type patcher in mod.GetTypes().Where(t => t.Namespace == "BeaverBuddies.Factions"))
            {
                var classPatch = patcher.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch");
                if (classPatch == null || classPatch.ConstructorArguments.Count == 0) continue;
                if (!(classPatch.ConstructorArguments[0].Value is Type type)) continue;
                var names = new List<string>();
                if (classPatch.ConstructorArguments.Count > 1 && classPatch.ConstructorArguments[1].Value is string name) names.Add(name);
                foreach (MethodInfo method in patcher.GetMethods(all))
                    foreach (var attribute in method.GetCustomAttributesData().Where(a => a.AttributeType.Name == "HarmonyPatch"))
                        if (attribute.ConstructorArguments.Count > 0 && attribute.ConstructorArguments[0].Value is string methodName) names.Add(methodName);
                foreach (string target in names)
                {
                    checkedPatches++;
                    if (type.GetMember(target, all).Length == 0) throw new Exception($"{patcher.Name}: {type.Name}.{target} is gone");
                }
            }
            if (checkedPatches < 30) throw new Exception("checked only " + checkedPatches + " patches");
        });

        test("Factions: the waiting room's faction switcher uses only classes of the style sheets the main menu loads", () =>
        {
            string uiPath = Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip"));
            using ZipArchive ui = ZipFile.OpenRead(uiPath);
            string Read(string entry)
            {
                using var reader = new StreamReader((ui.GetEntry(entry) ?? throw new Exception("UI.zip has no " + entry)).Open());
                return reader.ReadToEnd();
            }
            var defined = new HashSet<string>();
            foreach (Match sheet in Regex.Matches(Read("Views/MainMenu/TitleScreen.uxml"), "Style src=\"/Assets/Resources/UI/(Views/[^\"]+\\.uss)\""))
                foreach (Match m in Regex.Matches(Read(sheet.Groups[1].Value), "\\.([A-Za-z_][A-Za-z0-9_-]*)")) defined.Add(m.Groups[1].Value);
            var used = (string[])mod.GetType("BeaverBuddies.Lobby.LobbyFactionPicker", true)!.GetField("ClassesUsed", all)!.GetValue(null)!;
            var missing = used.Where(c => !defined.Contains(c)).ToList();
            if (missing.Count > 0) throw new Exception("not in the main menu's style sheets: " + string.Join(", ", missing));
            // The faction page is still built from these (so the switcher still looks like it).
            string item = Read("Views/MainMenu/NewGameFactionItem.uxml");
            foreach (string cls in new[] { "faction-item-selected__name", "arrow--left", "arrow--right", "faction-item__logo-background" })
                if (!item.Contains(cls)) throw new Exception("the faction page no longer uses " + cls);
        });

        test("Factions: a founding's faction and the host's factions survive the trip through the event JSON", () =>
        {
            Type replayEvent = mod.GetTypes().First(t => t.Name == "ReplayEvent" && t.IsAbstract);
            var json = mod.GetType("BeaverBuddies.IO.JsonSettings", true)!;
            MethodInfo serialize = json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent);
            MethodInfo deserialize = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent);
            object RoundTrip(object e) => deserialize.Invoke(null, new object[] { serialize.Invoke(null, new[] { e })! })!;
            var foundType = mod.GetType("BeaverBuddies.Colonies.FoundColonyEvent", true)!;
            object found = Activator.CreateInstance(foundType, true)!;
            foundType.GetField("faction")!.SetValue(found, "IronTeeth");
            if ((string?)foundType.GetField("faction")!.GetValue(RoundTrip(found)) != "IronTeeth") throw new Exception("the founding's faction was lost");
            var initType = mod.GetType("BeaverBuddies.Events.InitializeClientEvent", true)!;
            object init = Activator.CreateInstance(initType, true)!;
            initType.GetField("hostFactions")!.SetValue(init, new List<string> { "Folktails" });
            var factions = (List<string>?)initType.GetField("hostFactions")!.GetValue(RoundTrip(init));
            if (factions == null || !factions.SequenceEqual(new[] { "Folktails" })) throw new Exception("the host's factions were lost");
            var switchType = mod.GetType("BeaverBuddies.Factions.ColonyFactionSwitchEvent", true)!;
            object switching = Activator.CreateInstance(switchType, true)!;
            switchType.GetField("faction")!.SetValue(switching, "IronTeeth");
            object back = RoundTrip(switching);
            if (back.GetType() != switchType || (string?)switchType.GetField("faction")!.GetValue(back) != "IronTeeth") throw new Exception("the switch changed on the way");
        });

        zip?.Dispose();
    }
}
