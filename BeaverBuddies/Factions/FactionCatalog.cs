using BeaverBuddies.IO;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Timberborn.BlueprintSystem;
using Timberborn.Bots;
using Timberborn.FactionSystem;
using Timberborn.GameFactionSystem;
using Timberborn.GoodCollectionSystem;
using Timberborn.ModularShafts;
using Timberborn.NeedCollectionSystem;
using Timberborn.NeedSpecs;
using Timberborn.SingletonSystem;
using Timberborn.TemplateCollectionSystem;
using Timberborn.TemplateSystem;
using Timberborn.WorkerOutfitSystem;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// What each faction has in a mixed game, from the game's own collections: which templates (buildings, characters,
    /// plants) are one faction's alone, which goods and needs a faction has, and each faction's district center, bot,
    /// power-shaft parts, Trading Post and worker outfits. Static data, the same on every computer (it comes from the same
    /// game and mod files, which the join handshake checks), so simulation rules may read it. Built once, when first
    /// asked; empty outside a mixed game.
    /// </summary>
    public class FactionCatalog : RegisteredSingleton, ILoadableSingleton, IPostLoadableSingleton
    {
        public const string CommonCollectionId = "Common";

        private readonly ISpecService _specService;
        private readonly TemplateCollectionService _templateCollectionService;
        private readonly FactionNeedService _factionNeedService;
        private readonly FactionUnlockingService _factionUnlockingService;

        private bool built;
        private bool failed;
        // Set once the game has loaded and the catalog still can't be read: no call tries again.
        private bool gaveUp;
        private FactionSets templates;
        private FactionSets goods;
        private FactionSets needs;
        private readonly Dictionary<Blueprint, string> blueprintFactions = new Dictionary<Blueprint, string>();
        private readonly Dictionary<string, Blueprint> bots = new Dictionary<string, Blueprint>();
        private readonly Dictionary<string, ModularShaftPartsSpec> shaftParts = new Dictionary<string, ModularShaftPartsSpec>();
        private readonly Dictionary<string, TemplateSpec> districtCenters = new Dictionary<string, TemplateSpec>();
        private readonly Dictionary<string, string> tradingPosts = new Dictionary<string, string>();
        private readonly Dictionary<string, WorkerOutfitSpec> outfits = new Dictionary<string, WorkerOutfitSpec>();
        private readonly Dictionary<(string, bool), ImmutableArray<NeedSpec>> needsFor = new Dictionary<(string, bool), ImmutableArray<NeedSpec>>();
        private readonly Dictionary<string, HashSet<string>> goodSets = new Dictionary<string, HashSet<string>>();

        public static FactionCatalog Instance => SingletonManager.GetSingleton<FactionCatalog>();

        public FactionCatalog(ISpecService specService, TemplateCollectionService templateCollectionService,
            FactionNeedService factionNeedService, FactionUnlockingService factionUnlockingService)
        {
            _factionUnlockingService = factionUnlockingService;
            _specService = specService;
            _templateCollectionService = templateCollectionService;
            _factionNeedService = factionNeedService;
        }

        public void Load()
        {
            if (!MixedFactions.IsOn) return;
            EnsureBuilt();
            // The host's unlocks decide which factions a colony may take (D1); a guest learns them from the host.
            if (!(EventIO.Get() is ClientEventIO))
            {
                try
                {
                    BeaverBuddies.Colonies.ColonySession.LatchHostFactions(MixedFactions.AllFactions
                        .Where(f => !_factionUnlockingService.IsLocked(f)).Select(f => f.Id));
                }
                catch (Exception error)
                {
                    Plugin.LogWarning("[Factions] Could not read this computer's faction unlocks (every faction allowed): " + error.Message);
                }
            }
        }

        public void PostLoad()
        {
            if (!MixedFactions.IsOn) return;
            // Everything the catalog reads has loaded by now. One that still can't be read never will be: from here on
            // every answer falls back at once, instead of trying (and throwing and catching) again on each call, which the
            // game makes for every character, yield and building.
            if (!EnsureBuilt()) gaveUp = true;
        }

        /// <summary>The faction whose collections alone list this template (null: common, or not a mixed game).</summary>
        public string FactionOfTemplate(string templateName)
        {
            if (!MixedFactions.IsOn || string.IsNullOrEmpty(templateName) || !EnsureBuilt()) return null;
            return templates.SoleFaction(templateName);
        }

        /// <summary>The faction whose collections alone list this blueprint (a template without a TemplateSpec included).</summary>
        public string FactionOfBlueprint(Blueprint blueprint)
        {
            if (!MixedFactions.IsOn || blueprint == null || !EnsureBuilt()) return null;
            return blueprintFactions.TryGetValue(blueprint, out string faction) ? faction : null;
        }

        /// <summary>A faction has this good: common, or in its own collections.</summary>
        public bool HasGood(string faction, string goodId)
        {
            if (!MixedFactions.IsOn || !EnsureBuilt()) return true;
            return goods.Has(faction ?? MixedFactions.BaseFaction, goodId);
        }

        /// <summary>
        /// A good of the common collections (logs, berries, scrap metal: most of what yields give): every faction has it,
        /// so whose building asks does not matter. False for any other good, and outside a mixed game.
        /// </summary>
        public bool IsCommonGood(string goodId)
        {
            if (!MixedFactions.IsOn || !EnsureBuilt()) return false;
            return goods.InCommon(goodId);
        }

        /// <summary>The goods a faction has, as a set (for filters on hot paths); null when the catalog could not be read.</summary>
        public HashSet<string> GoodsOf(string faction)
        {
            if (!EnsureBuilt()) return null;
            faction ??= MixedFactions.BaseFaction;
            if (!goodSets.TryGetValue(faction, out HashSet<string> set))
                goodSets[faction] = set = new HashSet<string>(goods.ItemsOf(faction), StringComparer.Ordinal);
            return set;
        }

        /// <summary>The goods two factions both have (what may cross a Trading Post between them).</summary>
        public IEnumerable<string> SharedGoods(string a, string b)
        {
            if (!EnsureBuilt()) return Enumerable.Empty<string>();
            return goods.Shared(a, b);
        }

        /// <summary>A faction has this need: common, or in its own collections.</summary>
        public bool HasNeed(string faction, string needId)
        {
            if (!MixedFactions.IsOn || !EnsureBuilt()) return true;
            return needs.Has(faction ?? MixedFactions.BaseFaction, needId);
        }

        /// <summary>
        /// A character's needs: the game's loaded needs (already scaled by the difficulty) that its faction has, for a bot
        /// or a beaver, in the game's order. The same list vanilla gives a character of that faction. If the catalog could
        /// not be read (logged once), the game's own list: every faction's needs, as the game would give them.
        /// </summary>
        public ImmutableArray<NeedSpec> NeedsFor(string faction, bool bot)
        {
            if (!EnsureBuilt()) return (bot ? _factionNeedService.GetBotNeeds() : _factionNeedService.GetBeaverNeeds()).ToImmutableArray();
            faction ??= MixedFactions.BaseFaction;
            if (needsFor.TryGetValue((faction, bot), out ImmutableArray<NeedSpec> list)) return list;
            IEnumerable<NeedSpec> source = bot ? _factionNeedService.GetBotNeeds() : _factionNeedService.GetBeaverNeeds();
            list = source.Where(need => needs.Has(faction, need.Id)).ToImmutableArray();
            needsFor[(faction, bot)] = list;
            return list;
        }

        /// <summary>A faction's bot template (null if it has none).</summary>
        public Blueprint BotOf(string faction)
        {
            EnsureBuilt();
            return faction != null && bots.TryGetValue(faction, out Blueprint bot) ? bot : null;
        }

        /// <summary>A faction's power-shaft parts (null if it has none).</summary>
        public ModularShaftPartsSpec ShaftPartsOf(string faction)
        {
            EnsureBuilt();
            return faction != null && shaftParts.TryGetValue(faction, out ModularShaftPartsSpec parts) ? parts : null;
        }

        /// <summary>A faction's district center (its starting building).</summary>
        public TemplateSpec DistrictCenterOf(string faction)
        {
            EnsureBuilt();
            return faction != null && districtCenters.TryGetValue(faction, out TemplateSpec spec) ? spec : null;
        }

        /// <summary>A faction's Trading Post template name (null if the mod has none for it).</summary>
        public string TradingPostOf(string faction)
        {
            EnsureBuilt();
            return faction != null && tradingPosts.TryGetValue(faction, out string name) ? name : null;
        }

        /// <summary>A worker outfit of one faction (as the game keys them: id and worker type).</summary>
        public bool TryGetOutfit(string faction, string outfitId, string workerType, out WorkerOutfitSpec spec)
        {
            EnsureBuilt();
            return outfits.TryGetValue(OutfitKey(faction, outfitId, workerType), out spec);
        }

        private static string OutfitKey(string faction, string id, string workerType) => faction + "|" + id + "|" + workerType;

        /// <summary>
        /// Whether the catalog could be read. Kept unbuilt on a failure, so a later call tries again (the first callers are
        /// other services' Load), until the game has loaded (PostLoad); the error is logged once. Every answer above then
        /// falls back to the game's own (no faction filter) instead of throwing: a character is made, a stockpile filled,
        /// a yield taken and a building placed as the game would, inside a tick or a replay.
        /// </summary>
        private bool EnsureBuilt()
        {
            if (built) return true;
            if (gaveUp) return false;
            try
            {
                Build();
                built = true;
            }
            catch (Exception error)
            {
                if (!failed) Plugin.LogError("[Factions] Could not read the factions' collections: " + error);
                failed = true;
            }
            return built;
        }

        private void Build()
        {
            ImmutableArray<FactionSpec> factions = MixedFactions.AllFactions;
            List<string> order = factions.Select(f => f.Id).ToList();

            // Templates: each faction's template collections, by blueprint and by template name.
            Dictionary<string, List<Blueprint>> blueprintsByCollection = _specService.GetSpecs<TemplateCollectionSpec>()
                .GroupBy(spec => spec.CollectionId)
                .ToDictionary(group => group.Key, group => group.SelectMany(spec => spec.Blueprints)
                    .Select(asset => TryGetBlueprint(asset.Path)).Where(b => b != null).ToList());
            var namesByFaction = new Dictionary<string, IEnumerable<string>>();
            var listedBy = new Dictionary<Blueprint, List<string>>();
            foreach (FactionSpec faction in factions)
            {
                List<Blueprint> own = faction.TemplateCollectionIds
                    .SelectMany(id => blueprintsByCollection.TryGetValue(id, out List<Blueprint> list) ? list : new List<Blueprint>())
                    .Distinct().ToList();
                foreach (Blueprint blueprint in own)
                {
                    if (!listedBy.TryGetValue(blueprint, out List<string> by)) listedBy[blueprint] = by = new List<string>();
                    by.Add(faction.Id);
                }
                namesByFaction[faction.Id] = own.Select(b => b.GetSpec<TemplateSpec>()?.TemplateName).Where(n => n != null).ToList();
            }
            List<string> commonNames = (blueprintsByCollection.TryGetValue(CommonCollectionId, out List<Blueprint> common) ? common : new List<Blueprint>())
                .Select(b => b.GetSpec<TemplateSpec>()?.TemplateName).Where(n => n != null).ToList();
            templates = new FactionSets(order, commonNames, namesByFaction);
            foreach (KeyValuePair<Blueprint, List<string>> entry in listedBy)
            {
                if (entry.Value.Count == 1) blueprintFactions[entry.Key] = entry.Value[0];
            }

            // Goods and needs: the common collection plus each faction's.
            goods = FactionSets.FromCollections(order, new[] { CommonCollectionId },
                factions.ToDictionary(f => f.Id, f => (IEnumerable<string>)f.GoodCollectionIds),
                _specService.GetSpecs<GoodCollectionSpec>().GroupBy(s => s.CollectionId)
                    .ToDictionary(g => g.Key, g => (IEnumerable<string>)g.SelectMany(s => s.Goods).ToList()));
            needs = FactionSets.FromCollections(order, new[] { CommonCollectionId },
                factions.ToDictionary(f => f.Id, f => (IEnumerable<string>)f.NeedCollectionIds),
                _specService.GetSpecs<NeedCollectionSpec>().GroupBy(s => s.CollectionId)
                    .ToDictionary(g => g.Key, g => (IEnumerable<string>)g.SelectMany(s => s.Needs).ToList()));

            // Each faction's own pieces, found among the templates the game loaded (by name here, not through
            // TemplateNameMapper, which may not have loaded yet when the first caller asks).
            var byName = new Dictionary<string, TemplateSpec>();
            foreach (Blueprint blueprint in _templateCollectionService.AllTemplates)
            {
                TemplateSpec template = blueprint.GetSpec<TemplateSpec>();
                if (template != null && !byName.ContainsKey(template.TemplateName)) byName[template.TemplateName] = template;
            }
            foreach (Blueprint blueprint in _templateCollectionService.AllTemplates)
            {
                if (!blueprintFactions.TryGetValue(blueprint, out string faction)) continue;
                if (blueprint.HasSpec<BotSpec>() && !bots.ContainsKey(faction)) bots[faction] = blueprint;
                if (blueprint.HasSpec<ModularShaftPartsSpec>() && !shaftParts.ContainsKey(faction))
                    shaftParts[faction] = blueprint.GetSpec<ModularShaftPartsSpec>();
                if (blueprint.HasSpec<BeaverBuddies.Colonies.MultiColonyTradingPostSpec>() && !tradingPosts.ContainsKey(faction))
                    tradingPosts[faction] = blueprint.GetSpec<TemplateSpec>()?.TemplateName;
            }
            foreach (FactionSpec faction in factions)
            {
                if (faction.StartingBuildingId != null && byName.TryGetValue(faction.StartingBuildingId, out TemplateSpec center))
                    districtCenters[faction.Id] = center;
            }
            foreach (WorkerOutfitSpec outfit in _specService.GetSpecs<WorkerOutfitSpec>())
            {
                string key = OutfitKey(outfit.FactionId, outfit.Id, outfit.WorkerType);
                if (!outfits.ContainsKey(key)) outfits[key] = outfit;
            }

            Plugin.Log($"[Factions] Catalog: {order.Count} factions ({string.Join(", ", order)}); " +
                $"{listedBy.Count} faction templates ({listedBy.Count(e => e.Value.Count > 1)} shared by several); " +
                $"bots {bots.Count}, shaft parts {shaftParts.Count}, district centers {districtCenters.Count}, " +
                $"Trading Posts {tradingPosts.Count}, outfits {outfits.Count}; goods shared by all: " +
                (order.Count > 1 ? goods.Shared(order[0], order[1]).Count().ToString() : "-"));
        }

        private Blueprint TryGetBlueprint(string path)
        {
            try { return _specService.GetBlueprint(path); }
            catch (Exception) { return null; }
        }
    }
}
