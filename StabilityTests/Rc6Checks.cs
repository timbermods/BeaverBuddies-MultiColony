using BeaverBuddies.Connect;
using TimberNet;

/// <summary>
/// 1.4.0-rc6, the two join rough edges left after the rc5 review: an invite accepted in a game, and a direct join waited
/// for on the game thread. Checks that need no game.
/// </summary>
static class Rc6Checks
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

    // A connection whose connect is whatever Task it is given; nothing is ever read or written.
    sealed class ConnectingStream : ISocketStream
    {
        private readonly Task connecting;
        public ConnectingStream(Task connecting) { this.connecting = connecting; }
        public bool Connected => connecting.IsCompletedSuccessfully;
        public string Name => "connecting";
        public int MaxChunkSize => 64;
        public int MaxBytesPerSecond => int.MaxValue;
        public Task ConnectAsync() => connecting;
        public int Read(byte[] buffer, int offset, int count) => 0;
        public void Write(byte[] buffer, int offset, int count) { }
        public void Close() { }
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("rc6: an invite accepted in a game never connects from it: alone, the game is saved and the page joined from the main menu", () =>
        {
            Check(InviteRules.Decide(inMainMenu: true, inCoopSession: false, hostingPage: false) == InviteStep.Join, "the main menu no longer joins");
            Check(InviteRules.Decide(false, false, false) == InviteStep.OfferFromGame, "a game played alone is not offered the join");
            Check(InviteRules.Decide(false, true, false) == InviteStep.LeaveCoopGameFirst, "an invite takes over a running co-op game's connection");
            Check(InviteRules.Decide(true, false, true) == InviteStep.StopHostingFirst && InviteRules.Decide(false, false, true) == InviteStep.StopHostingFirst,
                "a host's own Co-op Game page is replaced by a join");
            // Only the Steam build has the service's body (IS_STEAM).
            string steam = Source("BeaverBuddies", "Steam", "SteamOverlayConnectionService.cs");
            string join = Body(steam, "private void JoinHostLobby(CSteamID lobby, CSteamID owner)");
            int decide = join.IndexOf("InviteRules.Decide(", StringComparison.Ordinal);
            int connect = join.IndexOf("_clientConnectionService.TryToConnect(owner)", StringComparison.Ordinal);
            Check(decide > 0 && connect > decide, "a lobby entered in a game still connects before anything is asked");
            Check(Body(steam, "private void OnLobbyEntered(LobbyEnter_t callback)").Contains("JoinHostLobby(lobby, owner);"), "an entered lobby no longer goes through the invite's rule");
            string offer = Body(steam, "private void OfferJoinFromGame(CSteamID lobby, string host)");
            int pending = offer.IndexOf("pendingInviteLobby = lobby.m_SteamID;", StringComparison.Ordinal);
            int leave = offer.IndexOf("_mainMenuSceneLoader.SaveAndOpenMainMenu();", StringComparison.Ordinal);
            Check(pending > 0 && leave > pending, "the game is not saved and left, or the invite is forgotten on the way");
            Check(offer.Contains("SetCancelButton(() => LeaveSafely(lobby)"), "staying keeps this player in the invite's lobby");
            string menu = Body(steam, "private void JoinPendingInvite()");
            Check(menu.Contains("LobbyGuestPanel>() == null) return;") && menu.Contains("_panelStack.TopPanel.IsOverlay) return;")
                && menu.Contains("JoinHostLobby(lobby, owner);"), "the main menu does not join the invite once it is up");
            Check(Body(steam, "public void UpdateSingleton()").Contains("if (done) JoinPendingInvite();"), "nothing joins the invite in the main menu");
            string csv = Source("BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv");
            foreach (string key in new[] { "FromGame", "SaveAndJoin", "InCoopGame", "WhileHosting" })
                Check(csv.Contains("\nBeaverBuddies.Invite." + key + ",\""), "no text for BeaverBuddies.Invite." + key);
        });

        yield return ("rc6: a direct join is connected on the network thread, never waited for on the game thread", () =>
        {
            // A connection that never answers: Start returns at once.
            var never = new TimberClient(new ConnectingStream(new TaskCompletionSource<bool>().Task));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try { never.Start(); }
            finally { never.Close(); }
            Check(watch.ElapsedMilliseconds < 1000, $"Start waited {watch.ElapsedMilliseconds} ms for the connection on the caller's thread");
            // A refused one is reported as any later failure: OnError, from Update, with the transport's reason.
            var refused = new TimberClient(new ConnectingStream(Task.Run(async () => { await Task.Delay(50); throw new IOException("refused by the test"); })));
            string? error = null;
            refused.OnError += message => error = message;
            try
            {
                refused.Start();
                var until = DateTime.UtcNow.AddSeconds(5);
                while (error == null && DateTime.UtcNow < until) { refused.Update(); Thread.Sleep(10); }
            }
            finally { refused.Close(); }
            Check(error != null && error.Contains("refused by the test"), "a refused connection is not reported, or loses its reason: " + (error ?? "nothing"));
        });
    }
}
