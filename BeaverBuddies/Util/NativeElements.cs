using System;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Util
{
    /// <summary>
    /// Elements drawn with the game's own style sheets and art, so a mod's panel looks like one of the game's. The
    /// classes are the ones the game's own panels use (read from Timberborn 1.1.2.4's UXML and USS: the Workplace panel,
    /// the warehouse's good selection, the District Crossing's side panel). A class that sets a nine-slice background
    /// (--background-image: panels, wooden buttons, input boxes) draws only on the game's nine-slice elements, so those
    /// are used wherever such a class is. Run the result through the game's VisualElementInitializer (click sounds,
    /// scroll bars, and switching hotkeys off while a player types).
    /// </summary>
    internal static class NativeElements
    {
        /// <summary>Secondary text on the game's green and blue panels.</summary>
        public static readonly Color Muted = new Color(0.70f, 0.72f, 0.66f);
        /// <summary>A problem the player can fix, readable on the game's dark panels.</summary>
        public static readonly Color Warning = new Color(1f, 0.58f, 0.46f);

        /// <summary>A panel section like the Workplace one: the game's green board, 8/12 padding.</summary>
        public static NineSliceVisualElement Section()
        {
            var section = new NineSliceVisualElement();
            section.AddToClassList("entity-sub-panel");
            section.AddToClassList("bg-sub-box--green");
            return section;
        }

        /// <summary>A box with one of the game's backgrounds, e.g. bg-sub-box--blue (the description's).</summary>
        public static NineSliceVisualElement Box(string backgroundClass)
        {
            var box = new NineSliceVisualElement();
            box.AddToClassList(backgroundClass);
            return box;
        }

        /// <summary>Text as the game's entity panels write it (13 px, light grey), wrapping.</summary>
        public static Label Text(string text = "", int size = 13, bool bold = false)
        {
            var label = new Label(text);
            label.AddToClassList("entity-panel__text");
            var s = label.style;
            s.fontSize = size;
            s.whiteSpace = WhiteSpace.Normal;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            s.paddingLeft = 0; s.paddingRight = 0; s.paddingTop = 0; s.paddingBottom = 0;
            if (bold) s.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        /// <summary>A caption in the game's yellow (as the district number beside a building's name).</summary>
        public static Label Caption(string text = "", int size = 12)
        {
            Label label = Text(text, size);
            label.AddToClassList("text--yellow");
            return label;
        }

        public static Label MutedText(string text = "", int size = 12)
        {
            Label label = Text(text, size);
            label.style.color = Muted;
            return label;
        }

        /// <summary>The game's wooden button (the warehouse's "Accept goods" selector, MixedStorage's buttons).</summary>
        public static Button WoodenButton(string text, Action onClick) => TextButton(text, onClick, "button-game");

        /// <summary>The game's red panel button (the District Crossing's "Manage distribution").</summary>
        public static Button RedButton(string text, Action onClick) =>
            TextButton(text, onClick, "entity-fragment__button", "entity-fragment__button--red");

        private static Button TextButton(string text, Action onClick, params string[] classes)
        {
            var button = new NineSliceButton { text = text };
            if (onClick != null) button.clicked += onClick;
            button.AddToClassList("entity-panel__text");
            foreach (string c in classes) button.AddToClassList(c);
            var s = button.style;
            s.fontSize = 13;
            s.whiteSpace = WhiteSpace.NoWrap;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.minHeight = 30;
            s.minWidth = 0;
            s.paddingLeft = 8; s.paddingRight = 8; s.paddingTop = 4; s.paddingBottom = 4;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            return button;
        }

        /// <summary>
        /// A square −/+ like the Workplace panel's worker buttons. The handler gets the click, for its modifier keys.
        /// </summary>
        public static Button SquareButton(bool plus, Action<ClickEvent> onClick)
        {
            var button = new Button();
            button.AddToClassList("button-square");
            button.AddToClassList("button-square--large");
            button.AddToClassList(plus ? "button-plus" : "button-minus");
            button.RegisterCallback<ClickEvent>(click => onClick(click));
            button.style.flexShrink = 0;
            return button;
        }

        /// <summary>The game's input box (bg-input), centred as the game's number boxes are.</summary>
        public static TextField InputBox(int maxLength, int width)
        {
            var field = new NineSliceTextField { maxLength = maxLength };
            field.AddToClassList("text-field");
            var s = field.style;
            s.width = width;
            s.minWidth = width;
            s.height = 24;
            s.flexShrink = 0;
            s.fontSize = 13;
            s.marginLeft = 0; s.marginRight = 0; s.marginTop = 0; s.marginBottom = 0;
            return field;
        }

        /// <summary>The game's check box (as "Prioritized by haulers").</summary>
        public static Toggle CheckBox(string text, bool small = false)
        {
            var toggle = new Toggle { text = text };
            toggle.AddToClassList("game-toggle");
            toggle.AddToClassList("entity-panel__toggle");
            if (small) toggle.AddToClassList("game-toggle--small");
            toggle.style.fontSize = small ? 12 : 13;
            return toggle;
        }

        /// <summary>The down arrow the game's drop-downs show beside their choice.</summary>
        public static VisualElement DropdownArrow()
        {
            var arrow = new VisualElement();
            arrow.AddToClassList("dropdown__arrow");
            arrow.AddToClassList("dropdown__arrow-down");
            arrow.pickingMode = PickingMode.Ignore;
            arrow.style.flexShrink = 0;
            return arrow;
        }

        /// <summary>A good's icon (or science's, or a beaver's) at a given size.</summary>
        public static Image Icon(int size)
        {
            var image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.style.width = size;
            image.style.height = size;
            image.style.minWidth = size;
            image.style.flexShrink = 0;
            return image;
        }

        public static VisualElement Row(Align align = Align.Center)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = align;
            return row;
        }

        /// <summary>A thin rule in the game's gold, between parts of a panel.</summary>
        public static VisualElement Rule()
        {
            var rule = new VisualElement();
            rule.style.height = 1;
            rule.style.backgroundColor = new Color(0.74f, 0.64f, 0.42f, 0.28f);
            rule.style.marginTop = 9;
            rule.style.marginBottom = 7;
            return rule;
        }

        public static void Show(VisualElement element, bool visible)
        {
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        /// <summary>Sets a label's text only when it changed (a text change lays the panel out again).</summary>
        public static void SetText(TextElement element, string text)
        {
            if (element.text != text) element.text = text;
        }

        /// <summary>A name to put into rich text: it can add no markup of its own.</summary>
        public static string Plain(string value) => (value ?? "").Replace("<", "").Replace(">", "");
    }
}
