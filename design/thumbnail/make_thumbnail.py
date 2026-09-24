"""Draws Timber Together's thumbnail (the preview in the game's mod list and on the Steam Workshop): the site's enamel
name plate over its map, two colonies whose roads meet at one Trading Post, with the shared river below. 800 x 450.

    python design/thumbnail/make_thumbnail.py      (writes BeaverBuddies/thumbnail.png; needs playwright and Pillow)

The colours, the plate and the map's shapes are the site's (docs/assets/style.css, the hero map in docs/index.html);
the font is the site's Barlow Semi Condensed (OFL, docs/assets/fonts). It is Timber Together's own art.
"""
import base64, io, os
from playwright.sync_api import sync_playwright
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
FONTS = os.path.join(REPO, "docs", "assets", "fonts")
OUT = os.path.join(REPO, "BeaverBuddies", "thumbnail.png")


def font(weight):
    with open(os.path.join(FONTS, f"barlow-semi-condensed-latin-{weight}.woff2"), "rb") as f:
        data = base64.b64encode(f.read()).decode()
    return f"@font-face {{ font-family: 'Barlow SC'; font-weight: {weight}; src: url(data:font/woff2;base64,{data}) format('woff2'); }}"


def mirror(x, w=0):
    return 800 - x - w


# one colony's buildings, drawn for colony 1 (left) and mirrored for colony 2 (right)
HOUSES = [(236, 276), (150, 308), (214, 200), (240, 200), (312, 326), (236, 362), (170, 362)]
# pines (x, ground y, scale): forest in the top corners around the plate, and along the river bank
TREES = [(24, 64, 1.1), (58, 50, 1.25), (92, 70, .95), (18, 112, 1.0), (50, 108, 1.35), (86, 120, .9),
         (28, 160, .95), (62, 166, 1.1), (98, 172, .75), (136, 212, .7),
         (26, 372, 1.05), (58, 380, .85), (88, 374, .7), (20, 330, .8)]


def colony(c, fill, road, flip):
    X = (lambda x, w=0: mirror(x, w)) if flip else (lambda x, w=0: x)
    parts = []
    # roads: the main road to the Trading Post and two branches
    def h(x1, x2, y):
        a, b = sorted((X(x1), X(x2)))
        return f"M{a} {y} H{b}"
    def v(x, y1, y2):
        return f"M{X(x)} {y1} V{y2}"
    parts.append(f'<g fill="none" stroke="{road}" stroke-width="11" stroke-linecap="square">'
                 f'<path d="{h(134, 366, 300)}"/><path d="{v(200, 300, 226)}"/><path d="{h(200, 262, 226)}"/>'
                 f'<path d="{v(300, 300, 352)}"/><path d="{h(214, 300, 352)}"/></g>')
    # the district center, with its flag
    dc = X(70, 64)
    roof_l, roof_r, peak = X(64), X(140), X(102)
    pole = X(100, 4)
    flag = (f"M{pole + 4} 222 h18 l-5 6 5 6 h-18z" if not flip else f"M{pole} 222 h-18 l5 6 -5 6 h18z")
    parts.append(f'<g fill="{fill}"><rect x="{dc}" y="278" width="64" height="48" rx="3"/>'
                 f'<path d="M{roof_l} 282 L{peak} 250 L{roof_r} 282 Z"/><rect x="{pole}" y="222" width="4" height="30"/>'
                 f'<path d="{flag}"/>')
    parts.append(f'<rect x="{X(94, 16)}" y="302" width="16" height="24" rx="1.5" fill="rgba(0,0,0,.28)"/>')
    for x, y in HOUSES:
        parts.append(f'<rect x="{X(x, 24)}" y="{y}" width="24" height="18" rx="2"/>')
    parts.append("</g>")
    return "".join(parts)


def pine(x, y, k, shade):
    """A pine standing on (x, y), k times the base size: a trunk and two tiers."""
    return (f'<rect x="{x - 2.2 * k:.1f}" y="{y - 7 * k:.1f}" width="{4.4 * k:.1f}" height="{7 * k:.1f}" fill="#5a3d26"/>'
            f'<path d="M{x} {y - 30 * k:.1f} L{x + 13 * k:.1f} {y - 6 * k:.1f} H{x - 13 * k:.1f} Z" fill="{shade}"/>'
            f'<path d="M{x} {y - 42 * k:.1f} L{x + 9.5 * k:.1f} {y - 22 * k:.1f} H{x - 9.5 * k:.1f} Z" fill="{shade}"/>')


def trees():
    out = []
    for i, (x, y, k) in enumerate(sorted(TREES, key=lambda t: t[1])):
        shade = ("#5d7e4b", "#6f8f5c", "#4f6f40")[i % 3]
        out.append(pine(x, y, k, shade))
        out.append(pine(mirror(x), y, k, shade))
    return "".join(out)


C1, C1_RIM, C2, C2_RIM = "#a24814", "#74310b", "#1a6a77", "#0f4750"
ROAD1, ROAD2 = "#b26e40", "#538686"
GROUND, GRID, WATER, WATER_EDGE = "#cfd8c9", "rgba(40,60,40,.09)", "#8fc3cf", "#4e97a8"
TIMBER, NAVY, NAVY_RIM, CAUTION = "#5a3d26", "#1d3440", "#0f1f27", "#e5b53b"
RIVER = "M-20 424 C130 394 250 448 410 414 S650 374 820 400"

SCENE = f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 450" width="800" height="450">
<defs>
  <pattern id="tiles" width="25" height="25" patternUnits="userSpaceOnUse"><path d="M25 0H0V25" fill="none" stroke="{GRID}"/></pattern>
  <marker id="head1" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="4.2" markerHeight="4.2" orient="auto"><path d="M0 0 L10 5 L0 10 Z" fill="{C1}"/></marker>
  <marker id="head2" viewBox="0 0 10 10" refX="6" refY="5" markerWidth="4.2" markerHeight="4.2" orient="auto"><path d="M0 0 L10 5 L0 10 Z" fill="{C2}"/></marker>
</defs>
<rect width="800" height="450" fill="{GROUND}"/>
<rect width="800" height="450" fill="url(#tiles)"/>
<path d="{RIVER}" fill="none" stroke="{WATER}" stroke-width="46" stroke-linecap="round"/>
<path d="{RIVER}" fill="none" stroke="{WATER_EDGE}" stroke-width="2" stroke-dasharray="8 10" opacity=".7"/>
{trees()}
<rect x="352" y="224" width="96" height="116" rx="7" fill="rgba(229,181,59,.16)" stroke="{CAUTION}" stroke-width="4.5"/>
{colony(1, C1, ROAD1, False)}
{colony(2, C2, ROAD2, True)}
<!-- the Trading Post: one half at the end of each colony's road -->
<rect x="366" y="266" width="34" height="64" rx="2" fill="{C1}"/>
<rect x="400" y="266" width="34" height="64" rx="2" fill="{C2}"/>
<path d="M354 271 L400 232 L446 271 Z" fill="{TIMBER}"/>
<line x1="400" y1="271" x2="400" y2="330" stroke="{GROUND}" stroke-width="2.5"/>
<rect x="377" y="306" width="12" height="24" rx="1.5" fill="rgba(0,0,0,.28)"/><rect x="411" y="306" width="12" height="24" rx="1.5" fill="rgba(0,0,0,.28)"/>
<!-- what crosses: logs one way, gears the other -->
<path d="M326 216 Q400 170 474 216" fill="none" stroke="{C1}" stroke-width="4" stroke-linecap="round" marker-end="url(#head1)"/>
<path d="M474 352 Q400 398 326 352" fill="none" stroke="{C2}" stroke-width="4" stroke-linecap="round" marker-end="url(#head2)"/>
<g><rect x="316" y="292" width="22" height="16" rx="4" fill="{C1}" stroke="#fff" stroke-width="2.4"/><path d="M321 300h12" stroke="#fff" stroke-width="1.8"/></g>
<g><circle cx="473" cy="300" r="9" fill="{C2}" stroke="#fff" stroke-width="2.4"/><circle cx="473" cy="300" r="3" fill="#fff"/></g>
</svg>"""

PAGE = f"""<!doctype html><html><head><meta charset="utf-8"><style>
{font(600)}
{font(700)}
html, body {{ margin: 0; width: 800px; height: 450px; overflow: hidden; background: {GROUND}; }}
.scene {{ position: absolute; inset: 0; }}
.sign {{ position: absolute; left: 0; right: 0; top: 24px; display: flex; justify-content: center; }}
.plate {{
  position: relative; background: {NAVY}; color: #fff; border-radius: 8px; text-align: center;
  padding: 20px 46px 21px;
  box-shadow: inset 0 0 0 1px {NAVY_RIM}, inset 0 0 0 6px {NAVY}, inset 0 0 0 8px rgba(255,255,255,.92),
              0 1px 0 rgba(20,35,42,.28), 0 14px 22px -12px rgba(20,35,42,.6);
}}
.plate::before, .plate::after {{
  content: ""; position: absolute; top: 15px; width: 9px; height: 9px; border-radius: 50%;
  background: radial-gradient(circle at 35% 35%, rgba(255,255,255,.55), rgba(0,0,0,.35) 70%);
  box-shadow: inset 0 0 0 1px rgba(0,0,0,.35);
}}
.plate::before {{ left: 15px; }} .plate::after {{ right: 15px; }}
h1 {{ margin: 0; font: 700 74px/.95 'Barlow SC'; letter-spacing: -.005em; }}
p {{ margin: 9px 0 0; font: 600 20px/1 'Barlow SC'; letter-spacing: .15em; text-transform: uppercase; opacity: .92; }}
</style></head><body>
<div class="scene">{SCENE}</div>
<div class="sign"><div class="plate"><h1>Timber Together</h1><p>Build apart. Thrive together.</p></div></div>
</body></html>"""

if __name__ == "__main__":
    with sync_playwright() as p:
        browser = p.chromium.launch(channel="msedge")
        page = browser.new_page(viewport={"width": 800, "height": 450}, device_scale_factor=2)
        page.set_content(PAGE)
        page.evaluate("document.fonts.ready")
        png = page.screenshot(type="png")
        browser.close()
    Image.open(io.BytesIO(png)).convert("RGB").resize((800, 450), Image.LANCZOS).save(OUT, optimize=True)
    print("wrote", OUT, os.path.getsize(OUT), "bytes")
