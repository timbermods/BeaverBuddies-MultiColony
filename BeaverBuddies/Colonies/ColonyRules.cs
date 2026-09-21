using System;
using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    public enum ColonyScopeKind
    {
        /// <summary>Shared by everyone: speed, working hours, unlocks, chat-like events.</summary>
        Global,
        /// <summary>Acts on named entities (buildings, district centers); each must be on the actor's land.</summary>
        Entities,
        /// <summary>Places a building; every footprint tile must be the actor's, and the strip is for crossings only.</summary>
        Placement,
        /// <summary>A list of tiles or entities that is filtered down to the actor's own.</summary>
        List,
    }

    /// <summary>Where a building would go, in the event's own terms. The world turns it into footprint tiles.</summary>
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
        int Filter(Func<ColonyTarget, bool> keep);
        /// <summary>Counts the items <paramref name="keep"/> accepts without changing the list.</summary>
        int CountKept(Func<ColonyTarget, bool> keep);
    }

    /// <summary>What an event touches, declared by the event itself (see ReplayEvent.GetColonyScope).</summary>
    public sealed class ColonyScope
    {
        public ColonyScopeKind Kind { get; private set; }
        public IReadOnlyList<string> EntityIds { get; private set; } = Array.Empty<string>();
        public ColonyPlacement Placement { get; private set; }
        public IColonyList List { get; private set; }

        public static readonly ColonyScope Global = new ColonyScope { Kind = ColonyScopeKind.Global };

        /// <summary>Null or empty ids are skipped: the event handles a missing entity itself.</summary>
        public static ColonyScope Entities(params string[] entityIds) =>
            new ColonyScope { Kind = ColonyScopeKind.Entities, EntityIds = entityIds ?? Array.Empty<string>() };

        public static ColonyScope Place(ColonyPlacement placement) =>
            new ColonyScope { Kind = ColonyScopeKind.Placement, Placement = placement };

        public static ColonyScope TileList<T>(List<T> items, Func<T, ColonyTile> tileOf) =>
            new ColonyScope { Kind = ColonyScopeKind.List, List = new ColonyList<T>(items, item => ColonyTarget.At(tileOf(item))) };

        public static ColonyScope EntityList<T>(List<T> items, Func<T, string> entityIdOf) =>
            new ColonyScope { Kind = ColonyScopeKind.List, List = new ColonyList<T>(items, item => ColonyTarget.Entity(entityIdOf(item))) };
    }

    /// <summary>One item of a list scope: a tile, or an entity whose tile the world looks up.</summary>
    public readonly struct ColonyTarget
    {
        public readonly ColonyTile Tile;
        public readonly string EntityId;
        public bool IsEntity => EntityId != null;

        private ColonyTarget(ColonyTile tile, string entityId)
        {
            Tile = tile;
            EntityId = entityId;
        }

        public static ColonyTarget At(ColonyTile tile) => new ColonyTarget(tile, null);
        public static ColonyTarget Entity(string entityId) => new ColonyTarget(default, entityId ?? "");
    }

    internal sealed class ColonyList<T> : IColonyList
    {
        private readonly List<T> items;
        private readonly Func<T, ColonyTarget> targetOf;

        public ColonyList(List<T> items, Func<T, ColonyTarget> targetOf)
        {
            this.items = items ?? new List<T>();
            this.targetOf = targetOf;
        }

        public int Count => items.Count;

        public int Filter(Func<ColonyTarget, bool> keep) => items.RemoveAll(item => !keep(targetOf(item)));

        public int CountKept(Func<ColonyTarget, bool> keep)
        {
            int kept = 0;
            foreach (T item in items)
            {
                if (keep(targetOf(item))) kept++;
            }
            return kept;
        }
    }

    /// <summary>The game state the rules read. Implemented against the game in ColonyGameWorld, and by fakes in tests.</summary>
    public interface IColonyWorld
    {
        /// <summary>The tile of the entity's block object, or null when it does not exist or has no position.</summary>
        ColonyTile? EntityTile(string entityId);

        /// <summary>Every tile the building would occupy, or null when that cannot be worked out.</summary>
        IReadOnlyList<ColonyTile> Footprint(ColonyPlacement placement);

        /// <summary>True for a District Crossing half, the only building allowed on the border strip.</summary>
        bool IsCrossing(string templateName);

        /// <summary>
        /// The step from a tile of the building to the tile behind it (where the other half of a crossing stands), or
        /// null when that cannot be worked out.
        /// </summary>
        ColonyTile? BackStep(ColonyPlacement placement);

        /// <summary>
        /// True when a District Crossing half stands at this tile and height (or was just allowed there), so a half
        /// placed across the border is really backed by the actor's own half. A world that cannot know (a preview,
        /// or a player's own check before the pair is placed together) answers true; the host answers for real.
        /// </summary>
        bool HasCrossingAt(ColonyTile tile, int z);
    }

    public enum ColonyRefusal
    {
        None,
        /// <summary>A named building or district belongs to another colony.</summary>
        OtherColony,
        /// <summary>A building would stand, even partly, on another colony's land.</summary>
        OutsideLand,
        /// <summary>A building other than a District Crossing would stand on the border strip.</summary>
        BorderStrip,
        /// <summary>Nothing in an area action is on the actor's land.</summary>
        NothingOwn,
        /// <summary>The building's footprint could not be worked out, so it is not allowed.</summary>
        UnknownFootprint,
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
    /// Decides whether a colony may do an action. Reads only: it never changes the world, and it changes a list
    /// event only when <c>rewrite</c> is true (the host, just before the event is replayed and broadcast).
    /// </summary>
    public static class ColonyRules
    {
        public static ColonyVerdict Judge(ColonyScope scope, int actorColony, ColonyTerritory territory,
            IColonyWorld world, bool rewrite)
        {
            if (scope == null || territory == null) return ColonyVerdict.Allow;
            switch (scope.Kind)
            {
                case ColonyScopeKind.Entities:
                    foreach (string id in scope.EntityIds)
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        ColonyTile? tile = world.EntityTile(id);
                        // A missing entity is left to the event, which already handles it.
                        if (tile == null) continue;
                        int owner = territory.OwnerOf(tile.Value);
                        if (owner != actorColony)
                            return ColonyVerdict.Refuse(ColonyRefusal.OtherColony, $"{id} at {tile.Value} is colony {owner}'s");
                    }
                    return ColonyVerdict.Allow;

                case ColonyScopeKind.Placement:
                    return JudgePlacement(scope.Placement, actorColony, territory, world);

                case ColonyScopeKind.List:
                    return JudgeList(scope.List, actorColony, territory, world, rewrite);

                default:
                    return ColonyVerdict.Allow;
            }
        }

        private static ColonyVerdict JudgePlacement(ColonyPlacement placement, int actorColony,
            ColonyTerritory territory, IColonyWorld world)
        {
            if (placement == null) return ColonyVerdict.Allow;
            IReadOnlyList<ColonyTile> footprint = world.Footprint(placement);
            if (footprint == null || footprint.Count == 0)
                return ColonyVerdict.Refuse(ColonyRefusal.UnknownFootprint, $"no footprint for {placement.TemplateName}");
            if (world.IsCrossing(placement.TemplateName))
                return JudgeCrossing(placement, footprint, actorColony, territory, world);
            foreach (ColonyTile tile in footprint)
            {
                int owner = territory.OwnerOf(tile);
                if (owner != actorColony)
                    return ColonyVerdict.Refuse(ColonyRefusal.OutsideLand, $"{placement.TemplateName} tile {tile} is colony {owner}'s");
            }
            foreach (ColonyTile tile in footprint)
            {
                if (territory.IsStrip(tile))
                    return ColonyVerdict.Refuse(ColonyRefusal.BorderStrip, $"{placement.TemplateName} tile {tile} is on the border");
            }
            return ColonyVerdict.Allow;
        }

        /// <summary>
        /// The game places a District Crossing as a pair with one click: two halves back to back. Each half must stand
        /// wholly on one colony's land. A half on the actor's own land may stand anywhere there, strip included. A
        /// half on another colony's land must stand on that colony's strip with the actor's own strip directly behind
        /// every tile, and the actor's own crossing half must really stand there, so the pair straddles the border
        /// and a lone half can never be pushed onto the other side. That half joins the other colony's district and
        /// is built by its beavers, like any building on its land.
        /// </summary>
        private static ColonyVerdict JudgeCrossing(ColonyPlacement placement, IReadOnlyList<ColonyTile> footprint,
            int actorColony, ColonyTerritory territory, IColonyWorld world)
        {
            int owner = territory.OwnerOf(footprint[0]);
            foreach (ColonyTile tile in footprint)
            {
                if (territory.OwnerOf(tile) != owner)
                    return ColonyVerdict.Refuse(ColonyRefusal.OutsideLand, $"{placement.TemplateName} would stand on both sides of the border");
            }
            if (owner == actorColony) return ColonyVerdict.Allow;
            // Crossing halves are one tile deep (checked against the game's blueprint in 1.1.2.4), so the tile behind
            // each tile of the half is one step back.
            ColonyTile? back = world.BackStep(placement);
            foreach (ColonyTile tile in footprint)
            {
                if (back == null || !territory.IsStrip(tile))
                    return ColonyVerdict.Refuse(ColonyRefusal.OutsideLand, $"{placement.TemplateName} tile {tile} is colony {owner}'s");
                var behind = new ColonyTile(tile.X + back.Value.X, tile.Y + back.Value.Y);
                if (territory.OwnerOf(behind) != actorColony || !territory.IsStrip(behind))
                    return ColonyVerdict.Refuse(ColonyRefusal.OutsideLand, $"{placement.TemplateName} tile {tile} is colony {owner}'s and not back to back with your border");
                if (!world.HasCrossingAt(behind, placement.Z))
                    return ColonyVerdict.Refuse(ColonyRefusal.OutsideLand, $"{placement.TemplateName} tile {tile} is colony {owner}'s and no crossing half of yours stands behind it");
            }
            return ColonyVerdict.Allow;
        }

        private static ColonyVerdict JudgeList(IColonyList list, int actorColony, ColonyTerritory territory,
            IColonyWorld world, bool rewrite)
        {
            if (list == null || list.Count == 0) return ColonyVerdict.Allow;
            int total = list.Count;
            Func<ColonyTarget, bool> keep = target =>
            {
                if (!target.IsEntity) return territory.OwnerOf(target.Tile) == actorColony;
                if (target.EntityId.Length == 0) return true;
                ColonyTile? tile = world.EntityTile(target.EntityId);
                // A missing entity is kept: the event skips it itself.
                return tile == null || territory.OwnerOf(tile.Value) == actorColony;
            };
            int kept = list.CountKept(keep);
            if (kept == 0) return ColonyVerdict.Refuse(ColonyRefusal.NothingOwn, $"none of {total} items are colony {actorColony}'s");
            int removed = total - kept;
            if (rewrite && removed > 0) list.Filter(keep);
            return ColonyVerdict.Kept(removed);
        }
    }
}
