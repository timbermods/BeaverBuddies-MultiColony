# Separate colonies (alpha)

Two (up to four) players on one map, each running their own colony: their own districts, beavers, stock and, if
the host chooses, their own science and unlocks. It is co-op, not a race: nobody wins, there are no prices, and the
colonies help each other through **trading posts**.

**State of testing.** Alpha. Seen in a game with the land-split alphas (alpha1 to 5): hosting and joining over
Steam, founding a second colony and building in it. **Nothing of this version's model has been seen in a game
yet**: district ownership, the trading post and its panel, gifts, separate science and the road-network checks are
covered by automated checks only. Play on a copy of your save and keep backups.

## The rules in one minute

- **Build anywhere.** There is no border and no land of your own.
- **A colony is its districts.** Every district center belongs to a player. Buildings belong to the district they
  are in, beavers to the district they live in, goods to the district that stores them.
- **Roads never join, except through a District Crossing.** The game already refuses a path or building that would
  join two districts' roads; a crossing is the only link. A crossing between two players' districts is a **trading
  post**.
- **You change your own colony.** Settings, priorities, workers, demolition, migration and distribution of another
  player's districts are refused while that player is playing. While they are away, you may look after their
  colony too: both colonies keep running.
- **Shared by everyone:** game speed and pause, working hours, pings, chat, saving, and the map itself: water,
  droughts, badwater tides, trees and berries. Trees and crops near another colony are contested.

## Starting

**Host settings** (Mod Settings → BeaverBuddies), read when hosting starts:

| Setting | Default | What it does |
|---|---|---|
| **Separate colonies (alpha)** | on | Players run their own colonies. Off: one shared colony, exactly as in the Stability Fork. |
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
2. Place it anywhere its entrance is not on another colony's roads. It is free, needs no science, and appears
   **already built**, yours, with starting beavers, food and water (the new game's, or the Normal difficulty's for a
   save that did not record them).
3. Founding in a shared game turns it into a separate-colonies game. The existing districts stay the host's, and
   their imports are closed, like every new district's (see *Trading*).

## Trading posts

Build a **District Crossing** where your roads come close to another colony's: one half on each side, each half
reached by its own colony's road. In a separate-colonies game a crossing needs **no science and costs 10 logs**, so
colonies can trade from the start.

- **Each side is run by its own colony.** Your beavers bring goods to your half and take goods from it; the other
  colony's beavers do the same on theirs. No beaver crosses.
- **What moves is decided by each colony's own Distribution settings** (the **Distribution** tab, F8, or the button
  on the crossing's panel), exactly as between two districts in the game:
  - **Import** (the receiving colony's choice): *Disabled*, *Auto* or *Forced*.
  - **Export threshold** (the giving colony's choice): goods only leave above this fill level; at the top of the
    slider nothing leaves.
- **Every good starts at import *Disabled*** in a separate-colonies game, so nothing moves until the receiving player
  opens a good. This also applies between your own districts: open imports there too if you want goods to flow.

**The trading-post panel**, under the crossing's own panel when the crossing joins two colonies, shows:

- the two colonies, in their colours;
- **what the other colony could use**: the goods they import, least stocked first, then goods they store but have
  little of;
- **Give 10** beside each: your half's workers bring 10 of that good across, whatever their import settings, until 10
  have passed. They will not send it straight back while the gift is under way;
- **Sent to / Received from**: totals of what has passed each way through all trading posts between the two colonies;
- with separate science, **Give 50 / Give 250 science**.

Either player may remove a crossing (deleting one half removes both, as in the game). Running a half (workers,
priority) stays with its colony.

**Beavers** never migrate between colonies on their own. You may send beavers to another colony by hand (the
Migration tab): it is how a failing colony is rescued. You cannot take beavers from another colony's district.

## Separate science and unlocks

With the setting on, when a separate-colonies game begins:

- **Each colony has its own science.** Inventors, the Numbercruncher and the observatory add to their own colony's
  science; a relic's reward goes to the colony of the beaver who demolished it; the Iron Teeth control tower uses its
  own colony's. The top bar shows yours.
- **Each colony unlocks its own buildings.** Unlocking costs your colony's science and unlocks the building on your
  toolbar only. Placing a building checks that your colony has it unlocked.
- A shared game being split: its science and unlocks so far stay with the first colony; new colonies start with
  none. A new game: every colony starts with the same unlocks.
- **Bot worker types** stay unlocked for everyone; whoever unlocks one pays for it.
- You may place a building you have unlocked beside another colony's road: it joins their district and their
  beavers run it. That is allowed on purpose ("I'll build you a gear workshop").

## What you see

In a co-op session each player's interface shows **their own colony only**: the top bar's goods, population,
housing, workplaces, wellbeing and science; the batch control window's lists (F1 to F10, opening on your biggest
district); alerts; the notification journal. Selecting another colony's building opens its panels but leaves your
figures alone. Still whole-map: the *Global* history graphs in F9/F10 and in a good's tooltip, which the game
records for the whole map.

## Road networks

The game refuses to place a road, building or tubeway that would join two districts' roads, so two colonies' roads
can only meet at a crossing. Two gaps are closed by this mod:

- **Zipline links** are judged once, by the host, with the game's own check.
- **Two placements that are each fine alone** can join roads once both are built (two players placing at the same
  moment). Every computer notices at the same moment and warns both players ("Two districts' roads have been joined
  without a District Crossing"): remove the joining path or building. The game's district bookkeeping cannot handle
  joined roads, so do this at once.

## Water and the map

Water is one shared simulation: a dam upstream changes what flows to a colony downstream, and droughts and badwater
tides come to everyone at once. Stored water (tanks) is a good like any other and belongs to its colony. Prefer maps
with a water source near each start.

## Testing alone (debug)

With **Always Use Detailed Logging** on (the mod's debug mode), the host can press **Ctrl+Shift+K** to make their own
actions count as the next colony's (colony 1 → 2 → 3 → 4 → 1). The toolbar, science and every refusal follow. Every
switch is logged.

## Known limits

- Up to four colonies; more players join as helpers of the host's colony.
- The game ends only when every beaver on the map is gone, not per colony.
- Beavers working by range (lumberjacks, gatherers) can work on anything they reach, including another colony's
  marked trees.
- Renaming is shared: anyone can rename anything.
- A trading post buffers at most 30 of a good; a gift stalls while the receiving colony has nowhere to put it.
- Gifts of a good also count vanilla trades of that good through the same crossing.
- The *Global* history graphs (F9, F10, good tooltips) cover the whole map.
- Separate science is meant for co-op: in single player, science earned by another start's buildings goes to that
  colony's pool, which only its player can spend.
- A guest's planting tools follow the first colony's unlocks (the game builds that list before the guest is seated).
- A player who leaves the session still counts as playing until the host rehosts.
- Placing is judged by the placing player's own tool: the check against joining two districts' roads is not repeated
  in the multiplayer replay, where it read state that differs between computers.
- Two colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest from the
  connection panel.

## How it works

- **The host decides.** Every action goes through the host. The host writes which connection it came from (a guest
  cannot claim another), seats players by their stable id, writes the actor's colony into the action, checks it
  against the rules just before playing it, and plays and forwards it, keeps only the actor's part of a list action,
  or drops it (logged as `[Colony] Refused …`). Guests never judge, so the computers cannot disagree.
- **Ownership is saved on the district centers**; everything else follows from districts, which every computer
  simulates identically.
- **Science** keeps one pool and one unlock set per colony in the save. Simulation code that earns, spends or reads
  science names the colony of the building doing it; everything else is display.
- **The ledger** counts goods at the one place they pass from one half of a crossing to the other.

## Testing

Automated checks (all passing): **StabilityTests** (headless: the network stamping, player slots, the ownership
rules, founding, migration pairing) and **RuntimeChecks** against the compiled mod and the game's assemblies: every
action type declares what it touches, the shared actions are listed for review, and every game method or field the
mod hooks still exists. In-game test scripts: [ALPHA-TEST-SCRIPTS.md](ALPHA-TEST-SCRIPTS.md).
