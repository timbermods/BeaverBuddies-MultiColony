# Separate colonies (alpha)

Two (up to four) players on one map, each running their own colony: their own districts, land, beavers, stock,
working hours and, if the host chooses, their own science and unlocks. It is co-op, not a race: nobody wins, and the
colonies meet only at **trading posts**, where they barter.

**State of testing.** Alpha. Seen in a game with the land-split alphas (alpha1 to 5): hosting and joining over
Steam, founding a second colony and building in it. **Nothing of this version's model has been seen in a game
yet**: ownership, land, per-colony marks and work, trading-post exchanges and their panel, separate science and
the road-network checks are covered by automated checks only. Play on a copy of your save and keep backups.

## The rules in one minute

- **A colony is its districts and its land.** Every district center belongs to a player. Beavers belong to the district
  they live in; buildings to their district, or else to the colony that placed them; marks to the colony that made
  them. A colony's **land** is every tile within 10 tiles of its buildings and paths, first come: where two colonies
  both reach, the land is the one's that got there first, and stays theirs while they reach it.
- **Build, mark and plant on your land or free land.** Never on another colony's land or right next to its roads.
  Building towards another colony stops at the edge of its land.
- **Two colonies' roads never join, except through a Trading Post**, the building colonies barter through, and the
  only place colonies meet. (A District Crossing links a colony's own districts, as in the game.)
- **You change your own colony only**, whether or not the other player is playing.
- **Each colony's beavers work for it alone** (see *Keeping colonies apart*).
- **Shared by everyone:** game speed and pause, pings, chat, saving, and the map itself: water, droughts, badwater
  tides and weather.

## Starting

**Host settings** (Mod Settings → BeaverBuddies), read when hosting starts:

| Setting | Default | What it does |
|---|---|---|
| **Separate colonies (alpha)** | on | Players run their own colonies. Off: one shared colony, as in the Stability Fork (except that every District Crossing holds 100 of a good). |
| **Separate science and unlocks per colony (alpha)** | on | Each colony earns and spends its own science and unlocks its own buildings. Chosen when a separate-colonies game begins and kept for the life of the save. |

**Who plays which colony** is remembered by the save: each player is known by their Steam ID (or, without Steam, by
an id kept on their computer). The first time a player joins a save they take the next free colony; after that they
always get the same one, whoever hosts. With every colony taken (four), an extra player joins as a helper of the
host's colony. The connection panel shows each name with its colony number.

**A new game on a multi-start map** (BeaverBuddies maps with several starting locations) gives start N to player N:
the host's first colony is start 1, the next player's start 2, and so on.

**Any other game** (a standard map, or any existing save): the district centers already there are the host's
colony's. Every other player **founds** their colony once:

1. On joining, a message offers to place a district center. (If you cancel, **Ctrl+K** opens the same tool.)
2. Place it at least 20 tiles from other colonies' buildings and paths (their land shows as coloured outlines), so
   both colonies have room to grow. It is free, needs no science, and appears **already built**, yours, with starting
   beavers, food and water (the new game's, or the Normal difficulty's for a save that did not record them).
3. Founding in a shared game turns it into a separate-colonies game. The existing districts stay the host's.

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
Distribution tab) never move anything across one, so its panel has no *Imported goods* box, no **Manage
distribution** and no stock list; it lists what waits on the half itself.

**The District Crossing is the game's own**, with its usual cost, science and import and export settings, for
linking a colony's own districts. It is an ordinary building for the colony rules: it may not stand on another
colony's land or next to its roads. If one ever ends up joining two colonies anyway (another colony's road reaching
its far half), it moves nothing between them, and its panel says to build a Trading Post.

**An exchange** is "this many of one good for that many of another", for example *1000 logs for 250 gears*:

1. **Offer.** Select either half of the trading post. The trading section at the bottom of its panel has a **You
   give** and a **You get** card, each with an item and an amount from 0 to 9999. Click the item for the game's goods
   grid beside the panel (science and beavers first, then the goods by the game's groups, each with the colony's
   stock; *Only what is in stock* is ticked at first). Choosing the other card's item swaps the two. **−** and **+**
   step the amount by 10 (Shift: 100; beavers by 1). A line under the cards reads the offer back, or says what is wrong
   with it; **Make offer** waits for a valid one. **Repeat until cancelled** makes it a standing deal. One side may be
   0: asking for 0 is a gift, giving 0 asks for help. The other player gets a notice.
2. **Answer.** The other colony's player selects the trading post and chooses **Accept** or **Decline**. The offering
   player may **Withdraw offer** until then. An offer changed in the meantime is never accepted by mistake.
3. **Delivery.** Each colony's Trading Post workers fetch their colony's side from its storage and bring it to
   their half; it passes to the other half at once, and the other colony's workers haul it away into their storage. No
   beaver crosses.
4. **In step.** Neither side may deliver more than a tenth of its amount (at least 10) ahead of what the other side
   has delivered, so an exchange is never filled one way only. The panel says when your beavers are waiting.
5. **Done.** When both amounts have crossed, both players get a notice and the trading post is free for the next
   exchange; a repeating exchange starts its next round instead. **Cancel exchange** ends it early (either player);
   what has crossed stays crossed.

**Science and beavers** are exchange items too. Nobody carries them: science passes from pool to pool (only with
separate science), 25 at a time, and adult beavers move from the giving half's district to the other, one at a
time, the last adult always staying. Both keep the same pace as goods.

**Ctrl+T** (or **Trade** at the top right) opens the trading posts and colonies window: each of your Trading Posts
with its exchange and progress (or *not trading yet*) and a **Go to** button, and each colony with its population and whether its player is
playing.

**Good to know:**

- One exchange at a time per trading post. Build more Trading Posts for more exchanges at once.
- A half holds up to **100 of each good** waiting to be hauled away (30 in the game). What has not been hauled away
  holds up further deliveries of that good: staff both halves, and keep storage room for what you receive.
- Anything of an exchange's good that crosses counts towards it.

**The trading-post panel** is built from the game's own panel pieces (the Workplace section's board, the
description's blue cards, the game's wooden and red buttons, input boxes, progress bars and check boxes, and the
warehouse's goods grid). It shows who you trade with in their colour, with **All posts** (Ctrl+T); the exchange (the
offer cards, an offer waiting for an answer, or each side's progress as a bar with its count); what is waiting on the
half (only when something is); what has passed each way between the two colonies (all trading posts), as icons with
amounts; and, with separate science, **Gift science 50 / 250**. A player of neither colony sees it read-only. When
the game's sections above it leave too little room on the screen, its content scrolls instead of running off the
bottom.

Either colony may remove a Trading Post between them (deleting one half removes both, as in the game); a District
Crossing, or a Trading Post within one colony, is that colony's alone. Running a half (workers,
priority) stays with its colony.

## When a colony is handed over

A colony whose player can't run it goes to another colony: its district centers, buildings, land, marks, stock and
science pool. The receiving colony may also build whatever the old one had unlocked; the old player keeps their
unlocks too.

- **No beavers or bots left** for a whole in-game day: to the nearest living colony (district center to district
  center). Every computer decides this the same way.
- **Its player away:** once they have missed the number of in-game days of hosted co-op play the host set (**Hand
  over a colony after its player is away**, 7 by default, 0 for never) in a row: to the nearest colony whose player
  is playing. Days the host plays alone in single player, and the first day after loading (players are still
  joining), don't count; a day the player is in the game starts the count again. The host decides and every
  computer plays it. Not while debugging alone (with detailed logging on), where the host plays every colony.
- **By the host**, from the Ctrl+T window: any colony whose player is away (not in this session), or that has no
  beavers. Useful for a player whose Steam account changed: they join, get a new slot, and the host hands their old
  colony to it.

The player who lost their colony gets a notice and may **found a new one** (Ctrl+K). A trading post between the two
colonies then stands within one colony: its exchange ends, and it trades again only if another colony's roads reach
its other half.

## Separate science and unlocks

With the setting on, when a separate-colonies game begins:

- **Each colony has its own science.** Inventors, the Numbercruncher and the observatory add to their own colony's
  science; a relic's reward goes to the colony of the beaver who demolished it; the Iron Teeth control tower uses its
  own colony's. The top bar shows yours.
- **Each colony unlocks its own buildings.** Unlocking costs your colony's science and unlocks the building on your
  toolbar only. Placing a building checks that your colony has it unlocked.
- A shared game being split: its science and unlocks so far stay with the first colony; new colonies start with
  none. A new game: every colony starts with the same unlocks.
- **Bot worker types** ("bots may work here") are unlocked per colony too, paid from the unlocking colony's science.

## Keeping colonies apart

The game hands out some work to any beaver who can walk there, and where two colonies' land meets their beavers can
walk to the same places. So in a separate-colonies game:

| Work | Whose |
|---|---|
| Building a construction site, demolishing, picking up recovered goods | Only the colony that owns it (either colony may take down a trading post) |
| Cutting trees | Only trees the colony marked itself |
| Planting (foresters, farmhouses) | Only on the colony's own planting marks |
| Harvesting, gathering, scavenging | What grows on the colony's own marks, or wild things on land no other colony holds |
| Working hours | Each colony's own (the working-hours buttons and the clock show yours) |
| Chronometers set to working hours | Their own colony's hours |
| Bot worker types (separate science) | Each colony's own unlocks |
| Automation | A building, relay or memory cell may be wired only to its own colony's |
| Names | Only the owner renames |
| Migration | Only between a colony's own districts |

Marks work per tile: a tile marked by one colony (for planting or cutting) can't be marked or unmarked by another.
Unmarking an area removes only your own marks in it.

**What still reaches across:** the map is one world. Water, droughts and badwater reach everyone, so a dam or a
badwater pump outside another colony's land can still change what flows onto it. Decorations and other buildings
with an area effect help any beaver standing in their area, whoever's it is. Game speed and pause are one clock for
everyone.

## What you see

In a co-op session each player's interface shows **their own colony only**: the top bar's goods, population,
housing, workplaces, wellbeing and science; the batch control window's lists (F1 to F10, opening on your biggest
district); alerts; the notification journal. Selecting another colony's building opens its panels but leaves your
figures alone. Still whole-map: the *Global* history graphs in F9/F10 and in a good's tooltip, which the game
records for the whole map.

## Road networks

The game refuses to place a road, building or tubeway that would join two districts' roads, so two colonies' roads
can only meet at a crossing (a Trading Post, between colonies). Two gaps are closed by this mod:

- **Zipline links** are judged once, by the host, with the game's own check.
- **Two placements that are each fine alone** can join roads once both are built (two players placing at the same
  moment). Every computer notices at the same moment and warns both players ("Two districts' roads have been joined
  without a District Crossing or Trading Post"): remove the joining path or building. The game's district bookkeeping cannot handle
  joined roads, so do this at once.

## Water and the map

Water is one shared simulation: a dam upstream changes what flows to a colony downstream, and droughts and badwater
tides come to everyone at once. Stored water (tanks) is a good like any other and belongs to its colony. Prefer maps
with a water source near each start.

## Seeing the land

Every colony's land is outlined in its colour while a building, planting, cutting, demolishing or founding tool is in
hand, and at any time with **Ctrl+L**. Where two outlines meet is where a trading post goes.

## Testing alone (debug)

With **Always Use Detailed Logging** on (the mod's debug mode), the host can press **Ctrl+Shift+K** to make their own
actions count as the next colony's (colony 1 → 2 → 3 → 4 → 1). The toolbar, science and every refusal follow. Every
switch is logged.

**Dev mode** (Alt+Shift+Z) in co-op: while the host has dev mode on, two of its tools are played on every computer, for
the colony of the player using them:
- its instant unlock (Ctrl-click on a locked building or bot toggle), which costs no science;
- a construction site's *Finish now*.

Its other tools, such as deleting any object or the dev panel's other buttons, change only the computer they are used
on, and desync the game. A notice says so when dev mode is switched on in co-op.

**Tick once** (the pause key pressed while paused) is off in co-op: it would advance only the computer it is
pressed on, and desync the game. A notice says so; unpause to play on.

## Known limits

- Up to four colonies; more players join as helpers of the host's colony.
- The game ends only when every beaver on the map is gone, not per colony.
- A colony's land is measured in a straight line on the map (10 tiles), not by walking; on a cliff edge it may
  reach a little further than its beavers do.
- Land is first come. Founding keeps 20 tiles from other colonies, but a colony that grows quickly towards another
  still claims the land between them first.
- A building nobody placed as an action (built before the game was hosted, or in a save older than the two-colony
  builds) takes its colony on its own, checked every 16 ticks: its district's, else the owner of the road at its
  entrance (a path: the road it is), else the colony whose land it stands on. One on nobody's land, or across two
  colonies' land, waits until one of these applies.
- A half of a trading post holds at most 100 of a good; an exchange stalls while the receiving colony has nowhere to
  put what arrives.
- Trading happens in a co-op session: host the game (even alone) to trade.
- The *Global* history graphs (F9, F10, good tooltips) cover the whole map.
- Separate science is meant for co-op: in single player, science earned by another start's buildings goes to that
  colony's pool, which only its player can spend.
- A guest's planting tools follow the first colony's unlocks (the game builds that list before the guest is seated).
- Dev mode's tools, apart from its instant unlock and *Finish now*, are not shared: using them desyncs a co-op game.
- While a co-op game is paused, what was just built or removed updates its district (the district badge and highlight,
  and which district's builders a new construction site waits for) when the game resumes, at the same moment on every
  computer.
- Placing is judged by the placing player's own tool: the check against joining two districts' roads is not repeated
  in the multiplayer replay, where it read state that differs between computers.
- Two colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest from the
  connection panel.

## How it works

- **The host decides.** Every action goes through the host. The host writes which connection it came from (a guest
  cannot claim another), seats players by their stable id, writes the actor's colony into the action, checks it
  against the rules just before playing it, and plays and forwards it, keeps only the actor's part of a list action,
  or drops it (logged as `[Colony] Refused …`). Guests never judge, so the computers cannot disagree.
- **Ownership is saved**: on the district centers, on every building (the colony that placed it, from its first
  moment as a construction site) and on map marks. Land is worked out from the buildings standing, the same on every
  computer. Beavers choose their work from these only, so every computer's beavers choose alike.
- **Science** keeps one pool and one unlock set per colony in the save. Simulation code that earns, spends or reads
  science names the colony of the building doing it; everything else is display.
- **Exchanges** are saved on the two halves of the Trading Post. Goods are counted at the one place they pass from one
  half to the other, identically on every computer; the totals delivered each way are kept there too.

## Testing

Automated checks (all passing): **StabilityTests** (headless: the network stamping, player slots, the ownership
rules, founding, migration pairing) and **RuntimeChecks** against the compiled mod and the game's assemblies: every
action type declares what it touches, the shared actions are listed for review, and every game method or field the
mod hooks still exists. In-game test scripts: [ALPHA-TEST-SCRIPTS.md](ALPHA-TEST-SCRIPTS.md).
