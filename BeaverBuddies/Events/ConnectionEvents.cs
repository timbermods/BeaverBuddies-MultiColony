using BeaverBuddies.Colonies;
using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using BeaverBuddies.Reporting;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.Localization;
using Timberborn.Versioning;
using Timberborn.WebNavigation;
using UnityEngine.UIElements;

namespace BeaverBuddies.Events
{
    [Serializable]
    public class InitializeClientEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Global;
        public override bool ChangesGame() => false;

        public string serverModVersion;
        public string serverGameVersion;
        //public string mapName;
        public bool isDebugMode;
        // The host's choice for the session. Absent from an older host, which reads as the game's default.
        public bool removeLargeColonySpeedLimit;
        // The session's speed boost (SpeedBoost) as this player joins. Absent from an older host, which reads as 0.
        public float speedBoost;
        // The host started this session from a new game's waiting room: nobody joins late, so founding and the rest
        // don't wait for the first tick (ColonySession.JoiningClosedAtStart).
        public bool joiningClosedAtStart;
        // A mixed-factions game: the factions unlocked on the host's computer (D1), which a colony may take. Null otherwise.
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<string> hostFactions;

        public override void Replay(IReplayContext context)
        {
            //context.GetSingleton<ReplayService>().SetServerMapName(mapName);
            LargeColonySpeedLimit.AdoptHostChoice(removeLargeColonySpeedLimit);
            ColonySession.AdoptHostChoice(joiningClosedAtStart);
            ColonySession.AdoptHostFactions(hostFactions);
            context.GetSingleton<ReplayService>().SetBoost(speedBoost);
            string warningMessage = null;
            if (serverGameVersion != GameVersions.CurrentVersion.ToString())
            {
                warningMessage = $"Warning! Server Timberborn version ({serverGameVersion}) does not match client Timberborn version ({GameVersions.CurrentVersion}).\n" +
                    $"Please ensure that you are running the same version of the game.";
            } else if (serverModVersion != Plugin.Version)
            {
                warningMessage = $"Warning! Server mod version ({serverModVersion}) does not match client mod version ({Plugin.Version}).\n" +
                    $"Please ensure that you are running the same version of the {Plugin.Name} mod.";
            } else if (isDebugMode != Settings.Debug)
            {
                // TODO: Should debug mode just come from the server?
                // Could be a bit tricky, since it must come before load
                warningMessage = $"Warning! Server debug mode ({isDebugMode}) does not match client debug mode ({Settings.Debug}).\n" +
                    $"Please update your config files to be in or not in debug mode.";
            }
            if (warningMessage != null)
            {
                Plugin.LogWarning(warningMessage);
                context.GetSingleton<DialogBoxShower>().Create().SetMessage(warningMessage).Show();
            }
        }

        /// <param name="host">The session this message starts (built on a join thread: EventIO may not be it yet, or at all).</param>
        public static InitializeClientEvent Create(EventIO host = null)
        {
            InitializeClientEvent message = new InitializeClientEvent()
            {
                serverModVersion = Plugin.Version,
                serverGameVersion = GameVersions.CurrentVersion.ToString(),
                isDebugMode = Settings.Debug,
                removeLargeColonySpeedLimit = LargeColonySpeedLimit.BeginHostSession(host),
                speedBoost = ReplayService.SessionBoost,
                joiningClosedAtStart = ColonySession.JoiningClosedAtStart,
                // A waiting room's guest is sent this before the host's game exists (MixedFactions.IsOn is the menu's, or
                // between scenes): its room says whether the game is mixed, and latched the host's factions at Start.
                hostFactions = BeaverBuddies.Factions.MixedFactions.IsOn || BeaverBuddies.Lobby.LobbySession.Current?.Setup.Mixed == true
                    ? ColonySession.HostFactions?.ToList() : null,
                //mapName = mapName,
            };
            return message;
        }
    }

    [Serializable]
    public class ClientDesyncedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Global;
        public override bool ChangesGame() => false;

        public string desyncID;
        public string desyncTrace;
        /// <summary>
        /// Separate colonies: how many colony changes the desynced computer had counted at the last colony check that
        /// agreed (ColonyDigest.Agreed), and, when the colony check caught it, how many the host had at the one that
        /// differed. Every computer logs its changes from the one after the first.
        /// </summary>
        public int? colonyChangesAgreed;
        public int? colonyChangesHost;

        private void ConfirmConsent(IReplayContext context, Action confirmCallback)
        {
            Settings settings = context.GetSingleton<Settings>();

            // If they've already consented, just skip the dialog
            if (settings.ReportingConsent.Value)
            {
                confirmCallback();
                return;
            }
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            shower.Create()
                .SetLocalizedMessage("BeaverBuddies.ClientDesynced.ConsentMessage")
                .SetConfirmButton(() =>
                {
                    // Save the consent to the config
                    settings.ReportingConsent.SetValue(true);
                    confirmCallback();
                }, _loc.T("BeaverBuddies.ClientDesynced.ConsentAgreement"))
                .SetDefaultCancelButton()
                .Show();
        }

        private void PostDesync(IReplayContext context, Action<string> callback)
        {
            ReplayService replayService = context.GetSingleton<ReplayService>();
            ReportingService reportingService = context.GetSingleton<ReportingService>();
            RehostingService rehostingService = context.GetSingleton<RehostingService>();
            GameSaveRepository repository = context.GetSingleton<GameSaveRepository>();
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            string ioType = EventIO.Get()?.GetType().Name;
            string mapName = replayService.ServerMapName;
            Action<Task<bool>> onPost = (success) =>
            {
                if (success.Result)
                {
                    callback("BeaverBuddies.ClientDesynced.ReportSuccess");
                }
                else
                {
                    callback("BeaverBuddies.ClientDesynced.ReportFailed");
                }
            };

            string versionInfo = $"{Plugin.Name}: {Plugin.Version}; Timberborn: {GameVersions.CurrentVersion}";

            if (!rehostingService.SaveRehostFile(saveReference =>
            {
                byte[] mapBytes = ServerHostingUtils.GetMapBtyes(repository, saveReference);
                reportingService.PostDesync(desyncID, desyncTrace, ioType, mapName, versionInfo, mapBytes).ContinueWith(onPost);
            }, true))
            {
                _ = reportingService.PostDesync(desyncID, desyncTrace, ioType, mapName, versionInfo, null).ContinueWith(onPost);
            };
        }

        private void TurnOnTracing(Action<string> callback)
        {
            Settings.TemporarilyDebug = true;
            callback("BeaverBuddies.ClientDesynced.TracingEnabled");
        }

        public override void Replay(IReplayContext context)
        {
            ReplayService replayService = context.GetSingleton<ReplayService>();
            // Every computer logs its colony changes since the last count the desynced one agreed on: the desynced one as
            // it stops (HandleDesync plays this at once, a heartbeat's digest that differs included), the others as its
            // word arrives. The first line that differs between two players' logs is the change they did not make alike.
            // First, so nothing below can stop it, and caught, so it cannot stop anything below.
            if (Colonies.ColonyModeService.IsSeparateColonies)
            {
                try
                {
                    // A desync event without the count (none is sent in a shared game) lists the last 256 changes.
                    int agreed = colonyChangesAgreed ?? System.Math.Max(0, Colonies.ColonyDigest.Changes - 256);
                    Plugin.LogWarning($"[Colony] Colony changes here as {(replayService.IsDesynced ? "this computer" : "another player")} "
                        + $"desynced (tick {replayService.TicksSinceLoad}): {Colonies.ColonyDigest.DescribeSince(agreed, colonyChangesHost)}");
                }
                catch (Exception e) { Plugin.LogError("[Colony] Could not log the last colony changes: " + e); }
            }
            // The guest logged what its check found as it happened; the host's log names it too (its first line, as
            // a detailed-logging trace is long and was already written to the guest's log).
            if (EventIO.Get() is ServerEventIO && !string.IsNullOrEmpty(desyncTrace))
            {
                string first = desyncTrace.Split('\n')[0].Trim();
                Plugin.LogWarning("A player desynced: " + (first.Length > 300 ? first.Substring(0, 300) + "..." : first));
            }
            context.GetSingleton<BeaverBuddies.Fixes.MultiplayerInputRecovery>()?.RequestReset();
            // Paused, and the pick with it: otherwise picking the old speed again afterwards would be taken for asking for
            // the speed already picked, and ignored (SpeedChangePatcher).
            replayService.SetChosenSpeed(0);
            BeaverBuddies.DesyncDetecter.WaterDiagnostics.WriteOnDesync();
            BeaverBuddies.DesyncDetecter.WalkerDiagnostics.WriteOnDesync();
            // The other computers write their colony report too, as the desynced player's arrives: the last colony check
            // in each can then be set side by side. (The desynced computer wrote its own when it stopped.)
            if (!replayService.IsDesynced && Colonies.ColonyModeService.IsSeparateColonies)
                Colonies.ColonyDiagnostics.Instance?.WriteReport("another player desynced");
            ReportingService reportingService = context.GetSingleton<ReportingService>();
            RehostingService rehostingService = context.GetSingleton<RehostingService>();
            GameSaveRepository repository = context.GetSingleton<GameSaveRepository>();
            var shower = context.GetSingleton<DialogBoxShower>();
            ILoc _loc = shower._loc;
            Button infoButton = null;

            Action<string> infoCallback = (message) =>
            {
                if (infoButton != null)
                {
                    infoButton.text = _loc.T(message);
                }
            };
            Action bugReportAction = () =>
            {
                if (Settings.Debug)
                {
                    ConfirmConsent(context, () =>
                    {
                        infoButton?.SetEnabled(false);
                        PostDesync(context, infoCallback);
                    });
                }
                else
                {
                    infoButton?.SetEnabled(false);
                    TurnOnTracing(infoCallback);
                }
            };
            bool isHost = EventIO.Get() is ServerEventIO;
            Action reconnectAction = () =>
            {
                if (isHost)
                {
                    if (!rehostingService.RehostGame())
                    {
                        shower.Create()
                            .SetLocalizedMessage("BeaverBuddies.ClientDesynced.FailedToRehostMessage")
                            .Show();
                    }
                }
                else
                {
                    // The same way this guest joined: over Steam through the host's lobby, or to the address it typed.
                    context.GetSingleton<ClientConnectionService>()
                    ?.Reconnect();
                }
            };


            string reconnectText = isHost ? _loc.T("BeaverBuddies.ClientDesynced.SaveAndRehostButton") : _loc.T("BeaverBuddies.ClientDesynced.WaitForRehostButton");
            string reconnectMessage = _loc.T("BeaverBuddies.ClientDesynced.Message");
            // The report button needs an upload token, which public builds do not have, and the sentence that asks
            // every player to press Enable Logging is only added when that button is there (see DesyncDialogPlan).
            // Detailed logging would stop a large game for everyone: it is not offered there (1.4.0-rc1 review, D-S11).
            bool largeGame = (Colonies.ColonyDiagnostics.Instance?.CharactersInDistricts() ?? 0) >= DesyncDialogPlan.LargeGameCharacters;
            string bugReportMessageKey = DesyncDialogPlan.ReportButtonKey(Settings.Debug, reportingService.HasAccessToken, largeGame);
            // The question the report button answers, only with that button (1.4.0-rc5 review, C10).
            if (bugReportMessageKey != null) reconnectMessage += " " + _loc.T("BeaverBuddies.ClientDesynced.ReportQuestion");
            if (DesyncDialogPlan.AsksToEnableLogging(Settings.Debug, reportingService.HasAccessToken, largeGame))
            {
                reconnectMessage += "\n\n" + _loc.T("BeaverBuddies.ClientDesynced.NeedToEnableTracing");
            }

            var builder = shower.Create().SetMessage(reconnectMessage);
            if (bugReportMessageKey != null)
            {
                builder.SetInfoButton(bugReportAction, _loc.T(bugReportMessageKey));
            }
            DialogBox box = builder.SetConfirmButton(reconnectAction, reconnectText)
                .SetDefaultCancelButton()
                .Show();
            infoButton = box.GetPanel().Q<Button>("InfoButton");
        }
    }
}
