# BeaverBuddies MultiColony

**Two players, one map, a colony each.** A Timberborn co-op mod where each player runs their own colony (their own
district, beavers, buildings and land) on a shared map, and the two colonies trade goods through District
Crossings on the border. Works on the game's standard maps and on BeaverBuddies multi-start maps.

![Timberborn 1.1.2.4](https://img.shields.io/badge/Timberborn-1.1.2.4-2a4034?labelColor=172620&style=flat-square) ![Status: alpha](https://img.shields.io/badge/status-alpha-e0812f?labelColor=172620&style=flat-square) [![GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-2a4034?labelColor=172620&style=flat-square)](License.txt)

[Install](#install) · [Start a two-colony game](#start-a-two-colony-game) · [Playing](#playing-your-colony) · [Controls](#controls) · [Troubleshooting](#troubleshooting-and-reporting-problems) · [Full rules](TWO-COLONIES.md) · [Changelog](STABILITY-CHANGELOG.md)

> [!WARNING]
> **Alpha: nobody has played this yet.** The rules and the networking are covered by automated checks, but no
> part of the separate-colonies mode has been seen in a real game. Play on a copy of your save, keep backups, and
> please report what you find ([how](#troubleshooting-and-reporting-problems)).

MultiColony is built on the [BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork)
(1.1.10), which is built on the original [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies). Everything
those do still works: Steam invites, the connection panel and chat, pings, player cursors, and ordinary
shared-colony co-op. Separate colonies are opt-in per new game, and old saves play exactly as before.

## Install

**You need:** Timberborn **1.1.2.4** (Steam version, Windows is what has been tested) with the **Harmony** and
**Mod Settings** mods enabled. **Every player** needs the same game version and **the exact same download** of this mod.

1. Download the `BeaverBuddies-MultiColony-….zip` from the [Releases page](https://github.com/timbermods/BeaverBuddies-MultiColony/releases), or use the zip you were sent.
2. **Close Timberborn.**
3. In `Documents\Timberborn\Mods`, **delete every other BeaverBuddies folder** (the Stability Fork, the Workshop
   version, older MultiColony builds). They share one mod ID and conflict. Also unsubscribe from the Workshop
   BeaverBuddies if you have it.
4. Extract the zip and copy the `BeaverBuddies-MultiColony` folder into `Documents\Timberborn\Mods`.
5. Start Timberborn and enable **BeaverBuddies - MultiColony (alpha)** in the mod list.

To update, replace the folder with the new download. Both players must update together: a player with a different
build cannot join.

## Start a two-colony game

**1. Host: settings.** Main menu → **Mod Settings → BeaverBuddies**:

- **Separate colonies (alpha)** must be ticked (it is by default);
- set **Colony the host plays** to **Colony 1** (the guest then plays colony 2).

**2. Host: pick a game.** Any save works, new or old, on any map:

- **A standard map, or any existing save:** your colony plays as usual. Your friend founds theirs after joining
  (step 5), whenever they like, once.
- **A new game on a BeaverBuddies multi-start map** (two or more starting locations): both colonies start at once,
  one at each of the first two starting locations. Nothing to found.

**3. Host: save, then host.** Save the game. Open **Load Game**, select that save and choose **Host co-op game**
(instead of Load). Invite your friend with **Invite Friends** (Steam), or give them your IP address (port **25565**).

**4. Guest: join** by accepting the Steam invite (or **Join co-op game** → the host's IP). Your game receives the
host's save and loads it. **Do not unpause until everyone is in**: nobody can join a game that has already started.

**5. Guest: found your colony** (unless the game started with two colonies). When you join, a message offers to
**place your district center**. Choose **Place district center**, or cancel and press **Ctrl+K** whenever you are
ready (there is no time limit), then click anywhere on the map:

- It costs nothing and needs no science. It appears **already built**, with starting food, water, adults and
  children: the ones the new game gave the host, or the game's Normal difficulty for a save that did not record them.
- It must be far enough from the existing buildings that they all stay on the host's side of the new border. The
  preview turns red (*Too close to the other colony's buildings*) where it may not go.
- It can be done once. After that, colony 2 exists and **Ctrl+K** says so.
- Until then you play together as one shared colony, except in a game created fresh with this mode on, where you can
  only found your colony (speed, chat and pings still work). The more gets built, the less room there is: found early.

Everyone sees *Colony 2 has been founded.* The connection panel now shows each name with its colony, e.g.
*Alex (colony 2)*.

**6. Unpause and play.**

## Playing your colony

### Your land and the border

The map is split in two by a **straight line along the map grid, halfway between the two colonies' district centers**. Everything on your side
is yours: you can build there, and your beavers and districts live there. Everything on the other side is your
friend's.

Along the border runs a **border strip**, the row of tiles on each side touching the other colony. Only a
**District Crossing** may be built on the strip, so the two colonies' roads never touch. That applies at every
height too, including platforms, stairs and bridges.

**Press K** to show the strip (colony 1 blue, colony 2 orange). It also shows by itself whenever a tool is active,
such as building, planting, cutting or demolishing.

### What you can and can't do

| | Your colony | The other colony |
|---|---|---|
| Build, demolish, place paths | ✔ | ✖ preview turns red |
| Pause, priorities, workers, recipes, stockpiles, floodgates, automation, ziplines | ✔ | ✖ *That belongs to the other colony.* |
| Migration and distribution settings | ✔ | ✖ |
| Tree cutting, planting, clearing, demolition areas | ✔ | only your side is marked, even if you drag across |
| Select buildings, open their panels, look around | ✔ | ✔ |

**Shared by both:** game speed and pause, working hours, science and unlocks, renaming, pings, chat and saving.

The host double-checks every action before anyone's game carries it out, so a refused action never happens anywhere.

### Trading through a District Crossing

1. Press **K** to see the border and pick three free, level tiles along it.
2. Either player picks the **District Crossing** and places the pair so the two halves meet **exactly at the
   border**, one half on each colony's strip. Anywhere else the preview is red.
3. **Each player builds a path from their roads to the entrance of the half on their side.** Each colony's
   beavers build their own half; a half with no path to it is never built.
4. Open the **Distribution** tab (**F8**, or the button on the crossing's panel). In a two-colony game **every good
   starts at import *Disabled***, so nothing crosses yet:
   - **To receive a good**, set it to *Auto* or *Forced* import in **your** district.
   - **To stop giving a good**, raise its **export threshold** in your district. At the top of the slider, nothing leaves.

Each player can only change their own districts. If you build a second district of your own, set its imports too,
or goods won't move between your own districts either.

Deleting either half of a crossing deletes both, as in the base game.

### Beavers

Beavers never migrate between the two colonies on their own, even through a crossing, and moving them to the other
colony by hand is refused. Migration between your own districts works as normal.

### Rehosting and later sessions

- Saves keep the colonies, the border, crossings and trade settings.
- **Seats are chosen by the host's setting each session.** When you rehost, keep **Colony the host plays** on the
  colony you've been playing. If your friend hosts next time, they set it to the colony they play.
- After a desync the host uses **Save and Rehost** as usual; everyone rejoins.

## Controls

| Key | Action |
|---|---|
| **K** | Show or hide the colony border |
| **Ctrl+K** | Found your colony (colony 2's player, once, any time until colony 2 exists) |
| **Ctrl+Shift+K** | *Debug only:* the host acts as the other colony, for testing alone (needs **Always Use Detailed Logging**) |

All of these can be changed under **Options → Bindings → BeaverBuddies**. The co-op keys **Ping Location**, **Toggle
connection panel** and **Chat: start typing** are unbound until you set them there.

## Settings

In **Mod Settings → BeaverBuddies**. Only the host's settings matter for these:

| Setting | What it does |
|---|---|
| **Separate colonies (alpha)** | On by default. New games on a two-start map get a colony per start; in any other hosted game the second player may found their colony once. Off: one shared colony, as in the Stability Fork. |
| **Colony the host plays** | *Colony 1* or *Colony 2*. Guests play the other one. Read when hosting starts. |

Everything else (Steam, the connection panel, speed limits, cursors, detailed logging) works as in the Stability
Fork; see [Co-op basics](#co-op-basics).

## Good to know

- **Two players.** Only two colonies are supported. A third player can join but shares colony 2. On a map with
  more than two starts, only the first two become colonies.
- **Shared science.** Research points and unlocks are shared between both colonies.
- **The game ends** only when every beaver on the map is gone, not per colony.
- **Beavers work by range.** A lumberjack or gatherer near the border can work on the other side (for example,
  cutting trees your friend marked). The rules only cover what players do.
- **The split is a straight north-south or east-west line** halfway between the two district centers, so where
  colony 2 is founded decides how the map is shared.
- **Single player:** without a co-op session nothing is refused and no colony can be founded. Host the game (even
  alone) to play the mode.
- **Performance:** two colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest
  from the connection panel.
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

- **Can't join / stuck on "Receiving map…":** both players must have the same zip. Reinstall from the same file and restart.
- **The guest can't do anything:** in a game created fresh with this mode, colony 2 must be founded first. Press **Ctrl+K**.
- **Ctrl+K says the host has turned off Separate colonies:** the host ticks **Separate colonies (alpha)** and rehosts.
- **Every placement is red for the guest:** check the host's **Colony the host plays** is set as you expect. The
  connection panel shows who plays which colony.
- **Reporting:** send `Player.log` from both players
  (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`), plus screenshots for anything on screen.
  Lines from this mode start with `[Colony]`. Step-by-step test scripts are in [ALPHA-TEST-SCRIPTS.md](ALPHA-TEST-SCRIPTS.md).

Report issues at [github.com/timbermods/BeaverBuddies-MultiColony/issues](https://github.com/timbermods/BeaverBuddies-MultiColony/issues),
not to the original BeaverBuddies project.

## How it works

- **The host decides.** Every player action goes through the host. The host writes which connection each action
  came from (a guest can't pretend to be someone else), checks it against the colony rules just before playing it,
  and then plays and forwards it, keeps only the player's own part of an area action, or drops it (logged as
  `[Colony] Refused …`). Guests never judge, so the two games can't disagree.
- **Land is a formula** of the two saved district-center positions, in whole numbers, so every computer agrees.
- **Founding** is an ordinary multiplayer action: every computer creates the district center and the same beavers
  at the same moment.
- The save gets one small extra entry (the mode, the colony positions and the starting settings). Shared-colony
  games save nothing new.

Design notes and the implementation plan: [design/TWO-COLONY-ALPHA-PLAN.md](design/TWO-COLONY-ALPHA-PLAN.md).

## Building from source

1. Clone this repository and copy `BeaverBuddies/env.props.windows-template` to `BeaverBuddies/env.props`; point it
   at your Timberborn install and the Workshop copies of Harmony and Mod Settings.
2. Restore once:
   `dotnet restore BeaverBuddies/BeaverBuddies.csproj -s https://api.nuget.org/v3/index.json -s https://nuget.bepinex.dev/v3/index.json`
   (and the same for `StabilityTests` and `RuntimeChecks`).
3. Build with `--no-restore`, choosing where the mod folder goes:
   `dotnet build BeaverBuddies/BeaverBuddies.csproj -c "Release Steam" --no-restore -p:BeaverBuddiesModsPath="<folder>\BeaverBuddies\"`
   (`-c Release` for a build without Steam networking).
4. Checks: `dotnet run --project StabilityTests --no-build` (241 headless checks, 31 of them for separate colonies),
   `dotnet run --project RuntimeChecks --no-restore -- <built BeaverBuddies.dll> <Timberborn_Data\Managed> <Harmony folder> <Mod Settings Scripts folder>`
   (86 checks against the game's own assemblies), and
   `python -m unittest discover -s RuntimeChecks -p "test_water_snapshots.py"`.

These checks can't start Unity or prove full multiplayer determinism; in-game testing is what confirms behavior.

## Credits and license

Built on the [BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork) and the
original [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies) by thomaswp and contributors, who designed the
multiplayer this all rests on. Licensed under GPL-3.0 ([License.txt](License.txt)); authorship is preserved in the
repository history. Every change is listed in [STABILITY-CHANGELOG.md](STABILITY-CHANGELOG.md).
