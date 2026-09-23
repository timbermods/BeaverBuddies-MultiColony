using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using Timberborn.TooltipSystem;
using TimberNet;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// The waiting room's page, for the host and a guest alike: a page of the game's own New Game wizard
    /// (MainMenu/NewGameTemplate: the banner, the capsule title, Back and Next), holding the Game Mode page's summary
    /// plate with the faction's logo ring, the settlement's name, the Mods window's board with one Mods row per player,
    /// the faction page's yellow status line, and (for the host) the map page's wide Invite button.
    /// <para>
    /// Only the game's templates and the classes of the style sheets the main menu loads (CommonStyle, CoreStyle,
    /// OptionsStyle, MainMenuStyle, MainMenuMiscStyle, ModdingStyle). Not Util/NativeElements: its helpers use the in-game
    /// sheets, which the main menu does not load. See design/PRE-GAME-LOBBY-PLAN.md §5.
    /// </para>
    /// </summary>
    internal sealed class LobbyPage
    {
        public static readonly string[] ClassesUsed =
        {
            "new-game__main-content", "new-game__summary", "new-game__summary-text", "faction-item__logo-background",
            "faction-item__logo", "content-centered", "text--yellow", "text--big", "text--centered", "load-box__list-title",
            "scroll--green-decorated", "mod-manager-box__list", "unlock-condition__text", "wide-menu-button", "map-item__icon",
            "map-selection__wide-button-label", "text--grey", "text--default", "checkmark-green", "button-square",
            "button-square--large", "button-cross",
        };

        private const string InviteIconPath = "UI/Images/Game/ico-beavers";

        private readonly VisualElementLoader _loader;
        private readonly VisualElementInitializer _initializer;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly Dictionary<int, Row> rows = new Dictionary<int, Row>();

        public VisualElement Root { get; }
        public Button Back { get; }
        public Button Next { get; }
        public Label Status { get; }
        public Button Invite { get; }
        public Label DirectIp { get; }

        private readonly Label header;
        private readonly VisualElement ring;
        private readonly VisualElement logo;
        private readonly Label summary;
        private readonly Label settlement;
        private readonly Label listTitle;
        private readonly ScrollView board;

        /// <summary>A guest's own row checkbox was clicked (the host's page has none).</summary>
        public event Action<bool> OwnReadyToggled;
        /// <summary>The host clicked a guest's remove button.</summary>
        public event Action<LobbyPlayer> RemoveClicked;

        public LobbyPage(VisualElementLoader loader, VisualElementInitializer initializer, ITooltipRegistrar tooltipRegistrar,
            string headerLocKey)
        {
            _loader = loader;
            _initializer = initializer;
            _tooltipRegistrar = tooltipRegistrar;

            // The game's pages give the template's title a key through their own UXML (AttributeOverrides); loaded alone it
            // has none, and the localizer would throw. So it is cloned here, keyed, and only then initialised.
            Root = loader.LoadVisualTreeAsset("MainMenu/NewGameTemplate").CloneTree().ElementAt(0);
            header = Root.Q<Label>("HeaderText");
            if (header is LocalizableLabel localizable) localizable._textLocKey = headerLocKey;
            initializer.InitializeVisualElement(Root);

            Back = Root.Q<Button>("BackButton");
            Next = Root.Q<Button>("NextButton");
            VisualElement main = Root.Q(className: "new-game__main-content");

            // The Game Mode page's summary plate, with the faction page's logo ring beside it.
            var summaryRow = new VisualElement();
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.justifyContent = Justify.Center;
            ring = new VisualElement();
            ring.AddToClassList("faction-item__logo-background");
            ring.AddToClassList("content-centered");
            ring.style.marginRight = 10;
            ring.style.marginTop = 10;
            ring.style.marginBottom = 20;
            logo = new VisualElement();
            logo.AddToClassList("faction-item__logo");
            ring.Add(logo);
            summaryRow.Add(ring);
            var plate = new VisualElement();
            plate.AddToClassList("new-game__summary");
            plate.AddToClassList("content-centered");
            summary = new Label();
            summary.AddToClassList("new-game__summary-text");
            plate.Add(summary);
            summaryRow.Add(plate);
            main.Add(summaryRow);

            // The settlement's name, in the gold the game uses for a save's details.
            settlement = new Label();
            settlement.AddToClassList("text--yellow");
            settlement.style.fontSize = 14;
            settlement.style.unityTextAlign = TextAnchor.MiddleCenter;
            settlement.style.marginTop = -10;
            settlement.style.marginBottom = 10;
            main.Add(settlement);

            // Load Game's list title, then the Mods window's board.
            listTitle = new Label();
            listTitle.AddToClassList("text--big");
            listTitle.AddToClassList("text--centered");
            listTitle.AddToClassList("load-box__list-title");
            main.Add(listTitle);

            board = new ScrollView();
            board.AddToClassList("scroll--green-decorated");
            board.AddToClassList("mod-manager-box__list");
            board.style.width = 600;
            board.style.maxHeight = 300;
            board.style.flexGrow = 0;
            board.style.flexShrink = 1;
            main.Add(board);

            // The faction page's one-line status (its unlock condition), kept in place so the page never jumps.
            Status = new Label();
            Status.AddToClassList("unlock-condition__text");
            Status.style.unityTextAlign = TextAnchor.MiddleCenter;
            Status.style.marginTop = 12;
            main.Add(Status);

            // The map page's wide button with an icon (its Download maps).
            Invite = new NineSliceButton();
            Invite.AddToClassList("wide-menu-button");
            Invite.style.marginTop = 10;
            var inviteIcon = new Image();
            inviteIcon.AddToClassList("map-item__icon");
            inviteIcon.sprite = LoadSprite(InviteIconPath);
            Invite.Add(inviteIcon);
            var inviteLabel = new Label(RegisteredLocalizationService.T("BeaverBuddies.Host.InviteFriends"));
            inviteLabel.AddToClassList("map-selection__wide-button-label");
            Invite.Add(inviteLabel);
            main.Add(Invite);

            DirectIp = new Label();
            DirectIp.AddToClassList("text--grey");
            DirectIp.style.unityTextAlign = TextAnchor.MiddleCenter;
            DirectIp.style.marginTop = 8;
            main.Add(DirectIp);

            // Once, before any row: each pass over a ScrollView adds another set of scroll-bar decorations.
            foreach (VisualElement element in new VisualElement[] { summaryRow, settlement, listTitle, board, Status, Invite, DirectIp })
                initializer.InitializeVisualElement(element);
        }

        public void SetHeader(string text) => header.text = text;

        /// <summary>
        /// A save's gold line: its name as the Load Game box shows it (an autosave is the game's own "Autosave", in each
        /// player's language), and its in-game date worded the same way (when known).
        /// </summary>
        public static string SaveLine(Timberborn.UIFormatters.TimestampFormatter formatter, string saveName, int cycle, int day)
        {
            string name = saveName ?? "";
            if (name.Contains(Timberborn.GameSaveRepositorySystem.GameSaveRepository.AutosaveNameSuffix))
                name = RegisteredLocalizationService.T("Saving.Autosave");
            if (cycle <= 0 || formatter == null) return name;
            return RegisteredLocalizationService.T("BeaverBuddies.Lobby.SaveLine", name, formatter.FormatLongLocalized(cycle, day));
        }

        /// <summary>
        /// The plate's text, the faction's logo ring (hidden without a faction: a save's metadata names none) and the gold
        /// line under the plate (a new game's settlement, or a save's name and in-game date).
        /// </summary>
        public void SetSummary(string text, FactionSpec faction, string line)
        {
            summary.text = text;
            Sprite sprite = faction?.Logo.Asset;
            if (sprite != null) logo.style.backgroundImage = new StyleBackground(sprite);
            ring.ToggleDisplayStyle(sprite != null);
            settlement.text = line ?? "";
        }

        /// <summary>
        /// Shows the players, host first, in the room's order. Rows are kept and updated, never rebuilt, so nothing is
        /// initialised twice. <paramref name="you"/> is the guest's own number (0 on the host's page).
        /// </summary>
        public void SetPlayers(IReadOnlyList<LobbyPlayer> players, int you, bool hostPage, bool canChange, Sprite factionLogo)
        {
            listTitle.text = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Players", players.Count);
            var seen = new HashSet<int>();
            for (int index = 0; index < players.Count; index++)
            {
                LobbyPlayer player = players[index];
                seen.Add(player.Number);
                if (!rows.TryGetValue(player.Number, out Row row))
                {
                    row = new Row(this, player.Number, own: !hostPage && player.Number == you, removable: hostPage && !player.IsHost);
                    rows[player.Number] = row;
                }
                // Kept in the room's order.
                if (board.contentContainer.IndexOf(row.Root) != index)
                {
                    row.Root.RemoveFromHierarchy();
                    board.contentContainer.Insert(Math.Min(index, board.contentContainer.childCount), row.Root);
                }
                row.Show(player, own: !hostPage && player.Number == you, canChange, factionLogo);
            }
            foreach (int gone in rows.Keys.Where(n => !seen.Contains(n)).ToList())
            {
                rows[gone].Root.RemoveFromHierarchy();
                rows.Remove(gone);
            }
        }

        public void SetStatus(LobbyText text) => Status.text = RegisteredLocalizationService.T(text.Key, text.Args);

        private static Sprite LoadSprite(string path)
        {
            try { return UnityEngine.Resources.Load<Sprite>(path); }
            catch (Exception error)
            {
                Plugin.LogWarning($"[Lobby] Could not load the sprite {path}: {error.Message}");
                return null;
            }
        }

        /// <summary>One player: the Mods window's row (checkbox, icon, name, gold tag), then the ready state and remove.</summary>
        private sealed class Row
        {
            public VisualElement Root { get; }
            private readonly LobbyPage page;
            private readonly Toggle toggle;
            private readonly Image icon;
            private readonly Label name;
            private readonly Label tag;
            private readonly Label you;
            private readonly VisualElement check;
            private readonly Label state;
            private readonly Button remove;
            private LobbyPlayer player;
            private bool own;
            private bool canChange;
            private bool shownReady;

            public Row(LobbyPage page, int number, bool own, bool removable)
            {
                this.page = page;
                Root = page._loader.LoadVisualElement("Modding/ModItem");
                Root.Q("PriorityWrapper")?.ToggleDisplayStyle(false);
                Root.Q("WarningIcon")?.ToggleDisplayStyle(false);
                toggle = Root.Q<Toggle>("ModToggle");
                icon = Root.Q<Image>("ModIcon");
                name = Root.Q<Label>("ModName");
                tag = Root.Q<Label>("ModVersion");

                you = new Label();
                you.AddToClassList("text--yellow");
                you.style.fontSize = 14;
                you.style.marginRight = 5;
                Root.Add(you);

                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                Root.Add(spacer);

                // The game's own pairing of a green tick and a word (its zipline tooltip).
                check = new VisualElement();
                check.AddToClassList("checkmark-green");
                check.style.marginRight = 4;
                check.style.flexShrink = 0;
                Root.Add(check);
                state = new Label();
                state.style.marginRight = 8;
                Root.Add(state);

                if (removable)
                {
                    remove = new Button();
                    remove.AddToClassList("button-square");
                    remove.AddToClassList("button-square--large");
                    remove.AddToClassList("button-cross");
                    remove.clicked += () => { if (player != null) page.RemoveClicked?.Invoke(player); };
                    Root.Add(remove);
                }
                else
                {
                    // Keeps every row's state in line with the rows that have a remove button.
                    var gap = new VisualElement();
                    gap.style.width = 28;
                    Root.Add(gap);
                }

                foreach (VisualElement element in new VisualElement[] { you, spacer, check, state }) page._initializer.InitializeVisualElement(element);
                if (remove != null)
                {
                    page._initializer.InitializeVisualElement(remove);
                    page._tooltipRegistrar.Register(remove, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Remove.Tooltip"));
                }

                toggle.RegisterValueChangedCallback(changed =>
                {
                    if (this.own && canChange) page.OwnReadyToggled?.Invoke(changed.newValue);
                    else toggle.SetValueWithoutNotify(shownReady);
                });
                SetLive(own);
            }

            public void Show(LobbyPlayer shown, bool own, bool canChange, Sprite factionLogo)
            {
                player = shown;
                this.canChange = canChange;
                if (this.own != own) SetLive(own);
                bool ready = shown.IsHost || (shown.Ready && !shown.Joining);
                shownReady = ready;
                toggle.SetValueWithoutNotify(ready);
                toggle.SetEnabled(!own || canChange);
                if (factionLogo != null) icon.sprite = factionLogo;
                name.text = shown.Joining ? RegisteredLocalizationService.T("BeaverBuddies.Lobby.Joining") : shown.Name;
                name.EnableInClassList("text--grey", shown.Joining);
                LobbyText? tagText = LobbyRules.Tag(shown);
                tag.text = tagText == null ? "" : RegisteredLocalizationService.T(tagText.Value.Key, tagText.Value.Args);
                you.text = own ? RegisteredLocalizationService.T("BeaverBuddies.Lobby.You") : "";
                check.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
                state.text = RegisteredLocalizationService.T(shown.Joining ? "BeaverBuddies.Lobby.Joining"
                    : ready ? "BeaverBuddies.Lobby.Ready" : "BeaverBuddies.Lobby.NotReady");
                state.EnableInClassList("text--default", ready);
                state.EnableInClassList("text--grey", !ready);
                remove?.SetEnabled(canChange);
            }

            // Only a guest's own checkbox takes clicks; everyone else's is a read-only mark (no hover either).
            private void SetLive(bool live)
            {
                own = live;
                PickingMode mode = live ? PickingMode.Position : PickingMode.Ignore;
                toggle.pickingMode = mode;
                foreach (VisualElement child in toggle.Query<VisualElement>().ToList()) child.pickingMode = mode;
            }
        }
    }
}
