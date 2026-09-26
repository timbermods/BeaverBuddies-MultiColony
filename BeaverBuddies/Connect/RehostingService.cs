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
    /// A game hosts itself: it saves, as a new save of its settlement, and opens the Co-op Game room for that save as a
    /// window over this game (1.4.0-rc7; rc4 to rc6 went to the main menu for it), where the players join and ready up.
    /// Host co-op game from single player's game menu (a "… Co-op" save), and Save and Rehost in co-op (a "… Rehost" save:
    /// its players rejoin it). The save was just written, so Start makes no exit save of this game (K3).
    /// </summary>
    public class RehostingService : RegisteredSingleton
    {
        private readonly AutosaveNameService _autosaveNameService;
        private readonly GameSaver _gameSaver;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly SettlementReferenceService _settlementReferenceService;
        private readonly DialogBoxShower _dialogBoxShower;

        public RehostingService(
            AutosaveNameService autosaveNameService, 
            GameSaver gameSaver, 
            GameSaveRepository gameSaveRepository,
            SettlementReferenceService settlementReferenceService,
            DialogBoxShower dialogBoxShower
        ) 
        {
            _autosaveNameService = autosaveNameService;
            _gameSaver = gameSaver;
            _gameSaveRepository = gameSaveRepository;
            _settlementReferenceService = settlementReferenceService;
            _dialogBoxShower = dialogBoxShower;
        }

        // TODO: Should probably check IEnumerable<IAutosaveBlocker> autosaveBlockers
        // that Autosaver uses, both here and in general when a client joins to avoid
        // saving when it could corrupt things. Hopefully the save would fail if
        // there's a real issue, rather than corrupting, but I don't know...
        /// <param name="beforeSave">Run once the game may be saved, just before it is (the host's move notice to its guests).</param>
        public bool SaveRehostFile(Action<SaveReference> callback, bool waitUntilAccessible, string suffix = " Rehost", Action beforeSave = null)
        {
            if (ReplayService.HasReplayFailure)
            {
                _dialogBoxShower.Create()
                    .SetMessage("This game stopped after a failed multiplayer action. Go back to the main menu and load a good save before rehosting.")
                    .SetDefaultCancelButton().Show();
                return false;
            }
            beforeSave?.Invoke();
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
        /// Save and Rehost (the desync dialog, and the host's game menu in co-op): this game is saved and its Co-op Game room
        /// opens over it. Its guests, while its session still runs, are told first and follow into the room by themselves
        /// (1.4.0-rc7, K1: the notice, then the save, then the session's end, then the room); after a desync or a lost
        /// connection they come back to it with Reconnect or Rejoin.
        /// </summary>
        public bool RehostGame()
        {
            bool saved = SaveRehostFile(save => HostSaved(save, rehost: true), true, beforeSave: () => ServerHostingUtils.TellGuestsMoving());
            // The guests were told and have left: a save that failed after that ends the session they left.
            if (!saved) ServerHostingUtils.AbandonMove();
            return saved;
        }

        /// <summary>Host co-op game in single player's game menu: this game, saved, in its Co-op Game room over it.</summary>
        public bool HostThisGame()
        {
            return SaveRehostFile(save => HostSaved(save, rehost: false), true, " Co-op");
        }

        // The save just written (a frame ago: its file is closed), hosted in this game. No checks to run: this game wrote it.
        private void HostSaved(SaveReference save, bool rehost)
        {
            Plugin.Log($"[Lobby] Hosting \"{save.SaveName}\" in this game ({(rehost ? "a rehost" : "a game played alone")})");
            try
            {
                ServerHostingUtils.LoadAndHost(_gameSaveRepository, save, savedForRoom: true);
            }
            catch (Exception error)
            {
                Plugin.LogError("[Lobby] Could not open the Co-op Game room for the save: " + error);
                ServerHostingUtils.AbandonMove();
                _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.CouldNotOpen")).Show();
            }
        }
    }
}
