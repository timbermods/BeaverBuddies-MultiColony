using BeaverBuddies.Editor;
using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// One window for trade and colonies (Ctrl+T, the Trade button at the top right, or "All trading posts" on a
    /// trading post): every trading post of the player's colony with its exchange and a button to go there, and every
    /// colony with its player, population and whether it is being played. The host also finds here the buttons to hand
    /// a colony over (only a colony whose player is away, or which has no beavers left).
    /// Display and buttons only: each button sends an ordinary action.
    /// </summary>
    public class TradeOverviewPanel : IPostLoadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        public const string KeyBindingId = "BeaverBuddies.KeyBind.TradeOverview";

        private static readonly Color Ink = new Color(0.91f, 0.88f, 0.81f);
        private static readonly Color Muted = new Color(0.72f, 0.69f, 0.62f);
        private static readonly Color Background = new Color(0.09f, 0.10f, 0.09f, 0.94f);
        private static readonly Color Rule = new Color(0.45f, 0.42f, 0.35f);

        private readonly UILayout _uiLayout;
        private readonly InputService _inputService;
        private readonly VisualElementInitializer _visualElementInitializer;
        private readonly EntityComponentRegistry _entityComponentRegistry;
        private readonly EntitySelectionService _entitySelectionService;

        private VisualElement window, postsList, coloniesList;
        private Label postsTitle, coloniesTitle, emptyPosts;
        private Button topButton;
        private bool open;
        private float nextRefresh;
        // What the lists show, so they are rebuilt only when that changes (a rebuild would swallow a click).
        private string postsShape, coloniesShape;
        private readonly Dictionary<string, Label> postLabels = new Dictionary<string, Label>();
        private readonly Dictionary<int, Label> colonyLabels = new Dictionary<int, Label>();

        public static TradeOverviewPanel Instance { get; private set; }

        public TradeOverviewPanel(UILayout uiLayout, InputService inputService, VisualElementInitializer visualElementInitializer,
            EntityComponentRegistry entityComponentRegistry, EntitySelectionService entitySelectionService)
        {
            _uiLayout = uiLayout;
            _inputService = inputService;
            _visualElementInitializer = visualElementInitializer;
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
        }

        private static bool Available => ColonyModeService.IsSeparateColonies && !EventIO.IsNull && ColonySession.LocalSlot >= 0;

        public bool ProcessInput()
        {
            if (_inputService.IsKeyDown(KeyBindingId) && Available)
            {
                if (open) Close();
                else Open();
            }
            return false;
        }

        /// <summary>Opens the window; false where it cannot open (no co-op session, separate colonies off).</summary>
        public bool Open()
        {
            if (window == null || !Available) return false;
            open = true;
            window.style.display = DisplayStyle.Flex;
            nextRefresh = 0;
            return true;
        }

        public void Close()
        {
            open = false;
            if (window != null) window.style.display = DisplayStyle.None;
        }

        public void UpdateSingleton()
        {
            if (topButton != null) topButton.style.display = Available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open || window == null) return;
            if (!Available)
            {
                Close();
                return;
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            try
            {
                Refresh();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Trading posts window: " + error.Message);
                Close();
            }
        }

        // ---- building ----

        private void Build()
        {
            window = new VisualElement();
            var s = window.style;
            s.position = Position.Absolute;
            s.right = 16;
            s.top = 150;
            s.width = 380;
            s.maxHeight = 560;
            s.backgroundColor = Background;
            s.paddingLeft = 10; s.paddingRight = 10; s.paddingTop = 8; s.paddingBottom = 8;
            s.borderTopWidth = 1; s.borderBottomWidth = 1; s.borderLeftWidth = 1; s.borderRightWidth = 1;
            s.borderTopColor = Rule; s.borderBottomColor = Rule; s.borderLeftColor = Rule; s.borderRightColor = Rule;
            s.borderTopLeftRadius = 4; s.borderTopRightRadius = 4; s.borderBottomLeftRadius = 4; s.borderBottomRightRadius = 4;

            var header = Row();
            Label title = Text(15, bold: true);
            title.text = T("BeaverBuddies.Colony.Overview.Title");
            title.style.flexGrow = 1;
            header.Add(title);
            header.Add(MakeButton("X", Close));
            window.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.style.maxHeight = 500;
            postsTitle = Text(13, bold: true);
            postsTitle.style.marginTop = 6;
            emptyPosts = Text(12, color: Muted);
            postsList = new VisualElement();
            coloniesTitle = Text(13, bold: true);
            coloniesTitle.style.marginTop = 10;
            coloniesList = new VisualElement();
            scroll.Add(postsTitle);
            scroll.Add(emptyPosts);
            scroll.Add(postsList);
            scroll.Add(coloniesTitle);
            scroll.Add(coloniesList);
            window.Add(scroll);
            var footer = Row();
            footer.style.marginTop = 6;
            footer.Add(MakeButton(T("BeaverBuddies.Colony.Overview.Report"), () => ColonyDiagnostics.Instance?.WriteReport("asked for")));
            window.Add(footer);

            _visualElementInitializer.InitializeVisualElement(window);
            window.style.display = DisplayStyle.None;
            _uiLayout.AddAbsoluteItem(window);

            topButton = new Button(() => { if (open) Close(); else Open(); }) { text = T("BeaverBuddies.Colony.Overview.Button") };
            topButton.style.fontSize = 12;
            _visualElementInitializer.InitializeVisualElement(topButton);
            topButton.style.display = DisplayStyle.None;
            _uiLayout.AddTopRightButton(topButton, 50);
        }

        // ---- the lists ----

        private void Refresh()
        {
            int me = ColonySession.LocalSlot;
            ColonyExchangeService exchanges = ColonyExchangeService.Instance;

            // Trading Posts with a half in this player's colony (trading or not yet), each seen from that half.
            var posts = _entityComponentRegistry.GetEnabled<DistrictCrossing>()
                .Where(half => TradingPosts.IsTradingPostBuilding(half) && DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(half)) == me)
                .Select(half => (key: ReplayEvent.GetEntityID(half), half))
                .Where(p => p.key != null).OrderBy(p => p.key, StringComparer.Ordinal).ToList();
            postsTitle.text = string.Format(T("BeaverBuddies.Colony.Overview.Posts"), posts.Count);
            emptyPosts.text = T("BeaverBuddies.Colony.Overview.NoPosts");
            emptyPosts.style.display = posts.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            string shape = string.Join(",", posts.Select(p => p.key));
            if (shape != postsShape)
            {
                postsShape = shape;
                postsList.Clear();
                postLabels.Clear();
                foreach (var (key, half) in posts)
                {
                    var row = Row();
                    row.style.marginTop = 3;
                    Label label = Text(12);
                    label.style.flexGrow = 1;
                    label.style.flexShrink = 1;
                    row.Add(label);
                    DistrictCrossing target = half;
                    row.Add(MakeButton(T("BeaverBuddies.Colony.Overview.GoTo"), () => GoTo(target)));
                    postsList.Add(row);
                    postLabels[key] = label;
                }
                _visualElementInitializer.InitializeVisualElement(postsList);
            }
            foreach (var (key, half) in posts)
            {
                if (postLabels.TryGetValue(key, out Label label)) label.text = Describe(half, exchanges);
            }

            // Colonies.
            ColonyLifecycle lifecycle = ColonyLifecycle.Instance;
            ColonySlotTable table = ColonySlotService.Instance?.Table;
            List<int> slots = Enumerable.Range(0, ColonySlotTable.MaxSlots)
                .Where(slot => (lifecycle?.OwnsDistrict(slot) ?? false) || (table?.Entries.Any(e => e.Slot == slot) ?? false)).ToList();
            bool host = EventIO.Get() is ServerEventIO;
            coloniesTitle.text = T("BeaverBuddies.Colony.Overview.Colonies");
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
                    Label label = Text(12);
                    label.style.marginTop = 3;
                    label.style.color = SlotColor(slot);
                    coloniesList.Add(label);
                    colonyLabels[slot] = label;
                    var buttons = handovers.Where(h => h.from == slot).ToList();
                    if (buttons.Count == 0) continue;
                    var row = Row();
                    foreach (var (from, to) in buttons)
                    {
                        int source = from, target = to;
                        row.Add(MakeButton(string.Format(T("BeaverBuddies.Colony.Overview.HandTo"), ColonyExchangeService.ColonyName(target)),
                            () => HandOver(source, target)));
                    }
                    coloniesList.Add(row);
                }
                _visualElementInitializer.InitializeVisualElement(coloniesList);
            }
            List<int> present = ColonyLifecycle.PresentSlots();
            foreach (int slot in slots)
            {
                if (colonyLabels.TryGetValue(slot, out Label label)) label.text = DescribeColony(slot, me, lifecycle, present);
            }
        }

        private string Describe(DistrictCrossing half, ColonyExchangeService exchanges)
        {
            if (!TradingPosts.IsTradingPost(half)) return T("BeaverBuddies.Colony.Overview.NotTrading");
            DistrictCrossing partner = TradingPosts.Partner(half);
            int them = DistrictOwner.OwnerOfDistrict(TradingPosts.DistrictOf(partner)) ?? -1;
            string with = string.Format(T("BeaverBuddies.Colony.Overview.With"), ColonyExchangeService.ColonyName(them));
            CrossingExchange mine = ColonyExchangeService.Of(half), theirs = ColonyExchangeService.Of(partner);
            if (exchanges == null || mine == null || theirs == null || !mine.IsOpen) return with + T("BeaverBuddies.Colony.Overview.Idle");
            string give = exchanges.Amount(mine.Total, mine.GoodId), get = exchanges.Amount(theirs.Total, theirs.GoodId);
            if (mine.State == ExchangeState.Proposed)
                return with + string.Format(T(mine.ProposedHere ? "BeaverBuddies.Colony.Overview.YouOffered" : "BeaverBuddies.Colony.Overview.TheyOffer"), give, get);
            string progress = string.Format(T("BeaverBuddies.Colony.Overview.Progress"), mine.Sent, mine.Total, theirs.Sent, theirs.Total);
            string round = mine.Repeat ? " " + string.Format(T("BeaverBuddies.Colony.Overview.Round"), mine.Rounds + 1) : "";
            return with + string.Format(T("BeaverBuddies.Colony.Overview.Running"), give, get) + " " + progress + round;
        }

        private string DescribeColony(int slot, int me, ColonyLifecycle lifecycle, List<int> present)
        {
            string name = ColonyExchangeService.ColonyName(slot) + (slot == me ? " " + T("BeaverBuddies.Colony.Overview.You") : "");
            if (lifecycle == null || !lifecycle.OwnsDistrict(slot)) return name + ": " + T("BeaverBuddies.Colony.Overview.NoColony");
            int population = lifecycle.PopulationOf(slot);
            string status;
            if (population == 0) status = T("BeaverBuddies.Colony.Overview.Dead");
            else if (present.Contains(slot)) status = T("BeaverBuddies.Colony.Overview.Playing");
            else
            {
                int? away = lifecycle.DaysAway(slot);
                status = away == null ? T("BeaverBuddies.Colony.Overview.AwayUnknown") : string.Format(T("BeaverBuddies.Colony.Overview.Away"), away.Value);
            }
            return string.Format(T("BeaverBuddies.Colony.Overview.Colony"), name, population, status);
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

        private static Color SlotColor(int slot) =>
            slot >= 0 && slot < StartingLocationPlayer.PLAYER_COLORS.Length ? Color.Lerp(StartingLocationPlayer.PLAYER_COLORS[slot], Ink, 0.35f) : Ink;

        private static string T(string key) => RegisteredLocalizationService.T(key);

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
            button.style.marginLeft = 4;
            button.style.marginTop = 2;
            return button;
        }
    }
}
