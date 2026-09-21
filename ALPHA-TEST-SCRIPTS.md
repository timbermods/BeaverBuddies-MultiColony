# Separate colonies: alpha test scripts

Two scripts for `1.2.0-two-colony-alpha5`. Please report a result for **every line**: *works*, *fails* (what you
saw), or *not tried*. A screenshot helps for anything drawn on screen (the border, a red preview, a notice, the
connection panel). Send `Player.log` at the end (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`).
Lines from this mode in the log start with `[Colony]`.

Install: remove every other BeaverBuddies folder from `Documents\Timberborn\Mods` (including the Stability Fork),
copy in `BeaverBuddies-MultiColony`, enable **BeaverBuddies - MultiColony (alpha)**, restart the game.

## Script A: host alone (about 20 minutes)

Setup: in **Mod Settings → BeaverBuddies** tick **Separate colonies (alpha)**, set
**Colony the host plays** to *Colony 1*, and tick **Always Use Detailed Logging** (debug mode). Start a **new game**
on a map with two starting locations. Save, then in **Load Game** select that save and choose **Host co-op game**; start without anyone joining.

1. **Two colonies.** Two starting District Centers, with beavers at each. The log has a line
   `[Colony] Separate colonies switched on for this new game: 2 colonies starting at ...`.
   Save, return to the menu, load the save and host it again: the log says `... loaded from the save` with the same
   two positions.
2. **Your land.** Place a path and a small building near your start (colony 1): they are placed. Point the same
   building at colony 2's land: the preview is red and says *You can only build on your own land.*
3. **The border.** Press **K**: a blue strip and an orange strip appear along the line between the starts (screenshot
   please). Press **K** again: gone. Pick any building tool: the strip shows while the tool is active. Try a path on
   the blue strip: red, *Only a District Crossing can be built on the border.* Try a platform or stairs over the strip
   (at a height): red too.
4. **Area tools.** Drag a tree-cutting area and a planting area across the border: only the tiles on your side are
   marked. Drag a demolition area across it: only your side's objects are marked.
5. **The other colony's building.** Select colony 2's District Center. Try pause, priority, the worker count and
   demolish: each shows *That belongs to the other colony.* and nothing changes.
6. **Acting as colony 2.** Press **Ctrl+Shift+K**: notice *Debug: your actions now count as colony 2.* The same
   actions now work on colony 2 and are refused on colony 1. Press it again to return to colony 1.
7. **A crossing.** Choose the District Crossing and place the pair so the halves meet exactly at the border (blue
   half on the blue strip, orange half on the orange strip): the preview is valid there and red anywhere else.
   Place it. Build a path from each colony's roads to the entrance of its half (flip with Ctrl+Shift+K to build
   colony 2's path). Both halves get built, each by its own colony's beavers, and the crossing does not show an
   invalid-connection status.
8. **Trade.** Open the **Distribution** tab (F8). In both districts every good shows import *Disabled*, and nothing
   crosses. As colony 2, set logs to *Auto* import in colony 2's district: logs move from colony 1 to colony 2 only.
   As colony 1, raise colony 1's export threshold for logs to the top: the flow stops. As colony 1, try to change
   colony 2's import setting: refused.
9. **Beavers stay.** Let it run 10 game days. The population of each district changes only by births and deaths.
   In the Migration tab, try to move beavers from colony 1's district to colony 2's: refused.
10. **Mode off.** Untick the separate-colonies setting and start a new multi-start game: it plays like 1.1.10
    (shared control, no border, no refusals, imports start at *Auto*).
11. Send `Player.log`.

## Script C: a standard map (host alone, about 15 minutes)

Setup as Script A (separate colonies on, *Colony 1*, debug on), but start a new game on a **standard map** (one
starting location). Save, then in **Load Game** select that save and choose **Host co-op game**, without anyone joining.

1. One district center with beavers, as usual. The log has `[Colony] Separate colonies switched on for this new
   game: one colony, waiting for colony 2 to be founded (it will start with N adults, ...)`.
2. Build a few paths and a building near your start (as colony 1: no refusals, no border).
3. Press **Ctrl+Shift+K** (act as colony 2). Try to place a path: refused, *Found your colony first (Ctrl+K).*
4. Press **Ctrl+K**: a district-center placement tool opens. Point it right next to your buildings: red, *Too close
   to the other colony's buildings.* Point it far away: valid. Place it.
5. A **finished** district center appears there with starting food and water and the same number of adults and
   children colony 1 started with; *Colony 2 has been founded.* The beavers walk around and start working. The log has
   `[Colony] Colony 2 founded at ...`. Select one of the new beavers: its district must be colony 2's new district, not
   colony 1's (screenshot please).
6. Press **K**: the border appears halfway between the two district centers. The Distribution tab shows import
   *Disabled* for both districts.
7. Press **Ctrl+K** again: *Colony 2 already exists in this game.* Save, reload, host again: still two colonies
   (the log says `loaded from the save` with both positions).
8. Script A lines 2 to 9 now apply to this game as well.
9. **An existing save.** Load any save made without this mode (or with an older build), host it, press
   **Ctrl+Shift+K** then **Ctrl+K**: the founding tool opens, and founding works as in lines 4 to 6. The new colony
   gets the Normal difficulty's starting beavers, food and water (9 adults, 4 children, 130 food). Before founding,
   acting as colony 2 is not refused (it is a shared game until then).

## Script D: each player sees their own colony (two players, about 10 minutes)

After colony 2 is founded (or on a two-start map), with nothing selected:

1. **Top bar food/water:** each player sees only their own colony's stock. Right after founding on a standard map
   both show the starting amounts (for example 130 food each), not the sum (260).
2. **Population:** each player's number counts their own beavers. Open **F1**: the list shows your own beavers only
   (different names on the two screens).
3. Select one of the **other** colony's buildings: its panel opens, but your top bar keeps showing your own figures.
   Select one of **your** buildings: the top bar shows that district, as in the game.
4. **F2, F3, F4, F7:** only your own homes, workplaces, storage and districts are listed.
5. Make a shortage in one colony only (for example no water): the alert shows for that colony's player only.
6. When a beaver is born or dies in one colony, only that colony's player gets the journal entry.

## Script B: two players (about 45 minutes)

Both install the same zip. The host sets **Colony the host plays** to *Colony 1* and starts a new separate-colonies
game as in Script A (debug mode not needed), on a two-start map **or on a standard map**. The friend joins over a
Steam invite before the host unpauses.

0. **Standard map only:** on joining, the friend sees the offer to place their district center. They place it away
   from the host's buildings; it appears finished with starting beavers, food and water, on both screens.

1. The connection panel shows each name with its colony, e.g. *Kyler (colony 1)*. Both see the border with **K**.
2. Each player does Script A lines 2 to 5 against the other player's colony, at the same time.
3. One player places a District Crossing straddling the border; each player builds the path to their own half.
   Both halves get built and link.
4. **Trade.** The guest sets one good to *Auto* import in their district: that good arrives from the host's colony.
   The host raises that good's export threshold to the top: the flow stops. Each player tries to change the other's
   distribution setting: refused.
5. **Shared things** work for both players: speed and pause, working hours, unlocking a building with the shared
   science.
6. Play 30 minutes at your normal speed. No desync dialog. Note the guest's frame rate from the connection panel
   at the start and at the end.
7. **Save and Rehost** with the same host and seat: the border, the crossing and the distribution settings are
   unchanged, and each player still controls only their own colony. Optional: the friend hosts the rehost save with
   **Colony the host plays** set to *Colony 2*, and both still control their own colony.
8. Both players send `Player.log`. If a desync happens, first compare the mod lists at the top of both logs, then
   look for the `Random state mismatch` line.
