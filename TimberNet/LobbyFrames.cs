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

    /// <summary>
    /// The game a waiting room is for, as the host chose it: a new game (its faction, map and difficulty) or a save (its
    /// name and in-game date; <see cref="IsSave"/>). Display only.
    /// </summary>
    public sealed class LobbySummary
    {
        public string FactionId { get; }
        public string MapName { get; }
        /// <summary>The difficulty's loc key; null for a custom one (each player shows the game's own "Custom").</summary>
        public string? ModeLocKey { get; }
        public string Settlement { get; }
        public string HostName { get; }
        /// <summary>Rows show the colony each player will play (a new game with separate colonies; a save seats by who it remembers).</summary>
        public bool SeparateColonies { get; }
        /// <summary>A hosted save's name; null for a new game.</summary>
        public string? SaveName { get; }
        /// <summary>A hosted save's in-game date, for each player's game to word.</summary>
        public int Cycle { get; }
        public int Day { get; }
        public bool IsSave => SaveName != null;
        /// <summary>
        /// Each colony plays a faction of its own (a mixed-factions game): each row shows its player's faction, and a
        /// player who may pick one does so in the room. False for any other game, whose summary says nothing of it.
        /// </summary>
        public bool Mixed { get; }
        /// <summary>The factions a player may pick, in the game's order (the host's unlocks). Empty unless mixed.</summary>
        public IReadOnlyList<string> Factions { get; }

        public LobbySummary(string factionId, string mapName, string? modeLocKey, string settlement, string hostName, bool separateColonies)
            : this(factionId, mapName, modeLocKey, settlement, hostName, separateColonies, null, 0, 0, false, null) { }

        /// <summary>A new game, mixed or not.</summary>
        public LobbySummary(string factionId, string mapName, string? modeLocKey, string settlement, string hostName, bool separateColonies,
            bool mixed, IEnumerable<string>? factions)
            : this(factionId, mapName, modeLocKey, settlement, hostName, separateColonies, null, 0, 0, mixed, factions) { }

        /// <summary>A hosted save: its settlement, name and in-game date.</summary>
        public static LobbySummary ForSave(string settlement, string saveName, int cycle, int day, string hostName) =>
            new LobbySummary("", "", null, settlement, hostName, false, saveName ?? "", cycle, day, false, null);

        /// <summary>
        /// A hosted save whose own data was read (the menu read its world's singletons): its base faction, whether it has
        /// separate colonies (rows then show the colony each player will play), and whether it is mixed.
        /// </summary>
        public static LobbySummary ForSave(string settlement, string saveName, int cycle, int day, string hostName,
            string factionId, bool separateColonies, bool mixed, IEnumerable<string>? factions) =>
            new LobbySummary(factionId ?? "", "", null, settlement, hostName, separateColonies, saveName ?? "", cycle, day, mixed, factions);

        private LobbySummary(string factionId, string mapName, string? modeLocKey, string settlement, string hostName,
            bool separateColonies, string? saveName, int cycle, int day, bool mixed, IEnumerable<string>? factions)
        {
            FactionId = LobbyFrames.Clip(factionId, 64);
            MapName = LobbyFrames.Clip(mapName, 128);
            ModeLocKey = modeLocKey == null ? null : LobbyFrames.Clip(modeLocKey, 128);
            Settlement = LobbyFrames.Clip(settlement, 64);
            HostName = PlayerActivity.CleanName(hostName);
            SeparateColonies = separateColonies;
            SaveName = saveName == null ? null : LobbyFrames.Clip(saveName, 128);
            Cycle = Math.Max(0, cycle);
            Day = Math.Max(0, day);
            Factions = mixed
                ? (factions ?? Enumerable.Empty<string>()).Where(LobbyFrames.IsWellFormedFactionId).Distinct()
                    .Take(LobbyFrames.MaxFactions).ToList()
                : new List<string>();
            Mixed = mixed && Factions.Count > 0;
        }

        public JObject ToJson()
        {
            var json = new JObject
            {
                ["faction"] = FactionId,
                ["map"] = MapName,
                ["mode"] = ModeLocKey,
                ["settlement"] = Settlement,
                ["host"] = HostName,
                ["separate"] = SeparateColonies,
                ["save"] = SaveName == null ? null : new JObject { ["name"] = SaveName, ["cycle"] = Cycle, ["day"] = Day },
            };
            // Only a mixed game says anything of factions, so any other summary is what 1.4.0-beta19 sent.
            if (Mixed)
            {
                json["mixed"] = true;
                json["factions"] = new JArray(Factions);
            }
            return json;
        }

        public static bool TryParse(JToken? token, out LobbySummary? summary)
        {
            summary = null;
            if (!(token is JObject json)) return false;
            if (!LobbyFrames.TryString(json["faction"], out string faction) || !LobbyFrames.TryString(json["map"], out string map)
                || !LobbyFrames.TryString(json["settlement"], out string settlement) || !LobbyFrames.TryString(json["host"], out string host)
                || !(json["separate"] is JValue { Type: JTokenType.Boolean } separate))
                return false;
            string? mode = json["mode"] is JValue { Type: JTokenType.String } modeValue ? (string?)modeValue : null;
            string? saveName = null;
            int cycle = 0, day = 0;
            if (json["save"] is JObject save)
            {
                if (!LobbyFrames.TryString(save["name"], out string name)) return false;
                saveName = name;
                cycle = save["cycle"] is JValue { Type: JTokenType.Integer } c ? (int)c : 0;
                day = save["day"] is JValue { Type: JTokenType.Integer } d ? (int)d : 0;
            }
            bool mixed = false;
            List<string>? factions = null;
            if (json["mixed"] is JValue { Type: JTokenType.Boolean } mixedValue && (bool)mixedValue)
            {
                if (!(json["factions"] is JArray array) || array.Count == 0 || array.Count > LobbyFrames.MaxFactions) return false;
                factions = new List<string>();
                foreach (JToken token2 in array)
                {
                    if (!LobbyFrames.TryString(token2, out string id) || !LobbyFrames.IsWellFormedFactionId(id)) return false;
                    factions.Add(id);
                }
                mixed = true;
            }
            summary = new LobbySummary(faction, map, mode, settlement, host, (bool)separate, saveName, cycle, day, mixed, factions);
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
        /// <summary>The faction its colony plays (the room's rows show its logo); null when the room names none.</summary>
        public string? Faction { get; }
        /// <summary>It may pick its faction here (a mixed new game, or a mixed save's player who has no colony yet).</summary>
        public bool MayPick { get; }

        public LobbyPlayer(int number, string name, bool ready, bool isHost, bool joining, int? colony, string? faction = null,
            bool mayPick = false)
        {
            Number = number;
            Name = PlayerActivity.CleanName(name);
            Ready = ready;
            IsHost = isHost;
            Joining = joining;
            Colony = colony;
            Faction = LobbyFrames.IsWellFormedFactionId(faction) ? faction : null;
            MayPick = mayPick;
        }

        public JObject ToJson()
        {
            var json = new JObject
            {
                ["n"] = Number,
                ["name"] = Name,
                ["ready"] = Ready,
                ["host"] = IsHost,
                ["joining"] = Joining,
                ["colony"] = Colony,
            };
            // Only rows that name a faction say so, so a room that names none sends what 1.4.0-beta19 sent.
            if (Faction != null) json["faction"] = Faction;
            if (MayPick) json["pick"] = true;
            return json;
        }

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
            string? faction = null;
            if (json["faction"] != null && json["faction"]!.Type != JTokenType.Null)
            {
                if (!LobbyFrames.TryString(json["faction"], out string id) || !LobbyFrames.IsWellFormedFactionId(id)) return false;
                faction = id;
            }
            bool mayPick = json["pick"] is JValue { Type: JTokenType.Boolean } pick && (bool)pick;
            player = new LobbyPlayer(n, name, (bool)ready, (bool)host, (bool)joining, colony, faction, mayPick);
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
        /// <summary>The most factions a mixed room offers (the game has two; mods may add a few).</summary>
        public const int MaxFactions = 8;

        public const string WelcomeType = "LobbyWelcome";
        public const string RosterType = "LobbyRoster";
        public const string StateType = "LobbyState";
        public const string EndType = "LobbyEnd";
        public const string HelloType = "LobbyHello";
        public const string ReadyType = "LobbyReady";
        public const string FactionType = "LobbyFaction";

        public static bool IsLobbyType(string? type) =>
            type == WelcomeType || type == RosterType || type == StateType || type == EndType || type == HelloType || type == ReadyType
            || type == FactionType;

        /// <summary>The frames a guest sends.</summary>
        public static bool IsGuestType(string? type) => type == HelloType || type == ReadyType || type == FactionType;

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

        /// <param name="separateAtStart">A hosted shared save becomes separate colonies at Start (sent only when true).</param>
        public static JObject Roster(IEnumerable<LobbyPlayer> players, bool separateAtStart = false)
        {
            var frame = new JObject { [TimberNetBase.TYPE_KEY] = RosterType, ["players"] = new JArray(players.Select(p => p.ToJson())) };
            if (separateAtStart) frame["separateAtStart"] = true;
            return frame;
        }

        public static bool TryParseRoster(JObject frame, out List<LobbyPlayer> players) => TryParseRoster(frame, out players, out _);

        /// <summary>A roster, and whether the host has the save become separate colonies at Start (absent: no).</summary>
        public static bool TryParseRoster(JObject frame, out List<LobbyPlayer> players, out bool separateAtStart)
        {
            separateAtStart = frame["separateAtStart"] is JValue { Type: JTokenType.Boolean } flag && (bool)flag;
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

        /// <summary>A guest's pick of faction in a mixed room: the faction's id, as the game names it ("IronTeeth").</summary>
        public static JObject Faction(string factionId) => new JObject { [TimberNetBase.TYPE_KEY] = FactionType, ["faction"] = factionId };

        public static bool TryParseFaction(JObject frame, out string factionId)
        {
            factionId = "";
            if (!TryString(frame["faction"], out string id) || !IsWellFormedFactionId(id)) return false;
            factionId = id;
            return true;
        }

        /// <summary>The same rule as the colony slot table: short, one line, no field separator.</summary>
        public static bool IsWellFormedId(string? id) =>
            !string.IsNullOrEmpty(id) && id!.Length <= MaxIdLength && id.IndexOfAny(new[] { '|', '\n', '\r' }) < 0;

        /// <summary>A faction id as the game's blueprints name one: letters, digits, dots and underscores, at most 64.</summary>
        public static bool IsWellFormedFactionId(string? id) =>
            !string.IsNullOrEmpty(id) && id!.Length <= 64 && id.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_');

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
