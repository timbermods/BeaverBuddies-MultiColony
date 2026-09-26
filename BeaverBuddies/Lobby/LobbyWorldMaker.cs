using BeaverBuddies.Colonies;
using BeaverBuddies.Connect;
using BeaverBuddies.Util;
using HarmonyLib;
using System;
using Timberborn.Autosaving;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameSceneLoading;
using Timberborn.SceneLoading;
using Timberborn.SettlementNameSystem;
using Timberborn.SingletonSystem;
using TimberNet;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// The host's side of a waiting room's Start, in the game scenes (bound in every one; it does nothing without a
    /// LobbySession). In the scene that makes the new world, a single-player one since the server is not EventIO yet: it
    /// fills the colony slot table in the room's order, saves the world at tick 0 (queued in the frame the game posts
    /// NewGameInitializedEvent, so before its unpause), reads the save's bytes a frame later and hands them to the
    /// session, which sends them and loads them as the hosted game. In that game it ends the session.
    /// </summary>
    public class LobbyWorldMaker : ILoadableSingleton, IUpdatableSingleton
    {
        /// <summary>How long a queued save may take to be written before the start is given up.</summary>
        private const double SaveTimeoutMs = 20000;

        private readonly EventBus _eventBus;
        private readonly GameSaver _gameSaver;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly SettlementReferenceService _settlementReferenceService;
        private readonly AutosaveNameService _autosaveNameService;
        private readonly ISceneLoader _sceneLoader;
        private readonly LoadingScreen _loadingScreen;
        private readonly DialogBoxShower _dialogBoxShower;

        private bool making;
        private SaveReference queuedSave;
        private double queuedAtMs;
        private bool saveWritten;
        private int framesSinceWritten;

        public LobbyWorldMaker(EventBus eventBus, GameSaver gameSaver, GameSaveRepository gameSaveRepository,
            SettlementReferenceService settlementReferenceService, AutosaveNameService autosaveNameService,
            ISceneLoader sceneLoader, LoadingScreen loadingScreen, DialogBoxShower dialogBoxShower)
        {
            _eventBus = eventBus;
            _gameSaver = gameSaver;
            _gameSaveRepository = gameSaveRepository;
            _settlementReferenceService = settlementReferenceService;
            _autosaveNameService = autosaveNameService;
            _sceneLoader = sceneLoader;
            _loadingScreen = loadingScreen;
            _dialogBoxShower = dialogBoxShower;
        }

        public void Load()
        {
            LobbySession session = LobbySession.Current;
            if (session == null) return;
            if (session.State == LobbySessionState.Loading)
            {
                LobbySession.FinishIfLoaded();
                return;
            }
            if (session.State != LobbySessionState.CreatingWorld) return;
            if (!_sceneLoader.TryGetSceneParameters(out GameSceneParameters parameters) || !parameters.NewGame)
            {
                Fail(session, "the game didn't open the new world");
                return;
            }
            making = true;
            _eventBus.Register(this);
            Plugin.Log("[Lobby] Making the new world for the co-op game");
        }

        [OnEvent]
        public void OnNewGameInitialized(NewGameInitializedEvent newGameInitializedEvent)
        {
            LobbySession session = LobbySession.Current;
            if (!making || session == null || session.State != LobbySessionState.CreatingWorld || queuedSave != null) return;
            try
            {
                SeatInRoomOrder(session);
                string name = LobbyRules.SaveName(_autosaveNameService.Timestamp());
                queuedSave = new SaveReference(name, _settlementReferenceService.SettlementReference);
                queuedAtMs = RttTracker.NowMs;
                // Written in this frame's LateUpdate, after every other listener of this event, before the game unpauses.
                _gameSaver.QueueSaveSkippingNameValidation(queuedSave, () => saveWritten = true);
            }
            catch (Exception error)
            {
                Fail(session, "the new world couldn't be saved: " + error.Message);
            }
        }

        public void UpdateSingleton()
        {
            LobbySession session = LobbySession.Current;
            if (!making || session == null) return;
            try
            {
                if (session.State == LobbySessionState.CreatingWorld && queuedSave != null)
                {
                    if (!saveWritten)
                    {
                        if (RttTracker.NowMs - queuedAtMs > SaveTimeoutMs) Fail(session, "the new world's save wasn't written");
                        return;
                    }
                    // The save's file is closed only after its callback returns: read it a frame later (RehostingService).
                    if (framesSinceWritten++ < 1) return;
                    byte[] bytes = ServerHostingUtils.GetMapBtyes(_gameSaveRepository, queuedSave);
                    session.OnWorldSaved(queuedSave, bytes);
                    return;
                }
                session.Update(_sceneLoader, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Tip.Loading"));
                if (session.State == LobbySessionState.Failed) AfterFailure();
            }
            catch (Exception error)
            {
                Fail(session, error.Message);
            }
        }

        /// <summary>
        /// The colony slot table, filled before the world's first save in the order the room showed: the host colony 1,
        /// then each guest (D11). Each guest's hello in the game then finds its colony. Only a separate-colonies save
        /// keeps the table; in a shared game this changes nothing.
        /// </summary>
        private static void SeatInRoomOrder(LobbySession session)
        {
            ColonySlotTable table = ColonySlotService.Instance?.Table;
            if (table == null) return;
            table.Resolve(LocalPlayerIdentity.Id, LocalPlayerIdentity.Name);
            foreach (LobbyMemberInfo guest in session.StartedWith)
            {
                // A guest that never said who it is, over a direct connection, has no id yet: it takes the lowest free
                // colony when it says hello in the game, as before.
                if (string.IsNullOrEmpty(guest.StableId)) continue;
                int? slot = table.Resolve(guest.StableId, guest.Name);
                Plugin.Log($"[Lobby] Guest {guest.Number} ({guest.Name}) seated {(slot.HasValue ? $"in colony {slot.Value + 1}" : "as a helper")}");
            }
        }

        private void Fail(LobbySession session, string reason)
        {
            making = false;
            session.Fail(reason);
            AfterFailure();
        }

        // The host stays in the world it made, as a solo game, and is told why.
        private void AfterFailure()
        {
            making = false;
            try { _loadingScreen.Disable(); } catch (Exception error) { Plugin.LogWarning("[Lobby] " + error.Message); }
            string reason = LobbySession.FailureReason;
            LobbySession.FailureReason = null;
            if (reason == null) return;
            try
            {
                _dialogBoxShower.Create()
                    .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Failed", reason))
                    .Show();
            }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not show why the co-op start failed: " + error.Message); }
        }
    }

    [ManualMethodOverwrite]
    /*
     * 2026-09-22, Timberborn 1.1.2.4, SceneLoading: LoadingScreen.Disable
        this.LoadingScreenDisabled?.Invoke(this, EventArgs.Empty);
        Hide();
     */
    /// <summary>
    /// Keeps the loading screen up while a waiting room's world is made: the scene loader brings it down when the new
    /// world has loaded, and the host would see (and hear) that world before it is thrown away for its reloaded save.
    /// </summary>
    [HarmonyPatch(typeof(LoadingScreen), nameof(LoadingScreen.Disable))]
    public class LoadingScreenDisablePatcher
    {
        public static bool Prefix() => !LobbySession.HoldLoadingScreen;
    }
}
