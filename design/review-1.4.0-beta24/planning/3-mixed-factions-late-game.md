# Planning inventory 3: mixed factions and the late game (beta23; beta24 changed none of the files cited here)

Made while planning [REVIEW-PLAN-1.4.0-beta24.md](../../REVIEW-PLAN-1.4.0-beta24.md), by a read-only sweep of
`BeaverBuddies/Factions/`, the mixed-factions plan and beta21's review, checked in places against the game's
`Blueprints.zip` (`Timberborn_Data/StreamingAssets/Modding/Blueprints.zip`). Line numbers are beta23's; re-check before
citing.

**The Wonders:** Folktails' is the **Earth Recultivator**, Iron Teeth's the **Earth Repopulator** (planes and pilots)
(`Blueprints.zip: Buildings/Monuments/EarthRecultivator/…Folktails`, `…/EarthRepopulator/…IronTeeth`). The
mixed-factions plan names neither.

## 1. Patches in Factions/ (46 classes)

- Every patch checks `MixedFactions.IsOn` first, except `MixedFactionsDecidePatcher` (the decision) and
  `NewGameFactionCapturePatcher` (main menu).
- RuntimeChecks enforces this from the compiled code (`PatchGateChecks.cs:27-43`); StabilityTests by a text scan
  (`FactionChecks.cs:222-246`).
- All of them are applied last, in a catch that turns the feature off (`Plugin.cs:197-212`).

**Key:** SIM = simulation or saved state; TICK-DISPLAY = runs in a tick or replay, changes only what is shown or the
profile; LOAD = scene load; UI = display.

| Class (file:line) | Game method | Replaces? | Kind |
|---|---|---|---|
| `WorldEntitiesLoaderFactionPatcher` (CharacterFaction.cs:118-135) | `WorldEntitiesLoader.InstantiateEntity` | no | LOAD, SIM |
| `NewbornSpawnerFactionPatcher` (:143-164) | `NewbornSpawner.SpawnAdult`, `SpawnChild` | no | **SIM** (births) |
| `BeaverGrowUpFactionPatcher` (:170-185) | `BeaverFactory.CreateAdultFromChild` | no | **SIM** |
| `BotManufactoryFactionPatcher` (:192-207) | `BotManufactory.OnProductionFinished` | no | **SIM** |
| `DevCharacterFactionPatcher` (:213-234) | `BeaverGeneratorTool.PlaceBeavers`, `BotGeneratorTool.PlaceBots` (local faction) | no | dev |
| `FactionNeedsPatcher` (FactionCharacterPatches.cs:31-45) | `NeedManager.GetNeeds` | **yes** | **SIM** |
| `FactionMaxWellbeingPatcher` (:52-72) | `WellbeingLimitService.GetMaxWellbeing` | **yes** | UI, achievements |
| `FactionBeaverTexturePatcher` (:81-95) | `BeaverTextureSetter.InitializeEntity` (one synced draw) | **yes** | **SIM** |
| `FactionBotFactoryLoadPatcher` (:103-122) | `BotFactory.Load` | **yes** | LOAD |
| `FactionBotFactoryCreatePatcher` (:124-135) | `BotFactory.Create` (sets `_botTemplate`) | no | **SIM** |
| `FactionWorkerOutfitPatcher` (:143-157) | `WorkerOutfitService.TryGetOutfitSpec` | **yes** | TICK-DISPLAY |
| Avatars, empty slots (:163-247) | badges, `CharacterButton` | some | UI |
| `FactionStockpilePatcher`, `FactionStockpileGoodsPatcher` (FactionBuildingRules.cs:25-54) | `StockpileInventoryInitializer.Initialize`, `InventoryInitializer.GetGoods` | no | **SIM** |
| `FactionPlanterPatcher` (:61-76) | `PlanterBuilding.GetAllowedPlantables` | no | **SIM** |
| `FactionYieldRemoverPatcher` (:84-95) | `YieldRemovingBuilding.IsAllowed` | no | **SIM** (called in ticks from `InRangeYielders`) |
| `FactionPlanterNamePatcher` (FactionToolbar.cs:117-135) | `PlantingToolButtonFactory.GetPlanterBuildingName` | **yes** | UI (crash fix) |
| Statistics, wellbeing box, distribution items, import icons, good statistics, stockpile tooltip (FactionDisplayPatches.cs:57-197) | panel factories | some | UI |
| `FactionResourceCounterPatcher` (:204-214) | `ResourceCounterGoodsDropdownProvider.InitializeEntity` | no | the dropdown only (beta21 M5) |
| `FactionGameOverPatcher` (:220-231) | `GameOverBox.OnGameOverEvent` | no | UI |
| **`FactionWonderCompletionPatcher`** (:238-256) | `GameWonderCompletionService.CompleteWonder`, reads **`LocalFaction`** (:244) | **yes** | **TICK-DISPLAY**: called from `WonderCompletionCountdownStarter.Tick` and `GameWonderCompletionRestorer.PostLoad`; writes the profile and the `WasCompletedFirstTime*` flags |
| `FactionWonderPanelPatcher` (:262-286) | `WonderCompletionPanel` | no | UI |
| `FactionWonderSoundPatcher` (:293-305) | `GameUISoundController.PlayWonderLaunchSound` (`SelectedFaction ?? LocalFaction`) | **yes** | UI, inside the replayed activation on every computer |
| Tutorials (:311-326) | `TutorialService.GetConfigurations`, `TutorialTriggers.Load` | skip / yes | LOAD |
| Shafts (FactionModelPatches.cs:139-191) | `ShaftFrameFactory.Load`, `ShaftModelFactory.Load`, `ModularShaftModelService.Load`, `ModularShaftModelUpdater.Awake` (readonly field set by reflection) | some | LOAD, models |
| Paths, driveways (:198-227) | `DynamicPathModel.Awake` (previews use `LocalFaction`), `DrivewayModelInstantiator.InstantiateModel` | no | models |
| Decals (:235-282) | `DecalService.Load`, `DecalButtonContainer.Show`, **`DecalSupplier.InitializeEntity` (writes the saved `ActiveDecal`)** | some | saved state (beta21 M5: deterministic) |
| `MixedFactionsDecidePatcher` (MixedFactions.cs:186-193) | `FactionService.Load` | no | LOAD, ungated by design |
| Blueprint modifiers, template de-dup (:201-253) | `FactionBlueprintModifierProvider.Initialize`, `TemplateCollectionService.Load` | no | LOAD |
| `NewGameFactionCapturePatcher` (NewGameFactionCapture.cs:109-118) | `GameSceneLoader.StartNewGame` | no | main menu |

**Faction code outside Factions/** (not covered by PatchGateChecks): the multi-start patches
(`MultiStart/MultiStartPatches.cs:89-102, 189-191`); `ColonyFoundingService.Found` (:380-397) and `SwitchFaction`
(:469-557); `ColonyRulesService.JudgeFactions` (:220-272, host); `ColonyHandover` (:163-165); `TradingPostExchange`
(:523, 531-532, 616, 707); `FactionToolbar.RefreshNow` inside replays (FactionToolbar.cs:87-108, via
`ColonyFactionService.Changed` :109 and `RefreshToolLocks`, `ColonyScienceService.cs:355`).

**None of the faction code is timed by `ColonyProfiler`.** The plan's R6 (memory and load time, "measure once",
MIXED-FACTIONS-PLAN.md:884) was never measured.

## 2. Late-game systems: handled, or never mentioned

| System | Status |
|---|---|
| Bots and bot needs | **Handled:** needs and template per character. **Named, never checked:** Folktails Catalyst and PunchCard, Iron Teeth Grease and ControlTower. Biofuel, Catalyst, PunchCard and Grease are one-faction goods (`GoodCollection.*`), so they never cross between factions |
| Bot parts | BotChassis, BotHead, BotLimb are among the 17 shared goods (FactionRuntimeChecks.cs:142-143). The bot part factory (both factions) is never mentioned |
| Charging | Nothing in code. `ChargingStation.IronTeeth` is a powered attraction for Energy. Nothing checks that a bot charges |
| Births (breeding pods, lodges) | **Handled** (`NewbornSpawnerFactionPatcher`; M2 traced the sites). No Script F step, no run-time check |
| Tubeways (Iron Teeth only) and ziplines (Folktails only) | **Never mentioned** for factions (only as road-carrying, TWO-COLONIES :391, :399-400) |
| Badwater rig (`BadwaterRig.Folktails` only) | **Never mentioned** anywhere in the repo |
| Beehive | The BeeSting need, and B-1's crash path (fixed). Its effect on another faction's crops is **never mentioned** |
| Wonders | Screens, sound and completion handled (plan D24; `FactionDisplayPatches.cs:233-305`). **Never mentioned:** each effect is a faction need (`EarthRecultivator` in `NeedCollection.Folktails`, `EarthRepopulator` in `NeedCollection.IronTeeth`); the completion countdown is one for the game |
| Explosives, dynamite | Explosives is a shared good. Explosives factory, dynamite, tunnel and detonator (both factions) **never mentioned** |
| Metal | ScrapMetal and MetalBlock shared. MetalPart, Metalsmith and the efficient mine are Iron Teeth; the Mine Folktails; the Smelter both. **Never mentioned**. No Plastic good in 1.1.2.4 |
| Monuments, decorations, wellbeing | Wellbeing maximum per character. Monuments **never named**. Most decoration and monument needs belong to one faction; BeaverStatue and Detailer are both. TWO-COLONIES :362-363 ("help any beaver … whoever's it is") has no mixed-game caveat |
| District centers | **Handled** (equal footprints pinned, FactionRuntimeChecks.cs:63-78) |
| Stockpiles (D18) | **Handled.** District Crossing imports are filtered only in the display (FactionDisplayPatches.cs:113-143) |
| Yield removers, planters | **Handled.** Script F only looks at the farmhouse |
| Shafts, paths, driveways, decals | **Handled** (models) |
| Faction goals, achievements | Left as the base faction (D25) |
| Automation (Lever.Folktails vs Lever.IronTeeth, …) | **Never mentioned.** Cross-colony wiring is refused; D17 stops a colony placing the other faction's automation. Wiring across factions can only happen inside one colony after a handover |
| Water buildings | **Never mentioned**: the ordinary and badwater pumps are Folktails, the deep pumps Iron Teeth; Centrifuge, Discharge and the compact pump are both |
| Power across colonies of different factions | **Never mentioned** (no power rule exists at all) |

## 3. What the desync checks see in a mixed game

- **Digest (every heartbeat):** only `ColonyDigest.Note("faction", slot, …)` in `ColonyFactionService.Set`
  (ColonyFactionService.cs:107), which counts only in a tick or replay (the new-game starts aren't counted). Births,
  growing up, bots and the switch's spawns add nothing.
- **Daily fingerprint** (`ColonyDiagnostics.cs:165-221`, compared, guest stops on a difference):
  `/mixed:<table>` and `/chars:<hash>`, which sums over every entity with a `NeedManager`
  `Hash(entity) * (faction * 31 + NeedSpecs.Length + 1)` (:182-189), one hash for the map. **No counts by faction per
  colony.** `people=` is adults, children and bots per slot.
- **Not hashed:** which needs a character has and their values, wellbeing, the fur draw, the outfit, the bot template,
  and what D18 decides on buildings (a stockpile's goods, a planter's plants, a gatherer's yields).
- `CharacterFaction.FactionId` fills itself in on first read (CharacterFaction.cs:31); the fingerprint reads it. Safe
  only because the needs are built, and read it, first.
- Review B-4 asked for a check that the mixed line contains `chars:`. None exists.

## 4. Documented limits

- TWO-COLONIES *Known limits*: a mixed save needs Timber Together and a new game (:446-448); more memory and a longer load
  (:449); both Trading Post halves use the placer's model, and achievements, the unlock goal, the load menu and other
  mods see the host's faction (:450-452); no picks for a game hosted from inside a game (:453-454). "Not played yet"
  (:108). The *State of testing* paragraph (:7-16) doesn't list mixed factions among the unplayed parts.
- STABILITY-CHANGELOG: beta20 "Not seen in a game" (:181); beta21 Scripts D and F owed (:103).
- Left in beta21: B-6 (FINDINGS :73, :257-258), E5, J11b, J8b.

## 5. Existing checks

- **StabilityTests, about 22:** `FactionChecks.cs` (14, pure logic and three source scans), `LobbyChecks.cs:386-460`,
  `ReviewBeta21Checks.cs:49, 76, 90`, `JoinReviewChecks.cs:274`.
- **RuntimeChecks, about 14:** `FactionRuntimeChecks.cs` (10: six read `Blueprints.zip`, one reflects members, one patch
  targets, one UI classes, one event JSON), `PatchGateChecks.cs:27, 45`, `ReviewBeta21RuntimeChecks.cs:19, 40`.
- **Nothing runs a game or creates a character.** No check exercises needs, births, the D18 filters or the
  fingerprint.

**Script F** (ALPHA-TEST-SCRIPTS.md:430-480): setting on; a mixed room on a 3-start map; picks; in game; toolbars;
build housing, farmhouse, warehouse, bot assembler, charging station (Energy against Biofuel); a dev-placed Iron Teeth
building refused; a Trading Post with 17 goods; save, load, rehost, checks green for three days (the plan asked for a
week); standard-map founding; the switch (11a dynamite and Beehive after it, 11b at tick 0); the save's room; a locked
faction; a faction mod; send logs.

**What Script F doesn't touch:** births and growing up; secondary bot needs and the bot part factory; charging;
tubeways and ziplines; the badwater rig; the beehive on crops; both Wonders and their completion; the explosives
factory; metal industries; monuments and decorations across factions; gatherers, lumberjacks, scavengers; District
Crossings and distribution; automation; water buildings; power across colonies; handovers between factions; goals,
achievements, game over; any long or large-map run; memory and load time.
