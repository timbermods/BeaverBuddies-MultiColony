using BeaverBuddies.Colonies;
using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.AutomationBuildingsUI;
using Timberborn.AutomationUI;
using Timberborn.BaseComponentSystem;
using Timberborn.FireworkSystem;
using Timberborn.HttpApiSystem;
using Timberborn.PowerGeneration;
using Timberborn.PowerManagement;
using Timberborn.WaterBuildings;
using Timberborn.WaterSourceSystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Events
{

    public class AutomationEvent : ReplayEvent
    {
        // The building being set, and any building it is wired to (a relay's or memory cell's input arrives here as
        // an entity id among the arguments), must be the actor's. Other text arguments name no entity and count for
        // nobody.
        public override ColonyScope GetColonyScope() =>
            ColonyScope.Entities(new[] { entityID }.Concat(arguments?.OfType<string>() ?? Enumerable.Empty<string>()).ToArray());

        public string entityID;
        public string methodKey;
        public object[] arguments;

        private static readonly Dictionary<string, MethodInfo> methodCache = new();

        private static string MakeKey(string className, string methodName) => $"{className}.{methodName}";

        private static MethodInfo getComponentMethodInfo;

        public override void Replay(IReplayContext context)
        {
            if (!TryGetMethodInfo(methodKey, out var methodInfo))
            {
                Plugin.LogError($"No MethodInfo for: {methodKey}. Cannot replay this event.");
                return;
            }
            if (arguments == null || methodInfo.GetParameters().Length != arguments.Length)
            {
                Plugin.LogError($"Argument count mismatch for {methodKey}. Expected {methodInfo.GetParameters().Length}, got {arguments?.Length}. Cannot replay this event.");
                return;
            }
            object componentObj = GetComponentForType(methodInfo.DeclaringType, entityID, context);
            // The building was demolished (or never had this part) between the click and this tick: skipped, the same on
            // every computer. Calling the setter on nothing threw, and a throw here ends the session for everyone.
            if (componentObj == null)
            {
                Plugin.LogWarning($"Skipped {methodKey}: entity {entityID} is gone");
                return;
            }
            object[] deserialized = Deserialize(arguments, context, methodInfo);
            methodInfo.Invoke(componentObj, deserialized);
        }

        public override string ToActionString()
        {
            return $"Calling {methodKey} for Entity {entityID}";
        }

        static bool TryGetMethodInfo(string methodKey, out MethodInfo info)
        {
            if (methodCache.TryGetValue(methodKey, out var cachedInfo))
            {
                info = cachedInfo;
                return true;
            }
            info = default;
            return false;
        }

        private static MethodInfo GetMethodInfoForGetComponent()
        {
            if (getComponentMethodInfo != null)
            {
                return getComponentMethodInfo;
            }
            var methodInfo = typeof(ReplayEvent).GetMethod(nameof(GetComponent),
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (methodInfo == null || methodInfo.GetParameters().Length != 2)
            {
                throw new Exception($"GetComponent method not found or has incorrect parameters: {methodInfo?.GetParameters().Length}.");
            }
            getComponentMethodInfo = methodInfo;
            return methodInfo;
        }

        private static object GetComponentForType(Type type, string entityID, IReplayContext context)
        {
            MethodInfo methodInfo = GetMethodInfoForGetComponent();
            var genericMethod = methodInfo.MakeGenericMethod(type);
            return genericMethod.Invoke(null, new object[] { context, entityID });
        }

        public static void ApplyAutomationPatches(Harmony harmony)
        {
            // Important: We can only override methods like this if they:
            // 1) Are only ever called from the UI (not from step logic)
            // 2) Are in a BaseComponent (so we can get the EntityID)
            // The only exception is Load and Duplicate, but in both cases
            // other logic prevents events from forming, so it should be safe.
            (Type, string)[] methodsToPatchInfo =
            [
                (typeof(Chronometer), nameof(Chronometer.SetStartTime)),
                (typeof(Chronometer), nameof(Chronometer.SetEndTime)),
                (typeof(Chronometer), nameof(Chronometer.SetMode)),
                (typeof(ContaminationSensor), nameof(ContaminationSensor.SetMode)),
                (typeof(ContaminationSensor), nameof(ContaminationSensor.SetThreshold)),
                (typeof(DepthSensor), nameof(DepthSensor.SetMode)),
                (typeof(DepthSensor), nameof(DepthSensor.SetThreshold)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetContinuous)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetFireworkId)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetFlightDistance)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetHeading)),
                (typeof(FireworkLauncher), nameof(FireworkLauncher.SetPitch)),
                (typeof(FlowSensor), nameof(FlowSensor.SetMode)),
                (typeof(FlowSensor), nameof(FlowSensor.SetThreshold)),
                (typeof(Gate), nameof(Gate.SetOpeningMode)),
                (typeof(Indicator), nameof(Indicator.SetColorReplicationEnabled)),
                (typeof(Indicator), nameof(Indicator.SetJournalEntryEnabled)),
                (typeof(Indicator), nameof(Indicator.SetPinnedMode)),
                (typeof(Indicator), nameof(Indicator.SetWarningEnabled)),
                // Note: Lever calls these methods during load, but this should be
                // ok because we don't record events until the game has started
                (typeof(Lever), nameof(Lever.SetPinned)),
                (typeof(Lever), nameof(Lever.SetSpringReturn)),
                // Note: This won't be a great UX, since we really have to make
                // this an event, which will make the UI laggy, but I think that's
                // unavoidable.
                (typeof(Lever), nameof(Lever.SwitchState)),
                (typeof(Memory), nameof(Memory.SetMode)),
                (typeof(Memory), nameof(Memory.SetInputA)),
                (typeof(Memory), nameof(Memory.SetInputB)),
                (typeof(Memory), nameof(Memory.SetResetInput)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetComparisonMode)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetCountBeavers)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetCountBots)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetGlobalMode)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetMode)),
                (typeof(PopulationCounter), nameof(PopulationCounter.SetThreshold)),
                (typeof(PowerMeter), nameof(PowerMeter.SetComparisonMode)),
                (typeof(PowerMeter), nameof(PowerMeter.SetIntThreshold)),
                (typeof(PowerMeter), nameof(PowerMeter.SetMode)),
                (typeof(PowerMeter), nameof(PowerMeter.SetPercentThreshold)),
                (typeof(Relay), nameof(Relay.SetInput)),
                (typeof(Relay), nameof(Relay.IncreaseInputs)),
                (typeof(Relay), nameof(Relay.RemoveInput)),
                (typeof(Relay), nameof(Relay.SetMode)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetComparisonMode)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetFillRateThreshold)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetGoodId)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetIncludeInputs)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetMode)),
                (typeof(ResourceCounter), nameof(ResourceCounter.SetThreshold)),
                (typeof(ScienceCounter), nameof(ScienceCounter.SetMode)),
                (typeof(ScienceCounter), nameof(ScienceCounter.SetThreshold)),
                (typeof(Speaker), nameof(Speaker.SetPlaybackMode)),
                (typeof(Speaker), nameof(Speaker.SetSoundId)),
                (typeof(Speaker), nameof(Speaker.SetSpatialMode)),
                // Note: Timer calls these methods during load (see above)
                (typeof(Timer), nameof(Timer.SetInput)),
                (typeof(Timer), nameof(Timer.SetMode)),
                (typeof(Timer), nameof(Timer.SetResetInput)),
                (typeof(WeatherStation), nameof(WeatherStation.SetEarlyActivationHours)),
                (typeof(WeatherStation), nameof(WeatherStation.SetMode)),
                // The HTTP API (HTTP Lever, HTTP Adapter) needs nothing here: each player's computer runs its own
                // listener, and a request to switch an HTTP lever reaches Lever.SwitchState above in a frame
                // (HttpApiIntermediary.UpdateSingleton), so it is that player's action, shared and judged by colony like
                // a click. A request to colour one is not shared: see HttpLeverSetColorCoopPatcher below.

                // Some building also have special automation UIs that exist when automated
                (typeof(Floodgate), nameof(Floodgate.SetAutomationHeightAndSynchronize)),
                (typeof(FillValve), nameof(FillValve.SetAutomationTargetHeightAndSynchronize)),
                (typeof(FillValve), nameof(FillValve.SetAutomationTargetHeightEnabledAndSynchronize)),

                // TODO: These are not automation-related events, but they are "automated" events
                // so I need to refactor this class to separate these two ideas
                (typeof(FillValve), nameof(FillValve.SetTargetHeightAndSynchronize)),
                (typeof(FillValve), nameof(FillValve.SetTargetHeightEnabledAndSynchronize)),
                (typeof(FillValve), nameof(FillValve.ToggleSynchronization)),
                (typeof(ThrottlingValve), nameof(ThrottlingValve.SetOutflowLimitAndSynchronize)),
                // The outflow slider sets this first (off at its top end, on below it), then the limit: until 1.4.0-rc1
                // only the limit was shared, so the valve limited the flow on the dragging player's computer alone (A2).
                (typeof(ThrottlingValve), nameof(ThrottlingValve.SetOutflowLimitEnabledAndSynchronize)),
                (typeof(ThrottlingValve), nameof(ThrottlingValve.SetReactionSpeedAndSynchronize)),
                (typeof(ThrottlingValve), nameof(ThrottlingValve.SetAutomationOutflowLimitAndSynchronize)),
                (typeof(ThrottlingValve), nameof(ThrottlingValve.SetAutomationOutflowLimitEnabledAndSynchronize)),
                (typeof(ThrottlingValve), nameof(ThrottlingValve.ToggleSynchronization)),
                (typeof(WaterSourceRegulator), nameof(WaterSourceRegulator.Open)),
                (typeof(WaterSourceRegulator), nameof(WaterSourceRegulator.Close)),
                (typeof(WaterSourceRegulator), nameof(WaterSourceRegulator.Automate)),
                (typeof(WaterInputPipeCoordinates), nameof(WaterInputPipeCoordinates.SetDepthLimit)),
                (typeof(WaterInputPipeCoordinates), nameof(WaterInputPipeCoordinates.DisableDepthLimit)),
                (typeof(Clutch), nameof(Clutch.SetMode)),
                // A water mover's flow rate: the slider on every pump (Timberborn 1.1, WaterMoverFragment). The pump
                // moves that much water every tick; until 1.4.0-rc1 it did so on the dragging player's computer alone (A2).
                (typeof(WaterMover), nameof(WaterMover.SetFlowRate)),
                // The dev power generator is placed with dev mode, but once it stands anyone can drag its strength or
                // flip it, dev mode off (so without dev mode's co-op warning). Both change the power network (A2).
                (typeof(AdjustableStrengthPowerGenerator), "set_" + nameof(AdjustableStrengthPowerGenerator.GeneratorStrength)),
                (typeof(AdjustableStrengthPowerGenerator), nameof(AdjustableStrengthPowerGenerator.FlipRotation)),

            ];
            // A game update that renames or removes one of these no longer throws out of the mod's start (which skipped
            // the patches installed after this one). The action is then not shared, so co-op is refused while any is
            // missing (CoopFixGuard, R8).
            foreach (var (type, name) in methodsToPatchInfo)
            {
                MethodInfo method = null;
                try
                {
                    method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (method != null) OverrideMethod(harmony, method);
                }
                catch (Exception error)
                {
                    Plugin.LogError($"Could not patch {type.Name}.{name}: {error}");
                    method = null;
                }
                if (method == null)
                {
                    Plugin.LogError($"This game version has no {type.Name}.{name} to share: co-op is refused until MultiColony is updated");
                    MissingRecorders.Add($"{type.Name}.{name}");
                }
            }
        }

        /// <summary>
        /// Game methods of the list above this game version does not have (a game update renamed or removed them): the
        /// actions they stand for would change one computer only. Empty normally.
        /// </summary>
        internal static readonly List<string> MissingRecorders = new List<string>();

        private static void OverrideMethod(Harmony harmony, MethodInfo info)
        {
            string key = MakeKey(info.DeclaringType.FullName, info.Name);
            methodCache[key] = info;

            // Prepare the Harmony Prefix
            // We point Harmony to our UniversalPrefix method below
            var prefix = typeof(AutomationEvent).GetMethod(nameof(UniversalPrefix), BindingFlags.NonPublic | BindingFlags.Static);

            // Apply the patch
            harmony.Patch(info, prefix: new HarmonyMethod(prefix));
        }

        // This is the method Harmony actually calls. It records, so it runs first (see ReplayEvent.DoPrefix);
        // new HarmonyMethod(prefix) above takes the priority from this attribute.
        [HarmonyPriority(Priority.First)]
        private static bool UniversalPrefix(BaseComponent __instance, MethodBase __originalMethod, object[] __args)
        {
            // Use the same key logic to identify which method was triggered
            string key = MakeKey(__originalMethod.DeclaringType.FullName, __originalMethod.Name);

            // Call the original logic to create and record the event
            return DoPrefix(__instance, key, __args);
        }

        // It would be lovely to have this in a dict-like lookup, but unfortunately
        // since we can only get the type at runtime, generics won't work. So I could do
        // make everything take and return an object, but that's a pain, so I'll just do an if/else.
        // Right now, the if/else is easier, but if it becomes cumbersome, I can easily make this
        // into non-static methods and use an interface.
        private static object[] Serialize(object[] arguments)
        {
            object[] argsCopy = new object[arguments.Length];
            Array.Copy(arguments, argsCopy, arguments.Length);
            arguments = argsCopy;
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                if (arg == null)
                    continue;
                if (arg is BaseComponent)
                {
                    arguments[i] = GetEntityID((BaseComponent)arg);
                    if (arguments[i] == null)
                    {
                        Plugin.LogWarning($"Failed to serialize argument of type {arg.GetType().Name} with value {arg}. This may cause issues during replay.");
                    }
                }
            }
            return arguments;
        }

        private object[] Deserialize(object[] arguments, IReplayContext context, MethodInfo methodInfo)
        {
            object[] argsCopy = new object[arguments.Length];
            Array.Copy(arguments, argsCopy, arguments.Length);
            arguments = argsCopy;
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                if (arg == null)
                    continue;
                if (i >= methodInfo.GetParameters().Length)
                {
                    Plugin.LogError($"Argument index {i} is out of range for method {methodInfo.Name}. Cannot deserialize this argument.");
                    continue;
                }
                Type argType = methodInfo.GetParameters()[i].ParameterType;

                if (typeof(BaseComponent).IsAssignableFrom(argType) && arg is string)
                {
                    arguments[i] = GetComponentForType(argType, (string)arg, context);
                }

                if (argType.IsEnum && (arg is long || arg is int))
                {
                    arguments[i] = Enum.ToObject(argType, arg);
                }

                // JSON uses doubles only, so downcase to float
                if (argType == typeof(float) && arg is double)
                {
                    arguments[i] = (float)(double)arg;
                }

                if (argType == typeof(int) && arg is long)
                {
                    arguments[i] = (int)(long)arg;
                }
            }
            return arguments;
        }

        private static bool DoPrefix(BaseComponent entity, string methodKey, object[] arguments)
        {
            return DoEntityPrefix(entity, entityID =>
            {
                //Plugin.Log($"Serializing args for: {methodKey}: {arguments.Length}");
                //Plugin.LogStackTrace();
                return new AutomationEvent
                {
                    entityID = entityID,
                    methodKey = methodKey,
                    arguments = Serialize(arguments)
                };
            });
        }
    }

    public class SetAutomatableInputEvent : ReplayEvent
    {
        // Both the building being wired and the sensor it listens to must be yours: another colony could otherwise
        // change what its sensor says and so run your building.
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(automatableID, inputID);

        public string automatableID;
        public string inputID;

        public override void Replay(IReplayContext context)
        {
            Automatable automatable = GetComponent<Automatable>(context, automatableID);
            if (automatable == null) return;
            Automator automator = inputID == null ? null : GetComponent<Automator>(context, inputID);
            automatable.SetInput(automator);
        }

        public override string ToActionString()
        {
            return $"Setting Automatable {automatableID} input to {inputID}";
        }
    }

    [HarmonyPatch(typeof(AutomatableFragment), nameof(AutomatableFragment.SetInput))]
    static class AutomatableFragmentSetInputPatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(AutomatableFragment __instance, Automator automator)
        {
            return ReplayEvent.DoEntityPrefix(__instance._automatable, entityID =>
            {
                string automatorID = ReplayEvent.GetEntityID(automator);
                return new SetAutomatableInputEvent
                {
                    automatableID = entityID,
                    inputID = automatorID,
                };
            });
        }
    }

    public enum TimerIntervalInput
    {
        A,
        B
    }

    public class SetTimerIntervalEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(entityID);

        public string entityID;
        public TimerIntervalInput input;
        public float time;
        public IntervalType intervalType;

        public override void Replay(IReplayContext context)
        {
            var timer = GetComponent<Timer>(context, entityID);
            if (timer == null) return;

            TimerInterval interval;
            if (input == TimerIntervalInput.A)
            {
                interval = timer.TimerIntervalA;
            }
            else
            {
                interval = timer.TimerIntervalB;
            }

            switch (intervalType)
            {
                case IntervalType.Ticks:
                    interval.SetTicks((int)Math.Round(time));
                    break;
                case IntervalType.Hours:
                    interval.SetHours(time);
                    break;
                case IntervalType.Days:
                    interval.SetDays(time);
                    break;
            }
        }
        public override string ToActionString()
        {
            return $"Setting Timer interval {input} to {intervalType}={time} for Entity {entityID}";
        }
    }

    // Because TimerIntervalElements don't have access to the Timer they're editing, we need to store it.
    [HarmonyPatch(typeof(TimerFragment), nameof(TimerFragment.ShowFragment))]
    static class TimerFragmentShowFragmentPatch
    {
        static void Prefix(BaseComponent entity)
        {
            TimerIntervalElementSetTimeIntervalPatch.CurrentEditingTimer = entity.GetComponent<Timer>();
        }
    }

    [HarmonyPatch(typeof(TimerFragment), nameof(TimerFragment.ClearFragment))]
    static class TimerFragmentClearFragmentPatch
    {
        static void Prefix()
        {
            TimerIntervalElementSetTimeIntervalPatch.CurrentEditingTimer = null;
        }
    }

    // There's no easy way to capture a TimerInterval edit, so we capture it manually.
    [HarmonyPatch(typeof(TimerIntervalElement), nameof(TimerIntervalElement.SetTimeInterval))]
    static class TimerIntervalElementSetTimeIntervalPatch
    {
        public static Timer CurrentEditingTimer { get; set; }

        [HarmonyPriority(Priority.First)]
        static bool Prefix(TimerIntervalElement __instance, float time, IntervalType intervalType)
        {
            Timer timer = CurrentEditingTimer;
            // Also a timer deleted while its panel was open (its entity id can no longer be read).
            if (!timer) return true;
            TimerIntervalInput input = timer.TimerIntervalA == __instance._timerInterval ? TimerIntervalInput.A : TimerIntervalInput.B;
            return ReplayEvent.DoEntityPrefix(timer, entityID =>
            {
                return new SetTimerIntervalEvent
                {
                    entityID = entityID,
                    input = input,
                    time = time,
                    intervalType = intervalType,
                };
            });
        }
    }

    public class ResetTransmitterEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(entityID);

        public string entityID;
        public bool resetAll;

        // As the game's panel does (Timberborn 1.1.2.4, SequentialTransmitterResetFragment): Reset resets this
        // transmitter (OnReset → ISequentialTransmitter.Reset), Reset all its whole partition (OnResetAll →
        // AutomationResetter.ResetPartition). Until 1.4.0-rc1 the replay had the two the other way round.
        public override void Replay(IReplayContext context)
        {
            if (resetAll)
            {
                Automator automator = GetComponent<Automator>(context, entityID);
                if (automator == null) return;
                context.GetSingleton<AutomationResetter>().ResetPartition(automator);
            }
            else
            {
                ISequentialTransmitter transmitter = GetComponent<ISequentialTransmitter>(context, entityID);
                if (transmitter == null) return;
                transmitter.Reset();
            }
        }

        public override string ToActionString()
        {
            return $"Resetting {(resetAll ? "partition" : "transmitter")} for Entity {entityID}";
        }
    }

    [HarmonyPatch(typeof(SequentialTransmitterResetFragment), nameof(SequentialTransmitterResetFragment.OnReset))]
    static class SequentialTransmitterResetFragmentOnResetPatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(SequentialTransmitterResetFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._automator, entityID =>
            {
                return new ResetTransmitterEvent
                {
                    entityID = entityID,
                    resetAll = false,
                };
            });
        }
    }

    [HarmonyPatch(typeof(SequentialTransmitterResetFragment), nameof(SequentialTransmitterResetFragment.OnResetAll))]
    static class SequentialTransmitterResetFragmentOnResetAllPatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(SequentialTransmitterResetFragment __instance)
        {
            return ReplayEvent.DoEntityPrefix(__instance._automator, entityID =>
            {
                return new ResetTransmitterEvent
                {
                    entityID = entityID,
                    resetAll = true,
                };
            });
        }
    }

    public class WeatherStationSetActivateEarlyEvent : ReplayEvent
    {
        public override ColonyScope GetColonyScope() => ColonyScope.Entities(entityID);

        public string entityID;
        public bool activateEarly;

        public override void Replay(IReplayContext context)
        {
            WeatherStation station = GetComponent<WeatherStation>(context, entityID);
            if (station == null) return;
            station.SetEarlyActivationEnabled(activateEarly);
        }

        public override string ToActionString()
        {
            return $"Setting WeatherStation {entityID} to activate early: {activateEarly}";
        }
    }

    /*
     * For this method we have to directly capture the UI event, since SetEarlyActivationEnabled gets called
     * during the UI component's update method, which resets the value before the server can play the event.
     */
    [HarmonyPatch(typeof(WeatherStationFragment), nameof(WeatherStationFragment.OnEarlyActivationToggleChanged))]
    static class WeatherStationFragmentOnEarlyActivationToggleChangedPatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(WeatherStationFragment __instance, ChangeEvent<bool> evt)
        {
            if (evt.newValue == __instance._weatherStation.EarlyActivationEnabled)
            {
                // If there's no change, don't capture the event. We may be waiting on the event
                // to propagte.
                return true;
            }

            return ReplayEvent.DoEntityPrefix(__instance._weatherStation, entityID =>
            {
                // Revert the toggle for a cleaner UI experience, since we're not update the state yet,
                // and the UpdateFragment method will just toggle it back.
                __instance._earlyActivationToggle.SetValueWithoutNotify(!evt.newValue);
                return new WeatherStationSetActivateEarlyEvent
                {
                    entityID = entityID,
                    activateEarly = evt.newValue,
                };
            });
        }
    }

    [HarmonyPatch(typeof(RelayFragment), nameof(RelayFragment.RemoveRow))]
    [ManualMethodOverwrite]
    /*
    8/15/2026
    private void RemoveRow(int index)
	{
		_relay.RemoveInput(index);
		for (int i = index; i < _visibleMultipleInputs; i++)
		{
			_inputSelectors[i].UpdateSelectedValue();
		}
		_visibleMultipleInputs--;
	}
     */
    static class RelayFragmentRemoveRowPatch
    {
        static bool Prefix(RelayFragment __instance, int index)
        {
            __instance._relay.RemoveInput(index);
            // Don't update the UI (wait until the change actually happens)
            // We move that code below.
            return false;
        }
    }

    [HarmonyPatch(typeof(RelayFragment), nameof(RelayFragment.UpdateFragment))]
    [ManualMethodOverwrite]
    /* See above */
    static class RelayFragmentUpdateFragmentPatch
    {
        static void Postfix(RelayFragment __instance)
        {
            if (__instance._relay == null) return;
            if (__instance._visibleMultipleInputs != __instance._relay.Inputs.Count)
            {
                __instance._visibleMultipleInputs = __instance._relay.Inputs.Count;
                // Quick clicks on "add input" before the first is played can give a relay more inputs than the panel's
                // eight rows (the button counts the relay's inputs, which only grow when the click is played): the rows
                // past the eighth are not drawn, instead of throwing every frame (1.4.0-rc1, H1).
                int rows = Math.Min(__instance._visibleMultipleInputs, __instance._inputSelectors.Count);
                for (int i = 0; i < rows; i++)
                {
                    __instance._inputSelectors[i].UpdateSelectedValue();
                }
                __instance.UpdateMultipleInputs();
            }
        }
    }

    /// <summary>
    /// The HTTP API's colour request (/api/color/{lever}/{rrggbb}) sets an HTTP lever's light, which is saved with the
    /// lever. Each player's computer runs its own listener, so the request would colour the lever on that computer
    /// alone, and the next rehost would keep only the host's colour. Nothing the simulation reads depends on it (only
    /// the light and the indicators that copy its colour), so in a co-op game it is dropped rather than shared
    /// (1.4.0-rc1, A3, the review's default). Switching an HTTP lever on or off is shared (Lever.SwitchState).
    /// Single player is unchanged.
    /// </summary>
    [ManualMethodOverwrite]
    /*
     * 2026-09-23 (Timberborn 1.1.2.4, HttpLever.SetColor)
        _customizableIlluminator.SetCustomColor(color);
        _customizableIlluminator.SetIsCustomized(value: true);
     */
    [HarmonyPatch(typeof(HttpLever), nameof(HttpLever.SetColor))]
    static class HttpLeverSetColorCoopPatcher
    {
        private static bool told;

        [HarmonyPriority(Priority.Last)]
        static bool Prefix()
        {
            if (EventIO.IsNull) return true;
            if (!told)
            {
                told = true;
                Plugin.LogWarning("An HTTP API request to colour an HTTP lever was ignored: colours set through the HTTP API are not shared in co-op");
            }
            return false;
        }
    }
}
