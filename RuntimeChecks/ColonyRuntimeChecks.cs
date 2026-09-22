using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
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
            // Map areas (planting, tree cutting) are shared on purpose: resources near another colony are contested.
            // Unmarking trees (an empty tree event unmarks) only ever removes the actor's own marks, and working hours
            // are set for the actor's own colony: both are checked when played, not here. Presence and handovers are
            // refused from anyone but the host (ColonyRulesService), and so is telling a guest its action was refused.
            // Looking after a colony (grants, switching) is judged by the host against ColonyStewardRules; a wishlist is
            // only ever the actor's own colony's (the stamped slot), like working hours.
            var expected = new[] { "ActAsColonyEvent", "ActionRefusedEvent", "AutosaveEvent", "BuildingUnlockedEvent", "ClientDesyncedEvent",
                "ColonyHandoverEvent", "ColonyPresenceEvent", "GroupedEvent", "HeartbeatEvent", "InitializeClientEvent", "PingEvent",
                "PlayerHelloEvent", "ShowOptionsMenuEvent", "SpeedBoostEvent", "SpeedSetEvent", "StewardGrantedEvent", "StewardRevokedEvent",
                "TraceLoggedForTickEvent", "TreeCuttingAreaEvent", "WishlistChangedEvent", "WorkerTypeUnlockedEvent", "WorkingHoursChangedEvent" };
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
            if (!text.Contains("\"player\":2")) throw new Exception("player missing from JSON: " + text);
            // Compact: what every action costs to write, hash, compress and read again is the text's length.
            if (text.Contains("\n") || text.Contains(": ")) throw new Exception("the event JSON is not compact: " + text);
            object back = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent).Invoke(null, new object[] { text })!;
            if ((int)replayEvent.GetField("player")!.GetValue(back)! != 2) throw new Exception("player lost on the way back");
            // An event from an older build has no player: it reads as the host's.
            object old = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent).Invoke(null,
                new object[] { text.Replace("\"player\":2,", "").Replace(",\"player\":2", "") })!;
            if ((int)replayEvent.GetField("player")!.GetValue(old)! != 0) throw new Exception("an event without player did not read as 0");
        });

        test("Colony: a guest's tag and a refusal's reason survive the trip through the event JSON", () =>
        {
            // A guest recognises its own action coming back, or refused, by the tag the host keeps (Latency.PendingActions).
            var json = mod.GetType("BeaverBuddies.IO.JsonSettings", true)!;
            MethodInfo serialize = json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent);
            MethodInfo deserialize = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent);
            var placed = mod.GetType("BeaverBuddies.Events.BuildingPlacedEvent", true)!;
            object e = Activator.CreateInstance(placed, true)!;
            replayEvent.GetField("requestId")!.SetValue(e, "abcd1234:17");
            object back = deserialize.Invoke(null, new object[] { serialize.Invoke(null, new[] { e })! })!;
            if ((string)replayEvent.GetField("requestId")!.GetValue(back)! != "abcd1234:17") throw new Exception("the tag was lost");
            if (back.GetType() != placed) throw new Exception("came back as " + back.GetType().Name);

            var refusedType = mod.GetType("BeaverBuddies.Events.ActionRefusedEvent", true)!;
            var refusalType = mod.GetType("BeaverBuddies.Colonies.ColonyRefusal", true)!;
            object refused = Activator.CreateInstance(refusedType, true)!;
            refusedType.GetField("refusedRequestId")!.SetValue(refused, "abcd1234:17");
            refusedType.GetField("refusal")!.SetValue(refused, Enum.Parse(refusalType, "OtherColonyArea"));
            object again = deserialize.Invoke(null, new object[] { serialize.Invoke(null, new[] { refused })! })!;
            if (again.GetType() != refusedType) throw new Exception("the refusal came back as " + again.GetType().Name);
            if ((string)refusedType.GetField("refusedRequestId")!.GetValue(again)! != "abcd1234:17") throw new Exception("the refused tag was lost");
            if (refusedType.GetField("refusal")!.GetValue(again)!.ToString() != "OtherColonyArea") throw new Exception("the reason was lost");
        });

        test("Colony: the host's answers written into events survive the trip through the event JSON", () =>
        {
            // What the host decides while playing an action travels to the guests in the action itself: whether a
            // building could still be placed, what a founded colony starts with, the day's colony check.
            var json = mod.GetType("BeaverBuddies.IO.JsonSettings", true)!;
            MethodInfo serialize = json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent);
            MethodInfo deserialize = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent);
            object RoundTrip(object e) => deserialize.Invoke(null, new object[] { serialize.Invoke(null, new[] { e })! })!;

            var placedType = mod.GetType("BeaverBuddies.Events.BuildingPlacedEvent", true)!;
            object placed = Activator.CreateInstance(placedType, true)!;
            if (placedType.GetField("placed")!.GetValue(RoundTrip(placed)) != null) throw new Exception("an unplayed placement came back decided");
            placedType.GetField("placed")!.SetValue(placed, false);
            if (!Equals(placedType.GetField("placed")!.GetValue(RoundTrip(placed)), false)) throw new Exception("the host's 'not placed' was lost");

            var foundType = mod.GetType("BeaverBuddies.Colonies.FoundColonyEvent", true)!;
            var settingsType = mod.GetType("BeaverBuddies.Colonies.ColonyStartingSettings", true)!;
            object found = Activator.CreateInstance(foundType, true)!;
            object settings = Activator.CreateInstance(settingsType)!;
            settingsType.GetField("Adults")!.SetValue(settings, 7);
            settingsType.GetField("ChildAgeMax")!.SetValue(settings, 0.75f);
            settingsType.GetField("Food")!.SetValue(settings, 42);
            foundType.GetField("startingSettings")!.SetValue(found, settings);
            object foundBack = RoundTrip(found);
            object settingsBack = foundType.GetField("startingSettings")!.GetValue(foundBack) ?? throw new Exception("the starting settings were lost");
            if ((int)settingsType.GetField("Adults")!.GetValue(settingsBack)! != 7 || (int)settingsType.GetField("Food")!.GetValue(settingsBack)! != 42
                || (float)settingsType.GetField("ChildAgeMax")!.GetValue(settingsBack)! != 0.75f)
                throw new Exception("the starting settings changed on the way");
            if (foundType.GetField("startingSettings")!.GetValue(RoundTrip(Activator.CreateInstance(foundType, true)!)) != null)
                throw new Exception("a founding from an older host came back with settings");

            var heartbeatType = mod.GetType("BeaverBuddies.HeartbeatEvent", true)!;
            object heartbeat = Activator.CreateInstance(heartbeatType, true)!;
            heartbeatType.GetField("digest")!.SetValue(heartbeat, 0xDEADBEEFCAFEF00DUL);
            heartbeatType.GetField("changes")!.SetValue(heartbeat, 42);
            object heartbeatBack = RoundTrip(heartbeat);
            if (!Equals(heartbeatType.GetField("digest")!.GetValue(heartbeatBack), 0xDEADBEEFCAFEF00DUL) || !Equals(heartbeatType.GetField("changes")!.GetValue(heartbeatBack), 42))
                throw new Exception("the heartbeat's colony digest was lost");
            if (heartbeatType.GetField("digest")!.GetValue(RoundTrip(Activator.CreateInstance(heartbeatType, true)!)) != null)
                throw new Exception("a heartbeat from an older host came back with a digest");

            var presenceType = mod.GetType("BeaverBuddies.Colonies.ColonyPresenceEvent", true)!;
            object presence = Activator.CreateInstance(presenceType, true)!;
            presenceType.GetField("check")!.SetValue(presence, "owners=1 stamps=2");
            if ((string?)presenceType.GetField("check")!.GetValue(RoundTrip(presence)) != "owners=1 stamps=2") throw new Exception("the day's check was lost");
        });

        test("Colony: the day's players by id, the host's limit, the session's players, a steward and a reserve survive the JSON", () =>
        {
            var json = mod.GetType("BeaverBuddies.IO.JsonSettings", true)!;
            MethodInfo serialize = json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent);
            MethodInfo deserialize = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent);
            object RoundTrip(object e) => deserialize.Invoke(null, new object[] { serialize.Invoke(null, new[] { e })! })!;

            var presenceType = mod.GetType("BeaverBuddies.Colonies.ColonyPresenceEvent", true)!;
            object presence = Activator.CreateInstance(presenceType, true)!;
            presenceType.GetField("presentPlayerIds")!.SetValue(presence, new List<string> { "steam:1", "local:abc" });
            presenceType.GetField("limit")!.SetValue(presence, 7);
            object presenceBack = RoundTrip(presence);
            var ids = (List<string>?)presenceType.GetField("presentPlayerIds")!.GetValue(presenceBack);
            if (ids == null || !ids.SequenceEqual(new[] { "steam:1", "local:abc" })) throw new Exception("the players' ids were lost");
            if ((int)presenceType.GetField("limit")!.GetValue(presenceBack)! != 7) throw new Exception("the limit was lost");
            if ((int)presenceType.GetField("limit")!.GetValue(RoundTrip(Activator.CreateInstance(presenceType, true)!))! != -1)
                throw new Exception("an older host's presence did not read as an unknown limit");

            var helloType = mod.GetType("BeaverBuddies.Colonies.PlayerHelloEvent", true)!;
            object hello = Activator.CreateInstance(helloType, true)!;
            helloType.GetField("players")!.SetValue(hello, "0|steam:1|Kyler\n1|steam:2|Sarah");
            if ((string?)helloType.GetField("players")!.GetValue(RoundTrip(hello)) != "0|steam:1|Kyler\n1|steam:2|Sarah") throw new Exception("the session's players were lost");

            var grantType = mod.GetType("BeaverBuddies.Colonies.StewardGrantedEvent", true)!;
            object grant = Activator.CreateInstance(grantType, true)!;
            grantType.GetField("colonySlot")!.SetValue(grant, 2);
            grantType.GetField("stewardPlayerId")!.SetValue(grant, "steam:2");
            grantType.GetField("stewardName")!.SetValue(grant, "Sarah");
            object grantBack = RoundTrip(grant);
            if ((int)grantType.GetField("colonySlot")!.GetValue(grantBack)! != 2 || (string?)grantType.GetField("stewardPlayerId")!.GetValue(grantBack) != "steam:2"
                || (string?)grantType.GetField("stewardName")!.GetValue(grantBack) != "Sarah")
                throw new Exception("the grant changed on the way");

            var actType = mod.GetType("BeaverBuddies.Colonies.ActAsColonyEvent", true)!;
            if ((int)actType.GetField("colonySlot")!.GetValue(RoundTrip(Activator.CreateInstance(actType, true)!))! != -1)
                throw new Exception("acting as nothing did not read as the own seat");

            var proposedType = mod.GetType("BeaverBuddies.Colonies.ExchangeProposedEvent", true)!;
            object proposed = Activator.CreateInstance(proposedType, true)!;
            proposedType.GetField("keep")!.SetValue(proposed, 200);
            if ((int)proposedType.GetField("keep")!.GetValue(RoundTrip(proposed))! != 200) throw new Exception("the reserve was lost");

            var wishType = mod.GetType("BeaverBuddies.Colonies.WishlistChangedEvent", true)!;
            object wish = Activator.CreateInstance(wishType, true)!;
            wishType.GetField("items")!.SetValue(wish, new List<string> { "Gear", "Plank" });
            var items = (List<string>?)wishType.GetField("items")!.GetValue(RoundTrip(wish));
            if (items == null || !items.SequenceEqual(new[] { "Gear", "Plank" })) throw new Exception("the wishes were lost");
        });

        test("Colony: the events that leave joining open at tick 0 are listed for review", () =>
        {
            // The first action that changes the game closes joining (ReplayService): a later joiner would be sent the
            // save without it. Only events a joiner can do without may say they change nothing.
            var changes = replayEvent.GetMethod("ChangesGame", all)!;
            var neutral = eventTypes
                .Where(t => !(bool)changes.Invoke(RuntimeHelpers.GetUninitializedObject(t), null)!)
                .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            // ShowOptionsMenuEvent is a SpeedSetEvent (pausing to open the menu); SpeedBoostEvent is a speed change too (the
            // session's boost, 1.4.0-beta5). ActAsColonyEvent is session state (which colony a steward acts as), not saved,
            // and refused before the first tick anyway.
            var expected = new[] { "ActAsColonyEvent", "ActionRefusedEvent", "ClientDesyncedEvent", "HeartbeatEvent", "InitializeClientEvent", "PingEvent",
                "PlayerHelloEvent", "ShowOptionsMenuEvent", "SpeedBoostEvent", "SpeedSetEvent", "TraceLoggedForTickEvent" };
            if (!neutral.SequenceEqual(expected))
                throw new Exception("The list of events that leave joining open changed; review it and update this check: " + string.Join(", ", neutral));
        });

        test("Colony: no simulation-reachable game method reads a dev key the mod does not neutralise", () =>
        {
            // InputService.IsKeyHeld read inside code that runs on every computer during a replay or a tick (placing a
            // building, deconstructing one) reads that computer's keyboard: the two dev keys on Ctrl did (F2 of the
            // alpha10 review). Both are patched out of co-op games (Fixes/DevKeysCoopFix); any other such reader in
            // these assemblies must be reviewed and either patched or listed here.
            var readers = new List<string>();
            foreach (string assemblyName in new[] { "Timberborn.BuildingTools", "Timberborn.RecoveredGoodSystem", "Timberborn.Demolishing",
                "Timberborn.ConstructionSites", "Timberborn.BlockSystem", "Timberborn.EntitySystem", "Timberborn.PlantingUI", "Timberborn.Forestry" })
            {
                foreach (Type type in LoadableTypes(Assembly.Load(assemblyName)))
                {
                    MethodInfo[] methods;
                    try { methods = type.GetMethods(all | BindingFlags.DeclaredOnly); }
                    catch (Exception) { continue; }
                    foreach (MethodInfo method in methods)
                    {
                        List<MethodBase> calls;
                        // A method whose body references a native Unity module cannot be decoded here; it is not one of these.
                        try { calls = MethodsCalled(method); }
                        catch (Exception) { continue; }
                        if (calls.Any(m => m.DeclaringType?.Name == "InputService" && (m.Name == "IsKeyHeld" || m.Name == "IsKeyDown")))
                            readers.Add(type.Name + "." + method.Name);
                    }
                }
            }
            readers.Sort(StringComparer.Ordinal);
            // Reviewed: the two neutralised in co-op; the dev mode plant spawner, which only the planting tool calls
            // on the player's own computer (a dev tool that makes plants there alone, one of the documented dev mode
            // desyncs, not a replayed path); and the tools' own input handling, which only runs on the player's own
            // computer (it records an action; the action is what is played everywhere).
            var reviewed = new[] { "BuildingGoodsRecoveryService.OnBuildingDeconstructed", "BuildingPlacer.ShouldBePlacedFinished",
                "DevModePlantableSpawner.SpawnPlantables" };
            var unreviewed = readers.Except(reviewed).Where(r => !r.Contains("Tool") && !r.Contains("Picker") && !r.Contains("Cursor")).ToList();
            if (unreviewed.Count > 0) throw new Exception("review these key readers: " + string.Join(", ", unreviewed));
            foreach (string needed in reviewed.Take(2))
                if (!readers.Contains(needed)) throw new Exception("the game no longer reads a dev key in " + needed + "; the patch in DevKeysCoopFix may be stale");
        });

        test("Colony: a founding replay reads no difficulty specs of its own", () =>
        {
            // Founding uses the starting settings the host wrote into the event (F4): every computer's own specs
            // (a mod changing the default difficulty on one of them) must stay out of Found.
            var founding = mod.GetType("BeaverBuddies.Colonies.ColonyFoundingService", true)!;
            var calls = MethodsCalled(founding.GetMethod("Found", all)!).Select(m => m.DeclaringType!.Name + "." + m.Name).ToList();
            if (calls.Any(c => c.StartsWith("ISpecService.") || c == "ColonyFoundingService.StartingSettings"))
                throw new Exception("Found reads local specs: " + string.Join(", ", calls.Where(c => c.StartsWith("ISpecService."))));
            if (!calls.Contains("ColonyFoundingService.HostStartingSettings"))
                throw new Exception("Found no longer falls back to HostStartingSettings for an older host's event");
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
            ("Timberborn.ConstructionSites.ConstructionFactory", "Timberborn.ConstructionSites", "CreateAsFinished"),
            ("Timberborn.Beavers.BeaverFactory", "Timberborn.Beavers", "CreateAdult"),
            ("Timberborn.Beavers.BeaverFactory", "Timberborn.Beavers", "CreateChild"),
            ("Timberborn.BlockObjectTools.BlockObjectToolFactory", "Timberborn.BlockObjectTools", "Create"),
            ("Timberborn.GameStartup.StartingBuildingSpawner", "Timberborn.GameStartup", "get_StartingBuildingTemplateSpec"),
            ("Timberborn.GameStartup.StartingBuildingSpawner", "Timberborn.GameStartup", "PlaceStartingBuilding"),
            ("Timberborn.BlockSystem.BlockValidator", "Timberborn.BlockSystem", "BlocksValid"),
            // Each player sees their own colony: the display methods the colony view replaces or adjusts.
            ("Timberborn.GameDistricts.DistrictContextService", "Timberborn.GameDistricts", "SelectDistrict"),
            ("Timberborn.ResourceCountingSystemUI.ContextualResourceCountingService", "Timberborn.ResourceCountingSystemUI", "GetContextualResourceCount"),
            ("Timberborn.ResourceCountingSystem.ResourceCountingService", "Timberborn.ResourceCountingSystem", "GetDistrictResourceCounter"),
            ("Timberborn.PopulationUI.PopulationPanel", "Timberborn.PopulationUI", "GetContextualPopulationData"),
            ("Timberborn.Population.PopulationDataCollector", "Timberborn.Population", "CollectData"),
            ("Timberborn.WellbeingUI.BasicStatisticsPanel", "Timberborn.WellbeingUI", "UpdateWellbeing"),
            ("Timberborn.Wellbeing.WellbeingService", "Timberborn.Wellbeing", "GetAverageDistrictWellbeing"),
            ("Timberborn.BatchControl.BatchControlRowGroup", "Timberborn.BatchControl", "UpdateVisibleRows"),
            ("Timberborn.BatchControl.BatchControlBoxDistrictController", "Timberborn.BatchControl", "Show"),
            ("Timberborn.BatchControl.BatchControlBoxDistrictController", "Timberborn.BatchControl", "UpdateDropdown"),
            ("Timberborn.StatusSystem.StatusAggregator", "Timberborn.StatusSystem", "IsVisible"),
            ("Timberborn.StatusSystem.DynamicStatusAggregator", "Timberborn.StatusSystem", "IsVisible"),
            ("Timberborn.NotificationSystemUI.NotificationPanel", "Timberborn.NotificationSystemUI", "AddNotification"),
            // A dying beaver's colony is read before the game takes it out of its district (ColonyJournal).
            ("Timberborn.Characters.Character", "Timberborn.Characters", "KillCharacter"),
            // And the colony any beaver leaves, for one that dies in no district (ColonyJournal).
            ("Timberborn.GameDistricts.Citizen", "Timberborn.GameDistricts", "UnassignDistrict"),
            // Separate science and unlocks.
            ("Timberborn.ScienceSystem.ScienceService", "Timberborn.ScienceSystem", "get_SciencePoints"),
            ("Timberborn.ScienceSystem.ScienceService", "Timberborn.ScienceSystem", "AddPoints"),
            ("Timberborn.ScienceSystem.ScienceService", "Timberborn.ScienceSystem", "SubtractPoints"),
            ("Timberborn.ScienceSystem.BuildingUnlockingService", "Timberborn.ScienceSystem", "Unlocked"),
            ("Timberborn.ScienceSystem.BuildingUnlockingService", "Timberborn.ScienceSystem", "Unlockable"),
            ("Timberborn.ScienceSystem.BuildingUnlockingService", "Timberborn.ScienceSystem", "UnlockIgnoringCost"),
            ("Timberborn.ScienceSystem.ScienceNeedingBuilding", "Timberborn.ScienceSystem", "Tick"),
            ("Timberborn.Workshops.Manufactory", "Timberborn.Workshops", "IncreaseProductionProgress"),
            ("Timberborn.AutomationBuildings.ScienceCounter", "Timberborn.AutomationBuildings", "Sample"),
            ("Timberborn.Demolishing.Demolisher", "Timberborn.Demolishing", "Demolish"),
            ("Timberborn.WorkSystem.WorkplaceUnlockingService", "Timberborn.WorkSystem", "Unlockable"),
            ("Timberborn.ToolSystem.ToolUnlockingService", "Timberborn.ToolSystem", "LockIfNeeded"),
            ("Timberborn.ToolSystem.ToolUnlockingService", "Timberborn.ToolSystem", "IsLocked"),
            // The trading post.
            ("Timberborn.DistributionSystem.DistrictCrossingInventory", "Timberborn.DistributionSystem", "TransferStock"),
            // A round's goods wait on their half, reserved, until both sides are in; then they cross together.
            ("Timberborn.InventorySystem.Inventory", "Timberborn.InventorySystem", "ReserveStock"),
            ("Timberborn.InventorySystem.Inventory", "Timberborn.InventorySystem", "UnreserveStock"),
            ("Timberborn.InventorySystem.Inventory", "Timberborn.InventorySystem", "UnreservedAmountInStock"),
            // Traded beavers move as the game migrates them, and each arrival goes in the population log.
            ("Timberborn.GameDistricts.Citizen", "Timberborn.GameDistricts", "AssignDistrict"),
            ("Timberborn.GameDistrictsMigration.MigrationService", "Timberborn.GameDistrictsMigration", "IsNotContaminated"),
            ("Timberborn.GameDistrictsMigration.MigrationService", "Timberborn.GameDistrictsMigration", "RefusesWork"),
            ("Timberborn.GameDistrictsMigration.MigrationService", "Timberborn.GameDistrictsMigration", "IsEmployed"),
            ("Timberborn.GameDistrictsMigration.MigrationService", "Timberborn.GameDistrictsMigration", "HasHome"),
            ("Timberborn.GameDistrictsMigration.MigrationService", "Timberborn.GameDistrictsMigration", "GetDayOfBirth"),
            ("Timberborn.NotificationSystem.NotificationBus", "Timberborn.NotificationSystem", "Post"),
            ("Timberborn.DistributionSystem.DistrictCrossingWorkplaceBehavior", "Timberborn.DistributionSystem", "TryExport"),
            ("Timberborn.DistributionSystem.DistrictCrossing", "Timberborn.DistributionSystem", "CanExportGood"),
            ("Timberborn.DistributionSystem.DistrictCrossingInventory", "Timberborn.DistributionSystem", "IncomingStock"),
            ("Timberborn.DistributionSystem.DistrictCrossingInventoryInitializer", "Timberborn.DistributionSystem", "AllowEveryGoodAsTakeable"),
            ("Timberborn.InventorySystem.Inventory", "Timberborn.InventorySystem", "UnreservedCapacity"),
            ("Timberborn.CoreUI.VisualElementInitializer", "Timberborn.CoreUI", "InitializeVisualElement"),
            // The Trading Post's panel hides the crossing's distribution panels it would otherwise get.
            ("Timberborn.DistributionSystemUI.DistrictCrossingFragment", "Timberborn.DistributionSystemUI", "UpdateRootAndIcons"),
            ("Timberborn.DistributionSystemUI.DistrictCrossingInventoryFragment", "Timberborn.DistributionSystemUI", "ShowFragment"),
            // The Trading Post's toolbar button shows only in a separate-colonies game (a tool disabler).
            ("Timberborn.ToolSystem.IToolDisabler", "Timberborn.ToolSystem", "IsEnabled"),
            // Keeping colonies apart.
            ("Timberborn.YielderFinding.YielderFinder", "Timberborn.YielderFinding", "FindLivingYielderWithoutAccessible"),
            ("Timberborn.YielderFinding.YielderFinder", "Timberborn.YielderFinding", "FindYielderWithAccessible"),
            ("Timberborn.Planting.PlantingSpotFinder", "Timberborn.Planting", "CanPlantAt"),
            ("Timberborn.Planting.PlantingService", "Timberborn.Planting", "SetPlantingCoordinates"),
            ("Timberborn.Planting.PlantingService", "Timberborn.Planting", "UnsetPlantingCoordinates"),
            ("Timberborn.Forestry.TreeCuttingArea", "Timberborn.Forestry", "AddCoordinates"),
            ("Timberborn.Forestry.TreeCuttingArea", "Timberborn.Forestry", "RemoveCoordinates"),
            ("Timberborn.ConstructionSites.ConstructionJob", "Timberborn.ConstructionSites", "StartConstructionJob"),
            ("Timberborn.Demolishing.DemolishJob", "Timberborn.Demolishing", "CanStartJob"),
            ("Timberborn.RecoveredGoodSystem.RecoverGoodStackJobProvider", "Timberborn.RecoveredGoodSystem", "IsStackRecoverable"),
            ("Timberborn.WorkSystem.WorkerWorkingHours", "Timberborn.WorkSystem", "get_AreWorkingHours"),
            ("Timberborn.WorkSystem.WorkplaceWorkingHours", "Timberborn.WorkSystem", "get_AreWorkingHours"),
            ("Timberborn.AutomationBuildings.Chronometer", "Timberborn.AutomationBuildings", "Sample"),
            ("Timberborn.AutomationBuildings.Chronometer", "Timberborn.AutomationBuildings", "UpdateOutputState"),
            ("Timberborn.TimeSystemUI.ClockPanel", "Timberborn.TimeSystemUI", "UpdateMovingParts"),
            ("Timberborn.TimeSystemUI.ClockPanel", "Timberborn.TimeSystemUI", "NormalizeRotation"),
            ("Timberborn.WorkSystemUI.WorkingHoursPanel", "Timberborn.WorkSystemUI", "UpdateTitle"),
            ("Timberborn.Carrying.CarrierInventoryFinder", "Timberborn.Carrying", "TryCarryFromAnyInventoryLimited"),
            // Road networks.
            ("Timberborn.ZiplineSystem.ZiplineTower", "Timberborn.ZiplineSystem", "IsConnectedTo"),
            ("Timberborn.ZiplineSystem.ZiplineConnectionService", "Timberborn.ZiplineSystem", "CanBeConnected"),
            ("Timberborn.Navigation.DistrictConflictDetector", "Timberborn.Navigation", "AreDistrictsInConflict"),
            ("Timberborn.Navigation.DistrictService", "Timberborn.Navigation", "IsOnDistrictRoad"),
            ("Timberborn.GameDistrictsUI.DistrictPreviewsValidator", "Timberborn.GameDistrictsUI", "IsValid"),
            // Land: a building nobody stamped takes the owner of the road at its entrance.
            ("Timberborn.Buildings.BuildingAccessible", "Timberborn.Buildings", "CalculateAccess"),
            ("Timberborn.ScienceSystem.UnlockableOnceSpec", "Timberborn.ScienceSystem", "GetSpec"),
            // Dev mode's shortcuts that are played on every computer.
            ("Timberborn.BuildingTools.BuildingToolLocker", "Timberborn.BuildingTools", "UnlockIgnoringScienceCost"),
            ("Timberborn.WorkSystemUI.WorkplaceUnlockingDialogService", "Timberborn.WorkSystemUI", "UnlockIgnoringScienceCost"),
            ("Timberborn.WorkSystem.WorkplaceUnlockingService", "Timberborn.WorkSystem", "UnlockIgnoringCost"),
            ("Timberborn.ConstructionSitesUI.ConstructionSiteDebugFragment", "Timberborn.ConstructionSitesUI", "OnFinishNowClick"),
            ("Timberborn.ConstructionSites.ConstructionSite", "Timberborn.ConstructionSites", "FinishNow"),
            // A guest's pending actions, drawn until the host answers.
            ("Timberborn.Rendering.AreaTileDrawer", "Timberborn.Rendering", "UpdateArea"),
            ("Timberborn.Rendering.AreaTileDrawerFactory", "Timberborn.Rendering", "Create"),
            // Planting marks carry the tiles the marker levelled; played again, the game's levelling is replaced by them.
            ("Timberborn.TerrainQueryingSystem.TerrainAreaService", "Timberborn.TerrainQueryingSystem", "InMapLeveledCoordinates"),
            ("Timberborn.PlantingUI.PlantingSelectionService", "Timberborn.PlantingUI", "MarkArea"),
            ("Timberborn.PlantingUI.PlantingSelectionService", "Timberborn.PlantingUI", "UnmarkArea"),
            ("Timberborn.BlockSystem.BlockObjectSpec", "Timberborn.BlockSystem", "GetBlocks"),
            ("Timberborn.Debugging.DevModeManager", "Timberborn.Debugging", "get_Enabled"),
            // Tick once (the pause key while paused) bypasses TickBuckets, so it is refused in co-op.
            ("Timberborn.TickSystem.Ticker", "Timberborn.TickSystem", "TickOnce"),
            ("Timberborn.TimeSystemUI.SpeedControlPanel", "Timberborn.TimeSystemUI", "PauseOrTickOnce"),
            // Dev mode turns every tool on; the Trading Post's button still stays hidden in a shared game.
            ("Timberborn.ToolButtonSystem.ToolButton", "Timberborn.ToolButtonSystem", "get_ToolEnabled"),
            ("Timberborn.DistributionSystem.DistrictCrossingInventoryInitializer", "Timberborn.DistributionSystem", "Initialize"),
        })
        {
            test($"Colony: the game still has {typeName.Split('.').Last()}.{method}", () =>
            {
                Type type = Assembly.Load(assemblyName).GetType(typeName, true)!;
                if (!type.GetMethods(all).Any(m => m.Name == method)) throw new Exception("missing; the colony patch would not apply");
            });
        }

        foreach (var (typeName, assemblyName, field) in new[]
        {
            ("Timberborn.ScienceSystem.BuildingUnlockingService", "Timberborn.ScienceSystem", "_unlockedBuildings"),
            ("Timberborn.PlantingUI.PlantingSelectionService", "Timberborn.PlantingUI", "_terrainAreaService"),
            ("Timberborn.TerrainQueryingSystem.TerrainAreaService", "Timberborn.TerrainQueryingSystem", "_terrainService"),
            ("Timberborn.DistributionSystem.DistrictCrossingInventory", "Timberborn.DistributionSystem", "_linked"),
            ("Timberborn.DistributionSystem.DistrictCrossing", "Timberborn.DistributionSystem", "_linked"),
            ("Timberborn.DistributionSystem.DistrictCrossingWorkplaceBehavior", "Timberborn.DistributionSystem", "_districtCrossing"),
            ("Timberborn.DistributionSystem.DistrictCrossingWorkplaceBehavior", "Timberborn.DistributionSystem", "_districtCrossingInventory"),
            ("Timberborn.DistributionSystem.DistrictCrossingInventoryInitializer", "Timberborn.DistributionSystem", "DistrictCrossingCapacity"),
            ("Timberborn.DistributionSystemUI.DistrictCrossingFragment", "Timberborn.DistributionSystemUI", "_districtCrossing"),
            ("Timberborn.DistributionSystemUI.DistrictCrossingFragment", "Timberborn.DistributionSystemUI", "_root"),
            ("Timberborn.DistributionSystemUI.DistrictCrossingInventoryFragment", "Timberborn.DistributionSystemUI", "_root"),
            ("Timberborn.DistributionSystemUI.DistrictCrossingInventoryFragment", "Timberborn.DistributionSystemUI", "_districtCrossingInventory"),
            ("Timberborn.DistributionSystem.DistrictCrossingInventory", "Timberborn.DistributionSystem", "_mirrorOperationLock"),
            ("Timberborn.InventorySystem.Inventory", "Timberborn.InventorySystem", "_reservedStock"),
            ("Timberborn.ToolButtonSystem.ToolButton", "Timberborn.ToolButtonSystem", "_toolDisablers"),
            ("Timberborn.WorkSystem.WorkerWorkingHours", "Timberborn.WorkSystem", "_ignoreWorkingHours"),
            ("Timberborn.WorkSystem.WorkplaceWorkingHours", "Timberborn.WorkSystem", "_ignoreWorkingHours"),
            ("Timberborn.WorkSystem.WorkingHoursManager", "Timberborn.WorkSystem", "_startHours"),
            ("Timberborn.AutomationBuildings.Chronometer", "Timberborn.AutomationBuildings", "_sampledWorkEndHours"),
            ("Timberborn.AutomationBuildings.Chronometer", "Timberborn.AutomationBuildings", "_dayNightCycle"),
            ("Timberborn.TimeSystemUI.ClockPanel", "Timberborn.TimeSystemUI", "_workTimeEndMarker"),
            ("Timberborn.WorkSystemUI.WorkingHoursPanel", "Timberborn.WorkSystemUI", "_hours"),
            ("Timberborn.WorkSystemUI.WorkingHoursPanel", "Timberborn.WorkSystemUI", "_increaseHoursButton"),
            ("Timberborn.WorkSystemUI.WorkingHoursPanel", "Timberborn.WorkSystemUI", "_decreaseHoursButton"),
            ("Timberborn.Navigation.DistrictService", "Timberborn.Navigation", "_districtMap"),
            ("Timberborn.Navigation.DistrictService", "Timberborn.Navigation", "_districtConflictDetector"),
            // Frame-to-tick fixes (Fixes/FrameToTickFixes): the gate detector swapped for one on the real graph, the
            // crossing provider's two snapshot caches.
            ("Timberborn.AutomationBuildings.GateUpdater", "Timberborn.AutomationBuildings", "_gateConflictDetector"),
            ("Timberborn.DistributionSystem.DistrictDistributableGoodProvider", "Timberborn.DistributionSystem", "_importCache"),
            ("Timberborn.DistributionSystem.DistrictDistributableGoodProvider", "Timberborn.DistributionSystem", "_exportCache"),
            ("Timberborn.Population.PopulationService", "Timberborn.Population", "_populationDataCollector"),
            ("Timberborn.ConstructionSitesUI.ConstructionSiteDebugFragment", "Timberborn.ConstructionSitesUI", "_constructionSite"),
            // The journal is listed again, through the colony filter, once this player is seated (ColonyJournal).
            ("Timberborn.NotificationSystemUI.NotificationPanel", "Timberborn.NotificationSystemUI", "_notifications"),
            ("Timberborn.NotificationSystemUI.NotificationPanel", "Timberborn.NotificationSystemUI", "_notificationView"),
            ("Timberborn.NotificationSystemUI.NotificationPanel", "Timberborn.NotificationSystemUI", "_latestNotification"),
            ("Timberborn.NotificationSystemUI.NotificationPanel", "Timberborn.NotificationSystemUI", "_latestNotificationElement"),
        })
        {
            test($"Colony: the game still has the field {typeName.Split('.').Last()}.{field}", () =>
            {
                Type type = Assembly.Load(assemblyName).GetType(typeName, true)!;
                if (type.GetField(field, all) == null) throw new Exception("missing; the colony code reading it would fail");
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

        // A Trading Post half buffers 100 of each good; a District Crossing keeps the game's 30. The mod passes the one
        // place the game reads its fixed 30 through CapacityFor; if the game ever reads the number elsewhere, or not at
        // all, or makes the inventory where the half's blueprint is not yet known, these fail instead of a Trading Post
        // silently keeping 30 (or every crossing getting 100).
        var initializer = Assembly.Load("Timberborn.DistributionSystem")
            .GetType("Timberborn.DistributionSystem.DistrictCrossingInventoryInitializer", true)!;
        var capacityField = initializer.GetField("DistrictCrossingCapacity", all)!;

        test("Colony: the game's crossing buffer is 30, read once where every good is allowed in", () =>
        {
            if ((int)capacityField.GetValue(null)! != 30) throw new Exception("the game's number changed: " + capacityField.GetValue(null));
            var readers = initializer.GetMethods(all).Where(m => StaticFieldsRead(m).Contains(capacityField)).Select(m => m.Name).ToList();
            if (!readers.SequenceEqual(new[] { "AllowEveryGoodAsTakeable" }))
                throw new Exception("read by: " + string.Join(", ", readers));
        });

        var capacityPatcher = mod.GetType("BeaverBuddies.Colonies.TradingPostCapacityPatcher", true)!;
        var capacityFor = capacityPatcher.GetMethod("CapacityFor", all)!;
        test("Colony: the buffer patch keeps the game's read and passes it through CapacityFor", () =>
        {
            var codeType = Assembly.Load("0Harmony").GetType("HarmonyLib.CodeInstruction", true)!;
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(codeType))!;
            list.Add(Activator.CreateInstance(codeType, OpCodes.Ldsfld, capacityField)!);
            list.Add(Activator.CreateInstance(codeType, OpCodes.Ret, null)!);
            var transpiler = mod.GetType("BeaverBuddies.Colonies.TradingPostCapacityTranspiler", true)!.GetMethod("Transpiler", all)!;
            var result = ((IEnumerable)transpiler.Invoke(null, new object[] { list })!).Cast<object>().ToList();
            if (result.Count != 3) throw new Exception($"expected 3 instructions, got {result.Count}");
            OpCode Op(int i) => (OpCode)codeType.GetField("opcode")!.GetValue(result[i])!;
            object? Operand(int i) => codeType.GetField("operand")!.GetValue(result[i]);
            if (Op(0) != OpCodes.Ldsfld || !Equals(Operand(0), capacityField)) throw new Exception($"the game's read became {Op(0)} {Operand(0)}");
            if (Op(1) != OpCodes.Call || !Equals(Operand(1), capacityFor)) throw new Exception($"then {Op(1)} {Operand(1)}");
            if (Op(2) != OpCodes.Ret) throw new Exception("the rest changed");
        });
        test("Colony: a District Crossing keeps the game's 30; only a Trading Post half gets 100", () =>
        {
            var flag = capacityPatcher.GetField("tradingPost", all)!;
            try
            {
                flag.SetValue(null, false);
                if ((int)capacityFor.Invoke(null, new object[] { 30 })! != 30) throw new Exception("a District Crossing did not keep 30");
                flag.SetValue(null, true);
                if ((int)capacityFor.Invoke(null, new object[] { 30 })! != 100) throw new Exception("a Trading Post half did not get 100");
            }
            finally
            {
                flag.SetValue(null, false);
            }
        });
        test("Colony: the game makes a half's inventory in Initialize(subject, decorator), which reads the 30", () =>
        {
            var initialize = initializer.GetMethod("Initialize", all) ?? throw new Exception("Initialize is gone");
            var parameters = initialize.GetParameters();
            // The patch's Prefix takes the half by this name.
            if (parameters.Length != 2 || parameters[0].Name != "subject" || parameters[0].ParameterType.Name != "DistrictCrossingInventory")
                throw new Exception("its parameters are now: " + string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}")));
            if (!MethodsCalled(initialize).Any(m => m.Name == "AllowEveryGoodAsTakeable"))
                throw new Exception("it no longer calls AllowEveryGoodAsTakeable");
        });
        test("Colony: a Trading Post half is known by its blueprint's spec before its inventory is made", () =>
        {
            // TemplateInstantiator.Instantiate: every component (specs included) is made, and the entity's component cache
            // filled, before any decorator's initializer runs.
            var instantiator = Assembly.Load("Timberborn.TemplateInstantiation").GetType("Timberborn.TemplateInstantiation.TemplateInstantiator", true)!;
            var calls = MethodsCalled(instantiator.GetMethod("Instantiate", all)!).Select(m => m.Name).ToList();
            int made = calls.IndexOf("InstantiateInactive"), initialized = calls.IndexOf("Invoke");
            if (made < 0 || initialized < 0 || made > initialized) throw new Exception("it now calls: " + string.Join(", ", calls));
        });

        // Each player's journal judges a death by the colony the beaver died in, read in a prefix on KillCharacter
        // (ColonyJournal). These fail if the game posts the death before it kills the beaver, or no longer takes the
        // beaver out of its district on its Died event (then the prefix would not be needed, or not be enough).
        test("Colony: the game kills a beaver before it posts the death, and its Died event takes it out of its district", () =>
        {
            var mortal = Assembly.Load("Timberborn.MortalSystem").GetType("Timberborn.MortalSystem.Mortal", true)!;
            var calls = MethodsCalled(mortal.GetMethod("DieIfItIsTime", all)!).Select(m => m.DeclaringType!.Name + "." + m.Name).ToList();
            int kill = calls.IndexOf("Character.KillCharacter"), post = calls.IndexOf("NotificationBus.Post");
            if (kill < 0 || post < 0 || kill > post) throw new Exception("DieIfItIsTime now calls: " + string.Join(", ", calls));
            var citizen = Assembly.Load("Timberborn.GameDistricts").GetType("Timberborn.GameDistricts.Citizen", true)!;
            if (!MethodsCalled(citizen.GetMethod("OnDied", all)!).Any(m => m.Name == "RemoveFromDistrictsAssignment"))
                throw new Exception("Citizen.OnDied no longer takes the beaver out of its district");
            if (!MethodsCalled(citizen.GetMethod("Awake", all)!).Any(m => m.Name == "add_Died"))
                throw new Exception("Citizen no longer listens to Character.Died");
        });

        // A beaver can die in no district (cut off from it, or its district center deleted); the journal then goes by
        // the colony it last left, read in a prefix on Citizen.UnassignDistrict. This fails if a way out of a district
        // no longer goes through it.
        test("Colony: every way a beaver leaves its district goes through Citizen.UnassignDistrict", () =>
        {
            var citizen = Assembly.Load("Timberborn.GameDistricts").GetType("Timberborn.GameDistricts.Citizen", true)!;
            foreach (string way in new[] { "RemoveFromDistrictsAssignment", "UnassignDistrictIfCutOff", "AssignDistrict" })
                if (!MethodsCalled(citizen.GetMethod(way, all)!).Any(m => m.Name == "UnassignDistrict"))
                    throw new Exception($"Citizen.{way} no longer calls UnassignDistrict");
            var setters = citizen.GetMethods(all | BindingFlags.DeclaredOnly).Where(m => m.Name != "UnassignDistrict" && m.Name != "AssignDistrict")
                .Where(m => MethodsCalled(m).Any(c => c.Name == "set_AssignedDistrict")).Select(m => m.Name).ToList();
            if (setters.Count > 0) throw new Exception("AssignedDistrict is now also set in " + string.Join(", ", setters));
        });

        // The journal is display only: its prefixes let the game's methods run, it asks the game and this mod only
        // questions (and the save and the panel it lists), the rule StabilityTests checks is the one it uses, and the
        // recording and the listing again are still wired up.
        test("Colony: the journal only reads the game, and decides by the rule StabilityTests checks", () =>
        {
            var journal = mod.GetType("BeaverBuddies.Colonies.ColonyJournal", true)!;
            var death = mod.GetType("BeaverBuddies.Colonies.ColonyJournalDeathPatcher", true)!;
            var leave = mod.GetType("BeaverBuddies.Colonies.ColonyJournalLeavePatcher", true)!;
            foreach (Type patcher in new[] { death, leave })
                if (patcher.GetMethod("Prefix", all)!.ReturnType != typeof(void))
                    throw new Exception($"{patcher.Name}'s prefix can skip the game's method");
            const BindingFlags declared = all | BindingFlags.DeclaredOnly;
            var ours = new[] { journal, death, leave };
            var called = ours
                .SelectMany(t => new[] { t }.Concat(t.GetNestedTypes(all)))
                .SelectMany(t => t.GetMethods(declared).Cast<MethodBase>().Concat(t.GetConstructors(declared)))
                .SelectMany(MethodsCalled)
                .ToList();
            // Of this mod it may only ask too: nothing that changes a colony (an owner, the digest, a stamp).
            var modAllowed = new HashSet<string> { "DistrictOwner.OwnerOf", "Plugin.LogWarning", "SingletonManager.GetSingleton", "RegisteredSingleton..ctor" };
            var modChanges = called
                .Where(m => m.DeclaringType!.Namespace?.StartsWith("BeaverBuddies") == true && m.DeclaringType.Name != "JournalFilter")
                .Where(m => !ours.Contains(m.DeclaringType) && !ours.Contains(m.DeclaringType!.DeclaringType))
                .Where(m => !m.Name.StartsWith("get_"))
                .Select(m => m.DeclaringType!.Name + "." + m.Name)
                .Where(n => !modAllowed.Contains(n))
                .Distinct().OrderBy(n => n).ToList();
            if (modChanges.Count > 0) throw new Exception("calls into this mod " + string.Join(", ", modChanges));
            foreach (var (type, method, target, name) in new[]
            {
                (death, "Prefix", "ColonyJournal", "RecordDeath"),
                (leave, "Prefix", "ColonyJournal", "RecordLeaving"),
                (journal, "RecordDeath", "Dictionary`2", "set_Item"),
                (journal, "RecordLeaving", "Dictionary`2", "set_Item"),
                (journal, "Load", "NotificationBus", "add_NotificationPosted"),
                (journal, "UpdateSingleton", "ColonyJournal", "ListAgain"),
                (journal, "ListAgain", "NotificationPanel", "AddNotification"),
            })
            {
                if (!MethodsCalled(type.GetMethod(method, all)!).Any(m => m.DeclaringType!.Name == target && m.Name == name))
                    throw new Exception($"{type.Name}.{method} no longer calls {target}.{name}");
            }
            var calls = called
                .Where(m => m.DeclaringType!.Namespace?.StartsWith("Timberborn") == true || m.DeclaringType.Namespace == "UnityEngine")
                .Select(m => m.DeclaringType!.Name + "." + m.Name)
                .Distinct().OrderBy(n => n).ToList();
            var asks = new[] { "GetComponent", "GetEntity", "TryGetSingleton", "Has", "Get" };
            var allowed = new HashSet<string>
            {
                "NotificationBus.add_NotificationPosted", "NotificationPanel.AddNotification", "ISingletonSaver.GetSingleton",
                "IObjectSaver.Set",
            };
            var changes = calls.Where(c => !c.Split('.')[1].StartsWith("get_") && !asks.Contains(c.Split('.')[1]) && !allowed.Contains(c))
                .ToList();
            if (changes.Count > 0) throw new Exception("calls " + string.Join(", ", changes));
            var view = mod.GetType("BeaverBuddies.Colonies.ColonyViewNotificationPatcher", true)!;
            if (!MethodsCalled(view.GetMethod("Prefix", all)!).Any(m => m.DeclaringType == journal && m.Name == "ShouldShow"))
                throw new Exception("the journal's filter no longer asks ColonyJournal");
            if (!MethodsCalled(journal.GetMethod("ShouldShow", all)!).Any(m => m.DeclaringType!.Name == "JournalFilter" && m.Name == "ShouldShow"))
                throw new Exception("ColonyJournal no longer decides by JournalFilter");
        });

        // A shared-colony game's save holds only what the Stability Fork's does. Every colony service that writes into a
        // save asks for separate colonies before it asks the saver for anything; the few that decide by something else
        // are named here, with why. (An IL order check: it cannot tell that the answer is used, only that it is asked.)
        test("Colony: every colony saver asks for separate colonies before it writes", () =>
        {
            var decidedOtherwise = new Dictionary<string, string>
            {
                ["ColonyModeService"] = "its own Enabled is the mode",
                ["ColonyScienceService"] = "on only in a separate-colonies game with separate science",
                ["ColonyReach"] = "keeps land only from Begin, which only a separate-colonies game calls",
            };
            var savers = mod.GetTypes()
                .Where(t => t.Namespace == "BeaverBuddies.Colonies" && t.GetInterfaces().Any(i => i.Name == "ISaveableSingleton" || i.Name == "IPersistentEntity"))
                .ToList();
            if (savers.Count < 10) throw new Exception($"only {savers.Count} savers found; the check lost them");
            bool AsksFirst(Type t)
            {
                var calls = MethodsCalled(t.GetMethod("Save", all)!).Select(m => m.Name).ToList();
                int asks = calls.IndexOf("get_IsSeparateColonies");
                int writes = calls.FindIndex(name => name == "GetSingleton" || name == "GetComponent");
                return asks >= 0 && (writes < 0 || asks < writes);
            }
            var ungated = savers.Where(t => !decidedOtherwise.ContainsKey(t.Name) && !AsksFirst(t)).Select(t => t.Name).ToList();
            if (ungated.Count > 0) throw new Exception("these save without asking: " + string.Join(", ", ungated));
            var gone = decidedOtherwise.Keys.Where(name => !savers.Any(t => t.Name == name)).ToList();
            if (gone.Count > 0) throw new Exception("named but no longer savers: " + string.Join(", ", gone));
            var owner = mod.GetType("BeaverBuddies.Colonies.DistrictOwner", true)!;
            if (!MethodsCalled(owner.GetMethod("InitializeEntity", all)!).Any(m => m.Name == "get_IsSeparateColonies"))
                throw new Exception("DistrictOwner gives district centers a slot without asking for separate colonies");
        });

        // In a multiplayer game the "instant" navmesh is brought up to date at the start of each tick instead of at the
        // end of each frame (Fixes/InstantNavMeshFix.cs), which replaces LateUpdateSingleton. These fail if the game
        // changes what that method does, or brings the instant navmesh up to date anywhere else.
        var synchronizer = Assembly.Load("Timberborn.Navigation").GetType("Timberborn.Navigation.NavigationSynchronizer", true)!;
        test("Colony: the game's end-of-frame navmesh update is still previews, instant changes, then telling listeners", () =>
        {
            var calls = MethodsCalled(synchronizer.GetMethod("LateUpdateSingleton", all)!).Select(m => m.Name).ToList();
            if (!calls.SequenceEqual(new[] { "ProcessPreviewChanges", "ProcessInstantChanges", "NotifyAllNavmeshChanges" }))
                throw new Exception("it now calls: " + string.Join(", ", calls));
        });
        test("Colony: the game brings the instant navmesh up to date only at the end of a frame and on load", () =>
        {
            var callers = synchronizer.GetMethods(all).Where(m => MethodsCalled(m).Any(c => c.Name == "ProcessInstantChanges" && c.DeclaringType == synchronizer))
                .Select(m => m.Name).OrderBy(n => n).ToList();
            if (!callers.SequenceEqual(new[] { "LateUpdateSingleton", "PostLoad" }))
                throw new Exception("called from: " + string.Join(", ", callers));
            var tick = MethodsCalled(synchronizer.GetMethod("Tick", all)!).Select(m => m.Name).ToList();
            if (!tick.SequenceEqual(new[] { "ProcessRegularChanges", "NotifyAllNavmeshChanges" }))
                throw new Exception("its Tick now calls: " + string.Join(", ", tick));
        });

        // A building placed before the game was hosted carries no colony. While it is a construction site it has no
        // district either, so ColonyReach reads the road at its entrance: the point the game finds a construction
        // site's builders by. These fail if the game stops using that point, or the land code reads anything that
        // differs between computers.
        test("Colony: the game finds a construction site's builders by the road at BuildingAccessible.CalculateAccess", () =>
        {
            var districtBuilding = Assembly.Load("Timberborn.GameDistricts").GetType("Timberborn.GameDistricts.DistrictBuilding", true)!;
            var calls = MethodsCalled(districtBuilding.GetMethod("ShouldBeAssignedToConstructionDistrict", all)!)
                .Select(m => m.DeclaringType!.Name + "." + m.Name).ToList();
            if (!calls.SequenceEqual(new[] { "BuildingAccessible.CalculateAccess", "DistrictCenter.IsOnInstantDistrictRoad" }))
                throw new Exception("it now calls: " + string.Join(", ", calls));
        });

        test("Colony: land bookkeeping reads the entrance on the tick-updated district map, and nothing that differs between computers", () =>
        {
            var reach = mod.GetType("BeaverBuddies.Colonies.ColonyReach", true)!;
            var stamp = mod.GetType("BeaverBuddies.Colonies.ColonyStamp", true)!;
            // Their own methods and their lambdas' (compiled into nested types).
            const BindingFlags declared = all | BindingFlags.DeclaredOnly;
            var calls = new[] { reach, stamp }
                .SelectMany(t => new[] { t }.Concat(t.GetNestedTypes(all)))
                .SelectMany(t => t.GetMethods(declared).Cast<MethodBase>().Concat(t.GetConstructors(declared)))
                .SelectMany(MethodsCalled)
                .Select(m => m.DeclaringType!.Name + "." + m.Name)
                .ToHashSet();
            foreach (string needed in new[] { "BuildingAccessible.CalculateAccess", "IDistrictService.IsOnDistrictRoad" })
                if (!calls.Contains(needed)) throw new Exception("no longer calls " + needed);
            // The instant map and a building's construction district follow the frame, and the local and displayed
            // slot are this computer's.
            var forbidden = new[] { "IDistrictService.IsOnInstantDistrictRoad", "DistrictCenter.IsOnInstantDistrictRoad",
                "DistrictCenter.AccessibleIsOnInstantDistrictRoad", "DistrictBuilding.get_InstantDistrict",
                "DistrictBuilding.get_ConstructionDistrict", "DistrictBuilding.GetDistrictOrConstructionDistrict",
                "DistrictBuilding.GetInstantOrConstructionDistrict", "DistrictOwner.OwnerOf", "ColonySession.get_LocalSlot",
                "ColonyScienceService.get_DisplaySlot" };
            var found = forbidden.Where(calls.Contains).ToList();
            if (found.Count > 0) throw new Exception("calls " + string.Join(", ", found));
        });
    }

    static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }

    /// <summary>The methods a method calls (call, callvirt), decoded from its IL, in order.</summary>
    static List<MethodBase> MethodsCalled(MethodBase method)
    {
        var methods = new List<MethodBase>();
        byte[] body = method.GetMethodBody()?.GetILAsByteArray();
        if (body == null) return methods;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);
        int position = 0;
        while (position < body.Length)
        {
            ushort code = body[position++];
            if (code == 0xfe) code = (ushort)(0xfe00 | body[position++]);
            OpCode op = opcodes[code];
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: position += 1; break;
                case OperandType.InlineVar: position += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: position += 8; break;
                case OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(body, position); break;
                case OperandType.InlineMethod:
                    int token = BitConverter.ToInt32(body, position);
                    if (op == OpCodes.Call || op == OpCodes.Callvirt) methods.Add(method.Module.ResolveMethod(token)!);
                    position += 4;
                    break;
                default: position += 4; break;
            }
        }
        return methods;
    }

    /// <summary>The static fields a method reads (ldsfld), decoded from its IL.</summary>
    static List<FieldInfo> StaticFieldsRead(MethodBase method)
    {
        var fields = new List<FieldInfo>();
        byte[] body = method.GetMethodBody()?.GetILAsByteArray();
        if (body == null) return fields;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);
        int position = 0;
        while (position < body.Length)
        {
            ushort code = body[position++];
            if (code == 0xfe) code = (ushort)(0xfe00 | body[position++]);
            OpCode op = opcodes[code];
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: position += 1; break;
                case OperandType.InlineVar: position += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: position += 8; break;
                case OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(body, position); break;
                case OperandType.InlineField:
                    int token = BitConverter.ToInt32(body, position);
                    if (op == OpCodes.Ldsfld) fields.Add(method.Module.ResolveField(token)!);
                    position += 4;
                    break;
                default: position += 4; break;
            }
        }
        return fields;
    }
}
