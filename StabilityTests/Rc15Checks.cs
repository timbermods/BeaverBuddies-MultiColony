using BeaverBuddies.Panel;
using TimberNet;

/// <summary>
/// 1.4.0-rc15, from Kyler: a trade offer's message stays on screen until the player clicks it away, a click goes to the
/// Trading Post with the offer, and it chimes; a chat message chimes too; and the cancelled exchange's message is plain
/// English ("What waited on each half goes back to its own colony" read badly). Checks that need no game.
/// </summary>
static class Rc15Checks
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

    static string Csv(string key)
    {
        string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
        int at = csv.IndexOf("\n" + key + ",\"", StringComparison.Ordinal);
        Check(at >= 0, key + " is gone");
        int start = at + key.Length + 3;
        return csv.Substring(start, csv.IndexOf("\",\"", start, StringComparison.Ordinal) - start);
    }

    static ChatMessage Msg(int sequence, int player) => new ChatMessage(sequence, player, "P" + player, "FFFFFF", "hi");

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("rc15: a chat message chimes only when another player's arrives live", () =>
        {
            Check(!ChatFormat.Chimes(new[] { Msg(5, 1) }, 1, 0), "your own message chimed");
            Check(ChatFormat.Chimes(new[] { Msg(5, 0) }, 1, 0), "another player's message did not chime");
            Check(!ChatFormat.Chimes(new[] { Msg(3, 0), Msg(4, 2) }, 1, 4), "the history a guest gets on joining chimed");
            Check(ChatFormat.Chimes(new[] { Msg(4, 0), Msg(5, 2) }, 1, 4), "a live message after the history did not chime");
            Check(!ChatFormat.Chimes(Array.Empty<ChatMessage>(), 1, 0), "nothing new chimed");
        });

        yield return ("rc15: the chat log remembers how far the history a guest gets on joining went", () =>
        {
            var log = new ChatLog();
            Check(log.HistoryThrough == 0, "a new log has history");
            log.AddHistory(new[] { Msg(1, 0), Msg(2, 1), Msg(3, 0) });
            Check(log.HistoryThrough == 3 && log.LastSequence == 3 && log.Count == 3, $"history through {log.HistoryThrough}, last {log.LastSequence}");
            log.Add(Msg(4, 0));
            Check(log.HistoryThrough == 3 && log.LastSequence == 4, "a live message counted as history");
            log.AddHistory(new[] { Msg(4, 0) });
            Check(log.Count == 4, "a message that came live and again in the history was kept twice");
            string client = Body(Source("TimberNet", "TimberClient.cs"), "protected override void HandleChat(");
            Check(client.Contains("if (isHistory) Chat.AddHistory(numbered);"), "a guest no longer marks the history it is sent as history");
        });

        yield return ("rc15: a chat message from another player chimes, whether the connection panel is open, collapsed or hidden", () =>
        {
            string panel = Source("BeaverBuddies", "Panel", "ConnectionPanelService.cs");
            string tick = Body(panel, "void Tick()");
            int listen = tick.IndexOf("if (net != null && !chimeFailed) ListenForChat(net);", StringComparison.Ordinal);
            Check(listen >= 0 && listen < tick.IndexOf("if (mode == PanelDisplayMode.Hidden", StringComparison.Ordinal), "the chat's chime is gone, or is silent while the panel is hidden");
            string chat = Body(panel, "void ListenForChat(TimberNetBase net)");
            Check(chat.Contains("bool chime = me >= 0 && ChatFormat.Chimes(log.Since(heardSequence), me, log.HistoryThrough);")
                && chat.Contains("if (chime) sounds.Play(BeaverBuddies.Util.NoticeSounds.ChatSound);"), "the chat no longer plays its chime");
            string sounds = Source("BeaverBuddies", "Util", "NoticeSounds.cs");
            Check(sounds.Contains("\"Environment.Buildings.Speaker.Chime_01\"") && sounds.Contains("\"Environment.Buildings.Speaker.Chime_02\""),
                "the chimes are no longer the Speaker's");
            Check(sounds.Contains("MinIntervalSeconds = 1") && sounds.Contains("MixerNames.UIMixerNameKey"), "a burst no longer chimes once, or the chime ignores the interface volume");
        });

        yield return ("rc15: an offer and a request to end an exchange stay until clicked, and a click goes to the player's side of the post", () =>
        {
            string exchange = Source("BeaverBuddies", "Colonies", "TradingPostExchange.cs");
            Check(Body(exchange, "public void Propose(DistrictCrossing half,").Contains("Ask(() => to, partner, () => string.Format(T(\"BeaverBuddies.Colony.Trade.Notice.Proposed\")"),
                "an offer is a passing notice again, or no longer goes to the other colony's side of the post");
            Check(Body(exchange, "public void Cancel(DistrictCrossing half,").Contains("Ask(() => them, partner, () => string.Format(T(\"BeaverBuddies.Colony.Trade.Notice.CancelAsked\")"),
                "a request to end an exchange is a passing notice again");
            foreach (string answer in new[] { "public void Accept(DistrictCrossing half,", "public void Cancel(DistrictCrossing half,", "public void Keep(DistrictCrossing half," })
                Check(Body(exchange, answer).Contains("Answered(half, actorSlot);"), "answering no longer closes the post's message: " + answer);
            string notices = Source("BeaverBuddies", "Colonies", "TradeNotices.cs");
            Check(!notices.Contains("Time.") && !notices.Contains("schedule"), "a trade message is timed: it must stay until clicked");
            Check(notices.Contains("close.AddToClassList(\"close-button\");") && notices.Contains("board.AddToClassList(warning ? \"square-large--red\" : \"square-large--green\");")
                && notices.Contains("label.AddToClassList(\"game-text-normal\");"), "the message no longer looks like the game's notification");
            Check(Body(notices, "private void GoTo(Notice notice)").Contains("_entitySelectionService.SelectAndFocusOn(notice.Half);"), "a click no longer goes to the post");
            Check(Body(notices, "public void UpdateSingleton()").Contains("_noticeSounds.Play(NoticeSounds.TradeSound);"), "a trade message no longer chimes");
            // Built outside the tick: an action only posts it.
            Check(Body(notices, "public bool Post(string text, DistrictCrossing half, bool warning)").Contains("posted.Add((text, half, warning));"), "a message is built inside the tick");
        });

        yield return ("rc15: the trade messages read as plain English: no halves going back to colonies", () =>
        {
            Check(Csv("BeaverBuddies.Colony.Trade.Notice.Cancelled") == "The exchange between {0} and {1} was cancelled. Any goods already brought to the Trading Post go back to the colony that brought them.",
                "the cancelled exchange's message changed: " + Csv("BeaverBuddies.Colony.Trade.Notice.Cancelled"));
            Check(Csv("BeaverBuddies.Colony.Trade.Notice.Proposed").EndsWith(" Click here to answer.") && Csv("BeaverBuddies.Colony.Trade.Notice.CancelAsked").EndsWith(" Click here to answer."),
                "the messages that stay no longer say a click answers them");
            foreach (string key in new[] { "Notice.Cancelled", "AskCancelTooltip", "AgreeCancelTooltip", "EndExchangeTooltip", "PausedCancel" })
            {
                string text = Csv("BeaverBuddies.Colony.Trade." + key);
                Check(!text.Contains("each half") && !text.Contains("What wait") && !text.Contains("what wait") && !text.Contains("crossed stay crossed"), key + " reads as before: " + text);
            }
            // The site's Trading Post demo shows the same tooltips.
            string demo = Source("docs", "assets", "trade-demo.js");
            Check(demo.Contains(Csv("BeaverBuddies.Colony.Trade.AgreeCancelTooltip")) && !demo.Contains("each half"), "the site's demo no longer matches the mod's cancel tooltips");
        });
    }
}
