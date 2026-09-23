#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

// The review of 1.4.0-rc2 to rc4 (design/review-1.4.0-rc4/), checks against the compiled mod and the installed game's
// assemblies and UI files. One check per finding, named after it.
internal static class Rc5RuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, string managedPath, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        Type Mod(string type) => mod.GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");
        string Ui(string entry)
        {
            using ZipArchive ui = ZipFile.OpenRead(Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip")));
            using var reader = new StreamReader((ui.GetEntry(entry) ?? throw new Exception("UI.zip has no " + entry)).Open());
            return reader.ReadToEnd();
        }
        // A property of a USS rule, in px (the first rule with exactly this selector).
        float Px(string sheet, string selector, string property)
        {
            Match rule = Regex.Match(sheet, @"(^|\n)" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
            if (!rule.Success) throw new Exception("no rule " + selector);
            Match value = Regex.Match(rule.Groups[2].Value, @"(^|[\s;])" + Regex.Escape(property) + @":\s*(\d+)px");
            if (!value.Success) throw new Exception(selector + " has no " + property + " in px");
            return float.Parse(value.Groups[2].Value);
        }

        test("A1, A7: every game has the rehosting service (Host co-op game alone), and a game clears the Host co-op game box's mode", () =>
        {
            var configure = IlScan.Instructions(Only(Mod("BeaverBuddies.ReplayConfigurator"), "Configure"));
            int coopOnly = configure.FindIndex(i => i.Calls && i.Member?.Name == "get_IsNull");
            int bind = configure.FindIndex(i => i.Calls && i.Member is MethodInfo m && m.Name == "Bind" && m.IsGenericMethod
                && m.GetGenericArguments()[0].Name == "RehostingService");
            int reset = configure.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Connect.HostCoopMenu", "BoxClosed"));
            if (coopOnly < 0) throw new Exception("the configurator no longer tells co-op games apart");
            if (bind < 0 || bind > coopOnly) throw new Exception("RehostingService is bound only in co-op: a game played alone has no Host co-op game");
            if (reset < 0 || reset > coopOnly) throw new Exception("a game keeps the Host co-op game box's mode: its Load game hosts, and does nothing");
            if (configure.Count(i => i.Calls && i.Member is MethodInfo m && m.Name == "Bind" && m.IsGenericMethod
                && m.GetGenericArguments()[0].Name == "RehostingService") != 1) throw new Exception("RehostingService is bound twice");
        });

        test("A2: a closed Connecting box's Cancel still takes it away", () =>
        {
            var cancel = IlScan.Instructions(Only(Mod("BeaverBuddies.Lobby.ConnectingBox"), "OnUICancelled"));
            if (!cancel.Any(i => i.Calls && i.Is("BeaverBuddies.Lobby.ConnectingBox", "Poll"))) throw new Exception("a closed box's Cancel does nothing");
        });

        test("A8: the game menu's hosting button follows how the game was loaded; the old in-menu reconnect is gone", () =>
        {
            Type ui = Mod("BeaverBuddies.Connect.ClientConnectionUI");
            var dress = IlScan.Instructions(Only(ui, "DressHostInGame"));
            if (dress.Any(i => i.Calls && i.Member?.Name == "get_IsNull")) throw new Exception("a session that ended still reads as playing alone");
            if (!IlScan.Instructions(Only(ui, "HostKind")).Any(i => i.Calls && i.Member?.Name == "get_LoadedAsHost"))
                throw new Exception("the button no longer asks how the game was loaded");
            Type rules = Mod("BeaverBuddies.Connect.HostButtonRules");
            object? Decide(bool coop, bool host, bool failed) => rules.GetMethod("Decide")!.Invoke(null, new object[] { coop, host, failed });
            if (Decide(true, false, false)?.ToString() != "Hidden") throw new Exception("a guest whose session ended is offered Host co-op game");
            if (Decide(false, false, false)?.ToString() != "HostCoopGame") throw new Exception("a game played alone has no Host co-op game");
            if (Decide(true, true, false)?.ToString() != "SaveAndRehost") throw new Exception("the host is not offered Save and Rehost");
            if (Mod("BeaverBuddies.ReplayService").GetProperty("LoadedAsHost") == null) throw new Exception("ReplayService.LoadedAsHost is gone");
        });

        test("A11, B7: rc4's dead code is gone: the start prompt, the in-menu reconnect, the rehost flag, the unused loader", () =>
        {
            foreach (string gone in new[] { "BeaverBuddies.Colonies.HostStartGate", "BeaverBuddies.Colonies.HostStartRules" })
                if (mod.GetType(gone) != null) throw new Exception(gone + " is back, and no host can reach it");
            Type service = Mod("BeaverBuddies.Connect.ClientConnectionService");
            foreach (string gone in new[] { "ReconnectNow", "ShowWaitForSteamInvite" })
                if (service.GetMethod(gone, All) != null) throw new Exception(gone + " is back, and nothing reaches it");
            if (Mod("BeaverBuddies.Connect.HostCoopFlow").GetProperty("PendingRehost", All) != null) throw new Exception("PendingRehost is back (nothing reads it)");
            if (Mod("BeaverBuddies.Connect.RehostingService").GetConstructors().Single().GetParameters().Any(p => p.ParameterType.Name == "ValidatingGameLoader"))
                throw new Exception("RehostingService asks for a loader it never uses");
        });

        test("C3: the game's main menu band has no room for two more buttons, so the mod fits the band to its panel", () =>
        {
            string panel = Ui("Views/MainMenu/MainMenuPanel.uxml");
            string misc = Ui("Views/MainMenu/MainMenuMiscStyle.uss").Replace("\r\n", "\n");
            string core = Ui("Views/Core/CoreStyle.uss").Replace("\r\n", "\n");
            int buttons = Regex.Matches(panel, "class=\"menu-button menu-button--stretched\"").Count;
            float band = Px(misc, ".main-menu__content", "height"), logo = Px(misc, ".background__logo", "height");
            float row = Px(core, ".menu-button", "height"), padding = Px(misc, ".main-menu-panel", "padding");
            float discord = Px(misc, ".main-menu-panel__discord-button", "height") + Px(misc, ".main-menu-panel__discord-button", "margin-top");
            float game = 2 * padding + buttons * row + discord, withMod = game + 2 * row;
            if (buttons != 10 || band != 720 || logo != 104 || row != 44) throw new Exception($"the main menu changed: {buttons} buttons, band {band}, logo {logo}, rows {row}");
            if (withMod <= band - logo) throw new Exception($"the panel ({withMod} px) fits the band's {band - logo} px now: the fit may be dropped");
            Type ui = Mod("BeaverBuddies.Connect.ClientConnectionUI");
            if (!IlScan.Instructions(Only(ui, "AddJoinButton")).Any(i => i.Calls && i.Member?.Name == "FitMainMenu"))
                throw new Exception($"the panel ({withMod} px) hangs over the band's {band - logo} px: the menu no longer fits it");
            var fit = IlScan.Instructions(Only(ui, "FitMainMenu"));
            foreach (string name in new[] { "main-menu__content", "background__logo", "MainMenuPanel" })
                if (!fit.Any(i => i.Text == name)) throw new Exception("the fit no longer finds " + name);
            if (!panel.Contains("name=\"MainMenuPanel\"") || !Ui("Views/MainMenu/Background/LogoBackground.uxml").Contains("background__logo"))
                throw new Exception("the main menu's panel or logo band is renamed");
        });

        test("C8: the mod's buttons are initialised as the game's (NineSliceButton, the scene's initializer); the game's members it reads are there", () =>
        {
            var insert = IlScan.Instructions(Only(Mod("BeaverBuddies.Connect.ButtonInserter"), "DuplicateOrGetButton"));
            if (!insert.Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "NineSliceButton")) throw new Exception("the added button is not a NineSliceButton");
            if (insert.Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "LocalizableButton"))
                throw new Exception("the added button is a keyless LocalizableButton, which the game's initializer refuses");
            if (!insert.Any(i => i.Calls && i.Member?.Name == "InitializeVisualElement")) throw new Exception("the added button gets no click sound");
            Type loader = Game("Timberborn.CoreUI", "Timberborn.CoreUI.VisualElementLoader");
            if (loader.GetField("_visualElementInitializer", All)?.FieldType.Name != "VisualElementInitializer") throw new Exception("VisualElementLoader has no initializer");
            foreach (var (assembly, type) in new[] { ("Timberborn.GameSaveRepositorySystemUI", "Timberborn.GameSaveRepositorySystemUI.LoadGameBox"),
                ("Timberborn.OptionsGame", "Timberborn.OptionsGame.GameOptionsBox"), ("Timberborn.MainMenuPanels", "Timberborn.MainMenuPanels.NewGameModePanel") })
                if (Game(assembly, type).GetField("_visualElementLoader", All)?.FieldType != loader) throw new Exception(type + " has no _visualElementLoader");
            if (Game("Timberborn.CoreUI", "Timberborn.CoreUI.NineSliceButton").GetConstructor(Type.EmptyTypes) == null) throw new Exception("NineSliceButton can't be made");
            if (!Ui("Views/Core/CoreStyle.uss").Contains("--click-sound: \"UI.Click\"")) throw new Exception("the menu buttons have no click sound to copy");
            Type sound = Game("Timberborn.CoreUI", "Timberborn.CoreUI.UISoundInitializer");
            if (!IlScan.Instructions(Only(sound, "InitializeVisualElement")).Any(i => i.Calls && i.Member?.Name == "RegisterCallback"))
                throw new Exception("the game's click sound no longer comes from its initializer");
        });

        test("C9: the Game Mode page's custom difficulty shows its own Tutorial row in a settings list; the mod follows both mode buttons", () =>
        {
            Type page = Game("Timberborn.MainMenuPanels", "Timberborn.MainMenuPanels.NewGameModePanel");
            var customize = IlScan.Instructions(page.GetMethod("OnCustomizeButtonClicked", All, Type.EmptyTypes) ?? throw new Exception("OnCustomizeButtonClicked is gone"));
            MethodInfo predefined = page.GetMethods(All).Single(m => m.Name == "OnPredefinedModeButtonClicked");
            if (!customize.Any(i => i.Calls && i.Member?.Name == "HideMainToggle") || !IlScan.Instructions(predefined).Any(i => i.Calls && i.Member?.Name == "ShowMainToggle"))
                throw new Exception("the mode buttons no longer swap the Tutorial rows");
            string layout = Ui("Views/MainMenu/NewGameModePanel.uxml");
            int list = layout.IndexOf("name=\"CustomModeSettings\"", StringComparison.Ordinal);
            int tutorial = layout.IndexOf("name=\"TutorialToggleCustomWrapper\"", StringComparison.Ordinal);
            int adults = layout.IndexOf("name=\"StartingAdultsWrapper\"", StringComparison.Ordinal);
            if (list < 0 || tutorial < list || adults < tutorial) throw new Exception("the custom list no longer starts with its Tutorial row");
            foreach (string patcher in new[] { "BeaverBuddies.Lobby.NewGameModePanelCustomizePatcher", "BeaverBuddies.Lobby.NewGameModePanelPredefinedPatcher" })
                if (!IlScan.Instructions(Only(Mod(patcher), "Postfix")).Any(i => i.Calls && i.Member?.Name == "ModeChanged"))
                    throw new Exception(patcher + " no longer moves the colony checkboxes");
        });
    }
}
