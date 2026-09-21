# Separate colonies: alpha test scripts

For the trading-exchange build (colonies as owned districts, trading posts, separate science). Please report a
result for **every line**: *works*, *fails* (what you saw), or *not tried*. A screenshot helps for anything drawn on
screen (the trading-post panel, a notice, the connection panel, the toolbar). Send `Player.log` at the end
(`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`). Lines from this mode start with `[Colony]`.

Install: remove every other BeaverBuddies folder from `Documents\Timberborn\Mods` (including the Stability Fork),
copy in `BeaverBuddies-MultiColony`, enable **BeaverBuddies - MultiColony (alpha)**, restart the game.

## Script A: host alone (about 30 minutes)

Setup: in **Mod Settings → BeaverBuddies** check **Separate colonies (alpha)** and **Separate science and unlocks per
colony (alpha)** are ticked, and tick **Always Use Detailed Logging** (debug mode). Start a **new game** on a
**standard map**. Save, then **Load Game** → select the save → **Host co-op game**; start without anyone joining.

1. **Seats.** The log has `[Colony] Separate colonies switched on: new game with one start` (while creating the game)
   and, after hosting, `[Colony] The host plays slot 0`.
2. **Your colony.** Build paths and a few buildings near your start: nothing is refused.
3. **Act as colony 2.** Press **Ctrl+Shift+K**: notice *Debug: your actions now count as colony 2.* Select your
   (colony 1's) district center or a building and try pause, priority, workers, rename, demolish: each shows *That
   belongs to another colony.* and nothing changes.
4. **Found colony 2.** Press **Ctrl+K**: a district-center tool opens. Move it within 10 tiles of colony 1's buildings:
   red, *That is another colony's land…* Place it somewhere free: a finished
   district center appears with starting food, water and beavers; notice *A new colony has been founded.*; the log has
   `[Colony] Slot 1 founded a colony at …`. Select one of the new beavers: its district is the new district.
5. **Each colony's screen.** As colony 2 (still flipped), the top bar shows colony 2's food, population and science
   (0 science). Press **Ctrl+Shift+K** three more times (back to colony 1): the top bar shows colony 1's.
6. **Land.** As colony 1, move a building's preview within 10 tiles of colony 2's buildings: red, *That is another
   colony's land…* Try to mark trees or plant there: nothing is marked (the log says the tiles belong to another
   colony). As colony 2, place and mark freely on colony 2's land. As colony 1, lay a path towards colony 2: it can go
   no further than the edge of colony 2's land. Drag the demolish tool over colony 2's crop fields: their planting
   marks stay.
7. **A trading post.** Bring a road from each district to the edge where the two lands meet and place a **District
   Crossing** across it, one half on each side (flip to colony 2 to build colony 2's road). It needs no science and costs 10 logs. Each colony's beavers
   build and staff their own half. Hover a half's inventory: each good holds up to **100**.
8. **The panel.** Select a half: at the bottom of the crossing's panel, a **Trading post** section shows both
   colonies, *No exchange here yet*, the offer form (*You give* / *You ask*, **<** / **>**, amount boxes, **Make
   offer**) and the totals each way (screenshot please). Typing digits in an amount box must not change the game
   speed or open any window.
9. **No trade by settings.** In colony 2's district (F8) set logs to import *Forced*: nothing crosses.
10. **An exchange.** As colony 1, offer **100 logs for 20 of** a good colony 2 has (e.g. planks): *Waiting for Colony 2
    to answer*. Flip to colony 2 (Ctrl+Shift+K): the panel shows *Colony 1 offers 100 Logs and asks 20 Planks in
    return*, with **Accept** and **Decline**. Accept. Colony 1's workers carry logs to their half, colony 2's carry
    planks to theirs, and each side hauls away what arrives. The progress lines rise **in step** (logs at most 10
    ahead of their share). At the end: *Exchange complete*; the post is free again. Then try a **gift** (ask 0) and
    **Cancel exchange** half way: nothing more crosses; a load already on the way stays on its own half and is carried
    home.
11. **Science.** Build an inventor in each colony. Each colony's science rises only from its own inventor. As colony
    1, unlock a building: colony 1's science drops, and after flipping to colony 2 that building is still locked on
    the toolbar. **Give 50 science** to the other colony: yours drops by 50, theirs rises by 50.
12. **Kept apart.** In the Migration tab (F7), try to send beavers to the other colony's district: refused. Set
    working hours differently in each colony (flip with Ctrl+Shift+K): each colony's beavers stop at their own hour, and
    the clock's needle follows the colony you are acting as. Near the trading post, mark trees on colony 1's land and
    build a lumberjack flag for each colony close by: only colony 1's lumberjacks cut them. Place a building on colony
    1's land near the post: only colony 1's builders bring its logs.
13. **Save, reload, host again:** owners, land, marks, working hours, science pools, unlocks, the totals and an
    exchange under way (or an offer waiting) are unchanged.
14. **Mode off.** Untick **Separate colonies** and start a new game: one shared colony, no refusals, no trading-post
    panel, crossings trade by import settings as in the game (holding up to 100), science is one pool.
15. Send `Player.log`.

## Script B: two players (about 45 minutes)

Both install the same zip. The host hosts a game as in Script A (debug mode not needed): a new standard-map game, an
existing save, or a new multi-start map game. The friend joins over a Steam invite before the host unpauses.

1. The friend is seated (log: `[Colony] Player 1 (…) plays slot 1`). The connection panel shows each name with its
   colony, e.g. *Alex (colony 2)*.
2. On a standard map or an existing save: the friend sees the offer to place a district center and founds their colony.
3. Each player tries Script A line 3 against the other's colony: refused. Then one player leaves; the other tries
   again: still refused.
4. Build a trading post between the two colonies and run an exchange (Script A lines 7 to 10, one player per side;
   the offer and the answer each come from a different player's screen).
5. Separate science (Script A line 11), one player per colony.
6. Play 30 minutes at your normal speed with trade running. No desync dialog. Note the guest's frame rate from the
   connection panel at the start and at the end.
7. **Save and Rehost.** Both players get the same colony again. Then **swap hosts**: the friend hosts the same save;
   each player still gets their own colony.
8. Send both `Player.log` files. If a desync happens, first compare the mod lists at the top of both logs, then look
   for the `Random state mismatch` line.
