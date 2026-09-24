# Pre-game lobby: invite players and ready up before a new co-op game starts — design and implementation plan

This is a handoff document for the session that builds the feature. It was written on 2026-09-22 from a full
investigation of the decompiled game (Timberborn **1.1.2.4**: MainMenuPanels, CoreUI, GameSceneLoading, SceneLoading,
GameStartup, GameSaveRuntimeSystem, SettlementNameSystem(UI), MainMenuModdingUI and more), of the game's own UI source
files (`StreamingAssets/Modding/UI.zip`), and of this repository at **`8a89012` (1.4.0-beta16, `main` =
`trading-exchange`)**. Read it to the end before writing code.

Labels used below:

- **(read)**: checked against the decompiled game, the game's UXML/USS, or the repo at `8a89012`. Trust it, but a line
  number may have moved.
- **(decision)**: decided by the maintainer (D1 to D6) or by this plan (D7 onward). Do not re-open it; if it proves
  impossible, stop and say why.
- **(verify)**: a belief not fully checked. Settle it in Phase 0 or at the step that needs it, before building on it.
  Some can only be settled in the game; those are on the test script (§10.3) and the release notes say so.

Game references are `Assembly: Class.Member` with the `Timberborn.` prefix dropped (for example
`MainMenuPanels: NewGameModePanel.StartNewGame`). Decompile what you need with
`ilspycmd "<Managed>/Timberborn.X.dll" -r "<Managed>" -o <scratch>/dec` (Managed is
`C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\Managed`). The game's UI source files are
`Timberborn_Data/StreamingAssets/Modding/UI.zip` (377 `.uxml`, 31 `.uss` under `Views/<Area>/`): read them with
Python's `zipfile` rather than extracting (deep paths break Windows MAX_PATH). Sprites are in `Resources` and load with
`UnityEngine.Resources.Load<Sprite>("<path without extension>")` (write `UnityEngine.Resources` in this mod: it has its
own `Resources` class).

Mock-ups of the two pages, drawn from the game's own sprites and USS values (not a game screenshot), are in
[`pre-game-lobby/lobby-host.jpg`](pre-game-lobby/lobby-host.jpg) and
[`pre-game-lobby/lobby-guest.jpg`](pre-game-lobby/lobby-guest.jpg). They are the target look (the guest's shows the
Not ready button hovered). §5 says exactly how each piece is built; where a mock-up's wording differs from §11.1, the
strings in §11.1 win.

---

## 1. Goal and exit criteria

Today a co-op game with a colony each takes four steps and two loading screens before player 2 can act: the host
starts a new game alone, saves it, goes back to the menu, loads it with **Host co-op game**, waits paused for the
guest, and only after the host unpauses may the guest place their district center (founding waits for the first tick,
because joining is open until then).

This feature adds a **waiting room** to the New Game flow. The host picks faction, map and difficulty as usual, clicks
**Host co-op game** instead of Start, names the settlement, and lands on a **Co-op Game** page where friends join (Steam
invite or direct IP) and mark themselves **ready**. The host clicks **Start Game**; the host's computer creates the
world behind the loading screen, saves it at tick 0, and every computer loads that same save. Everyone arrives in the
game together, paused at tick 0, with joining already closed. The host has the map's district center; every other
player may place theirs straight away, even while paused.

Done means all of this holds:

1. On the New Game panel's Game Mode page, **Host co-op game** sits beside **Start** and is enabled exactly when Start
   is. Clicking it asks for the settlement name in the game's own settlement-name box, then opens the waiting room.
2. The waiting room is a page of the New Game wizard, built from the game's own templates, classes and sprites (§5).
   It lists every player with name, colony and ready state; guests toggle ready; the host can invite (Steam), remove a
   guest, cancel, and start. Starting with a guest not ready, or with nobody, asks first.
3. A guest who accepts a Steam invite or joins by IP from the main menu sees a short **Connecting** box, then the same
   page with **I'm ready** / **Leave**, and live updates of the roster and the host's progress.
4. After **Start Game**, nobody new can join; every player who was in the waiting room is in the game, from the same
   bytes, paused at tick 0. The host never sees the freshly made world before the reload.
5. In a separate-colonies game, each guest gets the colony the waiting room showed. On a multi-start map, each gets the
   start of that colony. A guest without a start is offered **Place your district center** as soon as they are seated,
   and may place it while the game is still paused.
6. **Load Game → Host co-op game**, the in-game **Save and Rehost**, **Join co-op game** to a classic host, and every
   single-player game behave exactly as at `8a89012`, apart from the guest's new Connecting box (D21). The checks prove
   the gates (§10).
7. StabilityTests and RuntimeChecks pass, both builds have 0 warnings, and the docs, changelog and site describe the
   feature honestly as **not yet played**.

---

## 2. Decisions

### From the maintainer (2026-09-22)

- **D1: New games only.** The waiting room comes from the New Game panel. **Load Game → Host co-op game** keeps today's
  dialog and flow. (Build the waiting-room code so a later version could offer it for saves too, but don't wire that.)
- **D2: The host may start at any time.** If any guest in the waiting room is not ready, or nobody has joined, the host
  is asked first (a native yes/no box). Everyone in the waiting room comes into the game, ready or not.
- **D3: Joining closes for good at Start.** A player who was not in the waiting room cannot join after it; a guest whose
  game fails to load cannot come back. Both need the host's **Save and Rehost**, as today after the first tick.
- **D4: Player 1 has the map's district center; the others place theirs when they please**, including while the game
  is still paused at tick 0.
- **D5: Folktails is the case to build and test.** Nothing here is faction-specific: the faction the host picked on the
  New Game panel goes into the save, and guests don't choose one. (A per-player faction choice belongs to the mixed
  factions work, `design/MIXED-FACTIONS-PLAN.md`; the waiting room is where it would go later. Not now.)
  *Since 1.4.0-beta20 it does:* with Mixed factions on, each player picks a faction on this page with the faction
  page's own switcher (`Lobby/LobbyFactionPicker.cs`; `design/MIXED-FACTIONS-PLAN.md` §6).
- **D6: The look must be as close as possible to Timberborn's own UI.** Every frame, button, list, checkbox, icon and
  text style is the game's own (§5). Nothing hand-drawn where the game has a piece for it.

### Made by this plan

- **D7: The waiting room is the fourth page of the game's New Game wizard.** It is the game's `MainMenu/NewGameTemplate`
  (banner, capsule title, Back and Next buttons), pushed with `PanelStack.HideAndPush` after the Game Mode page, as
  each wizard page pushes the next. Title **Co-op Game** for the host; **Kyler's Game** (the host's name) for a guest,
  who sees the same page. Back = **Cancel** / **Leave**; Next = **Start Game** / **I'm ready** (**Not ready** once
  ready).
- **D8: The settlement is named first, in the game's own settlement-name box.** Clicking **Host co-op game** shows
  `Game/SettlementNameBox` (the box a solo new game shows after the start is placed) over the Game Mode page, with its
  "Name your settlement" text, input and a Next button, plus a **Cancel** button. The name is checked the way the game
  checks it (`GameSaveRepository.CreateDirectoryForSettlement`: taken or invalid names show the game's own messages
  `Saving.TakenName` / `Saving.InvalidName`) and then goes into `NewGameConfiguration.SettlementName`, so the game never
  prompts in the new world. Guests see it on the page.
- **D9: The server opens when the waiting room opens**, with the same TCP listener and Steam lobby as today's hosting,
  in a new **lobby mode**. The server lives in a static `LobbySession`, **not in `EventIO`**, until the saved world is
  loaded (§4.5 says why). The host's colony settings are latched at that moment (`ColonySession.BeginHostSession`); the
  page is modal, so Mod Settings can't change meanwhile.
- **D10: Waiting-room traffic is a new TimberNet phase after the compatibility handshake**: new frame kinds, not replay
  events, and not Steam lobby member data. It works the same over Steam and direct IP, and an old build never reaches it
  (the handshake refuses any other build).
- **D11: Seats follow the waiting room's order.** Player numbers are given when a guest enters the waiting room. In a
  separate-colonies game the host is colony 1 and guests colonies 2, 3, 4 in the order they entered among those still
  there; a fifth guest and later are **helpers** of colony 1, as in game today. Before saving the new world, the host
  writes that order into the colony slot table, so each guest's hello finds its colony. In a shared game there are no
  colonies and rows show only **Host**.
- **D12: On a multi-start map, the starts filled are `min(Players field, 1 + guests at Start, 4)`**, so no start
  colony is made for a player who isn't there. Guests beyond the filled starts found their colony (D4).
- **D13: Ready is a guest's own toggle.** The host is always ready. A guest who has not yet said hello shows as
  **Joining…** and counts as not ready. Ready changes nothing but the page and the confirm in D2.
- **D14: The host may remove a guest** (a framed red-cross button on the guest's row, confirmed with a yes/no box). A
  guest may **Leave** (confirmed). **Cancel** on the host's page ends the waiting room for everyone (confirmed when
  someone is in it). Each guest is told why in a plain native box, not the "Joining failed" dialog.
- **D15: The new world is never seen.** The loading screen stays up from **Start Game** until the reloaded save has
  loaded: the game's own `LoadingScreen`, with the mod's text as its tip (`Creating the world for your co-op game…`,
  then `Loading your co-op game…`; guests: `Loading Kyler's co-op game…`). The save is
  `"<timestamp> Co-op start"` in the named settlement.
- **D16: Founding, handing over and colony switching no longer wait for the first tick in a waiting-room game.** A new
  session flag, `ColonySession.JoiningClosedAtStart`, is true on the host from **Start Game** and told to guests in the
  first message. `ColonyRules.WaitsForStart` returns false when it is set. The host's own **Hand to …** buttons keep
  waiting for the first tick (a guest still loading looks away until their hello is played).
- **D17: No setting.** The button is the opt-in; the old path stays for saves.
- **D18: Not in this version:** chat in the waiting room, a map thumbnail, the mod-list comparison in the waiting room
  (mismatch warnings still appear in the game, as today), per-player faction choice, and the waiting room for saves.
- **D19: Keep-alive.** The host sends the waiting room's state every second from a background timer (not the game
  thread, which freezes while the world is made). A guest's page offers **Keep waiting** / **Leave** after 120 s without
  any word from the host (not while the save is arriving). Nothing ever drops a guest automatically.
- **D20: The guest's waiting room is a main-menu page.** A guest who accepts a waiting-room invite while in a game (or
  uses the in-game **Join co-op game**) is disconnected with a native box: "Kyler is waiting for you in a co-op waiting
  room. Return to the main menu, then accept the invite again (or join from there)." Classic hosts are joined from a
  game as today.
- **D21: The guest's Connecting box.** After **Join co-op game** or accepting an invite, a guest sees a native box
  "Connecting to Kyler…" (or the address) with **Cancel**, until the waiting room opens, the save arrives (classic
  host), or an error shows. It replaces today's "Joined! Receiving map..." box (Steam only today; nothing is shown for
  direct IP). This is the one visible change for classic hosting, and it is an improvement.
- **D22: At most 7 guests** (the Steam lobby holds 8). An eighth is refused with "The waiting room is full."
- **D23: Classic hosting is byte-for-byte unchanged on the wire.** Lobby frames only ever flow in lobby mode.

---

## 3. The flow

### 3.1 Host

1. Main menu → New Game → faction → map → Game Mode page (vanilla, plus the mod's Players field on multi-start maps).
2. **Host co-op game** (beside **Start**). The game's settlement-name box asks for the name (D8). **Cancel** or Esc
   returns to the Game Mode page.
3. The **Co-op Game** page opens (D7). The server starts in lobby mode (Steam lobby + TCP port); **Invite Friends** is
   enabled once the Steam lobby exists. The host is the first row, ready.
4. Guests appear as they connect: **Joining…**, then their name and **Not ready** / **Ready**. The status line says
   what the host is waiting for ("Anna is not ready yet. You can still start.").
5. **Start Game** (or Enter). If a guest is not ready, or nobody has joined, a yes/no box asks first (D2).
6. Nobody new can join from here on. The loading screen comes up with "Creating the world for your co-op game…", the
   world is made (single player, never shown), the slot table is filled in waiting-room order (D11), and it is saved at
   tick 0 as `<timestamp> Co-op start`.
7. The save's bytes go to every guest at once; the host waits (at most 10 s) until every guest's join has been queued,
   then loads that save as host, as **Load Game → Host co-op game** does today ("Loading your co-op game…").
8. In the game: paused at tick 0, every guest connected and loading. The connection panel shows a guest as
   **loading** until their hello is played (§6.6). There is no "Joining: open" line and no "Start the game?" question:
   joining is already closed. The host unpauses whenever they like.

### 3.2 Guest

1. Accepts the Steam invite (or the host's lobby from the friends list), or **Join co-op game** → address, from the main
   menu. The **Connecting** box shows (D21).
2. The build check passes; the host's first word is the waiting room's welcome. The Connecting box closes and the
   **Kyler's Game** page opens: the host's faction, map and difficulty, the settlement name, and the players.
3. **I'm ready** toggles ready (the button then reads **Not ready**; the guest's own row checkbox toggles it too).
   **Leave** (confirmed) disconnects and returns to the main menu.
4. After the host's **Start Game**: the status reads "Kyler is creating the world…", then "Receiving the world…"; the
   ready button is disabled.
5. The save arrives and loads ("Loading Kyler's co-op game…"). The game opens paused at tick 0. Once the guest is
   seated, a guest without a start colony is offered **Place your district center** at once, and may place it while
   paused.
6. If the host cancels, removes the guest, or fails to create the world, the page closes and a plain box says why.

### 3.3 Timeline of one start (host `H`, guest `G`)

| Step | Where | What happens |
|---|---|---|
| 1 | H menu | `LobbySession.Start()`: close to newcomers (TimberNet gate + Steam `bb_open=0`, `bb_state=started`); latch `ServerEventIO` as not accepting; `ColonySession.JoiningClosedAtStart = true`; freeze the member list; broadcast `creatingWorld`; hold the loading screen; `ISceneLoader.LoadScene(CreateNewGameParameters(cfg), tip)` |
| 2 | H new-game scene | Single player (`EventIO` null). The settlement name is preset, so no prompt. The mod's multi-start patch places the starts (D12) |
| 3 | H | `NewGameInitializedEvent`: pre-seed the slot table; queue the save; don't unpause |
| 4 | H | Save written. A frame later: read its bytes; broadcast `sendingWorld`; release every parked join with the bytes |
| 5 | G (pool thread) | `StartQueuing` → the save frame → `SetState` → the init event (with `joiningClosedAtStart`) → activity lane |
| 6 | H | All joins queued (or 10 s): `EventIO.Set(io)`, `DeterminismService.InitGameStartState(bytes)`, release the hold, `LoadScene(CreateGameSaveParameters(save), tip)` |
| 7 | G | `LoadMap` → loads the same bytes |
| 8 | H, G | Both paused at tick 0. G's hello is played → seated in its reserved colony → founding prompt |

---

## 4. Facts the design rests on

### 4.1 The game's New Game flow (read)

- `MainMenuPanels: MainMenuPanel.NewGameClicked` → `HideAndPush(NewGameFactionPanel)` → `NewGameMapPanel.Open(faction)`
  (`HideAndPush`) → `ShowParametersPanelIfMapValid` (the only map check: `MapVersionValidator`) →
  `NewGameModePanel.SelectFactionAndMap(faction, mapItem)` + `HideAndPush(NewGameModePanel)`.
- `NewGameModePanel` uses `MainMenu/NewGameModePanel`, which wraps `MainMenu/NewGameTemplate`. Its Start button is the
  template's `NextButton` (a `NineSliceButton .menu-button .menu-button--large-text`, relabelled
  `NewGameConfigurationPanel.Start`) with a click lambda calling private `StartNewGame()`; Enter
  (`OnUIConfirmed`) calls the same. `StartNewGame()`:
  ```csharp
  if (TryGetValidatedGameMode(out var gameMode))
      _gameSceneLoader.StartNewGame(new NewGameConfiguration(_factionSpec.Id, _map.MapFileReference, gameMode, string.Empty));
  ```
- `UpdateNextButton() { _nextButton.SetEnabled(TryGetValidatedGameMode(out _)); }` runs from `UpdateSummary()` and as
  the custom-mode controller's change callback. `GetPanel()` runs `UpdateSummary()` every time the page is shown. The
  summary label is `SummaryText` ("Folktails - Diorama - Normal").
- `NewGameConfigurationSystem: NewGameConfiguration(string factionId, MapFileReference, GameModeSpec, string
  settlementName)`. `GameSceneLoading: GameSceneParameters.CreateNewGameParameters(cfg)` /
  `CreateGameSaveParameters(saveRef)` are public statics. `GameSceneLoader.StartNewGame` = `ISceneLoader.LoadScene(
  CreateNewGameParameters(cfg), GetTip())` (a random tip).
- **Settlement name.** The menu has no name step; vanilla passes `string.Empty`. In the game, `SettlementReferenceService.
  Load` uses a non-blank `SettlementName`; otherwise `GameStarter.SpawnStartingBuilding` places the start and shows
  `Game/SettlementNameBox` (`PushOverlay`, which pauses). Its confirm calls `GameSaveRepository.
  CreateDirectoryForSettlement(text)`: `OK`, `NameTaken` (folder exists and isn't empty) → `Saving.TakenName`,
  `NameInvalid` → `Saving.InvalidName`. A preset name skips the prompt **and** that check, so the waiting room must run
  the check itself (it is bound in the main menu). Saves go to `<Saves>/<Settlement>/<Save>.timber`.
- The mod already patches this page: postfixes on `NewGameModePanel.SelectModeButton` / `SelectFactionAndMap` (open
  Customize on multiplayer maps) and on `CustomNewGameModeController.Initialize` / `GetGameMode` (the Players field,
  which wraps the mode in `MultiplayerNewGameModeSpec(mode, players)`; `StartBuildingsService.MaxStartLocations` reads it
  back while the world is made) — `BeaverBuddies/MultiStart/MultiStartPatches.cs`.

### 4.2 Making, saving and reloading a world (read)

- **Load frame.** All singleton `Load`/`PostLoad` calls run in one frame. Events posted before `EventBus.PostLoad` are
  delivered then.
- **`GameStartup: GameInitializer`**, one state per frame: `SpawnBeavers` → `PostSpawnBeavers` (posts
  **`NewGameInitializedEvent`**) → `UnpauseGame` (`_speedManager.ChangeSpeed(1f)`) → `ShowUI` (posts
  `ShowPrimaryUIEvent`) → `Finished`. A loaded save jumps to `ShowUI` and never unpauses: **loaded games start paused**.
- At `NewGameInitializedEvent` the start building(s) and beavers exist, speed is 0 (`SceneLoader` sets
  `Time.timeScale = 0`; `SpeedManager` starts at 0) and no tick has run.
- **The game never saves by itself at the start of a new game** (first autosave after 10 minutes, and
  `GameStartupAutosaveBlocker` holds until `ShowPrimaryUIEvent`). `GameSaver` checks no blockers.
- `GameSaver.QueueSaveSkippingNameValidation(ref, cb)` writes in `GameSaverUnityAdapter.LateUpdate`;
  `SaveInstantlySkippingNameValidation` writes at once. **`OnSaveCompleted` runs while the file is still open**: read
  the bytes a frame later (`RehostingService` already does).
- **The host must reload, not play on.** `GameScene: DateSalter.Save` draws two random numbers on every save;
  `PopulationSampler._performInitialSample` is not saved; the game randomises some values on load that it doesn't save
  (`ServerEventIO.cs` header comment); the mod seeds the random state from the save's bytes
  (`DeterminismService.InitGameStartState`); co-op services are bound only when `EventIO` is set before the load.
- **Loading a save from a game scene** (`StartSaveGame`, or `ISceneLoader.LoadScene`) is a supported vanilla path (the
  in-game Options → Load uses it). It shows the loading screen, doesn't pass through the main menu, and so doesn't run
  the mod's main-menu configurator: `EventIO` survives a game-to-game load. The old scene's singletons unload; the new
  Game container runs `ReplayConfigurator`, which binds co-op services only if `!EventIO.IsNull` (`Plugin.cs:21-77`).
- **Faction** travels as `NewGameConfiguration.FactionId`, is saved by `GameFactionSystem: FactionService`, and is read
  from the save first. Faction unlocking is never checked on load. A guest needs only the save.

### 4.3 The loading screen (read)

- `SceneLoading: LoadingScreen` is one instance for the whole run (Bootstrapper context, exported, `DontDestroyOnLoad`,
  sort order 10000). `Enable(string tip)` shows it with `Core.Loading` and the tip; `Disable()` hides it. Both fire
  public events. The mod already injects it (`PingService`, `PlayerActivityService`).
- `SceneLoader.LoadSceneCoroutine`: waits while another load runs; `Enable(tip)`; `timeScale = 0`; one frame;
  `SceneManager.LoadScene(2)`; `UnloadUnusedAssets`; `GC.Collect`; **`Disable()`**. So after the new-game scene has
  loaded, the screen would come down and the world would show; the hold (§6.4) prevents that.
- `ISceneLoader.LoadScene(parameters, tip)` takes the tip text directly: that is how the mod's text reaches the screen.
- `Muter` mutes the master volume while the screen is up. The mod's `PingService` / `PlayerActivityService` tear down on
  `LoadingScreenEnabled` (they aren't bound in the single-player scene).
- Coroutines on the scene loader's `CoroutineStarter` MonoBehaviour survive scene loads
  (`ServerHostingUtils.GetMonoBehaviour`). `WaitForSeconds` stalls at `timeScale 0`: use unscaled time.

### 4.4 The mod's join path today (read)

- **Framing.** Every frame is a 4-byte big-endian length and a payload. The guest's first frame after the handshake is
  either a 0 length (an error message follows) or the raw save; everything later is gzip JSON dispatched on `"type"`
  (`TimberNetBase.cs:506-566`). "The map is always the first frame a client reads" (`TimberNetBase.cs:135-137`) is
  enforced only in the guest's reader.
- **Host join order** (`TimberServer.cs:143-199`), per connection: refuse if joining is closed → compatibility
  handshake (15 s timer; `CompatibilityHandshake.cs`) → refuse if closed meanwhile → `await mapProvider()` →
  `StartQueuing` (under the broadcast lock: refuse if closed; add to `clients`; **give the player number**; record the
  Steam-verified id) → the save (paced on TCP, 1 MB/s) → `SetState` → the init event → `FinishQueuing` (send lane,
  activity lane with the chat history) → **only now** the receive thread for this guest (`:194`).
- **The provider** is `Func<Task<byte[]>>`; today it wraps fixed bytes (`ServerEventIO.cs:60-70`).
  `TimberServer.UpdateProviders` exists and has no callers. A join awaiting it has **no timeout**, and the guest's read
  blocks with none (TCP sets no `ReceiveTimeout`; Steam takes with `Timeout.Infinite`).
- **A parked join is in no list**: not `clients`, not numbered, no pings, and `Close()` / `AbortSession()` don't close
  it.
- **Errors.** `SendErrorMessage` writes the 0 length and the gzip text as two writes not under one lock, with the one
  global message `StopAcceptingClients` set (`TimberServer.cs:444-450`). The guest shows the join-failed dialog.
- **Handshake** (`CompatibilityHandshake.cs`, `BuildCompatibility.cs:10-13`): the identity includes each assembly's
  module version id, so two different builds always refuse each other before any later frame. Lobby frames need no
  version negotiation.
- **Pings, roster, heartbeats** run only from `TimberServer.OnUpdate`, pumped by the game scene's `ReplayService`, and
  only for guests with an activity channel. Nothing pumps the server in the main menu or during loading.
- **Why a server started in the menu survives into the game:** `EventIO` is a static the Game configurator never
  resets; network threads aren't scene objects; `SteamNet`'s pump is `DontDestroyOnLoad`; the Steam lobby belongs to the
  `SteamListener`. Only the main-menu configurator resets `EventIO` (`Plugin.cs:91-92`).
- **Guest.** `ClientConnectionService.TryToConnect` sets `EventIO` to the `ClientEventIO` and its `UpdateSingleton`
  pumps it (MainMenu and Game contexts). `LoadMap` (`ClientConnectionService.cs:274-293`) resets singletons, writes the
  save as `Online Games/<hash>`, seeds the random state and calls `StartSaveGame`.
- **Steam** (`SteamListener.cs`): the lobby is created at server start (`CreateLobby(FriendsOnly, 8)`), type
  FriendsOnly or Invisible from `Settings.LobbyJoinable`, data `bb_open = "1"`; `CloseToNewGuests()` sets not joinable
  and `bb_open = "0"`. Only lobby members may connect (5 s grace). `SteamOverlayConnectionService.OnLobbyEntered`
  refuses `bb_open == "0"` with `JoinCoopGame.Error.HostStarted`, otherwise `TryToConnect(owner)` and today's
  "Joined! Receiving map..." box. The host sees a Steam guest's persona name; a TCP guest has none.

### 4.5 Why the lobby's server stays out of `EventIO` (read)

If the lobby's `ServerEventIO` were `EventIO` while the new world is made, that scene would be a co-op scene: the mod's
`ReplayService` would close joining at its first tick or first change (permanently, and `StartQueuing` would then
refuse every waiting guest), events, tick count and hash from that scene would carry into the real one, and the
provider would hold no bytes. Keeping it in `LobbySession` makes the new-world scene a plain single-player load; setting
`EventIO` just before loading the save is exactly `LoadAndHost`'s confirm path (`ServerHostingUtils.cs:166-178`).
Precedent for setting `EventIO` inside a single-player scene right before `StartSaveGame`: **Host co-op game** on the
in-game Options → Load box (the `LoadGameBox` patch is context-free).

### 4.6 Where joining closes, and the tick-0 gates (read)

- `ServerEventIO.StopAcceptingClients(gameChanged)` (`:159-192`, idempotent) latches `stoppedAccepting`, sets
  TimberServer's permanent error message (English, hard-coded) and closes the Steam lobby. Called only by
  `ReplayService` at the first game-changing action at tick 0 (`:422-429`) and at tick 1 (`:945-951`).
- `ServerEventIO.IsAcceptingClients => !stoppedAccepting && NetBase.IsAcceptingClients` is what the rest reads:
  - `HostStartGate` (`HostStartRules.ShouldHold(isHost, acceptingClients, ticks == 0, changesGame, !confirmed)`) holds
    the host's first change and asks "Start the game / Keep waiting";
  - `ColonyRulesService.AllowOnHost:106-114` refuses a guest's change at tick 0 while accepting;
  - the connection panel's "Joining: open" (`ConnectionPanelService.cs:358`).
  All three go quiet once `stoppedAccepting` is latched. `HostStartGate` can never fire if the latch is set before the
  host's `ReplayService` is loaded (`RecordEvent` ignores everything before `IsLoaded`).
- **`ColonyRules.WaitsForStart(foundingOrHandover, hostTicksSinceLoad) => foundingOrHandover && hostTicksSinceLoad < 1`**
  (`ColonyRules.cs:304-305`) ignores whether joining is open. Callers: `ColonyRulesService.cs:98` (host verdict for
  `FoundColonyEvent`, `ColonyHandoverEvent`, `ActAsColonyEvent`, **the host's own included**),
  `ColonyFoundingService.cs:115` (the local prompt, Ctrl+K, `WhyNot`), `ColonyHandover.cs:336` (the host's Hand-to
  buttons), `TradeOverviewPanel.cs:425` (steward buttons). Test: `StabilityTests/ColonyChecks.cs:617-625`.
- Nothing else needs the first tick: the hello is played at tick 0 (`ChangesGame` false), the founding's starting
  settings come from the save, the colony digest resets in `ReplayService.Initialize` and is compared at tick 1, tick-0
  events replay through the same path as ticks, and first-day presence skips its first check. **But no founding has
  ever been played at tick 0 in co-op** (every flow refuses it today): it is on the test script.

### 4.7 Seating (read)

- `LocalPlayerIdentity.Id` is `steam:<id>` when Steam runs, else `local:<guid>` (PlayerPrefs); `Name` is
  `Settings.PingDisplayName` (`ColonySlotService.cs:18-48`). Static: readable in the menu.
- A brand-new save has **no slot table**. On hosting, `ColonySlotService.PostLoad → SeatHost` gives the host slot 0.
  A guest's `PlayerHelloEvent` is sent once its game has loaded; the host's `HostSeat` → `ColonySlotTable.SeatHello`
  → `CheckHello` (Steam: the verified id wins) → `Resolve`: a known id keeps its slot, a new id takes the **lowest
  free** slot, none free → helper on the host's slot (`ColonySlotTable.cs:82-136, 188-205`). So without help, slots
  follow load-finish order, not the waiting room's.
- `ColonySlotService.Apply` (every computer) → the guest's own hello → `OfferFounding()` (`:216-228`).
- The table is saved (`BeaverBuddies.ColonySlots`) only in separate-colonies games with entries.
- Multi-start: starts are ordered by `PlayerIndex`; start N (1-based) → slot N-1 (`MultiStartPatches.cs:62-103`); the
  loop stops at `MaxStartLocations()`.

---

## 5. The look (D6): exactly how each piece is built

### 5.1 Rules

1. **Build from the game's templates**, loaded with `VisualElementLoader.LoadVisualElement("<path>")` (it loads
   `UI/Views/<path>`, clones it, and runs `VisualElementInitializer` on it). Templates carry their own style sheets.
2. **Only classes that exist in the main menu's style sheets.** The main menu's panel stack lives in
   `MainMenu/TitleScreen.uxml`, which attaches **CommonStyle, SteamWorkshopStyle, CoreStyle, OptionsStyle,
   MainMenuStyle, MainMenuMiscStyle, ModdingStyle**. The in-game sheets (GameStyle, EntityPanelCommonStyle, …) are
   **not** loaded, so `entity-panel__text`, `entity-sub-panel`, `game-scroll-view`, `entity-panel__toggle` and
   `progress-bar--green` do nothing there. **Do not use `Util/NativeElements.cs` in the waiting room**: its helpers are
   built on those in-game classes.
3. **Nine-slice classes** (`--background-image`: frames, `menu-button`, `wide-menu-button`, `text-field`, `bg-box--*`)
   draw only on `NineSliceVisualElement`, `NineSliceButton`, `NineSliceTextField` and **`LocalizableButton`** (which has
   its own nine-slice background; that is why `ButtonInserter` copies look right). Plain `background-image` classes
   (`close-button`, `checkmark-green`, `button-cross`, checkboxes, `new-game__summary`, `mod-manager-box__list`) draw on
   any element.
4. **Disabled = `SetEnabled(false)`**: Unity's runtime theme gives `.unity-disabled { opacity: 0.5 }`, and hover and
   click sound stop. That is how the game greys Start and Load. No custom disabled style.
5. **Initialise only what you build in code**, once (`VisualElementInitializer.InitializeVisualElement`): click
   sounds, scroll-bar art, text-field hotkey blocking. **Never initialise a subtree twice that holds a `ScrollView`**
   (each pass adds scroll-bar decorations). **Never pass a `LocalizableButton` without a `text-loc-key` through the
   initializer** (`VisualElementLocalizer` throws "text-loc-key is not set").
6. **Content goes into the template's content element**, found by class: `root.Q(className: "new-game__main-content")`.
   The `content-container` attribute applies only to template instances. **(verify)** in the first build.
7. **Sprites for states, not glyphs.** The menu font is Noto Sans Display (fallbacks Noto Sans, Noto Sans Symbols 2);
   don't draw ✓ or ✗ as text.
8. **Panel stack discipline.** `PanelStack.Pop(p)` throws if `p` isn't on top: pop any yes/no box you pushed over a
   page before popping the page. Esc → top panel's `OnUICancelled`, Enter → `OnUIConfirmed`.
9. **No hand-drawn elements.** The mock-ups' only non-native piece in the research draft (coloured player dots) was
   dropped. If a piece is missing from the game, leave it out and say so in §12.

Class → style sheet, for every class the lobby adds by hand (read from UI.zip; RuntimeChecks enforces this list, §10.2):

| Class | Sheet | | Class | Sheet |
|---|---|---|---|---|
| `new-game__summary`, `new-game__summary-text` | MainMenuMisc | | `text--default`, `text--grey`, `text--big` | Core |
| `faction-item__logo-background`, `faction-item__logo` | MainMenuMisc | | `text--yellow`, `text--centered` | Common |
| `unlock-condition__text` | MainMenuMisc | | `scroll--green-decorated` | Core |
| `menu-button--large-text` | MainMenuMisc | | `menu-button`, `menu-button--medium` | Core |
| `mod-manager-box__list` | Modding | | `wide-menu-button` | Core |
| `load-box__list-title` | Options | | `content-centered`, `content-row-centered` | Core |
| `checkmark-green`, `cross-red` | Common | | `button-square`, `button-square--large`, `button-cross` | Common |
| `map-item__icon`, `map-selection__wide-button-label` | Common | | `game-text--red` | Common |

### 5.2 The **Host co-op game** button

- Postfix `NewGameModePanel.GetPanel` (like `LoadGameBoxGetPanelPatcher`, `Connect/ServerHostingUtils.cs:28-39`):
  `ButtonInserter.DuplicateOrGetButton(__result, "NextButton", "HostCoopButton", b => { b.text =
  T("BeaverBuddies.Saving.HostCoopGame"); b.clicked += … })`. It becomes a `LocalizableButton .menu-button
  .menu-button--large-text` (245×44, 17 px bold) after **Start** in `#NavigationButtons`. Reuse the existing key: it is
  translated in all 15 languages.
- Postfix `NewGameModePanel.UpdateNextButton`: `host.SetEnabled(__instance._nextButton.enabledSelf)`, so an invalid
  custom mode greys both, natively.
- On click: build the configuration (§6.3 step 1), then show the settlement-name box.
- **(verify in game)** that `menu-button--large-text` looks right on a `LocalizableButton` (it is only a font size) and
  that three buttons fit the banner (Back 184 + Start 245 + Host 245 px ≈ 690 of the 1920 px banner).

### 5.3 The settlement-name box (D8)

- `root = LoadVisualElement("Game/SettlementNameBox")` (it brings CoreStyle and GameMiscStyle). Tree (read):
  `#Wrapper .settlement-name-box > NineSliceVisualElement #SettlementNameBox .settlement-name-box__content
  .sliced-border .sliced-border--nontransparent .content-centered > [LocalizableLabel #Message
  (Saving.NameSettlement)] [NineSliceTextField #Input .text-field] [#Buttons .content-row-centered
  .settlement-name-box__buttons-root > [VisualElement > #RelocateButton, #ResetStartLocation] [LocalizableButton
  #ConfirmButton .menu-button (FlexibleStart.Start "Start!")]]`.
- Hide `RelocateButton` and `ResetStartLocation` (`ToggleDisplayStyle(false)`). Relabel `ConfirmButton` "Next" (use
  the game's own "Next" key from `NewGameTemplate`'s `NextButton` if it has one **(verify)**, else
  `BeaverBuddies.Lobby.Next`). Insert a **Cancel** `LocalizableButton .menu-button` (text `Core.Cancel` via
  `CommonLocKeys.CancelKey`) before it in `#Buttons`; don't initialise it (no loc key attribute): set `text` directly.
- `Input.maxLength = 50` (the game's limit). Focus it. Confirm on the button, on Enter (`OnUIConfirmed`), and on the
  game's focus-out + confirm pattern (`SettlementNameBoxShower.PromptDisallowingCancelling`).
- Show it with `PanelStack.PushOverlay(controller)` over the Game Mode page (as the game shows it). The controller is a
  small `IPanelController`: `OnUIConfirmed` → `CreateDirectoryForSettlement(text)`: `OK` → `Pop(this)` and open the
  waiting room; `NameTaken` / `NameInvalid` → `DialogBoxShower.Create().SetLocalizedMessage("Saving.TakenName" |
  "Saving.InvalidName").Show()`. Empty text → nothing. `OnUICancelled` → `Pop(this)`.
- Initial text: the last name used in this session, else empty (vanilla's `_initialSettlementName`).
- Note: `CreateDirectoryForSettlement` creates the folder. A cancelled waiting room leaves an empty folder, which the
  game treats as free (`NameTaken` needs a non-empty folder).

### 5.4 The waiting-room page (host) — see `pre-game-lobby/lobby-host.jpg`

```
page = LoadVisualElement("MainMenu/NewGameTemplate")                 // #root .grow-centered .new-game-panel (720 tall)
page.Q<Label>("HeaderText").text = T("BeaverBuddies.Lobby.Header.Host")        // "Co-op Game" in the capsule (18 px)
main = page.Q(className: "new-game__main-content")                   // padding 25, centred column
back = page.Q<Button>("BackButton");  back.text = T(CommonLocKeys.CancelKey)   // menu-button--medium
next = page.Q<Button>("NextButton");  next.text = T("BeaverBuddies.Host.StartGame")  // menu-button--large-text
```

Content of `main`, top to bottom (all built in code, initialised once, before rows are added):

1. **Summary row** (`VisualElement`, `flexDirection: Row`, `alignItems: Center`):
   - logo ring: `VisualElement .faction-item__logo-background .content-centered` (52 px, `bg-circle-big-1`) >
     `VisualElement .faction-item__logo` with `style.backgroundImage = new StyleBackground(faction.Logo.Asset)`
     (`FactionSpecService.GetFaction(id)`); the faction panel's own logo ring;
   - plate: `VisualElement .new-game__summary .content-centered` > `Label .new-game__summary-text`: an exact copy of
     the Game Mode page's `#Summary` ("Folktails - Diorama - Normal", the host's `SummaryText` text).
2. **Settlement name**: `Label .text--yellow` (#BCA26C), `fontSize 14`, centred, small top margin: "Beaverton".
3. **List title**: `Label .text--big .text--centered .load-box__list-title`: "Players (3)" (Load Game's list title).
4. **Player board**: `new ScrollView()` with `.scroll--green-decorated .mod-manager-box__list` (the Mods window's dark
   board with gold rim; its `-unity-slice` background draws on a plain ScrollView), `width 600`, `maxHeight 300`.
   Initialise it once, then add rows (§5.6). Rebuild rows by reusing row elements; never re-initialise the board.
5. **Status line**: `Label .unlock-condition__text` (yellow #FFFF00, 14 px, min-height 20: the faction page's own
   status line), centred, `marginTop 12`. Text from `LobbyRules.HostStatus` (§6.2). Keep it present (empty text rather
   than hidden) so the page doesn't jump.
6. **Invite Friends**: `NineSliceButton .wide-menu-button` (the map page's Download-maps button style) containing
   `Image .map-item__icon` (24 px, sprite `UI/Images/Game/ico-beavers`) and `Label .map-selection__wide-button-label`
   (`BeaverBuddies.Host.InviteFriends`, already translated). Click → `SteamListener.ShowInviteFriendsPanel()`.
   Disabled (`SetEnabled(false)`) until the Steam lobby exists (`LobbyID` valid); hidden when the server has no Steam
   listener.
7. **Direct-IP line**: `Label .text--grey` (14 px, #CCCCCC), centred: "Friends without Steam join by IP, port 25565."
   (`Settings.Port`).
8. **Navigation**: Back = **Cancel** (Esc too): confirm if any guest is in (§5.8), then end the waiting room and
   `Pop(this)` back to the Game Mode page. Next = **Start Game** (Enter too): always enabled until pressed (D2); then
   both buttons are disabled and the status reads "Starting…" until the loading screen covers the page.

Show the page with `_panelStack.HideAndPush(this)` right after the name box pops.

### 5.5 The guest's page — see `pre-game-lobby/lobby-guest.jpg`

The same builder, with these differences:
- Header: "Kyler's Game" (`BeaverBuddies.Lobby.Header.Guest`, `{0}` = the host's name).
- Summary: built by the guest from the host's ids (§6.1 `LobbyWelcome`), in the guest's language: faction display name
  from `FactionSpecService`, the map's name as the host sent it, the difficulty from its loc key (custom: the game's own
  custom-mode name **(verify the key)**). Logo from the guest's `FactionSpec`.
- No Invite Friends, no direct-IP line, no remove buttons.
- The guest's own row: tag followed by `Label .text--yellow` "(you)"; its checkbox is live (toggles ready).
- Back = **Leave** (confirm), Next = **I'm ready** / **Not ready** (Enter toggles). After the host's Start, Next is
  disabled; Leave stays.
- Status line: `LobbyRules.GuestStatus` (§6.2).

### 5.6 A player row

- `row = LoadVisualElement("Modding/ModItem")` (the Mods window's row; brings CommonStyle, CoreStyle, ModdingStyle;
  already initialised). Tree (read): `#ModItem .mod-item > #PriorityWrapper, Toggle #ModToggle .mod-item__toggle, Image
  #ModIcon .mod-item__icon, Label #ModName .mod-item__name, Label #ModVersion .text--yellow .mod-item__version,
  #WarningIcon .warning-icon`.
- Hide `PriorityWrapper` and `WarningIcon`.
- `ModToggle` = ready (`checkbox_on` / `checkbox_off`, 25 px, the Mods / Settings / Tutorial checkbox). Live only on the
  local guest's row; elsewhere read-only: `pickingMode = Ignore` on the toggle and its children, and
  `SetValueWithoutNotify(ready)`. The host's own row: ticked, read-only.
- `ModIcon.sprite` = the game's faction logo (24 px). (Mixed factions will use it per player later.)
- `ModName` = the player's name (white 17 px, ellipsis). A guest still joining: "Joining…" in `text--grey`.
- `ModVersion` (gold 14 px) = the tag: separate colonies: "Host · Colony 1", "Colony 2", "Colony 3", "Colony 4",
  "Helper"; shared game: "Host" on the host's row and nothing on guests' rows.
- Then a spacer (`flexGrow 1`), then the state: **Ready** = `Image .checkmark-green` (18 px) + `Label .text--default`
  "Ready"; **Not ready** = `Label .text--grey` "Not ready"; **Joining** = `Label .text--grey` "Joining…". (The game pairs
  `checkmark-green` with a label in its zipline tooltip.)
- Host's page, guest rows only: **remove** = `new Button()` with `.button-square .button-square--large .button-cross`
  (24 px framed cross with hover and pressed art), tooltip via `ITooltipRegistrar.Register(button,
  T("BeaverBuddies.Lobby.Remove.Tooltip"))`, click → confirm (§5.8). Other rows get a 28 px spacer so the states line up.
- Don't use `Options/ListViewItem` / `list-view__item-background`: those rows highlight on hover and read as
  selectable.

### 5.7 The guest's Connecting box (D21)

- A small `IPanelController` on `LoadVisualElement("Core/DialogBox")` (read: `#DialogBox .content-row-centered >
  NineSliceVisualElement .sliced-border .sliced-border--nontransparent > #Box .box > [#Content > Label #Message]
  [#Buttons > #CancelButton, #InfoButton, #ConfirmButton (menu-button menu-button--medium)]`). Set `Message` to
  "Connecting to Kyler…" (`BeaverBuddies.Lobby.Connecting`) or "Connecting to 1.2.3.4…"; show only `CancelButton`
  (`Core.Cancel`). `PushDialog` it.
- Cancel / Esc → close the client, `EventIO.ResetIf(client)`, `Pop(this)`. It is popped by the code that opens the
  waiting room or shows an error; a scene change (classic host's save arriving) removes it with the scene.
- The Steam path already waits for the overlay to close before showing its box
  (`SteamOverlayConnectionService.WaitForSteamOverlayToClose`): keep that.

### 5.8 Yes/no boxes and end-of-waiting-room boxes

All with `DialogBoxShower.Create().SetMessage(text).SetConfirmButton(action, text).SetDefaultCancelButton().Show()`
(the game's `Core/DialogBox`: frame, text, `menu-button--medium` buttons). They sit on top of the page: they are popped
by the builder before the page can pop. Texts in §11.1.

- Host: start with someone not ready; start alone; remove a guest; cancel with guests in.
- Guest: leave.
- Guest, after the host ends it (the page is popped first, then a plain OK box): cancelled; removed; the host could not
  create the world; no word for 120 s (**Keep waiting** / **Leave**).
- In a game (D20): the invite-in-game box.

### 5.9 The loading screen

Pass the mod's text as the tip: `ISceneLoader.LoadScene(GameSceneParameters.CreateNewGameParameters(cfg),
T("BeaverBuddies.Lobby.Tip.Creating"))`, then `LoadScene(CreateGameSaveParameters(save),
T("BeaverBuddies.Lobby.Tip.Loading"))`; the guest's `LoadMap` passes `T("BeaverBuddies.Lobby.Tip.GuestLoading", host)`
when it came through a waiting room (classic joins keep the game's random tip). The screen itself is the game's.

---

## 6. Architecture

### 6.1 The wire (TimberNet; D10, D23)

**Host → guest lobby frames**: `[int32 -1][int32 length][gzip(UTF-8 JSON)]`, written with both lengths and the body
under one `lock(stream)`. `-1` (big-endian `FF FF FF FF`) is a sentinel no other frame uses: 0 already means "error
follows", and a real save is never negative **(verify how `TryReadLength` treats a negative length today)**.

- The guest's reader accepts the sentinel **at any position**: before the save it dispatches the frame to the lobby
  inbox; after the save it reads and drops it (a keep-alive can race the save frame). Anything else keeps today's
  meaning (0 = error, first other frame = the save).
- A classic host never writes the sentinel, so classic hosting is byte-identical and every existing frame-order check
  stays as it is.

**Guest → host lobby frames**: ordinary `[length][gzip(JSON)]` with `type` `LobbyHello` / `LobbyReady`. The host reads
a waiting-room guest from right after the handshake; until that guest is admitted to the game (`StartQueuing`) it
accepts **only** these two types, caps a frame at 64 KB and decompresses with the capped
`CompressionUtils.Decompress(maxBytes)`. Any other type from an unadmitted connection is dropped (never queued as a
`player -1` event, never a `SessionFault`); an oversized one closes that connection only.

**Frames** (all display-only; validated like `StatusFrames` and `ChatMessage`; names cleaned with
`PlayerActivity.CleanName`; ids checked like `ColonySlotTable.CheckHello`: at most 64 characters, no `|` or line break):

| Frame | Direction | Fields |
|---|---|---|
| `LobbyWelcome` | H → G, once | `you` (the guest's player number), `hostName`, `summary {factionId, mapName, modeLocKey (null = custom), settlement, separateColonies}` |
| `LobbyRoster` | H → G, on every change and with each keep-alive | `players: [{n, name, ready, host, joining, colony (1-4, 0 = helper, null in a shared game)}]` in waiting-room order |
| `LobbyState` | H → G, every 1 s (keep-alive) and on change | `seq`, `state`: `open` \| `starting` \| `creatingWorld` \| `sendingWorld` |
| `LobbyEnd` | H → G, then close | `reason`: `cancelled` \| `removed` \| `failed`, `detail` (English, for `failed`) |
| `LobbyHello` | G → H, once, on welcome | `id` (`LocalPlayerIdentity.Id`), `name` (`PingDisplayName`) |
| `LobbyReady` | G → H | `ready` |

**Server changes** (`TimberServer`, lobby mode only):

1. `OpenLobby(LobbyRoom room)` before `Start()`. In lobby mode, after the handshake a connection is **admitted to the
   waiting room** instead of awaiting the provider at once: refuse if the room is closed to newcomers (error frame with
   the room's message) or full (D22); give the player number now (move that out of `StartQueuing` for lobby members;
   classic joins keep today's order); record the Steam-verified id; add a `LobbyMember`; start the guest's receive
   thread now, gated as above; send `LobbyWelcome` and push the roster to everyone. Then the join task awaits the
   provider as today.
2. The provider in lobby mode is a `TaskCompletionSource<byte[]>` created with
   **`TaskCreationOptions.RunContinuationsAsynchronously`** (without it, completing it on the game thread would run every
   guest's paced save send on the game thread). `ReleaseLobby(bytes)` completes it; `CancelLobby` cancels it.
3. `StartQueuing` lets a waiting-room member through even though the room is closed to newcomers; it keeps the number
   given at admission. `errorMessage` / `StopAcceptingClients` stay for the in-game close.
4. A lobby writer (one background thread or `System.Threading.Timer`; interval `LobbyIntervalMs = 1000`, tests use
   less): sends `LobbyState` (+ roster when changed) to every member not yet admitted to the game, checking a per-member
   "save started" flag **inside** `lock(stream)` so nothing is written into or after the save frame from the host side.
5. `RemoveFromLobby(n)` → `LobbyEnd removed` + close. `CancelLobby(reason)` → `LobbyEnd` to all + close all + cancel
   the provider. A member whose stream ends leaves the roster.
6. `Close()` and `AbortSession()` also close every waiting-room member (today they'd hang forever).
7. `SendErrorMessage(client, message)`: a per-connection message, both writes under `lock(stream)`.
8. Thread-safe `LobbyRoom.Snapshot()` (members in order, ready, joining, colony, state, version counter) for the menu
   page to poll each frame. Nothing depends on `Update()` being pumped in the menu.

**Client changes** (`TimberClient` / `TimberNetBase` guest reader):

1. The sentinel handling above, into a thread-safe `LobbyInbox` (`Welcome`, latest `Roster`, latest `State`, `End`,
   `LastFrameAtMs`, a version counter).
2. `SendLobbyHello(id, name)` / `SendLobbyReady(bool)`: gzip JSON frames under `lock(stream)`, allowed only before the
   save has arrived.

Put the frame types, validation and `LobbyRoom` / `LobbyInbox` in TimberNet (new files `LobbyFrames.cs`,
`LobbyRoom.cs`, `LobbyInbox.cs`): StabilityTests reference TimberNet, so all of it is tested headlessly (§10.1).

### 6.2 Pure rules (`BeaverBuddies/Lobby/LobbyRules.cs`, `System.*` only, linked into StabilityTests)

- `ColonyOf(indexInRoom, separateColonies)`: host 0 → colony 1; guests in order → 2, 3, 4; then helper; null in a shared
  game.
- `StartsToFill(playersField, guests)`: `min(playersField, 1 + guests, 4)` (D12).
- `StartConfirm(guests)`: `None` \| `Alone` \| `NotReady(names)` (D2).
- `HostStatus(guests, state)` and `GuestStatus(ready, state, hostName)`: which key and arguments (§11.1). The host's
  not-ready set includes guests still joining (they have no name yet). Exactly one not ready: `OneNotReady(name)`, or
  `OneJoining` if that one is still joining. More than one: `SomeNotReady(count)`. None: `AllReady`; no guests: `Empty`.
  Test every case.
- `WatchdogDue(lastFrameMs, nowMs, state)`: 120 s, never while `sendingWorld`.
- `SaveName(timestamp)`: `"<timestamp> Co-op start"` (the Rehost save's timestamp form, commas removed).

### 6.3 `LobbySession` (host; `BeaverBuddies/Lobby/LobbySession.cs`, static `Current`)

States: `Open` → `Starting` → `CreatingWorld` → `SendingWorld` → `Loading` → done (`Current = null`), or `Cancelled` /
`Failed`. Log every transition as `[Lobby] …`.

1. **Open** (from the name box's confirm): take the configuration from the Game Mode page: `TryGetValidatedGameMode`,
   `_factionSpec.Id`, `_map.MapFileReference`, `_map.DisplayName`, the mode's `DisplayNameLocKey` (null for custom),
   the `SummaryText` text (publicizer). Do **not** capture by calling `StartNewGame()` and blocking
   `GameSceneLoader.StartNewGame` (simpler to read, and one less global hook). `io = new ServerEventIO();
   io.StartLobby(summary)`: the same listeners as `Start(bytes)` (TCP port, Steam listener when enabled; calls
   `ColonySession.BeginHostSession()`), then `OpenLobby` and `Start`. Steam: `bb_open = "1"`, `bb_state = "lobby"`.
2. **Start** (after the confirm, D2): `CloseToNewcomers("The Host has already started this game from its waiting room,
   so it can no longer be joined. Ask the Host to save and rehost.")`; latch `io`'s `stoppedAccepting` **without**
   setting TimberServer's permanent error (members must still pass `StartQueuing`); Steam `CloseToNewGuests()` +
   `bb_state = "started"`; `ColonySession.JoiningClosedAtStart = true`; freeze the member order; broadcast
   `creatingWorld`. Build `cfg = new NewGameConfiguration(factionId, mapRef, mode', settlement)`. On a multiplayer map
   (the mod's `MultiplayerSettingsUpdater.IsMultiplayer`) the mode is always custom and already a
   `MultiplayerNewGameModeSpec` (the mod's `GetGameMode` postfix), so `mode'` is
   `new MultiplayerNewGameModeSpec(mode, LobbyRules.StartsToFill(mode.Players, guests))`; elsewhere `mode'` is the mode
   as picked. `HoldLoadingScreen = true`;
   `ISceneLoader.LoadScene(CreateNewGameParameters(cfg), tip)`.
3. **World saved** (from `LobbyWorldMaker`, §6.4): broadcast `sendingWorld`; `ReleaseLobby(bytes)`; each frame check
   that every frozen member is admitted to the game (in `clients`) or gone; after 10 s unscaled, close the rest. Then
   `EventIO.Set(io)`; `DeterminismService.InitGameStartState(bytes)`; `HoldLoadingScreen = false`; state `Loading`;
   `LoadScene(CreateGameSaveParameters(save), tip)`. Skip the save validators for this fresh save **(verify none shows a
   dialog for a save this computer just wrote; today's `LoadAndHost` confirm path skips them too)**. After the reload's
   first `PostLoad` in the co-op scene, `Current = null`.
4. **Cancel** (host's Cancel): `CancelLobby("cancelled")`, `io.Close()` (closes the Steam lobby); `Current = null`.
5. **Fail(reason)** (any exception from step 2 on, or no world saved within 120 s unscaled of step 2): `CancelLobby(
   "failed", reason)`; `io.Close()`; `HoldLoadingScreen = false`; `LoadingScreen.Disable()`; if the host is in the
   single-player new-world scene, show "Couldn't start the co-op game: {reason}. You can play this world on your own,
   or save it and use Host co-op game." It stays paused (the unpause was skipped).
6. **The main-menu configurator** (`Plugin.cs:79-119`) ends any `LobbySession.Current` left from before (a scene change
   the flow didn't expect) with `Fail("left the menu")`, before `EventIO.Reset()`.

### 6.4 Making the world (host, Game scene; `BeaverBuddies/Lobby/LobbyWorldMaker.cs`)

Bound in **every** Game scene (next to `ColonyConfigurator` in `ReplayConfigurator`, before the `EventIO.IsNull`
return). Does nothing unless `LobbySession.Current?.State == CreatingWorld` and the scene is a new game
(`GameSceneParameters.NewGame`).

1. **Hold the loading screen.** Harmony prefix on `SceneLoading: LoadingScreen.Disable`: return false while
   `LobbySession.HoldLoadingScreen`. (The hold is released by step 6.3.3 or `Fail`; every path out must release it.)
2. ~~**Don't unpause.**~~ Dropped in Phase 0 (§14, item 1): the save is written before the unpause, and the made
   world is discarded.
3. **`[OnEvent] NewGameInitializedEvent`**:
   - Pre-seed the slot table: `ColonySlotService.Instance.Table.Resolve(LocalPlayerIdentity.Id, LocalPlayerIdentity.
     Name)`, then for each frozen member in order `Resolve(member.StableId, member.Name)`, where the stable id is the
     Steam-verified id if any, else the `LobbyHello` id. A member who never said hello is skipped (they get the lowest
     free slot later). The table is saved only in a separate-colonies game, which is the only case it matters.
   - `GameSaver.QueueSaveSkippingNameValidation(new SaveReference(LobbyRules.SaveName(ts), settlementRef), onSaved)`
     (`AutosaveNameService.Timestamp()` as `RehostingService` does; `settlementRef` from `SettlementReferenceService`).
     On a `GameSaverException`, delete the half-written save and `Fail`.
4. `onSaved` → one frame later (the file handle; `TimeoutUtils.RunAfterFrames`) → `ServerHostingUtils.GetMapBtyes` →
   `LobbySession.OnWorldSaved(saveRef, bytes)`.
5. Factor the shared bits out of `RehostingService.SaveRehostFile` rather than copying them (it is bound only in co-op
   scenes).

### 6.5 The guest (`BeaverBuddies/Lobby/LobbyGuestPanel.cs`, `ConnectingBox.cs`; changes to `ClientConnectionService`, `SteamOverlayConnectionService`)

1. `TryToConnect` (both routes) shows the Connecting box (D21) instead of `ShowConnectionMessage(true)`; failures keep
   today's dialog (pop the box first).
2. `ClientConnectionService.UpdateSingleton` (already pumps the client) also reads the client's `LobbyInbox`:
   - first `Welcome` in the **main menu**: pop the Connecting box, `SendLobbyHello`, `HideAndPush` the guest page;
   - first `Welcome` in a **game scene** (D20): close the client, `EventIO.ResetIf`, pop the box, show the in-game box;
   - roster / state changes: update the page (by version counter);
   - `End`: pop the page, close, `EventIO.ResetIf`, show the reason box;
   - watchdog (`LobbyRules.WatchdogDue`): the Keep waiting / Leave box (once; re-armed on the next frame from the host).
3. The page's Ready → `SendLobbyReady`; Leave → confirm → close, `EventIO.ResetIf(client)`, `Pop`.
4. `LoadMap` is unchanged except the tip (§5.9). The page is removed with the main menu scene.
5. Steam: `OnLobbyEntered` keeps refusing `bb_open == "0"` with the existing `JoinCoopGame.Error.HostStarted` text
   (translated), which fits both a started waiting room and a started classic game.

### 6.6 The game after a waiting room (D16)

1. `ColonySession.JoiningClosedAtStart`: a `volatile static bool` (the init event is built on a network thread).
   `BeginHostSession` sets it false; `LobbySession.Start` sets it true; `AdoptHostChoice` takes a third argument.
2. `InitializeClientEvent.joiningClosedAtStart` (new field; `Create` fills it; `Replay` passes it to
   `AdoptHostChoice`). This is the only wire change outside the lobby phase.
3. `ColonyRules.WaitsForStart(bool foundingOrHandover, int hostTicksSinceLoad, bool joiningClosedAtStart) =>
   foundingOrHandover && hostTicksSinceLoad < 1 && !joiningClosedAtStart`. Update the doc comment (the reason is late
   joiners; a waiting-room game has none). Callers: `ColonyRulesService.cs:98`, `ColonyFoundingService.cs:115`,
   `TradeOverviewPanel.cs:425` pass the flag; **`ColonyHandover.cs:336` passes `false`** with a comment (D16).
4. Nothing else changes: `HostStartGate`, `ColonyRulesService:106-114` and the connection panel's "Joining: open" are
   already quiet because `stoppedAccepting` is latched before the host's `ReplayService` loads; `ReplayService`'s two
   `StopAcceptingClients` calls are idempotent.
5. The founding prompt: `ColonySlotService.Apply → OfferFounding` now shows at tick 0 for a guest without a colony.
6. Multi-start: the pre-seeded table puts each member in the slot of their start.
7. The desync, digest and rehost machinery is untouched. A **Save and Rehost** later is a classic rehost.
8. **Connection panel** (small, in every co-op game): a connected guest whose hello hasn't been played yet shows
   "(loading)" after their name, from `ColonySlotService` / the session's seats, through the pure `PanelModelBuilder` /
   `ConnectionStatusModel` (tested headlessly). This tells the host when everyone is in before unpausing.

### 6.7 New and changed files

New:
- `TimberNet/LobbyFrames.cs`, `TimberNet/LobbyRoom.cs`, `TimberNet/LobbyInbox.cs`
- `BeaverBuddies/Lobby/LobbyRules.cs` (pure), `LobbySession.cs`, `LobbyWorldMaker.cs`, `LobbyPatches.cs` (the New Game
  button, `UpdateNextButton`, `LoadingScreen.Disable`, `GameInitializer.UnpauseGame`), `SettlementNamePanel.cs`,
  `LobbyPage.cs` (the shared page and row builder, §5), `LobbyHostPanel.cs`, `LobbyGuestPanel.cs`, `ConnectingBox.cs`
- `StabilityTests/LobbyChecks.cs`, `RuntimeChecks/LobbyRuntimeChecks.cs`

Changed:
- `TimberNet/TimberServer.cs`, `TimberNetBase.cs`, `TimberClient.cs`
- `BeaverBuddies/IO/ServerEventIO.cs` (`StartLobby`, the latch without the permanent error, provider)
- `BeaverBuddies/Steam/SteamListener.cs` (`bb_state`), `SteamOverlayConnectionService.cs` (Connecting box)
- `BeaverBuddies/Connect/ClientConnectionService.cs` (Connecting box, inbox, D20, tip)
- `BeaverBuddies/Connect/RehostingService.cs` (factor the save helper out)
- `BeaverBuddies/Colonies/ColonySession.cs`, `ColonyRules.cs`, `ColonyRulesService.cs`, `ColonyFoundingService.cs`,
  `ColonyHandover.cs`, `TradeOverviewPanel.cs`
- `BeaverBuddies/Events/ConnectionEvents.cs` (the init field)
- `BeaverBuddies/Panel/*` (the "(loading)" marker)
- `BeaverBuddies/Plugin.cs` (MainMenu: `LobbyHostPanel`, `LobbyGuestPanel`, `SettlementNamePanel` factory; Game:
  `LobbyWorldMaker`; the stale-session check)
- `BeaverBuddies/Localizations/enUS_BeaverBuddie.csv`, `StabilityTests/StabilityTests.csproj` (link `LobbyRules.cs`),
  `StabilityTests/Program.cs`, `RuntimeChecks/Program.cs`

---

## 7. Phases

Work on a branch from `origin/main`. Commit at the end of each phase. Every phase ends with both builds (**Release**
and **Release Steam**, into a scratch mods folder with `-p:BeaverBuddiesModsPath=<scratch>/` so the installed mod isn't
overwritten), 0 warnings, and both suites green (`dotnet run --project StabilityTests`; RuntimeChecks with the compiled
DLL, `<Managed>`, and the two workshop paths — see the previous release notes for the exact command). Other sessions
release in parallel (the mixed factions work may be in flight): `git fetch` before each phase and rebase if `main`
moved.

### Phase 0: Verify the unknowns (no product code)

Settle each and record the answers in §14:

1. `GameStartup: GameInitializer.UnpauseGame`: its body, so the prefix skips only `ChangeSpeed(1f)` and keeps the state
   change. Does anything else unpause a new game (`SpeedManager` defaults, a mod patch)?
2. `SceneLoading: LoadingScreen.Disable` callers (only `SceneLoader`?), and that skipping it leaves the screen and the
   mute consistent until the next load's `Disable`.
3. Every mod patch that checks `EventIO` and could run in the one frame between `EventIO.Set(io)` and the scene change
   inside a single-player scene (grep `EventIO.IsNull`, `EventIO.Get()`, `EventIO.IsHost` in `Harmony` patches). The
   in-game Load → Host co-op game path is the precedent; confirm it takes the same frame.
4. `TimberNetBase.TryReadLength` with `-1`, and whether any reader treats a negative length as an error today.
5. The receive loop for a waiting-room member switching from "lobby frames only" to normal after `StartQueuing` /
   `FinishQueuing` on the same thread: what the host's dispatch needs (activity channel, tracker) for the first normal
   frame, and that the guest sends none before its game loads.
6. `NewGameModePanel` members used (`_nextButton`, `_summary`, `_factionSpec`, `_map`, `_predefinedGameMode`,
   `TryGetValidatedGameMode`, `_root`) and `MapItem.DisplayName`; the custom mode's display name key.
7. The game's loc key for "Next" (`NewGameTemplate`'s `NextButton`), and `Core.Cancel` / `CommonLocKeys.CancelKey`.
8. Missing-key behaviour of the game's `ILoc` for a key only in enUS (the repo says English shows; confirm, since the
   waiting room adds about 40 keys).
9. The New Game page's tutorial checkbox (`TutorialToggleController`): what it changes, whether a tutorial runs in a
   hosted co-op game, and whether the waiting room should force it off. Decide and write it down; if it matters,
   force it off for waiting-room games and say so on the page's status line.
10. That `GameSaveRepository`, `FactionSpecService`, `ISceneLoader`, `ITooltipRegistrar` and `VisualElementLoader` are
    bound in MainMenu, and `LoadingScreen`, `GameSaver`, `AutosaveNameService`, `SettlementReferenceService`,
    `ISceneLoader` in Game (RuntimeChecks' BindingChecks will confirm after the build).
11. The template names in §5 exist in UI.zip, and `NewGameTemplate`'s content element is found by
    `Q(className: "new-game__main-content")`.
12. Whether `NewGame.Notification` (the "new settlement" notification) is saved, so the reloaded host still has it
    (cosmetic; note it in §12 if not).

### Phase 1: TimberNet lobby phase

`LobbyFrames`, `LobbyRoom`, `LobbyInbox`; the server and client changes of §6.1; `StabilityTests/LobbyChecks.cs`
(§10.1 items 1–12). No mod code yet. Classic checks untouched and green.

### Phase 2: Host session

`LobbyRules`; `ServerEventIO.StartLobby` and the latch; `SteamListener` `bb_state`; `LobbySession` states Open,
Starting, Cancelled, Failed (Creating/Sending/Loading are wired in Phase 4); the main-menu stale-session check. Checks
for `LobbyRules`.

### Phase 3: Host UI

The button (§5.2), the name box (§5.3), the host page and rows (§5.4, §5.6), the yes/no boxes (§5.8). The page polls
`LobbyRoom.Snapshot()` each frame (`IUpdatableSingleton`), rebuilding rows only when the version changes.

### Phase 4: Making the world and the hand-off

`LobbyWorldMaker`, the two patches, the pre-seed, the save, `OnWorldSaved`, the wait for queued joins, the reload,
`Fail` and its failsafe timer, the loading-screen tips. Factor the save helper out of `RehostingService`.

### Phase 5: Guest

The Connecting box (§5.7), the inbox polling, the guest page (§5.5), Ready and Leave, the end boxes, the watchdog,
the in-game refusal (D20), the loading tip.

### Phase 6: In the game

`JoiningClosedAtStart`, the init field, `WaitsForStart` and its four callers, the "(loading)" marker (§6.6).

### Phase 7: Checks

§10. Add them as you go; this phase is for the ones that span phases and for the counts.

### Phase 8: Documentation

§11.

### Phase 9: Release

Follow the maintainer's release loop for the next free `1.4.0-betaN` (at this writing `main` is beta16, so beta17
unless another release lands first; re-check before cutting):

- Bump `BeaverBuddies/BeaverBuddies.csproj` `<Version>`, `BeaverBuddies/manifest.json`, `BeaverBuddies/changelog.txt`
  (top entry), the `STABILITY-CHANGELOG.md` entry, and the version strings on the site (`docs/index.html`,
  `install.html`, `troubleshooting.html`) and its check counts.
- Build both configurations into a scratch mods folder; zip in the previous release's entry order (new files
  appended), reading long paths through `\\?\`; `<ver>-SHA256SUMS.txt`.
- Commit; `git fetch`; confirm `origin/trading-exchange` is an ancestor of `HEAD`; push `HEAD` to `trading-exchange`,
  `main` and the branch; annotated tag `v1.4.0-betaN` ("Timber Together 1.4.0-betaN").
- `gh release create --repo timbermods/TimberTogether --prerelease --verify-tag` with notes derived from the
  previous release's (`gh release view <prev> --json body`). Always pass `--repo` (gh's default here is the Stability
  Fork).
- Afterwards: the asset hash matches (`gh release download`), the CI "Tests" run is green on the tag, Pages built.
- The notes and changelog say plainly: **not seen in a game**; Script D is this release's.

---

## 8. Wire and save changes

- **Wire:** the lobby phase (new frames in lobby mode only; classic hosting byte-identical);
  `InitializeClientEvent.joiningClosedAtStart`. The handshake already requires the same build, so no negotiation.
- **Save:** none new. A waiting-room game's save is an ordinary separate-colonies (or shared) save whose slot table was
  filled before its first save. It loads and rehosts like any other.
- **Steam lobby data:** new key `bb_state` (`lobby` / `started`); `bb_open` unchanged.

---

## 9. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Completing the provider on the game thread runs every guest's paced save send inline and freezes the host | `RunContinuationsAsynchronously`; LobbyChecks item 2 |
| R2 | Parked guests leak (no list closes them) and hang on "Receiving…" forever | Members in `LobbyRoom`; `Close`/`AbortSession`/`CancelLobby` close them; item 4 |
| R3 | Reading a guest before admission widens what an unauthenticated connection can do (events as `player -1`, `SessionFault`, unbounded frames) | Lobby-only gate, 64 KB cap, capped decompression, drop the rest; item 3 |
| R4 | A lobby frame lands after the save and is misread | Guest drops the sentinel after the save; host checks "save started" inside `lock(stream)`; item 6 |
| R5 | `StopAcceptingClients`' permanent error refuses the members at `StartQueuing` | Separate newcomer gate; the latch without the error; item 5 |
| R6 | The loading-screen hold is never released and the host is stuck behind it | Every exit path releases it; `Fail` failsafe after 120 s; the main-menu configurator releases it |
| R7 | The skipped unpause breaks `GameInitializer`'s state machine (UI never shows in a failed start) | Phase 0.1; keep the state change; on `Fail` the host is in a paused, working solo game |
| R8 | `EventIO` set for one frame inside a single-player scene trips a patch | Phase 0.3; the in-game Load → Host precedent |
| R9 | A member's hello arrives with a different id than pre-seeded (TCP guest changed identity) | They take the lowest free slot as today; logged |
| R10 | Main-menu stylesheet scope differs from what UI.zip suggests; a class draws nothing | RuntimeChecks class→sheet list (§10.2); test script looks at every piece |
| R11 | `PanelStack.Pop` throws when a yes/no box is still on top of a page being closed | Always pop the box first; one owner closes the page |
| R12 | Steam lobby freezes (main thread busy while the world is made) drop a guest | Steam's own keepalive (20 s connected timeout) covers today's equivalent waits; the keep-alive runs off the game thread; test script times a large map |
| R13 | A founding at tick 0 has never been played in co-op | Script D line; the digest is compared at tick 1, so a desync would show at once |
| R14 | Guests queued late miss a host action at tick 0 | The host waits for every member to be queued before loading the save (their `StartQueuing` runs before the save is sent) |
| R15 | The Players field rule (D12) surprises a host who set 4 starts for 2 players | Documented in the README, TWO-COLONIES and the notes; the rule exists so no start colony is made that nobody plays |

---

## 10. Checks

### 10.1 StabilityTests (new `StabilityTests/LobbyChecks.cs`; link `BeaverBuddies/Lobby/LobbyRules.cs` in the csproj; add to `Program.cs:64`)

Built on the in-memory `PipeStream` / `PipeListener` (`Preview5Checks.cs:108-144`) and the fake Steam rig
(`SteamLinkChecks`). Each test must finish in 5 s: use short lobby intervals.

1. A waiting-room guest gets `LobbyWelcome` and the roster before any save; its ready toggle shows in the host's
   snapshot; after `ReleaseLobby`, the save, `SetState` and the init event follow, in that order.
2. `ReleaseLobby` returns at once, and the paced save is not sent on the releasing thread.
3. From an unadmitted connection: a non-lobby frame never reaches `ReadEvents`; a `SessionFault` does nothing; an
   oversized lobby frame closes only that connection.
4. `CancelLobby`: every member gets `LobbyEnd cancelled` and is closed; `host.Close()` closes parked members.
5. After `CloseToNewcomers`, a new connection is refused after its handshake with the room's message, while a member is
   still admitted at `StartQueuing`.
6. A lobby frame written after the save is dropped by the guest; nothing is misread.
7. Classic mode: no sentinel on the wire; the existing frame-order checks run unchanged.
8. Player numbers follow waiting-room order; a member who leaves drops off the roster; colonies are re-predicted.
9. Keep-alive: the guest inbox's last-frame age stays small while the provider is pending, and grows when the writer
   stops.
10. The same session over the fake Steam network.
11. `RemoveFromLobby`: `LobbyEnd removed`, closed, roster updated. An eighth guest is refused ("full").
12. `LobbyHello` validation: a bad id is refused (member stays "joining"), a name is cleaned.
13. `LobbyRules`: `ColonyOf`, `StartsToFill`, `StartConfirm`, `HostStatus`/`GuestStatus` choices, `WatchdogDue`,
    `SaveName`.
14. `WaitsForStart` (extend `ColonyChecks.cs:617-625`): with the flag, false at tick 0 for founding / hand-over /
    act-as; without it, today's answers.
15. `HostStartRules` (extend `FeatureChecks.cs:164-172`): a session that began with joining closed never holds.
16. Slot table: pre-seeding host + members in order gives 0, 1, 2, 3 and a helper; it survives `Encode`/`Decode`;
    `SeatHello` with the reserved id gets the reserved slot over Steam (verified) and TCP (claimed).
17. Connection panel model: an unseated guest shows "(loading)".
18. Strings: every `BeaverBuddies.Lobby.*` key the code asks for exists in enUS; no digits in keys (the
    `PanelModelChecks.cs:53-72` pattern).
19. Source scan: nothing under `BeaverBuddies/Lobby/` uses `NativeElements` or an in-game-only class
    (`entity-panel__text`, `entity-sub-panel`, `game-scroll-view`, `entity-panel__toggle`, `progress-bar--green`).

### 10.2 RuntimeChecks (new `RuntimeChecks/LobbyRuntimeChecks.cs`, registered in `Program.cs:96-120`)

1. Every hooked or reflected game member exists with the expected signature: `NewGameModePanel.GetPanel`,
   `UpdateNextButton`, `TryGetValidatedGameMode`, fields `_nextButton`, `_summary`, `_factionSpec`, `_map`, `_root`;
   `GameInitializer.UnpauseGame`; `LoadingScreen.Enable(string)` / `Disable()`; `NewGameInitializedEvent`;
   `GameSaver.QueueSaveSkippingNameValidation`; `GameSceneParameters.CreateNewGameParameters` /
   `CreateGameSaveParameters`; the four-argument `NewGameConfiguration` constructor;
   `GameSaveRepository.CreateDirectoryForSettlement`; `ISceneLoader.LoadScene(ISceneParameters, string)`;
   `FactionSpecService.GetFaction`.
2. **UI.zip** (`<Managed>/../StreamingAssets/Modding/UI.zip`): the templates and names the lobby queries exist:
   `MainMenu/NewGameTemplate` (`HeaderText`, `BackButton`, `NextButton`, class `new-game__main-content`),
   `Modding/ModItem` (`PriorityWrapper`, `ModToggle`, `ModIcon`, `ModName`, `ModVersion`, `WarningIcon`),
   `Game/SettlementNameBox` (`Input`, `ConfirmButton`, `RelocateButton`, `ResetStartLocation`, `Buttons`),
   `Core/DialogBox` (`Message`, `CancelButton`, `InfoButton`, `ConfirmButton`).
3. UI.zip: every class in §5.1's table is defined in one of the seven sheets `MainMenu/TitleScreen.uxml` attaches (parse
   the `<Style src>` list and the `.uss` selectors, so the check follows the game's files, not this plan).
4. `InitializeClientEvent.joiningClosedAtStart` survives the event JSON.
5. IL: `ColonyRulesService.AllowOnHost` passes the flag into `WaitsForStart` (reads `ColonySession.
   JoiningClosedAtStart`); `ColonyLifecycle.HostMayHandOver` passes `false`.
6. No new `ReplayEvent` type (the existing `ChangesGame` review list is unchanged).
7. BindingChecks covers the new MainMenu and Game bindings by itself.

### 10.3 Test script (add to `ALPHA-TEST-SCRIPTS.md` as **Script D: the waiting room** *(betaN)*; two computers)

1. Host: New Game → Folktails → a standard map → Normal. **Host co-op game** sits beside Start and greys with it
   (Custom with an invalid value). The settlement box looks like the game's own (Cancel returns; a taken name shows the
   game's message).
2. The Co-op Game page matches `design/pre-game-lobby/lobby-host.jpg`: banner, capsule, summary plate with the logo
   ring, Players board, status line, Invite Friends (enabled after a moment), IP line, Cancel / Start Game. Screenshot
   it at 1920×1080 and at the smallest window you use.
3. Guest: accept the invite from the main menu → Connecting box → the Kyler's Game page (compare with
   `lobby-guest.jpg`). Toggle ready from the button and from the row checkbox; the host's row updates within a second.
4. Host: remove the guest (confirm); the guest sees "removed". Guest rejoins by invite. Guest: Leave; rejoin by IP.
5. Host: Start with the guest not ready → the confirm; Keep waiting; guest ready; Start. Both see the loading screen
   with the mod's text; the host never sees the new world before it.
6. In game: both paused at tick 0; the host has the district center; no "Joining: open", no "Start the game?". The
   guest is offered **Place your district center** at once; place it while paused. Unpause; play 5 minutes; no desync.
7. Multi-start map, Players 4, two players: two starts filled, the guest in start 2.
8. A third account tries the old invite after Start: refused with the "already started" message.
9. Host Cancel with a guest in: the guest sees "closed". Host backs out of the name box and the Game Mode page: nothing
   left open (the next **Load Game → Host co-op game** works as before).
10. Classic: **Load Game → Host co-op game** with the same guest: works as before, the guest sees the Connecting box
    instead of "Joined! Receiving map...".
11. Accept a waiting-room invite while playing a solo game: the D20 box; nothing breaks.
12. Leave the guest on the page for 3 minutes before Start; nothing drops. Pull the host's network during the page:
    the guest gets the Keep waiting / Leave box after 2 minutes.

---

## 11. Documentation (every new player-facing string is English only)

### 11.1 Strings (`BeaverBuddies/Localizations/enUS_BeaverBuddie.csv`; keys `BeaverBuddies.Lobby.*`, no digits)

Reused (already translated): `BeaverBuddies.Saving.HostCoopGame` "Host co-op game", `BeaverBuddies.Host.StartGame`
"Start Game", `BeaverBuddies.Host.InviteFriends` "Invite Friends", `BeaverBuddies.JoinCoopGame.Error.HostStarted`, the
game's `Saving.NameSettlement`, `Saving.TakenName`, `Saving.InvalidName`, `Core.Cancel`.

New (short, second person, British spelling; `{0}` placeholders):

| Key | English |
|---|---|
| `Lobby.Next` (only if the game has no "Next" key) | Next |
| `Lobby.Header.Host` | Co-op Game |
| `Lobby.Header.Guest` | {0}'s Game |
| `Lobby.Players` | Players ({0}) |
| `Lobby.Tag.Host` / `Lobby.Tag.HostColony` / `Lobby.Tag.Colony` / `Lobby.Tag.Helper` | Host / Host · Colony {0} / Colony {0} / Helper |
| `Lobby.You` | (you) |
| `Lobby.Ready` / `Lobby.NotReady` / `Lobby.Joining` | Ready / Not ready / Joining… |
| `Lobby.Button.Ready` / `Lobby.Button.Unready` / `Lobby.Button.Leave` | I'm ready / Not ready / Leave |
| `Lobby.DirectIp` | Friends without Steam join by IP, port {0}. |
| `Lobby.Remove.Tooltip` | Remove from the waiting room |
| `Lobby.Status.Empty` | Invite friends, then start the game. |
| `Lobby.Status.OneNotReady` | {0} is not ready yet. You can still start. |
| `Lobby.Status.OneJoining` | A player is still joining. You can still start. |
| `Lobby.Status.SomeNotReady` | {0} players are not ready yet. You can still start. |
| `Lobby.Status.AllReady` | Everyone is ready. |
| `Lobby.Status.Starting` | Starting… |
| `Lobby.Status.GuestNotReady` | Press I'm ready when you are. {0} starts the game. |
| `Lobby.Status.GuestReady` | You're ready. Waiting for {0} to start the game… |
| `Lobby.Status.CreatingWorld` | {0} is creating the world… |
| `Lobby.Status.SendingWorld` | Receiving the world… |
| `Lobby.Confirm.Alone` | Nobody has joined yet. Start the game on your own? Nobody can join once it has started, until you save and rehost. |
| `Lobby.Confirm.NotReady` | Not everyone is ready ({0}). Start anyway? Everyone in the waiting room comes into the game, ready or not. |
| `Lobby.Confirm.Remove` | Remove {0} from the waiting room? |
| `Lobby.Confirm.Cancel` | Close the waiting room? Everyone in it goes back to the main menu. |
| `Lobby.Confirm.Leave` | Leave {0}'s waiting room? |
| `Lobby.Confirm.Start` / `Lobby.Confirm.KeepWaiting` | Start anyway / Keep waiting |
| `Lobby.End.Cancelled` | {0} closed the waiting room. |
| `Lobby.End.Removed` | {0} removed you from the waiting room. |
| `Lobby.End.Failed` | {0} couldn't start the game: {1} |
| `Lobby.Watchdog` | {0}'s game hasn't answered for two minutes. |
| `Lobby.InGameInvite` | {0} is waiting for you in a co-op waiting room. Return to the main menu, then accept the invite again (or join from there). |
| `Lobby.Connecting` | Connecting to {0}… |
| `Lobby.Failed` (host) | Couldn't start the co-op game: {0}. You can play this world on your own, or save it and use Host co-op game. |
| `Lobby.Tip.Creating` / `Lobby.Tip.Loading` / `Lobby.Tip.GuestLoading` | Creating the world for your co-op game… / Loading your co-op game… / Loading {0}'s co-op game… |
| `Lobby.Loading` (panel marker) | (loading) |

Host-generated refusals stay hard-coded English, like the existing ones in `ServerEventIO.StopAcceptingClients`:
"The Host has already started this game from its waiting room, so it can no longer be joined. Ask the Host to save and
rehost." and "The waiting room is full."

### 11.2 Documents

- **`README.md`** "Start a game" (≈50-92): a new first path, **New game with a waiting room** (Game Mode page → Host
  co-op game → name → invite → ready → Start Game; place your district center whenever you like); step 3 (Load Game →
  Host co-op game) becomes the path for saves; step 4's "wait, paused" and "Start the game / Keep waiting" apply to saves
  only; step 5 notes founding at tick 0 in a waiting-room game. Troubleshooting (≈305-311): "Ctrl+K says the game has
  not started yet" doesn't happen after a waiting room; a guest whose game failed to load needs Save and Rehost.
- **`TWO-COLONIES.md`** "Starting" (≈30-72): the waiting room; "Joining closes" (≈51-56) at Start for a waiting-room
  game; founding step 1 (≈61-64): at once in a waiting-room game; the multi-start fill rule (D12); stewards (≈199-200)
  and hand-over by the host (≈220) wording; How it works (≈412-423): the waiting room's seats and the lobby phase.
- **`STEAM-INVITES.md`**: using it (≈7-20) with the waiting room; Lobby/Admission (≈48-52): `bb_state`; the two-account
  playtest (≈111-131); known limits (≈133-140).
- **`CONNECTION-PANEL.md`**: the Joining row (≈47): not shown after a waiting room; "Who can join" (≈189-194); the
  "(loading)" marker.
- **`ALPHA-TEST-SCRIPTS.md`**: Script D (§10.3); Script B line 2 (≈156-160) says it describes Load Game hosting.
- **`STABILITY-CHANGELOG.md`**: a feature entry in the beta16 style: a bold summary (asked for by the maintainer to clean
  up getting player 2 in), then bold-led bullets naming the code; **Wire change.** (the lobby phase; the init field);
  `- Docs: …`; `- Checks: StabilityTests N (k new: …); RuntimeChecks M (j new: …). Both builds, 0 warnings.`;
  `- Not seen in a game. Script D is this release's.`
- **`BeaverBuddies/changelog.txt`**: two plain `*` lines, then the "Full details…" line.
- **`WORKSHOP.md:43`** and **`BeaverBuddies/Doc/WorkshopDescription.txt:11`**: "founds theirs once the host unpauses"
  → also "or straight away after a waiting room".
- **Site** (`docs/`, Pages from `main:/docs`): `index.html` (≈186-199 the hosting steps, ≈381 "Join before the host
  starts", the `#new` section, a `#features` card "Invite and ready up before the game starts", the check counts);
  `install.html` (≈119-150 `#host` and `#join`); `faq.html` (≈143, 282); `troubleshooting.html` (≈96-171 `#joining`).
- **`design/`**: leave the older plans as they are (the maintainer keeps them as history).

---

## 12. Known limits (documented, not fixed)

- New games only (D1). A save is hosted as before, with today's wait at tick 0.
- Nobody can join after **Start Game**, and a guest whose game fails to load can't come back without **Save and
  Rehost** (D3).
- No chat, map preview or mod-list comparison in the waiting room (D18). Mod mismatch warnings still appear in the game.
- The settlement-name box has no **Change start location** (the game offers it after a completed wonder); relocate
  later in the game if the game allows it.
- A guest must be in the main menu to enter a waiting room (D20).
- At most 7 guests; 4 colonies; the rest are helpers of colony 1 (D22, D11).
- On a multi-start map the starts filled are the players present at Start, at most the Players field (D12).
- Direct IP: anyone who can reach the port can enter the waiting room (as with hosting today); the host can remove
  them.
- The host's colony settings are fixed when the waiting room opens (the page is modal).
- Whatever Phase 0 finds for the tutorial checkbox and the new-settlement notification.

---

## 13. Working rules for the implementing session

- Read §2 and §5 again at the start of each phase. D1–D6 are the maintainer's: don't change them. If one proves
  impossible, stop and report.
- **Classic flows are unchanged.** Every new code path returns immediately unless a `LobbySession` is active (host) or
  the client got a `LobbyWelcome` (guest), except D21 (the Connecting box) and D16's flag, which is false outside
  waiting-room games. Any other behaviour change for Load Game hosting, rehosting or joining a classic host is a bug.
- **Determinism.** The waiting room touches no simulation code. The only game-state writes are the slot table
  pre-seed (before the first save, so it is in the bytes everyone loads) and the session flag (a rule change only in
  waiting-room games, told to guests in the init event).
- **Native UI (D6).** Only the game's templates, the classes in §5.1's table and the sprites named in §5; no
  `NativeElements`, no inline colours, no hand-drawn shapes. When unsure how the game draws something, open the
  game's UXML/USS in UI.zip and copy its structure.
- Match the surrounding code: pure rules in `*Rules` files with `System.*` only; `RegisteredSingleton` services;
  `XxxPatcher` classes with a dated `[ManualMethodOverwrite]` excerpt of the game code a patch copies (see
  `ServerHostingUtils.cs`); comments that say why, not what.
- Strings: enUS only; keys `BeaverBuddies.Lobby.*`; no digits in keys.
- Log lines are prefixed `[Lobby]`, one per decision or state change, never per frame.
- Run both suites and both builds at the end of every phase; record the counts for the changelog.
- Be honest in every document: this is **not seen in a game** until someone plays Script D.

## 14. Phase 0 findings

Settled 2026-09-22 against 1.1.2.4 and the repo at `07a39a5` (beta16 + this plan). Baseline: both builds 0 warnings,
StabilityTests 355/355, RuntimeChecks 330/330.

1. **`GameInitializer.UnpauseGame`** is `_speedManager.ChangeSpeed(1f); return InitializationState.ShowUI;`.
   **No patch is needed, and §6.4 step 2 is dropped.** The made world is thrown away once saved: the save is queued in
   the `NewGameInitializedEvent` handler and written in that frame's `LateUpdate`, before `UnpauseGame` runs on the
   next frame, so it holds tick 0 either way. On a failure the host keeps an ordinary solo game, unpaused as vanilla
   leaves it.
2. **`LoadingScreen.Disable`** is called only by `SceneLoader.LoadSceneCoroutine`. `SoundSettingsSystem: Muter` mutes on
   `LoadingScreenEnabled` and unmutes on `LoadingScreenDisabled`, so holding `Disable` keeps the sound muted until the
   reload's own `Disable`, which is what we want. Prefix returning false while held.
3. **`EventIO` set inside a single-player scene.** The tick patch (`TickableBucketServiceTickUpdatePatcher`) falls
   back to vanilla when `TickingService` is null. The in-game Options → Load → **Host co-op game** already sets
   `EventIO` in a single-player scene and then waits in a dialog, so this is exercised today. `LoadSceneCoroutine` sets
   `Time.timeScale = 0` synchronously before it yields, so the one frame before the scene change runs no ticks.
4. **A length of -1** reads back as -1 (`BitConverter.ToInt32`). Today the guest would pass it to
   `ReceiveFile`/`ReadUntilComplete` and throw. The sentinel has to be handled at the top of the read loop. Classic
   hosting never writes it.
5. **The host's receive loop.** `StampReceivedEvent`, `HandleActivity` and `HandleChat` trust `playerIds`. A
   waiting-room member gets its number at admission, so the gate must drop every non-lobby frame until that member is
   admitted to the game (`StartQueuing`), or a frame sent early would be stamped with a real number. The guest sends
   nothing but lobby frames before its save arrives (its activity channel only exists from then on).
6. **`NewGameModePanel`**: `_map` (a `MapItem`: `DisplayName`, `MapFileReference`), `_factionSpec` (`Id`,
   `DisplayName.Value`, `Logo`), `_predefinedGameMode` (null for a custom mode; `GameModeSpec.DisplayNameLocKey`),
   `_summary` (`SummaryText`), `_nextButton`, `_root`, and private `TryGetValidatedGameMode`. The summary is
   `faction + " - " + map + " - " + (selected mode button text or T("NewGameConfigurationPanel.Custom"))`.
7. **Keys:** "Next" is `CommonLocKeys.NavigationNextKey` (`Core.NavigationNext`); Cancel is `Core.Cancel`; a custom
   mode's name is `NewGameConfigurationPanel.Custom`. `Lobby.Next` is not needed.
8. **Missing translations.** `LocalizationLoader.GetLocalization` puts every enUS record into a language that lacks
   it, with a log warning, so the English text shows. English-only keys are fine.
9. **The tutorial checkbox** writes the player's `TutorialSettings`, which the new world reads when it is made. This is
   exactly what happens today when a new game is made alone and then hosted, so there is no new risk. Not forced off.
10. **APIs:** `GameSaveRepository.CreateDirectoryForSettlement(string)` is public; `FactionSpecService.GetFaction(id)`;
    `VisualElementLoader.LoadVisualTreeAsset(name)` is public. BindingChecks confirms the bindings after the build.
11. **Templates.** All the §5 templates and names exist in UI.zip. **Change from §5.4:**
    `LoadVisualElement("MainMenu/NewGameTemplate")` would throw. Its `HeaderText` is a `LocalizableLabel` with no
    `text-loc-key` (the game's pages set one with `AttributeOverrides`), and `VisualElementLocalizer` throws for an
    unset key. So the page is built with `LoadVisualTreeAsset(...).CloneTree().ElementAt(0)`, the label's
    `_textLocKey` is set (publicized), and only then does `VisualElementInitializer.InitializeVisualElement` run. The
    content element (class `new-game__main-content`) sits inside MainContent's `Center`. `Modding/ModItem`,
    `Game/SettlementNameBox` and `Core/DialogBox` have no unset keys.
12. **The new-settlement notification** goes into the game's notification journal before the save, and the save holds
    the journal, so the reloaded host still has it.

**Built as 1.4.0-beta18 (2026-09-22)**, where it departs from this plan:

- No `bb_state` Steam key (§8): `bb_open = "0"` at Start already gives an old invite the existing "already started"
  message.
- `ColonyOf` lives in TimberNet (`LobbyRoom.ColonyOf`), since the roster frames need it; `LobbyRules` keeps the rest.
- Every waiting-room frame goes out through a `SendLane` of the guest's own (found in the review before release:
  writing under the pump's lock let one stalled direct-IP guest hold up the host's game thread).
- The guest's page and the host's page share one builder (`LobbyPage`); the settlement box's Next uses the game's
  `Core.NavigationNext`.
- Checks: StabilityTests 372 (17 new), RuntimeChecks 338 (7 new). Not played.
