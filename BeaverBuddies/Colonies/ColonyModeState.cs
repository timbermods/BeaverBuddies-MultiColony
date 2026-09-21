using System.Collections.Generic;

namespace BeaverBuddies.Colonies
{
    /// <summary>The decisions around the saved mode that need no game types, kept apart so they can be tested headless.</summary>
    public static class ColonyModeState
    {
        /// <summary>A save with the mode on but fewer than two starts is treated as a shared-colony game.</summary>
        public static bool IsUsable(bool enabled, IReadOnlyCollection<ColonyTile> starts) =>
            enabled && starts != null && starts.Count >= 2;

        /// <summary>
        /// Whether colony 2 may be founded: never once the land is divided; otherwise when the save awaits it or the
        /// host allows it.
        /// </summary>
        public static bool FoundingOpen(bool alreadyDivided, bool saveAwaitsFounding, bool hostAllows) =>
            !alreadyDivided && (saveAwaitsFounding || hostAllows);

        /// <summary>
        /// Whether automatic migration may pair two districts, given their district centers' tiles. With no territory
        /// (the mode is off) every pair is allowed, as in the game.
        /// </summary>
        public static bool SameColony(ColonyTerritory territory, ColonyTile a, ColonyTile b) =>
            territory == null || territory.OwnerOf(a) == territory.OwnerOf(b);
    }
}
