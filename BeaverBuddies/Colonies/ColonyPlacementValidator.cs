using BeaverBuddies.IO;
using System.Linq;
using Timberborn.BlockSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Marks a building's preview invalid, with the reason, where the colony rules would refuse it: on another colony's
    /// land or roads, or (the founding tool) where a colony may not be founded. It checks the same blocks and doorstep
    /// the host does. So the refusal shows before the click.
    /// Previews only, and never while events replay: the game also checks validity inside a replayed placement, on
    /// every computer, and the answer there must not depend on who is looking. The host's verdict still decides; this
    /// only saves a click.
    /// </summary>
    public class ColonyPlacementValidator : IBlockObjectValidator
    {
        private static readonly ColonyProfiler.Spot Previews = ColonyProfiler.Declare("Placement previews");

        private bool loggedError;

        public bool IsValid(BlockObject blockObject, out string errorMessage)
        {
            errorMessage = null;
            if (!blockObject.IsPreview || ReplayService.IsReplayingEvents || EventIO.IsNull) return true;
            if (!blockObject.Positioned) return true;
            // Asked every frame for every block of what the player is placing (a dragged path is many). The founding
            // tool has its own check; everything else is only a question with separate colonies on, and nothing is
            // looked up or timed before that is known.
            ColonyFoundingService founding = SingletonManager.GetSingleton<ColonyFoundingService>();
            bool foundingTool = founding != null && founding.FoundingToolActive;
            if (!foundingTool && !ColonyModeService.IsSeparateColonies) return true;
            ColonyVerdict verdict;
            long started = ColonyProfiler.Start();
            try
            {
                verdict = foundingTool
                    ? founding.Judge(ColonySession.LocalSlot, blockObject.Placement, checkBlocks: false)
                    : Judge(blockObject);
            }
            catch (System.Exception error)
            {
                // Runs every frame while placing; a failure must not stop placement. The host still judges the click.
                if (!loggedError) Plugin.LogError("[Colony] Could not check a placement preview: " + error);
                loggedError = true;
                return true;
            }
            finally
            {
                ColonyProfiler.Stop(Previews, started);
            }
            if (verdict.IsAllowed) return true;
            errorMessage = ColonyRulesService.RefusalMessage(verdict.Refusal);
            return false;
        }

        private static ColonyVerdict Judge(BlockObject blockObject)
        {
            int slot = ColonySession.LocalSlot;
            var world = SingletonManager.GetSingleton<ColonyRulesService>()?.World;
            if (slot < 0 || world == null) return ColonyVerdict.Allow;
            Vector3Int? doorstep = blockObject.HasEntrance ? blockObject.PositionedEntrance.DoorstepCoordinates : (Vector3Int?)null;
            ColonyRefusal refusal = world.TilesConflict(slot, blockObject.PositionedBlocks.GetAllCoordinates().ToList(), doorstep,
                crossing: TradingPosts.IsTradingPostBuilding(blockObject), out string detail);
            return refusal == ColonyRefusal.None ? ColonyVerdict.Allow : ColonyVerdict.Refuse(refusal, detail);
        }
    }
}
