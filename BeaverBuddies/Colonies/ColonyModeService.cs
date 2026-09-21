using System.Collections.Generic;
using System.Linq;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The saved state of a separate-colonies game: whether the mode is on and where each colony started, in colony
    /// order. Nothing else is saved; who owns what follows from these (see ColonyTerritory). The values travel to
    /// guests inside the save the host sends, so every computer has the same ones.
    /// </summary>
    public class ColonyModeService : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey ColonyModeKey = new SingletonKey("BeaverBuddies.ColonyMode");
        private static readonly PropertyKey<bool> EnabledKey = new PropertyKey<bool>("Enabled");
        private static readonly ListKey<Vector3Int> StartsKey = new ListKey<Vector3Int>("Starts");

        private readonly ISingletonLoader _singletonLoader;

        private bool enabled;
        private List<Vector3Int> starts = new List<Vector3Int>();

        /// <summary>The land division when the mode is on and usable, otherwise null. Everything else checks this.</summary>
        public ColonyTerritory Territory { get; private set; }

        /// <summary>The current game's territory, or null in a shared-colony game (and before a game is loaded).</summary>
        public static ColonyTerritory ActiveTerritory => SingletonManager.GetSingleton<ColonyModeService>()?.Territory;

        public ColonyModeService(ISingletonLoader singletonLoader)
        {
            _singletonLoader = singletonLoader;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(ColonyModeKey, out IObjectLoader loader)) return;
            enabled = loader.Has(EnabledKey) && loader.Get(EnabledKey);
            starts = loader.Has(StartsKey) ? loader.Get(StartsKey) : new List<Vector3Int>();
            Apply("loaded from the save");
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            // A shared-colony game saves nothing, so its save is the same as before this mode existed.
            if (!enabled) return;
            IObjectSaver saver = singletonSaver.GetSingleton(ColonyModeKey);
            saver.Set(EnabledKey, enabled);
            saver.Set(StartsKey, starts);
        }

        /// <summary>
        /// Turns the mode on for a new game. Called by the multi-start initializer before the first starting building
        /// is placed, because new district centers read the mode for their trade defaults.
        /// </summary>
        public void Activate(IEnumerable<Vector3Int> startCoordinates)
        {
            enabled = true;
            starts = startCoordinates.ToList();
            Apply("switched on for this new game");
        }

        private void Apply(string how)
        {
            var tiles = starts.Select(s => new ColonyTile(s.x, s.y)).ToList();
            if (!ColonyModeState.IsUsable(enabled, tiles))
            {
                Territory = null;
                if (enabled)
                    Plugin.LogError($"[Colony] Separate colonies are on in this save but it has {tiles.Count} start(s); playing it as one shared colony");
                return;
            }
            Territory = new ColonyTerritory(tiles);
            Plugin.Log($"[Colony] Separate colonies {how}: {tiles.Count} colonies starting at {string.Join(", ", tiles)}");
        }
    }
}
