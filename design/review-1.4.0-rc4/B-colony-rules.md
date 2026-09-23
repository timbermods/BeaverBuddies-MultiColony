# Reviewer B: colony rules, determinism and the timing of the new game-state actions (rc2 to rc4)

Scope: `ebe3d4a..ebb6570` for the absence hand-over (rc2), the Game Mode page's colony choice and the guest's split (rc3),
and the shared save made separate at Start (rc4). Everything below was read in the mod and, where the game is involved,
in the decompiled game. Nothing was built or run. Line numbers are HEAD (`ebb6570`).

**Summary.** No desync and no session-stopper was found in the new replayed paths. The conversion
(`ColonyConversionEvent`), the split (`Found`'s split path) and the absence rule (`AbsenceReceiver` in `Seen`) read
only state that every computer has, in a fixed order, and play at the same point everywhere (section 2). The findings
are gameplay, UI and docs issues, plus a larger amount of code that rc4 left dead: every host now comes from a waiting
room that closes joining before the game loads.

| ID | Title | Status | Kind | Severity |
|---|---|---|---|---|
| B1 | A split or converted colony starts with Normal difficulty, not the game's, and the texts say "the game's" | Confirmed | gameplay, docs | low-medium |
| B2 | On a guest, Ctrl+T's absence status counts players who left as present, so the rc2 "not handed over" status is wrong | Confirmed | UI | low |
| B3 | A guest that loads before the host can act in the still-shared game before the conversion | Plausible | gameplay | low |
| B4 | The split and the conversion always give the shared colony to slot 0, which a stale slot table can give to someone other than the host | Plausible | gameplay | low |
| B5 | A converted shared co-op save takes the guests' science and unlocks by default, which goes against rc3's reason for sharing them | Confirmed (by design; needs a decision) | gameplay | low |
| B6 | The game menu's Found your own colony stays shown after someone else's split, and a click does nothing | Confirmed | UI | low |
| B7 | rc4 left the late-join machinery dead: HostStartGate, the tick-0 waits, "Joining: open" and `ServerEventIO.Start` | Confirmed | dead code | low |

---

## 1. Findings

### B1: A split or converted colony starts with Normal difficulty, not the game's, and the texts say "the game's"

- **Status:** Confirmed. **Kind:** gameplay and docs. **Severity:** low to medium.
- **Who it hits:** every guest who splits a shared game (rc3), and every guest who founds after a conversion (rc4),
  when the game was started on a difficulty other than Normal (Easy, Hard or a custom one). Also, a custom difficulty
  made with a mod.
- **Evidence:**
  - A new game records its starting settings only when it is separate. `MultiStartPatches.cs:56-59` (one start) and
    `76-79` (several starts) call `ColonyModeService.Enable(ReadStartingSettings(...))` only under
    `if (separateOne)` and `if (separateColonies)`. `ColonyModeService.Save` writes nothing when the game is not
    enabled (`ColonyModeService.cs:120-137`). So a shared game never saves its difficulty. Neither does a single-player
    game made with Separate unticked, nor a Stability Fork save.
  - The game has nothing else to read it from after a load. `GameModeSpec` is only in `GameSceneParameters` for a new
    game, and no game singleton saves it (searched the decompiled game for a `GameMode` or `Difficulty` singleton key;
    only the new-game and main-menu assemblies know `StartingAdults`).
  - `ColonyFoundingService.HostStartingSettings` (`ColonyFoundingService.cs:299-328`) then takes the default
    `GameModeSpec` (`IsDefault` first, so Normal), or the hard-coded Normal values at `313-315`.
  - The split (host's judge, `ColonyRulesService.cs:410-411`, then `Found` at `ColonyFoundingService.cs:398-407`) and
    the conversion (`SaveConversion.cs:53-55`, then `76`) both write that value into the event. The same value then
    becomes `ColonyModeService.StartingSettings` for good (`ColonyModeService.cs:152`), so every later founding gets it
    too.
  - What the players read: the split's confirmation `BeaverBuddies.Colony.Split.Confirm` (enUS csv line 529: *"starts
    with the game's starting beavers and goods"*), and README.md:335, which says the same. TWO-COLONIES.md:115 says
    *"the new game's, or for a save that did not record them the host's Normal difficulty"*. It doesn't say that a shared
    new game never records them.
- **Failure scenario:** Kyler makes a new game on Hard with Separate colonies unticked (rc3's page), and hosts it. A
  friend chooses Found your own colony. Their district center comes with Normal's 9 adults, 4 children and 130 food
  (or the host's modded default), not Hard's. The dialog had said the game's. The same happens with a Hard
  single-player save converted at Start: every guest's colony starts on Normal. The numbers are the same on every
  computer, so this is not a desync. It is a balance difference that contradicts the texts.
- **Proposed fix:** one of the following, in order of preference.
  1. Record the new game's starting settings in both branches of the prefix, whatever the mode. For example, add
     `ColonyModeService.RecordStartingSettings(ReadStartingSettings(startBuildingService))` before the `if`. Also save
     them for a shared game under a key of their own (such as `BeaverBuddies.StartingSettings`). The Stability Fork
     ignores singleton keys it doesn't know. This bends the beta7 rule that a shared save holds only what the fork's
     does, so it needs Kyler's decision.
  2. For the conversion only: the room's convert options offer a difficulty for new colonies (the game's own
     `GameModeSpec` list, Normal first), and the host writes it into `ColonyConversionEvent.startingSettings`.
  3. At the least, make the texts say what happens. `Split.Confirm`, README.md:335 and TWO-COLONIES.md:115 should say
     "Normal difficulty's starting beavers and goods: a shared game doesn't remember its difficulty".
- **Proposed check:** a StabilityTests source check that
  `Body(multiStart, "public static bool Prefix(StartingBuildingInitializer __instance)")` records the starting
  settings before `if (separateOne)` and before `if (separateColonies)`. With fix 3, a csv check that
  `BeaverBuddies.Colony.Split.Confirm` doesn't contain "the game's starting".

### B2: On a guest, Ctrl+T's absence status counts players who left as present, so the rc2 "not handed over" status is wrong

- **Status:** Confirmed. **Kind:** UI. **Severity:** low.
- **Who it hits:** guests in a mixed-factions game where the host has set a hand-over limit (not the default 0),
  after any player has left the session. Leaving is common: a lost connection can only rejoin after a Save and Rehost.
- **Evidence:**
  - `TradeOverviewPanel.cs:424` computes `present = ColonyLifecycle.PresentSlots()` on every computer.
    `PresentSlots` (`ColonyHandover.cs:289-296`) filters by `(EventIO.Get() as ServerEventIO)?.NetBase?.ConnectedPlayerIds`.
    On a guest that is `null`, and `connected == null` lets every entry through. A guest's session map is the host's
    last hello table (`ColonySlotService.Apply`, `ColonySlotService.cs:194-203`), and nothing ever removes a player
    who left. So a guest counts everyone who said hello this session.
  - `DescribeColony` (`TradeOverviewPanel.cs:563-583`) uses that set in two places: "playing" (`569`, already the
    case before rc2), and rc2's `lifecycle.AbsenceReceiver(slot, present) == null` at `576`. The receiver found is one
    whose player may have left.
  - The hand-over itself is decided from the host's own list: `HostDaily` (`ColonyHandover.cs:257-259, 275`), and
    `Seen` from the event's `presentSlots` (`314-335`). The guest's status therefore disagrees with what will happen.
    TWO-COLONIES.md:360-361 says the window shows the status "everywhere".
- **Failure scenario:** a mixed game with four players. The host plays Folktails, guest 1 Folktails, guest 2 Iron
  Teeth, and a fourth colony is Iron Teeth whose player is away. The limit is 7. Guest 2's connection drops. On the
  host, the away colony reads *not handed over: no colony of its faction is in the game*, and no warning ever comes. On
  guest 1 it reads *missed 5 of 7 days*, as if a hand-over were coming, and guest 2's colony still reads *playing*.
- **Proposed fix:** `ColonyLifecycle.Seen` keeps the presence it was told, for example
  `lastPresentSlots = new HashSet<int>(present)`, exposed as `PresentAtLastCheck`. The panel uses
  `host ? ColonyLifecycle.PresentSlots() : lifecycle.PresentAtLastCheck` for both "playing" and `AbsenceReceiver`.
  This is also what the warning uses, so Ctrl+T and the warning can never disagree. As an alternative, use it on the
  host too, for the same reason.
- **Proposed check:** a StabilityTests source check that `Seen` stores the event's slots, and that the panel's
  colonies refresh reads `PresentAtLastCheck` when it is not the host. A logic check on a small pure helper (the
  present set for display, given `isHost`, the connected list and the last presence) covering a guest after a player
  has left.

### B3: A guest that loads before the host can act in the still-shared game before the conversion

- **Status:** Plausible. It needs a guest whose game finishes loading before the host's, which can't be proven
  without running the game. **Kind:** gameplay. **Severity:** low.
- **Who it hits:** the guests of a shared save hosted with Separate colonies ticked, when the host's computer loads
  much more slowly than a guest's (a fast guest on a LAN, and a big save on a slow host).
- **Evidence:**
  - The host loads its scene as soon as every guest is queued (`LobbySession.Update`, `LobbySession.cs:266-286`). The
    guests load once they have received the save. Neither waits for the other.
  - The host sends the conversion only at its first frame with `ReplayService.IsLoaded` (`SaveConversion.cs:51-55`).
    `IsLoaded` is set 2 frames after its `ReplayService` starts updating (`ReplayService.cs:746-755`). A guest's
    actions (and its hello) that reached the host before that are in the IO queue.
  - `ReplayEvents` plays IO events first and the host's own queued events after (`ReplayService.cs:366-372`). So in
    that first batch a guest's `BuildingPlacedEvent` is judged while the game is still shared: `AllowOnHost` returns
    `true` at `ColonyRulesService.cs:200-201`. It is played, and the conversion then stamps it slot 0's
    (`ColonyStamps.Begin`, `ColonyStamps.cs:128-145`).
  - This stays deterministic, because every computer plays the same order. Only the ownership is surprising.
- **Failure scenario:** the guest is in first, sees a shared game and places a few paths and a Lumberjack Flag.
  Moments later the game is separate, and those are the host's. The guest can't remove them (`NothingOwn`), and is
  then offered to found a colony elsewhere. The more likely variant is harmless: a guest acts on its shared view before
  the conversion reaches it, and the host refuses the action under separate rules with a notice.
- **Proposed fix:** while `SaveConversion.Pending != null` on the host, refuse a guest's `ChangesGame()` action with
  `NotStartedYet`, in `AllowOnHost` before line 181. Hellos don't change the game (`ColonySlotService.cs:242`), so
  they still seat the guest. Alternatively, have `ReplayService.Initialize` (host) enqueue the conversion itself, and
  make its first `ReplayEvents` play the host's own events first.
- **Proposed check:** a StabilityTests source check that `AllowOnHost` refuses a guest's game-changing event while
  `SaveConversion.Pending` is set. Or a logic check on a pure rule
  `ColonyRules.WaitsForConversion(isGuest, conversionPending, changesGame)`.

### B4: The split and the conversion always give the shared colony to slot 0, which a stale slot table can give to someone other than the host

- **Status:** Plausible. It needs a shared save that carries a slot table, which only builds before 1.4.0-beta7
  wrote. **Kind:** gameplay. **Severity:** low.
- **Who it hits:** a shared save from 1.4.0-beta1 to beta6 that another player hosted before, when it is split or
  converted.
- **Evidence:**
  - The texts say the shared colony stays the host's (`Split.Confirm`, `Converted.Host`, csv lines 529 and 538).
  - The code hard-codes slot 0 in four places: `ColonyStamps.Begin` stamps 0 (`ColonyStamps.cs:139`),
    `ColonyModeService.Enable` passes `AdoptUnowned(0)` (`ColonyModeService.cs:157`), `ColonyScienceService.Enable`
    gives the pool and unlocks to `points[0]` and `unlocked[0]` (`ColonyScienceService.cs:246-252`), and an unowned
    district center reads 0 (`DistrictOwner.cs:30`).
  - The host's seat comes from the save's slot table: `SeatHost` calls `Table.Resolve(hostPlayerId) ?? 0`
    (`ColonySlotService.cs:103-106`). `ColonySlotService.Load` reads the table whatever the save's mode
    (`ColonySlotService.cs:83-87`). Only beta7 (`fa54267`) stopped saving it in shared games.
  - `ColonyRules.MayFound` decides who is the host from that seat (`ColonyFoundingService.cs:357-358`).
- **Failure scenario:** an old shared save in which a friend is slot 0 and Kyler is slot 2. Kyler hosts it and a
  third player splits it. Every building, mark and district center becomes slot 0's, the friend's. Kyler, the host,
  has no colony, although the confirmation said the shared colony stays his. `MayFound` also refuses the friend's
  split (their slot 0 owns the districts) but lets Kyler found, which contradicts "never the host".
- **Proposed fix:** in `ColonySlotService.PostLoad`, before `SeatHost()`, clear the table when
  `!ColonyModeService.IsSeparateColonies`. All the `Load`s have run by then, and every computer loads the same bytes;
  the hellos carry the host's table anyway. This makes the host slot 0 in every shared game, which is what the four
  hard-coded zeroes assume.
- **Proposed check:** a StabilityTests source check that `Body(slots, "public void PostLoad()")` clears the table
  when the game is not separate, before `SeatHost()`.

### B5: A converted shared co-op save takes the guests' science and unlocks by default, which goes against rc3's reason for sharing them

- **Status:** Confirmed as behavior. Whether it is a bug is Kyler's call. **Kind:** gameplay. **Severity:** low.
- **Who it hits:** the guests of a shared co-op save (one they helped build) that the host converts at Start.
- **Evidence:**
  - rc3 made the split keep one pool (`ColonyFoundingService.cs:403-407`, `separateScience: false`). The changelog
    gives the reason as *"the guest earned them too"*.
  - The conversion instead takes `Setup.ConvertScience`. Its default is the host's last New Game choice
    (`LobbyHostPanel.cs:224`), and that defaults to on (`NewGameColonyOptions.cs:23, 84`).
  - With it on, `ColonyScienceService.Enable(newGame: false)` gives the whole pool and every unlock to slot 0, and
    nothing to the other slots (`ColonyScienceService.cs:246-252`). The room's tooltip says so
    (`Lobby.Convert.ScienceTooltip`, csv 537).
  - The two paths are each deterministic and consistent with their own docs, but they disagree on who earned a shared
    game's science.
- **Failure scenario:** two friends play a shared co-op save for 20 cycles. The host rehosts it and ticks Separate
  colonies, leaving the science box as it was (ticked). The guest founds with 0 science and none of the unlocks they
  helped pay for. The split from the game menu would have kept them.
- **Proposed fix:** for a save with no separate colonies, start the room's Separate science box unticked. Or tick it
  only for a single-player save, if the reader can tell (it can't today). Or keep the behavior and make the room's gold
  line say that the guests start without the science and unlocks so far.
- **Proposed check:** a StabilityTests source check on `BuildConvertOptions` for whichever default is chosen.

### B6: The game menu's Found your own colony stays shown after someone else's split, and a click does nothing

- **Status:** Confirmed. **Kind:** UI. **Severity:** low.
- **Who it hits:** a guest who has the game menu open while another guest's split is played.
- **Evidence:**
  - The button's display is set only in the `GameOptionsBox.GetPanel` postfix (`SharedColonySplit.cs:62`, `97-102`).
    `GetPanel` returns the same root, and it is asked for when the panel is shown (Timberborn.OptionsGame,
    `GameOptionsBox.GetPanel` returns `_root`).
  - A click re-reads `Offered` and returns silently when it is false (`SharedColonySplit.cs:73-74`).
- **Failure scenario:** guest B opens Esc while guest A's founding is played. B presses Found your own colony and
  nothing happens: no dialog and no notice. B doesn't know the game is already separate and that Ctrl+K now works.
- **Proposed fix:** when `!Offered` in `Ask`, show the reason, such as `Founding.NotNeeded` or
  `Colony.Founding.SplitOther`'s hint ("found with Ctrl+K"), or hide the button there. Optionally refresh the
  button's display on `ColonyModeService.Enable`.
- **Proposed check:** a StabilityTests source check that `Body(split, "private void Ask(GameOptionsBox box)")` doesn't
  `return` silently on `!Offered`.

### B7: rc4 left the late-join machinery dead: HostStartGate, the tick-0 waits, "Joining: open" and `ServerEventIO.Start`

- **Status:** Confirmed. **Kind:** dead code. **Severity:** low. It is harmless today, but misleading: the code reads
  as if late joining still existed.
- **Evidence that no host can have joining open:**
  - The only `ServerEventIO` is made in `LobbySession.Open` (`LobbySession.cs:150-151`, `StartLobby`). The only
    `EventIO.Set` for a host is `LobbySession.cs:281`. The other `EventIO.Set` is the guest's,
    `ClientConnectionService.cs:163`.
  - `Start` calls `IO.CloseLobby` first (`LobbySession.cs:196`). That sets `stoppedAccepting` (`ServerEventIO.cs:74-77`),
    so `IsAcceptingClients` is false (`209`) before the game scene exists.
  - `Start` also calls `ColonySession.CloseJoiningAtStart()` (`LobbySession.cs:199`) for saves and new games alike.
    The guests' init event is built only after `SendMap` has awaited the room's save (`TimberServer.cs:527-531`,
    `824`), after Start, so it always carries `joiningClosedAtStart = true` (`ConnectionEvents.cs:82`).
- **Dead as a result:**
  - `HostStartGate` (whole class), `HostStartRules.ShouldHold`, the `TryHold` call in `ReplayEvent.DoPrefix`
    (`ReplayEvent.cs:193-195`), its binding (`ColonyConfigurator.cs:91`), and the `BeaverBuddies.Colony.Start.*`
    strings (csv 380-383). `HostStartGate.Confirmed` has no reader.
  - `ColonyRulesService.cs:98-108` (`WaitsForStart` is false whenever `JoiningClosedAtStart`) and `110-118` (needs
    `IsAcceptingClients`). Also `ColonyRefusal.NotStartedYet`'s two strings (`Refused.NotStartedYet`, csv 202;
    `Founding.NotStartedYet`, csv 226).
  - `ColonyFoundingService.WaitingForStart` (`152-154`), `offerPending` and `UpdateSingleton` (`177-183`, `210-219`),
    and the `NotStartedYet` branches of `WhyNot` and `SplitCanBeginNow` (`229`, `240`). TradeOverviewPanel's
    `started` is always true (`432-433`).
  - `ServerEventIO.Start(byte[])` (`ServerEventIO.cs:48-57`) has no caller. `StopAcceptingClients` and its two
    messages (`215-239`) are no-ops (`stoppedAccepting` is already true), and so are its two callers,
    `ReplayService.cs:424-427` and `960-963`. TimberServer's non-room join branch
    (`lobby == null && IsAcceptingClients`, `TimberServer.cs:567`) is unreachable.
  - The connection panel's *Joining: open* (`ConnectionPanelService.cs:358`, `ConnectionStatusModel.cs:197`,
    `BeaverBuddies.Panel.JoiningOpen`) never shows.
  - `ColonySession.JoiningClosedAtStart`, `CloseJoiningAtStart`, `AdoptHostChoice` and the init event's field are now
    always true, so they carry no information.
  - Still live, by design: `HostMayHandOver`'s wait for tick 1 (`ColonyHandover.cs:366-373`, `joiningClosedAtStart: false`).
- **Proposed fix:** remove the dead parts, and with them `WaitsForStart`'s third parameter. Keep `WaitsForStart` for
  `HostMayHandOver`. Or keep them as a safety net and say so in one comment where `ServerEventIO` is made. Either way,
  update the StabilityTests and RuntimeChecks that pin them (the HostStartGate and WaitsForStart checks).
- **Proposed check:** RuntimeChecks reflection: `ServerEventIO` has no public `Start(byte[])`, and `HostStartGate`
  is gone. Or, for the safety-net option, a StabilityTests source check that `new ServerEventIO` appears only in
  `LobbySession.Open`, and that `Start` calls `IO.CloseLobby(` before `EventIO.Set(IO)` can run (`Update`).

---

## 2. Found sound (checked, don't redo)

**The conversion (rc4)**
- **EventIO is the ServerEventIO before the game scene exists.** `EventIO.Set(IO)` runs before
  `LoadScene(...CreateGameSaveParameters(save))` (`LobbySession.cs:281-286`). `ReplayConfigurator` binds co-op
  services only when EventIO is set (`Plugin.cs:51`). `SaveConversion` is bound in every game scene
  (`ColonyConfigurator.cs:73`), and in single player it just drops `Pending` (`SaveConversion.cs:45-48`).
- **It is sent once and played once.**
  - `Pending` is cleared before the send (`SaveConversion.cs:52`), when a start fails (`LobbySession.cs:325`) and at
    the next main menu (`Plugin.cs:104`).
  - `Replay` returns if the game is already enabled (`SaveConversion.cs:74`). `UpdateSingleton` drops `Pending` in a
    separate game (`45`).
  - A rehost of a converted game offers no checkbox, because its save reads as separate (`LobbyHostPanel.cs:214`), and
    `Start` leaves `Pending` null for it (`LobbySession.cs:210-211`).
- **`DoPrefix`'s return value is read correctly on the host.** The host's `UserEventBehavior` is `QueuePlay`
  (`ServerEventIO.cs:37`), so `ShouldPlayPatchedEvents` is false (`EventIO.cs:101-114`) and a recorded event returns
  false: "not recorded" is false. The one silent loss is `HasReplayFailure`, which returns false at
  `ReplayEvent.cs:176` with nothing recorded. The session has failed by then, so this is negligible.
- **HostStartGate never holds it,** and neither does the local refusal. `IsAcceptingClients` is false (B7).
  `RefuseLocally` judges only `FoundColonyEvent` in a shared game (`ColonyRulesService.cs:354`).
- **Only the host may send it.** `ColonyRulesService.cs:89-96`. The host's own events get `player = 0` in
  `RecordEvent` (`ReplayService.cs:325`).
- **Every guest is offered the founding exactly once, whatever the seating order.**
  - If the guest's hello is played before the conversion, the hello's `OfferFounding` finds a shared game and does
    nothing (`FoundingAllowed` is false; not mixed). The conversion's `OfferFounding` then prompts
    (`SaveConversion.cs:83`).
  - If the conversion comes first (the usual case), its `OfferFounding` finds `LocalSlot == -1` and does nothing
    (`ColonyFoundingService.cs:184-188`, then `OfferFactionSwitch` returns at `622`). The guest's own hello later runs
    `Apply` and then `OfferFounding` (`ColonySlotService.cs:216-228`), which prompts in the now separate game.
  - `WaitingForStart` is false there, because the init event (`joiningClosedAtStart = true`) is written to the guest
    ahead of its queue (`TimberServer.cs:529-535`).
- **Slots in a shared save.** A shared save has no table (`ColonySlotService.cs:93`, since beta7). `SeatHost` makes
  the host slot 0 (`Resolve` on an empty table, `ColonySlotTable.cs:93-99`). Guests take 1 to 3 in hello order, as
  decided by the host and broadcast in every hello. A fifth player helps the host's slot (`ColonySlotService.cs:178`).
  After the conversion the table is saved, so each player keeps their slot on a rehost.
- **`Enable(newGame: false)` is deterministic.**
  - `ColonyStamps.Begin(true)` stamps every positioned, non-preview, non-Trading-Post building slot 0, and counts that
    as one digest change (`ColonyStamps.cs:128-145`). Previews are filtered (`PlacedBuilding`, `207-217`).
  - `AdoptUnowned(0)` reads only saved marks (`ColonyMarks.cs:136-153`). `ColonyScienceService.Enable(false)` reads
    the game's raw pool before `Enabled` flips (`ColonyScienceService.cs:240-246`), and leaves out profile-only
    unlocks (`244`).
  - The digest gate is open at that point, because `IsReplayingEvents` is true and `Enabled` is set before `Begin`
    (`Plugin.cs:181-182`, `ColonyModeService.cs:151-155`).
- **The digest and the daily check after the conversion.**
  - `ColonyDigest.Reset()` runs in `Initialize`, before any replay, on every computer (`ReplayService.cs:729-736`).
  - The conversion is played at the same tick (0 while paused) everywhere. The tick-1 heartbeat carries the digest once
    the game is separate (`972-978`).
  - The starting settings are part of the daily fingerprint (`ColonyDiagnostics.cs:237-239`). They come from the event
    (`SaveConversion.cs:53, 66, 76`), so they are the same everywhere even when a guest's mods change the default
    difficulty.
  - `ColonyLifecycle` starts counting days the first day after the conversion (`ColonyHandover.cs:193-200`).
- **The conversion's settings are allowed through the wire.** `ColonyStartingSettings` is already reached from
  `FoundColonyEvent`, so `ReplayEventBinder` allows it.
- **Crash sweep of `ColonyConversionEvent.Replay`.** Every service lookup is null-safe. The notices are in a
  try/catch (`SaveConversion.cs:79-88`). `Enable` is the same code rc1's shared-game founding already ran.
  `SaveConversion.UpdateSingleton` isn't inside a replay, and `HostStartingSettings` catches its own errors
  (`ColonyFoundingService.cs:303-310`).

**The split (rc3)**
- **`MayFound`'s actorIsHost.**
  - On the host's judge (`ColonyRulesService.cs:404-411`), `actorSlot` is `SlotOfPlayer(player)` and it is compared
    with the host's unshifted seat. An unseated guest (-1) is refused as a helper first (`ColonyRules.cs:341`).
  - On a guest's local judge and preview (`ColonyRulesService.cs:360`, `ColonyPlacementValidator.cs:39`),
    `SeatOfPlayer(0)` comes from the hello table. An unseated guest compares -1 with -1, but `actorHasSlot` refuses
    it first.
  - At replay `atReplay` skips it (`ColonyFoundingService.cs:357`).
  - The debug `HostSlotShift` makes a shifted host "not the host" on the judge, but the host can never open the tool
    in a shared game. `FoundingAllowed` needs `SharedColonySplit.Confirmed` (`158`), which only the guest-only button
    sets (`SharedColonySplit.cs:42`, `250`).
- **`Confirmed`.** It is reset in each scene's `ColonyFoundingService` constructor (`ColonyFoundingService.cs:95`).
  After a confirmation it deliberately stays set for the scene, so Ctrl+K can retry after a refused or skipped
  founding (the `Founding.Failed` text says "Try again with Ctrl+K").
- **Order inside `Found`.** `Enable` (and so `Begin`) runs before `PlaceCenter`, so the new district center isn't
  stamped 0 (`ColonyFoundingService.cs:400-413`). A founding skipped at replay leaves the game shared (`389-395`).
- **A third player after a split.** `LocalPlayerMayFound` becomes true (`IsSeparateColonies`). They get
  `SplitOther`, which points them to Ctrl+K and Ctrl+T, and the menu button is hidden (`SplitOffered` is false once
  separate).
- **`TellFounding`'s localIsHost** is `EventIO.Get() is ServerEventIO`, which is right on the host during a replay
  (`ColonyFoundingService.cs:668-669`).
- **Science on the split versus the conversion** is deterministic in both, with the value in code or in the event
  (`ColonyFoundingService.cs:406`, `SaveConversion.cs:76`). Only the design question in B5 remains.

**The Game Mode page and mixed factions (rc3)**
- **`ForNewWorld` is read in both start paths** (`MultiStartPatches.cs:56, 76`). The room branch applies only while
  `State == CreatingWorld && !IsSave` (`NewGameColonyOptions.cs:34-36`). `Start` sets that state before `LoadScene`
  (`LobbySession.cs:217-231`), and the new world's `StartingBuildingInitializer` and `FactionService.Load` run inside
  that scene. A loaded save never runs the prefix.
- **The statics' defaults** (`Separate` and `SeparateScience` true, Mixed false; `NewGameColonyOptions.cs:20-26`)
  equal the `ISettings` defaults the constructor reads (`83-85`). They match the old Mod Settings' defaults.
- **`MixedFactions.Decide`.** The room path needs `Setup.Mixed && Setup.Separate` (`MixedFactions.cs:108`), and
  `Setup.Mixed` already implies `Separate` (`LobbyHostPanel.cs:100`, `NewGameFactionCapture.cs:43`). The solo path takes
  what `Capture` recorded at `GameSceneLoader.StartNewGame` with `Requested` as it was then
  (`NewGameFactionCapture.cs:99-112`). The room doesn't call `StartNewGame`, and `Clear()` in `Decide`'s `finally`
  drops any stale capture.
- **A single-player Start with Separate ticked** makes a one-colony separate game, as `SeparateColoniesForNewGames`
  (default on) did before rc3. Hosting it later goes through the ordinary separate-save room.

**The absence rule (rc2)**
- **`AbsenceReceiver` gets the same candidates in `Seen` and `HostDaily`.** The host's `present` list is the event's
  `presentSlots` (`ColonyHandover.cs:257-263`), and `Seen` turns it into a set (`316`). `NearestLiving` walks
  `Distinct().OrderBy(s => s)` (`147`), so the order doesn't matter.
- **`NearestLiving` is deterministic.** The squared distances are integers, and a tie goes to the lowest slot
  (`160`, a strict `<`). The mixed path orders by distance and then slot (`FactionRules.cs:131-132`). Populations and
  district centers are tick-aligned, and `Seen` is played at a tick boundary everywhere.
- **E-3 holds with `hasReceiver`.** A hand-over needs `announced[slot]` from the previous day's `Seen` (which now
  requires a receiver in that day's presence, `ColonyHandover.cs:334-335`) and a receiver today (`275-276`). Without
  a receiver there is no warning and no hand-over, and the next day with a receiver warns first. `IsHandedOver`'s
  default `hasReceiver = true` in `HostDaily` is equivalent, because of the null check that follows.
- **Crash sweep of `AbsenceReceiver` inside `ColonyPresenceEvent.Replay`.** It is evaluated only when `IsDue` (so
  never at the default limit 0), and only on registered district centers. Nothing in it can throw on saved state.

---

## 3. Docs

- **README.md:335, and the in-game `Split.Confirm` (csv 529-533):** "starts with the game's starting beavers and
  goods". It is Normal's (or the host's default difficulty) for every shared game (B1).
- **TWO-COLONIES.md:115:** "(the new game's, or for a save that did not record them the host's Normal difficulty…".
  A shared new game never records them, so a split always gets Normal (B1).
- **TWO-COLONIES.md:360-362:** the Ctrl+T status "everywhere". On a guest it can differ from the host's after a
  player leaves (B2). The same passage, and **README.md:271-273**, say the warning comes "the day the count reaches
  the limit". In a mixed game with no receiver of its faction it comes on the first day one is present (rc2).
- **`BeaverBuddies.Colony.Handover.Tomorrow` (csv 384):** "their colony is handed to the nearest colony". In a mixed
  game it goes to the nearest colony of its own faction (rc2). README.md:263-266 has it right.
- **ALPHA-TEST-SCRIPTS Script S, steps 6-7:** after a guest has once confirmed Found my colony and left the tool, their
  Ctrl+K opens the founding tool directly (`Confirmed` stays set for the scene), no longer "points to the menu". Worth
  a sentence in step 7 so a tester doesn't report it.
- **ALPHA-TEST-SCRIPTS Script H, step 4:** "the guest is offered to place a district center" is right, but usually
  through the guest's own hello rather than the conversion itself (section 2). If a tester sees the prompt a moment
  after the host's notice, that is expected.
- **STABILITY-CHANGELOG rc4:** "*The in-game Start the game? prompt (HostStartGate) is no longer reached*" is
  accurate. The rest of the late-join code it implies is listed in B7.
