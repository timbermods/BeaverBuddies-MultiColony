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
            var expected = new[] { "ActionRefusedEvent", "AutosaveEvent", "BuildingUnlockedEvent", "ClientDesyncedEvent",
                "ColonyHandoverEvent", "ColonyPresenceEvent", "GroupedEvent", "HeartbeatEvent", "InitializeClientEvent", "PingEvent",
                "PlayerHelloEvent", "ShowOptionsMenuEvent", "SpeedSetEvent", "TraceLoggedForTickEvent",
                "TreeCuttingAreaEvent", "WorkerTypeUnlockedEvent", "WorkingHoursChangedEvent" };
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

        test("Colony: the events that leave joining open at tick 0 are listed for review", () =>
        {
            // The first action that changes the game closes joining (ReplayService): a later joiner would be sent the
            // save without it. Only events a joiner can do without may say they change nothing.
            var changes = replayEvent.GetMethod("ChangesGame", all)!;
            var neutral = eventTypes
                .Where(t => !(bool)changes.Invoke(RuntimeHelpers.GetUninitializedObject(t), null)!)
                .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            // ShowOptionsMenuEvent is a SpeedSetEvent (pausing to open the menu).
            var expected = new[] { "ActionRefusedEvent", "ClientDesyncedEvent", "HeartbeatEvent", "InitializeClientEvent", "PingEvent",
                "PlayerHelloEvent", "ShowOptionsMenuEvent", "SpeedSetEvent", "TraceLoggedForTickEvent" };
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
            ("Timberborn.Population.PopulationService", "Timberborn.Population", "_populationDataCollector"),
            ("Timberborn.ConstructionSitesUI.ConstructionSiteDebugFragment", "Timberborn.ConstructionSitesUI", "_constructionSite"),
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

        // A trading post buffers 100 of each good. The mod rewrites the one place the game reads its fixed 30; if the
        // game ever reads the number elsewhere, or not at all, these fail instead of crossings silently keeping 30.
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

        test("Colony: the buffer patch turns that read into 100", () =>
        {
            var codeType = Assembly.Load("0Harmony").GetType("HarmonyLib.CodeInstruction", true)!;
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(codeType))!;
            list.Add(Activator.CreateInstance(codeType, OpCodes.Ldsfld, capacityField)!);
            list.Add(Activator.CreateInstance(codeType, OpCodes.Ret, null)!);
            var transpiler = mod.GetType("BeaverBuddies.Colonies.TradingPostCapacityPatcher", true)!.GetMethod("Transpiler", all)!;
            var result = ((IEnumerable)transpiler.Invoke(null, new object[] { list })!).Cast<object>().ToList();
            var opcode = (OpCode)codeType.GetField("opcode")!.GetValue(result[0])!;
            object operand = codeType.GetField("operand")!.GetValue(result[0]);
            if (opcode != OpCodes.Ldc_I4 || !(operand is int value) || value != 100)
                throw new Exception($"got {opcode} {operand}");
            if (result.Count != 2) throw new Exception("instructions were added or lost");
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
