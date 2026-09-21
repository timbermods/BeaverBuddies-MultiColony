# BeaverBuddies MultiColony

**Two players, one map, a colony each.** A Timberborn co-op mod where each player runs their own colony (their own
districts, beavers, stock and science) on a shared map, and the colonies help each other through **trading posts**:
District Crossings where their roads meet. Co-op, not a race. Works on the game's standard maps and on BeaverBuddies
multi-start maps.

![Timberborn 1.1.2.4](https://img.shields.io/badge/Timberborn-1.1.2.4-2a4034?labelColor=172620&style=flat-square) ![Status: alpha](https://img.shields.io/badge/status-alpha-e0812f?labelColor=172620&style=flat-square) [![GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-2a4034?labelColor=172620&style=flat-square)](License.txt)

[Install](#install) · [Start](#start-a-game) · [Playing](#playing-your-colony) · [Trading](#trading-posts) · [Controls](#controls) · [Troubleshooting](#troubleshooting-and-reporting-problems) · [Full rules](TWO-COLONIES.md) · [Changelog](STABILITY-CHANGELOG.md)

> [!WARNING]
> **Alpha.** Hosting, joining over Steam and founding a second colony have been played. This version's model
> (colonies as owned districts, trading posts, separate science) has **not been played yet**; it is covered by
> automated checks. Play on a copy of your save, keep backups, and please report what you find
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
   version, older MultiColony builds). They share one mod ID and conflict. Also unsubscribe from the Workshop
   BeaverBuddies if you have it.
4. Extract the zip and copy the `BeaverBuddies-MultiColony` folder into `Documents\Timberborn\Mods`.
5. Start Timberborn and enable **BeaverBuddies - MultiColony (alpha)** in the mod list.

To update, replace the folder with the new download. Every player must update together: a player with a different
build cannot join.

## Start a game

**1. Host: settings.** Main menu → **Mod Settings → BeaverBuddies** (both on by default):

- **Separate colonies (alpha)**: each player runs their own colony.
- **Separate science and unlocks per colony (alpha)**: each colony earns its own science and unlocks its own
  buildings. Chosen when a separate-colonies game begins, then fixed for the save.

**2. Host: pick a game.** Any save works, new or old, on any map.

- **A new game on a BeaverBuddies multi-start map:** start 1 is the host's colony, start 2 the next player's, and so on.
- **Anything else** (a standard map, an existing save): the district centers already there are the host's colony.
  Every other player founds theirs after joining (step 5).

**3. Host: save, then host.** Save the game. Open **Load Game**, select that save and choose **Host co-op game**
(instead of Load). Invite your friend with **Invite Friends** (Steam), or give them your IP address (port **25565**).

**4. Guest: join** by accepting the Steam invite (or **Join co-op game** → the host's IP). **Do not unpause until
everyone is in**: nobody can join a game that has already started.

**5. Guest without a colony: found yours.** A message offers to **place your district center** (or press **Ctrl+K**
later, whenever you are ready). Click anywhere its entrance is not on another colony's roads. It is free, needs no
science, and appears **already built**, with starting beavers, food and water. You can found once.

**Your colony is remembered.** The save knows each player by their Steam ID (or an id kept on your computer without
Steam): you get the same colony every time, whoever hosts. The connection panel shows each name with its colony.

## Playing your colony

- **Build anywhere.** There is no border and no land of your own.
- **A colony is its districts.** Your district centers are yours; buildings belong to the district they are in,
  beavers to the district they live in, goods to the district that stores them.
- **Your roads never join another colony's**, except through a District Crossing (the game itself refuses anything
  else).
- **You change your own colony.** Another player's buildings, districts, migration and distribution are refused
  while they are playing (*That belongs to another colony…*). While they are away, you may look after their colony.
- **Your screen shows your colony only:** the top bar (goods, population, wellbeing, science), the batch control
  lists (F1 to F10), alerts and the notification journal.
- **Shared by everyone:** speed, pause, working hours, pings, chat, saving, and the map: water, droughts, badwater,
  trees and berries. A dam upstream changes what flows downstream. Stored water belongs to its colony.
- **Beavers stay in their colony**, except when you send them to another colony by hand (the Migration tab), which
  is how a failing colony is rescued.

## Trading posts

Build a **District Crossing** where your roads come close to another colony's: one half on each side. In a
separate-colonies game it needs **no science and costs 10 logs**. Each colony's beavers run their own half.

- **Goods move by each colony's Distribution settings** (the **Distribution** tab, F8): the receiving colony opens a
  good's **import** (*Auto* or *Forced*); the giving colony limits it with the **export threshold**. **Every good starts
  at import Disabled**, so nothing moves until the receiving player opens it (between your own districts too).
- **The trading-post panel** under the crossing shows what the other colony could use, **Give 10** buttons that send
  goods whatever their import settings, what has passed each way, and (with separate science) **Give 50 / 250
  science**.
- Either player may remove a crossing.

Details: [TWO-COLONIES.md](TWO-COLONIES.md#trading-posts).

## Controls

| Key | Action |
|---|---|
| **Ctrl+K** | Found your colony (a player without one, once) |
| **Ctrl+Shift+K** | *Debug only:* the host acts as the next colony, for testing alone (needs **Always Use Detailed Logging**) |

Both can be changed under **Options → Bindings → BeaverBuddies**. The co-op keys **Ping Location**, **Toggle
connection panel** and **Chat: start typing** are unbound until you set them there.

## Good to know

- **Up to four colonies.** More players join as helpers of the host's colony.
- **The game ends** only when every beaver on the map is gone, not per colony.
- **Beavers work by range:** a lumberjack or gatherer can work on anything it reaches, including another colony's
  marked trees.
- **Two placements at the same moment** can join two districts' roads once both are built; both players are warned
  (*Two districts' roads have been joined…*): remove the joining path or building at once.
- **Bot worker types** stay unlocked for everyone; whoever unlocks one pays.
- **Single player:** without a co-op session nothing is refused and no colony can be founded. Host the game (even
  alone) to play the mode.
- **Performance:** more colonies mean more to simulate. Prefer a smaller map; the host can ease off for a slow guest
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

- **Can't join / stuck on "Receiving map…":** every player must have the same zip. Reinstall from the same file and restart.
- **Ctrl+K says the host has turned off Separate colonies:** the host ticks **Separate colonies (alpha)** and rehosts.
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
- **Ownership is saved on district centers**; everything else follows from districts, which every computer
  simulates identically. Science keeps one pool and one unlock set per colony in the save.
- The save gets a few small extra entries (the mode, who plays which colony, owners, science, the trade ledger).
  Shared-colony games save nothing new.

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
