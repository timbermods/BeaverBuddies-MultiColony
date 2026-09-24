using BeaverBuddies.Connect;
using BeaverBuddies.DesyncDetecter;
using BeaverBuddies.Editor;
using BeaverBuddies.Events;
using BeaverBuddies.Fixes;
using BeaverBuddies.Help;
using BeaverBuddies.IO;
using BeaverBuddies.MultiStart;
using BeaverBuddies.Reporting;
using BeaverBuddies.Steam;
using BeaverBuddies.Util;
using BeaverBuddies.Util.Logging;
using Bindito.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Timberborn.ModManagerScene;

namespace BeaverBuddies
{
    [Context("Game")]
    public class ReplayConfigurator : IConfigurator
    {

        public void Configure(IContainerDefinition containerDefinition)
        {
            if (Plugin.Disabled) return;
            // Reset everything before loading singletons
            SingletonManager.Reset();

            Plugin.Log($"Registering In Game Services");

            // Add client connection Singletons, since we can now
            // connect from the in-game Options menu (even if we're not
            // playing co-op right now).
            containerDefinition.Bind<ClientConnectionService>().AsSingleton();
            containerDefinition.Bind<ClientConnectionUI>().AsSingleton();
            containerDefinition.Bind<SteamOverlayConnectionService>().AsSingleton();
            containerDefinition.Bind<RegisteredLocalizationService>().AsSingleton();
            containerDefinition.Bind<Settings>().AsSingleton();
            containerDefinition.Bind<ModListService>().AsSingleton();

            MultiStartConfigurator.Configure(containerDefinition);
            BeaverBuddies.Colonies.ColonyConfigurator.Configure(containerDefinition);
            // A new game's waiting room makes its world in a single-player scene, then loads it as the hosted game.
            containerDefinition.Bind<BeaverBuddies.Lobby.LobbyWorldMaker>().AsSingleton();
            // Host co-op game in a game played alone, and Save and Rehost in co-op: both save this game and open its Co-op
            // Game room over it. Bound in every game, before the co-op-only services below (1.4.0-rc5 review, A1).
            containerDefinition.Bind<RehostingService>().AsSingleton();
            // A save's Co-op Game room opens in a game too, as a window over it (1.4.0-rc7): the host's panel, what a mixed
            // save's room offers (the host's unlocked factions), and the game scene's side of it (the main menu's style
            // sheets, and which scene this is). In every game, co-op or not, so before the co-op-only services below.
            containerDefinition.Bind<BeaverBuddies.Lobby.InGameLobby>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Lobby.LobbyHostPanel>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Factions.NewGameFactionCapture>().AsSingleton();
            // And a host's room joined from this game (an invite, the game menu's Join co-op game): its window over it.
            containerDefinition.Bind<BeaverBuddies.Lobby.LobbyGuestPanel>().AsSingleton();

            // EventIO gets set before load, so if it's null, this is a regular
            // game, so don't initialize these services.
            if (EventIO.IsNull) return;

            Plugin.Log("Registering Co-op services");
            //containerDefinition.Bind<ServerConnectionService>().AsSingleton();
            containerDefinition.Bind<ReplayService>().AsSingleton();
            containerDefinition.Bind<TickProgressService>().AsSingleton();
            containerDefinition.Bind<TickingService>().AsSingleton();
            containerDefinition.Bind<DeterminismService>().AsSingleton();
            containerDefinition.Bind<TickReplacerService>().AsSingleton();
            containerDefinition.Bind<WonderTickService>().AsSingleton();
            containerDefinition.Bind<MultiplayerInputRecovery>().AsSingleton();
            containerDefinition.Bind<ReportingService>().AsSingleton();
            containerDefinition.Bind<LateTickableBuffer>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Ping.PingService>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Activity.PlayerActivityService>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Panel.ConnectionPanelService>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Activity.PlayerCursorSettingsUI>().AsSingleton();
            containerDefinition.Bind<ModMismatchWarningService>().AsSingleton();
            containerDefinition.Bind<DevModeCoopWarning>().AsSingleton();
            containerDefinition.Bind<CoopFixGuard>().AsSingleton();
            containerDefinition.Bind<TickOnceCoopNotice>().AsSingleton();
            containerDefinition.Bind<GateTickRunner>().AsSingleton();
            containerDefinition.Bind<RealGateConflict>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Latency.PendingActions>().AsSingleton();
            // We can safely add this regardless of whether tracing is enabled
            // because it will only trace if the config is set to do so.
            containerDefinition.Bind<DesyncDetecterService>().AsSingleton();

        }
    }

    [Context("MainMenu")]
    public class ConnectionMenuConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            if (Plugin.Disabled)
            {
                containerDefinition.Bind<DuplicateModWarning>().AsSingleton();
                return;
            }
            // This will be called if the player exits to the main menu,
            // so it's best to reset everything.
            SingletonManager.Reset();
            // A new game's waiting room left over from a scene the flow did not expect (its guests are told why).
            BeaverBuddies.Lobby.LobbySession.EndStale("the host went back to the main menu");
            EventIO.Reset();
            // A faction picked in an earlier waiting room is not this next game's.
            BeaverBuddies.Factions.LocalFactionPick.Clear();
            // No game is loaded, so nothing is mixed (until 1.4.0-rc7 NewGameFactionCapture did this as it was made; it is
            // made in every game now, where it must leave the loaded game's factions alone).
            BeaverBuddies.Factions.MixedFactions.Reset();
            // Nor a hosted save's conversion to separate colonies that never started (1.4.0-rc4).
            BeaverBuddies.Colonies.SaveConversion.Pending = null;

            Plugin.Log($"Registering Main Menu Services");
            containerDefinition.Bind<ClientConnectionService>().AsSingleton();
            containerDefinition.Bind<ClientConnectionUI>().AsSingleton();
            containerDefinition.Bind<FirstTimerService>().AsSingleton();
            containerDefinition.Bind<ChangeLogService>().AsSingleton();
            containerDefinition.Bind<RegisteredLocalizationService>().AsSingleton();
            containerDefinition.Bind<MultiplayerMapMetadataService>().AsSingleton();
            containerDefinition.Bind<Settings>().AsSingleton();
            containerDefinition.Bind<ModListService>().AsSingleton();
            // Whether a new game made here is a mixed-factions game (the host's setting and unlocks).
            containerDefinition.Bind<BeaverBuddies.Factions.NewGameFactionCapture>().AsSingleton();
            // The Game Mode page's colony checkboxes (Separate colonies, and under it science and factions).
            containerDefinition.Bind<BeaverBuddies.Lobby.NewGameColonyOptions>().AsSingleton();

            //new ReportingService().PostDesync("test").ContinueWith(result => Plugin.Log($"Posted: {result.Result}"));
            containerDefinition.Bind<SteamOverlayConnectionService>().AsSingleton();
            containerDefinition.Bind<DuplicateModWarning>().AsSingleton();
            // A new game's waiting room (Host co-op game on the Game Mode page), a save's (the Load game box's Host co-op
            // game), and a guest's page in one.
            containerDefinition.Bind<BeaverBuddies.Lobby.LobbyHostPanel>().AsSingleton();
            containerDefinition.Bind<BeaverBuddies.Lobby.LobbyGuestPanel>().AsSingleton();

            //ReflectionUtils.PrintChildClasses(typeof(MonoBehaviour),
            //    "Start", "Awake", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy");
            //ReflectionUtils.PrintChildClasses(typeof(IUpdatableSingleton));
            //ReflectionUtils.PrintChildClasses(typeof(ILateUpdatableSingleton));
            //ReflectionUtils.PrintChildClasses(typeof(IBatchControlRowItem));
            //ReflectionUtils.PrintChildClasses(typeof(IUpdateableBatchControlRowItem));
            //ReflectionUtils.PrintChildClasses(typeof(IParallelTickableSingleton));
            //ReflectionUtils.FindStaticFields();
            //ReflectionUtils.FindHashSetFields();
            //ReflectionUtils.PrintChildClasses(typeof(IEntityPanelFragment));
        }
    }

    [HarmonyPatch]
    public class Plugin : IModStarter
    {
        public static readonly string Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public const string Name = "BeaverBuddies MultiColony";
        /// <summary>This mod's own id (manifest.json). Mod Settings keeps its settings under it.</summary>
        public const string ID = "timbermods.BeaverBuddiesMultiColony";
        /// <summary>The id of the original BeaverBuddies and of the Stability Fork, which cannot run alongside this.</summary>
        public const string OtherBeaverBuddiesID = "beaverbuddies";

        /// <summary>
        /// Another BeaverBuddies started first: this one patches nothing and binds nothing but the main menu's warning,
        /// so the game runs as the other one alone.
        /// </summary>
        public static bool Disabled { get; private set; }

        private static ILogger logger;

        /// <summary>The folder this mod runs from (its manifest, DLLs, Buildings and TemplateCollections).</summary>
        public static string ModPath { get; private set; }

        public void StartMod(IModEnvironment modEnvironment)
        {
            logger = new UnityLogger();
            ModPath = modEnvironment?.ModPath;

            Log($"{Name} v{Version} is loaded!");

            // Another BeaverBuddies already patched the game: patching it again would break both. The main menu says
            // why (DuplicateModWarning) and this copy stays out of the way.
            if (Harmony.HasAnyPatches(OtherBeaverBuddiesID))
            {
                LogError("Another BeaverBuddies mod is enabled and already running; BeaverBuddies MultiColony will not start. Disable the other one and restart.");
                Disabled = true;
                return;
            }

            // Colony state changes count towards the digest only in a separate-colonies game, inside the simulation, once
            // a game is loaded. A shared game counts nothing.
            Colonies.ColonyDigest.Gate = () => Colonies.ColonyModeService.IsSeparateColonies && ReplayService.IsLoaded
                && (DeterminismService.IsTicking || ReplayService.IsReplayingEvents);

            // apply all harmony patches automatically (mixed factions' on their own, see PatchAllIsolatingFactions).
            Harmony harmony = new Harmony(ID);
            PatchAllIsolatingFactions(harmony);
            AutomationEvent.ApplyAutomationPatches(harmony);

            // apply each advanced monomod patch manually.
            Install(nameof(GameSaverSavePatcher), GameSaverSavePatcher.Install);
            Install(nameof(TimeTimePatcher), TimeTimePatcher.Install);

            Log(UnityEngine.Application.consoleLogPath);
        }

        /// <summary>
        /// Patch classes, and hand-made patches, that could not be applied to this game version (a game update renamed
        /// or changed what they patch). Empty normally. Single player carries on without them; a co-op game is stopped
        /// at load while any is missing (CoopFixGuard), since one computer's game would then do what the other's does
        /// not, with nobody told why.
        /// </summary>
        internal static readonly List<string> FailedPatches = new List<string>();

        private static void Install(string name, Action install)
        {
            try { install(); }
            catch (Exception error)
            {
                FailedPatches.Add(name);
                LogError($"Could not apply {name} to this game version (a game update?): co-op is refused until MultiColony is updated. {error}");
            }
        }

        /// <summary>
        /// PatchAll, except that mixed factions' patches (BeaverBuddies.Factions: UI, models and needs that only a mixed game
        /// uses) go last and on their own: if a game update breaks one, mixed factions is off for this run
        /// (MixedFactions.Unavailable) and the rest of the mod still starts. Every mixed-factions patch but the decision does
        /// nothing while mixed factions is off (RuntimeChecks: PatchGateChecks), so the ones already applied stay inert.
        /// No method is patched by classes of both groups, so no two patches change places.
        /// Any other patch class is applied on its own: one a game update broke is left out and named (FailedPatches),
        /// and every other still applies. Until 1.4.0-rc1 the first failure threw out of the mod's start, so every patch
        /// class after it, the mixed-factions ones, the shared automation settings and the save and clock patches were all
        /// left out, and a co-op game could still start on what was left (the review of beta24, R8).
        /// </summary>
        private static void PatchAllIsolatingFactions(Harmony harmony)
        {
            const string factions = "BeaverBuddies.Factions";
            List<Type> classes = AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly).Where(t => t.HasHarmonyAttribute()).ToList();
            foreach (Type type in classes.Where(t => t.Namespace != factions))
            {
                try { harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception error)
                {
                    FailedPatches.Add(type.FullName);
                    LogError($"Could not apply {type.FullName} to this game version (a game update?): co-op is refused until " +
                        $"MultiColony is updated. {error}");
                }
            }
            try
            {
                foreach (Type type in classes.Where(t => t.Namespace == factions)) harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception error)
            {
                Factions.MixedFactions.Unavailable = error.GetBaseException().Message;
                LogError("[Factions] A game method that mixed factions changes is not what this version expects; mixed factions is off " +
                    "until the mod is updated: " + error);
            }
        }

        public static string GetWithDate(string message)
        {
            return $"[{System.DateTime.Now.ToString("HH-mm-ss.ff")}] {message}";
        }

        public static void Log(string message)
        {
            Remember(message);
            if (!Settings.VerboseLogging) return;
            logger.LogInfo(GetWithDate(message));
        }

        public static void LogWarning(string message)
        {
            Remember("warning: " + message);
            logger.LogWarning(GetWithDate(message));
        }

        public static void LogError(string message)
        {
            Remember("error: " + message);
            logger.LogError(GetWithDate(message));
        }

        // The diagnostics report keeps the last colony and desync lines.
        private static void Remember(string message)
        {
            if (message == null) return;
            if (message.Contains("[Colony]") || message.IndexOf("desync", System.StringComparison.OrdinalIgnoreCase) >= 0
                || message.Contains("Random state"))
                BeaverBuddies.Colonies.ColonyDiagnostics.Remember(GetWithDate(message));
        }

        public static void LogStackTrace()
        {
            logger.LogInfo(new StackTrace().ToString());
        }
    }
}
