> Reviewer C's report from the review of 1.4.0-beta18 to beta20 (see [the findings](../REVIEW-FINDINGS-1.4.0-beta18-20.md)).
> `$SP` was that session's scratchpad (the frozen beta20 source in `$SP/base`, rigs and scripts); it was not kept.
> Line numbers are beta20's. The fixes it proposes shipped in 1.4.0-beta21, except where the findings say otherwise.

# Reviewer C: what reaches everyone, compatibility, leaks, and the checks (1.4.0-beta18 to beta20)

Base: `$SP/base` (tag `v1.4.0-beta20`, `79d4757`), built DLLs `$SP/mods20/{Release,ReleaseSteam}/version-1.1/`, game
1.1.2.4, Harmony 2.4.1 (workshop 3284904751), Mod Settings (3283831040). Nobody played anything. Everything below was
proven by a rig or script (named), or by tracing the mod and the decompiled game.

**Work folder `$SP/revC`:**
- `rig/`: a .NET 8 console (`Rig.dll bind|resolve|order|savehash`) that
  - builds and validates every scene's container with Bindito's own classes;
  - resolves every patch with Harmony's own resolver;
  - prints PatchAll's class order;
  - computes LateGamePerformance's save hash.
- `timing/`: times the mod's own `SaveColonyReader.Read` on copies of real saves (`saves/`).
- `checks/`: three new RuntimeChecks files. `diffs/C-checks.diff` wires them in; they are verified against beta20
  (357/357) and against a build with four planted bugs (`mut/`: all four caught, all four missed by the existing checks).
- `fix/`: base plus every fix proposed here (`diffs/C-fixes.diff`, applied by `apply_fixes.py`). It compiles with
  0 warnings. RuntimeChecks, with the new checks, passes 357/357 and StabilityTests 391/391.
- Outputs: `bind-release.txt`, `resolve-*.txt`, `order-Release.txt`, `othermods/*.txt`, `runtimechecks-{new,mut,fix}.txt`,
  `stability-mut.txt`, `stability-fix.txt`.

A build note for the main reviewer: building from a copy under `$SP` fails at the content-copy step (MSB3030 on paths
over 260 characters). The DLL still compiles into `obj/Release/netstandard2.1/`, and I tested with that DLL dropped into
a copy of `mods20/Release/version-1.1`.

---

### C-E1: The new services' dependency graph (every scene, single-player and co-op)
- Status: **Refuted.** Reproduced with Bindito's own classes (`rig bind`, and `checks/BinditoChecks.cs`). The rig
  builds each scene's container the way `ContainerCreator.CreateContainer` does:
  - the Bootstrapper as parent, where a scene sees only exported bindings;
  - every `[Context]` configurator: 493 of the game and Mod Settings, and the mod's 3;
  - every binding built, then `BindingValidator` on every binding (`Bindito.Core.dll`, decompiled to
    `revC/dec/Bindito.Core.cs`).

  The Game scene is built twice: single-player (`EventIO` unset, so `ReplayConfigurator` returns at `Plugin.cs:50`)
  and co-op.
- Severity: (would be session-stopper) · Hits: everyone
- Evidence: counts before and after the mod's configurators.

  | Scene | Bindings | Problems the mod adds |
  |---|---|---|
  | Game, single-player | 2171 → 2213 (+42) | 0 |
  | Game, co-op | 2171 → 2234 (+63) | 0 |
  | MainMenu | 242 → 255 (+13) | 0 |
  | MapEditor | 982 → 985 (+3) | 0 |

  - The +42/+63/+13/+3 match `Plugin.cs:36-46`, `:54-76`, `:101-117`, `ColonyConfigurator.cs:62-105`,
    `MultiStartConfigurator.cs:26-28` and `EditorConfigurator.cs:19` one for one, so every new service was validated.
  - The 7 problems in the "game alone" baseline are all Mod Settings' mod-manager UI (its publicized access is refused
    by .NET 8). Nothing of the mod depends on them.
  - `ColonyFoundingService`'s new `EntityRegistry`/`EntityService`, `LobbyWorldMaker` (8 game services) in both Game
    builds, `NewGameFactionCapture` in MainMenu, and `CharacterFaction`, bound `AsTransient` for its `BeaverSpec`
    decorator: all resolve.
- **But the existing check would not have caught it (test gap, confirmed by mutation).** `RuntimeChecks/BindingChecks.cs`:
  - takes the union of each context's bindings (`:44-62`), so a service bound in every game that needs a co-op-only one
    passes;
  - counts every Bootstrapper binding as visible, not only `AsExported` ones (`:69`);
  - reads only the longest constructor, and no `[Inject]` methods (`:51-52`);
  - never sees a duplicate `Bind`, which throws at scene load.

  In `mut/` I added `ReplayService` (co-op only) to `LobbyWorldMaker`'s constructor. That breaks every single-player
  game and every waiting room's world-making. BindingChecks still passed all three contexts; the new check failed with
  "LobbyWorldMaker isn't instantiable due to missing dependency: ReplayService".
- Fix: none needed in the mod.
- Check to add: `revC/checks/BinditoChecks.cs` (reflection only, no compile-time reference), wired as in
  `diffs/C-checks.diff`. It adds 5 tests: the Bootstrapper, Game single-player, Game co-op, MainMenu and MapEditor. It
  can replace BindingChecks or sit beside it.
- Test script: new line in the 10-minute smoke test (plan §6.1). Solo new game, load an old save, open the map editor,
  host from in-game: `Player.log` has no `BinditoException`.

### C-E2: One `PatchAll` for everything
- Status: **Refuted as a present bug; confirmed as a design risk.**
  - Resolution: `rig resolve` and `checks/HarmonyResolutionChecks.cs` use Harmony 2.4.1's own `PatchClassProcessor`,
    `GetBulkMethods` (which runs every `TargetMethod(s)`) and `PatchTools.GetOriginalMethod`, for both builds.
  - Failure mode: a trace of the decompiled game.
- Severity: (on a game update) session-stopper · Hits: everyone
- Evidence:
  - **1.1.2.4 resolves cleanly.** Release: 273 classes, 318 targets (279 distinct), 0 failures. ReleaseSteam: 274
    classes, 319 targets, 0 failures.
  - 0 problems in 496 injected parameters: `__instance` 190, `__result` 66, `__state` 66, `__exception` 6,
    `__runOriginal` 2, named arguments 166. Harmony checks these only when it really patches.
  - No abstract targets. Every target in an overloaded group names its argument types. That includes beta20's
    `FactionMaxWellbeingPatcher` (`GetMaxWellbeing(WellbeingTracker)`, 2 overloads) and `FactionBotFactoryCreatePatcher`
    (`BotFactory.Create(Vector3, Quaternion, object)`, 2 overloads).
  - 295 of the 318 targets are named without argument types: any overload a game update adds makes one ambiguous. That
    includes 4 `AccessTools.Method(type, name)` calls in `TargetMethods`: `NewbornSpawner.SpawnAdult/SpawnChild`,
    `BeaverGeneratorTool.PlaceBeavers` and `BotGeneratorTool.PlaceBots`.
  - The 67 beta18–20 targets are listed in `resolve-Release.txt` (`RIG_LIST=1`).
  - **Order** (`order-Release.txt`): PatchAll patches root(28), MultiStart(6), Lobby(3), Fixes(34), **Factions(46)**,
    then Events(76), Editor(8), DesyncDetecter(15), Connect(3), Colonies(51) and 2 more.

    A Factions class that throws after an update stops every event-recording, desync and colony patch after it.
- **What happens:**
  1. `harmony.PatchAll()` (`Plugin.cs:176`) throws out of `StartMod`.
  2. `ModCodeStarter.StartMod` (Timberborn.ModManagerScene) has no catch, so `ModManagerSceneUI.LoadModsAndStartGame`
     throws before `StartGame()`.
  3. The game stays on the mod manager. Each Enter or Start reloads every mod's assemblies and fails again.

  The failure is loud, but it takes the whole game down over a patch that only mixed games use. The 46 Factions classes
  patch 51 UI, model and needs methods, which are the likeliest to be renamed. No method is patched both by a Factions
  class and by any other class (checked over the resolved target list), so patching them last changes no patch order.
- Fix (proportionate; `diffs/C-fixes.diff`, compiled and tested in `fix/`):
  - Core patches stay all-or-nothing.
  - The Factions classes are patched last, in one `try`. On failure, `MixedFactions.Unavailable` is set and logged, and
    the mod starts.
  - `MixedFactions.Decide` returns "off" while `Unavailable` is set, and `NewGameFactionCapture.MixedAvailable` returns
    false. So nothing turns mixed on, and every Factions patch already applied stays inert: the new gate check (T1)
    enforces that they are gated.
  - The decision and capture patches only decide, so leaving them applied is harmless.
  - No unpatching is needed.

  ```csharp
  // Plugin.StartMod: PatchAllIsolatingFactions(harmony); in place of harmony.PatchAll();
  private static void PatchAllIsolatingFactions(Harmony harmony)
  {
      const string factions = "BeaverBuddies.Factions";
      List<Type> classes = AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly).Where(t => t.HasHarmonyAttribute()).ToList();
      foreach (Type type in classes.Where(t => t.Namespace != factions)) harmony.CreateClassProcessor(type).Patch();
      try { foreach (Type type in classes.Where(t => t.Namespace == factions)) harmony.CreateClassProcessor(type).Patch(); }
      catch (Exception error) { Factions.MixedFactions.Unavailable = error.GetBaseException().Message; LogError("[Factions] ... " + error); }
  }
  ```

  Wire: none. Saves: none.

  One consequence to decide: a *mixed save* opened while `Unavailable` loads like a mixed save opened without the mod,
  and loses a faction (C3, documented). A notice in `ColonyFactionService.PostLoad` for "save says mixed, but
  unavailable" would be worth adding, and needs one localization string. It is not in the diff.
- Check to add: `revC/checks/HarmonyResolutionChecks.cs` (1 test, reflection on `0Harmony`).
  - It covers every class, including TargetMethods classes and MultiStart, which the existing checks skip. It checks
    resolution, targets with bodies, and injected parameters.
  - In `mut/` I removed `FactionBotFactoryCreatePatcher`'s argument types. The new check failed with "Ambiguous match
    for HarmonyMethod[... BotFactory, methodname=Create ...]". The existing "Factions: its Harmony patches still find
    their game methods" **passed** (`runtimechecks-mut.txt`).
- Test script: smoke test: `Player.log` has no "[Factions] A game method that mixed factions changes" line.

### C-E6: `SaveColonyReader.Read` on the main menu's thread
- Status: **Confirmed** (performance, minor). Timed by rig (`timing/`), running the mod's own compiled `Read` on
  copies of real saves in `.NET 8` on this computer.
- Severity: performance · Hits: everyone who hosts a save from the main menu
- Evidence (`LobbyHostPanel.cs:131`, called from `ServerHostingUtils.LoadAndHost` on the UI click):

  | Save | Size | Kind | First `Read` (with JIT) | Median | Max |
  |---|---|---|---|---|---|
  | "Romans missing leg (9) TESTING" | 1.1 MB on disk, `world.json` 8.5 MB | shared, not separate | 110 ms | 35 ms | 55 ms |
  | beta15 autosave "Day 46-10" | `world.json` 9.4 MB | separate colonies | – | 56 ms | 92 ms |

  - Inflating `world.json` alone takes 11–12 ms.
  - Unity's Mono is slower than .NET 8 for Newtonsoft parsing. I estimate 2–4x, which I could not measure: a one-off
    hitch of roughly 0.1–0.3 s on the click.
- **The early stop never happens in practice.** The mod's singletons are saved after the game's: `BeaverBuddies.ColonyMode`
  and `ColonySlots` are at index 42–43 of 50 in the real separate-colonies save, at byte 5.72 M of the 5.74 M-byte
  Singletons object. `ColonyFactions` is bound after `ColonyLifecycle` (index 48), so in a mixed save it would be about
  index 49.

  So every save, mixed or not, is read through nearly all of `Singletons`, which is 55–60% of `world.json`. The class
  comment ("stops once it has the few singletons it wants … so a large world costs little", `SaveColonyReader.cs:29-33`)
  is wrong.
- Fix: the smallest is to correct the comment. Stopping earlier isn't possible with the keys at the end.
  - If the hitch matters after a playtest, run the read on a worker. Make `OpenForSave` do
    `Task.Run(() => SaveColonyReader.Read(bytes))` and open the room on the main thread when it completes (the bytes are
    already in memory). This changes no wire or save.
  - I don't recommend it before a playtest shows a visible hitch.
- Check to add: none (a timing check would be flaky).
- **For the main reviewer (T2 / X4):**
  - `TWO-COLONIES.md:75-76` ("The rows show no colony … which the menu does not read") is wrong since beta20.
  - `STABILITY-CHANGELOG.md:15` ("a room that is not mixed sends beta19's frames") is wrong for every save room the menu
    can read:
    - `LobbySummary.ForSave(..., colonies.BaseFaction, colonies.SeparateColonies, ...)` (`LobbySession.cs:122-127`,
      `LobbyFrames.cs:69-71`) now sends `"faction":"<id>"` and the real `"separate"`, where beta19 sent `""` and `false`;
    - a separate-colonies save's rows now carry `"colony"` through `room.Seating` (`LobbySession.cs:130-135`,
      `LobbyRoom.cs:275-297`).
  - The StabilityTests check "a waiting room that is not mixed sends what 1.4.0-beta19 sent" (`LobbyChecks.cs:386`)
    covers only the new-game summary.

### C-E7: Small changes that reach everyone
- Status: each item was confirmed harmless by trace, except two cosmetic ones with fixes.
- Severity: (none) / performance (trivial) · Hits: everyone
- **The `CharacterFaction` decorator** (`ColonyConfigurator.cs:31`, on `BeaverSpec`; both `BeaverAdult` and
  `BeaverChild` blueprints have it):
  - `Save` returns before touching the saver when `!IsOn` (`CharacterFaction.cs:39-43`).
  - The game's `EntitySaver.GetComponent` is what creates a component entry (Timberborn.WorldPersistence
    `EntitySaver.GetComponent` → `GetOrAddComponent`), and `SaveEntity` only calls each `IPersistentEntity.Save`.
  - So a non-mixed save gets no `BeaverBuddies.CharacterFaction` entry. `ColonyFactionService.Save` (`:77-84`) and
    `FactionDecalDefaultPatcher` are gated too.

  A save without mixed factions is structurally identical to beta19's. `Load` and `FactionId` are never evaluated when
  off. **Harmless.**
- **`FoundColonyEvent.faction` and `InitializeClientEvent.hostFactions`** are always written, as `"faction":null` and
  `"hostFactions":null` (`ColonyFoundingService.cs:606`, `ConnectionEvents.cs:43`).
  - Both sides are the same build (MVID handshake) and both hash the same text, so this is **harmless**. It only
    contradicts "nothing changes".
  - Fix (optional, in the diff): `[Newtonsoft.Json.JsonProperty(NullValueHandling = NullValueHandling.Ignore)]` on
    both, like `hostSpeed` (`ReplayService.cs:89`). Non-mixed events then match beta19's JSON exactly. A missing field
    deserializes to null as before, and the RuntimeChecks round trip still passes.
- **`FoundingToolActive`** (`ColonyFoundingService.cs:96`): `Values.Any(lambda)` allocates a delegate and a boxed
  enumerator per call.
  - It is called per preview block per frame by `ColonyPlacementValidator.IsValid` (co-op, `:31-32`) and per frame by
    `ColonyRoadOverlay.PlacingSomething` (`:133-135`), in shared games too.
  - Trivial GC pressure. Fix (in the diff): a `foreach` over `foundingTools.Values` against `_toolService.ActiveTool`,
    with no allocation.
- **MultiStart** (`MultiStartPatches.cs:86-102`): when not mixed, `startFaction` is null, so the spec isn't swapped.
  The `finally` writes back the value it read, and `StartingBuildingTemplateSpec` is a plain auto-property
  (`StartingBuildingSpawner`, Timberborn.GameStartup). `SimFactionOf` returns null when off. **Harmless.**
- **`JudgeFactions`** (`ColonyRulesService.cs:178, 216-270`): three type tests per judged event. A `FoundColonyEvent`
  gets `faction = null` when off. A `ColonyFactionSwitchEvent` in a non-mixed game goes to
  `HostJudgeSwitch` → `JudgeSwitch(false, ...)` and is refused. **Harmless.**
- **Hidden UI and empty tooltips:** the row icon's `Register(icon, () => factionName ?? "")` (`LobbyPage.cs:339`) never
  shows. The game's `Tooltip.UpdateSingleton` shows updatable content only when `TooltipContent.HasContent()`, which is
  false for "" (Timberborn.TooltipSystem). Hidden colony-card and Trading Post elements are `display:none`.
  **Harmless.**
- Check to add: none beyond T1.

### C-C1: A third faction installed (a mod adding a faction)
- Status: **Plausible.** Traced; not testable, because no faction mod is installed here.
- Severity: gameplay (mixed only) · Hits: mixed factions with a faction mod
- Evidence:
  - The code is written for N factions: `AllFactions` (`MixedFactions.cs:76`), `OtherFactionIds` (every non-base
    faction, `:152-156`), `FactionSets`/`FactionCatalog`, the picker (arrows over the list, `LobbyFactionPicker.cs:114`)
    and `MaxFactions` 8.
  - `MixedAvailable` needs *every* faction unlocked (`NewGameFactionCapture.cs:50`). A mod faction without
    `UnlockableFactionSpec` is never locked (`FactionUnlockingService.IsLocked`).
  - RuntimeChecks' "exactly two" tests only the game's own `Blueprints.zip`, so it neither catches nor forbids a mod
    faction.
- What happens with a third faction and mixed on:
  1. **Every** faction's collections load in every mixed game, even one nobody plays. A faction mod that copies
     blueprints under the same template names makes `TemplateNameMapper` throw, and no mixed game loads.
  2. A faction mod that reuses Folktails' collections makes those templates "listed by two factions". So
     `FactionCatalog.FactionOfTemplate` returns null (common) for them: Iron Teeth colonies can then build, plant and
     store Folktails things. The D18 rules and the toolbar filter quietly stop applying to the base factions.
  3. The founding checks use the base district center's footprint for every faction (M10). A third faction with a
     different footprint is checked on the wrong cells.
  4. The mod ships Trading Posts for Folktails and Iron Teeth only (`TemplateCollections/`). A third faction's colony
     has none in its toolbar. This is not beta20-specific.
- Fix: this is the maintainer's call, as it touches D-decisions.
  - Smallest: log a warning in `Decide` when `AllFactions.Length > 2` ("mixed factions has only been checked with the
    game's two factions").
  - Safe: make mixed factions unavailable when `AllFactions.Length != 2`, with the same note as a locked faction.
  - Neither changes the wire or saves.
- Check to add: none possible without a faction mod. The plan's "exactly two" pin is fine as a game-data fact.
- Test script: new line F-x. With any faction mod installed and mixed factions on, start a waiting-room new game, then
  check the log for template name collisions and that each colony's toolbar shows only its faction's buildings.

### C-C2: The maintainer's own mods
- Status: **Refuted** (no conflict). This covers Harmony targets and the two named interactions. I checked by rig
  (`rig resolve` on each installed DLL, `othermods/*.txt`), by grep of each repo's `source/`, and by trace.
- Severity: – · Hits: players of both
- Evidence:
  - **No shared patch targets.** HungryPathing, LateGamePerformance, PerformanceLog, PersistentWorkAreas,
    OptimizedLocalHousing and TipsyTail have no attribute patches; the first three patch by hand. MixedStorage has 20,
    on `SingleGoodAllower`, `StockpileVisualizers`, `StockpileInventoryFragment`, `InputService` and the stockpile
    priority behaviors.
  - None of the 7 names any of beta18–20's ~55 target types in a patch. The only overlaps by type name are:
    - LGP's `SaveSnapshot` allowed list (`DecalSupplier`, not a patch);
    - HungryPathing reading `NeedManager`;
    - PersistentWorkAreas subscribing to `LoadingScreen.LoadingScreenEnabled`, while the lobby only skips `Disable`,
      so the event is unaffected.

    MixedStorage's `ObtainPatch` "fails" in the rig only because its `TypeByName` target's assembly wasn't loaded there.
  - **HungryPathing:** `NeedManager.GetNeedSpec` does **not** throw for a need the character lacks. It returns null
    (`Needs.GetNeedSpec` → `TryGetValue`, Timberborn.NeedSystem).
    - Every `GetNeedSpec` call is behind `HasNeed`: `_needOrder` is built only from needs with
      `HasNeed && NeedIsEnabled` (`HungryPathingRootBehavior.cs:549-568`; the calls at `:175` and `:361` iterate
      `_needOrder`; `:640-645` checks `HasNeed`).
    - The configured needs are `{"Hunger","Thirst"}` (`Config.cs:16`), which both factions have.
    - Cosmetic: its once-per-process "Needs in this game" log line describes whichever faction's beaver it saw first.
  - **MixedStorage:** it lists and allocates from `Inventory.InputGoods` (`StorageState.cs:88,101,129`). That is the
    set `FactionStockpileGoodsPatcher` already filtered when `StockpileInventoryInitializer.Initialize` built the
    inventory (`FactionBuildingRules.cs:25-54`), for created and loaded stockpiles alike. So a stockpile's MixedStorage
    UI shows only its faction's goods.
  - **LGP BackgroundSave vs the waiting room's world save:** LGP wraps `QueueSaveSkippingNameValidation`'s callback and
    calls it only after the file is committed. `GameSaveRepository` lookups wait for running jobs
    (`BackgroundSave.cs:408-462, 542-590`). So `LobbyWorldMaker`'s "written, then read a frame later"
    (`LobbyWorldMaker.cs:92, 108-116`) still reads a complete file.
  - **LGP SaveSnapshot:** it has no character components on its allowed list, so the new `CharacterFaction` changes
    nothing there.
- Side note for the LGP repo (not a beta18–20 regression): `SaveSnapshot.Allowed` pins
  `BeaverBuddies.Colonies.ColonyStamp` by an FNV hash of `Save`'s raw IL bytes (`ae23955dc80c940f`, read at beta2).
  - IL bytes embed metadata tokens, so every MultiColony rebuild hashes differently: beta20 Release `db077342cbaf071b`,
    ReleaseSteam `0f633081a3c941dc`, the installed beta15 `ae77e00996060228` (`rig savehash`).
  - So LGP keeps every building with a ColonyStamp on the main thread, and logs a warning, in every separate-colonies
    game.
  - Fix in LGP: hash the IL with tokens resolved to member names.
- Fix: none in MultiColony.

### C-C3: Saves
- Status: **Refuted** (safe), by trace.
- Severity: – · Hits: everyone
- Evidence:
  - A beta17–19 save has no `BeaverBuddies.ColonyFactions`, so `Decide` goes to "the save (not mixed)"
    (`MixedFactions.cs:112-118`). `CharacterFaction.Load`, `ColonyFactionService.Load` and `.Save` all return early.
  - A beta20 non-mixed save is structurally beta19's (C-E7), so it also opens in beta19.
  - A mixed save opened without the mod, or with C-E2's `Unavailable`, loses a faction's content. That is documented;
    see C-E2's notice suggestion.

### C-T1: Checks that check less than their names say
- Status: **Confirmed by mutation.** In `mut/` I planted four bugs in a copy of beta20 and built it:
  1. `LobbyWorldMaker` needs `ReplayService`;
  2. `FactionStockpilePatcher.Prefix` calls `SimFactionOf` before the gate;
  3. `[HarmonyPatch(nameof(ShowAdultEmpty)), HarmonyPatch(nameof(ShowChildEmpty)), HarmonyPrefix]` on one line;
  4. `BotFactory.Create` without argument types.

  All existing checks passed: StabilityTests 391/391 (`stability-mut.txt`), and RuntimeChecks' BindingChecks and both
  "Harmony patches still find their game methods". The new checks failed on exactly those four (`runtimechecks-mut.txt`).
- Severity: test gap · Hits: everyone
- Evidence:
  - The **gate scan** (`StabilityTests/FactionChecks.cs:222-246`) only asks that the text `MixedFactions.IsOn` appears
    somewhere in the class's source. Bug 2 keeps the text, a comment would pass, and a gate in the finalizer would pass
    an ungated prefix.
  - The **stacking scan** (`:248-279`) counts consecutive attribute lines, so two names on one line pass (bug 3).
  - The **name checks** (`FactionRuntimeChecks.cs:246-266`, `LobbyRuntimeChecks.cs:59-69`):
    - use `GetMember`, which includes inherited members, where Harmony needs *declared* ones;
    - ignore overloads and injected parameters;
    - skip TargetMethods classes and classes without a class-level type;
    - never look at MultiStart.
  - **BindingChecks**: see C-E1.
  - **"Not mixed sends beta19"**: covers only the new-game summary (C-E6's note).
- Fix, and checks to add: `revC/checks/` (8 tests, `diffs/C-checks.diff`):
  - `BinditoChecks.cs` (C-E1);
  - `HarmonyResolutionChecks.cs` (C-E2);
  - `PatchGateChecks.cs`, on the compiled IL:
    - **The gate.** In every Prefix, Postfix and Finalizer of a `BeaverBuddies.Factions` patch class (the decision and
      capture excepted), the first call, `newobj` or store must be `MixedFactions.get_IsOn`, followed at once by a
      branch. The not-mixed side must reach `ret` with no call or store, and a bool prefix must return true there.
    - It allows a lambda's display-class allocation, a cached static lambda, `static bool Prefix() => !IsOn`, and a
      patch method that only calls a gated helper of its own class (`FactionEmptySlotPatcher.Show`).
    - **Stacking.** Read from the compiled `CustomAttributeData`: two method names or two types on one member, or a
      method-level name under a class-level one.

  They can replace the two StabilityTests scans, or run beside them as the authoritative versions.
- Found while writing it: `FactionPlanterPatcher.Postfix`, `FactionDecalDefaultPatcher.Prefix` and
  `FactionPlanterNamePatcher.Prefix` allocate their lambda's closure *before* the gate, in every game. The C# compiler
  hoists the display class to the method start. The allocation is trivial (planter and decal entity creation, a UI
  label), so no fix is needed.
- Not covered by anything, and left to A's rigs: `ClientConnectionService`, the panels, `LobbySession` and
  `LobbyWorldMaker` as behavior. BinditoChecks now at least validates their construction.

### C-S1: Statics that outlive a game
- Status: each item traced. Three are **confirmed** leaks or stale values with small fixes; the rest are **refuted** as
  harmless.
- Severity: gameplay/display (one), memory (two), diagnostics (one) · Hits: mixed factions (and memory for everyone
  after a mixed game)

| Static | Can it do harm? | Fix (in `diffs/C-fixes.diff`) |
|---|---|---|
| `ColonySession.hostFactions` (`ColonySession.cs:42-66`) | **Yes.** Written only by the host's latch (`FactionCatalog.Load`, mixed games; its catch keeps the old value, `:67-78`) and by `AdoptHostFactions`, which ignores null. A guest of a **mixed save room** is sent `hostFactions = null`: the start message is built in the menu, where `IsOn` is false (`ConnectionEvents.cs:87`, J5). That guest keeps whatever an earlier session in this process left, and `FactionChoice.Available()` (`FactionChoice.cs:64`) then offers the wrong cards. It can hide a faction the host allows, or offer one the host refuses. The host judges with its own fresh latch, so it is not a desync. | `ColonySession.ForgetHostFactions()`, called from `MixedFactions.Reset()`, which runs at every Game scene's configure: before the host's latch in `FactionCatalog.Load` and before the guest's `InitializeClientEvent` replay. Unknown then means "every faction", the documented fallback. |
| `FactionDisplay.factionIcon` (`FactionDisplayPatches.cs:35`; `Reset()` at `:50` has no caller) | **Leak.** It keeps an old scene's `Image`, and through `parent` its whole UI tree, until the next mixed game. Writing a sprite to the dead image is harmless. | Call `FactionDisplay.Reset()` from `MixedFactions.Reset()`. |
| `FactionModels.shaftModels` (`FactionModelPatches.cs:68-75`, cleared only in the mixed-only postfix `:169-173`) | **Leak.** It keeps the old game's `ModularShaftModelService`s (factories and prefab instances) through every non-mixed game after it. Never used while off (`:107`). | `FactionModels.Reset()`, which clears the dictionary, called from `MixedFactions.Reset()`. |
| `FactionCreationContext.warned` (`[ThreadStatic]`, `CharacterFaction.cs:69, 86-91`) | Diagnostics only: the "made with no faction in hand" warning is given once per process, so a second mixed game's missing context goes unlogged (M2 relies on this log). | `FactionCreationContext.Reset()`, which clears `stack` and `warned`, called from `MixedFactions.Reset()` on the main thread. |
| `NewGameFactionCapture.NoticeLockedFaction` (`:27, :74`; shown in `ColonyFactionService.PostLoad :68-75`) | Barely. It is set at a solo New Game's Start, just before that game loads. It leaks only if `StartNewGame` fails after the prefix and a save is loaded next, which then shows a stray "faction not unlocked" notice. | One line in `Decide`: clear it when `!parameters.NewGame`. |
| `MixedFactions.AllFactions` (`:43`, not in `Reset`) | No. `Decide` rewrites it first thing in every game and map-editor scene (`:76`). The menu never reads it (it uses `NewGameFactionCapture`'s own list). | – |
| `ColonySession.joiningClosedAtStart` (not reset at the menu) | No. The host resets it in `BeginHostSession`; a guest takes the host's value from the first message; single-player never waits (no `ReplayService`, so `?? 1`). | – |
| `LobbyHostPanel.lastSettlementName` | On purpose. | – |
| Also checked: `ColonyFactionService.table`/`Changed` (reset through `IResettableSingleton` on each `SingletonManager.Reset`, and in `Load`); `FactionSelection.SelectedFaction` and `FactionMaxWellbeingPatcher` (reset in `Load`); `FactionStockpilePatcher.StockpileFaction` (cleared in its finalizer); `LocalFactionPick.Mine` and `LobbySession.Current` (cleared or ended at the main menu) | No. | – |

- Check to add: in StabilityTests or RuntimeChecks, call `MixedFactions.Reset()` and assert
  `ColonySession.HostFactions == null`, and that the `shaftModels` and `factionIcon` fields are empty or null (set them
  first by reflection). Sketch: `revC/fix` compiles, and the existing suites pass with the resets in.
- Test script, Script F new line: play a mixed *save* room as the guest after having hosted a mixed game on the same
  computer with only Folktails unlocked. The founding dialog must offer every faction the host has.

---

## Found sound
- **E1**: every service bound in Game (single-player and co-op), MainMenu and MapEditor has its whole dependency graph
  bound, validated by Bindito itself. No duplicate bindings, no cycles, no two-constructor classes.
- **E2 today**: all 273 (274 Steam) patch classes resolve with Harmony 2.4.1 against 1.1.2.4. No ambiguous, missing or
  abstract targets. All 496 injected parameters are valid. No method is patched both by a Factions/Lobby/MultiStart class
  and by another class.
- **E7**: a non-mixed save is structurally beta19's (no `CharacterFaction` entry, no `ColonyFactions` singleton, decal
  and start template untouched). `JudgeFactions` and the MultiStart hunks do nothing when off. Empty tooltips never show.
- **C2**: none of the maintainer's 7 mods patches a beta18–20 target. HungryPathing is safe with per-character needs.
  MixedStorage sees only the faction-filtered `InputGoods`. LGP's BackgroundSave keeps the waiting room's world save
  complete before it is read. PersistentWorkAreas is unaffected by the held loading screen.
- **C3**: beta17–19 saves load as not mixed. Non-mixed beta20 saves open in beta19.
- **S1**: `AllFactions`, `joiningClosedAtStart`, `ColonyFactionService.table/Changed`, `SelectedFaction`,
  `StockpileFaction`, `LocalFactionPick`, `LobbySession.Current`: reset where they need to be, or never read stale.
- **E6 correctness**: `SaveColonyReader` returns the right values on both real saves (base faction; separate or not;
  slot table). Its cost is the only issue.
- **Gate audit (IL)**: every Factions patch method but the two deciding ones reads `MixedFactions.IsOn` first and does
  nothing else when it is off. Three allocate a closure before the gate (trivial).
