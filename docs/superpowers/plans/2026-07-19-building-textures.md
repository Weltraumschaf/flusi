# Building Textures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Texture the placeholder box buildings — 13 skyscrapers in
`CityA_Skytown` (glass curtain-wall look) and 6 town buildings in
`TownB_Millbrook` (walled/windowed look) — replacing today's flat
untextured gray, with random per-building variety.

**Architecture:** Same throwaway-`Unity_RunCommand` scene-editing pattern as
the landscape-textures and two-island-world plans, plus this project's
existing AI asset-generation tool (`Unity_AssetGeneration_GenerateAsset`)
for the texture content. No new files under `Assets/Scripts`.

**Tech Stack:** Unity 6000.5.4f1 Editor API (`Material`, `MeshRenderer`,
`MaterialPropertyBlock`), the project's asset-generation MCP tool, URP Lit
shader.

## Global Constraints

- Design doc: `docs/superpowers/specs/2026-07-19-building-textures-design.md`.
- Skyscrapers (`CityA_Skytown`, 13 boxes): 3 glass curtain-wall material
  variants, Smoothness raised (~0.65) for a glassy sheen, Metallic left at
  shader default.
- Town buildings (`TownB_Millbrook`, 6 boxes): 3 wall/window material
  variants, default Smoothness/Metallic.
- Style: simple/stylized, tileable, NOT photorealistic PBR — same bar as
  the landscape pass.
- No new geometry, no per-face materials, no reflection probes, no custom
  shaders.
- **Asset generation requires user consent before the FIRST
  `Unity_AssetGeneration_GenerateAsset` call in a session** — send a
  plain-text message listing what will be generated, note it's blocking
  with no ETA, and wait for confirmation before calling the tool. This
  consent gate is per-conversation: a dispatched subagent can't get a reply
  from the real project owner, so **the controller must make every
  `GenerateAsset` call directly**, not delegate it, regardless of which
  execution mode is chosen for the rest of this plan.
- `Unity_AssetGeneration_GenerateAsset` does not overwrite an existing file
  at `savePath` — it silently appends `" 1"` instead. Not a concern for
  this plan's first-time generation, but relevant if any step is re-run.
- `Unity_RunCommand` sandbox: `CommandScript` implements `IRunCommand` and
  nothing else, is the only top-level class.
- Before any scene edit: `EditorApplication.isPlaying` must be false. After:
  `EditorSceneManager.MarkSceneDirty` + `SaveScene`.
- `result.Log` ignores format specifiers.
- Commit: ≤50-char imperative subject, body wrapped at 72.

---

### Task 1: Generate the 6 building textures

**Files:**
- Create: `Assets/Textures/Buildings/Skyscraper_Blue.png`,
  `Assets/Textures/Buildings/Skyscraper_Green.png`,
  `Assets/Textures/Buildings/Skyscraper_Bronze.png`,
  `Assets/Textures/Buildings/Town_Cream.png`,
  `Assets/Textures/Buildings/Town_Brick.png`,
  `Assets/Textures/Buildings/Town_Gray.png`
- (each PNG's paired `.meta` lands automatically on Unity's next import —
  commit it alongside, per this repo's asset/`.meta` rule)

**Interfaces:**
- Produces: 6 tileable PNG textures at the paths above. Task 2 wraps each
  into a `Material`.

This task is **controller-only** (see Global Constraints on the consent
gate) — do not delegate it to an implementer subagent.

- [ ] **Step 1: Get consent, then generate**

Before the first call, send the project owner a plain-text message naming
these 6 assets, noting generation is blocking with no fixed ETA (tens of
seconds to a few minutes per asset, generated in parallel), and wait for
their confirmation on a following turn.

Once confirmed, call `Unity_AssetGeneration_GenerateAsset` 6 times (in
parallel, independent assets) with `command: "GenerateImage"`,
`modelId: "hand-painted-textures-2-0"` (seamless tileable hand-painted
style — matches the design doc's "simple/stylized, not photorealistic"
requirement, and is the same model used for the terrain/water textures),
`width: 1024`, `height: 1024`, `waitForCompletion: true`:

| savePath | prompt |
| --- | --- |
| `Assets/Textures/Buildings/Skyscraper_Blue.png` | "seamless tileable stylized cartoon glass skyscraper facade texture, blue-teal tinted glass windows in a regular grid with thin dark mullions, simple flat shading, hand-painted game art style, no photorealism" |
| `Assets/Textures/Buildings/Skyscraper_Green.png` | "seamless tileable stylized cartoon glass skyscraper facade texture, emerald green tinted glass windows in a regular grid with thin dark mullions, simple flat shading, hand-painted game art style, no photorealism" |
| `Assets/Textures/Buildings/Skyscraper_Bronze.png` | "seamless tileable stylized cartoon glass skyscraper facade texture, warm bronze/amber tinted glass windows in a regular grid with thin dark mullions, simple flat shading, hand-painted game art style, no photorealism" |
| `Assets/Textures/Buildings/Town_Cream.png` | "seamless tileable stylized cartoon small building wall texture, warm cream stucco wall with a regular grid of simple square windows, flat shading, hand-painted game art style, no photorealism" |
| `Assets/Textures/Buildings/Town_Brick.png` | "seamless tileable stylized cartoon small building wall texture, red brick wall with a regular grid of simple square windows, flat shading, hand-painted game art style, no photorealism" |
| `Assets/Textures/Buildings/Town_Gray.png` | "seamless tileable stylized cartoon small building wall texture, light gray concrete panel wall with a regular grid of simple square windows, flat shading, hand-painted game art style, no photorealism" |

- [ ] **Step 2: Verify**

For each of the 6 PNGs: confirm the file exists at its `savePath` and
Unity imported it (check the Console for import errors via
`Unity_GetConsoleLogs`). Visually check each one actually looks like its
name (glass skyscraper vs. walled building, and the right color family)
and tiles reasonably (no obvious hard seam) — per this repo's documented
asset-gen gotcha, a generation can silently miss the "seamless/tileable"
request, so don't just trust that generation succeeded.

If a texture doesn't tile well or doesn't read as intended, regenerate
that one asset only (same `savePath` — remember it appends `" 1"` instead
of overwriting, so move/rename the bad one first via
`AssetDatabase.MoveAssetToTrash`, not `File.Delete`, per this repo's asset
rules).

- [ ] **Step 3: Commit**

```bash
git add Assets/Textures/Buildings/Skyscraper_Blue.png Assets/Textures/Buildings/Skyscraper_Blue.png.meta \
        Assets/Textures/Buildings/Skyscraper_Green.png Assets/Textures/Buildings/Skyscraper_Green.png.meta \
        Assets/Textures/Buildings/Skyscraper_Bronze.png Assets/Textures/Buildings/Skyscraper_Bronze.png.meta \
        Assets/Textures/Buildings/Town_Cream.png Assets/Textures/Buildings/Town_Cream.png.meta \
        Assets/Textures/Buildings/Town_Brick.png Assets/Textures/Buildings/Town_Brick.png.meta \
        Assets/Textures/Buildings/Town_Gray.png Assets/Textures/Buildings/Town_Gray.png.meta
git commit -m "$(cat <<'EOF'
Generate skyscraper and town building textures

Six stylized tileable textures: 3 glass curtain-wall variants for
skyscrapers, 3 wall/window variants for town buildings. Not yet
wired into any Material.
EOF
)"
```

---

### Task 2: Create the 6 building materials

**Files:**
- Create: `Assets/Materials/Skyscraper_Blue.mat`,
  `Assets/Materials/Skyscraper_Green.mat`,
  `Assets/Materials/Skyscraper_Bronze.mat`,
  `Assets/Materials/Town_Cream.mat`,
  `Assets/Materials/Town_Brick.mat`,
  `Assets/Materials/Town_Gray.mat`

**Interfaces:**
- Consumes: the 6 PNGs from Task 1 (`Assets/Textures/Buildings/*.png`).
- Produces: 6 URP/Lit `Material` assets at the paths above. Task 3 loads
  them by exact path and assigns them to buildings.

- [ ] **Step 1: Create the materials**

Run via `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        string[] skyscraperNames = { "Skyscraper_Blue", "Skyscraper_Green", "Skyscraper_Bronze" };
        string[] townNames = { "Town_Cream", "Town_Brick", "Town_Gray" };
        int created = 0;

        foreach (var n in skyscraperNames)
        {
            var mat = new Material(shader) { name = n };
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Buildings/" + n + ".png");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.65f);
            AssetDatabase.CreateAsset(mat, "Assets/Materials/" + n + ".mat");
            result.RegisterObjectCreation(mat);
            created++;
        }

        foreach (var n in townNames)
        {
            var mat = new Material(shader) { name = n };
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Buildings/" + n + ".png");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, "Assets/Materials/" + n + ".mat");
            result.RegisterObjectCreation(mat);
            created++;
        }

        result.Log("Created {0} building materials", created);
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `Created 6 building materials`. Confirm via a second query
that each material at `Assets/Materials/<Name>.mat` loads with
`GetTexture("_BaseMap") != null`, and that the 3 skyscraper materials each
report `GetFloat("_Smoothness") == 0.65` while the 3 town materials report
the shader's unmodified default (don't hardcode an expected default value —
just confirm it wasn't touched, e.g. by comparing against a freshly
constructed `new Material(shader).GetFloat("_Smoothness")`).

- [ ] **Step 3: Commit**

```bash
git add Assets/Materials/Skyscraper_Blue.mat Assets/Materials/Skyscraper_Blue.mat.meta \
        Assets/Materials/Skyscraper_Green.mat Assets/Materials/Skyscraper_Green.mat.meta \
        Assets/Materials/Skyscraper_Bronze.mat Assets/Materials/Skyscraper_Bronze.mat.meta \
        Assets/Materials/Town_Cream.mat Assets/Materials/Town_Cream.mat.meta \
        Assets/Materials/Town_Brick.mat Assets/Materials/Town_Brick.mat.meta \
        Assets/Materials/Town_Gray.mat Assets/Materials/Town_Gray.mat.meta
git commit -m "$(cat <<'EOF'
Add skyscraper and town building materials

URP/Lit materials wrapping the generated building textures. The 3
skyscraper variants get a raised Smoothness for a glassy sheen; the
3 town variants stay at shader defaults. Not yet assigned to any
building in the scene.
EOF
)"
```

---

### Task 3: Assign materials to buildings, with per-building tiling correction

**Files:**
- Modify: `Assets/Scenes/MainScene.unity` (`CityA_Skytown` and
  `TownB_Millbrook` children's `MeshRenderer` material + property block)

**Interfaces:**
- Consumes: the 6 materials from Task 2
  (`Assets/Materials/{Skyscraper_Blue,Skyscraper_Green,Skyscraper_Bronze,
  Town_Cream,Town_Brick,Town_Gray}.mat`), and the existing
  `CityA_Skytown`/`TownB_Millbrook` GameObjects created by the two-island
  world plan (13 and 6 primitive-cube children respectively, each a
  `PrimitiveType.Cube` with non-uniform `localScale`).

Box primitive UVs map each face `0..1` regardless of the box's actual
scale. Skyscraper boxes are 60 units wide but 30-150 tall — applying the
texture with no tiling adjustment stretches the window grid vertically on
tall buildings. Fix: for each building, set a `MaterialPropertyBlock`
override on `_BaseMap_ST` so the vertical tile count scales with the
height/width ratio, keeping windows roughly square without duplicating the
shared material.

- [ ] **Step 1: Assign materials and per-building tiling**

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

        var skyscraperMats = new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Skyscraper_Blue.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Skyscraper_Green.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Skyscraper_Bronze.mat"),
        };
        var townMats = new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Town_Cream.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Town_Brick.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Town_Gray.mat"),
        };

        var skyscraperCluster = GameObject.Find("CityA_Skytown");
        var townCluster = GameObject.Find("TownB_Millbrook");
        var block = new MaterialPropertyBlock();

        int skyscraperCount = 0;
        var skyRng = new System.Random(7001);
        for (int i = 0; i < skyscraperCluster.transform.childCount; i++)
        {
            var mr = skyscraperCluster.transform.GetChild(i).GetComponent<MeshRenderer>();
            if (mr == null) continue;
            result.RegisterObjectModification(mr);
            mr.sharedMaterial = skyscraperMats[skyRng.Next(skyscraperMats.Length)];

            var scale = mr.transform.localScale;
            float tileY = Mathf.Max(1f, scale.y / scale.x);
            mr.GetPropertyBlock(block);
            block.SetVector("_BaseMap_ST", new Vector4(1f, tileY, 0f, 0f));
            mr.SetPropertyBlock(block);
            skyscraperCount++;
        }

        int townCount = 0;
        var townRng = new System.Random(7002);
        for (int i = 0; i < townCluster.transform.childCount; i++)
        {
            var mr = townCluster.transform.GetChild(i).GetComponent<MeshRenderer>();
            if (mr == null) continue;
            result.RegisterObjectModification(mr);
            mr.sharedMaterial = townMats[townRng.Next(townMats.Length)];

            var scale = mr.transform.localScale;
            float tileY = Mathf.Max(1f, scale.y / scale.x);
            mr.GetPropertyBlock(block);
            block.SetVector("_BaseMap_ST", new Vector4(1f, tileY, 0f, 0f));
            mr.SetPropertyBlock(block);
            townCount++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        result.Log("Assigned materials: skyscrapers={0}, town buildings={1}", skyscraperCount, townCount);
    }
}
```

- [ ] **Step 2: Verify**

Expected log: `Assigned materials: skyscrapers=13, town buildings=6`
(matches the two-island plan's known child counts). Confirm via a second
query that every child of `CityA_Skytown` has a `sharedMaterial.name`
starting with `"Skyscraper_"` and every child of `TownB_Millbrook` has one
starting with `"Town_"` — none left on the default gray material. Also
confirm the assignment isn't degenerate (e.g. not every building landing
on the same variant by RNG bad luck) by logging the distinct material
names actually used in each cluster; expect all 3 variants represented in
each 6-13 building cluster (reroll the seed if one variant is entirely
unused — cosmetic, not worth deep statistical rigor).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scenes/MainScene.unity
git commit -m "$(cat <<'EOF'
Texture skyscrapers and town buildings

Randomly assigns one of 3 glass-tint materials to each Skytown
skyscraper and one of 3 wall materials to each Millbrook town
building, with per-building tiling so the window grid stays
roughly square on both short and tall boxes.
EOF
)"
```

---

### Task 4: Visual verification and regression check

**Files:**
- None modified (verification only), unless a fix is needed.

**Interfaces:**
- Consumes: the fully textured buildings from Tasks 1-3.

- [ ] **Step 1: Run the existing automated suite**

Via `Unity_RunCommand`, call `Flusi.EditorTools.FlusiTestRunner.RunEditMode()`
then `RunPlayMode()` in **separate** `Unity_RunCommand` invocations, polling
each in the same turn it was started:
`until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt`.
Expected: `STATUS Passed`, EditMode 46 / PlayMode 6 — unchanged from
baseline (this plan adds no pure-logic code; grep
`Assets/Tests/EditMode/*.cs` for `[Test]`/`[UnityTest]` first if the count
looks off, per this repo's documented baseline-drift caveat).

- [ ] **Step 2: Visual check**

Force the Game view open (`EditorApplication.ExecuteMenuItem("Window/General/Game")`),
enter Play mode, fly or teleport the camera near `CityA_Skytown`
(`(-9000, 200, 1500)` world, looking down at the cluster) and
`ScreenCapture.CaptureScreenshot` a frame, then do the same near
`TownB_Millbrook` (`(8500, 100, -2300)`). Exit Play mode afterward (poll
`isPlaying` before the next call). If capture fails in this session (a
known, documented unreliability in this repo), say so honestly and ask the
project owner to eyeball it instead of claiming an unconfirmed success.

Confirm from whatever you can see: skyscrapers read as tinted glass and
visibly differ from each other (not all one color), town buildings read as
walled/windowed and visibly differ from each other, and neither cluster is
flat gray anymore. Also sanity-check that tall skyscrapers don't show
obviously stretched/smeared windows (the Task 3 tiling fix should have
prevented this).

- [ ] **Step 3: Commit (only if a fix was needed)**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Fix issues found in building texture verification pass

EOF
)"
```

If nothing needed fixing, no commit — Tasks 1-3 already captured the work.

---

## Self-Review Notes

- **Spec coverage:** texture generation (Task 1) → design doc §4/§5/§7;
  material creation with skyscraper Smoothness bump (Task 2) → §4/§5;
  random per-building assignment (Task 3) → §6; verification (Task 4) →
  §8. All design doc sections have a task.
- **Type/name consistency checked:** material asset names/paths
  (`Skyscraper_Blue`/`Green`/`Bronze`, `Town_Cream`/`Brick`/`Gray`) are
  identical between where they're created (Task 2's `AssetDatabase.CreateAsset`
  path) and where they're loaded (Task 3's `AssetDatabase.LoadAssetAtPath`
  path). GameObject names (`CityA_Skytown`, `TownB_Millbrook`) match the
  two-island plan's actual output; child counts (13, 6) match its logged
  result.
- **No placeholders:** every prompt, path, model ID, and numeric value
  (Smoothness 0.65, RNG seeds) is concrete. The tiling-correction addition
  in Task 3 isn't in the design doc's text but doesn't contradict it —
  it's an implementation detail serving the design's "nicer" visual bar,
  same latitude the landscape plan used for its blend thresholds.
- **Consent-gate handling:** Task 1 is explicitly controller-only, with
  reasoning spelled out in Global Constraints, so whoever executes this
  plan doesn't accidentally delegate it and hit a dead-end where a
  subagent asks a question the real user never sees.
