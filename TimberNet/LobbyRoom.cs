using System;
using System.Collections.Generic;
using System.Linq;

namespace TimberNet
{
    /// <summary>A guest in a host's waiting room.</summary>
    public sealed class LobbyMember
    {
        internal LobbyMember(int number, ISocketStream stream, string? verifiedId)
        {
            Number = number;
            Stream = stream;
            VerifiedId = verifiedId;
            Name = PlayerActivity.CleanName(stream.Name);
        }

        /// <summary>The player number the host gave when the guest came in (it keeps it in the game).</summary>
        public int Number { get; }
        internal ISocketStream Stream { get; }
        /// <summary>Who the connection proved it is (a Steam ID), or null over a direct connection.</summary>
        public string? VerifiedId { get; }
        /// <summary>Who the guest says it is (its hello), or null before it has said.</summary>
        public string? ClaimedId { get; internal set; }
        public string Name { get; internal set; }
        public bool Ready { get; internal set; }
        public bool SaidHello { get; internal set; }
        /// <summary>The faction it picked in a mixed room, or null (the room's base faction).</summary>
        public string? Faction { get; internal set; }
        /// <summary>The id its colony seat will be found by in the game: the proved one if any, else the one it said.</summary>
        public string? StableId => VerifiedId ?? ClaimedId;
        public bool Connected => Stream.Connected;

        // Set as the save is about to go out: from then on the stream belongs to the game, and nothing of the waiting room
        // is written to it (see TimberServer.WriteLobbyFrame).
        internal volatile bool inGame;

        // Waiting (0), in the game (1) or gone (2), claimed once: the join's StartQueuing and the room's removal (the host's,
        // the 10 s straggler rule's, a lost connection's) can race, and exactly one of them must win.
        private int fate;

        /// <summary>The join takes this member into the game; false if it has already left the room.</summary>
        internal bool TryEnterGame()
        {
            if (System.Threading.Interlocked.CompareExchange(ref fate, 1, 0) != 0) return false;
            inGame = true;
            return true;
        }

        /// <summary>The room lets this member go; false if it is already in the game (or already gone).</summary>
        internal bool TryLeave() => System.Threading.Interlocked.CompareExchange(ref fate, 2, 0) == 0;
        /// <summary>What the room says to this guest, written in order on a thread of its own (never waiting for it).</summary>
        internal SendLane? Lane;
        public bool InGame => inGame;
    }

    /// <summary>What the waiting room looks like right now, for the host's page. Safe to keep.</summary>
    public sealed class LobbySnapshot
    {
        public LobbySnapshot(int version, LobbyStage stage, bool closed, IReadOnlyList<LobbyPlayer> players, IReadOnlyList<LobbyMemberInfo> guests)
        {
            Version = version;
            Stage = stage;
            ClosedToNewcomers = closed;
            Players = players;
            Guests = guests;
        }

        /// <summary>Changes whenever anything in the room does.</summary>
        public int Version { get; }
        public LobbyStage Stage { get; }
        public bool ClosedToNewcomers { get; }
        /// <summary>Every row, host first, in the order the guests came in.</summary>
        public IReadOnlyList<LobbyPlayer> Players { get; }
        /// <summary>The guests alone, in the same order, with what the host needs to seat them.</summary>
        public IReadOnlyList<LobbyMemberInfo> Guests { get; }
    }

    public sealed class LobbyMemberInfo
    {
        public LobbyMemberInfo(int number, string name, string? stableId, bool saidHello, bool ready, bool inGame, string? faction = null)
        {
            Number = number;
            Name = name;
            StableId = stableId;
            SaidHello = saidHello;
            Ready = ready;
            InGame = inGame;
            Faction = faction;
        }

        public int Number { get; }
        public string Name { get; }
        public string? StableId { get; }
        public bool SaidHello { get; }
        public bool Ready { get; }
        public bool InGame { get; }
        /// <summary>The faction it picked in a mixed room, or null for none (it plays the room's base faction).</summary>
        public string? Faction { get; }
    }

    /// <summary>
    /// The host's waiting room: who is in it, in the order they came in, whether each is ready, and where the host is
    /// with starting. The server keeps it up to date from its network threads; the host's page reads
    /// <see cref="Snapshot"/>. Presentation and seating only: nothing here reaches the game.
    /// </summary>
    public sealed class LobbyRoom
    {
        /// <summary>The Steam lobby holds eight, the host included.</summary>
        public const int MaxGuests = 7;
        public const int MaxColonies = 4;
        public const string FullMessage = "The waiting room is full.";

        private readonly object gate = new object();
        private readonly List<LobbyMember> members = new List<LobbyMember>();
        private LobbyStage stage = LobbyStage.Open;
        private string? closedMessage;
        private int version;

        public LobbyRoom(LobbySummary summary)
        {
            Summary = summary;
            hostFaction = string.IsNullOrEmpty(summary.FactionId) ? null : summary.FactionId;
        }

        public LobbySummary Summary { get; }

        private string? hostFaction;

        /// <summary>
        /// A hosted save's seating, set by the host before any guest comes in: given every row's stable id in the room's
        /// order (the host's first; null for a guest that has not said who it is), the colony each will play (1 to 4, 0
        /// for a helper, null when unknown). Null for a new game, whose rows take colonies in the order they came in
        /// (<see cref="ColonyOf"/>). Display only.
        /// </summary>
        public Func<IReadOnlyList<string?>, IReadOnlyList<int?>>? Seating { get; set; }
        /// <summary>The host's own stable id, for <see cref="Seating"/>.</summary>
        public string? HostStableId { get; set; }
        /// <summary>A hosted save's faction per colony (1 to 4), or null for a colony that has none yet.</summary>
        public Func<int, string?>? FactionOfColony { get; set; }

        /// <summary>The host's own pick in a mixed room (its colony's faction, the new game's base faction).</summary>
        public string? HostFaction { get { lock (gate) return hostFaction; } }

        /// <summary>The host picked its faction in a mixed new game's room, which is still open.</summary>
        public void SetHostFaction(string factionId)
        {
            lock (gate)
            {
                if (!Summary.Mixed || Summary.IsSave || stage != LobbyStage.Open || !Summary.Factions.Contains(factionId)
                    || hostFaction == factionId) return;
                hostFaction = factionId;
                version++;
            }
        }

        /// <summary>
        /// A guest's pick of faction: taken only in a mixed room that is still open, from a guest that has said who it is
        /// and may pick, for a faction the room offers. Anything else is dropped: the room is display and setup only.
        /// </summary>
        internal bool SetFaction(LobbyMember member, string factionId)
        {
            lock (gate)
            {
                if (!Summary.Mixed || stage != LobbyStage.Open || !member.SaidHello || !Summary.Factions.Contains(factionId)
                    || member.Faction == factionId) return false;
                List<LobbyMember> shown = ShownLocked();
                int index = shown.IndexOf(member);
                if (index < 0 || !MayPickLocked(ColoniesLocked(shown)[index + 1])) return false;
                member.Faction = factionId;
                version++;
                return true;
            }
        }

        /// <summary>
        /// The colony a row will play: the host (index 0) colony 1, guests 2, 3, 4 in the order they came in, then
        /// helpers of colony 1 (0). Null in a shared game, which has no colonies. The host writes this order into the
        /// colony slot table before the world's first save, so it is what the game gives them.
        /// </summary>
        public static int? ColonyOf(int index, bool separateColonies) =>
            !separateColonies ? (int?)null : index < MaxColonies ? index + 1 : 0;

        public int Version { get { lock (gate) return version; } }

        public LobbyStage Stage
        {
            get { lock (gate) return stage; }
            internal set { lock (gate) { stage = value; version++; } }
        }

        public bool IsClosedToNewcomers { get { lock (gate) return closedMessage != null; } }

        /// <summary>A newcomer is refused with this once the room is closed; null while it is open.</summary>
        internal string? ClosedMessage { get { lock (gate) return closedMessage; } }

        internal void CloseToNewcomers(string message)
        {
            lock (gate)
            {
                closedMessage ??= message;
                if (stage == LobbyStage.Open) stage = LobbyStage.Starting;
                version++;
            }
        }

        /// <summary>Why a newcomer may not come in now, or null if it may.</summary>
        internal string? RefusalForNewcomer()
        {
            lock (gate)
            {
                if (closedMessage != null) return closedMessage;
                if (members.Count(m => m.Connected) >= MaxGuests) return FullMessage;
                return null;
            }
        }

        internal void Add(LobbyMember member)
        {
            lock (gate) { members.Add(member); version++; }
        }

        internal bool Remove(LobbyMember member)
        {
            lock (gate)
            {
                if (!members.Remove(member)) return false;
                version++;
                return true;
            }
        }

        internal List<LobbyMember> Members() { lock (gate) return members.ToList(); }

        internal LobbyMember? Find(int number) { lock (gate) return members.FirstOrDefault(m => m.Number == number); }

        /// <summary>A guest's hello: taken once (its page sends one), so a guest can't change who it is, or flood the room.</summary>
        internal bool SetHello(LobbyMember member, string id, string name)
        {
            lock (gate)
            {
                if (member.SaidHello) return false;
                member.ClaimedId = id;
                member.Name = name;
                member.SaidHello = true;
                version++;
                return true;
            }
        }

        /// <summary>A guest's ready: false when it changes nothing.</summary>
        internal bool SetReady(LobbyMember member, bool ready)
        {
            lock (gate)
            {
                // Once the host has pressed Start the rows stay as they were: readiness no longer changes anything.
                if (stage != LobbyStage.Open || member.Ready == ready) return false;
                member.Ready = ready;
                version++;
                return true;
            }
        }

        public LobbySnapshot Snapshot()
        {
            lock (gate)
            {
                List<LobbyMember> shown = ShownLocked();
                IReadOnlyList<int?> colonies = ColoniesLocked(shown);
                var players = new List<LobbyPlayer>
                {
                    new LobbyPlayer(0, Summary.HostName, true, true, false, colonies[0], RowFactionLocked(colonies[0], hostFaction),
                        MayPickLocked(colonies[0])),
                };
                var guests = new List<LobbyMemberInfo>();
                for (int i = 0; i < shown.Count; i++)
                {
                    LobbyMember member = shown[i];
                    int? colony = colonies[i + 1];
                    players.Add(new LobbyPlayer(member.Number, member.Name, member.Ready, false, !member.SaidHello, colony,
                        member.SaidHello ? RowFactionLocked(colony, member.Faction) : null, member.SaidHello && MayPickLocked(colony)));
                    guests.Add(new LobbyMemberInfo(member.Number, member.Name, member.StableId, member.SaidHello, member.Ready, member.inGame,
                        member.Faction));
                }
                return new LobbySnapshot(version, stage, closedMessage != null, players, guests);
            }
        }

        // The rows after the host's, in the order they came in.
        private List<LobbyMember> ShownLocked() => members.Where(m => m.Connected || m.inGame).ToList();

        // Every row's colony, the host's first: in the order they came in for a new game, or as a hosted save seats them.
        private IReadOnlyList<int?> ColoniesLocked(List<LobbyMember> shown)
        {
            if (Seating != null)
            {
                var ids = new List<string?> { HostStableId };
                ids.AddRange(shown.Select(m => m.SaidHello ? m.StableId : null));
                try
                {
                    IReadOnlyList<int?> seated = Seating(ids);
                    if (seated != null && seated.Count == ids.Count) return seated;
                }
                catch (Exception) { /* Display only: a failure shows no colonies. */ }
                return ids.Select(_ => (int?)null).ToList();
            }
            var result = new List<int?>();
            for (int index = 0; index <= shown.Count; index++) result.Add(ColonyOf(index, Summary.SeparateColonies));
            return result;
        }

        // The faction a row shows. A room that is not mixed: a new game names none (as 1.4.0-beta19), a save its own. A
        // mixed room: a save's colony keeps the faction it has; otherwise the player's pick, else the room's base faction.
        private string? RowFactionLocked(int? colony, string? picked)
        {
            if (!Summary.Mixed) return Summary.IsSave && !string.IsNullOrEmpty(Summary.FactionId) ? Summary.FactionId : null;
            string? colonyFaction = colony.HasValue && colony.Value > 0 ? FactionOfColony?.Invoke(colony.Value) : null;
            if (colonyFaction != null) return colonyFaction;
            if (picked != null) return picked;
            return hostFaction ?? (string.IsNullOrEmpty(Summary.FactionId) ? null : Summary.FactionId);
        }

        // A mixed new game: everyone picks. A mixed save: only a player whose colony has no faction yet (it will found).
        private bool MayPickLocked(int? colony)
        {
            if (!Summary.Mixed) return false;
            if (!Summary.IsSave) return true;
            return colony.HasValue && colony.Value > 0 && FactionOfColony?.Invoke(colony.Value) == null;
        }
    }
}
