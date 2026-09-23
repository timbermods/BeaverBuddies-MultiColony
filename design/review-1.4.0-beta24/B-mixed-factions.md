# Reviewer B: Folktails and Iron Teeth together in the late game, and both Wonders (1.4.0-rc1 review)

**Base:** `1323b8c` (1.4.0-beta24, the P-1 fix and the empty check files), Timberborn 1.1.2.4. **Leads:** F1 to F9, V1 to
V3, sweep 4 (faction content) and my share of sweep 7 (crash paths): `Factions/`, `Fixes/WonderTimingFix.cs`, the Wonder
events of `Events/EntityUIEvents.cs`, and the faction parts of `ColonyFoundingService` and `ColonyHandover`.

**How:** every template, good, need and recipe in `Blueprints.zip` (a script, kept as RuntimeChecks), the decompiled game
(all 497 assemblies), the mod's source and compiled IL, the game's own code run outside the game where it needs no Unity
(an animator built from each bot's blueprint, the pilot's method decoded and transpiled), and measurements of the mod's
own code on the three reference saves. **Nothing was played; the game was never started.**

**Checks:** StabilityTests 418 → **422**, RuntimeChecks 362 → **374** on both builds (Release Steam and Release), both
builds with 0 warnings. Every fix's check fails on beta24's DLL or source (run on `beta24-ReleaseSteam.dll`: B1, B2, B3
(both), B4 and B5 fail; the sound guards F1, F3, V1, V2 pass on both, as they should).

**Files I changed outside my own:** `Colonies/ColonyDiagnostics.cs` (D's), two lines: the census in the daily line (F6).
`Colonies/ColonyFoundingService.cs` (IsUntouched, a display path of the faction switch, which is mine).

---

## 1. Findings

### B1: a Folktails bot that pilots the Earth Repopulator stops the session (Confirmed; crash, session-stopper; mixed)

- **The game.** When the Earth Repopulator is activated each of its assigned workers becomes a pilot
  (`Timberborn.WonderPlanes: PlaneLauncher.OnWonderActivated` :705 → `TeleportAndInitializePilots` :710 →
  `Pilot.PrepareForFlying` :354). `PrepareForFlying` sets the animation flag `"Piloting"` with no check
  (`_characterAnimator.SetBool(AnimationName, true)`, :360). `TimbermeshAnimatorController.SetBool` reads the flag from a
  dictionary (`_boolValues[parameterName]`, `Timberborn.TimbermeshAnimations` :1072): a name the animator lacks throws
  `KeyNotFoundException`. The same call runs at load for a pilot in flight (`Pilot.PostLoadEntity` :309).
- **The data.** `Pilot` is decorated on every adult beaver and every bot. Every beaver's animator has `Piloting` (one
  shared template), and so has an Iron Teeth bot's; a **Folktails bot's has not** (`Characters/Bot/Bot.Folktails`:
  `TimbermeshAnimatorControllerSpec.BoolParameters`). The Wonder takes bots (`EarthRepopulator.IronTeeth`
  `WorkplaceSpec`: `DisallowOtherWorkerTypes` false, a Bot unlock for 5,000 science), and Iron Teeth players use bots as
  pilots on purpose: pilots are destroyed after the flight.
- **The path, without dev mode.** A Folktails colony with bots whose player is away is handed to an Iron Teeth colony:
  by the host from the Ctrl+T window (`TradeOverviewPanel.cs:790-795`, `HandoverReason.ByHost`; any living colony
  whose player is not in the game, `ColonyHandover.HostMayHandOver` :338), or by the host's *abandoned colony* setting
  (`ColonyHandover.cs:263`; in a two-player mixed game the only present colony is the other faction's, D21's
  fallback). The Iron Teeth player builds an Earth Repopulator in that district (D17 lets a colony place its own
  faction's buildings anywhere), sets it to bots, and activates it. The activation is a replayed action
  (`WonderActivatedEvent.Replay`, `EntityUIEvents.cs:1289`): every computer throws in the same place, the replay fails,
  and `ReplayService.AbortReplay` (:524) stops multiplayer for everyone ("Multiplayer has stopped because this action may
  have changed only part of the game state"), with the Wonder left half activated in the save.
- **Reproduced outside the game:** RuntimeChecks builds each bot's animator from its blueprint the way the game's Awake
  does, and calls the game's own `CharacterAnimator.SetBool("Piloting", true)` on the Folktails bot's: it throws
  `KeyNotFoundException`.
- **Fix** (`Factions/FactionCharacterPatches.cs:252-321`): a transpiler on `Pilot.PrepareForFlying` sends that one
  `SetBool` call through `FactionAnimation.SetBoolIfItHas`, which, in a mixed game only, leaves out a flag the
  animator lacks (logged once per scene); outside a mixed game it calls `SetBool` exactly as the game. The rest of the
  method stays the game's. A body without exactly one `SetBool` (a game update) is left as the game has it and logged,
  never thrown out of the mod's patching. The same guard covers the load path. A Folktails bot flies the plane in its
  current pose (display only). No wire or save change.
- **Checks:** `B1: a Folktails bot that pilots the Earth Repopulator's plane no longer throws inside the replayed
  activation` (`RuntimeChecks/RcFactionRuntimeChecks.cs`: the data premise, the game's SetBool throwing, the transpiler
  run on the game's decoded IL changing only that call, and the guard on both bots' animators); `B1: the pilot animation
  guard leaves an unexpected body as the game has it, logs, and never throws` (`StabilityTests/RcFactionChecks.cs`).
  The first fails on beta24's DLL ("no patch of Pilot.PrepareForFlying").
- **Test:** Script M8.

### B2 (H1): a faction catalog that could not be read threw inside ticks and replays (Plausible trigger; crash hardening; mixed)

- `FactionCatalog` builds its tables once, on first use, and on an error logs once and stays unbuilt
  (`FactionCatalog.cs` beta24 :182-197). Every accessor then dereferenced its null tables: `FactionOfTemplate`,
  `HasGood`, `GoodsOf`, `HasNeed`, `NeedsFor` threw `NullReferenceException`. Their callers run in ticks and replays: a
  character's needs (every birth and bot), a stockpile's goods (every construction site), a yield (gatherers), the
  host's placement judge, trading.
- No trigger is known with the game's own data (the sweep found nothing that makes `Build` throw); a game update or a
  faction mod's data could. So it is hardening, not a found crash.
- **Fix** (`FactionCatalog.cs:81-150, 189-209`): `EnsureBuilt` says whether the catalog could be read, and every
  accessor falls back to the game's own answer (no faction filter): no template faction, every good and need allowed,
  no goods filter (null; the four callers that filter now skip), the game's own need list. The game then plays on as an
  unfiltered two-faction game would (bots get both factions' needs), instead of stopping. The catalog tries again on
  each call only until the game has loaded (`PostLoad`); after that an unreadable catalog answers at once, instead of
  building, throwing and catching again on every call the game makes for each character, yield and building.
- **Left:** a mixed game's *load* still needs the catalog for the two game methods that take one template of a kind
  (`BotFactory.Load`, `ShaftFrameFactory.Load`: the game's `GetSingle` throws with both factions' templates). With an
  unreadable catalog the game would fail to load, before anyone plays: a clean failure, not a session stop.
- **Check:** `B2 (H1): a faction catalog that could not be read answers as the game would, and never throws`
  (RuntimeChecks: every accessor on a catalog whose spec service throws, and no further try after `PostLoad`). Fails
  on beta24 (`FactionOfTemplate threw NullReferenceException`).

### B3: the hot faction lookups (Confirmed; performance; mixed)

- **S1, `YieldRemovingBuilding.IsAllowed`** (`FactionBuildingRules.cs:79-104`). The game asks it for every yielder a
  gatherer's, lumberjack's or scavenger's range takes in when the range changes, and again for each yielder planted or
  grown in range (`Timberborn.Yielding: InRangeYielders.UpdateYielders`, `OnEntityInitialized`). The postfix worked out
  the building's faction for each call (two component lookups, a singleton lookup, a template lookup). Half the game's
  yielder templates give a **common** good (every tree's Log, every ruin's ScrapMetal, Berries, PineResin, Water:
  17 of 34), and trees and ruins are most of a late map's yielders. **Fix:** a common good returns at once
  (`FactionCatalog.IsCommonGood`, `FactionSets.InCommon`): the answer is the same by construction (`Has` is true for
  every faction when the good is common). Measured (the mod's `FactionSets` on the game's real collections, .NET 8,
  unoptimised): `Has` 20 ns, `InCommon` 11 ns; the call now costs about 40 ns for a tree or a ruin instead of about
  120 ns (estimate for the component and singleton lookups). No wire or save change.
- **S3, `MixedFactions.Spec`** (`MixedFactions.cs:158-176`): a closure and an ImmutableArray enumerator per call, for
  every beaver made (its fur), every path painted, every avatar shown. **Fix:** a plain loop; no allocation.
- **Checks:** `B3 (S1): the yield filter looks up the building's faction only for goods not every faction has, with the
  same answers` (RuntimeChecks: the order of the calls in the compiled postfix, and every yield in `Blueprints.zip`);
  `B3 (S1): a common good is every faction's, and only a common good skips the faction lookup` (StabilityTests);
  `B3 (S3): MixedFactions.Spec … makes no closure and no enumerator` (RuntimeChecks, IL). The two RuntimeChecks fail
  on beta24.

### B4 (F8): the display at scale (Confirmed; performance, display; mixed)

- **Paths** (`FactionModelPatches.cs:25-73, 211-230`). Each path of a mixed game was painted twice as it loaded: once
  in `DynamicPathModel.Awake`'s postfix, where a loading path's colony is not known yet (its stamp loads after Awake), so
  always in the base faction's materials, which the game had just applied itself; then again in
  `ColonyStamp.InitializeEntity`. Each paint searched the model's children twelve times by name
  (`FindChild(prefix + variant)`: a depth-first walk reading each child's `name`, which allocates, `Timberborn.Common`
  :1244-1260) and built twelve strings. On a save with 3,000 paths that is 6,000 paints, about 72,000 recursive
  searches and several hundred thousand name strings at load (estimate: a few hundred ms and some MB of garbage),
  and again for every path of a colony handed over. **Fix:** the pieces are taken from the model's own tables, as its
  Awake filled them (`_groundModels`, `_roofModels`; the objects the game painted, `Timberborn.PathSystem`
  :735-754): no search, no strings. And the Awake postfix leaves alone a path whose faction is the base faction (the
  game has just painted exactly that). A loading path is now painted once, and only if its colony is not the base
  faction's. Display only.
- **The Ctrl+T window's "untouched" test** (`ColonyFoundingService.cs:449-481`). Asked once a second while the window
  is open (`TradeOverviewPanel.cs:478, 486`), it walked every entity until it found a building of the colony's own
  faction; in a late game that can be thousands of trees and ruins first (about 0.5 to 1 ms a second at 20,000
  entities). **Fix:** the building that showed the colony touched is remembered and looked at first (still standing,
  still the colony's, still its faction's); the walk runs only while the answer may be yes. Display only: the switch's
  own judgement (`UntouchedFacts`, played in a replay on every computer) is unchanged and never reads the witness.
- **Checks:** `B4 (F8): a path is painted from its model's own pieces, and not again in the base faction the game has
  just used` (RuntimeChecks: the game's premise from its IL, the mod's IL); `B4 (F8): the untouched check looks first
  at the building that showed the colony touched, and forgets a deleted one` (StabilityTests, source). Both fail on
  beta24.

### B5 (V1): in co-op, activating a Wonder played no launch sound (Confirmed; display; everyone)

- The game plays the launch sound in `WonderFragment.ActivateWonder` (`Timberborn.WondersUI` :282-286). In co-op that
  method is recorded, not run (`ReplayEvent.DoPrefix` returns `EventIO.ShouldPlayPatchedEvents`, false on host
  (`QueuePlay`) and guest (`Send`)), and the replay calls `Wonder.Activate` only. Nobody heard it.
- **Fix** (`EntityUIEvents.cs:1298-1302`): after a replayed activation that happened, the computer of the player who
  acted plays the launch sound (`ColonySession.LocalPlayer`), in a try/catch. In a mixed game it is the selected
  Wonder's faction's, else the player's colony's (the existing `FactionWonderSoundPatcher`). Display only; no wire
  change.
- **Check:** `B5 (V1): the player who activates a Wonder hears its launch sound in co-op, after the activation, on their
  computer only` (RuntimeChecks). Fails on beta24.
- **Test:** Script M5.

### F6: the daily check in a mixed game (Confirmed gap; detection; mixed)

- The daily fingerprint hashed each character's faction and its number of needs into one number for the map
  (`chars:`, `ColonyDiagnostics.cs:182-189`); nothing per colony, and no check held the mixed line to it (beta21's B-4
  asked for one).
- **Would a character made with the wrong faction on one computer be caught before it changes anything?** Its needs
  would differ, so the needs it goes after and the draws they make would differ within a tick or two: the always-on
  random-state compare would stop the guest at the next heartbeat. The daily check is the backstop and names it.
- **Fix:** a mixed game's line also carries `/census:0=Folktails:31+4,1=IronTeeth:40+6` (each colony's beavers and bots
  by faction, from its districts' populations; `ColonyFactionService.Census`, `ColonyFactionService.cs:103-146`,
  called once a day, one line in `ColonyDiagnostics.cs:211-212`). Saved state only, the same on every computer; about
  600 component lookups a day. A game that is not mixed keeps its line byte for byte. **Wire:** the line is compared
  between computers; both sides run the same build (the handshake), so nothing older meets it.
- **Check:** `F6: a mixed game's daily colony check keeps chars: and names each colony's beavers and bots by faction`
  (StabilityTests: `/mixed:`, `/chars:`, `/census:` in the mixed branch only, the census reading saved state and
  nothing of this computer's). Fails on beta24's source.
- **Test:** Script M2.

### Refuted or found sound (the leads that turned out fine; evidence in §3)

- **F1 content:** every one of the 313 buildings of the two factions (156 Folktails, 157 Iron Teeth; the dev buildings
  aside) is listed by one faction alone, and is built, run, fuelled and supplied with its own faction's goods, and
  serves only its own faction's needs or common ones. Nothing in the late game (badwater rig, tubeways, bot part
  factories, charging, metal industries, pumps, monuments and decorations, explosives factories, the Wonders) is
  missing from the faction code: every place the game assumes one faction is covered (§3). Refuted as a source of
  crashes or starvation, except B1.
- **F2 creation sites:** pilots are not created (the Wonder's own workers); planes are not characters. Refuted.
- **F3 `BotFactory._botTemplate`:** read only in `Create`, set on every `Create` in a mixed game. Refuted.
- **V1 completion on a tick path:** profile and two display flags only, whichever colony completes. Refuted as a desync.

---

## 2. My rows of the coverage matrix

FT = Folktails, IT = Iron Teeth. "Scripts" are the steps in §5.

| Feature | Desync | Crash | Cross-colony | Mixed | Cost | Save/rehost |
|---|---|---|---|---|---|---|
| **FT Wonder: Earth Recultivator** (activation, effect, deactivation timer) | Sound: activation is the host's word (`WonderActivatedEvent.activated`, beta12); animation on the tick (`WonderTickService`, WonderChecks at 10/30/144 FPS) | Sound: replay null-safe (`GetComponent` by id); no pilots; per-Wonder try/catch in the tick | Left: its effect (radius 40, `RangedEffectSubject`) helps FT beavers of any colony in range (doc §5) | Sound: its need is FT's alone; an IT beaver in range is skipped (`NeedManager.ApplyEffect` → `TryGetNeed`), `V1` check | Sound: one ranged effect per Wonder, the game's own | Sound: animation pose saved at the tick's (`SaveWriterWonderPosePatcher`) |
| **IT Wonder: Earth Repopulator** (planes, catapult, launcher, pilots) | Sound: catapult, runway, launcher on the tick (beta12, WonderChecks bit for bit); planes spawn only in the tick | Fixed: B1, `B1: a Folktails bot that pilots…` (RuntimeChecks); plane `GetSingle<PlaneSpec>`: one template | Sound: pilots are the Wonder's own district's workers (`TeleportAndInitializePilots`); Left: effect range as above | Fixed: B1 (FT bot pilots); Sound: its need is IT's alone (`V1`); FT beaver pilots fly without the IT pilot helmet (display) | Sound: at most 8 planes per activation | Sound: pilots and planes saved by the game, poses at the tick's; Playtest: M11 (save mid-flight) |
| **Both Wonders in one game; the single completion** | Sound: `WonderCompletionCountdownStarter` is one tickable singleton, the same on every computer; the mod's completion prefix writes the profile and two display flags only (`V1` check) | Sound: completion prefix and panel patches read only the local slot's faction; the panel's labels exist (the game sets them first) | Left (D3): the first Wonder of any colony to deactivate completes the map for everyone | Left (D3): each player's profile records the map for their own colony's faction, whoever completed it | Sound: once per game | Sound: `CountdownFinished` saved by the game; `GameWonderCompletionRestorer` re-records only the profile |
| **Wonder launch sound** (display) | Sound: display only, after the replayed activation | Sound: in a try/catch | Fixed: B5, `B5 (V1)…`: the acting player's computer plays it (in co-op nobody's did) | Sound: the Wonder's faction's sound (`FactionWonderSoundPatcher`) | Sound: once per activation | Sound: nothing saved |
| **Wonder timing at speed 7 and a boost of 30** | Sound: per tick, not per frame (WonderChecks); speed changes only how many ticks a frame holds | Sound: per-Wonder try/catch | Sound | Sound: both Wonders ticked in entity-id order | Sound: two Wonders, a few method calls a tick | Playtest: M5 (one side at 15 FPS, speed 7) |
| **Wonder blockers, inventory, workers (V3)** | Sound: the game's, in the tick | Sound | Sound: district-scoped (`NotEnoughWorkersWonderBlocker`, `UnreachableBuildingWonderBlocker`, `WonderInventory` filled by its district's haulers, `WonderInputChecker` reads its district's inventories) | Sound: FT Wonder needs Extract + Paper (FT), IT's TreatedPlank + Berries; both within their faction (F1 check) | Sound | Sound |
| **FT bots** (BotAssembler, BotPartFactory, Refinery: Biofuel, Catalyst; PrintingPress: PunchCard) | Sound: template chosen by the assembler's faction (`BotManufactoryFactionPatcher`, `FactionBotFactoryCreatePatcher` every `Create`) | Sound: animations of every building FT bots work at or visit exist on their animator (`F1/F3` check); Fixed: B1 (pilot) | Sound: district-scoped workplaces and consumption | Sound: needs Biofuel, Catalyst, PunchCard only (`F3` check); never go to an IT charging station (no Energy need) | Sound: needs built once per bot (cached per faction) | Sound: bot template name saved by the game; the bot's faction is its template's (no `CharacterFaction` on bots, `ColonyConfigurator.cs:31`) |
| **IT bots** (BotAssembler, BotPartFactory, ChargingStation, GreaseFactory, ControlTower) | Sound: as FT | Sound: IT bots have no zipline flags; the game subscribes to zipline events only if the animator has them (`Timberborn.ZiplineMovementSystem` :273) | Left: the ControlTower's range helps IT bots of any colony in range (doc) | Sound: needs Energy, Grease, ControlTower only (`F3`); Charging slot plays on IT bots only (`F1/F3`) | Sound | Sound |
| **Bot parts traded between factions** | Sound: shared goods (17), C's T2 | Sound | Sound: through a Trading Post only | Sound: an assembler of either faction takes either faction's parts and makes its own faction's bot | Sound | Sound |
| **FT births (lodges) and growing up** | Sound: faction from the spawner (`NewbornSpawnerFactionPatcher`, both methods, `TargetMethods`); a child's from itself (`BeaverGrowUpFactionPatcher`) | Sound: null-safe (`SimFactionOf(null)` → base) | Sound: a lodge's own district | Sound: a lodge of FT makes FT beavers, whoever owns it (D13) | Sound: one dictionary lookup per birth | Sound: `CharacterFaction` saved, read before Awake (`WorldEntitiesLoaderFactionPatcher`) |
| **IT births (breeding pods, advanced pods)** | Sound: as FT (`Timberborn.Reproduction` :414-420, `SpawnAdult`/`SpawnChild(_building)`) | Sound | Sound | Sound: a pod makes its own faction's beavers | Sound | Sound |
| **Ziplines (FT only)** | Sound: links judged by the host (`ColonyRoadNetworks.HostAllowsZipline`) | Sound: IT bots (after a handover) ride without the zipline pose (guarded by the game) | Sound: colony-level link rule | Sound: IT beavers share the beaver template (harness and flags) | Sound | Sound |
| **Tubeways (IT only)** | Sound: faction-neutral components decorated on every character (`Timberborn.TubeSystem`); visitors change only lighting | Sound | Sound: paths for the road rule (TWO-COLONIES) | Sound: FT colonies can't place them (D17); FT characters in a received district use them | Sound | Sound |
| **Badwater rig (FT), badwater pressurizer and deep pumps (IT), each faction's pumps** | Sound: the game's water; A's W-leads | Sound | Left: water is shared (documented) | Sound: costs and recipes within their faction's goods (Water, Badwater common; MetalPart IT) (`F1`) | Sound | Sound |
| **Metal industries** (FT Mine, both Smelters, IT EfficientMine and Metalsmith) | Sound | Sound | Sound: district-scoped | Sound: ScrapMetal and MetalBlock common; MetalPart IT only, used only by IT buildings (`F1`) | Sound | Sound |
| **Explosives factories** (both) | Sound | Sound | Left: blasts reach the other colony (D3, A's X2) | Sound: Badwater → Explosives, Fireworks, all common | Sound | Sound |
| **FT monuments and decorations** (FarmerMonument, FountainOfJoy, Agora, …) | Sound: the game's ranged effects in the tick | Sound: a need a character lacks is skipped | Left: help FT beavers of any colony in range (doc) | Sound: FT or common needs only (`F1`) | Sound | Sound |
| **IT monuments and decorations** (LaborerMonument, FlameOfUnity, TributeToIngenuity, …) | Sound | Sound | Left: as FT | Sound: IT or common needs only (`F1`) | Sound | Sound |
| **Beehive (FT)** | Sound: sting draws are per character in population order, the same everywhere | Sound: `HasNeed(BeeSting)` first (`Timberborn.NeedApplication` :337); B-1 (beta21) fixed the deleted-beaver path | Left: stings FT beavers of any colony and speeds up any crop in range (doc) | Sound: IT beavers are never stung (no BeeSting need); IT crops are pollinated (doc) | Sound | Sound |
| **Stockpiles, planters, yield removers late** (Aquatic farmhouse FT, Hydroponic garden IT, Efficient farmhouse FT, Tapper's shack both) | Sound: D18 keyed by template (beta21 M8) | Sound: B2 fallback | Sound | Sound: IT tapper leaves Maple (MapleSyrup is FT's) | Fixed: B3, `B3 (S1)…` | Sound |
| **Science late** (FT Observatory, IT Numbercruncher) | Sound: per-colony science (A's) | Sound | Sound | Sound: recipes of their own faction (`F1`) | Sound | Sound |
| **Decontamination** (IT pod, FT Herbalist's Antidote) | Sound | Sound: FloatingSleeping on the shared beaver template | Sound: district-scoped attractions | Sound: BadwaterContamination is common | Sound | Sound |
| **Power shafts and engines of both factions** | Sound: models only | Sound: shaft models built at load, in a catch (`FactionModels.BuildOtherShaftModels`) | Left (D3): networks may join across colonies (A's P1) | Sound: each shaft its own faction's model | Sound: one extra model set at load | Sound |
| **Paths and gates in each colony's faction** | Sound: display only | Sound: paint in a try/catch | Sound | Sound | Fixed: B4, `B4 (F8): a path is painted…` | Sound: repainted at load from the stamp |
| **The Ctrl+T faction switch buttons (IsUntouched)** | Sound: display only; the switch's own judgement unchanged | Sound | Sound | Sound | Fixed: B4, `B4 (F8): the untouched check…` | Sound |
| **Handover across factions** (host's Ctrl+T, abandoned colony) | Sound: `PreferSameFaction` pure, saved state (FactionChecks) | Fixed: B1 (FT bots flying an IT Wonder) | Sound: D21 | Left (D21): the receiver builds only its own faction's buildings; received bots may lack Biofuel or Energy (doc) | Sound | Sound: entities keep their faction |
| **Characters at scale (needs, wellbeing)** | Sound: each character's needs from its faction, built once in Awake | Sound: B2 fallback | Sound | Sound: the same need list vanilla gives that faction | Sound: per-character need count equals a one-faction game's; no per-tick faction patch per character (B6 by reading) | Sound |
| **Character creation sites** (births, growing up, bots, dev, founding, switch; pilots) | Sound: every site pushes its faction (M2); pilots and planes create no character (`V2` check) | Sound | Sound | Sound | Sound | Sound |
| **The daily check in a mixed game (F6)** | Fixed: F6, `F6: a mixed game's daily colony check…` | Sound: in the check's try/catch | Sound | Sound | Sound: about 600 lookups a day | Sound: saved state only |
| **Faction catalog failure (H1)** | Sound: the same data on every computer | Fixed: B2, `B2 (H1)…` | Sound | Sound | Sound | Left: a mixed load with an unreadable catalog fails at load (no known trigger) |
| **Loading both factions (F7)** | Sound | Sound | Sound | Sound | Playtest: P4 (B6: load time and memory within 25 %) | Playtest: P4 |
| **A mixed save's life (F9)** | Sound: faction table and per-beaver faction saved | Sound: `SaveColonyReader` returns null on a bad save | Sound | Sound | Sound: save room reader 13–27 ms on the reference saves (.NET 8); `CharacterFaction` adds about 50 bytes per beaver | Playtest: M11 |

---

## 3. Found sound (don't redo)

**Sweep 4, the content (`Blueprints.zip`, 1,067 entries), now kept as RuntimeChecks (`F1`, `F1/F3`, `F3`, `V1`):**
- Every building template of either faction's collections (313, the two dev buildings aside) is that faction's alone
  (`TemplateSpec` ends `.Folktails` / `.IronTeeth`), including every late-game one: `BadwaterRig.Folktails`,
  `Tubeway`/`TubewayStation`/`VerticalTubeway`/`ImpermeableTubeway.IronTeeth`, both `BotPartFactory`,
  `ChargingStation.IronTeeth`, `Metalsmith.IronTeeth`, `EfficientMine.IronTeeth`, `Mine.Folktails`, both `Smelter`,
  FT `WaterPump`/`LargeWaterPump`/`BadwaterPump`/`MechanicalPump`, IT `DeepWaterPump`/`DeepBadwaterPump`/
  `DeepMechanicalPump`, both `CompactMechanicalPump`, both `ExplosivesFactory`, both Wonders, every monument and
  decoration. So D17 (the host's `JudgeFactions`, `ColonyRulesService.cs:258-270`) covers all of them, and
  `BuildingPlacedEvent` is the only way a building is placed.
- Every building's costs, every recipe's ingredients, products and fuel, and each Wonder's required goods are its own
  faction's (common or own): no building needs a good its colony can't store (0 exceptions).
- Every attraction, ranged effect, Wonder effect and area need a building serves is its own faction's or common.
- The only animation flags one bot has and the other lacks: FT lacks `Piloting`, `Charging`, `NoEnergy`; IT lacks
  `NoFuel`, `Zipline*`. `NoFuel`/`NoEnergy` come from each bot's own critical-need spec; `Zipline*` is guarded by the
  game; `Charging` is played only on a bot that needs Energy; `Piloting` was B1. No workplace that takes bots plays a
  flag either bot lacks. Beavers share one template (no faction difference).
- Bots: FT Biofuel, Catalyst, PunchCard; IT Energy, Grease, ControlTower, exactly vanilla's (`F3`). Bot parts
  (BotChassis, BotHead, BotLimb) are in both factions' collections; bot fuels and boosts are one faction's.

**Every place the game assumes one faction (1.1.2.4), and who handles it:**
- `FactionService.Current`: `BeaverTextureSetter` (patched), `BeaverEntityBadge`/`BotEntityBadge` avatars (patched),
  the beaver selection sound (both factions' `SoundId` is `Common`), `CharacterButton` empty slots (patched),
  `DecalService.Load` (patched), `FactionGoalsSystem` and `Achievements` (D25), the four collection providers (the
  mod's `OtherFactionCollections`), `GameOverBox` (patched), `PlayWonderLaunchSound` (patched), the starting building
  (founding uses the faction's), `GameWonderCompletionService` (patched), `WonderCompletionPanel` (patched), the
  Wonder dev module (dev), `DrivewayModelInstantiator` and `DynamicPathModel` (patched), tutorials (off),
  `BasicStatisticsPanelFactory` (patched), the unlock-progress display (UI), `WorkerOutfitService.Load` (lookups
  patched).
- `GetSingle<>`: `BeaverFactory` (de-dup), `BlockOccupierSpec`, `BotFactory` (patched), both `ModularShafts` (patched),
  `RecoveredGoodStackSpec`, `PlaneSpec` (one, Planes.IronTeeth).
- `FactionNeedService`: `NeedManager.GetNeeds` (patched), `WellbeingLimitService` (patched, display), `Attraction`
  (union, fine), `GetBeaverOrBotNeedById` (ids are unique in the union).

**Wonders:**
- Pilots are the Wonder's own assigned workers (`PlaneLauncher.TeleportAndInitializePilots`), killed then deleted
  (`DestroyPilots` → `Character.DestroyCharacter`), never created; `PlaneSpawner` makes planes only; nothing in
  `Timberborn.WonderPlanes` calls `BeaverFactory` or `BotFactory` (`V2` check). M2's creation-site list stays complete.
- A Wonder's effect reaches any character in range (`RangedEffectSubject.ApplyEffects`, position-based); a character
  without the need is skipped (`NeedManager.ApplyEffect` → `TryGetNeed`).
- The completion countdown is one tickable singleton; the mod's prefix writes only `WonderCompletionService` (player
  data) and `WasCompletedFirstTime*` (the panel's display) (`V1` check). After completion the game only unblocks game
  over (`GameOverDisabler`, the same on every computer). The completion panel is an overlay: with *Pause reduction* off,
  the host's game waits until it is closed (the generic co-op behaviour for overlays).
- In co-op the activation never ran the game's `ActivateWonder` (B5).
- `WonderTickService` ticks both Wonders in entity-id order; `WonderParticleController` saves frame time
  (`Time.time`) into the save: particles only, never read by the simulation.

**Mixed factions elsewhere:**
- `BotFactory._botTemplate` is read only in `Create` and set by the mod on every `Create` in a mixed game (the dev
  tool's one-argument `Create` calls the patched three-argument one).
- The worker outfit patch returns false for an outfit the worker's faction lacks (a FT beaver or bot as a pilot): the
  game then keeps no outfit (`WorkerOutfitAttachmentVisualizer.OnGotEmployed`).
- `CharacterFaction` is on beavers only; a bot's faction is its template's.
- A handover's colony choice is pure and saved state (`FactionRules.PreferSameFaction`).
- The save room's reader, measured on the reference saves (.NET 8, the mod's own `SaveColonyReader`): R-late 19 ms
  (8.3 MB world, shared save: reads every singleton), R-blast 13 ms, R-long 27 ms (separate colonies). A few times that
  under Mono, once per click on a save's room: C-E6's decision stands.
- **F7, estimated from the code:** a mixed game loads the other faction's 34 materials, its tool buttons and icons, a
  second set of power-shaft models and both bot instances; building models load when an entity of that template is
  made (`PreviewFactory` caches on use), so a late mixed game holds both factions' models because both colonies build
  them. Faction work at load: the catalog once, the template de-dup once, and one `EntityLoader` per saved entity to
  read a beaver's faction before Awake (about 20,000 small allocations on a large save). The 25 % budget is likely
  met; P4 measures it.

**H1, sweep 7 for my area:** every entry point below was read against the plan's H1 states (an entity deleted earlier
in the tick, a component missing because the building is the other faction's, unfinished or a map object, a
district or colony gone, a character with no `CharacterFaction`, an older save, a singleton missing outside the Game
scene):
- Load: `MixedFactionsDecidePatcher` (catch-all), blueprint modifiers, template de-dup, the four providers,
  `WorldEntitiesLoaderFactionPatcher` (push/pop in a finalizer), `FactionBotFactoryLoadPatcher`, the shaft patches
  (models built in a catch), `FactionDecalServicePatcher`, tutorials, `ColonyFactionService.Load/PostLoad`,
  `FactionCatalog.Load` (unlock read in a catch), `CharacterFaction.Load`: sound.
- Entity creation (in ticks, replays and loads): `FactionNeedsPatcher` (Fixed B2), `FactionBeaverTexturePatcher` (one
  draw; null spec → the game's), `FactionStockpilePatcher`/`GoodsPatcher` (Fixed B2: null filter skipped),
  `FactionPlanterPatcher`, `FactionShaftUpdaterPatcher`, `FactionPathModelPatcher` (try/catch), `FactionDrivewayPatcher`,
  `FactionDecalDefaultPatcher`, `FactionResourceCounterPatcher` (Fixed B2): sound.
- Ticks: `NewbornSpawnerFactionPatcher`, `BeaverGrowUpFactionPatcher`, `BotManufactoryFactionPatcher`,
  `FactionBotFactoryCreatePatcher`, `FactionWorkerOutfitPatcher`, `FactionYieldRemoverPatcher` (Fixed B2),
  `FactionWonderCompletionPatcher`, `FactionWonderPanelPatcher` and `FactionGameOverPatcher` (UI inside the tick's
  event), `WonderTickService.Tick` (per-Wonder try/catch; deleted Wonders are unregistered), the Wonder frame gates,
  `ColonyFactionService.Census` (Fixed F6; slot −1 skipped): sound.
- Replays: `WonderActivatedEvent` (null-safe; Fixed B1 and B5), `ColonyFactionSwitchEvent` (founding service missing
  → logged; judged again from saved state), `FoundColonyEvent`'s faction parts, `FactionToolbar.Refresh` (catch):
  sound.
- Patch time: the Wonder transpilers and the new pilot transpiler never throw (they leave an unexpected body alone).
- Not throwing on game data we have, left as the game's: `BlueprintModifiers` of a faction mod that omits the list
  (the game's own `Initialize` has the same premise).

---

## 4. Left, and why

- **The single Wonder completion and its credit (D3):** the first colony's Wonder completes the map for everyone, and
  each player's profile records the map for their own colony's faction. The game's behaviour; documented (§5).
- **Ranged effects, the Beehive and the ControlTower reach the other colony:** by position, like water. A colony's
  decorations, monuments and Wonder help the other colony's beavers of their faction in range; a Beehive stings
  Folktails beavers of any colony and speeds up any crop in range. Refusing it would need a range rule the game doesn't
  have; documented.
- **A colony handed to the other faction (D21):** its beavers, bots and buildings keep their faction, and its new owner
  builds only their own faction's buildings, so the received bots may run out of Biofuel or Energy and the received
  beavers of their own foods. Documented.
- **Folktails bots and beavers as Earth Repopulator pilots** fly without the piloting pose or the Iron Teeth pilot
  helmet (display).
- **B2 at load:** an unreadable catalog still stops a mixed game's load at `BotFactory.Load` / `ShaftFrameFactory.Load`
  (the game's own `GetSingle`), before anyone plays. No known trigger; a fallback there would pick one faction's bot
  and shafts for both.
- **F7 memory and load time:** only a recording can show them (Script P, P4).
- **The Wonder completion panel pauses the host's game until it is closed** (the generic co-op overlay rule, *Pause
  reduction* off): not specific to Wonders.
- **A colony that has lost every building of its own faction is untouched again** (`UntouchedFacts` counts buildings,
  exchanges and centers only) and its own player may switch it; the switch remakes beavers only up to the starting
  numbers and leaves any bots of the old faction. Only by that player's confirmed choice; a late colony reaches it only
  by demolishing or blasting everything it built. Worth a line in the switch's tooltip if it ever matters.

---

## 5. Doc text and playtest steps

**TWO-COLONIES, *Known limits*** (proposed, after the mixed-factions lines):

> - **Wonders:** a Wonder's effect helps its own faction's beavers within its range, whichever colony they belong to;
>   the other faction's beavers get nothing from it. The first Wonder of any colony to finish completes the map for
>   everyone, and each player's profile records the map for their own colony's faction.
> - Decorations, monuments, a Wonder and the Iron Teeth Control Tower help the other colony's beavers or bots of their
>   faction in range; a Folktails Beehive stings Folktails beavers of any colony and speeds up any crop in range.
> - A colony handed to a player of the other faction keeps its beavers, bots and buildings, but its new owner can only
>   build their own faction's buildings: the received bots need Biofuel (Folktails) or Energy (Iron Teeth) and the
>   received beavers their own foods, which only the buildings they came with make. A Folktails bot can fly an Iron
>   Teeth Earth Repopulator's plane; it does so without the piloting pose.

**Changelog (1.4.0-rc1), proposed:**
> - Mixed factions: a Folktails bot flying an Iron Teeth Earth Repopulator's plane (after a colony was handed over)
>   stopped the session for everyone; it now flies.
> - In co-op, activating a Wonder plays its launch sound again, for the player who activated it.
> - Mixed factions: the daily colony check names each colony's beavers and bots by faction; paths are painted in their
>   colony's faction with far less work as a game loads; the Trading Posts window no longer walks every building each
>   second in a late game.

**Script M, mixed factions in the late game (two players, about 90 minutes).** Send both players' `Player.log` and
Ctrl+Shift+J reports after each part.

- **M1 (setup, hosted alone with dev mode, TWO-COLONIES *Testing alone*):** a new game with *Separate colonies* and
  *Mixed factions* on, Folktails; three colonies: 1 Folktails (the host's), 2 Iron Teeth (the guest's), 3 Folktails
  (nobody's). With dev mode, for colonies 1 and 2: bot assembler, bot part factory, 6+ bots; Folktails a refinery
  (Biofuel, Catalyst) and printing press (PunchCard); Iron Teeth two charging stations, a grease factory and a control
  tower; lodges (FT) and breeding pods (IT) with beavers; ziplines (FT) and tubeways (IT); a badwater rig (FT) and a
  deep badwater pump (IT); an explosives factory; the metal buildings; three monuments or decorations each; a Beehive
  beside some Folktails and Iron Teeth crops; a Trading Post between them; the Earth Recultivator (1) finished, filled
  and staffed; the Earth Repopulator (2) finished, filled, set to bots, its eight places taken by Iron Teeth bots.
  Colony 3: a bot assembler and 8 Folktails bots. Then, still alone, Ctrl+T → hand colony 3 over to colony 2, and
  (acting as colony 2) build a second Earth Repopulator in colony 3's old district, finished, filled, set to bots, its
  eight places taken by the Folktails bots. Save. Host; the guest joins as colony 2.
- **M2 (F6):** play two in-game days. Each player's daily `[Colony] Check day N …` line ends in
  `/census:0=Folktails:A+B,1=IronTeeth:C+D`, the same on both, and A to D match each colony's beavers and bots in its
  population panel.
- **M3 (births, F2):** wait for a lodge birth, a breeding-pod birth and a child growing up. Each new beaver has its
  colony's faction's fur and avatar. Neither log has *was made with no faction in hand*.
- **M4 (bots, F3):** each assembler makes its own faction's bot (model, name, avatar). Iron Teeth bots charge at the
  charging stations; Folktails bots never go there. No bot of either colony shows a need of the other faction.
- **M5 (V1, V2, B5):** the host activates the Earth Recultivator, the guest the Earth Repopulator; the guest caps their
  frame rate at 15 and both play at speed 7 while the planes launch. The activating player hears the launch sound, the
  other doesn't. All planes launch; the pilots are gone half an hour later; no desync.
- **M6 (V1 effect):** while the Recultivator is active, a Folktails beaver near it shows the Earth Recultivator need
  rising; an Iron Teeth beaver walked near it does not.
- **M7 (completion):** half an hour after the first Wonder switches off, both players see the completion screen, each
  with their own faction's picture and words; the game waits for the host to close it; the next daily check is equal.
- **M8 (B1, the handover across factions):** the guest activates the second Earth Repopulator, the one in colony 3's
  old district with Folktails bots assigned. The planes launch with Folktails bots aboard (in their ordinary pose);
  **no "Multiplayer has stopped" message** (1.4.0-beta24 stopped here for everyone); both logs have
  `[Factions] A character without the "Piloting" animation does it without`. Save and reload: no error. The daily
  check's census shows colony 2 with Folktails bots (`1=Folktails:…+8`) beside its Iron Teeth beavers and bots.
- **M9 (F5):** the Beehive stings only Folktails beavers; the Iron Teeth crops beside it grow faster too (documented).
- **M10 (F8):** in the late game, keep Ctrl+T open for two minutes: no hitch each second (compare frame times with it
  closed in the Ctrl+Shift+J report).
- **M11 (F9, save and rehost):** save while a plane is on the runway, rehost, the guest joins: the plane and its pilot
  carry on; the census and `chars:` of the next daily check are equal on both.

**Script P, P4 (B6):** the late mixed game from M1 against a one-faction game of the same size: tick time (within 5 %),
load time and memory (within 25 %).
