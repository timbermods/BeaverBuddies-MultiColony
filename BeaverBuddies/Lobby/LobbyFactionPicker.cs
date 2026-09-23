using BeaverBuddies.Factions;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// A waiting room's faction switcher (D7), made of the game's own New Game faction page: its logo ring, its left and
    /// right arrows (with their hover art and click sound), and the plate that names the chosen faction. A mixed room shows
    /// it to every player who may pick; the arrows go through the factions the host has unlocked, in the game's order.
    /// Main-menu classes only (the menu loads CommonStyle and MainMenuMiscStyle, where these live).
    /// </summary>
    internal sealed class LobbyFactionPicker
    {
        public static readonly string[] ClassesUsed =
        {
            "arrow--left", "arrow--right", "faction-item__logo-background", "faction-item__logo", "faction-item-selected__name",
            "name__text", "content-centered", "text--yellow",
        };

        public VisualElement Root { get; }

        private readonly Label caption;
        private readonly Button left;
        private readonly Button right;
        private readonly VisualElement logo;
        private readonly Label name;
        private List<FactionSpec> factions = new List<FactionSpec>();
        private string current;
        private bool canChange;

        /// <summary>The player chose another faction with an arrow.</summary>
        public event Action<string> Changed;

        public LobbyFactionPicker(VisualElementInitializer initializer)
        {
            Root = new VisualElement();
            Root.style.alignItems = Align.Center;
            Root.style.marginBottom = 8;

            caption = new Label(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Faction.Yours"));
            caption.AddToClassList("text--yellow");
            caption.style.fontSize = 14;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.style.marginBottom = 2;
            Root.Add(caption);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;

            left = new Button(() => Step(-1));
            left.AddToClassList("arrow--left");
            left.style.marginRight = 6;
            row.Add(left);

            // The faction page's logo ring, then its name plate (placed in the row: the page's own sits on a big card).
            var ring = new VisualElement();
            ring.AddToClassList("faction-item__logo-background");
            ring.AddToClassList("content-centered");
            ring.style.marginRight = 4;
            logo = new VisualElement();
            logo.AddToClassList("faction-item__logo");
            ring.Add(logo);
            row.Add(ring);

            var plate = new VisualElement();
            plate.AddToClassList("faction-item-selected__name");
            plate.AddToClassList("content-centered");
            plate.style.position = Position.Relative;
            plate.style.bottom = StyleKeyword.Auto;
            name = new Label();
            name.AddToClassList("name__text");
            plate.Add(name);
            row.Add(plate);

            right = new Button(() => Step(1));
            right.AddToClassList("arrow--right");
            right.style.marginLeft = 6;
            row.Add(right);
            Root.Add(row);

            initializer.InitializeVisualElement(Root);
        }

        /// <summary>The factions offered, the one shown, and whether this player may change it now (the room is open).</summary>
        public void Set(IEnumerable<FactionSpec> offered, string shown, bool mayChange)
        {
            factions = (offered ?? Enumerable.Empty<FactionSpec>()).Where(f => f != null).ToList();
            if (factions.All(f => f.Id != current) || current == null) current = shown;
            canChange = mayChange;
            Show();
        }

        /// <summary>Shows a faction without asking (the host's word, or the player's own pick coming back).</summary>
        public void Select(string faction)
        {
            if (faction == null || faction == current) return;
            current = faction;
            Show();
        }

        public string Current => current;

        private void Step(int direction)
        {
            if (!canChange || factions.Count < 2) return;
            string next = FactionRules.Step(factions.Select(f => f.Id).ToList(), current, direction);
            if (next == current) return;
            current = next;
            Show();
            Changed?.Invoke(next);
        }

        private void Show()
        {
            FactionSpec spec = factions.FirstOrDefault(f => f.Id == current);
            Sprite sprite = spec?.Logo.Asset;
            if (sprite != null) logo.style.backgroundImage = new StyleBackground(sprite);
            name.text = spec?.DisplayName.Value ?? current ?? "";
            bool arrows = factions.Count > 1;
            left.style.visibility = arrows ? Visibility.Visible : Visibility.Hidden;
            right.style.visibility = arrows ? Visibility.Visible : Visibility.Hidden;
            left.SetEnabled(canChange);
            right.SetEnabled(canChange);
        }
    }
}
