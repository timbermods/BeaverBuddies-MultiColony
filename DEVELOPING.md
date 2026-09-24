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
  save) can take that player's colony, and one person could hold several room connections (the host sees each in the
  room and can remove it). The direct-IP port (**25565**) is open whenever you host, **Steam invites or not**:
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
  save holds only the mode and its starting settings.

- **The waiting room** keeps its players in a room of its own on the host's server, before any save exists: small
  messages of their own after the build check (a guest's hello and ready, the host's roster and progress), never game
  actions. At **Start Game** the host's computer makes the world as a single-player game, saves it at tick 0, sends
  those bytes to every player in the room, and loads the same bytes itself as the hosted game, as **Host co-op game**
  on a save does. The colony slot table is filled in the room's order before that first save.
- **A room opened in a game** is the same room in a window. A guest who joins from a game stays in a single-player game,
  paused under the room window, until the host's save arrives: the join is held apart from the game.
  A host moving a running co-op game to a room first tells every guest (a small message of its own, never a game
  action), then ends the session and opens the room on the same port, handing its Steam lobby to the room; each guest
  ends its session quietly and joins the room by itself.

## Separate colonies under the hood

- **Joining closes at Start.** Every game is hosted through the Co-op Game room (a game hosts itself by saving and
  opening its room over the game, `RehostingService`). The host's first message says so
  (`InitializeClientEvent.joiningClosedAtStart`), so `ColonyRules.WaitsForStart` holds nothing back. The refusals
  for a game still open to joiners stay as a guard.
- **The room** is a phase of the host's server before any save exists (`TimberNet`: `LobbyRoom`, `LobbyFrames`,
  `LobbyInbox`). After the build check a guest waits there; only its hello and ready are read. The host's roster
  and progress go out every second on a lane of their own, marked by a -1 length so a save's bytes on the wire are
  unchanged. The scene that makes a new world is single-player (`LobbySession`, `LobbyWorldMaker`); the save is
  written at tick 0, sent, and loaded by everyone. The join check covers the mod's own files (`Buildings`,
  `TemplateCollections`) as well as the game and mod versions.
- **The room in a game.** `LobbyPage` builds the room's content once, in the main menu's page frame or a game's window
  frame (`Common/NamedBoxTemplate`, with the main menu's style sheets added by `InGameLobby`). A join made in a game
  is held by `ClientConnectionService`, not installed as the session, until the host's save arrives (`LoadMap`).
  A host moving a running game sends each guest a control frame of its own (`MoveFrames`, never replayed or hashed)
  before its server closes; a guest that read it treats the close as the move (`TimberClient.HostMoved`) and rejoins.
  The host's Steam lobby is handed to the room (`ServerEventIO.HandLobbyToRoom`), and the room's server rebinds the
  same port. The exit save at Start is the game's own (`Autosaver.CreateExitSave`), as `ExitSaveRules` says.
- **Seating** (`ColonySlotTable.SeatHello`, `CheckHello`). Over Steam, a guest's stable id is held to the Steam ID
  Steam proved for the connection (`TimberServer.VerifiedIdOf`). A direct connection proves nothing, so its hello is
  taken at its word. A seated connection can't say hello again as someone else; an id with a line break or `|`, or
  over 64 characters, is refused. Refusals log as `[Colony] Refused PlayerHelloEvent …`.
- **Mixed factions** (`BeaverBuddies/Factions`). The mode is decided before the game's collections load
  (`MixedFactions`: the room that makes the world, the solo New Game's Start, or the save's
  `BeaverBuddies.ColonyFactions`). A mixed game loads every faction's content, de-duplicated
  (`OtherFactionCollections`); `FactionCatalog` says which faction lists what. Each colony's faction is saved
  (`ColonyFactionService`), and so is each beaver's (`CharacterFaction`). Needs, looks and each building's goods and
  plants follow the character's or building's own faction on every computer; only what you see follows your colony's.
- **Science** keeps one pool and one unlock set per colony in the save. Simulation code that earns, spends or reads
  science names the colony of the building doing it; science with no named colony goes to the first colony, with a
  warning in the log.
- **Exchanges** are saved on the Trading Post's two halves, with each round's goods held on their half. A round
  crosses in the simulation, at the same tick everywhere, once both sides are in. The ledger, each side's reserve,
  the last terms and the totals traded are kept there too.
- **Stewards and wishes** are saved singletons (a steward's stable id per colony; up to three items per colony), set
  by actions the host judges. Which colony a steward acts as right now, and who is in the game today, travel in
  actions and aren't saved.
- **The colony digest.** Every colony state change (owners, marks, science and unlocks, exchanges and the ledger,
  traded beavers, presence, hand-overs, working hours) made in a tick or a replayed action folds into a running
  digest. The host sends it with every heartbeat; a guest whose own differs stops that tick, logging both digests and
  each side's change count. On a desync every computer logs its changes since the last agreed count (`Colony changes
  here as …`, from `ClientDesyncedEvent.colonyChangesAgreed` and `ColonyDigest.DescribeSince`): line two players'
  lists up by change number, and the first line that differs is the change their computers didn't make alike. The
  last 16384 changes are kept. Once a day the host also sends a full colony check (`[Colony] Check`, one tick after
  the turn of the day); a guest that differs stops too. A shared game has none of this.
- **Buildings nobody placed as an action** (built before the game was hosted) take their colony on their own, checked
  at the first tick and every 16 ticks: their district's, else the owner of the road at their entrance.
- **Placement** is judged by the placing player's own tool; the joined-roads check isn't repeated in the replay,
  where it read state that differs between computers. Whether a spot is still free is checked by the host alone, and
  guests take its answer. Two district centers placed on the same tiles in one tick: the second is skipped.
- **In a paused co-op game**, what was just built or removed updates its district when the game resumes, at the same
  moment everywhere. Gates and automation react at the tick, not the frame. A District Crossing's panel no longer
  changes what that computer's workers export (their snapshot is taken in the tick).
- **Deletions cost frames.** Whenever something is deleted during a tick, every computer finishes that tick in the
  next frame, so the game removes it at the same moment everywhere: at most *frame rate ÷ (1 + deletions a tick)*
  ticks a second. The diagnostics report and a daily `[Perf]` log line count them. With detailed logging on, the log
  has one trade line a day; a line `[Colony] Trade check:` is a bug.

## Steam networking

The Steam transport uses `ISteamNetworkingSockets`, the API Valve recommends (the legacy `ISteamNetworking` P2P API
is deprecated), from the game's own Steamworks assembly.

- **Lobby.** The host opens a friends-only Steam lobby; the overlay invites into it. Lobby data says whether the room
  is open, and closes at **Start Game**, so an old invite explains itself instead of hanging. On a rehost the lobby
  is handed to the new room, so connected guests come straight back.
- **Admission.** The host accepts a connection only from a lobby member, so a stranger who knows a Steam ID can't
  connect. A guest that arrives before the host's lobby view catches up gets a five-second grace period.
- **One thread for Steam.** Every Steam call happens on the game's main thread, where Steam callbacks arrive.
  TimberNet's threads use in-memory queues: `Write` never blocks, and `Read` blocks on a queue the pump fills.
- **Pumped between ticks.** The pump runs once a frame and, at most once a millisecond, between a tick's buckets, so
  the ping doesn't grow with the game speed. Only data moves there: no state changes, closing or admission.
- **Background connect.** The connection completes after `ConnectAsync` returns; the client waits on a worker thread
  (up to 45 s) before the compatibility handshake's 15 s clock starts.
- **Throughput.** The send rate ceiling is raised to 8 MB/s and the send buffer to 4 MB (Steam's defaults are
  256 KB/s and 512 KB). A full buffer keeps data queued for the next frame: nothing is dropped, order is kept.
- **Never hangs silently.** A send without progress for 30 s, a connect over 40 s, or a queue past 128 MB fails the
  connection with an explanation, including Steam's own end reason.
- **Steam can't break hosting.** If Steam fails to start, direct-IP hosting carries on. A local close flushes queued
  data for up to 3 s, and data the peer sent before closing is still delivered.
- **The overlay.** While Steam's overlay is open the game pushes an input-blocking panel; it's removed when the
  overlay closes, or as soon as a dialog opened over it is closed.

`StabilityTests` runs the production transport (`SteamLinkSocket`, `SteamLinkManager`) against a fake Steam network
that fails any test calling Steam off the game thread, rejects messages over 512 KB and forces backpressure. It
covers ordered transfer through a tiny buffer, the queue cap, timeouts, the stall detector, closing, lobby-only
admission, a full TimberNet session over Steam, and protocol parity with a direct connection. The thin layer that
calls Steam itself (`SteamLinkBackend`, `SteamNet`, `SteamListener`, `SteamOverlayConnectionService`) runs only in
the game, with two Steam accounts.

## Connection panel, chat and player cursors

These travel on a side lane, beside the game's events: pings, the roster, chat and cursor activity.

- **Never part of the game.** Side-lane frames are intercepted on the receive thread before the event queue, are
  never in the replay script or the desync hash, and are never sent to a guest until its save, state and init frames
  have been written. Every frame is validated; a malformed one is dropped and never ends the session. If the panel,
  the chat or the cursor overlay fails, it switches itself off and the game carries on.
- **Ping.** Once a second the host sends each guest a probe, answered on the guest's network thread. The reply also
  carries the guest's tick and frame rate (**Guest behind**, **Guest fps**). The host smooths the results and sends
  every guest a short roster. Over Steam, data moves only while a game thread serves Steam, so the ping includes a
  short wait at each end: at most a millisecond during the simulation.
- **Chat.** The host numbers every message and sends it to everyone, the sender included, so all see one order. It
  keeps the history (2,000 messages) for late joiners, strips control characters and `<` `>`, and allows a burst of
  six messages, then two a second.
- **Cursors.** At most ten samples a second, in unscaled time. Each connection keeps only the newest state per
  player, drained by one pooled task through the same framed write path as game events. The host assigns each
  guest's identity (the host is player 0). The receive mailbox keeps at most 64 states, and a remote display expires
  after three seconds without updates. Per-player styles are saved locally to `BeaverBuddiesCursorStyles.json` in
  the game's persistent data folder, keyed by display name.
- **Panel width** follows the game's population panel above it (else the nearest panel above, else its own text,
  210–300 px); the width it followed is written to `Player.log`.

`StabilityTests` covers the round-trip tracker, the wire formats and their malformed frames, real host and guest
sessions (the side lane never changes the hash, the script or tick progress), the chat log, rate limit and
cleaning, player colors, the style store, the panel's wording and sizing, and the speed boost's limits.

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
   `dotnet build BeaverBuddies/BeaverBuddies.csproj -c "Release Steam" --no-restore -p:BeaverBuddiesModsPath="<folder>\TimberTogether\"`
   (`-c Release` for a build without Steam networking).
4. Checks: `dotnet run --project StabilityTests --no-restore` (headless),
   `dotnet run --project RuntimeChecks --no-restore -- <built BeaverBuddies.dll> <Timberborn_Data\Managed> <Harmony folder> <Mod Settings Scripts folder>`
   (against the game's own assemblies), and
   `python -m unittest discover -s RuntimeChecks -p "test_water_snapshots.py"`.

**StabilityTests** (headless) covers the network stamping, player slots, the ownership rules, founding, migration
pairing, the blueprint join check, the colony digest, the Steam transport and the side lane. **RuntimeChecks** runs
against the compiled mod and the game's assemblies: every action type declares what it touches, the shared actions
are listed for review, the host's answers survive the event JSON, no simulation-reachable game method reads a dev key
the mod doesn't neutralize, and every game method or field the mod hooks still exists.

These checks can't start Unity or prove full multiplayer determinism; in-game testing is what confirms behavior. The
step-by-step playtests are in [ALPHA-TEST-SCRIPTS.md](ALPHA-TEST-SCRIPTS.md).
