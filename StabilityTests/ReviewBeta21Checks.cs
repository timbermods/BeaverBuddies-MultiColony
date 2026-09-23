using System.Text.RegularExpressions;

/// <summary>
/// The review of 1.4.0-beta18 to beta20 (design/REVIEW-FINDINGS-1.4.0-beta18-20.md): one check per finding that can be
/// checked without the game, named after the finding.
/// </summary>
static class ReviewBeta21Checks
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

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("J1: the guest's LoadMap reads nothing from the singleton registry after emptying it", () =>
        {
            string body = Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private void LoadMap(");
            int reset = body.IndexOf("SingletonManager.Reset()", StringComparison.Ordinal);
            Check(reset > 0, "LoadMap no longer resets the registry: re-check this check");
            string after = body.Substring(reset + "SingletonManager.Reset()".Length);
            foreach (string reader in new[] { "RegisteredLocalizationService", "SingletonManager.GetSingleton", ".Instance" })
                Check(!after.Contains(reader), $"LoadMap uses {reader} after SingletonManager.Reset(), when nothing is registered");
            string check = Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private void CheckWaitingRoom(");
            Check(check.Contains("saveReceived"), "CheckWaitingRoom must stand down once the save has come (JoinFlowRules.CheckWaitingRoom)");
        });

        yield return ("B-1: the mod never removes a character with a bare EntityService.Delete (Character.DestroyCharacter kills it first)", () =>
        {
            // The game lets go of a character (CharacterPopulation, BeaverPopulation, its district and home) only on
            // CharacterKilledEvent. A bare Delete leaves a destroyed beaver in those lists, and the next explosion or
            // Beehive check reads its Transform and throws (beta20's faction switch did this).
            var deleteCall = new Regex(@"(?:_entityService|entityService|EntityService>\(\))\s*\.\s*Delete\(\s*([A-Za-z_][\w\.]*)");
            var declaration = new Regex(@"\b(Beaver|Bot|Character|Adult|Child|Worker|Citizen)\s+([A-Za-z_]\w*)\s*(?:=|in\b|;|\))");
            var characterName = new Regex(@"^(beaver|bot|character|adult|child|worker|citizen)s?\w*$", RegexOptions.IgnoreCase);
            var hits = new List<string>();
            foreach (string file in Directory.EnumerateFiles(Path.Combine(Root(), "BeaverBuddies"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                string text = File.ReadAllText(file);
                var declared = new HashSet<string>(declaration.Matches(text).Select(m => m.Groups[2].Value));
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    Match call = deleteCall.Match(lines[i]);
                    if (!call.Success) continue;
                    string argument = call.Groups[1].Value.Split('.')[0];
                    if (declared.Contains(argument) || characterName.IsMatch(argument))
                        hits.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
            Check(hits.Count == 0, "bare Delete of a character: " + string.Join("; ", hits));
        });

        yield return ("J5c: the host's factions are forgotten when a session begins (host or guest), never as a game scene is set up", () =>
        {
            // A waiting room latches them at Start, and a slow guest's start message can be built after the host's game
            // scene is set up: forgetting them there sent that guest none.
            Check(!Body(Source("BeaverBuddies", "Factions", "MixedFactions.cs"), "public static void Reset(").Contains("ForgetHostFactions()"),
                "MixedFactions.Reset forgets the host's factions");
            Check(Body(Source("BeaverBuddies", "Colonies", "ColonySession.cs"), "public static void BeginHostSession(").Contains("ForgetHostFactions()"),
                "a new host session keeps an earlier one's factions");
            Check(Body(Source("BeaverBuddies", "Connect", "ClientConnectionService.cs"), "private bool TryToConnect(ISocketStream").Contains("ForgetHostFactions()"),
                "a new join keeps an earlier host's factions");
            Check(Body(Source("BeaverBuddies", "Lobby", "LobbySession.cs"), "public void Start(").Contains("LatchHostFactions"),
                "a mixed waiting room no longer latches the host's factions at Start");
        });

        yield return ("C-C1: a new game is mixed only with exactly the game's two factions (a faction mod keeps it one faction, and the Game Mode page says why)", () =>
        {
            string capture = Source("BeaverBuddies", "Factions", "NewGameFactionCapture.cs");
            string possible = Body(capture, "public bool MixedPossible(out string lockedFaction, out bool notTwoFactions)");
            Check(possible.Contains("Count() != 2") && possible.Contains("notTwoFactions = true"), "MixedPossible no longer asks for exactly two factions");
            Check(Body(capture, "public bool MixedAvailable(out string lockedFaction, out bool notTwoFactions)").Contains("MixedPossible("),
                "MixedAvailable no longer asks MixedPossible");
            // Since 1.4.0-rc3 the Game Mode page greys Mixed factions and its tooltip says why (the room said it before).
            Check(Source("BeaverBuddies", "Lobby", "NewGameColonyOptions.cs").Contains("BeaverBuddies.NewGame.MixedFactions.NotTwo"),
                "the Game Mode page no longer says why a game is one faction");
        });

        yield return ("Playtest (beta23): a guest's waiting room opens only once a page is on top, never over the Steam overlay's blocker", () =>
        {
            // HideAndPush hides only the top panel. With the game's SteamOverlayInputBlocker on top (an invite accepted in
            // the overlay), the page hid the blocker and shared the screen with the main menu, half height each.
            string pump = Body(Source("BeaverBuddies", "Lobby", "LobbyGuestPanel.cs"), "private void Pump(");
            int wait = pump.IndexOf("PageOnTop()", StringComparison.Ordinal), open = pump.IndexOf("Open(current", StringComparison.Ordinal);
            Check(wait > 0 && open > wait, "the guest's page opens without waiting for a page on top");
            Check(Body(Source("BeaverBuddies", "Lobby", "LobbyGuestPanel.cs"), "private bool PageOnTop(").Contains("IsOverlay"),
                "PageOnTop no longer asks whether the top panel is an overlay");
        });

        yield return ("Playtest (beta23): each row says ready once, on its right; the Mods window's checkbox stays hidden", () =>
        {
            string page = Source("BeaverBuddies", "Lobby", "LobbyPage.cs");
            Check(page.Contains("Root.Q<Toggle>(\"ModToggle\")?.ToggleDisplayStyle(false)"), "a row shows the Mods window's checkbox again");
            Check(!page.Contains(".ElementAt("), "LobbyPage takes a template's element by index again (NewGameTemplate forwards to its content slot)");
        });

        yield return ("J11a: a Steam lobby made after the host pressed Start opens closed", () =>
        {
            string created = Body(Source("BeaverBuddies", "Steam", "SteamListener.cs"), "private void OnLobbyCreated(");
            Check(created.Contains("closed ? \"0\" : \"1\"") && created.Contains("SetLobbyJoinable(LobbyID, false)"), "OnLobbyCreated opens the lobby whatever happened first");
            Check(Body(Source("BeaverBuddies", "Steam", "SteamListener.cs"), "public void CloseToNewGuests(").Contains("closed = true"), "CloseToNewGuests no longer notes it for a lobby still being made");
        });
    }
}
