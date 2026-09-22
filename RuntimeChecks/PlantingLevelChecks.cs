#nullable enable
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

// Planting marks are levelled by the game's terrain picker, which stops at the layer the local player has sliced the
// view to. The planting event carries the tiles the marker levelled, so no computer levels them again with its own view.
// Runs the game's real levelling (TerrainAreaService, TerrainPicker, GridTraversal) over a small made-up terrain.
internal static class PlantingLevelChecks
{
    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // Bound to the mod's ColonyTile at run time (see below): keeps the tiles in column x = 1.
    private static bool KeepColumnOne<T>(T tile) => (int)typeof(T).GetField("X")!.GetValue(tile)! == 1;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        var unity = Assembly.Load("UnityEngine.CoreModule");
        Type vector3Int = unity.GetType("UnityEngine.Vector3Int", true)!;
        Type vector3 = unity.GetType("UnityEngine.Vector3", true)!;
        Type rayType = unity.GetType("UnityEngine.Ray", true)!;
        Type terrainServiceType = Assembly.Load("Timberborn.TerrainSystem").GetType("Timberborn.TerrainSystem.ITerrainService", true)!;
        Type visibilityType = Assembly.Load("Timberborn.LevelVisibilitySystem").GetType("Timberborn.LevelVisibilitySystem.ILevelVisibilityService", true)!;
        Type mapSizeType = Assembly.Load("Timberborn.MapStateSystem").GetType("Timberborn.MapStateSystem.MapSize", true)!;
        Type traversalType = Assembly.Load("Timberborn.GridTraversing").GetType("Timberborn.GridTraversing.GridTraversal", true)!;
        var querying = Assembly.Load("Timberborn.TerrainQueryingSystem");
        Type pickerType = querying.GetType("Timberborn.TerrainQueryingSystem.TerrainPicker", true)!;
        Type areaServiceType = querying.GetType("Timberborn.TerrainQueryingSystem.TerrainAreaService", true)!;
        Type selectionType = Assembly.Load("Timberborn.PlantingUI").GetType("Timberborn.PlantingUI.PlantingSelectionService", true)!;
        Type eventType = mod.GetType("BeaverBuddies.Events.PlantingAreaMarkedEvent", true)!;
        Type overrideType = mod.GetType("BeaverBuddies.Events.PlantingLeveledCoordinatesPatcher", true)!;
        Type contextType = mod.GetType("BeaverBuddies.Events.IReplayContext", true)!;
        Type listType = typeof(List<>).MakeGenericType(vector3Int);
        FieldInfo recorded = overrideType.GetField("Recorded", all)!;

        object V(int x, int y, int z) => Activator.CreateInstance(vector3Int, x, y, z)!;
        (int x, int y, int z) Xyz(object v) => ((int)vector3Int.GetProperty("x")!.GetValue(v)!,
            (int)vector3Int.GetProperty("y")!.GetValue(v)!, (int)vector3Int.GetProperty("z")!.GetValue(v)!);
        IList NewList(IEnumerable<(int x, int y, int z)> tiles)
        {
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var t in tiles) list.Add(V(t.x, t.y, t.z));
            return list;
        }
        List<(int, int, int)> Tiles(IEnumerable items) => items.Cast<object>().Select(Xyz).ToList();
        string Show(IEnumerable<(int, int, int)> tiles) => "[" + string.Join(" ", tiles) + "]";

        // An 8x8 map: ground at height 3, and a raised block at height 6 where x < 4.
        static int Height(int x, int y) => x < 4 ? 6 : 3;
        var terrain = DispatchProxy.Create(terrainServiceType, typeof(TerrainProxy));
        ((TerrainProxy)terrain).Height = Height;

        object MapSize()
        {
            object size = RuntimeHelpers.GetUninitializedObject(mapSizeType);
            mapSizeType.GetProperty("TotalSize")!.GetSetMethod(true)!.Invoke(size, new[] { V(8, 8, 12) });
            return size;
        }
        // The game's levelling as one player's view sees it: sliced to show levels below maxVisibleLevel.
        object AreaService(int maxVisibleLevel)
        {
            var visibility = DispatchProxy.Create(visibilityType, typeof(VisibilityProxy));
            ((VisibilityProxy)visibility).MaxVisibleLevel = maxVisibleLevel;
            object traversal = Activator.CreateInstance(traversalType, MapSize())!;
            object picker = Activator.CreateInstance(pickerType, terrain, traversal, visibility)!;
            return Activator.CreateInstance(areaServiceType, terrain, picker)!;
        }
        // Looking almost straight down on the raised block.
        object ray = Activator.CreateInstance(rayType,
            Activator.CreateInstance(vector3, 2.5f, 2.5f, 20f)!, Activator.CreateInstance(vector3, 0.01f, 0.02f, -1f)!)!;
        List<(int, int, int)> Leveled(object areaService, IList blocks) =>
            Tiles((IEnumerable)areaServiceType.GetMethod("InMapLeveledCoordinates")!.Invoke(areaService, new[] { blocks, ray })!);
        // The planting tool's drag: a rectangle on the level of the terrain the ray picked first (AreaPicker).
        IList Dragged(object areaService)
        {
            object picker = areaServiceType.GetField("_terrainPicker", all)!.GetValue(areaService)!;
            object? picked = pickerType.GetMethod("PickTerrainCoordinates", new[] { rayType })!.Invoke(picker, new[] { ray });
            if (picked == null) throw new Exception("the ray picked no terrain");
            int z = Xyz(picked.GetType().GetProperty("Coordinates")!.GetValue(picked)!).z;
            return NewList(from x in Enumerable.Range(1, 5) from y in Enumerable.Range(1, 3) select (x, y, z));
        }
        var levelAbove = eventType.GetMethod("LevelAbove", all)!;
        var onGround = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(vector3Int, typeof(bool)), terrain,
            terrainServiceType.GetMethod("OnGround")!);
        List<(int, int, int)> LevelAbove(IList blocks) => Tiles((IEnumerable)levelAbove.Invoke(null, new object[] { blocks, onGround })!);

        const int FullView = 100, Sliced = 3;

        test("Planting: the game levels the same drag differently in a sliced view (why events carry the tiles)", () =>
        {
            IList blocks = Dragged(AreaService(FullView));
            var full = Leveled(AreaService(FullView), blocks);
            var sliced = Leveled(AreaService(Sliced), blocks);
            if (full.Count == 0) throw new Exception("the full view marked nothing");
            if (full.SequenceEqual(sliced))
                throw new Exception($"both views gave {Show(full)}: the game no longer levels by view; the recorded tiles may be unneeded");
        });

        foreach (int view in new[] { FullView, Sliced })
            test($"Planting: an older event is levelled as the marker's own game did (view {view})", () =>
            {
                object marker = AreaService(view);
                IList blocks = Dragged(marker);
                var expected = Leveled(marker, blocks);
                var fallback = LevelAbove(blocks);
                if (!fallback.SequenceEqual(expected)) throw new Exception($"fallback {Show(fallback)}, the game {Show(expected)}");
                if (view == Sliced && fallback.Count == 0) throw new Exception("the sliced view marked nothing");
            });

        test("Planting: while an event is played, levelling gives the recorded tiles", () =>
        {
            var tiles = NewList(new[] { (1, 2, 6), (5, 5, 3) });
            var prefix = overrideType.GetMethod("Prefix", all)!;
            try
            {
                recorded.SetValue(null, tiles);
                object?[] args = { null };
                if ((bool)prefix.Invoke(null, args)!) throw new Exception("the game's levelling ran");
                if (!Tiles((IEnumerable)args[0]!).SequenceEqual(Tiles(tiles))) throw new Exception("other tiles were given");
                if (ReferenceEquals(args[0], tiles)) throw new Exception("the event's own list was handed to the game");
                recorded.SetValue(null, null);
                args[0] = null;
                if (!(bool)prefix.Invoke(null, args)!) throw new Exception("the game's levelling was skipped outside a replay");
            }
            finally { recorded.SetValue(null, null); }
        });

        // The override replaces the game's levelling while an event is played. A prefix that skips the original runs
        // after every other mod's prefix on the same method, as the Stability Fork's port of this fix does.
        bool Targets(CustomAttributeData a, string method) => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"
            && a.ConstructorArguments.Count == 2
            && Equals(a.ConstructorArguments[0].Value, areaServiceType) && Equals(a.ConstructorArguments[1].Value, method);
        int? DeclaredPriority(MemberInfo member) => member.GetCustomAttributesData()
            .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPriority")
            .Select(a => (int?)(int)a.ConstructorArguments[0].Value!).FirstOrDefault();
        // Harmony's own default, Priority.Normal, when neither the prefix nor its class names one.
        int Priority(MethodInfo prefix) => DeclaredPriority(prefix) ?? DeclaredPriority(prefix.DeclaringType!) ?? 400;

        test("Planting: the levelling override lets other mods' prefixes run first (Priority.Last)", () =>
        {
            var prefix = overrideType.GetMethod("Prefix", all)!;
            if (!overrideType.GetCustomAttributesData().Any(a => Targets(a, "InMapLeveledCoordinates")))
                throw new Exception("the override does not patch TerrainAreaService.InMapLeveledCoordinates");
            if (Priority(prefix) != 0) throw new Exception($"the override's priority is {Priority(prefix)}, not Priority.Last (0)");
        });

        test("Planting: colony rules judge and trim the recorded tiles", () =>
        {
            object e = Activator.CreateInstance(eventType, true)!;
            eventType.GetField("prefabName")!.SetValue(e, "Carrot");
            IList input = NewList(new[] { (1, 1, 2), (2, 1, 2), (3, 1, 2) });
            eventType.GetField("inputBlocks")!.SetValue(e, input);
            object Scope() => eventType.GetMethod("GetColonyScope")!.Invoke(e, null)!;
            object ListOf(object scope) =>
                (scope.GetType().GetField("TileList", all)?.GetValue(scope) ?? scope.GetType().GetProperty("TileList", all)!.GetValue(scope))!;
            int Count(object list) => (int)list.GetType().GetProperty("Count")!.GetValue(list)!;

            // Older event: the dragged blocks, as before.
            if (Count(ListOf(Scope())) != 3) throw new Exception("an older event was not judged by its dragged blocks");

            IList coordinates = NewList(new[] { (1, 1, 3), (2, 1, 3) });
            eventType.GetField("coordinates")!.SetValue(e, coordinates);
            object list = ListOf(Scope());
            if (Count(list) != 2) throw new Exception("the recorded tiles were not the ones judged");
            // The host removes the tiles the actor may not use from the list it judged: the list the replay marks.
            // The tiles are judged as the mod's own ColonyTile (a column: x and y), not as names.
            Type tileType = eventType.Assembly.GetType("BeaverBuddies.Colonies.ColonyTile")!;
            Delegate keepX1 = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(tileType, typeof(bool)),
                typeof(PlantingLevelChecks).GetMethod(nameof(KeepColumnOne), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(tileType));
            list.GetType().GetMethod("Filter")!.Invoke(list, new object[] { keepX1 });
            if (Show(Tiles(coordinates)) != Show(new[] { (1, 1, 3) })) throw new Exception($"recorded tiles left {Show(Tiles(coordinates))}");
            if (input.Count != 3) throw new Exception("the dragged blocks were trimmed instead");
        });

        test("Planting: a replay that fails leaves no recorded tiles behind", () =>
        {
            object service = RuntimeHelpers.GetUninitializedObject(selectionType);
            // Unset picker: if the game levelled again here it would throw at once.
            selectionType.GetField("_terrainAreaService", all)!.SetValue(service, RuntimeHelpers.GetUninitializedObject(areaServiceType));
            var context = (SingletonContextProxy)DispatchProxy.Create(contextType, typeof(SingletonContextProxy));
            context.Singleton = service;
            object e = Activator.CreateInstance(eventType, true)!;
            eventType.GetField("prefabName")!.SetValue(e, "Carrot");
            eventType.GetField("inputBlocks")!.SetValue(e, NewList(new[] { (1, 1, 2) }));
            eventType.GetField("coordinates")!.SetValue(e, NewList(new[] { (1, 1, 3) }));
            try { eventType.GetMethod("Replay")!.Invoke(e, new object[] { context }); }
            catch (TargetInvocationException) { /* the unset game services: expected here */ }
            if (recorded.GetValue(null) != null) throw new Exception("recorded tiles leaked out of the replay");
        });
    }
}

public class TerrainProxy : DispatchProxy
{
    public Func<int, int, int> Height = (_, _) => 0;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        object tile = args![0]!;
        Type type = tile.GetType();
        int x = (int)type.GetProperty("x")!.GetValue(tile)!, y = (int)type.GetProperty("y")!.GetValue(tile)!, z = (int)type.GetProperty("z")!.GetValue(tile)!;
        bool inside = x >= 0 && y >= 0 && x < 8 && y < 8;
        return targetMethod!.Name switch
        {
            "Underground" => inside && z < Height(x, y),
            "OnGround" => inside && z == Height(x, y),
            _ => throw new NotSupportedException(targetMethod.Name),
        };
    }
}

public class VisibilityProxy : DispatchProxy
{
    public int MaxVisibleLevel;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod!.Name == "get_MaxVisibleLevel" ? MaxVisibleLevel : throw new NotSupportedException(targetMethod.Name);
}

public class SingletonContextProxy : DispatchProxy
{
    public object? Singleton;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Singleton;
}
