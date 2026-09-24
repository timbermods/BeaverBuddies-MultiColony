> Reviewer A's report from the review of 1.4.0-beta18 to beta20 (see [the findings](../REVIEW-FINDINGS-1.4.0-beta18-20.md)).
> `$SP` was that session's scratchpad (the frozen beta20 source in `$SP/base`, rigs and scripts); it was not kept.
> Line numbers are beta20's. The fixes it proposes shipped in 1.4.0-beta21, except where the findings say otherwise.

# Reviewer A: the join and the waiting room (beta18–beta20)

Leads: E4, E5, J1–J11, and X4's wire part. Base is `$SP/base` (tag `v1.4.0-beta20`). Nothing has been played. Each finding
says whether it was proved by tracing, by a script, or by a rig.

## Where the work is (`$SP/revA`)

| Path | What |
|---|---|
| `ReviewAChecks.cs` (and a copy in `patches/`) | 11 StabilityTests checks. They compile against beta20's API, so the same file runs on both trees. On base, 4 fail; each failure is a confirmed finding below (`results-base.txt`). Every check stays under 5 s. |
| `ReviewAFixChecks.cs` | 3 checks of the pure rules the fix adds (`JoinFlowRules`). They run only on the fixed tree. |
| `src/` | Base plus `ReviewAChecks`. `dotnet run --project StabilityTests -- --only ReviewA` gives 7/11. |
| `fixed/` | Base plus every fix below, plus both check files. The **full StabilityTests suite passes 405/405** (391 + 11 + 3), and `LobbyChecks` is unchanged. The mod compiles with `-c Release`: no CS errors or warnings. Only the resource copy fails, and only because the scratchpad path is too long for Windows (MSB3030, not code). RuntimeChecks was not run, for the same reason. |
| `patches/all-fixes.diff` | A unified diff against `$SP/base` (`a/`, `b/` paths) of every fix, 622 lines. The findings below point to its hunks. |
| `wire/Program.cs`, `wire17/`, `wire20/`, `dump17.txt`, `dump20.txt` | The E4 byte comparison: one scripted classic session, built against beta17's TimberNet and against beta20's. |
| `results-base.txt`, `results-fixed.txt` | The ReviewA check output on each tree. |

To port the checks: add `ReviewAChecks.cs` and `ReviewAFixChecks.cs` to `StabilityTests/`, link
`BeaverBuddies/Connect/JoinFlowRules.cs` in the csproj, and concat both in `Program.cs`. The hunks at the end of the
diff do this.

---

## Findings, by who they hit

### Everyone (classic joins too)

### A-new-1: "Connecting to …" returns late, over the waiting room's page or after a join has already failed
- **Status:** Confirmed by trace (`PanelStack.Push`/`Hide` in `$SP/dec/Timberborn.CoreUI.decompiled.cs` around :2890–2912; `EventBus.Post` is synchronous once ready). The pure rule is checked by `ReviewAFixChecks` "A-new-1".
- **Severity:** display
- **Hits:** everyone who accepts an invite through the Steam overlay. The waiting-room case is the common one.
- **Evidence:**
  - `Steam/SteamOverlayConnectionService.cs:196-202`: with the overlay open, `WaitForSteamOverlayToClose(success)` registers for `PanelHiddenEvent`.
  - `:153-158` `OnPanelHidden` then calls `ShowConnectionMessage(true, …)` on the next panel hidden, whatever panel that is.
  - `Connect/ClientConnectionService.cs:228-244` `ShowConnecting` pushes a box without asking whether anything is still connecting.
- **What happens:**
  - **(a) Waiting room.** The guest accepts in the overlay. The welcome arrives while the overlay is still open. `LobbyGuestPanel.Open` then runs `HideAndPush(page)` (`LobbyGuestPanel.cs:164`), which hides the overlay's input blocker and posts `PanelHiddenEvent` synchronously. `OnPanelHidden` pushes a "Connecting to Kyler…" dialog in the middle of that `HideAndPush`, and the page goes on top of it. When the page later pops (Leave, the room ends, Esc), the stale Connecting box resurfaces, and its Cancel is the only way past it.
  - **(b) A failed join, any host.** The join fails while the overlay is open, and the error dialog goes over the blocker. When the player dismisses the error, `PanelHiddenEvent` fires, and "Connecting to …" appears for a dead join. beta17 showed "Joined! Receiving map..." here, which was also wrong but was only an OK box.
- **Fix:** `ShowConnecting` shows the box only while a join is under way and not yet welcomed: `JoinFlowRules.ShowConnectingBox(joining, welcomed)`. See the diff, ClientConnectionService hunk `@@ -230,6 +239,10 @@`, and `JoinFlowRules.cs`. Wire and saves: none.
- **Check to add:** `ReviewAFixChecks` "A-new-1" (the rule). The panel sequence itself needs the game.
- **Test script:** D4b. Accept the invite from the Steam overlay and leave the overlay open until the Player 1's Game page shows, then close it. No "Connecting to …" box sits over the page, and Leave returns straight to the main menu.

### J5a: the start message carries the previous session's speed boost
- **Status:** Confirmed by trace.
- **Severity:** gameplay (speed only, not a desync)
- **Hits:** every waiting room hosted after a boosted co-op session in the same game process. The same happens to a classic in-game **Save and Rehost** while a boost is on, which is pre-existing since beta5.
- **Evidence:**
  - `Events/ConnectionEvents.cs:85` `speedBoost = ReplayService.SessionBoost`.
  - `ReplayService.cs:157-158, 232`: the static is set to 0 only in ReplayService's constructor, which runs when the host's co-op scene loads.
  - The start message is built on each guest's join thread (`TimberServer.cs:495-502`) after its paced save. For a room, that is before or during the host's reload (`LobbySession.Update` :252-261). For a rehost, it is while the host still sits in the old game's dialog (`ServerHostingUtils.LoadAndHost` :160-208).
- **What happens:**
  1. Guests replay `InitializeClientEvent`, then `SetBoost(stale)`. The host's new game starts at 0.
  2. With several guests, the start messages of the others are also queued behind a guest's own (`SendEventToClients`). So each guest ends with whichever value it replayed last, and guests can disagree with each other too.
  3. A guest runs its chosen speed plus the stale boost against a host running without it. It stops and goes on heartbeats until someone changes the boost.
- **Fix:** `ReplayService.ResetSessionBoost()`, called in `ServerEventIO.StartServer` next to `ColonySession.BeginHostSession()`. See the diff, ServerEventIO `@@ -87,6 +87,9 @@` and ReplayService `@@ -156,6 +156,8 @@`. A new session's boost is 0 in every path. Wire and saves: none.
- **Check to add:** a FeatureChecks-style check that `ServerEventIO.StartServer` calls `ResetSessionBoost`, by a source scan like LobbyChecks' string check. The code runs only with the game.
- **Test script:** D7b. Before the room, play a co-op session with a speed boost, quit to the main menu, then host the waiting room. After Start, the guest's speed shows no boost. Also Script C: boost on, **Save and Rehost**, then the guest shows no boost after the reconnect.

### Waiting room

### J1: a waiting-room guest never loads the world, and is left on a blank main menu
- **Status:** Confirmed by trace, by a premise rig and by a source-scan check.
  - "ReviewA J1 premise" shows the guest is still `Welcomed` in the frame its save is handed over.
  - "ReviewA J1" fails on base.
- **Severity:** session-stopper. The guest has to restart the game.
- **Hits:** every waiting-room game with a guest (new games and save rooms), in beta18, beta19 and beta20.
- **Evidence (as the plan's §0 says, plus the ending):**
  - `ClientConnectionService.cs:329` `SingletonManager.Reset()`, then :346-347 `RegisteredLocalizationService.T(...)` → NullReferenceException (`Util/RegisteredLocalizationService.cs:19`). `NotifyEach` catches it and queues "could not be loaded" (`TimberNetBase.cs:722-726`).
  - In the same `UpdateSingleton` (:360-361), `CheckWaitingRoom` (:372-380) finds `Welcomed`, and `GetSingleton<LobbyGuestPanel>()` is null. It takes the D20 branch: `client = null`, `EventIO.ResetIf(joining)`, which closes the connection. The queued error is never pumped, and the D20 box's `T()` throws inside `ShowSafely`.
  - **New:** on that frame or the next, `LobbyGuestPanel.Pump` sees `EventIO.Get() != io` (`LobbyGuestPanel.cs:108-111`), then `Close()` → `PanelStack.Pop(page)` → `ShowTop()` → `MainMenuPanel.GetPanel()`. The mod's postfix `MainMenuGetPanelPatcher` (`Connect/ClientConnectionUI.cs:22`) calls `SingletonManager.GetSingleton<ClientConnectionUI>().AddJoinButton` and throws: the registry is empty.
    - `ShowTop` has already popped the main menu panel (`_stack.Pop()`) and never pushes it back.
    - `Hide` has removed the stack's input processor.
    - Result: no panel and no input. The guest sees an empty main menu background, and the host sees the guest drop.
- **Nothing else reads the emptied registry on the guest's way into the game.** Checked:
  - every main-menu singleton the mod binds (`Plugin.cs:101-117`): only ClientConnectionService, LobbyGuestPanel, SteamOverlayConnectionService (`ReleaseSurfacedBlocker` → `Pop` → `ShowTop` → the same postfix, if a blocker surfaces in that frame) and the two `GetPanel` postfixes can run in those frames;
  - the game-scene side: `ReplayConfigurator` resets the registry again and rebinds before anything reads it, as for a classic join.

  So the fix must also stop `CheckWaitingRoom`, and must make the reset the last thing before the load.
- **Fix (three parts; diff hunks ClientConnectionService `@@ -120,7`, `@@ -323,10`, `@@ -339,16`, `@@ -368,11`, ClientConnectionUI `@@ -19,7`/`@@ -28,7`, and `JoinFlowRules.cs`):**
  1. `LoadMap`: set `saveReceived = true` first, write the file, seed, and word the tip. Only then run `SingletonManager.Reset()`, and immediately after it `LoadScene`/`StartSaveGame`. The tip is in a try/catch that falls back to `StartSaveGame`. A failure while writing the file (disk full, say) now leaves the menu's singletons whole, so the join's error dialog and the page's close both work.
  2. `CheckWaitingRoom` asks `JoinFlowRules.CheckWaitingRoom(connected, welcomed, saveReceived, pageBound)`, which returns `Nothing` once the save has come. `saveReceived` is an instance field that LoadMap sets on the game thread before CheckWaitingRoom runs, so there is no race with the receive thread. `TimberClient.saveArrived` is written after `mapBytes` is published (`TimberNetBase.cs:544-545`), so reading it could miss the frame; don't use it.
  3. `SingletonManager.GetSingleton<ClientConnectionUI>()?.AddJoinButton(...)` in both `GetPanel` postfixes, so that no panel shown during a reset can empty the menu.
  - **D20 still works:** a guest in a game has `saveReceived` false and no page bound, so it gets `LeaveForGame`. **Classic joins:** never welcomed, so `Nothing`, and LoadMap takes `StartSaveGame` as before. Wire and saves: none.
- **Check to add:**
  - `ReviewAChecks` "J1", a source scan: LoadMap reads no registry after `SingletonManager.Reset()`; CheckWaitingRoom has a saved/delivered guard; the `GetPanel` postfixes are null-safe.
  - `ReviewAFixChecks` "J1" (the rule table, including D20 and classic).
  - "J1 premise" (TimberNet).
- **Test script:** D8 is the line that fails today. Add D8a: the guest's loading screen says "Loading Player 1's co-op game…". The guest arrives in the game paused at tick 0, and its `Player.log` has no "Ignoring an error in a handler for the received save".

### A-new-3: a guest the room has let go is read ungated until its connection closes, so a SessionFault it sends kills the co-op game at load
- **Status:** Confirmed by rig: "ReviewA A-new-3" on base reports "1 action(s) as player -1, 1 session fault(s)".
- **Severity:** session-stopper (a hostile or modified client only)
- **Hits:** waiting room. Anyone who can reach the TCP port can come in (known limit), and a host naturally removes an unknown "Player".
- **Evidence:**
  - `TimberServer.RemoveFromLobby` :238 → `ForgetLobbyMember` :289 drops the stream from `lobbyMembers` at once.
  - The stream is closed only after `member.Lane?.WaitUntilEmpty(AbortFlushMs)` (:240-244, up to 2 s). The member's receive thread, which has run since admission (:491), keeps reading all that time.
  - `IsInWaitingRoom` (:307-308) is now false, so `TimberNetBase.ReceiveMessages` takes the game path (:535-598):
    - a `SessionFault` frame goes into `sessionFaults` (:588-591);
    - an action is stamped `-1` (:426) and queued.
  - Nothing clears those queues, and the same `TimberServer` becomes the co-op game's server. At its first `Update` (`ReplayService.UpdateSingleton` → `io.Update()`), `OnSessionFault` calls `ReplayService.AbortReplay`, which ends the host's game for everyone.
  - In a shared game every action is allowed (`ColonyRulesService.cs:181`), so a queued action is also played at tick 0.
  - The same window opens in `CancelLobby` (harmless: the server closes) and in the 10 s straggler removal (J4).
- **Fix (one line):**
  ```diff
  -        protected override bool IsInWaitingRoom(ISocketStream source) =>
  -            lobbyMembers.TryGetValue(source, out LobbyMember? member) && !member.inGame;
  +        protected override bool IsInWaitingRoom(ISocketStream source) =>
  +            lobby != null && !(lobbyMembers.TryGetValue(source, out LobbyMember? member) && member.inGame);
  ```
  In lobby mode every connection that is read was admitted (its reader starts at `AdmitToLobby`; `TimberServer.cs:486-492, 507`). It stays gated until it enters the game, and a let-go member's lobby frames are already ignored by `HandleLobbyFrame`. Classic mode is unchanged (`lobby == null`). Wire and saves: none.
- **Check to add:** `ReviewAChecks` "A-new-3". A stalled guest is removed, then sends an action and a fault; neither may reach `ReadEvents` or `OnSessionFault`.
- **Test script:** none; it needs a hostile client. It is a rig check only.

### J2/J4: a guest taken out as its join reaches StartQueuing is let in as a new player (and the reverse race)
- **Status:** Confirmed by a deterministic rig. "ReviewA J2/J4" holds the `queuedMessages` lock by reflection, calls `RemoveFromLobby` in the gap, and on base gets "admitted as new player 2".
- **Severity:** broken feature (low; needs a straggler at the 10 s mark)
- **Hits:** waiting room
- **Evidence:**
  - `SendMap` checks membership (:795), then `StartQueuing` looks it up again (:529).
  - In between, `RemoveFromLobby` (from `LobbySession.Update` :246-250) forgets the member. Then `fromWaitingRoom` is false, but `IsAcceptingClients` is still true: a room never sets `errorMessage`, because `ServerEventIO.CloseLobby` latches only `stoppedAccepting` (:67-81, :206).
  - So the guest is numbered afresh (`lastPlayerId`++) and added to `clients`, and the removal's task then closes its stream within `AbortFlushMs`.
  - The reverse order is also possible: `StartQueuing` finds the member and `RemoveFromLobby` checks `inGame` before it is set. The member is then forgotten under the join (its `playerIds` entry removed, so it plays as -1) and closed right after entering the game.
  - Either way the guest is dropped with a connection error instead of the room's "Your connection did not take the world in time". Nobody in the game misses a tick-0 action, because the host starts loading only after this.
  - **J2 otherwise refuted:** in lobby mode every connection passes `AdmitToLobby` (:490), which refuses newcomers once the room is closed (`LobbyRoom.RefusalForNewcomer`), for the whole game, since `lobby` is never cleared. This race is the only way a non-member reaches `StartQueuing`.
- **Fix:** an atomic claim on `LobbyMember` (`fate`: waiting, in the game, gone; `TryEnterGame`/`TryLeave` by `CompareExchange`).
  - `StartQueuing` admits a lobby-mode connection only if `TryEnterGame()` wins, and never through `IsAcceptingClients` in lobby mode. A refused leaver is closed without an error frame (there is no `errorMessage`; it was told by `LobbyEnd`).
  - `RemoveFromLobby`, `LeaveLobby` and `CancelLobby` claim with `TryLeave()`.
  - See the diff, LobbyRoom `@@ -34,6` and TimberServer `@@ -213`, `@@ -234`, `@@ -282`, `@@ -299`, `@@ -526`, `@@ -541`. Wire and saves: none.
  - Also suggested: in `LobbySession.Update` :248-249, log "leaving it out" only when `RemoveFromLobby` returns true.
- **Check to add:** `ReviewAChecks` "J2/J4".
- **Test script:** D7c. A guest on a throttled link (about 100 KB/s) with a large save: the host's log shows each guest's number once. No new player number appears after "was not ready to receive the world".

### J10a: one waiting guest's frames make the room write the roster to everyone thousands of times a second, and slower guests are taken out
- **Status:** Confirmed by rig and measured.
  - Base: 32,411 ready toggles in 1 s made the host write **3.4 MB/s** to each other guest; the fixed tree wrote 3 KB/s.
  - "ReviewA J10 flood" (a guest draining 20 KB/s, the queue limit scaled to 100 KB) is taken out on base on every run and stays on the fixed tree.
- **Severity:** broken feature (a hostile or modified client). It is also CPU on the host's pool.
- **Hits:** waiting room
- **Evidence:**
  - `HandleLobbyFrame` (:310-323) calls `RequestLobbyPump` for every hello and every ready, changed or not.
  - `LobbyRoom.SetHello` (:222-231) has no once-only guard.
  - `RequestLobbyPump` calls `Timer.Change(0, …)` (:339), which queues a pool callback each time, and `PumpLobby` loops while requests keep coming (:351-355).
  - Every pass posts the roster and state to every lane. A lane over `MaxLobbyQueuedBytes` (1 MB) is taken out as stalled (:373-377). At 3.4 MB/s, a guest on a 10 Mbit link fills that in about half a second.
- **Fix:**
  - `SetHello` is taken once; `SetReady` and `SetHello` return whether anything changed; only a change requests a pump.
  - The writer makes at most one pass every `LobbyPumpGapMs` (50 ms), and `RequestLobbyPump` schedules at the gap instead of 0. Keep-alives and `ReleaseLobby`'s direct pass are unchanged.
  - See the diff, LobbyRoom `@@ -219`, TimberServer `@@ -299` (HandleLobbyFrame), `@@ -332` and `@@ -348`. All existing LobbyChecks pass. Wire and saves: none; the frames are the same, just fewer.
- **Check to add:** `ReviewAChecks` "J10: a waiting guest flooding hellos cannot get a slower guest taken out".
- **Test script:** none (hostile).

### J8a: a welcome and an end read in the same frame: the guest is told nothing and keeps "Connecting to …"
- **Status:** Confirmed by trace. The "ReviewA J8 premise" rig shows a guest whose inbox is both Welcomed and Ended when the connection drops.
- **Severity:** display (rare: the room must end within about one frame of the guest's welcome)
- **Hits:** waiting room
- **Evidence:**
  - The page opens only on `Welcomed && !Ended` (`LobbyGuestPanel.cs:96`).
  - `ClientEventIO.OnConnectionError` suppresses the join's error whenever the room `Ended` (:98, :109).
  - The Connecting box is closed only by the page's `Open` or by the error lambda, so it stays with Cancel and no reason. The likeliest way: the host confirms Cancel while a new guest is arriving.
- **Fix:**
  - `ClientEventIO` hands the connection to its error callback (`Action<string, TimberClient>`) instead of deciding.
  - The callback closes the Connecting box. If the room ended, it calls `LobbyGuestPanel.ShowEndIfUnseen(net)`, which shows the same end box unless the page is showing, or has shown, that room's end (`JoinFlowRules.ReportJoinError`). Otherwise it shows the join error as before.
  - `ShowEnd` is made idempotent per connection.
  - See the diff, ClientEventIO (all hunks), ClientConnectionService `@@ -129,6`, and LobbyGuestPanel `@@ -262`/`@@ -272`. Wire and saves: none.
- **Check to add:** `ReviewAFixChecks` "J8" (the rule) and "J8 premise" (TimberNet).
- **Test script:** D11b. Host: open the Cancel confirm, and while it is up, have a guest join by IP; then confirm. The guest gets "Kyler closed the waiting room", not a Connecting box.

### J8b: if the world-making scene stops updating, nothing times out and the guest's watchdog can never fire
- **Status:** Confirmed by trace.
- **Severity:** display (the host's game is broken anyway; the guest can still press Leave)
- **Hits:** waiting room
- **Evidence:**
  - `CreateTimeoutMs` is checked only in `LobbySession.Update` (:235), which runs from `LobbyWorldMaker.UpdateSingleton`.
  - The keep-alive runs on the timer thread (`TimberServer.cs:445`), so `LastFrameAtMs` stays fresh, and `LobbyRules.WatchdogDue` (:90-91) never fires in `CreatingWorld`.
- **Fix (sketch):** `LobbyInbox` records when the stage last changed, and `WatchdogDue(lastFrameMs, nowMs, stage, stageSinceMs)` is also due when `stage == CreatingWorld && nowMs - stageSinceMs >= CreateTimeoutMs + 60000`. The host gives up by itself at 120 s whenever its scene runs. Wire: none.
- **Check to add:** extend the LobbyChecks rules test with that case.
- **Test script:** none (needs a broken load).

### A-new-2: Start counts the room before closing it
- **Status:** Plausible (a window of microseconds). Traced.
- **Severity:** gameplay (rare)
- **Hits:** waiting room
- **Evidence:**
  - `LobbySession.Start` :179-181 takes `Room.Snapshot()` (StartedWith) and only then calls `IO.CloseLobby`.
  - `AdmitToLobby` holds `lobbyPumpGate` from its refusal check to `room.Add`, but `CloseLobbyToNewcomers` doesn't take that gate (:153-157).
  - A guest admitted between the two becomes a member, gets the save and enters the game. It is missing from `StartedWith`, so it has no pre-seeded slot, no start on a multi-start map and no planned faction, and founds as a guest without an id would.
- **Fix:** close first, then count; and `CloseLobbyToNewcomers` takes `lobbyPumpGate`. See the diff, LobbySession `@@ -176` and TimberServer `@@ -152`. Wire and saves: none.
- **Check to add:** hard to hit in a rig. A source scan that `LobbySession.Start` calls `CloseLobby` before `Snapshot` is cheap.
- **Test script:** none.

### J11a: the Steam lobby opens after Start if Start was pressed before the lobby existed
- **Status:** Plausible (traced; needs Start within about a second of opening the room, e.g. "Start the game on your own?").
- **Severity:** display (the server still refuses)
- **Hits:** waiting room. The same race exists for a classic host who acts in the first second.
- **Evidence:**
  - `SteamListener.CloseToNewGuests` (:97-105) returns if `!LobbyID.IsValid()`.
  - `OnLobbyCreated` (:58-73) later sets the lobby type (friends can see it) and `bb_open = "1"`.
  - A friend who joins passes `OnLobbyEntered`'s `bb_open` check and is refused only after the handshake, by `AdmitToLobby`, with the room's message. That text is right; the only cost is a lobby that looks joinable.
- **Fix:** a `volatile bool closed` set by `CloseToNewGuests`. `OnLobbyCreated` then writes `bb_open = closed ? "0" : "1"` and calls `SetLobbyJoinable(false)` when closed. Wire: Steam lobby data only.
- **Check to add:** none headless (needs Steam).
- **Test script:** D10b. Host opens a room and presses Start immediately; a friend's Steam friends list must not offer "Join game".

### J11b: guests never leave the host's Steam lobby
- **Status:** Plausible. By grep, the only guest-side `LeaveLobby` is the refusal at `SteamOverlayConnectionService.cs:191`.
- **Severity:** broken feature (low)
- **Hits:** waiting room
- **What happens:**
  - A guest who presses Leave, is removed, or fails to join stays a Steam lobby member.
  - The Steam lobby holds 8 (`SteamListener.cs:55`), while the room allows 7 connected guests. After enough comings and goings, a new friend can't enter the Steam lobby, while the room shows free places.
  - A removed guest is still a member (`SteamListener.IsInLobby`) and can connect straight back. D14 doesn't promise a ban, so that part is only worth documenting.
- **Fix (sketch):** remember the lobby id in `OnLobbyEntered`, and call `SteamMatchmaking.LeaveLobby` when the guest's room ends or it leaves (`LobbyGuestPanel.Leave`/`ShowEnd`) and when a join fails.
- **Check to add:** none headless.
- **Test script:** D6b. Guest: Leave, then rejoin by invite, five times. Each rejoin works, and the host's Steam lobby shows one member per guest (the Steam overlay's lobby view).

### J5b: the large-colony speed limit's session latch records no session in a room
- **Status:** Confirmed by trace.
- **Severity:** gameplay (speed only; very low)
- **Hits:** waiting room
- **Evidence:**
  - `LargeColonySpeedLimit.BeginHostSession` (:34-39) records `EventIO.Get()`. In a room, the start message is built on a join thread before `LobbySession.Update` calls `EventIO.Set(IO)` (:256), so it records null or the right IO depending on timing.
  - When null, `IsRemoved` on the host falls back to the host's live setting (:21-29). This matters only if the host changes Mod Settings mid-game; then host and guests scale speed differently. It affects only pacing.
- **Fix:** `InitializeClientEvent.Create(EventIO host)`, called as `Create(this)` from `ServerEventIO.CreateInitEvent`, then `BeginHostSession(host)`. See the diff, ConnectionEvents and LargeColonySpeedLimit hunks and ServerEventIO `@@ -132`. Wire and saves: none.
- **Check to add:** none headless (static game state). Optional source scan.
- **Test script:** none.

### E5 / J3: hosting from the main menu is always a room, and what that does to joins from a game
- **Status:** Confirmed by trace. Bullets 1 and 2 are D20 and documented; bullet 3 is below.
- **Severity:** gameplay
- **Hits:** waiting room. It reaches players who accept an invite while in a co-op game.
- **Evidence:**
  - `ServerHostingUtils.cs:151-158` sends every main-menu host through `OpenForSave`.
  - **J3:** a desynced guest's **Reconnect** (in its game) to a host that re-hosted from the main menu gets the D20 box ("return to the main menu…"). This is as designed; the in-game **Save and Rehost** stays classic.
  - **Bullet 3:** `TryToConnect` calls `EventIO.Set(client)` (`ClientConnectionService.cs:142`) before it knows what the host is.
    - In a co-op game this closes the running session at once. Until the welcome arrives, the old game's `ReplayService` (`io => EventIO.Get()`, `ReplayService.cs:130`) is working through the new connection: as a guest it sends its player's actions there, and a host's game waits for heartbeats.
    - D20 then resets EventIO. The player is left in their old game with no session and only the D20 box. If they were hosting, all their guests were dropped.
    - The first half of this is pre-existing for classic joins (which then load the new save), and so is the hazard of the old game's actions being sent to the new host.
- **Fix:** a product decision, so not in the diff. Either ask before connecting when `EventIO` holds a live session ("Leave this co-op game to join Kyler?"), or install the new client as EventIO only once its save arrives. The second is a larger change.
- **Check to add:** none.
- **Test script:** D12b. As a guest in a running co-op game, accept a waiting-room invite. Expect the D20 box, and the old session is over; note what the old game does.

### Mixed factions and the waiting room

### J5c: `hostFactions` in the start message is null for mixed save rooms, and racy for mixed new games
- **Status:** Confirmed by trace.
- **Severity:** display / gameplay (a guest may be offered a faction the host then refuses)
- **Hits:** mixed factions and the waiting room
- **Evidence:**
  - `ConnectionEvents.cs:87` sends factions only if `MixedFactions.IsOn`, which the main menu resets (`NewGameFactionCapture` ctor → `MixedFactions.Reset`). A save room builds every start message in the menu, so it sends null.
  - For a new game, it is on only while the host is still in the world-making scene. During the reload, `ColonyConfigurator` :61 resets it until `FactionService.Load`.
  - The guest's `AdoptHostFactions(null)` keeps its old value (`ColonySession.cs:61-65`; S1). That value feeds `FactionChoice.Available` for the founding dialog and the trade panel (`FactionChoice.cs:64`, `TradeOverviewPanel.cs:489`), while the host judges with its own (`ColonyRulesService.cs:232`).
- **Fix:**
  - In `LobbySession.Start`, `if (Setup.Mixed) ColonySession.LatchHostFactions(Setup.Factions)`. A room's factions are the host's unlocked ones: `OfferedFactions` requires all to be unlocked, and a save uses `UnlockedFactions`.
  - `Create` sends `HostFactions` when `MixedFactions.IsOn || LobbySession.Current?.Setup.Mixed == true`.
  - See the diff, LobbySession `@@ -176` and ConnectionEvents. Reviewer C's S1 reset (null → reset on the guest) complements it. Wire: `hostFactions` now filled where it was null.
- **Check to add:** a RuntimeChecks/IL or source scan that `LobbySession.Start` latches when mixed.
- **Test script:** Script F. A mixed save hosted from the main menu, where the host has only Folktails unlocked and the guest has no colony: the guest's founding dialog offers only what the host allows.

---

## J8: every exit (the table asked for)

| Exit | Host | Guest | Hold released | How checked |
|---|---|---|---|---|
| Host Cancel (Open) | `CancelLobby(Cancelled)` waits ≤2 s on the game thread, then `IO.Close`; page popped | "Kyler closed the waiting room" | never held | LobbyChecks "Ending the room tells every waiting guest"; trace |
| Host removes a guest | `RemoveFromLobby` sends End(removed); closed after the flush | "removed you" | — | LobbyChecks "The host can remove a waiting guest"; A-new-3 (the window) |
| Guest Leave, or disconnects (Open) | EOF → `LeaveLobby` → roster | page closes / "connection closed" | — | LobbyChecks "one who leaves drops off"; trace |
| Guest leaves after Start, before release | out of the room; `StartedWith` keeps its slot, start and pick (by design); the others are released and queued | — | — | ReviewA "J8: a guest that leaves after Start" |
| Guest disconnects mid-save | the paced write throws → closed | join error (`mapDelivered` false) | — | trace |
| Straggler at 10 s | `RemoveFromLobby(Failed, …)`, then load | "couldn't start…" if the page is still up; J2/J4 race | — | ReviewA J2/J4 |
| World not saved (20 s) or not made (120 s), or an exception | `Fail` → `CancelLobby(Failed)` → `IO.Close` → hold released → `LoadingScreen.Disable` → solo world plus box | "couldn't start the game: …" | yes (`Fail`, `AfterFailure`) | trace |
| The world scene never updates | no timeout | keep-alives continue, watchdog blind | no | **J8b** |
| The save's load call throws | `Fail`; in-game guests' streams closed | session ends | yes | trace |
| Host quits to the desktop | — | generic "connection closed" | — | trace (expected) |
| Main menu with a session left over | `EndStale` → `Cancel`/`Fail` (≤2 s) | told | yes | trace |
| Welcome and End in one frame | — | nothing said, Connecting box stays | — | **J8a** |
| Save room fails after Start | `sending.Fail` → failure box in the menu | told "failed" | never held | trace |
| "\<ts\> Co-op start" save | stays on disk (success and failure) | — | — | by design, harmless |

---

## Found sound

- **E4, wire.** A scripted classic session (handshake plus advisories, a 70 KB save, SetState, init, host and guest events, heartbeat, chat both ways, activity both ways, SessionFault, a refused late joiner) is byte-identical between beta17's and beta20's TimberNet, over 3 runs each (`revA/wire*`, `dump17.txt` = `dump20.txt`). The only classic difference on the wire is at mod level: the init event gains `"joiningClosedAtStart":false,"hostFactions":null` (NullValueHandling Include), and `FoundColonyEvent.faction`. Both are declared in the lobby plan §8 and beta20, and the handshake requires equal builds. D23 holds for framing.
- **E4, hunk by hunk.** The classic path is inert in every changed hunk:
  - `TimberServer`: accept loop (`lobby` null), `SendMap` (catch filtered on `lobbySave`), `StartQueuing` (no member), `SendErrorMessage` (same bytes, now under one lock), `Close` (empty lobby collections);
  - `TimberNetBase`: the sentinel is never written by a classic host; `IsInWaitingRoom` is false (with the A-new-3 fix too, since `lobby != null`);
  - `ClientEventIO`: `Ended` is never true; `ClientConnectionService`: never welcomed, so LoadMap takes `StartSaveGame`;
  - `ServerEventIO.Start` → `StartServer`: same order, `BeginHostSession` first;
  - `SteamOverlayConnectionService`: only the box text; `RehostingService`, `ReplayService`: no diff; `WaitsForStart`'s flag is false.
- **J2 (newcomers).** Every lobby-mode connection passes `AdmitToLobby`, which refuses once closed, for the whole game. Only the J4 race reaches `StartQueuing`.
- **J4 (the thread pool).** With the pool starved to two free threads (the rig's accept loop holding one), all 7 guests' paced saves were queued 2.5–3.1 s after release, against the 10 s budget (≥8 s after `ReleaseLobby`'s ≤2 s flush). With all but one thread taken, it was 3.9 s. On a real host, the accept loops permanently hold three pool threads (TimberServer's plus one per listener in `MultiSocketListener`), and nothing else blocks the pool at release. It is measured on .NET 8's pool; Mono's injects at a similar rate.
- **J6.** The host loading its file while guests load captured bytes is the same pattern as beta17's `LoadAndHost` (bytes read at the click, `StartSaveGame` at the dialog's Start, minutes later). `StartSaveGame(ref)` equals `LoadScene(CreateGameSaveParameters(ref), GetTip())`: the only difference is the tip and one menu-side random draw, both before `DeterminismService`'s constructor applies the seed. Optional hardening for both paths: re-read the file at Start and compare.
- **J7.**
  - The host loads only once every member is queued (`StartQueuing` runs before its save is sent) or removed, and it records nothing until its game has loaded, so every queued guest gets every tick-0 action behind its save.
  - `joiningClosedAtStart` is true in every start message of a room, and false after any `BeginHostSession` (classic, and rehost).
  - Tick-0 actions use the same paused-replay path as R10.
  - A founding at tick 0 has still never been played (R13): Script D8.
- **J9.**
  - The game-thread waits are bounded: `CancelLobby` and `Fail` ≤ `AbortFlushMs` (2 s); `TimberClient.Start` 3 s and `Dns.GetHostEntry` are unchanged from beta17.
  - Lock order is pump gate → room gate → lane gate, and `queuedMessages` → lane gate. The stream lock is never taken while another is held, and the fixes keep this: `CloseLobbyToNewcomers` takes pump → room, like `AdmitToLobby`.
  - `lobbyTimer`'s cross-thread use is safe (`?.` reads once; `ObjectDisposedException` caught).
  - Concurrent start messages race on `TimberNetBase.Hash`, which only logs and seeds SetState, and nothing compares it.
  - A lobby write stuck under the stream lock after `inGame` blocks that guest's paced save indefinitely, which is the same exposure as beta17's paced save to a frozen guest (pre-existing).
- **J10 (gate and caps), fuzzed:**
  - 300 random frames, a 10 MB gzip bomb, deep JSON, and 18 wrong or foreign types from a waiting guest: nothing reached the room or the game, the host stayed up, and the other guest kept hearing keep-alives;
  - lengths 0, -1, -5, `int.MinValue`, 64 KB+1 and `int.MaxValue` each closed only that guest.
- **J10 (id after Start).** A later hello changed only the room's display. `StartedWith` is frozen at Start and is what seats and plans; the in-game hello carries `LocalPlayerIdentity.Id` whatever the room saw. With the fix, a hello is taken once anyway. The direct-IP claim itself is out of scope, and the same as the in-game hello.
- **X4 (wire).**
  - Faction ids are validated both ways (`IsWellFormedFactionId`, at most 8 factions, at most 16 rows), and a `LobbyFaction` is taken only when mixed, open, after the hello, and from a guest that may pick (LobbyChecks).
  - Nit: `TryParseRoster`/`TryParseWelcome`/`TryParseState` throw `OverflowException` for out-of-range integers instead of returning false (2 of 8 fuzz cases). They are host → guest only, and caught by `ReceiveLobbyFrame`; the inbox stays usable.
- **J11 (names).** The Steam persona name versus the hello's name is display only; the hello's name wins, as in the game.
- **R6 (the loading-screen hold).** It is released on every exit that can run: `Update`, `FinishIfLoaded`, `Fail`, `AfterFailure`, `EndStale`. The one exception is J8b.
- **J1's fix keeps D20 and classic.** A guest in a game (no page, `saveReceived` false) still leaves with the box; classic joins are never welcomed.
