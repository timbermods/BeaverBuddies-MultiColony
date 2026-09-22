using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace TimberNet
{
    /// <summary>What a guest has heard from a host's waiting room, as one consistent picture.</summary>
    public sealed class LobbyView
    {
        public LobbyView(int version, bool welcomed, int you, LobbySummary? summary, IReadOnlyList<LobbyPlayer> players,
            LobbyStage stage, bool ended, LobbyEndReason endReason, string? endDetail, double lastFrameAtMs)
        {
            Version = version;
            Welcomed = welcomed;
            You = you;
            Summary = summary;
            Players = players;
            Stage = stage;
            Ended = ended;
            EndReason = endReason;
            EndDetail = endDetail;
            LastFrameAtMs = lastFrameAtMs;
        }

        /// <summary>Changes whenever anything here does (a keep-alive with nothing new included).</summary>
        public int Version { get; }
        /// <summary>The host opened a waiting room and let this guest in (so it is not hosting a save).</summary>
        public bool Welcomed { get; }
        /// <summary>This guest's player number.</summary>
        public int You { get; }
        public LobbySummary? Summary { get; }
        public IReadOnlyList<LobbyPlayer> Players { get; }
        public LobbyStage Stage { get; }
        public bool Ended { get; }
        public LobbyEndReason EndReason { get; }
        public string? EndDetail { get; }
        /// <summary>When the host was last heard from (<see cref="RttTracker.NowMs"/>), for the "no answer" box.</summary>
        public double LastFrameAtMs { get; }
    }

    /// <summary>
    /// A guest's copy of the waiting room: written by its receive thread as the host's frames arrive, read by the menu
    /// page each frame through <see cref="View"/>. Frames that don't parse are dropped: this is display only.
    /// </summary>
    public sealed class LobbyInbox
    {
        private readonly object gate = new object();
        private int version;
        private bool welcomed;
        private int you = -1;
        private LobbySummary? summary;
        private IReadOnlyList<LobbyPlayer> players = new List<LobbyPlayer>();
        private string? lastRosterText;
        private LobbyStage stage = LobbyStage.Open;
        private int lastSequence = -1;
        private bool ended;
        private LobbyEndReason endReason;
        private string? endDetail;
        private double lastFrameAtMs;

        public LobbyInbox()
        {
            lastFrameAtMs = RttTracker.NowMs;
        }

        public LobbyView View()
        {
            lock (gate) return new LobbyView(version, welcomed, you, summary, players, stage, ended, endReason, endDetail, lastFrameAtMs);
        }

        internal void Receive(string? type, JObject frame, double nowMs)
        {
            lock (gate)
            {
                lastFrameAtMs = nowMs;
                switch (type)
                {
                    case LobbyFrames.WelcomeType:
                        if (welcomed || !LobbyFrames.TryParseWelcome(frame, out int number, out LobbySummary? welcomeSummary)) return;
                        welcomed = true;
                        you = number;
                        summary = welcomeSummary;
                        break;
                    case LobbyFrames.RosterType:
                        // The host repeats the roster with every keep-alive: only a different one is news.
                        string rosterText = frame.ToString(Newtonsoft.Json.Formatting.None);
                        if (rosterText == lastRosterText || !LobbyFrames.TryParseRoster(frame, out List<LobbyPlayer> roster)) return;
                        lastRosterText = rosterText;
                        players = roster;
                        break;
                    case LobbyFrames.StateType:
                        // Frames come in order on one connection; the sequence only guards against an old one.
                        if (!LobbyFrames.TryParseState(frame, out int sequence, out LobbyStage newStage) || sequence <= lastSequence) return;
                        lastSequence = sequence;
                        if (newStage == stage) return;
                        stage = newStage;
                        break;
                    case LobbyFrames.EndType:
                        if (ended || !LobbyFrames.TryParseEnd(frame, out LobbyEndReason reason, out string? detail)) return;
                        ended = true;
                        endReason = reason;
                        endDetail = detail;
                        break;
                    default:
                        return;
                }
                version++;
            }
        }
    }
}
