# Review of 1.4.0-rc2 to rc4 (for 1.4.0-rc5): the reviewers' brief

## What is under review

BeaverBuddies MultiColony, a Timberborn 1.1.2.4 lockstep co-op mod (C#, Harmony). Repository (a git worktree):
`C:/Users/Kyler/code/BeaverBuddies-MultiColony/.claude/worktrees/timbermods-beaverbuddies-multicolony-53f20d`

The last in-depth review was of 1.4.0-beta24 (it became rc1, commit `ebe3d4a`). Since then three releases were
built in one session and checked only by their author (a self-read of the diff, plus automated checks):

- **rc2** (`805b387`): the absence hand-over's default is 0 (never). In a mixed-factions game an absent player's
  colony goes only to a colony of its own faction (`ColonyLifecycle.AbsenceReceiver`,
  `FactionRules.PreferSameFaction(..., sameFactionOnly)`, `ColonyAbsence.IsAnnounced(..., hasReceiver)`), with a
  Ctrl+T status and a tooltip.
- **rc3** (`1b596c9`, merged with a docs-site commit as `04a0c5e`):
  - A **Separate colonies** checkbox on the New Game difficulty page (`Lobby/NewGameColonyOptions.cs`), with Separate
    science and Mixed factions under it; four Mod Settings removed.
  - A guest splits a shared game once, from the Esc menu (`Colonies/SharedColonySplit.cs`,
    `ColonyFoundingService.BeginSplit`, `ColonyRules.MayFound` / `SplitOffered`).
  - Fields removed from `InitializeClientEvent`.
- **rc4** (`ebb6570`, the current HEAD): every game is hosted through a Co-op Game page (the waiting room).
  - A main-menu **Host co-op game** box, which is the game's LoadGameBox in a host mode (`Connect/HostCoopFlow.cs`,
    LoadGameBox patches in `Connect/ServerHostingUtils.cs`).
  - In-game **Host co-op game** and **Save and Rehost**: the game saves, goes to the main menu and opens the page
    there (`RehostingService`, `HostCoopFlow`).
  - A guest's **Rejoin** / **Reconnect** goes to the main menu and waits for the host's page
    (`ClientConnectionService.WatchRejoin`, `ReplayService.EndSession(..., offerRejoin)`).
  - A shared save can be made separate colonies at Start (the room's checkboxes, the room's `separateAtStart` flag
    in TimberNet, `Colonies/SaveConversion.cs` with `ColonyConversionEvent`).
  - The original BeaverBuddies in-game hosting dialog was removed.

How to read the changes:
- `git diff ebe3d4a ebb6570 -- <paths>` and `git log --format='%h %s%n%b' ebe3d4a..ebb6570`.
- `STABILITY-CHANGELOG.md`, entries `## 1.4.0-rc2` to `## 1.4.0-rc4`.
- The design docs: README, TWO-COLONIES.md, ALPHA-TEST-SCRIPTS.md (Scripts S and H).

## Rules (hard)

- **Never start, drive or test Timberborn.** No `Timberborn.exe`, no `steam -applaunch`, no command-line switches.
  Never write under `C:/Users/Kyler/Documents/Timberborn`.
- **Do not change any file in the repository**, and do not run git commands that change anything (no commit,
  checkout, stash, reset, merge). Read-only: `git diff`, `git log`, `git show`, reading files.
- **Do not build or run the test suites in the repository** (another process uses its build folders). If you must try
  a snippet of C#, do it outside the repository, in your own folder under the scratchpad below.
- Write your report, and nothing else, to `SCRATCH/review-rc5/<your letter>-<area>.md`, where
  SCRATCH = `C:/Users/Kyler/AppData/Local/Temp/claude/C--Users-Kyler-code-BeaverBuddies-MultiColony--claude-worktrees-timbermods-beaverbuddies-multicolony-53f20d/1a7f49ab-4610-41db-989b-5c8047c74244/scratchpad`.

## What you can use

- **The decompiled game:** all 497 assemblies at `SCRATCH/game/<Assembly>.decompiled.cs`, for example
  `Timberborn.GameSaveRepositorySystemUI.decompiled.cs`, `Timberborn.MainMenuPanels.decompiled.cs`,
  `Timberborn.CoreUI.decompiled.cs`, `Timberborn.OptionsGame.decompiled.cs`.
- **The game's UI sources:** `C:/Program Files (x86)/Steam/steamapps/common/Timberborn/Timberborn_Data/StreamingAssets/Modding/UI.zip`
  (377 .uxml and 31 .uss files under `Views/`). Read them with Python's `zipfile`, with `PYTHONIOENCODING=utf-8`.
- **Main-menu style scope:** `Views/MainMenu/TitleScreen.uxml` attaches CommonStyle, CoreStyle, OptionsStyle,
  MainMenuStyle, MainMenuMiscStyle, ModdingStyle and SteamWorkshopStyle. Pages pushed in the main menu get these.
- **The mod's own checks,** read as documentation of intent: `StabilityTests/*.cs` and `RuntimeChecks/*.cs`. The
  checks named `rc2:`, `rc3:` and `rc4:` are in `StabilityTests/RcMainChecks.cs` and `RuntimeChecks/RcMainRuntimeChecks.cs`.

## What to deliver

A report in Markdown with these sections:

1. **Findings.** For each one:
   - an **ID** (your letter and a number: A1, A2…);
   - a **title**;
   - its **status**:
     - **Confirmed:** traced end to end in the mod and the decompiled game;
     - **Plausible:** the path exists, but one link can't be proven without running the game;
     - **Refuted:** checked, and it holds (put these in section 2 instead);
   - its **kind**: crash or session-stopper, desync, gameplay, UI or look, docs, performance, or dead code;
   - **who it hits**;
   - **evidence**: mod `file:line` and decompiled-game references;
   - a **concrete failure scenario**: what the player does, and what goes wrong;
   - a **proposed fix**, specific enough to implement;
   - a **proposed check**: a StabilityTests source or logic check, or a RuntimeChecks IL, reflection or UI-file check.
2. **Found sound.** What you checked and found correct, with a line of evidence each, so nobody redoes it.
3. **Docs.** Places where README, TWO-COLONIES or ALPHA-TEST-SCRIPTS say something the code doesn't do.

Be rigorous and adversarial: the author wrote this quickly and reviewed it only once. Prefer a few well-evidenced
findings to many guesses, but list the Plausible ones with what would settle them. Rank findings most severe first.
Keep the report under about 600 lines.
