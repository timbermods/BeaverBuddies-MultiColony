using BeaverBuddies.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// What each colony says it is looking for: up to three goods (or science, or beavers), shown beside the colony in
    /// the trading window and marked in the good picker when a neighbour chooses what to give. Saved colony state,
    /// the same on every computer; set by an action the host judges (a colony sets only its own).
    /// </summary>
    public class ColonyWishlist : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey WishlistKey = new SingletonKey("BeaverBuddies.ColonyWishlist");
        private static readonly ListKey<string> EntriesKey = new ListKey<string>("Wishes");

        private readonly ISingletonLoader _singletonLoader;
        private readonly List<string>[] wishes = Enumerable.Range(0, ColonySlotTable.MaxSlots).Select(_ => new List<string>()).ToArray();

        public static ColonyWishlist Instance => SingletonManager.GetSingleton<ColonyWishlist>();

        public ColonyWishlist(ISingletonLoader singletonLoader)
        {
            _singletonLoader = singletonLoader;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(WishlistKey, out IObjectLoader loader) || !loader.Has(EntriesKey)) return;
            foreach (string entry in loader.Get(EntriesKey))
            {
                if (WishlistTerms.TryDecode(entry, out int slot, out List<string> items))
                    wishes[slot] = WishlistTerms.Normalize(items, null);
            }
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            List<string> entries = Enumerable.Range(0, wishes.Length).Where(slot => wishes[slot].Count > 0)
                .Select(slot => WishlistTerms.Encode(slot, wishes[slot])).ToList();
            if (entries.Count > 0) singletonSaver.GetSingleton(WishlistKey).Set(EntriesKey, entries);
        }

        public IReadOnlyList<string> Of(int slot) => slot >= 0 && slot < wishes.Length ? wishes[slot] : (IReadOnlyList<string>)Array.Empty<string>();

        public bool Wants(int slot, string item) => !string.IsNullOrEmpty(item) && Of(slot).Contains(item);

        /// <summary>Played on every computer: the colony's player changed what it is looking for.</summary>
        public void Set(int slot, IEnumerable<string> items)
        {
            if (slot < 0 || slot >= wishes.Length) return;
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;
            wishes[slot] = WishlistTerms.Normalize(items, item => exchanges == null || exchanges.IsKnownItem(item));
            ColonyDigest.Note("wishlist", slot, ColonyDigest.Of(string.Join(",", wishes[slot])));
            Plugin.Log($"[Colony] Slot {slot} is looking for: {(wishes[slot].Count == 0 ? "nothing" : string.Join(", ", wishes[slot]))}");
        }

        /// <summary>Diagnostics: each colony's wishes.</summary>
        public string Fingerprint() => string.Join(" ", wishes.Select((w, i) => $"{i}:{(w.Count == 0 ? "-" : string.Join(",", w))}"));
    }

    /// <summary>A colony's player says what the colony is looking for (their own colony only, as the host stamps it).</summary>
    [Serializable]
    public class WishlistChangedEvent : ReplayEvent
    {
        public List<string> items = new List<string>();

        // Only ever the actor's own colony (the stamped slot), so there is no entity to judge.
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context) => ColonyWishlist.Instance?.Set(slot, items);

        public override string ToActionString() => "Looking for: " + string.Join(", ", items ?? new List<string>());
    }
}
