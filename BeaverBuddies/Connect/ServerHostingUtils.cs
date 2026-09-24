using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.MainMenuScene;
using Timberborn.Localization;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json.Bson;
using Timberborn.GameSceneLoading;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using Timberborn.CoreUI;
using System.Collections;
using BeaverBuddies.Steam;
using TimberNet;
using UnityEngine;
using Timberborn.SceneLoading;
using Timberborn.Common;

namespace BeaverBuddies.Connect
{

    /// <summary>
    /// The Load game box's Host co-op game button (1.4.0-rc7): a copy of its Load, right of it, in the main menu and in a
    /// game. It hosts the selected save through its Co-op Game room (LobbyHostPanel.OpenForSave): the main menu's page, or
    /// a game's window over the game. Load, Enter and a double-click load, as the game made them. Shown as the game menu's
    /// hosting button is (HostButtonRules.ShowOnLoadBox): not for a game loaded as a guest, nor after a failed action.
    /// </summary>
    public static class LoadGameBoxHostButton
    {
        public const string Name = "HostButton";
    }

    [HarmonyPatch(typeof(LoadGameBox), nameof(LoadGameBox.GetPanel))]
    public class LoadGameBoxGetPanelPatcher
    {
        public static void Postfix(LoadGameBox __instance, ref VisualElement __result)
        {
            if (__result == null) return;
            ILoc _loc = __instance._loc;
            Button host = ButtonInserter.DuplicateOrGetButton(__result, "LoadButton", LoadGameBoxHostButton.Name, (button) =>
            {
               button.text = _loc.T("BeaverBuddies.Saving.HostCoopGame");
               button.clicked += () => HostSelectedGame(__instance);
            }, __instance._visualElementLoader?._visualElementInitializer);
            bool shown = HostButtonRules.ShowOnLoadBox(SingletonManager.GetSingleton<BeaverBuddies.Lobby.LobbyHostPanel>() != null,
                ClientConnectionUI.HostKind());
            host.ToggleDisplayStyle(shown);
            // Four medium buttons do not fit the box as the game sizes it: it is made wider while Host co-op game is there
            // (LoadBoxFit), never its buttons narrower.
            VisualElement box = __result.Q(className: "load-box");
            if (box != null) box.style.width = shown ? new StyleLength(LoadBoxFit.WidenedBox) : new StyleLength(StyleKeyword.Null);
            // Under the picture, what the selected save is.
            LoadGameBoxColonies.Of(__result);
        }

        [ManualMethodOverwrite]
        /*
         * 2026-09-23, Timberborn 1.1.2.4, GameSaveRepositorySystemUI: LoadGameBox.LoadGame
        if (_saveList.TryGetSelectedSave(out var selectedSave))
        {
            if (_gameSaveRepository.SaveExists(selectedSave.SaveReference))
            {
                _validatingGameLoader.LoadGame(selectedSave.SaveReference);
                return true;
            }
            UnityEngine.Debug.LogWarning("Save: " + selectedSave.DisplayName + " doesn't exist, failed to load.");
        }
        return false;
         */
        // LoadGameBox.LoadGame, hosting the selected save through the game's own save checks (ValidatingGameLoader's
        // validators, as a load goes through them) instead of loading it.
        private static void HostSelectedGame(LoadGameBox __instance)
        {
            if (__instance._saveList.TryGetSelectedSave(out var selectedSave))
            {
                if (__instance._gameSaveRepository.SaveExists(selectedSave.SaveReference))
                {
                    ServerHostingUtils.LoadIfSaveValidAndHost(__instance._validatingGameLoader, __instance._dialogBoxShower, selectedSave.SaveReference);
                    return;
                }
                Plugin.LogWarning("[Lobby] The save " + selectedSave.SaveReference.SaveName + " doesn't exist, so it was not hosted.");
            }
        }
    }

    /// <summary>A save was picked: Host co-op game follows the game's Load, and the line under the picture says what it is.</summary>
    [HarmonyPatch(typeof(LoadGameBox), "OnSaveSelectionChanged")]
    public class LoadGameBoxSaveSelectedPatcher
    {
        static void Postfix(LoadGameBox __instance)
        {
            try
            {
                bool selected = __instance._saveList.TryGetSelectedSave(out GameSaveItem save);
                VisualElement root = __instance.GetPanel();
                root?.Q<Button>(LoadGameBoxHostButton.Name)?.SetEnabled(selected);
                LoadGameBoxColonies.Of(root)?.Show(selected ? save.SaveReference : null, __instance._gameSaveRepository);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Lobby] Could not show the selected save's colonies: " + error.Message);
            }
        }
    }

    internal class ServerHostingUtils
    {
        public static void LoadIfSaveValidAndHost(ValidatingGameLoader loader, DialogBoxShower shower, SaveReference saveReferece)
        {
            CheckNextValidator(loader, shower, saveReferece, 0);
        }

        [ManualMethodOverwrite]
        /*
        04/19/2025 ValidatingGameLoader.CheckNextValidator
        if (index >= _gameLoadValidators.Length)
        {
	        _gameSceneLoader.StartSaveGame(saveReference);
	        return;
        }
        _gameLoadValidators[index].ValidateSave(saveReference, delegate
        {
	        CheckNextValidator(saveReference, index + 1);
        });
         */
        private static void CheckNextValidator(ValidatingGameLoader loader, DialogBoxShower shower, SaveReference saveReference, int index)
        {
            if (index >= loader._gameLoadValidators.Length)
            {
                LoadAndHost(loader._gameSceneLoader._gameSaveRepository, saveReference, savedForRoom: false);
                return;
            }
            loader._gameLoadValidators[index].ValidateSave(saveReference, delegate
            {
                CheckNextValidator(loader, shower, saveReference, index + 1);
            });
        }

        public static byte[] GetMapBtyes(GameSaveRepository repository, SaveReference saveReference)
        {
            var inputStream = repository.OpenSaveWithoutLogging(saveReference);
            byte[] data;
            using (var memoryStream = new MemoryStream())
            {
                inputStream.CopyTo(memoryStream);
                data = memoryStream.ToArray();
            }
            inputStream.Close();
            return data;
        }

        public static MonoBehaviour GetMonoBehaviour(ISceneLoader loader)
        {
            return ((SceneLoader)loader)._coroutineStarter._monoBehaviour;
        }

        /// <summary>
        /// The save's Co-op Game room, in this scene (1.4.0-rc7): the main menu's page, or a window over this game. After the
        /// game's own checks of a save picked in the Load game box, or at once for a save this game has just written for it
        /// (<paramref name="savedForRoom"/>: the game menu's Host co-op game and Save and Rehost).
        /// </summary>
        public static void LoadAndHost(GameSaveRepository repository, SaveReference saveReference, bool savedForRoom)
        {
            byte[] data = GetMapBtyes(repository, saveReference);
            Plugin.Log($"Reading map with length {data.Length}");

            // A save is always hosted through its waiting room, the Co-op Game room (1.4.0-beta19; the only way since
            // 1.4.0-rc4): players join and ready up, and everyone loads the save together at Start. Its panel is bound in the
            // main menu and in every game (1.4.0-rc7), which it opens over.
            BeaverBuddies.Lobby.LobbyHostPanel waitingRoom = SingletonManager.GetSingleton<BeaverBuddies.Lobby.LobbyHostPanel>();
            if (waitingRoom == null)
            {
                Plugin.LogWarning("[Lobby] No Co-op Game room can open in this scene; nothing was hosted");
                return;
            }
            // Hosting starts here: a session or join left from before ends.
            EndSessionForRoom();
            waitingRoom.OpenForSave(saveReference, data, savedForRoom);
        }

        /// <summary>
        /// Before a room opens, whatever session this game still has ends, so that the room's server can take the port: a
        /// co-op game's session ends quietly (its host chose to host again; the game stays, played alone until Start), and a
        /// join left from before is closed.
        /// </summary>
        internal static void EndSessionForRoom()
        {
            // A join under way ends: held apart from a game it is not EventIO, and nothing else would close it.
            ClientConnectionService joins = SingletonManager.GetSingleton<ClientConnectionService>();
            joins?.EndJoin(joins.CurrentJoin);
            if (EventIO.IsNull) return;
            SingletonManager.GetSingleton<ReplayService>()?.EndSession(null);
            // A desync or a failed action ended the session already, and may have left its connection installed.
            EventIO.Reset();
        }
    }
}
