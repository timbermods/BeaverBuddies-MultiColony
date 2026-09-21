using BeaverBuddies.IO;
using Timberborn.BlockSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Marks the founding tool's preview invalid, with the reason, where a colony may not be founded (it would join
    /// another colony's roads), so the refusal shows before the click. Previews only, and never while events replay:
    /// the game also checks validity inside a replayed placement, on every computer, and the answer there must not
    /// depend on who is looking. The host's verdict at replay time still decides; this only saves a click. Ordinary
    /// building is allowed anywhere; the game's own check already refuses roads that would merge two districts.
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
            if (founding == null || !founding.FoundingToolActive) return true;
            ColonyVerdict verdict;
            try
            {
                verdict = founding.Judge(ColonySession.LocalSlot, blockObject.Placement, checkBlocks: false);
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
