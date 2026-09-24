# Separate colonies: test scripts

For the 1.4.0 betas and release candidates (separate colonies, trading posts and barter, colony handover, since
1.4.0-rc1 the late-game playtest, since 1.4.0-rc3 Script S: separate or shared, chosen on the New Game page, since
1.4.0-rc4 Script H: hosting a save, and hosting from a game, with 1.4.0-rc5's fixes marked *(rc5)*, and since
1.4.0-rc7 Script G: hosting and joining from inside a game, with the steps of older scripts it changed marked
*(rc7)*). The scripts began with the alphas; a label such as *(alpha13)* or *(beta2)* says which build a line was
added for. Please report a result for **every line**: *works*, *fails* (what you saw), or *not tried*. A screenshot
helps for anything drawn on screen (the trading-post panel, a notice, the connection panel, the toolbar). Send
`Player.log` at the end (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`). Lines from this mode
start with `[Colony]`.

Install: remove every other BeaverBuddies folder from `Documents\Timberborn\Mods` (including the Stability Fork),
copy in `BeaverBuddies-MultiColony`, enable **BeaverBuddies MultiColony (beta)**, restart the game. If another
BeaverBuddies is still enabled, the main menu names it (please check that message appears if you try it).

## Script A: host alone (about 30 minutes)

Setup: in **Mod Settings → BeaverBuddies** tick **Always Use Detailed Logging** (debug mode). Start a **new game** on a
**standard map**, with **Separate colonies** and **Separate science and unlocks** ticked on the difficulty page (under
Tutorial; since 1.4.0-rc3, a Mod Setting before). Save, then from the main menu **Host co-op game** → select the save →
**Host co-op game** → **Start Game** (yes to starting alone).

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
22. **Mode off (beta7).** Untick **Separate colonies** on the difficulty page and start a new game: one shared colony, no
    refusals, no Trading Post in District Management (turn dev mode on with Alt+Shift+Z: still none), District
    Crossings trade by import settings as in the game and hold 30 of a good, science is one pool. Host it and play a
    few minutes: the log says `[Colony] Hosting` and has no
    `[Colony] Check day` lines. Save: the save (a zip) has no `BeaverBuddies.` entry in its `world.json`.
22a. **The start prompt (beta2; retired in rc4).** Every game now starts from a waiting room, where joining closes at
    Start, so the host is never asked *Start the game?* any more: placing a path while paused just places it.
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
existing save, or a new multi-start map game. The friend joins its Co-op Game page over a Steam invite, before the host
presses Start Game (since rc4 nobody can join a started game).

1. The friend is seated (log: `[Colony] Player 1 (…) plays slot 1`). The connection panel shows each name with its
   colony, e.g. *Alex (colony 2)*.
1a. **Seating checks the Steam connection (beta8).** The friend who joined over Steam is seated as above, and the host's log
    has no `[Colony] Refused PlayerHelloEvent` line. If a third player can, they join **by IP** (direct connection):
    also seated, with no refusal. After **Save and Rehost** (line 7) each gets their own colony again.
2. On a standard map or an existing separate-colonies save: as soon as the friend is in (paused or not), they see the
   offer to place a district center and found their colony. The friend's notice is
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
8m. **A shared game stays shared (beta7, rc3).** The host hosts a shared save (made with **Separate colonies**
    unticked, or a Stability Fork save). The friend joins; after the host unpauses no founding message appears, the
    friend's **Ctrl+K** says *This game has one shared colony* and points to the game menu, and the host's says the
    shared colony is theirs. Both
    build anywhere, next to each other's buildings, for a quarter of an hour at speed 1 to 3: no refusals, no
    desync. **Home** takes each player to the biggest district center.
8n. **Founding splits a shared game (beta7, rc3).** The host hosts the same shared save again. After the host
    unpauses, the friend opens the game menu (Esc) → **Found your own colony** → **Found my colony**, and places their
    district center: with its door on or beside the shared colony's road the preview is red; anywhere else, even close
    by, it is placed. Both logs have
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
8r. **Reconnect after a desync (beta10, rc4).** Only if a desync happens: the host chooses **Save and Rehost** and the
    guest **Reconnect (wait for Rehost)**, in either order. Both go to the main menu; the guest waits (*Waiting for the
    host to host again…*) and lands on the host's Co-op Game page as soon as it opens: a Steam guest through the host's
    lobby (with the host's **Allow Friends to Join Directly via Steam** on) or its invite, a direct-IP guest by
    redialling the address it typed. Ready, Start Game.
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
8z. **The colony choices' tooltips (beta13, rc3).** On the New Game difficulty page, hover **Separate colonies** and
    the two checkboxes under it: each tooltip is on screen. From the tooltips alone, does the friend (who has not
    read this) know what unticking Separate colonies gives, and where a shared game is split later? Report anything
    they would still get wrong.
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
    colony with a notice. Then the friend leaves while looking after the host's colony (grant it again first) and,
    with the host's limit set to 2 (0, never, is the default since 1.4.0-rc2), the host plays past the limit: the host's colony is **not** handed over while the friend is in the game; once the
    friend has left too, it is (from the next day's check).
8g. **The warning before a hand-over (beta2).** With the host's setting at 2 and the friend away, the day the window
    shows *missed 2 of 2 days* the host gets the warning notice; the next day the colony is handed over.
8h. **A guest's change while the host waits (beta2; retired in rc4).** Every game starts from a room, closed at Start,
    so a guest's path while the game is still paused is simply placed. (A shared save made separate at Start is Script H
    step 4a.)
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

1. Everyone joins the host's Co-op Game page and readies up; **Start Game**; then three or four players each found a
   colony on a medium map, and link each pair of neighbors with a trading post. Once the game has started, a fifth
   player tries to join: refused (the game has started; the host must Save and Rehost).
1a. **Two join by IP at once (beta11, rc4).** With a save of a grown colony (a few MB) on its Co-op Game page, two
    guests press **Join co-op game** by IP within a few seconds of each other and ready up; **Start Game**. While both
    receive the save, the host's loading goes on and both guests load. Before beta11 the host froze until the first
    guest's save had arrived.
1b. **High ping at a boosted speed (beta12).** A player far away (ping 150 ms or more) and a speed boost to 30: the
    connection panel's *Easing off* should come and go, not stay at a low percentage while every guest keeps up.
2. Play at least two hours at your usual speed, with repeating exchanges running and colonies growing to 100+
   beavers. Note the tick rate and each player's frame rate from the connection panel every half hour.
3. A player joins after the game has started (the host saves and hosts again with them). Then **swap hosts**.
4. A player leaves for the rest of the evening: from the next day their colony shows as away in Ctrl+T, and, with the
   host's limit set to 2 (the default, 0, never hands over since 1.4.0-rc2), it is handed over after 2 days, with the host's detailed logging on or off (only a host testing alone,
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
2. **The settlement's name.** **Host co-op game** shows the game's own naming box (as for renaming a beaver) asking
   *What would you like to call your settlement?*, with **Next** and **Cancel** *(beta23: seen)*. Cancel goes back. A
   name already used shows the game's own message and the box stays. Name it and go on.
3. **The host's page.** A **Co-op Game** page like the New Game pages: the banner and title, the "Folktails - map -
   Normal" plate centred, the settlement's name in gold, **Players (1)** with your row (your faction's logo, *Host ·
   Colony 1*, a green tick and **Ready** on the right), **Invite Friends** (usable after a moment), the IP line,
   **Cancel** and **Start Game** *(beta23: seen)*. Screenshot it at your usual resolution and at the smallest window
   you use.
4. **Joining.** Guest, from the main menu: accept the invite. A *Connecting to …* box, then the **Kyler's Game** page:
   the same plate and players, your row marked *(you)*, **Ready** and **Leave**. The host's row for you shows
   *Joining…*, then your name, *Colony 2* and **Not ready**, within a second. The host's page has a red cross for your
   row, in a column of its own to the right of *Not ready*.
4b. **Accepted in the Steam overlay (beta23).** Guest accepts the invite inside the Steam overlay (Shift+Tab) and
   closes the overlay: the whole screen is the **Kyler's Game** page, with **Leave** and **Ready** at the bottom,
   and no main menu showing. (In beta22 the page shared the screen with the main menu.)
4c. **From the list (beta24).** Guest, main menu → **Join co-op game**: a box like the Load Game box, titled *Join
   co-op game*, lists *Friends' games* with the host's row (their name, *Folktails - map - Normal* in gold, *Waiting
   room* on the right) and, under it, *Join by IP address* with the field and **Connect**. Select the row, **Join**: the
   same *Connecting* box, then the waiting room. Also: a friend in a started game shows *Already started*, greyed; with
   no friend hosting, the list says so. Screenshot the box.
5. **Ready.** Guest: **Ready** (the button then reads **Not ready**), then **Not ready**, then **Ready**
   again: your row's state and the host's row follow each time, and the host's line says who is not ready.
6. **Remove and leave.** Host: the red cross on the guest's row → confirm: the guest's page closes with *Kyler removed
   you from the waiting room.* Guest joins again by IP (**Join co-op game**). Guest: **Leave** → confirm: back to the
   main menu, and the host's list drops the row.
6b. **Leave and come back (beta21).** Over Steam: the guest leaves and accepts the invite again, five times; each time
   its row comes back. (Known limit: the host's Steam lobby keeps everyone who left. If a rejoin is refused as full,
   say after how many.)
7. **Start with someone not ready.** Guest joins again and is not ready. Host: **Start Game** → *Not everyone is
   ready (…)* → **Keep waiting**: nothing happens. Guest: ready. Host: **Start Game**. Both see the loading screen
   with the mod's line (*Creating the world…*, *Loading your co-op game…*, *Loading Kyler's co-op game…*). The host
   never sees the new world before it.
7a. **The guest gets in (beta21).** The guest's game loads (its log has `Loading map` and no `NullReferenceException`).
   In beta18 to beta20 no guest got past Start: its game never loaded and it was left on an empty main menu (review
   J1).
8. **In the game.** Both paused at the start. The host has the district center. No *Joining: open*, and placing
   something as host asks nothing. The host's connection panel shows *(loading)* after the guest's name until the
   guest is in. The guest is offered **Place your district center** at once: place it **while still paused**. The
   log has `[Lobby] Guest 1 (…) seated in colony 2`. Unpause and play 5 minutes: no desync.
9. **A multi-start map.** Players field 4, two players: two starts are filled, the guest's is start 2, and nobody
   founds anything. (`[Lobby] Filling 2 start(s)` in the host's log.)
10. **Too late.** A third account accepts an old invite after **Start Game**: refused with "The host has already
    started the game…".
10b. **Start at once (beta21).** Host opens a room and presses **Start Game** straight away (yes to starting alone): on a
    friend's Steam friends list the host shows no *Join game*.
11. **Cancel.** Host opens a waiting room, the guest joins, host **Cancel** → confirm: the guest sees *Kyler closed
    the waiting room.* The host is back on the difficulty page; **Load game** → any save → **Host co-op game** still
    works (the guest sees a *Connecting* box, then that save's page) *(rc7)*.
12. **From a game** *(rc7)*. Guest plays a solo game and accepts a waiting-room invite: the room opens as a window over
    the guest's game (Script G, step 6).
12b. **From a co-op game** *(rc6)*. Guest is in someone else's co-op game and accepts a waiting-room invite: a box says
    to leave that game first, and that game carries on undisturbed.
13. **Waiting.** Leave the guest on the page three minutes before Start: nothing drops. Pull the host's network
    cable for two minutes while the guest waits: the guest is asked *Keep waiting* / *Leave*.
15. **A save (beta19, rc7).** Host, from the main menu: **Load game** → a separate-colonies save you both
    played → **Host co-op game** (right of Load). The same page opens: the plate says the settlement, the gold line the save's name (an autosave
    reads *Autosave*) and its date as the Load Game box writes it; each row names the player's colony, with its
    faction's logo.
    The guest joins from the main menu and readies up; **Start Game**: both load the save, paused, and each is in the
    colony the save remembers (`[Colony] Player … plays slot …`). **Cancel** instead goes back to the Load game box.
15a. **From a game (beta19; rc7).** The old dialog (connected players, Invite Friends, Start Game) is gone: hosting from
    a game opens the same room as a window over the game (Script G).
15b. **A player new to the save (beta19).** A third player who never played the save joins its waiting room: in the
    game they take the next free colony (or found one), as before.
16. Send both `Player.log` files. Lines from the waiting room start with `[Lobby]`.

## Script F: Folktails and Iron Teeth together (two players, about 40 minutes) *(beta20)*

Mixed factions. **Not played yet**: every line is new. Both computers need the same mod version; the host needs
Iron Teeth unlocked (reach 15 average wellbeing with Folktails once, or dev mode's unlock). Screenshots of the
waiting room and of each colony help.

1. **The checkbox.** Host: New Game → Folktails → a map → the difficulty page: **Separate colonies** ticked, and
   **Mixed factions** ticked under it (since 1.4.0-rc3; a Mod Setting before). Its tooltip is on screen.
2. **The room.** New Game → Folktails → a BeaverBuddies multi-start map with 3 starts → Normal → **Host co-op game**.
   The page shows the plate *map - Normal* (no faction), a gold line *Separate colonies: each player builds their own
   colony, as Folktails or Iron Teeth.*, then **Your
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
11a. **After a switch (beta21).** Let the switched colony play a day, then set off dynamite near colony 1's beavers, or
    leave a beaver by a Folktails Beehive for two days: no crash, and the population total equals the districts' sum.
    (beta20's switch left the old beavers in the game's lists, and this crashed; review B-1.)
11b. **A switch at tick 0 (beta21).** A mixed waiting-room game on a 3-start map, the guest leaving the picker alone.
    While still paused at the start the guest accepts *Switch*: its colony's beavers are Iron Teeth (fur, needs), as
    many as the game mode starts with (review B-2).
12. **The save's room.** Main menu → **Host co-op game** → that save → **Host co-op game**: the rows
    *Host · Colony 1* with the Folktails logo and the guest's *Colony 2* with the Iron Teeth logo; nobody's switcher
    shows (both colonies have their faction).
13. **Locked (rc3).** On a computer without Iron Teeth unlocked, the difficulty page's **Mixed factions** is greyed and
    its tooltip says Iron Teeth is still locked; a new room has one faction and no switcher.
13a. **A faction mod (beta21, rc3).** With a mod that adds a faction installed, **Mixed factions** is greyed and its
    tooltip says it is made for Folktails and Iron Teeth; a new room has one faction.
14. Send both `Player.log` files. Lines start with `[Factions]` and `[Lobby]`.

## The late-game playtest (two players) *(1.4.0-rc1)*

The 1.4.0-rc1 review read the late game, Folktails and Iron Teeth together and Trading Posts at scale against the game's
own code, and fixed what it found; none of it has been played. This playtest is what it leaves to people. It has
three parts (Scripts L, M and T), a set of short recordings (Script P) and a long session. Each line says what should
happen and, in brackets, the review finding it checks (`design/REVIEW-FINDINGS-1.4.0-beta24.md`).

**For every part:**
- Both players install the same zip and use the same mods and settings.
- The host turns on **Always Use Detailed Logging** only for Script T and small tests. From 200 beavers and bots it
  costs too much, and the desync dialog no longer offers it.
- After each part, send both players' `Player.log` and the Ctrl+Shift+J report (press it just before leaving; it is
  saved in `BeaverBuddies-Reports` next to `Player.log`).
- **Building a late game quickly:** use dev mode (Alt+Shift+Z) **in single player** (placing finished buildings,
  adding beavers and science), save, then host that save. In co-op only three dev tools are shared: Ctrl-click
  unlock, *Finish now* and *Add 1000 Science*. Turn dev mode off before the tests.

## Script L: the late game, two players (about 90 minutes) *(1.4.0-rc1)*

**Setup (the host, single player, dev mode, about 30 minutes):**
1. Take a copy of your largest late save (300 or more beavers), made as one shared colony (**Separate colonies**
   unticked, or a Stability Fork save): step 1 splits it.
2. Near where the guest will found their colony, build:
   - a chain of automation: a lever, a relay, a memory, a timer, and an Indicator set to *warn*;
   - a Population Counter set to count everywhere;
   - an HTTP Lever and an HTTP Adapter;
   - a spring-return lever wired to a Detonator on a Dynamite, a few tiles from where the guest will build;
   - a pump and a throttling valve;
   - three synchronised floodgates along the edge of your land;
   - a gravity battery on a power shaft that ends at that edge.
3. Save.
4. Main menu → **Host co-op game** → the save → **Host co-op game**, leaving **Separate colonies** unticked on its page
   (step 1 splits it). The guest joins; Start. (A split keeps one pool of science and unlocks, so the guest can build the
   late game at once.)

1. **Founding beside your fields** (E-7). The guest splits the game (Esc → **Found your own colony** → **Found my
   colony**) and places their district center right beside the host's farms and forests, then
   builds a farmhouse and a lumberjack flag that reach them. After a day none of the host's marked crops or trees is
   worked by the guest's beavers, and the guest's unmark tool leaves the host's marks.
1a. **Unlocking together** (E-5). Both players click **Unlock** on the same locked building within a second: the
    science counter drops once, not twice.
2. **Automation** (A1). The guest builds the same chain as the host's; one player caps their frame rate at 15 (the
   game's frame limit setting). Flip levers on both computers: both see the same lights within a tick. Pause, flip,
   unpause: the change shows on the first tick after unpausing, the same on both. Press **Reset** and **Reset all** on
   a memory: Reset resets that one, Reset all the whole chain (P-1: they were swapped).
3. **The HTTP API** (A3).
   - Each player starts the HTTP API (the HTTP Lever's panel) and switches their own HTTP lever by URL
     (`http://localhost:8080/api/switch-on/<name>`): it switches on both computers.
   - The guest switching the host's HTTP lever by URL is refused with a notice.
   - `http://localhost:8080/api/color/<name>/ff0000` does nothing in co-op (the log says so once).
4. **The Detonator** (A1-1). One click on the spring-return lever wired to the Detonator: the dynamite goes off on
   both computers, the same tick (compare the logs). Before rc1 it never did in co-op.
5. **Counters and warnings** (A4, O4). Set each colony's global Population Counter's threshold between the two
   colonies' populations: each lights by its own colony's count, the same on both computers. Switch each colony's
   warning Indicator on: each player sees only their own colony's warning.
6. **Water settings** (A2, W1). The guest drags their pump's flow rate, and their throttling valve's outflow from
   *unlimited* to half: the pump's panel and the valve read the same on both computers. The guest places two
   floodgates touching the host's synchronised ones, changes their height and wires them to a lever: only the guest's
   follow; the host's keep their height and wiring.
7. **Power across colonies** (P1, documented). The guest lays a shaft into the host's power shaft: one network (both
   see the same power). A clutch of either colony cuts it, and either colony's Power Meter reads all of it.
8. **Weather** (W4). Play through a drought and a badtide with automated floodgates, valves and regulators on both
   colonies: no desync through both.
9. **A blast beside the other colony** (X1, X2, E-8). The guest builds a second district center next to the host's
   colony, fills it with beavers, then deletes it: its beavers join the guest's first district, none the host's (the
   top bars' counts show it). Then set off a chain of dynamite beside the other colony: the same result on both
   computers, no error; note how long it takes at speed 3 (the Ctrl+Shift+J report's *Frames cut short* line).
10. **Placing while the host hovers** (E-6, a regression line). The host holds a path preview beside the guest's road
    (red) without clicking; the guest places three buildings elsewhere: all three appear on both computers.
11. **Maximum speed** (S5). Twenty minutes at speed 3 (x7), then at a boost of 15 (chat box `/boost 15`). The game
    stays in step; at the boost the host eases off (logged) and the guest's lag stays steady, not growing.
12. **A steward's colony** (E-3). Host setting *Hand over a colony after its player is away* = 1. The guest asks the
    host to look after their colony (Ctrl+T), then leaves. Play three days; the host ends the stewardship. The next
    day brings the warning (*… unless they are in the game tomorrow*), and the hand-over comes the day after.
12a. **Off by default** *(1.4.0-rc2)*. On a fresh install, Mod Settings shows *Hand over a colony after its player is
    away* at 0. With it at 0, the guest leaves for three in-game days: no warning and no hand-over; the host's Ctrl+T
    says *the host hands no colony over for absence* on the guest's colony.
13. **Save and Rehost.** Esc → **Save and Rehost**; the guest chooses **Rejoin** and lands on the page; Ready, Start
    Game: everything as before, no desync. Both players
    press Ctrl+Shift+J and compare the `day N tick T:` check lines: equal.
14. Send both `Player.log` files and reports.

## Script M: Folktails and Iron Teeth in the late game (two players, about 90 minutes) *(1.4.0-rc1)*

1. **Setup** (hosted alone with dev mode; TWO-COLONIES, *Testing alone*):
   - A new game with **Separate colonies** and **Mixed factions** ticked on the difficulty page, Folktails. Three colonies: 1 Folktails (the
     host's), 2 Iron Teeth (the guest's), 3 Folktails (nobody's).
   - For colonies 1 and 2, with dev mode:
     - a bot assembler, a bot part factory and 6 or more bots;
     - Folktails: a refinery (Biofuel, Catalyst) and a printing press (PunchCard);
     - Iron Teeth: two charging stations, a grease factory and a control tower;
     - lodges (Folktails) and breeding pods (Iron Teeth) with beavers;
     - ziplines (Folktails) and tubeways (Iron Teeth);
     - a badwater rig (Folktails) and a deep badwater pump (Iron Teeth);
     - an explosives factory and the metal buildings;
     - three monuments or decorations each;
     - a Beehive beside some Folktails and Iron Teeth crops;
     - a Trading Post between them.
   - The Earth Recultivator (colony 1) finished, filled and staffed. The Earth Repopulator (colony 2) finished,
     filled, set to bots, its eight places taken by Iron Teeth bots.
   - Colony 3: a bot assembler and 8 Folktails bots. Then, still alone, Ctrl+T → hand colony 3 over to colony 2.
     Acting as colony 2, build a second Earth Repopulator in colony 3's old district: finished, filled, set to bots,
     its eight places taken by the Folktails bots.
   - Save. Host; the guest joins as colony 2.
2. **The daily check** (F6). Play two in-game days. Each player's daily `[Colony] Check day N …` line ends in
   `/census:0=Folktails:A+B,1=IronTeeth:C+D`, the same on both, and A to D match each colony's beavers and bots in its
   population panel.
3. **Births** (F2). A lodge birth, a breeding-pod birth and a child growing up: each new beaver has its colony's
   faction's fur and avatar. Neither log has *was made with no faction in hand*.
4. **Bots** (F3). Each assembler makes its own faction's bot (model, name, avatar). Iron Teeth bots charge at the
   charging stations; Folktails bots never go there. No bot shows a need of the other faction.
5. **Both Wonders** (V1, V2, B5). The host activates the Earth Recultivator, the guest the Earth Repopulator; the guest
   caps their frame rate at 15 and both play at speed 7 while the planes launch. The activating player hears the
   launch sound, the other doesn't. All planes launch; the pilots are gone half an hour later; no desync.
6. **A Wonder's effect** (V1). While the Recultivator is active, a Folktails beaver near it shows the Earth Recultivator
   need rising; an Iron Teeth beaver walked near it does not.
7. **Completion** (V1). Half an hour after the first Wonder switches off, both players see the completion screen, each
   with their own faction's picture and words; the game waits for the host to close it; the next daily check is
   equal.
8. **Folktails bots flying an Iron Teeth Wonder** (B1). The guest activates the second Earth Repopulator (in colony 3's
   old district, with Folktails bots). The planes launch with Folktails bots aboard (in their ordinary pose). **No
   "Multiplayer has stopped" message**: beta24 stopped here for everyone. Both logs have `[Factions] A character
   without the "Piloting" animation does it without`. Save and reload: no error. The census shows colony 2 with
   Folktails bots (`1=Folktails:…+8`) beside its Iron Teeth beavers and bots.
9. **The Beehive** (F5). It stings only Folktails beavers; the Iron Teeth crops beside it grow faster too (documented).
10. **Ctrl+T at scale** (B4). In the late game, keep Ctrl+T open for two minutes: no hitch every second.
11. **Save and rehost mid-flight** (F9). Save while a plane is on the runway, rehost, the guest joins: the plane and its
    pilot carry on; the census and `chars:` of the next daily check are equal on both.
11a. **Away in a mixed game** *(1.4.0-rc2)*. Host setting *Hand over a colony after its player is away* = 1. The guest
    (colony 2, Iron Teeth) leaves; nobody else plays Iron Teeth. Play three days: no warning and no hand-over, and the
    host's Ctrl+T says *not handed over: no colony of its faction is in the game* on colony 2. Hover the host's **Hand
    to …** button there: its tooltip says the two are different factions and what the receiver can't do. Don't press
    it. Set the setting back to 0; the host saves and rehosts, and the guest joins again.
12. Send both `Player.log` files and reports.

## Script T: Trading Posts at scale (two players, about 90 minutes) *(1.4.0-rc1)*

**Setup:** a separate-colonies game with separate science and two colonies (a third for T-9). With dev mode **only in
single player**, build 20 Trading Posts between them, tanks, piles and warehouses on both sides, and stock of every
good. Save, host, and have a guest join. Turn on detailed logging for both, the guest a minute after the host: the game
goes on with no desync dialog (D-new-2; before rc1, switching it on mid-game read as a desync).

1. **40 exchanges** (T4, T7). Open an exchange at every post: goods of each kind, science, beavers, gifts and requests,
   some repeating, some with a reserve. Ctrl+T lists every post with its terms and round, and the window stays smooth.
2. **Conservation** (C6). Play 10 in-game days. Each day both logs have one `[Colony] Day … trade:` line, identical on
   both computers, with `checks ok`; no `Trade check:` warning.
3. **No room** (C3). At a post where you give 100 Logs a round, take away the other colony's storage room for Logs.
   After the first round your panel says *No room on your half for more Logs: 100 … still wait on …'s half*, and
   theirs says *… haul them away (they need storage room)*. Give them room: the round goes on.
4. **Paused or flooded** (C3). Pause one half (its button, then with automation) and flood one: both panels and Ctrl+T
   say *paused or flooded*. Unpause: it goes on.
5. **No stock** (C3). Give away all of a good you trade: *Your district has no more … to bring.*
6. **Beavers** (C2). Trade 5 beavers from a district of 6 adults while they work: the round waits (*adults free to
   go*), then moves exactly 5, and the ledger and the log's *crossed* line say 5. No *Only N of M beavers* warning.
7. **Every good both ways** (T2). One exchange per good both ways (liquids into tanks, piles into piles), plus science
   and beavers, repeating for 10 cycles. Each crosses; the panel's *Traded with* totals match the log.
8. **Goods already on the half** (C4). Receive 100 Water with no tank room, then offer 100 Water back: the round fills
   at once from the Water waiting there, and crosses.
9. **Mixed** (C1). A three-colony mixed game hosted as Folktails, with two Iron Teeth colonies trading Grease. Remove
   the road at one half: the exchange shows *paused*, not ended. Restore the road: it goes on.
10. **A post blown up mid-round** (C5). With goods held on both halves, blow the post up with dynamite. Both players
    get *An exchange ended: its Trading Post was removed…*; the log lists what waited; the goods lie as recovered goods.
    No desync.
11. **Save and rehost mid-round** (T1). With rounds half filled at several posts, save, reload and rehost: the bars show
    the same held amounts, the rounds finish, there is no desync, and the day's trade lines still match.
12. **Cost** (C7). With all 40 exchanges running at speed 7, the Ctrl+Shift+J report's *Trading post exchanges*,
    *Trading post workers* and *Trading post daily check* rows stay small (well under 1 ms a tick on average).

## Script P: performance recordings (one or two players) *(1.4.0-rc1)*

**For every recording:**
- Use Kyler's **PerformanceLog** mod, with its defaults. Change nothing else between the two recordings of a pair.
- Close other programs, keep the game window in front, and keep the same window size.
- Load the save and choose speed 7 (the third speed button). Wait **1 minute**, play untouched for **3 minutes**, then
  leave to the main menu.
- Send the session folders (`Documents\Timberborn\PerformanceLog\`), `Player.log`, and in co-op the Ctrl+Shift+J
  reports. Compare pairs with `python tools/perflog.py compare <A> <B>`.

1. **P1, single player, with and without MultiColony** (B1, S10). The late save with MultiColony disabled (restart),
   then enabled (restart). Tick time within 1 % of each other; PerformanceLog no longer lists MultiColony on
   `TickableEntity.Tick`.
2. **P2, hosting with nobody joined** (B2, B3, C3, S9). Host the late save from the main menu, start alone, and record.
   Then Esc → **Save and Rehost**; a guest joins the page; Start; the guest splits it (Esc → **Found your own
   colony**), let a day pass, and record again. In each, also hold a path tool over roads for 30 s. MultiColony's share of tick time: at most 5 % shared and
   7 % split. No MultiColony method among the top allocators.
3. **P3, with a guest** (B4, S2, S5). The late save split into two colonies, a guest joined. Three minutes each at
   speed 2, speed 3, and speed 3 with a boost of 15. Both players record.
   - At speeds 2 and 3, the guest stays within 4 ticks of the host 95 % of the time.
   - At boost 15, the host eases off (logged) and the guest's lag is steady.
   - The report's *Frames cut short … a tick* is small at speed 3.
4. **P4, mixed factions** (B6). Script M's grown game against a one-faction game of the same size, P2's steps. Tick
   cost within 5 %; load time and the report's `Memory:` line within 25 %.
5. **P5, with LateGamePerformance** (C1). P2 again with LateGamePerformance 0.4.28 on both computers. The same daily
   `[Colony] Check` lines on both (no desync). LateGamePerformance's own warning about `ColonyStamp`'s hash appears
   once (C1-4, a finding for that mod).
6. **P6, a rehost** (B5, S6). In P3's session, Esc → **Save and Rehost**; the guest chooses **Rejoin**, then Ready, and
   the host **Start Game**. From the click to the
   guest playing: under 30 s on a home network, and no host freeze over 1 s.
7. **P7, big actions** (A6, E). Record the frames around each of these on the split late save:
   - placing a 50-building automation blueprint;
   - a hand-over of a large colony by hand (Ctrl+T);
   - the founding that splits the save;
   - a 100-tile path dragged by the guest (the host's *Placement checks in replays* row).

## The long session *(1.4.0-rc1)*

A late two-colony game with a guest, for at least 20 in-game cycles; then a mixed game and a trading game for 10 each
(Scripts M and T's games). Play it or leave it running, as you like. At the end, copy every `[Perf] Day` line from
both players' `Player.log`. What to look for:
- the heap levelling off after the first days, not climbing steadily;
- *detailed-logging traces 0 ticks* (detailed logging off);
- *buckets lost to the one-tick cap* near 0 at normal speed;
- no `[Colony] Colony state differs` line and no `[Colony] Trade check:` line.

## Script S: separate or shared, chosen on the New Game page (two players, about 30 minutes) *(1.4.0-rc3)*

Since 1.4.0-rc3 a new game's colonies are chosen on the New Game difficulty page, not in Mod Settings, and a guest
splits a shared game from the game menu. Screenshots help for every page and box here: the checkboxes must line up,
and no text may overlap or run off.

1. **The page.** New Game → Folktails → a map → the difficulty page. Under **Tutorial**: **Separate colonies**, and
   under it, indented, **Separate science and unlocks** and **Mixed factions**. All the checkboxes line up with
   Tutorial's; the labels are the page's white text. Hover each: a tooltip. Untick **Separate colonies**: the two under
   it disappear; tick it: they come back. Screenshot both.
2. **Custom difficulty (rc5).** Press **Customize**: the colony checkboxes move into the custom settings list, right
   under its **Tutorial** row, and their boxes line up with the list's own (Tutorial, Enable droughts, Enable badtides);
   the list scrolls as before; nothing overlaps. Screenshot. Pick Normal again: they are back under the page's Tutorial.
   A multi-start map opens in custom difficulty: the same there.
3. **Iron Teeth.** Back, pick Iron Teeth, the same map: whether or not the game shows its Tutorial checkbox for Iron
   Teeth, the colony checkboxes are there, aligned.
4. **Remembered.** Untick **Separate colonies**, quit the game, start it again: the page shows it unticked. Tick it again.
5. **A shared room.** Untick **Separate colonies** → **Host co-op game**. Under the plate, the gold line reads *One shared
   colony: everyone plays it together…*, on the host's page and on the guest's. Start.
6. **The game menu.** In the game (paused or not), the guest opens the game menu (Esc): **Found your own colony** is below
   Settings (and Player cursors), the same size as the others, its text on one line. The host's menu has no such button.
   The guest's **Ctrl+K** says the game has one shared colony and points to the menu; the host's says the shared colony
   is theirs.
7. **Asking first.** The guest presses **Found your own colony**: a box says it can't be undone and that the shared colony
   stays the host's. **No**: nothing changes. Again, **Found my colony**: the menu closes with the district center
   in hand. Leave the tool (right-click or Esc): the game is still shared, and the button is still in the menu. (From
   now on the guest's **Ctrl+K** opens the founding tool directly: they have said yes once.)
8. **The split.** Again, **Found my colony**, and place the district center; the host unpauses if needed. The guest
   reads *Your colony is founded. This game now has separate colonies, for good.*; the host reads that a new colony has
   been founded and the shared colony is theirs. Both logs: `[Colony] Separate colonies switched on: slot 1 founded a
   colony in a shared game`. The top bar's science is the same number on both (one pool). Neither game menu has the
   button any more.
9. **For good.** The host's Save and Rehost: the guest is brought into the room over their game *(rc7)*; Ready,
   Start: still two colonies, no button, and the guest's Ctrl+K says they already have a colony.
10. **A separate room.** New game, **Separate colonies** ticked and **Separate science and unlocks** unticked → Host co-op
    game: the gold line reads *Separate colonies: each player builds their own colony.* In the game, the guest is offered
    to place a district center as before, and the colonies share one pool of science.
11. Send both `Player.log` files.

## Script H: hosting a save, and hosting from a game (two players, about 40 minutes) *(1.4.0-rc4)*

Every game is hosted through a Co-op Game page since 1.4.0-rc4, and the original BeaverBuddies hosting dialog is gone.
Since 1.4.0-rc7 a save is hosted from the Load game box, in the main menu or in a game, and the room opens over a game
as a window (Script G). Screenshots help for every box and page here: nothing may overlap or run off.

1. **The main menu** *(rc7)*. Under **Load game**: **Join co-op game** only (no Host co-op game), the same size, and it
   clicks like the game's buttons. With a save (so **Continue** shows), the whole menu, its frame and the Discord logo
   sit inside the brown band, clear of its bottom bar, the band its usual size. Screenshot.
2. **The Load game box** *(rc7)*. Press **Load game**: the game's own box, titled *Load game*, with **Delete settlement**,
   **Delete save**, **Load** and, right of it, **Host co-op game**, all one size in one row, nothing overlapping (the box
   is a little wider than the game's). Pick saves and read the gold line under the picture: a separate-colonies save you
   both played says *Separate colonies: 2 players*, one made alone with Separate colonies ticked *… only yours so far*,
   a shared one *One shared colony. When you host it, you can make it separate colonies.* The save list doesn't move as
   you click through. Screenshot.
3. **Load still loads** *(rc7)*. Enter or a double-click on a save loads it, as **Load** does. Back in the box, pick a
   shared save, **Host co-op game**, then **Cancel** on its page: the box comes back with the save's gold line still
   there.
4. **A shared save, made separate at Start.** Load game → a shared save → **Host co-op game**. The page shows
   **Separate colonies** (unticked) under the gold line *One shared colony…*, lined up like the New Game page's. Tick
   it: **Separate science and unlocks** appears under it, and the gold line reads *Separate colonies from Start…* on
   the host's page and, at once, on the guest's. Untick: back. Tick again, with separate science unticked, and **Start
   Game**. Both load; the host reads *This game now has separate colonies, for good…*; the guest is offered to place a
   district center; both logs have `[Colony] Separate colonies switched on: the host hosted this save as separate
   colonies`. The science in the top bar is the same number on both. The guest's offer may come a moment after the
   host's notice: it comes with the guest's own arrival.
4a. **Separate science is unticked at first** *(rc5)*. On a shared save's page, tick **Separate colonies**: **Separate
   science and unlocks** under it is unticked (the colonies go on sharing). If the guest's game loads before the
   host's and the guest places a path at once, it is refused with *Not yet: the game is still starting*; a moment later
   it can be placed, and it is the guest's colony's, not the host's.
4b. **Load game in a hosted game** *(rc5)*. In that game the host presses Esc → **Load game** → an autosave → **Load**:
   it loads (it used to do nothing after hosting from the box).
5. **A separate save's page** shows no checkboxes: it is separate already.
6. **From a game played alone** *(rc7)*. Load any save with **Load game** and play a minute. Esc: **Host co-op game**
   and **Join co-op game** are under Load game. **Host co-op game**: a box says the game is saved and its room opens
   over it. Confirm: a save named *… Co-op*, and its room as a window over the game (Script G, step 9). The friend
   joins; Ready; **Start Game**: both in the game you left.
7. **The menu in co-op.** In that game, the host's Esc menu has **Save and Rehost** where Host co-op game was; the
   guest's has neither. After step 8's lost connection, a guest who chooses **Stay here** still has neither *(rc5)*.
8. **Save and Rehost** *(rc7)*. The host chooses Esc → **Save and Rehost** and confirms. The guest is brought into the
   host's room over their own game, with no *connection lost* (Script G, step 10). Ready; **Start Game**: both carry on
   where they were, each in their own colony.
9. **The guest first** *(rc5; rc7)*. To press first the guest needs the desync dialog's **Reconnect (wait for Rehost)**
   (step 10), or drops their own network for a minute while the host plays on, then **Rejoin**. The guest waits in
   their game, under *Waiting for the host's Co-op Game room…*, while the host is still in the game: no error, the
   guest's game stays smooth, and the host gets at most one *mods differ* message, not one every few seconds. Then the host's **Save and Rehost**: the guest joins
   as the page opens. Once more, and the guest presses **Cancel** while waiting: it stops, and **Join co-op game** still
   works. Also once with the Steam overlay open (Shift+Tab) as the guest gets in: the waiting box goes away once the
   overlay closes, and **Cancel** always works.
10. **After a desync** (if one happens): the desync dialog's **Save and Rehost** and **Reconnect (wait for Rehost)** do
    the same as step 8.
11. **Both ways of joining.** Do step 8 once with the guest joined over Steam and once by IP address.
12. **An invite in a game** *(rc6; rc7)*. The guest plays a save alone; the host opens a Co-op Game page and invites
    them over Steam. The guest accepts (Shift+Tab): once the overlay closes, the host's room opens as a window over the
    guest's game (Script G, step 6; rc6's *Save and join* box is gone). Once more while the guest is in a co-op game
    with someone else: a box says to leave that game first, and that game carries on undisturbed.
13. **A direct join that finds nobody** *(rc6)*. Main menu → Join co-op game → an address where nothing hosts (for
    example a friend's IP while they are in the main menu): the *Connecting* box shows at once, the menu stays smooth,
    and after about 3 seconds it says the connection failed.
14. Send both `Player.log` files. Lines start with `[Lobby]`, `[Join]` and `[Colony]`.

## Script G: hosting and joining from inside a game (two players, about 60 minutes) *(1.4.0-rc7)*

The Co-op Game room also opens as a window over a running game: a save is hosted from the Load game box in a game,
the game menu hosts the game you're in, a guest joins from their game, and a host's guests are carried into its room.
Nobody goes through the main menu. **Not played yet**: every line is new, and nothing of it has been seen in the game.
Screenshots matter here: the window and the Load game box are built from the game's own templates without a look at
them, so please screenshot each one named below, at your usual resolution and at the smallest window you use.

1. **The Load game box, main menu.** Main menu → **Load game**: **Delete settlement**, **Delete save**, **Load**,
   **Host co-op game**, one size, one row, centred, nothing overlapping or cut, the box a little wider than the game's.
   The gold line under the picture for every save you pick; the list doesn't move. **Screenshot 1.**
2. **The Load game box, in a game.** Load any save alone, Esc → **Load game**: the same box, the same buttons and line.
   **Screenshot 2.** Load, Enter and a double-click still load.
3. **The host's window.** In that game, Esc → **Load game** → another save → **Host co-op game**. The window opens over
   the game, which pauses (the clock stops, the speed buttons are locked): the capsule title *Co-op Game*, a close
   button, the plate (the settlement), the gold line (the save's name and date), **Players (1)** with your row (*Host*),
   **Invite Friends**, the IP line, the status line, and **Cancel** / **Start Game** at the bottom. Nothing overlaps,
   the window fits the screen, and any long line wraps within two lines. **Screenshot 3** (usual size) and **3b** (the
   smallest window). The log has `[Lobby] Waiting room open for the save`.
4. **Cancel returns.** **Cancel**: back to the Load game box, its gold line there; Esc → the game menu; Esc → the game,
   at the speed it had. Open the room again; with a guest in it (step 6), the close button and Esc ask first.
5. **A shared save and a mixed save.** Host a shared save from a game: the **Separate colonies** checkboxes in the
   window line up, nothing overlaps (**Screenshot 5**). A mixed save (if you have one): each row's faction logo.
6. **Joining from a game.** The guest plays a save alone. The host invites them (**Invite Friends**); the guest accepts
   (Shift+Tab): a *Connecting* box over the guest's game, then the *Kyler's Game* window over it, the game paused, with
   **Ready** and **Leave**. **Screenshot 6.** The guest's log: `[Lobby] Joining from a game: this game stays
   single-player until the host's save arrives`. **Ready**, **Not ready**: the host's row follows. **Leave** (asks):
   back to the guest's game as it was.
7. **Join co-op game in a game.** The guest, Esc → **Join co-op game**: the Join box over the game menu, the host's row
   listed (**Screenshot 7**); **Join** → the window. Leave, and join by IP from the same box. Also: a co-op game's menu
   has no **Join co-op game**, and neither has the menu of a player whose room is open.
8. **Start from games.** Both in the room (host from a game, guest from a game), **Start Game**: both load the hosted
   save, paused, each in their colony, without the main menu. Afterwards **Load game** shows an exit save of each game
   that was replaced (as Exit to menu makes); logs: `[Lobby] This game's exit save is made`.
9. **Host the game you're in.** Play alone, Esc → **Host co-op game** → confirm: a save *… Co-op* and its window over
   the game. The guest joins; **Start Game**: no exit save of the host's game this time (log: `No exit save of this
   game at Start (it was just saved for the room)`).
10. **Carried, over Steam.** In a co-op game joined over Steam, the host: Esc → **Save and Rehost** → confirm. The guest
    sees no *connection lost*: a box *The host is moving this game to a Co-op Game room…*, then, within a few seconds,
    the host's window over their game, already in the room. Host log: `[Lobby] Moving to a waiting room: told 1
    guest(s); Steam lobby … kept for the room`, `Steam lobby … kept from the game and reopened`; guest log: `[Lobby] The
    host is moving this game to a waiting room; following it`, then `connecting to the host`. Ready, **Start Game**:
    carry on; no exit save of the guest's game (`it was loaded as a guest`). Once more with the host's **Allow Friends
    to Join Directly via Steam** off: the guest still comes back.
11. **Carried, by IP.** The same with the guest joined by IP address: the guest comes back within a few seconds.
12. **Carried to another save.** In a co-op game, the host: Esc → **Load game** → another save → **Host co-op game**:
    the guest is carried into that save's room the same way. At Start the host's co-op game gets its exit save, the
    guest's does not.
13. **A lost connection.** In a co-op game the guest pulls their network cable for a minute: *The multiplayer connection
    was lost* with **Rejoin** and **Stay here**. **Rejoin**: a box over the guest's game, *Waiting for the host's Co-op
    Game room…*. The host: **Save and Rehost**: the guest's window opens over their game. Once more, and the guest
    presses **Cancel** while waiting: the wait stops, and **Join co-op game** in the game menu still works.
14. **After a desync** (if one happens): the host's **Save and Rehost** carries any guest still connected; the desynced
    guest's **Reconnect (wait for Rehost)** waits in their game and joins the room.
15. **Refusals.** A guest in someone else's co-op game accepts an invite: a box says to leave that game first; that game
    carries on. The host, with a room open, accepts someone else's invite: a box says to close the room first.
16. **Removed.** The host removes the guest (the red cross): the guest's window closes, *Kyler removed you from the
    waiting room.*, and the guest is back in their game, which plays on.
17. Send both `Player.log` files, with the screenshots (1, 2, 3, 3b, 5, 6, 7). Lines start with `[Lobby]` and `[Join]`.
