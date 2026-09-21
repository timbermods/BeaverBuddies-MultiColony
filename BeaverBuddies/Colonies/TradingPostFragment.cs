using BeaverBuddies.Editor;
using BeaverBuddies.Events;
using BeaverBuddies.Panel;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.Goods;
using Timberborn.InputSystem;
using Timberborn.InventorySystem;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The trading post's panel, under a District Crossing that joins two players' colonies, built from the game's own
    /// panel pieces so it reads as part of the game: who trades here, then the exchange (the offer form, an offer
    /// waiting for an answer, or the progress of each side), what waits at the post, what has passed between the two
    /// colonies, and a science gift. It scrolls rather than run off a short screen. Display and buttons only; each
    /// button sends an ordinary action that every computer plays.
    /// </summary>
    public class TradingPostFragment : IEntityPanelFragment
    {
        private static readonly int[] ScienceGifts = { 50, 250 };
        // The form's two sides.
        private const int Give = 1, Get = 2;
        private const float RefreshInterval = 0.5f;
        // The panel keeps this far from the screen's bottom edge, and is never squeezed smaller than this.
        private const float ScreenMargin = 12, MinHeight = 150;
        private const int ChipsShown = 5;
        // More beavers than this in one click of a good's amount is never meant: choosing beavers starts at 1.
        private const int MostBeaversByDefault = 10;
        private static readonly Color BarText = new Color(0.93f, 0.93f, 0.9f);

        private sealed class OfferSide
        {
            public int Side;
            public NineSliceVisualElement Card;
            public Label Caption, Stock, Name;
            public Button Selector;
            public Image Icon;
            public TextField Amount;
        }

        private sealed class TermRow
        {
            public VisualElement Root;
            public Label Caption, What, Note;
            public Image Icon;
        }

        private sealed class ProgressRow
        {
            public VisualElement Root;
            public Label Caption, Text;
            public Image Icon;
            public Timberborn.CoreUI.ProgressBar Bar;
        }

        private sealed class ChipRow
        {
            public VisualElement Root, Chips;
            public Label Caption;
            public string Shown;
        }

        private readonly TradeItems _items;
        private readonly VisualElementInitializer _visualElementInitializer;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly InputService _inputService;

        private NineSliceVisualElement root;
        private Label headerText;
        private ScrollView body;
        private TradingPostGoodPicker picker;
        // Making an offer.
        private VisualElement compose;
        private OfferSide giveSide, getSide;
        private Label summary;
        private Toggle repeatToggle;
        private Button makeOfferButton;
        // An offer waiting for an answer.
        private VisualElement proposal, answerButtons;
        private Label proposalTitle, proposalNote, proposalRepeats;
        private TermRow firstTerm, secondTerm;
        private Button acceptButton, declineButton, withdrawButton;
        // An exchange under way.
        private VisualElement active;
        private Label activeTitle, activeNote, waitingLabel;
        private ProgressRow firstProgress, secondProgress;
        private Button stopButton;
        // Seen by a player of neither colony, with nothing going on.
        private Label idleLabel, viewOnlyLabel;
        // At the post, what has passed, and science.
        private ChipRow dockRow, sentRow, receivedRow;
        private Label historyTitle;
        private VisualElement giftRow;
        private Label giftPoints;
        private readonly List<(int amount, Button button)> giftButtons = new List<(int, Button)>();

        private DistrictCrossing crossing;
        private float nextRefresh;
        private float fittedHeight = -1;
        private int partnerSlot = -1;

        // The offer being written: kept while the panel is open on other crossings too.
        private string giveItem, getItem;

        // The exchange as last shown: Accept and Cancel act on exactly this, never on something that changed since.
        private int shownSerial;
        private string shownGiveGood, shownGetGood;
        private int shownGiveAmount, shownGetAmount;
        private bool shownRepeat;

        public TradingPostFragment(TradeItems items, VisualElementInitializer visualElementInitializer,
            ITooltipRegistrar tooltipRegistrar, InputService inputService)
        {
            _items = items;
            _visualElementInitializer = visualElementInitializer;
            _tooltipRegistrar = tooltipRegistrar;
            _inputService = inputService;
        }

        // ---- building (once; afterwards only texts, pictures and what is shown change, so no click is lost) ----

        public VisualElement InitializeFragment()
        {
            picker = new TradingPostGoodPicker(_items, _inputService, _tooltipRegistrar, _visualElementInitializer);
            root = NativeElements.Section();
            root.style.flexShrink = 0;

            VisualElement header = NativeElements.Row();
            header.style.flexShrink = 0;
            headerText = RichText(13);
            headerText.style.flexGrow = 1;
            headerText.style.flexShrink = 1;
            header.Add(headerText);
            Button allPosts = SmallButton(T("BeaverBuddies.Colony.Trade.AllPostsShort"), OpenOverview);
            allPosts.style.marginLeft = 6;
            _tooltipRegistrar.RegisterWithKeyBinding(allPosts, T("BeaverBuddies.Colony.Trade.AllPostsTooltip"), TradeOverviewPanel.KeyBindingId);
            header.Add(allPosts);
            root.Add(header);

            body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("scroll--green-decorated");
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.verticalScrollerVisibility = ScrollerVisibility.Auto;
            body.style.flexShrink = 1;
            body.style.minHeight = 0;
            body.contentContainer.style.paddingLeft = 0;
            body.contentContainer.style.paddingRight = 0;
            root.Add(body);

            body.Add(BuildCompose());
            body.Add(BuildProposal());
            body.Add(BuildActive());
            idleLabel = NativeElements.MutedText(T("BeaverBuddies.Colony.Trade.NoExchangeView"), 13);
            idleLabel.style.marginTop = 6;
            body.Add(idleLabel);
            dockRow = BuildChipRow();
            dockRow.Root.style.marginTop = 6;
            _tooltipRegistrar.Register(dockRow.Root, T("BeaverBuddies.Colony.Trade.AtThisPostTooltip"));
            body.Add(dockRow.Root);
            body.Add(BuildHistory());
            body.Add(BuildGift());
            viewOnlyLabel = NativeElements.MutedText(T("BeaverBuddies.Colony.Trade.ViewOnly"));
            viewOnlyLabel.style.marginTop = 8;
            body.Add(viewOnlyLabel);

            root.Add(picker.Root);
            // The game's own setup: click sounds, buttons that click with any modifier, scroll bars, and input boxes
            // that switch the game's hotkeys off while a player types in them.
            _visualElementInitializer.InitializeVisualElement(root);
            root.RegisterCallback<GeometryChangedEvent>(_ => FitToScreen());
            root.style.display = DisplayStyle.None;
            return root;
        }

        private VisualElement BuildCompose()
        {
            compose = new VisualElement();
            giveSide = BuildOfferSide(Give, T("BeaverBuddies.Colony.Trade.YouGiveCaption"));
            getSide = BuildOfferSide(Get, T("BeaverBuddies.Colony.Trade.YouGetCaption"));
            compose.Add(giveSide.Card);
            compose.Add(getSide.Card);
            summary = RichText(12);
            summary.style.marginTop = 6;
            summary.style.marginLeft = 1;
            compose.Add(summary);
            repeatToggle = NativeElements.CheckBox(T("BeaverBuddies.Colony.Trade.RepeatToggle"));
            repeatToggle.style.marginTop = 5;
            repeatToggle.style.marginBottom = 5;
            compose.Add(repeatToggle);
            makeOfferButton = NativeElements.WoodenButton(T("BeaverBuddies.Colony.Trade.Propose"), Propose);
            _tooltipRegistrar.Register(makeOfferButton, () => string.Format(T("BeaverBuddies.Colony.Trade.ProposeTooltip"), PlainName(partnerSlot)));
            compose.Add(makeOfferButton);
            return compose;
        }

        /// <summary>"You give / You have 240" over [icon Planks ▾] [−] [100] [+], on the description's blue.</summary>
        private OfferSide BuildOfferSide(int side, string caption)
        {
            var o = new OfferSide { Side = side };
            o.Card = Card();
            VisualElement top = NativeElements.Row();
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.marginBottom = 3;
            o.Caption = NativeElements.Caption(caption);
            o.Stock = NativeElements.MutedText();
            o.Stock.style.unityTextAlign = TextAnchor.MiddleRight;
            o.Stock.style.flexShrink = 1;
            o.Stock.style.marginLeft = 6;
            top.Add(o.Caption);
            top.Add(o.Stock);
            o.Card.Add(top);

            VisualElement row = NativeElements.Row();
            o.Selector = NativeElements.WoodenButton("", () => TogglePicker(o));
            var s = o.Selector.style;
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.justifyContent = Justify.FlexStart;
            s.flexGrow = 1;
            s.flexShrink = 1;
            s.minHeight = 32;
            s.height = 32;
            s.paddingLeft = 6;
            s.paddingRight = 0;
            o.Icon = NativeElements.Icon(24);
            o.Icon.style.marginRight = 6;
            o.Selector.Add(o.Icon);
            o.Name = NativeElements.Text("", 13);
            o.Name.style.flexGrow = 1;
            o.Name.style.flexShrink = 1;
            o.Name.style.unityTextAlign = TextAnchor.MiddleLeft;
            o.Name.pickingMode = PickingMode.Ignore;
            o.Selector.Add(o.Name);
            o.Selector.Add(NativeElements.DropdownArrow());
            _tooltipRegistrar.Register(o.Selector, T("BeaverBuddies.Colony.Trade.ChooseTooltip"));
            picker.AddOpener(o.Selector);
            row.Add(o.Selector);

            Button minus = NativeElements.SquareButton(plus: false, e => StepAmount(o, up: false, e.shiftKey));
            minus.style.marginLeft = 4;
            _tooltipRegistrar.Register(minus, () => StepTooltip(o, "BeaverBuddies.Colony.Trade.StepLess"));
            row.Add(minus);
            o.Amount = NativeElements.InputBox(maxLength: 4, width: 52);
            o.Amount.value = "100";
            o.Amount.RegisterValueChangedCallback(_ => RefreshSummary());
            row.Add(o.Amount);
            Button plus = NativeElements.SquareButton(plus: true, e => StepAmount(o, up: true, e.shiftKey));
            _tooltipRegistrar.Register(plus, () => StepTooltip(o, "BeaverBuddies.Colony.Trade.StepMore"));
            row.Add(plus);
            o.Card.Add(row);
            return o;
        }

        private VisualElement BuildProposal()
        {
            proposal = new VisualElement();
            NineSliceVisualElement card = Card();
            card.Add(TitleRow(out proposalTitle, out proposalNote));
            firstTerm = BuildTermRow();
            secondTerm = BuildTermRow();
            card.Add(firstTerm.Root);
            card.Add(secondTerm.Root);
            proposalRepeats = NativeElements.MutedText(T("BeaverBuddies.Colony.Trade.RepeatsLine"));
            proposalRepeats.style.marginTop = 2;
            card.Add(proposalRepeats);
            proposal.Add(card);

            answerButtons = NativeElements.Row();
            answerButtons.style.marginTop = 6;
            acceptButton = NativeElements.WoodenButton(T("BeaverBuddies.Colony.Trade.Accept"), Accept);
            declineButton = NativeElements.RedButton(T("BeaverBuddies.Colony.Trade.Decline"), Cancel);
            withdrawButton = NativeElements.RedButton(T("BeaverBuddies.Colony.Trade.Withdraw"), Cancel);
            foreach (Button button in new[] { acceptButton, declineButton, withdrawButton })
            {
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                answerButtons.Add(button);
            }
            declineButton.style.marginLeft = 6;
            proposal.Add(answerButtons);
            return proposal;
        }

        private VisualElement BuildActive()
        {
            active = new VisualElement();
            NineSliceVisualElement card = Card();
            card.Add(TitleRow(out activeTitle, out activeNote));
            firstProgress = BuildProgressRow(green: false);
            secondProgress = BuildProgressRow(green: true);
            card.Add(firstProgress.Root);
            card.Add(secondProgress.Root);
            waitingLabel = NativeElements.MutedText();
            waitingLabel.style.marginTop = 2;
            card.Add(waitingLabel);
            active.Add(card);
            stopButton = NativeElements.RedButton(T("BeaverBuddies.Colony.Trade.Cancel"), Cancel);
            stopButton.style.marginTop = 6;
            _tooltipRegistrar.Register(stopButton, T("BeaverBuddies.Colony.Trade.CancelTooltip"));
            active.Add(stopButton);
            return active;
        }

        private VisualElement BuildHistory()
        {
            var history = new VisualElement();
            history.Add(NativeElements.Rule());
            historyTitle = NativeElements.Caption();
            historyTitle.style.marginBottom = 2;
            history.Add(historyTitle);
            sentRow = BuildChipRow();
            receivedRow = BuildChipRow();
            history.Add(sentRow.Root);
            history.Add(receivedRow.Root);
            return history;
        }

        private VisualElement BuildGift()
        {
            giftRow = NativeElements.Row();
            giftRow.style.marginTop = 6;
            Image icon = NativeElements.Icon(18);
            icon.sprite = _items.IconOf(ExchangeTerms.Science);
            icon.style.marginRight = 5;
            giftRow.Add(icon);
            giftRow.Add(NativeElements.Text(T("BeaverBuddies.Colony.Trade.GiftScience"), 12));
            giftPoints = NativeElements.MutedText();
            giftPoints.style.marginLeft = 5;
            giftPoints.style.flexGrow = 1;
            giftPoints.style.flexShrink = 1;
            giftRow.Add(giftPoints);
            foreach (int amount in ScienceGifts)
            {
                int gift = amount;
                Button button = SmallButton(gift.ToString(CultureInfo.CurrentCulture), () => GiveScience(gift));
                button.style.marginLeft = 4;
                _tooltipRegistrar.Register(button, () => string.Format(T("BeaverBuddies.Colony.Trade.GiftTooltip"), gift, PlainName(partnerSlot)));
                giftButtons.Add((gift, button));
                giftRow.Add(button);
            }
            return giftRow;
        }

        private NineSliceVisualElement Card()
        {
            NineSliceVisualElement card = NativeElements.Box("bg-sub-box--blue");
            card.style.marginTop = 6;
            card.style.paddingLeft = 8; card.style.paddingRight = 8; card.style.paddingTop = 6; card.style.paddingBottom = 7;
            return card;
        }

        private static VisualElement TitleRow(out Label title, out Label note)
        {
            VisualElement row = NativeElements.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 3;
            title = RichText(13, bold: true);
            title.style.flexShrink = 1;
            note = NativeElements.MutedText();
            note.style.marginLeft = 6;
            note.style.flexShrink = 0;
            row.Add(title);
            row.Add(note);
            return row;
        }

        private static TermRow BuildTermRow()
        {
            var term = new TermRow { Root = NativeElements.Row() };
            term.Root.style.marginTop = 3;
            term.Caption = NativeElements.Caption();
            term.Caption.style.width = 70;
            term.Caption.style.flexShrink = 0;
            term.Icon = NativeElements.Icon(22);
            term.Icon.style.marginRight = 6;
            term.What = NativeElements.Text("", 13);
            term.What.style.flexGrow = 1;
            term.What.style.flexShrink = 1;
            term.Note = NativeElements.MutedText();
            term.Note.style.marginLeft = 6;
            term.Root.Add(term.Caption);
            term.Root.Add(term.Icon);
            term.Root.Add(term.What);
            term.Root.Add(term.Note);
            return term;
        }

        // The game's own progress bar (a warehouse's capacity), with the count on it.
        private static ProgressRow BuildProgressRow(bool green)
        {
            var progress = new ProgressRow { Root = new VisualElement() };
            progress.Root.style.marginTop = 3;
            progress.Caption = NativeElements.Caption();
            progress.Root.Add(progress.Caption);
            VisualElement line = NativeElements.Row();
            line.style.marginTop = 1;
            progress.Icon = NativeElements.Icon(24);
            progress.Icon.style.marginRight = 6;
            line.Add(progress.Icon);
            progress.Bar = new Timberborn.CoreUI.ProgressBar();
            if (green) progress.Bar.AddToClassList("progress-bar--green");
            progress.Bar.style.flexGrow = 1;
            progress.Bar.style.minHeight = 26;
            progress.Bar.style.height = 26;
            progress.Text = NativeElements.Text("", 12);
            progress.Text.style.color = BarText;
            progress.Text.style.unityTextAlign = TextAnchor.MiddleCenter;
            progress.Text.style.whiteSpace = WhiteSpace.NoWrap;
            progress.Bar.Add(progress.Text);
            line.Add(progress.Bar);
            progress.Root.Add(line);
            return progress;
        }

        private static ChipRow BuildChipRow()
        {
            var row = new ChipRow { Root = NativeElements.Row(Align.FlexStart) };
            row.Root.style.marginTop = 2;
            row.Caption = NativeElements.MutedText();
            row.Caption.style.width = 84;
            row.Caption.style.flexShrink = 0;
            row.Caption.style.marginTop = 1;
            row.Chips = NativeElements.Row();
            row.Chips.style.flexWrap = Wrap.Wrap;
            row.Chips.style.flexGrow = 1;
            row.Chips.style.flexShrink = 1;
            row.Root.Add(row.Caption);
            row.Root.Add(row.Chips);
            return row;
        }

        private static Button SmallButton(string text, Action onClick)
        {
            Button button = NativeElements.WoodenButton(text, onClick);
            button.style.fontSize = 12;
            button.style.minHeight = 24;
            button.style.height = 24;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.paddingLeft = 9;
            button.style.paddingRight = 9;
            button.style.flexShrink = 0;
            return button;
        }

        private static Label RichText(int size, bool bold = false)
        {
            Label label = NativeElements.Text("", size, bold);
            label.enableRichText = true;
            return label;
        }

        // ---- the panel's life ----

        public void ShowFragment(BaseComponent entity)
        {
            crossing = entity.GetComponent<DistrictCrossing>();
            picker.Close();
            nextRefresh = 0;
            body.scrollOffset = Vector2.zero;
            Refresh();
        }

        public void ClearFragment()
        {
            crossing = null;
            picker?.Close();
            StopTyping();
            if (root != null) root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Leaves the amount boxes. The game switches its hotkeys off while a player types in a box and on again when
        /// the box loses focus; a box hidden while it has focus might never lose it.
        /// </summary>
        private void StopTyping()
        {
            giveSide?.Amount.Blur();
            getSide?.Amount.Blur();
        }

        public void UpdateFragment()
        {
            if (!crossing) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
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
                Plugin.LogWarning("[Colony] Trading post panel: " + error);
                picker.Close();
                root.style.display = DisplayStyle.None;
                crossing = null;
            }
        }

        /// <summary>
        /// The panel ends above the screen's bottom edge: when the game's sections above it (the description, the
        /// workers) leave too little room, its content scrolls.
        /// </summary>
        private void FitToScreen()
        {
            IPanel panel = root.panel;
            if (panel == null || root.style.display == DisplayStyle.None) return;
            float top = root.worldBound.yMin, bottom = panel.visualTree.worldBound.yMax;
            if (float.IsNaN(top) || float.IsNaN(bottom) || bottom <= 0) return;
            float height = Mathf.Max(MinHeight, bottom - top - ScreenMargin);
            if (Mathf.Abs(height - fittedHeight) < 1) return;
            fittedHeight = height;
            root.style.maxHeight = height;
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
                picker.Close();
                StopTyping();
                root.style.display = DisplayStyle.None;
                return;
            }
            NativeElements.Show(root, true);
            DistrictCrossing myHalf = MyHalf();
            bool mine = myHalf != null;
            // Seen from the local player's half, or, for anyone else, from the half that was clicked.
            DistrictCrossing a = mine ? myHalf : crossing;
            DistrictCrossing b = TradingPosts.Partner(a);
            int aSlot = OwnerOf(a), bSlot = OwnerOf(b);
            partnerSlot = bSlot;
            NativeElements.SetText(headerText, mine
                ? string.Format(T("BeaverBuddies.Colony.Trade.TradingWith"), ColoredName(bSlot))
                : string.Format(T("BeaverBuddies.Colony.Trade.Between"), ColoredName(aSlot), ColoredName(bSlot)));

            CrossingExchange ax = ColonyExchangeService.Of(a), bx = ColonyExchangeService.Of(b);
            ExchangeState state = ax != null && bx != null ? ax.State : ExchangeState.None;
            Remember(state, ax, bx);
            bool composing = mine && state == ExchangeState.None;
            NativeElements.Show(compose, composing);
            NativeElements.Show(idleLabel, !mine && state == ExchangeState.None);
            NativeElements.Show(proposal, state == ExchangeState.Proposed);
            NativeElements.Show(active, state == ExchangeState.Active);
            if (!composing)
            {
                picker.Close();
                StopTyping();
            }

            if (composing) RefreshCompose(a, b, aSlot, bSlot);
            else if (state == ExchangeState.Proposed) RefreshProposal(mine, a, ax, bx, aSlot, bSlot);
            else if (state == ExchangeState.Active) RefreshActive(mine, a, ax, bx, aSlot, bSlot);

            RefreshDock(a);
            RefreshHistory(mine, aSlot, bSlot);
            RefreshGift(mine, aSlot);
            NativeElements.Show(viewOnlyLabel, !mine);
            picker.RefreshCounts();
            FitToScreen();
        }

        private void Remember(ExchangeState state, CrossingExchange ax, CrossingExchange bx)
        {
            shownSerial = state == ExchangeState.None ? 0 : ax.Serial;
            if (state == ExchangeState.None) return;
            shownGiveGood = ax.GoodId;
            shownGiveAmount = ax.Total;
            shownGetGood = bx.GoodId;
            shownGetAmount = bx.Total;
            shownRepeat = ax.Repeat;
        }

        // ---- making an offer ----

        private void RefreshCompose(DistrictCrossing mine, DistrictCrossing theirs, int me, int them)
        {
            // A new form starts on goods (what each colony has most of), never on science or beavers.
            if (giveItem == null || !_items.IsOffered(giveItem)) giveItem = _items.MostStocked(mine, me, getItem);
            if (getItem == null || !_items.IsOffered(getItem) || getItem == giveItem) getItem = _items.MostStocked(theirs, them, giveItem);
            ShowSide(giveSide, giveItem, string.Format(T("BeaverBuddies.Colony.Trade.YouHaveN"), Count(_items.StockOf(mine, me, giveItem))));
            ShowSide(getSide, getItem, string.Format(T("BeaverBuddies.Colony.Trade.TheyHaveN"), PlainName(them),
                Count(_items.StockOf(theirs, them, getItem))));
            RefreshSummary();
        }

        private void ShowSide(OfferSide side, string item, string stock)
        {
            Sprite icon = _items.IconOf(item);
            if (side.Icon.sprite != icon) side.Icon.sprite = icon;
            NativeElements.Show(side.Icon, icon != null);
            NativeElements.SetText(side.Name, _items.Name(item));
            NativeElements.SetText(side.Stock, stock);
        }

        /// <summary>One line under the form: what the offer is, or what is wrong with it. Make offer waits for a good one.</summary>
        private void RefreshSummary()
        {
            if (summary == null) return;
            TradeOfferForm.Verdict verdict = TradeOfferForm.Judge(giveItem, giveSide.Amount.value, getItem, getSide.Amount.value,
                out int give, out int get);
            string partner = PlainName(partnerSlot);
            string text;
            switch (verdict)
            {
                case TradeOfferForm.Verdict.Exchange:
                    text = string.Format(T("BeaverBuddies.Colony.Trade.SummaryExchange"), partner, AmountOf(give, giveItem), AmountOf(get, getItem));
                    break;
                case TradeOfferForm.Verdict.Gift:
                    text = string.Format(T("BeaverBuddies.Colony.Trade.SummaryGift"), partner, AmountOf(give, giveItem));
                    break;
                case TradeOfferForm.Verdict.Request:
                    text = string.Format(T("BeaverBuddies.Colony.Trade.SummaryRequest"), partner, AmountOf(get, getItem));
                    break;
                case TradeOfferForm.Verdict.BadAmount:
                    text = string.Format(T("BeaverBuddies.Colony.Trade.ErrorAmount"), ExchangeTerms.MaxAmount);
                    break;
                case TradeOfferForm.Verdict.NothingEitherWay:
                    text = T("BeaverBuddies.Colony.Trade.ErrorNothing");
                    break;
                case TradeOfferForm.Verdict.SameItem:
                    text = T("BeaverBuddies.Colony.Trade.ErrorSame");
                    break;
                default:
                    text = T("BeaverBuddies.Colony.Trade.ErrorNoItem");
                    break;
            }
            bool offer = TradeOfferForm.IsOffer(verdict);
            NativeElements.SetText(summary, text);
            summary.style.color = offer ? NativeElements.Muted : NativeElements.Warning;
            makeOfferButton.SetEnabled(offer);
        }

        private string ItemOf(OfferSide side) => side.Side == Give ? giveItem : getItem;

        private void SetItem(OfferSide side, string item)
        {
            if (side.Side == Give) giveItem = item;
            else getItem = item;
        }

        private void StepAmount(OfferSide side, bool up, bool shift)
        {
            TradeOfferForm.TryReadAmount(side.Amount.value, out int amount);
            int next = TradeOfferForm.Stepped(amount, TradeOfferForm.Step(ItemOf(side), shift), up);
            side.Amount.value = next.ToString(CultureInfo.InvariantCulture);
        }

        private string StepTooltip(OfferSide side, string key) =>
            string.Format(T(key), TradeOfferForm.Step(ItemOf(side), shift: false), TradeOfferForm.Step(ItemOf(side), shift: true));

        private void TogglePicker(OfferSide side)
        {
            if (picker.IsOpen && picker.Side == side.Side)
            {
                picker.Close();
                return;
            }
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf) return;
            DistrictCrossing half = side.Side == Give ? myHalf : TradingPosts.Partner(myHalf);
            int slot = OwnerOf(half);
            string heading = side.Side == Give
                ? T("BeaverBuddies.Colony.Trade.PickGive")
                : string.Format(T("BeaverBuddies.Colony.Trade.PickGet"), PlainName(slot));
            picker.Open(side.Side, heading, ItemOf(side), item => _items.StockOf(half, slot, item), item => Choose(side, item), side.Card);
        }

        private void Choose(OfferSide side, string item)
        {
            string previous = ItemOf(side);
            OfferSide other = side == giveSide ? getSide : giveSide;
            // Choosing what the other side has swaps the two.
            if (item == ItemOf(other)) SetItem(other, previous);
            SetItem(side, item);
            if (item == ExchangeTerms.Beavers && previous != ExchangeTerms.Beavers
                && (!TradeOfferForm.TryReadAmount(side.Amount.value, out int amount) || amount > MostBeaversByDefault))
                side.Amount.value = "1";
            nextRefresh = 0;
            Refresh();
        }

        // ---- an offer waiting for an answer ----

        private void RefreshProposal(bool mine, DistrictCrossing a, CrossingExchange ax, CrossingExchange bx, int aSlot, int bSlot)
        {
            bool offeredHere = ax.ProposedHere;
            if (mine && offeredHere)
            {
                NativeElements.SetText(proposalTitle, T("BeaverBuddies.Colony.Trade.YourOfferTitle"));
                NativeElements.SetText(proposalNote, string.Format(T("BeaverBuddies.Colony.Trade.WaitingFor"), PlainName(bSlot)));
                ShowTerm(firstTerm, T("BeaverBuddies.Colony.Trade.YouGiveCaption"), ax, "");
                ShowTerm(secondTerm, T("BeaverBuddies.Colony.Trade.YouGetCaption"), bx, "");
            }
            else if (mine)
            {
                NativeElements.SetText(proposalTitle, string.Format(T("BeaverBuddies.Colony.Trade.TheyOfferTitle"), ColoredName(bSlot)));
                NativeElements.SetText(proposalNote, "");
                ShowTerm(firstTerm, T("BeaverBuddies.Colony.Trade.YouGetCaption"), bx, "");
                // Whether the colony can keep its side.
                string have = ax.Total > 0
                    ? string.Format(T("BeaverBuddies.Colony.Trade.YouHaveNote"), Count(_items.StockOf(a, aSlot, ax.GoodId)))
                    : "";
                ShowTerm(secondTerm, T("BeaverBuddies.Colony.Trade.YouGiveCaption"), ax, have);
            }
            else
            {
                // Told from the offering colony's side.
                int proposer = offeredHere ? aSlot : bSlot, other = offeredHere ? bSlot : aSlot;
                NativeElements.SetText(proposalTitle, string.Format(T("BeaverBuddies.Colony.Trade.OfferBetween"),
                    ColoredName(proposer), ColoredName(other)));
                NativeElements.SetText(proposalNote, "");
                ShowTerm(firstTerm, Gives(proposer), offeredHere ? ax : bx, "");
                ShowTerm(secondTerm, Gives(other), offeredHere ? bx : ax, "");
            }
            NativeElements.Show(proposalRepeats, ax.Repeat);
            NativeElements.Show(answerButtons, mine);
            NativeElements.Show(acceptButton, mine && !offeredHere);
            NativeElements.Show(declineButton, mine && !offeredHere);
            NativeElements.Show(withdrawButton, mine && offeredHere);
        }

        private void ShowTerm(TermRow term, string caption, CrossingExchange side, string note)
        {
            NativeElements.SetText(term.Caption, caption);
            bool something = side.Total > 0;
            Sprite icon = something ? _items.IconOf(side.GoodId) : null;
            if (term.Icon.sprite != icon) term.Icon.sprite = icon;
            NativeElements.Show(term.Icon, icon != null);
            NativeElements.SetText(term.What, AmountOf(side.Total, side.GoodId));
            term.What.style.color = something ? new StyleColor(StyleKeyword.Null) : new StyleColor(NativeElements.Muted);
            NativeElements.SetText(term.Note, note);
            NativeElements.Show(term.Note, !string.IsNullOrEmpty(note));
        }

        // ---- an exchange under way ----

        private void RefreshActive(bool mine, DistrictCrossing a, CrossingExchange ax, CrossingExchange bx, int aSlot, int bSlot)
        {
            NativeElements.SetText(activeTitle, T("BeaverBuddies.Colony.Trade.UnderWayTitle"));
            NativeElements.SetText(activeNote, ax.Repeat ? string.Format(T("BeaverBuddies.Colony.Trade.RoundRepeating"), ax.Rounds + 1) : "");
            ShowProgress(firstProgress, mine ? T("BeaverBuddies.Colony.Trade.YouGiveCaption") : Gives(aSlot), ax);
            ShowProgress(secondProgress, mine ? T("BeaverBuddies.Colony.Trade.YouGetCaption") : Gives(bSlot), bx);
            bool waiting = mine && ax.Remaining > 0 && ColonyExchangeService.StillToBring(a) == 0;
            if (waiting) NativeElements.SetText(waitingLabel, string.Format(T("BeaverBuddies.Colony.Trade.Waiting"), PlainName(bSlot)));
            NativeElements.Show(waitingLabel, waiting);
            NativeElements.Show(stopButton, mine);
        }

        private void ShowProgress(ProgressRow row, string caption, CrossingExchange side)
        {
            // A side that gives nothing (a gift the other way) has nothing to show.
            NativeElements.Show(row.Root, side.Total > 0);
            if (side.Total <= 0) return;
            NativeElements.SetText(row.Caption, caption);
            Sprite icon = _items.IconOf(side.GoodId);
            if (row.Icon.sprite != icon) row.Icon.sprite = icon;
            row.Bar.SetProgress((float)side.Sent / side.Total);
            NativeElements.SetText(row.Text, string.Format(T("BeaverBuddies.Colony.Trade.ProgressText"), Count(side.Sent), Count(side.Total),
                _items.Name(side.GoodId)));
        }

        // ---- at the post, what has passed, and science ----

        /// <summary>Goods on this half: its colony's, waiting to cross, or the other colony's, waiting to be hauled away.</summary>
        private void RefreshDock(DistrictCrossing half)
        {
            var goods = new List<KeyValuePair<string, int>>();
            Inventory inventory = half.GetComponent<DistrictCrossingInventory>()?.Inventory;
            if (inventory != null)
            {
                var stock = inventory.Stock;
                for (int i = 0; i < stock.Count; i++)
                {
                    GoodAmount good = stock[i];
                    if (good.Amount > 0) goods.Add(new KeyValuePair<string, int>(good.GoodId, good.Amount));
                }
            }
            NativeElements.Show(dockRow.Root, goods.Count > 0);
            if (goods.Count > 0) ShowChips(dockRow, T("BeaverBuddies.Colony.Trade.AtThisPost"), goods.OrderByDescending(g => g.Value).ToList());
        }

        private void RefreshHistory(bool mine, int aSlot, int bSlot)
        {
            ColonyTradeLedger ledger = ColonyTradeLedger.Instance;
            NativeElements.SetText(historyTitle, mine
                ? string.Format(T("BeaverBuddies.Colony.Trade.TradedWith"), PlainName(bSlot))
                : T("BeaverBuddies.Colony.Trade.TradedBetween"));
            ShowChips(sentRow, mine ? T("BeaverBuddies.Colony.Trade.YouSent") : string.Format(T("BeaverBuddies.Colony.Trade.SentBy"), PlainName(aSlot)),
                ledger?.Sent(aSlot, bSlot));
            ShowChips(receivedRow, mine ? T("BeaverBuddies.Colony.Trade.YouReceived") : string.Format(T("BeaverBuddies.Colony.Trade.SentBy"), PlainName(bSlot)),
                ledger?.Sent(bSlot, aSlot));
        }

        /// <summary>A caption and the goods as icons with their amounts, most first; rebuilt only when they change.</summary>
        private void ShowChips(ChipRow row, string caption, List<KeyValuePair<string, int>> goods)
        {
            NativeElements.SetText(row.Caption, caption);
            goods = goods ?? new List<KeyValuePair<string, int>>();
            string shown = string.Join("|", goods.Take(ChipsShown).Select(g => g.Key + ":" + g.Value)) + "|" + goods.Count;
            if (shown == row.Shown) return;
            row.Shown = shown;
            row.Chips.Clear();
            if (goods.Count == 0)
            {
                row.Chips.Add(NativeElements.MutedText(T("BeaverBuddies.Colony.Trade.Nothing")));
                return;
            }
            foreach (var good in goods.Take(ChipsShown))
            {
                VisualElement chip = NativeElements.Row();
                chip.style.marginRight = 8;
                chip.style.height = 20;
                Image icon = NativeElements.Icon(18);
                icon.sprite = _items.IconOf(good.Key);
                icon.style.marginRight = 2;
                chip.Add(icon);
                chip.Add(NativeElements.Text(Count(good.Value), 12));
                string name = _items.Name(good.Key);
                _tooltipRegistrar.Register(chip, name);
                row.Chips.Add(chip);
            }
            if (goods.Count > ChipsShown)
                row.Chips.Add(NativeElements.MutedText(string.Format(T("BeaverBuddies.Colony.Trade.MoreChips"), goods.Count - ChipsShown)));
        }

        private void RefreshGift(bool mine, int aSlot)
        {
            bool science = mine && ColonyScienceService.IsEnabled;
            NativeElements.Show(giftRow, science);
            if (!science) return;
            int points = ColonyScienceService.Instance?.PointsOf(aSlot) ?? 0;
            NativeElements.SetText(giftPoints, string.Format(T("BeaverBuddies.Colony.Trade.GiftYouHave"), Count(points)));
            foreach (var (amount, button) in giftButtons) button.SetEnabled(points >= amount);
        }

        // ---- actions ----

        private void OpenOverview()
        {
            if (TradeOverviewPanel.Instance?.Open() != true) Notice(T("BeaverBuddies.Colony.Trade.HostFirst"));
        }

        private void Propose()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf) return;
            TradeOfferForm.Verdict verdict = TradeOfferForm.Judge(giveItem, giveSide.Amount.value, getItem, getSide.Amount.value,
                out int give, out int get);
            if (!TradeOfferForm.IsOffer(verdict) || !ExchangeTerms.AreValid(giveItem, give, getItem, get)) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            string giving = ExchangeTerms.GoodOf(giveItem, give), asking = ExchangeTerms.GoodOf(getItem, get);
            bool repeating = repeatToggle.value;
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

        /// <summary>"250 Gears", or "nothing" for a side that gives nothing.</summary>
        private string AmountOf(int amount, string item) =>
            ColonyExchangeService.Instance?.Amount(amount, item) ?? $"{amount} {_items.Name(item)}";

        private static string Gives(int slot) => string.Format(T("BeaverBuddies.Colony.Trade.Gives"), PlainName(slot));

        private static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string PlainName(int slot) => NativeElements.Plain(ColonyExchangeService.ColonyName(slot));

        /// <summary>A colony's name in bold in its player's color, lightened where it would be hard to read.</summary>
        private static string ColoredName(int slot)
        {
            string name = PlainName(slot);
            if (slot < 0 || slot >= StartingLocationPlayer.PLAYER_COLORS.Length) return "<b>" + name + "</b>";
            string hex = ChatFormat.ReadableHex(ColorUtility.ToHtmlStringRGB(StartingLocationPlayer.PLAYER_COLORS[slot]));
            return $"<color=#{hex}><b>{name}</b></color>";
        }

        private static string T(string key) => RegisteredLocalizationService.T(key);
    }
}
