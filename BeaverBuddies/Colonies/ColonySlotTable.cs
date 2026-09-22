using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Colonies
{
    /// <summary>One player the save knows: their stable id (Steam ID, or a GUID kept on their computer) and slot.</summary>
    public readonly struct ColonySlotEntry
    {
        public readonly string PlayerId;
        public readonly int Slot;
        public readonly string Name;

        public ColonySlotEntry(string playerId, int slot, string name)
        {
            PlayerId = playerId;
            Slot = slot;
            Name = name ?? "";
        }
    }

    /// <summary>The host's answer to a player's hello (see <see cref="ColonySlotTable.CheckHello"/>).</summary>
    public readonly struct HelloCheck
    {
        /// <summary>The id the connection is seated by (only when allowed).</summary>
        public readonly string SeatId;
        /// <summary>Why the hello is refused, for the host's log; null when it is allowed.</summary>
        public readonly string Refusal;
        /// <summary>The slot the seated id plays, or null for a helper (see <see cref="ColonySlotTable.SeatHello"/>).</summary>
        public readonly int? Slot;

        private HelloCheck(string seatId, string refusal, int? slot)
        {
            SeatId = seatId;
            Refusal = refusal;
            Slot = slot;
        }

        public bool IsAllowed => Refusal == null;

        public static HelloCheck Seat(string seatId) => new HelloCheck(seatId, null, null);
        public static HelloCheck Refuse(string why) => new HelloCheck(null, why ?? "refused", null);
        public HelloCheck WithSlot(int? slot) => new HelloCheck(SeatId, Refusal, slot);
    }

    /// <summary>
    /// Which player plays which colony. A slot (0 to <see cref="MaxSlots"/>-1) is a colony: district centers carry
    /// their owner's slot, and on a multi-start map slot N starts at the map's starting location N. The table maps
    /// each player's stable id to their slot and is saved with the game, so a player keeps their colony across
    /// sessions, rehosting and a different player hosting. A player new to the save takes the lowest free slot; with
    /// every slot taken they join as a helper on the host's slot (and are not recorded).
    /// </summary>
    public sealed class ColonySlotTable
    {
        public const int MaxSlots = 4;
        /// <summary>How a Steam player's stable id starts: "steam:" and the Steam ID.</summary>
        public const string SteamIdPrefix = "steam:";

        private readonly List<ColonySlotEntry> entries = new List<ColonySlotEntry>();

        public IReadOnlyList<ColonySlotEntry> Entries => entries;

        public int? SlotOf(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            foreach (ColonySlotEntry entry in entries)
            {
                if (entry.PlayerId == playerId) return entry.Slot;
            }
            return null;
        }

        public string NameOf(int slot) => entries.FirstOrDefault(e => e.Slot == slot).Name;

        /// <summary>The stable id of the player who plays a slot, or null while nobody does.</summary>
        public string PlayerIdOf(int slot) => entries.FirstOrDefault(e => e.Slot == slot).PlayerId;

        /// <summary>
        /// The slot for this player, recording them if they are new and a slot is free. Returns null for a helper
        /// (every slot taken): the caller seats them on the host's slot.
        /// </summary>
        public int? Resolve(string playerId, string name)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].PlayerId != playerId) continue;
                // Keep the latest name a player goes by; the slot never changes.
                if (!string.IsNullOrEmpty(name) && entries[i].Name != name)
                    entries[i] = new ColonySlotEntry(playerId, entries[i].Slot, name);
                return entries[i].Slot;
            }
            for (int slot = 0; slot < MaxSlots; slot++)
            {
                if (entries.Any(e => e.Slot == slot)) continue;
                entries.Add(new ColonySlotEntry(playerId, slot, name));
                Sort();
                return slot;
            }
            return null;
        }

        /// <summary>
        /// The host's verdict on a player's hello: whom to seat the connection as, or why not. <paramref name="claimedId"/>
        /// is the id the hello carries, which the guest's own computer wrote. <paramref name="verifiedId"/> is who the
        /// connection proved to be (a Steam connection's Steam ID), or null for one that proves nothing (direct TCP),
        /// whose claim is taken at its word. <paramref name="alreadySeatedId"/> is the id this connection was seated
        /// by earlier this session ("" for none), or null if it has not said hello yet.
        /// <list type="bullet">
        /// <item>A Steam ID other than the one the connection proved is refused: no honest guest says one.</item>
        /// <item>Any other claim over a proved connection is seated by the proved id. An honest guest says a local id
        /// only when its Steam ID could not be read; refused, it would stay unseated all session (a guest says hello
        /// once), and seated by the local id it would take a second colony, or a direct player's.</item>
        /// <item>A connection already seated can't be seated again as someone else: each hello could otherwise move it
        /// into another player's colony, or take and save a free slot.</item>
        /// </list>
        /// </summary>
        public static HelloCheck CheckHello(string claimedId, string verifiedId, string alreadySeatedId)
        {
            string seatId = claimedId;
            if (!string.IsNullOrEmpty(verifiedId) && claimedId != verifiedId)
            {
                if (claimedId != null && claimedId.StartsWith(SteamIdPrefix, StringComparison.Ordinal))
                    return HelloCheck.Refuse($"it says it is {claimedId}, but its connection is {verifiedId}");
                seatId = verifiedId;
            }
            if (alreadySeatedId != null && (seatId ?? "") != alreadySeatedId)
                return HelloCheck.Refuse($"it said hello again as {seatId}, but this session it is {(alreadySeatedId == "" ? "a player without an id" : alreadySeatedId)}");
            return HelloCheck.Seat(seatId);
        }

        /// <summary>
        /// Host, as a hello is played: <see cref="CheckHello"/>, then the slot for the id it seats (<see cref="Resolve"/>,
        /// which records a new player). A refused hello leaves the table as it was.
        /// </summary>
        public HelloCheck SeatHello(string claimedId, string verifiedId, string alreadySeatedId, string name)
        {
            HelloCheck check = CheckHello(claimedId, verifiedId, alreadySeatedId);
            if (!check.IsAllowed) return check;
            return check.WithSlot(Resolve(check.SeatId, name));
        }

        /// <summary>Replaces the whole table (a save being loaded, or the host's table arriving).</summary>
        public void Set(IEnumerable<ColonySlotEntry> newEntries)
        {
            entries.Clear();
            foreach (ColonySlotEntry entry in newEntries)
            {
                if (string.IsNullOrEmpty(entry.PlayerId) || entry.Slot < 0 || entry.Slot >= MaxSlots) continue;
                if (entries.Any(e => e.PlayerId == entry.PlayerId || e.Slot == entry.Slot)) continue;
                entries.Add(entry);
            }
            Sort();
        }

        // Always in slot order, so saving and sending never depend on the order players arrived in.
        private void Sort() => entries.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        /// <summary>A compact text form for events and saves: "slot|id|name" per line.</summary>
        public static string Encode(IEnumerable<ColonySlotEntry> entries) =>
            string.Join("\n", entries.Select(e => $"{e.Slot}|{e.PlayerId}|{e.Name.Replace("\n", " ").Replace("|", "/")}"));

        public static List<ColonySlotEntry> Decode(string text)
        {
            var result = new List<ColonySlotEntry>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (string line in text.Split('\n'))
            {
                string[] parts = line.Split(new[] { '|' }, 3);
                if (parts.Length < 2 || !int.TryParse(parts[0], out int slot)) continue;
                result.Add(new ColonySlotEntry(parts[1], slot, parts.Length > 2 ? parts[2] : ""));
            }
            return result;
        }
    }
}
