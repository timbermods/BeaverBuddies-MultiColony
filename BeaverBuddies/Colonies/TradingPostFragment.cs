using BeaverBuddies.Editor;
using BeaverBuddies.Events;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
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
    /// The trading post's panel, under a District Crossing that joins two players' colonies: who trades here, the
    /// exchange (offer one, answer one, or follow one under way), what has passed each way, and a science gift.
    /// Display and buttons only; each button sends an ordinary action that every computer plays.
    /// </summary>
    public class TradingPostFragment : IEntityPanelFragment
    {
        private static readonly int[] ScienceGifts = { 50, 250 };
        private static readonly Color Ink = new Color(0.91f, 0.88f, 0.81f);
        private static readonly Color Muted = new Color(0.72f, 0.69f, 0.62f);
        private static readonly Color Rule = new Color(0.45f, 0.42f, 0.35f);
        private static readonly Color CellBackground = new Color(0.05f, 0.05f, 0.04f, 0.85f);
        private static readonly Color CellEmpty = new Color(0.05f, 0.05f, 0.04f, 0.45f);

        private readonly GoodService _goodService;
        private readonly ResourceCountingService _resourceCountingService;
        private readonly VisualElementInitializer _visualElementInitializer;

        private VisualElement root;
        private Label title, between, exchangeTitle, exchangeText, progressMine, progressTheirs, waiting;
        private Label preview, formHint, totalsTitle, sent, received, scienceTitle, viewOnly;
        private VisualElement exchangeButtons, form, scienceRow, picker;
        private Button acceptButton, declineButton, withdrawButton, cancelButton, repeatButton;
        private Button giveItemButton, getItemButton;
        private Image giveIcon, getIcon;
        private TextField giveAmountField, getAmountField;
        private DistrictCrossing crossing;
        private float nextRefresh;

        // The offer being written: kept while the panel is open on other crossings too.
        private string giveGood, getGood;
        private bool repeat;
        // Which side's item grid is open: 0 none, 1 what you give, 2 what you ask for.
        private int pickerFor;

        // The exchange as last shown: Accept and Cancel act on exactly this, never on something that changed since.
        private int shownSerial;
        private string shownGiveGood, shownGetGood;
        private int shownGiveAmount, shownGetAmount;
        private bool shownRepeat;

        public TradingPostFragment(GoodService goodService, ResourceCountingService resourceCountingService,
            VisualElementInitializer visualElementInitializer)
        {
            _goodService = goodService;
            _resourceCountingService = resourceCountingService;
            _visualElementInitializer = visualElementInitializer;
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

            exchangeTitle = Text(12, bold: true);
            exchangeTitle.style.marginTop = 6;
            exchangeText = Text(12);
            progressMine = Text(12);
            progressTheirs = Text(12);
            waiting = Text(12, color: Muted);
            exchangeButtons = Row();
            acceptButton = MakeButton(T("BeaverBuddies.Colony.Trade.Accept"), Accept);
            declineButton = MakeButton(T("BeaverBuddies.Colony.Trade.Decline"), Cancel);
            withdrawButton = MakeButton(T("BeaverBuddies.Colony.Trade.Withdraw"), Cancel);
            cancelButton = MakeButton(T("BeaverBuddies.Colony.Trade.Cancel"), Cancel);
            foreach (Button button in new[] { acceptButton, declineButton, withdrawButton, cancelButton })
                exchangeButtons.Add(button);

            form = new VisualElement();
            giveAmountField = AmountField("100");
            getAmountField = AmountField("100");
            form.Add(OfferRow(T("BeaverBuddies.Colony.Trade.YouGive"), out giveIcon, out giveItemButton, giveAmountField, () => TogglePicker(1)));
            form.Add(OfferRow(T("BeaverBuddies.Colony.Trade.YouAsk"), out getIcon, out getItemButton, getAmountField, () => TogglePicker(2)));
            picker = new VisualElement();
            picker.style.flexDirection = FlexDirection.Row;
            picker.style.flexWrap = Wrap.Wrap;
            picker.style.marginTop = 2;
            picker.style.marginBottom = 2;
            picker.style.display = DisplayStyle.None;
            form.Add(picker);
            preview = Text(12);
            preview.style.marginTop = 2;
            form.Add(preview);
            var proposeRow = Row();
            proposeRow.Add(MakeButton(T("BeaverBuddies.Colony.Trade.Propose"), Propose));
            repeatButton = MakeButton("", ToggleRepeat);
            proposeRow.Add(repeatButton);
            form.Add(proposeRow);
            formHint = Text(11, color: Muted);
            formHint.style.marginTop = 2;
            form.Add(formHint);

            totalsTitle = Text(12, bold: true);
            totalsTitle.style.marginTop = 6;
            sent = Text(12);
            received = Text(12);
            scienceTitle = Text(12, bold: true);
            scienceTitle.style.marginTop = 6;
            scienceRow = Row();
            foreach (int amount in ScienceGifts)
            {
                int gift = amount;
                scienceRow.Add(MakeButton(string.Format(T("BeaverBuddies.Colony.Trade.Give"), gift), () => GiveScience(gift)));
            }
            viewOnly = Text(12, color: Muted);
            var overviewRow = Row();
            overviewRow.style.marginTop = 6;
            overviewRow.Add(MakeButton(T("BeaverBuddies.Colony.Trade.AllPosts"), OpenOverview));
            foreach (VisualElement element in new VisualElement[] { title, between, exchangeTitle, exchangeText, progressMine,
                progressTheirs, waiting, exchangeButtons, form, totalsTitle, sent, received, scienceTitle, scienceRow, viewOnly, overviewRow })
                root.Add(element);

            // The game's own setup: buttons that click with any modifier, and text boxes that switch the game's hotkeys
            // off while a player types in them.
            _visualElementInitializer.InitializeVisualElement(root);
            giveAmountField.RegisterValueChangedCallback(_ => RefreshPreview());
            getAmountField.RegisterValueChangedCallback(_ => RefreshPreview());
            root.style.display = DisplayStyle.None;
            return root;
        }

        public void ShowFragment(BaseComponent entity)
        {
            crossing = entity.GetComponent<DistrictCrossing>();
            ClosePicker();
            nextRefresh = 0;
            Refresh();
        }

        public void ClearFragment()
        {
            crossing = null;
            ClosePicker();
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

        // ---- who is who ----

        /// <summary>The half in the local player's colony, whichever half was clicked, or null.</summary>
        private DistrictCrossing MyHalf()
        {
            if (!crossing) return null;
            int localSlot = ColonySession.LocalSlot;
            DistrictCrossing partner = TradingPosts.Partner(crossing);
            if (OwnerOf(crossing) == localSlot) return crossing;
            if (OwnerOf(partner) == localSlot) return partner;
            return null;
        }

        private static int OwnerOf(DistrictCrossing half) => DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(half)) ?? -1;

        private void RefreshUnsafe()
        {
            if (!crossing || !TradingPosts.IsTradingPost(crossing))
            {
                root.style.display = DisplayStyle.None;
                return;
            }
            root.style.display = DisplayStyle.Flex;
            DistrictCrossing myHalf = MyHalf();
            // Seen from the local player's half, or, for anyone else, from the half that was clicked.
            DistrictCrossing a = myHalf ? myHalf : crossing;
            DistrictCrossing b = TradingPosts.Partner(a);
            int aSlot = OwnerOf(a), bSlot = OwnerOf(b);

            title.text = T("BeaverBuddies.Colony.Trade.Title");
            between.text = string.Format(T("BeaverBuddies.Colony.Trade.Between"), ColonyName(aSlot), ColonyName(bSlot));
            between.style.color = SlotColor(aSlot);

            RefreshExchange(myHalf != null, a, b, aSlot, bSlot);

            ColonyTradeLedger ledger = ColonyTradeLedger.Instance;
            totalsTitle.text = T("BeaverBuddies.Colony.Trade.Totals");
            sent.text = string.Format(T("BeaverBuddies.Colony.Trade.Flow"), ColonyName(aSlot), ColonyName(bSlot), Summary(ledger?.Sent(aSlot, bSlot)));
            received.text = string.Format(T("BeaverBuddies.Colony.Trade.Flow"), ColonyName(bSlot), ColonyName(aSlot), Summary(ledger?.Sent(bSlot, aSlot)));

            bool science = ColonyScienceService.IsEnabled && myHalf != null;
            Show(scienceTitle, science);
            Show(scienceRow, science);
            if (science)
                scienceTitle.text = string.Format(T("BeaverBuddies.Colony.Trade.Science"), ColonyName(bSlot),
                    ColonyScienceService.Instance.PointsOf(aSlot));

            viewOnly.text = T("BeaverBuddies.Colony.Trade.ViewOnly");
            Show(viewOnly, myHalf == null);
        }

        // ---- the exchange ----

        private void RefreshExchange(bool mine, DistrictCrossing a, DistrictCrossing b, int aSlot, int bSlot)
        {
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;
            CrossingExchange ax = ColonyExchangeService.Of(a), bx = ColonyExchangeService.Of(b);
            ExchangeState state = ax?.State ?? ExchangeState.None;
            exchangeTitle.text = T("BeaverBuddies.Colony.Trade.Exchange");
            shownSerial = state == ExchangeState.None ? 0 : ax.Serial;
            if (state != ExchangeState.None && bx != null)
            {
                shownGiveGood = ax.GoodId;
                shownGiveAmount = ax.Total;
                shownGetGood = bx.GoodId;
                shownGetAmount = bx.Total;
                shownRepeat = ax.Repeat;
            }

            Show(progressMine, false);
            Show(progressTheirs, false);
            Show(waiting, false);
            Show(acceptButton, mine && state == ExchangeState.Proposed && !ax.ProposedHere);
            Show(declineButton, mine && state == ExchangeState.Proposed && !ax.ProposedHere);
            Show(withdrawButton, mine && state == ExchangeState.Proposed && ax.ProposedHere);
            Show(cancelButton, mine && state == ExchangeState.Active);
            Show(form, mine && state == ExchangeState.None);
            if (!(mine && state == ExchangeState.None)) ClosePicker();

            if (state == ExchangeState.None || exchanges == null || bx == null)
            {
                exchangeText.text = T(mine ? "BeaverBuddies.Colony.Trade.NoExchange" : "BeaverBuddies.Colony.Trade.NoExchangeView");
                if (mine) RefreshForm(a, b, aSlot, bSlot);
                return;
            }

            string aGives = exchanges.Amount(ax.Total, ax.GoodId), bGives = exchanges.Amount(bx.Total, bx.GoodId);
            string repeating = ax.Repeat ? " " + T("BeaverBuddies.Colony.Trade.Repeating") : "";
            if (state == ExchangeState.Proposed)
            {
                // Always told from the offering colony's side: what it gives, and what it asks in return.
                bool offeredHere = ax.ProposedHere;
                string key = !mine ? "BeaverBuddies.Colony.Trade.OfferView"
                    : offeredHere ? "BeaverBuddies.Colony.Trade.YouOffer" : "BeaverBuddies.Colony.Trade.TheyOffer";
                exchangeText.text = (offeredHere
                    ? string.Format(T(key), ColonyName(aSlot), aGives, bGives, ColonyName(bSlot))
                    : string.Format(T(key), ColonyName(bSlot), bGives, aGives, ColonyName(aSlot))) + repeating;
                return;
            }

            exchangeText.text = ax.Repeat
                ? string.Format(T("BeaverBuddies.Colony.Trade.UnderWayRepeating"), ax.Rounds + 1, ax.Rounds)
                : T("BeaverBuddies.Colony.Trade.UnderWay");
            ShowProgress(progressMine, aSlot, ax);
            ShowProgress(progressTheirs, bSlot, bx);
            if (mine && ax.Remaining > 0 && ColonyExchangeService.StillToBring(a) == 0)
            {
                waiting.text = string.Format(T("BeaverBuddies.Colony.Trade.Waiting"), ColonyName(bSlot));
                Show(waiting, true);
            }
        }

        private void ShowProgress(Label label, int slot, CrossingExchange side)
        {
            if (side.Total <= 0) return;
            label.text = string.Format(T("BeaverBuddies.Colony.Trade.Progress"), ColonyName(slot), side.Sent, side.Total,
                GoodName(side.GoodId));
            Show(label, true);
        }

        private void RefreshForm(DistrictCrossing mine, DistrictCrossing theirs, int me, int them)
        {
            // A new form starts on goods (what each colony has most readily), never on science or beavers.
            if (giveGood == null || !IsOffered(giveGood)) giveGood = ItemsInOrder(mine, me).FirstOrDefault(g => !ExchangeTerms.IsSpecial(g));
            if (getGood == null || !IsOffered(getGood) || getGood == giveGood)
                getGood = ItemsInOrder(theirs, them).FirstOrDefault(g => g != giveGood && !ExchangeTerms.IsSpecial(g));
            ShowItem(giveIcon, giveItemButton, giveGood, "BeaverBuddies.Colony.Trade.YouHave", Stock(mine, me, giveGood));
            ShowItem(getIcon, getItemButton, getGood, "BeaverBuddies.Colony.Trade.TheyHave", Stock(theirs, them, getGood));
            repeatButton.text = T(repeat ? "BeaverBuddies.Colony.Trade.RepeatOn" : "BeaverBuddies.Colony.Trade.RepeatOff");
            formHint.text = string.Format(T("BeaverBuddies.Colony.Trade.FormHint"), ColonyName(them));
            RefreshPreview();
        }

        private void ShowItem(Image icon, Button button, string item, string key, int stock)
        {
            Sprite sprite = IconOf(item);
            icon.sprite = sprite;
            icon.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            button.text = string.Format(T(key), GoodName(item), stock);
        }

        private void RefreshPreview()
        {
            if (preview == null) return;
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;
            if (exchanges != null && TryReadOffer(out int give, out int get))
                preview.text = string.Format(T("BeaverBuddies.Colony.Trade.Preview"), exchanges.Amount(give, giveGood), exchanges.Amount(get, getGood))
                    + (repeat ? " " + T("BeaverBuddies.Colony.Trade.Repeating") : "");
            else
                preview.text = T("BeaverBuddies.Colony.Trade.Invalid");
        }

        private bool TryReadOffer(out int give, out int get)
        {
            bool ok = TryAmount(giveAmountField, out give) & TryAmount(getAmountField, out get);
            return ok && ExchangeTerms.AreValid(giveGood, give, getGood, get);
        }

        private static bool TryAmount(TextField field, out int amount)
        {
            string text = field?.value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                amount = 0;
                return true;
            }
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount)
                && amount <= ExchangeTerms.MaxAmount;
        }

        // ---- choosing what to give and to ask for ----

        private bool IsOffered(string item) =>
            item == ExchangeTerms.Beavers || (item == ExchangeTerms.Science ? ColonyScienceService.IsEnabled : _goodService.HasGood(item));

        /// <summary>Science (with separate science) and beavers first, then goods the colony has, then the rest; each by name.</summary>
        private List<string> ItemsInOrder(DistrictCrossing half, int slot)
        {
            var items = new List<string>();
            if (ColonyScienceService.IsEnabled) items.Add(ExchangeTerms.Science);
            items.Add(ExchangeTerms.Beavers);
            var goods = new List<string>();
            foreach (string goodId in _goodService.Goods) goods.Add(goodId);
            items.AddRange(goods.OrderByDescending(g => Stock(half, slot, g) > 0)
                .ThenBy(g => GoodName(g), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(g => g, StringComparer.Ordinal));
            return items;
        }

        /// <summary>How much of an item a colony has at this crossing's district: stock, science, or adult beavers.</summary>
        private int Stock(DistrictCrossing half, int slot, string item)
        {
            if (string.IsNullOrEmpty(item)) return 0;
            if (item == ExchangeTerms.Science) return ColonyScienceService.Instance?.PointsOf(slot) ?? 0;
            DistrictCenter district = TradingPosts.DistrictOf(half);
            if (!district) return 0;
            if (item == ExchangeTerms.Beavers) return district.GetComponent<DistrictPopulation>()?.NumberOfAdults ?? 0;
            return _resourceCountingService.GetDistrictResourceCounter(district).GetResourceCount(item).AvailableStock;
        }

        private void TogglePicker(int side)
        {
            if (pickerFor == side)
            {
                ClosePicker();
                return;
            }
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf) return;
            pickerFor = side;
            DistrictCrossing half = side == 1 ? myHalf : TradingPosts.Partner(myHalf);
            int slot = OwnerOf(half);
            string other = side == 1 ? getGood : giveGood;
            picker.Clear();
            foreach (string item in ItemsInOrder(half, slot))
            {
                // Two sides giving the same good make no sense.
                if (item == other) continue;
                picker.Add(PickerCell(item, Stock(half, slot, item), side));
            }
            _visualElementInitializer.InitializeVisualElement(picker);
            picker.style.display = DisplayStyle.Flex;
        }

        private void ClosePicker()
        {
            pickerFor = 0;
            if (picker == null) return;
            picker.style.display = DisplayStyle.None;
            picker.Clear();
        }

        private VisualElement PickerCell(string item, int stock, int side)
        {
            var cell = new Button(() =>
            {
                if (side == 1) giveGood = item;
                else getGood = item;
                ClosePicker();
                nextRefresh = 0;
                Refresh();
            });
            cell.tooltip = GoodName(item);
            cell.style.width = 40;
            cell.style.height = 44;
            cell.style.marginLeft = 1; cell.style.marginRight = 1; cell.style.marginTop = 1; cell.style.marginBottom = 1;
            cell.style.paddingLeft = 1; cell.style.paddingRight = 1; cell.style.paddingTop = 2; cell.style.paddingBottom = 1;
            cell.style.flexDirection = FlexDirection.Column;
            cell.style.alignItems = Align.Center;
            cell.style.backgroundColor = stock > 0 ? CellBackground : CellEmpty;
            Sprite sprite = IconOf(item);
            if (sprite != null)
            {
                var icon = new Image { sprite = sprite };
                icon.style.width = 24;
                icon.style.height = 24;
                cell.Add(icon);
            }
            else
            {
                // Science and beavers have no good icon: a short name instead.
                Label name = Text(10, bold: true);
                name.text = GoodName(item);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.style.height = 24;
                cell.Add(name);
            }
            Label count = Text(10, color: stock > 0 ? Ink : Muted);
            count.text = stock.ToString(CultureInfo.CurrentCulture);
            count.style.unityTextAlign = TextAnchor.MiddleCenter;
            cell.Add(count);
            return cell;
        }

        private Sprite IconOf(string item)
        {
            if (string.IsNullOrEmpty(item) || ExchangeTerms.IsSpecial(item)) return null;
            try { return _goodService.GetGood(item).Icon.Asset; }
            catch (Exception) { return null; }
        }

        private void OpenOverview()
        {
            if (TradeOverviewPanel.Instance?.Open() != true) Notice(T("BeaverBuddies.Colony.Trade.HostFirst"));
        }

        private void ToggleRepeat()
        {
            repeat = !repeat;
            repeatButton.text = T(repeat ? "BeaverBuddies.Colony.Trade.RepeatOn" : "BeaverBuddies.Colony.Trade.RepeatOff");
            RefreshPreview();
        }

        // ---- actions ----

        private void Propose()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf) return;
            if (!TryReadOffer(out int give, out int get))
            {
                Notice(T("BeaverBuddies.Colony.Trade.Invalid"));
                return;
            }
            string halfId = ReplayEvent.GetEntityID(myHalf);
            string giving = ExchangeTerms.GoodOf(giveGood, give), asking = ExchangeTerms.GoodOf(getGood, get);
            bool repeating = repeat;
            Send(() => new ExchangeProposedEvent
            {
                crossingID = halfId, giveGood = giving, giveAmount = give, getGood = asking, getAmount = get, repeat = repeating,
            });
        }

        private void Accept()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf || shownSerial == 0) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            // The terms as this player saw them on the panel: an offer changed in the meantime is not accepted.
            int serial = shownSerial, giveAmount = shownGiveAmount, getAmount = shownGetAmount;
            string giveId = shownGiveGood, getId = shownGetGood;
            bool repeating = shownRepeat;
            Send(() => new ExchangeAcceptedEvent
            {
                crossingID = halfId, serial = serial, giveGood = giveId, giveAmount = giveAmount, getGood = getId,
                getAmount = getAmount, repeat = repeating,
            });
        }

        private void Cancel()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf || shownSerial == 0) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            int serial = shownSerial;
            Send(() => new ExchangeCancelledEvent { crossingID = halfId, serial = serial });
        }

        private void GiveScience(int amount)
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf) return;
            int them = OwnerOf(TradingPosts.Partner(myHalf));
            if (them < 0 || them == ColonySession.LocalSlot) return;
            Send(() => new GiftScienceEvent { toSlot = them, amount = amount });
        }

        /// <summary>Sends an action through the host. Trading exists only in a hosted game.</summary>
        private void Send(Func<ReplayEvent> action)
        {
            if (ReplayEvent.DoPrefix(action)) Notice(T("BeaverBuddies.Colony.Trade.HostFirst"));
            nextRefresh = 0;
        }

        private static void Notice(string text) => SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(text);

        // ---- text ----

        private string Summary(List<KeyValuePair<string, int>> goods)
        {
            if (goods == null || goods.Count == 0) return T("BeaverBuddies.Colony.Trade.Nothing");
            return string.Join(", ", goods.Take(4).Select(g => $"{g.Value} {GoodName(g.Key)}")) + (goods.Count > 4 ? ", …" : "");
        }

        private string GoodName(string goodId) =>
            string.IsNullOrEmpty(goodId) ? "" : ColonyExchangeService.Instance?.GoodName(goodId) ?? goodId;

        private static string ColonyName(int slot) => ColonyExchangeService.ColonyName(slot);

        private static Color SlotColor(int slot) =>
            slot >= 0 && slot < StartingLocationPlayer.PLAYER_COLORS.Length ? StartingLocationPlayer.PLAYER_COLORS[slot] : Ink;

        private static string T(string key) => RegisteredLocalizationService.T(key);

        // ---- elements ----

        private static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static Label Text(int size, bool bold = false, Color? color = null)
        {
            var label = new Label();
            label.style.fontSize = size;
            label.style.color = color ?? Ink;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;
            return row;
        }

        private static Button MakeButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.fontSize = 12;
            button.style.marginLeft = 0;
            button.style.marginRight = 4;
            button.style.marginTop = 2;
            return button;
        }

        /// <summary>"You give:  [icon] Logs (you have 240)  [100]": the item button opens the item grid.</summary>
        private static VisualElement OfferRow(string caption, out Image icon, out Button item, TextField amount, Action choose)
        {
            var row = Row();
            row.style.flexWrap = Wrap.NoWrap;
            row.style.marginTop = 2;
            Label captionLabel = Text(12);
            captionLabel.text = caption;
            captionLabel.style.width = 58;
            captionLabel.style.flexShrink = 0;
            row.Add(captionLabel);
            icon = new Image();
            icon.style.width = 20;
            icon.style.height = 20;
            icon.style.flexShrink = 0;
            icon.style.marginRight = 2;
            row.Add(icon);
            item = MakeButton("", choose);
            item.style.flexGrow = 1;
            item.style.flexShrink = 1;
            item.style.whiteSpace = WhiteSpace.Normal;
            row.Add(item);
            amount.style.width = 52;
            amount.style.flexShrink = 0;
            amount.style.marginLeft = 2;
            row.Add(amount);
            return row;
        }

        // Styled like the chat box (seen working in the game): a dark box with a thin rule.
        private static TextField AmountField(string value)
        {
            var field = new TextField { maxLength = 4, value = value };
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
            field.style.marginRight = 0;
            VisualElement box = field.Q<VisualElement>(TextField.textInputUssName);
            if (box != null)
            {
                var b = box.style;
                b.backgroundColor = new Color(.03f, .03f, .02f, .9f);
                b.color = Ink;
                b.fontSize = 12;
                b.unityTextAlign = TextAnchor.MiddleRight;
                b.marginTop = 0; b.marginBottom = 0; b.marginLeft = 0; b.marginRight = 0;
                b.paddingTop = 2; b.paddingBottom = 2; b.paddingLeft = 4; b.paddingRight = 4;
                b.borderTopWidth = 1; b.borderBottomWidth = 1; b.borderLeftWidth = 1; b.borderRightWidth = 1;
                b.borderTopColor = Rule; b.borderBottomColor = Rule; b.borderLeftColor = Rule; b.borderRightColor = Rule;
                b.borderTopLeftRadius = 3; b.borderTopRightRadius = 3; b.borderBottomLeftRadius = 3; b.borderBottomRightRadius = 3;
            }
            return field;
        }
    }
}
