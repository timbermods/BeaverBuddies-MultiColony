# Reviewer D: cost at late-game size, long sessions, compatibility, and the reporting (1.4.0-rc1 review of beta24)

Plan: [`REVIEW-PLAN-1.4.0-beta24.md`](../REVIEW-PLAN-1.4.0-beta24.md), leads S1 to S12, C1 to C3, sweep 6 (§5.2), §5.3 item 3
and §5.4. Planning note 2 ([`planning/2-hot-paths.md`](planning/2-hot-paths.md)) was the starting inventory; every line of
it that is used below was read again.

- **Branch:** `worktree-agent-ad93964cc229099a1`, from `1323b8c` (beta24, the P-1 fix and the empty check files).
- **Nothing here ran the game.** Every number is either read from the code or measured outside the game: in
  RuntimeChecks (the mod's compiled DLL with the game's own assemblies, on .NET 8) or StabilityTests. The game runs on Unity's
  Mono, which is slower; the ratios are what carry over. Script P (§5) measures the rest in Kyler's own sessions.
- **Checks:** StabilityTests 418 → 422, RuntimeChecks 362 → 382 on both builds, 0 warnings on both builds. The new
  checks are in `StabilityTests/RcPerformanceChecks.cs` (with the model in `StabilityTests/RcPerformanceLockstepModel.cs`)
  and `RuntimeChecks/RcPerformanceRuntimeChecks.cs`. Every fix's check was run against beta24's Release Steam DLL: 16 of
  the 20 new RuntimeChecks fail there (365/382; the other failure is P-1's, from the base), the four benchmarks pass and
  print beta24's numbers. The three StabilityTests fix checks cannot build without their fixes (§1.10).
- **Wire and save:** no change to either anywhere below. Every player must still run the same build (the handshake), as
  always: the daily colony check's value is computed differently now (D-S12), the same on every computer of this build.

Line numbers: `mod file:line` are this branch's unless marked "beta24"; the game's are the decompiled 1.1.2.4
assemblies in the session's scratchpad (`<Assembly>.decompiled.cs`).

**Edits outside reviewer D's files**, each small and needed by a fix (for the merge):
- A's: `Colonies/ColonyWorkingHours.cs` (the working-hours spot sampled, 3 lines, D-S1); `Fixes/TickOnlyArrayFix.cs` (one
  condition, the dead flag, D-S10f).
- B's: `Colonies/ColonyHandover.cs` (`ColonyPresenceEvent.Compare` calls `DailyCheck`, 2 lines, D-S12).
- C's: `Colonies/ColonyTrading.cs` (the template cache weakly keyed, D-S7b).
- Unassigned: `Steam/SteamOverlayConnectionService.cs` (D-S7c), `Events/ConnectionEvents.cs` and
  `Connect/DesyncDialogPlan.cs` (D-S11).
- Shared: `Localizations/enUS_BeaverBuddie.csv` (the *Always Use Detailed Logging* tooltip's second line, D-S11).
- Tests: `StabilityTests/Stubs.cs` (the stub path follower has corners with times, D-S9), `RuntimeChecks/ScopeChecks.cs`
  and `RuntimeChecks/TraceChecks.cs` (run inside a session, D-S10c and D-S7a).
- Expect a merge conflict in `Colonies/ColonyDiagnostics.cs`: B (F6) and C (T3) add to the daily check, which D-S12 moved
  into `DailyCheck` and whose entity walk it reshaped.

---

## 1. Findings

### 1.1 What the mod costs in every game, co-op or not (S10)

**D-S10a: every random draw in every game looked a singleton up.** Confirmed; performance; everyone (single player too).
- Evidence: the three `RandomNumberGenerator` prefixes called `ShouldUseNonGameRNG`, which did
  `GetSingleton<DeterminismService>()` and gathered six facts before `RandomSourceRules.Choose` answered "the game's"
  for any draw outside a session (beta24 `DeterminismService.cs:176-210, 318-376`; `RandomSourceRules.cs:32`).
- Fix: `ShouldUseNonGameRNG` returns first from `EventIO.IsNull` (`DeterminismService.cs:176-182`). The answer outside a
  session is the same (Choose says Game first); inside one nothing changes.
- Check: `D-S10: outside a multiplayer game a random draw's check answers before any singleton lookup` (IL: the first
  call is `EventIO.get_IsNull`, before any `GetSingleton`). Fails on beta24.
- Test line (Script P, P1): none needed beyond P1's with/without recording.

**D-S10b: every new entity in every game drew 16 numbers from Unity's random state.** Confirmed; performance; everyone.
- Evidence: `GuidPatcher.Prefix` made every `Guid.NewGuid` from `UnityEngine.Random.Range` × 16 unless a thread-static
  flag said otherwise (beta24 `DeterminismService.cs:807-847`). The game calls `Guid.NewGuid` for every entity it makes
  without an ID (`Timberborn.EntitySystem` `EntitySetup.Builder.Build`, 706-715): births, crops, stacks, buildings.
- Fix: outside a session the game's own GUID is kept (`DeterminismService.cs:876-887`). Every co-op game installs its
  EventIO before its scene loads, so every ID made while a co-op game loads still comes from the shared random state:
  host `ServerHostingUtils.cs:162` (then `io.Start`, then the load); waiting room `LobbySession.cs:263` (`EventIO.Set`
  then `LoadScene`); guest `ClientConnectionService.cs:153` (at connect, before the map arrives).
- Check: `D-S10: outside a multiplayer game a new entity's ID is the game's own, with no draws from the game's random
  state`. On beta24 it throws (the draw is Unity's native call).

**D-S10c: the eleven "not gameplay"/"gameplay" markers counted in every game.** Confirmed; performance (small); everyone.
- Evidence: each prefix and finalizer did two dictionary operations (beta24 `DeterminismService.cs:525-691`), read only by
  `ShouldFreezeSeed`, which is session-only.
- Refuted part: planning note 2 said `SoundEmitter.Update` runs "per emitter per frame". It runs only while that emitter
  has a callback sound playing: `SoundEmitter.Awake` sets `enabled = false`, and only `Start2D(..., callback)` sets it
  `true` (`Timberborn.SoundSystem` 846-851, 863-868). The other markers run per sound, per input frame, per model made.
- Fix: every marker prefix returns at `EventIO.IsNull` with `__state = false`, so its finalizer does nothing
  (`DeterminismService.cs`, the ten `__state = false; if (EventIO.IsNull) return;` prefixes). A session started or ended
  inside a marked call stays balanced: the finalizer undoes only what its prefix did.
- Checks: `D-S10: outside a multiplayer game the not-gameplay and gameplay markers count nothing` (fails on beta24: all
  ten counted); `ScopeChecks` (the nesting and exception checks) now run inside a session (`ScopeChecks.Multiplayer`).

**D-S10d: `TickableEntity.Tick` was patched in every game, two calls per ticking entity per tick.** Confirmed;
performance; everyone.
- Evidence: the prefix and postfix only remembered which entity ticks (beta24 `DeterminismService.cs:227-241`), and the
  only reader is the detailed-logging trace of a random draw (`TraceGameDraw`, `:213-225`). `TickableEntity.Tick` runs
  for every ticking entity every tick (`Timberborn.TickSystem` 663-678, 718-731).
- Fix: no `[HarmonyPatch]` any more. `TickableEntityTickPatcher.EnsurePatched` (`DeterminismService.cs:230-276`)
  patches it once per program run, from `ReplayService.DoTick` at a tick start, only while detailed logging is on
  (`ReplayService.cs:934`). If it fails, only the trace loses the entity's name.
- **D-new-1 (found while fixing it):** a Harmony patch made during a session asks for GUIDs (MonoMod names what it makes
  with them), and in a session every GUID is drawn from the game's random state. Patching on one computer mid-session
  would have moved that computer's random state alone: an instant desync. PerformanceLog's `AutoWatch` guards itself
  the same way (`PerformanceLog/source/Watch.cs:219-233`). `EnsurePatched` runs inside the new
  `GuidPatcher.WithRealGuids` (`DeterminismService.cs:863-870`) and puts `UnityEngine.Random.state` back after.
  The mod patches nothing else at runtime (the other `Patch` calls are in `StartMod`).
- Checks: `D-S10: TickableEntity.Tick is patched only once detailed logging is on in a multiplayer game` (no class
  targets it; `DoTick` calls `EnsurePatched` behind `Settings.Debug`; `EnsurePatched` uses `WithRealGuids` and saves and
  restores the random state); `D-S10: in a session, a GUID asked for inside WithRealGuids (the on-demand patch) is a real
  one`. Both fail on beta24.

**D-S10e: the `Time.time` native detour is installed in every game.** Confirmed; performance; everyone. **Left.**
- Evidence: `TimeTimePatcher.Install` at `StartMod` (`Plugin.cs:184`); outside a session each `Time.time` read goes
  native → managed → `Time.timeAsDouble` (`DeterminismService.cs:1016-1022`). The game reads `Time.time` about once per
  animated character per frame (`MovementAnimator.Update`, `Timberborn.CharacterMovementSystem` 708) plus a handful of
  others (grep of all 497 assemblies: 20 sites, most per event).
- Why left: it cannot be gated more cheaply than it already is (its first line is the `EventIO.IsNull` check); only
  installing it at the first session would help single player, and that moves a native detour, which "irretrievably
  overwrites the method" (`:1005-1009`), from the mod's start (where a failure stops the mod loudly) to the moment a
  player hosts or joins (where a failure would leave a session without its clock). Script P's P1 measures what it costs.

**D-S10f: dead code.** Confirmed; removed.
- `TickWathcerService` (never bound), `DeterminismPatcher.PatchDeterminism` and `NonGamePatcher` (never called),
  `GameSaveHelper` with `IsSavingDeterministically` (never set), and its two patches: the process-wide
  `DateTime.ToString(string)` prefix, which every log line's timestamp went through (`Plugin.GetWithDate`), and the
  `DateSalter.Save` prefix, which always returned true (beta24 `GameSaveHelper.cs:11-41`). `TickOnlyArrayFix.cs:19` (A's
  file, one condition) no longer reads the flag.
- Check: `D-S10: the dead code is gone: ...` (fails on beta24).

**D-S10g: `ParameterProvider.GetParameters` postfix.** Refuted as a per-tick cost. It runs once per injected component,
when an entity or singleton is made (load, births, placements), not per tick. It must run in every game: it wraps the
listed classes' random number generators as they are made, and a singleton made before a session would otherwise
draw from the game's state in one.

**D-S10h: `Manufactory.IncreaseProductionProgress` prefix and finalizer** (`ColonyScienceService.cs:424-430`, A's
file). Confirmed as ungated; **left**: two thread-static reads and one write per workshop per tick, a few µs a tick at
200 workshops. A Harmony finalizer wraps the method either way.

**D-S10i: `ColonyMarks`' planting-unset postfix is ungated** (`ColonyMarks.cs:205-209`): a singleton lookup per unset
tile, in every game. **Left**: the game unsets planting marks when a player unmarks or demolishes, when something is built over
them, or when the terrain under them changes (`Timberborn.Planting` 1280-1306), never per tick.

### 1.2 Per frame (S9)

**D-S9: the walking animation searched every walker's path from its first corner every frame.** Confirmed; performance;
everyone in co-op. Planning estimate corrected.
- Evidence: `AnimationFixes.cs:75-76` (beta24) set `_nextCornerIndex = 0` before `AnimatedPathFollower.Update`. The path
  is not the whole route: `PathFollower.MoveAlongPath` rebuilds it every tick (`SetNewPath`, which also resets the
  index) with a corner every `MaxMovementStep` = 0.1 tile walked (`Timberborn.CharacterMovementSystem` 833, 867-903,
  316-325): about 16 to 30 corners a tick walking, more on a zipline or tubeway.
- Fix: `ResumeSearch` (`AnimationFixes.cs:118-126`) keeps the cached corner unless the clock went back past the corner
  before it. Corner times never decrease (`MoveAlongPath` adds `tickDeltaTime - remainingTime`, which only grows;
  `AddSmoothingAnimatedPathCorner` ends later still, 997-1007), so the forward search finds the corner a search from
  the first would. Drawing only: the simulation's position is the tick's (`TEBPatcher` resets it before each bucket).
- Checks: `D-S9: a walker's animation goes on from its cached corner each frame, and starts again only when its clock
  goes back` (StabilityTests; the stub follower counts corners looked at, 2 instead of 3 per frame here); `D-S9: on the
  game's own AnimatedPathFollower, going on from the cached corner draws what a search from the first draws` (400 random
  walks, 32,000 frames, same corner, segment and speed as beta24's rule, with the clock going back at random);
  `D-S9: micro-benchmark, 600 walkers ...`: 77 to 113 µs a frame with beta24's rule against 54 to 76 µs (three runs,
  a busy computer), both including the game's own `Update`.

**D-S9b: the alert filter read "does this computer filter" and this player's seat for every alert, every frame.**
Confirmed; performance; separate colonies.
- Evidence: `StatusAggregator.UpdateSingleton` asks `IsVisible` of every alert status every frame
  (`Timberborn.StatusSystem` 792-849); the mod's postfix called `ColonyViewService.Active` (a flag, three singleton
  lookups and the seat) and `IsOwn` (the seat again) for each (beta24 `ColonyView.cs:295-311`).
- Fix: `ColonyViewService.ActiveThisFrame` reads both once a frame (`ColonyView.cs:89-112`), after the same two static
  flags as before (single player and shared games stop there); `IsOwnFor` takes the seat.
- Check: the §5.3 spot check names its sampled profiler spot. Display only; no behaviour check beyond the unchanged
  `IsOwn` ones (`ColonyRuntimeChecks`).

**D-S9c: the road overlay walks every entity every 0.5 to 3 s while a building tool is in hand**
(`ColonyRoadOverlay.cs:95-130, 187-207`). Confirmed; **left**. Two component lookups per entity and four per path per
redraw, timed by the existing "Road overlay drawing" spot, so Script P shows it. Keeping a list of paths from the
events it already listens to would cut the walk to the paths, but the event order at load was not verified here.

**D-S9d: placement previews** (`ColonyPlacementValidator.cs:23-69`): per preview block per frame, a small list and the
road-conflict walk over districts. **Left**, measured by the existing "Placement previews" spot.

### 1.3 The daily colony check (S12)

**D-S12: every computer walked every entity twice a day for the colony check.** Confirmed; performance; separate
colonies.
- Evidence: `ColonyDiagnostics.Tick` took the fingerprint at the turn of the day (beta24 `ColonyDiagnostics.cs:134-158`),
  and `ColonyPresenceEvent.Compare` took it again as the host's day played, the next tick (beta24
  `ColonyHandover.cs:451`). Each walk asked every entity for four or five components, and every building for four
  entity-ID lookups more (beta24 `ColonyDiagnostics.cs:165-221`).
- Fix: one walk a day, where the comparison is made: `DailyCheck(day)` (`ColonyDiagnostics.cs:151-176`) is called by
  `ColonyPresenceEvent.Compare` (`ColonyHandover.cs:452`, a one-line change in B's file), and logs the `[Colony] Check`
  line and keeps it for the report from there. The same point of the same tick on every computer, as the comparison
  already was. The walk asks each entity for two components; the crossing's two come from the game's registry of
  District Crossings (both are its decorators: `Timberborn.DistributionSystem` 581-589; registered at initialization,
  `Timberborn.EntitySystem` 381, never for previews, which are never `Initialize`d); each district is hashed once.
  A profiler spot times it.
- Measured (RuntimeChecks, 17,652 entities with the game's own component caches): about 1.4 ms × 2 a day on beta24,
  about 1 ms × 1 now (0.93 to 1.28 ms over three runs), on .NET 8, without the per-building entity-ID lookups beta24 also made (they need live Unity objects). The
  planning estimate of 50 to 80 ms was pessimistic; on Mono expect a few ms.
- What changes for a player: the `[Colony] Check day N` line is logged one tick later than before (as the host's day
  plays), on every computer alike. With no presence event a day (the host could not send it), no line that day.
- Checks: `D-S12: each computer walks every entity once a day for the colony check, as the host's day plays` (fails on
  beta24); `D-S12: micro-benchmark, ...` (prints both builds' numbers).

### 1.4 Road networks (S4)

**D-S4: the district-conflict walk ran after navigation updates that changed no road.** Partly refuted, partly confirmed;
performance; separate colonies.
- Refuted: preview updates never reach it. It is an `ISingletonNavMeshListener`, which the game tells only the regular
  updates (`NavMeshUpdateNotifier.NotifyOfNavMeshUpdates`, `Timberborn.Navigation` 5250-5254); previews and instant
  updates go to their own listeners (5256-5265). In co-op the regular update is built once a tick, in
  `NavigationSynchronizer.Tick` (5921-5925; the mod's frame replacement processes previews only,
  `InstantNavMeshFix.cs:23-34`).
- Confirmed: every regular update walked, terrain-only ones included (beta24 `ColonyRoadNetworks.cs:157-181`).
- Fix: it walks only when `navMeshUpdate.UpdatedRoads` (`ColonyRoadNetworks.cs:166`). Every change that can join or part
  two districts' roads puts a road node in the update: road edges (`Timberborn.Navigation` 4253-4254), district
  obstacles and district centers (`DistrictChange.ApplyChange`, 2271-2296). Display only: the walk only raises a notice.
  A profiler spot times it.
- Measured: the game's `DistrictConflictDetector` over 4,500 road nodes in 12 districts, 0.11-0.13 ms a walk (.NET 8).
- Checks: `D-S4: a navigation update that changed no road does not walk the road networks` (fails on beta24: no spot,
  no filter); `D-S4: micro-benchmark, ...`.

### 1.5 Frame-ending interrupts (S2, P-5)

**D-S2: every deletion in a tick ends the frame's ticking; creations never do in a game.** Confirmed (deletions),
refuted (creations); performance; everyone in co-op. **Mechanism left, counted and reported.**
- Creations: `EntityComponentInstantiatePatcher` interrupts only for an entity made with a preset ID
  (`DeterminismService.cs:924`, `!entitySetupBuilder._id.HasValue` returns). In a game only loading sets one
  (`Timberborn.WorldPersistence` 826); the other caller is undo (`Timberborn.EntityUndoSystem` 530), which is off in the
  Game scene (`Timberborn.GameScene` 216, `UndoAllowed => false`). So births, crops, stacks and planes never interrupt.
- Deletions: `EntityDeletionEndsFramePatcher` (`Fixes/TickTimingFixes.cs:26-36`). Harvests delete when their stack is
  picked up (`Cuttable`, `Timberborn.Cutting` 257-282), dead characters, emptied stacks, blasts, demolitions,
  fireworks and planes. Kept: it exists so that Unity's end-of-frame destroy lands at the same point on every computer
  (beta12's D2), and nothing short of destroying at once can give that. Raising the one-tick cap on the refund does not
  raise the ceiling (below): the frames are the limit, not the time.
- The ceiling, from reading and from the model (D-S5): a computer cannot run more than `fps / (1 + interrupting buckets
  per tick)` ticks a second, and the interrupts cost nothing while that is above what the speed asks for. Speed 7 asks
  11.7 ticks/s, or 5.7 with the game's own large-colony limit (the default from 200 characters). At 60 fps, 2
  interrupting buckets a tick allow 20 ticks/s; 10 allow 5.5. The model: 10 a tick with the limit removed ran both
  computers at 7.1 ticks/s instead of 11.7.
- Reported (§5.3): `TickingService` counts interrupt requests, frames cut short, buckets given back and buckets lost to
  the cap (`ReplayService.cs:1036-1060, 1223-1234`); the Ctrl+Shift+J report and the daily `[Perf]` line print them.
- Check: `D-S2: frames cut short by a creation or deletion, and the game time the one-tick cap throws away, are
  counted` (a real `Ticker`: 100 buckets given back on 64 waiting, 35 lost; fails on beta24).
- Script P, P3: read "Frames cut short ... a tick" in both reports.

### 1.6 Lockstep when the simulation is the limit (S5)

**D-S5: no spiral; the slower computer sets the pace; boosts leave a guest seconds behind.** Confirmed by model; kind:
performance; everyone in co-op.
- The model (`StabilityTests/RcPerformanceLockstepModel.cs`) runs host and guest frame by frame as Unity and the mod run
  them: the frame's time capped at 1/3 s, times the time scale with `GameSpeedThrottler`'s scale
  (`Timberborn.Characters` 501-512; spec 30 → 200 characters, 1.0 → 0.4) and `SpeedManager.ScaleSpeed`
  (`Timberborn.TimeSystem` 858-865), buckets of 0.6 s / 129; the tick start as a refunded bucket; the guest's tick gate
  and its lost buckets (`ReplayService.cs:1146-1184`); the guest's hold after 0.1 s out of events (`:879-901`);
  interrupts with the capped refund; `CatchUpSpeed.For` at the host's pace; `HostPacing` sampled once a second.
  Costs are estimates: host 40 ms a tick, guest 60 ms, 8 ms of rendering a frame, 50 ms of latency.
- Results (after a minute of warm-up, 2 minutes measured):

| Case | Host / guest ticks/s (wanted) | Guest behind p50 / p95 / max | Guest fps (worst frame) |
|---|---|---|---|
| Speed 7, 200+ characters (the default limit) | 5.7 / 5.7 (5.7) | 0 / 1 / 1 | 83 (18 ms) |
| Speed 7, limit removed | 11.7 / 11.7 (11.7) | 1 / 2 / 2 | 38 (75 ms) |
| Speed 7, limit removed, guest twice as slow | 11.7 / 11.7 (11.7) | 1 / 2 / 2 | 38 (78 ms) |
| Speed 7, limit removed, 10 deletions a tick | 7.1 / 7.1 (11.7), host at 65% | 10 / 18 / 20 | 72 (60 ms) |
| Boost 15 (speed 22), limit removed | 13.0 / 13.0 (36.7), host at 40% | 29 / 49 / 52 | 28 (143 ms) |
| Boost 15, the default limit | 12.8 / 12.8 (15.7), host at 90% | 31 / 49 / 51 | 29 (104 ms) |

- Budget B4 (the guest within `BufferTicksFor(speed) + 2` for 95% of ticks, within 10% of the rate) holds at speed 7
  with the game's limit, and with it removed. A boost the computers can't carry settles where the host's easing
  thresholds (scaled with the speed, `HostPacing.Scaled`) put it: 30 to 50 ticks, 2 to 4 s at speed 22, so a guest's
  own actions come back seconds late. That is the design since beta12, not a spiral: the lag stays bounded (the host
  holds beyond `StopTicks`), and nothing drifts over time. Catching up does not help a computer that is CPU-bound: a
  faster time scale only makes its frames longer (the worst frames above).
- A model bug worth a note: an early version paused the guest whenever it had waited 0.1 s, without the "out of events"
  half of the condition, and the session deadlocked (host holding, guest still). The mod has that half
  (`ReplayService.cs:892`: `io.IsOutOfEvents && (!liveGuest || heldLong)`); the model now does too.
- Check: `D-S5: lockstep when the simulation is the limit: a late game keeps pace at speed 7, and no case spirals`
  (asserts B4 in the default case, and that every case ends bounded). Script P, P3 replaces the estimates.

### 1.7 Saves, rehosts, joins (S6)

**D-S6: at late-game size the mod's part of a save or rehost is milliseconds; the game's own save is the cost.**
Confirmed from reading and measurement; performance; everyone in co-op. **Left**, measured in Script P, P6.
- A late save is small: `R-late.timber` is 1.15 MB (world.json 8.7 MB inside), `R-blast` 1.07 MB (9.2 MB),
  `R-long` 0.6 MB (9.7 MB). The mod's share of a save: in `R-long` (two colonies) 169 `ColonyStamp`s, 6.6 KB, and
  `ColonyMarks` 25 KB, of 3.3 MB of components.
- Keeping the map bytes all session ("TODO remove map", `IO/ServerEventIO.cs:52, 238`) holds about 1 MB (the closure,
  and `TimberServer.lobbySave` for a waiting room). Left: nobody can join after the first tick anyway.
- Hashing them on the game thread (`DeterminismService.InitGameStartState`, `DeterminismService.cs:336-344`; `TimberNetBase.GetHashCode`,
  `TimberNet/TimberNetBase.cs:663-671`): 0.76 ms for 1.15 MB on .NET 8, four times on a guest.
- `ColonyMarks.Save` sorts the marks three keys deep and writes a string each at every save (`ColonyMarks.cs:61-72,
  169-172`): a few ms at 10,000 marks.
- Sending at 1 MB/s over direct IP: about a second for a late save.
- The waiting room reads the save and the guest loads it: the game's own load time, which Script P, P6 measures.
- LateGamePerformance's background save changes none of this (C1-3 below).

### 1.8 Long sessions (S7)

A read-only sweep of every collection in `BeaverBuddies/` and `TimberNet/` (static and per-service) for growth over a
session and across sessions. Three grew without bound; fixed. The rest are bounded (§3).

**D-S7a: detailed-logging traces grew without bound on some computers.** Confirmed; memory; everyone with detailed
logging on.
- Evidence: the trace history (`DesyncDetecterService.traces`, a stack trace per trace) is let go only by the host each
  tick and by a guest as the host's traces arrive (beta24 `DesyncDetecterService.cs:83-98, 210-214`). It grew for the
  whole session on a guest whose host had logging off; in single player or after a desync (nothing sends or checks); and
  switching logging on mid-session made one empty tick for every tick played so far (`StartTick` from tick -1), which a
  host then sent all at once.
- Fix: at most 128 ticks are kept (`DesyncDetecterService.cs:65, 150-152`; the comparison counts from the newest tick,
  so a tick let go reads as checked); a jump of more than that starts at the new tick (`:137-141`); nothing is traced
  outside a session (`:174`); a guest without logging warns once, not every tick, that the host's traces arrive.
  `TraceChecks` now run inside a session.
- Check: `D-S7: detailed-logging traces stay bounded: ...` (beta24 keeps 1,001 ticks of 1,000; fails).

**D-new-2: switching detailed logging on mid-session on one computer stopped the session with a false desync.**
Confirmed (by the check, on beta24's DLL); desync (false); everyone with detailed logging on both computers.
- Evidence: a computer that turns logging on mid-session starts `StartTick` from tick -1 and makes a list for every tick
  played, each holding one trace, `"Tick {tick} started"` with the *current* tick (beta24
  `DesyncDetecterService.cs:116-121`). The other computer, logging all along, sends its traces of the ticks before; the
  first comparison (`VerifyTraces`) finds "Tick 2000 started" where the other has "Tick 1999 started", calls it a desync
  and stops the session (`TraceLoggedForTickEvent.Replay`, `:43-50`). Either side switching on does it: a host sends its
  made-up ticks, a guest compares against them.
- Fix: D-S7a's jump: a computer switching on starts at the current tick, so the earlier ticks read as already checked.
- Check: `D-new-2: switching detailed logging on mid-session never reads the other player's earlier traces as a desync`
  (fails on beta24).
- Test line (Script L or M, optional): both players turn *Always Use Detailed Logging* on in Mod Settings during a
  small co-op game, one a minute after the other; the game should go on with no desync dialog.

**D-S7b: the Trading Post template cache kept every loaded game's building specs.** Confirmed; memory; everyone.
- Evidence: `TradingPosts.isTradingPostTemplate` was a static `Dictionary<BuildingSpec, bool>` never cleared (beta24
  `ColonyTrading.cs:33, 47-59`); the specs are made again for every game loaded (the template and blueprint systems are
  bound per scene).
- Fix: a `ConditionalWeakTable` (`ColonyTrading.cs:32-36, 50-54`, C's file, the declaration and one method body).
- Check: `D-S7: the Trading Post template cache and the Steam callbacks do not keep a left game or menu alive`.

**D-S7c: the disposed Steam callbacks kept every scene's services alive.** Confirmed; memory; everyone on Steam.
- Evidence: `SteamOverlayConnectionService.callbacks` is static; each main menu and game disposed the previous
  callbacks but never cleared the list (beta24 `Steam/SteamOverlayConnectionService.cs:32, 65-76`). Steamworks'
  `Callback<T>.Dispose` unregisters without dropping its delegate, which holds the old service, its `ClientConnectionService`,
  `PanelStack` and `EventBus`.
- Fix: `callbacks.Clear()` after disposing (`:68-71`, an unassigned folder).
- Check: the same D-S7 check (Steam build only; the other build has an empty service).

### 1.9 Detailed logging at late-game size (S11)

**D-S11: detailed logging cannot keep up with a large colony, and the desync dialog could offer it there.** Confirmed;
session-stopper when used; everyone with a build that has an upload token.
- Measured (RuntimeChecks, `D-S11: micro-benchmark, ...`), for a tick of 1,500 traces (600 beavers' running
  behaviours, `DesyncPatches.cs:350`, and the game's draws, `DeterminismService.cs:217-224`, each with a stack trace):
  9.3 ms to trace (6.2 µs a trace with a shallow stack; the game's are deeper); the host's send of them 3.7 ms and
  248 KB of JSON a tick, 2.8 MB/s at speed 7, against direct IP's 1 MB/s pacing; the water map hashed and copied every
  tick (`DesyncPatches.cs:251-276`, `WaterDiagnostics`) 13.8 ms for 256 × 256 × 3 columns. About 27 ms a tick in all
  on .NET 8, and guests fall behind the host's traffic.
- Who can turn it on: Mod Settings' *Always Use Detailed Logging*, and the desync dialog's *Enable Logging*, which only
  a build with an upload token shows (`DesyncDialogPlan.ReportButtonKey`; `pat.properties` is gitignored and beta24's
  released DLL embeds none).
- Decision and fix (the safe choice): the dialog never offers to turn it on in a game of 200 or more beavers and bots,
  the game's own large-colony line (`GameSpeedThrottlerSpec.MaxPopulation`): `DesyncDialogPlan.cs:73-89`
  (`largeGame`, `LargeGameCharacters`), `ConnectionEvents.cs:269-273` (`ColonyDiagnostics.CharactersInDistricts`). With
  logging already on, posting a report is still offered. The Mod Settings tooltip says a colony that size can't keep up
  (`enUS_BeaverBuddie.csv:67-68`, 101 characters on its line). A lighter level or a limited number of ticks was not
  built: nothing public can reach the dialog's button, and the long-session line (§5.3) now covers what a player needs.
- Check: `D-S11: in a large game the desync dialog never offers to turn detailed logging on; with it on, posting stays`
  (StabilityTests).

### 1.10 The reporting Kyler's sessions read (§5.3 item 3, S1)

Implemented. Nothing here is read by the simulation; nothing costs anything outside a co-op game except one day-number
read a tick in `ColonyDiagnostics.Tick`.
- **Interrupts per tick** (D-S2): counted by `TickingService`.
- **Profiler spots on every per-tick and per-frame hot path** (`ColonyProfiler`): new spots for each bucket's hashes and
  walker sync (`TEBPatcher`), the whole of a frame's ticking, the tick start (replay and send), the walking animation,
  the alert filter, the daily check and the road-networks walk; existing ones kept (working hours, resource searches,
  builder checks, previews, road overlay, trading posts, daily checks). The busiest are **sampled**: one call in 16 is
  timed and scaled up, marked "(~)" in the report (`ColonyProfiler.DeclareSampled`, `StartSampled`, `StopSampled`).
  Working hours (A's file, two lines), walking and alerts use it. **D-S1:** the working-hours spot cost about as much
  as the check: timing every call costs 34 ns, sampling 3.7 ns (StabilityTests, `D-S1: a sampled profiler spot ...`).
- **The mod's collections and the heap:** the report's `Memory:` line and the daily `[Perf]` line give the heap, GC
  counts (and today's), the traces kept, the water snapshots' MB, the walker records' ticks, the colony changes counted
  and the marked tiles (`ColonyDiagnostics.cs:360-432`).
- **The daily `[Perf]` line**, once a game day in a co-op game, always (one line of about 400 characters):
  `[Perf] Day 12 (tick 9216): Frames cut short by a creation or deletion in a tick: 12 in 768 ticks (0.02 a tick), of
  3840 frames that ticked | buckets lost to the one-tick cap 0 (0.0 ticks of game time), given back since load 1234 |
  collections today gen0 +35 gen1 +3 gen2 +1 | heap 812 MB, ...`. Kyler's long session (release bar 7) reads memory and
  interrupts from it without detailed logging.
- Checks: `§5.3: the report and the daily line carry the interrupts, the heap and the mod's growing collections`;
  `§5.3: every per-tick and per-frame hot path of the mod has a profiler spot, the busiest sampled`. Both fail on
  beta24.

**Checks that fail on beta24's Release Steam DLL** (this branch's RuntimeChecks run on it: 365/382): the six D-S10
checks, both D-S9 (the benchmark times beta24's rule beside the new one, so it needs the new one), the D-S4 filter, the
D-S12 walk, both D-S7, D-new-2, the D-S2 counters and both §5.3 checks: 16. The D-S4, D-S12, D-S8 and D-S11 benchmarks pass on
both and print both numbers. StabilityTests compiles the mod's sources, so its D-S9 check (3 corners looked at a frame
instead of 2 with beta24's rule), D-S11 (no `largeGame`) and D-S1 (no sampled spot) cannot pass, or build, without the
fixes. The D-S5 model check passes on beta24's sources too: it models, it fixes nothing.

### 1.11 Allocations (S3)

**D-S3: nothing in reviewer D's files allocates per entity or per tile on a per-tick or per-frame path.** Found sound.
- Per tick: `ReplayService` makes the heartbeat, one group and its list (`:687-699, 966-971`), and flattens the tick's
  events (`:352-354`); `TEBPatcher` allocates nothing after a bucket's first tick (`EntitySlotCache`, a cached delegate).
- Per frame: `AnimationFixes`, the alert filter and `ColonyDiagnostics.UpdateSingleton` allocate nothing.
- `DoEntityPrefix` allocates two closures before any check (`Events/ReplayEvent.cs:226-238`), refuted as a per-tick cost:
  recorded methods are reached only from the interface, replays and load (beta12 R4); the simulation's own calls go
  through `RunAsSimulation` (spring-return levers, `TickTimingFixes.cs:57-67`), a lever at a time.
- `NonTickRandomNumberGenerator` allocates a closure per draw of the listed visual classes (`DeterminismService.cs`,
  `GetNonGameRandom(() => ...)`): per model made, not per tick. Left.
- `MixedFactions.Spec` (B's area) allocates per call; per path painted and per model, not per tick (B's F8).

### 1.12 The unoptimised shipping build (S8)

**D-S8: Release Steam is built without the JIT optimizer; the mod's own hot code is small either way.** Confirmed;
performance; everyone. **Reported; the build config is unchanged** (decision D4).
- The Release Steam DLL carries `DebuggableAttribute` with the optimizer disabled; Release does not
  (`D-S8: micro-benchmark ...` prints which). Mono has the same switch (an assembly whose attribute disables the
  optimizer is not inlined from), as far as this review can tell without running it; P1b answers what it costs.
- Measured on .NET 8, per call, unoptimised / optimised: a bucket's entity-order hash (156 entities) 205 / 581 ns (slower
  optimised in both runs; not explained, and .NET 8's JIT is not the game's), a random draw's source 4.6 / 4.3 ns, the
  catch-up speed 19.7 / 17.8 ns, a profiler spot 40 / 37 ns, the heartbeat as JSON 5.6 / 5.6 µs. No consistent gain
  outside the game; the in-game answer is Script P's P1b.
- `PatchGateChecks` (which reads IL and names the unoptimised build's patterns) passes on the optimised Release build:
  RuntimeChecks runs on both builds, 382/382 each.

### 1.13 Compatibility (C1, C2, C3)

**C1: Timber Together with LateGamePerformance 0.4.28** (read in `C:/Users/Kyler/code/LateGamePerformance/source`; not
edited). Compatible when every player runs the same LateGamePerformance version. Findings for Kyler, in that mod:
1. **C1-1 `Ticker.Update` catch-up** (`CatchUp.cs:28-38, 90-127`): scales the frame time handed to the ticker; never
   touches `_accumulatedDeltaTime`, `TickBuckets`, `FinishFullTick` or `TickOnce`. Timber Together's tick gate and refund
   stay intact. Effect: a guest recovering from a hitch catches up somewhat slower (at most twice its recent frame time
   a frame). No co-op gate needed. Pacing only; confirmed.
2. **C1-2 `IdleEntities`** (`IdleEntities.cs:31, 203-279`): walks the game's own `_tickableEntities` and skips entities
   with no enabled tick component, a state only `EnableComponent`/`DisableComponent` change; runs Last, so Timber Together's
   bucket hash reads the same list. Reads nothing per computer. None; confirmed.
3. **C1-3 `BackgroundSave`** (`BackgroundSave.cs:313-328, 573-747`): does not hook `GameSaver.Save`; snapshots on the game
   thread inside whatever `Save` runs (Timber Together's deferred one), writes on a worker, calls back after the file exists.
   Instant saves (rehost, desync report) stay synchronous. None; confirmed.
4. **C1-4 `SaveSnapshot` pins `ColonyStamp` by IL hash** (`SaveSnapshot.cs:44-45, 138, 194-199`), read against beta2.
   `ColonyStamp.Save` changed in beta7 (it returns early in a shared game, `ColonyStamps.cs:36-41`), so the pin no longer
   matches: LateGamePerformance logs a warning each session and saves every building (each carries a `ColonyStamp`) on
   the main thread. Nothing is lost or stale. Performance only; fix in LateGamePerformance: re-read and re-pin. Confirmed.
5. **C1-5 hauling, home, yielder and district-count caches** (`HaulCache.cs`, `HomeSearch.cs`, `YielderSearch.cs:151-306`,
   `DistrictCounts.cs`): the yielder search replaces the game's at Priority.Last and searches the list Timber Together's
   colony filter already narrowed (`ColonySeparationPatches.cs:86-114`); it only drops candidates. The others keep the
   game's order per district and are invalidated by simulation hooks and ticks, never by time. None; confirmed (the
   haul cache's reaction to `HaulPrioritizable.Prioritized` on the clicking computer alone is harmless while the rebuilt
   list equals the cached one: plausible, none).
6. **C1-6 route maps, terrain maps and terrain search need the same LateGamePerformance on every player**
   (`RouteMaps.cs:51-53`, `TerrainMaps.cs:42-44`, `TerrainSearch.cs:27-34`, LateGamePerformance's own statements). A player
   without it, with another version, or with a feature that turned itself off (`TurnedOff.cs`) desyncs. Timber Together only
   warns on a mod-list difference (`ModCompatibility.cs:99-103`). Desync; confirmed. Doc line below.
7. **C1-7 a local preview can trigger a route-map scan on one computer** (`MapChanges.cs:44-71`): the postfixes on
   `DistrictMap.AddDistrictCenter/RemoveDistrictCenter/OnObstacleChanged/OnNavMeshUpdated` do not check which map they
   are on, and the preview district map calls them as the local player drags (`Timberborn.Navigation` 2616-2619). The
   next navigation tick scans the caches on that computer only; it builds a map only if one is unbuilt that no hook had
   flagged, which LateGamePerformance treats as its own bug ("Please report this line", `RouteMaps.cs:356-361`).
   Desync, low; plausible. Fix in LateGamePerformance: check the instance, as `SharedStateChangingPrefix` does
   (`RouteMapsBackground.cs:130-137`).
8. **C1-8 the worker timeout** (`TickWorkers.cs:95-100`): after 10 s the main thread rebuilds the unfilled maps while a
   stalled worker may still write one. Desync or crash, very low (needs a worker stalled 10 s); plausible.
9. **C1-9 terrain search history** (`TerrainSearch.cs:35, 136-139, 477-527`): a search made on one computer only would
   diverge; the only such caller is the game's dev-mode cursor tool. Low; plausible.
10. Everything else that reads the camera, frame time or threads (`UiThrottle`, `AnimatorCulling`, `SoundListenerSkip`,
    `WaterRendering`, the timing features) is display or measurement only. `AnimatorCulling` and Timber Together's Wonder
    timing (`WonderTimingFix.cs:550-580`) agree in either order. None; confirmed.

**C2: Kyler's other mods** (read; not edited). None desyncs when every player has the same mods, versions and settings.
- **OptimizedLocalHousing:** no patches; a tickable, saved singleton (`GameAdapter.cs:41-76`, `PassEngine.cs:79-91`),
  deterministic. Desync, low: it disables itself from the local mod list (`GameAdapter.cs:59-62`), so a player with a
  conflicting housing mod skips its `Tick`. Once a day: up to 32 path queries a tick, then an n × n matrix (2.9 MB at 600
  adults) worked through over about n ticks.
- **HungryPathing:** decides what beavers do from `HungryPathing.cfg` (`Config.cs:8-10`), which nothing compares
  between players: a different file desyncs (medium). Reads Timber Together's per-colony working hours by reflection, which
  matches this build.
- **PersistentWorkAreas:** draws only. None.
- **MixedStorage:** shares `SingleGoodAllower.Allow/Disallow` and `InputService.UpdateSingleton` with Timber Together;
  Timber Together's recording prefixes run first, the rest are void prefixes and finalizers. A refused Apply leaves its panel
  on "Queued" until reload (interface only, `StorageView.cs:452-457`).
- **TipsyTail:** no patches; adds a beaver need to both factions, so every player needs it.
- **PerformanceLog:** timing only; its `AutoWatch` puts the random state back after patching (the same issue as D-new-1).
  With this branch, Timber Together no longer has a prefix on every entity's tick for it to find.

**C3: a late single-player save hosted, then split.** Found sound from reading; Script P, P2 measures it.
- Hosting `R-late` as a shared colony: nothing is stamped (`ColonyStamp.Save` writes nothing in a shared game).
- Splitting it (*Allow founding colonies in a shared game*, a founding): `ColonyStamps.Begin(splittingSharedGame: true)`
  (`ColonyStamps.cs:128-146`) walks every entity twice (stamp, then track) in the registry's order, the same on every
  computer, stamps every placed building slot 0 and notes one digest change. About 11,000 entities and 3,500 buildings in
  `R-late`: a few component lookups each, milliseconds in one tick. A mixed game repaints each path as it is stamped
  (B's F8).
- Buildings no road reaches (levees, dams) wait unstamped and are looked at again every 16 ticks
  (`ColonyStamps.cs:161-184`): about 0.1 ms a tick at 1,000 of them. Left (beta1's backlog noted it).

### 1.14 Crash sweep (H1) of reviewer D's entry points

Every entry point in reviewer D's files that runs in a tick, a replay or a load, against the plan's late-game states (an
entity deleted earlier in the tick, a missing component, a district or colony gone, a character without a faction, an
older save, a singleton missing). No new throw path was found; the ones that could meet those states:
- **`TEBPatcher.Prefix`** (every bucket, co-op): an entity deleted in another bucket leaves this bucket's list at once
  (`TickableEntityBucket.Remove` while not ticking, `Timberborn.TickSystem` 706-716); one deleted in its own bucket
  leaves at that bucket's end and is destroyed before the next frame's buckets (D-S2). Walker, path follower and
  rotator are null-checked; the transform update is caught (`DeterminismService.cs:1122-1144`). A pilot on its plane
  (walker off) is handled. Sound.
- **Random-draw prefixes, `GuidPatcher`, the markers** (tick, replay, load): static reads, null-safe lookup, dictionary
  counts balanced by finalizers (`ScopeChecks`). The detailed-logging trace names the ticking entity, alive during its
  own tick. Sound.
- **`EntityComponentInstantiatePatcher`** (load only, D-S2): throws only after 100 duplicate IDs in a row. Sound.
- **`ReplayService.DoTick`, `TickBuckets`, `GiveBackBuckets`, `HeartbeatEvent.Replay`**: replays run through
  `ReplayExecution` (a throw aborts the session with a message: beta12); the new counters and spots are plain
  arithmetic; `GiveBackBuckets` checks the ticker and the bucket length. Sound.
- **`ColonyDiagnostics.Tick`** (every tick): the daily line is caught. **`DailyCheck`** (replay): called inside
  `Compare`'s catch; a crossing deleted this tick is no longer registered (`EntityComponentRegistry.Unregister` in
  `InternalDelete`, `Timberborn.EntitySystem` 456); a district center destroyed hashes 0 (`Hash` checks it alive); a
  character without a faction is B's `SimFactionOf`. Sound.
- **`ColonyRoadNetworks.OnNavMeshUpdated`** (tick, after a blast's terrain change): caught. **`DistrictMapConflictPatcher`**
  swallows the same `InvalidOperationException` on every computer (the district map's state is the same everywhere;
  beta21). Sound.
- **`ColonyRoadOverlay`'s entity events** (tick, as entities are made and deleted): `HasComponent` on an entity still
  alive during its `EntityDeletedEvent`. Sound.
- **`ColonyMarks`' planting patches** (replay, and in the tick when a blast changes terrain under a mark,
  `Timberborn.Planting` 1289-1297): dictionary operations, null-safe `Instance`. `Load` parses leniently. Sound.
- **`ColonyView`'s tick-time patches** (blink, journal): caught (`ColonyView.cs:369, 392`). The per-frame alert filter
  treats a destroyed subject as shown (`IsOwnFor` checks it alive). Sound.
- **`DesyncPatches`** (tick, detailed logging only): dereference components every such entity has (`BlockObject` of a
  reproducible, `EntityComponent` of a walker) and call read-only queries. Not re-reviewed beyond that (beta12).
- **`AnimationFixes`** (frame, co-op): `ResumeSearch` stays inside the list; the NaN guard is unchanged. Sound.

---

## 2. Coverage matrix rows

Reviewer D's rows, then the **cost column for every system of the plan's §2** (other reviewers fill the other columns of
those rows). Cost cells are the mod's own cost at late-game size (300-600 beavers, 10-20k entities), from reading and the
benchmarks above; the game's own cost is Script P's.

| Feature | Desync | Crash | Cross-colony | Mixed | Cost | Save/rehost |
|---|---|---|---|---|---|---|
| Tick loop, interrupts (P-5) | Sound: interrupts end every computer's frame at the same bucket (`TickTimingFixes.cs:26-36`); counters are read only by the report | Sound: `GiveBackBuckets` checks the ticker and the bucket size (`ReplayService.cs:1223-1234`); counters are plain adds | Sound: not per colony | Sound: faction-blind | Fixed: D-S2 counted, `D-S2` check; Left: the mechanism (determinism); Playtest: P3 "Frames cut short" | Sound: a save waits for the tick's end across interrupted frames (`ReplayService.cs:1112`) |
| Random draws and entity IDs | Fixed: D-new-1, `D-S10: in a session, a GUID asked for inside WithRealGuids ...`; Sound: co-op loads install EventIO first (D-S10b) | Sound: the new gates are static reads | Sound: map-wide by design | Sound: faction-blind | Fixed: D-S10a/b/c, the three `D-S10` gate checks | Sound: IDs made while a co-op game loads still come from the shared seed (D-S10b) |
| `TickableEntity.Tick` naming (detailed logging) | Fixed: D-new-1 (patched with real GUIDs, random state restored) | Sound: a failed patch is caught and logged (`DeterminismService.cs:257-264`) | Sound: n/a | Sound: n/a | Fixed: D-S10d, `D-S10: TickableEntity.Tick is patched only ...` | Sound: n/a |
| Walking animation (co-op) | Sound: drawing only; the tick's position is set before every bucket (`TEBPatcher`) | Sound: index kept within the list (`ResumeSearch`) | Sound: n/a | Sound: n/a | Fixed: D-S9, `D-S9` checks (77 → 56 µs a frame at 600 walkers, .NET 8) | Sound: n/a |
| Heartbeat and colony digest | Sound: beta12 (not re-reviewed) | Sound: beta12 | Sound: n/a | Left: B's F6 (faction not in the digest) | Sound: heartbeat JSON 5.6 µs a tick (D-S8); digest ring fixed at 16,384 | Sound: digest reset at load (`ReplayService.cs:726`) |
| Daily colony check | Sound: taken once, at the presence event, the same point everywhere (D-S12) | Sound: `Compare` catches a throw from the walk (`ColonyHandover.cs:447-456`) | Sound: n/a | Left: B's F6 adds per-faction counts | Fixed: D-S12, `D-S12` checks (about 1.4 ms × 2 → 1 ms × 1, .NET 8) | Sound: reads saved and tick state only |
| Road networks conflict walk | Sound: a notice only | Sound: caught (`ColonyRoadNetworks.cs:169-185`) | Sound: n/a | Sound: n/a | Fixed: D-S4, `D-S4` checks (0.11 ms a walk, road updates only) | Sound: n/a |
| Placement previews | Sound: previews only, never while replaying (`ColonyPlacementValidator.cs:26`) | Sound: caught, logged once (`:42-52`) | Sound: the host judges the click | Sound: n/a | Left: D-S9d, "Placement previews" spot; Playtest: P2 | Sound: n/a |
| Road overlay | Sound: display only | Sound: caught, switches itself off (`ColonyRoadOverlay.cs:123-129`) | Sound: n/a | Sound: n/a | Left: D-S9c, "Road overlay drawing" spot; Playtest: P2 with a path tool in hand | Sound: n/a |
| Alerts and journal filter | Sound: display only; the tick-time blink patch is display-only (`ColonyView.cs:357-375`, `ColonyRuntimeChecks`) | Sound: the tick-time patches catch (`:369, 392`) | Sound: per seat by design | Sound: n/a | Fixed: D-S9b, per-frame seat; sampled spot | Sound: n/a |
| Colony marks | Sound: sums and sorted saves (`ColonyMarks.cs:61-72, 95-116`) | Sound: null-safe lookups | Sound: A's rules | Sound: n/a | Left: D-S10i (unset postfix ungated, rare); "marked tiles" reported | Sound: a few ms per save at 10,000 marks (D-S6) |
| Detailed logging | Fixed: D-new-2 (a false desync when one computer switched it on mid-session), `D-new-2` check; a computer without it warns once (D-S7a) | Sound: bounded (D-S7a) | Sound: n/a | Sound: n/a | Fixed: D-S11, not offered at 200+ characters; measured 27 ms and 248 KB a tick | Sound: n/a |
| Co-op save | Sound: at a tick boundary with the parallel tick finished (beta12) | Sound: beta12 | Sound: n/a | Sound: n/a | Sound: the mod's part is ms (D-S6); Playtest: P6 | Sound: LateGamePerformance's background save keeps the boundary (C1-3) |
| Rehost and join | Sound: beta12 and beta21 (not re-reviewed) | Sound: beta21 | Sound: n/a | Left: B's F9 (mixed saves) | Sound: 1 MB kept, 0.76 ms a hash (D-S6); Playtest: P6 (B5) | Playtest: P6 |
| Speed pacing (catch-up, host pacing, throttle) | Sound: speed never enters the simulation (`CatchUpSpeed.cs:6-8`, `HostPacing.cs:28-29`) | Sound: pure arithmetic | Sound: both colonies count toward the game's throttle, the same everywhere | Sound: n/a | Sound: D-S5 model, no spiral; B4 holds at speed 7; Playtest: P3 | Sound: n/a |
| Long sessions (the mod's collections) | Sound: none read by the simulation | Sound: n/a | Sound: n/a | Sound: n/a | Fixed: D-S7a/b/c; reported daily (`[Perf]`); Playtest: the long session | Sound: `ColonyStamp` and marks are small (D-S6) |
| Reporting (spots, counters, report, `[Perf]`) | Sound: read only, never by the simulation | Sound: the daily line is caught (`ColonyDiagnostics.cs:139-147`) | Sound: n/a | Sound: n/a | Fixed: §5.3, two checks; sampled spots cost 3.7 ns a call | Sound: counters restart per load |
| Single player with the mod installed (B1) | Sound: nothing session-only runs | Sound: static gates | Sound: n/a | Sound: n/a | Fixed: D-S10a-d, f; Left: D-S10e (`Time.time` detour); Playtest: P1 | Sound: n/a |
| With LateGamePerformance | Left: C1-6 (every player needs the same version); Plausible: C1-7, C1-8, C1-9 (in that mod) | Plausible: C1-8 | Sound: C1-5 (searches keep the colony filter) | Sound: n/a | Left: C1-4 (stale pin, main-thread save of buildings); Playtest: P5 | Sound: C1-3 |
| With Kyler's other mods | Left: C2 (HungryPathing's file, OptimizedLocalHousing's self-disable) | Sound: C2 | Sound: HungryPathing reads per-colony hours | Sound: TipsyTail adds its need to both | Sound: C2 | Sound: OptimizedLocalHousing saves a deterministic singleton |
| Late saves hosted, then split (C3) | Sound: registry order, one digest change | Sound: C3 | Sound: every building slot 0 at the split | Left: B's F8 (paths repainted) | Sound: ms, once | Playtest: P2 (split) |

**The cost column for every system of the plan's §2** (the mod's own per-tick or per-frame cost; "UI" means it runs per
click):

| System | Cost |
|---|---|
| Automation core | Sound: the frame path is blocked in co-op (`FrameToTickFixes.cs:156-160`), less work than single player |
| Levers, relays, memory, timers, chronometers, gates, clutches, indicators, speakers | Sound: recorded setters are UI; spring return runs `RunAsSimulation` per lever; the chronometer's `Sample` adds an owner lookup per chronometer (A's `ColonyWorkingHours.cs:192-204`) |
| Sensors: depth, flow, contamination | Sound: no mod code |
| Counters and the weather station | Sound: science counter a thread-static read and, with separate science, an owner lookup per sample (`ColonyScienceService.cs:440-446`); others no mod code |
| HTTP Lever, HTTP Adapter | Sound: a recorded switch per request |
| Fireworks | Sound: settings UI; a launch's deletions end frames (D-S2) |
| Floodgates, fill and throttling valves, pumps, regulators | Sound: recorded setters (UI) |
| Water sources, badwater | Sound: a singleton lookup and a list add per source per tick, a sort per registry change (`WaterSourceFix.cs`, `WaterSourceOrderFix.cs`): µs a tick at 100 sources |
| Power | Sound: clutch and meter recorded (UI); no per-tick code |
| Dynamite, tunnels, explosions | Left: each bucket that deletes ends the frame (D-S2); a 40-charge chain costs a frame per bucket it deletes in; the road walk runs only if roads changed (D-S4) |
| Terrain under deleted buildings | Sound: no mod code |
| Wonders | Sound: the Wonder tick sorts Wonders (a handful) per tick; animator update fast path per frame |
| Bots | Left: B's F2/F3 measure per-character faction costs (`GetMaxWellbeing` per call) |
| Births, growing up | Sound: per birth; births never interrupt (D-S2) |
| Ziplines, tubeways | Sound: host-judged links (UI); riders' animation paths have more corners, cheaper with D-S9 |
| Beehives | Sound: no mod code |
| Trading Posts | Left: C's T4; "Trading post exchanges" and "Trading post workers" spots report it |
| Dev tools | Sound: no per-tick code |
| Colony separation rules (working hours, searches, jobs) | Fixed: D-S1 (sampled working-hours spot); Left: `CanPlantAt` recomputes the forester's owner per spot and `MayTake` looks `ColonyMarks` up per candidate (A's `ColonySeparationPatches.cs:61-71, 118-128`), a few lookups each, timed only as part of "Resource searches" |

---

## 3. Found sound

What was checked and found fine, so nobody redoes it.
- **SoundEmitter.Update** runs only while a callback sound plays (D-S10c).
- **Creations never interrupt ticking in a game** (D-S2): preset IDs come only from loading; undo is off in the Game scene.
- **The road-networks listener never hears previews** (D-S4).
- **The walking animation's path is one tick's**, rebuilt by `SetNewPath` each tick (D-S9).
- **`ParameterProvider` postfix** is per injection, not per tick (D-S10g).
- **Allocations** in reviewer D's files (D-S3).
- **Saves and rehosts at late-game size**: the mod's part is milliseconds (D-S6).
- **LateGamePerformance's catch-up, idle entities, background save and the colony layer's searches** (C1-1 to C1-3, C1-5).
- **Bounded collections** (S7 sweep), the notable ones:
  - `ColonyDigest.recent`: a ring of 16,384, reset at load (`ColonyDigest.cs:57`, `ReplayService.cs:726`);
  - `CrossingExchange.ledger`: 20 per half (`TradingPostExchange.cs:73, 193`);
  - `ChatLog` and `ChatView`: 2,000 lines (`TimberNet/ChatMessage.cs:160-177`, `Panel/ChatView.cs:151`);
  - `WaterDiagnostics`: 4 snapshots and 64 MB; `WalkerTrace`: 192 ticks (about 20 MB at 600 walkers), both detailed
    logging only;
  - `TimberNetBase.receivedEvents`: a guest's backlog, held under 60 ticks by the host's hold;
  - `SendLane`: a guest dropped above 16 MB (`TimberServer.cs:42`); `SteamLinkSocket.outgoing` 128 MB per link;
  - `ColonyStamps.unstamped`: let go on delete, destroy and stamp (`ColonyStamps.cs:155-184`);
  - `TEBPatcher`'s per-bucket caches: weakly keyed, trimmed per tick (`DeterminismService.cs:1073-1076, 1185`);
  - `PendingActions`: an 8 s timeout; `LatencyStats` 200; event subscriptions balanced (`PlayerActivityService.cs:138/145`,
    `PingService.cs:65/70`); `ColonyFactionService.Changed` reset per game.
- **Grows per session, small, left:** `ColonyJournal.owners` (about the living citizens that ever changed district, plus
  125; `ColonyJournal.cs:46, 104-153`); `ColonyMarks`' tiles (bounded by the map); per-connection maps in `TimberServer`
  and `ColonySlotService` (a few entries per join).
- **Held until the next game or session, not growth:** the save bytes (about 1 MB, D-S6); static references to the last
  game's `ColonyDiagnostics`, `TradeOverviewPanel`, `FactionToolDisabler` map and `LargeColonySpeedLimit`'s session IO,
  each replaced by the next.

## 4. Left, and why

- **D-S2, the interrupt mechanism:** determinism needs Unity's destroy at the same point on every computer (beta12 D2).
  Counted and reported instead; the model gives its ceiling.
- **D-S10e, the `Time.time` detour in single player:** moving a native detour to session start would make a failure
  break hosting instead of stopping the mod at start. P1 measures it.
- **D-S10h, D-S10i:** microseconds; per rare event.
- **D-S9c road overlay, D-S9d previews:** spotted, only while a building tool is in hand; P2 measures them.
- **D-S6, the kept save bytes:** about 1 MB after the first tick.
- **D-S8, the build config:** decision D4; P1b answers it in the game.
- **S1 per-candidate lookups in A's `ColonySeparationPatches.cs`:** hoist `ColonyMarks.Instance` out of `MayTake`'s
  per-candidate call and compute the forester's owner once per search. A's file; small; P2's "Resource searches" row
  says whether it is worth it.
- **The lockstep's boost behaviour** (D-S5): a guest seconds behind at a boost the computers can't carry is how
  `HostPacing`'s scaled thresholds work (beta12's decision); lowering them trades lag for more stop-go.
- **LateGamePerformance findings C1-4, C1-6 to C1-9 and C2's** belong to those mods (plan: findings to Kyler as a list).
- **Not done here, for the other reviewers:** per-faction character counts (B, F6) and goods conservation (C, T3) in the
  daily line; they can append to `ColonyDiagnostics.Fingerprint` or `DailyPerformanceLine`.

## 5. Doc text and Script P

### 5.1 Proposed doc text

TWO-COLONIES *Known limits* (and README's co-op notes):
- **Deletions cost frames in co-op.** Whenever something is deleted during a tick (a harvest picked up, a death, a
  blast, a demolition), every computer finishes that tick in the next frame, so that the game removes it at the same
  moment everywhere. At normal speeds this costs nothing; with many deletions a tick at a high speed or a low frame
  rate, the game runs slower than chosen (at most *frame rate ÷ (1 + deletions a tick)* ticks a second). The
  diagnostics report (Ctrl+Shift+J) counts them.
- **Detailed logging is for small colonies.** *Always Use Detailed Logging* makes every computer record every beaver's
  decisions and the host send them all every tick: a colony of 200 or more beavers and bots can't keep up, so the desync
  dialog no longer offers it there.
- **LateGamePerformance and co-op:** every player needs the same version of it (it says so itself), with nothing
  turned off after an error. Timber Together only warns when the players' mod lists differ.
- **A boost the computers can't carry** leaves a guest a few seconds behind the host (the host eases off until the
  guest keeps up), so the guest's own actions take that long to show.
- **The daily colony check** (`[Colony] Check` in the log) is taken as the host's day reaches each computer, one tick
  after the turn of the day.

Changelog (for the main session): the S10 gates (Timber Together costs less in single player), D-S9, D-S12, D-S4, D-S7a-c,
D-S11, the report's new lines and the `[Perf]` line, D-new-1, and D-new-2 (switching detailed logging on during a
session no longer stops it with a false desync).

### 5.2 Script P: performance recordings (Kyler, with PerformanceLog)

Common to every run:
- PerformanceLog installed and enabled, its defaults (deep profile); nothing else changed between the two recordings of
  a pair. Close other programs; the game window in front the whole time, the same window size.
- The save: `Saves/Romans missing leg/Romans missing leg (9) TESTING.timber` (`R-late`). Load it, choose the game's
  fastest speed (the third button, speed 7), wait **1 minute** (warm-up), then play untouched for **3 minutes**, then
  leave to the main menu (so the session folder is finished).
- Send: the session folders from `Documents\Timberborn\PerformanceLog\` (one per load), each player's `Player.log`
  (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`), and in co-op each player's Ctrl+Shift+J report
  (pressed just before leaving; saved in `TimberTogether-Reports` next to `Player.log`).

- **P1, single player, with and without Timber Together (B1, D-S10).** Record R-late in single player with Timber Together
  disabled in the mod manager (restart), then enabled (restart). Should see: tick time within 1% of each other;
  PerformanceLog's "which mod patches which hot method" no longer lists Timber Together on `TickableEntity.Tick`.
  `python tools/perflog.py compare <without> <with>`.
  - **P1b (D4, optional):** the same with Timber Together's Release build instead of Release Steam (the main session builds
    it into a folder for Kyler; Kyler copies it in himself). Should see whether the optimised DLL's tick share is lower.
- **P2, hosting with nobody joined (B2, B3, C3, S9).** Host R-late from the main menu (waiting room, Start at once),
  record. Then, in the same game, found a second colony with *Allow founding colonies in a shared game* on (split),
  let a day pass, record again. In each, also hold a path tool for 30 s over roads. Should see: Timber Together's share of
  tick time at most 5% shared, 7% split; no Timber Together method among the top allocators; the report's "Colony code"
  rows (working hours, resource searches, placement previews, road overlay, daily check) small against "Ticking".
- **P3, with a guest (B4, S2, S5).** Host R-late split into two colonies, a guest joined (same PerformanceLog settings),
  3 minutes each at the game's speed 2 (x3), speed 3 (x7), then speed 3 with a boost of 15 (chat box `/boost 15`).
  Both record. Should see: the guest within 4 ticks of the host for 95% of the time at speeds 2 and 3 (the report's
  Co-op lines and the host's `Host pacing` log lines); at boost 15 the host easing (logged) and the guest's lag steady,
  not growing; "Frames cut short ... a tick" small at speed 3.
- **P4, mixed factions (B6).** Script M's grown mixed game against a single-faction game of the same size, P2's steps.
  Should see: tick cost within 5%; load time and memory (the report's `Memory:` line) within 25%.
- **P5, with LateGamePerformance (C1).** P2 again with LateGamePerformance 0.4.28 enabled on both. Should see: the same
  daily `[Colony] Check` lines on both computers (no desync); LateGamePerformance's own log line about `ColonyStamp`'s
  hash (C1-4) once.
- **P6, a rehost (B5, S6).** In P3's session, Options → Save and Rehost; the guest rejoins. Time it from the click to
  the guest playing. Should see: under 30 s on a home network; no host freeze over 1 s (PerformanceLog's slow frames
  during the save and the send).

### 5.3 Lines for the long session (release bar 7)

- With a guest, both computers: at the end, copy every `[Perf] Day` line from each `Player.log`. Should see: "heap"
  levelling off after the first days (no steady climb), "traces 0 ticks" (logging off), "buckets lost" near 0 at
  normal speed, and no `[Colony] Colony state differs` line.
