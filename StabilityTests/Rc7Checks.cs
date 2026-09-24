using BeaverBuddies.Connect;
using BeaverBuddies.Lobby;
using TimberNet;

/// <summary>
/// 1.4.0-rc7, hosting and joining from inside a running game (design/IN-GAME-HOSTING-PLAN.md): the Load game box's Host co-op
/// game, the room's window in a game, the join held until its save arrives, guests carried into the host's room, and the
/// exit saves at Start. One named check per behaviour; checks that need no game.
/// </summary>
static class Rc7Checks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }

    static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root)!;
        Check(root != null, "could not find the repository root");
        return root!;
    }

    static string Source(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    static bool Exists(params string[] parts) => File.Exists(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    /// <summary>The body of a method, from its signature to the matching closing brace.</summary>
    static string Body(string text, string signature)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        Check(start >= 0, "not found: " + signature);
        int open = text.IndexOf('{', start), depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text.Substring(start, i - start + 1);
        }
        throw new Exception("unbalanced braces after " + signature);
    }

    /// <summary>An English text, or null when the key is not in the file.</summary>
    static string? Csv(string key)
    {
        string csv = "\n" + Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
        int at = csv.IndexOf("\n" + key + ",\"", StringComparison.Ordinal);
        if (at < 0) return null;
        int start = at + key.Length + 3;
        int end = csv.IndexOf("\",\"", start, StringComparison.Ordinal);
        return csv.Substring(start, end - start);
    }

    /// <summary>Where <paramref name="first"/> is found in <paramref name="text"/>, checked to come before <paramref name="then"/>.</summary>
    static void InOrder(string text, string what, params string[] steps)
    {
        int at = -1;
        foreach (string step in steps)
        {
            int next = text.IndexOf(step, at + 1, StringComparison.Ordinal);
            Check(next > at, $"{what}: \"{step}\" is missing or out of order");
            at = next;
        }
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        // ---- The Load game box and the main menu (plan §3, §4.6) ----

        yield return ("rc7: the Load game box's Host co-op game is shown wherever a room can open, and in a game as the game menu's hosting button", () =>
        {
            Check(HostButtonRules.ShowOnLoadBox(true, HostButtonKind.HostCoopGame), "the main menu, or a game played alone, has no Host co-op game");
            Check(HostButtonRules.ShowOnLoadBox(true, HostButtonKind.SaveAndRehost), "the host of a co-op game can't host a save from the box");
            Check(!HostButtonRules.ShowOnLoadBox(true, HostButtonKind.Hidden), "a guest's game, or a game a failed action stopped, hosts from the box");
            Check(!HostButtonRules.ShowOnLoadBox(false, HostButtonKind.HostCoopGame), "a scene without the room's panel shows a button that does nothing");
            string hosting = Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs");
            string patch = Body(hosting, "public static void Postfix(LoadGameBox __instance, ref VisualElement __result)");
            Check(patch.Contains("DuplicateOrGetButton(__result, \"LoadButton\", LoadGameBoxHostButton.Name,"), "Host co-op game is not a copy of Load, right of it");
            Check(patch.Contains("GetSingleton<BeaverBuddies.Lobby.LobbyHostPanel>() != null") && patch.Contains("ClientConnectionUI.HostKind()"),
                "the button does not follow where a room can open and how the game was loaded");
            Check(Body(hosting, "private static void HostSelectedGame(LoadGameBox __instance)").Contains("ServerHostingUtils.LoadIfSaveValidAndHost("),
                "a save is hosted without the game's own save checks");
            // The box is the game's again: its title, and Load, Enter and a double-click load.
            Check(!hosting.Contains("\"LoadGame\")]") && !hosting.Contains("header.text"), "the box's title or its load is changed again");
            Check(Csv("BeaverBuddies.Saving.HostCoopGame") == "Host co-op game", "the button's label changed");
        });

        yield return ("rc7: four medium buttons fit the Load game box once it is 70 px wider; the game's buttons keep their size", () =>
        {
            Check(!LoadBoxFit.Fits(4, LoadBoxFit.Box), "the game's box fits four buttons now: the widening may go (RuntimeChecks reads UI.zip)");
            Check(LoadBoxFit.Fits(3, LoadBoxFit.Box), "the game's own three buttons no longer fit its box");
            Check(LoadBoxFit.Fits(4, LoadBoxFit.WidenedBox), "the widened box still does not fit four buttons");
            Check(LoadBoxFit.Inside(LoadBoxFit.WidenedBox) - 4 * LoadBoxFit.ButtonMinWidth >= 30, "no room is left between the four buttons");
            string patch = Body(Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs"), "public static void Postfix(LoadGameBox __instance, ref VisualElement __result)");
            Check(patch.Contains("Q(className: \"load-box\")") && patch.Contains("LoadBoxFit.WidenedBox") && patch.Contains("StyleKeyword.Null"),
                "the box is not widened while Host co-op game is there, or stays wide without it");
            Check(!patch.Contains("minWidth") && !patch.Contains("host.style.width"), "the buttons are made narrower instead");
        });

        yield return ("rc7: the gold line under the picture says what every selected save is, read off the game's thread, in both scenes", () =>
        {
            string line = Source("BeaverBuddies", "Connect", "LoadGameBoxColonies.cs");
            Check(line.Contains("AddToClassList(\"game-text-small\")") && line.Contains("AddToClassList(\"text--yellow\")"), "the line is not the save list's small gold text");
            Check(line.Contains("status.style.minHeight = 30;"), "the line no longer keeps its two lines (the list jumps as saves are read)");
            string show = Body(line, "public void Show(SaveReference save, GameSaveRepository repository)");
            Check(!show.Contains("HostMode") && !show.Contains("Instance"), "the line is shown only in one scene or mode again");
            Check(Body(line, "private void Poll()").Contains("if (readingKey == shownKey) SetText(info);"), "a late read overwrites the line of the save now selected");
            string selected = Body(Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs"), "public class LoadGameBoxSaveSelectedPatcher");
            Check(selected.Contains("LoadGameBoxColonies.Of(root)?.Show(") && selected.Contains("SetEnabled(selected)"),
                "a selected save no longer updates the line and Host co-op game");
            Check(!selected.Contains("return;") || !selected.Contains("HostMode"), "the selection is followed only in a mode");
            foreach (string key in new[] { "Shared", "Separate", "Separate.One", "SeparateMixed", "SeparateMixed.One" })
                Check(Csv("BeaverBuddies.Saving.Status." + key) != null, "no English line for BeaverBuddies.Saving.Status." + key);
        });

        yield return ("rc7: the main menu has Load game then Join co-op game; its Host co-op game box and band fit are gone", () =>
        {
            string ui = Source("BeaverBuddies", "Connect", "ClientConnectionUI.cs");
            string add = Body(ui, "public void AddJoinButton(VisualElement __result, bool mainMenu)");
            InOrder(add, "the menu's buttons", "if (!mainMenu)", "HostButtonName", "mainMenu ? \"LoadGameButton\" : HostButtonName, \"JoinButton\"");
            Check(!ui.Contains("OpenBox()") && !ui.Contains("FitMainMenu"), "the main menu's Host co-op game box or band fit is back");
        });

        // ---- The room in a game: the host's side (plan §3, §4.1, §4.2) ----

        yield return ("rc7: every game binds the host's room, what a mixed save's room offers, and the game's side of it, before the co-op return", () =>
        {
            string configure = Body(Source("BeaverBuddies", "Plugin.cs"), "public class ReplayConfigurator");
            int coopOnly = configure.IndexOf("if (EventIO.IsNull) return;", StringComparison.Ordinal);
            Check(coopOnly > 0, "the configurator changed shape");
            foreach (string bound in new[] { "Bind<BeaverBuddies.Lobby.LobbyHostPanel>()", "Bind<BeaverBuddies.Lobby.InGameLobby>()",
                "Bind<BeaverBuddies.Factions.NewGameFactionCapture>()" })
            {
                int at = configure.IndexOf(bound, StringComparison.Ordinal);
                Check(at > 0 && at < coopOnly, bound + " is not bound in every game (a game played alone could not host)");
            }
            // Made in a game now, the faction capture must leave the loaded game's factions alone; the main menu resets them.
            string capture = Source("BeaverBuddies", "Factions", "NewGameFactionCapture.cs");
            Check(!Body(capture, "public NewGameFactionCapture(FactionSpecService factionSpecService").Contains("MixedFactions.Reset()"),
                "making the capture in a game turns a mixed game's factions off");
            Check(Body(Source("BeaverBuddies", "Plugin.cs"), "public class ConnectionMenuConfigurator").Contains("MixedFactions.Reset();"),
                "the main menu no longer says that nothing is mixed");
        });

        yield return ("rc7: a save's room is a window over a game (the game's named box), the page in the main menu; it goes where it was opened from", () =>
        {
            Check(LobbyRules.HostRoomPush(0) == RoomPush.Push, "a room opened with nothing open (a desync's Save and Rehost) hides nothing");
            Check(LobbyRules.HostRoomPush(2) == RoomPush.HideAndPush, "a room opened from the Load game box or the game menu does not take its place");
            string host = Source("BeaverBuddies", "Lobby", "LobbyHostPanel.cs");
            string open = Body(host, "private void OpenRoom(LobbySetup setup)");
            Check(open.Contains("inGame != null ? LobbyFrame.Window : LobbyFrame.Page") && open.Contains("inGame.AttachStyles"),
                "the room does not pick its frame by the scene, or its window lacks the main menu's sheets");
            Check(open.Contains("if (page.Close != null) page.Close.clicked += OnUICancelled;"), "the window's close button is not its Cancel");
            Check(open.Contains("LobbyRules.HostRoomPush(_panelStack._stack.Count) == RoomPush.Push") && open.Contains("_panelStack.HideAndPush(this);"),
                "the room is not pushed by the rule");
            string page = Source("BeaverBuddies", "Lobby", "LobbyPage.cs");
            string ctor = Body(page, "public LobbyPage(VisualElementLoader loader");
            InOrder(ctor, "the window", "LoadVisualTreeAsset(\"Common/NamedBoxTemplate\").CloneTree()", "attachStyles?.Invoke(Root);", "Root.Add(box);",
                "_textLocKey = headerLocKey", "Close = box.Q<Button>(\"CloseButton\")", "buttons.AddToClassList(\"box-buttons\")",
                "initializer.InitializeVisualElement(Root);");
            Check(ctor.Contains("LoadVisualTreeAsset(\"MainMenu/NewGameTemplate\").CloneTree()"), "the main menu's page is no longer the wizard's page");
            Check(!ctor.Contains(".ElementAt("), "a template's content slot is taken with ElementAt again");
            Check(Body(page, "private static Button WindowButton(VisualElement row)").Contains("AddToClassList(\"menu-button--medium\")"),
                "the window's buttons are not the game's medium menu buttons");
            foreach (string cls in new[] { "content-row-centered", "box-buttons", "menu-button", "menu-button--medium" })
                Check(page.Contains("\"" + cls + "\"") && Body(page, "public static readonly string[] WindowClassesUsed").Contains("\"" + cls + "\""),
                    cls + " is used by the window but not listed for RuntimeChecks");
        });

        yield return ("rc7: the window never grows past the screen: its content is held to the screen's height and only the board shrinks", () =>
        {
            string page = Source("BeaverBuddies", "Lobby", "LobbyPage.cs");
            string ctor = Body(page, "public LobbyPage(VisualElementLoader loader");
            Check(ctor.Contains("Root.panel?.visualTree?.layout.height") && ctor.Contains("screen - WindowMargin") && ctor.Contains("content.style.maxHeight"),
                "the window's height is not held to the screen");
            Check(ctor.Contains("board.style.flexShrink = 1;") && ctor.Contains("element.style.flexShrink = 0;"), "something other than the board shrinks (its text would be cut)");
            Check(ctor.Contains("board.AddToClassList(\"scroll--green-decorated\")"), "the board does not scroll");
            Check(ctor.Contains("factionNote.style.maxWidth = 600;") && ctor.Contains("board.style.width = 600;"), "the page's sizes are not the window's");
        });

        yield return ("rc7: the game menu's Host co-op game and Save and Rehost save this game and open its room over it, marked as just saved", () =>
        {
            string rehosting = Source("BeaverBuddies", "Connect", "RehostingService.cs");
            Check(!rehosting.Contains("MainMenuSceneLoader") && !rehosting.Contains("OpenMainMenu"), "hosting from a game goes through the main menu again");
            Check(Body(rehosting, "private void HostSaved(SaveReference save, bool rehost)").Contains("LoadAndHost(_gameSaveRepository, save, savedForRoom: true)"),
                "the save just written is not hosted in this game, or not marked as just saved");
            Check(Body(rehosting, "public bool SaveRehostFile(").Contains("TimeoutUtils.RunAfterFrames("), "the save is read before its file is closed");
            string host = Source("BeaverBuddies", "Lobby", "LobbyHostPanel.cs");
            Check(Body(host, "public void OpenForSave(SaveReference save, byte[] bytes, bool savedForRoom = false)").Contains("SavedForRoom = savedForRoom,"),
                "the room does not know its game was just saved (K3)");
            Check(Csv("BeaverBuddies.Host.FromGame.Confirm")!.Contains("room opens over it") && !Csv("BeaverBuddies.Host.FromGame.Confirm")!.Contains("main menu"),
                "the game menu's question still sends the player to the main menu");
        });

        yield return ("rc7: a save's room opens in either scene, only once the session this game had has ended (the room's server needs the port)", () =>
        {
            string hosting = Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs");
            string load = Body(hosting, "public static void LoadAndHost(GameSaveRepository repository, SaveReference saveReference, bool savedForRoom)");
            Check(!load.Contains("only from the main menu"), "a game still refuses to host");
            InOrder(load, "hosting", "GetSingleton<BeaverBuddies.Lobby.LobbyHostPanel>()", "EndSessionForRoom();", "waitingRoom.OpenForSave(saveReference, data, savedForRoom);");
            string end = Body(hosting, "internal static void EndSessionForRoom()");
            InOrder(end, "ending the session", "GetSingleton<ReplayService>()?.EndSession(null);", "EventIO.Reset();");
            Check(Body(hosting, "private static void CheckNextValidator(").Contains("LoadAndHost(loader._gameSceneLoader._gameSaveRepository, saveReference, savedForRoom: false)"),
                "a save picked in the Load game box skips the game's checks, or is taken for one just saved");
        });

        yield return ("rc7: a window in a game gets the four main-menu sheets its classes come from, loaded once by the game's asset loader", () =>
        {
            string lobby = Source("BeaverBuddies", "Lobby", "InGameLobby.cs");
            foreach (string sheet in new[] { "Options/OptionsStyle", "MainMenu/MainMenuStyle", "MainMenu/MainMenuMiscStyle", "Modding/ModdingStyle" })
                Check(lobby.Contains("\"UI/Views/" + sheet + "\""), "the window does not get " + sheet);
            string attach = Body(lobby, "public void AttachStyles(VisualElement root)");
            Check(attach.Contains("_assetLoader.Load<StyleSheet>(SheetPaths[i])") && attach.Contains("if (sheets == null)"), "the sheets are not loaded once, by the game's loader");
            Check(attach.Contains("!root.styleSheets.Contains(sheet)"), "a sheet is added twice");
            Check(lobby.Contains("public static bool InGame => Current != null;"), "the scene test changed");
        });
    }
}
