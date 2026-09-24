using Steamworks;
using System;
using System.Collections.Generic;
using TimberNet;

namespace BeaverBuddies.Steam
{
    /// <summary>
    /// Lets Steam friends join a hosted game. The host opens a Steam lobby, which is what the Steam
    /// overlay invites people into, and listens for peer-to-peer connections from lobby members only.
    /// Direct-IP hosting is unaffected: this runs alongside the TCP listener.
    /// </summary>
    public class SteamListener : ISocketListener
    {
        /// <summary>Lobby data: "1" while the host is still accepting players, "0" once the game has started.</summary>
        public const string OpenKey = "bb_open";
        /// <summary>Lobby data (1.4.0-beta24): the host's mod version, so a friend's Join co-op game box can tell whether it can join.</summary>
        public const string VersionKey = "bb_ver";
        /// <summary>Lobby data (1.4.0-beta24): "1" for a waiting room, "0" for a game hosted from a save or a game.</summary>
        public const string RoomKey = "bb_room";
        /// <summary>Lobby data (1.4.0-beta24): what the host is playing, one line (a new game's plate, or a save's settlement).</summary>
        public const string DescriptionKey = "bb_desc";
        /// <summary>Lobby data (1.4.0-beta24): the host's Steam name (a friend who is a guest in the lobby shows the host's game).</summary>
        public const string HostKey = "bb_host";
        private const int DescriptionLimit = 120;

        // What the lobby tells friends' Join co-op game boxes (SetDetails), written when the lobby exists.
        private volatile bool waitingRoom;
        private volatile string description = "";

        public CSteamID LobbyID { get; private set; }

        private readonly List<IDisposable> callbacks = new List<IDisposable>();
        private readonly SteamLinkListener link;
        private bool stopped;

        // The lobby a listener stopped for a move kept for the next one (1.4.0-rc7): the host moving its running game to a
        // waiting room keeps its guests' lobby, so the room's listener reopens it instead of making one. The guests are
        // still members, so IsInLobby lets them straight in, even when the lobby is invite-only. Game thread only.
        private static CSteamID handedOver = CSteamID.Nil;
        private bool keepLobby;

        public SteamListener()
        {
            if (!SteamOverlayConnectionService.IsSteamEnabled)
            {
                throw new Exception("SteamListener created when Steam is not enabled!");
            }
            SteamNet.Initialize();
            link = new SteamLinkListener(SteamNet.Manager, SteamNet.RunOnMain, IsInLobby);
        }

        public void Start()
        {
            Plugin.Log("SteamListener started...");
            try
            {
                link.Start();
                SteamNet.RunOnMain(CreateLobby);
            }
            catch (Exception e)
            {
                // Contained here: the multi-listener would otherwise fail the whole hosting attempt,
                // including direct IP, because of a Steam problem.
                Plugin.LogError("Steam invites are unavailable this session (direct IP still works): " + e.Message);
                link.Stop();
            }
        }

        // Set when the host starts the game, maybe before Steam has made the lobby (OnLobbyCreated reads it).
        private volatile bool closed;

        private void CreateLobby()
        {
            if (stopped) return;
            // A lobby the previous listener kept for this one (a move to a waiting room), if it is still this player's.
            CSteamID kept = handedOver;
            handedOver = CSteamID.Nil;
            if (kept.IsValid())
            {
                if (SteamMatchmaking.GetLobbyOwner(kept) == SteamUser.GetSteamID())
                {
                    Reopen(kept);
                    return;
                }
                Plugin.LogWarning($"The Steam lobby {kept} kept for the waiting room is no longer this player's; making a new one");
                LeaveSafely(kept);
            }
            callbacks.Add(Callback<LobbyCreated_t>.Create(OnLobbyCreated));
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 8);
        }

        // The kept lobby, opened again as this listener's, as OnLobbyCreated opens a new one: its type, whether it takes
        // players, and what friends' Join co-op game boxes read of it (now a waiting room).
        private void Reopen(CSteamID lobby)
        {
            LobbyID = lobby;
            var type = Settings.LobbyJoinable ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypeInvisible;
            SteamMatchmaking.SetLobbyType(LobbyID, type);
            SteamMatchmaking.SetLobbyJoinable(LobbyID, !closed);
            SteamMatchmaking.SetLobbyData(LobbyID, OpenKey, closed ? "0" : "1");
            WriteDetails();
            Plugin.Log($"Steam lobby {LobbyID} kept from the game and reopened ({SteamMatchmaking.GetNumLobbyMembers(LobbyID)} member(s)); " +
                $"joinable by friends={Settings.LobbyJoinable}{(closed ? "; closed, the game has started" : "")}");
        }

        /// <summary>
        /// This listener's lobby goes to the next listener when it stops, instead of being left (the host moving its running
        /// game to a waiting room, 1.4.0-rc7): the guests stay members, and come straight back.
        /// </summary>
        public void KeepLobbyForNextServer() => keepLobby = true;

        /// <summary>
        /// Leaves a lobby kept for a waiting room that never took it (the room could not start, or a server without Steam
        /// started instead). Game thread only.
        /// </summary>
        public static void LeaveHandedOverLobby()
        {
            CSteamID kept = handedOver;
            handedOver = CSteamID.Nil;
            if (!kept.IsValid()) return;
            Plugin.Log($"Leaving the Steam lobby {kept}, kept for a waiting room that did not open");
            LeaveSafely(kept);
        }

        private static void LeaveSafely(CSteamID lobby)
        {
            try { SteamMatchmaking.LeaveLobby(lobby); }
            catch (Exception error) { Plugin.LogWarning("Could not leave the Steam lobby: " + error.Message); }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (stopped) return;
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Plugin.LogError("Failed to create the Steam lobby: " + callback.m_eResult +
                    ". Steam invites will not work this session; direct IP still does.");
                return;
            }
            LobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            // Friends-only lets friends join from Steam directly; invisible means invite-only.
            var type = Settings.LobbyJoinable ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypeInvisible;
            SteamMatchmaking.SetLobbyType(LobbyID, type);
            // Closed already (the host pressed Start before Steam answered): the lobby opens closed, as CloseToNewGuests
            // would have left it. It used to open for friends after the game had started (review of beta20, J11a).
            SteamMatchmaking.SetLobbyData(LobbyID, OpenKey, closed ? "0" : "1");
            if (closed) SteamMatchmaking.SetLobbyJoinable(LobbyID, false);
            WriteDetails();
            Plugin.Log($"Steam lobby created with ID {LobbyID}; joinable by friends={Settings.LobbyJoinable}{(closed ? "; closed, the game has started" : "")}");
        }

        // Only people who joined our lobby (by invite or from the friends list) may connect.
        private bool IsInLobby(ulong steamId)
        {
            CSteamID lobby = LobbyID;
            if (!lobby.IsValid()) return false;
            int members = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int i = 0; i < members; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID == steamId) return true;
            }
            return false;
        }

        public ISocketStream AcceptClient()
        {
            Plugin.Log("Waiting to accept a client...");
            ISocketStream socket = link.AcceptClient();
            Plugin.Log("New Steam client accepted!");
            return socket;
        }

        /// <summary>
        /// What friends see of this game in their Join co-op game box: whether it is a waiting room, and a line saying what
        /// it is. Kept until the lobby exists, and written at once if it does.
        /// </summary>
        public void SetDetails(bool isWaitingRoom, string what)
        {
            waitingRoom = isWaitingRoom;
            string line = (what ?? "").Replace('\n', ' ').Trim();
            description = line.Length > DescriptionLimit ? line.Substring(0, DescriptionLimit) : line;
            SteamNet.RunOnMain(() => { if (!stopped && LobbyID.IsValid()) WriteDetails(); });
        }

        private void WriteDetails()
        {
            SteamMatchmaking.SetLobbyData(LobbyID, VersionKey, Plugin.Version);
            SteamMatchmaking.SetLobbyData(LobbyID, RoomKey, waitingRoom ? "1" : "0");
            SteamMatchmaking.SetLobbyData(LobbyID, DescriptionKey, description ?? "");
            SteamMatchmaking.SetLobbyData(LobbyID, HostKey, SteamFriends.GetPersonaName() ?? "");
        }

        /// <summary>Called when the host starts the game: nobody new can join, so say so in the lobby.</summary>
        public void CloseToNewGuests()
        {
            closed = true;
            SteamNet.RunOnMain(() =>
            {
                if (stopped || !LobbyID.IsValid()) return;
                SteamMatchmaking.SetLobbyJoinable(LobbyID, false);
                SteamMatchmaking.SetLobbyData(LobbyID, OpenKey, "0");
            });
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            Plugin.Log("Stopping SteamListener...");
            link.Stop();
            SteamNet.RunOnMain(() =>
            {
                if (LobbyID.IsValid())
                {
                    if (keepLobby)
                    {
                        // Kept for the waiting room the host is moving to, which opens next (ServerEventIO.MoveToRoom).
                        LeaveHandedOverLobby();
                        handedOver = LobbyID;
                        Plugin.Log($"Steam lobby {LobbyID} kept for the waiting room");
                    }
                    else SteamMatchmaking.LeaveLobby(LobbyID);
                }
                foreach (IDisposable callback in callbacks) callback.Dispose();
                callbacks.Clear();
            });
        }

        public void ShowInviteFriendsPanel()
        {
            if (!LobbyID.IsValid())
            {
                Plugin.LogWarning("The Steam lobby is not ready yet; try Invite Friends again in a moment.");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(LobbyID);
        }
    }
}
