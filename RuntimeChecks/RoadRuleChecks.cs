#nullable enable
using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

// The road rule (1.4.0-beta15), against the game's assemblies and the built mod: there is no land, and two colonies'
// roads meet only at a Trading Post, one colony's road at each of its ends. These fail if the game stops putting a
// building's road where the rule looks for it, or places a Trading Post's second half somewhere else.
internal static class RoadRuleChecks
{
    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const BindingFlags declared = all | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, string modDirectory, string managedDirectory, Action<string, Action> test)
    {
        var blockSystem = Assembly.Load("Timberborn.BlockSystem");
        Type entrance = blockSystem.GetType("Timberborn.BlockSystem.PositionedEntrance", true)!;

        // Every method of these types and of their compiler-made nested types (lambdas, iterators).
        IEnumerable<MethodBase> Methods(params Type[] types) => types
            .SelectMany(t => new[] { t }.Concat(t.GetNestedTypes(all)))
            .SelectMany(t => t.GetMethods(declared).Cast<MethodBase>().Concat(t.GetConstructors(declared)))
            .Where(m => m.GetMethodBody() != null);
        bool Calls(IEnumerable<MethodBase> methods, string type, string name) =>
            methods.Any(m => IlScan.Instructions(m).Any(i => i.Calls && i.Is(type, name)));

        test("Roads: a building's road goes at its entrance cell, outside it; the game's doorstep is its own cell inside", () =>
        {
            // PositionedEntrance.Coordinates is the blueprint's entrance cell placed with the building (Blocks.Transform),
            // and the game's path connection asks for a path there, where the entrance is marked. DoorstepCoordinates
            // is one step back into the building (Coordinates - direction), the cell a district center is centred on.
            var from = entrance.GetMethod("From", all)!;
            if (!Calls(new[] { from }, "Timberborn.BlockSystem.EntranceBlockSpec", "get_Coordinates")
                || !Calls(new[] { from }, "Timberborn.BlockSystem.Blocks", "Transform"))
                throw new Exception("PositionedEntrance.From no longer places the blueprint's entrance cell");
            var doorstep = entrance.GetProperty("DoorstepCoordinates", all)!.GetMethod!;
            if (!Calls(new[] { doorstep }, "Timberborn.BlockSystem.PositionedEntrance", "get_Coordinates")
                || !Calls(new[] { doorstep }, "Timberborn.Coordinates.Direction2DExtensions", "ToOffset"))
                throw new Exception("the doorstep is no longer a step from the entrance cell");
            Type connection = Assembly.Load("Timberborn.PathSystem").GetType("Timberborn.PathSystem.ConnectionService", true)!;
            var atEntrance = connection.GetMethod("IsEntranceInDirectionAt", all) ?? throw new Exception("the game's path connection check is gone");
            if (!Calls(new[] { atEntrance }, "Timberborn.PathSystem.IPathService", "IsPath")
                || !Calls(new[] { atEntrance }, "Timberborn.BlockSystem.IBlockService", "GetEntrancesAt"))
                throw new Exception("the game no longer wants a path on the entrance cell");
        });

        test("Roads: the colony rules look for a building's road at its entrance cell, never at the game's doorstep", () =>
        {
            var readers = Methods(mod.GetType("BeaverBuddies.Colonies.ColonyGameWorld", true)!,
                mod.GetType("BeaverBuddies.Colonies.ColonyPlacementValidator", true)!).ToList();
            if (!Calls(readers, "Timberborn.BlockSystem.PositionedEntrance", "get_Coordinates"))
                throw new Exception("the rules no longer read the entrance cell");
            if (Calls(readers, "Timberborn.BlockSystem.PositionedEntrance", "get_DoorstepCoordinates"))
                throw new Exception("the rules read the doorstep, the building's own cell, where no road can be");
            // Whose road a cell holds: a path (finished or not) by its colony, then the finished roads.
            if (!Calls(readers, "Timberborn.BlockSystem.IBlockService", "GetPathObjectAt"))
                throw new Exception("the rules no longer see paths still being built");
        });

        test("Roads: the game places a Trading Post's second half turned round, straight behind the first", () =>
        {
            Type area = Assembly.Load("Timberborn.AreaSelectionSystem").GetTypes()
                .FirstOrDefault(t => t.GetMethod("HalvesCoordinates", all) != null) ?? throw new Exception("the game's halves layout is gone");
            var halves = Methods(area).Where(m => m.Name == "HalvesCoordinates" || m.DeclaringType!.Name.Contains("HalvesCoordinates")).ToList();
            if (!Calls(halves, "Timberborn.Coordinates.OrientationExtensions", "Flip"))
                throw new Exception("the second half is no longer turned round");
            if (!Calls(halves, "Timberborn.BlockSystem.BlockObjectSpec", "get_Size"))
                throw new Exception("the second half is no longer placed by the building's size");
        });

        test("Roads: the game's paths, stairs, bridges, gates and district centers carry a road (PathSpec); a Trading Post does not", () =>
        {
            // The rule counts as a road whatever carries the game's PathSpec (its own IsPath test), finished or not, and
            // judges a placement carrying one by its whole footprint. The Trading Post must not be one: its halves stand
            // between two colonies' roads.
            string zipPath = Path.GetFullPath(Path.Combine(managedDirectory, "..", "StreamingAssets", "Modding", "Blueprints.zip"));
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            bool Carries(string path)
            {
                ZipArchiveEntry entry = zip.GetEntry(path) ?? throw new Exception("the game has no " + path);
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!["PathSpec"] != null;
            }
            foreach (string road in new[] { "Buildings/Paths/Path/Path.blueprint.json", "Buildings/Paths/Stairs/Stairs.Folktails.blueprint.json",
                "Buildings/Paths/SuspensionBridge1x1/SuspensionBridge1x1.IronTeeth.blueprint.json", "Buildings/Paths/Gate/Gate.Folktails.blueprint.json",
                "Buildings/DistrictManagement/DistrictCenter/DistrictCenter.Folktails.blueprint.json" })
                if (!Carries(road)) throw new Exception(road + " no longer carries PathSpec: the rule would not see it as a road");
            if (Carries("Buildings/DistrictManagement/DistrictCrossing/DistrictCrossing.Folktails.blueprint.json"))
                throw new Exception("the District Crossing (the Trading Post's model) now carries PathSpec");
            string post = Path.Combine(modDirectory, "Buildings", "DistrictManagement", "MultiColonyTradingPost", "MultiColonyTradingPost.Folktails.blueprint.json");
            if (JsonNode.Parse(File.ReadAllText(post))!["PathSpec"] != null) throw new Exception("the Trading Post carries PathSpec");
            // The game's own test for a path is the same: an object in the cell's path layer with PathSpec.
            Type pathService = Assembly.Load("Timberborn.PathSystem").GetType("Timberborn.PathSystem.PathService", true)!;
            if (!Calls(Methods(pathService), "Timberborn.BlockSystem.IBlockService", "GetPathObjectAt"))
                throw new Exception("the game no longer finds paths in the cell's path layer");
        });

        foreach (string faction in new[] { "Folktails", "IronTeeth" })
        {
            test($"Roads: the {faction} Trading Post's other road end is where the game puts the other half's", () =>
            {
                string file = Path.Combine(modDirectory, "Buildings", "DistrictManagement", "MultiColonyTradingPost",
                    $"MultiColonyTradingPost.{faction}.blueprint.json");
                JsonNode post = JsonNode.Parse(File.ReadAllText(file))!;
                JsonNode block = post["BlockObjectSpec"]!;
                int sizeX = block["Size"]!["X"]!.GetValue<int>(), sizeY = block["Size"]!["Y"]!.GetValue<int>();
                int ex = block["Entrance"]!["Coordinates"]!["X"]!.GetValue<int>(), ey = block["Entrance"]!["Coordinates"]!["Y"]!.GetValue<int>();
                if (post["PlaceableBlockObjectSpec"]!["Layout"]!.GetValue<string>() != "Half") throw new Exception("it is not placed as two halves");
                if (ey >= 0 && ey < sizeY && ex >= 0 && ex < sizeX) throw new Exception("its entrance cell is inside it");
                // The game's second half (HalvesCoordinates): at (size.x - 1, 2 * size.y - 1), turned round, so its cell
                // (a, b) is the first half's (size.x - 1 - a, 2 * size.y - 1 - b).
                (int x, int y) partner = (sizeX - 1 - ex, 2 * sizeY - 1 - ey);

                Type cellType = mod.GetType("BeaverBuddies.Colonies.ColonyCell", true)!;
                object Cell(int x, int y) => Activator.CreateInstance(cellType, x, y, 0)!;
                var footprint = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(cellType))!;
                for (int x = 0; x < sizeX; x++)
                    for (int y = 0; y < sizeY; y++) footprint.Add(Cell(x, y));
                MethodInfo far = mod.GetType("BeaverBuddies.Colonies.ColonyRoadRule", true)!.GetMethod("FarEntrance", all)!;
                object? found = far.Invoke(null, new[] { footprint, Cell(ex, ey) });
                if (found == null || !found.Equals(Cell(partner.x, partner.y)))
                    throw new Exception($"the rule looks for the other road at {found ?? "nowhere"}, the game puts it at {partner}");
            });
        }
    }
}
