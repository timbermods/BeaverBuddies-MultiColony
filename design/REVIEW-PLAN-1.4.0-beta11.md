# Review plan: desyncs and network, Timber Together 1.4.0-beta11

**Status:** approved 2026-09-22 (two review agents besides the main one, fixes implemented, released as 1.4.0-beta12).
Findings: [REVIEW-FINDINGS-1.4.0-beta11.md](REVIEW-FINDINGS-1.4.0-beta11.md).

## 0. Base, and what the brief got wrong

**Base.** Worktree HEAD `5fc1c05` is tag `v1.4.0-beta11` (`29cad83`) plus one commit that touches only `docs/*.html`.
`origin/trading-exchange` and `origin/main` have nothing newer. Game: Timberborn 1.1.2.4. Decompiled sources are
regenerated into the scratchpad per session.

**The brief describes beta9.** beta10 and beta11 have since changed the following; I checked each in the code:

| Brief says | beta11 code |
|---|---|
| Only `s0` of the random state is compared | All four words are compared. Each event carries `randomStateHashBefore` (`ReplayService.cs:636-641`), and a mismatch stops the game. The heartbeat also carries the entity-order hash and the walker-position hash, but a mismatch there is **only logged** (`ReplayService.cs:406-411`). |
| Recording prefixes run at the default priority | All ~65 run at `Priority.First`, and `RuntimeChecks/RecordingPriorityChecks.cs` enforces it. It has gaps, listed in R13. |
| `Thread.Sleep` between chunks on the game thread | Only the save sent to a joiner is paced, and it goes out on the joiner's own thread (`TimberNetBase.cs:356-391`). |
| Version-skew risks (older host, missing fields) | Out of scope: joining refuses any difference in the DLL MVID or the blueprint digest, so every computer runs the same build. |

**The premise the whole design rests on.** Every computer loads the same save bytes and the same RNG seed (a hash of
those bytes), and joining closes at tick 1. The review has to test that premise, not assume it. R1, R2 and R6 are
possible holes in it.

---

## 1. Architecture map

```
 UI click ──Harmony prefix (Priority.First)──► ReplayEvent.DoPrefix
                                                 │  RefuseLocally / HostStartGate.TryHold
                                                 ▼
             host: eventsToPlay queue            guest: send now (requestId tag), never play locally
                                                 │
 TimberNet  TCP (NoDelay, sync writes on game thread) │ Steam (ReliableNoNagle, pumped on game thread)
            gzip(JSON, TypeNameHandling.All), length-framed; activity/chat/status share the stream
                                                 ▼
 host TimberServer: stamp player, stamp tick = host TickCount at game-thread Update
                                                 ▼
 Ticker.Update ─► TickableBucketService.TickBuckets (replaced: TickingService)
   bucket 0 of tick T: FinishParallelTick ─► ReplayService.DoTick
       ticksSinceLoad++ (drives patched Time.time, traces, hashes); host closes joining at T=1
       host: Heartbeat{colony digest, entity/walker hash of T-1, RNG hash}
       ReadEvents(T) + own events ─► JudgePairs ─► per event: AllowOnHost (slot, trim, refuse)
            ─► compare RNG state ─► Replay() ─► re-enqueue (serialised after replay)
       SendEvents: one GroupedEvent per tick, compressed once, written to every guest
   buckets 1..N (entities, singletons); parallel water/soil run on worker threads until the next bucket 0
 guest: may not start T+1 until the host's group for T+1 has arrived (IsReadyToStartTick);
        plays the heartbeat first (RNG, hashes, digest), then the host's events in the host's order
 paused: UpdateSingleton replays events frame by frame at the tick boundary (CurrentSpeed == TargetSpeed == 0)
 speed:  SpeedSetEvent/SpeedBoostEvent shared; guest CatchUpSpeed; host HostPacing/FrameRatePacing
```

Load and session start:
- Host and guest both set `nextSeedOnLoad = hash(save bytes)` before loading (`ServerHostingUtils.cs:175`,
  `ClientConnectionService.cs:291`).
- `DeterminismService`'s constructor applies the seed with `Random.InitState`.
- Every RNG draw counts as game RNG until `ReplayService.Initialize`, two `UpdateSingleton` frames later.
- Joining closes at tick 1, or earlier at the first game-changing action (with a host prompt, `HostStartGate`).

Desync detection:

| Check | Stops the game? |
|---|---|
| RNG state per event (all four words) | Yes |
| Entity-order and walker hashes on each heartbeat | No, logged only |
| Colony digest on each heartbeat (separate colonies) | Yes |
| Daily colony fingerprint | Yes |
| Per-tick traces (debug) | Yes, with detailed logging on both sides |

---

## 2. Every system that touches synced state

**A. Lockstep core**
- `ReplayService` / `TickingService` (tick gate, replay loop, send).
- `EventIO` and `NetIOBase`: frame reading, per-action salvage on the host, guest leaves quietly.
- TimberNet ordering: `InsertInScript`, `PopEventsForTick`, the host re-stamping ticks.

**B. Player actions: 66 `ReplayEvent` types**

| File | Count | What |
|---|---|---|
| `ToolEvents` | 8 | Placement, demolition, planting, clearing, cutting, unlock, working hours, duplication |
| `EntityUIEvents` | 25 | Per-building settings |
| `AutomationEvents` | 5 | About 60 setters through one `UniversalPrefix` |
| `BatchEvents` | 4 | |
| `TimeEvents` | 3 | |
| Connection, system, heartbeat, trace, group, refusal | 7 | |
| `Colonies/` | 13 | Found, presence, handover, stewards ×3, hello, wishlist, exchange ×5 |
| `Ping` | 1 | |

They are recorded by ~65 bool `Priority.First` prefixes, plus ~12 recorders that are not Harmony patches (the
Trading Post and trade panels, the host's daily tick, the chat box, the tick-once block).

**C. Determinism layer (`DeterminismService.cs`)**
- RNG classification: 18 blacklisted types, 9 non-game markers, 1 game marker, and unknown callers go to non-game.
- `Guid.NewGuid` → 16 draws of the game RNG, always.
- The `Time.time` native detour (ticks × `fixedDeltaTime`) and `FluidSecondsPassedToday` without the in-tick
  fraction.
- `TEBPatcher`: the hashes, and snapping animation to the simulation position before each bucket.
- Entity-ID de-duplication on load.
- Saves deferred to the end of a full tick.

**D. Moved from frames to ticks (`Fixes/`, `TickReplacerService`)**
- The instant navmesh, gates and the gate-conflict walk, `AutomationRunner`, the import/export snapshot cache,
  `RecoveredGoodStackSpawner`.
- Water-source buffering, ordering and timing; zipline speed; demolition tie-break; dev keys; tick-once block.
- The `DistrictMap` / `DistrictBuildingsFix` finalizers, animation.

**E. The colony model (separate colonies)**
- About 20 pieces of saved or derived state: owners, stamps, land, marks, science, trading totals, exchanges and
  ledger, away days, stewards, wishlist, hours, the slot table.
- Host-only judging: `AllowOnHost`, `JudgePairs`, seating.
- Tick systems: `ColonyReach.Tick`, the exchange tick, the lifecycle tick.
- About 40 simulation patches: work separation, science pools and unlocks, working hours, Trading Post inventory
  and export, migration, marks.

**F. Load and session:** the save bytes and seed, joining closing, `HostStartGate`, rehost, the host-only
`SeatHost` at load.

**G. Lanes that must never touch the simulation:** activity, chat, pings, status/roster, `PendingActions`, and the
per-player colony view and journal. Checked only where a finding suggests they leak.

---

## 3. Desync risks to investigate

Each item says what the lead is and what would count as proof. Everything will be reported as **Confirmed** (traced
end to end in the mod and the decompiled game), **Plausible**, or **Refuted**. Line numbers are beta11.

### Tier 1: concrete leads with a plausible path to a silent desync

**R1. The patched `Time.time` carries over from the previous session.**
- `TimeTimePatcher.time` is only set from the `ticksSinceLoad` setter (`DeterminismService.cs:949-953`, called at
  `ReplayService.cs:126-135`). It is never reset on load.
- So from load until the first `DoTick`, `Time.time` returns whatever this process last had:
  - 0 in a fresh process;
  - the old session's end on a host after *Save and Rehost*, or on anyone who played before.
- **Check:** every game or mod reader of `Time.time` that runs during load, `PostLoad`, entity initialisation or a
  tick-0 replay, and stores or compares it (timers, cooldowns, `RuinModelUpdater`'s time trigger, animation start
  times that reach the simulation).
- **Likely fix:** reset the value where the seed is applied.

**R2. Saving changes live simulation state on the saving computer only.**
- Saves are not recorded events. The host's autosave, manual saves, a guest's own saves, the rehost save and the
  desync-report save each run on one computer, at the end of a full tick.
- The mod's own note (`DesyncPatches.cs:232-238`) says saving calls
  `SoilMoistureService.UpdateMoistureLevels` early, and that this is safe unless a replay runs before the singletons
  tick. In this design replays run at bucket 0, before the singletons.
- Co-op saves also skip `FinishFullTick` and with it `ForceFinishParallelTick`. So a save may run while the
  water/soil worker threads are still running and `WaterSource` ticks are still buffered.
- **Check:**
  - every write a save makes to live state;
  - who autosaves in a session (is the guest's autosave on?);
  - whether anything that runs at bucket 0 reads the synced moisture map.

**R3. The game's own unlock state differs per computer, by design.**
- `BuildingUnlockingService.UnlockIgnoringCost` runs only where the actor is the local colony
  (`ColonyScienceService.cs:487-497`, `ToolEvents.cs:597`).
- The same goes for `UnlockedPlantableGroupsRegistry`, the `BuildingUnlockedEvent` and `ToolUnlockedEvent` posts,
  and `RefreshToolLocks` running inside replays.
- The alpha10 review cleared `_unlockedBuildings`, but not the plantable registry or the event listeners.
- **Check:** every reader and listener in the game assemblies. Planting, farmhouse and forester plantable choice
  are the ones to worry about.

**R4. Recording prefixes reachable from simulation code.**
- `DoPrefix` lets the original run only while replaying. If tick code calls any recorded game method, every
  computer records it as a player action and skips it. A guest then sends it to the host as its own, and it is
  played a second time.
- `WorkplaceWorkerType.SetWorkerType` already passes through while ticking. The others don't.
- **Check:** a call-graph audit of every recorded target in 1.1.2.4, classifying each as UI-only or reachable from
  the tick or a replay. For example, can game code call `SpeedManager.ChangeSpeed`, or `SingleGoodAllower.Allow`?
- Also the fallbacks where a prefix returns true and the method runs **unrecorded on one computer**:
  `AutomationEvents.cs:434`, `BatchEvents.cs:107-110, 156-160, 210-214, 310-317`.

**R5. `Guid.NewGuid` always draws the game RNG, whatever the context** (`DeterminismService.cs:822-875`).
- Any call on one computer outside a tick or replay moves that computer's RNG.
- **Check:** map the game callers reachable from frame or UI code: previews, tool descriptions, UI-built entities.
  RNG is saved and restored only around the host's placement check.
- **Mod callers:** `WalkerDiagnostics.cs:73` and `WaterDiagnostics.cs:121`. When a `ClientDesyncedEvent` plays,
  they draw 16–32 values, but only on computers that have debug data. That matters if anyone plays on without a
  rehost.

**R6. The load window before `IsLoaded`.**
- For at least two frames every draw counts as game RNG, even blacklisted ones, non-game markers and other threads
  (`DeterminismService.cs:201-208`). Per-frame visuals and sounds in those frames may draw a different number of
  times on each computer.
- `DoPrefix` returns true before `IsLoaded`, so an action in the first frames runs on that computer only
  (`ReplayEvent.cs:131, 160-166`).
- **Check:** what draws during load, and whether the first heartbeat always catches it.

**R7. Game code that still changes simulation state per frame.**
- A systematic sweep of every `IUpdatableSingleton`, `ILateUpdatableSingleton` and MonoBehaviour
  `Update`/`LateUpdate` in `Timberborn.*.dll`, and every EventBus post made from them. Subtract what `Fixes/`
  already moved.
- **Known and unfixed:**
  - Wonders and the Earth Repopulator launch (fork PR #46 was closed);
  - fireworks deleted in a per-frame `Update` (are they tickable entities?);
  - the dev-mode walker debugger drawing `UnityEngine.Random` in `LateUpdate`;
  - zipline and swimming state set from animation.

**R8. Isolation of the parallel tick.**
- Water, soil moisture and soil contamination run on worker threads from mid-tick until the next bucket 0.
- **Check:** main-thread tick code reads only the published buffers, never in-flight state. This is the original
  author's open TODO.
- Also: the shared unseeded `System.Random` and the plain static flags (`IsTicking`, `makeRealGuid`, the patcher
  counters) read across threads.

**R9. Replays that go through UI tools or panels.**
- `ClearResourcesMarkedEvent` replays through `DemolishableSelectionTool.ActionCallback`. Does the rectangle depend
  on the layer slice, as planting did in alpha8? The dev-key scan skips "Tool" types and `Timberborn.DemolishingUI`.
- `WorkingHoursChangedEvent` in a shared game goes through `WorkingHoursPanel`.
- `ShowOptionsMenuEvent` opens a panel on every computer.
- `PlantingAreaMarkedEvent` carries the recorded `ray`.

**R10. How finely paused replays are split up.**
- While paused, the host plays each action in the frame it arrives. A guest may play several in one frame.
- **Check:**
  - nothing per-frame changes simulation state between them while paused;
  - the "replaying events when bucket != 0" path (`ReplayService.cs:355-358`) can't happen on a live session;
  - the colony digest's order is unaffected.

**R11. Exceptions swallowed on one computer.**
- The `DistrictBuildingsFix` finalizers are ungated and, by their own comment, triggered by preview registration
  (`DistrictBuildingsFix.cs:22-61`). The same goes for the `DistrictMap.AssignDistrictToRoadMap` finalizer.
- **Check:** whether the computer where the preview lives takes a different path through the simulation.

### Tier 2: stopping healthy games, and gaps in detection

**R12. False positives in the colony digest and the daily check.**
- `SeatHost` rewrites the host's saved slot table at `PostLoad`, outside lockstep, and the daily fingerprint hashes
  that table (`ColonySlotService.cs:97-116`, `ColonyDiagnostics.cs:203`).
- A hand-over notes one digest entry per entity, in registry order (`ColonyHandover.cs:355-364`).
- `InstantDistrict` is in the fingerprint.
- Science `Enable`, `deadSince` and steward `acting` are never noted.

**R13. Paths that stop everyone.**
- `AutomationEvent` with a missing target entity: `Invoke(null, …)` throws, which calls `AbortReplay` for all
  (`AutomationEvents.cs:51-53`).
- `BuildingPlacedEvent` with an unknown prefab: NRE.
- `DuplicationEvent` with a null source or target.
- The FillValve setters are listed twice (recorded twice?).
- `RelayFragmentRemoveRowPatch` is not `Priority.First`, and the enforcer can't see it (it doesn't follow calls
  into game code or void prefixes).

**R14. Detection policy.**
- The entity-order and walker hashes only log. Should a mismatch stop the game, or at least start a report?
- What reaches the simulation with no check at all?

### Tier 3: known or by design (re-check only)

- **R15.** The original author's suspects in 1.1.2.4: `NaturalResourceReproducer`,
  `WateredNaturalResource.StartDryingOut` (a timer), and gameplay in `OnDestroyed` at the end of a frame.
- **R16.** Replacing prefixes at `Priority.Last` against other mods; other mods are a warning only; dev-mode tools.
- **R17.** The colony features added since alpha10 (stewards, wishlist, supplies, navigation, journal, speed boost,
  the exchange rework):
  - The inventories found every host decision carried in its event, except `SeatHost` (R12).
  - I'll spot-check them.
  - The alpha10 report's "found sound" list stays the baseline and is not re-reviewed.

---

## 4. Network questions

**N1. The latency budget.** Build it from the code and measure what can be measured here, at speeds 1, 3 and 7.
The code comments give a tick as about 0.6, 0.2 and 0.09 s; I'll confirm that from `TickSpec`.
- **A guest's action:**
  1. the frame it is recorded in;
  2. one-way to the host;
  3. waiting for the host's next `Update` to stamp it;
  4. waiting for the host's next tick boundary (half a tick on average);
  5. the host replays it and sends it;
  6. one-way back;
  7. the guest's lag behind the host (`BufferTicksFor`: 1 tick, or 2 at speed 7);
  8. the guest reaches that tick.
- **A host's action:** only the tick-boundary wait.
- Separate what host-authoritative lockstep makes unavoidable from what can be removed. Mid-tick replay stays
  rejected.

**N2. Buckets lost on the guest.**
- `Ticker` has already taken the time off its accumulator when our `TickBuckets` refuses to tick. The time is lost,
  the guest falls behind, and `CatchUpSpeed` flips `Time.timeScale`, which notifies every animated building.
- **Evaluate:** handing back the unticked buckets (bounded) instead. Use an offline model of the gate plus
  catch-up across RTTs and speeds.
- This is not the rejected mid-tick design, and it doesn't change the buffer.

**N3. The Steam receive path.** The pump runs on the game thread, a receive thread parses, and the game thread picks
the event up in `Update`. If parsing loses the race with the ticker in the same frame, the tick waits a frame.
- **Measure:** how often that happens.
- **Option:** parse Steam frames on the game thread inside the pump.

**N4. The host's send path.**
- Over TCP, the host writes to every guest synchronously on the game thread, under the broadcast lock
  (`TimberServer.cs:476-516`). A guest whose socket buffer is full stalls the host, and so every other guest.
- The activity lane shares `lock(stream)` with gameplay writes.
- **Option:** ordered writer queues per guest (like `ActivityChannel`), with a stall limit.

**N5. What each message costs on the wire.**
- Every group is JSON with `TypeNameHandling.All`, compressed with `gzip Optimal`.
- **Measure:** size and time per tick at speed 7, and for large dragged areas. On the host that is compression on
  the game thread; on the guest it is deserialisation inside the tick (`ReadEvents`).
- **Options:**
  - `Fastest`, or no compression, for small frames;
  - shorter type names;
  - reading the JSON off the game thread, if the converters allow it.
- These change the wire format, which is fine because everyone runs the same build.

**N6. Per-tick overhead that grows with the colony.**
- `TEBPatcher` walks every entity in each bucket, to snap it and hash it.
- `HasEventsForTick` scans and calls `Update()` on every bucket loop.
- The digest.
- **Measure:** the cost against colony size, using the existing performance checks.

**N7. The pacing loop.** `HostPacing` and `FrameRatePacing` sample once a second from probe replies.
- **Check:** how fast they react to a slow guest.
- **Check:** whether the `CatchUpSpeed` thresholds still make sense at boosted speeds.

**N8. Several guests.** How the host fans out to three guests, how `WorstGuest` is chosen, and relayed Steam links.

**N9. Rejoining (an architecture question, not a change).** Rejoining means *Save and Rehost*, because loading is
not deterministic mid-session. Is a cheaper path worth it, such as an automatic rehost when a guest drops?

---

## 5. Method

1. **Baseline.**
   - Build both configs on beta11 and run StabilityTests (348) and RuntimeChecks (278), so later numbers compare to
     something.
   - Decompile the relevant Timberborn 1.1.2.4 assemblies into the scratchpad.
2. **Sweeps done by script, not by eye.**
   - Call-graph scans of the game assemblies:
     - R4: callers of every recorded target;
     - R5: `Guid.NewGuid` callers;
     - R7: frame-time writers;
     - R3: unlock readers and listeners.
   - The scans extend the existing `RuntimeChecks/IlScan.cs` approach, so they can become permanent checks.
3. **Parallel reviewers.**
   - About 5 agents, one per group: R1/R2/R6/R8 (load, save, threads), R3/R4/R5/R9 (actions), R7/R10/R11/R15
     (frames), R12–R14 (detection), N1–N9 (network).
   - Each tries to refute its own findings.
   - I then re-check every finding against the mod and the decompiled game before it goes in the report.
4. **Network measurements.**
   - The TCP host+guest rig in StabilityTests (`ActivityTransportChecks.Session`) with injected delay.
   - Microbenchmarks on real payloads: heartbeat groups, 500-tile marks.
   - An offline model of the tick gate plus `CatchUpSpeed` for N1/N2.
   - In-game timing is impossible from here. Every number gets a line in the test script that confirms it (Script B
     8e, the report's wait counts).
5. **Output.**
   - `REVIEW-FINDINGS-1.4.0-beta11.md`, in the alpha10 report's format:
     - each finding with its evidence (mod and game `file:line`), severity, status and proposed fix;
     - for each fix: wire and save impact, and the check to add;
     - a latency budget table and the network proposals, ranked by gain against risk;
     - new lines for `ALPHA-TEST-SCRIPTS.md`.
   - No code changes and no release in this pass. Implementing the fixes and cutting beta12 is a separate step, when
     you say so.

## 6. Out of scope

- Play-testing (not possible from here).
- Display-only differences between players, unless simulation code reads them.
- Save compatibility across betas.
- Direct-IP identity as a security question.
- Performance items that aren't about the network or the tick.
