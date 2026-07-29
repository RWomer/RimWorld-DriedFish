#!/usr/bin/env python3
"""
Dried Fish textures, v2.

Direction: the rack takes any fish, so don't depict whole fish. Hanging FILLETS
on a multi-row A-frame; item stacks are generic dried fillets, not fish shapes.

Adapted from the concept art for RimWorld's constraints:
  - 3 rows kept, but 5 fillets per row instead of ~10. At a 1x1 building the
    texture displays around 64px, so shapes below ~24px of texture turn to mush.
  - A-frame kept in near-side-on view; vanilla buildings read fine this way.
  - Chunkier fillets with heavier outlines so the silhouette survives downscaling.
"""
import cairosvg

OUT      = "#33241a"
WOOD     = "#8a6a45"
WOOD_LT  = "#a8845a"
WOOD_DK  = "#63482f"
SHADOW   = "#1d1409"

# Deliberately varied so a rack doesn't look stamped out
FILLETS = ["#d8c49a", "#c6a97a", "#e2d2aa", "#bd9e६e".replace("६", "6"),
           "#d0b98c", "#c9b085", "#dcc9a1"]
F_EDGE   = "#a98a5d"
F_LIGHT  = "#efe3c4"
F_DARK   = "#9d7f52"


def fillet_hanging(cx, top, h, hw, fill, seed=0):
    """A split fillet hung by its narrow end. Wavy, irregular, tapering."""
    skew = (-1) ** seed * (hw * 0.10)
    p = (
        f"M {cx},{top} "
        f"C {cx-hw*0.85+skew},{top+h*0.16} {cx-hw+skew},{top+h*0.40} {cx-hw*0.62+skew},{top+h*0.66} "
        f"C {cx-hw*0.42+skew},{top+h*0.83} {cx-hw*0.18},{top+h*0.94} {cx},{top+h} "
        f"C {cx+hw*0.18},{top+h*0.94} {cx+hw*0.44+skew},{top+h*0.83} {cx+hw*0.64+skew},{top+h*0.66} "
        f"C {cx+hw+skew},{top+h*0.40} {cx+hw*0.87+skew},{top+h*0.16} {cx},{top} Z"
    )
    grain = "".join(
        f'<path d="M {cx-hw*0.34},{top+h*(0.30+0.17*i)} Q {cx},{top+h*(0.26+0.17*i)} '
        f'{cx+hw*0.34},{top+h*(0.31+0.17*i)}" fill="none" stroke="{F_DARK}" '
        f'stroke-width="1.6" opacity="0.45" stroke-linecap="round"/>'
        for i in range(3)
    )
    return f"""
    <path d="{p}" fill="{fill}" stroke="{OUT}" stroke-width="3.2" stroke-linejoin="round"/>
    <path d="M {cx-hw*0.30},{top+h*0.18} C {cx-hw*0.52},{top+h*0.45}
             {cx-hw*0.40},{top+h*0.68} {cx-hw*0.16},{top+h*0.88}"
          fill="none" stroke="{F_LIGHT}" stroke-width="4" opacity="0.5" stroke-linecap="round"/>
    <path d="M {cx},{top+h*0.12} L {cx},{top+h*0.86}" stroke="{F_DARK}"
          stroke-width="1.8" opacity="0.5" stroke-linecap="round"/>
    {grain}
    <path d="M {cx-4},{top+2} Q {cx},{top-5} {cx+4},{top+2}" fill="none"
          stroke="{OUT}" stroke-width="2.4" stroke-linecap="round"/>"""


def rail(y, x0, x1, drop=0):
    return f"""
    <path d="M {x0},{y} L {x1},{y+drop}" stroke="{OUT}" stroke-width="11"
          stroke-linecap="round"/>
    <path d="M {x0},{y} L {x1},{y+drop}" stroke="{WOOD}" stroke-width="7"
          stroke-linecap="round"/>
    <path d="M {x0+6},{y-2} L {x1-6},{y+drop-2}" stroke="{WOOD_LT}" stroke-width="2"
          stroke-linecap="round" opacity="0.65"/>"""


def leg(x_apex, y_apex, x_foot, y_foot, w=9, shade=WOOD):
    return f"""
    <path d="M {x_apex},{y_apex} L {x_foot},{y_foot}" stroke="{OUT}"
          stroke-width="{w+5}" stroke-linecap="round"/>
    <path d="M {x_apex},{y_apex} L {x_foot},{y_foot}" stroke="{shade}"
          stroke-width="{w}" stroke-linecap="round"/>"""


def rack_svg():
    rows = [(78, 46), (140, 46), (202, 40)]      # (rail y, fillet length)
    xs = [66, 100, 132, 164, 194]
    body = []
    for ri, (ry, fh) in enumerate(rows):
        body.append(rail(ry, 40, 220, drop=-4))
        for i, cx in enumerate(xs):
            if ri == 2 and i == 4:
                continue                          # ragged edge, hand-hung look
            col = FILLETS[(i + ri * 3) % len(FILLETS)]
            body.append(fillet_hanging(cx, ry + 4, fh, 15, col, seed=i + ri))

    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <ellipse cx="128" cy="242" rx="106" ry="12" fill="{SHADOW}" opacity="0.26"/>

  <!-- rear legs of each A, darker for depth -->
  {leg(58, 44, 96, 236, 8, WOOD_DK)}
  {leg(200, 44, 236, 236, 8, WOOD_DK)}

  <!-- front legs -->
  {leg(50, 40, 20, 238, 10)}
  {leg(50, 40, 84, 238, 10)}
  {leg(206, 40, 176, 238, 10)}
  {leg(206, 40, 240, 238, 10)}

  <!-- A-frame tie beams -->
  <path d="M 30,196 L 76,196" stroke="{OUT}" stroke-width="12" stroke-linecap="round"/>
  <path d="M 30,196 L 76,196" stroke="{WOOD_DK}" stroke-width="8" stroke-linecap="round"/>
  <path d="M 184,196 L 232,196" stroke="{OUT}" stroke-width="12" stroke-linecap="round"/>
  <path d="M 184,196 L 232,196" stroke="{WOOD_DK}" stroke-width="8" stroke-linecap="round"/>

  {"".join(body)}
</svg>"""


def fillet_flat(cx, cy, w, h, fill, rot=0, flip=1):
    """A fillet lying flat: irregular, one tapered end, wavy edge."""
    p = (
        f"M {-w*0.5},{h*0.06} "
        f"C {-w*0.42},{-h*0.34} {-w*0.10},{-h*0.52} {w*0.10},{-h*0.46} "
        f"C {w*0.30},{-h*0.40} {w*0.40},{-h*0.22} {w*0.50},{-h*0.10} "
        f"C {w*0.42},{h*0.02} {w*0.44},{h*0.16} {w*0.30},{h*0.30} "
        f"C {w*0.08},{h*0.50} {-w*0.22},{h*0.48} {-w*0.38},{h*0.30} "
        f"C {-w*0.48},{h*0.20} {-w*0.52},{h*0.14} {-w*0.5},{h*0.06} Z"
    )
    return f"""
  <g transform="translate({cx},{cy}) rotate({rot}) scale({flip},1)">
    <path d="{p}" fill="{fill}" stroke="{OUT}" stroke-width="3.4" stroke-linejoin="round"/>
    <path d="M {-w*0.34},{-h*0.04} C {-w*0.10},{-h*0.20} {w*0.14},{-h*0.16} {w*0.34},{-h*0.02}"
          fill="none" stroke="{F_LIGHT}" stroke-width="4.5" opacity="0.45" stroke-linecap="round"/>
    <path d="M {-w*0.30},{h*0.10} C {-w*0.06},{h*0.02} {w*0.16},{h*0.04} {w*0.32},{h*0.14}"
          fill="none" stroke="{F_DARK}" stroke-width="2" opacity="0.5" stroke-linecap="round"/>
    <path d="M {-w*0.18},{-h*0.26} C {-w*0.02},{-h*0.30} {w*0.10},{-h*0.26} {w*0.20},{-h*0.18}"
          fill="none" stroke="{F_DARK}" stroke-width="1.7" opacity="0.4" stroke-linecap="round"/>
  </g>"""


def item_svg(kind):
    if kind == "a":
        parts = [fillet_flat(64, 68, 78, 46, FILLETS[0], rot=-8)]
    elif kind == "b":
        parts = [
            fillet_flat(60, 82, 74, 42, FILLETS[3], rot=4),
            fillet_flat(68, 68, 74, 42, FILLETS[1], rot=-6, flip=-1),
            fillet_flat(62, 54, 72, 40, FILLETS[2], rot=9),
        ]
    else:
        parts = []
        cols = [FILLETS[3], FILLETS[1], FILLETS[5], FILLETS[0], FILLETS[4],
                FILLETS[2], FILLETS[6], FILLETS[0]]
        for i, col in enumerate(cols):
            parts.append(fillet_flat(
                62 + (i % 2) * 5 - 2, 92 - i * 8.5, 76, 42, col,
                rot=(-7 if i % 2 else 6), flip=(-1 if i % 3 == 0 else 1)))
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">
  <ellipse cx="64" cy="106" rx="44" ry="9" fill="{SHADOW}" opacity="0.22"/>
  {"".join(parts)}
</svg>"""


TARGETS = [
    ("../../Textures/Things/Building/FishDryingRack/FishDryingRack.png", rack_svg(), 256),
    ("../../Textures/Things/Item/Resource/DriedFish/DriedFish_a.png", item_svg("a"), 128),
    ("../../Textures/Things/Item/Resource/DriedFish/DriedFish_b.png", item_svg("b"), 128),
    ("../../Textures/Things/Item/Resource/DriedFish/DriedFish_c.png", item_svg("c"), 128),
]

for path, svg, size in TARGETS:
    cairosvg.svg2png(bytestring=svg.encode(), write_to=path,
                     output_width=size, output_height=size)
    print("wrote", path)
