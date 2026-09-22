#nullable enable
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

// Dev mode's "Add 1000 Science" (the dev panel), against the compiled mod: it added the science on the computer it was
// clicked on alone, and the next unlock paid with it was skipped on the other computers (a colony desync, 1.4.0-beta12).
// It is now an action like dev mode's free unlock: recorded from the game's own button, refused unless the host has dev
// mode on (and from a player with no colony yet), and played on every computer.
internal static class DevScienceChecks
{
    public static void Run(Assembly mod, Action<string, Action> test)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        // Looked up in each check, so a build without them fails the checks instead of stopping the run.
        Type Mod(string name) => mod.GetType(name, false) ?? throw new Exception(name + " is missing");
        Type adder = Assembly.Load("Timberborn.ScienceSystemUI").GetType("Timberborn.ScienceSystemUI.ScienceAdder", true)!;
        Type scienceService = Assembly.Load("Timberborn.ScienceSystem").GetType("Timberborn.ScienceSystem.ScienceService", true)!;
        Type rules = mod.GetType("BeaverBuddies.Colonies.ColonyRulesService", true)!;

        // The types a method tests its argument against ("x is T"), which IlScan leaves unnamed.
        List<Type> TypesTested(MethodInfo method)
        {
            byte[] body = method.GetMethodBody()!.GetILAsByteArray()!;
            return IlScan.Instructions(method).Where(i => i.Op == OpCodes.Isinst)
                .Select(i => method.Module.ResolveType(BitConverter.ToInt32(body, i.Offset + 1))).ToList();
        }

        test("Dev mode: Add 1000 Science is recorded from the dev panel's own button, for the amount it adds", () =>
        {
            Type patcher = Mod("BeaverBuddies.Events.ScienceAdderPatcher");
            var target = patcher.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch")
                ?? throw new Exception("the patcher has no HarmonyPatch attribute");
            var arguments = target.ConstructorArguments;
            if (arguments.Count != 2 || ((Type)arguments[0].Value!).FullName != adder.FullName || (string)arguments[1].Value! != "AddScience")
                throw new Exception("the patcher does not patch ScienceAdder.AddScience");
            MethodInfo addScience = adder.GetMethod("AddScience", all) ?? throw new Exception("the game has no ScienceAdder.AddScience");
            var code = IlScan.Instructions(addScience);
            if (!code.Any(i => i.Calls && i.Is("Timberborn.ScienceSystem.ScienceService", "AddPoints")))
                throw new Exception("the game's AddScience no longer calls ScienceService.AddPoints");
            // The amount is written into the action, so it must be what the button adds.
            int amount = (int)patcher.GetField("Amount", all)!.GetValue(null)!;
            byte[] body = addScience.GetMethodBody()!.GetILAsByteArray()!;
            if (!code.Any(i => i.Op == OpCodes.Ldc_I4 && BitConverter.ToInt32(body, i.Offset + 1) == amount))
                throw new Exception($"the game's AddScience no longer adds {amount}");
        });

        test("Dev mode: Add 1000 Science is refused unless the host has dev mode on, and from a player with no colony", () =>
        {
            Type eventType = Mod("BeaverBuddies.Events.ScienceAddedEvent");
            MethodInfo isDevShortcut = rules.GetMethod("IsDevShortcut", all)!;
            object replayEvent = Activator.CreateInstance(eventType, true)!;
            if (!(bool)isDevShortcut.Invoke(null, new[] { replayEvent })!)
                throw new Exception("it is not one of the dev shortcuts the host refuses while its dev mode is off");
            // A player the host has not seated has no colony pool to add to (it would land in the first colony's).
            if (!TypesTested(rules.GetMethod("AllowOnHost", all)!).Contains(eventType))
                throw new Exception("the host does not refuse it from a player not seated yet");
        });

        test("Dev mode: Add 1000 Science played adds the science, the actor's colony's with separate science", () =>
        {
            Type eventType = Mod("BeaverBuddies.Events.ScienceAddedEvent");
            object service = RuntimeHelpers.GetUninitializedObject(scienceService);
            var context = (ReplayContextProxy)DispatchProxy.Create(mod.GetType("BeaverBuddies.Events.IReplayContext", true)!, typeof(ReplayContextProxy));
            context.Registry = service; context.RegistryType = scienceService;
            object replayEvent = Activator.CreateInstance(eventType, true)!;
            eventType.GetField("amount")!.SetValue(replayEvent, 1000);
            eventType.GetMethod("Replay")!.Invoke(replayEvent, new object[] { context });
            int points = (int)scienceService.GetProperty("SciencePoints")!.GetValue(service)!;
            if (points != 1000) throw new Exception($"played, the pool holds {points} instead of 1000");
            // With separate science the add runs inside the actor's slot (the pool patches send it there).
            if (!IlScan.Instructions(eventType.GetMethod("Replay")!).Any(i => i.Calls && i.Is("BeaverBuddies.Colonies.ColonyScienceService", "InSlot")))
                throw new Exception("with separate science it is not added to the actor's colony");
        });
    }
}
