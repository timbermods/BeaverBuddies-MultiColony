/// <summary>
/// 1.4.0-rc14, two things Kyler saw playing rc12: a long good's name ("Grilled potatoes") wrapped onto a second line that
/// spilled out of the Trading Post's goods selector, and the message for a removed Trading Post read badly ("What waited
/// on its halves…"). Checks that need no game.
/// </summary>
static class Rc14Checks
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
        yield return ("rc14: a good's name stays on one line in the Trading Post's 32 px selector, cut with an ellipsis as the game's dropdowns do", () =>
        {
            string side = Body(Source("BeaverBuddies", "Colonies", "TradingPostFragment.cs"), "private OfferSide BuildOfferSide(int side, string caption)");
            foreach (string rule in new[] { "o.Name.style.whiteSpace = WhiteSpace.NoWrap;", "o.Name.style.overflow = Overflow.Hidden;",
                "o.Name.style.textOverflow = TextOverflow.Ellipsis;", "o.Name.style.minWidth = 0;" })
                Check(side.Contains(rule), "the selector's name can wrap or spill again: " + rule + " is gone");
            Check(side.Contains("s.minWidth = 0;") && side.Contains("s.height = 32;"), "the selector no longer shrinks to its row (or is no longer 32 px high)");
        });

        yield return ("rc14: a removed Trading Post's message says it plainly, with no halves", () =>
        {
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
            int at = csv.IndexOf("\nBeaverBuddies.Colony.Trade.Notice.PostRemoved,\"", StringComparison.Ordinal);
            Check(at >= 0, "the removed post's message is gone");
            string text = csv.Substring(at, csv.IndexOf("\",\"", at, StringComparison.Ordinal) - at);
            Check(!text.Contains("half") && text.Contains("A Trading Post was removed, so its exchange has ended.")
                && text.Contains("left on the ground for any colony to collect"), "the removed post's message reads as rc12's: " + text.Trim());
        });
    }
}
