using System;
using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    /// <summary>A map tile in X and Y only; height never decides who owns land.</summary>
    public readonly struct ColonyTile : IEquatable<ColonyTile>
    {
        public readonly int X;
        public readonly int Y;

        public ColonyTile(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ColonyTile other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ColonyTile other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// Who owns which land in a separate-colonies game. Every tile belongs to the colony whose starting building is
    /// nearest (squared distance in X and Y, integers only, ties to the lower colony number), so ownership is a pure
    /// function of the saved start coordinates and is the same on every computer. Colonies are numbered from 1 in the
    /// order of the map's starting locations.
    ///
    /// The border strip is the tiles on each side that touch another colony's land (4-neighbours). Only a District
    /// Crossing half may be built there, which keeps the two colonies' roads apart so the game never merges them.
    /// </summary>
    public sealed class ColonyTerritory
    {
        private readonly ColonyTile[] starts;

        public ColonyTerritory(IReadOnlyList<ColonyTile> starts)
        {
            if (starts == null) throw new ArgumentNullException(nameof(starts));
            this.starts = new ColonyTile[starts.Count];
            for (int i = 0; i < starts.Count; i++) this.starts[i] = starts[i];
        }

        public int ColonyCount => starts.Length;

        public IReadOnlyList<ColonyTile> Starts => starts;

        /// <summary>The colony (1 to ColonyCount) that owns the tile. 0 only when there are no colonies.</summary>
        public int OwnerOf(int x, int y)
        {
            int best = 0;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < starts.Length; i++)
            {
                long dx = (long)x - starts[i].X;
                long dy = (long)y - starts[i].Y;
                long distance = dx * dx + dy * dy;
                // Strictly nearer only, so a tie stays with the lower colony number.
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i + 1;
                }
            }
            return best;
        }

        public int OwnerOf(ColonyTile tile) => OwnerOf(tile.X, tile.Y);

        /// <summary>True when one of the four neighbouring tiles belongs to a different colony.</summary>
        public bool IsStrip(int x, int y)
        {
            int owner = OwnerOf(x, y);
            return OwnerOf(x + 1, y) != owner || OwnerOf(x - 1, y) != owner
                || OwnerOf(x, y + 1) != owner || OwnerOf(x, y - 1) != owner;
        }

        public bool IsStrip(ColonyTile tile) => IsStrip(tile.X, tile.Y);

        /// <summary>Every strip tile inside a map of the given size, row by row. Used to draw the border.</summary>
        public List<ColonyTile> StripTiles(int width, int height)
        {
            var result = new List<ColonyTile>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsStrip(x, y)) result.Add(new ColonyTile(x, y));
                }
            }
            return result;
        }
    }
}
