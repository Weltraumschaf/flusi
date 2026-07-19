# Minimap Land/Sea Texture — Design

Amends `docs/Specification.md` §7.3 (Cockpit panel) and the `MinimapProjection`
maths it describes. Replaces the minimap's plain black panel with a stylized
land/sea background, and fixes a stale world-size bug found while scoping
this work.

- **Date:** 2026-07-19.
- **Status:** approved, pending implementation plan.

---

## 1. Motivation

The minimap (`Assets/Scripts/World/Minimap.cs`) currently draws only the
plane blip and POI markers on a flat black translucent panel — it gives no
sense of where land, sea, or rivers actually are. The owner wants land shown
in green and sea in blue, like a real simplified map.

While scoping this, a real correctness bug surfaced:
`Minimap.worldSizeMetres` is a single square value (`10000`), left over from
before the world was resized to a rectangular 22000×12000m two-island layout
(`docs/superpowers/specs/2026-07-18-two-island-world-design.md`). Anything
near the true world's edges (well inside 11000 or 6000 from centre, but
outside the stale 5000 half-extent) already clamps to the minimap border
incorrectly. This is fixed as part of this work so the new background and
the existing POI/plane dots share one correct, rectangular world mapping.

---

## 2. Scope

- **In scope:**
  - Fix `MinimapProjection`/`Minimap` to use the real rectangular world
    extent instead of a stale square `worldSizeMetres`.
  - Generate a static, baked-once background texture showing land (green)
    vs sea (blue), including the two rivers.
  - Fit that texture inside the (roughly square) minimap panel preserving
    the world's true 22:12 aspect ratio (letterboxed), instead of stretching.
- **Out of scope:** real terrain textures/colors on the minimap (sand/grass/
  rock detail — flat 2-tone only), a live top-down camera/RenderTexture,
  dynamic/runtime terrain changes (world is static), POI marker or plane
  blip visual changes (unaffected, draw on top as today).

---

## 3. World extent fix

Replace `Minimap`'s single `worldSizeMetres` field with the real world
bounds, matching the live terrain (`IslandTerrain`: position `(-11000, 0,
-6000)`, size `(22000, 900, 12000)`) — world XZ spans `(-11000, -6000)` to
`(11000, 6000)`.

`MinimapProjection.WorldToNormalized` changes signature from
`(Vector2 worldXZ, float worldSizeMetres)` to
`(Vector2 worldXZ, Vector2 worldMin, Vector2 worldMax)`, normalizing each
axis independently against its own extent. `Minimap` gets `worldMin`/
`worldMax` serialized `Vector2` fields (matching the terrain's own position/
size directly, so they can be copy-typed from the live terrain values rather
than back-derived) replacing `worldSizeMetres`. Existing
`MinimapProjectionTests` are updated for the new signature — same test
intent (centre → 0.5, corner → ~1, out-of-bounds → clamp), now against a
rectangular extent.

---

## 4. Land/sea texture generation

New pure static class `MinimapTerrainTexture` (`Assets/Scripts/World/`,
alongside `MinimapProjection`), following this project's established
pattern of pure static maths behind a thin `MonoBehaviour`
(`docs/Specification.md` §7.3):

```csharp
static Color32[] Build(int width, int height,
                        Func<float, float, float> terrainHeightAt,
                        float seaLevel,
                        IReadOnlyList<Vector2[]> riverPolygons,
                        Vector2 worldMin, Vector2 worldMax)
```

For each pixel: map back to world XZ, then
`isWater = terrainHeight <= seaLevel || InsidePolygon(xz, anyRiverPolygon)`;
color blue if water, green if land. `InsidePolygon` is a standard ray-casting
point-in-polygon test — pure 2D geometry, no Unity dependency, unit-testable
with a hardcoded square/triangle.

**Sea level:** the `Sea` mesh sits at world `y = 5`; terrain XZ where
`GetInterpolatedHeight` is at or below that is ocean. Confirmed live: the
terrain dips below `y = 5` exactly where the ocean surrounds the two
islands, so this single global threshold is sufficient for the sea (the
`Sea` mesh itself is not needed as an input).

**Rivers:** confirmed each river (`RiverA`, `RiverB`) is a simple ribbon mesh
— ordered left/right-bank vertex pairs walking the river's path (8–10 verts
total). Their bed sits a few metres below their own water surface, and that
surface height varies from ~8m near the coast to ~646m inland — far above
the global sea-level threshold, so rivers cannot be caught by height alone
and need their own footprint. A thin (non-pure, but branch-free) helper
walks `mesh.vertices` in pairs, projects to XZ, and closes them into one
boundary polygon (down one bank, back up the other) for the point-in-polygon
test above.

---

## 5. Texture generation & fit

New component `MinimapTerrainRenderer`, `Awake`/`Start`: samples
`Terrain.activeTerrain.terrainData.GetInterpolatedHeight` on a grid sized to
preserve the world's true 22000:12000 (11:6) aspect ratio (e.g. 256×140),
builds the `Color32[]` via `MinimapTerrainTexture.Build`, and applies it to a
`Texture2D` set as the sprite on a new background `Image`, inserted as the
first (bottom) sibling under the `Minimap` panel so the plane blip and POI
markers keep drawing on top.

Baked once at startup — the terrain is static, so no per-frame or per-toggle
regeneration is needed.

**Letterboxing:** the background `Image`'s `RectTransform` is sized to the
correct 11:6 aspect and centred within the (roughly square) `Minimap`
panel's bounds, rather than stretched to fill it — matching the world's true
proportions (islands/rivers keep correct shape) at the cost of a thin empty
strip top/bottom.

---

## 6. Colors

Flat, stylized, no shading/texture detail: a mid green for land, a mid blue
for sea/rivers — matching "blue and green" as asked, and the simple/legible
aesthetic already established for the cockpit gauges and terrain layers.
Existing POI dots and the plane blip are unaffected.

---

## 7. Testing

- `MinimapTerrainTextureTests` (EditMode, new): hardcoded height functions
  (e.g. a step function simulating an island) plus a synthetic square/
  triangle "river" polygon, asserting known pixels come out land vs water;
  an aspect/letterbox-math check for the fit-inside-panel calculation.
- `MinimapProjectionTests` (existing, updated): same three cases (centre,
  corner, out-of-bounds clamp) against the new rectangular-extent signature.
- No new PlayMode tests — this is static bake-once content generated from
  data already present in the scene, not per-frame logic.
- Manual/visual verification: screenshot or Editor check confirming the two
  islands read as green against blue sea, both rivers are visible, and the
  plane blip / POI markers still land in the correct place relative to the
  new background.
