using System.Reflection;

// What the 1.4.0-beta1 performance pass relies on, against the compiled mod: the static flags the busiest patches
// read are reset with the game, Steam is pumped before the game's own frame update, and the network sends events as
// the text the game serialized.
internal static class PerformanceRuntimeChecks
{
    public static void Run(Assembly mod, Action<string, Action> test)
    {
        test("Performance: the colony mode and science flags the busiest patches read are reset with the game", () =>
        {
            // ColonyModeService.IsSeparateColonies and ColonyScienceService.IsEnabled are static reads (every beaver's
            // working hours, every job search, every preview block every frame). They follow the running game only if
            // SingletonManager.Reset clears them when a game is left.
            Type resettable = mod.GetType("BeaverBuddies.IResettableSingleton", true)!;
            foreach (string name in new[] { "BeaverBuddies.Colonies.ColonyModeService", "BeaverBuddies.Colonies.ColonyScienceService" })
            {
                Type type = mod.GetType(name, true)!;
                if (!resettable.IsAssignableFrom(type)) throw new Exception(name + " does not reset its static flag with the game");
            }
        });

        test("Performance: Steam is pumped before the game's own scripts each frame", () =>
        {
            // A guest at the start of a tick can only use the host's word for it once the pump has handed it to the
            // receive thread; pumped after the game's ticker, every such message waited a whole frame more.
            Type pump = mod.GetType("BeaverBuddies.Steam.SteamNetPump", true)!;
            // Unity's class is named DefaultExecutionOrder, without the usual suffix.
            CustomAttributeData? order = pump.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.Name == "DefaultExecutionOrder" || a.AttributeType.Name == "DefaultExecutionOrderAttribute");
            if (order == null) throw new Exception("SteamNetPump has no DefaultExecutionOrder");
            int value = (int)order.ConstructorArguments[0].Value!;
            if (value >= 0) throw new Exception("SteamNetPump's execution order is " + value + "; it must run before the default order");
        });

        test("Performance: the game sends each event as the text it serialized, never parsed and written out again", () =>
        {
            // NetIOBase.WriteEvents must call the text overload; the parsed overload writes the text out again, and
            // the host used to do that once more for every guest and once for the hash.
            Type netIo = mod.GetType("BeaverBuddies.IO.NetIOBase`1", true)!;
            MethodInfo write = netIo.GetMethod("WriteEvents")!;
            byte[] il = write.GetMethodBody()!.GetILAsByteArray()!;
            var module = write.Module;
            bool callsText = false, callsParsed = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6F) continue;   // call, callvirt
                int token = BitConverter.ToInt32(il, i + 1);
                MethodBase? called;
                try { called = module.ResolveMethod(token, netIo.GetGenericArguments(), null); }
                catch (Exception) { continue; }
                if (called == null || called.Name != "DoUserInitiatedEvent") continue;
                ParameterInfo[] parameters = called.GetParameters();
                if (parameters.Length == 3 && parameters[0].ParameterType == typeof(string)) callsText = true;
                if (parameters.Length == 1) callsParsed = true;
            }
            if (!callsText || callsParsed) throw new Exception($"WriteEvents sends events as text: {callsText}; as parsed objects: {callsParsed}");
        });
    }
}
