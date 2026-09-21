# Plan: two-colony co-op alpha for the BeaverBuddies Stability Fork

Written 2026-09-20 for an AI coding assistant that will implement it. This document is a plan only: it contains no code and none of it has been tried in a game.

**How to read the labels.** **(read)** = verified on 2026-09-20 by reading this repo at main (`f61d524`, v1.1.10) or by decompiling the installed Timberborn 1.1.2.4 assemblies with `ilspycmd`. **(decision)** = a design choice made in this plan; follow it unless a Phase 0 finding proves it wrong, and then write down why. **(unverified)** = believed but not checked; Phase 0 must settle it before code depends on it.

---

## 1. Goal and exit criteria

**Goal.** A pre-release build of the mod in which two players on one multi-start map each control their own colony: own district(s), own beavers, own buildings, own land. Neither can act on the other's colony. The two colonies can exchange goods through District Crossings, with each player in control of what their colony gives and takes.

**The alpha is done when all of these are true:**

1. A zip `BeaverBuddies-Stability-Fork-1.2.0-two-colony-alpha1.zip` exists, built with 0 warnings in both configurations, in the normal release layout, with a SHA-256 file.
2. All existing automated checks still pass (StabilityTests, RuntimeChecks, the Python checks), and the new checks in section 8 pass.
3. With the mode **off**, behaviour is the same as v1.1.10 (the mode is opt-in per new game and saved in the save; old saves and shared-colony games are untouched).
4. The user has run the **solo host script** (section 8.3) and the **two-player script** (section 8.4) and reported the result for every line. A line that fails is either fixed or listed in the release notes under "Known problems". The scripts cover: separate starts, refused actions on the other colony, refused building across the border, a working crossing built half by each player, goods moving only when both sides allow it, no beavers migrating between the colonies, save and rehost keeping everything, and no desync in 30 minutes of play.
5. Docs are updated (section 9) and say plainly what was and was not seen in a game.

Nothing is published, merged, tagged or pushed except on the user's word (section 10).

---

## 2. Facts the design rests on

### 2.1 The mod (all **read**)

- **Event flow is host-authoritative.** A guest never plays its own actions: `ClientEventIO.UserEventBehavior` is `Send`. The guest's events go to the host; the host replays them in `ReplayService.ReplayEvents` ([ReplayService.cs:294](BeaverBuddies/ReplayService.cs:294)) and only **after a successful `Replay`** re-queues them for broadcast (`EnqueueEventForSending`, line 340). Guests replay only what the host broadcasts. The host's own actions are queued (`QueuePlay`) and go through the same `ReplayEvents` loop. **Consequence: a rule evaluated only on the host, just before `replayEvent.Replay(this)`, decides for everyone. An event the host refuses is never broadcast, and an event the host rewrites is broadcast rewritten.** This removes the brief's biggest risk (every computer evaluating ownership and disagreeing).
- **Events do not carry the sender.** `ReplayEvent` has `ticksSinceLoad` and `randomS0Before` only ([ReplayEvent.cs:16](BeaverBuddies/Events/ReplayEvent.cs:16)). `LocalPlayerID` is unused.
- **The host knows who sent what, but drops it.** `TimberServer.playerIds` maps each connection to a number (host is 0, guests 1, 2, ... in connection order, [TimberServer.cs:155](TimberNet/TimberServer.cs:155)). The listener loop in `TimberNetBase` has the source connection in hand (`client`) when it does `receivedEventQueue.Enqueue(control)` ([TimberNetBase.cs:438](TimberNet/TimberNetBase.cs:438)) but does not record it. Chat and activity frames are already stamped this way (`activity.WithPlayerId(id)`), so there is a pattern to copy.
- **Guests send a `GroupedEvent`** (one JSON object wrapping a list). `ReplayService.ReadEventsFromIO` flattens it ([ReplayService.cs:283](BeaverBuddies/ReplayService.cs:283)). A stamp on the wrapper must be copied to the children there.
- **A guest knows its own number**: `NetworkStatus.YourPlayerId` (from the roster the host sends).
- **Multi-start exists.** `StartingBuildingInitializerInitializePatcher` ([MultiStartPatches.cs:39](BeaverBuddies/MultiStart/MultiStartPatches.cs:39)) places one starting building per `StartingLocation`, ordered by `StartingLocationPlayer.PlayerIndex`, registers each in `StartBuildingsService`, then **deletes the starting locations**, so the player index is lost after the first frame of a new game. `GameInitializerSpawnBeaversPatcher` spawns a set of beavers at each. The player count comes from a field injected into the custom new-game panel; that panel code is commented as buggy in places.
- **Placement replay already builds a preview object** to validate placement (`BuildingPlacedEvent.IsPlacementValid`, [ToolEvents.cs:66](BeaverBuddies/Events/ToolEvents.cs:66)): it instantiates the blueprint, calls `blockObject.Reposition(placement)` and `IsValid()`. The same preview gives the footprint tiles.
- **Area tools carry explicit lists**: `PlantingAreaMarkedEvent.inputBlocks`, `TreeCuttingAreaEvent.coordinates` (tile lists), `ClearResourcesMarkedEvent.blocks` and `BuildingsDeconstructedEvent.entityIDs` (entity lists). Lists can be filtered in place.
- **District events exist**: `ManualMigrationEvent` (from district, to district), `SetDistrictMigrationToggledEvent`, `SetDistrictMinimumPopulationEvent`, `GoodDistributionSettingChangedEvent` (district entity, good, export threshold, import option), all in [BatchEvents.cs](BeaverBuddies/Events/BatchEvents.cs).
- About 50 `ReplayEvent` subclasses in `BeaverBuddies/Events/*.cs` plus `GroupedEvent` and `HeartbeatEvent` in `ReplayService.cs`.
- **Tests**: `StabilityTests` is headless .NET 8 with Unity/Steam doubles and runs the real TimberNet server and client over fake sockets. `RuntimeChecks` loads the compiled mod DLL against the game's managed assemblies. UI Toolkit cannot run in either.

### 2.2 The game (all **read** from decompiled 1.1.2.4)

- **A District Crossing is two separate buildings placed back to back.** Each has a `LinkedBuilding` component; `LinkedBuilding.LinkIfNeeded` looks for another `LinkedBuilding` at `_blockObject.CoordinatesBehind()` and links the pair. No tool code places the second half (only `Timberborn.BlockSystem` and `Timberborn.LinkedBuildingSystem` mention `CoordinatesBehind`). Construction of the pair is coupled (`LinkedConstructionSite`), and deleting one deletes the other (`LinkedBuilding.DeleteEntity`).
- **Each half belongs to the district on its own side** (it is a `DistrictBuilding`). `DistrictCrossingValidator.CanExport` requires both halves finished and assigned to **different** districts.
- **Goods already flow through a crossing without any player action.** Each half has an inventory (`DistrictCrossingInventory`) and is a workplace (`DistrictCrossingWorkplaceBehavior`): the district's own workers carry goods to their half, `TransferStock` moves them to the linked half, and the other district's workers empty it. `DistrictCrossingAutoExporter` does the same on tick for stock already there.
- **What flows is decided per district and per good** by `GoodDistributionSetting`: an `ExportThreshold` (exporter side; `DistributableGood.CanExport` is false when the threshold is 1 or more) and an `ImportOption` of `Disabled`, `Auto` or `Forced` (importer side; `DistrictDistributableGoodProvider.CanBeImported` returns false for `Disabled`). Export happens only when the exporter's fill rate is above its threshold **and** above the importer's fill rate **and** the importer accepts the good. Defaults from `GoodDistributionSetting.SetDefault`: threshold 0, import `Auto` (or `Forced` for goods with `ForceImport`). So with vanilla defaults a crossing between two colonies would level out their stocks of everything.
- **This is the trade system.** Nothing new has to be built for goods to move; the mod only has to choose the defaults and make sure each player can change only their own side. The mod already replays the setting change (`GoodDistributionSettingChangedEvent`).
- **Automatic migration has one choke point.** `MigrationCoordinator` picks a partner district only through `MigrationNeighbours.GetHighestSpareNeighbour` and `GetLowestSpareNeighbour`, which loop over `DistrictConnections.GetDistrictsConnectedWith(center)`. Districts joined by a crossing are in one `DistrictCluster`, so vanilla would move beavers between the two players' colonies on its own.
- **Which district builds and owns a building is decided by roads**, not by who placed it: `DistrictConstructionAssigner` gives a construction site to the first finished District Center whose instant road network reaches it, and `DistrictBuildingAssigner` does the same for finished buildings. A building placed beside the other player's road would be built by, and become part of, the other player's district. **So ownership cannot simply be "who placed it"; land has to be divided.**
- `DistrictConflictDetector` (in `Timberborn.Navigation`) reports a conflict when two districts' roads meet without a district obstacle between them. Crossing halves are district obstacles.
- **Science** (kept shared in the alpha, recorded for later): the producer the brief could not find is `Manufactory`, which calls `ScienceService.AddPoints(CurrentRecipe.ProducedSciencePoints)` when a production cycle finishes (`Timberborn.Workshops/Manufactory.cs`, near line 358). `ScienceAdder` in `Timberborn.ScienceSystemUI` also calls `AddPoints` (a dev tool by its name; **unverified**).
- Decompiling is quick: `ilspycmd -p -o <scratchpad>\dc\<Name> "<Managed>\Timberborn.<Name>.dll"`. Useful assemblies: `DistributionSystem`, `DistributionSystemBatchControl`, `GameDistricts`, `GameDistrictsMigration`, `GameDistrictsMigrationBatchControl`, `LinkedBuildingSystem`, `Navigation`, `BlockSystem`, `BlockObjectTools`, `ScienceSystem`, `GameOver`. Decompile into the scratchpad, never into the repo or the game folder.

---

## 3. Design decisions

### D1. Land is divided by a fixed border; ownership is a function of position

Every map tile belongs to the colony whose **starting building is nearest** (squared distance in X and Y only, height ignored, integer arithmetic, ties go to the lower colony number). With two colonies the border is the perpendicular bisector between the two starts.

- **The owner of an entity is the owner of the tile at its `BlockObject.Coordinates`.** No owner component, no stamping at creation, nothing extra to save per building. Because a player can only build on their own land (D3), "who placed it", "whose land it is on" and "whose district it joins" always agree.
- Things without a `BlockObject` (beavers, bots): the alpha does not restrict actions on them (the only such event found is renaming).
- The owner of a district is the owner of its District Center's tile.
- The rule sits behind one small service (working name `ColonyTerritory`: owner of a tile, owner of an entity, is a tile in the border strip). A later version can swap in a different rule (painted by the map author, or growing with roads) without touching the callers.

Why this and not "owner stamped on each building" or "land follows road reach" from the brief: both need more saved state and more patched game code, and road reach changes over time, which makes "whose is this tile" depend on nav-mesh timing. A fixed border is a pure function that can be fully tested headless, is trivial to explain to players, and makes the crossing location obvious. Its cost is a crude split on asymmetric maps; that is acceptable for an alpha and the map author controls it by where the starts are placed.

### D2. The border strip

The tiles on each side that touch the other colony's land (4-neighbour in X/Y, at any height) form the **border strip**. In the strip a player may place **only District Crossing halves** (templates that carry `DistrictCrossingSpec`). Everything else is refused there.

Reason: if both players could build paths up to the border, their roads would touch and the game would raise a district conflict and start reassigning buildings across colonies. The strip guarantees a gap that only a crossing (a district obstacle) can bridge. Each player builds the half on their own side, back to back, which is exactly how the game links a pair (2.2).

**Unverified, Phase 0:** the footprint and entrance layout of a crossing half, i.e. that a half fits in the one-tile strip with its back on the border and its entrance facing its owner's land. If a half is deeper than one tile, change the rule to "a crossing half may overlap the strip; its `CoordinatesBehind` tile must be the other colony's strip". Do not widen the strip without a reason.

### D3. Who is acting: the host stamps every event

- Add a `player` number to `ReplayEvent` (default 0).
- **Guest events:** the host writes the connection's number onto the received JSON object before it is queued, overwriting whatever the guest sent (a guest must not be able to claim to be someone else). Do it with a small virtual hook in the `TimberNetBase` listener loop that `TimberServer` overrides, next to where chat and activity are already stamped. The wrapper `GroupedEvent` gets the stamp; `ReadEventsFromIO` copies it to each child when flattening.
- **Host events:** stamped with 0 in `ReplayService.RecordEvent`.
- The stamp rides along when the host re-broadcasts, which costs nothing and lets guests show "who did this" later.

### D4. Seats: which colony a player number controls

- The host controls colony **H**, every guest controls the **other** colony. H is a host-side mod setting ("I play colony 1 / 2", default 1) read when the session starts and sent to guests in `InitializeClientEvent` so their UI knows their own colony.
- This makes seats independent of connection order, so "Save and Rehost" keeps them. If the friend hosts the save next time, they set the setting to the colony they were playing. The brief's Steam-ID mapping is not needed for two players. A third player who joins plays colony 2 together with the other guest; that is allowed and not tested.
- Seats are **not** simulation state. Only the host evaluates rules (D5), and guests use their seat for UI only.

### D5. Rules are enforced on the host only, at replay time

In `ReplayService.ReplayEvents`, just before `replayEvent.Replay(this)`, when the mode is on, ask a rule service (working name `ColonyRules`) for a verdict:

- **Allow**: replay as today.
- **Refuse**: log one line (who, what, why), do not replay, do not broadcast, carry on with the next event. It is not a replay failure and must not call `AbortReplay`.
- **Rewrite**: filter the event's own list in place (area tools), then replay and broadcast the filtered event. If the list ends up empty, refuse.

The rule code must only read state: no entity creation left behind, no `UnityEngine.Random` use, no game events posted. The placement footprint needs a preview object; reuse the one `IsPlacementValid` already creates (extend that helper to also return the footprint tiles) so no second instantiation happens, and keep the existing special case that skips validation for District Centers in mind: a District Center still needs its footprint checked against the border, so get its tiles without calling `IsValid()`.

Guests do **not** re-check. When the mode is off the whole step is skipped.

Every event type must declare how it is judged. Add an abstract-or-virtual member on `ReplayEvent` that returns the event's scope, with these kinds:

| Kind | Rule | Events (from the current list; the implementer completes it) |
|---|---|---|
| Global | always allowed | `SpeedSetEvent`, `ShowOptionsMenuEvent`, `AutosaveEvent`, `PingEvent`, `HeartbeatEvent`, `GroupedEvent`, `InitializeClientEvent`, `ClientDesyncedEvent`, `TraceLoggedForTickEvent`, `WorkingHoursChangedEvent`, `BuildingUnlockedEvent`, `WorkerTypeUnlockedEvent`, `EntityRenamedEvent` |
| Entity | every named entity must be on the actor's land; a missing entity is left to the event's own handling | all per-building events in `EntityUIEvents.cs` and `AutomationEvents.cs`, `DemolishButtonClickedEvent`, `DynamiteTriggeredEvent`, `GoodStackDeletedEvent`, `WonderActivatedEvent`, `ZiplineConnectionChangedEvent` (both ends) |
| District | every named district must be the actor's | `ManualMigrationEvent` (both from and to: this is what blocks sending beavers to the other colony), `SetDistrictMigrationToggledEvent`, `SetDistrictMinimumPopulationEvent`, `GoodDistributionSettingChangedEvent` |
| Placement | every footprint tile on the actor's land; strip tiles only for crossing halves | `BuildingPlacedEvent`. The duplication source may belong to anyone (copying settings is harmless) |
| Tile list | keep only the actor's tiles | `PlantingAreaMarkedEvent`, `TreeCuttingAreaEvent` |
| Entity list | keep only entities on the actor's land | `BuildingsDeconstructedEvent`, `ClearResourcesMarkedEvent`, `DuplicationEvent` if it names targets |

A reflection check (section 8.2) fails the build's checks when any `ReplayEvent` subclass has not declared a scope, so a future event cannot be forgotten.

### D6. Local refusal for good UX

In `ReplayEvent.DoPrefix` (the single funnel every patch goes through), when the mode is on and the session is live, judge the event locally with the local player's colony before recording it. If it would be refused: do not record, show a short in-game notice ("That belongs to the other colony" / "You can only build on your own land" / "Only a District Crossing can be built on the border"), and return false so the original method does not run. For rewrites, record the event unfiltered and let the host filter it. This layer is a courtesy; the host's verdict is the one that counts, so a bug here can never desync. Check the patches that do cleanup when `DoPrefix` returns false (for example `BuildingDeconstructionPatcher`) still behave.

Use the game's existing quick-notification mechanism for the notice (**unverified** which service; find it in Phase 0). New strings go in the mod's localization files.

### D7. Simulation rules that run on every computer

These change the simulation, so they run identically everywhere and may read only saved state (the mode flag and the start coordinates, D8):

1. **No automatic migration between colonies.** Replace `MigrationNeighbours.GetHighestSpareNeighbour` and `GetLowestSpareNeighbour` with copies that skip neighbour districts whose owner differs from the asking district's owner. Mark them `[ManualMethodOverwrite]` with the dated original in a comment, as the repo does elsewhere. Same-owner districts migrate as in vanilla.
2. **Trade is closed until a player opens it.** When the mode is on, `GoodDistributionSetting.SetDefault` ends with `ImportOption.Disabled` for every good (including `ForceImport` goods). A colony receives a good only after its owner sets that good to Auto or Forced in the game's own distribution screen; the giver limits what leaves with the game's own export threshold. Both controls already exist in the game UI and are already replayed, and D5 makes each side changeable only by its owner. The mode flag must be set **before** the starting District Centers are created (they call `SetDefault` when they are created), and settings loaded from a save must not be touched.

Both patches must do nothing when the mode is off.

### D8. Saved state and how the mode is switched on

- A saved singleton (working name `ColonyModeService`, `ISaveableSingleton` + `ILoadableSingleton`, registered in the game-scene configurator like the other services) holds: mode enabled, and the list of colony start coordinates in colony order. Nothing else. It travels to guests inside the save the host sends, so everyone has identical values.
- **Switching on:** a host mod setting "New multi-start games use separate colonies" (default off), read inside `StartingBuildingInitializerInitializePatcher` when it takes the multi-start path with 2 or more starts. There, before the first `Place`, set enabled and record each start's coordinates in player-index order. A mod setting is chosen over another injected field in the new-game panel because that panel code is the part the repo itself calls buggy.
- In the alpha the mode supports exactly the number of starts that were placed; rules are written for N colonies but only 2 are tested.
- If the save has the mode on but fewer than 2 start coordinates, treat the mode as off and log an error.

### D9. Seeing the border

Players must be able to see where their land ends, or the refusals will feel random. **Decision:** show the border with the game's own tile or area highlighting while a placement or area tool is active, plus a key binding to toggle it at any time (the repo already has a `KeyBindings` folder and blueprint pattern). **Unverified:** which game service draws tile highlights and what it costs for a line a few hundred tiles long; Phase 0 finds it. This is a rendering change: per the user's standing rule it is not called done until the user has sent a screenshot. If no suitable game service exists, the fallback for the alpha is the refusal notices alone plus a line in the connection panel ("You: colony 2, east of the border"), and the release notes say so.

### D10. Deliberately not in the alpha

| Left out | What happens instead | Note for later |
|---|---|---|
| Per-player science and unlocks | shared, as today | producer is `Manufactory` → `ScienceService.AddPoints` (2.2); credit by the manufactory's tile owner |
| Per-player working hours | shared | |
| Game over per colony | vanilla: only when every beaver on the map is gone | `GameOverChecker.IsGameOver` |
| Per-colony top-bar numbers | vanilla: the counters follow the selected district, which is normally your own | |
| Hiding the other colony's buttons and panels | panels open, actions are refused with a notice | |
| Shared-mark leakage | tree-cutting, planting and demolish marks are global sets; a lumberjack whose range crosses the border will cut trees the other player marked there | acceptable in co-op; list under known limits |
| Two factions, more than 2 colonies, competitive rules | not supported / not tested | |
| Invalid-looking placement preview across the border | preview looks valid, placing is refused with a notice | stretch goal if a block-object validator hook turns out to be cheap |

---

## 4. Phases

Work in order. Each phase ends with its checks green and a commit on the feature branch. Do not start Phase 3 before Phase 0's findings are written down.

### Phase 0: verify the unknowns (no product code)

Write the findings into a short section at the bottom of this file or a sibling notes file; later phases cite them.

1. **Crossing half geometry.** Find the District Crossing blueprint (look for the game's exported blueprints under `Timberborn_Data\StreamingAssets`, else decompile what reads them): footprint size, where the entrance is, what `CoordinatesBehind` resolves to. Confirm or adjust D2.
2. **How the player places a crossing in vanilla**: two separate placements, or one action that yields two `BuildingPlacer.Place` calls. Either works with D5; the script in 8.4 must describe the real steps.
3. **Footprint API**: the right member to list a repositioned preview's occupied tiles (something on `BlockObject` / `PositionedBlocks`), and how to get it for a District Center without calling `IsValid()`.
4. **Entity lifecycle for D7.2**: confirm `SetDefault` runs at District Center creation and not when settings load from a save (read `DistrictDistributionSetting`, lines near 79 and 106).
5. **Distribution screen**: read `Timberborn.DistributionSystemBatchControl` and confirm a player can set import to Disabled/Auto/Forced and an export threshold per good per district, and that the mod's existing patch records both. Note the slider's range.
6. **Notification service** for D6 and **highlight service** for D9.
7. **Host alone**: confirm from code that a host with no guests still runs `ReplayEvents` for its own actions (it should: `ServerEventIO` uses `QueuePlay`). The solo test script depends on it.
8. **`DistrictConflictDetector`**: read what happens on a conflict, to describe the failure the strip prevents and to know what to look for in logs.
9. Re-grep the full `ReplayEvent` subclass list and complete the table in D5.

Exit: every **unverified** item in this plan is either confirmed, or the plan is amended with the reason.

### Phase 1: identity plumbing (headless-testable)

- `player` on `ReplayEvent`; host-side stamp hook in TimberNet; copy from `GroupedEvent` to children; host stamps its own events.
- Seat setting, seat sent in `InitializeClientEvent`, a small seat service (local colony, colony of a player number).
- A **debug seat override** usable only by a host with the mod's debug setting on: a key binding that flips which colony the host's own events are stamped as. It exists so one person can test every rule alone (8.3). It must be impossible to use as a guest and must log each flip.
- Tests: 8.1 items 1 to 3.

Exit: tests green; with the mode off, a build behaves as 1.1.10 (the stamp is inert).

### Phase 2: mode, territory, saved state (headless-testable)

- `ColonyModeService` (D8), the host setting, activation inside the multi-start initializer before the first `Place`.
- `ColonyTerritory` (D1, D2) as pure logic with no Unity or game types beyond plain integers, so StabilityTests can link the source file directly the way it links other production sources.
- Tests: 8.1 items 4 to 6.

Exit: tests green; a new multi-start game with the setting on saves and reloads the flag and coordinates (checked in the solo script later).

### Phase 3: host-side rules

- Scope declaration on every event (D5 table), `ColonyRules`, the verdict step in `ReplayEvents`, in-place filtering for list events, footprint from the shared preview helper.
- One log line per refusal, prefixed consistently (for example `[Colony]`) so the test scripts can grep for it.
- Tests: 8.1 items 7 to 9 and 8.2.

Exit: tests green; build 0 warnings. **User checkpoint A**: hand over a test zip and the solo script (8.3). Continue with Phase 4 while waiting, but fix anything A finds before Phase 6.

### Phase 4: simulation rules

- The two `MigrationNeighbours` overwrites and the `SetDefault` rule (D7), both inert when the mode is off.
- Tests: 8.1 item 10 where the logic can be separated from game types; otherwise covered by 8.3 and 8.4.

Exit: tests green.

### Phase 5: player-facing layer

- Local refusal with notices in `DoPrefix` (D6) and localization strings.
- Border display and key binding (D9), or the documented fallback.
- Connection panel: show each player's colony beside their name if it fits the existing row layout without redesign; skip it otherwise.

Exit: build 0 warnings; the user has seen a screenshot-worthy build (they confirm visuals, not the implementer).

### Phase 6: alpha package and playtest

- Version `1.2.0-two-colony-alpha1` in `BeaverBuddies.csproj` and `manifest.json` (a dash suffix is safe for the game's manifest parser and the join handshake compares the string as is). Changelog entry at the top of `STABILITY-CHANGELOG.md`.
- Build both configurations, run every check, make the zip and the SHA-256 file (section 10), send both to the user with the two scripts.
- **User checkpoint B**: the two-player script (8.4). Fix and rebuild as `alpha2`, `alpha3` as needed; both players must reinstall each time because rebuilds are never byte-identical and the handshake compares module IDs.

Exit: section 1's criteria.

---

## 5. Files expected to change

New (suggested folder `BeaverBuddies/Colonies/`): `ColonyModeService`, `ColonyTerritory`, `ColonySeats`, `ColonyRules`, `ColonyMigrationPatches`, `ColonyTradeDefaultsPatch`, `ColonyBorderOverlay`, a configurator bound the same way `MultiStartConfigurator` is. New doc `TWO-COLONIES.md` at the repo root beside the other feature docs.

Changed: `Events/ReplayEvent.cs` (player, scope member, local refusal in `DoPrefix`), every file in `Events/` (scope declarations; list filtering in `ToolEvents.cs`; footprint from the preview helper), `ReplayService.cs` (stamp own events, copy stamp when flattening, verdict step), `Events/ConnectionEvents.cs` (seat in `InitializeClientEvent`), `MultiStart/MultiStartPatches.cs` (activate the mode and record starts), `Settings.cs` (two host settings, debug seat), `TimberNet/TimberNetBase.cs` and `TimberServer.cs` (stamp hook), localization files, key-binding blueprints, `StabilityTests/*` and `RuntimeChecks/*` (new check files and their registration in each `Program.cs`), `README.md`, `STABILITY-CHANGELOG.md`.

Match the surrounding code: same comment density, `[ManualMethodOverwrite]` with a dated copy of the original for every replaced game method, `Plugin.Log` for logging.

---

## 6. Determinism rules for this feature

1. Host-only rule code reads, never writes, and never touches `UnityEngine.Random`.
2. Code that runs on every computer (D7) may depend only on: the saved mode flag, the saved start coordinates, and entity positions. Never on seats, settings, player numbers, UI state, wall-clock time or connection state.
3. No floating point in territory maths.
4. No new iteration over unordered collections in simulation code. The migration overwrite keeps the game's own loop and only adds a skip.
5. A refused event must leave no trace in the simulation: the preview object used for the footprint is destroyed exactly as `IsPlacementValid` does today.
6. Mode off means every new code path returns before doing anything.

---

## 7. Risks

| Risk | Mitigation |
|---|---|
| A crossing half does not fit the strip rule as designed | Phase 0 item 1 before any rule code; D2 has the fallback wording |
| The host filter skips an event type (an action on the other colony goes through) | reflection check 8.2 makes an undeclared event a failing check |
| `SetDefault` patch changes loaded saves or shared-colony games | Phase 0 item 4; patch is inert when the mode is off; solo script reloads a save and compares settings |
| Migration overwrite breaks on a game update | `[ManualMethodOverwrite]` with dated original, as the repo already does for other overwrites |
| Roads of the two colonies still meet somehow (bridges, stairs, platforms over the strip) | the strip applies at every height; the solo script tries it; if it happens the log shows a district conflict (Phase 0 item 8) |
| Two colonies double the simulation load; the guest's frame rate is already the weak point | not solved here; release notes recommend a small map and the speed limit settings; the 30-minute line in 8.4 asks for the frame rate readout from the connection panel |
| The multi-start new-game screen is fragile | the mode is switched by a mod setting, not by more injected UI |
| Border display is a rendering change made without being able to see it | user screenshot before it is called done; fallback defined in D9 |
| The implementer cannot run the game | every in-game claim in docs and notes comes from the user's reports only; two fixed checkpoints (A and B) keep the user's time small |

---

## 8. Test plan

### 8.1 StabilityTests (headless)

1. A guest event arriving at a real `TimberServer` over the fake socket carries the server-assigned player number when the host reads it; a guest that writes its own `player` value is overwritten.
2. A stamped `GroupedEvent` hands its stamp to every child after flattening (test the flattening helper; extract it if needed so it can be linked).
3. Seat mapping: host seat 1 or 2, guests get the other; player numbers above 1 map like any guest.
4. Territory: nearest start wins; tie goes to the lower colony; height is ignored; 2, 3 and 4 starts; a start on the map edge.
5. Strip: tiles touching the other colony's land are strip tiles on both sides; diagonal borders give a staircase strip with no gaps a path could pass through (assert that no two non-strip tiles of different owners are 4-neighbours).
6. Mode state: fewer than 2 starts means mode off.
7. Rules, entity and district kinds: own allowed, other refused, missing entity falls through to allow (the event handles it).
8. Rules, placement: footprint fully inside own land allowed; one tile across refused; strip tile refused for a normal building and allowed for a crossing half.
9. Rules, lists: mixed list is filtered to own tiles or entities, order preserved; all-foreign list is refused.
10. Migration neighbour filter, if the choice logic can be expressed without game types.

Write the rule logic against small interfaces (tile owner lookup, entity position lookup) so these tests need no game assemblies.

### 8.2 RuntimeChecks (compiled mod + game assemblies)

- Reflection: every non-abstract `ReplayEvent` subclass in the mod assembly declares a scope. Fails with the list of offenders.
- Every event in the Global list is really meant to be global: the check prints the list so a reviewer sees it in the output.
- JSON round trip of an event keeps `player`.

### 8.3 Solo host script (user, about 20 minutes, checkpoint A and again at B)

Setup: mod settings "separate colonies" on and debug on; start a new game on a two-start map; host it with nobody joining.

1. Two starting buildings with beavers at each. Save, reload, host again: still in the mode (log line at load says so, with both start coordinates).
2. As colony 1: place a path and a building on own land: works. Place a building on colony 2's land: refused with a notice, log shows a `[Colony]` refusal.
3. Try to place a path on the border strip: refused. Try a platform or stairs over the strip at height: refused.
4. Drag a tree-cutting area and a planting area across the border: only the own-side tiles are marked.
5. Click pause, priority, workers and demolish on colony 2's starting building: each refused with a notice.
6. Flip the debug seat to colony 2: the same actions now work on colony 2 and are refused on colony 1.
7. Build a crossing: as colony 1 place a half on the strip facing the border; flip; as colony 2 place the matching half back to back. Both get built, each by its own colony's beavers, and link (no "invalid connection" status).
8. Open the distribution screen for each district: every good imports "Disabled". Nothing moves through the crossing. As colony 2 set logs to import; as colony 1 leave export at default: logs move from 1 to 2 only. As colony 1 try to change colony 2's import setting: refused.
9. Let it run 10 game days: no beaver changes colony (population per district only changes by birth and death). Try manual migration from district 1 to district 2: refused.
10. Turn the mode setting off, start a normal multi-start game: behaves as 1.1.10 (shared control, no refusals, vanilla distribution defaults).
11. Send `Player.log`.

### 8.4 Two-player script (user and friend, about 45 minutes, checkpoint B)

Both install the same zip; host sets seat "colony 1"; join over a Steam invite before unpausing.

1. Each player sees their colony named in the connection panel (if Phase 5 added it) and the border (if D9 shipped).
2. Lines 2 to 5 of the solo script, done by each player against the other's colony, at the same time.
3. Each player builds their own half of a crossing. It links.
4. Trade: guest opens import of one good, host lowers nothing: goods arrive at the guest. Host raises the export threshold for that good to the top: flow stops. Each tries to change the other's setting: refused.
5. Shared things still work for both: speed, pause, working hours, unlocking a building with shared science.
6. Play 30 minutes at the speed you normally use. No desync dialog. Note the guest's frame rate from the panel at the start and the end.
7. Save and rehost with the same host: seats, border, crossing and distribution settings are unchanged. Optional: the friend hosts the rehost save with seat "colony 2" and both still control their own colony.
8. Send both `Player.log` files. If a desync happens, use the repo's usual recipe: compare the mod lists in both logs first, then the RNG mismatch line.

---

## 9. Documentation

- `TWO-COLONIES.md`: what the mode is, how to switch it on, the border and strip rules, how to build a crossing together, how trade settings work, what is shared, known limits (D10). State of testing in plain words.
- `README.md`: a short section linking to it. `STABILITY-CHANGELOG.md`: one entry for the alpha.
- House rules for these docs: describe the current state only, no build-by-build history; keep "seen in game by the user" and "not tested" labels accurate; never claim something was seen that the user did not report.

---

## 10. Working rules for the implementing session

These are standing rules of this repo's owner. They override convenience.

- **Own worktree, own branch.** Other Claude sessions share the main checkout. Create a fresh git worktree on a new branch `two-colony-alpha` from `main`. Never checkout, stash, reset or discard files in the shared directory. A new worktree needs the git-ignored `BeaverBuddies/env.props` copied in from the main checkout.
- **Never write to the game folder or the user's Mods folder.** Always build with the mods path overridden: `dotnet build BeaverBuddies/BeaverBuddies.csproj -c "Release Steam" --no-restore -p:BeaverBuddiesModsPath="<scratchpad>\pkgout\BeaverBuddies\"` (and again with `-c Release` into another folder). Expect 0 warnings. Test builds go to the user as a zip.
- **Always `--no-restore`.** A plain build triggers a NuGet restore that fails (only the BepInEx feed is configured) and wrecks `obj/project.assets.json`. In a fresh worktree restore once with `dotnet restore <proj> -s https://api.nuget.org/v3/index.json -s https://nuget.bepinex.dev/v3/index.json` for BeaverBuddies, StabilityTests and RuntimeChecks.
- **Keep the worktree path short** (the packaging copy step fails past 260 characters); if it fails, use a short directory name and `-p:OutDir=bin/rs/`.
- **Checks:** `dotnet run --project StabilityTests --no-build` and `dotnet run --project RuntimeChecks --no-restore -- <built BeaverBuddies.dll> "C:/Program Files (x86)/Steam/steamapps/common/Timberborn/Timberborn_Data/Managed" "C:/Program Files (x86)/Steam/steamapps/workshop/content/1062090/3284904751" "C:/Program Files (x86)/Steam/steamapps/workshop/content/1062090/3283831040/version-1.1/Scripts"`, plus `python -m unittest discover -s RuntimeChecks -p "test_water_snapshots.py"`. At v1.1.10 the totals were 210, 69 and 3; report the new totals.
- **Zip layout:** `BeaverBuddies-Stability-Fork/{License.txt, README.md, STABILITY-CHANGELOG.md, PLAYER-ACTIVITY.md, STEAM-INVITES.md, CONNECTION-PANEL.md, TWO-COLONIES.md, thumbnail.png, version-1.1/...}` without `workshop_data.json`, made with Python `zipfile`, plus `<version>-SHA256SUMS.txt`.
- **File hygiene:** the checkout is CRLF; `sed -i` strips it, so bump versions with Python (`newline=""`) or the Edit tool. The Write and Edit tools expand `\u` escapes.
- **Visual changes are never shipped on guesses.** Anything drawn on screen is confirmed by the user's screenshot before it is described as working.
- **Commit, push, PR, merge, tag, release: each only when the user asks for it in words.** When asked for a pre-release, the repo's pattern is a tag on the branch tip, a PR, and `gh release create <tag> --prerelease --latest=false --verify-tag` with notes that say what has not been run in a game. The website under `docs/` follows the stable release and is not touched by a pre-release.
- **Report faithfully.** If a check fails, show the output. If a script line was not run, say so.

---

## 11. After the alpha (not part of this plan)

In rough order of value: per-player science and unlocks (hook in 2.2); hiding or greying the other colony's controls; an invalid-looking placement preview across the border; per-colony game over; map-author-painted territory in the map editor (the repo already has editor patches and a metadata service to build on); Steam-ID seats for more than two players; per-colony marks for tree cutting and planting.

---

## 12. Phase 0 findings and amendments (2026-09-21, implementing session)

Read from the decompiled Timberborn 1.1.2.4 assemblies and this repo. Each **unverified** item above is settled here.

1. **Crossing half geometry.** `DistrictCrossing.*.blueprint.json` (in `StreamingAssets/Modding/Blueprints.zip`): `BlockObjectSpec.Size` 3×1×2 (one tile deep), entrance at local (1,−1) in front, `DistrictObstacleSpec` at local (1,0). `CoordinatesBehind()` = `TransformCoordinates((0, Size.y, 0))`, the tile directly behind. `DistrictCrossingSpec` is internal (usable through the publicizer); the public component is `DistrictCrossing`.
2. **Placing a crossing: one click places both halves.** `AreaPicker.HalvesCoordinates` yields the second half at `start + orientation.Transform((size.x−1, size.y·2−1, 0))`, rotated 180°; `PreviewPlacer` treats the pair as one (both valid or neither), and `BlockObjectTool.Place` calls `BuildingPlacer.Place` once per half. **Amends D2:** "each player places their own half" is impossible in vanilla. Instead a half may stand on another colony's strip when every tile has the actor's own strip directly behind it **and** the actor's own crossing half really stands there (host checks the world, plus halves it allowed earlier in the same tick); each half must stand wholly on one side. Either player places the pair; each half joins, and is built by, the district on its own side.
3. **Footprint API.** `BlockObjectSpec.GetBlocks(Placement)` gives positioned blocks with no entity, so the host computes footprints from the spec and **no preview object is created** (supersedes "reuse the preview in `IsPlacementValid`" in D5). For previews, `BlockObject.PositionedBlocks.GetAllCoordinates()`.
4. **`SetDefault`.** Runs from `GoodDistributionSetting.CreateDefault` (new district centers, and goods missing from a save) and from the Distribution tab's Reset button; not for settings loaded from a save. Load order is singletons → entities → post-load, so the saved mode is known before any `CreateDefault`. The mod already records Reset through a prefix on `SetDefault` (`GoodDistributionSettingSetDefaultPatcher`); D7.2 is implemented by making that prefix's default `Disabled` in the mode and a postfix that applies `Disabled` only when the original ran (`__runOriginal`).
5. **Distribution screen.** `DistributionBatchControlTab`: per good an export-threshold slider (0 to 1, steps of 0.05) and a 3-state import toggle; header buttons Reset / Export all / none / Import all modes. All go through `SetExportThreshold` / `SetImportOption` / `SetDefault`, which the mod records.
6. **Notification:** `QuickNotificationService.SendWarningNotification(string)`. **Highlight:** `AreaTileDrawerFactory.Create(Color, GameObject)` → `AreaTileDrawer.UpdateArea(IEnumerable<Vector3Int>)` (chunked meshes, no per-frame calls); heights from `ITerrainService.GetAllHeightsInCell`. **Bonus:** `IBlockObjectValidator` (multi-bound) makes a placement preview red with a message; used for the D10 stretch goal, previews only and never while replaying.
7. **Host alone** runs `ReplayEvents` for its own actions (`ServerEventIO.UserEventBehavior` is `QueuePlay`).
8. **`DistrictConflictDetector`** only backs `DistrictPreviewsValidator`, which refuses a preview whose roads would merge two districts (`BuildingTools.DistrictsInConflict`).
9. **Event list:** 50 concrete `ReplayEvent` types; all declare a scope (checked by RuntimeChecks). District events name their District Center entity, so "District" scope is the entity rule on that entity.

Other amendments:

- **Stamping** is done at the JSON level in TimberNet (`TimberServer.StampReceivedEvent` → `TimberNetBase.StampPlayer`, which stamps the group and every child, in the `$type`/`$values` shape the mod really writes) **and** by the host when it flattens a group in `ReplayEvents`.
- **Only the first two starts become colonies** (seats exist for two).
- **Repository and name.** Built in the new BeaverBuddies-MultiColony repository; mod shown as "BeaverBuddies - MultiColony (alpha)" with the unchanged mod ID `beaverbuddies`; zip `BeaverBuddies-MultiColony-1.2.0-two-colony-alpha1.zip` with top folder `BeaverBuddies-MultiColony`.
- **Keys:** border toggle **K**, debug seat flip **Ctrl+Shift+K** (host + debug only), both rebindable.
