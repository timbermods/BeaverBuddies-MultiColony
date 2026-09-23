using BeaverBuddies.Connect;

/// <summary>
/// The main menu's Join co-op game box (1.4.0-beta24): which of a Steam friend's games can be joined, in what order the
/// list shows them, and that the box, the host's lobby details and the waiting room's buttons are wired as intended.
/// </summary>
static class JoinBoxChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }

    static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root)!;
        Check(root != null, "could not find the repository root");
        return root!;
    }

    static string Source(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    const string Ours = "1.4.0-beta24";

    static FriendGame Game(ulong id, string name, FriendGameState state) => new FriendGame(id, id + 100, name, state, "Folktails - Plains - Normal", Ours);

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("Join box: a friend's lobby is joinable only when it is this version, open, and says so", () =>
        {
            Check(FriendGameRules.Classify("", "", "", Ours) == FriendGameState.Looking, "no data yet");
            Check(FriendGameRules.Classify(null, null, null, Ours) == FriendGameState.Looking);
            Check(FriendGameRules.Classify(Ours, "1", "1", Ours) == FriendGameState.WaitingRoom);
            Check(FriendGameRules.Classify(Ours, "1", "0", Ours) == FriendGameState.OpenGame, "a game hosted from a save, still open");
            Check(FriendGameRules.Classify(Ours, "1", "", Ours) == FriendGameState.OpenGame);
            Check(FriendGameRules.Classify(Ours, "0", "1", Ours) == FriendGameState.Started, "a waiting room that has started");
            Check(FriendGameRules.Classify(Ours, "", "", Ours) == FriendGameState.Started);
            Check(FriendGameRules.Classify("1.4.0-beta23", "1", "1", Ours) == FriendGameState.OtherVersion);
            // A host from before beta24 writes bb_open but no version: it can't be joined from this build either.
            Check(FriendGameRules.Classify("", "1", "", Ours) == FriendGameState.OtherVersion, "an older host was offered");
            foreach (FriendGameState state in Enum.GetValues(typeof(FriendGameState)))
                Check(FriendGameRules.Joinable(state) == (state == FriendGameState.WaitingRoom || state == FriendGameState.OpenGame), state.ToString());
        });

        yield return ("Join box: games you can join come first, then by name; a refresh keeps the same friend selected", () =>
        {
            var games = FriendGameRules.Order(new[]
            {
                Game(3, "zed", FriendGameState.Started), Game(2, "Bob", FriendGameState.WaitingRoom),
                Game(1, "anna", FriendGameState.OpenGame), Game(4, "Carl", FriendGameState.OtherVersion),
                Game(5, "Dee", FriendGameState.Looking),
            });
            Check(string.Join(",", games.Select(g => g.Name)) == "anna,Bob,Carl,Dee,zed", string.Join(",", games.Select(g => g.Name)));
            Check(FriendGameRules.SelectionAfterRefresh(games, 2) == 1, "Bob stays selected where he now is");
            Check(FriendGameRules.SelectionAfterRefresh(games, 3) == -1, "a game that started can't stay selected");
            Check(FriendGameRules.SelectionAfterRefresh(games, 99) == -1 && FriendGameRules.SelectionAfterRefresh(games, null) == -1);
            Check(FriendGameRules.Order(null).Count == 0);
        });

        yield return ("Join box: the main menu opens it with Steam, a game keeps the address box, and joining is the invite's own path", () =>
        {
            string ui = Source("BeaverBuddies", "Connect", "ClientConnectionUI.cs");
            Check(ui.Contains("AddJoinButton(__result, mainMenu: true)") && ui.Contains("AddJoinButton(__result, mainMenu: false)"),
                "the main menu's and the game's Join buttons are no longer told apart");
            Check(ui.Contains("mainMenu && SteamOverlayConnectionService.IsSteamEnabled") && ui.Contains("SteamMatchmaking.JoinLobby"),
                "the friends' box is shown without Steam, or joins another way than the lobby");
            string box = Source("BeaverBuddies", "Connect", "JoinCoopBox.cs");
            Check(!box.Contains(".ElementAt(") && box.Contains("LoadVisualTreeAsset(\"Common/NamedBoxTemplate\").CloneTree()"),
                "the named box must be used as an instance (its box is a content slot)");
            Check(box.Contains("panelStack.HideAndPush(box)"), "the box no longer takes the main menu's place, as the Load Game box does");
        });

        yield return ("Join box: every host's Steam lobby says its version, whether it is a waiting room, and what it is", () =>
        {
            string listener = Source("BeaverBuddies", "Steam", "SteamListener.cs");
            foreach (string key in new[] { "VersionKey", "RoomKey", "DescriptionKey", "HostKey" })
                Check(listener.Contains($"SetLobbyData(LobbyID, {key},"), $"the lobby no longer writes {key}");
            Check(Source("BeaverBuddies", "IO", "ServerEventIO.cs").Contains("steam.SetDetails(room != null, SteamDescription)"),
                "the server no longer hands the lobby its details");
            Check(Source("BeaverBuddies", "Lobby", "LobbySession.cs").Contains("SteamDescription ="), "a waiting room no longer describes itself");
            // A save is hosted only through its waiting room since 1.4.0-rc4: the room describes it (its settlement).
            Check(Source("BeaverBuddies", "Lobby", "LobbySession.cs").Contains("SteamDescription = setup.IsSave ? setup.Settlement"),
                "a hosted save no longer describes itself");
        });

        yield return ("Waiting room: its two buttons are one size (Cancel / Start Game, Leave / Ready), and the guest's reads Ready", () =>
        {
            string page = Source("BeaverBuddies", "Lobby", "LobbyPage.cs");
            Check(page.Contains("Next.RemoveFromClassList(\"menu-button--large-text\")") && page.Contains("Next.AddToClassList(\"menu-button--medium\")"),
                "Next is no longer the size of Back");
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
            Check(csv.Contains("BeaverBuddies.Lobby.Button.Ready,\"Ready\""), "the guest's button no longer reads Ready");
            Check(!csv.Contains("I'm ready"), "a string still says I'm ready");
        });
    }
}
