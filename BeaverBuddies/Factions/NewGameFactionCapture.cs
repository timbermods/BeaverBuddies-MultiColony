using BeaverBuddies.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.FactionSystem;
using Timberborn.GameSceneLoading;
using Timberborn.NewGameConfigurationSystem;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// The main menu's side of Mixed factions for new games (D9): whether a new game made here would be mixed (the setting
    /// and Separate colonies are on, and every faction is unlocked on this computer's profile, the host's, D1), for the
    /// waiting room when it opens and for the solo New Game's Start. What it captures is plain static state: it has to
    /// outlive the menu scene and is taken once by the new game (MixedFactions.Decide).
    /// </summary>
    public class NewGameFactionCapture : RegisteredSingleton
    {
        private readonly FactionSpecService _factionSpecService;
        private readonly FactionUnlockingService _factionUnlockingService;

        private static bool pending;
        private static string pendingFaction;

        /// <summary>The display name of a faction that kept a new game from being mixed; shown once in that game.</summary>
        public static string NoticeLockedFaction { get; set; }

        public static NewGameFactionCapture Instance => SingletonManager.GetSingleton<NewGameFactionCapture>();

        public NewGameFactionCapture(FactionSpecService factionSpecService, FactionUnlockingService factionUnlockingService)
        {
            _factionSpecService = factionSpecService;
            _factionUnlockingService = factionUnlockingService;
        }

        /// <summary>Whether the host has asked for mixed factions in new games (whether or not it can have them).</summary>
        public static bool Requested => Settings.MixedFactionsForNewGames && Settings.SeparateColoniesForNewGames;

        /// <summary>
        /// A new game made now would be mixed. When it was asked for but a faction is still locked on this profile,
        /// <paramref name="lockedFaction"/> is that faction's display name.
        /// </summary>
        public bool MixedAvailable(out string lockedFaction)
        {
            lockedFaction = null;
            if (!Requested) return false;
            FactionSpec locked = Factions().FirstOrDefault(f => _factionUnlockingService.IsLocked(f));
            if (locked == null) return Factions().Count() > 1;
            lockedFaction = locked.DisplayName.Value;
            return false;
        }

        /// <summary>The factions a mixed game offers, in the game's order (every one, as all must be unlocked).</summary>
        public List<string> OfferedFactions() => Factions().Select(f => f.Id).ToList();

        public FactionSpec Spec(string id) => Factions().FirstOrDefault(f => f.Id == id);

        private IEnumerable<FactionSpec> Factions() => _factionSpecService.Factions.OrderBy(f => f.Order);

        /// <summary>The solo New Game's Start: whether the game about to be made is mixed.</summary>
        internal static void Capture(NewGameConfiguration configuration)
        {
            pending = false;
            pendingFaction = configuration?.FactionId;
            NewGameFactionCapture capture = Instance;
            if (capture == null || !Requested) return;
            pending = capture.MixedAvailable(out string locked);
            if (locked != null) NoticeLockedFaction = locked;
            Plugin.Log($"[Factions] New game as {pendingFaction}: mixed factions {(pending ? "on" : "off")}" +
                (locked != null ? $" ({locked} is locked on this computer)" : ""));
        }

        /// <summary>Taken once by the new game's scene: true only for the game this Start was for.</summary>
        internal static bool Take(string factionId) => pending && pendingFaction == factionId;

        internal static void Clear()
        {
            pending = false;
            pendingFaction = null;
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, MainMenuPanels: NewGameModePanel.StartNewGame
        _gameSceneLoader.StartNewGame(new NewGameConfiguration(_factionSpec.Id, _map.MapFileReference, gameMode, string.Empty));
     * The solo New Game's Start (a waiting room makes its world itself, LobbySession.Start, and says whether it is mixed).
     */
    [HarmonyPatch(typeof(GameSceneLoader), nameof(GameSceneLoader.StartNewGame))]
    static class NewGameFactionCapturePatcher
    {
        // Whitelisted in the gate check: it only records whether the next game will be mixed.
        static void Prefix(NewGameConfiguration newGameConfiguration)
        {
            try { NewGameFactionCapture.Capture(newGameConfiguration); }
            catch (Exception error) { Plugin.LogWarning("[Factions] Could not read the new game's factions: " + error.Message); }
        }
    }
}
