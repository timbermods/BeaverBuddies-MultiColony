using BeaverBuddies.Editor;
using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Panel;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// One window for trade and colonies, drawn as the game draws its own boxes (the framed box with a title badge and a
    /// close button, as the population's well-being): every Trading Post of the player's colony with its exchange and a
    /// button to go there, and every colony with its player, population and whether it is being played. The host also
    /// finds here the buttons to hand a colony over (only a colony whose player is away, or which has no beavers left).
    /// It opens and closes with Ctrl+T, the square Trade button at the top right, or "All posts" on a Trading Post, and
    /// closes with its close button or Esc. It does not pause the game (pausing is shared in co-op). Display and buttons
    /// only: each button sends an ordinary action.
    /// </summary>
    public class TradeOverviewPanel : IPostLoadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        public const string KeyBindingId = "BeaverBuddies.KeyBind.TradeOverview";
        // The Trade button's icon, drawn like the game's own top-right buttons' (a file of this mod).
        private const string ToggleIconPath = "UI/Images/BeaverBuddies/square-toggle-trade";
        private const float Top = 110, BottomMargin = 40, Width = 470;

        private readonly UILayout _uiLayout;
        private readonly InputService _inputService;
        private readonly VisualElementInitializer _visualElementInitializer;
        private readonly VisualElementLoader _visualElementLoader;
        private readonly IAssetLoader _assetLoader;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private readonly EntitySelectionService _entitySelectionService;

        private VisualElement window, postsList, coloniesList;
        private NineSliceVisualElement box;
        private ScrollView scroll;
        private Label postsTitle, emptyPosts;
        private VisualElement topButton;
        private Toggle topToggle;
        private bool open;
        private float nextRefresh;
        // What the lists show, so they are rebuilt only when that changes (a rebuild would swallow a click).
        private string postsShape, coloniesShape;
        private readonly Dictionary<string, (Label title, Label detail)> postLabels = new Dictionary<string, (Label, Label)>();
        private readonly Dictionary<int, (Label title, Label detail)> colonyLabels = new Dictionary<int, (Label, Label)>();

        public static TradeOverviewPanel Instance { get; private set; }

        public TradeOverviewPanel(UILayout uiLayout, InputService inputService, VisualElementInitializer visualElementInitializer,
            VisualElementLoader visualElementLoader, IAssetLoader assetLoader, ITooltipRegistrar tooltipRegistrar,
            EntityComponentRegistry entityComponentRegistry, EntitySelectionService entitySelectionService)
        {
            _uiLayout = uiLayout;
            _inputService = inputService;
            _visualElementInitializer = visualElementInitializer;
            _visualElementLoader = visualElementLoader;
            _assetLoader = assetLoader;
            _tooltipRegistrar = tooltipRegistrar;
            _entityComponentRegistry = entityComponentRegistry;
            _entitySelectionService = entitySelectionService;
        }

        public void PostLoad()
        {
            Instance = this;
            _inputService.AddInputProcessor(this);
            try
            {
                Build();
            }
            catch (Exception error)
            {
                Plugin.LogError("[Colony] Could not build the trading posts window: " + error);
                window = null;
            }
            try
            {
                BuildTopButton();
            }
            catch (Exception error)
            {
                Plugin.LogError("[Colony] Could not add the Trade button: " + error);
                topButton = null;
            }
        }

        private static bool Available => ColonyModeService.IsSeparateColonies && !EventIO.IsNull && ColonySession.LocalSlot >= 0;

        public bool ProcessInput()
        {
            if (_inputService.IsKeyDown(KeyBindingId) && Available)
            {
                Toggle();
                return true;
            }
            if (open && _inputService.UICancel)
            {
                Close();
                return true;
            }
            return false;
        }

        /// <summary>Opens the window, or closes it when open; false where it cannot open (no co-op session, separate colonies off).</summary>
        public bool Toggle()
        {
            if (open)
            {
                Close();
                return true;
            }
            return Open();
        }

        /// <summary>Opens the window; false where it cannot open (no co-op session, separate colonies off).</summary>
        public bool Open()
        {
            if (window == null || !Available) return false;
            open = true;
            window.style.display = DisplayStyle.Flex;
            topToggle?.SetValueWithoutNotify(true);
            nextRefresh = 0;
            Refresh();
            return true;
        }

        public void Close()
        {
            open = false;
            if (window != null) window.style.display = DisplayStyle.None;
            topToggle?.SetValueWithoutNotify(false);
        }

        public void UpdateSingleton()
        {
            if (topButton != null) NativeElements.Show(topButton, Available);
            if (!open || window == null) return;
            if (!Available)
            {
                Close();
                return;
            }
            if (Time.unscaledTime < nextRefresh) return;
            Refresh();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + 1f;
            try
            {
                FitToScreen();
                RefreshLists();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Trading posts window: " + error.Message);
                Close();
            }
        }

        // ---- building ----

        /// <summary>
        /// The game's box (CoreStyle): a framed panel (sliced-border, box__content-container), a title badge on its top
        /// edge (capsule-header), the round close button at its corner (close-button), and a scrolling list inside.
        /// </summary>
        private void Build()
        {
            // A strip across the screen that only places the box: clicks beside the box go to the game.
            window = new VisualElement { pickingMode = PickingMode.Ignore };
            var s = window.style;
            s.position = Position.Absolute;
            s.left = 0;
            s.right = 0;
            s.top = Top;
            s.alignItems = Align.Center;

            box = new NineSliceVisualElement();
            box.AddToClassList("sliced-border");
            box.AddToClassList("sliced-border--nontransparent");
            box.AddToClassList("box__content-container");
            box.style.width = Width;
            // The class stretches a box across its parent (box__content-container: align-self: stretch, flex-grow: 1).
            box.style.alignSelf = Align.Center;
            box.style.flexGrow = 0;
            box.style.paddingLeft = 32;
            box.style.paddingRight = 32;
            box.style.paddingBottom = 30;
            window.Add(box);

            var header = new NineSliceVisualElement();
            header.AddToClassList("capsule-header");
            header.AddToClassList("capsule-header--lower");
            header.AddToClassList("content-centered");
            var title = new Label(T("BeaverBuddies.Colony.Overview.Title"));
            title.AddToClassList("capsule-header__text");
            header.Add(title);
            box.Add(header);

            var close = new Button(Close);
            close.AddToClassList("close-button");
            _tooltipRegistrar.RegisterWithKeyBinding(close, T("BeaverBuddies.Colony.Overview.CloseTooltip"), KeyBindingId);
            box.Add(close);

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("game-scroll-view");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.style.flexShrink = 1;
            scroll.style.minHeight = 0;
            postsTitle = Heading();
            emptyPosts = NativeElements.MutedText("", 13);
            emptyPosts.style.marginTop = 4;
            postsList = new VisualElement();
            Label coloniesTitle = Heading(T("BeaverBuddies.Colony.Overview.Colonies"));
            coloniesTitle.style.marginTop = 14;
            coloniesList = new VisualElement();
            scroll.Add(postsTitle);
            scroll.Add(emptyPosts);
            scroll.Add(postsList);
            scroll.Add(coloniesTitle);
            scroll.Add(coloniesList);
            box.Add(scroll);

            VisualElement footer = NativeElements.Row();
            footer.style.marginTop = 12;
            footer.style.justifyContent = Justify.SpaceBetween;
            Label hint = NativeElements.MutedText(T("BeaverBuddies.Colony.Overview.Hint"), 12);
            hint.style.flexShrink = 1;
            hint.style.marginRight = 8;
            footer.Add(hint);
            Button report = SmallButton(T("BeaverBuddies.Colony.Overview.Report"), () => ColonyDiagnostics.Instance?.WriteReport("asked for"));
            _tooltipRegistrar.Register(report, T("BeaverBuddies.Colony.Overview.ReportTooltip"));
            footer.Add(report);
            box.Add(footer);

            _visualElementInitializer.InitializeVisualElement(window);
            window.style.display = DisplayStyle.None;
            _uiLayout.AddAbsoluteItem(window);
        }

        /// <summary>
        /// The Trade button among the game's own at the top right: the same square toggle (Common/SquareToggle, as the
        /// stockpile and water overlays), checked while the window is open.
        /// </summary>
        private void BuildTopButton()
        {
            topButton = _visualElementLoader.LoadVisualElement("Common/SquareToggle");
            topToggle = topButton.Q<Toggle>("Toggle");
            Sprite icon = _assetLoader.LoadSafe<Sprite>(ToggleIconPath);
            VisualElement checkmark = topToggle?.Q(className: "unity-toggle__checkmark");
            if (icon != null && checkmark != null) checkmark.style.backgroundImage = new StyleBackground(icon);
            else if (topToggle != null) topToggle.text = T("BeaverBuddies.Colony.Overview.Button");
            topToggle?.RegisterValueChangedCallback(change =>
            {
                if (change.newValue == open) return;
                if (change.newValue && !Open()) topToggle.SetValueWithoutNotify(false);
                else if (!change.newValue) Close();
            });
            _tooltipRegistrar.RegisterWithKeyBinding(topButton, T("BeaverBuddies.Colony.Overview.ButtonTooltip"), KeyBindingId);
            topButton.style.display = DisplayStyle.None;
            _uiLayout.AddTopRightButton(topButton, 50);
        }

        /// <summary>The box never runs off the bottom of the screen: its list scrolls instead.</summary>
        private void FitToScreen()
        {
            float screen = window.panel?.visualTree.worldBound.height ?? 0;
            if (screen > 0 && !float.IsNaN(screen)) box.style.maxHeight = Mathf.Max(220, screen - Top - BottomMargin);
        }

        // ---- the lists ----

        private void RefreshLists()
        {
            int me = ColonySession.LocalSlot;
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;

            // Trading Posts with a half in this player's colony (trading or not yet), each seen from that half.
            var posts = _entityComponentRegistry.GetEnabled<DistrictCrossing>()
                .Where(half => TradingPosts.IsTradingPostBuilding(half) && ColonyExchangeService.OwnerOf(half) == me)
                .Select(half => (key: ReplayEvent.GetEntityID(half), half))
                .Where(p => p.key != null).OrderBy(p => p.key, StringComparer.Ordinal).ToList();
            NativeElements.SetText(postsTitle, string.Format(T("BeaverBuddies.Colony.Overview.Posts"), posts.Count));
            NativeElements.SetText(emptyPosts, T("BeaverBuddies.Colony.Overview.NoPosts"));
            NativeElements.Show(emptyPosts, posts.Count == 0);
            string shape = string.Join(",", posts.Select(p => p.key));
            if (shape != postsShape)
            {
                postsShape = shape;
                postsList.Clear();
                postLabels.Clear();
                foreach (var (key, half) in posts)
                {
                    DistrictCrossing target = half;
                    postsList.Add(Card(out Label title, out Label detail,
                        SmallButton(T("BeaverBuddies.Colony.Overview.GoTo"), () => GoTo(target))));
                    postLabels[key] = (title, detail);
                }
                _visualElementInitializer.InitializeVisualElement(postsList);
            }
            foreach (var (key, half) in posts)
            {
                if (!postLabels.TryGetValue(key, out var labels)) continue;
                Describe(half, exchanges, out string title, out string detail);
                NativeElements.SetText(labels.title, title);
                NativeElements.SetText(labels.detail, detail);
            }

            // Colonies.
            ColonyLifecycle lifecycle = ColonyLifecycle.Instance;
            ColonySlotTable table = ColonySlotService.Instance?.Table;
            List<int> slots = Enumerable.Range(0, ColonySlotTable.MaxSlots)
                .Where(slot => (lifecycle?.OwnsDistrict(slot) ?? false) || (table?.Entries.Any(e => e.Slot == slot) ?? false)).ToList();
            bool host = EventIO.Get() is ServerEventIO;
            // The host's handover buttons depend on who may be handed over to whom.
            var handovers = host && lifecycle != null
                ? slots.SelectMany(from => slots.Where(to => lifecycle.HostMayHandOver(from, to)).Select(to => (from, to))).ToList()
                : new List<(int from, int to)>();
            string coloniesKey = string.Join(",", slots) + "|" + string.Join(",", handovers.Select(h => $"{h.from}>{h.to}"));
            if (coloniesKey != coloniesShape)
            {
                coloniesShape = coloniesKey;
                coloniesList.Clear();
                colonyLabels.Clear();
                foreach (int slot in slots)
                {
                    var buttons = handovers.Where(h => h.from == slot)
                        .Select(h => SmallButton(string.Format(T("BeaverBuddies.Colony.Overview.HandTo"), NativeElements.Plain(ColonyExchangeService.ColonyName(h.to))),
                            () => HandOver(h.from, h.to)))
                        .ToArray();
                    coloniesList.Add(Card(out Label title, out Label detail, buttons));
                    colonyLabels[slot] = (title, detail);
                }
                _visualElementInitializer.InitializeVisualElement(coloniesList);
            }
            List<int> present = ColonyLifecycle.PresentSlots();
            foreach (int slot in slots)
            {
                if (!colonyLabels.TryGetValue(slot, out var labels)) continue;
                NativeElements.SetText(labels.title, ColoredName(slot) + (slot == me ? " " + T("BeaverBuddies.Colony.Overview.You") : ""));
                NativeElements.SetText(labels.detail, DescribeColony(slot, lifecycle, present));
            }
        }

        /// <summary>"With {colony}" and one line on its exchange: the terms and the round's progress, or why it is idle.</summary>
        private void Describe(DistrictCrossing half, ColonyExchangeService exchanges, out string title, out string detail)
        {
            if (!TradingPosts.IsTradingPost(half))
            {
                title = T("BeaverBuddies.Colony.Trade.NotTradingTitle");
                detail = T("BeaverBuddies.Colony.Overview.NotTrading");
                return;
            }
            DistrictCrossing partner = TradingPosts.Partner(half);
            int them = ColonyExchangeService.OwnerOf(partner);
            title = string.Format(T("BeaverBuddies.Colony.Overview.With"), ColoredName(them));
            CrossingExchange mine = ColonyExchangeService.Of(half), theirs = ColonyExchangeService.Of(partner);
            if (exchanges == null || mine == null || theirs == null || !mine.IsOpen)
            {
                detail = T("BeaverBuddies.Colony.Overview.Idle");
                return;
            }
            string give = exchanges.Amount(mine.Total, mine.GoodId), get = exchanges.Amount(theirs.Total, theirs.GoodId);
            if (mine.State == ExchangeState.Proposed)
            {
                detail = string.Format(T(mine.ProposedHere ? "BeaverBuddies.Colony.Overview.YouOffered" : "BeaverBuddies.Colony.Overview.TheyOffer"), give, get);
                return;
            }
            string round = mine.Repeat
                ? string.Format(T("BeaverBuddies.Colony.Trade.RoundRepeating"), mine.Done + 1)
                : string.Format(T("BeaverBuddies.Colony.Trade.RoundOf"), mine.Done + 1, mine.Rounds);
            string progress = string.Format(T("BeaverBuddies.Colony.Overview.Progress"), Side(half, mine, exchanges), Side(partner, theirs, exchanges));
            string asked = mine.CancelAsked || theirs.CancelAsked ? " " + T("BeaverBuddies.Colony.Overview.CancelAsked") : "";
            detail = string.Format(T("BeaverBuddies.Colony.Overview.Running"), give, get) + " " + round + ". " + progress + asked;
        }

        /// <summary>A side's part of the round: "60/100", or "ready" for science and beavers that can be paid.</summary>
        private static string Side(DistrictCrossing half, CrossingExchange side, ColonyExchangeService exchanges)
        {
            if (side.Total <= 0) return "-";
            if (ExchangeTerms.IsSpecial(side.GoodId))
                return T(exchanges.IsIn(half, side) ? "BeaverBuddies.Colony.Overview.SideReady" : "BeaverBuddies.Colony.Overview.SideNotReady");
            return $"{side.Held}/{side.Total}";
        }

        private string DescribeColony(int slot, ColonyLifecycle lifecycle, List<int> present)
        {
            if (lifecycle == null || !lifecycle.OwnsDistrict(slot)) return T("BeaverBuddies.Colony.Overview.NoColony");
            int population = lifecycle.PopulationOf(slot);
            string status;
            if (population == 0) status = T("BeaverBuddies.Colony.Overview.Dead");
            else if (present.Contains(slot)) status = T("BeaverBuddies.Colony.Overview.Playing");
            else
            {
                int? away = lifecycle.DaysAway(slot);
                status = away == null ? T("BeaverBuddies.Colony.Overview.AwayUnknown") : string.Format(T("BeaverBuddies.Colony.Overview.Away"), away.Value);
            }
            return string.Format(T("BeaverBuddies.Colony.Overview.Colony"), population, status);
        }

        private void GoTo(DistrictCrossing half)
        {
            if (!half) return;
            try { _entitySelectionService.SelectAndFocusOn(half); }
            catch (Exception error) { Plugin.LogWarning("[Colony] Could not go to the trading post: " + error.Message); }
        }

        private void HandOver(int from, int to)
        {
            if (!(EventIO.Get() is ServerEventIO) || ColonyLifecycle.Instance?.HostMayHandOver(from, to) != true) return;
            ReplayEvent.DoPrefix(() => new ColonyHandoverEvent { fromSlot = from, toSlot = to, reason = (int)HandoverReason.ByHost });
            nextRefresh = 0;
        }

        // ---- elements ----

        /// <summary>A row of the list on the game's green board: a title, a line under it, and its buttons at the right.</summary>
        private static NineSliceVisualElement Card(out Label title, out Label detail, params Button[] buttons)
        {
            NineSliceVisualElement card = NativeElements.Box("bg-sub-box--green");
            card.style.marginTop = 6;
            card.style.paddingLeft = 10; card.style.paddingRight = 8; card.style.paddingTop = 6; card.style.paddingBottom = 7;
            VisualElement row = NativeElements.Row();
            VisualElement text = new VisualElement();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            title = NativeElements.Text("", 13);
            title.enableRichText = true;
            detail = NativeElements.MutedText("", 12);
            detail.style.marginTop = 1;
            text.Add(title);
            text.Add(detail);
            row.Add(text);
            VisualElement actions = new VisualElement();
            actions.style.flexShrink = 0;
            actions.style.alignItems = Align.FlexEnd;
            foreach (Button button in buttons)
            {
                button.style.marginLeft = 8;
                button.style.marginTop = 2;
                actions.Add(button);
            }
            row.Add(actions);
            card.Add(row);
            return card;
        }

        /// <summary>A heading as the game's boxes write them (game-text-heading).</summary>
        private static Label Heading(string text = "")
        {
            var label = new Label(text);
            label.AddToClassList("game-text-heading");
            label.AddToClassList("text--bold");
            return label;
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

        /// <summary>A colony's name in bold in its player's color, lightened where it would be hard to read.</summary>
        private static string ColoredName(int slot)
        {
            string name = NativeElements.Plain(ColonyExchangeService.ColonyName(slot));
            if (slot < 0 || slot >= StartingLocationPlayer.PLAYER_COLORS.Length) return "<b>" + name + "</b>";
            string hex = ChatFormat.ReadableHex(ColorUtility.ToHtmlStringRGB(StartingLocationPlayer.PLAYER_COLORS[slot]));
            return $"<color=#{hex}><b>{name}</b></color>";
        }

        private static string T(string key) => RegisteredLocalizationService.T(key);
    }
}
