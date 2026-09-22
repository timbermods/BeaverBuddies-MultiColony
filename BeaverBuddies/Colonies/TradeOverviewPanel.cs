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
    /// Each colony's row also shows its food and water (with the days they last), what it is looking for (its player
    /// sets that here, from the game's goods grid), how close an absent player's colony is to a hand-over, and who
    /// looks after it: a player asks another to look after their colony here, and a steward switches into it and back.
    /// It opens and closes with Ctrl+T, the square Trade button at the top right, or "All Posts" on a Trading Post, and
    /// closes with its close button or Esc; its title or frame drags it anywhere on screen. It does not pause the game
    /// (pausing is shared in co-op). Display and buttons
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
        private readonly TradeItems _items;

        /// <summary>A colony's row: its texts, its food and water, what it is looking for.</summary>
        private sealed class ColonyCard
        {
            public Label Title, Detail, Note;
            public VisualElement Supplies, Wishes;
            public Image FoodIcon, WaterIcon;
            public Label Food, Water;
        }

        private VisualElement window, postsList, coloniesList;
        private NineSliceVisualElement box;
        private ScrollView scroll;
        private Label postsTitle, emptyPosts;
        private VisualElement topButton;
        private Toggle topToggle;
        private TradingPostGoodPicker picker;
        private bool open;
        private float nextRefresh;
        // What the lists show, so they are rebuilt only when that changes (a rebuild would swallow a click).
        private string postsShape, coloniesShape;
        private readonly Dictionary<string, (Label title, Label detail)> postLabels = new Dictionary<string, (Label, Label)>();
        private readonly Dictionary<int, ColonyCard> colonyCards = new Dictionary<int, ColonyCard>();

        public static TradeOverviewPanel Instance { get; private set; }

        public TradeOverviewPanel(UILayout uiLayout, InputService inputService, VisualElementInitializer visualElementInitializer,
            VisualElementLoader visualElementLoader, IAssetLoader assetLoader, ITooltipRegistrar tooltipRegistrar,
            EntityComponentRegistry entityComponentRegistry, EntitySelectionService entitySelectionService, TradeItems items)
        {
            _uiLayout = uiLayout;
            _inputService = inputService;
            _visualElementInitializer = visualElementInitializer;
            _visualElementLoader = visualElementLoader;
            _assetLoader = assetLoader;
            _tooltipRegistrar = tooltipRegistrar;
            _entityComponentRegistry = entityComponentRegistry;
            _entitySelectionService = entitySelectionService;
            _items = items;
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
            picker?.Close();
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
            // Moved by its title badge or its frame, not by its lists and buttons.
            MakeDraggable(header, title);

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

            // The game's goods grid, beside the box, for what this player's colony is looking for.
            picker = new TradingPostGoodPicker(_items, _inputService, _tooltipRegistrar, _visualElementInitializer);
            box.Add(picker.Root);

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
            if (screen > 0 && !float.IsNaN(screen)) box.style.maxHeight = Mathf.Max(220, screen - Top - dragOffset.y - BottomMargin);
        }

        // ---- moving the box ----

        // How far the player dragged the box from its place (centred under the top bar). Kept while the game runs, so
        // the box opens where it was left.
        private Vector2 dragOffset;
        private Vector2 dragOffsetAtStart;
        private Vector3 dragStart;
        private bool dragging;

        /// <summary>
        /// Drags the box by <paramref name="grips"/> and by its own frame (the box itself, where no list, text or button
        /// is): a press there, moved, moves the box; anything inside it keeps its own clicks.
        /// </summary>
        private void MakeDraggable(params VisualElement[] grips)
        {
            var handles = new HashSet<VisualElement>(grips) { box };
            box.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || !(e.target is VisualElement target) || !handles.Contains(target)) return;
                dragging = true;
                dragStart = e.position;
                dragOffsetAtStart = dragOffset;
                box.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            box.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging || !box.HasPointerCapture(e.pointerId)) return;
                Vector3 moved = e.position - dragStart;
                dragOffset = dragOffsetAtStart + new Vector2(moved.x, moved.y);
                PlaceBox();
            });
            box.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                if (box.HasPointerCapture(e.pointerId)) box.ReleasePointer(e.pointerId);
            });
            box.RegisterCallback<PointerCaptureOutEvent>(e => dragging = false);
        }

        /// <summary>
        /// Moves the box by the dragged offset, kept on the screen: its title never under the screen's top edge, and
        /// enough of it left in view to take hold of again.
        /// </summary>
        private void PlaceBox()
        {
            Rect screen = window.panel?.visualTree.worldBound ?? Rect.zero;
            if (screen.width > 0 && screen.height > 0 && !float.IsNaN(screen.width) && !float.IsNaN(screen.height))
            {
                float side = Mathf.Max(0, screen.width / 2 - 80);
                dragOffset.x = Mathf.Clamp(dragOffset.x, -side, side);
                dragOffset.y = Mathf.Clamp(dragOffset.y, 10 - Top, Mathf.Max(10 - Top, screen.height - Top - 80));
            }
            box.style.translate = new Translate(dragOffset.x, dragOffset.y, 0);
            FitToScreen();
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
            ColonySlotService slotService = ColonySlotService.Instance;
            ColonySlotTable table = slotService?.Table;
            ColonyStewards stewards = ColonyStewards.Instance;
            ColonyWishlist wishlist = ColonyWishlist.Instance;
            int seat = ColonySession.LocalSeat;
            string myId = slotService?.LocalPlayerId;
            List<int> slots = Enumerable.Range(0, ColonySlotTable.MaxSlots)
                .Where(slot => (lifecycle?.OwnsDistrict(slot) ?? false) || (table?.Entries.Any(e => e.Slot == slot) ?? false)).ToList();
            bool host = EventIO.Get() is ServerEventIO;
            List<int> present = ColonyLifecycle.PresentSlots();
            // The host's handover buttons depend on who may be handed over to whom.
            var handovers = host && lifecycle != null
                ? slots.SelectMany(from => slots.Where(to => lifecycle.HostMayHandOver(from, to)).Select(to => (from, to))).ToList()
                : new List<(int from, int to)>();
            // Players who could look after a colony: everyone the session knows but this player.
            var others = (slotService?.Players ?? Enumerable.Empty<(int player, string id, string name)>())
                .Where(p => p.id != myId).GroupBy(p => p.id).Select(g => g.First()).ToList();
            bool started = lifecycle != null && !ColonyRules.WaitsForStart(true, SingletonManager.GetSingleton<ReplayService>()?.TicksSinceLoad ?? 1,
                ColonySession.JoiningClosedAtStart);
            string coloniesKey = string.Join(",", slots) + "|" + string.Join(",", handovers.Select(h => $"{h.from}>{h.to}"))
                + "|" + me + "/" + seat + "|" + string.Join(",", others.Select(p => p.id)) + "|" + string.Join(",", present)
                + "|" + (stewards?.Fingerprint() ?? "") + "|" + (wishlist?.Fingerprint() ?? "") + "|" + (started ? "s" : "w");
            if (coloniesKey != coloniesShape)
            {
                coloniesShape = coloniesKey;
                coloniesList.Clear();
                colonyCards.Clear();
                foreach (int slot in slots)
                {
                    var buttons = new List<Button>();
                    foreach (var h in handovers.Where(h => h.from == slot))
                        buttons.Add(SmallButton(string.Format(T("BeaverBuddies.Colony.Overview.HandTo"), NativeElements.Plain(ColonyExchangeService.ColonyName(h.to))),
                            () => HandOver(h.from, h.to)));
                    if (started) buttons.AddRange(StewardButtons(slot, seat, me, myId, host, present, others, stewards, lifecycle));
                    ColonyCard card = BuildColonyCard(buttons.ToArray());
                    coloniesList.Add(card.Title.parent.parent.parent);
                    colonyCards[slot] = card;
                    // This player's colony (the one their actions count as) says what it is looking for from here.
                    if (slot == me && started) BuildWishEditor(card, slot, wishlist);
                }
                _visualElementInitializer.InitializeVisualElement(coloniesList);
            }
            ColonySupplies supplies = ColonySupplies.Instance;
            foreach (int slot in slots)
            {
                if (!colonyCards.TryGetValue(slot, out ColonyCard card)) continue;
                string who = slot == seat ? " " + T("BeaverBuddies.Colony.Overview.You")
                    : slot == me ? " " + T("BeaverBuddies.Colony.Overview.YouRunning") : "";
                NativeElements.SetText(card.Title, ColoredName(slot) + who);
                NativeElements.SetText(card.Detail, DescribeColony(slot, lifecycle, present));
                RefreshSupplies(card, slot, lifecycle, supplies);
                RefreshNote(card, slot, seat, me, myId, stewards, slotService);
                if (slot != me) RefreshWishes(card, slot, wishlist);
            }
        }

        // ---- a colony's row ----

        /// <summary>
        /// The buttons for looking after a colony: an owner asks another player (or takes the colony back); a steward
        /// switches into the colony and back; the host asks a player on behalf of an absent one, or ends a stewardship.
        /// </summary>
        private IEnumerable<Button> StewardButtons(int slot, int seat, int me, string myId, bool host, List<int> present,
            List<(int player, string id, string name)> others, ColonyStewards stewards, ColonyLifecycle lifecycle)
        {
            if (stewards == null || lifecycle == null || !lifecycle.OwnsDistrict(slot)) yield break;
            bool mine = slot == seat;
            string stewardId = stewards.StewardIdOf(slot);
            if (mine)
            {
                if (stewardId == null)
                {
                    foreach (var p in others)
                        yield return SmallButton(string.Format(T("BeaverBuddies.Colony.Overview.LetLookAfter"), NativeElements.Plain(p.name)), () => Grant(slot, p.id, p.name));
                }
                else yield return SmallButton(T("BeaverBuddies.Colony.Overview.TakeBack"), () => Revoke(slot));
                yield break;
            }
            if (stewardId != null && stewardId == myId)
            {
                yield return me == slot
                    ? SmallButton(T("BeaverBuddies.Colony.Overview.BackToYours"), () => ActAs(-1))
                    : SmallButton(T("BeaverBuddies.Colony.Overview.RunColony"), () => ActAs(slot));
                yield break;
            }
            if (!host || present.Contains(slot)) yield break;
            // The host, for a colony whose player is away.
            if (stewardId == null)
            {
                foreach (var p in others.Where(p => ColonySession.SeatOfPlayer(p.player) != slot))
                    yield return SmallButton(string.Format(T("BeaverBuddies.Colony.Overview.LetLookAfter"), NativeElements.Plain(p.name)), () => Grant(slot, p.id, p.name));
            }
            else yield return SmallButton(T("BeaverBuddies.Colony.Overview.EndStewardship"), () => Revoke(slot));
        }

        /// <summary>"24 beavers and bots, playing" or "player away (missed 6 of 7 days)".</summary>
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
                int limit = lifecycle.HandoverLimit;
                if (away == null) status = T("BeaverBuddies.Colony.Overview.AwayUnknown");
                else if (limit > 0) status = string.Format(T("BeaverBuddies.Colony.Overview.AwayOf"), away.Value, limit);
                else if (limit == 0) status = string.Format(T("BeaverBuddies.Colony.Overview.AwayNoLimit"), away.Value);
                else status = string.Format(T("BeaverBuddies.Colony.Overview.Away"), away.Value);
            }
            return string.Format(T("BeaverBuddies.Colony.Overview.Colony"), population, status);
        }

        /// <summary>Food and water as the top bar draws them: the icon, the stock, and the days it lasts at yesterday's use.</summary>
        private void RefreshSupplies(ColonyCard card, int slot, ColonyLifecycle lifecycle, ColonySupplies supplies)
        {
            bool shown = supplies != null && lifecycle != null && lifecycle.OwnsDistrict(slot);
            NativeElements.Show(card.Supplies, shown);
            if (!shown) return;
            if (card.FoodIcon.sprite != supplies.FoodIcon) card.FoodIcon.sprite = supplies.FoodIcon;
            if (card.WaterIcon.sprite != supplies.WaterIcon) card.WaterIcon.sprite = supplies.WaterIcon;
            ShowSupply(card.Food, supplies.Food(slot));
            ShowSupply(card.Water, supplies.Water(slot));
        }

        private static void ShowSupply(Label label, Supply supply)
        {
            string days = SupplyDays.Format(supply.Days);
            string stock = supply.Stock.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
            NativeElements.SetText(label, days == null
                ? string.Format(T("BeaverBuddies.Colony.Overview.SupplyNoDays"), stock)
                : string.Format(T("BeaverBuddies.Colony.Overview.SupplyDays"), stock, days));
            label.style.color = SupplyDays.IsLow(supply.Days) ? new StyleColor(NativeElements.Warning) : new StyleColor(NativeElements.Muted);
        }

        /// <summary>Who looks after the colony, and who is running it now, in one muted line (or none).</summary>
        private void RefreshNote(ColonyCard card, int slot, int seat, int me, string myId, ColonyStewards stewards, ColonySlotService slotService)
        {
            string text = "";
            if (stewards != null)
            {
                string stewardId = stewards.StewardIdOf(slot);
                if (stewardId != null)
                    text = stewardId == myId ? T("BeaverBuddies.Colony.Overview.YouLookAfter")
                        : string.Format(T("BeaverBuddies.Colony.Overview.LookedAfterBy"), NativeElements.Plain(stewards.StewardNameOf(slot)));
                // Somebody else switched into it (their own name, as the session knows them).
                foreach (var pair in stewards.Acting)
                {
                    if (pair.Value != slot || pair.Key == ColonySession.LocalPlayer) continue;
                    string name = slotService?.PlayerNameOf(slotService.PlayerIdOf(pair.Key)) ?? "";
                    text += (text.Length > 0 ? " " : "") + string.Format(T("BeaverBuddies.Colony.Overview.RunBy"), NativeElements.Plain(name));
                }
            }
            if (slot == seat && me != seat)
                text += (text.Length > 0 ? " " : "") + string.Format(T("BeaverBuddies.Colony.Overview.YouAreRunning"), NativeElements.Plain(ColonyExchangeService.ColonyName(me)));
            NativeElements.SetText(card.Note, text);
            NativeElements.Show(card.Note, text.Length > 0);
        }

        /// <summary>Another colony's wishes: "Looking for: [icons]", or nothing.</summary>
        private void RefreshWishes(ColonyCard card, int slot, ColonyWishlist wishlist)
        {
            IReadOnlyList<string> wishes = wishlist?.Of(slot) ?? (IReadOnlyList<string>)Array.Empty<string>();
            NativeElements.Show(card.Wishes, wishes.Count > 0);
            if (wishes.Count == 0) return;
            string key = string.Join(",", wishes);
            if (card.Wishes.userData as string == key) return;
            card.Wishes.userData = key;
            card.Wishes.Clear();
            Label caption = NativeElements.MutedText(T("BeaverBuddies.Colony.Overview.LookingFor"));
            caption.style.marginRight = 6;
            card.Wishes.Add(caption);
            foreach (string item in wishes) card.Wishes.Add(WishChip(item, null));
        }

        /// <summary>
        /// This player's colony's wishes, as buttons: click one to change it, + to add (up to three), Clear to drop
        /// them all. The game's goods grid opens beside the box.
        /// </summary>
        private void BuildWishEditor(ColonyCard card, int slot, ColonyWishlist wishlist)
        {
            NativeElements.Show(card.Wishes, true);
            card.Wishes.Clear();
            Label caption = NativeElements.MutedText(T("BeaverBuddies.Colony.Overview.LookingFor"));
            caption.style.marginRight = 6;
            _tooltipRegistrar.Register(caption, T("BeaverBuddies.Colony.Overview.LookingForTooltip"));
            card.Wishes.Add(caption);
            List<string> wishes = (wishlist?.Of(slot) ?? (IReadOnlyList<string>)Array.Empty<string>()).ToList();
            for (int i = 0; i < wishes.Count; i++)
            {
                int index = i;
                Button chip = WishChip(wishes[i], () => EditWish(slot, index, card.Wishes));
                picker.AddOpener(chip);
                card.Wishes.Add(chip);
            }
            if (wishes.Count < WishlistTerms.MaxWishes)
            {
                int index = wishes.Count;
                Button add = NativeElements.SquareButton(plus: true, _ => EditWish(slot, index, card.Wishes));
                _tooltipRegistrar.Register(add, T("BeaverBuddies.Colony.Overview.AddWishTooltip"));
                picker.AddOpener(add);
                card.Wishes.Add(add);
            }
            if (wishes.Count > 0)
            {
                Button clear = NativeElements.RedButton(T("BeaverBuddies.Colony.Overview.ClearWishes"), () => SendWishes(new List<string>()));
                clear.style.fontSize = 12;
                clear.style.minHeight = 24;
                clear.style.height = 24;
                clear.style.paddingTop = 0;
                clear.style.paddingBottom = 0;
                clear.style.marginLeft = 8;
                card.Wishes.Add(clear);
            }
        }

        /// <summary>A good's icon on the game's wooden button (or, with no click, a plain chip with a tooltip).</summary>
        private Button WishChip(string item, Action onClick)
        {
            Button chip = NativeElements.WoodenButton("", onClick);
            chip.style.minHeight = 28;
            chip.style.height = 28;
            chip.style.paddingLeft = 4; chip.style.paddingRight = 4; chip.style.paddingTop = 2; chip.style.paddingBottom = 2;
            chip.style.marginRight = 4;
            if (onClick == null) chip.pickingMode = PickingMode.Position;
            Image icon = NativeElements.Icon(20);
            icon.sprite = _items.IconOf(item);
            chip.Add(icon);
            _tooltipRegistrar.Register(chip, onClick == null ? _items.Name(item) : string.Format(T("BeaverBuddies.Colony.Overview.WishTooltip"), _items.Name(item)));
            return chip;
        }

        private void EditWish(int slot, int index, VisualElement anchor)
        {
            if (picker.IsOpen && picker.Side == index + 1)
            {
                picker.Close();
                return;
            }
            List<string> wishes = (ColonyWishlist.Instance?.Of(slot) ?? (IReadOnlyList<string>)Array.Empty<string>()).ToList();
            string current = index < wishes.Count ? wishes[index] : null;
            picker.Open(index + 1, T("BeaverBuddies.Colony.Overview.PickWish"), current, item => _items.StockOfColony(slot, item),
                item => SetWish(slot, index, item), anchor, inStockOnlyDefault: false);
        }

        private void SetWish(int slot, int index, string item)
        {
            List<string> wishes = (ColonyWishlist.Instance?.Of(slot) ?? (IReadOnlyList<string>)Array.Empty<string>()).ToList();
            if (index < wishes.Count) wishes[index] = item;
            else wishes.Add(item);
            SendWishes(WishlistTerms.Normalize(wishes, null));
        }

        private void SendWishes(List<string> items)
        {
            if (ReplayEvent.DoPrefix(() => new WishlistChangedEvent { items = items }))
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(T("BeaverBuddies.Colony.Trade.HostFirst"));
            nextRefresh = 0;
        }

        // ---- looking after a colony ----

        private void Grant(int slot, string playerId, string name)
        {
            ReplayEvent.DoPrefix(() => new StewardGrantedEvent { colonySlot = slot, stewardPlayerId = playerId, stewardName = name });
            nextRefresh = 0;
        }

        private void Revoke(int slot)
        {
            ReplayEvent.DoPrefix(() => new StewardRevokedEvent { colonySlot = slot });
            nextRefresh = 0;
        }

        private void ActAs(int slot)
        {
            ReplayEvent.DoPrefix(() => new ActAsColonyEvent { colonySlot = slot });
            nextRefresh = 0;
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

        /// <summary>
        /// A colony's row: the post row's title and line, then its food and water (icons and days), a muted note (who
        /// looks after it), and what it is looking for, with its buttons at the right.
        /// </summary>
        private ColonyCard BuildColonyCard(params Button[] buttons)
        {
            var card = new ColonyCard();
            NineSliceVisualElement board = Card(out card.Title, out card.Detail, buttons);
            VisualElement text = card.Title.parent;
            card.Supplies = NativeElements.Row();
            card.Supplies.style.marginTop = 3;
            card.FoodIcon = NativeElements.Icon(18);
            card.Food = NativeElements.MutedText("", 12);
            card.Food.style.marginLeft = 3;
            card.Food.style.marginRight = 12;
            card.WaterIcon = NativeElements.Icon(18);
            card.Water = NativeElements.MutedText("", 12);
            card.Water.style.marginLeft = 3;
            card.Supplies.Add(card.FoodIcon);
            card.Supplies.Add(card.Food);
            card.Supplies.Add(card.WaterIcon);
            card.Supplies.Add(card.Water);
            _tooltipRegistrar.Register(card.Supplies, T("BeaverBuddies.Colony.Overview.SuppliesTooltip"));
            text.Add(card.Supplies);
            card.Note = NativeElements.MutedText("", 12);
            card.Note.style.marginTop = 2;
            card.Note.style.display = DisplayStyle.None;
            text.Add(card.Note);
            card.Wishes = NativeElements.Row();
            card.Wishes.style.marginTop = 4;
            card.Wishes.style.flexWrap = Wrap.Wrap;
            card.Wishes.style.display = DisplayStyle.None;
            text.Add(card.Wishes);
            return card;
        }

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
