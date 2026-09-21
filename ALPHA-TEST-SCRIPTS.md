# Separate colonies: alpha test scripts

For the 1.4.0 alpha (colonies with their own land, trading posts and barter, colony handover). Please report a
result for **every line**: *works*, *fails* (what you saw), or *not tried*. A screenshot helps for anything drawn on
screen (the trading-post panel, a notice, the connection panel, the toolbar). Send `Player.log` at the end
(`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`). Lines from this mode start with `[Colony]`.

Install: remove every other BeaverBuddies folder from `Documents\Timberborn\Mods` (including the Stability Fork),
copy in `BeaverBuddies-MultiColony`, enable **BeaverBuddies MultiColony (alpha)**, restart the game. If another
BeaverBuddies is still enabled, the main menu names it (please check that message appears if you try it).

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
4. **Found colony 2.** Press **Ctrl+K**: a district-center tool opens, and colony 1's land shows as an outline in its
   colour. Move it within 10 tiles of colony 1's buildings: red, *That is another colony's land…*; between 10 and 20
   tiles: red, *Too close to another colony…* Place it more than 20 tiles away: a finished
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
   build and staff their own half.
8. **The panel.** Select a half. The panel is titled **Trading Post** and describes a trading post; there is no
   *Imported goods* box beside it (no **Manage distribution**) and no stock list. At the bottom: *Trading with Colony
   2* in its colour with **All posts**; a **You give** and a **You get** card, each with a good (the one that colony
   has most of), *You have …* / *Colony 2 has …*, **−**, an amount box and **+**; a line reading the offer back;
   **Repeat until cancelled**; **Make offer**; and *Traded with Colony 2* (screenshot please: it should look like
   the game's own sections, readable and not cut off). Click a card's good: a box with the game's goods grid opens
   beside the panel (science and beavers first, each good with the colony's stock); pick one. Open it again and pick
   the other card's good: the two swap. Untick *Only what is in stock*: every good shows. Click outside the box, or
   press Esc: it closes (Esc leaves the panel open). **−** and **+** move the amount by 10, with Shift by 100. Set
   both amounts to 0: the line turns red and **Make offer** greys out. Typing digits in an amount box must not change
   the game speed or open any window. With the description shown and all worker slots in use on a small screen, the
   section scrolls instead of running off the bottom.
9. **No trade by settings.** In colony 2's district (F8) set logs to import *Forced*: nothing crosses.
10. **An exchange.** As colony 1, offer **100 logs for 20 of** a good colony 2 has (e.g. planks): *Your offer,
    waiting for Colony 2*, with **Withdraw offer**. Flip to colony 2 (Ctrl+Shift+K): the panel shows *Colony 1 offers
    an exchange*: *You get 100 Logs*, *You give 20 Planks* (with how many colony 2 has), and **Accept** and
    **Decline**. Accept. Colony 1's workers carry logs to their half, colony 2's carry planks to theirs, and each side
    hauls away what arrives. The two progress bars fill **in step** (logs at most 10 ahead of their share); *At this
    post* lists what waits on the half. At the end: *Exchange complete*; the post is free again. Then try a **gift** (ask 0) and
    **Cancel exchange** half way: nothing more crosses; a load already on the way stays on its own half and is carried
    home.
11. **Science.** Build an inventor in each colony. Each colony's science rises only from its own inventor. As colony
    1, unlock a building: colony 1's science drops, and after flipping to colony 2 that building is still locked on
    the toolbar. **Gift science 50** at the trading post: yours drops by 50, theirs rises by 50.
12. **Kept apart.** In the Migration tab (F7), try to send beavers to the other colony's district: refused. Set
    working hours differently in each colony (flip with Ctrl+Shift+K): each colony's beavers stop at their own hour, and
    the clock's needle follows the colony you are acting as. Near the trading post, mark trees on colony 1's land and
    build a lumberjack flag for each colony close by: only colony 1's lumberjacks cut them. Place a building on colony
    1's land near the post: only colony 1's builders bring its logs.
13. **More trading.** Offer 20 of a good for 10 of another with **Repeat until cancelled** ticked, accept: after it
    completes, round 2 starts by itself (*round 2, repeating*). Offer **50 science** for 10 logs, and **1 beaver** for
    30 berries: science moves between the top bars' pools, one adult beaver moves to the other colony. Press
    **Ctrl+T**: every trading post of your colony with its progress and **Go to**, and both colonies with their
    population. Press **Ctrl+L**: the land outlines show and hide.
14. **Handover.** As colony 1, open **Ctrl+T**: colony 2 (nobody plays it in this session) has **Hand to Colony 1**.
    Click it: colony 2's buildings, land and stock are colony 1's (select one of them), and after flipping to colony 2
    (Ctrl+Shift+K) you can press Ctrl+K to found again. A colony with no beavers left is handed over by itself a day
    later (if you can, let one starve and check).
15. **Save, reload, host again:** owners, land, marks, working hours, science pools, unlocks, the totals and an
    exchange under way (or an offer waiting) are unchanged.
16. **Mode off.** Untick **Separate colonies** and start a new game: one shared colony, no refusals, no trading-post
    panel, crossings trade by import settings as in the game (holding up to 100), science is one pool.
17. Send `Player.log`.

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
8. **Away.** The friend leaves; the host saves, then hosts again without them and plays on. The Ctrl+T window shows
   *player away (missed N days of hosted play)*. After the number of days in the host's setting (set it to 2 for this test), the friend's
   colony is handed to the host's. When the friend joins again, they get the notice and can found a new colony.
9. Send both `Player.log` files. If a desync happens, first compare the mod lists at the top of both logs, then look
   for the `Random state mismatch` line.

## Script C: scale (an evening, three or four players)

Everyone installs the same zip; the host turns on **Always Use Detailed Logging** (the log then has one line a day
per colony: population, land, exchanges).

1. Three or four players each found a colony on a medium map, and link each pair of neighbours with a trading post.
2. Play at least two hours at your usual speed, with repeating exchanges running and colonies growing to 100+
   beavers. Note the tick rate and each player's frame rate from the connection panel every half hour.
3. A player joins after the game has started (the host saves and hosts again with them). Then **swap hosts**.
4. A player leaves for the rest of the evening: check their colony is handed over after the set number of days.
5. Send every `Player.log` and your notes on the tick rate and frame rates.
