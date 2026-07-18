# Landscape Textures — Design

Amends `docs/Specification.md` §5 (World) and §5.1 (Art pipeline). Adds real
generated textures to the terrain and water, replacing the current flat
untextured gray.

- **Date:** 2026-07-18.
- **Status:** approved, pending implementation plan.

---

## 1. Motivation

The two-island world (see
`docs/superpowers/specs/2026-07-18-two-island-world-design.md`) is built and
verified, but the terrain has zero `TerrainLayer`s (flat default gray) and
the water (`Sea`, `RiverA`, `RiverB`) uses URP's plain default `Lit` material
(flat gray, no texture). This pass adds real land and water textures.

This is the first texturing pass for this project — per
`docs/Specification.md` §5.1 ("placeholders first... real models are swapped
in later"), geometry stays placeholder (primitive boxes, procedural
heightmap) but the *surface appearance* of that geometry is no longer
untextured gray.

---

## 2. Scope

- **In scope:** 3 terrain layers (sand, grass, rock) painted onto the
  existing terrain by height/slope; 1 water material shared by `Sea`,
  `RiverA`, `RiverB`.
- **Out of scope:** building/mountain-marker textures (they stay flat
  primitive color), animated water (shader-level waves/flow), snow or dirt
  transition layers, new geometry of any kind.
- **No gameplay code changes.** `FlightModel`, `IAircraftState`, and the
  ground-contact clamp are untouched — this is visual only.

---

## 3. Art style

Simple/stylized, not photorealistic PBR: flat-ish, clean, tileable colors
with light texture detail — readable as "grass" / "sand" / "water" at a
glance from a flying plane, and closer in spirit to the placeholder
primitives (box buildings, simple mountain shapes) than to the more detailed
cockpit gauge art. Normal/smoothness maps are a nice-to-have, not required —
the diffuse texture alone must read correctly on its own.

---

## 4. Land: 3 terrain layers, height/slope blended

Layers, each a generated tileable diffuse texture (`TerrainLayer` asset):

1. **Rock** — dominant wherever local terrain slope is steep (mountain
   flanks and peaks), regardless of elevation.
2. **Sand** — dominant at low elevation with gentle slope (coastlines, and
   the flattened airport/city/town plateaus at 6–10m, which read as
   sandy/beachside — a deliberate, accepted look, not a bug to fix).
3. **Grass** — the fill layer: everywhere that isn't steep (rock) or
   low-and-flat (sand).

Blending is procedural — height and slope sampled per alphamap texel from
the live `TerrainData`, same throwaway-`Unity_RunCommand` pattern already
used to sculpt the terrain (see the two-island plan). No manual painting in
the Editor; the result is reproducible from a script, not hand-drawn.

Exact height/slope thresholds and blend-band widths are tuned during
implementation (visually verified against the actual sculpted terrain), not
fixed in advance — same "tuned live, not fixed" latitude the two-island
design doc used for coordinates.

---

## 5. Water: one shared material

A single generated `Water` material (stylized tileable texture, blue,
simple) replaces the current reference to URP's plain default `Lit.mat` on
all three water surfaces: `Sea`, `RiverA`, `RiverB`. One material, assigned
in three places — not three separate generated textures — matching how `Sea`'s
material was already shared with the rivers when they were first built.

---

## 6. Generation pipeline

Uses this project's existing AI asset-generation tooling (the same pipeline
already used for the cockpit gauge sprites): `TerrainLayer` diffuse textures
via the terrain-layer generation command, the water texture via the
material generation command. Per this project's established asset-gen
gotchas (`CLAUDE.md`):

- Generation requires explicit user consent (a blocking, no-ETA operation)
  before the first call in a session — must be requested and confirmed
  before implementation starts generating anything.
- Regenerating in place needs the save-to-temp-then-copy-then-trash dance;
  it does not overwrite an existing file at `savePath`.
- Verify generated textures actually tile/repeat reasonably at the terrain's
  scale — a texture that looks fine as a single tile can read as an obvious
  repeating grid at real terrain size; check visually, not just that
  generation succeeded.

---

## 7. Testing

- No pure-logic code changes, so no new EditMode/PlayMode tests — same
  reasoning as the two-island plan (this is asset/material content, not
  logic).
- Manual/visual verification: screenshot or Editor visual check confirming
  sand reads at the coast, rock reads on the mountains, grass fills the
  rest, and water reads as water (not flat gray) on both the sea and both
  rivers.
