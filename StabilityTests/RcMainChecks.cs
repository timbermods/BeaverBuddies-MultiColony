using BeaverBuddies.Connect;

/// <summary>
/// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
/// that need no game: the main session: the release-readiness leads (R), beta24's Join box (B24) and the findings made while planning (P). One check per finding, named after it.
/// </summary>
static class RcMainChecks
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
        yield return ("P-1: Reset all resets the transmitter's whole partition and Reset only the transmitter, as the game's panel does", () =>
        {
            string replay = Body(Source("BeaverBuddies", "Events", "AutomationEvents.cs"), "class ResetTransmitterEvent");
            string body = Body(replay, "public override void Replay(IReplayContext context)");
            int all = body.IndexOf("if (resetAll)", StringComparison.Ordinal);
            int partition = body.IndexOf("ResetPartition", StringComparison.Ordinal);
            int single = body.IndexOf("transmitter.Reset()", StringComparison.Ordinal);
            int otherwise = body.IndexOf("else", StringComparison.Ordinal);
            Check(all >= 0 && partition > all && single > all, "the replay no longer branches on resetAll into both resets");
            Check(partition < otherwise && single > otherwise, "Reset all (resetAll) must reset the partition, and Reset (else) the one transmitter");
        });

        yield return ("B24-a: a friend's lobby text is one short line, and the list shows it without rich text", () =>
        {
            Check(FriendGameRules.OneLine(null, 10) == "" && FriendGameRules.OneLine("   ", 10) == "", "empty text");
            Check(FriendGameRules.OneLine("  Bob\r\nthe\tbuilder  ", 64) == "Bob the builder", "line breaks and tabs become one space, trimmed");
            Check(FriendGameRules.OneLine(new string('x', 500), FriendGameRules.DescriptionLimit).Length == FriendGameRules.DescriptionLimit, "cut to the limit");
            Check(FriendGameRules.OneLine("ab cd", 3) == "ab", "a space is never the last character");
            var game = new FriendGame(1, 2, "<size=200>Bob\n", FriendGameState.WaitingRoom, new string('d', 1000), "1.4.0\u0000x");
            Check(game.Name == "<size=200>Bob" && game.Description.Length == FriendGameRules.DescriptionLimit && game.Version == "1.4.0 x",
                "the row's text is cleaned when it is made");
            string bind = Body(Source("BeaverBuddies", "Connect", "JoinCoopBox.cs"), "private static void Show(Label label, string text)");
            Check(bind.Contains("enableRichText = false"), "the row's labels must show lobby text without rich text");
        });

        yield return ("B24-b: Enter while typing an address connects to it, even with a friend's game selected", () =>
        {
            string confirmed = Body(Source("BeaverBuddies", "Connect", "JoinCoopBox.cs"), "public bool OnUIConfirmed()");
            int typing = confirmed.IndexOf("Typing()", StringComparison.Ordinal);
            int join = confirmed.IndexOf("Join()", StringComparison.Ordinal);
            Check(typing >= 0 && join > typing, "the box must look at the address field's focus before joining the selected friend");
        });
    }
}
