# CLAUDE.md

BeaverBuddies MultiColony: a Timberborn co-op mod (C#, Harmony) where each player runs their own colony on one map and
the colonies trade only at Trading Posts. Built on the BeaverBuddies Stability Fork, itself built on thomaswp's
BeaverBuddies (GPL-3.0). Mod source in `BeaverBuddies/`, networking in `TimberNet/`, headless checks in
`StabilityTests/`, game-assembly checks in `RuntimeChecks/`, plans and reviews in `design/`, the changelog in
`STABILITY-CHANGELOG.md`, the full rules in `TWO-COLONIES.md`. Changes land on `main` by PR → merge.

Mod checks, as CI runs them (`.github/workflows/tests.yml`, .NET 8; CI restores from nuget.org, no local cache needed):

```
dotnet restore StabilityTests/StabilityTests.csproj --source https://api.nuget.org/v3/index.json
dotnet run --project StabilityTests --no-restore        # ends "N/N passed" (476/476 at rc6)
python -m unittest discover -s RuntimeChecks -p test_water_snapshots.py
```

Building the mod and `RuntimeChecks` need the game's assemblies: see README "Building from source". CI never touches
`docs/`.

## Standing rules

- Never launch or drive Timberborn, and never touch installed mods or saves. The maintainer (Kyler) playtests himself.
- Commit on a branch and open a PR, then merge it yourself (`gh pr merge <n> --merge`) once it is ready: its checks
  pass and nothing is left to do. Kyler, 2026-09-23: "Merge things that need to be merged automatically."
- Every release is a GitHub **pre-release** (`v1.4.0-rc4` …). Link to `/releases`, never `/releases/latest`.
- Fresh games only: no save-compatibility notes, no old-settings notes. 1.4.0 counts as unreleased, so player-facing
  text never says which version added or changed something.
- Tooltips in Mod Settings don't wrap (≤112 chars per line), if site copy is reused in game.

## Website

- **Where:** `docs/`: `index.html` (home), `install.html`, `troubleshooting.html`, `faq.html`. No 404 page. Shared
  `assets/style.css` + `site.js`; the Trading Post demo is `assets/trade-demo.js` + `assets/game-panel.css` with item
  icons in `assets/goods/`. Live at https://timbermods.github.io/BeaverBuddies-MultiColony/.
- **Published:** GitHub Pages serves `main:/docs`, so merging to main publishes; a build takes about a minute.
- **Latest releases update themselves:** when a release becomes GitHub's Latest, `.github/workflows/latest-release.yml`
  (the shared timbermods workflow) appends the standard footer to its notes, sets the site's
  `data-release="version|tag|asset-name"` fallback text and the README lines ending in `<!-- latest -->` to the new
  version, runs the site checks and commits to main. Pre-releases change nothing. Descriptions, status lists and FAQs
  stay manual (the checklist below). Dry run: Actions → Latest release → Run workflow.
- **Look:** "River Station Signage". The site is a small river station's signage: fired-enamel plates bolted to timber
  posts, telling two neighbours where to go and where to meet. The look is fixed: updates extend it, never restyle it.
- **Design records (read these before any site change):**
  - `PRODUCT.md`: the facts, voice and every site contract. (Root `DESIGN.md`/`PRODUCT.md` are the SITE's records;
    mod design lives in `design/` and the root `*.md` feature docs.)
  - `DESIGN.md`: the visual system and its named rules, the source of truth for the look.
  - `.impeccable/surfaces/docs-index-html.md`: the direction contract. Its OWN-WORLD hexes and 6px corners are the
    pre-build draft; the shipped values below (and DESIGN.md) win.
  - `.impeccable/design.json`: tokens and component snippets. `.impeccable/critique/`: the pre-redesign critique.

### Design rules (from DESIGN.md; keep them)

- **Colony Paint**: amber is colony 1 and teal is colony 2, everywhere; nothing else wears either (Download is navy).
- **Meeting**: both colony colours appear together only where colonies meet: the Trading Post sign, the section
  marker, the hero map and the demo's colony toggles.
- **Same Paint at Night**: in dark mode ground, text and timber change; plate fields keep their paint, only colony
  lettering on the ground lifts (`--c1-text`, `--c2-text`).
- **Painted-Word**: uppercase tracked lettering only for glance labels (nav, buttons, table heads, status); never
  paragraphs, never a kicker above a heading.
- **Two Voices**: Barlow Semi Condensed for headings/labels/buttons; system-ui for reading (68–72ch).
- **Mounted, Not Floating**: a shadow means bolted to the wall (`--mount`); no wide soft shadows, glows or card hover
  lift. Only buttons lift 2px and the chosen signpost card is struck forward 6px.
- Colour meanings: navy = actions/directions; caution yellow = status/attention only (status tag, `.not-played`,
  caution notes, focus ring, selection, rail arrows), always lettered in ink #14232a; timber = structure only (posts,
  rails, rules, header beam, footer) with bark lettering.
- Tokens live in `docs/assets/style.css` `:root`, and dark in `@media (prefers-color-scheme: dark) { :root {…} }`.
  Light / dark: stone #e2e6e1 / #0f191b, stone-deep #d3dad4 / #0a1214, enamel #f7f8f5 / #182427, ink #14232a /
  #e6ebe7, ink-soft #42525a / #a5b3b1, rule #b9c4bf / #2c3c3f, c1 #a24814 (text at night #eb8a45), c2 #1a6a77 (night
  text #5fc0cf), navy #1d3440 / #27475a, link #1d4f66 / #8cc9e0, timber #5a3d26 / #3a2819, caution #e5b53b / #d9a93a.
- Plates: `.plate` + `enamel | c1 | c2 | caution` (navy by default), optional `.grommets`; 7px corners, 1px rim, white
  keyline inset ~5–7px, mount shadow. Buttons 6px, code/keys 4px, tags 3px. Focus: 3px caution outline, offset 3px.
- Fonts: Barlow Semi Condensed 600/700 (`docs/assets/fonts/barlow-semi-condensed-latin-{600,700}.woff2`, licence
  `OFL-Barlow.txt`); body is system-ui, mono ui-monospace. Noto Sans 400/700 (`noto-sans-latin-*.woff2`, `OFL.txt`) is
  for the Trading Post replica only. No other webfonts, nothing from a CDN at runtime.
- Textures and art: none are raster. Timber grain, grommets, ticks and icons are inline SVG / CSS data URIs; the hero
  map is inline SVG. The only rasters are the game item icons in `assets/goods/` (Logs, Science, beaver from the game;
  Gears, Berries, Carrots from the MixedStorage site), used only inside the demo. Never add stock or generated
  imagery; a new raster needs provenance via Impeccable `embed-prompt <file> --prompt "Origin: …"`.
- Themes: light and dark follow `prefers-color-scheme` only (no toggle, no theme storage key). Check both. The only
  localStorage key is release.js's cache `tbmods.release.v2.timbermods/BeaverBuddies-MultiColony` (30 min); clear it
  when checking version badges.
- Phones: no horizontal scroll at 390px, tap targets ≥ 44px (menu links 48px). Breakpoints 1060/940/760/640/520.
- Motion: the one barter loop on the hero map (goods slide to the Trading Post and cross, 9s
  `cubic-bezier(.45,0,.2,1)`, the post glows), paused off screen; everything still under `prefers-reduced-motion`.
- Signatures: the two-arm signpost (Choose your start, `[data-signpost]`), the Trading Post sign (amber and teal
  plates on one timber post), the hero map with leader-line labels, the colony section marker.
- The Trading Post replica is a contained exception: the game's greens, wood, yellow captions and Noto Sans stay
  inside `.tp` / `game-panel.css`. The controls beside it are station style.
- Don't: nested cards, icon tiles or icon-over-title grids, left-stripe callouts (a note is a whole `.note.plate`
  with a `.sign-label`), thin-border wide-shadow cards, kickers/eyebrows (the marker is the only mark above a
  heading), emoji or text glyphs as icons, new accent colours, official Timberborn logos or key art.
- New components: build from these tokens and components, match the neighbouring sections, add them to DESIGN.md.

### Content rules

- Describe the mod as it is now for a fresh game. No "New in", "added in", version history or old-save caveats on
  player pages; that belongs in `STABILITY-CHANGELOG.md` and the release notes.
- Played / Not played on the site matches the README's top WARNING block exactly. Never invent numbers, reviews,
  screenshots or gameplay shots.
- Keep the credits (Stability Fork → thomaswp's BeaverBuddies, GPL-3.0) and the "unofficial, not affiliated with or
  endorsed by Mechanistry" line in every footer.
- Terminology: **Trading Posts** (never "District Crossings"). No land, borders or territory: roads never join except
  through a Trading Post. Mod name in game: **BeaverBuddies MultiColony (beta)**. Setting and button names exactly
  as in game.
- The Trading Post demo must keep matching the mod (`Colonies/TradingPostFragment.cs`, `ExchangeTerms`,
  `TradeOfferForm`): a colony is named by its player (Kyler = colony 1, Sarah = colony 2); ledger stamp
  `cycle-day` ("3-13"); 0–100 of an item per round, steps 10 / Shift 1 (beavers 1 / Shift 10); rounds 1–99.
- `docs/assets/release.js` is MultiColony's own variant of the timbermods release script (the other sites share one
  byte-identical copy, SHA-1 f771fa55…). This copy fills `data-release` / `data-release-href` from the most recently
  *published* pre-release, because GitHub's release list sorts tags as text. The HTML's static values are the
  fallback. Don't overwrite it with the shared copy, and change it only for a real bug.

### Update the website for a new release

When asked to "update the website for the latest release, consistent with the design":
1. Read what changed: `gh release list -R timbermods/BeaverBuddies-MultiColony -L 5`, `gh release view <tag> -R
   timbermods/BeaverBuddies-MultiColony`, the README, the top entries of `STABILITY-CHANGELOG.md`, `TWO-COLONIES.md`.
   List every player-facing change: settings moved/removed, hosting steps, keys, played status, game version.
2. Update every place the site states a changed fact:
   - Static version fallbacks: `grep -rn "<old version>" docs` (index: hero `.status-tag`, install step asset name,
     cross-check `.val`, `#release` h2 and `.ver`; install.html: download, `Get-FileHash`, `#compare`, `#verify`;
     troubleshooting.html `#setup` log line).
   - `#release` on index: bump `data-release-pinned="<version>"` and rewrite its `h3`/`p` pairs and the plate's line
     for the new release (release.js otherwise appends a "written for …" note).
   - Status: index `#status` (lead line, "Played in real games" and "Not played yet" checklists), the `.not-played`
     tags on `#play` cards, the muted waiting-room line in `#play`: all to match the README exactly.
   - Settings and hosting steps: index `#start` (signpost path cards, `.switch` rows, `.shared-note`) and `#play`
     (waiting room Host / Friends lists, cards' setting names); install.html `#host`, `#join`, `#settings` (tables per
     `.settings-group`) and `#update`; faq.html `#general`, `#playing` and `#compat` answers; troubleshooting.html `#joining`,
     `#colonies`, `#features`. Grep setting names: `grep -rn "Mod Settings\|Separate colonies\|Allow founding\|Mixed
     factions\|Host co-op\|Co-op Game" docs`.
   - Game version / requirements: `grep -rn "1.1.2.4" docs` (hero tag, `#install .both`, cross-check, install
     `#requirements` and `#compare`, faq `#general`, troubleshooting `#setup`).
   - `<meta name="description">` and `og:` tags on each page.
   - PRODUCT.md, in the same pass: Operating Context (ways to play, setting names and where they live, features)
     and the Status paragraph (current version, played status).
3. Put new content into existing components: a feature → a `.plate.enamel` article in `#play .stack` (add
   `<span class="not-played">Not played yet</span>` in its h3 if unplayed); a colony rule → `.rules li`; a setting →
   a row in install.html `#settings`; a question → a `details.q` with an `id` in the right faq/troubleshooting
   section (and its TOC); a note → `.note.plate.enamel` or `.caution` with a `.sign-label`. Don't restyle anything.
4. Test (no site test exists; from the repo root in Git Bash, both must pass):
   ```
   for f in docs/assets/*.js; do node --check "$f" || echo "FAIL $f"; done
   python -c "import re,pathlib,sys;d=pathlib.Path('docs');bad=[(p.name,u) for p in d.glob('*.html') for u in re.findall(r'(?<![-\w])(?:href|src)=\"(?![a-z]+:|#)([^\"#]+)',p.read_text(encoding='utf-8')) if not (d/u).exists()];print(bad or 'all local links resolve');sys.exit(bool(bad))"
   ```
5. Preview: `python -m http.server 8781 -d docs` (background), open http://localhost:8781/. Capture light, dark and a
   390px phone: with the personal `impeccable-site-flow` skill, `python <skill>/scripts/capsite.py
   http://localhost:8781/ <out> "" install.html faq.html troubleshooting.html` (expect `overflow 0`); otherwise the
   Browser pane in both colour schemes at desktop and mobile. Check the changed sections. Stop the server after.
6. Optional: `"$(ls -d ~/.claude/plugins/cache/impeccable/impeccable/*/skills/impeccable | tail -1)/scripts/impeccable"
   detect --json docs` (exits 2 when it reports anything; parse from the first `[`). Known false positives: the one
   warning, Noto Sans in `game-panel.css` (the replica); advisories for off-ramp font sizes, `#fff` plate lettering,
   2/5px radii and the replica's game colours; `cramped-padding` on all four pages (ignored in
   `.impeccable/config.json`: clamp() and nested padding). `embed-prompt --scan docs` lists the 12 goods icons as
   missing provenance: they are game icons, not generated.
7. If the look changed (a new component or layout), update DESIGN.md and `.impeccable/design.json`.
8. Update the README if it repeats the facts.
9. Ship: branch → commit → push → `gh pr create`. Once its checks pass: `gh pr merge <n> --merge` (that publishes),
   then verify:
   - `gh api repos/timbermods/BeaverBuddies-MultiColony/pages/builds/latest -q .status` is `built`;
   - `curl -s https://timbermods.github.io/BeaverBuddies-MultiColony/ | grep -c "<a changed string>"` finds it.

### Full redesign

A new look goes through the whole Impeccable flow (init → critique → audit → direction → build → finish review →
DESIGN.md). With the personal skill: "use the impeccable-site-flow skill to redesign this site".
