#nullable enable
using System.Reflection;

// Every [HarmonyPatch] class of the mod (all of them, TargetMethods classes and MultiStart included), resolved by
// Harmony's own code the way PatchAll resolves it: PatchClassProcessor merges the class's and each patch method's
// attributes, GetBulkMethods runs TargetMethod(s), and PatchTools.GetOriginalMethod finds each attribute target
// (AccessTools.DeclaredMethod: declared on that type, by name, and ambiguous if overloaded with no argument types).
// Then the rules Harmony applies only when it really patches: a target with a body, and every parameter of a
// prefix/postfix/finalizer a known injection, a ___field of the target's type, __n, or one of the target's arguments.
// One failure here is PatchAll throwing in StartMod: the game stops on the mod manager's screen.
internal static class HarmonyResolutionChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const BindingFlags Declared = All | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Assembly harmonyAssembly = Assembly.Load("0Harmony");
        Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true)!;
        Type processorType = harmonyAssembly.GetType("HarmonyLib.PatchClassProcessor", true)!;
        MethodInfo getBulk = processorType.GetMethod("GetBulkMethods", All)!;
        FieldInfo patchMethods = processorType.GetField("patchMethods", All)!;
        MethodInfo getOriginal = harmonyAssembly.GetType("HarmonyLib.PatchTools", true)!.GetMethod("GetOriginalMethod", All)!;
        MethodInfo hasHarmonyAttribute = harmonyAssembly.GetTypes().Where(t => t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)).First(m => m.Name == "HasHarmonyAttribute");
        object harmony = Activator.CreateInstance(harmonyType, "BeaverBuddies.RuntimeChecks")!;

        var classes = mod.GetTypes().Where(t => (bool)hasHarmonyAttribute.Invoke(null, new object[] { t })!).ToList();
        var failures = new List<string>();
        var targets = new List<(Type patchClass, MethodBase target)>();
        int injections = 0;
        foreach (Type type in classes)
        {
            try
            {
                object processor = harmonyType.GetMethod("CreateClassProcessor")!.Invoke(harmony, new object[] { type })!;
                var patches = ((System.Collections.IEnumerable)patchMethods.GetValue(processor)!).Cast<object>().ToList();
                var bulk = (List<MethodBase>)getBulk.Invoke(processor, null)!;
                foreach (object patch in patches)
                {
                    object info = patch.GetType().GetField("info", All)!.GetValue(patch)!;
                    object? kind = patch.GetType().GetField("type", All)!.GetValue(patch);
                    IEnumerable<MethodBase> originals = bulk.Count > 0 ? bulk : new[] { (MethodBase?)getOriginal.Invoke(null, new[] { info })
                        ?? throw new Exception("Undefined target method for patch method " + ((MethodInfo)info.GetType().GetField("method")!.GetValue(info)!).Name) };
                    foreach (MethodBase original in originals)
                    {
                        targets.Add((type, original));
                        if (original.IsAbstract) failures.Add($"{type.Name}: {original.DeclaringType!.Name}.{original.Name} has no body");
                        if (kind?.ToString() is "Prefix" or "Postfix" or "Finalizer")
                            foreach (string problem in Injections((MethodInfo)info.GetType().GetField("method")!.GetValue(info)!, original))
                            {
                                failures.Add($"{type.Name}: {problem}");
                            }
                        injections++;
                    }
                }
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                failures.Add($"{type.Name}: {inner.GetType().Name}: {inner.Message}");
            }
        }

        test($"Harmony: every patch of the mod resolves with Harmony's own resolver ({classes.Count} classes, {targets.Count} targets)", () =>
        {
            if (classes.Count < 250 || targets.Count < 300) throw new Exception($"found only {classes.Count} classes and {targets.Count} targets");
            if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
        });
    }

    // MethodPatcher.EmitCallParameter's rules for one patch method against one target.
    static IEnumerable<string> Injections(MethodInfo patch, MethodBase original)
    {
        Type returns = original is MethodInfo info ? info.ReturnType : typeof(void);
        ParameterInfo[] arguments = original.GetParameters();
        string where = $"{patch.Name} on {original.DeclaringType!.Name}.{original.Name}";
        foreach (ParameterInfo parameter in patch.GetParameters())
        {
            string name = parameter.Name!;
            Type type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            switch (name)
            {
                case "__instance":
                    if (!original.IsStatic && type != typeof(object) && !type.IsAssignableFrom(original.DeclaringType)) yield return $"{where}: __instance is {type.Name}";
                    continue;
                case "__originalMethod": case "__args": case "__state": case "__exception": case "__runOriginal": continue;
                case "__result":
                    if (returns == typeof(void)) yield return $"{where}: __result on a void method";
                    else if (type != typeof(object) && !type.IsAssignableFrom(returns)) yield return $"{where}: __result is {type.Name}, the target returns {returns.Name}";
                    continue;
            }
            if (name.StartsWith("___"))
            {
                string field = name.Substring(3);
                bool found = field.All(char.IsDigit) ? original.DeclaringType!.GetFields(Declared).Length > int.Parse(field)
                    : FieldOf(original.DeclaringType!, field) != null;
                if (!found) yield return $"{where}: no field {field}";
                continue;
            }
            if (name.StartsWith("__"))
            {
                if (!int.TryParse(name.Substring(2), out int index) || index < 0 || index >= arguments.Length) yield return $"{where}: no argument {name}";
                continue;
            }
            ParameterInfo? argument = arguments.FirstOrDefault(a => a.Name == name);
            if (argument == null) { yield return $"{where}: the target has no argument \"{name}\""; continue; }
            Type argumentType = argument.ParameterType.IsByRef ? argument.ParameterType.GetElementType()! : argument.ParameterType;
            if (type != typeof(object) && !type.IsAssignableFrom(argumentType)) yield return $"{where}: {name} is {type.Name}, the target's is {argumentType.Name}";
        }
    }

    // AccessTools.Field: the type's own field or one of a base type's.
    static FieldInfo? FieldOf(Type type, string name)
    {
        for (Type? t = type; t != null; t = t.BaseType)
        {
            FieldInfo? field = t.GetField(name, Declared);
            if (field != null) return field;
        }
        return null;
    }
}
