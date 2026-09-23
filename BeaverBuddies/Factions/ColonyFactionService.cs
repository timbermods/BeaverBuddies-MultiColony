using BeaverBuddies.Colonies;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.Bots;
using Timberborn.FactionSystem;
using Timberborn.GameDistricts;
using Timberborn.GameSceneLoading;
using Timberborn.Persistence;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// Each colony's faction in a mixed game (D11), saved with the game and the same on every computer: written when a
    /// colony gets its district center (a new game's starts, a founding, a switch) and only in a replay or in the new
    /// game's scene before its first save. Also the one place that says which faction anything is (D12).
    /// </summary>
    public class ColonyFactionService : RegisteredSingleton, ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton,
        IResettableSingleton
    {
        private readonly ISingletonLoader _singletonLoader;
        private readonly ISceneLoader _sceneLoader;

        // Static: read on hot paths (needs, toolbar, trading) and by patches with no service at hand.
        private static FactionTable table = new FactionTable();

        /// <summary>Raised on every computer when a colony's faction is set (display refreshes).</summary>
        public static event Action<int> Changed;

        public static ColonyFactionService Instance => SingletonManager.GetSingleton<ColonyFactionService>();

        // FactionService: loaded first, and with it the decision whether this game is mixed (MixedFactions.Decide).
        public ColonyFactionService(ISingletonLoader singletonLoader, ISceneLoader sceneLoader, Timberborn.GameFactionSystem.FactionService factionService)
        {
            _singletonLoader = singletonLoader;
            _sceneLoader = sceneLoader;
        }

        public void Reset()
        {
            table = new FactionTable();
            Changed = null;
        }

        public void Load()
        {
            table = new FactionTable();
            if (!MixedFactions.IsOn)
            {
                // A pick from a waiting room means nothing in a game that is not mixed.
                LocalFactionPick.Clear();
                return;
            }
            bool newGame = _sceneLoader.TryGetSceneParameters(out GameSceneParameters parameters) && parameters.NewGame;
            if (newGame)
            {
                // The host's first colony is the base faction; a multi-start map's other starts are written as they are placed.
                table.Set(0, MixedFactions.BaseFaction);
            }
            else table = MixedFactions.LoadedTable.Copy();
            Plugin.Log($"[Factions] Colonies: {table}");
        }

        public void PostLoad()
        {
            string locked = NewGameFactionCapture.NoticeLockedFaction;
            if (locked == null) return;
            NewGameFactionCapture.NoticeLockedFaction = null;
            ColonyRulesService rules = SingletonManager.GetSingleton<ColonyRulesService>();
            rules?.ShowNotice(RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.NotUnlocked", locked), warning: false);
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            if (!MixedFactions.IsOn) return;
            IObjectSaver saver = singletonSaver.GetSingleton(MixedFactions.SaveKey);
            saver.Set(MixedFactions.MixedKey, true);
            saver.Set(MixedFactions.BaseKey, MixedFactions.BaseFaction ?? "");
            saver.Set(MixedFactions.ColoniesKey, table.Encode());
        }

        /// <summary>A colony's faction: its own once it has had a district center, else the base faction.</summary>
        public static string FactionOfSlot(int slot) =>
            MixedFactions.IsOn ? table.Of(slot) ?? MixedFactions.BaseFaction : MixedFactions.BaseFaction;

        /// <summary>Whether a colony has had a district center of a recorded faction.</summary>
        public static bool HasFaction(int slot) => MixedFactions.IsOn && table.Has(slot);

        /// <summary>Display only: the faction of the colony this computer acts as (a steward's included).</summary>
        public static string LocalFaction => FactionOfSlot(ColonyScienceService.DisplaySlot);

        /// <summary>For the daily check.</summary>
        public static string Fingerprint() => MixedFactions.IsOn ? table.Fingerprint() : "";

        /// <summary>
        /// For the daily check (a mixed game only): each colony's beavers and bots by faction, from its districts'
        /// populations, as "0=Folktails:31+4,1=IronTeeth:40+6" (slot, faction, beavers + bots). Saved state only, the same
        /// on every computer. The chars hash beside it catches a character made in the wrong faction on one computer; this
        /// says which colony and faction, and in a long game's log shows each colony's make-up day by day.
        /// </summary>
        public static string Census(IEnumerable<DistrictCenter> districtCenters)
        {
            if (!MixedFactions.IsOn || districtCenters == null) return "";
            ImmutableArray<FactionSpec> factions = MixedFactions.AllFactions;
            // The last faction index is for a character of no known faction.
            var counts = new int[FactionTable.MaxSlots, factions.Length + 1, 2];
            foreach (DistrictCenter center in districtCenters)
            {
                int slot = DistrictOwner.OwnerOfDistrict(center) ?? -1;
                DistrictPopulation population = center ? center.GetComponent<DistrictPopulation>() : null;
                if (slot < 0 || slot >= FactionTable.MaxSlots || population == null) continue;
                foreach (Beaver beaver in population.Beavers) counts[slot, IndexOf(factions, SimFactionOf(beaver)), 0]++;
                foreach (Bot bot in population.Bots) counts[slot, IndexOf(factions, SimFactionOf(bot)), 1]++;
            }
            var census = new StringBuilder();
            for (int slot = 0; slot < FactionTable.MaxSlots; slot++)
            {
                for (int f = 0; f <= factions.Length; f++)
                {
                    if (counts[slot, f, 0] == 0 && counts[slot, f, 1] == 0) continue;
                    if (census.Length > 0) census.Append(',');
                    census.Append(slot).Append('=').Append(f < factions.Length ? factions[f].Id : "?")
                        .Append(':').Append(counts[slot, f, 0]).Append('+').Append(counts[slot, f, 1]);
                }
            }
            return census.ToString();
        }

        private static int IndexOf(ImmutableArray<FactionSpec> factions, string faction)
        {
            for (int i = 0; i < factions.Length; i++)
            {
                if (factions[i].Id == faction) return i;
            }
            return factions.Length;
        }

        /// <summary>
        /// Records a colony's faction. Called in a replay (founding, switch) or while a new game places its starts, so the
        /// same on every computer; the digest counts it when a tick or replay is under way.
        /// </summary>
        public static void Set(int slot, string faction)
        {
            if (!MixedFactions.IsOn || string.IsNullOrEmpty(faction) || table.Of(slot) == faction) return;
            table.Set(slot, faction);
            ColonyDigest.Note("faction", slot, ColonyDigest.Of(faction));
            Plugin.Log($"[Factions] Colony {slot + 1} plays {faction}");
            try { Changed?.Invoke(slot); }
            catch (Exception error) { Plugin.LogWarning("[Factions] Could not show a colony's new faction: " + error.Message); }
        }

        /// <summary>
        /// The faction of a character or building, for simulation: a beaver's own, else its template's if only one faction
        /// lists it. Null for anything common. Never depends on who owns it.
        /// </summary>
        public static string SimFactionOf(BaseComponent component)
        {
            if (!MixedFactions.IsOn || component == null) return null;
            CharacterFaction character = component.GetComponent<CharacterFaction>();
            if (character != null) return character.FactionId;
            return FactionCatalog.Instance?.FactionOfTemplate(component.GetComponent<TemplateSpec>()?.TemplateName);
        }

        /// <summary>
        /// Display only: the faction something looks like. Its own (character or template), else the faction of the colony
        /// that owns it (a path), else the base faction.
        /// </summary>
        public static string DisplayFactionOf(BaseComponent component)
        {
            if (!MixedFactions.IsOn) return MixedFactions.BaseFaction;
            string own = SimFactionOf(component);
            if (own != null) return own;
            int? owner = component == null ? null : DistrictOwner.OwnerOf(component, useConstructionDistrict: false);
            return owner.HasValue ? FactionOfSlot(owner.Value) : MixedFactions.BaseFaction;
        }

        /// <summary>The game's spec of a faction (the base faction's for an unknown id).</summary>
        public static FactionSpec Spec(string faction) => MixedFactions.Spec(faction);

        /// <summary>Every faction's id, in the game's order.</summary>
        public static IReadOnlyList<string> FactionIds => MixedFactions.AllFactions.Select(f => f.Id).ToList();
    }
}
