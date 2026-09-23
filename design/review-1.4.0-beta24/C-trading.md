# Reviewer C: Trading Posts at scale (1.4.0-rc1 review of beta24)

Leads T1 to T8 of [REVIEW-PLAN-1.4.0-beta24.md](../REVIEW-PLAN-1.4.0-beta24.md) §4 (Tier 1c), sweep 5 (every good × every
place a good is chosen or judged) and the trading code's share of sweep 7 (H1, the crash sweep). Base `1323b8c`
(beta24 + P-1). Nothing here ran the game: the evidence is the mod's source, the decompiled game (Timberborn 1.1.2.4,
`$SP/game/<Assembly>.decompiled.cs`), `Blueprints.zip`, the compiled mod in RuntimeChecks and a model in StabilityTests.

**In one paragraph.** The exchange engine is sound where it matters most: every good crosses by the game's own
`TransferStock` under a capacity rule the game mirrors onto both halves, so a round cannot throw, duplicate or lose goods
(proved below, and by a 72,000-step model). What was wrong is at the edges a late game reaches: a paused post ended
mixed-faction exchanges it should have kept (C1); a round of beavers could cross short while the other side paid in full
(C2); a stalled round never said why, in the panel or the Ctrl+T list (C3, C8); goods already on a half were never
counted towards a round, which in one case stalls it forever (C4); a post blown up mid-round said nothing (C5); and the
per-check costs the plan named (C7). All are fixed, with checks that fail on beta24's DLL or sources. A daily trade
self-check and log line (C6) give Script T what it needs. No wire change and no save change.

Checks: **StabilityTests 425/425** (418 + 7), **RuntimeChecks 371/371 on both builds** (362 + 9), both mod builds 0
warnings (StabilityTests keeps its 37 pre-existing nullable-annotation warnings, none added).

---

## 1. Findings

Line numbers: "beta24" means the base `1323b8c`; "now" means this branch. Game references are to
`$SP/game/<Assembly>.decompiled.cs`.

### C1: a paused Trading Post ended exchanges between two colonies of the non-host faction

- **Status:** Confirmed (by reading, end to end). **Kind:** gameplay (an exchange voided, its held goods sent home).
  **Hits:** mixed, trading (three or four colonies, two of them the faction the host did not pick).
- **Evidence.**
  - beta24 `Colonies/TradingPostExchange.cs:474-480`: the tick's check asks `FactionsAllow(OwnerOf(half), OwnerOf(partner), …)`
    *before* it asks whether the post trades (`:481`). `OwnerOf` is -1 for a half in no district (`:352`), which is what
    removing the road at one half does (the game's `DistrictBuilding.District` is null; `DistrictCrossingValidator.IsProperlyConnected`,
    `Timberborn.DistributionSystem:945-955`).
  - `Factions/ColonyFactionService.cs:87-88`: `FactionOfSlot(-1)` is `table.Of(-1) ?? BaseFaction`, and `FactionTable.Of`
    returns null for -1 (`Factions/FactionTable.cs:18`), so the half counts as the **host's** faction.
  - `Factions/FactionRules.cs:110-117`: beavers need the same faction both ways; goods need the receiver to store them.
  - So two Iron Teeth colonies in a Folktails-hosted game trading an Iron Teeth good (Grease, Coffee, …) or beavers: remove a
    road at either half and the next check ends the exchange with *"its colonies' factions no longer allow its terms"*
    (`Notice.VoidFaction`) instead of pausing it as TWO-COLONIES *Known limits* promises.
- **Fix.** The decision is now a pure function, `ExchangeTerms.Ending` (`Colonies/ExchangeTerms.cs:250-286` now): it
  judges the factions only while the post trades, so a paused post waits for its road; the engine computes the factions'
  answer only then (`!trading || FactionsAllow(…)`, `TradingPostExchange.cs:484-537` now). Deterministic (saved state).
  **Wire:** none. **Save:** none.
- **Checks.** StabilityTests `RcTradingChecks` "C1: a paused Trading Post's exchange waits for its road; the factions are
  judged only while the post trades" (exhaustive table over every open/offered/colony/owner/faction case, plus a source
  scan that `CheckTradingPosts` uses `Ending`, `RoundMayCross` and `!trading || FactionsAllow(`). RuntimeChecks
  `RcTradingRuntimeChecks` "C1: …" (the compiled `CheckTradingPosts` calls `ExchangeTerms.Ending`; the compiled `Ending` keeps
  a paused post going). Both fail on beta24 (no `Ending`; the faction call is unguarded).
- **Script T:** T-9 below.

### C2: a round of beavers could move fewer beavers than agreed, while the other side's goods crossed in full

- **Status:** Confirmed (by reading). **Kind:** gameplay / conservation of what was agreed (the ledger said the full count).
  **Hits:** trading (any game; worst in small districts and at volume).
- **Evidence.**
  - beta24 `TradingPostExchange.cs:517-524`: `BeaversToSpare`, which decides that a beaver side is *in* (`IsIn`, `:502`),
    counts adults that are not contaminated and of the right faction.
  - beta24 `:607-621`: `MoveBeavers` then also drops beavers that are **carrying something**
    (`GoodCarrier.IsCarrying`, `Timberborn.Carrying:825`) or **cannot walk to the other district**
    (`DistrictCenter.IsGloballyReachableFromCitizen`, `Timberborn.GameDistricts:1416-1423`) and takes
    `Min(amount, BeaversToSpare)` of the rest.
  - beta24 `:557-564`: `Cross` records `aTotal` in the totals and both ledgers whatever moved.
  - A district of 6 adults giving 5, one of them hauling at the crossing tick: the round is judged in (5 spare), 4 move, the
    partner's 100 goods cross, and the ledger says 5. At volume (50 of 60 adults) a busy district nearly always has haulers.
- **Fix.** One predicate: `Movable` (`TradingPostExchange.cs:604` now) lists the adults free to go (not carrying, not
  contaminated, of the receiver's faction, able to walk there); `BeaversToSpare` (`:588`) counts exactly those, and
  `MoveBeavers` (`:709`) moves the first `Total` of them in the game's migration order. `MoveSpecial` returns what moved
  and `Cross` records that (`:637-671`), with a warning if it ever differs. A round now waits until enough adults are free
  (the panel says so: `StatusYourBeavers` reworded). Allocation-free counting (plain loop, a kept list).
  **Wire:** none. **Save:** none.
- **Checks.** RuntimeChecks "C2: a round of beavers is judged in by exactly the adults it moves: free to go, carrying
  nothing, able to walk there" (IL: `BeaversToSpare` and `MoveBeavers` both go through `Movable`, which asks
  `IsCarrying`, `IsGloballyReachableFromCitizen`, `IsNotContaminated`, `BeaverMayJoin`; `Cross` keeps `MoveSpecial`'s
  result). Fails on beta24 (`Movable` is gone). The model in "T1/T3" moves exactly the counted adults and checks the
  totals equal what moved.
- **Script T:** T-6.

### C3: a round that stalled never said why (T5)

- **Status:** Confirmed. **Kind:** display (a stall that looks like progress). **Hits:** trading.
- **Evidence.** beta24 `TradingPostFragment.cs:1267-1293`: a goods side not in reads *"Your workers are bringing N more"*
  unless the half has no workers. Nothing said:
  - the half is **paused or flooded** (automation or the pause button blocks it: `PausableBuilding.Pause` →
    `BlockableObject.Block`, `Timberborn.Buildings:1419-1428`; every workplace gets the automation terminal,
    `Timberborn.AutomationBuildings:707-708, 2733-2793`);
  - there is **no room**: the post's room for a good (100) is shared by both halves, because the game mirrors every
    capacity reservation onto the linked half (`LinkedInventories`, `Timberborn.LinkedBuildingSystem:602-699`;
    `DistrictCrossingInventory.OnInventoryStockChanged` reserves the partner's room for every good on a half,
    `Timberborn.DistributionSystem:808-819`), so last round's goods not yet hauled away on the other half hold up this
    round. The *receiving* player, whose job it is to haul them (and who may have no storage for them), was told
    *"Your side is in. Waiting for X to bring 100 more"*;
  - the district has **none left** to bring.
  The Ctrl+Shift+J report (`ColonyDiagnostics.cs:452-466`) knew workers and room only.
- **Fix.** `ExchangeTerms.WhyGoodsWait` (pure: blocked, no workers, loads on the way, no room, no stock; `ExchangeTerms.cs:313`)
  fed by `ColonyExchangeService.WhyWaiting` (`TradingPostExchange.cs:403`, display only); the panel's line
  (`TradingPostFragment.StatusLine`, `:1275`) now says each reason for both sides, including *"haul away the 100 Logs on your
  half (they need storage room)"* for the receiver; the report uses the same words (`WhyNotIn`, `:417`;
  `ColonyDiagnostics.Stall`, a 4-line edit in reviewer D's file). Seven new English lines at the end of the CSV, one
  reworded (`StatusYourBeavers`). **Wire/save:** none.
- **Checks.** StabilityTests "C3: a round that waits says why: paused or flooded, no workers, no room, nothing left to bring"
  (the classifier's order; the panel handles every reason for both sides; every key has an English line; the report uses
  the same words). RuntimeChecks "C3: …" (IL: `WhyWaiting` reads `IsUnblocked`,
  `NumberOfAssignedWorkers`, `UnreservedCapacity`, `IncomingStock`; the panel calls it for both sides; the report calls
  `WhyNotIn`; the game's pause still blocks). Both fail on beta24.
- **Script T:** T-3, T-4, T-5.

### C4: goods already waiting on a giving half were never counted towards its round

- **Status:** Confirmed (by reading; reproduced in the model). **Kind:** gameplay (a stall that never resolves; otherwise
  wasted hauling). **Hits:** trading.
- **Evidence.**
  - beta24 `TradingPostExchange.cs:421-430`: only a load *arriving* is held for the round (`OnArrival`, from the game's stock
    event). Goods already on the half unreserved are never held: what an ended exchange left there, what the other colony
    sent in an earlier exchange, what arrived while the post was paused, a load's leftover.
  - beta24 `ColonyTrading.cs:256-271`: workers bring `Min(wanted, UnreservedCapacity)`; the half's unreserved room is
    100 less both halves' stock (`Inventory.UnreservedCapacity`, `Timberborn.InventorySystem:1061-1066`, with the mirror above).
  - So: colony A received 100 Water through the post and has no tank room for it (its workers cannot empty the half);
    A now offers 100 Water. The half's room for Water is full of A's own Water, workers can bring nothing, and nothing ever
    holds the Water already there: **the round never fills**. With storage room, the goods are carried home and back (200
    wasted trips). The game's own crossing uses what waits first (`DistrictCrossingWorkplaceBehavior.TryExportGood`,
    `Timberborn.DistributionSystem:1142-1159`).
- **Fix.** `HoldWaiting` (`TradingPostExchange.cs:546`), in the 8-tick check before the round is judged: what waits
  unreserved of the round's good is held as an arriving load is (`ExchangeTerms.ToHoldWaiting`, `ExchangeTerms.cs:244`),
  and not while the colony's reserve says no. Reserving stock never touches the room, so the capacity rule holds.
  Deterministic (tick, saved state). **Wire/save:** none.
- **Checks.** StabilityTests "C4: goods already waiting on a giving half count toward its round, so a half whose room they
  fill still fills its round" (20,000 random cases of `ToHoldWaiting`; the model shows beta24's stuck round with
  `holdWaiting: false` and none with the fix; source order). RuntimeChecks "C4: …" (IL: `HoldWaiting` runs before `IsIn`
  and reserves by `ToHoldWaiting`). Both fail on beta24.
- **Script T:** T-8.

### C5: a Trading Post removed mid-round said nothing (T6)

- **Status:** Confirmed. **Kind:** display / log (no crash, nothing lost). **Hits:** trading.
- **Evidence.** Removing either half removes both (`LinkedBuilding.DeleteEntity` → `UnlinkAndDelete`,
  `Timberborn.LinkedBuildingSystem:298-304, 330-334`). The game then leaves the halves' **whole** stock, held goods
  included, as recovered goods (`EntityComponent.InternalDelete` → `EntityDeletedEvent` → `DeconstructionNotifier`
  → `BuildingDeconstructedEvent` → `BuildingGoodsRecoveryService`, `Timberborn.EntitySystem:445-458`,
  `Timberborn.DeconstructionSystem:221-234`, `Timberborn.RecoveredGoodSystem:389-409`,
  `RecoverableGoodProvider.AddGoodsFromInventory` reads `Inventory.Stock`, `Timberborn.RecoverableGoodSystem:220-227`),
  which any colony's workers may pick up. beta24's `CrossingExchange` (`TradingPostExchange.cs:70`) did not hear it: no
  notice, no log of what was waiting. A blast from the other colony (allowed, D3) ends a round silently.
- **Fix.** `CrossingExchange` is an `IDeletableEntity` (`TradingPostExchange.cs:275`): the offering half logs the exchange
  and what waited on each half, and tells both colonies (`OnPostRemoved`, `:941`; `Notice.PostRemoved`). It changes no
  state; the game tells components before it posts the deletion that recovers the goods. **Wire/save:** none.
- **Checks.** RuntimeChecks "C5: a Trading Post removed with an exchange open says so, before the game leaves its halves'
  stock as recovered goods" (the interface, the call and the text; and the game's order: `DeleteEntity` before
  `EntityDeletedEvent`, recovery from `Inventory.Stock`). Fails on beta24.
- **Script T:** T-10.

### C6: conservation, checked once a day and logged (T3, plan §5.3)

- **Status:** Confirmed need (T3 asks for it). **Kind:** reporting. **Hits:** trading.
- **What.** `ColonyExchangeService.DailyCheck` (`TradingPostExchange.cs:962`), at each new day on every computer, over
  every Trading Post half once (a few µs; profiler spot *Trading post daily check*):
  - the goods each running exchange holds are on its half and reserved there;
  - no post holds more of a good on its two halves than its room (100).
  A broken one is logged as `[Colony] Trade check: …` (always: it would be a bug, and the game would throw when such a round
  crossed). With detailed logging on, one line: `[Colony] Day C-D trade: N posts, R exchanges running, O offered; held …;
  waiting to be hauled away …; crossed since yesterday 0>1 Log 300, …; stock Log 0:1234 1:567, …; checks ok`. It reads
  only simulation state and changes nothing; the same on every computer that agrees.
- **Pure parts proved in StabilityTests:** `TradeTotals` (the ledger's totals, moved into `ExchangeTerms.cs:48` from
  `ColonyTradeLedger`; the same save format and hash) and `TradeRecord` (moved from `TradingPostExchange.cs`, unchanged).
- **Checks.** RuntimeChecks "C6: once a day the trade checks …" (IL: `Tick` runs it; it reads `_reservedStock` and
  `AmountInStock`; the line needs `Settings.Debug`). StabilityTests "T3: the trade totals and the ledger survive a save in
  any order, and a damaged entry is skipped". Fails on beta24 (no `DailyCheck`).
- **Script T:** T-2, T-11.

### C7: the per-check costs at scale (T4)

- **Status:** Confirmed (by reading); small. **Kind:** performance. **Hits:** trading.
- **Evidence.** beta24 `TradingPostExchange.cs:453`: `GetEnabled<DistrictCrossing>().ToList()` every 8 ticks (the
  registry's `GetEnabled` is itself a LINQ `Where`, `Timberborn.EntitySystem:481-486`); `OwnerOf` computed 6–10 times per
  open exchange; `BeaversToSpare` a LINQ `Count` with a closure (`:517-524`); `CrossingExchange.Changed` built a string for
  every change it noted, i.e. for every load held (`:204`).
- **Fix.** A kept list; each post's two owners computed once and passed on (`IsIn`, `Cross`); `Movable` a plain loop into a
  kept list; the digest names given whole (`"exchange-hold"`, the same text, so the same digest). Estimated cost after:
  about 12 component reads per open exchange every 8 ticks: 40 exchanges ≈ 20 µs per tick. The *Trading post exchanges*
  and *Trading post workers* profiler spots already existed.
- **Checks.** StabilityTests "C7: the tick's check of every post allocates no list, and counts beavers without LINQ";
  RuntimeChecks "C7: the Trading Posts' check every 8 ticks copies no list" (also: `Changed` builds no string). Both fail
  on beta24.

### C8: the Ctrl+T window at scale: paused and stalled posts (T7)

- **Status:** Confirmed. **Kind:** display. **Hits:** trading.
- **Evidence.** beta24 `TradeOverviewPanel.cs:742-749`: a post with an open but paused exchange read *"Not trading yet /
  The other half needs a neighbouring colony's roads"*; a running round showed only `60/100`; and the list took only halves
  whose owner is the player (`:380-383`), so a post whose **own** half lost its road vanished from the player's list while
  their goods were held on it.
- **Fix.** A paused exchange reads *Exchange paused* (existing strings); a round held up appends the panel's own reason
  (`TradingPostFragment.StatusLine`); a half in no colony whose open exchange is this colony's stays listed
  (`TradeOverviewPanel.cs:374-384, 745` now). Display only.
- **Checks.** StabilityTests "C8: the Ctrl+T window says why a post's round is held up, shows a paused exchange as paused,
  and keeps its post listed" (source); RuntimeChecks "C8: the Ctrl+T window says why a post's round is held up, and shows
  a paused exchange as paused" (IL: `Describe` calls `StatusLine` and names `PausedTitle`). Both fail on beta24.
- **Script T:** T-1, T-4.

### Refuted, or found not reachable

- **C-R1: a crossing could throw in `TransferStock` for want of room on the other half.** Refuted. Every capacity
  reservation on a half is mirrored on its partner (`LinkedInventories.Reserve`/`MirrorCapacityReservation`), and the
  partner reserves room for every good on the half (`OnInventoryStockChanged`), so each half's unreserved room is
  `100 − both halves' stock − loads being carried in`: the same on both. `Held ≤ stock` (only the mod reserves held goods;
  `TakeInternal` refuses reserved stock, `Timberborn.InventorySystem:1264-1270, 1315-1325`), so `MoveGoods` finds all of
  it, and the partner's `GiveImported` has exactly the room it needs. The model checks this invariant after every step.
- **C-R2: a load's reservation clashing at load time** (the game's `GoodReserver` against `PostInitializeEntity`). Refuted:
  `GoodReserver.ResolveLoadedReservations` checks `HasUnreservedStock` before reserving (`Timberborn.InventorySystem:788-812`)
  and `PostInitializeEntity` reserves `Min(Held, unreserved)`; saved together, they fit.
- **C-R3: a round crossing twice in one check** (both halves `ProposedHere`). Needs an edited save: both halves are always
  written, one with 1 and one with 0.
- **C-R4: two posts spending the same beavers or science in one check.** Refuted: posts are checked one after another
  and each recounts after the previous one moved.
- **C-R5: LateGamePerformance's hauling cache and the posts (C1 of the plan).** No interaction: it caches haul candidates'
  weighted behaviours, invalidated by every inventory change (`HaulCache.cs:134-142`); a post's own carrying is the
  crossing's workplace behaviour, which it does not cache.

---

## 2. Coverage matrix rows (Trading Posts)

Columns as the plan's §1. "Model" is StabilityTests "T1/T3" (72,000 random steps over 12 seeds of one post: offers,
answers, loads, emptying, checks, pauses, blocks, no workers, no storage, saves, hand-overs, removal, recovered goods;
every step checks goods, science and beavers conserved, the room invariant, held ≤ reserved ≤ stock, and totals equal to
what crossed; then everything is unblocked and every open exchange must cross again or finish). The crash column is
sweep 7 (the table in §3).

| Feature | Desync | Crash | Cross-colony | Mixed | Cost | Save/rehost |
|---|---|---|---|---|---|---|
| Goods, boxes (Gear, bot parts, foods) | Sound: carried, held and crossed only in ticks and replayed actions, from saved state; each change in the digest | Sound: §3 (`Carry`, `OnArrival`, `Cross`; C-R1) | Sound: each half is served by its own district's workers; goods pass only in `Cross` | Sound: only to a faction that stores them (RuntimeChecks "T2") | Playtest: T-12. Heavy goods take many trips (Bot Chassis, weight 11: one a trip), §4 | Sound: held goods reserved again on load (`PostInitializeEntity`); model saves mid-round |
| Goods, piles (Log, Plank, Treated Plank, Dirt, Metal Block, Scrap Metal) | Sound: as boxes | Sound: as boxes | Sound: as boxes | Sound: both factions have piles ("T2") | Playtest: T-7, T-12 | Sound: as boxes |
| Goods, liquids (Water, Badwater, Extract; Biofuel, Catalyst, Antidote, Maple Syrup; Grease, Canola Oil, Coffee) | Sound: as boxes | Sound: as boxes | Sound: as boxes | Sound: both factions have tanks ("T2") | Playtest: T-7 | Sound: as boxes |
| Science | Sound: pool to pool in `Cross`, the pool read in the same tick | Sound: `MoveSpecial` checks the slots and separate science | Sound: only the two colonies' pools | Sound: science crosses between factions ("T2") | Sound: two reads a check | Sound: pools saved by `ColonyScienceService` |
| Beavers | Fixed: C2, RuntimeChecks "C2" (counted = moved; carrying and position are simulation state) | Sound: `Movable` null-safe; dead beavers leave the district first (existing RuntimeChecks) | Fixed: C2, "C2" (the ledger records what moved) | Sound: same faction only (`BeaverMayJoin`, "T2") | Fixed: C7, "C7" (a loop, no LINQ) | Sound: nothing held; the district is saved |
| Gift or request (one side 0) | Sound: a side of 0 is always in (`IsIn`) | Sound: nothing moves for it (`MoveGoods`/`MoveSpecial` return on 0) | Sound: as boxes | Sound: only the giving side is judged | Sound: as boxes | Sound: a total of 0 loads (`Load` validation) |
| Rounds 1–99 and repeating | Sound: `HasAnotherRound` after each round; the model | Sound: `Clear` after the last round | Sound: as boxes | Sound: judged at each check (C1) | Sound: nothing per round | Sound: `Rounds`, `Done`, `Repeat` saved |
| A reserve (keep at least) | Sound: the district's stock read in the tick (`StillToBringKeeping`, `ToHoldWaiting`) | Sound: arithmetic only | Left: two posts' reserves are judged apart (§4) | Sound: faction-independent | Sound: one counter read | Sound: `Keep` saved per side |
| Between factions (17 goods + science) | Sound: each colony's faction is saved state | Sound: `FactionTrade` reads tables, never throws | Sound: as boxes | Sound: the form, picker, prefill, wishlist, host and replay all ask `FactionTrade`/`FactionRules` ("T2" over all 60 goods × 4 pairs) | Left: `FactionTrade.Allows` allocates a closure, mixed only, B's file (§4) | Sound: as boxes |
| Two colonies of the non-host faction (3–4 colonies) | Fixed: C1, "C1" (both suites) | Sound: as between factions | Sound: as boxes | Fixed: C1, "C1" | Sound: as boxes | Playtest: T-9 |
| Goods of other mods | Sound: `IGoodService` goods, the same on every computer (build check) | Sound: an unknown good reads 0 and never throws (`StorableGoodRegistry.GetAmount`, `Timberborn.Goods:1019-1029`) | Sound: as boxes | Left: in a mixed game only if a faction's good collection lists them (§4) | Sound: as boxes | Left: a removed mod's good in a saved exchange waits until cancelled (§4) |
| 20 posts, 40 exchanges | Sound: posts checked in registry order, one after another (C-R4) | Sound: as each row | Sound: as boxes | Sound: as between factions | Fixed: C7, "C7"; Playtest: T-12 (profiler spots) | Playtest: T-11 |
| Offer, accept, decline, withdraw | Sound: serial and terms re-checked in `Accept` | Sound: every replay null-checks its half, or `WhyNotPropose` refuses a null one | Sound: the host judges `Entities(crossingID)`; the replay re-checks the owner | Sound: `FactionsAllow` at offer and accept | Sound: once per action | Sound: `State`, `ProposedHere`, `Serial` saved |
| A round filling (carry, arrive, hold) | Sound: worker decisions and arrivals are in the tick | Sound: `OnArrival` reserves `Min(ToHold, unreserved)` | Sound: as boxes | Sound: as boxes | Sound: C7 | Sound: `Held` saved, reserved again on load |
| Goods already waiting on the giving half | Fixed: C4, "C4" (held in the tick's check) | Fixed: C4, "C4" (reserves `Min(ToHoldWaiting, unreserved)`) | Sound: only the half's own colony's goods | Sound: faction-independent | Sound: one read a check | Playtest: T-8 |
| A round crossing | Sound: at the same tick everywhere (8-tick phase from the load) | Sound: C-R1 | Sound: `Crossing` lets only this caller through | Sound: as between factions | Sound: C7 | Sound: nothing half-done at a save (saves are at tick boundaries) |
| Cancel asked, agreed, kept | Sound: serial-checked actions | Sound: `TryGetOwnOpen` | Sound: each colony answers for itself | Sound: faction-independent | Sound: once per action | Left: a partner who never answers keeps it on hold (§4) |
| Paused (a half's road removed) | Fixed: C1, "C1" | Sound: `OwnerOf` -1 handled everywhere | Sound: its colony may end it alone (`Cancel`, `otherCanAnswer`) | Fixed: C1, "C1" | Sound: as boxes | Fixed: C8, "C8" (listed as paused) |
| A half paused or flooded (automation, the pause button, water) | Sound: blocking is simulation state | Sound: crossing into a blocked half gives goods as usual (`GiveInternal` does not check it) | Sound: automation wiring is the owner's (A's rules) | Sound: faction-independent | Sound: as boxes | Fixed: C3, "C3" (says so); Playtest: T-4 |
| Colony change (hand-over, other roads) | Sound: `Transfer` ends a post within one colony; the check ends one joining others (`EndColonies`) | Sound: `End` releases `Min(Held, reserved)` | Sound: held goods go home to their colony | Sound: hand-over faction rules are B's | Sound: once per hand-over | Sound: the model hands over mid-round |
| Faction change | Sound: a switch is refused while an exchange is open (`HasOpenExchange`) | Sound: as between factions | Sound: as boxes | Fixed: C1, "C1" (judged only while trading) | Sound: as boxes | Sound: factions saved (B) |
| Post removed mid-round (deletion, blast, tunnel, terrain) | Sound: deletions are played on every computer | Sound: `DeleteEntity` reads only (§3) | Left: what waited is recovered goods, anyone's (the documented rule) | Sound: faction-independent | Sound: once | Fixed: C5, "C5" (log, notice); Playtest: T-10 |
| Save and load mid-round | Sound: the 8-tick phase starts again on every computer with the load | Sound: `Load` validates; `PostInitializeEntity` reserves `Min` (C-R2) | Sound: as boxes | Sound: as boxes | Sound: nothing extra | Sound: the model saves at random steps; held equals reserved after each load; Playtest: T-11 |
| Rehost, reconnect after a desync | Sound: the host reloads the rehost save too (`RehostingService.RehostGame` → `LoadGame`) | Sound: as a load | Sound: as boxes | Sound: as boxes | Sound: as a load | Playtest: T-11 |
| A steward acting; a player away | Sound: actions carry the acting colony; the replay re-checks it | Sound: as offers | Sound: the steward acts as that colony only | Sound: as offers | Sound: as offers | Sound: grants saved (`ColonyStewards`) |
| Ledger, totals, last terms | Sound: sorted, order-free hash; each change in the digest | Sound: damaged entries skipped | Sound: each half's own | Sound: faction-independent | Sound: 20 rounds a half; totals bounded by pairs × goods | Sound: StabilityTests "T3" (round trip in any order, damaged entries) |
| Wishlist | Sound: the host stamps the slot; normalised in the replay | Sound: `Set` range-checks the slot | Sound: a colony sets only its own | Sound: `MayWish` | Sound: once per action | Sound: saved; bad entries skipped |
| Trading windows (panel 2 Hz, picker, Ctrl+T 1 Hz) with 40 exchanges | Sound: display only; buttons send actions | Sound: each refresh in try/catch | Sound: another colony's half only says whose it is | Left: `IsUntouched` walks every entity once a second (B's F8, §4) | Fixed: C8, "C8"; Playtest: T-1 | Sound: nothing saved |
| Daily trade check and line | Sound: reads only; never compared | Sound: try/catch, profiler spot | Sound: reads both colonies | Sound: faction-independent | Sound: once a day, every half once | Fixed: C6, RuntimeChecks "C6"; Playtest: T-2 |

---

## 3. Found sound (so nobody redoes it)

**The crash sweep of the trading code (H1, sweep 7).** Every trading entry point that runs in a tick, a replay or a load,
against the late-game states of H1 (deleted earlier in the tick, a component missing, a district or colony gone, no
`CharacterFaction`, an older save, a singleton missing):

| Entry point | Runs in | Why it cannot throw |
|---|---|---|
| `ColonyExchangeService.Tick` → `CheckTradingPosts` | tick, every 8 | Halves taken from the registry (a deleted one is unregistered, its partner too); `Of`/`Partner`/`OwnerOf` null-safe; `End` releases `Min(Held, reserved)`; C-R1 for `Cross`; `Tell` in try/catch |
| `HoldWaiting` (new) | tick | Reserves `Min(ToHoldWaiting, unreserved)` |
| `Movable`, `MoveBeavers` | tick | Null district → none; dead beavers leave the population before the death is posted (existing RuntimeChecks); no `CharacterFaction` → may cross (`MayBeaverCross(null, …)`) |
| `DailyCheck` (new) | tick, daily | Reads only; try/catch |
| `TradingPostCarryPatcher` (`TryExport`) | tick (worker decisions) | `CanExport` needs both halves linked and in districts; finder null-checked |
| `TradingPostPassPatcher` (`TransferStock`) → `OnArrival` | tick | Reserves `Min(ToHold, unreserved)`; nothing while `Crossing` |
| `TradingPostNoSettingsTradePatcher` | tick | Returns a bool |
| `TradingPostCapacityPatcher`/`Transpiler` | load, creation | Flag cleared in a finalizer; a missing field only warns |
| `CrossingExchange.Load` / `PostInitializeEntity` | load | Every value validated; `Min` reservation (C-R2) |
| `CrossingExchange.DeleteEntity` (new) | tick or replay | Reads only; `Tell` in try/catch; a singleton missing → nothing |
| `ColonyTradeLedger.Load`, `ColonyWishlist.Load` | load | Damaged entries skipped |
| The five exchange events and `WishlistChangedEvent` | replay | Null half → skip (Propose: `WhyNotPropose` refuses it); every check re-made from saved state |
| `ColonyLifecycle.Transfer` → `End` | tick or replay | As `End` |

**Also found sound:**
- Conservation: goods leave a half only by `TransferStock` inside `Cross` (the one caller let through) or by the owning
  colony's own emptying; held goods are reserved, so no other beaver can take them; a removed post's stock, held goods
  included, becomes recovered goods (C5's evidence). Science subtracts and adds the same amount after checking the pool.
- The ledger's record of goods equals what moved: `Held == Total` at `Cross` (`ToHold` caps `Held`, `IsDelivered` needs it).
- Determinism of the trading tick: registry order, saved state, the game's counters as updated in the tick; nothing reads
  `LocalSlot` except the notices (display, in try/catch).
- Reads from the UI (the panel, the window, the report) change no simulation cache: the resource counter's cache only
  maps a district to its own counter.
- Every good of both factions can be offered, carried to a half (the heaviest, 11, is under a beaver's 14), held there
  (the half allows every good of the game, 100 each) and stored by each faction that may receive it; the form, the picker,
  the prefill, the wishlist, the host and the replay all ask `FactionTrade`/`FactionRules` or the game's goods, and agree
  (RuntimeChecks "T2: …", over `Blueprints.zip`).
- Handover mid-round: held goods are released and carried home by their colony, whichever it now is.
- `ticks` (the 8-tick phase) is not saved, and need not be: every computer loads together (rehost reloads the host).

---

## 4. Left, and why

- **An exchange whose partner never answers a cancel** waits (goods held on the asker's half) until the partner answers, a
  steward does, or the colony is handed over. By design: ending takes both colonies. Document.
- **Two posts giving the same good from one district, each with a reserve,** judge their reserves apart: together they can
  dip below either. Rare; documented as "a reserve counts … the district of its Trading Post half".
- **A paused post's half in no colony can be removed by any colony** (`ColonyGameWorld.OwnerOf` is null for it: no district,
  and Trading Posts carry no colony stamp, `ColonyStamps.cs:214`). Plausible, minor; the rule is reviewer A's
  (`ColonyRules`/`ColonyGameWorld`). Suggest: judge a Trading Post half by its open exchange's colonies when it is in no
  district.
- **The Ctrl+T window's `IsUntouched`** walks every entity once a second in a mixed game: B's F8, left to B (a call site in
  my file; not changed, to keep the merge clean).
- **`FactionTrade.Allows` allocates a closure per call** (mixed games only; 2 per post every 8 ticks): negligible; B's file.
- **Heavy goods are slow:** a round of 100 Bot Chassis is 100 trips at 1 a trip (the game's crossing rules and lifting
  capacity; the mod only raised the room to 100). Document: staff the post (up to 10 workers).
- **A saved exchange of a good whose mod was removed** waits until the players cancel it (nothing throws).
- **`TradingPosts.isTradingPostTemplate`** (a static cache by `BuildingSpec`) is never cleared; it holds at most the
  templates the host judged, per load. Negligible.

---

## 5. Doc text and playtest steps

### Proposed doc text

**TWO-COLONIES *Trading posts*, "Each round"** (after step 3): *Goods of the exchange's item already waiting on your half
(sent back by an exchange that ended, or received earlier) count toward the round.*

**TWO-COLONIES *Trading posts*, step 4 (beavers):** replace "adult beavers move to the other colony's district (never the
last adult)" with: *adult beavers move to the other colony's district: only those free to go (carrying nothing, and able to
walk there), and never the last adult; a round of beavers waits until enough are free.*

**TWO-COLONIES *Trading posts*, the panel:** add: *When a round waits, the line under the bars says why: your half or
theirs is paused or flooded, has no workers, has no room because last round's goods still wait on the other half to be
hauled away (the post's room for a good is shared by its halves), or the colony has none left to bring. The Ctrl+T list
says the same for each post, and shows an exchange whose post lost a road as paused.*

**TWO-COLONIES *Known limits*:**
- *A Trading Post removed with an exchange open (by a player, a blast or the ground taken from under it) ends it; both
  players are told, and what waited on its halves is left as goods any colony's workers may pick up.*
- *A round of heavy goods takes many trips (a beaver carries one Bot Chassis at a time): give busy posts more workers.*
- *With detailed logging on, the log has one trade line a day: exchanges, goods held and waiting, what crossed since
  yesterday and each colony's stock of it. A line `[Colony] Trade check:` is a bug: please send the log.*
- (Replace) *A Trading Post trades only while a different colony's road reaches each half; with one removed, its exchange
  pauses until the road is back* — unchanged, and now true in a mixed game too.

**README, Trading Posts:** one line: *Stalled rounds say why, on the post and in Ctrl+T.*

### Script T (Trading Posts at scale), for Kyler

Setup: a separate-colonies game with separate science, two colonies (a third for T-9), dev mode used **only in single
player** to build 20 Trading Posts between them, tanks, piles and warehouses on both sides, and stock of every good.
Save, host, a guest joins. Detailed logging on for both. Send both `Player.log`s and both Ctrl+Shift+J reports.

- **T-1 (40 exchanges, T4/T7).** Open an exchange at every post (goods of each kind, science, beavers, gifts, requests, some
  repeating, some with a reserve). Open Ctrl+T: every post is listed with its terms and round; the window stays smooth.
  *Closes: 20 posts/40 exchanges, windows at scale.*
- **T-2 (conservation, T3).** Play 10 in-game days. Each day both logs have one `[Colony] Day … trade:` line, identical on
  both computers, with `checks ok`; no `Trade check:` warning. *Closes: daily check, ledger.*
- **T-3 (no room).** At a post where you give 100 Logs a round, remove the other colony's storage room for Logs. After the
  first round, your panel says *"No room on your half for more Logs: 100 … still wait on …'s half"*, theirs says *"… haul
  them away (they need storage room)"*. Give them room: the round goes on. *Closes: stall reasons.*
- **T-4 (paused/flooded).** Pause one half (its button, then with automation), and flood one: both panels and Ctrl+T say
  *paused or flooded*; unpause: it goes on.
- **T-5 (no stock).** Give away all of a good you trade: *"Your district has no more … to bring."*
- **T-6 (beavers, C2).** Trade 5 beavers from a district of 6 adults while they work: the round waits (*adults free to go*)
  and then moves exactly 5; the ledger and the log's *crossed* say 5. Check the log has no *Only N of M beavers* warning.
- **T-7 (every good both ways, T2).** One exchange per good both ways (liquids into tanks, piles into piles), science and
  beavers; 10 cycles repeating. Each crosses; the totals in the panel's *Traded with* match the log.
- **T-8 (goods already on the half, C4).** Receive 100 Water, have no tank room, then offer 100 Water back: the round fills
  at once from the Water waiting there and crosses.
- **T-9 (mixed, C1).** A three-colony mixed game hosted as Folktails, two Iron Teeth colonies trading Grease: remove the road
  at one half: the exchange shows *paused*, not ended; restore the road: it goes on.
- **T-10 (a post blown up mid-round, T6).** With goods held on both halves, blow up the post with dynamite (either colony):
  both players get *"An exchange ended: its Trading Post was removed…"*; the log lists what waited; the goods lie as recovered
  goods. No desync.
- **T-11 (save and rehost mid-round, T1).** With rounds half filled at several posts: save, reload, and rehost: the bars
  show the same held amounts; the rounds finish; no desync; the day's trade lines still match.
- **T-12 (cost, T4).** With all 40 exchanges running at speed 7, the Ctrl+Shift+J report's profiler spots *Trading post
  exchanges*, *Trading post workers* and *Trading post daily check* stay small (well under 1 ms a tick on average).

---

## Files changed

- Mine: `Colonies/ExchangeTerms.cs` (pure: `TradeRecord`, `TradeTotals` moved here; `Ending`, `RoundMayCross`,
  `ToHoldWaiting`, `WhyGoodsWait`), `Colonies/TradingPostExchange.cs`, `Colonies/ColonyTrading.cs` (the ledger uses
  `TradeTotals`), `Colonies/TradingPostFragment.cs`, `Colonies/TradeOverviewPanel.cs`.
- Shared: `Localizations/enUS_BeaverBuddie.csv` (8 lines at the end, 1 reworded).
- Reviewer D's: `Colonies/ColonyDiagnostics.cs`, `Stall` only (4 lines: the report's reason is the panel's).
- Checks: `StabilityTests/RcTradingChecks.cs` (7), `RuntimeChecks/RcTradingRuntimeChecks.cs` (9).
