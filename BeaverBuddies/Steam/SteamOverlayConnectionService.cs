#if IS_STEAM
using Timberborn.SteamStoreSystem;
#endif

using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.SingletonSystem;
using UnityEngine;
using Timberborn.SteamOverlaySystem;
using Timberborn.CoreUI;

namespace BeaverBuddies.Steam
{
    class SteamOverlayConnectionService : IUpdatableSingleton
    {
        public static bool IsSteamEnabled { get; private set; } = false;

#if IS_STEAM
        private SteamManager _steamManager;
        private ClientConnectionService _clientConnectionService;
        private PanelStack _panelStack;
        private SteamOverlayInputBlocker _inputBlocker;
        private EventBus _eventBus;
        private Settings _settings;
        private DialogBoxShower _dialogBoxShower;
        private Timberborn.MainMenuSceneLoading.MainMenuSceneLoader _mainMenuSceneLoader;

        private bool? _lastSuccess = null;
        private string _lastHostName = null;

        private static List<IDisposable> callbacks = new List<IDisposable>();

        public SteamOverlayConnectionService(
            SteamManager steamManager,
            ClientConnectionService clientConnectionService,
            SteamOverlayInputBlocker steamOverlayInputBlocker,
            PanelStack panelStack,
            EventBus eventBus,
            Settings settings,
            DialogBoxShower dialogBoxShower,
            Timberborn.MainMenuSceneLoading.MainMenuSceneLoader mainMenuSceneLoader
            )
        {
            _dialogBoxShower = dialogBoxShower;
            _mainMenuSceneLoader = mainMenuSceneLoader;
            _steamManager = steamManager;
            _clientConnectionService = clientConnectionService;
            _panelStack = panelStack;
            _inputBlocker = steamOverlayInputBlocker;
            _eventBus = eventBus;
            _settings = settings;
        }

        bool done = false;
        public void UpdateSingleton()
        {
            //Read();
            // Runs whether or not Steam has finished starting: it only does anything for a blocker the overlay left
            // behind under a dialog.
            try { SteamOverlayInputBlockerPatch.ReleaseSurfacedBlocker(_inputBlocker); }
            catch (Exception e) { Plugin.LogWarning("Could not release the Steam overlay's input blocker: " + e.Message); }
            if (!done)
            {
                if (_steamManager.Initialized)
                {
                    IsSteamEnabled = true;

                    foreach (var callback in callbacks)
                    {
                        callback.Dispose();
                    }
                    // Disposed, they still hold the last scene's service (and through it that scene's panels and event
                    // bus): let them go, or every main menu and game loaded in a run stays in memory (1.4.0-rc1 review, D-S7).
                    callbacks.Clear();

                    done = true;

                    //Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                    //Callback<LobbyInvite_t>.Create(OnLobbyInvite);
                    //Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
                    callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested));
                    callbacks.Add(Callback<LobbyEnter_t>.Create(OnLobbyEntered));

                    // Start Steam networking (relay warm-up and the per-frame pump).
                    try { SteamNet.Initialize(); }
                    catch (Exception e) { Plugin.LogError("Steam networking could not start; Steam invites are unavailable: " + e.Message); }

                    TrySetPingName();
                    TryJoinLaunchLobby();
                }
                else
                {
                    Plugin.Log("Waiting on Steamworks to initialize...");
                }
            }
            if (done) JoinPendingInvite();
            //ReceiveMessages();
        }

        private void TrySetPingName()
        {
            if (Settings.PingDisplayName != Settings.DefaultPingPlayerName) return;
            try
            {
                string steamName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
                if (!string.IsNullOrEmpty(steamName))
                {
                    _settings.PingPlayerName.SetValue(steamName);
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            string name = SteamFriends.GetFriendPersonaName(callback.m_steamIDFriend);
            Debug.Log("User " + name + " has requested to join the lobby; joining...");
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyInvite(LobbyInvite_t param)
        {
            string invitingUser = SteamFriends.GetFriendPersonaName(new CSteamID(param.m_ulSteamIDUser));
            Plugin.Log($"Invited to lobby {param.m_ulSteamIDLobby} by {invitingUser}");
        }

        private static bool launchLobbyHandled;

        // If the invited friend did not have the game running, Steam launches it with "+connect_lobby <id>".
        private void TryJoinLaunchLobby()
        {
            if (launchLobbyHandled) return;
            launchLobbyHandled = true;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobbyId) && lobbyId != 0)
                    {
                        Plugin.Log($"Launched from a Steam invite; joining lobby {lobbyId}...");
                        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogWarning("Could not read the Steam launch invite: " + e.Message);
            }
        }

        // This should only be called if the SteamOverlayInputBlocker is on top,
        // so we can assume it was the panel that was just removed.
        // If the game starts, it clears the stack silently, so this isn't shown
        // (also this Singleton is disposed when the current scene ends).
        [OnEvent]
        public void OnPanelHidden(PanelHiddenEvent panelHiddenEvent)
        {
            if (!_lastSuccess.HasValue) return;
            _clientConnectionService.ShowConnectionMessage(_lastSuccess.Value, _lastHostName);
            ClearWaitForSteamOverlay();
        }

        private void WaitForSteamOverlayToClose(bool success)
        {
            _lastSuccess = success;
            _eventBus.Register(this);
        }

        private void ClearWaitForSteamOverlay()
        {
            _lastSuccess = null;
            _eventBus.Unregister(this);

        }


        // The last host's lobby this guest entered (static: it outlives the scene that joined).
        private static CSteamID enteredLobby;

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            ClearWaitForSteamOverlay();
            var lobby = new CSteamID(callback.m_ulSteamIDLobby);
            var owner = SteamMatchmaking.GetLobbyOwner(lobby);
            // This player's own lobby, as a host.
            if (owner == SteamUser.GetSteamID()) return;
            if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Plugin.LogWarning($"Could not enter the host's Steam lobby (response {callback.m_EChatRoomEnterResponse}).");
                _clientConnectionService.ShowConnectionMessage(false);
                return;
            }
            JoinHostLobby(lobby, owner);
        }

        /// <summary>
        /// A host's lobby this player is in (an invite, the Join box, a rejoin): its Co-op Game page is joined from the main
        /// menu. From a game played alone the player is asked, and the game is saved and left for the main menu, where the
        /// join goes on by itself (1.4.0-rc6). A co-op game, or a page this player hosts, is never ended or replaced by it.
        /// </summary>
        private void JoinHostLobby(CSteamID lobby, CSteamID owner)
        {
            if (SteamMatchmaking.GetLobbyData(lobby, SteamListener.OpenKey) == "0")
            {
                // An old invite: the host already started, so nobody can join until they rehost.
                Plugin.Log("The host has already started the game; not connecting.");
                // Not the lobby of the co-op game this player is in: that one is theirs to keep.
                if (lobby != enteredLobby || EventIO.IsNull) SteamMatchmaking.LeaveLobby(lobby);
                _clientConnectionService.ShowJoinError("BeaverBuddies.JoinCoopGame.Error.HostStarted");
                return;
            }
            string host;
            try { host = SteamFriends.GetFriendPersonaName(owner); }
            catch (Exception) { host = null; }
            bool inMainMenu = SingletonManager.GetSingleton<BeaverBuddies.Lobby.LobbyGuestPanel>() != null;
            switch (InviteRules.Decide(inMainMenu, inCoopSession: !EventIO.IsNull, hostingPage: BeaverBuddies.Lobby.LobbySession.Current != null))
            {
                case InviteStep.OfferFromGame:
                    OfferJoinFromGame(lobby, host);
                    return;
                case InviteStep.LeaveCoopGameFirst:
                    // The lobby of the game this player is in, entered again: nothing to do.
                    if (lobby == enteredLobby) return;
                    TellWhyNot(lobby, "BeaverBuddies.Invite.InCoopGame", host);
                    return;
                case InviteStep.StopHostingFirst:
                    TellWhyNot(lobby, "BeaverBuddies.Invite.WhileHosting", host);
                    return;
            }
            // One host's lobby at a time: the lobby of a game that ended is left as the next is entered, so this guest
            // never stays in it, or inherits it when its host leaves (1.4.0-rc5 review, A3).
            if (enteredLobby.IsValid() && enteredLobby != lobby)
            {
                try { SteamMatchmaking.LeaveLobby(enteredLobby); }
                catch (Exception error) { Plugin.LogWarning("Could not leave the previous Steam lobby: " + error.Message); }
            }
            enteredLobby = lobby;
            Plugin.Log("Joining another's lobby...");
            bool success = _clientConnectionService.TryToConnect(owner);
            _lastHostName = host;
            if (_panelStack.IsPanelOnTop(_inputBlocker))
            {
                WaitForSteamOverlayToClose(success);
            }
            else
            {
                _clientConnectionService.ShowConnectionMessage(success, _lastHostName);
            }
        }

        // ---- an invite accepted in a game (1.4.0-rc6) ----

        // The lobby of an invite accepted in a game played alone, joined once the main menu is up (0: none). Static: it
        // crosses the scene change. The player stays in the lobby meanwhile, so its host lets the connection in.
        private static ulong pendingInviteLobby;
        private int framesInMenu;

        private void OfferJoinFromGame(CSteamID lobby, string host)
        {
            try
            {
                _dialogBoxShower.Create()
                    .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Invite.FromGame", host ?? ""))
                    .SetConfirmButton(() =>
                    {
                        Plugin.Log($"[Join] Saving this game and going to the main menu to join {host}'s Co-op Game page");
                        pendingInviteLobby = lobby.m_SteamID;
                        // The game's own way out: its exit save, then the main menu.
                        _mainMenuSceneLoader.SaveAndOpenMainMenu();
                    }, RegisteredLocalizationService.T("BeaverBuddies.Invite.SaveAndJoin"))
                    .SetCancelButton(() => LeaveSafely(lobby), RegisteredLocalizationService.T("BeaverBuddies.Rejoin.Stay"))
                    .Show();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not offer to join from the main menu: " + error.Message);
                LeaveSafely(lobby);
            }
        }

        private void TellWhyNot(CSteamID lobby, string key, string host)
        {
            Plugin.Log($"[Join] Not joining {host}'s Co-op Game page now ({key})");
            LeaveSafely(lobby);
            try { _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T(key, host ?? "")).Show(); }
            catch (Exception error) { Plugin.LogWarning("Could not say why the invite waits: " + error.Message); }
        }

        private static void LeaveSafely(CSteamID lobby)
        {
            try { SteamMatchmaking.LeaveLobby(lobby); }
            catch (Exception error) { Plugin.LogWarning("Could not leave the invite's Steam lobby: " + error.Message); }
        }

        // In the main menu, once it is up (not under its changelog or first-timer box): the invite's host is joined.
        private void JoinPendingInvite()
        {
            if (pendingInviteLobby == 0 || SingletonManager.GetSingleton<BeaverBuddies.Lobby.LobbyGuestPanel>() == null) return;
            if (framesInMenu++ < 2 || _panelStack._stack.Count == 0 || _panelStack.TopPanel.IsOverlay) return;
            var lobby = new CSteamID(pendingInviteLobby);
            pendingInviteLobby = 0;
            CSteamID owner;
            try { owner = SteamMatchmaking.GetLobbyOwner(lobby); }
            catch (Exception) { owner = CSteamID.Nil; }
            if (!owner.IsValid() || owner == SteamUser.GetSteamID())
            {
                // The host left meanwhile.
                Plugin.LogWarning("[Join] The invite's host is no longer in its lobby");
                LeaveSafely(lobby);
                _clientConnectionService.ShowConnectionMessage(false);
                return;
            }
            Plugin.Log("[Join] Joining the invite accepted in a game");
            JoinHostLobby(lobby, owner);
        }
#else
        // Need to implement the interface
        public void UpdateSingleton() { }
#endif
    }
}
