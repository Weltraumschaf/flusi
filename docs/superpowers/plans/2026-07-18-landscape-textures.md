# Landscape Textures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Texture the two-island terrain (sand/grass/rock, height+slope
blended) and give `Sea`/`RiverA`/`RiverB` a shared generated water material,
replacing today's flat untextured gray.

**Architecture:** Same throwaway-`Unity_RunCommand` scene-editing pattern as
the two-island world plan, plus this project's existing AI asset-generation
tool (`Unity_AssetGeneration_GenerateAsset`) for the actual texture/material
content. No new files under `Assets/Scripts`.

**Tech Stack:** Unity 6000.5.2f1 Editor API (`TerrainLayer`, `TerrainData`
alphamaps, `Material`), the project's asset-generation MCP tool.

## Global Constraints

- Design doc: `docs/superpowers/specs/2026-07-18-landscape-textures-design.md`.
- 3 terrain layers only: Rock (steep slope, any height) → Sand (low
  elevation, gentle slope — including the flattened airport/city/town
  plateaus, a deliberate look) → Grass (fill). No dirt/snow layers.
- One shared `Water` material for `Sea`, `RiverA`, `RiverB` — not three
  separate ones.
- Style: simple/stylized, tileable, NOT photorealistic PBR.
- **Asset generation requires user consent before the FIRST
  `Unity_AssetGeneration_GenerateAsset` call in a session** — send a
  plain-text message listing what will be generated, that it's blocking
  with no ETA, and wait for confirmation before calling the tool. This
  consent gate is per-conversation: a dispatched subagent is a separate
  conversation from the tool's perspective and cannot itself get a reply
  from the real project owner, so **the controller should make every
  `GenerateAsset` call directly** rather than delegating it to an
  implementer subagent, regardless of which execution mode is chosen for
  the rest of this plan.
- `Unity_AssetGeneration_GenerateAsset` does not overwrite an existing file
  at `savePath` — it silently appends `" 1"` instead. Not a concern for this
  plan's first-time generation, but relevant if any step is re-run.
- `Unity_RunCommand` sandbox: `CommandScript` implements `IRunCommand` and
  nothing else, is the only top-level class.
- Before any scene edit: `EditorApplication.isPlaying` must be false. After:
  `EditorSceneManager.MarkSceneDirty` + `SaveScene`.
- `result.Log` ignores format specifiers.
- Commit: ≤50-char imperative subject, body wrapped at 72.

---

### Task 1: Generate the 3 terrain textures and the water texture

**Files:**
- Create: `Assets/Textures/Terrain/Sand.png`, `Assets/Textures/Terrain/Grass.png`, `Assets/Textures/Terrain/Rock.png`
- Create: `Assets/Textures/Water/Water.png`
- (each PNG's paired `.meta` lands automatically on Unity's next import — commit it alongside, per this repo's asset/`.meta` rule)

**Interfaces:**
- Produces: 4 tileable PNG textures at the paths above. Task 2 wraps the
  first 3 into `TerrainLayer` assets; Task 3 wraps the 4th into a `Material`.

This task is **controller-only** (see Global Constraints on the consent
gate) — do not delegate it to an implementer subagent.

- [ ] **Step 1: Get consent, then generate**

Before the first call, send the project owner a plain-text message naming
these 4 assets, noting generation is blocking with no fixed ETA (tens of
seconds to a few minutes per asset, generated in parallel), and wait for
their confirmation on a following turn.

Once confirmed, call `Unity_AssetGeneration_GenerateAsset` 4 times (in
parallel, independent assets) with `command: "GenerateImage"`,
`modelId: "hand-painted-textures-2-0"` (seamless tileable hand-painted
style — matches the design doc's "simple/stylized, not photorealistic"
requirement), `width: 1024`, `height: 1024`, `waitForCompletion: true`:

| savePath | prompt |
| --- | --- |
| `Assets/Textures/Terrain/Sand.png` | "seamless tileable stylized cartoon sand beach texture, warm light tan color, simple flat shading, subtle grain, hand-painted game art style, no photorealism" |
| `Assets/Textures/Terrain/Grass.png` | "seamless tileable stylized cartoon grass texture, vibrant green, simple flat shading, small blade details, hand-painted game art style, no photorealism" |
| `Assets/Textures/Terrain/Rock.png` | "seamless tileable stylized cartoon rocky mountain texture, gray-brown stone, simple flat shading, chunky rock facets, hand-painted game art style, no photorealism" |
| `Assets/Textures/Water/Water.png` | "seamless tileable stylized cartoon water texture, bright blue, simple flat shading, gentle wave ripple pattern, hand-painted game art style, no photorealism" |

- [ ] **Step 2: Verify**

For each of the 4 PNGs: confirm the file exists at its `savePath` and
Unity imported it (check the Console for import errors via
`Unity_GetConsoleLogs`). Visually check each one actually looks like its
name (sand/grass/rock/water, not a broken or off-topic image) and tiles
reasonably (no obvious hard seam) — per this repo's documented asset-gen
gotcha, a generation can silently miss the "seamless/tileable" request, so
don't just trust that generation succeeded.

If a texture doesn't tile well or doesn't read as its material, regenerate
that one asset only (same `savePath` — remember it appends `" 1"` instead
of overwriting, so move/rename or delete the bad one first via
`AssetDatabase.MoveAssetToTrash`, not `File.Delete`, per this repo's asset
rules).

- [ ] **Step 3: Commit**

```bash
git add Assets/Textures/Terrain/Sand.png Assets/Textures/Terrain/Sand.png.meta \
        Assets/Textures/Terrain/Grass.png Assets/Textures/Terrain/Grass.png.meta \
        Assets/Textures/Terrain/Rock.png Assets/Textures/Terrain/Rock.png.meta \
        Assets/Textures/Water/Water.png Assets/Textures/Water/Water.png.meta
git commit -m "$(cat <<'EOF'
Generate sand, grass, rock, and water textures

Stylized tileable textures for the terrain's 3 blended layers and
the shared water material, generated via the project's AI asset
pipeline. Not yet wired into any TerrainLayer or Material.
EOF
)"
```

---

### Task 2: Create terrain layers and paint them by height/slope

**Files:**
- Create: `Assets/Terrain/SandLayer.terrainlayer`, `Assets/Terrain/GrassLayer.terrainlayer`, `Assets/Terrain/RockLayer.terrainlayer`
- Modify: `Assets/Scenes/IslandTerrain.asset` (alphamaps)

**Interfaces:**
- Consumes: the 3 terrain PNGs from Task 1 (`Assets/Textures/Terrain/{Sand,Grass,Rock}.png`).
- Produces: `terrainData.terrainLayers = [Rock, Sand, Grass]` (index order
  matters for the alphamap array below) and a fully painted alphamap.

- [ ] **Step 1: Create the TerrainLayer assets and paint the alphamap**

Run via `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying)
        {
            result.LogError("Exit Play mode before editing the terrain.");
            return;
        }

        var rock = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Terrain/Rock.png"), tileSize = new Vector2(50f, 50f) };
        var sand = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Terrain/Sand.png"), tileSize = new Vector2(50f, 50f) };
        var grass = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Terrain/Grass.png"), tileSize = new Vector2(50f, 50f) };

        AssetDatabase.CreateAsset(rock, "Assets/Terrain/RockLayer.terrainlayer");
        AssetDatabase.CreateAsset(sand, "Assets/Terrain/SandLayer.terrainlayer");
        AssetDatabase.CreateAsset(grass, "Assets/Terrain/GrassLayer.terrainlayer");
        result.RegisterObjectCreation(rock);
        result.RegisterObjectCreation(sand);
        result.RegisterObjectCreation(grass);

        var terrain = GameObject.Find("IslandTerrain").GetComponent<Terrain>();
        var data = terrain.terrainData;
        result.RegisterObjectModification(terrain);
        data.terrainLayers = new[] { rock, sand, grass };

        int alphaRes = data.alphamapResolution;
        int heightRes = data.heightmapResolution;
        var alphamaps = new float[alphaRes, alphaRes, 3];

        const float rockSlopeStart = 25f, rockSlopeFull = 40f;
        const float sandHeightMax = 12f, sandHeightBlend = 6f;

        for (int zi = 0; zi < alphaRes; zi++)
        {
            float normZ = zi / (float)(alphaRes - 1);
            for (int xi = 0; xi < alphaRes; xi++)
            {
                float normX = xi / (float)(alphaRes - 1);

                float height = data.GetInterpolatedHeight(normX, normZ);
                float slopeDeg = data.GetSteepness(normX, normZ);

                float rockWeight = Mathf.Clamp01((slopeDeg - rockSlopeStart) / (rockSlopeFull - rockSlopeStart));
                float sandWeight = Mathf.Clamp01((sandHeightMax - height) / sandHeightBlend) * (1f - rockWeight);
                float grassWeight = 1f - rockWeight - sandWeight;

                alphamaps[zi, xi, 0] = rockWeight;
                alphamaps[zi, xi, 1] = sandWeight;
                alphamaps[zi, xi, 2] = grassWeight;
            }
        }

        data.SetAlphamaps(0, 0, alphamaps);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        float sandSample = alphamaps[0, 0, 1];
        int midX = alphaRes / 2, midZ = alphaRes / 2;
        result.Log("layers={0} alphaRes={1} centerWeights(rock,sand,grass)=({2},{3},{4})",
            data.terrainLayers.Length, alphaRes, alphamaps[midZ, midX, 0], alphamaps[midZ, midX, 1], alphamaps[midZ, midX, 2]);
    }
}
```

Note: `GetInterpolatedHeight`/`GetSteepness` take NORMALIZED `[0,1]`
coordinates (fraction across the terrain), not world or heightmap-index
coordinates — different convention from the two-island plan's
`SampleHeight(worldPos)`, so don't mix them up.

- [ ] **Step 2: Verify**

The logged center weights are for the terrain's exact center (world origin,
in the open-water channel between the islands — height ≈0, slope ≈0), so
expect `sand` to dominate there (`sandWeight` at height 0 with
`sandHeightMax=12` gives full weight): roughly `(rock≈0, sand≈1, grass≈0)`.

Then spot-check a few more points to confirm the blending rule actually
produced the right shape — query `GetInterpolatedHeight`/`GetSteepness` at
normalized coordinates corresponding to: Island A's mountain peak
`(-6300,-1500)` world → expect high slope → rock-dominant; a mid-island flat
grass area away from any flatten zone or peak → expect grass-dominant. Use
the same world-to-normalized conversion as the two-island plan's terrain
origin/size (`normX = (worldX - (-11000)) / 22000`, `normZ = (worldZ -
(-6000)) / 12000`).

- [ ] **Step 3: Commit**

```bash
git add Assets/Terrain/SandLayer.terrainlayer Assets/Terrain/SandLayer.terrainlayer.meta \
        Assets/Terrain/GrassLayer.terrainlayer Assets/Terrain/GrassLayer.terrainlayer.meta \
        Assets/Terrain/RockLayer.terrainlayer Assets/Terrain/RockLayer.terrainlayer.meta \
        Assets/Scenes/IslandTerrain.asset
git commit -m "$(cat <<'EOF'
Paint terrain with sand, grass, and rock layers

Height/slope-blended alphamap: rock on steep mountain flanks, sand
at low elevation (including the airport/city/town plateaus), grass
filling the rest.
EOF
)"
```

---

### Task 3: Generate the water material and assign it

**Files:**
- Create: `Assets/Materials/Water.mat`
- Modify: `Assets/Scenes/MainScene.unity` (`Sea`, `RiverA`, `RiverB` material references)

**Interfaces:**
- Consumes: `Assets/Textures/Water/Water.png` from Task 1.
- Produces: a new `Water.mat` (URP Lit shader, `Water.png` as `_BaseMap`)
  assigned to all three water GameObjects, replacing their current
  reference to URP's default `Lit.mat`.

- [ ] **Step 1: Create and assign the material**

Run via `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying)
        {
            result.LogError("Exit Play mode before editing the scene.");
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var water = new Material(shader) { name = "Water" };
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Water/Water.png");
        water.SetTexture("_BaseMap", tex);
        water.SetColor("_BaseColor", Color.white);

        AssetDatabase.CreateAsset(water, "Assets/Materials/Water.mat");
        result.RegisterObjectCreation(water);

        string[] names = { "Sea", "RiverA", "RiverB" };
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            var mr = go.GetComponent<MeshRenderer>();
            result.RegisterObjectModification(mr);
            mr.sharedMaterial = water;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        result.Log("Water material created, assigned to Sea/RiverA/RiverB. hasBaseMap={0}",
            water.GetTexture("_BaseMap") != null);
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `hasBaseMap=True`. Confirm via a second query that
`GameObject.Find("Sea").GetComponent<MeshRenderer>().sharedMaterial.name ==
"Water"` and the same for `RiverA`/`RiverB` — all three must point at the
SAME material instance (shared, per the design doc), not three separate
copies.

- [ ] **Step 3: Commit**

```bash
git add Assets/Materials/Water.mat Assets/Materials/Water.mat.meta Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Add shared water material to sea and both rivers

Replaces the plain gray default-Lit placeholder with a generated
tileable water texture, shared across Sea, RiverA, and RiverB.
EOF
)"
```

---

### Task 4: Visual verification and regression check

**Files:**
- None modified (verification only), unless a fix is needed.

**Interfaces:**
- Consumes: the fully textured world from Tasks 1-3.

- [ ] **Step 1: Run the existing automated suite**

Via `Unity_RunCommand`, call `Flusi.EditorTools.FlusiTestRunner.RunEditMode()`
then `RunPlayMode()`, polling in the same turn:
`until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt`.
Expected: `STATUS Passed`, EditMode 46 / PlayMode 6 — unchanged from baseline
(this plan adds no pure-logic code).

- [ ] **Step 2: Visual check**

Force the Game view open (`EditorApplication.ExecuteMenuItem("Window/General/Game")`),
enter Play mode, `ScreenCapture.CaptureScreenshot` a frame over Island A
(mountain/sand/grass visible) and a frame near a river or the sea. Exit Play
mode afterward (poll `isPlaying` before the next call). If capture fails in
this session (a known, documented unreliability — see the two-island plan's
Task 7), say so honestly and ask the project owner to eyeball it instead of
claiming an unconfirmed success.

Confirm from whatever you can see: sand reads at the coast and around the
flattened plateaus, rock reads on the mountains, grass fills the rest, and
the water isn't flat gray anymore.

- [ ] **Step 3: Commit (only if a fix was needed)**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Fix issues found in landscape texture verification pass

EOF
)"
```

If nothing needed fixing, no commit — Tasks 1-3 already captured the work.

---

## Self-Review Notes

- **Spec coverage:** texture generation (Task 1) → design doc §4/§5/§6;
  terrain layer painting (Task 2) → §4; water material (Task 3) → §5;
  verification (Task 4) → §7. All design doc sections have a task.
- **Type/name consistency checked:** `TerrainLayer` array order (`[Rock,
  Sand, Grass]`) is consistent between where it's assigned
  (`data.terrainLayers = ...`) and where the alphamap array is indexed
  (`alphamaps[z, x, 0/1/2]`) — index 0/1/2 must match rock/sand/grass in
  that exact order or the wrong texture paints the wrong terrain.
  GameObject names (`Sea`, `RiverA`, `RiverB`, `IslandTerrain`) match the
  two-island plan's actual output.
- **No placeholders:** every prompt, path, model ID, and threshold is a
  concrete value. Height/slope thresholds are explicitly flagged as
  tunable (per the design doc's "tuned live" latitude), same pattern as
  the two-island plan's coordinates — not a deferred decision, a starting
  point with a documented verification step.
- **Consent-gate handling:** Task 1 is explicitly controller-only, with the
  reasoning spelled out in Global Constraints, so whoever executes this
  plan (subagent-driven or inline) doesn't accidentally delegate it and
  hit a dead-end where a subagent asks a question the real user never
  sees.
