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
            "new-game__main-content", "new-game__summary", "new-game__summary-text", "content-centered", "text--yellow",
            "text--big", "text--centered", "load-box__list-title",
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
        private readonly Label summary;
        private readonly Label settlement;
        private readonly Label factionNote;
        private readonly VisualElement factionSlot;
        // A hosted shared save: the host's Separate colonies checkboxes (1.4.0-rc4).
        private readonly VisualElement optionsSlot;
        private readonly Label listTitle;
        private readonly ScrollView board;
        // A mixed-factions room: the faction of each id, for each row's logo and its tooltip.
        private Func<string, FactionSpec> factionOf;

        /// <summary>The host clicked a guest's remove button.</summary>
        public event Action<LobbyPlayer> RemoveClicked;

        public LobbyPage(VisualElementLoader loader, VisualElementInitializer initializer, ITooltipRegistrar tooltipRegistrar,
            string headerLocKey)
        {
            _loader = loader;
            _initializer = initializer;
            _tooltipRegistrar = tooltipRegistrar;

            // Built as the game's own New Game pages are (NewGameModePanel.uxml, NewGameFactionPanel.uxml): a grow-centered
            // root holding the template as an instance (width-stretch), whose content goes into the template's content slot.
            // NewGameTemplate marks that slot (MainContent) content-container="true", so the TemplateContainer forwards its
            // children, and ElementAt(0), to the empty slot: 1.4.0-beta18 to beta21 took ElementAt(0) for the page's root
            // and threw ArgumentOutOfRange the moment the waiting room opened (the first playtest).
            TemplateContainer template = loader.LoadVisualTreeAsset("MainMenu/NewGameTemplate").CloneTree();
            template.AddToClassList("width-stretch");
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.AddToClassList("grow-centered");
            Root.Add(template);
            // The game's pages give the template's title a key through their own UXML (AttributeOverrides); loaded alone it
            // has none, and the localizer would throw. So it is keyed here, and only then initialised.
            header = template.Q<Label>("HeaderText");
            if (header is LocalizableLabel localizable) localizable._textLocKey = headerLocKey;
            initializer.InitializeVisualElement(Root);

            Back = template.Q<Button>("BackButton");
            Next = template.Q<Button>("NextButton");
            // Both buttons the size of the template's Back (menu-button--medium): its Next is the wizard's larger one, and
            // here the two sit side by side as a pair (Cancel / Start Game, Leave / Ready).
            Next.RemoveFromClassList("menu-button--large-text");
            Next.AddToClassList("menu-button--medium");
            VisualElement main = template.Q(className: "new-game__main-content") ?? template.contentContainer;

            // The Game Mode page's summary plate, alone and centred as that page shows it. (A logo ring beside it, until
            // beta22, pushed it off centre; each player's row shows their faction's logo.)
            var plate = new VisualElement();
            plate.AddToClassList("new-game__summary");
            plate.AddToClassList("content-centered");
            summary = new Label();
            summary.AddToClassList("new-game__summary-text");
            plate.Add(summary);
            main.Add(plate);

            // The settlement's name, in the gold the game uses for a save's details.
            settlement = new Label();
            settlement.AddToClassList("text--yellow");
            settlement.style.fontSize = 14;
            settlement.style.unityTextAlign = TextAnchor.MiddleCenter;
            settlement.style.marginTop = -10;
            settlement.style.marginBottom = 10;
            main.Add(settlement);

            // A mixed-factions room: a second gold line ("Each player picks a faction"), then the faction page's switcher.
            factionNote = new Label();
            factionNote.AddToClassList("text--yellow");
            factionNote.style.fontSize = 13;
            factionNote.style.unityTextAlign = TextAnchor.MiddleCenter;
            factionNote.style.whiteSpace = WhiteSpace.Normal;
            factionNote.style.maxWidth = 600;
            factionNote.style.marginBottom = 6;
            factionNote.style.display = DisplayStyle.None;
            main.Add(factionNote);
            optionsSlot = new VisualElement();
            optionsSlot.style.alignItems = Align.Center;
            optionsSlot.style.marginBottom = 6;
            main.Add(optionsSlot);
            factionSlot = new VisualElement();
            factionSlot.style.alignItems = Align.Center;
            main.Add(factionSlot);

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
            foreach (VisualElement element in new VisualElement[] { plate, settlement, factionNote, listTitle, board, Status, Invite, DirectIp })
                initializer.InitializeVisualElement(element);
        }

        public void SetHeader(string text) => header.text = text;

        /// <summary>A mixed-factions room: the gold line under the settlement (empty hides it).</summary>
        public void SetFactionNote(string text)
        {
            factionNote.text = text ?? "";
            factionNote.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Puts the host's colony checkboxes on the page (null takes them away).</summary>
        public void SetColonyOptions(VisualElement options)
        {
            optionsSlot.Clear();
            if (options != null) optionsSlot.Add(options);
        }

        /// <summary>Puts the faction switcher on the page (null takes it away).</summary>
        public void SetFactionPicker(LobbyFactionPicker picker)
        {
            factionSlot.Clear();
            if (picker != null) factionSlot.Add(picker.Root);
        }

        /// <summary>How each row finds its faction's logo and name (a room whose rows name their faction).</summary>
        public void SetFactions(Func<string, FactionSpec> lookup) => factionOf = lookup;

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
        /// The plate's text and the gold line under the plate (a new game's settlement, or a save's name and in-game date).
        /// </summary>
        public void SetSummary(string text, string line)
        {
            summary.text = text;
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
                    row = new Row(this, hostPage, removable: hostPage && !player.IsHost);
                    rows[player.Number] = row;
                }
                // Kept in the room's order.
                if (board.contentContainer.IndexOf(row.Root) != index)
                {
                    row.Root.RemoveFromHierarchy();
                    board.contentContainer.Insert(Math.Min(index, board.contentContainer.childCount), row.Root);
                }
                // Each row's own faction when the room names one (a mixed room, or a save), else the room's.
                FactionSpec rowFaction = player.Faction != null ? factionOf?.Invoke(player.Faction) : null;
                row.Show(player, own: !hostPage && player.Number == you, canChange, rowFaction?.Logo.Asset ?? factionLogo, rowFaction?.DisplayName.Value);
            }
            foreach (int gone in rows.Keys.Where(n => !seen.Contains(n)).ToList())
            {
                rows[gone].Root.RemoveFromHierarchy();
                rows.Remove(gone);
            }
        }

        /// <summary>The line under the players; a text with no key shows nothing (the line keeps its height).</summary>
        public void SetStatus(LobbyText text) =>
            Status.text = string.IsNullOrEmpty(text.Key) ? "" : RegisteredLocalizationService.T(text.Key, text.Args);

        private static Sprite LoadSprite(string path)
        {
            try { return UnityEngine.Resources.Load<Sprite>(path); }
            catch (Exception error)
            {
                Plugin.LogWarning($"[Lobby] Could not load the sprite {path}: {error.Message}");
                return null;
            }
        }

        /// <summary>
        /// One player: the Mods window's row (the faction's logo, the name, the gold tag), then two columns on the right:
        /// Ready or Not ready, and on the host's page a guest's remove button, set apart from it. The Mods window's checkbox
        /// is hidden: a row says whether its player is ready once, on its right (a guest readies with the page's button).
        /// The columns have fixed widths, so every row's state lines up.
        /// </summary>
        private sealed class Row
        {
            private const float StateWidth = 110;
            private const float RemoveWidth = 28;
            private const float RemoveGap = 16;

            public VisualElement Root { get; }
            private readonly LobbyPage page;
            private readonly Image icon;
            private readonly Label name;
            private readonly Label tag;
            private readonly Label you;
            private readonly VisualElement check;
            private readonly Label state;
            private readonly Button remove;
            private LobbyPlayer player;
            private string factionName;

            public Row(LobbyPage page, bool hostPage, bool removable)
            {
                this.page = page;
                Root = page._loader.LoadVisualElement("Modding/ModItem");
                Root.Q("PriorityWrapper")?.ToggleDisplayStyle(false);
                Root.Q("WarningIcon")?.ToggleDisplayStyle(false);
                Root.Q<Toggle>("ModToggle")?.ToggleDisplayStyle(false);
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

                // The state: the game's own pairing of a green tick and a word (its zipline tooltip), right-aligned.
                var stateColumn = new VisualElement();
                stateColumn.style.flexDirection = FlexDirection.Row;
                stateColumn.style.justifyContent = Justify.FlexEnd;
                stateColumn.style.alignItems = Align.Center;
                stateColumn.style.width = StateWidth;
                stateColumn.style.flexShrink = 0;
                check = new VisualElement();
                check.AddToClassList("checkmark-green");
                check.style.marginRight = 4;
                check.style.flexShrink = 0;
                stateColumn.Add(check);
                state = new Label();
                state.style.unityTextAlign = TextAnchor.MiddleRight;
                state.style.marginRight = 6;
                stateColumn.Add(state);
                Root.Add(stateColumn);

                // The host's page: a column for removing a guest, apart from the state; the host's own row keeps it empty.
                if (hostPage)
                {
                    var removeColumn = new VisualElement();
                    removeColumn.style.width = RemoveWidth;
                    removeColumn.style.marginLeft = RemoveGap;
                    removeColumn.style.flexShrink = 0;
                    removeColumn.style.alignItems = Align.Center;
                    if (removable)
                    {
                        remove = new Button();
                        remove.AddToClassList("button-square");
                        remove.AddToClassList("button-square--large");
                        remove.AddToClassList("button-cross");
                        remove.clicked += () => { if (player != null) page.RemoveClicked?.Invoke(player); };
                        removeColumn.Add(remove);
                    }
                    Root.Add(removeColumn);
                }

                foreach (VisualElement element in new VisualElement[] { you, spacer, stateColumn }) page._initializer.InitializeVisualElement(element);
                // The row's faction, named when its logo is hovered (a mixed room, or a save's colony).
                page._tooltipRegistrar.Register(icon, () => factionName ?? "");
                if (remove != null)
                {
                    page._initializer.InitializeVisualElement(remove);
                    page._tooltipRegistrar.Register(remove, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Remove.Tooltip"));
                }
            }

            public void Show(LobbyPlayer shown, bool own, bool canChange, Sprite factionLogo, string faction = null)
            {
                factionName = faction;
                player = shown;
                bool ready = shown.IsHost || (shown.Ready && !shown.Joining);
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
        }
    }
}
