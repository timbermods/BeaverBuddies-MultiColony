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
- **Roads never join, except through a District Crossing.** A crossing between two players' districts is a
  **trading post**, and it is the only place colonies meet.
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
2. Place it anywhere off another colony's land (more than 10 tiles from its buildings and paths). It is free,
   needs no science, and appears **already built**, yours, with starting beavers, food and water (the new game's, or
   the Normal difficulty's for a save that did not record them).
3. Founding in a shared game turns it into a separate-colonies game. The existing districts stay the host's.

## Trading posts

Build a **District Crossing** across the edge where your land meets another colony's: one half on each side, each
half reached by its own colony's road on its own land. Either colony may place it, as long as part of it is on the
placer's land (or free land); the placer's builders build both halves. In a separate-colonies game a crossing needs
**no science and costs 10 logs**, so colonies can trade from the start.

**Goods cross a trading post only through an exchange** agreed by the two colonies. Import and export settings (the
Distribution tab) do not move anything across a trading post; between one colony's own districts they work as in the
game.

**An exchange** is "this many of one good for that many of another", for example *1000 logs for 250 gears*:

1. **Offer.** Select either half of the crossing. The **Trading post** section at the bottom of its panel has the
   offer form: *You give* and *You ask*, each with a good (**<** and **>** step through the goods, those your colony,
   or theirs, has in stock first) and an amount from 0 to 9999. **Make offer.** One side may be 0: asking for 0 is a
   gift, giving 0 asks for help. The other player gets a notice.
2. **Answer.** The other colony's player selects the crossing and chooses **Accept** or **Decline**. The offering
   player may **Withdraw offer** until then. An offer changed in the meantime is never accepted by mistake.
3. **Delivery.** Each colony's crossing workers fetch their colony's side from its storage and bring it to their
   half; it passes to the other half at once, and the other colony's workers haul it away into their storage. No
   beaver crosses.
4. **In step.** Neither side may deliver more than a tenth of its amount (at least 10) ahead of what the other side
   has delivered, so an exchange is never filled one way only. The panel says when your beavers are waiting.
5. **Done.** When both amounts have crossed, both players get a notice and the trading post is free for the next
   exchange. **Cancel exchange** ends it early (either player); what has crossed stays crossed.

**Good to know:**

- One exchange at a time per trading post. Build more crossings for more exchanges at once.
- A half holds up to **100 of each good** waiting to be hauled away (30 in the game). What has not been hauled away
  holds up further deliveries of that good: staff both halves, and keep storage room for what you receive.
- Anything of an exchange's good that crosses counts towards it.

**The trading-post panel**, under the crossing's own panel when the crossing joins two colonies, shows the two
colonies in their colours, the exchange (the offer form, an offer waiting for an answer, or the progress of each
side), the totals delivered each way between the two colonies (all trading posts), and, with separate science,
**Give 50 / Give 250 science**. The crossing's own import icons and **Distribution** button belong to the game's
district-to-district trade, which does not apply at a trading post.

Either colony may remove a trading post (deleting one half removes both, as in the game); a crossing between one
colony's own districts is that colony's alone. Running a half (workers,
priority) stays with its colony.

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
- A colony's land is measured in a straight line on the map (10 tiles), not by walking; on a cliff edge it may
  reach a little further than its beavers do.
- Land is first come. A colony that founds right at the edge of another's land blocks its growth that way; talk
  before you found.
- In a save from before this version, buildings take their colony when they next have a district (a path: the
  district whose road it is); an older dam, levee or platform, which has none, counts only by the land it stands on.
- A half of a trading post holds at most 100 of a good; an exchange stalls while the receiving colony has nowhere to
  put what arrives.
- Trading happens in a co-op session: host the game (even alone) to trade.
- The *Global* history graphs (F9, F10, good tooltips) cover the whole map.
- Separate science is meant for co-op: in single player, science earned by another start's buildings goes to that
  colony's pool, which only its player can spend.
- A guest's planting tools follow the first colony's unlocks (the game builds that list before the guest is seated).
- A colony whose player never returns can't be run by anyone else (nobody may change it).
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
- **Exchanges** are saved on the two halves of the crossing. Goods are counted at the one place they pass from one
  half to the other, identically on every computer; the totals delivered each way are kept there too.

## Testing

Automated checks (all passing): **StabilityTests** (headless: the network stamping, player slots, the ownership
rules, founding, migration pairing) and **RuntimeChecks** against the compiled mod and the game's assemblies: every
action type declares what it touches, the shared actions are listed for review, and every game method or field the
mod hooks still exists. In-game test scripts: [ALPHA-TEST-SCRIPTS.md](ALPHA-TEST-SCRIPTS.md).
