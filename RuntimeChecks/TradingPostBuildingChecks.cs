using System.Collections;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

// The Trading Post is a building the mod adds with blueprints (BeaverBuddies/Buildings, BeaverBuddies/TemplateCollections):
// the game's District Crossing and its workings, under another name, price and panel, with its own models and a half
// two blocks deep instead of one. These read the built mod's blueprints and models next to its DLL and the game's own
// blueprints (StreamingAssets/Modding/Blueprints.zip). They fail if the game's crossing changes in a way the copy would
// miss, if a model would not load in a game of its faction, if the half's shape no longer fits together, or if the
// pieces that put the building on the toolbar no longer fit together.
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
        // The material names a game loads for a collection (each is an asset path ending in the material's name).
        IEnumerable<string> GameMaterials(string collection) =>
            GameBlueprint($"MaterialCollections/MaterialCollection.{collection}.blueprint.json")["MaterialCollectionSpec"]!["Materials"]!
                .AsArray().Select(m => m!.GetValue<string>().Split('/').Last());

        foreach (string faction in Factions)
        {
            test($"Trading Post: the {faction} one is the game's District Crossing but for its name, price, texts, mark and 3 × 2 half", () =>
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
                // Everything else is the game's own: workers, slots, the entrance, the district obstacle... The shape is
                // checked on its own below: size and blocks, where the cursor holds the pair, where goods are taken, the
                // walkway, and the models, colliders and construction base.
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
                    node["BlockObjectSpec"]!.AsObject().Remove("Size");
                    node["BlockObjectSpec"]!.AsObject().Remove("Blocks");
                    node["PlaceableBlockObjectSpec"]!.AsObject().Remove("CustomPivot");
                    node["BuildingAccessibleSpec"]!.AsObject().Remove("LocalAccess");
                    foreach (JsonNode? group in node["BlockObjectNavMeshSettingsSpec"]!["EdgeGroups"]!.AsArray()) group!.AsObject().Remove("AddedEdges");
                    node.Remove("Children");
                }
                if (!JsonNode.DeepEquals(expected, actual))
                    throw new Exception("it differs from the game's District Crossing; generate it again from the game's blueprint");
            });

            test($"Trading Post: the {faction} half is 3 × 2 × 3 and walkable through its middle into the other half", () =>
            {
                JsonNode crossing = GameBlueprint($"Buildings/DistrictManagement/DistrictCrossing/DistrictCrossing.{faction}.blueprint.json");
                JsonNode post = ModBlueprint($"{Folder}/MultiColonyTradingPost.{faction}.blueprint.json");
                JsonNode blockObject = post["BlockObjectSpec"]!;
                Expect(IsV3(blockObject["Size"], 3, 2, 3), "its size: 3 wide, 2 deep, 3 high");
                // The game orders blocks x first, then y, then z: the first 6 are the ground. Each is the crossing's own
                // ground block or the one above it.
                var blocks = blockObject["Blocks"]!.AsArray();
                Expect(blocks.Count == 18, "18 blocks");
                JsonNode ground = crossing["BlockObjectSpec"]!["Blocks"]![0]!, above = crossing["BlockObjectSpec"]!["Blocks"]![3]!;
                for (int i = 0; i < blocks.Count; i++)
                    Expect(JsonNode.DeepEquals(blocks[i], i < 6 ? ground : above), $"block {i} is the crossing's {(i < 6 ? "ground" : "upper")} block");
                Expect(IsV3(blockObject["Entrance"]!["Coordinates"], 1, -1, 0), "the door is in front of the middle block");
                JsonNode pivot = post["PlaceableBlockObjectSpec"]!["CustomPivot"]!;
                Expect(pivot["HasCustomPivot"]!.GetValue<bool>() && IsV3(pivot["Coordinates"], 1.5, 2, 0),
                    "the cursor holds the pair by the line where its halves meet");
                Expect(IsV3(post["BuildingAccessibleSpec"]!["LocalAccess"], 1.5, 0, 2), "goods are taken to the middle of that line");
                Expect(Edges(post).SequenceEqual(new[] { ((1, 0, 0), (1, -1, 0), false), ((1, 0, 0), (1, 1, 0), true), ((1, 1, 0), (1, 2, 0), false) }),
                    "the walkway: out of the door, along the middle, and into the other half (which adds its own way back)");
                Expect(Edges(crossing).SequenceEqual(new[] { ((1, 0, 0), (1, -1, 0), false), ((1, 0, 0), (1, 1, 0), false) }),
                    "the game's crossing still walks out of its door and into its other half, one block deep");

                JsonNode finished = post["Children"]!["#Finished"]!;
                JsonNode unfinished = post["Children"]!["#Unfinished"]!["Children"]!;
                Expect(finished["TimbermeshSpec"]!["Model"]!.GetValue<string>() == $"{Folder}/TradingPost.{faction}.Model", "its own finished model");
                Expect(unfinished["ConstructionStage0"]!["TimbermeshSpec"]!["Model"]!.GetValue<string>() == $"{Folder}/TradingPost.{faction}.ConstructionStage0.Model",
                    "its own construction stage");
                JsonNode constructionBase = GameBlueprint(unfinished["ConstructionBase3x2#nested"]!["BlueprintPath"]!.GetValue<string>() + ".json");
                Expect(IsV3(constructionBase["CollidersSpec"]!["BoxColliders"]![0]!["Size"], 3, 0.1, 2), "it stands on the game's 3 × 2 construction base");
                foreach (JsonNode? collider in finished["CollidersSpec"]!["BoxColliders"]!.AsArray()
                    .Concat(unfinished["ConstructionStage0"]!["CollidersSpec"]!["BoxColliders"]!.AsArray()))
                {
                    // World space: x across, y up, z deep (3 wide, 3 high, 2 deep).
                    foreach ((string axis, double limit) in new[] { ("X", 3.0), ("Y", 3.0), ("Z", 2.0) })
                    {
                        double center = collider!["Center"]![axis]!.GetValue<double>(), half = collider["Size"]![axis]!.GetValue<double>() / 2;
                        Expect(center - half >= -0.001 && center + half <= limit + 0.001, $"a collider stays in the blocks ({axis})");
                    }
                }
            });

            test($"Trading Post: the {faction} models load with the game's own reader and use only materials a {faction} game has", () =>
            {
                JsonNode post = ModBlueprint($"{Folder}/MultiColonyTradingPost.{faction}.blueprint.json");
                // A game loads the common collection and its own faction's (a mixed game both factions').
                var materials = GameMaterials("Common").Concat(GameMaterials(faction)).ToHashSet();
                string keyword = post["TransformSlotInitializerSpec"]!["Slots"]![0]!["SlotKeyword"]!.GetValue<string>();
                MethodInfo read = Assembly.Load("Timberborn.Timbermesh").GetType("Timberborn.Timbermesh.TimbermeshReader", true)!
                    .GetMethod("ReadFromStream", All)!;
                JsonNode children = post["Children"]!;
                foreach ((string model, bool finished) in new[]
                {
                    (children["#Finished"]!["TimbermeshSpec"]!["Model"]!.GetValue<string>(), true),
                    (children["#Unfinished"]!["Children"]!["ConstructionStage0"]!["TimbermeshSpec"]!["Model"]!.GetValue<string>(), false),
                })
                {
                    // The game finds a mod's model by its path without the extension (ModTimbermeshConverter).
                    string file = Path.Combine(modDirectory, (model + ".timbermesh").Replace('/', Path.DirectorySeparatorChar));
                    Expect(File.Exists(file), "the built mod has " + model + ".timbermesh");
                    object loaded;
                    using (FileStream stream = File.OpenRead(file)) loaded = read.Invoke(null, new object[] { stream })!;
                    int meshes = 0;
                    bool slot = false;
                    foreach (object node in (IEnumerable)Get(loaded, "Nodes"))
                    {
                        string name = (string)Get(node, "Name");
                        if (name.StartsWith("#Slot#" + keyword))
                        {
                            // The workers' slot (TransformSlotInitializer: "#Slot#" + the blueprint's keyword), inside the door.
                            object at = Get(node, "Position");
                            float x = (float)Get(at, "X"), z = (float)Get(at, "Z");
                            Expect(x > 1 && x < 2 && z >= 0 && z < 0.5f, $"{name} is just inside the door");
                            slot = true;
                        }
                        int vertices = (int)Get(node, "VertexCount");
                        if (vertices == 0) continue;
                        meshes++;
                        var properties = ((IEnumerable)Get(node, "VertexProperties")).Cast<object>().ToDictionary(p => (string)Get(p, "Name"));
                        foreach (string needed in new[] { "position", "normal", "uv0", "color" })
                            Expect(properties.ContainsKey(needed), $"{model} has {needed} (the building shader multiplies by color)");
                        byte[] colour = (byte[])Get(properties["color"], "Data");
                        Expect(colour.Length == vertices * 16, $"{model} has a colour for every vertex");
                        byte[] positions = (byte[])Get(properties["position"], "Data");
                        Expect(positions.Length == vertices * 12, $"{model} has a position for every vertex");
                        for (int i = 0; i < vertices; i++)
                        {
                            // The model's own space: x across, y up, z deep.
                            float x = BitConverter.ToSingle(positions, 12 * i), y = BitConverter.ToSingle(positions, 12 * i + 4),
                                z = BitConverter.ToSingle(positions, 12 * i + 8);
                            Expect(x >= -0.02f && x <= 3.02f && y >= -0.02f && y <= 3f && z >= -0.02f && z <= 2.02f,
                                $"{model} stays in its 3 × 2 × 3 blocks ({x}, {y}, {z})");
                        }
                        foreach (object mesh in (IEnumerable)Get(node, "Meshes"))
                        {
                            string material = (string)Get(mesh, "Material");
                            Expect(materials.Contains(material), $"{model} uses {material}, which a {faction} game loads");
                        }
                    }
                    Expect(meshes > 0, model + " has a mesh");
                    if (finished) Expect(slot, $"{model} has a \"#Slot#{keyword}\" node for its workers");
                }
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
            // The game's own layout of the pair (AreaPicker.HalvesCoordinates), for a half of the post's size and of the
            // crossing's, in every orientation, against the mod's ColonyGameWorld.SecondHalf and AreHalvesOfOnePost
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
            foreach ((int sx, int sy, int sz) in new[] { (postSize["X"]!.GetValue<int>(), postSize["Y"]!.GetValue<int>(), postSize["Z"]!.GetValue<int>()), (3, 1, 2) })
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

    static List<((int, int, int), (int, int, int), bool)> Edges(JsonNode blueprint) =>
        blueprint["BlockObjectNavMeshSettingsSpec"]!["EdgeGroups"]![0]!["AddedEdges"]!.AsArray()
            .Select(e => (Cell(e!["Start"]!), Cell(e["End"]!), e["IsTwoWay"]!.GetValue<bool>())).ToList();

    static (int, int, int) Cell(JsonNode v) => (v["X"]!.GetValue<int>(), v["Y"]!.GetValue<int>(), v["Z"]!.GetValue<int>());

    static bool IsV3(JsonNode? v, double x, double y, double z) =>
        v != null && Math.Abs(v["X"]!.GetValue<double>() - x) < 1e-6 && Math.Abs(v["Y"]!.GetValue<double>() - y) < 1e-6
        && Math.Abs(v["Z"]!.GetValue<double>() - z) < 1e-6;

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
