using BeaverBuddies.Factions;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSceneLoading;
using Timberborn.MainMenuSceneLoading;
using Timberborn.SingletonSystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    /// <summary>
    /// How a save is hosted (1.4.0-rc4): always through the Co-op Game page (the waiting room, LobbyHostPanel), where the
    /// friends join and press Ready and Start Game loads the save for everyone at once. The page is a main-menu page, so a
    /// game hosts itself by saving, going back to the main menu and opening the page for that save there: Host co-op game
    /// from single player's game menu, and Save and Rehost in co-op. Pending across the scene change, as plain static state.
    /// </summary>
    public static class HostCoopFlow
    {
        /// <summary>A save to open the Co-op Game page for as the main menu comes up (null: none).</summary>
        public static SaveReference PendingSave { get; private set; }

        /// <summary>From a game: open the Co-op Game page for this save in the main menu, going there now.</summary>
        /// <param name="rehost">A rehost (its players come back to it: Reconnect, Rejoin), for the log.</param>
        public static void HostInMainMenu(MainMenuSceneLoader mainMenuSceneLoader, SaveReference save, bool rehost)
        {
            PendingSave = save;
            Plugin.Log($"[Lobby] Hosting \"{save.SaveName}\" from the main menu ({(rehost ? "a rehost" : "a game played alone")})");
            mainMenuSceneLoader.OpenMainMenu();
        }

        /// <summary>The main menu takes the pending save (once).</summary>
        public static SaveReference TakePending()
        {
            SaveReference save = PendingSave;
            PendingSave = null;
            return save;
        }
    }

    /// <summary>
    /// The main menu's side of a game hosting itself (1.4.0-rc4): a save handed over by a game (HostCoopFlow.PendingSave)
    /// opens its Co-op Game page as soon as the main menu is up, through the game's own save checks. (Hosting a save picked
    /// in the Load game box is the box's own Host co-op game since 1.4.0-rc7: LoadGameBoxGetPanelPatcher.)
    /// </summary>
    public class HostCoopMenu : RegisteredSingleton, IUpdatableSingleton
    {
        private readonly ValidatingGameLoader _validatingGameLoader;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly PanelStack _panelStack;
        private int framesInMenu;

        public HostCoopMenu(ValidatingGameLoader validatingGameLoader, DialogBoxShower dialogBoxShower, PanelStack panelStack)
        {
            _validatingGameLoader = validatingGameLoader;
            _dialogBoxShower = dialogBoxShower;
            _panelStack = panelStack;
        }

        public void UpdateSingleton()
        {
            // A save a game handed over: its page opens once the main menu is up (not under a dialog or an overlay).
            if (HostCoopFlow.PendingSave == null) return;
            if (framesInMenu++ < 2) return;
            if (_panelStack._stack.Count == 0 || _panelStack.TopPanel.IsOverlay) return;
            SaveReference save = HostCoopFlow.TakePending();
            try
            {
                EventIO.Reset();
                ServerHostingUtils.LoadIfSaveValidAndHost(_validatingGameLoader, _dialogBoxShower, save);
            }
            catch (Exception error)
            {
                Plugin.LogError("[Lobby] Could not open the Co-op Game page for the save: " + error);
                _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.CouldNotOpen")).Show();
            }
        }
    }
}
