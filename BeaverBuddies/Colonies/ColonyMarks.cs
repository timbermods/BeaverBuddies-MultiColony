using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.Forestry;
using Timberborn.Persistence;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Whose map marks are whose. The game keeps one planting mark per tile and one set of trees to cut for the whole
    /// map; in a separate-colonies game each mark also remembers the colony that made it (saved). A colony's planters
    /// plant only on its own marks, its lumberjacks cut only the trees it marked, and its harvesters take only what grows
    /// on its own marks. A player changes only their own colony's marks.
    ///
    /// Marks made before this existed have no colony: they count for every colony.
    /// </summary>
    public class ColonyMarks : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey MarksKey = new SingletonKey("BeaverBuddies.ColonyMarks");
        private static readonly ListKey<string> PlantingKey = new ListKey<string>("Planting");
        private static readonly ListKey<string> CuttingKey = new ListKey<string>("Cutting");

        private readonly ISingletonLoader _singletonLoader;
        private readonly PlantingService _plantingService;
        private readonly TreeCuttingArea _treeCuttingArea;

        private readonly Dictionary<Vector3Int, int> planting = new Dictionary<Vector3Int, int>();
        private readonly Dictionary<Vector3Int, int> cutting = new Dictionary<Vector3Int, int>();

        /// <summary>
        /// The colony whose player is marking or unmarking planting right now (set around the replayed action), so the
        /// game's own per-tile calls know whose mark they are making.
        /// </summary>
        public static int? ActingSlot { get; set; }

        public static ColonyMarks Instance => SingletonManager.GetSingleton<ColonyMarks>();

        /// <summary>Tiles with a colony's planting or cutting mark, for the diagnostics report (a long session's size).</summary>
        public int MarkedTiles => planting.Count + cutting.Count;

        public ColonyMarks(ISingletonLoader singletonLoader, PlantingService plantingService, TreeCuttingArea treeCuttingArea)
        {
            _singletonLoader = singletonLoader;
            _plantingService = plantingService;
            _treeCuttingArea = treeCuttingArea;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(MarksKey, out IObjectLoader loader)) return;
            if (loader.Has(PlantingKey)) Read(loader.Get(PlantingKey), planting);
            if (loader.Has(CuttingKey)) Read(loader.Get(CuttingKey), cutting);
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            // Separate colonies only: a shared game's save holds only what the Stability Fork's does.
            if (!ColonyModeService.IsSeparateColonies) return;
            // Only marks that still stand, in a fixed order.
            List<string> plantingEntries = Write(planting, c => _plantingService.IsResourceAt(c));
            List<string> cuttingEntries = Write(cutting, c => _treeCuttingArea.IsInCuttingArea(c));
            if (plantingEntries.Count == 0 && cuttingEntries.Count == 0) return;
            IObjectSaver saver = singletonSaver.GetSingleton(MarksKey);
            saver.Set(PlantingKey, plantingEntries);
            saver.Set(CuttingKey, cuttingEntries);
        }

        /// <summary>The colony that made the planting mark on this tile, or null (no mark, or an older one).</summary>
        public int? PlantingOwner(Vector3Int tile) =>
            planting.TryGetValue(tile, out int slot) && _plantingService.IsResourceAt(tile) ? slot : (int?)null;

        /// <summary>The colony that marked this tile's tree for cutting, or null (no mark, or an older one).</summary>
        public int? CuttingOwner(Vector3Int tile) =>
            cutting.TryGetValue(tile, out int slot) && _treeCuttingArea.IsInCuttingArea(tile) ? slot : (int?)null;

        /// <summary>Whether <paramref name="slot"/> may change a mark whose owner is <paramref name="owner"/>.</summary>
        public static bool MayChangeMark(int slot, int? owner) => owner == null || owner.Value == slot;

        internal void SetPlanting(Vector3Int tile, int slot)
        {
            planting[tile] = slot;
            ColonyDigest.Note("plant", Hash(tile), slot);
        }

        /// <summary>
        /// Diagnostics: per colony, how many standing planting and cutting marks it has, and a hash of where they are
        /// (the same on every computer that agrees).
        /// </summary>
        public string Fingerprint()
        {
            var parts = new List<string>();
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                int plantingCount = 0, cuttingCount = 0;
                long hash = 0;
                foreach (var mark in planting)
                {
                    if (mark.Value != slot || !_plantingService.IsResourceAt(mark.Key)) continue;
                    plantingCount++;
                    hash += Hash(mark.Key);
                }
                foreach (var mark in cutting)
                {
                    if (mark.Value != slot || !_treeCuttingArea.IsInCuttingArea(mark.Key)) continue;
                    cuttingCount++;
                    hash += 31 * Hash(mark.Key);
                }
                if (plantingCount + cuttingCount > 0) parts.Add($"{slot}:{plantingCount}p{cuttingCount}c{(uint)hash:x}");
            }
            return string.Join(" ", parts);
        }

        private static long Hash(Vector3Int tile) => ((long)tile.x * 73856093) ^ ((long)tile.y * 19349663) ^ ((long)tile.z * 83492791);

        /// <summary>A colony handed over: its marks become the new owner's.</summary>
        internal void Transfer(int from, int to)
        {
            foreach (Vector3Int tile in planting.Where(m => m.Value == from).Select(m => m.Key).ToList()) planting[tile] = to;
            foreach (Vector3Int tile in cutting.Where(m => m.Value == from).Select(m => m.Key).ToList()) cutting[tile] = to;
            ColonyDigest.Note("marks-transfer", from, to);
        }
        internal void ClearPlanting(Vector3Int tile)
        {
            if (planting.Remove(tile)) ColonyDigest.Note("unplant", Hash(tile));
        }

        /// <summary>
        /// Marks trees for cutting for <paramref name="slot"/>, or unmarks them: only tiles that are free or already its
        /// own. Played on every computer from saved state alone.
        /// </summary>
        public void MarkCutting(List<Vector3Int> tiles, int slot, bool add)
        {
            // Marking also leaves alone trees growing on another colony's planting marks.
            var mine = tiles.Where(tile => MayChangeMark(slot, CuttingOwner(tile))
                && (!add || PlantingOwner(tile) == null || PlantingOwner(tile) == slot)).ToList();
            if (add)
            {
                _treeCuttingArea.AddCoordinates(mine);
                foreach (Vector3Int tile in mine) cutting[tile] = slot;
                foreach (Vector3Int tile in mine) ColonyDigest.Note("cut", Hash(tile), slot);
            }
            else
            {
                _treeCuttingArea.RemoveCoordinates(mine);
                foreach (Vector3Int tile in mine) cutting.Remove(tile);
                foreach (Vector3Int tile in mine) ColonyDigest.Note("uncut", Hash(tile), slot);
            }
            if (mine.Count < tiles.Count)
                Plugin.Log($"[Colony] Slot {slot}: {tiles.Count - mine.Count} of {tiles.Count} tree marks belong to another colony and were left alone");
        }

        private static void Read(IEnumerable<string> entries, Dictionary<Vector3Int, int> into)
        {
            foreach (string entry in entries)
            {
                string[] parts = entry.Split('|');
                if (parts.Length == 4 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y)
                    && int.TryParse(parts[2], out int z) && int.TryParse(parts[3], out int slot))
                    into[new Vector3Int(x, y, z)] = slot;
            }
        }

        private static List<string> Write(Dictionary<Vector3Int, int> marks, Func<Vector3Int, bool> stands) =>
            marks.Where(m => stands(m.Key))
                .OrderBy(m => m.Key.x).ThenBy(m => m.Key.y).ThenBy(m => m.Key.z)
                .Select(m => $"{m.Key.x}|{m.Key.y}|{m.Key.z}|{m.Value}").ToList();
    }

    // A player's planting action changes only marks their colony may change: its own, and marks of nobody's.
    [HarmonyPatch(typeof(PlantingService), nameof(PlantingService.SetPlantingCoordinates))]
    static class ColonyPlantingMarkSetPatcher
    {
        static bool Prefix(Vector3Int coordinates)
        {
            int? acting = ColonyMarks.ActingSlot;
            if (acting == null || ColonyMarks.Instance == null) return true;
            return ColonyMarks.MayChangeMark(acting.Value, ColonyMarks.Instance.PlantingOwner(coordinates));
        }

        static void Postfix(Vector3Int coordinates, bool __runOriginal)
        {
            int? acting = ColonyMarks.ActingSlot;
            if (!__runOriginal || acting == null) return;
            ColonyMarks.Instance?.SetPlanting(coordinates, acting.Value);
        }
    }

    [HarmonyPatch(typeof(PlantingService), nameof(PlantingService.UnsetPlantingCoordinates))]
    static class ColonyPlantingMarkUnsetPatcher
    {
        static bool Prefix(Vector3Int coordinates)
        {
            int? acting = ColonyMarks.ActingSlot;
            if (acting == null || ColonyMarks.Instance == null) return true;
            return ColonyMarks.MayChangeMark(acting.Value, ColonyMarks.Instance.PlantingOwner(coordinates));
        }

        // Also when the game removes a mark itself (a building placed over it, the ground changing).
        static void Postfix(Vector3Int coordinates, bool __runOriginal)
        {
            if (__runOriginal) ColonyMarks.Instance?.ClearPlanting(coordinates);
        }
    }
}
