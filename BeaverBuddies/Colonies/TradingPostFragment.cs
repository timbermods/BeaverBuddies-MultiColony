using BeaverBuddies.Editor;
using BeaverBuddies.Events;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.DistributionSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.ResourceCountingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The trading post's panel, under a District Crossing that joins two players' colonies: who trades here, what the
    /// partner could use (with a button to give some), what has passed each way, and a science gift. Display and
    /// buttons only; each button sends an ordinary action that every computer plays.
    /// </summary>
    public class TradingPostFragment : IEntityPanelFragment
    {
        private const int GiftAmount = 10;
        private static readonly int[] ScienceGifts = { 50, 250 };
        private static readonly Color Ink = new Color(0.91f, 0.88f, 0.81f);

        private readonly GoodService _goodService;
        private readonly ResourceCountingService _resourceCountingService;

        private VisualElement root;
        private Label title, between, needsTitle, giftState, sent, received, scienceTitle, viewOnly;
        private VisualElement needsList, scienceRow;
        private DistrictCrossing crossing;
        private float nextRefresh;

        public TradingPostFragment(GoodService goodService, ResourceCountingService resourceCountingService)
        {
            _goodService = goodService;
            _resourceCountingService = resourceCountingService;
        }

        public VisualElement InitializeFragment()
        {
            root = new VisualElement();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            title = Text(14, bold: true);
            between = Text(12);
            needsTitle = Text(12, bold: true);
            needsList = new VisualElement();
            giftState = Text(12);
            sent = Text(12);
            received = Text(12);
            scienceTitle = Text(12, bold: true);
            scienceRow = new VisualElement();
            scienceRow.style.flexDirection = FlexDirection.Row;
            foreach (int amount in ScienceGifts)
            {
                int gift = amount;
                scienceRow.Add(MakeButton(string.Format(T("BeaverBuddies.Colony.Trade.Give"), gift), () => GiveScience(gift)));
            }
            viewOnly = Text(12);
            foreach (VisualElement element in new VisualElement[] { title, between, needsTitle, needsList, giftState, sent, received, scienceTitle, scienceRow, viewOnly })
                root.Add(element);
            root.style.display = DisplayStyle.None;
            return root;
        }

        public void ShowFragment(BaseComponent entity)
        {
            crossing = entity.GetComponent<DistrictCrossing>();
            nextRefresh = 0;
            Refresh();
        }

        public void ClearFragment()
        {
            crossing = null;
            if (root != null) root.style.display = DisplayStyle.None;
        }

        public void UpdateFragment()
        {
            if (!crossing) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                RefreshUnsafe();
            }
            catch (Exception error)
            {
                // A panel must never break the game; hide it and say why once in the log.
                Plugin.LogWarning("[Colony] Trading post panel: " + error.Message);
                root.style.display = DisplayStyle.None;
                crossing = null;
            }
        }

        private void RefreshUnsafe()
        {
            if (!crossing || !TradingPosts.IsTradingPost(crossing))
            {
                root.style.display = DisplayStyle.None;
                return;
            }
            root.style.display = DisplayStyle.Flex;
            DistrictCrossing partnerHalf = TradingPosts.Partner(crossing);
            int localSlot = ColonySession.LocalSlot;
            int thisOwner = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(crossing)) ?? 0;
            int otherOwner = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(partnerHalf)) ?? 0;
            // "Mine" is the half in the local player's colony, whichever half was clicked.
            DistrictCrossing myHalf = thisOwner == localSlot ? crossing : otherOwner == localSlot ? partnerHalf : null;
            DistrictCrossing theirHalf = myHalf == crossing ? partnerHalf : crossing;
            int me = myHalf ? localSlot : thisOwner;
            int them = me == thisOwner ? otherOwner : thisOwner;

            title.text = T("BeaverBuddies.Colony.Trade.Title");
            between.text = string.Format(T("BeaverBuddies.Colony.Trade.Between"), ColonyName(thisOwner), ColonyName(otherOwner));
            between.style.color = SlotColor(thisOwner);

            RefreshNeeds(theirHalf, them, myHalf != null);

            CrossingGift gift = myHalf ? myHalf.GetComponent<CrossingGift>() : null;
            giftState.text = gift != null && gift.IsActive
                ? string.Format(T("BeaverBuddies.Colony.Trade.GiftUnderWay"), gift.Remaining, GoodName(gift.GoodId))
                : "";
            giftState.style.display = giftState.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            ColonyTradeLedger ledger = ColonyTradeLedger.Instance;
            sent.text = string.Format(T("BeaverBuddies.Colony.Trade.Sent"), ColonyName(them), Summary(ledger?.Sent(me, them)));
            received.text = string.Format(T("BeaverBuddies.Colony.Trade.Received"), ColonyName(them), Summary(ledger?.Sent(them, me)));

            bool science = ColonyScienceService.IsEnabled && myHalf != null;
            scienceTitle.style.display = science ? DisplayStyle.Flex : DisplayStyle.None;
            scienceRow.style.display = science ? DisplayStyle.Flex : DisplayStyle.None;
            if (science)
                scienceTitle.text = string.Format(T("BeaverBuddies.Colony.Trade.Science"), ColonyName(them),
                    ColonyScienceService.Instance.PointsOf(me));

            viewOnly.text = myHalf ? "" : T("BeaverBuddies.Colony.Trade.ViewOnly");
            viewOnly.style.display = myHalf ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// What the partner could use: first the goods their district imports (Auto or Forced), least full first, then
        /// goods they store but have little of. Read from their settings and the game's per-district counters only;
        /// the import list the simulation uses is cached, and reading it from the interface could fill that cache at a
        /// moment only this computer sees.
        /// </summary>
        private void RefreshNeeds(DistrictCrossing theirHalf, int them, bool canGive)
        {
            needsList.Clear();
            DistrictCenter theirDistrict = TradingPosts.DistrictOf(theirHalf);
            var needs = new List<(string good, float fill)>();
            if (theirDistrict)
            {
                DistrictDistributionSetting settings = theirDistrict.GetComponent<DistrictDistributionSetting>();
                var counter = _resourceCountingService.GetDistrictResourceCounter(theirDistrict);
                var wanted = new List<(string good, float fill)>();
                var low = new List<(string good, float fill)>();
                foreach (string goodId in _goodService.Goods)
                {
                    ResourceCount count = counter.GetResourceCount(goodId);
                    ImportOption option = settings?.GetGoodDistributionSetting(goodId)?.ImportOption ?? ImportOption.Disabled;
                    if (option != ImportOption.Disabled) wanted.Add((goodId, count.FillRate));
                    else if (count.TotalCapacity > 0 && count.FillRate < 0.25f) low.Add((goodId, count.FillRate));
                }
                needs.AddRange(wanted.OrderBy(n => n.fill).ThenBy(n => n.good, StringComparer.Ordinal));
                needs.AddRange(low.OrderBy(n => n.fill).ThenBy(n => n.good, StringComparer.Ordinal));
            }
            needsTitle.text = needs.Count > 0
                ? string.Format(T("BeaverBuddies.Colony.Trade.Needs"), ColonyName(them))
                : string.Format(T("BeaverBuddies.Colony.Trade.NeedsNone"), ColonyName(them));
            foreach (var (good, fill) in needs.Take(5))
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                Label label = Text(12);
                label.text = $"{GoodName(good)} ({Mathf.RoundToInt(fill * 100)}%)";
                label.style.flexGrow = 1;
                row.Add(label);
                if (canGive)
                {
                    string goodId = good;
                    row.Add(MakeButton(string.Format(T("BeaverBuddies.Colony.Trade.Give"), GiftAmount), () => GiveGoods(goodId)));
                }
                needsList.Add(row);
            }
        }

        private void GiveGoods(string goodId)
        {
            if (!crossing) return;
            int localSlot = ColonySession.LocalSlot;
            DistrictCrossing partnerHalf = TradingPosts.Partner(crossing);
            DistrictCrossing myHalf = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(crossing)) == localSlot ? crossing
                : DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(partnerHalf)) == localSlot ? partnerHalf : null;
            if (!myHalf) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            ReplayEvent.DoPrefix(() => new GiftGoodsEvent { crossingID = halfId, goodId = goodId, amount = GiftAmount });
            nextRefresh = 0;
        }

        private void GiveScience(int amount)
        {
            if (!crossing) return;
            int localSlot = ColonySession.LocalSlot;
            int thisOwner = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(crossing)) ?? 0;
            int otherOwner = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(TradingPosts.Partner(crossing))) ?? 0;
            int them = thisOwner == localSlot ? otherOwner : thisOwner;
            if (them == localSlot) return;
            ReplayEvent.DoPrefix(() => new GiftScienceEvent { toSlot = them, amount = amount });
            nextRefresh = 0;
        }

        private string Summary(List<KeyValuePair<string, int>> goods)
        {
            if (goods == null || goods.Count == 0) return T("BeaverBuddies.Colony.Trade.Nothing");
            return string.Join(", ", goods.Take(4).Select(g => $"{g.Value} {GoodName(g.Key)}")) + (goods.Count > 4 ? ", …" : "");
        }

        private string GoodName(string goodId)
        {
            try { return _goodService.GetGood(goodId).PluralDisplayName.Value; }
            catch (Exception) { return goodId; }
        }

        private static string ColonyName(int slot)
        {
            string name = ColonySlotService.Instance?.Table.NameOf(slot);
            return string.IsNullOrEmpty(name) ? string.Format(T("BeaverBuddies.Colony.Trade.ColonyN"), slot + 1) : name;
        }

        private static Color SlotColor(int slot) =>
            slot >= 0 && slot < StartingLocationPlayer.PLAYER_COLORS.Length ? StartingLocationPlayer.PLAYER_COLORS[slot] : Ink;

        private static string T(string key) => RegisteredLocalizationService.T(key);

        private static Label Text(int size, bool bold = false)
        {
            var label = new Label();
            label.style.fontSize = size;
            label.style.color = Ink;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Button MakeButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.fontSize = 12;
            button.style.marginLeft = 4;
            return button;
        }
    }
}
