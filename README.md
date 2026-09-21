# BeaverBuddies MultiColony

**Two players, one map, a colony each.** A Timberborn co-op mod where each player runs their own colony (their own
districts, land, beavers, stock, science and working hours) on a shared map. The colonies meet only at **trading
posts**: District Crossings where their roads come together, through which they barter goods. Co-op, not a race.
Works on the game's standard maps and on BeaverBuddies multi-start maps.

![Timberborn 1.1.2.4](https://img.shields.io/badge/Timberborn-1.1.2.4-2a4034?labelColor=172620&style=flat-square) ![Status: alpha](https://img.shields.io/badge/status-alpha-e0812f?labelColor=172620&style=flat-square) [![GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-2a4034?labelColor=172620&style=flat-square)](License.txt)

[Install](#install) · [Start](#start-a-game) · [Playing](#playing-your-colony) · [Trading](#trading-posts) · [Handover](#when-a-colony-is-handed-over) · [Controls](#controls) · [Troubleshooting](#troubleshooting-and-reporting-problems) · [Full rules](TWO-COLONIES.md) · [Changelog](STABILITY-CHANGELOG.md)

> [!WARNING]
> **Alpha.** Hosting, joining over Steam and founding a second colony have been played. This version's model
> (colonies with their own land, trading posts and barter, colony handover) has **not been played yet**; it is
> covered by automated checks. Play on a copy of your save, keep backups, and please report what you find
> ([how](#troubleshooting-and-reporting-problems)).

MultiColony is built on the [BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork)
(1.1.10), which is built on the original [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies). Everything
those do still works: Steam invites, the connection panel and chat, pings, player cursors, and ordinary
shared-colony co-op (turn **Separate colonies** off).

## Install

**You need:** Timberborn **1.1.2.4** (Steam version, Windows is what has been tested) with the **Harmony** and
**Mod Settings** mods enabled. **Every player** needs the same game version and **the exact same download** of this mod.

1. Download the `BeaverBuddies-MultiColony-….zip` from the [Releases page](https://github.com/timbermods/BeaverBuddies-MultiColony/releases), or use the zip you were sent.
2. **Close Timberborn.**
3. In `Documents\Timberborn\Mods`, **delete every other BeaverBuddies folder** (the Stability Fork, the Workshop
   version, older MultiColony builds), and unsubscribe from the Workshop BeaverBuddies if you have it. They change
   the same parts of the game and cannot run together; if one is still enabled, the main menu tells you which.
4. Extract the zip and copy the `BeaverBuddies-MultiColony` folder into `Documents\Timberborn\Mods`.
5. Start Timberborn and enable **BeaverBuddies MultiColony (alpha)** in the mod list.

This version has its own mod id, so its Mod Settings start from the defaults once.

To update, replace the folder with the new download. Every player must update together: a player with a different
build cannot join.

## Start a game

**1. Host: settings.** Main menu → **Mod Settings → BeaverBuddies** (both on by default):

- **Separate colonies (alpha)**: each player runs their own colony.
- **Separate science and unlocks per colony (alpha)**: each colony earns its own science and unlocks its own
  buildings. Chosen when a separate-colonies game begins, then fixed for the save.
- **Hand over a colony after its player is away (days)**: 7 by default, 0 for never (see
  [when a colony is handed over](#when-a-colony-is-handed-over)).

**2. Host: pick a game.** Any save works, new or old, on any map.

- **A new game on a BeaverBuddies multi-start map:** start 1 is the host's colony, start 2 the next player's, and so on.
- **Anything else** (a standard map, an existing save): the district centers already there are the host's colony.
  Every other player founds theirs after joining (step 5).

**3. Host: save, then host.** Save the game. Open **Load Game**, select that save and choose **Host co-op game**
(instead of Load). Invite your friend with **Invite Friends** (Steam), or give them your IP address (port **25565**).

**4. Guest: join** by accepting the Steam invite (or **Join co-op game** → the host's IP). **Do not unpause until
everyone is in**: nobody can join a game that has already started.

**5. Guest without a colony: found yours.** A message offers to **place your district center** (or press **Ctrl+K**
later, whenever you are ready). Other colonies' land shows as coloured outlines: place it **at least 20 tiles from
their buildings and paths**, so both colonies have room to grow. It is free, needs no science, and appears **already
built**, with starting beavers, food and water. You found once; you may found again only if your colony is handed
over.

**Your colony is remembered.** The save knows each player by their Steam ID (or an id kept on your computer without
Steam): you get the same colony every time, whoever hosts. The connection panel shows each name with its colony.

## Playing your colony

- **Your colony is your districts and your land**: every tile within 10 tiles of your buildings and paths that no
  other colony reached first. It grows as you build, and stops where another colony's land begins. Everything you
  place is yours from the moment you place it. **Every colony's land is outlined in its colour** while you hold a
  building, planting, cutting or demolishing tool, and any time with **Ctrl+L**.
- **You build, mark trees and plant on your own land or free land.** Never on another colony's land or right next to
  its roads (the preview turns red and says why). Building towards another colony does not take its land: your land
  ends where theirs begins.
- **You change only your own colony**, whether or not the other player is playing: their buildings, beavers,
  districts, marks and settings are refused (*That belongs to another colony.*).
- **Your beavers work only for your colony:**
  - builders build, demolish and pick up leftovers only for your colony;
  - lumberjacks cut only trees you marked;
  - foresters and farmers plant only on your marks;
  - gatherers, farmers and scavengers take only what grows on your marks, or wild things on land no other colony holds.
- **Your screen shows your colony only:** the top bar (goods, population, wellbeing, science), the batch control
  lists (F1 to F10), alerts, the notification journal, and the working hours (top right, and the clock's needle).
- **Your own working hours** and, with separate science, your own science, unlocks and bot worker types.
- **Beavers stay in their colony.** They never move to another colony, on their own or by the Migration tab.
- **Shared by everyone:** speed, pause, pings, chat, saving, and the map itself: water, droughts, badwater, weather.
  A dam upstream still changes what flows downstream.

## Trading posts

Build a **District Crossing** where your land meets another colony's: one half on each side of the edge, each
reached by its own colony's road on its own land. You may place it when part of it is on your land (or free land).
In a separate-colonies game it needs **no science and costs 10 logs**. Each colony's beavers run their own half.

Goods cross a trading post only through an **exchange** the two colonies agree on:

1. **Make an offer.** Select the crossing. In the **Trading post** section at the bottom of its panel, click what you
   give and what you ask for to choose from a grid of icons (with each colony's stock), and type the amounts (up to
   9999). For example: *1000 logs for 250 gears*. **Ask for 0 to give a gift**; give 0 to ask for help. Turn
   **Repeat** on for a standing deal that starts again each time it completes. Then **Make offer**.
2. **The other player accepts** (or declines) on the same panel. They get a notice when you make the offer.
3. **The beavers do the rest.** Each colony's crossing workers fetch their side's goods from their own storage and
   bring them to the crossing; the other colony's workers haul them away into theirs. Everything **moves in step**:
   neither side gets more than a tenth of its amount (at least 10) ahead of the other. When both amounts have
   crossed, both players get a notice (a repeating exchange simply starts its next round).

**Science and beavers** can be traded too, with the same offer form: science (with separate science) passes from
pool to pool, 25 at a time, and adult beavers move to the other colony's district, one at a time (the last adult
always stays).

**All your trading posts at once:** **Ctrl+T** (or **Trade** at the top right) lists every trading post of your
colony, its exchange and its progress, with a **Go to** button; and every colony, its population and whether its
player is playing.

- **One exchange at a time** per trading post; build more crossings for more at once. Either player may cancel an
  exchange at any time; what has crossed stays crossed.
- **Each half holds up to 100** of each good waiting to be hauled away (the game's own crossing holds 30). Staff both
  halves, and keep storage room for what you receive, and houses for beavers.
- **Import and export settings don't apply** at a trading post. They still move goods between your own districts,
  as in the game (with the bigger buffer: every crossing holds 100, in every game).
- The panel also shows what has passed each way and, with separate science, **Give 50 / 250 science**.
- Either player may remove a trading post.

Details: [TWO-COLONIES.md](TWO-COLONIES.md#trading-posts).

## When a colony is handed over

A colony whose player can't run it goes to another player, with its buildings, land, stock and science:

- **No beavers or bots left** for a whole day: to the nearest living colony.
- **Its player away:** once they have missed the number of in-game days of hosted co-op play set by the host (7 by
  default; days the host plays alone in single player, and the first day after loading, don't count): to the nearest
  colony whose player is playing.
- **By the host**, from the Ctrl+T window: any colony whose player is away, or that has no beavers (for example to
  a player whose Steam account changed).

The player who lost their colony gets a notice and may **found a new one** with **Ctrl+K**.

## Controls

| Key | Action |
|---|---|
| **Ctrl+K** | Found your colony (a player without one) |
| **Ctrl+L** | Show every colony's land (it also shows while you hold a building, planting, cutting or demolishing tool) |
| **Ctrl+T** | Trading posts and colonies |
| **Ctrl+Shift+K** | *Debug only:* the host acts as the next colony, for testing alone (needs **Always Use Detailed Logging**) |

All can be changed under **Options → Bindings → BeaverBuddies**. The co-op keys **Ping Location**, **Toggle
connection panel** and **Chat: start typing** are unbound until you set them there.

## Good to know

- **Up to four colonies.** More players join as helpers of the host's colony.
- **The game ends** only when every beaver on the map is gone, not per colony.
- **Two placements at the same moment** can join two districts' roads once both are built; both players are warned
  (*Two districts' roads have been joined…*): remove the joining path or building at once.
- **Single player:** without a co-op session nothing is refused and no colony can be founded. Host the game (even
  alone) to play the mode.
- **Performance:** more colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest
  from the connection panel. With **Always Use Detailed Logging** on, the log has one line a day per colony
  (population, land, exchanges), which helps with reports from long games.
- **New text is English only.**

## Co-op basics

Inherited from the Stability Fork. The full guides are in [STEAM-INVITES.md](STEAM-INVITES.md), [CONNECTION-PANEL.md](CONNECTION-PANEL.md)
and [PLAYER-ACTIVITY.md](PLAYER-ACTIVITY.md).

- **Steam invites:** both players online in Steam and owning Timberborn. Settings **Enable Steam Networking** and
  **Allow Friends to Join Directly via Steam**. Only friends who join the host's friends-only lobby can connect.
- **Direct IP:** the host forwards port **25565**, or both use a VPN such as Hamachi.
- **The connection panel** (top-left) shows each player, their ping, whether you're in sync, the tick rate and a
  chat box. Collapse it by clicking its title; hide or move it in Mod Settings.
- **Mismatched mods** are flagged when someone joins. It's a warning, but a mod that changes the simulation will
  cause desyncs, so match mod lists.
- **Desyncs** can still happen. The host uses **Save and Rehost**.

## Troubleshooting and reporting problems

- **Can't join / stuck on "Receiving map…":** every player must have the same zip. Reinstall from the same file and restart.
- **Ctrl+K says the host has turned off Separate colonies:** the host ticks **Separate colonies (alpha)** and rehosts.
- **"That is another colony's land":** that tile is within 10 tiles of their buildings or paths, and they were
  there first. Build on your side. To trade, put a District Crossing across the edge of your two lands.
- **Everything is refused right after joining:** the host has not seated you yet; wait a moment. If it persists,
  send the logs.
- **Reporting:** send `Player.log` from every player
  (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`), plus screenshots for anything on screen.
  Lines from this mode start with `[Colony]`. Step-by-step test scripts are in [ALPHA-TEST-SCRIPTS.md](ALPHA-TEST-SCRIPTS.md).

Report issues at [github.com/timbermods/BeaverBuddies-MultiColony/issues](https://github.com/timbermods/BeaverBuddies-MultiColony/issues),
not to the original BeaverBuddies project.

## How it works

- **The host decides.** Every player action goes through the host. The host writes which connection each action
  came from (a guest can't pretend to be someone else), seats players by their stable id, writes the actor's colony
  into the action, checks it against the rules just before playing it, and then plays and forwards it, keeps only
  the actor's part of a list action, or drops it (logged as `[Colony] Refused …`). Guests never judge, so the
  computers can't disagree.
- **Ownership is saved**: on district centers, on every building (the colony that placed it) and on map marks. Land
  is worked out from the buildings standing. Beavers' choices of work read only these, identically on every computer.
- The save gets a few small extra entries (the mode, who plays which colony, owners, marks, science, working hours,
  exchanges, the trade ledger). Shared-colony games save only the building owners.

Design notes and the plans: [design/TRADING-EXCHANGE-PLAN.md](design/TRADING-EXCHANGE-PLAN.md),
[design/TWO-COLONY-ALPHA-PLAN.md](design/TWO-COLONY-ALPHA-PLAN.md).

## Building from source

1. Clone this repository and copy `BeaverBuddies/env.props.windows-template` to `BeaverBuddies/env.props`; point it
   at your Timberborn install and the Workshop copies of Harmony and Mod Settings.
2. Restore once:
   `dotnet restore BeaverBuddies/BeaverBuddies.csproj -s https://api.nuget.org/v3/index.json -s https://nuget.bepinex.dev/v3/index.json`
   (and the same for `StabilityTests` and `RuntimeChecks`).
3. Build with `--no-restore`, choosing where the mod folder goes:
   `dotnet build BeaverBuddies/BeaverBuddies.csproj -c "Release Steam" --no-restore -p:BeaverBuddiesModsPath="<folder>\BeaverBuddies\"`
   (`-c Release` for a build without Steam networking).
4. Checks: `dotnet run --project StabilityTests --no-build` (headless),
   `dotnet run --project RuntimeChecks --no-restore -- <built BeaverBuddies.dll> <Timberborn_Data\Managed> <Harmony folder> <Mod Settings Scripts folder>`
   (against the game's own assemblies), and
   `python -m unittest discover -s RuntimeChecks -p "test_water_snapshots.py"`.

These checks can't start Unity or prove full multiplayer determinism; in-game testing is what confirms behavior.

## Credits and license

Built on the [BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork) and the
original [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies) by thomaswp and contributors, who designed the
multiplayer this all rests on. Licensed under GPL-3.0 ([License.txt](License.txt)); authorship is preserved in the
repository history. Every change is listed in [STABILITY-CHANGELOG.md](STABILITY-CHANGELOG.md).
