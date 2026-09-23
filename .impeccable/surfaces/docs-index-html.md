---
version: 1
slug: "docs-index-html"
primary_target: "docs/index.html"
related_targets: ["docs/install.html","docs/faq.html","docs/troubleshooting.html"]
---

# Surface brief: MultiColony site (docs/index.html, with install, faq, troubleshooting)

Scope: the whole static site in docs/. Home page mode: Persuade. install/faq/troubleshooting: Read.

Audience and job: a host who finds the mod and sends it to a friend; the friend arrives cold from a link. Action: both on the same build, in a two-colony game. Proof: the two-colony map, the playable Trading Post replica, honest played/not-played status. Constraints: static, no external requests; one self-hosted display face (Barlow Semi Condensed 600/700, SIL OFL, approved by the user), body in system fonts; fast, mobile-friendly; describe 1.4.0-rc2 as it is for a fresh game, no version provenance on player pages; changelog = one rc2 block plus links.

Home order (user's): hero, how it works, choose your start, install (#install), waiting room / mixed factions / away players, also included, beta status and reporting, troubleshooting, what's in 1.4.0-rc2, footer.

Memorable moment: the two enamel plates, amber and teal, meeting on one timber post at the Trading Post (the sign over the Trading Post demo), echoed by the section markers and the brand mark.

Queued after the build: clarify (choose your start + install), delight (small hero barter, reduced-motion safe), adapt (mobile), polish, README to rc2, commit/push/fast-forward main.

## Direction contract

THESIS: The site is a river station's signage: fired-enamel plates on timber posts telling two neighbours where to go and where to meet. It refuses the mod-page default of screenshot hero, icon-tile feature grid and floating white cards.

OWN-WORLD: River-stone ground (#e2e6e3 light, #0f1a1c night). Colony 1 amber enamel (#b8561a), colony 2 river-teal enamel (#1d6f7c), each only ever used for its colony; the Trading Post is where both meet on one plate. Timber (#6b4a2e) for posts, rails and rules. Plates: solid enamel fields, white lettering, a darker rim, two grommet dots, 6px corners, no drop-shadow glow. Headings in signage lettering (Barlow Semi Condensed, self-hosted), body in system-ui. Code on enamel-white plates. No icon tiles, no left-stripe callouts, no thin-border-wide-shadow cards.

STORY: In one glance: one map, a colony each, trade at a Trading Post, unlike ordinary BeaverBuddies' one shared colony. Then which setting starts which game, then both players install the same build and cross-check it, then play, then report.

FIRST VIEWPORT: Left, a timber post carrying a name plate "BeaverBuddies MultiColony" (display, ~3.2rem), a one-line pitch, the one-line difference from ordinary co-op, a navy enamel Download plate with arrow (navy, not amber: each colony color is used only for its colony, per OWN-WORLD), a secondary "Install guide" plate, and a riveted status tag (version from release.js · Timberborn 1.1.2.4 · Beta: back up your saves). Right, the map of two colonies across a river, roads meeting only at the Trading Post, labelled with leader lines. On phones the map follows the pitch directly, reduced in height.

FORM: River Station Signage, candidate 7 of 7 on the ordered list (seed 4dae0276). Raises: install steps one per full-width row marked like chalk measurements (forge); leader-line annotations on the map (tensegrity); every section a painted field (lowbrow); host-and-friend cross-check of three readings (six-pack); Choose your start keeps both paths present, the chosen one struck forward (cathode). Signature interaction: the two-arm signpost for Choose your start. Motion grammar: goods sliding along the road through the Trading Post, one slow loop, off under reduced motion.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
