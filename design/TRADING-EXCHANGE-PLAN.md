# Multi-Colony Trading Exchange — Implementation Plan

This is a handoff document. It was written from a design discussion, not from
an implementation. Read it fully before writing code. Anything marked
**VERIFY** is a belief about Timberborn internals that was *not* checked
against decompiled code — confirm it before building on it.

## 1. Goal

Turn the mod from "two players build one colony" into "two players each run
their own colony on one map and help each other". It is **non-competitive
co-op**. No prices, no currency, no winner.

Rules the owner of this project decided:

1. **No automatic map split.** There is no territory. Either player may
   build anywhere.
2. **The only restriction is on road networks.** Two players' road networks
   may not be joined, except through a **District Crossing**.
3. **The District Crossing between two players is a trading post.** Beavers
   of both players arrive at the join of their networks to deliver and fetch
   goods.
4. **Science and building unlocks are separate per player.**
5. **Water stays as it is.** Water on the map is one shared physical
   simulation. Stored water (tanks) is a good and is already per-district.
   Droughts and badwater tides stay global. Do not change any of this.
6. Trees, berries and other map resources near a border are contested. That
   is accepted.

## 2. Central design decision

**A player is the owner of one or more vanilla districts.** Do not build a
parallel "who owns this road" system.

Vanilla already defines a district as everything road-connected to a district
center, and the District Crossing as the only legal join between two
districts. So:

- Tag every district center with a persisted **owner slot**.
- Buildings, beavers and stock derive their owner from their district.
- A crossing with the **same owner** on both sides behaves exactly like
  vanilla. A crossing with **different owners** is a trading post. Same
  building, no new prefab; the mode is derived, never stored.
- A player may own several districts (their own sub-districts). The code
  already allows up to 4 players (`StartingLocationPlayer.MAX_PLAYERS`);
  pairwise crossings generalise to that with no extra work. Do not hard-code 2.

**Do not implement the trading post as one shared warehouse.** Timberborn
assumes every inventory belongs to exactly one district (reservations, stock
counts, hauling priority all key off the district — **VERIFY**). Keep the
crossing's existing two-sided inventory: side A is filled/emptied by A's
district, side B by B's. To the players it still looks like one common spot.

## 3. What exists in the repo today

Target: Timberborn 1.1 (`manifest.json` MinimumGameVersion 1.1.0.0, tested on
1.1.2.4), `netstandard2.1`, Harmony, blanket `harmony.PatchAll()` in
`Plugin.cs`. Game DLLs are referenced publicized from
`$(TimberbornManagedPath)Timberborn.*.dll` — decompile those (ILSpy/dnSpy) for
every **VERIFY** item.

| Area | State |
|---|---|
| Ownership of anything | **None.** `PlayerIndex` exists only on map-editor starting-location markers (`BeaverBuddies/Editor/StartingLocationPlayer.cs`). `BeaverBuddies/MultiStart/MultiStartPatches.cs` (~line 39–90) places one district center per marker ordered by `PlayerIndex`, then deletes the markers (~line 81). After that nothing is owned. |
| Player identity | `ReplayEvent.LocalPlayerID` (`BeaverBuddies/Events/ReplayEvent.cs:18`) is a **per-session GUID**. It is not stable across sessions and not attached to the world. |
| Placement validation | `BeaverBuddies/Events/ToolEvents.cs` (~65–83 `IsPlacementValid`, ~41–47 district-center special case). Not owner-aware. |
| District code | `BeaverBuddies/Fixes/DistrictBuildingsFix.cs` — finalizers that swallow exceptions from `DistrictMap.AddDistrictCenter`, `DistrictObstacleService`, `DistrictConnections`. Be aware these hide errors you may cause. |
| Distribution sync | `BeaverBuddies/Events/BatchEvents.cs`: `GoodDistributionSettingChangedEvent` (~262–316) with prefixes on `GoodDistributionSetting.SetExportThreshold/SetImportOption/SetDefault`; `MigrateBatchEvent` (~62–118); migration toggles. Import/export settings are **already synced**. |
| `DistrictCrossing*` | Untouched. Game classes known from `BeaverBuddies/Doc/DerivedClasses.txt`: `Timberborn.DistributionSystem.DistrictCrossing`, `DistrictCrossingInventory`, `DistrictCrossingAutoExporter`, `DistrictCrossingValidator`, `DistrictCrossingWorkplaceBehavior`; UI: `Timberborn.DistributionSystemUI.DistrictCrossingFragment`, `DistrictCrossingInventoryFragment`. Note the crossing **is a workplace** in 1.1. |
| Science | Untouched. `Timberborn.ScienceSystem.BuildingUnlockingService` has a `_unlockedBuildings` HashSet (`Doc/ClassesWithHashSets.txt:95`). Points are believed to live in `Timberborn.ScienceSystem.ScienceService` (**VERIFY**). |
| Per-player UI | None, apart from cursors/activity (`BeaverBuddies/Activity/`). |

### How to write a synced action (mandatory pattern)

Canonical example: `BeaverBuddies/Events/ToolEvents.cs:25–116`.

1. `[Serializable]` subclass of `ReplayEvent`, public plain fields, entities
   referenced by GUID string (`ReplayEvent.GetEntityID`). Override
   `Replay(IReplayContext)` and `ToActionString()`.
2. Harmony **prefix** on the game method the UI calls, returning
   `ReplayEvent.DoPrefix(() => new MyEvent{...})` or `DoEntityPrefix(...)`
   (`ReplayEvent.cs:112–151`), with `[HarmonyPriority(Priority.First)]` (see
   the comment on `DoPrefix`). Returning false suppresses the local action
   until the event comes back and replays on every client at a tick boundary
   (`ReplayService.cs` ~257–320).
3. Serialization is Newtonsoft with `TypeNameHandling.All`; the event type
   name is the wire contract.

**Determinism rules for this feature:**

- Every ownership / permission / science decision is made **inside
  `Replay()`**, from state that is identical on all clients (saved world
  state + fields carried in the event). Never from `LocalPlayerID`, local UI
  state, or anything only one client knows.
- The acting player's **slot** must be a field of the event, filled in by the
  sender in the prefix.
- Both clients hold *all* players' state (both science pools, both unlock
  sets). Only *display* is filtered locally. Local-only display is safe.
- Iterate owners/pools in a fixed order (by slot index), never over a
  `HashSet`/`Dictionary` whose order could differ.

## 4. Phases

Build in this order. Phase 1 blocks everything else. Each phase should leave
the game playable and desync-free (run the `StabilityTests` project and a
two-client session before moving on).

### Phase 1 — Stable player slots and district ownership

**Player slots**

- Introduce a stable integer slot per player (0..MAX_PLAYERS-1), matching the
  map's `PlayerIndex`.
- Persist in the save a mapping from a **stable player identifier** to slot.
  Choose the identifier deliberately: Steam ID where available, otherwise a
  GUID persisted in the player's local mod settings (not per-session). Host
  is slot 0 by default.
- On join/rejoin, the host resolves the joining player's identifier to a slot
  and tells everyone via a synced event. A player who is new to the save
  takes the lowest free slot; if none is free, define the behaviour (suggest:
  join as a helper sharing the host's slot) rather than leaving it undefined.
- Expose one service, e.g. `PlayerSlotService`, with `LocalSlot` (for UI and
  for filling event fields only) and the persisted mapping.

**District ownership**

- New persisted component on the district center entity, e.g.
  `DistrictOwner { int Slot }`, following the save/load style of
  `StartingLocationPlayer.cs:59–79`.
- In `MultiStartPatches`, before the starting-location marker is deleted,
  copy its `PlayerIndex` onto the district center it produced.
- A district center placed later takes the slot of the player who placed it.
  The slot must travel in the placement event (extend the district-center
  path in `ToolEvents.cs`).
- Saves made before this feature: every center gets slot 0. Single-player and
  single-start maps must behave exactly as before.
- Helper: `OwnerOf(entity)` → slot, resolved via the entity's district.
  Entities with no district (unconnected buildings, construction sites with
  no road) have **no owner**; callers must handle that.

**Acceptance:** save, quit, reload, swap which player hosts — every district
still reports the same owner slot on both clients.

### Phase 2 — Road networks cannot merge except through a crossing

**VERIFY first:** what vanilla does when two district centers become
road-connected without a crossing. The belief is that `DistrictMap` splits the
network by distance to each center rather than refusing. Read `DistrictMap`,
`DistrictConnections` and `DistrictCrossingValidator` before designing this.

Do **not** try to predict every merge at placement time. Networks can be
joined by: paths, stairs, platforms, bridges, buildings beavers walk through,
ziplines, tubeways, dynamite/terrain changes, and demolishing a crossing while
another link exists. Use two layers:

1. **Placement-time rejection (cheap cases).** Inside the replayed placement
   event, if the placed path-like object would directly connect road tiles
   belonging to districts of different owners, fail the placement and show the
   placing player a clear message ("Connect to another colony through a
   District Crossing"). Same-owner joins stay vanilla.
2. **Post-change safety net.** After district/navmesh recalculation, check
   whether any road-connected component contains centers of two different
   owners without a crossing between them. If so, deterministically resolve it
   (suggest: leave vanilla's split in place if VERIFY confirms it is stable
   and deterministic; otherwise remove the offending newest link) and raise a
   notification for both players. This must run on all clients identically —
   trigger it from the simulation, not from UI.

Ziplines and tubeways between different owners' stations: treat as a merge and
reject in the connect action.

**Acceptance:** none of the join methods above can produce a district whose
roads contain two owners' centers; nothing desyncs when a join is attempted.

### Phase 3 — The crossing as a trading post

- Derive `IsTradingPost` = the two sides' districts have different owners.
  Re-evaluate when districts change. Never persist it.
- Placement/validity: a crossing must be allowed where the two sides belong to
  different owners (check `DistrictCrossingValidator`). The crossing itself is
  **neutral**: whichever side reaches it first builds it; either player may
  demolish it; it has no owner slot.
- Staffing: the crossing is a workplace (`DistrictCrossingWorkplaceBehavior`).
  **VERIFY** which district supplies its workers and how each side's inventory
  is served, and make sure each side is served by its own district's beavers.
- Trading semantics stay vanilla: per-district export threshold and import
  option (off / auto / forced). These are already synced. Each player edits
  only their own district's settings by default.
- UI on `DistrictCrossingFragment` when `IsTradingPost`:
  - Show both players' names/colours (`StartingLocationPlayer.PLAYER_COLORS`).
  - **Partner needs**: goods where the partner district is below its import
    wish or simply low in stock.
  - **Gift**: "send N of good X" — one synced event; implement as a bounded
    one-off export so it cannot drain the giver indefinitely.
  - **Ledger**: totals of what has passed each way. If shown, it must be
    simulation state (saved, identical on both clients), updated where goods
    actually move.
- Keep beaver migration across a trading post (`MigrateBatchEvent` exists).
  It is the rescue mechanism for a dying colony.
- Check the crossing's build cost and science cost. If it needs planks or
  science, players cannot trade when they most need to. Make it available
  from the start and cheap when the multi-colony mode is on (mod setting or
  spec override).
- Multiple crossings between the same two players must work; throughput is
  limited by workers.

**Acceptance:** A sets an export threshold, B sets import; goods move A→B with
A's beavers only on A's side and B's only on B's side; gift works; both
clients show identical ledgers; no desync over a long session.

### Phase 4 — Separate science and unlocks

Make this a **game-creation option**, fixed for the life of the save (store
it in the save). Default on for multi-start games, off (vanilla behaviour) for
single-start. Do not support toggling mid-save.

**Science points**

- **VERIFY** `ScienceService`: where points are stored, added, spent, saved.
- Replace the single total with one pool per slot, saved with the game.
- **Earning:** points are produced by a building in a district → credit the
  pool of that district's owner. Patch at the point where the producing
  building is still known; if the add method only receives an int, patch the
  caller. A producer with no district/owner: credit slot 0 (document it).
- **Spending:** unlocking is a player action → synced event carrying the
  actor's slot; `Replay()` checks and deducts from that slot's pool. Re-check
  affordability in `Replay()`, not only in the UI.
- **Display:** top bar and unlock prompts show the local player's pool
  (display only).
- Loading a pre-feature save: whole existing total goes to slot 0.

**Unlocks**

- Per-slot unlocked sets instead of the single `_unlockedBuildings`. Replace
  the HashSet with a deterministic, saved structure per slot.
- Toolbar lock state reads the **local** slot's set (display).
- The placement event carries the actor's slot and `Replay()` re-checks that
  slot's set.
- **Accepted leak, by decision:** player A may place a building they unlocked
  next to B's road; it joins B's district and B's beavers run it. This is
  treated as legitimate co-op ("I'll place a gear workshop for you"). Do not
  try to close it.
- Audit other readers of the unlock service (wonders, faction unlocks, bots,
  tutorials, automation — see `AutomationEvents.cs` ~148) and decide per
  reader whether it means "this player" or "anyone".

**Gifting science:** add "gift N science" to the trading-post panel — one
synced event moving points between pools, validated in `Replay()`.

**Acceptance:** A's inventors raise only A's pool; A's unlock does not unlock
B's toolbar; both clients agree on both pools after save/reload; option off
reproduces vanilla behaviour exactly.

### Phase 5 — Per-player presentation (display only)

- Top bar defaults to the local player's district stock/population rather than
  global. Keep the vanilla global/district toggle.
- Tint or label district centers and crossing sides by owner colour.
- State in the README / in-game help: water, droughts, badwater and map
  resources are shared; stored water is per colony; a dam upstream affects the
  partner downstream. Recommend maps with a water source per start.

## 5. Open decisions for the implementer to raise, not guess

1. Stable player identifier: Steam ID vs. persisted local GUID (non-Steam
   builds exist? check `STEAM-INVITES.md`).
2. When a player is absent, may the present player edit the absent player's
   districts (thresholds, priorities)? Suggested default: yes — it is co-op and
   the sim keeps running for both colonies regardless.
3. More joiners than slots: refuse, or join as helper on an existing slot?
4. Safety-net resolution in Phase 2, pending the `DistrictMap` VERIFY.

## 6. Out of scope

- Any territory / tile ownership / build-permission system.
- Prices, currency, contracts, competitive scoring.
- Per-player water, weather, droughts or badwater.
- Closing the "place an unlocked building for your partner" leak.

## 7. Testing

- `StabilityTests/` after every phase.
- Two real clients: long session with trade running, save/reload, host swap,
  disconnect and rejoin mid-trade, attempt every network-join method from
  Phase 2.
- Regression: a single-start map with the science option off must be
  indistinguishable from the current 1.1.10 behaviour.
