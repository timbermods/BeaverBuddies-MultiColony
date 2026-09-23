using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// Each colony's faction, by slot: written when the colony gets its district center (a new game's start, a founding,
    /// a switch), so a slot without an entry has never had one. Saved as "slot|faction" rows. Plain code for the checks.
    /// </summary>
    public sealed class FactionTable
    {
        public const int MaxSlots = 4;

        private readonly string[] bySlot = new string[MaxSlots];

        /// <summary>The slot's faction, or null when it has none recorded.</summary>
        public string Of(int slot) => slot >= 0 && slot < MaxSlots ? bySlot[slot] : null;

        public bool Has(int slot) => Of(slot) != null;

        public void Set(int slot, string faction)
        {
            if (slot < 0 || slot >= MaxSlots) return;
            bySlot[slot] = string.IsNullOrEmpty(faction) ? null : faction;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxSlots; i++) bySlot[i] = null;
        }

        public FactionTable Copy()
        {
            var copy = new FactionTable();
            for (int i = 0; i < MaxSlots; i++) copy.bySlot[i] = bySlot[i];
            return copy;
        }

        /// <summary>Rows in slot order, only for slots that have a faction.</summary>
        public List<string> Encode()
        {
            var rows = new List<string>();
            for (int i = 0; i < MaxSlots; i++) if (bySlot[i] != null) rows.Add(i + "|" + bySlot[i]);
            return rows;
        }

        /// <summary>Reads rows; a row that is not "slot|faction" with a slot from 0 to 3 is skipped, the first row for a slot wins.</summary>
        public static FactionTable Decode(IEnumerable<string> rows)
        {
            var table = new FactionTable();
            if (rows == null) return table;
            foreach (string row in rows)
            {
                if (string.IsNullOrEmpty(row)) continue;
                string[] parts = row.Split('|');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int slot) || slot < 0 || slot >= MaxSlots) continue;
                string faction = parts[1].Trim();
                if (faction.Length == 0 || faction.IndexOfAny(new[] { '\n', '\r' }) >= 0 || table.Has(slot)) continue;
                table.bySlot[slot] = faction;
            }
            return table;
        }

        /// <summary>For the daily check and the log: "0Folktails,1IronTeeth".</summary>
        public string Fingerprint() => string.Join(",", Enumerable.Range(0, MaxSlots).Where(Has).Select(i => i + bySlot[i]));

        public override string ToString() => string.Join(" ", Enumerable.Range(0, MaxSlots).Where(Has).Select(i => $"{i}:{bySlot[i]}"));
    }
}
