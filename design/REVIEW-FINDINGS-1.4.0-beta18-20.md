# Review findings: the waiting room and mixed factions, Timber Together 1.4.0-beta18 to beta20 → fixed in 1.4.0-beta21

**Reviewed:** `v1.4.0-beta20` (`79d4757`), and the beta18 and beta19 changes inside it (from `v1.4.0-beta17`), with
Timberborn 1.1.2.4. The review followed [REVIEW-PLAN-1.4.0-beta18-20.md](REVIEW-PLAN-1.4.0-beta18-20.md).

**How it was done:**

| Who | Leads | Report |
|---|---|---|
| **A**: the join and the waiting room, with rigs | E4, E5, J1–J11, X4's parser | [A-join-and-waiting-room.md](review-1.4.0-beta18-20/A-join-and-waiting-room.md) |
| **B**: mixed factions against the decompiled game | M1–M15, E3 | [B-mixed-factions.md](review-1.4.0-beta18-20/B-mixed-factions.md) |
| **C**: what reaches everyone, compatibility, the checks themselves | E1, E2, E6, E7, C1–C3, T1, S1 | [C-everyone-and-compatibility.md](review-1.4.0-beta18-20/C-everyone-and-compatibility.md) |
| **The main reviewer** | X1–X4, T2; merging the fixes | Here, and J1, found while planning |

- The three ran in parallel on a frozen copy of beta20, against all 497 decompiled `Timberborn.*.dll`.
- Every finding was re-checked against the mod and the game before it was fixed. For example, B-1's premise was
  checked in `Timberborn.Characters`: `CharacterPopulation` drops a character only on `CharacterKilledEvent`, and
  `DestroyCharacter` is kill-then-delete.
- **Nothing was played.**

**Status key:**
- **Confirmed:** traced end to end in the mod and the game, or reproduced by a rig or a check.
- **Plausible:** the path exists, and one link couldn't be proven here (Steam, timing).
- **Refuted:** checked and found sound.

**The checks:**
- StabilityTests 391 → **410**:
  - 11 rigs (`JoinReviewChecks`); four of them fail on beta20's code (J1 premise, J2/J4, J10 flood, A-new-3);
  - 3 rule checks of the fixed join (`JoinFixChecks`);
  - 5 checks named after findings (`ReviewBeta21Checks`). Each fails on beta20's source.
- RuntimeChecks 349 → **359**, on both builds:
  - 8 from C (`BinditoChecks`, `HarmonyResolutionChecks`, `PatchGateChecks`). They pass on beta20, which had none of
    these faults, and fail on a build with four planted bugs that every older check passes.
  - 2 from B (`ReviewBeta21RuntimeChecks`). They fail on beta20's DLL.
- Both builds with 0 warnings.

---

## 1. Summary

**Who it hits:**
- **All**: every player, whatever they use.
- **Room**: the waiting room.
- **Mixed**: mixed-factions games.

| ID | What | Hits | Status | Kind | beta21 |
|---|---|---|---|---|---|
| **J1** | A waiting-room guest never loads the world. It is left on a blank main menu and must restart the game | Room | Confirmed | **session-stopper** | Fixed |
| **B-1** | The faction switch deletes living beavers without killing them. The next explosion or Beehive check throws for everyone, and a later rejoin desyncs | Mixed | Confirmed | **crash** | Fixed |
| A-new-3 | A guest the room has let go is read without the room's gate for up to 2 s. A session fault it sends ends the host's game at load | Room | Confirmed (rig; needs a modified client) | session-stopper | Fixed |
| J2/J4 | A guest removed by the 10 s rule just as it enters the game is let in as a new player, then closed | Room | Confirmed (rig) | broken feature | Fixed |
| J10a | One guest's frames make the room rewrite the roster thousands of times a second; slower guests are taken out | Room | Confirmed (rig; needs a modified client) | broken feature | Fixed |
| B-2 | A switch before the first tick (tick 0 of a waiting-room game) misses the colony's beavers: new center, old-faction beavers | Mixed | Confirmed | gameplay | Fixed |
| J5a | The start message carries the previous session's speed boost, also for classic Save and Rehost (since beta5) | All | Confirmed | gameplay | Fixed |
| J5c | A mixed save room tells its guests no host factions, and a mixed new game only sometimes | Mixed | Confirmed | gameplay (display; the host judges) | Fixed |
| C-S1 | A guest keeps an earlier session's allowed factions. The faction icon and shaft models keep an old scene's objects. The warning and the locked-faction notice leak into the next game | Mixed | Confirmed | gameplay / memory | Fixed |
| C-C1 | With a faction mod installed, a mixed game loads a third faction's content. A mod that reuses a faction's templates makes them common for everyone | Mixed | Plausible | gameplay / crash | Fixed: new games are mixed only with exactly two factions |
| C-E2 | One `PatchAll` for everything: after a game update, one broken mixed-factions patch stops the whole mod | All | Confirmed (risk) | session-stopper after an update | Fixed: patched last, in a catch that turns mixed factions off |
| J5b | The speed-limit latch records no session in a waiting room | Room | Confirmed | very low | Fixed |
| J8a | A welcome and an end read in one frame: the guest is told nothing, and "Connecting to …" stays up | Room | Confirmed (rare) | display | Fixed |
| A-new-1 | "Connecting to …" comes back late, over the waiting room's page or after a failed join (Steam overlay) | All | Confirmed | display | Fixed |
| A-new-2 | Start counts the room before closing it | Room | Plausible (tiny window) | gameplay | Fixed |
| J11a | A Steam lobby made after Start opens for friends | Room | Plausible | display | Fixed |
| B-3 | After a switch, the colony's paths keep the old faction's look until reload | Mixed | Confirmed | display | Fixed |
| B-4 | Nothing checked a character's faction or needs | Mixed | test gap | detection | Fixed: the daily check, mixed games only |
| C-E7 | Two always-null fields on the wire; `FoundingToolActive` allocates per preview block | All | Confirmed | cosmetic / performance | Fixed |
| C-E6 | The save room reads nearly all of a save's singletons on the menu thread (35–56 ms under .NET 8) | All (save rooms) | Confirmed | performance (minor) | Comment corrected; no worker thread until a playtest shows a hitch |
| C-T1 | Checks weaker than their names: the text gate scan, the stacking scan, name-only targets, the binding check | All | Confirmed (four planted bugs pass) | test gap | Fixed: new RuntimeChecks |
| B-5, T2 | Docs: "a beaver only eats its own faction's food"; save rooms "show no colony"; beta20's "a room that is not mixed sends beta19's frames" | – | Confirmed | docs | Fixed |
| J8b | A world-making scene that stops updating never times out, and the guest's watchdog never fires | Room | Confirmed | display | **Left** (sketch in A) |
| J11b | Guests never leave the host's Steam lobby, so its 8 places can fill while the room shows free ones | Room | Plausible | broken feature (low) | **Left**, documented |
| E5 | Accepting an invite from inside a co-op game ends that game first; the D20 box then leaves no session | Room | Confirmed | gameplay | **Left**, documented (a product choice) |
| B-6 | A single-start new game uses the "no faction in hand" warning (right answer) | Mixed | Confirmed | logging | **Left** (now once per scene, not per process) |
| – | "Release Steam", the zip's build, compiles without C# optimisation (the SDK only turns it on for "Release") | All | Confirmed | performance (small) | **Left**: same behaviour, and exact line numbers in `Player.log` while nothing is played |

---

## 2. The findings that mattered

### J1: a waiting-room guest never loads the world (Confirmed; since beta18)

- `ClientConnectionService.LoadMap` called `SingletonManager.Reset()`, then `RegisteredLocalizationService.T(...)` for
  the loading tip. After the reset nothing is registered, so the tip threw a NullReferenceException, `NotifyEach`
  swallowed it, and the load never started.
- In the same frame, `CheckWaitingRoom` found no `LobbyGuestPanel` (the registry was empty) and took the D20 branch:
  "this player is in a game", so drop the client.
- On the next frame the guest's page popped, and the main menu's `GetPanel` postfixes threw on the empty registry.
  The guest was left with no panel and no input.
- Classic joins never took the tip branch.
- **Fix:**
  - The tip is worded before the reset, and the reset runs just before the load.
  - `CheckWaitingRoom` stands down once `LoadMap` has the save (`JoinFlowRules.CheckWaitingRoom`, set on the same
    thread).
  - The two `GetPanel` postfixes are null-safe.
  - `RegisteredLocalizationService.T` shows the key rather than throwing when nothing is registered.
  - D20 (welcomed while in a game) and classic joins behave as before.
- **Checks:**
  - `JoinFixChecks` (the decision table);
  - `JoinReviewChecks` (the premise);
  - `ReviewBeta21Checks`: `LoadMap` reads nothing from the registry after the reset.
- **Test:** Script D7a (the start), which no guest could pass before.

### B-1: the faction switch deletes living beavers without killing them (Confirmed)

- `SwitchFaction` removed the colony's beavers with a bare `EntityService.Delete`.
- The game takes a character out of `CharacterPopulation` and `BeaverPopulation`, its district and its home only on
  `CharacterKilledEvent`. So the deleted beavers stayed in those lists after Unity destroyed them.
- The next explosion (`CharacterExploder`) or Folktails Beehive check read their `Transform` and threw, and every
  computer went to the crash scene.
- A player who loaded after the switch (a rehost, a reconnect) had no such beavers, and their draws differed: a desync.
- **Fix:** `Character.DestroyCharacter()`, which kills then deletes, as the game does for a child that grows up and a
  Wonder's pilot. The journal only notes the colony of a killed beaver; its death line comes from the game's own
  death notice, which a kill doesn't post.
- **Checks:**
  - `ReviewBeta21Checks`: no bare `Delete` of a character anywhere in the mod;
  - `ReviewBeta21RuntimeChecks`: the game's premise, by IL, and the switch uses `DestroyCharacter`.
- **Test:** Script F11a.

### B-2: a switch at tick 0 misses the colony's beavers (Confirmed)

- Beavers made with a colony (a start, a founding) join a district only when `DistrictCitizenAssigner.Tick` runs. The
  waiting room's tick-0 save is written before any tick.
- So a switch accepted while paused at tick 0 found an empty `DistrictPopulation`: it replaced the district center and
  kept 13 Folktails beavers.
- **Fix:** the switch also takes the colony's beavers still waiting for a district: alive, of the colony's faction,
  and nearest (the game's own `DistanceToCitizen`) to one of its centers. It walks them in entity order.
- **Check:** `ReviewBeta21RuntimeChecks` pins the premise.
- **Test:** Script F11b.

### The waiting room's server (A-new-3, J2/J4, J10a, A-new-2)

- Each waiting-room member's fate is now claimed once, atomically: `TryEnterGame` or `TryLeave`. So the join and the
  room's removal can't both win.
- A waiting-room server admits only its members to the game.
- Every connection stays behind the room's gate until it enters the game.
- A hello is taken once, and the room writes at most once every 50 ms.
- Start closes the room, then counts it.
- Classic hosting is untouched by all of this: a scripted classic session is byte-identical between beta17's and the
  fixed TimberNet (A, E4).

### J5: the start message, built before the host's game exists (J5a, J5b, J5c)

- A waiting room's start messages are built on join threads while the host is still in the menu (a save) or between
  scenes. Three values were read from state that didn't belong to the new session:
  - the speed boost: whatever the last session left;
  - the speed-limit latch: no session;
  - the host's factions: the menu's `MixedFactions.IsOn`, which is false.
- **Fix:**
  - The boost is reset when hosting starts.
  - The session is passed to the latch.
  - A mixed room latches the host's factions at Start.
  - Those factions are forgotten when a session begins (the host's `BeginHostSession`, a guest's join). They are not
    forgotten per scene: C's first fix forgot them as the host's game scene was set up, which a slow guest's start
    message could still follow.
- **Check:** `ReviewBeta21Checks`.

### C-E2: one `PatchAll` for everything

- Every one of the 273 patch classes (274 in the Steam build) resolves cleanly against 1.1.2.4, and every injected
  parameter is bound (C, by Bindito's and Harmony's own code).
- But `PatchAll` was all-or-nothing, and the game doesn't catch an exception from a mod's start.
- **Fix:** `Plugin.PatchAllIsolatingFactions`. It is exactly Harmony 2.4.1's `PatchAll` (checked in the decompiled
  Harmony: every type with a Harmony attribute, `CreateClassProcessor(type).Patch()`), with `BeaverBuddies.Factions`
  patched last inside a catch that sets `MixedFactions.Unavailable`.
  - No game method is patched by both groups, so no patch changes order.
  - Mixed factions' patches that did apply stay inert, because each is gated (checked by IL, `PatchGateChecks`).
- A failure anywhere else still stops the mod, as before: co-op can't run on part of its patches.

---

## 3. Found sound (don't redo)

- **E1:** every new service resolves in Game (single-player and co-op), MainMenu and MapEditor (C, `BinditoChecks`).
- **E3:**
  - The decision never runs in the map editor.
  - A non-mixed game loads beta19's collections in the same order.
  - The catalog can't be built early (B).
- **E4:**
  - A classic session is byte-identical on the wire from beta17 to beta20 (3 runs each).
  - Every changed classic hunk is inert (A).
- **E7:** a non-mixed save is structurally identical to beta19's. The `CharacterFaction` decorator writes nothing
  outside a mixed game (C).
- **J2:** every waiting-room connection passes `AdmitToLobby`, which refuses once the room is closed; only J4's race
  reached `StartQueuing`.
- **J3:** a desynced guest's Reconnect to a host that rehosted from the main menu gets the D20 box. That is as
  designed; the in-game Save and Rehost stays classic.
- **J4, the thread pool:** with the pool starved to two free threads, all 7 guests' paced saves were queued in
  2.5–3.1 s, well inside the 10 s budget.
- **J6:**
  - The host loading its file while guests load captured bytes is beta17's own classic pattern.
  - `StartSaveGame(ref)` equals `LoadScene(CreateGameSaveParameters(ref), tip)`.
- **J7:** the host loads only once every member is queued; tick-0 actions use the paused-replay path (beta12 R10). A
  founding at tick 0 is still unplayed (Script D8).
- **J9:**
  - Every game-thread wait is bounded.
  - The lock order is pump → room → lane, and `queuedMessages` → lane.
  - The stream lock is never nested.
- **J10:**
  - 300 fuzzed frames, a 10 MB gzip bomb, deep JSON and wrong types were all dropped.
  - Every bad length closed only that guest.
  - A hello after Start changes only the page.
- **R6:** the loading-screen hold is released on every exit that can run.
- **M2:** every character creation site in 1.1.2.4 is covered, and loaded beavers get their saved faction before
  `Awake` (B, `m2_creation_sites.py`).
- **M3:**
  - Every district center is free and placed finished, so there is no "free unfinished center".
  - The old center's cells are freed at once.
  - The deletion interrupt doesn't split the replay.
- **M4:** every tool exit the toolbar refresh can trigger is display only. The `LocalSlot` reads in `Found` and
  `SwitchFaction` only clear the local pick and move the camera.
- **M5:**
  - The decal default comes from the building's own template.
  - The resource counter's list only feeds its dropdown.
- **M6:** no crash sites beyond the plan's list. B checked 157 `Single`/`First` sites and 43 readers of `Current`.
- **M7:** all 20 replaced method bodies match the game apart from the intended faction choice; the texture setter
  makes exactly one draw.
- **M8–M15:**
  - Stockpiles, planters and yields follow the template on creation, on load, and after a handover.
  - Wonder completion writes only the local profile.
  - The footprint assumption holds in 1.1.2.4.
  - Trading allows exactly 17 goods plus Science and no Beavers, and every check agrees.
- **X1:** `LobbyRules.SeatingPlan` gives the same slots as `LobbyWorldMaker.SeatInRoomOrder`:
  - the same table and the same `Resolve`, in the same order;
  - a guest without an id is left unseated by both;
  - a guest with the host's id is seated at slot 0 by both, which the plan reads as no pick.
- **X2:**
  - A pick reaches the world only through the tick-0 save or a founding the host judges.
  - A pick after Start is dropped (the room isn't Open), and `StartedWith` is frozen at Start.
  - A guest who never picked follows the host's faction.
- **X3:** a save room's predicted colonies match the game's for everyone the save remembers. A brand-new player's
  predicted colony can differ, because the game seats in hello order and the room in room order. That is display only.
- **C2, your own mods:**
  - None patches a beta18–20 target.
  - HungryPathing is safe: `GetNeedSpec` returns null, and every call is behind `HasNeed`.
  - MixedStorage only sees the faction-filtered goods.
  - LateGamePerformance's background save completes before the room reads it.
  - Separately, and not a beta18–20 issue: LateGamePerformance pins `ColonyStamp.Save` by a raw-IL hash, which
    changes with every Timber Together build, so the pin hasn't matched since beta2.
- **C3:** beta17–19 saves load as not mixed.

---

## 4. Left, and why

- **E5, accepting an invite while in a co-op game.** Both possible fixes change a Steam join flow that can't be tried
  from here:
  - (a) ask "Leave this co-op game to join …?" before connecting;
  - (b) install the new connection only once its save arrives.

  The case is rare (a player in one co-op game accepting another's invite). It is documented in TWO-COLONIES'
  *Known limits*, and Script D12b watches for it.
- **J11b, Steam lobby members.** The fix, leaving the Steam lobby when the room ends or the player leaves, also needs
  Steam to try. It shows only after many rejoins. Documented; Script D6b.
- **J8b, a world-making scene that stops updating.** The host's game is broken anyway, and the guest can press Leave.
  The watchdog change is sketched in A.
- **C-E6 on a worker thread:** only if a playtest shows a hitch.
- **B-6:** the warning is now once per scene. Pushing the base faction for the vanilla single-start spawn would be one
  more patch for a log line.
- **The unoptimised "Release Steam" build:** behaviour is identical, and while nothing has been played, `Player.log`
  line numbers help more. Worth turning on after a playtest (`<Optimize>true</Optimize>` for that configuration).

---

## 5. After the release: what the first playtest found (1.4.0-beta23)

The first time the waiting room ran in a game (beta21), it had never opened at all:

- **The page's constructor threw.** `MainMenu/NewGameTemplate` marks its content slot `content-container="true"`, so
  `CloneTree().ElementAt(0)` reached the empty slot. The plan's §14 had settled on that call, and this review read it
  (A's J8 and E4 traced the page's logic, not its construction). **The lesson:** a UI Toolkit call that a static read
  takes for granted needs a check against the game's own files. RuntimeChecks now reads every template the mod takes
  as one element and fails on a content slot.
- **The name box drew broken** in the main menu. It was the in-game settlement box, whose style sheets are the Game
  scene's.
- **A guest accepting in the Steam overlay** got the page on half the screen, under the main menu.

All three are fixed in 1.4.0-beta23. See the changelog.

## 6. New test-script lines (ALPHA-TEST-SCRIPTS)

- **D6b:** a guest leaves and rejoins by invite five times; each rejoin works.
- **D7a:** the guest's game loads after Start, which no guest's did in beta18 to beta20.
- **D10b:** Start pressed the moment the room opens; a friend's Steam list must not offer "Join game".
- **D12b:** a guest in a running co-op game accepts a waiting-room invite. Expect the D20 box; note what the old game
  does.
- **F11a:** after a switch, set off dynamite or wait by a Folktails Beehive. Expect no crash.
- **F11b:** a switch at tick 0 in a mixed waiting-room game. The colony's beavers become the new faction's.
- **F13a:** with a faction mod installed, the room says mixed factions is for Folktails and Iron Teeth.
