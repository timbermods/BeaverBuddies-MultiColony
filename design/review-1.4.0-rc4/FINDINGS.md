# Review of 1.4.0-rc2 to rc4: what was found, and what 1.4.0-rc5 did about it

On 2026-09-23 Kyler asked for an independent review of the work since the last in-depth review (rc1, `ebe3d4a`): rc2's
hand-over within a faction, rc3's New Game page checkbox and a guest's split, and rc4's hosting through the Co-op Game
page, rehost, rejoin and a shared save made separate at Start (`ebe3d4a..ebb6570`). Three reviewers read the code and
the decompiled game, in parallel and without changing anything, each on one area, from one brief ([BRIEF.md](BRIEF.md)):

- **A** hosting, rehost, rejoin and the network side: [A-hosting-and-rejoin.md](A-hosting-and-rejoin.md) (11 findings);
- **B** colony rules and the timing of the new game-state actions: [B-colony-rules.md](B-colony-rules.md) (7);
- **C** the UI patches, the strings and the docs: [C-ui-strings-docs.md](C-ui-strings-docs.md) (12).

Every finding was checked again against the code and the decompiled game before anything was changed (the main menu's
height from UI.zip's sizes, the button initialisers from `Timberborn.CoreUI`, the Game Mode page's custom list from
`NewGameModePanel.uxml`). The game was never started. Each reviewer's *Found sound* section lists what held up.

Nothing found is a desync. Two findings were serious for players: rc4's headline **Host co-op game** in a game played
alone never appeared (A1/C1), and a rejoining guest could be left with a box that would not go away (A2).

| Finding | What it was | rc5 | Check |
|---|---|---|---|
| A1 = C1 | `RehostingService` was bound only in co-op scenes, so a game played alone had no **Host co-op game** | Bound in every game | `A1`, runtime `A1, A7` |
| A2 | A rejoin box closed while something covered it (the Steam overlay, a dialog) was dropped unpopped, with a dead Cancel | Such boxes are kept and taken away once on top; a closed box's Cancel pops it | `A2`, runtime `A2` |
| A3 | A Steam guest's rejoin joined whatever lobby Steam showed, usually the ended game's, and gave up with an error | Joins only a lobby whose data says it is an open Co-op Game page of this build; a guest leaves its previous host's lobby | `A3` |
| A4 | Each quiet direct try connected on the main thread (up to 3 s), back to back; a failed socket leaked | The address is asked off the main thread first; `TCPClientWrapper.Close` closes the socket even without a stream | `A4` |
| A5 | While a direct guest waited, the host's running game got a *mods differ* dialog every 3 s | A session warns about the same difference with the same player once | `A5` |
| A6 | A quiet try's bad or unresolvable address still showed an error dialog | Quiet | `A6` |
| A7 = C2 | The box's host mode survived into the game: the game's **Load game** did nothing | Only the main menu's box hosts; a game scene clears the mode | `A7`, runtime `A1, A7` |
| A8 = C4 | After a session ended, a guest was offered **Host co-op game** for its possibly out-of-step copy; the host's **Save and Rehost** became Host co-op game | Decided by how the game was loaded (`HostButtonRules`) | `A8`, runtime `A8` |
| A9 = C7 | The box's gold line went blank when the Co-op Game page closed over it | Shown again | `A9` |
| A10 | The box read the whole save on the menu's thread | Read in the task | `A10` |
| A11 = C11 | Dead code: `ReconnectNow`, `ShowWaitForSteamInvite`, `PendingRehost`, an unused loader, two orphaned strings | Removed | `A11`, runtime `A11, B7` |
| B1 | A split or converted colony started on Normal difficulty: a shared game never kept its own | Every new game keeps its starting settings, a shared one too (the Stability Fork ignores the key) | `B1` |
| B2 | On a guest, Ctrl+T counted players who had left as present, so rc2's *not handed over* status could be wrong | A guest shows the host's last presence, as the warnings use | `B2` |
| B3 | A guest that loaded before the host could act in the still-shared game, and its building became the host's | A guest's change is refused (*Not yet*) until the conversion is played | `B3` |
| B4 | The split and the conversion give the shared colony to slot 0, which a stale slot table (a shared save from before beta7) could give to someone else | **Not changed**: only pre-beta7 saves; the release candidates assume fresh games | none |
| B5 | A converted shared co-op save took the guests' science by default (the last New Game choice, usually on) | *Separate science and unlocks* starts unticked, as a split keeps one pool; its tooltip says what ticking costs | `B5` |
| B6 | **Found your own colony**, pressed after another player's split, did nothing | Says the game is separate and to found with Ctrl+K | `B6` |
| B7 | Every game now starts from a closed room, so `HostStartGate` and the late-join refusals could never act | `HostStartGate` and its strings removed; the late-join refusals stay as a guard, with a comment and a neutral *Not yet* text (also used for B3) | runtime `A11, B7` |
| C3 | With Host and Join, the main menu's panel (646 px) no longer fitted its band (616 px under the logo) | The band grows to fit the panel, up to the screen's height | `C3`, runtime `C3` |
| C5 | A rejoin swallowed every refusal, a build mismatch included, and waited for ever | It gives up and says why for another build or a full room; a Steam guest's box mentions the invite | `C5` |
| C6 | The in-game **Join co-op game** could no longer join anything | Not shown in a game | `Join box` |
| C8 | The mod's menu buttons had no click sound (never initialised) | Made as NineSliceButtons and initialised as the game's | `C8`, runtime `C8` |
| C9 | In a custom difficulty the colony checkboxes sat centred above the left-aligned settings list | They move into the list, under its Tutorial row | `C9`, runtime `C9` |
| C10 | Texts: a bug-report question with no button, a failed rehost naming the old flow, faction and hand-over wording, the rejoin's and lost connection's wording | Rewritten (the report question only with its button) | `C10` |
| C12 | Every room gained an empty 6 px slot | Shown only with its checkboxes | `C12` |

**Docs.** README, TWO-COLONIES and ALPHA-TEST-SCRIPTS follow the code: the faction ring (gone since beta22), the
conversion's science default, the mixed-factions warning, the steps rc4 made unreachable (Scripts B, C, D, F, P), Script S
(custom difficulty, *No* for the split's dialog) and Script H (rc5 steps marked *(rc5)*). **The website (`docs/`) was
not changed** (Kyler: no website updates), so it still describes the Mod Settings rc3 removed and the in-game hosting
dialog rc4 removed (C docs 1, A D5).

**Checks.** StabilityTests 453 → 474 (22 in `Rc5Checks.cs`, three older ones follow the change, one removed with
`HostStartRules`); RuntimeChecks 431 → 438 (7 in `Rc5RuntimeChecks.cs`, one older one follows the change). All 8 new or
changed runtime checks fail on rc4's released DLL; the new headless checks test rules and code rc4 does not have.
