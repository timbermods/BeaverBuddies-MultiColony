# Reviewer A: the late-game systems against the game (1.4.0-rc1 review)

Branch `worktree-agent-a62bba8527c45d80c`, from `1323b8c` (beta24 plus P-1). Game: Timberborn 1.1.2.4, decompiled. Nothing
here was run in the game (the review's rule); "Confirmed" means traced end to end in the mod and the decompiled game, and
where possible reproduced by a check that fails on beta24's DLL.

**Checks:**
- **Counts:** StabilityTests 423/423 (418 + 5), RuntimeChecks 373/373 on Release Steam and on Release (362 + 11). The mod
  builds with 0 warnings in both configurations.
- **Failing without the fixes:** all 11 new RuntimeChecks checks, and the rewritten `TimingChecks` one, fail on
  `beta24-ReleaseSteam.dll` (360/373 there, the 13th being P-1).
- **StabilityTests:** four of the five scans fail on beta24's source (duplicate list entries, the transpiler's throw, the
  missing Detonator and colony-rule files). The H1 scan is a guard that passes on both.
- **Rewritten check:** one existing check changed meaning with R8 (`RuntimeChecks/TimingChecks.cs`, "Timing transpiler
  leaves a changed method body as it is…"). The old one asserted the throw R8 removes.

**Files outside my list, and why:**
- `Events/ToolEvents.cs`: X4-1, two lines in the deletion recorder.
- `Plugin.cs`: one line, binding `CoopFixGuard`.
- The localisation CSV: one line at the end, `BeaverBuddies.CoopFix.Stopped`.
- `RuntimeChecks/TimingChecks.cs`: the R8 test above.

**Wire and save impact of everything below:**
- No new ReplayEvent and no field change.
- `AutomationEvent` carries four new method keys (`WaterMover.SetFlowRate`, `ThrottlingValve.SetOutflowLimitEnabledAndSynchronize`,
  `AdjustableStrengthPowerGenerator.set_GeneratorStrength`, `.FlipRotation`). A beta24 build would not know them, but the build
  handshake already keeps builds apart.
- Nothing new is saved.

---

## 1. Findings

### A2 (sweep 1): UI → simulation writes that are not recorded

**Method.** Over the game's IL: every method of every `*UI*` assembly and of the UI plumbing (`BatchControl`,
`BlockObjectTools`, `BuildingTools`, `CursorToolSystem`, `SelectionToolSystem`, `TimeSpeedButtonSystem`,
`ToolButtonSystem`, `DropdownSystem`, `SliderToggleSystem`, `EntityPanelSystem`), plus every `IInputProcessor`,
`ITool`, `IEntityPanelFragment` and `IDevModule` anywhere, their lambdas included: 9,250 methods (map editor assemblies
left out). Every call (and field store) into a simulation assembly that is not a query by name (get/Is/Has/Can/Find…)
was listed and classified against the mod's 142 recorded game methods (every recording prefix's target, found from its
IL, plus `AutomationEvent`'s list). First as a Cecil sweep (`$SP/sweepA`, 1,323 edges, EventBus posts listed separately),
then kept as the RuntimeChecks check **"A2: every call the game's panels, tools and keys make into its simulation is
shared, dev mode's, the map editor's or display only"**: 223 unrecorded calls, each on an allow-list with its reason, so
a game update that adds a UI path fails the build until someone looks at it. EventBus posts from UI code: all UI-only
events, `PlantingAreaMarkedEvent` (inside the recorded `MarkArea`) and the new-game start's settlement events (before
any co-op game starts).

**Classification of the 223** (the check's list, `RuntimeChecks/RcLateGameRuntimeChecks.cs`, `UiWrites.Allowed`):
- **Covered by a recorded method (22).** `GateToggle` → `Gate.Open/Close/Automate` → recorded `SetOpeningMode`. Lever
  `Press/Release` → recorded `SwitchState`. `PriorityToggle` → `IPrioritizable.SetPriority`: both implementations
  recorded. The planting lambdas inside the recorded `MarkArea`/`UnmarkArea`, and the demolish tool's helper inside its
  recorded `ActionCallback`. `CursorTool` → `GameOptionsBox.Show` (recorded). The speed lock goes through the mod's
  `SpeedLockPatcher`. Saving and loading, and terrain queries.
- **Dev mode (71):** dev panel modules, dev tools (`IDevModeTool`; every non-building placeable is `DevModeTool` in
  Blueprints), debug (diagnostic) fragments (dev only, `DiagnosticFragmentController`), the battery's charge slider,
  the unstable core's radius, the need edit buttons.
- **Map editor (21), preview and picking (18).**
- **Display (68):** markers, animators, lights, tints, cables, the tutorial panel.
  - Some are **saved**, but nothing in the simulation reads them: a stream gauge's highest-water marker, a bell's sound
    toggle, a building's decal, a light's custom colour (read only by lights and indicators). These stay on the player's
    computer until a rehost, which keeps the host's (see *Left*).
- **The panel's own choice (6):** which district the population, resources and wellbeing panels show. The automation
  samples its own per-district figures (`SamplingPopulationService`, `SamplingResourcesService`). `PopulationService.
  SwitchDistrict` recomputes the global figures in a frame, but `PopulationService.Tick` recomputes them in the same
  singleton pass before `AutomationRunner` (an `ILateTickable`) samples them, so nothing differs.
- **This computer's own (11):** the HTTP server, the HTTP Adapter's webhook settings (A3), the camera, custom sound and
  decal files. **Start (6):** a new game's starting building, placed before any co-op game starts.

Four were not harmless:

**A2-1. A pump's flow rate is not shared.** Confirmed. Kind: desync. Hits: everyone.
- *Mod:* `Events/AutomationEvents.cs:207` (now listed).
- *Game:* `WaterMoverFragment.SetFlowRate` → `WaterMover.SetFlowRate`
  (`Timberborn.WaterBuildingsUI`, `WaterMoverFragment`; `Timberborn.WaterBuildings`, `WaterMover`). `FlowRate` is saved
  and `Tick` moves `TickIntervalInSeconds * EffectiveFlowRate` of water. It is Timberborn 1.1's slider on every pump:
  the mechanical pump, the deep and badwater pumps.
- *Effect:* dragging it changed the water moved on the dragging player's computer alone. A water desync that nothing
  catches at once.
- *Fix:* add `WaterMover.SetFlowRate` to `AutomationEvent`'s list.
- *Checks:* RuntimeChecks "A2: the pumps' flow rate, the throttling valve's limit toggle and the dev generator's panel
  are shared" and the sweep check; StabilityTests "A2: AutomationEvent's list names each shared setter once…".
- *Script L:* "L-W1" below.

**A2-2. The throttling valve's outflow slider shares the limit but not its on/off.** Confirmed. Kind: desync. Hits: everyone.
- *Game:* `ThrottlingValveFragment.SetOutflowLimit` calls `SetOutflowLimitEnabledAndSynchronize(v)` and then
  `SetOutflowLimitAndSynchronize(v)`. Only the second was listed (`AutomationEvents.cs:189-193` at beta24).
  `GetTargetOutflowLimit` returns no limit while `OutflowLimitEnabled` is off.
- *Effect:* dragging from "unlimited" limited the flow on one computer, and dragging back to the top left it limited on
  the others.
- *Fix:* list it (`AutomationEvents.cs:194`). The fill valve's pair was already complete.
- *Checks:* as A2-1.

**A2-3. The dev power generator's panel works without dev mode and is not shared.** Confirmed. Kind: desync. Hits:
everyone whose save has one.
- *Game:* `DevPowerGenerator` is placeable with dev mode only (`PlaceableBlockObjectSpec.DevModeTool`), but
  `AdjustableStrengthPowerGeneratorFragment` has no dev-mode check. Its slider sets `GeneratorStrength` (the node's
  output multiplier) and its button calls `FlipRotation`.
- *Effect:* Scripts L and M build their scenarios with dev mode before hosting, so the generator is likely to be there.
  Dev mode's co-op warning never shows (dev mode is off), and the power network differs.
- *Fix:* `set_GeneratorStrength` and `FlipRotation` are listed (`AutomationEvents.cs:210-211`). `InitializeEntity`'s own
  call runs in a placement replay or at load, where recording lets it through.
- *Checks:* as A2-1.

**A2-4. Dev mode's Ctrl while planting spawns the plants on one computer.** Confirmed. Kind: desync. Hits: dev mode only.
- *Game:* `PlantingTool.Plant` calls the recorded `MarkArea` and then `DevModePlantableSpawner.SpawnPlantables`. That
  reads the dev keys `PlantSpawned`/`PlantGrown`/`PlantWithYield` (Ctrl/Shift/Alt, `DevModeOnly`) and spawns grown
  plants, locally.
- *Effect:* Ctrl is also the shared free-unlock modifier.
- *Fix:* like the other Ctrl dev keys (`DevKeysCoopFix`), the spawner does nothing in co-op
  (`Fixes/DevKeysCoopFix.cs:69`, `DevPlantSpawnKeyCoopPatcher`).
- *Check:* RuntimeChecks "A2: dev mode's plant spawning while planting (Ctrl held) is off in co-op…".

Also on the way: `FillValve.SetAutomationTargetHeight…` was listed twice (harmless, as beta12 said). The duplicates are
removed, and the StabilityTests check keeps the list free of them.

### A1: automation only on the tick

**A1-1. A spring-return lever (or an HTTP spring lever) wired to a Detonator never sets the dynamite off in co-op.**
Confirmed. Kind: gameplay (the same on every computer). Hits: everyone.
- *Game:* `Detonator.Evaluate` arms on an input that comes on (arming triggers the `Dynamite`). It disarms, and so
  un-triggers, if the input goes off at the same `Time.time` it armed (`Timberborn.AutomationBuildings`, `Detonator`).
  `AutomationRunner.Tick` evaluates scheduled partitions, then `CommitTickSingletons` (where `SpringReturnService`
  switches spring levers off), then evaluates again.
- *In co-op:*
  - `Time.time` is the tick's (`TimeTimePatcher`).
  - The click is played at the tick's start (`ReplayService.DoTick` before bucket 0).
  - The frame path is off (`FrameToTickFixes.cs:156`).
  - So the lever's pulse arms and disarms in one tick: nothing explodes. In single player the click is evaluated in its
    own frame, and it explodes.
- *Fix:* in co-op the Detonator only arms (`Fixes/TickTimingFixes.cs:95`, `DetonatorPulseCoopPatcher`,
  `[ManualMethodOverwrite]`, Priority.Last). Within one evaluation there is no flicker to filter.
- *Checks:* RuntimeChecks "A1: in co-op a Detonator goes off on a pulse that begins and ends in one tick…" (the game's
  premises plus the patch's IL); StabilityTests "A1: the co-op Detonator arms on its input and never takes the arming back".
- *Script L:* L-A3.

**A1, single player against co-op** (from `AutomationRunner`, `AutomationPlan`, `AutomatorPartition`, the transmitters):

| Building / case | Single player | Co-op (frame path off) |
|---|---|---|
| Any setter or wire change from a panel | Evaluated in the frame it is made (`UpdateSingleton`, with `EvaluateNext`) | Played at the next tick's start, evaluated at that tick's first pass (`EvaluateScheduled`, before `EvaluateNextPartitions`): same result, up to one tick later |
| Flipped while paused | Evaluated at once: lights, floodgates, pauses apply while paused | The action is played while paused (`ReplayService.UpdateSingleton`), evaluated at the first tick after unpausing |
| Spring-return lever, click | ON from the click to the next tick's commit | ON for one tick: seen by the start pass, `EvaluateNext` (Memory, Timer) and the terminals; off at the commit; seen off at the end pass |
| Spring-return lever, held | Stays on while the button is held (`_isPressed`) | One tick only (the mod's `LeverSpringReturnCoopPatcher`; holding never worked in co-op). **Doc** |
| Two opposite actions in one tick (on then off) | Each evaluated in its own frame: a pulse | Both played at one boundary: the first pass sees only the last state, so the pulse is lost. Rare by hand (a tick is 0.6 s of game time); possible from the HTTP API |
| Detonator on a one-tick pulse | Explodes | Explodes (A1-1 fix); before rc1, disarmed |
| Timer (ticks, random), Memory, Relay, Chronometer, sensors, counters | Committed and sampled in the tick | Same: tick-only. `Timer.NextRandomStateCached` is drawn in the tick only (frame path off) |
| Scheduled partition never rescheduled | Evaluated by the next frame | Evaluated by the next tick's first pass (the list is cleared only after evaluating) |
| Partition order | Lists and a Queue, filled in registration order; no hash-ordered collection in `Timberborn.Automation` | Same on every computer |

**Not reproduced outside the game:** `BaseComponent`'s `bool` reads `GameObject` (native), so the automation classes
can't be driven in RuntimeChecks without stubbing Unity throughout. The checks pin the game's premises and the mod's
patches from the IL instead; Script L covers the behaviour.

### A3: the HTTP API

- **Sound: switching.**
  - `HttpLeverJsonEndpoint` (listener thread) queues a command.
  - `HttpApiIntermediary.UpdateSingleton` (frame) → `HttpLever.SetState` → `Lever.SwitchState`, which is recorded: that
    player's action, sent to the host and judged by colony (`AutomationEvent`'s scope names the lever). A guest's request
    for the other colony's lever is refused with a notice.
  - Each computer's listener is its own (`HttpApi.Start` only from the panel's button), so a request is delivered once,
    to the computer it was sent to.
  - A lever gone before the play is skipped (`AutomationEvent.Replay`, null component). A name that isn't unique is
    ignored by the game (`UniquelyNamedEntityService.TryGet`).
  - Names are shared (renames are recorded), so two players name the same lever.
- **A3-1. Colour requests are dropped in co-op.** Confirmed. Kind: display, but saved state. Hits: everyone.
  - `HttpLever.SetColor` sets `CustomizableIlluminator.SetCustomColor`/`SetIsCustomized`, which are saved. Only lights
    and indicators read them.
  - Per the review's default ("drop SetColor in co-op if it can change saved state"), `HttpLeverSetColorCoopPatcher`
    (`AutomationEvents.cs:669`) skips it in co-op and logs once.
  - *Check:* RuntimeChecks "A3: an HTTP API request switches an HTTP lever as the player's shared action, and a colour
    request is dropped in co-op".
- **Webhooks (Left, documented):**
  - The HTTP Adapter's `Evaluate` runs in the tick on every computer, and each calls the adapter's webhooks with its own
    settings.
  - The settings are saved, but a change to them is this computer's own (unrecorded: the sweep lists them as "this
    computer's own"). So the webhook is called by the computer of the player who set it up, until a rehost, after which
    everyone has the host's settings.
  - Nothing in the simulation depends on a call's result (`_lastOnCallSuccessful` is display).

### A4: counters and sensors per colony

**A4-1. A Population Counter set to "global" counted every colony.** Confirmed. Kind: cross-colony. Hits: separate colonies.
- *Game:* `PopulationCounter.Sample` reads `SamplingPopulationService.GlobalPopulationData`, a copy of
  `PopulationService.GlobalPopulationData` (every district).
- *Fix (the review's default):* `Colonies/ColonyPopulationCounter.cs`, `ColonyPopulationCounterPatcher`
  (`[ManualMethodOverwrite]`, Priority.Last), in separate colonies and global mode:
  - it adds up the sampled figures of the districts its own colony owns (`SimOwnerOf(counter)`, then `OwnerOfDistrict`
    per finished district);
  - integer sums, into one `PopulationData` per counter (a `ConditionalWeakTable`, nothing allocated per tick);
  - a `ColonyProfiler` spot, "Population counters (colony)".
- A counter no colony owns keeps the game's count; a shared game is unchanged.
- *Checks:* RuntimeChecks "A4: a Population Counter set to the whole map counts its own colony…"; StabilityTests
  "A4, W1: … separate-colonies games only".

**Sound:**
- **Resource Counter:** its own district only (`SamplingResourcesService.GetSampledResourceCount(district)`). A
  crossing half's stock counts in its own district.
- **Science Counter:** its own colony (`ColonyScienceCounterPatcher`).
- **Chronometer:** its colony's hours (`ColonyChronometerPatcher`).
- **Weather Station:** map-wide, as intended.
- **Power Meter:** its whole network (P1).
- **PostLoad sampling:** `AutomationRunner.PostLoad` samples after every `Load`. District centers carry their saved
  owner (`DistrictOwner`) and buildings their stamp, and every computer loads the same bytes, so the first sample is the
  same everywhere.

### W1–W4: water

**W1-1. Synchronised fill valves, throttling valves and floodgates changed, and rewired, the other colony's.**
Confirmed. Kind: cross-colony. Hits: separate colonies.
- *Game:* the synchronisers (`FillValveSynchronizer`, `ThrottlingValveSynchronizer`, `FloodgateSynchronizer`, all in
  `Timberborn.WaterBuildings`) walk every touching synchronised building of the kind, transitively (BFS, a Queue;
  the HashSet is only `Contains`). They copy the heights, limits, reaction speed **and `Automatable.SetInput(source.Input)`**.
- *Entry points:* the `…AndSynchronize` setters, `ToggleSynchronization`, `OnEnterUnfinishedState` (a placement pulls,
  a duplicate pushes), and `Automatable.InputReconnected`, so a wire set on one valve rewired the neighbours.
  Synchronising is on by default.
- *Fix (the review's default, "within the actor's colony"):* `Colonies/ColonyWaterSync.cs`.
  - Every entry sets the colony of the building it starts from (`SimOwnerOf`), with re-entry kept.
  - The valves' single lookups (`GetValve`, `GetThrottlingValve`) return nothing of another colony.
  - The floodgate's two lookups are filtered: `SynchronizeNeighbor` is skipped, and `SynchronizeWithNeighbors` is
    replaced (`[ManualMethodOverwrite]`).
  - Another colony's building is neither changed, copied from nor passed through. The owner is shared state, so every
    computer agrees. Only separate colonies.
- *Checks:* RuntimeChecks "W1: synchronised fill valves, throttling valves and floodgates keep in step with their own
  colony's only"; StabilityTests "A4, W1…".
- *Script L:* L-W2.

**Sound:**
- **W2 (sensors):** Depth, flow and contamination sensors read `IThreadSafeWaterMap` in `Sample`, called only from
  `AutomationRunner.PostLoad`/`Tick`.
  - `ThreadSafeWaterMap.Tick` copies the finished columns in the singleton pass after `FinishParallelTick`, and the
    runner is `ILateTickable`, so it samples that copy.
  - A replayed floodgate or valve change (played after a forced `FinishParallelTick`, before bucket 0) is queued like a
    tick-time write, applied that tick and seen one tick later, on every computer.
- **W3 (ticking water buildings):**
  - `FillValve`, `StreamGauge`, `TickableWaterBuilding`, `WaterNeeder`, `ContaminationBlockableBuilding` and
    `WaterPoweredGenerator` read the thread-safe map.
  - `WaterMover` reads it and uses `TickIntervalInSeconds`. `ThrottlingValve.Tick` reads no water.
  - Writes are queued (`FlowLimiterService`, `WaterSimulator._modifications`, `WaterChangeService`).
  - The only frame-time reads are visual (the floodgate and regulator animations) and the seep modifier (W4).
- **W4 (the water-source fixes still match 1.1.2.4):**
  - `TickOnlyArrayFix`'s premise holds; it matters for saves only.
  - `WaterSourceOrderFix`: `UpdateThreadSafeRegistry` unchanged; contamination is mixed in list order.
  - `WaterSourceTimingFix`: exactly one `Time.deltaTime` in `GetStrengthModifier` (and see R8).
  - `WaterSourceFix`'s race premise is stale: `ThreadSafeWaterSource` snapshots `CurrentStrength` when built, so the
    deferral is equivalent and harmless. Kept.
  - Regulators switched by automation are terminals (in the tick); their panel is recorded.
  - `TimedComponentActivator` (map timers on sources and cores) is a `TickableComponent` counting days and ticks.
  - Badwater rigs, centrifuges and discharges write through `WaterOutput`/`WaterInput` on production events, in the tick.
- **H1, part of W4:** `LateTickableBuffer.TickComponents` now resets its flag and list in a `finally`
  (`Fixes/WaterSourceFix.cs:41`). A throw there still ends the session, but no longer leaves every later source ticking
  inline.

### R8: surviving a game update

**R8-1. `WaterSourceTimingFix` threw inside `PatchAll`, and a renamed automation setter threw out of `StartMod`.**
Confirmed by reading. Kind: session-stopper after a game update. Hits: everyone.
- *Before:* the transpiler threw unless it found exactly one `Time.deltaTime` (`Fixes/WaterSourceTimingFix.cs:34-35` at
  beta24). A throw inside the one `PatchAll` stops every later patch. Separately, `ApplyAutomationPatches` called
  `harmony.Patch(null)` on a `GetMethod` that found nothing (a `NullReferenceException` out of `StartMod`), which skips
  `GameSaverSavePatcher.Install` and `TimeTimePatcher.Install`, i.e. co-op without the deterministic `Time.time`.
- **Why refusing co-op, and only when needed, is the safest.** Without the seep fix, water seeps (the only sources with
  `WaterDepthStrengthModifier`: `WaterSeep`, `BadwaterSeep`) fade in on each computer's frame time: a certain water
  desync, which nothing catches quickly. A warning would let that session start and fail later, which is the plan's
  "worst outcome". Refusing every co-op game would block maps without seeps, which are unaffected.
- *Fix, the seep method:* the transpiler never throws. If it doesn't find exactly one read, it leaves the method as the
  game has it, sets `WaterSourceTimingFix.Unavailable` (why) and logs an error.
- *Fix, the automation list:* each lookup and patch is in its own try. A missing setter is logged and added to
  `AutomationEvent.MissingRecorders` (`AutomationEvents.cs:217-233`).
- *The guard:* `Fixes/CoopFixGuard.cs`, bound in every co-op game (`Plugin.cs:73`). On the loaded game's first frame
  it:
  - ends the session on each computer, with a clear message (`BeaverBuddies.CoopFix.Stopped`, through
    `ReplayService.EndSession`);
  - only when the seep fix is unavailable and the world has a seep, or when any shared setter is missing;
  - single player is never affected.
- *No throw in the tick:* `GetDeltaTime` no longer throws if the buffer is missing (it logs and uses the game's clock:
  a throw there would be inside a tick).
- *Checks:* RuntimeChecks "R8: a game update that changes the water seep method or renames a shared setter no longer
  stops the mod's start; a co-op game that needs it is stopped at load"; `TimingChecks` rewritten (the transpiler on an
  empty body: no throw, `Unavailable` set, the real body resets it); StabilityTests "R8: neither the water seep fix nor
  the shared setters' patching can throw out of the mod's start".
- **For Main (R8's sweep):** when any non-faction patch throws, `PatchAllIsolatingFactions` still lets the exception out
  of `StartMod`, and the configurators still bind (`Plugin.Disabled` stays false): co-op on a half-patched mod. A small
  follow-up: catch there, record the failure in `CoopFixGuard` (a third reason) and refuse co-op. I left `Plugin.cs`'s
  patching alone: it's Main's lead and a policy call.

### P1, P2: power

**P1 (Left, documented; the review's default).**
- Shafts join by position only (`TransputMap.GetFacingTransput`), so two colonies' shafts make one network: shared
  engines and batteries.
- One colony's recorded clutch (`Clutch.SetMode` → `SetDetached` removes the node and splits the graph) cuts the other's
  power.
- A Power Meter reads the whole graph (supply, demand, battery).
- All deterministic. Nothing breaks.

**P2 (Sound).**
- `MechanicalGraphManager` merges through a HashSet and splits by BFS. The HashSets only enumerate in slot order, which
  follows the add/remove history (ticks and replays), never hash codes, and no key type overrides `GetHashCode`.
- Totals are ints updated by deltas. `PowerEfficiency` is a ratio of ints. Batteries are Lists with equal shares. The
  only float sum is over a List (`WalkerPoweredGenerator`).
- Gravity battery charge is saved (`LayeredBlockObstacle.OccupancyRange`) and changes only in `BatteryService.Tick` on
  `FixedDeltaTimeInHours`.
- The visual `MechanicalNodeTransformHeight` also writes a height into saves, which nothing reads.

### X1–X4: dynamite, blasts, tunnels, terrain

**X1 (Sound).**
- *Order:* `Dynamite.Trigger` sets `_ticksToDetonate = 1` (goes off on the second tick); `TriggerDelayed(n)` is in
  ticks. `Detonate` triggers neighbours (a fixed `Neighbors4` array, live block lookups), deletes the path, lowers the
  terrain, kills, deletes itself.
- *Unstable cores:* go through `ExplosionService.Tick`: a List walked backwards, one shell per tick. The HashSets on
  the path only get Add/Clear, so they read out in insertion order. No RNG but the body timer.
- *Characters:* killed through `ExplosionVulnerable.DieFromExplosion` → `Mortal.DieInstantly` → `CharacterKilledEvent`,
  never deleted.
- *What follows in the tick:* terrain events, nav mesh (`NavigationSynchronizer.Tick`), water (on
  `ForcedParallelTickFinished`), terrain physics (next tick) and district updates.
- *Frames:* only visuals.
- *P-5 (Script L measures):* each bucket that deletes ends the frame (`TickTimingFixes.cs:26-36`), so a 41-charge chain
  runs in steps of about 2 ticks.
- *Game quirks, the same on every computer:*
  - `CharacterExploder` skips a second beaver on the same tile (the kill mutates the list).
  - `_ticksToDetonate` isn't saved (a delayed charge goes off on the first tick after a load; everyone loads, a rehost
    included).
  - A finished blast's `ExplosionData` is never removed from `_explosions`, so it grows and is saved. Worth reporting to
    Mechanistry.

**X2 (Left, documented; the review's default).** No filter on what a blast reaches:
- *dynamite:* its tile's path, the terrain column and the characters on the tile;
- *unstable cores:* everything in each shell but recovered-good stacks, other cores (set off), whatever loses support
  (terrain physics), every character.

**X3 (Sound).**
- `Tunnel` removes its voxel from `OnEnterFinishedState` (reached from `ConstructionSite.Tick` or the recorded Finish
  now).
- The dirt excavator (`Drill`) removes terrain on `ProductionFinished` in the tick (random block, game RNG, in the
  tick).
- `GroundRaiser` adds terrain on the site's deletion, in the tick.
- Relic rewards from blasts go to the relic's colony (`ColonyScienceRelicRewardPatcher`).

**X4 (G9) (Left, documented) + X4-1 (Confirmed, fixed).**
- *G9:* the demolish tool's picker adds terrain held up by what is deleted: dirt from terrain blocks on a platform,
  within `MaxSupportDistance` 3 (`TerrainPhysics`). It isn't dev-only, and the recorded event carries only entity ids,
  so in co-op that terrain floats, on every computer alike.
  - Proposed fix, **not small**: at replay, recompute `ITerrainPhysicsService.GetTerrainAndBlockObjectStack` over the
    host-trimmed list and `DestroyTerrain` the result. That needs a colony decision when the stack holds another
    colony's building. Left for 1.4.0 and documented.
- **X4-1 (display and local, everyone):** when the recorder skips the game's method it cleared the objects but not
  `_temporaryTerrainCoords`. The list grew with every deletion, raised the layer view to old heights
  (`SetVisibleLayerToShowAllObjects`), and was all destroyed at once the first time the game's own method ran on that
  computer (after a session ended). Now cleared (`Events/ToolEvents.cs:256`).
  - *Check:* RuntimeChecks "X4: a deletion the mod records clears the terrain the tool picked with it…".
- *Dev only:* the dynamite panel's delay keys (Ctrl 10 ticks, Shift 20; `DevModeOnly`) aren't recorded, so the co-op
  replay triggers at once (`Trigger()`). Documented.

### O1–O4

**O1 fireworks (Sound).**
- Launcher settings are recorded. Arming is an automation terminal.
- The launch is `FireworkLaunchService.Sample`, a sampling singleton in the tick: goods taken with
  `Inventory.TakeConsumed`, the rocket spawned with `EntityService.Instantiate`.
- `Firework.Update` (a frame) moves the rocket and deletes it when its particles end. Nothing in a tick reads a
  firework: it isn't tickable, and has no stamp, district or need.
  - I checked every whole-entity walk: the daily fingerprint, founding and handover counts, `ColonyStamps`.
- The rocket moves once per tick in co-op (tick-quantised clock): cosmetic.

**O2 (Sound):**
- **Zipline links:** recorded, both towers the actor's, the game's check on the host.
- **Tubeways:** have `PathSpec`, so the road rule stops them joining another colony's network.
- **Beehive:** the `Hive` runs on a tick-driven trigger. Its pollination helps any crop in range, the other colony's
  and faction's included: document.
- **Gates:** at the tick (`GateTickRunner`). Each opening walks the real road graph twice; D's cost lead.

**O3 (Sound, shared):** weather and hazards are shared state, compared through the RNG and the fingerprint. Kyler's long
session (release bar 7) is the proof.

**O4-1. Every player saw every colony's Indicator warnings.** Confirmed. Kind: display. Hits: separate colonies.
- `Indicator.EvaluateRisingEdge` → `ShowWarning` → `QuickNotificationService.SendWarningNotification` (a top-of-screen
  notice). It runs in the tick on every computer, unfiltered. The journal entry was already filtered
  (`ColonyViewNotificationPatcher`).
- *Fix:* `Colonies/ColonyIndicatorView.cs`. While colonies are shown apart, only the indicator's own colony's player
  gets it (try/catch, since it runs in the tick). Display only.
- *Check:* RuntimeChecks "O4: an Indicator's warning shows to its own colony's player only…".

Pinned indicators and levers still list the other colony's (display; left).

### H1: the crash sweep (sweep 7) for my area

**Scope.** Every entry point in `Events/AutomationEvents.cs`, `Events/EntityUIEvents.cs` (Wonder events excepted),
`Fixes/*` (bar `WonderTimingFix`, `AnimationFixes`), `ColonyRulesService`, `ColonyRules`, `ColonyGameWorld`,
`ColonySeparationPatches`, `ColonyWorkingHours` and `ColonyScienceService`, plus my new files. Each was checked against
the six late-game states in the plan's H1.

**Game facts the verdicts rest on:**
- `EntityService.Delete` takes an entity out of the registry at once, so id lookups return null for an entity deleted
  earlier in the tick.
- `(bool)component` reads the GameObject.
- `SingletonManager.GetSingleton` returns null, never throws.

**Verdicts:**
- **`AutomationEvent.Replay`** (every setter in the list): safe.
  - A deleted target is skipped.
  - A deleted automator argument deserialises to null, and every such game setter accepts null
    (`AutomatorConnection.Connect(null)` disconnects).
  - `Relay.RemoveInput` past the count is a no-op, and `SetInput` past it grows the list.
  - Argument types (long→int/enum, double→float) are guarded.
- **All other replays:** null-check their lookups. This is kept as a StabilityTests scan, "H1: every replayed action in
  the automation and panel events skips a building gone before it is played".
- **Finalizers:** those in these files don't swallow. `DistrictBuildingsFix`'s swallowing is safe: the game throws
  before writing, and with the co-op fixes the maps change at the same moments everywhere.

**Found and fixed:**

**H1-1. A recipe from a mod only one player runs threw out of the replay.** Confirmed. Kind: session-stopper. Hits:
games with different mods.
- `RecipeSpecService.GetRecipe` is a dictionary indexer. The event expected null, and the `KeyNotFoundException` ended
  the session for everyone.
- *Fix:* `TryGetRecipe` (`EntityUIEvents.cs:133`). A guest meeting one throws `MissingContentException` (it leaves
  quietly, nothing played). The host refuses a guest's recipe it doesn't have (`ColonyRulesService.cs:151-160`), like
  buildings.
- *Check:* RuntimeChecks "H1: a recipe this game does not have no longer throws out of a replay…" (it runs the game's
  real `GetRecipe` on an empty table).

**H1-2 (UI only):** two quick "add input" clicks before the first is played could give a relay nine inputs. The mod's
panel postfix then indexed the eighth-row list past its end every frame. Now bounded (`AutomationEvents.cs:644`).

**H1-3 (UI only):** the timer panel's static reference is tested with Unity's `bool` (`AutomationEvents.cs:473`).

**Left (defensive):**
- `ColonyRulesService.AllowOnHost` has catches around seating and judging only. A throw in its other checks would stop
  the session, but none was found (struct slot table, `(bool)` checks, null-safe `OwnerOf`). An outer try re-indents 130
  lines that B and C may also touch, so I left it: Main can add it at merge.
- `ColonyScienceContext.Enter` uses `OwnerOf` with the construction district (the doc says simulation code should pass
  false). It's consistent in co-op thanks to `InstantNavMeshFix`. Noted for whoever owns science next.

### H2: dev tools against `DevModeCoopWarning`

The text ("only its instant unlock, Finish now and Add 1000 Science are shared… its other tools change only your game
and will desync it") is **true as a rule** for the whole dev list:
- **The plan's nine** (Expire, Explode and its delayed form, Give all, Modify Inventory, Progress construction, Delete
  without exploding, Spawn newborn, Finish now) and the battery slider.
- **Also found by the sweep:** kill selected (**the ordinary Delete key** in dev mode), kill part or all of the
  population, long-lasting corpses, jump to the next day or season, reset the water simulation, forced wind, the water
  brush, placing beavers, bots and map objects, need editing, character control, core radius, complete or revoke the
  Wonder, faction unlocks in settings, and dev planting (now off, A2-4).

Two gaps:
- **The dev power generator isn't a dev tool once placed** (A2-3, now shared).
- **Three Ctrl dev keys do nothing in co-op** (place finished, no recovery, plant spawn), so "change only your game" is
  slightly wrong for them.

Proposed text (Main's call; I didn't edit the string): *"Dev mode is on. In co-op, only its instant unlock (Ctrl-click),
a construction site's Finish now and the dev panel's Add 1000 Science are shared, and only while the host has dev mode
on. Its other tools and keys (the debug buttons, the dev panel, Delete on a selected beaver, the beaver and bot tools)
change only your game and will desync it."*

### A5, A6

**A5 wiring over time (Sound).**
- *Creating a wire:* judged with both ends (`SetAutomatableInputEvent`, `AutomationEvent`'s entity arguments).
- *Copying settings:*
  - a placement copied from another colony's building is placed plain;
  - `DuplicationEvent` needs both ends;
  - the water synchronisers no longer rewire another colony's buildings (W1).
- *Can a building change colony later?*
  - roads never join, so a building can't be reassigned to another colony's district;
  - a handover moves a whole colony;
  - a faction switch needs an untouched colony.
- So a partition can't come to span colonies. G6 (open Memory/Relay panels re-applying inputs) is unchanged.

**A6 automation at scale (Plausible cost, Script P).**
- *Every automator placed or removed:* `AddAutomator` or `RemoveAutomator`, merges, then `UpdateSecondaryPartitionLists`,
  which is O(partitions) plus rebuilding only invalidated plans. A 50-building blueprint is 50 of these.
- *In co-op, every placement:* a replayed creation (one frame interrupt per frame of replays).
- *Estimate:* negligible against the buildings' own creation.
- *Measure:* Script P-A6.

---

## 2. Coverage matrix rows

| Feature | Desync | Crash | Cross-colony | Mixed | Cost | Save/rehost |
|---|---|---|---|---|---|---|
| Levers (plain, spring-return, pinned) | Sound: `SwitchState`/`SetSpringReturn`/`SetPinned` recorded; spring return as simulation (`LeverSpringReturnCoopPatcher`) | Sound: `AutomationEvent.Replay` skips a deleted lever; the game guards `(bool)` in `SpringReturnService` | Sound: scope names the lever | Sound: no faction code; D17 places each faction's own | Sound: one event per click | Sound: `IsOn`/spring/pinned saved; `_isPressed` unsaved and ignored in co-op |
| Relay, Memory, Timer, Chronometer, Weather Station | Sound: setters, intervals, resets (P-1) recorded; tick-only evaluation (A1 table) | Sound: null automators accepted, index past count no-op; H1-2 panel bound fixed | Sound: wires judged on both ends (A5) | Sound: as Levers | Sound: evaluation cost unchanged; frame path off saves work | Sound: game-saved state; tick-quantised `Time.time` |
| Depth, flow, contamination sensors | Sound: read `IThreadSafeWaterMap` in the tick (W2) | Sound: sampling guards `(bool)` district | Left: water is shared by design | Sound | Sound: game's own | Sound |
| Population Counter | Sound: sampled in the tick | Sound: sums over the registry's live list | Fixed: A4-1, RuntimeChecks "A4: …" | Sound: counts characters of both factions of its colony | Sound: O(districts) per global counter per tick, spot "Population counters (colony)" | Sound: nothing new saved |
| Resource, Science Counters, Power Meter | Sound | Sound | Sound: district / own colony; Left: Power Meter reads its whole network (P1) | Sound | Sound | Sound |
| Indicators, Speakers | Sound: settings recorded | Sound: O4 filter in try/catch | Fixed: O4-1 warnings, RuntimeChecks "O4: …"; journal already filtered; Left: pinned panels list all, a speaker in global mode plays for all | Sound | Sound: only on rising edges | Sound |
| Gates | Sound: at the tick (`GateTickRunner`), `SetOpeningMode` recorded | Sound (H1 sweep) | Sound: roads never join | Sound | Playtest: Script P, many gates switched by automation (D's lead) | Sound |
| Detonator and dynamite | Sound: chain in the tick, insertion-order sets. Fixed (behaviour, not a desync): A1-1, a one-tick pulse sets it off, RuntimeChecks "A1: …", StabilityTests "A1: …" | Sound: replays null-check; deletion ends the frame | Left: blasts reach anything (X2, documented) | Sound | Playtest: L-X1, a 41-charge chain's frames (P-5) | Sound: everyone loads the save (delay quirk the same everywhere); `_timeWhenArmed` unsaved and unused in co-op |
| HTTP Lever, HTTP Adapter, HTTP API | Sound: switch through recorded `SwitchState`; Fixed: A3-1 colour dropped, RuntimeChecks "A3: …" | Sound: a gone lever skipped; non-unique names ignored by the game | Sound: judged by colony, refusal notice | Sound | Playtest: L-A2 (a script flipping levers every second) | Left: adapter webhook settings are each computer's own until a rehost (documented) |
| Fireworks | Sound: launch in the sampling tick; the rocket's frame life is read by nothing | Sound | Sound: none | Sound | Sound | Sound: a save mid-flight is one computer's bytes, which everyone loads |
| Floodgates, fill valves, throttling valves | Fixed: A2-2 (valve limit toggle), RuntimeChecks "A2: the pumps' …" | Sound: synchroniser BFS unchanged; new patches only filter | Fixed: W1-1, RuntimeChecks "W1: …" | Sound | Sound: filter only while synchronising | Sound: nothing new saved |
| Pumps, water movers, input pipes, regulators, stream gauge | Fixed: A2-1 flow rate, RuntimeChecks "A2: …" | Sound | Left: water shared | Sound: each faction's pumps are B's F1 | Sound | Left: stream gauge marker reset is local until a rehost (display) |
| Water sources, seeps, badwater | Sound: three fixes still match 1.1.2.4 (W4) | Fixed: R8-1 (no throw at patch time or in `GetDeltaTime`), RuntimeChecks "R8: …", `TimingChecks`; H1 `finally` in `WaterSourceFix` | Left: shared by design | Sound | Sound | Fixed: R8-1 guard stops a seep map's co-op game if the fix can't apply |
| Power networks, clutches, batteries | Sound: P2 | Sound | Left: networks join across colonies, a clutch cuts both, a meter reads both (P1, documented) | Playtest: Script M, a shaft between an Iron Teeth and a Folktails colony | Sound | Sound: battery charge saved, changed in the tick |
| Dev power generator | Fixed: A2-3, RuntimeChecks "A2: …" | Sound | Sound: recorded with its entity's scope | Sound | Sound | Sound |
| Unstable cores, tunnels, dirt excavator, terrain blocks | Sound: X1, X3 | Sound | Left: blast and terrain reach (X2) | Sound | Playtest: L-X1 | Sound |
| Demolition with terrain (G9) | Sound: floats the same everywhere | Sound | Left: G9, documented; Fixed: X4-1 stale terrain list, RuntimeChecks "X4: …" | Sound | Sound | Sound |
| Ziplines, tubeways, beehives | Sound: links recorded and host-judged | Sound (H1 sweep, zipline replay) | Sound: towers the actor's; tubes are paths (road rule); Left: a beehive helps any crop in range | Playtest: Script M (B's F5) | Sound | Sound |
| Every panel, tool and key (sweep 1) | Fixed: A2-1..4; kept as RuntimeChecks "A2: every call the game's panels, tools and keys make…" | Sound | Sound | Sound | Sound: a check, nothing at run time | Left: saved display choices (light colour, decal, bell sound, gauge marker) are local until a rehost |
| Dev tools | Left: warned (H2); Fixed: A2-4 plant spawn off | Sound | Sound: dev shortcuts host-gated | Sound | Sound | Sound |
| Surviving a game update | Fixed: R8-1, RuntimeChecks "R8: …", StabilityTests "R8: …" | Fixed: R8-1 | Sound | Sound | Sound: one flag per frame | Sound |
| Workshop recipes from another mod | Fixed: H1-1 host refuses, guest leaves quietly, RuntimeChecks "H1: …" | Fixed: H1-1 | Sound | Sound | Sound | Sound |

---

## 3. Found sound (don't redo)

- **Automation core:**
  - No hash-ordered collection anywhere in `Timberborn.Automation`; partitions and plans are Lists and Queues, filled in
    registration order.
  - The frame path is off in co-op (`FrameToTickFixes.cs:156`).
  - Replays run before bucket 0, so a replayed setter is evaluated in its own tick.
- **The frame hooks of the late-game assemblies** (sweep 2), each covered or display:
  - `AutomationRunner.UpdateSingleton`, `GateUpdater`, `HttpApiIntermediary`;
  - the Wonder and plane hooks (`WonderTimingFix`);
  - `Firework.Update` (read by nothing);
  - `UnstableCore.Update` (dev only);
  - every animator, renderer and particle hook.
  - No frame hook at all in Bots, BotBehavior, PowerManagement, PowerGeneration, WonderCompletion,
    GameWonderCompletion, WaterContaminationBuildings, ZiplineMovementSystem, Illumination, Attractions,
    TerrainPhysics or Pollination.
  - `BotManufactoryAnimationController`'s draw is routed to the non-game generator (`DeterminismService.cs:464`).
- **Detonator's `Time.time`:** the mod's patched clock (A1-1 aside).
- **Tubeways:** they join nothing across colonies (PathSpec).
- **Water readers and writers, and the water-source fixes' premises** (W2–W4).
- **Mechanical graphs, batteries and clutches** (P2).
- **The dynamite and core chain:** in the tick, insertion-ordered, kills through `CharacterKilledEvent`.
- **Tunnels, the dirt excavator and terrain blocks** change terrain in the tick.
- **The panels' district switchers:** they don't feed the automation.
- **Every replay in my event files** null-checks its lookups (StabilityTests scan).
- **Every automation setter** accepts a null automator and a stale index.
- **Rehost:** the host reloads the save too (`RehostingService.LoadGame`), so unsaved fields (`_ticksToDetonate`,
  `_timeWhenArmed`, `_isPressed`) are reset on every computer alike.

## 4. Left, and why

- **P1 power across colonies, X2 blast reach:** the review's defaults, deterministic. Document.
- **G9:** the terrain a co-op deletion leaves. A fix exists (recompute the support stack at replay), but it needs a
  colony rule for stacks holding another colony's building. Not small. Document.
- **Saved display choices stay local until a rehost:** a light's colour (the panel), a decal, a bell's sound, a stream
  gauge's marker, an HTTP Adapter's webhook settings. Nothing simulated reads them.
  - Recording the colour picker would also need a guard on `SetCustomColor`'s in-tick callers (indicator colour
    replication). That's a product call: I propose recording the light colour and decal choices in 1.4.x.
- **Two opposite actions played in one tick lose the pulse** (A1 table). Rare by hand; it only matters for sub-tick
  signals from the HTTP API.
- **A held spring-return lever** stays on one tick in co-op (as before rc1).
- **Dev mode's delayed detonation keys** are ignored in co-op (dev only).
- **Pinned indicators and levers** list the other colony's (display). **A speaker in global mode** plays for every player.
- **`AllowOnHost`'s outer try** (defensive, no path found); **`StartMod`'s patch failures** (Main's R8, above).
- **Game quirks to report to Mechanistry, not ours:** `ExplosionData` never removed; `CharacterExploder` skipping a
  second beaver on the tile; `_ticksToDetonate` not saved.

## 5. Doc text and playtest steps

**TWO-COLONIES, *Known limits*** (proposed):
- *Power:* "Shafts of two colonies that touch make one power network: both colonies' engines and batteries feed it, a
  clutch of either colony cuts it, and a Power Meter reads all of it."
- *Blasts:* "A dynamite or unstable core blast destroys whatever it reaches, the other colony's buildings and beavers
  included, as terrain and water are shared."
- *Beehives:* "A beehive helps every crop in its range, the other colony's too."
- *G9:* "Demolishing a platform that holds up dirt from terrain blocks leaves the dirt floating in co-op (single player
  removes it). Remove the dirt first."
- *Local display choices:* "A light's colour, a decal, a bell's sound, a stream gauge's marker and an HTTP Adapter's
  webhook settings are each player's own until a rehost, which keeps the host's."

**TWO-COLONIES, the colony rules** (what changed):
- "Synchronised floodgates, fill valves and throttling valves keep in step with their own colony's only."
- "A Population Counter set to count everywhere counts its own colony."
- "An Indicator's warning is shown to its own colony's player."

**README, co-op notes:**
- *HTTP API:* "The HTTP API works in co-op: each player's computer runs its own server (start it from the HTTP Lever's
  panel), and a request to switch an HTTP lever is that player's action, shared with everyone and refused for another
  colony's lever. Requests to colour a lever are ignored in co-op. An HTTP Adapter's webhooks are called by each
  player's computer with the settings made there."
- *Automation timing:* "In co-op the automation runs on the game's ticks: a change shows up to one tick later than in
  single player, and a spring-return lever gives a pulse of one tick (it can't be held on)."
- *Game updates:* "If a Timberborn update changes a part of the game Timber Together corrects, co-op stops at load with a
  message instead of going out of step; single player is unaffected. Update Timber Together."

**Script L lines** (each: what should happen; send both `Player.log` and Ctrl+Shift+J):
- **L-A1:** on each colony, build a lever, a relay, a memory, a timer and an indicator in a chain. Flip levers on both
  computers, one player capped at 15 fps.
  - Both see the same lights within a tick.
  - Pause, flip, unpause: the change shows on the first tick after unpausing, the same on both.
- **L-A2 (HTTP):**
  - Both players start the HTTP API. Each switches their own HTTP lever by URL
    (`http://localhost:8080/api/switch-on/<name>`): it switches for both.
  - A guest switching the host colony's HTTP lever by URL is refused with a notice.
  - `/api/color/<name>/ff0000` does nothing in co-op (the log says so once).
  - An HTTP Adapter with a webhook set on the host is called from the host's computer only.
- **L-A3 (Detonator):** a spring-return lever wired to a Detonator on a dynamite. One click: it explodes, on both
  computers, the same tick (compare the logs). Before rc1 it didn't explode.
- **L-A4:** a Population Counter in global mode in each colony, threshold between the two colonies' populations: each
  lights by its own colony's count, the same on both computers.
- **L-W1:** drag a pump's flow rate and a throttling valve's outflow from "unlimited" to half, on the guest.
  - The water moved (the pump's panel) and the valve's state read the same on both computers.
  - The daily check stays green.
- **L-W2:** two colonies' floodgates side by side along the border, synchronising on. Change one colony's height and
  wire it to a lever: only that colony's gates follow; the other colony's keep their height and wiring.
- **L-W3:** a drought and a badtide with automated floodgates, valves and regulators on both colonies: no mismatch
  through both.
- **L-X1:** on a copy of `R-blast` split into two colonies, set off a chain beside the other colony.
  - Same result on both.
  - No exception.
  - Note how many frames it takes at speed 3 (P-5).
- **L-O4:** an Indicator with "warning" on each colony: each player sees only their own colony's warning notice.
- **L-R8 (only after a Timberborn update):** if co-op stops at load with "Co-op has stopped. This version of Timberborn
  changed…", send the log.

**Script P line:** **P-A6:** place a 50-building automation blueprint in co-op on `R-late`; record the frames around
the placement (PerformanceLog).
