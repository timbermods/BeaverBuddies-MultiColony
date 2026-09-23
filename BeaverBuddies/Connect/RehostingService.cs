using BeaverBuddies.Util;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Timberborn.Autosaving;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameSaveRuntimeSystemUI;
using Timberborn.InputSystem;
using Timberborn.SaveSystem;
using Timberborn.SceneLoading;
using Timberborn.SettlementNameSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using static Timberborn.GameSaveRuntimeSystem.GameSaver;

namespace BeaverBuddies.Connect
{
    /// <summary>
    /// A game hosts itself (1.4.0-rc4): it saves, as a new save of its settlement, and opens the Co-op Game page for that
    /// save in the main menu (HostCoopFlow), where the players join and ready up. Host co-op game from single player's game
    /// menu (a "… Co-op" save), and Save and Rehost in co-op (a "… Rehost" save: its players rejoin it).
    /// </summary>
    public class RehostingService : RegisteredSingleton
    {
        private readonly AutosaveNameService _autosaveNameService;
        private readonly GameSaver _gameSaver;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly SettlementReferenceService _settlementReferenceService;
        private readonly ValidatingGameLoader _validatingGameLoader;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly Timberborn.MainMenuSceneLoading.MainMenuSceneLoader _mainMenuSceneLoader;

        public RehostingService(
            AutosaveNameService autosaveNameService, 
            GameSaver gameSaver, 
            GameSaveRepository gameSaveRepository,
            SettlementReferenceService settlementReferenceService,
            ValidatingGameLoader validatingGameLoader,
            DialogBoxShower dialogBoxShower,
            Timberborn.MainMenuSceneLoading.MainMenuSceneLoader mainMenuSceneLoader
        ) 
        {
            _mainMenuSceneLoader = mainMenuSceneLoader;
            _autosaveNameService = autosaveNameService;
            _gameSaver = gameSaver;
            _gameSaveRepository = gameSaveRepository;
            _settlementReferenceService = settlementReferenceService;
            _validatingGameLoader = validatingGameLoader;
            _dialogBoxShower = dialogBoxShower;
        }

        // TODO: Should probably check IEnumerable<IAutosaveBlocker> autosaveBlockers
        // that Autosaver uses, both here and in general when a client joins to avoid
        // saving when it could corrupt things. Hopefully the save would fail if
        // there's a real issue, rather than corrupting, but I don't know...
        public bool SaveRehostFile(Action<SaveReference> callback, bool waitUntilAccessible, string suffix = " Rehost")
        {
            if (ReplayService.HasReplayFailure)
            {
                _dialogBoxShower.Create()
                    .SetMessage("This session stopped after a failed multiplayer action. Return to the main menu and reload a known-good save before rehosting.")
                    .SetDefaultCancelButton().Show();
                return false;
            }
            if (waitUntilAccessible)
            {
                Action<SaveReference> originalCallback = callback;
                callback = saveReference =>
                {
                    // Run on next frame because the GameSaver doesn't release its
                    // handle on the save stream until the method finishes executing
                    // i.e. after the callback has run.
                    var mono = ServerHostingUtils.GetMonoBehaviour(_settlementReferenceService._sceneLoader);
                    TimeoutUtils.RunAfterFrames(mono, () =>
                    {
                        originalCallback(saveReference);
                    });
                };
            }
            SettlementReference settlementReference = _settlementReferenceService.SettlementReference;
            string saveName = _autosaveNameService.Timestamp().Replace(",", "") + suffix;
            SaveReference saveReference = new SaveReference(saveName, settlementReference);
            try
            {
                _gameSaver.SaveInstantlySkippingNameValidation(saveReference, () =>
                {
                    callback(saveReference);
                });
            }
            catch (GameSaverException ex)
            {
                Plugin.LogError($"Error occured while saving: {ex.InnerException}");
                _gameSaveRepository.DeleteSaveSafely(saveReference);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Failed to rehost: {ex}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Save and Rehost (the desync dialog, and the host's game menu in co-op): everyone leaves this game, which is saved,
        /// and its Co-op Game page opens in the main menu; its players rejoin it there (Reconnect, Rejoin).
        /// </summary>
        public bool RehostGame()
        {
            return SaveRehostFile(save => HostCoopFlow.HostInMainMenu(_mainMenuSceneLoader, save, rehost: true), true);
        }

        /// <summary>Host co-op game in single player's game menu: this game, saved, on its Co-op Game page.</summary>
        public bool HostThisGame()
        {
            return SaveRehostFile(save => HostCoopFlow.HostInMainMenu(_mainMenuSceneLoader, save, rehost: false), true, " Co-op");
        }
    }
}
