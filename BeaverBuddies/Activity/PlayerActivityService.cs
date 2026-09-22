using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeaverBuddies.IO;
using Timberborn.Buildings;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.SceneLoading;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainQueryingSystem;
using TimberNet;
using UnityEngine;

namespace BeaverBuddies.Activity
{
    public sealed class RemoteActivity
    {
        public PlayerActivity State;
        public float LastSeen;
        public Vector3 CursorFrom, CursorTo;
        public float CursorChanged;
        /// <summary>The color the other player chose for themselves.</summary>
        public Color AdvertisedColor;
        public string Label;
        /// <summary>Key into the saved per-player styles; see <see cref="PlayerCursorPreferences.KeyFor"/>.</summary>
        public string StyleKey;
        public EntityComponent Selected, Editing;
        public Color HighlightColor;
        public readonly Highlighter Highlighter = new Highlighter();
        /// <summary>The label with what the player is doing in front of it, made once per label (drawn every frame).</summary>
        public string EditingLabel, ViewingLabel, SelectedLabel;

        string cachedHex;
        Color cachedColor;

        public int PlayerId => State.PlayerId;
        public string Name => State.Name;

        public Vector3 CursorPosition(float now) => Vector3.Lerp(CursorFrom, CursorTo, Mathf.Clamp01((now - CursorChanged) / .1f));

        /// <summary>The style's color override if there is one, otherwise the player's own color.</summary>
        public Color ResolveColor(PlayerCursorStyle style)
        {
            if (style.ColorHex == null) return AdvertisedColor;
            if (!string.Equals(style.ColorHex, cachedHex, StringComparison.Ordinal))
            {
                cachedHex = style.ColorHex;
                cachedColor = ColorUtility.TryParseHtmlString("#" + style.ColorHex, out var parsed) ? parsed : AdvertisedColor;
            }
            return cachedColor;
        }
    }

    /// <summary>What the settings panel needs to know about a connected player.</summary>
    public readonly struct PlayerCursorEntry
    {
        public readonly int PlayerId;
        public readonly string Name, Label, StyleKey;
        public readonly Color AdvertisedColor;
        public PlayerCursorEntry(RemoteActivity player)
        {
            PlayerId = player.PlayerId; Name = player.Name; Label = player.Label;
            StyleKey = player.StyleKey; AdvertisedColor = player.AdvertisedColor;
        }
    }

    // This service reads presentation state only. It never selects an entity, mutates a building,
    // calls simulation RNG, or records replay events.
    public sealed class PlayerActivityService : RegisteredSingleton, IPostLoadableSingleton, IUpdatableSingleton, IResettableSingleton
    {
        public const string PreferencesFileName = "BeaverBuddiesCursorStyles.json";

        static PlayerCursorPreferences preferences;
        /// <summary>Saved per-player cursor styles, shared by the overlay and the settings panel.</summary>
        public static PlayerCursorPreferences Preferences =>
            preferences ??= new PlayerCursorPreferences(Path.Combine(Application.persistentDataPath, PreferencesFileName));

        readonly InputService input;
        readonly CameraService camera;
        readonly TerrainPicker terrain;
        readonly SelectableObjectRaycaster raycaster;
        readonly EntitySelectionService selection;
        readonly EntityRegistry entities;
        readonly LoadingScreen loading;
        readonly Dictionary<int, RemoteActivity> remote = new Dictionary<int, RemoteActivity>();
        readonly List<int> expired = new List<int>();
        TimberNetBase net;
        GameObject overlayHost;
        bool loaded, suspended, failed, keysDirty;
        float nextSend, editingUntil;
        string editingId = "";

        // What was last sent, so nothing goes out while nothing changed (ten frames a second, each a raycast and a
        // few strings, went out before whether the player had moved or not). One still goes out every second: the
        // others forget a player a few seconds after their last frame (PlayerActivity.LifetimeSeconds).
        const float KeepAliveSeconds = 1f;
        PlayerActivity lastSent;
        float lastSentAt = -100;
        // The last cursor ray and what it hit: while the mouse and the camera stand still the ray is the same, and so
        // is the spot, without asking the scene again (once a second it is asked anyway, in case the ground changed).
        Ray lastRay;
        Vector3 lastHit;
        bool lastVisible, haveLastRay;
        float lastRaycastAt = -100;
        // The strings a frame carries, made again only when what they say changes.
        Color pingColor;
        string pingHex = "";
        Guid selectedGuid;
        string selectedText = "";

        public IEnumerable<RemoteActivity> RemotePlayers => remote.Values;

        /// <summary>The same players, for the overlay, which draws them every frame: no boxed enumerator.</summary>
        public Dictionary<int, RemoteActivity>.ValueCollection RemotePlayerValues => remote.Values;

        public int RemotePlayerCount => remote.Count;

        /// <summary>Incremented whenever the set of connected players or their style keys change.</summary>
        public int PlayersVersion { get; private set; }

        public PlayerActivityService(InputService input, CameraService camera, TerrainPicker terrain,
            SelectableObjectRaycaster raycaster, EntitySelectionService selection, EntityRegistry entities, LoadingScreen loading)
        {
            this.input = input; this.camera = camera; this.terrain = terrain; this.raycaster = raycaster;
            this.selection = selection; this.entities = entities; this.loading = loading;
        }

        public void PostLoad()
        {
            loaded = true;
            overlayHost = new GameObject("BeaverBuddies_PlayerActivity");
            var overlay = overlayHost.AddComponent<PlayerActivityOverlay>();
            overlay.Service = this; overlay.CameraService = camera;
            loading.LoadingScreenEnabled += OnLoading;
        }
        void OnLoading(object sender, EventArgs args) => Reset();

        public void Reset()
        {
            loaded = false;
            loading.LoadingScreenEnabled -= OnLoading;
            net?.ClearActivity();
            net = null;
            ClearRemote(); editingId = "";
            if (overlayHost != null) UnityEngine.Object.Destroy(overlayHost);
            overlayHost = null;
        }

        void ClearRemote()
        {
            foreach (var player in remote.Values)
            {
                try { player.Highlighter.UnhighlightAllSecondary(); }
                catch (Exception error) { Plugin.LogWarning("Could not clear a remote highlight: " + error.Message); }
            }
            if (remote.Count > 0) { PlayersVersion++; }
            remote.Clear();
        }

        static TimberNetBase CurrentNetwork() => EventIO.Get() is ServerEventIO host ? host.NetBase :
            EventIO.Get() is ClientEventIO guest ? guest.NetBase : null;

        /// <summary>The style used to draw this player's cursor, selection outline and labels.</summary>
        public PlayerCursorStyle StyleOf(RemoteActivity player) => Preferences.Get(player.StyleKey);

        public Color ColorOf(RemoteActivity player) => player.ResolveColor(StyleOf(player));

        /// <summary>
        /// The color this player's cursor is drawn in for you right now: the one you set for them, else the
        /// one they chose. False if they have no cursor (they left, or player activity is off).
        /// </summary>
        public bool TryGetCursorColor(int playerId, out Color color)
        {
            if (remote.TryGetValue(playerId, out var player) && player.State != null) { color = ColorOf(player); return true; }
            color = default;
            return false;
        }

        /// <summary>
        /// Where a player is: their cursor on the map, else what they have selected (a world position). False when
        /// neither is known (they left, their cursor is off the map, or player activity is off).
        /// </summary>
        public bool TryLocate(int playerId, out Vector3 point)
        {
            point = default;
            if (!remote.TryGetValue(playerId, out var player) || player.State == null) return false;
            if (player.State.CursorVisible)
            {
                point = player.CursorTo;
                return true;
            }
            var selected = player.Selected;
            if (!selected || selected.Deleted) return false;
            point = selected.Transform.position;
            return true;
        }

        /// <summary>Connected players in a stable order (host first), for the settings panel.</summary>
        public List<PlayerCursorEntry> Players() =>
            remote.Values.OrderBy(p => p.PlayerId).Select(p => new PlayerCursorEntry(p)).ToList();

        public void UpdateSingleton()
        {
            if (!loaded || failed) return;
            try { UpdateActivity(); }
            catch (Exception error)
            {
                // An optional overlay must not break replay or crash the simulation.
                failed = true;
                Reset();
                Plugin.LogWarning("Player activity display disabled for this scene: " + error.Message);
            }
        }

        void UpdateActivity()
        {
            var current = CurrentNetwork();
            if (!ReferenceEquals(current, net))
            {
                net?.ClearActivity();
                ClearRemote(); net = current; editingId = ""; nextSend = 0; lastSent = null;
            }
            bool hide = !Settings.PlayerActivityEnabled || !ReplayService.IsLoaded || ReplayService.HasReplayFailure;
            if (net == null || net.IsStopped || hide)
            {
                ClearRemote();
                net?.ClearActivity();
                if (!suspended && net != null && !net.IsStopped) net.SendActivity(HiddenState());
                suspended = true;
                return;
            }
            suspended = false;
            float now = Time.unscaledTime;
            foreach (var state in net.TakeActivity()) Apply(state, now);
            expired.Clear();
            foreach (var pair in remote)
            {
                if (now - pair.Value.LastSeen > PlayerActivity.LifetimeSeconds) expired.Add(pair.Key);
            }
            foreach (int id in expired) { remote[id].Highlighter.UnhighlightAllSecondary(); remote.Remove(id); keysDirty = true; }
            if (keysDirty) RefreshStyleKeys();
            if (now < nextSend) return;
            // No catch-up bursts at low FPS; wall-clock rate is independent of the simulation speed.
            nextSend = now + .1f;
            PlayerActivity captured = Capture(now);
            if (now - lastSentAt < KeepAliveSeconds && captured.SameAs(lastSent)) return;
            lastSent = captured;
            lastSentAt = now;
            net.SendActivity(captured);
        }

        string PingHex()
        {
            Color color = Settings.PingColorValue;
            if (color != pingColor || pingHex.Length == 0)
            {
                pingColor = color;
                pingHex = ColorUtility.ToHtmlStringRGB(color);
            }
            return pingHex;
        }

        PlayerActivity HiddenState() => new PlayerActivity(0, Settings.PingDisplayName, PingHex(), false, 0, 0, 0);

        PlayerActivity Capture(float now)
        {
            if (!Application.isFocused) return HiddenState();
            var selected = selection.SelectedObject;
            var entity = selected ? selected.GetComponent<EntityComponent>() : null;
            string selectedId = "";
            if (entity && !entity.Deleted)
            {
                if (entity.EntityId != selectedGuid || selectedText.Length == 0)
                {
                    selectedGuid = entity.EntityId;
                    selectedText = selectedGuid.ToString();
                }
                selectedId = selectedText;
            }
            Vector3 position = Vector3.zero;
            Vector2 mouse = input.MousePosition;
            bool visible = !input.MouseOverUI && mouse.x >= 0 && mouse.y >= 0 && mouse.x < Screen.width && mouse.y < Screen.height;
            if (visible)
            {
                Ray ray = camera.ScreenPointToRayInWorldSpace(mouse);
                if (haveLastRay && now - lastRaycastAt < KeepAliveSeconds && ray.origin == lastRay.origin && ray.direction == lastRay.direction)
                {
                    // The same ray as last time (the mouse and the camera stood still): the same spot.
                    visible = lastVisible;
                    position = lastHit;
                }
                else
                {
                    if (raycaster.TryHitSelectableObjectIncludeTerrainStump(ray, out _, out var hit)) position = hit.point;
                    else
                    {
                        var ground = terrain.PickTerrainCoordinates(camera.ScreenPointToRayInGridSpace(mouse));
                        if (ground.HasValue) position = CoordinateSystem.GridToWorld(ground.Value.Intersection);
                        else visible = false;
                    }
                    lastRay = ray; haveLastRay = true; lastRaycastAt = now;
                    lastVisible = visible; lastHit = position;
                }
            }
            if (now >= editingUntil) editingId = "";
            return new PlayerActivity(0, Settings.PingDisplayName, PingHex(),
                visible, position.x, position.y, position.z, selectedId, editingId);
        }

        // Called only for a locally initiated building command, never during event replay/ticks.
        public static void NotifyLocalEdit(string entityId)
        {
            if (DeterminismService.IsTicking || ReplayService.IsReplayingEvents || !Settings.PlayerActivityEnabled) return;
            var service = SingletonManager.GetSingleton<PlayerActivityService>();
            if (service == null || !service.loaded || service.failed) return;
            service.editingId = entityId;
            service.editingUntil = Time.unscaledTime + 3f;
        }

        EntityComponent Resolve(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return null;
            var entity = entities.GetEntity(guid);
            return entity && !entity.Deleted ? entity : null;
        }

        void Apply(PlayerActivity state, float now)
        {
            if (!loaded || failed) return;
            try
            {
                if (!remote.TryGetValue(state.PlayerId, out var player))
                {
                    if (remote.Count >= PlayerActivity.MaxPlayers) return;
                    player = new RemoteActivity(); remote.Add(state.PlayerId, player);
                    keysDirty = true;
                }
                if (player.State != null && player.State.Name != state.Name) keysDirty = true;
                Vector3 target = new Vector3(state.X, state.Y, state.Z);
                player.CursorFrom = player.State?.CursorVisible == true && state.CursorVisible ? player.CursorPosition(now) : target;
                player.CursorTo = target; player.CursorChanged = now; player.LastSeen = now;
                // A player still on the default Ping Color gets a color of their own by player number (see PlayerColors).
                ColorUtility.TryParseHtmlString("#" + PlayerColors.Effective(state.Color, state.PlayerId), out var advertised);
                player.State = state;
                player.AdvertisedColor = advertised;
                if (player.Label == null || keysDirty)
                {
                    // The name is part of the style key, so keysDirty is set whenever it changes (above).
                    player.Label = state.Name + (state.PlayerId == 0 ? " (Host)" : " (P" + state.PlayerId + ")");
                    player.EditingLabel = "Editing: " + player.Label;
                    player.ViewingLabel = "Viewing: " + player.Label;
                    player.SelectedLabel = "Selected: " + player.Label;
                }
                if (keysDirty) RefreshStyleKeys();
                Color color = ColorOf(player);
                var selected = Resolve(state.Selection);
                if (player.Selected != selected || player.HighlightColor != color) player.Highlighter.UnhighlightAllSecondary();
                player.Selected = selected; player.Editing = Resolve(state.Editing);
                player.HighlightColor = color;
                // Secondary highlights leave the local player's primary selection/hover color intact.
                if (selected && selected.HasComponent<HighlightableObject>()) player.Highlighter.HighlightSecondary(selected, color);
            }
            catch (Exception error)
            {
                failed = true; Reset();
                Plugin.LogWarning("Player activity display disabled for this scene: " + error.Message);
            }
        }

        // Players are remembered by name; two connected players with the same name are told apart by number.
        void RefreshStyleKeys()
        {
            keysDirty = false;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in remote.Values)
            {
                if (player.State == null) continue;
                counts[player.Name] = counts.TryGetValue(player.Name, out int n) ? n + 1 : 1;
            }
            foreach (var player in remote.Values)
            {
                if (player.State == null) continue;
                player.StyleKey = PlayerCursorPreferences.KeyFor(player.Name, player.PlayerId, counts[player.Name] > 1);
            }
            PlayersVersion++;
        }
    }
}
