using BeaverBuddies.Steam;
using BeaverBuddies.Util;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.InputSystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    /// <summary>
    /// The Join co-op game box (1.4.0-beta24; in a game's menu too since 1.4.0-rc7): the Steam friends who are hosting a
    /// game you can join, and under them the direct IP address, as before.
    /// <para>
    /// It is the game's own Load Game box, piece by piece: the named box (Common/NamedBoxTemplate: frame, capsule title,
    /// close button), a list title, a ListView of the Load Game box's save rows (Options/GameSaveItemElement: the name, and
    /// two small gold lines; the game's own hover and selected art), and a row of medium buttons. The address part is
    /// the game's input box (Core/InputBox): its message, field and button. Only the style sheets the main menu loads;
    /// in a game, those it lacks are added to the box's root (InGameLobby.AttachStyles).
    /// </para>
    /// <para>
    /// A friend's game is found from Steam alone: a friend playing Timberborn in a lobby (a host's lobby is friends-only
    /// unless the host turned that off) whose data the host's SteamListener writes: the mod version, whether it is a
    /// waiting room, open or started, and a line saying what it is. Joining is the Steam invite's own path
    /// (SteamMatchmaking.JoinLobby, then SteamOverlayConnectionService.OnLobbyEntered).
    /// </para>
    /// </summary>
    internal sealed class JoinCoopBox : IPanelController
    {
        public static readonly string[] ClassesUsed =
        {
            "content-row-centered", "text--big", "text--centered", "load-box__list-title", "panel-list-view", "text--default",
            "scroll--green-decorated", "box-buttons", "menu-button", "menu-button--medium", "box__text", "text-field",
            "box__input",
        };

        private const string HeaderLocKey = "BeaverBuddies.Menu.JoinCoopGame";
        private const int AddressLimit = 128;
        private const float BoxWidth = 620;
        private const float ListHeight = 216;
        private const long RefreshMs = 2000;
        // Lobby data is asked for again at most this often per lobby (it changes when the host starts, or its room fills).
        private static readonly TimeSpan LobbyDataAgain = TimeSpan.FromSeconds(6);

        private readonly PanelStack _panelStack;
        private readonly Action<ulong> _joinLobby;
        private readonly Action<string> _connect;
        private readonly VisualElement _root;
        private readonly ListView list;
        private readonly Label empty;
        private readonly Button join;
        private readonly TextField address;
        private readonly List<FriendGame> games = new List<FriendGame>();
        private readonly Dictionary<ulong, DateTime> askedAt = new Dictionary<ulong, DateTime>();
        private readonly IVisualElementScheduledItem refresh;
        private bool scanFailed;
        private bool closed;

        private JoinCoopBox(PanelStack panelStack, VisualElementLoader loader, VisualElementInitializer initializer,
            InputService inputService, Action<VisualElement> attachStyles, string lastAddress, Action<ulong> joinLobby, Action<string> connect)
        {
            _panelStack = panelStack;
            _joinLobby = joinLobby;
            _connect = connect;

            // The named box, as the game's boxes use it: an instance whose children go into the box (its content slot,
            // content-container="true"; so never ElementAt(0), see LobbyPage), its title keyed before it is initialised.
            TemplateContainer box = loader.LoadVisualTreeAsset("Common/NamedBoxTemplate").CloneTree();
            box.style.width = BoxWidth;
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            _root.AddToClassList("content-row-centered");
            attachStyles?.Invoke(_root);
            _root.Add(box);
            if (box.Q<Label>("Header") is LocalizableLabel header) header._textLocKey = HeaderLocKey;
            box.Q<Button>("CloseButton").clicked += OnUICancelled;

            // Friends' games: the Load Game box's list title, list and buttons.
            box.Add(Title("BeaverBuddies.JoinCoopGame.Friends"));
            var listArea = new VisualElement();
            listArea.style.height = ListHeight;
            listArea.style.flexShrink = 0;
            list = new ListView
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                itemsSource = games,
                makeItem = () => loader.LoadVisualElement("Options/GameSaveItemElement"),
                bindItem = (element, index) => Bind(element, games[index]),
            };
            list.AddToClassList("panel-list-view");
            list.AddToClassList("text--default");
            list.AddToClassList("scroll--green-decorated");
            list.style.flexGrow = 1;
            list.selectionChanged += _ => ShowJoinable();
            list.itemsChosen += _ => Join();
            listArea.Add(list);
            empty = new Label();
            empty.AddToClassList("text--default");
            empty.AddToClassList("text--centered");
            empty.style.whiteSpace = WhiteSpace.Normal;
            empty.style.flexGrow = 1;
            empty.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            empty.style.paddingLeft = 40;
            empty.style.paddingRight = 40;
            listArea.Add(empty);
            box.Add(listArea);
            join = Buttons(box, "BeaverBuddies.JoinCoopGame.Join", Join);

            // The address: the game's input box's message, field and button, under a title of its own.
            Label addressTitle = Title("BeaverBuddies.JoinCoopGame.ByAddress");
            addressTitle.style.marginTop = 22;
            box.Add(addressTitle);
            var message = new Label(RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.EnterIp"));
            message.AddToClassList("box__text");
            message.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            message.style.marginBottom = 10;
            box.Add(message);
            address = new NineSliceTextField();
            address.AddToClassList("text-field");
            address.AddToClassList("box__input");
            address.maxLength = AddressLimit;
            address.SetValueWithoutNotify(lastAddress ?? "");
            address.style.marginBottom = 0;
            box.Add(address);
            Buttons(box, "BeaverBuddies.JoinCoopGame.Connect", Connect);

            // Once, over everything (localisation, click sounds, the list's scroll bars, typing in the field).
            initializer.InitializeVisualElement(_root);
            address.Q<TextElement>()?.SetConfirmCancelActions(inputService, Connect, OnUICancelled);

            refresh = _root.schedule.Execute(Refresh).Every(RefreshMs);
            Refresh();
        }

        /// <summary>
        /// Shows the box in place of the main menu or the game menu (as the game's Load Game box is shown).
        /// <paramref name="attachStyles"/>: in a game, adds the main menu's sheets to the box's root.
        /// </summary>
        public static void Show(PanelStack panelStack, VisualElementLoader loader, VisualElementInitializer initializer,
            InputService inputService, Action<VisualElement> attachStyles, string lastAddress, Action<ulong> joinLobby, Action<string> connect)
        {
            var box = new JoinCoopBox(panelStack, loader, initializer, inputService, attachStyles, lastAddress, joinLobby, connect);
            panelStack.HideAndPush(box);
        }

        public VisualElement GetPanel() => _root;

        // Enter: the address while it is being typed, else the selected friend's game. The game hands Enter to the panel
        // stack even while a text field has focus (its key bindings aren't blocked by typing), and the field's own
        // confirm only runs when it loses focus, in either order: so a player typing an IP with a friend's row still
        // selected was sent to the friend's game instead (review of beta24, B24-b).
        public bool OnUIConfirmed()
        {
            if (closed) return false;
            if (Typing())
            {
                if (!string.IsNullOrWhiteSpace(address.value)) Connect();
                return true;
            }
            if (list.selectedItem is FriendGame game && game.Joinable) Join();
            else if (!string.IsNullOrWhiteSpace(address.value)) Connect();
            return true;
        }

        // The address field (or the text input inside it) has the keyboard focus.
        private bool Typing() =>
            address.focusController?.focusedElement is VisualElement focused && (focused == address || address.Contains(focused));

        public void OnUICancelled() => Close();

        private static Label Title(string locKey)
        {
            var title = new Label(RegisteredLocalizationService.T(locKey));
            title.AddToClassList("text--big");
            title.AddToClassList("text--centered");
            title.AddToClassList("load-box__list-title");
            return title;
        }

        private static Button Buttons(VisualElement box, string locKey, Action clicked)
        {
            var row = new VisualElement();
            row.AddToClassList("box-buttons");
            var button = new NineSliceButton { text = RegisteredLocalizationService.T(locKey) };
            button.AddToClassList("menu-button");
            button.AddToClassList("menu-button--medium");
            button.clicked += clicked;
            row.Add(button);
            box.Add(row);
            return button;
        }

        // One friend: their name, what they are playing (gold, left) and whether you can join (gold, right).
        private static void Bind(VisualElement row, FriendGame game)
        {
            // The text is a friend's Steam name and what their lobby says: shown as typed, never as rich text.
            Show(row.Q<Label>("DisplayName"), game.Name);
            Show(row.Q<Label>("GameTime"), game.State == FriendGameState.Looking ? "" : game.Description);
            Show(row.Q<Label>("Timestamp"), StateText(game));
            row.SetEnabled(game.Joinable);
        }

        private static void Show(Label label, string text)
        {
            label.enableRichText = false;
            label.text = text;
        }

        private static string StateText(FriendGame game)
        {
            switch (game.State)
            {
                case FriendGameState.WaitingRoom: return RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.State.WaitingRoom");
                case FriendGameState.OpenGame: return RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.State.Open");
                case FriendGameState.Started: return RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.State.Started");
                case FriendGameState.OtherVersion:
                    return string.IsNullOrEmpty(game.Version)
                        ? RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.State.OlderVersion")
                        : RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.State.OtherVersion", game.Version);
                default: return RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.State.Looking");
            }
        }

        private void Refresh()
        {
            if (closed) return;
            List<FriendGame> found;
            try
            {
                found = FriendGameRules.Order(Scan());
                scanFailed = false;
            }
            catch (Exception error)
            {
                if (!scanFailed) Plugin.LogWarning("[Join] Could not list your Steam friends' games: " + error.Message);
                scanFailed = true;
                found = new List<FriendGame>();
            }

            // Rebound only when something changed, so a row doesn't flicker (or lose its hover) every two seconds.
            if (!SameGames(found))
            {
                ulong? selected = (list.selectedItem as FriendGame)?.FriendId;
                games.Clear();
                games.AddRange(found);
                list.RefreshItems();
                int keep = FriendGameRules.SelectionAfterRefresh(games, selected);
                if (keep >= 0) list.SetSelectionWithoutNotify((IEnumerable<int>)new[] { keep });
                else list.ClearSelection();
            }
            bool none = games.Count == 0;
            list.style.display = none ? DisplayStyle.None : DisplayStyle.Flex;
            empty.style.display = none ? DisplayStyle.Flex : DisplayStyle.None;
            empty.text = RegisteredLocalizationService.T("BeaverBuddies.JoinCoopGame.NoFriends");
            ShowJoinable();
        }

        private bool SameGames(List<FriendGame> found)
        {
            if (found.Count != games.Count) return false;
            for (int i = 0; i < found.Count; i++)
            {
                FriendGame a = found[i], b = games[i];
                if (a.FriendId != b.FriendId || a.LobbyId != b.LobbyId || a.Name != b.Name || a.State != b.State
                    || a.Description != b.Description || a.Version != b.Version) return false;
            }
            return true;
        }

        // Friends playing Timberborn in a lobby, and what their lobby says (asked of Steam now and then; Steam keeps it). A
        // friend may be a guest in the lobby: the row is the host's game, named after the host, once per lobby.
        private List<FriendGame> Scan()
        {
            var found = new List<FriendGame>();
            var lobbies = new HashSet<ulong>();
            if (!SteamOverlayConnectionService.IsSteamEnabled) return found;
            AppId_t game = SteamUtils.GetAppID();
            DateTime now = DateTime.UtcNow;
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t played)) continue;
                if (played.m_gameID.AppID() != game || !played.m_steamIDLobby.IsValid()) continue;
                CSteamID lobby = played.m_steamIDLobby;
                if (!lobbies.Add(lobby.m_SteamID)) continue;
                if (!askedAt.TryGetValue(lobby.m_SteamID, out DateTime asked) || now - asked > LobbyDataAgain)
                {
                    SteamMatchmaking.RequestLobbyData(lobby);
                    askedAt[lobby.m_SteamID] = now;
                }
                string version = SteamMatchmaking.GetLobbyData(lobby, SteamListener.VersionKey);
                string open = SteamMatchmaking.GetLobbyData(lobby, SteamListener.OpenKey);
                string room = SteamMatchmaking.GetLobbyData(lobby, SteamListener.RoomKey);
                string description = SteamMatchmaking.GetLobbyData(lobby, SteamListener.DescriptionKey);
                string host = SteamMatchmaking.GetLobbyData(lobby, SteamListener.HostKey);
                FriendGameState state = FriendGameRules.Classify(version, open, room, Plugin.Version);
                string name = string.IsNullOrEmpty(host) ? SteamFriends.GetFriendPersonaName(friend) : host;
                found.Add(new FriendGame(friend.m_SteamID, lobby.m_SteamID, name, state, description, version));
            }
            return found;
        }

        private void ShowJoinable() => join.SetEnabled(!closed && list.selectedItem is FriendGame game && game.Joinable);

        private void Join()
        {
            if (closed || !(list.selectedItem is FriendGame game) || !game.Joinable) return;
            Close();
            Plugin.Log($"[Join] Joining {game.Name}'s game (Steam lobby {game.LobbyId})");
            _joinLobby(game.LobbyId);
        }

        private void Connect()
        {
            string typed = address.value?.Trim();
            if (closed || string.IsNullOrEmpty(typed)) return;
            Close();
            _connect(typed);
        }

        private void Close()
        {
            if (closed) return;
            closed = true;
            refresh?.Pause();
            if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
        }
    }
}
