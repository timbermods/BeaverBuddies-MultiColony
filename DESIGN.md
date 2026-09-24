---
name: Timber Together
description: The project site as a river station's signage, with fired-enamel plates on timber posts telling two neighbours where to go and where to meet.
colors:
  stone: "#e2e6e1"
  stone-deep: "#d3dad4"
  enamel: "#f7f8f5"
  ink: "#14232a"
  ink-soft: "#42525a"
  rule: "#b9c4bf"
  c1: "#a24814"
  c1-rim: "#74310b"
  c1-wash: "#f1ddcf"
  c2: "#1a6a77"
  c2-rim: "#0f4750"
  c2-wash: "#d3e8ea"
  navy: "#1d3440"
  navy-rim: "#0f1f27"
  link: "#1d4f66"
  timber: "#5a3d26"
  timber-deep: "#3f2a19"
  bark: "#f1e8da"
  bark-soft: "#d9c6ab"
  caution: "#e5b53b"
  caution-rim: "#a47b16"
  map-ground: "#cfd8c9"
  map-water: "#8fc3cf"
  map-water-edge: "#4e97a8"
  map-tree: "#6f8f5c"
  night-stone: "#0f191b"
  night-stone-deep: "#0a1214"
  night-enamel: "#182427"
  night-ink: "#e6ebe7"
  night-ink-soft: "#a5b3b1"
  night-rule: "#2c3c3f"
  night-c1-text: "#eb8a45"
  night-c2-text: "#5fc0cf"
  night-navy: "#27475a"
  night-link: "#8cc9e0"
  night-timber: "#3a2819"
  night-caution: "#d9a93a"
typography:
  display:
    fontFamily: "Barlow Semi Condensed, Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow, sans-serif"
    fontSize: "clamp(2.6rem, 6.4vw, 4.6rem)"
    fontWeight: 700
    lineHeight: 0.92
    letterSpacing: "-0.01em"
  headline:
    fontFamily: "Barlow Semi Condensed, Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow, sans-serif"
    fontSize: "clamp(1.9rem, 4.2vw, 2.75rem)"
    fontWeight: 700
    lineHeight: 1.08
    letterSpacing: "-0.005em"
  page-title:
    fontFamily: "Barlow Semi Condensed, Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow, sans-serif"
    fontSize: "clamp(2.4rem, 5.5vw, 3.8rem)"
    fontWeight: 700
    lineHeight: 1.08
  title:
    fontFamily: "Barlow Semi Condensed, Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow, sans-serif"
    fontSize: "1.35rem"
    fontWeight: 700
    lineHeight: 1.15
  body:
    fontFamily: "system-ui, -apple-system, Segoe UI, Roboto, Helvetica Neue, Arial, sans-serif"
    fontSize: "17px"
    fontWeight: 400
    lineHeight: 1.65
  label:
    fontFamily: "Barlow Semi Condensed, Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow, sans-serif"
    fontSize: "0.82rem"
    fontWeight: 700
    letterSpacing: "0.09em"
  button:
    fontFamily: "Barlow Semi Condensed, Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow, sans-serif"
    fontSize: "1.08rem"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "0.06em"
  mono:
    fontFamily: "ui-monospace, Cascadia Code, SF Mono, Consolas, Liberation Mono, monospace"
    fontSize: "0.86em"
rounded:
  tag: "3px"
  sm: "4px"
  panel: "6px"
  plate: "7px"
spacing:
  gutter: "clamp(1rem, 4vw, 2.5rem)"
  band: "clamp(3.5rem, 8vw, 6.5rem)"
  wrap: "1180px"
  plate-pad: "clamp(1.3rem, 3vw, 1.9rem)"
  target: "44px"
components:
  button-primary:
    backgroundColor: "{colors.navy}"
    textColor: "#ffffff"
    typography: "{typography.button}"
    rounded: "{rounded.panel}"
    padding: "0.7rem 1.35rem 0.72rem"
    height: "3rem"
  button-ghost:
    backgroundColor: "{colors.enamel}"
    textColor: "{colors.ink}"
    typography: "{typography.button}"
    rounded: "{rounded.panel}"
    padding: "0.7rem 1.35rem 0.72rem"
    height: "3rem"
  button-copy:
    backgroundColor: "{colors.navy}"
    textColor: "#ffffff"
    rounded: "{rounded.sm}"
    padding: "0 1rem"
    height: "44px"
  button-copy-hover:
    backgroundColor: "{colors.navy-rim}"
  plate-navy:
    backgroundColor: "{colors.navy}"
    textColor: "#ffffff"
    rounded: "{rounded.plate}"
    padding: "{spacing.plate-pad}"
  plate-c1:
    backgroundColor: "{colors.c1}"
    textColor: "#ffffff"
    rounded: "{rounded.plate}"
  plate-c2:
    backgroundColor: "{colors.c2}"
    textColor: "#ffffff"
    rounded: "{rounded.plate}"
  plate-enamel:
    backgroundColor: "{colors.enamel}"
    textColor: "{colors.ink}"
    rounded: "{rounded.plate}"
    padding: "{spacing.plate-pad}"
  plate-caution:
    backgroundColor: "{colors.caution}"
    textColor: "{colors.ink}"
    rounded: "{rounded.plate}"
  tag-not-played:
    backgroundColor: "{colors.caution}"
    textColor: "{colors.ink}"
    rounded: "{rounded.tag}"
    padding: "0.3rem 0.45rem"
  question:
    backgroundColor: "{colors.enamel}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    padding: "1rem 3.2rem 1rem 1.15rem"
---

# Design System: Timber Together

## Overview

**Creative North Star: "River Station Signage"**

The site is the signage of a small river station: fired-enamel plates bolted to timber posts, telling two neighbours where to go and where to meet. Every surface is either river-stone ground, a flat enamel plate with a white keyline and a darker rim, or timber (posts, rails, rules, the header beam and the footer). The world is literal about its one idea: two colonies on one map, joined only at a Trading Post. Amber enamel is colony 1, river-teal enamel is colony 2, and the only place they touch is on one post.

Density is that of a well-lettered notice board. Bands of stone and a few painted fields, heavy condensed lettering for anything a passer-by reads at a glance, and plain system-ui prose for anything read standing still. Depth is physical and shallow. Plates sit a few pixels proud of the wall on a short mount shadow, and nothing glows or floats.

The site comes in two lights. At night (`prefers-color-scheme: dark`) the ground goes dark but the enamel keeps its colors. Colony lettering on the ground brightens to stay legible, and plates stay the same paint.

The world rejects the mod-page default: the screenshot hero, the icon-tile feature grid and the floating white card with a thin border and a wide shadow.

**Key Characteristics:**
- Enamel plates: a solid field, a 1px darker rim, a white keyline inset about 5px, 7px corners, optional grommets, and a short mount shadow.
- Timber is the structure: posts, rails, section rules, the header beam, the footer. It never carries content color.
- Two colony colors, used only for their own colony. Navy is for actions and directions, caution yellow for status.
- Condensed signage lettering for headings, labels and buttons. system-ui for reading.
- One moving part: goods crossing at the Trading Post on the hero map.

## Colors

The palette is a painted-enamel set on river-stone: two colony enamels, a navy direction enamel, a caution yellow and timber, all contrast-checked against their grounds in both lights.

### Primary
- **Station Navy** (navy): the enamel of directions and actions, used for buttons, the default plate, the chosen signpost arm, the copy button, the question toggles, the checklist dots and the keyline on enamel plates. At night it lifts to night-navy.
- **Channel Blue** (link): body links on the stone ground (night-link at night). Underlined with a 1px underline offset 3px, thickening to 2px on hover.

### Secondary
- **Colony Amber** (c1, with c1-rim for the plate rim, c1-wash for tints): colony 1 and nothing else. It appears as a plate field, a map building or road, a marker half, or colony lettering. On the dark ground, colony lettering switches to night-c1-text.
- **River Teal** (c2, with c2-rim, c2-wash): colony 2 and nothing else. It is used exactly as amber is. Colony lettering at night uses night-c2-text.

### Tertiary
- **Caution Yellow** (caution, rim caution-rim; night-caution at night): status and attention. It appears on the hero's status tag, the "not played" tag, the caution note plate, text selection, the focus ring and the arrows between the How it works steps. Lettering on it is always dark ink (#14232a), in both lights.

### Neutral
- **River Stone** (stone; night-stone): the page ground and the sticky header.
- **Deep Stone** (stone-deep; night-stone-deep): alternate bands, hover wash on nav links and TOC links, the resting signpost arm.
- **Enamel White** (enamel; night-enamel): the light plate, code and keys, tables, questions and map labels.
- **Station Ink** (ink; night-ink): text and 1.5px outline strokes (open questions, map labels, path boxes).
- **Weathered Ink** (ink-soft; night-ink-soft): secondary text, table headers, small signage labels on the ground.
- **Rule Grey** (rule; night-rule): hairlines, resting outlines and the rims of enamel plates.
- **Timber** (timber, timber-deep; night-timber at night): posts, rails, section rules, the header beam (a 4px shadow line), the footer and the timber bands.
- **Bark** (bark, bark-soft): lettering on timber.
- **Map set** (map-ground, map-water, map-water-edge, map-tree): only inside the hero map's SVG.

### Named Rules
**The Colony Paint Rule.** Amber is colony 1 and teal is colony 2, everywhere. No button, link, highlight or decoration wears either color unless it stands for that colony. The primary Download is navy, not amber.

**The Meeting Rule.** The two colony colors appear together only where the colonies meet: the Trading Post sign, the section marker, the map and the trading demo's colony toggles.

**The Same Paint at Night Rule.** In dark mode the ground, text and timber change. Plate fields stay the same paint, and only the colony lettering sitting on the ground is lifted for contrast.

## Typography

**Display Font:** Barlow Semi Condensed 600/700, self-hosted under the SIL OFL (fallbacks Bahnschrift SemiCondensed, DIN Condensed, Roboto Condensed, Arial Narrow)
**Body Font:** system-ui (with -apple-system, Segoe UI, Roboto, Helvetica Neue, Arial)
**Label/Mono Font:** ui-monospace (with Cascadia Code, SF Mono, Consolas) for paths, settings and keys

**Character:** Road-sign lettering over plain reading text. The condensed face is used for anything short enough to be painted on a plate. Long text stays in the reader's own system face.

### Hierarchy
- **Display** (700, clamp 2.6–4.6rem, line-height 0.92): the name plate's title only. Its small line above ("BeaverBuddies") is part of the name, set in uppercase at 600.
- **Headline** (700, clamp 1.9–2.75rem, 1.08): section headings on the home page. Guide pages use the page title (clamp 2.4–3.8rem) and prose h2s at clamp 1.8–2.3rem.
- **Title** (700, 1.2–1.6rem, 1.15): plate titles, step titles, rail steps and questions (1.2rem).
- **Body** (400, 17px / 16.5px under 640px, 1.65): prose at a 68–72ch measure. Bold is set at 650.
- **Label** (700, 0.72–0.92rem, 0.04–0.16em tracking, uppercase): the signage label on plates. The same treatment is used for nav links, table headers, reading names, settings group names, footer headings, the TOC heading, switch states and the "not played" tag.
- **Button** (700, 1.08rem, 0.06em, uppercase): every plate button. The small line inside a button drops to 600, sentence case.

### Named Rules
**The Painted-Word Rule.** Uppercase tracked signage lettering is for labels a passer-by reads in one glance: navigation, buttons, table heads, names of readings, status. It is never used for paragraphs, and never as a line placed above a heading to announce it.

**The Two Voices Rule.** The condensed face is for headings, labels and buttons. system-ui is for reading. Don't set running text in the display face or headings in the body face.

## Layout

The page is a single column of full-width bands inside a centred wrap of 1180px, with a fluid gutter (clamp 1rem–2.5rem) on each side. Bands alternate stone, deep stone and timber, with vertical padding of clamp 3.5–6.5rem. A section head holds a colony marker, the heading and one lead line, capped at 60ch.

The home page is composed in rows: the hero (post and plate on the left, the map on the right), a three-step rail, the two-arm signpost, full-width install steps, the play grid (1.25fr / 1fr), a four-column inherited-features strip ruled top and bottom in timber, and a closing plate. The guide pages use a 220px sticky TOC beside a 760px prose column from 1000px up. Below that the TOC becomes an enamel panel of links above the prose.

Breakpoints, as shipped:
- **1060px:** nav links tighten.
- **940px:** the nav folds into a Menu disclosure that opens a navy plate. The hero stacks with the map after the pitch. Two-column grids stack, and the inherited strip becomes 2×2.
- **760px:** the rail turns vertical with the timber rail down the left. The signpost loses its pole and stacks its arms and cards in order, with both arms pointing right. Step numbers shrink.
- **640px:** body text goes to 16.5px, buttons go full width, the map's leader-line labels become a numbered key under the map, and the Trading Post sign shrinks.
- **520px:** the cross-check readings stack.

Touch targets are at least 44px everywhere: nav links, the menu toggle, question summaries, copy buttons, TOC links on small screens and trading demo controls. Menu links are 48px. Footer links are 36px.

## Elevation & Depth

Depth is mounting, not floating. A plate sits on the wall on one short shadow: a 1px contact line plus a tight drop pulled in by a negative spread, deepened at night. Everything else is flat and is separated by fills, 1.5px inset outlines or timber rules. Hover never raises a card. Only two things move toward the viewer: a button lifts 2px on hover, and the chosen signpost card is struck forward.

### Shadow Vocabulary
- **Mount** (`box-shadow: 0 1px 0 rgba(20,35,42,.28), 0 10px 18px -12px rgba(20,35,42,.55)`; at night `0 1px 0 rgba(0,0,0,.5), 0 12px 20px -12px rgba(0,0,0,.8)`): under every plate, button and map label.
- **Plate build-up** (`box-shadow: inset 0 0 0 1px rim, inset 0 0 0 5px field, inset 0 0 0 7px keyline, mount`): draws the rim and the white keyline inside the plate. On buttons it is 1/4/6px.
- **Struck forward** (`translateY(-6px)`, keyline 8px navy, `0 22px 30px -18px rgba(20,35,42,.6)`): the chosen signpost card only. It is turned off under 760px.
- **Header beam** (`box-shadow: 0 4px 0 timber`): the sticky header's timber edge.

### Named Rules
**The Mounted, Not Floating Rule.** A shadow means "bolted to the wall". There are no wide soft shadows, no glows, and no hover elevation on cards.

## Shapes

The form language is enamel and wood. Plates have gently rounded corners (7px), and buttons and panels 6px. Code, map labels and nav links use 4px. Tags, marker halves and switch states use 3px. Timber pieces are nearly square (2–3px). Borders are drawn inside, as inset box-shadows, so plates keep their painted edge. Two shapes are cut with clip-path: the signpost arms, as pointed boards with a 26px point, and the caution arrows between rail steps. Dots are round: grommets (8px, shaded), checklist marks and map pins. Install steps carry measuring-stick ticks painted above the step number.

## Components

### Buttons
Buttons are plates you can press.
- **Shape:** 6px corners, a 1px rim, a 4px field band and a 2px keyline inside, and the mount shadow. Minimum height 3rem.
- **Primary:** navy plate, white lettering in the button style, padding 0.7rem 1.35rem. An inline SVG arrow or download mark at 1.15em.
- **Hover / Focus:** lifts 2px over 0.18s `cubic-bezier(.2,.7,.2,1)`. It settles back on press. The focus ring is the global caution outline.
- **Ghost:** an enamel plate with a navy keyline and ink lettering. On the timber closing plate the buttons switch to enamel, and the ghost becomes a transparent plate with a white 2px outline.
- **Copy:** a small navy button without a plate (44px high, 4px corners) beside a path box. It darkens to navy-rim on hover.
- **Text link with arrow:** uppercase display lettering with a masked SVG arrow. It gains an underline on hover.

### Chips
- **Status tag:** a caution plate with a dark rivet dot on the left. Version, game version and "back up your saves" are separated by middle dots.
- **Not played:** a small caution tag (3px corners, 0.72rem label lettering), placed inline after a feature name.
- **Switch state:** a navy "On" chip, or an "Off" chip outlined in ink-soft, inside a stone switch row that names the setting.

### Cards / Containers
- **Corner Style:** the plate (7px).
- **Background:** navy (default), c1, c2, enamel or caution. Grommets are optional and used on hanging signs.
- **Shadow Strategy:** the plate build-up plus the mount (see Elevation).
- **Border:** the rim and keyline are inset. Enamel plates take a navy keyline, and caution plates an ink keyline.
- **Internal Padding:** clamp 1.3–1.9rem for content plates, and 1.05–1.25rem for notes.
- Code and keys on a coloured plate turn translucent white (14% fill, 38% outline) and take the plate's lettering.

### Inputs / Fields
- **Path box:** the path in mono on enamel with a 1.5px ink outline, paired with a Copy button.
- **Questions (FAQ, troubleshooting):** an enamel panel with a 1.5px rule outline and 6px corners, outlined in ink when open. The summary is display lettering at 1.2rem. A 22px navy square with an SVG plus/minus sits on the right.
- **Focus:** a 3px caution-yellow outline, offset 3px, on every focusable element.

### Navigation
- **Header:** sticky on stone, over the timber beam. The brand mark (40px SVG) sits beside the name in display lettering. Nav links are uppercase display lettering in ink-soft, 44px high. They get a deep-stone wash on hover, and the current page gets a 3px ink underline. Download is a small plate button.
- **Mobile (≤940px):** a Menu disclosure with a 2px ink outline, inverted when open, drops a navy plate with 48px links.
- **Guide TOC:** a timber rail on the left with display-lettered links, sticky from 1000px.
- **Footer:** timber ground, bark lettering, a 4px timber-deep top edge, and label-style column heads.

### Timber Post and Rail Vocabulary
Timber is the structure every sign hangs on:
- **Hero post:** a 12px grained timber post down the left of the hero column, carrying the name plate.
- **Section marker:** an amber half and a teal half meeting on a 5px timber post (26×12px each). Decorative and aria-hidden. It is the only mark above a section heading.
- **Rail:** a 10px timber-deep beam behind the three How it works plates, with caution arrows between them. It turns vertical on phones.
- **Install step rules:** each step is a full-width row between 2px timber rules. A large display numeral sits under painted measuring ticks.
- **Signpost pole:** a 16px timber pole between the two arms of Choose your start.
- **Section rules:** 2px timber lines rule the inherited strip, the table of rules and the cross-check readings.

### Signpost (signature)
Choose your start is a two-arm signpost. Two pointed boards on a timber pole, in deep stone, turn navy when pressed (`aria-pressed`). Below each arm hangs an enamel path card. Both cards stay fully readable. The chosen one is struck forward (6px lift, 8px navy keyline, deeper mount, 0.3s), and the other drops to a flat rule outline.

### Trading Post Sign (signature)
The Trading Post heading is an amber plate and a teal plate with grommets. They hang either side of one 40px timber post (11rem tall), and "Trading Post" is lettered vertically in bark. The plates are set in display lettering at clamp 1.6–2.3rem. On phones they shrink and lose their grommets.

### Hero Map (signature)
An inline SVG map of two colonies across a river, in the map set with colony-painted buildings and roads. It is labelled with enamel leader-line labels, and the label names are colony-coloured. The one loop of motion lives here: goods slide along each road into the Trading Post and across (9s, `cubic-bezier(.45,0,.2,1)`), and the post glows briefly as they cross. Under reduced motion the goods are shown still, with no animation.

### Trading Post Panel Replica (contained exception)
The interactive trading demo reproduces Timberborn's in-game entity window (its greens, wood, yellow captions and Noto Sans) from the game's own UI. It is a picture of the game and is styled separately. Its colors, fonts and shapes are not station tokens and must not leak into the site. The controls beside it belong to the station: 44px enamel toggles in display lettering, painted amber or teal when their colony is selected.

## Do's and Don'ts

### Do:
- **Do** build every container as an enamel plate: a solid field, a 1px rim, a keyline inset about 5–7px, 7px corners and the mount shadow.
- **Do** paint colony 1 amber and colony 2 teal wherever a colony is meant, and only then.
- **Do** use navy for every action and direction, including the primary Download.
- **Do** keep caution yellow for status and attention (status tag, not-played tag, caution notes, focus ring, selection, the rail arrows), always lettered in dark ink.
- **Do** use timber for structure (posts, rails, rules, the header beam and the footer) and bark for lettering on it.
- **Do** set headings, labels and buttons in Barlow Semi Condensed, and prose in system-ui at a 68–72ch measure.
- **Do** keep every touch target at 44px or more, and use the 3px caution focus ring offset 3px.
- **Do** keep motion to the one barter loop and the button and signpost lifts, and keep everything still under `prefers-reduced-motion`.
- **Do** draw icons as inline SVG strokes or masks.

### Don't:
- **Don't** use a colony color for a button, link, highlight or decoration that is not that colony.
- **Don't** build icon tiles or icon-over-title feature grids.
- **Don't** use left-stripe callouts. A note is a whole plate (enamel or caution) with a signage label.
- **Don't** use thin-border, wide-shadow floating cards, glows, or hover elevation on cards.
- **Don't** put a kicker or eyebrow line above a heading. The colony marker is the only mark above a section heading.
- **Don't** use emoji or text glyphs as icons.
- **Don't** let the Trading Post replica's game palette or Noto Sans leave the replica.
- **Don't** add external font or asset requests. The display face is self-hosted.
