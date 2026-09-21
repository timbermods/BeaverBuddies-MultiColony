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
            if (!blockObject.Positioned) return true;
            var founding = SingletonManager.GetSingleton<ColonyFoundingService>();
            bool foundingTool = founding != null && founding.FoundingToolActive;
            if (!ColonyModeService.IsSeparateColonies && !foundingTool) return true;
            ColonyVerdict verdict;
            try
            {
                verdict = Judge(blockObject, founding, foundingTool);
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

        private static ColonyVerdict Judge(BlockObject blockObject, ColonyFoundingService founding, bool foundingTool)
        {
            int colony = ColonySession.LocalColony;
            var world = new ColonyPreviewWorld(blockObject);
            if (foundingTool)
                return founding.Judge(colony, blockObject.Placement, world.Footprint(null), checkBlocks: false);
            if (ColonyModeService.FoundingPending)
            {
                // In a game created to await colony 2, its player's only placement is the founding one.
                if (colony == 1) return ColonyVerdict.Allow;
                return ColonyVerdict.Refuse(ColonyRefusal.NotFounded, "found your colony first");
            }
            return ColonyRules.Judge(ColonyScope.Place(new ColonyPlacement()), colony, ColonyModeService.ActiveTerritory,
                world, rewrite: false);
        }
    }
}
