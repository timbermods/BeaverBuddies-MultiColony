using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Colonies
{
    /// <summary>A colony's wishlist in the abstract, kept free of the game so it can be checked headless.</summary>
    public static class WishlistTerms
    {
        /// <summary>How many things a colony can say it is looking for.</summary>
        public const int MaxWishes = 3;

        /// <summary>Distinct, known items only, the first <see cref="MaxWishes"/> of them, in the order given.</summary>
        public static List<string> Normalize(IEnumerable<string> items, Func<string, bool> known)
        {
            var result = new List<string>();
            if (items == null) return result;
            foreach (string item in items)
            {
                if (string.IsNullOrEmpty(item) || result.Contains(item) || (known != null && !known(item))) continue;
                result.Add(item);
                if (result.Count >= MaxWishes) break;
            }
            return result;
        }

        public static string Encode(int slot, IEnumerable<string> items) => slot + "|" + string.Join(",", items ?? Enumerable.Empty<string>());

        public static bool TryDecode(string entry, out int slot, out List<string> items)
        {
            slot = -1;
            items = null;
            string[] parts = (entry ?? "").Split(new[] { '|' }, 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out slot) || slot < 0 || slot >= ColonySlotTable.MaxSlots) return false;
            items = parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            return true;
        }
    }
}
