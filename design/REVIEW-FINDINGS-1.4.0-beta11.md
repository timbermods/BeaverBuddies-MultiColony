# Review findings: desyncs and network, MultiColony 1.4.0-beta11 → fixed in 1.4.0-beta12

**Reviewed:** `v1.4.0-beta11` (`29cad83`, plus the docs-only `5fc1c05`), Timberborn 1.1.2.4, following
[REVIEW-PLAN-1.4.0-beta11.md](REVIEW-PLAN-1.4.0-beta11.md).

**How it was done:**
- The main reviewer covered load, saves, threads and detection (R1, R2, R6, R8, R10–R14).
- Two reviewers ran in parallel: game-side sweeps (R3, R4, R5, R7, R9) and the network (N1–N9).
- Every finding below was re-checked against the mod and against the decompiled game (all 497 `Timberborn.*.dll`,
  ilspycmd) before it was fixed or refuted.
- Nothing was played. The network numbers come from:
  - a frame-by-frame model that compiles the repo's real `CatchUpSpeed`/`HostPacing`;
  - real-loopback rigs;
  - benchmarks.

**Status key:**
- **Confirmed:** traced end to end in the mod and the game.
- **Plausible:** the path exists, and one link depends on timing.
- **Refuted:** checked and found sound.

**The checks:**
- StabilityTests 348 → **360**.
- RuntimeChecks 278 → **314**.
- Both builds with 0 warnings; Python 2/2.
- Run against beta11's DLL, 31 of the 36 new RuntimeChecks fail. The other five pass there too, as they should:
  - three reproduce the frame-rate bug in the game's own code;
  - one guards saves, and held before the fix;
  - one is a safety check.
- The two new real-socket transport checks fail on beta11's TimberNet.
- The other new StabilityTests exercise code that is new in beta12.

---

## 1. Summary

| ID | What | Status | Kind | beta12 |
|---|---|---|---|---|
| D1 | The Earth Repopulator's plane launch and every Wonder's animation run on render frames; planes are created outside any tick | Confirmed | desync | Fixed: the Stability Fork's closed PR #46, ported with log-and-skip transpilers |
| D2 | A deleted entity counts as alive until the end of the frame, and each computer ends its frames at a different point of the tick | Confirmed | desync | Fixed |
| D3 | Whether a Wonder can be activated is judged from per-frame animation state, on each computer | Plausible | desync | Fixed: the host decides |
| D4 | While a game loads, sounds and input marked as not simulation still draw the shared random numbers | Confirmed (mechanism) | desync at start | Fixed |
| D5 | The game's walker debugger (debug mode) draws `UnityEngine.Random` every frame | Confirmed | desync, debug only | Fixed |
| D6 | Desync diagnostics name their files with `Guid.NewGuid`, 16 game draws, on computers with debug data only | Confirmed | desync after a desync | Fixed |
| D7 | The patched `Time.time` carries the previous session's value until the first tick | Confirmed; only animation reads it | hardening | Fixed |
| S1 | A replayed automation setting on a demolished building throws, and the session ends for everyone | Confirmed | session-stopper | Fixed |
| S2 | A building from a mod only one side runs: the host throws (a guest's building) or a guest throws (the host's), and everyone stops | Confirmed | session-stopper | Fixed: the host refuses, the guest leaves quietly |
| S3 | The daily colony check hashes the host's seat table, which only reaches guests inside a hello | Confirmed | false alarm | Fixed |
| B1 | Spring-return levers are switched off through a recorded method from the tick | Confirmed | behaviour (late, duplicated, refusal notices) | Fixed |
| B2 | A paused building deleted in the tick records a "resume" on every computer | Confirmed | cosmetic | Fixed |
| B3 | Co-op saves read the water/soil arrays while their worker threads write them | Confirmed | torn saves | Fixed |
| B4 | Any stop (a guest waiting, the host easing off) saved at once, possibly mid-tick | Confirmed | mid-tick saves | Fixed |
| N-P1 | When the host eases off for a slow guest, the other guests are not told and go stop-go every tick | Confirmed (model: 5–43% frozen frames) | latency | Fixed |
| N-P2 | Over direct IP, one guest that stops reading freezes the host and every other guest | Confirmed (measured: 3.6 s to 20 s+) | stall | Fixed |
| N-P4 | Receive loops on pool threads wait tens of ms for a turn under CPU load | Measured (28 ms median) | latency | Fixed |
| N-P5 | Steam sends made outside the tick loop wait a frame | Confirmed | latency (1 frame each way) | Fixed |
| N-P6 | At boosted speeds an eased host never climbs back (thresholds in ticks) | Confirmed (model) | pacing | Fixed |
| N-P7 | gzip `Optimal` on large actions, on the host's game thread | Measured | CPU | Fixed (`Fastest`) |

---

## 2. Desyncs

### D1. The Earth Repopulator's launch and every Wonder's animation run on render frames (Confirmed, desync)

**The path** (all game code, 1.1.2.4):
1. `WonderActivatedEvent.Replay` calls `Wonder.Activate`, which starts the Wonder's animator. `AnimatorRegistry.UpdateSingleton`
   advances the animator per frame by `Time.deltaTime`.
2. `WonderAnimationController.Update` (`Timberborn.Wonders`, per frame) sees `PlayingFinished` and raises
   `StartAnimationFinished`.
3. `PlaneLauncher.OnStartAnimationFinished` calls `StartEjectingPlane`, then `PlaneCatapult.CatapultPlane`, then
   `PlaneSpawner.SpawnPlane`, then `EntityService.Instantiate`. That creates an entity, and its `Guid.NewGuid`
   (patched) makes 16 draws of the shared random numbers, on each computer at a different point of the tick.
4. The catapult's wait and runway (`PlaneCatapult.Update`), the plane (`Plane.Update`) and the launcher's turn
   (`PlaneLauncherRotator.Update`) are per frame too.
5. The turn's end deactivates the Wonder: `IsActive`, the unlock countdown, and a timer that destroys the pilots.

**Fix: the fork's PR #46, ported** (`Fixes/WonderTimingFix.cs`, `Doc/WonderTiming.md`).
- In co-op, `WonderTickService` steps every Wonder once per tick with the game's own methods. It runs
  `TimbermeshAnimator.UpdateAnimation` and `WonderAnimationController.Update`, then the catapult and plane, then
  the rotator, with the tick interval as their clock.
- The frame updates only draw.
- Saves restore the ticked poses first.

**Changed from #46, as Kyler asked when closing it:** a transpiler that finds a different number of `Time.deltaTime`
reads no longer throws out of the mod's single `PatchAll`, which would have left the mod half-patched. It leaves that
method as it is, logs once, and sets `WonderTiming.Unavailable`, which switches the whole takeover off. The Wonders
then run on frame time as in beta11.

**MultiColony additions:**
- `TEBPatcher` leaves switched-off walkers out of the walker hash. A pilot riding its plane (frame time, as in the
  game) otherwise logs a false `Walker mismatch`.
- D3 below.

**Checks:**
- RuntimeChecks `WonderChecks`: the fork's 14.
- The refusal check is turned round: an incompatible body is left unchanged, the timing is switched off, and
  nothing throws.
- The game's animation ends on tick 7, 7 and 6 at 10, 30 and 144 FPS before the fix, and on tick 7 at all three
  after it.

**Owed:**
- #46's playtest: one player capped at 15 FPS through a full launch.
- Script B 8t.

### D2. A deleted entity is alive until the end of the frame (Confirmed path, desync)

- **Why it is alive:**
  - `BaseComponent`'s `bool` is `GameObject != null` (`Timberborn.BaseComponentSystem`).
  - `EntityService.Delete` ends with `Object.Destroy`, which Unity carries out at the end of the frame.
- **Why the computers differ:** the mod spreads a tick over each computer's own frames. So in a later bucket of the
  same tick, a computer still in the deleting frame finds the entity alive, and one a frame further on finds it
  gone.
- **Example:**
  - A lumberjack's `WalkToReservableExecutor.Tick` checks `!_reservable`, a tree an explosion or a replayed demolition
    took. It keeps walking on one computer and fails on the other.
  - The same happens with paused actions: a deletion and the unpause after it can arrive in one frame on a guest and
    in two on the host.

**Fix** (`Fixes/TickTimingFixes.cs`, `TickingService`):
- A deletion inside the tick or a replayed action (`IsTicking || IsReplayingEvents`) interrupts the frame's ticking on
  every computer. The rest of the tick runs from the next frame, after Unity has destroyed the object.
- The frame's unticked buckets go back to `Ticker._accumulatedDeltaTime`, capped at one tick's worth, so the game
  runs no slower. Before, an interruption lost them.
- A pending save waits for the tick's end.

**Checks:** RuntimeChecks for the postfix and its trigger, the refund and its cap, and a save kept until the tick's
end. **Script B 8u.**

### D3. Wonder activation judged from animation state (Plausible, desync)

- **The check:** `Wonder.Activate` checks `CanBeActivated`. `AlreadyActivatedWonderBlocker` reads `IsAnimating`
  (`_animator.Enabled`), which the per-frame `WonderAnimationController.Update` clears.
- **The window:** a click just after a Wonder switched off can land while one computer's reverse animation is running
  and another's has finished.

**Fix:**
- `WonderActivatedEvent.activated`: the host asks `CanBeActivated()` as it plays the event and writes the answer.
- Guests follow it (`WonderActivationFollowsHostPatcher`, `Priority.Last`).
- With D1 this is belt and braces. It also covers `WonderTiming.Unavailable`.

### D4. The loading window ignored the non-game marks (Confirmed mechanism, desync at start)

- `DeterminismService.ShouldFreezeSeed` put "not loaded yet, so game RNG" before the explicit markers. So for the frames
  before `ReplayService.IsLoaded`:
  - `Sounds.GetRandomSound`, `SoundEmitter.Update`, `LoopingSoundPlayer`, `InputService.UpdateSingleton` and the other
    non-game markers drew the shared random numbers;
  - so did other threads.
- A sound one player's game played and the other's did not would move one random state only.

**Fix:**
- A pure rule, `RandomSourceRules.Choose`, has the markers first, then loading, and never lets another thread draw.
- `ShouldFreezeSeed` gathers the facts and calls it.

**Checks:** StabilityTests 4; RuntimeChecks for the wiring.

### D5. The walker debugger draws the shared random numbers (Confirmed, debug mode only)

- `WalkerDebugger.ResetPathMarkers` (`Timberborn.WalkingSystemUI`) calls `UnityEngine.Random.insideUnitSphere` per
  path corner in `LateUpdate`, on the computer that has debug mode on and a walker selected.
- **Fix:** in co-op, the random state is saved and restored around it.

### D6. Desync diagnostics drew game random numbers (Confirmed)

- `WalkerDiagnostics`/`WaterDiagnostics.WriteOnDesync` named their files with `Guid.NewGuid()`. That is 16 game draws,
  only on computers holding debug data, as a `ClientDesyncedEvent` plays.
- The players left in the game then split on their next action.
- **Fix:** `GuidPatcher.RealNewGuid()`.
- **Hardening in the same place:** `GuidPatcher.makeRealGuid` is `[ThreadStatic]`, so a real GUID asked for on a
  network thread can't make the main thread's next entity ID real.

### D7. The patched `Time.time` carries over between sessions (Confirmed; hardening)

- `TimeTimePatcher.time` was only set as each tick started. Until the first tick it read the previous session's value:
  0 in a fresh program, hours on a host that saved and rehosted.
- **Readers:** every game reader was checked.
  - `MovementAnimator.InitializeEntity` animates from the loaded position with `Time.time`: animation only.
  - `Detonator` compares `Time.time` inside the tick, after it is set.
  - Explosion lighting, Wonder particles and `FireIntensityController` are visuals.
- **Fix:** reset to 0 where the seed is applied, so every computer loads with the same clock.

---

## 3. Paths that stopped everyone, and a false alarm

### S1. An automation setting on a demolished building (Confirmed)

- `AutomationEvent.Replay` → `methodInfo.Invoke(componentObj, …)` with `componentObj == null`, which throws
  `TargetException` → `AbortReplay` → the session ends for everyone.
- A setting clicked on a building someone demolished in the same tick did it. A null `arguments` threw too.
- **Fix:** skipped with a warning, the same on every computer.
- **Check:** RuntimeChecks (both cases, with an empty entity registry).

### S2. A building one side's mods lack (Confirmed)

- `BuildingService.GetBuildingTemplate` throws `ArgumentException` for an unknown name, and other mods are only a
  warning at join.
- **A guest's building the host lacks** threw on the host:
  - in a shared game, in the replay;
  - in separate colonies it was caught by the judge's try.
- **The host's building a guest lacks** threw on that guest, which aborted and sent a `SessionFault`, which stopped
  everyone.

**Fix:**
- `AllowOnHost` refuses `BuildingPlacedEvent` and `BuildingUnlockedEvent` for a building the host doesn't have, in
  every game. The guest is told.
- A guest meeting an unknown building raises `MissingContentException` before anything is played and leaves quietly,
  like beta9's unreadable action. The host and the others play on.

**Refuted alongside:** `DuplicationEvent` with a missing building. The game's `Duplicator.Duplicate` already returns on
null.

### S3. The daily colony check and the seat table (Confirmed, false alarm)

- `ColonySlotService.SeatHost` (host only, `PostLoad`) may add or rename the host's entry in the saved table. Guests
  get the host's table only inside a later hello.
- The daily fingerprint hashed the table. So a guest whose hello was refused kept the save's table and was stopped
  at its next daily check.
- Nothing a guest simulates reads the table: its readers are the host's judge, notices and panels.
- **Fix:** left out of the fingerprint.

---

## 4. Behaviour bugs fixed on the way

- **B1. Spring-return levers.**
  - `SpringReturnService.CommitTick` → `Lever.SpringReturnToOff` → `SwitchState`, which is recorded. Every computer
    recorded the switch-off as a click.
  - The results: the host played it a tick late, each guest sent another, other colonies' players got refusal
    notices, and it read `_isPressed`, which only the pressing computer sets.
  - **Now:** in co-op the lever switches off in the tick, at once, the same everywhere, as a simulation call
    (`ReplayEvent.RunAsSimulation`, which recording prefixes let through).
  - Holding a lever on never worked in co-op, and still doesn't.
- **B2.** `PausableBuilding.OnExitFinishedState`/`OnExitUnfinishedState` → `Resume` during deletion now runs as a
  simulation call.
- **B3. Torn saves.**
  - A co-op save skipped `FinishFullTick`, and with it `ForceFinishParallelTick`. That is right, because the latter's
    listeners would catch up early on one computer.
  - But the game then refused the save ("Cannot access array outside of singleton Tick"). `TickOnlyArrayFix`
    silenced the refusal, so the save read the water and soil arrays mid-write.
  - Nobody desyncs from it (everyone reloads the same bytes), but the saved water could be half a step.
  - **Now:** a save at the end of a tick waits for the worker threads (`FinishParallelTick`, without the forced
    listeners).
  - The mod's old note that saving updates soil moisture early is out of date: in 1.1.2.4 `UpdateMoistureLevels` is
    called only from the tick.
- **B4.** `FinishFullTickIfNeededAndThen` took any `CurrentSpeed == 0` for a pause. Now only the players' pause
  (`TargetSpeed == 0`, always at a tick start) saves at once; any other stop finishes its tick first.

---

## 5. Network

### Latency budget (model, 60 fps; tick = 0.6 s ÷ speed: 600, 200 and 86 ms)

Guest click to the guest playing its own action, mean (p95), before beta12's changes:

| RTT | Speed 1 | Speed 3 | Speed 7 |
|---|---|---|---|
| 20 ms | 369 (638) | 170 (262) | 114 (156) |
| 80 ms | 440 (711) | 241 (332) | 190 (233) |
| 150 ms | 547 (819) | 365 (462) | 318 (368) |

- **Unavoidable** in host-authoritative lockstep:
  - the wait for the host's next tick boundary (half a tick on average: 304, 103 and 46 ms at speeds 1, 3 and 7);
  - the round trip.
- **Removable, all small:** receive-thread scheduling under load (P4), a Steam frame while paused or waiting at the
  gate (P5), and part of the guest's jitter buffer (P3/P8, deferred).
- **Corrections to the brief:**
  - The host stamps a guest's action with no extra tick: an action parsed before `DoTick(T)` plays in T.
  - `BufferTicksFor` is where catch-up starts, not the steady lag.

### Changed in beta12

| | What | Measured or modelled gain | Guard |
|---|---|---|---|
| P1 | The heartbeat carries the host's eased pace (`hostSpeed`, left out at full speed); guests run at `CatchUpSpeed.PaceFor(chosen, hostSpeed)` | Frozen frames behind a host eased to 85%, 70%, 50% and 30% drop from 4.6%, 19.7%, 42.7% and 65.5% to about 0; at 30%, 420 speed changes a minute to 0 | StabilityTests 3 (one a frame model); RuntimeChecks 2 (wiring, JSON round trip) |
| P2 | Direct-IP guests get an ordered send lane each (`TimberNet/SendLane.cs`); a guest that takes nothing for 30 s, or has 16 MB waiting, is dropped as over Steam; the session-end reason goes through the lane | Removes host and all-guest freezes of 3.6 s to over 20 s; about 0.1 to 0.6 ms a tick of socket writes off the game thread | StabilityTests: lane order and stall; a real-loopback guest that stops reading (fails on beta11) |
| P4 | Each connection is read by a thread of its own at AboveNormal priority | Handoff under full CPU load drops from 28 ms median (95 ms p90) to 0.085 ms | StabilityTests (fails on beta11) |
| P5 | What the host sends while paused, a guest's own action and a desync notice are handed to Steam at once | One frame each way (about 17 ms) over Steam | RuntimeChecks |
| P6 | `HostPacing` thresholds scale with speed above 7 | An eased host climbs back at speed 30 and 150 ms of ping (a guest in step reports 12 to 16 ticks) | StabilityTests |
| P7 | gzip `Fastest` | About 1.3 ms (Mono) less host game-thread time on a 500-tile mark; −7% to +13% bytes | existing transport checks |

None of these changes what is simulated or on which tick. Wire: P1 adds one optional heartbeat field; the rest are
local.

### Deferred, with reasons

- **P3/P8, the jitter buffer.** A slow shrink of the guest's buffer when the network is calm, and a one-frame refund
  at the gate. They are worth 10–40 ms normally, and up to 170 ms after a delay spike (in beta11 the buffer only
  grows). But they change the catch-up buffer, which the author settled in alpha5 and which the owner decided to
  revisit only with in-game data. Get Script B 8e's wait counts first.
- **P9.** Caching the Walker and rotator in `TEBPatcher`: about 0.05 ms a tick.
- **N9, a rehost prompt when a guest drops.** A feature, not a fix.
- **Considered and rejected by the network review:**
  - refunding up to a whole tick at the gate (removes the jitter buffer);
  - no gzip on small frames;
  - `$type` aliases;
  - parsing Steam frames in the pump;
  - `ToObject` off the game thread;
  - mid-tick replay (never re-proposed).

---

## 6. Checked and found sound

- **R3, unlock state.**
  - `_unlockedBuildings` is read only by the patched `Unlocked`, by `Save`, and by the mod.
  - `UnlockedPlantableGroupsRegistry` is read only by `PlantableToolLocker`/`UnlockedPlantableService`.
  - `BuildingUnlockedEvent` and `ToolUnlockedEvent` have only UI and tutorial listeners.
  - No planter, farmhouse or forester reads unlocks.
- **R4, recorded methods reached from the simulation.**
  - Besides B1 and B2, every recorded target is reached only from UI, a replay or a load. The full caller table is in
    the reviewer's notes.
  - The prefix fallbacks can't change anything.
  - The doubled FillValve patches never record twice: the second copy runs only if the first let the call through.
  - `RelayFragmentRemoveRowPatch` records through a `Priority.First` target.
- **R5, GUIDs.** Previews, tool descriptions, plantable previews and the placement check use `TemplateInstantiator`,
  which makes no GUID. Every `EntityService.Instantiate` caller runs in a tick, a replay or a load, except the Wonder
  planes (D1) and the dev spawners.
- **R7, frame-time game code.** All 163 frame hooks were classified.
  - Fireworks aren't tickable.
  - Zipline and swimming are read only by the zipline speed fix.
  - `NaturalResourceReproducer` runs in the tick.
  - `WateredNaturalResource.StartDryingOut` is started from the tick, and its timer ticks.
  - No gameplay runs in `OnDestroy`. The real end-of-frame effect is D2.
- **R8, the parallel tick.** Water, soil moisture and contamination take their inputs through queued modifications
  (`FlowLimiterService`, `SoilBarrierMap`, `WaterChangeService`), buffered arrays and `TickOnlyArray` guards, applied
  in the singleton tick. The one known race, `WaterSource.Tick`, is buffered by the mod. The only way past the guards
  was B3.
- **R9, replays through UI tools.** `DemolishableSelectionTool.ActionCallback` reads only the recorded objects and
  rectangle, with no layer slice, camera or input. Planting uses its `ray` only for levelling, which the replay
  replaces.
- **R10, paused replays.** In a live session a pause lands only at a tick start (`SpeedSetEvent` plays at `DoTick`), and
  paused actions play in the host's order. Only D2's end-of-frame effect could separate them, and it's fixed.
- **R11.** The exception-swallowing district finalizers only ever fire for the preview map, which no simulation reads
  in co-op. Without them, the placing computer's tick would abort.
- **Colony digest gating** (R12): no path found that feeds one computer's digest and not another's. The hand-over's
  registry order is the same everywhere.

---

## 7. Left as it is

- **G6, the Memory and Relay panels.** When open, these panels re-apply remembered inputs through recorded setters as
  another player's change replays. The result is the same everywhere, but it's an action nobody chose, and in separate
  colonies it causes refusal notices. Cosmetic.
- **G9, deleting a building in co-op.** It doesn't destroy terrain resting on the building: the replayed deletion
  carries only the buildings. The same on every computer; a gameplay gap, not a desync. It needs a terrain list in
  `BuildingsDeconstructedEvent` and a colony rule for it.
- **G10.** `ShowOptionsMenuEvent` can push the options box twice. An unmark clears another player's drag highlight.
- **Plantable tool locks.** On a guest these may show slot 0's unlocks until something refreshes them. Display only.
- **By design (unchanged):** other mods are a warning only, dev mode's other tools act locally, and a lever can't be
  held on in co-op.

---

## 8. What to play first (added to ALPHA-TEST-SCRIPTS.md)

In order:

| Line | Tests |
|---|---|
| Script B 8t | The Earth Repopulator with one player at 15 FPS (#46's checklist) |
| 8u | Dynamite by working lumberjacks; demolish while paused, then unpause |
| 8v | A fast guest behind a host easing off: no stop-go |
| 8w | A direct-IP guest's link cut: the others play on, and it is dropped after 30 s |
| 8x | Spring-return levers |
| 8y | Buildings from a mod on one side only |
| Script C 1b | Boosted speed with high ping |
