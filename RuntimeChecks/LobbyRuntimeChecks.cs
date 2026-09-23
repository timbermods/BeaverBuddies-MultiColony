#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

// A new game's waiting room (1.4.0-beta18, design/PRE-GAME-LOBBY-PLAN.md), against the compiled mod, the game's
// assemblies and the game's own UI files (StreamingAssets/Modding/UI.zip): every game member it hooks or reads is still
// there, the game still does things in the order the waiting room relies on, its pages use only the game's templates and
// the classes of the style sheets the main menu loads, and a game started from it never waits for late joiners.
internal static class LobbyRuntimeChecks
{
    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void Run(Assembly mod, string managedPath, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        Type Mod(string type) => mod.GetType(type, true)!;
        void Has(Type type, string member, string what)
        {
            if (type.GetMember(member, all).Length == 0) throw new Exception($"the game has no {type.Name}.{member} ({what})");
        }

        test("Waiting room: every game member it hooks or reads is still there", () =>
        {
            Type mode = Game("Timberborn.MainMenuPanels", "Timberborn.MainMenuPanels.NewGameModePanel");
            foreach (string member in new[] { "GetPanel", "UpdateNextButton", "TryGetValidatedGameMode", "_nextButton", "_summary",
                "_factionSpec", "_map", "_root", "_predefinedGameMode" })
                Has(mode, member, "Host co-op game and the waiting room's setup");
            Type spec = Game("Timberborn.NewGameConfigurationSystem", "Timberborn.NewGameConfigurationSystem.GameModeSpec");
            Has(spec, "DisplayNameLocKey", "the difficulty's name for a guest");
            Type config = Game("Timberborn.NewGameConfigurationSystem", "Timberborn.NewGameConfigurationSystem.NewGameConfiguration");
            if (!config.GetConstructors().Any(c => c.GetParameters().Length == 4 && c.GetParameters()[3].ParameterType == typeof(string)))
                throw new Exception("NewGameConfiguration no longer takes a settlement name");
            Type parameters = Game("Timberborn.GameSceneLoading", "Timberborn.GameSceneLoading.GameSceneParameters");
            Has(parameters, "CreateNewGameParameters", "making the world");
            Has(parameters, "CreateGameSaveParameters", "loading its save");
            Has(Game("Timberborn.GameSceneLoading", "Timberborn.GameSceneLoading.GameSceneLoader"), "_sceneLoader", "a guest's loading tip");
            Type loader = Game("Timberborn.SceneLoading", "Timberborn.SceneLoading.ISceneLoader");
            if (loader.GetMethod("LoadScene", new[] { Game("Timberborn.SceneLoading", "Timberborn.SceneLoading.ISceneParameters"), typeof(string) }) == null)
                throw new Exception("ISceneLoader.LoadScene no longer takes a tip");
            Type screen = Game("Timberborn.SceneLoading", "Timberborn.SceneLoading.LoadingScreen");
            if (screen.GetMethod("Disable", all, Type.EmptyTypes) == null) throw new Exception("the game has no LoadingScreen.Disable()");
            Type saver = Game("Timberborn.GameSaveRuntimeSystem", "Timberborn.GameSaveRuntimeSystem.GameSaver");
            Has(saver, "QueueSaveSkippingNameValidation", "the tick-0 save");
            Type repository = Game("Timberborn.GameSaveRepositorySystem", "Timberborn.GameSaveRepositorySystem.GameSaveRepository");
            if (repository.GetMethod("CreateDirectoryForSettlement", new[] { typeof(string) }) == null)
                throw new Exception("GameSaveRepository.CreateDirectoryForSettlement(string) is gone (the settlement's name check)");
            Has(Game("Timberborn.Autosaving", "Timberborn.Autosaving.AutosaveNameService"), "Timestamp", "the save's name");
            Has(Game("Timberborn.FactionSystem", "Timberborn.FactionSystem.FactionSpecService"), "GetFaction", "a guest's faction logo");
            Has(Game("Timberborn.CoreUI", "Timberborn.CoreUI.VisualElementLoader"), "LoadVisualTreeAsset", "the page's template");
            Has(Game("Timberborn.CoreUI", "Timberborn.CoreUI.LocalizableLabel"), "_textLocKey", "keying the page's title");
            Has(Game("Timberborn.CoreUI", "Timberborn.CoreUI.VisualElementExtensions"), "SetConfirmCancelActions", "Enter in the name box");
            Type stack = Game("Timberborn.CoreUI", "Timberborn.CoreUI.PanelStack");
            foreach (string member in new[] { "HideAndPush", "PushOverlay", "PushDialog", "Pop", "IsPanelOnTop" }) Has(stack, member, "the pages");
            Has(Game("Timberborn.Common", "Timberborn.Common.NewGameInitializedEvent"), ".ctor", "when the world is ready to save");
        });

        test("Waiting room: its Harmony patches still find their game methods", () =>
        {
            foreach (string patcher in new[] { "BeaverBuddies.Lobby.NewGameModePanelHostButtonPatcher",
                "BeaverBuddies.Lobby.NewGameModePanelUpdateNextButtonPatcher", "BeaverBuddies.Lobby.LoadingScreenDisablePatcher" })
            {
                var target = Mod(patcher).GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch")
                    ?? throw new Exception(patcher + " has no HarmonyPatch attribute");
                Type type = (Type)target.ConstructorArguments[0].Value!;
                string name = (string)target.ConstructorArguments[1].Value!;
                if (type.GetMethod(name, all) == null) throw new Exception($"{patcher}: {type.Name}.{name} is gone");
            }
        });

        test("Waiting room: the game saves a new world's first save before it unpauses, in the frame it says the world is ready", () =>
        {
            // LobbyWorldMaker queues the save when the game posts NewGameInitializedEvent; the game writes a queued save in
            // LateUpdate, and unpauses on its next step (GameInitializer: PostSpawnBeavers, then UnpauseGame).
            Type initializer = Game("Timberborn.GameStartup", "Timberborn.GameStartup.GameInitializer");
            MethodInfo post = initializer.GetMethod("PostSpawnBeavers", all) ?? throw new Exception("no GameInitializer.PostSpawnBeavers");
            if (!IlScan.Instructions(post).Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "NewGameInitializedEvent"))
                throw new Exception("PostSpawnBeavers no longer posts NewGameInitializedEvent");
            Type adapter = Game("Timberborn.GameSaveRuntimeSystem", "Timberborn.GameSaveRuntimeSystem.GameSaverUnityAdapter");
            MethodInfo late = adapter.GetMethod("LateUpdate", all) ?? throw new Exception("no GameSaverUnityAdapter.LateUpdate");
            if (!IlScan.Instructions(late).Any(i => i.Calls && i.Member?.Name == "SaveQueued"))
                throw new Exception("a queued save is no longer written in LateUpdate");
        });

        test("Waiting room: the templates and names its pages use are in the game's UI files", () =>
        {
            using ZipArchive ui = UiZip(managedPath);
            var wanted = new Dictionary<string, string[]>
            {
                ["Views/MainMenu/NewGameTemplate.uxml"] = new[] { "name=\"HeaderText\"", "name=\"BackButton\"", "name=\"NextButton\"", "new-game__main-content" },
                ["Views/Modding/ModItem.uxml"] = new[] { "name=\"PriorityWrapper\"", "name=\"ModToggle\"", "name=\"ModIcon\"", "name=\"ModName\"",
                    "name=\"ModVersion\"", "name=\"WarningIcon\"" },
                // The settlement's name is asked in the game's own input box (1.4.0-beta22; the in-game settlement box
                // drew without its in-game style sheets in the main menu).
                ["Views/Core/InputBox.uxml"] = new[] { "name=\"Message\"", "name=\"Input\"", "name=\"ConfirmButton\"", "name=\"CancelButton\"",
                    "CoreStyle.uss" },
                ["Views/Core/DialogBox.uxml"] = new[] { "name=\"Message\"", "name=\"CancelButton\"", "name=\"InfoButton\"", "name=\"ConfirmButton\"" },
                // The Join co-op game box (1.4.0-beta24): the Load Game box's named box and save rows.
                ["Views/Common/NamedBoxTemplate.uxml"] = new[] { "name=\"Header\"", "name=\"CloseButton\"", "content-container=\"true\"" },
                ["Views/Options/GameSaveItemElement.uxml"] = new[] { "name=\"DisplayName\"", "name=\"GameTime\"", "name=\"Timestamp\"",
                    "list-view__item-background" },
                ["Views/Options/LoadGameBox.uxml"] = new[] { "panel-list-view text--default scroll--green-decorated", "load-box__list-title", "box-buttons" },
            };
            foreach (var pair in wanted)
            {
                string text = Read(ui, pair.Key);
                foreach (string needle in pair.Value)
                    if (!text.Contains(needle)) throw new Exception($"{pair.Key} has no {needle}");
            }
            // The page is built from the template itself: its title must still need a key (the mod sets one first).
            if (Regex.IsMatch(Read(ui, "Views/MainMenu/NewGameTemplate.uxml"), "name=\"HeaderText\"[^>]*text-loc-key"))
                throw new Exception("NewGameTemplate's title now has a key of its own: check LobbyPage still sets the right one");
        });

        test("Waiting room: a template taken as one element has no content slot (the page is built around NewGameTemplate's)", () =>
        {
            // A template with content-container="true" clones into a TemplateContainer that forwards its children to that
            // slot, so ElementAt(0), which VisualElementLoader.LoadVisualElement also uses, finds the empty slot instead of
            // the root: 1.4.0-beta18 to beta21 threw there the moment the waiting room opened. Every template the mod names
            // next to LoadVisualElement, or next to LoadVisualTreeAsset in a method that calls ElementAt, is read here.
            using ZipArchive ui = UiZip(managedPath);
            var single = new SortedSet<string>();
            foreach (Type type in mod.GetTypes())
                foreach (MethodBase method in type.GetMethods(all | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                    .Concat(type.GetConstructors(all | BindingFlags.DeclaredOnly)))
                {
                    if (method.GetMethodBody() == null) continue;
                    var code = IlScan.Instructions(method);
                    bool elementAt = code.Any(i => i.Calls && i.Member?.Name == "ElementAt" && i.Member.DeclaringType?.Name == "VisualElement");
                    for (int at = 1; at < code.Count; at++)
                    {
                        var call = code[at];
                        if (!call.Calls || call.Member?.DeclaringType?.Name != "VisualElementLoader") continue;
                        bool takesOne = call.Member.Name == "LoadVisualElement" || (call.Member.Name == "LoadVisualTreeAsset" && elementAt);
                        if (takesOne && code[at - 1].Text != null) single.Add(code[at - 1].Text!);
                    }
                }
            if (!single.Contains("Modding/ModItem") || !single.Contains("Core/DialogBox"))
                throw new Exception("found only " + string.Join(", ", single) + ": the scan is not reading the mod's loads");
            var slotted = single.Where(name => Read(ui, $"Views/{name}.uxml").Contains("content-container=\"true\"")).ToList();
            if (slotted.Count > 0) throw new Exception("taken as one element, but it has a content slot: " + string.Join(", ", slotted));
            if (!Read(ui, "Views/MainMenu/NewGameTemplate.uxml").Contains("content-container=\"true\""))
                throw new Exception("NewGameTemplate has no content slot any more: check how LobbyPage builds the page");
        });

        test("Waiting room: every class its pages add is defined in a style sheet the main menu loads", () =>
        {
            using ZipArchive ui = UiZip(managedPath);
            string title = Read(ui, "Views/MainMenu/TitleScreen.uxml");
            var defined = new HashSet<string>();
            var sheets = Regex.Matches(title, "Style src=\"/Assets/Resources/UI/(Views/[^\"]+\\.uss)\"").Select(m => m.Groups[1].Value).ToList();
            if (sheets.Count < 5) throw new Exception("found only " + sheets.Count + " style sheets on the title screen");
            foreach (string sheet in sheets)
                foreach (Match m in Regex.Matches(Read(ui, sheet), "\\.([A-Za-z_][A-Za-z0-9_-]*)")) defined.Add(m.Groups[1].Value);
            var used = (string[])Mod("BeaverBuddies.Lobby.LobbyPage").GetField("ClassesUsed", all)!.GetValue(null)!;
            var missing = used.Where(c => !defined.Contains(c)).ToList();
            if (missing.Count > 0) throw new Exception("not in the main menu's style sheets: " + string.Join(", ", missing));
            // The settlement's name box carries CoreStyle itself (Core/InputBox): its classes must be there.
            var core = new HashSet<string>(Regex.Matches(Read(ui, "Views/Core/CoreStyle.uss"), "\\.([A-Za-z_][A-Za-z0-9_-]*)").Select(m => m.Groups[1].Value));
            var box = (string[])Mod("BeaverBuddies.Lobby.SettlementNamePanel").GetField("ClassesUsed", all)!.GetValue(null)!;
            var boxMissing = box.Where(c => !core.Contains(c)).ToList();
            if (boxMissing.Count > 0) throw new Exception("not in CoreStyle, which the input box brings: " + string.Join(", ", boxMissing));
            // The Join co-op game box is a main-menu box too.
            var join = (string[])Mod("BeaverBuddies.Connect.JoinCoopBox").GetField("ClassesUsed", all)!.GetValue(null)!;
            var joinMissing = join.Where(c => !defined.Contains(c)).ToList();
            if (joinMissing.Count > 0) throw new Exception("the Join box's classes, not in the main menu's style sheets: " + string.Join(", ", joinMissing));
        });

        test("Join co-op game box: the Steam calls it lists friends' games and joins them with are in the game's Steamworks", () =>
        {
            Assembly steam = Assembly.Load("com.rlabrecque.steamworks.net");
            Type Steam(string type) => steam.GetType("Steamworks." + type, true)!;
            foreach (var (type, member) in new[] { ("SteamFriends", "GetFriendCount"), ("SteamFriends", "GetFriendByIndex"),
                ("SteamFriends", "GetFriendGamePlayed"), ("SteamFriends", "GetFriendPersonaName"), ("SteamMatchmaking", "RequestLobbyData"),
                ("SteamMatchmaking", "GetLobbyData"), ("SteamMatchmaking", "JoinLobby"), ("SteamMatchmaking", "SetLobbyData"),
                ("SteamUtils", "GetAppID"), ("FriendGameInfo_t", "m_steamIDLobby"), ("FriendGameInfo_t", "m_gameID"), ("CGameID", "AppID") })
                Has(Steam(type), member, "the Join co-op game box");
            // The keys a host writes and a friend's box reads are the same strings.
            Type listener = Mod("BeaverBuddies.Steam.SteamListener");
            var keys = new[] { "OpenKey", "VersionKey", "RoomKey", "DescriptionKey", "HostKey" }.Select(k => (string)listener.GetField(k, all)!.GetValue(null)!).ToList();
            if (keys.Distinct().Count() != 5 || keys.Any(k => string.IsNullOrEmpty(k) || k.Length > 255))
                throw new Exception("the lobby's keys are not five distinct Steam lobby keys: " + string.Join(", ", keys));
            Has(Steam("SteamFriends"), "GetPersonaName", "the host's name in its lobby");
        });

        test("Waiting room for a save: every save is hosted through it, from the main menu (a game goes there first)", () =>
        {
            // LoadAndHost opens the waiting room (the main menu binds its page; a game does not). Since 1.4.0-rc4 there is
            // no other way: the original BeaverBuddies dialog, which let guests load as soon as they connected, is gone,
            // and a game hosts itself by saving and opening the page in the main menu (HostCoopFlow).
            MethodInfo loadAndHost = Mod("BeaverBuddies.Connect.ServerHostingUtils").GetMethod("LoadAndHost", all)!;
            var code = IlScan.Instructions(loadAndHost);
            if (!code.Any(i => i.Calls && i.Is("BeaverBuddies.Lobby.LobbyHostPanel", "OpenForSave")))
                throw new Exception("Host co-op game on a save no longer opens the waiting room");
            if (code.Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.FullName == "BeaverBuddies.IO.ServerEventIO"))
                throw new Exception("a save is hosted without its waiting room again (its own server and dialog)");
            // Only the main menu has the page.
            var menu = Mod("BeaverBuddies.ConnectionMenuConfigurator").GetMethod("Configure", all)!;
            var game = Mod("BeaverBuddies.ReplayConfigurator").GetMethod("Configure", all)!;
            bool Binds(MethodInfo configure) => IlScan.Instructions(configure).Any(i => i.Calls && i.Member is MethodInfo m
                && m.IsGenericMethod && m.GetGenericArguments().Any(t => t.FullName == "BeaverBuddies.Lobby.LobbyHostPanel"));
            if (!Binds(menu)) throw new Exception("the main menu no longer binds the host page");
            if (Binds(game)) throw new Exception("a game binds the host page: hosting from a game would open the waiting room");
            // What the page reads of a save: its date from the metadata, worded as the Load Game box words it.
            Has(Game("Timberborn.GameSaveRepositorySystem", "Timberborn.GameSaveRepositorySystem.GameSaveDeserializer"), "ReadFromSaveFile", "the save's date");
            Type metadata = Game("Timberborn.SaveMetadataSystem", "Timberborn.SaveMetadataSystem.SaveMetadata");
            Has(metadata, "Cycle", "the save's date");
            Has(metadata, "Day", "the save's date");
            Has(Game("Timberborn.UIFormatters", "Timberborn.UIFormatters.TimestampFormatter"), "FormatLongLocalized", "the save's date");
        });

        test("Waiting room: the host's word that joining closed at Start survives the trip through the event JSON", () =>
        {
            Type replayEvent = Mod("BeaverBuddies.Events.ReplayEvent");
            Type init = Mod("BeaverBuddies.Events.InitializeClientEvent");
            var json = Mod("BeaverBuddies.IO.JsonSettings");
            MethodInfo serialize = json.GetMethod("Serialize")!.MakeGenericMethod(replayEvent);
            MethodInfo deserialize = json.GetMethod("Deserialize")!.MakeGenericMethod(replayEvent);
            object e = Activator.CreateInstance(init, true)!;
            init.GetField("joiningClosedAtStart")!.SetValue(e, true);
            object back = deserialize.Invoke(null, new object[] { serialize.Invoke(null, new[] { e })! })!;
            if (back.GetType() != init || !(bool)init.GetField("joiningClosedAtStart")!.GetValue(back)!) throw new Exception("the flag was lost");
        });

        test("Waiting room: the host's verdict and the founding prompt read joining-closed-at-Start; the host's hand-over buttons don't", () =>
        {
            bool Reads(MethodBase method) => IlScan.Instructions(method).Any(i => i.Calls && i.Is("BeaverBuddies.Colonies.ColonySession", "get_JoiningClosedAtStart"));
            bool Asks(MethodBase method) => IlScan.Instructions(method).Any(i => i.Calls && i.Is("BeaverBuddies.Colonies.ColonyRules", "WaitsForStart"));
            MethodInfo allow = Mod("BeaverBuddies.Colonies.ColonyRulesService").GetMethod("AllowOnHost", all)!;
            if (!Reads(allow) || !Asks(allow)) throw new Exception("the host's verdict no longer passes the flag to WaitsForStart");
            MethodInfo waiting = Mod("BeaverBuddies.Colonies.ColonyFoundingService").GetProperty("WaitingForStart", all)!.GetMethod!;
            if (!Reads(waiting)) throw new Exception("the founding prompt still waits for the first tick after a waiting room");
            MethodInfo handOver = Mod("BeaverBuddies.Colonies.ColonyLifecycle").GetMethod("HostMayHandOver", all)!;
            if (!Asks(handOver) || Reads(handOver)) throw new Exception("the host's Hand to buttons no longer always wait for the first tick");
        });
    }

    static ZipArchive UiZip(string managedPath)
    {
        string path = Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip"));
        if (!File.Exists(path)) throw new Exception("the game's UI.zip is not at " + path);
        return ZipFile.OpenRead(path);
    }

    static string Read(ZipArchive zip, string entry)
    {
        ZipArchiveEntry file = zip.GetEntry(entry) ?? throw new Exception("UI.zip has no " + entry);
        using var reader = new StreamReader(file.Open());
        return reader.ReadToEnd();
    }
}
