# Separate colonies (beta)

Two (up to four) players on one map, each running their own colony: their own districts, land, beavers, stock,
working hours and, if the host chooses, their own science and unlocks. It is co-op, not a race: nobody wins, and the
colonies meet only at **trading posts**, where they barter.

**State of testing.** Beta. Seen in a game with the land-split alphas (alpha1 to 5): hosting and joining over
Steam, founding a second colony and building in it. **Nothing of this version's model has been seen in a game
yet**: ownership, land, per-colony marks and work, trading-post exchanges and their panel, separate science, the
road-network checks, and every build since alpha11 (the desync review's fixes onward) are covered by automated checks
only. Since alpha13 a guest whose colony state differs from the host's stops the tick it happens (see *Known limits*), so a bug
in that check would stop a healthy game too; the log line says which. Play on a copy of your save and keep backups.

## The rules in one minute

- **A colony is its districts and its land.** Every district center belongs to a player. Beavers belong to the district
  they live in; buildings to their district, or else to the colony that placed them; marks to the colony that made
  them. A colony's **land** is every tile within 10 tiles of its buildings and paths, first come: where two colonies
  both reach, the land is the one's that got there first, and stays theirs while they reach it.
- **Build, mark and plant on your land or free land.** Never on another colony's land or right next to its roads,
  buildings or paths, finished or still being built. Building towards another colony stops at the edge of its land.
- **Two colonies' roads never join, except through a Trading Post**, the building colonies barter through, and the
  only place colonies meet. (A District Crossing links a colony's own districts, as in the game.)
- **You change your own colony only**, whether or not the other player is playing.
- **Each colony's beavers work for it alone** (see *Keeping colonies apart*).
- **Shared by everyone:** game speed and pause, pings, chat, saving, and the map itself: water, droughts, badwater
  tides and weather.

## Starting

**Host settings** (Mod Settings → BeaverBuddies):

| Setting | Default | What it does |
|---|---|---|
| **Separate colonies for new games (beta)** | on | Read when a new game is made: players run their own colonies. Off: one shared colony, as in the Stability Fork (see *A shared-colony game*). A save keeps the mode it was started in. |
| **Allow founding colonies in a shared game (beta)** | off | Read when hosting starts: a player without a colony may found one in a shared save, which makes it a separate-colonies game for good (step 3 below). Off: a shared game stays shared. |
| **Separate science and unlocks per colony (beta)** | on | Each colony earns and spends its own science and unlocks its own buildings. Chosen when a separate-colonies game begins (or a shared one is split) and kept for the life of the save. |

The days before a hand-over (below) are read each day, so they can be changed during a game.

**Who plays which colony** is remembered by the save: each player is known by their Steam ID (or, without Steam, by
an id kept on their computer). The host checks that id for a Steam join but not for a direct IP join (see *How it
works*). The first time a player joins a save they take the next free colony; after that they
always get the same one, whoever hosts. With every colony taken (four), an extra player joins as a helper of the
host's colony. The connection panel shows each name with its colony number.

**A new game on a multi-start map** (BeaverBuddies maps with several starting locations) gives start N to player N:
the host's first colony is start 1, the next player's start 2, and so on.

**Joining closes** at the host's first tick, or at the first action that changes the game while it is still
paused (placing or marking something): a player joining after that would be sent the save the host started from,
without it. The host should wait for everyone, then unpause. So that this never happens by accident, the host's
first change while joining is open is held and the host is asked (**Start the game**, which plays it and closes
joining, or **Keep waiting**, which drops it), and a guest's change while the host waits is refused with the same
notice as a founding. The connection panel shows the host *Joining: open* until then.

**Any other separate-colonies game** (a new game on a standard map, or a separate-colonies save): the district
centers already there are the host's colony's. Every other player **founds** their colony once:

1. Once the host has unpaused, a message offers to place a district center. (If you cancel, **Ctrl+K** opens the
   same tool.) Not before: while the game is paused at the start, other players can still join, and a player who
   joined after the founding would load the save without it. The host can hand colonies over (Ctrl+T) from the first
   tick on, for the same reason.
2. Place it at least 20 tiles from other colonies' buildings and paths (their land shows as colored outlines), so
   both colonies have room to grow. It is free, needs no science, and appears **already built**, yours, with starting
   beavers, food and water (the new game's, or for a save that did not record them the host's Normal difficulty:
   the host writes them into the founding, so a mod changing the difficulty on one computer changes nothing).
3. A shared save (one colony: made with *Separate colonies for new games* off, or in the Stability Fork) stays
   shared unless the host ticks **Allow founding colonies in a shared game** before hosting. Then the first founding
   in it makes it a separate-colonies game for good: every building already there becomes the host's colony's, on
   every computer at that tick, and the new colony must stand 20 tiles from them like any other.

## A shared-colony game

With separate colonies off (a new game with *Separate colonies for new games* unticked, a shared save, or a save
from the Stability Fork) the game is the Stability Fork's shared co-op: one colony that every player builds
together. Nothing on the rest of this page applies. There is no land, no owners and no Trading Post (not even in
dev mode); District Crossings hold the game's 30 of a good; nothing of the colony model is kept, compared on the
heartbeat or checked once a day; and the save holds nothing of this mod's. What does apply is what MultiColony adds
to co-op in general (the README's *One shared colony*).

## Trading posts

The **Trading Post** is its own building, in the District Management group next to the District Crossing. It has the
District Crossing's model (each faction's own) and works like one, two linked halves each run by its own district's
workers, but it only ever trades between colonies. It **costs 10 logs and needs no science**, so colonies can trade
from the start, and it only shows in the toolbar of a separate-colonies game. Build it across the edge where your
land meets another colony's: one half on each side, each half reached by its own colony's road on its own land.
Either colony may place it, as long as part of it is on the placer's land (or free land); the placer's builders
build both halves. It trades once its two halves are in two different colonies' districts; until then its panel
says so, and nothing crosses it.

**Goods cross a Trading Post only through an exchange** agreed by the two colonies. Import and export settings (the
Distribution tab) never move anything across one. A Trading Post is **not a store**: its halves hold goods only for
the exchange under way (its panel has no *Imported goods* box, no **Manage distribution** and no stock list).

**The District Crossing is the game's own**, with its usual cost, science and import and export settings, for
linking a colony's own districts. It is an ordinary building for the colony rules: it may not stand on another
colony's land or next to its roads. If one ever ends up joining two colonies anyway (another colony's road reaching
its far half), it moves nothing between them, and its panel says to build a Trading Post.

**An exchange** is "this many of one item for that many of another, so many times", for example *100 logs for 25
gears, 4 rounds*. Each colony trades from **its own half**, the one its roads reach: the other colony's half only
says whose it is, with **Select your half**.

1. **Offer.** Select your half of the trading post. Its trading section has a **You give** and a **You get** card,
   each with an item and an amount from **0 to 100** (what a half holds of a good). Click the item for the game's
   goods grid beside the panel (science and beavers first, then the goods by the game's groups, each with the colony's
   stock; *Only what is in stock* is ticked at first). Choosing the other card's item swaps the two. **−** and **+**
   step the amount by 10 (Shift: 1; beavers by 1, Shift: 10). **Rounds** (1 to 99) repeats the exchange that many
   times, for more than 100; **Repeat until cancelled** makes it a standing deal. A line under the cards reads the
   offer back (*sarawr gets 100 Berries, and you get 1 Beaver. 3 rounds: 300 Berries for 3 Beavers in all.*), or says
   what is wrong with it; **Make offer** waits for a valid one. One side may be 0: asking for 0 is a gift, giving 0
   asks for help. For an exchange of more than one round, **Keep at least** sets a reserve of what you give (0 to
   9999): your side is brought (or paid) only while your colony would still have that much after the round, counting
   what already waits on your half; what waits stays, and the round goes on once you have more. Each side has its own
   reserve: the offering side's goes with the offer, and either side can change its own on the running exchange.
   The other player gets a notice.
2. **Answer.** The other colony's player selects their half and chooses **Accept** or **Decline**. The offering player
   may **Withdraw offer** until then. An offer changed in the meantime is never accepted by mistake. Declining or
   withdrawing leaves the offer's terms in the form of whoever did it, so a counter-offer is a changed number and
   **Make offer**.
3. **Each round.** Each colony's Trading Post workers fetch its goods from its storage and bring them to **its own
   half**, where they wait, held for the round (no other beaver takes them). Each side's bar shows how much waits on
   its half. Science and beavers are not carried: their bar shows how much the colony can give now.
4. **Crossing.** When both sides are in, the round crosses **all at once**: the goods pass to the other half and that
   colony's workers haul them away into their storage; science passes from pool to pool (only with separate science);
   adult beavers move to the other colony's district (never the last adult), each with a line in the other colony's
   notification journal (*Pip joined the colony from Colony 2 through a Trading Post.*). So nothing is ever given
   before what it was exchanged for is in. The round goes in both halves' **ledger**, and the next round starts.
5. **Done.** After the last round both players get a notice and the trading post is free for the next exchange.

**Ending an exchange early takes both colonies**, as agreeing to it did. **Cancel exchange** asks the other colony;
nothing crosses while it is asked. The other player chooses **Agree to cancel** or **Keep trading**, and the asking
player may take the request back (**Keep trading**). Once both agree, what waits on each half goes back into its own
colony's storage; rounds that crossed stay crossed. If the post stops joining the two colonies (a road removed), its
exchange pauses, and its colony may **End exchange** alone. When a colony is handed over, or a post ends up joining
other colonies than the two that agreed, its exchange ends by itself, and what waits on each half goes back home.

**Offering again.** Each half remembers the last exchange offered or accepted there, from its own side; a line above
the form (*Last exchange here: 100 Logs for 25 Gears. 4 rounds…*) has **Offer again**, and a click on a row of the
post's ledger puts that round's terms into the form (one round). Terms are only ever put into the form; nothing is
offered until **Make offer**.

**Ctrl+T**, the **Trade** button at the top right (a square button like the game's own there) or **All posts** on a
Trading Post opens the **trading posts and colonies** window, drawn as the game's own boxes are: each of your Trading
Posts with its exchange and round (or *not trading yet*) and a **Go to** button, and each colony with its population,
whether its player is playing (an absent player: *missed 6 of 7 days*, the host's limit), its **food and water** (the
top bar's icons, the stock, and the days it lasts at the rate the colony used yesterday, from the game's own daily
samples; red under a day), what it is **looking for**, and who looks after it. Its close button, Esc, Ctrl+T or the
Trade button close it. It does not pause the game.

**Looking for.** In that window, this player's own colony has a **Looking for** row: up to three items (goods,
science or beavers) chosen from the game's goods grid (the box opens beside the window, unticked so every good
shows), each chip a button to change it, **+** to add and **Clear** to drop them. It is saved colony state, set by an
action (a colony sets only its own). Other players see the wishes beside the colony in their own window, as
*Sarah is looking for: [icons]* under the header of a Trading Post they share with her, and in the goods grid when
they choose what to give her (the count in the game's yellow, with a word in the tooltip); choosing what to ask for
marks your own colony's wishes the same way.

**Good to know:**

- One exchange at a time per trading post. Build more Trading Posts for more exchanges at once.
- A half has room for **100 of each good**, used only by the round under way: its colony's goods waiting to cross,
  or the other colony's waiting to be hauled away. What has not been hauled away holds up the next round of that
  good: staff both halves, and keep storage room for what you receive.

**The trading-post panel** is built from the game's own panel pieces (the Workplace section's board, the
description's blue cards, the game's wooden and red buttons, input boxes, progress bars and check boxes, and the
warehouse's goods grid). On your half it shows who you trade with in their color, with **All posts**; the exchange
(the offer form, an offer waiting for an answer, or the round under way with each side's bar, what it waits for, and
ending it); what waits on the half (only when something does); the post's **ledger** (the last rounds that crossed,
with the cycle and day, what you gave and what the other player gave, by name); and what has passed each way between the two colonies (all
trading posts), as icons with amounts. When the game's sections above it leave too little room on the screen, its
content scrolls instead of running off the bottom.

Either of the two colonies trading through a Trading Post may remove it (deleting one half removes both, as in the
game); no other colony may. A District Crossing, or a Trading Post within one colony, is that colony's alone.
Running a half (workers, priority) stays with its colony. The two halves are placed as one: if either half may not
stand where it was put, neither is placed.

## Looking after a colony

A colony's player may ask another player in the session to look after it, so an evening away is not a week of
neglect and not a hand-over. It rests on the seat flip the host already had for testing alone: a player's actions
count as one colony at a time, and a steward switches which.

- **Asking.** In the Ctrl+T window, on your own colony: **Let … look after it**, one button per other player the
  session knows (by the stable id the slot table uses, so a helper without a colony can be a steward too); later,
  **Take it back**. The host may ask a player to look after the colony of a player who is away, and may end any
  stewardship (**End stewardship**); the steward may end it too. The grant is saved with the game and holds across
  sessions and hosts; a hand-over ends it.
- **Running it.** The steward finds **Run this colony** on the colony's row (and **Back to your colony** afterwards).
  While they run it, their actions are judged and stamped as that colony's, and their toolbar, science, top bar,
  working hours and every refusal follow, exactly as the owner's would; the connection panel shows them with that
  colony's number. Which colony a player acts as is session state (an action every computer plays, refused before
  the first tick like a founding), forgotten when the session ends. Both players may act on the colony at once.
- **Not handed over.** A colony looked after by a steward who is in the game is not handed over for its own player's
  absence (the days away still count, and show in the window). The host judges every grant, revocation and switch
  (ColonyStewardRules); a player who does not look after a colony cannot switch into it.

## When a colony is handed over

A colony whose player can't run it goes to another colony: its district centers, buildings, land, marks, stock and
science pool. The receiving colony may also build whatever the old one had unlocked; the old player keeps their
unlocks too.

- **No beavers or bots left** for a whole in-game day: to the nearest living colony (district center to district
  center). Every computer decides this the same way.
- **Its player away:** once they have missed the number of in-game days of hosted co-op play the host set (**Hand
  over a colony after its player is away**, 7 by default, 0 for never) in a row: to the nearest colony whose player
  is playing. Days the host plays alone in single player, and the first day after loading (players are still
  joining), don't count; a day the player is in the game starts the count again. A guest who leaves is away from
  the next day on. The host decides and every computer plays it. Not while the host tests alone with detailed
  logging on and nobody connected, where the host plays every colony; with a guest connected, logging changes
  nothing.
- **By the host**, from the Ctrl+T window, from the first tick on: any colony whose player is away (left, or not in
  this session), or that has no beavers. Useful for a player whose Steam account changed: they join, get a new slot,
  and the host hands their old colony to it.

The host tells every computer its limit with the day's presence, so the Ctrl+T window shows *missed 6 of 7 days*
everywhere, and the day the count reaches the limit every player in the game is warned that the colony is handed
over the next day unless its player is back (a colony looked after by a present steward gets no warning, as it is
not handed over). The player who lost their colony gets a notice and may **found a new one** (Ctrl+K). A trading
post between the two colonies then stands within one colony: its exchange ends (what waits on each half goes back
home), and it trades again only if another colony's roads reach its other half.

## Separate science and unlocks

With the setting on, when a separate-colonies game begins:

- **Each colony has its own science.** Inventors, the Numbercruncher and the observatory add to their own colony's
  science; a relic's reward goes to the colony of the beaver who demolished it (a relic destroyed fully demolished by
  a blast or a collapse pays the colony whose mark or land it stood on); the Iron Teeth control tower uses its own
  colony's. The top bar shows yours. Science that simulation code neither earns for nor spends from a named colony
  goes to the first colony on every computer, with a warning in the log.
- **Each colony unlocks its own buildings.** Unlocking costs your colony's science and unlocks the building on your
  toolbar only. Placing a building checks that your colony has it unlocked. A building renamed by a game update
  keeps its unlocks (the sets load through the game's own name mapper).
- A shared game being split: its science and unlocks so far stay with the first colony; new colonies start with
  none. A new game: every colony starts with the same unlocks.
- **Bot worker types** ("bots may work here") are unlocked per colony too, paid from the unlocking colony's science.

## Keeping colonies apart

The game hands out some work to any beaver who can walk there, and where two colonies' land meets their beavers can
walk to the same places. So in a separate-colonies game:

| Work | Whose |
|---|---|
| Building a construction site, demolishing | Only the colony that owns it (either partner may take down a trading post between them) |
| Picking up recovered goods, log piles and other stacks | Only where the colony may work (its land, or land no other colony holds) |
| Cutting trees | Only trees the colony marked itself |
| Planting (foresters, farmhouses) | Only on the colony's own planting marks |
| Harvesting, gathering, scavenging | What grows on the colony's own marks, or wild things on land no other colony holds |
| Working hours | Each colony's own (the working-hours buttons and the clock show yours; the bell rings at the game's own hours, and a handed-over colony's hours stay its own) |
| Chronometers set to working hours | Their own colony's hours |
| Bot worker types (separate science) | Each colony's own unlocks |
| Automation | A building, relay or memory cell may be wired only to its own colony's (copying settings from another colony's building, or placing a copy of it, is refused or placed plain: it would copy the links too) |
| Names | Only the owner renames |
| Migration | Only between a colony's own districts; beavers change colony only through a Trading Post, and a traded beaver must be able to walk to its new district and carry nothing |

Marks work per tile: a tile marked by one colony (for planting or cutting) can't be marked or unmarked by another.
Unmarking an area removes only your own marks in it.

**What still reaches across:** the map is one world. Water, droughts and badwater reach everyone, so a dam or a
badwater pump outside another colony's land can still change what flows onto it. Decorations and other buildings
with an area effect help any beaver standing in their area, whoever's it is. Game speed and pause are one clock for
everyone.

## What you see

In a co-op session each player's interface shows **their own colony only**: the top bar's goods, population,
housing, workplaces, wellbeing and science; the batch control window's lists (F1 to F10, opening on your biggest
district); alerts; the notification journal. A thing in no district goes by the colony it was last in, as the
journal does (below): another colony's beaver that died tragically is not in your alerts, and its death does not
make your alert row blink. Selecting another colony's building opens its panels but leaves your
figures alone. Still whole-map: the *Global* history graphs in F9/F10 and in a good's tooltip, which the game
records for the whole map.

The journal goes by the colony an entry's beaver is in now; a beaver that has died, or lives in no district (cut
off, or its district center deleted), goes by the colony it was last in. The save keeps that only for a beaver the
journal has an entry about: after a reload, the death of any other beaver still in no district is in nobody's
journal. A beaver moved through a Trading Post takes
its earlier entries to its new colony. The save keeps whose each entry is, so after a reload it is still your
colony's (it is listed again once you are seated). An entry saved by an earlier build whose beaver is gone is
hidden, since nothing says whose it was. The game's own journal, which the save holds, is not changed: alone, you
see every colony's entries.

## Road networks

The game refuses to place a road, building or tubeway that would join two districts' roads, so two colonies' roads
can only meet at a crossing (a Trading Post, between colonies). Two gaps are closed by this mod:

- **Zipline links** are judged once, by the host, with the game's own check.
- **Placing beside another colony's path or building** is refused whether it is finished or still being built (the
  game's own check knows only finished roads).
- **Two placements that are each fine alone** can still join roads once both are built (two players placing at the
  same moment). Every computer notices at the same tick and warns both players ("Two districts' roads have been
  joined without a District Crossing or Trading Post"). The game keeps running: the first district keeps the shared
  roads and the other goes without them until the joining path or building is removed, so do that soon.

## Water and the map

Water is one shared simulation: a dam upstream changes what flows to a colony downstream, and droughts and badwater
tides come to everyone at once. Stored water (tanks) is a good like any other and belongs to its colony. Prefer maps
with a water source near each start.

## Seeing the land

Every colony's land is outlined in its color while a building, planting, cutting, demolishing or founding tool is in
hand, and at any time with **Ctrl+L**. Where two outlines meet is where a trading post goes.

## Testing alone (debug)

With **Always Use Detailed Logging** on (the mod's debug mode) and nobody connected, the host can press
**Ctrl+Shift+K** to make their own actions count as the next colony's (colony 1 → 2 → 3 → 4 → 1). The toolbar,
science and every refusal follow. Every switch is logged. It does nothing while a guest is connected, and it ends
with detailed logging. While the host tests alone this way, every colony counts as present, so none is handed over
for absence.

**Dev mode** (Alt+Shift+Z) in co-op: while the host has dev mode on, two of its tools are played on every computer, for
the colony of the player using them:
- its instant unlock (Ctrl-click on a locked building or bot toggle), which costs no science;
- a construction site's *Finish now*.

Its other tools, such as deleting any object or the dev panel's other buttons, change only the computer they are used
on, and desync the game. A notice says so when dev mode is switched on in co-op.

Two of dev mode's keys are off in co-op, on every computer: holding Ctrl while placing no longer places the building
finished, and holding Ctrl while a building is removed no longer skips its recovered goods. The game read those keys
where the building was placed or removed, which in co-op happens on every computer, so a player only holding Ctrl
(for a shortcut, or the shared instant unlock) got a different building, or different goods, on their computer alone.

**Tick once** (the pause key pressed while paused) is off in co-op: it would advance only the computer it is
pressed on, and desync the game. A notice says so; unpause to play on.

## Known limits

- Up to four colonies; more players join as helpers of the host's colony.
- The game ends only when every beaver on the map is gone, not per colony.
- A colony's land is measured in a straight line on the map (10 tiles), not by walking; on a cliff edge it may
  reach a little further than its beavers do.
- Land is first come. Founding keeps 20 tiles from other colonies, but a colony that grows quickly towards another
  still claims the land between them first.
- Demolition marks on ruins and relics on land no colony holds may be set, cleared and worked by any colony.
- A building nobody placed as an action (built before the game was hosted, or in a save older than the two-colony
  builds) takes its colony on its own, checked at the first tick and every 16 ticks after: its district's, else the owner of the road at its
  entrance (a path: the road it is), else the colony whose land it stands on. One on nobody's land, or across two
  colonies' land, waits until one of these applies.
- An exchange gives at most 100 of an item a round (the room a half has); more takes rounds. A round waits while the
  receiving colony has not hauled away the last one's goods (no storage room, or no workers on its half).
- Trading happens in a co-op session: host the game (even alone) to trade.
- The *Global* history graphs (F9, F10, good tooltips) cover the whole map.
- Separate science is meant for co-op: in single player, science earned by another start's buildings goes to that
  colony's pool, which only its player can spend.
- A guest's planting tools follow the first colony's unlocks (the game builds that list before the guest is seated).
- Dev mode's tools, apart from its instant unlock and *Finish now*, are not shared: using them desyncs a co-op game.
  (Its "place finished" and "don't recover goods" keys, both Ctrl, are off in co-op.)
- While a co-op game is paused, what was just built or removed updates its district (the district badge and highlight,
  and which district's builders a new construction site waits for) when the game resumes, at the same moment on every
  computer. Gates open and close, and automation reacts to a changed input, at the tick rather than the frame, for
  the same reason: at most a tick later than in single player.
- Placing is judged by the placing player's own tool: the check against joining two districts' roads is not repeated
  in the multiplayer replay, where it read state that differs between computers. Whether the spot is still free when
  the placement is played is checked by the host alone (a district center by its blocks only); guests take its
  answer. Two district centers placed on the same tiles in one tick: the second is skipped everywhere.
- A District Crossing's panel, open on one computer, no longer changes what that computer's crossing workers export:
  the snapshot the workers read is taken in the tick only.
- In a separate-colonies game, every tick the host's heartbeat carries a running digest of every colony state change (owners, marks, science,
  exchanges, the ledger, land, presence, hand-overs, working hours), and each guest compares it with its own at the
  same point: a difference stops that guest with the desync dialog that tick, with both digests and the number of
  changes each side counted in the log. As any player desyncs, every computer also logs its colony changes since the
  last count the colony checks agreed on (`Colony changes here as …`, each with its number and the digest it left):
  a guest notes that count at every heartbeat whose digest matches, and sends it with the desync. Line two players'
  lists up by change number (`#n`): both start at the same number, and the first number whose line differs is the
  change their computers did not make alike; a line marks where the host's check that differed came. The last 16384
  changes are kept, so a tick with a large mark (one change per tile) still fits; if more than that were counted
  since, the list says which changes it no longer has. Once a day the host also sends its full colony
  check with the day's presence, and a guest that differs stops too. Colony code draws no random numbers, so without
  these a difference showed only once it changed a beaver's random draw, or never.
- Two colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest from the
  connection panel.
- The days of food and water in the Ctrl+T window are an estimate from yesterday's use (today's, scaled, before a
  full day has been sampled): a colony that just doubled its beavers eats faster than the number says. Display only.
- A reserve counts a colony's stock in the district of its Trading Post half, as the game counts it (the goods on
  the half included), not the whole colony's.
- A steward's own colony is "present" while they play; the colony they look after counts as present only for the
  hand-over rule, not for its own player's days away.

## How it works

- **The host decides.** Every action goes through the host. The host writes which connection it came from (a guest
  cannot claim another), seats players by their stable id, writes the actor's colony into the action, checks it
  against the rules just before playing it, and plays and forwards it, keeps only the actor's part of a list action,
  or drops it (logged as `[Colony] Refused …`). Guests never judge, so the computers cannot disagree. What the host
  decides while playing an action travels in the action too: whether a building could still be placed, what a
  founded colony starts with, the day's presence and colony check. A Trading Post's two halves are judged together.
- **Seating checks the connection where it can.** A guest's hello says its stable id. Over Steam the host holds it to
  the Steam ID Steam proved for the connection (`TimberServer.VerifiedIdOf`): a hello saying another Steam ID is
  refused, and a guest whose own Steam ID could not be read (it says a local id) is seated by the proved one. A
  direct (IP) connection proves nothing, so its hello is taken at its word; a guest there who says another player's
  id takes that player's colony, and one who rejoins under new ids at tick 0 takes the free slots. The host always
  listens for direct connections too, Steam invites or not. Over either, a connection already seated can't say hello
  again as someone else, an id the slot table can't hold as it is (a line break or `|`, over 64 characters) is
  refused, and so is a hello the host could not stamp with a guest's number (a connection it no longer knows).
  (`ColonySlotTable.SeatHello` and `CheckHello`; a refused hello is logged as `[Colony] Refused PlayerHelloEvent …`.)
- **Joining** closes at the first tick, or at the first action played while the host still waits paused, since a
  later joiner is sent the save the host started from. The join check covers the mod's own files (`Buildings`,
  `TemplateCollections`) as well as the game and mod versions.
- **Every colony state change** (owners, marks, science and unlocks, exchanges and their ledger, land, traded
  beavers, presence, hand-overs, working hours) made inside a tick or a replayed action folds into a running digest.
  The host sends it with every heartbeat; a guest whose own differs stops that tick. The last 16384 changes are
  kept, and every computer logs those after the last count the desynced guest agreed on
  (`ClientDesyncedEvent.colonyChangesAgreed`, `ColonyDigest.DescribeSince`); nothing kept is hashed or sent. A shared
  game has none.
- **Ownership is saved**: on the district centers, on every building (the colony that placed it, from its first
  moment as a construction site) and on map marks. Land is worked out from the buildings standing, the same on every
  computer. Beavers choose their work from these only, so every computer's beavers choose alike. A shared game
  saves none of it: its save is the Stability Fork's.
- **Science** keeps one pool and one unlock set per colony in the save. Simulation code that earns, spends or reads
  science names the colony of the building doing it; everything else is display.
- **Exchanges** are saved on the two halves of the Trading Post, with each round's goods waiting on their half (held
  there, and held again when a save is loaded). A round crosses in the simulation, at the same tick on every computer,
  once both sides are in; the post's ledger, each side's reserve and the last terms offered there, and the totals
  traded each way, are kept there too.
- **Stewards and wishes** are saved singletons (a steward's stable id per colony; up to three items per colony), set
  by actions the host judges, and part of the running digest and the daily check. Which colony a steward acts as
  right now, and who is in the game today (by stable id, with the host's hand-over limit), travel in actions and are
  not saved.

## Testing

Automated checks (all passing): **StabilityTests** (headless: the network stamping, player slots, the ownership
rules, founding and the wait for the first tick, migration pairing, the blueprint join check, the colony digest) and
**RuntimeChecks** against the compiled mod and the game's assemblies: every action type declares what it touches,
the shared actions and the actions that leave joining open are listed for review, the host's answers survive the
event JSON, no simulation-reachable game method reads a dev key the mod does not neutralize, and every game method
or field the mod hooks still exists. In-game [test scripts](ALPHA-TEST-SCRIPTS.md).
