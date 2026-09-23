#nullable enable
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: reviewer D: cost at late-game size, long sessions, compatibility, and the reporting Kyler's sessions read.
internal static class RcPerformanceRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");
        Type Mod(string name) => mod.GetType(name, true)!;
        FieldInfo eventIO = Mod("BeaverBuddies.IO.EventIO").GetField("instance", All)!;

        // ---- D-S10: what the mod costs in every game, co-op or not ----

        test("D-S10: outside a multiplayer game a random draw's check answers before any singleton lookup", () =>
        {
            var code = IlScan.Instructions(Only(Mod("BeaverBuddies.DeterminismService"), "ShouldUseNonGameRNG"));
            int gate = code.FindIndex(i => i.Calls && i.Is("BeaverBuddies.IO.EventIO", "get_IsNull"));
            int lookup = code.FindIndex(i => i.Calls && i.Member?.Name == "GetSingleton");
            if (gate < 0) throw new Exception("ShouldUseNonGameRNG never reads EventIO.IsNull");
            if (lookup >= 0 && lookup < gate) throw new Exception("ShouldUseNonGameRNG looks the singleton up before it knows it is in a multiplayer game");
            if (code.Take(gate).Any(i => i.Calls)) throw new Exception("ShouldUseNonGameRNG calls something before the multiplayer check");
        });

        test("D-S10: outside a multiplayer game a new entity's ID is the game's own, with no draws from the game's random state", () =>
        {
            object? prior = eventIO.GetValue(null);
            eventIO.SetValue(null, null);
            try
            {
                MethodInfo prefix = Only(Mod("BeaverBuddies.GuidPatcher"), "Prefix");
                object[] args = { Guid.Empty };
                bool runsOriginal;
                // beta24 drew 16 bytes from Unity's random state here, a native call that cannot run outside the game.
                try { runsOriginal = (bool)prefix.Invoke(null, args)!; }
                catch (TargetInvocationException e) { throw new Exception("the ID was made from the game's random state: " + e.InnerException?.GetType().Name); }
                if (!runsOriginal || (Guid)args[0] != Guid.Empty) throw new Exception("the mod made the ID itself outside a multiplayer game");
            }
            finally { eventIO.SetValue(null, prior); }
        });

        test("D-S10: outside a multiplayer game the not-gameplay and gameplay markers count nothing", () =>
        {
            Type service = Mod("BeaverBuddies.DeterminismService");
            var nonGame = (IDictionary)service.GetField("activeNonGamePatchers", All)!.GetValue(null)!;
            var game = (IDictionary)service.GetField("activeGamePatchers", All)!.GetValue(null)!;
            object? prior = eventIO.GetValue(null);
            eventIO.SetValue(null, null);
            var counted = new List<string>();
            try
            {
                foreach (string name in new[] { "InputPatcher", "SoundsPatcher", "SoundEmitterPatcher", "DateSalterPatcher",
                    "BeaverNameServiceRandomNamePatcher", "PlantableDescriberPatcher", "StockpileGoodPileVisualizerPatcher",
                    "LoopingSoundPlayerPatcher", "BotManufactoryAnimationControllerPatcher", "TerrainBlockRandomizerPickVariationPatcher" })
                {
                    nonGame.Clear(); game.Clear();
                    Type type = Mod("BeaverBuddies." + name);
                    object[] state = { true };
                    Only(type, "Prefix").Invoke(null, state);
                    if (nonGame.Count + game.Count != 0 || (bool)state[0]) counted.Add(name);
                    Only(type, "Finalizer").Invoke(null, new object[] { (bool)state[0] });
                    if (nonGame.Count + game.Count != 0) counted.Add(name + " (finalizer)");
                }
            }
            finally { nonGame.Clear(); game.Clear(); eventIO.SetValue(null, prior); }
            if (counted.Count > 0) throw new Exception("counted in single player: " + string.Join(", ", counted));
        });

        test("D-S10: TickableEntity.Tick is patched only once detailed logging is on in a multiplayer game", () =>
        {
            Type tickable = Game("Timberborn.TickSystem", "Timberborn.TickSystem.TickableEntity");
            var always = mod.GetTypes().Where(t => t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"
                && a.ConstructorArguments.Any(c => c.Value as Type == tickable)
                && a.ConstructorArguments.Any(c => c.Value as string == "Tick"))).Select(t => t.Name).ToList();
            if (always.Count > 0) throw new Exception("patched in every game by " + string.Join(", ", always));
            Type patcher = Mod("BeaverBuddies.DeterminismService+TickableEntityTickPatcher");
            var ensure = IlScan.Instructions(Only(patcher, "EnsurePatched"));
            if (!ensure.Any(i => i.Calls && i.Member?.Name == "Patch")) throw new Exception("EnsurePatched no longer patches TickableEntity.Tick");
            var tick = IlScan.Instructions(Only(Mod("BeaverBuddies.ReplayService"), "DoTick"));
            int call = tick.FindIndex(i => i.Calls && i.Member?.Name == "EnsurePatched");
            if (call < 0) throw new Exception("ReplayService.DoTick never patches it, so detailed logging would not name the ticking entity");
            int debug = tick.FindLastIndex(call, i => i.Calls && i.Is("BeaverBuddies.Settings", "get_Debug"));
            if (debug < 0 || !tick.Skip(debug + 1).Take(call - debug).Any(i => i.Op.FlowControl == FlowControl.Cond_Branch))
                throw new Exception("DoTick patches it without detailed logging on");
        });

        test("D-S10: the dead code is gone: TickWathcerService, DeterminismPatcher, GameSaveHelper and the process-wide DateTime.ToString prefix", () =>
        {
            var left = new[] { "BeaverBuddies.TickWathcerService", "BeaverBuddies.DeterminismPatcher", "BeaverBuddies.NonGamePatcher",
                "BeaverBuddies.GameSaveHelper", "BeaverBuddies.DateTimeStringPatcher", "BeaverBuddies.DateSatlerSavePatcher" }
                .Where(name => mod.GetType(name) != null).ToList();
            var dateTime = mod.GetTypes().Where(t => t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"
                && a.ConstructorArguments.Any(c => c.Value as Type == typeof(DateTime)))).Select(t => t.Name);
            left.AddRange(dateTime);
            if (left.Count > 0) throw new Exception("still there: " + string.Join(", ", left));
        });

        // ---- D-S9: the walking animation's corner search ----

        Type followerType = Game("Timberborn.CharacterMovementSystem", "Timberborn.CharacterMovementSystem.AnimatedPathFollower");
        Type cornerType = Game("Timberborn.CharacterMovementSystem", "Timberborn.CharacterMovementSystem.AnimatedPathCorner");
        Type vectorType = Game("UnityEngine.CoreModule", "UnityEngine.Vector3");
        FieldInfo nextCorner = followerType.GetField("_nextCornerIndex", All)!;
        MethodInfo setPath = followerType.GetMethod("SetNewPath")!, update = followerType.GetMethod("Update")!;
        PropertyInfo group = followerType.GetProperty("CurrentGroupId")!, speed = followerType.GetProperty("CurrentSpeed")!;
        // A tick's path, as PathFollower.MoveAlongPath makes it: corner times that never decrease. Every corner at one
        // place (no direction to turn to, which needs Unity's native rotation), each with its own group and speed, which
        // say which segment the follower chose.
        object Path(Random random, int corners, float start)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(cornerType))!;
            object place = Activator.CreateInstance(vectorType)!;
            float time = start;
            for (int i = 0; i < corners; i++)
            {
                list.Add(Activator.CreateInstance(cornerType, place, time, 1f + i, 0f, i)!);
                time += random.NextDouble() < .1 ? 0 : (float)random.NextDouble() * .06f;
            }
            return list;
        }
        object Follower(object path)
        {
            object follower = Activator.CreateInstance(followerType)!;
            setPath.Invoke(follower, new[] { path });
            return follower;
        }
        MethodInfo resume = Only(Mod("BeaverBuddies.Fixes.AnimatedPathFollowerUpdatePathcer"), "ResumeSearch");

        test("D-S9: on the game's own AnimatedPathFollower, going on from the cached corner draws what a search from the first draws", () =>
        {
            var random = new Random(24);
            int frames = 0;
            for (int walk = 0; walk < 400; walk++)
            {
                int corners = random.Next(1, 40);
                float start = (float)random.NextDouble() * 100;
                object path = Path(random, corners, start);
                object before = Follower(path), after = Follower(path);
                float clock = start - .1f;
                for (int frame = 0; frame < 80; frame++, frames++)
                {
                    // Mostly forward by a frame's share of a tick; now and then back, as at a tick boundary.
                    clock += random.NextDouble() < .15 ? -(float)random.NextDouble() * .3f : (float)random.NextDouble() * .05f;
                    nextCorner.SetValue(before, 0);
                    update.Invoke(before, new object[] { clock });
                    resume.Invoke(null, new[] { after, clock });
                    update.Invoke(after, new object[] { clock });
                    if (!Equals(nextCorner.GetValue(before), nextCorner.GetValue(after)) || !Equals(group.GetValue(before), group.GetValue(after))
                        || !Equals(speed.GetValue(before), speed.GetValue(after)))
                        throw new Exception($"walk {walk} frame {frame} at {clock}: corner {nextCorner.GetValue(after)} instead of {nextCorner.GetValue(before)}");
                }
            }
            Console.WriteLine($"  {frames} frames of 400 random walks: the same corner, segment and speed as a search from the first corner");
        });

        test("D-S9: micro-benchmark, 600 walkers at 60 fps and speed 7 (about 5 frames a tick), a tick's path of 25 corners", () =>
        {
            var random = new Random(3);
            const int walkers = 600, ticks = 40, framesPerTick = 5, corners = 25;
            var paths = Enumerable.Range(0, walkers).Select(_ => Path(random, corners, 0)).ToList();
            var fromFirst = paths.Select(Follower).ToList();
            var resumed = paths.Select(Follower).ToList();
            // Compiled calls, so reflection does not swamp what is measured: beta24's reset, and the mod's resume.
            var f = System.Linq.Expressions.Expression.Parameter(typeof(object));
            var t = System.Linq.Expressions.Expression.Parameter(typeof(float));
            var typed = System.Linq.Expressions.Expression.Convert(f, followerType);
            var reset = System.Linq.Expressions.Expression.Lambda<Action<object, float>>(System.Linq.Expressions.Expression.Assign(
                System.Linq.Expressions.Expression.Field(typed, nextCorner), System.Linq.Expressions.Expression.Constant(0)), f, t).Compile();
            var goOn = System.Linq.Expressions.Expression.Lambda<Action<object, float>>(
                System.Linq.Expressions.Expression.Call(resume, typed, t), f, t).Compile();
            var move = System.Linq.Expressions.Expression.Lambda<Action<object, float>>(
                System.Linq.Expressions.Expression.Call(typed, update, t), f, t).Compile();
            double Run(List<object> followers, Action<object, float> before)
            {
                var watch = new System.Diagnostics.Stopwatch();
                for (int tick = 0; tick < ticks; tick++)
                {
                    // A new tick: the game gives each walker a new path (SetNewPath), whose search starts at the first corner.
                    foreach (object follower in followers) nextCorner.SetValue(follower, 0);
                    watch.Start();
                    for (int frame = 0; frame < framesPerTick; frame++)
                    {
                        float clock = (frame + 1) * (corners * .03f) / framesPerTick;
                        for (int w = 0; w < followers.Count; w++)
                        {
                            before(followers[w], clock);
                            move(followers[w], clock);
                        }
                    }
                    watch.Stop();
                }
                return watch.Elapsed.TotalMilliseconds * 1000 / (ticks * framesPerTick);
            }
            Run(fromFirst, reset); Run(resumed, goOn);
            double first = Run(fromFirst, reset), cached = Run(resumed, goOn);
            Console.WriteLine($"  per frame, 600 walkers: from the first corner (beta24) {first:0} µs, from the cached corner {cached:0} µs");
        });

        // ---- D-S4: the road networks' district conflict walk ----

        Type navigation(string name) => Game("Timberborn.Navigation", "Timberborn.Navigation." + name);
        object Update(bool roads)
        {
            object ReadOnly<T>(List<T> list) => Game("Timberborn.Common", "Timberborn.Common.ReadOnlyList`1").MakeGenericType(typeof(T))
                .GetConstructors(All).Single().Invoke(new object[] { list });
            Type updateType = navigation("NavMeshUpdate"), cell = Game("UnityEngine.CoreModule", "UnityEngine.Vector3Int");
            var terrain = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(cell))!;
            terrain.Add(Activator.CreateInstance(cell, 5, 5, 2));
            object Of(IList list) => Game("Timberborn.Common", "Timberborn.Common.ReadOnlyList`1").MakeGenericType(cell)
                .GetConstructors(All).Single().Invoke(new object[] { list });
            return updateType.GetConstructors(All).Single().Invoke(new[] { Activator.CreateInstance(Game("Timberborn.Common", "Timberborn.Common.BoundingBox"))!,
                Of(terrain), ReadOnly(new List<int> { 42 }), ReadOnly(roads ? new List<int> { 43 } : new List<int>()) });
        }

        test("D-S4: a navigation update that changed no road does not walk the road networks", () =>
        {
            Type networks = Mod("BeaverBuddies.Colonies.ColonyRoadNetworks");
            FieldInfo separate = Mod("BeaverBuddies.Colonies.ColonyModeService").GetField("separateNow", All)!;
            object spot = networks.GetField("ConflictWalks", All)?.GetValue(null)
                ?? throw new Exception("the conflict walk has no profiler spot, so nobody can see what it costs");
            FieldInfo calls = spot.GetType().GetField("Calls", All)!;
            // Its services are never read before the walk: an instance made without them stands for the game's.
            object listener = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(networks);
            MethodInfo updated = Only(networks, "OnNavMeshUpdated");
            object priorMode = separate.GetValue(null)!;
            separate.SetValue(null, true);
            try
            {
                long before = (long)calls.GetValue(spot)!;
                updated.Invoke(listener, new[] { Update(roads: false) });
                if ((long)calls.GetValue(spot)! != before) throw new Exception("a terrain-only update walked the road networks");
                updated.Invoke(listener, new[] { Update(roads: true) });
                if ((long)calls.GetValue(spot)! != before + 1) throw new Exception("a road update did not walk them");
                separate.SetValue(null, false);
                updated.Invoke(listener, new[] { Update(roads: true) });
                if ((long)calls.GetValue(spot)! != before + 1) throw new Exception("a shared-colony game walked them");
            }
            finally { separate.SetValue(null, priorMode); }
        });

        test("D-S4: micro-benchmark, the game's district conflict walk over 3,000 path tiles and buildings in 12 districts", () =>
        {
            // A 256 x 256 map, one level: 12 districts, each a road network of about 250 path tiles with a building entrance
            // every other tile (a building's own road node), none joined to another. Built straight into the game's graph.
            const int size = 256, districts = 12, tilesEach = 250;
            Type graphType = navigation("RoadNavMeshGraph"), nodeType = navigation("NavMeshNode");
            object graph = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(graphType);
            var lists = Array.CreateInstance(typeof(List<>).MakeGenericType(nodeType), size * size * 2);
            IList Neighbours(int id)
            {
                var list = (IList?)lists.GetValue(id);
                if (list == null) { list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(nodeType))!; lists.SetValue(list, id); }
                return list;
            }
            void Join(int a, int b)
            {
                Neighbours(a).Add(Activator.CreateInstance(nodeType, b, 0, 1f)!);
                Neighbours(b).Add(Activator.CreateInstance(nodeType, a, 0, 1f)!);
            }
            var centers = new List<int>();
            int nodes = 0;
            for (int d = 0; d < districts; d++)
            {
                // A snake of path tiles in its own band of rows, a building node beside every other tile.
                int row0 = d * 20 + 2, previous = -1;
                for (int i = 0; i < tilesEach; i++)
                {
                    int x = 2 + i % 200, y = row0 + (i / 200) * 2;
                    int id = y * size + x;
                    if (previous >= 0) Join(previous, id); else centers.Add(id);
                    if ((i / 200) != ((i - 1) / 200) && i > 0) Join(id - size * 2, id);
                    previous = id;
                    nodes++;
                    if (i % 2 == 0) { Join(id, size * size + id); nodes++; }
                }
            }
            for (int i = 0; i < lists.Length; i++) if (lists.GetValue(i) == null) lists.SetValue(Activator.CreateInstance(typeof(List<>).MakeGenericType(nodeType)), i);
            graphType.GetField("_neighbors", All)!.SetValue(graph, lists);
            object obstacles = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(navigation("DistrictObstacleService"));
            obstacles.GetType().GetField("_obstacles", All)!.SetValue(obstacles, new bool[lists.Length]);
            object detector = Activator.CreateInstance(navigation("DistrictConflictDetector"), true)!;
            MethodInfo walk = detector.GetType().GetMethod("AreDistrictsInConflict")!;
            object[] args = { graph, obstacles, centers };
            if ((bool)walk.Invoke(detector, args)!) throw new Exception("the test network has two districts joined");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int runs = 50;
            for (int r = 0; r < runs; r++) walk.Invoke(detector, args);
            double ms = watch.Elapsed.TotalMilliseconds / runs;
            Console.WriteLine($"  one walk over {nodes} road nodes in {districts} districts: {ms:0.00} ms (.NET 8; the game's Mono is slower). " +
                "It ran on every regular navigation update in separate colonies; now only on those that changed a road");
        });
    }
}
