using BeaverBuddies.Colonies;
using BeaverBuddies.Lobby;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Timberborn.BlueprintSystem;
using Timberborn.FactionSystem;
using Timberborn.GameFactionSystem;
using Timberborn.GameSceneLoading;
using Timberborn.GoodCollectionSystem;
using Timberborn.NeedCollectionSystem;
using Timberborn.Persistence;
using Timberborn.TemplateCollectionSystem;
using Timberborn.TimbermeshMaterials;
using Timberborn.WorldPersistence;
using TimberNet;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// Whether this game is a mixed-factions game (each colony its own faction), decided once per game scene before any
    /// of the game's collections load: from the waiting room that is making a new world, from the solo New Game's Start,
    /// or from a save. A mixed game loads every faction's buildings, characters, goods, needs and materials; the game's
    /// own faction (FactionService.Current) stays the base faction, the host's pick, for whatever is not per colony.
    /// Nothing here changes a game that is not mixed.
    /// </summary>
    public static class MixedFactions
    {
        public static readonly SingletonKey SaveKey = new SingletonKey("BeaverBuddies.ColonyFactions");
        public static readonly PropertyKey<bool> MixedKey = new PropertyKey<bool>("Mixed");
        public static readonly PropertyKey<string> BaseKey = new PropertyKey<string>("Base");
        public static readonly ListKey<string> ColoniesKey = new ListKey<string>("Colonies");

        /// <summary>Each colony plays a faction of its own. Fixed for the scene (static: read on hot paths).</summary>
        public static bool IsOn { get; private set; }

        /// <summary>The game's own faction (FactionService.Current): the host's pick, and a new game's first colony.</summary>
        public static string BaseFaction { get; private set; }

        /// <summary>Every faction the game knows, in its order.</summary>
        public static ImmutableArray<FactionSpec> AllFactions { get; private set; } = ImmutableArray<FactionSpec>.Empty;

        /// <summary>The saved table of a loaded mixed game (colony → faction); empty for a new game.</summary>
        public static FactionTable LoadedTable { get; private set; } = new FactionTable();

        /// <summary>A new game's planned factions per slot (the waiting room's picks), for the starts it places.</summary>
        public static FactionTable PlannedTable { get; private set; } = new FactionTable();

        /// <summary>The faction the power shafts' models are being built for (FactionModelPatches); null for the base.</summary>
        internal static string ShaftBuildFaction;

        /// <summary>Why mixed factions can't run in this process (one of its patches no longer fits the game), or null.</summary>
        public static string Unavailable { get; internal set; }

        /// <summary>
        /// A scene is being set up: nothing is mixed until this scene's FactionService decides (a previous game's answer
        /// must never leak into what loads before it).
        /// </summary>
        public static void Reset()
        {
            IsOn = false;
            BaseFaction = null;
            LoadedTable = new FactionTable();
            PlannedTable = new FactionTable();
            ShaftBuildFaction = null;
            // What an earlier game left behind: its faction icon (an old scene's UI), its power-shaft models and the
            // once-only warning. (The factions an earlier host allowed are forgotten when a session begins, not here: a
            // waiting room latches them at Start, and a slow guest's start message may be built after the host's game
            // scene is set up. See ColonySession.ForgetHostFactions.)
            FactionDisplay.Reset();
            FactionModels.Reset();
            FactionCreationContext.Reset();
            FactionAnimation.Reset();
        }

        internal static void Decide(FactionService service)
        {
            IsOn = false;
            BaseFaction = null;
            LoadedTable = new FactionTable();
            PlannedTable = new FactionTable();
            string how = "not decided";
            try
            {
                AllFactions = service._factionSpecService.Factions.OrderBy(f => f.Order).ToImmutableArray();
                if (Unavailable != null)
                {
                    how = "unavailable in this run: " + Unavailable;
                    return;
                }
                if (service._mapEditorMode.IsMapEditor)
                {
                    how = "the map editor";
                    return;
                }
                GameSceneParameters parameters = service._sceneLoader.GetSceneParameters<GameSceneParameters>();
                // A locked-faction notice is for the new game whose Start noted it, never a save loaded after it.
                if (!parameters.NewGame) NewGameFactionCapture.NoticeLockedFaction = null;
                if (parameters.NewGame)
                {
                    string baseFaction = parameters.NewGameConfiguration.FactionId;
                    LobbySession lobby = LobbySession.Current;
                    if (lobby != null && lobby.State == LobbySessionState.CreatingWorld && !lobby.Setup.IsSave)
                    {
                        // The room's own choice, taken from the Game Mode page when it opened (1.4.0-rc3).
                        IsOn = lobby.Setup.Mixed && lobby.Setup.Separate;
                        how = "a waiting room's new world";
                        if (IsOn)
                        {
                            PlannedTable.Set(0, baseFaction);
                            // The same seating LobbyWorldMaker writes into the colony slot table (LobbyRules.SeatingPlan).
                            IReadOnlyList<LobbyMemberInfo> guests = lobby.StartedWith;
                            List<int?> slots = LobbyRules.SeatingPlan(LocalPlayerIdentity.Id, guests.Select(g => g.StableId).ToList());
                            for (int i = 0; i < guests.Count; i++)
                            {
                                if (slots[i] is int slot && slot > 0)
                                    PlannedTable.Set(slot, guests[i].SaidHello && IsKnown(guests[i].Faction) ? guests[i].Faction : baseFaction);
                            }
                        }
                    }
                    else
                    {
                        IsOn = NewGameFactionCapture.Take(baseFaction);
                        how = "a new game";
                    }
                }
                else if (service._singletonLoader.TryGetSingleton(SaveKey, out IObjectLoader loader) && loader.Has(MixedKey))
                {
                    IsOn = loader.Get(MixedKey);
                    if (IsOn && loader.Has(ColoniesKey)) LoadedTable = FactionTable.Decode(loader.Get(ColoniesKey));
                    how = "the save";
                }
                else how = "the save (not mixed)";
                if (IsOn && AllFactions.Length > 2)
                    // A save that is already mixed keeps its mode: loaded as one faction, it would lose the other's buildings.
                    Plugin.LogWarning($"[Factions] This mixed game now has {AllFactions.Length} factions (a faction mod?); mixed factions is only checked with two");
                if (IsOn && AllFactions.Length < 2)
                {
                    Plugin.LogWarning("[Factions] Mixed factions asked for, but the game has only one faction");
                    IsOn = false;
                }
            }
            catch (Exception error)
            {
                Plugin.LogError("[Factions] Could not decide whether this is a mixed-factions game (treated as not): " + error);
                IsOn = false;
            }
            finally
            {
                NewGameFactionCapture.Clear();
                Plugin.Log($"[Factions] Mixed factions: {(IsOn ? "on" : "off")} (from {how})");
            }
        }

        internal static bool IsKnown(string factionId) => !string.IsNullOrEmpty(factionId) && Find(factionId) != null;

        /// <summary>
        /// A faction's spec, else the base faction's. Asked for every beaver made (its fur), every path painted and every
        /// avatar shown: a plain loop over the two or three factions, with no closure or enumerator made per call.
        /// </summary>
        internal static FactionSpec Spec(string factionId) => Find(factionId) ?? Find(BaseFaction);

        private static FactionSpec Find(string factionId)
        {
            if (factionId == null) return null;
            ImmutableArray<FactionSpec> factions = AllFactions;
            for (int i = 0; i < factions.Length; i++)
            {
                if (factions[i].Id == factionId) return factions[i];
            }
            return null;
        }

        /// <summary>The base faction is known once FactionService has loaded.</summary>
        internal static void SetBase(string factionId)
        {
            BaseFaction = factionId;
            if (!IsOn) return;
            Plugin.Log($"[Factions] Base faction {factionId}; planned {PlannedTable}; saved {LoadedTable}");
        }

        /// <summary>The other factions' collections of one kind, in the game's order (for the union's providers).</summary>
        internal static IEnumerable<string> OtherFactionIds(Func<FactionSpec, IEnumerable<string>> ids)
        {
            if (!IsOn) return Enumerable.Empty<string>();
            return AllFactions.Where(f => f.Id != BaseFaction).SelectMany(ids).ToList();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, GameFactionSystem: FactionService.Load
        if (_singletonLoader.TryGetSingleton(FactionServiceKey, out var objectLoader)) { SetCurrentFaction(objectLoader.Get(IdKey)); return; }
        GameSceneParameters sceneParameters = _sceneLoader.GetSceneParameters<GameSceneParameters>();
        SetCurrentFaction(sceneParameters.NewGameConfiguration.FactionId);
     * Every faction-scoped collection is read after this (its providers call Current), so the decision goes first.
     */
    [HarmonyPatch(typeof(FactionService), nameof(FactionService.Load))]
    static class MixedFactionsDecidePatcher
    {
        // Whitelisted in the gate check: this is where MixedFactions.IsOn is decided.
        static void Prefix(FactionService __instance) => MixedFactions.Decide(__instance);

        static void Postfix(FactionService __instance) => MixedFactions.SetBase(__instance.Current?.Id);
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, GameFactionSystem: FactionService.SetCurrentFaction
        _factionBlueprintModifierProvider.Initialize(Current.BlueprintModifiers);
     * Initialize may run once; in a mixed game it gets every faction's modifiers (none in the base game, but a mod's
     * faction modifiers must apply to that faction's buildings, which are loaded too).
     */
    [HarmonyPatch(typeof(FactionBlueprintModifierProvider), nameof(FactionBlueprintModifierProvider.Initialize))]
    static class MixedFactionsBlueprintModifierPatcher
    {
        static void Prefix(ref IEnumerable<BlueprintModifierSpec> modifiers)
        {
            if (!MixedFactions.IsOn) return;
            modifiers = MixedFactions.AllFactions.SelectMany(f => f.BlueprintModifiers).Distinct().ToList();
        }
    }

    /// <summary>
    /// The union of every faction's collections in a mixed game: the other factions' ids, next to the game's own provider
    /// of the base faction's (the game unions every provider). Nothing in a game that is not mixed.
    /// </summary>
    public class OtherFactionCollections : ITemplateCollectionIdProvider, IGoodCollectionIdsProvider, INeedCollectionIdsProvider,
        IMaterialCollectionIdsProvider
    {
        // FactionService loads before any collection (the game's own providers read Current), and with it the decision.
        private readonly FactionService _factionService;

        public OtherFactionCollections(FactionService factionService)
        {
            _factionService = factionService;
        }

        public IEnumerable<string> GetTemplateCollectionIds() => MixedFactions.OtherFactionIds(f => f.TemplateCollectionIds);

        public IEnumerable<string> GetGoodCollectionIds() => MixedFactions.OtherFactionIds(f => f.GoodCollectionIds);

        public IEnumerable<string> GetNeedCollectionIds() => MixedFactions.OtherFactionIds(f => f.NeedCollectionIds);

        public IEnumerable<string> GetMaterialCollectionIds() => MixedFactions.OtherFactionIds(f => f.MaterialCollectionIds);
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, TemplateCollectionSystem: TemplateCollectionService.Load
        foreach provider, foreach collectionId: list.AddRange(... spec.Blueprints ... select _specService.GetBlueprint(asset.Path));
        AllTemplates = list.ToImmutableArray();
     * No de-duplication: both factions list BeaverAdult, BeaverChild and the two dev buildings, which would then be
     * registered twice (TemplateNameMapper throws, BeaverFactory's GetSingle throws). GetBlueprint caches one Blueprint per
     * path, so the same blueprint is the same object.
     */
    [HarmonyPatch(typeof(TemplateCollectionService), nameof(TemplateCollectionService.Load))]
    static class MixedFactionsTemplateDedupPatcher
    {
        static void Postfix(TemplateCollectionService __instance)
        {
            if (!MixedFactions.IsOn) return;
            int before = __instance.AllTemplates.Length;
            __instance.AllTemplates = __instance.AllTemplates.Distinct().ToImmutableArray();
            Plugin.Log($"[Factions] Templates of every faction: {__instance.AllTemplates.Length} ({before - __instance.AllTemplates.Length} shared)");
        }
    }
}
