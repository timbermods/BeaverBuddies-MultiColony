using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>What a colony starts with, taken from the new-game settings so a colony founded later gets the same.</summary>
    [Serializable]
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
    /// The saved state of a separate-colonies game: whether it is one, and the new game's starting settings for a
    /// colony founded later. Who owns what is saved on each district center (DistrictOwner), who plays which colony
    /// in ColonySlotService. Travels to guests inside the save the host sends, so every computer has the same values.
    ///
    /// Saves from the land-split alphas (1.2.0-two-colony-alpha1 to 5) also carry the colonies' start positions; they
    /// are read once, to give those saves' district centers their owners, and never saved again.
    /// </summary>
    public class ColonyModeService : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton, IResettableSingleton
    {
        private static readonly SingletonKey ColonyModeKey = new SingletonKey("BeaverBuddies.ColonyMode");
        private static readonly PropertyKey<bool> EnabledKey = new PropertyKey<bool>("Enabled");
        private static readonly ListKey<Vector3Int> LegacyStartsKey = new ListKey<Vector3Int>("Starts");
        private static readonly PropertyKey<int> AdultsKey = new PropertyKey<int>("StartingAdults");
        private static readonly PropertyKey<float> AdultAgeMinKey = new PropertyKey<float>("StartingAdultAgeMin");
        private static readonly PropertyKey<float> AdultAgeMaxKey = new PropertyKey<float>("StartingAdultAgeMax");
        private static readonly PropertyKey<int> ChildrenKey = new PropertyKey<int>("StartingChildren");
        private static readonly PropertyKey<float> ChildAgeMinKey = new PropertyKey<float>("StartingChildAgeMin");
        private static readonly PropertyKey<float> ChildAgeMaxKey = new PropertyKey<float>("StartingChildAgeMax");
        private static readonly PropertyKey<int> FoodKey = new PropertyKey<int>("StartingFood");
        private static readonly PropertyKey<int> WaterKey = new PropertyKey<int>("StartingWater");

        private readonly ISingletonLoader _singletonLoader;

        // Asked from patches on the game's busiest paths (every beaver's working hours, every job search, every
        // preview block every frame), so it is a static read, not a lookup of this service. It follows the running
        // game's service: set as its save loads or the mode is switched on, cleared when a game is left
        // (SingletonManager.Reset) and when the next game makes its service.
        private static bool separateNow;
        private bool enabled;

        /// <summary>A separate-colonies game: district centers belong to players and the colony rules apply.</summary>
        public bool Enabled
        {
            get => enabled;
            private set
            {
                enabled = value;
                separateNow = value;
            }
        }

        /// <summary>The new game's starting settings, given to a colony founded later. Null if never recorded.</summary>
        public ColonyStartingSettings StartingSettings { get; private set; }

        /// <summary>A land-split alpha save's division, read only to give its district centers owners. Else null.</summary>
        public ColonyTerritory LegacyTerritory { get; private set; }

        public static ColonyModeService Instance => SingletonManager.GetSingleton<ColonyModeService>();

        /// <summary>True in a separate-colonies game (and never before a game is loaded).</summary>
        public static bool IsSeparateColonies => separateNow;

        public ColonyModeService(ISingletonLoader singletonLoader)
        {
            _singletonLoader = singletonLoader;
            separateNow = false;
        }

        public void Reset()
        {
            separateNow = false;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(ColonyModeKey, out IObjectLoader loader)) return;
            Enabled = loader.Has(EnabledKey) && loader.Get(EnabledKey);
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
            if (loader.Has(LegacyStartsKey))
            {
                var starts = loader.Get(LegacyStartsKey).Select(s => new ColonyTile(s.x, s.y)).ToList();
                if (starts.Count >= 2)
                {
                    LegacyTerritory = new ColonyTerritory(starts);
                    Plugin.Log($"[Colony] Save from a land-split alpha: district centers get owners from its {starts.Count} starts");
                }
            }
            Plugin.Log(Enabled ? "[Colony] Separate colonies: on in this save" : "[Colony] Separate colonies: off in this save");
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            // A shared-colony game saves only the new game's starting settings here, which the Stability Fork ignores: a
            // colony founded in it later (a player's split, or its host making it separate at Start) starts with the game's
            // difficulty, not the default one (1.4.0-rc5 review, B1). No other colony service saves anything in it (see
            // each Save).
            if (!Enabled && StartingSettings == null) return;
            IObjectSaver saver = singletonSaver.GetSingleton(ColonyModeKey);
            saver.Set(EnabledKey, Enabled);
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
        /// A new game's starting settings, whatever its mode (a shared game's, for a colony founded in it later). The first
        /// recorded are kept.
        /// </summary>
        public void RecordStartingSettings(ColonyStartingSettings startingSettings)
        {
            StartingSettings ??= startingSettings;
        }

        /// <summary>
        /// Turns separate colonies on: for a new game (before its first district center exists, which reads the mode
        /// for its trade defaults), or when a colony is founded in a shared game.
        /// </summary>
        /// <param name="separateScience">Also give each colony its own science and unlocks (fixed for the save).</param>
        /// <param name="newGame">A new game: every colony starts with the same unlocks. A shared game being split: the
        /// science and unlocks so far stay with the first colony.</param>
        public void Enable(ColonyStartingSettings startingSettings, string how, bool separateScience, bool newGame)
        {
            bool wasEnabled = Enabled;
            if (!Enabled) Plugin.Log($"[Colony] Separate colonies switched on: {how}");
            Enabled = true;
            StartingSettings ??= startingSettings;
            // Buildings are stamped with their colony from now on. A shared game being split: its buildings are all the
            // first colony's.
            if (!wasEnabled) ColonyStamps.Instance?.Begin(splittingSharedGame: !newGame);
            // Its marks too: the fields and forests the shared colony marked stay the first colony's (E-7).
            if (!wasEnabled && !newGame) ColonyMarks.Instance?.AdoptUnowned(0);
            if (!wasEnabled && separateScience) ColonyScienceService.Instance?.Enable(newGame);
            // Display: from now on the toolbar's locks follow the local player's colony (RefreshToolLocks did nothing before).
            if (!wasEnabled) ColonyScienceService.Instance?.RefreshToolLocks();
        }
    }
}
