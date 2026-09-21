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
   (colony 1's) district center or a building and try pause, priority, workers, demolish: each shows *That belongs to
   another colony…* and nothing changes.
4. **Found colony 2.** Press **Ctrl+K**: a district-center tool opens. Point its entrance at one of colony 1's paths:
   red, *Too close: your district center would join another colony's roads.* Place it somewhere free: a finished
   district center appears with starting food, water and beavers; notice *A new colony has been founded.*; the log has
   `[Colony] Slot 1 founded a colony at …`. Select one of the new beavers: its district is the new district.
5. **Each colony's screen.** As colony 2 (still flipped), the top bar shows colony 2's food, population and science
   (0 science). Press **Ctrl+Shift+K** three more times (back to colony 1): the top bar shows colony 1's.
6. **Build anywhere.** As colony 1, place a building near colony 2's district center but not on its roads: allowed.
   Try a path that would join the two districts' roads: the game refuses it (red preview).
7. **A trading post.** Bring a road from each district close to the other and place a **District Crossing** between
   them (flip to colony 2 to build colony 2's road). It needs no science and costs 10 logs. Each colony's beavers
   build and staff their own half.
8. **The panel.** Select a half: under the crossing's panel, a **Trading post** section shows both colonies, what the
   other colony could use, **Give 10** buttons, *Sent to / Received from*, and **Give 50 / 250 science** (screenshot
   please).
9. **Trade by settings.** Every good in both districts starts at import *Disabled*; nothing crosses. As colony 2, set
   logs to *Auto* in colony 2's district (F8): logs move from colony 1 to colony 2; *Received from* on colony 2's side
   grows. As colony 1, try to change colony 2's import: refused.
10. **A gift.** As colony 1, press **Give 10** next to a good colony 2 does not import: *Gift under way: 10 more …*;
    10 of it cross, then the line disappears. They are not sent back.
11. **Science.** Build an inventor in each colony. Each colony's science rises only from its own inventor. As colony
    1, unlock a building: colony 1's science drops, and after flipping to colony 2 that building is still locked on
    the toolbar. **Give 50 science** to the other colony: yours drops by 50, theirs rises by 50.
12. **Migration.** In the Migration tab (F7), send beavers from your district to the other colony's: allowed. Let it
    run a few days: no beaver changes colony by itself.
13. **Save, reload, host again:** owners, science pools, unlocks, the ledger and any gift under way are unchanged.
14. **Mode off.** Untick **Separate colonies** and start a new game: one shared colony, no refusals, no trading-post
    panel, imports start at *Auto*, science is one pool.
15. Send `Player.log`.

## Script B: two players (about 45 minutes)

Both install the same zip. The host hosts a game as in Script A (debug mode not needed): a new standard-map game, an
existing save, or a new multi-start map game. The friend joins over a Steam invite before the host unpauses.

1. The friend is seated (log: `[Colony] Player 1 (…) plays slot 1`). The connection panel shows each name with its
   colony, e.g. *Alex (colony 2)*.
2. On a standard map or an existing save: the friend sees the offer to place a district center and founds their colony.
3. Each player tries Script A line 3 against the other's colony, while both are playing: refused.
4. Build a trading post between the two colonies (Script A lines 7 to 10, one player per side).
5. Separate science (Script A line 11), one player per colony.
6. Play 30 minutes at your normal speed with trade running. No desync dialog. Note the guest's frame rate from the
   connection panel at the start and at the end.
7. **Save and Rehost.** Both players get the same colony again. Then **swap hosts**: the friend hosts the same save;
   each player still gets their own colony.
8. Send both `Player.log` files. If a desync happens, first compare the mod lists at the top of both logs, then look
   for the `Random state mismatch` line.
