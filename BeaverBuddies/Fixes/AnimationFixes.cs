//# define NO_SMOOTH_ANIMATION

using BeaverBuddies.IO;
using HarmonyLib;
using Timberborn.CharacterMovementSystem;
using Timberborn.EntitySystem;
using UnityEngine;

namespace BeaverBuddies.Fixes
{
#if !NO_SMOOTH_ANIMATION
    [ManualMethodOverwrite]
    /*
4/19/2025
private void Update(float deltaTime)
{
    _animatedPathFollower.Update(Time.time);
    if (!_animatedPathFollower.Stopped)
    {
        UpdateTransform(deltaTime);
        UpdateAnimationSpeed();
        UpdateGroupId();
    }

    NotifyAnimationUpdated();
    UpdateRotation();
}
     */
    [HarmonyPatch(typeof(MovementAnimator), nameof(MovementAnimator.Update), typeof(float))]
    public class AnimatedPathFollowerUpdatePathcer
    {
        static bool Prefix(MovementAnimator __instance, float deltaTime)
        {
            if (EventIO.IsNull) return true;
            var tickProgressService = SingletonManager.GetSingleton<TickProgressService>();
            if (tickProgressService == null) return true;

            //Vector3 position = Vector3.zero;

            // The patched Time.time and the tick length, as plain managed values: the patched
            // getter is a native detour back into managed code, which is expensive to call for
            // every animated character on every frame.
            float simulationTime = TimeTimePatcher.SimulationTime;
            float tickLength = TimeTimePatcher.TickLength;
            float time = simulationTime;
            EntityComponent entity = __instance.GetComponent<EntityComponent>();
            if (entity != null)
            {
                // For the movement animation, use interpolated time based on
                // how many buckets we've ticked (i.e. how close to the next
                // time update).
                time = tickProgressService.InterpolatedTime(entity, simulationTime, tickLength);

                //if (entity.EntityId.ToString() == "00355d1d-36fd-f115-9c90-6a54dda73a85")
                //{
                //    Plugin.Log($"{entity.EntityId} (${entity.GetComponent<Character>().FirstName}) :\n" +
                //        $"index: {tickProgressService.GetEntityBucketIndex(entity)}\n" +
                //        $"nextTick: {tickProgressService.TickableBucketService._nextBucketIndex}\n" +
                //        $"ticked: {tickProgressService.HasTicked(entity)}\n" +
                //        $"last: {tickProgressService.TimeAtLastTick(entity)}\n" +
                //        $"perc: {tickProgressService.PercentTicked(entity)}\n" +
                //        $"time: {Time.time} -> {time}");
                //    position = __instance._animatedPathFollower.CurrentPosition;
                //}
            }
            else
            {
                Plugin.LogWarning("missing entity component!");
            }

            // Use the interpolated time
            // The game's follower only searches forward from its cached corner.
            // Our interpolated clock can move backwards at a tick boundary, so
            // reselect the segment instead of extrapolating from a later corner.
            ResumeSearch(__instance._animatedPathFollower, time);
            __instance._animatedPathFollower.Update(time);

            Vector3 position = __instance._animatedPathFollower.CurrentPosition;
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                float.IsNaN(position.z) || float.IsInfinity(position.z))
            {
                // Do not pass an invalid visual position to swimming/water-map
                // listeners. Restore the simulation position, leaving its path
                // and the simulation itself unchanged.
                __instance._animatedPathFollower.CurrentPosition = __instance.Transform.position;
            }

            // Otherwise, update as usual
            if (!__instance._animatedPathFollower.Stopped)
            {
                __instance.UpdateTransform(deltaTime);
                __instance.UpdateAnimationSpeed();
                __instance.UpdateGroupId();
            }
            __instance.NotifyAnimationUpdated();
            __instance.UpdateRotation();

            //if (position != Vector3.zero)
            //{
            //    Plugin.Log($"pos: {position} -> {__instance._animatedPathFollower.CurrentPosition}");
            //    Vector3 dir = __instance._animatedPathFollower.CurrentPosition - position;
            //    Plugin.Log($"XDir: {Mathf.Sign(dir.x)}, ZDir: {Mathf.Sign(dir.z)}");
            //}

            // We've replaced the original method, so skip it
            return false;
        }

        /// <summary>
        /// Before the follower's Update(<paramref name="time"/>): its search for the next corner starts again from the
        /// first corner only when the clock went back past a corner it had passed. Otherwise it goes on from its cached
        /// corner, as in the game: a path's corner times never decrease (PathFollower.MoveAlongPath), so every corner
        /// before the cached one is still behind the clock and the search finds what a search from the first would.
        /// Until 1.4.0-rc1 every animated character searched from the first corner every frame (review D-S9); a tick's
        /// path holds a corner every 0.1 of a tile walked. Drawing only: the simulation's position is the tick's.
        /// </summary>
        internal static void ResumeSearch(AnimatedPathFollower follower, float time)
        {
            int next = follower._nextCornerIndex;
            if (next <= 0) return;
            var corners = follower._pathCorners;
            // Past the last corner the follower holds Count + 1.
            int passed = System.Math.Min(next, corners.Count) - 1;
            if (passed < 0 || corners[passed].Time > time) follower._nextCornerIndex = 0;
        }
    }
#endif
}
