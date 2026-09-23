using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using BeaverBuddies.Steam;
using Steamworks;
using Timberborn.CoreUI;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.MainMenuPanels;
using Timberborn.OptionsGame;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    [HarmonyPatch(typeof(MainMenuPanel), "GetPanel")]
    public class MainMenuGetPanelPatcher
    {
        public static void Postfix(IPanelController __instance, ref VisualElement __result)
        {
            // Null while the registry is empty (LoadMap resets it a frame before the scene goes): a panel shown then must still show.
            SingletonManager.GetSingleton<ClientConnectionUI>()?.AddJoinButton(__result, mainMenu: true);
        }
    }

    [HarmonyPatch(typeof(GameOptionsBox), "GetPanel")]
    public class GameOptionsBoxGetPanelPatcher
    {
        public static void Postfix(IPanelController __instance, ref VisualElement __result)
        {
            // Null while the registry is empty (LoadMap resets it a frame before the scene goes): a panel shown then must still show.
            SingletonManager.GetSingleton<ClientConnectionUI>()?.AddJoinButton(__result, mainMenu: false);
        }
    }

    public class ClientConnectionUI : RegisteredSingleton
    {
        private InputBoxShower _inputBoxShower;
        private ClientConnectionService _clientConnectionService;
        private ILoc _loc;
        private Settings _settings;
        private PanelStack _panelStack;
        private VisualElementLoader _visualElementLoader;
        private VisualElementInitializer _visualElementInitializer;
        private InputService _inputService;

        public ClientConnectionUI(
            InputBoxShower inputBoxShower,
            ClientConnectionService clientConnectionService,
            ILoc loc,
            Settings settings,
            PanelStack panelStack,
            VisualElementLoader visualElementLoader,
            VisualElementInitializer visualElementInitializer,
            InputService inputService,
            DialogBoxShower dialogBoxShower
        )
        {
            _dialogBoxShower = dialogBoxShower;
            _inputBoxShower = inputBoxShower;
            _clientConnectionService = clientConnectionService;
            _loc = loc;
            _settings = settings;
            _panelStack = panelStack;
            _visualElementLoader = visualElementLoader;
            _visualElementInitializer = visualElementInitializer;
            _inputService = inputService;
        }

        public const string HostButtonName = "HostCoopButton";
        private readonly DialogBoxShower _dialogBoxShower;

        /// <summary>
        /// Host co-op game and Join co-op game, under Load game (1.4.0-rc4 adds Host). In the main menu, Host opens the Host
        /// co-op game box (HostCoopMenu) and Join the Join co-op game box. In a game, single player's Host co-op game hosts
        /// this game, and a co-op host's button is Save and Rehost; a guest has neither, and Join is not there: a Co-op Game
        /// page is joined from the main menu (D20).
        /// </summary>
        public void AddJoinButton(VisualElement __result, bool mainMenu)
        {
            Button host = ButtonInserter.DuplicateOrGetButton(__result, "LoadGameButton", HostButtonName, created =>
            {
                created.text = _loc.T("BeaverBuddies.Saving.HostCoopGame");
                created.clicked += () => HostClicked(mainMenu);
            }, _visualElementInitializer);
            if (!mainMenu) DressHostInGame(host);
            Button button = ButtonInserter.DuplicateOrGetButton(__result, HostButtonName, "JoinButton", button =>
            {
                button.text = _loc.T("BeaverBuddies.Menu.JoinCoopGame");
                button.clicked += () =>
                {
                    // Friends' games first, the address under them (JoinCoopBox); without Steam, the address box.
                    if (SteamOverlayConnectionService.IsSteamEnabled) ShowJoinBox();
                    else ShowBox();
                };
            }, _visualElementInitializer);
            // In a game every host is in a waiting room or a started game, and neither can be joined from a game (1.4.0-rc5
            // review, C6): the main menu's Join co-op game is the way in.
            button.ToggleDisplayStyle(mainMenu);
            if (mainMenu) FitMainMenu(__result);
        }

        // The game's main menu band (MainMenuPanel.uxml: .main-menu__content, 720 px, 104 of them the logo) was made for its
        // own ten buttons. With Host co-op game and Join co-op game its panel is taller than what is left, and it hung over
        // the band's bottom bar (1.4.0-rc5 review, C3). The band grows by what the panel needs, up to the screen's height:
        // its background and bars stretch, as the game's own do.
        private const string FitsPanelMarker = "beaverbuddies-fits-panel";
        private const float MainMenuBand = 720;
        // The breathing room above and below the panel, as the game's own spacers leave with its ten buttons.
        private const float PanelGap = 24;

        private static void FitMainMenu(VisualElement root)
        {
            VisualElement content = root.Q(className: "main-menu__content");
            VisualElement logo = root.Q(className: "background__logo");
            VisualElement panel = root.Q("MainMenuPanel");
            if (content == null || logo == null || panel == null || content.ClassListContains(FitsPanelMarker)) return;
            content.AddToClassList(FitsPanelMarker);
            void Fit(GeometryChangedEvent _)
            {
                float band = MainMenuFit.Band(panel.layout.height, logo.layout.height, root.layout.height, MainMenuBand, PanelGap);
                StyleLength wanted = band > MainMenuBand ? new StyleLength(band) : new StyleLength(StyleKeyword.Null);
                if (content.style.minHeight != wanted) content.style.minHeight = wanted;
            }
            panel.RegisterCallback<GeometryChangedEvent>(Fit);
            root.RegisterCallback<GeometryChangedEvent>(Fit);
        }

        // A game's menu: Host co-op game alone, Save and Rehost as the host of a co-op game, nothing as a guest (also after
        // the session ended: a guest's copy may be out of step, 1.4.0-rc5 review, A8).
        private void DressHostInGame(Button host)
        {
            HostButtonKind kind = HostKind();
            host.ToggleDisplayStyle(kind != HostButtonKind.Hidden && SingletonManager.GetSingleton<RehostingService>() != null);
            host.text = _loc.T(kind == HostButtonKind.SaveAndRehost ? "BeaverBuddies.ClientDesynced.SaveAndRehostButton" : "BeaverBuddies.Saving.HostCoopGame");
        }

        private static HostButtonKind HostKind()
        {
            ReplayService replay = SingletonManager.GetSingleton<ReplayService>();
            return HostButtonRules.Decide(coopGame: replay != null, loadedAsHost: replay?.LoadedAsHost == true, ReplayService.HasReplayFailure);
        }

        private void HostClicked(bool mainMenu)
        {
            if (mainMenu)
            {
                HostCoopMenu.Instance?.OpenBox();
                return;
            }
            RehostingService rehosting = SingletonManager.GetSingleton<RehostingService>();
            HostButtonKind kind = HostKind();
            if (rehosting == null || kind == HostButtonKind.Hidden) return;
            bool rehost = kind == HostButtonKind.SaveAndRehost;
            // It leaves this game for the main menu: asked first, saying what happens.
            _dialogBoxShower.Create()
                .SetMessage(_loc.T(rehost ? "BeaverBuddies.Host.Rehost.Confirm" : "BeaverBuddies.Host.FromGame.Confirm"))
                .SetConfirmButton(() =>
                {
                    bool saved = rehost ? rehosting.RehostGame() : rehosting.HostThisGame();
                    if (!saved) _dialogBoxShower.Create().SetLocalizedMessage("BeaverBuddies.ClientDesynced.FailedToRehostMessage").Show();
                }, _loc.T(rehost ? "BeaverBuddies.ClientDesynced.SaveAndRehostButton" : "BeaverBuddies.Saving.HostCoopGame"))
                .SetDefaultCancelButton()
                .Show();
        }

        private void ShowJoinBox()
        {
            try
            {
                JoinCoopBox.Show(_panelStack, _visualElementLoader, _visualElementInitializer, _inputService,
                    _settings.ClientConnectionAddress.Value,
                    lobby => SteamMatchmaking.JoinLobby(new CSteamID(lobby)),
                    ip =>
                    {
                        _settings.ClientConnectionAddress.SetValue(ip);
                        _clientConnectionService.ConnectOrShowFailureMessage(ip);
                    });
            }
            catch (Exception error)
            {
                // Never leave the player without a way to join: the address box always works.
                Plugin.LogWarning("[Join] Could not open the friends' games box; showing the address box: " + error);
                ShowBox();
            }
        }

        private void ShowBox()
        {
            ILoc _loc = _inputBoxShower._loc;
            var builder = _inputBoxShower.Create()
                .SetLocalizedMessage(_loc.T("BeaverBuddies.JoinCoopGame.EnterIp"))
                .SetConfirmButton(ip =>
                {
                    _settings.ClientConnectionAddress.SetValue(ip);
                    _clientConnectionService.ConnectOrShowFailureMessage(ip);
                });

            builder.Show();

            // Override max length for IPv6 and host names
            builder._input.maxLength = 128;
            builder._input.value = _settings.ClientConnectionAddress.Value;
        }
    }
}
