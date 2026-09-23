# Review findings: the late game, mixed factions and trading at scale, MultiColony 1.4.0-beta24 → fixed in 1.4.0-rc1

**Reviewed:** `v1.4.0-beta24` (`e7adcf4`), with the plan committed on top (`7d3eaa8`), against Timberborn 1.1.2.4. The
review followed [REVIEW-PLAN-1.4.0-beta24.md](REVIEW-PLAN-1.4.0-beta24.md). Its fixes and docs shipped as
**1.4.0-rc1**, the release candidate for 1.4.0.

**The rule it ran under:** never start, drive or test Timberborn, and never touch the installed mods (Kyler,
2026-09-23). The game was never started. Every build went to a scratch folder, and saves were read only from copies.
What only the running game can show is written as test steps (§6 and ALPHA-TEST-SCRIPTS, Scripts L, M, T and P).

**How it was done:**

| Who | Leads | Report |
|---|---|---|
| **A**: the late-game systems against the game | A1–A6, W1–W4, P1–P2, X1–X4, O1–O4, R8, H2; sweeps 1, 2 and 7 for its area | [A-late-game.md](review-1.4.0-beta24/A-late-game.md) |
| **B**: Folktails and Iron Teeth in the late game, both Wonders | F1–F9, V1–V3; sweep 4 (faction content), sweep 7 for its area | [B-mixed-factions.md](review-1.4.0-beta24/B-mixed-factions.md) |
| **C**: Trading Posts at scale | T1–T8; sweep 5 (every good × every place a good is judged), sweep 7 for its area | [C-trading.md](review-1.4.0-beta24/C-trading.md) |
| **D**: cost at late-game size, long sessions, compatibility, the reporting | S1–S12, C1–C3; sweep 6, §5.3 and §5.4 | [D-performance.md](review-1.4.0-beta24/D-performance.md) |
| **E**: the colony lifecycle and the other simulation events (added while running) | Tools, batches, stamps, owners, hand-over, stewards, absence, founding, the road rule | [E-colony-lifecycle.md](review-1.4.0-beta24/E-colony-lifecycle.md) |
| **The main session** | P-1 to P-5, B24-a to B24-d, R1–R8, the H1 scan; checking each finding again; merging; the release | Here |

- The five ran in parallel, each in its own worktree, against all 497 decompiled `Timberborn.*.dll`, `Blueprints.zip`
  and three late saves (`R-late`, `R-blast`, `R-long`, read-only copies).
- Each branch was merged only after its diff and evidence were read again. For example:
  - every setter added to `AutomationEvent` has no caller in a tick;
  - D's change to entity IDs outside a session is safe, because every co-op path installs its `EventIO` before the
    scene loads (`ClientConnectionService.cs:153`, `ServerHostingUtils.cs:162`, `LobbySession.cs:263`);
  - D's single daily walk is taken at the same point on the host and the guests (`ColonyPresenceEvent.Replay`,
    after `Seen`);
  - C's `TradeTotals` move keeps its save format and hash.

  One finding did not survive that check (**E-6**, §2).
- The planning notes are in [review-1.4.0-beta24/planning/](review-1.4.0-beta24/planning/).

**Status key:**
- **Confirmed:** traced end to end in the mod and the decompiled game, and, where it can run outside the game,
  reproduced by a check.
- **Plausible:** the path exists, and one link could not be proven without the game.
- **Refuted:** checked and found sound.

**Who it hits:** **All** (every player), **Separate** (separate-colonies games), **Mixed** (Folktails and Iron Teeth
together), **Trading** (games with Trading Posts), **Mods** (players whose mods differ).

**The checks:**
- **StabilityTests 417 → 445** (28 new):
  - Main 5 (`RcMainChecks`);
  - A 5 (`RcLateGameChecks`);
  - B 4 (`RcFactionChecks`);
  - C 7 (`RcTradingChecks`, with a 72,000-step exchange model);
  - D 4 (`RcPerformanceChecks`, with a lockstep model in `RcPerformanceLockstepModel`);
  - E 3 (`RcColonyChecks`).
- **RuntimeChecks 361 → 426 on both builds** (65 new):
  - Main 2, A 11, B 12, C 9, D 20, E 11 (`Rc*RuntimeChecks`).
  - They run against the game's own assemblies, and where the game's code runs without Unity they run it. Examples:
    the Folktails bot's animator built from its blueprint, the game's `CanPlant`, `GetRecipe` and
    `AnimatedPathFollower`.
- **Nearly every fix's check fails on beta24's DLL or source.** The reports list which. The ones that pass on both are
  guards for things found sound: the H1 scans, F1, F3 and the D-S5 model.
- **Both builds (Release Steam and Release) have 0 warnings.**
- **CI:** D-S1 compares two timed loops, so it joins the runner's tolerated timing list (R6).

---

## 1. Summary

| ID | What | Hits | Status | Kind | rc1 |
|---|---|---|---|---|---|
| **B1** | A Folktails bot piloting an Earth Repopulator's plane throws in the replayed activation: multiplayer stops for everyone, the Wonder half activated | Mixed | Confirmed (the game's code throws in RuntimeChecks) | **session-stopper** | Fixed |
| **A2-1** | A pump's flow rate (the 1.1 slider on every pump) is changed on the dragging player's computer only | All | Confirmed | **desync** | Fixed |
| **A2-2** | A throttling valve's slider shares the limit but not its on/off | All | Confirmed | **desync** | Fixed |
| **A2-3** | The dev power generator's panel works without dev mode and isn't shared | All (saves with one) | Confirmed | **desync** | Fixed |
| **E-1** | A planting mark of a crop the host's game lacks throws in the host's replay | Mods | Confirmed | **session-stopper** | Fixed: refused on the host; a guest leaves quietly |
| **E-2** | A distribution change for a good the host's game lacks throws | Mods | Confirmed | **session-stopper** | Fixed: skipped |
| **H1-1** (= E-9) | A workshop recipe the host's game lacks throws | Mods | Confirmed | **session-stopper** | Fixed: refused on the host; a guest leaves quietly |
| **R8-1** | After a game update, the water-seep fix's transpiler throws inside `PatchAll`, and a renamed shared setter throws out of `StartMod`. The rest of the mod is left unpatched, and co-op could still start | All (after an update) | Confirmed by reading | **session-stopper / silent desync** | Fixed, with the guard |
| **R8 (main)** | Any patch class that fails after an update takes every later one with it | All (after an update) | Confirmed | **session-stopper / silent desync** | Fixed: each class applied alone; failures named; co-op refused at load |
| **D-new-2** | Switching detailed logging on mid-session on one computer reads as a desync and stops the session | All (detailed logging) | Confirmed (fails on beta24's DLL) | **false desync** | Fixed |
| **D-new-1** | The on-demand patch (found while fixing D-S10d) would draw its IDs from the game's random state on one computer | All | Confirmed | **desync** (would have been) | Fixed before it shipped |
| A2-4 | Dev mode's Ctrl while planting spawns plants on one computer | Dev mode | Confirmed | desync | Fixed: off in co-op |
| **W1-1** | Synchronised fill valves, throttling valves and floodgates change and rewire the other colony's touching ones | Separate | Confirmed | **one colony changing another** | Fixed |
| **A4-1** | A Population Counter set to count everywhere counts every colony | Separate | Confirmed | one colony changing another | Fixed |
| **E-8** | A beaver or bot left without a district joins the nearest district center, whoever's (across factions in a mixed game) | Separate, Mixed | Confirmed | **one colony changing another** | Fixed (new save singleton) |
| **E-7** | A shared game split by a founding leaves every mark nobody's: the new colony works the first colony's fields and forests | Separate | Confirmed | gameplay | Fixed |
| E-3 | A steward's colony is handed over unwarned on the first day nobody keeps it, or after a load | Separate | Confirmed | gameplay | Fixed |
| E-5 | Two players unlocking the same building at once pay twice | Shared science | Confirmed | gameplay | Fixed |
| O4-1 | Every player sees every colony's Indicator warnings | Separate | Confirmed | display | Fixed |
| **P-1** | Reset and Reset all on the automation panel are swapped in co-op | All | Confirmed | gameplay | Fixed |
| **A1-1** | A spring-return lever never sets off a Detonator in co-op (the one-tick pulse arms and disarms it in one evaluation) | All | Confirmed | gameplay | Fixed |
| A3-1 | `HttpLever.SetColor` (saved state) from the HTTP API isn't shared | All | Confirmed | saved display state | Fixed: ignored in co-op |
| **C1** | A paused Trading Post ends the exchange between two colonies of the faction the host didn't pick | Mixed, Trading | Confirmed | gameplay | Fixed |
| **C2** | A round of beavers moves fewer than agreed (haulers, the unreachable) while the other side pays in full | Trading | Confirmed | gameplay (what was agreed isn't kept) | Fixed |
| **C4** | Goods already on a giving half are never counted toward its round: in one case the round never fills | Trading | Confirmed (model) | gameplay (a stall) | Fixed |
| C3, C8 | A stalled round never says why, in the panel or Ctrl+T; Ctrl+T drops a post whose own half lost its road | Trading | Confirmed | display | Fixed |
| C5 | A post removed mid-round says nothing | Trading | Confirmed | display / log | Fixed |
| C6 | Nothing checks goods conservation in a running game | Trading | test gap | detection | Fixed: a daily trade check and line |
| C7 | The every-8-ticks check copies a list, counts with LINQ, recomputes owners | Trading | Confirmed | performance (small) | Fixed |
| B2 | An unreadable faction catalog throws in ticks and replays | Mixed | Plausible trigger | crash hardening | Fixed: the game's answer as fallback |
| B3, B4 | Hot faction lookups; paths painted twice by name search at load; Ctrl+T walks every entity each second | Mixed | Confirmed | performance | Fixed |
| B5 | Activating a Wonder plays no launch sound in co-op | All | Confirmed | display | Fixed |
| F6 | The daily check can't name a mixed game's characters by colony and faction | Mixed | test gap | detection | Fixed: `/census:` |
| D-S10 | Every game, co-op or not, pays for random-draw checks, 16 draws per new entity's ID, the markers, and a patch on every entity's tick | All (single player too) | Confirmed | performance | Fixed (the `Time.time` detour left) |
| D-S9 | The walking animation searches each walker's path from its first corner every frame | All (co-op) | Confirmed | performance | Fixed |
| D-S12 | The daily colony check walks every entity twice a day | Separate | Confirmed | performance | Fixed |
| D-S4 | The road-networks walk runs after navigation updates that changed no road | Separate | Confirmed | performance | Fixed |
| D-S7 | Detailed-logging traces, the Trading Post template cache and disposed Steam callbacks grow without bound | All | Confirmed | memory | Fixed |
| D-S11 | Detailed logging can't keep up at 600 beavers (≈27 ms a tick), and the desync dialog can offer it there | All | Confirmed (measured) | session-stopper when used | Fixed: not offered from 200 characters |
| D-S2 | Every deletion in a tick ends that frame's ticking; nothing counted it | All (co-op) | Confirmed | performance | **Left** (determinism needs it); counted and reported |
| §5.3 | Nothing reported interrupts, memory or the mod's collections for a long session | All | test gap | detection | Fixed: report rows and a daily `[Perf]` line |
| H1 | Does every replayed action cope with an entity gone before it plays? | All | Refuted as a bug (all 59 do) | crash | A source scan keeps it so |
| H1-2, H1-3 | The relay panel can index past its list; the timer panel's static reference | All | Confirmed | interface | Fixed |
| X4-1 (= E-4) | A recorded deletion leaves the tool's picked terrain in its list, growing all session | All | Confirmed | display | Fixed |
| H2 | The dev-mode co-op warning doesn't name the tools and keys | All (dev mode) | Confirmed | docs | Fixed: the warning names them |
| B24-a | A friend's lobby text is shown as it comes: long, multi-line, rich text | All (Join co-op box) | Confirmed | display | Fixed |
| **B24-b** | Enter while typing an IP address with a friend's game selected joins the friend's game | All (Join co-op box) | Confirmed by reading (the game gives Enter to the panel stack while a field has focus) | gameplay | Fixed; B24-b stays a person's test |
| B24-c | *Already started* may never show: Steam may stop reporting an unjoinable lobby | All | Plausible | display | **Left**, documented |
| B24-d | A same-version, other-build DLL lists as joinable, then is refused at the handshake | All | Confirmed | display | **Left**: the handshake's message is clear |
| **E-6** | The host's check of a played placement reads its own tool previews | – | **Refuted** at merge | – | E's fix taken out again |
| P1 | Two colonies' shafts that touch make one power network | Separate | Confirmed | cross-colony (deterministic) | **Left**, documented (D3) |
| X2 | Blasts reach any colony | Separate | Confirmed | cross-colony (deterministic) | **Left**, documented (D3) |
| G9 | Demolishing a platform leaves terrain-block dirt floating in co-op | All | Confirmed | gameplay | **Left**, documented (not small) |
| C-left | A paused post's half in no colony can be removed by any colony | Separate, Trading | Plausible | cross-colony (minor) | **Left**, documented |
| E-10, E-11 | The road rule is over-strict for road-carrying buildings; vertical tubeways judged on one level | Separate | Plausible | gameplay (safe side) | **Left**, documented |

---

## 2. The findings that mattered

### B1: a Folktails bot flying the Earth Repopulator stopped the session (Confirmed; mixed)

**What happened.** When the Earth Repopulator is activated, `Pilot.PrepareForFlying` sets the animation flag
*Piloting* on each worker, with no check. The game's animator reads flags from a dictionary, so a name the animator
lacks throws. Every beaver and every Iron Teeth bot has the flag; **a Folktails bot has not**.

**How a game gets there without dev mode.** The Wonder takes bots, and Iron Teeth players use bots as pilots on
purpose, since pilots are lost. A Folktails colony with bots is handed to an Iron Teeth colony: by the host (Ctrl+T),
or by the absence setting, which in a two-player mixed game can only pick the other faction. The receiver builds an
Earth Repopulator in that district, sets it to bots and activates it. The activation is a replayed action, so every
computer throws in the same place, and `AbortReplay` stops multiplayer for everyone. The Wonder is left half activated
in the save.

**Reproduced outside the game:** RuntimeChecks builds the Folktails bot's animator from its blueprint and calls the
game's own `SetBool("Piloting", true)`; it throws.

**Fix.** A transpiler sends that one call through `FactionAnimation.SetBoolIfItHas`. In a mixed game it leaves out a flag
the animator lacks; anywhere else the call is the game's own. A body that has changed is left alone and logged,
never thrown. Script M8 plays it.

### The desyncs: panels that write the simulation without going through a shared action (A2)

A2 swept the game's IL: every method of the UI assemblies and the UI plumbing, 9,250 methods. Every call into the
simulation that isn't a query was classified against the 142 game methods the mod records:
- **223 calls are unrecorded**, each on an allow-list with its reason: covered by a recorded caller, dev mode, map
  editor, display, a panel's own choice, this computer's own, or a new game's start.
- **Three were real desyncs:**
  - the pump's flow rate (new in Timberborn 1.1);
  - the throttling valve's limit toggle;
  - the dev power generator, which works without dev mode once placed.
- **A fourth is dev mode's:** Ctrl while planting spawns plants on one computer.

The list is now a RuntimeChecks check, so **a game update that adds a UI path to the simulation fails the build** until
someone classifies it.

### Content from one player's mod: three ways to stop everyone's game (E-1, E-2, H1-1)

MultiColony only warns when players' mod lists differ. beta9 made the host refuse a *building* it doesn't have. The
same hole was open for:
- a crop (`TemplateNameMapper` throws inside the game's `CanPlant`);
- a good's distribution setting (`GetGoodDistributionSetting` throws);
- a recipe (`GetRecipe` is a dictionary indexer).

The host now refuses the mark and the recipe, the distribution change is skipped where the good doesn't exist, and a
guest who meets content the host used leaves quietly (`MissingContentException`), as for a building.

### Surviving a game update (R8)

An official release will meet the next Timberborn patch. Before rc1, three things could go wrong:
- **The seep fix threw.** Its transpiler threw unless it found exactly one `Time.deltaTime`.
- **A renamed setter threw.** A shared setter the update renamed threw out of `StartMod` before the save and clock
  patches were installed.
- **One failure took every later patch with it.** Any patch class that failed ended `PatchAll`.

In each case co-op could still start on a half-patched mod, which is the worst outcome: a desync that nothing catches
quickly.

**Now:**
- Each patch class and each hand-made patch is applied on its own.
- A failure is logged and named (`Plugin.FailedPatches`), and single player carries on.
- The seep fix leaves the method as the game has it and says why.
- `CoopFixGuard` stops a co-op game at load on each computer, with a message naming what is missing. It does so only
  when that game needs it: a missing seep fix counts only on a map with a seep.

### One colony changing another (W1-1, A4-1, O4-1, E-8, E-7)

Nothing in `Colonies/` knew about water-building synchronisation, the counters' global mode or beavers without a
district:
- **Water buildings.** Every synchroniser walks touching buildings of its kind and copies heights, limits and the
  automation input, so a valve set, or wired, by one player changed the other colony's. Synchronising now stops at
  another colony's building.
- **Beavers without a district.** One left without a district (its center deleted, cut off by a blast or flood) went
  to the nearest district center it could walk to, whoever's. `ColonyCitizens` now records the colony it left, and
  only that colony's centers take it. With none in reach it waits, as in the game, and follows a hand-over.
- **Marks after a split.** Splitting a shared late save stamped every building but left every planting and cutting
  mark nobody's; they now become the first colony's.

### Trading Posts (C1, C2, C4)

**What was sound.** The exchange engine itself: every good crosses by the game's own `TransferStock`, under a capacity
rule the game mirrors onto both halves. So a round can't throw, duplicate or lose goods. C proved it by reading and
by a 72,000-step model.

**What was wrong were the edges a late game reaches:**
- **C1.** A half in no district counted as the host's faction, so a paused post ended a non-host-faction exchange.
- **C2.** The count that let a beaver round in and the list that moved beavers were different rules.
- **C4.** Goods already on a half were never held for the round. A colony offering back goods it had received and
  couldn't store waited forever.

### D-new-2 and D-new-1: detailed logging

- **D-new-2.** Turning *Always Use Detailed Logging* on mid-session made up a trace for every tick already played, each
  naming the *current* tick. The other computer compared its real traces with them and stopped the session with a
  desync that wasn't one.
- **D-new-1.** While making detailed logging's patch on demand (D-S10d), D found that a Harmony patch made mid-session
  asks for GUIDs. In a session those are drawn from the game's random state, which would have desynced one computer.
  The patch now runs with real GUIDs and restores Unity's random state.

### P-1 and A1-1: automation that did the wrong thing on every computer

- **P-1.** The replay of Reset and Reset all had its two branches swapped (found while planning).
- **A1-1.** In co-op a click is played at the tick's start, and a spring-return lever is on for exactly one tick. The
  Detonator disarms (and un-triggers its dynamite) when its input goes off at the same `Time.time` it armed. So the
  pulse armed and disarmed it in one evaluation, and nothing exploded. In co-op the Detonator now only arms: within one
  evaluation there is no flicker to filter.

Neither was a desync. Both mean late-game automation doesn't do what the player built.

### E-6 refuted at merge: the lesson

**E's claim.** The host's check of a played placement asks the game's `DistrictPreviewsValidator`, which reads the host's
own tool previews. E replaced the check with a copy of `BlockObject.IsValid` without that validator.

**Why it was wrong.** The validator already passes while a replay runs (`DistrictPreviewsValidatorReplayPatcher`,
since 1.3.0-exchange-alpha1), and the host checks a played placement only in its replay. So nothing was ever refused.

**What changed at merge.** The copy was taken out, since it would have had to follow the game's `IsValid` through every
update. The checks now pin what does the work instead.

**Lesson for the next review:** a reviewer given part of the code should search the whole mod for patches on the game
method it reasons about.

---

## 3. The coverage matrix

The release bar asked for one row per late-game feature and one column per way it can fail, with no empty cell. Below
is every reviewer's row, reduced to its verdicts. **The evidence for each cell** (file and line in the mod and the
game, the check, or the script step) is in that reviewer's report, §2.

**Reading a cell:**
- **Sound:** found sound, with evidence.
- **Fixed:** fixed, with a check that fails without it.
- **Left:** documented, with the reason.
- **Playtest:** settled by that step of the late-game playtest.
- **Refuted:** a lead that turned out fine.
- **Plausible:** plausible, not proven.

A few reviewers' cell ids use their own lead names (S2, F8, D3 and so on); §1 maps them.

**Every cell is filled.** The only **Plausible** desync or crash cells are LateGamePerformance's (C1-7, C1-8, C1-9, row
*With LateGamePerformance*). They are findings in that mod, for Kyler (§7). MultiColony's side is the doc line that every
player needs the same version of any mod that changes the simulation.

| Area | Feature | Desync | Crash | Cross-colony | Mixed | Cost | Save/rehost |
|---|---|---|---|---|---|---|---|
| A | Levers | Sound | Sound | Sound | Sound | Sound | Sound |
| A | Relay, Memory, Timer, Chronometer, Weather Station | Sound | Sound | Sound | Sound | Sound | Sound |
| A | Depth, flow, contamination sensors | Sound | Sound | Left | Sound | Sound | Sound |
| A | Population Counter | Sound | Sound | Fixed A4-1, A4 | Sound | Sound | Sound |
| A | Resource, Science Counters, Power Meter | Sound | Sound | Sound · Left P1 | Sound | Sound | Sound |
| A | Indicators, Speakers | Sound | Sound | Fixed O4-1, O4 · Left | Sound | Sound | Sound |
| A | Gates | Sound | Sound | Sound | Sound | Playtest | Sound |
| A | Detonator and dynamite | Sound · Fixed A1-1, A1 | Sound | Left X2 | Sound | Playtest L-X1, P-5 | Sound |
| A | HTTP Lever, HTTP Adapter, HTTP API | Sound · Fixed A3-1, A3 | Sound | Sound | Sound | Playtest L-A2 | Left |
| A | Fireworks | Sound | Sound | Sound | Sound | Sound | Sound |
| A | Floodgates, fill valves, throttling valves | Fixed A2-2, A2 | Sound | Fixed W1-1, W1 | Sound | Sound | Sound |
| A | Pumps, water movers, input pipes, regulators, stream gauge | Fixed A2-1, A2 | Sound | Left | Sound | Sound | Left |
| A | Water sources, seeps, badwater | Sound | Fixed R8-1, R8 | Left | Sound | Sound | Fixed R8-1 |
| A | Power networks, clutches, batteries | Sound | Sound | Left P1 | Playtest | Sound | Sound |
| A | Dev power generator | Fixed A2-3, A2 | Sound | Sound | Sound | Sound | Sound |
| A | Unstable cores, tunnels, dirt excavator, terrain blocks | Sound | Sound | Left X2 | Sound | Playtest L-X1 | Sound |
| A | Demolition with terrain | Sound | Sound | Left G9 · Fixed X4-1, X4 | Sound | Sound | Sound |
| A | Ziplines, tubeways, beehives | Sound | Sound | Sound · Left | Playtest F5 | Sound | Sound |
| A | Every panel, tool and key | Fixed A2-1 | Sound | Sound | Sound | Sound | Left |
| A | Dev tools | Left H2 · Fixed A2-4 | Sound | Sound | Sound | Sound | Sound |
| A | Surviving a game update | Fixed R8-1, R8 | Fixed R8-1 | Sound | Sound | Sound | Sound |
| A | Workshop recipes from another mod | Fixed H1-1, H1 | Fixed H1-1 | Sound | Sound | Sound | Sound |
| B | FT Wonder: Earth Recultivator | Sound | Sound | Left | Sound | Sound | Sound |
| B | IT Wonder: Earth Repopulator | Sound | Fixed B1 | Sound · Left | Fixed B1 · Sound | Sound | Sound · Playtest M11 |
| B | Both Wonders in one game; the single completion | Sound | Sound | Left D3 | Left D3 | Sound | Sound |
| B | Wonder launch sound | Sound | Sound | Fixed B5, V1 | Sound | Sound | Sound |
| B | Wonder timing at speed 7 and a boost of 30 | Sound | Sound | Sound | Sound | Sound | Playtest M5 |
| B | Wonder blockers, inventory, workers | Sound | Sound | Sound | Sound | Sound | Sound |
| B | FT bots | Sound | Sound · Fixed B1 | Sound | Sound | Sound | Sound |
| B | IT bots | Sound | Sound | Left | Sound | Sound | Sound |
| B | Bot parts traded between factions | Sound | Sound | Sound | Sound | Sound | Sound |
| B | FT births (lodges) and growing up | Sound | Sound | Sound | Sound | Sound | Sound |
| B | IT births | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Ziplines | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Tubeways | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Badwater rig (FT), badwater pressurizer and deep pumps (IT), each faction's pumps | Sound | Sound | Left | Sound | Sound | Sound |
| B | Metal industries | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Explosives factories | Sound | Sound | Left D3, X2 | Sound | Sound | Sound |
| B | FT monuments and decorations | Sound | Sound | Left | Sound | Sound | Sound |
| B | IT monuments and decorations | Sound | Sound | Left | Sound | Sound | Sound |
| B | Beehive | Sound | Sound | Left | Sound | Sound | Sound |
| B | Stockpiles, planters, yield removers late | Sound | Sound | Sound | Sound | Fixed B3, S1 | Sound |
| B | Science late | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Decontamination | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Power shafts and engines of both factions | Sound | Sound | Left D3, P1 | Sound | Sound | Sound |
| B | Paths and gates in each colony's faction | Sound | Sound | Sound | Sound | Fixed B4, F8 | Sound |
| B | The Ctrl+T faction switch buttons | Sound | Sound | Sound | Sound | Fixed B4, F8 | Sound |
| B | Handover across factions | Sound | Fixed B1 | Sound | Left D21 | Sound | Sound |
| B | Characters at scale | Sound | Sound | Sound | Sound | Sound | Sound |
| B | Character creation sites | Sound | Sound | Sound | Sound | Sound | Sound |
| B | The daily check in a mixed game | Fixed F6 | Sound | Sound | Sound | Sound | Sound |
| B | Faction catalog failure | Sound | Fixed B2, H1 | Sound | Sound | Sound | Left |
| B | Loading both factions | Sound | Sound | Sound | Sound | Playtest P4, B6 | Playtest P4 |
| B | A mixed save's life | Sound | Sound | Sound | Sound | Sound | Playtest M11 |
| C | Goods, boxes | Sound | Sound | Sound | Sound | Playtest T-12 | Sound |
| C | Goods, piles | Sound | Sound | Sound | Sound | Playtest T-7, T-12 | Sound |
| C | Goods, liquids | Sound | Sound | Sound | Sound | Playtest T-7 | Sound |
| C | Science | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Beavers | Fixed C2 | Sound | Fixed C2 | Sound | Fixed C7 | Sound |
| C | Gift or request | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Rounds 1–99 and repeating | Sound | Sound | Sound | Sound | Sound | Sound |
| C | A reserve | Sound | Sound | Left | Sound | Sound | Sound |
| C | Between factions | Sound | Sound | Sound | Sound | Left | Sound |
| C | Two colonies of the non-host faction | Fixed C1 | Sound | Sound | Fixed C1 | Sound | Playtest T-9 |
| C | Goods of other mods | Sound | Sound | Sound | Left | Sound | Left |
| C | 20 posts, 40 exchanges | Sound | Sound | Sound | Sound | Fixed C7 · Playtest T-12 | Playtest T-11 |
| C | Offer, accept, decline, withdraw | Sound | Sound | Sound | Sound | Sound | Sound |
| C | A round filling | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Goods already waiting on the giving half | Fixed C4 | Fixed C4 | Sound | Sound | Sound | Playtest T-8 |
| C | A round crossing | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Cancel asked, agreed, kept | Sound | Sound | Sound | Sound | Sound | Left |
| C | Paused | Fixed C1 | Sound | Sound | Fixed C1 | Sound | Fixed C8 |
| C | A half paused or flooded | Sound | Sound | Sound | Sound | Sound | Fixed C3 · Playtest T-4 |
| C | Colony change | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Faction change | Sound | Sound | Sound | Fixed C1 | Sound | Sound |
| C | Post removed mid-round | Sound | Sound | Left | Sound | Sound | Fixed C5 · Playtest T-10 |
| C | Save and load mid-round | Sound | Sound | Sound | Sound | Sound | Sound · Playtest T-11 |
| C | Rehost, reconnect after a desync | Sound | Sound | Sound | Sound | Sound | Playtest T-11 |
| C | A steward acting; a player away | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Ledger, totals, last terms | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Wishlist | Sound | Sound | Sound | Sound | Sound | Sound |
| C | Trading windows (panel 2 Hz, picker, Ctrl+T 1 Hz) with 40 exchanges | Sound | Sound | Sound | Left F8 | Fixed C8 · Playtest T-1 | Sound |
| C | Daily trade check and line | Sound | Sound | Sound | Sound | Sound | Fixed C6 · Playtest T-2 |
| D | Tick loop, interrupts | Sound | Sound | Sound | Sound | Fixed S2 · Left · Playtest P3 | Sound |
| D | Random draws and entity IDs | Fixed S10 · Sound | Sound | Sound | Sound | Fixed S10 | Sound |
| D | `TickableEntity.Tick` naming | Fixed | Sound | Sound | Sound | Fixed S10 | Sound |
| D | Walking animation | Sound | Sound | Sound | Sound | Fixed S9 | Sound |
| D | Heartbeat and colony digest | Sound | Sound | Sound | Left F6 | Sound | Sound |
| D | Daily colony check | Sound | Sound | Sound | Left F6 | Fixed S12 | Sound |
| D | Road networks conflict walk | Sound | Sound | Sound | Sound | Fixed S4 | Sound |
| D | Placement previews | Sound | Sound | Sound | Sound | Left · Playtest P2 | Sound |
| D | Road overlay | Sound | Sound | Sound | Sound | Left · Playtest P2 | Sound |
| D | Alerts and journal filter | Sound | Sound | Sound | Sound | Fixed | Sound |
| D | Colony marks | Sound | Sound | Sound | Sound | Left | Sound |
| D | Detailed logging | Fixed | Sound | Sound | Sound | Fixed S11 | Sound |
| D | Co-op save | Sound | Sound | Sound | Sound | Sound · Playtest P6 | Sound |
| D | Rehost and join | Sound | Sound | Sound | Left F9 | Sound · Playtest P6, B5 | Playtest P6 |
| D | Speed pacing | Sound | Sound | Sound | Sound | Sound · Playtest P3 | Sound |
| D | Long sessions | Sound | Sound | Sound | Sound | Fixed · Playtest | Sound |
| D | Reporting | Sound | Sound | Sound | Sound | Fixed | Sound |
| D | Single player with the mod installed | Sound | Sound | Sound | Sound | Fixed · Left · Playtest P1 | Sound |
| D | With LateGamePerformance | Left C1-6 · Plausible C1-7, C1-8, C1-9 | Plausible C1-8 | Sound | Sound | Left C1-4 · Playtest P5 | Sound |
| D | With Kyler's other mods | Left C2 | Sound | Sound | Sound | Sound | Sound |
| D | Late saves hosted, then split | Sound | Sound | Sound | Left F8 | Sound | Playtest P2 |
| E | Placing a building, a dragged line of many, copies of a building's settings | Sound | Refuted · Sound | Sound | Sound | Playtest | Sound |
| E | Deleting buildings | Sound | Sound | Sound | Sound | Sound | Fixed E-4 · Left G9, X4 |
| E | Planting marks over large areas | Sound | Fixed E-1 | Sound · Fixed E-7 | Sound | Playtest | Sound |
| E | Cutting and demolition marks over large areas | Sound | Sound | Sound | Sound | Playtest | Sound |
| E | Unlocks (paid, dev instant) and dev science | Sound | Sound | Sound | Sound | Sound | Fixed E-5 |
| E | Manual migration | Sound | Sound | Sound | Sound | Sound | Sound |
| E | District minimum and migration toggles; automatic migration | Sound | Sound | Sound | Sound | Sound | Sound |
| E | Good distribution with many districts | Sound | Fixed E-2 | Sound | Sound | Sound | Sound |
| E | Building owners | Sound | Sound | Sound | Sound | Sound | Sound |
| E | Hand-over (dead, absent, by host) of a 10,000-entity colony | Sound | Sound | Fixed E-8 | Left | Playtest | Sound |
| E | Absence count, warning, player away for days | Sound | Sound | Fixed E-3 | Sound | Sound | Sound |
| E | Stewards acting for an away colony | Sound | Sound | Sound | Sound | Sound | Sound |
| E | Seats and slots | Sound | Sound | Sound | Sound | Sound | Sound |
| E | Founding late, beside a big colony | Sound | Sound | Sound | Left | Sound | Sound |
| E | Splitting a late shared save | Sound | Sound | Fixed E-7 | Sound | Playtest | Sound |
| E | Beavers without a district | Sound | Sound | Fixed E-8 | Fixed E-8 | Sound | Fixed E-8 |
| E | Journal per colony | Sound | Sound | Sound | Sound | Sound | Sound |
| E | Road rule and placement previews | Sound | Sound | Left E-10, E-11 | Sound | Playtest S9 | Sound |
| E | Mode, session, host start gate, Home key | Sound | Sound | Sound | Sound | Sound | Sound |

---

## 4. Found sound (don't redo)

Each report's §3 has the evidence. The main ones:
- **Automation core (A):**
  - partitions, plans and transmitters are Lists and Queues in registration order, and nothing is hash-ordered;
  - sensors sample the thread-safe water copy in the tick;
  - every shared setter accepts a null automator and a stale index;
  - every automation replay skips a building that is gone.
- **Water (A, W2–W4):** readers use the thread-safe map, and writes are queued. The water-source fixes' premises still
  hold on 1.1.2.4. `WaterSourceFix`'s race premise is stale but harmless.
- **Power (A, P2):** graphs merge and split in add/remove order, totals are ints, and battery charge changes only in
  `BatteryService.Tick`.
- **Dynamite and cores (A, X1, X3):** everything happens in the tick, in insertion order, and kills go through
  `CharacterKilledEvent`. Tunnels, the dirt excavator and terrain blocks change terrain in the tick.
- **Fireworks, ziplines, tubeways, gates, weather and hazards (A, O1–O3).**
- **Faction content (B, F1):** all 313 buildings of both factions are covered, and every place the game assumes one
  faction is handled. Wonder blockers, inventory and workers; births, bots and creation sites (F2, F3).
- **The trading engine (C):**
  - the capacity mirror;
  - held goods never exceed stock;
  - reservations at load;
  - posts checked in turn;
  - the ledger and totals through saves in any order;
  - LateGamePerformance's haul cache doesn't touch posts.
- **Cost and memory (D):**
  - `SoundEmitter.Update` runs only while a callback sound plays;
  - creations never interrupt ticking in a game;
  - the road listener never hears previews;
  - `ParameterProvider` runs per injection;
  - D's files allocate nothing per entity per tick;
  - the mod's part of a late save or rehost is milliseconds;
  - every other collection is bounded.
- **The colony lifecycle (E):**
  - no hash-ordered iteration in simulation paths;
  - every local-player read in the simulation is display-only or gated;
  - automatic migration stays within a colony;
  - `DistrictOwner` at load;
  - the founding replay reads only the tick map;
  - a hand-over walks the entity list once;
  - connection numbers are never reused;
  - four colonies fit every per-slot structure.
- **Main:**
  - **R2:** the defaults are right. Logging is on (*Silence logging* off), detailed logging is off, and reporting
    consent is off until the player agrees. Of the four host settings, separate colonies and separate science are on,
    and founding in shared games and mixed factions are off.
  - **H1:** all 59 `Replay` methods check each entity they look up.
  - **B24:** the Join co-op box's list, its joining path and its fallbacks (plan §0.1).
  - **R5:** the mod has no test-only code, and the zip holds the build's mod folder plus the listed docs. Its entries
    were compared with beta24's at release.

---

## 5. Left, and why

- **Power across colonies (P1)** and **blasts across colonies (X2):** both are deterministic, and nothing breaks.
  Refusing either needs a new placement rule or a blast preview, which is new risk this close to release. This was
  Kyler's D3 default. Documented.
- **G9, floating dirt after a co-op demolition:** a fix means recomputing the game's terrain stack at replay, plus a
  colony decision when the stack holds another colony's building. Not small. Documented.
- **Local display choices:** a light's colour, a decal, a bell's sound, a stream gauge's marker and an HTTP Adapter's
  webhook settings are saved but read by nothing simulated. They stay each player's own until a rehost. Documented.
- **Automation's one-tick granularity:**
  - two opposite actions in one tick lose the pulse;
  - a spring lever can't be held on.

  That's how lockstep works. Documented.
- **The frame-ending interrupt on deletions (D-S2):** determinism needs Unity's destroy at the same point on every
  computer (beta12 D2). Now counted and reported. D's model gives its ceiling.
- **The `Time.time` detour in single player (D-S10e):** moving it to session start would turn a failure at the mod's
  start into a failure while hosting. P1 measures its cost.
- **The unoptimised Release Steam build (D-S8, decision D4):** no consistent gain outside the game. P1b answers it in
  the game.
- **A paused post's half in no colony can be removed by any colony:** minor. The suggested rule (judge the half by its
  open exchange's colonies) is for after the playtest. Documented.
- **The road rule's edges (E-10, E-11)** err on the safe side. E-13 (a child growing up with no district) is rare.
  E-12 needs a modified client.
- **B2 at load:** an unreadable catalog still stops a mixed game's load, before anyone plays. There is no known
  trigger, and a fallback there would pick one faction's bots and shafts for both.
- **A colony that lost every building of its faction** counts as untouched again, and its player may switch it. That
  takes the player's confirmed choice.
- **Smaller costs:**
  - the road overlay and placement previews: spotted, measured by P2;
  - `Manufactory` and `ColonyMarks` prefixes: microseconds, rare events;
  - per-candidate lookups in resource searches: P2's row decides.
- **B24-c, B24-d:** display only. Documented in the README.
- **`AllowOnHost` has no outer catch:** none of its checks can throw (A's H1). Wrapping it re-indents 130 lines shared by
  three reviewers.

---

## 6. What only the playtest can settle (R4)

RuntimeChecks checks the game's premises and the mod's IL, and runs the game's code where it can run without Unity.
It can't run a game. Automation classes, water, characters and the UI need Unity's objects throughout. So these stay
open until a person plays them:

| What | Why the checks can't settle it | Script |
|---|---|---|
| Automation behaviour tick by tick (the lever, the Detonator, memory and timers) | `BaseComponent`'s `bool` reads a native `GameObject` | L2–L4 |
| The water rules in a flowing game (synchronisers, pump, valve) | the water simulator is native and threaded | L6, L8 |
| A beaver without a district choosing its colony's center | nav mesh, walking | L9 |
| Detonator and dynamite chains at speed, and the frames they cost | frames, Unity destroy | L4, L9, P7 |
| Both Wonders, the planes and the completion in a mixed game; a Folktails bot as pilot | animators, planes, UI | M5–M8 |
| Births, bots and charging per faction | characters | M3, M4 |
| Trading Posts at 20 posts and 40 exchanges; stalls; a post blown up; save mid-round | hauling, inventories in a live game | T1–T12 |
| Every cost budget (B1–B6) | the game's own tick and Mono | P1–P7 |
| Memory and interrupts over 20 cycles | a long session | the long session |
| The Join co-op box's Enter (B24-b) | UI focus in the game | owed |
| The waiting room's game (D7, D7a, D8), Script F, beta12's B 8t to 8y | as before | owed |

---

## 7. For Kyler: findings in other mods

MultiColony can't fix these. They are in LateGamePerformance 0.4.28 and in Kyler's other mods; read, not edited. With
every player on the same versions and settings, none desyncs except as noted.

**LateGamePerformance:**
- **C1-4.** `SaveSnapshot` pins `ColonyStamp.Save` by IL hash, read against beta2. `ColonyStamp.Save` changed in beta7,
  so the pin no longer matches: LateGamePerformance warns each session and saves every building on the main thread.
  Nothing is lost. Re-read and re-pin.
- **C1-6.** Route maps, terrain maps and terrain search need the same LateGamePerformance on every player. A player
  without it, on another version, or with a feature that turned itself off, desyncs. MultiColony only warns on a mod
  list difference; its docs now say every player needs the same version and settings.
- **C1-7 (plausible, low).** The `DistrictMap` postfixes don't check which map they are on. The preview district map
  calls them as a player drags, so one computer can scan its caches alone. Check the instance, as
  `SharedStateChangingPrefix` already does.
- **C1-8 (plausible, very low).** After a 10 s worker timeout the main thread rebuilds maps while a stalled worker may
  still write one.
- **C1-9 (plausible, low).** A terrain search made on one computer only (the dev cursor tool) diverges its history.
- **C1-1, C1-2, C1-3, C1-5:** the catch-up, idle entities, background save and caches are compatible.

**Other mods:**
- **HungryPathing:** `HungryPathing.cfg` decides what beavers do, and nothing compares it between players. A different
  file desyncs.
- **OptimizedLocalHousing:** it disables itself from the local mod list when it sees a conflicting housing mod, so a
  player with one skips its `Tick`. Low.
- **MixedStorage:** a refused Apply leaves its panel on *Queued* until reload. Interface only.
- **TipsyTail:** it adds a need to both factions, so every player needs it.
- **PerformanceLog:** its `AutoWatch` already guards the random state, as D-new-1 does. PersistentWorkAreas only draws.

---

## 8. The release bar

| # | Bar | State at rc1 |
|---|---|---|
| 1 | No known desync reachable without dev mode | Met by review: A2-1, A2-2, A2-3 and D-new-2 fixed; none known |
| 2 | No throw out of a tick or replay from late-game content | Met by review: B1, E-1, E-2, H1-1, R8 fixed; B2 hardened; the H1 scans |
| 3 | No colony changes another's things, except what TWO-COLONIES lists | Met by review: W1-1, A4-1, O4-1, E-7, E-8 fixed; P1, X2, area effects and water documented |
| 4 | Mixed factions: every late-game building works for its own colony | Met by review (F1, B1); **Script M** owed |
| 5 | Trading: every good both ways, repeating, conserved, no silent stall | Met by review (C1–C8, the model); **Script T** owed |
| 6 | Performance budgets B1–B6 | Estimated and reduced (D, B3, B4, C7); **Script P** owed |
| 7 | A long session | **Owed** (Kyler, with a guest) |
| 8 | Played before 1.4.0 final: D7, D7a, D8, B24-b, F, L, M, T | **Owed** |
| 9 | The coverage matrix is complete | Met: no empty cell; Plausible cells only in another mod |

1.4.0 final waits for 4 to 8 (decision D1).

---

## 9. Release readiness (R1–R8)

- **R1:** the candidate keeps its beta labels. Dropping them at 1.4.0 final is decision D5.
- **R2:** found sound (§4).
- **R3:** every left finding and every §7 decision is in TWO-COLONIES *Known limits* or the README.
- **R4:** §6.
- **R5:** found sound (§4); the zip's entries were checked at release.
- **R6:** D-S1 was added to CI's tolerated timing list. The earlier three stay.
- **R7:** `changelog.txt`, STABILITY-CHANGELOG, ALPHA-TEST-SCRIPTS, TWO-COLONIES and README are updated. No website
  update (Kyler's instruction). Renaming ALPHA-TEST-SCRIPTS is for 1.4.0 final.
- **R8:** fixed (§2).

---

## 10. New test-script lines (ALPHA-TEST-SCRIPTS, *The late-game playtest*)

- **Script L** (the late game, two players, about 90 minutes): steps 1 to 14, from founding beside the host's fields
  (E-7) and unlocking together (E-5), through automation, the HTTP API and the Detonator, to water, power, weather, a
  blast, maximum speed, a steward's colony (E-3), and a rehost.
- **Script M** (Folktails and Iron Teeth, about 90 minutes): steps 1 to 12. They cover the census line (F6), births,
  bots, both Wonders with the launch sound, a Folktails bot flying the Earth Repopulator (B1), the Beehive, Ctrl+T at
  scale, and a rehost mid-flight.
- **Script T** (Trading Posts at scale, about 90 minutes): steps 1 to 12. They cover 40 exchanges, the daily trade
  line, each stall reason, beavers, every good both ways, goods already waiting, the mixed paused post, a post blown
  up, a rehost mid-round, and the cost rows. Setup also switches detailed logging on a minute apart (D-new-2).
- **Script P** (PerformanceLog recordings): P1 to P7.
- **The long session:** the daily `[Perf]` lines to read.

---

## 11. Corrections to the plan

- **P-1:** `ToActionString` was right; only the replay's two branches were swapped.
- **Reviewer E was added** while the review ran. The plan's four reviewers left the colony lifecycle, tools and batch
  events unassigned.
- **E-6 was refuted** (§2).
- **P-5's creations:** only loading makes an entity with a preset ID in a game, so creations never interrupt ticking.
  Only deletions do.
- **The daily check's cost** was about 1 ms on .NET 8, not the planning note's 50 to 80 ms.
- **The walking animation's path** is one tick's, not the whole route (D-S9).
