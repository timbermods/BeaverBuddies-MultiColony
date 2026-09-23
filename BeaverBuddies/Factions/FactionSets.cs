using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// Which faction lists what: goods, needs, templates or plantables, as the game's collections give them (a faction
    /// names its collection ids, a collection names its items). An item in the common collections, or listed by more
    /// than one faction, belongs to nobody in particular; an item only one faction lists is that faction's. Kept free of
    /// the game so it is checked headless (StabilityTests), and fed the real data by FactionCatalog and RuntimeChecks.
    /// </summary>
    public sealed class FactionSets
    {
        private readonly List<string> factions;
        private readonly HashSet<string> common;
        private readonly Dictionary<string, HashSet<string>> own = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, List<string>> listedBy = new Dictionary<string, List<string>>();

        /// <param name="factionOrder">Every faction id, in the game's order.</param>
        /// <param name="commonItems">Items of the common collections.</param>
        /// <param name="itemsByFaction">Each faction's own items (its collections, merged).</param>
        public FactionSets(IEnumerable<string> factionOrder, IEnumerable<string> commonItems,
            IReadOnlyDictionary<string, IEnumerable<string>> itemsByFaction)
        {
            factions = factionOrder.Where(f => !string.IsNullOrEmpty(f)).Distinct().ToList();
            common = new HashSet<string>(commonItems ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (string faction in factions)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                if (itemsByFaction != null && itemsByFaction.TryGetValue(faction, out IEnumerable<string> items) && items != null)
                {
                    foreach (string item in items)
                    {
                        if (string.IsNullOrEmpty(item) || !set.Add(item)) continue;
                        if (!listedBy.TryGetValue(item, out List<string> by)) listedBy[item] = by = new List<string>();
                        by.Add(faction);
                    }
                }
                own[faction] = set;
            }
        }

        /// <summary>Builds the sets from the game's shape of the data: faction → collection ids, collection → items.</summary>
        public static FactionSets FromCollections(IEnumerable<string> factionOrder, IEnumerable<string> commonCollectionIds,
            IReadOnlyDictionary<string, IEnumerable<string>> collectionIdsByFaction,
            IReadOnlyDictionary<string, IEnumerable<string>> itemsByCollection)
        {
            IEnumerable<string> ItemsOfCollections(IEnumerable<string> ids) =>
                (ids ?? Enumerable.Empty<string>()).SelectMany(id =>
                    itemsByCollection != null && itemsByCollection.TryGetValue(id, out IEnumerable<string> items) && items != null
                        ? items : Enumerable.Empty<string>());
            List<string> order = factionOrder.ToList();
            var byFaction = new Dictionary<string, IEnumerable<string>>();
            foreach (string faction in order)
            {
                byFaction[faction] = collectionIdsByFaction != null && collectionIdsByFaction.TryGetValue(faction, out IEnumerable<string> ids)
                    ? ItemsOfCollections(ids).ToList() : new List<string>();
            }
            return new FactionSets(order, ItemsOfCollections(commonCollectionIds).ToList(), byFaction);
        }

        public IReadOnlyList<string> Factions => factions;

        /// <summary>The one faction whose collections alone list this item; null for a common or shared item.</summary>
        public string SoleFaction(string item)
        {
            if (string.IsNullOrEmpty(item) || common.Contains(item)) return null;
            return listedBy.TryGetValue(item, out List<string> by) && by.Count == 1 ? by[0] : null;
        }

        /// <summary>Common, listed by several factions, or listed by none.</summary>
        public bool IsCommon(string item) => SoleFaction(item) == null;

        /// <summary>Any collection lists it.</summary>
        public bool IsKnown(string item) => !string.IsNullOrEmpty(item) && (common.Contains(item) || listedBy.ContainsKey(item));

        /// <summary>Whether a faction has this item: common, or in its own collections.</summary>
        public bool Has(string faction, string item) =>
            !string.IsNullOrEmpty(item) && (common.Contains(item) || (faction != null && own.TryGetValue(faction, out HashSet<string> set) && set.Contains(item)));

        /// <summary>Every item a faction has (common first, then its own), each once, in a stable order.</summary>
        public IEnumerable<string> ItemsOf(string faction)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string item in common.OrderBy(i => i, StringComparer.Ordinal)) if (seen.Add(item)) yield return item;
            if (faction == null || !own.TryGetValue(faction, out HashSet<string> set)) yield break;
            foreach (string item in set.OrderBy(i => i, StringComparer.Ordinal)) if (seen.Add(item)) yield return item;
        }

        /// <summary>Items both factions have (what may cross between them, for goods).</summary>
        public IEnumerable<string> Shared(string a, string b) => ItemsOf(a).Where(item => Has(b, item));
    }
}
