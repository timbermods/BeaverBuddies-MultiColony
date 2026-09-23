# Changelog

Every change this fork makes relative to the original BeaverBuddies `v1.1` branch at commit
`a13b1f20dacb6e30efa967cc8ac83e73779c0755` (24 August 2026), built against Timberborn
1.1.2.4. For a plain-language summary, see the [README](README.md). Future releases add a new
entry above the current one.

## 1.4.0-beta19

**The waiting room for saves.** Asked for right after beta18 (the maintainer's answer to the plan's question D1):
**Load Game → Host co-op game** in the main menu now opens the same Co-op Game page as a new game, instead of the
plain dialog. Hosting from inside a game (Options → Load Game, and Save and Rehost after a desync) keeps the dialog,
as the maintainer chose: the waiting room is a main-menu page, and a rehost's guests reconnect as before. **Wire
change** (a waiting room's summary can describe a save); no save change.

- `ServerHostingUtils.LoadAndHost`, after the game's own checks of the save: where the main menu's
  `LobbyHostPanel` is (a game does not bind it), it reads the save's bytes and opens the room for them
  (`LobbyHostPanel.OpenForSave`); otherwise the old server and dialog.
- `LobbySetup.Save` / `SaveBytes`: at Start there is no world to make; the save's bytes go to every guest at once, and
  the host's page stays up (*Starting…*) until every guest's join is queued, then loads the same bytes as the hosted
  game (EventIO set, random state from the bytes), as a new game's hand-off does. Joining closes at Start, so after a
  save's waiting room founding, colony switching and stewardship work at once too.
- `LobbySummary.ForSave` (the settlement, the save's name and in-game date): the plate shows the settlement, the gold
  line the save's name and date as the Load Game box words them (`TimestampFormatter`; an autosave is the game's own
  *Autosave*, in each player's language); no faction logo ring (a save's metadata names none) and no colony numbers
  (a save seats each player by who it remembers, which the menu does not read).
- A guest of a save hosted from the main menu joins from the main menu, as for a new game; accepting its invite in a
  game now says to go back to the menu (a hosted save could be joined from a game before).
- Found in the review before release and fixed: an autosave showed its raw file name; the setup's copy of the save's
  bytes is dropped once they are handed over.
- Docs: README (*Start a game*, steps 3 to 5), TWO-COLONIES (*Starting*, *Known limits*), STEAM-INVITES,
  CONNECTION-PANEL, ALPHA-TEST-SCRIPTS (Script D, lines 15 to 15b), the site.
- Checks: StabilityTests 373 (1 new: a save's room tells its guests the save's name and date and guesses no colony),
  RuntimeChecks 339 (1 new: Host co-op game opens the room in the main menu and keeps the dialog in a game, and what
  it reads of a save is still there). Both builds, 0 warnings.
- Not seen in a game. Script D line 15 is this release's (and beta18's lines before it).

## 1.4.0-beta18

**A waiting room for new co-op games: invite players and ready up before the world exists.** Asked for by the
maintainer to clean up getting player 2 in: before, the host made a new game alone, saved it, went back to the menu,
loaded it with Host co-op game and waited paused, and player 2 could found only once the host unpaused. Now **Host
co-op game** sits beside **Start** on the New Game difficulty page; friends join a **Co-op Game** page (a page of the
game's own New Game wizard), mark themselves ready, and everyone starts together at tick 0 with joining already
closed, so a guest founds its colony at once, even paused. Designed in `design/PRE-GAME-LOBBY-PLAN.md` (the
maintainer's decisions D1 to D6: new games only, the host may start with someone not ready after a question,
joining closes for good at Start, the host keeps the map's district center and the others found when they please,
Folktails is the tested case, and the look must be the game's own). **Wire change** (a waiting-room phase before the
save, and one field in the host's first message); no save change.

- **The network phase** (`TimberNet`: `LobbyFrames`, `LobbyRoom`, `LobbyInbox`; `TimberServer.OpenLobby`,
  `ReleaseLobby`, `CloseLobbyToNewcomers`, `RemoveFromLobby`, `CancelLobby`). After the build check a guest comes into
  the room, gets its player number, and is read for its hello (stable id, name) and ready only: anything else it
  sends before its save is dropped, a frame over 64 KB closes it. The host's welcome, roster and state go to each
  guest every second from a `SendLane` of its own (off the game thread, which is busy making the world; a guest that
  stops reading is taken out and holds up nobody), marked by a -1 length so hosting a save sends the same bytes as
  before. Each guest's join waits on a `TaskCompletionSource` completed with the saved world (its continuations never
  run on the releasing thread), then goes on exactly as a guest of a hosted save: queued, the save, the state, the
  first event. Guests still waiting are closed with the server, can be removed, and are told why the room ended.
- **The host** (`BeaverBuddies/Lobby`): `LobbyPatches` (the button, greyed with Start), `SettlementNamePanel` (the
  game's own `Game/SettlementNameBox` with Cancel and Next; the name is checked as the game checks it and goes into the
  new game's configuration), `LobbyHostPanel` and `LobbyPage` (the page: `MainMenu/NewGameTemplate`, keyed before it
  is initialised, the Game Mode page's summary plate and the faction logo ring, the Mods window's board and rows, the
  faction page's status line, the map page's wide Invite Friends button, the game's yes/no boxes). `LobbySession`
  keeps the server outside EventIO until the save loads, so the scene that makes the world is single player; at
  Start it closes the room, fills a multi-start map's starts for the players present (at most the Players field), and
  makes the world behind the held loading screen (`LoadingScreenDisablePatcher`). `LobbyWorldMaker` fills the colony
  slot table in the room's order, queues the tick-0 save (`<date> Co-op start`), reads it a frame later, sends it,
  and once every guest is queued loads it as the hosted game (EventIO set, random state from the save's bytes). A
  failure tells the guests why and leaves the host in a solo game with the reason.
- **The guest**: a *Connecting to …* box (`ConnectingBox`, the game's dialog with Cancel) after Join co-op game or an
  accepted invite, in place of "Joined! Receiving map..."; `LobbyGuestPanel`, the same page titled with the host's
  name, with **I'm ready** / **Not ready** (or its row's checkbox) and **Leave**; the page says why when the room ends
  (and the join-failed dialog stays quiet then); two minutes without a word asks *Keep waiting* / *Leave*; a waiting
  room that answers while the player is in a game is left with a note to join from the main menu; the loading screen
  names the host's game.
- **In the game**: `ColonySession.JoiningClosedAtStart` (told to guests as `InitializeClientEvent.joiningClosedAtStart`)
  makes `ColonyRules.WaitsForStart` hold nothing back, so founding (and its prompt), colony switching and stewardship
  work at tick 0; the host's **Hand to …** buttons still wait for the first tick. *Joining: open* and *Start the
  game?* never show (the server's session is latched as closed at Start). The connection panel marks a guest still
  loading (`PanelPlayer.Loading`, in every co-op game).
- Found in the review before release and fixed: waiting-room writes no longer hold a shared lock while a guest's
  connection might block (the host's game thread took that lock); the keep-alive stops once everyone has the world;
  the guest page's "keep waiting?" resets for a new room; a new join forgets the last room's host.
- Docs: README (*Start a game*), TWO-COLONIES (*Starting*, *Known limits*, *How it works*), STEAM-INVITES,
  CONNECTION-PANEL, ALPHA-TEST-SCRIPTS (Script D), WORKSHOP and the site. English strings only.
- Checks: StabilityTests 372 (17 new: the waiting room's network phase over pipes with the real handshake, a guest
  that stops reading, its rules, the seating order, its strings and style-sheet scope, founding without waiting after
  a waiting room, the panel's loading mark), RuntimeChecks 338 (7 new: every game member and patch target it uses,
  the save-before-unpause order, UI.zip's templates, names and the main menu's style sheets, the init event's flag,
  and who reads it). Both builds, 0 warnings.
- Not seen in a game. Script D is this release's.

## 1.4.0-beta17

**Another colony's district's migration controls are greyed out.** Found playing beta15: in the Migration tab (F7),
with the other colony's district chosen in the window's district list, its automatic migration row (the minimum, −
and +, and the two toggles) could be clicked. Every such change was already refused (the Entities and Migration
scopes, on the player's own computer and on the host; the host's log showed its own district's changes played and
the other colony's manual migration refused), but the game's toggles are set only when a row is made, so a refused
toggle went on looking changed.

- `ColonyMigrationControls` (display only): `PopulationDistributorBatchControlRowItem.UpdateRowItem` greys the row
  out on a district that is not this player's colony's (`ColonyViewService.IsOwnDistrict`), whose toggles then show
  the district's real setting, its owner's changes included. This player's own rows are left as the game has them
  (set from the toggle at once, played a tick or a round trip later), so they do not bounce back while waiting.
- `ManualMigrationPopulationRow.SetButtonsEnabledState`: the 1, 10 and all buttons are greyed out while either
  district of the manual panel is another colony's (they were refused with a notice before).
- No wire change, no save change. RuntimeChecks 331 (1 new: the two patches' targets, and that the controls ask whose
  district it is, grey out and show the setting). StabilityTests 355.

## 1.4.0-beta16

**Five changes asked for after playing beta15**, where trading posts and exchanges worked (goods and beavers crossed,
the ledger filled). **Wire change** (a Trading Post's placement is judged differently).

- **A Trading Post may be placed before its roads.** beta15 refused one without a road from each of two colonies at its
  ends; the roads are now what it needs to work, not to be placed. It trades once its two halves are in two different
  colonies' districts (`TradingPosts.JoinsTwoColonies`, unchanged), and its panel says *Not trading yet* until then.
  `ColonyGameWorld.RoadConflict` answers None for a Trading Post before reading the map; `TradingPostRoads`, its
  message, `ColonyRoadRule.TradingPost` and `FarEntrance` are gone. A post still carries no road, so the road rule
  needs no exception beyond it.
- **Other mods' per-building settings follow ownership.** MixedStorage's warehouse and pile goods could be changed on
  another colony's building through its own menu: its `StorageAllocationEvent` declares no colony scope, so the host
  allowed it ("declares no colony scope; allowing it"). An event that declares none but names one building in a public
  string field called `entityID` (this mod's convention, and MixedStorage's) is now judged as a change to that building
  (`ColonyRules.ScopeByEntityField`), on the player's own computer (refused there with *That belongs to another
  colony.*) and on the host. No change to MixedStorage. Each such event type is logged once.
- **The trading posts and colonies window moves.** Drag it by its title badge or its frame (not by its lists and
  buttons); it stays on screen, and opens where it was left for the rest of the game. The footer says so.
- **A Trading Post's All Posts button** no longer draws over the top of the panel's scrolling part: the header row has
  a height of its own and a gap below it. The button (and the docs) now say **All Posts**.
- **Ctrl+L reads at a glance.** beta15 drew each colony's roads with the game's area outline (thin lines along the
  path edges) in the colonies' own colors, which `StartingLocationPlayer.PLAYER_COLORS` lightens for text: on a beige
  path at night they were nearly invisible. Now every road cell is a filled square, the game's own tile marker (the
  one it draws a building's range with: `MarkerDrawerFactory`'s mesh and material, built into `AreaTileDrawer`'s
  mesh once per change, on the game's UI layer, lifted clear of the path models), in a strong version of each
  colony's hue (`ColonyRoadOverlay.Palette`). A road piece of several levels (a district center, stairs) gets squares
  on its bottom level only. It shows by itself while a building or the founding tool is in hand (no longer with the
  planting, cutting or demolishing tools), and at any time with Ctrl+L. If the game has no marker to lend, the
  outlines are drawn as before.
- Checks: StabilityTests 355 (1 new for other mods' events; the 2 Trading Post road tests removed), RuntimeChecks 330
  (5 new: a Trading Post placed with no roads, the host judging another mod's per-building event, the window's drag,
  the header's room, the road overlay's squares and colors; the 3 checks of the post's far road end removed). Against
  beta15's DLL the new ones fail.

## 1.4.0-beta15

**No more land: build anywhere, and two colonies' roads meet only at a Trading Post.** Asked for after
playing beta14. Land (every tile within 10 tiles of a colony's buildings, first come) was more trouble than help: a Trading
Post had to sit exactly where two colonies' land met, and a post could still be placed with the same colony's road at
both ends (a screenshot showed one between two of one player's roads). The mod is played with friends, so it no
longer guards where anyone builds. What is left is the one rule that keeps the game's districts working: two
colonies' roads never join, except through a Trading Post. **Wire change** (the colony digest and the daily check
changed), and a save change: land is no longer saved, and an older save's land is ignored.

- **No land.** `ColonyReach` and `ColonyReachGrid` are gone, with everything that read them:
  - the placement refusal on another colony's land (`OtherColonyArea`) and the refusal next to another colony's
    buildings;
  - founding's 20 tiles from other colonies (`TooCloseToColony`): a colony may be founded anywhere its district
    center's roads would not join another colony's;
  - marking only where a colony may work (the `Tiles` scope): planting and tree cutting are marked anywhere, and a
    tile another colony marked stays theirs as before;
  - the land fallbacks in the simulation: wild bushes, ruins, piles and recovered goods that nobody marked go to
    whichever colony's workers get there first (`ColonySeparation.MayTake`, `NaturalOwnerOf`; the recovered-goods
    patch is removed);
  - the land outlines, the `land=` part of the daily check, the report's land lines and the `land` digest notes.
- **Kept:** who placed each building (`ColonyStamp`), now in `ColonyStamps`, which still gives a building nobody
  placed as an action the owner of its district or of the road at its entrance (no longer of the land under it).
  Ownership is unchanged: a player changes only their own colony's buildings, districts and marks.
- **The road rule** (`ColonyRoadRule`, pure and tested headlessly; the game side in `ColonyGameWorld`, as the host
  judges it and as the preview shows it). Refused, with *That would join another colony's roads. Colonies' roads meet
  only at a Trading Post.*:
  - a path, stairs or anything else that carries a road (the game's `PathSpec`: also bridges, gates, tubeways, zipline
    stations and district centers) on or beside another colony's road, finished or still being built;
  - a path on the cell in front of another colony's building's door (that building would join the placer's roads);
  - a building whose entrance cell (where its road goes) is on or beside another colony's road.
  Anything else may stand right beside another colony's buildings and roads. The game's own check still refuses
  whatever would join two districts' finished roads, and zipline links to another colony's tower are refused as
  before.
- **A Trading Post needs two colonies' roads when it is placed**: one at each half's entrance, from two different
  colonies, one of them the placer's (`TradingPostRoads`: *A Trading Post needs a road at each end: yours at one,
  another colony's at the other. Build both roads up to it first.*). The other half's entrance is worked out from the
  half being judged (straight through the post, where the game's `HalvesCoordinates` puts it), so each half, the
  preview and the host give the same answer. Both halves are still judged together.
- **The game's names for a building's door, checked:** its `PositionedEntrance.Coordinates` is the cell outside the
  door where the road must be; its `DoorstepCoordinates` is the building's own cell inside. The rule reads the first.
- **Ctrl+L shows colonies' roads** (`ColonyRoadOverlay`): each colony's paths in its color, while any tool other than
  the default is in hand, and at any time with the key (now *Show colonies' roads*). The debug seat switch
  (Ctrl+Shift+K) moved with it.
- Founding's own check as it is played still reads only the tick-updated district map; the new check for another
  colony's paths still being built runs in the host's judgement and the preview only, so every computer's replay
  gives the same answer.
- Texts: the founding prompt, the refusals, the handover notices (no more "land"), the Trading Post's description
  and the overview's empty line.
- Checks: StabilityTests 356 (6 new for the road rule, 11 land tests removed), RuntimeChecks 328 (6 new: the game puts
  a building's road at its entrance cell; the rules read that cell and see paths still being built; the game places a
  Trading Post's second half turned round behind the first; its paths, stairs, bridges, gates and district centers
  carry `PathSpec` and the Trading Post does not; each faction's post has its other road end where the rule looks for
  it. 5 game methods the rule reads are listed; the recovered-goods patch's is dropped).

## 1.4.0-beta14

**Dev mode's *Add 1000 Science* is shared, so buying a building with it no longer desyncs a co-op game.** Found
playing beta12 (2026-09-22, two players over Steam, separate colonies and separate science). The host turned dev mode
on, clicked *Add 1000 Science* in the dev panel, then placed Platforms, which were still locked: the game unlocks the
building first and pays its 100 science. The science had been added on the host's computer alone. When the unlock
was played on the guest, the host's colony had no science there, so the guest skipped it (*Not enough science to
unlock Platform.Folktails for slot 0 any more; skipped*), and the every-tick colony check stopped the guest at that
tick: the host's list of changes had a `science-spend` and an `unlock` that the guest's lacked, then the same eight
`land` changes (the Platforms, placed on both). Placing them on water had nothing to do with it.

- The dev panel's button (`ScienceAdder.AddScience`) is now an action like dev mode's free unlock
  (`ScienceAddedEvent`, `Events/ToolEvents.cs`): played on every computer, into the clicking player's colony with
  separate science (the one pool otherwise).
- As with the free unlock and *Finish now*, the host refuses it while its own dev mode is off, and from a player it
  has not seated yet (who has no colony to add to).
- The dev mode notice, the README, TWO-COLONIES.md and the site name it with the other two shared tools. Dev mode's
  other tools still change only the computer they are used on, and desync the game.
- **Wire change** (a new event type; the join already requires the same build).
- RuntimeChecks 318 (4 new: the game still has `ScienceAdder.AddScience`; the patch is on it and records the 1000 it
  adds; the host's two refusals; the replay adds the science). The shared-event list check names the new event.
  Against beta13's DLL the four fail.

The rest of that session played as it should: a founding, then 268 ticks of both players building, with the colony
check agreeing at every tick until the science; then both computers' logs named what differed. The README, the
site and TWO-COLONIES.md now say the colony model has had this one short session.

## 1.4.0-beta13

**Clearer tooltips for the two colony settings.** Players read *Separate colonies for new games* and *Allow founding
colonies in a shared game* as two halves of one choice, and turned the first off and the second on to get separate
colonies in a new game. That works, but the long way round: the new game starts shared and the first founding splits
it, so the other player's colony gets the host's Normal difficulty start instead of the new game's settings. The
tooltips now say what each is for (Mod Settings, two lines each, still within 112 characters):

- *Separate colonies for new games*: "Host only, and only for new games. On: each player gets their own colony. Off: all players share one colony." / "A save keeps the mode it started with. To split a shared save you already have, use the next setting."
- *Allow founding colonies in a shared game*: "Host only, for a shared save (one colony for all players): lets a player without a colony found their own." / "The first one founded splits the save into separate colonies for good. Off: shared saves stay shared."

The settings, their defaults and what they do are unchanged. README, TWO-COLONIES.md and the site's install page
describe them the same way, and the StabilityTests check on the tooltips now also asks that the first names the
second. No wire change, no save change.

## 1.4.0-beta12

**A desync and network review of beta11, and its fixes.** Two review agents and the main one went over everything
that touches synced state and the network. Each finding was checked against the decompiled game (1.1.2.4) before it
was fixed or refuted. The plan and the full report, with the evidence for every item, are in
[design/REVIEW-PLAN-1.4.0-beta11.md](design/REVIEW-PLAN-1.4.0-beta11.md) and
[design/REVIEW-FINDINGS-1.4.0-beta11.md](design/REVIEW-FINDINGS-1.4.0-beta11.md). **Wire change.**

**Built on the Stability Fork 1.1.14.** After this release, the same day, the fork took these fixes as its 1.1.14
(its PR #55), without the separate-colony one. MultiColony records 1.1.14 as merged (MC11, a merge with no file
changes): the fork's own additions (its host check for a building it lacks, its quiet guest leave) were already here.

Desyncs:
- **Wonders run on the tick** (the Stability Fork's PR #46, closed there, ported with one change).
  - What ran on render frames: every Wonder's activation and deactivation animation, and the whole of the Earth
    Repopulator's plane launch (the catapult's wait, the runway, the launcher's turn between planes).
  - What their ends did: spawned the planes (entities created outside any tick, whose IDs draw the shared random
    numbers), deactivated the Wonder and started the pilots' timer. So each computer did these at a different point.
  - Now `WonderTickService` steps them once per tick with the game's own code, and the frames only draw between ticks
    (`Fixes/WonderTimingFix.cs`, [the design note](BeaverBuddies/Doc/WonderTiming.md)).
  - The change from #46: a game update that changes how often those methods read the frame clock no longer throws
    out of the mod's one `PatchAll` (which would have left the mod half patched). It leaves the method as it is, logs
    once, and switches the whole takeover off, so the Wonders fall back to frame time.
  - The host also decides whether a Wonder can be activated (`WonderActivatedEvent.activated`), since the game's
    check reads animation state.
  - The always-on walker hash leaves out switched-off walkers, so pilots riding their planes don't log a mismatch.
- **A deleted entity is gone for everyone at the same moment.**
  - Why it was alive: Unity destroys a deleted entity at the end of the frame, and until then the game's own "does it
    still exist" check says yes.
  - Why that desynced: a tick is spread over a different number of frames on each computer. So in a later bucket of
    the same tick, a lumberjack walked on to a tree an explosion took on one computer, and gave up on the other.
  - Now a deletion inside a tick or a replayed action ends that frame's ticking on every computer
    (`Fixes/TickTimingFixes.cs`). The frame's unticked buckets are given back to the game's ticker, so the game runs
    no slower (an interruption used to lose them), and a pending save waits for the end of the tick.
- **Loading keeps the non-game random marks.** In the first frames of a game, sounds and input the mod marks as not
  simulation still drew the shared random numbers, so a sound one player heard and another did not moved one random
  state. The rule is now `RandomSourceRules` (checked on its own): marked methods first, and another thread never
  draws the game's.
- **The rest:**
  - The game's walker debugger (debug mode) no longer draws the shared random numbers.
  - The desync diagnostics files are named with real GUIDs (they drew game random numbers on computers with debug
    data only).
  - The patched `Time.time` starts at 0 as a game loads, instead of the last session's value (only animation read
    it).
  - A real GUID asked for on another thread no longer affects the main thread's.

Stopping everyone, and a false alarm:
- **A setting for a building that is gone** (an automation change replayed on a building demolished in the same tick)
  is skipped on every computer; it threw and ended the session.
- **A building from a mod only some players run.**
  - The host refuses a guest's placement or unlock of a building the host's game does not have, and tells the guest.
  - A guest that meets a building the host used and it lacks leaves quietly, like beta9's unreadable action, while
    the host and the others play on.
  - Both used to stop everyone.
- **The daily colony check no longer hashes the seat table.** The host changes it as it loads and hands it to guests
  only inside a hello, so a guest whose hello was refused was stopped at its next daily check. No guest simulates
  anything from it.

Behaviour:
- **Spring-return levers** switch off in the tick, at once, on every computer.
  - Before, the tick's own switch was taken for a click on each computer: played a tick late, sent again by every
    guest, and refused with a notice for the other colonies' players.
  - Holding one on never worked in co-op, and still doesn't.
  - Also: a paused building deleted in the tick no longer records a "resume".
- **Saves wait for the water and soil simulation.**
  - What was wrong: a co-op save skips the game's full-tick finish, rightly. But the game then refused the save, the
    mod's workaround silenced the refusal, and the save read the water arrays while their threads wrote them.
  - Now a save waits for those threads first.
  - Only the players' pause saves at once. A stop for another reason (a guest waiting, the host easing off) finishes
    its tick first.

Network:
- **Guests follow the host's pace.** When the host eases off for a slow guest, the other guests kept the full speed,
  reached every tick early and stood waiting for it: stop-go at every tick, worse the more the host eased. The
  heartbeat now carries the host's pace (`HeartbeatEvent.hostSpeed`, left out at full speed), and guests run at it
  (`CatchUpSpeed.PaceFor`).
- **One stalled direct-IP guest no longer freezes everyone.**
  - The host wrote each tick to every direct guest on its game thread. A guest that stopped reading (asleep, its
    Wi-Fi gone) froze the host, and so every other guest, until its connection gave up (measured: 3.6 s to over 20 s).
  - Each direct guest now has an ordered send lane of its own (`TimberNet/SendLane.cs`).
  - A guest that takes nothing for 30 seconds, or has 16 MB waiting, is dropped, as over Steam.
- **Receiving on threads of their own.** Each connection is read by a dedicated thread above normal priority. A pool
  thread waited tens of milliseconds for a turn while the game's workers kept every core busy.
- **Steam at once.** What the host sends while paused, a guest's own action and a desync notice are handed to Steam at
  once, not at the next frame's pump.
- **Pacing at boosted speeds.** The host's easing thresholds grow with the speed above 7. At speed 30 with a high ping,
  a guest in step read as behind, and an eased host never climbed back.
- gzip's fastest level: much less time on large actions, on the host's game thread.

Checks:
- StabilityTests 360 (12 new) and RuntimeChecks 314 (36 new, 14 of them the fork's Wonder checks).
- After the release, the same day: on GitHub's runner the stalled-guest check found the guest never stalled (the
  runner's socket buffers took the whole 2 MB). It is now two checks: real sockets for "the host never waits", and a
  connection that really blocks for "the guest is dropped after the limit". StabilityTests 361; the mod is unchanged.
- Run against beta11's code, the new checks for these fixes fail (the report lists which pass there, and why).
- Not played: Script B lines 8t to 8y and Script C 1b.

## 1.4.0-beta11

**Two guests joining over direct IP at once no longer freeze the host.** Left open in beta10 (the port's review found
it; it was older than the port). As a guest's join finishes, the host sends it its start message
(`InitializeClientEvent`) ahead of what was queued for it while its save went out. That message was written straight
to every guest (`TimberServer.SendEventToClients` with `sendNow`), under the lock every broadcast takes. A second guest
still receiving its save over a direct connection has its stream held by its own join thread for the whole paced save
(about 1 MB/s), so the write waited for the rest of that save, and the host's game thread, whose tick broadcasts need
the same lock, stood still with it: about a second for each MB of the save still to send. Now the start message is
written at once only to the guest whose join is finishing (its stream is free); every other guest gets it the usual
way, queued for one still receiving its save (sent in order after it) and written to the rest. Over Steam nothing was
paced, so this happened over direct IP only.
- The start message holds only the host's session choices, the same for every guest, so a guest getting another's a
  little later changes nothing. The joining guest still gets its own first.
- A new check: a guest whose save goes out at 1 KB/s is still downloading while a second guest joins over an unpaced
  link; the second gets its save and its start message, and a tick broadcast from the host finishes at once (before
  the fix, neither happened until the first guest's save was done).
- Checks: StabilityTests 348 (1 new), RuntimeChecks 278. Not played.

## 1.4.0-beta10

**Built on the Stability Fork 1.1.12**, and on the change the fork merged after it (its PR #50). Most of 1.1.12 was
MultiColony's own fixes ported into the fork (planting with sliced views, the placement replay validator, tick once,
dev mode's Ctrl keys, closing joining at tick 0, the frame type binder); the fork's own changes are ported here, with
the fixes the fork's reviews made to MultiColony's code. MultiColony keeps its own behaviour where it went further (a
guest that cannot read the host's action leaves quietly, and the host keeps a group's readable actions; beta9).
- **A fuller desync check** (fork SF6, SF-NF1). A guest compared only `Random.state.s0`, one of the xorshift state's
  four words. Every event now also carries a hash of all four (`ReplayEvent.randomStateHashBefore`), and every
  heartbeat the host's entity-order hash (each tick bucket's size and a rotating eighth of its entity IDs) and
  walker-position hash (every walker's root position as exact float bits), taken as the tick starts
  (`HeartbeatEvent.entityOrderHash`, `walkerPositionHash`; `DesyncDetecter/DesyncCheck.cs`). A random state that
  differs stops the session as before; an entity or walker difference alone is logged once per game (`Entity
  mismatch`, `Walker mismatch`) and the game goes on, so a later desync says when the games first differed.
  `TEBPatcher` keeps its hashes in every multiplayer game now, reset as a game loads. What the check found travels as
  the desync's trace, so the host's log and the report name it; MultiColony's colony-digest line does the same. The
  colony digest, the daily colony check and beta9's lists of colony changes are unchanged.
  `ReplayService.HandleDesync(reason, colonyHostChanges)` is one method for both. **Wire change.**
- **Direct TCP sends at once** (SF7): `NoDelay` on every socket the mod makes or accepts (`TCPClientWrapper`), and
  `SendDataWithLength` sleeps between chunks only for the save sent to a joining guest (on that guest's own thread);
  a gameplay frame over one chunk used to stall the host's game thread about 31 ms per extra 32 KB. Ending the
  session no longer waits for a joining guest's paced save. MultiColony's single first write (the length and the
  frame's start together) stays.
- **The desync dialog** (SF5): the sentence asking to press Enable Logging shows only with that button (public builds
  have no upload token), and a guest's **Reconnect (wait for Rehost)** joins the way it joined: the address it
  typed, or the host's new Steam lobby when Steam shows it, else a notice to accept a fresh invite
  (`Connect/DesyncDialogPlan.cs`, `ClientConnectionService.Reconnect`). It used to dial the saved direct-IP address,
  127.0.0.1 unless changed, for a Steam guest too.
- **Every recording prefix runs first** (the fork's #50, done for MultiColony by its own PR #9, whose version and
  check this build keeps). A prefix that records a player's action carries `[HarmonyPriority(Priority.First)]` (65,
  tick once's shared pause among them): another mod's prefix on the same method now runs inside the replay on every
  computer, not at the click on one, and cannot stop the action from being recorded (Harmony skips later bool
  prefixes after one returns false). MixedStorage's `SingleGoodAllower` prefixes rely on it. Prefixes that replace
  the game's method run `Priority.Last` (now also `DistrictPreviewsValidatorReplayPatcher` and dev mode's two
  Ctrl-key prefixes). RuntimeChecks finds the recording prefixes in the compiled IL and requires First.
- **From the fork's reviews of MultiColony's code:** tick once after a failed multiplayer action no longer ticks the
  stopped game, and pressed on a computer held at speed 0 while the shared game runs (a guest waiting for the host,
  a host easing off) it records the shared pause instead of the *Tick once is off* notice. Closing joining is safe
  inside a replay (a server that never started, a Steam lobby that throws). A guest refused because joining closed
  during the build check is told why, and the refusal is written outside the lock every broadcast takes.
- **New here:** joining closes as the first tick starts, before anything of it is sent. It closed after tick 1's
  events went out, so a guest admitted in between had the tick-0 save and never got tick 1 (the fork's reviewer
  noted it; neither had fixed it).
- The unused file-replay classes are gone (`RecordToFileService`, `FileWriteIO`, `FileReadIO`; fork #42). Tests: the
  status-traffic check waits for the init event and the two ping sessions run one after the other (fork #40, both
  flaky on a runner); CI pins the .NET 8 SDK and restores from nuget.org.
- Not ported: the fork's identity, version and site changes; its end-to-end planting check (MultiColony's planting
  code is the fork's, and its own checks cover the colony parts). History: the fork's `main` at `3a2cc2f` is recorded
  as merged (an ours-merge, as for 1.1.11).
- Checks: StabilityTests 347 (25 new), RuntimeChecks 278 (8 new). Not played.

## 1.4.0-beta9

**The four things beta8 left.**
- **A guest that cannot read the host's action leaves on its own.** In beta8 a guest that got an action it could
  not read (one from a mod only the host has) stopped with `AbortSession`, which sends the host a session fault: the
  host and every other guest stopped too, and each was told the action "may have changed only part of the game
  state" and to reload a known-good save, though nothing had been played. The guest now only closes its connection
  (`ClientEventIO.LeftOverUnreadableAction`, `ReplayService.AbortReplay(reason, leaveQuietly)`), as if it had quit:
  the host sees it leave and plays on with the others, whose games are whole. Its dialog says that nothing of the
  action was played, no save is harmed, and how to join again (install the mod the message names, or the host stops
  using it; then the host saves and rehosts). A failed action, and a fault from another player, stop everyone as
  before.
- **The host refuses a guest's unreadable action, not its whole tick.** A guest sends a tick's actions as one group,
  and beta8's host dropped the group when one action in it could not be read. A host now reads such a group an action
  at a time (`NetIOBase.ReadableActionsOf`, host only: `KeepsReadableActions`): the readable ones are kept in their
  order and read again through the type binder as one group, and each lost one (an unreadable action, an empty entry,
  a group inside the group) is logged and refused to that guest on its own (`ActionRefusedEvent`). A frame whose
  fault is the group's own, or that has no readable action, is refused whole, as before. What the host keeps is what
  every computer plays, so nobody goes out of step. A guest still keeps nothing of a frame from the host it cannot
  read.
- **Another colony's death alert stays out of your alert panel.** The game puts the *died tragically* alert on the
  beaver's own entity, which it has taken out of its district by then, and a thing in no district counted as
  everyone's. `ColonyViewService.IsOwn` now falls back to the colony the journal recorded as the beaver died or left
  its district (`ColonyJournal.RecordedOwnerOf`, the rule `JournalFilter.IsOwn`), so the alert counts, the batch
  control window's lists and the journal agree; a beaver cut off from its district goes by its last colony there too.
  As such an alert comes on it also made every player's alert row blink: a prefix on
  `NotifyingStatusMonitor.OnStatusToggled` skips that for another colony's subject. The event it posts is only for the
  alert panel (RuntimeChecks checks that nothing else in the game names it), so nothing simulated changes.
- **A desync's lists start where the colony checks last agreed.** beta8 logged each computer's last 256 changes,
  and one tick can count thousands (a mark notes one per tile), so the change that differed could already be gone,
  and the host, logging later, listed different numbers. A guest now notes how many changes it had counted at every
  heartbeat whose digest matched (`ColonyDigest.Agreed`, reset at load), the desync event carries that count and the
  host's count at the check that differed (`ClientDesyncedEvent.colonyChangesAgreed`, `colonyChangesHost`), and
  every computer logs its changes from the next one on (`ColonyDigest.DescribeSince`), with a line where the host's
  check came. The ring keeps the last 16384 changes (about 900 KB, made once, still nothing allocated per change);
  if more than that were counted since the agreed count, the list says which changes it no longer has.
- Checks: StabilityTests 322 (2 new: the alert rule, the agreed window), RuntimeChecks 270 (2 new: a host keeping a
  group's readable actions in order, and the death alert's entity, blink event and patch). Not played.

## 1.4.0-beta8

**Seven reviewed pull requests** (timbermods/BeaverBuddies-MultiColony#2 to #8), each reviewed again before merging;
the review's small fixes went onto the pull requests' branches first.
- **Over Steam, the host checks who a guest is (MC3, #2).** The host seated a guest by whatever stable id its
  `PlayerHelloEvent` claimed, and every player's id is sent to the whole session and kept in the save, so a guest
  could take another player's colony by claiming their id. The host now compares the hello with the Steam ID Steam
  proved for the connection (`SteamLinkSocket.RemoteSteamId`, through the new `IVerifiedIdentity`): a hello that
  claims another Steam ID is refused, and a guest whose game could not read its own Steam ID is seated by the proved
  one. On every join, a connection already seated cannot say hello again as someone else, an id with a line break
  or a `|`, or longer than 64 characters, is refused (it could add rows to the saved slot table), and ids and names
  go into the host's log on one line (`ColonySlotTable.ForLog`). **Direct IP joins are still taken at their word**,
  and the host listens for them in a Steam-invite game too (README: *Which joins are verified*). Host-only; no
  simulation, wire or save change.
- **A received frame can only create actions (MC7, #5).** Frames are read with Newtonsoft's
  `TypeNameHandling.All`, so a `$type` in one named any loaded type to create. `ReplayEventBinder` now lets through
  only `ReplayEvent` types and what their fields carry (strings, numbers, enums, the mod's structs, lists of them);
  anything else, a list, array, map or Nullable of it, a Unity object, a delegate or a reflection type is refused
  before it is created. Another mod's actions (MixedStorage's `StorageAllocationEvent`) pass with the classes they
  declare. The JSON written, and so the event hash, is unchanged.
- **An unreadable frame stops the session cleanly (MC-NF1, #5).** A guest that receives an action it cannot read
  (one from a mod only the host has, a refused type) now stops the session with a reason naming the type and its
  assembly, and plays nothing more of that tick; before, it skipped the tick's actions and drifted out of step. A
  host that cannot read a guest's frame logs it (its first 500 characters), keeps the guest's other frames, carries
  on and sends that guest an `ActionRefusedEvent` for each action it lost ("The host could not accept that
  action."); before, a malformed frame could throw out of the host's tick.
- **Each journal is its own colony's (MC2, #3).** The notification journal still showed the other colony's deaths
  (the game takes a dying beaver out of its district before it posts the death, and "no owner" counted as
  everyone's) and, after a reload, every saved entry (listed before the guest is seated). `ColonyJournal` records
  the colony a beaver dies in or leaves (`Character.KillCharacter`, `Citizen.UnassignDistrict`, read-only
  prefixes), saves whose each journal entry is (`BeaverBuddies.ColonyJournal`, separate-colonies saves only), and
  lists the panel again once the player is seated; `JournalFilter.ShouldShow` is the rule. An entry saved by an
  earlier build whose beaver is gone is hidden. Display only. The other colony's death alert still shows for about
  a day in the alert panel (same cause, not yet changed).
- **Only the founder hears how a founding went (MC4, #7).** Every player got the founder's notices, warnings
  included ("could not be founded there after all ... Try again with Ctrl+K"). The others now get a plain notice,
  *A new colony has been founded: <name>.*, and the founder's success is no longer styled as a warning
  (`ColonyRules` decides the text and style).
- **The host's placement check puts the random state back (MC1, #7).** The host checks a replayed placement on a
  throwaway copy of the building; the copy's components wake as it is made, and another mod's building could draw
  Unity random numbers there on the host only. `UnityEngine.Random.state` is now saved before the copy and put back
  in a `finally`, before the copy is destroyed. No change for the game's own buildings.
- **The desync log lists each computer's last 256 colony changes (MC6, #4).** On a desync in a separate-colonies
  game every computer logs `Colony changes here as ... desynced`, each change with its number and the digest it
  left: lined up by `#n`, the first line that differs between two players' logs is the change they did not make
  alike. The ring allocates nothing and is cleared with the digest at load; the log is caught so it cannot stop the
  rest of the desync handling. One tick can count more than 256 changes (a large mark, a hand-over), so the change
  that differed can already be out of a list.
- **The planting replay's levelling override runs last (#8)**: `[HarmonyPriority(Priority.Last)]`, as in the
  Stability Fork's port; another mod's prefix on `TerrainAreaService.InMapLeveledCoordinates` now runs first.
  Nothing changes without such a mod.
- **Stability Fork 1.1.11 is recorded as merged (MC8, #6)**: a merge commit with no file changes, so the next sync
  from the fork does not meet the 11 conflicts of the hand port again.
- **CI (#7):** StabilityTests and the Python snapshot tests run on GitHub Actions (Windows, .NET 8) for every push
  and pull request. Two checks that time real threads against the wall clock are only a warning there; a crash or
  any other failure fails the build.
- Checks: StabilityTests 320 (19 new), RuntimeChecks 268 (29 new). Not played.

## 1.4.0-beta7

**With separate colonies off, a game is the Stability Fork's.** A shared-colony game (a new game with the setting
off, a shared save, a Stability Fork save) still ran parts of the colony model: it kept land and stamped buildings,
counted them in a digest that could stop a guest, took a daily colony check, saved owners, a lifecycle entry and a
table of players' ids and names, and its District Crossings held 100 of a good. All of that is now for
separate-colonies games only, so a shared game plays, checks itself and saves as the Stability Fork does.
- **Founding in a shared game is its own setting, off by default.** *Separate colonies* did two jobs: whether a new
  game has a colony per player, and whether a player may found a colony in a shared save, which splits it into
  colonies for good. At its default (on), hosting an old shared save offered every guest without a district a
  founding at the first tick. Now **Separate colonies for new games (beta)** (on) decides new games only, and
  **Allow founding colonies in a shared game (beta)** (off) decides the second: latched when hosting starts and told
  to guests (`Settings.FoundingInSharedGames`, `ColonySession.HostAllowsFounding`,
  `InitializeClientEvent.foundingInSharedGame`). A separate-colonies save allows founding whatever it says. Ctrl+K in
  a shared game that does not allow it says so.
- **District Crossings hold the game's 30 of a good again**, in every game; only a Trading Post half holds 100
  (`ExchangeTerms.MaxAmount`). The patch keeps the game's read and passes it through
  `TradingPostCapacityPatcher.CapacityFor`, which gives 100 only while a half carrying `MultiColonyTradingPostSpec`
  has its inventory made (the game puts a blueprint's specs on an entity before it initialises any decorator).
  **A save from an earlier build** whose District Crossing holds more than 30 of a good across its two halves is
  expected not to load: as it loads, the game reserves room on one half for the other half's goods
  (`DistrictCrossingInventory.ReserveStock`), and that throws once they no longer fit (traced in the game's code, not
  tried). Earlier builds' saves are not supported by this release: start a new game, or empty the crossings in the
  earlier build first. Stability Fork saves are not affected (their crossings held 30).
- **A shared game keeps no colony state.** No land (`ColonyReach` begins only with separate colonies: a
  separate-colonies save or new game, or a founding that splits a shared game), no building stamps and no district
  owners (a shared game's placements, starts and district centers get none), nothing counted in the colony digest
  (`ColonyDigest.Gate`), no digest on the heartbeat (`HeartbeatEvent.digest` is null), no daily colony check, and no
  diagnostics report written and copied by itself on a desync (Ctrl+Shift+J still writes one). Its save holds nothing
  of this mod's: every colony saver asks for separate colonies first (the lifecycle, the seat table, marks, stewards,
  wishes, working hours, the trade ledger, each crossing's exchange, building stamps and district owners; a shared
  save from an earlier build that carries stamps or owners loses them at its next save), and the toolbar's locks
  are the game's own.
- **Splitting a shared game** (the host allows it and a player founds): on every computer, at the founding's tick,
  every building standing becomes the first colony's (the stamps one change in the digest, the number of buildings;
  the land each gives counted as it is added, in the order the game made them, the same on every computer), and land
  is kept from then on. Before that, the founding is judged against the shared colony's land, worked out afresh from
  the buildings on every computer at that tick (all colony 0's, so the order does not matter). The founding tool's
  preview keeps the one it worked out until a building comes or goes. The new colony must still stand 20 tiles
  from the shared colony.
- **No Trading Post in a shared game's toolbar in dev mode either.** Dev mode (and the map editor) turns every tool
  on whatever the disablers say; a postfix on `ToolButton.ToolEnabled` keeps it hidden, so the map editor no longer
  offers it either (the map editor is never a separate-colonies game).
- **Home in a shared game** goes to the biggest district center; for a guest it went nowhere.
- **After a desync, picking the old speed again works.** The pause a desync forces now clears the pick too
  (`ReplayService.SetChosenSpeed(0)`); before, the pick stayed and choosing it again was ignored as a no-op (a
  beta5 slip, in every game).
- Still in a shared game, because it is co-op in general: the desync fixes (gates, automation, planting on sliced
  views, dev mode's shortcuts, Tick once, joined district roads), the performance pass, a guest's pending actions,
  the start prompt, the speed boost, going to a player, your own chat color and the diagnostics key. MultiColony and
  the Stability Fork still cannot join each other's games.
- Docs: README (a *One shared colony* section), TWO-COLONIES.md, ALPHA-TEST-SCRIPTS.md (A22, A22f, A22g, B8m, B8n),
  the site, `Doc/ToTestV6.md` and the in-game changelog.
- Checks: StabilityTests 301 (2 new: one colony's land has no contested tiles and refuses a founding beside it, and
  every Mod Settings tooltip within two lines of 112 characters, with both colony settings explained); RuntimeChecks
  239 (6 new: the game's read passed through `CapacityFor`, 30 for a District Crossing and 100 for a Trading Post
  half, the game making a half's inventory in `Initialize(subject, …)`, after its specs, every colony saver asking
  for separate colonies before it asks the saver for anything, and the two newly hooked game methods). Both builds,
  0 warnings. A review of the change before release found the older-save crossing load above (documented, not
  patched: earlier builds' saves are out of scope), the stamps and owners still written in shared saves from earlier
  builds, the preview's land being worked out again for every beaver or plant made, and the split's digest wording;
  all but the first are fixed here.
- Not seen in a game. Script A line 22 and Script B lines 8m and 8n are this release's.

## 1.4.0-beta6

**Your own name in the chat, in a color you pick.** The chat colored your own name the way others see it: your Ping
Color, or the color for your player number while that is still the default (orange for the host, blue for the
first guest, and so on). Under Options, **Player cursors**, a card at the top, **You, in the chat**, now lets you
pick the color you see your own name in: the **Default** swatch (what others see), the same ten presets as a
player's card, or the Red/Green/Blue sliders; **Reset** returns to the default. It is on your screen only: nothing
is sent, others still see your Ping Color or your number's color, and their own choices for you still win on their
screens. Lines already written change color within a moment, as a player's do.
- Kept with the player styles in `BeaverBuddiesCursorStyles.json`, under the reserved key `#you`, so the file keeps
  its shape: an older build reads it as before, and this one reads an older file. A player who calls themselves
  `#you` is kept apart from it (`PlayerCursorPreferences.SelfKey`, `OwnChatColor`, `SetOwnChatColor`; `KeyFor`
  steps aside).
- The card has no size or transparency: there is no cursor of your own to draw. `PlayerCursorSettingsUI.BuildCard`
  now builds both kinds of card; `ConnectionPanelService.ChatColorOf` reads the choice first for your own
  messages, and `LocalPlayerId()` gives the dialog your player number for the default swatch.
- Docs: PLAYER-ACTIVITY.md, CONNECTION-PANEL.md, README, the site, ALPHA-TEST-SCRIPTS.md (B8l), `Doc/ToTestV6.md`
  and the in-game changelog.
- Checks: StabilityTests 299 (3 new: the color kept under a key no name can take and dropped when back to the
  default; a save and reload, and an older file read as before; every string the dialog asks for in the English
  file); RuntimeChecks 233. Both builds, 0 warnings.
- Not seen in a game: the card at the top of the dialog, and the chat line changing after a pick. Script B line 8l
  is this release's.

## 1.4.0-beta5

**A speed boost, from the chat box.** A row at the top of the chat, `Speed boost [-] [0] [+]`, adds a constant to
the speed the players pick at the top right (the game's speed 1, 2 and 3 run at 1, 3 and 7): with a boost of +0.5,
speed 2 runs at 3.5, and the fastest button at 7.5, about 12.5 ticks a second, past the 11.7 the buttons alone
give. The game itself puts no limit there (its developer panel has x30 and x99, and its speed manager takes any
number); the mod's limits are what its lockstep can be expected to keep up with.
- **How it works.** `-` and `+` step by 0.5, to the half-step grid; the box takes a typed number (a sign, a comma or
  a dot are fine) on Enter, or when the cursor leaves it; Esc drops what was typed. Any player may change it, and
  everyone plays the change as an event (`SpeedBoostEvent`: Global, changes nothing in the game), like a speed
  change; the asker's row shows the asked value until the answer arrives, or the old one again if there was no
  session to ask. The row shows what the boost makes of the picked speed, `= 3.5x`, while the game runs. The game's
  own top-right buttons show a speed no button has the way the game shows any custom speed: `x3.5` on the last
  button. A player who joins is told the boost in the start message (`InitializeClientEvent.speedBoost`, 0 from an
  older host); a new session starts at 0, like the chat. The boost is between -6.5 and +23, and the game never runs
  below 0.5x or above 30x (`SpeedBoost.cs`, pure). `Panel/ChatView.cs` (the row: the game's own small - and + and a
  box drawn like the chat's), `Panel/ConnectionPanelService.cs` (the request and the refresh), `PanelLayout.ChatHeight`
  178 (28 for the row).
- **What it changes and what it does not.** Only how fast ticks are worked through, exactly as the speed buttons
  do: nothing simulated, nothing sent with a tick, nothing saved. The tick rate is still bounded by the slowest
  computer: the host eases off for a guest that falls behind as before, and the simulation's share of a frame grows
  with the speed (a true speed 7 took about half of the host's frame in a large colony, alpha22), so a large colony
  will not reach 30x; the connection panel's tick rate says what is really achieved.
- **Speed changes with a boost.** The players' pick and the boost are kept apart (`ReplayService.ChosenSpeed` and
  `Boost`; `TargetSpeed` is their sum, `SpeedSetEvent.speed` stays the pick): picking a speed keeps the boost, and
  the game's return from a pause goes back to the picked speed, not the boosted one (a postfix on
  `SpeedControlPanel.SetSpeed`; without it a pause at 3 + 0.5 unpaused to 3.5 + 0.5, and a guest that paused while
  catching up came back at the catch-up speed). The game's keys for the next and previous speed find the picked
  speed's button (a prefix on `TimeSpeedButtonGroup.GetCurrentButton`); before, at any speed no button has (a
  catch-up speed too) they did nothing. A click on the speed already picked records nothing (it used to record a
  no-op and run the bare speed for a frame).
- **Catching up above speed 7.** The catch-up rule capped a guest at speed 10, which at a boosted speed of 12 would
  have held a guest that fell behind *below* the speed it was meant to run at. The cap is now three above the
  chosen speed when that is higher (`CatchUpSpeed.CapFor`), and the buffer a guest settles at grows with the speed
  above 7 so it stays about a sixth of a second (two ticks at 7, three at 10, nine at 30) instead of two ticks of
  20 ms, which the network's jitter would have crossed on every tick. Speeds 1 to 7 are exactly as before.
- Docs: README, CONNECTION-PANEL.md (the row, the Speed line, the limits), ALPHA-TEST-SCRIPTS.md (B8k), the site
  and the in-game changelog.
- Checks: StabilityTests 296 (9 new: the boost kept to a hundredth and within its limits; the picked speed plus the
  boost, paused staying paused; a running game between 0.5x and 30x whatever is typed; - and + moving by a half
  step to the grid and stopping at the limits; typed values read with a sign, a comma or spaces and refused
  otherwise; shown with a sign the same in every culture and readable back; the row's English strings; a boosted
  speed never held below itself and catching up above it; the buffer above 7 a stretch of time, with a guest at
  speed 14 that hitches settling at the same lag in seconds as at 7); RuntimeChecks 233 (the two event lists name
  `SpeedBoostEvent`). Both builds, 0 warnings.
- Not seen in a game: the row's look (the game's small - and + in the dark panel), that the box takes and gives
  back the keyboard as the chat box does, the game's `x3.5` on the last speed button, and how far a real colony can
  be pushed. Script B line 8k is this release's; where the tick rate settles above speed 7 on real computers is the
  thing to report.

## 1.4.0-beta4

**The Stability Fork's 1.1.11 chat and cursor colors.** MultiColony is now built on Stability Fork 1.1.11 (it was
1.1.10). That release changes only how players are colored on the cursors and in the chat; nothing that is
simulated, sent or saved changes, and its author played it and reported that it works. Brought over as it is:
- **Every player gets a color of their own.** Ping Color starts as the same yellow for everyone, so until someone
  changed it every cursor and every name in the chat was yellow. A player who has not changed it now gets a color
  by player number: the host is orange, and the guests are blue, green, pink, purple, teal, red and lime as they
  join, repeating after eight. It applies to that player's cursor, selection outline, name label and chat name, and
  to the swatch called *Their color* under Options, Player cursors. A color a player chose is never replaced (only
  the exact default yellow counts as not chosen), and a color you set for someone under Player cursors still wins.
  Pings keep the Ping Color as set. Whoever is looking works the color out from the player number, so nothing new
  goes over the network, and a player who leaves and joins again gets a new number and so a new color.
  `Activity/PlayerColors.cs` (the Stability Fork's, unchanged), used from `PlayerActivityService.Apply` and
  `ConnectionPanelService.ChatColorOf`.
- **Only the name is colored in the chat.** A whole line, name and message, took the player's color; now only the
  name does, and the message is in the panel's normal text color (`Panel/ChatFormat.Line`).
- **MultiColony's colony colors are a different set** and stay so: the land outlines, the *(colony N)* beside a
  name and the trading window use the game's own start colors, one per colony. A player's cursor color and their
  colony's color need not match (PLAYER-ACTIVITY.md and CONNECTION-PANEL.md say so).
- The Ping Color tooltip says what the color is for, in two short lines (the Stability Fork's is one long one).
- Docs: README, CONNECTION-PANEL.md, PLAYER-ACTIVITY.md, the site, `Doc/ToTestV6.md` and the in-game changelog name
  1.1.11 and describe the colors.
- Checks: StabilityTests 287 (5 new, the Stability Fork's: every player number up to eight gets a different color
  and none is yellow, light enough to read, a chosen color is kept and only the default yellow replaced, two players
  on the default differ and every machine picks the same, numbers past the palette start over; the chat-line check
  expects only the name colored); RuntimeChecks 233.
- Not seen in a MultiColony game: the colors were played in the Stability Fork, not here. Script B line 8j is this
  release's.

## 1.4.0-beta3

**Mod settings tooltips that fit on the screen.** No code change. The tooltips in Mod Settings ran to 250 to 430
characters on one line, and Mod Settings does not wrap them, so the longer ones (separate colonies, hand-over days,
the guest frame-rate floor, detailed logging) were drawn off both edges of the screen. Every one of the seventeen
now says what the setting does in one sentence of at most 112 characters, with a second short line only where a
warning or a pointer belongs (pause before saving with *Never auto-pause*; every player must turn detailed logging
on; the frame-rate floor is also in the connection panel; Ctrl+K founds a colony; Ctrl+T hands one over). The same
facts in fewer words; the labels are unchanged. `Localizations/enUS_BeaverBuddie.csv` (English only, like the rest).
- Checks unchanged: StabilityTests 282; RuntimeChecks 233.
- Not seen in a game: the tooltip box's width is the game's, so please hover a few (any Script) and say if one still
  runs off the screen.

## 1.4.0-beta2

**Nine things that make a colony each easier to live with.** Everything sits on machinery that was already there;
nothing simulated changes unless a player uses it. All new text is English only.

- **Food and water at a glance.** Each colony's row in the trading window (Ctrl+T) shows the top bar's food and
  water icons with the stock and the days it lasts at the rate the colony used yesterday (the game's own daily
  samples; today's use, scaled, before a full day has been sampled; red under a day). `Colonies/ColonySupplies.cs`,
  `SupplyDays.cs` (pure). Display only.
- **Looking after a colony.** A colony's player asks another player in the session to look after it (**Let … look
  after it** on their own row; **Take it back** later); the steward presses **Run this colony** and their actions,
  toolbar, top bar, science and refusals count as that colony's until **Back to your colony**, the way the debug
  seat flip worked for the host alone. The host may do this for an absent player's colony and may end any
  stewardship. A colony looked after by a steward who is in the game is not handed over for absence. The grant is
  saved (by the steward's stable id); which colony a player acts as is session state played as an action, refused
  before the first tick. Every hello now carries every player's stable id and name (`PlayerHelloEvent.players`), so
  every computer knows who a steward is. `Colonies/ColonyStewards.cs`, `ColonyStewardRules.cs` (pure);
  `ColonySession.SeatOfPlayer` / `LocalSeat` beside `SlotOfPlayer` / `LocalSlot`; three events (`StewardGrantedEvent`,
  `StewardRevokedEvent`, `ActAsColonyEvent`) judged on the host by `ColonyStewardRules`.
- **Go to a player, and Home.** A click on a player's row in the connection panel takes the camera to their cursor
  (or what they have selected); the own row, or the **Home** key (rebindable), goes back to the colony's biggest
  district center. `Colonies/ColonyNavigation.cs`, `PlayerActivityService.TryLocate`. Display only.
- **The day before a hand-over.** The host's presence event now carries its hand-over limit and the day's players by
  stable id; the window shows *missed 6 of 7 days*, and the day the count reaches the limit every player in the game
  is warned (`ColonyAbsence.cs`, pure). No hand-over rule changed, apart from stewards.
- **Offer again, and counter-offers.** Each half remembers the last exchange offered or accepted there (saved;
  `CrossingExchange.LastTerms`, encoded by `ExchangeTerms.EncodeTerms`): a line above the form offers it again in one
  click, and a click on a ledger row puts that round's terms into the form. **Decline** and **Withdraw offer** leave
  the offer's terms in the form of whoever pressed them. Terms only ever go into the form; nothing is offered without
  **Make offer**.
- **A wishlist per colony.** Up to three items a colony is looking for, set from its own row in the trading window
  with the game's goods grid (opened unticked, beside the window). Shown beside the colony in every player's window,
  under the header of a Trading Post with that colony (*Sarah is looking for: [icons]*), and in the goods grid when a
  partner chooses what to give (the count in yellow, a word in the tooltip). Saved; `Colonies/ColonyWishlist.cs`,
  `WishlistTerms.cs` (pure); `WishlistChangedEvent` (the actor's own colony, like working hours).
- **A reserve on an exchange.** **Keep at least** (shown for more than one round) sets what a side keeps back: its
  goods are brought, or its science or beavers paid, only while the colony would still have that much after the
  round (the goods on its half count as had). Each side has its own, the offering side's with the offer
  (`ExchangeProposedEvent.keep`), either side's changeable on the running exchange (`ExchangeFloorSetEvent`). Saved on
  the half (`CrossingExchange.Keep`), in the digest and the daily check; `ExchangeTerms.CanSpare` /
  `StillToBringKeeping` (pure). The status line says when a reserve holds a side back.
- **The host is asked before its first change closes joining.** While the host waits paused at the start with
  players able to join, its first change (a path, a mark, a founding) is held and a dialog asks: **Start the game**
  (the held actions are then recorded in order) or **Keep waiting** (they are dropped, with a notice). A guest's
  change while the host waits is refused with the founding's *Not before the game starts* notice. The connection
  panel shows the host **Joining: open** until then. `Colonies/HostStartGate.cs`, `HostStartRules.cs` (pure);
  `ServerEventIO.IsAcceptingClients`; the hook in `ReplayEvent.DoPrefix`.
- The daily colony check and the diagnostics report now cover stewards and wishes; the report names the seat beside
  the acting slot.
- Checks: StabilityTests 282 (+14: supply days, the reserve and the terms string, the wishlist, the steward rules,
  the start gate, the hand-over timing, the panel's joining line); RuntimeChecks 233 (+1: the new fields through the
  event JSON; the two review lists updated). Both builds, with 0 warnings. Nothing of this version has been played
  yet; ALPHA-TEST-SCRIPTS.md lines A22a to A22e and B8f to B8i cover it.

## 1.4.0-beta1

**A performance review of alpha22 with the guest (player 2) in mind, and what it changed.** The lockstep design was
read end to end for what a guest pays that the host does not, and the mod's own patches for what they cost every
computer per frame, per tick and per action (two sweeps: the colony code in the simulation, and the per-frame
interface and activity code). Nothing here changes what is simulated: every change is bookkeeping, caching or the
order of checks, and every computer still plays the same actions on the same ticks with the same results. The mod
list and the two settings now say **(beta)**.

What a guest pays for lockstep, and what changed:
- **Every event a guest received was written out as text again on the game thread, only to hash it.** The hash (the
  network's running check that both sides saw the same events) is now taken from the message's bytes on the receive
  thread, as it arrives, and the game thread only combines it. The host, for its part, wrote each event out as text
  three times (once to parse it, once to hash it, once per guest to compress it): it now encodes, hashes and
  compresses each event once and sends every guest the same bytes. A guest's own actions leave as the text they were
  serialized to, without the parse-and-write-out round trip they made before.
- **Steam is served before the game's own frame update.** The game's ticker and the Steam pump both ran in Unity's
  Update phase, in no fixed order. Pumped after the ticker, a heartbeat that arrived while the last frame was drawn
  reached the guest's tick loop one frame later than it could have. A guest at the start of a tick waits for that
  heartbeat, the ticks it could not run are lost to drift, and the drift is made up by catch-up speed (two changes of
  the game's time scale): at a high speed this could be a frame of standing still per tick. The pump now runs first
  (`DefaultExecutionOrder`).
- **Cursor frames go out only when something changed.** Every player sent its cursor, selection and editing state ten
  times a second, each with a raycast into the scene and a few strings, whether or not the mouse had moved, and kept
  sending a "hidden" frame while its window was in the background. A frame now goes out when it differs from the
  last, and once a second regardless (the others forget a player three seconds after their last frame); the raycast
  is skipped while the mouse and the camera stand still. Receiving a message no longer allocates the decompressor's
  default 80 KB buffer per message, and an empty mailbox hands out nothing.

What every computer paid in the mod's own patches:
- **The profiler took a global lock and hashed a name on every timed call**, on the busiest paths there are: every
  beaver's and every workplace's working-hours check, every tick, plus every job search and every preview block every
  frame. Spots are now declared once and timed with two timestamps and three additions. A headless check measures
  the two ways side by side (2,000,000 calls: 102 ms before, 70 ms now, single-threaded on the build machine; the old
  way also serialized every caller on one lock).
- **The working-hours checks asked three service lookups and the profiler before finding they had nothing to do**
  (a shared-colony game, or a bot). They read one static flag first now; with separate colonies on, the owner lookup
  and the timing only.
- **`IsSeparateColonies` and separate science's `IsEnabled` are static reads**, not lookups of a service, on every
  path that asks (the job searches, the science context around every producing workshop's tick, the status and
  batch-control filters, the panels). They follow the running game and are cleared when it is left.
- **Placement previews allocate nothing per tile.** While a building or path is being placed, the colony rules check
  every preview block every frame; the check copied the footprint twice and made a five-element array per tile. It
  reads the footprint as given and walks a fixed neighbour table now, and in a shared-colony game nothing is looked
  up or timed at all.
- **Resource searches** (lumberjacks, gatherers, scavengers, harvesters) are filtered and timed only with separate
  colonies on; a Trading Post worker's export decision is timed only at a Trading Post.
- **Land bookkeeping:** "is this building still waiting for a colony?" was a walk down the list of waiting buildings
  for every entity that came into being (a save's load is every building): a set lookup now. Every land question
  asked the game for the map's size twice; the grid is kept once made.
- **The top bar's per-good count, the population panel and wellbeing** each rebuilt the list of this player's
  districts (a query and a list per call, per good shown): one list, filled at most once per frame.
- **The connection panel** parsed its display-mode and corner settings (an `Enum.IsDefined` and a box) twice per
  frame: parsed only when the text changes. It also put every panel in its corner into words twice a second for a
  log line that is written once per change.
- Smaller: the chat box is asked once per frame whether it has the cursor; the cursor overlay looks the camera up
  once, keeps each player's labels and measures text without allocating; the activity parser keeps its key arrays.

Reviewed, left as they are (and why):
- **The guest's wait at each tick boundary, and the catch-up speed.** A guest cannot start a tick before the host's
  word for it arrives, and the ticks it loses waiting are made up at a higher speed: that is the lockstep design, and
  CatchUpSpeed (alpha5) trades a little smoothness for a shorter round trip on purpose. Changing it is a design
  decision for after the beta has been played; the diagnostics report (Ctrl+Shift+J) counts and times those waits.
- **Reading each received event into an object** stays on the game thread (the converters touch game types); it is
  well under a millisecond for anything but a huge area mark.
- **The per-action log line** stays: it is the trail every bug report relies on.
- **Gzip on every message** stays, as the wire format; a small frame costs tens of microseconds each way.
- **Ending a frame's ticking at every new entity** (so the entity starts before the next bucket, the same everywhere)
  is a determinism requirement.
- The chat colour refresh, the trade overview's per-frame availability check, the tool disabler's spec lookup per
  button and the trading window's 2 Hz refresh are each too small to be worth a change.
- Checks: StabilityTests 268 (an event sent as text hashes as one sent parsed; a guest's hash is taken from the bytes
  it received and matches the host's once every event is read; a profiler spot needs no lookup or lock and the
  report sees every call, timed against the old way; an empty activity mailbox costs nothing; a frame that changed
  nothing is the same frame); RuntimeChecks 232 (the two static flags are reset with the game; Steam is pumped before
  the game's own scripts; the game sends events as text, never parsed and written out again).
- Not played in a game: nothing since alpha11 has been. Script B line 8e is this release's.

## 1.4.0-alpha22

**The Trading Post's ledger names both sides.** Each row read *gave [icon] 100 · got [icon] 25*; it now reads
*You gave [icon] 100 · Sarah gave [icon] 25*, with the partner's name in their colour, as the rest of the panel
names them (the player who runs the colony, or *Colony N* while nobody does). The two parts share the row's width
and a long name is cut short with an ellipsis instead of pushing the amounts out. The cycle-day stamp is unchanged.
- Strings: `Colony.Trade.LedgerYouGave`, `Colony.Trade.LedgerTheyGave` (English only, like the rest).
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.
- The site's interactive panel (the docs site) shows the same rows.

## 1.4.0-alpha21

**The in-game changelog and the Doc folder describe MultiColony.** No code change.

- `BeaverBuddies/changelog.txt` (the ChangeLog resource the in-game changelog dialog reads; the dialog stays off in
  this fork, `Help/ChangeLogService.cs`) now begins with MultiColony's versions, alpha21 back to alpha1, then the
  Stability Fork, above the original project's entries.
- `BeaverBuddies/Doc/` gains a README saying what each file is and which are the original project's historical
  lists; `Changelog.txt`, `WorkshopDescription.txt` (Steam BBCode, from WORKSHOP.md) and `ToTestV6.md` (what is owed
  to in-game testing and what to watch for) are rewritten for this fork; `Consent.txt`, `TranslationPrompt.txt`
  and `Movement.md` get a note on where the shipped text lives, the state of translations, and the fix that
  answered the analysis.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha20

**Every guide and page is current.** No code change.

- **PLAYER-ACTIVITY.md** covers MultiColony: activity across colonies, an *Editing* notice for a change the host
  then refuses, cursor colors versus colony colors, selections and pings across colonies, the check counts, and a
  playtest step.
- **WORKSHOP.md**'s description says what the Trading Post is now (its own building, 100 an item a round, rounds,
  both sides in before anything crosses, ending takes both), when a colony is founded, that desyncs are caught at
  once, and that the mod's files are part of the join check.
- **The docs site** (`docs/`) was still the Stability Fork's: it now describes MultiColony (a colony each, trading
  posts, founding, the every-tick check), links to this repository's releases (every release is a pre-release, so
  the pages link to the release list and the page script picks the newest), names the mod's folder and mod-list
  entry, and says in the FAQ, install guide and troubleshooting when joining closes, what the desync messages
  mean, and what the known limits are.
- README.md and TWO-COLONIES.md name alpha20 as the last unplayed build.
- Left as they are, on purpose: `BeaverBuddies/changelog.txt` and `BeaverBuddies/Doc/` are the original project's
  (the in-game changelog dialog is off in this fork); `design/` holds the plans as written.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha19

**STEAM-INVITES.md covers MultiColony.** No code change.

- The host waits paused and changes nothing until everyone is in; when joining closes over Steam (the first tick or
  the first change) and what an old invite says; the lobby's closed state.
- The build check includes the mod's own files; a colony follows the Steam account, and what to do when the
  account changes.
- The two-account playtest: a friend who leaves shows as away, an invite after a change while paused, and rejoining
  a save with the friend hosting.
- Known limits: rehosting after a desync, and the every-tick colony check.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha18

**CONNECTION-PANEL.md covers MultiColony.** No code change.

- The player rows with each player's colony, *(colony N)*: where the number comes from, a helper, a guest not yet
  seated, and shared-colony games.
- *Out of sync* now also means a colony-state difference (the every-tick digest or the daily check).
- A guest who leaves: off the list, and away from the next day for hand-over.
- Who can join and when joining closes; what the join check compares (including the mod's own files).
- The check count brought up to date; a known limit on seat versus presence.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha17

**TWO-COLONIES.md covers alpha11 to alpha16.** No code change.

- State of testing names the unplayed builds and what the every-tick check means.
- The rules: no building beside another colony's paths or buildings, finished or not; a founding takes the host's
  difficulty when the save recorded none; hand-over by absence with detailed logging on, a guest who leaves, and
  the host's hand-over from the first tick; a relic's science when nothing demolished it, and unnamed science; unlock
  sets follow renamed buildings; copying settings from another colony's building; traded beavers must walk and
  carry nothing.
- Road networks: placing beside an unfinished path is refused; joined roads keep the game running.
- Known limits: district centers get the block check; the crossing panel's snapshot.
- How it works: what the host writes into actions, the join rules and check, the running digest. Testing: what the
  check suites cover now.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha16

**The README covers alpha11 to alpha15.** No code change.

- The alpha warning names the unplayed builds and what the every-tick check means for a healthy game.
- Starting a game: the host waits paused and changes nothing until everyone is in (joining closes at the first
  change as well as the first tick); founding is offered once the host unpauses.
- Playing: no building beside another colony's paths and buildings, finished or not; log piles and stacks are
  collected where the colony may work.
- Trading posts: only the two partners may remove one; both halves are placed as one.
- Hand-over: the setting can be changed during the game; a guest who leaves is away from the next day; the host's
  hand-over waits for the first tick.
- Controls and *Good to know*: Ctrl+Shift+K needs nobody connected; joined roads keep the game running; dev mode's
  Ctrl keys are off in co-op; gates and automation react at the tick.
- Co-op basics and troubleshooting: the mod's files are part of the join check; colony state is compared every tick
  and daily; what the *can no longer be joined*, *has not started yet* and `Colony state differs` messages mean.
- How it works: guests take the host's answers (placements, founding, presence) and the running digest.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha15

**The test scripts cover alpha12 to alpha14.** No code change.

- Script A (host alone) gains lines for joined roads and the *roads joined* notice, a Trading Post half on the
  wrong land and partner-only removal, gates (by hand, by switch, while hovering, and one that would join two
  districts), automation and copying settings from another colony's building, the District Crossing panel left
  open, and the digest count after a load.
- Script B (two players) gains the every-tick digest (what a `Colony state differs … at tick` line means and what
  to send), a guest's refused working-hours change and dev-mode unlock, joined roads and gates with two players, and
  the hand-over by absence with detailed logging on and without a rehost.
- Script C: the hand-over step no longer says to turn the host's logging off (alpha12 fixed that), and a Trading
  Post half refused for a third colony.
- Checks unchanged: StabilityTests 263; RuntimeChecks 229.

## 1.4.0-alpha14

**The alpha10 review's Appendix B: the baseline's own per-frame state.** The last section of the review. Each is
somewhere the game itself changes or caches simulation state once per frame, or from what the local player is
hovering, and a tick is spread over a different number of frames on each computer.

- **Gates open and close at the tick, judged on the real roads.** The game opens and closes gates once per frame,
  and decides whether a gate may open (it must not join two districts) from the preview road graph and district
  map: the real ones plus whatever the local player is hovering with a tool. In co-op the gates are now updated at
  the start of each tick, and the question is answered with the game's own walk over the real road graph and
  district map (`Fixes/FrameToTickFixes`), so no computer's hovering and no frame boundary can open a gate on one
  computer only.
- **Automation is evaluated in the tick only.** The game also evaluates automation whose inputs changed once per
  frame, between two buckets of a tick, so a switch's effect landed mid-tick at a point that differed per computer.
  In co-op only the tick's own evaluations run (start and end of each tick): a change shows at the end of the tick
  it was made in, the same everywhere.
- **The crossing panel no longer freezes the workers' snapshot.** A District Crossing's import icons ask the same
  provider the workers' export decision reads, and whoever asks first fills a cache kept until a setting or the
  storage changes: a panel open on one computer froze a snapshot taken at a frame boundary that the other
  computer's simulation took at its tick. In co-op an ask from outside the simulation gets its answer but leaves the
  cache as the simulation left it.
- **Two district centers on the same tiles in one tick** no longer stop the session: a district center's placement
  gets the block check (from the spec, no preview copy), so the second is skipped everywhere.
- Closed already by alpha11: the Terrain Block replay check, which read the local tool's preview blocks, now runs on
  the host alone and guests take its answer.
- Reviewed, no change: `Ruins.Shuffle` re-selects, on the computer where it was selected, the ruin that replaces
  one; selection is this computer's own and reaches nothing the simulation reads.
- Checks: StabilityTests 263; RuntimeChecks 229 (the game members the gate, automation and snapshot fixes rely on).

## 1.4.0-alpha13

**A colony desync is caught the tick it happens, not the day some beaver's random draw changed.** The alpha10 review's
section 4 (what is compared between computers, and what is not) implemented in full.

- **A running colony digest, checked every tick.** Every change to colony state (a building or district getting its
  owner, a planting or cutting mark set or cleared, science earned, spent or unlocked, an exchange proposed, held,
  crossed, cancelled or ended, a ledger line, a round's totals, land reached or given up, beavers traded, a day's
  presence, a hand-over, working hours) folds into one 64-bit number on every computer. The host writes it into
  every heartbeat; a guest compares at the same point of the same tick and stops with the desync dialog if it
  differs, with the number of changes each side counted in the log. Colony code draws no random numbers, so until now
  a colony that differed showed only once the difference reached a beaver's random draw, possibly days later, or never
  (a science pool, an unlock set, a serial or the ledger might never). Only changes made inside the simulation (a tick
  or a replayed action) count, so loading and display cannot skew it.
- **The daily colony check covers everything.** It now also has: each colony's unlocked buildings and bot worker
  types by name (not only their count); the tiles two colonies reach and who holds each; every exchange, closed ones
  too, with its good, serial, rounds, repeat, who proposed it and its ledger; the goods waiting on each half; the
  totals traded; the mode flags, the starting settings and the slot table; the two unsaved phases (the stamping and
  crossing counters); and the digest itself. It is taken in every co-op game, not only with separate colonies. The
  host still sends it with the day's presence and a guest that differs stops.
- Checks: StabilityTests 263 (the digest: the same changes give the same number, another order or another number
  differ, nothing counts outside the simulation, names hash the same every run); RuntimeChecks 226 (the heartbeat's
  digest survives the event JSON; an older host's heartbeat has none).

## 1.4.0-alpha12

**The alpha10 review's Appendix A: the bugs that were not desyncs.** Every item of that list that alpha11 had not
already dealt with.

- **Two colonies' roads joined no longer freeze the game.** The game's district map throws when two district centers
  share a road network (its tools never let that happen in single player), and in co-op the throw came out of the
  tick, so the beavers stood still and the *roads joined* warning, raised after the tick, never showed. Now the first
  district keeps the shared roads, the other goes without them, the tick runs on, and the warning shows so the
  players can break the link.
- **Placing beside another colony's unfinished path is refused.** The check only knew finished roads on the
  tick-updated map: a path the other colony was still building, or one it finished just before a pause, was
  invisible, and a path laid beside it joined the two colonies' roads once both were done. Another colony's building
  or path on or beside the footprint, finished or not, now counts (a Trading Post half excepted: the other colony's
  half stands right behind one's own).
- **A Trading Post's two halves are accepted or refused together.** Each half was a placement of its own, judged on
  its own, so one could be accepted and the other refused, leaving a half that could never finish.
- **Only its two partners may remove a Trading Post**, as the rules said; any colony could.
- **Hand-over by absence works with detailed logging on.** It was skipped whenever the host had detailed logging on,
  not only while the host tested alone, and the missed days kept counting meanwhile, so turning logging off handed
  colonies over at once. Now, with logging on and nobody connected (testing alone, when the host plays every colony
  with Ctrl+Shift+K), every colony counts as present; with a guest connected, logging changes nothing.
- **A guest who leaves is away from that day on** (it counted as playing until the host rehosted), and its colony
  may be handed over by hand from Ctrl+T at once.
- **Ctrl+Shift+K** does nothing while a guest is connected, and the shift ends with detailed logging.
- **A guest's free unlock (dev mode) opens the tool when the host answers**, not before: with the host's dev mode
  off the tool opened on the guest and every placement made with it was refused.
- **Log piles and other stacks** are collected only where the collecting colony may work; any colony's lumberjacks
  and gatherers took them.
- **Traded beavers** are only ones who can walk to the new district (the game reassigned one who could not to the
  nearest district of any colony, possibly its old one) and who carry nothing (what they carried crossed uncounted).
- **Copying settings from another colony's building is refused** (copy settings, or placing a copy of it): it took
  the automation links too, wiring the new building to the other colony's sensor. A copy placed from another
  colony's building is placed plain.
- **A refused working-hours change** on a guest no longer stays on its panel; the panel goes back to the colony's
  hours when the host refuses it (or never answers).
- **Per-colony unlock sets** load through the game's template name mapper, so a building renamed by a game update
  keeps its unlock.
- **A save's unstamped buildings** give their colony its land at the first tick, not the 16th, so a colony founded
  right after unpausing keeps its distance from them.
- **An action whose sender could not be seated** is played as nobody's (slot -1), not with the slot the sender wrote.
- Docs: recovered goods are judged by land (not owner); the days-before-hand-over setting is read each day; the
  working-hours bell rings at the game's own hours and a hand-over keeps a colony's hours; demolition marks on
  nobody's land are anyone's. Comments about a "rescue" migration to another colony, which the rules refuse,
  corrected.
- Checks: StabilityTests 262 (a Trading Post is removed by either partner and nobody else); RuntimeChecks 226.

## 1.4.0-alpha11

**The Trading Post, reworked: each side's goods wait on its own half until both are in, then everything crosses at
once; ending an exchange takes both colonies; a ledger per post; a native, closable trading window.** Also in this
release: the alpha10 desync review's five fixes, and a faster round trip for a guest's actions (below).

The Trading Post:
- **Nothing crosses before what it was exchanged for is in.** Each round, each colony's Trading Post workers bring its
  goods to its own half, where they wait, held (reserved, so no other beaver takes them; held again when a save is
  loaded). When both sides are in, the round crosses in one go: the goods to the other half, for that colony's
  workers to haul into storage; science from pool to pool; adult beavers to the other colony's district. Until now
  goods passed across as they arrived, in step, and science and beavers moved by themselves every few moments, so
  one colony could receive beavers or science long before its partner's goods arrived.
- **Science is exchanged like a good** (it was already in the goods grid): the **Gift science 50 / 250** buttons and
  their action (`GiftScienceEvent`) are gone. A science or beaver side is in when its colony can pay it now (the
  science in its pool; adults able to move, one adult always staying), and it moves when the round crosses.
- **Up to 100 of each item a round**, what a half holds, and **Rounds** (1 to 99) repeats the exchange for more;
  **Repeat until cancelled** stays. The offer carries the rounds, and accepting checks them with the other terms.
  − and + step by 10 (Shift: 1; beavers by 1, Shift: 10).
- **Accurate progress on both sides:** each side's bar counts what waits on its own half, the other colony's side
  included (it used to jump from nothing to done); science and beavers show what the colony can spare. A line says
  what the round waits for: your workers, the other colony's goods, science, adults, or workers on your half.
- **Ending an exchange early takes both colonies.** **Cancel exchange** asks the other colony, who chooses **Agree to
  cancel** or **Keep trading**; the asking colony may take it back. Nothing is brought and nothing crosses while it
  is asked. Once ended, what waits on each half goes back into its own colony's storage; rounds that crossed stay
  crossed (before, a cancel was one-sided, and beavers already moved stayed moved). Offers are still declined or
  withdrawn by one player. An exchange at a post that stops joining the two colonies pauses, and its colony may end
  it alone; a handover, or a post that ends up joining other colonies than the two that agreed, ends it by itself.
- **Not a store.** A half's room (100 of a good) is only used by the round under way. The game's stock list
  (*No goods in stock*), which alpha10 hid once but which showed itself again on every update, is no longer shown
  for a Trading Post.
- **Only your half trades.** The trading menu is on the half your roads reach; the other colony's half says whose it
  is, with **Select your half**.
- **A ledger per post:** each half records the last 20 rounds that crossed there (the cycle and day, as the
  notification journal writes them, and what its colony gave and got); the panel lists the last 8. The totals traded
  between the two colonies stay below it.
- **A beaver who joins through a Trading Post** gets a line in the receiving colony's notification journal, like a
  birth (*Pip joined the colony from Colony 2 through a Trading Post.*). Traded beavers are chosen as the game
  chooses who migrates.
- **The trading posts and colonies window** (Ctrl+T, or **All posts** on a post) is the game's own box: its framed
  panel, title badge and red close button (CoreStyle's `sliced-border`, `capsule-header`, `close-button`), with each
  post and colony as a row on the game's green board and its buttons as the game's wooden ones. Its close button, Esc,
  Ctrl+T and the Trade button close it (its old close button was an unstyled text button that did not show), and it
  does not pause the game. The **Trade** button at the top right is the game's square toggle, like the water and
  stockpile buttons beside it, with a trade icon drawn in their style (`UI/Images/BeaverBuddies`).
- Amounts of one read in the singular (*1 Beaver*).
- An exchange open in an earlier alpha's save starts its round again; one of more than 100 a round ends.
- Checks: StabilityTests 261 (a round's goods: what workers bring and what is held never pass the round's amount,
  and uneven loads always fill both halves; rounds 1 to 99; the last adult stays; the offer form with rounds, and
  every text with an English line); RuntimeChecks 226 (the game members a round's holding and crossing, the beaver
  moves and the population log use).

**Also in this release: the alpha10 desync review's five findings, and a faster round trip for a guest's actions.**
A review read every colony path of alpha10 for state that could differ between computers.

- **Joining after someone acted at tick 0 (F1).** A player who joins is sent the save the host started from and only
  the actions played after they connect, so anything played while the host still waited, paused, was missing from
  their game; and the founding prompt asked every guest to do exactly that the moment they were seated. Now founding
  and hand-over wait for the host's first tick (the prompt appears then; Ctrl+K and the Ctrl+T buttons say why until
  then), and the first action that changes the game while paused closes joining, with a message saying to rehost.
- **Dev mode's Ctrl keys (F2).** "Place finished" and "don't recover goods" were read where a building is placed or
  removed, which in co-op is every computer: a player only holding Ctrl got a finished building, or no recovered
  goods, on their computer alone. Both keys are off in a co-op game.
- **Science named by no one (F3).** A relic that stood fully demolished and was then destroyed by a blast or a
  collapse paid its science to each computer's own colony. Now a relic's reward names the colony whose mark or land
  it stands on, and any science read or changed inside a tick or a replay without a colony goes to the first colony
  on every computer, with a warning in the log.
- **Founding in a save without recorded settings (F4)** read each computer's own difficulty specs; a mod changing the
  default difficulty on one computer gave a different number of beavers. The host now writes the starting settings
  into the founding action and every computer uses those.
- **The blueprint files are part of the join check (F5).** A guest with the right DLL but a missing, stale or edited
  `Buildings/` or `TemplateCollections/` folder is refused, not warned.
- **The daily colony check is compared.** The host sends its colony fingerprint (owners, land, marks, science,
  exchanges, hours, absence) with the day's presence; a guest whose own differs stops with the desync dialog at once,
  with both lines in the log, instead of only when the difference changed a beaver's random draw.

Faster for a guest (and the host):
- **A guest no longer checks a placement again.** Every replayed building was checked by making and destroying a
  whole copy of it (the game's check needs an instance), on every computer: a guest paid that for each building coming
  back from the host, on top of placing it, and a dragged path of thirty tiles was thirty copies in one frame. The
  host checks once, as the placement is played, and writes the answer into the action; guests take it. (If the two
  games ever disagreed, the game's own placing throws and the session stops with a message, instead of one computer
  placing and the other silently skipping, as before.) The host's check also tries the cheap block check first.
- **Area marks are judged as tiles, not names.** Each tile of a dragged area was written as a string and parsed back,
  on the marker's computer and twice on the host.
- **A planting action is half the size:** the dragged blocks were sent along with the levelled tiles the replay marks,
  and the replay only ever used the tiles.
- **Compact JSON** for every action, and received actions are read straight from the parsed message instead of being
  written out as text and parsed again.
- The line logged for every action (with Unity's stack trace) is only written with detailed logging on.
- Checks for these (counted in the totals above): founding and hand-over wait for the first tick; the blueprint check
  changes with a byte and matches between two installs; the host's answers survive the event JSON; the events that
  leave joining open are listed; a scan of simulation-reachable game methods for dev key reads; `Found` reads no local
  specs; the planting scope is judged by tiles.

Not done, and why:
- **Playing a guest's action in the middle of the host's tick** would need both computers to apply it at the same
  bucket of the tick and the host to send a word per bucket instead of per tick; the tick boundary stays.
- Reusing one copy per building for the host's placement check (alpha5's reason stands); with guests no longer
  checking, the copy is made once per placement instead of once per computer.

## 1.4.0-alpha10

**The Trading Post is its own building, and the District Crossing is the game's own again.** Until now a District
Crossing became a trading post wherever it joined two colonies, and every District Crossing was cheap (10 logs, no
science) in a separate-colonies game.

- **New: the Trading Post** (District Management, after the District Crossing): the District Crossing's model and
  workings (each faction's own; two linked halves, each run by its own district's workers) under its own name, icon
  and description. It **costs 10 logs and needs no science**. All of alpha9's trading moves to it: the trading panel,
  the goods grid, exchanges, repeat deals, gifts and the science gift; its panel has no *Imported goods*, **Manage
  distribution** or stock list. It trades once its two halves are in two different colonies' districts; until then
  its panel says what it is waiting for, and nothing crosses it (import settings never move anything across it).
  It only appears in the toolbar of a separate-colonies game (or in dev mode, which shows every tool).
- **The District Crossing is the game's own:** its usual cost and science, import and export settings, and its own
  panels, for linking a colony's own districts. For the colony rules it is an ordinary building: it may not stand on
  another colony's land or beside its roads, and it counts towards its colony's land. One that ends up joining two
  colonies anyway (another colony's road reaching its far half) moves nothing between them, and its panel says so.
- The Trading Post, not the crossing, is what the colony rules treat as the meeting point: it needs only one block on
  the placer's own or free land, it counts for no colony's land, and either colony may remove one between them.
- **Ctrl+T** also lists your Trading Posts that are not trading yet.
- Built from the game's own blueprints: `Buildings/DistrictManagement/MultiColonyTradingPost` holds each faction's
  blueprint (the game's District Crossing blueprint with the name, price, tool order, texts and icon changed, and the
  mod's `MultiColonyTradingPostSpec` added), and `TemplateCollections` appends it to each faction's toolbar. The
  toolbar button is hidden outside separate-colonies games by an `IToolDisabler`.
- Old saves are not carried over: a District Crossing between two colonies in an earlier alpha's save no longer
  trades.
- Removed with the old behaviour: the crossing's cost and science patches, and alpha9's crossing title and
  description patches (the Trading Post has its own).
- Checks: StabilityTests 259 (the building's texts have English lines); RuntimeChecks 210 (each faction's Trading
  Post blueprint is the game's District Crossing except for its name, price, texts, icon and mark; each faction's
  toolbar lists it once; its icon is a sprite; no game assembly uses its spec's name; the game still has the tool
  disabler hook).

## 1.4.0-alpha9

**A trading post is now only a trading post, with a panel that looks like one of the game's own.** A District
Crossing between two colonies still showed the game's district-distribution controls (the *Imported goods* box with
**Manage distribution**, and the crossing's stock list) and described itself as balancing goods between districts,
though import and export settings move nothing across a trading post. Its trading section was plain text with no
background, hard to read over the map, and ran off the bottom of the screen.

- **Only a trading post:** at a crossing between two colonies the panel is titled **Trading Post** and describes one;
  the *Imported goods* box, **Manage distribution** and the stock list are not shown. A crossing between one colony's
  own districts is unchanged. Display only: what crosses a trading post, and how, is unchanged.
- **A new trading section**, built from the game's own panel pieces (the Workplace section's board, the description's
  blue cards, the game's wooden and red buttons, −/+ buttons, input boxes, progress bars and check boxes): *Trading
  with* the other colony in its colour, and **All posts** (Ctrl+T).
- **Making an offer:** a **You give** and a **You get** card, each with the good's icon and name, how much the colony
  has, −/+ (10 at a time, Shift 100, beavers 1) and the amount. A line under the cards reads the offer back, or says in
  red what is wrong with it; **Make offer** waits for a valid offer. **Repeat until cancelled** is a check box. The
  form starts on the good each colony has most of.
- **Choosing a good** opens the warehouse's goods grid beside the panel: science and beavers, then the game's good
  groups with their icons, each good with the colony's stock (*Only what is in stock* at first). Choosing the other
  card's good swaps the two. A click elsewhere, or Esc, closes it.
- **An offer** is a card saying who offers, *You get* and *You give* with icons (and how much you have of what you
  would give), with **Accept** and **Decline**, or **Withdraw offer**.
- **An exchange under way** shows each side as the game's progress bar with its count, the round of a repeating
  exchange, and when your side is waiting; **Cancel exchange** as before.
- *At this post* lists what waits on the half (only when something does). *Traded with* shows what has passed each
  way (all posts) as icons with amounts. **Gift science 50 / 250** greys out a gift you cannot afford.
- **Never cut off:** when the game's sections above it leave too little room on the screen, the trading section
  scrolls, with the game's scroll bar. The goods grid stays on the screen too.
- Science and beavers are named *Science* and *Beavers*, like goods; the notice of a new offer says to select the
  trading post.
- Checks: StabilityTests 259 (reading amount boxes, the −/+ steps, the form's verdict against what an exchange
  accepts over 5000 random entries, and an English line for every text the trading post shows); RuntimeChecks 208
  (the game still has the panel methods and fields the patches use; the panel title is the one read of the entity's
  name, and the patch reads the trading post's title there instead).

## 1.4.0-alpha8

**Fixed: planting marks could desync a co-op game when players had sliced the view to different layers.** The
planting and cancel-planting tools level the dragged area with the game's terrain picker
(`TerrainAreaService.InMapLeveledCoordinates`), which stops at the layer the local player has sliced the view to
(`ILevelVisibilityService.MaxVisibleLevel`). The mod played the planting event again on every computer from the dragged
blocks and the camera ray, so each computer levelled it with its own view: a player whose view was sliced lower got
the marks at another height than everyone else.

- **The planting event now carries the tiles the marking player levelled**, and played on each computer the game's
  marking acts on exactly those tiles (its own checks of which tiles may be planted still run). Nothing reads the view.
- **Older events** (without the tiles) are levelled from the dragged blocks alone: the tile above each block that stands
  on ground, which is what the marking player's own game gave. Also view-free.
- **Colony rules judge the recorded tiles**, and the host's trimming of tiles another colony owns trims the list that
  is marked.
- A guest's pending planting marks show the recorded tiles.
- Tree cutting and demolition marks were checked and were already safe: they carry tiles and entities levelled by the
  marking player.
- Checks: RuntimeChecks 199 (the game's own levelling over a made-up terrain gives a different height in a sliced
  view; the fallback matches the game; the replay uses the recorded tiles; colony trimming applies to them).

## 1.4.0-alpha7

**Fixed: "tick once" desynced a co-op game.** The game's pause key (period by default, not only in dev mode) pauses a
running game, and pressed again while paused advances the game by exactly one tick. That tick runs through
`Ticker.TickOnce`, which never goes through `TickableBucketService.TickBuckets`, the method the mod drives the game
by. So the mod's own tick (playing the players' actions, counting ticks) was skipped, and the tick ran on the
computer of the player who pressed it only, desyncing the game.

- **In a co-op game, tick once is refused**, with a notice saying why: unpause to play on. Pausing with the same key
  works as before.
- Single player is unchanged.
- Checks: RuntimeChecks 189 (the game still has `Ticker.TickOnce` and `SpeedControlPanel.PauseOrTickOnce`).

## 1.4.0-alpha6

**Fixed: buildings placed before the game was hosted belonged to no colony.** A new co-op game starts in single
player, and what the host places before clicking Host is placed directly, not as an action, so nothing names its
colony. The mod gave such buildings a colony later, but only once they had a district, and a construction site has
none until it is finished. Seen after two days of a new game: 7 of the first colony's construction sites (three
lodges, a farmhouse, a warehouse, a pump and a tank) still "waiting for a colony" in the diagnostics report. While
they waited, their land didn't count, and in the simulation they were nobody's (any colony's builders could build
them). Every computer agreed, so it was never a desync.

- **A building with no colony now takes the owner of the road at its entrance**: the point the game finds a
  construction site's builders by, read from the road map that changes only at tick boundaries. Before, it looked at
  the building's own corner tile, which is right for a path but never a road for a building.
- **Failing that, the colony whose land it stands on**, if it is one colony's: for a site no road reaches, such as a
  pump placed a level above its path. One on nobody's land, or across two colonies' land, keeps waiting.
- **A district center has its colony from the moment it is made.** The game's own starting building on a one-start
  map was placed with no colony and took one 16 ticks later; its land now counts from the start.
- The same rules apply to buildings from saves older than the two-colony builds, and to a dam, levee or platform with
  no district.
- Saves from 1.4.0-alpha5 load as they are; buildings still waiting take their colony within 16 ticks, at the same
  tick on every computer.
- Checks: StabilityTests 255 (a building takes the land under it only when that is one colony's); RuntimeChecks 187
  (the game still finds a construction site's builders by the entrance the mod reads; the land code reads nothing that
  differs between computers).

## 1.4.0-alpha5

**A guest sees its own actions sooner.** Placing a building or marking an area as a guest took well over a second to
show at speed 1, with a 9 ms ping and a fast computer.

- **The guest no longer runs a tick behind the host by design.** A guest paused itself whenever it had played every
  tick the host had sent, until the host sent the next one. So it could not play a tick until the host had already
  started the tick after it: always at least a whole tick behind (0.6 s at speed 1). Now it plays the tick it is in and
  waits only at the start of the next one, for the host's word for that tick (the gate was already there). A guest in
  step now runs a few milliseconds behind the host instead of a tick. A guest held there for more than 0.1 s (the host
  or the network is late) still stands still, so its beavers don't walk on the spot. The connection panel's *Waiting
  for host* now means held there for a while, not "nothing queued", which is normal for most of a tick now.
- **Catching up after a hitch goes further at speeds 1 to 3.** A guest caught up only once more than two ticks behind,
  and stopped at one. At speeds 1 to 3 (a tick of 0.2 s or more) it now catches up once more than one tick behind, and
  all the way. Faster speeds keep the wider buffer, where a tick is short and the network's jitter would show.
- **The click shows at once.** Until the host's answer comes back, the tiles of what a guest placed, marked for
  planting or cutting, or marked for removal are tinted (red for a removal). The tint only draws: it creates nothing
  in the game and can't cause a desync. It goes when the action comes back, when the host refuses it, or after 8 s.
- **A refused action is explained.** When the host refuses a guest's action (another colony's land, not unlocked,
  dev mode off...), it now tells that guest why, instead of saying nothing. The message changes nothing in the game.
  When one click is refused twice in a row (an unlock for lack of science, then the building it was for), the first
  reason is the one shown.
- **Direct IP links send without delay:** no Nagle delay on the socket, and each message's length and first bytes go
  out in one write (up to about 200 ms saved per message on some systems). On Steam links it is one message instead
  of two.
- The host's placement check in replays no longer asks the scene for all its root objects every time. How long those
  checks take is now in the report ("Placement checks in replays").
- No more warnings for normal play: the host no longer logs "Event past time" and "late event" for every guest
  action (they arrive a tick before the host plays them, as designed), and a guest no longer logs a warning each time
  it waits for the host at the start of a tick.
- **Diagnostics report:** a *Co-op* part under Performance lists:
  - each link (Steam or Direct, ping, ticks behind, frame rate);
  - for a guest, how long its own actions take to come back at each speed (average, median, slowest, in ms and
    ticks), how many were refused or never answered, how far behind the host it runs (sampled once a second), and how
    often and how long it waits for the host.
- Checks: StabilityTests 254 (catch-up at speeds 1 to 3, message framing, the delay numbers); RuntimeChecks 184 (a
  guest's tag and a refusal's reason survive the event JSON; the game methods the tint uses).

Not done, and why:
- **Playing a guest's action in the middle of the host's tick** (the plan's first item) would need both computers to
  apply it at the same point inside a tick, which a guest can't reproduce. At a tick boundary, which is safe, the host
  already plays a guest's action at the next boundary and sends it on at once.
- **Reusing one preview per building for the host's placement check:** moving a preview tells its components, which
  feed the preview-only services, so a reused preview could change a later check. Without testing it in a game that
  risks a desync; the check now reports its cost instead.

## 1.4.0-alpha4

**Fixed: a desync while building roads to construction sites**, seen on the guest a few ticks after the host placed
a District Crossing and the paths to it. The game gives a construction site (and a building, for some purposes) its
district from an "instant" copy of the road map, which it brings up to date at the end of every frame. A tick is spread
over several frames, a different number on each computer, so a road finishing mid-tick joined a site to its district
after a different share of the tick on each computer, and a hauler or builder ticking in between could choose
differently. In a multiplayer game that copy is now brought up to date at the start of each tick, with the regular road
map, at the same moment on every computer. Previews (what the local player is placing) still update every frame. The
host's log showed the tell-tale sign: the crossing's half took its district's worker type at a frame, on the host
only. The other game systems that follow that map now also act at the same moment everywhere:
- automatic migration between districts;
- population and resource counters (automation);
- district connection changes.

While the game is paused, a building placed or removed gets its district when the game resumes.

- **Dev mode in co-op.** Its tools change the game on the computer they are used on. The fix applies to the two that
  are useful for testing:
  - the instant unlock (Ctrl-click on a locked building or bot toggle) is now an unlock like any other, played on every
    computer, without the science cost (before, it unlocked on one computer only: the host's colony had a building
    unlocked on the host and not on the guest);
  - a construction site's *Finish now* is played on every computer, and only the site's colony may use it.

  Both only while the host has dev mode on: the host decides whether a game is being tested. The others (deleting any object, the dev panel's other buttons) are still not shared. A notice says so when dev mode
  is switched on in a co-op game.
- The game's own worker type change when a building joins a district (it takes the district's default) is played
  where it happens, in the tick, instead of being sent as a player's action (a guest refused it for another colony's
  building).
- **Diagnostics:**
  - the daily check has a new part, `districts=`: which district each building and construction site is joined to;
  - when a guest desyncs, the other computers write their report too, so the two can be set side by side;
  - a report written after the session ended still says whether this computer was the host or a guest;
  - the report says whether dev mode is on, or was on this game.
- RuntimeChecks: 9 new checks, 179 in all. They pin the game methods the fixes use, and check that the game's
  end-of-frame navmesh update still does exactly what the fix moves into the tick, and nothing else.

## 1.4.0-alpha3

**Fixed: every game crashed while loading** ("ConstructionSitePanelDescriptionUpdater isn't instantiable due to
missing dependency: GoodService"), in every build since 1.3.0-exchange-alpha1. The trading post's panel asked the
game for its goods service by its class, which the game only hands out by its interface (`IGoodService`), so the game
could not build its entity panel.

- **New check** (RuntimeChecks): it reads, from the game's own code and Mod Settings', which services each context
  (main menu, game, map editor) provides, and checks that everything this mod gives the game to build asks only for
  those. Run against 1.4.0-alpha2 it reports exactly this crash.
- The land map is made on first use, after every service has loaded, instead of relying on the order services load
  in.

## 1.4.0-alpha2

**A diagnostics report.** Press Ctrl+Shift+J (or *Diagnostics report* in the Ctrl+T window): a plain-text report is
copied to the clipboard and saved in `BeaverBuddies-Reports` next to `Player.log`. A computer that desyncs writes one
by itself. It holds:

- **Performance:** frame rate (average, slowest 5%, slowest frame), ticks per second, entity and beaver counts, and
  the time spent in each busy part of this mod (resource searches, builder job checks, working hours, trading post
  workers, land bookkeeping, previews, daily checks), measured with two timestamps per call.
- **Colonies:** each colony's districts and population, homeless beavers and adults without a job, land, buildings
  (and how many are unfinished), working hours, and the building statuses showing (such as unreachable, no workers,
  lack of resources); beavers in no district.
- **Trading posts:** each post's workers per half and its exchange, with what is holding it up (waiting for the other
  side, no workers, the other half full).
- **Desync:** once a day every computer takes a one-line check of the colony state (owners, building owners, land,
  population, exchanges, marks, science, working hours, away days) and logs it. The lines for a day must match on
  every computer; the first part that differs shows where they stopped agreeing. The report carries the last ten.
- **Recent log:** the last colony and desync log lines.

Reads only; nothing in it changes the game.

## 1.4.0-alpha1

**Ready for longer games: land you can see, colonies that don't dead-end, better trading, its own identity.** Not yet
played in a game. Every player must install this build. Saves from 1.3.0-exchange-alpha1 and alpha2 and the
two-colony alphas load.

- **The land shows.** Every colony's land is outlined in its colour while a building, planting, cutting, demolishing
  or founding tool is in hand, and any time with Ctrl+L.
- **Colonies are handed over instead of dead-ending.** A colony with no beavers or bots left for a whole day goes to
  the nearest living colony. A colony whose player has missed a number of in-game days of hosted co-op play in a row
  (new host setting, 7 by default, 0 for never; single-player days and the first day after loading don't count)
  goes to the nearest colony whose player is playing. The host may hand over, from the new
  Ctrl+T window, any colony whose player is away or that has no beavers. What moves: district centers, buildings,
  land, marks, stock and the science pool (the receiver may also build what the old colony had unlocked). The player
  who lost their colony may found a new one.
- **Founding keeps its distance:** at least 20 tiles from another colony's buildings and paths, so both have room to
  grow. The founding preview says so.
- **Its own identity:** mod id `timbermods.BeaverBuddiesMultiColony`, name *BeaverBuddies MultiColony (alpha)*, links to
  this project. The `workshop_data.json` that still pointed at the original BeaverBuddies Workshop item is gone: a
  first Workshop upload makes a new item. If another BeaverBuddies is enabled too, the main menu names it; if it
  started first, this one stays out of the way instead of patching the game twice. Mod Settings start from their
  defaults once (they are kept under the new id). A development build now deploys to `Mods\BeaverBuddies-MultiColony`.
- **Trading:** the offer form picks goods from a grid of icons with each colony's stock; **Repeat** makes a standing
  deal that starts again each time it completes; **science and adult beavers** can be exchanged too (they move by
  themselves, in step: science 25 at a time, beavers one at a time, the last adult always staying); the new **trading posts and colonies
  window** (Ctrl+T, or *Trade* at the top right) lists every trading post of your colony with its progress and a
  *Go to* button, and every colony with its population and whether its player is playing.
- **Fixed:** the debug key Ctrl+Shift+K (the host acts as the next colony, for testing alone) did nothing since
  1.3.0-exchange-alpha1: its handler had gone with the old border display.
- **For large games:** with detailed logging, one log line a day per colony (population, land, exchanges);
  headless checks that land for 20,000 building tiles on a 256 by 256 map stays quick; a scale test script (Script C).

## 1.3.0-exchange-alpha2

**Barter exchanges at trading posts, and colonies kept apart everywhere else.** A trading post now works like one: the
two colonies agree on "1000 logs for 250 gears" and their beavers carry it out. And an audit of everything else one
colony could do to another closed every way except the trading post and the shared world (water, weather, the game
clock). Not yet played in a game. Every player must install this build. Saves from alpha1 load; a gift that was under
way in one is dropped.

**Kept apart**

- **A colony changes only itself.** The rule that let a player change the colony of a player who was not playing is
  gone: another colony's things are refused, always. Renaming follows the same rule, and buildings, relays and memory
  cells may be wired only to their own colony's. A District Crossing between one colony's own districts is that
  colony's; only a trading post may be taken down by either.
- **Every building carries the colony that placed it** (saved), from its first moment as a construction site. So a
  site not yet connected, a building cut off from its roads, and a dam or levee (which have no district) all have an
  owner. Buildings from older saves take their district's owner the first time they have one (a path, the owner of
  the district whose road it is); an older dam or levee counts only by the land it stands on.
- **Land.** A colony's land is every tile within 10 tiles of its buildings and paths, first come: a tile two colonies
  reach is the land of the one that got there first (saved), so building towards another colony stops at its edge.
  Building, founding, marking trees and planting are refused on another colony's land, and nothing may be built on or
  next to another colony's roads; a District Crossing goes across the edge of two lands. The building preview turns
  red with the reason before the click. Demolishing and clearing wild things on another colony's land is refused,
  and the demolish tool's rectangle no longer clears another colony's planting marks.
- **Marks are per colony.** Planting marks and trees marked for cutting remember the colony that made them (saved).
  A colony's planters plant only on its own marks, its lumberjacks cut only the trees it marked, and unmarking an area
  removes only your own marks.
- **Each colony's beavers work for it alone.** The game hands some work to any beaver who can walk there; now:
  builders build, demolish and pick up recovered goods only for their own colony (the placer builds a District
  Crossing; either colony may take down a trading post), and lumberjacks, gatherers, farmhouses and scavengers take
  only what grows on their colony's marks, or wild things on land no other colony holds.
- **Working hours per colony.** A player's working-hours buttons set their own colony's hours; its beavers, workshops
  and chronometers follow them, and the panel and the clock's needle show the player's own. The game's setting stays
  as it was, identical everywhere, for a colony that never chose.
- **Bot worker types per colony** (with separate science), like building unlocks. A save from before keeps its bot
  workplaces: every colony starts with the bot worker types the game had.
- **No migration between colonies**, in either direction, by the Migration tab or otherwise.

**Exchanges**

- **Exchanges.** One colony offers a number of one good for a number of another; either number may be 0 (a gift, or a
  request for help). The other colony accepts or declines; the offering colony may withdraw; either may cancel a
  running exchange. Each colony's crossing workers fetch their side from their own storage and bring it to their
  half; the other colony's workers haul it away. The sides move in step: neither side more than a tenth of its
  amount (at least 10) ahead of the other. The other player gets a notice when an offer is made, answered or ended,
  both when it completes, and a player whose offer or answer could not be played is told so. One exchange per
  trading post, saved on the two halves of the crossing, with a number that keeps answers and cancels from reaching
  a later exchange. Only the colony whose half it is may offer, answer or cancel from it.
- **Only what is owed crosses.** Goods pass only for a running exchange and only up to what their colony still owes;
  a load still on the way when an exchange ends stays on its own half and is carried home.
- **Only exchanges cross a trading post.** Import and export settings no longer move goods between two colonies,
  neither by the crossing's workers nor by the crossing's own exporter. Crossings between one colony's own districts
  work as in the game.
- **Gifts of goods are gone** (the *Give 10* buttons and the *could use* list): an exchange asking for 0 is a gift.
  Science gifts stay.
- **Imports no longer start Disabled** in a separate-colonies game. That only stopped goods from flowing to another
  colony, which trading posts now do themselves. New districts get the game's own defaults, and founding in a shared
  game no longer closes the existing districts' imports. Districts made with alpha1 keep their saved settings.
- **A District Crossing holds 100 of each good** (the game's is 30), in every game. The patch rewrites the one place
  the game reads its fixed number, and a check fails if the game changes that.
- The trading-post panel's buttons say so when there is no co-op session, instead of doing nothing.

## 1.3.0-exchange-alpha1

**Colonies as owned districts, trading posts and separate science**, following
[design/TRADING-EXCHANGE-PLAN.md](design/TRADING-EXCHANGE-PLAN.md). Replaces the land split of the two-colony alphas.
Not yet played in a game. Every player must install this build.

- **No territory.** Anyone may build anywhere. Each district center carries its owner's colony (saved); buildings,
  beavers and stock belong to their district. A player changes their own colony, things in no district, and the
  colony of a player who is not playing this session.
- **Players are remembered** by Steam ID (or an id kept on their computer): a player gets the same colony every
  session, whoever hosts. New players take the next free colony (up to four); more join as helpers of the host's.
  The *Colony the host plays* setting is gone.
- **Founding** for every player without a colony, once; the new district center must not join another colony's
  roads. A multi-start map's start N is player N's.
- **Trading posts:** a District Crossing between two players' districts. A saved ledger of goods passed each way;
  gifts of goods (whatever the partner's imports, no bounce-back) and of science; a panel with what the partner
  could use. Crossings need no science and cost 10 logs in colony games.
- **Separate science and unlocks** (setting, fixed per save): a pool and an unlock set per colony; production, relic
  rewards, the control tower's upkeep and automation counters use their building's colony; the toolbar and top bar
  show the local colony's. Bot worker types stay shared.
- **Road networks:** zipline links are judged by the host alone (the replayed check read per-computer state); roads
  joined by two simultaneous placements are detected at the same tick everywhere and both players are warned.
- **Unlocks replay safely:** an unlock no longer affordable is skipped instead of stopping the session (also in
  shared games); with separate science an unlock the colony already has is not paid twice.
- **Fixed (also in shared co-op):** replaying a placement no longer runs the game's district-join check, which read
  what the local player was hovering and could refuse a building on one computer only (a desync).
- Map areas (tree cutting, planting) are shared; beavers may be sent to another colony by hand but not taken.
- Saves from the land-split alphas load with their district centers given the owners of their old land.

## 1.2.0-two-colony-alpha5

**Each player's screen shows their own colony only.** Reported from play: right after founding, player 1's top bar
showed 260 food and player 2's 130, and both saw the same beavers. The game kept the colonies apart; the interface
added them together, because Timberborn shows the whole settlement whenever no district is selected (player 1 saw
both colonies' 130; player 2 had their district center selected and saw only it).

- With nothing selected, the top bar's goods, population, housing, workplaces and wellbeing count the local
  player's districts only.
- Selecting one of the other colony's buildings opens its panels but no longer switches the top bar to their district.
- The batch control window (F1 to F10) opens on the player's biggest district, and with *Global* chosen its lists
  show only the player's colony (tabs that always list everything, mechanical and migration, included).
- Alerts and the notification journal only count the player's colony.
- Display only: nothing simulated or saved changes, and each computer may show different figures safely. Outside a
  co-op session everything is shown, as in the game. Still whole-map: science, and the Global history graphs in
  F9/F10 and in a good's tooltip.

## 1.2.0-two-colony-alpha4

**Colony 2 can be founded in any hosted game, once.** Reported from play: the second player pressed Ctrl+K and was
told there was no colony waiting to be founded, because the save had not been created with the setting on.

- Founding no longer depends on how the save was created. In any hosted game without a colony 2, its player can
  found it once, at any time: offered on joining, and on **Ctrl+K**. A save that recorded no starting settings gives
  the new colony the game's default (Normal) starting beavers, food and water; colony 1 is measured from its
  recorded start, or else its most populated district center.
- The host setting is now **Separate colonies (alpha)**, **on by default**, and also gates founding (off: one shared
  colony, as in the Stability Fork). The host's choice is sent to guests when they join.
- Only a new game created with the mode on restricts colony 2's player to founding; any other save plays as one
  shared colony until colony 2 is founded. Founding itself is still checked at the moment it happens, on every
  computer, from saved state only.
- Clearer messages when founding is not possible (colony 2 exists, the host has it off, not in a session).
- The border key works after a mid-session founding without reloading.

## 1.2.0-two-colony-alpha3

Two fixes to District Crossings, found by reading the code. Not yet played in a game. Every player must install this
build.

- **The border between two colonies is a straight line along the map grid**, halfway between the two starts, across
  the axis on which they are further apart. A District Crossing is three tiles wide and needs three tiles in a row
  on each side of the border. With the border measured by nearest start, starts placed diagonally from each other
  (about 30 to 60 degrees off the grid) left no such place anywhere on the map, so no crossing could be built.
- **A crossing pair is accepted whichever way it faces.** The game records the half under the cursor first. When
  that was the half across the border, the host refused it (the placer's own half was not there yet) and then placed
  the own half alone. The host now notes every crossing half on its placer's own land before judging a set of
  actions.

## 1.2.0-two-colony-alpha2

Adds **founding on standard maps**. Not yet played in a game. Every player must install this build.

- **Standard maps (one start):** the host's colony starts as usual; colony 2's player founds it by placing a district
  center anywhere that leaves colony 1's buildings on its own side (offered on joining, and on **Ctrl+K**). It appears
  finished with the new game's starting food, water, adults and children, and the land is then divided between the
  two district centers. The founding is re-checked at the moment it happens, on every computer, and skipped with a
  notice if the spot has changed (built on, or blasted) in the meantime. Colony 1 is measured from its starting
  building, recorded when the game places it.
- A multi-start map played with a single start also uses founding.
- README rewritten as a player's guide.

## 1.2.0-two-colony-alpha1

An alpha of **separate colonies** ([TWO-COLONIES.md](TWO-COLONIES.md)), on top of 1.1.10, in the new
BeaverBuddies-MultiColony repository. Not yet played in a game. Every player must install this build: the join
check compares the mod build, and each action now carries who sent it.

- **Opt-in per new game.** A host setting, *Separate colonies for new multi-start games*, gives each start of a new
  multi-start game its own colony. The save records the mode and the start positions; shared-colony games and old
  saves are unchanged and save nothing new.
- **Land is divided between the starts**, with a border strip on each side where only District Crossings may stand, so
  the colonies' roads never meet.
- **The host stamps who sent each action** (a guest cannot claim another number) and judges every action just before
  replaying it: actions on the other colony are dropped and never reach anyone; area actions keep only the actor's
  own tiles and objects. Every action type declares what it touches, and a check fails when one does not.
- **Seats:** the host chooses its colony in the settings (*Colony the host plays*); guests play the other.
- **Placement previews turn red** with the reason on the other colony's land or on the strip; refused actions show a
  notice. A District Crossing pair may straddle the border, each half on its colony's strip; the half on the other
  side is accepted only when the placer's own half stands behind it.
- **Trade starts closed:** every good of a new district starts at import Disabled, so goods cross only once the
  receiving player opens a good; the giver limits it with the export threshold.
- **Standard maps (one start):** the host's colony starts as usual; the second player founds colony 2 by placing
  a district center anywhere that leaves colony 1's buildings on its own side. It appears finished, with the new
  game's starting food, water, adults and children, and the land is then divided between the two district centers.
  Offered on joining, and on **Ctrl+K**.
- **No automatic migration between colonies**; manual migration to the other colony is refused.
- **Border display** (key **K**, and whenever a tool is active), the colony beside each name in the connection
  panel, and a debug key for a host testing alone (**Ctrl+Shift+K**, debug mode only).
- Mod renamed *BeaverBuddies - MultiColony (alpha)* in the mod list; the mod ID is unchanged, so remove other
  BeaverBuddies copies before installing.

## 1.1.10

The last Stability Fork release, on top of 1.0.9. It contains everything from the three 1.1.10 pre-releases (1.1.10-release-candidate, -2 and -3): a
cheaper pass over every entity on each tick, a plainer connection panel, and chat drawn in the color of each player's cursor. Every player
should install this build: the join check compares the mod build, so it will not join a session with an earlier version. Nothing new is sent
over the network.

The fork owner played 1.1.10 in multiplayer over Steam invites for more than an hour, in large colonies (300+), and reported that it worked very well.

### The pass over every entity on each tick is cheaper

Before each batch of entities ticks, this mod visits every entity in it to keep the animation of the
ones that walk (the beavers and bots: 361 of the 11,464 entities in the colony below) in step
between the players. To find them it looked up a component on every entity, on every tick, and it
also folded every entity into two hashes that only the detailed log ever prints.

Measured in a two-player recording of a large colony (speed 7, 11.7 ticks a second), that pass took
7.1 ms a tick on the host and 7.4 ms on the guest, about a fifth of the guest's 36 ms tick. About
3.2 to 3.5 ms of it was that component lookup (estimated from a sample of one lookup in sixteen)
and about 3.4 ms was the loop and the hashing together.

- The pass now remembers, for each position in a bucket, whether the entity there has the walking
  component, and only asks the game again when a different entity turns up at that position.
  Adding or removing an entity shifts the positions after it, and those are asked again; nothing
  has to be invalidated, and a stale answer cannot be reused because an answer is only trusted for
  the very same entity object. This relies on an entity not gaining or losing that component once
  it is ticking. The walkers themselves are handled exactly as before.
- The two hashes ("Order hash" and "Move hash" in the detailed log's line for each tick) are now
  only kept while detailed logging is on, which is when they are printed, and nothing else read
  them. They start from zero when detailed logging turns on, including when a desync turns it on, so
  both players' lines agree from the first one. Their values are therefore not comparable with a log
  written by an earlier build.
- Nothing that is simulated, sent or saved changes.

Expected effect: roughly 3 to 6 ms less on each tick, between about 8% and 17% of the guest's tick
in that recording. That is an estimate from the measurements above (how much of the
loop and hashing time was the hashing is not known); it has not been measured.

What it does not fix: in the same recording the guest's frame rate fell from 23 to 8.5 frames a
second over about seven minutes at speed 7, and an earlier recording of the host showed the same
kind of slowing (from 80 to 29 frames a second). Most of that time was spent in Unity's late-update phase (about 20 ms a tick on the host
and 34 ms on the guest), which is not code of this mod, and what runs there is not known. Every
recording so far started fast and slowed down over a session at a high speed, and a restart
started fast again. This release does not change that.

### A plainer connection panel

- **One dot.** While the panel is expanded, the dot beside the sync status (green, yellow or red with the status) is the only one: the dot beside
  the title and the dots beside each player are gone. A collapsed panel is one line with no status row, so it keeps its own dot.
- **A player's row is a name and a ping.** The "You" and "Host" tags are gone. The host reads each guest's ping. A guest reads its own ping to the
  host on the host's row, and the ping the host measured for every other guest. Over 80 ms the number is yellow and over 160 ms red, and "No
  response" is red.
- **Your own row is bold, with a dash where the ping would be**, since you have no ping to yourself.
- **The collapse button has a box around it** (a small "-", or "+" while collapsed), so it is not mistaken for that dash, which sits at the same
  edge of the panel. It does what it did before, and clicking the title still collapses and expands the panel.

Screenshots of a host, alone and with a guest, showed the header without its dot, the sync dot as the only one, a guest's row and your own row in
bold with a dash, the boxed collapse button, whole chat lines in each player's color (one yellow, one pink), and that the game's font draws bold.
Before the box was added, the collapse button's dash sat directly above your own row's dash, which is why the button is boxed.

### Chat takes the cursor colors

A chat line, name and message, is drawn in the color you see on that player's cursor: the color they chose (their Ping Color), or the one you set
for them under Options, Player cursors. Change that color and the lines already written change with it, within a moment. Your own lines use your
Ping Color. A player who has left, or whose cursor is off, keeps the color you saved for them, else the one their messages carried. A color too dark
to read on the panel is lightened, as before. Before, only the name was colored, and always in the color the player chose for themselves.

### Validation

- Release Steam and non-Steam builds succeed with no warnings. 210 StabilityTests (9 new for the memory of which entities walk, and 2 for the
  panel and the chat colors), 69 RuntimeChecks against the built mod and 3 Python checks pass. None of them can draw the panel.
- **Played:** the fork owner played 1.1.10 in multiplayer over Steam invites for more than an hour, in large colonies (300+), and reported that it worked very well.
- **Not checked:** how much time the entity pass saves has not been measured, and lines already written changing color after a cursor color is
  changed has not been checked. The pass works on Unity's entities, which the checks cannot create, so it is covered by its own checks with stand-ins,
  the build and the runtime checks loading the mod, and by that play.

## 1.0.9

It contains everything from the four 1.0.9 pre-releases: the Steam ping fix, the
fix for controls that stopped answering, the frame rate easing, and the compact chat and panel
layout. Every player should install this build: guests now send the host one more number than
before (their frame rate), and the join check compares the mod build, so it will not join a session
with an earlier version.

### The ping over Steam no longer grows with the game speed

Reported as a good ping at a low game speed and 200 to 300 ms at a high one. **The fork owner ran a
session at 11.7 ticks a second (a true speed 7) with the ping under 100 ms.**

Over Steam the ping is not only the network. Steam is only served from the game thread, and the
game thread served it once per frame. A probe passes four of those pumps on its way round: the host
sending it, the guest receiving it, the guest sending the reply (queued just after the pump that
delivered the probe, so it waits a whole frame) and the host receiving that. Run through the real
transport and ping code over a fake Steam network, that comes to about a frame and a half per
player, so the panel showed roughly the round trip on the wire plus 1.5 x (the host's frame + the
guest's frame). The waits also delayed real traffic, not only the number: every action a guest
made and every tick the host sent waited for the same pumps.

At a low game speed a frame is short and this is a few milliseconds. At a high speed it is not.
The game ticks as many of a tick's 129 buckets in a frame as the frame's time, multiplied by the
game speed, asks for, so the simulation is spread over the frames it needs and a frame lasts
roughly the non-simulation work divided by (1 - the share of the main thread the simulation
takes). The share grows with the speed. In a host log at a true speed 7 the simulation took 51%
of the main thread (44 ms a tick, 11.7 ticks a second) and frames were 17 to 19 ms, about twice
what they are at speed 1. A computer that needs 90% of its main thread at that speed has frames ten
times as long as an idle one, and a guest that is catching up (it runs up to speed 10) can need
more than 100%. So the ping rises with the game speed, and most of all with the frame length of the
slower computer.

- The game's tick loop now lets Steam move data between the buckets of a tick, at most once every
  millisecond, and straight after a tick's events are queued for the guests. This only moves data
  (one native call per connection when nothing is waiting), runs on the game thread like every
  other Steam call, and does nothing outside a Steam session. The once-per-frame pump is unchanged
  and still does everything else: connecting, closing and failures.
- The simulation over the fake Steam network (5 ms each way) shows the pings below, in ms. The last
  column adds a 12 ms part of every tick that cannot be interrupted (the singletons and the wait for
  the parallel work), which the extra pumps cannot reach.

  | Host frame | Guest frame | Once per frame | Between ticks | Between ticks, 12 ms uninterruptible |
  |---:|---:|---:|---:|---:|
  | 8 ms | 8 ms | 35 | 32 | 31 |
  | 17 ms | 17 ms | 58 | 18 | 20 |
  | 17 ms | 60 ms | 123 | 17 | 19 |
  | 17 ms | 100 ms | 177 | 17 | 19 |
  | 100 ms | 100 ms | 323 | 13 | 18 |
  | 200 ms | 200 ms | 713 | 13 | 17 |

  What is left is the part of each frame that is not simulation (rendering, the garbage collector),
  where nothing can be served, so it is a few milliseconds at most for a frame like the ones in the
  host log.
- A direct-IP connection is not affected: its data moves on its own threads, not on the frame.
- The log now says how long data waited, once a minute while someone is connected, for example
  `Steam link timing over 60 s: data waited for the game thread 17.3 ms on average and up to 118 ms
  if Steam were only served once per frame (4 gaps over 50 ms); with the pumping between ticks it
  waited 1.6 ms on average and up to 24 ms (0 gaps over 50 ms).` The first figures are what a
  once-per-frame pump would have cost in that session and the second are what it cost, so one
  session shows both, to read next to the ping in the panel.

Not confirmed: the guest's frame length at a high speed has never been measured, so it is not known
that this accounts for all of the 200 to 300 ms that was seen. The timing line in `Player.log` on
both computers is what to look at if the ping is still high.

### Controls that stopped answering after a message was closed

Reported after a disconnect or a resync attempt: once the message was closed, the controls did not
work as expected, and Escape did not open the menu. Going through every way a session can end, in
this mod and in the game's own code, found five separate causes. Each leaves the game running but
ignoring the player. **The fork owner confirmed that the controls work after a disconnect.** Which of
the five that covered was not recorded, and the rest have not been seen in a running game: they come
from reading the code, and each fix changes a decision that is checked on its own.

- **After "Multiplayer has stopped", the menu could not be opened.** A multiplayer action that
  fails to replay stops multiplayer for the rest of that game and blocks every further action, so a
  half-applied action cannot make things worse. The block also covered the game menu: Escape and
  the options button both open it through the same call. The message tells the player to return to
  the main menu, so the only way out was to kill the game. The menu now opens, and everything else
  stays blocked.
- **A dropped connection left the dead session in place.** When a guest lost the connection during
  a game, its network was closed but the session stayed installed. Every action, the menu included,
  was then queued for a session that no longer existed and never played, and the game was held
  paused. The message meant to explain it was shown through the main menu's dialogs, which no longer
  exist once a game has loaded, and the fallback looked the dialog up where it is never registered,
  so nothing was shown at all. The session now ends the way a desync ends it: what the player does
  applies here again, the game stays paused, and the game itself shows the reason and the way out
  (open the menu to save, or to return to the main menu and join again). If the connection drops
  while the game is still loading, the message appears as soon as the game is up.
- **A cancelled or failed join or host left a dead session in the main menu.** Cancelling the host's
  lobby, or a join that failed after the connection was made (a host that had already started, a
  build mismatch), left the closed session installed until the main menu was loaded again. Whatever
  was played next from that menu, single player included, then started as a multiplayer game with
  nobody to talk to: paused for good, and Escape did nothing. A session that ends before it has a
  game is now cleared away, and a host who cancels a rehost from a running game goes back to
  playing locally.
- **Steam's overlay closing under a dialog.** While the overlay is open the game pushes an empty
  panel that blocks input, and pops it when the overlay closes, but only if it is still on top. If a
  dialog opened over it in between (an invite that cannot be joined, a connection error), the game
  left it in place for good: once the dialog was closed, a panel that no key could close sat on top
  and swallowed every key press, until the overlay was opened again. The panel is now removed as
  soon as the dialog above it is closed.
- **Input held when a session stops is cleared.** A desync already cleared the keys and mouse
  buttons held when its dialog appeared, so they did not carry over once it was closed. A failed
  action and a lost connection now do the same.

### The host can ease off for a guest's frame rate

- With the large colony speed limit removed, a slower computer can keep up with the simulation
  and still have a bad time: in a real session a guest stayed within a few ticks of the host at
  a true speed 7 while drawing 13 to 14 frames a second, because the simulation took about 63%
  of every second on that computer. The existing easing only looks at how many ticks behind a
  guest is, so it never reacted.
- New host choice, **Ease off below**: Off (the default), 20, 30, 45 or 60 fps. It is a line in
  the connection panel that the host clicks to pick the next value, and the same setting is in
  the mod settings. Only the host's value is ever used, and it can be changed at any time during
  a session.
- Guests report their frames per second in the reply they already send to the host's ping
  probe, about once a second. A guest reports nothing while its game window is in the
  background, where the system throttles it and the figure says nothing about the computer, and
  the host forgets a guest's figure as soon as a reply arrives without one.
- **The rule looks at the middle value of the slowest guest's last five reports**, which one or two
  bad seconds cannot move: a guest's one-second frame rates are noisy (anything from 2 to 59 fps
  within a few seconds, because a garbage collection or an autosave takes most of one second).
  Below the floor, the host drops 10% of the chosen speed, down to 30%, and waits for five fresh
  reports, so the next decision only sees frame rates from after the drop. It climbs back 5% after
  six reports in a row that are clear of the floor by some headroom (a quarter of the floor, at
  least 5 fps). In between it holds, which keeps it from see-sawing, because easing off is exactly
  what raises the guest's frame rate. A paused game and speed 1 are never eased, and switching the
  choice off or the guest leaving restores full speed at once.
- The percentage the host had to drop from is remembered. It does not climb back to it for a
  minute of play, then tries once; if that fails again from the same percentage the wait doubles,
  up to four minutes. Changing the floor, switching it off, or the guest leaving forgets it.
- It combines with the existing easing by taking the lower of the two percentages, never both
  multiplied, and the hold for a guest far behind still wins. The panel says which one is
  holding the host back: **Easing off** reads "75% (frame rate)" when it is this one. The host
  also sees **Guest fps**. Each change is written to `Player.log`, with the middle value the
  decision was made on.
- In a model of a guest that is fine up to 80% of the chosen speed and collapses above it, the
  host stays between 75% and 85% and tries the higher speed at most six times in eighteen minutes;
  a model of a guest that draws 15 fps at a true speed 7 is brought back above 30 fps with the host
  settled at 50% of the chosen speed, and a fast guest is never slowed at any floor.
- Like the other pacing, this changes how fast the host works through ticks, never which tick
  anything happens on, so it cannot change what anyone simulates.
- **Played once, with an earlier version of the rule** and the floor at 20 fps: no desync, and the
  guest's average frame rate went from 5 to 11 fps (1.0.8, same colony, true speed 7) to 21 to 27
  fps. But the host changed speed 68 times in seven minutes, between 60% and 95%, and never
  settled, because that version dropped after three bad seconds and climbed straight back. The rule
  above is the one that replaced it, and **it has not been played in a multiplayer session**. The
  clickable line in the panel has not been seen in the game.

### The chat and the panel are smaller, line up with the game's panels, and stay in front

From a screenshot with the frame rate easing lines showing: the chat was as tall as the whole top
of the panel, so with everything the host sees it ran down to the bottom of the screen and the
game's alerts ("Nothing to do in range") were drawn over its text box; and the pacing lines were
long enough to push the panel to its widest, wider than the game's beaver counters above it.

- **A compact chat.** The chat has a fixed height (150 interface units, about five lines and the
  box to type in) instead of matching the section above it, so it no longer grows with the rest
  of the panel.
- **The panel is as wide as the beaver counters above it.** Its width is measured from the
  game's own population panel (a root element named `Counters`) in the same corner each time the
  panel refreshes, so it lines up with it at any UI scale. Without those counters it follows the
  nearest visible panel above it; with nothing to follow it sizes to its text. A width
  outside 180 to 520 is never followed. Each change is written to `Player.log` with the widths of
  the panels in that corner, so a session shows what it followed if it ever looks wrong.
- **Short labels.** The pacing text is what made the panel wide, and labels wrapped onto two
  lines. The labels are **Guest behind**, **Easing off** and **Guest fps**; the values read "75% of
  speed", "75% (frame rate)" and "waiting for a guest". A check keeps every label within its
  column and every pacing text within 20 characters.
- **In front of the alerts while you type.** While the cursor is in the chat box, the panel's
  corner of the game's interface is drawn in front of the other corners, where the alerts are, and
  it goes back to its place when the cursor leaves (or the chat is hidden, collapsed or reset).
  The game defines each corner as ignoring the pointer, so this changes only what is drawn on
  top. If the game ever stopped positioning its corners on their own, it is left alone and a line
  says so in `Player.log`.

Not verified: none of this has been seen in the game. The decisions (which width to follow, the
height, the string lengths) are covered by checks; the measuring, the drawing order and how it
looks are not, and are the things to look at first.

### Validation

- Release Steam and non-Steam builds succeed with no warnings. 199 StabilityTests (38 new since
  1.0.8: 16 for the frame rate easing, 6 for the panel layout and label lengths, 5 for the Steam
  pumping and the ping, 11 for ending a session), 69 RuntimeChecks against the built mod (5 new,
  for the menu) and 3 Python checks pass (the water snapshot comparison, and the walker trace
  comparison's self-test).
- The four changes were built and tested on their own first and are combined here; the combined
  build was checked by the same suites, which is what carries the interactions between them
  (they meet in `ReplayService`, the connection panel and the tests). **The combined build has not
  been played as a whole.** What was seen in the game is said in each section above.

## 1.0.8

It contains everything in the 1.0.4 to 1.0.7 pre-releases below, which were never full releases
themselves. Every player should install this build.

The fork owner played this build in multiplayer at a true speed 7 (large colony speed limit
removed), the configuration in which 1.0.7 desynced within minutes both times it was tried, and
reported that it works great, with no desync in a ten minute session.

### A desync at high speed: a beaver's zipline state came from the animation

Seen twice in one evening at a true speed 7 (large colony speed limit removed), with and without
other mods' route map changes. Comparing the two players' verbose logs tick by tick showed the
same thing both times: entity order and random state identical, then the **move hash** (where
every walking character is) differing, and two to six ticks later a beaver arriving at a building
on one computer and not the other, which is when the random state differs and the desync is
reported. Both times it began a few ticks after a hitch on the guest, while it was catching up.

One input to walking speed is not simulation state. In a flooded tile a beaver's speed is
multiplied by its water penalty modifiers, and the zipline's is `IsOnZipline ? 0.5 : 1`.
`ZiplineVisitor.IsOnZipline` is switched by an event from the per-frame movement animation, when
the animated model crosses onto or off a zipline corner. Which frame that is, and so whether it
falls before or after that beaver's own tick, depends on frame rate and on how many ticks a frame
carried. At low speed there are many frames per tick and both computers switch at nearly the same
point; at a true speed 7, and above all on a guest catching up with several ticks per frame, they
can differ by a tick.

- In a multiplayer session the modifier now asks `ZiplinePathTracker` instead, which holds the same
  fact as simulation state: it follows the path corners the walker actually moved along, from the
  tick, and it is saved with the game. The values are the game's own. The animation, harness and
  swimming visuals still follow the animated model. Single player is untouched.
- This is the one frame-timed input to walking speed found by reading the game's code. The logs
  could not say which beaver differed, so it was not proven to be the cause of those two desyncs;
  what is known is that the desync did not come back in the session described above. If one
  does, the next item will say which beaver and why.

### Walker diagnostics, written with the water diagnostics on a desync

While debug mode is on, every walking character's position, path (next corner, corner count, last
corner, corner speed), speed inputs (base speed, bonus multiplier) and both zipline flags are kept
for the last 192 ticks, and written to `BeaverBuddiesDiagnostics/walkers-*.tsv` on both computers
when a desync is reported. Floats are written as exact bits. 192 ticks because the host is ten to
twenty ticks ahead of a guest by the time a desync is reported, and the first difference is
several ticks before that; the water snapshots of the two computers did not overlap at all.
`RuntimeChecks/compare_walker_traces.py <host> <guest>` prints the first tick and character that
differ and which columns differ. Nothing is recorded with debug mode off.

### The host no longer waits for a guest that is still loading

On a rehost the host kept the old session's tick count, compared it with the joining guest's tick
zero and logged `Host pacing: waiting for a guest that is 150 ticks behind`. It cleared by itself
and cost nothing, because the game was loading. A guest that has not ticked yet is no longer
counted as behind.

## 1.0.7 (pre-release, included in 1.0.8)

A pre-release for testing, on top of 1.0.6. Every player should install this build.

### A chat box in the connection panel

Below the connection panel, in the same rectangle and exactly as tall as the section above it, is
a chat box for the players in the game. See [CONNECTION-PANEL.md](CONNECTION-PANEL.md) for how it
behaves; in short:

- Type in the box and press Enter. Each line reads `Name: message`, the name in that player's
  Ping Color. The log follows new messages unless you scroll up.
- The host numbers every message and sends it to every player, the sender included, so everyone
  sees one conversation in one order.
- **Full history.** The host keeps the whole session's conversation (up to 2,000 messages) and
  sends all of it to a player who joins later. It goes out in a few compressed frames after the
  joining guest has its save, state and init event, and a message sent during the join is
  either in that history or queued behind it, never both and never neither. Chat is per session:
  a reload or a rehost starts an empty chat.
- Collapsing the panel hides the chat; the collapsed header shows how many new messages there
  are. A new optional key binding, **Chat: start typing**, puts the cursor in the box (unbound
  until chosen; clicking the box always works).
- While the cursor is in the box the game's hotkeys are switched off, using the game's own
  mechanism for text boxes, so typing does not move the camera. Focus is released whenever the
  chat is hidden, collapsed, or the scene ends, so the hotkeys cannot stay off.

How it travels: chat frames use the same separate lane as cursor activity and the connection
status feed. They are handled on the receive thread before they can reach the game's event queue,
so they never enter the replay script or the desync hash. The host assigns each message's sender
and number (a guest's own claims are ignored), rate-limits each guest to a burst of six and then
two a second, and everything a peer sends is cleaned (plain text, one line, 200 characters, no `<`
or `>`) and validated; a malformed frame is dropped and never ends the session. The lane gained an
in-order queue for this: its existing latest-wins queue would have dropped messages.

If the chat fails it disables itself and the rest of the panel carries on.

Not verified: none of it has run in the game yet. The transport is covered by automated checks
over real host and guest sessions; the look, the keyboard handling and the mouse wheel over the
log are not, and are the things to look at first.

## 1.0.6 (pre-release, included in 1.0.8)

A pre-release for testing, on top of 1.0.5. Every player should install this build.

### A failing message handler can no longer crash the game

1.0.5 fixed the one handler that crashed a guest's game in 1.0.4. The way it got there was still
open: the network layer's `Update` called its `OnError`, `OnSessionFault` and `OnMapReceived`
subscribers unprotected, from the game's update loop, and they show dialogs and load scenes. An
exception in any of them was an uncaught exception, which is the game's crash screen.

- `TimberNetBase` now calls every subscriber by itself inside a `try`. One that throws is written
  to `Player.log` and the others still run.
- If loading the save received from the host fails, the guest is told ("The save from the host
  arrived but could not be loaded") instead of being left in the menu with no explanation.
- The join messages reached from Steam callbacks (`ShowJoinError`, `ShowConnectionMessage`) are
  guarded the same way: a message that cannot be shown is logged.

### The host waits for a guest that is very far behind

Easing off (1.0.4) stops at 30% of the chosen speed. With the large colony speed limit removed,
30% of speed 7 is still about 3.5 ticks a second, so a guest that has stopped altogether (a long
save, a long garbage collection, a stalled connection) keeps falling behind while the host queues
events for it. Left long enough, that fills Steam's send buffer, and a full buffer that makes
no progress for 30 seconds ends the connection.

- When the slowest guest is more than **60 ticks** behind (about five seconds at a true speed 7)
  the host stands still, at any speed, until that guest is within **10 ticks**, then carries on.
- Waiting does not change the easing percentage: one stall says nothing about what a computer
  can sustain. A guest that leaves, or whose connection times out, releases the host at once.
- The connection panel shows the host **waiting for a guest to catch up** while it applies, and
  both changes are written to `Player.log`.
- In the test model a guest frozen for 30 seconds is never more than about 60 ticks behind,
  and full speed returns afterwards.

This changes how fast the host works through ticks, never which tick anything happens on, so
it cannot change what anyone simulates.

## 1.0.5 (pre-release, included in 1.0.8)

A pre-release for testing, on top of 1.0.4. Every player should install this build.

### A guest could be dropped, and then crash, right after a long load

Seen in a 1.0.4 session: a guest finished a 40 second load, its first network read failed with
`Steam networking error: ppOutMessages must be the same size as nMaxMessages!`, the connection
closed, and the game then crashed with a `NullReferenceException` in `PanelStack.Show`.

- **The dropped connection.** Steam messages are read in batches into a buffer of 64, up to 256
  per update. Steamworks.NET refuses a read whose requested count differs from the buffer's
  length. The code asked for "whatever is left of the 256" on each call, which is 64 for every
  call except the last one of an update when between 193 and 255 messages are waiting. That only
  happens when many messages have piled up, as they do while a guest spends a long time loading
  (more so with debug mode on, which sends more). Every read now asks for exactly one full
  buffer; the 256 is a soft limit that can be passed by less than one buffer. The loop lives in
  `ReceiveBatching.Drain` so it can be tested: a new check drives it with 0 to 1000 waiting
  messages against a fake that refuses a mismatched count, as Steam's wrapper does.
- **The crash.** The guest's "could not connect" handler is created in the main menu and used
  that menu's dialog stack. It is also what reports a connection lost later, in the game, when
  that stack no longer exists; showing the dialog threw, nothing caught it, and the game
  crashed. The handler now falls back to the game's own dialog, and if that fails too it only
  logs. A lost connection can no longer crash the game from here.

Neither change affects what anyone simulates.

## 1.0.4 (pre-release, included in 1.0.8)

A pre-release for testing. Every player should install this build: the game warns when mod
versions differ, and mixed versions are untested.

### Game speed

- New setting, **Remove the large colony speed limit** (off by default). Timberborn slows its
  own speed settings as the population grows (`GameSpeedThrottler`): above speed 1 it runs at
  `1 + (speed - 1) x factor`, and the factor falls with population. In a colony of about 350,
  speed 7 ran at 3.4 (5.7 ticks a second where speed 7 asks for 11.7) and speed 3 at 1.8,
  while the simulation was using about a third of the time a true speed 7 allows on the
  computer it was measured on. With the setting on, the chosen speed is the speed.
- In multiplayer **the host's choice applies to everyone** for the whole session. Every
  computer applies that scaling by itself, so if the host removed it and a guest did not, the
  host would run twice as fast, and because the guest's catch-up speed is scaled down too it
  could never recover. The choice travels in the message a guest receives when it joins; a
  guest's own setting is ignored during a session, and a host changing the setting mid-session
  changes nothing until the next one. In single player the setting applies at once.
- This only changes how fast ticks are worked through, never what happens in them, so it
  cannot change what anyone simulates.

### The host eases off for a guest that cannot keep up

- A guest that falls behind speeds itself up (1.0.2). That recovers from hitches, but a
  computer that cannot sustain the chosen speed at all falls further behind every second
  however hard it tries. Removing the speed limit makes that more likely, so the host now
  notices and slows a little, only when it has to.
- Guests report the tick their game has reached in the reply they already send to the host's
  ping probe, about once a second, so the host knows how far behind each guest is.
- Nothing happens while the slowest guest is within 15 ticks, or is further behind but closing
  the gap. If it is more than 15 behind and has not gained for four reports in a row, the host
  drops to 85% of the chosen speed; while already easing, two reports are enough for the next
  15% step, down to a floor of 30%. Four reports for the first step, because lag also grows
  for as long as a single stall lasts (a save, a long garbage collection, the window in the
  background) and a fast computer recovers from that by itself. Once every guest is within 4
  ticks the host climbs back 5% per report, so a guest that was only slow for a while gets
  full speed back. Speed 1 and a paused game are never eased.
- In the test model of a guest whose computer manages 8 ticks a second at speed 7 (which asks
  for 11.7), the host settles at 7.9 and the guest is never more than 31 ticks behind; without
  easing it is 660 behind after three minutes and still falling. A guest that manages 10 gets
  10.1, one that manages 5 gets 4.8. A fast guest with hitches, or with a single stall of up
  to 4 seconds, never slows the host.
- The connection panel shows the host **Slowest guest behind**, and **Easing off for guests**
  with the percentage while it applies. Each change is also written to `Player.log`.
- Like the catch-up rule, this changes how fast the host works through ticks, not which tick
  anything happens on.

### Validation

- Release Steam and non-Steam builds succeed with no warnings. 132 StabilityTests (sixteen new
  in `HostPacingChecks`: who decides the speed limit, the reply format and its limits, the
  easing rule step by step, the model above, a real host and guest session in which the host
  reads the guest's lag from its replies, and the panel), 64 RuntimeChecks against the built
  mod and 2 Python checks pass.
- Not yet played in a multiplayer session.

## 1.0.3

Every player should install this build: it exchanges a little extra information when someone
joins, so it will not join a session with an earlier version.

### Mod list warning

- When a player joins, the host and the guest each send the other their list of enabled mods
  (ID, name and version) as part of the compatibility handshake, and each compares the two
  lists. If they differ, both players are shown a warning that names the mods that are on only
  one computer or at different versions. The host sees it in the lobby, before choosing Start
  Game; the guest sees it as soon as the game has loaded. It is also written to `Player.log`.
- It is only a warning and never stops anyone joining: mods that only change the interface are
  harmless, and only the players can tell which mods matter. It exists because a mod that acts
  on one computer only makes the games drift apart into a desync: in a real session one player
  had an extra housing mod, which switched another housing mod off on their computer alone. The
  join check only compared this mod and the game, so nothing said so.
- The exchange happens after the build check has passed, on the same connection and inside the
  same time limit, so a different build is still refused before any mod list is sent. The list
  is bounded (32 KB compressed, at most 300 mods, names shortened and stripped of control
  characters), and anything unreadable is ignored: a malformed or oversized list can never end
  the session or put odd text in the warning. If a computer cannot read its own mod list it
  sends a marker the other side ignores, so nobody is told that every mod differs.
- The warning text is English only for now; other languages show the English text.

### Validation

- Release Steam and non-Steam builds succeed with no warnings. 116 StabilityTests (nineteen
  new: the list format and its limits, the comparison, the message, the exchange during the
  handshake including a refused build and an oversized list, and real host and guest sessions),
  64 RuntimeChecks (five new, using the game's own mod objects) and 2 Python checks pass.
- The warning has not yet been seen in a running game.

## 1.0.2

Every player should install this build: the game warns when mod versions differ, and mixed
versions are untested.

### Performance

- A guest now catches up to the host before it falls far behind at high game speeds. The original
  rule sped a guest up only once it was more ticks behind than the game speed: more than 1 tick at
  speed 1, but more than 7 ticks at speed 7. Both players run at the same nominal speed, so every
  hitch on the guest added lag that nothing recovered until it passed that mark, and at speed 7 a
  guest sat 3 to 5 ticks behind (about 0.3 to 0.4 s before it saw the result of its own actions, on
  top of the network delay). A guest now starts catching up once it is more than 2 ticks behind,
  whatever the game speed, and continues until it is within 1. It aims for a small buffer and not
  zero, because a guest with nothing queued has to wait for the host's heartbeat before every tick.
- This only changes how quickly a player works through ticks it has already received. The host
  still decides which tick every event runs on, so it cannot change what any player simulates. The
  host is unaffected (it is never behind), a paused game keeps the original rule exactly, and a
  guest is never slower to catch up than before. The 10x cap is unchanged.
- The catch-up speed changes less often than before. Every speed change notifies each animated
  building, and "ticks behind" naturally flickers by one as the host's tick arrives and the guest's
  finishes, so the original rule changed speed on almost every tick once it was active. Within one
  catch-up the speed now only rises, then drops back once. In the test model of a guest that loses
  0.3 s every 5 s at speed 7, average lag falls from 7.6 to 2.2 ticks and speed changes from 1174 to
  92 over two minutes; at speed 3, from 3.3 to 1.9 ticks and from 530 to 62; speed 1 is unchanged.
- If a guest's computer cannot sustain the chosen speed at all, no catch-up rule helps: compare the
  tick rate in the connection panel on both computers (about 11.7 per second at speed 7).

### Validation

- Release Steam build succeeds with no warnings. 59 RuntimeChecks pass against the built mod.
- 97 StabilityTests pass, ten of them new (`CatchUpSpeedChecks`): exact behaviour at each speed,
  never slower than the original rule, the paused case, the host case, and the hitching-guest
  model above. The rule is a pure function (`BeaverBuddies/CatchUpSpeed.cs`) linked into the tests.
- Two Python checks pass, and the non-Steam build also succeeds with no warnings.
- The fork owner played this build in multiplayer and reported that it works great.

## 1.0.1

Every player must install this build; it will not join a session with 1.0.0.

### Performance

- A guest's actions are sent to the host as soon as they are made, instead of at the next tick
  boundary. The host still decides which tick they run on (a guest never plays or hashes its own
  actions), so this only removes about half a tick of input delay, roughly 0.3 s at normal speed,
  and cannot change what any player simulates.
- The per-tick desync traces no longer carry stack traces over the network. A trace records its
  stack as an object and formats it only when a desync report is written, so detailed logging
  costs much less CPU and the trace payload is far smaller. Desync reports still include your own
  stacks; the other player's traces appear as messages only.
- Each tick, only characters that move are examined for animation state. Buildings are skipped
  after one component lookup instead of four.
- The per-frame animation update reads the simulation clock and the tick length as plain values
  instead of calling into Unity several times for every animated character, and looks up each
  character's tick bucket once instead of twice. The result is computed with the same arithmetic.
- The "Client trying to tick before receiving Heartbeat" warning is logged once per tick instead
  of on every check.

### Install folder

- The install folder inside the download is now `BeaverBuddies-Stability-Fork` (it was `BeaverBuddies-StabilityPreview`). If you
  installed an earlier download, delete the old folder before copying in the new one, because both
  share a mod ID and would conflict.

### Validation

- Release Steam and non-Steam builds succeed with no warnings. 87 StabilityTests, 59 RuntimeChecks
  (four new ones cover the trace payload and the desync report) and 2 Python checks pass.
- The fork owner played this build and reported that it works great.

## 1.0.0

The first official release of this fork. See `STEAM-INVITES.md`, `CONNECTION-PANEL.md` and `PLAYER-ACTIVITY.md`.

### Steam friend invites

- Replace the legacy `ISteamNetworking` P2P transport (deprecated by Valve) with
  `ISteamNetworkingSockets`, so Steam friends can be invited from Steam's overlay without
  Hamachi or port forwarding. It is offered alongside direct IP, not instead of it.
- All Steam calls run on the game thread; TimberNet talks to Steam through queues. Writes
  never block, and the connection completes in the background instead of inside the
  3-second wait in `TimberClient.Start()`.
- The host accepts only players who joined its friends-only Steam lobby. The lobby records
  whether the host is still accepting players, so an old invite explains itself. A friend
  whose game was closed joins through Steam's launch invite (`+connect_lobby`).
- Raise Steam's send rate and buffer limits so the save transfer is not throttled (the
  original capped it at 128 KB/s). Every connection failure, stall or timeout ends with an
  explanation that includes Steam's own end reason, in the error dialog and in `Player.log`.
- The transport keeps unread data between reads, validates read ranges and wakes blocked
  readers when a connection closes. A comment in the original's Steam read routine says it
  "will fail" if Steam merges several messages into one packet.
- A Steam failure can no longer prevent hosting over direct IP.
- TimberNet: transports can report why they failed and complete connecting in the
  background (`IFailureDescriber`, `IConnectionAwaitable`).
- Remove an unused upstream handler that still called the legacy Steam P2P API.

### Connection panel

- A small HUD panel during multiplayer showing connected players, each player's ping
  (green, yellow or red), whether you are in sync, the tick rate, game speed, how far a
  guest is behind the host, and whether players are connected directly or through Steam.
- It collapses to one line by clicking its title (remembered), and can be hidden from Mod
  Settings or with an optional key. Its corner is a setting.
- Ping is measured by the network layer (a probe once a second, answered on the guest's
  network thread) so it works the same over Hamachi, direct IP and Steam. Probes and the
  player roster use the separate presentation lane: never replayed, never hashed, never
  sent to a guest that is still joining, and validated on arrival.
- The panel docks into the game's own HUD layout, and only reads: it sends no gameplay
  event, and if it fails it disables itself.

### Player activity

- Show other players' translucent, colored cursors with names, remote selection
  outlines in each player's color, and **Viewing / Editing** labels on buildings.
- An in-game **Player cursors** dialog (Options menu) sets, per connected player, the
  cursor's color (their color, presets or exact RGB), size (50%-300%) and transparency
  (0%-90%). Choices are local, applied live, and remembered by player name in
  `BeaverBuddiesCursorStyles.json`.
- The **Player activity indicators** setting (on by default) turns sharing on and off.
- Activity uses its own lane on the existing connection, separate from the replay
  script and desync hash: host-assigned identities, latest-wins coalescing, no
  game-thread blocking, and nothing sent to a guest until its join has finished.

### Compatibility and connection safety

- Negotiate compatibility before requesting or loading the shared map. Compare the running
  game version, full mod version, and loaded BeaverBuddies/TimberNet module IDs. Replacing
  files without restarting cannot disguise an old process. Reject builds that lack the
  check and mismatched binaries. Bound the compatibility wait to 15 seconds, close failed
  connections, and report an update/restart message. The original only warned about a
  version mismatch after the save had loaded.
- Stop replay after a failed action, discard pending actions, pause the session, notify
  connected peers, and restore the replay flag even if error handling throws. Block
  further simulation and rehosting until the scene is reloaded; the affected player is told
  to reload a known-good save. A failure stop contains partial state; it does not roll back
  the action or recover unsaved progress, and peer notification is best-effort if the
  connection has already failed.
- Lock complete network frames so concurrent header and payload writes cannot mix.
- Close corrupted or failed connections, stop consuming events after failure, and deliver
  client error callbacks through the update thread.
- Close discarded client connections and prevent an old socket's cleanup from
  unregistering its replacement. Synchronize socket registry access.
- Synchronize client-list access during joins, broadcasts and shutdown.

### Desync fixes

- **Water and frame rate.** Replace the render-frame clock in
  `WaterDepthStrengthModifier.GetStrengthModifier` with Timberborn's configured simulation
  tick interval during multiplayer. This prevents different frame rates from producing
  different water-seep output. Inject `ITickService` into the existing water-source
  buffer. Preserve the game's depth thresholds, hysteresis, fade speed, disabled-state
  reset and maximum-strength clamp. Single-player keeps its original frame clock. Validate
  that the targeted method contains exactly one clock call to replace. The fork owner
  confirmed this resolved their reported badtide desync. A regression experiment
  reproduced different output at 30 and 144 FPS using the installed game's ramp
  instructions, then verified identical output after the production transpiler; its depth
  query and frame clock are test doubles, and it does not start Unity or install Harmony
  into a live game. Evaporation settings are unchanged. The source ramp now advances by
  simulation seconds rather than local frame duration, so its timing can differ from
  upstream.
- **Water-source ordering.** Apply water-source simulation snapshots in a consistent order
  by coordinates, strength and contamination. Preserve the live source registry and
  values. This corrects a demonstrated order-dependent case when multiple sources affect
  the same water column: the installed game's source-update task produced three results
  across six registration orders; canonical ordering produced one. This case was not
  established as the cause of the reported badtide desync.
- **Saving flag.** Restore the previous saving flag after exit saves, including
  exceptions, using a Harmony finalizer. Clear stale saving state when resetting between
  scenes. Restore the previous flag after deferred normal saves using `try/finally`. This
  addresses a defect consistent with immediate join-time desync reports, in which one
  peer omitted a moisture trace because its saving flag remained set.
- **Random numbers.** Preserve nested gameplay and non-gameplay RNG classification and
  restore it after exceptions in random-selection wrappers. Make all ten RNG
  classification patches exception-safe, counting nested calls instead of using simple set
  membership, and restore the ticker's prior RNG classification in a finalizer, including
  nested updates. The ordinary RNG check stays enabled regardless of detailed logging
  settings.
- **Equal-distance demolition jobs.** Choose exactly equal-distance jobs by persistent
  entity ID in multiplayer. Preserve nearer-job preference, eligibility, priority and
  reservation rules; single-player behavior is unchanged. Not confirmed in a live session,
  and the logs of the incident that prompted it did not prove an equal-distance tie
  caused it.
- **Stuck controls.** Recover input on desync notification and multiplayer scene load,
  including direct-IP Save and Rehost, using the game's built-in device reset. Recovery is
  deferred to Update and consumes cached held/down/release binding state. Repeated requests
  are coalesced; devices are not reset every frame and saved keybindings are not
  changed. The service is registered only in multiplayer scenes. The native device reset
  is mocked in regression tests. This is a targeted recovery measure, not a proven root
  cause, and the original report has not been confirmed fixed.
- **Entity IDs.** Apply regenerated entity IDs to the entity builder and fail explicitly if
  a unique ID cannot be found after the retry limit.

### Crash fixes

- **Animation.** Reset the animation path cursor before interpolation that can move
  backward between ticks. Fall back to the simulation position for non-finite visual
  coordinates before water and swimming listeners consume them. The fork owner reported
  that this fixed their crashing issue.
- **Demolition-selection replay.** `ClearResourcesMarkedEvent` looked up each entity in a
  replayed demolition selection with no null check, so if builders had already demolished
  some of the selected entities before the event arrived (it was stamped for tick 1771 and
  replayed at 1773), a `NullReferenceException` aborted the whole session. Missing
  entities are now skipped with a warning, the same way `BuildingsDeconstructedEvent` does,
  and an event with nothing left is skipped. The host re-stamps and forwards events at its
  own tick, so both sides skip the same entities and stay in sync. The regression check
  replays a stale selection against a real empty entity registry and fails on the old
  code. A selection where only some entities are missing needs live Unity objects, so it
  is untested, and the fix has not been confirmed in a live session. `DuplicationEvent` has
  the same kind of weak spot and is left unchanged.

### Performance

- Recycle expired water diagnostic arrays, preserving snapshot retention and captured
  values while removing steady-state map-sized allocations.
- Optimize ordered event insertion and bulk removal of consumed backlog.
- Parse each incoming transport frame once instead of twice.
- Gate routine event, packet and tick logging before formatting and serialization.
- Synthetic tests measured about 85 MB versus zero bytes allocated for 16 warmed-up
  captures, and 438.92 ms versus 0.28 ms for 4,000 ordered inserts. These are not FPS
  tests. Detailed logging, synchronous sends and throttling remain performance costs.

### Diagnostics

- Add separate hashes for active depth, contamination, overflow, geometry, inactive
  storage and source inputs without disabling existing checks.
- With detailed logging enabled, retain up to four water-map snapshots within a 64 MiB
  budget and write a local ZIP on desync. Reset capture between sessions and catch
  diagnostic failures. No automatic diagnostic upload exists.
- Add a Python tool to compare retained snapshots by tick, cell, field and exact
  floating-point bits.
- Include individual water-source coordinates and exact strength and contamination bits in
  detailed traces. Record selected demolition target IDs and distances there too, with up
  to eight eligible candidates in verbose local logs, without treating harmless
  candidate-order differences as synchronized trace mismatches.
- Add transport, animation and player-activity regression tests and a compiled-mod
  runtime test executable.

### Other

- Disable the original project's in-game changelog dialog, which appeared whenever the mod
  version changed.
- Report the mod version as exactly the release version, without a source-commit suffix.

## Installation

1. Fully close Timberborn on every computer.
2. Download `BeaverBuddies-Stability-Fork-1.0.3.zip` from the
   [latest release](https://github.com/timbermods/BeaverBuddies-Stability-Fork/releases/latest),
   extract it, and copy the `BeaverBuddies-Stability-Fork` folder into
   `Documents/Timberborn/Mods`. If you installed an earlier download, delete its old
   `BeaverBuddies-StabilityPreview` folder first: the two share a mod ID and would conflict.
3. Make sure **Harmony** and **Mod Settings** are enabled, then enable **BeaverBuddies -
   Stability Fork**, version **1.0.3**, on every computer. Disable the Workshop
   BeaverBuddies and any duplicate local copies: they share one mod ID.
4. Every player must use the same build. Test on a copied save first.

For a diagnostic session, enable **Always Use Detailed Logging** on both peers. It adds
overhead. If a desync occurs, keep both Player.log files and the newest water ZIP from each
peer under:

`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\BeaverBuddiesDiagnostics`

Large maps may retain fewer snapshots; peers running far apart may have no shared retained
ticks. Old ZIPs remain until removed. See `RuntimeChecks/compare_water_snapshots.py` for
comparison commands.

## Validation and limits

The 1.0.3 validation run passed **182 checks**: 116 in `StabilityTests` (network transport,
the Steam transport against a simulated Steam network, direct-versus-Steam protocol parity,
animation, player activity, ping measurement, the connection panel, the guest catch-up
rule and the mod list warning), 64 in
`RuntimeChecks` (the compiled mod running against the game's own assemblies) and two Python
archive-comparison checks. The mod builds against Timberborn 1.1.2.4 with no warnings.
Tests require .NET 8; game-dependent checks additionally require the user's installed game
assemblies and Harmony directory. No proprietary game assemblies, decompiled game code,
player logs or saves are included in this fork.

The Steam transport is tested against a fake Steam network that fails any Steam call made
off the game thread. Two protocol-parity checks run one scripted session (about 60 events
in each direction, one of 220 KB, plus cursor traffic) over a direct connection and over
Steam under stress, and require both peers to end with identical events and state hashes;
corrupting one byte in the Steam path makes them fail. The layer that calls the real Steam
client is not covered by the automated checks; it has been confirmed in real playtests.

The fork owner's two-player playtests confirmed: the animation crash fix, the badtide
desync fix, the player activity indicators, Steam invites and the connection panel, and
that the compatibility check, failed-action stop, input-delay and performance changes play well. They
confirm the reported issues and that this build plays well, not universal determinism.

Not confirmed in a live session: the demolition-selection crash fix, equal-distance
demolition tie-breaking and the stuck-controls recovery. Only two players have been tested.
Everyone in a session must run the identical build. Other mods are compared as a warning
only, and settings are not compared. Text added by this fork is English only. Other game versions and combinations of
mods may still have unrelated problems. No Housing Optimize changes are included. Upstream
authorship and GPL licensing are preserved in `License.txt` and the repository history.
