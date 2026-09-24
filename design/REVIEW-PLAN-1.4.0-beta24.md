# Review plan: ready for 1.4.0? The late game, mixed factions and trading at scale (Timber Together 1.4.0-beta24)

**Status:** proposed 2026-09-23, for Kyler's approval. Written against beta23, then rebased on beta24 (the Join co-op
box, reviewed in §0.1). Written to be carried out by one session (Opus 5.5, xhigh) that runs reviewers as subagents,
fixes what it finds and cuts a release candidate, on Kyler's computer. It never runs the game (the rule below).
Findings go to `design/REVIEW-FINDINGS-1.4.0-beta24.md`.

**Carried out** 2026-09-23 and released as **1.4.0-rc1**. The findings, the coverage matrix and what was left are in
[REVIEW-FINDINGS-1.4.0-beta24.md](REVIEW-FINDINGS-1.4.0-beta24.md), and the reviewers' reports in
[review-1.4.0-beta24/](review-1.4.0-beta24/). Corrections to this plan:
- a fifth reviewer (E, the colony lifecycle) was added;
- in P-1, `ToActionString` was right, and only the replay was swapped;
- only deletions end a frame's ticking in a game (P-5), not creations;
- E-6, a finding of the review, was refuted at merge (findings, §2).

> **Hard rule: never start, drive or test Timberborn, and never touch the installed mods.** Kyler, 2026-09-23: "I just
> don't want you controlling timberborn."
>
> - **Never start the game.** That means no `Timberborn.exe`, no `steam -applaunch`, no command-line switches, and not
>   even the game's own benchmark. No test rig, and no scripted or automated play. Kyler does all in-game testing
>   himself.
> - **Never change what the game loads.**
>   - Never write to `Documents/Timberborn/Mods`, the mod manager's enabled list or the game's settings.
>   - Every build goes to a scratch folder (`-p:BeaverBuddiesModsPath=<scratch>/`). Without it, the post-build step
>     copies into the installed mod.
>   - Never edit the game install.
>   - Never write into `Documents/Timberborn/Saves`: copy a save to the scratchpad to read it.
> - **Everything else is fine, because none of it runs the game:**
>   - reading and decompiling the game's DLLs, `Blueprints.zip` and copies of saves;
>   - building the mod and running both check suites (RuntimeChecks loads the game's DLLs into a plain .NET process and
>     never starts Unity);
>   - the review, the fixes and the release.
> - **What only the running game can show goes to Kyler as short, exact test steps** (§6). The mod is taught to report
>   what those tests need (§5.3), so one ordinary session of his answers the question.

**Why this review.** 1.4.0 is meant to be the first official release. Everything that has been played is the early
game. The systems people build towards in the late game (automation, the HTTP API, water automation, dynamite and
tunnels, both Wonders, bots, power grids, badwater), Folktails and Iron Teeth together, and Trading Posts at scale
have never run in a Timber Together game.

**Kyler's goal** (2026-09-23): through the rigour of this review, to know with high confidence that every late-game
feature works and doesn't crash. The game testing comes later, in a real two-player playtest in a late-game colony.
So the review:
- **covers every late-game feature, not a sample.** The coverage matrix (§1) is the proof, and it has no empty cells;
- **treats a crash as the worst finding after a desync.** A throw inside a tick or a replay ends the session for both
  players, so every such path is proven unable to throw, or guarded and checked (H1);
- **checks without running the game:** the mod's own checks, the game's code run outside the game, and reporting in
  the mod (§5.3);
- **makes the later playtest count.** It writes that playtest (§6) so that each step closes named cells of the
  matrix, and the mod reports what each step needs.

---

## 0. Base, what has been played, and what is already known

**Base.** Tag `v1.4.0-beta24`, commit `e7adcf4`: beta23 `c02637b`, a CI test fix `85258bc`, and beta24. This plan was
written against beta23; beta24 was read while it was being released, and the released commit matches what was read.
Before starting, `git fetch`. If main has moved past this plan's own commit, start from main and say what the extra
commits changed.

Game: Timberborn 1.1.2.4. Checks at beta24, per its changelog: StabilityTests 417 and RuntimeChecks 361, both builds
with 0 warnings.

**Line numbers** in this plan and its planning notes are beta23's. beta24 touched only the files listed in §0.1; of
those, only `IO/ServerEventIO.cs` is cited here, and those citations use beta24's lines. The decompiled game (all 497
`Timberborn.*.dll`) was made while planning into this session's scratchpad; the executing session makes its own
(`ilspycmd`, 10 at a time, about 15 minutes).

**What has been played** (so that nothing below is mistaken for tested):

| Area | Played | Not played |
|---|---|---|
| Lockstep core, colony digest, separate colonies | Kyler, 2026-09-22/23: 47 cycles, about 30 beavers per player, maximum speed, no desync. The only cycle-47 save on this computer, `Saves/beta15/beta15 (1) late.timber`, has 8,381 entities (mostly ruins and plants), 30 beavers, 2 district centers, 1 Trading Post, 11 lodges | Anything larger than an early colony |
| Trading Posts | Early game, proof of concept (Kyler, 2026-09-23) | Many posts, repeating exchanges over many cycles, every kind of good, science and beavers at volume |
| Waiting room | Pages seen (beta23) | A game started from it (Script D7, D7a, D8) |
| Join co-op box (beta24) | Kyler, 2026-09-23: "works great" | B24-b (Enter while typing an address with a friend selected) |
| Mixed factions | Nothing | All of Script F, and every late-game system in a mixed game |
| Late-game systems in co-op | Nothing in Timber Together. beta12 read Wonder timing, levers and deletions; its Script B lines 8t to 8y are owed | Automation, HTTP API, water automation, dynamite, tunnels, both Wonders, bots, power, badwater, fireworks, zipline and tubeway networks, beehives |

**Not re-reviewed** (found sound before; don't redo them unless this review's scale or the colony layer breaks their
premise, and then say which premise):
- alpha10's review (`design/REVIEW-PLAN-1.4.0-beta11.md` §0 lists it) and its fixes (alpha11 to alpha14);
- beta12's R1 to R11 (`design/REVIEW-FINDINGS-1.4.0-beta11.md`): all 163 frame hooks classified, the parallel water and
  soil simulation safe by design, recorded methods reached only from UI, replay or load, the FillValve double patch
  harmless;
- beta21's found-sound list (`design/REVIEW-FINDINGS-1.4.0-beta18-20.md` §3): M2 (every character creation site), M6
  (157 `Single`/`First` sites), M7 (the 20 replaced bodies), stockpiles, planters and yields on creation, load and
  handover, and the rest.

**The premise, and what it means for the late game.** Every computer loads the same bytes with the same seed; every
player action replays at a tick boundary; a guest stops at the first difference (RNG words every tick, the colony
digest every heartbeat, the daily fingerprint). So the late game can fail in six ways only, and every lead below is one
of them:
1. state changed on one computer outside a tick or a replay (a UI path, a frame update, another thread);
2. simulation driven by frame time;
3. a mod patch whose answer depends on the local player (`DisplaySlot`, `LocalFaction`);
4. a throw inside a tick or a replay, which ends the session for everyone;
5. cost: a tick the computers can't keep up with, or a hitch, a leak, a save that takes too long;
6. a rule that lets one colony change another colony's things, or a system the colony layer never taught to tell them
   apart.

### 0.1 beta24, the Join co-op box (reviewed while planning)

**What changed** (`c02637b..e7adcf4`): 22 files, +693 / −26, with three new files. One of the 22 is the CI fix's
test. It is not a late-game change, but it is the newest code on the release's main path (every guest joins through
it), so it gets its own short lead, B24.
- `Connect/JoinCoopBox.cs` (new, 308 lines): **Join co-op game** in the main menu, with Steam, opens a box listing
  friends' co-op games, the IP address under it. It refreshes every 2 s.
- `Connect/FriendGameRules.cs` (new): classify, order and keep the selection.
- `Steam/SteamListener.cs`: four lobby keys, `bb_ver`, `bb_room`, `bb_desc` and `bb_host`, written on the main thread.
- `IO/ServerEventIO.cs`, `Lobby/LobbySession.cs`, `Connect/ServerHostingUtils.cs`: the description each host shows.
- `Connect/ClientConnectionUI.cs`: four more constructor dependencies. It is bound in every game and in the main menu
  (`Plugin.cs:40, 105`).
- `Lobby/LobbyPage.cs`: both buttons medium; the guest's reads **Ready**.
- Checks 417 / 361.

**Found sound, by reading:**
- Joining from the list is the Steam invite's own path: `SteamMatchmaking.JoinLobby`, then
  `SteamOverlayConnectionService.OnLobbyEntered` with its started check. The box pops itself before it joins, so the
  guest page's wait for a page on top (`LobbyGuestPanel.PageOnTop`, beta23) is met.
- `bb_open` goes to "0" on both paths, a waiting room's Start (`ServerEventIO.CloseLobby`) and a classic game's first
  tick (`StopAcceptingClients`). So *Open to join* can't outlive the join window, and joining stays refused after it.
- The box falls back to the address box if it can't open, and in a game or without Steam the old box is kept
  (`ClientConnectionUI.ShowJoinBox`).
- The new constructor dependencies are covered by `RuntimeChecks/BinditoChecks.cs`, which builds and validates the
  Game and MainMenu containers with the mod's configurators. Confirm that the 361 includes those checks on the final
  build.
- A Stability Fork or pre-beta24 host lists as *Older version* (no `bb_ver`) and can't be joined from it.

**B24, to confirm** (small; none blocks a release):
- **B24-a: lobby strings are shown as they come.** `bb_desc` and `bb_host` are capped only by the host that writes
  them (120 characters, `SteamListener.SetDetails`). The reader shows any friend's lobby data in the row's labels
  (`JoinCoopBox.Bind`). Steam names can hold `<`…`>`, which UI Toolkit labels read as rich text.
  - **Fix:** cap on read, and turn rich text off on the three labels. Display only.
- **B24-b: Enter with a friend selected while typing an address.** The field has its own confirm
  (`SetConfirmCancelActions`) and the box's `OnUIConfirmed` joins the selected friend first. Which one wins on one
  Enter? **Check in a game.** A player typing an IP must never be sent to a friend's game instead.
- **B24-c: "Already started" may never show.** After Start the host's lobby is made unjoinable
  (`SetLobbyJoinable(false)`), and Steam may then stop reporting it in `GetFriendGamePlayed`, so the friend drops off
  the list. That's cosmetic; say so in the docs if it is so.
- **B24-d: same version string, different build.** The list matches `bb_ver` against `Plugin.Version`; the handshake
  matches the DLL. A self-built or Release (not Release Steam) DLL of the same version lists as joinable, then is
  refused at the handshake with the build message. Acceptable; confirm the message is clear.

Kyler has seen the list work (2026-09-23); B24-b stays a person's test.

### Found while planning

Read in the mod and the decompiled game, not run.

- **P-1: the automation panel's Reset and Reset all are swapped in co-op** (Confirmed by reading). In the game,
  `SequentialTransmitterResetFragment.OnReset` calls `_sequentialTransmitter.Reset()` and `OnResetAll` calls
  `_automationResetter.ResetPartition(_automator)` (`Timberborn.AutomationUI`). The mod records `OnReset` as
  `resetAll = false` (`BeaverBuddies/Events/AutomationEvents.cs:485-500`), which replays `ResetPartition` (:473-475),
  and `OnResetAll` as `resetAll = true` (:502-517), which replays `ISequentialTransmitter.Reset()` (:465-470). Every
  computer does the same wrong thing, so it's no desync, but every co-op player who presses either button gets the
  other one. `ToActionString` (:481) is swapped the same way.
- **P-2: the HTTP API is live in co-op, half recorded, and undocumented.** The HTTP Lever and the HTTP Adapter are
  ordinary late-game buildings: Automation group, 5,000 science, both factions (`Blueprints.zip`,
  `Buildings/Automation/HttpLever`, `HttpAdapter`). A request arrives on the listener's thread and waits in
  `HttpApiIntermediary`, whose `UpdateSingleton` (a frame, on the game thread) calls `HttpLever.SetState` →
  `Lever.SwitchState`. The mod records `SwitchState` (`AutomationEvents.cs:137`), so the request becomes the local
  player's action. `HttpLever.SetColor` isn't recorded. Each computer starts its own listener (`HttpApi.Start`, port
  saved in the save's `HttpApi` singleton, 8080 by default), and each adapter's webhooks fire from every computer. The
  mod's only word on it is a comment saying it is left out (`AutomationEvents.cs:173-175`).
- **P-3: nothing keeps any late-game network apart except roads.** Nothing in `Colonies/` mentions mechanical nodes,
  water-building synchronisation or the counters' global mode.
  - A shaft placed against another colony's shaft makes one power network.
  - `FillValve`, `Floodgate` and `ThrottlingValve` `…AndSynchronize` setters also change their synchronised
    neighbours, which may be another colony's. The event is scoped to the clicked building only
    (`AutomationEvents.cs:26-27`, `EntityUIEvents.cs:509, 550`).
  - A Population Counter in global mode counts every colony's beavers.

  All of this is deterministic, but each case breaks "you change your own colony only" (TWO-COLONIES) or its spirit.
- **P-4: a Wonder's effect is a faction need, and the Wonder countdown is one for the whole map.** The Folktails
  Wonder is the Earth Recultivator and the Iron Teeth one the Earth Repopulator (planes and pilots). Each effect is a
  need of its own faction (`NeedCollection.Folktails` / `.IronTeeth`), so in a mixed game the other faction's beavers
  get nothing. Nothing crashes: `NeedManager.ApplyEffect` skips a need the character lacks (`Timberborn.NeedSystem`,
  `TryGetNeed`). The completion countdown (`WonderCompletionCountdownStarter`, a tickable singleton) is one for the map:
  the first colony to finish completes the game's Wonder for everyone.
- **P-5: every creation or deletion inside a tick ends that frame's ticking in co-op.** Deletions:
  `TickTimingFixes.cs:27-35`. Creations: `EntityComponentInstantiatePatcher`, `DeterminismService.cs:852`. The unticked
  buckets are given back, up to one tick (`ReplayService.cs:1201, 1245`). Early game that is rare. A late game has
  births, goods stacks, construction, pilots, planes, fireworks and a 40-charge dynamite chain, and at speed 7 or
  boosted it may end most frames early. That is only a cost, never a desync, but it is unmeasured and it lands exactly
  where the late game is already slow.

---

## 1. The release bar

**The coverage matrix** is the review's main deliverable, in the findings.
- **Rows:** every late-game feature: each row of §2, every late-game building and good of both factions (F1), and
  every kind of trade (T2).
- **Columns:** the ways a feature can fail:
  - desync;
  - crash (a throw in a tick or a replay);
  - one colony changing another;
  - mixed factions;
  - cost at late-game size;
  - save, load and rehost.
- **Each cell says one of:**
  - **Sound**, with the evidence;
  - **Fixed**, with the check that fails without the fix;
  - **Left**, documented, with the reason;
  - **Playtest**, with the step of §6 that settles it.

No cell is empty. No cell with a Plausible desync or crash is left without a fix or Kyler's decision.

**Blocking for 1.4.0** (this review works towards them; the release candidate carries the fixes; 1.4.0 itself waits
for the owed playtests):

1. No known desync that can be reached without dev mode, in any system in §2.
2. No throw out of a tick or a replay from late-game content, in shared, separate or mixed games.
3. No colony can change another colony's things through a late-game system, except what TWO-COLONIES lists as shared
   (water and weather today, plus whatever §7's decisions add). Every exception is documented.
4. Mixed factions: every late-game building of both factions works for its own colony: placed, staffed, supplied, its
   effect applied. Nothing of one faction breaks, starves or crashes the other.
5. Trading Posts: every tradable good, science and beavers cross in both directions; exchanges repeat for many cycles;
   nothing is created or lost; nothing stalls without saying why.
6. Performance: budgets B1 to B6. The review estimates them from the code and removes what it can (§5.4). The numbers
   come from Kyler's own recordings, Script P (§6), helped by the mod's new reporting (§5.3).
7. A long session, played by Kyler with a guest:
   - at least 20 in-game cycles of a late two-colony game, and 10 each of a mixed game and a trading game;
   - no mismatch, no exception from the mod, and bounded memory, as the mod reports them (§5.3).

   These are owed tests, like the playtests; the release candidate doesn't wait for them.
8. Played by people before 1.4.0 final: Script D7, D7a and D8, B24-b (the Join co-op box), Script F, and the
   two-player late-game playtest this review writes (§6: Scripts L, M and T).
9. The coverage matrix (above) is complete: no empty cell, and no Plausible desync or crash left without a fix or
   Kyler's decision.

**Budgets** (proposed; Kyler can change them). "The reference save" is `R-late` in §3. They are measured in Script P.

| | What | Budget |
|---|---|---|
| B1 | Timber Together installed, single-player | Tick time within 1 % of the game alone: the mod costs nothing outside co-op |
| B2 | Co-op host, no guests | The mod's own share of tick time at most 5 % in a shared colony, 7 % with the save split into two colonies |
| B3 | Allocation | No mod patch or singleton among PerformanceLog's top allocators per tick (GC pauses are the late game's hitches) |
| B4 | Host and guest, speed 7 and a boost of 15 | The guest stays within `BufferTicksFor(speed) + 2` ticks of the host for 95 % of ticks, with no catch-up spiral, and the achieved tick rate is within 10 % of single-player |
| B5 | Rehost of the reference save | Save, send and the guest's load in under 30 s on a home network; the host's game never freezes for more than 1 s |
| B6 | Mixed factions | Tick cost within 5 % of a single-faction game of the same size; load time and memory within 25 % |

---

## 2. Map of the late game: how each system reaches the simulation, and what the mod does today

All `file:line` references are beta23's (see §0 for beta24). "Recorded" means a player's click goes through
`ReplayEvent.DoPrefix` and replays at a tick boundary.

| System | Reaches the simulation through | The mod today | Leads |
|---|---|---|---|
| Automation core | `AutomationRunner`: `Tick` (evaluate scheduled, then sequential `EvaluateNext`, commit, sample) and `UpdateSingleton` (evaluate scheduled **with** `EvaluateNext`, every frame) | Frame path off in co-op (`Fixes/FrameToTickFixes.cs:156-160`) | A1, A5, A6 |
| Levers, relays, memory, timers, chronometers, gates, clutches, indicators, speakers | Setters from their panels | 73 setters recorded by one reflection prefix (`AutomationEvents.cs:109-201`); timer intervals, inputs, resets and the weather station's early toggle have their own events (:322-568). Spring return runs as simulation (`TickTimingFixes.cs:56-67`) | A2, P-1 |
| Sensors: depth, flow, contamination | `Sample()` in the automation tick reads the water map | Nothing special | W2 |
| Counters: population, resource, science, power; weather station | `Sample()` | Science: the counter's own colony (`ColonyScienceService.cs:440-445`). Chronometer: its colony's hours (`ColonyWorkingHours.cs:190-202`). Nothing for the others | A4 |
| HTTP Lever, HTTP Adapter | Listener thread → `HttpApiIntermediary.UpdateSingleton` → `Lever.SwitchState`, `SetColor`; webhooks | `SwitchState` recorded; the rest not; undocumented | A3, P-2 |
| Fireworks | Launcher settings (recorded); `FireworkLaunchService` (sampling singleton); `Firework` is a frame component | Settings recorded | O1 |
| Floodgates | `SetHeightAndSynchronize`, `ToggleSynchronization`, automation height | Recorded, scoped to the clicked gate (`EntityUIEvents.cs:507-588`) | W1 |
| Fill valves, throttling valves | `…AndSynchronize` setters; both are tickable components reading the water map | Recorded (FillValve twice, harmless per beta12) | W1, W3 |
| Pumps, water movers, input pipes, regulators | Mode, depth limit, open, close, automate | Recorded (`EntityUIEvents.cs:1148-1198`; `AutomationEvents.cs:194-198`) | W4 |
| Water sources, badwater | Tick; the parallel water simulation | Three fixes: order, timing, after the parallel tick (`Fixes/WaterSource*.cs`) | W4 |
| Power | Mechanical graphs, batteries, generators, clutch | Clutch and power meter recorded. The battery slider and the dev generator's panel are dev tools, not recorded (`MechanicalSystemUI` `BatteryFragment.ChangeCharge`; `PowerGenerationUI`) | P1, P2, H2 |
| Dynamite, tunnels, explosions, unstable cores | Detonation recorded; the chain, terrain removal and deaths run in the tick (`ExplosionService`, `CharacterExploder`) | `DynamiteTriggeredEvent` (`EntityUIEvents.cs:737-770`); deletions end the frame; nothing on blast reach | X1 to X4 |
| Terrain under deleted buildings (G9) | The deletion tool | Left in beta12: the event carries no terrain (`ToolEvents.cs:207-241`) | X4 |
| Wonders (both factions) | Activation (recorded, host decides), animation and planes (moved to the tick), effect (ranged, faction need), completion countdown (tick) | beta12 D1 and D3 (`Fixes/WonderTimingFix.cs`, `EntityUIEvents.cs:1276-1338`); mixed: completion screens | V1 to V3, P-4 |
| Bots (both factions) | Factories, parts, charging, per-faction needs | Mixed: needs and template per faction (`Factions/FactionCharacterPatches.cs:31-135`) | F3 |
| Births, breeding pods, growing up | `NewbornSpawner`, `BeaverFactory` | Mixed: faction from the spawner (`Factions/CharacterFaction.cs:137-185`) | F2 |
| Ziplines (Folktails), tubeways (Iron Teeth) | Links (tool), navigation | Zipline links judged by the host (`ColonyRoadNetworks.cs:138-148`); tubeways only as road-carrying (TWO-COLONIES :391) | O2, F5 |
| Beehives | Crop growth near the hive | Nothing (B-1 fixed the crash path) | O2, F5 |
| Trading Posts | `ColonyExchangeService.Tick` every `CrossingInterval` ticks, exchange events | The whole trading layer (`Colonies/TradingPost*.cs`, `TradeItems.cs`, `ExchangeTerms.cs`, `Factions/FactionTrade.cs`) | T1 to T8 |
| Dev tools | Debug fragments, the dev panel | Three shared (instant unlock, Finish now, Add 1000 Science); the rest warned about (`Fixes/DevModeCoopWarning.cs`) | H2 |

---

## 3. What we have to work with (found while planning)

**Reference saves on this computer.** Read from copies in the scratchpad; the originals are never written.

| Name | File | What is in it |
|---|---|---|
| `R-late` | `Saves/Romans missing leg/Romans missing leg (9) TESTING.timber` | 1.1.2.4, Folktails, 11,356 entities, 319 adults, 1,601 paths, 229 levees, 46 floodgates, 12 gravity batteries, 6 geothermal engines, depth and contamination sensors, 2 gates, a dynamite and an explosives factory, 24 zipline stations, an observatory. No levers, relays, timers, Wonder or HTTP buildings. Carries Kyler's OptimizedLocalHousing singleton |
| `R-blast` | `Saves/roman is kinda uglyyyy/roman is kinda uglyyyy (37).timber` | Saved in 1.0.13; a reference for its contents only. 14,702 entities, 478 adults, 3,428 paths, 39 double dynamite and 2 dynamite, 22 tunnels, 57 fill valves, 62 floodgates, a badwater rig and dome, a dirt excavator |
| `R-long` | `Saves/beta15/beta15 (1) late.timber` | Timber Together, two colonies, day 47-9: small |

None has Iron Teeth, a mixed game, bots, a Wonder, HTTP buildings, levers, relays, memory or timers. Scripts L, M and
T (§6) give Kyler the steps to build those in single-player with dev mode before hosting.

**Planning notes** (read-only sweeps made for this plan; start from them, and re-check a line before citing it):
- [`planning/1-late-game-events-and-fixes.md`](review-1.4.0-beta24/planning/1-late-game-events-and-fixes.md): every
  ReplayEvent, the automation patch list, every `Fixes/` patch, and the colony rules for these systems;
- [`planning/2-hot-paths.md`](review-1.4.0-beta24/planning/2-hot-paths.md): every tickable, updatable and hot-path
  patch, with estimated costs;
- [`planning/3-mixed-factions-late-game.md`](review-1.4.0-beta24/planning/3-mixed-factions-late-game.md): every
  faction patch, the late-game systems it handles or never mentions, what the desync checks see, and Script F's gaps.

**Tools already written:**
- Kyler's **PerformanceLog** 0.1.2 (`Documents/Timberborn/Mods/PerformanceLog`): per-frame and per-tick timing, per
  singleton, entity kind and mod, allocations and GC; `tools/perflog.py report|compare`. It only observes. Kyler records
  with it (Script P); the session reads and compares the recordings.
- **LateGamePerformance** 0.4.27 (installed) / 0.4.28 (repo): the mod late-game players will run beside this one (C1).
- In this repo: `RuntimeChecks/IlScan.cs` (IL reading), `compare_walker_traces.py`, `compare_water_snapshots.py`,
  `WalkerDiagnostics`/`WaterDiagnostics` (per-tick dumps), the Ctrl+Shift+J report (`ColonyDiagnostics.cs:32-130`),
  `ColonyProfiler`.
- `Timberborn_Data/StreamingAssets/Modding/Blueprints.zip`: every template, need and good of both factions.

---

## 4. Leads

Every lead says what to check and what counts as proof. Every finding is reported as **Confirmed** (traced end to end
in the mod and the game, or reproduced by a check), **Plausible**, or **Refuted**. A lead that only the running game can
settle is not closed by reading alone. It gets a line in Script L, M, T or P (§6) and stays Plausible until Kyler's test
comes back.

### Tier 1a: the late game in co-op (every player)

**A1. Automation runs only on the tick in co-op.** The game evaluates scheduled partitions every frame, and that path
also runs `EvaluateNext`. The tick runs `EvaluateScheduled(false)`, the sequential partitions' `EvaluateNext`,
commit, sample, then `EvaluateScheduled(false)` again (`Timberborn.Automation`, `AutomationRunner.Tick` and
`UpdateSingleton`). Co-op drops the frame path. **Check:**
- that dropping it changes *when* a result shows, never *whether*, against single-player. In particular:
  - pulses shorter than a tick: a spring-return lever pressed and released, a timer of one tick, chronometer edges, a
    memory cell set and reset in one tick;
  - a scheduled combinational partition that is never scheduled again until something changes;
- that nothing evaluates while the game is paused (what a player sees after flipping a lever while paused);
- that a replayed setter at the tick boundary is evaluated in that same tick (the order of `AutomationRunner` against
  the mod's tickable singletons).

**Proof:** a table of each automation building's behaviour, single-player against co-op, from the game's code.
- Where the game's automation classes can be driven outside Unity (RuntimeChecks, as `WonderChecks` does for the
  Wonder timing), reproduce the cases there.
- What can't be reproduced becomes Script L lines, with one player's frame rate capped at 15.

**A2. The recorded setters are a hand-kept list.** **Check, by script over the decompiled game** (§5.2, sweep 1):
every UI callback in `*UI.dll` (panel buttons, sliders, toggles, dropdowns, input processors) that reaches a write to
saved or ticked state without passing a recorded method. beta12 R4 checked the other direction (recorded methods
reached only from UI); beta14's Add 1000 Science showed this one is where the bugs were. Already known from planning:
the dev generator's panel (strength, Flip rotation) and the battery's dev slider. Keep the sweep as a RuntimeChecks
check with an allow-list, so a new unrecorded path fails the build.

**A3. The HTTP API (P-2).** **Check:**
- a request on the host's and on a guest's computer: recorded as that player's action, judged by colony (a guest
  switching the other colony's HTTP lever is refused, and says so), delivered once;
- two players whose saves name the same lever;
- what reads a lever's color (indicator color replication?) and whether `SetColor` can change anything saved;
- the webhooks (every computer calls them; is that what a player wants);
- a request for a lever that is gone.

**Decide** (default in §7): support the API as each player's own actions and document it; record or drop `SetColor`.

**A4. What counters and sensors read, per colony.**
- Population Counter in global mode (all colonies' beavers);
- Resource Counter (district-scoped: confirm it can't reach the other colony through a District Crossing);
- Power Meter (a network that may span colonies, P1);
- Weather Station and Chronometer (map-wide, as intended);
- Science Counter (its own colony; confirmed while planning).

Also each `Sample()` at load: `AutomationRunner.PostLoad` samples before the first tick, before colony stamps are
certain. **Check** each against "a colony's automation reads its own colony".

**A5. Wiring over time.** The rules judge a wire when it is made (`AutomationEvents.cs:26-27, 322-343`). **Check:**
wires that end up joining two colonies after a handover, a steward's action, a faction switch or a Trading Post
changing hands. What a relay's `IncreaseInputs` and a memory's inputs do then, and whether a partition can span two
colonies.

**A6. Automation at scale.** Every automator placed or removed repartitions (`AutomationRunner.Register`,
`ReassignExistingPartition`). **Measure** placing a 50-building automation park as a blueprint in co-op, on the
reference save.

**W1. Synchronised water buildings across colonies (P-3).** `FillValve.SynchronizeNeighbors`, the floodgate's
synchronisation and the throttling valve's change neighbours of the same kind. **Check:** whether the neighbour
search stops at colony lines (it can't know them); what "synchronised" means at a building joined later by the other
colony; and the replay under an event scoped to one building. **Decide** (default in §7).

**W2. Water sensors against the water simulation.** Depth, flow and contamination sensors sample during
`AutomationRunner.Tick` and read the water map. **Check** that they read the tick-consistent copy on every computer,
including the tick right after a replayed floodgate or valve change. beta12 R8 found the parallel simulation sound in
general; confirm it for these readers in 1.1.2.4.

**W3. Water buildings that tick.** `FillValve`, `ThrottlingValve` (its reaction speed), `StreamGauge`, `WaterMover`
and `TickableWaterBuilding` read water depth in their tick. **Check:** which copy they read, whether `TickOnlyArrayFix`
covers them, and whether anything reads `Time`.

**W4. Water sources and badwater through a whole late game.** Check that the three fixes (`WaterSourceFix`,
`WaterSourceOrderFix`, and `WaterSourceTimingFix`, which **throws** if 1.1.2.4's body changes, unlike the Wonder fix)
still match 1.1.2.4 and cover:
- regulators switched by automation during a drought and a badtide;
- `WaterSourceActivator` on map timers;
- badwater rigs, domes, discharges and centrifuges.

Script L: a drought and a badtide with automated floodgates and valves, on both colonies.

**P1. Power networks across colonies (P-3).** No rule stops a colony's shaft meeting another colony's. A joined network
shares engines and batteries, and it means:
- one colony's clutch (recorded, scoped to its own clutch) cuts the other's power;
- a Power Meter reads both.

**Check** what joins and what breaks. **Decide** (default in §7).

**P2. Mechanical graphs at scale.** Graph merge and split order, and battery charge, must be deterministic.
**Check** that nothing iterates a hash set or dictionary whose order differs between computers (entity ids are the
same everywhere; object hash codes are not).

**X1. Dynamite at scale.** `R-blast` has 41 charges. **Check:**
- the whole chain (`ExplosionService`, `TilesExplosion`, neighbours, unstable cores) runs inside ticks, in the same
  order everywhere;
- how many frames a big chain takes with P-5's interrupt after every deletion;
- `CharacterExploder` kills through `CharacterKilledEvent` (beta21's B-1 rule: never delete a character without
  killing it);
- the terrain removal's consequences: water, soil, navigation mesh, district map, `ColonyRoadNetworks.OnNavMeshUpdated`,
  `ColonyStamps`, a Trading Post or a road join made or broken by the blast.

Script L: a chain on a copy of `R-blast` hosted with a second colony, both colonies' beavers in range.

**X2. Blast reach across colonies.** Only the dynamite's owner may detonate it (`EntityUIEvents.cs:739`). Nothing
limits what the blast destroys: the other colony's buildings, beavers, Trading Post half, stock. **Decide** (default in
§7) and document.

**X3. Tunnels, the dirt excavator, terraforming.** Terrain removed at construction's end (`Tunnel`,
`ITerrainRemovingEntity`, `Timberborn.Terraforming`). **Check** that it happens in the tick and that the colony layer
(reach, roads, relic rewards: `ColonyScienceService.cs:455-469`) follows.

**X4. G9, left in beta12.** A co-op deletion leaves terrain that the game's tool would have destroyed
(`ToolEvents.cs:207-241`, TODO at :238). Players will meet it with late-game builds. **Decide:** fix it (the terrain
list in the event, plus a colony rule) or document it in *Known limits*.

**V1. Wonders in a two-colony game (P-4).**
- **Activation.** The owner may activate, and the host decides (`EntityUIEvents.cs:1276-1338`).
- **The effect.** A ranged effect (`WonderEffectController` → `RangedEffectBuilding`). What does its range reach:
  the other colony's beavers? Of which faction?
- **Completion.** The countdown is one for the map: the first colony's Wonder completes it for everyone
  (`GameWonderCompletionService.CompleteWonder`, `WonderCompletedEvent`). What each computer shows, what it writes, and
  whether anything after completion changes the simulation.
- Mixed games: the completion patch reads `LocalFaction` on a tick path (`Factions/FactionDisplayPatches.cs:238-256`;
  beta21 found it profile-only). Confirm again for the case where the *other* colony completes.

**V2. Wonder timing in the late game.** beta12 moved the animations and the plane launch to the tick
(`WonderTimingFix.cs`). **Check** at speed 7 and a boost of 30, when a tick is short and the game is CPU-bound:
- `IsAnimating` still ends on the same tick everywhere;
- pilots are made with the right faction in a mixed game (`Pilot`, `PlaneSpawner`: late-game-only creation sites; M2
  said all sites are covered, so confirm these are in its list);
- pilots die through `CharacterKilledEvent`.

Script B 8t is still owed; Script M repeats it with both Wonders, one side at 15 fps.

**V3. A Wonder's workers and blockers in a two-colony game.** `NotEnoughWorkersWonderBlocker`,
`UnreachableBuildingWonderBlocker`, `WonderInventory`: only the owner's beavers and goods count.

**O1. Fireworks.** **Check:** the launch (a sampling singleton, driven by automation), goods consumed in the tick, and
flights frame-timed but visual only.

**O2. Ziplines, tubeways, beehives, gates at scale.**
- A zipline network of 24+ stations, and tubeways (Iron Teeth) as roads between colonies.
- The beehive's effect on crops of another colony, and of another faction (F5).
- Gates (moved to the tick, alpha14) with many districts.

**O3. Hazards and weather over many cycles.** Drought and badtide lengths, `HazardousWeatherHistory`, the weather
station. These are shared and covered by the RNG compare; Kyler's long session (release bar 7) confirms it.

**O4. Notices and the journal.** An Indicator with a journal entry or warning belongs to one colony. **Check** who sees
it (`ColonyJournal`): display only, but late-game players build many.

**H1. Crash-proofing, everywhere a late game can reach.** This is the first priority after desyncs. A throw out of a
tick or a replay ends the session for everyone, and the late game reaches states the early game never does.

**Sweep** (§5.2, sweep 7) every mod entry point that runs in a tick, a replay or a load:
- every Harmony prefix, postfix, finalizer and transpiler whose target runs there;
- every `ReplayEvent.Replay`;
- every tickable singleton, entity listener and event-bus handler of the mod.

For each, list the late-game states it can meet, and prove it copes with them, or guard it and add a check:
- an entity deleted earlier in the same tick (blasts; beta12's D2);
- a component missing because the building is the other faction's, unfinished, or a map object (a ruin, an unstable
  core);
- a district or colony that no longer exists (after a handover or a switch);
- a character with no `CharacterFaction` (older saves, dev spawns);
- a save from an older version;
- a singleton missing outside the Game scene.

**Also:**
- The game's own code that the mod makes throw by breaking one of its assumptions, as two districts sharing roads did
  (`DistrictMapConflictPatcher`).
- The finalizers that swallow exceptions (`Fixes/DistrictBuildingsFix.cs`): a swallowed throw must not leave the two
  computers in different states.

Keep the result as a list in the findings. Where a scan can be written, add one: for example, a source scan that every
`Replay` null-checks what it looks up by id.

**H2. Dev tools in co-op.** Nine debug buttons change the simulation (Expire, Explode and its delayed form,
which runs on `Time.deltaTime` in `UnstableCore.Update`, Inventory: Give all, Modify Inventory, Progress construction,
Delete without exploding, Spawn newborn, Finish now), plus the battery slider and the dev generator. Only Finish now
is shared. **Check** that `DevModeCoopWarning`'s text matches this list. Players who build test colonies will use
them; the policy stays "warned", unless §7 says to share more.

### Tier 1b: mixed factions in the late game

Kyler: "it's critical that this feature works flawlessly and is performant."

**F1. Every late-game template of both factions, in a mixed game.** **Check, by script over `Blueprints.zip`:** every
building, good, need and recipe of each faction's late game. Then confirm that in a mixed game each one:
- is placeable by its own colony only (D17);
- has its needs met by its own faction's goods;
- is reachable by the faction code that must know it.

List what the faction code never names. From planning, never mentioned anywhere:
- the badwater rig (Folktails only);
- tubeways (Iron Teeth only);
- the bot part factory, and charging that actually charges;
- metal industries (the Metalsmith, the efficient mine and metal parts are Iron Teeth; the Mine is Folktails; the
  Smelter is both);
- each faction's pumps (the ordinary and badwater pumps are Folktails, the deep pumps Iron Teeth);
- monuments and decorations whose needs belong to one faction;
- explosives factories;
- the Wonders' effects (P-4).

**F2. Characters at scale.** 600 characters of two factions: needs per character (`FactionNeedsPatcher` replaces
`GetNeeds`), wellbeing, births in breeding pods and lodges (`NewbornSpawnerFactionPatcher`), growing up.

**Measure** the per-character cost against a single-faction game of the same size (B6).

**Check** that no late-game creation site misses the faction context: pilots, dev spawns, the switch, births in a
district that changed hands.

**F3. Bots of both factions in one game.**
- The bot template is left at the last faction used (`FactionCharacterPatches.cs:124-135`); confirm every creation
  sets it.
- Each faction's secondary bot needs (Folktails Catalyst and PunchCard, Iron Teeth Grease and ControlTower): the plan
  names them, nothing checks them.
- Bot parts are shared goods; bot fuel isn't.
- Charging stations are attractions: which faction's bots can use which.

**F4. Both Wonders in one game** (P-4, V1, V2): each colony builds its own; the effects; the single completion.

**F5. Where the two factions meet.**
- Water (shared), power (P1), blasts (X2).
- Ranged effects of decorations, monuments and the beehive on the other faction's characters and crops.
- A zipline beside an Iron Teeth colony.
- A handover to a player of the other faction (M13), a steward of the other faction.
- Trading (T2).

**F6. Coverage of the desync checks in a mixed game.** The daily fingerprint hashes each character's faction and its
number of needs (`ColonyDiagnostics.cs:182-189`), nothing per colony by faction, and not the needs' values. The
heartbeat's digest never sees a character's faction. **Check:** whether a late-game character made with the wrong
faction on one computer would be caught before it changes anything else. Add per-colony counts by faction to the
daily line if that is cheap. B-4 asked for a check that the mixed line contains `chars:`; it was never added. Add it.

**F7. Loading both factions.** Memory, load time and asset count for a late mixed save against a single-faction one
(the plan's R6 said "measure once"; it never was). B6.

**F8. The display at scale.**
- Paths repainted at least twice per load, 12 child lookups each (`FactionModelPatches.cs:47-56`); a handover repaints
  every path.
- The Ctrl+T window's `IsUntouched` walks every entity every second (`TradeOverviewPanel.cs:178-184, 478-479`).
- Measure on a save with 3,000 paths.

**F9. A mixed save's life.** Save, load, rehost and the waiting room's save room for a late mixed save: sizes, the
`SaveColonyReader` stream of a large save on the menu's thread (beta21's C-E6, left for a playtest; measure it now).

### Tier 1c: Trading Posts at scale

Kyler: "large scale, repeating trades, and trades covering all the good types have not been performed yet."

**T1. The exchange's life over many cycles.** `ColonyExchangeService.Tick` checks every post every `CrossingInterval`
(8) ticks (`TradingPostExchange.cs:316, 436-495`). **Check every transition:**
- proposed, accepted, active;
- rounds crossing;
- keep and floor;
- cancel, and end on colony change or faction change (:466-479);
- through save, load, rehost and reconnect after a desync;
- a player away and their colony handed over (`ColonyAbsence`, `ColonyHandover`);
- a steward acting.

A round in progress at every one of these moments.

**T2. Every good, in both directions.**
- Every good of each faction and the common goods; the 17 that cross between factions; science; beavers (adults only,
  never between factions, the last adult stays).
- Liquids (tanks), pile goods, underground-pile goods.
- Goods a colony can't store (D3), goods with no storage built yet, goods from other mods.

**Check, by script:** for every good, the offer form, the picker, the prefill, the wishlist, the host's judgement and
the replay agree. Then Script T: one exchange per good, both ways.

**T3. Conservation.** Across a round, what leaves one colony arrives in the other: stock on the halves, goods carried
by haulers, science, beavers. Nothing is duplicated when a round crosses in the same tick as a save, a deletion, a
flood or a handover.

**Check:**
- the rule in StabilityTests, over the exchange code's pure parts (`ExchangeTerms`, the ledger);
- a daily conservation line in the detailed log (§5.3): per good, each colony's stock and the goods in transit,
  against the ledgers. Script T shows it.

Consider a cheap version of it in the daily fingerprint line.

**T4. Scale.** Many posts, many exchanges, the largest amounts (`ExchangeTerms.MaxAmount`, 100 per round).
- The hauling load a round creates, and LateGamePerformance's hauling cache (C1).
- The post's capacity transpiler (`ColonyTrading.cs:165-200`).
- `GetEnabled<DistrictCrossing>().ToList()` every check (:453).
- `BeaversToSpare`'s LINQ over adults (:517-524).
- `OwnerOf` twice per open exchange.

Measure with 20 posts and 40 exchanges.

**T5. Stalls.** An exchange that can never complete (no hauler reaches the post, no storage, a flooded or paused
half, a half with no road) must say why and not tie up goods forever. **Check** each stall's notice, and that the
goods come back when it ends.

**T6. Trading Posts and the late game.** Dynamite or a tunnel removing a half mid-round (partner-only removal, goods in
transit); a flood; automation pausing a half (`PausableBuilding`); a half on a zipline or tubeway network.

**T7. The trading windows at scale.** Ctrl+T (`TradeOverviewPanel`, 905 lines) and the post's panel
(`TradingPostFragment`, 1,560 lines; 2 Hz allocations noted in beta1) with 40 exchanges and every good in the picker;
the wishlist and the ledger (`LedgerLength = 20`, :73) after 50 cycles.

**T8. Trading between factions at volume.** The 17 goods plus science only, at every place a good is chosen; a round
voided when a colony changes faction (:474-479, 709); the faction switch refused while an exchange is open.

### Tier 2: scale, performance, long sessions

The planning sweep's inventory of every tickable, updatable and hot-path patch, with estimated costs, is
[`review-1.4.0-beta24/planning/2-hot-paths.md`](review-1.4.0-beta24/planning/2-hot-paths.md). Its estimates are from
reading. The review can't measure in the game, so it:
- measures what runs outside the game (micro-benchmarks of the mod's own code, in StabilityTests or RuntimeChecks);
- makes the mod report the rest (§5.3);
- gives Kyler Script P to record it.

**S1. The mod's per-tick cost at late-game size.** Every tickable singleton and every patch on a per-tick, per-entity
path gets:
- a `ColonyProfiler` spot, so the Ctrl+Shift+J report and Script P show it on a late save, shared and split into two
  colonies (B2);
- where its code runs outside the game, a micro-benchmark at late-game sizes.

Anything O(entities) per tick gets a number. The largest candidates, from reading:
- working hours: per beaver and per workplace every tick, about 1–1.5 ms per tick at 1,000 callers, and its profiler
  spot costs about as much as the work (`ColonyWorkingHours.cs:136-180`);
- yielder searches and planting spots, which recompute the searcher's owner per candidate
  (`ColonySeparationPatches.cs:86-128`);
- mixed factions' `YieldRemovingBuilding.IsAllowed` postfix, which recomputes the building's faction per call
  (`Factions/FactionBuildingRules.cs:84-95`);
- the exchange check: 6 to 10 `OwnerOf` per open exchange, a closure per `FactionTrade.Allows`, a list copy every 8
  ticks (`TradingPostExchange.cs:450-524`).

**S2. Frame-ending interrupts (P-5).** Have the mod count the interrupts per tick and report them (§5.3), so Script P
shows what they cost in achieved speed at speed 7. Read from the code: with about 10 interrupting buckets per tick at 60
fps, the ceiling is about 5.5 ticks a second, where speed 7 wants 11.7. If they matter, look at firing the interrupt
once per bucket, or raising the one-tick cap on what `GiveBackBuckets` hands back. That is a determinism argument; bring
it to the findings before changing anything.

**S3. Allocation.** Every per-tick or per-frame allocation in the mod (LINQ, `ToList`, string building, closures:
`MixedFactions.Spec` allocates on every call, `DoEntityPrefix` two closures before any check). The late game's hitches
are GC pauses (LateGamePerformance measured a median freeze of 675 ms in a 35-minute co-op session). B3.

**S4. Road-network checks after every navigation change.** In separate colonies,
`ColonyRoadNetworks.OnNavMeshUpdated` runs the game's district-conflict walk over the road graph on every
navigation-mesh update, whatever the update (`ColonyRoadNetworks.cs:157-181`). The mod still sends preview
notifications every frame (`Fixes/InstantNavMeshFix.cs:26-33`). **Check** whether those reach this listener, which
would mean a whole-network walk every frame while a path is dragged. Measure with 3,000 paths and 10+ districts.

**S5. Lockstep when the simulation is CPU-bound.** Early game at "max speed", the computers keep up. Late game at speed
7, or a boost, neither can. **Check** what `CatchUpSpeed` (a lagging guest speeds up, which can't help when the CPU is
the limit), `HostPacing`, `FrameRatePacing`, the game's own `GameSpeedThrottler` for large populations (both colonies
count) and `LargeColonySpeedLimit` do together. Is there a spiral? Does the slower computer set the pace smoothly?
Reuse beta12's frame-by-frame lockstep model with late-game tick costs (estimated, or from Kyler's PerformanceLog
recordings). Script L's maximum-speed step confirms it (B4).

**S6. Saves, rehosts, reconnects and joins at late-game size.** B5. The steps:
- the save at a tick boundary (`FinishParallelTick` first, beta12 B3);
- the rehost reads the whole save on the game thread, with two copies, keeps the bytes for the whole session ("TODO
  remove map", `IO/ServerEventIO.cs:238` in beta24) and hashes them on the game thread;
- the send is paced at 1 MB/s over direct IP;
- the guest hashes the save four times, twice on the game thread;
- `ColonyMarks.Save` sorts every mark three times at every save, autosaves included (`ColonyMarks.cs:58-69`);
- the reconnect after a desync;
- LateGamePerformance's background save (C1).

**S7. Long sessions.** Everything that grows with time:
- the colony journal (`ForgetAbove`), the trade ledger (20 per half), the digest's recent list, trace history
  (`maxTraceTicks` 10);
- chat (2,000 lines), activity;
- per-entity dictionaries that never forget deleted entities;
- `ColonyStamp` and `CharacterFaction` on every entity (save size);
- tick counters and float time sums after hours at a boost of 30.

**Long session:** the mod reports its own collections' sizes and the heap once a day in the detailed log (§5.3).
Kyler's long session (release bar 7) shows whether anything grows.

**S8. The shipping build is unoptimised.** The zip ships **Release Steam**, which the SDK doesn't optimise (left in
beta21 §4, for line numbers). Micro-benchmark the mod's hot paths from both builds outside the game, and check that
`PatchGateChecks` (which reads unoptimised IL) passes on an optimised build. Script P can compare the two in a game.
Decision D4.

**S9. Everything that runs every frame.** With 600 characters and 20 districts:
- **Walking animation rescans each walker's whole path every frame:** `AnimationFixes.cs:75-76` resets
  `_nextCornerIndex` to 0 before `AnimatedPathFollower.Update`. That is O(path length) per walker per frame, in co-op
  only. Keep the index unless time went backwards.
- **The road overlay walks every entity** every 0.5–3 s while any building tool is in hand
  (`ColonyRoadOverlay.cs:95-130, 187-207`).
- **Placement previews:** for each path tile, 5 neighbour checks, each of which may loop over every district
  (`ColonyPlacementValidator` → `ColonyGameWorld.RoadOwnerAt`).
- **Mixed games:** the Ctrl+T window's `IsUntouched` walks every entity once a second (F8).
- **From beta1's backlog:** `ColonyView` status and batch-row patches, the connection panel's rebuild every 0.5 s,
  `ChatView.RefreshColors`.

**S10. What the mod costs in every game, co-op or not (B1).** Several patches have no gate:
- `TickableEntity.Tick`'s prefix and postfix (twice per ticking entity per tick, `DeterminismService.cs:227-241`);
- every game random draw (a `GetSingleton` per draw, :318-376);
- `Guid.NewGuid` (16 random calls per new entity);
- `ParameterProvider.GetParameters`;
- the "not gameplay" markers, including `SoundEmitter.Update` per emitter per frame;
- the `Time.time` native detour, installed in every game (:905-955);
- a process-wide `DateTime.ToString` prefix whose flag is never set (`GameSaveHelper.cs:32-41`);
- `Manufactory.IncreaseProductionProgress` per workshop per tick (`ColonyScienceService.cs:424-470`).

Gate each one behind the co-op flag where that is safe, or show why it must run in every game. Script P's single-player
recordings, with and without the mod, confirm it. The dead code found on the way goes too: `TickWatcherService`, that
`DateTime` prefix, `IsSavingDeterministically`, `DeterminismPatcher.PatchDeterminism`.

**S11. Detailed logging at late-game scale.** With `Settings.Debug` on, the mod builds strings per beaver per tick,
takes a stack trace per trace, hashes and copies the whole water and moisture maps every tick (up to 64 MB), and sends
every trace to the guests every tick (`DesyncDetecter/DesyncDetecterService.cs:143-288`, `ReplayService.cs:933-943`).
The desync dialog can switch it on mid-session (`Events/ConnectionEvents.cs:173-177`). That is exactly what a late-game
player hit by a desync would press.
- **Estimate** its cost per tick at late-game size from the code, and time its pieces outside the game.
- **Decide** what the dialog offers in a large game: a warning, a lighter level, or detailed logging for a limited
  number of ticks.

**S12. The daily colony check.** It walks every entity (about 10 component lookups each) twice a day on every computer:
`ColonyDiagnostics.Tick` (`:146-152`) and `ColonyPresenceEvent.Compare` (`ColonyHandover.cs:451`). That is about 50 to
80 ms in one tick at 20,000 entities, estimated: a hitch every in-game day.
- **Time** the walk outside the game where it can run there, and give it a `ColonyProfiler` spot so Script P shows it.
- Compute it once a day and reuse it, or spread the walk over the day's ticks. The value must stay the same on every
  computer.

### Tier 2: compatibility

**C1. Timber Together with LateGamePerformance, the late-game stack.** LateGamePerformance (Kyler's) was built for
BeaverBuddies co-op, and it rewrites what Timber Together hooks:
- `Ticker.Update`'s catch-up (`source/CatchUp.cs`), against the tick gate and `GiveBackBuckets`;
- the entity tick list (`source/IdleEntities.cs:31, 203`, which knows BeaverBuddies hashes the game's list), against
  the entity-order hash and P-5's interrupts;
- background saves (`source/BackgroundSave.cs:313-328`), against saves at a tick boundary and the rehost save;
- the save snapshot, which pins `BeaverBuddies.Colonies.ColonyStamp` by a hash (`source/SaveSnapshot.cs:138`; stale
  since Timber Together beta2);
- hauling, home and terrain caches, against the colony layer's "each colony's beavers work for it alone".

**Check:** read both mods on every shared target (LateGamePerformance's source is at
`C:/Users/Kyler/code/LateGamePerformance`), then give Kyler a Script P line that records the pair. Findings in
LateGamePerformance go to Kyler as a list; they are not fixed from here.

**C2. Kyler's other mods in a late game.** OptimizedLocalHousing (its singleton is in `R-late`), HungryPathing,
PersistentWorkAreas, MixedStorage (large stockpiles, faction goods), TipsyTail, PerformanceLog (observer). beta21's C2
covered the beta18 to beta20 targets; this one covers late-game targets.

**C3. Late single-player and Stability Fork saves hosted in Timber Together.** `R-late` hosted as a shared colony. Then
split it with *Allow founding colonies in a shared game*: 11,000 entities stamped at one tick (cost, correctness).

### Tier 3: release readiness

**R1. Names and labels for 1.4.0.**
- The manifest's "(beta)", the four settings' "(beta)", and "beta" in README, TWO-COLONIES, WORKSHOP, the site and
  the notes.
- Which features stay labelled beta at 1.4.0.
- *State of testing* rewritten from what has actually been played.

This is for the final release; the candidate keeps beta labels. Decision D5.

**R2. Defaults and logging.** `VerboseLogging` on (the bug-report trail), detailed logging, reporting consent, and the
four host settings' defaults.

**R3. Known limits.** Every §7 decision and every "left" finding documented in TWO-COLONIES *Known limits* and the
README.

**R4. Checks that check less than their names say.** RuntimeChecks checks names and signatures, not behaviour;
nothing runs a game, and by Kyler's rule nothing will. Record which leads only a person's test can settle, so the next
review knows.

**R5. The zip.** Test-only code never ships. Add a check over the zip script's inputs.

**R6. CI.** The wall-clock tests tolerated on the runner (`tests.yml`'s `$timing` list): still right?

**R7. The in-game changelog and docs.** `changelog.txt` (embedded), STABILITY-CHANGELOG, ALPHA-TEST-SCRIPTS (rename to
TEST-SCRIPTS at 1.4.0?), ToTestV6.

**R8. Surviving a game update.** An official release will meet the next Timberborn patch. Beta21 (C-E2) made the
faction patches fail on their own, and the Wonder transpilers switch themselves off. But `WaterSourceTimingFix`'s
transpiler **throws** when it doesn't find exactly one `Time.deltaTime` (`Fixes/WaterSourceTimingFix.cs:34-35`), and a
throw inside the one `PatchAll` stops every later patch.
- **Sweep:** every transpiler, `TargetMethods` and `AccessTools` lookup that can throw at patch time.
- **Decide** per patch: disable the feature and warn, or refuse co-op with a clear message.

A co-op game that runs with a determinism fix silently missing is the worst outcome. `ModListChecks` and the MVID
handshake don't cover this.

---

## 5. Method

### 5.1 Baseline

1. `git fetch`. Confirm the base, copy `env.props` into the worktree, restore from the local cache, and build Release
   and Release Steam into a scratch mods folder (never the installed one). Run both suites (417, 361 at beta24).
2. Decompile the game into the scratchpad.
3. Copy the reference saves into the scratchpad (read-only originals).

### 5.2 Sweeps by script (kept as checks where that's cheap)

1. **UI → simulation writes not recorded** (A2, H2): a Cecil call graph over the game from every UI callback and
   input processor to stores into saved or ticked state, stopping at recorded methods. Allow-list the display-only
   ones, and keep it as a RuntimeChecks check.
2. **Late-game frame paths:** every `IUpdatableComponent.Update` and `UpdateSingleton` in the late-game assemblies
   (automation, water, power, explosions, Wonders, fireworks, ziplines, bots, HTTP) that writes simulation state.
   beta12 classified 163 hooks; confirm none of these was missed, and list any that 1.1.2.4's late-game assemblies
   add.
3. **Colony reach:** for every late-game building type, what else its actions and effects can change (neighbours,
   networks, ranges), against the colony rules (P-3, A4, W1, P1, X2, V1, F5).
4. **Faction content** (F1): `Blueprints.zip` → per faction: buildings, goods, needs, recipes, templates the mod's
   faction code names, and what it doesn't.
5. **Tradable goods** (T2): every good × every place a good is chosen or judged.
6. **The mod's hot paths** (S1, S3, S9 to S12): every tickable, updatable and per-entity patch, its gate, its
   allocations, its complexity. Start from `review-1.4.0-beta24/planning/2-hot-paths.md`.
7. **Crash paths** (H1): every mod entry point in a tick, a replay or a load, with the late-game states it can meet.
   Split by area among the reviewers; the main session merges them into the matrix's crash column.

### 5.3 Without the game: what can be run, and what the mod will report

The review never runs Timberborn (the rule at the top). One playtest found what three reviewers missed (beta23), so
running things still matters. Three things stand in for the game:

1. **The game's code outside the game.** RuntimeChecks loads the game's DLLs into a plain .NET process, without
   Harmony. Where a game class needs no Unity object, run it there and call the mod's patches the way Harmony would.
   beta12 did this for the Wonder timing (`RuntimeChecks/WonderChecks.cs`, whose header describes the method: the
   game's IL decoded, the mod's transpilers run on it, prefixes and postfixes called in Harmony's order, Unity's native
   calls stubbed). Candidates:
   - automation partitions and their evaluation order (A1);
   - `ExchangeTerms` and the ledger (T3);
   - the faction tables against `Blueprints.zip` (F1);
   - `FriendGameRules` (B24).
2. **The mod's own rigs.** StabilityTests already runs TCP host and guest sessions (the `Session` rig in
   `ActivityTransportChecks`) and the lockstep's pure parts. Extend them where a lead is about the wire or the lockstep
   (S5, S6).
3. **Reporting, so that Kyler's own sessions answer the rest.** Add to the Ctrl+Shift+J report and, with detailed
   logging on, to the daily log line:
   - per-tick frame interrupts (S2);
   - `ColonyProfiler` spots for every hot path (S1, S12);
   - each colony's goods and goods in transit against the ledgers (T3);
   - characters per colony and faction (F6);
   - the mod's own collections' sizes and the heap (S7).

   These are cheap counters. They are never on by default where they cost anything, and they change nothing the
   simulation reads.

**No test rig and no scenario saves.** Kyler builds late-game test games himself, following §6's setup steps.

### 5.4 Measurements

The review measures only outside the game: micro-benchmarks of the mod's own code at late-game sizes, in StabilityTests
or RuntimeChecks. It turns the budgets (§1) into **Script P** (§6), which Kyler records with PerformanceLog when he
chooses. The session reads the recordings with `perflog.py report` and `compare`.

| Run | What Kyler records | Budgets and leads |
|---|---|---|
| P1 | Single-player, the late save, with and without Timber Together | B1, S10 |
| P2 | Hosting it with nobody joined, shared, then split into two colonies | B2, B3 |
| P3 | With a guest, at speeds 3 and 7 and a boost of 15 | B4 |
| P4 | A mixed game against a single-faction one of the same size | B6 |
| P5 | P2 again with LateGamePerformance | C1 |
| P6 | A rehost | B5 |

Every run uses the same save, speed and window size, and records at least 3 minutes after a warm-up.

### 5.5 Reviewers

Four reviewers and the main session. Each tries to refute its own findings first, then hands over evidence (file:line
in the mod and the game, or a check).

| Reviewer | Leads | Notes |
|---|---|---|
| **A**, the late-game systems against the game | A1 to A6, W1 to W4, P1, P2, X1 to X4, O1 to O4, H1, H2; sweeps 1 to 3 | Writes Script L's lines for its leads |
| **B**, mixed factions and both Wonders | F1 to F9, V1 to V3; sweep 4 | Writes Script M |
| **C**, Trading Posts at scale | T1 to T8; sweep 5 | Writes Script T |
| **D**, performance, long sessions, compatibility | S1 to S12, C1 to C3; sweep 6; §5.4 | Micro-benchmarks, the §5.3 reporting, Script P |
| **Main** | R1 to R8; B24-a to B24-d; P-1 to P-5 end to end; the §7 defaults; checking every finding again before it goes in the report; the fixes and the release | Puts Scripts L, M, T and P together |

Each reviewer also fills the coverage matrix's rows for its area, the crash column included (sweep 7).

Give reviewers a frozen `git archive` copy of the base and a shared brief. Merge their diffs yourself (see the
appendix).

### 5.6 Output, fixes, release

1. **Findings** in `design/REVIEW-FINDINGS-1.4.0-beta24.md`, in the beta21 format. Each finding gets:
   - its status (Confirmed, Plausible, Refuted);
   - its kind: desync, crash, session-stopper, gameplay, performance, display or docs;
   - who it hits: everyone, separate, mixed, trading;
   - evidence, a fix, and its wire and save impact;
   - the check added, and a test-script line.

   Add a **found sound** list and a **left, and why** list. Reviewer reports go in `design/review-1.4.0-beta24/`.
2. **Fixes,** each with a check that fails without it (StabilityTests or RuntimeChecks). Where only the running game can
   show it, add a script line for Kyler, and the finding stays open until his test comes back. P-1 is fixed first; it's
   two lines.
3. **Release:** the release candidate (D1), with the usual loop:
   - version bump, changelog entry, `changelog.txt`, test-script lines;
   - TWO-COLONIES *Known limits* and *State of testing*, README;
   - both builds (into a scratch folder, never the installed mod), both suites, zip, notes, tag, `gh release`;
   - push trading-exchange, main and the branch, fetching first;
   - CI after the push.

   No website update until 1.4.0 final (Kyler's standing instruction since beta23).
4. **Report back:** the release link, what was found and fixed, what was left, check counts, the micro-benchmarks
   against the budgets, and the playtests and recordings that are owed.

---

## 6. The two-player late-game playtest (written by this review, played later)

The real test comes later: Kyler and a friend in a late-game colony. The review writes it into ALPHA-TEST-SCRIPTS as
one playtest in three parts (Scripts L, M and T), ordered to cover the most per hour. Each step:
- names the matrix cells it closes;
- says what should happen;
- says what to send: both players' `Player.log` and Ctrl+Shift+J reports.

Each part starts with setup steps:
1. build the scenario in single-player with dev mode on (placing finished buildings, adding beavers and science);
2. save it;
3. host it.

Dev tools are fine in single-player; in co-op they desync.

- **Script L, the late game, two players (about 90 minutes)**, on a late save with an automation park, split into two
  colonies:
  - automation and HTTP levers on both sides;
  - water automation through a drought and a badtide;
  - a dynamite chain beside the other colony;
  - a Wonder;
  - maximum speed for 20 minutes;
  - a rehost;
  - both Ctrl+Shift+J reports compared.
- **Script M, mixed factions in the late game**, on a new mixed game grown with dev tools: both Wonders, bots of both
  factions, births, tubeways and ziplines, trading all 17 goods, a handover across factions.
- **Script T, Trading Posts at scale**, with 20 posts and 40 exchanges: every good both ways, repeating for 10 cycles, a
  stall, a post blown up mid-round, save and rehost mid-round.
- **Script P, performance recordings** (§5.4): PerformanceLog recordings P1 to P6, a few minutes each. Send the
  session folders.
- **The long session** (release bar 7), when convenient.
- Still owed, in this order: D7, D7a and D8 (a game from the waiting room); B24-b (the Join co-op box); F;
  then beta12's B 8t to 8y.

---

## 7. Decisions for you

The review starts with the defaults below unless you say otherwise. Each product default applies only once the review
has confirmed the facts behind it; anything that turns out to need your judgment comes back to you with a
recommendation, the way the Earth Repopulator question did in beta12.

**D1. Scope and name.** Review, fixes and a release candidate in one run, named **1.4.0-rc1** (recommended). 1.4.0
final follows once D7 to D8, B24-b, F, L, M and T have been played. Alternatives: beta25, or findings only.

**D2. No game control.** Settled by Kyler: the review never starts or drives Timberborn and never changes the
installed mods (the rule at the top). Nothing to decide.

**D3. Product defaults for what reaches across colonies:**

| Question | Default | Why |
|---|---|---|
| Power networks joining across colonies (P1) | **Allow and document**, after confirming nothing breaks (clutches, meters, batteries) | Deterministic. A placement rule like the road rule is new risk this close to release |
| Synchronised water buildings (W1) | **Synchronise within the actor's colony only** | A recorded action must not change another colony's buildings. That's the mod's own rule |
| Population Counter in global mode (A4) | **Count the counter's own colony** | "A colony is its districts" |
| Dynamite and tunnel blasts reaching the other colony (X2) | **Allow and document** (the map is shared, like water) | Refusing by blast radius needs a preview of the chain |
| HTTP API (A3) | **Support it as each player's own actions and document it**; drop `SetColor` in co-op if it can change saved state | It already works through the recorded lever |
| A Wonder's effect and the single completion (V1, P-4) | **Keep the game's behaviour and document it** | The completion is the map's, as in single-player |
| G9, terrain left by a co-op deletion (X4) | **Document it for 1.4.0**, and fix it if the review finds it small | |

**D4. The unoptimised shipping build.** Default: micro-benchmark it (S8), and turn optimisation on for 1.4.0 final if
the gain is real, Script P agrees and `PatchGateChecks` still passes. The candidate keeps the current build.

**D5. Beta labels at 1.4.0 final.** Default: drop "(beta)" everywhere, except on *Mixed factions for new games* until
Script M has been played.

**D6. Reviewers:** four plus the main session (recommended), or two (A+D, B+C) to spend less.

---

## 8. Out of scope

- Starting, driving or testing the game in any way, and any automated in-game testing (Kyler's rule).
- The lockstep core and the earlier found-sound lists, except where named above.
- The waiting room's join flow (beta21 reviewed it; D7 is a playtest).
- Fixing other mods (C1 and C2 findings go to Kyler as lists).
- The Stability Fork: its late game is shared-colony only. Say which findings would port, but don't port them.
- A third faction.
- Direct-IP identity as a security question.

---

## Appendix: notes for the session that runs this

From earlier reviews and releases (the project memory has the detail):
- **Building:**
  - Copy `BeaverBuddies/env.props` from the main checkout.
  - Restore with `-s C:/Users/Kyler/.nuget/packages`, then always build with `--no-restore`.
  - Build into a scratch mods folder with `-p:BeaverBuddiesModsPath=<scratch>/`, always. Without it the post-build
    step overwrites the installed mod, which the rule at the top forbids.
  - The zip ships **Release Steam**.
- **RuntimeChecks** takes the DLL and the game's `Managed`, Harmony and Mod Settings folders
  (`dotnet run --project RuntimeChecks -- <dll> <Managed> <workshop>/3284904751 <workshop>/3283831040/version-1.1/Scripts`).
  It has no global logger; use `QuietLoggerProxy`.
- **StabilityTests** has a 5 s limit per test. Real-socket stall tests can pass locally and fail on the runner; use a
  stream whose writes really block.
- **Line endings.** The repo is CRLF with some BOMs; reviewer diffs are LF. Apply them with the scratchpad's
  `applydiff.py`, or an `ed.py` that keeps BOM and CRLF. Write scripts with the Write tool; Bash heredocs mangle
  backslashes.
- **Harmony:**
  - Never stack two `[HarmonyPatch(nameof(…))]` names on one method; use `TargetMethods()`.
  - A recording prefix is `Priority.First`.
  - A prefix that replaces a game method is `Priority.Last`.
  - Every per-frame or per-tick patch reads a static flag before any `GetSingleton`, uses a declared
    `ColonyProfiler.Spot`, and allocates nothing per tile or entity.
- **Characters:** never delete a character without killing it (`Character.DestroyCharacter`).
- **Releasing:**
  - `git fetch` and `merge-base --is-ancestor origin/trading-exchange HEAD` right before pushing; rebase, don't merge,
    if main moved. `changelog.txt` is embedded, so rebuild after a rebase.
  - `gh` defaults to the upstream fork: always pass `--repo timbermods/TimberTogether`.
  - Right after a release, `gh release view` can show no assets; the API listing is the authority.
  - Check the CI "Tests" run on the tag and the branch.
