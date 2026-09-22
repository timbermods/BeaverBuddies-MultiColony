# Publishing on the Steam Workshop

This is the text for the mod's own Workshop page, and how to publish it. Nothing here has been uploaded.

## Before the first upload

- The mod has its own id (`timbermods.BeaverBuddiesMultiColony`), and there is no `workshop_data.json` in the mod
  folder, so Timberborn's uploader creates a **new** Workshop item. (Earlier builds carried the original
  BeaverBuddies item's number; that file is gone.) After the first upload, Timberborn writes a new
  `workshop_data.json` into the uploaded folder: keep that one for later updates.
- Upload from the built mod folder (`BeaverBuddies-MultiColony`, as in the release zip), from Timberborn's mod
  manager.
- Tags: Multiplayer, Gameplay. Dependencies: Harmony, Mod Settings.
- Preview image: `thumbnail.png` (consider one of your own; the current one is inherited).
- Credit the original authors in the description (below).

## Title

BeaverBuddies MultiColony (beta): co-op with a colony each

## Description

Play Timberborn together, each with your own colony, on one map.

**Each player runs their own colony:** their own districts, beavers, stock, working hours and (if the host chooses)
science and unlocks. Your screen shows your colony; your beavers work for your colony only; nobody can change anyone
else's colony.

**No borders:** build and plant anywhere, right up to another colony's buildings. The one rule: two colonies' roads
never join, except through a trading post. Ctrl+L shows each colony's roads in its color.

**Colonies meet at trading posts:** a building of its own (10 logs, no science), placed between two colonies' roads,
one road end on each side. Offer "100 logs for 25 gears, 4 rounds" (or science, or beavers, or a gift), the other
player accepts, and both colonies' beavers carry it out: each side's goods wait on its own half and cross all at
once when both are in. Standing deals repeat by themselves, with a reserve so they never starve you; ending one early
takes both players. Say what your colony is looking for, see at a glance how many days of food and water each colony
has, and offer the last exchange again in one click.

**Away for the evening?** Ask a friend to look after your colony: they switch into it and back, and it is not handed
over while they are in the game.

**Found your colony anywhere:** on a standard map the host starts with the colony that is there; every other player
founds theirs once the host unpauses (after a new game's waiting room, at once), free and already built, with starting beavers. Colonies are remembered by
Steam account, whoever hosts. A colony whose player stops playing, or that dies out, is handed to another player,
and its player can found again.

**Desyncs are caught at once:** colony state is compared with the host's every tick, so a game that drifts stops
the tick it happens instead of much later.

**Or one shared colony:** turn *Separate colonies for new games* off and a game is the Stability Fork's shared
co-op, with none of the colony model running.

Up to four colonies. Built on BeaverBuddies by thomaswp and contributors, and on the BeaverBuddies Stability Fork.

**Beta:** please play on a copy of your save and report problems (with every player's Player.log) at
https://github.com/timbermods/BeaverBuddies-MultiColony/issues

**Do not enable another BeaverBuddies at the same time:** they cannot run together (the main menu tells you if one
is enabled). Every player needs the same version, and the same files: a copy with the Trading Post's files missing
or edited is refused at the join. Join while the host waits, paused, before they build anything.

Requires Harmony and Mod Settings.
