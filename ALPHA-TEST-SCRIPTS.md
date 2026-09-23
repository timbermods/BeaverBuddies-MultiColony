# Separate colonies: test scripts

For the 1.4.0 betas (separate colonies, trading posts and barter, colony handover). The scripts began with
the alphas; a label such as *(alpha13)* or *(beta2)* says which build a line was added for. Please report a
result for **every line**: *works*, *fails* (what you saw), or *not tried*. A screenshot helps for anything drawn on
screen (the trading-post panel, a notice, the connection panel, the toolbar). Send `Player.log` at the end
(`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`). Lines from this mode start with `[Colony]`.

Install: remove every other BeaverBuddies folder from `Documents\Timberborn\Mods` (including the Stability Fork),
copy in `BeaverBuddies-MultiColony`, enable **BeaverBuddies MultiColony (beta)**, restart the game. If another
BeaverBuddies is still enabled, the main menu names it (please check that message appears if you try it).

## Script A: host alone (about 30 minutes)

Setup: in **Mod Settings → BeaverBuddies** check **Separate colonies for new games (beta)** and **Separate science and unlocks per
colony (beta)** are ticked, and tick **Always Use Detailed Logging** (debug mode). Start a **new game** on a
**standard map**. Save, then **Load Game** → select the save → **Host co-op game**; start without anyone joining.

1. **Seats.** The log has `[Colony] Separate colonies switched on: new game with one start` (while creating the game)
   and, after hosting, `[Colony] The host plays slot 0`.
2. **Your colony.** Build paths and a few buildings near your start: nothing is refused.
3. **Act as colony 2.** Press **Ctrl+Shift+K**: notice *Debug: your actions now count as colony 2.* Select your
   (colony 1's) district center or a building and try pause, priority, workers, rename, demolish: each shows *That
   belongs to another colony.* and nothing changes.
4. **Found colony 2.** Press **Ctrl+K**: a district-center tool opens, and colony 1's roads show in its color. Move
   it so its door would open onto or beside one of colony 1's roads: red, *Your district center would join another
   colony's roads…* Place it anywhere else, even close to colony 1's buildings: a finished
   district center appears with starting food, water and beavers; notice *A new colony has been founded.*; the log has
   `[Colony] Slot 1 founded a colony at …`. Select one of the new beavers: its district is the new district.
5. **Each colony's screen.** As colony 2 (still flipped), the top bar shows colony 2's food, population and science
   (0 science). Press **Ctrl+Shift+K** three more times (back to colony 1): the top bar shows colony 1's.
6. **No land (beta15).** As colony 1, place buildings and mark trees and crops right next to colony 2's buildings:
   nothing is refused. As colony 1, lay a path up to colony 2's road: the tile beside it is red, *That would join
   another colony's roads…* A house of colony 1 with its door facing colony 2's road, right beside it: red; the same
   house turned round: placed. Drag the demolish tool over colony 2's crop fields: their planting marks stay.
7. **A trading post.** Open District Management: after the District Crossing there is a **Trading Post** (the
   crossing's model, its own icon; hover it: 10 logs, no science). The District Crossing shows the game's own price
   and science. Try to place a District Crossing with one half's door on colony 2's road: red, *That would join
   another colony's roads*. Place a **Trading Post** where the two colonies will meet, before either road is there:
   it is placed. Bring a road from each district to one half's door each (flip to colony 2 to build colony 2's road).
   Colony 1's builders build both halves; each colony's beavers staff their own half. Before colony 2's road reaches
   its half, select the post: *Not trading yet*, and why.
8. **The panel.** Select colony 1's half. The panel is titled **Trading Post** and describes a trading post; there
   is no *Imported goods* box beside it (no **Manage distribution**), no stock list and no *No goods in stock* box. At
   the bottom: *Trading with Colony 2* in its color with **All Posts**; a **You give** and a **You get** card, each
   with a good (the one that colony has most of), *You have …* / *Colony 2 has …*, **−**, an amount box and **+**; a
   **Rounds** card with **−**, a box and **+**, and **Repeat until cancelled**; a line reading the offer back; **Make
   offer**; the **Ledger** (*No round has crossed here yet*); and *Traded with Colony 2* (screenshot please: it should
   look like the game's own sections, readable and not cut off). Select colony 2's half: only *Colony 2's half* with
   **Select your half**, which selects colony 1's. Click a card's good: a box with the game's goods grid opens beside
   the panel (science and beavers first, each good with the colony's stock); pick one. Open it again and pick the
   other card's good: the two swap. Untick *Only what is in stock*: every good shows. Click outside the box, or press
   Esc: it closes (Esc leaves the panel open). **−** and **+** move the amount by 10 (Shift: by 1), never past 100.
   Type 150: the line turns red (*0 to 100 per round*) and **Make offer** grays out; so does 0 on both sides, and 0
   rounds. Tick **Repeat until cancelled**: the rounds box grays out. Typing digits in a box must not change the game
   speed or open any window. With the description shown and all worker slots in use on a small screen, the section
   scrolls instead of running off the bottom.
9. **No trade by settings.** In colony 2's district (F8) set logs to import *Forced*: nothing crosses.
10. **An exchange.** As colony 1, offer **100 logs for 20 of** a good colony 2 has (e.g. planks), **3 rounds**: the
    line reads *3 rounds: 300 Logs for 60 Planks in all*; then *Your offer, waiting for Colony 2*, with **Withdraw
    offer**. Flip to colony 2 (Ctrl+Shift+K) and select its half: *Colony 1 offers an exchange*: *You get 100 Logs*,
    *You give 20 Planks* (with how many colony 2 has), *3 rounds…*, and **Accept** and **Decline**. Accept. Colony 1's
    workers carry logs to **their own half** and colony 2's carry planks to theirs; the goods **stay there**: nothing
    crosses yet. Each side's bar fills as its goods arrive (*on your half* / *on their half*), and the line under them
    says what the round waits for. When both bars are full, both loads cross at once (within a moment), each colony
    hauls away what arrived, the **Ledger** gets a line (*cycle-day, gave 100 logs, got 20 planks*) and round 2 of 3
    starts. After round 3: *The exchange … is complete*; the post is free again. Then try a **gift** (ask 0).
11. **Ending it takes both.** Start another exchange and, half way through a round, press **Cancel exchange** as colony
    1: *You asked Colony 2 to end this exchange*, and nothing more is brought. Flip to colony 2: *Colony 1 asks to end
    this exchange* with **Agree to cancel** and **Keep trading**. Press **Keep trading**: the round goes on. Ask again
    and **Agree to cancel**: the exchange ends, and each colony's workers carry what waited on their half back into
    their own storage (the top bar's count comes back). Rounds that crossed stay in the ledger.
12. **Science.** Build an inventor in each colony. Each colony's science rises only from its own inventor. As colony
    1, unlock a building: colony 1's science drops, and after flipping to colony 2 that building is still locked on
    the toolbar. There is no science gift any more: science is traded as an exchange item (line 14).
13. **Kept apart.** In the Migration tab (F7), try to send beavers to the other colony's district: refused. Set
    working hours differently in each colony (flip with Ctrl+Shift+K): each colony's beavers stop at their own hour, and
    the clock's needle follows the colony you are acting as. Near the trading post, mark trees as colony 1 and build a
    lumberjack flag for each colony close by: only colony 1's lumberjacks cut them. Place a building as colony 1 near
    the post: only colony 1's builders bring its logs.
14. **More trading.** Offer 20 of a good for 10 of another with **Repeat until cancelled** ticked, accept: after the
    first round crosses, round 2 starts by itself (*round 2, repeating*). Offer **50 science** for 30 berries: the
    science bar shows how much colony 1 can spare, and the science moves between the top bars' pools **only when the
    berries are in**, not before. Offer **1 beaver** for 30 berries: the beaver moves to the other colony only when the
    berries are in, and colony 2's notification journal (bottom left) says *… joined the colony from Colony 1 through a
    Trading Post*. Press **Ctrl+T**, the square **Trade** button at the top right, or **All Posts**: a box like the
    game's own (a title badge, a red close button) lists every trading post of your colony with its exchange, round and
    **Go to**, and both colonies with their population; the game keeps running behind it. Close it with its close
    button, then open it and press Esc, then Ctrl+T: each closes it. Drag it by its title to one side, close it and
    open it again: it opens where it was left. Press **Ctrl+L**: each colony's roads show in its color, and hide
    again.
15. **Two colonies' roads joined.** As colony 1, lay a path, then as colony 2 try a path on the tile beside its end:
    red, *That would join another colony's roads…*, also while colony 1's path is still a construction site. If you manage to join the two roads anyway (place both at the same moment, each
    fine alone), the game **keeps running** and the notice *Two districts' roads are joined…* shows; remove one
    path and it goes.
16. **A Trading Post between one colony's roads.** Place a Trading Post with colony 1's road at both ends: it is
    placed and built, and its panel says *Not trading yet*; nothing crosses it. Then remove a post that trades between colonies 1 and
    2 while flipped to a colony 3 (found one, or flip past 2): refused; as colony 1 or 2: allowed.
17. **Gates.** Place a gate on one of your paths and close and open it (by hand, then with a switch): it opens within a
    moment and never shows the conflict mark. While it opens, hover a path preview across it with the tool: the
    hovering must not stop it. Then place a gate that would join colony 1's and colony 2's roads: it stays shut with
    the conflict mark.
18. **Automation.** A switch wired to pause a building: the building pauses within a moment of the flip. Copy
    settings from colony 2's building onto colony 1's (or place a copy of it): refused with a notice, or the copy is
    placed without its settings.
19. **The District Crossing panel.** Between two of colony 1's districts, with import settings set, keep a crossing's
    panel open for a few minutes of hauling: the workers keep exporting as before.
20. **Handover.** As colony 1, open **Ctrl+T**: colony 2 (nobody plays it in this session) has **Hand to Colony 1**.
    Click it: colony 2's buildings and stock are colony 1's (select one of them), and after flipping to colony 2
    (Ctrl+Shift+K) you can press Ctrl+K to found again. A colony with no beavers left is handed over by itself a day
    later (if you can, let one starve and check).
21. **Save, reload, host again:** owners, marks, working hours, science pools, unlocks, the totals, the
    ledger and an exchange under way (with the goods waiting on each half, which nobody carries off after loading) or
    an offer waiting are unchanged. The day's `[Colony] Check day N tick T:` line in the log ends in `digest=…/0`
    right after a load (the count starts again from zero).
22. **Mode off (beta7).** Untick **Separate colonies for new games** and start a new game: one shared colony, no
    refusals, no Trading Post in District Management (turn dev mode on with Alt+Shift+Z: still none), District
    Crossings trade by import settings as in the game and hold 30 of a good, science is one pool. Host it and play a
    few minutes: the log says `[Colony] Hosting; founding a colony in a shared game off` and has no
    `[Colony] Check day` lines. Save: the save (a zip) has no `BeaverBuddies.` entry in its `world.json`.
22a. **The start prompt (beta2).** Host a save and, while still paused at the start, place a path: a dialog asks
    whether to start the game or keep waiting. **Keep waiting**: nothing is placed, the notice says players can still
    join, and the connection panel still says *Joining: open*. Place again and choose **Start the game**: the path
    appears, the *Joining* line goes, and the log says joining closed.
22b. **Food and water, and wishes (beta2).** Open Ctrl+T: each colony's row shows the top bar's food and water icons
    with the stock and, from the second day on, the days it lasts (red under a day). On your colony, **Looking for:**
    press **+**: the goods grid opens beside the window, unticked; choose gears, then planks; chips appear; click a
    chip to change it; **Clear** drops them. As colony 2 (Ctrl+Shift+K), colony 1's row shows *Looking for:* with the
    icons, a Trading Post's panel shows *… is looking for:* under the header, and in the **You give** grid those
    goods' counts are yellow with *… is looking for this* in the tooltip.
22c. **Offer again and counter-offers (beta2).** After an exchange has run, the form shows *Last exchange here: …*
    with **Offer again**: the terms come back, rounds and all. Click a ledger row: that round's terms come into the
    form, one round. Make an offer as colony 1, switch to colony 2 and **Decline**: colony 2's form now holds the
    mirrored terms (what colony 2 would give and get); change a number and **Make offer**.
22d. **A reserve (beta2).** Offer 50 logs for 10 planks, **Repeat until cancelled**, and set **Keep at least** to
    more than colony 1 has, minus 50: accept as colony 2; colony 1's workers bring nothing and the status says the
    colony keeps N logs back; colony 2 sees *… keeps a reserve*. Lower the reserve on the running exchange (the box
    under **You give**, Enter or click away): the round goes on. Save and reload: the reserve and the last terms are
    still there.
22e. **Home (beta2).** Scroll far away and press **Home**: the camera returns to your biggest district center.
22f. **A crossing holds 30, a Trading Post 100 (beta7).** Back in the separate-colonies game, a District Crossing
    between two of colony 1's districts shows room for 30 of a good in its inventory; a Trading Post half, 100.
22g. **Home in a shared game (beta7).** In step 22's shared game, press **Home**: the camera goes to the biggest
    district center.
23. Send `Player.log`.

## Script B: two players (about 45 minutes)

Both install the same zip. The host hosts a game as in Script A (debug mode not needed): a new standard-map game, an
existing save, or a new multi-start map game. The friend joins over a Steam invite before the host unpauses.

1. The friend is seated (log: `[Colony] Player 1 (…) plays slot 1`). The connection panel shows each name with its
   colony, e.g. *Alex (colony 2)*.
1a. **Seating checks the Steam connection (beta8).** The friend who joined over Steam is seated as above, and the host's log
    has no `[Colony] Refused PlayerHelloEvent` line. If a third player can, they join **by IP** (direct connection):
    also seated, with no refusal. After **Save and Rehost** (line 7) each gets their own colony again.
2. On a standard map or an existing save: once the host unpauses, the friend sees the offer to place a district
   center and founds their colony. (Before that, Ctrl+K says the game has not started yet.) The friend's notice is
   *A new colony has been founded.*; the host's is a plain notice, not a warning: *A new colony has been founded:
   Alex.* If the spot changed before it could be founded, only the friend is told to try again.
3. Each player tries Script A line 3 against the other's colony: refused. Then one player leaves; the other tries
   again: still refused.
4. Build a trading post between the two colonies and run an exchange (Script A lines 7 to 11, one player per side;
   the offer and the answer each come from a different player's screen).
5. Separate science (Script A line 12), one player per colony.
6. Play 30 minutes at your normal speed with trade running. No desync dialog. Note the guest's frame rate from the
   connection panel at the start and at the end.
7. **Save and Rehost.** Both players get the same colony again. Then **swap hosts**: the friend hosts the same save;
   each player still gets their own colony.
8. **Away.** The friend leaves while the host plays on (no rehost): from the next day the Ctrl+T window shows
   *player away (missed N days of hosted play)* and offers **Hand to …** at once. Wait the number of days in the
   host's setting (set it to 2 for this test; it can be changed during the game): the friend's colony is handed to
   the host's. Do this once with the host's **Always Use Detailed Logging** on: the hand-over must still happen
   (before alpha12 it never did with logging on). When the friend joins again (the host saves and rehosts), they get
   the notice and can found a new colony.
8a. **Every tick compared (alpha13).** Play a quarter of an hour at speed 1 to 3 with building, marking and an
    exchange, across a day change. Expect **no** desync dialog. If one appears and the log says `Colony state differs
    from the host's at tick …` (not `Random state mismatch`), that is this build's digest disagreeing, not your game:
    send both `Player.log` files; the two change counts in that line say which side counted one more, and each log's
    `Colony changes here as … desynced` list starts at the same change number `#n` on both computers (the first
    after the last check they agreed on): lined up, the first line that differs is the change they made differently.
    A list that says changes are *no longer kept* means more than 16384 were counted since; say so.
8b. **Refusals a guest sees (alpha12).** A guest who is not seated in a colony (a helper: join a game with every
    colony taken) changes the working hours: refused, and the panel goes back to the colony's hours. With the host's
    dev mode off, the guest Ctrl-clicks a locked building: the tool does **not** open; the host's refusal notice
    appears instead. With the host's dev mode on, the tool opens once the host answers.
8c. **Joined roads, two players (alpha12).** Colony 1 lays a path and pauses the moment it finishes; colony 2 places
    a path on the tile beside its end and resumes: refused (*That would join another colony's roads…*). Force a join (both place at the same moment): the game keeps running on both computers and
    both see the *roads joined* notice.
8d. **Gates, two players (alpha14).** One player opens a gate on their own roads while the other hovers a path
    preview across it: it opens on both screens within a tick. A gate that would join the two colonies' roads stays
    shut with the conflict mark on both.
8j. **Player colors (beta4).** With neither player having changed Ping Color: the host's cursor, selection outline,
    label and chat name are orange on the guest's screen, and the guest's are blue on the host's; a chat message is
    in the panel's normal color with only the name colored. Under Options, Player cursors, *Their color* shows the
    same color. One player sets a Ping Color: theirs changes everywhere, the other's does not. In the trading window
    and the road overlay (Ctrl+L) the colony colors are the colonies' own (brighter on the roads), not these.
8k. **Speed boost (beta5).** At speed 1, the host clicks **+** in the chat box's top row once: the row shows +0.5
    and *= 1.5x* on both computers, the game's speed buttons show *x1.5* on the last one, and the panel's *Speed*
    line says 1.5x. The guest types 2 in the box and presses Enter: both run at 3x. Pick the fastest button: 9x;
    note the tick rate on each computer and the guest's *Behind host* for a minute. Pause and unpause: the speed
    comes back at 9x, not more. Type something in the box and press Esc: nothing changes. Set the boost back to 0
    with **-** or by typing 0. Host again: it starts at 0.
8l. **Your own chat color (beta6).** Options, **Player cursors**: the first card, **You, in the chat**, shows your
    default swatch (orange for the host, blue for the guest, or your Ping Color if you changed it). Pick a preset:
    your name in the chat changes on your screen within a moment, and not on the other player's. Drag a slider:
    the same. **Reset**: back to the default. Restart the game and host again: the color is remembered. The other
    player's card still has Size and Transparency; yours does not.
8m. **A shared game stays shared (beta7).** The host leaves **Allow founding colonies in a shared game** unticked and
    hosts a shared save (made with **Separate colonies for new games** off, or a Stability Fork save). The friend
    joins; after the host unpauses no founding message appears, and **Ctrl+K** says *This is one shared colony*. Both
    build anywhere, next to each other's buildings, for a quarter of an hour at speed 1 to 3: no refusals, no
    desync. **Home** takes each player to the biggest district center.
8n. **Founding splits a shared game (beta7).** The host ticks **Allow founding colonies in a shared game** and
    hosts the same shared save again. After the host unpauses, the friend is offered to found a colony: with its door
    on or beside the shared colony's road the preview is red; anywhere else, even close by, it is placed. Both logs have
    `[Colony] Separate colonies switched on: slot 1 founded a colony in a shared game` and `[Colony] The shared
    colony's N buildings are colony 0's` with the same N. Play ten more minutes without a desync (the colony digest
    is compared from now on); save, reload and host again: both colonies keep their buildings.
8o. **Each journal is its colony's (beta8).** Wait until a beaver of the host's colony dies (old age, drowning, thirst) and a
    child grows up in it: the host's notification journal (bottom left) lists both, the friend's lists neither. Then
    the same the other way round. Save, reload and host again, the friend joins: once the friend's log says
    `[Colony] This computer plays slot 1`, the friend's journal holds only colony 2's entries and the host's only
    colony 1's (a dead beaver's body is gone after a day: its entry stays in its own colony's journal). While a
    beaver that died tragically lies there, its *died tragically* alert (top left) is only in its own colony's alert
    panel, and only that colony's row blinks (beta9).
8p. **A mod only the host has (beta9).** Only if you have MixedStorage (or another mod that sends its own actions):
    the host enables it, the friend does not (ignore the mod warning). The host changes a storage's allocation: the
    friend's game stops with *An action from the host could not be read … Nothing of that action was played here*,
    and the host's game **goes on** (no dialog; the friend leaves the connection panel). The host keeps playing a
    minute, then **Save and Rehost**; the friend enables the mod and joins again. The other way round (the friend has
    the mod, the host does not): the friend's allocation change is refused with *The host could not accept that
    action*, and nothing else the friend does is refused.
8q. **The fuller desync check (beta10).** Play a quarter of an hour at speed 3, then five minutes at speed 7, both
    players building and marking, with at least 100 beavers. Expect **no** desync dialog and, in neither log, an
    `Entity mismatch` or `Walker mismatch` line (logged once, the game goes on) or a `Random state mismatch: the
    first word agrees but the rest does not` line (that one stops the game). Any of them in a healthy game is a false
    alarm of the new check: send both `Player.log` files.
8r. **Reconnect after a desync (beta10).** Only if a desync happens: the host chooses **Save and Rehost**; a Steam
    guest chooses **Reconnect (wait for Rehost)** and joins the host's new lobby (with the host's **Allow Friends to
    Join Directly via Steam** on), or is told to accept a fresh invite; a direct-IP guest redials the address it
    typed.
8s. **Tick once while a guest waits (beta10).** On a guest, press the period key while the game runs at speed 7 and
    the guest briefly waits for the host (the connection panel's *Waiting for host*), or on a host easing off for a
    slow guest: the game pauses for everyone, as the pause button does, and no *Tick once is off* notice shows. Press
    it again while paused: the notice shows and nothing ticks.
8t. **The Earth Repopulator (beta12).** On an Iron Teeth map, with one player's frame rate capped at 15 (the game's
    or the graphics driver's limit) and the other uncapped, activate a finished Earth Repopulator with 8 pilots. All 8
    planes launch, the Wonder deactivates and the pilots disappear half an hour later, with no desync and no
    `Walker mismatch` line. Save and Rehost once while a plane is on the runway and once while the launcher turns.
    Activate a Folktails Earth Recultivator in co-op, and again right after it deactivates. With LateGamePerformance on
    both computers, repeat the launch with one player's camera turned away from the Wonder. The launch should look
    smooth at speed 1.
8u. **Deletions (beta12).** At speed 1 and at speed 7, let dynamite go off next to lumberjacks working, and demolish a
    building a builder is walking to. Pause, demolish something, and unpause at once. No `Entity mismatch` or `Walker
    mismatch` line in either log, and the game runs at its usual tick rate.
8v. **A host easing off (beta12).** At speed 7, the host sets **Ease off below** to 60 fps, so it eases for any guest below that.
    The other player's beavers should **not** stop and start at every tick while the connection panel shows
    *Easing off*; both run at the same, slower pace.
8w. **A direct-IP guest's link cut (beta12).** Three players, one joined by IP. That player turns off their Wi-Fi (or
    pulls the cable) during play: the host and the other guest play on without a freeze; about 30 seconds later the
    host's log says the player was dropped.
8x. **Spring-return levers (beta12).** In separate colonies, each player sets a lever to spring-return and presses it:
    it turns on, and off again a tick later, on both screens; the other player gets **no** refusal notice.
8y. **A building from a mod on one side only (beta12).** Only if you have a mod that adds a building: the friend has it,
    the host does not. The friend places it: refused with a notice, and the friend plays on. The other way round (the
    host has it): the host places it, the friend's game stops with *The host used the building …* and leaves; the host
    plays on.
8z. **The colony settings' tooltips (beta13).** In **Mod Settings → BeaverBuddies**, hover **Separate colonies for new
    games** and **Allow founding colonies in a shared game**: each tooltip is two lines, all on screen. From the
    tooltips alone, does the friend (who has not read this) know which one to use for a new game with a colony each,
    and which one to split a shared save? Report anything they would still get wrong.
8aa. **A Trading Post between two roads (beta15, beta16).** In separate colonies, the friend places a Trading Post
    where the two colonies will meet, before either road is there: it is placed (beta16). Each player then builds a
    road to one half's door: the post says *Not trading yet* until both are there; then an exchange works (Script A
    line 10). The friend's builders build both halves; each colony's beavers staff their own half.
8ab. **Build anywhere, roads never join (beta15).** Build right next to each other's buildings (houses, farms, a
    warehouse against the other's): no refusals. Try a path touching the other colony's path, a house whose door
    faces the other colony's road right beside it, and a path on the cell in front of the other colony's building's
    door: each is red, *That would join another colony's roads…* Press Ctrl+L: each colony's paths show in its color.
    Play ten minutes: no desync.
8ac. **Another mod's settings (beta16).** With MixedStorage on both computers, the friend selects the host's
    warehouse or pile and changes its goods in the MixedStorage menu: refused with *That belongs to another colony.*,
    and nothing changes on either screen. On the friend's own warehouse it works.
8ad. **The trading posts window (beta16).** Open it (Ctrl+T or **All Posts**) and drag it by its title to the side:
    it moves, and stays on screen however far you drag it. Close it and open it again: it opens where it was left.
    On a Trading Post, the **All Posts** button sits above the scrolling part, clear of its scroll bar.
8ae. **Whose roads are whose (beta16).** At night and by day, press **Ctrl+L**: every road cell of each colony shows as
    a bright square in its color (colony 1 red, colony 2 green-teal), easy to tell apart on grass and on the paths
    themselves; a district center's and stairs' squares lie on the ground, none float inside them. Pick a building:
    the squares show while it is in hand, and not with the planting or cutting tools. A screenshot helps.
8af. **Another colony's migration (beta17).** In the Migration tab (F7), choose the other colony's district in the
    window's district list: its automatic migration row (the minimum, − and +, and both toggles) is greyed out, and
    its toggles follow the other player's settings as they change them. In the manual migration panel, the 1, 10 and
    all buttons are greyed out whenever one of the two districts is the other colony's. Your own district's controls
    work as before.
8e. **The guest's smoothness (beta1).** Play ten minutes at speed 7 with 150 or more beavers, both players building
    and marking. On the guest, the beavers should not stand still for a moment at every tick (note it if they do, and
    at what speed it starts); in the diagnostics report (Ctrl+Shift+J) *Waited for the host at the start of a tick*
    should be rare, and under *Colony code since load* the *Working hours checks* line's average should be a few µs.
    The other player's cursor still follows their mouse, shows *Editing:* on a building they change, and disappears
    about three seconds after they quit. Note the guest's frame rate from the connection panel, as in line 6, to
    compare with alpha22.
8f. **Looking after a colony (beta2).** The host, on its own colony in Ctrl+T, presses **Let <friend> look after
    it**: both get a notice; the friend's window shows *You look after this colony* and **Run this colony**. The friend
    presses it: their connection panel row shows the host's colony number, their toolbar and top bar are the host's
    colony's, and a building they place is the host's colony's (the host can change it). **Back to your colony**
    restores their own. The host presses **Take it back**: the friend, if still running it, is put back to their own
    colony with a notice. Then the friend leaves while looking after the host's colony (grant it again first) and
    the host plays past the limit: the host's colony is **not** handed over while the friend is in the game; once the
    friend has left too, it is (from the next day's check).
8g. **The warning before a hand-over (beta2).** With the host's setting at 2 and the friend away, the day the window
    shows *missed 2 of 2 days* the host gets the warning notice; the next day the colony is handed over.
8h. **A guest's change while the host waits (beta2).** Host a save; the friend joins; before the host unpauses, the
    friend places a path: refused with *Not before the game starts: players can still join*. The host unpauses; the
    friend places again: fine.
8i. **Go to a player (beta2).** Click the friend's row in the connection panel: the camera jumps to their cursor;
    with their cursor over the interface, to what they have selected; with neither, a notice says so.
9. **Dev mode and Ctrl.** The host turns dev mode on (Alt+Shift+Z). Ctrl-click a locked building to unlock it, then
   place it with Ctrl still held; while the guest places paths, the host holds Ctrl+L; while the host holds Ctrl, a
   building finishes demolition. Every building must look the same on both screens (a construction site, not a
   finished building), and recovered goods appear on both.
9a. **Add 1000 Science (beta14).** The host keeps dev mode on, opens the dev panel and clicks *Add 1000 Science*,
    then places a locked building (Platforms, on water or not): the host's science reads 1000 less the building's
    cost, the building is unlocked and placed on both screens, and nobody's game stops. The friend turns dev mode on
    too and does the same: the science and the unlock are the friend's colony's alone. With the host's dev mode off,
    the friend's click is refused with a notice.
10. **A partial install.** The guest deletes `Buildings/DistrictManagement/MultiColonyTradingPost` from its mod folder
    and tries to join: the join is refused with a build mismatch message, not merely warned about. Put the folder back.
11. Both players press **Ctrl+Shift+J** before quitting, and diff the `day N tick T:` lines of the two reports: they
    must be the same (a difference now also stops the guest at once, at the turn of the day).
12. Send both `Player.log` files. If a desync happens, first compare the mod lists at the top of both logs, then look
    for the `Random state mismatch` or `Colony state differs` line. A `Colony state differs … at tick` line is the
    every-tick digest (alpha13); one `… on day` is the daily check.

## Script C: scale (an evening, three or four players)

Everyone installs the same zip; the host turns on **Always Use Detailed Logging** (the log then has one line a day
per colony: population, exchanges).

1. Everyone joins while the host waits paused; the host unpauses; then three or four players each found a colony on
   a medium map, and link each pair of neighbors with a trading post. Try it the wrong way round once: player 2
   presses Ctrl+K before the host unpauses (a notice says the game has not started); the host places a path while
   paused, then player 3 tries to join (refused: the game was changed).
1a. **Two join by IP at once (beta11).** With a save of a grown colony (a few MB), two guests press **Join co-op
    game** by IP within a few seconds of each other while the host waits paused. While both receive the save, the
    host's game stays responsive (the camera moves, menus open) and its connection panel keeps updating; both
    guests join. Before beta11 the host froze until the first guest's save had arrived.
1b. **High ping at a boosted speed (beta12).** A player far away (ping 150 ms or more) and a speed boost to 30: the
    connection panel's *Easing off* should come and go, not stay at a low percentage while every guest keeps up.
2. Play at least two hours at your usual speed, with repeating exchanges running and colonies growing to 100+
   beavers. Note the tick rate and each player's frame rate from the connection panel every half hour.
3. A player joins after the game has started (the host saves and hosts again with them). Then **swap hosts**.
4. A player leaves for the rest of the evening: from the next day their colony shows as away in Ctrl+T, and it is
   handed over after the set number of days, with the host's detailed logging on or off (only a host testing alone,
   with nobody connected, plays every colony).
4a. **A Trading Post between two others.** A third player tries to remove a post between colony 1 and colony 2:
    refused.
5. Send every `Player.log` and your notes on the tick rate and frame rates.

## Script D: the waiting room (two players, about 20 minutes) *(beta18)*

A new game where everyone joins before the world is made. **Not played yet**: every line is new. Compare the pages
with the mock-ups in `design/pre-game-lobby/` (in the repository); a screenshot of each page helps.

1. **The button.** Host: **New Game** → Folktails → a standard map → Normal. **Host co-op game** sits beside
   **Start** and looks like it. Choose **Customize**, set a starting value out of range: both buttons grey out.
   Set it back.
2. **The settlement's name.** **Host co-op game** shows the game's own "name your settlement" box, with **Cancel**
   and **Next**. Cancel goes back. A name already used shows the game's own message. Name it and go on.
3. **The host's page.** A **Co-op Game** page like the New Game pages: the banner and title, the faction logo and
   "Folktails - map - Normal" plate, the settlement's name in gold, **Players (1)** with your row (ticked, *Host ·
   Colony 1*, **Ready**), a yellow line *Invite friends, then start the game.*, **Invite Friends** (usable after a
   moment), the IP line, **Cancel** and **Start Game**. Screenshot it at your usual resolution and at the smallest
   window you use.
4. **Joining.** Guest, from the main menu: accept the invite. A *Connecting to …* box, then the **Kyler's Game** page:
   the same plate and players, your row marked *(you)*, **I'm ready** and **Leave**. The host's row for you shows
   *Joining…*, then your name, *Colony 2* and **Not ready**, within a second.
5. **Ready.** Guest: **I'm ready** (the button then reads **Not ready**), then untick your row's checkbox, then tick
   it: the host's row follows each time, and the host's line says who is not ready.
6. **Remove and leave.** Host: the red cross on the guest's row → confirm: the guest's page closes with *Kyler removed
   you from the waiting room.* Guest joins again by IP (**Join co-op game**). Guest: **Leave** → confirm: back to the
   main menu, and the host's list drops the row.
7. **Start with someone not ready.** Guest joins again and is not ready. Host: **Start Game** → *Not everyone is
   ready (…)* → **Keep waiting**: nothing happens. Guest: ready. Host: **Start Game**. Both see the loading screen
   with the mod's line (*Creating the world…*, *Loading your co-op game…*, *Loading Kyler's co-op game…*). The host
   never sees the new world before it.
8. **In the game.** Both paused at the start. The host has the district center. No *Joining: open*, and placing
   something as host asks nothing. The host's connection panel shows *(loading)* after the guest's name until the
   guest is in. The guest is offered **Place your district center** at once: place it **while still paused**. The
   log has `[Lobby] Guest 1 (…) seated in colony 2`. Unpause and play 5 minutes: no desync.
9. **A multi-start map.** Players field 4, two players: two starts are filled, the guest's is start 2, and nobody
   founds anything. (`[Lobby] Filling 2 start(s)` in the host's log.)
10. **Too late.** A third account accepts an old invite after **Start Game**: refused with "The host has already
    started the game…".
11. **Cancel.** Host opens a waiting room, the guest joins, host **Cancel** → confirm: the guest sees *Kyler closed
    the waiting room.* The host is back on the difficulty page; **Load Game → Host co-op game** on any save still
    works as before (the guest now sees a *Connecting* box instead of "Joined! Receiving map...").
12. **From a game.** Guest plays a solo game and accepts a waiting-room invite: a box says to go back to the main menu
    and accept it again; nothing else changes.
13. **Waiting.** Leave the guest on the page three minutes before Start: nothing drops. Pull the host's network
    cable for two minutes while the guest waits: the guest is asked *Keep waiting* / *Leave*.
15. **A save (beta19).** Host, from the main menu: **Load Game** → a separate-colonies save you both played →
    **Host co-op game**. The same page opens: the plate says the settlement, the gold line the save's name (an autosave
    reads *Autosave*) and its date as the Load Game box writes it; no faction logo, and no colony numbers on the rows.
    The guest joins from the main menu and readies up; **Start Game**: both load the save, paused, and each is in the
    colony the save remembers (`[Colony] Player … plays slot …`). **Cancel** instead goes back to the Load Game box.
15a. **From a game, the old way (beta19).** Host a save from Options → **Load Game** in a solo game, and use **Save
    and Rehost** after a desync: the old dialog shows (connected players, Invite Friends, Start Game), not the waiting
    room, and a guest can still join from a game.
15b. **A player new to the save (beta19).** A third player who never played the save joins its waiting room: in the
    game they take the next free colony (or found one), as before.
16. Send both `Player.log` files. Lines from the waiting room start with `[Lobby]`.

## Script F: Folktails and Iron Teeth together (two players, about 40 minutes) *(beta20)*

Mixed factions. **Not played yet**: every line is new. Both computers need the same mod version; the host needs
Iron Teeth unlocked (reach 15 average wellbeing with Folktails once, or dev mode's unlock). Screenshots of the
waiting room and of each colony help.

1. **The setting.** Host: Mod Settings → BeaverBuddies → **Mixed factions for new games (beta)** on (Separate
   colonies on). The tooltip fits on two lines.
2. **The room.** New Game → Folktails → a BeaverBuddies multi-start map with 3 starts → Normal → **Host co-op game**.
   The page shows the plate *map - Normal* (no faction), a gold line *Mixed factions: each player picks…*, then **Your
   faction** with the faction page's arrows, ring and name plate reading *Folktails*. Log: `[Factions]` nothing yet.
3. **Picking.** Guest joins from the main menu: the same switcher, showing Folktails. Guest presses the right arrow:
   *Iron Teeth*, the Iron Teeth logo in the ring. Within a second the host's row for the guest shows the Iron Teeth
   logo (hover: *Iron Teeth*). Host presses an arrow and back: the guest sees the host's row follow. After **Start
   Game** the arrows grey out.
4. **In the game.** Log (both): `[Factions] Mixed factions: on`, the catalog line (2 factions, 4 shared templates,
   bots 2, shaft parts 2, district centers 2, Trading Posts 2, goods shared by all: 17). Start 2 is an **Iron Teeth**
   district center with Iron Teeth beavers; start 1 Folktails. No error in `Player.log`.
5. **Each toolbar.** Host: Folktails buildings and crops only (paths and stairs too). Guest: Iron Teeth's. The top-left
   faction icon is each player's own. Ctrl+T: each colony has its faction's diamond.
6. **Build both.** Guest builds Iron Teeth housing, a farmhouse, a warehouse, a bot assembler and a charging station.
   The farmhouse lists Iron Teeth and common crops; the warehouse's good choice lists Iron Teeth and common goods. A
   bot is made: Iron Teeth model, needs panel with **Energy**, not Biofuel. Host does the same with Folktails
   buildings (a Folktails bot needs Biofuel).
7. **Refused.** Host, dev mode on (Alt+Shift+Z), places an Iron Teeth building from the dev toolbar: refused, *That
   building belongs to another faction.*
8. **Trading.** A Trading Post between the two colonies. Its header shows the other colony's faction diamond and the
   line *Between factions: goods both factions use, and science. No beavers.* The give grid lists only the 17 shared
   goods (+ Science with separate science), no Beavers, no Carrots. Offer 50 Logs for 10 Gears: it runs.
9. **Save, quit, load, rehost.** Everything as before: factions, fur, bot needs, models, toolbars. Desync checks stay
   green for three days.
10. **A standard map.** A new mixed room on a standard map: the guest picks Iron Teeth. In the game the guest's
    founding box says *Place your Iron Teeth colony's district center*; **Another faction** shows one card per
    faction. Found it: Iron Teeth district center and beavers.
11. **The switch.** A third colony (or Ctrl+Shift+K alone, debug) founds Folktails, then before building anything Ctrl+T
    → **Play Iron Teeth instead**: its district center and beavers become Iron Teeth in place. Build an Iron Teeth
    warehouse: the button is gone (a path alone would not count).
12. **The save's room.** Main menu → Load Game → that save → **Host co-op game**: the ring shows Folktails, the rows
    *Host · Colony 1* with the Folktails logo and the guest's *Colony 2* with the Iron Teeth logo; nobody's switcher
    shows (both colonies have their faction).
13. **Locked.** On a computer without Iron Teeth unlocked, Mixed factions on, a new room: the gold line says Iron
    Teeth is not unlocked and everyone plays one faction; no switcher.
14. Send both `Player.log` files. Lines start with `[Factions]` and `[Lobby]`.
