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
            if (!IlScan.Of(patcher).Values.SelectMany(m => m).Any(m => m.Name == "Patch")) throw new Exception("EnsurePatched no longer patches TickableEntity.Tick");
            // Patched in a session on this computer alone: Harmony's patcher asks for GUIDs, which a session draws from the
            // game's random state, so the patch runs with real GUIDs and the random state is put back.
            if (!ensure.Any(i => i.Calls && i.Is("BeaverBuddies.GuidPatcher", "WithRealGuids"))) throw new Exception("the patch can draw GUIDs from the game's random state");
            if (!ensure.Any(i => i.Calls && i.Member?.Name == "get_state") || !ensure.Any(i => i.Calls && i.Member?.Name == "set_state"))
                throw new Exception("the patch does not put the game's random state back");
            var tick = IlScan.Instructions(Only(Mod("BeaverBuddies.ReplayService"), "DoTick"));
            int call = tick.FindIndex(i => i.Calls && i.Member?.Name == "EnsurePatched");
            if (call < 0) throw new Exception("ReplayService.DoTick never patches it, so detailed logging would not name the ticking entity");
            int debug = tick.FindLastIndex(call, i => i.Calls && i.Is("BeaverBuddies.Settings", "get_Debug"));
            if (debug < 0 || !tick.Skip(debug + 1).Take(call - debug).Any(i => i.Op.FlowControl == FlowControl.Cond_Branch))
                throw new Exception("DoTick patches it without detailed logging on");
        });

        test("D-S10: in a session, a GUID asked for inside WithRealGuids (the on-demand patch) is a real one", () =>
        {
            Type guids = Mod("BeaverBuddies.GuidPatcher");
            MethodInfo prefix = Only(guids, "Prefix");
            using var session = ScopeChecks.Multiplayer(mod);
            bool? inside = null;
            Only(guids, "WithRealGuids").Invoke(null, new object[] { (Action)(() => inside = (bool)prefix.Invoke(null, new object[] { Guid.Empty })!) });
            if (inside != true) throw new Exception("the patcher's GUIDs came from the game's random state");
            // Outside it, a session's GUID is drawn from Unity's random state (native here, so it throws).
            try { prefix.Invoke(null, new object[] { Guid.Empty }); throw new Exception("outside WithRealGuids a session's GUID was a real one"); }
            catch (TargetInvocationException) { }
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
        // Looked up inside each check, so a build without it fails the check instead of the run.
        MethodInfo Resume() => Only(Mod("BeaverBuddies.Fixes.AnimatedPathFollowerUpdatePathcer"), "ResumeSearch");

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
                    Resume().Invoke(null, new[] { after, clock });
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
                System.Linq.Expressions.Expression.Call(Resume(), typed, t), f, t).Compile();
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

        // ---- D-S7: long sessions ----

        FieldInfo pluginLogger = Mod("BeaverBuddies.Plugin").GetField("logger", All)!;
        void Quietly(Action run)
        {
            object? previous = pluginLogger.GetValue(null);
            pluginLogger.SetValue(null, DispatchProxy.Create(Mod("BeaverBuddies.Util.Logging.ILogger"), typeof(QuietLoggerProxy)));
            try { run(); } finally { pluginLogger.SetValue(null, previous); }
        }

        test("D-S7: detailed-logging traces stay bounded: a guest whose host logs nothing, logging switched on mid-session, and none outside a session", () => Quietly(() =>
        {
            Type service = Mod("BeaverBuddies.DesyncDetecter.DesyncDetecterService");
            PropertyInfo debug = Mod("BeaverBuddies.Settings").GetProperty("TemporarilyDebug", All)!;
            var traces = (IList)service.GetField("traces", All)!.GetValue(null)!;
            MethodInfo startTick = Only(service, "StartTick"), trace = Only(service, "Trace");
            object instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(service);
            void Reset() => Only(service, "Reset").Invoke(instance, null);
            void Log(string text) => trace.Invoke(null, new object[] { text, true, true });
            debug.SetValue(null, true);
            try
            {
                using (ScopeChecks.Multiplayer(mod))
                {
                    // A guest's ticks, with nothing from the host to check them against.
                    Reset();
                    for (int tick = 0; tick < 1000; tick++) { startTick.Invoke(null, new object[] { tick }); Log("a draw"); }
                    if (traces.Count > 200) throw new Exception($"a guest kept {traces.Count} ticks of traces with nothing to check them against");
                    // Logging switched on at tick 20,000 of a session that had it off (the ticks were never counted).
                    Reset();
                    startTick.Invoke(null, new object[] { 20000 });
                    if (traces.Count > 200) throw new Exception($"switching logging on at tick 20,000 made {traces.Count} ticks of traces at once");
                }
                // Single player, or a session ended by a desync: nobody will check them.
                Reset();
                int before = traces.Count > 0 ? ((IList)traces[traces.Count - 1]!).Count : 0;
                for (int i = 0; i < 100; i++) Log("a draw");
                int after = traces.Count > 0 ? ((IList)traces[traces.Count - 1]!).Count : 0;
                if (after != before) throw new Exception($"{after - before} traces kept outside a session");
            }
            finally { debug.SetValue(null, false); Reset(); }
        }));

        test("D-S7: the Trading Post template cache and the Steam callbacks do not keep a left game or menu alive", () =>
        {
            FieldInfo cache = Mod("BeaverBuddies.Colonies.TradingPosts").GetField("isTradingPostTemplate", All)!;
            if (!cache.FieldType.Name.StartsWith("ConditionalWeakTable")) throw new Exception($"the template cache holds its specs strongly ({cache.FieldType.Name}): each game loaded adds a set");
            // Only the Steam build has the callbacks (IS_STEAM; the other build keeps an empty service).
            Type? steam = mod.GetType("BeaverBuddies.Steam.SteamOverlayConnectionService", false);
            if (steam?.GetField("callbacks", All) == null) return;
            var update = IlScan.Instructions(Only(steam, "UpdateSingleton"));
            if (!update.Any(i => i.Calls && i.Member?.Name == "Clear" && i.Member.DeclaringType?.Name.StartsWith("List") == true))
                throw new Exception("the disposed Steam callbacks stay in the static list, each holding its scene");
        });

        // ---- D-S2 and §5.3: what the report and the daily line count ----

        test("D-S2: frames cut short by a creation or deletion, and the game time the one-tick cap throws away, are counted", () =>
        {
            Type tickingType = Mod("BeaverBuddies.TickingService");
            Type tickerType = Game("Timberborn.TickSystem", "Timberborn.TickSystem.Ticker");
            Type bucketsType = Game("Timberborn.TickSystem", "Timberborn.TickSystem.TickableBucketService");
            object ticker = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(tickerType);
            float perBucket = .6f / 129;
            tickerType.GetField("_secondsPerBucket", All)!.SetValue(ticker, perBucket);
            tickerType.GetField("_accumulatedDeltaTime", All)!.SetValue(ticker, 64 * perBucket);
            // Made without its constructor, which would register it as the game's singleton for every later check.
            object ticking = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(tickingType);
            tickingType.GetField("ticker", All)!.SetValue(ticking, ticker);
            object buckets = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(bucketsType);
            long Count(string name) => (long)(tickingType.GetProperty(name)?.GetValue(ticking)
                ?? throw new Exception($"TickingService.{name} is not counted"));
            PropertyInfo interrupt = tickingType.GetProperty("ShouldInterruptTicking")!;
            // Two deletions in one frame ask once; the frame's end clears it, and the next frame's asks again.
            interrupt.SetValue(ticking, true); interrupt.SetValue(ticking, true); interrupt.SetValue(ticking, false); interrupt.SetValue(ticking, true);
            if (Count("InterruptRequests") != 2) throw new Exception($"{Count("InterruptRequests")} interrupt requests counted, not 2");
            // 100 buckets left unticked, with 64 already waiting: 129 fit, 35 are thrown away.
            tickingType.GetMethod("GiveBackBuckets", All)!.Invoke(ticking, new[] { buckets, (object)100 });
            if (Count("FramesCutShort") != 1 || Count("BucketsGivenBack") != 100 || Count("BucketsLost") != 35)
                throw new Exception($"cut short {Count("FramesCutShort")}, given back {Count("BucketsGivenBack")}, lost {Count("BucketsLost")}");
            float waiting = (float)tickerType.GetField("_accumulatedDeltaTime", All)!.GetValue(ticker)!;
            if (Math.Abs(waiting - 129 * perBucket) > 1e-6) throw new Exception("the given-back time is no longer capped at one tick");
        });

        test("§5.3: the report and the daily line carry the interrupts, the heap and the mod's growing collections", () =>
        {
            Type diagnostics = Mod("BeaverBuddies.Colonies.ColonyDiagnostics");
            string memory = (string)Only(diagnostics, "MemoryLine").Invoke(null, null)!;
            foreach (string part in new[] { "heap", "gen2", "traces", "water snapshots", "walker records", "colony changes", "marked tiles" })
                if (!memory.Contains(part)) throw new Exception($"the memory line has no {part}: {memory}");
            var tick = IlScan.Instructions(Only(diagnostics, "Tick"));
            int gate = tick.FindIndex(i => i.Calls && i.Is("BeaverBuddies.IO.EventIO", "get_IsNull"));
            int daily = tick.FindIndex(i => i.Calls && i.Member?.Name == "DailyPerformanceLine");
            if (daily < 0 || gate < 0 || gate > daily) throw new Exception("the daily performance line is not written, or not only in a co-op game");
            var report = IlScan.Instructions(Only(diagnostics, "Performance"));
            if (!report.Any(i => i.Calls && i.Member?.Name == "InterruptLine") || !report.Any(i => i.Calls && i.Member?.Name == "MemoryLine"))
                throw new Exception("the report leaves out the interrupts or the memory");
        });

        test("§5.3: every per-tick and per-frame hot path of the mod has a profiler spot, the busiest sampled", () =>
        {
            Type profiler = Mod("BeaverBuddies.Colonies.ColonyProfiler");
            var spots = (IList)profiler.GetField("spots", All)!.GetValue(null)!;
            // Declared by static fields: make sure each declaring class has run its static constructor.
            foreach (string type in new[] { "BeaverBuddies.TEBPatcher", "BeaverBuddies.TickingService", "BeaverBuddies.ReplayService",
                "BeaverBuddies.Fixes.AnimatedPathFollowerUpdatePathcer", "BeaverBuddies.Colonies.ColonyViewService",
                "BeaverBuddies.Colonies.ColonyHoursChecks", "BeaverBuddies.Colonies.ColonyDiagnostics", "BeaverBuddies.Colonies.ColonyRoadNetworks" })
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(Mod(type).TypeHandle);
            var names = spots.Cast<object>().ToDictionary(s => (string)s.GetType().GetField("Name")!.GetValue(s)!,
                s => (bool)s.GetType().GetField("Sampled", All)!.GetValue(s)!);
            var expected = new (string name, bool sampled)[]
            {
                ("Bucket hashes and walker positions before each bucket (co-op)", false),
                ("Ticking, the game's and the mod's (co-op, per frame)", false),
                ("Tick start: actions replayed and sent (co-op)", false),
                ("Walking animation between ticks (co-op, per walker per frame)", true),
                ("Alerts shown only for this colony (per alert per frame)", true),
                ("Working hours checks", true),
                ("Daily colony check (every entity)", false),
                ("Road networks: district conflict walk", false),
            };
            var wrong = expected.Where(e => !names.TryGetValue(e.name, out bool sampled) || sampled != e.sampled).Select(e => e.name).ToList();
            if (wrong.Count > 0) throw new Exception("missing or not sampled as it should be: " + string.Join("; ", wrong));
        });

        // ---- D-S11: detailed logging at late-game size ----

        test("D-S11: micro-benchmark, what detailed logging costs a tick at late-game size (traces, the host's send, the water map)", () => Quietly(() =>
        {
            Type service = Mod("BeaverBuddies.DesyncDetecter.DesyncDetecterService");
            PropertyInfo debug = Mod("BeaverBuddies.Settings").GetProperty("TemporarilyDebug", All)!;
            object instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(service);
            MethodInfo trace = Only(service, "Trace"), startTick = Only(service, "StartTick");
            debug.SetValue(null, true);
            try
            {
                using var session = ScopeChecks.Multiplayer(mod);
                Only(service, "Reset").Invoke(instance, null);
                startTick.Invoke(null, new object[] { 1 });
                // About what a colony of 600 beavers traces a tick: each beaver's running behaviour, and the game's random
                // draws (every one of them a line with a stack trace).
                const int traces = 1500;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < traces; i++)
                    trace.Invoke(null, new object[] { $"Tick RNG; s0 before: {i * 7919:X8}; Last entity: BeaverAdult - {Guid.NewGuid()}", true, false });
                double tracing = watch.Elapsed.TotalMilliseconds;
                // The host sends every trace of the tick to every guest.
                watch.Restart();
                var events = ((IEnumerable)Only(service, "CreateReplayEventsAndClear").Invoke(null, null)!).Cast<object>().ToList();
                MethodInfo serialize = Mod("BeaverBuddies.IO.JsonSettings").GetMethod("Serialize")!.MakeGenericMethod(Mod("BeaverBuddies.Events.ReplayEvent"));
                long bytes = events.Sum(e => ((string)serialize.Invoke(null, new[] { e })!).Length);
                double sending = watch.Elapsed.TotalMilliseconds;
                // The water map, hashed and copied every tick: 256 x 256 columns, 3 deep.
                Type column = Game("Timberborn.WaterSystem", "Timberborn.WaterSystem.ReadOnlyWaterColumn");
                Array columns = Array.CreateInstance(column, 256 * 256 * 3);
                byte[] counts = new byte[256 * 256];
                Type water = Mod("BeaverBuddies.DesyncDetecter.WaterDiagnostics");
                watch.Restart();
                Only(water, "Describe").Invoke(null, new object[] { columns, counts, 256 * 256 });
                Only(water, "CaptureData").Invoke(null, new object[] { columns, counts, 1, 256, 256 * 256 });
                double waterMs = watch.Elapsed.TotalMilliseconds;
                Only(water, "Reset").Invoke(null, null);
                Console.WriteLine($"  a tick of {traces} traces: {tracing:0.0} ms to trace ({tracing * 1000 / traces:0.0} µs each, a shallow stack here; " +
                    $"the game's are deeper), {sending:0.0} ms and {bytes / 1024} KB of JSON to send, {bytes * 11.7 / 1048576:0.0} MB/s at speed 7 " +
                    $"(direct IP is paced at 1 MB/s); the water map {waterMs:0.0} ms");
            }
            finally
            {
                debug.SetValue(null, false);
                Only(service, "Reset").Invoke(instance, null);
            }
        }));

        // ---- D-S8: the shipping build is not optimised ----

        test("D-S8: micro-benchmark of the mod's own per-tick and per-frame code, in this build (compare the Release and Release Steam runs)", () =>
        {
            var debuggable = mod.GetCustomAttribute<System.Diagnostics.DebuggableAttribute>();
            bool optimised = debuggable == null || !debuggable.IsJITOptimizerDisabled;
            var lines = new List<string>();
            // One call, compiled (no reflection in the loop), timed over many.
            void Time(string what, int calls, Action once)
            {
                for (int i = 0; i < calls / 10; i++) once();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < calls; i++) once();
                lines.Add($"{what} {watch.Elapsed.TotalMilliseconds * 1e6 / calls:0.0} ns");
            }
            Action Call(MethodInfo method, object? target, params object?[] args)
            {
                var call = System.Linq.Expressions.Expression.Call(target == null ? null : System.Linq.Expressions.Expression.Constant(target), method,
                    method.GetParameters().Select((p, i) => (System.Linq.Expressions.Expression)System.Linq.Expressions.Expression.Constant(args[i], p.ParameterType)));
                return System.Linq.Expressions.Expression.Lambda<Action>(call).Compile();
            }
            // Each bucket's entity-order hash (co-op, 128 buckets a tick): a bucket of 156 entities, 20,000 in all.
            Type hashesType = Mod("BeaverBuddies.DesyncDetecter.TickHashes");
            object hashes = Activator.CreateInstance(hashesType)!;
            IList<Guid> ids = Enumerable.Range(0, 156).Select(_ => Guid.NewGuid()).ToList();
            Func<Guid, Guid> same = id => id;
            Time("a bucket's hash (156 entities)", 200000, Call(hashesType.GetMethod("AddBucket")!.MakeGenericMethod(typeof(Guid)), hashes, ids, same));
            // Every game random draw in co-op: which random state it draws from.
            Time("a random draw's source", 5_000_000, Call(Only(Mod("BeaverBuddies.RandomSourceRules"), "Choose"), null, true, false, false, false, true, true, true, false));
            // A guest's catch-up speed, every frame.
            Time("the catch-up speed", 5_000_000, Call(Only(Mod("BeaverBuddies.CatchUpSpeed"), "For"), null, 7f, 5, 7f));
            // A profiler spot around every hot path.
            Type profiler = Mod("BeaverBuddies.Colonies.ColonyProfiler");
            object spot = profiler.GetMethod("Declare", new[] { typeof(string) })!.Invoke(null, new object[] { "D-S8 benchmark spot" })!;
            var start = System.Linq.Expressions.Expression.Call(Only(profiler, "Start"));
            Time("a profiler spot", 5_000_000, System.Linq.Expressions.Expression.Lambda<Action>(System.Linq.Expressions.Expression.Call(Only(profiler, "Stop"),
                System.Linq.Expressions.Expression.Constant(spot, spot.GetType()), start)).Compile());
            // The heartbeat the host sends every tick, as JSON.
            Type heartbeat = Mod("BeaverBuddies.HeartbeatEvent");
            object beat = Activator.CreateInstance(heartbeat, true)!;
            heartbeat.GetField("digest")!.SetValue(beat, (ulong?)0x1234567890abcdef);
            heartbeat.GetField("changes")!.SetValue(beat, (int?)1234);
            Type replayEvent = Mod("BeaverBuddies.Events.ReplayEvent");
            Time("the heartbeat as JSON", 50000, Call(Mod("BeaverBuddies.IO.JsonSettings").GetMethod("Serialize")!.MakeGenericMethod(replayEvent), null, beat));
            Console.WriteLine($"  {(optimised ? "optimised" : "NOT optimised (the Release Steam zip)")}: " + string.Join(", ", lines));
        });

        // ---- D-S12: the daily colony check ----

        test("D-S12: each computer walks every entity once a day for the colony check, as the host's day plays", () =>
        {
            Type diagnostics = Mod("BeaverBuddies.Colonies.ColonyDiagnostics");
            MethodInfo fingerprint = Only(diagnostics, "Fingerprint");
            bool Takes(MethodBase method) => IlScan.Instructions(method).Any(i => i.Calls && i.Member == fingerprint);
            if (Takes(Only(diagnostics, "Tick"))) throw new Exception("ColonyDiagnostics.Tick still takes its own check at the turn of the day");
            MethodInfo compare = Only(Mod("BeaverBuddies.Colonies.ColonyPresenceEvent"), "Compare");
            if (Takes(compare)) throw new Exception("the presence event takes a second check instead of the day's one");
            if (!IlScan.Instructions(compare).Any(i => i.Calls && i.Member?.Name == "DailyCheck")) throw new Exception("the presence event does not compare the day's check");
            var daily = IlScan.Instructions(Only(diagnostics, "DailyCheck"));
            if (daily.Count(i => i.Calls && i.Member == fingerprint) != 1) throw new Exception("DailyCheck does not take exactly one check");
            if (!daily.Any(i => i.Calls && i.Member?.Name == "Log")) throw new Exception("DailyCheck does not log the day's line");
        });

        test("D-S12: micro-benchmark, the daily colony check over 20,000 entities (the game's own component lookups)", () =>
        {
            object world = FakeWorld(mod, Game, 12000, 5000, 600, 40, out int entities);
            MethodInfo fingerprint = Only(Mod("BeaverBuddies.Colonies.ColonyDiagnostics"), "Fingerprint");
            // DailyCheck (1.4.0-rc1) takes one a day; beta24 took two (the turn of the day, and the presence event).
            int walks = Mod("BeaverBuddies.Colonies.ColonyDiagnostics").GetMethod("DailyCheck", All) != null ? 1 : 2;
            fingerprint.Invoke(world, null);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int runs = 20;
            for (int r = 0; r < runs; r++) fingerprint.Invoke(world, null);
            double ms = watch.Elapsed.TotalMilliseconds / runs;
            Console.WriteLine($"  one check over {entities} entities: {ms:0.00} ms, {walks} a day: {ms * walks:0.00} ms a day (.NET 8, and without the " +
                "entity-ID lookups beta24 also made per building, which need a live Unity object; the game's Mono is slower)");
        });
    }

    /// <summary>
    /// A ColonyDiagnostics over a made-up world whose entities hold the game's own component caches: natural resources,
    /// buildings (stamped, in one of 12 districts), characters, and District Crossings registered as the game registers
    /// them. Nothing in it is alive to Unity, so the checks' "is it destroyed" tests say yes and hash 0: only the walk and
    /// its component lookups are measured.
    /// </summary>
    static object FakeWorld(Assembly mod, Func<string, string, Type> game, int resources, int buildings, int characters, int crossings, out int count)
    {
        object Blank(Type type) => System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        void Set(object target, string field, object? value)
        {
            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? info = type.GetField(field, All);
                if (info != null) { info.SetValue(target, value); return; }
            }
            throw new Exception($"{target.GetType().Name}.{field} is gone");
        }
        Type entityType = game("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityComponent");
        Type cacheType = game("Timberborn.BaseComponentSystem", "Timberborn.BaseComponentSystem.ComponentCache");
        Type mapType = game("Timberborn.BaseComponentSystem", "Timberborn.BaseComponentSystem.TypeIndexMap");
        Type centerType = game("Timberborn.GameDistricts", "Timberborn.GameDistricts.DistrictCenter");
        Type buildingType = game("Timberborn.GameDistricts", "Timberborn.GameDistricts.DistrictBuilding");
        Type crossingType = game("Timberborn.DistributionSystem", "Timberborn.DistributionSystem.DistrictCrossing");
        Type stampType = mod.GetType("BeaverBuddies.Colonies.ColonyStamp", true)!;
        Type readOnly = game("Timberborn.Common", "Timberborn.Common.ReadOnlyList`1").MakeGenericType(typeof(object));
        // Every type the check asks an entity for, cached in each template's map as the game caches it on first ask.
        Type[] asked = { entityType, stampType, buildingType, crossingType, mod.GetType("BeaverBuddies.Colonies.CrossingExchange", true)!,
            game("Timberborn.DistributionSystem", "Timberborn.DistributionSystem.DistrictCrossingInventory"),
            game("Timberborn.NeedSystem", "Timberborn.NeedSystem.NeedManager") };
        var maps = new Dictionary<string, object>();
        var centers = new List<object>();
        var all = new List<object>();
        var registeredCrossings = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(
            game("Timberborn.EntitySystem", "Timberborn.EntitySystem.IRegisteredComponent")))!;
        object Entity(string template, params object[] parts)
        {
            object entity = Blank(entityType), cache = Blank(cacheType);
            var components = new List<object> { entity };
            components.AddRange(parts);
            if (!maps.TryGetValue(template, out object? map))
            {
                map = Activator.CreateInstance(mapType, true)!;
                object list = readOnly.GetConstructors(All).Single().Invoke(new object[] { components });
                foreach (Type type in asked) mapType.GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, new[] { list });
                maps[template] = map;
            }
            Set(cache, "_typeIndexMap", map);
            Set(cache, "_components", components);
            foreach (object part in components) Set(part, "_componentCache", cache);
            all.Add(entity);
            return entity;
        }
        for (int i = 0; i < 12; i++)
        {
            object center = Blank(centerType);
            Entity("center", center);
            centers.Add(center);
        }
        var random = new Random(12);
        object Building()
        {
            object building = Blank(buildingType);
            object center = centers[random.Next(centers.Count)];
            Set(building, "<District>k__BackingField", center);
            Set(building, "<InstantDistrict>k__BackingField", center);
            Set(building, "<ConstructionDistrict>k__BackingField", center);
            return building;
        }
        for (int i = 0; i < resources; i++) Entity("tree");
        for (int i = 0; i < buildings; i++) Entity("building", Blank(stampType), Building());
        for (int i = 0; i < characters; i++) Entity("beaver");
        for (int i = 0; i < crossings; i++)
        {
            object crossing = Blank(crossingType);
            Entity("crossing", Blank(stampType), Building(), crossing);
            registeredCrossings.Add(crossing);
        }
        count = all.Count;

        object registry = Activator.CreateInstance(game("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityRegistry"))!;
        var order = (IList)registry.GetType().GetField("_entitiesInInstantiationOrder", All)!.GetValue(registry)!;
        foreach (object entity in all) order.Add(entity);
        object components = Blank(game("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityComponentRegistry"));
        var byType = (IDictionary)Activator.CreateInstance(components.GetType().GetField("_registeredComponents", All)!.FieldType)!;
        byType[crossingType] = registeredCrossings;
        Set(components, "_registeredComponents", byType);
        object districts = Blank(game("Timberborn.GameDistricts", "Timberborn.GameDistricts.DistrictCenterRegistry"));
        Set(districts, "_allDistrictCenters", Activator.CreateInstance(typeof(List<>).MakeGenericType(centerType)));
        object diagnostics = Blank(mod.GetType("BeaverBuddies.Colonies.ColonyDiagnostics", true)!);
        Set(diagnostics, "_entityRegistry", registry);
        Set(diagnostics, "_entityComponentRegistry", components);
        Set(diagnostics, "_districtCenterRegistry", districts);
        return diagnostics;
    }
}
