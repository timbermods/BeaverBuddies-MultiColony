#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// A player's action in co-op is caught by a prefix that records it (ReplayEvent.DoPrefix) and skips the game's method
// on that computer; the recorded action is played later, on every computer at the same tick, and while it is played
// the prefix lets the method run. Such a prefix must run before every other mod's prefix on the method: one that ran
// first would act on the clicking computer alone, and if it returned false Harmony would skip the recording prefix too,
// so the action would never be sent. Prefixes that replace the game's method run last (Priority.Last); recording
// prefixes run first (Priority.First). These checks find the recording prefixes from their code (the prefix, or a helper
// or lambda it uses, calls ReplayEvent.DoPrefix, DoEntityPrefix or ReplayService.RecordEvent) and read the priority
// Harmony itself gives each one.
internal static class RecordingPriorityChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    const int First = 800;

    sealed class Prefix
    {
        public MethodInfo Method = null!;
        public int Priority;
        // Game methods it patches, as "Type.Method (MethodType)".
        public List<string> Targets = new();
        public bool Manual;
        public string Name => $"{Method.DeclaringType!.Name}.{Method.Name}";
    }

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Assembly harmony = Assembly.Load("0Harmony");
        Type extensions = harmony.GetType("HarmonyLib.HarmonyMethodExtensions", true)!;
        Type harmonyMethodType = harmony.GetType("HarmonyLib.HarmonyMethod", true)!;
        object Call(string name, params object?[] args) => extensions.GetMethod(name)!.Invoke(null, args)!;
        T Field<T>(object info, string name) => (T)harmonyMethodType.GetField(name)!.GetValue(info)!;
        // Harmony gives -1 (none named) Priority.Normal (400) when it makes the patch.
        int PriorityOf(object info) => Field<int>(info, "priority") is var p && p == -1 ? 400 : p;
        // Null when the patch does not name its method (none of the recording prefixes may do that).
        string? TargetOf(object info) => Field<Type?>(info, "declaringType") is Type type && Field<string?>(info, "methodName") is string name
            ? $"{type.FullName}.{name} ({Field<object?>(info, "methodType") ?? "Normal"})" : null;

        Type replayEvent = mod.GetType("BeaverBuddies.Events.ReplayEvent", true)!;
        Type replayService = mod.GetType("BeaverBuddies.ReplayService", true)!;
        var sinks = new List<MethodBase>
        {
            replayEvent.GetMethod("DoPrefix", All)!,
            replayEvent.GetMethod("DoEntityPrefix", All)!,
            replayService.GetMethod("RecordEvent", All)!,
        };

        var prefixes = new List<Prefix>();
        var otherPatches = new List<(string name, string target)>();
        foreach (Type type in Types(mod))
        {
            if (!type.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")) continue;
            // As PatchAll reads a patch class: the class's attributes merged with the patch method's.
            object fromType = Call("GetMergedFromType", type);
            foreach (MethodInfo method in type.GetMethods(All))
            {
                string? role = new[] { "Prefix", "Postfix", "Transpiler", "Finalizer" }.FirstOrDefault(r => method.Name == r
                    || method.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.Harmony" + r));
                if (role == null) continue;
                object merged = Call("Merge", fromType, Call("GetMergedFromMethod", method));
                string? target = TargetOf(merged);
                if (role == "Prefix" && method.ReturnType == typeof(bool))
                    prefixes.Add(new Prefix { Method = method, Priority = PriorityOf(merged), Targets = target == null ? new() : new() { target } });
                else if (target != null) otherPatches.Add(($"{type.Name}.{method.Name}", target));
            }
        }
        // Prefixes the mod patches in by hand (harmony.Patch(original, prefix: new HarmonyMethod(method))): named by a
        // string in the method that patches them, and patched onto the game methods that method lists as (type, name).
        foreach (Type type in Types(mod))
        foreach (MethodInfo method in type.GetMethods(All))
        {
            List<(OpCode op, object? operand)> code = Instructions(method);
            if (!code.Any(i => i.operand is MethodBase m && m.DeclaringType?.FullName == "HarmonyLib.Harmony" && m.Name == "Patch")) continue;
            var patchers = new List<MethodBase> { method };
            foreach (var caller in Types(mod).SelectMany(t => t.GetMethods(All)))
                if (Instructions(caller).Any(i => i.operand is MethodBase m && Same(m, method))) patchers.Add(caller);
            var listed = new List<string>();
            var all = patchers.SelectMany(Instructions).ToList();
            for (int n = 0; n < all.Count; n++)
            {
                // typeof(Game type), nameof(Method): ldtoken, a call, ldstr. The mod's own types name the prefix instead.
                if (all[n].op != OpCodes.Ldtoken || all[n].operand is not Type target || target.Assembly == mod) continue;
                var name = all.Skip(n + 1).Take(3).FirstOrDefault(i => i.op == OpCodes.Ldstr).operand as string;
                if (name != null) listed.Add($"{target.FullName}.{name} (Normal)");
            }
            foreach (string name in code.Where(i => i.op == OpCodes.Ldstr).Select(i => (string)i.operand!))
            foreach (MethodInfo prefix in type.GetMethods(All).Where(m => m.Name == name && m.ReturnType == typeof(bool)))
            {
                // new HarmonyMethod(method) takes the method's own [HarmonyPriority], as the mod's call does.
                object info = Activator.CreateInstance(harmonyMethodType, prefix)!;
                prefixes.Add(new Prefix { Method = prefix, Priority = PriorityOf(info), Targets = listed, Manual = true });
            }
        }

        var memo = new Dictionary<MethodBase, bool>();
        bool Records(MethodBase method, HashSet<MethodBase> path)
        {
            if (sinks.Any(s => Same(s, method))) return true;
            if (memo.TryGetValue(method, out bool known)) return known;
            if (!path.Add(method)) return false;
            // What the method calls, and the lambdas it creates (a recording made in a dialog's callback).
            bool result = Instructions(method).Any(i => i.operand is MethodBase called && called.DeclaringType?.Assembly == mod
                && (i.op == OpCodes.Call || i.op == OpCodes.Callvirt || i.op == OpCodes.Ldftn || i.op == OpCodes.Newobj)
                && Records(called, path));
            path.Remove(method);
            return memo[method] = result;
        }
        var recording = prefixes.Where(p => Records(p.Method, new HashSet<MethodBase>())).ToList();
        var others = prefixes.Except(recording).ToList();

        test($"Harmony: every event-recording prefix runs first (Priority.First): {recording.Count} of the mod's {prefixes.Count} bool prefixes", () =>
        {
            // Found none, or far fewer than there are, means the search broke, not that the rule holds.
            if (recording.Count < 60) throw new Exception($"only {recording.Count} recording prefixes were found; the search is broken");
            string[] expected = { "SingleGoodAllowerAllowPatcher.Prefix", "SingleGoodAllowerDisallowPatcher.Prefix",
                "WorkerTypeToggleTryToUnlockPatcher.Prefix", "SpeedChangePatcher.Prefix", "AutomationEvent.UniversalPrefix" };
            var missed = expected.Where(e => !recording.Any(p => p.Name == e)).ToList();
            if (missed.Count > 0) throw new Exception("not found as recording: " + string.Join(", ", missed));
            if (recording.Any(p => p.Name == "PlantingLeveledCoordinatesPatcher.Prefix"))
                throw new Exception("the planting levelling override (which replaces the method) was taken for a recording prefix");
            var wrong = recording.Where(p => p.Priority != First).Select(p => $"{p.Name} ({p.Priority})").ToList();
            if (wrong.Count > 0) throw new Exception($"{wrong.Count} not Priority.First (800): " + string.Join(", ", wrong));
        });

        test("Harmony: no other patch of the mod shares a method with an event-recording prefix", () =>
        {
            // Another prefix's order against the recording prefix decides whether it runs at the click, on one computer,
            // or in the replay, on every computer; a postfix or finalizer runs at the click too, although the method was
            // skipped. None shares one now, so a new one fails here until someone has looked at it.
            var recorded = recording.SelectMany(p => p.Targets.Select(t => (p.Name, t))).ToList();
            var unread = recording.Where(p => p.Targets.Count == 0).Select(p => p.Name).ToList();
            if (unread.Count > 0) throw new Exception("the method these recording prefixes patch could not be read: " + string.Join(", ", unread));
            if (recording.Where(p => p.Manual).Sum(p => p.Targets.Count) < 50)
                throw new Exception("the game methods the mod patches by hand could not be read");
            var shared = others.SelectMany(p => p.Targets.Select(t => (p.Name, t))).Concat(otherPatches)
                .SelectMany(o => recorded.Where(r => r.t == o.Item2).Select(r => $"{o.Item1} and {r.Name} on {r.t}")).ToList();
            var twice = recorded.GroupBy(r => r.t).Where(g => g.Select(r => r.Name).Distinct().Count() > 1)
                .Select(g => string.Join(" and ", g.Select(r => r.Name).Distinct()) + " on " + g.Key);
            shared.AddRange(twice);
            if (shared.Count > 0) throw new Exception(string.Join("; ", shared));
        });
    }

    static bool Same(MethodBase a, MethodBase b) => a.Module == b.Module && a.MetadataToken == b.MetadataToken;

    static IEnumerable<Type> Types(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }

    static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).GroupBy(o => (ushort)o.Value).ToDictionary(g => g.Key, g => g.First());

    /// <summary>A method's IL, with the methods, types and strings its instructions name resolved.</summary>
    static List<(OpCode op, object? operand)> Instructions(MethodBase method)
    {
        var list = new List<(OpCode, object?)>();
        byte[]? body;
        try { body = method.GetMethodBody()?.GetILAsByteArray(); }
        catch { return list; }
        if (body == null) return list;
        Type[]? typeArguments = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
        Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        int position = 0;
        while (position < body.Length)
        {
            ushort value = body[position++];
            if (value == 0xfe && position < body.Length) value = (ushort)(0xfe00 | body[position++]);
            if (!OpCodesByValue.TryGetValue(value, out OpCode op)) break;
            object? operand = null;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: position += 1; break;
                case OperandType.InlineVar: position += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: position += 8; break;
                case OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(body, position); break;
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.InlineString:
                    int token = BitConverter.ToInt32(body, position);
                    position += 4;
                    try
                    {
                        operand = op.OperandType switch
                        {
                            OperandType.InlineMethod => method.Module.ResolveMethod(token, typeArguments, methodArguments),
                            OperandType.InlineString => method.Module.ResolveString(token),
                            _ => method.Module.ResolveMember(token, typeArguments, methodArguments),
                        };
                    }
                    catch { }
                    break;
                default: position += 4; break;
            }
            list.Add((op, operand));
        }
        return list;
    }
}
