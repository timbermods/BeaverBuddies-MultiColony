# In-game hosting and joining (plan for 1.4.0-rc7)

Status: **built in 1.4.0-rc7 without the game** (2026-09-24: StabilityTests pass; the mod not yet compiled against
Timberborn's assemblies, RuntimeChecks not yet run, nothing played; the deviations from this plan are listed in the
rc7 pull request). Written 2026-09-23 after 1.4.0-rc6, from Kyler's request and his answers below. It
reverses rc4's rule that a waiting room is a main-menu page only (and D20 of `design/PRE-GAME-LOBBY-PLAN.md`): a
save can now be hosted, and a room joined, from inside a running game, with nobody sent to the main menu.

Read with: `CLAUDE.md` (standing rules), `TWO-COLONIES.md` (rules, the waiting room), `design/PRE-GAME-LOBBY-PLAN.md`
(the room's design and native-UI recipe), `design/review-1.4.0-rc4/FINDINGS.md` (what rc5 fixed in these paths),
`STABILITY-CHANGELOG.md` entries rc4 to rc6.

## 1. What Kyler asked for

> Please merge the "Host co-op" and "Load game" menu features, where "Host co-op game" is to the right of the "Load"
> button. I only want a "Load game" menu. You can remove the "Host co-op menu" from the main game screen. I want to have a
> flow where in the Load Game menu, I can select a settlement/save and click "Host co-op game" (similar to the original
> flow in the beaverbuddies mod, put it to the right of the load button). I want the UI to look clean and professional and
> close to the native timberborn UI style as possible with perfect formatting. The other UI menus you made for
> multiplayer games are designed perfectly.
>
> The host stays in the game, and a UI menu pops up where the host is waiting for other players to join. Once the other
> players have joined, the host can click start. Don't send the players back to the main menu when this flow is used. A
> key difference too is that player 2 doesn't just immediately load the game when joining (like in the old beaverbuddies
> mod). They join a lobby (this can be a UI window, don't send them to the main menu if they are in game) and after the
> host confirms everyone has joined the lobby, you can start and the saves will begin loading for both players.
>
> Players should be able to "Load game" => "Host co-op game" with the above flow while they are both in an active
> timberborn game.

His decisions (2026-09-23):

| # | Question | Decision |
|---|---|---|
| K1 | Host and guest already play a co-op game together; the host picks Load game → Host co-op game. What does the guest see? | **Carried into the lobby**: the session ends and the guest's game shows the host's lobby window by itself, no click. |
| K2 | While the lobby window is open over a game, what happens to the game behind it? | **Paused, window on top.** Closing it (Cancel, Leave) returns to the game as it was. |
| K3 | At Start each player's current game is replaced by the hosted save. Save it first? | **Exit save, like Exit to menu.** Skipped for a guest whose game is the host's co-op game (its copy is the host's). |
| K4 | The game menu's Host co-op game (alone) and Save and Rehost (co-op host), which went to the main menu? | **Keep, with the in-game flow**: one click saves the game you're in and opens the lobby window over it. **Join co-op game** comes back to the game menu too. |

## 2. The flows, as players see them

Words in bold are button labels exactly as shown in game.

**A. Main menu.**
- The main menu has **Load game** and **Join co-op game** under it; its own **Host co-op game** button goes.
- The Load game box has **Delete settlement**, **Delete save**, **Load** and, right of it, **Host co-op game**, all the
  game's medium menu buttons. The gold line under the save's picture (*Separate colonies: 2 players*, *One shared
  colony…*) is shown for every selected save.
- **Host co-op game** there opens the existing full-screen **Co-op Game** page (unchanged: `LobbyHostPanel` page mode).
  Its Cancel returns to the Load game box.
- New games are unchanged: **Host co-op game** beside **Start** on the difficulty page opens the same page.

**B. Hosting a save from a game played alone.**
- Esc → **Load game** → a settlement and save → **Host co-op game**. The save goes through the game's own checks, as
  a load does. Then the **lobby window** opens over the game, which pauses (K2).
- The window holds what the page holds: the save's plate (settlement, save name and date), the gold notes, the
  shared-save conversion checkboxes, a mixed game's faction picker, the players' board (each row's colony and faction,
  Ready marks, the host's remove button), **Invite Friends** and the direct-IP line, the status line, **Cancel** and
  **Start Game**.
- **Cancel** closes the room (its guests are told, as today) and returns to the Load game box; Esc, again, to the game.
- **Start Game** (asking first if someone isn't ready, as today): the host's current game gets its exit save (K3),
  the chosen save's bytes go to every guest, and everyone loads it. Nobody passes through the main menu.
- Shortcut (K4): Esc → **Host co-op game** asks, saves this game as `<date> Co-op` (as today), and opens the window
  for that save, in this game. No exit save at Start for this game: it was just saved.

**C. Hosting while hosting a co-op game** (Load game → Host co-op game, or Esc → **Save and Rehost**).
- Save and Rehost saves `<date> Rehost` (as today) and hosts it; Load game → Host co-op game hosts the chosen save
  (the current co-op game gets its exit save at Start, K3).
- Before the room opens, the guests are told the host is moving to a room, and the session ends quietly: **no**
  *connection lost* message on either side.
- Every guest's game pauses and shows the host's lobby window by itself (K1), already in the room. They press
  **Ready** as usual; the host presses **Start Game**.
- The desync dialog's **Save and Rehost** does the same.

**D. Joining from a game played alone.**
- Accepting a Steam invite (or Esc → **Join co-op game**, back in the game menu, K4) opens the lobby window over the
  game, which pauses. rc6's *Save and join* box goes.
- The window shows the host's room as the guest page does today (**Ready** / **Not ready**, **Leave**, the faction
  picker in a mixed room, the notes). **Leave** returns to the game as it was.
- At the host's Start, the guest's game gets its exit save (K3), then the host's save loads.

**E. A guest in the host's co-op game**: carried (C). No exit save for them (K3).

**F. A player in someone else's co-op game** who accepts an invite: unchanged from rc6, a box says to leave that game
first (`InviteStep.LeaveCoopGameFirst`); the running session is never touched. A host hosting their own room: rc6's
`StopHostingFirst`.

**G. A lost connection** (the host's game crashed or quit, not a move from C): the guest's *connection lost* box keeps
**Rejoin** and **Stay here**. **Rejoin** now waits **in the game** (the rc5 waiting box over the paused game) and opens
the lobby window when the host's room is up. The desync dialog's **Reconnect (wait for Rehost)** does the same. No
main menu.

**H. Failure and edges.**
- The host's room fails to start (a guest's join stalls, the save can't be sent): the window says why, as the page does
  today, and the host is back in their game.
- The host closes the room, or removes a guest: the guest's window closes with the host's reason in the game's own box,
  and the guest is back in their game.
- A guest's connection drops while in the window: the window closes and the join's error box says so (rc5 behaviour).
- The Load game box's **Host co-op game** is hidden for a player whose game was loaded as a guest (also after that
  session ended) and after a failed action: `HostButtonRules` (rc5) applies to it as to the game menu.

## 3. UI specification

**Native look.** Everything is built from the game's own templates and classes, as the room's page and the rc3/rc4
checkboxes are (see `design/PRE-GAME-LOBBY-PLAN.md` §5 and the memory of what already works). No new colours, fonts or
custom art.

**The Load game box** (the game's `LoadGameBox`, `Views/Options/LoadGameBox.uxml`, in both scenes):
- A copy of **Load** named `HostButton`, inserted right after it (`ButtonInserter.DuplicateOrGetButton`, which makes an
  initialised `NineSliceButton` since rc5, so it clicks like the game's).
- **Width.** Four medium buttons do not fit the box as the game sizes it: `.load-box` is 800 px wide
  (OptionsStyle), its `.box__content-container` has 45 px padding each side (CoreStyle), which leaves 710 px, and four
  `.menu-button--medium` buttons are at least 4 × 184 = 736 px (CoreStyle; `.box-buttons` is a centred row,
  OptionsStyle). Widen the box's `load-box` element by 70 px (inline `style.width`) when the Host button is added. Don't
  shrink the game's buttons. A RuntimeChecks UI-file check reads these numbers from UI.zip and fails if they change.
- **The gold line** (`HostCoopMenu`'s status label today: `game-text-small text--yellow`, centred, max width 300,
  two lines kept) moves to the merged box and shows for every selected save, read off the menu's thread as rc5 does.
- Enter and double-click load, natively (rc4's host mode, `HostCoopMenu.HostMode` and the `LoadGame` prefix go).
- The title is the game's *Load game* again (no title swap).

**The lobby window (in a game).** A box like the Load game box, not the main menu's full-screen wizard page:
- the game's `Common/NamedBoxTemplate` (capsule header, close button, `sliced-border box__content-container`), pushed
  with `PanelStack.HideAndPush` over the Load game box or the game menu (so its Cancel returns there), or with `Push`
  when opened by an invite or a carried guest;
- header: *Co-op Game* for the host, *{host}'s Game* for a guest (the page's own texts,
  `BeaverBuddies.Lobby.Header.Host` / `.Guest`);
- content: the page's sections in the page's order (plate, notes, options slot, faction slot, list title and board,
  invite row and direct-IP line, status line), then a `box-buttons` row with the pair (**Cancel** / **Start Game**,
  **Leave** / **Ready**) as `menu-button menu-button--medium`;
- close button and Esc: the host's Cancel (asks first, as the page's Back does), the guest's Leave (asks first);
- **size**: the board scrolls (`scroll--green-decorated`); the box never grows past the screen. Reuse the page's
  sizes (the notes' 600 px max width) and check that the longest texts wrap in two lines at most.

**Styles in a game.** `LobbyPage` uses classes from sheets the main menu loads (`LobbyPage.ClassesUsed`: CommonStyle,
CoreStyle, OptionsStyle, MainMenuStyle, MainMenuMiscStyle, ModdingStyle). A game scene loads CoreStyle, CommonStyle and
the game sheets, not the main menu's. The window adds the missing sheets to its own root:
`root.styleSheets.Add(assetLoader.Load<StyleSheet>("UI/Views/MainMenu/MainMenuMiscStyle"))` and so on (Resources paths,
as the UXML `<Style src="/Assets/Resources/UI/Views/...uss">` lines name them; `IAssetLoader` is the game's asset
loader, bound everywhere). Or build the window from a UXML that attaches them. Either way a RuntimeChecks check must show
every class in `ClassesUsed` (and any new one) is defined in a sheet the window has. The rc3 checkbox rows
(`new-game-mode-panel__*`) are MainMenuMiscStyle classes too.

**The page frame vs the window frame.** Refactor `LobbyPage` so its content is built once and hosted by either frame:
the main menu's `MainMenu/NewGameTemplate` page (unchanged look) or the in-game box. Everything that fills the content
(`SetSummary`, `SetFactionNote`, `SetColonyOptions`, `SetFactionPicker`, `SetPlayers`, `SetStatus`, `Back`/`Next`,
`Invite`, `DirectIp`) keeps its API, so `LobbyHostPanel` and `LobbyGuestPanel` change little.

**The game menu** (Esc, `GameOptionsBox`), under **Load game**:
- **Host co-op game** (a game played alone) or **Save and Rehost** (the host of a co-op game, also after its session
  ended), hidden for a guest's game (`HostButtonRules`, rc5);
- **Join co-op game**: shown when no co-op session is live in this game and this player hosts no room. It opens the
  Join box (friends' games with Steam, the address box without), as the main menu's does, and the join opens the lobby
  window over this game.

**Texts** (English CSV; other languages fall back): the in-game window reuses the page's keys. New or changed keys at
least: the move notice's log lines (none shown), the guest's window header if a new key is needed, and the removal of
rc6's `BeaverBuddies.Invite.FromGame` / `SaveAndJoin` (the invite now opens the window). `Invite.InCoopGame` and
`Invite.WhileHosting` stay. `BeaverBuddies.Lobby.InGameInvite` (D20's message) becomes unused: remove it with the path.

## 4. Architecture and code changes

The network side already runs outside the main menu: `LobbySession` owns a `ServerEventIO` room (`StartLobby`) that is
**not** installed as `EventIO` until the save loads (`LobbySession.Update` → `EventIO.Set(IO)` → `LoadScene`), and
`ISceneLoader` is bound in the Bootstrapper context, so `Start` can load the save from a game scene. The work is the UI
in a game, the guest's connection inside a running game, carrying guests, and removing the main-menu detours.

### 4.1 Panels in both scenes
- Bind `LobbyHostPanel`, `LobbyGuestPanel` and `NewGameFactionCapture` in the game scene too
  (`Plugin.ReplayConfigurator.Configure`, **before** `if (EventIO.IsNull) return;` like `RehostingService` since rc5).
  Their dependencies all exist in the Game context (checked in the decompiled 1.1.2.4, see §8).
- `LobbyHostPanel.OpenFrom(NewGameModePanel)` stays main-menu only. `OpenForSave(save, bytes)` works in both scenes and
  picks the frame (page in the main menu, window in a game).
- `LobbyGuestPanel.Pump` opens the window in a game (today it waits for `PageOnTop()` and the main menu). It finds the
  joining client through `ClientConnectionService` (see 4.3), not `EventIO.Get()`, in a game.
- `HostCoopMenu` (main-menu singleton: host mode, pending save across the scene change) goes. What remains of it (the
  gold line's reader and cache) becomes part of the Load game box patches, working in both scenes.
- `HostCoopFlow.HostInMainMenu`, `PendingSave` and the main menu's pending-save opening go.

### 4.2 Hosting from a game
- `ServerHostingUtils.LoadAndHost` (after the game's validators, `LoadIfSaveValidAndHost`) calls the scene's
  `LobbyHostPanel.OpenForSave` in both scenes (today it refuses in a game: "A save can be hosted only from the main
  menu").
- `RehostingService.HostThisGame` and `RehostGame`: save as today (`SaveRehostFile(..., waitUntilAccessible: true)`),
  then host that save in this scene: `LoadIfSaveValidAndHost` or straight to `OpenForSave` (the save was just written).
  Mark it so no exit save is made at Start (K3).
- **A live co-op session (the host's)** must end before the room opens: the room's server needs the same port and the
  Steam lobby. Order: (1) tell the guests the host is moving to a room (4.4); (2) save if rehosting; (3) end the session
  on the host without a message (`ReplayService.EndSession(null)` or a new quiet variant) and close its server; (4) open
  the room. Make sure the old listener's port is released before the new one binds (TimberServer close, then start).
- Pausing (K2): pushing the window through `PanelStack` pauses a game by itself (see §8: `PanelShownEvent.LockSpeed` →
  `SpeedManager.ChangeAndLockSpeed(0)`; unlocked when the panel is hidden). Don't add another pause. In a co-op game
  that has just ended its session this is the same single-player behaviour.

### 4.3 Joining from a game: keep the game single-player until Start
- Today `ClientConnectionService.TryToConnect(ISocketStream)` installs the join as `EventIO` at once. In a game played
  alone that turns the running game into a "co-op" one while the guest waits: patched actions would be sent to the host
  instead of played (`ReplayEvent.DoPrefix`), and the tick patches read `EventIO`. So **in a game scene the join is held
  by `ClientConnectionService` and installed as `EventIO` only when the save arrives** (`LoadMap`, just before
  `SingletonManager.Reset` and the scene load). In the main menu keep today's behaviour.
- Consequences to handle: `ClientEventIO.OnConnectionError` decides by `ReferenceEquals(EventIO.Get(), this)`
  (`ConnectionErrorPlanner`); a held join must still report while joining. `LobbyGuestPanel` watches the held client.
  `ClientConnectionService.UpdateSingleton` already pumps `client.Update()` and `CheckWaitingRoom()`.
- `JoinFlowRules.CheckWaitingRoom`: `LeaveForGame` (D20) becomes "show the window" in a game scene. Keep the rule pure
  and tested.
- `InviteRules` (rc6): `OfferFromGame` becomes "join in this game" (open the window; no Save-and-join box).
  `SteamOverlayConnectionService.OfferJoinFromGame`, `pendingInviteLobby` and `JoinPendingInvite` go.
  `LeaveCoopGameFirst` and `StopHostingFirst` stay.
- The game menu's **Join co-op game** returns (`ClientConnectionUI.AddJoinButton`: today hidden when `!mainMenu`), shown
  per 3.
- `ClientConnectionService.Reconnect` (Rejoin, the desync dialog's Reconnect): no `OpenMainMenu`. `WatchRejoin` works in
  a game scene (today it returns unless `LobbyGuestPanel` exists, which is main-menu only), with its box over the paused
  game, and the lobby window when welcomed. Keep rc5's quiet tries, probe off the main thread, the Steam lobby check and
  the give-up rules.

### 4.4 Carrying the guests (K1)
- **The notice.** Before the host ends a live session to open a room (C), it tells every guest. Prefer a TimberNet
  control frame sent by the server to all players (like the status and lobby frames, `LobbyFrames` / `StatusFrames`),
  handled on the guest's network thread into a flag the main thread reads; not a replay event (the session is ending,
  and replay events wait for ticks). It says: moving to a room, and (optionally) how to reach it.
- **The guest**, on the notice: ends its session quietly (no *connection lost*, no Rejoin box), keeps its route
  (`lastJoin`) and starts the in-game rejoin at once (4.3). A direct-IP guest redials the typed address (the room
  listens on the same port as soon as it opens). The window opens when the room welcomes it.
- **Steam: keep the lobby.** A Steam guest can only connect to a host whose current lobby it is in
  (`SteamListener.IsInLobby`), and today every server makes a new lobby (`SteamListener.CreateLobby`) and leaves it on
  stop (`Stop` → `LeaveLobby`). For a move, the old listener must **hand its lobby to the room's listener** instead of
  leaving it (for example a static handover slot read by the next `SteamListener` before `CreateLobby`), which reopens it
  (joinable, `bb_open` "1", `bb_room` "1", new description). The guests are still members, so they connect straight to
  the host's Steam ID (`TryToConnect(CSteamID)`), even when the lobby is invite-only. A lost connection (G) still finds a
  new lobby as rc5 does.
- **Timing.** The guests' old connections close as the host's old server stops; the notice must be read first. Send it,
  flush (`FlushSteam` / the send lane), then close. A guest that missed it (a slow or dropped link) sees *connection
  lost* with **Rejoin**, which reaches the same room (G), so nothing is stuck.

### 4.5 Start: exit saves (K3) and loading
- The exit save is the game's own: `Timberborn.Autosaving.Autosaver.CreateExitSave()` (public; Game context only;
  already wrapped by the mod's `AutosaverCreateExitSavePatcher`). `MainMenuSceneLoader.SaveAndOpenMainMenu` is not
  usable here (it opens the main menu).
- **Host:** at Start, before `LobbySession.Start` loads the save, make the exit save of the current game unless it was
  just saved by the Esc shortcuts, or the game was loaded as a guest. Autosaver is a game-scene service: reach it through
  a small game-scene singleton (bound in the game scene's configurator), not from `ClientConnectionService` or the
  panels, which are also bound in the main menu.
- **Guest:** in `ClientConnectionService.LoadMap`, before `SingletonManager.Reset`, the same, unless this game was
  loaded as a guest (`ReplayService` present and `!LoadedAsHost`) or is the main menu.
- The save failing must not stop the start: log and go on.

### 4.6 The main menu, simplified
- `ClientConnectionUI.AddJoinButton(mainMenu: true)`: no Host button (the Join button goes after **Load game**).
- `MainMenuFit` / `FitMainMenu` (rc5, C3) go: with Join only, the panel is 602 px against the band's 616 px, as before
  rc4. Update rc5's C3 checks to require the Host button gone and the band untouched.
- `HostCoopFlow` / `HostCoopMenu` go (4.1).

### 4.7 What stays
- New games and their waiting room (the Game Mode page) and the full-screen page in the main menu.
- The room's network code (`LobbyRoom`, `LobbyFrames`, `LobbyInbox`, `TimberServer`), Ready, the host's Start rules,
  the save conversion (`SaveConversion`, the room's `separateAtStart`), mixed factions in rooms.
- rc5's rejoin machinery (`WatchRejoin`, probes, `RejoinGivesUp`, `RejoinEntersLobby`, closing boxes), rc6's direct
  connect on the network thread, `HostButtonRules`, `InviteRules` (reworded).

## 5. Wire and saves
- New: the host's move notice (4.4). Everyone runs the same build (the handshake checks), so no compatibility code.
- Saves unchanged. The Steam lobby's data keys unchanged.

## 6. Checks

**StabilityTests** (headless; CI runs them): one named check per behaviour, as `StabilityTests/Rc6Checks.cs` does.
- The pure rules: the Load game box's Host button visibility; `InviteRules` (alone → join in game); `CheckWaitingRoom`
  (a game shows the window); when an exit save is made (host, guest, carried guest, Esc shortcut, main menu).
- The move notice: a TimberNet logic check with an in-memory pipe (as `LobbyChecks` / `JoinReviewChecks` do): the server
  sends it, the client reads it before its connection closes, and a client that got it reports no connection error.
- Source checks for the wiring (the order: notice → save → end → open; `EventIO` not installed in a game before
  `LoadMap`; no `OpenMainMenu` left in the rejoin and hosting paths; `Bind<LobbyHostPanel>` before the co-op return).
- Update the checks this plan changes: rc4's hosting checks (`RcMainChecks` rc4 section), rc5's A7/C3 and parts of A1,
  A8 (`Rc5Checks`, `Rc5RuntimeChecks`), rc6's invite checks (`Rc6Checks`, `Rc6RuntimeChecks`), `JoinBoxChecks`, and
  `JoinFixChecks` (it pins D20's `LeaveForGame` and `InGameInvite`).

**RuntimeChecks** (need the game's assemblies; run locally): the game members the new code uses (below), the
UI.zip numbers of 3 (box width, padding, button minimum), every class the window uses defined in a sheet it loads,
the IL of the key orders (no `OpenMainMenu` in `Reconnect`, `Autosaver.CreateExitSave` before the load), and that
`LobbyHostPanel`/`LobbyGuestPanel` bind in the game context.

## 7. Docs
- README: hosting (a save: Load game → Host co-op game, in the menu or in a game; the game you're in: Esc), joining
  from a game, rehost and rejoin without the main menu. Player-facing: no version history ("since rc7"), fresh games
  only.
- TWO-COLONIES: the waiting room section (both frames; joining from a game; carrying; the Steam lobby kept on a move;
  exit saves).
- ALPHA-TEST-SCRIPTS: a new script for in-game hosting (both players in a game, carried guests, Leave/Cancel, the
  exit saves, Steam and IP), and the steps of Scripts H, D, S that now differ (the main menu's Host button, rc6's Save and
  join, Rejoin via the main menu).
- STABILITY-CHANGELOG `## 1.4.0-rc7` and `BeaverBuddies/changelog.txt` (shown in game) at release.
- Don't change `docs/` (the website).

## 8. Game facts the work relies on (Timberborn 1.1.2.4, from the decompiled assemblies)

**Bindings.** In both `MainMenu` and `Game` contexts: `GameSaveDeserializer`, `SaveMetadataSerializer`,
`TimestampFormatter`, `FactionSpecService`, `FactionUnlockingService`, `ITooltipRegistrar`, `InputService`,
`ValidatingGameLoader`, `GameSceneLoader`, `PanelStack`, `DialogBoxShower`, `VisualElementLoader`, `LoadGameBox`,
`MainMenuSceneLoader` (also MapEditor). `ISceneLoader`: Bootstrapper (global). `Autosaver`: `Game` only.
`SpeedManager`: `Game` and `MapEditor`.

**PanelStack** (`Timberborn.CoreUI`): `Push`, `PushOverlay`, `PushDialog`, `HideAndPush`, `HideAndPushDialog`,
`HideAndPushOverlay`, `HideAndPushWithoutPause`, `Pop` (throws unless the panel is on top), `IsPanelOnTop`; the private
`_stack` and `TopPanel.IsOverlay` are used by the mod. Every push but `HideAndPushWithoutPause` posts
`PanelShownEvent(isDialog, lockSpeed: true)`; in a game a listener calls `SpeedManager.ChangeAndLockSpeed(0f)`, and the
speed unlocks when the panels are hidden. Esc goes to the top panel's `OnUICancelled`, Enter to `OnUIConfirmed`.

**LoadGameBox** (`Timberborn.GameSaveRepositorySystemUI`): `Open()` does `_panelStack.HideAndPushOverlay(this)` then
loads the settlements. Members the mod uses: `GetPanel`, `OnUICancelled`, private `LoadGame()` (returns bool),
`OnSaveSelectionChanged()`, `_saveList` (`TryGetSelectedSave`), `_gameSaveRepository`, `_validatingGameLoader`,
`_dialogBoxShower`, `_loc`, `_visualElementLoader`. UXML names: `Header`, `SavesWrapper`, `LoadButton`,
`DeleteSettlementButton`, `DeleteSaveButton`, the Thumbnail with `ShowSavedModsButton`.

**UI sizes** (UI.zip): `.load-box` 800 × 685 px (OptionsStyle); `.box__content-container` padding 45 px (CoreStyle);
`.menu-button--medium` height 33, min-width 184, 13 px (CoreStyle); `.box-buttons` flex row, centred, min-height 32,
margin-top 10 (OptionsStyle); `.load-box__thumbnail` 325 × 185. `Common/NamedBoxTemplate`: `NamedBoxTemplate`
(`content-row-centered`) → `sliced-border box__content-container` (content container) with `HeaderWrapper` /
`Header` (`capsule-header`) and `CloseButton` (`close-button`).

**Buttons.** `LocalizableButton` needs a `text-loc-key` or the initializer (`VisualElementLocalizer`) throws; the mod's
added buttons are `NineSliceButton`s initialised with `VisualElementInitializer.InitializeVisualElement` (click sound
from `UISoundInitializer`, any-modifier clicks from `ButtonClickabilityInitializer`). Get the initializer from
`VisualElementLoader._visualElementInitializer` or by injection.

**Saving and scenes.** `MainMenuSceneLoader.SaveAndOpenMainMenu()` posts `PreMainMenuStartedEvent(skipAutoSave:
false)`, which makes `Autosaver.CreateExitSave()` save once (`Save(instant: true)`, only with a settlement); `OpenMainMenu()`
skips it. `GameSaver.Save` is synchronous; rc4's `SaveRehostFile(waitUntilAccessible: true)` defers its callback a frame
so the file is closed.

**Steam** (Steamworks.NET): a guest that is a member of a lobby is let in by the host's `SteamListener.IsInLobby` check at
accept time; `SteamMatchmaking.LeaveLobby` ends membership; lobby data needs `RequestLobbyData` for lobbies the player is
not in.

## 9. Risks and open points
- **Not buildable in the cloud.** The mod and RuntimeChecks need the game's assemblies; a session without them can only
  run StabilityTests. Keep game-API use to members listed in §8 or already used in the mod, and leave the build, the
  RuntimeChecks run and the release to a session on Kyler's machine.
- **The held join (4.3)** is the most delicate part: every place that assumes a guest's join is `EventIO` (the guest
  panel, `CheckWaitingRoom`, the error planner, `ReportWhileJoining`, `LoadMap`) must be walked.
- **The move (4.4)**: port reuse and the Steam lobby handover; a guest must never end up in the host's closing session.
- **The window's look** can't be seen without the game: rely on the game's templates and the UI.zip arithmetic, and list
  screenshots to take in the new test script.
- Mixed-factions rooms for a save in a game need the host's unlocked factions: `NewGameFactionCapture.UnlockedFactions`
  uses `FactionUnlockingService`, bound in the game too (§8).

## 10. Suggested order
1. Load game box merge and main-menu cleanup (3, 4.1, 4.6): smallest, visible, and removes rc4's host mode.
2. Panels in both scenes and the in-game window frame (3, 4.1), hosting from a game played alone (4.2, B).
3. Joining from a game played alone: the held join, the window, invites and the game menu's Join (4.3, D).
4. Rejoin in a game (G), then carrying guests (4.4, C, E) with the Steam lobby handover.
5. Exit saves (4.5).
6. Checks, docs, the new test script; then the local build, RuntimeChecks and the rc7 release.
