using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.DistributionSystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.ResourceCountingSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// What a trading post's panel can offer and ask for: the game's goods in the game's own groups, then science (with
    /// separate science) and beavers; each with its icon, its name and how much a colony has of it at a crossing.
    /// Display only.
    /// </summary>
    public class TradeItems
    {
        // The game's own icons for science (the top bar's) and for adult beavers (the population panel's).
        private const string ScienceIconPath = "Sprites/TopBar/Science";
        private const string BeaverIconPath = "UI/Images/Game/ico-adult";

        private readonly IGoodService _goodService;
        private readonly GoodsGroupSpecService _goodsGroupSpecService;
        private readonly ResourceCountingService _resourceCountingService;
        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private Sprite scienceIcon, beaverIcon;
        private bool specialIconsLoaded;
        private List<(Sprite icon, List<string> goods)> groups;

        public TradeItems(IGoodService goodService, GoodsGroupSpecService goodsGroupSpecService,
            ResourceCountingService resourceCountingService, DistrictCenterRegistry districtCenterRegistry)
        {
            _goodService = goodService;
            _goodsGroupSpecService = goodsGroupSpecService;
            _resourceCountingService = resourceCountingService;
            _districtCenterRegistry = districtCenterRegistry;
        }

        /// <summary>A good of this game, science (only when each colony has its own), or beavers.</summary>
        public bool IsOffered(string item) =>
            item == ExchangeTerms.Beavers || (item == ExchangeTerms.Science ? ColonyScienceService.IsEnabled : _goodService.HasGood(item));

        /// <summary>Science (with separate science) and beavers, in that order.</summary>
        public IEnumerable<string> SpecialItems()
        {
            if (ColonyScienceService.IsEnabled) yield return ExchangeTerms.Science;
            yield return ExchangeTerms.Beavers;
        }

        /// <summary>The game's good groups in the game's order, each with its icon and its goods in the game's order.</summary>
        public List<(Sprite icon, List<string> goods)> Groups()
        {
            if (groups != null) return groups;
            groups = new List<(Sprite, List<string>)>();
            var listed = new HashSet<string>();
            foreach (GoodGroupSpec group in _goodsGroupSpecService.GoodGroupSpecs.OrderBy(g => g.Order))
            {
                List<string> goods = _goodService.GetGoodsForGroup(group.Id).Where(listed.Add)
                    .OrderBy(g => _goodService.GetGood(g).GoodOrder).ToList();
                if (goods.Count > 0) groups.Add((SafeIcon(() => group.Icon.Asset), goods));
            }
            // A good in no group (another mod's) still belongs somewhere.
            List<string> rest = _goodService.Goods.Where(listed.Add).OrderBy(Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (rest.Count > 0) groups.Add((null, rest));
            return groups;
        }

        public Sprite IconOf(string item)
        {
            if (string.IsNullOrEmpty(item)) return null;
            if (ExchangeTerms.IsSpecial(item))
            {
                LoadSpecialIcons();
                return item == ExchangeTerms.Science ? scienceIcon : beaverIcon;
            }
            return _goodService.HasGood(item) ? SafeIcon(() => _goodService.GetGood(item).IconSmall.Value) : null;
        }

        public string Name(string item) =>
            string.IsNullOrEmpty(item) ? "" : ColonyExchangeService.Instance?.GoodName(item) ?? item;

        /// <summary>How much a colony has of an item at this crossing half's district: stock, science, or adult beavers.</summary>
        public int StockOf(DistrictCrossing half, int slot, string item)
        {
            if (string.IsNullOrEmpty(item)) return 0;
            if (item == ExchangeTerms.Science) return ColonyScienceService.Instance?.PointsOf(slot) ?? 0;
            DistrictCenter district = TradingPosts.DistrictOf(half);
            if (!district) return 0;
            if (item == ExchangeTerms.Beavers) return district.GetComponent<DistrictPopulation>()?.NumberOfAdults ?? 0;
            if (!_goodService.HasGood(item)) return 0;
            return _resourceCountingService.GetDistrictResourceCounter(district).GetResourceCount(item).AvailableStock;
        }

        /// <summary>How much a colony has of an item over all its districts: stock, science, or adult beavers.</summary>
        public int StockOfColony(int slot, string item)
        {
            if (string.IsNullOrEmpty(item) || slot < 0) return 0;
            if (item == ExchangeTerms.Science) return ColonyScienceService.Instance?.PointsOf(slot) ?? 0;
            bool beavers = item == ExchangeTerms.Beavers;
            if (!beavers && !_goodService.HasGood(item)) return 0;
            int total = 0;
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                if (DistrictOwner.OwnerOfDistrict(districtCenter) != slot) continue;
                if (beavers) total += districtCenter.GetComponent<DistrictPopulation>()?.NumberOfAdults ?? 0;
                else total += _resourceCountingService.GetDistrictResourceCounter(districtCenter).GetResourceCount(item).AvailableStock;
            }
            return total;
        }

        /// <summary>The good a colony has most of at a crossing half's district (a form's first choice), never another.</summary>
        public string MostStocked(DistrictCrossing half, int slot, string other, Func<string, bool> allowed = null)
        {
            string best = null;
            int bestStock = -1;
            foreach (var (_, goods) in Groups())
            {
                foreach (string good in goods)
                {
                    if (good == other || (allowed != null && !allowed(good))) continue;
                    int stock = StockOf(half, slot, good);
                    if (stock > bestStock)
                    {
                        best = good;
                        bestStock = stock;
                    }
                }
            }
            return best;
        }

        private void LoadSpecialIcons()
        {
            if (specialIconsLoaded) return;
            specialIconsLoaded = true;
            scienceIcon = SafeIcon(() => UnityEngine.Resources.Load<Sprite>(ScienceIconPath));
            beaverIcon = SafeIcon(() => UnityEngine.Resources.Load<Sprite>(BeaverIconPath));
        }

        private static Sprite SafeIcon(Func<Sprite> load)
        {
            try { return load(); }
            catch (Exception) { return null; }
        }
    }
}
