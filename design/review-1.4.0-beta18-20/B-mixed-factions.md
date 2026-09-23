> Reviewer B's report from the review of 1.4.0-beta18 to beta20 (see [the findings](../REVIEW-FINDINGS-1.4.0-beta18-20.md)).
> `$SP` was that session's scratchpad (the frozen beta20 source in `$SP/base`, rigs and scripts); it was not kept.
> Line numbers are beta20's. The fixes it proposes shipped in 1.4.0-beta21, except where the findings say otherwise.

# Reviewer B: mixed factions (beta20) against the decompiled game (Timberborn 1.1.2.4)

Leads: M1–M15 and E3. Source: `$SP/base` (tag `v1.4.0-beta20`, `79d4757`). Game: `$SP/dec` (all 497 assemblies) and
`Blueprints.zip`. Nobody played anything: every result below is by trace (mod + decompiled game) or by script over the
decompile. Line numbers are `$SP/base`'s; game references are `Type.Member (file:line)` in `$SP/dec`.

## Scripts and artefacts (all in `$SP/revB`)

| Path | What it is |
|---|---|
| `m2_creation_sites.py` → `m2_creation_sites.out` | Every call of `BeaverFactory.Create*`, `BotFactory.Create`, `EntityService.Instantiate`, `WorldEntitiesLoader.InstantiateEntity` in the game and the mod, with its enclosing method (M2). |
| `m6_single_faction_sites.py` → `m6_single_faction_sites.out` | Every `GetSingle<`/`GetSingleSpec<`/`.Single(`/`.First(`(`OrDefault`) over templates, specs or collections (157), and every reader of `FactionService.Current` (43), each marked PATCHED or not against the mod's 293 Harmony targets (M6). |
| `character_delete_scan.py` | Source scan: a bare `EntityService.Delete` of a Beaver/Bot/Character variable in the mod. Finds `ColonyFoundingService.cs:476` in beta20, nothing after the fix. Ready to port to StabilityTests (B-1). |
| `bp_list.py`, `bp_keys.py`, `bp_grep.py`, `goods_groups.py` | Read `Blueprints.zip` (district-center costs, collections, need collections, goods groups, decal suppliers). |
| `B-switch.diff`, `B-fingerprint.diff` | The proposed fixes as unified diffs against `$SP/base`. |
| `patch/BeaverBuddies/Colonies/*.cs`, `build/` | The patched files, and a copy of `$SP/base` with them in. The patched copy builds (`dotnet build … -c Release --no-restore`): no CS errors or warnings. The copy of content files into `revB/mods` then fails on Windows MAX_PATH; the DLL is in `build/BeaverBuddies/obj/Release/netstandard2.1/`. It has not been run. |

---

## Findings

### B-1: The faction switch deletes living beavers without killing them, which crashes the game later in the session
- **Status:** Confirmed by trace (mod and decompiled game). Not run.
- **Severity:** crash. It also causes a desync for anyone who loaded the game after the switch.
- **Hits:** mixed factions, from the first switch made after the game has ticked.
- **Evidence:**
  - The mod deletes the beavers with a bare Delete: `Colonies/ColonyFoundingService.cs:476`,
    `foreach (Beaver beaver in beavers) _entityService.Delete(beaver);`.
  - What that Delete does and doesn't do:
    - Game `EntityService.Delete` (EntitySystem:663-669) runs `IDeletableEntity`s, unregisters the entity and calls
      `Object.Destroy`.
    - `Character` is not an `IDeletableEntity`.
    - `CharacterPopulation` (Characters:352-382) removes a character only on `CharacterKilledEvent`.
      `BeaverPopulation.OnCharacterKilled` (Beavers:553) works the same way. That event is posted only by
      `Character.KillCharacter` (Characters:294-301).
    - The game's own removal of a living character is `Character.DestroyCharacter()`, which calls `KillCharacter()`
      and then `Delete` (Characters:288). It is used for a child that grows up (Beavers:274), a Wonder pilot
      (WonderPlanes:666) and Mortal (MortalSystem:702).
    - `GoodReserver` isn't an `IDeletableEntity` either. It releases reservations only in `OnDied` (InventorySystem:667, 814).
  - Readers of the stale list:
    - `CharacterExploder.ExplodeCharactersAt` (Explosions:363-392) runs on every explosion. It reads
      `character.Transform.position` for every character, with no condition.
    - `AreaNeedApplier.TryApplyNeed` (NeedApplication:301-320) is on the Folktails Beehive
      (`Beehive.Folktails` has `AreaNeedApplierSpec`). It makes one synced random draw per character, then reads
      `Transform.position` when the draw hits.
    - `BaseComponent.Transform` is a cached reference (`ComponentCache.CachedTransform`, BaseComponentSystem:255, 518).
      Once Unity has destroyed the object, `.position` throws `MissingReferenceException`.
    - An uncaught exception sends the game to its crash scene: `ExceptionListener.OnUncaughtException`, then
      `StopAllRootObjects` and `CrashSceneLoader` (ErrorReporting:447-485).
- **What happens:**
  1. A colony that has run at least one tick switches faction.
  2. Its 9 adults and 4 children are deleted but stay in `CharacterPopulation` and `BeaverPopulation`. At the end of
     that frame Unity destroys their GameObjects.
  3. Every computer that played the switch then crashes at the first explosion (dynamite, TNT, an unstable core)
     anywhere on the map.
  4. They also crash at a Beehive check that draws a hit on one of those beavers. In a normal game the injury chance is
     above 0, so this happens sooner or later in any mixed game with a Folktails colony that has a Beehive.
  5. Before that, anyone who loaded the game after the switch has no such beavers: a guest who reconnects after a
     desync, or a classic late joiner. Their Beehive check makes fewer random draws than the host's, so the synced random
     generator diverges and the game desyncs.
  6. Side effects until the game is reloaded:
     - population counts and the speed throttle (`GameSpeedThrottler`, Characters:496-504) count these beavers;
     - `PopulationService.AllDead` (Population:421-431) can never become true;
     - their reservations are never released.
  7. The save doesn't contain them, so a reload clears all of this.
- **Fix:** remove each beaver the way the game does. This is part of `B-switch.diff`:
  ```diff
  -                foreach (Beaver beaver in beavers) _entityService.Delete(beaver);
  +                foreach (Beaver beaver in beavers) RemoveBeaver(beaver);
  ...
  +        private static void RemoveBeaver(Beaver beaver) => beaver.GetComponent<Character>().DestroyCharacter();
  ```
  - It is played inside the replay on every computer, as before, and it is still a deletion, so
    `EntityDeletionEndsFramePatcher` interrupts the same way.
  - What it adds is vanilla's own death bookkeeping:
    - `CharacterKilledEvent` (the population lists);
    - `Character.Died`, which reaches `Citizen.OnDied`, the dweller, `GoodCarrier.EmptyHands` and `GoodReserver`, which
      releases the reservations;
    - the mod's read-only `ColonyJournalDeathPatcher`.
  - It posts no death notification: Mortal isn't involved, the same as for a child that grows up.
  - **Wire: none. Save: none.**
- **Check to add:**
  - StabilityTests: port `revB/character_delete_scan.py`. It fails on any `EntityService.Delete(x)` in
    `BeaverBuddies/**/*.cs` whose argument is declared as a Beaver, Bot, Character, Adult, Child, Worker or Citizen.
  - RuntimeChecks: pin the premise by IL.
    - `Character.DestroyCharacter` calls `KillCharacter`, and `KillCharacter` constructs `CharacterKilledEvent`.
    - `CharacterPopulation` has no `EntityDeletedEvent` handler.
- **Test script:** F, new step after F5.
  1. Switch colony 3, having let the game run for a moment first.
  2. Set off dynamite in colony 1, or wait two days with a beaver standing next to a Folktails Beehive.
  3. Expected: no crash, and the population panel's total equals the sum of the districts' populations.

### B-2: A switch made before the first tick misses the colony's beavers
- **Status:** Confirmed by trace (mod, game and the waiting room's save timing). Not run.
- **Severity:** gameplay. Every computer does the same thing, so there's no desync.
- **Hits:** mixed factions: waiting-room games at tick 0, and any game switched while paused right after a founding.
- **Evidence:**
  - The switch counts and replaces only `DistrictPopulation.Beavers` (`ColonyFoundingService.cs:472-476`), then spawns
    `Math.Min(adults, start.Adults)` new beavers (:498-499).
  - Beavers made with a colony have no district until a tick runs:
    - `StartingBeaversInitializer` (GameStartup:560-597) and the mod's `SpawnBeavers` (:546-555) create beavers without
      a `CharacterBirthInit`. Only births get `AssignInitialDistrict` (CharactersGame:151-160).
    - `Citizen.InitializeEntity` only adds the beaver to `UnassignedCitizenRegistry` (GameDistricts:617-623).
    - They join a district only when `DistrictCitizenAssigner.Tick` runs (GameDistricts:1550-1616).
  - The waiting room's tick-0 save is written before any tick:
    - `LobbyWorldMaker` queues it at `NewGameInitializedEvent` (`Lobby/LobbyWorldMaker.cs:23`).
    - `GameSaverUnityAdapter.LateUpdate` writes it in that same frame (GameSaveRuntimeSystem:302-305).
    - That comes before `GameInitializer.UnpauseGame`, which runs in the next frame (GameStartup:399-409).
    - `Ticker.FinishFullTick` does nothing at bucket 0 (TickSystem:585-592).
  - So in the tick-0 world every start's beavers are in no district. Nothing assigns them until the host unpauses.
  - A switch can reach that state:
    - In a waiting-room game `WaitsForStart` doesn't hold the switch (`ColonyRules.cs:315-316`, `ColonyRulesService`
      :98-104).
    - `OfferFounding` → `OfferFactionSwitch` offers it at seating, at tick 0 (`ColonyFoundingService.cs:156-170`,
      `FactionChoice.cs:161-178`). The dialog appears whenever the player's lobby pick differs or is empty.
    - The Ctrl+T card's "Play … instead" is shown too (`TradeOverviewPanel.cs:447`, with `started` true).
- **What happens:**
  1. A mixed multi-start waiting room starts. A guest left the picker alone, or wants the other faction.
  2. While the game is still paused at tick 0, the guest clicks Switch.
  3. `population.Beavers` is empty, so nothing is deleted and 0 adults and 0 children are spawned.
  4. The colony gets the Iron Teeth district center and keeps its 13 Folktails beavers. They join the new district
     at the first tick, with Folktails needs, food bonuses and fur.
  5. The same happens to a colony founded and switched while the game is paused, for example when the host paused
     before a guest founds and then switches.
- **Fix:** also count and remove the colony's beavers that are still waiting for a district. Those are the ones the
  game's assigner would give this colony: alive, of the colony's current faction, and nearest (the game's own
  `DistrictCenter.DistanceToCitizen`) to one of its district centers. Walk entities in instantiation order so the
  result is deterministic. This is in `B-switch.diff`:
  ```diff
  +            string oldFaction = ColonyFactionService.FactionOfSlot(slot);
  +            var waiting = new List<Beaver>();
  +            foreach (EntityComponent entity in _entityRegistry.Entities)
  +            {
  +                Beaver beaver = entity.GetComponent<Beaver>();
  +                Citizen citizen = entity.GetComponent<Citizen>();
  +                if (beaver == null || citizen == null || citizen.HasAssignedDistrict) continue;
  +                if (!(entity.GetComponent<Character>()?.Alive ?? false) || entity.GetComponent<CharacterFaction>()?.FactionId != oldFaction) continue;
  +                DistrictCenter nearest = _districtCenterRegistry.FinishedDistrictCenters.OrderBy(dc => dc.DistanceToCitizen(citizen)).FirstOrDefault();
  +                if (nearest != null && centers.Contains(nearest)) waiting.Add(beaver);
  +            }
  ...
  +            foreach (Beaver beaver in waiting)
  +            {
  +                if (beaver.GetComponent<Child>() != null) children++;
  +                else adults++;
  +                RemoveBeaver(beaver);
  +            }
  ```
  - Reads: saved state and positions only.
  - **Wire: none. Save: none.**
  - A smaller alternative:
    - Refuse the switch, in the host's judge and again at replay, while the colony has district centers and
      `ColonyLifecycle.PopulationOf(slot) == 0` but unassigned beavers exist.
    - Then hold the seat-time offer the way `offerPending` holds the founding offer, until the population is counted.
    - It is simpler, but the tick-0 offer becomes "unpause first".
- **Check to add:** RuntimeChecks, pinning the premises by IL:
  - `StartingBeaversInitializer.SpawnBeaver` → `BeaverFactory.CreateAdult/CreateChild`, which pass no
    `CharacterBirthInit`;
  - `Citizen.InitializeEntity` only calls `UnassignedCitizenRegistry.Add`;
  - `GameSaverUnityAdapter.LateUpdate` → `SaveQueued`.

  No headless rig can run the switch itself.
- **Test script:** D+F, new.
  1. Open a mixed waiting room on a 3-start map. The guest doesn't touch the picker.
  2. At tick 0, while still paused, the guest accepts the "switch to Iron Teeth" offer.
  3. Expected: the colony's beavers are Iron Teeth (fur, Iron Teeth food bonuses in the needs panel), and the count
     equals the game mode's starting adults and children.

### B-3: After a switch, the colony's paths keep the old faction's look
- **Status:** Confirmed by trace.
- **Severity:** display.
- **Hits:** mixed factions.
- **Evidence:** `FactionModels.RepaintIfPath` is called only when a `ColonyStamp` is set or re-stamped
  (`Colonies/ColonyStamps.cs:55, 64`). Nothing listens to `ColonyFactionService.Changed` except `FactionToolbar`
  (`FactionToolbar.cs:77`). Paths are the only buildings an untouched colony can own (`Buildings.Common` lists only
  `Path`).
- **What happens:** a Folktails colony's paths stay Folktails-coloured after it becomes Iron Teeth, until the next load.
  `DynamicPathModel.Awake` then paints them from the owner's faction.
- **Fix:** after `ColonyFactionService.Set(slot, faction)` in `SwitchFaction`, repaint the slot's stamped paths. This
  is display code only; it reads the saved stamp. It is in `B-switch.diff`:
  ```diff
  +            foreach (EntityComponent entity in _entityRegistry.Entities.ToList())
  +            {
  +                ColonyStamp stamp = entity.GetComponent<ColonyStamp>();
  +                if (stamp != null && stamp.Slot == slot) FactionModels.RepaintIfPath(entity);
  +            }
  ```
  **Wire: none. Save: none.**
- **Check to add:** none headless. Add it to the script.
- **Test script:** F5: before switching colony 3, lay a few paths. After the switch they look like Iron Teeth paths.

### B-4 (M1): Nothing checks a character's faction or needs
- **Status:** Confirmed as a gap. No divergence source found: every creation site sets the faction from replayed or
  saved state (see M2 below).
- **Severity:** test gap.
- **Hits:** mixed factions.
- **Evidence:** `ColonyDiagnostics.Fingerprint` (`Colonies/ColonyDiagnostics.cs:165-212`) hashes districts, stamps,
  exchanges and population counts, but not `CharacterFaction` or `NeedManager.NeedSpecs`.
- **What happens:** if a later change made one computer create a beaver in a different faction, nothing would show it
  until behaviour diverged. For example: a new creation path, a mod, or a "first read" moved outside `Instantiate`.
- **Fix:** add a mixed-only term to the daily fingerprint: the sum over characters of `Hash(entity) *
  (Of(SimFactionOf(entity)) * 31 + NeedSpecs.Length + 1)`. It goes in the entity loop that already exists, so it costs
  O(1) per entity, and it is added only inside the `mixed` flag. A non-mixed game's line is byte-for-byte unchanged.
  - The diff is `B-fingerprint.diff`.
  - **Wire:** the daily check line of mixed games gains `/chars:<hex>`. Both sides run the same mod version (the
    handshake checks it). **Save: none.**
- **Check to add:** StabilityTests already pins the non-mixed fingerprint shape. Add a case that the mixed flags
  segment contains `chars:`.
- **Test script:** F7 ("checks are green for 3 days") now covers characters too.

### B-5: The docs overstate what "per-character needs" does for food
- **Status:** Confirmed by trace and data.
- **Severity:** docs.
- **Hits:** mixed factions.
- **Evidence:**
  - `TWO-COLONIES.md:137` says "A beaver only eats its own faction's food". The plan's D19 claims appraisal returns 0.
  - But `Hunger` is in the Common need collection (`NeedCollection.Common`), and every food's `ConsumptionEffects`
    include `Hunger` (for example `CornRation`: Hunger 0.3 and CornRation 0.2).
  - `InventoryNeedBehavior.AppraiseGood` sums the appraisal over all effects (InventoryNeedSystem:256-315). So a
    Folktails beaver does eat Iron Teeth food for Hunger, and gets no bonus.
  - It only happens when foreign food is in reach. D3 and D18 keep it out of trade and stockpiles, so in practice that
    means a colony that received the other faction's food buildings in a handover.
- **Fix:** reword: "A beaver gets the wellbeing bonus only from its own faction's foods; any food stills its hunger
  (Berries and water are everyone's)." **Wire: none. Save: none.**
- **Check to add:** none.
- **Test script:** none.

### B-6 (M2 nit): A single-start new game uses up the once-per-process "no faction in hand" warning
- **Status:** Confirmed by trace. It is already known in the plan.
- **Severity:** docs/logging. The result is correct.
- **Hits:** mixed factions.
- **Evidence:**
  - The vanilla `GameInitializer.SpawnBeavers` (GameStartup:383-397) is wrapped only for multi-start
    (`MultiStart/MultiStartPatches.cs:175-197`).
  - `CharacterFaction.Unknown()` then calls `WarnOnce` (`Factions/CharacterFaction.cs:33-37`). `warned` is a
    `[ThreadStatic]` static for the whole process, so a genuine later case is silent.
- **Fix, optional:** in `GameInitializerSpawnBeaversPatcher`, when the game is mixed and not multi-start, push
  `MixedFactions.BaseFaction` for the vanilla call. Use a Prefix that pushes plus a Finalizer that pops, in the same
  style as `NewbornSpawnerFactionPatcher`. **Wire: none. Save: none.**

---

## Leads refuted, and what makes each safe

### M2: character creation sites
- **Status:** Refuted. Script `m2_creation_sites.py`, plus trace.
- **Every creation site in 1.1.2.4, and what covers it:**

  | Game or mod call | Covered by |
  |---|---|
  | `BeaverFactory.CreateAdult` / `CreateChild` from `StartingBeaversInitializer.SpawnBeaver`, multi-start | `MultiStartPatches.cs:190` |
  | The same, single start | Base fallback. Correct, because the single start is slot 0 (see B-6) |
  | `BeaverGeneratorTool.PlaceBeavers` (dev) | `CharacterFaction.cs:206-227` (local; dev tools are local-only anyway) |
  | `ColonyFoundingService.SpawnBeavers` | :379 and :496 |
  | `CreateNewbornAdult` / `CreateNewbornChild` from `NewbornSpawner.SpawnAdult/SpawnChild` | :136-157 (`TargetMethods`, both) |
  | `CreateAdultFromChild` from `Child.GrowUp` | :163-178 |
  | `BotFactory.Create(pos)` from `BotGeneratorTool.PlaceBots` | :206-227 |
  | `BotFactory.Create(pos,rot,init)` from `BotManufactory.OnProductionFinished` | :185-200 |
  | `WorldEntitiesLoader.TryInstantiateEntity` | :111-128 (single overload) |
  | `UndoableEntity.InstantiateEntity` | Never in a game: the Game scene binds `DummyUndoRegistry` (GameScene:214, 255) |
  | `PlaneSpawner`, `FireworkSpawner`, `BlockObjectFactory` | Not characters |

- **Order of reads:**
  - `TemplateInstantiator.Instantiate` builds every component and runs the dedicated initializers, then
    `SetActive(true)` runs every Awake (TemplateInstantiation:305-318).
  - `NeedManager.Awake` → `GetNeeds` is the first reader of `FactionId`, and it runs inside the scope that pushed the
    faction.
  - `CharacterFaction` is decorated on `BeaverSpec`, which both beaver templates have. Bots take their faction from
    their own template.
- **Loads:** the loader peek pushes the saved faction before Awake, and `Load` agrees with it.

### M3(a): a "free" unfinished second district center
- **Status:** Refuted by the data.
- **Why:**
  - Both `DistrictCenter.*.blueprint.json` have `BuildingCost: []`, `ScienceCost: 0` and `PlaceFinished: true`.
  - `BuildingPlacer.ShouldBePlacedFinished` (BuildingTools:240-247) therefore always places them finished. That
    includes the mod's replay, which goes through `placer.Place` (`Events/ToolEvents.cs:84`).
  - So an unfinished district center doesn't exist in 1.1.2.4, and every district center is free.
  - An untouched colony can place a second one (`UntouchedFacts` skips `DistrictCenter`), and the switch rebuilds both.
    There's nothing to gain from it.
  - It would reopen only if a mod gave district centers a cost.

### M3(b): the new center on the old one's cells, in the same replay
- **Status:** Refuted by trace.
- **Why:**
  - Block occupancy is released at once: `BlockObject.DeleteEntity` → `RemoveFromService` → `BlockService.UnsetObject`
    (BlockSystem:1075-1078, 1253-1257). So `CreateAsFinished` doesn't collide.
  - The district registry and the district follow at once too:
    - `BlockObjectState.DeleteEntity` → `NotifyOnStateExited` (BlockSystem:1805-1811) → the
      `DistrictCenterRegistry` update and `DistrictCenter.OnExitFinishedState`.
    - The change is `DistrictService.RemoveDistrict`, which is enqueued, and then the new center's `AddDistrict`,
      also enqueued (Navigation:2850-2864).
    - `DistrictUpdater` applies both FIFO queues in order (Navigation:3023-3056). The navmesh update is batched the
      same way on every computer.
  - The switch runs as one synchronous call inside one replay. On every computer the deleted objects are alive for the
    whole call and gone by the next frame.
  - The deletion interrupt stops the frame's remaining buckets after the replay step (`ReplayService.cs:1126`). It
    doesn't split the event batch. This is beta12 D2's mechanism, unchanged.

### M3(c): goods carried, bots, beavers outside the district population
- **Carried goods:** lost, the same on every computer. With B-1's fix they are emptied as at a death.
- **Bots:** an untouched colony can't have any, because a bot assembler is its own faction's building.
- **Beavers outside the district population:** that part is B-2.

### M3 order: the toolbar refreshes before the new center exists
- **Status:** Refuted: it is display only (see M4).

### M4: UI changes inside replays
- **Status:** Refuted by trace.
- **Why:**
  - `RefreshNow` can exit only toolbar tools that `FactionToolDisabler` just disabled. Those are `BlockObjectTool`s and
    `PlantingTool`s.
    - `BlockObjectTool.Exit` removes the input processor, hides previews and resets the area picker
      (BlockObjectTools:699-706).
    - `PlantingTool.Exit` → `SelectionToolProcessor.Exit` resets the picker and cursor, and `ShowNoneCallback` is empty
      (PlantingUI:1365-1406, SelectionToolSystem:142-148).
    - `ToolService.SwitchToolInternal` posts `ToolExitedEvent` and `ToolEnteredEvent`, and `ExitToolGroup` posts
      `ToolGroupExitedEvent`.
  - Every listener of those events is visual: `ConstructionModeService` (model swap and water opacity),
    `ToolButton`/`ToolGroupButton` classes, highlighters, `ExplosionVisualizerService`, brushes and the map editor.
  - None of them calls a method on the mod's recorded list (the `DoPrefix` targets in `Events/` and `Colonies/`).
  - `OnDevModeToggledEvent(null)` never dereferences its argument (ToolButtonSystem:297-300, 764-767).
  - The `LocalSlot` reads in `Found`/`SwitchFaction` (:384, 388, 501, 507) only clear `LocalFactionPick` and move the
    camera.

### M5: the decal default and the resource counter's list
- **Status:** Refuted, both, by trace and data.
- **The decal default:**
  - Only six templates have `DecalSupplierSpec`: PoleBanner, SquareBanner and Detailer, each for both factions.
  - All six are single-faction templates, so `DisplayFactionOf` returns the template's faction. It never falls back
    to the owner, and it is deterministic.
  - It applies only when `ActiveDecal` is empty, which means a new entity. It picks a built-in decal of that faction,
    and every computer has those.
  - `DistrictOwner.OwnerOf` is a pure read (`Colonies/DistrictOwner.cs:89-103`).
- **The resource counter:** `ResourceCounterGoodsDropdownProvider.Items` feeds only the dropdown UI
  (AutomationBuildingsUI:2784-2797). The counter's good is seeded as `_goodService.Goods[0]` in
  `ResourceCounter.Awake` (AutomationBuildings:3533-3536), not from `Items`.

### M6: crash sites with both factions loaded
- **Status:** Refuted. Script `m6_single_faction_sites.py`: 157 `Single`/`First` sites and 43 `Current` readers.
- **What makes each safe:**
  - **Template `GetSingle` sites:**
    - `AdultSpec`/`ChildSpec` are fixed by de-duplication.
    - `BotSpec` and both `ModularShaftPartsSpec` sites are patched.
    - `BlockOccupierSpec` and `RecoveredGoodStackSpec` are Common (one each).
    - `PlaneSpec` is `Planes.IronTeeth` only, one.
  - **`.Single` over templates:**
    - `PlantingToolButtonFactory` is patched.
    - `WorkingRefineryForEachRecipeAchievement`, `BuildingService.GetBuildingTemplate` and
      `BlockObjectToolFinder.TryFindTool` match unique exact names.
    - `TemplateNameMapper` maps backward-compatible names with `throwIfDuplicated:false`.
  - **`GetSingleSpec` sites:** these are global specs, the same in a mixed game.
  - **`TopBarPanel.CreateCounter`:** its single-good groups are `Water` and `Badwater`, one good each, which
    `goods_groups.py` confirms.
  - **Dictionaries:** no `ToDictionary`/`ToFrozenDictionary` is built over `TemplateService.GetAll`, `GoodService.Goods`
    or needs.
  - **Unpatched `Current` readers:**
    - achievements and `FactionGoalsUnlocker` (D25);
    - `BeaverSelectionSound` (both factions' `SoundId` is "Common");
    - the four providers (base, plus `OtherFactionCollections`);
    - `StartingBuildingSpawner.Load` (base; multi-start swaps it, founding uses `CenterOf`);
    - `GameOverBox.Load` (overridden at `OnGameOverEvent`);
    - `IsWonderCompletedWithCurrentFaction` (only used by the patched `CompleteWonder`);
    - `DrivewayModelInstantiator.Load` and `DynamicPathModel.AddModel` (repainted by postfixes);
    - `GoalRowFactory` (D25);
    - `WorkerOutfitService.Load` (superseded by the `TryGetOutfitSpec` prefix);
    - the dev module's log text.
  - **Wonders:** the two Wonders are `EarthRecultivator` and `EarthRepopulator`. The only per-faction single is the
    plane template, which is fine.
  - **`FactionGoalsSystem`:** profile only.
  - **Validators:** `FactionValidators` and `NeedVerifier` read global specs, independent of the mode.
  - **The map editor:** `FactionService` is bound only in the Game scene (GameFactionSystem:440-452), so `Decide` never
    runs there, and the main menu's `NewGameFactionCapture` resets the statics first.
  - **Blueprint modifiers:** a gated union; both factions' lists are empty.

### M7: the 20 replaced method bodies, line by line against the game
- **Status:** Refuted: no drift found.

  | Method (game file:line) | Verdict |
  |---|---|
  | `NeedManager.GetNeeds` (NeedSystem:698) | Same list. `NeedsFor` filters `GetBeaverNeeds`/`GetBotNeeds` (already ordered by `Order` and difficulty-modified) to Common plus the faction's collection, which is exactly the single-faction list. |
  | `WellbeingLimitService.GetMaxWellbeing(tracker)` (Wellbeing:434) | Same sum over the character's faction's needs. Display and achievements only. |
  | `BeaverTextureSetter.InitializeEntity` (Beavers:577) | Same component reads and the same `Child` test. Exactly one `GetEnumerableElement` draw, from the same `FactionSpec` arrays. |
  | `BotFactory.Load` (Bots:217) | Caches each faction's bot and sets `_botTemplate` to the base faction's. Vanilla runs if none is found. `Create`'s prefix only picks the template. |
  | `WorkerOutfitService.TryGetOutfitSpec` (WorkerOutfitSystem:483) | Same logic, keyed by faction. `Workplace?.` is null-safe, and a string key replaces the int hash. |
  | `CharacterButton.ShowAdultEmpty/ShowChildEmpty/ShowBotEmpty` (CharactersUI:233-249) | Same body, with the faction's avatar. |
  | `DistributionSettingGroupFactory.CreateItems` (DistributionSystemBatchControl:402) | Same body plus a filter. |
  | `GoodStatisticsGroupFactory.CreateItems` (GoodStatisticsBatchControl:541) | Same body plus a filter. It is eager (`ToList`) where the game's is lazy; UI only. |
  | `GoodStockpilesTooltipFactory.AddIcons` (StockpilesUI:809) | Same, plus a filter. A missing key returns instead of throwing. |
  | `GameWonderCompletionService.CompleteWonder` (GameWonderCompletion:310) | Same, with the local faction. |
  | `GameUISoundController.PlayWonderLaunchSound` (GameSound:446) | Same. |
  | `TutorialService.GetConfigurations` and `TutorialTriggers.Load` (TutorialSystem:472, 746) | Equal to a faction without `StartingFactionSpec`. |
  | `ShaftFrameFactory.Load` and `ShaftModelFactory.Load` (ModularShafts:1350, 1444) | Same, with the faction's parts. |
  | `DecalService.Load` (DecalSystem:324) | Same, without the faction filter. |
  | `DecalButtonContainer.Show` (DecalSystemUI:240) | Same, plus a filter. |
  | `PlantingToolButtonFactory.GetPlanterBuildingName` (PlantingUI:1459) | Same, with the planter chosen by faction. |

### M8: rules keyed by template
- **Status:** Refuted.
- **Why:**
  - **Stockpiles:**
    - `StockpileInventoryInitializer` is a dedicated decorator initializer. It runs inside `Instantiate`, after every
      spec component exists (TemplateInstantiation:309-316). That covers creation and load alike.
    - A handover doesn't re-create anything, and ownership isn't read.
    - The good picker lists `Inventory.AllowedGoods` (StockpilesUI:1196).
    - `SingleGoodAllower.DuplicateFrom` checks `Takes` (InventorySystem:1839-1846).
  - **Planters:** planters are decided in Awake. **Yields:** `IsAllowed` backs `GetAllowedYielders`.
  - **`_botTemplate`:** every mixed `Create` sets `_botTemplate` from the creation context first, and both factions
    have a bot. It can only matter for a faction mod without one.

### M9: Wonder completion on a tick path
- **Status:** Refuted.
- **Why:** it writes the player profile and `WasCompletedFirstTime*`. Only `WonderCompletionPanel` reads those
  (GameWonderCompletionUI:326-327). `WonderCompletedEvent` and `CountdownFinished` stay vanilla.

### M10: the founding checks use the base faction's footprint
- **Status:** Refuted for 1.1.2.4.
- **Why:** both district centers are 3×3×5 with the same blocks, and RuntimeChecks pins that. It is a limit only
  against mods that change one faction's footprint.

### E3: the faction decision in every game
- **Status:** Refuted.
- **What runs outside the try/catch:** only field resets. `SetBase` is null-safe.
- **Non-mixed games:** `OtherFactionCollections` returns empty for all four kinds. The empty provider adds nothing to
  `TemplateCollectionService`'s list, to `GameGoodFilter`/`FactionNeedService`'s set unions, or to `MaterialRepository`.
  It creates no loadable singleton, so load order is unchanged. De-duplication and modifiers are gated. Content and
  order are therefore beta19's.
- **Load order:** `FactionCatalog` can't be built before `AllTemplates` exists.
  - Its constructor takes `TemplateCollectionService`, which takes the game's providers, which take `FactionService`.
  - `SingletonListener` collects singletons in construction order, dependencies first (SingletonSystem:660-698).
  - The only earlier callers are `BotFactory.Load` and the two shaft `Load`s. Each of them depends on
    `TemplateService` → `TemplateCollectionService` (TemplateSystem:305).
  - A failed build stays unbuilt and retries.
  - `Blueprint` has reference equality (BlueprintSystem:678), so `Distinct` and the catalog's dictionaries key by
    instance.
- **Entry points:** the solo capture's only entry is `NewGameModePanel` → `StartNewGame` (MainMenuPanels:1312). No
  tutorial path reaches it.

### M11–M15: gameplay and display
- **M11, needs:**
  - Each faction's bots get only their own critical need: `Biofuel` or `Energy`, by template.
  - The wellbeing maximum is per character.
  - `EffectProbabilityService` and `SoakedEffects` read the union, which is harmless because `NeedManager` ignores
    needs it lacks (NeedSystem:627-640).
  - The food claim is B-5.
- **M12, trading:**
  - Exactly Common (13) plus {BotChassis, BotHead, BotLimb, TreatedPlank} = 17 goods, plus Science, and no Beavers.
    That equals D3's list.
  - The form (`GiveAllowed`/`GetAllowed`), the prefill, the picker, the wishlist, `WhyNotPropose`, `Accept` and the tick
    check all call `FactionTrade.Allows` with the same giver→receiver direction. They read the saved table and the
    catalog only.
  - Nit: `CheckTradingPosts` evaluates `OwnerOf(half)` and `OwnerOf(partner)` for every open exchange every 8 ticks,
    even when the game isn't mixed. The arguments are evaluated before `FactionTrade.Allows` returns. The cost is
    negligible.
- **M13, handover:**
  - `PreferSameFaction` receives the same distance list as the old choice, and `MaxValue` is filtered on both sides.
  - Received buildings keep their template rules.
  - The toolbar refreshes through `RefreshToolLocks` (`ColonyHandover.cs:389`).
- **M14, display:**
  - The disabler is O(1) with a cache that `FactionToolbar.Load` resets.
  - Dev mode shows everything (`ToolButton.ToolEnabled`).
  - The shafts' second model set never re-enters its own postfix (the `ShaftBuildFaction` guard).
  - The paths after a switch are B-3.
- **M15, the untouched rule as built:**
  - Marks don't count, and an open exchange does.
  - Only `Path` is common.
  - A Trading Post, of any faction, counts once it is stamped to the colony.
  - The host's judge and the replay use the same `UntouchedFacts`.
  - Nit: `IsUntouched` walks every entity, including trees, twice a second while the Ctrl+T window is open. That is
    acceptable.

---

## Found sound (don't redo)

- **M2:** every 1.1.2.4 character creation site is covered (table above). The loaded-beaver peek is right. `NeedManager.Awake` is the first reader, inside the creation scope.
- **M3(a):** district centers are free and always placed finished in 1.1.2.4, so no "free unfinished center" exists.
- **M3(b):** the old center's blocks are released synchronously. District and navmesh changes are applied FIFO. The deletion interrupt doesn't split the replay.
- **M4:** every tool exit and tool, group and dev-mode event listener that `RefreshNow` can trigger is display only. The `LocalSlot` reads in Found and Switch are local.
- **M5:** the decal default is always a template's faction, so it is deterministic. The counter's `Items` list is UI only.
- **M6:** no crash site beyond the plan's §3.2. Script output: `revB/m6_single_faction_sites.out`.
- **M7:** all 20 replaced bodies match the game, apart from the intended faction choice. The texture setter makes exactly one synced random draw.
- **M8:** stockpile, planter and yield rules apply at creation and at load, keyed by template. `_botTemplate` is always set per creation.
- **M9:** Wonder completion writes only the profile and UI flags.
- **M10:** equal district-center footprints are pinned by RuntimeChecks.
- **E3:** non-mixed games load exactly beta19's collections, in the same order. `FactionCatalog` can't be built early. `Decide` never runs in the map editor.
- **Trading:** the 17 goods plus Science and no Beavers are consistent across the form, picker, wishlist, propose, accept and the tick check.
- **Handover:** the same-faction preference is right.
- **Toolbar:** the disabler, the dev-mode override and the refresh arguments are sound.
- **`DistrictOwner.OwnerOf`:** a pure read, so display callers can't mutate state.
- **Dev spawns:** they push the local faction, which is harmless because dev generator tools are local-only (already a documented desync).
