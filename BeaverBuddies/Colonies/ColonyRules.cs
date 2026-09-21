using System;
using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    public enum ColonyScopeKind
    {
        /// <summary>Shared by everyone: speed, working hours, chat-like events, map areas.</summary>
        Global,
        /// <summary>Acts on named entities; each must be the actor's, unowned, or owned by an absent player.</summary>
        Entities,
        /// <summary>Moves beavers: the district they leave must be the actor's; they may go to anyone's.</summary>
        Migration,
        /// <summary>Places a building: allowed anywhere, if the actor's colony has it unlocked.</summary>
        Placement,
        /// <summary>A list of entities cut down to the ones the actor may act on.</summary>
        List,
        /// <summary>Founds a colony for a player who has none yet.</summary>
        Founding,
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
        /// A District Crossing belongs to no one: either player may remove it. Set on demolition actions only;
        /// running a crossing half (workers, priority) stays with its district's owner.
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

        /// <summary>Map areas (trees to cut, crops to plant) are nobody's: resources near a border are shared.</summary>
        public static ColonyScope TileList<T>(List<T> items) => Global;

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

        /// <summary>True for a District Crossing half.</summary>
        bool IsCrossing(string entityId);

        /// <summary>Whether this slot's colony may build this building (always true without separate science).</summary>
        bool IsUnlockedFor(int slot, string templateName);
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
    /// Decides whether a player may do an action. There is no territory: anyone may build anywhere. What belongs to a
    /// colony is what its districts hold (a district center carries its owner's slot; buildings and beavers belong to
    /// their district). A player may change only their own colony's things, anything that belongs to no district,
    /// and the things of a player who is not playing right now (co-op: both colonies keep running).
    ///
    /// Reads only: it changes a list event only when <c>rewrite</c> is true (the host, before replaying it).
    /// </summary>
    public static class ColonyRules
    {
        /// <summary>The actor may change something owned by <paramref name="owner"/>.</summary>
        public static bool MayChange(int actorSlot, int? owner, Func<int, bool> isPresent) =>
            owner == null || owner.Value == actorSlot || !isPresent(owner.Value);

        public static ColonyVerdict Judge(ColonyScope scope, int actorSlot, IColonyWorld world,
            Func<int, bool> isPresent, bool rewrite)
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
                        if (!MayChange(actorSlot, owner, isPresent))
                            return ColonyVerdict.Refuse(ColonyRefusal.OtherColony, $"{id} belongs to slot {owner}");
                    }
                    return ColonyVerdict.Allow;

                case ColonyScopeKind.Migration:
                {
                    string from = scope.EntityIds.Count > 0 ? scope.EntityIds[0] : null;
                    // Sending beavers to another colony is allowed (it is how a failing colony is rescued); taking
                    // them from another player's district is not.
                    if (string.IsNullOrEmpty(from)) return ColonyVerdict.Allow;
                    int? owner = world.OwnerOf(from);
                    return MayChange(actorSlot, owner, isPresent)
                        ? ColonyVerdict.Allow
                        : ColonyVerdict.Refuse(ColonyRefusal.OtherColony, $"beavers of slot {owner}'s district {from}");
                }

                case ColonyScopeKind.Placement:
                    if (scope.Placement != null && !world.IsUnlockedFor(actorSlot, scope.Placement.TemplateName))
                        return ColonyVerdict.Refuse(ColonyRefusal.Locked, $"{scope.Placement.TemplateName} is locked for slot {actorSlot}");
                    return ColonyVerdict.Allow;

                case ColonyScopeKind.List:
                    return JudgeList(scope.List, scope.CrossingsNeutral, actorSlot, world, isPresent, rewrite);

                case ColonyScopeKind.Founding:
                    // Judged by the founding service, which knows who has a colony already.
                    return ColonyVerdict.Allow;

                default:
                    return ColonyVerdict.Allow;
            }
        }

        private static ColonyVerdict JudgeList(IColonyList list, bool crossingsNeutral, int actorSlot, IColonyWorld world,
            Func<int, bool> isPresent, bool rewrite)
        {
            if (list == null || list.Count == 0) return ColonyVerdict.Allow;
            int total = list.Count;
            Func<string, bool> keep = id =>
                string.IsNullOrEmpty(id)
                || (crossingsNeutral && world.IsCrossing(id))
                || MayChange(actorSlot, world.OwnerOf(id), isPresent);
            int kept = list.CountKept(keep);
            if (kept == 0) return ColonyVerdict.Refuse(ColonyRefusal.NothingOwn, $"none of {total} items are slot {actorSlot}'s to change");
            int removed = total - kept;
            if (rewrite && removed > 0) list.Filter(keep);
            return ColonyVerdict.Kept(removed);
        }

        /// <summary>
        /// Whether a player may found a colony now. Once per player: only a player whose slot owns no district center
        /// yet. The save must be a separate-colonies game, or the host must allow it this session (founding turns a
        /// shared game into one). The spot must be free and the new district center must not join another colony's
        /// roads.
        /// </summary>
        public static ColonyVerdict JudgeFounding(bool actorHasSlot, bool actorOwnsDistrict, bool foundingAllowed,
            bool blocksValid, bool touchesOtherDistrict)
        {
            if (!actorHasSlot) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "a helper plays another player's colony");
            if (actorOwnsDistrict) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "this player already has a colony");
            if (!foundingAllowed) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "the host has not turned on separate colonies");
            if (!blocksValid) return ColonyVerdict.Refuse(ColonyRefusal.Blocked, "the spot is taken or unsuitable");
            if (touchesOtherDistrict) return ColonyVerdict.Refuse(ColonyRefusal.FoundingConflict, "it would join another district's roads");
            return ColonyVerdict.Allow;
        }
    }
}
