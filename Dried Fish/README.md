# Dried Fish

A standalone fish drying rack for RimWorld 1.6 — the fishing culture's answer to jerky.

Measures its input in **nutrition** rather than item count, so it values every fish
correctly: vanilla, modded, and the double-nutrition "adept catches" from Vanilla
Ideology Expanded. Drying is time-based and responds to temperature, wind, rain and
crowding, modelled on North Atlantic stockfish.

No Harmony patching. No Vanilla Expanded Framework dependency.

**Status: verified in-game.** See the test log at the bottom.

---

## Why this isn't a Vanilla Expanded processor

The obvious approach was a second process on VFE Classical's meat drying rack. Two of
the four design requirements aren't supported by that framework.

**Nutrition-based input is refused by design.** `PipeSystem.ProcessDef.Ingredient` has
a `nutritionGetter` flag, but `ConfigErrors` rejects combining it with a category:

> ProcessDef has ingredients that look for a category and try to grab amount by nutrition. This is not possible right now.

The reason is structural. `nutritionGetter` converts a nutrition requirement into a
fixed item count *at load time* by dividing by that one ThingDef's nutrition. A
category has many nutrition values, so there is no single count to precompute. Making
it work means changing `ThingAndResourceOwner` from an int count to a float nutrition
accumulator, which is saved game state.

**Temperature is dead code.** `Process.FactorIfAcceleratingProcess()` exists, carries
the docstring "Not implemented at the moment," and is called from nowhere in
VanillaExpandedFramework. `VFEC_DryMeat` sets `minAccelerationTemp: 28` and the meat
rack's description promises that warmth speeds it up. Neither does anything.

There is also a genuine bug worth knowing about: `WorkGiver_BringToProcessor` builds
its haul list from `ThingCategoryDef.childThingDefs`, which is **direct children
only**, where every other acceptance check in PipeSystem uses `IsWithinCategory` and
walks the parent chain. Any modded meat filed under a *sub-category* of `MeatRaw` — as
VFE Fishing's fish are — is therefore accepted by the processor but never hauled to it.
The correct call is `DescendantThingDefs`. The pre-1.6 `ItemProcessor` system has the
identical bug in `WorkGiver_InsertProcessorFirst`, which is why this has been broken
across versions.

So this mod follows vanilla's fermenting barrel pattern instead: a building that
accumulates input, advances on a timer, and is modified by ambient conditions. That
pattern natively supports everything required and doesn't break when VEF updates.

---

## How it works

Haul fish to the rack. It accumulates **nutrition**, not item count. Acceptance uses
`ThingDef.IsWithinCategory`, which walks the category tree, so modded fish in their own
sub-categories work with no patch.

- **Capacity:** 3.75 nutrition — the same as the 75 raw meat in a meat rack batch.
- **Yield:** 20 dried fish per nutrition, so a full batch is 75. Identical conversion
  ratio to jerky.
- **Time:** 15 days at ideal conditions.
- **Topping up** pulls progress back proportionally, so a nearly-finished rack can't be
  used to launder a fresh batch through.
- The rack shows an **empty** or **loaded** texture depending on its contents.

### Temperature

Modelled on North Atlantic stockfish. The Lofoten drying season runs February to May
for a specific reason: just above freezing is cold enough that bacteria lose the race
against dehydration, but warm enough that the fish isn't locked solid.

| Temperature | Speed |
|---|---|
| -40 °C | 25% — sublimation only |
| -20 °C | 55% |
| -5 to 8 °C | 100% — the Lofoten window |
| 15 °C | 70% |
| 22 °C | 35% |
| 30 °C and above | 10% |

### Wind — outdoor racks only

Wind is what actually dries fish on a hjell, and RimWorld models it: map-wide, wandering
between 0% and 150% inside a range set by the current weather. Fog uses a 0.5 factor,
rain 1.5, thunderstorms 1.5 plus a 1.25 offset.

| Wind | Speed | Days at ideal temp |
|---|---|---|
| 0% (dead calm) | 0.35 | 42.9 |
| 30% | 0.60 | 25.0 |
| 100% | 1.00 | 15.0 |
| 150% (max) | 1.35 | 11.1 |

**A roof cuts wind speed by 25%** before the curve is applied — so in dead calm a roofed
rack dries exactly as fast as an open one (there's no wind to block), and only in a gale
does the shelter cost you anything, bottoming out at 81% of open-air speed.

**Indoor racks get no wind at all.** That's the whole trade: outdoors is faster on
average but swings between 11 and 43 days; indoors is slower and utterly predictable.
Which is precisely why real preservation industries moved inside — consistency, not
speed.

Two interactions fall out of this for free. **Fog** is a double penalty: low wind, and
the damp weather you least want fish hanging in. **Thunderstorms** are a paradox — the
windiest weather in the game, arriving with the rain that halts an uncovered rack. The
roofed shed is the answer to both.

The inspect pane shows the live wind percentage, which vanilla displays nowhere.

### Rain

An unroofed rack stops completely while it rains. Checked **per-cell, not per-room**, so
a bare roof on posts gives rain cover while keeping outdoor airflow. That's a drying
shed, it's what real ones look like, and it's meant to work.

### Spoilage

Above `ruinTemperature` (32 °C) the batch begins to spoil — not instantly, but
accumulating at roughly **1.9 days at 32 °C** and **14 hours at 40 °C**. A caution
message fires once at 3%, and the inspect pane shows the percentage climbing.

**Spoilage does not reverse** by default. Cooling the rack stops it getting worse but
doesn't undo it, which makes a heatwave a real decision: is this batch far enough along
to be worth moving somewhere cold, or do you cut it loose?

Pawns will not haul fish to a rack that is currently over the ruin temperature.

### Crowding — indoor racks only

Temperature alone isn't enough, because a colony with coolers would park racks in the
existing freezer and the whole climate model would go inert. So the second variable is
airflow, and it punishes **crowding rather than walls**.

Indoors, the rack sums the drying nutrition of every rack in its room and divides by the
room's cell count. Only racks *with fish in them* count — butcher tables, shelves,
stored food and empty racks are all free, so sharing a cold workroom costs nothing
beyond rack density itself.

A full rack holds 3.75 nutrition, so load = 3.75 / (cells per rack).

| Situation | Cells per rack | Time | vs. outdoor ideal |
|---|---|---|---|
| Mixed cold workroom w/ butcher, 9×9, 2 racks | 40 | 17 days | 1.17× |
| 11×11 room, 4 racks | 30 | 18 days | 1.22× |
| Grav ship hold, 6×8, 2 racks | 24 | 19 days | 1.29× |
| 11×11 room, 6 racks | 20 | 21 days | 1.37× |
| Dedicated hall, 12×12, 8 racks | 18 | 22 days | 1.44× |
| 1 rack tucked into an existing freezer | 30 | 22 days | 1.44× |
| Cramped grav ship hold, 5×5, 2 racks | 12 | 27 days | 1.80× |
| 4 racks crammed into that freezer | 8 | 56 days | 3.73× |
| 8 racks crammed into that freezer | 4 | 196 days | 13.1× |

The freezer trick works for exactly one rack and collapses the moment you scale it.

### Where this leaves the two structures

A roofed pavilion in a cold biome is the best setup available: full outdoor airflow,
rain cover, no crowding penalty. That's intended. The walled, cooled hall's value was
never airflow — it's **temperature control**. On a tropical or desert map an outdoor
pavilion sits above the ruin threshold and rots every batch no matter how much wind it
gets; only a cooled room works there.

So fish drying starts as a cold-biome technology, and everywhere else earns it with
architecture.

---

## Mod settings

Options → Mod settings → Dried Fish. Settings are read live, so changes apply
immediately to a loaded colony. Note that RimWorld mod settings are **global**, not
per-save.

| Setting | Range | Default |
|---|---|---|
| Drying time | 25–300% | 100% |
| Yield | 50–200% | 100% |
| Temperature impact | 0–200% | 100% |
| Crowding impact | 0–200% | 100% |
| Simulate wind on outdoor racks | on/off | on |
| Rain stops outdoor drying | on/off | on |
| Heat spoilage | Off / Forgiving / Permanent | Permanent |

The two sliders scale their factor toward 1.0 (no effect) — at 0% temperature or
crowding stops mattering entirely. Wind and rain are toggles rather than sliders because
neither is really a matter of degree: wind is variance the player can't influence, and
rain either lands on the fish or it doesn't.

The panel's worked examples are computed live from the actual defs, so they update as
you drag and stay honest if the curves are ever retuned.

Two consequences worth knowing. Turning **rain** off removes the reason to build a roof,
since the roof still costs a little wind — that's accepted, not a bug. Setting
**spoilage to Off** also clears any spoilage already accumulated, so it can rescue a
struggling batch rather than freezing it at its current damage.

---

## Building it

VS Code alone won't compile this. You need:

- **.NET SDK** — `brew install --cask dotnet-sdk`, or from dotnet.microsoft.com.
- **C# Dev Kit** extension (optional, but gives IntelliSense against the RimWorld
  assemblies).

Then:

1. Edit `RimWorldManaged` in `Source/DriedFish/DriedFish.csproj` to point at your
   `Managed` folder. The macOS Steam default is already filled in.
2. `cd Source/DriedFish && dotnet build -c Release`
3. The DLL lands in `Assemblies/`.

The project targets `net472`, which macOS has no reference assemblies for. The
`Microsoft.NETFramework.ReferenceAssemblies` package in the csproj supplies them at
build time, so Mono isn't needed. If you see *"The reference assemblies for
.NETFramework,Version=v4.7.2 were not found"*, that package failed to restore.

**Tip:** symlink the mod folder into RimWorld's `Mods/` directory rather than copying,
so a rebuild is picked up on the next restart.

**Careful with Finder** when copying assets in: dragging a folder onto a matching folder
*replaces* it rather than merging, unlike `cp -R`. This has already eaten
`Source/DriedFish/` once.

---

## Textures

Generated from `Source/art/make_art.py` — SVG authored in code, rasterised with
cairosvg (`pip install cairosvg`) — then hand-revised. The palette constants at the top
are editable text.

**`Graphic_StackCount` extends `Graphic_Collection`, which loads every texture in a
FOLDER.** So `texPath` is a *directory*, not a filename prefix, and the item needs three
variants inside it. Classical stores jerky the same way
(`Textures/Things/Item/Resource/Jerky/jerky_a.png`). Getting this wrong renders the item
as a magenta X.

```
Textures/Things/Building/FishDryingRack/FishDryingRackEmpty.png   256x256
Textures/Things/Building/FishDryingRack/FishDryingRackFull.png    256x256
Textures/Things/Item/Resource/DriedFish/DriedFish_a.png           128x128, single fillet
Textures/Things/Item/Resource/DriedFish/DriedFish_b.png           small stack
Textures/Things/Item/Resource/DriedFish/DriedFish_c.png           large stack
```

Art direction: the rack accepts anything from anchovies to marlin, so nothing depicts a
recognisable fish. Hanging pieces are generic split fillets on a multi-row A-frame; item
stacks are layered slabs seen edge-on. The rack is drawn in a mild top-down perspective
to match the camera. Earlier versions are in `ArtArchive/`.

The empty/loaded swap is handled by `Building_FishDryingRack`, which overrides `Graphic`
and returns `fullGraphicData` when the rack isn't empty. Because buildings are printed
into the map mesh, the comp calls `MapMeshDirty` whenever it crosses the empty/loaded
line — overriding `Graphic` alone would not refresh the display.

---

## Tuning

Item stats live in `Defs/ThingDefs_Items/`. Everything else is on the building's comp,
which is duplicated per Odyssey branch — edit both, or the branch you play.

| Property | Effect |
|---|---|
| `capacityNutrition`, `daysToDry`, `productPerNutrition` | Throughput |
| `temperatureFactorCurve` | Speed by ambient temperature |
| `windFactorCurve` | Speed by wind, outdoors only |
| `roofedWindFactor` | Wind cut by a roof overhead (0.75) |
| `ruinTemperature` | Where spoilage begins |
| `spoilRatePerDay`, `spoilRatePerDegreePerDay` | How fast a hot batch is lost |
| `spoilRecoveryPerDay` | 0 = spoilage is permanent |
| `airflowFactorCurve` | Nutrition per room cell → speed, indoors only |
| `enclosedBaseFactor` | Flat indoor penalty (0.9 = never quite matches sea wind) |
| `rainRatePause` | Rain intensity that halts an unroofed rack |
| `acceptedCategories` | Any ThingCategoryDef; sub-categories included automatically |
| `fullGraphicData` | Texture shown while the rack holds fish |

**SimpleCurve requires a `<points>` wrapper.** A bare `<li>` list silently produces a
curve with zero points, and `Evaluate` on an empty curve returns 0 — which presents as
everything running at 0% speed with no error.

**Patch files use `<Patch>` as the root element, not `<Patches>`.** A `<Patches>` root
makes RimWorld reject the whole document at parse time — every operation inside is
skipped, and the only sign is one line in the startup log: *"Unexpected document element
in patch XML; got Patches, expected 'Patch'"*. This mod currently declares everything
in defs and needs no patches at all.

`FDR_DriedFish` mirrors `VFEC_Jerky`: `foodType: Kibble`, `preferability: MealFine`. It
is a terminal food pawns eat directly, and cooking recipes that want meat will **not**
accept it. To make it an ingredient, change `foodType` to `Meat` and drop
`preferability` to `RawTasty`. If you want the Sushi addon to take it, it also needs to
be in the `Fish` category — but then the rack will try to dry its own output, so keep
the guard in `Accepts()`. That's the same trap that forces `VFEC_DryMeat` to blacklist
`VFEC_Jerky`.

It does **not** mirror jerky on deterioration. Jerky is immortal; dried fish uses a low
`DeteriorationRate` of 0.5/day (survival meals are 0.25, rice is 6). It never rots and
needs no refrigeration, but a stack left uncovered in wet weather will degrade — the
same covered-versus-exposed logic the rack itself runs on. Indoors it lasts forever.

---

## Odyssey handling

`LoadFolders.xml` splits on `Ludeon.RimWorld.Odyssey`, mirroring how VFE Fishing itself
is structured:

- **Odyssey on** — VFE Fishing's fish inherit vanilla `FishBase` and land in the vanilla
  `Fish` category, alongside vanilla's own fish. Adept catches are a *quantity* perk
  (stacks of 10) at a flat 0.25 nutrition. The drying research is gated behind vanilla's
  `Fishing` project.
- **Odyssey off** — fish live in `VCEF_RawFishCategory`, a child of `MeatRaw`. Adept
  catches are genuinely **double nutrition** for their size class. VFE Fishing ships no
  research at all, so the drying research stands alone with no prerequisite.

Both nutrition cases are handled correctly because the rack measures nutrition. A
count-based recipe would get one of them wrong whichever way it was tuned.

The `ResearchProjectDef` is duplicated per branch rather than patched, deliberately: a
`PatchOperation` whose xpath fails to match does so **silently**, whereas a bad def
produces a visible load error.

---

## Known gaps

- **No partial-load graphic.** A rack with one fish looks the same as a full one. A
  third texture would fix it.
- **Finished batches drop to the interaction cell automatically** rather than needing a
  collection job. Simpler, slightly more generous than vanilla's barrel.
- **All-or-nothing spoilage.** A batch at 99% spoilage is still whole; at 100% it's
  gone. Partial loss would be gentler.
- **Outdoor racks have no density check.** A roofed pavilion packed with racks gets full
  speed. Deliberate — a RimWorld cell is a metre or two, and outdoors the moisture is
  gone before it matters.
- **The DEV report gizmo is still in.** Dev-mode only, costs nothing, useful for
  diagnosing category problems in a modded load order.

### Upgrading mid-save

Racks built before the `thingClass` was added stay plain `Building` instances for the
life of that save, because RimWorld resolves the class at spawn. They work fine but
never swap to the loaded texture. Deconstruct and rebuild them once. Fresh installs are
unaffected.

---

## Test log

Verified in-game on RimWorld 1.6 + Odyssey, with Vanilla Fishing Expanded, VFE
Classical, VFE Tribals, VE Cooking, VCE Sushi and VIE Memes and Structures loaded.

| Test | Result |
|---|---|
| Rack builds and appears under Production | Pass |
| Item renders at all three stack sizes | Pass |
| Pawns haul fish to the rack | Pass |
| Nutrition accounting | Pass — 0.50 tuna → 10, 1.25 mackerel → 25 |
| Adept catches valued correctly | Pass — 5 stingray = 5 mackerel under Odyssey |
| Drying progresses over time | Pass |
| Pawns eat dried fish | Pass |
| Temperature response | Pass — 51 °F → 89%, matches the curve exactly |
| Heat spoilage, warning and destruction | Pass — 110 °F spoiled and cleared with alert |
| Save / load mid-batch, mid-spoil | Pass — all scribed fields survived |
| Indoor airflow, 3 racks in 9×9 | Pass — 80% speed, predicted 79.9% |
| Indoor airflow, 3 racks in 3×3 | Pass — 8% speed / 9% airflow, predicted 7.7% / 8.5% |
| Topping up pulls progress back | Pass — 10% + 3 mackerel → 6%, predicted 6.25% |
| Rain pauses an unroofed rack | Pass |
| Wind scales outdoor drying | Pass — 150% wind → 135% speed, matches the curve |
| Mod settings panel | Pass |
| Research gated behind vanilla Fishing | Pass |
| Empty / loaded graphic swap | Pass — on newly built racks |
