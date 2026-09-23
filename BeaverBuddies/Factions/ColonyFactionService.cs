using BeaverBuddies.Colonies;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.FactionSystem;
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

        public ColonyFactionService(ISingletonLoader singletonLoader, ISceneLoader sceneLoader)
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
            if (!MixedFactions.IsOn) return;
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
