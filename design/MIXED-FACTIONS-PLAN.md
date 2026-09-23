# Mixed factions: Folktails and Iron Teeth colonies in one game — investigation, design and implementation plan

This is the build plan for letting each colony in a separate-colonies game play its own faction, with faction
choice built into the waiting rooms (New Game and Load Game) so that multiplayer and Folktails-with-Iron-Teeth read as
native parts of Timberborn.

- **Revision 2, 2026-09-22.** Refreshed against **`61c8451` (1.4.0-beta19)**.
- **Revision 1** was written against beta14 (`bf2264c`). Since then:
  - colony land was removed (beta15);
  - Trading Posts can be placed before roads (beta16);
  - the waiting rooms were added, for new games (beta18) and for saves hosted from the main menu (beta19).
- The game facts come from a decompile of all 497 `Timberborn.*.dll` of Timberborn **1.1.2.4**.

Labels:

- **(read)**: checked against the decompiled game or the repo at `61c8451`. Trust it, but a line number may have moved.
- **(decision)**: D1–D8 are the maintainer's; D9 onward are this plan's. Don't reopen them. If one proves impossible,
  record why in §14 and take the stated fallback.
- **(verify)**: a belief not fully checked. Settle it at the step that needs it and record the answer in §14.

Game references are `Assembly: Class.Member`. Decompile with
`ilspycmd "<Managed>/Timberborn.X.dll" -r "<Managed>" -o <scratch>/dec`. Read game blueprints from
`Timberborn_Data/StreamingAssets/Modding/Blueprints.zip`, and the game's UXML/USS from `Modding/UI.zip`, both with
Python `zipfile`. Extracting into the scratchpad hits Windows MAX_PATH.

---

## 1. Goal and exit criteria

In a separate-colonies game each colony plays its own faction, chosen in the waiting room. For example: the host's
colony is Folktails, a friend's is Iron Teeth, on one map, trading through Trading Posts. Everything a player sees
and does for their colony is their faction's: toolbar, beavers and bots (looks, needs, food), buildings, paths,
storage, goods lists. The simulation stays identical on every computer.

Done means:

1. **Choosing in the waiting room.** With *Mixed factions* on, every player picks a faction there, with the game's
   own faction switcher (name plate and arrows). Each row shows that player's faction logo. In a hosted save, each row
   shows the player's colony and its faction, read from the save.
2. **The game follows the choices.**
   - A multi-start map places each start in its player's faction.
   - On a standard map, a guest's founding uses the faction they picked; a chooser covers anyone who didn't pick.
   - An untouched colony can still switch faction in the game (the fallback).
3. **Loading works.** A mixed game loads both factions with no exception in `Player.log`. That covers every building
   of both factions, bots of both, births, children growing up, saving and loading.
4. **Each thing is its own faction.** Each beaver and bot has its own faction's needs, looks and outfits; each
   building its own faction's model.
5. **The toolbar and display follow the local colony's faction** (steward switch, handover, founding, faction switch
   included).
6. **Trading follows D2 and D3.** Science still trades.
7. **Off means unchanged (D22).** A game without Mixed factions behaves exactly as beta19. So does a room or save
   without it: same frames' meaning, same UI.
8. **Checks and docs.**
   - StabilityTests and RuntimeChecks pass, and both builds have 0 warnings.
   - Docs, changelog and site describe the feature honestly as **not yet played**.
   - Released as the next beta.

---

## 2. Decisions

### From the maintainer

- **D1: The host's unlocks count.** Iron Teeth is available when it is unlocked on the hosting computer's profile
  (`FactionUnlockingService`). A guest's own profile doesn't matter. When a different player hosts the save later,
  their profile counts from then on.
- **D2: No beaver trades between factions.**
- **D3: Goods only where the receiver can store them.** A good may go to a colony only if that colony's faction has a
  warehouse, pile or tank setting for it. **(read)** Every faction has all three stockpile types and every good is
  Box, Pileable or Liquid, so the rule is exactly: *the good is in `GoodCollection.Common` or the receiving faction's
  `GoodCollection`*. Between Folktails and Iron Teeth that leaves these 17 goods:
  - Box: Gear, PineResin, Explosives, Fireworks, BotChassis, BotLimb, BotHead, Berries.
  - Pileable: Log, Plank, TreatedPlank, ScrapMetal, MetalBlock, Dirt.
  - Liquid: Water, Badwater, Extract.
- **D4: Shared-colony games stay single faction.**
- **D5: No land.** Borders are gone (beta15): build anywhere, and roads never join except at a Trading Post.
  Ownership is `DistrictOwner`, `ColonyStamp` (`Colonies/ColonyStamps.cs`) and the mark tables. Nothing here may
  reintroduce land.
- **D6: Trading Posts remain the only way two colonies' roads join.**
- **D7: Pick the faction in the waiting rooms.** The choice is made in the New Game waiting room and, where it still
  matters, the Load Game waiting room. The UI is built from Timberborn's own menu pieces so it looks native.
- **D8: Base 1.4.0-beta19; the work ships as 1.4.0-beta20.** Take liberties where needed, and do not stop before the beta20 release.

### This plan's

- **D9: One new host setting.** *Mixed factions for new games (beta)*, default **off**.
  - It takes effect only if *Separate colonies for new games* is on **and** every faction is unlocked on the host's
    profile. Otherwise the game is a normal one-faction game, and a notice says why.
  - It is read when a new game is made: by the waiting room when it opens, or by the solo New Game's Start.
  - A save keeps its mode for life.
- **D10: A mixed game loads every faction**, de-duplicated: the union of all factions' template, good, need and
  material collections, and their blueprint modifiers.
  - `FactionService.Current` stays the **base faction**: the host's faction-page choice, saved by the game as usual.
  - Anything not routed per colony keeps using it.
- **D11: Each colony (slot) has a saved faction.** An entry is written when the colony gets its district center:
  - a new game's starts: slot 0 is the base faction, and each multi-start slot is its lobby player's choice, else the
    base faction;
  - a founding: the founder's faction;
  - the untouched switch (D14).

  A slot with no entry has never had a district center. A colony that died and is founded again takes its new
  choice.
- **D12: Every entity carries its own faction, whoever owns it now.**
  - A building's is its template's: the one faction whose template collections list its blueprint. A template listed
    by none or several is **common** (`Path`, `DevPowerGenerator`, `DevWaterSource`, map objects).
  - A bot's is its template's.
  - A beaver's is stored on it (new saved component `CharacterFaction`), set at creation (D13), and never changes.
  - For display only, a common building (a path) takes the owning colony's faction, else the base faction.
- **D13: A new character's faction.**
  - Lodge or breeding-pod births: the spawner building's.
  - Bot assembler: its own (which also picks the bot template).
  - A child growing up: the child's.
  - Starting beavers: the start's faction. The base faction, unless a multi-start start is placed for another; the
    start placement pushes its slot's faction.
  - Founding and switch: the chosen faction.
  - Dev spawns: the local colony's.
  - Anything else: the base faction, with one warning in the log.
- **D14: The untouched switch (fallback).** A colony that has only its district center(s) may switch faction by a
  synced action of its own seated player. "Only its district center" means: no other stamped building, construction
  site or path; no marks; no offer or exchange; no unlocks with separate science.
  - It replaces the district center in place. Both factions' are the same 3×3×5 footprint with the same entrance
    **(read)**.
  - The stock moves across, and the beavers are replaced (as many adults and children as there were, up to the
    starting numbers).
  - It exists for games made solo and hosted later, for players who joined a hosted save after its start, and for a
    wrong pick.
- **D15: Founding uses a known faction first.**
  1. The faction the player picked in a waiting room (kept on their own computer until used).
  2. Else, if the game is mixed, a chooser in the founding dialog.
  3. Else the base faction.

  The founding event carries the faction, and the host checks it is available (D1).
- **D16: The toolbar shows the local colony's faction plus common tools.** The local colony is
  `ColonySession.LocalSlot`, so a steward's acting colony counts. Dev mode shows everything, as the game does.
- **D17: A colony places only its own faction's and common buildings.** The host refuses others; Trading Posts of
  any faction are exempt. Foundings and switches are host-authored.
- **D18: Buildings work only with their own faction's things.** This is a simulation rule keyed by the building's
  template, so it is identical on every computer:
  - stockpiles hold only goods their faction can store;
  - planters plant only their faction's and common plantables;
  - yield removers take only yields whose good their faction uses.
- **D19: Needs are per character.** The union would starve every bot. **(read)** `Biofuel` (Folktails) and `Energy`
  (Iron Teeth) are critical bot needs, stop work, and cost −3 wellbeing. Per-character needs also mean a beaver never
  eats the other faction's food (appraisal returns 0 for a need it lacks). Only Berries and Water are shared foods.
- **D20: A beaver crossing a Trading Post must match the target colony's faction.** This covers foreign beavers a
  colony got in a handover.
- **D21: A handover prefers the nearest living colony of the same faction**, else the nearest. Entities keep their
  faction. The receiver keeps its own faction for its toolbar and new buildings, and can still run what it received.
- **D22: Off means unchanged.** Every patch and hook returns immediately unless mixed. The waiting room of a
  non-mixed game sends exactly what beta19 sends. StabilityTests enforce the gate with a source scan (§10).
- **D23: Each Trading Post half, when placed, takes the faction of the colony whose road is at its entrance.** This
  is optional polish (Phase 9). Posts can be placed before roads (beta16), so it often falls back to the placer's
  faction. It is a model only.
- **D24: Display follows the local colony's faction.** This covers wellbeing panels, the faction icon, avatars in
  empty slots, game over, Wonder screens, and the good-statistics tab. Tutorials are off in mixed games (they are
  Folktails-only).
- **D25: Left as the base faction.** Achievements, the faction-unlock goal, and the load menu's metadata.

---

## 3. Facts about the game (read, 1.1.2.4)

### 3.1 One faction per save

- `Timberborn.GameFactionSystem: FactionService`.
  - `Current` is saved under singleton `FactionService` / `Id`.
  - `Load()` reads the save, or for a new game `GameSceneParameters.NewGameConfiguration.FactionId`. Then
    `SetCurrentFaction` calls `FactionBlueprintModifierProvider.Initialize(Current.BlueprintModifiers)`, which asserts
    it runs once. Both factions' modifiers are empty in vanilla.
- **Faction providers**, each unioned with a Common provider:
  - `FactionTemplateCollectionIdProvider` → `TemplateCollectionService.Load`, which concatenates with **no de-dup**.
  - `FactionGoodCollectionIdsProvider` → `GameGoodFilter.Load`, a set union.
  - `FactionNeedCollectionIdsProvider` → `FactionNeedService.Load`, a set union.
  - `FactionMaterialCollectionIdsProvider` → `MaterialRepository.Load`, which does `Distinct` and throws on a duplicate
    name.

  All of them are multi-bound. A mod can add its own provider with `MultiBind<…>().ToExisting<>()`; no Harmony is
  needed.
- **Load order.** `SingletonLifecycleService.LoadAll` runs every `ILoadableSingleton.Load` in repository order, then
  entities, then `PostLoad`. `FactionService.Load` runs before the collection loads, because they call `Current`.
- **Unlocks.** Iron Teeth carries `UnlockableFactionSpec { PrerequisiteFaction: Folktails, AverageWellbeingToUnlock:
  15 }`. Unlocks are per profile: `FactionUnlockingService.IsLocked(spec)`, bound in MainMenu and Game.
- **New game entry points:**
  - (a) the solo New Game's Start: `NewGameModePanel` → `GameSceneLoader.StartNewGame(NewGameConfiguration)`;
  - (b) **the waiting room**: `LobbySession.Start` calls `sceneLoader.LoadScene(GameSceneParameters
    .CreateNewGameParameters(new NewGameConfiguration(Setup.FactionId, …)))` **directly** (`Lobby/LobbySession.cs`
    ≈163), bypassing `GameSceneLoader.StartNewGame`.

### 3.2 What loading both factions does

- **Shared template blueprint paths:** `Characters/Beaver/BeaverAdult`, `Characters/Beaver/BeaverChild`,
  `Buildings/Power/DevPowerGenerator/DevPowerGenerator`, `Buildings/Water/DevWaterSource/DevWaterSource`.
  - Every other building's template name ends `.Folktails` / `.IronTeeth`.
  - `Path` is a common template (old save names `Path.Folktails` / `Path.IronTeeth`).
  - Plantable template names are unsuffixed; their faction comes from `NaturalResources.<F>`.
- **Clashes:** 55 recipes, no duplicate ids. No two material paths share a name. `GoodOrder` is unique over all 60
  goods.
- **Needs.**
  - Faction beaver needs are bonus-only, except Folktails `BeeSting` (−1, hidden unless stung).
  - Bot needs: Folktails `Biofuel` (critical, start 0.8), `Catalyst`, `PunchCard`. Iron Teeth `Energy` (critical,
    start 0.5), `Grease`, `ControlTower`.
- **Crash sites with both loaded.** Every `TemplateService.GetSingle<` site and every `GetAll<…>().Single` was checked:
  - `TemplateNameMapper.Load`: "Duplicate template name" (fixed by de-dup).
  - `BeaverFactory.Load`: `GetSingle<AdultSpec>` / `GetSingle<ChildSpec>` (fixed by de-dup).
  - `Timberborn.Bots: BotFactory.Load`: `GetSingle<BotSpec>` (two bot templates).
  - `Timberborn.ModularShafts: ShaftFrameFactory.Load` and `ShaftModelFactory.Load`: `GetSingle<ModularShaftPartsSpec>`.
  - `Timberborn.PlantingUI`: the planter-name lookup (`.Single` over `PlanterBuildingSpec` of a resource group, ≈1461)
    throws when the bottom bar is built.
  - `Timberborn.WorkerOutfitSystem: WorkerOutfitService`: keyed `(Id, WorkerType)` and filtered by `Current`.
    Unfiltered it throws; filtered, the other faction's bots get hats their template lacks, and
    `GetAttachmentDefinition` throws.
  - A union of needs starves every bot.

  Fine as they are: `BlockOccupierSpec`, `RecoveredGoodStackSpec` and `PlaneSpec` (one each), an achievement's
  `.Single` over a unique name, and `TopBarPanel`'s `Goods.Single` (Water and Badwater each have one good).
- **Entity creation and load.**
  - `EntityService.Instantiate` builds every component, then `SetActive(true)` runs every `Awake` synchronously.
  - `NeedManager` builds its needs in `Awake` (private `GetNeeds()`).
  - A saved entity: `WorldEntitiesLoader.InstantiateEntity(SerializedEntity, …)` (Awake) → `EntitiesLoader`: `Load`,
    then PreInitialize, Initialize (`InitializeEntity`) and PostInitialize.
  - The serialized data is in hand before Awake: `new EntityLoader(serializedEntity).TryGetComponent(key, out …)`.
- **Creation sites:**
  - `Timberborn.Reproduction: NewbornSpawner.SpawnAdult/SpawnChild(spawner)`
  - `Timberborn.Beavers: BeaverFactory.CreateAdultFromChild(child)`
  - `Timberborn.GameStartup: StartingBeaversInitializer.Initialize(position, …)`
  - `Timberborn.BotsUpkeep: BotManufactory.OnProductionFinished` → `BotFactory.Create(pos, rot, init)`
  - dev tools in `Timberborn.BeaversUI` and `Timberborn.BotsUI`
  - MultiColony's founding and multi-start
- **Starting goods** are hard-coded `"Berries"` and `"Water"`, both Common. MultiColony's founding hard-codes them too.
- **Per-entity `Current` uses and what is in hand at each:**

  | Use | Where | In hand |
  |---|---|---|
  | Textures | `BeaverTextureSetter.InitializeEntity`: one draw from the synced RNG; 5 adult and 3 child textures per faction | The beaver |
  | Avatars | `BeaverEntityBadge.GetEntityAvatar`, `BotEntityBadge.GetEntityAvatar` | The entity |
  | Empty dwelling and worker slots | `CharacterButton.ShowAdultEmpty/ShowChildEmpty/ShowBotEmpty` | Nothing |
  | Paths and gates | `DynamicPathModel.GetModelVariant` (Awake) | The path or gate |
  | Driveways | `DrivewayModelInstantiator.Load` caches the material; `InstantiateModel(model, blockObject, …)` applies it | The building |
  | Decals | `DecalService.Load` filters by `FactionId` (ids never collide) | — |
  | Planters | `PlanterBuilding.GetAllowedPlantables` (private, used in Awake) | The building |
  | Yield removers | `YieldRemovingBuilding.IsAllowed(YielderSpec)` | The building |
  | Stockpiles | `StockpileInventoryInitializer.Initialize` → `AddAllowedGoodType` (every loaded good of its type) | The building |
  | Wellbeing | `WellbeingLimitService.GetMaxWellbeing(tracker)`: display and achievements only; tiers are absolute | The tracker |
  | Population box | `PopulationWellbeingBox` (the only global needs list) | — |
  | Faction icon | `BasicStatisticsPanelFactory.Create` | — |
  | Game over, Wonder, tutorials | `GameOverBox`; `GameWonderCompletion(UI)`; `GameSound.PlayWonderLaunchSound`; `TutorialSystem` | — |
  | Starting district center | `StartingBuildingSpawner.Load` (`Current.StartingBuildingId`); used for the first colony and by MultiColony's founding | — |
  | Fine with the union | `Attractions`, `Effects`, `NeedApplication`, `SoakedEffects`, `FactionGoalsSystem`, `GameFactionSystemUI`, `Achievements` (D25), menus and editor | — |

- **Storage.**
  - Folktails: Small, Medium and Large Warehouse; SmallPile, LargePile, UndergroundPile; Small, Medium and Large Tank.
  - Iron Teeth: Small, Medium and Large Warehouse; Small and Large IndustrialPile; Small, Medium and Large Tank.
- **Plantables.**
  - Folktails: Dandelion, Carrot, Cattail, Potato, Spadderdock, Sunflower, Wheat, ChestnutTree, Maple.
  - Iron Teeth: CoffeeBush, Canola, Cassava, Corn, Eggplant, Kohlrabi, Soybean, Mangrove.
  - Common: BlueberryBush, Birch, Oak, Pine, Succulent.
- **Worker outfits.** All ids exist in both factions except Pilot (Iron Teeth). Farmer, Forester and Scavenger differ
  by a faction hat. The shared beaver template carries both factions' hats; each bot template carries only its own.
- **Toolbar.**
  - `BottomBarPanel.Load` builds it once.
  - Visibility is `ToolButton.ToolEnabled` over multi-bound `IToolDisabler`s (dev mode and the editor turn every tool
    on).
  - `ToolGroupButton.IsVisible` = any tool enabled, applied at `PostLoad` and on `OnDevModeToggledEvent`. Buttons
    re-check on `OnDevModeToggledEvent` and `OnToolGroupEnteredEvent`.

### 3.3 The game's menu UI for factions (read, `UI.zip`)

- **`Views/MainMenu/NewGameFactionPanel.uxml`:** the New Game template with a `FactionList` (`faction-list
  content-row-centered`) and a yellow `UnlockCondition` label (`unlock-condition__text`).
- **`Views/MainMenu/NewGameFactionItem.uxml`**, the faction card:
  - the unselected state (`normal-faction`): a `LogoRing` (`faction-item__logo-background content-centered`, 52px,
    `bg-circle-big-1`) holding `Logo` (`faction-item__logo`, 48px), plus a shadow and an avatar;
  - the selected state: `faction-item-selected__background` (352×355, `faction_characters_bg`), a ring, the big
    avatar, the name plate `faction-item-selected__name` (284×46, `faction_name`) holding a `name__text` label (white,
    14px), and the arrows `arrow--left` / `arrow--right` (34×57, `left_arrow` / `right_arrow`, hover variants, click
    sound) at `faction-item-selected__left-arrow` / `__right-arrow`.
  - Styles live in MainMenuMiscStyle (`faction-*`, `name__text`, `unlock-condition__text`) and CommonStyle (the arrows,
    `map-item-faction-icon` 40px).
- **In-game faction icon idiom:** a diamond `bg-diamond-1` background with the logo
  (`basic-statistics__faction-icon` 36px and `population-counter__faction-icon-background` 40px, PopulationStyle).
  Main-menu classes don't exist in the Game scene, and the in-game ones don't exist in the menu (see
  [[timberborn-native-ui-classes]]).
- **`FactionSpec`** has `Logo`, `Avatar`, `DisplayName` (`Faction.<Id>.DisplayName`) and `NewGameFullAvatar`.

---

## 4. Facts about the mod at beta19 (read)

- **Colony state.**
  - Slots are `int` from 0 (`ColonySlotTable.MaxSlots = 4`).
  - Singletons are keyed `BeaverBuddies.<Name>`, and save only in separate-colonies games.
  - Services are `RegisteredSingleton` with a `static Instance`. `SingletonManager.Reset()` resets
    `IResettableSingleton`s; the MainMenu configurator calls it.
  - Patches reach services through `Instance`.
- **Ownership (D5).**
  - `DistrictOwner` (district centers).
  - `ColonyStamp` in `Colonies/ColonyStamps.cs`: a building's colony, with unstamped resolution by district → road at
    entrance.
  - `ColonySeparation.SimOwnerOf` for simulation.
  - `ColonyGameWorld : IColonyWorld, IColonyRoadMap` with `RoadOwnerAt(cell)` and `EntranceOwnerAt(cell)`.
  - The pure road rule is `ColonyRoadRule`. The game's `PositionedEntrance.Coordinates` is the cell **outside** the
    door.
- **New game, separate colonies.**
  - `MultiStart/MultiStartPatches.cs` prefixes `StartingBuildingInitializer.Initialize`, reads
    `Settings.SeparateColoniesForNewGames`, and calls `ColonyModeService.Enable`.
  - On a multi-start map it places **every** start at creation, under `DistrictOwner.PendingSlot` = the location's
    `StartingLocationPlayer.PlayerIndex`, then deletes the locations.
  - `GameInitializerSpawnBeaversPatcher` spawns each start's beavers through the game's `StartingBeaversInitializer`.
- **The waiting room** (`Lobby/`, `TimberNet/Lobby*.cs`):
  - `LobbySetup` holds a new game (`FactionId`, `Map`, `Mode`, `ModeLocKey`, `SummaryText`, `Settlement`) or a save
    (`Save`, `SaveBytes`, `Cycle`, `Day`).
  - `LobbySession.Open` builds `LobbySummary` + `LobbyRoom` and starts the server in lobby mode.
  - `LobbySession.Start` closes the room (`ColonySession.CloseJoiningAtStart()`). For a new game it loads a
    single-player new-game scene; for a save it sends the bytes at once.
  - `LobbyWorldMaker`: at `NewGameInitializedEvent` it calls `SeatInRoomOrder`. That fills `ColonySlotTable` by
    `Resolve` in room order (host, then each `StartedWith` guest that has a `StableId`), then saves at tick 0; the host
    and every guest then load that save.
  - `LobbyRules.StartsToFill(playersField, guests)`: a multi-start map fills one start per player in the room.
  - `LobbyRoom.ColonyOf(index, separate)` shows host = colony 1, guests 2–4, then helpers (0).
- **Waiting-room frames** (`LobbyFrames`):
  - Host → guest: `LobbyWelcome` (`you`, `summary`), `LobbyRoster` (`players[]` of `LobbyPlayer {n, name, ready,
    host, joining, colony}`), `LobbyState`, `LobbyEnd`.
  - Guest → host: `LobbyHello {id, name}` and `LobbyReady {ready}`, handled in `TimberServer.HandleLobbyFrame`, with
    `LobbyMember` holding `ClaimedId`/`Name`/`Ready`.
  - `LobbySummary {faction, map, mode, settlement, host, separate, save?}`.
  - Parsing is strict (a bad frame is dropped); everything is display only.
  - `LobbyInbox` is the guest's copy; `LobbySnapshot`/`LobbyMemberInfo` the host's.
- **Waiting-room UI** (`LobbyPage`):
  - The game's `MainMenu/NewGameTemplate` page: the banner, capsule title, Back and Next.
  - The Game Mode summary plate with the faction page's logo ring. For a save the ring is hidden: the metadata has no
    faction.
  - A gold line (settlement, or the save's name and date).
  - The Mods window's board of `Modding/ModItem` rows. `ModIcon` shows the room's faction logo; `ModVersion` shows the
    tag "Colony 2".
  - The faction page's yellow status line, and the Invite button.
  - Main-menu classes only (see `LobbyPage.ClassesUsed`; RuntimeChecks checks them against `UI.zip`).
  - `LobbyHostPanel.OpenFrom(NewGameModePanel)` (Host co-op game on the Game Mode page) and
    `OpenForSave(save, bytes)` (Load Game → Host co-op game, main menu only). `LobbyGuestPanel` is the guest's.
- **Joining at tick 0.** `ColonySession.JoiningClosedAtStart` is true after a waiting room, and goes to guests in
  `InitializeClientEvent.joiningClosedAtStart`. `ColonyRules.WaitsForStart(foundingOrHandover, hostTicks,
  joiningClosedAtStart)` then lets founding happen at tick 0, even while paused.
- **Founding** (`Colonies/ColonyFoundingService.cs`):
  - `OfferFounding` shows a `DialogBoxShower` box (Place / cancel), re-offered once the game may start. Ctrl+K opens
    the same flow.
  - The tool is built from `StartingBuildingSpawner.StartingBuildingTemplateSpec`.
  - `FoundColonyEvent {coordinates, orientation, isFlipped, startingSettings}`; the host rewrites `startingSettings`
    in `ColonyRulesService.Judge`.
  - `Found()` replays: judge again → `CreateAsFinished` under `PendingSlot` → Berries and Water → `BeaverFactory`
    beavers.
- **Events.**
  - A `ReplayEvent` subclass overrides `GetColonyScope()` and optionally `ChangesGame()`.
  - Serialization is Newtonsoft `TypeNameHandling.All` over public fields.
  - The host stamps `slot` and may rewrite fields before playing and sending.
  - RuntimeChecks keeps the Global-scope and `ChangesGame()==false` review lists.
- **Hello.** `InitializeClientEvent` carries host choices to guests; `ColonySession.BeginHostSession` /
  `AdoptHostChoice` latch them.
- **Desync.** `ColonyDigest.Note` (heartbeat) and `ColonyDiagnostics.Fingerprint()` (daily; `flags` hold the modes).
- **Local vs acting colony.**
  - Display: `ColonySession.LocalSlot`, `ColonyScienceService.DisplaySlot`, `ColonyViewService`.
  - Simulation: the event's `slot`, or the entity's owner. **Never the local slot in a tick or replay.**
- **Toolbar locks.** `ColonyScienceService.RefreshToolLocks()` runs at every seat, steward, handover, mode and
  founding change. Hook the faction refresh onto it. `TradingPostToolDisabler` is the `IToolDisabler` precedent.
- **Trading.**
  - Items are strings (a good id, `ExchangeTerms.Science`, `ExchangeTerms.Beavers`).
  - `TradingPostGoodPicker` is fed by `TradeItems` (cached `Groups()`).
  - The form verdict is the pure `TradeOfferForm.Judge`.
  - The binding check is `ColonyExchangeService.WhyNotPropose` / `Accept` (replay, deterministic).
  - `CheckTradingPosts` runs every 8 ticks. Beavers move in `MoveBeavers`. Wishes: `ColonyWishlist` + `WishlistTerms`.
- **Trading Post halves.** Two entities of the same template, two `BuildingPlacedEvent`s, paired on the host by
  `ColonyRulesService.JudgePairs`. `LinkedBuilding` links them without comparing templates. Since beta16 a post may
  be placed with no roads.
- **Handover.** `ColonyLifecycle.NearestLiving(from, candidates)` in `ColonyHandover.cs`.
- **Saves are zips.** The `world.json` keys are `GameVersion`, `Timestamp`, `Singletons` (a dict, before
  `Entities`) and `Entities`. Useful singletons:
  - `FactionService.Id`
  - `BeaverBuddies.ColonyMode.Enabled`
  - `BeaverBuddies.ColonySlots.Table` (`"slot|id|name"` lines)
- **Conventions.**
  - Pure classes (`*Rules`, `*Table`, `*Terms`) are linked into StabilityTests.
  - Patch classes are `XxxPatcher`, installed by `harmony.PatchAll()`.
  - In-game UI uses `Util/NativeElements.cs`; the menu uses menu classes only.
  - Strings go in enUS only (British spelling, short, second person, keys without digits).
  - Mod Settings tooltips: 1–2 lines of at most 112 characters.
  - Checks at beta19: StabilityTests 373, RuntimeChecks 339.

---

## 5. Architecture

```
MainMenu                                     world-making scene (host)             every computer, loaded game
 Settings.MixedFactions ─┐
 host unlocks ───────────┼─► LobbySetup.Mixed ─► LobbySummary.mixed/factions
 lobby picks (frames) ───┘   LobbyMemberInfo.Faction      │
 solo Start ──► NewGameFactionCapture                     ▼
                              MixedFactions.Decide (FactionService.Load prefix):
                              new game → lobby or capture; save → BeaverBuddies.ColonyFactions
                                   │ IsOn, BaseFaction, slot table, per-start factions
                                   ▼
   OtherFactionCollections (providers) + TemplateCollectionService de-dup
                                   ▼
   FactionCatalog (game adapter over pure FactionSets) · ColonyFactionService (slot→faction, saved)
   CharacterFaction (per beaver) · FactionCreationContext · FactionOf(entity)
                                   ▼
   simulation rules · character patches · model patches · toolbar/display · trading · founding/switch UI
```

### 5.1 New files

| File | Kind | Purpose |
|---|---|---|
| `Factions/MixedFactions.cs` | static + patches | `IsOn`, `BaseFaction`, `Decide` (a `FactionService.Load` prefix), blueprint-modifier union, `OtherFactionCollections`, template de-dup, `[Factions]` logging |
| `Factions/NewGameFactionCapture.cs` | MainMenu service + patch | The solo New Game's Start (a `GameSceneLoader.StartNewGame` prefix), and the mixed check shared with the waiting room |
| `Factions/FactionSets.cs` | **pure** | Faction → collections → items: `ItemsOf(f)` (common ∪ own), `SoleFaction(item)`, `IsCommon`. For goods, needs, template paths and plantables |
| `Factions/FactionRules.cs` | **pure** | Founding faction, switch, placement, `IsTradeableTo`, beaver crossing, `PreferSameFaction`, the room's picks (`LobbyFactionRules` section) |
| `Factions/FactionTable.cs` | **pure** | slot → faction rows `"slot\|faction"` |
| `Factions/SaveColonyReader.cs` | **pure** (Newtonsoft only) | Streams a save's `world.json` singletons: base faction, separate, slot table, mixed table. Stops at `Entities` |
| `Factions/FactionCatalog.cs` | game | Built from `ISpecService` and `TemplateCollectionService.AllTemplates`. Lookups: spec by id, faction of template, goods, needs, plantables, district center, Trading Post template, availability (D1) |
| `Factions/ColonyFactionService.cs` | game | Singleton `BeaverBuddies.ColonyFactions {Mixed, Base, Colonies}`. `FactionOfSlot`, `LocalFaction`, `Set` (replay only, digest), `FactionOf(entity)`, `Fingerprint`, untouched facts |
| `Factions/CharacterFaction.cs` | component + patches | Saved faction on beavers; `FactionCreationContext` (a `[ThreadStatic]` stack); the loader peek; creation-site scopes |
| `Factions/FactionCharacterPatches.cs` | patches | Needs, wellbeing max, textures, bot factory, outfits, avatars |
| `Factions/FactionModelPatches.cs` | patches | Paths, gates, driveways, decals, power shafts |
| `Factions/FactionBuildingRules.cs` | patches | D18 |
| `Factions/FactionToolbar.cs` | disabler + patches | `FactionToolDisabler`, refresh, the planting-UI crash fix |
| `Factions/FactionDisplayPatches.cs` | patches | Population box, faction icon, goods UIs, game over, Wonder, tutorials off |
| `Factions/FactionChoice.cs` | service + events + UI | Founding faction (local pick or chooser), `ColonyFactionSwitchEvent`, the switch offer |
| `Factions/FactionIcons.cs` | UI helper | The in-game diamond-framed logo (`bg-diamond-1`) and the menu ring (`faction-item__logo-background`), one call each |
| `Lobby/LobbyFactionPicker.cs` | menu UI | The faction page's switcher (arrows + name plate + ring) for the waiting room |

Changed: `TimberNet/LobbyFrames.cs`, `LobbyRoom.cs`, `LobbyInbox.cs`, `TimberServer.cs`, `TimberClient.cs`;
`Lobby/LobbySession.cs`, `LobbyHostPanel.cs`, `LobbyGuestPanel.cs`, `LobbyPage.cs`, `LobbyWorldMaker.cs`,
`LobbyRules.cs`; `MultiStart/MultiStartPatches.cs`; `Colonies/ColonyFoundingService.cs`, `ColonyRules.cs`,
`ColonyRulesService.cs`, `ColonyGameWorld.cs`, `ColonySession.cs`, `ColonyDiagnostics.cs`, `ColonyHandover.cs`,
`ColonyScienceService.cs`, `ColonyStamps.cs`, `TradeItems.cs`, `TradeOfferForm.cs`, `TradingPostExchange.cs`,
`TradingPostFragment.cs`, `TradingPostGoodPicker.cs`, `TradeOverviewPanel.cs`, `ColonyWishlist.cs`,
`ColonyConfigurator.cs`; `Events/ConnectionEvents.cs`; `Settings.cs`; `Plugin.cs`; enUS CSV; the tests; the docs.

### 5.2 The mode decision

A `FactionService.Load` prefix calls `MixedFactions.Decide(__instance)`. It runs once per game scene, before any
collection loads.

1. **New game from a waiting room:** `LobbySession.Current` is in `CreatingWorld` and not `IsSave`.
   - `IsOn = session.Setup.Mixed`, `BaseFaction = Setup.FactionId`.
   - The start factions come from the seating plan. It is the **same** order `LobbyWorldMaker.SeatInRoomOrder` uses:
     host → slot 0; each `StartedWith` guest with a `StableId` → the next slot. Each slot takes that member's pick,
     else the base faction.
   - Put the plan in one function, `LobbyWorldMaker.SeatingPlan(session)`, used by both.
2. **New game otherwise** (solo Start): `IsOn = NewGameFactionCapture.Take()`, and `BaseFaction` = the configuration's
   faction.
3. **Loaded save:** read `__instance._singletonLoader.TryGetSingleton(new SingletonKey("BeaverBuddies.ColonyFactions"),
   …)`. `IsOn = Mixed`, and the slot table comes with it.

Always clear the capture. Log once: `[Factions] Mixed factions: on (base Folktails; slots 0:Folktails 1:IronTeeth)`.

The statics are plain (not `IResettableSingleton`); `ColonyFactionService.Load` copies them.

Also:

- **Blueprint modifiers.** A prefix on `FactionBlueprintModifierProvider.Initialize` gives, when on, every faction's
  modifiers, distinct, in `Order`.
- **Collections.** One class implements the four provider interfaces and returns, when on, the other factions'
  collection ids in `Order`. It takes `FactionService` + `FactionSpecService` in its constructor, which also fixes load
  order. It is bound with `MultiBind…().ToExisting<>()`.
- **De-dup.** A postfix on `TemplateCollectionService.Load` sets `AllTemplates` to its distinct blueprints (first
  occurrence, order kept). **(verify)** `GetBlueprint(path)` returns one instance per path, else de-dup by name.

### 5.3 The faction of anything (`ColonyFactionService.FactionOf(BaseComponent)`)

1. A `CharacterFaction`: its id.
2. A template with a sole faction (catalog): that faction.
3. Display only: the owner colony's faction (`ColonyStamp`/`DistrictOwner` → `FactionOfSlot`), else base.

Simulation rules use 1–2 only.

### 5.4 `CharacterFaction` and the creation context

```csharp
public class CharacterFaction : BaseComponent, IPersistentEntity           // decorator on BeaverSpec
{
    static readonly ComponentKey Key = new("BeaverBuddies.CharacterFaction");
    static readonly PropertyKey<string> FactionKey = new("Faction");
    string factionId;
    // Needs are built in Awake, before Load, inside Instantiate, where the context is set: resolve on first read.
    public string FactionId => factionId ??= FactionCreationContext.Current ?? MixedFactions.BaseFaction;
    public void Save(IEntitySaver s) { if (MixedFactions.IsOn && factionId != null) s.GetComponent(Key).Set(FactionKey, factionId); }
    public void Load(IEntityLoader l) { if (l.TryGetComponent(Key, out var o)) factionId = o.Get(FactionKey); }
}
```

- **Loaded beavers.** A prefix and finalizer on `WorldEntitiesLoader.InstantiateEntity(SerializedEntity, …)` push the
  serialized faction (else base) and pop it.
- **New characters.** A scope at each creation site (D13):
  - `NewbornSpawner.SpawnAdult/SpawnChild`: the spawner's faction.
  - `BeaverFactory.CreateAdultFromChild`: the child's.
  - `BotManufactory.OnProductionFinished`: the building's.
  - Founding and switch: the chosen faction.
  - The multi-start start loop: the slot's faction.
  - The dev tools: the local faction.

The context is a stack, popped in `finally` or a finalizer.

---

## 6. The waiting rooms (D7): design

### 6.1 New Game waiting room, mixed

- **When it is mixed.** When the room opens (`LobbyHostPanel.OpenFrom`),
  `Setup.Mixed = Settings.MixedFactionsForNewGames && Settings.SeparateColoniesForNewGames && every faction unlocked
  on the host's profile` (`NewGameFactionCapture.MixedAvailable()`). If the setting is on but a faction is locked,
  the page shows the reason as its status line once: `BeaverBuddies.Lobby.Faction.NotUnlocked`.
- **The summary plate** drops the faction: "Diorama - Normal" instead of "Folktails - Diorama - Normal". Build it from
  the map and mode as the guest's `Summary()` does. A second gold line says "Each player picks a faction"
  (`BeaverBuddies.Lobby.Faction.Mixed`).
- **Every player's page, host included, gets the faction page's switcher** under the plate, as `LobbyFactionPicker`,
  centred: the ring with the chosen faction's logo, then `arrow--left`, the name plate (`faction-item-selected__name`)
  with a `name__text` label of the faction's `DisplayName`, then `arrow--right`. Above it, a small `text--yellow`
  caption "Your faction" (`BeaverBuddies.Lobby.Faction.Yours`).
  - The arrows cycle the factions in `FactionSpec.Order`. The first is chosen by default: the host's faction-page
    pick, or for a guest the room's base faction.
  - The arrows are disabled once the room is not `Open`. A change is sent at once.
  - The switcher replaces the summary row's single ring in a mixed room.
- **Rows.** Each row's `ModIcon` is that player's faction logo (currently the room's). A row's tooltip names the
  faction.
- **The host** picks with the same switcher. That changes `Setup.FactionId` and the room summary's `faction` (the base
  faction and the host colony's). A pick is not a reason to un-ready anyone.
- **Protocol** (TimberNet, display only; a non-mixed room sends none of it):
  - `LobbySummary`: new optional `mixed` (bool) and `factions` (the ids offered, host-available). They are omitted
    when not mixed, so a non-mixed summary's JSON is unchanged.
  - `LobbyPlayer`: new optional `faction` (string), omitted when null.
  - Guest → host `LobbyFaction {faction}` (new `FactionType`; `IsGuestType` includes it).
    - The host accepts it only while `Open`, only if the room is mixed, and only if the id is in the summary's
      `factions`; otherwise it is dropped.
    - It is stored on `LobbyMember.Faction` (null = base), then `version++` and a pump.
    - `LobbySnapshot` / `LobbyMemberInfo` carry it.
  - `LobbyRoom.SetHostFaction(id)` changes the host row and the summary. The summary is immutable, so replace it; the
    welcome is sent only once, but guests read the host row's faction from the roster.
  - `LobbyRoom.Summary` is a property: keep the host's pick in a field, and put it in the host row.
- **Guest side.** `TimberClient.SendLobbyFaction(id)`. The guest keeps its pick locally in `LobbyFactionChoice.Mine`
  (static, the id), cleared when a room opens and when the main menu loads. The in-game founding uses it (D15).
- **At Start.**
  - `StartedWith` carries each guest's faction.
  - `MixedFactions.Decide` computes the seating plan (§5.2). On a multi-start map each filled start is placed in its
    slot's faction (§7.3 step 3).
  - On a standard map the guests found later, each with the pick their own computer kept.

### 6.2 Load Game waiting room (saves hosted from the main menu)

When a save's room opens (`LobbyHostPanel.OpenForSave`), read the save's singletons with `SaveColonyReader` on
`SaveBytes`. That gives the base faction, separate colonies, the slot table, and the mixed table.

- **The ring comes back for saves:** the base faction's logo, as a new game shows it.
- **Rows show colonies.** For a separate-colonies save the room predicts each row's colony, replacing beta19's "no
  colony".
  - Copy the save's `ColonySlotTable` and `Resolve` the host (`LocalPlayerIdentity.Id`), then each guest's
    `StableId` in room order. That is what the game does at hello.
  - The mod gives `LobbyRoom` a seating callback: `Func<IReadOnlyList<string?>, IReadOnlyList<int?>>`, stable ids in
    room order → colony numbers (1–4, 0 helper, null unknown). TimberNet can't reference mod types.
- **Row icons** are that colony's faction logo: the table's faction when mixed, else the base faction. A colony not
  founded yet shows the base faction's.
- **Mixed save, player with no colony yet** (their predicted slot has no faction entry): they get the switcher, and
  their pick is kept locally for their founding (D15). Everyone else's switcher is hidden.
- **Summary JSON** gains `mixed`, `factions` and a per-row `faction` (as in §6.1).
- **Non-mixed saves** also gain the ring and the row colonies. That is a small display improvement for every save
  room, allowed by D8. It changes no simulation.

### 6.3 In the game (menus of the Game scene)

- **The founding dialog (D15).** Build it from in-game classes.
  - The message becomes "Place your {0} colony's district center." when the faction is known (the local pick, or the
    only one).
  - When the game is mixed and no pick is kept, a row of faction buttons: each is `FactionIcons.Diamond(logo)` plus
    the faction's name, and a selected one gets the game's `selected-item` highlight **(verify)** that it draws
    in-game. Clicking selects it. Place district center uses the selection.
  - Ctrl+K opens the same dialog.
- **The Ctrl+T window.**
  - Each colony card shows its faction's diamond icon before the name.
  - The local colony's card has "Play {0} instead" wooden buttons while the untouched switch is allowed (D14).
- **The Trading Post header** shows the partner's diamond icon. Between factions, a muted line lists what can cross.
- **The top-left faction icon** is the local colony's.

---

## 7. Phases

Work on a branch from `origin/main` (`61c8451`). Commit at the end of each phase with both builds at 0 warnings, and
StabilityTests and RuntimeChecks green. `git fetch` before each phase; other sessions may push.

### Phase 0: Verify

Record the answers in §14:

1. De-dup identity (§5.2).
2. How a new entity's `Guid` is made in a replay (`Found()` already does it; the switch must match).
3. The dev-mode beaver spawn method name.
4. That `InitializeNeeds` runs only in `Awake`.
5. The `ModularShaftModelService` / `ShaftModelFactory` / `ShaftFrameFactory` constructors and fields.
6. That the solo New Game path is `NewGameModePanel` → `GameSceneLoader.StartNewGame`.
7. That the in-game `selected-item` and `bg-diamond-1` classes draw in a dialog. The dialog sits in the game's UI
   document, so they should.
8. Whether the top bar is per colony.

### Phase 1: Load both factions, and the crash fixes

1. **The setting** (`Settings.cs`, under `// ---- Separate colonies ----`): `MixedFactions` (bool, default false).
   - Label: "Mixed factions for new games (beta)".
   - Tooltip, two lines of at most 112 characters:
     - "Host only, new games with separate colonies: each player picks Folktails or Iron Teeth for their colony." (104)
     - "Needs every faction unlocked on the host's computer. A save keeps the mode it started with." (91)
   - Static getter `MixedFactionsForNewGames`.
2. **`NewGameFactionCapture`**, bound in the MainMenu configurator; `FactionSpecService` and `FactionUnlockingService`
   injected.
   - `MixedAvailable(out lockedFaction)`: the setting and separate colonies are on, and every faction is unlocked.
   - A `GameSceneLoader.StartNewGame` prefix captures it for a solo start.
   - If the setting is on but a faction is locked: remember a notice for the Game scene
     (`BeaverBuddies.Colony.Faction.NotUnlocked`: "Mixed factions is on, but {0} is not unlocked on this computer, so
     this game has one faction."), shown once after load.
3. **The decision, providers, modifiers and de-dup** (§5.2).
4. **The catalog** (§5.1). Log its counts.
5. **Crash fixes** (all gated):
   - **Bots:** replace `BotFactory.Load` and `Create(Vector3, Quaternion, object)` when on: one blueprint per faction,
     chosen by `FactionCreationContext.Current ?? BaseFaction`, with vanilla's init components in vanilla's order.
   - **Shafts:** the two `Load`s pick the parts spec of `MixedFactions.ShaftBuildFaction ?? BaseFaction`.
   - **PlantingUI:** the planter-name lookup picks the planter of the plantable's faction, else the base faction's,
     else the first.
   - **Worker outfits:** `WorkerOutfitService` keys by `(FactionId, Id, WorkerType)`; `TryGetOutfitSpec` looks up the
     worker's own faction and mirrors the vanilla logic.
6. **Acceptance.**
   - A solo new mixed game (setting on) loads, and the log shows the decision and catalog lines.
   - In dev mode, place every Iron Teeth building next to Folktails ones. Nothing throws.
   - Save and load.
   - A non-mixed game logs "off" and nothing else changes.

### Phase 2: Characters

1. `CharacterFaction`, the context, the loader peek and the creation scopes (§5.4).
2. **Needs:** replace `NeedManager.GetNeeds()` when on with `FactionCatalog.NeedsFor(faction, isBot)`. It filters
   `FactionNeedService.Needs` (already scaled), keeps order, and is cached.
3. **Wellbeing max:** `WellbeingLimitService.GetMaxWellbeing(tracker)` from that character's `NeedSpecs`.
4. **Textures:** replace `BeaverTextureSetter.InitializeEntity` when on. Exactly **one** RNG draw from that faction's
   list.
5. **Avatars:** the two badges use the entity's faction. The empty slots use the selected building's faction
   (dwelling or workplace fragment `ShowFragment` sets `FactionDisplay.PanelFaction`), else the local faction.
6. **Acceptance.**
   - Seat flip: dev-spawned beavers of each colony show their own needs and fur.
   - Each faction's bot assembler makes bots with only their own needs (Biofuel or Energy).
   - Births and growing up keep the faction. Save and load keep it too.

### Phase 3: Colony factions, founding, switching

1. **`ColonyFactionService`.**
   - Load, and Save when on.
   - `Set` only in a replay: `ColonyDigest.Note("faction", slot, ColonyDigest.Of(id))`, then refresh the toolbar and
     the overview.
   - `ColonyDiagnostics.Fingerprint()` gets the table, and `flags` get `mixed`.
2. **New game starts.**
   - `MixedFactions.Decide` fills the table: slot 0 = base, plus each filled multi-start slot's faction.
   - In the multi-start loop (`StartingBuildingInitializerInitializePatcher`), when on: for each start of slot `s`
     with a faction different from base, place a district center of that faction. `StartingBuildingSpawner.Place`
     uses `StartingBuildingTemplateSpec`, so swap that property to `StartingBuildingOf(f)` for the call and restore it
     after (it has a private setter; the publicizer makes it writable).
   - Then, in `GameInitializerSpawnBeaversPatcher`, spawn each start's beavers inside that slot's faction scope. Both
     loops know the slot of each start: keep a list of (building, slot) in `StartBuildingsService`, or read the
     building's `DistrictOwner`.
   - A standard map's single start is the host's (base).
   - The table is written from these starts. Everything happens during the new-game scene, before the tick-0 save.
     `Set` does not note the digest here; the gate is closed outside a tick or replay.
3. **Available factions (D1).**
   - The host latches `HostFactions` in `ColonySession.BeginHostSession` from its profile, when on.
   - `InitializeClientEvent` gains `hostFactions`; `AdoptHostChoice` takes it.
   - Solo: the local profile.
4. **Founding (D15).**
   - `FoundColonyEvent` gains `public string faction;`.
   - The dialog and Ctrl+K follow §6.3. The tool is built per faction from `StartingBuildingOf(f)`.
   - The host judge (`FactionRules.JudgeFoundingFaction(isOn, faction, known, available)`) refuses an unknown or
     unavailable faction with `ColonyRefusal.FactionUnavailable` ("The host has not unlocked that faction.").
     Rewrite: null when off, base when on and empty.
   - The founding placement's `TemplateName` is the chosen faction's district center.
   - `Found()`: `Set(slot, faction)` first, then the district center from `StartingBuildingOf(faction)`, then the
     beavers in that faction's scope.
   - Clear `LobbyFactionChoice.Mine` once the local player's founding has played.
5. **Switch (D14).**
   - `ColonyFactionSwitchEvent : ReplayEvent { public string faction; }`, Global scope, changes the game. Add it to
     the RuntimeChecks Global list, to `WaitsForStart`'s set (with `JoiningClosedAtStart`), and to the "unseated
     actors refused" list.
   - **Host:** `FactionRules.JudgeSwitch(isOn, isSeatOwner, current, wanted, available, facts)` →
     `FactionSwitchVerdict`. Refusal: `ColonyRefusal.FactionSwitchNotAllowed` ("A colony can change faction only
     before it builds, marks, unlocks or trades anything.").
   - **Replay:** judge again, then for each of the slot's district centers:
     1. Record the placement and the `SimpleOutputInventory` stock.
     2. Count the slot's adults and children.
     3. Delete the beavers, then the district center.
     4. `CreateAsFinished` the new one under `PendingSlot`, then `SetSlot`.
     5. Give back the goods the new faction stores.
     6. Spawn up to the starting numbers inside the new scope.
     7. `Set(slot, f)`.
     8. Journal and notice ("{0} now plays {1}.").
     9. Refresh.

     Follow `Found()` closely.
   - **UI:** the Ctrl+T card buttons (§6.3), plus a one-time dialog for a seated player whose colony exists when they
     join, while untouched, in a mixed game, **only if** their kept lobby pick differs from their colony's faction or
     they had none. "Your colony starts as {0}. You can switch to {1} until it builds, marks or trades anything."
     Buttons: Keep, Switch.
6. **Placement (D17).**
   - `IColonyWorld` gains `FactionOfTemplate` / `FactionOfSlot`.
   - In the Placement scope, when on: refuse a sole-faction template that isn't the actor's faction, except Trading
     Posts.
   - `ColonyRefusal.OtherFactionBuilding`: "That building belongs to another faction."
7. **Acceptance.**
   - Seat flip: found colony 2 as Iron Teeth (chooser).
   - Colony 3 founded Folktails, then switched while untouched; after a path, the switch is refused.
   - A 3-start map made from a mixed waiting room (or solo with a scripted table, §10.3) places each start in its
     faction.
   - Save and load keep everything. Heartbeat and daily check are green.

### Phase 4: Models

1. **Driveways:** a postfix on `DrivewayModelInstantiator.InstantiateModel` sets the material from `FactionOf(block
   object)`.
2. **Paths and gates:** `PathLook.Apply(entity)` re-sets the six ground (`PathMaterial`) and six roof
   (`BaseWoodMaterial`) children.
   - Call it from a `DynamicPathModel.Awake` postfix; previews use the local faction.
   - Call it again when `ColonyStamp`'s slot is set or re-stamped (`Colonies/ColonyStamps.cs`).
3. **Decals.**
   - Load every `DecalSpec`.
   - The picker (`DecalButtonContainer.Show`) is filtered by the building's faction.
   - An empty decal takes the building faction's first.
4. **Power shafts:** a second `ModularShaftModelService` set per other faction, built by reflection under
   `ShaftBuildFaction`. `ModularShaftModelUpdater.Awake` points at the building faction's set.
   - **Fallback:** base models for all shafts (Known limits).
5. **Acceptance:** each colony's paths, driveways, gates, banners and shafts look like its own faction.

### Phase 5: Buildings keep to their faction (D18)

1. **Stockpiles:** replace `StockpileInventoryInitializer.Initialize` when the building has a sole faction. Add only
   goods of its `WhitelistedGoodType` in `GoodsOf(faction)`, mirroring the rest of vanilla exactly **(verify the
   body)**.
2. **Planters:** a postfix on `PlanterBuilding.GetAllowedPlantables` keeps common and own-faction plantables.
3. **Yield removers:** a postfix on `YieldRemovingBuilding.IsAllowed` also requires the yield good in
   `GoodsOf(faction)`.
4. **Acceptance:** a Folktails warehouse, farmhouse and gatherer ignore Iron Teeth goods and crops.

### Phase 6: Toolbar and display

1. **`FactionToolDisabler`**, bound next to `TradingPostToolDisabler`. A block-object or planting tool is enabled if
   its template's or plantable's faction is null or the local faction. It is O(1).
2. **Refresh** at the end of `RefreshToolLocks()` and from `ColonyFactionService.Set`:
   1. `OnDevModeToggledEvent(null)` on every tool button and on every entry of the private
      `ToolButtonService._toolGroupButtons`. **Never** post a real `DevModeToggledEvent`.
   2. Switch to the default tool if the active tool is now disabled.
   3. Exit the group if it is now hidden.
   4. Refresh the faction icon.
3. **Population box:** a postfix on `PopulationWellbeingBox.GetPanel` hides needs outside the local faction.
4. **Faction icon:** the top-left icon is the local colony's logo.
5. **Goods lists**, display only; **never** filter `GoodService.Goods` or patch `GetGoodsForGroup`:
   - the distribution tab (re-implement the factory's items, filtered by the district's faction);
   - the crossing's import icons;
   - good statistics (the local faction);
   - the stockpile tooltip's storage list;
   - the resource-counter dropdown.
6. **Game over, Wonder, tutorials** follow D24. Tutorials are off when mixed.
7. **Top bar:** only if Phase 0.8 finds it global, hide foreign rows the local colony has none of.
8. **Acceptance:** a seat flip switches the toolbar, crops, wellbeing box and icon; a steward sees the colony's
   faction; dev mode shows all.

### Phase 7: Trading (D2, D3, D20)

1. **Pure rules:** `FactionRules.IsTradeableTo(item, receiverStores, separateScience, giverFaction, receiverFaction)`
   and `MayBeaverCross(beaverFaction, targetFaction)`.
2. **Form:** `TradeOfferForm.Judge` gains optional `giveAllowed`/`getAllowed` (default allow) and the verdicts
   `ErrorNotStorable` ("{0} can't store {1}. Between factions, only goods both factions use can be traded.") and
   `ErrorBeaversFaction` ("Beavers can only move between colonies of the same faction."). Update the fuzz test.
3. **Picker:** `TradingPostGoodPicker.Open(..., allowed)`. Items failing it are not listed.
   - Give side: the partner receives; get side: the local colony receives.
   - Filter per call; `TradeItems.Groups()` stays cached.
   - `MostStocked` and `ApplyPrefill` respect it.
4. **Binding check** (replay, saved state only): `WhyNotPropose` and `Accept` check both sides with
   `FactionCatalog.GoodsOf(FactionOfSlot(receiver))`. Notice `…Trade.Notice.OfferFailedFaction`. `Accept` also checks
   the partner half's owner is still `theirs.Colony`.
5. **Running exchanges:** `CheckTradingPosts` ends an exchange whose terms now fail (`…Trade.Notice.VoidFaction`).
6. **Beavers (D20):** `MoveBeavers`, `BeaversToSpare`, `IsIn` and the form's status count only beavers whose
   `CharacterFaction` matches the target colony's faction.
7. **Wishes:** normalize and pick with the wishing colony as the receiver. Partners' wish marks only on allowed items.
8. **The post between factions:** a muted line, "Between factions: goods both factions use, and science. No beavers."
   (`…Trade.BetweenFactions`).
9. **Acceptance.**
   - Folktails ↔ Iron Teeth: exactly the 17 goods (+ Science), no Beavers.
   - Prefill of Carrot clears.
   - A forged Corn offer is refused at replay.
   - Same-faction beaver trades still work. Foreign beavers from a handover don't cross.

### Phase 8: The waiting rooms (§6)

1. **TimberNet.** The optional fields and the `LobbyFaction` frame (§6.1): parse and build, strict validation, the
   host's handling, the snapshot, the inbox, `SendLobbyFaction`.
   - Non-mixed JSON is byte-identical to beta19's. Test it.
2. **Session and setup:** `LobbySetup.Mixed`, `LobbySetup.Factions` and `LobbySetup.HostFaction`. The seating plan
   (§5.2) and `StartedWith` factions.
3. **`LobbyFactionPicker`** (menu classes only; add them to `LobbyPage.ClassesUsed`, which RuntimeChecks checks
   against `UI.zip`):
   - the `faction-item__logo-background` ring + `faction-item__logo`;
   - `arrow--left` / `arrow--right` buttons;
   - the `faction-item-selected__name` plate with a `name__text` label;
   - a `text--yellow` caption.

   Built with `VisualElementInitializer` once (click sounds). Events: `Changed(string id)`.
4. **`LobbyPage`:** a mixed layout (the plate without faction, the gold "each player picks" line, the picker), per-row
   faction icons and faction tooltips. The non-mixed layout is unchanged.
5. **Host panel:** the room is mixed from `MixedAvailable`. The host's picks change the room. For saves:
   `SaveColonyReader`, the seating callback, row factions, and the ring back.
6. **Guest panel:** the picker when the room is mixed and this guest may pick (new game: always; save: only without a
   colony). It sends picks and keeps `LobbyFactionChoice.Mine`.
7. **World maker:** `SeatingPlan` is shared with `Decide`.
8. **Acceptance** (as far as can be checked without two computers): StabilityTests for the frames, the rules and the
   seating plan. RuntimeChecks for the classes and for `SaveColonyReader` on a synthetic save (and on the newest real
   save if present, read-only).

### Phase 9: Handover, Trading Post halves, the rest

1. **Handover:** `FactionRules.PreferSameFaction` inside `NearestLiving`.
2. **Trading Post halves (D23, optional):** in `JudgePairs`, after both are allowed, a half whose entrance cell (the
   `FarEntrance`-style computation beta15 used; see `ColonyRoadRule`) has a road of a colony with another faction
   takes that faction's post template. **Fallback:** skip it.
3. **The mixed notice** from Phase 1.
4. **Acceptance:** four colonies, where a dead Folktails colony goes to a Folktails one over a nearer Iron Teeth one.

### Phase 10: Checks (§10) — Phase 11: Docs (§11) — Phase 12: Release

- Released as **1.4.0-beta20** (the maintainer's title for this work).
- Bump `BeaverBuddies.csproj`, `manifest.json`, `BeaverBuddies/changelog.txt`, the STABILITY-CHANGELOG entry, and
  `docs/index.html`, `install.html` and `troubleshooting.html`.
- `rm -rf BeaverBuddies/bin`, then build **Release** and **Release Steam** into a scratch mods folder
  (`-p:BeaverBuddiesModsPath=<scratch>/`).
- Zip in the previous release's entry order, with new files appended and long paths read through `\\?\`, plus
  `SHA256SUMS`.
- Commit, fetch, and check that `origin/trading-exchange` is an ancestor. Push `HEAD` to `trading-exchange`, `main`
  and the branch, with an annotated tag.
- `gh release create --repo timbermods/BeaverBuddies-MultiColony --prerelease --verify-tag`, with notes from the
  previous release's.
- Verify the asset hash, the CI Tests run, and the Pages build. Say plainly that it is **not seen in a game**.

---

## 8. Determinism rules

1. Simulation reads only the event's host-stamped fields, saved state (the faction table, `CharacterFaction`) and
   static data identical everywhere (the catalog, from the same game and mod files the join handshake checks).
2. Never read `LocalFaction`, `LocalSlot`, `LobbyFactionChoice.Mine` or other display state in a tick, replay, load or
   simulation path. `FactionOf` step 3 is display only.
3. The synced RNG's use stays vanilla's: one texture draw, nothing else.
4. No `Dictionary`/`HashSet` iteration decides simulation order. Catalog lists are ordered by the game's `Order` or
   ordinal id.
5. Founding and the switch create entities only in a replay, through the same calls everywhere.
6. The faction table changes only through `Set`, in a replay or in the new-game scene before its tick-0 save (the save
   carries it). It shows in the daily fingerprint.
7. A load reproduces the save: `CharacterFaction` and the table are saved, and a building's faction is its template.
8. The creation context is a stack popped in `finally`.
9. **The waiting room is display and setup only.** Its picks reach the game solely through the tick-0 save (the table,
   the starts) or through a founding event the host judges.

---

## 9. Save and wire changes, and risks

- **Save (mixed only):** singleton `BeaverBuddies.ColonyFactions {Mixed, Base, Colonies}`, and component
  `BeaverBuddies.CharacterFaction {Faction}`. Non-mixed saves are unchanged.
- **Wire:**
  - `FoundColonyEvent.faction`; `ColonyFactionSwitchEvent`; `InitializeClientEvent.hostFactions`.
  - In the waiting room: the optional summary fields `mixed` and `factions`, the optional row field `faction`, and
    `LobbyFaction`.
  - The handshake already requires the same mod version.
- **A mixed save outside MultiColony** loses the other faction's buildings and characters (the game's "loading
  issues"). Document it; it can't be prevented.

| # | Risk | Mitigation |
|---|---|---|
| R1 | Another crash site | Phase 1 acceptance: every building of both factions, then save and load. §3.2's lists are exhaustive at 1.1.2.4 |
| R2 | `CharacterFaction` read outside the creation window | Lazy base default, a one-time warning, and the loader peek |
| R3 | The switch deletes live beavers | Untouched only; beavers first; test day and night |
| R4 | Shaft reflection | RuntimeChecks table; fallback |
| R5 | Other mods reading `Current` | Known limit |
| R6 | Memory and load time | Known limit; measure once |
| R7 | Picks differ between host and guest views | The host's roster is the truth; the guest's own pick is sent and echoed |
| R8 | The seating plan differs from `SeatInRoomOrder` | One shared function; a StabilityTests check |
| R9 | `SaveColonyReader` on a huge save | Stream, and stop at `Entities`; on any error fall back to beta19's display |
| R10 | Menu classes in the Game scene, or the reverse | `LobbyPage` and the picker use menu classes; in-game UI uses in-game ones; both lists are checked against `UI.zip` |

---

## 10. Checks

### 10.1 StabilityTests (`StabilityTests/FactionChecks.cs`)

Link `FactionSets.cs`, `FactionRules.cs`, `FactionTable.cs` and `SaveColonyReader.cs`.

- **Table:** round trip; garbage ignored; a missing slot is base.
- **Sets:** common, shared, sole, `ItemsOf`.
- **Founding faction:** off, unknown, unavailable, available, and the base fallback.
- **Switch:** every verdict.
- **Placement:** own, common, Trading Post of any faction, other faction refused, off.
- **Trade items:** science, beavers, goods, asymmetry.
- **Beaver crossing.**
- **Handover preference.**
- **Trade form:** the two verdicts, and the fuzz invariant.
- **Wishlist:** forbidden items dropped.
- **Waiting room:** summary and row JSON without faction fields are identical to beta19's; with them they round-trip;
  a `LobbyFaction` with an unknown id, or in a non-mixed room, is dropped; the host's pick sets the host row.
- **Seating plan:** it matches `SeatInRoomOrder`'s order, including guests without ids.
- **`SaveColonyReader`:** a synthetic zip with the singletons; a zip where `Entities` comes first; a corrupt zip.
- **The gate:** a source scan that every `[HarmonyPatch]` body under `Factions/` mentions `MixedFactions.IsOn`, bar
  the whitelist.
- The existing tooltip and loc-key checks cover the new strings.

### 10.2 RuntimeChecks (`RuntimeChecks/FactionRuntimeChecks.cs`)

- **Data:**
  - exactly two factions;
  - each starting building exists, with equal district-center footprints;
  - the 4 shared template paths, and unique names after de-dup;
  - one shaft parts spec and one bot template per faction, and the critical bot needs;
  - unique material names and decal ids; outfits unique by `(faction, id, type)`;
  - box, pile and liquid per faction; the 17 tradeable goods; the plantable split;
  - a Trading Post per faction.
- **Game surface:** every patched or reflected member, in a table.
- **UI:** each class in `LobbyPage.ClassesUsed`, `LobbyFactionPicker.ClassesUsed` and `FactionIcons.ClassesUsed` exists
  in `UI.zip`'s style sheets.
- **Events:** the switch event is on the Global list; JSON round trips of the new fields.
- **No key collisions.**

### 10.3 Solo script (`ALPHA-TEST-SCRIPTS.md`, Script F)

1. Mixed setting on. Solo New Game as Folktails. Log "on".
   - The toolbar is Folktails and common.
2. Unpause. Ctrl+Shift+K to colony 2. Ctrl+K opens the chooser; found Iron Teeth.
   - Iron Teeth center, fur, toolbar and card icon.
3. Iron Teeth houses, farms, a bot assembler and a charging station.
   - The bot's needs show Energy only.
   - The Iron Teeth farmhouse and warehouse lists hold Iron Teeth and common only.
4. In colony 1, try an Iron Teeth building in dev mode: refused.
5. Colony 3: found Folktails, then "Play Iron Teeth instead" before building. After a path, the button is gone.
6. A Trading Post between colonies 1 and 2 offers the 17 goods, Science, and no Beavers. 50 Logs for 10 Gears runs.
7. Save, quit, load: everything as before. Checks are green for 3 days.
8. Mixed on with separate colonies off: a shared game, log "off".

### 10.4 Two-player script (Script F, continued)

1. **The host opens a mixed New Game waiting room** as Folktails on a 3-start map.
   - The guest joins and sees the switcher and each row's faction.
   - The guest picks Iron Teeth; the host's row for them shows Iron Teeth.
   - Start: start 2 is an Iron Teeth district center with Iron Teeth beavers.
2. **The same on a standard map:** the guest's founding dialog says Iron Teeth without asking.
3. **The guest has Iron Teeth locked** on their profile: still allowed (D1).
4. **Load Game → Host co-op game** on that save: rows show "Colony 1 · Folktails" and "Colony 2 · Iron Teeth" icons,
   and the ring shows Folktails.
5. **A week of play:** desync checks are green.

---

## 11. Documentation

- **`TWO-COLONIES.md`:** a new section, *Mixed factions (beta)*, covering:
  - the setting;
  - picking in the waiting room;
  - how each colony gets its faction;
  - the switch;
  - what a colony builds, plants and stores;
  - beavers and bots;
  - trading between factions (the 17 goods, Science, no beavers);
  - handovers;
  - what you see.

  Also: rows in *Keeping colonies apart*, *Known limits*, and *How it works*.
- **`README.md`:** a *Mixed factions* subsection, and the setting.
- **`STABILITY-CHANGELOG.md`:** a feature entry (beta7 style: bold summary, bold-led bullets naming code, **Save
  change.**, **Wire change.**, Docs, Checks with counts, "Not seen in a game. Script F.").
- **`BeaverBuddies/changelog.txt`:** plain lines, then "Full details…".
- **Site:**
  - `docs/index.html`: `#new`, a `#features` card "Folktails and Iron Teeth together", `#limits`, and the check
    counts.
  - `install.html#settings`.
  - `faq.html#compat`.
- **`ALPHA-TEST-SCRIPTS.md`:** Script F.
- **`design/PRE-GAME-LOBBY-PLAN.md`** D5 ("Folktails only"): add a pointer to this plan.

---

## 12. Known limits

- A mixed save needs MultiColony (§9).
- Only new games can be mixed.
- After a handover between factions, a colony runs what it received but builds only its own faction's buildings.
- Achievements, the unlock goal and the load menu's metadata use the base faction.
- Tutorials are off in mixed games.
- Mods reading `FactionService.Current` see the base faction.
- Memory and load time rise with both factions loaded.
- Iron Teeth availability follows whoever hosts.
- A game made solo and hosted later (or a save hosted from inside a game) has no waiting-room picks: guests use the
  founding chooser or the switch.
- If D23 or the per-faction shafts fell back: both halves show the placer's model, and shafts show the base faction's.

---

## 13. Handoff prompt (if another session builds this)

```text
Implement design/MIXED-FACTIONS-PLAN.md (revision 2) from start to finish on a branch from main.
Read the whole plan first. Do Phase 0 and commit its findings in §14 before any product code.
D1–D8 are the maintainer's; D9 onward are the plan's — record any change and why in §14, and take the stated
fallbacks rather than ship something fragile. Every phase ends with both builds (0 warnings), StabilityTests and
RuntimeChecks green, and a commit. A game without Mixed factions must behave exactly as main does (D22). Menu UI
uses menu classes only, in-game UI in-game classes only; make it look like the game's own faction page. Nobody can
play the game during this work: every doc and note says "not seen in a game". Copy BeaverBuddies/env.props from the
main checkout into the worktree. Fetch before each phase and before any push. Finish with the Phase 12 release and a
report: what was built per phase, every deviation and fallback, the check counts, and Script F.
```

## 14. Phase 0 findings and amendments

*(Filled in while building.)*
