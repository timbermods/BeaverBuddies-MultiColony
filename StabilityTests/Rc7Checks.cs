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
    }
}
