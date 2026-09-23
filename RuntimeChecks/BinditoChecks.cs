#nullable enable
using System.Reflection;

// Every scene's container, built and validated with Bindito's own classes, as the game builds it: the Bootstrapper
// (project) container first, then each scene's as its child, running the configurators of that context (the game's,
// Mod Settings' and this mod's), building every binding and validating every binding's dependency chain
// (ContainerCreator.CreateContainer; a missing or duplicate binding stops the scene from loading). The Game scene is
// built twice, as a single-player game (EventIO unset: ReplayConfigurator returns before its co-op services) and as a
// co-op one. What the game's own configurators already fail on here (Mod Settings' mod-manager UI, which uses members
// the .NET runtime of these checks won't let it reach) is subtracted: only what this mod adds fails the check.
//
// BindingChecks reads bindings from IL and checks constructors; this also catches a service bound only in co-op asked
// for by one bound in every game, a Bootstrapper binding that isn't exported, [Inject] methods, providers, a type bound
// twice, a class with two parameterful constructors, and a cycle.
internal static class BinditoChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void Run(Assembly mod, string managedDirectory, IEnumerable<string> modDirectories, Action<string, Action> test)
    {
        Assembly core = Assembly.Load("Bindito.Core");
        Type Core(string name) => core.GetType(name, true)!;
        Type configuratorInterface = Core("Bindito.Core.IConfigurator");
        Type contextAttribute = Core("Bindito.Core.ContextAttribute");
        Type containerInterface = Core("Bindito.Core.IContainer");

        var configurators = new List<(Type type, string[] contexts, bool mine)>();
        var files = Directory.GetFiles(managedDirectory, "Timberborn.*.dll").Concat(Directory.GetFiles(managedDirectory, "Bindito.*.dll"))
            .Concat(modDirectories.Where(Directory.Exists).SelectMany(d => Directory.GetFiles(d, "*.dll")));
        var assemblies = new List<Assembly>();
        foreach (string file in files)
        {
            try { assemblies.Add(Assembly.Load(Path.GetFileNameWithoutExtension(file))); }
            catch { }
        }
        foreach (Assembly assembly in assemblies.Where(a => a != mod).Distinct().Append(mod))
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
            foreach (Type type in types)
            {
                if (type.IsAbstract || !configuratorInterface.IsAssignableFrom(type)) continue;
                string[] contexts;
                try { contexts = type.GetCustomAttributes(contextAttribute, false).Select(a => (string)contextAttribute.GetProperty("ContextName")!.GetValue(a)!).ToArray(); }
                catch { continue; }
                if (contexts.Length > 0) configurators.Add((type, contexts, assembly == mod));
            }
        }

        // The mod's Plugin.Log writes through a logger StartMod sets in the game.
        FieldInfo logger = mod.GetType("BeaverBuddies.Plugin", true)!.GetField("logger", All)!;
        if (logger.GetValue(null) == null) logger.SetValue(null, NullProxy.Of(logger.FieldType));
        FieldInfo eventIO = mod.GetType("BeaverBuddies.IO.EventIO", true)!.GetField("instance", All)!;

        object Build(IEnumerable<Type> ofContext, object? parent, List<string> problems)
        {
            object binder = Activator.CreateInstance(Core("Bindito.Core.Internal.Binder"), new[] { parent })!;
            object registry = Activator.CreateInstance(Core("Bindito.Core.Internal.BindingBuilderRegistry"), binder)!;
            object definition = Activator.CreateInstance(Core("Bindito.Core.Internal.ContainerDefinition"), registry,
                Activator.CreateInstance(Core("Bindito.Core.Internal.ProvisionListenerNotifier")),
                Activator.CreateInstance(Core("Bindito.Core.Internal.InjectionListenerNotifier")))!;
            void BindInstance(Type bound, object instance)
            {
                object builder = definition.GetType().GetMethod("Bind")!.MakeGenericMethod(bound).Invoke(definition, null)!;
                builder.GetType().GetMethod("ToInstance")!.Invoke(builder, new[] { instance });
            }
            BindInstance(containerInterface, NullProxy.Of(containerInterface));
            foreach (Type type in ofContext)
            {
                try { configuratorInterface.GetMethod("Configure")!.Invoke(Activator.CreateInstance(type, true), new[] { definition }); }
                catch (Exception e) { problems.Add($"{type.FullName} threw: {e.GetBaseException().Message}"); }
            }
            // What Bindito.Unity's SceneConfigurator / ProjectConfigurator bind themselves.
            Type sceneProvider = Type.GetType("Bindito.Unity.ISceneProvider, Bindito.Unity", true)!;
            BindInstance(sceneProvider, NullProxy.Of(sceneProvider));
            object instantiator = definition.GetType().GetMethod("Bind")!.MakeGenericMethod(Type.GetType("Bindito.Unity.IInstantiator, Bindito.Unity", true)!).Invoke(definition, null)!;
            object scoped = instantiator.GetType().GetMethod("To")!.MakeGenericMethod(Type.GetType("Bindito.Unity.Instantiator, Bindito.Unity", true)!).Invoke(instantiator, null)!;
            scoped.GetType().GetMethod("AsSingleton")!.Invoke(scoped, null);
            // BindingBuilderRegistry.BuildAllBindings, one binding at a time so that every problem is seen.
            var singles = (System.Collections.IDictionary)registry.GetType().GetField("_boundBindingBuilders", All)!.GetValue(registry)!;
            foreach (System.Collections.DictionaryEntry entry in singles)
            {
                try { binder.GetType().GetMethod("Bind")!.Invoke(binder, new[] { entry.Key, entry.Value!.GetType().GetMethod("Build")!.Invoke(entry.Value, null) }); }
                catch (Exception e) { problems.Add("bind: " + e.GetBaseException().Message); }
            }
            var multis = (System.Collections.IDictionary)registry.GetType().GetField("_boundMultiBindingBuilders", All)!.GetValue(registry)!;
            foreach (System.Collections.DictionaryEntry entry in multis)
                foreach (object builder in (System.Collections.IEnumerable)entry.Value!)
                {
                    try { binder.GetType().GetMethod("MultiBind")!.Invoke(binder, new[] { entry.Key, builder.GetType().GetMethod("Build")!.Invoke(builder, null) }); }
                    catch (Exception e) { problems.Add("multibind: " + e.GetBaseException().Message); }
                }
            // ContainerCreator.ValidateConfiguration, every binding.
            object validator = Activator.CreateInstance(Core("Bindito.Core.Internal.BindingValidator"),
                Activator.CreateInstance(Core("Bindito.Core.Internal.BindingAnalyser"),
                    Activator.CreateInstance(Core("Bindito.Core.Internal.DependencyRetriever"),
                        Activator.CreateInstance(Core("Bindito.Core.Internal.ConstructorRetriever")), Activator.CreateInstance(Core("Bindito.Core.Internal.MethodRetriever"))),
                    Activator.CreateInstance(Core("Bindito.Core.Internal.BindingResolver"), Activator.CreateInstance(Core("Bindito.Core.Internal.MultiBindingService")), binder, parent)))!;
            MethodInfo validate = validator.GetType().GetMethod("Validate")!;
            void Validate(object key, object binding)
            {
                try { validate.Invoke(validator, new[] { key, binding.GetType().GetProperty("ProvisionBinding")!.GetValue(binding) }); }
                catch (Exception e) { problems.Add("validate: " + e.GetBaseException().Message); }
            }
            foreach (dynamic pair in (System.Collections.IEnumerable)binder.GetType().GetProperty("Bindings")!.GetValue(binder)!)
                Validate(pair.Key, pair.Value);
            foreach (dynamic pair in (System.Collections.IEnumerable)binder.GetType().GetProperty("MultiBindings")!.GetValue(binder)!)
                foreach (object binding in (System.Collections.IEnumerable)pair.Value) Validate(pair.Key, binding);
            return binder;
        }

        var bootProblems = new List<string>();
        object boot = Build(configurators.Where(c => !c.mine && c.contexts.Contains("Bootstrapper")).Select(c => c.type), null, bootProblems);
        test("Bindito: the Bootstrapper container builds and validates", () =>
        {
            if (bootProblems.Count > 0) throw new Exception(string.Join("; ", bootProblems));
        });
        foreach (var (name, context, coop) in new[] { ("Game (single-player)", "Game", false), ("Game (co-op)", "Game", true),
            ("MainMenu", "MainMenu", false), ("MapEditor", "MapEditor", false) })
        {
            test($"Bindito: the {name} container builds and validates with this mod's configurators, as the game builds it", () =>
            {
                object? io = coop ? NullProxy.Of(eventIO.FieldType) : null;
                var gameOnly = new List<string>();
                var withMod = new List<string>();
                try
                {
                    eventIO.SetValue(null, io);
                    Build(configurators.Where(c => !c.mine && c.contexts.Contains(context)).Select(c => c.type), boot, gameOnly);
                    eventIO.SetValue(null, io);
                    object built = Build(configurators.Where(c => c.contexts.Contains(context)).Select(c => c.type), boot, withMod);
                    int count = ((System.Collections.ICollection)built.GetType().GetProperty("Bindings")!.GetValue(built)!).Count;
                    if (count < (context == "MainMenu" ? 200 : 900)) throw new Exception($"only {count} bindings: the configurators were not all run");
                }
                finally { eventIO.SetValue(null, null); }
                if (!configurators.Any(c => c.mine && c.contexts.Contains(context))) throw new Exception("this mod has no configurator for " + context);
                var added = withMod.Where(p => !gameOnly.Contains(p)).Distinct().ToList();
                if (added.Count > 0) throw new Exception(string.Join("; ", added));
            });
        }
    }
}

/// <summary>An interface whose every method does nothing and returns its type's default (a logger, an EventIO, a container).</summary>
public class NullProxy : DispatchProxy
{
    public static object Of(Type type) => DispatchProxy.Create(type, typeof(NullProxy));
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Type r = targetMethod!.ReturnType;
        return r == typeof(void) || !r.IsValueType ? null : Activator.CreateInstance(r);
    }
}
