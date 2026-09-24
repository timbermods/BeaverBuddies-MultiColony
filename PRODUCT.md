# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Pairs (and small groups) of Timberborn players who want to play on one map together, but each run their own colony.
That includes people who already have a shared BeaverBuddies save they built together. Mostly non-technical.
Usually one person, the host, finds the mod, sets it up and sends the zip to a friend. The friend arrives with
nothing but that zip or a link. Right now the audience is beta testers: people willing to play an unfinished build
and report what breaks.

## Product Purpose

The website for **Timber Together**, a co-op multiplayer mod for Timberborn
(repo and releases: https://github.com/timbermods/TimberTogether).

The big idea: **two players, one map, a colony each.** Each player runs their own colony on a shared map: their own
districts, beavers, stock, science and working hours. The colonies meet only at **Trading Posts**, a building
placed between their two roads, where they barter goods. It is co-op, not a race. This is why the mod exists: most
co-op mods mean sharing one colony, and this one lets you be neighbors who trade.

Success, in order:
1. **Both players end up on the exact same build and in a two-colony game.** This is the primary goal.
2. **Good bug reports:** every player's `Player.log`, plus the `TimberTogether-Reports` folder next to it after a
   desync, sent as a GitHub issue.

## Positioning

Separate colonies that trade, not one shared colony. Other co-op mods, including the original BeaverBuddies and
the Stability Fork it is built on, put every player in one colony. Timber Together gives each player their own colony
on one map, links them only through Trading Post barter, and still offers ordinary shared-colony co-op too.

## Operating Context

- The host reads the site first, then sends the zip or a link to a friend. Guests land on the site cold and need
  the install steps and nothing else.
- Ways to play. The host chooses where the game is made, with the **Separate colonies** checkbox; none of this is
  in Mod Settings.
  - **New game, a colony each:** *Separate colonies* on the New Game difficulty page (under the game's own Tutorial
    checkbox), ticked by default; the page remembers the last choice. Under it: *Separate science and unlocks*
    (ticked) and *Mixed factions* (unticked).
  - **Split a shared save**, two ways, both for good:
    - When hosting it: the save's **Co-op Game** page shows *Separate colonies* (unticked) with *Separate science and
      unlocks*; ticked, the game becomes separate colonies at Start.
    - Later: a player other than the host chooses **Found your own colony** in the game menu (Esc), once; science
      stays shared.
    - Either way **the existing colony, with its science and unlocks, stays whole as the host's. Each other player
      starts from scratch** (a new district center with starting beavers, food and water). Nothing divides a
      300-beaver colony into halves.
  - **One shared colony:** *Separate colonies* unticked; ordinary BeaverBuddies co-op, with Timber Together's
    improvements.
  - The host's one colony setting in **Mod Settings → Timber Together** is *Hand over a colony after its player is
    away (days)*, 0 (never) by default.
- **Features the site must include** (confirmed by the maintainer, 2026-09-23), alongside separate colonies and
  Trading Posts:
  - **The Co-op Game page:** every game is hosted through it. A new game (**Host co-op game** beside Start), a save
    (**Host co-op game** on the main menu, under Load game, opens the game's save box with a gold line saying what the
    save is), or the game you're playing (Esc → **Host co-op game**; for a co-op host, **Save and Rehost**, with
    guests' **Rejoin** / **Reconnect** landing them on the page). The host invites (Steam or IP), friends ready up,
    and Start Game loads everyone together. Guests find the host's game under **Join co-op game** on the main menu.
  - **Mixed factions:** the *Mixed factions* checkbox under *Separate colonies* on the New Game page, unticked by
    default. Each player picks Folktails or Iron Teeth for their own colony on the Co-op Game page.
  - **Away players keep their colony:** by default a colony is never handed over while its player is away. A friend
    can look after it (Ctrl+T), and automatic hand-over after a set number of days away is opt-in, for groups where
    someone may not come back.
- Inherited from the Stability Fork: Steam invites, the connection panel and chat, pings, player cursors, desync
  fixes.
- Reporting: `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log` from every player. In a
  separate-colonies game a desync also writes a diagnostics report to `TimberTogether-Reports` in that same folder
  (Ctrl+Shift+J writes one on demand). Issues go to this repo, not to the original BeaverBuddies.

## Capabilities and Constraints

**Install facts the site must make impossible to miss:**
1. Download the `TimberTogether-….zip` from **GitHub Releases** (under Assets, not "Source code"), or use
   the zip a friend sent. The releases are pre-releases, so link to `/releases`, never `/releases/latest`.
2. **Close Timberborn.**
3. In `Documents\Timberborn\Mods`, **delete every other BeaverBuddies folder** (the Stability Fork, the Workshop
   version, any other Timber Together download). They conflict. **Unsubscribe from the Workshop BeaverBuddies.**
4. Copy the `TimberTogether` folder into `Documents\Timberborn\Mods` and enable
   **Timber Together**. Requires the **Harmony** and **Mod Settings** mods.
5. **Every player has the same game version and the exact same download. Update together:** a different build
   cannot join (*Multiplayer build mismatch*).

**Status:** beta, current version **1.4.0-rc4** (a release candidate). The site names the newest published release,
and `docs/assets/release.js` fills the version badges from GitHub. 1.4.0 has not been released yet. It is built for
Timberborn **1.1.2.4**, and only the Steam version on Windows has been tested. It passes a large automated test
suite, but real-game play is still limited: many features are covered by checks, not yet played. Players should play
on a copy of their save and keep backups. Be honest about this without scaring people off.

**Describe the mod as it is now, for a fresh game.** User-facing pages never say which version added or changed a
feature ("since beta19", "rc2 made…"). They never mention earlier builds, older saves or save compatibility. The
version history lives in the changelog only.

**Terminology:** Trading Posts (never "District Crossings"). No land, borders or territory: the only rule between
colonies is that their roads never join, except through a Trading Post.

**Stack and hosting:** static site in `docs/` (plain HTML, CSS and small vanilla JS; no build step), served by GitHub
Pages from `main:/docs` at https://timbermods.github.io/TimberTogether/. It must stay fast, lightweight
and mobile-friendly. It is one of the timbermods sites (the Stability Fork's and MixedStorage's are siblings).

**Sources of truth:** the repo `README.md`, `TWO-COLONIES.md` (full rules) and the release notes. Where the site and
the README disagree, flag the mismatch; don't guess.

## Brand Commitments

- **Credit, clearly and on the site:** built on the BeaverBuddies Stability Fork
  (https://github.com/timbermods/BeaverBuddies-Stability-Fork), which is built on thomaswp's original BeaverBuddies
  (https://github.com/thomaswp/BeaverBuddies). GPL-3.0.
- **Voice:** a fellow player inviting friends to test something new and ambitious. Warm, clear, honest about beta
  status, never overpromising.
- **No official Timberborn logos or key art.** Small in-game item icons (goods, the beaver) are allowed in UI
  replicas such as the Trading Post demo.
- Mod name as shown in game: **Timber Together**.

## Evidence on Hand

- `Media/`: the mod icon (`Icon-full.png`, `IconBG.png`), `logo.jpg`, `thumbnail-large.png`.
- `docs/assets/goods/`: item icons (Logs, Science and the beaver taken from the game; Gears, Berries and Carrots from
  the MixedStorage site).
- An interactive Trading Post panel replica already on the site (`docs/assets/trade-demo.js`, `game-panel.css`).
- **No gameplay screenshots or clips yet.** Future work leaves marked slots for the maintainer's own shots (two
  colonies side by side on one map, a Trading Post barter, the colony settings) and never fakes them.
- No testimonials, player counts, download numbers or press exist. Don't invent any.

## Product Principles

1. **Same build first.** Every page helps both players end up on the exact same download and game version. The
   "delete other BeaverBuddies folders" and "update together" steps are never buried.
2. **Neighbors who trade.** Lead with separate colonies and Trading Post barter. Shared-colony co-op is supported,
   but secondary.
3. **Honest beta, described as it is now.** Say what has been played and what hasn't. Ask for backups and reports
   plainly, without alarm. No version history and no old-save caveats on user pages.
4. **Written for the friend who got a zip.** Non-technical, step by step, exact folder and setting names as they
   appear in game.
5. **A report is a contribution.** Make sending `Player.log` and the reports folder to GitHub issues easy and
   welcome.
