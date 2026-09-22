using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Colonies
{
    /// <summary>Whose notification journal an entry belongs in, kept free of the game for the checks.</summary>
    public static class JournalFilter
    {
        /// <summary>
        /// Whether this player's journal lists an entry. Alone, or before this player is seated (active false), every
        /// entry is listed, as in the game. Otherwise an entry about a thing of this player's colony: by the colony it
        /// is in now, else by the colony recorded for it (a dead beaver has left its district before its death is
        /// posted; after a reload its body may be gone). A thing that still exists in no colony, and an entry about
        /// nothing, are everyone's. A thing gone with no colony recorded (a journal saved by an earlier build) is
        /// hidden: it may be the other colony's.
        /// </summary>
        public static bool ShouldShow(bool active, int localSlot, bool subjectIsEmpty, bool entityExists, int? liveOwner,
            int? recordedOwner)
        {
            if (!active || subjectIsEmpty) return true;
            int? owner = liveOwner ?? recordedOwner;
            if (owner != null) return owner.Value == localSlot;
            return entityExists;
        }

        /// <summary>
        /// Whether a thing is this player's to see, in the alerts and the batch control window: by the colony it is in
        /// now, else by the colony recorded for it (a dead beaver, whose alert shows while its body lies there, or a
        /// beaver cut off from its district). A thing in no colony, with none recorded, is everyone's.
        /// </summary>
        public static bool IsOwn(int localSlot, int? liveOwner, int? recordedOwner)
        {
            int? owner = liveOwner ?? recordedOwner;
            return owner == null || owner.Value == localSlot;
        }

        /// <summary>
        /// Which recorded subjects can be forgotten when the record grows: those out of the journal (kept) that are gone.
        /// A living beaver keeps its last colony, for the day it dies in no district (cut off, or its district deleted).
        /// </summary>
        public static List<Guid> Forgettable(IEnumerable<Guid> recorded, ICollection<Guid> kept, Func<Guid, bool> exists) =>
            recorded.Where(subject => !kept.Contains(subject) && !exists(subject)).ToList();

        /// <summary>The recorded owners, for the save: "subject:slot" separated by commas, in the order given.</summary>
        public static string Encode(IEnumerable<KeyValuePair<Guid, int>> owners) =>
            string.Join(",", owners.Select(pair => $"{pair.Key:N}:{pair.Value}"));

        /// <summary>Owners read back from a save. Anything that is not a subject and a slot is skipped.</summary>
        public static List<KeyValuePair<Guid, int>> Decode(string text)
        {
            var owners = new List<KeyValuePair<Guid, int>>();
            foreach (string entry in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && Guid.TryParse(parts[0], out Guid subject) && int.TryParse(parts[1], out int slot)
                    && slot >= 0 && slot < ColonySlotTable.MaxSlots)
                    owners.Add(new KeyValuePair<Guid, int>(subject, slot));
            }
            return owners;
        }
    }
}
