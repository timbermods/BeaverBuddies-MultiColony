using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Characters;
using Timberborn.EntitySystem;
using Timberborn.NotificationSystem;
using Timberborn.NotificationSystemUI;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Each player's notification journal lists their own colony's entries (the rule: JournalFilter). The game keeps
    /// one journal for the whole map (NotificationSaver, saved as it is and never changed here); what each computer
    /// lists of it is decided here. Display only: nothing here changes anything that is simulated.
    ///
    /// Two things the game's journal does not know are kept: the colony a beaver was in as it died (the game takes it
    /// out of its district before it posts the death) and the colony of each entry's subject as it was posted, so an
    /// entry keeps its colony once its subject is gone. A separate-colonies game saves them for the journal's entries.
    /// The journal is listed again whenever this computer's colony changes: a guest is seated only after the save has
    /// loaded (and the game has listed the whole journal), and a player can switch to a colony they look after.
    /// </summary>
    public class ColonyJournal : RegisteredSingleton, ILoadableSingleton, ISaveableSingleton, IUpdatableSingleton
    {
        private static readonly SingletonKey JournalKey = new SingletonKey("BeaverBuddies.ColonyJournal");
        private static readonly PropertyKey<string> OwnersKey = new PropertyKey<string>("Owners");

        // Past this many recorded subjects, those no longer in the journal are forgotten (a child who grew up, a death
        // that has scrolled out). The game's journal holds NotificationSaver.MaxNotifications (25).
        private const int ForgetAbove = 100;

        private readonly ISingletonLoader _singletonLoader;
        private readonly NotificationBus _notificationBus;
        private readonly NotificationSaver _notificationSaver;
        private readonly NotificationPanel _notificationPanel;
        private readonly EntityRegistry _entityRegistry;

        // subject -> the colony it was in when last seen (as it died, or as an entry about it was posted).
        private readonly Dictionary<Guid, int> owners = new Dictionary<Guid, int>();
        // The colony the panel was last listed for; -1 for every colony's (the game's own list).
        private int listedFor = -1;

        public static ColonyJournal Instance => SingletonManager.GetSingleton<ColonyJournal>();

        public ColonyJournal(ISingletonLoader singletonLoader, NotificationBus notificationBus,
            NotificationSaver notificationSaver, NotificationPanel notificationPanel, EntityRegistry entityRegistry)
        {
            _singletonLoader = singletonLoader;
            _notificationBus = notificationBus;
            _notificationSaver = notificationSaver;
            _notificationPanel = notificationPanel;
            _entityRegistry = entityRegistry;
        }

        public void Load()
        {
            if (_singletonLoader.TryGetSingleton(JournalKey, out IObjectLoader loader) && loader.Has(OwnersKey))
            {
                foreach (KeyValuePair<Guid, int> pair in JournalFilter.Decode(loader.Get(OwnersKey))) owners[pair.Key] = pair.Value;
            }
            _notificationBus.NotificationPosted += OnNotificationPosted;
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            // Separate colonies only: a shared game's save holds only what the Stability Fork's does.
            if (!ColonyModeService.IsSeparateColonies) return;
            var saved = new List<KeyValuePair<Guid, int>>();
            var seen = new HashSet<Guid>();
            foreach (Notification notification in _notificationSaver.Notifications)
            {
                if (!seen.Add(notification.Subject)) continue;
                int? owner = LiveOwner(notification.Subject) ?? Recorded(notification.Subject);
                if (owner != null) saved.Add(new KeyValuePair<Guid, int>(notification.Subject, owner.Value));
            }
            if (saved.Count > 0) singletonSaver.GetSingleton(JournalKey).Set(OwnersKey, JournalFilter.Encode(saved));
        }

        /// <summary>Whether this player's journal lists an entry.</summary>
        public bool ShouldShow(Notification notification)
        {
            Guid subject = notification.Subject;
            EntityComponent entity = Entity(subject);
            return JournalFilter.ShouldShow(ColonyViewService.Active, ColonySession.LocalSlot, subject == Guid.Empty,
                entity != null, entity != null ? DistrictOwner.OwnerOf(entity) : null, Recorded(subject));
        }

        // The two below run inside the game's tick (a death, a post): whatever happens here, the game carries on.

        /// <summary>A character is about to die, still in its district: its colony, for the death's entry.</summary>
        public void RecordDeath(Character character)
        {
            try
            {
                EntityComponent entity = character.GetComponent<EntityComponent>();
                int? owner = DistrictOwner.OwnerOf(character);
                if (entity != null && owner != null) owners[entity.EntityId] = owner.Value;
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not note a death's colony for the journal: " + error.Message);
            }
        }

        private void OnNotificationPosted(object sender, NotificationEventArgs args)
        {
            if (!ColonyModeService.IsSeparateColonies) return;
            try
            {
                Guid subject = args.Notification.Subject;
                // A dead beaver is in no district any more: what was recorded as it died stays.
                int? owner = LiveOwner(subject);
                if (owner != null) owners[subject] = owner.Value;
                if (owners.Count > ForgetAbove) ForgetAllBut(subject);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not note an entry's colony for the journal: " + error.Message);
            }
        }

        private void ForgetAllBut(Guid posted)
        {
            // The entry being posted may not be in the saved journal yet.
            var kept = new HashSet<Guid>(_notificationSaver.Notifications.Select(n => n.Subject)) { posted };
            foreach (Guid subject in owners.Keys.Where(s => !kept.Contains(s)).ToList()) owners.Remove(subject);
        }

        private EntityComponent Entity(Guid subject) => subject == Guid.Empty ? null : _entityRegistry.GetEntity(subject);

        private int? LiveOwner(Guid subject)
        {
            EntityComponent entity = Entity(subject);
            return entity != null ? DistrictOwner.OwnerOf(entity) : null;
        }

        private int? Recorded(Guid subject) => owners.TryGetValue(subject, out int slot) ? slot : (int?)null;

        public void UpdateSingleton()
        {
            // The panel lists the journal itself as it loads; after that, again whenever the colony shown changes.
            int view = ColonyViewService.Active ? ColonySession.LocalSlot : -1;
            if (view == listedFor || _notificationPanel._notificationView == null) return;
            listedFor = view;
            ListAgain();
        }

        /// <summary>Empties the panel and adds the saved journal again, through the filter (ColonyViewNotificationPatcher).</summary>
        private void ListAgain()
        {
            NotificationPanel panel = _notificationPanel;
            try
            {
                foreach (VisualElement element in panel._notifications) panel._notificationView.Remove(element);
                panel._notifications.Clear();
                panel._latestNotification = null;
                panel._latestNotificationElement.Clear();
                foreach (Notification notification in _notificationSaver.Notifications) panel.AddNotification(notification);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not list the notification journal again: " + error.Message);
            }
        }
    }

    // The game takes a dying beaver out of its district (Citizen.OnDied, on Character.Died) before Mortal posts the
    // death, so by then nothing says whose it was. Read only: the game's own method runs as it would.
    [HarmonyPatch(typeof(Character), nameof(Character.KillCharacter))]
    static class ColonyJournalDeathPatcher
    {
        static void Prefix(Character __instance)
        {
            if (!ColonyModeService.IsSeparateColonies || !__instance.Alive) return;
            ColonyJournal.Instance?.RecordDeath(__instance);
        }
    }
}
