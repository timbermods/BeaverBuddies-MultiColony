/// <summary>
/// 1.4.0-rc13: a guest's split separates science. Found your own colony used to keep one pool of science and unlocks for
/// every colony; now each colony earns its own from the split on, every colony keeps the unlocks made so far, and the
/// science earned so far stays with the host's colony (Kyler, 2026-09-25, after a playtest of rc12). Checks that need no game.
/// </summary>
static class Rc13Checks
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
        yield return ("rc13: a guest's split separates science; every colony keeps the unlocks so far, the host's colony the points", () =>
        {
            string found = Body(Source("BeaverBuddies", "Colonies", "ColonyFoundingService.cs"),
                "public void Found(Placement placement, int slot, ColonyStartingSettings settings, string faction = null)");
            Check(found.Contains("separateScience: true,") && found.Contains("newGame: false, unlocksForEveryColony: true"),
                "a split keeps one pool of science again, or gives only the host's colony the unlocks");
            // The science service: the points so far go to the first colony (the host's in a shared game), the unlocks to
            // every colony when asked.
            string science = Body(Source("BeaverBuddies", "Colonies", "ColonyScienceService.cs"), "public void Enable(bool unlocksForEveryColony)");
            Check(science.Contains("points[0] = shared;"), "the science earned so far no longer stays with the first colony");
            Check(science.Contains("if (i == 0 || unlocksForEveryColony) unlocked[i].UnionWith(sharedUnlocked);")
                && science.Contains("if (i == 0 || unlocksForEveryColony) workerUnlocked[i].UnionWith(sharedWorkers);"),
                "the other colonies don't get the buildings and bot types unlocked so far");
            string mode = Source("BeaverBuddies", "Colonies", "ColonyModeService.cs");
            Check(mode.Contains("ColonyScienceService.Instance?.Enable(unlocksForEveryColony);"), "the mode no longer passes the unlock rule to the science");
        });

        yield return ("rc13: only the split changes: a new game gives every colony the unlocks, a save made separate keeps them the host's", () =>
        {
            string starts = Source("BeaverBuddies", "MultiStart", "MultiStartPatches.cs");
            Check(starts.Contains("scienceOne, newGame: true, unlocksForEveryColony: true);")
                && starts.Contains("separateScience, newGame: true, unlocksForEveryColony: true);"), "a new game's colonies no longer start with the same unlocks");
            string conversion = Body(Source("BeaverBuddies", "Colonies", "SaveConversion.cs"), "public override void Replay(IReplayContext context)");
            Check(conversion.Contains("newGame: false, unlocksForEveryColony: false);"), "a save made separate at Start now shares its unlocks (its tooltip says the friends start with none)");
        });

        yield return ("rc13: the split's texts say the science is separate and the unlocks kept; nothing says it stays one pool", () =>
        {
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
            int at = csv.IndexOf("\nBeaverBuddies.Colony.Split.Confirm,\"", StringComparison.Ordinal);
            Check(at >= 0, "no split confirmation");
            string confirm = csv.Substring(at, csv.IndexOf("\",\"", at, StringComparison.Ordinal) - at);
            Check(!confirm.Contains("stay shared") && confirm.Contains("keeps every unlock made so far") && confirm.Contains("earns its own science"),
                "the split's confirmation still says science and unlocks stay shared");
            Check(csv.Contains("BeaverBuddies.Colony.Founding.SplitDone,\"Your colony is founded. This game now has separate colonies, for good, and each colony earns its own science.\""),
                "the founder isn't told the science is now their own");
            foreach (var (file, text) in new[] { ("README.md", Source("README.md")), ("TWO-COLONIES.md", Source("TWO-COLONIES.md")),
                ("docs/faq.html", Source("docs", "faq.html")), ("docs/index.html", Source("docs", "index.html")) })
                Check(!text.Contains("science stays one pool") && !text.Contains("Science stays one pool"), file + " still says a split keeps science one pool");
        });
    }
}
