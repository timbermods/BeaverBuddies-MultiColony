using BeaverBuddies.Colonies;
using BeaverBuddies.IO;
using BeaverBuddies.MultiStart;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.MapRepositorySystem;
using Timberborn.NewGameConfigurationSystem;
using Timberborn.SceneLoading;
using TimberNet;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// The game a waiting room is for: a new game as the host set it up on the game's Game Mode page, or a save chosen on
    /// the main menu's Load Game box (<see cref="Save"/> set).
    /// </summary>
    public sealed class LobbySetup
    {
        /// <summary>A hosted save (Load Game → Host co-op game in the main menu); null for a new game.</summary>
        public SaveReference Save { get; set; }
        /// <summary>The save's bytes, read once: every guest and the host load exactly these.</summary>
        public byte[] SaveBytes { get; set; }
        /// <summary>The save's in-game date (from its metadata; 0 when unknown).</summary>
        public int Cycle { get; set; }
        public int Day { get; set; }
        public bool IsSave => Save != null;

        public string FactionId { get; set; }
        public MapFileReference Map { get; set; }
        public string MapName { get; set; }
        /// <summary>The validated mode: a MultiplayerNewGameModeSpec on a multi-start map (the Players field).</summary>
        public GameModeSpec Mode { get; set; }
        /// <summary>The difficulty's loc key, or null for a custom one.</summary>
        public string ModeLocKey { get; set; }
        /// <summary>The Game Mode page's own summary ("Folktails - Diorama - Normal").</summary>
        public string SummaryText { get; set; }
        public string Settlement { get; set; }
    }

    public enum LobbySessionState
    {
        Open,
        CreatingWorld,
        SendingWorld,
        Loading,
        Cancelled,
        Failed,
    }

    /// <summary>
    /// The host's waiting room for a new co-op game, from the Co-op Game page to the moment the saved world has loaded as
    /// a hosted game. It outlives the menu scene and the scene that makes the world (a static), and holds the server
    /// outside EventIO until the save is loaded: with EventIO set, the scene that makes the world would be a co-op scene
    /// that closes joining at its first tick and carries its events into the real one (design/PRE-GAME-LOBBY-PLAN.md §4.5).
    /// <para>
    /// Start: close to newcomers → make the world (single player, behind the loading screen, LobbyWorldMaker) → save it at
    /// tick 0 → send the save to every guest in the room → once each is queued, install the server as EventIO, seed the
    /// random state from the save's bytes and load the save as host, as Load Game → Host co-op game does.
    /// </para>
    /// </summary>
    public sealed class LobbySession
    {
        /// <summary>The refusal a player gets who tries to come in after Start (English, like the server's other refusals).</summary>
        public const string ClosedMessage = "The Host has already started this game from its waiting room, so it can no longer be " +
            "joined. Ask the Host to save and rehost.";
        /// <summary>How long making and saving the world may take before the host gives up on the co-op start.</summary>
        public const double CreateTimeoutMs = 120000;
        /// <summary>How long the host waits for every guest's join to be queued before it loads the save anyway.</summary>
        public const double QueueTimeoutMs = 10000;

        private static volatile bool holdLoadingScreen;

        /// <summary>The waiting room being hosted, from its page to the loaded game; null otherwise.</summary>
        public static LobbySession Current { get; private set; }

        /// <summary>The loading screen stays up (LoadingScreen.Disable is skipped) while the host's world is made.</summary>
        public static bool HoldLoadingScreen => holdLoadingScreen;

        public ServerEventIO IO { get; }
        public LobbySetup Setup { get; }
        public LobbyRoom Room { get; }
        public LobbySessionState State { get; private set; }
        /// <summary>The guests in the room when the host pressed Start, in the room's order.</summary>
        public IReadOnlyList<LobbyMemberInfo> StartedWith { get; private set; } = Array.Empty<LobbyMemberInfo>();

        private SaveReference save;
        private byte[] saveBytes;
        private double stateSinceMs;

        private LobbySession(ServerEventIO io, LobbySetup setup, LobbyRoom room)
        {
            IO = io;
            Setup = setup;
            Room = room;
            State = LobbySessionState.Open;
            stateSinceMs = RttTracker.NowMs;
        }

        private TimberServer Server => IO.NetBase;

        /// <summary>Opens the server as a waiting room. Null if it could not start (the log says why).</summary>
        public static LobbySession Open(LobbySetup setup)
        {
            EndStale("a new waiting room opened");
            // A save seats each player by who it remembers (the colony slot table in the save), which the menu can't read:
            // its rows show no colony.
            LobbySummary summary = setup.IsSave
                ? LobbySummary.ForSave(setup.Settlement, setup.Save.SaveName, setup.Cycle, setup.Day, Settings.PingDisplayName)
                : new LobbySummary(setup.FactionId, setup.MapName, setup.ModeLocKey, setup.Settlement,
                    Settings.PingDisplayName, Settings.SeparateColoniesForNewGames);
            var room = new LobbyRoom(summary);
            var io = new ServerEventIO();
            io.StartLobby(room);
            if (io.NetBase == null)
            {
                Plugin.LogError("[Lobby] The waiting room could not start its server");
                return null;
            }
            Current = new LobbySession(io, setup, room);
            Plugin.Log(setup.IsSave
                ? $"[Lobby] Waiting room open for the save \"{setup.Save.SaveName}\" of \"{setup.Settlement}\" ({setup.SaveBytes?.Length ?? 0} bytes)"
                : $"[Lobby] Waiting room open for a new game: {setup.SummaryText}, settlement \"{setup.Settlement}\"");
            return Current;
        }

        /// <summary>
        /// The host pressed Start (and answered any question, D2): nobody new comes in, and the world is made behind the
        /// loading screen. From here the menu scene is gone. A save is not made: its bytes go to the guests at once, and
        /// the host's page calls <see cref="Update"/> each frame until it loads.
        /// </summary>
        public void Start(ISceneLoader sceneLoader, string tip)
        {
            if (State != LobbySessionState.Open) return;
            LobbySnapshot snapshot = Room.Snapshot();
            StartedWith = snapshot.Guests;
            IO.CloseLobby(ClosedMessage);
            ColonySession.CloseJoiningAtStart();
            Plugin.Log($"[Lobby] Starting with {StartedWith.Count} guest(s): " +
                string.Join(", ", StartedWith.Select(g => $"{g.Number} {g.Name} ({(g.Ready ? "ready" : "not ready")})")));
            if (Setup.IsSave)
            {
                SetState(LobbySessionState.CreatingWorld);
                OnWorldSaved(Setup.Save, Setup.SaveBytes);
                return;
            }
            Server.SetLobbyStage(LobbyStage.CreatingWorld);
            SetState(LobbySessionState.CreatingWorld);

            GameModeSpec mode = Setup.Mode;
            if (mode is MultiplayerNewGameModeSpec multi)
            {
                // D12: as many starts as there are players now, at most the Players field.
                int starts = LobbyRules.StartsToFill(multi.Players, StartedWith.Count);
                Plugin.Log($"[Lobby] Filling {starts} start(s) of the {multi.Players} the Players field allows");
                mode = new MultiplayerNewGameModeSpec(multi, starts);
            }
            holdLoadingScreen = true;
            try
            {
                sceneLoader.LoadScene(GameSceneParameters.CreateNewGameParameters(
                    new NewGameConfiguration(Setup.FactionId, Setup.Map, mode, Setup.Settlement)), tip);
            }
            catch (Exception error)
            {
                Fail("the new world could not be made: " + error.Message);
            }
        }

        /// <summary>The world is saved: every guest in the room is sent its bytes (on their own threads).</summary>
        public void OnWorldSaved(SaveReference saveReference, byte[] bytes)
        {
            if (State != LobbySessionState.CreatingWorld) return;
            save = saveReference;
            saveBytes = bytes;
            // Kept here and by the server from now on: the setup's copy of a save's bytes is not needed again.
            Setup.SaveBytes = null;
            Plugin.Log($"[Lobby] Sending \"{saveReference.SaveName}\" ({bytes.Length} bytes) to the guests");
            Server.SetLobbyStage(LobbyStage.SendingWorld);
            Server.ReleaseLobby(bytes);
            SetState(LobbySessionState.SendingWorld);
        }

        /// <summary>
        /// Every frame of the scene that makes the world (for a save: of the menu, from the host's page). Once each guest's
        /// join is queued (so it gets everything the host plays from tick 0 on), the save is loaded as the hosted game.
        /// </summary>
        public void Update(ISceneLoader sceneLoader, string loadingTip)
        {
            double elapsed = RttTracker.NowMs - stateSinceMs;
            if (State == LobbySessionState.CreatingWorld && elapsed > CreateTimeoutMs)
            {
                Fail("the new world was not saved in time");
                return;
            }
            if (State != LobbySessionState.SendingWorld) return;
            if (!Server.LobbyGuestsQueued)
            {
                if (elapsed < QueueTimeoutMs) return;
                // A guest whose join never got going (its connection stalled): it would miss what the host plays, so it
                // does not come in. The others play on; it can come back after a Save and Rehost.
                foreach (LobbyMemberInfo guest in Room.Snapshot().Guests.Where(g => !g.InGame))
                {
                    Plugin.LogWarning($"[Lobby] Guest {guest.Number} ({guest.Name}) was not ready to receive the world; leaving it out");
                    Server.RemoveFromLobby(guest.Number, LobbyEndReason.Failed, "Your connection did not take the world in time.");
                }
            }
            SetState(LobbySessionState.Loading);
            Plugin.Log("[Lobby] Every guest has the world on its way; loading it as the hosted game");
            // LoadAndHost's confirm path: the server becomes the session, the random state comes from the same bytes the
            // guests load, and the save loads with co-op services bound.
            EventIO.Set(IO);
            DeterminismService.InitGameStartState(saveBytes);
            holdLoadingScreen = false;
            try
            {
                sceneLoader.LoadScene(GameSceneParameters.CreateGameSaveParameters(save), loadingTip);
            }
            catch (Exception error)
            {
                Fail("the saved world could not be loaded: " + error.Message);
            }
        }

        /// <summary>The hosted game has loaded from the save: the waiting room is over.</summary>
        public static void FinishIfLoaded()
        {
            LobbySession session = Current;
            if (session == null || session.State != LobbySessionState.Loading) return;
            Plugin.Log("[Lobby] The hosted game has loaded; the waiting room is over");
            holdLoadingScreen = false;
            Current = null;
        }

        /// <summary>The host closed the waiting room before starting: everyone in it is told, and the server stops.</summary>
        public void Cancel()
        {
            if (State != LobbySessionState.Open) return;
            Plugin.Log("[Lobby] The host closed the waiting room");
            try { Server?.CancelLobby(LobbyEndReason.Cancelled, null); }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not tell the guests: " + error.Message); }
            IO.Close();
            SetState(LobbySessionState.Cancelled);
            if (Current == this) Current = null;
        }

        /// <summary>
        /// The co-op start could not go on. Guests still waiting are told why; the host keeps whatever scene it is in (a
        /// solo game when the world was made). The caller brings the loading screen down (the hold is released here).
        /// </summary>
        public void Fail(string reason)
        {
            if (State == LobbySessionState.Cancelled || State == LobbySessionState.Failed) return;
            Plugin.LogError("[Lobby] The co-op start failed: " + reason);
            try { Server?.CancelLobby(LobbyEndReason.Failed, reason); }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not tell the guests: " + error.Message); }
            EventIO.ResetIf(IO);
            IO.Close();
            SetState(LobbySessionState.Failed);
            holdLoadingScreen = false;
            if (Current == this) Current = null;
            FailureReason = reason;
        }

        /// <summary>Why the last co-op start failed, for the host's box in the solo world; null once shown.</summary>
        public static string FailureReason { get; set; }

        /// <summary>A waiting room left over from a scene the flow did not expect (the main menu loading, say) ends.</summary>
        public static void EndStale(string why)
        {
            LobbySession session = Current;
            if (session == null) return;
            if (session.State == LobbySessionState.Open) session.Cancel();
            else session.Fail(why);
            FailureReason = null;
        }

        private void SetState(LobbySessionState state)
        {
            State = state;
            stateSinceMs = RttTracker.NowMs;
        }
    }
}
