using BeaverBuddies.Factions;
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
using Timberborn.SaveMetadataSystem;
using Timberborn.UIFormatters;
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
    /// settlement's name), it shows the room as it fills, and Start Game starts the new co-op game (LobbySession). Since
    /// 1.4.0-beta19 it is also what Host co-op game on the main menu's Load Game box opens for a save (OpenForSave).
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
        private readonly GameSaveDeserializer _gameSaveDeserializer;
        private readonly SaveMetadataSerializer _saveMetadataSerializer;
        private readonly TimestampFormatter _timestampFormatter;

        private LobbyPage page;
        private LobbySession session;
        // A save's room after Start: its bytes are going out, and this page loads it once every guest is queued.
        private LobbySession sending;
        private FactionSpec faction;
        private int shownVersion = -1;
        private bool starting;
        private static string lastSettlementName;
        // A mixed-factions room: the host's own faction switcher (a new game), and the line under the settlement.
        private LobbyFactionPicker picker;
        private string factionNote;

        public LobbyHostPanel(VisualElementLoader loader, VisualElementInitializer initializer, PanelStack panelStack,
            DialogBoxShower dialogBoxShower, ITooltipRegistrar tooltipRegistrar, ISceneLoader sceneLoader,
            GameSaveRepository gameSaveRepository, InputService inputService, GameSaveDeserializer gameSaveDeserializer,
            SaveMetadataSerializer saveMetadataSerializer, TimestampFormatter timestampFormatter)
        {
            _gameSaveDeserializer = gameSaveDeserializer;
            _saveMetadataSerializer = saveMetadataSerializer;
            _timestampFormatter = timestampFormatter;
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
            // Mixed factions for new games (D9): with every faction unlocked here, each player picks theirs in the room.
            factionNote = null;
            NewGameFactionCapture capture = NewGameFactionCapture.Instance;
            string locked = null;
            bool notTwo = false;
            if (capture != null && capture.MixedAvailable(out locked, out notTwo))
            {
                setup.Mixed = true;
                setup.Factions = capture.OfferedFactions();
                // The Game Mode page's summary without the faction: each player has their own.
                setup.SummaryText = modePanel._map.DisplayName + " - "
                    + RegisteredLocalizationService.T(setup.ModeLocKey ?? "NewGameConfigurationPanel.Custom");
                factionNote = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Faction.Mixed");
            }
            else if (locked != null) factionNote = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Faction.NotUnlocked", locked);
            else if (notTwo) factionNote = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Faction.NotTwo");
            SettlementNamePanel.Show(_panelStack, _gameSaveRepository, _dialogBoxShower, _loader, _initializer, _inputService,
                lastSettlementName, name =>
                {
                    lastSettlementName = name;
                    setup.Settlement = name;
                    OpenRoom(setup);
                });
        }

        /// <summary>
        /// Host co-op game on the main menu's Load Game box, once the game's own checks of the save have passed
        /// (ServerHostingUtils.LoadAndHost): the waiting room for that save, with <paramref name="bytes"/> as everyone's copy.
        /// </summary>
        public void OpenForSave(SaveReference save, byte[] bytes)
        {
            if (session != null || sending != null) return;
            faction = null;
            int cycle = 0, day = 0;
            try
            {
                SaveMetadata metadata = _gameSaveDeserializer.ReadFromSaveFile(save, _saveMetadataSerializer);
                if (metadata != null)
                {
                    cycle = metadata.Cycle;
                    day = metadata.Day;
                }
            }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not read the save's date: " + error.Message); }
            string settlement = save.SettlementReference.SettlementName;
            // The save's own colonies and factions, read from its bytes without loading it (display only).
            SaveColonyInfo colonies = SaveColonyReader.Read(bytes);
            if (colonies == null) Plugin.LogWarning("[Lobby] Could not read the save's colonies; its rows show none");
            NewGameFactionCapture capture = NewGameFactionCapture.Instance;
            faction = colonies != null ? capture?.Spec(colonies.BaseFaction) : null;
            factionNote = colonies != null && colonies.Mixed ? RegisteredLocalizationService.T("BeaverBuddies.Lobby.Faction.MixedSave") : null;
            OpenRoom(new LobbySetup
            {
                Save = save,
                SaveBytes = bytes,
                Cycle = cycle,
                Day = day,
                Settlement = settlement,
                SummaryText = settlement,
                SaveColonies = colonies,
                Mixed = colonies != null && colonies.Mixed,
                Factions = colonies != null && colonies.Mixed ? capture?.UnlockedFactions() ?? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(),
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
            page.SetSummary(setup.SummaryText,
                setup.IsSave ? LobbyPage.SaveLine(_timestampFormatter, setup.Save.SaveName, setup.Cycle, setup.Day) : setup.Settlement);
            page.SetFactionNote(factionNote);
            page.SetFactions(id => NewGameFactionCapture.Instance?.Spec(id));
            LocalFactionPick.Clear();
            picker = null;
            if (setup.Mixed && !setup.IsSave)
            {
                // The host picks its own colony's faction here too (the new game's faction), with the game's own switcher.
                picker = new LobbyFactionPicker(_initializer);
                picker.Set(setup.Factions.Select(id => NewGameFactionCapture.Instance?.Spec(id)), setup.FactionId, true);
                picker.Changed += id =>
                {
                    if (session == null || session.Setup != setup) return;
                    setup.FactionId = id;
                    faction = NewGameFactionCapture.Instance?.Spec(id);
                    session.IO.NetBase?.SetLobbyHostFaction(id);
                    Plugin.Log($"[Lobby] The host will play {id}");
                    shownVersion = -1;
                };
                page.SetFactionPicker(picker);
            }
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

        // Esc, or Cancel: back to the Game Mode page (or the Load Game box), and everyone in the room is told.
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
            if (sending != null)
            {
                try { SendSave(); }
                catch (Exception error) { sending.Fail(error.Message); }
                return;
            }
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
            bool open = snapshot.Stage == LobbyStage.Open;
            page.SetPlayers(snapshot.Players, 0, hostPage: true, canChange: open, faction?.Logo.Asset);
            if (picker != null) picker.Set(session.Setup.Factions.Select(id => NewGameFactionCapture.Instance?.Spec(id)), session.Setup.FactionId, open);
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
            // The menu scene ends here for a new game; the session carries on (LobbyWorldMaker).
            session = null;
            started.Start(_sceneLoader, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Tip.Creating"));
            if (started.State == LobbySessionState.SendingWorld)
            {
                // A save: its bytes are going out; this page stays up (Starting...) until it loads (SendSave).
                sending = started;
                return;
            }
            if (started.State != LobbySessionState.Failed) return;
            ShowFailure();
        }

        // A save's room after Start: the session loads the save once every guest's join is queued.
        private void SendSave()
        {
            sending.Update(_sceneLoader, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Tip.Loading"));
            if (sending.State == LobbySessionState.Loading)
            {
                sending = null;
                return;
            }
            if (sending.State != LobbySessionState.Failed) return;
            sending = null;
            ShowFailure();
        }

        // Back to the page before the room (the Game Mode page, or the Load Game box), with the reason.
        private void ShowFailure()
        {
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
