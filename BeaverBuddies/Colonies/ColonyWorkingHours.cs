using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.AutomationBuildings;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using Timberborn.TimeSystemUI;
using Timberborn.WorkSystem;
using Timberborn.WorkSystemUI;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Working hours per colony. In a separate-colonies game a player's working-hours buttons set their own colony's
    /// hours (saved here), and only that colony's beavers and workshops follow them. The game's own setting is left
    /// as it was, the same on every computer, and serves as the hours of a colony that never chose its own.
    /// </summary>
    public class ColonyWorkingHours : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton, IUpdatableSingleton
    {
        private static readonly SingletonKey HoursKey = new SingletonKey("BeaverBuddies.ColonyWorkingHours");
        private static readonly ListKey<string> EntriesKey = new ListKey<string>("Hours");

        private readonly ISingletonLoader _singletonLoader;
        private readonly WorkingHoursManager _workingHoursManager;
        private readonly IDayNightCycle _dayNightCycle;
        private readonly WorkingHoursPanel _workingHoursPanel;

        private readonly int?[] hours = new int?[ColonySlotTable.MaxSlots];
        private int version, shownVersion = -1, shownSlot = int.MinValue;

        public static ColonyWorkingHours Instance => SingletonManager.GetSingleton<ColonyWorkingHours>();

        public ColonyWorkingHours(ISingletonLoader singletonLoader, WorkingHoursManager workingHoursManager,
            IDayNightCycle dayNightCycle, WorkingHoursPanel workingHoursPanel)
        {
            _singletonLoader = singletonLoader;
            _workingHoursManager = workingHoursManager;
            _dayNightCycle = dayNightCycle;
            _workingHoursPanel = workingHoursPanel;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(HoursKey, out IObjectLoader loader) || !loader.Has(EntriesKey)) return;
            foreach (string entry in loader.Get(EntriesKey))
            {
                string[] parts = entry.Split('|');
                if (parts.Length == 2 && int.TryParse(parts[0], out int slot) && int.TryParse(parts[1], out int value)
                    && slot >= 0 && slot < hours.Length)
                    hours[slot] = Math.Max(0, Math.Min(24, value));
            }
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            List<string> entries = Enumerable.Range(0, hours.Length).Where(slot => hours[slot] != null)
                .Select(slot => $"{slot}|{hours[slot].Value}").ToList();
            if (entries.Count > 0) singletonSaver.GetSingleton(HoursKey).Set(EntriesKey, entries);
        }

        /// <summary>A colony's working hours per day (0 to 24).</summary>
        public int HoursOf(int slot) =>
            slot >= 0 && slot < hours.Length && hours[slot] != null
                ? hours[slot].Value
                : (int)Math.Ceiling(_workingHoursManager.WorkedPartOfDay * 24f);

        private bool HasOwn(int slot) => slot >= 0 && slot < hours.Length && hours[slot] != null;

        /// <summary>Diagnostics: each colony's own hours (- for the game's).</summary>
        public string Fingerprint() => string.Join(" ", hours.Select((h, i) => $"{i}:{(h.HasValue ? h.Value.ToString() : "-")}"));

        /// <summary>When a colony's working day ends, worked out as the game does (the game's own for a colony that never chose).</summary>
        public float EndHours(int slot) =>
            HasOwn(slot) ? _workingHoursManager._startHours + hours[slot].Value / 24f * 24f : _workingHoursManager.EndHours;

        /// <summary>The game's own test, with the colony's hours.</summary>
        public bool AreWorkingHours(int slot)
        {
            if (!HasOwn(slot)) return _workingHoursManager.AreWorkingHours;
            float hoursPassedToday = _dayNightCycle.HoursPassedToday;
            if (hours[slot].Value > 0 && hoursPassedToday >= _workingHoursManager._startHours)
                return hoursPassedToday < EndHours(slot);
            return false;
        }

        /// <summary>Played on every computer: the colony's player chose new hours.</summary>
        public void Set(int slot, int newHours)
        {
            if (slot < 0 || slot >= hours.Length) return;
            hours[slot] = Math.Max(0, Math.Min(24, newHours));
            version++;
            Plugin.Log($"[Colony] Slot {slot} works {hours[slot]} hours a day");
        }

        /// <summary>A guest's change was refused, or never answered: the panel goes back to the colony's hours.</summary>
        public void Resync() => version++;

        // The panel shows the local player's colony: after a change arrives, and when this player's colony changes.
        public void UpdateSingleton()
        {
            if (!ColonyModeService.IsSeparateColonies) return;
            int slot = ColonySession.LocalSlot;
            if (slot < 0 || (slot == shownSlot && version == shownVersion)) return;
            shownSlot = slot;
            shownVersion = version;
            try
            {
                int shown = HoursOf(slot);
                _workingHoursPanel._hours = shown;
                _workingHoursPanel._increaseHoursButton?.Enable();
                _workingHoursPanel._decreaseHoursButton?.Enable();
                if (shown >= 24) _workingHoursPanel._increaseHoursButton?.Disable();
                if (shown <= 0) _workingHoursPanel._decreaseHoursButton?.Disable();
                _workingHoursPanel.UpdateTitle();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not show the colony's working hours: " + error.Message);
            }
        }

        /// <summary>The colony whose hours a beaver, workplace or chronometer keeps, or null for the game's own.</summary>
        internal static int? ColonyOf(Timberborn.BaseComponentSystem.BaseComponent component) =>
            ColonyModeService.IsSeparateColonies && Instance != null ? ColonySeparation.SimOwnerOf(component) : null;
    }

    // A beaver keeps its colony's hours (bots ignore working hours, as in the game).
    [HarmonyPatch(typeof(WorkerWorkingHours), nameof(WorkerWorkingHours.AreWorkingHours), MethodType.Getter)]
    static class ColonyWorkerWorkingHoursPatcher
    {
        static bool Prefix(WorkerWorkingHours __instance, ref bool __result)
        {
            if (__instance._ignoreWorkingHours) return true;
            long started = ColonyProfiler.Start();
            int? slot = ColonyWorkingHours.ColonyOf(__instance);
            if (slot != null) __result = ColonyWorkingHours.Instance.AreWorkingHours(slot.Value);
            ColonyProfiler.Stop("Working hours checks", started);
            return slot == null;
        }
    }

    // A workplace's productivity counts its colony's hours.
    [HarmonyPatch(typeof(WorkplaceWorkingHours), nameof(WorkplaceWorkingHours.AreWorkingHours), MethodType.Getter)]
    static class ColonyWorkplaceWorkingHoursPatcher
    {
        static bool Prefix(WorkplaceWorkingHours __instance, ref bool __result)
        {
            if (__instance._ignoreWorkingHours) return true;
            long started = ColonyProfiler.Start();
            int? slot = ColonyWorkingHours.ColonyOf(__instance);
            if (slot != null) __result = ColonyWorkingHours.Instance.AreWorkingHours(slot.Value);
            ColonyProfiler.Stop("Working hours checks", started);
            return slot == null;
        }
    }

    [ManualMethodOverwrite]
    /*
     * 9/21/2026 (Timberborn 1.1.2.4)
        SampledTime = _dayNightCycle.HoursPassedToday;
        _sampledWorkEndHours = _workingHoursManager.EndHours;
        UpdateOutputState();
     */
    // A chronometer set to working hours follows its own colony's hours.
    [HarmonyPatch(typeof(Chronometer), nameof(Chronometer.Sample))]
    static class ColonyChronometerPatcher
    {
        static bool Prefix(Chronometer __instance)
        {
            int? slot = ColonyWorkingHours.ColonyOf(__instance);
            if (slot == null) return true;
            __instance.SampledTime = __instance._dayNightCycle.HoursPassedToday;
            __instance._sampledWorkEndHours = ColonyWorkingHours.Instance.EndHours(slot.Value);
            __instance.UpdateOutputState();
            return false;
        }
    }

    // The clock's working-hours needle shows this player's colony's (display only).
    [HarmonyPatch(typeof(ClockPanel), nameof(ClockPanel.UpdateMovingParts))]
    static class ColonyClockPanelPatcher
    {
        static void Postfix(ClockPanel __instance)
        {
            if (!ColonyModeService.IsSeparateColonies || ColonyWorkingHours.Instance == null) return;
            int slot = ColonySession.LocalSlot;
            if (slot < 0) return;
            __instance._workTimeEndMarker.SetRotation(ClockPanel.NormalizeRotation(ColonyWorkingHours.Instance.EndHours(slot) / 24f));
        }
    }
}
