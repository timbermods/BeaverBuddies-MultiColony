using BeaverBuddies.IO;
using BeaverBuddies.Steam;
using BeaverBuddies.Util;
using Steamworks;
using System;
using System.IO;
using System.Net.Sockets;
using System.Net;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSceneLoading;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WebNavigation;
using TimberNet;
using System.Linq;
using Timberborn.SettlementNameSystem;
using BeaverBuddies.Lobby;

namespace BeaverBuddies.Connect
{
    public class ClientConnectionService : RegisteredSingleton, IUpdatableSingleton
    {
        private GameSceneLoader _gameSceneLoader;
        private GameSaveRepository _gameSaveRepository;
        private DialogBoxShower _dialogBoxShower;
        private UrlOpener _urlOpener;
        private ClientEventIO client;
        private Settings _settings;
        private PanelStack _panelStack;
        private VisualElementLoader _visualElementLoader;
        // "Connecting to …" while a join is under way (see ConnectingBox).
        private ConnectingBox connectingBox;

        // How this player last joined a host, for the desync dialog's reconnect (see Reconnect). Static because it
        // must outlive the scene that joined: the join loads the host's game, which has a new instance of this service.
        private static JoinRoute lastJoin;

        public ClientConnectionService(
            GameSceneLoader gameSceneLoader,
            GameSaveRepository gameSaveRepository,
            DialogBoxShower dialogBoxShower,
            UrlOpener urlOpener,
            Settings settings,
            PanelStack panelStack,
            VisualElementLoader visualElementLoader
        )
        {
            _gameSceneLoader = gameSceneLoader;
            _gameSaveRepository = gameSaveRepository;
            _dialogBoxShower = dialogBoxShower;
            _urlOpener = urlOpener;
            _settings = settings;
            _panelStack = panelStack;
            _visualElementLoader = visualElementLoader;
        }

        public bool TryToConnect(CSteamID friendID)
        {
            // The connection is established in the background by Steam networking; see SteamLinkSocket.
            var socket = SteamNet.Manager?.Connect(friendID.m_SteamID);
            if (socket == null)
            {
                Plugin.LogError("Steam networking is not ready, so the host could not be reached.");
                return false;
            }
            if (!TryToConnect(socket)) return false;
            lastJoin = JoinRoute.ViaSteam(friendID.m_SteamID);
            return true;
        }

        public bool TryToConnect(string address)
        {
            string typed = address;
            int port = _settings.DefaultPort.Value;
            Plugin.Log("Try to resolve address: " + address);
            // Parse address and port
            if (TryParseHostAndPort(address, out string parsedAddress, out int? parsedPort))
            {
                address = parsedAddress;
                Plugin.Log($"Parsed address: {address}, port: {port}");
            }
            else
            {
                // A rejoin's quiet try says nothing: the next try comes (1.4.0-rc5 review, A6).
                if (!quietJoin) ShowError("BeaverBuddies.JoinCoopGame.Error.InvalidFormat");
                return false;
            }

            // Set port if provided
            if (parsedPort.HasValue)
            {
                port = parsedPort.Value;
            }


            // If it's not an IP address, resolve the hostname
            if (!IPAddress.TryParse(address, out _))
            {

                // Resolve the address if it's a hostname
                if (ResolveHostnameIfNecessary(parsedAddress, out string resolvedAddress))
                {
                    address = resolvedAddress;
                }
                else
                {
                    // A rejoin's quiet try says nothing: the next try comes (1.4.0-rc5 review, A6).
                    if (!quietJoin) ShowError("BeaverBuddies.JoinCoopGame.Error.InvalidAddress");
                    return false;
                }
            }

            if (!TryToConnect(new TCPClientWrapper(address, port))) return false;
            lastJoin = JoinRoute.ViaAddress(typed);
            // A rejoin's quiet try waits under its own box (WatchRejoin): no Connecting box for each try.
            if (!quietJoin) ShowConnecting(typed);
            return true;
        }

        private bool TryToConnect(ISocketStream socket)
        {
            Plugin.Log("Connecting client");
            // One join at a time: one still under way ends (a held one is not EventIO, so installing this one would not
            // close it).
            EndJoin(client);
            // Only a waiting room this join enters names the loading screen.
            lobbyHostName = null;
            saveReceived = false;
            // An earlier session's host factions mean nothing to this one (the host's first message brings its own).
            BeaverBuddies.Colonies.ColonySession.ForgetHostFactions();
            bool quiet = quietJoin;
            ClientEventIO joining = null;
            joining = ClientEventIO.Create(socket, bytes => LoadMap(bytes, joining), (error, net) =>
            {
                // Nothing came of this join (ClientEventIO closed it and took it out of EventIO): it is no longer under way.
                if (ReferenceEquals(client, joining)) client = null;
                // A rejoin's try that found no host yet (its page not open): the next try comes, and nothing is said.
                if (quiet && (net == null || !net.Lobby.View().Welcomed))
                {
                    if (!JoinFlowRules.RejoinGivesUp(error))
                    {
                        Plugin.Log("[Lobby] Rejoin: the host is not hosting yet (" + error + ")");
                        return;
                    }
                    // Waiting can't help (another build, a full room): the rejoin stops and says why (1.4.0-rc5 review, C5).
                    Plugin.Log("[Lobby] Rejoin: the host can't take this player (" + error + ")");
                    ShowSafely(() =>
                    {
                        StopRejoin(resetJoin: false);
                        ShowError("BeaverBuddies.JoinCoopGame.Error.CouldNotConnect", error);
                    });
                    return;
                }
                // Only reached while joining, before the host's game has loaded (see ClientEventIO): once a game
                // exists, a lost connection is reported by that game, because this service's dialogs belong to the
                // scene that started the join. In 1.0.4 showing one after the game loaded threw, and the uncaught
                // exception crashed the game.
                ShowSafely(() =>
                {
                    CloseConnectingBox();
                    LobbyGuestPanel page = SingletonManager.GetSingleton<LobbyGuestPanel>();
                    if (net != null && net.Lobby.View().Ended && page != null)
                    {
                        // A host's waiting room ended this join and said why: its page shows that, or, if the page never
                        // opened (the welcome and the end were read in one frame), it is shown from here.
                        page.ShowEndIfUnseen(net);
                        return;
                    }
                    ShowError("BeaverBuddies.JoinCoopGame.Error.CouldNotConnect", error);
                });
            });
            
            client = joining;
            if (client == null)
            {
                Plugin.Log("Client creation failed.");
                return false;
            }

            // In a game the join is held apart from it until its save arrives (LoadMap); the main menu installs it now.
            if (JoinFlowRules.HoldJoin(InGameLobby.InGame))
            {
                client.HeldJoin = true;
                Plugin.Log("[Lobby] Joining from a game: this game stays single-player until the host's save arrives");
                return true;
            }
            EventIO.Set(client);
            return true;
        }

        /// <summary>The join under way (installed in the main menu, held in a game), also once its save has come; or null.</summary>
        public ClientEventIO CurrentJoin => client;

        /// <summary>The join under way whose save has not come yet, still connected; or null (LobbyGuestPanel shows its room).</summary>
        public ClientEventIO JoinUnderWay => client?.NetBase != null && !client.NetBase.IsStopped && !saveReceived ? client : null;

        /// <summary>
        /// Ends a join (the player left the room or cancelled, the host's room ended, another join replaces it, or the player
        /// hosts instead): taken out of EventIO if it is installed, closed if it is held.
        /// </summary>
        public void EndJoin(ClientEventIO join)
        {
            if (join == null) return;
            if (ReferenceEquals(client, join)) client = null;
            join.HeldJoin = false;
            if (ReferenceEquals(EventIO.Get(), join)) EventIO.Reset();
            else join.Close();
        }

        public void ConnectOrShowFailureMessage()
        {
            ConnectOrShowFailureMessage(_settings.ClientConnectionAddress.Value);
        }

        public void ConnectOrShowFailureMessage(string address)
        {
            TryToConnect(address);
        }

        /// <summary>
        /// A guest's "Reconnect (wait for Rehost)" after a desync, and Rejoin after a lost connection: joins the host again
        /// the way it joined before (DesyncDialogPlan.Reconnect), in this scene (1.4.0-rc7; rc4 to rc6 went to the main menu
        /// for it): a box over the paused game waits, trying every few seconds, and the host's room opens as a window over
        /// the game once it welcomes this player (<see cref="WatchRejoin"/>).
        /// </summary>
        public void Reconnect() => StartRejoin(following: false, keptLobby: null);

        /// <summary>From a game whose session ended: <see cref="Reconnect"/>, from wherever this service is found.</summary>
        public static void RejoinFromGame() => SingletonManager.GetSingleton<ClientConnectionService>()?.Reconnect();

        /// <summary>
        /// The host of this player's game moved it to a waiting room and said so (1.4.0-rc7, K1; ClientEventIO): the rejoin
        /// starts at once, trying every second, and the room's window opens over this game when it welcomes this player.
        /// <paramref name="keptLobby"/>: the Steam lobby the host kept for its room, which this player is still a member of.
        /// </summary>
        public static void FollowHost(ulong? keptLobby)
        {
            ClientConnectionService service = SingletonManager.GetSingleton<ClientConnectionService>();
            if (service == null)
            {
                Plugin.LogWarning("[Lobby] The host moved to a waiting room, but nothing here can follow it (Join co-op game joins it)");
                return;
            }
            service.StartRejoin(following: true, keptLobby);
        }

        private void StartRejoin(bool following, ulong? keptLobby)
        {
            Plugin.Log(following ? "[Lobby] Following the host into its waiting room" : "[Lobby] Rejoin: waiting for the host's Co-op Game room");
            rejoinPending = true;
            followingMove = following;
            followLobby = keptLobby;
            rejoinSinceMs = RttTracker.NowMs;
            nextRejoinMs = 0;
        }

        // ---- a rejoin, in the scene the player is in (the main menu since 1.4.0-rc4, a game too since rc7) ----

        // Set by Reconnect, Rejoin and a host's move; this service waits for the host's Co-op Game room and joins it.
        // Static: a rejoin started before a scene change (a game that ended into the main menu) goes on after it.
        private static bool rejoinPending;
        // A host's move (K1) is followed, not just waited for: tried every second at first, and a Steam guest goes straight to
        // the host in the lobby it kept (followLobby).
        private static bool followingMove;
        private static ulong? followLobby;
        private static double rejoinSinceMs;
        private const int ProbeTimeoutMs = 3000;
        private ConnectingBox rejoinBox;
        private double nextRejoinMs;
        // The join under way is one of the rejoin's quiet tries: finding nobody there is not an error.
        private bool quietJoin;
        // A direct rejoin first asks, off the menu's thread, whether anything listens at the host's address; only then does
        // it join (whose connect waits on this thread). Nobody listening costs the menu nothing (1.4.0-rc5 review, A4).
        private System.Threading.Tasks.Task<bool> probe;
        private string probeAddress;
        // Boxes closed while something covered them: taken away once they are on top again (1.4.0-rc5 review, A2).
        private readonly System.Collections.Generic.List<ConnectingBox> closingBoxes = new System.Collections.Generic.List<ConnectingBox>();

        /// <summary>
        /// Every frame, in the main menu or a game: a rejoin waits for the host's room, trying every few seconds, until it is
        /// in; its box sits over the paused game.
        /// </summary>
        private void WatchRejoin()
        {
            if (!rejoinPending) return;
            // Once the scene is up (its guest's room is bound, in the main menu and every game).
            if (SingletonManager.GetSingleton<LobbyGuestPanel>() == null) return;
            TimberClient net = client?.NetBase;
            if (net != null && !net.IsStopped && net.Lobby.View().Welcomed)
            {
                // In the host's room: its page has taken over.
                StopRejoin(resetJoin: false);
                return;
            }
            if (rejoinBox == null || rejoinBox.IsClosed)
            {
                if (!JoinFlowRules.BoxCanShow(InGameLobby.InGame, _panelStack._stack.Count,
                    _panelStack._stack.Count > 0 && _panelStack.TopPanel.IsOverlay)) return;
                // Following the host's move, it says so; a Steam guest can also come in by the host's invite (an invite-only
                // lobby is not shown to friends).
                string waiting = followingMove ? "BeaverBuddies.Rejoin.Following"
                    : lastJoin?.SteamHost != null ? "BeaverBuddies.Rejoin.WaitingSteam" : "BeaverBuddies.Rejoin.Waiting";
                rejoinBox = ConnectingBox.Show(_visualElementLoader, _panelStack, RegisteredLocalizationService.T(waiting),
                    () => StopRejoin(resetJoin: true));
            }
            // A try under way.
            if (net != null && !net.IsStopped) return;
            // The host's address, asked off this thread: joined once something listens there.
            if (probe != null)
            {
                if (!probe.IsCompleted) return;
                bool listening = probe.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && probe.Result;
                string address = probeAddress;
                probe = null;
                if (!listening) return;
                quietJoin = true;
                try { TryToConnect(address); }
                finally { quietJoin = false; }
                return;
            }
            double now = RttTracker.NowMs;
            if (now < nextRejoinMs) return;
            nextRejoinMs = now + JoinFlowRules.RejoinEveryMs(followingMove, now - rejoinSinceMs);
            ReconnectPlan plan = DesyncDialogPlan.Reconnect(lastJoin, _settings.ClientConnectionAddress.Value, FindHostLobby, followLobby);
            switch (plan.Step)
            {
                case ReconnectStep.ConnectInLobby:
                    // Still a member of the lobby the host kept for its room: straight to the host once the lobby says the
                    // room is open (a Steam connection starts in the background; nothing waits on this thread).
                    if (!RejoinLobbyOpen(plan.Lobby) || !(lastJoin?.SteamHost is ulong host)) return;
                    Plugin.Log("[Lobby] Rejoin: the host's room is open in the lobby it kept; connecting to the host");
                    quietJoin = true;
                    try { TryToConnect(new CSteamID(host)); }
                    finally { quietJoin = false; }
                    break;
                case ReconnectStep.JoinSteamLobby:
                    // The host's lobby, entered only once it is a Co-op Game page letting players in: until the host rehosts,
                    // Steam shows the lobby of the game that ended, which refuses everyone (1.4.0-rc5 review, A3).
                    if (!RejoinLobbyOpen(plan.Lobby)) return;
                    Plugin.Log("[Lobby] Rejoin: the host's Co-op Game page is up; joining its Steam lobby");
                    // Entering it joins as accepting an invite does, with its own Connecting box.
                    StopRejoin(resetJoin: false);
                    try { SteamMatchmaking.JoinLobby(new CSteamID(plan.Lobby)); }
                    catch (Exception error) { Plugin.LogWarning("[Lobby] Rejoin: could not join the host's Steam lobby: " + error.Message); }
                    break;
                case ReconnectStep.WaitForSteamInvite:
                    // Not visible (or the host's lobby is invite-only): keep looking; the host's invite joins too.
                    break;
                default:
                    string typed = plan.Address;
                    int port = _settings.DefaultPort.Value;
                    probeAddress = typed;
                    probe = System.Threading.Tasks.Task.Run(() => HostListening(typed, port));
                    break;
            }
        }

        // What the host's Steam lobby says (Steam keeps a lobby's data once asked, as the Join co-op game box asks it).
        private static bool RejoinLobbyOpen(ulong lobby)
        {
            try
            {
                var id = new CSteamID(lobby);
                SteamMatchmaking.RequestLobbyData(id);
                FriendGameState state = FriendGameRules.Classify(SteamMatchmaking.GetLobbyData(id, SteamListener.VersionKey),
                    SteamMatchmaking.GetLobbyData(id, SteamListener.OpenKey), SteamMatchmaking.GetLobbyData(id, SteamListener.RoomKey),
                    Plugin.Version);
                return JoinFlowRules.RejoinEntersLobby(state);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Lobby] Rejoin: could not read the host's Steam lobby: " + error.Message);
                return false;
            }
        }

        /// <summary>
        /// Off the menu's thread: whether anything accepts a connection at the address a guest typed (host, host:port or
        /// [IPv6]:port) within 3 s. Touches nothing of the game; the connection is closed at once.
        /// </summary>
        private static bool HostListening(string typed, int defaultPort)
        {
            try
            {
                if (!TryParseHostAndPort(typed, out string host, out int? port)) return false;
                if (!IPAddress.TryParse(host, out IPAddress ip))
                {
                    IPAddress[] found = Dns.GetHostAddresses(host);
                    if (found.Length == 0) return false;
                    ip = found[0];
                }
                using (var socket = new TcpClient(ip.AddressFamily))
                {
                    return socket.ConnectAsync(ip, port ?? defaultPort).Wait(ProbeTimeoutMs) && socket.Connected;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void StopRejoin(bool resetJoin)
        {
            rejoinPending = false;
            followingMove = false;
            followLobby = null;
            // An address check still under way finishes on its own; its answer is not wanted any more.
            probe = null;
            ConnectingBox box = rejoinBox;
            rejoinBox = null;
            if (box != null && !box.IsClosed) CloseBox(box, "rejoin");
            if (!resetJoin) return;
            Plugin.Log("[Lobby] The player stopped waiting to rejoin");
            EndJoin(client);
        }

        // Takes a Connecting box away: now, or, if something covers it, once it is on top again (polled every frame).
        private void CloseBox(ConnectingBox box, string what)
        {
            try
            {
                box.Close();
                if (!box.IsClosed && !closingBoxes.Contains(box)) closingBoxes.Add(box);
            }
            catch (Exception error) { Plugin.LogWarning($"Could not close the {what} box: " + error.Message); }
        }

        // The Steam lobby the host is in, when Steam shows it to this player: the host's lobby is friends-only when it
        // has "Allow Friends to Join Directly via Steam" on, and then its friends see it; an invite-only lobby is not.
        private static ulong? FindHostLobby(ulong host)
        {
            try
            {
                if (SteamFriends.GetFriendGamePlayed(new CSteamID(host), out FriendGameInfo_t game) && game.m_steamIDLobby.IsValid())
                {
                    return game.m_steamIDLobby.m_SteamID;
                }
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not look up the host's Steam lobby: " + error.Message);
            }
            return null;
        }

        /// <summary>Shows the standard "could not join" dialog with a specific reason.</summary>
        public void ShowJoinError(string reasonKey, string details = null)
        {
            ShowSafely(() =>
            {
                CloseConnectingBox();
                ShowError(reasonKey, details);
            });
        }

        /// <summary>
        /// "Connecting to …" with Cancel, until the host's waiting room opens, its save arrives or an error is shown
        /// (D21). <paramref name="host"/> is the host's name or the address typed; null says only "Connecting…".
        /// </summary>
        public void ShowConnecting(string host)
        {
            ShowSafely(() =>
            {
                CloseConnectingBox();
                // Asked for late (a Steam overlay closing after the room's page opened, or after the join failed): nothing is
                // connecting any more, and a box pushed now would sit over the page, or over nothing, until cancelled.
                TimberClient net = client?.NetBase;
                if (!JoinFlowRules.ShowConnectingBox(net != null && !net.IsStopped && !saveReceived, net?.Lobby.View().Welcomed == true)) return;
                string message = string.IsNullOrEmpty(host)
                    ? RegisteredLocalizationService.T("BeaverBuddies.Lobby.ConnectingAnyone")
                    : RegisteredLocalizationService.T("BeaverBuddies.Lobby.Connecting", host);
                ClientEventIO joining = client;
                connectingBox = ConnectingBox.Show(_visualElementLoader, _panelStack, message, () =>
                {
                    Plugin.Log("The player cancelled joining");
                    EndJoin(joining);
                });
            });
        }

        /// <summary>Takes the Connecting box away (the waiting room opened, or an error is about to be shown).</summary>
        public void CloseConnectingBox()
        {
            // A rejoin's box goes as the room's page comes (WatchRejoin sees it in, and stops).
            if (rejoinBox != null && !rejoinBox.IsClosed) CloseBox(rejoinBox, "rejoin");
            ConnectingBox box = connectingBox;
            if (box == null || box.IsClosed) return;
            CloseBox(box, "Connecting");
        }

        // These are reached from Steam callbacks, which do not care which scene is loaded. A message that
        // cannot be shown is logged; it must never take the game down with it.
        private static void ShowSafely(Action show)
        {
            try { show(); }
            catch (Exception error) { Plugin.LogWarning("Could not show a multiplayer message: " + error.Message); }
        }

        public void ShowConnectionMessage(bool success, string hostName = null)
        {
            ShowSafely(() =>
            {
                if (success)
                {
                    ShowConnecting(hostName);
                }
                else
                {
                    CloseConnectingBox();
                    ShowError("BeaverBuddies.JoinCoopGame.ConnectionFailedMessage");
                }
            });
        }

        private void ShowError(string reasonKey, string details = null)
        {
            string messageKey;
            if (reasonKey != null)
            {
                messageKey = "BeaverBuddies.JoinCoopGame.ConnectionFailedMessageWithError";
            }
            else
            {
                messageKey = "BeaverBuddies.JoinCoopGame.ConnectionFailedMessage";
            }

            ILoc _loc = _dialogBoxShower._loc;
            string reasonMessage = null;
            if (reasonKey != null)
            {
                reasonMessage = _loc.T(reasonKey);
            }

            if (details != null)
            {
                if (reasonMessage != null)
                {
                    reasonMessage += "\n";
                }
                else
                {
                    reasonMessage = "";
                }
                reasonMessage += "\"" + details + "\"";
            }

            var action = () =>
            {
                _urlOpener.OpenUrl(LinkHelper.TroubleshootingUrl);
            };

            string message = _loc.T(messageKey, reasonMessage);
            _dialogBoxShower.Create()
                .SetMessage(message)
                .SetConfirmButton(action)
                .SetDefaultCancelButton()
                .Show();
        }

        private void LoadMap(byte[] mapBytes, ClientEventIO joined)
        {
            // The waiting room is over for this guest from here, whatever happens below (see CheckWaitingRoom).
            saveReceived = true;

            Plugin.Log("Loading map");
            // K3: a join made in a game replaces that game with the host's save: its exit save first, as Exit to menu makes it
            // (not a guest's copy of the host's game; the main menu has none). Before anything of the host's save is set up
            // (its file, the start's random seed) and before the join is installed, so it is saved as the single-player game
            // it still is.
            InGameLobby.Current?.ExitSaveForStart(savedForRoom: false);
            //string saveName = Guid.NewGuid().ToString();
            string saveName = TimberNetBase.GetHashCode(mapBytes).ToString("X8");
            SaveReference saveRef = new SaveReference("Online Games", new SettlementReference(saveName, _gameSaveRepository.DefaultSaveDirectory));
            Stream stream = _gameSaveRepository.CreateSaveSkippingNameValidation(saveRef);
            stream.Write(mapBytes);
            stream.Close();

            // Set the RNG seed before loading the map
            // The server does the same
            DeterminismService.InitGameStartState(mapBytes);
            // From a new game's waiting room, the loading screen says whose game this is. Worded before the reset below,
            // which empties the registry the text comes from (in 1.4.0-beta18 to beta20 it was worded after it and threw,
            // so no waiting-room guest's save ever loaded).
            string waitingRoomHost = lobbyHostName;
            lobbyHostName = null;
            string tip = null;
            if (waitingRoomHost != null)
            {
                try { tip = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Tip.GuestLoading", waitingRoomHost); }
                catch (Exception error) { Plugin.LogWarning("Could not word the loading screen's tip: " + error.Message); }
            }

            // A join held apart from a game becomes the session only now, with this game about to be replaced (1.4.0-rc7);
            // in the main menu it has been EventIO since it connected.
            if (joined != null && joined.HeldJoin)
            {
                joined.HeldJoin = false;
                EventIO.Set(joined);
            }

            // Clean up our current co-op state before loading,
            // so we don't, for example, end up ticking the client before
            // it's actually loaded. Only now, with the load certain: a failure above leaves the menu's singletons (the
            // guest's page, the main menu panel's patch) whole for the join's error dialog.
            SingletonManager.Reset();
            if (tip != null)
                _gameSceneLoader._sceneLoader.LoadScene(GameSceneParameters.CreateGameSaveParameters(saveRef), tip);
            else
                _gameSceneLoader.StartSaveGame(saveRef);
        }

        // LoadMap has been given the host's save (this join's). Checked by CheckWaitingRoom on the same thread.
        private bool saveReceived;

        // The host's name, once a waiting room has welcomed this guest (for the loading screen).
        private static string lobbyHostName;

        public void UpdateSingleton()
        {
            connectingBox?.Poll();
            rejoinBox?.Poll();
            for (int i = closingBoxes.Count - 1; i >= 0; i--)
            {
                closingBoxes[i].Poll();
                if (closingBoxes[i].IsClosed) closingBoxes.RemoveAt(i);
            }
            WatchRejoin();
            if (client == null) return;
            //Plugin.Log("Updating client!");
            client.Update();
            CheckWaitingRoom();
        }

        /// <summary>
        /// A host's waiting room has welcomed this guest: its room shows (LobbyGuestPanel), the main menu's page or, in a
        /// game, a window over it (1.4.0-rc7; rc6 and before left a room joined from a game, D20). Only the host's name is
        /// kept here, for the loading screen.
        /// </summary>
        private void CheckWaitingRoom()
        {
            TimberClient net = client?.NetBase;
            LobbyView view = net?.Lobby.View();
            // Once the save has come the room is over, and LoadMap has emptied the registry (1.4.0-beta18 to beta20 took that
            // for "in a game" and dropped every waiting-room guest as its save loaded).
            WaitingRoomStep step = JoinFlowRules.CheckWaitingRoom(net != null && !net.IsStopped, view?.Welcomed == true,
                saveReceived, InGameLobby.InGame);
            if (step == WaitingRoomStep.Nothing) return;
            lobbyHostName = view.Summary?.HostName;
        }

        /// <summary>
        /// Tries to parse a host:port or [IPv6]:port string.
        /// Supports IPv4, IPv6, and hostnames.
        /// Returns true if parsing succeeded.
        /// </summary>
        public static bool TryParseHostAndPort(
            string input,
            out string host,
            out int? port)
        {
            host = null;
            port = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Uri requires a scheme, so we prepend a dummy one
            var uriString = input.Contains("://") ? input : "tcp://" + input;

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                if (!input.StartsWith("[") && input.Count(c => c == ':') >= 2)
                {
                    Plugin.Log("Attempting to wrap likely IPv6 address");
                    return TryParseHostAndPort($"[{input}]", out host, out port);
                }
                return false;
            }

            // Hostname or IP string (IPv6 brackets stripped)
            host = uri.Host;

            // Port: Uri.Port returns -1 if missing
            if (uri.Port != -1)
                port = uri.Port;

            return true;
        }

        private bool ResolveHostnameIfNecessary(string address, out string resolvedAddress)
        {
            resolvedAddress = null;

            try
            {
                // Otherwise, try to resolve it
                IPHostEntry hostEntry = Dns.GetHostEntry(address);
                if (hostEntry.AddressList.Length > 0)
                {
                    resolvedAddress = hostEntry.AddressList[0].ToString();
                    Plugin.Log(address + " resolved to " + resolvedAddress);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Could not resolve hostname: " + ex.ToString());
            }

            return false;
        }
    }
}
