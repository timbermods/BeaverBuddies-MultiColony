using System.Reflection;
using System.Reflection.Emit;

// The game builds every service and panel through its container, which can only hand out what some configurator
// bound. A class of this mod asking for something the game never binds under that exact type (a concrete class the
// game binds only by its interface, say) stops the game while it loads a save. These checks read which types the
// game's configurators and this mod's bind, per context, straight from their code, and check every constructor this
// mod gives the container against them.
internal static class BindingChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    sealed class Bindings
    {
        public readonly HashSet<Type> Single = new();
        public readonly HashSet<Type> Multi = new();
        // Classes the container will build: bound themselves, bound to an interface, or providers.
        public readonly HashSet<Type> Built = new();
    }

    /// <param name="modDirectories">Where the mods this one needs keep their code (Mod Settings binds services too).</param>
    public static void Run(Assembly mod, string managedDirectory, IEnumerable<string> modDirectories, Action<string, Action> test)
    {
        var game = new Dictionary<string, Bindings>();
        var files = Directory.GetFiles(managedDirectory, "Timberborn.*.dll").Concat(Directory.GetFiles(managedDirectory, "Bindito.*.dll"))
            .Concat(modDirectories.Where(Directory.Exists).SelectMany(d => Directory.GetFiles(d, "*.dll")))
            .Where(f => !string.Equals(Path.GetFileNameWithoutExtension(f), mod.GetName().Name, StringComparison.OrdinalIgnoreCase));
        foreach (string file in files)
        {
            Assembly assembly;
            try { assembly = Assembly.Load(Path.GetFileNameWithoutExtension(file)); }
            catch { continue; }
            foreach (Type type in Types(assembly)) Collect(type, assembly, game);
        }
        var mine = new Dictionary<string, Bindings>();
        foreach (Type type in Types(mod)) Collect(type, mod, mine);

        test($"Colony: the game's configurators were read ({string.Join(", ", game.Select(g => $"{g.Key} {g.Value.Single.Count}"))})", () =>
        {
            if (!game.TryGetValue("Game", out Bindings bindings) || bindings.Single.Count < 500)
                throw new Exception("too few game bindings found; the reading of configurators is broken");
        });

        foreach (var (context, modBindings) in mine.OrderBy(m => m.Key))
        {
            test($"Colony: everything this mod builds in the {context} context gets what it asks for", () =>
            {
                var missing = new List<string>();
                foreach (Type built in modBindings.Built.Where(t => t.Assembly == mod).OrderBy(t => t.FullName))
                {
                    ConstructorInfo constructor = built.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
                    if (constructor == null) continue;
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                    {
                        if (!Provided(parameter.ParameterType, context, game, modBindings))
                            missing.Add($"{built.Name} needs {Name(parameter.ParameterType)}");
                    }
                }
                if (missing.Count > 0) throw new Exception("nothing binds: " + string.Join("; ", missing));
            });
        }
    }

    static bool Provided(Type type, string context, Dictionary<string, Bindings> game, Bindings mine)
    {
        if (type.Namespace?.StartsWith("Bindito") == true) return true;
        // The root context's services reach every scene.
        var sources = new[] { mine, Get(game, context), Get(game, "Bootstrapper") };
        if (sources.Any(s => s.Single.Contains(type))) return true;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            Type item = type.GetGenericArguments()[0];
            return sources.Any(s => s.Multi.Contains(item) || s.Single.Contains(item));
        }
        return false;
    }

    static Bindings Get(Dictionary<string, Bindings> all, string context) =>
        all.TryGetValue(context, out Bindings bindings) ? bindings : new Bindings();

    static void Collect(Type type, Assembly assembly, Dictionary<string, Bindings> into)
    {
        if (type.IsAbstract || !type.GetInterfaces().Any(i => i.FullName == "Bindito.Core.IConfigurator")) return;
        var contexts = type.GetCustomAttributesData()
            .Where(a => a.AttributeType.FullName == "Bindito.Core.ContextAttribute")
            .Select(a => a.ConstructorArguments[0].Value as string).Where(c => c != null).ToList();
        if (contexts.Count == 0) return;
        var found = new Bindings();
        var visited = new HashSet<MethodBase>();
        foreach (MethodInfo method in type.GetMethods(All)) Scan(method, assembly, found, visited);
        foreach (string context in contexts)
        {
            if (!into.TryGetValue(context, out Bindings bindings)) into[context] = bindings = new Bindings();
            bindings.Single.UnionWith(found.Single);
            bindings.Multi.UnionWith(found.Multi);
            bindings.Built.UnionWith(found.Built);
        }
    }

    // Reads the calls in a method (and in methods of the same assembly it calls): Bind<T>, MultiBind<T>, To<T>, ToProvider<T>.
    static void Scan(MethodBase method, Assembly assembly, Bindings found, HashSet<MethodBase> visited)
    {
        if (!visited.Add(method)) return;
        byte[] body;
        try { body = method.GetMethodBody()?.GetILAsByteArray(); }
        catch { return; }
        if (body == null) return;
        foreach (int token in CallTokens(body))
        {
            MethodBase called;
            try { called = method.Module.ResolveMethod(token, method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments()); }
            catch { continue; }
            if (called == null) continue;
            if (called is MethodInfo info && info.IsGenericMethod)
            {
                Type argument = info.GetGenericArguments()[0];
                switch (info.Name)
                {
                    case "Bind":
                        found.Single.Add(argument);
                        if (!argument.IsInterface && !argument.IsAbstract) found.Built.Add(argument);
                        break;
                    case "MultiBind": found.Multi.Add(argument); break;
                    case "To":
                    case "ToProvider": found.Built.Add(argument); break;
                }
            }
            // Helpers of the same assembly that bind for the configurator (ColonyConfigurator.Configure, nested lambdas),
            // and configurators it installs (Install(new Other())).
            if (called.DeclaringType?.Assembly != assembly) continue;
            if (called is ConstructorInfo && called.DeclaringType.GetInterfaces().Any(i => i.FullName == "Bindito.Core.IConfigurator"))
            {
                foreach (MethodInfo installed in called.DeclaringType.GetMethods(All)) Scan(installed, assembly, found, visited);
            }
            else if (called.GetMethodBody() != null) Scan(called, assembly, found, visited);
        }
    }

    static IEnumerable<int> CallTokens(byte[] body)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).GroupBy(o => (ushort)o.Value).ToDictionary(g => g.Key, g => g.First());
        int position = 0;
        while (position < body.Length)
        {
            ushort code = body[position++];
            if (code == 0xfe && position < body.Length) code = (ushort)(0xfe00 | body[position++]);
            if (!opcodes.TryGetValue(code, out OpCode op)) yield break;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: position += 1; break;
                case OperandType.InlineVar: position += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: position += 8; break;
                case OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(body, position); break;
                case OperandType.InlineMethod:
                    int token = BitConverter.ToInt32(body, position);
                    position += 4;
                    if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Ldftn || op == OpCodes.Newobj) yield return token;
                    break;
                default: position += 4; break;
            }
        }
    }

    static IEnumerable<Type> Types(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
        catch { return Enumerable.Empty<Type>(); }
    }

    static string Name(Type type) => type.IsGenericType
        ? $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>"
        : type.Name;
}
