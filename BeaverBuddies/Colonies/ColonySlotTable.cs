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
