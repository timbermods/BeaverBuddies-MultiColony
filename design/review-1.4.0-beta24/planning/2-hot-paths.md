# Planning inventory 2: what the mod costs per tick and per frame at late-game size (beta23; beta24 moved lines only in `IO/ServerEventIO.cs`, updated below)

Made while planning [REVIEW-PLAN-1.4.0-beta24.md](../../REVIEW-PLAN-1.4.0-beta24.md), by a read-only sweep of the mod
at `c02637b`. Line numbers are beta23's. Costs are **estimates from reading**, not measurements. How often the game
calls a patched method was inferred; the plan's S leads measure it. Re-check lines before citing them.

**Shorthand:**
- **GC**: one `GetComponent<T>`, about 0.3 µs in the game (`STABILITY-CHANGELOG.md:1786-1794`: one per entity per
  tick on 11,464 entities took 3.2–3.5 ms).
- **GS**: one `SingletonManager.GetSingleton<T>`, a dictionary lookup by type.
- **OwnerOf**: `DistrictOwner.OwnerOf`, about 3 GC for a beaver and 4 for a finished building (`DistrictOwner.cs:89-103`).
- **LocalSlot**: `ColonySession.LocalSlot`, about 3 GS plus 2 small lookups per call (`ColonySession.cs:104-135`).
- **E**: entities, **D**: districts, **C**: crossing halves, **M**: map marks.

`ColonyReach` no longer exists: it was deleted in beta15 (`5318176`). `ColonyStamps` took its place.

## The biggest late-game risks, as read

1. **Every deletion, and every entity created with a preset id, during a tick ends that frame's ticking**
   (`Fixes/TickTimingFixes.cs:26-36`, `DeterminismService.cs:851-900`).
   - The unticked buckets are handed back, capped at one tick (`ReplayService.cs:1201-1208, 1245`).
   - A tick takes at least 1 + (buckets that delete or create) frames. At 60 fps with about 10 such buckets per tick,
     the ceiling is about 5.5 ticks/s; speed 7 wants 11.7.
2. **Walking animation rescans the whole path every frame** (`Fixes/AnimationFixes.cs:75-76` sets
   `_nextCornerIndex = 0` before `AnimatedPathFollower.Update`). The cost is proportional to path length, per walker,
   per frame.
3. **The daily colony check walks every entity twice a day on every computer**: `ColonyDiagnostics.Tick`
   (`Colonies/ColonyDiagnostics.cs:146-152`) and `ColonyPresenceEvent.Compare` (`Colonies/ColonyHandover.cs:451`).
   About 8–12 GC per building, so 50–80 ms in one tick at 20,000 entities.
4. **`ColonyRoadNetworks.OnNavMeshUpdated` runs the game's conflict walk on every navigation-mesh update**
   (`Colonies/ColonyRoadNetworks.cs:157-181`), whatever the update. The mod still sends preview notifications every
   frame (`Fixes/InstantNavMeshFix.cs:26-33`). If those reach this listener, the walk runs every frame while a path is
   dragged. **Check.**
5. **The road overlay walks every entity** every 0.5–3 s while any building tool is in hand
   (`Colonies/ColonyRoadOverlay.cs:95-130, 187-207`).
6. **Mixed factions:** `IsUntouched` walks every entity once a second while the Ctrl+T window is open
   (`Colonies/TradeOverviewPanel.cs:475-481` → `Colonies/ColonyFoundingService.cs:450-461`).
7. **Placement previews:** per path tile placed, 5 neighbour checks, each of which may loop over every district
   (`ColonyPlacementValidator` → `ColonyGameWorld.RoadOwnerAt`/`EntranceOwnerAt`).
8. **The `Time.time` native detour is installed in every game, single-player included** (`DeterminismService.cs:905-955`,
   `Plugin.cs:184`).

## Tickable singletons

| Class | Where | Per call | Scales as |
|---|---|---|---|
| `ColonyStamps` | `Colonies/ColonyStamps.cs:161-184` | Separate colonies only, tick 1 and every 16th tick (:82, :165), over the **unstamped** list (normally empty). Per waiting building, OwnerOfDistrict and `RoadOwner` (:186-204) | O(U × D). Per new entity about 4 GC (:148-152, 218-232); `Begin` walks all entities at load or at a split (:128-146) |
| `ColonyExchangeService` | `Colonies/TradingPostExchange.cs:436-448` | Separate colonies, every 8 ticks (:316). `CheckTradingPosts` (:450-484) copies the enabled crossings to a list. Per open exchange: 2 OwnerOf, 2 `FactionTrade.Allows` (a closure each), `IsTradingPost` (about 6 GC), `IsIn`. A beaver-giving side runs `BeaversToSpare`, a LINQ count over the district's adults (:517-524) | O(C) + O(open exchanges × about 20 GC). `Changed("hold")` concatenates a string per arriving load (:204) |
| `ColonyLifecycle` | `Colonies/ColonyHandover.cs:177-192` | Once a day: `HandOverDeadColonies` (:216-234), host `HostDaily` (:237-266), O(4 × D). A handover (`Transfer`, :349-391) walks every entity and notes the digest per building | O(4 × D) a day |
| `ColonyDiagnostics` | `Colonies/ColonyDiagnostics.cs:134-158` | At each new day in separate colonies, `Fingerprint()` (:165-221): every district and **every entity** (`ColonyStamp`, `DistrictBuilding`, `CrossingExchange`, `DistrictCrossingInventory`, `NeedManager` in mixed games, each hashed), plus `ColonyMarks.Fingerprint` (4 × M), science names, ledger totals | O(E × about 10 GC), done twice a day (risk 3) |
| `GateTickRunner` | `Fixes/FrameToTickFixes.cs:44-50` | Co-op. The game's gate update once per tick; each opening walks the real road graph twice (:93-132) | Per opening, O(road network) |
| `WonderTickService` | `Fixes/WonderTimingFix.cs:58-76` | Co-op. Sorts the Wonders (2 GC per comparison), 3 GC per Wonder; a closure | O(W log W) |
| `TickReplacerService` | `TickReplacerService.cs:26-30` | Co-op. The game's recovered-good-stack spawner once per tick | Game's cost |
| `ReplayService` / `TickingService` | `ReplayService.cs:929-993, 1210-1252` | Heartbeat, event reading (LINQ, :340-360), rule and mismatch checks, sending (:686-703). Per bucket: `ShouldTick`, a Steam pump at most once per ms | O(events) |

`TickWatcherService` (`TickWatcherService.cs:16-53`) is not bound anywhere: dead code.

## Updatable singletons (every frame)

- `ColonyDiagnostics` (`:112-132`): O(1), no allocation.
- `TradeOverviewPanel` (`:169-180`): O(1). While open, `RefreshLists` once a second (:184, 374-469): LINQ over all
  crossings with a GUID string each, 16 `HostMayHandOver` pairs at O(D), supplies at O(D × goods) per slot, and
  `IsUntouched` (O(E)) in mixed games.
- `ColonyWorkingHours` (`:105-126`), `ColonyJournal` (`:173-180`): flag and LocalSlot checks.
- `ColonyRoadOverlay` (`:95-130`): see risk 5.
- `ReplayService` (`:739-779`), `PendingActions` (guest, LINQ per frame while an action is pending, `Latency/PendingActions.cs:176-247`),
  `PlayerActivityService` (a lock, 10 Hz capture, `CleanName` LINQ), `ConnectionPanelService` (chat lock every frame;
  every 0.5 s a rebuild and `RefreshColors`).
- MonoBehaviours: `PlayerActivityOverlay.OnGUI`, `PingOverlay.OnGUI`, `SteamNetPump.Update`.

## Patches on per-tick or per-frame targets

### Tick loop, determinism and timing

| Target | Where | Gate | Work |
|---|---|---|---|
| `TickableEntity.Tick` prefix and postfix | `DeterminismService.cs:227-241` | **none** (every game) | A static store, twice per ticking entity per tick |
| `TickableEntityBucket.TickAll` | `:976-1114` | `EventIO.IsNull` | A `ConditionalWeakTable` lookup per bucket (a lock inside), a bucket hash; per walker about 2 GC, a transform read, the walker hash, `UpdateTransform` |
| `TickableBucketService.TickBuckets` | `ReplayService.cs:1264-1275` | failure flag, `EventIO` | 1 GS per frame |
| `Ticker.Update` | `DeterminismService.cs:779-791` | none | O(1) per frame |
| Every game random draw | `:318-376`, rule `:188-210` | **none** | 1 GS per draw (finds nothing in single-player); co-op adds a thread compare, 2 counts, the rule; a string per draw with Debug on |
| `Guid.NewGuid` | `:793-848` | none | 16 `UnityEngine.Random.Range` calls and a 16-byte array per new entity |
| `EntityService.Instantiate` | `:851-900` | co-op, preset id | Registry lookup; **ends the frame's ticking** (risk 1) |
| `EntityService.Delete` postfix | `Fixes/TickTimingFixes.cs:26-36` | co-op, in a tick or replay | 1 GS; **ends the frame's ticking** |
| `ParameterProvider.GetParameters` | `DeterminismService.cs:456-520` | none | A set lookup per component injected |
| "Not gameplay" markers | `:525-691` | none | 2 dictionary operations per call; `SoundEmitter.Update` runs per emitter per frame |
| `Time.time` detour | `:905-955` | installed always | Native-to-managed call per `Time.time` read |
| `MovementAnimator.Update` replacement | `Fixes/AnimationFixes.cs:29-108` | `EventIO.IsNull` | Per animated character per frame: 1 GS, 1 GC, `InterpolatedTime`, then the full-path rescan (risk 2) |
| `TimbermeshAnimator.UpdateAnimation` | `Fixes/WonderTimingFix.cs:549-580` | fast path | Per animated model per frame; a short list search while a Wonder animates |
| `NavigationSynchronizer` | `Fixes/InstantNavMeshFix.cs:23-49` | co-op | Instant navigation processing at the tick |
| Gates, gate conflicts, automation frame path, crossing caches | `Fixes/FrameToTickFixes.cs:53-197` | co-op | Blocking the automation frame path saves work |
| `WaterSource.Tick`, `FinishParallelTick` | `Fixes/WaterSourceFix.cs:42-70` | co-op binding | 1 GS and a list add per source per tick |
| `WaterSourceRegistry.UpdateThreadSafeRegistry` | `Fixes/WaterSourceOrderFix.cs:12-48` | co-op | Sort O(S log S); strings per coordinate with Debug on |
| `WaterDepthStrengthModifier` transpiler | `Fixes/WaterSourceTimingFix.cs:39-46` | always | 1 GS per source per tick |
| `TickOnlyArrayService.AllowEdit` | `Fixes/TickOnlyArrayFix.cs:12-28` | none | 2 static reads (one flag, `IsSavingDeterministically`, is never set) |
| `DateTime.ToString(string)` prefix | `GameSaveHelper.cs:32-41` | **none, process-wide** | Every formatted date, every log timestamp; its flag is never set: dead weight |
| `DemolishJobProvider.GetJob` | `Fixes/DemolitionSelectionFix.cs:26-73` | co-op | O(jobs) per search |
| Detailed-logging patches | `DesyncDetecter/DesyncPatches.cs:24-363` | `Settings.Debug` | Off: O(1). **On: strings per beaver per tick, a stack trace per trace, whole-map moisture and water hashes and a copy of up to 64 MB every tick (`DesyncDetecterService.cs:143-288`), every trace sent to guests every tick (`ReplayService.cs:933-943`).** It can be turned on mid-session from the desync dialog (`Events/ConnectionEvents.cs:173-177`) |

### Colony rules (gate: `ColonyModeService.IsSeparateColonies`, a static read)

- **Working hours** (`Colonies/ColonyWorkingHours.cs:136-180`): per beaver and per workplace every tick, 1 GS, 2
  timer reads (the profiler spot) and OwnerOf. About 1–1.5 ms per tick at 1,000 callers; the profiler's own timing
  is about as large as the work. `Chronometer.Sample` (:190-202) the same per chronometer.
- **Yielder searches** (`Colonies/ColonySeparationPatches.cs:86-114`): per search 2 timer reads, OwnerOf, a LINQ
  `Where` (closure and iterator); per candidate `MayTake` (:61-71): 1 GC, 1 GS, 2 mark lookups.
- **`PlantingSpotFinder.CanPlantAt`** (:117-128): per spot, the planter's OwnerOf (recomputed), 1 GS, a mark lookup.
- **Jobs:** construction (:138-163, about 6 GC per attempt), demolition (:167-179, about 7 GC), good stacks (:183-194).
- **Migration** (`Colonies/ColonySimulationPatches.cs:34-82`): O(connected districts) × 4 GC.
- **Crossings** (`Colonies/ColonyTrading.cs:79, 218-286`): `TradesOnlyByExchange` first (1–5 GC); the trading-post
  path of `TryExport` adds `StillToBring` (6–8 GC).
- **Placement previews** (`Colonies/ColonyPlacementValidator.cs:23-69`, `ColonyGameWorld.cs:133-174`,
  `ColonyRoadRule.cs:71-106`): per preview block per frame (risk 7).
- **Marks** (`Colonies/ColonyMarks.cs:134-206`): LINQ and lookups per tile while marking; the unset postfix at :204 has
  **no gate**.
- **Science** (`Colonies/ColonyScienceService.cs:372-545`): `Manufactory.IncreaseProductionProgress` in **every game**
  (2 thread-static accesses per workshop per tick; OwnerOf too with separate science); `WorkerKey` concatenates a
  string per bot worker-type check (:202-230).
- **What each player sees** (`Colonies/ColonyView.cs`): status patches (:295-311, about 8 lookups and 3–6 GC per visible
  status), batch rows (:256-276, LocalSlot per row), notifications (:341-359), top bar and population (:197-232).
- **Recording patches:** `DoEntityPrefix` allocates 2 closures before any check (`Events/ReplayEvent.cs:172-240`);
  `UniversalPrefix` builds a string per call. `DistrictDistributionSetting.AddGoodDistributionSetting`
  (`Events/BatchEvents.cs:259-271`) is ungated: O(D × goods) allocations at load.

## Mixed factions (gate: `MixedFactions.IsOn`; nothing per frame or per entity when off)

- `SimFactionOf` (`Factions/ColonyFactionService.cs:117-123`): 1 GC, else 1 GS + 1 GC + a template lookup.
  `DisplayFactionOf` adds OwnerOf; `FactionIds` does `ToList` per call.
- `MixedFactions.Spec` (`MixedFactions.cs:160-161`): LINQ `FirstOrDefault` with closures, **allocates every call**; used
  by texture, avatars, empty slots, driveways, paths.
- Needs (`FactionCharacterPatches.cs:31-45`): per creation or load only (cached `NeedsFor`); `GetMaxWellbeing`
  (:52-72) per call 1 GS + 1 GC + `SimFactionOf`.
- Loading (`CharacterFaction.cs:55-59, 118-135`): a new `EntityLoader` per loaded entity.
- **`YieldRemovingBuilding.IsAllowed` postfix** (`FactionBuildingRules.cs:84-95`): 1 GS + `SimFactionOf` + 2 set lookups
  per call, the building's faction recomputed every time. If the game calls it per candidate yielder during searches,
  this is per candidate. **Profile it.**
- Trade (`FactionTrade.cs:21-43`): `Allows` allocates a closure per call (2 per open exchange every 8 ticks; per good in
  the picker grid, `TradingPostFragment.cs:900, 906`); `BeaverMayJoin` 1 GC per adult in `BeaversToSpare`.
- **`PaintPath`** (`FactionModelPatches.cs:30-56`): `Spec` plus 12 × (string concat + `FindChild` +
  `GetComponentInChildren`) per path, on every path's `Awake` (previews included) and on every stamp or init
  (`ColonyStamps.cs:55, 64`). A handover repaints every path of the colony in one tick.
- The daily fingerprint adds 1 GC + `SimFactionOf` + a string hash per character (`ColonyDiagnostics.cs:183-189`).

## Network and saves

- **Heartbeat** (`ReplayService.cs:74-111`): `digest`, `changes`, `entityOrderHash`, `walkerPositionHash`,
  `hostSpeed`. Built every tick, wrapped in a `GroupedEvent`, serialised with `TypeNameHandling.All`
  (`IO/FileIO.cs:56-72`), hashed and gzipped once (`TimberNet/TimberServer.cs:867-872`). About 0.4–0.6 KB before gzip;
  grows with tick rate, not colony size (11.7 ticks/s at speed 7; the boost goes to 30).
- **`HasEventsForTick`** (`TimberNet/TimberNetBase.cs:808-812`): `Update()` and a LINQ `Any` per call; grows when a
  guest is far behind.
- **`ColonyDigest.Note`** (`Colonies/ColonyDigest.cs:72-86`): no allocation; a 16,384-entry ring. Bursts: per tile of
  area marking, per building in a handover. Steady volume: science adds and exchange holds.
- **Daily fingerprint:** 300–600 characters, built twice a day everywhere, sent once by the host.
- **`ColonyMarks.Save`** (`Colonies/ColonyMarks.cs:58-69, 166-169`): 3 LINQ `OrderBy` and a string per mark at every
  save, autosaves included.
- **Rehost** (`Connect/RehostingService.cs:56-102`, `ServerHostingUtils.cs:123-212`, `IO/ServerEventIO.cs:48-57, 238` (beta24 lines)):
  1. an instant save on the game thread;
  2. waits for the running tick and the water and soil threads;
  3. restores the Wonder poses;
  4. reads the whole save into memory on the game thread, with 2 copies;
  5. keeps the bytes for the whole session ("TODO remove map");
  6. hashes every byte on the game thread (`DeterminismService.cs:301-309`).
- **Sending a save** (`TimberNet/TimberServer.cs:814-841`): on the join task; direct TCP in 32 KB chunks paced at 1 MB/s
  (`TimberNetBase.cs:380-408`, `TCPClientWrapper.cs:15-16`); Steam unpaced. The guest hashes the save 4 times (twice
  on the receive thread, twice on the game thread in `LoadMap`).
- **`LargeColonySpeedLimit`** measures nothing itself: it intercepts `SpeedManager.ChangeSpeedScale`, which the game's
  `GameSpeedThrottler` calls as the population grows (at about 350 beavers, speed 7 ran at 3.4:
  `STABILITY-CHANGELOG.md:2212-2216`). The setting to remove it is off by default.

## Dead code seen on the way

`TickWatcherService` (not bound), the `DateTime.ToString` prefix (flag never set), `IsSavingDeterministically` (never
set), `DeterminismPatcher.PatchDeterminism`, `WaterInputDepthAction`.
