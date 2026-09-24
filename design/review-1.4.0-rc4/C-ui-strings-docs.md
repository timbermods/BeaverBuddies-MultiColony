# Review of 1.4.0-rc2 to rc4, reviewer C: UI patches, new strings, docs

Scope: the Game Mode page's colony checkboxes (`Lobby/NewGameColonyOptions.cs`, `LobbyPatches.cs`), the room's options
slot (`LobbyPage.cs`, `LobbyHostPanel.BuildConvertOptions`, `LobbyGuestPanel.ShowNote`), the Host co-op game box
(`Connect/HostCoopFlow.cs`, LoadGameBox patches in `Connect/ServerHostingUtils.cs`), the main-menu and Esc buttons
(`Connect/ClientConnectionUI.cs`, `Colonies/SharedColonySplit.cs`), the rejoin wait (`ConnectingBox`,
`ClientConnectionService.WatchRejoin`, `ReplayService.EndSession`), rc2's Ctrl+T line and tooltip
(`TradeOverviewPanel.cs`), the English CSV diff `ebe3d4a..ebb6570`, and README / TWO-COLONIES / ALPHA-TEST-SCRIPTS.
Game sources: UI.zip (files named below) and SCRATCH/game decompiled assemblies. Nothing was built or run.

Paths: mod files are under `BeaverBuddies/` unless given in full; `dec:` means `SCRATCH/game/<Assembly>.decompiled.cs`.

**Summary:** 12 findings: 10 Confirmed and 2 Plausible (C3, C9). By kind: 2 are a feature that can't be reached or
a function that breaks (C1, C2), 8 are UI or look (C3 to C9, C12), 1 is strings (C10) and 1 is dead code (C11). There
are also 8 docs items (section 3), one of them the public website.

---

## 1. Findings (most severe first)

### C1. The game menu's **Host co-op game** never appears in a single-player game, so hosting from a game played alone can't be reached
- **Status:** Confirmed. **Kind:** gameplay/UI (an advertised rc4 feature can't be reached). **Hits:** every host who
  wants to host the game they are playing alone (README step 4, TWO-COLONIES "From inside a game", in-game changelog
  rc4 bullet 2, Script H step 6).
- **Evidence:**
  - `Plugin.cs:53` `if (EventIO.IsNull) return;`: a game loaded alone returns here, before `Plugin.cs:63`
    `Bind<RehostingService>()`. `SingletonManager.Reset()` at the top of the same `Configure` clears any earlier one.
  - `Connect/ClientConnectionUI.cs:106-108` `DressHostInGame`:
    `host.ToggleDisplayStyle((alone || hosting) && SingletonManager.GetSingleton<RehostingService>() != null)`. Alone,
    RehostingService is null, so the button is hidden.
  - `ClientConnectionUI.cs:119-120` `HostClicked` also returns early when it is null.
  - `RehostingService`'s constructor needs only game-scene services (AutosaveNameService, GameSaver,
    GameSaveRepository, SettlementReferenceService, ValidatingGameLoader, DialogBoxShower, MainMenuSceneLoader). Its one
    co-op dependency is static: `ReplayService.HasReplayFailure`.
- **Scenario:** Load any save with Load game and press Esc. Under Load game there is only **Join co-op game**. Script H
  step 6 fails at its first check.
- **Fix:**
  - Move `containerDefinition.Bind<RehostingService>().AsSingleton();` above `if (EventIO.IsNull) return;` in
    `ReplayConfigurator`.
  - `ReplayService.HasReplayFailure` is reset only in `ReplayService`'s constructor (`ReplayService.cs:192`), so a
    failed co-op session earlier in the same process would make `SaveRehostFile` refuse a solo game. Reset it
    for a solo scene too, e.g. in `RehostingService`'s constructor when `EventIO.IsNull`, or with a static reset in
    `ReplayConfigurator` before the early return.
- **Check:** a StabilityTests source check that, in `Plugin.cs`'s `ReplayConfigurator.Configure`,
  `Bind<RehostingService>` comes before `if (EventIO.IsNull) return;`, and that the solo path resets
  `HasReplayFailure`.

### C2. Hosting from the Host co-op game box leaves `HostMode` on in the game, so the in-game **Load game** silently does nothing
- **Status:** Confirmed. **Kind:** gameplay/UI (a game function breaks without a word). **Hits:** a host whose game
  was started from the main menu's Host co-op game box, for the rest of that game, until they return to the main
  menu. Script A's setup uses exactly this path, then its line 21 "Save, reload, host again".
- **Evidence:**
  - `Connect/HostCoopFlow.cs:66` `HostMode` is static.
  - It is cleared only by `HostCoopMenu`'s constructor (`:94`, a main-menu singleton) and by `BoxClosed()` (`:107`),
    through the postfix on `LoadGameBox.OnUICancelled` (`ServerHostingUtils.cs:118-122`).
  - Pressing **Host co-op game** in the box takes this path: `HostSelectedGame` → `LoadIfSaveValidAndHost` →
    `LobbyHostPanel.OpenForSave` → `_panelStack.HideAndPush(this)` (`LobbyHostPanel.cs:201`). The box stays on the
    stack, hidden, and `OnUICancelled` is never called. **Start Game** then changes scene with `HostMode == true`.
  - In the game, `LoadGameBox` is bound again (`dec:Timberborn.GameSaveRepositorySystemUI` 448-449,
    `[Context("MainMenu")][Context("Game")]`) and its Load button's click calls `LoadGame()` (`dec` 537). The prefix
    `LoadGameBoxLoadGamePatcher` (`ServerHostingUtils.cs:107-114`) sees `HostMode` and routes to
    `HostSelectedGameFromBox`. That reads the whole save (`GetMapBtyes`) and then reaches `LoadAndHost`, where
    `LobbyHostPanel` is null in a game, so it only logs *"A save can be hosted only from the main menu; nothing was
    hosted"* (`:213`).
  - Enter (`OnUIConfirmed`) and a double-click (`OnDoubleClickActionRequested`) go through the same `LoadGame`.
- **Scenario:** Main menu → Host co-op game → a save → Host co-op game → Start Game (alone, or with a friend). In the
  game: Esc → Load game → pick a save → Load. Nothing happens, and the log has one warning. After a desync, the
  in-game Load that TWO-COLONIES points to is dead too. The docs' "**Load game** only loads" (README:125,
  TWO-COLONIES:89) is then false.
- **Fix:**
  - Treat host mode as a main-menu state: in the `LoadGame` prefix and in the `OnSaveSelectionChanged` postfix, write
    `if (!HostCoopMenu.HostMode || HostCoopMenu.Instance == null) return true;`.
  - Also clear it where the box stops mattering: call `HostCoopMenu.BoxClosed()` in `LobbyHostPanel.OpenForSave` when
    the page is pushed, or at the room's Start, and in `ReplayConfigurator.Configure`.
- **Check:** StabilityTests: the prefix body contains `HostCoopMenu.Instance == null`, and `ReplayConfigurator`
  (or `LobbySession.Start`) calls `HostCoopMenu.BoxClosed()`. Script H: add "after a game started from the box, Esc →
  Load game → Load loads".

### C3. With the new **Host co-op game** button the main menu's button panel no longer fits its 720 px band (about 26 to 30 px over)
- **Status:** Plausible (layout arithmetic from UI.zip; one screenshot settles it). **Kind:** UI/look. **Hits:**
  every player on every main-menu visit (whenever a save exists, so Continue shows).
- **Evidence:**
  - Band and panel sizes:
    - `Views/MainMenu/MainMenuMiscStyle.uss:507-510`: `.main-menu__content { height: 720px }`.
    - `:5-9`: `.background__logo { height: 104px }`, which leaves about 616 px for `MainContent`.
    - `:516-521`: `.main-menu__panel-wrapper { flex-shrink: 0 }`.
    - `:84-89`: `.main-menu-panel { padding: 35px; flex-shrink: 0 }`.
    - `:501-505`: the two `.main-menu-spacer`s are 50 px and may shrink to 0.
  - Button sizes: `Views/Core/CoreStyle.uss:104-118` `.menu-button { height: 44px }` (`*` zeroes margins, CoreStyle
    1-9). The Discord button is 40 px with an 8 px top margin (MainMenuMiscStyle 119-125).
  - The count: `Views/MainMenu/MainMenuPanel.uxml:14-23` has ten buttons. Continue shows whenever a save exists
    (`dec:Timberborn.MainMenuPanels` MainMenuPanel `_continueButton.ToggleDisplayStyle(... != null)`).
    `ClientConnectionUI.cs:84-100` adds Host (new in rc4) and Join.
  - The sum is 70 + 12×44 + 48 = **646 px**, against about 616 px (620 if Yoga also shrinks the 104 px logo band a
    little). Before rc4 (Join only) it was 602 px and fitted with 14 px to spare.
- **Scenario:** Title screen with any save. The menu's bottom frame and the Discord logo run over the bottom
  decorative bar (`.background__bottom-bar`, absolute, `bottom: -5px`), and the frame hangs about 25 px below the
  brown band.
- **Fix:** Keep the panel at 11 rows. Two options:
  - Put **Host co-op game** and **Join co-op game** side by side in one 44 px row: a row `VisualElement` holding two
    `menu-button` copies, each `flexGrow 1`, with a 4 px gap.
  - Or keep a single **Co-op game** button that opens a two-button DialogBox (Host co-op game / Join co-op game).
  - The Esc menu (`GameOptionsBox`, `grow-centered` over the whole screen) has room and needs nothing.
- **Check:** a RuntimeChecks UI-file check. Read `MainMenuPanel.uxml` and the USS values above, count the mod's added
  rows (from the IL of `AddJoinButton`), and require `70 + rows×44 + 48 ≤ 720 − 104`.

### C4. When a session ends, the Esc menu treats every player as playing alone: a former guest is offered **Host co-op game**, and the host's **Save and Rehost** turns into it
- **Status:** Confirmed. **Kind:** UI (wrong button and label). **Hits:** every player after a lost connection, a
  desync or a failed action.
- **Evidence:**
  - `DressHostInGame` (`ClientConnectionUI.cs:106-109`) reads "alone" as `EventIO.IsNull`.
  - `EventIO.Reset()` runs in `ReplayService.EndSession` (`ReplayService.cs:565`), `HandleDesync` (`:623`) and
    `AbortReplay` (`:538`), while the co-op scene still has `RehostingService`.
  - `GameOptionsBox.GetPanel` runs the postfix every time the menu opens (`dec:Timberborn.OptionsGame`, `Show` →
    `PushOverlay`).
  - The class comment (`ClientConnectionUI.cs:77-81`), `RcMainChecks.cs:316-317` and Script H step 7 all promise that
    a guest has neither button.
- **Scenario A (a guest):** the host's connection drops. The guest's dialog offers **Rejoin** or **Stay here**. The
  guest chooses Stay here, opens Esc and sees **Host co-op game**. Pressing it saves a "… Co-op" copy of the guest's
  own world and opens a Co-op Game page for it, possibly just as the real host rehosts. That splits the group across
  two divergent copies (after a desync, the guest's copy is the one out of step).
- **Scenario B (the host, after a desync):** the host closes the desync dialog with **No** and later opens Esc. The
  button reads **Host co-op game**, not **Save and Rehost**. The confirm text is the solo one ("Host this game as a
  co-op game?") and the save is named "… Co-op". It still works, since `PendingRehost` is never read (C11), but the
  label contradicts README step 4.
- **Fix:** Decide from the scene, not from the live IO.
  - Record the role once when the co-op scene loads, e.g. `ReplayService.SceneRole = Host | Guest`, or reuse
    `ColonySession`'s host flag.
  - A former host keeps **Save and Rehost** (calling `RehostGame`, which is also what the desync dialog does).
  - A former guest gets no Host button. It could instead get **Rejoin**, which calls
    `ClientConnectionService.RejoinFromGame`, so a player who chose "Stay here" can still rejoin.
- **Check:** StabilityTests: `DressHostInGame` does not use `EventIO.IsNull` as the solo test (it reads the recorded
  scene role), and a `ReplayService` field is set in its constructor from `EventIO.Get() is ServerEventIO`.

### C5. The rejoin wait never says why it isn't getting in: every refusal is swallowed and retried every 3 s
- **Status:** Confirmed. **Kind:** UI (a silent wait with no end). **Hits:** a guest who pressed **Rejoin** or
  **Reconnect** whenever the host's side refuses them rather than simply not being there yet.
- **Evidence:** `Connect/ClientConnectionService.cs:131-137`: during a quiet rejoin try, any error before
  `Welcomed` (`net == null || !net.Lobby.View().Welcomed`) is logged and dropped. That covers:
  - the host already pressed Start: the room is closed (`LobbySession.Start` → `IO.CloseLobby`);
  - a *Multiplayer build mismatch* (the host updated the mod);
  - removed by the host, or the room full;
  - a direct-IP host that is up but refusing.

  `WatchRejoin` (`:212-257`) keeps its box saying *"Waiting for the host to host again. You join their Co-op Game
  page as soon as it opens."* and tries again every `RejoinEveryMs = 3000` (`:203`), for ever.

  For a Steam guest whose host's lobby is not visible, the `WaitForSteamInvite` case (`:247-249`) just keeps waiting.
  The text that told them to accept the host's invite (`ClientDesynced.WaitForSteamInvite`) is now unreachable (C11).
- **Scenario:** the host rehosts and presses **Start Game** after 30 s. A slower guest pressed Rejoin 40 s after
  that. The guest's box waits indefinitely while the host plays. Or the host installed a new build: the guest waits
  for ever with no hint of *build mismatch*.
- **Fix:**
  - Keep the silence only for "nobody there": connect refused or timed out with `net == null`.
  - When the host answered and refused (`net != null && (net.Lobby.View().Ended || an error string came back)`), stop
    the rejoin, close the box, and show the refusal as an ordinary join does: `ShowError(...)` or
    `page.ShowEndIfUnseen(net)`.
  - On the `WaitForSteamInvite` step, change the box's message once to add *"If the host's game doesn't show in Steam,
    accept their invite."*.
- **Check:** StabilityTests: the quiet branch in `TryToConnect` distinguishes `net == null` from a host's refusal
  (e.g. `quiet && net == null`), and `WatchRejoin`'s `WaitForSteamInvite` case sets a message.

### C6. The in-game **Join co-op game** button can no longer join anything
- **Status:** Confirmed. **Kind:** UI (a dead end). **Hits:** anyone pressing it in a game.
- **Evidence:**
  - `ClientConnectionUI.cs:90-99` still adds Join to the Esc menu. In a game it opens the address box (`ShowBox`).
  - Since rc4 every host is either in a waiting room or in a started game:
    - `ServerHostingUtils.cs:202-213`: a save is hosted only through `LobbyHostPanel.OpenForSave`, and the old
      in-game dialog is gone.
    - `LobbySession.cs:195-199`: joining closes at Start.
  - A join from a game to a room ends in `BeaverBuddies.Lobby.InGameInvite` (*"… return to the main menu, then …
    join from there"*, `ClientConnectionService.cs:529`). A join to a started game is refused.
- **Scenario:** a player in a solo game presses Esc → Join co-op game, types the host's IP, and is told to go to the
  main menu. There is no path where the in-game button works.
- **Fix:** Hide Join in a game (`if (!mainMenu) button.ToggleDisplayStyle(false)`). Or give it the rejoin treatment:
  go to the main menu and open the Join box there (a static `OpenJoinBoxInMenu` flag read by
  `ClientConnectionService` as `rejoinPending` is).
- **Check:** StabilityTests: `AddJoinButton` hides or reroutes the Join button when `!mainMenu`.

### C7. The Host co-op game box blanks the save's gold line after the player backs out of its Co-op Game page
- **Status:** Confirmed. **Kind:** UI/look. **Hits:** a host who opens a save's page and presses Cancel.
- **Evidence:**
  - `LobbyHostPanel.cs:201` pushes the page with `HideAndPush`. Popping it calls `PanelStack.ShowTop()`
    (`dec:Timberborn.CoreUI` 2825-2836, 2915-2922), which calls `GetPanel()` on the box again.
  - The postfix (`ServerHostingUtils.cs:51-52`) then runs `Dress`, which does `status.text = ""`
    (`HostCoopFlow.cs:140`).
  - The selection hasn't changed, so `SaveSelected` never runs and nothing puts the text back, although `read[key]`
    still has it.
- **Scenario:** Host co-op game → pick a shared save (*One shared colony. When you host it…*) → Host co-op game →
  Cancel. Back in the box, the same save is selected and the gold line under its picture is empty.
- **Fix:** In `Dress`, re-show instead of clearing: `Show(shownKey != null && read.TryGetValue(shownKey, out var i) ?
  i : null);`, keeping `status.text = ""` only when `shownKey == null`.
- **Check:** StabilityTests: `Dress`'s body shows `read[shownKey]` rather than assigning `""` unconditionally.

### C8. The mod's new buttons have no click sound and ignore modifier-clicks, unlike the native buttons beside them
- **Status:** Confirmed. **Kind:** UI/look (native feel). **Hits:**
  - since rc4: the main menu's and Esc menu's **Host co-op game** / **Save and Rehost**, and the box's
    **Host co-op game**, now the box's main button;
  - since rc3: the Esc **Found your own colony**;
  - older: the Join button.
- **Evidence:**
  - `Connect/ButtonInserter.cs:20-27` makes `new LocalizableButton()`, copies the classes and inserts it. It never
    calls `VisualElementInitializer.InitializeVisualElement`.
  - The native buttons get their sound from `UISoundInitializer` (`dec:Timberborn.CoreUI` 3652-3677), which registers
    the `ClickEvent` handler that plays `--click-sound` (set by `.menu-button`, CoreStyle 117). They get modifier-clicks
    from `ButtonClickabilityInitializer` (959-984).
  - Both run only through the initializer.
  - The nine-slice drawing is fine: `LocalizableButton` draws it itself (`dec` 1981-2015).
- **Scenario:** In the main menu, clicking Load game clicks audibly and clicking Host co-op game makes no sound.
  Shift-clicking the new buttons does nothing.
- **Fix:**
  - Have `ButtonInserter.DuplicateOrGetButton` take the `VisualElementInitializer` (all callers have one or can get
    `SingletonManager`'s) and initialise the new button once, after `init(button)`.
  - Create a `NineSliceButton` rather than a `LocalizableButton` with no key: `VisualElementLocalizer` throws for an
    `ILocalizableElement` whose key isn't set (`dec` 3829-3845).
- **Check:** RuntimeChecks IL: `ButtonInserter.DuplicateOrGetButton` calls `InitializeVisualElement` and constructs
  no `LocalizableButton`.

### C9. In custom difficulty the colony checkboxes no longer line up with the page's own checkboxes
- **Status:** Plausible (layout from UI.zip; a screenshot settles it). **Kind:** UI/look. **Hits:** every
  custom-difficulty New Game. It is automatic on multi-start maps: `MultiStartPatches.cs:334-376` call
  `OnCustomizeButtonClicked`.
- **Evidence:**
  - Custom mode hides the moved `TutorialToggleWrapper` (`TutorialToggleController.HideMainToggle`,
    `dec:Timberborn.MainMenuPanels` 1416-1420). It shows the game's second Tutorial row inside the list instead:
    `TutorialToggleCustomWrapper`, whose label is `new-game-mode-panel__setting-label`, `width: 300px`
    (`Views/MainMenu/NewGameModePanel.uxml`; MainMenuMiscStyle 450-455).
  - The list's rows (Tutorial, Enable droughts, Enable badtides) are left-aligned in a list about 550 px wide.
  - The mod's column is centred as a block above the list (`alignSelf = Center`, `NewGameColonyOptions.cs:181`, in
    `.new-game-mode-panel__mode-details { align-items: center }`). Its checkboxes sit about 150 px to the right of the
    list's checkboxes.
  - The class comment's promise that every checkbox lines up (`NewGameColonyOptions.cs:44-46`) holds only in the
    predefined modes.
- **Scenario:** New Game → a multi-start map. The custom list opens, headed by *Separate colonies* and the rows under
  it, centred, over a left-aligned list whose first row is *Tutorial*.
- **Fix:** In custom mode, align the column with the list: `alignSelf = FlexStart`, with `marginLeft` equal to the
  scroll view's content left. Or move the column into `CustomModeSettings`' content container, right after
  `TutorialToggleCustomWrapper`, whenever custom mode is shown: a postfix on `OnCustomizeButtonClicked` and on
  `OnPredefinedModeButtonClicked` re-parents it. Script S step 2 should then say "under Tutorial, inside the list".
- **Check:** a screenshot line in Script S step 2 ("the colony checkboxes' boxes are in line with Tutorial's in the
  list").

### C10. Strings that say something the code doesn't, or read badly (rc2 to rc4)
- **Status:** Confirmed. **Kind:** UI text. **Hits:** everyone who reads them.
- **Evidence and fixes:**
  1. `ClientDesynced.Message` was rewritten in rc4 but still ends *"Before reloading, would you like to file a bug
     report…?"*.
     - Public builds have no report button: `DesyncDialogPlan.ReportButtonKey` returns null without a token
       (`DesyncDialogPlan.cs:79-84`).
     - So the question is answered by **No** / **Reconnect (wait for Rehost)** (`ConnectionEvents.cs:276-277`).
     - Fix: move that sentence into a separate key that is appended only when `bugReportMessageKey != null`, as
       `NeedToEnableTracing` is.
  2. `ClientDesynced.FailedToRehostMessage` (*"Failed to Rehost. Manually save and Host again."*) is shown for the
     solo **Host co-op game** too (`ClientConnectionUI.cs:128`) and names the old flow.
     - When `SaveRehostFile` refuses for `HasReplayFailure`, it has already shown its own dialog
       (`RehostingService.cs:65-70`), so two dialogs stack.
     - Fix: new text *"The game could not be saved for hosting. Save it with Save game, then use Host co-op game in
       the main menu."*, shown only when `SaveRehostFile` returned false without having shown its own box.
  3. `Colony.Overview.AwayNoSameFaction` says *"no colony of its faction is in the game"*. The condition is
     `lifecycle.AbsenceReceiver(slot, present) == null` (`TradeOverviewPanel.cs:576`): no *present* living colony of
     its faction (`ColonyHandover.cs:181`). A same-faction colony whose player is also away makes the text false.
     Fix: *"…; not handed over: no colony of its faction is being played"*.
  4. `Colony.Overview.HandToOtherFactionTooltip`: `{0}`/`{1}` are player names (`ColonyExchangeService.ColonyName`,
     `TradingPostExchange.cs:1095-1099`). That gives *"Kyler and Alex are different factions. Alex can run what it
     receives…"*. Fix: *"{0}'s colony and {1}'s play different factions. {1}'s colony can run what it receives, but
     builds only its own faction's buildings…"*.
  5. `Rejoin.Waiting` *"Waiting for the host to host again."* Fix: *"Waiting for the host's Co-op Game page. You join
     it as soon as it opens."*.
  6. `Lobby.Colonies.SharedToSeparate` is also shown on the host's own page (`LobbyHostPanel.cs:234-235`) and reads
     *"…everything built so far is the host's colony"* to the host. Fix: a host variant (*"…is your colony"*), or
     neutral wording.
  7. The hard-coded lost-connection text (`Connect/SessionEndMessages.cs:17-19`) says the host comes back through
     *"(Save and Rehost)"*. After a host crash or quit, the host comes back with the main menu's Host co-op game, and
     the rejoin waits for either. Fix: *"…as soon as the host hosts this game again."*.
  8. The tone of the New Game page, room notes, box status, split dialog and hand-over texts is otherwise consistent
     and accurate (section 2).
- **Check:** StabilityTests CSV checks for items 1 to 6, e.g. `ClientDesynced.Message` must not contain "bug report",
  and a new `ClientDesynced.ReportQuestion` key exists and is appended only in the report-button branch.

### C11. Dead code and dead strings left by rc3 and rc4
- **Status:** Confirmed. **Kind:** dead code. **Hits:** maintainers; also the reason for C5's missing Steam hint.
- **Evidence:**
  - `ClientConnectionService.ReconnectNow` and `ShowWaitForSteamInvite` (`:276-309`) run only when
    `LobbyGuestPanel` exists.
    - That panel is bound in the main menu only (`Plugin.cs:127`), and both callers of `Reconnect()` are in a game:
      the desync dialog (`ConnectionEvents.cs:254`) and `RejoinFromGame` from `EndSession`.
    - So `ClientDesynced.WaitForSteamInvite`, edited in rc4, is never shown.
  - `HostCoopFlow.PendingRehost` (`HostCoopFlow.cs:29,35,45`) is written but never read.
  - `BeaverBuddies.Host.ConnectedClients` and `BeaverBuddies.Host.DirectConnectClient` (CSV 80-82) lost their only
    user, the removed in-game hosting dialog.
  - Every rc4 session closes joining at the room's Start (`LobbySession.cs:199`), and a `ServerEventIO` is made only
    for a room. So `ColonyRules.WaitsForStart(...)` is always false for rc4 hosts. That makes these unreachable:
    - `Colony.Founding.NotStartedYet`;
    - the "Not before the game starts" refusal;
    - `SharedColonySplit.Ask`'s `SplitCanBeginNow` wait (`ColonyFoundingService.cs:237-242`).
- **Fix:**
  - Delete `ReconnectNow`, `ShowWaitForSteamInvite` and `PendingRehost`, or use `PendingRehost` in `HostCoopMenu`,
    e.g. to title the page "Rehost".
  - Move the Steam hint into the rejoin box (C5).
  - Remove the two orphaned keys.
  - Keep `WaitsForStart`, but mark it "only an older host" in its comment, or remove it together with its texts.
- **Check:** a RuntimeChecks IL scan: no method of `ClientConnectionService` is unreachable from its public API (or
  simply assert `ReconnectNow` is gone), and a CSV check that every `BeaverBuddies.Host.*` key is referenced.

### C12. Every waiting room gained a 6 px gap below the gold note, even with no checkboxes
- **Status:** Confirmed. **Kind:** look (nit). **Hits:** every room.
- **Evidence:** `Lobby/LobbyPage.cs:124-128`: `optionsSlot` is always added with `marginBottom = 6` and stays
  displayed when empty (`SetColonyOptions(null)` only clears it). Before rc4 the note sat directly on the faction
  slot and list title.
- **Fix:** `optionsSlot.style.display = options == null ? None : Flex` in `SetColonyOptions`.
- **Check:** StabilityTests: `SetColonyOptions` toggles `optionsSlot`'s display.

---

## 2. Found sound (checked; no need to redo)

- **Tooltips in the main menu work.**
  - `dec:Timberborn.TooltipSystem` 720-731: `TooltipSystemConfigurator` is `[Context("MainMenu")]` and binds
    `ITooltipRegistrar`, `Tooltip` and `TooltipContainer`. The container is created on its own root layer
    (`RootVisualElementProvider.Create(..., 3)`, 440).
  - The game's own `MainMenuPanel` registers a tooltip in the main menu (`dec:Timberborn.MainMenuPanels` 716).
  - So the Game Mode page's tooltips and the room's tooltips all show.
- **The greyed Mixed factions row still shows its tooltip.** It is registered on the row (`NewGameColonyOptions.cs:128`),
  which stays enabled. Only the toggle is disabled, and `Tooltip.RegisterTooltip` enables on the row's own
  `MouseEnterEvent` (`dec` 302-323). The label's 0.5 opacity matches Unity's `.unity-disabled` look on the toggle.
- **Moving the Tutorial row is safe.**
  - `TutorialToggleController.Initialize` queries by name once, at `NewGameModePanel.Load`, before the move
    (`dec:MainMenuPanels` 1241, 1392-1402), and keeps element references.
  - `SetFaction`, `Show/HideMainToggle` and `UpdateTogglesVisibility` use those references.
  - `CustomNewGameModeController.Initialize` and `MultiStartPatches` query only inside `CustomModeSettings`.
  - Iron Teeth (no `StartingFactionSpec`) hides both Tutorial rows, and the column still shows its own rows.
- **Build is idempotent, and the click sounds are right.**
  - `NewGameModePanel` and `NewGameColonyOptions` are both main-menu singletons, and each scene has its own `_root`.
    `Attach` builds once per scene (`block == null || root.Q(BlockName) == null`) and then only refreshes.
  - Each new row is initialised alone (`CheckboxRow` → `InitializeVisualElement(row)`). The column, which holds the
    already-initialised Tutorial row, is never initialised, so there is no second click sound.
  - Attach runs in the same postfix, after the Host button.
- **The Game Mode page's height is fine in the predefined modes.**
  - The page is 720 px (`.new-game-panel`, MainMenuMiscStyle 213-215). The logo takes 104 px, the spacer 10, the
    padding 2×25 and the summary 76, which leaves about 480 px for ModeDetails.
  - Description (≈100) + Customize (56) + four rows × 31 gives about 280 px.
  - In custom mode, the list (`flex-grow: 1`, scrolls) absorbs the column's 93 px.
- **The room's options slot is correct.**
  - The classes resolve in the main menu (`TitleScreen.uxml` and `NewGameTemplate.uxml` attach MainMenuMiscStyle).
  - The indent is 28 = the 25 px checkmark + 3 px padding (MainMenuMiscStyle 463-472).
  - The rows are initialised one by one, and the slot is not in the constructor's initialiser list.
  - Every note is at most about 115 characters at 13 px in a 600 px `maxWidth`, so it takes 2 lines at most. The
    board shrinks (`flexShrink 1`) and nothing overlaps.
  - `SetEnabled(false)` greys the checkboxes at Start. The guests' notes follow `SeparateAtStart` (a version bump in
    `LobbyRoom.SetSeparateAtStart`).
- **The Host co-op game box's status line is laid out correctly.**
  - `.text--yellow` (CommonStyle 54) beats `.game-text-small`'s colour (CommonStyle 1) because it comes later in the
    same sheet.
  - The longest texts (71 and 62 characters at 12 px) wrap to 2 lines inside the 300 px `maxWidth`.
  - The 30 px `minHeight` keeps the list still while a save is read. The list gives up about 36 of about 285 px.
  - The Mods button is absolute inside the thumbnail, so it can't overlap. The Mods box is a dialog, with no
    `ShowTop`, so it has no C7 effect.
  - Delete keeps working and updates the line.
- **The box's title, Load/Host swap, Enter and double-click, and the in-game hiding all work.**
  - The header is `NamedBoxTemplate`'s `Header` label, and its game title is captured before the first change.
  - `LoadGameBox.Open` → `HideAndPushOverlay` → `GetPanel` → `Dress` (`dec` 559-563).
  - Enter and a double-click go through `LoadGame` (`dec` 587), which the prefix routes to hosting.
  - In a game `menu == null`, so Host is hidden.
- **Esc menu order and widths are fine.** The order is Load game, Host co-op game / Save and Rehost, Join co-op game,
  Key bindings, Settings, Player cursors, Found your own colony, and so on. Whichever of the two last buttons is added
  first, Found your own colony lands under Player cursors. The longest label, *Found your own colony* (21
  characters, 14 px bold), fits in `menu-button`'s 245 px minimum less 40 px of padding. The Esc box has room on a
  720 px screen: 12 rows is 676 px.
- **The dialogs are fine.**
  - `DialogBox` wraps (`.box__text { white-space: normal }`, `.box { max-width: 650px }`, CoreStyle 209-219). The
    `\n\n` in the CSV renders as blank lines, as older keys do.
  - `SetDefaultCancelButton()` labels the cancel button **No** (`dec:Timberborn.CoreUI` 1186, 1261).
  - The lost-connection dialog's Esc is **Stay here** (`DialogBox.OnUICancelled` → cancel callback).
- **The strings check out mechanically.**
  - Every key the rc2 to rc4 code uses exists in the English CSV. The dynamic ones are `Saving.Status.*.One`,
    `LobbyRules.KeyPrefix + "Colonies.*"` and `"Faction.MixedSave"`.
  - Placeholders match: `MixedFactions.Locked {0}`, `SplitHost/SplitOther {0}` (FoundingNotice),
    `Saving.Status.Separate {0}` (an extra argument to the keys without one is harmless), `AwayNoSameFaction {0} {1}`
    and `HandToOtherFactionTooltip {0} {1}`.
  - The one Mod Settings tooltip that changed, `AbandonedColonyDays`, has lines of 103 and 105 characters, within
    the 112 rule.
  - Other languages fall back to English for the new keys (`dec:Timberborn.Localization` 596-628). The 14
    translations of `ClientDesynced.Message` keep the older, still roughly right "host rehosts, then reconnect".
- **The texts match the behaviour.**
  - The split dialog says science stays shared, and it does: `Found` → `Enable(..., separateScience: false)`.
  - *Separate colonies: N players* counts the slot table's rows, and helpers are not recorded
    (`ColonySlotTable.Resolve`), so N is the players who ever held a colony slot, host included. An empty table reads
    as *only yours so far*.
  - Script H step 4's log line matches `SaveConversion.cs:76` and `ColonyModeService.cs:150`.

---

## 3. Docs

1. **The public website is still rc2's.**
   - `docs/*.html` did not change in rc3 or rc4. Pages is served from `origin/main:/docs`, and `origin/main` contains
     rc4.
   - `docs/install.html`'s "Host a game" section tells hosts to:
     - use **Separate colonies for new games (beta)** and **Allow founding colonies in a shared game (beta)** in Mod
       Settings (both removed in rc3);
     - host a save with **Load Game → Host co-op game**;
     - use "**Options → Load Game → Host co-op game, or Save and Rehost**, works without a waiting room … choose Start
       Game, then wait, paused … The game asks before your first change", which is the removed dialog and the retired
       prompt.
   - "Found your colony: … hosted from inside a game, once the host unpauses" is no longer possible.
   - `docs/index.html:211` and `:304` and `docs/troubleshooting.html:170` ("the host builds nothing until everyone is
     in") repeat the old flow.
   - 14 stale mentions in all (`grep -i "Separate colonies for new games\|Allow founding\|Load Game"`).
2. **Hosting a game played alone (C1).** These describe a button that is hidden in single player:
   - README:121-122 (step 4);
   - TWO-COLONIES:85-89 ("From inside a game, … (playing alone)");
   - `changelog.txt` rc4 bullet 2 (shown in game);
   - ALPHA-TEST-SCRIPTS Script H step 6.

   They become right once C1 is fixed.
3. **"The save's faction ring" is gone since beta22**, but these still mention it:
   - README:113;
   - TWO-COLONIES:94 (rewritten in rc4 with the phrase kept);
   - Script F 12 ("the ring shows Folktails");
   - the `LobbyPage.cs:17` class comment ("plate with the faction's logo ring").

   `LobbyPage.cs:96-97` says the ring beside the plate was removed. The rows carry the faction logo.
4. **Older ALPHA-TEST-SCRIPTS steps that rc4 made unreachable.** Every game now starts from a waiting room, where
   joining closes at Start:
   - Script B intro, line 151: "The friend joins over a Steam invite before the host unpauses".
   - Script B step 2, lines 158-159: the offer comes "once the host unpauses … (Before that, Ctrl+K says the game has
     not started yet.)". In rc4 the offer comes at once, as TWO-COLONIES:110-112 says.
   - Script B 8h, lines 318-320: "before the host unpauses, the friend places a path: refused with *Not before the
     game starts*".
   - Script C step 1, lines 345-348: players join "while the host waits paused", a "Ctrl+K before the host unpauses"
     notice, and "player 3 tries to join (refused: the game was changed)".
   - Script C 1a, lines 349-352: two guests join by IP and receive the save "while the host waits paused". In rc4 they
     join the room, and the save goes to everyone at Start.
   - Script P, P2, lines 672-674: "Host the late save … with nobody joined … Then, in the same game, have a guest join
     and split it". Nobody can join a started game. This needs a Save and Rehost with the guest, and it is then no
     longer "nobody joined".
5. **Script S step 7** says "**Cancel**: nothing changes". The split dialog's second button is **No**
   (`SharedColonySplit.cs:89` `SetDefaultCancelButton()` → `Core.No`). The same applies to the Host co-op game and
   Save and Rehost confirms (Script H steps 6 and 8 don't name it).
6. **Script D 15** (edited in rc4) still says "no faction logo, and no colony numbers on the rows". Since beta20 a
   save's rows show each player's colony and faction logo: README:112-113 and Script F 12 (*Host · Colony 1* with the
   Folktails logo).
7. **"Load game only loads"** (README:125, TWO-COLONIES:89) is false in any game started from the Host co-op game box
   until C2 is fixed. Script H has no step that would catch it. Add a step after step 4: "Esc → Load game → Load
   loads".
8. **Script H step 7** ("the guest's has neither") holds only while connected (C4). Add: "after the connection is
   lost and the guest chooses Stay here, the guest's Esc menu has no Host co-op game".
