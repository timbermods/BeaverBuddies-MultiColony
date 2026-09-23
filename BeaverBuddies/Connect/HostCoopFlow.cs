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
        /// <summary>The pending save is a rehost: its players come back to it (Reconnect, Rejoin).</summary>
        public static bool PendingRehost { get; private set; }

        /// <summary>From a game: open the Co-op Game page for this save in the main menu, going there now.</summary>
        public static void HostInMainMenu(MainMenuSceneLoader mainMenuSceneLoader, SaveReference save, bool rehost)
        {
            PendingSave = save;
            PendingRehost = rehost;
            Plugin.Log($"[Lobby] Hosting \"{save.SaveName}\" from the main menu ({(rehost ? "a rehost" : "a game played alone")})");
            mainMenuSceneLoader.OpenMainMenu();
        }

        /// <summary>The main menu takes the pending save (once).</summary>
        public static SaveReference TakePending()
        {
            SaveReference save = PendingSave;
            PendingSave = null;
            PendingRehost = false;
            return save;
        }
    }

    /// <summary>
    /// The main menu's side of hosting a save (1.4.0-rc4):
    /// <list type="bullet">
    /// <item>Host co-op game on the main menu opens the game's own Load Game box as the Host co-op game box: its title,
    /// its settlements and saves, and Host co-op game in place of Load. Under the save's picture, in the gold of the save
    /// list's details, it says what the selected save is: separate colonies (and how many players it remembers), or one
    /// shared colony, which the Co-op Game page can make separate.</item>
    /// <item>A save handed over by a game (HostCoopFlow.PendingSave) opens its Co-op Game page as soon as the main menu
    /// is up.</item>
    /// </list>
    /// </summary>
    public class HostCoopMenu : RegisteredSingleton, IUpdatableSingleton
    {
        public const string StatusName = "BeaverBuddiesSaveColonies";

        /// <summary>The Load Game box is open as the Host co-op game box.</summary>
        public static bool HostMode { get; private set; }

        private readonly LoadGameBox _loadGameBox;
        private readonly ValidatingGameLoader _validatingGameLoader;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly PanelStack _panelStack;
        private readonly GameSaveRepository _gameSaveRepository;

        // What each save is, read off the menu's thread once per box (the reader streams the save's world, tens of ms).
        private readonly Dictionary<string, SaveColonyInfo> read = new Dictionary<string, SaveColonyInfo>();
        private Task<SaveColonyInfo> reading;
        private string readingKey;
        private string shownKey;
        private Label status;
        private int framesInMenu;
        // The box's own title (Load game, in the player's language), kept to put back.
        private string loadTitle;

        public static HostCoopMenu Instance => SingletonManager.GetSingleton<HostCoopMenu>();

        public HostCoopMenu(LoadGameBox loadGameBox, ValidatingGameLoader validatingGameLoader, DialogBoxShower dialogBoxShower,
            PanelStack panelStack, GameSaveRepository gameSaveRepository)
        {
            _loadGameBox = loadGameBox;
            _validatingGameLoader = validatingGameLoader;
            _dialogBoxShower = dialogBoxShower;
            _panelStack = panelStack;
            _gameSaveRepository = gameSaveRepository;
            HostMode = false;
        }

        /// <summary>Host co-op game on the main menu.</summary>
        public void OpenBox()
        {
            HostMode = true;
            read.Clear();
            shownKey = null;
            _loadGameBox.Open();
        }

        /// <summary>The box closed (its close button, Esc): it is the Load Game box again.</summary>
        public static void BoxClosed() => HostMode = false;

        /// <summary>
        /// The box is shown: in host mode, its title and main button are Host co-op game's, and the save's colonies are
        /// said under its picture; otherwise it is the game's Load Game box, as the game made it.
        /// </summary>
        public void Dress(VisualElement root)
        {
            Label header = root.Q<Label>("Header");
            if (header != null)
            {
                string hostTitle = RegisteredLocalizationService.T("BeaverBuddies.Saving.HostCoopGame");
                // The game's own title, the first time it is seen (before this ever changed it).
                if (loadTitle == null && header.text != hostTitle) loadTitle = header.text;
                if (HostMode) header.text = hostTitle;
                else if (loadTitle != null) header.text = loadTitle;
            }
            root.Q<Button>("LoadButton")?.ToggleDisplayStyle(!HostMode);
            root.Q<Button>(LoadGameBoxHostButton.Name)?.ToggleDisplayStyle(HostMode);
            if (status == null || status.panel == null && root.Q<Label>(StatusName) == null)
            {
                status = new Label { name = StatusName };
                status.AddToClassList("game-text-small");
                status.AddToClassList("text--yellow");
                status.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                status.style.whiteSpace = WhiteSpace.Normal;
                status.style.maxWidth = 300;
                status.style.marginTop = 6;
                // Two lines kept for it while it is shown, so the save list does not move as each save's line is read.
                status.style.minHeight = 30;
                VisualElement saves = root.Q("SavesWrapper");
                if (saves != null) saves.Add(status);
            }
            status.text = "";
            status.ToggleDisplayStyle(HostMode);
        }

        /// <summary>The selected save changed: in host mode, say what it is (read once, off the menu's thread).</summary>
        public void SaveSelected(SaveReference save)
        {
            if (!HostMode || status == null) return;
            string key = Key(save);
            shownKey = key;
            if (save == null)
            {
                Show(null);
                return;
            }
            if (read.TryGetValue(key, out SaveColonyInfo info))
            {
                Show(info);
                return;
            }
            Show(null);
            if (reading != null && readingKey == key) return;
            try
            {
                byte[] bytes = ServerHostingUtils.GetMapBtyes(_gameSaveRepository, save);
                readingKey = key;
                reading = Task.Run(() => SaveColonyReader.Read(bytes));
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Lobby] Could not read the save's colonies: " + error.Message);
            }
        }

        public void UpdateSingleton()
        {
            // A save's colonies, read.
            if (reading != null && reading.IsCompleted)
            {
                SaveColonyInfo info = reading.Status == TaskStatus.RanToCompletion ? reading.Result : null;
                read[readingKey] = info;
                if (readingKey == shownKey) Show(info);
                reading = null;
            }
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

        private void Show(SaveColonyInfo info)
        {
            if (status == null) return;
            status.text = info == null ? "" : StatusText(info);
        }

        /// <summary>What a save is, for the Host co-op game box (display only).</summary>
        public static string StatusText(SaveColonyInfo info)
        {
            int players = Lobby.LobbyRules.PlayersRemembered(info?.SlotTable);
            string key = Lobby.LobbyRules.SaveStatusKey(info != null, info?.SeparateColonies ?? false, info?.Mixed ?? false, players);
            return key == null ? "" : RegisteredLocalizationService.T(key, players);
        }

        private static string Key(SaveReference save) => save == null ? "" : save.SettlementReference?.SettlementName + "/" + save.SaveName;
    }
}
