using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace TimberNet
{
    /// <summary>
    /// The host's word to every guest of its running game that it is moving the game to a waiting room (1.4.0-rc7): a Save
    /// and Rehost, or a save hosted from the Load game box while hosting. A control frame, like the status and chat frames:
    /// never replayed or hashed, read on the guest's receive thread, and only from the host. The guest stops reading at it
    /// and its connection ends as the host closes, which is then no error: the guest's game ends its session quietly and
    /// follows the host into the room (<see cref="TimberNetBase.OnHostMoved"/>). It may say how to reach the room: the
    /// Steam lobby the host keeps for it, which the guest is still a member of.
    /// </summary>
    public static class MoveFrames
    {
        public const string Type = "HostMoving";
        public const string LobbyKey = "lobby";
        // A Steam ID as decimal text (a ulong has at most 20 digits).
        private const int MaxLobbyDigits = 20;

        public static bool IsMoveType(string? type) => type == Type;

        /// <summary>The notice; <paramref name="steamLobby"/> is the Steam lobby the room keeps (null or 0: none).</summary>
        public static JObject Notice(ulong? steamLobby)
        {
            var frame = new JObject { [TimberNetBase.TYPE_KEY] = Type };
            if (steamLobby.HasValue && steamLobby.Value != 0) frame[LobbyKey] = steamLobby.Value.ToString();
            return frame;
        }

        /// <summary>
        /// Reads a notice. False for a frame that is not one, or that carries anything but a well-formed lobby; a notice
        /// without a lobby reads as <paramref name="steamLobby"/> null.
        /// </summary>
        public static bool TryParse(JObject frame, out ulong? steamLobby)
        {
            steamLobby = null;
            if (frame == null || !IsMoveType(TimberNetBase.GetType(frame))) return false;
            JToken? lobby = frame[LobbyKey];
            if (lobby == null) return frame.Count == 1;
            if (frame.Count != 2 || lobby.Type != JTokenType.String) return false;
            string text = (string)lobby!;
            if (text.Length == 0 || text.Length > MaxLobbyDigits || !text.All(char.IsDigit) || !ulong.TryParse(text, out ulong id) || id == 0) return false;
            steamLobby = id;
            return true;
        }
    }
}
