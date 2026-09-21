using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using Timberborn.CoreUI;
using Timberborn.InputSystem;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Where a player chooses what their colony gives, or asks for, at a trading post: a box beside the panel like the
    /// one a warehouse chooses its good in, with the goods in the game's groups (science and beavers first), each with
    /// how much the colony has. It closes on a choice, on a click anywhere else, or on Esc. Display only.
    /// </summary>
    internal class TradingPostGoodPicker : IInputProcessor
    {
        private const int CellWidth = 50;
        private const int CellHeight = 46;
        private const int PerRow = 5;
        private const int GroupIconWidth = 30;
        // Kept this far from the screen's edges.
        private const float ScreenMargin = 8;
        private const float Dimmed = 0.4f;

        private sealed class Cell
        {
            public string Item;
            public Button Button;
            public Image Icon;
            public Label Count;
        }

        private readonly TradeItems _items;
        private readonly InputService _inputService;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly VisualElementInitializer _visualElementInitializer;

        private readonly Label title, empty;
        private readonly Toggle inStockOnly;
        private readonly ScrollView scroll;
        private readonly List<Cell> cells = new List<Cell>();

        private Func<string, int> stockOf;
        private Action<string> choose;
        private string selected;
        private float anchorTop, placedTop = float.NaN;
        private bool mouseOverBox, mouseOverOpener;

        public VisualElement Root { get; }
        public bool IsOpen { get; private set; }
        /// <summary>Which side the box is choosing for (the fragment's own number), while it is open.</summary>
        public int Side { get; private set; }

        public TradingPostGoodPicker(TradeItems items, InputService inputService, ITooltipRegistrar tooltipRegistrar,
            VisualElementInitializer visualElementInitializer)
        {
            _items = items;
            _inputService = inputService;
            _tooltipRegistrar = tooltipRegistrar;
            _visualElementInitializer = visualElementInitializer;

            // The game's framed box around a green board, as around a warehouse's good selection.
            Root = NativeElements.Box("bg-sub-box--frame");
            var s = Root.style;
            s.position = Position.Absolute;
            s.right = Length.Percent(100);
            s.marginRight = 5;
            s.width = GroupIconWidth + PerRow * CellWidth + 44;
            s.paddingLeft = 1; s.paddingRight = 1; s.paddingTop = 1; s.paddingBottom = 1;
            s.display = DisplayStyle.None;
            NineSliceVisualElement board = NativeElements.Box("bg-sub-box--green");
            board.style.paddingLeft = 8; board.style.paddingRight = 6; board.style.paddingTop = 10; board.style.paddingBottom = 10;
            Root.Add(board);

            title = NativeElements.Text("", 13, bold: true);
            board.Add(title);
            inStockOnly = NativeElements.CheckBox(T("BeaverBuddies.Colony.Trade.InStockOnly"), small: true);
            inStockOnly.value = true;
            inStockOnly.style.marginTop = 4;
            inStockOnly.style.marginBottom = 2;
            inStockOnly.RegisterValueChangedCallback(_ => Rebuild());
            board.Add(inStockOnly);
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll--green-decorated");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.style.flexShrink = 1;
            board.Add(scroll);
            empty = NativeElements.MutedText(T("BeaverBuddies.Colony.Trade.NoneInStock"));
            empty.style.marginTop = 6;
            board.Add(empty);

            Root.RegisterCallback<MouseEnterEvent>(_ => mouseOverBox = true);
            Root.RegisterCallback<MouseLeaveEvent>(_ => mouseOverBox = false);
            Root.RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        /// <summary>
        /// Registers a button that opens the box, so that clicking it again closes the box instead of the click
        /// counting as a click elsewhere.
        /// </summary>
        public void AddOpener(VisualElement opener)
        {
            opener.RegisterCallback<MouseEnterEvent>(_ => mouseOverOpener = true);
            opener.RegisterCallback<MouseLeaveEvent>(_ => mouseOverOpener = false);
        }

        /// <param name="side">The fragment's number for the side being chosen.</param>
        /// <param name="anchor">The element the box opens beside (its top is lined up with the box's).</param>
        public void Open(int side, string heading, string current, Func<string, int> stock, Action<string> onChoose, VisualElement anchor)
        {
            Side = side;
            selected = current;
            stockOf = stock;
            choose = onChoose;
            title.text = heading;
            VisualElement parent = Root.parent;
            anchorTop = parent != null && anchor != null ? anchor.worldBound.yMin - parent.worldBound.yMin : 0;
            placedTop = float.NaN;
            IPanel panel = parent?.panel;
            // Never taller than the screen; the goods scroll instead.
            scroll.style.maxHeight = panel != null ? Mathf.Max(120, panel.visualTree.worldBound.height - 2 * ScreenMargin - 70) : 420;
            Rebuild();
            if (!IsOpen)
            {
                IsOpen = true;
                _inputService.AddInputProcessor(this);
            }
            Root.style.top = anchorTop;
            Root.style.display = DisplayStyle.Flex;
            scroll.scrollOffset = Vector2.zero;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Side = 0;
            mouseOverBox = false;
            _inputService.RemoveInputProcessor(this);
            Root.style.display = DisplayStyle.None;
            cells.Clear();
            scroll.Clear();
            choose = null;
            stockOf = null;
        }

        /// <summary>Esc or a click outside the box (and outside the buttons that open it) closes it.</summary>
        public bool ProcessInput()
        {
            if (!IsOpen) return false;
            if (_inputService.UICancel)
            {
                Close();
                return true;
            }
            if (_inputService.MainMouseButtonDown && !mouseOverBox && !mouseOverOpener) Close();
            return false;
        }

        /// <summary>Brings the counts up to date (the goods shown change only when the box is opened or filtered).</summary>
        public void RefreshCounts()
        {
            if (!IsOpen || stockOf == null) return;
            foreach (Cell cell in cells) ShowCount(cell, stockOf(cell.Item));
        }

        private void Rebuild()
        {
            cells.Clear();
            scroll.Clear();
            if (stockOf == null) return;
            bool onlyInStock = inStockOnly.value;
            AddGroup(null, _items.SpecialItems(), onlyInStock);
            foreach (var (icon, goods) in _items.Groups()) AddGroup(icon, goods, onlyInStock);
            NativeElements.Show(empty, cells.Count == 0);
            // Only the new rows: the game's setup gives a scroll view its scroll bar again each time it sees it.
            foreach (VisualElement row in scroll.contentContainer.Children()) _visualElementInitializer.InitializeVisualElement(row);
        }

        private void AddGroup(Sprite groupIcon, IEnumerable<string> items, bool onlyInStock)
        {
            VisualElement row = null, grid = null;
            foreach (string item in items)
            {
                int stock = stockOf(item);
                if (onlyInStock && stock <= 0 && item != selected) continue;
                if (row == null)
                {
                    row = NativeElements.Row(Align.FlexStart);
                    row.style.marginTop = 4;
                    Image header = NativeElements.Icon(26);
                    header.sprite = groupIcon;
                    header.style.marginTop = 8;
                    header.style.marginRight = GroupIconWidth - 26;
                    row.Add(header);
                    grid = new VisualElement();
                    grid.style.flexDirection = FlexDirection.Row;
                    grid.style.flexWrap = Wrap.Wrap;
                    grid.style.width = PerRow * CellWidth;
                    row.Add(grid);
                    scroll.Add(row);
                }
                grid.Add(MakeCell(item, stock));
            }
        }

        // The warehouse's good button (bg-box--green, lighter on hover, highlighted when chosen), a little taller so
        // the count fits under the icon.
        private VisualElement MakeCell(string item, int stock)
        {
            var wrapper = new VisualElement();
            wrapper.style.width = CellWidth;
            wrapper.style.height = CellHeight;
            wrapper.style.paddingLeft = 2; wrapper.style.paddingRight = 2; wrapper.style.paddingTop = 2; wrapper.style.paddingBottom = 2;
            var button = new NineSliceButton();
            button.AddToClassList("good-selection-box-item");
            button.AddToClassList("bg-box--green");
            button.EnableInClassList("selected-item", item == selected);
            var s = button.style;
            s.width = CellWidth - 4;
            s.height = CellHeight - 4;
            s.flexDirection = FlexDirection.Column;
            s.alignItems = Align.Center;
            s.justifyContent = Justify.Center;
            s.paddingLeft = 1; s.paddingRight = 1; s.paddingTop = 3; s.paddingBottom = 1;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            string chosen = item;
            button.clicked += () =>
            {
                Action<string> onChoose = choose;
                Close();
                onChoose?.Invoke(chosen);
            };
            Image icon = NativeElements.Icon(22);
            icon.sprite = _items.IconOf(item);
            button.Add(icon);
            Label count = NativeElements.Text("", 11);
            count.style.unityTextAlign = TextAnchor.MiddleCenter;
            count.style.whiteSpace = WhiteSpace.NoWrap;
            count.pickingMode = PickingMode.Ignore;
            button.Add(count);
            wrapper.Add(button);
            var cell = new Cell { Item = item, Button = button, Icon = icon, Count = count };
            _tooltipRegistrar.Register(button, () => string.Format(T("BeaverBuddies.Colony.Trade.CellTooltip"), _items.Name(item),
                stockOf != null ? stockOf(item).ToString("N0", CultureInfo.CurrentCulture) : "0"));
            ShowCount(cell, stock);
            cells.Add(cell);
            return wrapper;
        }

        private static void ShowCount(Cell cell, int stock)
        {
            NativeElements.SetText(cell.Count, stock.ToString("N0", CultureInfo.CurrentCulture));
            float opacity = stock > 0 ? 1f : Dimmed;
            cell.Icon.style.opacity = opacity;
            cell.Count.style.opacity = opacity;
        }

        /// <summary>Beside the chosen side's card, moved up or down as far as needed to stay on the screen.</summary>
        private void Place()
        {
            VisualElement parent = Root.parent;
            IPanel panel = Root.panel;
            if (!IsOpen || parent == null || panel == null) return;
            Rect screen = panel.visualTree.worldBound;
            float parentTop = parent.worldBound.yMin, height = Root.layout.height;
            if (float.IsNaN(parentTop) || float.IsNaN(height) || height <= 0) return;
            float lowest = screen.yMax - ScreenMargin - height - parentTop;
            float highest = screen.yMin + ScreenMargin - parentTop;
            float top = Mathf.Max(highest, Mathf.Min(anchorTop, lowest));
            if (!float.IsNaN(placedTop) && Mathf.Abs(placedTop - top) < 0.5f) return;
            placedTop = top;
            Root.style.top = top;
        }

        private static string T(string key) => RegisteredLocalizationService.T(key);
    }
}
