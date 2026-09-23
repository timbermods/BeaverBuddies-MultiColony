#nullable enable
using System.Collections;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: reviewer B: Folktails and Iron Teeth together in the late game, and both Wonders.
// The game's own data is read from its Blueprints.zip, beside the Managed folder the game's assemblies were loaded from.
internal static class RcFactionRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static readonly string[] Factions = { "Folktails", "IronTeeth" };

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");

        // ---- the game's data ----
        string managed = Path.GetDirectoryName(Assembly.Load("Timberborn.Common").Location)!;
        string blueprintsPath = Path.GetFullPath(Path.Combine(managed, "..", "StreamingAssets", "Modding", "Blueprints.zip"));
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
        List<string> Strings(JsonNode? array) => array?.AsArray().Select(n => n!.GetValue<string>()).ToList() ?? new List<string>();
        JsonNode FactionSpec(string id) => Need($"Factions/Faction.{id}.blueprint.json")["FactionSpec"]!;
        List<string> TemplatePaths(string faction)
        {
            var ids = Strings(FactionSpec(faction)["TemplateCollectionIds"]);
            var paths = new List<string>();
            foreach (string file in Entries("TemplateCollections/"))
            {
                JsonNode spec = Need(file)["TemplateCollectionSpec"]!;
                if (ids.Contains(spec["CollectionId"]!.GetValue<string>())) paths.AddRange(Strings(spec["Blueprints"]));
            }
            return paths.Distinct().ToList();
        }
        Dictionary<string, IEnumerable<string>> ItemsByCollection(string folder, string spec, string list) =>
            Entries(folder).Select(f => Need(f)[spec]!).GroupBy(s => s["CollectionId"]!.GetValue<string>())
                .ToDictionary(g => g.Key, g => (IEnumerable<string>)g.SelectMany(s => Strings(s[list])).ToList());
        // The mod's own FactionSets over the game's collections, as FactionCatalog builds them.
        Type setsType = mod.GetType("BeaverBuddies.Factions.FactionSets", true)!;
        object Sets(string idsKey, string folder, string spec, string list) => setsType.GetMethod("FromCollections")!.Invoke(null, new object[]
        {
            Factions, new[] { "Common" },
            (IReadOnlyDictionary<string, IEnumerable<string>>)Factions.ToDictionary(f => f, f => (IEnumerable<string>)Strings(FactionSpec(f)[idsKey])),
            (IReadOnlyDictionary<string, IEnumerable<string>>)ItemsByCollection(folder, spec, list),
        })!;
        bool Has(object sets, string faction, string item) => (bool)setsType.GetMethod("Has")!.Invoke(sets, new object[] { faction, item })!;
        JsonNode Character(string path) => Need(path)["TimbermeshAnimatorControllerSpec"]!;
        HashSet<string> Flags(string path) => Strings(Character(path)["BoolParameters"]).Concat(Strings(Character(path)["FloatParameters"])).ToHashSet();
        const string BeaverAdult = "Characters/Beaver/BeaverAdult.blueprint.json";
        const string BotFolktails = "Characters/Bot/Bot.Folktails.blueprint.json";
        const string BotIronTeeth = "Characters/Bot/Bot.IronTeeth.blueprint.json";

        PropertyInfo isOn = mod.GetType("BeaverBuddies.Factions.MixedFactions", true)!.GetProperty("IsOn")!;
        void Mixed(bool on) => isOn.GetSetMethod(true)!.Invoke(null, new object[] { on });

        // B1. The Earth Repopulator makes each of its workers a pilot; the game sets their "Piloting" animation without
        // asking whether the animator has it, and a Folktails bot's has not. In a mixed game a Folktails bot can work there
        // (a Folktails colony handed to an Iron Teeth one, whose player builds the Wonder and sets it to bots), and the
        // activation, a replayed action, threw on every computer and stopped the session.
        test("B1: a Folktails bot that pilots the Earth Repopulator's plane no longer throws inside the replayed activation", () =>
        {
            // The premise, from the game's data: bots may work at the Wonder; only the Folktails bot lacks the flag.
            JsonNode workplace = Need("Buildings/Monuments/EarthRepopulator/EarthRepopulator.IronTeeth.blueprint.json")["WorkplaceSpec"]!;
            if (workplace["DisallowOtherWorkerTypes"]!.GetValue<bool>()
                || !workplace["WorkerTypeUnlockCosts"]!.AsArray().Any(c => c!["WorkerType"]!.GetValue<string>() == "Bot"))
                throw new Exception("bots can no longer work at the Earth Repopulator: the premise moved");
            if (Flags(BotFolktails).Contains("Piloting") || !Flags(BotIronTeeth).Contains("Piloting") || !Flags(BeaverAdult).Contains("Piloting"))
                throw new Exception("which characters can play \"Piloting\" changed: the premise moved");
            // The premise, from the game's code: PrepareForFlying sets the flag, and SetBool throws on a flag the animator lacks.
            Type pilot = Game("Timberborn.WonderPlanes", "Timberborn.WonderPlanes.Pilot");
            Type characterAnimator = Game("Timberborn.CharacterModelSystem", "Timberborn.CharacterModelSystem.CharacterAnimator");
            MethodInfo prepare = Only(pilot, "PrepareForFlying");
            MethodInfo setBool = characterAnimator.GetMethod("SetBool", new[] { typeof(string), typeof(bool) })!;
            if (!IlScan.Instructions(prepare).Any(i => i.Calls && Equals(i.Member, setBool)))
                throw new Exception("Pilot.PrepareForFlying no longer sets an animation flag");
            object folktailsBot = Animator(characterAnimator, Strings(Character(BotFolktails)["BoolParameters"]), Strings(Character(BotFolktails)["FloatParameters"]));
            try
            {
                setBool.Invoke(folktailsBot, new object[] { "Piloting", true });
                throw new Exception("the game's SetBool accepted a flag the animator lacks: the premise moved");
            }
            catch (TargetInvocationException e) when (e.InnerException is KeyNotFoundException) { }
            // The fix: the mod's transpiler sends that one call through its guard, and nothing else changes.
            Type patcher = mod.GetType("BeaverBuddies.Factions.FactionPilotAnimationPatcher")
                ?? throw new Exception("no patch of Pilot.PrepareForFlying: a Folktails bot pilot still throws");
            Type codeType = Assembly.Load("0Harmony").GetType("HarmonyLib.CodeInstruction", true)!;
            var scratch = new DynamicMethod("Decode", typeof(void), Type.EmptyTypes, typeof(RcFactionRuntimeChecks).Module, true);
            object before = TimingChecks.ReadInstructions(prepare, scratch.GetILGenerator(), codeType);
            var beforeOperands = Operands(before).ToList();
            object after = patcher.GetMethod("Transpiler", All)!.Invoke(null, new[] { before })!;
            var afterOperands = Operands(after).ToList();
            Type guard = mod.GetType("BeaverBuddies.Factions.FactionAnimation", true)!;
            MethodInfo guarded = guard.GetMethod("SetBoolIfItHas")!;
            if (afterOperands.Any(o => Equals(o, setBool)) || afterOperands.Count(o => Equals(o, guarded)) != 1)
                throw new Exception("the transpiled PrepareForFlying does not set its flag through FactionAnimation.SetBoolIfItHas");
            if (afterOperands.Count != beforeOperands.Count || Enumerable.Range(0, afterOperands.Count)
                    .Any(k => !Equals(afterOperands[k], beforeOperands[k]) && !(Equals(beforeOperands[k], setBool) && Equals(afterOperands[k], guarded))))
                throw new Exception("the transpiler changed more of PrepareForFlying than its one SetBool call");
            var logger = mod.GetType("BeaverBuddies.Plugin", true)!.GetField("logger", All)!;
            object? previousLogger = logger.GetValue(null);
            logger.SetValue(null, DispatchProxy.Create(mod.GetType("BeaverBuddies.Util.Logging.ILogger", true)!, typeof(QuietLoggerProxy)));
            Mixed(true);
            try
            {
                // A Folktails bot: the flag is left out, and nothing throws.
                guarded.Invoke(null, new[] { folktailsBot, "Piloting", (object)true });
                // An Iron Teeth bot: the flag is set, as the game sets it (the pose that follows needs Unity, not run here).
                object ironTeethBot = Animator(characterAnimator, Strings(Character(BotIronTeeth)["BoolParameters"]), Strings(Character(BotIronTeeth)["FloatParameters"]));
                try { guarded.Invoke(null, new[] { ironTeethBot, "Piloting", (object)true }); }
                catch (TargetInvocationException) { /* UpdateState, after the flag is stored, reaches Unity */ }
                if (!Flag(ironTeethBot, "Piloting")) throw new Exception("an Iron Teeth bot pilot's flag was not set");
            }
            finally
            {
                Mixed(false);
                logger.SetValue(null, previousLogger);
            }
        });

        // H1. The catalog of what each faction has is read from the game's collections; if that ever fails (a faction mod's
        // data), every answer falls back to the game's own instead of a NullReferenceException inside a tick, a replay or a
        // load (a character's needs, a stockpile's goods, a yield, a building's placement).
        test("B2 (H1): a faction catalog that could not be read answers as the game would, and never throws", () =>
        {
            Type catalogType = mod.GetType("BeaverBuddies.Factions.FactionCatalog", true)!;
            Type needService = Game("Timberborn.GameFactionSystem", "Timberborn.GameFactionSystem.FactionNeedService");
            Type needProviders = Game("Timberborn.NeedCollectionSystem", "Timberborn.NeedCollectionSystem.INeedCollectionIdsProvider");
            object needs = Activator.CreateInstance(needService, null, null, Array.CreateInstance(needProviders, 0))!;
            Type needSpec = Game("Timberborn.NeedSpecs", "Timberborn.NeedSpecs.NeedSpec");
            var list = (IList)needService.GetField("_needs", All)!.GetValue(needs)!;
            foreach (var (id, type) in new[] { ("Biofuel", "Bot"), ("Energy", "Bot"), ("Hunger", "Beaver") })
            {
                object spec = RuntimeHelpers.GetUninitializedObject(needSpec);
                needSpec.GetProperty("Id")!.SetValue(spec, id);
                needSpec.GetProperty("CharacterType")!.SetValue(spec, type);
                list.Add(spec);
            }
            // A spec service that throws, as a broken collection would; it counts how often the catalog tries.
            Type specServiceType = Game("Timberborn.BlueprintSystem", "Timberborn.BlueprintSystem.ISpecService");
            object specService = typeof(DispatchProxy).GetMethod("Create", 2, Type.EmptyTypes)!
                .MakeGenericMethod(specServiceType, typeof(ThrowingSpecServiceProxy)).Invoke(null, null)!;
            object catalog = catalogType.GetConstructors().Single().Invoke(new object?[] { specService, null, needs, null });
            var logger = mod.GetType("BeaverBuddies.Plugin", true)!.GetField("logger", All)!;
            object? previousLogger = logger.GetValue(null);
            logger.SetValue(null, DispatchProxy.Create(mod.GetType("BeaverBuddies.Util.Logging.ILogger", true)!, typeof(QuietLoggerProxy)));
            Mixed(true);
            try
            {
                object? Call(string name, params object?[] args)
                {
                    try { return catalogType.GetMethod(name)!.Invoke(catalog, args); }
                    catch (TargetInvocationException e) { throw new Exception($"FactionCatalog.{name} threw {e.InnerException?.GetType().Name} on a catalog it could not read"); }
                }
                if (Call("FactionOfTemplate", "Smelter.IronTeeth") != null) throw new Exception("an unread catalog named a template's faction");
                if (!(bool)Call("HasGood", "Folktails", "Corn")!) throw new Exception("an unread catalog refused a good (the game allows every loaded good)");
                if (!(bool)Call("HasNeed", "Folktails", "Energy")!) throw new Exception("an unread catalog refused a need");
                if (Call("GoodsOf", "Folktails") != null) throw new Exception("an unread catalog gave a goods filter");
                if (catalogType.GetMethod("IsCommonGood") is MethodInfo common && (bool)common.Invoke(catalog, new object[] { "Log" })!)
                    throw new Exception("an unread catalog called a good common");
                var bot = ((IEnumerable)Call("NeedsFor", "Folktails", true)!).Cast<object>().Select(s => (string)needSpec.GetProperty("Id")!.GetValue(s)!).ToList();
                if (!bot.SequenceEqual(new[] { "Biofuel", "Energy" })) throw new Exception("an unread catalog's bot needs are " + string.Join(", ", bot) + ", not the game's");
                // Once the game has loaded, a catalog that still can't be read stops trying: each call the game makes
                // (every character, yield and building) would otherwise build, throw and catch again.
                var proxy = (ThrowingSpecServiceProxy)specService;
                if (proxy.Calls == 0) throw new Exception("the test's catalog never tried to read the specs");
                (catalogType.GetMethod("PostLoad") ?? throw new Exception("the catalog does not give up after the load")).Invoke(catalog, null);
                int tries = proxy.Calls;
                for (int i = 0; i < 100; i++) Call("FactionOfTemplate", "Smelter.IronTeeth");
                if (proxy.Calls != tries) throw new Exception($"after the load, 100 calls tried to read the catalog {proxy.Calls - tries} more times");
            }
            finally
            {
                Mixed(false);
                logger.SetValue(null, previousLogger);
            }
        });

        // S1/S3 (performance, mixed only). The yield filter runs for every yielder a gatherer's, lumberjack's or scavenger's
        // range takes in, and for each one planted there; most yields are common goods (logs, berries, scrap metal).
        test("B3 (S1): the yield filter looks up the building's faction only for goods not every faction has, with the same answers", () =>
        {
            Type patcher = mod.GetType("BeaverBuddies.Factions.FactionYieldRemoverPatcher", true)!;
            var code = IlScan.Instructions(patcher.GetMethod("Postfix", All)!);
            int commonCall = code.FindIndex(i => i.Calls && i.Member?.Name == "IsCommonGood");
            int faction = code.FindIndex(i => i.Calls && i.Member?.Name == "SimFactionOf");
            if (commonCall < 0 || faction < 0 || commonCall > faction) throw new Exception("the yield filter works out the building's faction before asking whether the good is common");
            // Every yield in the game: a common good is every faction's (so skipping the building's faction changes nothing).
            object goods = Sets("GoodCollectionIds", "GoodCollections/", "GoodCollectionSpec", "Goods");
            MethodInfo inCommon = setsType.GetMethod("InCommon")!;
            var yields = new List<(string Template, string Spec, string Good)>();
            foreach (string file in Entries("NaturalResources/").Concat(Entries("MapEditor/")))
            {
                JsonNode? node = Blueprint(file);
                if (node == null) continue;
                foreach (var spec in node.AsObject())
                    if (spec.Value is JsonObject o && o["Yielder"]?["Yield"]?["Id"] is JsonNode id) yields.Add((file, spec.Key, id.GetValue<string>()));
            }
            if (yields.Count < 20) throw new Exception($"found only {yields.Count} yields; not looking in the right place");
            foreach (var (template, spec, good) in yields)
            {
                bool common = (bool)inCommon.Invoke(goods, new object[] { good })!;
                if (common && Factions.Any(f => !Has(goods, f, good))) throw new Exception($"{good} is common yet a faction lacks it");
                // A lumberjack's trees and a scavenger's ruins, the most numerous yielders, all skip the lookup.
                if (!common && (spec == "RuinSpec" || (spec == "CuttableSpec" && template.StartsWith("NaturalResources/Trees/"))))
                    throw new Exception($"{template}'s {spec} yields {good}, not a common good: the shortcut no longer covers it");
            }
        });

        test("B3 (S3): MixedFactions.Spec, asked for every beaver made and path painted, makes no closure and no enumerator", () =>
        {
            Type mixed = mod.GetType("BeaverBuddies.Factions.MixedFactions", true)!;
            foreach (MethodInfo method in mixed.GetMethods(All).Where(m => m.Name is "Spec" or "Find" or "IsKnown"))
            {
                foreach (var i in IlScan.Instructions(method))
                {
                    if (i.Op == OpCodes.Newobj) throw new Exception($"MixedFactions.{method.Name} allocates a {i.Member?.DeclaringType?.Name}");
                    if (i.Calls && i.Member?.DeclaringType?.FullName == "System.Linq.Enumerable") throw new Exception($"MixedFactions.{method.Name} uses LINQ ({i.Member.Name})");
                }
            }
        });

        // F8. A mixed game paints every path in its colony's faction's materials as it loads and when its colony changes.
        test("B4 (F8): a path is painted from its model's own pieces, and not again in the base faction the game has just used", () =>
        {
            // The premise: the game's Awake paints each piece it finds by name and keeps it in the model's two tables.
            Type model = Game("Timberborn.PathSystem", "Timberborn.PathSystem.DynamicPathModel");
            var add = IlScan.Instructions(Only(model, "AddModel"));
            if (!add.Any(i => i.Member?.Name == "_groundModels") || !add.Any(i => i.Member?.Name == "_roofModels")
                || add.Count(i => i.Calls && i.Member?.Name == "AddVariants") != 2 || !add.Any(i => i.Calls && i.Member?.Name == "get_Current"))
                throw new Exception("DynamicPathModel.AddModel no longer keeps the base faction's painted pieces in its two tables");
            var variant = IlScan.Instructions(Only(model, "GetModelVariant"));
            if (!variant.Any(i => i.Calls && i.Member?.Name == "set_sharedMaterial")) throw new Exception("GetModelVariant no longer paints the piece");
            // The mod: no search by name and no names built, per path.
            Type models = mod.GetType("BeaverBuddies.Factions.FactionModels", true)!;
            foreach (MethodInfo method in models.GetMethods(All).Where(m => m.Name is "PaintPath" or "Paint"))
            {
                var code = IlScan.Instructions(method);
                if (code.Any(i => i.Calls && i.Member?.Name is "FindChild" or "FindChildTransform")) throw new Exception($"FactionModels.{method.Name} searches the model by name");
                // (PaintPath's only string is its warning, made when a paint fails.)
                if (method.Name == "Paint" && code.Any(i => i.Calls && i.Member?.DeclaringType == typeof(string) && i.Member.Name == "Concat"))
                    throw new Exception($"FactionModels.{method.Name} builds names");
            }
            var paint = IlScan.Instructions(models.GetMethods(All).Single(m => m.Name == "PaintPath"));
            if (!paint.Any(i => i.Member?.Name == "_groundModels") || !paint.Any(i => i.Member?.Name == "_roofModels"))
                throw new Exception("PaintPath does not take the pieces from the model's tables");
            var awake = IlScan.Instructions(mod.GetType("BeaverBuddies.Factions.FactionPathModelPatcher", true)!.GetMethod("Postfix", All)!);
            int baseRead = awake.FindIndex(i => i.Calls && i.Member?.Name == "get_BaseFaction");
            int painting = awake.FindIndex(i => i.Calls && i.Member?.Name == "PaintPath");
            if (baseRead < 0 || painting < 0 || baseRead > painting) throw new Exception("a path is painted again in the base faction's materials as it wakes");
        });

        // F1, found sound (a guard against a game update): what the review's content sweep found, kept as a check.
        test("F1: every building of each faction is its own, and is built, run and supplied with its own faction's goods and needs", () =>
        {
            object goods = Sets("GoodCollectionIds", "GoodCollections/", "GoodCollectionSpec", "Goods");
            object needs = Sets("NeedCollectionIds", "NeedCollections/", "NeedCollectionSpec", "Needs");
            var recipes = Entries("Recipes/").Select(f => Need(f)["RecipeSpec"]!).ToDictionary(r => r["Id"]!.GetValue<string>());
            var problems = new List<string>();
            int buildings = 0;
            var shared = TemplatePaths("Folktails").Intersect(TemplatePaths("IronTeeth")).ToHashSet();
            foreach (string faction in Factions)
            {
                foreach (string path in TemplatePaths(faction).Where(p => p.StartsWith("Buildings/") && !shared.Contains(p)))
                {
                    JsonNode? node = Blueprint(path);
                    string? name = node?["TemplateSpec"]?["TemplateName"]?.GetValue<string>();
                    // The plane and the shaft parts are no buildings.
                    if (node == null || name == null || node["BuildingSpec"] == null) continue;
                    buildings++;
                    if (!name.EndsWith("." + faction)) problems.Add($"{name} is listed by {faction} alone");
                    void Good(string what, string? good) { if (good != null && !Has(goods, faction, good)) problems.Add($"{name}: {what} {good} is not {faction}'s"); }
                    void NeedOf(string what, string? need) { if (need != null && !Has(needs, faction, need)) problems.Add($"{name}: {what} {need} is not {faction}'s"); }
                    foreach (JsonNode? cost in node["BuildingSpec"]?["BuildingCost"]?.AsArray() ?? new JsonArray()) Good("costs", cost?["Id"]?.GetValue<string>());
                    foreach (string id in Strings(node["ManufactorySpec"]?["ProductionRecipeIds"]))
                    {
                        if (!recipes.TryGetValue(id, out JsonNode? recipe)) { problems.Add($"{name}: no recipe {id}"); continue; }
                        foreach (JsonNode? i in recipe["Ingredients"]?.AsArray() ?? new JsonArray()) Good($"recipe {id} takes", i?["Id"]?.GetValue<string>());
                        foreach (JsonNode? p in recipe["Products"]?.AsArray() ?? new JsonArray()) Good($"recipe {id} makes", p?["Id"]?.GetValue<string>());
                        string? fuel = recipe["Fuel"]?.GetValue<string>();
                        Good($"recipe {id} burns", string.IsNullOrEmpty(fuel) ? null : fuel);
                    }
                    foreach (JsonNode? g in node["WonderInventorySpec"]?["RequiredGoods"]?.AsArray() ?? new JsonArray()) Good("needs", g?["Id"]?.GetValue<string>());
                    foreach (string spec in new[] { "AttractionSpec", "ContinuousEffectBuildingSpec", "WonderEffectControllerSpec", "AreaNeedApplierSpec" })
                        foreach (JsonNode? e in node[spec]?["Effects"]?.AsArray() ?? new JsonArray()) NeedOf("serves", e?["NeedId"]?.GetValue<string>());
                }
            }
            if (buildings < 250) throw new Exception($"read only {buildings} buildings; not looking in the right place");
            if (problems.Count > 0) throw new Exception(string.Join("; ", problems.Take(12)));
        });

        test("F1/F3: an animation a building's slots ask of a bot, the bot has, unless its faction lacks every need the building serves", () =>
        {
            object needs = Sets("NeedCollectionIds", "NeedCollections/", "NeedCollectionSpec", "Needs");
            var botNeeds = Entries("Needs/").Select(f => Need(f)["NeedSpec"]!).Where(n => n["CharacterType"]!.GetValue<string>() == "Bot")
                .Select(n => n["Id"]!.GetValue<string>()).ToHashSet();
            var bots = new[] { ("Folktails", BotFolktails), ("IronTeeth", BotIronTeeth) };
            var problems = new List<string>();
            foreach (string file in Entries("Buildings/"))
            {
                JsonNode? node = Blueprint(file);
                string? name = node?["TemplateSpec"]?["TemplateName"]?.GetValue<string>();
                if (node == null || name == null) continue;
                var animations = (node["TransformSlotInitializerSpec"]?["Slots"]?.AsArray() ?? new JsonArray())
                    .Select(s => s?["Animation"]?.GetValue<string>()).Where(a => !string.IsNullOrEmpty(a)).Select(a => a!).ToList();
                JsonNode? workplace = node["WorkplaceSpec"];
                bool botsWork = workplace != null && (workplace["DefaultWorkerType"]?.GetValue<string>() == "Bot" || !(workplace["DisallowOtherWorkerTypes"]?.GetValue<bool>() ?? false));
                var served = (node["AttractionSpec"]?["Effects"]?.AsArray() ?? new JsonArray()).Select(e => e?["NeedId"]?.GetValue<string>()).Where(n => n != null).ToList();
                foreach (var (faction, path) in bots)
                {
                    var missing = animations.Where(a => !Flags(path).Contains(a)).ToList();
                    if (missing.Count == 0) continue;
                    if (botsWork) problems.Add($"{faction} bots may work at {name}, whose slots play {string.Join(", ", missing)} they lack");
                    else if (served.Any(n => botNeeds.Contains(n!) && Has(needs, faction, n!))) problems.Add($"{faction} bots visit {name} (it serves their needs) and lack {string.Join(", ", missing)}");
                }
            }
            if (problems.Count > 0) throw new Exception(string.Join("; ", problems));
        });

        test("F3: each faction's bots get exactly its own bot needs, the critical one and both boosts", () =>
        {
            object needs = Sets("NeedCollectionIds", "NeedCollections/", "NeedCollectionSpec", "Needs");
            var botNeeds = Entries("Needs/").Select(f => Need(f)["NeedSpec"]!).Where(n => n["CharacterType"]!.GetValue<string>() == "Bot")
                .Select(n => n["Id"]!.GetValue<string>()).ToList();
            var expected = new Dictionary<string, string[]> { ["Folktails"] = new[] { "Biofuel", "Catalyst", "PunchCard" }, ["IronTeeth"] = new[] { "ControlTower", "Energy", "Grease" } };
            foreach (string faction in Factions)
            {
                var own = botNeeds.Where(n => Has(needs, faction, n)).OrderBy(n => n).ToList();
                if (!own.SequenceEqual(expected[faction])) throw new Exception($"{faction} bots would need {string.Join(", ", own)}");
            }
        });

        // V1/V2, found sound (guards against a game update): the facts the review's Wonder findings rest on.
        test("V1: each Wonder's effect is its own faction's need, and a character without the need is skipped", () =>
        {
            object needs = Sets("NeedCollectionIds", "NeedCollections/", "NeedCollectionSpec", "Needs");
            foreach (var (path, faction, other) in new[] {
                ("Buildings/Monuments/EarthRecultivator/EarthRecultivator.Folktails.blueprint.json", "Folktails", "IronTeeth"),
                ("Buildings/Monuments/EarthRepopulator/EarthRepopulator.IronTeeth.blueprint.json", "IronTeeth", "Folktails") })
            {
                var effects = Need(path)["WonderEffectControllerSpec"]!["Effects"]!.AsArray().Select(e => e!["NeedId"]!.GetValue<string>()).ToList();
                if (effects.Count == 0 || effects.Any(n => !Has(needs, faction, n) || Has(needs, other, n)))
                    throw new Exception($"{path}: its effect ({string.Join(", ", effects)}) is no longer {faction}'s alone");
            }
            Type needManager = Game("Timberborn.NeedSystem", "Timberborn.NeedSystem.NeedManager");
            MethodInfo apply = needManager.GetMethods(All).Single(m => m.Name == "ApplyEffect" && m.GetParameters().Length == 1);
            if (!IlScan.Instructions(apply).Any(i => i.Calls && i.Member?.Name == "TryGetNeed"))
                throw new Exception("NeedManager.ApplyEffect no longer skips a need the character lacks");
        });

        test("V1: the single Wonder completion writes this player's profile and two display flags, nothing the simulation reads", () =>
        {
            Type prefix = mod.GetType("BeaverBuddies.Factions.FactionWonderCompletionPatcher", true)!;
            var allowedStores = new HashSet<string> { "set_WasCompletedFirstTimeForFaction", "set_WasCompletedFirstTimeForMap" };
            foreach (MethodInfo method in prefix.GetMethods(All).Where(m => !m.IsConstructor))
            {
                foreach (var i in IlScan.Instructions(method))
                {
                    if (i.Stores) throw new Exception($"{method.Name} stores {i.Member?.Name}");
                    if (!i.Calls || i.Member == null) continue;
                    string type = i.Member.DeclaringType?.FullName ?? "";
                    bool fine = allowedStores.Contains(i.Member.Name) || i.Member.Name.StartsWith("get_")
                        || type == "Timberborn.WonderCompletion.WonderCompletionService" || type.StartsWith("System.")
                        || type.StartsWith("BeaverBuddies.Factions.") || type == "Timberborn.GameWonderCompletion.GameWonderCompletionService" && i.Member.Name == "IsWonderCompletedWithAnyFaction";
                    if (!fine) throw new Exception($"{method.Name} calls {type}.{i.Member.Name}");
                }
            }
            Type service = Game("Timberborn.WonderCompletion", "Timberborn.WonderCompletion.WonderCompletionService");
            if (!IlScan.Instructions(Only(service, "CompleteWonder")).Any(i => i.Calls && i.Member?.Name == "SetString" && i.Member.DeclaringType?.Name == "IPlayerDataService"))
                throw new Exception("WonderCompletionService.CompleteWonder no longer writes only the player's data");
        });

        // V1 (display, every co-op game): the game plays the launch sound in WonderFragment.ActivateWonder, which co-op
        // records instead of running, so no player heard it.
        test("B5 (V1): the player who activates a Wonder hears its launch sound in co-op, after the activation, on their computer only", () =>
        {
            Type fragment = Game("Timberborn.WondersUI", "Timberborn.WondersUI.WonderFragment");
            var game = IlScan.Instructions(Only(fragment, "ActivateWonder"));
            if (!game.Any(i => i.Calls && i.Member?.Name == "PlayWonderLaunchSound")) throw new Exception("the game's ActivateWonder no longer plays the launch sound");
            Type ev = mod.GetTypes().Single(t => t.Name == "WonderActivatedEvent" && t.Namespace == "BeaverBuddies.Events");
            var replay = IlScan.Instructions(ev.GetMethod("Replay", All)!);
            int activate = replay.FindIndex(i => i.Calls && i.Member?.Name == "Activate");
            int local = replay.FindIndex(i => i.Calls && i.Member?.Name == "get_LocalPlayer");
            int sound = replay.FindIndex(i => i.Calls && i.Member?.Name == "PlayWonderLaunchSound");
            if (sound < 0) throw new Exception("the replayed activation plays no launch sound");
            if (activate < 0 || local < activate || sound < local) throw new Exception("the launch sound is not played after the activation, for the acting player only");
        });

        test("V2: a pilot is one of the Wonder's own workers, never a new character, and dies through DestroyCharacter", () =>
        {
            Type launcher = Game("Timberborn.WonderPlanes", "Timberborn.WonderPlanes.PlaneLauncher");
            var teleport = IlScan.Instructions(Only(launcher, "TeleportAndInitializePilots"));
            if (!teleport.Any(i => i.Calls && i.Member?.Name == "get_AssignedWorkers") || !teleport.Any(i => i.Calls && i.Member?.Name == "PrepareForFlying"))
                throw new Exception("the pilots are no longer the Wonder's assigned workers");
            if (!IlScan.Instructions(Only(launcher, "DestroyPilots")).Any(i => i.Calls && i.Member?.Name == "DestroyCharacter"))
                throw new Exception("pilots are no longer destroyed through Character.DestroyCharacter (killed first)");
            Assembly planes = Assembly.Load("Timberborn.WonderPlanes");
            foreach (Type type in planes.GetTypes())
                foreach (MethodInfo method in type.GetMethods(All).Where(m => m.GetMethodBody() != null))
                    foreach (var i in IlScan.Instructions(method))
                        if (i.Calls && (i.Member?.DeclaringType?.Name is "BeaverFactory" or "BotFactory"))
                            throw new Exception($"{type.Name}.{method.Name} makes a character: a creation site the faction code must know");
        });
    }

    // A character's animator as the game builds it in Awake (TimbermeshAnimatorController.InitializeAllParameters), from
    // its template's flags, without Unity: only what SetBool and HasParameter read.
    static object Animator(Type characterAnimator, List<string> bools, List<string> floats)
    {
        Type controllerType = Assembly.Load("Timberborn.TimbermeshAnimations").GetType("Timberborn.TimbermeshAnimations.TimbermeshAnimatorController", true)!;
        Type specType = Assembly.Load("Timberborn.TimbermeshAnimations").GetType("Timberborn.TimbermeshAnimations.TimbermeshAnimatorControllerSpec", true)!;
        object spec = RuntimeHelpers.GetUninitializedObject(specType);
        specType.GetProperty("BoolParameters")!.SetValue(spec, bools.ToImmutableArray());
        specType.GetProperty("FloatParameters")!.SetValue(spec, floats.ToImmutableArray());
        object controller = RuntimeHelpers.GetUninitializedObject(controllerType);
        controllerType.GetField("_spec", All)!.SetValue(controller, spec);
        controllerType.GetField("_boolValues", All)!.SetValue(controller, new Dictionary<string, bool>());
        controllerType.GetField("_floatValues", All)!.SetValue(controller, new Dictionary<string, float>());
        controllerType.GetMethod("InitializeAllParameters", All)!.Invoke(controller, null);
        object animator = RuntimeHelpers.GetUninitializedObject(characterAnimator);
        characterAnimator.GetField("_animatorController", All)!.SetValue(animator, controller);
        return animator;
    }

    static bool Flag(object characterAnimator, string name)
    {
        object controller = characterAnimator.GetType().GetField("_animatorController", All)!.GetValue(characterAnimator)!;
        var values = (Dictionary<string, bool>)controller.GetType().GetField("_boolValues", All)!.GetValue(controller)!;
        return values[name];
    }

    static IEnumerable<object?> Operands(object code)
    {
        foreach (object instruction in (IEnumerable)code) yield return instruction.GetType().GetField("operand")!.GetValue(instruction);
    }
}

/// <summary>A game spec service whose every call throws (a collection the faction catalog can't read), counting the calls.</summary>
public class ThrowingSpecServiceProxy : DispatchProxy
{
    public int Calls;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls++;
        throw new InvalidOperationException("a broken collection (test)");
    }
}
