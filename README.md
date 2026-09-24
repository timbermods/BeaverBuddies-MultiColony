# Timber Together

***Build apart. Thrive together.***

Co-op Timberborn where every player runs their own colony on one shared map: their own districts, beavers, stock,
science and working hours. The colonies meet only at **Trading Posts**, where they barter. Co-op, not a race.

![Timberborn 1.1.2.4](https://img.shields.io/badge/Timberborn-1.1.2.4-2a4034?labelColor=172620&style=flat-square) ![Status: beta](https://img.shields.io/badge/status-beta-e0812f?labelColor=172620&style=flat-square) [![GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-2a4034?labelColor=172620&style=flat-square)](License.txt)

[Install](#install) · [Host](#host-a-game) · [Join](#join-a-game) · [Your colony](#your-colony) · [Trading Posts](#trading-posts) · [Controls](#controls) · [Troubleshooting](#troubleshooting) · [Website](https://timbermods.github.io/TimberTogether/)

> [!WARNING]
> **Beta: the release candidate for 1.4.0.** Automated checks cover everything here, but real games have been short.
> Play on a copy of your save and keep backups.
> - **Played:** hosting, joining over Steam and founding a second colony; two colonies building side by side, in step;
>   Trading Posts exchanging goods and beavers; opening the Co-op Game room and joining it; and the Stability Fork's
>   Steam invites, connection panel, cursors and desync fixes, over hours of two-player play.
> - **Not played yet:** starting a game from the Co-op Game room; the **Separate colonies** checkbox and splitting a
>   shared game; hosting a save or the game you're in, joining from inside a game, and rejoining a rehost; Folktails and
>   Iron Teeth together; looking after an away player's colony, and hand-overs; the Trading Post's own model and its
>   3 × 2 halves; most of the road rule; Trading Posts at scale; and the late game (automation, the HTTP API, water
>   automation, power, dynamite and tunnels, both Wonders, bots).

Built on [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies) by Thomas Price (thomaswp) and contributors,
through the [BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork)
([credits](#credits-and-license)). Steam invites, the connection panel, chat, pings, cursors and ordinary
shared-colony co-op all still work.

## Install

You need Timberborn **1.1.2.4** (tested on Windows, Steam version) and the **Harmony** and **Mod Settings** mods.
**Every player installs the same zip.**

1. Download `TimberTogether-<version>.zip` from the newest [release](https://github.com/timbermods/TimberTogether/releases),
   under **Assets** (not "Source code").
2. Close Timberborn.
3. In `Documents\Timberborn\Mods`, delete any other BeaverBuddies folder (the Stability Fork, an older copy of this
   mod) and unsubscribe from the Workshop BeaverBuddies. Only one can run; the main menu names any that is still on.
4. Extract the zip and copy the `TimberTogether` folder into `Documents\Timberborn\Mods`.
5. Start Timberborn and enable **Harmony**, **Mod Settings** and **Timber Together**.

**Settings:** open the **Mods** list (main menu, or Esc in a game) and press the settings button beside **Timber
Together**.

**To update**, replace the folder with the new zip. Everyone updates together: a different build can't join.

## Host a game

Every co-op game starts in a **Co-op Game** room. Invite with **Invite Friends** (Steam) or give your IP address
(port **25565**); friends press **Ready**, and your **Start Game** loads the game for everyone at once, paused.
Nobody can join after that, until you **Save and Rehost**.

**A new game.** New Game → faction → map → difficulty. Under the game's **Tutorial** checkbox:

- **Separate colonies** (ticked): a colony each. Unticked, everyone builds [one shared colony](#one-shared-colony).
  While it's ticked, two more boxes show under it:
  - **Separate science and unlocks** (ticked): each colony earns its own science. Unticked, they share one pool.
  - **Mixed factions** (unticked): each player picks Folktails or Iron Teeth in the room. Greyed unless every faction
    is unlocked and no other faction mod is installed.

Then press **Host co-op game** beside **Start** and name your settlement. **Invite Friends** lights up as soon as
the Steam lobby is ready.

**A save.** Load game (main menu, or Esc in a game) → pick a save → **Host co-op game**, right of **Load**. A gold
line under the save's picture says whether it has separate colonies or one shared colony. Each player gets the colony
the save remembers for them. For a shared save, the room offers **Separate colonies**: tick it to split the game at
Start, for good. Your colony keeps everything built so far, and each friend founds a new one.

**The game you're in.** Playing alone, Esc → **Host co-op game** saves the game and opens its room over it.

**Save and Rehost.** In a co-op game, that button reads **Save and Rehost**: the game is saved and everyone meets in
its room again, to let someone new in or to recover from a dropped connection or a desync. Guests come along by
themselves; one who missed it chooses **Rejoin**.

## Join a game

**Join co-op game** (main menu, or Esc while you play alone) lists your Steam friends' games: pick the host's and
press **Join**. Or accept the host's Steam invite, or type their IP address under the list. In the room, press
**Ready** and wait for the host.

Joined from inside a game, the room opens over it and your game waits, paused. **Leave** takes you back to it; at
**Start Game** it is saved as at *Exit to menu*, and the host's game loads. In a co-op game, leave it first to accept
someone else's invite.

**Found your colony.** In the game you're offered a district center to place (**Place district center**, or
**Ctrl+K** later). Put it anywhere its roads won't join another colony's: it's free and already built, with starting
beavers, food and water. On a multi-start map you get the next start instead. The save remembers your colony by your
Steam ID, whoever hosts.

## Your colony

- **Yours alone:** your districts, beavers, stock, working hours and marks, and with separate science your own science
  and unlocks. Nobody else can change them, and your beavers work only for your colony.
- **Build anywhere,** right up to a neighbor. The one rule: **your roads never join another colony's**, except
  through a Trading Post. **Ctrl+L** shows every colony's roads in its color.
- **Your screen is your colony's:** the top bar, alerts, batch lists and working hours.
- **Shared by everyone:** the map (water, droughts, badwater, weather), speed, pause, pings, chat and saving.
- **Up to four colonies.** More players help run the host's colony.
- **Folktails and Iron Teeth together** (Mixed factions): each colony plays its own faction. Between factions, Trading
  Posts carry the goods both use, and science, but not beavers.

**Away for a while?** Your colony is kept for you. To keep it running, press **Ctrl+T** → **Let … look after it** on
your colony: your friend presses **Run this colony** to play it, and **Back to your colony** when done. **Take it
back** any time.

**A colony changes hands** only when it has no beavers or bots left for a whole day, when the host hands it over from
the Ctrl+T window, or after its player misses the host's limit, **Hand over a colony after its player is away
(days)** in Timber Together's settings (0, never, by default). Its player can then found a new colony with
**Ctrl+K**.

## Trading Posts

Build a **Trading Post** (District Management; 10 logs, no science) between your road and another colony's, one
half's door on each road. It's the only place two colonies' roads meet, and it starts trading once both reach it.

1. **Offer.** Select your half. Choose what **You give** and **You get** (0 to 100 of each a round) and how many
   **Rounds**, or tick **Repeat until cancelled**. A side at 0 is a gift. **Keep at least** holds back a reserve so
   a long deal never empties your stock. Then **Make offer**.
2. **They accept** on their half, or decline; a declined offer stays in your form, ready to adjust.
3. **The beavers carry it out.** Each colony's workers bring its side to its own half, and when both are in, the
   round crosses at once. Nothing is given before what it was traded for is in.

Science (with separate science) and adult beavers can be traded the same way. **Cancel exchange** needs the other player to agree; whatever
waits on each half then goes home. One exchange runs per post at a time: build more posts for more.

**Ctrl+T** (or the **Trade** button, top right) lists your posts and every colony, with its food and water in days,
what it is **looking for**, and who looks after it. Set what your colony is looking for there.

## One shared colony

Untick **Separate colonies** on the New Game page and everyone builds one colony together, as in ordinary
BeaverBuddies. Later, a player other than the host can split off once: Esc → **Found your own colony**. The game then
has separate colonies for good: the shared colony stays the host's, and science stays one pool.

## Controls

| Key | Action |
|---|---|
| **Ctrl+K** | Found your colony |
| **Ctrl+L** | Show every colony's roads in its color |
| **Ctrl+T** | Trading Posts and colonies |
| **Home** | Back to your colony (click a name in the connection panel to go to that player) |
| **Ctrl+Shift+J** | Write a diagnostics report |

Change them under Options → Bindings → **Timber Together**. **Ping Location**, **Toggle connection panel** and
**Chat: start typing** have no key until you set one there.

## Co-op basics

- **Steam invites** need both players online in Steam and owning Timberborn there.
- **Direct IP** needs port **25565** forwarded to the host, or a virtual LAN such as Hamachi. Anyone who can reach
  that port while you host can join, unverified: open it only for people you trust.
- **The connection panel** (top left) shows each player and their colony, ping, sync and tick rate, with chat and a
  speed boost for everyone.
- **Other mods** must match on every computer, at the same versions. You get a warning when they differ; a mod that
  changes the simulation will make the games drift apart.
- **Dev mode** desyncs the game, except its instant unlock, *Finish now* and *Add 1000 Science*.
- **After a Timberborn update** that changes something this mod corrects, co-op stops at load and names it (single
  player carries on). Update Timber Together.
- The game ends only when every beaver on the map is gone. Much of the text this mod adds is English only.

More: [Steam invites](STEAM-INVITES.md), [the connection panel](CONNECTION-PANEL.md),
[player cursors](PLAYER-ACTIVITY.md), and the full colony rules in [TWO-COLONIES.md](TWO-COLONIES.md).

## Troubleshooting

- **Multiplayer build mismatch:** someone has a different build. Everyone installs the same zip and restarts.
- **Someone dropped out or wants in late:** the host chooses Esc → **Save and Rehost**; they choose **Rejoin**, or
  join from **Join co-op game**.
- **A desync:** the host chooses **Save and Rehost**; guests choose **Reconnect (wait for Rehost)**.
- **"That would join another colony's roads":** keep your roads a cell apart from theirs, or link them with a Trading
  Post.
- **A Trading Post says *Not trading yet*:** each half needs a different colony's road at its door.
- **Ctrl+K says this is one shared colony:** a player other than the host splits it with Esc → **Found your own
  colony**.

**Reporting a problem:** open an [issue](https://github.com/timbermods/TimberTogether/issues) (here, not with the
original BeaverBuddies) with every player's `Player.log` from `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn`
and their newest report from the `TimberTogether-Reports` folder beside it. **Ctrl+Shift+J** writes a report any
time; a desync in a separate-colonies game writes one on every computer. The
[troubleshooting page](https://timbermods.github.io/TimberTogether/troubleshooting.html) has more.

Building it yourself, or curious how it works? See [DEVELOPING.md](DEVELOPING.md).

## Credits and license

Timber Together is a modified version of [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies)
([Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3293380223)), created by **Thomas Price (thomaswp)** with contributions from Robin, Slide, Phil Lehmkuhl,
SamuZad, Joe Stead, Zibo Ye, Dasker and Tarensaror. The multiplayer this all rests on is theirs (keeping every
player's game in step, the connections, the desync checks) and so is much of the code: this mod would not exist
without their work. It is built through the
[BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork).
[CREDITS.md](CREDITS.md) has the full credits.

Licensed under GPL-3.0, like the original ([License.txt](License.txt)); `License.txt` and `CREDITS.md` ship in the mod
folder. Every original commit keeps its author in the repository history, and every change is listed in
[STABILITY-CHANGELOG.md](STABILITY-CHANGELOG.md). An unofficial community mod for Timberborn, not affiliated with or
endorsed by Mechanistry or by the authors of BeaverBuddies.
