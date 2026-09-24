# Separate colonies: the full rules

Up to four players on one map, each running their own colony: their own districts, beavers, stock, working hours
and, if the host chooses, their own science and unlocks. It's co-op, not a race. The colonies meet only at
**Trading Posts**, where they barter.

Installing, hosting and joining are in the [README](README.md), with what has been played so far at its top. This
page is the full set of rules.

## The rules in one minute

- **A colony is its districts.** Beavers belong to the district they live in, buildings to their district (or the
  colony that placed them), marks to the colony that made them. There is no land and no border.
- **Build, mark and plant anywhere**, right up to another colony's buildings and roads.
- **Two colonies' roads never join**, except through a Trading Post. (A District Crossing links your own districts,
  as in the game.)
- **You change only your own colony** (or one you look after), whether or not its neighbors are playing.
- **Your beavers work only for your colony.**
- **Shared by everyone:** game speed and pause, pings, chat, saving, and the map: water, droughts, badwater and
  weather.

## Starting a game

### The New Game page

The host chooses on the New Game difficulty page, under the game's **Tutorial** checkbox. The page remembers the
choice, and a game keeps it for good. The same boxes apply to a game started alone with **Start**.

| Checkbox | Default | What it does |
|---|---|---|
| **Separate colonies** | ticked | A colony each. Unticked: one [shared colony](#a-shared-colony-game). |
| **Separate science and unlocks** (under it, while ticked) | ticked | Each colony earns its own science and unlocks its own buildings. Unticked: one shared pool. |
| **Mixed factions** (under it, while ticked) | unticked | Each player picks Folktails or Iron Teeth (see [Mixed factions](#mixed-factions)). Greyed, with the reason, unless every faction is unlocked on the host's computer and no other faction mod is installed. |

### Who plays which colony

The save remembers each player by their Steam ID (without Steam, by an id kept on their computer). The first time
you join a save you get the next free colony; after that, always the same one, whoever hosts. With four colonies
taken, more players join as helpers of the host's colony. The connection panel shows each name with its colony.

### The Co-op Game room

Every co-op game starts in a **Co-op Game** room. The host opens it with **Host co-op game**:

- **A new game:** beside **Start** on the New Game page, then name the settlement.
- **A save:** right of **Load** in the Load game box (main menu, or Esc in a game). A gold line under the save's
  picture says what it is: *One shared colony*, or *Separate colonies* with how many players it remembers.
- **The game you're in:** in the game menu (Esc) while you play alone. The game is saved, and the room opens over it.

Friends join with **Join co-op game** (main menu, or Esc while playing alone), a Steam invite, or the host's IP
address (port 25565). The room lists everyone, host first, then guests in the order they came, each **Ready** or
**Not ready**. The host can remove a guest. **Start Game** asks first if someone isn't ready or nobody came.

**In a game, the room is a window over it.** The game pauses underneath. **Cancel** (host) or **Leave** (guest)
returns you to your game as it was. At **Start Game**, a game of your own gets its usual exit save first, unless it
was just saved for the room.

**At Start Game:**

- Joining closes. To let someone in later, the host uses **Save and Rehost**.
- Everyone loads the same world, paused. The connection panel shows *(loading)* beside a guest until they're in.
- On a multi-start map, each player gets a start in room order, as many as there are players (at most the map's
  **Max Starting Locations**, and four). Anyone beyond founds a colony; past four colonies, players are helpers.

**Save and Rehost.** In a co-op game, the host's game menu has **Save and Rehost** instead of Host co-op game. The
game is saved and its room opens; connected guests come along by themselves, the room opening over their game. A
guest who missed it sees *The multiplayer connection was lost* and chooses **Rejoin**, which waits in the game and
joins the room when it opens. After a desync, guests choose **Reconnect (wait for Rehost)**, which does the same.

A guest's game has no hosting buttons: its copy may be out of step.

### Founding your colony

In a new separate-colonies game on a standard map, the district centers already there are the host's colony. In a
save, every colony keeps its owner, and a returning player gets theirs back. A player without a colony founds one:

1. As soon as you're in, even while the game is paused, a message offers a district center: **Place district
   center**. If you cancel, press **Ctrl+K** when you're ready.
2. Place it anywhere its roads won't join another colony's (their roads show in their colors).
3. It appears **already built**, free, with the starting beavers and supplies of the game's difficulty.

### Making a shared save separate

A shared save (made with **Separate colonies** unticked, or in the Stability Fork) shows **Separate colonies** in
its room, unticked. Tick it (and, under it, **Separate science and unlocks** if you like) and, at Start, the game
becomes separate colonies for good:

- Everything built so far, and every planting and cutting mark, becomes the host's colony.
- Each other player founds their own colony.
- With **Separate science and unlocks** ticked, each colony earns its own science from then on; what was earned so
  far stays with the host's colony. Unticked, the colonies share one pool.

Keep a copy of the save first.

### Splitting a shared game later

In a shared game, a player other than the host can choose Esc → **Found your own colony**. It asks first, since it
can't be undone, then they place their district center. That placement makes the split: everything built so far,
marks included, stays the host's colony, and science stays one pool. Other players who played the shared colony
then found their own (Ctrl+K), or look after the host's.

## Mixed factions

With **Mixed factions** ticked, each colony plays its own faction: one player can run Folktails and another Iron
Teeth on the same map. It's made for the game's two factions; with a faction mod installed, a new game stays one
faction.

**Picking your faction.**

- In the Co-op Game room, each player picks with the game's own faction switcher. Each row shows that player's
  faction.
- On a multi-start map, each start is placed in its player's faction.
- On a standard map, your founding places your picked faction's district center; **Another faction** changes it.
- A player who didn't pick chooses when founding: one card per faction.
- In a mixed save's room, only a player whose colony has no faction yet picks one.

**Which factions.** The host's unlocks count: Iron Teeth is available if it's unlocked on the hosting computer.

**Changing your mind.** Until your colony builds anything of its own faction, the Ctrl+T window offers **Play …
instead** on your colony. Your district center and beavers switch in place, and your stock stays. Not while the
colony has an offer or exchange open at a Trading Post.

**Each colony its own faction:**

- **Toolbar:** your faction's buildings and crops, plus the common ones (paths, stairs). You can't place the other
  faction's buildings.
- **Beavers and bots** look like their faction and have its needs: an Iron Teeth bot needs Energy, a Folktails bot
  Biofuel. Any food stills hunger, but only a faction's own foods give their wellbeing bonus. A baby is born the
  faction of the house or pod it comes from.
- **Storage** holds your faction's goods and the common ones. Farmhouses and foresters plant your faction's crops and
  trees, and gatherers take what your faction uses.
- **What you see** follows your colony's faction: the wellbeing box, the goods lists, the game-over and Wonder
  screens. Tutorials are off in a mixed game.

**Trading between factions.** Goods cross only if the receiving faction can store them: the 17 goods both use (Log,
Plank, Treated Plank, Gear, Metal Block, Scrap Metal, Dirt, Pine Resin, Explosives, Fireworks, Berries, Water,
Badwater, Extract and the three bot parts), plus science. **Beavers never cross between factions.** The goods grid
lists only what may cross.

**Hand-overs.** A colony handed over goes to the nearest living colony of its own faction first. For absence, it goes
only to one of its own faction; with none, it waits. The receiver can run what it gets, but builds only its own
faction's buildings.

**Wonders.** Each colony builds its own faction's. A Wonder helps beavers of its faction in its range, whichever colony
they belong to. The first Wonder finished completes the map for everyone.

## A shared-colony game

With **Separate colonies** unticked (or a Stability Fork save), everyone builds one colony together, as in ordinary
BeaverBuddies. None of this page applies until someone [splits it](#splitting-a-shared-game-later): no owners and
no Trading Posts.

## Trading Posts

The **Trading Post** is in District Management, next to the District Crossing. It **costs 10 logs and needs no
science**, and appears only in separate-colonies games.

**Placing it.** Build it between two colonies' roads, one road end at each half's door. Either colony may place it,
before the roads or after; the placer's builders build it. It trades once each half is reached by a different
colony's road. Until then its panel says *Not trading yet*.

**Goods cross only through an exchange** both colonies agree to. Import and export settings don't apply, and the post
isn't a store: its halves hold goods only for the round under way. (The District Crossing stays the game's own, for
your own districts.)

An **exchange** is "this many of one thing for that many of another, so many times": for example *100 logs for 25
gears, 4 rounds*. You trade from **your half**, the one your roads reach.

1. **Offer.** Select your half. Set **You give** and **You get**: click an item for the goods grid, and set the amount
   from 0 to 100 (**−**/**+** by 10, Shift by 1; beavers by 1). Set **Rounds** (1 to 99), or tick **Repeat until
   cancelled**. A side at 0 is a gift, or a request for help. For more than one round, **Keep at least** holds back a
   reserve so a long deal never empties your stock. Then **Make offer**.
2. **Answer.** The other player selects their half and chooses **Accept** or **Decline**. Until then you can
   **Withdraw offer**. A declined or withdrawn offer stays in the form, ready to change.
3. **Each round.** Each colony's Trading Post workers bring its side to its own half, where it waits. Science and
   beavers aren't carried: their bar shows what the colony can give. If a round waits, the panel says why: paused or
   flooded, no workers, no room, or nothing left to bring.
4. **Crossing.** When both sides are in, the round crosses **at once**. Goods go to the other half, and that colony's
   workers haul them home. Science (with separate science) passes between pools. Adult beavers who are free to go
   (carrying nothing, not contaminated) move to the other colony, never its last adult. Nothing is given before what
   it was traded for is in.
5. **Done.** After the last round both players get a notice, and the post is free again.

**Ending early takes both players.** **Cancel exchange** asks the other player, who chooses **Agree to cancel** or
**Keep trading**. Then whatever waits on each half goes home; rounds that crossed stay crossed. If a road is removed,
the exchange pauses, and either colony can **End exchange** alone. An exchange also ends if a colony is handed over,
or if the post is removed.

**Offer again.** A line above the form offers the last exchange at this post again. Clicking a row in the post's
ledger puts that round's terms in the form.

**The Ctrl+T window** (also the **Trade** button at the top right, or **All Posts** on a post) lists:

- each of your posts, its exchange and round, with **Go to**;
- each colony: its population, whether its player is playing, its **food and water** in days, what it's **looking
  for**, and who looks after it.

**Looking for.** Set up to three items your colony would like to receive, on your colony's row in that window. Others
see them there, under the header of a post you share, and in the goods grid when choosing what to give you.

**Good to know:**

- One exchange at a time per post; build more posts for more.
- A half holds up to 100 of a good for the round under way. The next round waits until the last one's goods have
  been hauled away, so staff both halves and keep storage room.
- Heavy goods take many trips: give busy posts more workers (up to 10).
- Either of its two colonies may remove a post; no one else can. Its halves are placed as one.

## Looking after a colony

Going away for a while? Ask a friend to look after your colony.

- **Asking.** In the Ctrl+T window, on your colony: **Let … look after it** (one button per other player), and later
  **Take it back**. The host can ask a player to look after an away player's colony, and end that with **End
  stewardship**.
- **Running it.** Your friend presses **Run this colony**, and **Back to your colony** when done. While they run it,
  their toolbar, science and top bar are your colony's. You can both play it at once.
- **Kept.** A colony looked after by someone in the game isn't handed over for its player's absence.

## When a colony is handed over

A colony goes to another colony, with its buildings, marks, stock and science, when:

- **it has no beavers or bots left** for a whole day: to the nearest living colony (in a mixed game, its own
  faction's first);
- **its player has been away** longer than the host's limit, **Hand over a colony after its player is away
  (days)** in Timber Together's settings (**Mods** → the settings button beside it). The default is 0: never. It goes to the nearest colony whose player is playing
  (in a mixed game, only one of its own faction). Days the host plays alone, and the first day after loading, don't
  count;
- **the host hands it over**, from the Ctrl+T window: any colony whose player is away, or that has no beavers. Useful
  if a player's Steam account changed.

With a limit set, the Ctrl+T window shows how close it is (*missed 6 of 7 days*), and everyone is warned the day
before. A player who lost their colony can found a new one with **Ctrl+K**. The receiving colony can build whatever
the old one had unlocked.

## Separate science and unlocks

With **Separate science and unlocks** ticked:

- **Each colony earns its own science:** its inventors, Numbercruncher and observatory. A relic's reward goes to the
  colony that demolished it. The top bar shows yours.
- **Each colony unlocks its own buildings,** paid from its own science, on its own toolbar. Bot worker types too.
- A new game starts every colony with the same unlocks. A shared save made separate with this box ticked keeps
  what was earned with the host's colony, and new colonies start with none. A shared game split later keeps one
  pool.

## Keeping colonies apart

| Work | Whose |
|---|---|
| Building, demolishing | Only the owning colony (either partner can remove a Trading Post between them) |
| Cutting trees | Only trees your colony marked |
| Planting | Only on your colony's planting marks |
| Harvesting, gathering, scavenging | What grows on your marks, and wild things nobody marked (first come) |
| Recovered goods | Whoever gets there first |
| Log piles and other stacks | Whoever gets there first, except on another colony's marks, which are left to it |
| Working hours | Each colony's own |
| Automation | Only wired to your own colony's buildings |
| Names and building settings | Only the owner changes them, including other mods' settings (MixedStorage, for one) |
| Water buildings kept in step (floodgates, valves) | Only with your own colony's |
| Counters and indicator warnings | Your own colony's |
| Beavers without a district | Go to your colony's nearest district center, never another colony's |
| Migration | Only between your own districts. Beavers change colony only through a Trading Post. In the Migration tab (F7), another colony's controls are greyed out |

A tile marked by one colony can't be marked or unmarked by another. Unmarking an area removes only your own marks.

**What still reaches across:**

- **Water:** one shared world. A dam upstream changes what flows downstream, and droughts and badwater reach
  everyone. Prefer maps with water near each start.
- **Blasts** from dynamite and unstable cores destroy whatever they reach, any colony's.
- **Power:** two colonies' shafts that touch make one network.
- **Area effects:** decorations, monuments and Wonders help any beaver in range (in a mixed game, only their own
  faction's).

## What you see

In co-op, your interface shows **your own colony**: the top bar (goods, population, housing, workplaces, wellbeing,
science), the batch control lists (F1 to F10), alerts and the notification journal. You can open another colony's
buildings; your figures stay yours. The *Global* history graphs (F9, F10) still cover the whole map.

## The road rule

Two colonies' roads never join, except through a Trading Post. Anything else may stand anywhere. Refused, with *That
would join another colony's roads*:

- a path, stairs or anything that carries a road (bridges, gates, tubeways, zipline stations, district centers) on or
  beside another colony's road, built or not;
- a path in front of another colony's building's door;
- a building whose door opens onto or beside another colony's road.

A zipline tower can't be linked to another colony's tower. A district center, a Wonder, and tubeway and zipline
stations count as road on every side.

**Seeing the roads.** Every colony's roads show as bright squares in its color while you place a building, and any
time with **Ctrl+L**.

**Two placements at once** can still join two colonies' roads. Both players are warned (*Two districts' roads have
been joined…*), and the game keeps running: remove the joining path or building soon.

## Dev mode and testing alone

**Dev mode** (Alt+Shift+Z) in co-op: only its instant unlock, *Finish now* and *Add 1000 Science* are shared. Its other
tools desync the game, and its Ctrl placement keys are off. **Tick once** (the period key by default, while paused) is off in co-op.

**Testing alone:** with **Always Use Detailed Logging** on and nobody connected, the host can press **Ctrl+Shift+K** to
act as the next colony (1 → 2 → 3 → 4 → 1).

## Known limits

- Up to four colonies; more players help run the host's colony. The room holds up to seven guests.
- The game ends only when every beaver on the map is gone, not per colony.
- Mixed factions is for new games only. A mixed save needs Timber Together: without it, the game loads one faction and
  drops the other's buildings and beavers. Mixed games load both factions, so they use more memory.
- In a mixed game, both halves of a Trading Post show the placing colony's faction, and achievements follow the host's
  faction.
- The room has no chat or map preview. Anyone who can reach the direct-IP port can join it; the host can remove them.
- Trading needs a co-op session: host the game (even alone) to trade.
- A post's reserve counts your stock in that half's district, not your whole colony's.
- The days of food and water in the Ctrl+T window are an estimate from yesterday's use.
- **Automation** runs on the game's ticks in co-op: up to a tick later than alone. A spring-return lever gives a
  one-tick pulse, which sets off a Detonator.
- **The HTTP API** works in co-op: each computer runs its own, and switching an HTTP lever is that player's action
  (refused for another colony's lever). Lever colors set over HTTP are ignored.
- A light's color, a decal, a bell's sound and a stream gauge's marker are each player's own until a rehost.
- Demolishing a platform that holds up dirt leaves the dirt floating in co-op: remove the dirt first.
- Content mods must match: the host refuses a building, crop or recipe its game lacks, and a guest missing one the
  host used leaves the game.
- Other mods that change the simulation need the same version and settings on every computer; you only get a warning
  when the mod lists differ.
- After a Timberborn update that changes something this mod corrects, co-op stops at load with a message. Update
  Timber Together.
- **Performance:** more colonies mean more to simulate; prefer a smaller map. Many deletions in one tick (harvests,
  deaths, blasts) can slow a fast game. **Always Use Detailed Logging** is for small colonies only.
- A speed boost the computers can't keep up with leaves a guest a few seconds behind, so their actions show late.

How separate colonies are kept in step: [DEVELOPING.md](DEVELOPING.md#separate-colonies-under-the-hood).
