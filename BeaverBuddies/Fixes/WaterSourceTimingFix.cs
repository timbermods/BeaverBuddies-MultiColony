using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Timberborn.WaterSourceSystem;
using UnityEngine;

namespace BeaverBuddies.Fixes
{
    // This gameplay modifier is evaluated by WaterSource.Tick, but the vanilla
    // fade uses render-frame duration. Replace only that clock read, preserving
    // the game's depth thresholds, hysteresis, fade speed and clamping.
    //
    // A game update that changes how often the method reads the frame clock no longer throws out of the mod's patching
    // (1.4.0-rc1, R8): a throw inside PatchAll stopped every patch after this one, the rest of the mod's desync fixes
    // included. The method is then left as the game has it, and Unavailable says why. Without the fix, water seeps (the
    // only water sources with this modifier) come back on each computer's own frame time and put the water out of step,
    // so a co-op game that has them is stopped at load with a message (CoopFixGuard). A game without seeps is unaffected.
    [HarmonyPatch(typeof(WaterDepthStrengthModifier), nameof(WaterDepthStrengthModifier.GetStrengthModifier))]
    public static class WaterSourceTimingFix
    {
        /// <summary>Why the fix is not applied to this game version, or null when it is.</summary>
        internal static string Unavailable { get; private set; }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var original = AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime));
            var replacement = AccessTools.Method(typeof(WaterSourceTimingFix), nameof(GetDeltaTime));
            int found = 0;
            foreach (var instruction in result)
            {
                if (instruction.Calls(original)) found++;
            }
            if (found != 1)
            {
                Unavailable = $"WaterDepthStrengthModifier.GetStrengthModifier reads the frame clock {found} times, where Timberborn 1.1.2.4 read it once";
                Plugin.LogError("[Fixes] Water seep timing is not corrected for co-op on this game version (" + Unavailable
                    + "): the method is left as the game has it, and a co-op game with water seeps is stopped at load until Timber Together is updated");
                return result;
            }
            Unavailable = null;
            foreach (var instruction in result)
            {
                if (instruction.Calls(original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
            }
            return result;
        }

        private static bool toldMissingBuffer;

        internal static float GetDeltaTime()
        {
            if (EventIO.IsNull) return GetFrameDeltaTime();
            var buffer = SingletonManager.GetSingleton<LateTickableBuffer>();
            if (buffer == null)
            {
                // Co-op services are bound with the map when a session is set (Plugin's ReplayConfigurator), so this does
                // not happen. If it ever did, the game's own clock is used: a throw here would be inside a tick.
                if (!toldMissingBuffer)
                {
                    toldMissingBuffer = true;
                    Plugin.LogError("Water-source tick service is not initialized: water seeps use the frame clock");
                }
                return GetFrameDeltaTime();
            }
            return buffer.TickIntervalInSeconds;
        }

        // Keep the Unity native call outside the multiplayer path.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetFrameDeltaTime() => Time.deltaTime;
    }
}
