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

        test("rc7: every class the room's window and the Join box use is in CoreStyle, CommonStyle or a main-menu sheet added to their root", () =>
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
                // The game menu's Join co-op game box gets the same sheets.
                .Concat(Strings("BeaverBuddies.Connect.JoinCoopBox", "ClassesUsed"))
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
            if (!Mod("BeaverBuddies.Lobby.InGameLobby").GetConstructors().Single().GetParameters().Any(p => p.ParameterType == loader))
                throw new Exception("the game side of the room no longer takes the asset loader");
        });

        test("rc7: every game binds the host's and the guest's rooms, their game side and the faction capture before the co-op return", () =>
        {
            var configure = IlScan.Instructions(Only(Mod("BeaverBuddies.ReplayConfigurator"), "Configure"));
            int coopOnly = configure.FindIndex(i => i.Calls && i.Member?.Name == "get_IsNull");
            if (coopOnly < 0) throw new Exception("the configurator no longer tells co-op games apart");
            foreach (string bound in new[] { "LobbyHostPanel", "InGameLobby", "NewGameFactionCapture", "LobbyGuestPanel" })
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

        // ---- Joining from a game: the held join (plan §4.3) ----

        test("rc7: a join made in a game is held apart from it, and installed as EventIO in LoadMap before the registry's reset", () =>
        {
            Type service = Mod("BeaverBuddies.Connect.ClientConnectionService");
            MethodInfo connect = service.GetMethods(All).Single(m => m.Name == "TryToConnect" && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType.Name == "ISocketStream");
            var code = IlScan.Instructions(connect);
            int hold = code.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Connect.JoinFlowRules", "HoldJoin"));
            int held = code.FindIndex(i => i.Calls && i.Member?.Name == "set_HeldJoin");
            int install = code.FindIndex(i => i.Calls && i.Is("BeaverBuddies.IO.EventIO", "Set"));
            if (hold < 0 || held < hold || install < 0) throw new Exception("a game's join is not held, or the main menu's not installed");
            MethodInfo loadMap = service.GetMethod("LoadMap", All) ?? throw new Exception("LoadMap is gone");
            if (loadMap.GetParameters().Length != 2) throw new Exception("LoadMap no longer knows which join its save is for");
            var load = IlScan.Instructions(loadMap);
            int set = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.IO.EventIO", "Set"));
            int reset = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.SingletonManager", "Reset"));
            int scene = load.FindLastIndex(i => i.Calls && (i.Member?.Name == "LoadScene" || i.Member?.Name == "StartSaveGame"));
            if (set < 0 || reset < set || scene < reset) throw new Exception("the held join is not installed just before the registry's reset and the load");
            if (Mod("BeaverBuddies.IO.ClientEventIO").GetProperty("HeldJoin") == null) throw new Exception("ClientEventIO.HeldJoin is gone");
            if (Mod("BeaverBuddies.Connect.ConnectionErrorPlanner").GetMethods(All).All(m => m.Name != "Decide" || m.GetParameters().Length != 4))
                throw new Exception("the error planner no longer knows a held join");
        });

        test("rc7: nothing on the guest's side takes a join for EventIO; every way a join ends goes through EndJoin", () =>
        {
            Type guest = Mod("BeaverBuddies.Lobby.LobbyGuestPanel");
            foreach (var pair in IlScan.Of(guest))
                if (pair.Value.Any(m => m.DeclaringType?.FullName == "BeaverBuddies.IO.EventIO" && (m.Name == "Get" || m.Name == "ResetIf")))
                    throw new Exception($"LobbyGuestPanel.{pair.Key.Name} still takes the join for EventIO");
            Type service = Mod("BeaverBuddies.Connect.ClientConnectionService");
            foreach (var pair in IlScan.Of(service))
                if (pair.Value.Any(m => m.DeclaringType?.FullName == "BeaverBuddies.IO.EventIO" && m.Name == "ResetIf"))
                    throw new Exception($"ClientConnectionService.{pair.Key.Name} ends a join only if it is installed");
            if (!IlScan.Instructions(Only(guest, "Leave")).Any(i => i.Calls && i.Is("BeaverBuddies.Connect.ClientConnectionService", "EndJoin")))
                throw new Exception("Leave does not end a held join");
            if (!IlScan.Instructions(Only(Mod("BeaverBuddies.Connect.ServerHostingUtils"), "EndSessionForRoom")).Any(i => i.Calls && i.Member?.Name == "EndJoin"))
                throw new Exception("hosting leaves a held join connected");
        });

        test("rc7: an invite in a game joins in place (JoinInGame), and the game menu's Join box is the one in the main menu", () =>
        {
            Type step = Mod("BeaverBuddies.Connect.InviteStep");
            if (!Enum.GetNames(step).Contains("JoinInGame") || Enum.GetNames(step).Contains("OfferFromGame"))
                throw new Exception("the invite's steps are not rc7's: " + string.Join(", ", Enum.GetNames(step)));
            object? decided = Mod("BeaverBuddies.Connect.InviteRules").GetMethod("Decide")!.Invoke(null, new object[] { false, false, false });
            if (decided?.ToString() != "JoinInGame") throw new Exception("a game played alone does not join in place: " + decided);
            Type ui = Mod("BeaverBuddies.Connect.ClientConnectionUI");
            if (!IlScan.Instructions(Only(ui, "AddJoinButton")).Any(i => i.Calls && i.Is("BeaverBuddies.Connect.JoinButtonRules", "Show")))
                throw new Exception("the game menu's Join co-op game does not follow its rule");
            if (!IlScan.Instructions(Only(ui, "ShowJoinBox")).Any(i => i.Loads == false && i.Member?.Name == "AttachStyles"))
                throw new Exception("the game menu's Join box gets no style sheets in a game");
            using ZipArchive zip = OpenUi();
            if (!Read(zip, "Views/Game/GameOptionsBox.uxml").Contains("name=\"LoadGameButton\"")) throw new Exception("the game menu has no Load game button to add Join under");
        });

        // ---- Rejoining in a game, and carrying the guests (plan §4.3, §4.4) ----

        test("rc7: TimberNet carries the host's move notice: the host sends it, a guest reads it before the end and raises no error for it", () =>
        {
            Assembly net = Assembly.Load("TimberNet");
            Type frames = net.GetType("TimberNet.MoveFrames", true)!;
            if ((string?)frames.GetField("Type")?.GetValue(null) != "HostMoving") throw new Exception("the notice's type changed (every player runs the same build, but check it)");
            Type server = net.GetType("TimberNet.TimberServer", true)!;
            if (server.GetMethod("SendMoveNotice") == null) throw new Exception("the host cannot tell its guests it is moving");
            Type client = net.GetType("TimberNet.TimberClient", true)!;
            foreach (string member in new[] { "HostMoved", "MovedToSteamLobby", "OnHostMoving", "HandleConnectionFailure" })
                if (client.GetMember(member, All).Length == 0) throw new Exception("TimberClient." + member + " is gone");
            if (net.GetType("TimberNet.TimberNetBase", true)!.GetEvent("OnHostMoved") == null) throw new Exception("nothing raises the move on the update thread");
            var failure = IlScan.Instructions(client.GetMethod("HandleConnectionFailure", All)!);
            int queue = failure.FindIndex(i => i.Calls && i.Member?.Name == "QueueHostMoved");
            int close = failure.FindIndex(i => i.Calls && i.Member?.Name == "Close");
            if (queue < 0 || close < queue) throw new Exception("a moved guest's end is not queued before its close (the game could see the session over first)");
        });

        test("rc7: the host's move: notice before the save, then the quiet end and the server's close, then the room; the Steam lobby is kept", () =>
        {
            Type utils = Mod("BeaverBuddies.Connect.ServerHostingUtils");
            var end = IlScan.Instructions(Only(utils, "EndSessionForRoom"));
            int tell = end.FindIndex(i => i.Calls && i.Member?.Name == "TellGuestsMoving");
            int quiet = end.FindIndex(i => i.Calls && i.Is("BeaverBuddies.ReplayService", "EndSession"));
            int reset = end.FindLastIndex(i => i.Calls && i.Is("BeaverBuddies.IO.EventIO", "Reset"));
            if (tell < 0 || quiet < tell || reset < quiet) throw new Exception("the guests are not told before the session ends and its server closes");
            if (!IlScan.Instructions(Only(utils, "TellGuestsMoving")).Any(i => i.Calls && i.Is("BeaverBuddies.IO.ServerEventIO", "MoveToRoom")))
                throw new Exception("the host's move tells nobody");
            var move = IlScan.Instructions(Only(Mod("BeaverBuddies.IO.ServerEventIO"), "MoveToRoom"));
            int send = move.FindIndex(i => i.Calls && i.Member?.Name == "SendMoveNotice");
            int keep = move.FindIndex(i => i.Calls && i.Member?.Name == "KeepLobbyForNextServer");
            if (send < 0 || keep < send) throw new Exception("the lobby is kept before the guests are told, or not at all");
            Type rehosting = Mod("BeaverBuddies.Connect.RehostingService");
            MethodInfo save = Only(rehosting, "SaveRehostFile");
            if (!save.GetParameters().Any(p => p.Name == "beforeSave")) throw new Exception("Save and Rehost can no longer tell the guests before its save");
            var saving = IlScan.Instructions(save);
            int before = saving.FindIndex(i => i.Calls && i.Member?.Name == "Invoke" && i.Member.DeclaringType == typeof(Action));
            int saved = saving.FindIndex(i => i.Calls && i.Member?.Name == "SaveInstantlySkippingNameValidation");
            if (before < 0 || saved < before) throw new Exception("the guests are told after the save");
            // The Steam calls the handover makes.
            Assembly steam = Assembly.Load("com.rlabrecque.steamworks.net");
            foreach (var (type, member) in new[] { ("SteamMatchmaking", "GetLobbyOwner"), ("SteamMatchmaking", "SetLobbyType"), ("SteamMatchmaking", "SetLobbyJoinable"),
                ("SteamMatchmaking", "GetNumLobbyMembers"), ("SteamMatchmaking", "LeaveLobby"), ("SteamUser", "GetSteamID") })
                Has(steam.GetType("Steamworks." + type, true)!, member, "the Steam lobby kept for the room");
            Type listener = Mod("BeaverBuddies.Steam.SteamListener");
            foreach (string member in new[] { "KeepLobbyForNextServer", "LeaveHandedOverLobby", "Reopen", "handedOver" })
                Has(listener, member, "the Steam lobby kept for the room");
        });

        test("rc7: a guest told of the move ends quietly and follows; a rejoin waits in the game and never goes to the main menu", () =>
        {
            Type io = Mod("BeaverBuddies.IO.ClientEventIO");
            var moved = IlScan.Instructions(Only(io, "OnHostMoved"));
            int quiet = moved.FindIndex(i => i.Calls && i.Is("BeaverBuddies.ReplayService", "EndSession"));
            int follow = moved.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Connect.ClientConnectionService", "FollowHost"));
            if (quiet < 0 || follow < quiet) throw new Exception("a moved guest does not end its session and follow the host");
            // EndSession(null): the message (null) and offerRejoin (false) are its two arguments.
            if (quiet < 2 || moved[quiet - 2].Op != OpCodes.Ldnull || moved[quiet - 1].Op != OpCodes.Ldc_I4_0)
                throw new Exception("a moved guest's session ends with a message, or offers Rejoin");
            Type service = Mod("BeaverBuddies.Connect.ClientConnectionService");
            if (service.GetConstructors().Single().GetParameters().Any(p => p.ParameterType.Name == "MainMenuSceneLoader"))
                throw new Exception("ClientConnectionService still asks for the main menu's loader");
            var watch = IlScan.Instructions(Only(service, "WatchRejoin"));
            if (!watch.Any(i => i.Calls && i.Is("BeaverBuddies.Connect.JoinFlowRules", "BoxCanShow"))) throw new Exception("the rejoin's box waits for a panel a game may never have");
            Type plan = Mod("BeaverBuddies.Connect.ReconnectStep");
            if (!Enum.GetNames(plan).Contains("ConnectInLobby")) throw new Exception("a Steam guest cannot go straight to the host in the kept lobby");
        });

        // ---- Exit saves at Start (plan §4.5, K3) ----

        test("rc7: Start makes the game's own exit save of the game it replaces, before the load, on both sides", () =>
        {
            Type autosaver = Game("Timberborn.Autosaving", "Timberborn.Autosaving.Autosaver");
            MethodInfo create = autosaver.GetMethod("CreateExitSave", BindingFlags.Public | BindingFlags.Instance) ?? throw new Exception("Autosaver.CreateExitSave is gone or not public");
            if (create.GetParameters().Length != 0) throw new Exception("Autosaver.CreateExitSave takes arguments now");
            Type lobby = Mod("BeaverBuddies.Lobby.InGameLobby");
            if (!lobby.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == autosaver)) throw new Exception("the game side of the room has no autosaver");
            var exit = IlScan.Instructions(Only(lobby, "ExitSaveForStart"));
            int rule = exit.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Lobby.ExitSaveRules", "Make"));
            int save = exit.FindIndex(i => i.Calls && i.Member == create);
            if (rule < 0 || save < rule) throw new Exception("the exit save is made without its rule, or not at all");
            // The host: at Start, before the session starts (and loads the save).
            var start = IlScan.Instructions(Only(Mod("BeaverBuddies.Lobby.LobbyHostPanel"), "StartNow"));
            int hostSave = start.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Lobby.InGameLobby", "ExitSaveForStart"));
            int starts = start.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Lobby.LobbySession", "Start"));
            if (hostSave < 0 || starts < hostSave) throw new Exception("the host's game is not saved before its room starts");
            // The guest: when its save arrives, before the join is installed and the registry reset.
            var load = IlScan.Instructions(Mod("BeaverBuddies.Connect.ClientConnectionService").GetMethod("LoadMap", All)!);
            int guestSave = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.Lobby.InGameLobby", "ExitSaveForStart"));
            int seed = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.DeterminismService", "InitGameStartState"));
            int install = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.IO.EventIO", "Set"));
            int reset = load.FindIndex(i => i.Calls && i.Is("BeaverBuddies.SingletonManager", "Reset"));
            if (guestSave < 0 || seed < guestSave || install < guestSave || reset < guestSave)
                throw new Exception("the guest's game is not saved before the host's save (its seed, its session) replaces it");
            // Only a game binds it: the autosaver is the Game context's (BindingChecks checks the container can make it).
            if (IlScan.Instructions(Only(Mod("BeaverBuddies.ConnectionMenuConfigurator"), "Configure")).Any(i => i.Calls && i.Member is MethodInfo m
                && m.IsGenericMethod && m.GetGenericArguments()[0] == lobby)) throw new Exception("the main menu binds the game side of the room");
        });
    }
}
