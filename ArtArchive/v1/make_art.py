#!/usr/bin/env python3
"""
Generates RimWorld textures for the Dried Fish mod.

Style target: vanilla / Vanilla Expanded. Flat cel-shaded fills, a single dark
outline colour, muted desaturated palette, readable at small sizes.

Historical reference: Lofoten hjell. Cod are split, headed, and hung in PAIRS
tied together at the tail, draped over a horizontal pole so the bodies hang
head-down. Racks are open frames of lashed poles, deliberately spaced for wind.
"""
import cairosvg

# --- palette -----------------------------------------------------------------
OUT      = "#33241a"   # universal outline
WOOD     = "#8a6a45"
WOOD_LT  = "#a8845a"
WOOD_DK  = "#63482f"
LASH     = "#c0a271"   # rope lashing
FISH     = "#c6a56b"
FISH_LT  = "#e2ca96"
FISH_DK  = "#9a7c4c"
FISH_CUT = "#efe0bd"   # the split flesh face
SHADOW   = "#1d1409"


def fish_defs(uid, body=FISH, light=FISH_LT, dark=FISH_DK):
    """A unit fish, nose at (0,0), tail at (100,0), ~34 tall. Split-cod profile."""
    path = (
        "M 2,0 "
        "C 10,-13 30,-18 52,-17 "
        "C 70,-16 82,-12 90,-7 "
        "L 100,-16 L 99,0 L 100,16 L 90,7 "
        "C 82,12 70,16 52,17 "
        "C 30,18 10,13 2,0 Z"
    )
    return f"""
  <defs>
    <clipPath id="clip{uid}"><path d="{path}"/></clipPath>
    <g id="fish{uid}">
      <path d="{path}" fill="{body}" stroke="{OUT}" stroke-width="4"
            stroke-linejoin="round"/>
      <g clip-path="url(#clip{uid})">
        <!-- dorsal shadow, hugging the back edge rather than banding -->
        <ellipse cx="52" cy="-30" rx="54" ry="19" fill="{dark}" opacity="0.5"/>
        <!-- belly highlight -->
        <ellipse cx="44" cy="14" rx="38" ry="8" fill="{light}" opacity="0.7"/>
        <!-- the split: stockfish is opened flat down the spine -->
        <path d="M 8,1 C 32,-4 62,-4 92,-1" fill="none" stroke="{FISH_CUT}"
              stroke-width="4" stroke-linecap="round" opacity="0.6"/>
        <path d="M 8,1 C 32,-4 62,-4 92,-1" fill="none" stroke="{OUT}"
              stroke-width="1.6" stroke-linecap="round" opacity="0.5"/>
      </g>
      <path d="{path}" fill="none" stroke="{OUT}" stroke-width="4"
            stroke-linejoin="round"/>
      <path d="M 26,-13 C 30,-4 30,4 26,13" fill="none" stroke="{OUT}"
            stroke-width="2" opacity="0.55" stroke-linecap="round"/>
      <path d="M 93,-11 L 98,-14 M 93,0 L 99,0 M 93,11 L 98,14" stroke="{OUT}"
            stroke-width="1.8" opacity="0.5" stroke-linecap="round"/>
      <circle cx="16" cy="-4" r="2.4" fill="{OUT}"/>
    </g>
  </defs>"""


def hanging_fish(cx, top, length, hw=13):
    """One fish hanging head-down, tail lashed at `top`."""
    bot = top + length
    p = (
        f"M {cx},{top} "
        f"C {cx-hw},{top+length*0.16} {cx-hw},{top+length*0.46} {cx-hw*0.78},{top+length*0.72} "
        f"C {cx-hw*0.55},{bot-4} {cx-hw*0.22},{bot} {cx},{bot} "
        f"C {cx+hw*0.22},{bot} {cx+hw*0.55},{bot-4} {cx+hw*0.78},{top+length*0.72} "
        f"C {cx+hw},{top+length*0.46} {cx+hw},{top+length*0.16} {cx},{top} Z"
    )
    return f"""
    <path d="{p}" fill="{FISH}" stroke="{OUT}" stroke-width="3.5" stroke-linejoin="round"/>
    <path d="M {cx-hw*0.45},{top+length*0.22} C {cx-hw*0.72},{top+length*0.5}
             {cx-hw*0.6},{top+length*0.7} {cx-hw*0.3},{bot-8}"
          fill="none" stroke="{FISH_LT}" stroke-width="5" stroke-linecap="round" opacity="0.75"/>
    <path d="M {cx+hw*0.5},{top+length*0.2} C {cx+hw*0.8},{top+length*0.5}
             {cx+hw*0.66},{top+length*0.74} {cx+hw*0.28},{bot-6}"
          fill="none" stroke="{FISH_DK}" stroke-width="6" stroke-linecap="round" opacity="0.7"/>
    <path d="M {cx},{top+10} L {cx-1},{bot-12}" stroke="{FISH_CUT}" stroke-width="2.6"
          stroke-linecap="round" opacity="0.5"/>
    <circle cx="{cx-4}" cy="{bot-9}" r="2.2" fill="{OUT}"/>"""


def rack_svg():
    """256x256 fish drying rack, 1x1 building."""
    fish = []
    # five fish, staggered lengths so it reads as hand-hung rather than stamped
    for cx, ln in [(52, 128), (90, 142), (128, 132), (166, 145), (204, 126)]:
        fish.append(hanging_fish(cx, 74, ln))

    tails = "".join(
        f'<path d="M {cx-13},52 L {cx},74 L {cx+13},52 L {cx+7},60 L {cx},54 L {cx-7},60 Z" '
        f'fill="{FISH_DK}" stroke="{OUT}" stroke-width="2.6" stroke-linejoin="round"/>'
        for cx in (52, 90, 128, 166, 204)
    )

    lashings = "".join(
        f'<path d="M {cx-11},70 Q {cx},58 {cx+11},70" fill="none" stroke="{LASH}" '
        f'stroke-width="6" stroke-linecap="round"/>'
        f'<path d="M {cx-11},70 Q {cx},58 {cx+11},70" fill="none" stroke="{OUT}" '
        f'stroke-width="1.8" stroke-linecap="round" opacity="0.6"/>'
        for cx in (52, 90, 128, 166, 204)
    )

    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <ellipse cx="128" cy="234" rx="104" ry="15" fill="{SHADOW}" opacity="0.28"/>

  <!-- rear brace pole, sits behind the fish for depth -->
  <path d="M 30,168 L 226,162" stroke="{WOOD_DK}" stroke-width="9" stroke-linecap="round"/>

  <!-- splayed uprights -->
  <path d="M 44,44 L 62,44 L 48,236 L 24,236 Z" fill="{WOOD}" stroke="{OUT}"
        stroke-width="4" stroke-linejoin="round"/>
  <path d="M 46,48 L 54,48 L 42,232 L 32,232 Z" fill="{WOOD_LT}" opacity="0.55"/>
  <path d="M 194,44 L 212,44 L 232,236 L 208,236 Z" fill="{WOOD}" stroke="{OUT}"
        stroke-width="4" stroke-linejoin="round"/>
  <path d="M 204,48 L 212,48 L 228,232 L 218,232 Z" fill="{WOOD_DK}" opacity="0.5"/>

  <!-- top pole -->
  <rect x="16" y="48" width="224" height="20" rx="10" fill="{WOOD}"
        stroke="{OUT}" stroke-width="4"/>
  <rect x="24" y="52" width="208" height="6" rx="3" fill="{WOOD_LT}" opacity="0.7"/>
  <path d="M 70,64 L 96,64 M 140,64 L 172,64" stroke="{WOOD_DK}" stroke-width="2.5"
        opacity="0.6" stroke-linecap="round"/>

  {tails}
  {lashings}
  {"".join(fish)}
</svg>"""


def item_svg(count):
    """128x128 dried fish. count drives the _a/_b/_c stack variants."""
    layouts = {
        1: [(20, 74, 0.86, -8)],
        2: [(16, 86, 0.80, -6), (26, 56, 0.80, 7)],
        3: [(12, 96, 0.72, -4), (20, 70, 0.72, 6), (30, 44, 0.72, -9)],
    }[count]

    uses = []
    for x, y, s, rot in layouts:
        uses.append(
            f'<g transform="translate({x},{y}) rotate({rot}) scale({s})">'
            f'<use href="#fishI"/></g>'
        )

    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">
  {fish_defs("I")}
  <ellipse cx="64" cy="104" rx="46" ry="10" fill="{SHADOW}" opacity="0.22"/>
  {"".join(uses)}
</svg>"""


TARGETS = [
    ("../../Textures/Things/Building/FishDryingRack/FishDryingRack.png", rack_svg(), 256),
    # NOTE: Graphic_StackCount extends Graphic_Collection, which loads every
    # texture in a FOLDER. So texPath in the def is a directory, and these three
    # files must live inside it -- not as siblings with a shared prefix.
    ("../../Textures/Things/Item/Resource/DriedFish/DriedFish_a.png", item_svg(1), 128),
    ("../../Textures/Things/Item/Resource/DriedFish/DriedFish_b.png", item_svg(2), 128),
    ("../../Textures/Things/Item/Resource/DriedFish/DriedFish_c.png", item_svg(3), 128),
]

for path, svg, size in TARGETS:
    cairosvg.svg2png(bytestring=svg.encode(), write_to=path,
                     output_width=size, output_height=size)
    print("wrote", path)
