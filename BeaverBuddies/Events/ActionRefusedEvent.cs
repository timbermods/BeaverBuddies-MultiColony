using BeaverBuddies.Colonies;

namespace BeaverBuddies.Events
{
    /// <summary>
    /// Sent by the host where it refused a guest's action (ColonyRulesService.AllowOnHost), so that guest learns at once
    /// and why, instead of waiting for an answer that never comes. Every computer plays it and nothing in the game
    /// changes: only the guest that sent the action shows a notice and clears its pending marker.
    /// </summary>
    class ActionRefusedEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        /// <summary>The refused action's own tag (its requestId).</summary>
        public string refusedRequestId;
        public ColonyRefusal refusal;

        public override void Replay(IReplayContext context)
        {
            Latency.PendingActions.Instance?.Refused(refusedRequestId, refusal);
        }

        public override string ToActionString()
        {
            return $"Telling a player their action {refusedRequestId} was refused: {refusal}";
        }
    }
}
