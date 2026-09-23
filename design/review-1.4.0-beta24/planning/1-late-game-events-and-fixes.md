# Planning inventory 1: late-game events, fixes and colony rules (beta23; beta24 changed none of the files cited here)

Made while planning [REVIEW-PLAN-1.4.0-beta24.md](../../REVIEW-PLAN-1.4.0-beta24.md), by a read-only sweep of the mod
at `c02637b`. Line numbers are beta23's. **Not verified line by line**: re-check before citing a line in a finding.
Where the plan corrects this sweep, the plan wins (for example, P-1: `ResetTransmitterEvent`'s two buttons are swapped,
confirmed against the game's `SequentialTransmitterResetFragment`).

**What the mod has nothing for:**
- recording or blocking the game's HTTP API (but see P-2: its lever commands reach the recorded `Lever.SwitchState`);
- the reach of dynamite blasts;
- tunnels, sluices, batteries, tubeways, beehives.

## 1. Every ReplayEvent subclass

There are 70 class declarations: 68 concrete and 2 abstract generic bases (`BuildingDropdownEvent<T>` at
`EntityUIEvents.cs:47`, `PriorityChangedEvent<T>` at :386).

- The default scope is `null` (`ReplayEvent.cs:69`). Every mod event overrides it
  (`RuntimeChecks/ColonyRuntimeChecks.cs:20` enforces that).
- An event from another mod is judged through its `entityID` field (`ColonyRules.cs:212-217`,
  `ColonyRulesService.cs:372-383`).
- Every recorder goes through `ReplayEvent.DoPrefix`/`DoEntityPrefix` (`ReplayEvent.cs:172-240`), which lets the call
  through when replaying or inside `RunAsSimulation` (:177-179).

### Automation (`Events/AutomationEvents.cs`)

| Class | Line | Recorded from | Scope |
|---|---|---|---|
| `AutomationEvent` | 21 | `UniversalPrefix` (228-236) on the list at 111-199; replays by `MethodInfo.Invoke` (39-61) | `Entities(entityID + every string argument)` (26-27); component arguments become entity ids (253-259) |
| `SetAutomatableInputEvent` | 322 | `AutomatableFragment.SetInput` (345-361) | `Entities(automatableID, inputID)` (326) |
| `SetTimerIntervalEvent` | 369 | `TimerIntervalElement.SetTimeInterval` (432-454); `TimerFragment.ShowFragment`/`ClearFragment` remember the timer (413-429) | `Entities(entityID)` (371) |
| `ResetTransmitterEvent` | 456 | `SequentialTransmitterResetFragment.OnReset` → `resetAll=false` (485-500); `OnResetAll` → `resetAll=true` (502-517). **Replay swaps them (P-1)** | `Entities(entityID)` (458) |
| `WeatherStationSetActivateEarlyEvent` | 519 | `WeatherStationFragment.OnEarlyActivationToggleChanged` (543-568) | `Entities(entityID)` (521) |

- Relay panel rewrites (not events): `RelayFragmentRemoveRowPatch` (570-593, `[ManualMethodOverwrite]`) and
  `RelayFragmentUpdateFragmentPatch` (595-613).
- The lever's spring-return-to-off runs as a simulation call (`Fixes/TickTimingFixes.cs:56-67`).

### Water buildings

| Class | Line | Recorded from | Scope |
|---|---|---|---|
| `FloodgateHeightChangedEvent` | `EntityUIEvents.cs:507` | `Floodgate.SetHeightAndSynchronize` (527-545) | `Entities(entityID)` (509) |
| `FloodgateSynchronizedChangedEvent` | :548 | `Floodgate.ToggleSynchronization` (569-588) | `Entities(entityID)` (550) |
| `WaterMoverModeChangedEvent` | :1148 | `WaterMoverToggle.SetWaterMovement` (1179-1198), pumps incl. badwater-only (1171) | `Entities(entityID)` (1150) |
| `AutomationEvent` entries | `AutomationEvents.cs:178-198` | Floodgate automation height; FillValve ×7 (two duplicates); ThrottlingValve ×5; `WaterSourceRegulator.Open/Close/Automate`; `WaterInputPipeCoordinates.SetDepthLimit/DisableDepthLimit` | as `AutomationEvent` |

- Nothing for Sluice, WaterDump, StreamGauge or a separate badwater building.
- `WaterInputDepthAction` (`EntityUIEvents.cs:1140-1145`) is declared and never used.

### Dynamite, wonders, power, ziplines, beehives

| Class | Line | Recorded from | Scope |
|---|---|---|---|
| `DynamiteTriggeredEvent` | `EntityUIEvents.cs:737` | `DynamiteFragment.DetonateSelectedDynamite` (no-argument overload, 756-770); replays `Dynamite.Trigger()` (743-748) | `Entities(entityID)` (739) |
| `WonderActivatedEvent` | :1276 | `WonderFragment.ActivateWonder` (1324-1338); `WonderActivationFollowsHostPatcher` on `Wonder.CanBeActivated`, `Priority.Last` (1310-1322); the host writes `activated` (1281-1293) | `Entities(entityID)` (1278) |
| `ZiplineConnectionChangedEvent` | :1201 | `ZiplineConnectionAddingTool.Connect` (1239-1255), `ZiplineConnectionButtonFactory.RemoveConnection` (1257-1273) | `Entities(currentTower, otherTower)` (1203), plus the host-only game check |

- Power: `Clutch.SetMode` (`AutomationEvents.cs:199`) and `PowerMeter.*` (148-151). Nothing for batteries or
  `AdjustableStrengthPowerGenerator` (both dev-only panels: the battery slider shows in dev mode, the generator is
  `DevPowerGenerator`).
- Tubeways: no mod code; docs call them road-carrying (`TWO-COLONIES.md:391,397`).
- Beehives: no event; one comment at `Colonies/ColonyFoundingService.cs:560-563`.

### Bots

| Class | Line | Recorded from | Scope |
|---|---|---|---|
| `WorkerTypeUnlockedEvent` | `EntityUIEvents.cs:859` | `WorkplaceUnlockingService.Unlock` (964-978); dev `UnlockIgnoringScienceCost` (946-962) | Global (861) |
| `WorkerTypeSetEvent` | :980 | `WorkplaceWorkerType.SetWorkerType` (1000-1019, skipped while ticking at 1009); `WorkerTypeToggle.TryToUnlock` (1027-1057) | `Entities(workplaceEntityID)` (982) |
| `DefaultWorkerTypeChangedEvent` | :1341 | `DistrictCenterFragment.SetBeaverWorkerType`/`SetBotWorkerType` (1371-1389) | `Entities(entityID)` (1343) |

### Districts and migration (`Events/BatchEvents.cs`)

| Class | Line | Recorded from | Scope |
|---|---|---|---|
| `ManualMigrationEvent` | 61 | `ManualMigrationPopulationRow.MigratePopulation` (96-122) | `Migration(from, to)` (64) |
| `SetDistrictMinimumPopulationEvent` | 124 | `PopulationDistributor.SetMinimumAndMigrate` (148-169) | `Entities(district)` (126) |
| `SetDistrictMigrationToggledEvent` | 171 | `PopulationDistributor.ToggleAllowImmigrationAndMigrate` / `ToggleAllowEmigrationAndMigrate` (226-244) | `Entities(district)` (173) |
| `GoodDistributionSettingChangedEvent` | 274 | `GoodDistributionSetting.SetExportThreshold`/`SetImportOption`/`SetDefault` (335-366), via `DistrictDistributionSetting.AddGoodDistributionSetting` (259-271) | `Entities(district)` (276) |

### Stockpiles, goods, workplaces (`EntityUIEvents.cs`)

- `StockpilePriorityChangedEvent` 599 (`StockpilePriority.Accept/Empty/Obtain/Supply`, 648-686).
- `SingleGoodAllowedEvent` 254 (`SingleGoodAllower.Allow/Disallow`, 274-306).
- `GoodStackDeletedEvent` 773 (792-806); `HaulPrioritizablePrioritizedEvent` 1060 (1083-1099).
- `GatheringPrioritizedEvent` 67; `ManufactoryRecipeSelectedEvent` 108; `PlantablePrioritizedEvent` 149;
  `FarmHousePrioritizePlantingChangedEvent` 193.
- `BuildingPausedChangedEvent` 327 (`PausableBuilding.Pause/Resume`, 348-384).
- `ConstructionPriorityChangedEvent` 417; `WorkplacePriorityChangedEvent` 433; `WorkplaceDesiredWorkersChangedEvent` 449.
- `ToggleForresterReplantDeadTreesEvent` 1102; `EntityRenamedEvent` 809.
- `WorkingHoursChangedEvent` (`ToolEvents.cs:713`, Global; per-colony hours on replay 721-727).

### Planting, cutting, demolition, placement (`Events/ToolEvents.cs`)

- `BuildingPlacedEvent` 30 (`BuildingPlacer.Place`, 178-205; scope `Place`).
- `BuildingsDeconstructedEvent` 207 (`BlockObjectDeletionTool<BuildingSpec>.DeleteBlockObjects` 230-257, and
  `DeleteBuildingFragment.DeleteBuilding` `EntityUIEvents.cs:308-325`). Carries only `entityIDs` (239-241), TODO at
  238: G9.
- `PlantingAreaMarkedEvent` 260; `ClearResourcesMarkedEvent` 380; `TreeCuttingAreaEvent` 477; `DuplicationEvent` 760;
  `DemolishButtonClickedEvent` (`EntityUIEvents.cs:690`).

### Science, unlocks, dev mode

- `BuildingUnlockedEvent` (`ToolEvents.cs:547`), `ScienceAddedEvent` (:676, `ScienceAdder.AddScience` 696-710).
- `ConstructionSiteFinishedNowEvent` (`EntityUIEvents.cs:908`, `ConstructionSiteDebugFragment.OnFinishNowClick`
  928-941).
- The host refuses dev shortcuts while its dev mode is off (`ColonyRulesService.cs:132-139`, `IsDevShortcut` 323-326).
- `WonderDebugFragment` is not patched.

### System and colony events

- Speed: `SpeedSetEvent` (`TimeEvents.cs:17`), `SpeedBoostEvent` (:49), `ShowOptionsMenuEvent` (:208).
- `AutosaveEvent` (`SystemEvents.cs:13`; recorder commented out 34-47).
- Session: `ActionRefusedEvent`, `InitializeClientEvent`, `ClientDesyncedEvent`, `GroupedEvent`, `HeartbeatEvent`,
  `PingEvent`, `TraceLoggedForTickEvent`.
- Colonies: `FoundColonyEvent`, `ColonyPresenceEvent`, `ColonyHandoverEvent`, `PlayerHelloEvent`, steward events,
  `WishlistChangedEvent`, the five exchange events (`TradingPostExchange.cs:882-976`, all `Entities(crossingID)`),
  `ColonyFactionSwitchEvent` (`FactionChoice.cs:204`).

## 2. The automation patch list

`ApplyAutomationPatches` (102-211, called from `Plugin.cs:180`): 75 entries, 73 distinct.

| Line | Type | Methods |
|---|---|---|
| 111-113 | Chronometer | SetStartTime, SetEndTime, SetMode |
| 114-117 | ContaminationSensor, DepthSensor | SetMode, SetThreshold |
| 118-122 | FireworkLauncher | SetContinuous, SetFireworkId, SetFlightDistance, SetHeading, SetPitch |
| 123-124 | FlowSensor | SetMode, SetThreshold |
| 125 | Gate | SetOpeningMode |
| 126-129 | Indicator | SetColorReplicationEnabled, SetJournalEntryEnabled, SetPinnedMode, SetWarningEnabled |
| 132-137 | Lever | SetPinned, SetSpringReturn, SwitchState |
| 138-141 | Memory | SetMode, SetInputA, SetInputB, SetResetInput |
| 142-147 | PopulationCounter | SetComparisonMode, SetCountBeavers, SetCountBots, SetGlobalMode, SetMode, SetThreshold |
| 148-151 | PowerMeter | SetComparisonMode, SetIntThreshold, SetMode, SetPercentThreshold |
| 152-155 | Relay | SetInput, IncreaseInputs, RemoveInput, SetMode |
| 156-161 | ResourceCounter | SetComparisonMode, SetFillRateThreshold, SetGoodId, SetIncludeInputs, SetMode, SetThreshold |
| 162-163 | ScienceCounter | SetMode, SetThreshold |
| 164-166 | Speaker | SetPlaybackMode, SetSoundId, SetSpatialMode |
| 168-172 | Timer, WeatherStation | SetInput, SetMode, SetResetInput; SetEarlyActivationHours, SetMode |
| 178-188 | Floodgate, FillValve | automation height; FillValve targets, enabled, sync (179-180 repeated at 186-187) |
| 189-199 | ThrottlingValve, WaterSourceRegulator, WaterInputPipeCoordinates, Clutch | as listed above |

- No dedup and no null check on `GetMethod` (202-206, 215). beta12 found the FillValve double patch harmless.
- Comments: patched methods must be UI-only (104-108); Lever and Timer setters run at load (130-131, 167);
  `SwitchState` as an event makes the UI laggy (134-136); **HTTP intentionally omitted** (173-175); the water
  entries are "not automation-related" (182-183).

## 3. Fixes/*.cs

Co-op-only singletons are bound only when `EventIO` is set (`Plugin.cs:53`, bindings 62-75): `WonderTickService`,
`LateTickableBuffer`, `GateTickRunner`, `RealGateConflict`, `DevModeCoopWarning`, `TickOnceCoopNotice`,
`MultiplayerInputRecovery`.

| File | What it does | Gate |
|---|---|---|
| AnimationFixes.cs | Movement animation from interpolated tick time (visual) | `EventIO.IsNull` (34) |
| DemolitionSelectionFix.cs | Nearest demolish job, ties broken by entity id | :29 |
| DevKeysCoopFix.cs | Ignores the dev Ctrl keys for place-finished and no-recovery | :25, :45 |
| DevModeCoopWarning.cs | Warns that dev tools other than the three shared ones desync | :47 |
| DistrictBuildingsFix.cs | Finalizers for duplicate registrations and destroyed district centers | none |
| FrameToTickFixes.cs | Gates at the tick (30-57), real graph for gate conflicts (67-146), **automation frame path off** (156-160), crossing caches not filled by frame reads (175-203) | co-op |
| InstantNavMeshFix.cs | The instant navigation copy updates at the tick | :28, :45 |
| MultiplayerInputRecovery.cs | Resets stuck input after replays and loads | co-op binding |
| SimulationUpdateFix.cs | All commented out | n/a |
| TestingStrategies_Scrap.cs | Not compiled | n/a |
| TickOnceCoopBlock.cs | Refuses a single-computer "tick once" | :57-58 |
| TickOnlyArrayFix.cs | Saves may read tick-only arrays | save flags |
| TickTimingFixes.cs | **Deletion ends the frame's ticking** (26-36); lever spring return as simulation (56-67); paused-building exits as simulation (74-93); walker debugger's `Random.state` (100-112) | co-op |
| WaterSourceFix.cs | Water sources tick after the parallel tick | co-op binding |
| WaterSourceOrderFix.cs | Canonical order of the thread-safe water-source list | :19 |
| WaterSourceTimingFix.cs | One `Time.deltaTime` read → tick interval. **Throws** if it doesn't find exactly one (34-35) | :41 |
| WaterWheelFix.cs | Catches a UI marker exception | none |
| WonderTimingFix.cs | Wonder animations and the plane launch on the tick | `RunsThisFrame` (295) |
| ZiplineSpeedFix.cs | Water penalty from the saved path tracker | :34 |

## 4. Dynamite, terrain, tunnels, G9

- `DynamiteTriggeredEvent` records only the entity id; nothing names the blast area, terrain, or what is hit.
- No references to `Detonator`, `TerrainPhysics` or terrain destruction.
- Tunnels appear once, in the relic reward comment (`ColonyScienceService.cs:455-469`).
- Deletion timing: `TickTimingFixes.cs:15-36`, `:69-93`.
- G9 is only in `design/REVIEW-FINDINGS-1.4.0-beta11.md:349-351` (left). Not in any shipped document.

## 5. Wonders

- `EntityUIEvents.cs:1276-1338`: activation, the host's decision, the recorder.
- `Fixes/WonderTimingFix.cs`: `WonderTickService` (40-82) takes every Wonder in entity-id order, whatever its colony;
  `WonderTiming` (84-535); patches 539-669; `SaveWriter.WriteToSaveStream` restores tick poses (660-669).
- `Factions/FactionDisplayPatches.cs:233-305`: completion (local faction to the profile), panel, launch sound (runs
  inside the replayed activation on every computer).
- `DeterminismService.cs:1043-1045`: flying pilots are left out of the walker hash; `DesyncCheck.cs:76-80`.

## 6. Colony rules for these systems

- `MayChange` (`ColonyRules.cs:204-205`); `Entities` scope refuses on the first foreign id (238-247).
- The owner: `DistrictOwner.OwnerOf ?? ColonySeparation.NaturalOwnerOf` (`ColonyGameWorld.cs:44-49`);
  `DistrictOwner.OwnerOf` (`DistrictOwner.cs:89-103`): district-center owner, citizen's district, finished district
  building, `ColonyStamp`, construction district.
- The host judges before replaying (`AllowOnHost`, `ColonyRulesService.cs:51-211`); a local courtesy check first
  (`RefuseLocally`, 332-354). Separate colonies only (181, 334), except zipline links (152-159), missing buildings
  (141-150) and dev shortcuts (132-139).
- **Automation:** wiring to another colony's automator is refused; non-id strings count for nobody (24-25).
  `DuplicationEvent` needs both ends; a placement copied from another colony's building has its source stripped
  (`ColonyRulesService.cs:182-189`).
- **Per-colony readings:** Chronometer (`ColonyWorkingHours.cs:189-202`), ScienceCounter
  (`ColonyScienceService.cs:439-445`). **None** for PopulationCounter, ResourceCounter, PowerMeter or the other
  sensors, and no filter on signals between colonies.
- G6 (left in beta12): open Memory/Relay panels re-apply inputs through recorded setters (refusal notices).
- **Water:** every action is scoped to the clicked building; the replays call `*AndSynchronize` and nothing looks at
  the synchronised partners (P-3).
- **Dynamite:** only the owner detonates; nothing covers what the blast affects.
- **Wonders:** only the owner activates; no rule on the effect or the tick stepping; area effects reach any beaver
  (`TWO-COLONIES.md:362-364`); the game ends only when every beaver on the map is gone (`README.md:280`).
- **Ziplines:** both towers must be the actor's; the host alone runs `CanBeConnected`
  (`ColonyRoadNetworks.cs:138-148`).
