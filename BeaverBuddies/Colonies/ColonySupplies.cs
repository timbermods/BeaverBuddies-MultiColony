using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.GoodsSampling;
using Timberborn.ResourceCountingSystem;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>One measure of a colony's food or water: how much it has and for how long.</summary>
    public readonly struct Supply
    {
        public readonly int Stock;
        public readonly float? Days;

        public Supply(int stock, float? days)
        {
            Stock = stock;
            Days = days;
        }
    }

    /// <summary>
    /// A colony's food and water at a glance, for the trading window: the stock in its districts and how many days it
    /// lasts at the rate the colony used yesterday (the game's own daily samples), so a partner can see when a gift
    /// would help. Display only: it reads the game's counters and changes nothing.
    /// </summary>
    public class ColonySupplies : RegisteredSingleton, ILoadableSingleton
    {
        private const string FoodGroup = "Food", WaterGroup = "Water";

        private readonly DistrictCenterRegistry _districtCenterRegistry;
        private readonly ResourceCountingService _resourceCountingService;
        private readonly IGoodService _goodService;
        private readonly GoodsGroupSpecService _goodsGroupSpecService;
        private readonly IDayNightCycle _dayNightCycle;
        private Sprite foodIcon, waterIcon;
        private bool iconsLoaded;

        public static ColonySupplies Instance => SingletonManager.GetSingleton<ColonySupplies>();

        public ColonySupplies(DistrictCenterRegistry districtCenterRegistry, ResourceCountingService resourceCountingService,
            IGoodService goodService, GoodsGroupSpecService goodsGroupSpecService, IDayNightCycle dayNightCycle)
        {
            _districtCenterRegistry = districtCenterRegistry;
            _resourceCountingService = resourceCountingService;
            _goodService = goodService;
            _goodsGroupSpecService = goodsGroupSpecService;
            _dayNightCycle = dayNightCycle;
        }

        public void Load() { }

        public Supply Food(int slot) => Measure(slot, FoodGroup);
        public Supply Water(int slot) => Measure(slot, WaterGroup);

        /// <summary>The top bar's own icons for food and water.</summary>
        public Sprite FoodIcon => Icons().food;
        public Sprite WaterIcon => Icons().water;

        private (Sprite food, Sprite water) Icons()
        {
            if (!iconsLoaded)
            {
                iconsLoaded = true;
                foodIcon = SafeIcon(FoodGroup);
                waterIcon = SafeIcon(WaterGroup);
            }
            return (foodIcon, waterIcon);
        }

        private Sprite SafeIcon(string group)
        {
            try { return _goodsGroupSpecService.GetSpec(group)?.Icon.Asset; }
            catch (Exception) { return null; }
        }

        private Supply Measure(int slot, string group)
        {
            int stock = 0, yesterday = 0, today = 0;
            List<string> goods;
            try { goods = _goodService.GetGoodsForGroup(group).ToList(); }
            catch (Exception) { return new Supply(0, null); }
            foreach (DistrictCenter districtCenter in _districtCenterRegistry.FinishedDistrictCenters)
            {
                if (DistrictOwner.OwnerOfDistrict(districtCenter) != slot) continue;
                DistrictResourceCounter counter = _resourceCountingService.GetDistrictResourceCounter(districtCenter);
                DistrictGoodSamplingRegistry samples = districtCenter.GetComponent<DistrictGoodSamplingRegistry>();
                DistrictGoodsBalance balance = districtCenter.GetComponent<DistrictGoodsBalance>();
                foreach (string good in goods)
                {
                    stock += counter.GetResourceCount(good).AvailableStock;
                    yesterday += LastSampleUse(samples, good);
                    today += balance != null ? Math.Max(0, balance.GetConsumption(good)) : 0;
                }
            }
            return new Supply(stock, SupplyDays.Estimate(stock, yesterday, today, _dayNightCycle.DayProgress));
        }

        // The game samples each district's goods at every daytime start: the last sample's consumption is a full day's.
        private static int LastSampleUse(DistrictGoodSamplingRegistry samples, string good)
        {
            try
            {
                GoodSamplingRegistry registry = samples?.GoodSamplingRegistry;
                GoodSampleHistory history = registry?.GetGoodSampleHistory(good);
                if (history == null) return 0;
                var list = history.GoodSamples;
                if (list.Count == 0) return 0;
                return Math.Max(0, list[list.Count - 1].Consumption);
            }
            catch (Exception) { return 0; }
        }
    }
}
