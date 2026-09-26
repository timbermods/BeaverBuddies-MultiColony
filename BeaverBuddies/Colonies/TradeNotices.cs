using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.DistributionSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A trade message that asks the player for an answer (an offer made to their colony, or a request to end one of its
    /// exchanges) stays on screen until the player clicks it away. A click on the message goes to their side of that
    /// Trading Post and selects it, where they answer; its close button only closes it; answering at that post closes
    /// it too. It is drawn as the game's own quick notification is (the green board with the game's text, Common/
    /// QuickNotificationPanel), just below where that one appears, and it chimes as it appears (NoticeSounds). Display
    /// only: posted by an action every computer plays, it is built on the next frame, on the computer of the player it
    /// is for, so nothing of it runs inside a tick.
    /// </summary>
    public class TradeNotices : RegisteredSingleton, IPostLoadableSingleton, IUpdatableSingleton
    {
        // The game's quick notification sits at 100 px and takes up to two lines; these go below it, as wide as it may be.
        private const float Top = 152, MaxWidth = 500;
        // Past this many, the oldest goes, so the messages never cover the map.
        private const int MaxShown = 5;

        private readonly UILayout _uiLayout;
        private readonly VisualElementInitializer _visualElementInitializer;
        private readonly EntitySelectionService _entitySelectionService;
        private readonly NoticeSounds _noticeSounds;

        private sealed class Notice
        {
            public DistrictCrossing Half;
            public VisualElement Root;
        }

        private VisualElement stack;
        // Posted by this frame's actions, shown on the next frame.
        private readonly List<(string Text, DistrictCrossing Half, bool Warning)> posted = new List<(string, DistrictCrossing, bool)>();
        private readonly List<Notice> shown = new List<Notice>();

        public static TradeNotices Instance => SingletonManager.GetSingleton<TradeNotices>();

        public TradeNotices(UILayout uiLayout, VisualElementInitializer visualElementInitializer,
            EntitySelectionService entitySelectionService, NoticeSounds noticeSounds)
        {
            _uiLayout = uiLayout;
            _visualElementInitializer = visualElementInitializer;
            _entitySelectionService = entitySelectionService;
            _noticeSounds = noticeSounds;
        }

        public void PostLoad()
        {
            try
            {
                // A strip across the screen that only places the messages: clicks beside them go to the game.
                stack = new VisualElement { pickingMode = PickingMode.Ignore };
                var s = stack.style;
                s.position = Position.Absolute;
                s.left = 0;
                s.right = 0;
                s.top = Top;
                s.alignItems = Align.Center;
                _uiLayout.AddAbsoluteItem(stack);
            }
            catch (Exception error)
            {
                Plugin.LogError("[Colony] Could not add the trade messages: " + error);
                stack = null;
            }
        }

        /// <summary>
        /// Shows <paramref name="text"/> until the player clicks it away; a click on it goes to <paramref name="half"/>.
        /// A newer message for the same post takes the older one's place. A <paramref name="warning"/> is on the game's
        /// red board, as its warning notices are. False if it cannot be shown this way (the caller then shows it as an
        /// ordinary notice).
        /// </summary>
        public bool Post(string text, DistrictCrossing half, bool warning)
        {
            if (stack == null) return false;
            posted.Add((text, half, warning));
            return true;
        }

        /// <summary>The player answered at <paramref name="half"/>: its message has done its job.</summary>
        public void Answered(DistrictCrossing half)
        {
            posted.RemoveAll(p => p.Half == half);
            foreach (Notice notice in shown.Where(n => n.Half == half).ToList()) Close(notice);
        }

        public void UpdateSingleton()
        {
            if (posted.Count == 0) return;
            try
            {
                foreach ((string text, DistrictCrossing half, bool warning) in posted) Show(text, half, warning);
                _noticeSounds.Play(NoticeSounds.TradeSound);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not show a trade message: " + error.Message);
            }
            finally
            {
                posted.Clear();
            }
        }

        /// <summary>
        /// The game's notification board, green or red (square-large--green or --red, game-text-normal: the quick
        /// notification's classes), with the game's round close button (close-button) at its end.
        /// </summary>
        private void Show(string text, DistrictCrossing half, bool warning)
        {
            foreach (Notice old in shown.Where(n => n.Half == half).ToList()) Close(old);
            while (shown.Count >= MaxShown) Close(shown[0]);

            var notice = new Notice { Half = half };
            var board = new NineSliceVisualElement();
            board.AddToClassList(warning ? "square-large--red" : "square-large--green");
            var s = board.style;
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.maxWidth = MaxWidth;
            s.marginBottom = 4;
            s.paddingLeft = 8; s.paddingRight = 4; s.paddingTop = 5; s.paddingBottom = 5;

            var label = new Label(NativeElements.Plain(text));
            label.AddToClassList("game-text-normal");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;
            label.style.marginRight = 6;
            board.Add(label);

            var close = new Button(() => Close(notice));
            close.AddToClassList("close-button");
            var c = close.style;
            // The game's close button sits on a box's corner; here it ends the line, at the text's size.
            c.position = Position.Relative;
            c.right = 0;
            c.top = 0;
            c.width = 24;
            c.height = 24;
            c.flexShrink = 0;
            c.marginLeft = 0; c.marginRight = 0; c.marginTop = 0; c.marginBottom = 0;
            board.Add(close);

            // A click anywhere else on the message goes to the post.
            board.RegisterCallback<ClickEvent>(click =>
            {
                if (click.target is VisualElement target && (target == close || close.Contains(target))) return;
                GoTo(notice);
            });

            notice.Root = board;
            _visualElementInitializer.InitializeVisualElement(board);
            stack.Add(board);
            shown.Add(notice);
            // In front of the game's panels and the mod's windows, which may be opened after it.
            stack.BringToFront();
        }

        private void GoTo(Notice notice)
        {
            Close(notice);
            if (!notice.Half) return;
            try { _entitySelectionService.SelectAndFocusOn(notice.Half); }
            catch (Exception error) { Plugin.LogWarning("[Colony] Could not go to the trading post: " + error.Message); }
        }

        private void Close(Notice notice)
        {
            shown.Remove(notice);
            notice.Root?.RemoveFromHierarchy();
        }
    }
}
