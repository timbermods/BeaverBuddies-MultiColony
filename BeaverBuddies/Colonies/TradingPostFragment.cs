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
using Timberborn.SelectionSystem;
using Timberborn.TooltipSystem;
using Timberborn.UIFormatters;
using Timberborn.WorkSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The Trading Post's panel, built from the game's own panel pieces so it reads as part of the game. On the player's
    /// own half: who they trade with, then the exchange (the offer form, an offer waiting for an answer, or each side's
    /// round under way, with ending it by agreement), what waits on the half, the post's ledger of rounds that crossed,
    /// and the totals traded between the two colonies. On the other colony's half it only says whose half it is and
    /// leads to the player's own. A Trading Post not yet between two colonies says what it is waiting for; a District
    /// Crossing that ends up joining two colonies says it moves nothing between them. It scrolls rather than run off a
    /// short screen. Display and buttons only; each button sends an ordinary action that every computer plays.
    /// </summary>
    public class TradingPostFragment : IEntityPanelFragment
    {
        // The form's two sides.
        private const int Give = 1, Get = 2;
        private const float RefreshInterval = 0.5f;
        // The panel keeps this far from the screen's bottom edge, and is never squeezed smaller than this.
        private const float ScreenMargin = 12, MinHeight = 150;
        private const int ChipsShown = 5;
        private const int LedgerShown = 8;
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
            public Label Caption, Note, Text;
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
        private readonly EntitySelectionService _entitySelectionService;
        private readonly TimestampFormatter _timestampFormatter;

        private NineSliceVisualElement root;
        private Label headerText;
        private Button allPostsButton;
        private ScrollView body;
        private TradingPostGoodPicker picker;
        // Why there is no trading here, or whose half this is; with the way to the player's own half.
        private Label noticeLabel;
        private Button myHalfButton;
        // Making an offer.
        private VisualElement compose;
        private OfferSide giveSide, getSide;
        private TextField roundsBox;
        private Button roundsLess, roundsMore;
        private Label roundsNote;
        private Label summary;
        private Toggle repeatToggle;
        private Button makeOfferButton;
        // An offer waiting for an answer.
        private VisualElement proposal, answerButtons;
        private Label proposalTitle, proposalNote, proposalRounds;
        private TermRow firstTerm, secondTerm;
        private Button acceptButton, declineButton, withdrawButton;
        // An exchange under way.
        private VisualElement active, cancelArea, cancelButtons;
        private Label activeTitle, activeNote, statusLabel, cancelLabel;
        private ProgressRow giveProgress, getProgress;
        private Button askCancelButton, agreeCancelButton, keepButton, endButton;
        // What waits on the half, the post's ledger, and the two colonies' totals.
        private ChipRow dockRow;
        private VisualElement ledgerSection, ledgerRows, historySection;
        private Label ledgerTitle, historyTitle;
        private ChipRow sentRow, receivedRow;
        private string ledgerShown;

        private DistrictCrossing crossing;
        private float nextRefresh;
        private float fittedHeight = -1;
        private int partnerSlot = -1;

        // The offer being written: kept while the panel is open on other crossings too.
        private string giveItem, getItem;

        // The exchange as last shown: Accept and Cancel act on exactly this, never on something that changed since.
        private int shownSerial;
        private string shownGiveGood, shownGetGood;
        private int shownGiveAmount, shownGetAmount, shownRounds;
        private bool shownRepeat;

        public TradingPostFragment(TradeItems items, VisualElementInitializer visualElementInitializer,
            ITooltipRegistrar tooltipRegistrar, InputService inputService, EntitySelectionService entitySelectionService,
            TimestampFormatter timestampFormatter)
        {
            _items = items;
            _visualElementInitializer = visualElementInitializer;
            _tooltipRegistrar = tooltipRegistrar;
            _inputService = inputService;
            _entitySelectionService = entitySelectionService;
            _timestampFormatter = timestampFormatter;
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
            allPostsButton = SmallButton(T("BeaverBuddies.Colony.Trade.AllPostsShort"), OpenOverview);
            allPostsButton.style.marginLeft = 6;
            _tooltipRegistrar.RegisterWithKeyBinding(allPostsButton, T("BeaverBuddies.Colony.Trade.AllPostsTooltip"), TradeOverviewPanel.KeyBindingId);
            header.Add(allPostsButton);
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

            noticeLabel = RichText(13);
            noticeLabel.style.marginTop = 6;
            body.Add(noticeLabel);
            myHalfButton = NativeElements.WoodenButton(T("BeaverBuddies.Colony.Trade.SelectMyHalf"), SelectMyHalf);
            myHalfButton.style.marginTop = 6;
            body.Add(myHalfButton);
            body.Add(BuildCompose());
            body.Add(BuildProposal());
            body.Add(BuildActive());
            dockRow = BuildChipRow();
            dockRow.Root.style.marginTop = 6;
            _tooltipRegistrar.Register(dockRow.Root, T("BeaverBuddies.Colony.Trade.OnThisHalfTooltip"));
            body.Add(dockRow.Root);
            body.Add(BuildLedger());
            body.Add(BuildHistory());

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
            compose.Add(BuildRounds());
            summary = RichText(12);
            summary.style.marginTop = 6;
            summary.style.marginLeft = 1;
            compose.Add(summary);
            makeOfferButton = NativeElements.WoodenButton(T("BeaverBuddies.Colony.Trade.Propose"), Propose);
            makeOfferButton.style.marginTop = 6;
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
            o.Amount = NativeElements.InputBox(maxLength: 3, width: 44);
            o.Amount.value = ExchangeTerms.MaxAmount.ToString(CultureInfo.InvariantCulture);
            o.Amount.RegisterValueChangedCallback(_ => RefreshSummary());
            _tooltipRegistrar.Register(o.Amount, () => string.Format(T("BeaverBuddies.Colony.Trade.AmountTooltip"), ExchangeTerms.MaxAmount));
            row.Add(o.Amount);
            Button plus = NativeElements.SquareButton(plus: true, e => StepAmount(o, up: true, e.shiftKey));
            _tooltipRegistrar.Register(plus, () => StepTooltip(o, "BeaverBuddies.Colony.Trade.StepMore"));
            row.Add(plus);
            o.Card.Add(row);
            return o;
        }

        /// <summary>"Rounds" [−] [3] [+] with the check box for a standing deal, on the description's blue.</summary>
        private VisualElement BuildRounds()
        {
            NineSliceVisualElement card = Card();
            VisualElement top = NativeElements.Row();
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.marginBottom = 3;
            top.Add(NativeElements.Caption(T("BeaverBuddies.Colony.Trade.RoundsCaption")));
            roundsNote = NativeElements.MutedText(string.Format(T("BeaverBuddies.Colony.Trade.RoundsNote"), ExchangeTerms.MaxAmount));
            roundsNote.style.unityTextAlign = TextAnchor.MiddleRight;
            roundsNote.style.flexShrink = 1;
            roundsNote.style.marginLeft = 6;
            top.Add(roundsNote);
            card.Add(top);

            VisualElement row = NativeElements.Row();
            roundsLess = NativeElements.SquareButton(plus: false, e => StepRounds(up: false, e.shiftKey));
            _tooltipRegistrar.Register(roundsLess, () => string.Format(T("BeaverBuddies.Colony.Trade.StepLess"), TradeOfferForm.RoundsStep(false), TradeOfferForm.RoundsStep(true)));
            row.Add(roundsLess);
            roundsBox = NativeElements.InputBox(maxLength: 2, width: 36);
            roundsBox.value = "1";
            roundsBox.RegisterValueChangedCallback(_ => RefreshSummary());
            _tooltipRegistrar.Register(roundsBox, () => string.Format(T("BeaverBuddies.Colony.Trade.RoundsTooltip"), ExchangeTerms.MaxRounds));
            row.Add(roundsBox);
            roundsMore = NativeElements.SquareButton(plus: true, e => StepRounds(up: true, e.shiftKey));
            _tooltipRegistrar.Register(roundsMore, () => string.Format(T("BeaverBuddies.Colony.Trade.StepMore"), TradeOfferForm.RoundsStep(false), TradeOfferForm.RoundsStep(true)));
            row.Add(roundsMore);
            repeatToggle = NativeElements.CheckBox(T("BeaverBuddies.Colony.Trade.RepeatToggle"));
            repeatToggle.style.marginLeft = 10;
            repeatToggle.style.flexShrink = 1;
            repeatToggle.RegisterValueChangedCallback(_ => RefreshSummary());
            _tooltipRegistrar.Register(repeatToggle, T("BeaverBuddies.Colony.Trade.RepeatTooltip"));
            row.Add(repeatToggle);
            card.Add(row);
            return card;
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
            proposalRounds = NativeElements.MutedText();
            proposalRounds.style.marginTop = 3;
            card.Add(proposalRounds);
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
            giveProgress = BuildProgressRow(green: false);
            getProgress = BuildProgressRow(green: true);
            card.Add(giveProgress.Root);
            card.Add(getProgress.Root);
            statusLabel = RichText(12);
            statusLabel.style.marginTop = 5;
            card.Add(statusLabel);
            active.Add(card);

            // Ending an exchange takes both colonies, as agreeing to it did.
            cancelArea = new VisualElement();
            cancelArea.style.marginTop = 6;
            cancelLabel = RichText(12);
            cancelLabel.style.marginBottom = 4;
            cancelArea.Add(cancelLabel);
            cancelButtons = NativeElements.Row();
            askCancelButton = NativeElements.RedButton(T("BeaverBuddies.Colony.Trade.AskCancel"), Cancel);
            _tooltipRegistrar.Register(askCancelButton, () => string.Format(T("BeaverBuddies.Colony.Trade.AskCancelTooltip"), PlainName(partnerSlot)));
            agreeCancelButton = NativeElements.RedButton(T("BeaverBuddies.Colony.Trade.AgreeCancel"), Cancel);
            _tooltipRegistrar.Register(agreeCancelButton, T("BeaverBuddies.Colony.Trade.AgreeCancelTooltip"));
            keepButton = NativeElements.WoodenButton(T("BeaverBuddies.Colony.Trade.KeepTrading"), Keep);
            endButton = NativeElements.RedButton(T("BeaverBuddies.Colony.Trade.EndExchange"), Cancel);
            _tooltipRegistrar.Register(endButton, T("BeaverBuddies.Colony.Trade.EndExchangeTooltip"));
            foreach (Button button in new[] { askCancelButton, agreeCancelButton, keepButton, endButton })
            {
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                cancelButtons.Add(button);
            }
            keepButton.style.marginLeft = 6;
            cancelArea.Add(cancelButtons);
            active.Add(cancelArea);
            return active;
        }

        private VisualElement BuildLedger()
        {
            ledgerSection = new VisualElement();
            ledgerSection.Add(NativeElements.Rule());
            ledgerTitle = NativeElements.Caption(T("BeaverBuddies.Colony.Trade.LedgerTitle"));
            ledgerTitle.style.marginBottom = 2;
            _tooltipRegistrar.Register(ledgerTitle, T("BeaverBuddies.Colony.Trade.LedgerTooltip"));
            ledgerSection.Add(ledgerTitle);
            ledgerRows = new VisualElement();
            ledgerSection.Add(ledgerRows);
            return ledgerSection;
        }

        private VisualElement BuildHistory()
        {
            historySection = new VisualElement();
            historySection.Add(NativeElements.Rule());
            historyTitle = NativeElements.Caption();
            historyTitle.style.marginBottom = 2;
            historySection.Add(historyTitle);
            sentRow = BuildChipRow();
            receivedRow = BuildChipRow();
            historySection.Add(sentRow.Root);
            historySection.Add(receivedRow.Root);
            return historySection;
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
            VisualElement top = NativeElements.Row();
            top.style.justifyContent = Justify.SpaceBetween;
            progress.Caption = NativeElements.Caption();
            progress.Note = NativeElements.MutedText();
            progress.Note.style.marginLeft = 6;
            progress.Note.style.flexShrink = 1;
            progress.Note.style.unityTextAlign = TextAnchor.MiddleRight;
            top.Add(progress.Caption);
            top.Add(progress.Note);
            progress.Root.Add(top);
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
        /// Leaves the input boxes. The game switches its hotkeys off while a player types in a box and on again when the
        /// box loses focus; a box hidden while it has focus might never lose it.
        /// </summary>
        private void StopTyping()
        {
            giveSide?.Amount.Blur();
            getSide?.Amount.Blur();
            roundsBox?.Blur();
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

        private static int OwnerOf(DistrictCrossing half) => ColonyExchangeService.OwnerOf(half);

        /// <summary>The selected half, when it is in the local player's colony; else null.</summary>
        private DistrictCrossing MyHalf()
        {
            int localSlot = ColonySession.LocalSlot;
            return crossing && localSlot >= 0 && OwnerOf(crossing) == localSlot ? crossing : null;
        }

        private void RefreshUnsafe()
        {
            bool tradingPost = crossing && TradingPosts.IsTradingPostBuilding(crossing);
            bool strayCrossing = crossing && !tradingPost && TradingPosts.JoinsTwoColonies(crossing);
            if (!tradingPost && !strayCrossing)
            {
                picker.Close();
                StopTyping();
                root.style.display = DisplayStyle.None;
                return;
            }
            NativeElements.Show(root, true);
            DistrictCrossing mine = MyHalf();
            DistrictCrossing partner = TradingPosts.Partner(crossing);
            bool trading = tradingPost && TradingPosts.IsTradingPost(crossing);
            CrossingExchange ax = ColonyExchangeService.Of(crossing), bx = ColonyExchangeService.Of(partner);
            ExchangeState state = ax != null && bx != null ? ax.State : ExchangeState.None;
            // An exchange at a post that stopped joining the two colonies waits; its colony may still end it (or withdraw
            // or decline an offer).
            bool paused = tradingPost && !trading && mine != null && state != ExchangeState.None;
            partnerSlot = OwnerOf(partner);

            bool ownTrading = trading && mine != null;
            NativeElements.Show(allPostsButton, tradingPost);
            NativeElements.Show(myHalfButton, false);
            if (!ownTrading)
            {
                foreach (VisualElement section in new[] { compose, dockRow.Root, ledgerSection, historySection })
                    NativeElements.Show(section, false);
                picker.Close();
                StopTyping();
                NativeElements.Show(noticeLabel, true);
                ShowNotice(strayCrossing, trading, partner);
                NativeElements.Show(active, paused && state == ExchangeState.Active);
                NativeElements.Show(proposal, paused && state == ExchangeState.Proposed);
                if (paused)
                {
                    Remember(state, ax, bx);
                    if (state == ExchangeState.Active) RefreshActive(ax, bx, OwnerOf(crossing), partnerSlot, paused: true);
                    else RefreshProposal(ax, bx, canAccept: false);
                }
                FitToScreen();
                return;
            }

            NativeElements.Show(noticeLabel, false);
            NativeElements.SetText(headerText, string.Format(T("BeaverBuddies.Colony.Trade.TradingWith"), ColoredName(partnerSlot)));
            Remember(state, ax, bx);
            bool composing = state == ExchangeState.None;
            NativeElements.Show(compose, composing);
            NativeElements.Show(proposal, state == ExchangeState.Proposed);
            NativeElements.Show(active, state == ExchangeState.Active);
            if (!composing)
            {
                picker.Close();
                StopTyping();
            }

            int me = OwnerOf(crossing);
            if (composing) RefreshCompose(crossing, partner, me, partnerSlot);
            else if (state == ExchangeState.Proposed) RefreshProposal(ax, bx, canAccept: true);
            else RefreshActive(ax, bx, me, partnerSlot, paused: false);

            RefreshDock(crossing);
            RefreshLedger(ax);
            RefreshHistory(me, partnerSlot);
            picker.RefreshCounts();
            FitToScreen();
        }

        /// <summary>
        /// Why there is no trading from here: the other colony's half (with the way to the player's own), a player of
        /// neither colony, a Trading Post whose halves are not (yet) reached by two different colonies' roads, or a
        /// District Crossing that ended up joining two colonies (which moves nothing between them).
        /// </summary>
        private void ShowNotice(bool strayCrossing, bool trading, DistrictCrossing partner)
        {
            int a = OwnerOf(crossing), b = OwnerOf(partner), local = ColonySession.LocalSlot;
            noticeLabel.style.color = NativeElements.Muted;
            if (strayCrossing)
            {
                NativeElements.SetText(headerText, string.Format(T("BeaverBuddies.Colony.Trade.Between"), ColoredName(a), ColoredName(b)));
                NativeElements.SetText(noticeLabel, T("BeaverBuddies.Colony.Trade.CrossingBetweenColonies"));
                noticeLabel.style.color = NativeElements.Warning;
                return;
            }
            if (trading)
            {
                if (local >= 0 && b == local)
                {
                    // The other colony's half of a post the player trades at.
                    NativeElements.SetText(headerText, string.Format(T("BeaverBuddies.Colony.Trade.TheirHalfTitle"), ColoredName(a)));
                    NativeElements.SetText(noticeLabel, string.Format(T("BeaverBuddies.Colony.Trade.TheirHalf"), PlainName(a)));
                    NativeElements.Show(myHalfButton, true);
                }
                else
                {
                    NativeElements.SetText(headerText, string.Format(T("BeaverBuddies.Colony.Trade.Between"), ColoredName(a), ColoredName(b)));
                    NativeElements.SetText(noticeLabel, string.Format(T("BeaverBuddies.Colony.Trade.ViewOnly"), PlainName(a), PlainName(b)));
                }
                return;
            }
            NativeElements.SetText(headerText, T("BeaverBuddies.Colony.Trade.NotTradingTitle"));
            NativeElements.SetText(noticeLabel, T(ColonyModeService.IsSeparateColonies ? "BeaverBuddies.Colony.Trade.NotLinked" : "BeaverBuddies.Colony.Trade.NoColonies"));
        }

        private void Remember(ExchangeState state, CrossingExchange ax, CrossingExchange bx)
        {
            shownSerial = state == ExchangeState.None ? 0 : ax.Serial;
            if (state == ExchangeState.None) return;
            shownGiveGood = ax.GoodId;
            shownGiveAmount = ax.Total;
            shownGetGood = bx.GoodId;
            shownGetAmount = bx.Total;
            shownRounds = ax.Rounds;
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
            bool repeat = repeatToggle.value;
            roundsBox.SetEnabled(!repeat);
            roundsLess.SetEnabled(!repeat);
            roundsMore.SetEnabled(!repeat);
            TradeOfferForm.Verdict verdict = TradeOfferForm.Judge(giveItem, giveSide.Amount.value, getItem, getSide.Amount.value,
                roundsBox.value, repeat, out int give, out int get, out int rounds);
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
                case TradeOfferForm.Verdict.BadRounds:
                    text = string.Format(T("BeaverBuddies.Colony.Trade.ErrorRounds"), ExchangeTerms.MaxRounds);
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
            if (offer) text += " " + RoundsText(rounds, repeat, give, giveItem, get, getItem);
            NativeElements.SetText(summary, text);
            summary.style.color = offer ? NativeElements.Muted : NativeElements.Warning;
            makeOfferButton.SetEnabled(offer);
        }

        /// <summary>"Once.", "3 rounds: 300 Berries for 3 Beavers in all.", or "Round after round, until you both agree to stop."</summary>
        private string RoundsText(int rounds, bool repeat, int give, string giveGood, int get, string getGood)
        {
            if (repeat) return T("BeaverBuddies.Colony.Trade.RoundsRepeat");
            if (rounds <= 1) return T("BeaverBuddies.Colony.Trade.RoundsOnce");
            return string.Format(T("BeaverBuddies.Colony.Trade.RoundsMany"), rounds, AmountOf(give * rounds, giveGood), AmountOf(get * rounds, getGood));
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

        private void StepRounds(bool up, bool shift)
        {
            if (repeatToggle.value) return;
            if (!TradeOfferForm.TryReadRounds(roundsBox.value, out int rounds)) rounds = 1;
            int next = TradeOfferForm.Stepped(rounds, TradeOfferForm.RoundsStep(shift), up, 1, ExchangeTerms.MaxRounds);
            roundsBox.value = next.ToString(CultureInfo.InvariantCulture);
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

        private void RefreshProposal(CrossingExchange ax, CrossingExchange bx, bool canAccept)
        {
            bool offeredHere = ax.ProposedHere;
            if (offeredHere)
            {
                NativeElements.SetText(proposalTitle, T("BeaverBuddies.Colony.Trade.YourOfferTitle"));
                NativeElements.SetText(proposalNote, string.Format(T("BeaverBuddies.Colony.Trade.WaitingFor"), PlainName(partnerSlot)));
                ShowTerm(firstTerm, T("BeaverBuddies.Colony.Trade.YouGiveCaption"), ax, "");
                ShowTerm(secondTerm, T("BeaverBuddies.Colony.Trade.YouGetCaption"), bx, "");
            }
            else
            {
                NativeElements.SetText(proposalTitle, string.Format(T("BeaverBuddies.Colony.Trade.TheyOfferTitle"), ColoredName(partnerSlot)));
                NativeElements.SetText(proposalNote, "");
                ShowTerm(firstTerm, T("BeaverBuddies.Colony.Trade.YouGetCaption"), bx, "");
                // Whether the colony can keep its side.
                string have = ax.Total > 0
                    ? string.Format(T("BeaverBuddies.Colony.Trade.YouHaveNote"), Count(_items.StockOf(crossing, OwnerOf(crossing), ax.GoodId)))
                    : "";
                ShowTerm(secondTerm, T("BeaverBuddies.Colony.Trade.YouGiveCaption"), ax, have);
            }
            NativeElements.SetText(proposalRounds, RoundsText(ax.Rounds, ax.Repeat, ax.Total, ax.GoodId, bx.Total, bx.GoodId));
            NativeElements.Show(acceptButton, !offeredHere && canAccept);
            NativeElements.Show(declineButton, !offeredHere);
            NativeElements.Show(withdrawButton, offeredHere);
            declineButton.style.marginLeft = canAccept ? 6 : 0;
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

        private void RefreshActive(CrossingExchange ax, CrossingExchange bx, int me, int them, bool paused)
        {
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;
            DistrictCrossing partner = TradingPosts.Partner(crossing);
            NativeElements.SetText(activeTitle, T(paused ? "BeaverBuddies.Colony.Trade.PausedTitle" : "BeaverBuddies.Colony.Trade.UnderWayTitle"));
            NativeElements.SetText(activeNote, ax.Repeat
                ? string.Format(T("BeaverBuddies.Colony.Trade.RoundRepeating"), ax.Done + 1)
                : string.Format(T("BeaverBuddies.Colony.Trade.RoundOf"), ax.Done + 1, ax.Rounds));
            bool mineIn = ShowProgress(giveProgress, T("BeaverBuddies.Colony.Trade.YouGiveCaption"), crossing, ax, exchanges, own: true);
            bool theirsIn = ShowProgress(getProgress, T("BeaverBuddies.Colony.Trade.YouGetCaption"), partner, bx, exchanges, own: false);
            NativeElements.SetText(statusLabel, paused ? T("BeaverBuddies.Colony.Trade.PausedStatus") : Status(ax, bx, mineIn, theirsIn, me, them, exchanges));
            statusLabel.style.color = paused ? NativeElements.Warning : NativeElements.Muted;
            RefreshCancel(ax, bx, paused);
        }

        /// <summary>
        /// One side of the round under way: goods as waiting on its half out of the round's amount; science and beavers
        /// as how much of it its colony can give now. True when the side is in.
        /// </summary>
        private bool ShowProgress(ProgressRow row, string caption, DistrictCrossing half, CrossingExchange side,
            ColonyExchangeService exchanges, bool own)
        {
            // A side that gives nothing (a gift the other way) has nothing to show.
            NativeElements.Show(row.Root, side.Total > 0);
            if (side.Total <= 0) return true;
            NativeElements.SetText(row.Caption, caption);
            Sprite icon = _items.IconOf(side.GoodId);
            if (row.Icon.sprite != icon) row.Icon.sprite = icon;
            int have;
            string note;
            if (side.GoodId == ExchangeTerms.Science)
            {
                have = ColonyExchangeService.ScienceToSpare(OwnerOf(half));
                note = T("BeaverBuddies.Colony.Trade.NoteScience");
            }
            else if (side.GoodId == ExchangeTerms.Beavers)
            {
                have = exchanges?.BeaversToSpare(half) ?? 0;
                note = T("BeaverBuddies.Colony.Trade.NoteBeavers");
            }
            else
            {
                have = side.Held;
                note = T(own ? "BeaverBuddies.Colony.Trade.NoteOnYourHalf" : "BeaverBuddies.Colony.Trade.NoteOnTheirHalf");
            }
            int shown = Math.Min(have, side.Total);
            row.Bar.SetProgress((float)shown / side.Total);
            NativeElements.SetText(row.Note, note);
            NativeElements.SetText(row.Text, string.Format(T("BeaverBuddies.Colony.Trade.ProgressText"), Count(shown), Count(side.Total),
                _items.Name(side.GoodId)));
            return exchanges?.IsIn(half, side) ?? false;
        }

        /// <summary>What the round waits for now, in one line.</summary>
        private string Status(CrossingExchange ax, CrossingExchange bx, bool mineIn, bool theirsIn, int me, int them,
            ColonyExchangeService exchanges)
        {
            string partner = PlainName(them);
            if (ax.CancelAsked || bx.CancelAsked) return T("BeaverBuddies.Colony.Trade.StatusOnHold");
            if (!mineIn)
            {
                if (ax.GoodId == ExchangeTerms.Science)
                    return string.Format(T("BeaverBuddies.Colony.Trade.StatusYourScience"), Count(ax.Total - ColonyExchangeService.ScienceToSpare(me)));
                if (ax.GoodId == ExchangeTerms.Beavers)
                    return string.Format(T("BeaverBuddies.Colony.Trade.StatusYourBeavers"), Count(ax.Total));
                if (crossing.GetComponent<Workplace>()?.NumberOfAssignedWorkers == 0) return T("BeaverBuddies.Colony.Trade.StatusNoWorkers");
                return string.Format(T("BeaverBuddies.Colony.Trade.StatusYouBring"), Count(ax.Total - ax.Held), _items.Name(ax.GoodId));
            }
            if (!theirsIn)
            {
                if (ExchangeTerms.IsSpecial(bx.GoodId))
                    return string.Format(T("BeaverBuddies.Colony.Trade.StatusTheirSpecial"), partner, _items.Name(bx.GoodId));
                return string.Format(T("BeaverBuddies.Colony.Trade.StatusTheyBring"), partner, Count(bx.Total - bx.Held), _items.Name(bx.GoodId));
            }
            return T("BeaverBuddies.Colony.Trade.StatusCrossing");
        }

        /// <summary>
        /// Ending the exchange takes both colonies: ask, withdraw the request, or answer the other colony's. A paused
        /// exchange (the post no longer joins the two) can be ended by its colony alone.
        /// </summary>
        private void RefreshCancel(CrossingExchange ax, CrossingExchange bx, bool paused)
        {
            string partner = PlainName(partnerSlot);
            bool iAsked = ax.CancelAsked, theyAsked = bx.CancelAsked;
            string text = paused ? T("BeaverBuddies.Colony.Trade.PausedCancel")
                : theyAsked ? string.Format(T("BeaverBuddies.Colony.Trade.TheyAskCancel"), ColoredName(partnerSlot))
                : iAsked ? string.Format(T("BeaverBuddies.Colony.Trade.YouAskedCancel"), partner)
                : "";
            NativeElements.SetText(cancelLabel, text);
            NativeElements.Show(cancelLabel, text.Length > 0);
            cancelLabel.style.color = theyAsked ? NativeElements.Warning : NativeElements.Muted;
            NativeElements.Show(endButton, paused);
            NativeElements.Show(askCancelButton, !paused && !iAsked && !theyAsked);
            NativeElements.Show(agreeCancelButton, !paused && theyAsked);
            NativeElements.Show(keepButton, !paused && (iAsked || theyAsked));
            keepButton.style.marginLeft = theyAsked ? 6 : 0;
        }

        // ---- what waits on the half, the ledger, and the colonies' totals ----

        /// <summary>Goods on this half: its colony's, held for the round, or the other colony's, waiting to be hauled away.</summary>
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
            if (goods.Count > 0) ShowChips(dockRow, T("BeaverBuddies.Colony.Trade.OnThisHalf"), goods.OrderByDescending(g => g.Value).ToList());
        }

        /// <summary>The rounds that crossed at this post, newest first: when, what the colony gave and what it got.</summary>
        private void RefreshLedger(CrossingExchange side)
        {
            NativeElements.Show(ledgerSection, true);
            IReadOnlyList<TradeRecord> records = side?.Ledger ?? (IReadOnlyList<TradeRecord>)Array.Empty<TradeRecord>();
            string shown = records.Count + "|" + (records.Count > 0 ? records[records.Count - 1].Encode() : "");
            if (shown == ledgerShown) return;
            ledgerShown = shown;
            ledgerRows.Clear();
            if (records.Count == 0)
            {
                ledgerRows.Add(NativeElements.MutedText(T("BeaverBuddies.Colony.Trade.LedgerEmpty")));
                return;
            }
            for (int i = records.Count - 1; i >= 0 && i >= records.Count - LedgerShown; i--)
            {
                TradeRecord record = records[i];
                VisualElement row = NativeElements.Row();
                row.style.marginTop = 2;
                row.style.height = 20;
                Label date = NativeElements.MutedText(_timestampFormatter.FormatShort(record.Cycle, record.Day));
                date.style.width = 42;
                date.style.flexShrink = 0;
                _tooltipRegistrar.Register(date, _timestampFormatter.FormatLongLocalized(record.Cycle, record.Day));
                row.Add(date);
                row.Add(LedgerPart(T("BeaverBuddies.Colony.Trade.LedgerGave"), record.Gave, record.GaveAmount));
                row.Add(LedgerPart(T("BeaverBuddies.Colony.Trade.LedgerGot"), record.Got, record.GotAmount));
                ledgerRows.Add(row);
            }
            if (records.Count > LedgerShown)
                ledgerRows.Add(NativeElements.MutedText(string.Format(T("BeaverBuddies.Colony.Trade.LedgerMore"), records.Count - LedgerShown)));
        }

        /// <summary>"gave [icon] 100", or "gave nothing".</summary>
        private VisualElement LedgerPart(string caption, string item, int amount)
        {
            VisualElement part = NativeElements.Row();
            part.style.width = 110;
            part.style.flexShrink = 1;
            Label label = NativeElements.MutedText(caption);
            label.style.marginRight = 4;
            part.Add(label);
            if (amount <= 0 || item == null)
            {
                part.Add(NativeElements.Text(T("BeaverBuddies.Colony.Trade.NothingInReturn"), 12));
                return part;
            }
            Image icon = NativeElements.Icon(18);
            icon.sprite = _items.IconOf(item);
            icon.style.marginRight = 2;
            part.Add(icon);
            part.Add(NativeElements.Text(Count(amount), 12));
            _tooltipRegistrar.Register(part, AmountOf(amount, item));
            return part;
        }

        private void RefreshHistory(int me, int them)
        {
            NativeElements.Show(historySection, true);
            ColonyTradeLedger ledger = ColonyTradeLedger.Instance;
            NativeElements.SetText(historyTitle, string.Format(T("BeaverBuddies.Colony.Trade.TradedWith"), PlainName(them)));
            ShowChips(sentRow, T("BeaverBuddies.Colony.Trade.YouSent"), ledger?.Sent(me, them));
            ShowChips(receivedRow, T("BeaverBuddies.Colony.Trade.YouReceived"), ledger?.Sent(them, me));
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
                _tooltipRegistrar.Register(chip, _items.Name(good.Key));
                row.Chips.Add(chip);
            }
            if (goods.Count > ChipsShown)
                row.Chips.Add(NativeElements.MutedText(string.Format(T("BeaverBuddies.Colony.Trade.MoreChips"), goods.Count - ChipsShown)));
        }

        // ---- actions ----

        private void OpenOverview()
        {
            if (TradeOverviewPanel.Instance?.Toggle() != true) Notice(T("BeaverBuddies.Colony.Trade.HostFirst"));
        }

        /// <summary>From the other colony's half, to the player's own (which has the trading).</summary>
        private void SelectMyHalf()
        {
            DistrictCrossing partner = crossing ? TradingPosts.Partner(crossing) : null;
            if (!partner) return;
            try { _entitySelectionService.SelectAndFocusOn(partner); }
            catch (Exception error) { Plugin.LogWarning("[Colony] Could not select the other half: " + error.Message); }
        }

        private void Propose()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf) return;
            bool repeating = repeatToggle.value;
            TradeOfferForm.Verdict verdict = TradeOfferForm.Judge(giveItem, giveSide.Amount.value, getItem, getSide.Amount.value,
                roundsBox.value, repeating, out int give, out int get, out int rounds);
            if (!TradeOfferForm.IsOffer(verdict) || !ExchangeTerms.AreValid(giveItem, give, getItem, get)) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            string giving = ExchangeTerms.GoodOf(giveItem, give), asking = ExchangeTerms.GoodOf(getItem, get);
            Send(() => new ExchangeProposedEvent
            {
                crossingID = halfId, giveGood = giving, giveAmount = give, getGood = asking, getAmount = get, rounds = rounds,
                repeat = repeating,
            });
        }

        private void Accept()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf || shownSerial == 0) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            // The terms as this player saw them on the panel: an offer changed in the meantime is not accepted.
            int serial = shownSerial, giveAmount = shownGiveAmount, getAmount = shownGetAmount, rounds = shownRounds;
            string giveId = shownGiveGood, getId = shownGetGood;
            bool repeating = shownRepeat;
            Send(() => new ExchangeAcceptedEvent
            {
                crossingID = halfId, serial = serial, giveGood = giveId, giveAmount = giveAmount, getGood = getId,
                getAmount = getAmount, rounds = rounds, repeat = repeating,
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

        private void Keep()
        {
            DistrictCrossing myHalf = MyHalf();
            if (!myHalf || shownSerial == 0) return;
            string halfId = ReplayEvent.GetEntityID(myHalf);
            int serial = shownSerial;
            Send(() => new ExchangeKeptEvent { crossingID = halfId, serial = serial });
        }

        /// <summary>Sends an action through the host. Trading exists only in a hosted game.</summary>
        private void Send(Func<ReplayEvent> action)
        {
            if (ReplayEvent.DoPrefix(action)) Notice(T("BeaverBuddies.Colony.Trade.HostFirst"));
            nextRefresh = 0;
        }

        private static void Notice(string text) => SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(text);

        // ---- text ----

        /// <summary>"100 Planks", or "nothing" for a side that gives nothing.</summary>
        private string AmountOf(int amount, string item) =>
            ColonyExchangeService.Instance?.Amount(amount, item) ?? $"{amount} {_items.Name(item)}";

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
