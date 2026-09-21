using System;
using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    public enum ColonyScopeKind
    {
        /// <summary>Shared by everyone, or only ever acting on the actor's own colony: speed, chat, pings, unmarking.</summary>
        Global,
        /// <summary>Acts on named entities; each must be the actor's or nobody's.</summary>
        Entities,
        /// <summary>Moves beavers between two districts; both must be the actor's.</summary>
        Migration,
        /// <summary>
        /// Places a building: the actor's colony must have it unlocked, and it must neither stand where another colony
        /// works nor touch another colony's roads.
        /// </summary>
        Placement,
        /// <summary>A list of entities cut down to the ones the actor may act on.</summary>
        List,
        /// <summary>Founds a colony for a player who has none yet.</summary>
        Founding,
        /// <summary>Marks map tiles (trees to cut, crops to plant): cut down to the tiles the actor's colony may use.</summary>
        Tiles,
    }

    /// <summary>Where a building would go, in the event's own terms. The world turns it into what it needs.</summary>
    public sealed class ColonyPlacement
    {
        public string TemplateName;
        public int X, Y, Z;
        /// <summary>The game's Orientation as an int, so this file needs no game types.</summary>
        public int Orientation;
        public bool IsFlipped;
    }

    /// <summary>A list in an event that the rules may shorten in place, keeping the order of what is left.</summary>
    public interface IColonyList
    {
        int Count { get; }
        /// <summary>Removes every item <paramref name="keep"/> rejects and returns how many were removed.</summary>
        int Filter(Func<string, bool> keep);
        /// <summary>Counts the items <paramref name="keep"/> accepts without changing the list.</summary>
        int CountKept(Func<string, bool> keep);
    }

    /// <summary>What an event touches, declared by the event itself (see ReplayEvent.GetColonyScope).</summary>
    public sealed class ColonyScope
    {
        public ColonyScopeKind Kind { get; private set; }
        public IReadOnlyList<string> EntityIds { get; private set; } = Array.Empty<string>();
        public ColonyPlacement Placement { get; private set; }
        public IColonyList List { get; private set; }
        /// <summary>
        /// A trading post belongs to no one: either colony may remove it. Set on demolition actions only; running a
        /// half (workers, priority) stays with its district's owner, and a crossing between one colony's own districts
        /// is that colony's.
        /// </summary>
        public bool CrossingsNeutral { get; private set; }

        public static readonly ColonyScope Global = new ColonyScope { Kind = ColonyScopeKind.Global };

        /// <summary>Null or empty ids are skipped: the event handles a missing entity itself.</summary>
        public static ColonyScope Entities(params string[] entityIds) =>
            new ColonyScope { Kind = ColonyScopeKind.Entities, EntityIds = entityIds ?? Array.Empty<string>() };

        /// <summary>Removing things: like <see cref="Entities"/>, but a District Crossing may be removed by anyone.</summary>
        public static ColonyScope Demolish(params string[] entityIds) =>
            new ColonyScope { Kind = ColonyScopeKind.Entities, EntityIds = entityIds ?? Array.Empty<string>(), CrossingsNeutral = true };

        /// <summary>Beavers leave <paramref name="fromDistrictId"/> for <paramref name="toDistrictId"/>.</summary>
        public static ColonyScope Migration(string fromDistrictId, string toDistrictId) =>
            new ColonyScope { Kind = ColonyScopeKind.Migration, EntityIds = new[] { fromDistrictId, toDistrictId } };

        public static ColonyScope Place(ColonyPlacement placement) =>
            new ColonyScope { Kind = ColonyScopeKind.Placement, Placement = placement };

        public static ColonyScope Found(ColonyPlacement placement) =>
            new ColonyScope { Kind = ColonyScopeKind.Founding, Placement = placement };

        /// <summary>
        /// Marking map tiles (trees to cut, crops to plant): only where the actor's colony may work, which is where it
        /// reaches or where no other colony does. <paramref name="tileIdOf"/> names a tile for the world.
        /// </summary>
        public static ColonyScope Tiles<T>(List<T> items, Func<T, string> tileIdOf) =>
            new ColonyScope { Kind = ColonyScopeKind.Tiles, List = new ColonyList<T>(items, tileIdOf) };

        public static ColonyScope EntityList<T>(List<T> items, Func<T, string> entityIdOf, bool demolition = false) =>
            new ColonyScope { Kind = ColonyScopeKind.List, List = new ColonyList<T>(items, entityIdOf), CrossingsNeutral = demolition };
    }

    internal sealed class ColonyList<T> : IColonyList
    {
        private readonly List<T> items;
        private readonly Func<T, string> idOf;

        public ColonyList(List<T> items, Func<T, string> idOf)
        {
            this.items = items ?? new List<T>();
            this.idOf = idOf;
        }

        public int Count => items.Count;
        public int Filter(Func<string, bool> keep) => items.RemoveAll(item => !keep(idOf(item)));

        public int CountKept(Func<string, bool> keep)
        {
            int kept = 0;
            foreach (T item in items)
            {
                if (keep(idOf(item))) kept++;
            }
            return kept;
        }
    }

    /// <summary>The game state the rules read. Implemented against the game in ColonyGameWorld, and by fakes in tests.</summary>
    public interface IColonyWorld
    {
        /// <summary>The slot owning the entity (by its district), or null when it has no owner or does not exist.</summary>
        int? OwnerOf(string entityId);

        /// <summary>True for a half of a trading post (a District Crossing between two colonies).</summary>
        bool IsCrossing(string entityId);

        /// <summary>Whether this slot's colony may build this building (always true without separate science).</summary>
        bool IsUnlockedFor(int slot, string templateName);

        /// <summary>Whether this slot's colony may mark or build on a tile: it reaches it, or no other colony does.</summary>
        bool MayUseTile(int slot, string tileId);

        /// <summary>
        /// Why this slot's colony may not place this building here (another colony's area or roads), or None.
        /// </summary>
        ColonyRefusal PlacementConflict(int slot, ColonyPlacement placement, out string detail);
    }

    public enum ColonyRefusal
    {
        None,
        /// <summary>A named building or district belongs to another player who is playing now.</summary>
        OtherColony,
        /// <summary>Nothing in an area action is the actor's to change.</summary>
        NothingOwn,
        /// <summary>The actor's colony has not unlocked this building.</summary>
        Locked,
        /// <summary>This player already has a colony, founding is off, or the player has no slot.</summary>
        CannotFound,
        /// <summary>The spot is taken or the ground does not allow the building (checked when founding).</summary>
        Blocked,
        /// <summary>A new colony's district center would join another colony's roads.</summary>
        FoundingConflict,
        /// <summary>Not enough science in the actor's colony.</summary>
        NotEnoughScience,
        /// <summary>The spot is where another colony works (near its buildings and paths).</summary>
        OtherColonyArea,
        /// <summary>The building would touch another colony's roads (and so join or block them).</summary>
        TouchesOtherColony,
    }

    public readonly struct ColonyVerdict
    {
        public readonly ColonyRefusal Refusal;
        /// <summary>How many items a list action lost (or would lose) to other colonies.</summary>
        public readonly int Removed;
        public readonly string Detail;

        public ColonyVerdict(ColonyRefusal refusal, int removed, string detail)
        {
            Refusal = refusal;
            Removed = removed;
            Detail = detail;
        }

        public bool IsAllowed => Refusal == ColonyRefusal.None;

        public static readonly ColonyVerdict Allow = new ColonyVerdict(ColonyRefusal.None, 0, null);
        public static ColonyVerdict Refuse(ColonyRefusal refusal, string detail) => new ColonyVerdict(refusal, 0, detail);
        public static ColonyVerdict Kept(int removed) => new ColonyVerdict(ColonyRefusal.None, removed, null);
    }

    /// <summary>
    /// Decides whether a player may do an action. What belongs to a colony is what its districts hold (a district
    /// center carries its owner's slot; beavers belong to their district; buildings to their district, or else to the
    /// colony that placed them), and the land it works: tiles near its buildings and paths. A player changes only their
    /// own colony's things and things nobody owns, builds and marks only where no other colony works (where two
    /// colonies' land overlaps, near a trading post, both may), and never touches another colony's roads. Whether the
    /// other player is playing makes no difference: colonies meet only at trading posts.
    ///
    /// Reads only: it changes a list event only when <c>rewrite</c> is true (the host, before replaying it).
    /// </summary>
    public static class ColonyRules
    {
        /// <summary>The actor may change something owned by <paramref name="owner"/>: its own, or nobody's.</summary>
        public static bool MayChange(int actorSlot, int? owner) => owner == null || owner.Value == actorSlot;

        public static ColonyVerdict Judge(ColonyScope scope, int actorSlot, IColonyWorld world, bool rewrite)
        {
            if (scope == null) return ColonyVerdict.Allow;
            switch (scope.Kind)
            {
                case ColonyScopeKind.Entities:
                    foreach (string id in scope.EntityIds)
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        if (scope.CrossingsNeutral && world.IsCrossing(id)) continue;
                        int? owner = world.OwnerOf(id);
                        if (!MayChange(actorSlot, owner))
                            return ColonyVerdict.Refuse(ColonyRefusal.OtherColony, $"{id} belongs to slot {owner}");
                    }
                    return ColonyVerdict.Allow;

                case ColonyScopeKind.Migration:
                    // Beavers move only within a colony: neither taken from another colony nor sent to one.
                    foreach (string district in scope.EntityIds)
                    {
                        if (string.IsNullOrEmpty(district)) continue;
                        int? owner = world.OwnerOf(district);
                        if (!MayChange(actorSlot, owner))
                            return ColonyVerdict.Refuse(ColonyRefusal.OtherColony, $"district {district} is slot {owner}'s");
                    }
                    return ColonyVerdict.Allow;

                case ColonyScopeKind.Placement:
                {
                    if (scope.Placement == null) return ColonyVerdict.Allow;
                    if (!world.IsUnlockedFor(actorSlot, scope.Placement.TemplateName))
                        return ColonyVerdict.Refuse(ColonyRefusal.Locked, $"{scope.Placement.TemplateName} is locked for slot {actorSlot}");
                    ColonyRefusal conflict = world.PlacementConflict(actorSlot, scope.Placement, out string detail);
                    return conflict == ColonyRefusal.None ? ColonyVerdict.Allow : ColonyVerdict.Refuse(conflict, detail);
                }

                case ColonyScopeKind.List:
                    return JudgeList(scope.List, scope.CrossingsNeutral, actorSlot, world, rewrite);

                case ColonyScopeKind.Tiles:
                    return JudgeTiles(scope.List, actorSlot, world, rewrite);

                case ColonyScopeKind.Founding:
                    // Judged by the founding service, which knows who has a colony already.
                    return ColonyVerdict.Allow;

                default:
                    return ColonyVerdict.Allow;
            }
        }

        private static ColonyVerdict JudgeList(IColonyList list, bool crossingsNeutral, int actorSlot, IColonyWorld world,
            bool rewrite)
        {
            Func<string, bool> keep = id =>
                string.IsNullOrEmpty(id)
                || (crossingsNeutral && world.IsCrossing(id))
                || MayChange(actorSlot, world.OwnerOf(id));
            return Keep(list, keep, actorSlot, rewrite);
        }

        private static ColonyVerdict JudgeTiles(IColonyList list, int actorSlot, IColonyWorld world, bool rewrite) =>
            Keep(list, tile => string.IsNullOrEmpty(tile) || world.MayUseTile(actorSlot, tile), actorSlot, rewrite);

        private static ColonyVerdict Keep(IColonyList list, Func<string, bool> keep, int actorSlot, bool rewrite)
        {
            if (list == null || list.Count == 0) return ColonyVerdict.Allow;
            int total = list.Count;
            int kept = list.CountKept(keep);
            if (kept == 0) return ColonyVerdict.Refuse(ColonyRefusal.NothingOwn, $"none of {total} items are slot {actorSlot}'s to change");
            int removed = total - kept;
            if (rewrite && removed > 0) list.Filter(keep);
            return ColonyVerdict.Kept(removed);
        }

        /// <summary>
        /// Whether a player may found a colony now. Once per player: only a player whose slot owns no district center
        /// yet. The save must be a separate-colonies game, or the host must allow it this session (founding turns a
        /// shared game into one). The spot must be free, and the new district center must not join another colony's
        /// roads or stand on its land.
        /// </summary>
        public static ColonyVerdict JudgeFounding(bool actorHasSlot, bool actorOwnsDistrict, bool foundingAllowed,
            bool blocksValid, bool touchesOtherDistrict, bool onOtherColonyLand = false)
        {
            if (!actorHasSlot) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "a helper plays another player's colony");
            if (actorOwnsDistrict) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "this player already has a colony");
            if (!foundingAllowed) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "the host has not turned on separate colonies");
            if (!blocksValid) return ColonyVerdict.Refuse(ColonyRefusal.Blocked, "the spot is taken or unsuitable");
            if (touchesOtherDistrict) return ColonyVerdict.Refuse(ColonyRefusal.FoundingConflict, "it would join another district's roads");
            if (onOtherColonyLand) return ColonyVerdict.Refuse(ColonyRefusal.OtherColonyArea, "it would stand on another colony's land");
            return ColonyVerdict.Allow;
        }
    }
}
