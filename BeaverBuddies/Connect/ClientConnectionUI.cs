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
            InputService inputService
        )
        {
            _inputBoxShower = inputBoxShower;
            _clientConnectionService = clientConnectionService;
            _loc = loc;
            _settings = settings;
            _panelStack = panelStack;
            _visualElementLoader = visualElementLoader;
            _visualElementInitializer = visualElementInitializer;
            _inputService = inputService;
        }

        public void AddJoinButton(VisualElement __result, bool mainMenu)
        {
            Button button = ButtonInserter.DuplicateOrGetButton(__result, "LoadGameButton", "JoinButton", button =>
            {
                button.text = _loc.T("BeaverBuddies.Menu.JoinCoopGame");
                button.clicked += () =>
                {
                    // The main menu with Steam: friends' games first, the address under them (JoinCoopBox). In a game (a
                    // waiting room can't be joined from one, D20) or without Steam: the address box, as before.
                    if (mainMenu && SteamOverlayConnectionService.IsSteamEnabled) ShowJoinBox();
                    else ShowBox();
                };
            });
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
