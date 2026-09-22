using System;
using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    /// <summary>A map cell: a tile at a height. Roads join only at the same height (stairs aside).</summary>
    public readonly struct ColonyCell : IEquatable<ColonyCell>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ColonyCell(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public ColonyCell Step(int dx, int dy) => new ColonyCell(X + dx, Y + dy, Z);

        public bool Equals(ColonyCell other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is ColonyCell other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397 ^ Y) * 397 ^ Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    /// <summary>What the road rule reads of the map. Implemented against the game in ColonyGameWorld, and by fakes in tests.</summary>
    public interface IColonyRoadMap
    {
        /// <summary>
        /// The colony whose road is on this cell: a finished road of one of its districts, or a path (stairs...) it
        /// placed, finished or still being built. Null for none.
        /// </summary>
        int? RoadOwnerAt(ColonyCell cell);

        /// <summary>
        /// The colony whose building has its entrance on this cell: the cell outside its door, where the road it waits
        /// for goes (the game's PositionedEntrance.Coordinates). Null for none. A Trading Post's entrances are nobody's:
        /// each of its halves takes its own colony's road.
        /// </summary>
        int? EntranceOwnerAt(ColonyCell cell);
    }

    /// <summary>
    /// The one rule separate colonies build by: two colonies' roads never join, except through a Trading Post. There is
    /// no land: a player may build anywhere, right up to another colony's buildings and roads, as long as nothing of
    /// theirs would join the other colony's roads. What joins them:
    ///  - a path, stairs or anything else that carries a road (the game's PathSpec) on or beside another colony's road,
    ///    or at another colony's building's entrance (that building would join the placer's roads);
    ///  - a building whose entrance (the cell outside its door, where its road must be) is on or beside another
    ///    colony's road: the road it needs there would join them.
    /// A Trading Post is the exception, and the only place two colonies' roads meet: one half's entrance on each
    /// colony's road. It may be placed anywhere, roads or not; it trades once a different colony's road reaches each
    /// half (TradingPosts.JoinsTwoColonies), so the roads are what it needs to work, not to be placed.
    /// The game's own placement check already refuses a building that joins two districts' finished roads in the
    /// placing player's interface; this also sees paths still being built, and is what the host judges.
    ///
    /// Reads only, and only what is the same on every computer, so a verdict is too.
    /// </summary>
    public static class ColonyRoadRule
    {
        // The cell itself and its four neighbours at the same height.
        private static readonly (int dx, int dy)[] Around = { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>
        /// Why <paramref name="slot"/>'s colony may not place this here, or None. <paramref name="pathLike"/>: it carries
        /// a road itself (a path, stairs...). <paramref name="entrance"/>: the cell outside its door, where its road must
        /// be, if it has one.
        /// </summary>
        public static ColonyRefusal Conflict(int slot, IReadOnlyList<ColonyCell> footprint, ColonyCell? entrance, bool pathLike,
            IColonyRoadMap map, out string detail)
        {
            detail = null;
            if (pathLike)
            {
                for (int i = 0; i < footprint.Count; i++)
                {
                    if (TouchesOthersRoad(slot, footprint[i], map, out detail)) return ColonyRefusal.TouchesOtherColony;
                    int? door = map.EntranceOwnerAt(footprint[i]);
                    if (door != null && door.Value != slot)
                    {
                        detail = $"would lead into slot {door}'s building at its entrance {footprint[i]}";
                        return ColonyRefusal.TouchesOtherColony;
                    }
                }
            }
            if (entrance != null && TouchesOthersRoad(slot, entrance.Value, map, out detail)) return ColonyRefusal.TouchesOtherColony;
            return ColonyRefusal.None;
        }

        private static bool TouchesOthersRoad(int slot, ColonyCell cell, IColonyRoadMap map, out string detail)
        {
            for (int i = 0; i < Around.Length; i++)
            {
                ColonyCell near = cell.Step(Around[i].dx, Around[i].dy);
                int? owner = map.RoadOwnerAt(near);
                if (owner != null && owner.Value != slot)
                {
                    detail = $"would join slot {owner}'s road at {near}";
                    return true;
                }
            }
            detail = null;
            return false;
        }

    }
}
