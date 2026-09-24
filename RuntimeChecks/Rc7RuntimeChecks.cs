#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

// 1.4.0-rc7, hosting and joining from inside a running game (design/IN-GAME-HOSTING-PLAN.md), checks against the compiled
// mod and the installed game's assemblies and UI files: the game members the new code uses, the UI.zip numbers the Load
// game box's widening rests on, the style sheets the room's window gets in a game, the order of the key steps in the IL,
// and what the game context binds.
internal static class Rc7RuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, string managedPath, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        Type Mod(string type) => mod.GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");
        void Has(Type type, string member, string why)
        {
            if (type.GetMember(member, All).Length == 0) throw new Exception($"{type.FullName}.{member} is gone ({why})");
        }
        ZipArchive OpenUi() => ZipFile.OpenRead(Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip")));
        string Read(ZipArchive ui, string entry)
        {
            using var reader = new StreamReader((ui.GetEntry(entry) ?? throw new Exception("UI.zip has no " + entry)).Open());
            return reader.ReadToEnd().Replace("\r\n", "\n");
        }
        // The value in px of a property of the first rule with exactly this selector, in whichever sheet has it.
        (float Value, string Sheet) Px(ZipArchive ui, string selector, string property)
        {
            foreach (ZipArchiveEntry entry in ui.Entries.Where(e => e.FullName.EndsWith(".uss", StringComparison.OrdinalIgnoreCase)))
            {
                Match rule = Regex.Match(Read(ui, entry.FullName), @"(^|\n)" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
                if (!rule.Success) continue;
                Match value = Regex.Match(rule.Groups[2].Value, @"(^|[\s;])" + Regex.Escape(property) + @":\s*(\d+)px");
                if (value.Success) return (float.Parse(value.Groups[2].Value), entry.FullName);
            }
            throw new Exception($"no sheet in UI.zip gives {selector} a {property} in px");
        }
        HashSet<string> Classes(ZipArchive ui, IEnumerable<string> sheets)
        {
            var defined = new HashSet<string>();
            foreach (string sheet in sheets)
                foreach (Match m in Regex.Matches(Read(ui, sheet), "\\.([A-Za-z_][A-Za-z0-9_-]*)")) defined.Add(m.Groups[1].Value);
            return defined;
        }
        string[] Strings(string type, string field) => (string[])Mod(type).GetField(field, All)!.GetValue(null)!;

        // ---- The Load game box (plan §3) ----

        test("rc7: the Load game box's sizes are what its widening for Host co-op game rests on (UI.zip)", () =>
        {
            using ZipArchive ui = OpenUi();
            var (box, boxSheet) = Px(ui, ".load-box", "width");
            var (padding, paddingSheet) = Px(ui, ".box__content-container", "padding");
            var (button, buttonSheet) = Px(ui, ".menu-button--medium", "min-width");
            Type fit = Mod("BeaverBuddies.Connect.LoadBoxFit");
            float F(string name) => (float)fit.GetField(name, All)!.GetValue(null)!;
            if (box != F("Box") || padding != F("Padding") || button != F("ButtonMinWidth"))
                throw new Exception($"the game's box is {box} px ({boxSheet}), its padding {padding} px ({paddingSheet}), a medium button " +
                    $"{button} px ({buttonSheet}); LoadBoxFit assumes {F("Box")}, {F("Padding")}, {F("ButtonMinWidth")}");
            if (4 * button <= box - 2 * padding) throw new Exception("four medium buttons fit the game's box now: the widening may go");
            if (4 * button > box + F("Extra") - 2 * padding) throw new Exception("four medium buttons do not fit the widened box");
            string layout = Read(ui, "Views/Options/LoadGameBox.uxml");
            foreach (string name in new[] { "\"LoadButton\"", "\"SavesWrapper\"", "load-box", "box-buttons" })
                if (!layout.Contains(name)) throw new Exception("the Load game box's layout has no " + name);
        });

        test("rc7: the Load game box's members the mod reads and patches are there, and it no longer patches LoadGame", () =>
        {
            Type box = Game("Timberborn.GameSaveRepositorySystemUI", "Timberborn.GameSaveRepositorySystemUI.LoadGameBox");
            foreach (string member in new[] { "GetPanel", "OnSaveSelectionChanged", "_saveList", "_loc", "_validatingGameLoader", "_dialogBoxShower",
                "_gameSaveRepository", "_visualElementLoader" })
                Has(box, member, "the Load game box's Host co-op game and gold line");
            foreach (string gone in new[] { "BeaverBuddies.Connect.LoadGameBoxLoadGamePatcher", "BeaverBuddies.Connect.LoadGameBoxClosedPatcher" })
                if (mod.GetType(gone) != null) throw new Exception(gone + " is back: the box has a host mode again");
            if (mod.GetType("BeaverBuddies.Connect.LoadGameBoxColonies") == null) throw new Exception("the box's gold line is gone");
        });

        // ---- The room's window in a game (plan §3, §4.1) ----

        test("rc7: every class the room's window uses is in CoreStyle, CommonStyle or a main-menu sheet it adds to its root", () =>
        {
            using ZipArchive ui = OpenUi();
            // The main menu's sheets, as its title screen loads them.
            var titleSheets = Regex.Matches(Read(ui, "Views/MainMenu/TitleScreen.uxml"), "Style src=\"/Assets/Resources/UI/(Views/[^\"]+\\.uss)\"")
                .Select(m => m.Groups[1].Value).ToList();
            string ByName(string file) => titleSheets.FirstOrDefault(s => s.EndsWith("/" + file, StringComparison.Ordinal))
                ?? throw new Exception(file + " is not among the title screen's sheets: " + string.Join(", ", titleSheets));
            // What a game scene has: CoreStyle and CommonStyle (plan §3), and what the window adds.
            var attached = Strings("BeaverBuddies.Lobby.InGameLobby", "SheetPaths").Select(p => p.StartsWith("UI/") ? p.Substring(3) + ".uss" : p).ToList();
            foreach (string sheet in attached)
                if (!titleSheets.Contains(sheet)) throw new Exception($"the window adds {sheet}, which is not a main-menu sheet: " + string.Join(", ", titleSheets));
            var defined = Classes(ui, attached.Concat(new[] { ByName("CoreStyle.uss"), ByName("CommonStyle.uss") }));
            var used = Strings("BeaverBuddies.Lobby.LobbyPage", "ClassesUsed").Concat(Strings("BeaverBuddies.Lobby.LobbyPage", "WindowClassesUsed"))
                .Concat(Strings("BeaverBuddies.Lobby.LobbyFactionPicker", "ClassesUsed")).Concat(Strings("BeaverBuddies.Lobby.NewGameColonyOptions", "ClassesUsed"))
                .Distinct().ToList();
            var missing = used.Where(c => !defined.Contains(c)).ToList();
            if (missing.Count > 0) throw new Exception("the window uses classes no sheet it has defines: " + string.Join(", ", missing));
            // The rows are the Mods window's (Modding/ModItem): its own classes must be there too.
            var row = Regex.Matches(Read(ui, "Views/Modding/ModItem.uxml"), "class=\"([^\"]+)\"").SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct();
            var rowMissing = row.Where(c => !defined.Contains(c)).ToList();
            if (rowMissing.Count > 0) throw new Exception("a player's row has classes the window's sheets do not define: " + string.Join(", ", rowMissing));
        });

        test("rc7: the window is the game's named box: its title, close button and content slot are where the window looks", () =>
        {
            using ZipArchive ui = OpenUi();
            string named = Read(ui, "Views/Common/NamedBoxTemplate.uxml");
            foreach (string part in new[] { "name=\"Header\"", "name=\"CloseButton\"", "box__content-container", "content-container=\"true\"" })
                if (!named.Contains(part)) throw new Exception("Common/NamedBoxTemplate has no " + part);
            Type panelStack = Game("Timberborn.CoreUI", "Timberborn.CoreUI.PanelStack");
            foreach (string member in new[] { "Push", "HideAndPush", "Pop", "IsPanelOnTop", "_stack", "TopPanel" })
                Has(panelStack, member, "the room's window over a game");
            Type loader = Game("Timberborn.AssetSystem", "Timberborn.AssetSystem.IAssetLoader");
            if (!loader.GetMethods().Any(m => m.Name == "Load" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string)))
                throw new Exception("IAssetLoader.Load<T>(string) is gone (the window's style sheets)");
            if (Mod("BeaverBuddies.Lobby.InGameLobby").GetConstructors().Single().GetParameters().Single().ParameterType != loader)
                throw new Exception("the game side of the room no longer takes only the asset loader");
        });

        test("rc7: every game binds the host's room, its game side and the faction capture before the co-op return", () =>
        {
            var configure = IlScan.Instructions(Only(Mod("BeaverBuddies.ReplayConfigurator"), "Configure"));
            int coopOnly = configure.FindIndex(i => i.Calls && i.Member?.Name == "get_IsNull");
            if (coopOnly < 0) throw new Exception("the configurator no longer tells co-op games apart");
            foreach (string bound in new[] { "LobbyHostPanel", "InGameLobby", "NewGameFactionCapture" })
            {
                int at = configure.FindIndex(i => i.Calls && i.Member is MethodInfo m && m.Name == "Bind" && m.IsGenericMethod
                    && m.GetGenericArguments()[0].Name == bound);
                if (at < 0 || at > coopOnly) throw new Exception(bound + " is not bound in every game");
            }
            var capture = Mod("BeaverBuddies.Factions.NewGameFactionCapture").GetConstructors().Single();
            if (IlScan.Instructions(capture).Any(i => i.Calls && i.Member?.Name == "Reset" && i.Member.DeclaringType?.Name == "MixedFactions"))
                throw new Exception("making the faction capture in a game turns the loaded game's mixed factions off");
            if (!IlScan.Instructions(Only(Mod("BeaverBuddies.ConnectionMenuConfigurator"), "Configure"))
                .Any(i => i.Calls && i.Member?.Name == "Reset" && i.Member.DeclaringType?.Name == "MixedFactions"))
                throw new Exception("the main menu no longer resets mixed factions");
        });

        test("rc7: a game hosts itself in place: the save it just wrote goes to its room, and nothing goes to the main menu", () =>
        {
            Type rehosting = Mod("BeaverBuddies.Connect.RehostingService");
            var saved = IlScan.Instructions(Only(rehosting, "HostSaved"));
            if (!saved.Any(i => i.Calls && i.Is("BeaverBuddies.Connect.ServerHostingUtils", "LoadAndHost"))) throw new Exception("the saved game is not hosted in its room");
            if (rehosting.GetConstructors().Single().GetParameters().Any(p => p.ParameterType.Name == "MainMenuSceneLoader"))
                throw new Exception("RehostingService still asks for the main menu's loader");
            var load = IlScan.Instructions(Only(Mod("BeaverBuddies.Connect.ServerHostingUtils"), "LoadAndHost"));
            int end = load.FindIndex(i => i.Calls && i.Member?.Name == "EndSessionForRoom");
            int open = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Lobby.LobbyHostPanel", "OpenForSave"));
            if (end < 0 || open < end) throw new Exception("the room opens before this game's session has ended (its server holds the port)");
        });
    }
}
