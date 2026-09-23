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
    /// The Load Game box's Host co-op game button (a copy of its Load), shown only while the box is open as the main
    /// menu's Host co-op game box (HostCoopMenu, 1.4.0-rc4). The Load Game box itself only loads, in the main menu and in a
    /// game: a game hosts itself from its game menu.
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
            ButtonInserter.DuplicateOrGetButton(__result, "LoadButton", LoadGameBoxHostButton.Name, (button) =>
            {
               button.text = _loc.T("BeaverBuddies.Saving.HostCoopGame");
               button.clicked += () => HostSelectedGame(__instance);
            });
            // Null in a game (the main menu's alone): the box is the Load Game box there.
            HostCoopMenu menu = SingletonManager.GetSingleton<HostCoopMenu>();
            if (menu != null) menu.Dress(__result);
            else __result.Q<Button>(LoadGameBoxHostButton.Name)?.ToggleDisplayStyle(false);
        }

        /// <summary>Enter or a double-click on a save: in the Host co-op game box, host it rather than load it.</summary>
        internal static void HostSelectedGameFromBox(LoadGameBox box) => HostSelectedGame(box);

        [ManualMethodOverwrite]
        /*
         * 04/19/2025
        if (_saveList.TryGetSelectedSave(out var selectedSave))
        {
            if (_gameSaveRepository.SaveExists(selectedSave.SaveReference))
            {
                _validatingGameLoader.LoadGameIfSaveValid(selectedSave.SaveReference);
                return true;
            }

            Debug.LogWarning("Save: " + selectedSave.DisplayName + " doesn't exist, failed to load.");
        }
        return false;
         */
        // Duplicates the LoadGameBox.LoadGame method, but loads the game with
        // the HostingSaveReference instead of ValidatingGameLoader
        private static void HostSelectedGame(LoadGameBox __instance)
        {
            if (__instance._saveList.TryGetSelectedSave(out var selectedSave))
            {
                if (__instance._gameSaveRepository.SaveExists(selectedSave.SaveReference))
                {
                    ServerHostingUtils.LoadIfSaveValidAndHost(__instance._validatingGameLoader, __instance._dialogBoxShower, selectedSave.SaveReference);
                }
                // Debug.LogWarning("Save: " + selectedSave.DisplayName + " doesn't exist, failed to load.");
            }
        }
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
    /// <summary>The Host co-op game box's Enter and double-click host the selected save (1.4.0-rc4); the Load Game box loads it.</summary>
    [HarmonyPatch(typeof(LoadGameBox), "LoadGame")]
    public class LoadGameBoxLoadGamePatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(LoadGameBox __instance, ref bool __result)
        {
            if (!HostCoopMenu.HostMode) return true;
            __result = __instance._saveList.TryGetSelectedSave(out _);
            LoadGameBoxGetPanelPatcher.HostSelectedGameFromBox(__instance);
            return false;
        }
    }

    /// <summary>The box closed: the next time it opens from Load Game it is the Load Game box again.</summary>
    [HarmonyPatch(typeof(LoadGameBox), nameof(LoadGameBox.OnUICancelled))]
    public class LoadGameBoxClosedPatcher
    {
        static void Postfix() => HostCoopMenu.BoxClosed();
    }

    /// <summary>A save was picked: the Host co-op game box's button follows the game's Load, and it says what the save is.</summary>
    [HarmonyPatch(typeof(LoadGameBox), "OnSaveSelectionChanged")]
    public class LoadGameBoxSaveSelectedPatcher
    {
        static void Postfix(LoadGameBox __instance)
        {
            if (!HostCoopMenu.HostMode) return;
            try
            {
                bool selected = __instance._saveList.TryGetSelectedSave(out GameSaveItem save);
                __instance.GetPanel()?.Q<Button>(LoadGameBoxHostButton.Name)?.SetEnabled(selected);
                HostCoopMenu.Instance?.SaveSelected(selected ? save.SaveReference : null);
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
                LoadAndHost(loader, shower, saveReference);
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

        public static void LoadAndHost(ValidatingGameLoader loader, DialogBoxShower shower, SaveReference saveReference)
        {
            var sceneLoader = loader._gameSceneLoader;
            var repository = sceneLoader._gameSaveRepository;
            byte[] data = GetMapBtyes(repository, saveReference);
            Plugin.Log($"Reading map with length {data.Length}");

            // A save is always hosted through its waiting room, the Co-op Game page (1.4.0-beta19; the only way since
            // 1.4.0-rc4): players join and ready up, and everyone loads the save together at Start. The page is the main
            // menu's: a game hosts itself by going there first (HostCoopFlow), never from here.
            BeaverBuddies.Lobby.LobbyHostPanel waitingRoom = SingletonManager.GetSingleton<BeaverBuddies.Lobby.LobbyHostPanel>();
            if (waitingRoom != null)
            {
                // Hosting starts here: a join or session left from before ends.
                EventIO.Reset();
                waitingRoom.OpenForSave(saveReference, data);
                return;
            }
            Plugin.LogWarning("[Lobby] A save can be hosted only from the main menu; nothing was hosted");
        }
    }
}
