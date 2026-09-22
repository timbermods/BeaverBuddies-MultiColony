# Mixed factions: Folktails and Iron Teeth colonies in one game — investigation, design and implementation plan

This is a handoff document for the session that builds the feature. It was written on 2026-09-22 from a full
investigation of the decompiled game (Timberborn **1.1.2.4**, all 497 `Timberborn.*.dll`) and of this repository at
**`bf2264c` (1.4.0-beta14, `main` = `trading-exchange`)**. Read it to the end before writing code.

Labels used below:

- **(read)**: checked against the decompiled game or the repo at `bf2264c`. Trust it, but a line number may have moved.
- **(decision)**: decided by the maintainer (D1 to D6) or by this plan (D7 onward). Do not re-open it; if it proves
  impossible, stop and say why.
- **(verify)**: a belief not fully checked. Settle it in Phase 0 or at the step that needs it, before building on it.

Game references are `Assembly: Class.Member` (for example `Timberborn.Bots: BotFactory.Load`). Decompile what you need
with `ilspycmd "<Managed>/Timberborn.X.dll" -r "<Managed>" -o <scratch>/dec` (Managed is
`C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\Managed`). Read game blueprints straight
from `Timberborn_Data/StreamingAssets/Modding/Blueprints.zip` with Python's `zipfile`: extracting into the scratchpad
hits Windows MAX_PATH.

---

## 1. Goal and exit criteria

In a separate-colonies game, each colony plays its own faction: one player runs Folktails, another Iron Teeth, on
the same map, trading through Trading Posts. Everything a player sees and does for their colony is their faction's:
toolbar, beavers and bots (looks, needs, food), buildings, paths, storage, goods lists. The game's simulation stays
identical on every computer.

Done means all of this holds:

1. A new game made with **Mixed factions** on loads both factions and runs with no exception in `Player.log`. That
   covers building every building of both factions, bots of both factions, births, children growing up, saving and
   loading.
2. The host's colony is the faction picked in the game's New Game panel. Every other colony picks its faction when
   it is founded. A multi-start colony starts in the host's faction and may switch while it is untouched.
3. Each beaver and bot has exactly its own faction's needs, looks and outfits. Each building's model is its own
   faction's.
4. The toolbar shows only the local colony's faction's buildings and crops, plus the common ones. It follows a
   steward switch, a handover, a founding and a faction switch.
5. Trading follows D2 and D3. Science still trades.
6. A game without Mixed factions behaves exactly as `bf2264c` does: every new patch returns at once, and the checks
   prove the gate.
7. StabilityTests and RuntimeChecks pass, both builds have 0 warnings, and the docs, changelog and site describe
   the feature honestly as **not yet played**.

---

## 2. Decisions

### From the maintainer (2026-09-22)

- **D1: The host's unlocks count.** Iron Teeth may be chosen only if Iron Teeth is unlocked on the hosting computer's
  profile (the game's `FactionUnlockingService`). A guest's own profile does not matter. When a different player hosts
  the save later, their profile counts from then on.
- **D2: No beaver trades between factions.** Beavers never cross a Trading Post between colonies of different
  factions. Same-faction colonies trade beavers as today.
- **D3: Goods only where the receiver can store them.** A good may go to a colony only if that colony's faction has a
  warehouse, pile or tank setting for it. **(read)** Both factions have all three storage types, and every good is
  Box, Pileable or Liquid. So the rule is exactly: *the good is in `GoodCollection.Common` or in the receiving
  faction's `GoodCollection`*. Between Folktails and Iron Teeth that leaves these 17 goods:
  - Box: Gear, PineResin, Explosives, Fireworks, BotChassis, BotLimb, BotHead, Berries.
  - Pileable: Log, Plank, TreatedPlank, ScrapMetal, MetalBlock, Dirt.
  - Liquid: Water, Badwater, Extract.

  Faction-only food never crosses. Science still trades (with separate science on).
- **D4: Shared-colony games stay single faction.** Mixed factions needs separate colonies. A shared save split by
  founding stays one faction.
- **D5: Borders are being abandoned.** Colony land and territory (the `ColonyReach` land grid, "build on your land",
  the 20-tile founding distance) are being removed in parallel work. **Nothing in this plan may depend on land.**
  Ownership comes from districts, the ownership stamps on buildings (`DistrictOwner`, `ColonyStamp`) and the mark
  tables. If the border removal lands before you finish, rebase on it; where this plan says "the colony that owns it",
  use whatever ownership API survives (today `DistrictOwner.OwnerOf` / `ColonySeparation.SimOwnerOf`).
- **D6: Trading Posts remain the only way two colonies' roads join.**

### Made by this plan

- **D7: One new host setting.** *Mixed factions for new games (beta)*, off by default. It is read only when a new game
  is made, like *Separate colonies for new games*. It takes effect only if separate colonies is also on **and** every
  faction is unlocked on the host's profile. Otherwise the game is a normal one-faction game and a notice says why. A
  save keeps its mode for life.
- **D8: A mixed game loads every faction.** It loads the union of all factions' template, good, need and material
  collections and blueprint modifiers, de-duplicated. `FactionService.Current` stays the **base faction**: the host's
  New Game choice, saved by the game as usual. Anything not routed per colony keeps using it.
- **D9: Each colony (slot) has a faction.** It is saved per slot. It is set when the colony starts (slot 0: base
  faction; multi-start slots: base faction; a founding: the founder's choice) and by the untouched-colony switch
  (D12). A colony that dies and is founded again takes its new choice.
- **D10: Every entity carries its own faction, independent of who owns it now.**
  - A building's faction is its template's: the faction whose template collections list its blueprint. Templates
    listed by no faction, or by several, are **common**: `Path`, `DevPowerGenerator`, `DevWaterSource`, the map
    objects.
  - A bot's faction is its template's (`Bot.Folktails` / `Bot.IronTeeth`).
  - A beaver's faction is stored on it by a new saved component, `CharacterFaction`. It is set at creation from
    whoever created it (D11) and never changes.
  - For display only, a common building (a path) takes the faction of the colony that owns it, else the base faction.
- **D11: Where a new character's faction comes from.**
  - Born in a lodge or breeding pod: the spawner building's faction.
  - Assembled: the bot assembler's faction, which also picks the bot template.
  - A child growing up: the child's faction.
  - Starting beavers of a new game and of every multi-start start: the base faction.
  - Founding beavers: the founding's faction.
  - Dev-mode spawns: the local colony's faction.
  - Any other creation: the base faction, with one warning in the log.
- **D12: An untouched colony may switch faction.** Untouched means it has only its district center(s): no other
  building, construction site or path stamped with its slot, no planting or cutting marks, no offer or exchange open,
  and no unlocks (with separate science). The switch is a synced action by that colony's own seated player (not a
  steward, not the host for someone else), allowed from the first tick. On every computer it replaces the district
  center with the other faction's, in place: the two are the same 3×3×5 footprint with the same entrance **(read)**.
  It also moves the stock across and replaces the beavers with the new faction's (as many adults and children as it
  had, up to the starting numbers). This is how multi-start guests pick their faction. It is also a safety net for a
  founder who picked wrong.
- **D13: The toolbar shows the local colony's faction plus common tools.** "Local colony" is `ColonySession.LocalSlot`,
  which includes a steward's acting colony. Dev mode shows everything, as the game does.
- **D14: A colony may place only its own faction's buildings and common ones.** The host refuses others. Exception:
  Trading Post halves, whose faction the host assigns (D17). Foundings and faction switches are host-authored.
- **D15: Buildings work only with their own faction's things.** This is a simulation rule, keyed by the building's
  template, so it is identical on every computer:
  - A stockpile holds only goods its faction can store.
  - A farmhouse or forester plants only its own faction's plantables and common ones.
  - A gatherer, lumberjack or other yield remover takes only yields whose good its faction uses.
- **D16: Needs are per character, not the union.** This is required, not cosmetic. **(read)** The Folktails bot need
  `Biofuel` and the Iron Teeth bot need `Energy` are both critical, stop work and carry a −3 wellbeing penalty. A
  union would starve every bot. For beavers, per-character needs also keep need updates as cheap as vanilla.
  **(read)** They also mean a beaver never eats the other faction's food: appraisal returns 0 for any effect on a need
  the beaver lacks. Only Berries and Water are shared foods.
- **D17: Each Trading Post half takes the faction of the colony whose road reaches it.** The host decides it when it
  judges the two halves together, and writes it into that half's placement event. If no colony's road reaches a
  half's entrance, the half takes the placer's faction. Each colony then sees its own faction's half.
- **D18: A handover prefers the nearest living colony of the same faction**, then falls back to the nearest one as
  today. Entities keep their own faction after a handover (D10). The receiving colony keeps its own faction for its
  toolbar and new buildings. It can still run the other faction's buildings and beavers it received.
- **D19: A beaver crossing a Trading Post must match the target colony's faction.** This applies even between
  same-faction colonies: a colony that received the other faction's beavers in a handover can't pass them on.
- **D20: Display follows the local colony's faction.** This covers wellbeing panels, avatars in empty slots, the
  game-over text, Wonder completion screens and the good-statistics tab. Tutorials are off in mixed games: they are
  Folktails-only and name Folktails buildings.
- **D21: Left as the base faction.** Achievements, faction-unlock goals and the save's thumbnail and faction icon in
  the load menu. See §11, Known limits.
- **D22: Off means unchanged.** Every patch and hook added here returns immediately unless `MixedFactions.IsOn`. Any
  existing code path whose behaviour changes when the mode is off is a bug. StabilityTests enforce this with a source
  scan (§9).

---

## 3. Facts the design rests on

### 3.1 How the game ties a save to one faction (read)

- `Timberborn.GameFactionSystem: FactionService` has `Current` (a `FactionSpec`), saved under singleton
  `FactionService` / `Id`.
  - `Load()` reads the save. For a new game it reads `GameSceneParameters.NewGameConfiguration.FactionId`.
  - It then calls `SetCurrentFaction`, which calls
    `FactionBlueprintModifierProvider.Initialize(Current.BlueprintModifiers)`. That call asserts it runs only once.
    Both factions' `BlueprintModifiers` are empty in vanilla.
- Faction-scoped collections reach the game through multi-bound providers, each unioned with a Common provider. The
  faction providers read `FactionService.Current`:
  - `FactionTemplateCollectionIdProvider` → `TemplateCollectionService.Load`, which concatenates with **no de-dup**.
  - `FactionGoodCollectionIdsProvider` → `GameGoodFilter.Load`, a set union.
  - `FactionNeedCollectionIdsProvider` → `FactionNeedService.Load`, a set union.
  - `FactionMaterialCollectionIdsProvider` → `MaterialRepository.Load`, which uses `Distinct()` and throws on a
    duplicate material name.
- Singleton lifecycle (`Timberborn.SingletonSystem: SingletonLifecycleService.LoadAll`):
  1. every `ILoadableSingleton.Load` in repository order;
  2. entities (`WorldEntitiesLoader.LoadNonSingletons`);
  3. `PostLoad`.

  `FactionService.Load` necessarily runs before the collection loads, because they call `Current` (vanilla would
  crash otherwise).
- Faction files: `Factions/Faction.Folktails.blueprint.json` and `Faction.IronTeeth.blueprint.json`.
  - Iron Teeth carries `UnlockableFactionSpec { PrerequisiteFaction: Folktails, AverageWellbeingToUnlock: 15 }`.
  - Unlocks are per player profile: `FactionUnlockingService.IsLocked(spec)`, bound in MainMenu and in Game.
- The new game starts through `Timberborn.GameSceneLoading: GameSceneLoader.StartNewGame(NewGameConfiguration)`
  (MainMenu).

### 3.2 What loading both factions does (read)

- **Shared template blueprint paths** across the two factions: `Characters/Beaver/BeaverAdult`,
  `Characters/Beaver/BeaverChild`, `Buildings/Power/DevPowerGenerator/DevPowerGenerator`,
  `Buildings/Water/DevWaterSource/DevWaterSource`. Every other building template name ends `.Folktails` /
  `.IronTeeth`. `Path` is a common template, and its old save names are `Path.Folktails` / `Path.IronTeeth`.
- **Recipes:** 55, no duplicate ids.
- **Materials:** no two paths share a material name across the union. `MaterialRepository` is safe.
- **Goods:** `GoodOrder` is unique over all 60 goods, so `GoodService.Goods` has the same order on every computer.
- **Needs:** faction beaver needs are bonus-only, except Folktails `BeeSting` (−1, hidden unless stung). Bot needs:
  - Folktails: `Biofuel` (critical, start 0.8), `Catalyst`, `PunchCard`.
  - Iron Teeth: `Energy` (critical, start 0.5), `Grease`, `ControlTower`.
- **Crash sites with both loaded:**
  - `Timberborn.TemplateSystem: TemplateNameMapper.Load` throws "Duplicate template name" when not de-duplicated.
  - `Timberborn.Beavers: BeaverFactory.Load` calls `GetSingle<AdultSpec>` / `GetSingle<ChildSpec>`, which throw
    without de-dup.
  - `Timberborn.Bots: BotFactory.Load` calls `GetSingle<BotSpec>`, which throws with two bot templates.
  - `Timberborn.ModularShafts: ShaftFrameFactory.Load` and `ShaftModelFactory.Load` call
    `GetSingle<ModularShaftPartsSpec>`, which throws.
  - `Timberborn.PlantingUI: …GetPlanterBuildingName` does `.Single()` over planters of a resource group, and
    Farmhouse and Forester have one per faction. It throws when the bottom bar is built.
  - `Timberborn.WorkerOutfitSystem: WorkerOutfitService` keys by `(Id, WorkerType)`, filtered by `Current`.
    Unfiltered, `ToDictionary` throws. Filtered, a bot of the other faction gets a hat attachment its template lacks,
    and `GetAttachmentDefinition` throws `KeyNotFoundException`.
  - Needs as a union: every bot starves (see above).
- **Per-entity `Current` uses and what is in hand at each:**

  | Use | Where | What is in hand |
  |---|---|---|
  | Beaver textures | `Timberborn.Beavers: BeaverTextureSetter.InitializeEntity` | The beaver. One draw from the synced RNG. Both factions have 5 adult and 3 child textures |
  | Beaver avatar | `Timberborn.BeaversUI: BeaverEntityBadge.GetEntityAvatar` | The beaver |
  | Bot avatar | `Timberborn.BotsUI: BotEntityBadge.GetEntityAvatar` | The bot |
  | Empty dwelling/worker slots | `Timberborn.CharactersUI: CharacterButton.ShowAdultEmpty/ShowChildEmpty/ShowBotEmpty` | No entity. Called from dwelling and workplace fragments |
  | Path models | `Timberborn.PathSystem: DynamicPathModel.GetModelVariant` | The path or gate, set in `Awake` |
  | Driveway models | `Timberborn.PathSystem: DrivewayModelInstantiator.Load` caches `Current.PathMaterial`; `InstantiateModel(model, blockObject, …)` applies it | The building |
  | Decals | `Timberborn.DecalSystem: DecalService.Load` filters by `FactionId` | Ids never collide |
  | Planters | `Timberborn.Planting: PlanterBuilding.GetAllowedPlantables` (private, used in `Awake`) | The building. Allows every plantable of its `PlantableResourceGroup`, so a Folktails farmhouse would plant Corn |
  | Yield removers | `Timberborn.Yielding: YieldRemovingBuilding.IsAllowed(YielderSpec)` | The building. Allows any known good of its resource group |
  | Stockpiles | `Timberborn.Stockpiles: StockpileInventoryInitializer.Initialize` → `InventoryInitializer.AddAllowedGoodType` | The building. Allows every loaded good of the stockpile's `WhitelistedGoodType` |
  | Wellbeing max | `Timberborn.Wellbeing: WellbeingLimitService` sums all faction needs; `GetMaxWellbeing(tracker)` | Display and achievements only. Tiers are absolute points |
  | Global needs list | `Timberborn.WellbeingUI: PopulationWellbeingCounterGroupFactory.Create` via `PopulationWellbeingBox` | Built once. The only global needs list in the UI. A character's own panel (`WellbeingFragment`) follows its `NeedManager.NeedSpecs` |
  | Faction icon | `Timberborn.WellbeingUI: BasicStatisticsPanelFactory.Create` | Set once |
  | Game over, Wonder, tutorials | `Timberborn.GameOverUI: GameOverBox`; `Timberborn.GameWonderCompletion(UI)`; `Timberborn.GameSound: PlayWonderLaunchSound`; `Timberborn.TutorialSystem` (`HasSpec<StartingFactionSpec>`) | See D20 |
  | Starting district center | `Timberborn.GameStartup: StartingBuildingSpawner.Load` (`Current.StartingBuildingId`) | Used by the first colony and by MultiColony's founding (`StartingBuildingTemplateSpec`) |
  | Unchanged | `WonderPlanes` (`GetSingle<PlaneSpec>`: only `Planes.IronTeeth` has one); `BeaverSelectionSound` (`SoundId` is "Common" for both) | — |
  | Unchanged: fine with the union | `Attractions` (`IsCurrentFactionNeed`), `Effects`, `NeedApplication` and `SoakedEffects` (need lookups by id over the union); `FactionGoalsSystem` and `GameFactionSystemUI` (Iron Teeth unlock goal and alert); `Achievements` (D21); `MainMenuPanels`, `MapItemsUI` and `MapEditorStockpilesUI` (menu and editor, not the Game scene) | — |

- **Entity creation and load order.**
  - `EntityService.Instantiate` → `TemplateInstantiator.Instantiate` builds every component, then
    `GameObject.SetActive(true)`, which runs every `IAwakableComponent.Awake` synchronously.
  - `NeedManager` builds its needs in `Awake` (`InitializeNeeds` → private `GetNeeds()`).
  - A saved entity: `WorldEntitiesLoader.InstantiateEntity(SerializedEntity, …)` instantiates it (Awake), then
    `EntitiesLoader` runs `Load` (every `IPersistentEntity`), then PreInitialize, Initialize (`InitializeEntity`) and
    PostInitialize.
  - The serialized data is in hand **before** Awake: `new EntityLoader(serializedEntity).TryGetComponent(key, out …)`.
- **Beaver and bot creation call sites:**
  - `Timberborn.Reproduction: NewbornSpawner.SpawnAdult/SpawnChild(spawner)` (lodges and breeding pods).
  - `Timberborn.Beavers: Child.GrowUp` → `BeaverFactory.CreateAdultFromChild(child)`.
  - `Timberborn.GameStartup: StartingBeaversInitializer.Initialize(position, …)`.
  - `Timberborn.BotsUpkeep: BotManufactory.OnProductionFinished` → `BotFactory.Create(pos, rot, init)`.
  - Dev: `Timberborn.BeaversUI` (≈899/903) and `Timberborn.BotsUI: …PlaceBots` → `BotFactory.Create(position)`.
  - MultiColony founding: `ColonyFoundingService.Found` → `_beaverFactory.CreateAdult/CreateChild`.
  - MultiColony multi-start: `MultiStartPatches` `GameInitializerSpawnBeaversPatcher` → `StartingBeaversInitializer`.
- **Starting goods.** `StartingGoodsProvider.InitialGoods` yields hard-coded `"Berries"` and `"Water"`, both Common.
  MultiColony's founding also hard-codes them.
- **Storage.** Folktails: Small/Medium/Large Warehouse, SmallPile/LargePile/UndergroundPile, Small/Medium/Large Tank.
  Iron Teeth: Small/Medium/Large Warehouse, Small/Large IndustrialPile, Small/Medium/Large Tank.
- **Toolbar.**
  - `Timberborn.BottomBarSystem: BottomBarPanel.Load` builds it once.
  - Visibility is `ToolButton.ToolEnabled`, which asks every multi-bound `IToolDisabler` (dev mode and the map editor
    turn every tool on).
  - `ToolGroupButton.IsVisible` is "any tool enabled", applied at `PostLoad` and on `OnDevModeToggledEvent`. A tool
    button re-checks at `PostLoad`, on `OnDevModeToggledEvent`, and on `OnToolGroupEnteredEvent`.
  - MultiColony already has `TradingPostToolDisabler` and a dev-mode `ToolEnabled` postfix in
    `Colonies/TradingPostToolDisabler.cs`.
- **Plantables by faction:**
  - Folktails: Dandelion, Carrot, Cattail, Potato, Spadderdock, Sunflower, Wheat, ChestnutTree, Maple.
  - Iron Teeth: CoffeeBush, Canola, Cassava, Corn, Eggplant, Kohlrabi, Soybean, Mangrove.
  - Common: BlueberryBush, Birch, Oak, Pine, Succulent.
  - Plantable template names are unsuffixed, so their faction must come from the `NaturalResources.<Faction>`
    collections.
- **Worker outfits:** all outfit ids exist for both factions except Pilot (Iron Teeth only). Builder, Gatherer,
  Hauler and Lumberjack are identical; Farmer, Forester and Scavenger differ by a faction-suffixed hat. The shared
  beaver template carries both factions' hats. Each bot template carries only its own.

### 3.3 The mod (read, at bf2264c)

- **Colony state.**
  - Slots are `int` from 0, with `ColonySlotTable.MaxSlots = 4`.
  - Singletons are keyed `BeaverBuddies.<Name>` and save only when separate colonies are on (see
    `ColonyModeService`, `ColonySlotService`, `ColonyScienceService`, `ColonyWorkingHours`, `ColonyHandover`).
  - Services are `RegisteredSingleton` with `static Instance`, reset by `SingletonManager.Reset()`, which the MainMenu
    configurator calls.
  - Patches reach services through static `Instance`, not DI.
- **New game.** `MultiStart/MultiStartPatches.cs` prefixes `StartingBuildingInitializer.Initialize`. It reads
  `Settings.SeparateColoniesForNewGames` and calls `ColonyModeService.Enable(...)`. On a multi-start map it places
  **every** start at creation, for players who haven't joined too, with `DistrictOwner.PendingSlot`, then deletes the
  starting locations.
- **Founding** (`Colonies/ColonyFoundingService.cs`):
  1. The offer dialog uses `DialogBoxShower` after the host's first tick; Ctrl+K opens the same flow.
  2. The tool is `BlockObjectToolFactory.Create(StartingBuildingSpawner.StartingBuildingTemplateSpec…)`.
  3. `FoundColonyEvent` has `coordinates`, `orientation`, `isFlipped` and `startingSettings`.
  4. The host judges it in `ColonyRulesService.AllowOnHost` / `Judge` (it rewrites `startingSettings`).
  5. `Found()` replays on every computer: judge again → `CreateAsFinished` under `PendingSlot` → Berries and Water →
     beavers via `BeaverFactory`.
- **Events** (`Events/ReplayEvent.cs`):
  - Subclass `ReplayEvent`, override `GetColonyScope()` and optionally `ChangesGame()`.
  - Serialization is Newtonsoft `TypeNameHandling.All` with `ReplayEventBinder`: public fields, no registration.
  - `ReplayEvent.DoPrefix(() => new XEvent{…})` records.
  - The host stamps `slot` in `AllowOnHost` and may rewrite fields before the event is played and sent on.
  - Guests replay without judging.
  - RuntimeChecks keeps two review lists: Global-scope events (`ColonyRuntimeChecks.cs` ≈ lines 45–65) and
    `ChangesGame()==false` events (≈ line 216).
- **Refused before the first tick:** `ColonyRules.WaitsForStart` applies to Found, Handover and ActAsColony.
- **Hello.**
  - `InitializeClientEvent` (`Events/ConnectionEvents.cs`) carries host choices to guests (`foundingInSharedGame`,
    `separateScience`).
  - The host latches its choices in `ColonySession.BeginHostSession`; guests adopt them with
    `ColonySession.AdoptHostChoice`.
- **Desync checks.**
  - `ColonyDigest.Note(what, …)` inside a tick or replay feeds the heartbeat.
  - `ColonyDiagnostics.Fingerprint()` is the daily check, and its `flags` hold the mode.
- **Local vs acting colony.**
  - Display: `ColonySession.LocalSlot` (includes a steward's acting colony), `ColonyScienceService.DisplaySlot`,
    `ColonyViewService`.
  - Simulation: the event's `slot`, or the entity's owner.
  - **Never read the local slot inside a tick or replay.**
- **Toolbar locks.** `ColonyScienceService.RefreshToolLocks()` is called at every seat, steward, handover, mode and
  founding change (7 call sites). Hook the faction toolbar refresh onto it.
- **Trading.**
  - Items are strings: a good id, `ExchangeTerms.Science` or `ExchangeTerms.Beavers`.
  - The picker is the mod's own `TradingPostGoodPicker`, fed by `TradeItems` (cached `Groups()`, `SpecialItems()`,
    `IsOffered`).
  - The form verdict is the pure `TradeOfferForm.Judge`, shown in `TradingPostFragment.RefreshSummary`.
  - The binding check is `ColonyExchangeService.WhyNotPropose` / `Accept` in `TradingPostExchange.cs`, deterministic
    at replay.
  - `CheckTradingPosts` runs every 8 ticks and ends exchanges whose colonies changed.
  - Beavers move in `MoveBeavers` via `Citizen.AssignDistrict`.
  - Wishes: `ColonyWishlist` + pure `WishlistTerms`.
- **Trading Post halves.**
  - Two entities of the **same** template from one tool, two `BuildingPlacedEvent`s.
  - The host pairs them in `ColonyRulesService.JudgePairs`.
  - `LinkedBuilding` links the halves without comparing templates.
  - Every Trading Post test is on `MultiColonyTradingPostSpec`, never on a template name. The only exception is
    `RuntimeChecks/TradingPostBuildingChecks.cs`.
- **Handover.** `ColonyLifecycle.NearestLiving(from, candidates)` in `Colonies/ColonyHandover.cs`, called for dead
  colonies (every computer) and for absent ones (host).
- **Conventions.**
  - Pure classes (`*Rules`, `*Terms`, `*Table`, …) use only `System.*`. They are linked into
    `StabilityTests/StabilityTests.csproj` and paired with a game-bound `*Service`.
  - Patch classes are `XxxPatcher` with `[HarmonyPatch]`, installed by `harmony.PatchAll()`. A prefix that
    **records** a player action needs `[HarmonyPriority(Priority.First)]`; none of this plan's patches record.
  - UI is code-built from `Util/NativeElements.cs` and the game's own classes.
  - New strings go in `enUS_BeaverBuddie.csv` only (British spelling, short, second person, keys without digits).
  - Mod Settings tooltips: 1–2 lines of at most 112 characters each (a StabilityTests check enforces it).

---

## 4. Architecture

```
            MainMenu                                    Game scene (every computer)
  GameSceneLoader.StartNewGame ──capture──► MixedFactions.Decide() in FactionService.Load prefix
  (setting + host unlocks)                   (new game: the capture; save: BeaverBuddies.ColonyFactions)
                                                      │ IsOn, BaseFaction, slot table
                                                      ▼
   OtherFactionCollections (multi-bound providers) ─► union of templates / goods / needs / materials
   TemplateCollectionService.Load postfix ─────────► de-dup by blueprint
                                                      ▼
                                   FactionCatalog (game adapter over pure FactionSets)
                     template→faction · goods(faction) · needs(faction,type) · plantables · DC · post template
                                                      ▼
      ColonyFactionService (slot→faction, saved, digest)     CharacterFaction (per beaver, saved)
      FactionCreationContext (who is creating a character)   FactionOf(entity) resolution (D10)
                                                      ▼
     simulation rules (template-keyed)   character patches   model/look patches   display patches   trading rules
```

### 4.1 New files (folder `BeaverBuddies/Factions/`)

| File | Kind | Purpose |
|---|---|---|
| `MixedFactions.cs` | static + patches | `IsOn`, `BaseFaction`, `Decide` (FactionService.Load prefix), new-game capture, blueprint-modifier union, `OtherFactionCollections` providers, template de-dup, `[Factions]` logging |
| `FactionSets.cs` | **pure** | From plain dictionaries (faction → collection ids, collection → items, common ids), gives `FactionsOf(item)`, `ItemsOf(faction)` (common ∪ own), `IsCommon(item)`, `SoleFaction(item)` (null if common or listed by several). Used for goods, needs, template blueprint paths and plantables |
| `FactionRules.cs` | **pure** | Verdicts and rules: founding choice, switch (untouched), placement faction, `IsTradeableTo`, beaver rule, `PreferSameFaction`, available factions. Enums `FactionChoiceVerdict`, `FactionSwitchVerdict` |
| `FactionTable.cs` | **pure** | slot → faction rows `"slot\|faction"`, parse and format (garbage rows ignored, missing slot → base), like `ColonySlotTable` |
| `FactionCatalog.cs` | game | `RegisteredSingleton, ILoadableSingleton`. Builds `FactionSets` from `ISpecService` (`FactionSpec`, `TemplateCollectionSpec`, `GoodCollectionSpec`, `NeedCollectionSpec`) and `TemplateCollectionService.AllTemplates`. Lookups: `Spec(id)`, `FactionOfTemplate(name)`, `GoodsOf(f)`, `NeedsFor(f, isBot)` (from `FactionNeedService.Needs`, order kept, cached arrays), `PlantableFaction(name)`, `StartingBuildingOf(f)`, `TradingPostTemplateOf(f)`, `IsAvailable(f)` (host unlocks, D1) |
| `ColonyFactionService.cs` | game | `RegisteredSingleton, ILoadableSingleton, ISaveableSingleton, IResettableSingleton`. Singleton `BeaverBuddies.ColonyFactions` (`Mixed`, `Base`, `Colonies` list). `FactionOfSlot(s)`, `LocalFaction`, `Set(slot, f)` (replay only; `ColonyDigest.Note("faction", …)`), `FactionOf(entity)` (D10 order), `Fingerprint()` |
| `CharacterFaction.cs` | component + patches | Saved component on beavers (`BeaverBuddies.CharacterFaction` / `Faction`), `FactionCreationContext` (a `[ThreadStatic]` stack with `using` scopes), the loader peek patch, creation-site patches (D11) |
| `FactionCharacterPatches.cs` | patches | Needs (`NeedManager.GetNeeds`), textures, bot factory, worker outfits, wellbeing max, avatars |
| `FactionModelPatches.cs` | patches | Paths, gates, driveways, modular shafts, decals |
| `FactionBuildingRules.cs` | patches | D15: stockpiles, planters, yield removers |
| `FactionToolbar.cs` | disabler + patches | `FactionToolDisabler : IToolDisabler`, the refresh, the planting-UI crash fix |
| `FactionDisplayPatches.cs` | patches | Wellbeing box and icon, goods UIs, game over, Wonder, tutorials off |
| `FactionChoice.cs` | service + events + UI | The founding chooser, `ColonyFactionSwitchEvent` (D12) and its host judge and replay, the multi-start switch offer |
| `NewGameFactionCapture.cs` | MainMenu service | Reads the setting and the host's unlocks when a new game starts |

### 4.2 The mode decision (once per scene, before any collection loads)

`[HarmonyPatch(typeof(FactionService), nameof(FactionService.Load))] Prefix(FactionService __instance)` →
`MixedFactions.Decide(...)`:

1. Scene parameters are `__instance._sceneLoader.GetSceneParameters<GameSceneParameters>()` (publicized).
2. **New game:** `IsOn = NewGameFactionCapture.Take()`, which is true only if it was captured for this start, the
   setting was on, separate colonies was on and every faction was unlocked. `BaseFaction` is the configuration's
   `FactionId`.
3. **Loaded save:** read `__instance._singletonLoader.TryGetSingleton(new SingletonKey("BeaverBuddies.ColonyFactions"),
   out loader)`. `IsOn = loader.Get(Mixed)`; the slot table is read too.
4. Always clear the capture.
5. Log one line: `[Factions] Mixed factions: on (base Folktails; colonies 0:Folktails 1:IronTeeth)`, or off.

The static must not be an `IResettableSingleton`: the MainMenu configurator resets those, and the capture is set after
it. `ColonyFactionService` (Game) copies what it needs in its `Load`.

**Blueprint modifiers.** Prefix `FactionBlueprintModifierProvider.Initialize(IEnumerable<BlueprintModifierSpec>
modifiers)`: when on, replace the argument with every faction's `BlueprintModifiers`, distinct, in `Order` order. It
is empty in vanilla; this is for mods.

**Collections.** One class `OtherFactionCollections` implements `ITemplateCollectionIdProvider`,
`IGoodCollectionIdsProvider`, `INeedCollectionIdsProvider` and `IMaterialCollectionIdsProvider`. It is bound once and
`MultiBind…().ToExisting<>()` four times in the Game configurator. It takes `FactionService` and `FactionSpecService`
in its constructor, which also guarantees load order. Each method returns nothing when off, and otherwise the
collection ids of every faction except `Current`, in `Order`. No Harmony is needed: the game unions all providers.

**De-dup.** Postfix `TemplateCollectionService.Load`: when on, set `AllTemplates` to the distinct blueprints, keeping
the first occurrence and the order. **(verify)** whether `ISpecService.GetBlueprint(path)` returns one instance per
path; if not, de-dup by `Blueprint.Name` / path. After this, `TemplateNameMapper`, `BeaverFactory` and
`BuildingService` load as in vanilla.

### 4.3 The faction of anything (`ColonyFactionService.FactionOf(BaseComponent)`)

1. A `CharacterFaction` component: its resolved id.
2. A template listed by exactly one faction (`FactionCatalog.FactionOfTemplate(TemplateSpec.TemplateName)`): that
   faction. This covers bots and all suffixed buildings.
3. Otherwise (common templates): **display only**, the owner colony's faction (`DistrictOwner.OwnerOf` or its
   post-border successor → `FactionOfSlot`), else `BaseFaction`.

Simulation rules (D15, D14, needs, outfits) use only 1 and 2, never 3. Step 3 depends on ownership that can change,
which is fine for a model but not for a rule.

### 4.4 `CharacterFaction` and the creation context

```csharp
public class CharacterFaction : BaseComponent, IPersistentEntity   // decorator on BeaverSpec
{
    static readonly ComponentKey Key = new("BeaverBuddies.CharacterFaction");
    static readonly PropertyKey<string> FactionKey = new("Faction");
    string factionId;
    // Resolved on first read: needs are built in Awake, before Load, inside Instantiate, where the context is set.
    public string FactionId => factionId ??= FactionCreationContext.Current ?? MixedFactions.BaseFaction;
    public void Save(IEntitySaver s) { if (MixedFactions.IsOn && factionId != null) s.GetComponent(Key).Set(FactionKey, factionId); }
    public void Load(IEntityLoader l) { if (l.TryGetComponent(Key, out var o)) factionId = o.Get(FactionKey); }
}
```

- **Loaded beavers.** Prefix and finalizer on the internal `WorldEntitiesLoader.InstantiateEntity(SerializedEntity,
  ICollection<…>)`. When on:
  1. Push `new EntityLoader(serializedEntity).TryGetComponent(Key, …)` → its faction, else `BaseFaction`.
  2. Pop in the finalizer.

  `Load` then sets the same value. Log a warning once if they differ.
- **New characters** (D11). Each creation site pushes a scope around the game's call:
  - `NewbornSpawner.SpawnAdult/SpawnChild` prefix/finalizer: the spawner's `FactionOf`.
  - `BeaverFactory.CreateAdultFromChild` prefix/finalizer: the child's `CharacterFaction`.
  - `BotManufactory.OnProductionFinished` prefix/finalizer: the building's faction. The bot factory reads the context.
  - Founding and faction switch: the chosen faction, in mod code.
  - Starting beavers (`StartingBeaversInitializer.Initialize`): no scope; the base faction is correct.
  - Dev spawn (`BeaversUI` spawn tool method and `BotsUI …PlaceBots`): `ColonyFactionService.LocalFaction`. **(verify)**
    the dev beaver tool's method name.
- **The context is a stack** so nested creations are safe. `Current` is the top or null. A creation that reads no
  context while mixed falls back to `BaseFaction` and logs `[Factions] a character was created with no faction
  context` once per session.

---

## 5. Phases

Work on a branch from `origin/main`. Commit at the end of each phase. Every phase ends with both builds, 0 warnings,
`dotnet run --project StabilityTests` and RuntimeChecks green (§9 commands). The border removal (D5) is in flight in
another session: `git fetch` before each phase and rebase if `main` moved.

### Phase 0: Verify the unknowns (no product code)

Settle each **(verify)** below, and record the answers in a new §13 of this file ("Phase 0 findings"):

1. De-dup identity: does `GetBlueprint(path)` return the same `Blueprint` instance for the same path?
2. `EntitySetup.Builder` without `SetId`: how is the new entity's `Guid` made, and does MultiColony already make it
   deterministic? The founding creates entities in a replay, so there must be an answer; the switch (D12) must use
   the same path.
3. The dev-mode beaver spawn method in `Timberborn.BeaversUI` (≈ decompiled line 899) and its class.
4. For D17, how to compute a not-yet-built half's entrance tile from `BlockObjectSpec` + `Placement`. Candidates:
   `Placement.Transform…`, `BlockObjectSpec.Entrance`, `BuildingAccessibleSpec`. Also how to test "a finished
   district's road is at this tile". `ColonyReach.RoadOwner` uses `DistrictService.IsOnDistrictRoad(district,
   worldPos)`; keep that part and drop the land fallback.
5. Is the top bar already per colony in MultiColony (the steward text says it "follows")? Find the patch. If it
   sums every district, see Phase 6 step 7.
6. The `ModularShaftModelService` / `ShaftModelFactory` / `ShaftFrameFactory` constructors and their fields, for
   building a second set (Phase 4 step 4).
7. `NeedManager.GetNeeds` is private and called from `InitializeNeeds` in `Awake`. Confirm nothing else calls
   `InitializeNeeds` later (for example contamination, or `InfluenceByChildhood`).
8. `GameSceneLoader.StartNewGame` is the only new-game entry the New Game panel uses (`NewGameModePanel.StartNewGame`
   → it). `StartNewGameInstantly` (dev quick start) captures nothing.

### Phase 1: Load both factions, and the crash fixes

Files: `Factions/MixedFactions.cs`, `NewGameFactionCapture.cs`, `FactionSets.cs`, `FactionTable.cs`,
`FactionCatalog.cs`, `ColonyFactionService.cs` (state and save only), `Settings.cs`, `Plugin.cs` (MainMenu binding),
`Colonies/ColonyConfigurator.cs` (bindings), enUS CSV.

1. **The setting.** In `Settings.cs`, under `// ---- Separate colonies ----`:
   `MixedFactions` (bool, default **false**).
   - Label key `BeaverBuddies.Settings.MixedFactions`: "Mixed factions for new games (beta)".
   - Tooltip key `.Tooltip`, two lines of at most 112 characters each:
     - "Host only, new games with separate colonies: each player picks Folktails or Iron Teeth for their colony."
     - "Needs every faction unlocked on the host's computer. A save keeps the mode it started with."
   - Add the static getter `MixedFactionsForNewGames => instance?.MixedFactions.Value ?? false`.
2. **The capture.** `NewGameFactionCapture` is bound in `ConnectionMenuConfigurator` (MainMenu) as a
   `RegisteredSingleton`, with `FactionSpecService` and `FactionUnlockingService` injected.
   - Prefix `GameSceneLoader.StartNewGame(NewGameConfiguration c)` → `NewGameFactionCapture.Instance?.Capture(c)`.
   - It records `requested = Settings.MixedFactionsForNewGames && Settings.SeparateColoniesForNewGames`, and
     `allUnlocked = factions.All(f => !IsLocked(f))`.
   - It stores a static `Pending { requested, allUnlocked, factionId }`.
   - If `requested && !allUnlocked`, remember a notice for the Game scene: key
     `BeaverBuddies.Colony.Faction.NotUnlocked`, "Mixed factions is on, but {0} is not unlocked on this computer, so
     this game has one faction." `{0}` is the game's display name of the locked faction. Show it once after load with
     `ColonyRulesService.ShowNotice`.
3. **The decision** (§4.2), the union providers, the blueprint-modifier prefix and the de-dup postfix.
4. **The catalog.**
   - Build `FactionSets` for templates (by blueprint path, then mapped to `TemplateName`), goods, needs and plantables
     (the `NaturalResources.<F>` collections).
   - Log counts: `[Factions] catalog: 2 factions, N templates (4 shared), 60 goods (17 shared), …`.
   - The catalog is only consulted when on, but it is cheap enough to build always. Build it only when on (D22).
5. **Crash fixes (all gated):**
   - **Bots:** prefix `BotFactory.Load` (return false when on). Cache one blueprint per faction from
     `TemplateService.GetAll<BotSpec>()` via the catalog, and call `_templateInstantiator.CacheInstance` for each.
     Prefix `BotFactory.Create(Vector3, Quaternion, object)` (return false when on): build the same `EntitySetup`
     with the blueprint for `FactionCreationContext.Current ?? BaseFaction`, and add the same init components as
     vanilla, in the same order.
   - **Shafts:** prefix `ShaftFrameFactory.Load` and `ShaftModelFactory.Load` to pick the `ModularShaftPartsSpec` of
     `MixedFactions.ShaftBuildFaction ?? BaseFaction` (default base) instead of `GetSingle`. Identify a parts spec's
     faction by its collection (`ModularShaftParts.<F>`) through the catalog. Per-faction models come in Phase 4.
   - **Planting UI:** prefix the `PlantingUI` `GetPlanterBuildingName` (the `.Single` over `PlanterBuildingSpec`) to
     choose the planter whose faction equals the plantable's faction (the catalog), else the base faction's, else
     the first. Display only.
   - **Worker outfits:** prefix `WorkerOutfitService.Load` (when on) to build a mod dictionary keyed
     `(FactionId, Id, WorkerType)` from `GetSpecs<WorkerOutfitSpec>()`. Prefix `TryGetOutfitSpec(Worker, out spec)`
     to look up the worker's own faction (§4.3 steps 1–2; for a beaver that is `CharacterFaction`). Mirror the
     vanilla method's other logic (the workplace's `WorkplaceWorkerOutfitSpec`) exactly.
6. **Acceptance (solo, dev mode allowed):** a new mixed game on a standard map as Folktails loads.
   - The log shows the decision and catalog lines, and no exception.
   - In dev mode, place one of every Iron Teeth building next to Folktails ones. Nothing throws.
   - Save and load. Nothing throws.
   - A non-mixed game loads with no `[Factions]` lines except "off".

### Phase 2: Characters: faction, needs, looks

Files: `Factions/CharacterFaction.cs`, `FactionCharacterPatches.cs`, `ColonyConfigurator.cs` (decorator
`builder.AddDecorator<BeaverSpec, CharacterFaction>()` + `Bind<CharacterFaction>().AsTransient()`).

1. `CharacterFaction`, the context, the loader peek and the creation-site scopes (§4.4).
2. **Needs.** Prefix the private `NeedManager.GetNeeds()` (return false when on):
   `__result = FactionCatalog.NeedsFor(faction, HasComponent<BotSpec>())`. The faction is `CharacterFaction` for
   beavers and the template's for bots (§4.3). `NeedsFor` filters `FactionNeedService.Needs` (already scaled by
   `NeedModificationService`; do not re-scale) by `FactionSets.ItemsOf(faction)` and `CharacterType`, keeping order.
   Cache per `(faction, type)`.
3. **Wellbeing max.** Prefix `WellbeingLimitService.GetMaxWellbeing(tracker)`: compute from that character's
   `NeedManager.NeedSpecs` with the same formula as vanilla's private helper. Cache per `(faction, type)`.
4. **Textures.** Prefix `BeaverTextureSetter.InitializeEntity` (return false when on): pick `Textures` /
   `ChildTextures` of `CharacterFaction`'s `FactionSpec`, with exactly **one** `_randomNumberGenerator
   .GetEnumerableElement` call, as vanilla does. The RNG is synced; a different number of draws would desync.
5. **Avatars.**
   - Postfix `BeaverEntityBadge.GetEntityAvatar` and `BotEntityBadge.GetEntityAvatar` to use the entity's faction.
   - For the empty slots: postfix the dwelling and workplace fragments' `ShowFragment(entity)` to set a static
     `FactionDisplay.PanelFaction = FactionOf(entity)`, cleared in `ClearFragment`.
   - Prefix `CharacterButton.ShowAdultEmpty/ShowChildEmpty/ShowBotEmpty` to use `PanelFaction ?? LocalFaction`.
6. **Acceptance:**
   - In a mixed game, dev-spawn a beaver in each colony (seat flip, §9.3). Each has its own faction's needs in its
     panel, and the right fur.
   - Build a bot assembler of each faction: Folktails bots need Biofuel, Iron Teeth bots need Energy. Neither has the
     other's need.
   - Births and growing up keep the faction.
   - Save and load: needs, faction and fur family are the same.

### Phase 3: Colony factions, founding, switching

Files: `Factions/ColonyFactionService.cs`, `FactionRules.cs`, `FactionChoice.cs`, `Colonies/ColonyFoundingService.cs`,
`Colonies/ColonyRulesService.cs`, `Colonies/ColonyRules.cs` (`IColonyWorld` + refusals),
`Colonies/ColonyGameWorld.cs`, `Colonies/ColonySession.cs`, `Events/ConnectionEvents.cs`,
`Colonies/ColonyDiagnostics.cs`, `Colonies/TradeOverviewPanel.cs`, `MultiStart/MultiStartPatches.cs` (nothing,
unless Phase 0 says otherwise), enUS CSV.

1. **The slot table.**
   - `ColonyFactionService.Load` copies `MixedFactions`' table. A new game sets slot 0 and every multi-start slot to
     `BaseFaction`: nothing to write, since a missing slot means base.
   - `Save` writes when on.
   - `Set` is called only inside a replay: `ColonyDigest.Note("faction", slot, ColonyDigest.Of(id))`, then refresh
     the toolbar (Phase 6) and the overview.
   - Add `ColonyFactionService.Instance?.Fingerprint()` (e.g. `F:0Folktails,1IronTeeth`) to
     `ColonyDiagnostics.Fingerprint()`, and `mixed` to its `flags`.
2. **Available factions (D1).**
   - `ColonySession.BeginHostSession` latches `HostFactions`: the factions unlocked on the host's profile (the
     catalog's `FactionUnlockingService`). Latch it only when on.
   - `InitializeClientEvent` gets `public List<string> hostFactions`; guests adopt it with `AdoptHostChoice`.
   - With no session (solo), use the local profile.
   - `FactionCatalog.IsAvailable(f)` reads it.
3. **Founding.**
   - Add `public string faction;` to `FoundColonyEvent`. It is null in non-mixed games.
   - **The chooser.** When on, the founding offer dialog and Ctrl+K show one native wooden button per faction:
     `NativeElements.WoodenButton` with the faction's `Logo` sprite and localized `DisplayName`, via
     `DialogBoxShower…AddContent(panel)`, plus the default cancel.
     - A faction not in `HostFactions` shows disabled, with key `BeaverBuddies.Colony.Faction.HostLocked`: "The host
       has not unlocked {0}."
     - Prompt key `BeaverBuddies.Colony.Faction.Choose`: "Choose your colony's faction, then place its district
       center."
     - Choosing opens the founding tool for that faction's district center: `FactionCatalog.StartingBuildingOf(f)`,
       whose `PlaceableBlockObjectSpec` replaces `StartingBuildingSpawner.StartingBuildingTemplateSpec` in the tool.
       Keep one tool per faction, created lazily.
     - `FoundingPlacer` records `faction`.
     - Not mixed: unchanged.
   - **Host judge** (`ColonyRulesService.Judge`, Founding scope):
     - Add `FactionRules.JudgeFoundingFaction(isOn, faction, known, available)`.
     - Refuse an unknown faction or one not in `HostFactions` with new `ColonyRefusal.FactionUnavailable` (message
       key `BeaverBuddies.Colony.Refused.FactionUnavailable`: "The host has not unlocked that faction.").
     - When the rewrite happens, write `faction = null` if not on, and `faction ??= BaseFaction` if on.
     - The founding placement's `TemplateName` must be the chosen faction's district center, so block validation uses
       the right spec (the footprints are equal, but be correct).
   - **`Found()` replay:**
     1. `ColonyFactionService.Set(slot, faction ?? BaseFaction)` first.
     2. Create the district center from `StartingBuildingOf(faction)`.
     3. Spawn the beavers inside `using (FactionCreationContext.Push(faction))`.
4. **The switch (D12).**
   - `ColonyFactionSwitchEvent : ReplayEvent { public string faction; }`, Global scope, `ChangesGame() => true`.
     - Add it to the RuntimeChecks Global list.
     - Add it to `WaitsForStart` (refused before the first tick) and to the "refused for unseated actors" list.
   - **Host** (`AllowOnHost`, next to the steward events' special cases):
     - `FactionRules.JudgeSwitch(isOn, isSeatOwner, current, wanted, available, facts)` →
       `FactionSwitchVerdict { Allowed, NotMixed, NotYours, SameFaction, Unavailable, Touched }`.
     - `facts` comes from `ColonyFactionService.UntouchedFacts(slot)`: count of stamped entities of the slot that are
       not district centers (buildings, construction sites, paths); mark counts from `ColonyMarks`; open or offered
       exchanges from `ColonyExchangeService`; unlock count (separate science only); count of district centers
       (must be ≥ 1).
     - Refuse with new `ColonyRefusal.FactionSwitchNotAllowed` (key `…Refused.FactionSwitch`: "A colony can change
       faction only before it builds, marks, unlocks or trades anything.").
   - **Replay** (every computer): judge again from the same facts; skip and log if not allowed, as `Found()` does.
     Otherwise, for each district center of the slot (normally one):
     1. Record its placement and its `SimpleOutputInventory` stock.
     2. Count its colony's adults and children (from the slot's districts).
     3. Delete those beavers (`EntityService.Delete`).
     4. Delete the district center (`EntityService.Delete`, as `StartingBuildingSpawner.DeleteStartingBuilding`
        does).
     5. Create the new district center with `_constructionFactory.CreateAsFinished(builder, placement)` under
        `DistrictOwner.PendingSlot = slot`, then `SetSlot`.
     6. Give back every recorded good the new faction can store (`GiveExistingIgnoringCapacity`).
     7. Spawn `min(adults, settings.adults)` adults and `min(children, settings.children)` children with the founding
        spacing, inside the new faction's context.
     8. `Set(slot, faction)`.
     9. Journal and notice: key `BeaverBuddies.Colony.Faction.Switched`, "{0} now plays {1}." (colony name, faction
        name).
     10. Refresh the toolbar.

     Follow `Found()`'s code closely: entity ids (Phase 0.2), `PendingSlot`, the camera only for the actor.
   - **UI:**
     - (a) On the local player's own colony card in the Ctrl+T window (`TradeOverviewPanel`): a wooden button per
       other available faction, "Play {0} instead" (key `BeaverBuddies.Colony.Faction.SwitchButton`), shown only
       while the local pre-check allows it. Otherwise nothing.
     - (b) A one-time dialog for a seated player whose colony exists when they join, while untouched, after the first
       tick, once per session (the pattern of the founding offer's `offerPending`):
       - Key `BeaverBuddies.Colony.Faction.StartPrompt`: "Your colony starts as {0}. You can switch to {1} until it
         builds, marks or trades anything."
       - Buttons: "Keep {0}" (`…Faction.Keep`) and "Switch to {1}" (`…Faction.Switch`).
       - With more than one other faction (mods), show one switch button each.
5. **Faction shown.**
   - Each colony card in the Ctrl+T window shows the faction logo (a `NativeElements.Icon`) before the title, with the
     faction name in the detail line. Key `BeaverBuddies.Colony.Overview.Faction`: "{0}", or fold it into the
     existing line.
   - The Trading Post header ("Trading with {0}") shows the partner's logo.
6. **Placement faction (D14).**
   - Add `string FactionOfTemplate(string templateName)` and `string FactionOfSlot(int slot)` to `IColonyWorld`,
     implemented in `ColonyGameWorld`.
   - In `ColonyRules`' Placement scope, when on: refuse if the template's sole faction is not null and differs from
     the actor slot's faction, **unless** the template is a Trading Post (`IsTradingPostTemplate`).
   - New `ColonyRefusal.OtherFactionBuilding`, key `…Refused.OtherFactionBuilding`: "That building belongs to another
     faction."
   - Dev mode is not exempt: the host judges the same.
7. **Acceptance:**
   - Solo with seat flip: found colony 2 as Iron Teeth; colony 3 as Folktails, then switch it to Iron Teeth while
     untouched; build a path in colony 3, and the switch is refused with the message.
   - On a 3-start multi-start map, start 2 switches through the Ctrl+T button.
   - Save and load keep every colony's faction.
   - Daily check and heartbeat stay green.

### Phase 4: Every building looks like its own faction

Files: `Factions/FactionModelPatches.cs`, plus a small hook in `Colonies/ColonyReach.cs` (or wherever `ColonyStamp`
survives the border removal).

1. **Driveways.** Postfix `DrivewayModelInstantiator.InstantiateModel(DrivewayModel, BlockObject blockObject, …)`:
   when on, set `__result.GetComponentInChildren<Renderer>(true).sharedMaterial = Spec(FactionOf(blockObject))
   .PathMaterial.Asset`.
2. **Paths and gates.** A helper `PathLook.Apply(EntityComponent)` re-sets the six ground children (`PathMaterial`)
   and six roof children (`BaseWoodMaterial`) of `DynamicPathModel` for the entity's faction (§4.3, with step 3 for
   paths). Use `GetComponentInChildren<Renderer>(true)`: they are inactive. **(verify)** the child naming from
   `DynamicPathModel.InitializeModels/AddModel` (prefix + variant).
   - Call it from a postfix of `DynamicPathModel.Awake`. For previews, use `LocalFaction`. Gates have a suffixed
     template, so they are right from Awake.
   - Call it again when a path's owner is set or changed: `ColonyStamp`'s stamp setter and `InitializeEntity`, and a
     handover's re-stamp. This is display only, with no digest.
3. **Decals.**
   - Prefix `DecalService.Load` when on: load every `DecalSpec` (ids never collide).
   - Filter the picker in `DecalButtonContainer.Show(decalSupplier)` to the building's faction (and those with no
     faction).
   - Prefix `DecalSupplier.InitializeEntity`: an empty decal takes the first of the building's own faction.
   - Tail decals take the Detailer's faction, as vanilla would.
4. **Modular shafts (per faction).**
   - The Phase 1 prefix already picks the base faction's parts. Now build a second
     `ModularShaftModelService` + `ShaftModelFactory` + `ShaftFrameFactory` for each other faction, by reflection
     with the same injected dependencies (Phase 0.6). Call `Load()` on each inside
     `MixedFactions.ShaftBuildFaction = f`. Keep them in `FactionModelPatches`.
   - Postfix `ModularShaftModelUpdater.Awake` to point `_modularShaftModelService` at the set for the building's
     template faction.
   - **Fallback, if the reflection proves fragile:** keep the base faction's shaft models for every shaft, and list it
     under Known limits. This is a model only.
5. **Acceptance:** Iron Teeth paths, driveways, gates, banners and power shafts look Iron Teeth in an Iron Teeth
   colony, and Folktails ones look Folktails next to them. Save and load keep the looks.

### Phase 5: Buildings work only with their own faction's things (D15)

File: `Factions/FactionBuildingRules.cs`. This is simulation: each rule keys **only** on the building's template
(§4.3 steps 1–2). Common buildings keep vanilla behaviour.

1. **Stockpiles.**
   - Prefix `StockpileInventoryInitializer.Initialize` when on and the building has a sole faction: add the allowed
     goods as vanilla does, but only `GoodService.Goods` of the stockpile's `WhitelistedGoodType` that are in
     `GoodsOf(faction)`. Mirror vanilla's other calls (`SingleGoodAllower`, capacity) exactly.
     **(verify)** the vanilla body before copying it.
   - The stockpile dropdown reads `AllowedGoods`, so it follows on its own.
2. **Planters.** Postfix the private `PlanterBuilding.GetAllowedPlantables`: keep plantables whose faction (the
   catalog's `PlantableFaction`) is null (common) or the building's.
3. **Yield removers.** Postfix `YieldRemovingBuilding.IsAllowed(YielderSpec)`: when `__result` is true and the building
   has a sole faction, require `GoodsOf(faction).Contains(yielderSpec.Yield.Id)`. Logs are Common, so lumberjacks of
   both factions still cut Mangrove and Maple. The gatherer's dropdown (`GetAllowedGoods`) follows on its own.
4. **Acceptance:**
   - A Folktails warehouse's good list has no Iron Teeth goods.
   - A Folktails farmhouse's crop list has only Folktails and common crops.
   - A Folktails gatherer next to a planted Coffee bush ignores it.
   - A game with the mode off shows the vanilla lists.

### Phase 6: Toolbar and display follow the local colony

Files: `Factions/FactionToolbar.cs`, `FactionDisplayPatches.cs`, `Colonies/ColonyScienceService.cs` (one call).

1. **`FactionToolDisabler : IToolDisabler`**, `MultiBind` in the Game configurator next to `TradingPostToolDisabler`.
   - `IsEnabled(tool)` is `true` when off.
   - For a `BlockObjectTool`, the template's sole faction must be null or `LocalFaction`.
   - For a `PlantingTool`, the plantable's faction must be null or `LocalFaction`.
   - Anything else: true.
   - Keep it O(1): a dictionary from template name to faction, built once.
2. **Refresh.** `FactionToolbar.Refresh()`, called at the end of `ColonyScienceService.RefreshToolLocks()`, so it runs
   at all 7 existing call sites, and from `ColonyFactionService.Set`:
   1. Call `OnDevModeToggledEvent(null)` on every `ToolButtonService.ToolButtons` and on every entry of the private
      `ToolButtonService._toolGroupButtons`. The argument is ignored. **Do not** post a real `DevModeToggledEvent`:
      other systems listen to it.
   2. If the active tool is now disabled, `ToolService.SwitchToDefaultTool()`.
   3. If the active group is hidden, `ToolGroupService.ExitToolGroup()`.
   4. Refresh the basic-statistics faction icon (step 4).
3. **Population wellbeing box.** Postfix `PopulationWellbeingBox.GetPanel` (runs on every open): hide need counters
   outside `ItemsOf(LocalFaction)`, and groups left empty.
4. **Faction icon.** After `BasicStatisticsPanelFactory.Create`, keep the `FactionIcon` image and set it to
   `Spec(LocalFaction).Logo.Asset` on each refresh.
5. **Goods lists (display only; never filter `GoodService.Goods` itself, it is the simulation's index):**
   - District distribution tab: postfix `DistrictDistributionSetting.GetGoodDistributionSettingsForGroup` for **UI
     callers only**. It has one caller, `DistributionSettingGroupFactory.CreateItems`; filtering there is safer, so
     patch the factory with a prefix that re-implements its short body with a filtered list. Filter by the
     district's colony faction.
   - District crossing import icons: postfix `ImportGoodIconFactory.CreateImportGoodIcon(parent, goodId)` to record
     each icon's root per good. Postfix `DistrictCrossingFragment.UpdateRootAndIcons` to hide goods outside the
     crossing district's faction.
   - Good statistics tab: re-implement `GoodStatisticsGroupFactory.CreateItems` (prefix) with goods of
     `ItemsOf(LocalFaction)`. **Do not** patch `GoodService.GetGoodsForGroup`: simulation code calls it.
   - Stockpile tooltip's storage list: postfix `GoodStockpilesTooltipFactory.Load` to keep templates of
     `LocalFaction` or common. It is built once at load, so filter at use in `AddIcons` if the local faction can
     change.
   - Automation resource counter dropdown: postfix `ResourceCounterGoodsDropdownProvider.InitializeEntity` to keep
     `ItemsOf(FactionOf(building))`, via `AccessTools.PropertySetter` on `Items`.
6. **Game over, Wonder, tutorials.**
   - Game over: in `GameOverBox`, set `Flavor` and `Info` from `LocalFaction`'s spec in a postfix of its
     `OnGameOverEvent` handler.
   - Wonder completion: `GameWonderCompletionService.CompleteWonder` and `IsWonderCompletedWithCurrentFaction` use
     `LocalFaction` (the player's own profile). `WonderCompletionPanel` uses `LocalFaction`'s spec.
     `PlayWonderLaunchSound` uses the activating Wonder's template faction.
   - Tutorials: when on, make `TutorialService.GetConfigurations` and `TutorialTriggers.Load` behave as for a
     faction without `StartingFactionSpec`, i.e. no tutorial.
7. **Top bar.** If Phase 0.5 finds the top bar sums every district, postfix `TopBarCounterRow.UpdateAndGetStock` to
   return 0 and hide the row for goods outside `ItemsOf(LocalFaction)` **that the local colony has none of**. Keep
   rows the colony actually holds, for example after a handover. Otherwise nothing: rows already hide at 0 stock.
8. **Acceptance:**
   - Flip the seat between a Folktails and an Iron Teeth colony: the toolbar, crop tools, wellbeing box and faction
     icon switch at once; an open tool of the other faction closes.
   - A steward running the other faction's colony sees that faction.
   - Dev mode shows every tool.

### Phase 7: Trading (D2, D3, D19)

Files: `Factions/FactionRules.cs`, `Colonies/TradeItems.cs`, `TradeOfferForm.cs`, `TradingPostExchange.cs`,
`TradingPostFragment.cs`, `TradingPostGoodPicker.cs`, `TradeOverviewPanel.cs`, `ColonyWishlist.cs`,
`WishlistTerms.cs`, `ColonyRulesService.cs` (`JudgePairs`), enUS CSV.

1. **Pure rules** (`FactionRules`):
   - `IsTradeableTo(string item, Func<string,bool> receiverStores, bool separateScience, string giverFaction,
     string receiverFaction)`:
     - Science: `separateScience`.
     - Beavers: `giverFaction == receiverFaction`.
     - A good: `receiverStores(item)`.
     - When off, the callers pass "store everything" and equal factions, so behaviour is unchanged.
   - `MayBeaverCross(string beaverFaction, string targetFaction) => beaverFaction == targetFaction`.
2. **Form.**
   - `TradeOfferForm.Judge` gets two optional predicates, `giveAllowed` and `getAllowed` (default: always true), and
     two new verdicts:
     - `ErrorNotStorable`, key `BeaverBuddies.Colony.Trade.ErrorNotStorable`: "{0} can't store {1}. Between factions,
       only goods both factions use can be traded." (receiver name, good name).
     - `ErrorBeaversFaction`, key `…Trade.ErrorBeaversFaction`: "Beavers can only move between colonies of the same
       faction."
   - `RefreshSummary` shows them.
   - Update the fuzz test (`ColonyChecks` "form" test) to pass the allow-all predicates, and add a test for each new
     verdict.
3. **Picker.**
   - `TradingPostGoodPicker.Open(...)` gets an optional `Func<string,bool> allowed`. Items failing it are not listed
     at all (not greyed): the "Only what is in stock" box can't bring them back.
   - `TradingPostFragment.TogglePicker`: the give side passes `item => IsTradeableTo(item, partner)`; the get side
     `item => IsTradeableTo(item, me)`.
   - `TradeItems.SpecialItems()` keeps Beavers; the predicate hides it between factions.
   - Filter per call. `TradeItems.Groups()` is cached in a singleton: do not bake the filter into it.
   - `TradeItems.MostStocked` (the default good) must skip goods failing the predicate.
   - `ApplyPrefill` (Offer again, ledger rows, declined offers) must clear a side whose item fails the predicate.
4. **Binding check** (every computer, at replay, from saved state only):
   - In `ColonyExchangeService.WhyNotPropose`, next to the `IsKnownItem` lines, add
     `IsTradeableTo(give, receiver=partner)` and `IsTradeableTo(get, receiver=actor)`, with
     `receiverStores = FactionCatalog.GoodsOf(ColonyFactionService.FactionOfSlot(receiver)).Contains`.
   - Same in `Accept`'s `why` chain.
   - On failure, notice `…Trade.Notice.OfferFailedFaction`: "Your offer was not made: {0} can't be traded with a
     colony of another faction."
   - Also add to `Accept` the cheap check that the partner half's owner is still `theirs.Colony` (the gap the trading
     survey found).
5. **Running exchanges.** In `CheckTradingPosts`, next to `ColoniesChanged`: when on, end an open or offered exchange
   whose terms now fail `IsTradeableTo`, with notice `…Trade.Notice.VoidFaction`: "The exchange at a Trading Post
   ended: {0} can't go to a colony of another faction." This can only happen after a re-founding or a switch, and the
   switch requires no open exchange, so it's belt and braces.
6. **Beavers (D19).** In `MoveBeavers`, filter the movers to beavers whose `CharacterFaction.FactionId` equals the
   target colony's faction. Apply the same filter wherever movable adults are counted: `BeaversToSpare(half)`, `IsIn`,
   and the form's `StatusYourBeavers`. So an exchange never waits for beavers that can't go.
7. **Wishes.** `ColonyWishlist.Set` normalizes with `IsKnownItem && IsTradeableTo(item, receiver = the wishing
   colony)`. The wishlist picker passes the same predicate. In the partner's picker, a wish mark shows only on items
   that pass the current side's predicate.
8. **Trading Post half faction (D17).**
   - In `ColonyRulesService.JudgePairs`, after both halves are allowed: for each half, find the colony whose finished
     district road is at the half's entrance tile (Phase 0.4). If that colony's faction differs from the half's
     template faction, set `prefabName = FactionCatalog.TradingPostTemplateOf(faction)`.
   - Do this only when on, and only when that faction's post template exists.
   - The rewritten event is played on the host and sent on, so guests place the same thing.
   - The placement judge already allows any faction's Trading Post (Phase 3 step 6).
   - **(verify)** that the two post templates have the same footprint and `Layout: Half`. RuntimeChecks already
     proves they equal the game's District Crossing per faction, and the crossings are the same shape.
   - **Fallback:** if the entrance computation is not solid, skip this step (both halves keep the placer's model) and
     list it under Known limits. Nothing functional depends on it.
9. **Line on the post between factions.** On a Trading Post whose two colonies differ in faction, a muted line under
   the header, key `…Trade.BetweenFactions`: "Between factions: goods both factions use, and science. No beavers."
10. **Acceptance:**
    - Between a Folktails and an Iron Teeth colony, the give grid lists exactly the 17 goods (plus Science with
      separate science) and no Beavers.
    - Offer again with Carrot clears that side.
    - A hand-crafted offer event (RuntimeChecks, or a dev hook) of Corn is refused at replay with the notice.
    - Two Folktails colonies trade beavers as before.
    - A Folktails colony holding Iron Teeth beavers after a handover can't pass them to a Folktails neighbour: the
      round waits with "no beavers to spare".

### Phase 8: Handover and the rest

1. **Handover target (D18).**
   - Add pure `FactionRules.PreferSameFaction(IEnumerable<(int slot, long distance)> candidates, Func<int,string>
     factionOf, string fromFaction)`: the nearest of the same faction, else the nearest; ties go to the lower slot.
   - Refactor `NearestLiving` to collect distances, then call it.
   - Both callers keep their candidate sets.
2. **Re-founding** after a colony died is ordinary founding: it sets the slot's faction again.
3. **Mixed notice** for a game that asked for mixed but couldn't (Phase 1 step 2).
4. **Acceptance:** four colonies, Folktails A (dead) and C, Iron Teeth B and D. A goes to C even when B is nearer.
   With no Folktails colony alive, it goes to the nearest.

### Phase 9: Checks

See §9. Add them as you go; this phase is for the ones that span phases and for the counts.

### Phase 10: Documentation

See §10.

### Phase 11: Release

Follow the maintainer's release loop for the next free `1.4.0-betaN`. At this plan's writing, main is beta14, so this
is beta15 unless another release lands first; re-check the version before cutting.

- Bump `BeaverBuddies.csproj` `<Version>`, `manifest.json`, `BeaverBuddies/changelog.txt` (top entry), the
  STABILITY-CHANGELOG entry, and the version strings in `docs/index.html`, `install.html` and `troubleshooting.html`.
- Build **Release** and **Release Steam** into a scratch mods folder (`-p:BeaverBuddiesModsPath=<scratch>/`), so
  the installed mod is not overwritten.
- Build the zip in the previous release's entry order, with new files appended. Read long paths through `\\?\`.
- Generate `<ver>-SHA256SUMS.txt`.
- Commit. `git fetch`, and confirm `origin/trading-exchange` is an ancestor of `HEAD`. Push `HEAD` to
  `trading-exchange`, `main` and the branch. Create the annotated tag `v1.4.0-betaN` ("BeaverBuddies MultiColony
  1.4.0-betaN").
- Run `gh release create --repo timbermods/BeaverBuddies-MultiColony --prerelease --verify-tag` with notes derived from
  the previous release's (`gh release view <prev> --json body`). Always pass `--repo`: gh's default here is the
  Stability Fork.
- Afterwards: check that the asset hash matches (`gh release download`), that the CI "Tests" run is green on the tag,
  and that the Pages build ran.
- The notes and changelog say plainly: **not seen in a game**.

---

## 6. Determinism rules for this feature (read them before each phase)

1. **Simulation decisions** read only: the event's host-stamped fields; saved state (the slot → faction table,
   `CharacterFaction`); and static data identical on every computer (the catalog, built from the same game and mod
   files, which the join handshake already checks).
2. **Never** read `LocalFaction`, `ColonySession.LocalSlot` or anything display-only in a tick, a replay, a load, or a
   component's simulation path. `FactionOf` step 3 (owner-derived) is for models and UI only.
3. **Keep the synced RNG's use identical to vanilla:** the texture setter draws exactly once; the bot factory and
   creation scopes draw nothing.
4. **Nothing iterates a `Dictionary`/`HashSet` to decide simulation order.** The catalog's lists are ordered by the
   game's `Order` fields or by ordinal id.
5. **The switch (D12) and founding create entities only inside a replay**, through the same calls on every computer
   (see Phase 0.2 for entity ids).
6. Every change to the slot → faction table goes through `ColonyFactionService.Set` inside a replay, notes the digest,
   and appears in the daily fingerprint.
7. A load must reproduce what the save had. `CharacterFaction` is saved. A building's faction is its template. The
   table is saved.
8. The creation context is a stack popped in `finally` / finalizers. A leaked context would stamp later characters
   wrongly on one computer only.

---

## 7. Save and wire changes

- **Save (mixed games only):**
  - Singleton `BeaverBuddies.ColonyFactions { Mixed: bool, Base: string, Colonies: List<string "slot|faction"> }`.
  - Entity component `BeaverBuddies.CharacterFaction { Faction: string }` on beavers.
  - The game's own `FactionService.Id` stays the base faction.
  - A non-mixed save is byte-for-byte what `bf2264c` writes.
- **Wire:** `FoundColonyEvent.faction`; new `ColonyFactionSwitchEvent`; `InitializeClientEvent.hostFactions`. The join
  handshake already requires the same mod version.
- **Opening a mixed save without MultiColony** (the plain game or the Stability Fork) loads only the base faction. The
  game drops the other faction's buildings and characters as "loading issues", and they are **lost** if that copy is
  saved. Document it (Known limits, README, TWO-COLONIES); it cannot be prevented from inside the mod.

---

## 8. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Another `GetSingle`/`.Single()`/`Current` site throws with both factions loaded | Phase 1 acceptance places every building of both factions in dev mode, saves and loads. At 1.1.2.4 every `TemplateService.GetSingle<` site is covered: the rest (`BlockOccupierSpec`, `RecoveredGoodStackSpec`, `PlaneSpec`) resolve to one template. The only `GetAll<…>().Single` sites are the PlantingUI planter name (fixed) and an achievement's recipe count (unique name, fine). `TopBarPanel`'s `Goods.Single` is fine (Water and Badwater each have one good in the union). Every `FactionService`/`FactionNeedService` user is in §3.2 |
| R2 | A component reads `CharacterFaction` outside the creation window (context empty) | The lazy default is the base faction, plus a one-time warning; the loader peek covers loads; creation sites are enumerated in §3.2 |
| R3 | Deleting beavers and the district center in the switch leaves dangling references (reservations, jobs) | Only untouched colonies switch (idle beavers, no other buildings). Delete beavers first. Test a switch during work hours and at night |
| R4 | Shaft model sets built by reflection break on a game update | RuntimeChecks table of the constructors and fields; fallback to base models |
| R5 | Other mods that read `FactionService.Current` see the base faction | Known limit |
| R6 | Memory and load time rise (both factions' models and materials) | Known limit; measure the load time once and put it in the notes |
| R7 | Trading Post half rewrite picks the wrong colony | Host-only, falls back to the placer's; it's a model, not a rule |
| R8 | The toolbar refresh via `OnDevModeToggledEvent(null)` misses a button type | Acceptance step; RuntimeChecks confirms the method on `ToolButton` and `ToolGroupButton` |
| R9 | Border removal lands mid-work and moves `ColonyStamp`/`RoadOwner` | Fetch before each phase; §4.3 step 3 and D17 name the concept, not the class |
| R10 | Wishes or ledger prefill bring back a forbidden item | Predicate at every entry point (picker, prefill, wish normalize) and at the binding check |

---

## 9. Checks

### 9.1 StabilityTests (new `StabilityTests/FactionChecks.cs`; link `FactionSets.cs`, `FactionRules.cs`, `FactionTable.cs` in the csproj)

- **Factions: a table row round-trips.** Garbage rows are ignored, a missing slot reads as the base faction, rows
  outside 0–3 are ignored.
- **Factions: sets from collections.** Common items belong to no faction. An item listed by two factions is common. An
  item's sole faction is the one faction listing it. `ItemsOf(f)` is common ∪ f's.
- **Factions: the founding faction.** Mode off → no faction. Unknown → refused. Not in the host's list → refused.
  Available → allowed. The base faction is the fallback when the event has none.
- **Factions: switching.** One test per verdict (not mixed, not yours, same faction, unavailable, each "touched" fact
  alone), plus allowed.
- **Factions: placement.** Own faction, common and Trading Post of any faction are allowed. The other faction's
  building is refused. Mode off → everything allowed.
- **Factions: trade items.** Science only with separate science. Beavers only between equal factions. A good only if
  the receiver stores it. It is asymmetric: my foreign good may go to its own faction.
- **Factions: beaver crossing** needs equal factions.
- **Factions: handover** prefers the nearest same-faction colony, falls back to the nearest, lowest slot on ties.
- **Trade form: the two new verdicts**, and the fuzz invariant still holds with allow-all predicates.
- **Wishlist:** forbidden items are dropped on normalize.
- **Factions: every Harmony patch under `BeaverBuddies/Factions/` is gated** (source scan). For each
  `[HarmonyPatch]` class in those files, the text of its `Prefix`/`Postfix`/`Finalizer` bodies must mention
  `MixedFactions.IsOn`. Whitelist the Phase 1 decision prefix, which decides the flag.
- **Existing checks** cover the new setting's tooltip (≤ 112 chars, 2 lines) and "every Colony.* key has an English
  line".

### 9.2 RuntimeChecks (new `RuntimeChecks/FactionRuntimeChecks.cs`, registered in `Program.cs`)

Data (from `Blueprints.zip`, through the same `FactionSets` code, which also proves the adapter's logic):

- Exactly the factions Folktails and Iron Teeth. Each `StartingBuildingId` template exists. Both district centers
  have equal `BlockObjectSpec.Size` and `Entrance`.
- The shared template paths across the faction unions are exactly the 4 known ones. Template names are unique after
  de-dup.
- One `ModularShaftPartsSpec` and one `BotSpec` template per faction. The critical bot needs are `Biofuel`
  (Folktails) and `Energy` (Iron Teeth), each only in its faction.
- Material names are unique across the union. Decal ids are unique across factions. Worker outfits are unique by
  `(FactionId, Id, WorkerType)`.
- Each faction has a Box, a Pileable and a Liquid stockpile. The tradeable-between-factions set equals the 17 goods of
  D3. Every good's `GoodType` is one of the three.
- Plantables: each is listed by one faction or common, matching §3.2.
- The mod's `MultiColonyTradingPost.<F>` exists for every faction.

Game surface (a table like `ColonyRuntimeChecks.cs` ≈303–478): every type, method and field this plan patches or
reads by reflection exists, for example:

- `FactionService.Load`, `_singletonLoader`, `_sceneLoader`
- `FactionBlueprintModifierProvider.Initialize`
- `TemplateCollectionService.Load`, `AllTemplates` setter
- `WorldEntitiesLoader.InstantiateEntity`
- `NeedManager.GetNeeds`
- `WellbeingLimitService.GetMaxWellbeing`
- `BeaverTextureSetter.InitializeEntity`
- `BotFactory.Load/Create`
- `ShaftFrameFactory.Load`, `ShaftModelFactory.Load`, `ModularShaftModelUpdater.Awake` and `_modularShaftModelService`
- the PlantingUI planter-name method
- `WorkerOutfitService.Load/TryGetOutfitSpec`
- `DrivewayModelInstantiator.InstantiateModel`, `DynamicPathModel.Awake`
- `DecalService.Load`, `DecalButtonContainer.Show`, `DecalSupplier.InitializeEntity`
- `StockpileInventoryInitializer.Initialize`, `PlanterBuilding.GetAllowedPlantables`, `YieldRemovingBuilding.IsAllowed`
- `ToolButton.OnDevModeToggledEvent`, `ToolGroupButton.OnDevModeToggledEvent`, `ToolButtonService._toolGroupButtons`
- `PopulationWellbeingBox.GetPanel`, `BasicStatisticsPanelFactory.Create`
- the goods-UI factories of Phase 6
- `GameOverBox`, `GameWonderCompletionService`, `TutorialService`, `TutorialTriggers`
- `NewbornSpawner.SpawnAdult/SpawnChild`, `BeaverFactory.CreateAdultFromChild`, `BotManufactory.OnProductionFinished`
- `GameSceneLoader.StartNewGame`, `FactionUnlockingService.IsLocked`

Events:

- `ColonyFactionSwitchEvent` overrides `GetColonyScope` and is on the Global review list.
- A JSON round trip of `FoundColonyEvent.faction` and `InitializeClientEvent.hostFactions`.
- `BeaverBuddies.CharacterFaction` and `BeaverBuddies.ColonyFactions` collide with no existing key.

### 9.3 Solo host script (add to `ALPHA-TEST-SCRIPTS.md` as a new script; the host alone, debug on)

1. Mod Settings: Separate colonies on, Mixed factions on. Start a new game as Folktails on a standard map.
   - Log: `[Factions] Mixed factions: on (base Folktails…)`.
   - The toolbar has only Folktails and common buildings.
2. Unpause. Ctrl+Shift+K to colony 2. Ctrl+K opens the chooser with Folktails and Iron Teeth. Found as Iron Teeth.
   - Iron Teeth district center, beavers with Iron Teeth fur, the toolbar is Iron Teeth.
   - The Ctrl+T card shows the Iron Teeth logo.
3. In colony 2, build Iron Teeth housing, farms, a bot assembler and a charging station.
   - A bot is made. Its needs panel shows Energy, not Biofuel.
   - The Iron Teeth farmhouse lists Iron Teeth crops only.
   - An Iron Teeth warehouse lists Iron Teeth and common goods only.
4. Back in colony 1 (Folktails), do the same with Folktails buildings.
   - Try placing an Iron Teeth building in dev mode: refused with "That building belongs to another faction."
5. Ctrl+Shift+K to colony 3. Found as Folktails. Before building anything, Ctrl+T → "Play Iron Teeth instead".
   - The district center and beavers become Iron Teeth.
   - Build a path, and the button is gone.
6. Build a Trading Post between colonies 1 and 2.
   - Each half shows its own faction's model (or the placer's, if D17 fell back).
   - The give grid lists the 17 shared goods (+ Science) and no Beavers. Offer 50 Logs for 10 Gears; it runs.
7. Save, quit to menu, load. Every faction, fur, need list, model and toolbar is as before. Heartbeat and daily check
   are green for 3 days.
8. A new game with Mixed factions on but separate colonies off: a normal shared game, log "off".

### 9.4 Two-player script (two computers)

1. The host (Iron Teeth unlocked) makes a mixed game as Iron Teeth. The guest (Iron Teeth **locked** on their
   profile) joins.
   - The founding chooser offers both. The guest founds as Iron Teeth: allowed (D1).
2. The host sets up a Folktails colony (seat flip before the guest joins, or on a 3-start map) and trades with the
   guest: 17 goods, no beavers.
3. On a multi-start map, the guest gets the start dialog after the first tick, switches to Folktails, and then builds.
4. After an in-game week, the desync checks are green. Save.
5. The guest hosts the save, with Iron Teeth locked on their profile. Iron Teeth is no longer offered to new
   foundings; existing Iron Teeth colonies play on.

---

## 10. Documentation (every new player-facing string is English only)

- **`TWO-COLONIES.md`:**
  - New section **Mixed factions (beta)** after *Starting*: the setting and its conditions; how each colony gets its
    faction; switching while untouched; what a colony can build, plant and store; beavers and bots; trading between
    factions (the 17 goods, Science, no beavers); handovers between factions; what you see.
  - Rows in *Keeping colonies apart* for planting, gathering and storage by faction.
  - *Known limits* additions (§11).
  - *How it works*: the union, the catalog, `CharacterFaction`, the saved table.
- **`README.md`:** a short *Mixed factions* subsection under *Start a game*, and the new setting in the settings list.
- **`STABILITY-CHANGELOG.md`:** a feature-sized entry in the beta7 style. Open with a bold summary sentence, then
  bold-led bullets naming the code, then:
  - **Save change.** and **Wire change.**
  - `- Docs: …`
  - `- Checks: StabilityTests N (k new: …); RuntimeChecks M (j new: …). Both builds, 0 warnings.`
  - `- Not seen in a game. Script <X> is this release's.`
- **`BeaverBuddies/changelog.txt`:** one or two plain `*` lines, then the "Full details…" line.
- **Site:**
  - `docs/index.html`: the `#new` section for the release; a `#features` card "Folktails and Iron Teeth together";
    `#limits` bullets; the check counts in the two places noted in the memory of earlier releases.
  - `docs/install.html#settings`: the new setting.
  - `docs/faq.html#compat`: a mixed save needs MultiColony.
- **`ALPHA-TEST-SCRIPTS.md`:** scripts 9.3 and 9.4.
- **`design/TWO-COLONY-ALPHA-PLAN.md`** line "Two factions … not supported": leave the history, and add a pointer to
  this plan.

---

## 11. Known limits (documented, not fixed)

- A mixed save needs MultiColony. Elsewhere it loses the other faction's buildings and characters (§7).
- Only new games can be mixed. A shared save, or an older separate-colonies save, stays one faction.
- After a handover between factions, a colony runs the other faction's buildings and beavers it received but can't
  build more of them. Its toolbar is its own faction's. Bots of the other faction need that faction's charging or
  fuel buildings, which still work if they came with the colony.
- Achievements, the Iron Teeth unlock goal and the load menu's faction icon use the base faction. "Build every
  structure" can't be earned in a mixed game.
- Tutorials are off in mixed games.
- Other mods that read `FactionService.Current` see the base faction.
- Loading both factions uses more memory and takes longer to load.
- Iron Teeth availability follows whoever hosts (D1).
- If D17 or per-faction shafts fell back: Trading Post halves show the placer's model; power shafts show the base
  faction's model.

---

## 12. Working rules for the implementing session

- Read §2 and §6 again at the start of each phase. D1–D6 are the maintainer's: don't change them. If one proves
  impossible, stop and report.
- Everything new is gated (D22). When unsure whether a code path is simulation or display, treat it as simulation and
  key it on template or saved state.
- Match the surrounding code:
  - pure rules in `*Rules`/`*Table`/`*Sets` files with `System.*` only;
  - game-bound `*Service` classes as `RegisteredSingleton`;
  - `XxxPatcher` classes;
  - comments in the repo's style: why, not what, with a dated excerpt of the game code a patch depends on (see
    `TradingPostToolDisabler.cs`).
- UI through `Util/NativeElements.cs` and the game's own classes. Nothing hand-drawn.
- Strings: enUS only; short, second person, British spelling; keys `BeaverBuddies.Colony.Faction.*`,
  `BeaverBuddies.Colony.Trade.*`, `BeaverBuddies.Colony.Refused.*`, with no digits in keys.
- Run both suites and both builds at the end of every phase. Record the counts for the changelog.
- Log lines are prefixed `[Factions]`, one line per decision, never per tick.
- Be honest in every document: this is **not seen in a game** until someone plays scripts 9.3 and 9.4.

## 13. Phase 0 findings

*(The implementing session fills this in.)*
