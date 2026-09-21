using System;
using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Each colony's land. A colony reaches every tile within <see cref="Radius"/> tiles of one of its buildings or
    /// paths (the game lets a district's beavers work about as far off its roads), and a tile is the land of the colony
    /// that reached it first. Another colony reaching the same tile later gains nothing there, so no colony can build
    /// its way into another's land; a tile passes on only when its owner no longer reaches it at all (to the lowest
    /// numbered colony still reaching it). Kept free of the game so it can be checked headless.
    ///
    /// Reach counts: every building tile adds one to each tile of its disk, and removing the building takes the same
    /// away. Who got there first depends on the order things were built, which is the same on every computer (they
    /// all play the same actions in the same order); what cannot be rebuilt from the buildings alone, the owners of
    /// tiles two colonies reach, is saved (see ColonyReach).
    /// </summary>
    public sealed class ColonyReachGrid
    {
        public const int Radius = 10;

        private static readonly (int dx, int dy)[] Disk = BuildDisk(Radius);

        private readonly int width, height;
        private readonly int[][] counts = new int[ColonySlotTable.MaxSlots][];
        private readonly int[] owners;

        public ColonyReachGrid(int width, int height)
        {
            this.width = Math.Max(0, width);
            this.height = Math.Max(0, height);
            owners = new int[this.width * this.height];
            for (int i = 0; i < owners.Length; i++) owners[i] = -1;
        }

        public int Width => width;
        public int Height => height;

        /// <summary>
        /// Adds (<paramref name="sign"/> +1) or takes away (-1) the reach of one colony's building standing on
        /// <paramref name="tiles"/> (its footprint, each column once).
        /// </summary>
        public void Apply(int slot, IEnumerable<(int x, int y)> tiles, int sign)
        {
            if (slot < 0 || slot >= ColonySlotTable.MaxSlots || tiles == null || sign == 0) return;
            int[] grid = counts[slot] ??= new int[width * height];
            foreach (var (x, y) in tiles)
            {
                foreach (var (dx, dy) in Disk)
                {
                    int tx = x + dx, ty = y + dy;
                    if (tx < 0 || ty < 0 || tx >= width || ty >= height) continue;
                    int i = ty * width + tx;
                    int before = grid[i];
                    grid[i] += sign;
                    if (before <= 0 && grid[i] > 0 && owners[i] < 0) owners[i] = slot;
                    else if (before > 0 && grid[i] <= 0 && owners[i] == slot) owners[i] = NextReacher(i);
                }
            }
        }

        private int NextReacher(int i)
        {
            for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
            {
                if (counts[slot] != null && counts[slot][i] > 0) return slot;
            }
            return -1;
        }

        public bool Reaches(int slot, int x, int y)
        {
            if (slot < 0 || slot >= ColonySlotTable.MaxSlots || !Contains(x, y)) return false;
            int[] grid = counts[slot];
            return grid != null && grid[y * width + x] > 0;
        }

        /// <summary>Whose land the tile is, or null when no colony reaches it.</summary>
        public int? Owner(int x, int y)
        {
            if (!Contains(x, y)) return null;
            int owner = owners[y * width + x];
            return owner < 0 ? (int?)null : owner;
        }

        /// <summary>A colony may use its own land and land nobody holds.</summary>
        public bool MayUse(int slot, int x, int y)
        {
            int? owner = Owner(x, y);
            return owner == null || owner.Value == slot;
        }

        /// <summary>Tiles more than one colony reaches, with their owner: what saving must keep.</summary>
        public IEnumerable<(int x, int y, int slot)> ContestedTiles()
        {
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] < 0) continue;
                int reaching = 0;
                for (int slot = 0; slot < ColonySlotTable.MaxSlots; slot++)
                {
                    if (counts[slot] != null && counts[slot][i] > 0) reaching++;
                }
                if (reaching > 1) yield return (i % width, i / width, owners[i]);
            }
        }

        /// <summary>Restores a saved owner of a contested tile (only a colony that reaches it can own it).</summary>
        public void RestoreOwner(int x, int y, int slot)
        {
            if (Reaches(slot, x, y)) owners[y * width + x] = slot;
        }

        private bool Contains(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        private static (int, int)[] BuildDisk(int radius)
        {
            var disk = new List<(int, int)>();
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius) disk.Add((dx, dy));
                }
            }
            return disk.ToArray();
        }
    }
}
