using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using TimberNet;

/// <summary>
/// The waiting room's network phase (TimberNet: LobbyFrames, LobbyRoom, LobbyInbox and the server and client changes),
/// over in-memory pipes with the real handshake.
/// </summary>
static class LobbyChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static bool Until(Func<bool> condition, int ms = 3000) => SpinWait.SpinUntil(condition, ms);

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("A waiting-room guest is welcomed and sees the roster before any save; its hello and ready reach the host", () => WithRoom(rig =>
        {
            var guest = rig.Join();
            Check(Until(() => guest.Lobby.View().Welcomed && guest.Lobby.View().Players.Count == 2), "not welcomed");
            LobbyView view = guest.Lobby.View();
            Check(view.You == 1 && view.Summary!.Settlement == "Beaverton" && view.Summary.HostName == "Kyler");
            Check(view.Players[0].IsHost && view.Players[0].Colony == 1 && view.Players[1].Joining && view.Players[1].Colony == 2);
            Check(guest.SendLobbyHello("local:anna", "Anna") && guest.SendLobbyReady(true));
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 2 && rig.Host.Lobby!.Snapshot().Players[1].Ready), "ready not seen");
            LobbySnapshot snapshot = rig.Host.Lobby!.Snapshot();
            Check(snapshot.Players[1].Name == "Anna" && !snapshot.Players[1].Joining && snapshot.Guests[0].StableId == "local:anna");
            Check(Until(() => guest.Lobby.View().Players.Count == 2 && guest.Lobby.View().Players[1].Ready), "the guest's roster did not follow");
        }));

        yield return ("Releasing the room sends the save, then the state and the first event, and the host's game thread sends none of it", () => WithRoom(rig =>
        {
            var recorder = new ThreadRecorder();
            var guest = rig.Join(recorder);
            byte[]? map = null;
            guest.OnMapReceived += bytes => map = bytes;
            Check(Until(() => guest.Lobby.View().Welcomed));
            byte[] save = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();
            rig.Host.SetLobbyStage(LobbyStage.SendingWorld);
            int caller = Environment.CurrentManagedThreadId;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            rig.Host.ReleaseLobby(save);
            Check(clock.ElapsedMilliseconds < 200, "ReleaseLobby waited");
            Check(Until(() => { guest.Update(); return map != null; }), "no save");
            Check(map!.SequenceEqual(save));
            Check(!recorder.LargeWriteThreads.Contains(caller), "the save was written on the releasing thread");
            Check(guest.Lobby.View().Stage == LobbyStage.SendingWorld, "the stage did not come before the save");
            Check(Until(() => rig.Host.LobbyGuestsQueued));
            List<JObject> events = new();
            Check(Until(() => { events.AddRange(guest.ReadEvents(0)); return events.Any(e => (string?)e["type"] == "InitProbe"); }), "no init event");
            Check(guest.Hash == rig.Host.Hash, "state and init did not arrive in order");
            // In the game now: the host no longer treats it as waiting.
            Check(rig.Host.Lobby!.Snapshot().Guests[0].InGame);
        }));

        yield return ("A guest still waiting can send nothing but its hello and ready; a frame too large closes only that guest", () => WithRoom(rig =>
        {
            var other = rig.Join();
            Check(Until(() => other.Lobby.View().Welcomed));
            PipeStream raw = rig.JoinRaw();
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 3));
            var net = new TestNet();
            net.Send(raw, CompressionUtils.Compress(new JObject { ["type"] = "FakeAction", ["ticksSinceLoad"] = 0 }.ToString()));
            net.Send(raw, CompressionUtils.Compress(new JObject { ["type"] = "SessionFault", ["reason"] = "x" }.ToString()));
            net.Send(raw, CompressionUtils.Compress(LobbyFrames.Hello("local:raw", "Raw").ToString()));
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Any(p => p.Name == "Raw")), "the hello after the junk was not read");
            Check(!rig.Host.IsStopped && rig.Host.ReadEvents(0).Count == 0, "a waiting guest's frame reached the game");
            // A length the waiting room never accepts.
            raw.Write(new byte[] { 0, 1, 0x11, 0x70 }, 0, 4);
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 2), "the oversized guest stayed");
            Check(Until(() => !raw.Connected || ReadsEnd(raw)), "the oversized guest was not closed");
            Check(!other.IsStopped && rig.Host.Lobby!.Snapshot().Players.Any(p => p.Number == other.Lobby.View().You));
        }));

        yield return ("Ending the room tells every waiting guest why and closes it; closing the host closes waiting guests", () =>
        {
            WithRoom(rig =>
            {
                var a = rig.Join();
                var b = rig.Join();
                Check(Until(() => a.Lobby.View().Welcomed && b.Lobby.View().Welcomed));
                rig.Host.CancelLobby(LobbyEndReason.Cancelled, null);
                Check(Until(() => a.Lobby.View().Ended && b.Lobby.View().Ended), "not told");
                Check(a.Lobby.View().EndReason == LobbyEndReason.Cancelled);
                Check(Until(() => a.IsStopped && b.IsStopped), "not closed");
            });
            WithRoom(rig =>
            {
                var a = rig.Join();
                Check(Until(() => a.Lobby.View().Welcomed));
                rig.Host.Close();
                Check(Until(() => a.IsStopped), "a waiting guest outlived the host");
            });
        });

        yield return ("After Start nobody new comes in, with the room's reason, while those waiting still get the save", () => WithRoom(rig =>
        {
            var a = rig.Join();
            byte[]? map = null;
            a.OnMapReceived += bytes => map = bytes;
            Check(Until(() => a.Lobby.View().Welcomed));
            rig.Host.CloseLobbyToNewcomers("The room is closed.");
            var late = rig.Join();
            string error = "";
            late.OnError += message => error = message;
            Check(Until(() => { late.Update(); return error.Length > 0; }), "the newcomer was not refused");
            Check(error.Contains("The room is closed.") && !late.Lobby.View().Welcomed);
            Check(Until(() => a.Lobby.View().Stage == LobbyStage.Starting));
            rig.Host.ReleaseLobby(new byte[] { 1, 2, 3 });
            Check(Until(() => { a.Update(); return map != null; }), "the member did not get the save");
        }));

        yield return ("A guest reads a waiting-room frame only before its save, and never mistakes one for the save", () =>
        {
            byte[] welcome = CompressionUtils.Compress(LobbyFrames.Welcome(2,
                new LobbySummary("Folktails", "Map", null, "Town", "Host", false)).ToString());
            byte[] state = CompressionUtils.Compress(LobbyFrames.State(9, LobbyStage.CreatingWorld).ToString());
            var bytes = new List<byte>();
            bytes.AddRange(Length(LobbyFrames.Sentinel)); bytes.AddRange(Length(welcome.Length)); bytes.AddRange(welcome);
            bytes.AddRange(Length(3)); bytes.AddRange(new byte[] { 7, 8, 9 });
            bytes.AddRange(Length(LobbyFrames.Sentinel)); bytes.AddRange(Length(state.Length)); bytes.AddRange(state);
            // A pipe that stays open: a stream that ended would stop the guest before it handed the save on.
            var (hostSide, guestSide) = PipeStream.Pair();
            hostSide.Write(bytes.ToArray(), 0, bytes.Count);
            var client = new TimberClient(guestSide);
            byte[]? map = null;
            client.OnMapReceived += received => map = received;
            try
            {
                client.Start();
                Check(Until(() => { client.Update(); return map != null; }), "no save");
                Check(map!.SequenceEqual(new byte[] { 7, 8, 9 }));
                // The state frame after the save has been read (and dropped) by now.
                Check(Until(() => client.Lobby.View().LastFrameAtMs > 0));
                Thread.Sleep(100);
                LobbyView view = client.Lobby.View();
                Check(view.Welcomed && view.You == 2 && view.Summary!.ModeLocKey == null);
                Check(view.Stage == LobbyStage.Open, "a frame after the save was used");
                Check(!client.IsStopped, "the frame after the save broke the connection");
            }
            finally { client.Close(); hostSide.Close(); }
        });

        yield return ("Guests are numbered in the order they came; one who leaves drops off and the colonies move up", () => WithRoom(rig =>
        {
            var a = rig.Join(); Check(Until(() => a.Lobby.View().Welcomed));
            var b = rig.Join(); Check(Until(() => b.Lobby.View().Welcomed));
            var c = rig.Join(); Check(Until(() => c.Lobby.View().Welcomed));
            Check(a.Lobby.View().You == 1 && b.Lobby.View().You == 2 && c.Lobby.View().You == 3);
            Check(Until(() => a.Lobby.View().Players.Count == 4));
            Check(a.Lobby.View().Players.Select(p => p.Colony).SequenceEqual(new int?[] { 1, 2, 3, 4 }));
            b.Close();
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 3), "the leaver stayed");
            Check(rig.Host.Lobby!.Snapshot().Players.Select(p => p.Number).SequenceEqual(new[] { 0, 1, 3 }));
            Check(Until(() => a.Lobby.View().Players.Count == 3 && a.Lobby.View().Players[2].Colony == 3), "the roster did not follow");
        }));

        yield return ("A waiting guest that stops reading holds up nobody: the others keep hearing, the host's calls return, and it is taken out", () =>
        {
            int previousStall = TimberServer.SendStallLimitMs, previousFlush = TimberServer.AbortFlushMs;
            TimberServer.SendStallLimitMs = 600;
            TimberServer.AbortFlushMs = 300;
            try
            {
                WithRoom(rig =>
                {
                    var a = rig.Join();
                    var stuck = new StallingStream();
                    var b = rig.Join(wrap: stuck.Wrap);
                    Check(Until(() => a.Lobby.View().Welcomed && b.Lobby.View().Welcomed));
                    stuck.Stall();
                    // The pump keeps writing to everyone else while one write to the stuck guest never returns.
                    Thread.Sleep(400);
                    Check(RttTracker.NowMs - a.Lobby.View().LastFrameAtMs < 250, "the other guest stopped hearing the host");
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    rig.Host.SetLobbyStage(LobbyStage.Open);
                    Check(clock.ElapsedMilliseconds < 100, "the host's call waited for the stuck guest");
                    // Taken out once its lane has been stuck past the limit.
                    Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 2), "the stuck guest stayed");
                    clock.Restart();
                    rig.Host.CancelLobby(LobbyEndReason.Cancelled, null);
                    Check(clock.ElapsedMilliseconds < 1000, "ending the room waited " + clock.ElapsedMilliseconds + " ms");
                    Check(Until(() => a.Lobby.View().Ended));
                });
            }
            finally
            {
                TimberServer.SendStallLimitMs = previousStall;
                TimberServer.AbortFlushMs = previousFlush;
            }
        });

        yield return ("The host keeps a waiting guest's line alive while nothing else happens", () => WithRoom(rig =>
        {
            var a = rig.Join();
            Check(Until(() => a.Lobby.View().Welcomed));
            Thread.Sleep(400);
            Check(RttTracker.NowMs - a.Lobby.View().LastFrameAtMs < 250, "no keep-alive");
        }));

        yield return ("The host can remove a waiting guest, and the eighth guest is refused", () => WithRoom(rig =>
        {
            var guests = new List<TimberClient>();
            for (int i = 0; i < LobbyRoom.MaxGuests; i++)
            {
                var guest = rig.Join();
                Check(Until(() => guest.Lobby.View().Welcomed), $"guest {i} not welcomed");
                guests.Add(guest);
            }
            var eighth = rig.Join();
            string error = "";
            eighth.OnError += message => error = message;
            Check(Until(() => { eighth.Update(); return error.Length > 0; }), "the eighth came in");
            Check(error.Contains(LobbyRoom.FullMessage));
            Check(rig.Host.RemoveFromLobby(guests[3].Lobby.View().You));
            Check(Until(() => guests[3].Lobby.View().Ended && guests[3].Lobby.View().EndReason == LobbyEndReason.Removed), "not told");
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == LobbyRoom.MaxGuests), "still listed");
            Check(!rig.Host.RemoveFromLobby(99));
        }));

        yield return ("A hello with a bad id is refused, and names are cleaned", () => WithRoom(rig =>
        {
            PipeStream raw = rig.JoinRaw();
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players.Count == 2));
            var net = new TestNet();
            net.Send(raw, CompressionUtils.Compress(LobbyFrames.Hello("steam:1|2", "Mallory").ToString()));
            net.Send(raw, CompressionUtils.Compress(new JObject { ["type"] = LobbyFrames.ReadyType, ["ready"] = true }.ToString()));
            Check(Until(() => rig.Host.Lobby!.Snapshot().Players[1].Ready));
            Check(rig.Host.Lobby!.Snapshot().Players[1].Joining, "a bad hello was taken");
            net.Send(raw, CompressionUtils.Compress(LobbyFrames.Hello("local:ok", "<b>Anna</b>\n").ToString()));
            Check(Until(() => !rig.Host.Lobby!.Snapshot().Players[1].Joining));
            string name = rig.Host.Lobby!.Snapshot().Players[1].Name;
            Check(!name.Contains('<') && !name.Contains('\n') && name.Contains("Anna"), name);
            Check(!LobbyFrames.IsWellFormedId("") && !LobbyFrames.IsWellFormedId(new string('a', 65)) && LobbyFrames.IsWellFormedId("steam:76561198000000000"));
        }));

        yield return ("Waiting-room rules: the start question, the status lines, the starts to fill, the watchdog and the save name", () =>
        {
            LobbyPlayer host = new(0, "Kyler", true, true, false, 1);
            LobbyPlayer anna = new(1, "Anna", true, false, false, 2), bob = new(2, "Bob", false, false, false, 3);
            LobbyPlayer joining = new(3, "Player", false, false, true, 4);
            Check(BeaverBuddies.Lobby.LobbyRules.StartConfirm(new[] { host }) == BeaverBuddies.Lobby.StartQuestion.Alone);
            Check(BeaverBuddies.Lobby.LobbyRules.StartConfirm(new[] { host, anna }) == BeaverBuddies.Lobby.StartQuestion.None);
            Check(BeaverBuddies.Lobby.LobbyRules.StartConfirm(new[] { host, anna, bob }) == BeaverBuddies.Lobby.StartQuestion.NotReady);
            // A guest still joining is not ready, even if it said it was before its hello.
            LobbyPlayer readyButJoining = new(4, "Player", true, false, true, 0);
            Check(BeaverBuddies.Lobby.LobbyRules.StartConfirm(new[] { host, readyButJoining }) == BeaverBuddies.Lobby.StartQuestion.NotReady);
            string Key(IReadOnlyList<LobbyPlayer> players, LobbyStage stage = LobbyStage.Open) =>
                BeaverBuddies.Lobby.LobbyRules.HostStatus(players, stage).Key.Replace(BeaverBuddies.Lobby.LobbyRules.KeyPrefix, "");
            Check(Key(new[] { host }) == "Status.Empty");
            Check(Key(new[] { host, anna }) == "Status.AllReady");
            var one = BeaverBuddies.Lobby.LobbyRules.HostStatus(new[] { host, anna, bob }, LobbyStage.Open);
            Check(one.Key.EndsWith("Status.OneNotReady") && (string)one.Args[0] == "Bob");
            Check(Key(new[] { host, anna, joining }) == "Status.OneJoining", "a joining guest was named");
            var some = BeaverBuddies.Lobby.LobbyRules.HostStatus(new[] { host, bob, joining }, LobbyStage.Open);
            Check(some.Key.EndsWith("Status.SomeNotReady") && (int)some.Args[0] == 2);
            Check(Key(new[] { host, bob }, LobbyStage.CreatingWorld) == "Status.Starting");
            Check(BeaverBuddies.Lobby.LobbyRules.GuestStatus(false, LobbyStage.Open, "Kyler").Key.EndsWith("GuestNotReady"));
            Check(BeaverBuddies.Lobby.LobbyRules.GuestStatus(true, LobbyStage.Open, "Kyler").Key.EndsWith("GuestReady"));
            Check(BeaverBuddies.Lobby.LobbyRules.GuestStatus(true, LobbyStage.Starting, "Kyler").Key.EndsWith("CreatingWorld"));
            Check(BeaverBuddies.Lobby.LobbyRules.GuestStatus(true, LobbyStage.SendingWorld, "Kyler").Key.EndsWith("SendingWorld"));
            Check(BeaverBuddies.Lobby.LobbyRules.StartsToFill(4, 1) == 2 && BeaverBuddies.Lobby.LobbyRules.StartsToFill(2, 3) == 2
                && BeaverBuddies.Lobby.LobbyRules.StartsToFill(4, 0) == 1 && BeaverBuddies.Lobby.LobbyRules.StartsToFill(8, 6) == 4);
            Check(BeaverBuddies.Lobby.LobbyRules.WatchdogDue(0, 120000, LobbyStage.Open));
            Check(!BeaverBuddies.Lobby.LobbyRules.WatchdogDue(0, 119000, LobbyStage.Open));
            Check(!BeaverBuddies.Lobby.LobbyRules.WatchdogDue(0, 500000, LobbyStage.SendingWorld), "asked while the save arrived");
            Check(BeaverBuddies.Lobby.LobbyRules.SaveName("2026-09-22 20h31m, Day 1-1") == "2026-09-22 20h31m Day 1-1 Co-op start");
            Check(BeaverBuddies.Lobby.LobbyRules.Tag(host)!.Value.Key.EndsWith("Tag.HostColony"));
            Check(BeaverBuddies.Lobby.LobbyRules.Tag(new LobbyPlayer(5, "H", true, false, false, 0))!.Value.Key.EndsWith("Tag.Helper"));
            Check(BeaverBuddies.Lobby.LobbyRules.Tag(new LobbyPlayer(0, "K", true, true, false, null))!.Value.Key.EndsWith("Tag.Host"));
            Check(BeaverBuddies.Lobby.LobbyRules.Tag(new LobbyPlayer(1, "A", true, false, false, null)) == null, "a shared-game guest got a tag");
        });

        yield return ("The waiting room's order seats its guests: the host colony 1, guests 2 to 4, then helpers", () =>
        {
            // As LobbyWorldMaker fills the table before the world's first save, from the room's snapshot.
            var table = new BeaverBuddies.Colonies.ColonySlotTable();
            Check(table.Resolve("steam:100", "Kyler") == 0);
            Check(table.Resolve("steam:201", "Anna") == 1);
            Check(table.Resolve("local:direct", "Bob") == 2);
            Check(table.Resolve("steam:203", "Cy") == 3);
            Check(table.Resolve("steam:204", "Dee") == null, "a fifth colony");
            var saved = BeaverBuddies.Colonies.ColonySlotTable.Decode(BeaverBuddies.Colonies.ColonySlotTable.Encode(table.Entries));
            var loaded = new BeaverBuddies.Colonies.ColonySlotTable();
            loaded.Set(saved);
            // Each guest's hello in the game finds the colony the room showed: over Steam by the proved id, over a direct
            // connection by the id it said in the room (the same LocalPlayerIdentity.Id).
            Check(loaded.SeatHello("steam:201", "steam:201", null, "Anna").Slot == 1);
            Check(loaded.SeatHello("local:direct", null, null, "Bob").Slot == 2);
            Check(loaded.SeatHello("local:other", "steam:203", null, "Cy").Slot == 3, "the proved Steam id did not win");
            // The same rule the room shows its rows by.
            Check(Enumerable.Range(0, 5).Select(i => LobbyRoom.ColonyOf(i, true)).SequenceEqual(new int?[] { 1, 2, 3, 4, 0 }));
        });

        yield return ("Every waiting-room string exists in the English file, and the pages use only the main menu's style sheets", () =>
        {
            string root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root)!;
            Check(root != null, "could not find the repository root");
            string csv = File.ReadAllText(Path.Combine(root!, "BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv"));
            var defined = new HashSet<string>(System.Text.RegularExpressions.Regex.Matches(csv, "^([A-Za-z0-9.]+),",
                System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value));
            string lobby = Path.Combine(root!, "BeaverBuddies", "Lobby");
            var files = Directory.GetFiles(lobby, "*.cs").Append(Path.Combine(root!, "BeaverBuddies", "Connect", "ClientConnectionService.cs"));
            var missing = new List<string>();
            int used = 0;
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"(BeaverBuddies\\.Lobby\\.[A-Za-z.]+)\""))
                { used++; if (!defined.Contains(m.Groups[1].Value)) missing.Add(m.Groups[1].Value); }
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "KeyPrefix \\+ \"([A-Za-z.]+)\""))
                { used++; string key = "BeaverBuddies.Lobby." + m.Groups[1].Value; if (!defined.Contains(key)) missing.Add(key); }
            }
            Check(used > 30, "found only " + used + " keys; the check is not looking in the right place");
            Check(missing.Count == 0, "missing from enUS_BeaverBuddie.csv: " + string.Join(", ", missing.Distinct()));
            Check(!System.Text.RegularExpressions.Regex.IsMatch(string.Join(",", defined.Where(k => k.StartsWith("BeaverBuddies.Lobby."))), "[0-9]"),
                "a waiting-room key has a digit");
            // The main menu loads CommonStyle, CoreStyle, OptionsStyle, MainMenuStyle, MainMenuMiscStyle and ModdingStyle,
            // not the in-game sheets: their classes (and NativeElements, built on them) would draw nothing there.
            string[] inGameOnly = { "entity-panel__text", "entity-sub-panel", "game-scroll-view", "entity-panel__toggle", "progress-bar--green", "NativeElements." };
            foreach (string file in Directory.GetFiles(lobby, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (string name in inGameOnly) Check(!text.Contains(name), $"{Path.GetFileName(file)} uses {name}");
            }
        });

        yield return ("A save's waiting room tells its guests the save's name and date, and predicts no colonies", () =>
        {
            int previous = TimberServer.LobbyIntervalMs;
            TimberServer.LobbyIntervalMs = 50;
            var rig = new Rig(LobbySummary.ForSave("Beaverton", "Before the drought", 3, 7, "Kyler"));
            try
            {
                var guest = rig.Join();
                Check(Until(() => guest.Lobby.View().Welcomed && guest.Lobby.View().Players.Count == 2), "not welcomed");
                LobbySummary summary = guest.Lobby.View().Summary!;
                Check(summary.IsSave && summary.SaveName == "Before the drought" && summary.Cycle == 3 && summary.Day == 7
                    && summary.Settlement == "Beaverton" && !summary.SeparateColonies);
                // The save seats each player by who it remembers: the room guesses no colony, for the host or a guest.
                Check(guest.Lobby.View().Players.All(p => p.Colony == null));
                byte[]? map = null;
                guest.OnMapReceived += bytes => map = bytes;
                rig.Host.CloseLobbyToNewcomers("closed");
                rig.Host.SetLobbyStage(LobbyStage.SendingWorld);
                rig.Host.ReleaseLobby(new byte[] { 4, 5, 6 });
                Check(Until(() => { guest.Update(); return map != null; }), "no save");
            }
            finally
            {
                rig.Dispose();
                TimberServer.LobbyIntervalMs = previous;
            }
            // A new game's summary says it is not a save, and one from an older frame without "save" reads as a new game.
            var newGame = new LobbySummary("Folktails", "Diorama", null, "Town", "Host", true);
            Check(LobbySummary.TryParse(newGame.ToJson(), out LobbySummary? back) && !back!.IsSave);
            var withoutSave = newGame.ToJson();
            withoutSave.Remove("save");
            Check(LobbySummary.TryParse(withoutSave, out LobbySummary? old) && !old!.IsSave);
            Check(!LobbySummary.TryParse(new JObject(newGame.ToJson()) { ["save"] = new JObject { ["name"] = 5 } }, out _),
                "a save without a name was taken");
        });

        yield return ("Waiting-room frames round-trip, and bad ones are refused", () =>
        {
            var summary = new LobbySummary("Folktails", "Diorama", "NewGameMode.Hard", "Beaverton", "Kyler", true);
            Check(LobbyFrames.TryParseWelcome(LobbyFrames.Welcome(3, summary), out int you, out LobbySummary? parsed) && you == 3
                && parsed!.MapName == "Diorama" && parsed.ModeLocKey == "NewGameMode.Hard" && parsed.SeparateColonies);
            var players = new[] { new LobbyPlayer(0, "Kyler", true, true, false, 1), new LobbyPlayer(4, "Helper", false, false, true, 0) };
            Check(LobbyFrames.TryParseRoster(LobbyFrames.Roster(players), out List<LobbyPlayer> roster) && roster.Count == 2
                && roster[1].Colony == 0 && roster[1].Joining && roster[1].Number == 4);
            Check(LobbyFrames.TryParseState(LobbyFrames.State(5, LobbyStage.CreatingWorld), out int seq, out LobbyStage stage)
                && seq == 5 && stage == LobbyStage.CreatingWorld);
            Check(LobbyFrames.TryParseEnd(LobbyFrames.End(LobbyEndReason.Failed, "disk full"), out LobbyEndReason reason, out string? detail)
                && reason == LobbyEndReason.Failed && detail == "disk full");
            Check(!LobbyFrames.TryParseRoster(new JObject { ["players"] = new JArray(new JObject { ["n"] = 1, ["name"] = "x", ["ready"] = true,
                ["host"] = false, ["joining"] = false, ["colony"] = 9 }) }, out _), "a colony past four was taken");
            Check(!LobbyFrames.TryParseWelcome(new JObject { ["you"] = 0 }, out _, out _));
            Check(!LobbyFrames.TryParseState(new JObject { ["seq"] = 1, ["state"] = "later" }, out _, out _));
            Check(LobbyRoom.ColonyOf(0, true) == 1 && LobbyRoom.ColonyOf(3, true) == 4 && LobbyRoom.ColonyOf(4, true) == 0
                && LobbyRoom.ColonyOf(1, false) == null);
        });

        // ---- mixed factions (design/MIXED-FACTIONS-PLAN.md §6) ----

        yield return ("Factions: a waiting room that is not mixed sends what 1.4.0-beta19 sent, with no faction fields", () =>
        {
            JObject summary = new LobbySummary("Folktails", "Diorama", null, "Town", "Host", true).ToJson();
            Check(summary["mixed"] == null && summary["factions"] == null, summary.ToString());
            JObject row = new LobbyPlayer(1, "Anna", true, false, false, 2).ToJson();
            Check(row["faction"] == null && row["pick"] == null, row.ToString());
            // A mixed summary with no factions to offer is not mixed.
            Check(!new LobbySummary("Folktails", "Diorama", null, "Town", "Host", true, true, Array.Empty<string>()).Mixed);
        });

        yield return ("Factions: a mixed summary and its rows round-trip, and bad faction fields are refused", () =>
        {
            var summary = new LobbySummary("Folktails", "Diorama", null, "Town", "Host", true, true, new[] { "Folktails", "IronTeeth", "bad id!" });
            Check(summary.Mixed && summary.Factions.SequenceEqual(new[] { "Folktails", "IronTeeth" }), "an unusable id was offered");
            Check(LobbySummary.TryParse(summary.ToJson(), out LobbySummary? back) && back!.Mixed && back.Factions.Count == 2);
            Check(!LobbySummary.TryParse(new JObject(summary.ToJson()) { ["factions"] = "IronTeeth" }, out _), "factions not a list");
            Check(!LobbySummary.TryParse(new JObject(summary.ToJson()) { ["factions"] = new JArray("Iron Teeth") }, out _), "a bad id");
            var row = new LobbyPlayer(2, "Anna", true, false, false, 3, "IronTeeth", mayPick: true);
            Check(LobbyFrames.TryParseRoster(LobbyFrames.Roster(new[] { row }), out List<LobbyPlayer> rows)
                && rows[0].Faction == "IronTeeth" && rows[0].MayPick);
            Check(!LobbyFrames.TryParseRoster(new JObject { ["players"] = new JArray(new JObject(row.ToJson()) { ["faction"] = 7 }) }, out _));
            Check(LobbyFrames.TryParseFaction(LobbyFrames.Faction("IronTeeth"), out string picked) && picked == "IronTeeth");
            Check(!LobbyFrames.TryParseFaction(new JObject { ["faction"] = "Iron\nTeeth" }, out _));
            Check(LobbyFrames.IsGuestType(LobbyFrames.FactionType) && LobbyFrames.IsLobbyType(LobbyFrames.FactionType));
        });

        yield return ("Factions: a guest's pick in a mixed room reaches the host and every roster; picks it may not make are dropped", () =>
        {
            int previous = TimberServer.LobbyIntervalMs;
            TimberServer.LobbyIntervalMs = 50;
            var rig = new Rig(new LobbySummary("Folktails", "Diorama", null, "Beaverton", "Kyler", true, true, new[] { "Folktails", "IronTeeth" }));
            try
            {
                var guest = rig.Join();
                Check(Until(() => guest.Lobby.View().Welcomed && guest.Lobby.View().Summary!.Mixed), "not welcomed");
                // Before its hello a guest's pick is dropped (it has no row of its own yet).
                guest.SendLobbyFaction("IronTeeth");
                Check(guest.SendLobbyHello("local:anna", "Anna"));
                Check(Until(() => rig.Host.Lobby!.Snapshot().Guests.Count == 1 && rig.Host.Lobby!.Snapshot().Guests[0].SaidHello), "no hello");
                Check(rig.Host.Lobby!.Snapshot().Guests[0].Faction == null, "a pick before the hello was taken");
                Check(Until(() => guest.Lobby.View().Players.Count == 2 && guest.Lobby.View().Players[1].Faction == "Folktails"
                    && guest.Lobby.View().Players[1].MayPick), "a guest without a pick shows the room's faction and may pick");
                Check(guest.SendLobbyFaction("IronTeeth"));
                Check(Until(() => rig.Host.Lobby!.Snapshot().Guests[0].Faction == "IronTeeth"), "the pick did not reach the host");
                Check(Until(() => guest.Lobby.View().Players[1].Faction == "IronTeeth"), "the roster did not follow");
                guest.SendLobbyFaction("Otters");
                Thread.Sleep(200);
                Check(rig.Host.Lobby!.Snapshot().Guests[0].Faction == "IronTeeth", "a faction the room does not offer was taken");
                // The host's own pick is its row's, and reaches the guest.
                rig.Host.SetLobbyHostFaction("IronTeeth");
                Check(Until(() => guest.Lobby.View().Players[0].Faction == "IronTeeth"), "the host's pick did not reach the guest");
                // After Start nothing changes any more.
                rig.Host.CloseLobbyToNewcomers("closed");
                guest.SendLobbyFaction("Folktails");
                Thread.Sleep(200);
                Check(rig.Host.Lobby!.Snapshot().Guests[0].Faction == "IronTeeth", "a pick after Start was taken");
            }
            finally
            {
                rig.Dispose();
                TimberServer.LobbyIntervalMs = previous;
            }
            // A room that is not mixed takes no pick at all.
            WithRoom(plain =>
            {
                var guest = plain.Join();
                Check(Until(() => guest.Lobby.View().Welcomed));
                Check(guest.SendLobbyHello("local:bo", "Bo") && guest.SendLobbyFaction("IronTeeth"));
                Check(Until(() => plain.Host.Lobby!.Snapshot().Guests.Count == 1 && plain.Host.Lobby!.Snapshot().Guests[0].SaidHello));
                Thread.Sleep(150);
                Check(plain.Host.Lobby!.Snapshot().Guests[0].Faction == null && plain.Host.Lobby!.Snapshot().Players.All(p => p.Faction == null));
            });
        });

        yield return ("Factions: a hosted save's room seats each row as the save does, shows its colony's faction, and lets only a founder pick", () =>
        {
            var summary = LobbySummary.ForSave("Beaverton", "Spring", 2, 3, "Kyler", "Folktails", true, true, new[] { "Folktails", "IronTeeth" });
            var room = new LobbyRoom(summary)
            {
                HostStableId = "steam:1",
                // The save remembers the host in colony 1 and Anna in colony 2; anyone else takes the lowest free colony.
                Seating = ids => ids.Select(id => id == null ? (int?)null : id == "steam:1" ? 1 : id == "local:anna" ? 2 : 3).ToList(),
                FactionOfColony = colony => colony == 1 ? "Folktails" : colony == 2 ? "IronTeeth" : null,
            };
            LobbySnapshot empty = room.Snapshot();
            Check(empty.Players[0].Colony == 1 && empty.Players[0].Faction == "Folktails" && !empty.Players[0].MayPick);
            // A save that is not mixed shows its own faction on every row, and nobody picks.
            var plain = new LobbyRoom(LobbySummary.ForSave("Beaverton", "Spring", 2, 3, "Kyler", "IronTeeth", false, false, null));
            Check(plain.Snapshot().Players.All(p => p.Faction == "IronTeeth" && !p.MayPick && p.Colony == null));
        });
    }

    static bool ReadsEnd(PipeStream stream)
    {
        var buffer = new byte[1];
        try
        {
            var read = Task.Run(() => { while (stream.Read(buffer, 0, 1) == 1) { } return true; });
            return read.Wait(500);
        }
        catch (Exception) { return true; }
    }

    static byte[] Length(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    static void WithRoom(Action<Rig> test)
    {
        int previous = TimberServer.LobbyIntervalMs;
        TimberServer.LobbyIntervalMs = 50;
        var rig = new Rig();
        try { test(rig); }
        finally
        {
            rig.Dispose();
            TimberServer.LobbyIntervalMs = previous;
        }
    }

    sealed class Rig : IDisposable
    {
        readonly RigListener listener = new();
        readonly List<TimberClient> guests = new();
        readonly List<PipeStream> raws = new();
        public TimberServer Host { get; }

        public Rig(LobbySummary? summary = null)
        {
            Host = new TimberServer(listener, () => throw new InvalidOperationException("a waiting room never asks for a map"),
                () => new JObject { [TimberNetBase.TYPE_KEY] = "InitProbe", [TimberNetBase.TICKS_KEY] = 0 })
            { CompatibilityIdentity = "same" };
            Host.OpenLobby(new LobbyRoom(summary ?? new LobbySummary("Folktails", "Diorama", "NewGameMode.Normal", "Beaverton", "Kyler", true)));
            Host.Start();
        }

        public TimberClient Join(ThreadRecorder? recorder = null, Func<ISocketStream, ISocketStream>? wrap = null)
        {
            var (hostSide, guestSide) = PipeStream.Pair();
            ISocketStream served = recorder == null ? hostSide : recorder.Wrap(hostSide);
            listener.Add(wrap == null ? served : wrap(served));
            var guest = new TimberClient(guestSide) { CompatibilityIdentity = "same" };
            guests.Add(guest);
            guest.Start();
            return guest;
        }

        /// <summary>A guest that passed the handshake and then writes whatever the test wants.</summary>
        public PipeStream JoinRaw()
        {
            var (hostSide, guestSide) = PipeStream.Pair();
            listener.Add(hostSide);
            raws.Add(guestSide);
            CompatibilityHandshake.Run(guestSide, "same", false);
            return guestSide;
        }

        public void Dispose()
        {
            Host.Close();
            foreach (var guest in guests) guest.Close();
            foreach (var raw in raws) raw.Close();
        }
    }

    sealed class RigListener : ISocketListener
    {
        readonly BlockingCollection<ISocketStream> pending = new();
        public void Add(ISocketStream stream) => pending.Add(stream);
        public void Start() { }
        public ISocketStream AcceptClient() => pending.Take();
        public void Stop() => pending.CompleteAdding();
    }

    /// <summary>A guest's connection that stops taking data when told (its game froze): writes wait until it is closed.</summary>
    sealed class StallingStream
    {
        volatile bool stalled;
        public void Stall() => stalled = true;
        public ISocketStream Wrap(ISocketStream inner) => new Stream(inner, this);

        sealed class Stream : ISocketStream
        {
            readonly ISocketStream inner;
            readonly StallingStream owner;
            public Stream(ISocketStream inner, StallingStream owner) { this.inner = inner; this.owner = owner; }
            public bool Connected => inner.Connected;
            public string? Name => inner.Name;
            public int MaxChunkSize => inner.MaxChunkSize;
            public int MaxBytesPerSecond => inner.MaxBytesPerSecond;
            public int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public void Write(byte[] buffer, int offset, int count)
            {
                while (owner.stalled && inner.Connected) Thread.Sleep(10);
                if (!inner.Connected) throw new IOException("closed");
                inner.Write(buffer, offset, count);
            }
            public void Close() => inner.Close();
            public Task ConnectAsync() => inner.ConnectAsync();
        }
    }

    /// <summary>Records which threads write large chunks (the save) to a guest.</summary>
    sealed class ThreadRecorder
    {
        public readonly ConcurrentBag<int> LargeWriteThreads = new();
        public ISocketStream Wrap(ISocketStream inner) => new Stream(inner, this);

        sealed class Stream : ISocketStream
        {
            readonly ISocketStream inner;
            readonly ThreadRecorder recorder;
            public Stream(ISocketStream inner, ThreadRecorder recorder) { this.inner = inner; this.recorder = recorder; }
            public bool Connected => inner.Connected;
            public string? Name => inner.Name;
            public int MaxChunkSize => inner.MaxChunkSize;
            public int MaxBytesPerSecond => inner.MaxBytesPerSecond;
            public int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public void Write(byte[] buffer, int offset, int count)
            {
                if (count > 500) recorder.LargeWriteThreads.Add(Environment.CurrentManagedThreadId);
                inner.Write(buffer, offset, count);
            }
            public void Close() => inner.Close();
            public Task ConnectAsync() => inner.ConnectAsync();
        }
    }
}
