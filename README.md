# Timber Together

***Build apart. Thrive together.***

**Two players, one map, a colony each.** A Timberborn co-op mod where each player runs their own colony (their own
districts, beavers, stock, science and working hours) on a shared map. The colonies meet only at **trading posts**:
a building placed between their two roads, through which they barter goods. Co-op, not a race.
Works on the game's standard maps and on multi-start maps.

![Timberborn 1.1.2.4](https://img.shields.io/badge/Timberborn-1.1.2.4-2a4034?labelColor=172620&style=flat-square) ![Status: beta](https://img.shields.io/badge/status-beta-e0812f?labelColor=172620&style=flat-square) [![GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-2a4034?labelColor=172620&style=flat-square)](License.txt)

[Install](#install) · [Start](#start-a-game) · [Playing](#playing-your-colony) · [Trading](#trading-posts) · [Handover](#when-a-colony-is-handed-over) · [Controls](#controls) · [One shared colony](#one-shared-colony) · [Troubleshooting](#troubleshooting-and-reporting-problems) · [Full rules](TWO-COLONIES.md) · [Changelog](STABILITY-CHANGELOG.md)

> [!WARNING]
> **Beta: the release candidate for 1.4.0.** A large automated test suite covers everything below, but real games
> have been short so far.
> - **Played:** hosting, joining over Steam and founding a second colony; two colonies founding and building, in step
>   every tick; Trading Posts with exchanges of goods and beavers; opening the waiting room and joining it; and the
>   Stability Fork's Steam invites, connection panel, cursors and desync fixes, over hours of two-player play.
> - **Not played yet:** the New Game page's **Separate colonies** checkbox and splitting a shared game from the game
>   menu; **Host co-op game** for a save and from a game, and rejoining a rehost; the waiting room as a window over a
>   game (hosting and joining from inside a game, guests carried into the host's room); starting a game from the
>   waiting room; Folktails and Iron Teeth together; looking after an
>   away player's colony, and hand-overs; most of the road rule; Trading Posts at scale; and the late game
>   (automation and the HTTP API, water automation, power, dynamite and tunnels, both Wonders, bots). A two-player
>   late-game playtest is next ([ALPHA-TEST-SCRIPTS](ALPHA-TEST-SCRIPTS.md), Scripts L, M and T).
>
> In a separate-colonies game a guest whose colony state differs from the host's stops the tick it happens, so a bug
> in that check would also stop a healthy game; the log line says which it was. Play on a copy of your save, keep
> backups, and please report what you find ([how](#troubleshooting-and-reporting-problems)).

Timber Together is a modified version of [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies), the co-op mod
by Thomas Price (thomaswp) and contributors, built through the
[BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork) ([credits](#credits-and-license)).
Everything those do still works: Steam invites, the connection panel and chat, pings, player cursors, and ordinary
shared-colony co-op, which plays as in the Stability Fork (see [One shared colony](#one-shared-colony)).

## Install

**You need:** Timberborn **1.1.2.4** (Steam version, Windows is what has been tested) with the **Harmony** and
**Mod Settings** mods enabled. **Every player** installs the same version of the mod (**the exact same download**) and
runs the same game version.

1. On the [Releases page](https://github.com/timbermods/TimberTogether/releases), open the newest release
   (the releases are pre-releases) and download `TimberTogether-….zip` under **Assets**
   (not "Source code"). Or use the zip you were sent.
2. **Close Timberborn.**
3. In `Documents\Timberborn\Mods`, **delete every other BeaverBuddies folder** (the Stability Fork, the Workshop
   version, any other copy of Timber Together), and unsubscribe from the Workshop BeaverBuddies if you have it. They change
   the same parts of the game and cannot run together; if one is still enabled, the main menu tells you which.
4. Extract the zip and copy the `TimberTogether` folder into `Documents\Timberborn\Mods`.
5. Start Timberborn and enable **Timber Together** in the mod list.


To update, replace the folder with the new download. Every player must update together: a player with a different
build cannot join.

## Start a game

**1. Host: separate colonies or one shared colony.** A new game's colonies are chosen on the **New Game** difficulty
page, under the game's own **Tutorial** checkbox:

- **Separate colonies**, ticked: each player builds their own colony. Unticked: everyone plays one shared colony, as
  in the Stability Fork. The page remembers your last choice. A game keeps what it was made with, for good; a shared
  game can still be split later by a player who wants their own colony (see [one shared colony](#one-shared-colony)).
- Under it, only while it is ticked:
  - **Separate science and unlocks**, ticked: each colony earns its own science and unlocks its own buildings.
    Unticked, the colonies share one pool.
  - **Mixed factions** (not played yet): each player picks **Folktails or Iron Teeth** for their own colony, in the
    waiting room (see [Folktails and Iron Teeth together](#folktails-and-iron-teeth-together)). It needs every faction
    unlocked on your computer, and the game's two factions only: otherwise the box is greyed, and its tooltip says why.

In **Mod Settings → Timber Together**, the host's one colony setting is **Hand over a colony after its player is away
(days)**: 0 (never) by default. A player stepping away asks a friend to look after their colony instead (Ctrl+T). Set
a number of days for a group where someone may not come back (see
[when a colony is handed over](#when-a-colony-is-handed-over)).

**2. Host: a new game with a waiting room** (the easy way; starting a game from it hasn't been played yet).

- **New Game** → faction → map → difficulty, as usual, with **Separate colonies** ticked or not (step 1). Then choose
  **Host co-op game** beside **Start**, and name your settlement (the game's own box).
- The **Co-op Game** page opens. Invite with **Invite Friends** (Steam), or give your IP address (port **25565**).
  Friends appear as they join: **Joining…**, then their name and colony, and **Ready** once they press **Ready**.
  You can remove someone from the room.
- Press **Start Game** whenever you like (if someone is not ready, or nobody came, you are asked first). Your
  computer makes the world behind the loading screen, and everyone loads it together, paused at the start. Nobody new
  can join after Start (a **Save and Rehost** lets someone in later).
- **Guests**: **Join co-op game** (in the main menu, or in the game menu, Esc, while you play alone) lists your Steam
  friends' co-op games; pick the host's and press **Join**. Or accept the Steam invite, or type the host's IP under the
  list. A short *Connecting* box, then the same page with **Ready** and **Leave**. Joined from a game (the game menu,
  or an invite accepted while you play), the room is a window over your game, which pauses under it: **Leave** returns
  you to your game as it was, and your game stays yours until the host's **Start Game**, when it gets its exit save
  (as **Exit to menu** makes it) and the host's save loads. In a co-op game, or while hosting a room of your own, the
  invite waits: leave first, then accept it again.
  A game that has started may drop off the list instead of showing *Already started* (Steam may stop reporting a
  lobby nobody can join). A friend with the same version number but another build (one they built themselves) lists
  as joinable and is then refused with *Multiplayer build mismatch*: everyone installs the same zip.
- In the game, the host has the map's district center. On a standard map every other player is offered **Place your
  district center** as soon as they are in, even while the game is still paused (step 5). On a BeaverBuddies
  multi-start map each player gets a start, in the order they came into the room (only as many starts as players,
  at most the Players field); anyone beyond founds theirs.
- **With Mixed factions on**, the page also shows the New Game faction page's own switcher: each player, you
  included, picks Folktails or Iron Teeth with its arrows, and each row shows that player's faction. A multi-start
  map places each start in its player's faction; on a standard map each guest founds their picked faction's colony.

**Or host a save** (any save, new or old, on any map, including a new game you started alone):

- **A new game on a multi-start map:** start 1 is the host's colony, start 2 the next player's, and so on.
- **A new game on a standard map, or a separate-colonies save:** the district centers already there are the host's
  colony. Every other player founds theirs after joining (step 5).
- **A shared save** (a single-player game made with **Separate colonies** unticked, a shared co-op save, or a
  Stability Fork save) stays one shared colony, unless you tick **Separate colonies** in its Co-op Game room (step 3).
  A player other than the host may also split it later, once, from the game menu (see
  [one shared colony](#one-shared-colony)).

**3. Host a save: Load game → Host co-op game.** In the main menu, or in a game (Esc), choose **Load game**: pick a
settlement and a save, and a gold line under its picture says what it is (separate colonies, with how many players it
remembers, or one shared colony). **Host co-op game**, right of **Load**, checks the save as a load does and opens its
**Co-op Game** room (its settlement, name and in-game date): the full page in the main menu, a window over your game in
a game, which pauses under it. Invite with **Invite Friends** (Steam) or your IP address (port **25565**), friends
ready up, and **Start Game** loads the save for everyone at once; from a game, your game gets its exit save first (as
**Exit to menu** makes it). **Cancel** closes the room (everyone in it is told) and takes you back to the Load game
box. Each player gets the colony the save remembers for them (a new player the next free one); the room reads that from
the save and shows each player's colony and its faction. Guests join as for a new game, from the main menu or from
their game. A guest's game (also after its connection was lost) has no **Host co-op game** in its Load game box: its
copy may be out of step.

For a shared save the page also shows the New Game page's **Separate colonies** checkbox, unticked, with **Separate
science and unlocks** under it, also unticked. Ticked, the game becomes a separate-colonies game at Start, for good:
everything built so far is your colony, and each friend founds their own. The colonies go on sharing one pool of science
and unlocks, as a split does, unless you tick **Separate science and unlocks**: then each colony earns its own from then
on, what was earned so far stays with yours, and your friends' colonies start with none. Your friends' pages say which
it will be.

**4. Host the game you're in.** Playing alone, open the game menu (Esc) and choose **Host co-op game**: the game is
saved, as a new save, and its **Co-op Game** room opens over it, as in step 3 (no exit save at Start: it was just
saved). Hosting a co-op game, the same button reads **Save and Rehost**: the game is saved and its room opens over it,
and your guests come with you by themselves. Their session ends quietly (no *connection lost*), and the room opens over
each guest's own game, already joined: they press **Ready**, you press **Start Game**. Choosing **Load game** → **Host
co-op game** while you host a co-op game brings your guests into that save's room the same way. Over Steam your lobby
is kept for the room, so your guests come straight back, even when it is invite-only; a direct-IP guest reconnects to
the same address. A guest who missed it (a slow connection) sees *The multiplayer connection was lost* and chooses
**Rejoin**, which waits in their game and joins the room when it opens; after a desync the guests choose **Reconnect
(wait for Rehost)**, which does the same. A guest has no hosting button, also after the connection is lost: their copy
of the game may be out of step. Nobody goes through the main menu.

**5. Guest without a colony: found yours.** As soon as you are in, a message offers to **place your district
center** (or press **Ctrl+K** later, whenever you are ready). Place it **anywhere its roads won't join another colony's** (other colonies' roads show in their colors). It
is free, needs no science, and appears **already built**, with starting beavers, food and water. You found once; you may found again only if your colony is handed
over.

**Your colony is remembered.** The save knows each player by their Steam ID (or an id kept on your computer without
Steam): you get the same colony every time, whoever hosts. The connection panel shows each name with its colony.
Over Steam the host checks that id; over a direct IP join it can't (see [Which joins are
verified](#how-it-works)).

### Folktails and Iron Teeth together

*Not played yet.* With **Mixed factions** ticked under **Separate colonies** on the New Game page, every colony plays
its own faction:

- **Pick in the waiting room**, with the game's own faction switcher. Otherwise pick when founding: one card per
  faction.
- **Your colony is your faction's.** Its toolbar, beavers and bots (their looks and needs), buildings, storage, crops
  and goods lists are all its faction's.
- **Trading between factions**: the 17 goods both factions use, and science. Never beavers.
- **Changing your mind**: until your colony builds anything of its own faction, Ctrl+T offers **Play … instead**.
- The host's unlocks decide whether Iron Teeth is there. With a mod that adds a faction installed, new games stay one
  faction (the page says why): the feature is made and checked for Folktails and Iron Teeth.

See [TWO-COLONIES.md](TWO-COLONIES.md#mixed-factions-beta) for the details.

## Playing your colony

- **Your colony is your districts.** Everything you place is yours from the moment you place it. There is no land
  and no border: where to build is up to you and your friends.
- **You build, mark trees and plant anywhere**, right up to another colony's buildings and roads. The one rule:
  **your roads never join another colony's**, except through a Trading Post. A path may not touch another colony's
  road (finished or still being built), and a building's door may not open onto or beside one; the preview turns red
  and says why. **Every colony's roads show as bright squares in its color** while you place a building, and any
  time with **Ctrl+L**.
- **You change only your own colony**, whether or not the other player is playing: their buildings, beavers,
  districts, marks and settings are refused (*That belongs to another colony.*), and so are the settings other mods
  add to a building (MixedStorage's warehouse and pile goods). In the Migration tab (F7), another colony's district's
  migration controls are greyed out.
- **Your beavers work only for your colony:**
  - builders build and demolish only for your colony;
  - lumberjacks cut only trees you marked;
  - foresters and farmers plant only on your marks;
  - gatherers, farmers and scavengers take only what grows on your marks, and wild things nobody marked;
  - wild things, leftovers and piles nobody marked go to whichever colony's workers get there first.
- **Your screen shows your colony only:** the top bar (goods, population, wellbeing, science), the batch control
  lists (F1 to F10), alerts, the notification journal, and the working hours (top right, and the clock's needle).
- **Your own working hours** and, with separate science, your own science, unlocks and bot worker types.
- **Beavers stay in their colony.** They never move to another colony, on their own or by the Migration tab.
- **Shared by everyone:** speed, pause, pings, chat, saving, and the map itself: water, droughts, badwater, weather.
  A dam upstream still changes what flows downstream.

## Trading posts

Build a **Trading Post** (District Management, next to the District Crossing) **between your road and another
colony's**, one half's door on each road. Place it wherever you like, before the roads or after: it trades once your
road reaches one half and another colony's road the other (until then its panel says *Not trading yet*). It is the
only place two colonies' roads may meet. It **costs 10 logs and needs no science**, and looks like a District Crossing. The placer's builders
build it; each colony's beavers run their own half. It only appears in separate-colonies games.

The **District Crossing** is the game's own again, for linking your own districts (import and export settings, its
usual cost and science). Neither half's door may open onto another colony's road, and one that somehow ends up
joining two colonies moves nothing between them.

Goods cross a trading post only through an **exchange** the two colonies agree on. Each colony trades from **its own
half** (the one its roads reach); the other colony's half only offers **Select your half**.

1. **Make an offer.** Select your half. Its panel has a **You give** and a **You get** card. Click the good on a card
   to pick another from the game's goods grid, with each colony's stock (untick *Only what is in stock* to see every
   good). Amounts go from **0 to 100** a round: **−** and **+** (10 at a time, Shift for 1; beavers one at a time) or
   type them. For more, set **Rounds** (1 to 99), or tick **Repeat until cancelled** for a standing deal. The line
   under the cards says what you are offering, e.g. *sarawr gets 100 Logs, and you get 25 Gears. 4 rounds: 400 Logs
   for 100 Gears in all.* **Set a side to 0** for a gift (or to ask for help). For a deal of more than one round,
   **Keep at least** sets a reserve: your workers bring a round only while your colony would still have that much
   left after it, so a standing deal never starves you (the other side sets its own, on the running exchange).
   Then **Make offer**.
2. **The other player accepts** (or declines) on their half. They get a notice when you make the offer. **Decline**
   (and **Withdraw offer**) leaves the offer's terms in your own form, so a counter-offer is a changed number and
   **Make offer**.
3. **The beavers do the rest.** Each round, each colony's Trading Post workers bring their side's goods from their own
   storage to **their own half**, where they wait. When both sides are in, the round **crosses all at once**: goods
   to the other half (that colony's workers haul them into storage), science from pool to pool, beavers to the other
   colony's district. Nothing is given before what it was exchanged for is in. After the last round both players
   get a notice.

**Science and beavers** can be traded too, with the same offer form: science (with separate science) passes from
pool to pool, and adult beavers move to the other colony's district (the last adult always stays), both only when
the round crosses. Each beaver who arrives gets a line in the notification journal, like a birth.

**Ending an exchange early takes both players**, as agreeing to it did: **Cancel exchange** asks the other player,
who chooses **Agree to cancel** or **Keep trading**. What waits on each half then goes back to its own colony; rounds
that crossed stay crossed.

**Offer again:** the panel remembers the last exchange at each post and offers it again in one click (a line above
the form); clicking a row of the post's ledger puts that round's terms into the form.

**All your trading posts at once:** **Ctrl+T**, the square **Trade** button at the top right, or **All Posts** on a
trading post opens a window (drawn like the game's own boxes; drag its title to move it; close it with its close
button, Esc or Ctrl+T) listing
every trading post of your colony, its exchange and its round, with a **Go to** button; and every colony with its
population, whether its player is playing (and how many days of the host's limit an absent player has missed), its
**food and water** with the days they last at yesterday's use, what it is **looking for**, and who looks after it.

**Looking for:** in that window, your own colony has a **Looking for** row: up to three goods (or science, or
beavers) you would like to receive, chosen from the game's goods grid. Other players see them beside your colony
there, under the header of a trading post they share with you, and in the goods grid when they choose what to give
you (the count in yellow).

- **One exchange at a time** per trading post; build more Trading Posts for more at once.
- **A Trading Post is not a store.** Each half has room for 100 of a good, used only by the round under way: your
  goods waiting to cross, or the other colony's waiting to be hauled away. Staff both halves, and keep storage room
  for what you receive, and houses for beavers.
- **Import and export settings don't apply** at a Trading Post. They still move goods between your own districts
  through District Crossings, as in the game (a crossing holds the game's 30 of a good).
- The panel also shows each side's round as a bar, what waits on your half, the post's **ledger** (each round that
  crossed there: when, what you gave, what you got), and what has passed each way between the two colonies.
- Either of the two colonies trading through a post may remove it; no other colony can. Its two halves are placed as
  one: if either may not stand where it was put, neither is placed.

Details: [TWO-COLONIES.md](TWO-COLONIES.md#trading-posts).

## Looking after another player's colony

Away for the evening? Ask a friend to look after your colony: in the **Ctrl+T** window, on your colony, press **Let
… look after it** (one button per other player in the session), or **Take it back** later. The steward finds **Run
this colony** on your colony's row: their actions, toolbar, top bar and science then count as your colony's until
they press **Back to your colony**. Both of you can play your colony at the same time. A colony looked after by a
player who is in the game is **not handed over** for its own player's absence. The host may also ask a player to
look after the colony of a player who is away, and may end any stewardship. The steward is remembered by the save
(by the same Steam ID or local id as the colonies).

## When a colony is handed over

A colony whose player can't run it goes to another player, with its buildings, stock and science:

- **No beavers or bots left** for a whole day: to the nearest living colony.
- **Its player away**, only if the host has set a number of days (the default is 0, never): once they have missed
  that many in-game days of hosted co-op play (changeable during the game; days the host plays alone in single
  player, and the first day after loading, don't count), to the nearest colony whose player is playing. In a
  mixed-factions game only to a colony of its own faction; with none, it waits. A guest who leaves is away from the
  next day on. To keep a friend's colony going while they are away, look after it (above).
- **By the host**, from the Ctrl+T window, once the game has started: any colony whose player is away, or that has
  no beavers (for example to a player whose Steam account changed).

With a limit set, the Ctrl+T window shows how close an absent player is (*missed 6 of 7 days*), and the day the count reaches the
limit every player in the game gets a warning (in a mixed-factions game, the first such day a colony of its faction is
being played): unless the absent player is in the game the next day, the colony is handed over then. The player who lost their colony gets a notice and may **found a new one** with **Ctrl+K**.

## Controls

| Key | Action |
|---|---|
| **Ctrl+K** | Found your colony (a player without one; in a shared game only if the host allows it) |
| **Ctrl+L** | Show every colony's roads as bright squares in its color (they also show while you place a building or found a colony) |
| **Ctrl+T** | Trading posts and colonies |
| **Home** | Back to your colony (its biggest district center; in a shared game, the biggest); clicking a name in the connection panel takes your camera to that player instead |
| **Ctrl+Shift+J** | Write a diagnostics report (also a button in the Ctrl+T window) |
| **Ctrl+Shift+K** | *Debug only:* the host acts as the next colony, for testing alone (needs **Always Use Detailed Logging** and nobody connected) |

All can be changed under **Options → Bindings → Timber Together**. The co-op keys **Ping Location**, **Toggle
connection panel** and **Chat: start typing** are unbound until you set them there.

## Good to know

- **Up to four colonies.** More players join as helpers of the host's colony.
- **The game ends** only when every beaver on the map is gone, not per colony.
- **Two placements at the same moment** can join two districts' roads once both are built; the game keeps running,
  both players are warned (*Two districts' roads have been joined…*), and the first district keeps the shared roads
  until the joining path or building is removed.
- **Dev mode** (Alt+Shift+Z): only its instant unlock, *Finish now* and the dev panel's *Add 1000 Science* are shared
  (while the host has dev mode on); its other tools and keys desync the game (the debug buttons, the dev panel, Delete
  on a selected beaver), and its three Ctrl keys (place finished, don't recover goods, spawn planted crops) are off in
  co-op.
- **Gates and automation** react at the tick rather than the frame in co-op: at most a tick later than in single
  player, the same on every computer. So do the **Wonders**' animations and the Earth Repopulator's plane launch
  (a launch takes a few ticks longer), and a **spring-return lever** switches off the tick after it is pressed even
  while you hold it. That one-tick pulse sets off a **Detonator**.
- **The HTTP API** works in co-op: each player's computer runs its own (start it from the HTTP Lever's panel), and a
  request to switch an HTTP lever is that player's action, shared with everyone and refused for another colony's
  lever. Requests to colour a lever are ignored in co-op; an HTTP Adapter's webhooks are called by each player's
  computer with the settings made there.
- **Mods that add content:** every player needs the same ones. The host refuses a building, crop, good or recipe its
  own game does not have; a player whose game lacks one the host used leaves the game with a message (the others play
  on).
- **Other mods that change the simulation** (LateGamePerformance, for one): every player needs the same version, with
  the same settings. Timber Together only warns when the players' mod lists differ.
- **After a Timberborn update:** if the update changes a part of the game Timber Together corrects for co-op, co-op stops
  at load with a message naming it, instead of going out of step; single player carries on. Update Timber Together.
- **Single player:** without a co-op session nothing is refused and no colony can be founded. Host the game (even
  alone) to play the mode.
- **Performance:** more colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest
  from the connection panel. In co-op, every deletion in a tick (a harvest picked up, a death, a blast) makes every
  computer finish that tick in the next frame, so the game can run slower than chosen at high speed with many of
  them; the diagnostics report (Ctrl+Shift+J) and a daily `[Perf]` line in the log count them. **Always Use Detailed
  Logging** records every beaver's decisions and makes the host send them every tick: it is for small colonies, and
  the desync dialog doesn't offer it from 200 beavers and bots.
- **New text is English only.**

## One shared colony

With **Separate colonies** unticked on the New Game page, a new game is ordinary shared co-op, as in the Stability
Fork: one colony that every player builds together. A shared save (made that way, or in the Stability Fork) loads the
same way and stays shared.

**Splitting off.** A player other than the host who wants a colony of their own opens the game menu
(Esc) and chooses **Found your own colony**, below Settings. It is there only for them, and only in a shared game.
It asks first, because this can't be undone: the game becomes a separate-colonies game for every player, for good,
and the shared colony, with everything built so far, stays the host's. Then they place their district center, which
starts with the game's starting beavers and goods; only that placement makes the split, and leaving the tool changes
nothing. Science and unlocks stay one pool, shared by every colony. Anyone else who played the shared colony then
founds their own (Ctrl+K) or looks after the host's.

Nothing of the colony model runs in a shared game: no owners or colony rules, no Trading Post (not even in dev
mode), District Crossings holding the game's 30 of a good, no colony check on the heartbeat or once a day, and a
save with nothing of this mod's in it, the same as the Stability Fork's. What does apply is what Timber Together adds to
co-op in general: its desync fixes (gates, automation, planting on sliced views, dev mode's shortcuts, Tick once,
joined district roads), the performance pass, a guest's pending actions, the host being asked before joining
closes, the speed boost, going to a player and **Home**, your own chat color, and the diagnostics report on
**Ctrl+Shift+J**. Every player still needs Timber Together: it and the Stability Fork cannot join each other's games.

## Co-op basics

Inherited from the Stability Fork. The full guides are in [STEAM-INVITES.md](STEAM-INVITES.md), [CONNECTION-PANEL.md](CONNECTION-PANEL.md)
and [PLAYER-ACTIVITY.md](PLAYER-ACTIVITY.md).

- **Steam invites:** both players online in Steam and owning Timberborn. Settings **Enable Steam Networking** and
  **Allow Friends to Join Directly via Steam**. Only friends who join the host's friends-only lobby can connect.
- **Direct IP:** the host forwards port **25565**, or both use a VPN such as Hamachi. Anyone who can reach that port
  while you host can join, and the host can't check who they are (see [Which joins are verified](#how-it-works)).
- **The connection panel** (top-left) shows each player, their ping, whether you're in sync, the tick rate and a
  chat box. At the top of the chat is a **speed boost**: `-` and `+` (or a typed number) add a constant to the speed
  you pick at the top right, for everyone, so the fastest button with +0.5 runs at 7.5x (from -6.5 to +23; the
  game never runs below 0.5x or above 30x). Collapse the panel by clicking its title; hide or move it in Mod
  Settings. Chat names are drawn in each
  player's cursor color, and a player who has not chosen one gets a color of their own by player number (the host
  orange, then blue, green, pink, purple, teal, red and lime), so nobody starts out yellow. The color you see your
  own name in is yours to pick under Options, **Player cursors** (only you see it).
- **A guest's actions** go to the host and back before they happen. While they travel, the tiles of what you placed
  or marked are tinted (red for a removal); if the host refuses the action, a notice says why.
- **Mismatched mods** are flagged when someone joins. It's a warning, but a mod that changes the simulation will
  cause desyncs, so match mod lists. A guest without a mod whose actions the host sends (MixedStorage, say) leaves the
  game at the first such action, with a message naming the mod; the host and the other players play on. An action a
  guest sends that the host cannot read is refused, and the guest's other actions of that moment still happen. This mod's own files are checked, not warned
  about: a different zip, or a missing or edited `Buildings` or `TemplateCollections` folder, is refused at the join.
- **Desyncs** can still happen. Every tick each guest compares all of the host's random-number state with its own
  and which entities tick and where every walking character stands: a random
  state that differs stops the game, an entity or walker difference is written to the log once (`Entity mismatch`,
  `Walker mismatch`) so a later desync says when the games first differed. In a separate-colonies game, colony state
  (owners, marks, science, exchanges) is compared with the host's every tick too, and in full once a day, so
  one is caught when it happens. The host uses **Save and Rehost** and the guests **Reconnect (wait for Rehost)**, in
  any order: each guest waits in their game and joins the host's Co-op Game room, a window over it, as soon as it
  opens, the way they joined (a direct-IP guest redials the address it used; a Steam guest joins the host's Steam lobby
  when Steam shows it, with **Allow Friends to Join Directly via Steam** on, or accepts the host's invite).
- **Direct connections send at once:** Nagle's algorithm is off on every direct (IP) socket, and only the save sent to
  a joining player is paced.

## Troubleshooting and reporting problems

- **Can't join / stuck on "Receiving map…":** every player must have the same zip. Reinstall from the same file and restart.
  *Multiplayer build mismatch* names what differs (the game or mod version, or the mod's `Buildings` and
  `TemplateCollections` files).
- **A friend lost the connection, or joins late:** they choose **Rejoin** (or join from **Join co-op game**), and the
  host chooses **Save and Rehost** in the game menu: everyone meets in the Co-op Game room, and Start Game carries on.
- **A guest's game failed to load after the waiting room's Start:** nobody can join a started game; the host uses
  **Save and Rehost**, and the guest joins that.
- **"… invited you to their co-op game. You are in a co-op game now":** a co-op game is not ended for an invite.
  Leave it from the game menu (Esc), then accept the invite again, or join from **Join co-op game**.
- **The Co-op Game window over your game:** the game is paused under it. **Cancel** (the host) or **Leave** (a guest),
  or its close button or Esc, returns you to your game as it was. Only the host's **Start Game** replaces your game,
  after its exit save.
- **Ctrl+K says this is one shared colony:** the save is a shared game. A player other than the host splits it from
  the game menu (Esc → **Found your own colony**); that makes it separate for good.
- **A desync dialog with `Colony state differs` in the log:** the every-tick colony check disagreed. It
  may be a bug in the check itself; send every player's `Player.log`: the line has both numbers and how many changes
  each side counted.
- **"That would join another colony's roads":** a path of yours would touch their road, or a building's door would
  open onto or beside it. Keep your roads a cell apart from theirs; to link the two, put a Trading Post between them.
- **A Trading Post says *Not trading yet*:** each half needs a different colony's road at its door, yours at one and
  theirs at the other (Ctrl+L shows whose roads are whose).
- **Everything is refused right after joining:** the host has not seated you yet; wait a moment. If it persists,
  send the logs.
- **Diagnostics report:** press **Ctrl+Shift+J** (or *Diagnostics report* in the Ctrl+T window) when something
  looks wrong: slow, stuck beavers, a colony misbehaving. The report is copied to the clipboard, ready to paste into
  a chat or an issue, and saved in `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\TimberTogether-Reports`.
  In a separate-colonies game it is also written by itself when a computer desyncs: then send **every player's**
  latest report. It covers the
  frame and tick rate and the time spent in this mod's code, each colony's districts, homeless and jobless beavers,
  building statuses (such as unreachable) and trading posts (with what is holding an exchange up), the daily check
  of the colony state and the running digest that must match on every computer, and the last colony log lines.
- **Reporting:** send `Player.log` from every player
  (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`), plus screenshots for anything on screen.
  Lines from this mode start with `[Colony]`. Step-by-step [test scripts](ALPHA-TEST-SCRIPTS.md) say what to try and what to expect.

Report issues at [github.com/timbermods/TimberTogether/issues](https://github.com/timbermods/TimberTogether/issues),
not to the original BeaverBuddies project.

## How it works

- **The host decides.** Every player action goes through the host. The host writes which connection each action
  came from (a guest can't pretend to be another connection), seats players by their stable id, writes the actor's
  colony into the action, checks it against the rules just before playing it, and then plays and forwards it, keeps
  only the actor's part of a list action, or drops it (logged as `[Colony] Refused …`). Guests never judge, so the
  computers can't disagree.
- **Which joins are verified.** A guest who joins **over Steam** is seated by the Steam ID that Steam proved for
  its connection: a guest saying another Steam ID is refused, and one whose game could not read its own Steam ID is
  seated by the proved one. A guest who joins **by IP (direct connection)** proves nothing, so the host takes the id
  it says: someone who copies another player's id (every player's id is shared with the session and kept in the
  save) can take that player's colony, and someone who rejoins under new ids while the host waits at the start can
  reserve the free colonies. The direct-IP port (**25565**) is open whenever you host, **Steam invites or not**:
  anyone who can reach it (on your network, through a forwarded port, or over IPv6 if your firewall lets them in)
  joins unverified. Forward that port, or let it through your firewall, only while you play by IP with people you
  trust. Either way a guest can't say hello a second time as someone else, and an id with a line break or a `|` in
  it (no id the mod makes has one) is refused.
- **Ownership is saved**: on district centers, on every building (the colony that placed it) and on map marks.
  Beavers' choices of work read only these, identically on every computer.
- **Guests take the host's answers.** Whether a placement is still possible when it is played, what a founded colony
  starts with, the day's presence: each is decided once by the host and written into the action every computer
  plays. In a separate-colonies game every change to colony state folds into a running digest that the host sends
  with each heartbeat; a guest whose digest differs stops that tick.
- A separate-colonies save gets a few small extra entries (the mode, who plays which colony, owners, marks,
  science, working hours, exchanges, the trade ledger, which colony each journal entry is for). A shared-colony game's
  save has none: it is the same as the
  Stability Fork's.

- **The waiting room** keeps its players in a room of its own on the host's server, before any save exists: small
  messages of their own after the build check (a guest's hello and ready, the host's roster and progress), never game
  actions. At **Start Game** the host's computer makes the world as a single-player game, saves it at tick 0, sends
  those bytes to every player in the room, and loads the same bytes itself as the hosted game, as **Host co-op game**
  on a save does. The colony slot table is filled in the room's order before that first save.
- **A room opened in a game** is the same room in a window. A guest who joins from a game keeps playing alone until the
  host's save arrives: the join is held apart from the game (the game is not a co-op game while its player waits).
  A host moving a running co-op game to a room first tells every guest (a small message of its own, never a game
  action), then ends the session and opens the room on the same port, handing its Steam lobby to the room; each guest
  ends its session quietly and joins the room by itself.

Design notes and the plans: [design/PRE-GAME-LOBBY-PLAN.md](design/PRE-GAME-LOBBY-PLAN.md) (the waiting room),
[design/IN-GAME-HOSTING-PLAN.md](design/IN-GAME-HOSTING-PLAN.md) (the room in a game),
[design/TRADING-EXCHANGE-PLAN.md](design/TRADING-EXCHANGE-PLAN.md),
[design/TWO-COLONY-ALPHA-PLAN.md](design/TWO-COLONY-ALPHA-PLAN.md); the desync and network review:
[design/REVIEW-FINDINGS-1.4.0-beta11.md](design/REVIEW-FINDINGS-1.4.0-beta11.md); Wonders on the tick:
[BeaverBuddies/Doc/WonderTiming.md](BeaverBuddies/Doc/WonderTiming.md).

## Building from source

1. Clone this repository and copy `BeaverBuddies/env.props.windows-template` to `BeaverBuddies/env.props`; point it
   at your Timberborn install and the Workshop copies of Harmony and Mod Settings.
2. Restore once:
   `dotnet restore BeaverBuddies/BeaverBuddies.csproj -s https://api.nuget.org/v3/index.json -s https://nuget.bepinex.dev/v3/index.json`
   (and the same for `StabilityTests` and `RuntimeChecks`).
3. Build with `--no-restore`, choosing where the mod folder goes:
   `dotnet build BeaverBuddies/BeaverBuddies.csproj -c "Release Steam" --no-restore -p:BeaverBuddiesModsPath="<folder>\BeaverBuddies\"`
   (`-c Release` for a build without Steam networking).
4. Checks: `dotnet run --project StabilityTests --no-build` (headless),
   `dotnet run --project RuntimeChecks --no-restore -- <built BeaverBuddies.dll> <Timberborn_Data\Managed> <Harmony folder> <Mod Settings Scripts folder>`
   (against the game's own assemblies), and
   `python -m unittest discover -s RuntimeChecks -p "test_water_snapshots.py"`.

These checks can't start Unity or prove full multiplayer determinism; in-game testing is what confirms behavior.

## Credits and license

Timber Together is a modified version of [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies)
([Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3293380223)), created by **Thomas Price (thomaswp)** with contributions from Robin, Slide, Phil Lehmkuhl,
SamuZad, Joe Stead, Zibo Ye, Dasker and Tarensaror. The multiplayer this all rests on is theirs (keeping every
player's game in step, the connections, the desync checks) and so is much of the code: this mod would not exist
without their work. It is built through the
[BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork).
[CREDITS.md](CREDITS.md) has the full credits.

Licensed under GPL-3.0, like the original ([License.txt](License.txt)); `License.txt` and `CREDITS.md` ship in the mod
folder. Every original commit keeps its author in the repository history, and every change is listed in
[STABILITY-CHANGELOG.md](STABILITY-CHANGELOG.md). An unofficial community mod for Timberborn, not affiliated with or
endorsed by Mechanistry or by the authors of BeaverBuddies.
