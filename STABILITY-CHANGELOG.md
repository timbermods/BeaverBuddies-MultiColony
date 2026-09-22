# Changelog

Every change this fork makes relative to the original BeaverBuddies `v1.1` branch at commit
`a13b1f20dacb6e30efa967cc8ac83e73779c0755` (24 August 2026), built against Timberborn
1.1.2.4. For a plain-language summary, see the [README](README.md). Future releases add a new
entry above the current one.

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
