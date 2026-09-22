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
        /// <summary>The id its colony seat will be found by in the game: the proved one if any, else the one it said.</summary>
        public string? StableId => VerifiedId ?? ClaimedId;
        public bool Connected => Stream.Connected;

        // Set as the save is about to go out: from then on the stream belongs to the game, and nothing of the waiting room
        // is written to it (see TimberServer.WriteLobbyFrame).
        internal volatile bool inGame;
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
        public LobbyMemberInfo(int number, string name, string? stableId, bool saidHello, bool ready, bool inGame)
        {
            Number = number;
            Name = name;
            StableId = stableId;
            SaidHello = saidHello;
            Ready = ready;
            InGame = inGame;
        }

        public int Number { get; }
        public string Name { get; }
        public string? StableId { get; }
        public bool SaidHello { get; }
        public bool Ready { get; }
        public bool InGame { get; }
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
        }

        public LobbySummary Summary { get; }

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

        internal void SetHello(LobbyMember member, string id, string name)
        {
            lock (gate)
            {
                member.ClaimedId = id;
                member.Name = name;
                member.SaidHello = true;
                version++;
            }
        }

        internal void SetReady(LobbyMember member, bool ready)
        {
            lock (gate)
            {
                // Once the host has pressed Start the rows stay as they were: readiness no longer changes anything.
                if (stage != LobbyStage.Open || member.Ready == ready) return;
                member.Ready = ready;
                version++;
            }
        }

        public LobbySnapshot Snapshot()
        {
            lock (gate)
            {
                bool separate = Summary.SeparateColonies;
                var players = new List<LobbyPlayer> { new LobbyPlayer(0, Summary.HostName, true, true, false, ColonyOf(0, separate)) };
                var guests = new List<LobbyMemberInfo>();
                int index = 1;
                foreach (LobbyMember member in members)
                {
                    if (!member.Connected && !member.inGame) continue;
                    players.Add(new LobbyPlayer(member.Number, member.Name, member.Ready, false, !member.SaidHello, ColonyOf(index, separate)));
                    guests.Add(new LobbyMemberInfo(member.Number, member.Name, member.StableId, member.SaidHello, member.Ready, member.inGame));
                    index++;
                }
                return new LobbySnapshot(version, stage, closedMessage != null, players, guests);
            }
        }
    }
}
