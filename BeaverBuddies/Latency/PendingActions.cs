using BeaverBuddies.Colonies;
using BeaverBuddies.Events;
using BeaverBuddies.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.MapStateSystem;
using Timberborn.Rendering;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace BeaverBuddies.Latency
{
    /// <summary>
    /// A guest's own actions between its click and the host's answer. A guest does nothing itself: it sends each action
    /// to the host, which plays it at its next tick and sends it back, and only then does the guest's game change. Until
    /// then this marks the tiles of each pending placement or mark (light; red for a removal), so the click shows at once.
    /// The mark goes when the action comes back, when the host refuses it (with a notice saying why), or after
    /// <see cref="TimeoutSeconds"/>.
    ///
    /// It only draws: it creates no entity, changes nothing the game simulates and draws no random numbers, so it can't
    /// cause a desync. It also keeps the numbers the diagnostics report shows about the guest's delay. Bound only in a
    /// co-op game.
    /// </summary>
    public class PendingActions : RegisteredSingleton, ILoadableSingleton, IUpdatableSingleton
    {
        public const double TimeoutSeconds = 8;

        private static readonly Color AddColor = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color RemoveColor = new Color(1f, 0.35f, 0.3f, 0.55f);

        private sealed class Pending
        {
            public double SentAt;
            public int Tick;
            public float Speed;
            public List<Vector3Int> Tiles;
            public bool Removal;
        }

        private readonly AreaTileDrawerFactory _areaTileDrawerFactory;
        private readonly BuildingService _buildingService;
        private readonly EntityRegistry _entityRegistry;
        private readonly ReplayService _replayService;
        private readonly MapSize _mapSize;

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        // Tags are this game's own (the same random id the events already carry) and a count, so a guest's tag can't
        // match another computer's.
        private readonly string origin = ReplayEvent.LocalPlayerID.Substring(0, 8);
        private int sent;

        private GameObject root;
        private AreaTileDrawer addDrawer, removeDrawer;
        private bool dirty, drawFailed;

        private double nextSample = 1;
        private int waitingForTick = -1;
        private double waitStarted;
        private double lastNoticeAt = double.MinValue;

        public LatencyStats Stats { get; } = new LatencyStats();

        public static PendingActions Instance => SingletonManager.GetSingleton<PendingActions>();

        public PendingActions(AreaTileDrawerFactory areaTileDrawerFactory, BuildingService buildingService,
            EntityRegistry entityRegistry, ReplayService replayService, MapSize mapSize)
        {
            _mapSize = mapSize;
            _areaTileDrawerFactory = areaTileDrawerFactory;
            _buildingService = buildingService;
            _entityRegistry = entityRegistry;
            _replayService = replayService;
        }

        // Loadable only so the game builds it at load: it is found through SingletonManager.
        public void Load() { }

        private double Now => clock.Elapsed.TotalSeconds;

        private static bool IsGuest => EventIO.Get() is ClientEventIO;

        /// <summary>True while a guest is held at the start of a tick, waiting for the host's word for it.</summary>
        public bool IsWaitingForHost => waitingForTick >= 0;

        /// <summary>How long a guest has been held at the start of a tick; 0 when it is not.</summary>
        public double SecondsWaitingForHost => waitingForTick >= 0 ? Now - waitStarted : 0;

        /// <summary>A guest is sending one of its own actions to the host: tag it and mark its tiles.</summary>
        public void Sent(ReplayEvent replayEvent)
        {
            if (!IsGuest || replayEvent == null) return;
            replayEvent.requestId = $"{origin}:{++sent}";
            try
            {
                var entry = new Pending { SentAt = Now, Tick = _replayService.TicksSinceLoad, Speed = _replayService.TargetSpeed };
                entry.Tiles = TilesOf(replayEvent, out entry.Removal);
                pending[replayEvent.requestId] = entry;
                if (entry.Tiles.Count > 0) dirty = true;
            }
            catch (Exception error)
            {
                // The mark is a courtesy: the action itself is sent either way.
                Plugin.LogWarning("Could not mark a pending action: " + error.Message);
            }
        }

        // Both are called while events are played: they must never throw into that loop, which would end the session.

        /// <summary>An action came back from the host and was played here.</summary>
        public void Echoed(ReplayEvent replayEvent)
        {
            try
            {
                if (!IsGuest || replayEvent?.requestId == null) return;
                if (!pending.TryGetValue(replayEvent.requestId, out Pending entry)) return;
                pending.Remove(replayEvent.requestId);
                Stats.Echoed((Now - entry.SentAt) * 1000, _replayService.TicksSinceLoad - entry.Tick, entry.Speed);
                if (entry.Tiles.Count > 0) dirty = true;
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not clear a pending action: " + error.Message);
            }
        }

        /// <summary>The host refused an action: if it was this guest's, clear its mark and say why.</summary>
        public void Refused(string requestId, ColonyRefusal refusal)
        {
            try
            {
                if (!IsGuest || requestId == null) return;
                if (!pending.TryGetValue(requestId, out Pending entry)) return;
                pending.Remove(requestId);
                Stats.Refusal();
                if (entry.Tiles.Count > 0) dirty = true;
                // One click can be refused twice in a row (an unlock for lack of science, then the placement it was
                // for as not unlocked). The first reason is the one that explains it; the notice bar keeps only the
                // last, so the next second's refusals say nothing more.
                double now = Now;
                if (now - lastNoticeAt < 1) return;
                lastNoticeAt = now;
                SingletonManager.GetSingleton<ColonyRulesService>()?.ShowNotice(ColonyRulesService.RefusalMessage(refusal));
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not report a refused action: " + error.Message);
            }
        }

        /// <summary>A guest is at the start of <paramref name="tick"/> and the host's word for it has not arrived.</summary>
        public void WaitingForHost(int tick)
        {
            if (waitingForTick == tick) return;
            waitingForTick = tick;
            waitStarted = Now;
        }

        /// <summary>A guest starts <paramref name="tick"/>.</summary>
        public void TickStarted(int tick)
        {
            if (waitingForTick == tick) Stats.Waited((Now - waitStarted) * 1000);
            waitingForTick = -1;
        }

        public IEnumerable<string> ReportLines() => Stats.Lines(TimeoutSeconds);

        public void UpdateSingleton()
        {
            if (!IsGuest)
            {
                // The session ended: nothing will come back.
                if (pending.Count > 0)
                {
                    pending.Clear();
                    dirty = true;
                }
            }
            else
            {
                double now = Now;
                if (pending.Count > 0)
                {
                    foreach (string expired in pending.Where(p => now - p.Value.SentAt > TimeoutSeconds).Select(p => p.Key).ToList())
                    {
                        if (pending[expired].Tiles.Count > 0) dirty = true;
                        pending.Remove(expired);
                        Stats.NoAnswer();
                    }
                }
                if (now >= nextSample)
                {
                    nextSample = now + 1;
                    if (_replayService.TargetSpeed > 0) Stats.SampleBehind(EventIO.Get().TicksBehind, _replayService.TargetSpeed);
                }
            }
            if (dirty) Redraw();
        }

        private void Redraw()
        {
            dirty = false;
            if (drawFailed) return;
            try
            {
                if (root == null)
                {
                    if (pending.Count == 0) return;
                    root = new GameObject("BeaverBuddies_PendingActions");
                    addDrawer = _areaTileDrawerFactory.Create(AddColor, root);
                    removeDrawer = _areaTileDrawerFactory.Create(RemoveColor, root);
                }
                addDrawer.UpdateArea(OnMap(pending.Values.Where(p => !p.Removal)));
                removeDrawer.UpdateArea(OnMap(pending.Values.Where(p => p.Removal)));
            }
            catch (Exception error)
            {
                // Drawing is a courtesy: stop, and leave nothing half-drawn on the map.
                drawFailed = true;
                root?.SetActive(false);
                Plugin.LogWarning("Could not draw pending actions: " + error.Message);
            }
        }

        // The drawer only knows tiles on the map.
        private List<Vector3Int> OnMap(IEnumerable<Pending> entries)
        {
            Vector3Int size = _mapSize.TerrainSize;
            return entries.SelectMany(p => p.Tiles)
                .Where(tile => tile.x >= 0 && tile.y >= 0 && tile.x < size.x && tile.y < size.y)
                .Distinct().ToList();
        }

        private List<Vector3Int> TilesOf(ReplayEvent replayEvent, out bool removal)
        {
            removal = false;
            switch (replayEvent)
            {
                case BuildingPlacedEvent placed:
                    var placement = new Placement(placed.coordinates, placed.orientation,
                        placed.isFlipped ? FlipMode.Flipped : FlipMode.Unflipped);
                    return Ground(_buildingService.GetBuildingTemplate(placed.prefabName)?.GetSpec<BlockObjectSpec>()?.GetBlocks(placement)
                        .Select(block => block.Coordinates));
                case PlantingAreaMarkedEvent planting when planting.coordinates != null:
                    removal = planting.prefabName == PlantingAreaMarkedEvent.UNMARK;
                    // Levelled when it was recorded, so the marks sit where the plants will.
                    return planting.coordinates.ToList();
                case TreeCuttingAreaEvent cutting when cutting.coordinates != null:
                    removal = !cutting.wasAdded;
                    return cutting.coordinates.ToList();
                case ClearResourcesMarkedEvent clearing when clearing.blocks != null:
                    removal = clearing.markForDemolition;
                    return Ground(clearing.blocks.SelectMany(EntityBlocks));
                case BuildingsDeconstructedEvent deleting when deleting.entityIDs != null:
                    removal = true;
                    return Ground(deleting.entityIDs.SelectMany(id => Guid.TryParse(id, out Guid guid) ? EntityBlocks(guid) : Enumerable.Empty<Vector3Int>()));
            }
            return new List<Vector3Int>();
        }

        private IEnumerable<Vector3Int> EntityBlocks(Guid id)
        {
            BlockObject blockObject = _entityRegistry.GetEntity(id)?.GetComponent<BlockObject>();
            if (!blockObject) return Enumerable.Empty<Vector3Int>();
            BlockObjectSpec spec = blockObject.GetComponent<BlockObjectSpec>();
            return spec == null ? Enumerable.Empty<Vector3Int>() : spec.GetBlocks(blockObject.Placement).Select(block => block.Coordinates);
        }

        // The bottom block of each column: the mark lies where the object meets the ground.
        private static List<Vector3Int> Ground(IEnumerable<Vector3Int> blocks) =>
            blocks == null ? new List<Vector3Int>()
                : blocks.GroupBy(block => new Vector2Int(block.x, block.y)).Select(column => column.OrderBy(block => block.z).First()).ToList();
    }
}
