#nullable enable
using System.Reflection;

// What 1.4.0-beta16 changed after playing beta15, against the compiled mod: another mod's per-building event (MixedStorage's
// warehouse and pile settings) is judged as a change to that building, the trading posts window moves by its title,
// a Trading Post's header leaves room for its All Posts button above the scrolling part, and Ctrl+L draws each colony's
// roads as filled squares in strong colours.
internal static class PlaytestFixChecks
{
    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const BindingFlags declared = all | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        IEnumerable<MethodBase> Methods(Type type) => new[] { type }.Concat(type.GetNestedTypes(all))
            .SelectMany(t => t.GetMethods(declared).Cast<MethodBase>().Concat(t.GetConstructors(declared)))
            .Where(m => m.GetMethodBody() != null);
        bool Calls(IEnumerable<MethodBase> methods, string type, string name) =>
            methods.Any(m => IlScan.Instructions(m).Any(i => i.Calls && i.Is(type, name)));

        test("Other mods: an event without a colony scope that names a building in entityID is judged as a change to it", () =>
        {
            Type rules = mod.GetType("BeaverBuddies.Colonies.ColonyRulesService", true)!;
            MethodInfo judge = rules.GetMethods(all).Single(m => m.Name == "Judge" && m.GetParameters().Length == 3 && !m.IsStatic);
            if (!Calls(new[] { judge }, "BeaverBuddies.Colonies.ColonyRules", "ScopeByEntityField"))
                throw new Exception("the host still allows another mod's per-building event without looking at the building");
            // The host (AllowOnHost) and the player's own computer (RefuseLocally) both come through that Judge.
            foreach (string caller in new[] { "AllowOnHost", "RefuseLocally" })
                if (!IlScan.Instructions(rules.GetMethod(caller, all)!).Any(i => i.Calls && i.Member == judge))
                    throw new Exception(caller + " no longer judges through it");
        });

        test("Trading posts window: dragged by its title and frame, and kept on the screen", () =>
        {
            Type panel = mod.GetType("BeaverBuddies.Colonies.TradeOverviewPanel", true)!;
            var methods = Methods(panel).ToList();
            if (!Calls(methods, "UnityEngine.UIElements.PointerCaptureHelper", "CapturePointer"))
                throw new Exception("the box does not take hold of the pointer while dragged");
            if (!Calls(methods, "UnityEngine.UIElements.IStyle", "set_translate"))
                throw new Exception("the box is not moved");
            if (!Calls(new[] { panel.GetMethod("PlaceBox", all)! }, "UnityEngine.Mathf", "Clamp"))
                throw new Exception("the box can be dragged off the screen");
            if (!Calls(new[] { panel.GetMethod("Build", all)! }, "BeaverBuddies.Colonies.TradeOverviewPanel", "MakeDraggable"))
                throw new Exception("the box is built without its grips");
        });

        test("Trading Post panel: the header row has room for the All Posts button above the scrolling part", () =>
        {
            Type fragment = mod.GetType("BeaverBuddies.Colonies.TradingPostFragment", true)!;
            var build = new[] { fragment.GetMethod("InitializeFragment", all)! };
            if (!Calls(build, "UnityEngine.UIElements.IStyle", "set_minHeight") || !Calls(build, "UnityEngine.UIElements.IStyle", "set_marginBottom"))
                throw new Exception("the header row sets no height or gap of its own");
        });

        test("Colony roads (Ctrl+L): filled squares from the game's own tile marker, in strong colours", () =>
        {
            // What the overlay borrows from the game: the marker's mesh and material, and the area drawer that builds a
            // mesh of them once per change.
            var rendering = Assembly.Load("Timberborn.Rendering");
            Type factory = rendering.GetType("Timberborn.Rendering.MarkerDrawerFactory", true)!;
            Type spec = factory.GetField("_markerDrawerFactorySpec", all)?.FieldType ?? throw new Exception("the game's marker spec field is gone");
            foreach (string asset in new[] { "TileMesh", "TileMaterial" })
                if (spec.GetProperty(asset, all) == null) throw new Exception("the game's marker has no " + asset);
            Type drawer = rendering.GetType("Timberborn.Rendering.AreaTileDrawer", true)!;
            if (!drawer.GetConstructors().Any(c => c.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(new[] { "Mesh", "Material", "Vector2Int", "GameObject" })))
                throw new Exception("the game's area drawer can no longer be made with another mesh");
            Type overlay = mod.GetType("BeaverBuddies.Colonies.ColonyRoadOverlay", true)!;
            if (!Methods(overlay).Any(m => IlScan.Instructions(m).Any(i => i.Op == System.Reflection.Emit.OpCodes.Newobj
                    && i.Is("Timberborn.Rendering.AreaTileDrawer", ".ctor"))))
                throw new Exception("the overlay does not draw filled squares");
            // Strong colours: the colonies' own (lightened for text) read as nothing on a beige path.
            var colors = (Array)overlay.GetNestedType("Palette", all)!.GetField("Roads", all)!.GetValue(null)!;
            foreach (object color in colors)
            {
                float r = (float)color.GetType().GetField("r")!.GetValue(color)!, g = (float)color.GetType().GetField("g")!.GetValue(color)!,
                    b = (float)color.GetType().GetField("b")!.GetValue(color)!;
                if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 0.5f) throw new Exception($"a road colour is washed out: {r}, {g}, {b}");
            }
        });
    }
}
