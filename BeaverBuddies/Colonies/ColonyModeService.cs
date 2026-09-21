using System.Collections.Generic;
using System.Linq;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>What a colony starts with, taken from the new-game settings so a colony founded later gets the same.</summary>
    public sealed class ColonyStartingSettings
    {
        public int Adults;
        public float AdultAgeMin, AdultAgeMax;
        public int Children;
        public float ChildAgeMin, ChildAgeMax;
        public int Food;
        public int Water;

        public override string ToString() =>
            $"{Adults} adults, {Children} children, {Food} food, {Water} water";
    }

    /// <summary>
    /// The saved state of a separate-colonies game: whether the mode is on, where each colony started (in colony
    /// order), whether colony 2 is still to be founded (a map with one start), and the new game's starting settings
    /// for that founding. Who owns what follows from the starts (see ColonyTerritory). The values travel to guests
    /// inside the save the host sends, so every computer has the same ones.
    /// </summary>
    public class ColonyModeService : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey ColonyModeKey = new SingletonKey("BeaverBuddies.ColonyMode");
        private static readonly PropertyKey<bool> EnabledKey = new PropertyKey<bool>("Enabled");
        private static readonly ListKey<Vector3Int> StartsKey = new ListKey<Vector3Int>("Starts");
        private static readonly PropertyKey<bool> AwaitingFoundingKey = new PropertyKey<bool>("AwaitingFounding");
        private static readonly PropertyKey<int> AdultsKey = new PropertyKey<int>("StartingAdults");
        private static readonly PropertyKey<float> AdultAgeMinKey = new PropertyKey<float>("StartingAdultAgeMin");
        private static readonly PropertyKey<float> AdultAgeMaxKey = new PropertyKey<float>("StartingAdultAgeMax");
        private static readonly PropertyKey<int> ChildrenKey = new PropertyKey<int>("StartingChildren");
        private static readonly PropertyKey<float> ChildAgeMinKey = new PropertyKey<float>("StartingChildAgeMin");
        private static readonly PropertyKey<float> ChildAgeMaxKey = new PropertyKey<float>("StartingChildAgeMax");
        private static readonly PropertyKey<int> FoodKey = new PropertyKey<int>("StartingFood");
        private static readonly PropertyKey<int> WaterKey = new PropertyKey<int>("StartingWater");
        private static readonly PropertyKey<Vector3Int> FirstStartKey = new PropertyKey<Vector3Int>("FirstColonyStart");

        private readonly ISingletonLoader _singletonLoader;

        private bool enabled;
        private List<Vector3Int> starts = new List<Vector3Int>();

        /// <summary>The land division when the mode is on and usable, otherwise null. Everything else checks this.</summary>
        public ColonyTerritory Territory { get; private set; }

        /// <summary>A one-start game whose second colony has not been founded yet.</summary>
        public bool AwaitingFounding { get; private set; }

        /// <summary>The new game's starting settings, given to a colony founded later. Null if never recorded.</summary>
        public ColonyStartingSettings StartingSettings { get; private set; }

        /// <summary>Where colony 1's starting building stands, recorded while colony 2 is awaited. Null if never recorded.</summary>
        public Vector3Int? FirstColonyStart { get; private set; }

        /// <summary>The current game's territory, or null in a shared-colony game (and before a game is loaded).</summary>
        public static ColonyTerritory ActiveTerritory => SingletonManager.GetSingleton<ColonyModeService>()?.Territory;

        /// <summary>True in a one-start game until colony 2 is founded.</summary>
        public static bool FoundingPending => SingletonManager.GetSingleton<ColonyModeService>()?.AwaitingFounding == true;

        /// <summary>
        /// True in a separate-colonies game, and in a game created to await colony 2. A shared game in which colony 2
        /// may still be founded is not one (yet): it plays as one shared colony until then.
        /// </summary>
        public static bool IsSeparateColonies => ActiveTerritory != null || FoundingPending;

        /// <summary>
        /// Colony 2 may be founded now: it does not exist yet, and the save was created for it or the host allows it
        /// this session. The host's choice only gates who may ask; the founding itself depends on saved state alone.
        /// </summary>
        public static bool FoundingOpen =>
            ColonyModeState.FoundingOpen(ActiveTerritory != null, FoundingPending, ColonySession.HostAllowsFounding);

        public ColonyModeService(ISingletonLoader singletonLoader)
        {
            _singletonLoader = singletonLoader;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(ColonyModeKey, out IObjectLoader loader)) return;
            enabled = loader.Has(EnabledKey) && loader.Get(EnabledKey);
            starts = loader.Has(StartsKey) ? loader.Get(StartsKey) : new List<Vector3Int>();
            AwaitingFounding = loader.Has(AwaitingFoundingKey) && loader.Get(AwaitingFoundingKey);
            FirstColonyStart = loader.Has(FirstStartKey) ? loader.Get(FirstStartKey) : (Vector3Int?)null;
            if (loader.Has(AdultsKey))
            {
                StartingSettings = new ColonyStartingSettings
                {
                    Adults = loader.Get(AdultsKey),
                    AdultAgeMin = loader.Get(AdultAgeMinKey),
                    AdultAgeMax = loader.Get(AdultAgeMaxKey),
                    Children = loader.Get(ChildrenKey),
                    ChildAgeMin = loader.Get(ChildAgeMinKey),
                    ChildAgeMax = loader.Get(ChildAgeMaxKey),
                    Food = loader.Get(FoodKey),
                    Water = loader.Get(WaterKey),
                };
            }
            Apply("loaded from the save");
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            // A shared-colony game saves nothing, so its save is the same as before this mode existed.
            if (!enabled) return;
            IObjectSaver saver = singletonSaver.GetSingleton(ColonyModeKey);
            saver.Set(EnabledKey, enabled);
            saver.Set(StartsKey, starts);
            saver.Set(AwaitingFoundingKey, AwaitingFounding);
            if (FirstColonyStart.HasValue) saver.Set(FirstStartKey, FirstColonyStart.Value);
            if (StartingSettings != null)
            {
                saver.Set(AdultsKey, StartingSettings.Adults);
                saver.Set(AdultAgeMinKey, StartingSettings.AdultAgeMin);
                saver.Set(AdultAgeMaxKey, StartingSettings.AdultAgeMax);
                saver.Set(ChildrenKey, StartingSettings.Children);
                saver.Set(ChildAgeMinKey, StartingSettings.ChildAgeMin);
                saver.Set(ChildAgeMaxKey, StartingSettings.ChildAgeMax);
                saver.Set(FoodKey, StartingSettings.Food);
                saver.Set(WaterKey, StartingSettings.Water);
            }
        }

        /// <summary>
        /// Turns the mode on for a new game with two or more starts. Called by the multi-start initializer before the
        /// first starting building is placed, because new district centers read the mode for their trade defaults.
        /// </summary>
        public void Activate(IEnumerable<Vector3Int> startCoordinates, ColonyStartingSettings startingSettings)
        {
            enabled = true;
            AwaitingFounding = false;
            starts = startCoordinates.ToList();
            StartingSettings = startingSettings;
            Apply("switched on for this new game");
        }

        /// <summary>
        /// Turns the mode on for a new game with one start: the host's colony starts as usual and colony 2 is founded
        /// later by its player (see ColonyFoundingService). Until then nothing is divided.
        /// </summary>
        public void ActivateAwaitingFounding(ColonyStartingSettings startingSettings)
        {
            enabled = true;
            AwaitingFounding = true;
            starts = new List<Vector3Int>();
            StartingSettings = startingSettings;
            Apply("switched on for this new game");
        }

        /// <summary>
        /// The game placed (or moved, with its relocate option) colony 1's starting building. Only kept while colony 2
        /// is awaited: that is the district center colony 1's land is measured from.
        /// </summary>
        public void RecordFirstColonyStart(Vector3Int coordinates)
        {
            if (!AwaitingFounding) return;
            FirstColonyStart = coordinates;
            Plugin.Log($"[Colony] Colony 1 starts at {coordinates}");
        }

        /// <summary>Colony 2 is founded: the land is divided between the two district centers from now on.</summary>
        public void CompleteFounding(Vector3Int firstStart, Vector3Int secondStart, ColonyStartingSettings startingSettings)
        {
            // Also for a game that was never a separate-colonies game: founding makes it one.
            enabled = true;
            StartingSettings ??= startingSettings;
            AwaitingFounding = false;
            starts = new List<Vector3Int> { firstStart, secondStart };
            Apply("completed by founding colony 2");
        }

        private void Apply(string how)
        {
            if (enabled && AwaitingFounding)
            {
                Territory = null;
                Plugin.Log($"[Colony] Separate colonies {how}: one colony, waiting for colony 2 to be founded " +
                           $"(it will start with {StartingSettings?.ToString() ?? "no recorded starting settings"})");
                return;
            }
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
