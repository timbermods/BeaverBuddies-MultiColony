using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Connect
{
    /// <summary>What a Steam friend's co-op game is, as the Join co-op game box lists it.</summary>
    public enum FriendGameState
    {
        /// <summary>Steam hasn't sent the lobby's details yet.</summary>
        Looking,
        /// <summary>A waiting room that is still open: join now.</summary>
        WaitingRoom,
        /// <summary>A game hosted from a save or a game, still letting players in (until its first tick).</summary>
        OpenGame,
        /// <summary>The game has started: nobody new can join until the host rehosts.</summary>
        Started,
        /// <summary>Another BeaverBuddies version: players on different builds can't join each other.</summary>
        OtherVersion,
    }

    /// <summary>One friend's co-op game, from their Steam lobby's data (see SteamListener's keys).</summary>
    public sealed class FriendGame
    {
        public FriendGame(ulong friendId, ulong lobbyId, string name, FriendGameState state, string description, string version)
        {
            FriendId = friendId;
            LobbyId = lobbyId;
            Name = name ?? "";
            State = state;
            Description = description ?? "";
            Version = version ?? "";
        }

        public ulong FriendId { get; }
        public ulong LobbyId { get; }
        public string Name { get; }
        public FriendGameState State { get; }
        /// <summary>What the host is playing: a new game's "Faction - map - difficulty", or a save's settlement.</summary>
        public string Description { get; }
        public string Version { get; }
        public bool Joinable => FriendGameRules.Joinable(State);
    }

    /// <summary>
    /// The Join co-op game box's decisions, apart from Steam and the UI so that StabilityTests can check them (System only).
    /// </summary>
    public static class FriendGameRules
    {
        /// <summary>
        /// A friend's lobby from its data: <paramref name="version"/> is the host's mod version (bb_ver, since 1.4.0-beta24),
        /// <paramref name="open"/> bb_open ("1" while players may join), <paramref name="room"/> bb_room ("1" for a waiting
        /// room). Empty data means Steam hasn't answered yet; a lobby with an open flag but no version is a host from before
        /// beta24, which can't be joined from this build either.
        /// </summary>
        public static FriendGameState Classify(string version, string open, string room, string ourVersion)
        {
            if (string.IsNullOrEmpty(version) && string.IsNullOrEmpty(open)) return FriendGameState.Looking;
            if (version != ourVersion) return FriendGameState.OtherVersion;
            if (open != "1") return FriendGameState.Started;
            return room == "1" ? FriendGameState.WaitingRoom : FriendGameState.OpenGame;
        }

        public static bool Joinable(FriendGameState state) =>
            state == FriendGameState.WaitingRoom || state == FriendGameState.OpenGame;

        /// <summary>The list's order: games you can join first, then by name (so rows don't jump as Steam answers).</summary>
        public static List<FriendGame> Order(IEnumerable<FriendGame> games) =>
            (games ?? Enumerable.Empty<FriendGame>())
                .OrderBy(g => g.Joinable ? 0 : 1)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.FriendId)
                .ToList();

        /// <summary>The row to keep selected after a refresh: the same friend's, if it is still there and joinable.</summary>
        public static int SelectionAfterRefresh(IReadOnlyList<FriendGame> games, ulong? selectedFriend)
        {
            if (selectedFriend == null) return -1;
            for (int i = 0; i < games.Count; i++)
                if (games[i].FriendId == selectedFriend && games[i].Joinable) return i;
            return -1;
        }
    }
}
