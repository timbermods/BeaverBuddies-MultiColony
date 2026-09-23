using BeaverBuddies.Colonies;
using BeaverBuddies.Connect;
using BeaverBuddies.Factions;

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

        yield return ("H1: every replayed action skips, never throws, when the entity it names is gone (a blast, a deletion, a handover)", () =>
        {
            // An action reaches the tick after its click: by then a blast, a deletion or another action can have removed
            // what it names, and a throw in a replay stops the session for everyone. Every entity a Replay looks up by id
            // must be checked for null in that Replay, or handed to a method that checks it (listed here).
            var checkedElsewhere = new HashSet<string>
            {
                "ExchangeProposedEvent.half",   // ColonyExchangeService.Propose → WhyNotPropose: "no such crossing"
            };
            var lookup = new System.Text.RegularExpressions.Regex(@"(\w+)\s*=\s*(?:GetComponent<[^>]+>|GetEntityComponent)\(");
            int replays = 0;
            var unguarded = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(Root(), "BeaverBuddies"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                string text = File.ReadAllText(file);
                int at = 0;
                const string signature = "public override void Replay(IReplayContext context)";
                while ((at = text.IndexOf(signature, at, StringComparison.Ordinal)) >= 0)
                {
                    string rest = text.Substring(at + signature.Length).TrimStart();
                    // An expression-bodied Replay (=> …;) is its one statement.
                    string body = rest.StartsWith("=>") ? rest.Substring(0, rest.IndexOf(';') + 1) : Body(text.Substring(at), signature);
                    var classes = System.Text.RegularExpressions.Regex.Matches(text.Substring(0, at), @"class (\w+)");
                    string owner = classes.Count > 0 ? classes[classes.Count - 1].Groups[1].Value : "?";
                    foreach (System.Text.RegularExpressions.Match m in lookup.Matches(body))
                    {
                        string v = m.Groups[1].Value;
                        bool isChecked = System.Text.RegularExpressions.Regex.IsMatch(body,
                            $@"\b{v}\s*[=!]=\s*null|\(bool\)\s*{v}\b|\b{v}\?\.|!\s*{v}\b|\b{v}\s+is\s+(not\s+)?null");
                        if (!isChecked && !checkedElsewhere.Contains(owner + "." + v)) unguarded.Add(owner + "." + v);
                    }
                    replays++;
                    at += 10;
                }
            }
            Check(replays >= 50, $"found only {replays} Replay methods: the scan no longer sees them");
            Check(unguarded.Count == 0, "Replay looks up an entity without checking it is still there: " + string.Join(", ", unguarded));
        });

        yield return ("R8: a patch a game update breaks is left out and named, every other still applies, and co-op is refused", () =>
        {
            string plugin = Source("BeaverBuddies", "Plugin.cs");
            string patchAll = Body(plugin, "private static void PatchAllIsolatingFactions(Harmony harmony)");
            int loop = patchAll.IndexOf("classes.Where(t => t.Namespace != factions)", StringComparison.Ordinal);
            Check(loop >= 0, "the loop over the non-faction patch classes is gone");
            string rest = patchAll.Substring(loop);
            Check(rest.IndexOf("try", StringComparison.Ordinal) < rest.IndexOf("CreateClassProcessor", StringComparison.Ordinal)
                && rest.Contains("FailedPatches.Add(type.FullName)"),
                "each non-faction patch class must be applied in its own try, recording a failure (a throw skipped every later patch)");
            string start = Body(plugin, "public void StartMod(");
            Check(start.Contains("Install(nameof(GameSaverSavePatcher)") && start.Contains("Install(nameof(TimeTimePatcher)"),
                "the hand-made patches must be applied through Install, which records a failure");
            string guard = Source("BeaverBuddies", "Fixes", "CoopFixGuard.cs");
            Check(Body(guard, "public void UpdateSingleton()").Contains("Plugin.FailedPatches"), "the co-op guard must refuse a game with patches left out");
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

        // ---- 1.4.0-rc2: hand-overs (Kyler, 2026-09-23: stewardship is the way to keep a friend's colony; a hand-over is a last resort) ----

        yield return ("rc2: in a mixed game an absent player's colony goes only to a colony of its own faction; with none it waits, unwarned", () =>
        {
            const string Folktails = "Folktails", IronTeeth = "IronTeeth";
            string FactionOf(int slot) => slot == 1 || slot == 3 ? IronTeeth : Folktails;
            var candidates = new List<(int, long)> { (1, 10), (2, 40), (3, 90) };
            // The absence rule: its own faction or none. The other hand-overs (nobody left, by the host) still cross.
            Check(FactionRules.PreferSameFaction(candidates, FactionOf, Folktails, sameFactionOnly: true) == 2, "the nearest of its faction");
            Check(FactionRules.PreferSameFaction(new List<(int, long)> { (1, 10), (3, 5) }, FactionOf, Folktails, sameFactionOnly: true) == null,
                "none of its faction: none, not the other faction's");
            Check(FactionRules.PreferSameFaction(new List<(int, long)> { (1, 10), (3, 5) }, FactionOf, Folktails) == 3,
                "without the flag the nearest of any faction, as before (a colony with nobody left)");
            Check(!ColonyAbsence.IsAnnounced(30, 7, kept: false, hasReceiver: false), "no warning without a colony to take it");
            Check(!ColonyAbsence.IsHandedOver(30, 7, kept: false, announcedAtLastCheck: true, hasReceiver: false), "no hand-over without one");

            // The day loop (as RcColonyChecks' E-3 one), with a receiver that comes and goes: the warning still always
            // comes the day before, and never for a hand-over that can't come.
            (int? handedOver, int? warned) Days(int limit, Func<int, bool> receiver, int days)
            {
                int away = 0;
                bool announced = false;
                int? warned = null;
                for (int day = 1; day <= days; day++)
                {
                    if (ColonyAbsence.IsHandedOver(away, limit, false, announced) && receiver(day)) return (day, warned);
                    away++;
                    bool now = ColonyAbsence.IsAnnounced(away, limit, false, receiver(day));
                    if (now && !announced) warned = day;
                    announced = now;
                }
                return (null, warned);
            }
            var twoPlayers = Days(7, day => false, 60);
            Check(twoPlayers.warned == null && twoPlayers.handedOver == null, "a two-player mixed game: never warned, never handed over");
            var later = Days(7, day => day >= 12, 60);
            Check(later.warned == 12 && later.handedOver == 13, $"a colony of its faction joins on day 12: warned {later.warned}, handed over {later.handedOver}");
            var gone = Days(7, day => day == 12 || day >= 20, 60);
            Check(gone.warned == 20 && gone.handedOver == 21, $"its receiver left the day after the warning: warned again when it is back ({gone.warned}), handed over the day after ({gone.handedOver})");

            // The game's side: the host's check and the day's presence ask for a receiver of the same faction; nobody left still crosses.
            string lifecycle = Source("BeaverBuddies", "Colonies", "ColonyHandover.cs");
            Check(lifecycle.Contains("public int? AbsenceReceiver(int from, IEnumerable<int> present) => NearestLiving(from, present, sameFactionOnly: true);"),
                "an absent player's colony must go only to a colony of its faction (in a mixed game)");
            Check(Body(lifecycle, "public int? NearestLiving(").Contains("FactionOfSlot(from), sameFactionOnly)"), "NearestLiving must pass the rule on");
            Check(Body(lifecycle, "private void HostDaily(int day)").Contains("AbsenceReceiver(slot, present)"), "the host must hand over only to the absence receiver");
            Check(Body(lifecycle, "public void Seen(").Contains("hasReceiver: AbsenceReceiver(slot, present) != null"),
                "the day's presence must warn only of a hand-over that has a colony to go to");
            Check(Body(lifecycle, "private void HandOverDeadColonies(int day)").Contains("NearestLiving(slot, Enumerable.Range(0, ColonySlotTable.MaxSlots));"),
                "a colony with nobody left still goes to the nearest living colony, of any faction");
        });

        yield return ("rc2: the absence hand-over is off by default (0 days), and its tooltip says so", () =>
        {
            string settings = Source("BeaverBuddies", "Settings.cs");
            Check(settings.Contains("new(0, ModSettingDescriptor.CreateLocalized(\"BeaverBuddies.Settings.AbandonedColonyDays\")"), "the setting's default must be 0");
            Check(settings.Contains("AbandonedColonyDays.Value ?? 0;"), "without settings, no colony is handed over for absence");
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv").Replace("\r\n", "\n");
            int at = csv.IndexOf("BeaverBuddies.Settings.AbandonedColonyDays.Tooltip,", StringComparison.Ordinal);
            Check(at >= 0 && csv.Substring(at, 300).Contains("0 (default): never"), "the tooltip must say 0 is the default");
            foreach (string key in new[] { "BeaverBuddies.Colony.Overview.AwayNoSameFaction", "BeaverBuddies.Colony.Overview.HandToOtherFactionTooltip" })
                Check(csv.Contains("\n" + key + ",\""), "no English line for " + key);
        });

        // ---- 1.4.0-rc3: separate or shared is chosen on the Game Mode page; a guest splits a shared game from the game menu ----

        yield return ("rc3: a new game's colonies are chosen on the Game Mode page, and every new world reads that choice", () =>
        {
            string settings = Source("BeaverBuddies", "Settings.cs");
            foreach (string gone in new[] { "SeparateColonies { get; }", "FoundingInSharedGames { get; }", "SeparateScience { get; }", "MixedFactions { get; }" })
                Check(!settings.Contains(gone), "still a Mod Setting: " + gone);
            string options = Source("BeaverBuddies", "Lobby", "NewGameColonyOptions.cs");
            // The world being made: a waiting room's own copy while it makes its world, else the page's.
            string forNewWorld = Body(options, "public static (bool separate, bool separateScience) ForNewWorld()");
            Check(forNewWorld.Contains("LobbySessionState.CreatingWorld") && forNewWorld.Contains("!lobby.Setup.IsSave") && forNewWorld.Contains("lobby.Setup.Separate"),
                "a waiting room's world must be made with the room's own choice");
            Check(forNewWorld.Contains("Separate && SeparateScience"), "separate science only in a separate-colonies game");
            // The page's rows: the Tutorial row joins their column; each row initialised alone (the Tutorial row already is).
            string build = Body(options, "private void Build(VisualElement root)");
            Check(build.Contains("root.Q(\"TutorialToggleWrapper\")") && build.Contains("block.Add(tutorial)"), "the checkboxes must line up with the page's Tutorial row");
            Check(!build.Contains("InitializeVisualElement(block)") && Body(options, "private Toggle Row(").Contains("InitializeVisualElement(row)"),
                "initialising the column would give the Tutorial checkbox a second click sound");
            string refresh = Body(options, "private void Refresh()");
            Check(refresh.Contains("scienceRow.ToggleDisplayStyle(separated)") && refresh.Contains("mixedRow.ToggleDisplayStyle(separated)"),
                "the choices under Separate colonies must show only while it is ticked");
            Check(refresh.Contains("mixed.SetEnabled(possible)") && Body(options, "private string MixedTooltip()").Contains("MixedFactions.Locked"),
                "Mixed factions must be greyed, saying why, when it can't be had here");
            // Every new world and the waiting room read the choice.
            string start = Source("BeaverBuddies", "MultiStart", "MultiStartPatches.cs");
            Check(start.Split("NewGameColonyChoice.ForNewWorld()").Length == 3, "both start paths must read NewGameColonyChoice.ForNewWorld()");
            Check(Source("BeaverBuddies", "Factions", "NewGameFactionCapture.cs").Contains("NewGameColonyChoice.MixedRequested && BeaverBuddies.Lobby.NewGameColonyChoice.Separate"),
                "mixed factions must be asked for on the page, and only with separate colonies");
            Check(Source("BeaverBuddies", "Factions", "MixedFactions.cs").Contains("IsOn = lobby.Setup.Mixed && lobby.Setup.Separate;"), "a room's world must be mixed by its own choice");
            string host = Source("BeaverBuddies", "Lobby", "LobbyHostPanel.cs");
            Check(host.Contains("Separate = NewGameColonyChoice.Separate,") && host.Contains("SeparateScience = NewGameColonyChoice.SeparateScience,"),
                "the room must take the page's choice when it opens");
            Check(Source("BeaverBuddies", "Lobby", "LobbySession.cs").Contains("Settings.PingDisplayName, setup.Separate, setup.Mixed, setup.Factions"),
                "guests must be told the room's own choice");
        });

        yield return ("rc3: only a guest can split a shared game, from the game menu, once confirmed; the host never; science stays shared", () =>
        {
            Check(ColonyRules.MayFound(separateColonies: true, actorIsHost: true) && ColonyRules.MayFound(true, false), "anyone founds in a separate game");
            Check(ColonyRules.MayFound(false, actorIsHost: false) && !ColonyRules.MayFound(false, actorIsHost: true), "in a shared game a guest, never the host");
            Check(ColonyRules.SplitOffered(isGuest: true, separateColonies: false, seated: true, ownsDistrict: false), "a seated guest of a shared game is offered it");
            Check(!ColonyRules.SplitOffered(false, false, true, false), "never the host");
            Check(!ColonyRules.SplitOffered(true, true, true, false), "never in a separate game (the ordinary founding is offered there)");
            Check(!ColonyRules.SplitOffered(true, false, true, true) && !ColonyRules.SplitOffered(true, false, false, false), "not with a colony, nor before seating");
            // The notices: the founder, the host and everyone else are each told the game is separate now, for good.
            Check(ColonyRules.FoundingNoticeFor(1, 1, founded: true, split: true) == FoundingNotice.SplitDone, "the founder");
            Check(ColonyRules.FoundingNoticeFor(0, 1, true, split: true, localIsHost: true) == FoundingNotice.SplitHost, "the host");
            Check(ColonyRules.FoundingNoticeFor(2, 1, true, split: true, localIsHost: false) == FoundingNotice.SplitOther, "another player");
            Check(ColonyRules.FoundingNoticeFor(0, 1, true) == FoundingNotice.Founded && ColonyRules.FoundingNoticeFor(1, 1, false, split: true) == FoundingNotice.Failed,
                "an ordinary founding and a failed try are told as before");
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv").Replace("\r\n", "\n");
            foreach (FoundingNotice notice in new[] { FoundingNotice.SplitDone, FoundingNotice.SplitHost, FoundingNotice.SplitOther })
                Check(csv.Contains("\n" + ColonyRules.FoundingNoticeKey(notice) + ",\""), "no English line for " + notice);

            string founding = Source("BeaverBuddies", "Colonies", "ColonyFoundingService.cs");
            Check(Body(founding, "public ColonyVerdict Judge(").Contains("ColonyRules.MayFound(ColonyModeService.IsSeparateColonies,")
                && Body(founding, "public ColonyVerdict Judge(").Contains("actorIsHost: actorSlot == ColonySession.SeatOfPlayer(ColonySession.HostPlayer)"),
                "the host's verdict must refuse the host's own split");
            Check(founding.Contains("private static bool FoundingAllowed => ColonyModeService.IsSeparateColonies || SharedColonySplit.Confirmed;"),
                "in a shared game the founding tool opens only after the split is confirmed");
            string found = Body(founding, "public void Found(Placement placement, int slot, ColonyStartingSettings settings, string faction = null)");
            Check(found.Contains("separateScience: false") && found.Contains("newGame: false"), "a split keeps one pool of science and unlocks");
            Check(Body(founding, "public void BeginSplit()").Contains("SharedColonySplit.Confirmed = true"), "BeginSplit must allow this guest's founding");
            Check(Body(founding, "public ColonyFoundingService(").Contains("SharedColonySplit.Confirmed = false"), "a new scene must forget an earlier session's confirmation");

            string split = Source("BeaverBuddies", "Colonies", "SharedColonySplit.cs");
            Check(Body(split, "public static bool Offered").Contains("EventIO.Get() is ClientEventIO") && Body(split, "public static bool Offered").Contains("ColonyRules.SplitOffered("),
                "the button must be a guest's, by the rule");
            Check(Body(split, "public void AddButton(").Contains("button.ToggleDisplayStyle(Offered)"), "the button must be hidden wherever it is not offered");
            string ask = Body(split, "private void Ask(GameOptionsBox box)");
            Check(ask.IndexOf("SplitCanBeginNow", StringComparison.Ordinal) < ask.IndexOf("SetConfirmButton", StringComparison.Ordinal)
                && ask.IndexOf("SetConfirmButton", StringComparison.Ordinal) < ask.IndexOf("founding.BeginSplit()", StringComparison.Ordinal),
                "the split must wait for the game's start, then ask, and begin only once confirmed");
            // The host's old switch is gone: from Mod Settings, the session and the join message.
            string session = Source("BeaverBuddies", "Colonies", "ColonySession.cs");
            Check(!session.Contains("HostAllowsFounding") && !session.Contains("HostSeparateScience"), "the host's founding switch is still in the session");
            Check(!Source("BeaverBuddies", "Events", "ConnectionEvents.cs").Contains("foundingInSharedGame"), "the join message still carries the host's founding switch");
        });

        yield return ("rc3: the waiting room says what kind of game it is, the same on the host's page and every guest's", () =>
        {
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(false, "Folktails", separate: true, mixed: false) == "BeaverBuddies.Lobby.Colonies.Separate", "separate");
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(false, "Folktails", true, mixed: true) == "BeaverBuddies.Lobby.Colonies.SeparateMixed", "separate, mixed");
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(false, "Folktails", separate: false, mixed: false) == "BeaverBuddies.Lobby.Colonies.Shared", "shared");
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(true, "IronTeeth", false, false) == "BeaverBuddies.Lobby.Colonies.Shared", "a shared save");
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(true, "IronTeeth", true, false) == "BeaverBuddies.Lobby.Colonies.SeparateSave", "a separate save");
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(true, "IronTeeth", true, true) == "BeaverBuddies.Lobby.Faction.MixedSave", "a mixed save");
            Check(BeaverBuddies.Lobby.LobbyRules.ColonyNoteKey(true, "", true, true) == null, "a save that could not be read: no guess");
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv").Replace("\r\n", "\n");
            foreach (string key in new[] { "Colonies.Separate", "Colonies.SeparateMixed", "Colonies.Shared", "Colonies.SeparateSave", "Faction.MixedSave" })
                Check(csv.Contains("\nBeaverBuddies.Lobby." + key + ",\""), "no English line for " + key);
            Check(Source("BeaverBuddies", "Lobby", "LobbyHostPanel.cs").Split("LobbyRules.ColonyNoteKey(").Length == 3, "the host's page (a new game and a save) must use the rule");
            Check(Source("BeaverBuddies", "Lobby", "LobbyGuestPanel.cs").Contains("LobbyRules.ColonyNoteKey(summary.IsSave, summary.FactionId, summary.SeparateColonies, summary.Mixed)"),
                "a guest's page must use the rule on the host's summary");
        });
    }
}
