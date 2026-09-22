using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A colony's player may ask another player in the session to look after it: the steward switches into that colony
    /// (their actions count as its, their toolbar, top bar and science follow) and back to their own, the way the
    /// debug seat flip already let the host test other colonies alone. It bridges the gap between "its player is
    /// playing" and a permanent hand-over after days away: a colony looked after by a player who is in the game is
    /// never handed over for absence. The grant is saved with the game (by the steward's stable id, so it holds across
    /// sessions and hosts); which colony a player is acting as is session state, told to every computer by an action,
    /// and forgotten when the session ends. The host judges every grant and switch (ColonyStewardRules).
    /// </summary>
    public class ColonyStewards : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey StewardsKey = new SingletonKey("BeaverBuddies.ColonyStewards");
        private static readonly ListKey<string> EntriesKey = new ListKey<string>("Stewards");

        private readonly ISingletonLoader _singletonLoader;
        private readonly ColonyRulesService _colonyRulesService;

        // Per colony: the steward's stable id and the name they went by when asked.
        private readonly string[] ids = new string[ColonySlotTable.MaxSlots];
        private readonly string[] names = new string[ColonySlotTable.MaxSlots];
        // Session: connection number -> the colony that player acts as (absent: their own seat).
        private readonly SortedDictionary<int, int> acting = new SortedDictionary<int, int>();

        public static ColonyStewards Instance => SingletonManager.GetSingleton<ColonyStewards>();

        public ColonyStewards(ISingletonLoader singletonLoader, ColonyRulesService colonyRulesService)
        {
            _singletonLoader = singletonLoader;
            _colonyRulesService = colonyRulesService;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(StewardsKey, out IObjectLoader loader) || !loader.Has(EntriesKey)) return;
            foreach (string entry in loader.Get(EntriesKey))
            {
                string[] parts = entry.Split(new[] { '|' }, 3);
                if (parts.Length < 2 || !int.TryParse(parts[0], out int slot) || slot < 0 || slot >= ids.Length || string.IsNullOrEmpty(parts[1]))
                    continue;
                ids[slot] = parts[1];
                names[slot] = parts.Length > 2 ? parts[2] : "";
            }
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            List<string> entries = Enumerable.Range(0, ids.Length).Where(slot => ids[slot] != null)
                .Select(slot => $"{slot}|{ids[slot]}|{Clean(names[slot])}").ToList();
            if (entries.Count > 0) singletonSaver.GetSingleton(StewardsKey).Set(EntriesKey, entries);
        }

        private static string Clean(string name) => (name ?? "").Replace("\n", " ").Replace("|", "/");

        // ---- questions ----

        public bool HasSteward(int slot) => slot >= 0 && slot < ids.Length && ids[slot] != null;
        public string StewardIdOf(int slot) => HasSteward(slot) ? ids[slot] : null;
        public string StewardNameOf(int slot) => HasSteward(slot) ? names[slot] : null;
        public bool IsSteward(string playerId, int slot) => HasSteward(slot) && playerId != null && ids[slot] == playerId;

        /// <summary>The colony a connection acts as, or null for its own seat.</summary>
        public int? ActingSlotOf(int player) => acting.TryGetValue(player, out int slot) ? slot : (int?)null;

        /// <summary>Every connection acting as a colony other than its seat: connection number and colony.</summary>
        public IEnumerable<KeyValuePair<int, int>> Acting => acting;

        /// <summary>The colony has a steward who is in the game today (as the host last said who is).</summary>
        public bool IsLookedAfter(int slot) => HasSteward(slot) && (ColonyLifecycle.Instance?.IsPlayerPresent(ids[slot]) ?? false);

        /// <summary>The same, against a list of players in the game (the host, before it tells everyone).</summary>
        public bool IsLookedAfterBy(int slot, ICollection<string> presentPlayerIds) =>
            HasSteward(slot) && presentPlayerIds != null && presentPlayerIds.Contains(ids[slot]);

        /// <summary>Diagnostics: each colony's steward (a stable number for the id, - for none).</summary>
        public string Fingerprint() =>
            string.Join(" ", ids.Select((id, i) => $"{i}:{(id == null ? "-" : ((uint)ColonyDigest.Of(id)).ToString("x"))}"));

        /// <summary>Host only: whether it lets this grant, revocation or switch through. <paramref name="why"/> says why not.</summary>
        public bool HostAllows(ReplayEvent replayEvent, out string why)
        {
            ColonySlotService slots = ColonySlotService.Instance;
            ColonyLifecycle lifecycle = ColonyLifecycle.Instance;
            if (slots == null || lifecycle == null)
            {
                why = "no slot service";
                return false;
            }
            bool host = replayEvent.player == ColonySession.HostPlayer;
            int seat = ColonySession.SeatOfPlayer(replayEvent.player);
            string actorId = slots.PlayerIdOf(replayEvent.player);
            why = null;
            switch (replayEvent)
            {
                case StewardGrantedEvent grant:
                {
                    string ownerId = slots.Table.PlayerIdOf(grant.colonySlot);
                    bool ownerPresent = ColonyLifecycle.PresentSlots().Contains(grant.colonySlot);
                    bool known = slots.HasPlayer(grant.stewardPlayerId);
                    if (!lifecycle.OwnsDistrict(grant.colonySlot)) why = $"slot {grant.colonySlot} has no colony";
                    else if (!ColonyStewardRules.MayGrant(host, seat, grant.colonySlot, ownerPresent, grant.stewardPlayerId, ownerId, known))
                        why = $"player {replayEvent.player} (seat {seat}) may not ask that player to look after slot {grant.colonySlot}";
                    // The name every computer shows is the one the host knows, not one the sender wrote.
                    else grant.stewardName = slots.PlayerNameOf(grant.stewardPlayerId) ?? grant.stewardName;
                    return why == null;
                }
                case StewardRevokedEvent revoked:
                    if (!ColonyStewardRules.MayRevoke(host, seat, actorId, revoked.colonySlot, StewardIdOf(revoked.colonySlot)))
                        why = $"player {replayEvent.player} (seat {seat}) may not end slot {revoked.colonySlot}'s stewardship";
                    return why == null;
                case ActAsColonyEvent act:
                    if (act.colonySlot >= 0 && !lifecycle.OwnsDistrict(act.colonySlot)) why = $"slot {act.colonySlot} has no colony";
                    else if (!ColonyStewardRules.MayActAs(seat, actorId, act.colonySlot, StewardIdOf(act.colonySlot)))
                        why = $"player {replayEvent.player} (seat {seat}) does not look after slot {act.colonySlot}";
                    return why == null;
                default:
                    return true;
            }
        }

        // ---- played on every computer ----

        public void Grant(int slot, string playerId, string name, int bySlot)
        {
            if (slot < 0 || slot >= ids.Length || string.IsNullOrEmpty(playerId)) return;
            ids[slot] = playerId;
            names[slot] = name ?? "";
            ColonyDigest.Note("steward", slot, ColonyDigest.Of(playerId));
            Plugin.Log($"[Colony] {names[slot]} ({playerId}) now looks after slot {slot}'s colony (asked by slot {bySlot})");
            Tell(playerId, "BeaverBuddies.Colony.Steward.YouWereAsked", ColonyExchangeService.ColonyName(slot), warning: false);
            TellSeat(slot, "BeaverBuddies.Colony.Steward.LooksAfterYours", names[slot], warning: false);
        }

        public void Revoke(int slot, string why)
        {
            if (!HasSteward(slot)) return;
            string playerId = ids[slot], name = names[slot];
            ids[slot] = null;
            names[slot] = null;
            // Whoever was running it goes back to their own colony.
            foreach (int player in acting.Where(p => p.Value == slot).Select(p => p.Key).ToList()) SetActing(player, -1, tell: true);
            ColonyDigest.Note("steward", slot, 0);
            Plugin.Log($"[Colony] {name} ({playerId}) no longer looks after slot {slot}'s colony ({why})");
            Tell(playerId, "BeaverBuddies.Colony.Steward.YouNoLonger", ColonyExchangeService.ColonyName(slot), warning: true);
            TellSeat(slot, "BeaverBuddies.Colony.Steward.YoursAlone", name, warning: false);
        }

        /// <summary>A player now acts as <paramref name="slot"/>'s colony, or (below 0) as their own seat again.</summary>
        public void SetActing(int player, int slot, bool tell = true)
        {
            if (slot < 0) acting.Remove(player);
            else acting[player] = slot;
            Plugin.Log(slot < 0 ? $"[Colony] Player {player} acts as its own colony again" : $"[Colony] Player {player} now acts as slot {slot}");
            if (player != ColonySession.LocalPlayer) return;
            // This computer's player: the toolbar, the top bar and every refusal follow the new colony.
            ColonyScienceService.Instance?.RefreshToolLocks();
            if (!tell) return;
            string text = slot < 0
                ? RegisteredLocalizationService.T("BeaverBuddies.Colony.Steward.BackToYours")
                : string.Format(RegisteredLocalizationService.T("BeaverBuddies.Colony.Steward.NowRunning"), ColonyExchangeService.ColonyName(slot));
            Notice(text, warning: false);
        }

        // ---- notices (display only) ----

        private void Tell(string playerId, string key, string argument, bool warning)
        {
            if (playerId == null || ColonySlotService.Instance?.LocalPlayerId != playerId) return;
            Notice(string.Format(RegisteredLocalizationService.T(key), argument), warning);
        }

        private void TellSeat(int slot, string key, string argument, bool warning)
        {
            if (ColonySession.LocalSeat != slot) return;
            Notice(string.Format(RegisteredLocalizationService.T(key), argument), warning);
        }

        private void Notice(string text, bool warning)
        {
            try { _colonyRulesService.ShowNotice(text, warning); }
            catch (Exception error) { Plugin.LogWarning("[Colony] Could not show a stewardship notice: " + error.Message); }
        }
    }

    /// <summary>A colony's player (or the host, for an absent player's colony) asks another player to look after it.</summary>
    [Serializable]
    public class StewardGrantedEvent : ReplayEvent
    {
        public int colonySlot;
        public string stewardPlayerId;
        /// <summary>Written by the host, from the name it knows the player by.</summary>
        public string stewardName;

        // Judged on the host by ColonyStewardRules (see ColonyRulesService), not by ownership of an entity.
        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context) =>
            ColonyStewards.Instance?.Grant(colonySlot, stewardPlayerId, stewardName, slot);

        public override string ToActionString() => $"Asking {stewardName} to look after colony {colonySlot + 1}";
    }

    /// <summary>The owner, the steward or the host ends a stewardship.</summary>
    [Serializable]
    public class StewardRevokedEvent : ReplayEvent
    {
        public int colonySlot;

        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context) => ColonyStewards.Instance?.Revoke(colonySlot, "ended by a player");

        public override string ToActionString() => $"Ending the stewardship of colony {colonySlot + 1}";
    }

    /// <summary>
    /// A player switches which colony their actions count as: one they look after, or (below 0) their own seat.
    /// Session state only, so it leaves joining open; it waits for the first tick like a founding does.
    /// </summary>
    [Serializable]
    public class ActAsColonyEvent : ReplayEvent
    {
        public int colonySlot = -1;

        public override bool ChangesGame() => false;

        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context) => ColonyStewards.Instance?.SetActing(player, colonySlot);

        public override string ToActionString() => colonySlot < 0 ? "Acting as my own colony again" : $"Acting as colony {colonySlot + 1}";
    }
}
