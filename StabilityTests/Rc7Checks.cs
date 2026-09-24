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

        // ---- Joining from a game: the held join (plan §4.3, flow D) ----

        yield return ("rc7: a join made in a game is held apart from it, and becomes the game's EventIO only when its save arrives, before the reset", () =>
        {
            Check(JoinFlowRules.HoldJoin(inGame: true) && !JoinFlowRules.HoldJoin(inGame: false), "a game's join is installed at once, or the main menu's held");
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            string connect = Body(service, "private bool TryToConnect(ISocketStream socket)");
            InOrder(connect, "a join", "EndJoin(client);", "ClientEventIO.Create(socket, bytes => LoadMap(bytes, joining),", "client = joining;",
                "if (JoinFlowRules.HoldJoin(InGameLobby.InGame))", "client.HeldJoin = true;", "return true;", "EventIO.Set(client);");
            string load = Body(service, "private void LoadMap(byte[] mapBytes, ClientEventIO joined)");
            InOrder(load, "the save's arrival", "saveReceived = true;", "if (joined != null && joined.HeldJoin)", "joined.HeldJoin = false;",
                "EventIO.Set(joined);", "SingletonManager.Reset();");
            // Nothing else in the mod installs a join: the running game stays single-player while its player waits in a room.
            foreach (string file in Directory.GetFiles(Path.Combine(Root(), "BeaverBuddies"), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("EventIO.Set(client)") || text.Contains("EventIO.Set(joined)"))
                    Check(Path.GetFileName(file) == "ClientConnectionService.cs", Path.GetFileName(file) + " installs a join");
            }
        });

        yield return ("rc7: every place that took a guest's join for EventIO follows the held join: errors, the room, Leave, Cancel, the rejoin", () =>
        {
            // The error planner: a held join is not the session, but its error is still the join's to report.
            Check(ConnectionErrorPlanner.Decide(false, true, false, false) == ConnectionErrorPlan.ReportWhileJoining, "a held join's failure says nothing");
            Check(ConnectionErrorPlanner.Decide(false, false, false, false) == ConnectionErrorPlan.Ignore, "a replaced join still reports");
            Check(ConnectionErrorPlanner.Decide(true, false, false, false) == ConnectionErrorPlanner.Decide(true, false, false),
                "the three-argument rule no longer means a join that is not held");
            string io = Source("BeaverBuddies", "IO", "ClientEventIO.cs");
            Check(Body(io, "private void OnConnectionError(string error, Action<string, TimberClient> onError)").Contains("ConnectionErrorPlanner.Decide(isCurrent, HeldJoin, mapDelivered,"),
                "the join's error ignores that it is held");
            Check(io.Contains("if (!HeldJoin) SingletonManager.GetSingleton<ReplayService>()?.AbortReplay("), "a held join can stop the game in this scene");
            // The guest's room reads the service's join, and ends it through the service.
            string guest = Source("BeaverBuddies", "Lobby", "LobbyGuestPanel.cs");
            Check(!guest.Contains("EventIO.Get()") && !guest.Contains("EventIO.ResetIf"), "the guest's room still takes the join for EventIO");
            Check(Body(guest, "private void Pump()").Contains("_clientConnectionService.JoinUnderWay") && Body(guest, "private void Pump()").Contains("_clientConnectionService.CurrentJoin"),
                "the guest's room does not watch the join under way");
            Check(Body(guest, "private void Leave()").Contains("_clientConnectionService.EndJoin(leaving);"), "Leave does not end a held join");
            // The service: Cancel, stopping a rejoin, and a new join end the old one, installed or held.
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            Check(!service.Contains("EventIO.ResetIf("), "the service still ends a join only if it is installed");
            string end = Body(service, "public void EndJoin(ClientEventIO join)");
            InOrder(end, "ending a join", "if (ReferenceEquals(client, join)) client = null;", "join.HeldJoin = false;",
                "if (ReferenceEquals(EventIO.Get(), join)) EventIO.Reset();", "else join.Close();");
            Check(Body(service, "public void ShowConnecting(string host)").Contains("EndJoin(joining);"), "Cancel leaves a held join connected");
            Check(Body(service, "private void StopRejoin(bool resetJoin)").Contains("EndJoin(client);"), "stopping a rejoin leaves its held try connected");
            Check(Body(service, "public ClientEventIO JoinUnderWay").Contains("!saveReceived"), "the room reopens once the save has come");
            // Hosting instead ends a join under way.
            Check(Body(Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs"), "internal static void EndSessionForRoom()").Contains("joins?.EndJoin(joins.CurrentJoin);"),
                "a held join stays connected when its player hosts instead");
        });

        yield return ("rc7: a guest's room is a window over a game, never over an overlay, and its close button is Leave", () =>
        {
            Check(JoinFlowRules.CheckWaitingRoom(true, true, false, inGame: true) == WaitingRoomStep.ShowWindow, "a room joined in a game is left (D20)");
            Check(LobbyRules.GuestRoomPush(true, 0, false) == RoomPush.Push, "a room welcomed while playing hides nothing");
            Check(LobbyRules.GuestRoomPush(true, 1, false) == RoomPush.HideAndPush, "a room joined from the game menu does not take its place");
            Check(LobbyRules.GuestRoomPush(true, 1, true) == RoomPush.Wait && LobbyRules.GuestRoomPush(false, 1, true) == RoomPush.Wait,
                "a room opens over the Steam overlay's input blocker");
            Check(LobbyRules.GuestRoomPush(false, 1, false) == RoomPush.HideAndPush && LobbyRules.GuestRoomPush(false, 0, false) == RoomPush.HideAndPush,
                "the main menu's page is no longer pushed as before");
            string guest = Source("BeaverBuddies", "Lobby", "LobbyGuestPanel.cs");
            string open = Body(guest, "private void Open(ClientEventIO current, LobbyView view)");
            Check(open.Contains("inGame != null ? LobbyFrame.Window : LobbyFrame.Page") && open.Contains("inGame.AttachStyles"), "the guest's room does not pick its frame by the scene");
            Check(open.Contains("if (page.Close != null) page.Close.clicked += AskToLeave;"), "the window's close button is not Leave");
            Check(open.Contains("if (HowToPush() == RoomPush.Push) _panelStack.Push(this);"), "the guest's room is not pushed by the rule");
            Check(Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private void CheckWaitingRoom()").Contains("InGameLobby.InGame"),
                "the waiting-room check does not know the scene");
        });

        yield return ("rc7: an invite accepted in a game joins the room in the game; rc6's Save and join and the main menu's pending join are gone", () =>
        {
            Check(InviteRules.Decide(inMainMenu: false, inCoopSession: false, hostingPage: false) == InviteStep.JoinInGame, "a game played alone does not join in place");
            Check(InviteRules.Decide(false, true, false) == InviteStep.LeaveCoopGameFirst, "an invite takes over a running co-op game");
            Check(InviteRules.Decide(false, false, true) == InviteStep.StopHostingFirst, "an invite replaces the room this player hosts");
            string steam = Source("BeaverBuddies", "Steam", "SteamOverlayConnectionService.cs");
            string join = Body(steam, "private void JoinHostLobby(CSteamID lobby, CSteamID owner)");
            Check(join.Contains("!BeaverBuddies.Lobby.InGameLobby.InGame"), "a game is taken for the main menu (the guest's room is bound in both)");
            Check(join.Contains("case InviteStep.JoinInGame:") && join.IndexOf("_clientConnectionService.TryToConnect(owner)", StringComparison.Ordinal) > join.IndexOf("case InviteStep.JoinInGame:", StringComparison.Ordinal),
                "an invite in a game does not connect");
            foreach (string gone in new[] { "SaveAndOpenMainMenu", "_mainMenuSceneLoader", "pendingInviteLobby" })
                Check(!steam.Contains(gone), gone + " is back");
            foreach (string key in new[] { "BeaverBuddies.Invite.FromGame", "BeaverBuddies.Invite.SaveAndJoin", "BeaverBuddies.Lobby.InGameInvite" })
                Check(Csv(key) == null, key + " is in the English texts, but nothing shows it");
        });

        yield return ("rc7: a game's menu has Join co-op game while no session is live and no room is hosted; its box gets the main menu's sheets", () =>
        {
            Check(JoinButtonRules.Show(mainMenu: false, sessionLive: false, hostingRoom: false), "a game played alone has no Join co-op game");
            Check(!JoinButtonRules.Show(false, true, false), "a live co-op game offers Join (it would take over the session)");
            Check(!JoinButtonRules.Show(false, false, true), "a host's own room offers Join");
            Check(JoinButtonRules.Show(true, true, true), "the main menu hides Join");
            string box = Source("BeaverBuddies", "Connect", "JoinCoopBox.cs");
            InOrder(Body(box, "private JoinCoopBox(PanelStack panelStack"), "the box's root", "_root.AddToClassList(\"content-row-centered\");",
                "attachStyles?.Invoke(_root);", "_root.Add(box);");
            Check(Body(Source("BeaverBuddies", "Connect", "ClientConnectionUI.cs"), "private void ShowJoinBox()").Contains("inGame != null ? inGame.AttachStyles"),
                "the game menu's Join box gets no sheets in a game");
            string configure = Body(Source("BeaverBuddies", "Plugin.cs"), "public class ReplayConfigurator");
            int bind = configure.IndexOf("Bind<BeaverBuddies.Lobby.LobbyGuestPanel>()", StringComparison.Ordinal);
            Check(bind > 0 && bind < configure.IndexOf("if (EventIO.IsNull) return;", StringComparison.Ordinal), "a game has no guest's room to show");
        });

        // ---- Rejoining in a game, and carrying the guests into the host's room (plan §4.3, §4.4, flows C, E, G) ----

        yield return ("rc7: the host's move notice reaches a guest before its connection closes, and that guest reports no connection error", () =>
        {
            // A real host and guest over an in-memory pipe: the guest has its save (it is in the game), the host says it is
            // moving, then closes, as ending its session does.
            (int Moved, string? Error, TimberClient Guest) Run(bool notice)
            {
                var (hostStream, guestStream) = PipeStream.Pair();
                var host = new TimberServer(new PipeListener(hostStream), () => Task.FromResult(new byte[] { 7, 8, 9 }), null);
                var guest = new TimberClient(guestStream);
                int maps = 0, moved = 0;
                string? error = null;
                guest.OnMapReceived += _ => maps++;
                guest.OnError += e => error = e;
                guest.OnHostMoved += () => moved++;
                try
                {
                    host.Start();
                    guest.Start();
                    Check(SpinWait.SpinUntil(() => { host.Update(); guest.Update(); return maps > 0; }, 3000), "the guest never got its save");
                    // Once the guest has finished joining (the host no longer queues for it), it is told.
                    if (notice) Check(SpinWait.SpinUntil(() => host.SendMoveNotice(4242) == 1, 3000), "the host told nobody");
                    host.Close();
                    Check(SpinWait.SpinUntil(() => { guest.Update(); return moved > 0 || error != null; }, 3000), "the guest heard nothing of the end");
                    // Said once.
                    guest.Update();
                    return (moved, error, guest);
                }
                finally { host.Close(); guest.Close(); }
            }
            var (moved, error, told) = Run(notice: true);
            Check(moved == 1 && error == null, $"a guest told of the move reported {(error != null ? "a connection error: " + error : moved + " moves")}");
            Check(told.HostMoved && told.MovedToSteamLobby == 4242, "the guest did not keep what the notice said");
            // Without the notice the same end is the connection lost it always was.
            var (movedAnyway, lost, untold) = Run(notice: false);
            Check(movedAnyway == 0 && lost != null && !untold.HostMoved, "a guest never told reads the host's end as a move");
        });

        yield return ("rc7: a move notice is the host's control frame: never replayed or hashed, dropped from a guest, and its lobby validated", () =>
        {
            Check(MoveFrames.TryParse(MoveFrames.Notice(76561198000000000UL), out ulong? lobby) && lobby == 76561198000000000UL, "the lobby is lost on the way");
            Check(MoveFrames.TryParse(MoveFrames.Notice(null), out ulong? none) && none == null, "a notice without a lobby is refused");
            Check(MoveFrames.TryParse(MoveFrames.Notice(0), out ulong? zero) && zero == null, "lobby 0 is taken for a lobby");
            var bad = new[]
            {
                new Newtonsoft.Json.Linq.JObject { ["type"] = MoveFrames.Type, ["lobby"] = 5 },
                new Newtonsoft.Json.Linq.JObject { ["type"] = MoveFrames.Type, ["lobby"] = "12a" },
                new Newtonsoft.Json.Linq.JObject { ["type"] = MoveFrames.Type, ["lobby"] = "123456789012345678901" },
                new Newtonsoft.Json.Linq.JObject { ["type"] = MoveFrames.Type, ["lobby"] = "1", ["extra"] = true },
                new Newtonsoft.Json.Linq.JObject { ["type"] = "Heartbeat" },
            };
            foreach (var frame in bad) Check(!MoveFrames.TryParse(frame, out _), "a malformed notice is read: " + frame.ToString(Newtonsoft.Json.Formatting.None));
            string net = Source("TimberNet", "TimberNetBase.cs");
            string receive = Body(net, "private void ReceiveMessages(ISocketStream client, bool isClient)");
            InOrder(receive, "the receive loop", "if (MoveFrames.IsMoveType(controlType))", "if (!isClient) continue;", "OnHostMoving(", "return;",
                "StampReceivedEvent(client, control);");
            string client = Source("TimberNet", "TimberClient.cs");
            InOrder(Body(client, "protected override void HandleConnectionFailure(ISocketStream stream, string message)"), "a moved guest's end",
                "if (hostMoved)", "QueueHostMoved();", "Close();", "return;", "QueueError(message);");
        });

        yield return ("rc7: the host's move goes notice, then save (Save and Rehost), then the session's quiet end and its server's close, then the room", () =>
        {
            string rehosting = Source("BeaverBuddies", "Connect", "RehostingService.cs");
            Check(Body(rehosting, "public bool RehostGame()").Contains("beforeSave: () => ServerHostingUtils.TellGuestsMoving()"), "Save and Rehost does not tell the guests first");
            InOrder(Body(rehosting, "public bool SaveRehostFile("), "Save and Rehost", "if (ReplayService.HasReplayFailure)", "beforeSave?.Invoke();",
                "_gameSaver.SaveInstantlySkippingNameValidation(");
            string hosting = Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs");
            InOrder(Body(hosting, "public static void LoadAndHost(GameSaveRepository repository, SaveReference saveReference, bool savedForRoom)"),
                "hosting", "EndSessionForRoom();", "waitingRoom.OpenForSave(");
            InOrder(Body(hosting, "internal static void EndSessionForRoom()"), "ending the session", "TellGuestsMoving();",
                "GetSingleton<ReplayService>()?.EndSession(null);", "EventIO.Reset();");
            string tell = Body(hosting, "internal static void TellGuestsMoving()");
            Check(tell.Contains("EventIO.Get() is ServerEventIO server) || server.IsSessionOver) return;") && tell.Contains("server.MoveToRoom();"),
                "the move is told for a session this game does not host, or not at all");
            string server = Source("BeaverBuddies", "IO", "ServerEventIO.cs");
            InOrder(Body(server, "public void MoveToRoom()"), "the host's notice", "if (moving ||", "NetBase.SendMoveNotice(lobby);",
                "SteamNet.PumpBetweenTicks(force: true);", "steam?.KeepLobbyForNextServer();");
            // The room's server is started after the old one is closed (EndSession resets EventIO, which closes it and its
            // listeners), so the port and the Steam lobby are free for it.
            Check(Source("BeaverBuddies", "ReplayService.cs").Contains("EventIO.Reset();"), "ending the session leaves its server open");
            Check(Csv("BeaverBuddies.Host.Rehost.Confirm")!.Contains("brought into the room"), "Save and Rehost's question still says the players leave");
        });

        yield return ("rc7: the host keeps its Steam lobby for the room: the old listener hands it over, the room's reopens it, an unused one is left", () =>
        {
            string listener = Source("BeaverBuddies", "Steam", "SteamListener.cs");
            string stop = Body(listener, "public void Stop()");
            InOrder(stop, "the old listener's stop", "if (keepLobby)", "LeaveHandedOverLobby();", "handedOver = LobbyID;", "else SteamMatchmaking.LeaveLobby(LobbyID);");
            string create = Body(listener, "private void CreateLobby()");
            InOrder(create, "the room's listener", "CSteamID kept = handedOver;", "handedOver = CSteamID.Nil;",
                "SteamMatchmaking.GetLobbyOwner(kept) == SteamUser.GetSteamID()", "Reopen(kept);", "return;", "SteamMatchmaking.CreateLobby(");
            string reopen = Body(listener, "private void Reopen(CSteamID lobby)");
            foreach (string step in new[] { "SetLobbyType(", "SetLobbyJoinable(LobbyID, !closed)", "SetLobbyData(LobbyID, OpenKey, closed ? \"0\" : \"1\")", "WriteDetails();" })
                Check(reopen.Contains(step), "the kept lobby is not reopened as a new one is: " + step);
            // A room that never took it: its server did not start, or a server without Steam started, or the main menu came.
            Check(Source("BeaverBuddies", "Lobby", "LobbySession.cs").Contains("BeaverBuddies.Steam.SteamListener.LeaveHandedOverLobby();"), "a room that failed keeps the lobby");
            Check(Source("BeaverBuddies", "IO", "ServerEventIO.cs").Contains("if (!listeners.OfType<SteamListener>().Any()) BeaverBuddies.Steam.SteamListener.LeaveHandedOverLobby();"),
                "a server without Steam keeps the lobby for nobody");
            Check(Body(Source("BeaverBuddies", "Plugin.cs"), "public class ConnectionMenuConfigurator").Contains("SteamListener.LeaveHandedOverLobby();"), "the main menu keeps the lobby");
            // Guests connect only as members of the host's current lobby: the kept lobby is it.
            Check(Body(listener, "private bool IsInLobby(ulong steamId)").Contains("CSteamID lobby = LobbyID;"), "the host no longer lets in its lobby's members");
        });

        yield return ("rc7: a guest told of the move ends its session quietly and follows the host at once, in its game", () =>
        {
            string io = Source("BeaverBuddies", "IO", "ClientEventIO.cs");
            Check(io.Contains("NetBase.OnHostMoved += OnHostMoved;") && io.Contains("NetBase.OnHostMoved -= OnHostMoved;"), "the guest does not hear of the move");
            InOrder(Body(io, "private void OnHostMoved()"), "the guest's move", "ulong? keptLobby = NetBase?.MovedToSteamLobby;", "CleanUp();",
                "FailedToConnect = true;", "if (!isCurrent || !mapDelivered) return;", "GetSingleton<ReplayService>()?.EndSession(null);",
                "EventIO.ResetIf(this);", "ClientConnectionService.FollowHost(keptLobby);");
            Check(!Body(io, "private void OnHostMoved()").Contains("SessionEndMessages") && !Body(io, "private void OnHostMoved()").Contains("offerRejoin"),
                "the guest is told its connection was lost, or offered Rejoin, for a move");
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            InOrder(Body(service, "public static void FollowHost(ulong? keptLobby)"), "following", "GetSingleton<ClientConnectionService>()",
                "service.StartRejoin(following: true, keptLobby);");
            string start = Body(service, "private void StartRejoin(bool following, ulong? keptLobby)");
            Check(start.Contains("nextRejoinMs = 0;") && start.Contains("followLobby = keptLobby;"), "the follow waits before its first try, or forgets the kept lobby");
            Check(Csv("BeaverBuddies.Rejoin.Following") != null, "the carried guest's box has no text");
        });

        yield return ("rc7: a rejoin waits in the game, its box over the paused game; a move is tried every second, a Steam guest straight to the host", () =>
        {
            Check(JoinFlowRules.BoxCanShow(inGame: true, panelsOpen: 0, overlayOnTop: false), "a game with nothing open never shows the rejoin's box");
            Check(!JoinFlowRules.BoxCanShow(inGame: false, panelsOpen: 0, overlayOnTop: false), "the main menu's box shows before the menu is up");
            Check(!JoinFlowRules.BoxCanShow(true, 1, true) && JoinFlowRules.BoxCanShow(true, 1, false), "the box goes over an overlay, or not over the game menu");
            Check(JoinFlowRules.RejoinEveryMs(true, 0) == JoinFlowRules.FollowEveryMs && JoinFlowRules.FollowEveryMs <= 1000, "a move is not followed at once");
            Check(JoinFlowRules.RejoinEveryMs(true, JoinFlowRules.FollowFastForMs + 1) == JoinFlowRules.WaitEveryMs, "a move is tried every second for ever");
            Check(JoinFlowRules.RejoinEveryMs(false, 0) == JoinFlowRules.WaitEveryMs && JoinFlowRules.WaitEveryMs == 3000, "a rejoin's pace changed");
            ReconnectPlan kept = DesyncDialogPlan.Reconnect(JoinRoute.ViaSteam(7), "127.0.0.1", _ => 99, keptLobby: 55);
            Check(kept.Step == ReconnectStep.ConnectInLobby && kept.Lobby == 55, "a Steam guest looks for a new lobby though it is in the kept one");
            Check(DesyncDialogPlan.Reconnect(JoinRoute.ViaSteam(7), "127.0.0.1", _ => 99).Step == ReconnectStep.JoinSteamLobby, "a lost connection no longer finds the host's lobby");
            Check(DesyncDialogPlan.Reconnect(JoinRoute.ViaAddress("host:1"), "x", _ => null, keptLobby: 55).Step == ReconnectStep.DialAddress,
                "a direct guest dials a Steam lobby");
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            Check(!service.Contains("OpenMainMenu") && !service.Contains("MainMenuSceneLoader"), "a rejoin goes to the main menu again");
            string watch = Body(service, "private void WatchRejoin()");
            Check(watch.Contains("JoinFlowRules.BoxCanShow(InGameLobby.InGame,"), "the rejoin's box waits for a panel a game with nothing open never has");
            int from = watch.IndexOf("case ReconnectStep.ConnectInLobby:", StringComparison.Ordinal);
            Check(from > 0, "the rejoin has no step for the kept lobby");
            string inLobby = watch.Substring(from, watch.IndexOf("case ReconnectStep.JoinSteamLobby:", from, StringComparison.Ordinal) - from);
            InOrder(inLobby, "the kept lobby", "if (!RejoinLobbyOpen(plan.Lobby)", "quietJoin = true;", "TryToConnect(new CSteamID(host));", "quietJoin = false;");
            Check(SessionEndMessages.ConnectionLost(null).Contains("Rejoin waits here"), "the lost connection still sends the player to the main menu");
            Check(Csv("BeaverBuddies.Rejoin.Waiting")!.Contains("Co-op Game room"), "the rejoin's box still names a page in the main menu");
        });

        // ---- Exit saves at Start (plan §4.5, K3) ----

        yield return ("rc7: Start's exit save: a host or guest leaving a game of their own, not a guest's copy, a game just saved, or the main menu", () =>
        {
            Check(ExitSaveRules.Make(inGame: true, loadedAsGuest: false, savedForRoom: false), "the host's game (Load game → Host co-op game) is not saved");
            Check(!ExitSaveRules.Make(true, false, savedForRoom: true), "the game menu's Host co-op game or Save and Rehost is saved twice");
            Check(!ExitSaveRules.Make(true, loadedAsGuest: true, false), "a carried guest's copy of the host's game is saved");
            Check(ExitSaveRules.Make(true, false, false), "a guest joining from a game played alone is not saved");
            Check(!ExitSaveRules.Make(inGame: false, false, false), "the main menu makes an exit save");
            string lobby = Source("BeaverBuddies", "Lobby", "InGameLobby.cs");
            string exit = Body(lobby, "public void ExitSaveForStart(bool savedForRoom)");
            InOrder(exit, "the exit save", "bool loadedAsGuest = replay != null && !replay.LoadedAsHost;", "ExitSaveRules.Make(", "_autosaver.CreateExitSave();");
            Check(exit.Contains("catch (Exception error)") && exit.Contains("starting anyway"), "a failed exit save stops the start");
            // The host: at Start, before the session starts. The guest: when the save arrives, before its join is installed.
            InOrder(Body(Source("BeaverBuddies", "Lobby", "LobbyHostPanel.cs"), "private void StartNow()"), "the host's Start",
                "InGameLobby.Current?.ExitSaveForStart(started.Setup.SavedForRoom);", "started.Start(_sceneLoader,");
            InOrder(Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private void LoadMap(byte[] mapBytes, ClientEventIO joined)"), "the guest's save",
                "InGameLobby.Current?.ExitSaveForStart(savedForRoom: false);", "DeterminismService.InitGameStartState(mapBytes);", "EventIO.Set(joined);",
                "SingletonManager.Reset();");
            // The autosaver is the game's alone: the main menu never binds the game side of the room.
            Check(!Body(Source("BeaverBuddies", "Plugin.cs"), "public class ConnectionMenuConfigurator").Contains("InGameLobby>"), "the main menu binds the autosaver's user");
        });
    }
}
