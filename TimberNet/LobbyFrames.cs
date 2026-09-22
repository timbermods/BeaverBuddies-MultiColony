using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TimberNet
{
    /// <summary>Where a waiting room is: open for players, then the host's world being made and sent.</summary>
    public enum LobbyStage
    {
        Open,
        Starting,
        CreatingWorld,
        SendingWorld,
    }

    /// <summary>Why the host ended a guest's time in the waiting room.</summary>
    public enum LobbyEndReason
    {
        Cancelled,
        Removed,
        Failed,
    }

    /// <summary>The new game a waiting room is for, as the host chose it. Display only.</summary>
    public sealed class LobbySummary
    {
        public string FactionId { get; }
        public string MapName { get; }
        /// <summary>The difficulty's loc key; null for a custom one (each player shows the game's own "Custom").</summary>
        public string? ModeLocKey { get; }
        public string Settlement { get; }
        public string HostName { get; }
        public bool SeparateColonies { get; }

        public LobbySummary(string factionId, string mapName, string? modeLocKey, string settlement, string hostName, bool separateColonies)
        {
            FactionId = LobbyFrames.Clip(factionId, 64);
            MapName = LobbyFrames.Clip(mapName, 128);
            ModeLocKey = modeLocKey == null ? null : LobbyFrames.Clip(modeLocKey, 128);
            Settlement = LobbyFrames.Clip(settlement, 64);
            HostName = PlayerActivity.CleanName(hostName);
            SeparateColonies = separateColonies;
        }

        public JObject ToJson() => new JObject
        {
            ["faction"] = FactionId,
            ["map"] = MapName,
            ["mode"] = ModeLocKey,
            ["settlement"] = Settlement,
            ["host"] = HostName,
            ["separate"] = SeparateColonies,
        };

        public static bool TryParse(JToken? token, out LobbySummary? summary)
        {
            summary = null;
            if (!(token is JObject json)) return false;
            if (!LobbyFrames.TryString(json["faction"], out string faction) || !LobbyFrames.TryString(json["map"], out string map)
                || !LobbyFrames.TryString(json["settlement"], out string settlement) || !LobbyFrames.TryString(json["host"], out string host)
                || !(json["separate"] is JValue { Type: JTokenType.Boolean } separate))
                return false;
            string? mode = json["mode"] is JValue { Type: JTokenType.String } modeValue ? (string?)modeValue : null;
            summary = new LobbySummary(faction, map, mode, settlement, host, (bool)separate);
            return true;
        }
    }

    /// <summary>One row of a waiting room, as every player sees it.</summary>
    public sealed class LobbyPlayer
    {
        /// <summary>The player number the host gave (0 is the host).</summary>
        public int Number { get; }
        public string Name { get; }
        public bool Ready { get; }
        public bool IsHost { get; }
        /// <summary>Connected, but has not said who it is yet.</summary>
        public bool Joining { get; }
        /// <summary>The colony it will play, from 1; 0 for a helper; null in a shared game.</summary>
        public int? Colony { get; }

        public LobbyPlayer(int number, string name, bool ready, bool isHost, bool joining, int? colony)
        {
            Number = number;
            Name = PlayerActivity.CleanName(name);
            Ready = ready;
            IsHost = isHost;
            Joining = joining;
            Colony = colony;
        }

        public JObject ToJson() => new JObject
        {
            ["n"] = Number,
            ["name"] = Name,
            ["ready"] = Ready,
            ["host"] = IsHost,
            ["joining"] = Joining,
            ["colony"] = Colony,
        };

        public static bool TryParse(JToken? token, out LobbyPlayer? player)
        {
            player = null;
            if (!(token is JObject json)) return false;
            if (!(json["n"] is JValue { Type: JTokenType.Integer } number) || !LobbyFrames.TryString(json["name"], out string name)
                || !(json["ready"] is JValue { Type: JTokenType.Boolean } ready) || !(json["host"] is JValue { Type: JTokenType.Boolean } host)
                || !(json["joining"] is JValue { Type: JTokenType.Boolean } joining))
                return false;
            int? colony = null;
            if (json["colony"] is JValue { Type: JTokenType.Integer } colonyValue)
            {
                int value = (int)colonyValue;
                if (value < 0 || value > LobbyRoom.MaxColonies) return false;
                colony = value;
            }
            int n = (int)number;
            if (n < 0) return false;
            player = new LobbyPlayer(n, name, (bool)ready, (bool)host, (bool)joining, colony);
            return true;
        }
    }

    /// <summary>
    /// The waiting room's messages. They flow only between a host that opened a waiting room and a guest in it, before
    /// the guest has the save: hosting a save sends none, so its bytes on the wire are what they always were.
    /// <para>
    /// Host → guest: <c>[int32 -1][int32 length][gzip(JSON)]</c>. The -1 marks a waiting-room frame: 0 already means
    /// "an error follows", and a save is never negative. The guest reads one anywhere; after its save it drops it (a
    /// keep-alive can race the save). Guest → host: an ordinary <c>[length][gzip(JSON)]</c> frame of type
    /// <see cref="HelloType"/> or <see cref="ReadyType"/>; the host reads nothing else from a guest still waiting.
    /// </para>
    /// Everything here is display only, and validated like chat and activity: nothing a peer sends can reach the game.
    /// </summary>
    public static class LobbyFrames
    {
        public const int Sentinel = -1;
        /// <summary>The longest waiting-room frame either side accepts, compressed and decompressed.</summary>
        public const int MaxFrameBytes = 64 * 1024;
        public const int MaxIdLength = 64;
        public const int MaxRosterPlayers = 16;

        public const string WelcomeType = "LobbyWelcome";
        public const string RosterType = "LobbyRoster";
        public const string StateType = "LobbyState";
        public const string EndType = "LobbyEnd";
        public const string HelloType = "LobbyHello";
        public const string ReadyType = "LobbyReady";

        public static bool IsLobbyType(string? type) =>
            type == WelcomeType || type == RosterType || type == StateType || type == EndType || type == HelloType || type == ReadyType;

        /// <summary>The two frames a guest sends.</summary>
        public static bool IsGuestType(string? type) => type == HelloType || type == ReadyType;

        // ---- Host → guest ----

        public static JObject Welcome(int you, LobbySummary summary) =>
            new JObject { [TimberNetBase.TYPE_KEY] = WelcomeType, ["you"] = you, ["summary"] = summary.ToJson() };

        public static bool TryParseWelcome(JObject frame, out int you, out LobbySummary? summary)
        {
            summary = null;
            you = -1;
            if (!(frame["you"] is JValue { Type: JTokenType.Integer } number) || (int)number < 1) return false;
            you = (int)number;
            return LobbySummary.TryParse(frame["summary"], out summary);
        }

        public static JObject Roster(IEnumerable<LobbyPlayer> players) =>
            new JObject { [TimberNetBase.TYPE_KEY] = RosterType, ["players"] = new JArray(players.Select(p => p.ToJson())) };

        public static bool TryParseRoster(JObject frame, out List<LobbyPlayer> players)
        {
            players = new List<LobbyPlayer>();
            if (!(frame["players"] is JArray array) || array.Count > MaxRosterPlayers) return false;
            foreach (JToken token in array)
            {
                if (!LobbyPlayer.TryParse(token, out LobbyPlayer? player) || player == null) return false;
                players.Add(player);
            }
            return true;
        }

        public static JObject State(int sequence, LobbyStage stage) =>
            new JObject { [TimberNetBase.TYPE_KEY] = StateType, ["seq"] = sequence, ["state"] = StageName(stage) };

        public static bool TryParseState(JObject frame, out int sequence, out LobbyStage stage)
        {
            sequence = 0;
            stage = LobbyStage.Open;
            if (!(frame["seq"] is JValue { Type: JTokenType.Integer } seq) || !TryString(frame["state"], out string name)) return false;
            sequence = (int)seq;
            return TryStage(name, out stage);
        }

        public static JObject End(LobbyEndReason reason, string? detail) =>
            new JObject { [TimberNetBase.TYPE_KEY] = EndType, ["reason"] = ReasonName(reason), ["detail"] = detail == null ? null : Clip(detail, 500) };

        public static bool TryParseEnd(JObject frame, out LobbyEndReason reason, out string? detail)
        {
            reason = LobbyEndReason.Cancelled;
            detail = frame["detail"] is JValue { Type: JTokenType.String } text ? Clip((string)text!, 500) : null;
            if (!TryString(frame["reason"], out string name)) return false;
            switch (name)
            {
                case "cancelled": reason = LobbyEndReason.Cancelled; return true;
                case "removed": reason = LobbyEndReason.Removed; return true;
                case "failed": reason = LobbyEndReason.Failed; return true;
                default: return false;
            }
        }

        // ---- Guest → host ----

        public static JObject Hello(string id, string name) =>
            new JObject { [TimberNetBase.TYPE_KEY] = HelloType, ["id"] = id, ["name"] = PlayerActivity.CleanName(name) };

        /// <summary>A guest's hello: its stable id (as it will say hello in the game) and its name, cleaned.</summary>
        public static bool TryParseHello(JObject frame, out string id, out string name)
        {
            name = "";
            if (!TryString(frame["id"], out id) || !IsWellFormedId(id)) return false;
            name = PlayerActivity.CleanName(frame["name"] is JValue { Type: JTokenType.String } text ? (string?)text : null);
            return true;
        }

        public static JObject Ready(bool ready) => new JObject { [TimberNetBase.TYPE_KEY] = ReadyType, ["ready"] = ready };

        public static bool TryParseReady(JObject frame, out bool ready)
        {
            ready = false;
            if (!(frame["ready"] is JValue { Type: JTokenType.Boolean } value)) return false;
            ready = (bool)value;
            return true;
        }

        /// <summary>The same rule as the colony slot table: short, one line, no field separator.</summary>
        public static bool IsWellFormedId(string? id) =>
            !string.IsNullOrEmpty(id) && id!.Length <= MaxIdLength && id.IndexOfAny(new[] { '|', '\n', '\r' }) < 0;

        public static string StageName(LobbyStage stage)
        {
            switch (stage)
            {
                case LobbyStage.Starting: return "starting";
                case LobbyStage.CreatingWorld: return "creatingWorld";
                case LobbyStage.SendingWorld: return "sendingWorld";
                default: return "open";
            }
        }

        static bool TryStage(string name, out LobbyStage stage)
        {
            switch (name)
            {
                case "open": stage = LobbyStage.Open; return true;
                case "starting": stage = LobbyStage.Starting; return true;
                case "creatingWorld": stage = LobbyStage.CreatingWorld; return true;
                case "sendingWorld": stage = LobbyStage.SendingWorld; return true;
                default: stage = LobbyStage.Open; return false;
            }
        }

        static string ReasonName(LobbyEndReason reason) =>
            reason == LobbyEndReason.Removed ? "removed" : reason == LobbyEndReason.Failed ? "failed" : "cancelled";

        internal static bool TryString(JToken? token, out string value)
        {
            value = "";
            if (!(token is JValue { Type: JTokenType.String } text)) return false;
            value = (string?)text ?? "";
            return true;
        }

        internal static string Clip(string? text, int max)
        {
            string clean = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray());
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }
    }
}
