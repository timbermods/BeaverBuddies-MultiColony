# Reviewer E: the colony lifecycle and the other simulation events (1.4.0-rc1 review)

Base `0a16798` (beta24, P-1, B24-a, B24-b, the check files). Branch `worktree-agent-a24dc61c893e231f0`. Read against
the decompiled Timberborn 1.1.2.4 in the session's scratchpad (`game/<Assembly>.decompiled.cs`, cited as
`<Assembly>:<line>`) and `Blueprints.zip`. Mod lines are this branch's. Nothing here was played: every finding is
Confirmed by reading the mod and the game, and by a check that runs the game's own code where it can run outside
the game.

**Area:** `Events/ToolEvents.cs`, `Events/BatchEvents.cs`; in `Colonies/`: ColonyStamps, DistrictOwner, ColonyGameWorld
(shared with A), ColonyHandover (not its faction parts), ColonySlotService, ColonySlotTable, ColonyStewards,
ColonyStewardRules, ColonyAbsence, ColonyJournal, JournalFilter, ColonyMigrationControls, ColonySimulationPatches,
ColonyNavigation, ColonyRoadRule, ColonyPlacementValidator (correctness), ColonyTerritory, ColonyModeService,
ColonyModeState, ColonySession, HostStartGate, HostStartRules, ColonyFoundingService (not its faction parts).

**Checks:** StabilityTests **423/423** (baseline 420 + 3). RuntimeChecks **374/374** on Release Steam and on Release
(baseline 362 + 12). Both builds 0 warnings. Against beta24's unfixed Release Steam DLL: 367/374, the 7 failures being
E-1, E-2, E-5, E-6, E-7, E-8 and P-1 (fixed before this branch); every premise check passes there.

**Files outside my list** (each a few separate lines, said again at each finding):
- `Colonies/ColonyRulesService.cs` (A's): E-1's host refusal, a block of its own right after the building refusal.
- `Colonies/ColonyMarks.cs` (D's): E-7's `AdoptUnowned`, a new method after `Transfer`.
- `Colonies/ColonyConfigurator.cs` (shared): one binding for E-8's `ColonyCitizens`.
- `RuntimeChecks/PlacementRandomChecks.cs`: accepts E-6's validity helper where it looked for `BlockObject.IsValid`.

**Wire and save:** no wire change. One save addition: the singleton `BeaverBuddies.ColonyCitizens` (E-8), written in
separate-colonies games only and only while a beaver has no district. Older saves load (absent means empty); an
older build ignores the key.

---

## 1. Findings

### E-1. A planting mark of a plant the host's game lacks stopped the session for everyone
- **Status:** Confirmed. **Kind:** crash (session-stopper). **Hits:** everyone, with mismatched mods (the mod lists
  only warn when they differ, `Connect/ModCompatibility.cs:30-35`).
- **Evidence:** a guest running a crop mod the host lacks marks that crop. `PlantingAreaMarkedEvent` is Global scope, so
  nothing refuses it. The host's replay calls `PlantingSelectionService.MarkArea` (`Timberborn.PlantingUI:1249-1258`),
  whose first free tile asks `PlantingAreaValidator.CanPlant` (`Timberborn.Planting:1160-1177`) →
  `SpawnValidationService.IsUnobstructed(coordinates, name)` (`Timberborn.NaturalResources:433-437`) →
  `TemplateNameMapper.GetTemplate`, which throws `TemplateMappingException` for an unknown name
  (`Timberborn.TemplateSystem:262-269`). A throw in a replay is `AbortReplay` for every player
  (`ReplayService.cs:439-449`). beta9 already refused a *building* the host lacks (`ColonyRulesService.cs:141-150`).
- **Fix:** `PlantingAreaMarkedEvent.NamesUnknownPlant` (`ToolEvents.cs:343-349`) asks the same name mapper. The host
  refuses such a mark before playing it (`ColonyRulesService.cs:152-159`, A's file, in every game, like the building).
  A guest that meets one (the host runs the crop mod) throws `MissingContentException` before anything is marked
  (`ToolEvents.cs:307-313`), so only that guest leaves quietly, as for a building. Wire and save: none.
- **Check:** `RcColonyRuntimeChecks`: "E-1: the game's planting check throws for a plant it does not know" (runs the
  game's `CanPlant` over its real mapper); "E-1: the host refuses a mark of a plant its game lacks, and a guest meeting
  one leaves quietly" (runs `AllowOnHost` and `Replay`). Fails on beta24.
- **Test line (optional, needs a crop mod on the guest only):** the guest marks the modded crop. It says the host
  refused it; nobody's game stops.

### E-2. A distribution change for a good the host's game lacks stopped the session
- **Status:** Confirmed. **Kind:** crash (session-stopper). **Hits:** everyone, mismatched mods.
- **Evidence:** every district holds a setting for every good *its* game loaded (`AddMissingGoodSettings`,
  `Timberborn.DistributionSystem:1477-1487`), so a guest with a goods mod sees the extra good in the Distribution
  tab. The replay called `DistrictDistributionSetting.GetGoodDistributionSetting`, which throws `ArgumentException` for
  an unknown good (`Timberborn.DistributionSystem:1430-1440`). The mod's null check after it never fired.
- **Fix:** `GoodDistributionSettingChangedEvent.SettingFor` (`BatchEvents.cs:303-317`) finds the setting without
  throwing. Every computer without the good skips the change and logs it. Nothing of the good can exist there,
  because the host refuses its buildings. Wire and save: none.
- **Check:** `RcColonyRuntimeChecks` "E-2: the game's district lookup throws…" (premise, the game's own method) and
  "E-2: a distribution change for a good this game lacks is skipped, not thrown" (the helper on the game's real
  settings, and the replay's IL no longer calls the throwing lookup). Fails on beta24.
- **Test line:** none (mismatched mods only).

### E-3. A steward's colony was handed over, unwarned, on the first day nobody kept it
- **Status:** Confirmed. **Kind:** gameplay. **Hits:** separate, a player away for days with a steward.
- **Evidence:** the days away still count while a steward in the game looks after the colony (TWO-COLONIES,
  *Looking after a colony*). The warning fired only when the count *equalled* the limit
  (`ColonyAbsence.IsDueTomorrow`, beta24's `WarnBeforeHandover`), and it was skipped while a steward kept the colony. The
  host's check handed over any colony `away >= limit` that nobody kept that day. So a colony looked after for 20 days
  (limit 7) was handed over at the first daily check its steward missed, with no warning ever: TWO-COLONIES promises
  that "the day the count reaches the limit every player in the game is warned". The same after a load: a colony
  already due was handed over at the first counted day of the new session, without a warning in it.
- **Fix:** a hand-over for absence now always comes the day after its warning, in the same session.
  - `ColonyAbsence.IsAnnounced` and `IsHandedOver` hold the pure rule (`ColonyAbsence.cs:19-32`).
  - Each day's presence, played on every computer, announces each colony that is due and kept by nobody today, and
    warns once, when it is newly announced (`ColonyHandover.cs:317-342`).
  - The host hands over only a colony the last presence announced (`ColonyHandover.cs:258-264`).
  - A normal absence behaves as before (warned at 7, handed over at the next check).
  - `announced` is session state, like the day's present players. After a load the first presence announces
    again.
  - Wire and save: none.
- **Check:** `RcColonyChecks` "E-3: a hand-over for absence always comes the day after its warning, also when a steward
  kept the colony past the limit". It simulates the day loop with the rule for plain absence, a steward who stops, a
  steward or player back the next day, and a load, and scans that `HostDaily`, `Seen` and `Transfer` use it. On
  beta24 the old rule hands the steward case over on day 21 unwarned.
- **Test line (Script L):** host limit 1 day. The guest asks the host to look after their colony and leaves. Play 2
  days (the colony is kept, "missed 2 of 1 days"). The host ends the stewardship. The next day every player is warned
  "…unless they are in the game tomorrow…", and the colony is handed over only the day after.

### E-4. A deletion sent as an action left the tool's picked terrain behind
- **Status:** Confirmed. **Kind:** display. **Hits:** everyone.
- **Evidence:** the deletion tool adds the terrain it picked above the deleted objects to `_temporaryTerrainCoords`
  (`Timberborn.BlockObjectTools:516-519`). Only the game's own `DeleteBlockObjects` clears it (:578-593). The
  recording prefix cleared only the block objects, so over a session the list grew, and each later deletion's prompt
  raised the visible level to the highest terrain ever picked (`SetVisibleLayerToShowAllObjects`, :540-557).
- **Fix:** the prefix clears it too (`ToolEvents.cs:276-282`). The terrain itself (G9) is A's X4. If A's G9 fix
  records these coordinates in the event, it reads them before this clear. Wire and save: none.
- **Check:** `RcColonyChecks` "E-4: a deletion sent as an action leaves no picked terrain behind in the tool"
  (source scan).
- **Test line:** none needed.

### E-5. Two players of a shared game unlocking the same building at once paid twice
- **Status:** Confirmed. **Kind:** gameplay (lost science). **Hits:** shared games, and separate colonies without
  separate science.
- **Evidence:** the game's `BuildingUnlockingService.Unlock` checks only `Unlockable` (enough science) and never
  `Unlocked` (`Timberborn.ScienceSystem:261-269`). The replay skipped an already-unlocked building only with separate
  science. The comment's reason (the game's set also holds profile-remembered buildings) applies only to
  `UnlockableOnceSpec` buildings (:285-295): per `Blueprints.zip`, just the HTTP Lever and HTTP Adapter.
- **Fix:** in the shared set, an already-unlocked building is skipped unless it is `UnlockableOnceSpec`
  (`ToolEvents.cs:613-624`). The shared set is the same on every computer for every other building. Wire and save:
  none.
- **Check:** `RcColonyRuntimeChecks` "E-5: the game's Unlock pays again for a building already unlocked; a shared
  game's replay skips it" (IL of the game's `Unlock` and of the replay). Fails on beta24.
- **Test line (Script L, shared save):** both players click Unlock on the same building within a second. The science
  counter drops once.

### E-6. The host's check of a played placement read the host's own tool previews
- **Status:** Confirmed. **Kind:** gameplay (a building silently not placed, on every computer). **Hits:** everyone,
  mostly separate colonies.
- **Evidence:**
  - Only the host checks a replayed placement, and the guests take its answer (`ToolEvents.cs:102-112`).
  - The check ran the game's `BlockObject.IsValid` (`Timberborn.BlockSystem:1205-1212`), which asks every validator
    (:1939-1950). One of them, `DistrictPreviewsValidator` (`Timberborn.GameDistrictsUI:1365-1398`), asks
    `IsPreviewDistrictInConflict(null)`: "do the previews shown now join two districts' roads", on the *preview* road
    graph (`Timberborn.Navigation:2918-2933`).
  - The replayed copy is never in that graph: previews join it only through `Preview.AddToPreviewServices`, which the
    tools call (`Timberborn.BlockSystem:3368-3374`) and the check does not. So the answer was about the host's own
    tool.
  - While the host hovered a preview that joined two districts (a path beside another colony's road, which the colony
    rule shows red), every placement played in those frames, by anyone, was refused, and skipped on every computer
    ("Invalid placement" in the host's log only).
- **Fix:** `BuildingPlacedEvent.IsValidWithoutHostPreviews` (`ToolEvents.cs:177-198`) repeats `BlockObject.IsValid`
  (quoted, with its date) but leaves out that one validator. It never judged the placed building itself: the placing
  player's own tool does (its previews are in *their* graph), and between colonies the colony rule does. Wire and
  save: none.
- **Check:** `RcColonyRuntimeChecks` "E-6: the game's district validator asks the preview road graph…" (pins
  `BlockObject.IsValid`'s body and the validator's call) and "E-6: the host's check of a played placement asks every
  validator but the one that reads its own previews" (runs the helper with the game's validation service: a district
  validator that would throw if asked is skipped; every other one is asked, and a refusal still refuses). Fails on
  beta24. `PlacementRandomChecks` now accepts the helper (the random-state guard still covers it).
- **Test line (Script L):** the host holds a path preview beside the guest's road (red), without clicking. Meanwhile the
  guest places a building elsewhere. It appears on both computers.

### E-7. A shared game split by a founding left its marks nobody's
- **Status:** Confirmed. **Kind:** gameplay (one colony working another's fields and forests). **Hits:** separate,
  after *Allow founding colonies in a shared game*.
- **Evidence:**
  - A shared game records no mark owners (`ColonyMarks.ActingSlot` is set only with separate colonies,
    `ToolEvents.cs:317, 453`).
  - "Marks made before this existed have no colony: they count for every colony" (`ColonyMarks.cs:14-22`), and a
    resource with no mark owner is anyone's (`ColonySeparationPatches.cs:61-70`).
  - The split stamped every *building* the first colony's (`ColonyStamps.Begin`) but left every mark nobody's. So the
    new colony's planters, harvesters and lumberjacks worked the first colony's fields and marked forests, and its
    player could unmark them. In a late save that is most of the map's farming.
- **Fix:** at the split, `ColonyModeService.Enable` (`ColonyModeService.cs:155-157`) calls the new
  `ColonyMarks.AdoptUnowned(0)` (`ColonyMarks.cs:125-148`, D's file, a separate method). Every standing planting and
  cutting mark without a colony becomes colony 0's, counted as one digest change. It reads the game's planting map (a
  fixed array scan) and cutting area, the same on every computer. A new game adopts nothing. Wire: none. Save: those
  marks are now saved with their owner (the existing `BeaverBuddies.ColonyMarks` list).
- **Check:** `RcColonyRuntimeChecks` "E-7: splitting a shared game makes its standing marks the first colony's, and a new
  game's changes none". It runs `ColonyModeService.Enable` and `ColonyMarks` over the game's own `PlantingMap` and
  cutting area, and checks that a mark with an owner keeps it. Fails on beta24, behaviourally.
- **Test line (Script L, split):** host `R-late` shared with *Allow founding…* on. The guest founds beside the host's
  farms and builds a farmhouse and a lumberjack flag reaching them. The guest's beavers never plant, harvest or cut
  on the host's marks, and the guest can't unmark them.

### E-8. A beaver without a district joined the nearest district center, whoever's
- **Status:** Confirmed. **Kind:** gameplay (beavers change colony without a Trading Post; in a mixed game, across
  factions). **Hits:** separate (and mixed).
- **Evidence:**
  - A beaver or bot loses its district when its district center is deleted, or when it is cut off by a blast or a
    flood (`Citizen.UnassignDistrictIfCutOff`, `Timberborn.GameDistricts:646-661`).
  - Leaving the district drops its home and job at once (`Timberborn.DwellingSystem:1012-1022`,
    `Timberborn.WorkSystem:968-978`), so nothing else says whose it was.
  - Every tick, `DistrictCitizenAssigner` gives each one without a district to the nearest finished district center it
    can walk to, in straight-line distance, whoever's (`Timberborn.GameDistricts:1550-1612, 1416-1434`). Walking
    includes terrain, so another colony's district center is usually in reach.
  - So a late-game player who deletes a district center near another colony's district gave that district's beavers
    to the neighbour. Nothing in the mod patched this (`ColonySimulationPatches.cs` kept only migration apart).
- **Fix** (`ColonySimulationPatches.cs:92-232`):
  - `ColonyCitizens` records the colony a beaver or bot last left, in `Citizen.UnassignDistrict`: every way out of a
    district goes through it, which ColonyRuntimeChecks already pins. It is read from the district center's own
    `DistrictOwner`, which still answers as the center is deleted. `OwnerOfDistrict` answers null by then.
  - A copy of `AssignToClosestDistrict` (`[ManualMethodOverwrite]`, `Priority.Last`, body quoted) runs the same loop
    in the same order, over its own colony's district centers only (`ColonyModeState.MayJoin`).
  - One that never had a district (a new game's start, a founding's beavers) joins the nearest, as before.
  - With none of its colony's in reach it waits, as in the game. When its player founds again, or the colony is
    handed over, it joins that colony (`ColonyLifecycle.Transfer` remaps, `ColonyHandover.cs:388-389`).
  - Saved only for those still without a district. New save singleton `BeaverBuddies.ColonyCitizens`, separate
    colonies only. Wire: none.
  - Per tick it costs one static read, plus a loop only for citizens without a district (normally none).
- **Check:** `RcColonyChecks` "E-8: a beaver with no district joins only its own colony's districts; one that never had
  one, any" (the rule and the copy's order). `RcColonyRuntimeChecks` "E-8: the game gives a beaver with no district to
  the nearest district center it can walk to, whoever's" pins the game's assigner and its home and job drops.
  "E-8: a beaver with no district joins only its own colony's district centers, and a hand-over takes it along" covers
  the record, the transfer, the gate order and both patch targets. Fails on beta24.
- **Test line (Script L):** the guest builds a second district next to the host's district, lets beavers move in, then
  deletes its district center. Its beavers go to the guest's other district and none to the host's. With no other
  district in reach they wait and follow the guest's next district center.

### Plausible or refuted (no fix here)

- **E-9 (pointer for A, Confirmed by reading, crash with mismatched mods):** the same "names content the host lacks"
  pattern exists in A's `EntityUIEvents.cs:108-123`.
  - `ManufactoryRecipeSelectedEvent` calls `RecipeSpecService.GetRecipe`, a dictionary indexer that throws
    `KeyNotFoundException` for an unknown recipe (`Timberborn.Workshops:2154-2157`). Its null check never fires.
  - A guest whose mod adds a recipe to a vanilla workshop stops the session by choosing it.
  - Fix, small: look the recipe up without throwing (`_recipeSpecs` is publicized), or refuse on the host like E-1.
  - Not changed here, to stay out of A's file.
- **E-10 (Plausible, gameplay, Left):** the road rule treats every cell of a road-carrying building as road
  (`ColonyGameWorld.PlacementConflict`, `building.HasSpec<PathSpec>()`).
  - For a district center (3×3), a Wonder (7×7), and a tubeway or zipline station (3×2) that is over-strict: their nav
    meshes have walls and only an entrance edge (`Blueprints.zip`: `EarthRecultivator` has one edge at (3,0)→(3,-1)).
  - So such a building can't stand beside another colony's road on any side. That is safe, and documented in general.
- **E-11 (Plausible, gameplay, Left, narrow):** a vertical tubeway joins the tubeway above and below it (edges to z±1,
  `VerticalTubeway.IronTeeth`), and the road rule checks neighbours at the same height only.
  - A finished tubeway of the other colony is still caught by the game's own preview check on the placing player's
    tool.
  - An unfinished one joins once both are done, and the joined-roads warning then says so (TWO-COLONIES, *Road
    networks*).
- **E-12 (Refuted as a reachable bug):** `BuildingsDeconstructedEvent` deletes any entity id it is sent, which would
  delete a character without killing it. Only a modified client can send a non-building id: the tool and the panel
  button list buildings only. That is out of scope (plan §8).
- **E-13 (Left, rare):** a child that grows up while it has no district becomes an adult (a new entity,
  `Citizen.InfluenceByChildhood`) with no record, and joins the nearest district, as before E-8.

---

## 2. Coverage matrix rows

| Feature | Desync | Crash | Cross-colony | Mixed | Cost | Save/rehost |
|---|---|---|---|---|---|---|
| Placing a building, a dragged line of many, copies of a building's settings | Sound: host checks once and writes `placed`; guests take it (`ToolEvents.cs:102-112`); check copy puts the random state back (PlacementRandomChecks) | Fixed: E-6 (the host's own previews); Sound: missing building → MissingContent, host refuses (`ReplayEvent.cs:122-135`, `ColonyRulesService.cs:143-150`); deleted copy source → plain placement | Sound: road rule + unlock per slot (`ColonyRules.cs:260-267`); copied settings of another colony's building dropped (`ColonyRulesService.cs:193-198`) | Sound: B's other-faction refusal (`ColonyRulesService.cs:267-279`) | Playtest: Script P, host drags a 100-tile path; Ctrl+Shift+J "Placement checks in replays" (D's spot) | Sound: events are not saved; the placed buildings are, stamped (`ColonyStamps.cs:36-47`) |
| Deleting buildings (tool, panel button) | Sound: same ids, same order everywhere | Sound: deleted earlier in the tick → skipped (E-H1 check) | Sound: list trimmed to the actor's (`ColonyRules.cs:281-289`); a post's partner may delete it | Sound: nothing faction-specific | Sound: O(ids) | Fixed: E-4 (tool state); Left: G9 terrain (A's X4) |
| Planting marks over large areas | Sound: tiles levelled by the marker, carried (PlantingLevelChecks) | Fixed: E-1 (plant the host lacks) | Sound: each tile the actor's or free (`ColonyMarks`); Fixed: E-7 (split) | Sound: plantables per faction come from each planter's own list | Playtest: Script P, a 100×100 mark (D: per-tile digest notes) | Sound: marks saved per owner, sorted (`ColonyMarks.cs:193-196`) |
| Cutting and demolition marks over large areas | Sound: coordinates and ids carried | Sound: gone entities skipped (ReplayEventChecks, E-H1); `Demolishable` exists on every picked entity | Sound: own or free marks only; list trimmed | Sound: nothing faction-specific | Playtest: the host judges one `OwnerOf` per id (~µs each) | Sound: as above |
| Unlocks (paid, dev instant) and dev science | Sound: per-slot sets in separate science; shared set equal on all but `UnlockableOnceSpec` | Sound: unknown building refused, lack of science skipped (`ToolEvents.cs:631-640`) | Sound: actor's slot | Sound: B's toolbar per faction | Sound: O(tool buttons) per unlock, display | Fixed: E-5 (paid twice) |
| Manual migration | Sound | Sound: missing district → skip (E-H1) | Sound: both districts the actor's (`ColonyRules.cs:249-258`); buttons greyed | Sound: same named distributors on both factions' centers (decorator, `Blueprints.zip`) | Sound | Sound |
| District minimum and migration toggles; automatic migration | Sound: same loop, same order (`ColonySimulationPatches.cs:34-85`) | Sound: missing district → skip | Sound: only within a colony | Sound: as above | Sound: O(connected districts) | Sound |
| Good distribution with many districts | Sound | Fixed: E-2 | Sound: district scope | Sound: every district holds every loaded good (both factions' in a mixed game) | Sound: one click is one event per good (~60) | Sound |
| Building owners (stamps, DistrictOwner lookups) | Sound: stamps set in ticks and replays, in entity order (list-based registries, `Timberborn.EntitySystem:582-603`) | Sound: deleted stamps dropped (`ColonyStamps.cs:155-177`); Unity-null checks in `OwnerOf` | Sound: owner by district, else stamp | Sound: paths repainted (B, guarded) | Sound: waiting list normally empty | Sound: saved in separate games only |
| Hand-over (dead, absent, by host) of a 10,000-entity colony | Sound: host decides absence; death decided alike everywhere; notes per building | Sound: every step guarded or null-safe (E-H1 check without services) | Fixed: E-8 (waiting beavers follow) | Left: B (faction of the receiver) | Playtest: Script P, one-tick walk of every entity plus a digest note per building and a repaint per path in mixed games (estimated 10–100 ms once) | Sound: away days and dead days saved |
| Absence count, warning, player away for days | Sound: count played from the host's presence event | Sound | Fixed: E-3 | Sound | Sound: O(4 × districts) a day | Sound: counts saved; the announcement is session state (announced again after a load) |
| Stewards acting for an away colony | Sound: grant saved, acting played as an action | Sound: services missing → skip (E-H1) | Sound: host judges (`ColonyStewardRules`) | Sound | Sound | Sound: grants saved; acting forgotten per session (documented) |
| Seats and slots (four colonies, helpers) | Sound: host's tables in the hello; SortedDictionaries | Sound: ids checked (`ColonySlotTable.CheckHello`) | Sound | Sound | Sound | Sound: table saved in separate games |
| Founding late, beside a big colony | Sound: judged again at replay from the tick map only (`ColonyFoundingService.cs:309-318, 355-366`) | Sound: blasted or taken spot → skipped and told | Sound: entrance rule; the center's walls leave only its entrance (`Blueprints.zip`) | Left: B | Sound | Sound |
| Splitting a late shared save | Sound: one digest change for the buildings, one for the marks | Sound | Fixed: E-7 | Sound: a shared game is single-faction | Playtest: Script P, split `R-late` (entity walk plus a planting-map scan, estimated under 20 ms) | Sound: stamps and marks saved from the split on |
| Beavers without a district (deleted center, cut off) | Sound: record written in the tick on every computer | Sound: recorder guarded; assigner gated first | Fixed: E-8 | Fixed: E-8 (keeps factions apart) | Sound: only while some have no district | Fixed: E-8 (saved while waiting) |
| Journal per colony | Sound: display only | Sound: every hook in a try (`ColonyJournal.cs:98-147`) | Sound | Sound | Sound: bounded (`ForgetAbove`) | Sound: owners of journal subjects saved |
| Road rule and placement previews | Sound: host judges; previews never while replaying (`ColonyPlacementValidator.cs:26`) | Sound: previews in a try | Left: E-10 (over-strict), E-11 (vertical tubeway) | Sound | Playtest: D's S9 | Sound |
| Mode, session, host start gate, Home key | Sound: static mode flag reset per scene | Sound | Sound | Sound | Sound | Sound |

---

## 3. Found sound

- **Sweep 7 (H1), every entry point in this area that runs in a tick, a replay or a load:**
  - **Replays.** Each of the 18 events (`BuildingPlaced`, `BuildingsDeconstructed`, `PlantingAreaMarked`,
    `ClearResourcesMarked`, `TreeCuttingArea`, `BuildingUnlocked`, `ScienceAdded`, `WorkingHoursChanged`, `Duplication`,
    `ManualMigration`, `SetDistrictMinimumPopulation`, `SetDistrictMigrationToggled`, `GoodDistributionSettingChanged`,
    `ColonyPresence`, `ColonyHandover`, `PlayerHello`, the three steward events, `FoundColony`) was checked for:
    - a named entity gone;
    - an unknown name (E-1, E-2);
    - a missing singleton;
    - the other faction;
    - an unfinished or map object.

    The two E-H1 RuntimeChecks run the id-naming ones and the lifecycle ones in those states.
  - **Ticks.**
    - `ColonyStamps.Tick` (deleted stamps; `CalculateAccess` relies on the game's own invariant).
    - `ColonyLifecycle.Tick`: `HandOverDeadColonies`, whose Transfer calls are guarded or null-safe; `HostDaily`
      records through `DoPrefix`.
    - Both migration-neighbour patches.
    - The journal's two prefixes (in a try).
    - E-8's two patches.
  - **Event bus.** Stamps on entity init and delete; the journal's notification hook.
  - **Loads.** Every `Load` in this area tolerates missing or short data (`ColonyModeService`'s starting keys were all
    added in one commit). Also `DistrictOwner.InitializeEntity` with legacy territory, and
    `ColonyStamp.InitializeEntity`'s repaint (B's, guarded).
  - **The host's judging inside the replay loop.** `PlacementConflict` throws for an unknown template, but runs after
    the `HasBuilding` refusal and inside `AllowOnHost`'s try. `JudgePairs` is in a try. `HostAllows` and
    `Founding.Judge` are fine.
- **Hash-ordered iteration.**
  - The entity and component registries and `DistrictCenterRegistry` are lists.
  - The steward and slot maps are `SortedDictionary`.
  - `ColonyLifecycle.requested` and `presentPlayerIds` are only asked, never iterated. `Seen` sums over its set,
    which gives the same result in any order.
  - `ColonyJournal.owners` is iterated only to remove entries.
  - `ColonyMarks` and `ColonyCitizens` are saved sorted and otherwise read by key.
- **Local-player answers in the simulation.** Every `LocalSlot`, `LocalSeat`, `DisplaySlot` and `LocalPlayer` read in
  this area is display only:
  - toolbar refreshes and notices;
  - the founding camera;
  - `LocalFactionPick.Clear`;
  - the migration controls.

  `UnlockedPlantableGroupsRegistry`, which the unlock replay updates for the local colony only, is planting-UI state
  (`Timberborn.PlantingUI:1548-1586`).
- **Validators.** The host's replayed-placement check reads no other UI state:
  - `ColonyPlacementValidator` returns early while replaying;
  - `StartingBuildingPlacementValidator` applies only before the game is initialised;
  - the district center's check is blocks only.
- **Automatic migration.** It stays within a colony. Both factions' district centers carry the same distributors.
- **`DistrictOwner.OwnerOf` at load.** A shared save split later keeps its old district centers at slot -1, which reads
  as 0 everywhere, before and after a reload.
- **The founding at replay.** It reads only the tick map and blocks, so a blasted or taken spot is skipped on every
  computer. The district center's nav mesh joins only at its entrance, so the entrance-only founding rule is exact.
- **Hand-over at scale.** `Transfer` walks the entity list once and adds one digest note per building (a 16,384 ring,
  so a 10,000-building hand-over fits). Exchanges between the two colonies end with null-safe partners; stewards are
  revoked and actors sent home.
- **Connection numbers** are never reused within a server (`Interlocked.Increment`, `TimberServer.cs:262, 565`), so no
  new guest inherits another's "acting as".
- **Four colonies and helpers.** `MaxSlots` is 4, and every per-slot array and bit sum fits.

## 4. Left, and why

- **E-9:** A's file, reported to A and the main session with the fix.
- **E-10:** over-strict, safe, documented in general. Relaxing it needs the nav-edge data per building, a larger change.
- **E-11:** narrow. Caught by the game's own check for finished tubeways, and by the joined-roads warning otherwise.
- **E-12:** crafted events only (§8).
- **E-13:** rare, and no worse than before.
- **The shared-game split.** Players other than the host and the founder lose the first colony: they have no colony
  and must found one, or be made a steward. This is by design; doc text below.
- **Costs,** estimated by reading only; Script P lines below. A one-tick hand-over or split walk is a hitch, not a
  per-tick cost.

## 5. Doc text and test steps

**TWO-COLONIES, *When a colony is handed over*:** replace "the day the count reaches the limit every player…" with:
> A hand-over for absence is always announced the day before, to every player in the game: the day the count
> reaches the limit, or, for a colony a steward looked after past it, the first day nobody keeps it (and, after a
> load, the first day counted). It happens the next day unless its player or its steward is back.

**TWO-COLONIES, step 3 of founding (shared saves):**
> …every building already there, and every planting and cutting mark, becomes the host's colony's, on every
> computer at that tick. Other players who played the shared colony have no colony now: they found their own
> (Ctrl+K), or the host asks them to look after the first colony (Ctrl+T).

**TWO-COLONIES, *Keeping colonies apart* (new line):**
> A beaver or bot without a district (its district center deleted, or cut off by a blast or a flood) joins the
> nearest district center of its own colony it can walk to, never another colony's; with none in reach it waits, as
> in the game, until its player founds again or the colony is handed over.

**TWO-COLONIES, *Known limits* (new lines):**
> - An action naming a building, crop or good that the host's game doesn't have (a guest's content mod) is refused by
>   the host; a guest missing one the host used leaves the game quietly. (A recipe added by a guest's mod still stops
>   the session: E-9.)
> - A district center, a Wonder, and a tubeway or zipline station count as road on every tile: they can't stand
>   beside another colony's road on any side, even where they have no door. A vertical tubeway is judged on its own
>   level: one joining another colony's unfinished tubeway above or below is caught by the joined-roads warning.

**Script L** (late game, split into two colonies):
1. *(E-7)* Host `R-late` shared, with *Allow founding colonies in a shared game* on. The guest founds beside the host's
   farms and builds a farmhouse and a lumberjack flag reaching them. **Should:** after a day, none of the host's
   marked crops or trees is taken by the guest's beavers, and the guest's unmark tool leaves the host's marks.
2. *(E-8)* The guest builds a second district center next to the host's district, fills it with beavers, then
   deletes it. **Should:** its beavers join the guest's first district, none the host's. The top bars' beaver counts
   show it.
3. *(E-6)* The host holds a path preview beside the guest's road (red), without clicking. The guest places three
   buildings elsewhere. **Should:** all three appear on both computers.
4. *(E-3)* Host setting "Hand over a colony after its player is away" = 1. The guest asks the host to look after the
   guest's colony, then leaves. Play 3 days. The host ends the stewardship (Ctrl+T). **Should:** the next day brings
   the warning "…unless they are in the game tomorrow…", and the hand-over comes the day after. Send the host's
   `Player.log` (`[Colony]` lines).
5. *(E-5, shared save)* Both players click Unlock on the same locked building within a second. **Should:** science
   drops by its cost once.

**Script P:**
- The host hands a large colony over by hand (Ctrl+T, **Hand to …**) on a split `R-late`. Record the frame.
- Split `R-late` by a founding. Record the founding's frame.
- Drag a 100-tile path as a guest. Record the host's "Placement checks in replays" spot in Ctrl+Shift+J.
