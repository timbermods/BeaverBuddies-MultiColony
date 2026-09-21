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

        /// <summary>Counts every change, so a display can tell when to draw again.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Adds (<paramref name="sign"/> +1) or takes away (-1) the reach of one colony's building standing on
        /// <paramref name="tiles"/> (its footprint, each column once).
        /// </summary>
        public void Apply(int slot, IEnumerable<(int x, int y)> tiles, int sign)
        {
            if (slot < 0 || slot >= ColonySlotTable.MaxSlots || tiles == null || sign == 0) return;
            Version++;
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

        /// <summary>How many tiles are a colony's land.</summary>
        public int LandSize(int slot)
        {
            int size = 0;
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == slot) size++;
            }
            return size;
        }

        /// <summary>A colony may use its own land and land nobody holds.</summary>
        public bool MayUse(int slot, int x, int y)
        {
            int? owner = Owner(x, y);
            return owner == null || owner.Value == slot;
        }

        /// <summary>
        /// The one colony whose land these tiles are (a building's footprint), leaving out tiles nobody holds. Null when
        /// none of them is anyone's, or they are two colonies'.
        /// </summary>
        public int? SoleOwner(IEnumerable<(int x, int y)> tiles)
        {
            int? owner = null;
            foreach (var (x, y) in tiles)
            {
                int? here = Owner(x, y);
                if (here == null) continue;
                if (owner != null && owner.Value != here.Value) return null;
                owner = here;
            }
            return owner;
        }

        /// <summary>
        /// Whether any colony other than <paramref name="slot"/> reaches a tile within <see cref="Radius"/> of these:
        /// a colony founded here would have its land run straight into another's.
        /// </summary>
        public bool OthersReachNear(int slot, IEnumerable<(int x, int y)> tiles)
        {
            foreach (var (x, y) in tiles)
            {
                foreach (var (dx, dy) in Disk)
                {
                    for (int other = 0; other < ColonySlotTable.MaxSlots; other++)
                    {
                        if (other != slot && Reaches(other, x + dx, y + dy)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// A colony handed over to another: everything <paramref name="from"/> reaches and owns becomes
        /// <paramref name="to"/>'s. A tile both reached stays with whichever owned it; one only <paramref name="from"/>
        /// owned becomes <paramref name="to"/>'s.
        /// </summary>
        public void Transfer(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= ColonySlotTable.MaxSlots || to >= ColonySlotTable.MaxSlots) return;
            Version++;
            int[] source = counts[from];
            if (source != null)
            {
                int[] target = counts[to] ??= new int[width * height];
                for (int i = 0; i < source.Length; i++) target[i] += source[i];
                counts[from] = null;
            }
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == from) owners[i] = to;
            }
        }

        /// <summary>The tiles of a colony's land that touch land not its own: its outline, for drawing.</summary>
        public IEnumerable<(int x, int y)> BorderTiles(int slot)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (owners[y * width + x] != slot) continue;
                    if (!IsOwnedBy(slot, x + 1, y) || !IsOwnedBy(slot, x - 1, y) || !IsOwnedBy(slot, x, y + 1) || !IsOwnedBy(slot, x, y - 1))
                        yield return (x, y);
                }
            }
        }

        // Off the map counts as the colony's own, so the map edge draws no line.
        private bool IsOwnedBy(int slot, int x, int y) => !Contains(x, y) || owners[y * width + x] == slot;

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
