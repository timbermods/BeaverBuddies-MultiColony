using System.Reflection;
using System.Runtime.CompilerServices;

// Separate colonies, against the compiled mod and the game's own assemblies: every action declares what it touches,
// the sender's number survives the trip through JSON, and every game method the colony patches replace still exists.
internal static class ColonyRuntimeChecks
{
    public static void Run(Assembly mod, Action<string, Action> test)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var replayEvent = mod.GetType("BeaverBuddies.Events.ReplayEvent", true)!;
        var getScope = replayEvent.GetMethod("GetColonyScope", all)!;
        var eventTypes = mod.GetTypes()
            .Where(t => replayEvent.IsAssignableFrom(t) && !t.IsAbstract && t != replayEvent)
            .OrderBy(t => t.Name)
            .ToList();

        test($"Colony: every one of the {eventTypes.Count} event types declares what it touches", () =>
        {
            // A new event that forgets to declare would be judged "shared" in a separate-colonies game, letting one
            // player act on the other's colony. So forgetting fails here instead.
            var missing = eventTypes
                .Where(t => t.GetMethod("GetColonyScope", all)!.DeclaringType == replayEvent)
                .Select(t => t.Name).ToList();
            if (missing.Count > 0) throw new Exception("No colony scope: " + string.Join(", ", missing));
        });

        test("Colony: every event type's scope can be read from an empty event", () =>
        {
            var failed = new List<string>();
            foreach (Type type in eventTypes)
            {
                object instance = RuntimeHelpers.GetUninitializedObject(type);
                try
                {
                    if (getScope.Invoke(instance, null) == null) failed.Add(type.Name + " (null)");
                }
                catch (TargetInvocationException e) { failed.Add(type.Name + " (" + e.InnerException?.GetType().Name + ")"); }
            }
            if (failed.Count > 0) throw new Exception("Scope could not be read: " + string.Join(", ", failed));
        });

        test("Colony: the shared (always allowed) event types are listed for review", () =>
        {
            var global = mod.GetType("BeaverBuddies.Colonies.ColonyScope", true)!.GetField("Global", all)!.GetValue(null);
            var shared = eventTypes
                .Where(t => ReferenceEquals(getScope.Invoke(RuntimeHelpers.GetUninitializedObject(t), null), global))
                .Select(t => t.Name).ToList();
            // Printed so a reviewer sees what either player may do regardless of colony.
            Console.WriteLine("      Shared by both colonies: " + string.Join(", ", shared));
            var expected = new[] { "AutosaveEvent", "BuildingUnlockedEvent", "ClientDesyncedEvent", "EntityRenamedEvent",
                "GroupedEvent", "HeartbeatEvent", "InitializeClientEvent", "PingEvent", "ShowOptionsMenuEvent", "SpeedSetEvent",
                "TraceLoggedForTickEvent", "WorkerTypeUnlockedEvent", "WorkingHoursChangedEvent" };
            if (!shared.SequenceEqual(expected))
                throw new Exception("The shared list changed; review it and update this check: " + string.Join(", ", shared));
        });

        test("Colony: the sender's number survives the trip through the event JSON", () =>
        {
            var json = mod.GetType("BeaverBuddies.IO.JsonSettings", true)!;
            var speed = mod.GetType("BeaverBuddies.Events.SpeedSetEvent", true)!;
            object e = Activator.CreateInstance(speed, true)!;
            replayEvent.GetField("player")!.SetValue(e, 2);
            string text = (string)json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent).Invoke(null, new[] { e })!;
            if (!text.Contains("\"player\": 2")) throw new Exception("player missing from JSON: " + text);
            object back = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent).Invoke(null, new object[] { text })!;
            if ((int)replayEvent.GetField("player")!.GetValue(back)! != 2) throw new Exception("player lost on the way back");
            // An event from an older build has no player: it reads as the host's.
            object old = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent).Invoke(null,
                new object[] { text.Replace("\"player\": 2,", "").Replace(",\r\n  \"player\": 2", "").Replace(",\n  \"player\": 2", "") })!;
            if ((int)replayEvent.GetField("player")!.GetValue(old)! != 0) throw new Exception("an event without player did not read as 0");
        });

        test("Colony: a real serialized group of actions is stamped all the way down", () =>
        {
            // Guards the shape the mod really sends: with type names on, the list inside a group is written as
            // {"$type": ..., "$values": [...]}. The host's stamp must reach every action inside.
            var json = mod.GetType("BeaverBuddies.IO.JsonSettings", true)!;
            var grouped = mod.GetType("BeaverBuddies.GroupedEvent", true)!;
            var speed = mod.GetType("BeaverBuddies.Events.SpeedSetEvent", true)!;
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(replayEvent))!;
            list.Add(Activator.CreateInstance(speed, true)!);
            list.Add(Activator.CreateInstance(speed, true)!);
            object group = Activator.CreateInstance(grouped, all, null, new object[] { list }, null)!;
            string text = (string)json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent).Invoke(null, new[] { group })!;

            var timberNet = Assembly.Load("TimberNet");
            var jObject = Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.Linq.JObject", true)!;
            object parsed = jObject.GetMethod("Parse", new[] { typeof(string) })!.Invoke(null, new object[] { text })!;
            timberNet.GetType("TimberNet.TimberNetBase", true)!.GetMethod("StampPlayer")!.Invoke(null, new object[] { parsed, 3 });
            string stamped = parsed.ToString()!;

            object back = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent).Invoke(null, new object[] { stamped })!;
            var children = (System.Collections.IEnumerable)grouped.GetField("events")!.GetValue(back)!;
            int count = 0;
            foreach (object child in children)
            {
                count++;
                if ((int)replayEvent.GetField("player")!.GetValue(child)! != 3) throw new Exception("an action inside the group was not stamped");
            }
            if (count != 2) throw new Exception($"expected 2 actions, got {count}");
        });

        foreach (var (typeName, assemblyName, method) in new[]
        {
            ("Timberborn.GameDistrictsMigration.MigrationNeighbours", "Timberborn.GameDistrictsMigration", "GetHighestSpareNeighbour"),
            ("Timberborn.GameDistrictsMigration.MigrationNeighbours", "Timberborn.GameDistrictsMigration", "GetLowestSpareNeighbour"),
            ("Timberborn.DistributionSystem.GoodDistributionSetting", "Timberborn.DistributionSystem", "SetDefault"),
            ("Timberborn.GameStartup.StartingBuildingInitializer", "Timberborn.GameStartup", "Initialize"),
        })
        {
            test($"Colony: the game still has {typeName.Split('.').Last()}.{method}", () =>
            {
                Type type = Assembly.Load(assemblyName).GetType(typeName, true)!;
                if (type.GetMethod(method, all) == null) throw new Exception("missing; the colony patch would not apply");
            });
        }

        test("Colony: the game still marks crossing halves and backs them onto each other", () =>
        {
            var distribution = Assembly.Load("Timberborn.DistributionSystem");
            distribution.GetType("Timberborn.DistributionSystem.DistrictCrossingSpec", true);
            distribution.GetType("Timberborn.DistributionSystem.DistrictCrossing", true);
            var blockObject = Assembly.Load("Timberborn.BlockSystem").GetType("Timberborn.BlockSystem.BlockObject", true)!;
            if (blockObject.GetMethod("CoordinatesBehind", all) == null) throw new Exception("BlockObject.CoordinatesBehind is gone");
            Assembly.Load("Timberborn.BlockSystem").GetType("Timberborn.BlockSystem.IBlockObjectValidator", true);
        });
    }
}
