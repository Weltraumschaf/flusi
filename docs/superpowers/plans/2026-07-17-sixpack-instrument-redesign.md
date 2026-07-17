# Six-Pack Instrument Visual Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each of the six sixpack gauges a generated, simplified-realistic background face (bezel, ticks, numerals, colour arcs) while leaving every needle/card rotation script untouched.

**Architecture:** Presentation-only. Generate one (or two) sprite(s) per gauge via `Unity_AssetGeneration_GenerateAsset`, import as a UI Sprite, assign onto the gauge's existing `Image` component (or a new sibling `Image` where the current hierarchy has no background object to reuse), then disable the `GaugeFaceBuilder` component (or delete its now-superseded manually-authored children) on that gauge so procedural ticks don't double up with the baked art.

**Tech Stack:** Unity 6000.5.2f1, URP, `UnityEngine.UI` (uGUI, not UI Toolkit). All scene edits done via throwaway C# in `Unity_RunCommand`, per CLAUDE.md.

## Global Constraints

- No `IAircraftState` field, no `GaugeChannel`/`LampChannel` enum value, no calibration number (`minValue`/`maxValue`/`startAngle`/`sweepAngle`) changes anywhere in this plan — every gauge's existing needle/card math is authoritative and untouched.
- New asset `.meta` files land in the **same commit** as the asset they describe (CLAUDE.md rule).
- Scene edits (`MarkSceneDirty` + `SaveScene`) throw in Play mode — every wiring step must check `EditorApplication.isPlaying` is `false` first.
- `Unity_AssetGeneration_GenerateAsset` requires the user's explicit go-ahead once per conversation before the first call (the tool blocks the agent until every generation in a batch completes, with no fixed ETA). Ask before Task 1 Step 1's generation call if this is a fresh conversation.
- EditMode 41 / PlayMode 6 is the current test baseline (may have grown since spec 2026-07-16 — check `Temp/flusi-tests.txt` output for the actual current numbers before the first task and treat that as the baseline for every subsequent regression check in this plan).
- `CommandScript` in every `Unity_RunCommand` call must implement `IRunCommand` and be the only top-level class (sandbox rule, CLAUDE.md).

---

## Reference geometry (established during planning, do not re-derive)

All six gauge roots are `RectTransform`s with `sizeDelta = (150, 150)`, anchored at a point (not stretched), `localScale = (1, 1)`. Their `Face` children (where present) stretch to fill the parent exactly (`anchorMin (0,0)`, `anchorMax (1,1)`, `sizeDelta (0,0)`), i.e. the Face `Image` renders at 150×150 px before canvas scaling.

Live hierarchy (`SixPack/...`), captured from the current scene:

```
Airspeed [NeedleGauge]
  Face [Image(sprite=Knob, color≈#0D0D0F) GaugeFaceBuilder(start=0 sweep=320 ticks=21 majorEvery=4 radius=60 labelMin=0 labelMax=500 labelRadius=44)]
  Needle [Image(sprite=null, 4x58)]
  Hub [Image(sprite=Knob, 14x14)]
Attitude [AttitudeIndicator]
  Ball [Image(sprite=Knob, white) Mask]
    BankRoot [150x150]
      PitchCard [400x600, no Image component]
        Sky [Image(sprite=null, color blue, 400x300, anchoredPos y=150)]
        Ground [Image(sprite=null, color brown, 400x300, anchoredPos y=-150)]
        HorizonLine [Image(sprite=null, white, 400x3)]
  RollPointer [12x72]
    Tip [Image(sprite=Knob, orange, 11x11, anchoredPos y=-5)]
  AircraftSymbol [Image(sprite=null, orange, 60x3)]   -- sibling of Ball, NOT masked, already fixed
  AircraftDot [Image(sprite=Knob, orange, 9x9)]        -- sibling of Ball, NOT masked, already fixed
Altimeter [Altimeter]
  Face [Image(sprite=Knob) GaugeFaceBuilder(start=0 sweep=324 ticks=10 majorEvery=1 labelMin=0 labelMax=9)]
  ThousandsNeedle [Image(sprite=null, 7x38)]
  HundredsNeedle [Image(sprite=null, 4x58)]
  Hub [Image(sprite=Knob, 14x14)]
TurnCoordinator [NeedleGauge]
  Face [Image(sprite=Knob) GaugeFaceBuilder(start=-30 sweep=60 ticks=7 majorEvery=3 showLabels=False)]
  Needle [Image(sprite=null, 4x58)]
  Hub [Image(sprite=Knob, 14x14)]
  LabelL, LabelR [Text]           -- already hand-authored, keep as-is
  SlipTube [Image(sprite=UISprite, sliced, 46x16)]   -- already hand-authored, keep as-is
  SlipBall [Image(sprite=Knob, 12x12)]                -- already hand-authored, keep as-is
Heading [HeadingIndicator]
  Face [Image(sprite=Knob)]                            -- no GaugeFaceBuilder, plain circle
  Card [GaugeFaceBuilder(sweep=350 ticks=36 majorEvery=3 showLabels=False), no Image component]
    CardN, CardE, CardS, CardW [Text]   -- hand-placed cardinal letters
  LubberLine [Image(sprite=null, orange, 4x14, anchoredPos y=68)]   -- already fixed, keep as-is
VerticalSpeed [NeedleGauge]
  Face [Image(sprite=Knob) GaugeFaceBuilder(start=185 sweep=170 ticks=9 majorEvery=2 labelMin=-100 labelMax=100)]
  Needle [Image(sprite=null, 4x58)]
  Hub [Image(sprite=Knob, 14x14)]
```

Every gauge also has a `Caption` `Text` child (e.g. "AIRSPEED") — untouched by this plan.

**Sprite import settings**, applied to every generated PNG before it's usable as a UI `Image.sprite`:

```csharp
var importer = (TextureImporter)AssetImporter.GetAtPath(path);
importer.textureType = TextureImporterType.Sprite;
importer.spriteImportMode = SpriteImportMode.Single;
importer.alphaIsTransparency = true;
importer.mipmapEnabled = false;
importer.SaveAndReimport();
```

---

## Task 1: Airspeed Indicator background

**Files:**
- Create: `Assets/Sprites/Cockpit/AirspeedFace.png` (+ `.meta`)
- Modify: `Assets/Scenes/MainScene.unity` (via `Unity_RunCommand`, not hand-edited)

**Interfaces:**
- Consumes: nothing from other tasks — first task, creates the `Assets/Sprites/Cockpit/` folder.
- Produces: the `Assets/Sprites/Cockpit/` folder (Tasks 2–6 save into it), the sprite-import-settings snippet above (reused verbatim by Tasks 2–6).

- [ ] **Step 1: Get user go-ahead, then generate the face art**

Tell the user: "About to generate the Airspeed Indicator background (a black gauge face with baked ticks, numerals, and a green/yellow/red speed arc). This call blocks until it finishes — usually tens of seconds, sometimes a few minutes. OK to proceed?" Wait for confirmation.

Then call:

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/AirspeedFace.png",
  "prompt": "Simplified-realistic light-aircraft cockpit gauge face, flat clean illustration (not photoreal, no dirt/glare/screws/reflections). Matte dark charcoal (#141416) circular face filling the frame edge to edge, thin 4% darker bezel rim. Crisp white tick marks and numerals '0' '100' '200' '300' '400' '500' arranged clockwise from the 12 o'clock position sweeping about 320 degrees (leaving a 40-degree gap at top-left), major ticks every 100, minor ticks every 25. A colour arc band following the ticks: green from 150 to 400, yellow/amber from 400 to 460, a red radial line at 468. Small white text caption near the bottom reading 'KM/H'. Centered, top-down orthographic view, transparent PNG background outside the circle."
}
```

- [ ] **Step 2: Fix sprite import settings and verify it loaded**

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        const string path = "Assets/Sprites/Cockpit/AirspeedFace.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) { result.LogError("No importer at " + path); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) { result.LogError("Sprite failed to load after reimport: " + path); return; }
        result.Log("Sprite loaded OK: {0}", sprite);
    }
}
```

Expected: log line "Sprite loaded OK: AirspeedFace (UnityEngine.Sprite)", no errors.

- [ ] **Step 3: Wire the sprite onto Airspeed/Face and disable its GaugeFaceBuilder**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Flusi;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("Must be in Edit mode"); return; }

        var face = GameObject.Find("SixPack/Airspeed/Face");
        if (face == null) { result.LogError("SixPack/Airspeed/Face not found"); return; }

        var image = face.GetComponent<Image>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/AirspeedFace.png");
        if (sprite == null) { result.LogError("AirspeedFace sprite not found"); return; }

        var so = new SerializedObject(image);
        so.FindProperty("m_Sprite").objectReferenceValue = sprite;
        so.FindProperty("m_Color").colorValue = Color.white;
        so.ApplyModifiedProperties();
        result.RegisterObjectModification(image);

        var builder = face.GetComponent<GaugeFaceBuilder>();
        if (builder != null)
        {
            builder.enabled = false;
            result.RegisterObjectModification(builder);
        }

        for (int i = face.transform.childCount - 1; i >= 0; i--)
        {
            var child = face.transform.GetChild(i).gameObject;
            if (child.name == "Tick" || child.name == "Label")
                result.DestroyObject(child);
        }

        EditorSceneManager.MarkSceneDirty(face.scene);
        EditorSceneManager.SaveScene(face.scene);
        result.Log("Wired AirspeedFace sprite, disabled GaugeFaceBuilder, cleared old ticks/labels.");
    }
}
```

Expected: log line confirming the wire, no errors.

- [ ] **Step 4: Run the EditMode/PlayMode regression baseline**

```csharp
using UnityEngine;
using Flusi.EditorTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        FlusiTestRunner.RunEditMode();
        result.Log("EditMode run started; poll {0}", FlusiTestRunner.ResultPath);
    }
}
```

Poll `Temp/flusi-tests.txt` (Read tool) until it contains `STATUS Passed`. Expected: `STATUS Passed`, same or greater test count than the Global-Constraints baseline, and `Unity_GetConsoleLogs` (`logTypes: "error"`) empty. If the count is unexpectedly different, read the console before proceeding (CLAUDE.md stale-pass caveat).

- [ ] **Step 5: Dump RectTransform state for owner review**

```csharp
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var face = GameObject.Find("SixPack/Airspeed/Face");
        var img = face.GetComponent<UnityEngine.UI.Image>();
        result.Log("Airspeed/Face sprite={0} enabled(GaugeFaceBuilder)={1}",
            img.sprite ? img.sprite.name : "null",
            face.GetComponent<Flusi.GaugeFaceBuilder>()?.enabled);
    }
}
```

Ask the project owner to look at the Airspeed gauge in the Editor (Play mode, cockpit view) before continuing to Task 2.

- [ ] **Step 6: Commit**

```bash
git add Assets/Sprites/Cockpit/AirspeedFace.png Assets/Sprites/Cockpit/AirspeedFace.png.meta \
        Assets/Sprites/Cockpit.meta Assets/Sprites.meta Assets/Scenes/MainScene.unity
git commit -m "Give Airspeed Indicator a generated gauge face"
```

(`Assets/Sprites.meta` and `Assets/Sprites/Cockpit.meta` only exist if this is genuinely the first asset in a newly-created folder — check `git status` first and only add what's actually new/modified.)

---

## Task 2: Attitude Indicator background

Two separate art pieces: the pitch card (inside the mask, pans/rotates with the aircraft) and a fixed bank-tick ring (outside the mask's content, drawn as a new sibling so it overlays the horizon at the rim without being clipped by it).

**Files:**
- Create: `Assets/Sprites/Cockpit/AttitudePitchCard.png`, `Assets/Sprites/Cockpit/AttitudeBankRing.png` (+ `.meta` each)
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: `Assets/Sprites/Cockpit/` folder and the import-settings snippet from Task 1.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Generate the pitch card art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 768,
  "savePath": "Assets/Sprites/Cockpit/AttitudePitchCard.png",
  "prompt": "Simplified-realistic artificial-horizon pitch card for a light-aircraft attitude indicator, flat clean illustration not photoreal. Top half solid sky blue (#3D8CD9), bottom half solid earth brown (#734D29), a crisp white horizon line at the vertical center. White pitch-ladder tick lines above and below the horizon line at roughly 10, 20, 30 degree spacing, each with a short white numeral label (10, 20, 30) near the line's end, mirrored above (sky, climb) and below (ground, dive). No vignette, no clouds, no texture, no gradient — solid flat colours only, full-bleed rectangle, no transparency, no border."
}
```

- [ ] **Step 2: Generate the bank-tick ring art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/AttitudeBankRing.png",
  "prompt": "A thin ring of white tick marks for the bezel of a light-aircraft attitude indicator, viewed top-down. Square canvas, fully transparent everywhere except a narrow band at the very outer edge (outer 15% of the radius): short white tick marks at 0, 10, 20, 30, 60 degrees either side of top-center, with the 0/10/20/30 ticks slightly shorter and the 60-degree ticks slightly longer. No numerals, no circle fill, no background — everything except the tick marks must be fully transparent alpha."
}
```

- [ ] **Step 3: Fix import settings on both sprites and verify they loaded**

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string[] paths = {
            "Assets/Sprites/Cockpit/AttitudePitchCard.png",
            "Assets/Sprites/Cockpit/AttitudeBankRing.png",
        };
        foreach (var path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { result.LogError("No importer at " + path); continue; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) result.LogError("Sprite failed to load: " + path);
            else result.Log("Sprite loaded OK: {0}", sprite);
        }
    }
}
```

Expected: two "Sprite loaded OK" lines, no errors.

- [ ] **Step 4: Wire both sprites into the Attitude hierarchy**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("Must be in Edit mode"); return; }

        var attitude = GameObject.Find("SixPack/Attitude");
        var pitchCard = GameObject.Find("SixPack/Attitude/Ball/BankRoot/PitchCard");
        if (attitude == null || pitchCard == null) { result.LogError("Attitude hierarchy not found"); return; }

        // 1. Add an Image to PitchCard carrying the sky/ground/ladder art.
        var pitchImage = pitchCard.GetComponent<Image>();
        if (pitchImage == null)
        {
            pitchImage = pitchCard.AddComponent<Image>();
            result.RegisterObjectCreation(pitchImage);
        }
        var pitchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/AttitudePitchCard.png");
        if (pitchSprite == null) { result.LogError("AttitudePitchCard sprite not found"); return; }
        pitchImage.sprite = pitchSprite;
        pitchImage.color = Color.white;
        pitchImage.type = Image.Type.Simple;
        pitchImage.raycastTarget = false;

        // 2. Retire the flat-colour Sky/Ground/HorizonLine children, now baked into the art.
        foreach (var childName in new[] { "Sky", "Ground", "HorizonLine" })
        {
            var child = pitchCard.transform.Find(childName);
            if (child != null) result.DestroyObject(child.gameObject);
        }

        // 3. Insert the bank-tick ring as a new sibling right after Ball, so it draws
        //    over the horizon but below the fixed RollPointer/AircraftSymbol/AircraftDot.
        var ringGo = new GameObject("BankRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        result.RegisterObjectCreation(ringGo);
        var ringRt = (RectTransform)ringGo.transform;
        ringRt.SetParent(attitude.transform, false);
        ringRt.anchorMin = Vector2.zero;
        ringRt.anchorMax = Vector2.one;
        ringRt.sizeDelta = Vector2.zero;
        ringRt.anchoredPosition = Vector2.zero;
        ringGo.transform.SetSiblingIndex(1); // right after Ball (index 0)

        var ringImage = ringGo.GetComponent<Image>();
        var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/AttitudeBankRing.png");
        if (ringSprite == null) { result.LogError("AttitudeBankRing sprite not found"); return; }
        ringImage.sprite = ringSprite;
        ringImage.color = Color.white;
        ringImage.raycastTarget = false;

        EditorSceneManager.MarkSceneDirty(attitude.scene);
        EditorSceneManager.SaveScene(attitude.scene);
        result.Log("Wired AttitudePitchCard onto PitchCard, added BankRing at sibling index 1, removed flat Sky/Ground/HorizonLine.");
    }
}
```

Expected: log line confirming the wire, no errors. Note: `PitchCard`'s `RectTransform.sizeDelta` stays `(400, 600)` — unchanged, so the 512×768 art (same ~2:3 aspect) fills it without stretching oddly.

- [ ] **Step 5: Run the regression baseline**

Same code as Task 1 Step 4. Expected: same as Task 1 Step 4.

- [ ] **Step 6: Dump state for owner review**

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var pitchCard = GameObject.Find("SixPack/Attitude/Ball/BankRoot/PitchCard");
        var ring = GameObject.Find("SixPack/Attitude/BankRing");
        result.Log("PitchCard has Image={0}, BankRing exists={1}, BankRing sibling index={2}",
            pitchCard.GetComponent<Image>() != null, ring != null, ring != null ? ring.transform.GetSiblingIndex() : -1);
    }
}
```

Ask the project owner to check the Attitude Indicator in Play mode — bank and pitch by moving the aircraft, confirm the horizon art tracks and the bank ring shows around the rim without being covered by the horizon.

- [ ] **Step 7: Commit**

```bash
git add Assets/Sprites/Cockpit/AttitudePitchCard.png Assets/Sprites/Cockpit/AttitudePitchCard.png.meta \
        Assets/Sprites/Cockpit/AttitudeBankRing.png Assets/Sprites/Cockpit/AttitudeBankRing.png.meta \
        Assets/Scenes/MainScene.unity
git commit -m "Give Attitude Indicator generated horizon and bank-ring art"
```

---

## Task 3: Altimeter background

**Files:**
- Create: `Assets/Sprites/Cockpit/AltimeterFace.png` (+ `.meta`)
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: `Assets/Sprites/Cockpit/` folder, import-settings snippet (Task 1).
- Produces: nothing consumed later.

- [ ] **Step 1: Generate the face art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/AltimeterFace.png",
  "prompt": "Simplified-realistic light-aircraft altimeter gauge face, flat clean illustration not photoreal, no dirt/glare/screws/reflections. Matte dark charcoal (#141416) circular face filling the frame edge to edge, thin 4% darker bezel rim. Crisp white numerals 0 through 9 evenly spaced clockwise around almost the full circle (a small gap of about 36 degrees at the top), with a white major tick at each numeral and a shorter minor tick halfway between each pair. No colour arcs. Small white text caption near the bottom reading 'ALT · 100 M'. Centered, top-down orthographic view, transparent PNG background outside the circle."
}
```

- [ ] **Step 2: Fix import settings and verify**

Same code pattern as Task 1 Step 2, with `path = "Assets/Sprites/Cockpit/AltimeterFace.png"`.

- [ ] **Step 3: Wire the sprite onto Altimeter/Face and disable its GaugeFaceBuilder**

Same code pattern as Task 1 Step 3, with `"SixPack/Airspeed/Face"` replaced by `"SixPack/Altimeter/Face"` and the sprite path replaced by `"Assets/Sprites/Cockpit/AltimeterFace.png"`.

- [ ] **Step 4: Run the regression baseline**

Same code as Task 1 Step 4.

- [ ] **Step 5: Dump state for owner review**

Same code pattern as Task 1 Step 5, with the path replaced by `"SixPack/Altimeter/Face"`.

Ask the owner to check the Altimeter in Play mode, gaining altitude to confirm both needles still track correctly against the new face.

- [ ] **Step 6: Commit**

```bash
git add Assets/Sprites/Cockpit/AltimeterFace.png Assets/Sprites/Cockpit/AltimeterFace.png.meta \
        Assets/Scenes/MainScene.unity
git commit -m "Give Altimeter a generated gauge face"
```

---

## Task 4: Turn Coordinator background and needle

**Files:**
- Create: `Assets/Sprites/Cockpit/TurnCoordinatorFace.png`, `Assets/Sprites/Cockpit/TurnCoordinatorAirplane.png` (+ `.meta` each)
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: `Assets/Sprites/Cockpit/` folder, import-settings snippet (Task 1). Leaves `LabelL`, `LabelR`, `SlipTube`, `SlipBall`, `Caption` untouched — they're already hand-authored and correct per spec.
- Produces: nothing consumed later.

- [ ] **Step 1: Generate the face art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/TurnCoordinatorFace.png",
  "prompt": "Simplified-realistic light-aircraft turn-coordinator gauge face, flat clean illustration not photoreal, no dirt/glare/screws/reflections. Matte dark charcoal (#141416) circular face filling the frame edge to edge, thin 4% darker bezel rim. NOT numbered like a clock — instead two short white reference tick marks positioned at roughly 30 degrees left and right of top-center (standard-turn index marks), each with a tiny white aeroplane-wingtip-shaped mark beside it. Leave the center and the lower two-thirds of the face empty/plain (a slip indicator will be added there separately). Centered, top-down orthographic view, transparent PNG background outside the circle."
}
```

- [ ] **Step 2: Generate the airplane-silhouette needle art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 256,
  "height": 192,
  "savePath": "Assets/Sprites/Cockpit/TurnCoordinatorAirplane.png",
  "prompt": "A small white silhouette of a single-engine light aircraft viewed directly from behind (rear view), wings spanning nearly the full width, a short vertical tail fin at the center, simple flat solid white silhouette with no shading or gradient or outline colour, small orange wingtip accent dots optional. Fully transparent background everywhere except the silhouette shape. Centered in frame with generous transparent margin on all sides so the shape can rotate without clipping."
}
```

- [ ] **Step 3: Fix import settings on both sprites and verify**

Same code pattern as Task 2 Step 3, with the two paths replaced by `"Assets/Sprites/Cockpit/TurnCoordinatorFace.png"` and `"Assets/Sprites/Cockpit/TurnCoordinatorAirplane.png"`.

- [ ] **Step 4: Wire both sprites, resize the needle, disable the Face's GaugeFaceBuilder**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Flusi;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("Must be in Edit mode"); return; }

        var face = GameObject.Find("SixPack/TurnCoordinator/Face");
        var needle = GameObject.Find("SixPack/TurnCoordinator/Needle");
        if (face == null || needle == null) { result.LogError("TurnCoordinator hierarchy not found"); return; }

        var faceImage = face.GetComponent<Image>();
        var faceSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/TurnCoordinatorFace.png");
        if (faceSprite == null) { result.LogError("TurnCoordinatorFace sprite not found"); return; }
        var faceSo = new SerializedObject(faceImage);
        faceSo.FindProperty("m_Sprite").objectReferenceValue = faceSprite;
        faceSo.FindProperty("m_Color").colorValue = Color.white;
        faceSo.ApplyModifiedProperties();
        result.RegisterObjectModification(faceImage);

        var builder = face.GetComponent<GaugeFaceBuilder>();
        if (builder != null)
        {
            builder.enabled = false;
            result.RegisterObjectModification(builder);
        }
        for (int i = face.transform.childCount - 1; i >= 0; i--)
        {
            var child = face.transform.GetChild(i).gameObject;
            if (child.name == "Tick" || child.name == "Label")
                result.DestroyObject(child);
        }

        var needleImage = needle.GetComponent<Image>();
        var needleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/TurnCoordinatorAirplane.png");
        if (needleSprite == null) { result.LogError("TurnCoordinatorAirplane sprite not found"); return; }
        var needleSo = new SerializedObject(needleImage);
        needleSo.FindProperty("m_Sprite").objectReferenceValue = needleSprite;
        needleSo.FindProperty("m_Color").colorValue = Color.white;
        needleSo.ApplyModifiedProperties();
        result.RegisterObjectModification(needleImage);

        var needleRt = (RectTransform)needle.transform;
        Undo.RecordObject(needleRt, "Resize TC needle");
        needleRt.sizeDelta = new Vector2(56f, 42f); // matches the 256x192 art's 4:3 aspect
        result.RegisterObjectModification(needleRt);

        EditorSceneManager.MarkSceneDirty(face.scene);
        EditorSceneManager.SaveScene(face.scene);
        result.Log("Wired TurnCoordinatorFace + airplane needle, resized needle to 56x42, disabled GaugeFaceBuilder.");
    }
}
```

Expected: log line confirming the wire, no errors.

- [ ] **Step 5: Run the regression baseline**

Same code as Task 1 Step 4.

- [ ] **Step 6: Dump state for owner review**

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var face = GameObject.Find("SixPack/TurnCoordinator/Face");
        var needle = GameObject.Find("SixPack/TurnCoordinator/Needle");
        result.Log("Face sprite={0}, Needle sprite={1}, Needle size={2}",
            face.GetComponent<Image>().sprite?.name,
            needle.GetComponent<Image>().sprite?.name,
            ((RectTransform)needle.transform).sizeDelta);
    }
}
```

Ask the owner to check the Turn Coordinator in Play mode while banking — confirm the airplane silhouette rocks left/right believably and the reference marks/L/R labels/slip ball still read clearly.

- [ ] **Step 7: Commit**

```bash
git add Assets/Sprites/Cockpit/TurnCoordinatorFace.png Assets/Sprites/Cockpit/TurnCoordinatorFace.png.meta \
        Assets/Sprites/Cockpit/TurnCoordinatorAirplane.png Assets/Sprites/Cockpit/TurnCoordinatorAirplane.png.meta \
        Assets/Scenes/MainScene.unity
git commit -m "Give Turn Coordinator generated face and airplane needle"
```

---

## Task 5: Heading Indicator bezel and compass card

**Files:**
- Create: `Assets/Sprites/Cockpit/HeadingBezel.png`, `Assets/Sprites/Cockpit/HeadingCard.png` (+ `.meta` each)
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: `Assets/Sprites/Cockpit/` folder, import-settings snippet (Task 1). Leaves `LubberLine` untouched (already fixed and correct).
- Produces: nothing consumed later.

- [ ] **Step 1: Generate the fixed bezel art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/HeadingBezel.png",
  "prompt": "Simplified-realistic light-aircraft heading-indicator bezel, flat clean illustration not photoreal, no dirt/glare/screws/reflections. Matte dark charcoal (#141416) circular face filling the frame edge to edge, thin 4% darker bezel rim, otherwise completely plain and empty in the middle (a rotating compass card will show through). Centered, top-down orthographic view, transparent PNG background outside the circle."
}
```

- [ ] **Step 2: Generate the rotating compass-card art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/HeadingCard.png",
  "prompt": "Simplified-realistic compass rose card for a light-aircraft heading indicator, flat clean illustration not photoreal. Circular card, matte dark charcoal (#141416) background filling the frame. Bold white cardinal letters N, E, S, W at 0/90/180/270 degrees, white numerals 3/6/9/12/15/18/21/24 (representing 30/60/120/150/210/240/300/330 degrees) at the intermediate 30-degree marks, a white tick mark every 10 degrees around the full circle (longer ticks at the 30-degree numeral positions). Centered, top-down orthographic view, transparent PNG background outside the circle."
}
```

- [ ] **Step 3: Fix import settings on both sprites and verify**

Same code pattern as Task 2 Step 3, with the two paths replaced by `"Assets/Sprites/Cockpit/HeadingBezel.png"` and `"Assets/Sprites/Cockpit/HeadingCard.png"`.

- [ ] **Step 4: Wire the bezel onto Face, the compass rose onto Card, remove the old procedural card**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Flusi;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("Must be in Edit mode"); return; }

        var face = GameObject.Find("SixPack/Heading/Face");
        var card = GameObject.Find("SixPack/Heading/Card");
        if (face == null || card == null) { result.LogError("Heading hierarchy not found"); return; }

        var faceImage = face.GetComponent<Image>();
        var bezelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/HeadingBezel.png");
        if (bezelSprite == null) { result.LogError("HeadingBezel sprite not found"); return; }
        var faceSo = new SerializedObject(faceImage);
        faceSo.FindProperty("m_Sprite").objectReferenceValue = bezelSprite;
        faceSo.FindProperty("m_Color").colorValue = Color.white;
        faceSo.ApplyModifiedProperties();
        result.RegisterObjectModification(faceImage);

        var cardImage = card.GetComponent<Image>();
        if (cardImage == null)
        {
            cardImage = card.AddComponent<Image>();
            result.RegisterObjectCreation(cardImage);
        }
        var cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cockpit/HeadingCard.png");
        if (cardSprite == null) { result.LogError("HeadingCard sprite not found"); return; }
        cardImage.sprite = cardSprite;
        cardImage.color = Color.white;
        cardImage.type = Image.Type.Simple;
        cardImage.raycastTarget = false;

        var builder = card.GetComponent<GaugeFaceBuilder>();
        if (builder != null) result.DestroyObject(builder);

        foreach (var childName in new[] { "CardN", "CardE", "CardS", "CardW" })
        {
            var child = card.transform.Find(childName);
            if (child != null) result.DestroyObject(child.gameObject);
        }
        for (int i = card.transform.childCount - 1; i >= 0; i--)
        {
            var child = card.transform.GetChild(i).gameObject;
            if (child.name == "Tick" || child.name == "Label")
                result.DestroyObject(child);
        }

        EditorSceneManager.MarkSceneDirty(face.scene);
        EditorSceneManager.SaveScene(face.scene);
        result.Log("Wired HeadingBezel onto Face, HeadingCard onto Card, removed procedural GaugeFaceBuilder + N/E/S/W text.");
    }
}
```

Expected: log line confirming the wire, no errors. `Card`'s `RectTransform.sizeDelta` stays `(150, 150)`, matching the 512×512 square art.

- [ ] **Step 5: Run the regression baseline**

Same code as Task 1 Step 4.

- [ ] **Step 6: Dump state for owner review**

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var face = GameObject.Find("SixPack/Heading/Face");
        var card = GameObject.Find("SixPack/Heading/Card");
        result.Log("Face sprite={0}, Card sprite={1}, Card childCount={2}",
            face.GetComponent<Image>().sprite?.name,
            card.GetComponent<Image>().sprite?.name,
            card.transform.childCount);
    }
}
```

Ask the owner to check the Heading Indicator in Play mode while turning — confirm the compass rose rotates correctly under the fixed lubber line and the cardinal letters land in believable positions.

- [ ] **Step 7: Commit**

```bash
git add Assets/Sprites/Cockpit/HeadingBezel.png Assets/Sprites/Cockpit/HeadingBezel.png.meta \
        Assets/Sprites/Cockpit/HeadingCard.png Assets/Sprites/Cockpit/HeadingCard.png.meta \
        Assets/Scenes/MainScene.unity
git commit -m "Give Heading Indicator a generated bezel and compass card"
```

---

## Task 6: Vertical Speed Indicator background

**Files:**
- Create: `Assets/Sprites/Cockpit/VerticalSpeedFace.png` (+ `.meta`)
- Modify: `Assets/Scenes/MainScene.unity`

**Interfaces:**
- Consumes: `Assets/Sprites/Cockpit/` folder, import-settings snippet (Task 1).
- Produces: nothing — last task.

- [ ] **Step 1: Generate the face art**

```json
{
  "tool": "Unity_AssetGeneration_GenerateAsset",
  "command": "GenerateSprite",
  "modelId": "gemini-3.0-pro",
  "width": 512,
  "height": 512,
  "savePath": "Assets/Sprites/Cockpit/VerticalSpeedFace.png",
  "prompt": "Simplified-realistic light-aircraft vertical-speed-indicator gauge face, flat clean illustration not photoreal, no dirt/glare/screws/reflections. Matte dark charcoal (#141416) circular face filling the frame edge to edge, thin 4% darker bezel rim. Crisp white numerals 0, 20, 40, 60, 80, 100 arranged around the top and both sides, with '0' at 9-o'clock-left, ascending values going up the left side to a 'UP' label near 12 o'clock, and descending mirrored values going down the right side to a 'DOWN' label, leaving the bottom third of the circle (around 6 o'clock) as a gap with no numerals. White tick mark at every numeral, shorter minor ticks halfway between. Small white text caption near the bottom reading 'M/S · 100'. Centered, top-down orthographic view, transparent PNG background outside the circle."
}
```

- [ ] **Step 2: Fix import settings and verify**

Same code pattern as Task 1 Step 2, with `path = "Assets/Sprites/Cockpit/VerticalSpeedFace.png"`.

- [ ] **Step 3: Wire the sprite onto VerticalSpeed/Face and disable its GaugeFaceBuilder**

Same code pattern as Task 1 Step 3, with `"SixPack/Airspeed/Face"` replaced by `"SixPack/VerticalSpeed/Face"` and the sprite path replaced by `"Assets/Sprites/Cockpit/VerticalSpeedFace.png"`.

- [ ] **Step 4: Run the regression baseline**

Same code as Task 1 Step 4.

- [ ] **Step 5: Dump state for owner review**

Same code pattern as Task 1 Step 5, with the path replaced by `"SixPack/VerticalSpeed/Face"`.

Ask the owner to check the Vertical Speed Indicator in Play mode while climbing and descending.

- [ ] **Step 6: Commit**

```bash
git add Assets/Sprites/Cockpit/VerticalSpeedFace.png Assets/Sprites/Cockpit/VerticalSpeedFace.png.meta \
        Assets/Scenes/MainScene.unity
git commit -m "Give Vertical Speed Indicator a generated gauge face"
```

---

## Task 7: Full-panel review

**Files:** none created or modified — verification-only task.

**Interfaces:**
- Consumes: all six gauges' finished art from Tasks 1–6.
- Produces: nothing — end of plan.

- [ ] **Step 1: Run the full regression baseline one more time**

Same code as Task 1 Step 4, both `RunEditMode()` and `RunPlayMode()`.

Expected: `STATUS Passed` for both, counts matching (or exceeding, if any were added elsewhere) the Global-Constraints baseline, `Unity_GetConsoleLogs` (`logTypes: "error"`) empty.

- [ ] **Step 2: Capture a full-panel dump for the owner**

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var root = GameObject.Find("SixPack");
        foreach (Transform gauge in root.transform)
        {
            var face = gauge.Find("Face");
            var img = face != null ? face.GetComponent<Image>() : null;
            result.Log("{0}: Face sprite = {1}", gauge.name, img != null && img.sprite != null ? img.sprite.name : "(no Face image)");
        }
    }
}
```

Ask the project owner for a final look at the whole panel together in Play mode — cockpit view, flying a short loop (climb, bank, turn) so all six gauges move at once — before considering this redesign done.
