using System;
using System.Collections.Generic;
using System.Linq;

namespace BeaverBuddies.Factions
{
    /// <summary>The host's word on the faction a founding asks for.</summary>
    public enum FactionChoiceVerdict
    {
        Allowed,
        /// <summary>No faction of that id is loaded.</summary>
        Unknown,
        /// <summary>The host has not unlocked it (D1).</summary>
        Unavailable,
    }

    /// <summary>The host's word on an untouched colony's switch to another faction (D14).</summary>
    public enum FactionSwitchVerdict
    {
        Allowed,
        /// <summary>Not a mixed-factions game.</summary>
        NotMixed,
        /// <summary>Only the colony's own seated player may switch it (not a steward, not the host for someone else).</summary>
        NotYours,
        /// <summary>The colony has no district center (found one instead).</summary>
        NoColony,
        SameFaction,
        Unknown,
        Unavailable,
        /// <summary>It has built, marked, unlocked or traded something.</summary>
        Touched,
    }

    /// <summary>What decides whether a colony is still untouched, as the game counts it.</summary>
    public readonly struct UntouchedFacts
    {
        public UntouchedFacts(int districtCenters, int otherBuildings, int marks, bool tradeOpen, int unlocks)
        {
            DistrictCenters = districtCenters;
            OtherBuildings = otherBuildings;
            Marks = marks;
            TradeOpen = tradeOpen;
            Unlocks = unlocks;
        }

        public int DistrictCenters { get; }
        /// <summary>Buildings, construction sites and paths stamped with the colony, district centers left out.</summary>
        public int OtherBuildings { get; }
        /// <summary>Planting and cutting marks of the colony.</summary>
        public int Marks { get; }
        /// <summary>An exchange offered or running at one of its Trading Posts.</summary>
        public bool TradeOpen { get; }
        /// <summary>Buildings and bot worker types it unlocked (separate science only).</summary>
        public int Unlocks { get; }

        public bool IsUntouched => OtherBuildings == 0 && Marks == 0 && !TradeOpen && Unlocks == 0;
    }

    /// <summary>
    /// The mixed-factions decisions, kept free of the game so they are checked headless (StabilityTests): which faction a
    /// founding gets, whether a colony may switch, which buildings a colony may place, what may cross a Trading Post
    /// between two factions, and which colony a handover prefers.
    /// </summary>
    public static class FactionRules
    {
        /// <summary>
        /// A founding's faction (D15): outside a mixed game the event's faction is ignored (the base faction); in one, a
        /// missing faction is the base faction and any other must be loaded and available on the host.
        /// </summary>
        public static FactionChoiceVerdict JudgeFoundingFaction(bool mixed, string faction, IReadOnlyCollection<string> known,
            IReadOnlyCollection<string> available)
        {
            if (!mixed || string.IsNullOrEmpty(faction)) return FactionChoiceVerdict.Allowed;
            if (known == null || !known.Contains(faction)) return FactionChoiceVerdict.Unknown;
            if (available != null && !available.Contains(faction)) return FactionChoiceVerdict.Unavailable;
            return FactionChoiceVerdict.Allowed;
        }

        /// <summary>The faction a founding event plays with: its own when mixed and set, else the base faction.</summary>
        public static string FoundingFaction(bool mixed, string faction, string baseFaction) =>
            mixed && !string.IsNullOrEmpty(faction) ? faction : baseFaction;

        /// <summary>Whether an untouched colony may become <paramref name="wanted"/> (D14).</summary>
        public static FactionSwitchVerdict JudgeSwitch(bool mixed, bool isSeatOwner, string current, string wanted,
            IReadOnlyCollection<string> known, IReadOnlyCollection<string> available, UntouchedFacts facts)
        {
            if (!mixed) return FactionSwitchVerdict.NotMixed;
            if (!isSeatOwner) return FactionSwitchVerdict.NotYours;
            if (facts.DistrictCenters <= 0) return FactionSwitchVerdict.NoColony;
            if (string.IsNullOrEmpty(wanted) || known == null || !known.Contains(wanted)) return FactionSwitchVerdict.Unknown;
            if (wanted == current) return FactionSwitchVerdict.SameFaction;
            if (available != null && !available.Contains(wanted)) return FactionSwitchVerdict.Unavailable;
            if (!facts.IsUntouched) return FactionSwitchVerdict.Touched;
            return FactionSwitchVerdict.Allowed;
        }

        /// <summary>
        /// Whether a colony of <paramref name="actorFaction"/> may place a building whose template is
        /// <paramref name="templateFaction"/>'s alone (null: common) (D17). Trading Posts of any faction are allowed: the
        /// host gives each half its faction.
        /// </summary>
        public static bool MayPlace(bool mixed, string templateFaction, string actorFaction, bool isTradingPost) =>
            !mixed || isTradingPost || templateFaction == null || actorFaction == null || templateFaction == actorFaction;

        /// <summary>
        /// Whether the factions allow an item to go to a colony (D2, D3): beavers only between colonies of one faction;
        /// science always (whether science trades at all is decided elsewhere); a good only if the receiving colony's
        /// faction can store it. Outside a mixed game callers pass equal factions and "stores everything".
        /// </summary>
        public static bool FactionAllows(string item, Func<string, bool> receiverStores, string giverFaction, string receiverFaction,
            string scienceId, string beaversId)
        {
            if (string.IsNullOrEmpty(item)) return true;
            if (item == scienceId) return true;
            if (item == beaversId) return giverFaction == receiverFaction;
            return receiverStores == null || receiverStores(item);
        }

        /// <summary>A beaver may cross into a colony of its own faction only (D20); a beaver of no known faction may.</summary>
        public static bool MayBeaverCross(string beaverFaction, string targetFaction) =>
            beaverFaction == null || targetFaction == null || beaverFaction == targetFaction;

        /// <summary>
        /// The colony a handover goes to (D21): the nearest of the same faction, else the nearest; the lower slot on a tie.
        /// Null when there are no candidates. A candidate at no distance (long.MaxValue: no district center on one side)
        /// is none, as in the handover's own choice.
        /// </summary>
        public static int? PreferSameFaction(IEnumerable<(int slot, long distance)> candidates, Func<int, string> factionOf,
            string fromFaction)
        {
            var list = (candidates ?? Enumerable.Empty<(int, long)>()).Where(c => c.Item2 < long.MaxValue)
                .OrderBy(c => c.Item2).ThenBy(c => c.Item1).ToList();
            if (list.Count == 0) return null;
            if (fromFaction != null && factionOf != null)
            {
                foreach (var candidate in list)
                {
                    if (factionOf(candidate.Item1) == fromFaction) return candidate.Item1;
                }
            }
            return list[0].Item1;
        }

        /// <summary>
        /// The faction switcher's arrows (the faction page's own): the next faction in <paramref name="offered"/>'s order,
        /// wrapping around; the first when the current one is not offered.
        /// </summary>
        public static string Step(IReadOnlyList<string> offered, string current, int step)
        {
            if (offered == null || offered.Count == 0) return current;
            int index = -1;
            for (int i = 0; i < offered.Count; i++) if (offered[i] == current) index = i;
            if (index < 0) return offered[0];
            int next = ((index + step) % offered.Count + offered.Count) % offered.Count;
            return offered[next];
        }

        /// <summary>
        /// Whether a player may pick a faction in a waiting room: always in a mixed new game; in a mixed save only when the
        /// colony they will play has no faction yet (they will found it); never otherwise.
        /// </summary>
        public static bool MayPickInRoom(bool mixed, bool isSave, bool colonyHasFaction) => mixed && (!isSave || !colonyHasFaction);
    }
}
