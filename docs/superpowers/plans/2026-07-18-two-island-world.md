# Two-Island World Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current single placeholder island with two distinct islands
(a city island and a mountain island) separated by open water, each with an
airport, a landmark mountain, a cluster of building placeholders, and a river.

**Architecture:** All work is Unity scene/terrain content, done through
throwaway C# scripts executed via the `Unity_RunCommand` MCP tool — no files
land in `Assets/Scripts`. Each task edits `Assets/Scenes/MainScene.unity`
and/or `Assets/Scenes/IslandTerrain.asset` directly through the Editor API,
verifies the result with a read-only query, then commits the changed binary
assets. The existing `Airport`/`Landmark`/`PointOfInterestRegistry` components
are reused unmodified.

**Tech Stack:** Unity 6000.5.2f1 Editor API (`Terrain`, `TerrainData`, `Mesh`),
executed through the project's `Unity_RunCommand` MCP tool. No new packages.

## Global Constraints

- Design doc: `docs/superpowers/specs/2026-07-18-two-island-world-design.md`.
- Spec: `docs/Specification.md` §5 (World), §6 (ground contact), §7.4 (POI
  registry), §9 (no floating-origin/streaming — stay well inside the ~50km
  float-safe radius from origin).
- World grows from 10km×10km to 22km×12km, still centered on origin (max
  radius ≈12.5km — nowhere near the 50km limit).
- Placeholders only this pass: box buildings, primitive markers, procedural
  heightmap mountains — no Asset Store art (design doc §2).
- Rivers are flat water-plane strips following terrain height, not carved
  heightmap channels (design doc §4, to avoid interacting with the
  terrain-height ground-contact clamp).
- Every `Unity_RunCommand` script: `CommandScript` implements `IRunCommand`
  and nothing else, is the only top-level class; helper logic goes in private
  methods or local functions of `CommandScript`, never a second class.
- Before any scene edit: check `EditorApplication.isPlaying` is `false` (all
  scripts below do this).
- After any scene edit: `EditorSceneManager.MarkSceneDirty` +
  `EditorSceneManager.SaveScene` (all scripts below do this) — required to
  reach disk, and both throw in Play mode.
- `result.Log` ignores format specifiers (`{0:F2}` prints literally) — none
  are used below.

---

### Task 1: Resize the world and reset the terrain

**Files:**
- Modify: `Assets/Scenes/IslandTerrain.asset` (via Editor API, not by hand)
- Modify: `Assets/Scenes/MainScene.unity` (`Sea` GameObject transform)

**Interfaces:**
- Produces: terrain `size = (22000, 900, 12000)`, `transform.position =
  (-11000, 0, -6000)`, `heightmapResolution = 1025`, all heights reset to 0.
  `Sea` GameObject scaled to cover the new terrain with margin. Every later
  task assumes these exact values when computing world-space heightmap
  coordinates.

- [ ] **Step 1: Run the resize/reset script**

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

        var terrainGO = GameObject.Find("IslandTerrain");
        var terrain = terrainGO.GetComponent<Terrain>();
        var data = terrain.terrainData;

        result.RegisterObjectModification(terrainGO.transform);
        result.RegisterObjectModification(terrain);

        data.heightmapResolution = 1025;
        data.size = new Vector3(22000f, 900f, 12000f);
        terrainGO.transform.position = new Vector3(-11000f, 0f, -6000f);

        int res = data.heightmapResolution;
        data.SetHeights(0, 0, new float[res, res]);

        var seaGO = GameObject.Find("Sea");
        result.RegisterObjectModification(seaGO.transform);
        seaGO.transform.localScale = new Vector3(3200f, 1f, 1800f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        result.Log("size={0} pos={1} heightmapRes={2} seaScale={3} cornerHeight={4}",
            data.size, terrainGO.transform.position, data.heightmapResolution,
            seaGO.transform.localScale, data.GetHeight(0, 0));
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `size=(22000.0, 900.0, 12000.0) pos=(-11000.0, 0.0, -6000.0)
heightmapRes=1025 seaScale=(3200.0, 1.0, 1800.0) cornerHeight=0`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scenes/IslandTerrain.asset Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Resize terrain to 22km x 12km for the two-island world

Reset heights to sea level so the next tasks sculpt onto a clean
base, and widened the sea plane to still cover the larger terrain.
EOF
)"
```

---

### Task 2: Sculpt Island A (city island) terrain

**Files:**
- Modify: `Assets/Scenes/IslandTerrain.asset`

**Interfaces:**
- Consumes: terrain state from Task 1 (`size=(22000,900,12000)`,
  `position=(-11000,0,-6000)`, `heightmapResolution=1025`, flat heights).
- Produces: Island A landmass centered at world `(-6000, 0)`, radius 4000,
  base height up to 40m; mountain peak at world `(-6300, -1500)`, radius 900,
  peak height 350m; two flattened zones: airport rect
  `x:[-7600,-6400] z:[-4400,-2000]` at height 6m, city rect
  `x:[-9700,-8300] z:[800,2200]` at height 10m. Later tasks (3) place objects
  at these exact coordinates and rely on the flattened heights being level.

- [ ] **Step 1: Run the sculpt script**

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

        var terrain = GameObject.Find("IslandTerrain").GetComponent<Terrain>();
        var data = terrain.terrainData;
        var origin = terrain.transform.position;
        int res = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, res, res);

        const float centerX = -6000f, centerZ = 0f, radius = 4000f, baseMax = 40f;
        const float peakX = -6300f, peakZ = -1500f, peakRadius = 900f, peakHeight = 350f;
        const float airportX0 = -7600f, airportZ0 = -4400f, airportX1 = -6400f, airportZ1 = -2000f, airportFlat = 6f;
        const float cityX0 = -9700f, cityZ0 = 800f, cityX1 = -8300f, cityZ1 = 2200f, cityFlat = 10f;
        const float margin = 250f;

        for (int zi = 0; zi < res; zi++)
        {
            float worldZ = origin.z + zi / (float)(res - 1) * data.size.z;
            for (int xi = 0; xi < res; xi++)
            {
                float worldX = origin.x + xi / (float)(res - 1) * data.size.x;

                float distIsland = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(centerX, centerZ));
                float land = Falloff(distIsland, radius) * baseMax;

                float distPeak = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(peakX, peakZ));
                float mountain = Mathf.Pow(Falloff(distPeak, peakRadius), 1.5f) * peakHeight;

                float h = land + mountain;
                h = Mathf.Lerp(h, airportFlat, FlattenBlend(worldX, worldZ, airportX0, airportZ0, airportX1, airportZ1, margin));
                h = Mathf.Lerp(h, cityFlat, FlattenBlend(worldX, worldZ, cityX0, cityZ0, cityX1, cityZ1, margin));

                heights[zi, xi] = Mathf.Clamp01(h / data.size.y);
            }
        }

        result.RegisterObjectModification(terrain);
        data.SetHeights(0, 0, heights);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        float peakSample = terrain.SampleHeight(new Vector3(peakX, 0f, peakZ));
        float airportSample = terrain.SampleHeight(new Vector3(-7000f, 0f, -3200f));
        float citySample = terrain.SampleHeight(new Vector3(-9000f, 0f, 1500f));
        float farAwaySample = terrain.SampleHeight(new Vector3(-1000f, 0f, 0f));
        result.Log("peak={0} airport={1} city={2} farAway(should be ~0)={3}",
            peakSample, airportSample, citySample, farAwaySample);
    }

    private static float Falloff(float dist, float r)
    {
        float t = Mathf.Clamp01(dist / r);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    private static float FlattenBlend(float x, float z, float x0, float z0, float x1, float z1, float m)
    {
        float dx = Mathf.Max(Mathf.Max(x0 - x, 0f), x - x1);
        float dz = Mathf.Max(Mathf.Max(z0 - z, 0f), z - z1);
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        return 1f - Mathf.Clamp01(d / m);
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `peak≈350` (within a few meters — `SampleHeight` interpolates),
`airport≈6`, `city≈10`, `farAway≈0` (a point 5000 units from Island A's
center, past its radius, should read essentially flat).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scenes/IslandTerrain.asset
git commit -m "$(cat <<'EOF'
Sculpt Island A: city-island landmass and mountain

Radial-falloff landmass centered at (-6000, 0) with a 350m peak
inland, plus flattened zones for the airport and city footprints
that later tasks build on.
EOF
)"
```

---

### Task 3: Place Island A content (airport, city, mountain landmark, river)

**Files:**
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: Island A terrain from Task 2 (flattened airport/city zones,
  mountain peak at `(-6300, 350, -1500)`); reuses existing GameObjects
  `Airport` (has `Flusi.Airport`), `MountainPeak` (has `Flusi.Landmark`), and
  `Flusi.Landmark`/`Flusi.Airport` label field name `label`
  (`Assets/Scripts/World/PointOfInterest.cs`).
- Produces: `Airport` repositioned to `(-7000, 6, -3200)` labeled "Westport";
  `MountainPeak` repositioned to `(-6300, 350, -1500)` labeled "Cloudspire";
  new `CityA_Skytown` GameObject (Landmark, label "Skytown") at
  `(-9000, 0, 1500)` with 13 child box buildings; new `RiverA` GameObject
  (mesh strip) from the peak to the west coast.

- [ ] **Step 1: Run the content script**

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

        var terrain = GameObject.Find("IslandTerrain").GetComponent<Terrain>();
        var seaMaterial = GameObject.Find("Sea").GetComponent<MeshRenderer>().sharedMaterial;

        var airportGO = GameObject.Find("Airport");
        result.RegisterObjectModification(airportGO.transform);
        airportGO.transform.position = new Vector3(-7000f, 6f, -3200f);
        var airportComp = airportGO.GetComponent<Flusi.Airport>();
        result.RegisterObjectModification(airportComp);
        var airportSO = new SerializedObject(airportComp);
        airportSO.FindProperty("label").stringValue = "Westport";
        airportSO.ApplyModifiedProperties();

        var peakGO = GameObject.Find("MountainPeak");
        result.RegisterObjectModification(peakGO.transform);
        peakGO.transform.position = new Vector3(-6300f, 350f, -1500f);
        var peakComp = peakGO.GetComponent<Flusi.Landmark>();
        result.RegisterObjectModification(peakComp);
        var peakSO = new SerializedObject(peakComp);
        peakSO.FindProperty("label").stringValue = "Cloudspire";
        peakSO.ApplyModifiedProperties();

        var cityGO = new GameObject("CityA_Skytown");
        cityGO.transform.position = new Vector3(-9000f, 0f, 1500f);
        var cityLandmark = cityGO.AddComponent<Flusi.Landmark>();
        var citySO = new SerializedObject(cityLandmark);
        citySO.FindProperty("label").stringValue = "Skytown";
        citySO.ApplyModifiedProperties();
        result.RegisterObjectCreation(cityGO);

        var rng = new System.Random(12345);
        const int cols = 5, rows = 3;
        const float spacingX = 260f, spacingZ = 260f;
        int built = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (r == rows - 1 && c >= cols - 2) continue;
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Skyscraper_" + r + "_" + c;
                b.transform.SetParent(cityGO.transform, false);
                float jitterX = (float)(rng.NextDouble() - 0.5) * 80f;
                float jitterZ = (float)(rng.NextDouble() - 0.5) * 80f;
                float height = 30f + (float)rng.NextDouble() * 120f;
                float localX = (c - (cols - 1) / 2f) * spacingX + jitterX;
                float localZ = (r - (rows - 1) / 2f) * spacingZ + jitterZ;
                b.transform.localPosition = new Vector3(localX, height / 2f, localZ);
                b.transform.localScale = new Vector3(60f, height, 60f);
                result.RegisterObjectCreation(b);
                built++;
            }
        }

        var riverPath = new[]
        {
            new Vector3(-6300f, 0f, -1500f),
            new Vector3(-6500f, 0f, -2400f),
            new Vector3(-6700f, 0f, -3300f),
            new Vector3(-6900f, 0f, -4200f),
        };
        var riverGO = BuildRiverStrip(terrain, riverPath, 60f, 220f, 2f, "RiverA", seaMaterial);
        result.RegisterObjectCreation(riverGO);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        result.Log("Airport at {0}, Peak at {1}, City buildings built={2}, River verts={3}",
            airportGO.transform.position, peakGO.transform.position, built,
            riverGO.GetComponent<MeshFilter>().sharedMesh.vertexCount);
    }

    private GameObject BuildRiverStrip(Terrain terrain, Vector3[] pathXZ, float startWidth, float endWidth, float lift, string name, Material material)
    {
        int n = pathXZ.Length;
        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];

        for (int i = 0; i < n; i++)
        {
            Vector3 p = pathXZ[i];
            float t = i / (float)(n - 1);
            float width = Mathf.Lerp(startWidth, endWidth, t);
            Vector3 prev = i > 0 ? pathXZ[i - 1] : pathXZ[i];
            Vector3 next = i < n - 1 ? pathXZ[i + 1] : pathXZ[i];
            Vector3 dir = (next - prev).normalized;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
            float y = terrain.SampleHeight(p) + lift;
            verts[i * 2] = new Vector3(p.x - side.x, y, p.z - side.z);
            verts[i * 2 + 1] = new Vector3(p.x + side.x, y, p.z + side.z);
            uvs[i * 2] = new Vector2(0f, t);
            uvs[i * 2 + 1] = new Vector2(1f, t);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int b = i * 2;
            int ti = i * 6;
            tris[ti] = b; tris[ti + 1] = b + 2; tris[ti + 2] = b + 1;
            tris[ti + 3] = b + 1; tris[ti + 4] = b + 2; tris[ti + 5] = b + 3;
        }

        var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `Airport at (-7000.0, 6.0, -3200.0), Peak at (-6300.0, 350.0,
-1500.0), City buildings built=13, River verts=8`.

- [ ] **Step 3: Visual check**

Force the Game view open and capture a screenshot to sanity-check the river
isn't inverted (invisible from above — a known winding-order risk for
procedural strip meshes) and buildings sit on the flattened city plateau, not
floating/clipping:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        SceneView.lastActiveSceneView.pivot = new Vector3(-7000f, 800f, -2500f);
        SceneView.lastActiveSceneView.rotation = Quaternion.Euler(60f, 0f, 0f);
        SceneView.lastActiveSceneView.Repaint();
        result.Log("Scene view framed over Island A. Ask the owner to screenshot if this session can't capture.");
    }
}
```

If the river is invisible from above, swap the two triangles' winding in
`BuildRiverStrip` (`b, b+1, b+2` / `b+1, b+3, b+2`) and re-run Step 1.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Place Island A content: airport, city, mountain, river

Repositions the existing Airport/MountainPeak markers onto the new
city-island layout and adds the Skytown building cluster and a
procedural river strip from the peak to the west coast.
EOF
)"
```

---

### Task 4: Sculpt Island B (mountain island) terrain

**Files:**
- Modify: `Assets/Scenes/IslandTerrain.asset`

**Interfaces:**
- Consumes: terrain state from Task 1.
- Produces: Island B landmass centered at world `(6000, 0)`, radius 4500,
  base height up to 50m; mountain peak at world `(6400, 800)`, radius 1300,
  peak height 600m; two flattened zones: airport rect
  `x:[7400,8600] z:[-4600,-2200]` at height 6m, town rect
  `x:[8000,9000] z:[-2900,-1700]` at height 8m.

- [ ] **Step 1: Run the sculpt script**

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

        var terrain = GameObject.Find("IslandTerrain").GetComponent<Terrain>();
        var data = terrain.terrainData;
        var origin = terrain.transform.position;
        int res = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, res, res);

        const float centerX = 6000f, centerZ = 0f, radius = 4500f, baseMax = 50f;
        const float peakX = 6400f, peakZ = 800f, peakRadius = 1300f, peakHeight = 600f;
        const float airportX0 = 7400f, airportZ0 = -4600f, airportX1 = 8600f, airportZ1 = -2200f, airportFlat = 6f;
        const float townX0 = 8000f, townZ0 = -2900f, townX1 = 9000f, townZ1 = -1700f, townFlat = 8f;
        const float margin = 250f;

        for (int zi = 0; zi < res; zi++)
        {
            float worldZ = origin.z + zi / (float)(res - 1) * data.size.z;
            for (int xi = 0; xi < res; xi++)
            {
                float worldX = origin.x + xi / (float)(res - 1) * data.size.x;

                float distIsland = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(centerX, centerZ));
                float land = Falloff(distIsland, radius) * baseMax;

                float distPeak = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(peakX, peakZ));
                float mountain = Mathf.Pow(Falloff(distPeak, peakRadius), 1.5f) * peakHeight;

                float h = land + mountain;
                h = Mathf.Lerp(h, airportFlat, FlattenBlend(worldX, worldZ, airportX0, airportZ0, airportX1, airportZ1, margin));
                h = Mathf.Lerp(h, townFlat, FlattenBlend(worldX, worldZ, townX0, townZ0, townX1, townZ1, margin));

                float existing = heights[zi, xi] * data.size.y;
                heights[zi, xi] = Mathf.Clamp01(Mathf.Max(existing, h) / data.size.y);
            }
        }

        result.RegisterObjectModification(terrain);
        data.SetHeights(0, 0, heights);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        float peakSample = terrain.SampleHeight(new Vector3(peakX, 0f, peakZ));
        float airportSample = terrain.SampleHeight(new Vector3(8000f, 0f, -3400f));
        float townSample = terrain.SampleHeight(new Vector3(8500f, 0f, -2300f));
        float channelSample = terrain.SampleHeight(new Vector3(0f, 0f, 0f));
        result.Log("peak={0} airport={1} town={2} channel(should be ~0)={3}",
            peakSample, airportSample, townSample, channelSample);
    }

    private static float Falloff(float dist, float r)
    {
        float t = Mathf.Clamp01(dist / r);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    private static float FlattenBlend(float x, float z, float x0, float z0, float x1, float z1, float m)
    {
        float dx = Mathf.Max(Mathf.Max(x0 - x, 0f), x - x1);
        float dz = Mathf.Max(Mathf.Max(z0 - z, 0f), z - z1);
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        return 1f - Mathf.Clamp01(d / m);
    }
}
```

Note the `Mathf.Max(existing, h)` when writing back: Island A (Task 2) and
Island B share the same heightmap, and their falloff radii don't overlap
(A's edge is at x=-2000, B's at x=1500 — 3.5km apart), but taking the max
instead of overwriting protects Island A's sculpt if the radii are ever
tuned closer together later.

- [ ] **Step 2: Verify**

Expected log: `peak≈600`, `airport≈6`, `town≈8`, `channel≈0` (world origin
sits in open water between the islands).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scenes/IslandTerrain.asset
git commit -m "$(cat <<'EOF'
Sculpt Island B: mountain-island landmass and peak

Larger landmass centered at (6000, 0) with a 600m peak, plus
flattened zones for the airport and town footprints.
EOF
)"
```

---

### Task 5: Place Island B content (airport, town, mountain landmark, river)

**Files:**
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: Island B terrain from Task 4; reuses `Flusi.Landmark`'s existing
  `Lighthouse` GameObject as the mountain marker (repositioned/relabeled,
  same pattern as Task 3's `MountainPeak` reuse); creates a new `Airport`
  GameObject (Task 3 already claimed the original one for Island A).
- Produces: new `AirportB` GameObject at `(8000, 6, -3400)` labeled
  "Eastfield"; `Lighthouse` repositioned to `(6400, 600, 800)` labeled
  "Giant's Peak"; new `TownB_Millbrook` GameObject (Landmark, label
  "Millbrook") at `(8500, 0, -2300)` with 6 child box buildings; new
  `RiverB` GameObject (mesh strip) from the peak to the east coast.

- [ ] **Step 1: Run the content script**

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

        var terrain = GameObject.Find("IslandTerrain").GetComponent<Terrain>();
        var seaMaterial = GameObject.Find("Sea").GetComponent<MeshRenderer>().sharedMaterial;
        var referenceAirport = GameObject.Find("Airport");

        var airportBGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        airportBGO.name = "AirportB";
        airportBGO.transform.position = new Vector3(8000f, 6f, -3400f);
        airportBGO.transform.localScale = referenceAirport.transform.localScale;
        var airportBComp = airportBGO.AddComponent<Flusi.Airport>();
        var airportBSO = new SerializedObject(airportBComp);
        airportBSO.FindProperty("label").stringValue = "Eastfield";
        airportBSO.ApplyModifiedProperties();
        result.RegisterObjectCreation(airportBGO);

        var peakGO = GameObject.Find("Lighthouse");
        result.RegisterObjectModification(peakGO.transform);
        peakGO.transform.position = new Vector3(6400f, 600f, 800f);
        var peakComp = peakGO.GetComponent<Flusi.Landmark>();
        result.RegisterObjectModification(peakComp);
        var peakSO = new SerializedObject(peakComp);
        peakSO.FindProperty("label").stringValue = "Giant's Peak";
        peakSO.ApplyModifiedProperties();

        var townGO = new GameObject("TownB_Millbrook");
        townGO.transform.position = new Vector3(8500f, 0f, -2300f);
        var townLandmark = townGO.AddComponent<Flusi.Landmark>();
        var townSO = new SerializedObject(townLandmark);
        townSO.FindProperty("label").stringValue = "Millbrook";
        townSO.ApplyModifiedProperties();
        result.RegisterObjectCreation(townGO);

        var rng = new System.Random(54321);
        const int cols = 3, rows = 2;
        const float spacingX = 150f, spacingZ = 150f;
        int built = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Building_" + r + "_" + c;
                b.transform.SetParent(townGO.transform, false);
                float jitterX = (float)(rng.NextDouble() - 0.5) * 50f;
                float jitterZ = (float)(rng.NextDouble() - 0.5) * 50f;
                float height = 10f + (float)rng.NextDouble() * 20f;
                float localX = (c - (cols - 1) / 2f) * spacingX + jitterX;
                float localZ = (r - (rows - 1) / 2f) * spacingZ + jitterZ;
                b.transform.localPosition = new Vector3(localX, height / 2f, localZ);
                b.transform.localScale = new Vector3(35f, height, 35f);
                result.RegisterObjectCreation(b);
                built++;
            }
        }

        var riverPath = new[]
        {
            new Vector3(6400f, 0f, 800f),
            new Vector3(7300f, 0f, 900f),
            new Vector3(8300f, 0f, 950f),
            new Vector3(9300f, 0f, 1000f),
            new Vector3(10200f, 0f, 1050f),
        };
        var riverGO = BuildRiverStrip(terrain, riverPath, 60f, 220f, 2f, "RiverB", seaMaterial);
        result.RegisterObjectCreation(riverGO);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        result.Log("AirportB at {0}, Peak at {1}, Town buildings built={2}, River verts={3}",
            airportBGO.transform.position, peakGO.transform.position, built,
            riverGO.GetComponent<MeshFilter>().sharedMesh.vertexCount);
    }

    private GameObject BuildRiverStrip(Terrain terrain, Vector3[] pathXZ, float startWidth, float endWidth, float lift, string name, Material material)
    {
        int n = pathXZ.Length;
        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];

        for (int i = 0; i < n; i++)
        {
            Vector3 p = pathXZ[i];
            float t = i / (float)(n - 1);
            float width = Mathf.Lerp(startWidth, endWidth, t);
            Vector3 prev = i > 0 ? pathXZ[i - 1] : pathXZ[i];
            Vector3 next = i < n - 1 ? pathXZ[i + 1] : pathXZ[i];
            Vector3 dir = (next - prev).normalized;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
            float y = terrain.SampleHeight(p) + lift;
            verts[i * 2] = new Vector3(p.x - side.x, y, p.z - side.z);
            verts[i * 2 + 1] = new Vector3(p.x + side.x, y, p.z + side.z);
            uvs[i * 2] = new Vector2(0f, t);
            uvs[i * 2 + 1] = new Vector2(1f, t);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int b = i * 2;
            int ti = i * 6;
            tris[ti] = b; tris[ti + 1] = b + 2; tris[ti + 2] = b + 1;
            tris[ti + 3] = b + 1; tris[ti + 4] = b + 2; tris[ti + 5] = b + 3;
        }

        var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `AirportB at (8000.0, 6.0, -3400.0), Peak at (6400.0, 600.0,
800.0), Town buildings built=6, River verts=10`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Place Island B content: airport, town, mountain, river

Adds a new Eastfield airport marker, repurposes the old Lighthouse
GameObject as the Giant's Peak landmark, and adds the Millbrook
building cluster and a river strip from the peak to the east coast.
EOF
)"
```

---

### Task 6: Cleanup and structural verification

**Files:**
- Modify: `Assets/Scenes/MainScene.unity` (only if the spawn check in Step 1
  finds a problem — see contingency below)

**Interfaces:**
- Consumes: full scene state from Tasks 1-5.
- Produces: confirmation that exactly 2 `Flusi.Airport` and 4 `Flusi.Landmark`
  components exist, and that the aircraft's spawn point
  (`Aircraft` GameObject, `(0, 400, -1500)`) is over open water, not land.

- [ ] **Step 1: Run the verification script**

Run via `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var terrain = GameObject.Find("IslandTerrain").GetComponent<Terrain>();
        float spawnHeight = terrain.SampleHeight(new Vector3(0f, 0f, -1500f));

        int airportCount = GameObject.FindObjectsByType<Flusi.Airport>(FindObjectsSortMode.None).Length;
        int landmarkCount = GameObject.FindObjectsByType<Flusi.Landmark>(FindObjectsSortMode.None).Length;

        result.Log("spawnHeight={0} (expect ~0, plane spawns at y=400) airportCount={1} (expect 2) landmarkCount={2} (expect 4)",
            spawnHeight, airportCount, landmarkCount);
    }
}
```

- [ ] **Step 2: Verify, with contingency**

Expected log: `spawnHeight≈0 airportCount=2 landmarkCount=4`.

- If `airportCount`/`landmarkCount` are wrong, re-check Tasks 3 and 5 ran
  (a component may have failed to attach) before proceeding.
- If `spawnHeight` is more than ~50 (the plane would spawn close to a
  mountainside instead of open water), the channel width was tuned too
  narrow in Task 2/4 — move the `Aircraft` GameObject's Z position toward 0
  in a follow-up script, `MarkSceneDirty`, `SaveScene`, and re-run Step 1.

- [ ] **Step 3: Commit (only if Step 2's contingency fired)**

```bash
git add Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Nudge aircraft spawn clear of Island terrain

The spawn point sampled non-trivial terrain height after sculpting;
moved it back into open water in the channel.
EOF
)"
```

---

### Task 7: Regression test, manual verification, and final commit

**Files:**
- None modified (verification only), unless the manual check in Step 3
  surfaces a problem worth a follow-up fix.

**Interfaces:**
- Consumes: the complete two-island world from Tasks 1-6.
- Produces: confirmation the existing automated suite is unaffected and the
  world is flyable end-to-end.

- [ ] **Step 1: Run the existing automated suite**

Via `Unity_RunCommand`, call `Flusi.EditorTools.FlusiTestRunner.RunEditMode()`
then `RunPlayMode()`, polling `Temp/flusi-tests.txt` per the project's
established pattern (`CLAUDE.md` → Tests section) with:

```bash
until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt
```

Expected: `STATUS Passed`, EditMode 46 / PlayMode 6 (this pass adds no new
pure-logic code, so counts are unchanged — see Global Constraints). None of
the existing PlayMode tests load `MainScene` or hardcode world coordinates
(`AircraftControllerSmokeTests`, `CockpitPanelSmokeTests`, `GearTests`,
`PointOfInterestRegistryTests` all build their own GameObjects), so they are
unaffected by the terrain/scene changes.

- [ ] **Step 2: Manual flight check**

Via `Unity_RunCommand`: open `MainScene`, enter Play mode, drive the aircraft
with synthetic Input System events toward Island A, then Island B (per
`CLAUDE.md`'s `InputSystem.QueueStateEvent` pattern — queue, pump, and read
input in the same script since synthetic state doesn't survive to the next
frame), confirm no console errors and `Flusi.PointOfInterestRegistry.All`
reports 6 entries (2 Airport, 4 Landmark) once Play mode is running. Exit
Play mode afterward (`EditorApplication.isPlaying = false`; poll before the
next `Unity_RunCommand` call, the exit is deferred a frame).

- [ ] **Step 3: Visual review**

Force the Game view open and `ScreenCapture.CaptureScreenshot` a few frames
from cockpit view over each island (per `CLAUDE.md`'s documented technique).
If the session can't capture (headless/unattended), ask the project owner to
eyeball the result in the Editor instead — don't claim visual success from
an unconfirmed capture.

- [ ] **Step 4: Final commit (only if Steps 1-3 required fixes)**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Fix issues found in two-island world verification pass

EOF
)"
```

If no fixes were needed, this task produces no commit — Tasks 1-6 already
captured all the content changes.

---

## Self-Review Notes

- **Spec coverage:** world resize (Task 1) → design doc §3; Island A/B
  landmass+mountain+flatten (Tasks 2, 4) → design doc §4.1/4.2; POI
  placement (Tasks 3, 5) → design doc §5; cleanup/spawn safety (Task 6) →
  design doc §7 risks; regression + manual verification (Task 7) → design
  doc §6. All design doc sections have a task.
- **Type/name consistency checked:** `label` field name (from
  `PointOfInterest.cs`), `Flusi.Airport`/`Flusi.Landmark` types, and every
  GameObject name referenced by a later task (`Airport`, `MountainPeak`,
  `Lighthouse`, `Sea`, `IslandTerrain`, `CityA_Skytown`, `TownB_Millbrook`)
  match what the earlier task actually creates/reuses.
- **No placeholders:** every coordinate, radius, and height is a concrete
  number; the design doc's "tuned live" caveat is satisfied by Task 3/5's
  explicit visual-check steps with a real contingency (winding-order fix),
  not a deferred decision.
