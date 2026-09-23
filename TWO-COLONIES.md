# Separate colonies (beta)

Two (up to four) players on one map, each running their own colony: their own districts, beavers, stock, working
hours and, if the host chooses, their own science and unlocks. It is co-op, not a race: nobody wins, and the
colonies meet only at **trading posts**, where they barter.

**State of testing.** Beta. Seen in a game with the land-split alphas (alpha1 to 5): hosting and joining over
Steam, founding a second colony and building in it; and in three short sessions with separate science (beta12,
beta14 and beta15): a founding and both players building, in step at every tick (beta12's one desync, dev mode's
science, was fixed in beta14), and in beta15 trading posts with exchanges of goods and beavers. **The rest of this
version's model has not been seen in a game yet**: colony handover, the road rule that replaced land in beta15, the
waiting room (beta18 for new games, beta19 for saves), and most of the desync review's fixes (alpha11 onward) are
covered by automated checks only. Since alpha13 a guest whose colony state differs from the host's stops the tick it happens (see *Known limits*), so a bug
in that check would stop a healthy game too; the log line says which. The waiting room first opened in 1.4.0-beta23
(until then **Next** crashed the game; until beta21 no guest could have got from it into the game): its pages have
been seen, a game started from it has not yet. **The late game** (automation and the HTTP API, water automation, power,
dynamite and tunnels, both Wonders, bots), **Folktails and Iron Teeth together**, and **Trading Posts at scale** have
not been played either: the 1.4.0-rc1 review read each of them against the game's own code, fixed what it found and
added checks (`design/REVIEW-FINDINGS-1.4.0-beta24.md`), and a two-player late-game playtest (ALPHA-TEST-SCRIPTS,
Scripts L, M and T) is what comes next. Play on a copy of your save and keep backups.

## The rules in one minute

- **A colony is its districts.** Every district center belongs to a player. Beavers belong to the district they live
  in; buildings to their district, or else to the colony that placed them; marks to the colony that made them. There
  is no land and no border.
- **Build, mark and plant anywhere**, right up to another colony's buildings and roads. Where to build is the
  players' call.
- **Two colonies' roads never join, except through a Trading Post**, the building colonies barter through, and the
  only place their roads meet. A path may not touch another colony's road, and a building's door may not open onto
  one (see *Road networks*). (A District Crossing links a colony's own districts, as in the game.)
- **You change your own colony only**, whether or not the other player is playing.
- **Each colony's beavers work for it alone** (see *Keeping colonies apart*).
- **Shared by everyone:** game speed and pause, pings, chat, saving, and the map itself: water, droughts, badwater
  tides and weather.

## Starting

**A new game's colonies** are chosen on the New Game difficulty page (since 1.4.0-rc3), under the game's own
**Tutorial** checkbox. The page remembers the last choice; a game keeps what it was made with, for good.

| Checkbox | Default | What it does |
|---|---|---|
| **Separate colonies** | ticked | Each player builds their own colony. Unticked: everyone plays one shared colony, as in the Stability Fork (see *A shared-colony game*), which a player other than the host may split once, from the game menu (step 3 below). |
| **Separate science and unlocks** (under it) | ticked | Each colony earns and spends its own science and unlocks its own buildings. Unticked: the colonies share one pool. |
| **Mixed factions** (under it) | unticked | Each colony plays Folktails or Iron Teeth, its player's pick (see *Mixed factions*). Greyed, with the reason in its tooltip, unless every faction is unlocked on the host's computer and the game has only its two factions. |

The waiting room says which kind of game it is, under its plate. The host's one colony setting in Mod Settings is
**Hand over a colony after its player is away (days)** (below); it is read each day, so it can be changed during a game.

**Who plays which colony** is remembered by the save: each player is known by their Steam ID (or, without Steam, by
an id kept on their computer). The host checks that id for a Steam join but not for a direct IP join (see *How it
works*). The first time a player joins a save they take the next free colony; after that they
always get the same one, whoever hosts. With every colony taken (four), an extra player joins as a helper of the
host's colony. The connection panel shows each name with its colony number.

**A new game on a multi-start map** (BeaverBuddies maps with several starting locations) gives start N to player N:
the host's first colony is start 1, the next player's start 2, and so on.

**A new game with a waiting room** (1.4.0-beta18). On the New Game panel's difficulty page the host chooses **Host
co-op game** (beside Start) and names the settlement in the game's own box. The **Co-op Game** page, a page of the
game's New Game wizard, lists everyone in the room: the host (colony 1), then each guest in the order they came in
(colonies 2 to 4, then helpers of colony 1; no colonies in a shared game), each **Ready** or **Not ready**
(**Joining…** until the guest has said who it is). Guests join from the main menu, by Steam invite or **Join co-op
game**, and press **Ready**; the host may remove a guest, and **Start Game** asks first if someone is not ready
or nobody came. At Start:

- nobody new can join, for good (a **Save and Rehost** lets someone in later, as after any first tick);
- the host's computer makes the world as a single-player game behind the loading screen, fills the colony slot
  table in the room's order, saves it at tick 0 (a save named `<date> Co-op start` in the named settlement), sends
  those bytes to every guest in the room and loads the same bytes itself as the hosted game;
- on a multi-start map only as many starts are filled as there are players (host and guests, at most the Players
  field and four), each to its player in room order; anyone beyond founds a colony;
- the game opens paused at tick 0 with every guest loading; the connection panel marks a guest *(loading)* until
  its game has loaded. There is no *Joining: open* and no *Start the game?*: joining closed at Start. **Founding**,
  switching colonies and asking a steward work at once, while paused; the host's **Hand to …** buttons still wait
  for the first tick (a guest still loading looks away).

**A save in the waiting room.** Every save is hosted through the same page (the only way since 1.4.0-rc4):
- **From the main menu**, **Host co-op game** (under Load game) opens the game's own save browser as the **Host co-op
  game** box: its title, its settlements and saves, and **Host co-op game** in place of Load (Enter and a double-click
  host too). Under the selected save's picture a gold line says what it is: *Separate colonies: 2 players* (the
  players its slot table remembers), *… only yours so far*, or *One shared colony*. **Host co-op game** opens the page
  for the save (its settlement, name and in-game date), after the game's own checks of the save.
- **From inside a game**, the game menu's **Host co-op game** (playing alone) saves the game as a new save
  (`<date> Co-op`) and opens its page in the main menu. A co-op host's same button is **Save and Rehost** (`<date>
  Rehost`, also on the desync dialog): everyone leaves the game, and the others' **Rejoin** (on the lost-connection
  message) or **Reconnect (wait for Rehost)** takes them to their main menu, where they join the page as soon as it
  opens (Cancel stops waiting). A direct-IP guest asks the host's address every few seconds, off the menu's thread,
  and joins once something listens there; a Steam guest enters the host's lobby only once its data says it is an open
  Co-op Game page of this build (until the host rehosts, Steam shows the lobby of the game that ended), or accepts the
  host's invite. The wait says nothing while nobody is there or the old game refuses newcomers, and stops with the
  reason for another build of the mod or a full room (1.4.0-rc5). **Load game** only loads.
- **The game menu's button** is decided by how the game was loaded (`HostButtonRules`), not by whether its session
  still runs: **Host co-op game** in a game played alone, **Save and Rehost** for the host (also after a desync or a
  lost guest), and nothing for a guest, also once its connection is lost (its copy may be out of step) or after a
  failed action. A game's menu has no **Join co-op game**: a page is joined from the main menu (1.4.0-rc5).

At **Start Game** the save's bytes go to everyone and the host loads the same bytes; joining closes at Start, so
founding, switching colonies and asking a steward work at once, as after a new game's waiting room. A save seats each
player by who it remembers (the slot table in the save); the page reads that from the save and shows each row's
colony and its faction (a brand-new player's row shows the colony they would most likely get).

**A shared save made separate at Start** (1.4.0-rc4). For a save that is not separate colonies (a single-player game,
a shared co-op save, a Stability Fork save), the host's page shows the New Game page's **Separate colonies** checkbox,
unticked, and under it **Separate science and unlocks**, also unticked (since 1.4.0-rc5: the players of a shared save
earned its science together, and a guest's split keeps one pool too). Ticked, the guests' pages say so (*Separate
colonies from Start: everything built so far becomes the host's colony…*), and at Start the game becomes a
separate-colonies game, for good: once the host's game has loaded, its first action says so, and every computer plays
it at the same point, as a split (every building and mark already there becomes the host's colony's). Until the host
has played it, a guest's change (a guest whose game loaded first) is refused with *Not yet: the game is still
starting*, so nothing a guest builds becomes the host's (1.4.0-rc5). The host is told; each guest is offered to found
their colony. With separate science, each colony earns its own from then on, what was earned so far stays with the
host's colony and the guests' colonies start with none; unticked, the colonies share one pool. Only the host can send
it, and it does nothing in a game already separate.

**Any other separate-colonies game** (a new game on a standard map, or a separate-colonies save): the district
centers already there are the host's colony's. Every other player **founds** their colony once:

1. As soon as you are in, a message offers to place a district center, even while the game is still paused (if you
   cancel, **Ctrl+K** opens the same tool): every game starts from a waiting room, where joining closed at Start. The
   host's **Hand to …** buttons wait for the first tick (a guest still loading looks away).
2. Place it anywhere its roads won't join another colony's (other colonies' roads show in their colors). It is
   free, needs no science, and appears **already built**, yours, with starting
   beavers, food and water: the new game's difficulty, which every new game keeps, a shared one too (since 1.4.0-rc5,
   so a split or a converted save founds on it), or, for a save that recorded none (a Stability Fork save), the host's
   default difficulty. The host writes them into the founding, so a mod changing the difficulty on one computer
   changes nothing.
3. A shared save (one colony: made with *Separate colonies* unticked, or in the Stability Fork) stays shared. A
   player other than the host may split it once (1.4.0-rc3): in the game menu (Esc), **Found your own colony**, below
   Settings, shown only to them and only in a shared game. It asks first, since it can't be undone; then they place
   their district center as above, and only that founding makes the split (leaving the tool changes nothing). The
   game is then a separate-colonies game for good: every building already there, and every planting and cutting
   mark, becomes the host's colony's, on every computer at that tick (the marks since 1.4.0-rc1: before, the new
   colony's workers could take the first colony's fields). Science and unlocks stay one pool, shared by every colony,
   as they were. Every player is told. Other players who played the shared colony have no colony then: they found
   their own (Ctrl+K), or the host asks them to look after the first colony (Ctrl+T). The host can't split a shared
   game: they play its colony.

## Mixed factions (beta)

*1.4.0-beta20. Not played yet.* With **Mixed factions** ticked under **Separate colonies** on the New Game page, every
colony plays a faction of its own:
one player can run Folktails and another Iron Teeth on the same map, trading through Trading Posts. It is made for the
game's two factions: with a mod that adds a faction installed, a new game stays one faction and the page says why
(1.4.0-beta21; a third faction's content would load into every mixed game).

**Picking your faction.** It is part of the waiting room (the host's **Host co-op game**):

- The **Co-op Game** page shows the faction page's own switcher, its logo ring, arrows and name plate, under the
  settlement's name. Each player, the host included, picks with the arrows before Start. Each player's row shows
  their faction's logo (hover it for the name), and the plate reads *map - difficulty*, since there is no one
  faction.
- On a **multi-start map** each start is placed in its player's faction: their district center, their beavers.
- On a **standard map** the host's colony is the host's pick. Every other player's founding places their picked
  faction's district center (*Place your Iron Teeth colony's district center*); **Another faction** changes it.
- A player who did not pick (a game made alone and hosted later, or someone who joined a save) chooses in the
  founding box: one card per faction, with the game's own logo, name and description.
- A **mixed save** hosted from the main menu shows each player's colony and its faction in the room. Only a player
  whose colony has no faction yet (they will found it) picks one there.

**Which factions.** The host's unlocks count: Iron Teeth is there if it is unlocked on the hosting computer, whoever
picks it. A new game becomes mixed only with every faction unlocked on the host's computer (otherwise the room says
so and everyone plays the host's faction). When another player hosts the save later, their unlocks count from then on.

**Changing your mind.** A colony that has built nothing of its own faction yet (only its district center, and common
things like paths) can still switch: **Play Iron Teeth instead** on your colony in the Ctrl+T window. On a
multi-start map a player whose start is not the faction they picked is asked once. The district center and beavers
become the other faction's, in place (the same count, up to the starting numbers), and the stock stays. Not while
the colony has an offer or exchange open at a Trading Post: end it first.

**Each colony its own faction:**

- **Toolbar**: your faction's buildings and crops, and the common ones (paths, stairs, dev buildings). A steward
  sees the faction of the colony they run. A colony can't place the other faction's buildings (the host refuses them).
- **Beavers and bots** look like their faction and have its needs: an Iron Teeth bot needs Energy, a Folktails bot
  Biofuel. Any food stills a beaver's hunger, but only its own faction's foods give their wellbeing bonus (Berries
  and water are everyone's); since goods only cross where the other faction stores them, that shows only after a
  handover of the other faction's food buildings. Babies are born the faction
  of the lodge or breeding pod they come from; a child grows up the faction it was.
- **Buildings, paths and power shafts** look like their own faction. A path takes its colony's.
- **Warehouses, piles and tanks** hold their own faction's goods and the common ones. Farmhouses and foresters plant
  their faction's crops and trees and the common ones. Gatherers take what their faction uses (lumberjacks still cut
  every tree: logs are everyone's).
- **What you see** follows your colony's faction: the faction icon at the top left, the wellbeing box, the goods
  lists (distribution, statistics, a resource counter's choice), the game-over and Wonder screens. Tutorials are off
  in a mixed game (they are written for Folktails).

**Trading between factions.** A good goes to a colony only if its faction can store it: between Folktails and Iron
Teeth that is the 17 goods both use (Log, Plank, Treated Plank, Gear, Metal Block, Scrap Metal, Dirt, Pine Resin,
Explosives, Fireworks, Berries, Water, Badwater, Extract and the three bot parts), plus science. **Beavers never cross
between factions.** The goods grid lists only what may cross that way. The form says why when an offer from before
can't, a Trading Post between factions says what may cross under its header, and each colony's wishes list only
what it may receive. Two colonies of one faction trade as before.

**Handovers.** A colony handed over goes to the nearest living colony *of its faction* first, then the nearest; a
colony whose player is away goes **only** to one of its faction, and with none it waits (1.4.0-rc2). So in a
two-player mixed game only a colony with nobody left, or the host's choice in Ctrl+T (whose button says what the
receiver can't do), crosses factions; to keep an away friend's colony going, look after it. The
receiver keeps its own faction for its toolbar. It can still run the buildings and beavers it received, and a beaver
crosses a Trading Post only into a colony of its own faction. What it received keeps its faction: its bots need
Biofuel (Folktails) or Energy (Iron Teeth), and its beavers their own foods, which only the buildings they came with
make, since the receiver builds only its own faction's. A Folktails bot can fly an Iron Teeth Earth Repopulator's plane
(it flies without the piloting pose; until 1.4.0-rc1 this stopped the session for everyone).

**Wonders.** Each colony builds its own faction's: the Folktails Earth Recultivator, the Iron Teeth Earth Repopulator.
A Wonder's effect helps the beavers of its own faction in its range, whichever colony they belong to; the other
faction's beavers get nothing from it (the need is their faction's alone). The first Wonder of any colony to finish
completes the map for everyone, and each player's profile records the map for their own colony's faction.

## A shared-colony game

With separate colonies off (a new game with *Separate colonies* unticked, a shared save, or a save from the
Stability Fork) the game is the Stability Fork's shared co-op: one colony that every player builds together, until a
player other than the host splits it from the game menu (*Starting*, step 3). Nothing on the rest of this page
applies until then. There are no owners and no Trading Post (not even in dev
mode); District Crossings hold the game's 30 of a good; nothing of the colony model is kept, compared on the
heartbeat or checked once a day; and the save holds nothing of this mod's. What does apply is what MultiColony adds
to co-op in general (the README's *One shared colony*).

## Trading posts

The **Trading Post** is its own building, in the District Management group next to the District Crossing. It has the
District Crossing's model (each faction's own) and works like one, two linked halves each run by its own district's
workers, but it only ever trades between colonies. It **costs 10 logs and needs no science**, so colonies can trade
from the start, and it only shows in the toolbar of a separate-colonies game. Build it **between two colonies'
roads**, one colony's road at each half's door (the post is 3 cells wide and 2 deep, so the two road ends are in
line, 3 cells apart). Either colony may place it, anywhere, before the roads are there or after; the placer's
builders build both halves. It trades once its two halves are in two different colonies' districts, each reached by
a different colony's road; until then its panel says so, and nothing crosses it.

**Goods cross a Trading Post only through an exchange** agreed by the two colonies. Import and export settings (the
Distribution tab) never move anything across one. A Trading Post is **not a store**: its halves hold goods only for
the exchange under way (its panel has no *Imported goods* box, no **Manage distribution** and no stock list).

**The District Crossing is the game's own**, with its usual cost, science and import and export settings, for
linking a colony's own districts. It is an ordinary building for the colony rules: neither half's door may open onto
another colony's road, so it can't be built between two colonies. If one ever ends up joining two colonies anyway (another colony's road reaching
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
   its half. Science and beavers are not carried: their bar shows how much the colony can give now. Goods of the
   exchange's item already waiting on your half (sent back by an exchange that ended, or received earlier) count
   toward the round. When a round waits, the line under the bars says why: your half or theirs is paused or flooded,
   has no workers, has no room because last round's goods still wait on the other half to be hauled away (the post's
   room for a good is shared by its halves), or the colony has none left to bring. The Ctrl+T list says the same
   for each post, and shows an exchange whose post lost a road as paused.
4. **Crossing.** When both sides are in, the round crosses **all at once**: the goods pass to the other half and that
   colony's workers haul them away into their storage; science passes from pool to pool (only with separate science);
   adult beavers move to the other colony's district: only those free to go (carrying nothing, and able to walk
   there), never the last adult, and a round of beavers waits until enough are free. Each has a line in the other colony's
   notification journal (*Pip joined the colony from Colony 2 through a Trading Post.*). So nothing is ever given
   before what it was exchanged for is in. The round goes in both halves' **ledger**, and the next round starts.
5. **Done.** After the last round both players get a notice and the trading post is free for the next exchange.

**Ending an exchange early takes both colonies**, as agreeing to it did. **Cancel exchange** asks the other colony;
nothing crosses while it is asked. The other player chooses **Agree to cancel** or **Keep trading**, and the asking
player may take the request back (**Keep trading**). Once both agree, what waits on each half goes back into its own
colony's storage; rounds that crossed stay crossed. If the post stops joining the two colonies (a road removed), its
exchange pauses, and its colony may **End exchange** alone. When a colony is handed over, or a post ends up joining
other colonies than the two that agreed, its exchange ends by itself, and what waits on each half goes back home. A
Trading Post removed with an exchange open (by a player, a blast or the ground taken from under it) ends it: both
players are told, and what waited on its halves is left there as goods any colony's workers may pick up.

**Offering again.** Each half remembers the last exchange offered or accepted there, from its own side; a line above
the form (*Last exchange here: 100 Logs for 25 Gears. 4 rounds…*) has **Offer again**, and a click on a row of the
post's ledger puts that round's terms into the form (one round). Terms are only ever put into the form; nothing is
offered until **Make offer**.

**Ctrl+T**, the **Trade** button at the top right (a square button like the game's own there) or **All Posts** on a
Trading Post opens the **trading posts and colonies** window (drag it by its title or frame to move it; it opens
where it was left), drawn as the game's own boxes are: each of your Trading
Posts with its exchange and round (or *not trading yet*) and a **Go to** button, and each colony with its population,
whether its player is playing (an absent player: *missed 6 of 7 days*, when the host has set a limit), its **food and water** (the
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
warehouse's goods grid). On your half it shows who you trade with in their color, with **All Posts**; the exchange
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
  the first tick like a founding, except after a waiting room), forgotten when the session ends. Both players may act on the colony at once.
- **Not handed over.** A colony looked after by a steward who is in the game is not handed over for its own player's
  absence (the days away still count, and show in the window). The host judges every grant, revocation and switch
  (ColonyStewardRules); a player who does not look after a colony cannot switch into it.

## When a colony is handed over

A colony whose player can't run it goes to another colony: its district centers, buildings, marks, stock and
science pool. The receiving colony may also build whatever the old one had unlocked; the old player keeps their
unlocks too.

- **No beavers or bots left** for a whole in-game day: to the nearest living colony (district center to district
  center). Every computer decides this the same way.
- **Its player away**, only if the host has set a limit (**Hand over a colony after its player is away**: 0, never,
  by default since 1.4.0-rc2; a friend stepping away asks another player to look after their colony instead, see
  *Looking after a colony*): once they have missed that many in-game days of hosted co-op play in a row, to the
  nearest colony whose player is playing. **In a mixed-factions game only to a colony of its own faction**
  (1.4.0-rc2): with none in the game, the colony waits, and nobody is warned. A colony of the other faction could run
  what it received but not build for, fuel or feed it. Days the host plays alone in single player, and the first day after loading (players are still
  joining), don't count; a day the player is in the game starts the count again. A guest who leaves is away from
  the next day on. The host decides and every computer plays it. Not while the host tests alone with detailed
  logging on and nobody connected, where the host plays every colony; with a guest connected, logging changes
  nothing.
- **By the host**, from the Ctrl+T window, from the first tick on: any colony whose player is away (left, or not in
  this session), or that has no beavers. Useful for a player whose Steam account changed: they join, get a new slot,
  and the host hands their old colony to it.

The host tells every computer its limit with the day's presence, so the Ctrl+T window shows *missed 6 of 7 days*
everywhere (in a mixed game with no colony of its faction being played: *not handed over: no colony of its faction is
being played*). A guest's window follows that presence, as the warnings do: a guest's own list keeps a player who left
(1.4.0-rc5). A hand-over for absence is always announced the day before, to every player in the game: the day the
count reaches the limit (in a mixed game, the first such day a colony of its faction is being played), or, for a colony
a steward looked after past it, the first day nobody keeps it (and, after a load, the first day counted). It happens the next day unless its player or its steward is back (until 1.4.0-rc1 a
steward's colony could go unwarned on the first day nobody kept it). The player who lost their colony gets a notice and may **found a new one** (Ctrl+K). A trading
post between the two colonies then stands within one colony: its exchange ends (what waits on each half goes back
home), and it trades again only if another colony's roads reach its other half.

## Separate science and unlocks

With **Separate science and unlocks** ticked when a separate-colonies game is made (a shared game split later keeps one
pool):

- **Each colony has its own science.** Inventors, the Numbercruncher and the observatory add to their own colony's
  science; a relic's reward goes to the colony of the beaver who demolished it (a relic destroyed fully demolished by
  a blast or a collapse pays the colony whose mark it stood on, else the first colony); the Iron Teeth control tower uses its own
  colony's. The top bar shows yours. Science that simulation code neither earns for nor spends from a named colony
  goes to the first colony on every computer, with a warning in the log.
- **Each colony unlocks its own buildings.** Unlocking costs your colony's science and unlocks the building on your
  toolbar only. Placing a building checks that your colony has it unlocked. A building renamed by a game update
  keeps its unlocks (the sets load through the game's own name mapper).
- A shared game being split: its science and unlocks so far stay with the first colony; new colonies start with
  none. A new game: every colony starts with the same unlocks.
- **Bot worker types** ("bots may work here") are unlocked per colony too, paid from the unlocking colony's science.

## Keeping colonies apart

The game hands out some work to any beaver who can walk there, and two colonies' beavers can walk to the same
places. So in a separate-colonies game:

| Work | Whose |
|---|---|
| Building a construction site, demolishing | Only the colony that owns it (either partner may take down a trading post between them) |
| Picking up recovered goods, log piles and other stacks | Whichever colony's workers get there first (a stack on another colony's marks is left to it) |
| Cutting trees | Only trees the colony marked itself |
| Planting (foresters, farmhouses) | Only on the colony's own planting marks |
| Harvesting, gathering, scavenging | What grows on the colony's own marks, and wild things nobody marked (first come) |
| Working hours | Each colony's own (the working-hours buttons and the clock show yours; the bell rings at the game's own hours, and a handed-over colony's hours stay its own) |
| Chronometers set to working hours | Their own colony's hours |
| Bot worker types (separate science) | Each colony's own unlocks |
| Automation | A building, relay or memory cell may be wired only to its own colony's (copying settings from another colony's building, or placing a copy of it, is refused or placed plain: it would copy the links too) |
| Names | Only the owner renames |
| Other mods' building settings | Only the owner changes them: another mod's action for one building (MixedStorage's warehouse and pile goods) is judged like this mod's own |
| Stockpiles, planting, gathering (mixed factions) | A building keeps to its own faction: its goods, crops and trees, and the common ones |
| Water buildings kept in step (floodgates, fill valves, throttling valves) | Only with their own colony's: synchronising stops at another colony's building, which keeps its heights and its wiring (1.4.0-rc1) |
| Population Counters set to count everywhere | Their own colony's districts (1.4.0-rc1; the Science Counter already read its own colony's science) |
| Indicator warnings | Shown to their own colony's player, as the journal entry already was (1.4.0-rc1) |
| Beavers and bots without a district | The nearest district center of their own colony they can walk to, never another colony's; with none in reach they wait, as in the game, until their player founds again or the colony is handed over (1.4.0-rc1) |
| Migration | Only between a colony's own districts; beavers change colony only through a Trading Post, and a traded beaver must be able to walk to its new district and carry nothing. In the Migration tab (F7), another colony's district's automatic migration settings, and the manual buttons between it and yours, are greyed out |

Marks work per tile: a tile marked by one colony (for planting or cutting) can't be marked or unmarked by another.
Unmarking an area removes only your own marks in it.

**What still reaches across:** the map is one world. Water, droughts and badwater reach everyone, so a dam or a
badwater pump near another colony can still change what flows to it. **Blasts** of dynamite and unstable cores
destroy whatever they reach, the other colony's buildings and beavers included. **Power:** shafts of two colonies that
touch make one network: both colonies' engines and batteries feed it, a clutch of either colony cuts it, and a Power
Meter reads all of it. Decorations, monuments, Wonders, the Iron Teeth Control Tower and other buildings with an area
effect help any beaver or bot standing in their area, whoever's it is (in a mixed game only those of their faction,
the only ones with that need); a Beehive stings Folktails beavers of any colony and speeds up any crop in its range.
Game speed and pause are one clock for everyone.

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

Two colonies' roads never join, except through a Trading Post. That is the only building rule between colonies:
anything else may stand anywhere, right beside another colony's buildings and roads. Refused, with *That would join
another colony's roads*:

- a path, stairs or anything else that carries a road (bridges, gates, tubeways, zipline stations; a district center
  counts as road too), on or beside (the four sides, at the same height) another colony's road, finished or still
  being built (the game's own check knows only finished roads);
- a path on the cell in front of another colony's building's door (that building would join your roads);
- a building whose door opens onto, or beside, another colony's road (the road it needs there would join them).

The game itself also refuses a road, building or tubeway that would join two districts' roads. What this mod adds:

- **Zipline links** are judged once, by the host, with the game's own check, and a tower can't be linked to another
  colony's tower.
- **Two placements that are each fine alone** can still join roads once both are built (two players placing at the
  same moment). Every computer notices at the same tick and warns both players ("Two districts' roads have been
  joined without a District Crossing or Trading Post"). The game keeps running: the first district keeps the shared
  roads and the other goes without them until the joining path or building is removed, so do that soon.

## Water and the map

Water is one shared simulation: a dam upstream changes what flows to a colony downstream, and droughts and badwater
tides come to everyone at once. Stored water (tanks) is a good like any other and belongs to its colony. Prefer maps
with a water source near each start.

## Seeing the roads

Every cell of every colony's roads (paths, stairs, bridges, district centers) is drawn as a bright square in a strong
version of its color while a building or founding tool is in hand, and at any time with **Ctrl+L**, so you can see
whose road reaches a spot before placing a Trading Post between two.

## Testing alone (debug)

With **Always Use Detailed Logging** on (the mod's debug mode) and nobody connected, the host can press
**Ctrl+Shift+K** to make their own actions count as the next colony's (colony 1 → 2 → 3 → 4 → 1). The toolbar,
science and every refusal follow. Every switch is logged. It does nothing while a guest is connected, and it ends
with detailed logging. While the host tests alone this way, every colony counts as present, so none is handed over
for absence.

**Dev mode** (Alt+Shift+Z) in co-op: while the host has dev mode on, three of its tools are played on every computer, for
the colony of the player using them:
- its instant unlock (Ctrl-click on a locked building or bot toggle), which costs no science;
- a construction site's *Finish now*;
- the dev panel's *Add 1000 Science*, which adds to that colony's science (the one pool in a shared game).

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
- **Mixed factions** (1.4.0-beta20) is only for new games; a save keeps its mode. A mixed save needs MultiColony:
  opened in the plain game or the Stability Fork it loads one faction, and the other faction's buildings and
  characters are dropped as the game's "loading issues" (and gone if that copy is saved).
- Mixed factions loads both factions' models and data: more memory, a longer load.
- In a mixed game both halves of a Trading Post show the placing colony's faction's model; achievements, the Iron
  Teeth unlock goal and the save's own faction (the load menu) are the host's faction's; other mods that read the
  game's faction see the host's.
- In a hosted save's waiting room a player whose colony has no faction yet picks one there; the others keep theirs, and
  a player can still switch an untouched colony.
- Every game is hosted through the waiting room, from the main menu (a new game; a save, from Host co-op game); a game
  hosts itself by going there (the game menu's Host co-op game, Save and Rehost). It holds at most seven guests (the Steam lobby's eight), and
  is joined from the main menu only. An invite accepted in a game never connects from it (1.4.0-rc6,
  `InviteRules`): playing alone, the player is asked, and **Save and join** makes the game's own exit save and goes to
  the main menu, which joins the host's page by itself (the player stays in the host's Steam lobby meanwhile, so the
  host lets them in); in a co-op game, or while hosting a page, the invite is set aside with a message and the lobby
  left, so the running session is never taken over. It has no chat, map preview or mod-list
  comparison (mismatch warnings still show in the game). Its settlement-name box has no *Change start location*.
  Anyone who can reach the direct-IP port can come into the room; the host can remove them.
- Over Steam, a guest who leaves a waiting room stays in the host's Steam lobby (eight places). After many leaves and
  rejoins a friend may find it full while the room still shows free places; the host re-opening the room clears it.
- If the host's game stops while it makes a waiting room's world, the guests wait on *Creating the world…* until they
  press **Leave**.
- The game ends only when every beaver on the map is gone, not per colony.
- There is no land: where to build is the players' call. Wild bushes, ruins and piles nobody marked go to whichever
  colony's workers reach them first.
- Demolition marks on ruins and relics nobody marked may be set, cleared and worked by any colony.
- A Trading Post trades only while a different colony's road reaches each half; with one removed, its exchange
  pauses until the road is back.
- A building nobody placed as an action (built before the game was hosted, or in a save older than the two-colony
  builds) takes its colony on its own, checked at the first tick and every 16 ticks after: its district's, else the owner of the road at its
  entrance (a path: the road it is). It waits until one of these applies.
- An exchange gives at most 100 of an item a round (the room a half has); more takes rounds. A round waits while the
  receiving colony has not hauled away the last one's goods (no storage room, or no workers on its half).
- Trading happens in a co-op session: host the game (even alone) to trade.
- The *Global* history graphs (F9, F10, good tooltips) cover the whole map.
- Separate science is meant for co-op: in single player, science earned by another start's buildings goes to that
  colony's pool, which only its player can spend.
- A guest's planting tools follow the first colony's unlocks (the game builds that list before the guest is seated).
- Dev mode's tools, apart from its instant unlock, *Finish now* and *Add 1000 Science*, are not shared: using them
  desyncs a co-op game.
  (Its "place finished", "don't recover goods" and plant-spawning keys, all Ctrl, are off in co-op. The dev power
  generator, which any player can adjust once it stands, is shared since 1.4.0-rc1.)
- **Automation in co-op runs on the game's ticks:** a change shows up to one tick later than in single player, and a
  spring-return lever gives a pulse of one tick (it can't be held on). A Detonator never takes back an arming in
  co-op, so that pulse sets it off, as a click does in single player (until 1.4.0-rc1 it didn't). Two opposite actions
  played in one tick (on, then off, from the HTTP API) lose the pulse.
- **The HTTP API** works in co-op: each player's computer runs its own server (start it from the HTTP Lever's panel),
  and a request to switch an HTTP lever is that player's action, shared with everyone and refused for another
  colony's lever. Requests to colour a lever are ignored in co-op. An HTTP Adapter's webhooks are called by each
  player's computer with the settings made there.
- A light's colour, a decal, a bell's sound, a stream gauge's marker and an HTTP Adapter's webhook settings are each
  player's own until a rehost, which keeps the host's. Nothing simulated reads them.
- Demolishing a platform that holds up dirt from terrain blocks leaves the dirt floating in co-op (single player
  removes it with the platform). Remove the dirt first.
- An action naming a building, crop, good or recipe that the host's game doesn't have (a guest's content mod) is
  refused by the host; a guest missing one the host used leaves the game quietly. Until 1.4.0-rc1 a crop, a good's
  distribution setting or a workshop recipe from such a mod stopped the session for everyone.
- A district center, a Wonder, and a tubeway or zipline station count as road on every tile: they can't stand beside
  another colony's road on any side, even where they have no door. A vertical tubeway is judged on its own level: one
  joining another colony's unfinished tubeway above or below is caught by the joined-roads warning.
- A round of heavy goods takes many trips (a beaver carries one Bot Chassis at a time): give busy Trading Posts more
  workers (up to 10).
- With detailed logging on, the log has one trade line a day: the exchanges, the goods held and waiting to be hauled
  away, what crossed since yesterday and each colony's stock of it. A line `[Colony] Trade check:` is a bug: please
  send the log.
- **A Timberborn update** that changes a part of the game MultiColony corrects for co-op no longer stops the mod from
  starting: single player carries on, and a co-op game is stopped at load with a message naming what is missing,
  instead of going out of step. Update MultiColony then.
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
  exchanges, the ledger, presence, hand-overs, working hours), and each guest compares it with its own at the
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
- **Deletions cost frames in co-op.** Whenever something is deleted during a tick (a harvest picked up, a death, a
  blast, a demolition), every computer finishes that tick in the next frame, so that the game removes it at the same
  moment everywhere. At normal speeds this costs nothing; with many deletions a tick at a high speed or a low frame
  rate the game runs slower than chosen (at most *frame rate ÷ (1 + deletions a tick)* ticks a second). The
  diagnostics report (Ctrl+Shift+J) counts them, and so does a daily `[Perf]` line in the log.
- **Detailed logging is for small colonies.** *Always Use Detailed Logging* records every beaver's decisions and makes
  the host send them all every tick: a colony of 200 or more beavers and bots can't keep up, so the desync dialog no
  longer offers it there (1.4.0-rc1).
- A speed boost the computers can't carry leaves a guest a few seconds behind the host (the host eases off until the
  guest keeps up), so the guest's own actions take that long to show.
- Other mods that change the simulation (LateGamePerformance, for one) need the same version, with the same settings,
  on every player's computer; MultiColony only warns when the players' mod lists differ.
- The daily colony check (`[Colony] Check` in the log) is taken as the host's day reaches each computer, one tick
  after the turn of the day, the same on every computer.
- A half of a paused Trading Post (no colony's road reaches it) belongs to no colony, so any colony may remove it.
- The days of food and water in the Ctrl+T window are an estimate from yesterday's use (today's, scaled, before a
  full day has been sampled): a colony that just doubled its beavers eats faster than the number says. Display only.
- A reserve counts a colony's stock in the district of its Trading Post half, as the game counts it (the goods on
  the half included), not the whole colony's.
- A steward's own colony is "present" while they play; the colony they look after counts as present only for the
  hand-over rule, not for its own player's days away.

## How it works

- **Mixed factions** (`BeaverBuddies/Factions`). The mode is decided before any of the game's collections load
  (`MixedFactions`: the waiting room that makes the world, the solo New Game's Start, or the save's
  `BeaverBuddies.ColonyFactions`). A mixed game loads every faction's buildings, characters, goods, needs and
  materials (`OtherFactionCollections`), de-duplicated.
  - `FactionCatalog` says which faction lists what.
  - Each colony's faction is saved (`ColonyFactionService`), and so is each beaver's (`CharacterFaction`, read from
    the save before the beaver wakes). A building's is its template's.
  - Needs, fur, avatars, outfits, the bot a bot assembler makes, and each building's goods and plants follow the
    character's or building's own faction, the same on every computer. Only what you see follows your colony's.
  - A founding carries its faction and the host checks it is unlocked; a switch is judged by the host and again as
    it is played. Each colony's faction is in the daily colony check.

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
- **Joining** closes at the waiting room's Start, before the world is made or the save sent: every game is hosted
  through a waiting room since 1.4.0-rc4 (a game hosts itself by saving and opening its room in the main menu,
  `HostCoopFlow`). The host's first message says so (`InitializeClientEvent.joiningClosedAtStart`), and
  `ColonyRules.WaitsForStart` then holds nothing back. (A host that waited in its game, paused, while players joined,
  held its first change for *Start the game?*; that way of hosting, BeaverBuddies' original, is gone, and since
  1.4.0-rc5 so is its prompt, `HostStartGate`. The refusals for a game still open to joiners stay, as the guard should
  a host ever start with joining open.)
- **The waiting room** is a phase of the host's server before any save exists (`TimberNet`: `LobbyRoom`,
  `LobbyFrames`, `LobbyInbox`): after the build check a guest waits there, and only its hello and ready are read; the
  host's roster and progress go to it every second from a lane of its own (never the game thread), marked by a -1
  length so a hosted save's bytes on the wire are unchanged. The server stays outside the game's session until the
  saved world loads, so the scene that makes the world is a single-player one (`LobbySession`, `LobbyWorldMaker`). The join check covers the mod's own files (`Buildings`,
  `TemplateCollections`) as well as the game and mod versions.
- **Every colony state change** (owners, marks, science and unlocks, exchanges and their ledger, traded
  beavers, presence, hand-overs, working hours) made inside a tick or a replayed action folds into a running digest.
  The host sends it with every heartbeat; a guest whose own differs stops that tick. The last 16384 changes are
  kept, and every computer logs those after the last count the desynced guest agreed on
  (`ClientDesyncedEvent.colonyChangesAgreed`, `ColonyDigest.DescribeSince`); nothing kept is hashed or sent. A shared
  game has none.
- **Ownership is saved**: on the district centers, on every building (the colony that placed it, from its first
  moment as a construction site) and on map marks. Beavers choose their work from these only, so every computer's beavers choose alike. A shared game
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
