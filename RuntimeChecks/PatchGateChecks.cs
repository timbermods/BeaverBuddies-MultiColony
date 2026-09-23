#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// Two checks over the compiled mod that StabilityTests made by reading source text:
// 1. Mixed factions' patches do nothing outside a mixed game: in each prefix, postfix and finalizer of a
//    BeaverBuddies.Factions patch class, the first thing that calls, creates or stores (a lambda's captured-variable
//    class aside) is MixedFactions.IsOn, it is
//    branched on at once, and the not-mixed side returns without calling or storing anything (a bool prefix returns
//    true, so the game's own method runs). A comment, a gate in another method of the class, or a gate after other work
//    fails it.
// 2. No member stacks two patch targets: Harmony merges every [HarmonyPatch] on a class, and a method's with its class's,
//    so a second method name on one member, or a method-level name under a class-level one, silently patches only one.
//    Read from the compiled attributes, so two on one line and named arguments count.
internal static class PatchGateChecks
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static readonly string[] PatchNames = { "Prefix", "Postfix", "Finalizer" };
    // Where MixedFactions.IsOn is decided, and the solo New Game's capture: they run in every game on purpose.
    static readonly HashSet<string> Deciding = new() { "MixedFactionsDecidePatcher", "NewGameFactionCapturePatcher" };

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type mixed = mod.GetType("BeaverBuddies.Factions.MixedFactions", true)!;
        MethodInfo isOn = mixed.GetProperty("IsOn")!.GetGetMethod()!;

        test("Factions: every patch of the feature checks MixedFactions.IsOn first and does nothing when it is off", () =>
        {
            int methods = 0;
            var wrong = new List<string>();
            foreach (Type type in mod.GetTypes().Where(t => t.Namespace == "BeaverBuddies.Factions" && IsPatchClass(t)))
            {
                if (Deciding.Contains(type.Name)) continue;
                foreach (MethodInfo method in type.GetMethods(Declared).Where(IsPatchMethod))
                {
                    methods++;
                    string? problem = Gate(method, isOn);
                    if (problem != null) wrong.Add($"{type.Name}.{method.Name}: {problem}");
                }
            }
            if (methods < 40) throw new Exception($"found only {methods} patch methods; the check is not looking in the right place");
            if (wrong.Count > 0) throw new Exception(string.Join("; ", wrong));
        });

        test("Patches: no class or method stacks two patch targets (read from the compiled attributes)", () =>
        {
            int members = 0;
            var stacked = new List<string>();
            foreach (Type type in mod.GetTypes())
            {
                var onClass = Patches(type.GetCustomAttributesData());
                var methods = type.GetMethods(Declared).Select(m => (m, Patches(m.GetCustomAttributesData()))).Where(p => p.Item2.Count > 0).ToList();
                if (onClass.Count == 0 && methods.Count == 0) continue;
                members++;
                if (onClass.Count(NamesMethod) > 1 || onClass.Count(NamesType) > 1) stacked.Add($"{type.FullName} (class)");
                foreach (var (method, onMethod) in methods)
                {
                    if (onMethod.Count(NamesMethod) > 1 || onMethod.Count(NamesType) > 1) stacked.Add($"{type.FullName}.{method.Name}");
                    else if (onClass.Any(NamesMethod) && onMethod.Any(NamesMethod)) stacked.Add($"{type.FullName}.{method.Name} (names a method under a class that names one)");
                }
            }
            if (members < 250) throw new Exception($"found only {members} patch classes; the check is not looking in the right place");
            if (stacked.Count > 0) throw new Exception("stacked: " + string.Join(", ", stacked));
        });
    }

    static bool IsPatchClass(Type type) => type.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");

    static bool IsPatchMethod(MethodInfo method) => PatchNames.Contains(method.Name)
        || method.GetCustomAttributesData().Any(a => a.AttributeType.FullName is "HarmonyLib.HarmonyPrefix" or "HarmonyLib.HarmonyPostfix" or "HarmonyLib.HarmonyFinalizer");

    static List<CustomAttributeData> Patches(IEnumerable<CustomAttributeData> attributes) =>
        attributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToList();

    static bool NamesMethod(CustomAttributeData a) => a.Constructor.GetParameters().Zip(a.ConstructorArguments)
        .Any(p => p.First.Name == "methodName" || (p.First.Name == "methodType" && Convert.ToInt32(p.Second.Value) != 0));

    static bool NamesType(CustomAttributeData a) => a.Constructor.GetParameters()
        .Any(p => p.Name is "declaringType" or "typeName" or "assemblyQualifiedDeclaringType");

    // Null when the method is gated; else what is wrong.
    static string? Gate(MethodInfo method, MethodInfo isOn)
    {
        var code = IlScan.Instructions(method);
        int at = 0;
        for (; at < code.Count; at++)
        {
            var i = code[at];
            if (i.Calls && i.Member is MethodInfo called && called == isOn) break;
            // A lambda's captured variables: the compiler makes their class where the method starts (an allocation, no effect).
            if ((i.Op == OpCodes.Newobj || i.Op == OpCodes.Stfld) && i.Member?.DeclaringType?.Name.StartsWith("<>c__DisplayClass") == true) continue;
            // A lambda that captures nothing: made once and cached in the compiler's <>c class.
            if ((i.Op == OpCodes.Newobj && typeof(Delegate).IsAssignableFrom(i.Member?.DeclaringType)) || (i.Op == OpCodes.Stsfld && i.Member?.DeclaringType?.Name == "<>c")) continue;
            // A patch method that hands everything to a helper of its own class (FactionEmptySlotPatcher.Show): the helper is gated.
            if (i.Calls && i.Member is MethodInfo helper && helper.DeclaringType == method.DeclaringType && ReturnsAtOnce(code, at + 1))
            {
                string? inner = Gate(helper, isOn);
                return inner == null ? null : $"its helper {helper.Name} {inner}";
            }
            if (i.Calls || i.Op == OpCodes.Newobj || i.Stores || i.Op == OpCodes.Stelem_Ref || i.Op == OpCodes.Stobj)
                return $"{(i.Member?.Name ?? i.Op.Name)} comes before MixedFactions.IsOn";
        }
        if (at == code.Count) return "never reads MixedFactions.IsOn";
        // The not-mixed side, followed from the read with IsOn false. Both builds are checked: Release is optimized, but
        // "Release Steam" (the zip's) is not, and its IL keeps each condition in a local before it branches.
        int steps = 0;
        return NotMixed(at + 1, new List<int?> { 0 }, new Dictionary<int, int?>());

        string? NotMixed(int index, List<int?> stack, Dictionary<int, int?> locals)
        {
            while (true)
            {
                if (index < 0 || index >= code.Count) return "the not-mixed side does not return";
                if (++steps > 512) return "the not-mixed side is too long to follow";
                var i = code[index];
                OpCode op = i.Op;
                string name = op.Name!;
                int? Pop() { if (stack.Count == 0) return null; int? v = stack[^1]; stack.RemoveAt(stack.Count - 1); return v; }
                if (op == OpCodes.Ret)
                {
                    if (method.ReturnType == typeof(bool) && method.Name != "Postfix" && (stack.Count == 0 || stack[^1] != 1))
                        return "returns false when not mixed (the game's own method would not run)";
                    return null;
                }
                if (op == OpCodes.Nop) { index++; continue; }
                if (op == OpCodes.Br || op == OpCodes.Br_S) { index = code.FindIndex(x => x.Offset == i.Target); continue; }
                if (op == OpCodes.Brtrue || op == OpCodes.Brtrue_S || op == OpCodes.Brfalse || op == OpCodes.Brfalse_S)
                {
                    int? value = Pop();
                    bool onTrue = op == OpCodes.Brtrue || op == OpCodes.Brtrue_S;
                    int taken = code.FindIndex(x => x.Offset == i.Target);
                    if (value.HasValue) { index = (value.Value != 0) == onTrue ? taken : index + 1; continue; }
                    // A condition this side can't know (an argument, say): both ways must return without doing anything.
                    return NotMixed(taken, new List<int?>(stack), new Dictionary<int, int?>(locals))
                        ?? NotMixed(index + 1, new List<int?>(stack), new Dictionary<int, int?>(locals));
                }
                if (name.StartsWith("ldc.i4"))
                {
                    stack.Add(i.Number ?? (name == "ldc.i4.m1" ? -1 : name.Length == 8 && char.IsDigit(name[7]) ? name[7] - '0' : (int?)null));
                    index++; continue;
                }
                if (op == OpCodes.Ldnull) { stack.Add(0); index++; continue; }
                if (name.StartsWith("ldarg") || name.StartsWith("ldloca") || op == OpCodes.Ldsfld) { stack.Add(null); index++; continue; }
                if (op == OpCodes.Ldfld) { Pop(); stack.Add(null); index++; continue; }
                if (name.StartsWith("ldloc"))
                {
                    int slot = i.Number ?? name[^1] - '0';
                    stack.Add(locals.TryGetValue(slot, out int? v) ? v : null);
                    index++; continue;
                }
                if (name.StartsWith("stloc"))
                {
                    locals[i.Number ?? name[^1] - '0'] = Pop();
                    index++; continue;
                }
                if (op == OpCodes.Ceq)
                {
                    int? b = Pop(), a = Pop();
                    stack.Add(a.HasValue && b.HasValue ? (a == b ? 1 : 0) : null);
                    index++; continue;
                }
                if (op == OpCodes.Cgt || op == OpCodes.Cgt_Un || op == OpCodes.Clt || op == OpCodes.Clt_Un) { Pop(); Pop(); stack.Add(null); index++; continue; }
                if (op == OpCodes.Pop) { Pop(); index++; continue; }
                if (op == OpCodes.Dup) { int? v = stack.Count > 0 ? stack[^1] : null; stack.Add(v); index++; continue; }
                // Setting an out or ref argument (__result) is how a patch answers; it changes nothing else.
                if (name.StartsWith("stind")) { Pop(); Pop(); index++; continue; }
                return $"does {(i.Member?.Name ?? name)} when not mixed";
            }
        }
    }

    // Whether the method returns straight after instruction `from` (what an unoptimized build puts between a call and its ret).
    static bool ReturnsAtOnce(List<IlScan.Instruction> code, int from)
    {
        for (int index = from, steps = 0; index >= 0 && index < code.Count && steps < 8; steps++)
        {
            var i = code[index];
            if (i.Op == OpCodes.Ret) return true;
            if (i.Op == OpCodes.Br || i.Op == OpCodes.Br_S) { index = code.FindIndex(x => x.Offset == i.Target); continue; }
            if (i.Op != OpCodes.Nop && !i.Op.Name!.StartsWith("stloc") && !i.Op.Name.StartsWith("ldloc")) return false;
            index++;
        }
        return false;
    }
}
