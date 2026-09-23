using BeaverBuddies.Colonies;
using System;
using Timberborn.BaseComponentSystem;

namespace BeaverBuddies.Factions
{
    /// <summary>What a colony may look for in a mixed-factions game (D3): what it may receive, science and beavers included.</summary>
    public static class ColonyWishes
    {
        public static bool MayWish(int slot, string item) => FactionTrade.ColonyHas(slot, item);
    }

    /// <summary>
    /// What the factions allow across a Trading Post (D2, D3, D20), by colony: the form, the picker, the wishlist and the
    /// exchange's own checks all ask here. Every answer comes from saved state (each colony's faction, a beaver's own) and
    /// the catalog, so the checks every computer makes as an exchange is played agree. Outside a mixed game: everything.
    /// </summary>
    public static class FactionTrade
    {
        /// <summary>Whether <paramref name="item"/> may go from colony <paramref name="giver"/> to <paramref name="receiver"/>.</summary>
        public static bool Allows(string item, int giver, int receiver)
        {
            if (!MixedFactions.IsOn || string.IsNullOrEmpty(item)) return true;
            string giverFaction = ColonyFactionService.FactionOfSlot(giver);
            string receiverFaction = ColonyFactionService.FactionOfSlot(receiver);
            FactionCatalog catalog = FactionCatalog.Instance;
            Func<string, bool> stores = catalog == null ? null : (Func<string, bool>)(good => catalog.HasGood(receiverFaction, good));
            return FactionRules.FactionAllows(item, stores, giverFaction, receiverFaction, ExchangeTerms.Science, ExchangeTerms.Beavers);
        }

        /// <summary>The two colonies play different factions (a mixed game only).</summary>
        public static bool BetweenFactions(int a, int b) =>
            MixedFactions.IsOn && a >= 0 && b >= 0 && ColonyFactionService.FactionOfSlot(a) != ColonyFactionService.FactionOfSlot(b);

        /// <summary>Whether goods of <paramref name="item"/> are this colony's faction's to have (always outside a mixed game).</summary>
        public static bool ColonyHas(int slot, string item) =>
            !MixedFactions.IsOn || item == ExchangeTerms.Science || item == ExchangeTerms.Beavers
            || FactionCatalog.Instance == null || FactionCatalog.Instance.HasGood(ColonyFactionService.FactionOfSlot(slot), item);

        /// <summary>A beaver may join colony <paramref name="target"/>: always, unless a mixed game's beaver is of another faction.</summary>
        public static bool BeaverMayJoin(BaseComponent beaver, int target) =>
            !MixedFactions.IsOn || beaver == null
            || FactionRules.MayBeaverCross(beaver.GetComponent<CharacterFaction>()?.FactionId, ColonyFactionService.FactionOfSlot(target));
    }
}
