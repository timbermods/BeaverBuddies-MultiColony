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
    }
}
