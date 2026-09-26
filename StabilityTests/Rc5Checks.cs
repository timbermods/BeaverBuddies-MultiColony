using BeaverBuddies.Connect;

/// <summary>
/// The review of 1.4.0-rc2 to rc4 (design/review-1.4.0-rc4/), checks that need no game. One check per finding, named after
/// it: A (hosting, rehost and rejoin), B (colony rules and timing), C (UI, strings and docs).
/// </summary>
static class Rc5Checks
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

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        // ---- A: hosting, rehost and rejoin ----

        yield return ("A1: Host co-op game is in a game played alone: the rehosting service is made in every game, not only in co-op", () =>
        {
            string configure = Body(Source("BeaverBuddies", "Plugin.cs"), "public class ReplayConfigurator");
            int bind = configure.IndexOf("Bind<RehostingService>()", StringComparison.Ordinal);
            int coopOnly = configure.IndexOf("if (EventIO.IsNull) return;", StringComparison.Ordinal);
            Check(bind >= 0 && coopOnly > 0, "the configurator changed shape");
            Check(bind < coopOnly, "RehostingService is bound only in co-op again: a game played alone has no Host co-op game");
            Check(configure.IndexOf("Bind<RehostingService>()", bind + 1, StringComparison.Ordinal) < 0, "RehostingService is bound twice");
        });

        yield return ("A2: a Connecting or rejoin box closed under something else is still taken away, and its Cancel always works", () =>
        {
            string box = Source("BeaverBuddies", "Lobby", "ConnectingBox.cs");
            string cancel = Body(box, "public void OnUICancelled()");
            Check(cancel.Contains("if (closed)") && cancel.Contains("Poll();"), "a closed box's Cancel and Esc do nothing (its owner may have let it go)");
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            Check(Body(service, "private void CloseBox(ConnectingBox box, string what)").Contains("closingBoxes.Add(box)"),
                "a box that could not close yet is forgotten");
            Check(Body(service, "private void StopRejoin(bool resetJoin)").Contains("CloseBox(box, \"rejoin\")"), "the rejoin's box is closed without being kept");
            Check(Body(service, "public void CloseConnectingBox()").Contains("CloseBox("), "the Connecting box is closed without being kept");
            string update = Body(service, "public void UpdateSingleton()");
            Check(update.Contains("closingBoxes[i].Poll();") && update.Contains("closingBoxes.RemoveAt(i)"), "boxes waiting to close are never taken away");
        });

        yield return ("A3: a Steam rejoin enters the host's lobby only once it is an open Co-op Game page of this build", () =>
        {
            Check(JoinFlowRules.RejoinEntersLobby(FriendGameState.WaitingRoom), "an open Co-op Game page is not joined");
            Check(!JoinFlowRules.RejoinEntersLobby(FriendGameState.Started), "the lobby of the game that ended (bb_open 0) is joined, and refuses");
            Check(!JoinFlowRules.RejoinEntersLobby(FriendGameState.Looking), "a lobby Steam hasn't described yet is joined");
            Check(!JoinFlowRules.RejoinEntersLobby(FriendGameState.OtherVersion), "another build's lobby is joined");
            Check(!JoinFlowRules.RejoinEntersLobby(FriendGameState.OpenGame), "a game (not a page) is joined");
            string watch = Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private void WatchRejoin()");
            int gate = watch.IndexOf("if (!RejoinLobbyOpen(plan.Lobby)) return;", StringComparison.Ordinal);
            int stop = watch.IndexOf("StopRejoin(resetJoin: false);\n                    try { SteamMatchmaking.JoinLobby", StringComparison.Ordinal);
            Check(gate >= 0 && stop > gate, "the rejoin stops and joins whatever lobby Steam shows first");
            string entered = Source("BeaverBuddies", "Steam", "SteamOverlayConnectionService.cs");
            Check(entered.Contains("SteamMatchmaking.LeaveLobby(enteredLobby)") && entered.Contains("enteredLobby = lobby;"),
                "a guest stays in the lobby of a game that ended");
        });

        yield return ("A4: a direct rejoin asks off the menu's thread whether the host listens, and a failed connect is closed", () =>
        {
            string watch = Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private void WatchRejoin()");
            int plan = watch.IndexOf("switch (plan.Step)", StringComparison.Ordinal);
            Check(plan > 0, "the rejoin changed shape");
            string steps = watch.Substring(plan);
            Check(steps.Contains("probe = System.Threading.Tasks.Task.Run(() => HostListening(typed, port));"), "the address is not asked off the menu's thread");
            // The address's step only probes (rc7's ConnectInLobby connects over Steam, which starts in the background).
            string dial = steps.Substring(steps.IndexOf("default:", StringComparison.Ordinal));
            Check(!dial.Contains("TryToConnect("), "a rejoin's try connects on the menu's thread before anything listens");
            Check(!steps.Substring(0, steps.IndexOf("default:", StringComparison.Ordinal)).Contains("TryToConnect(typed") , "a Steam step dials an address");
            // A socket that never connected has no stream; closing it still closes the socket.
            var wrapper = new TimberNet.TCPClientWrapper("127.0.0.1", 9);
            var inner = (System.Net.Sockets.TcpClient)typeof(TimberNet.TCPClientWrapper)
                .GetField("client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(wrapper)!;
            wrapper.Close();
            Check(inner.Client == null, "a never-connected socket is left open when its wrapper is closed");
        });

        yield return ("A5: the same mods difference with the same player is warned about once a session, not at every try", () =>
        {
            ModWarnings.Clear();
            var difference = new ModDifference();
            difference.OnlyThere.Add(new ModEntry("some.mod", "Some mod", "1.0"));
            Check(ModWarnings.Add(new PendingModWarning("Alex", difference)), "the first warning is dropped");
            Check(!ModWarnings.Add(new PendingModWarning("Alex", difference)), "a rejoin's every try warns again");
            var other = new ModDifference();
            other.OnlyThere.Add(new ModEntry("some.mod", "Some mod", "2.0"));
            Check(ModWarnings.Add(new PendingModWarning("Alex", other)), "a different difference is not warned about");
            Check(ModWarnings.Add(new PendingModWarning("Sam", difference)), "another player's difference is not warned about");
            Check(ModWarnings.TakeAll().Count == 3, "the queue does not hold each warning once");
            ModWarnings.Clear();
            Check(ModWarnings.Add(new PendingModWarning("Alex", difference)), "a new session does not warn again");
            ModWarnings.Clear();
        });

        yield return ("A6: a rejoin's quiet try shows no error for an address it can't read or resolve", () =>
        {
            string connect = Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "public bool TryToConnect(string address)");
            foreach (string key in new[] { "InvalidFormat", "InvalidAddress" })
                Check(connect.Contains("if (!quietJoin) ShowError(\"BeaverBuddies.JoinCoopGame.Error." + key + "\");"), key + " is shown at every quiet try");
        });

        yield return ("A7: the Load game box loads as the game made it: no host mode to leak into a game (since rc7 it hosts with its own button)", () =>
        {
            string hosting = Source("BeaverBuddies", "Connect", "ServerHostingUtils.cs");
            // rc4's host mode made the box's Enter and double-click host, and a game's box did nothing after hosting from the
            // main menu's. rc7 has no mode: Load, Enter and a double-click load; Host co-op game is a button of its own.
            Check(!hosting.Contains("[HarmonyPatch(typeof(LoadGameBox), \"LoadGame\")]"), "the box's LoadGame is patched again (a host mode)");
            Check(!hosting.Contains("HostMode") && !hosting.Contains("BoxClosed"), "the box has a host mode again");
            Check(!Body(Source("BeaverBuddies", "Plugin.cs"), "public class ReplayConfigurator").Contains("BoxClosed"), "a game scene clears a host mode that no longer exists");
        });

        yield return ("A8: the game menu's hosting button follows how the game was loaded, not whether its session is live", () =>
        {
            Check(HostButtonRules.Decide(coopGame: false, loadedAsHost: false, replayFailure: false) == HostButtonKind.HostCoopGame, "alone: Host co-op game");
            Check(HostButtonRules.Decide(true, true, false) == HostButtonKind.SaveAndRehost, "the host (also after a desync or a lost guest): Save and Rehost");
            Check(HostButtonRules.Decide(true, false, false) == HostButtonKind.Hidden, "a guest, also after its session ended, hosts nothing (its copy may be out of step)");
            Check(HostButtonRules.Decide(true, true, true) == HostButtonKind.Hidden, "after a failed action nothing is rehosted");
            string ui = Source("BeaverBuddies", "Connect", "ClientConnectionUI.cs");
            Check(!Body(ui, "private void DressHostInGame(Button host)").Contains("EventIO.IsNull"), "the button still reads a session that ended as playing alone");
            Check(Body(ui, "internal static HostButtonKind HostKind()").Contains("replay?.LoadedAsHost"), "the button no longer asks how the game was loaded");
            Check(Body(ui, "private void HostClicked()").Contains("kind == HostButtonKind.Hidden) return;"), "a hidden button's click still hosts");
            Check(Source("BeaverBuddies", "ReplayService.cs").Contains("LoadedAsHost = EventIO.Get() is ServerEventIO;"), "the game no longer records whether it was loaded as the host");
        });

        yield return ("A9: the Load game box's gold line comes back with the box after a Co-op Game room closes over it", () =>
        {
            // rc7: the line lives in the box (LoadGameBoxColonies) and is made once; showing the box again never blanks it.
            string line = Source("BeaverBuddies", "Connect", "LoadGameBoxColonies.cs");
            string of = Body(line, "public static LoadGameBoxColonies Of(VisualElement root)");
            Check(of.Contains("userData is LoadGameBoxColonies line) return line;"), "each showing of the box makes a new, empty line");
            Check(!of.Contains(".text ="), "showing the box again blanks the selected save's line");
        });

        yield return ("A10: the Load game box reads a save's file off the game's thread", () =>
        {
            string selected = Body(Source("BeaverBuddies", "Connect", "LoadGameBoxColonies.cs"), "public void Show(SaveReference save, GameSaveRepository repository)");
            Check(!selected.Contains("GetMapBtyes"), "the whole save is read on the game's thread");
            int task = selected.IndexOf("Task.Run(", StringComparison.Ordinal);
            Check(task > 0 && selected.IndexOf("stream.CopyTo(bytes)", StringComparison.Ordinal) > task, "the file is not read inside the task");
        });

        yield return ("A11: rc4's dead code is gone: the in-menu reconnect, the rehost flag, the unused loader, the start prompt", () =>
        {
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            foreach (string gone in new[] { "ReconnectNow", "ShowWaitForSteamInvite", "triedLobby" })
                Check(!service.Contains(gone), gone + " is back");
            // rc7 hosts in the game, so HostCoopFlow (and its PendingRehost) is gone altogether.
            Check(!File.Exists(Path.Combine(Root(), "BeaverBuddies", "Connect", "HostCoopFlow.cs")), "HostCoopFlow is back (a game no longer hosts through the main menu)");
            Check(!Source("BeaverBuddies", "Connect", "RehostingService.cs").Contains("_validatingGameLoader"), "RehostingService keeps a loader it never uses");
            Check(!File.Exists(Path.Combine(Root(), "BeaverBuddies", "Colonies", "HostStartGate.cs")), "the start prompt, unreachable since every game starts from a room, is back");
            foreach (string key in new[] { "BeaverBuddies.Colony.Start.Prompt", "BeaverBuddies.Colony.Start.Kept", "BeaverBuddies.Host.ConnectedClients",
                "BeaverBuddies.Host.DirectConnectClient", "BeaverBuddies.ClientDesynced.WaitForSteamInvite" })
                Check(Csv(key) == null, key + " is in the English texts, but nothing shows it");
        });

        // ---- B: colony rules and timing ----

        yield return ("B1: every new game keeps its starting settings, shared too, so a colony founded later starts on its difficulty", () =>
        {
            string prefix = Body(Source("BeaverBuddies", "MultiStart", "MultiStartPatches.cs"), "public static bool Prefix(StartingBuildingInitializer __instance)");
            int one = prefix.IndexOf("if (separateOne)", StringComparison.Ordinal);
            int several = prefix.IndexOf("if (separateColonies)", StringComparison.Ordinal);
            int firstRecord = prefix.IndexOf("RecordStartingSettings(", StringComparison.Ordinal);
            int secondRecord = prefix.IndexOf("RecordStartingSettings(", firstRecord + 1, StringComparison.Ordinal);
            Check(firstRecord >= 0 && firstRecord < one, "a one-start shared game does not keep its starting settings");
            Check(secondRecord > one && secondRecord < several, "a several-start shared game does not keep its starting settings");
            string mode = Source("BeaverBuddies", "Colonies", "ColonyModeService.cs");
            Check(Body(mode, "public void Save(ISingletonSaver singletonSaver)").Contains("if (!Enabled && StartingSettings == null) return;"),
                "a shared game saves no starting settings, so a split or a conversion starts on Normal");
        });

        yield return ("B2: on a guest, Ctrl+T's playing and hand-over status follow the host's last presence, as the warnings do", () =>
        {
            string lifecycle = Source("BeaverBuddies", "Colonies", "ColonyHandover.cs");
            Check(Body(lifecycle, "public void Seen(").Contains("lastPresentSlots = present.OrderBy(slot => slot).ToList();"), "the day's presence is not kept");
            Check(lifecycle.Contains("isHost || lastPresentSlots == null ? PresentSlots()"), "a guest reads its session list, which keeps players who left");
            Check(Source("BeaverBuddies", "Colonies", "TradeOverviewPanel.cs").Contains("lifecycle?.PresentForDisplay(host)"), "Ctrl+T does not use the presence for display");
        });

        yield return ("B3: a guest's change waits until a hosted save's conversion to separate colonies has been played", () =>
        {
            string rules = Source("BeaverBuddies", "Colonies", "ColonyRulesService.cs");
            int refuse = rules.IndexOf("replayEvent.ChangesGame() && SaveConversion.HostAwaitsConversion", StringComparison.Ordinal);
            Check(refuse > 0, "a guest's change can be played in the still-shared game and become the host's");
            int lineStart = rules.LastIndexOf('\n', refuse);
            Check(rules.Substring(lineStart, refuse - lineStart).Contains("replayEvent.player != ColonySession.HostPlayer"),
                "the host's own actions wait too (the conversion is one of them)");
            string conversion = Source("BeaverBuddies", "Colonies", "SaveConversion.cs");
            Check(conversion.Contains("public static bool HostAwaitsConversion => Pending != null || sent;"), "the wait ends before the conversion is played");
            Check(conversion.Contains("else sent = true;"), "a sent conversion is not waited for");
            Check(Body(conversion, "public override void Replay(IReplayContext context)").Contains("SaveConversion.Played();"), "the wait never ends");
        });

        yield return ("B5: a shared save made separate at Start keeps one pool of science by default", () =>
        {
            string options = Body(Source("BeaverBuddies", "Lobby", "LobbyHostPanel.cs"), "private VisualElement BuildConvertOptions(LobbySetup setup)");
            Check(options.Contains("setup.ConvertScience = false;"), "the friends' colonies start with none of the science they earned");
            Check(Csv("BeaverBuddies.Lobby.Convert.ScienceTooltip")!.Contains("start with none"), "the tooltip does not say what separate science costs the friends");
        });

        yield return ("B6: Found your own colony, pressed after another player's split, says where founding is", () =>
        {
            string ask = Body(Source("BeaverBuddies", "Colonies", "SharedColonySplit.cs"), "private void Ask(GameOptionsBox box)");
            int offered = ask.IndexOf("if (!Offered)", StringComparison.Ordinal);
            Check(offered > 0 && ask.IndexOf("Colony.Split.AlreadySeparate", offered, StringComparison.Ordinal) > offered, "the button silently does nothing");
            Check(Csv("BeaverBuddies.Colony.Split.AlreadySeparate")?.Contains("Ctrl+K") == true, "the text does not say how to found");
        });

        // ---- C: UI, strings and docs ----

        yield return ("C3: the main menu's band is the game's: with Join co-op game alone the panel fits it (rc7), so nothing resizes it", () =>
        {
            // rc5 grew the band for Host co-op game and Join co-op game (646 px against 616). rc7 takes Host co-op game off
            // the main menu (the Load game box hosts): 602 px, as before rc4, so the fit is gone with it.
            string ui = Source("BeaverBuddies", "Connect", "ClientConnectionUI.cs");
            string add = Body(ui, "public void AddJoinButton(VisualElement __result, bool mainMenu)");
            Check(!ui.Contains("FitMainMenu") && !ui.Contains("main-menu__content"), "the main menu's band is still resized");
            Check(add.Contains("if (!mainMenu)") && add.IndexOf("HostButtonName", StringComparison.Ordinal) > add.IndexOf("if (!mainMenu)", StringComparison.Ordinal),
                "the main menu has Host co-op game again");
            Check(add.Contains("mainMenu ? \"LoadGameButton\" : HostButtonName, \"JoinButton\""), "Join co-op game is no longer right under Load game in the main menu");
            Check(!File.ReadAllText(Path.Combine(Root(), "BeaverBuddies", "Connect", "JoinFlowRules.cs")).Contains("class MainMenuFit"), "the band's rule is back");
        });

        yield return ("C5: a rejoin gives up, and says why, only when waiting can't help: another build, or a full room", () =>
        {
            Check(JoinFlowRules.RejoinGivesUp("Multiplayer build mismatch. Both players must install the same archive and restart Timberborn."), "a build mismatch is waited out for ever");
            Check(JoinFlowRules.RejoinGivesUp("The waiting room is full."), "a full room is waited out");
            const string started = "The host has already started this game from its waiting room, so it can't be ";
            Check(Source("BeaverBuddies", "Lobby", "LobbySession.cs").Contains("ClosedMessage = \"" + started), "the running game's refusal changed");
            Check(!JoinFlowRules.RejoinGivesUp(started + "joined now. Ask the host to save and rehost."), "the running game's refusal ends the rejoin before the host rehosts");
            Check(!JoinFlowRules.RejoinGivesUp("Couldn't receive data: connection reset"), "a connection cut as the host rehosts ends the rejoin");
            Check(!JoinFlowRules.RejoinGivesUp(null!) && !JoinFlowRules.RejoinGivesUp(""), "nobody listening ends the rejoin");
            Check(JoinFlowRules.RoomFullMessage == TimberNet.LobbyRoom.FullMessage, "the full room's message changed in TimberNet");
            Check(Source("TimberNet", "CompatibilityHandshake.cs").Contains("\"" + JoinFlowRules.BuildMismatchMarker), "the handshake's mismatch message changed");
            string service = Source("BeaverBuddies", "Connect", "ClientConnectionService.cs");
            Check(service.Contains("if (!JoinFlowRules.RejoinGivesUp(error))"), "a rejoin swallows every refusal");
            Check(Body(service, "private void WatchRejoin()").Contains("\"BeaverBuddies.Rejoin.WaitingSteam\""), "a Steam guest is not told the host's invite joins too");
        });

        yield return ("C8: every button the mod adds beside the game's is initialised as the game's are (click sound, any modifier)", () =>
        {
            string inserter = Source("BeaverBuddies", "Connect", "ButtonInserter.cs");
            Check(inserter.Contains("new NineSliceButton()") && !inserter.Contains("new LocalizableButton()"), "a keyless LocalizableButton can't be initialised");
            Check(inserter.Contains("initializer?.InitializeVisualElement(button);"), "the added button is never initialised");
            // Each call passes the scene's initializer.
            foreach (string file in Directory.GetFiles(Path.Combine(Root(), "BeaverBuddies"), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                for (int at = text.IndexOf("ButtonInserter.DuplicateOrGetButton(", StringComparison.Ordinal); at >= 0;
                    at = text.IndexOf("ButtonInserter.DuplicateOrGetButton(", at + 1, StringComparison.Ordinal))
                {
                    int depth = 0, end = at;
                    for (int i = text.IndexOf('(', at); i < text.Length; i++)
                    {
                        if (text[i] == '(') depth++;
                        else if (text[i] == ')' && --depth == 0) { end = i; break; }
                    }
                    string call = text.Substring(at, end - at);
                    string last = call.Substring(call.LastIndexOf("},", StringComparison.Ordinal) + 2);
                    Check(last.Contains("nitializer"), Path.GetFileName(file) + " adds a button without the game's initializer");
                }
            }
        });

        yield return ("C9: in a custom difficulty the colony checkboxes go into the settings list, under its Tutorial row", () =>
        {
            string options = Source("BeaverBuddies", "Lobby", "NewGameColonyOptions.cs");
            string place = Body(options, "private void Place()");
            Check(place.Contains("customSettings.style.display.value != DisplayStyle.None") && place.Contains("list.Q(\"TutorialToggleCustomWrapper\")"),
                "the column no longer follows the page's Tutorial row");
            Check(Body(options, "private void Refresh()").Contains("Place();"), "the column is placed only on a mode change");
            string patches = Source("BeaverBuddies", "Lobby", "LobbyPatches.cs");
            Check(patches.Contains("[HarmonyPatch(typeof(NewGameModePanel), \"OnCustomizeButtonClicked\")]")
                && patches.Contains("[HarmonyPatch(typeof(NewGameModePanel), \"OnPredefinedModeButtonClicked\")]"), "a mode change no longer moves the column");
        });

        yield return ("C10: the texts say what the code does: the report question, a failed rehost, factions, the rejoin, the lost connection", () =>
        {
            Check(!Csv("BeaverBuddies.ClientDesynced.Message")!.Contains("bug report"), "the desync message asks a question public builds have no button for");
            Check(Csv("BeaverBuddies.ClientDesynced.ReportQuestion") != null, "the report question is gone");
            string events = Source("BeaverBuddies", "Events", "ConnectionEvents.cs");
            Check(events.Contains("if (bugReportMessageKey != null) reconnectMessage += \" \" + _loc.T(\"BeaverBuddies.ClientDesynced.ReportQuestion\");"),
                "the report question is not asked with the report button");
            Check(!Csv("BeaverBuddies.ClientDesynced.FailedToRehostMessage")!.Contains("Manually save and Host again"), "a failed rehost names the old flow");
            Check(Csv("BeaverBuddies.Colony.Overview.AwayNoSameFaction")!.Contains("being played"), "the status says no colony of its faction is in the game");
            Check(Csv("BeaverBuddies.Colony.Overview.HandToOtherFactionTooltip")!.StartsWith("The colonies of {0} and {1}"), "the tooltip calls two players factions");
            Check(Csv("BeaverBuddies.Colony.Handover.Tomorrow")!.Contains("nearest colony of its faction"), "the warning names any nearest colony in a mixed game");
            Check(Csv("BeaverBuddies.Rejoin.Waiting")!.StartsWith("Waiting for the host to open the waiting room"), "the rejoin's text is the old one");
            Check(!Csv("BeaverBuddies.Lobby.Colonies.SharedToSeparate")!.Contains("is the host's colony, and each player"), "the host's own page tells the host to found");
            Check(!Source("BeaverBuddies", "Connect", "SessionEndMessages.cs").Contains("(Save and Rehost)"), "the lost connection names only Save and Rehost");
            Check(!Csv("BeaverBuddies.Colony.Refused.NotStartedYet")!.Contains("unpause"), "the wait's refusal asks the host to unpause");
        });

        yield return ("C12: a Co-op Game page shows its checkbox slot only when it holds checkboxes", () =>
        {
            string page = Source("BeaverBuddies", "Lobby", "LobbyPage.cs");
            Check(page.Contains("optionsSlot.style.display = DisplayStyle.None;"), "an empty slot adds a gap to every room");
            Check(Body(page, "public void SetColonyOptions(VisualElement options)").Contains("optionsSlot.style.display = options != null ? DisplayStyle.Flex : DisplayStyle.None;"),
                "the slot is not shown with its checkboxes");
        });
    }
}
