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
                ["Views/Game/SettlementNameBox.uxml"] = new[] { "name=\"Input\"", "name=\"ConfirmButton\"", "name=\"RelocateButton\"",
                    "name=\"ResetStartLocation\"", "text-loc-key=\"Saving.NameSettlement\"" },
                ["Views/Core/DialogBox.uxml"] = new[] { "name=\"Message\"", "name=\"CancelButton\"", "name=\"InfoButton\"", "name=\"ConfirmButton\"" },
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
        });

        test("Waiting room for a save: Host co-op game opens it in the main menu, and keeps the dialog in a game", () =>
        {
            // LoadAndHost opens the waiting room when the host page is there (the main menu binds it; a game does not),
            // and otherwise goes on to its own server and dialog (Options → Load and Save and Rehost in a game).
            MethodInfo loadAndHost = Mod("BeaverBuddies.Connect.ServerHostingUtils").GetMethod("LoadAndHost", all)!;
            var code = IlScan.Instructions(loadAndHost);
            if (!code.Any(i => i.Calls && i.Is("BeaverBuddies.Lobby.LobbyHostPanel", "OpenForSave")))
                throw new Exception("Host co-op game on a save no longer opens the waiting room");
            if (!code.Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.FullName == "BeaverBuddies.IO.ServerEventIO"))
                throw new Exception("hosting from inside a game lost its own dialog");
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
