# Building Textures — Design

Amends `docs/Specification.md` §5 (World). Adds real generated textures to
the placeholder box buildings, replacing the current flat untextured gray.

- **Date:** 2026-07-19.
- **Status:** approved, pending implementation plan.

---

## 1. Motivation

The two-island world (`docs/superpowers/specs/2026-07-18-two-island-world-design.md`)
placed 13 skyscraper boxes in `CityA_Skytown` and 6 town-building boxes in
`TownB_Millbrook`, all `GameObject.CreatePrimitive(PrimitiveType.Cube)` with
Unity's default material (flat gray). The landscape-textures pass
(`docs/superpowers/specs/2026-07-18-landscape-textures-design.md`)
explicitly punted on building textures ("stay flat primitive color"). This
pass closes that gap.

---

## 2. Scope

- **In scope:** generated diffuse textures for both building clusters,
  wrapped in `Material` assets, randomly assigned per building for visual
  variety.
- **Out of scope:** new geometry (pitched roofs, house silhouettes,
  balconies), per-face materials/UV unwrapping, reflection
  probes/real-time reflections, custom shaders, animated windows/lights.
- **No gameplay code changes.** Visual only.

---

## 3. Art style

Same bar as the landscape pass: simple/stylized, not photorealistic PBR —
flat-ish, clean, tileable diffuse textures that read correctly as "glass
skyscraper" / "small building" from a flying plane at a glance. A touch of
extra realism is allowed only where it's cheap: a Smoothness bump on the
skyscraper material for a glassy catch-the-light look, no reflection probes
or custom shader work needed to get it.

---

## 4. Skyscrapers — `CityA_Skytown` (13 boxes)

- 3 generated tileable glass curtain-wall diffuse textures: distinct tint
  families (e.g. blue-teal, green, bronze), each with a mullion/window-grid
  pattern.
- 3 URP/Lit `Material` assets, one per texture, Smoothness raised (roughly
  0.6-0.7) for a glassy sheen. Metallic stays low/default — this is a cheap
  sheen cue, not a real reflective glass shader.
- Same texture covers all 6 box faces via the primitive's default UVs
  (top face reads as glass too) — accepted, matching how the box geometry
  itself is already a simplification.

---

## 5. Town buildings — `TownB_Millbrook` (6 boxes)

- 2-3 generated tileable small-building diffuse textures: painted
  stucco/brick wall with a window grid, a couple of distinct color
  families so the cluster doesn't read as one repeated building.
- 2-3 matching URP/Lit `Material` assets, default Smoothness/Metallic (no
  glass sheen — these are walls, not glass).

---

## 6. Assignment

One throwaway `Unity_RunCommand` script (not a checked-in gameplay script,
same pattern as the terrain-sculpting and city-placement scripts already
used in this project):

- Seeded `System.Random` (reproducible, same convention as the existing
  city-gen code's `new System.Random(12345)` / `new System.Random(54321)`).
- Walks `CityA_Skytown`'s children, sets each `MeshRenderer.sharedMaterial`
  to one of the 3 skyscraper materials, picked at random.
- Per-building tiling correction (added during implementation, not in this
  spec's original scope — see the implementation plan's Task 3): the
  window-grid texture is stretched on tall boxes unless tiling scales with
  the box's height/width ratio. The initial approach (`MaterialPropertyBlock`
  override, keeping all buildings pointed at the 6 shared `Material` assets)
  turned out not to work — `MaterialPropertyBlock` is pure runtime `Renderer`
  state that Unity never serializes into `.unity` scene files, so the
  correction silently vanished on scene reload. Fixed by giving each
  building its own `Object.Instantiate()`d `Material` (cloned from its
  cluster's chosen variant) with the tiling baked into that instance.
  **Consequence: the 6 base `.mat` assets under `Assets/Materials/` are no
  longer referenced by any building** — they're templates the per-building
  clones were made from, not live materials. Editing a base `.mat` (tint,
  smoothness, texture) has no effect on the scene; regenerate/reassign per
  building instead.
- Walks `TownB_Millbrook`'s children, sets each `MeshRenderer.sharedMaterial`
  to one of the 2-3 town-building materials, picked at random.
- `MarkSceneDirty` + `SaveScene` at the end so the assignment lands on disk.

---

## 7. Generation pipeline

Same tooling as the landscape pass (`Unity_AssetGeneration_GenerateAsset`,
`GenerateMaterial`/`GenerateImage`-style commands — exact command chosen
during implementation based on what best produces a tileable diffuse
texture). Per this project's established asset-gen gotchas (`CLAUDE.md`):

- Generation requires explicit user consent (blocking, no-ETA operation)
  before the first call in a session.
- Regenerating in place needs the save-to-temp-then-copy-then-trash dance;
  it does not overwrite an existing file at `savePath`.
- Verify each texture actually tiles reasonably at building scale (box
  faces are 35-150 world units) — check visually, not just that generation
  succeeded.
- Verify generated PNGs before use: no guaranteed real alpha channel is
  needed here (opaque diffuse textures), but sample actual pixel content
  rather than trusting the Read-tool preview if anything looks off.

---

## 8. Testing

- No pure-logic code changes, so no new EditMode/PlayMode tests — same
  reasoning as the landscape-textures pass.
- Manual/visual verification: screenshot or Editor visual check confirming
  skyscrapers read as glass-toned and distinct from each other, town
  buildings read as walled/windowed and distinct from each other, and
  neither cluster is flat gray anymore.
