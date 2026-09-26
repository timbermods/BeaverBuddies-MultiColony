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
    /// The host's Co-op Game room, as it fills, and Start Game, which starts the co-op game (LobbySession). A new game's room
    /// is the New Game wizard's page after Game Mode (D7), opened by Host co-op game (after the settlement's name). A save's
    /// room (OpenForSave) is opened by the Load game box's Host co-op game, in the main menu as that page, and in a game
    /// (1.4.0-rc7) as a window over the game, which pauses under it: also by the game menu's Host co-op game and Save and
    /// Rehost, which save the game first. Bound in both scenes.
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
        // A hosted shared save: Separate colonies, and Separate science and unlocks under it (1.4.0-rc4).
        private VisualElement convertOptions;
        private Toggle convertSeparate, convertScience;
        private VisualElement convertScienceRow;

        // The room's gold line under the plate (LobbyRules.ColonyNoteKey), or none.
        private static string Note(string key) => key == null ? null : RegisteredLocalizationService.T(key);

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
                // The page's colony checkboxes as they are now (1.4.0-rc3): the room's world is made with these.
                Separate = NewGameColonyChoice.Separate,
                SeparateScience = NewGameColonyChoice.SeparateScience,
            };
            // Mixed factions for new games (D9): with every faction unlocked here, each player picks theirs in the room. The
            // page greys the checkbox when it can't be (and says why), so the room has nothing to explain.
            NewGameFactionCapture capture = NewGameFactionCapture.Instance;
            if (capture != null && capture.MixedAvailable(out _, out _))
            {
                setup.Mixed = true;
                setup.Factions = capture.OfferedFactions();
                // The Game Mode page's summary without the faction: each player has their own.
                setup.SummaryText = modePanel._map.DisplayName + " - "
                    + RegisteredLocalizationService.T(setup.ModeLocKey ?? "NewGameConfigurationPanel.Custom");
            }
            factionNote = Note(LobbyRules.ColonyNoteKey(false, setup.FactionId, setup.Separate, setup.Mixed));
            SettlementNamePanel.Show(_panelStack, _gameSaveRepository, _dialogBoxShower, _loader, _initializer, _inputService,
                lastSettlementName, name =>
                {
                    lastSettlementName = name;
                    setup.Settlement = name;
                    OpenRoom(setup);
                });
        }

        /// <summary>
        /// A save's waiting room, with <paramref name="bytes"/> as everyone's copy: the Load game box's Host co-op game, once
        /// the game's own checks of the save have passed (ServerHostingUtils.LoadAndHost), or this game just saved for it
        /// (<paramref name="savedForRoom"/>: the game menu's Host co-op game and Save and Rehost). The main menu's page, or a
        /// window over the game.
        /// </summary>
        public void OpenForSave(SaveReference save, byte[] bytes, bool savedForRoom = false)
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
            factionNote = Note(LobbyRules.ColonyNoteKey(true, colonies?.BaseFaction, colonies?.SeparateColonies ?? false, colonies?.Mixed ?? false));
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
                SavedForRoom = savedForRoom,
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
            // In a game, a window over it (its sheets added: a game has not the main menu's); in the main menu, the page.
            InGameLobby inGame = InGameLobby.Current;
            page = new LobbyPage(_loader, _initializer, _tooltipRegistrar, "BeaverBuddies.Lobby.Header.Host",
                inGame != null ? LobbyFrame.Window : LobbyFrame.Page, inGame != null ? inGame.AttachStyles : (Action<VisualElement>)null);
            page.Back.text = RegisteredLocalizationService.T(CommonLocKeys.CancelKey);
            page.Next.text = RegisteredLocalizationService.T("BeaverBuddies.Host.StartGame");
            page.Back.clicked += OnUICancelled;
            // The window's close button is its Cancel (asking first, with guests in the room).
            if (page.Close != null) page.Close.clicked += OnUICancelled;
            page.Next.clicked += () => OnUIConfirmed();
            page.Invite.clicked += () => session?.IO.SteamListener?.ShowInviteFriendsPanel();
            page.RemoveClicked += ConfirmRemove;
            page.SetSummary(setup.SummaryText,
                setup.IsSave ? LobbyPage.SaveLine(_timestampFormatter, setup.Save.SaveName, setup.Cycle, setup.Day) : setup.Settlement);
            page.SetFactionNote(factionNote);
            page.SetColonyOptions(BuildConvertOptions(setup));
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
            // In place of what it was opened from (the Game Mode page, the Load game box, the game menu), so Cancel returns
            // there; over the game when nothing is open (a desync's Save and Rehost). Either way a game pauses under it.
            if (LobbyRules.HostRoomPush(_panelStack._stack.Count) == RoomPush.Push) _panelStack.Push(this);
            else _panelStack.HideAndPush(this);
        }

        /// <summary>
        /// A hosted save that is not separate colonies (a single-player game, or a shared co-op one): the host may make it
        /// one at Start, for good, with the New Game page's checkboxes (Separate colonies, and Separate science and
        /// unlocks under it). Unticked by default: the save stays what it is. Null for any other room.
        /// </summary>
        private VisualElement BuildConvertOptions(LobbySetup setup)
        {
            convertOptions = null;
            convertSeparate = convertScience = null;
            convertScienceRow = null;
            if (!setup.IsSave || setup.SaveColonies == null || setup.SaveColonies.SeparateColonies) return null;
            convertOptions = NewGameColonyOptions.CheckboxColumn("BeaverBuddiesConvertOptions");
            convertSeparate = NewGameColonyOptions.CheckboxRow(convertOptions, "BeaverBuddies.NewGame.SeparateColonies", false, _initializer,
                out VisualElement separateRow, out _);
            convertScience = NewGameColonyOptions.CheckboxRow(convertOptions, "BeaverBuddies.NewGame.SeparateScience", true, _initializer,
                out convertScienceRow, out _);
            _tooltipRegistrar.Register(separateRow, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Convert.SeparateTooltip"));
            _tooltipRegistrar.Register(convertScienceRow, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Convert.ScienceTooltip"));
            setup.ConvertSeparate = false;
            // The science starts shared: the players of a shared save earned it together, and separate science here would
            // leave each friend's new colony with none of it (1.4.0-rc5 review, B5). (A guest's split, since rc13, gives
            // every colony the unlocks so far instead.)
            setup.ConvertScience = false;
            convertSeparate.SetValueWithoutNotify(false);
            convertScience.SetValueWithoutNotify(setup.ConvertScience);
            convertScienceRow.ToggleDisplayStyle(false);
            convertSeparate.RegisterValueChangedCallback(change =>
            {
                if (session == null || session.Setup != setup || starting) return;
                setup.ConvertSeparate = change.newValue;
                convertScienceRow.ToggleDisplayStyle(change.newValue);
                session.IO.NetBase?.SetLobbySeparateAtStart(change.newValue);
                factionNote = Note(LobbyRules.ColonyNoteKey(true, setup.SaveColonies.BaseFaction, false, false, separateAtStart: change.newValue));
                page?.SetFactionNote(factionNote);
                Plugin.Log($"[Lobby] The save {(change.newValue ? "becomes separate colonies" : "stays one shared colony")} at Start");
            });
            convertScience.RegisterValueChangedCallback(change =>
            {
                if (session == null || session.Setup != setup || starting) return;
                setup.ConvertScience = change.newValue;
            });
            return convertOptions;
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

        // Esc, Cancel or the window's close button: back to the Game Mode page, the Load game box or the game menu (or the
        // game), and everyone in the room is told.
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
            convertOptions?.SetEnabled(false);
            page.SetStatus(new LobbyText(LobbyRules.KeyPrefix + "Status.Starting"));
            LobbySession started = session;
            // K3: a room opened over a game replaces that game with the hosted save: its exit save first, as Exit to menu
            // makes it (not for a game just saved for the room; the main menu has none).
            InGameLobby.Current?.ExitSaveForStart(started.Setup.SavedForRoom);
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

        // Back to what was under the room (the Game Mode page, the Load game box, the game menu, or the game), with the reason.
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
