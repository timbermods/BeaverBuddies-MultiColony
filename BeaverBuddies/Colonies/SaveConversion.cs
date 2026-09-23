using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>The host's choice, in a hosted save's waiting room, to make a shared save a separate-colonies game at Start.</summary>
    public sealed class PendingConversion
    {
        public PendingConversion(bool separateScience)
        {
            SeparateScience = separateScience;
        }

        /// <summary>Each colony its own science and unlocks from Start (what was earned so far stays with the host's colony).</summary>
        public bool SeparateScience { get; }
    }

    /// <summary>
    /// A hosted shared save made separate colonies at Start (1.4.0-rc4): the waiting room's Separate colonies checkbox
    /// (LobbyHostPanel) leaves a <see cref="PendingConversion"/>, and once the host's game has loaded it says so as its
    /// first action (<see cref="ColonyConversionEvent"/>), which every computer plays at the same point. It is the split a
    /// guest makes from the game menu (SharedColonySplit), without a founding: every building and mark there is the
    /// host's colony's, and each guest is then offered to found their own.
    /// </summary>
    public class SaveConversion : RegisteredSingleton, IUpdatableSingleton
    {
        /// <summary>Set by the host's waiting room as it starts; taken by the host's game once (null: nothing to do).</summary>
        public static PendingConversion Pending { get; set; }

        // Sent, and not yet played on the host.
        private static bool sent;

        /// <summary>
        /// Host: the conversion is still to come (not sent yet, or sent and not yet played). A guest's change is refused
        /// until then: played first, in the still-shared game, it would become the host's colony's (1.4.0-rc5 review, B3).
        /// </summary>
        public static bool HostAwaitsConversion => Pending != null || sent;

        /// <summary>The conversion was played on this computer.</summary>
        internal static void Played() => sent = false;

        private readonly ColonyFoundingService _colonyFoundingService;

        public SaveConversion(ColonyFoundingService colonyFoundingService)
        {
            _colonyFoundingService = colonyFoundingService;
            sent = false;
        }

        public void UpdateSingleton()
        {
            PendingConversion pending = Pending;
            if (pending == null) return;
            // Only the host that pressed Start has one; anything else (a guest, a game without a session) drops it.
            if (!(EventIO.Get() is ServerEventIO) || ColonyModeService.IsSeparateColonies)
            {
                Pending = null;
                return;
            }
            // Its first action: once the session plays actions, not while the game is still loading.
            if (!ReplayService.IsLoaded) return;
            Pending = null;
            ColonyStartingSettings start = _colonyFoundingService.HostStartingSettings();
            Plugin.Log($"[Colony] Making this hosted save separate colonies (separate science {(pending.SeparateScience ? "on" : "off")})");
            bool notRecorded = ReplayEvent.DoPrefix(() => new ColonyConversionEvent { separateScience = pending.SeparateScience, startingSettings = start });
            if (notRecorded) Plugin.LogWarning("[Colony] The hosted save could not be made separate colonies: the session was not ready");
            else sent = true;
        }
    }

    /// <summary>The host makes this shared game a separate-colonies game, for good (see <see cref="SaveConversion"/>).</summary>
    [Serializable]
    public class ColonyConversionEvent : ReplayEvent
    {
        public bool separateScience;
        /// <summary>What a colony founded later starts with: the host's, so every computer founds the same (see FoundColonyEvent).</summary>
        public ColonyStartingSettings startingSettings;

        // Only the host sends it (ColonyRulesService); it changes the whole game.
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context)
        {
            SaveConversion.Played();
            ColonyModeService mode = ColonyModeService.Instance;
            if (mode == null || mode.Enabled) return;
            ColonyFoundingService founding = SingletonManager.GetSingleton<ColonyFoundingService>();
            mode.Enable(startingSettings ?? founding?.HostStartingSettings(), "the host hosted this save as separate colonies", separateScience,
                newGame: false);
            // Display, on each computer: the host is told the colony so far is theirs; a guest is offered to found theirs.
            try
            {
                if (EventIO.Get() is ServerEventIO)
                    SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(RegisteredLocalizationService.T("BeaverBuddies.Colony.Converted.Host"), warning: false);
                else founding?.OfferFounding();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not tell this player the game is now separate colonies: " + error.Message);
            }
        }

        public override string ToActionString() => $"Making this game separate colonies (separate science {(separateScience ? "on" : "off")})";
    }
}
