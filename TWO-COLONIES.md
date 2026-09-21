# Separate colonies (alpha)

Two players, one map, a colony each. On a map with two starting locations, each player gets their own starting
district, their own beavers and their own land. On any other map, including the game's standard maps, the host
starts as usual and the second player **founds** their colony wherever they like (see
[Standard maps](#standard-maps-founding-colony-2)). Neither player can act on the other's colony. The two colonies
can trade goods through District Crossings built on the border, and each player controls what their colony takes.

**State of testing.** This is an alpha and **nobody has played it yet**. The rules, the land division and the
network stamping are covered by automated checks (see [Testing](#testing)), and the build loads against the
game's own assemblies. Nothing in this document has been seen in a game: not the border display, the notices,
the crossing rule, the trade defaults or the migration rule. The two test scripts at the end are how that will
change. Expect problems, keep backups of your saves, and play on a copy.

## Switching it on

The host decides this when **creating a new game**. Nothing changes for existing saves or for shared-colony games.

1. In **Mod Settings → BeaverBuddies**, tick **Separate colonies for new multi-start games (alpha)**.
2. Choose **Colony the host plays**: *Colony 1* or *Colony 2*. Every guest plays the other one.
3. Start a **new game**. On a map with two starting locations (a BeaverBuddies multi-start map) both colonies
   start at once. On a map with one start (any standard map) the host's colony starts as usual and colony 2 is
   founded by its player after joining. The mode is recorded in the save.
4. Save it, then host it as usual (**Load Game** → select the save → **Host co-op game**) and have your friend join
   before you unpause.

Colony 1 is the map's first starting location and colony 2 the second, in the order the map author numbered them.
The connection panel shows each player's colony beside their name.

**Rehosting.** Seats are not stored in the save; the host's setting chooses them each time. When you rehost, keep
**Colony the host plays** on the colony you played. When your friend hosts the save instead, they choose the colony
they were playing.

## Standard maps: founding colony 2

On a map with one starting location there is only one district center at the start. It is the host's (colony 1).
Colony 2 does not exist until its player founds it:

1. The host starts the new game as usual, saves, hosts it and brings the friend in. The host may play in the
   meantime.
2. When the friend joins, a message offers to **place their district center**. (If they close it, **Ctrl+K** opens
   the same tool at any time.)
3. They place it anywhere on the map. It costs nothing, needs no science, and appears **already built**, with the
   same starting food, water, adults and children the new game gave colony 1.
4. From that moment the land is divided halfway between the two district centers, exactly as on a two-start map,
   and each player controls their own colony. Everyone sees *Colony 2 has been founded.*

**Where it may go:** every building colony 1 already has must stay on colony 1's side of the new border and off
the border strip, and the new district center must stand wholly inside colony 2. The preview turns red with *Too
close to the other colony's buildings* when that is not so. Founding early, before colony 1 spreads out, leaves
the most room.

**Until colony 2 is founded** its player can do nothing but found it (shared things such as speed and chat still
work); the host plays freely. The land is not divided yet, so there is no border to show.

**When it is founded**, colony 1's districts have their imports set to *Disabled* too, so trade starts closed on
both sides, as on a two-start map.

Colony 1 is measured from its district center. If colony 1 has built more than one district center by then, the
one with the lowest internal id is used; found colony 2 early to avoid surprises.

## Whose land is whose

The border is **one straight line along the map grid**, halfway between the two starting buildings (on a standard
map, colony 1's starting building and the founded district center). It runs north-south when the starts are further
apart east-west, and east-west otherwise; height is ignored and a tile exactly halfway is colony 1's. It is straight
because a District Crossing is three tiles wide and needs a straight piece of border: a slanted border between two
diagonal starts would have none. A building belongs to the colony
that owns the tile it stands on, and a district belongs to the colony that owns its District Center's tile.

The tiles on each side that touch the other colony's land are the **border strip**. Only a District Crossing may be
built on the strip, which keeps the two colonies' roads from ever touching. The strip applies at every height:
platforms, stairs and bridges over it are refused too.

Press **K** (rebindable under **Key bindings → BeaverBuddies → Show colony border**) to show the border strip, drawn
in each colony's colour (colony 1 blue, colony 2 orange). It also shows by itself while any tool is active,
such as building, planting, cutting or demolishing.

## What you can and cannot do

| On your own colony | On the other colony |
|---|---|
| Everything, as in normal co-op | Nothing that changes it: placing, demolishing, pausing, priorities, workers, recipes, stockpiles, floodgates, automation, zipline links, migration and distribution settings are all refused |
| | Looking is fine: you can select their buildings and open their panels |

- **Placing buildings:** the preview turns red with the reason when any part would stand on the other colony's
  land, or when anything other than a District Crossing would stand on the border strip.
- **Area tools** (tree cutting, planting, clearing resources, demolishing): drag across the border as you like;
  only the tiles and objects on your own land are marked.
- **Refused actions** show a short notice ("That belongs to the other colony."). The host checks every action
  from every player, so a refused action never reaches anyone's game.
- **Shared by both colonies:** game speed and pause, working hours, science and building unlocks, renaming,
  pings, chat and saving.

## Building a District Crossing together

In Timberborn a District Crossing is two halves placed back to back with one click. In this mode the pair must
**straddle the border**: one half on your side of the strip, the other half on the other colony's side, directly
behind yours. Either player can place it. Each half must stand wholly on one side, and the host only accepts the
half on the other side when your own half really stands behind it, so a lone half can never be pushed onto the
other colony's land.

1. Show the border (**K**). It is straight, so any three free, level tiles along it will do.
2. Choose the District Crossing and place it so that the two halves meet exactly at the border. The preview is red
   anywhere else.
3. Each half joins the district on its own side and is built by that colony's beavers. **Each player needs a path
   to the entrance of the half on their side**, or that half is never built.

Deleting either half deletes both, as in the game, so either player can remove a crossing.

## Trade

Goods move through a crossing by the game's own distribution rules, set per district and per good in the
**Distribution** tab of the batch control window (**F8**, or the button on the crossing's panel):

- **Import** (the receiving colony's choice): *Disabled*, *Auto* or *Forced*.
- **Export threshold** (the giving colony's choice): the game only sends goods above this fill level; at the top
  of the slider it sends nothing.

In a separate-colonies game **every good starts at import Disabled** in every district, so a new crossing moves
nothing. To receive a good, its player sets that good to *Auto* or *Forced* in their own district. To stop giving a
good, its player raises that good's export threshold. Each player can change only their own colony's districts.
The **Reset** button in the Distribution tab also sets import back to Disabled.

This also applies between two districts of the **same** colony: set imports there as well if you want goods to
move between your own districts.

## Beavers stay in their colony

Automatic migration never moves beavers between the two colonies, even when a crossing joins them; it still works
between districts of the same colony. Manual migration to or from the other colony's district is refused.

## Testing alone (debug)

With **Always Use Detailed Logging** on in Mod Settings (the mod's debug mode), a host can press **Ctrl+Shift+K** to make their own actions
count as the other colony's, and again to switch back. It lets one person test both sides of every rule. It works
only for the host, only in debug mode, and every switch is logged.

## Known limits

- Science, unlocks and working hours are shared.
- The game ends only when every beaver on the map is gone, not per colony.
- The top bar's numbers follow the selected district, as in the game.
- Tree-cutting, planting and demolition marks are shared lists: a lumberjack whose range reaches across the border
  will cut trees the other player marked on their side.
- Only two colonies are supported. On a map with more starts, only the first two become colonies; any other start's
  district belongs to whichever of the two is nearer. Every guest plays the same colony.
- Beavers do some work by range without anyone acting: a gatherer or lumberjack near the border can work on the
  other colony's side. The rules only cover what players do.
- Renaming is shared: either player can rename any building or beaver.
- The border is a straight north-south or east-west line halfway between the two starts, so starts that are diagonal
  from each other, or unevenly placed, give an uneven split.
- Two colonies mean more to simulate. Prefer a small map, and use the speed settings if the guest's frame rate drops.
- Outside a hosted session (single player) nothing is refused; the land division, trade defaults and migration
  rule still apply.

## How it works

- **The host decides.** Every action goes through the host before anyone plays it. The host writes on each action
  which connection it came from (a guest cannot claim to be someone else), checks it against the colony rules just
  before playing it, and either plays and forwards it, plays and forwards only the part on the player's own land,
  or drops it with a line in its log starting `[Colony] Refused`. Guests never judge, so they cannot disagree.
- **Land is a formula.** Ownership is worked out from the two start positions saved in the game, with whole
  numbers only, so every computer agrees without extra data.
- **Simulation rules** (no migration between colonies, trade starting closed) read only what is in the save and
  run identically on every computer.
- The mode saves one small entry in the save (whether it is on, and the start positions). A shared-colony game
  saves nothing new.

## Testing

Automated checks (all passing at `1.2.0-two-colony-alpha3`):

- **StabilityTests**, headless: 29 checks for this feature. They cover the host stamping each guest's actions over the
  real network code (a guest that claims another number is overwritten), grouped actions, seats, land division with
  2, 3 and 4 starts, the border strip (including that no gap is left on a diagonal border), placement,
  the crossing rule (including a lone half and a half across the border), area filtering, and the migration
  pairing rule, and founding (what colony 2's player may do before founding, where founding is allowed, and who
  may found). Grouped actions are tested in the JSON shape the mod really sends.
- **RuntimeChecks**, against the compiled mod and the game's assemblies: every one of the 50 action types declares
  what it touches (a new one that does not fails the check), the list of actions shared by both colonies is
  printed for review, the sender survives the trip through the network format, a real serialized group of
  actions is stamped all the way down, and every game method the mode replaces or founding uses still exists.

In-game test scripts are in the release notes of each alpha: a solo host script (one person, debug mode) and a
two-player script. Their results will be recorded here.
