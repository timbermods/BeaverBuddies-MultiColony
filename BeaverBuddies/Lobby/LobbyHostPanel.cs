using BeaverBuddies.Steam;
using BeaverBuddies.Util;
using System;
using System.Linq;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.InputSystem;
using Timberborn.MainMenuPanels;
using Timberborn.NewGameConfigurationSystem;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using TimberNet;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// The host's Co-op Game page, the New Game wizard's page after Game Mode (D7): opened by Host co-op game (after the
    /// settlement's name), it shows the room as it fills, and Start Game starts the new co-op game (LobbySession).
    /// </summary>
    public class LobbyHostPanel : RegisteredSingleton, IPanelController, IUpdatableSingleton
    {
        private readonly VisualElementLoader _loader;
        private readonly VisualElementInitializer _initializer;
        private readonly PanelStack _panelStack;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly ISceneLoader _sceneLoader;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly InputService _inputService;

        private LobbyPage page;
        private LobbySession session;
        private FactionSpec faction;
        private int shownVersion = -1;
        private bool starting;
        private static string lastSettlementName;

        public LobbyHostPanel(VisualElementLoader loader, VisualElementInitializer initializer, PanelStack panelStack,
            DialogBoxShower dialogBoxShower, ITooltipRegistrar tooltipRegistrar, ISceneLoader sceneLoader,
            GameSaveRepository gameSaveRepository, InputService inputService)
        {
            _loader = loader;
            _initializer = initializer;
            _panelStack = panelStack;
            _dialogBoxShower = dialogBoxShower;
            _tooltipRegistrar = tooltipRegistrar;
            _sceneLoader = sceneLoader;
            _gameSaveRepository = gameSaveRepository;
            _inputService = inputService;
        }

        /// <summary>Host co-op game on the Game Mode page: the settlement's name, then the waiting room.</summary>
        public void OpenFrom(NewGameModePanel modePanel)
        {
            if (session != null || !modePanel.TryGetValidatedGameMode(out GameModeSpec mode)) return;
            faction = modePanel._factionSpec;
            var setup = new LobbySetup
            {
                FactionId = modePanel._factionSpec.Id,
                Map = modePanel._map.MapFileReference,
                MapName = modePanel._map.DisplayName,
                Mode = mode,
                ModeLocKey = modePanel._predefinedGameMode?.DisplayNameLocKey,
                SummaryText = modePanel._summary.text,
            };
            SettlementNamePanel.Show(_panelStack, _gameSaveRepository, _dialogBoxShower, _loader, _initializer, _inputService,
                lastSettlementName, name =>
                {
                    lastSettlementName = name;
                    setup.Settlement = name;
                    OpenRoom(setup);
                });
        }

        private void OpenRoom(LobbySetup setup)
        {
            session = LobbySession.Open(setup);
            if (session == null)
            {
                _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.CouldNotOpen")).Show();
                return;
            }
            page = new LobbyPage(_loader, _initializer, _tooltipRegistrar, "BeaverBuddies.Lobby.Header.Host");
            page.Back.text = RegisteredLocalizationService.T(CommonLocKeys.CancelKey);
            page.Next.text = RegisteredLocalizationService.T("BeaverBuddies.Host.StartGame");
            page.Back.clicked += OnUICancelled;
            page.Next.clicked += () => OnUIConfirmed();
            page.Invite.clicked += () => session?.IO.SteamListener?.ShowInviteFriendsPanel();
            page.RemoveClicked += ConfirmRemove;
            page.SetSummary(setup.SummaryText, faction, setup.Settlement);
            page.DirectIp.text = RegisteredLocalizationService.T("BeaverBuddies.Lobby.DirectIp", Settings.Port);
            shownVersion = -1;
            starting = false;
            Refresh();
            _panelStack.HideAndPush(this);
        }

        public VisualElement GetPanel() => page?.Root;

        // Enter, or Start Game.
        public bool OnUIConfirmed()
        {
            if (starting || session == null) return false;
            LobbySnapshot snapshot = session.Room.Snapshot();
            switch (LobbyRules.StartConfirm(snapshot.Players))
            {
                case StartQuestion.Alone:
                    Ask(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Alone"), StartNow);
                    break;
                case StartQuestion.NotReady:
                    string names = string.Join(", ", LobbyRules.NotReady(snapshot.Players)
                        .Select(p => p.Joining ? RegisteredLocalizationService.T("BeaverBuddies.Lobby.Joining") : p.Name));
                    Ask(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.NotReady", names), StartNow);
                    break;
                default:
                    StartNow();
                    break;
            }
            return true;
        }

        // Esc, or Cancel: back to the Game Mode page, and everyone in the room is told.
        public void OnUICancelled()
        {
            if (starting || session == null) return;
            if (session.Room.Snapshot().Guests.Count > 0)
                Ask(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Cancel"), CloseRoom,
                    RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Close"));
            else CloseRoom();
        }

        public void UpdateSingleton()
        {
            if (page == null || session == null || starting) return;
            try { Refresh(); }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not update the waiting room: " + error.Message); }
        }

        private void Refresh()
        {
            SteamListener steam = session.IO.SteamListener;
            page.Invite.ToggleDisplayStyle(steam != null);
            page.Invite.SetEnabled(steam != null && steam.LobbyID.IsValid());
            if (session.Room.Version == shownVersion) return;
            LobbySnapshot snapshot = session.Room.Snapshot();
            shownVersion = snapshot.Version;
            page.SetPlayers(snapshot.Players, 0, hostPage: true, canChange: snapshot.Stage == LobbyStage.Open, faction?.Logo.Asset);
            page.SetStatus(LobbyRules.HostStatus(snapshot.Players, snapshot.Stage));
        }

        private void StartNow()
        {
            if (starting || session == null) return;
            starting = true;
            page.Back.SetEnabled(false);
            page.Next.SetEnabled(false);
            page.SetStatus(new LobbyText(LobbyRules.KeyPrefix + "Status.Starting"));
            LobbySession started = session;
            // The menu scene ends here; the session carries on (LobbyWorldMaker).
            session = null;
            started.Start(_sceneLoader, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Tip.Creating"));
            if (started.State != LobbySessionState.Failed) return;
            // The world could not even begin to load: back to the Game Mode page, with the reason.
            starting = false;
            if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
            page = null;
            string reason = LobbySession.FailureReason;
            LobbySession.FailureReason = null;
            _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Failed", reason ?? "")).Show();
        }

        private void CloseRoom()
        {
            if (session == null) return;
            session.Cancel();
            session = null;
            if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
            page = null;
        }

        private void ConfirmRemove(LobbyPlayer player)
        {
            if (session == null || starting) return;
            LobbySession current = session;
            Ask(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Remove", player.Name),
                () => current.IO.NetBase?.RemoveFromLobby(player.Number),
                RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.RemoveButton"),
                RegisteredLocalizationService.T(CommonLocKeys.CancelKey));
        }

        // The game's yes/no box. Its buttons close it before their action runs, so the page is on top again by then.
        private void Ask(string message, Action onYes, string yesText = null, string noText = null)
        {
            _dialogBoxShower.Create()
                .SetMessage(message)
                .SetConfirmButton(onYes, yesText ?? RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Start"))
                .SetCancelButton(() => { }, noText ?? RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.KeepWaiting"))
                .Show();
        }
    }
}
