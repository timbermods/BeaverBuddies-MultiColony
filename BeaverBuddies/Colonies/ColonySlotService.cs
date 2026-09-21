using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Steam;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// This computer's player, the same from one session to the next: the Steam ID when Steam is running, otherwise a
    /// random id made once and kept on this computer. It lets a save remember which colony is whose.
    /// </summary>
    public static class LocalPlayerIdentity
    {
        private const string PrefsKey = "BeaverBuddies.PlayerId";

        public static string Id
        {
            get
            {
                if (SteamOverlayConnectionService.IsSteamEnabled)
                {
                    try { return "steam:" + SteamUser.GetSteamID().m_SteamID; }
                    catch (Exception error) { Plugin.LogWarning("[Colony] Steam ID unavailable: " + error.Message); }
                }
                string id = null;
                try { id = UnityEngine.PlayerPrefs.GetString(PrefsKey, null); } catch (Exception) { }
                if (string.IsNullOrEmpty(id))
                {
                    id = "local:" + GuidPatcher.RealNewGuid().ToString("N");
                    try
                    {
                        UnityEngine.PlayerPrefs.SetString(PrefsKey, id);
                        UnityEngine.PlayerPrefs.Save();
                    }
                    catch (Exception error) { Plugin.LogWarning("[Colony] Could not keep the player id: " + error.Message); }
                }
                return id;
            }
        }

        public static string Name => Settings.PingDisplayName;
    }

    /// <summary>
    /// Who plays which colony. The saved part is the slot table (stable player id → slot). The session part says which
    /// connection (0 for the host, a guest's connection number) plays which slot; only the host decides it, when a
    /// player says hello, and sends the whole table to everyone with that hello.
    /// </summary>
    public class ColonySlotService : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton, IPostLoadableSingleton,
        IUpdatableSingleton
    {
        private static readonly SingletonKey SlotsKey = new SingletonKey("BeaverBuddies.ColonySlots");
        private static readonly PropertyKey<string> TableKey = new PropertyKey<string>("Table");

        private readonly ISingletonLoader _singletonLoader;

        public ColonySlotTable Table { get; } = new ColonySlotTable();

        // connection number -> slot, for this session.
        private readonly SortedDictionary<int, int> session = new SortedDictionary<int, int>();

        /// <summary>This computer's connection number (0 on the host); -1 until the host has answered its hello.</summary>
        public int LocalPlayer { get; private set; } = -1;

        public static ColonySlotService Instance => SingletonManager.GetSingleton<ColonySlotService>();

        public ColonySlotService(ISingletonLoader singletonLoader)
        {
            _singletonLoader = singletonLoader;
        }

        public void Load()
        {
            if (_singletonLoader.TryGetSingleton(SlotsKey, out IObjectLoader loader) && loader.Has(TableKey))
                Table.Set(ColonySlotTable.Decode(loader.Get(TableKey)));
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            if (Table.Entries.Count == 0) return;
            singletonSaver.GetSingleton(SlotsKey).Set(TableKey, ColonySlotTable.Encode(Table.Entries));
        }

        public void PostLoad()
        {
            // The host seats itself; guests are seated when their hello arrives.
            if (EventIO.Get() is ServerEventIO) SeatHost();
        }

        private void SeatHost()
        {
            int slot = Table.Resolve(LocalPlayerIdentity.Id, LocalPlayerIdentity.Name) ?? 0;
            session.Clear();
            session[ColonySession.HostPlayer] = slot;
            LocalPlayer = ColonySession.HostPlayer;
            Plugin.Log($"[Colony] The host plays slot {slot} ({Table.Entries.Count} player(s) known to this save)");
            ColonyScienceService.Instance?.RefreshToolLocks();
        }

        private bool helloSent;

        public void UpdateSingleton()
        {
            // A guest says hello once, as soon as it can act (not from inside another action's replay, where new
            // actions are not recorded).
            if (helloSent || !(EventIO.Get() is ClientEventIO) || ReplayService.IsReplayingEvents) return;
            if (ReplayEvent.GetReplayServiceIfReady() == null) return;
            helloSent = true;
            PlayerHelloEvent.Send();
        }

        /// <summary>The slot a connection plays this session, or -1 if it has not said hello.</summary>
        public int SlotOfPlayer(int player) => session.TryGetValue(player, out int slot) ? slot : -1;

        /// <summary>Someone playing this session plays this slot.</summary>
        public bool IsPresent(int slot) => session.Values.Contains(slot);

        public IEnumerable<KeyValuePair<int, int>> Session => session;

        /// <summary>Host only, before a hello is replayed: seat the player and write the tables into the event.</summary>
        public void HostSeat(PlayerHelloEvent hello)
        {
            int hostSlot = SlotOfPlayer(ColonySession.HostPlayer);
            int? slot = Table.Resolve(hello.playerId, hello.playerName);
            int seat = slot ?? Math.Max(0, hostSlot);
            session[hello.player] = seat;
            hello.table = ColonySlotTable.Encode(Table.Entries);
            hello.session = string.Join(",", session.Select(p => $"{p.Key}:{p.Value}"));
            Plugin.Log(slot.HasValue
                ? $"[Colony] Player {hello.player} ({hello.playerName}) plays slot {seat}"
                : $"[Colony] Player {hello.player} ({hello.playerName}) joins as a helper of slot {seat}: every slot is taken");
        }

        /// <summary>Every computer, as a hello replays: take the host's tables.</summary>
        public void Apply(PlayerHelloEvent hello)
        {
            Table.Set(ColonySlotTable.Decode(hello.table));
            session.Clear();
            foreach (string pair in (hello.session ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int player) && int.TryParse(parts[1], out int slot))
                    session[player] = slot;
            }
            if (hello.playerId == LocalPlayerIdentity.Id && !(EventIO.Get() is ServerEventIO))
            {
                LocalPlayer = hello.player;
                Plugin.Log($"[Colony] This computer plays slot {SlotOfPlayer(LocalPlayer)}");
                ColonyScienceService.Instance?.RefreshToolLocks();
                // A player without a colony is offered to found one now.
                SingletonManager.GetSingleton<ColonyFoundingService>()?.OfferFounding();
            }
        }
    }

    /// <summary>
    /// A guest says who it is, right after joining. The host seats it (see ColonySlotService.HostSeat) and the event,
    /// carrying the host's tables, is replayed everywhere so every computer knows every seat.
    /// </summary>
    [Serializable]
    public class PlayerHelloEvent : ReplayEvent
    {
        public string playerId;
        public string playerName;
        // Written by the host before the event is played and sent on.
        public string table;
        public string session;

        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context)
        {
            ColonySlotService.Instance?.Apply(this);
        }

        public override string ToActionString() => $"Hello from {playerName}";

        /// <summary>A guest, once it can act (see ColonySlotService.UpdateSingleton).</summary>
        public static void Send()
        {
            if (!(EventIO.Get() is ClientEventIO)) return;
            ReplayEvent.DoPrefix(() => new PlayerHelloEvent
            {
                playerId = LocalPlayerIdentity.Id,
                playerName = LocalPlayerIdentity.Name,
            });
        }
    }
}
