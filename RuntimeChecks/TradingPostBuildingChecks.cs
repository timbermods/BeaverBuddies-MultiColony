using System.Collections;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

// The Trading Post is a building the mod adds with blueprints (BeaverBuddies/Buildings, BeaverBuddies/TemplateCollections):
// the game's District Crossing, model and workings, under another name, price and panel. These read the built mod's
// blueprints next to its DLL and the game's own (StreamingAssets/Modding/Blueprints.zip), and fail if the game's crossing
// changes in a way the copy would miss, or if the pieces that put the building on the toolbar no longer fit together.
internal static class TradingPostBuildingChecks
{
    const string Folder = "Buildings/DistrictManagement/MultiColonyTradingPost";
    const string SpecName = "MultiColonyTradingPostSpec";
    static readonly string[] Factions = { "Folktails", "IronTeeth" };
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static void Run(Assembly mod, string modDirectory, string managedDirectory, Action<string, Action> test)
    {
        string zipPath = Path.GetFullPath(Path.Combine(managedDirectory, "..", "StreamingAssets", "Modding", "Blueprints.zip"));
        JsonNode GameBlueprint(string path)
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry entry = zip.GetEntry(path) ?? throw new Exception("the game has no " + path);
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            return JsonNode.Parse(reader.ReadToEnd())!;
        }
        JsonNode ModBlueprint(string path)
        {
            string file = Path.Combine(modDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) throw new Exception("the built mod has no " + path);
            return JsonNode.Parse(File.ReadAllText(file))!;
        }

        foreach (string faction in Factions)
        {
            test($"Trading Post: the {faction} one is the game's District Crossing but for its name, price, texts and mark", () =>
            {
                JsonNode crossing = GameBlueprint($"Buildings/DistrictManagement/DistrictCrossing/DistrictCrossing.{faction}.blueprint.json");
                JsonNode post = ModBlueprint($"{Folder}/MultiColonyTradingPost.{faction}.blueprint.json");
                Expect(post["TemplateSpec"]!["TemplateName"]!.GetValue<string>() == $"MultiColonyTradingPost.{faction}", "its template name");
                Expect(post["BuildingSpec"]!["ScienceCost"]!.GetValue<int>() == 0, "it needs no science");
                var cost = post["BuildingSpec"]!["BuildingCost"]!.AsArray();
                Expect(cost.Count == 1 && cost[0]!["Id"]!.GetValue<string>() == "Log" && cost[0]!["Amount"]!.GetValue<int>() == 10, "it costs 10 logs");
                Expect(post["PlaceableBlockObjectSpec"]!["ToolGroupId"]!.GetValue<string>() == "DistrictManagement", "it sits in the District Crossing's toolbar group");
                Expect(post["PlaceableBlockObjectSpec"]!["ToolOrder"]!.GetValue<int>() > crossing["PlaceableBlockObjectSpec"]!["ToolOrder"]!.GetValue<int>(),
                    "it comes after the District Crossing");
                Expect(post[SpecName] is JsonObject mark && mark.Count == 0, "it carries the mod's mark");
                Expect(post["DistrictCrossingSpec"] != null && post["LinkedBuildingSpec"] != null, "it works as a crossing of two linked halves");
                Expect(post["LabeledEntitySpec"]!["Icon"]!.GetValue<string>() == $"{Folder}/TradingPostIcon", "its own icon");
                // Everything else is the game's own, models and colliders included.
                var expected = crossing.DeepClone().AsObject();
                var actual = post.DeepClone().AsObject();
                actual.Remove(SpecName);
                foreach (JsonObject node in new[] { expected, actual })
                {
                    node.Remove("LabeledEntitySpec");
                    node["TemplateSpec"]!.AsObject().Remove("TemplateName");
                    node["BuildingSpec"]!.AsObject().Remove("ScienceCost");
                    node["BuildingSpec"]!.AsObject().Remove("BuildingCost");
                    node["PlaceableBlockObjectSpec"]!.AsObject().Remove("ToolOrder");
                }
                if (!JsonNode.DeepEquals(expected, actual))
                    throw new Exception("it differs from the game's District Crossing; generate it again from the game's blueprint");
            });

            test($"Trading Post: the {faction} toolbar lists it once", () =>
            {
                JsonNode game = GameBlueprint($"TemplateCollections/TemplateCollection.Buildings.{faction}.blueprint.json");
                JsonNode added = ModBlueprint($"TemplateCollections/TemplateCollection.Buildings.{faction}.blueprint.json");
                string id = game["TemplateCollectionSpec"]!["CollectionId"]!.GetValue<string>();
                Expect(added["TemplateCollectionSpec"]!["CollectionId"]!.GetValue<string>() == id, "the collection's id");
                var appended = added["TemplateCollectionSpec"]!["Blueprints#append"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
                string blueprint = $"{Folder}/MultiColonyTradingPost.{faction}.blueprint";
                Expect(appended.SequenceEqual(new[] { blueprint }), "it appends exactly the Trading Post");
                Expect(File.Exists(Path.Combine(modDirectory, (blueprint + ".json").Replace('/', Path.DirectorySeparatorChar))), "the appended file is built");
                var listed = game["TemplateCollectionSpec"]!["Blueprints"]!.AsArray().Select(n => n!.GetValue<string>());
                Expect(listed.Contains($"Buildings/DistrictManagement/DistrictCrossing/DistrictCrossing.{faction}.blueprint"),
                    "the game still has the District Crossing on this toolbar");
            });
        }

        test("Trading Post: the host pairs two halves where the game's tool lays one post down", () =>
        {
            // The game's own layout of the pair (AreaPicker.HalvesCoordinates), for a half of the post's size, in every
            // orientation, against the mod's ColonyGameWorld.SecondHalf and AreHalvesOfOnePost
            // (ColonyRulesService.JudgePairs judges the two halves of one post together).
            Assembly blockSystem = Assembly.Load("Timberborn.BlockSystem"), coordinates = Assembly.Load("Timberborn.Coordinates"),
                blueprints = Assembly.Load("Timberborn.BlueprintSystem");
            Type blockObjectSpec = blockSystem.GetType("Timberborn.BlockSystem.BlockObjectSpec", true)!;
            Type placeableSpec = blockSystem.GetType("Timberborn.BlockSystem.PlaceableBlockObjectSpec", true)!;
            Type layout = blockSystem.GetType("Timberborn.BlockSystem.BlockObjectLayout", true)!;
            Type componentSpec = blueprints.GetType("Timberborn.BlueprintSystem.ComponentSpec", true)!;
            Type blueprint = blueprints.GetType("Timberborn.BlueprintSystem.Blueprint", true)!;
            Type orientation = coordinates.GetType("Timberborn.Coordinates.Orientation", true)!;
            Type placement = coordinates.GetType("Timberborn.Coordinates.Placement", true)!;
            object unflipped = coordinates.GetType("Timberborn.Coordinates.FlipMode", true)!.GetField("Unflipped", All)!.GetValue(null)!;
            Type vector = Assembly.Load("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int", true)!;
            MethodInfo halves = Assembly.Load("Timberborn.AreaSelectionSystem").GetType("Timberborn.AreaSelectionSystem.AreaPicker", true)!
                .GetMethod("HalvesCoordinates", All) ?? throw new Exception("the game's AreaPicker.HalvesCoordinates is gone");
            Type world = mod.GetType("BeaverBuddies.Colonies.ColonyGameWorld", true)!;
            MethodInfo paired = world.GetMethod("AreHalvesOfOnePost", All, null, new[] { placement, placement, vector }, null)!;
            MethodInfo second = world.GetMethod("SecondHalf", All)!;
            object V(int x, int y, int z) => Activator.CreateInstance(vector, x, y, z)!;
            int At(object v, string axis) => (int)Get(v, axis);
            bool Same(object a, object b) => Get(a, "Coordinates").Equals(Get(b, "Coordinates")) && Get(a, "Orientation").Equals(Get(b, "Orientation"));

            JsonNode postSize = ModBlueprint($"{Folder}/MultiColonyTradingPost.Folktails.blueprint.json")["BlockObjectSpec"]!["Size"]!;
            foreach ((int sx, int sy, int sz) in new[] { (postSize["X"]!.GetValue<int>(), postSize["Y"]!.GetValue<int>(), postSize["Z"]!.GetValue<int>()) })
            {
                object spec = Activator.CreateInstance(blockObjectSpec)!;
                blockObjectSpec.GetProperty("Size")!.SetValue(spec, V(sx, sy, sz));
                object placeable = Activator.CreateInstance(placeableSpec)!;
                placeableSpec.GetProperty("Layout")!.SetValue(placeable, Enum.Parse(layout, "Half"));
                Array specs = Array.CreateInstance(componentSpec, 2);
                specs.SetValue(spec, 0);
                specs.SetValue(placeable, 1);
                object noChildren = typeof(ImmutableArray<>).MakeGenericType(blueprint).GetField("Empty")!.GetValue(null)!;
                object built = Activator.CreateInstance(blueprint, "TradingPostPairCheck", specs, noChildren)!;
                object half = blueprint.GetMethods().Single(m => m.Name == "GetSpec" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                    .MakeGenericMethod(placeableSpec).Invoke(built, null)!;
                object size = V(sx, sy, sz);
                foreach (object turned in Enum.GetValues(orientation))
                {
                    var laid = ((IEnumerable)halves.Invoke(null, new[] { half, V(10, 20, 2), turned, unflipped })!).Cast<object>().ToList();
                    string where = $"{sx} × {sy}, {turned}";
                    Expect(laid.Count == 2, where + ": the game lays down two halves");
                    Expect(Same(second.Invoke(null, new[] { laid[0], size })!, laid[1]), where + ": the second half is where the game puts it");
                    Expect((bool)paired.Invoke(null, new[] { laid[0], laid[1], size })! && (bool)paired.Invoke(null, new[] { laid[1], laid[0], size })!,
                        where + ": the two are paired, either way round");
                    object far = Get(laid[1], "Coordinates");
                    object moved = Activator.CreateInstance(placement, V(At(far, "x") + 1, At(far, "y"), At(far, "z")), Get(laid[1], "Orientation"),
                        Get(laid[1], "FlipMode"))!;
                    Expect(!(bool)paired.Invoke(null, new[] { laid[0], moved, size })!, where + ": a half a block away is not its partner");
                    object same = Activator.CreateInstance(placement, far, Get(laid[0], "Orientation"), Get(laid[1], "FlipMode"))!;
                    Expect(!(bool)paired.Invoke(null, new[] { laid[0], same, size })!, where + ": a half not turned round is not its partner");
                }
            }
        });

        test("Trading Post: its icon is a sprite in the built mod", () =>
        {
            string icon = Path.Combine(modDirectory, Folder.Replace('/', Path.DirectorySeparatorChar), "TradingPostIcon.png");
            Expect(File.Exists(icon) && File.ReadAllBytes(icon).Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }), "a PNG");
            JsonNode meta = JsonNode.Parse(File.ReadAllText(icon + ".meta.json"))!;
            Expect(meta["isSprite"]!.GetValue<bool>(), "marked as a sprite");
        });

        test("Trading Post: its mark is a spec the game finds by its name, which no game assembly uses", () =>
        {
            Type spec = mod.GetType("BeaverBuddies.Colonies." + SpecName, true)!;
            Type componentSpec = Assembly.Load("Timberborn.BlueprintSystem").GetType("Timberborn.BlueprintSystem.ComponentSpec", true)!;
            Expect(componentSpec.IsAssignableFrom(spec) && !spec.IsAbstract && spec.IsPublic, "a public component spec");
            // The game maps every ComponentSpec's class name to its type in one dictionary: a second class of this name
            // anywhere would stop its specs loading.
            byte[] name = Encoding.UTF8.GetBytes(SpecName);
            var clashes = Directory.GetFiles(managedDirectory, "*.dll").Where(dll => Contains(File.ReadAllBytes(dll), name))
                .Select(Path.GetFileName).ToList();
            Expect(clashes.Count == 0, "the name also appears in " + string.Join(", ", clashes));
        });
    }

    static object Get(object target, string member)
    {
        Type type = target.GetType();
        PropertyInfo? property = type.GetProperty(member, All);
        if (property != null) return property.GetValue(target)!;
        return type.GetField(member, All)?.GetValue(target) ?? throw new Exception($"{type.Name} has no {member}");
    }

    static void Expect(bool condition, string what)
    {
        if (!condition) throw new Exception("wrong: " + what);
    }

    static bool Contains(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return true;
        }
        return false;
    }
}
