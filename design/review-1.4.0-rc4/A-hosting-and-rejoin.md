# Reviewer A: hosting, rehosting, the guest's rejoin, and the network side (1.4.0-rc2 to rc4)

Scope: `git diff ebe3d4a ebb6570` for Connect/HostCoopFlow.cs, ServerHostingUtils.cs, RehostingService.cs,
ClientConnectionService.cs, ClientConnectionUI.cs, SessionEndMessages.cs, DesyncDialogPlan.cs, IO/ClientEventIO.cs,
IO/ServerEventIO.cs, ReplayService.cs (EndSession), Lobby/LobbySession.cs, LobbyHostPanel.cs, LobbyGuestPanel.cs,
Lobby/ConnectingBox.cs, TimberNet/LobbyRoom.cs, LobbyFrames.cs, LobbyInbox.cs, TimberServer.cs, TimberClient.cs,
TCPClientWrapper.cs, Events/ConnectionEvents.cs, Colonies/ColonySession.cs, Colonies/SaveConversion.cs,
Steam/SteamOverlayConnectionService.cs, Steam/SteamListener.cs, Plugin.cs; checked against the decompiled
Timberborn.CoreUI (PanelStack), GameSaveRepositorySystemUI (LoadGameBox, ValidatingGameLoader),
GameSaveRuntimeSystem (GameSaver), MainMenuSceneLoading, SteamOverlaySystem, OptionsGame (GameOptionsBox).
Nothing was built or run; the game was not started.

Summary: **11 findings**: 7 Confirmed, 1 Plausible, 3 Confirmed with one Plausible link (marked). By kind: 2
session-stoppers, 3 gameplay/UI, 3 UI, 2 performance, 1 dead code. Plus 6 docs items.

| ID | Title | Status | Kind | Severity |
|----|-------|--------|------|----------|
| A1 | Host co-op game never shows in a single-player game's menu | Confirmed | gameplay/UI (feature missing) | High |
| A2 | A rejoin box can be left on screen with a Cancel that does nothing, and the guest's page never opens | Confirmed | session-stopper | High |
| A3 | A Steam guest's rejoin ends at the first lobby Steam shows, usually the old game's: "in any order" fails | Plausible | session-stopper (rejoin fails) | Medium-High |
| A4 | Each quiet direct-IP try blocks the main thread (up to 3 s) and the tries run back to back | Confirmed (duration Plausible) | performance/UI | Medium |
| A5 | A direct-IP guest waiting to rejoin gives the host's running game a mods-differ dialog every 3 s | Confirmed (when mod lists differ) | UI | Medium |
| A6 | Quiet tries aren't quiet for a host name: a DNS failure stacks an error dialog every 3 s | Confirmed (trigger Plausible) | UI | Low-Medium |
| A7 | Host mode leaks into the game: the first in-game Load game after hosting from the box loads nothing | Confirmed | gameplay/UI | Low-Medium |
| A8 | After a session ends, the game menu offers Host co-op game to guests, and to a host after a failed action | Confirmed | UI/gameplay | Low |
| A9 | The Host co-op game box's gold line goes blank when the box comes back from the Co-op Game page | Confirmed | UI | Low |
| A10 | The Host co-op game box reads the whole save on the menu's thread at every new selection | Confirmed | performance | Low |
| A11 | Dead code left by rc4 (PendingRehost, ReconnectNow, the classic start gates, triedLobby) | Confirmed | dead code | Low |

---

## 1. Findings

### A1. Host co-op game never shows in a single-player game's menu (RehostingService is bound only in co-op)

- **Status:** Confirmed. **Kind:** gameplay/UI (a headline rc4 feature is missing). **Who:** every player who hosts a
  game they're playing alone (README step 4, Script H step 6).
- **Evidence:**
  - `Plugin.cs:53` `if (EventIO.IsNull) return;` comes before `Plugin.cs:63` `Bind<RehostingService>()`. A game
    loaded alone has no EventIO at configure time: the main-menu configurator reset it (`Plugin.cs:100`) and nothing
    sets it for a plain load. So RehostingService is never made in single player. `grep Bind<RehostingService>` finds
    only that line.
  - `ClientConnectionUI.cs:108`: `host.ToggleDisplayStyle((alone || hosting) && SingletonManager.GetSingleton<RehostingService>() != null)`
    hides the button, and `ClientConnectionUI.cs:119-120` returns if it's missing anyway.
  - The only way a game is ever co-op at load is through a room (`LobbySession.Update` → `EventIO.Set(IO)`, LobbySession.cs:281),
    so the button shows only for a co-op host (as Save and Rehost), never for a game played alone.
- **Scenario:** The player loads a save with Load game, plays, and presses Esc. There's no Host co-op game under Load game
  (Script H step 6 fails at once). The only way to host that game is to save it by hand, exit, and use the main menu box.
- **Fix:** In `ReplayConfigurator.Configure`, move `containerDefinition.Bind<RehostingService>().AsSingleton();` above
  `if (EventIO.IsNull) return;`. Its constructor needs only game-scene singletons (AutosaveNameService, GameSaver,
  GameSaveRepository, SettlementReferenceService, ValidatingGameLoader, DialogBoxShower, MainMenuSceneLoader). The
  co-op-only ReplayService still gets it through DI.
- **Check:** StabilityTests source check: in `Body(plugin, "public class ReplayConfigurator")`, the index of
  `Bind<RehostingService>()` must be less than the index of `if (EventIO.IsNull) return;`. RuntimeChecks: an IL scan of
  `ReplayConfigurator.Configure` checks that the generic `Bind` call over RehostingService comes before the first call
  to `EventIO.get_IsNull`.

### A2. A rejoin box can be left on screen with a Cancel that does nothing, and the guest's page never opens

- **Status:** Confirmed (every link traced in the mod and in the decompiled PanelStack and SteamOverlayInputBlocker).
  The trigger is an overlay above the box at the moment the rejoin joins. **Kind:** session-stopper. **Who:** a
  rejoining guest who has the Steam overlay open (for example, accepting the host's invite in it, the path README L379
  describes for an invite-only lobby), or who has an error dialog over the box (see A6).
- **Evidence:**
  - `ClientConnectionService.StopRejoin` (`ClientConnectionService.cs:258-268`) calls `box.Close()`, then drops its only
    reference (`rejoinBox = null`).
  - `ConnectingBox.Close` (`ConnectingBox.cs:56-61`) pops the box only when it is on top. Otherwise it sets
    `popWhenOnTop` and relies on its owner's `Poll()` (`ConnectingBox.cs:64-69`), which only happens through
    `rejoinBox?.Poll()` (`ClientConnectionService.cs:497`), now null. So the box is never popped.
  - Its Cancel and Esc go to `OnUICancelled` (`ConnectingBox.cs:48-53`), which returns at once because `closed` is
    already true. PanelStack routes Esc only to the top panel (`Timberborn.CoreUI.decompiled.cs:2853`, ProcessUICancel),
    and an overlay on top swallows all other input (ProcessInput 2777).
  - `LobbyGuestPanel.Pump` opens the page only when `PageOnTop()` (`LobbyGuestPanel.cs:99,140`: the top panel isn't an
    overlay). The stale box was pushed with `PushDialog` (`ConnectingBox.cs:39`), so it is an overlay. Its
    `CloseConnectingBox()` call (line 98) no longer reaches the box (`ClientConnectionService.cs:369`: rejoinBox is null).
  - What covers the box: the game pushes `SteamOverlayInputBlocker` as an overlay whenever the Steam overlay opens, and
    pops it only if it is on top (`Timberborn.SteamOverlaySystem.decompiled.cs:144-158`).
  - Two paths reach `StopRejoin` while covered:
    - the welcome check, `ClientConnectionService.cs:218-222`;
    - the Steam branch, which calls `StopRejoin` before `JoinLobby` (line 243).
- **Scenario:**
  1. A guest waits on *Waiting for the host to host again…*. The host rehosts and sends a Steam invite.
  2. The guest presses Shift+Tab and accepts the invite in the overlay. `OnLobbyEntered` connects (SteamOverlayConnectionService.cs:199).
  3. The welcome arrives while the overlay is still open. `StopRejoin` "closes" the box under the blocker, and the
     reference is dropped.
  4. The overlay closes and its blocker pops. The rejoin box is on top again, and Cancel and Esc do nothing. The guest
     page never opens: the host sees a *Joining…* row that never readies, and the guest can't leave.
  5. If the host cancels the room, the end dialog appears over the stuck box. After that, the only way out is to quit
     Timberborn. If the host presses Start anyway, the save's scene change clears the box.
- **Fix:**
  - (a) In `StopRejoin`, keep a box whose pop is deferred: `if (box != null) { box.Close(); if (!box.IsClosed) closingBoxes.Add(box); }`,
    and in `UpdateSingleton` poll `closingBoxes`, removing each once `IsClosed`. Or keep `rejoinBox` until
    `IsClosed` and null it in `UpdateSingleton`.
  - (b) Make `ConnectingBox.OnUICancelled` able to pop itself when closed:
    `if (closed) { if (_panelStack.IsPanelOnTop(this)) { popWhenOnTop = false; _panelStack.Pop(this); } return; }`.
  - The same orphaning exists, from before rc4, in `ShowConnecting` (`CloseConnectingBox()` at line 347, then
    `connectingBox = ConnectingBox.Show(...)` at 356). Fix both the same way.
- **Check:** StabilityTests source checks:
  - `Body(service, "private void StopRejoin(bool resetJoin)")` must not assign `rejoinBox = null` unless the box
    `IsClosed` (or must add it to a polled list);
  - `Body(connectingBox, "public void OnUICancelled()")` must pop when `closed` and on top.

  Better still, extract a pure `ConnectingBoxRules.CancelPops(bool closed, bool onTop)` and test it (closed and on top →
  pop).

### A3. A Steam guest's rejoin ends at the first lobby Steam shows, usually the old game's lobby: "in any order" fails

- **Status:** Plausible. The one unproven link is that Steam's `GetFriendGamePlayed` reports the host's started lobby
  (joinable off, `bb_open` "0"). The mod's own Join box assumes it does: `FriendGameRules.cs:88` has a *Started* state
  for exactly those lobbies. **Kind:** session-stopper (the rejoin gives up with an error). **Who:** Steam guests who press
  Reconnect (wait for Rehost) before the host presses Save and Rehost (the desync message says "in any order", CSV
  line 26), and Steam guests who press Rejoin while the host plays on.
- **Evidence:**
  - The host's Steam lobby lives for the whole game:
    - `SteamListener.CloseToNewGuests` (SteamListener.cs:136-146, called at Start by `ServerEventIO.CloseLobby`,
      ServerEventIO.cs:74-88) only sets joinable off and `bb_open` to "0";
    - the lobby is left only in `SteamListener.Stop` (148-160), which runs when the host's main menu resets EventIO
      (Plugin.cs:100).
  - The guest never leaves it: `LeaveLobby` appears only at SteamOverlayConnectionService.cs:194 (the refusal) and
    SteamListener.cs:156. TWO-COLONIES L520 says so too.
  - `WatchRejoin` (ClientConnectionService.cs:235-246):
    - `DesyncDialogPlan.Reconnect` returns `JoinSteamLobby` for whatever lobby `FindHostLobby` (313-327) sees;
    - `StopRejoin(resetJoin: false)` then ends the rejoin (`rejoinPending = false`, and `triedLobby` reset to 0, which
      makes the guard at 240 dead) before `SteamMatchmaking.JoinLobby`.
  - `OnLobbyEntered` (SteamOverlayConnectionService.cs:177-211) then does one of three things:
    - shows *connection failed* if entering failed (a non-joinable lobby);
    - calls `LeaveLobby` and shows *The host has already started the game* for `bb_open == "0"`;
    - does nothing at all if the guest is now the old lobby's owner (the host left it and ownership passed to a
      remaining guest; `owner != SteamUser.GetSteamID()` is false). This can happen when Steam's friend info still
      shows the old lobby for a moment after the host left.
- **Scenario:**
  1. A guest desyncs. Following the dialog ("in any order"), the Steam guest presses Reconnect first and reaches the
     main menu while the host is still reading its desync dialog.
  2. The first try finds the host's current, closed lobby, joins it, and shows *The host has already started the
     game. Ask them to rehost and send you a new invite.* The rejoin is over.
  3. The host then rehosts, and nothing brings the guest in until they accept an invite or use Join co-op game.
  4. The same happens to a Steam guest who pressed Rejoin after their own connection dropped while the host played on:
     an error at once, instead of waiting.

  Direct-IP guests aren't affected: the running game's room refuses them quietly (TimberServer.cs:524 →
  LobbyRoom.cs:230-237), and they try again.
- **Fix:**
  - (1) Record the lobby a guest entered: in `OnLobbyEntered` on success, keep `lastLobby` alongside `lastJoin`
    (extend `JoinRoute.ViaSteam(host, lobby)`).
  - (2) When a Steam guest's session ends (EndSession, HandleDesync, Reconnect), `SteamMatchmaking.LeaveLobby(lastLobby)`,
    so it never inherits ownership of the old lobby.
  - (3) In `WatchRejoin`'s `JoinSteamLobby` case, do not `StopRejoin`. Skip `plan.Lobby == lastLobby`. Otherwise call
    `SteamMatchmaking.RequestLobbyData(plan.Lobby)`, and join only once `GetLobbyData(lobby, SteamListener.OpenKey) == "1"`
    (and `RoomKey == "1"`), keeping the box until the welcome.
  - (4) While `rejoinPending`, make `OnLobbyEntered`'s refusals quiet: log, `LeaveLobby`, and let the watcher go on.
- **Check:**
  - Extract a pure `DesyncDialogPlan.RejoinLobbyStep(ulong lobby, ulong oldLobby, string open, string room)` and test
    it in StabilityTests: the old lobby → wait; `open` of "0" or "" → wait; "1" with room "1" → join.
  - Source check: in `Body(service, "private void WatchRejoin()")`, no `StopRejoin(` before `SteamMatchmaking.JoinLobby`.

### A4. Each quiet direct-IP try blocks the main thread (up to 3 s) and the tries run back to back

- **Status:** Confirmed that the main thread blocks. Plausible how long each block lasts: 3 s when the host's firewall
  drops the connection attempt, and about 1-2 s on Windows when it is refused, because Windows retries after a refusal.
  **Kind:** performance/UI. **Who:** direct-IP guests waiting to rejoin, whenever nothing listens at the host's
  address. That happens between the host's old server closing (Plugin.cs:100, as its main menu configures) and the
  room opening (at least 2 frames plus the save checks, HostCoopFlow.cs:186-192), and all the time when the host's game
  crashed or was closed (a common reason for *connection lost*).
- **Evidence:**
  - `WatchRejoin` runs in `UpdateSingleton` on the main thread. It sets `nextRejoinMs = now + 3000` before the try
    (ClientConnectionService.cs:232-234), then calls `TryToConnect(plan.Address)` synchronously (252).
  - That reaches `ClientEventIO`'s constructor, which calls `NetBase.Start()` (ClientEventIO.cs:48-50), which calls
    `client.ConnectAsync().Wait(3000)` (TimberClient.cs:164).
  - A try that times out takes the whole 3 s, so the next frame is already due, and the menu renders about one frame
    every 3 s for as long as nothing listens.
  - A host name adds `Dns.GetHostEntry` on the same thread (ClientConnectionService.cs:99-111, 580).
  - A failed attempt also leaks its socket: `TCPClientWrapper.Close` (TCPClientWrapper.cs:68-76) calls
    `client.GetStream()` first, which throws on a socket that never connected, so `client.Close()` is skipped. The
    pending connect keeps retrying in the background for about 21 s, so several of these are alive at once.
- **Scenario:** The host's game crashes. The guest reads *connection lost* and presses Rejoin. Their main menu shows
  *Waiting for the host…* but stutters or freezes (about 1 frame every 3 s) until the host is back and hosting, and
  Cancel takes seconds to respond.
- **Fix:**
  - Don't dial on the main thread. In `WatchRejoin`'s address case, start a probe `Task` that resolves the name and
    connects a plain `TcpClient` with a 3 s timeout, and look at it on later frames. Only when it connects, close it
    and call `TryToConnect` (the host is then listening, so the real connect returns at once).
  - Or change `TimberClient.Start` to connect on its network thread, as Steam's `IConnectionAwaitable` path already
    does. That also removes the 3 s freeze of every ordinary Join.
  - In `TCPClientWrapper.Close`, close the stream and the client in separate try blocks.
- **Check:**
  - TimberNet logic check in StabilityTests: a fake `ISocketStream` whose `ConnectAsync` never completes. Either
    `new TimberClient(fake).Start()` returns within 100 ms (if the connect moves to the network thread), or a source
    check that `WatchRejoin`'s default branch no longer calls `TryToConnect(` directly.
  - `TCPClientWrapper.Close` on a never-connected wrapper leaves the inner `TcpClient` disposed (reflection on `client.Client == null`).

### A5. A direct-IP guest waiting to rejoin gives the host's running game a mods-differ dialog every 3 s

- **Status:** Confirmed, when the two mod lists differ (which the mod allows: a difference is only a warning).
  **Kind:** UI. **Who:** a host still in the game (on the desync dialog, or playing on after a guest's connection
  dropped) while a direct-IP guest waits to rejoin.
- **Evidence:**
  - Each quiet try connects to the host's in-game server. The server runs the compatibility handshake
    (TimberServer.cs:510) before the room refuses the newcomer (TimberServer.cs:519-524 → `AdmitToLobby` 260-290,
    `RefusalForNewcomer` LobbyRoom.cs:230-237).
  - The handshake queues the guest's mod list (`RunCompatibilityHandshake`, TimberNetBase.cs:456-460), and
    `TimberNetBase.Update` raises `OnPeerAdvisory` (768-772). The host's ReplayService calls `io.Update()` every frame
    (ReplayService.cs:756).
  - `ModCompatibility.OnPeerAdvisory` (ModCompatibility.cs:79-102) queues a `ModWarnings` entry whenever the lists
    differ. `ModMismatchWarningService` (bound in co-op games, Plugin.cs:71) shows a dialog for every batch it finds
    (ModMismatchWarningService.cs:24-33). Nothing removes duplicates: `ModWarnings.Clear()` runs only when a server or
    client starts.
- **Scenario:** A guest desyncs and presses Reconnect first ("in any order"). The host, still in the game reading the
  desync message, gets *Your mods and the other player's differ…* every 3 seconds, one on top of the other, until they
  press Save and Rehost. A guest whose Wi-Fi dropped while the host plays on does the same to the host for as long as
  the host keeps playing. The host's log also gains lines like *Refused a guest at the waiting room* every 3 s.
- **Fix:** Deliver a guest's advisory only once it is admitted. In `TimberServer`, keep the handshake's remote advisory
  with the stream and enqueue it after `AdmitToLobby` returns a member (drop it when refused). Also, or instead, have
  `ModCompatibility.OnPeerAdvisory` skip a difference already warned about this session (a `HashSet` of peer plus
  difference text).
- **Check:** TimberNet logic check. A `TimberServer` with an open-then-closed `LobbyRoom`, and a fake client stream
  that does the handshake with an advisory: after `CloseLobbyToNewcomers`, one connection plus `Update()` raises no
  `OnPeerAdvisory`. Or a unit check that two identical `OnPeerAdvisory` calls queue one warning.

### A6. Quiet tries aren't quiet for a host name: a DNS failure stacks an error dialog every 3 s

- **Status:** Confirmed code path. The trigger (the typed address is a name, and DNS fails while waiting) is
  Plausible, and likely when the lost connection was the guest's own network. **Kind:** UI. **Who:** direct-IP guests
  who joined by host name (dynamic DNS and similar).
- **Evidence:**
  - `TryToConnect(string)` calls `ShowError("…InvalidAddress")` (ClientConnectionService.cs:109) and
    `ShowError("…InvalidFormat")` (87) whatever `quietJoin` says. Only the network error callback checks `quiet`
    (129-137).
  - `WatchRejoin` goes on trying while an error dialog sits over the rejoin box, because the box isn't closed, so it
    isn't shown again (224-229).
  - `Dns.GetHostEntry` (580) is synchronous, and can take seconds when no DNS server answers (see A4).
- **Scenario:** The guest's router drops, the game says *connection lost*, and the guest presses Rejoin. With no DNS,
  every 3 s a *Could not connect: invalid address* dialog stacks on the rejoin box. Once the network is back and the
  host's page opens, the next try succeeds with those dialogs still on top, which is exactly A2's trigger: the rejoin
  box is stuck.
- **Fix:** In `TryToConnect(string)`, when `quietJoin`, log the parse or resolve failure and return false without
  `ShowError`. Better, do the resolution in A4's probe task.
- **Check:** StabilityTests source check: both `ShowError(` calls in `Body(service, "public bool TryToConnect(string address)")`
  are guarded by `!quietJoin`.

### A7. Host mode leaks into the game: the first in-game Load game after hosting from the box loads nothing

- **Status:** Confirmed. **Kind:** gameplay/UI. **Who:** a host whose game was started from the main menu's Host co-op
  game box (the main rc4 way to host a save), who then uses Esc → Load game in that game.
- **Evidence:**
  - `HostCoopMenu.HostMode` is static (HostCoopFlow.cs:66). It is set by `OpenBox` (100) and cleared only by
    `BoxClosed` (107, the `OnUICancelled` postfix, ServerHostingUtils.cs:118-122) and by the constructor (94), which
    runs only in the main menu.
  - Hosting from the box pushes the Co-op Game page over the box (LobbyHostPanel.cs:201, `HideAndPush`). Start loads
    the save's scene (LobbySession.cs:286) without the box ever being cancelled, so `HostMode` is still true in the game.
  - In the game, `LoadGameBoxLoadGamePatcher.Prefix` (ServerHostingUtils.cs:104-115) sees `HostMode` and hosts instead
    of loading. The game's Load click, Enter and double-click all go through `LoadGame`
    (Timberborn.GameSaveRepositorySystemUI.decompiled.cs:537-540, 570-573, 582-585).
  - `LoadAndHost` finds no `LobbyHostPanel` in a game and only logs *A save can be hosted only from the main menu*
    (ServerHostingUtils.cs:205-213).
  - The in-game box looks normal: the postfix hides only the Host button (line 53), and Load stays visible.
- **Scenario:** Host co-op game box → a save → Host co-op game → Start Game. Later in that game the host presses Esc →
  Load game → picks an autosave → Load. The save checks run (possibly with their dialogs), then nothing happens and
  nothing is said. Closing and reopening the box works (its `OnUICancelled` cleared the flag).
- **Fix:** Clear the mode whenever a game scene configures: add `HostCoopMenu.ResetMode()` (an internal
  `HostMode = false`) to `ReplayConfigurator.Configure`. Also make the prefix and the selection postfix
  require `HostCoopMenu.Instance != null` (the main menu's), since the mode means nothing without it.
- **Check:** StabilityTests source checks:
  - `Body(hosting, "static bool Prefix(LoadGameBox __instance, ref bool __result)")` contains `HostCoopMenu.Instance == null`;
  - `Plugin.cs`'s ReplayConfigurator resets the mode.

### A8. After a session ends, the game menu offers Host co-op game to guests, and to a host after a failed action

- **Status:** Confirmed. **Kind:** UI/gameplay. **Who:** guests after a desync, a lost connection or a failed action;
  hosts after a failed action.
- **Evidence:**
  - `DressHostInGame` (ClientConnectionUI.cs:104-110) shows the button when `EventIO.IsNull || EventIO is ServerEventIO`
    and RehostingService exists. RehostingService exists in any game that was co-op at load (Plugin.cs:63), a guest's
    included.
  - Every way a session ends resets EventIO: `AbortReplay` 538, `EndSession` 565, `HandleDesync` 623. So a guest whose
    session ended is "alone" and gets **Host co-op game**. That contradicts rc4's "a guest has neither"
    (STABILITY-CHANGELOG rc4, TWO-COLONIES L85-88, Script H step 7).
  - After a failed action, a host is also "alone": the button reads Host co-op game instead of Save and Rehost.
    `SaveRehostFile` then refuses with its message (RehostingService.cs:65-71), and `HostClicked` stacks a second
    *Failed to Rehost…* dialog (ClientConnectionUI.cs:128).
- **Scenario:**
  - A guest desyncs and closes the dialog. Esc shows Host co-op game. Pressing it saves the guest's own out-of-step
    copy (`<date> Co-op`, in the join's hash-named settlement) and opens a room for it, which friends could join
    instead of the host's.
  - A host whose action failed presses the button and gets two dialogs, one saying rehosting isn't allowed.
- **Fix:** Remember the session's role for the whole game (for example `ReplayService.StartedAsGuest`, set in its
  constructor from `EventIO.Get() is ClientEventIO`). In `DressHostInGame`, hide the button when the game started as a
  guest, and hide it (or keep Save and Rehost with the refusal as its tooltip) when `ReplayService.HasReplayFailure`.
  In `HostClicked`, don't show the second dialog when `SaveRehostFile` already said why.
- **Check:** Extract `HostButtonRules.Show(bool alone, bool hosting, bool startedAsGuest, bool replayFailure)` and
  test it in StabilityTests: guest after end → hidden; failed host → hidden or Save and Rehost; alone → shown.

### A9. The Host co-op game box's gold line goes blank when the box comes back from the Co-op Game page

- **Status:** Confirmed. **Kind:** UI. **Who:** hosts who open a save's page and press Cancel (or whose save Start fails).
- **Evidence:**
  - `Dress` sets `status.text = ""` on every `GetPanel` (HostCoopFlow.cs:140, called from the postfix,
    ServerHostingUtils.cs:52).
  - The page is pushed with `HideAndPush` (LobbyHostPanel.cs:201). Its `Pop` (CloseRoom 359, ShowFailure 347) makes
    PanelStack re-show the box through `ShowTop` → `GetPanel` (Timberborn.CoreUI.decompiled.cs:2825-2836, 2915-2921).
  - The selection doesn't change, so `OnSaveSelectionChanged` doesn't run again and nothing refills the line.
- **Scenario:** Pick a shared save (*One shared colony…*), press Host co-op game, then Cancel on the page. The box
  comes back with the same save selected and no gold line.
- **Fix:** In `Dress`, restore the shown save's line:
  `status.text = HostMode && shownKey != null && read.TryGetValue(shownKey, out var info) ? StatusText(info) : "";`.
- **Check:** StabilityTests source check: `Body(flow, "public void Dress(VisualElement root)")` no longer sets
  `status.text = ""` without looking at `shownKey`.

### A10. The Host co-op game box reads the whole save on the menu's thread at every new selection

- **Status:** Confirmed. **Kind:** performance. **Who:** hosts clicking through large late-game saves (and slow disks).
- **Evidence:** `SaveSelected` calls `ServerHostingUtils.GetMapBtyes` (HostCoopFlow.cs:164 → ServerHostingUtils.cs:177-188),
  which copies the entire save into memory on the main thread. Only the parse runs in `Task.Run`. The changelog says
  the line "is read off the menu's thread".
- **Scenario:** Clicking through a settlement's saves hitches on each new one, for as long as the file takes to read.
- **Fix:** Move the file read into the task:
  `Task.Run(() => SaveColonyReader.Read(ServerHostingUtils.GetMapBtyes(_gameSaveRepository, save)))`. The repository
  only opens a file stream. Catch inside the task.
- **Check:** StabilityTests source check: `Body(flow, "public void SaveSelected(SaveReference save)")` calls
  `GetMapBtyes` only inside the `Task.Run` lambda.

### A11. Dead code left by rc4

- **Status:** Confirmed. **Kind:** dead code. **Who:** maintainers.
- **Evidence:**
  - `HostCoopFlow.PendingRehost` is set (HostCoopFlow.cs:35,45) and never read (grep).
  - `ClientConnectionService.ReconnectNow` and `ShowWaitForSteamInvite` (276-309) can't be reached: `Reconnect` is
    called only from a game (ConnectionEvents.cs:253, `RejoinFromGame`), where `LobbyGuestPanel` is null, so it
    always takes the `rejoinPending` branch (185-191).
  - `RehostingService._validatingGameLoader` is unused.
  - The `triedLobby` guard (240) is reset by `StopRejoin` (261) on the very next line of that path.
  - Since every session starts from a room, the classic "joining open" branches never run:
    - `ServerEventIO.IsAcceptingClients` is false from `CloseLobby` at Start (ServerEventIO.cs:74-77, 209);
    - so `HostStartGate.TryHold` never holds (HostStartRules.cs:10-11);
    - `ReplayService`'s `StopAcceptingClients` at tick 0/1 (426, 962) is a no-op;
    - `ColonyRulesService`'s "host still waiting for players" refusal (111-113) never fires;
    - `ColonyRules.WaitsForStart` is always false (`joiningClosedAtStart` is true for every session:
      LobbySession.cs:199 → ConnectionEvents.cs:82).
- **Fix:** Remove `PendingRehost`, `ReconnectNow` and `ShowWaitForSteamInvite` (and the `WaitForSteamInvite` string),
  and the unused field. Leave the classic gates for rc5 or drop them with their checks and strings
  (`Colony.Start.*`). They're harmless, but they suggest a way to join that no longer exists.
- **Check:** None needed beyond removing the code. If the gates stay, a comment check naming them unreachable.

---

## 2. Found sound

- **The rehost save is complete and released before the scene changes.**
  - `GameSaver.Save` is synchronous and calls `OnSaveCompleted` inside its `using` stream
    (Timberborn.GameSaveRuntimeSystem.decompiled.cs:253-274).
  - `SaveRehostFile(waitUntilAccessible: true)` defers the callback one frame (RehostingService.cs:72-85,
    TimeoutUtils.cs:11-23), so `HostInMainMenu` → `OpenMainMenu` runs after the file is closed.
  - `OpenMainMenu` posts `PreMainMenuStartedEvent(skipAutoSave: true)`, then `LoadSceneInstantly`
    (MainMenuSceneLoading.decompiled.cs:127-131), so no autosave interferes.
- **The host's old server and its guests when the host leaves for the main menu.**
  - The main-menu configurator resets EventIO (Plugin.cs:100). `TimberServer.Close` closes every guest without a
    SessionFault (TimberServer.cs:963-993).
  - Guests get `OnConnectionError` → `EndRunningGame` → `EndSession(ConnectionLost, offerRejoin: true)`
    (ClientEventIO.cs:112-117). A guest whose game was still loading gets the same through
    `ReplayService.UpdateSingleton` (ReplayService.cs:760-762).
  - A host's own `EndSession(null)` shows nothing.
- **The handed-over save opens its page only when the menu is up, once.**
  - `framesInMenu` and the overlay test (HostCoopFlow.cs:186-187) wait out the changelog and first-timer dialogs,
    which are pushed in PostLoad.
  - `TakePending` clears the pending save before the validators run, so a Cancel in a validator dialog just leaves
    the host in the menu. An exception shows *Could not open* (193-198).
  - `LoadIfSaveValidAndHost` mirrors `ValidatingGameLoader.CheckNextValidator` exactly
    (GameSaveRepositorySystemUI.decompiled.cs:1233-1249).
- **HasReplayFailure blocks an in-game rehost but not the right one.**
  - `ReplayService` is an `IResettableSingleton` (ReplayService.cs:113), and `SingletonManager.Reset` in the next
    configurator calls its `Reset()`, which clears `HasReplayFailure` (190-193).
  - So the advised route (main menu → Host co-op game box → a known-good save) works.
- **The quiet try's failures.**
  - A connect that fails in the constructor calls `onError(msg, null)`, which is silent while quiet, and `Create`
    returns null so `client` is null (ClientEventIO.cs:48-58,132-137).
  - A later failure calls `CleanUp`, which nulls `NetBase` (121-129), so `WatchRejoin` sees `net == null` and tries
    again (231). `ReportWhileJoining` calls `ResetIf(this)`.
  - `client.Update()` and `CheckWaitingRoom` on a dead client do nothing (NetIOBase.cs:29-33).
- **A quiet try can't slip into the host's running game.** At Start, `IO.CloseLobby` → `CloseLobbyToNewcomers` sets
  `closedMessage` (LobbySession.cs:196, LobbyRoom.cs:219-227). Every later connection goes through `AdmitToLobby` →
  `RefusalForNewcomer` (TimberServer.cs:519-524, LobbyRoom.cs:230-237) and is refused. `StartQueuing` also refuses
  anyone not claimed from the room (566-590).
- **`rejoinPending` across scenes.**
  - It is static and set just before `OpenMainMenu` (ClientConnectionService.cs:185-190).
  - `WatchRejoin` acts only where `LobbyGuestPanel` exists (216), so never in a game, and D20's `CheckWaitingRoom`
    is not involved.
  - Each scene's instance starts with `nextRejoinMs = 0` (an immediate first try).
  - It is cleared on the welcome (218-222), on Cancel and Esc (228), and in the Steam branch.
- **ClientConnectionService as a RegisteredSingleton.**
  - There is one instance per scene: MainMenu and Game each bind it once (Plugin.cs:39,107).
  - `LobbyGuestPanel`, `SteamOverlayConnectionService` and `ClientConnectionUI` get the same instance through DI.
  - The only registry lookup is `RejoinFromGame` (196), and the desync dialog uses `ReplayService.GetSingleton`.
  - The service is missing only between `LoadMap`'s `SingletonManager.Reset` and the next scene, where no Rejoin
    dialog can be shown.
  - `MainMenuSceneLoader` is bound in both the MainMenu and Game contexts (MainMenuSceneLoading.decompiled.cs:142-149),
    so the new constructor parameter resolves in both.
- **Cancel semantics of the rejoin box.** Cancel and Esc call `StopRejoin(resetJoin: true)`, which closes only the
  quiet join (`EventIO.ResetIf(joining)`, 271-273). A Steam invite accepted while waiting replaces the box with its
  own Connecting box (`ShowConnecting` → `CloseConnectingBox`), and `WatchRejoin` doesn't show a second box while an
  overlay is on top (226). A2 is the exception.
- **EndSession(…, offerRejoin).** Only guests get the Rejoin and Stay buttons (ReplayService.cs:571-576, 762;
  ClientEventIO.cs:116). `EndSession` returns early when already desynced or failed (559), so there's never a second
  dialog.
- **The room's `separateAtStart`.**
  - `LobbyRoom.SetSeparateAtStart` changes the flag and `version` under `gate`, only for a shared save while Open
    (LobbyRoom.cs:139-150). Start's `CloseToNewcomers` moves the stage off Open, so later ticks are refused.
  - The lock order (`lobbyPumpGate` → `gate` in `PumpLobby`; `gate` alone from the main thread,
    TimberServer.cs:170-175) can't deadlock.
  - The roster frame carries the flag only when true (LobbyFrames.cs:275-282). The guest's inbox compares the whole
    roster text, so a flip is news and bumps `version` (LobbyInbox.cs:89-96,113).
  - `LobbyGuestPanel` re-says the note only when the flag changes (117-121, 183-190).
  - The host's callbacks check `session.Setup == setup` and `starting`, and Start disables the checkboxes
    (LobbyHostPanel.cs:228-242, 313).
- **SaveConversion.Pending's lifecycle.**
  - It is set only by `Start` for a save (LobbySession.cs:210-211), never by Cancel or by a room that never started.
  - It is cleared by `Fail` (325), by the main-menu configurator (Plugin.cs:104), and by the host's game, which takes
    it once loaded or drops it when not a host or already separate (SaveConversion.cs:42-52).
  - A game loaded by some other route can't pick up a stale one, because every hosted session goes through the main
    menu first.
- **The rc3 join-message change.**
  - Nothing reads the removed fields or properties (grep of `HostAllowsFounding`, `HostSeparateScience`,
    `foundingInSharedGame`: only the changelog and the checks).
  - The build handshake requires the same build on both sides, and Newtonsoft ignores unknown members anyway.
  - `AdoptHostChoice(joiningClosedAtStart)` (ColonySession.cs:78-82) is always called with true now.
- **The Host co-op game box's async read.**
  - `SaveColonyReader.Read` is pure and catches everything (SaveColonyReader.cs:42-59).
  - A faulted task caches null. A newer selection simply replaces `reading`, and the old result is dropped, so it's
    never shown for the wrong save (HostCoopFlow.cs:161-181).
  - The status label is found again, not duplicated, when the box re-opens in the same scene (126), and each main menu
    makes its own.
- **The LoadGame prefix's result and the in-game Host button.**
  - `__result` mirrors the game's "a save was selected" (the click sound), and returning false skips the original
    (ServerHostingUtils.cs:107-114).
  - In a game, the duplicated Host button is hidden (53).
  - The `[ManualMethodOverwrite]` comment matches 1.1.2.4's `LoadGameBox.LoadGame`
    (GameSaveRepositorySystemUI.decompiled.cs:587-599).
- **The original dialog is gone and nothing needs it.**
  - `LoadMap` serves the room's guests (the save arrives the same way).
  - `lobbyHostName` is always set for a room, so the loading tip always shows.
  - `ServerEventIO.Start(byte[])` and `mapProvider` are now used only by tests. That's harmless.

## 3. Docs

- **D1 (A1).** Several places describe Host co-op game in a single-player game's menu, which doesn't show:
  - README L121-124 (*Playing alone, open the game menu (Esc) and choose Host co-op game*);
  - TWO-COLONIES L85-86;
  - STABILITY-CHANGELOG rc4 (*Host co-op game in a game played alone*);
  - ALPHA-TEST-SCRIPTS Script H step 6.
- **D2 (A3, A5).** Several places say the buttons can be pressed in any order:
  - README L376-379;
  - the desync message (enUS CSV line 26, *in any order*);
  - STABILITY-CHANGELOG rc4 (*The order doesn't matter: the host and the guests can press their buttons either way
    round*).

  For a Steam guest who presses first, the rejoin ends with *The host has already started the game* (A3). For a
  direct-IP guest who presses first, the host gets a mods-differ dialog every 3 s when mod lists differ (A5).
- **D3.** ALPHA-TEST-SCRIPTS Script H step 9 (*the guest presses Rejoin quickly and the host waits a minute before
  confirming Save and Rehost*) can't be done as written. **Rejoin** appears only on *connection lost*
  (ReplayService.cs:571-576), and a guest's connection is lost only once the host has confirmed and left for the main
  menu (Plugin.cs:100). To test "guest first", use the desync dialog's Reconnect (step 10), or drop the guest's own
  network. Run it both over Steam (A3) and by IP with a differing mod (A5).
- **D4 (A8).** *A co-op host's button is Save and Rehost; a guest has neither* (STABILITY-CHANGELOG rc4, TWO-COLONIES
  L85-88, Script H step 7) holds only while the session lasts. After a desync, lost connection or failed action, a
  guest's menu shows Host co-op game.
- **D5.** The website still describes paths rc4 removed. The merge commit 04a0c5e left it at rc2, and Pages serves
  main:/docs, so this becomes live once main moves:
  - `docs/install.html:159`: *Load Game, select the save and choose Host co-op game* (removed from the Load Game box);
  - `docs/install.html:162-167`: *Options → Load Game → Host co-op game … works without a waiting room … choose Start
    Game, then wait, paused* (the original dialog, removed);
  - `docs/index.html:211,304`: Load Game → Host co-op game;
  - `docs/faq.html:185` and `docs/troubleshooting.html:285-286`: Reconnect without the main-menu wait.
- **D6 (A10).** STABILITY-CHANGELOG rc4 says the save line *is read off the menu's thread*. Only the parse is. The whole
  file is read on it.
