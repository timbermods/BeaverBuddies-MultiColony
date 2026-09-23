using System;
using System.Collections.Generic;
using System.Reflection;

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
        /// Places a building: the actor's colony must have it unlocked, and it must not join another colony's roads.
        /// Anywhere else is fine: there is no land.
        /// </summary>
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

    /// <summary>
    /// A list in an event that the rules may shorten in place, keeping the order of what is left. Each item is judged
    /// by a key: an entity id.
    /// </summary>
    public interface IColonyList<TKey>
    {
        int Count { get; }
        /// <summary>Removes every item <paramref name="keep"/> rejects and returns how many were removed.</summary>
        int Filter(Func<TKey, bool> keep);
        /// <summary>Counts the items <paramref name="keep"/> accepts without changing the list.</summary>
        int CountKept(Func<TKey, bool> keep);
    }

    public interface IColonyList : IColonyList<string> { }

    /// <summary>What an event touches, declared by the event itself (see ReplayEvent.GetColonyScope).</summary>
    public sealed class ColonyScope
    {
        public ColonyScopeKind Kind { get; private set; }
        public IReadOnlyList<string> EntityIds { get; private set; } = Array.Empty<string>();
        public ColonyPlacement Placement { get; private set; }
        public IColonyList List { get; private set; }
        /// <summary>
        /// A trading post belongs to its two partners: either may remove it. Set on demolition actions only; running a
        /// half (workers, priority) stays with its district's owner, and a crossing between one colony's own districts
        /// is that colony's.
        /// </summary>
        public bool CrossingsNeutral { get; private set; }

        public static readonly ColonyScope Global = new ColonyScope { Kind = ColonyScopeKind.Global };

        /// <summary>Null or empty ids are skipped: the event handles a missing entity itself.</summary>
        public static ColonyScope Entities(params string[] entityIds) =>
            new ColonyScope { Kind = ColonyScopeKind.Entities, EntityIds = entityIds ?? Array.Empty<string>() };

        /// <summary>Removing things: like <see cref="Entities"/>, but a Trading Post between two colonies may be removed by either.</summary>
        public static ColonyScope Demolish(params string[] entityIds) =>
            new ColonyScope { Kind = ColonyScopeKind.Entities, EntityIds = entityIds ?? Array.Empty<string>(), CrossingsNeutral = true };

        /// <summary>Beavers leave <paramref name="fromDistrictId"/> for <paramref name="toDistrictId"/>.</summary>
        public static ColonyScope Migration(string fromDistrictId, string toDistrictId) =>
            new ColonyScope { Kind = ColonyScopeKind.Migration, EntityIds = new[] { fromDistrictId, toDistrictId } };

        public static ColonyScope Place(ColonyPlacement placement) =>
            new ColonyScope { Kind = ColonyScopeKind.Placement, Placement = placement };

        public static ColonyScope Found(ColonyPlacement placement) =>
            new ColonyScope { Kind = ColonyScopeKind.Founding, Placement = placement };

        public static ColonyScope EntityList<T>(List<T> items, Func<T, string> entityIdOf, bool demolition = false) =>
            new ColonyScope { Kind = ColonyScopeKind.List, List = new ColonyList<T>(items, entityIdOf), CrossingsNeutral = demolition };
    }

    internal class ColonyList<T, TKey> : IColonyList<TKey>
    {
        private readonly List<T> items;
        private readonly Func<T, TKey> keyOf;

        public ColonyList(List<T> items, Func<T, TKey> keyOf)
        {
            this.items = items ?? new List<T>();
            this.keyOf = keyOf;
        }

        public int Count => items.Count;
        public int Filter(Func<TKey, bool> keep) => items.RemoveAll(item => !keep(keyOf(item)));

        public int CountKept(Func<TKey, bool> keep)
        {
            int kept = 0;
            foreach (T item in items)
            {
                if (keep(keyOf(item))) kept++;
            }
            return kept;
        }
    }

    internal sealed class ColonyList<T> : ColonyList<T, string>, IColonyList
    {
        public ColonyList(List<T> items, Func<T, string> idOf) : base(items, idOf) { }
    }

    /// <summary>The game state the rules read. Implemented against the game in ColonyGameWorld, and by fakes in tests.</summary>
    public interface IColonyWorld
    {
        /// <summary>The slot owning the entity (by its district), or null when it has no owner or does not exist.</summary>
        int? OwnerOf(string entityId);

        /// <summary>True for a half of a Trading Post between two colonies, one of them <paramref name="slot"/>'s.</summary>
        bool IsCrossingOf(int slot, string entityId);

        /// <summary>Whether this slot's colony may build this building (always true without separate science).</summary>
        bool IsUnlockedFor(int slot, string templateName);

        /// <summary>
        /// Why this slot's colony may not place this building here (it would join another colony's roads), or None. See
        /// <see cref="ColonyRoadRule"/>.
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
        /// <summary>The building would join another colony's roads (a path beside them, or an entrance on or beside them).</summary>
        TouchesOtherColony,
        /// <summary>A dev mode shortcut while the host's dev mode is off (host only).</summary>
        DevModeOff,
        /// <summary>Refused by the host for a reason of its own (not seated yet, a zipline the game refuses...).</summary>
        HostRefused,
        /// <summary>Founding or handing over a colony before the host's first tick, while players can still join.</summary>
        NotStartedYet,
        /// <summary>A mixed-factions game: a faction the host has not unlocked, or one the game does not have.</summary>
        FactionUnavailable,
        /// <summary>A mixed-factions game: a colony that has built something of its own faction can no longer switch.</summary>
        FactionSwitchNotAllowed,
        /// <summary>A mixed-factions game: another faction's building (each colony builds its own faction's).</summary>
        OtherFactionBuilding,
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
    /// colony that placed them), and the marks it made. A player changes only their own colony's things and things
    /// nobody owns. There is no land: a player builds and marks anywhere, as long as nothing they build joins another
    /// colony's roads (<see cref="ColonyRoadRule"/>). Whether the other player is playing makes no difference: colonies
    /// meet only at trading posts.
    ///
    /// Reads only: it changes a list event only when <c>rewrite</c> is true (the host, before replaying it).
    /// </summary>
    public static class ColonyRules
    {
        /// <summary>The actor may change something owned by <paramref name="owner"/>: its own, or nobody's.</summary>
        public static bool MayChange(int actorSlot, int? owner) => owner == null || owner.Value == actorSlot;

        /// <summary>
        /// The scope of an event that declares none (another mod's) but names one building in a public string field
        /// called entityID, as this mod's own events and MixedStorage's do: a change to that building. Null when it has
        /// no such field.
        /// </summary>
        public static ColonyScope ScopeByEntityField(object replayEvent)
        {
            if (replayEvent == null) return null;
            FieldInfo field = EntityFieldOf(replayEvent.GetType());
            return field == null ? null : ColonyScope.Entities(field.GetValue(replayEvent) as string);
        }

        private static readonly Dictionary<Type, FieldInfo> entityFields = new Dictionary<Type, FieldInfo>();

        private static FieldInfo EntityFieldOf(Type type)
        {
            lock (entityFields)
            {
                if (entityFields.TryGetValue(type, out FieldInfo known)) return known;
                FieldInfo field = type.GetField("entityID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null && field.FieldType != typeof(string)) field = null;
                entityFields[type] = field;
                return field;
            }
        }

        public static ColonyVerdict Judge(ColonyScope scope, int actorSlot, IColonyWorld world, bool rewrite)
        {
            if (scope == null) return ColonyVerdict.Allow;
            switch (scope.Kind)
            {
                case ColonyScopeKind.Entities:
                    foreach (string id in scope.EntityIds)
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        if (scope.CrossingsNeutral && world.IsCrossingOf(actorSlot, id)) continue;
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
                || (crossingsNeutral && world.IsCrossingOf(actorSlot, id))
                || MayChange(actorSlot, world.OwnerOf(id));
            return Keep(list, keep, actorSlot, rewrite);
        }

        private static ColonyVerdict Keep<TKey>(IColonyList<TKey> list, Func<TKey, bool> keep, int actorSlot, bool rewrite)
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
        /// Founding a colony and handing one over wait for the host's first tick. Until then players can still join,
        /// and a player who joins is sent the save the host started from and only what is played after they connected:
        /// a colony founded before that would be missing from their game, and one handed over would keep its old
        /// owner there, silently. After the first tick nobody can join, so nobody can miss it. Every other action at
        /// tick 0 closes joining instead (see ReplayService); these two are held back because the founding prompt
        /// invites every guest to act the moment they are seated, while others are still on their way.
        /// <para>
        /// A game started from a new game's waiting room (<paramref name="joiningClosedAtStart"/>, ColonySession) has no
        /// late joiners: everyone came in before the world was made, and joining closed at Start. Nothing waits there, so
        /// a guest founds its colony while the game is still paused (D4 of design/PRE-GAME-LOBBY-PLAN.md).
        /// </para>
        /// </summary>
        public static bool WaitsForStart(bool foundingOrHandover, int hostTicksSinceLoad, bool joiningClosedAtStart) =>
            foundingOrHandover && hostTicksSinceLoad < 1 && !joiningClosedAtStart;

        /// <summary>
        /// Who may found a colony (1.4.0-rc3): anyone without one in a separate-colonies game. In a shared game, any player
        /// but the host, who plays the shared colony: that founding splits the game into separate colonies, for good (the
        /// guest chose it from the game menu, SharedColonySplit, and confirmed it cannot be undone).
        /// </summary>
        public static bool MayFound(bool separateColonies, bool actorIsHost) => separateColonies || !actorIsHost;

        /// <summary>
        /// The game menu's Found your own colony (1.4.0-rc3): offered only to a guest (never the host), in a shared game,
        /// once seated, while they have no colony. Gone for everyone once the game is separate.
        /// </summary>
        public static bool SplitOffered(bool isGuest, bool separateColonies, bool seated, bool ownsDistrict) =>
            isGuest && !separateColonies && seated && !ownsDistrict;

        /// <summary>
        /// Whether a player may found a colony now. Once per player: only a player whose slot owns no district center
        /// yet. The save must be a separate-colonies game, or a guest must be splitting a shared one (<see cref="MayFound"/>).
        /// The spot must be free, and the new district center must not join another colony's roads. Anywhere else will
        /// do: there is no land, and no distance to keep from other colonies.
        /// </summary>
        public static ColonyVerdict JudgeFounding(bool actorHasSlot, bool actorOwnsDistrict, bool foundingAllowed,
            bool blocksValid, bool touchesOtherDistrict)
        {
            if (!actorHasSlot) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "a helper plays another player's colony");
            if (actorOwnsDistrict) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "this player already has a colony");
            if (!foundingAllowed) return ColonyVerdict.Refuse(ColonyRefusal.CannotFound, "the host plays a shared game's colony; only another player can split it");
            if (!blocksValid) return ColonyVerdict.Refuse(ColonyRefusal.Blocked, "the spot is taken or unsuitable");
            if (touchesOtherDistrict) return ColonyVerdict.Refuse(ColonyRefusal.FoundingConflict, "it would join another district's roads");
            return ColonyVerdict.Allow;
        }

        /// <summary>
        /// What a founding tells this computer's player. Every computer plays the founding at its tick, but only the
        /// founder asked for it: they hear that it worked, or that the spot changed and they can try again. Everyone
        /// else hears only that a new colony exists, and nothing of a failed try. Display only.
        /// </summary>
        /// <param name="split">The founding split a shared game (1.4.0-rc3): everyone is told the game is now separate, for
        /// good; the host that the shared colony is theirs; other players without a colony how to go on.</param>
        public static FoundingNotice FoundingNoticeFor(int localSlot, int founderSlot, bool founded, bool split = false,
            bool localIsHost = false)
        {
            if (localSlot == founderSlot) return founded ? split ? FoundingNotice.SplitDone : FoundingNotice.Done : FoundingNotice.Failed;
            if (!founded) return FoundingNotice.None;
            if (!split) return FoundingNotice.Founded;
            return localIsHost ? FoundingNotice.SplitHost : FoundingNotice.SplitOther;
        }

        /// <summary>
        /// The text a founding notice shows, as a localization key, or null for no notice. Founding.Other names the
        /// colony as {0}.
        /// </summary>
        public static string FoundingNoticeKey(FoundingNotice notice) => notice switch
        {
            FoundingNotice.Done => "BeaverBuddies.Colony.Founding.Done",
            FoundingNotice.Failed => "BeaverBuddies.Colony.Founding.Failed",
            FoundingNotice.Founded => "BeaverBuddies.Colony.Founding.Other",
            FoundingNotice.SplitDone => "BeaverBuddies.Colony.Founding.SplitDone",
            FoundingNotice.SplitHost => "BeaverBuddies.Colony.Founding.SplitHost",
            FoundingNotice.SplitOther => "BeaverBuddies.Colony.Founding.SplitOther",
            _ => null,
        };

        /// <summary>
        /// Only the founder's failed try is shown as a warning: it asks them to act (try again). A founding that
        /// worked is news, for the founder and for everyone else.
        /// </summary>
        public static bool FoundingNoticeWarns(FoundingNotice notice) => notice == FoundingNotice.Failed;
    }

    /// <summary>The notice a founding shows on one computer: see <see cref="ColonyRules.FoundingNoticeFor"/>.</summary>
    public enum FoundingNotice
    {
        None,
        /// <summary>The founder: their colony was founded.</summary>
        Done,
        /// <summary>The founder, as a warning: the spot changed before the founding's tick; try again.</summary>
        Failed,
        /// <summary>Another player: a colony was founded (a plain notice naming it).</summary>
        Founded,
        /// <summary>The founder of a colony that split a shared game: founded, and the game is now separate for good.</summary>
        SplitDone,
        /// <summary>The host, as a shared game is split: the game is now separate, and the shared colony is theirs.</summary>
        SplitHost,
        /// <summary>Another player, as a shared game is split: separate for good; they may found their own or be a steward.</summary>
        SplitOther,
    }
}
