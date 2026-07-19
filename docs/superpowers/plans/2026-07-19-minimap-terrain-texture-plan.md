# Minimap Land/Sea Texture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the minimap's flat black panel with a baked green (land) /
blue (sea + rivers) background, and fix the stale square world-size bug that
would otherwise misalign it with the existing POI/plane blips.

**Architecture:** A new pure static class (`MinimapTerrainTexture`) computes
land/water per pixel from a terrain-height sampler, a sea-level float, and a
list of river boundary polygons — no Unity scene dependency, fully
EditMode-testable. A new thin `MonoBehaviour` (`MinimapTerrainRenderer`)
supplies the live terrain/river data, bakes the result into a `Texture2D`
once at `Awake`, and fits it into the (roughly square) minimap panel
preserving the world's true aspect ratio. `Minimap`/`MinimapProjection` are
fixed to use the real rectangular world extent instead of a stale square
value, and `MinimapTerrainRenderer` reads that same extent from `Minimap`
(new public properties) rather than duplicating it, so the background and
the dots can never drift apart.

**Tech Stack:** Unity 6000.5.2f1, C#, `com.unity.test-framework`
(NUnit-based EditMode tests), Universal Render Pipeline (rendering
unaffected — this is UI-only).

## Global Constraints

- Unity **6000.5.2f1**, C#, URP. Do not add packages/dependencies.
- Namespace is flat `Flusi` (`Flusi.Tests` for tests); no nested namespaces.
- Follow the established pattern: pure static maths behind a thin
  `MonoBehaviour`, so the logic is EditMode-testable
  (`docs/Specification.md` §7.3).
- Scene edits: throwaway C# inside `Unity_RunCommand`
  (`SerializedObject`/`FindProperty`/`ApplyModifiedProperties` for private
  `[SerializeField]`s), then `EditorSceneManager.MarkSceneDirty` + `SaveScene`
  — never add new Editor scripts to do scene work. Check
  `EditorApplication.isPlaying == false` first.
- `Unity_RunCommand` sandbox: `CommandScript` must implement `IRunCommand`
  and be the only top-level class; no `System.Reflection` or
  `System.Text.RegularExpressions`; fully qualify `UnityEngine.UI.Image`.
- Tests: run via `Flusi.EditorTools.FlusiTestRunner.RunEditMode()` inside
  `Unity_RunCommand`, poll `Temp/flusi-tests.txt` for `STATUS Passed`.
  Baseline before this plan: **EditMode 46, PlayMode 6** — this plan only
  adds EditMode tests (no PlayMode/runtime-input changes), so PlayMode
  stays at 6.
- A "green" test run is worthless until proven live — check console errors
  and that the run isn't stale (see `CLAUDE.md` Tests section).
- Commit messages: `write-commit-messages` skill format (imperative
  subject ≤50 chars, body wrapped at 72, explaining what/why, no trailers).
- `ProjectSettings/UnityConnectSettings.asset` may flip from ordinary Editor
  activity — check `git diff` and leave it out of commits if that's all it is.

---

### Task 1: Fix the world-extent bug (`MinimapProjection` + `Minimap`)

**Files:**
- Modify: `Assets/Scripts/World/MinimapProjection.cs`
- Modify: `Assets/Scripts/World/Minimap.cs`
- Test: `Assets/Tests/EditMode/MinimapProjectionTests.cs`

**Interfaces:**
- Produces: `MinimapProjection.WorldToNormalized(Vector2 worldXZ, Vector2 worldMin, Vector2 worldMax) -> Vector2`
  (replaces the old `(Vector2 worldXZ, float worldSizeMetres)` overload,
  which is deleted, not kept alongside).
- Produces: `Minimap.WorldMin -> Vector2` and `Minimap.WorldMax -> Vector2`
  (public read-only properties wrapping new serialized fields), consumed by
  Task 3's `MinimapTerrainRenderer`.

- [ ] **Step 1: Write the failing tests**

Replace the full contents of `Assets/Tests/EditMode/MinimapProjectionTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapProjectionTests
    {
        private static readonly Vector2 WorldMin = new Vector2(-11000f, -6000f);
        private static readonly Vector2 WorldMax = new Vector2(11000f, 6000f);

        [Test]
        public void Origin_MapsToCentre()
        {
            var n = MinimapProjection.WorldToNormalized(Vector2.zero, WorldMin, WorldMax);
            Assert.AreEqual(0.5f, n.x, 0.0001f);
            Assert.AreEqual(0.5f, n.y, 0.0001f);
        }

        [Test]
        public void PositiveCorner_MapsToOne()
        {
            var n = MinimapProjection.WorldToNormalized(WorldMax, WorldMin, WorldMax);
            Assert.AreEqual(1f, n.x, 0.0001f);
            Assert.AreEqual(1f, n.y, 0.0001f);
        }

        [Test]
        public void NegativeCorner_MapsToZero()
        {
            var n = MinimapProjection.WorldToNormalized(WorldMin, WorldMin, WorldMax);
            Assert.AreEqual(0f, n.x, 0.0001f);
            Assert.AreEqual(0f, n.y, 0.0001f);
        }

        [Test]
        public void OutOfBounds_Clamps()
        {
            var n = MinimapProjection.WorldToNormalized(new Vector2(99999f, -99999f), WorldMin, WorldMax);
            Assert.AreEqual(1f, n.x, 0.0001f);
            Assert.AreEqual(0f, n.y, 0.0001f);
        }

        [Test]
        public void RectangularWorld_AxesNormalizeIndependently()
        {
            // Halfway across X (22000 wide) but only a quarter across Z (12000 tall)
            var point = new Vector2(0f, -3000f); // 0 -> mid X; -3000 -> 1/4 up from min Z
            var n = MinimapProjection.WorldToNormalized(point, WorldMin, WorldMax);
            Assert.AreEqual(0.5f, n.x, 0.0001f);
            Assert.AreEqual(0.25f, n.y, 0.0001f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Inside `Unity_RunCommand`:

```csharp
using UnityEditor;
using Flusi.EditorTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying)
        {
            result.LogError("Editor is in Play mode; aborting test run.");
            return;
        }
        FlusiTestRunner.RunEditMode();
        result.Log("EditMode run started.");
    }
}
```

Then poll: `until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt`

Expected: compilation failure (`WorldToNormalized(Vector2, float)` overload
doesn't match the new 3-arg calls) — confirms the test file compiles against
a signature that doesn't exist yet.

- [ ] **Step 3: Implement `MinimapProjection`**

Replace `Assets/Scripts/World/MinimapProjection.cs` in full:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Maps world XZ to a 0..1 minimap coordinate, independently per axis,
    /// so a rectangular (non-square) world maps correctly.
    public static class MinimapProjection
    {
        public static Vector2 WorldToNormalized(Vector2 worldXZ, Vector2 worldMin, Vector2 worldMax)
        {
            float nx = Mathf.Clamp01((worldXZ.x - worldMin.x) / (worldMax.x - worldMin.x));
            float ny = Mathf.Clamp01((worldXZ.y - worldMin.y) / (worldMax.y - worldMin.y));
            return new Vector2(nx, ny);
        }
    }
}
```

- [ ] **Step 4: Implement the `Minimap` field/property change**

In `Assets/Scripts/World/Minimap.cs`, replace the
`[SerializeField] private float worldSizeMetres = 10000f;` field with:

```csharp
[SerializeField] private Vector2 worldMin = new Vector2(-11000f, -6000f);
[SerializeField] private Vector2 worldMax = new Vector2(11000f, 6000f);

public Vector2 WorldMin => worldMin;
public Vector2 WorldMax => worldMax;
```

And in `Place()`, change:

```csharp
Vector2 n = MinimapProjection.WorldToNormalized(worldXZ, worldSizeMetres);
```

to:

```csharp
Vector2 n = MinimapProjection.WorldToNormalized(worldXZ, worldMin, worldMax);
```

- [ ] **Step 5: Run tests to verify they pass**

Same `Unity_RunCommand` + poll as Step 2.
Expected: `STATUS Passed`, `passed=50 failed=0` (46 baseline + 4 new tests in
this file — the old file had 3 tests, this one has 5, net +2 here; final
count confirmed against the full baseline in Task 4's final check since
Task 3 adds more tests too).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/World/MinimapProjection.cs Assets/Scripts/World/Minimap.cs Assets/Tests/EditMode/MinimapProjectionTests.cs
git commit -m "$(cat <<'EOF'
Fix minimap projection to use the real rectangular world

Minimap.worldSizeMetres was a single square value (10000) left over
from before the world was resized to a rectangular 22000x12000
two-island layout. Anything near the true world's edges was already
clamping to the minimap border incorrectly.

Replace it with worldMin/worldMax matching the terrain's actual
bounds, and make WorldToNormalized normalize each axis independently
so a non-square world maps correctly.
EOF
)"
```

---

### Task 2: Pure land/water texture maths (`MinimapTerrainTexture`)

**Files:**
- Create: `Assets/Scripts/World/MinimapTerrainTexture.cs`
- Test: `Assets/Tests/EditMode/MinimapTerrainTextureTests.cs`

**Interfaces:**
- Consumes: nothing (pure, only `UnityEngine.Vector2`/`Color32`/`Mathf`).
- Produces:
  - `MinimapTerrainTexture.LandColor -> Color32` and
    `MinimapTerrainTexture.WaterColor -> Color32` (public static readonly),
    consumed by Task 3.
  - `MinimapTerrainTexture.InsidePolygon(Vector2 point, Vector2[] polygon) -> bool`
  - `MinimapTerrainTexture.Build(int width, int height, Func<float,float,float> terrainHeightAt, float seaLevel, IReadOnlyList<Vector2[]> riverPolygons, Vector2 worldMin, Vector2 worldMax) -> Color32[]`,
    consumed by Task 3.
  - `MinimapTerrainTexture.FitWithLetterbox(Vector2 containerSize, float aspectWidthOverHeight) -> Vector2`,
    consumed by Task 3.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/MinimapTerrainTextureTests.cs`:

```csharp
using System;
using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapTerrainTextureTests
    {
        private static readonly Vector2[] UnitSquare =
        {
            new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(10f, 10f), new Vector2(10f, 0f)
        };

        [Test]
        public void InsidePolygon_PointInside_ReturnsTrue()
        {
            Assert.IsTrue(MinimapTerrainTexture.InsidePolygon(new Vector2(5f, 5f), UnitSquare));
        }

        [Test]
        public void InsidePolygon_PointOutside_ReturnsFalse()
        {
            Assert.IsFalse(MinimapTerrainTexture.InsidePolygon(new Vector2(20f, 20f), UnitSquare));
        }

        [Test]
        public void FitWithLetterbox_SquareContainer_WidthConstrained()
        {
            var size = MinimapTerrainTexture.FitWithLetterbox(new Vector2(200f, 200f), 22000f / 12000f);
            Assert.AreEqual(200f, size.x, 0.01f);
            Assert.AreEqual(200f * 12000f / 22000f, size.y, 0.01f);
        }

        [Test]
        public void FitWithLetterbox_WideContainer_HeightConstrained()
        {
            var size = MinimapTerrainTexture.FitWithLetterbox(new Vector2(400f, 100f), 22000f / 12000f);
            Assert.AreEqual(100f, size.y, 0.01f);
            Assert.AreEqual(100f * 22000f / 12000f, size.x, 0.01f);
        }

        [Test]
        public void Build_AboveSeaLevelEverywhere_AllLand()
        {
            var pixels = MinimapTerrainTexture.Build(
                2, 2,
                (x, z) => 100f,
                seaLevel: 5f,
                riverPolygons: Array.Empty<Vector2[]>(),
                worldMin: new Vector2(-10f, -10f),
                worldMax: new Vector2(10f, 10f));

            Assert.AreEqual(4, pixels.Length);
            foreach (var p in pixels)
                Assert.AreEqual(MinimapTerrainTexture.LandColor, p);
        }

        [Test]
        public void Build_BelowSeaLevelEverywhere_AllWater()
        {
            var pixels = MinimapTerrainTexture.Build(
                2, 2,
                (x, z) => 0f,
                seaLevel: 5f,
                riverPolygons: Array.Empty<Vector2[]>(),
                worldMin: new Vector2(-10f, -10f),
                worldMax: new Vector2(10f, 10f));

            foreach (var p in pixels)
                Assert.AreEqual(MinimapTerrainTexture.WaterColor, p);
        }

        [Test]
        public void Build_RiverPolygonOverridesHighLand()
        {
            // Covers the whole sampled area, so every pixel should read as
            // water even though the height function says "land".
            var river = new[]
            {
                new Vector2(-10f, -10f), new Vector2(-10f, 10f),
                new Vector2(10f, 10f), new Vector2(10f, -10f)
            };
            var pixels = MinimapTerrainTexture.Build(
                2, 2,
                (x, z) => 100f,
                seaLevel: 5f,
                riverPolygons: new[] { river },
                worldMin: new Vector2(-10f, -10f),
                worldMax: new Vector2(10f, 10f));

            foreach (var p in pixels)
                Assert.AreEqual(MinimapTerrainTexture.WaterColor, p);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Same `Unity_RunCommand` pattern as Task 1 Step 2.
Expected: compilation failure — `MinimapTerrainTexture` doesn't exist yet.

- [ ] **Step 3: Implement `MinimapTerrainTexture`**

Create `Assets/Scripts/World/MinimapTerrainTexture.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flusi
{
    /// Pure land/water maths for the minimap background: given a terrain
    /// height sampler, a sea-level threshold, and river boundary polygons,
    /// produces a flat 2-tone Color32 buffer. No Unity scene dependency.
    public static class MinimapTerrainTexture
    {
        public static readonly Color32 LandColor = new Color32(72, 148, 72, 255);
        public static readonly Color32 WaterColor = new Color32(58, 110, 189, 255);

        public static Color32[] Build(
            int width,
            int height,
            Func<float, float, float> terrainHeightAt,
            float seaLevel,
            IReadOnlyList<Vector2[]> riverPolygons,
            Vector2 worldMin,
            Vector2 worldMax)
        {
            var pixels = new Color32[width * height];
            float worldW = worldMax.x - worldMin.x;
            float worldH = worldMax.y - worldMin.y;

            for (int py = 0; py < height; py++)
            {
                float v = (py + 0.5f) / height;
                float worldZ = worldMin.y + v * worldH;

                for (int px = 0; px < width; px++)
                {
                    float u = (px + 0.5f) / width;
                    float worldX = worldMin.x + u * worldW;

                    bool isWater = terrainHeightAt(worldX, worldZ) <= seaLevel;
                    if (!isWater)
                    {
                        var point = new Vector2(worldX, worldZ);
                        for (int i = 0; i < riverPolygons.Count && !isWater; i++)
                            isWater = InsidePolygon(point, riverPolygons[i]);
                    }

                    pixels[py * width + px] = isWater ? WaterColor : LandColor;
                }
            }

            return pixels;
        }

        /// Standard even-odd ray-casting point-in-polygon test.
        public static bool InsidePolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; i++)
            {
                bool crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
                if (crosses)
                {
                    float xAtY = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y)
                        / (polygon[j].y - polygon[i].y) + polygon[i].x;
                    if (point.x < xAtY) inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        /// Size of the largest box with `aspectWidthOverHeight` that fits
        /// centred inside `containerSize`.
        public static Vector2 FitWithLetterbox(Vector2 containerSize, float aspectWidthOverHeight)
        {
            float containerAspect = containerSize.x / containerSize.y;
            if (containerAspect > aspectWidthOverHeight)
            {
                float h = containerSize.y;
                return new Vector2(h * aspectWidthOverHeight, h);
            }
            else
            {
                float w = containerSize.x;
                return new Vector2(w, w / aspectWidthOverHeight);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same `Unity_RunCommand` + poll as Step 2.
Expected: `STATUS Passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/World/MinimapTerrainTexture.cs Assets/Tests/EditMode/MinimapTerrainTextureTests.cs
git commit -m "$(cat <<'EOF'
Add pure land/water texture maths for the minimap

New MinimapTerrainTexture: given a terrain-height sampler, a
sea-level threshold, and river boundary polygons, produces a flat
2-tone Color32 buffer (green land, blue water), plus a letterbox-fit
helper for placing a non-square-aspect texture inside a square panel.
No Unity scene dependency, fully EditMode-testable.
EOF
)"
```

---

### Task 3: `MinimapTerrainRenderer` (thin bake-once component)

**Files:**
- Create: `Assets/Scripts/World/MinimapTerrainRenderer.cs`
- Test: `Assets/Tests/EditMode/MinimapTerrainRendererTests.cs`

**Interfaces:**
- Consumes: `Minimap.WorldMin`/`WorldMax` (Task 1),
  `MinimapTerrainTexture.Build`/`FitWithLetterbox`/`LandColor`/`WaterColor`
  (Task 2).
- Produces: `MinimapTerrainRenderer.RiverPolygonFromMesh(MeshFilter mf) -> Vector2[]`
  (public static, so it's testable without a live scene — consumed
  internally by this component's own `Awake`, and by Task 4's manual scene
  wiring only indirectly through the component itself).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/MinimapTerrainRendererTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapTerrainRendererTests
    {
        [Test]
        public void RiverPolygonFromMesh_WalksLeftBankThenRightBankReversed()
        {
            // Simulate a 2-station ribbon: (left0,right0), (left1,right1).
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),   // left0  (index 0)
                    new Vector3(1f, 0f, 0f),   // right0 (index 1)
                    new Vector3(0f, 0f, 10f),  // left1  (index 2)
                    new Vector3(1f, 0f, 10f),  // right1 (index 3)
                }
            };

            var go = new GameObject("TestRiver", typeof(MeshFilter));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var polygon = MinimapTerrainRenderer.RiverPolygonFromMesh(go.GetComponent<MeshFilter>());

            // Expected walk: left0, left1 (forward), then right1, right0 (backward).
            Assert.AreEqual(4, polygon.Length);
            Assert.AreEqual(new Vector2(0f, 0f), polygon[0]);
            Assert.AreEqual(new Vector2(0f, 10f), polygon[1]);
            Assert.AreEqual(new Vector2(1f, 10f), polygon[2]);
            Assert.AreEqual(new Vector2(1f, 0f), polygon[3]);

            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Same `Unity_RunCommand` pattern as before.
Expected: compilation failure — `MinimapTerrainRenderer` doesn't exist yet.

- [ ] **Step 3: Implement `MinimapTerrainRenderer`**

Create `Assets/Scripts/World/MinimapTerrainRenderer.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Bakes the minimap's land/water background once at Awake from the
    /// live terrain and river meshes. Static bake — the world doesn't
    /// change at runtime, so this never re-runs after Awake.
    public class MinimapTerrainRenderer : MonoBehaviour
    {
        private const int TextureWidth = 256;
        private const int TextureHeight = 140; // ~22000:12000 world aspect

        [SerializeField] private Minimap minimap;      // source of truth for world extent
        [SerializeField] private Terrain terrain;
        [SerializeField] private MeshFilter[] riverMeshes;
        [SerializeField] private float seaLevel = 5f;  // matches the Sea mesh's world Y
        [SerializeField] private RectTransform panel;  // the square minimap area to fit inside
        [SerializeField] private Image background;     // this component's own Image

        private void Awake()
        {
            var riverPolygons = new List<Vector2[]>(riverMeshes.Length);
            foreach (var mf in riverMeshes)
                if (mf != null) riverPolygons.Add(RiverPolygonFromMesh(mf));

            var pixels = MinimapTerrainTexture.Build(
                TextureWidth, TextureHeight,
                SampleTerrainHeight,
                seaLevel,
                riverPolygons,
                minimap.WorldMin, minimap.WorldMax);

            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            background.sprite = Sprite.Create(
                texture, new Rect(0f, 0f, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f));

            float worldAspect = (minimap.WorldMax.x - minimap.WorldMin.x)
                / (minimap.WorldMax.y - minimap.WorldMin.y);
            var fitSize = MinimapTerrainTexture.FitWithLetterbox(panel.rect.size, worldAspect);

            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = fitSize;
        }

        private float SampleTerrainHeight(float worldX, float worldZ) =>
            terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

        /// Walks a river ribbon mesh's vertices (ordered left/right-bank
        /// pairs per station) into one closed 2D boundary polygon: left
        /// bank forward, then right bank backward.
        public static Vector2[] RiverPolygonFromMesh(MeshFilter mf)
        {
            var verts = mf.sharedMesh.vertices;
            int pairs = verts.Length / 2;
            var polygon = new Vector2[verts.Length];

            for (int i = 0; i < pairs; i++)
            {
                var world = mf.transform.TransformPoint(verts[i * 2]);
                polygon[i] = new Vector2(world.x, world.z);
            }
            for (int i = 0; i < pairs; i++)
            {
                var world = mf.transform.TransformPoint(verts[verts.Length - 1 - i * 2]);
                polygon[pairs + i] = new Vector2(world.x, world.z);
            }

            return polygon;
        }
    }
}
```

Note: the test constructs its `GameObject`/`MeshFilter` with an identity
transform, so `TransformPoint` is a no-op there — the test validates the
vertex-walk order, not the world-transform step.

- [ ] **Step 4: Run test to verify it passes**

Same `Unity_RunCommand` + poll as before.
Expected: `STATUS Passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/World/MinimapTerrainRenderer.cs Assets/Tests/EditMode/MinimapTerrainRendererTests.cs
git commit -m "$(cat <<'EOF'
Add MinimapTerrainRenderer to bake the minimap background

Thin MonoBehaviour: reads world extent from Minimap (single source of
truth, so the background can't drift from the POI/plane blips), reads
the terrain and river meshes, and bakes a one-time Texture2D via
MinimapTerrainTexture. River meshes are simple left/right-bank
ribbons; RiverPolygonFromMesh walks them into one closed boundary
polygon for the point-in-polygon test.
EOF
)"
```

---

### Task 4: Wire the scene and verify

**Files:**
- Modify: `Assets/Scenes/MainScene.unity` (via `Unity_RunCommand`, not hand-edited)

**Interfaces:**
- Consumes: `Minimap` (Task 1), `MinimapTerrainRenderer` (Task 3).
- Produces: nothing further — this is the terminal integration task.

- [ ] **Step 1: Add the `TerrainBackground` GameObject and wire it up**

Inside `Unity_RunCommand` (check `EditorApplication.isPlaying == false`
first, per Global Constraints):

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying)
        {
            result.LogError("Editor is in Play mode; aborting scene edit.");
            return;
        }

        var minimapGO = GameObject.Find("Minimap");
        var minimap = minimapGO.GetComponent<Flusi.Minimap>();

        // Explicitly set the new world-extent fields (Task 1's fields default
        // in C#, but the scene's serialized YAML won't have them until saved).
        var minimapSO = new SerializedObject(minimap);
        minimapSO.FindProperty("worldMin").vector2Value = new Vector2(-11000f, -6000f);
        minimapSO.FindProperty("worldMax").vector2Value = new Vector2(11000f, 6000f);
        minimapSO.ApplyModifiedProperties();

        var bg = new GameObject("TerrainBackground", typeof(RectTransform), typeof(Image), typeof(Flusi.MinimapTerrainRenderer));
        var bgRT = (RectTransform)bg.transform;
        bgRT.SetParent(minimapGO.transform, false);
        bgRT.SetSiblingIndex(0); // draw behind PlaneBlip and POI markers

        var image = bg.GetComponent<Image>();
        image.raycastTarget = false;

        var renderer = bg.GetComponent<Flusi.MinimapTerrainRenderer>();
        var rendererSO = new SerializedObject(renderer);
        rendererSO.FindProperty("minimap").objectReferenceValue = minimap;
        rendererSO.FindProperty("terrain").objectReferenceValue = Terrain.activeTerrain;
        rendererSO.FindProperty("panel").objectReferenceValue = minimapGO.GetComponent<RectTransform>();
        rendererSO.FindProperty("background").objectReferenceValue = image;
        rendererSO.FindProperty("seaLevel").floatValue = 5f;

        var riverMeshesProp = rendererSO.FindProperty("riverMeshes");
        var riverNames = new[] { "RiverA", "RiverB" };
        riverMeshesProp.arraySize = riverNames.Length;
        for (int i = 0; i < riverNames.Length; i++)
        {
            var riverGO = GameObject.Find(riverNames[i]);
            riverMeshesProp.GetArrayElementAtIndex(i).objectReferenceValue = riverGO.GetComponent<MeshFilter>();
        }
        rendererSO.ApplyModifiedProperties();

        result.RegisterObjectCreation(bg);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("TerrainBackground wired and scene saved.");
    }
}
```

- [ ] **Step 2: Verify sibling order and field wiring**

```csharp
using UnityEngine;
using System.Text;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var minimapGO = GameObject.Find("Minimap");
        var sb = new StringBuilder();
        for (int i = 0; i < minimapGO.transform.childCount; i++)
            sb.AppendFormat("child[{0}] = {1}\n", i, minimapGO.transform.GetChild(i).name);
        result.Log(sb.ToString());
    }
}
```

Expected: `child[0] = TerrainBackground`, `child[1] = PlaneBlip` (POI markers
appended after these at runtime).

- [ ] **Step 3: Run EditMode tests**

Same pattern as prior tasks:

```csharp
using UnityEditor;
using Flusi.EditorTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying)
        {
            result.LogError("Editor is in Play mode; cannot start test run.");
            return;
        }
        FlusiTestRunner.RunEditMode();
        result.Log("EditMode run started.");
    }
}
```

Poll: `until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt`

Expected: `STATUS Passed`, `passed=56`.

Exact accounting: baseline 46 already includes the original 3
`MinimapProjectionTests`. Task 1 replaced those 3 with 5 (net +2). Task 2
added 7 (`MinimapTerrainTextureTests`). Task 3 added 1
(`MinimapTerrainRendererTests`). Total: `46 + 2 + 7 + 1 = 56`. If the
reported count differs from 56, treat it as a live-assembly problem per
`CLAUDE.md` ("A green is worthless until proven live") — check
`Unity_GetConsoleLogs` before assuming the tests themselves are wrong.

- [ ] **Step 4: Confirm PlayMode baseline is unchanged**

In a **separate** `Unity_RunCommand` call (never combine with EditMode in
one script — corrupts the test runner):

```csharp
using UnityEditor;
using Flusi.EditorTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying)
        {
            result.LogError("Editor is in Play mode; cannot start test run.");
            return;
        }
        FlusiTestRunner.RunPlayMode();
        result.Log("PlayMode run started.");
    }
}
```

Poll the same way. Expected: `passed=6` (unchanged — this plan touches no
PlayMode/runtime-input code).

- [ ] **Step 5: Visual verification**

Build and check per `CLAUDE.md` (`BuildScript.BuildMac()` via
`Unity_RunCommand` if the Editor is open; check `Builds/macOS/flusi.app`'s
binary mtime, not the bundle directory's, to confirm a rebuild happened —
the tool's own MCP-level "failed" on routine shader warnings is not
trustworthy). Launch the app and confirm:

- Both islands read as green against blue sea.
- Both rivers are visible as blue threads through the green.
- The plane blip and POI markers still land in the correct spot relative to
  the new background (no drift from Task 1's fix).
- The background is letterboxed (thin empty strip top/bottom), not
  stretched/squashed.

Per `CLAUDE.md`, in-session screen capture is unreliable — if
`ScreenCapture`/`screencapture` both fail silently or loudly, ask the owner
to look rather than trusting a silent "success".

- [ ] **Step 6: Commit** (only if Step 1's scene wiring wasn't already
  captured by an earlier commit in this task — scene-only changes still
  need their own commit)

```bash
git add Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Wire the minimap terrain background into the scene

Adds TerrainBackground as the first (bottom) child of Minimap so it
draws behind the plane blip and POI markers, wired to the live
terrain, both river meshes, and the panel's own RectTransform for the
letterbox fit.
EOF
)"
```

---

## Plan Self-Review Notes

- **Spec coverage:** §3 (world-extent fix) → Task 1. §4 (land/water pure
  maths, sea level, rivers) → Task 2 + Task 3's `RiverPolygonFromMesh`. §5
  (texture generation, letterbox fit) → Task 3. §6 (colors) →
  `MinimapTerrainTexture.LandColor`/`WaterColor` in Task 2. §7 (testing) →
  each task's own test file, plus Task 4 Step 3/4 for the full baseline.
- **Type consistency checked:** `Vector2 worldMin/worldMax` used identically
  across `MinimapProjection`, `Minimap.WorldMin`/`WorldMax`, and
  `MinimapTerrainRenderer`; `Color32` used consistently (not `Color`) since
  `Texture2D.SetPixels32` needs it; `MeshFilter[]` (not `Mesh[]` or
  `Transform[]`) consistently for river references, matching
  `RiverPolygonFromMesh`'s parameter type.
- **No placeholders:** every step has complete, runnable code — no TBD/TODO.
