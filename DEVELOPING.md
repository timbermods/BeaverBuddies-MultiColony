# Developing Timber Together

How the mod keeps separate colonies in step, and how to build and check it. For playing, see the [README](README.md); for the full colony rules, [TWO-COLONIES.md](TWO-COLONIES.md).

## How it works

- **The host decides.** Every player action goes through the host. The host writes which connection each action
  came from (a guest can't pretend to be another connection), seats players by their stable id, writes the actor's
  colony into the action, checks it against the rules just before playing it, and then plays and forwards it, keeps
  only the actor's part of a list action, or drops it (logged as `[Colony] Refused …`). Guests never judge, so the
  computers can't disagree.
- **Which joins are verified.** A guest who joins **over Steam** is seated by the Steam ID that Steam proved for
  its connection: a guest saying another Steam ID is refused, and one whose game could not read its own Steam ID is
  seated by the proved one. A guest who joins **by IP (direct connection)** proves nothing, so the host takes the id
  it says: someone who copies another player's id (every player's id is shared with the session and kept in the
  save) can take that player's colony, and someone who rejoins under new ids while the host waits at the start can
  reserve the free colonies. The direct-IP port (**25565**) is open whenever you host, **Steam invites or not**:
  anyone who can reach it (on your network, through a forwarded port, or over IPv6 if your firewall lets them in)
  joins unverified. Forward that port, or let it through your firewall, only while you play by IP with people you
  trust. Either way a guest can't say hello a second time as someone else, and an id with a line break or a `|` in
  it (no id the mod makes has one) is refused.
- **Ownership is saved**: on district centers, on every building (the colony that placed it) and on map marks.
  Beavers' choices of work read only these, identically on every computer.
- **Guests take the host's answers.** Whether a placement is still possible when it is played, what a founded colony
  starts with, the day's presence: each is decided once by the host and written into the action every computer
  plays. In a separate-colonies game every change to colony state folds into a running digest that the host sends
  with each heartbeat; a guest whose digest differs stops that tick.
- A separate-colonies save gets a few small extra entries (the mode, who plays which colony, owners, marks,
  science, working hours, exchanges, the trade ledger, which colony each journal entry is for). A shared-colony game's
  save has none: it is the same as the
  Stability Fork's.

- **The waiting room** keeps its players in a room of its own on the host's server, before any save exists: small
  messages of their own after the build check (a guest's hello and ready, the host's roster and progress), never game
  actions. At **Start Game** the host's computer makes the world as a single-player game, saves it at tick 0, sends
  those bytes to every player in the room, and loads the same bytes itself as the hosted game, as **Host co-op game**
  on a save does. The colony slot table is filled in the room's order before that first save.
- **A room opened in a game** is the same room in a window. A guest who joins from a game keeps playing alone until the
  host's save arrives: the join is held apart from the game (the game is not a co-op game while its player waits).
  A host moving a running co-op game to a room first tells every guest (a small message of its own, never a game
  action), then ends the session and opens the room on the same port, handing its Steam lobby to the room; each guest
  ends its session quietly and joins the room by itself.

Design notes and the plans: [design/PRE-GAME-LOBBY-PLAN.md](design/PRE-GAME-LOBBY-PLAN.md) (the waiting room),
[design/IN-GAME-HOSTING-PLAN.md](design/IN-GAME-HOSTING-PLAN.md) (the room in a game),
[design/TRADING-EXCHANGE-PLAN.md](design/TRADING-EXCHANGE-PLAN.md),
[design/TWO-COLONY-ALPHA-PLAN.md](design/TWO-COLONY-ALPHA-PLAN.md); the desync and network review:
[design/REVIEW-FINDINGS-1.4.0-beta11.md](design/REVIEW-FINDINGS-1.4.0-beta11.md); Wonders on the tick:
[BeaverBuddies/Doc/WonderTiming.md](BeaverBuddies/Doc/WonderTiming.md).

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
