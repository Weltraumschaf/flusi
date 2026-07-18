# Two-Island World — Design

Amends `docs/Specification.md` §5 (World). Replaces the current single-island
layout with two distinct islands separated by open water, within the same
float-precision-safe budget the spec already allots.

- **Date:** 2026-07-18.
- **Status:** approved, pending implementation plan.

---

## 1. Motivation

The MVP (flight model, cockpit panel, minimap, one placeholder island) is done.
The son currently likes Zeppelins, so the placeholder aircraft shape is fine as
is — no need to source a real Concorde model yet. The next highest-value
work is the world itself: two large islands, each with an airport, a city
with skyscrapers, a mountain, and a river, giving pillar 3 ("a world worth
exploring", §1) more to actually explore.

Aircraft-model realism is deferred, not dropped — see the post-MVP list in
`docs/Specification.md` §8.

---

## 2. Scope

- **In scope:** re-sculpt the terrain into two islands, add city/mountain/river
  content to each, reposition the existing POI markers (`Airport`, `Landmark`)
  onto the new layout.
- **Out of scope (this pass):** real Asset Store art (buildings/mountains stay
  primitive placeholders, matching `docs/Specification.md` §5.1's "placeholders
  first" rule), audio, extra cameras, "Nice landing!", gamepad input, real
  aircraft model.
- **No code changes expected.** This is scene/terrain content work, done with
  throwaway `Unity_RunCommand` scripts per the project's established pattern —
  nothing new lands in `Assets/Scripts`. `Airport`, `Landmark`, and
  `PointOfInterestRegistry` are reused unchanged.

---

## 3. World size

Single `Terrain` object (current architecture, no floating-origin, no
terrain tiling/streaming — those stay explicit non-goals per
`docs/Specification.md` §9/§10).

- Grows from the current 10km × 10km (`size = (10000, 600, 10000)`) to
  **22km × 12km** (`size ≈ (22000, 900, 12000)`), still centered on world
  origin.
- Max radius from origin ≈ 12.5km — well inside the ~50km float-precision-safe
  radius the spec already reserves (§5). This is *not* the "grow the world"
  option that was explicitly declined during design discussion (that meant
  breaching the 50km radius and reopening floating-origin/streaming, §10) — it
  is filling more of the 80–100km budget the spec already allotted but the
  current 10km terrain never used.

---

## 4. Island layout

Two islands, side by side, separated by an open-water channel (~4km),
distinct character rather than symmetric:

### 4.1 Island A — "city island" (west, center ≈ x = -6000)

- Skyscraper cluster near the coast: ~12–15 box placeholders, heights
  30–150m, loosely gridded with jitter.
- 1 airport: flat coastal zone (flattened heightmap patch, same technique the
  current airport already uses).
- 1 modest mountain, inland, peak ≈ 350m.
- 1 river: mountain peak → nearest (west) coast, flat water-plane strip
  (not a carved heightmap channel — avoids interacting with the terrain-height
  ground-contact clamp, `docs/Specification.md` §6), height-sampled along its
  path so it sits on the sculpted terrain rather than floating or clipping.

### 4.2 Island B — "mountain island" (east, center ≈ x = +6000, larger footprint)

- Tall mountain centerpiece, peak ≈ 600m.
- 1 airport: flat coastal zone.
- Small town near the airport: ~6 low box placeholders, heights 10–30m.
- 1 river: mountain peak → nearest (east) coast, same flat-strip technique.

Exact coordinates, radii, and heightmap falloff curves are tuned live in the
Editor during implementation, not fixed in advance — the numbers above are
targets, not final values.

---

## 5. Points of interest

6 total, all through the existing `Airport`/`Landmark` components
(self-registering into `PointOfInterestRegistry`, read by `Minimap` — no code
changes):

| Kind     | Island | Label (placeholder, user may rename) |
| -------- | ------ | ------------------------------------- |
| Airport  | A      | "Westport"                            |
| Airport  | B      | "Eastfield"                           |
| Landmark | A      | mountain peak                         |
| Landmark | A      | city cluster                          |
| Landmark | B      | mountain peak                         |
| Landmark | B      | town                                  |

The current single-island POIs (one airport, two landmarks at the old
coordinates) are repositioned/replaced onto the new layout, not kept alongside
it.

---

## 6. Testing

- No new pure-logic code, so no new EditMode tests (nothing to unit-test —
  this is terrain/scene content).
- PlayMode smoke test: scene loads, plane flies from Island A across the water
  channel to Island B without errors, the existing terrain-height
  ground-contact clamp (`docs/Specification.md` §6) still holds over both
  islands' sculpted terrain.
- Manual playtest: fly both islands, confirm the minimap shows all 6 POIs at
  plausible positions, confirm the river strips read visually as rivers (not
  floating or clipped through terrain).

---

## 7. Risks / gotchas to watch

- Flattening zones for airports/city footprints must not fight the mountain's
  radial falloff — flatten *after* the base island shape, same order the
  original single-mountain terrain used.
- River strip height-sampling must run after all heightmap sculpting is
  finalized, or the strip will sit at stale heights.
- Building placeholders must avoid the flattened airport runway zone and the
  river path so nothing clips.
