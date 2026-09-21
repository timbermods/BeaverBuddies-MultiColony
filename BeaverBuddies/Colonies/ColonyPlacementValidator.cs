using BeaverBuddies.IO;
using Timberborn.BlockSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Marks a placement preview invalid, with the reason, when the local player may not build there, so a refused
    /// placement looks refused before the click. Previews only, and never while events replay: the game also checks
    /// validity inside a replayed placement, on every computer, and the answer there must not depend on who is looking.
    /// The host's verdict at replay time still decides; this only saves a click.
    /// </summary>
    public class ColonyPlacementValidator : IBlockObjectValidator
    {
        private bool loggedError;

        public bool IsValid(BlockObject blockObject, out string errorMessage)
        {
            errorMessage = null;
            if (!blockObject.IsPreview || ReplayService.IsReplayingEvents || EventIO.IsNull) return true;
            ColonyTerritory territory = ColonyModeService.ActiveTerritory;
            if (territory == null || !blockObject.Positioned) return true;
            ColonyVerdict verdict;
            try
            {
                var world = new ColonyPreviewWorld(blockObject);
                verdict = ColonyRules.Judge(ColonyScope.Place(new ColonyPlacement()), ColonySession.LocalColony,
                    territory, world, rewrite: false);
            }
            catch (System.Exception error)
            {
                // Runs every frame while placing; a failure must not stop placement. The host still judges the click.
                if (!loggedError) Plugin.LogError("[Colony] Could not check a placement preview: " + error);
                loggedError = true;
                return true;
            }
            if (verdict.IsAllowed) return true;
            errorMessage = ColonyRulesService.RefusalMessage(verdict.Refusal);
            return false;
        }
    }
}
