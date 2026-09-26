#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

// 1.4.0-rc15, checks against the compiled mod and the game's files: an offer (or a request to end an exchange) stays on
// screen until the player clicks it, a click goes to the post, and it chimes; a chat message from another player chimes.
internal static class Rc15RuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    static List<string?> Calls(Type type, string method) =>
        IlScan.Instructions(type.GetMethod(method, All) ?? throw new Exception(type.Name + "." + method + " is gone"))
            .Where(i => i.Calls).Select(i => i.Member?.DeclaringType?.Name + "." + i.Member?.Name).ToList();

    public static void Run(Assembly mod, string managedPath, Action<string, Action> test)
    {
        test("rc15: the chimes are the game's own Speaker chimes, played through the interface volume", () =>
        {
            using ZipArchive blueprints = ZipFile.OpenRead(Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "Blueprints.zip")));
            var soundIds = new HashSet<string>();
            foreach (ZipArchiveEntry entry in blueprints.Entries.Where(e => e.FullName.StartsWith("Sounds/Speaker/")))
            {
                using var reader = new StreamReader(entry.Open());
                Match id = Regex.Match(reader.ReadToEnd(), "\"SoundId\"\\s*:\\s*\"([^\"]+)\"");
                if (id.Success) soundIds.Add(id.Groups[1].Value);
            }
            Type sounds = mod.GetType("BeaverBuddies.Util.NoticeSounds", true)!;
            foreach (string field in new[] { "TradeSound", "ChatSound" })
            {
                string sound = (string)(sounds.GetField(field)?.GetRawConstantValue() ?? throw new Exception(field + " is gone"));
                if (!soundIds.Contains(sound)) throw new Exception($"the game has no Speaker sound {sound} (it has: {string.Join(", ", soundIds)})");
            }
            List<string?> play = Calls(sounds, "Play");
            if (!play.Contains("ISoundSystem.PlaySound2D")) throw new Exception("NoticeSounds.Play no longer plays a flat (2D) sound");
            if (!play.Contains("ISoundSystem.SetCustomMixer")) throw new Exception("NoticeSounds.Play no longer uses the interface mixer");
        });

        test("rc15: an offer and a request to end an exchange are messages that stay, go to the post, and chime", () =>
        {
            Type exchange = mod.GetType("BeaverBuddies.Colonies.ColonyExchangeService", true)!;
            if (!Calls(exchange, "Propose").Contains("ColonyExchangeService.Ask")) throw new Exception("an offer is a passing notice again");
            if (!Calls(exchange, "Cancel").Contains("ColonyExchangeService.Ask")) throw new Exception("a request to end an exchange is a passing notice again");
            if (!Calls(exchange, "Ask").Contains("TradeNotices.Post")) throw new Exception("Ask no longer posts a message that stays");
            foreach (string answer in new[] { "Accept", "Cancel", "Keep" })
                if (!Calls(exchange, answer).Contains("ColonyExchangeService.Answered")) throw new Exception(answer + " no longer closes the post's message");
            Type notices = mod.GetType("BeaverBuddies.Colonies.TradeNotices", true)!;
            if (!Calls(notices, "UpdateSingleton").Contains("NoticeSounds.Play")) throw new Exception("a trade message no longer chimes");
            if (!Calls(notices, "GoTo").Contains("EntitySelectionService.SelectAndFocusOn")) throw new Exception("a click on a trade message no longer goes to the post");
            if (Calls(notices, "Show").Any(c => c != null && c.StartsWith("Time."))) throw new Exception("a trade message is timed again: it must stay until clicked");
        });

        test("rc15: the messages use the game's notification board, text and close button (CommonStyle, CoreStyle)", () =>
        {
            using ZipArchive ui = ZipFile.OpenRead(Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip")));
            string Sheet(string path)
            {
                using var reader = new StreamReader((ui.GetEntry(path) ?? throw new Exception("UI.zip has no " + path)).Open());
                return reader.ReadToEnd();
            }
            string common = Sheet("Views/Common/CommonStyle.uss"), core = Sheet("Views/Core/CoreStyle.uss");
            foreach (string board in new[] { "square-large--green", "square-large--red" })
                if (!Regex.IsMatch(common, @"\." + board + @"\s*\{[^}]*--background-image")) throw new Exception("the game's notification board " + board + " is gone");
            if (!Regex.IsMatch(core, @"\.close-button\s*\{[^}]*background-image")) throw new Exception("the game's close button is gone");
            using var panel = new StreamReader((ui.GetEntry("Views/Common/QuickNotificationPanel.uxml") ?? throw new Exception("the game's quick notification is gone")).Open());
            string uxml = panel.ReadToEnd();
            if (!uxml.Contains("game-text-normal")) throw new Exception("the game's quick notification no longer uses game-text-normal");
        });

        test("rc15: a chat message from another player chimes, from the connection panel", () =>
        {
            Type panel = mod.GetType("BeaverBuddies.Panel.ConnectionPanelService", true)!;
            List<string?> chat = Calls(panel, "UpdateChat");
            if (!chat.Contains("ChatFormat.Chimes") || !chat.Contains("NoticeSounds.Play")) throw new Exception("the chat no longer chimes");
            if (!chat.Contains("ChatLog.get_HistoryThrough")) throw new Exception("the chat chimes for the history a guest gets on joining");
        });
    }
}
