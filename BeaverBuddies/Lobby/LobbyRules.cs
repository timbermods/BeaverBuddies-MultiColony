using System;
using System.Collections.Generic;
using System.Linq;
using TimberNet;

namespace BeaverBuddies.Lobby
{
    /// <summary>What the host is asked before a start that isn't everyone's (D2).</summary>
    public enum StartQuestion
    {
        /// <summary>Everyone in the room is ready: start at once.</summary>
        None,
        /// <summary>Nobody has come in.</summary>
        Alone,
        /// <summary>Someone is not ready (or still joining).</summary>
        NotReady,
    }

    /// <summary>A line of text as a loc key and its arguments.</summary>
    public readonly struct LobbyText
    {
        public LobbyText(string key, params object[] args)
        {
            Key = key;
            Args = args;
        }

        public string Key { get; }
        public object[] Args { get; }
    }

    /// <summary>
    /// The waiting room's decisions, kept free of the game so they are checked headlessly (StabilityTests). Nothing here
    /// touches the simulation: it chooses what the host is asked, which line the page shows, how many starts a
    /// multi-start map fills, and what the new world's save is called.
    /// </summary>
    public static class LobbyRules
    {
        public const string KeyPrefix = "BeaverBuddies.Lobby.";
        /// <summary>A guest hears nothing for this long (not while its save arrives): it is asked whether to keep waiting.</summary>
        public const double WatchdogMs = 120000;

        /// <summary>
        /// The starts a multi-start map fills (D12): the host and the guests in the room at Start, at most the Players
        /// field and four. A start nobody plays would make a colony nobody runs; a guest beyond the starts founds one.
        /// </summary>
        public static int StartsToFill(int playersField, int guests) =>
            Math.Max(1, Math.Min(Math.Min(playersField, 1 + Math.Max(0, guests)), LobbyRoom.MaxColonies));

        /// <summary>The guests who are not ready, a guest still joining included, in the room's order.</summary>
        public static List<LobbyPlayer> NotReady(IEnumerable<LobbyPlayer> players) =>
            players.Where(p => !p.IsHost && (p.Joining || !p.Ready)).ToList();

        public static StartQuestion StartConfirm(IReadOnlyList<LobbyPlayer> players)
        {
            if (!players.Any(p => !p.IsHost)) return StartQuestion.Alone;
            return NotReady(players).Count > 0 ? StartQuestion.NotReady : StartQuestion.None;
        }

        /// <summary>The host's status line under the players.</summary>
        public static LobbyText HostStatus(IReadOnlyList<LobbyPlayer> players, LobbyStage stage)
        {
            if (stage != LobbyStage.Open) return new LobbyText(KeyPrefix + "Status.Starting");
            // Nobody in the room yet: nothing to say (the Invite button under it says it all).
            if (!players.Any(p => !p.IsHost)) return new LobbyText("");
            List<LobbyPlayer> notReady = NotReady(players);
            if (notReady.Count == 0) return new LobbyText(KeyPrefix + "Status.AllReady");
            if (notReady.Count > 1) return new LobbyText(KeyPrefix + "Status.SomeNotReady", notReady.Count);
            // A guest still joining has not said its name yet: it is counted, never named.
            return notReady[0].Joining
                ? new LobbyText(KeyPrefix + "Status.OneJoining")
                : new LobbyText(KeyPrefix + "Status.OneNotReady", notReady[0].Name);
        }

        /// <summary>A guest's status line.</summary>
        public static LobbyText GuestStatus(bool ready, LobbyStage stage, string hostName)
        {
            switch (stage)
            {
                case LobbyStage.Open:
                    return ready ? new LobbyText(KeyPrefix + "Status.GuestReady", hostName)
                        : new LobbyText(KeyPrefix + "Status.GuestNotReady", hostName);
                case LobbyStage.SendingWorld:
                    return new LobbyText(KeyPrefix + "Status.SendingWorld");
                default:
                    return new LobbyText(KeyPrefix + "Status.CreatingWorld", hostName);
            }
        }

        /// <summary>Whether to ask a guest if it still wants to wait (D19). Never while its save is on its way.</summary>
        public static bool WatchdogDue(double lastFrameMs, double nowMs, LobbyStage stage) =>
            stage != LobbyStage.SendingWorld && nowMs - lastFrameMs >= WatchdogMs;

        /// <summary>
        /// The colony slot each guest gets when the new world is seated in the room's order (LobbyWorldMaker
        /// .SeatInRoomOrder, D11): the host slot 0, then each guest with an id the next free slot, in the order they came
        /// in; a guest without an id is seated when it says hello in the game (null here), and one beyond the four
        /// colonies is a helper (null). The mixed-factions decision uses it to know which slot each guest's pick is for.
        /// </summary>
        public static List<int?> SeatingPlan(string hostId, IReadOnlyList<string> guestIds)
        {
            var table = new BeaverBuddies.Colonies.ColonySlotTable();
            table.Resolve(hostId, "");
            var slots = new List<int?>();
            foreach (string id in guestIds ?? Array.Empty<string>())
                slots.Add(string.IsNullOrEmpty(id) || id == hostId ? null : table.Resolve(id, ""));
            return slots;
        }

        /// <summary>The name of the new world's first save, in the settlement the host named (like a Rehost save's).</summary>
        public static string SaveName(string timestamp) => (timestamp ?? "").Replace(",", "") + " Co-op start";

        /// <summary>The key of a row's tag: the host, a colony, a helper, or none (a guest in a shared game).</summary>
        public static LobbyText? Tag(LobbyPlayer player)
        {
            if (player.Colony == null) return player.IsHost ? new LobbyText(KeyPrefix + "Tag.Host") : (LobbyText?)null;
            if (player.Colony == 0) return new LobbyText(KeyPrefix + "Tag.Helper");
            return player.IsHost ? new LobbyText(KeyPrefix + "Tag.HostColony", player.Colony.Value)
                : new LobbyText(KeyPrefix + "Tag.Colony", player.Colony.Value);
        }
    }
}
