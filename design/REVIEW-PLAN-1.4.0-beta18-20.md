# Review plan: the waiting room and mixed factions, MultiColony 1.4.0-beta18 to beta20

**Status:** approved 2026-09-22 (three reviewers plus the main one; fixes, then released as 1.4.0-beta21). Findings:
[REVIEW-FINDINGS-1.4.0-beta18-20.md](REVIEW-FINDINGS-1.4.0-beta18-20.md).

## 0. Base, scope, and what is already known

**Base.** Worktree HEAD `79d4757` is tag `v1.4.0-beta20`. `origin/main` and `origin/trading-exchange` are the same
commit. Game: Timberborn 1.1.2.4. The decompiled game is regenerated into the scratchpad per session (`ilspycmd` is
installed).

**Scope.** Three releases that change how a co-op game starts, then what it can contain. From `v1.4.0-beta17` to
`v1.4.0-beta20`, leaving out `docs/` and `design/`: 82 files, +8,919 / −145.

| Release | Commit | What | Code, tests, repo docs |
|---|---|---|---|
| beta18 | `cc14682` | The waiting room for new games | beta17 → beta19: 50 files, +4,034 / −92 |
| beta19 | `61c8451` | The waiting room for saves hosted from the main menu | (in the row above) |
| beta20 | `79d4757` | Mixed factions (opt-in), and faction picks in the waiting room | 61 files, +4,937 / −105 |

- **None of it has been played.** Script D (the waiting room, `ALPHA-TEST-SCRIPTS.md:361`) and Script F (mixed
  factions) are owed. Checks at beta20: StabilityTests 391 and RuntimeChecks 349; both builds have 0 warnings.
- **Each release was reviewed before release by the session that wrote it.** `795f49c` found 7 things, beta19's
  review found 2, and `866890c` found 11 and "no desync". This review treats those as claims to test, not as a
  baseline.
- **Not re-reviewed:** beta12's and alpha10's "found sound" lists and the lockstep core, except where these releases
  changed them.
- **The premise.** Every computer loads the same save bytes with the same seed.
  - The waiting room changes *how* those bytes are made and delivered: the host makes the world single-player behind
    a held loading screen, saves it at tick 0, and sends it before anyone loads.
  - Mixed factions changes *what* loading them means: both factions' content, and a faction on every beaver.

  Both changes go straight to the premise.

### Found while planning: a waiting-room guest can't get into the game

This is **J1**. I confirmed it by reading the code; it has not been run. Every waiting-room game with a guest hits it,
in beta18, beta19 and beta20, so it comes before everything else.

1. The save arrives. `ClientConnectionService.LoadMap` first calls `SingletonManager.Reset()`
   (`BeaverBuddies/Connect/ClientConnectionService.cs:329`), which empties the registry (`SingletonManager.cs:10-21`).
2. For a waiting-room guest `lobbyHostName` is always set, because `CheckWaitingRoom` writes it every frame once the
   guest is welcomed (:374). So `LoadMap` takes the branch at :345-347 and calls `RegisteredLocalizationService.T(...)`
   for the loading tip.
   - That reads `SingletonManager.GetSingleton<RegisteredLocalizationService>().ILoc`
     (`Util/RegisteredLocalizationService.cs:19`), which is now null. It throws a NullReferenceException, and
     `LoadScene` never runs.
   - `NotifyEach` catches the exception and queues "could not be loaded" (`TimberNet/TimberNetBase.cs:717-727`).
3. In the same `UpdateSingleton` (:360-361), `CheckWaitingRoom` runs next. The registry is still empty, so
   `GetSingleton<LobbyGuestPanel>()` (:375) is null, and it takes the "this player is in a game" branch (D20):
   `client = null` and `EventIO.ResetIf` (:377-380). Nothing updates that client again, so the queued error never
   shows. The guest is left in the menu with no message.
4. Fixing step 2 alone isn't enough. Step 3 still runs in the frame the load starts: the guest drops its connection
   and loads a single-player game.

Classic joins reach neither branch: `lobbyHostName` is null, so they call `StartSaveGame`. Nothing in StabilityTests
or RuntimeChecks runs `ClientConnectionService`. The lines came in with `a798711` (beta18).

---

## 1. Who each change can hurt

| Group | Who | What reaches them | Priority |
|---|---|---|---|
| **Everyone** | Every player on beta18 or later, including those who never open a waiting room or turn on mixed factions | Classic hosting and joining run through rewritten `TimberServer`, `TimberNetBase` and `ClientConnectionService` code. Load Game → Host co-op game in the main menu *always* opens a waiting room now. Seven new services are built in every game. 48 new or changed Harmony patches go through one `PatchAll`. The faction decision runs in every game and in the map editor. Every beaver carries a new component | First |
| **Waiting room** | Anyone who starts co-op from the main menu (New Game or Load Game) | The J leads | Second |
| **Mixed factions** | Hosts who turn the beta setting on and have Iron Teeth unlocked | The M leads | Third, except for desyncs, which rank with the first group |

---

## 2. How it fits together

### 2.1 Every way into a co-op game (after beta19)

| Path | Entry | Changed by beta18/19? |
|---|---|---|
| Waiting room, new game | New Game → Game Mode → **Host co-op game** (`Lobby/LobbyPatches.cs:14-29` → `LobbyHostPanel.OpenFrom`) | New |
| Waiting room, save (main menu only) | Load Game → **Host co-op game** → `ServerHostingUtils.LoadAndHost` (`Connect/ServerHostingUtils.cs:141-158`) → `LobbyHostPanel.OpenForSave` | New; **replaces the classic dialog in the main menu** |
| Classic host, in a game | Options → Load → Host co-op game; the desync dialog's **Save and Rehost** | Same flow. `ServerEventIO.Start` now goes through `StartServer`, and `TimberServer`'s internals changed |
| Direct-IP guest | `ClientConnectionUI` → `TryToConnect` | Connecting box, `CheckWaitingRoom`, the `LoadMap` tip branch |
| Steam invite, overlay join, `+connect_lobby` | `SteamOverlayConnectionService` | The host's name; the Connecting box replaces "Joined!" |
| Reconnect after a desync (beta10) | `DesyncDialogPlan.Reconnect` | Code unchanged; behaviour changed by the save room and D20 (J3) |

### 2.2 The waiting room's hand-off

```
host page (menu)                        TimberServer, lobby mode                          guest page (menu)
 LobbySession.Open ─StartServer─► accept → handshake (MVID) → AdmitToLobby ─SendLane─► [-1][frame] → LobbyInbox
   Open ─Start─► CreatingWorld: new game → a single-player scene, loading screen held,
                  SeatInRoomOrder, save "<ts> Co-op start"   (a save skips this)
        ─► SendingWorld: ReleaseLobby → per guest: StartQueuing → paced save → state → InitializeClientEvent
   Update: all guests queued (or 10 s, stragglers removed) → EventIO.Set, seed, LoadScene(save)   guest: LoadMap
```

### 2.3 Mixed factions

```
Settings + the host's unlocks + waiting-room picks
   ─► MixedFactions.Decide, a FactionService.Load prefix (runs in every game scene)
   ─► the other faction's collections (providers) + template de-dup ─► FactionCatalog
   ─► ColonyFactionService (slot → faction, saved) · CharacterFaction (per beaver, saved) · creation context
   ─► simulation rules: needs, stockpiles, planters, yields, bots, placement, trade, handover, the switch
   ─► display: toolbar, models, icons, goods lists
```

---

## 3. Inventory (done while planning)

- **Patches.** There are 48:
  - 46 new patch classes in `Factions/`, over 51 game methods, plus 2 changed patches in
    `MultiStart/MultiStartPatches.cs`.
  - 18 of the new classes replace the game's own method body (20 methods).
  - 14 targets are named by a string the compiler doesn't check, and 4 are looked up by `AccessTools.Method` with
    no parameter types.
  - No class or method stacks two patch names.
  - Every patch is gated on `MixedFactions.IsOn` except the decision (`MixedFactionsDecidePatcher`) and the solo New
    Game capture, which run on purpose in every game.
  - There are no transpilers and no manual `Patch` calls. Everything goes through the one `harmony.PatchAll()`
    (`Plugin.cs:176`).
- **Saved state, mixed games only:** the singleton `BeaverBuddies.ColonyFactions {Mixed, Base, Colonies}` and the
  component `BeaverBuddies.CharacterFaction {Faction}`. A patch now also writes the game's own `DecalSupplier.ActiveDecal`.
- **Events:**
  - `FoundColonyEvent.faction`;
  - `ColonyFactionSwitchEvent` (new, Global);
  - `InitializeClientEvent.hostFactions` (beta20) and `.joiningClosedAtStart` (beta18).
- **The waiting room's wire:**
  - host → guest, each behind a `-1` length: `LobbyWelcome`, `LobbyRoster`, `LobbyState`, `LobbyEnd`;
  - guest → host: `LobbyHello`, `LobbyReady`, `LobbyFaction`.

  All of them come after the build handshake.
- **Threads.** The waiting room adds:
  - a pump timer;
  - one `SendLane` thread per member, and a receive thread per waiting guest;
  - join tasks, and release and removal tasks.

  Two waits run on the game thread: `CancelLobby` (up to 2 s) and `TimberClient.Start` (up to 3 s).

---

## 4. Leads

Each lead says what to check and what would count as proof. Every finding will be reported as:
- **Confirmed:** traced end to end in the mod and the decompiled game, or reproduced by a check;
- **Plausible**;
- **Refuted**.

Line numbers are beta20's.

### Tier 1a: everyone, including players who never use the new features

**E1. New services in every game.**
- `ColonyConfigurator` binds seven faction services in every Game scene (`Colonies/ColonyConfigurator.cs:95-105`).
- `ColonyFoundingService` now also needs `EntityRegistry` and `EntityService`.
- The main menu builds `NewGameFactionCapture` (`Plugin.cs:110`).

If any of their dependencies isn't bound in that scene, every game fails to load. RuntimeChecks only checks that the
game members exist. **Check:** compare, by script, every new constructor's parameters against the game's own
configurators for Game, MainMenu and MapEditor (decompiled).

**E2. One `PatchAll` for everything.** A game update can rename or overload a target:
- `AccessTools.Method` without parameter types then throws an ambiguous-match error;
- `TargetMethods` returns null;
- either way `PatchAll` throws, and the mod's later patches don't apply.

That breaks every co-op game, not only mixed ones. RuntimeChecks checks that names exist, not signatures, and it skips
`TargetMethods` classes and `MultiStart/`. **Check:** resolve every patch in the mod with Harmony's own resolver
against the game's DLLs. Whether feature patches should be isolated is a finding for the report, not a change in this
pass.

**E3. The faction decision runs in every game and in the map editor** (`Factions/MixedFactions.cs:67-135, 166-173`),
before the game's own collections load. **Check:**
- what runs outside its try/catch;
- that a game without mixed factions loads exactly beta19's templates, goods, needs and materials, in the same order;
- that `FactionCatalog` can't be built before `AllTemplates` is filled and then keep `built = true`. Its constructor
  doesn't take `FactionService`, which is what orders the other three services.

**E4. Classic hosting and joining.** The waiting-room plan promised classic hosting would be "byte-for-byte unchanged
on the wire" (lobby plan D23). But beta18 changed code on the paths everyone uses:
- `TimberServer`: the accept loop (:485-507), `StartQueuing` (:529-548), `SendErrorMessage` (:771-780), `SendMap`
  (:783-810) and `Close` (:927-934);
- `TimberNetBase`: the sentinel (:509-520) and the waiting-room gate (:524-533);
- `ClientEventIO`: error suppression (:98, :109);
- `ClientConnectionService`: the Connecting box and `CheckWaitingRoom`;
- the Steam invite service, and `WaitsForStart`'s new argument.

**Check:** go through it hunk by hunk. Then run the existing TCP host+guest rig with beta17's and beta20's builds and
compare the bytes of a classic session.

**E5. Hosting from the main menu always opens a waiting room now** (`ServerHostingUtils.cs:151-158`). Consequences to
confirm:
- A guest who is in a game can't join it (D20; this one is documented).
- A guest's desync **Reconnect** is refused when the host rehosts from the main menu (J3).
- `TryToConnect` installs the client (`EventIO.Set`, `ClientConnectionService.cs:142`) before it knows the host has a
  waiting room. So accepting an invite while in a co-op game ends that game first.

**E6. Every save hosted from the main menu is read on the menu's thread** (`SaveColonyReader.Read`,
`LobbyHostPanel.cs:131`). The reader stops early only once it has all four singletons it wants.
`BeaverBuddies.ColonyFactions` exists only in mixed saves, so every other save streams its whole `Singletons` object.
**Check:** time it on the largest save here. Also, the save room's frames now differ from beta19's for every save,
while the changelog says "a room that is not mixed sends beta19's frames".

**E7. Smaller changes that reach everyone.** Confirm each one is harmless:
- Every beaver in every game gets a `CharacterFaction` component. Its save and load do nothing when mixed factions is
  off, so a save without mixed factions should be byte-identical to beta19's for the same game.
- `FoundColonyEvent.faction` and `InitializeClientEvent.hostFactions` are always written, as `null`. (`hostSpeed` uses
  `NullValueHandling.Ignore`; these two don't.)
- `FoundingToolActive` allocates for every preview block on every frame (`ColonyFoundingService.cs:96`), in
  shared-colony games too.
- Every multi-start game reads `StartingBuildingTemplateSpec` and writes it back around each start
  (`MultiStartPatches.cs:88, 99`).
- `JudgeFactions` runs for every event the host judges (`ColonyRulesService.cs:178`).
- Hidden UI is added to every colony card, Trading Post header and room row, and every row icon gets an empty tooltip.

### Tier 1b: the waiting room (beta18/19)

**J1. A waiting-room guest can't load the world** (§0). Confirmed by reading. Still to check: that nothing else in
`LoadMap`'s path, or in the first frames of the guest's load, reads the emptied registry.

**J2. Joining after Start.** A waiting-room session never sets `TimberServer.errorMessage`:
- `CloseLobby` sets `stoppedAccepting` (`IO/ServerEventIO.cs:69-70`), so ReplayService's `StopAcceptingClients` calls
  return early (:206).
- `IsAcceptingClients` therefore stays true all game.
- Newcomers are refused only by `AdmitToLobby`, and `StartQueuing`'s `IsAcceptingClients || fromWaitingRoom` (:530)
  would admit a non-member that got that far.

**Check:** can any connection reach `StartQueuing` without being a member? Candidates: a Steam or TCP joiner in a
running game, and the removal race in J4. A late joiner in a running game is the one thing lockstep can't survive.

**J3. Reconnect after a desync** (see E5).

**J4. Races at release.**
- The 10 s straggler removal (`LobbySession.Update` :246-250 → `RemoveFromLobby`, TimberServer :234-247) can:
  - close the stream of a guest that has just entered the game;
  - or run between `SendMap`'s member check (:795) and `StartQueuing`'s lookup (:529). The guest is then admitted as
    a new player, and then closed.
- Every guest's paced save starts at once, each sleeping between chunks on a pool thread (TimberNetBase :403),
  alongside the pump timer and the flush tasks. Can the pool back up enough on a large save with three or more guests
  to push `StartQueuing` past the 10 s?

**Check:** a StabilityTests rig with several guests, a large save, and pacing on.

**J5. Session values captured before the host's game exists.** The start message is built on a join thread at
release, while the host is still in the menu (TimberServer :495-502 → `InitializeClientEvent.Create`). At that moment:
- `LargeColonySpeedLimit.BeginHostSession` records `EventIO.Get()`, which is null;
- `SessionBoost` is whatever the last session left;
- `hostFactions` reads `MixedFactions.IsOn`, which is false in the menu, so a mixed save hosted through the room tells
  its guests nothing.

**Check:** every field of `InitializeClientEvent` and every `BeginHostSession` latch, waiting-room path against
classic path. Can any difference change the simulation (speed limits, the boost, `joiningClosedAtStart`)?

**J6. Same bytes, same seed.**
- The host loads the save from disk (`LobbySession.cs:261`).
- Guests load bytes captured earlier: for a save room, at the click, possibly minutes before Start; for a new game, a
  frame after the write.
- Both seed from the captured bytes.
- The waiting room loads with `LoadScene(CreateGameSaveParameters)` on both sides, where classic hosting uses
  `StartSaveGame`.

**Check:** can the file change in between (Steam Cloud, an autosave, another program)? Are the two load calls
equivalent? Should the host load the captured bytes instead?

**J7. Tick 0.** After a waiting room, founding, handover and the switch no longer wait for tick 1
(`ColonyRules.WaitsForStart`, :315). The lobby plan's own risks R13 and R14 were:
- a founding at tick 0 has never been played;
- a guest queued late might miss a host action at tick 0.

**Check:**
- that the host waits for every member's `StartQueuing` before loading;
- what a host action taken while a guest is still loading does;
- the paused-replay path (beta12 R10) at tick 0.

**J8. Failure and cancel paths.** Build a table of every exit and test each one against it:
- the host presses Back; a guest presses Leave; the host removes a guest;
- a guest disconnects while waiting, or after Start (their seat and start stay reserved, through `StartedWith`);
- the world-making fails or times out. The host has no timeout if the world-making scene never updates, and the
  keep-alives stop the guests' 120 s watchdog;
- the loading-screen hold is released on every exit (lobby plan R6);
- the host quits to the desktop, and guests get a generic box;
- a failure leaves the "Co-op start" save on disk;
- a welcome and a `LobbyEnd` in the same frame hide the guest's error.

**J9. Threads and blocking.**
- `CancelLobby` waits up to 2 s on the game thread, including during the main menu's container setup (`Plugin.cs:95`
  → `EndStale`).
- `TimberClient.Start` waits up to 3 s on the game thread, and `Dns.GetHostEntry` runs there too.
- A lobby frame's write that is stuck holding the stream lock when the guest enters the game has no timeout (:405,
  :882-885).
- `lobbyTimer` is touched from two threads, and concurrent start messages race on `TimberNetBase.Hash` (used for
  logging only).

**Check:** the lock order (pump, then room, then lane; the stream lock never nested), and each wait's worst case.

**J10. Input from unauthenticated connections before admission** (lobby plan R3).
- A waiting guest may send only lobby frames, of 1 byte to 64 KB, decompressed with a 64 KB cap.
- `SetHello` has no stage check and no once-only guard.
- A direct-IP guest's claimed id decides its colony.

**Check:** fuzz the frames in StabilityTests to confirm the gate and the caps hold against a hostile client, and
find what changing the id after Start can do.

**J11. Steam.**
- The timing of `bb_open` against `AdmitToLobby`.
- Guests never leave the Steam lobby, so after rejoins its 8 slots and `MaxGuests` (7) can disagree.
- The Steam persona name against the name in the hello.

### Tier 1c: mixed factions, desyncs and crashes

**M1. Nothing hashes a beaver's faction or needs.** The daily fingerprint has the faction table and each colony's
adult, child and bot counts (`ColonyDiagnostics.cs:168-176, 199-201`), but nothing covers `CharacterFaction` or
`NeedManager`. A beaver made with a different faction on one computer would go unseen until it changes behaviour.
**Check:** trace M2, and decide whether each colony's counts by faction belong in the fingerprint.

**M2. A new character's faction is fixed the first time it is read** (`factionId ??= Context.Current ?? Unknown()`,
`Factions/CharacterFaction.cs:31`). That relies on `NeedManager.Awake` reading it inside the call that creates the
beaver.

**Check:** by script, find every caller in 1.1.2.4 of `BeaverFactory.Create*`, of `BotFactory.Create`, and of
`EntityService.Instantiate` with a beaver or bot template. Compare them with the eight sites that set the faction.
Already known:
- a single-start new game's beavers use the fallback. It gives the right answer, but uses up the once-per-process
  warning;
- births and bots fall back to the base faction silently when the building's template is common.

**M3. The faction switch** deletes and creates entities inside a replay (`ColonyFoundingService.cs:455-516`).
- **A free district center.** A second district center still under construction doesn't make a colony touched
  (anything with a `DistrictCenter` is skipped, :428). The switch then rebuilds every center **finished**
  (`CreateAsFinished`, :403).
- **The same cells in the same replay.** The new center goes on the old one's cells. beta12 (D2) found that a deleted
  entity stays alive until the frame ends, and `EntityDeletionEndsFramePatcher` interrupts ticking after a deletion.
  **Check:** block occupancy, the district and navmesh rebuild, and that the interrupt splits the replay the same way
  on every computer.
- **What is left behind.** Only `DistrictPopulation.Beavers` are replaced, and only the center's own stock moves.
  Goods carried by beavers, bots, and beavers outside the population stay as they are.
- **Order.** The toolbar refresh runs before any new center exists (see M4).

**M4. UI changes inside replays.** `ColonyFactionService.Set` → `Changed`, and `RefreshToolLocks`, both run
`FactionToolbar.RefreshNow` inside replays. That can call `SwitchToDefaultTool` or `ExitToolGroup` on this computer
only, while `DoPrefix` lets recorded methods through (`ReplayEvent.cs:177`). **Check:**
- every tool's exit path in 1.1.2.4, against the recorded targets;
- that the `LocalSlot` reads inside `Found` and `SwitchFaction` (:384, 388, 501, 507) have local effects only.

**M5. Saved or simulated state written by display helpers.**
- `FactionDecalDefaultPatcher` writes the saved `ActiveDecal` from `DisplayFactionOf` when an entity is created
  (`FactionModelPatches.cs:267-279`).
- `FactionResourceCounterPatcher` filters `ResourceCounterGoodsDropdownProvider.Items` (`FactionDisplayPatches.cs:204-212`).

**Check:** that `DisplayFactionOf` reads only entity state at that moment (the owner may not be stamped yet), and
whether `Items` seeds an automation counter's chosen good.

**M6. Crash sites with both factions loaded.** The plan's §3.2 list came from the session that built the feature.
**Check independently, by script, over all 497 `Timberborn.*.dll`:**
- every `GetSingle<`, `.Single(` and `.First(` over templates, specs or collections;
- every reader of `FactionService.Current`;
- minus what is patched.

Include each faction's Wonder, `FactionGoalsSystem`, the map editor, and the union of blueprint modifiers.

**M7. The 20 replaced method bodies.** Each prefix that returns false re-implements a game method: needs, textures,
the bot factory, outfits, decals, shafts, goods lists, tutorials and Wonder completion. **Check:** each one against the
decompiled original, line by line. For the simulation ones (needs, textures with their one random draw, the bot
factory, stockpile goods), any difference is a desync or a change in behaviour.

**M8. Simulation rules keyed by template (D18):**
- stockpile goods: a `[ThreadStatic]` value set around `StockpileInventoryInitializer.Initialize`, plus a `GetGoods`
  postfix;
- planters;
- yield removers.

**Check:** that both creation and load pass through them, including buildings received in a handover. Also,
`BotFactory._botTemplate` is left at the last faction used (`FactionCharacterPatches.cs:127-134`).

**M9. Wonder completion** now uses the local faction on a tick path (`FactionDisplayPatches.cs:238-259`) and writes
the local profile. **Check:** that nothing in the simulation reads what it writes.

**M10. The founding checks use the base faction's district-center footprint** for every faction
(`ColonyFoundingService.cs:297, 317, 328`). That holds in 1.1.2.4, and RuntimeChecks asserts it. A mod that changes
either footprint breaks it.

### Tier 2: gameplay and display in mixed games

**M11. Needs.** Each faction's bots get only their own critical need, beavers never eat the other faction's food, and
the wellbeing maximum is per character.

**M12. Trading.**
- Exactly the 17 goods plus Science cross, and no Beavers.
- The form, the picker, the prefill, the wishlist and the binding check in the replay all agree.
- Running exchanges end when their terms fail.
- The new Accept check.
- The tick check's cost: `OwnerOf` twice per open exchange per tick.

**M13. Handover.** The nearest colony of the same faction comes first, and a colony keeps running what it received.
Check the toolbar after a handover and after a steward switch.

**M14. Toolbar, models, display.**
- `FactionToolDisabler`, and the refresh through `OnDevModeToggledEvent(null)`, never a real event.
- Dev mode shows everything.
- Models: paths, driveways, decals and shafts. The shafts use a second `ModularShaftModelService` built by reflection,
  with a readonly field written.
- The goods lists, game over, and tutorials off.

**M15. The untouched rule as built** (plan §14): marks don't count, an open exchange does, and common buildings don't.

### Tier 2: the waiting room and factions together

**X1. Seating.** `LobbyRules.SeatingPlan` (the factions' start plan) must equal `LobbyWorldMaker.SeatInRoomOrder` in
every case: guests without ids, guests who left after Start (J8), and more guests than starts.

**X2. Picks reach the world only through the tick-0 save or a founding the host judges** (plan §8 rule 9).
`MixedFactions.Decide` reads `LobbySession.Current` only while `CreatingWorld` and not for a save. **Check:**
- a room whose host turns separate colonies off;
- a host who changes faction after the guests picked;
- a pick that arrives after Start.

**X3. Save rooms.** `SaveColonyReader` and the seating callback predict each row's colony, and in a mixed save a
player with no colony yet gets the picker. **Check:** that the prediction equals what `ColonySlotTable.Resolve` does
at hello, and `hostFactions` in the start message (J5).

**X4. Wire.**
- A new-game room without mixed factions sends JSON identical to beta19's. This is tested.
- Save rooms differ from beta19's (E6).
- A `LobbyFaction` frame is accepted only when the room is mixed and open, and only from a guest who may pick.

### Tier 3: leaks, compatibility, checks and docs

**S1. Statics that outlive a game.**
- `ColonySession.hostFactions` is never reset, and the catch in `FactionCatalog.Load` keeps the old value.
- `FactionDisplay.Reset()` has no caller, so an old scene's `Image` is kept.
- `shaftModels` is cleared only in mixed games.
- `warned` fires once per process.
- `NoticeLockedFaction` shows in whichever game loads next.
- `MixedFactions.AllFactions` is not in `Reset`.
- `ColonySession.joiningClosedAtStart` is not reset at the main menu.
- `LobbyHostPanel.lastSettlementName` survives on purpose.

**C1. Other mods.**
- Mods that read `FactionService.Current` see the base faction; this is a known limit.
- A third-faction mod: the code names no faction, but RuntimeChecks asserts exactly two, and the picker, the catalog
  and the unlock rule have only ever met two.

**C2. Your own mods, installed on this computer.**
- HungryPathing asks each beaver's `NeedManager` for configured need ids (`GetNeedSpec`), which one faction's beavers
  may now lack.
- MixedStorage reflects into `StockpileVisualizers`, while D18 replaces how stockpiles get their goods.
- LateGamePerformance, OptimizedLocalHousing, PersistentWorkAreas, TipsyTail and PerformanceLog: scan their patch
  targets against the 48.

**C3. Saves.** Saves from beta17 to beta19 load in beta20 (no `ColonyFactions`, so mixed factions is off). A mixed
save opened without the mod loses a faction; that is documented.

**T1. Checks that check less than their names say.**
- The gate scan looks only for the text `MixedFactions.IsOn` somewhere in each `Factions/` patch class. It doesn't
  check that the gate comes first, and it misses `MultiStart/`, `Colonies/` and `Lobby/`.
- The stacking scan misses two names on one line, named arguments, and a class-level name paired with a method-level
  one.
- RuntimeChecks checks names, not signatures.
- "Not mixed sends beta19" covers only the new-game summary.
- Nothing runs `ClientConnectionService`, the panels, `LobbySession` or `LobbyWorldMaker`, and nothing runs a real
  two-process room.

**T2. Docs.**
- TWO-COLONIES :75-76 still says a save room shows no colony.
- The changelog says "a room that is not mixed sends beta19's frames" and describes the wire change as "optional
  fields".
- The mixed-factions plan's §8 names `LobbyFactionChoice.Mine`, which is now `LocalFactionPick.Mine`.
- `Base` is saved but never read back.

---

## 5. Method

1. **Baseline.** Build Release and Release Steam at `79d4757` (0 warnings), and run StabilityTests (391) and
   RuntimeChecks (349). Also build `v1.4.0-beta17`, for the classic-path comparisons (E4). Decompile 1.1.2.4 into the
   scratchpad.
2. **Sweeps by script,** kept as checks where that's cheap:
   - Harmony's own resolution of every patch in the mod, for both builds (E2);
   - the dependency-injection graph: every new constructor against each scene's bindings (E1);
   - character creation sites (M2), single-faction assumptions (M6), and tool exits against recorded methods (M4);
   - the 20 replaced bodies against the game's originals (M7);
   - a classic session's bytes, beta17 against beta20 (E4);
   - `SaveColonyReader` timed on the largest save here (E6).
3. **Waiting-room rigs** in StabilityTests:
   - the `ClientConnectionService` path, as far as it runs without Unity (J1);
   - several guests with a large, paced save (J4);
   - every exit in J8's table;
   - hostile frames (J10).
4. **Reviewers in parallel.** I propose three plus me; each tries to refute its own findings first.

   | Reviewer | Leads |
   |---|---|
   | **A**, the join and the waiting room (and the rigs) | E4, E5, J1–J11 |
   | **B**, mixed factions against the decompiled game | M1–M15, E3 |
   | **C**, everything that reaches everyone | E1, E2, E6, E7, C1–C3, T1, S1 |
   | **Me** | X1–X4, the switch and founding end to end, T2, and re-checking every finding before it goes in the report |

5. **Output.** `design/REVIEW-FINDINGS-1.4.0-beta18-20.md`, in the beta11 findings' format. Each finding gets:
   - evidence, as `file:line` in the mod and in the game;
   - severity: desync, crash, session-stopper, gameplay, display or docs;
   - who it hits: everyone, waiting room, or mixed;
   - a proposed fix, with its wire and save impact;
   - the check to add, and a test-script line.

   The report also gets a "found sound" list, so later sessions don't redo that work.
6. **No code changes and no release in this pass.** Fixes and a beta21 cut are the next step, when you say so.

## 6. What a playtest would settle faster

None of the three releases has been played, and one run of the game catches whole groups of E leads at once. In order
of value:

1. **Ten minutes on one computer, both features off.**
   - Start a solo new game.
   - Load an old save.
   - Open the map editor.
   - Host a save from inside a game, and join it from a second copy of the game if you can.

   A clean `Player.log` settles most of E1 to E3.
2. **Script D (the waiting room), on two computers.** I expect it to fail at the guest's load (J1). That's worth
   knowing before anything else.
3. **Script F (mixed factions).**

The review doesn't wait for these.

## 7. Decisions for you

1. **J1 now or later?** It breaks every waiting room that has a guest, in all three releases, and the waiting room is
   now the only way to host from the main menu.
   - (a) Fix it first, as a small beta21 hotfix: build the tip before the reset, and stop `CheckWaitingRoom` once the
     save has arrived. **Recommended.**
   - (b) Fold it into the review's fixes.
2. **Scope after the review:** findings only, or findings, fixes and a release in one run (the beta12 pattern)?
3. **Reviewers:** three plus me (recommended, for three releases), or two plus me as for beta12.

## 8. Out of scope

- Playing the game (not possible from here); the playtest lines get written instead.
- The lockstep core, and beta12's and alpha10's found-sound lists.
- The Stability Fork: it has neither the waiting room nor separate colonies, so nothing here ports.
- Save compatibility beyond "older saves still load".
- Direct-IP identity as a security question, apart from J10's input limits.
- Performance outside the join, the tick, `SaveColonyReader`, and loading both factions (measured once).
