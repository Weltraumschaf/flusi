# Cockpit Instrument Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the floating 2D HUD text overlays with an opaque cockpit
instrument panel across the bottom third of the screen, carrying the standard
six-pack of analogue gauges plus a minimap, annunciator lamps and a fuel bar.

**Architecture:** Pure static maths (`GaugeScale`, `AltimeterScale`,
`FlightDerivations`) behind thin MonoBehaviours, exactly like the existing
`HudFormat` / `MinimapProjection` pattern. Every gauge reads the existing
read-only `IAircraftState` seam. Gauge faces are assembled at runtime from Unity
built-in sprites and fonts — no texture assets, no new dependencies.

**Tech Stack:** Unity 6000.3.19f1, C#, URP, uGUI (`UnityEngine.UI`), New Input
System, Unity Test Framework.

**Spec:** `docs/superpowers/specs/2026-07-16-cockpit-instruments-design.md`.
Section references below (§3.4, §3.5, …) point at that document.

---

## Global Constraints

- **Unity 6000.3.19f1**, C#, URP. Target macOS and Linux standalone.
- **New Input System only.** Never use `UnityEngine.Input` or `Input.GetAxis`.
- **Do not add packages or dependencies.** (Project CLAUDE.md.) If a task seems
  to need one, stop and ask.
- **No texture or art assets.** Faces come from
  `Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd")` (a filled circle)
  and `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
- **Namespace is flat `Flusi`** for runtime code and `Flusi.Tests` for tests.
  Folders are organisational only.
- **Angle convention:** degrees, `0` = needle points at 12 o'clock, positive =
  clockwise. Components apply `localEulerAngles.z = -angle`, because Unity's Z
  rotation runs counter-clockwise. Never mix conventions.
- **Never modify `Assets/Scripts/Flight/FlightModel.cs` or `FlightConfig.cs`.**
  All 13 `FlightModelTests` must stay green and unedited. (13 is the flight-test
  count; 22 is the current EditMode total — 13 flight + 5 `HudFormat` + 3
  `MinimapProjection` + 1 harness. Do not confuse the two.)
- **Prefer clear, readable C# over clever abstractions.** (Project CLAUDE.md.)
- **Commit `.meta` files.** Unity generates a `.cs.meta` next to every new `.cs`
  *after* the Editor imports it. Every new file's meta must be staged in the same
  commit, or asset references break. See "Running tests" below for the ordering.

### Running tests (controller only)

Subagents cannot drive Unity. The controller runs tests through the Editor's MCP
bridge using the existing harness in `Assets/Editor/FlusiTestRunner.cs`:

```csharp
// via mcp__unity__Unity_RunCommand
Flusi.EditorTools.FlusiTestRunner.RunEditMode();   // or RunPlayMode()
```

Then poll `Temp/flusi-tests.txt` for `STATUS Passed` / `STATUS Failed` and read
`Temp/flusi-tests-failures.txt` for failure detail.

**A green result is meaningless until you have checked for compile errors.**
When compilation fails, Unity's test runner does not fail — it silently runs the
last successfully-built assemblies and reports a *stale* pass. Task 2 hit this:
it reported `passed=40` while the project did not compile at all, and the count
looked plausible. **Always call `mcp__unity__Unity_GetConsoleLogs` with
`logTypes: "error"` and confirm zero errors before believing any test result.**
An unexpected count — even a passing one — means look at the console, not shrug.

**Per-task ordering — do not deviate:**

1. Subagent writes the code and tests.
2. Controller lets Unity import (a script change triggers a domain reload; wait
   ~30 s before the next MCP call, and retry with backoff if the bridge drops).
3. Controller runs the tests and confirms the expected result.
4. Controller stages the newly generated `.meta` files.
5. **Then** review and commit.

---

## File Structure

**New directory: `Assets/Scripts/Cockpit/`** — everything the panel is made of.

| File | Responsibility |
| --- | --- |
| `Cockpit/GaugeScale.cs` | Pure: value → needle angle, clamped. |
| `Cockpit/AltimeterScale.cs` | Pure: two-needle altimeter wrap maths. |
| `Cockpit/FlightDerivations.cs` | Pure: vertical speed, m/s → km/h. |
| `Cockpit/HudFormat.cs` | Moved from `Hud/`. Digital readout strings. |
| `Cockpit/GaugeChannel.cs` | Enum: which value a `NeedleGauge` reads. |
| `Cockpit/NeedleGauge.cs` | One-needle round gauge. Three instances. |
| `Cockpit/Altimeter.cs` | Two-needle altimeter. |
| `Cockpit/HeadingIndicator.cs` | Rotating compass card. |
| `Cockpit/AttitudeIndicator.cs` | Rolling/sliding horizon ball. |
| `Cockpit/GaugeFaceBuilder.cs` | Builds ticks + labels around a circle at `Awake`. |
| `Cockpit/LampChannel.cs` | Enum: which bool an `AnnunciatorLamp` reads. |
| `Cockpit/AnnunciatorLamp.cs` | Two-state lamp. Two instances (ASSIST, GEAR). |
| `Cockpit/FuelGauge.cs` | Fuel bar. **Static placeholder** — see §3.5. |
| `Cockpit/CockpitPanel.cs` | Owns panel visibility + digital readouts. |

**Deleted:** `Hud/HudController.cs`, `Hud/ArtificialHorizon.cs`,
`Hud/HeadingCompass.cs`, and the `Hud/` directory itself.

**Unchanged:** everything under `World/`. `World/Minimap.cs` stays where it is —
it is about world points of interest, not about the panel; only its RectTransform
is reparented, in Task 11.

**Touched outside `Cockpit/`, minimally:**
- `Flight/IAircraftState.cs`, `Flight/AircraftController.cs`,
  `Input/FlightControls.inputactions` and its generated wrapper — landing gear
  (Task 3).
- `Cameras/CameraRig.cs` — extract `public void ToggleView()` so the panel's
  view-toggle behaviour is testable (Task 12). Same pattern as the gear toggle.
- `Flight/FlightModel.cs` and `Flight/FlightConfig.cs` — **never**.

### Deviation from the spec, deliberate

Spec §6 says `HudFormat` keeps "its 5 tests". That is wrong. `HudFormat.Compass`
is called only from `HudController`, which this plan deletes; the
`HeadingIndicator` supersedes it. It and its 3 tests (`Compass_North_IsN`,
`Compass_East_IsE`, `Compass_Wraps`) go, leaving 2. Dead code with passing tests
is still dead code.

**They go in Task 9, together with `HudController`** — not earlier. `Compass` is
dead only *once its caller is gone*; deleting it while `HudController` still
calls it breaks the build for every task in between. (This plan originally put
the deletion in Task 2 and did exactly that.)

---

## Task 1: `GaugeScale` — pure needle maths

**Files:**
- Create: `Assets/Scripts/Cockpit/GaugeScale.cs`
- Test: `Assets/Tests/EditMode/GaugeScaleTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public static float GaugeScale.ValueToAngle(float value, float minValue, float maxValue, float startAngle, float sweepAngle)` — returns degrees clockwise from 12 o'clock.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/GaugeScaleTests.cs`:

```csharp
using NUnit.Framework;

namespace Flusi.Tests
{
    public class GaugeScaleTests
    {
        // Airspeed calibration: 0..500 km/h, 0 deg at 12 o'clock, +320 clockwise.
        [Test]
        public void Value_At_Min_Gives_StartAngle()
            => Assert.AreEqual(0f, GaugeScale.ValueToAngle(0f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_At_Max_Gives_StartPlusSweep()
            => Assert.AreEqual(320f, GaugeScale.ValueToAngle(500f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_At_Midpoint_Gives_HalfSweep()
            => Assert.AreEqual(160f, GaugeScale.ValueToAngle(250f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_Below_Min_Clamps_To_StartAngle()
            => Assert.AreEqual(0f, GaugeScale.ValueToAngle(-100f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_Above_Max_Clamps_To_StartPlusSweep()
            => Assert.AreEqual(320f, GaugeScale.ValueToAngle(9999f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Negative_Sweep_Runs_CounterClockwise()
            => Assert.AreEqual(-30f, GaugeScale.ValueToAngle(55f, -55f, 55f, 30f, -60f), 0.001f);

        // VSI calibration (spec 3.2): -100..+100 m/s, start 185, sweep 170.
        // Zero must land on 9 o'clock, i.e. 270 degrees.
        [Test]
        public void VerticalSpeed_Zero_Points_At_Nine_OClock()
            => Assert.AreEqual(270f, GaugeScale.ValueToAngle(0f, -100f, 100f, 185f, 170f), 0.001f);

        // Turn coordinator calibration (spec 3.2): -55..+55 bank, start -30, sweep 60.
        [Test]
        public void TurnCoordinator_Level_Bank_Gives_Level_Symbol()
            => Assert.AreEqual(0f, GaugeScale.ValueToAngle(0f, -55f, 55f, -30f, 60f), 0.001f);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Controller runs `Flusi.EditorTools.FlusiTestRunner.RunEditMode()` via MCP.
Expected: compile error — `GaugeScale` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Cockpit/GaugeScale.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Pure needle maths for round gauges.
    ///
    /// Angle convention: degrees, 0 = needle points at 12 o'clock, positive =
    /// clockwise. Components apply localEulerAngles.z = -angle, because Unity's
    /// Z rotation runs counter-clockwise.
    public static class GaugeScale
    {
        /// Maps value onto a needle angle, clamped at both ends of the range.
        /// A negative sweepAngle runs the gauge counter-clockwise.
        public static float ValueToAngle(float value, float minValue, float maxValue,
                                         float startAngle, float sweepAngle)
        {
            float t = Mathf.InverseLerp(minValue, maxValue, value);
            return startAngle + t * sweepAngle;
        }
    }
}
```

`Mathf.InverseLerp` already clamps to 0..1, which is what gives us the
out-of-range clamping for free.

- [ ] **Step 4: Run tests to verify they pass**

Expected: `STATUS Passed`, `passed=30` — 22 existing EditMode tests plus the 8
new ones. Confirm the failure file is empty. Do not proceed on any failure.

- [ ] **Step 5: Stage metas and commit**

```bash
git add Assets/Scripts/Cockpit/ Assets/Tests/EditMode/GaugeScaleTests.cs
git status --short   # confirm .cs.meta files are staged, not untracked
git commit -m "Add pure needle-angle maths for round gauges"
```

---

## Task 2: `AltimeterScale`, `FlightDerivations`, and the `HudFormat` move

**Files:**
- Create: `Assets/Scripts/Cockpit/AltimeterScale.cs`
- Create: `Assets/Scripts/Cockpit/FlightDerivations.cs`
- Move: `Assets/Scripts/Hud/HudFormat.cs` → `Assets/Scripts/Cockpit/HudFormat.cs`
- Modify: `Assets/Tests/EditMode/HudFormatTests.cs` (delete 3 Compass tests)
- Test: `Assets/Tests/EditMode/AltimeterScaleTests.cs`
- Test: `Assets/Tests/EditMode/FlightDerivationsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public static float AltimeterScale.HundredsAngle(float altitudeMetres)`
  - `public static float AltimeterScale.ThousandsAngle(float altitudeMetres)`
  - `public static float FlightDerivations.VerticalSpeed(float speedMetresPerSecond, float pitchDegrees)`
  - `public static float FlightDerivations.SpeedKmh(float metresPerSecond)`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/AltimeterScaleTests.cs`:

```csharp
using NUnit.Framework;

namespace Flusi.Tests
{
    public class AltimeterScaleTests
    {
        [Test]
        public void Ground_Level_Points_Both_Needles_Up()
        {
            Assert.AreEqual(0f, AltimeterScale.HundredsAngle(0f), 0.001f);
            Assert.AreEqual(0f, AltimeterScale.ThousandsAngle(0f), 0.001f);
        }

        [Test]
        public void Half_A_Thousand_Puts_Long_Needle_At_Six_OClock()
        {
            Assert.AreEqual(180f, AltimeterScale.HundredsAngle(500f), 0.001f);
            Assert.AreEqual(18f, AltimeterScale.ThousandsAngle(500f), 0.001f);
        }

        [Test]
        public void Long_Needle_Wraps_At_Exactly_One_Thousand()
        {
            Assert.AreEqual(0f, AltimeterScale.HundredsAngle(1000f), 0.001f);
            Assert.AreEqual(36f, AltimeterScale.ThousandsAngle(1000f), 0.001f);
        }

        [Test]
        public void Fifteen_Hundred_Reads_One_And_A_Half()
        {
            Assert.AreEqual(180f, AltimeterScale.HundredsAngle(1500f), 0.001f);
            Assert.AreEqual(54f, AltimeterScale.ThousandsAngle(1500f), 0.001f);
        }

        // The terrain clamp should stop this happening, but the gauge must not
        // produce a negative angle if it ever does.
        [Test]
        public void Negative_Altitude_Wraps_Instead_Of_Going_Negative()
        {
            Assert.AreEqual(324f, AltimeterScale.HundredsAngle(-100f), 0.001f);
            Assert.AreEqual(356.4f, AltimeterScale.ThousandsAngle(-100f), 0.01f);
        }
    }
}
```

Create `Assets/Tests/EditMode/FlightDerivationsTests.cs`:

```csharp
using NUnit.Framework;

namespace Flusi.Tests
{
    public class FlightDerivationsTests
    {
        [Test]
        public void Level_Flight_Has_Zero_Vertical_Speed()
            => Assert.AreEqual(0f, FlightDerivations.VerticalSpeed(100f, 0f), 0.001f);

        // sin(30) == 0.5, so half the airspeed becomes climb rate.
        [Test]
        public void Nose_Up_Thirty_Degrees_Climbs_At_Half_Airspeed()
            => Assert.AreEqual(50f, FlightDerivations.VerticalSpeed(100f, 30f), 0.001f);

        [Test]
        public void Nose_Down_Thirty_Degrees_Descends_At_Half_Airspeed()
            => Assert.AreEqual(-50f, FlightDerivations.VerticalSpeed(100f, -30f), 0.001f);

        [Test]
        public void Straight_Up_Climbs_At_Full_Airspeed()
            => Assert.AreEqual(100f, FlightDerivations.VerticalSpeed(100f, 90f), 0.001f);

        [Test]
        public void SpeedKmh_Converts_Metres_Per_Second()
            => Assert.AreEqual(360f, FlightDerivations.SpeedKmh(100f), 0.001f);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: compile error — `AltimeterScale` and `FlightDerivations` do not exist.

- [ ] **Step 3: Write the implementations**

Create `Assets/Scripts/Cockpit/AltimeterScale.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Pure two-needle altimeter maths.
    /// Long needle: one revolution per 1000 m. Short needle: one per 10 000 m.
    ///
    /// Angle convention as GaugeScale: degrees clockwise from 12 o'clock.
    public static class AltimeterScale
    {
        public static float HundredsAngle(float altitudeMetres)
            => Mathf.Repeat(altitudeMetres, 1000f) / 1000f * 360f;

        public static float ThousandsAngle(float altitudeMetres)
            => Mathf.Repeat(altitudeMetres, 10000f) / 10000f * 360f;
    }
}
```

`Mathf.Repeat` (not `%`) is what keeps a negative altitude from producing a
negative angle: `Mathf.Repeat(-100f, 1000f) == 900f`.

Create `Assets/Scripts/Cockpit/FlightDerivations.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Values derived from IAircraftState that no gauge should recompute itself.
    public static class FlightDerivations
    {
        /// Metres per second of climb. This is exact, not an approximation:
        /// FlightModel integrates position along
        /// Quaternion.Euler(-Pitch, Heading, 0) * Vector3.forward, whose Y
        /// component is sin(pitch).
        public static float VerticalSpeed(float speedMetresPerSecond, float pitchDegrees)
            => speedMetresPerSecond * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);

        public static float SpeedKmh(float metresPerSecond) => metresPerSecond * 3.6f;
    }
}
```

- [ ] **Step 4: Move `HudFormat` and drop the dead `Compass`**

Move the file, taking its `.meta` so the GUID survives:

```bash
git mv Assets/Scripts/Hud/HudFormat.cs Assets/Scripts/Cockpit/HudFormat.cs
git mv Assets/Scripts/Hud/HudFormat.cs.meta Assets/Scripts/Cockpit/HudFormat.cs.meta
```

Make exactly one edit to `Assets/Scripts/Cockpit/HudFormat.cs` — route `Speed`
through the new `SpeedKmh` so the ×3.6 constant lives in one place:

```csharp
        public static string Speed(float metresPerSecond)
            => $"{Mathf.RoundToInt(FlightDerivations.SpeedKmh(metresPerSecond))} km/h";
```

**Leave `Compass` and its 3 tests alone.** They are dead code, but only once
`HudController` — their sole caller — is gone. Task 9 deletes all of it together.
Removing `Compass` here breaks the build for Tasks 2 through 8.

`HudFormatTests.cs` is not touched by this task.

- [ ] **Step 5: Run tests to verify they pass**

Expected: `STATUS Passed`, `passed=40` — Task 1 left 30, plus 5 `AltimeterScale`
and 5 `FlightDerivations`. `Compass` and its 3 tests stay until Task 9.
All 5 `HudFormat` tests must still pass — that is what proves the `SpeedKmh`
refactor did not change behaviour.

- [ ] **Step 6: Stage metas and commit**

```bash
git add Assets/Scripts/Cockpit/ Assets/Scripts/Hud/ Assets/Tests/EditMode/
git status --short   # confirm both new .cs.meta files are staged
git commit -m "Add altimeter and vertical-speed maths, retire dead compass text"
```

---

## Task 3: Landing gear — real toggled state

**Files:**
- Modify: `Assets/Input/FlightControls.inputactions`
- Regenerate: `Assets/Scripts/Flight/FlightControls.cs` (Unity does this on import)
- Modify: `Assets/Scripts/Flight/IAircraftState.cs`
- Modify: `Assets/Scripts/Flight/AircraftController.cs`
- Test: `Assets/Tests/PlayMode/GearTests.cs`

**Interfaces:**
- Consumes: `IAircraftState` (existing).
- Produces:
  - `bool IAircraftState.GearDown { get; }`
  - `public void AircraftController.ToggleGear()`
  - `FlightControls.Flight.ToggleGear` action, bound to `<Keyboard>/g`.

**Why a public `ToggleGear()`:** the PlayMode test must flip gear without
synthesising keyboard input, which would drag in the Input System test framework.
The input callback simply calls this method, so the test exercises the real
state transition.

- [ ] **Step 1: Add the action to the input asset**

In `Assets/Input/FlightControls.inputactions`, add to the `actions` array of the
`Flight` map, after the existing `ToggleView` entry:

```json
                {
                    "name": "ToggleGear",
                    "type": "Button",
                    "id": "3f0c1a52-9b7e-4d61-8a4c-2e5b90d7c118",
                    "expectedControlType": "",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
```

And add to the `bindings` array, after the existing `ToggleView` binding:

```json
                {
                    "name": "",
                    "id": "b71d4e39-0c86-49f2-95a3-6d148ae2c503",
                    "path": "<Keyboard>/g",
                    "interactions": "",
                    "processors": "",
                    "groups": "",
                    "action": "ToggleGear",
                    "isComposite": false,
                    "isPartOfComposite": false
                }
```

Mind the commas: the preceding entry in each array now needs a trailing comma.

- [ ] **Step 2: Let Unity regenerate the wrapper**

The asset has "Generate C# Class" enabled, targeting
`Assets/Scripts/Flight/FlightControls.cs` (that path is deliberate — it must
compile into the `Flusi` assembly). Unity regenerates it on import.

Controller: wait for the import, then verify via MCP that the action exists:

```csharp
// mcp__unity__Unity_RunCommand
var c = new Flusi.FlightControls();
result.Log("ToggleGear present: {0}", c.Flight.ToggleGear != null);
c.Dispose();
```

Expected: `ToggleGear present: True`. If it did not regenerate, re-import the
asset before continuing. **Do not hand-edit `FlightControls.cs`** — it is
generated and will be overwritten.

- [ ] **Step 3: Write the failing test**

Create `Assets/Tests/PlayMode/GearTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Flusi.Tests
{
    public class GearTests
    {
        [UnityTest]
        public IEnumerator Gear_Starts_Down_And_Toggles()
        {
            var go = new GameObject("Aircraft");
            var controller = go.AddComponent<AircraftController>();
            yield return null;

            var state = (IAircraftState)controller;
            Assert.IsTrue(state.GearDown, "gear should start down");

            controller.ToggleGear();
            Assert.IsFalse(state.GearDown, "gear should retract on toggle");

            controller.ToggleGear();
            Assert.IsTrue(state.GearDown, "gear should extend again on second toggle");

            Object.Destroy(go);
        }
    }
}
```

- [ ] **Step 4: Run PlayMode tests to verify failure**

Controller runs `Flusi.EditorTools.FlusiTestRunner.RunPlayMode()`.
Expected: compile error — `GearDown` and `ToggleGear` do not exist.

- [ ] **Step 5: Extend the seam**

In `Assets/Scripts/Flight/IAircraftState.cs`, add `GearDown` after `AutoLevelOn`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Read-only view of the aircraft for HUD, minimap and cameras.
    /// Consumers must depend on this, never on FlightModel/AircraftController internals.
    public interface IAircraftState
    {
        float AltitudeMeters { get; }
        float SpeedMetersPerSecond { get; }
        float HeadingDegrees { get; }
        float PitchDegrees { get; }
        float BankDegrees { get; }
        Vector3 WorldPosition { get; }
        bool AutoLevelOn { get; }
        bool GearDown { get; }
    }
}
```

- [ ] **Step 6: Implement it on the controller**

In `Assets/Scripts/Flight/AircraftController.cs` make exactly four edits.

Add the serialized field after `autoLevelOn` (line ~11):

```csharp
        [SerializeField] private bool gearDown = true;
```

Add the property after `AutoLevelOn` (line ~25):

```csharp
        public bool GearDown => gearDown;
```

Subscribe and unsubscribe alongside the existing auto-level handlers:

```csharp
        private void OnEnable()
        {
            _controls.Enable();
            _controls.Flight.ToggleAutoLevel.performed += OnToggleAutoLevel;
            _controls.Flight.ToggleGear.performed += OnToggleGear;
        }

        private void OnDisable()
        {
            _controls.Flight.ToggleAutoLevel.performed -= OnToggleAutoLevel;
            _controls.Flight.ToggleGear.performed -= OnToggleGear;
            _controls.Disable();
        }
```

Add the toggle next to `OnToggleAutoLevel`:

```csharp
        /// Public so tests and future cockpit switches can toggle the gear
        /// without synthesising keyboard input.
        public void ToggleGear() => gearDown = !gearDown;

        private void OnToggleGear(UnityEngine.InputSystem.InputAction.CallbackContext _)
            => ToggleGear();
```

`FixedUpdate` is untouched. Gear applies no force — `FlightModel` never sees it.

- [ ] **Step 7: Run PlayMode tests to verify they pass**

Expected: `STATUS Passed`, `passed=4` (1 aircraft smoke + 2 POI registry + 1 new
gear test). Then run EditMode too and confirm all 40 are still green, the 13
`FlightModelTests` among them — the seam changed, so prove nothing regressed.

- [ ] **Step 8: Stage metas and commit**

```bash
git add Assets/Input/ Assets/Scripts/Flight/ Assets/Tests/PlayMode/GearTests.cs
git status --short   # FlightControls.cs (regenerated) and GearTests.cs.meta staged
git commit -m "Add landing gear as real toggled state on G"
```

---

## Task 4: `GaugeFaceBuilder` — ticks and labels from code

**Files:**
- Create: `Assets/Scripts/Cockpit/GaugeFaceBuilder.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `GaugeFaceBuilder`, a MonoBehaviour that populates its own
  RectTransform with tick and label children at `Awake`. All calibration is
  serialized.

**No unit test:** this component only creates GameObjects — there is no logic to
assert that would not just restate the implementation. Task 12's PlayMode smoke
test covers "it builds without throwing"; the real check is visual.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/Cockpit/GaugeFaceBuilder.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Builds a round gauge face at Awake: tick marks and numeric labels placed
    /// around a circle. No texture assets — ticks are plain Image rectangles and
    /// labels use Unity's built-in font.
    ///
    /// Angle convention as GaugeScale: degrees clockwise from 12 o'clock.
    public class GaugeFaceBuilder : MonoBehaviour
    {
        [Header("Arc")]
        [SerializeField] private float startAngle = 0f;
        [SerializeField] private float sweepAngle = 320f;

        [Header("Ticks")]
        [SerializeField] private int tickCount = 21;      // inclusive of both ends
        [SerializeField] private int majorEvery = 4;
        [SerializeField] private float radius = 60f;
        [SerializeField] private float minorLength = 6f;
        [SerializeField] private float majorLength = 12f;
        [SerializeField] private float tickWidth = 2f;
        [SerializeField] private Color tickColor = Color.white;

        [Header("Labels")]
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float labelMinValue = 0f;
        [SerializeField] private float labelMaxValue = 500f;
        [SerializeField] private float labelRadius = 42f;
        [SerializeField] private int labelFontSize = 10;

        private void Awake() => Build();

        private void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < tickCount; i++)
            {
                float t = tickCount <= 1 ? 0f : i / (float)(tickCount - 1);
                float angle = startAngle + t * sweepAngle;
                bool major = majorEvery > 0 && i % majorEvery == 0;

                CreateTick(angle, major ? majorLength : minorLength);

                if (showLabels && major)
                    CreateLabel(angle, Mathf.Lerp(labelMinValue, labelMaxValue, t), font);
            }
        }

        private void CreateTick(float angle, float length)
        {
            var go = new GameObject("Tick", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(tickWidth, length);
            rt.anchoredPosition = PointAt(angle, radius - length * 0.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, -angle);

            var image = go.GetComponent<Image>();
            image.color = tickColor;
            image.raycastTarget = false;
        }

        private void CreateLabel(float angle, float value, Font font)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(40f, 16f);
            rt.anchoredPosition = PointAt(angle, labelRadius);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = labelFontSize;
            text.color = tickColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = Mathf.RoundToInt(value).ToString();
        }

        /// Point at `angle` degrees clockwise from 12 o'clock, `r` out from centre.
        /// sin/cos are swapped versus the usual convention precisely because
        /// angle 0 must mean "up", not "right".
        private static Vector2 PointAt(float angle, float r)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r);
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Controller: wait for the domain reload, then read the console via
`mcp__unity__Unity_GetConsoleLogs` with `logTypes: "error"`.
Expected: no compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Cockpit/GaugeFaceBuilder.cs Assets/Scripts/Cockpit/GaugeFaceBuilder.cs.meta
git commit -m "Build gauge faces procedurally from Unity built-ins"
```

---

## Task 5: `GaugeChannel` and `NeedleGauge`

**Files:**
- Create: `Assets/Scripts/Cockpit/GaugeChannel.cs`
- Create: `Assets/Scripts/Cockpit/NeedleGauge.cs`

**Interfaces:**
- Consumes: `GaugeScale.ValueToAngle` (Task 1), `FlightDerivations.SpeedKmh` and
  `FlightDerivations.VerticalSpeed` (Task 2), `IAircraftState` (existing).
- Produces: `NeedleGauge` with `public void SetSource(IAircraftState s)`, and the
  `GaugeChannel` enum with members `AirspeedKmh`, `VerticalSpeed`, `BankDegrees`.

- [ ] **Step 1: Write the enum**

Create `Assets/Scripts/Cockpit/GaugeChannel.cs`:

```csharp
namespace Flusi
{
    /// Which value a NeedleGauge reads from IAircraftState.
    public enum GaugeChannel
    {
        AirspeedKmh,
        VerticalSpeed,
        BankDegrees,
    }
}
```

- [ ] **Step 2: Write the gauge**

Create `Assets/Scripts/Cockpit/NeedleGauge.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// A single-needle round gauge. One component, several instances; the
    /// channel decides what it reads. Every calibration value is serialized so
    /// it can be tuned in the Inspector while the game runs.
    public class NeedleGauge : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform needle;
        [SerializeField] private GaugeChannel channel = GaugeChannel.AirspeedKmh;

        [Header("Calibration (see spec 3.2)")]
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 500f;
        [SerializeField] private float startAngle = 0f;
        [SerializeField] private float sweepAngle = 320f;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null || needle == null) return;

            float angle = GaugeScale.ValueToAngle(Read(_state), minValue, maxValue,
                                                  startAngle, sweepAngle);
            needle.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        /// Not static: it reads the instance field `channel`.
        private float Read(IAircraftState s) => channel switch
        {
            GaugeChannel.AirspeedKmh => FlightDerivations.SpeedKmh(s.SpeedMetersPerSecond),
            GaugeChannel.VerticalSpeed => FlightDerivations.VerticalSpeed(
                                              s.SpeedMetersPerSecond, s.PitchDegrees),
            GaugeChannel.BankDegrees => s.BankDegrees,
            _ => 0f,
        };
    }
}
```

- [ ] **Step 3: Verify it compiles**

Controller: check `mcp__unity__Unity_GetConsoleLogs` with `logTypes: "error"`.
Expected: no compilation errors. Then run EditMode tests — expected still green.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Cockpit/GaugeChannel.cs Assets/Scripts/Cockpit/GaugeChannel.cs.meta \
        Assets/Scripts/Cockpit/NeedleGauge.cs Assets/Scripts/Cockpit/NeedleGauge.cs.meta
git commit -m "Add the single-needle round gauge component"
```

---

## Task 6: `Altimeter`

**Files:**
- Create: `Assets/Scripts/Cockpit/Altimeter.cs`

**Interfaces:**
- Consumes: `AltimeterScale.HundredsAngle` / `ThousandsAngle` (Task 2),
  `IAircraftState`.
- Produces: `Altimeter` with `public void SetSource(IAircraftState s)`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/Cockpit/Altimeter.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Two-needle altimeter: the long needle turns once per 1000 m, the short
    /// needle once per 10 000 m. Kept separate from NeedleGauge because it drives
    /// two needles from one value and wraps rather than clamping.
    public class Altimeter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform hundredsNeedle;
        [SerializeField] private RectTransform thousandsNeedle;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null) return;

            float altitude = _state.AltitudeMeters;

            if (hundredsNeedle != null)
                hundredsNeedle.localEulerAngles =
                    new Vector3(0f, 0f, -AltimeterScale.HundredsAngle(altitude));

            if (thousandsNeedle != null)
                thousandsNeedle.localEulerAngles =
                    new Vector3(0f, 0f, -AltimeterScale.ThousandsAngle(altitude));
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Expected: no errors in `mcp__unity__Unity_GetConsoleLogs`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Cockpit/Altimeter.cs Assets/Scripts/Cockpit/Altimeter.cs.meta
git commit -m "Add the two-needle altimeter component"
```

---

## Task 7: `AttitudeIndicator` and `HeadingIndicator`

**Files:**
- Create: `Assets/Scripts/Cockpit/AttitudeIndicator.cs`
- Create: `Assets/Scripts/Cockpit/HeadingIndicator.cs`
- Delete: `Assets/Scripts/Hud/ArtificialHorizon.cs` (+ `.meta`)
- Delete: `Assets/Scripts/Hud/HeadingCompass.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `IAircraftState`.
- Produces: `AttitudeIndicator` and `HeadingIndicator`, each with
  `public void SetSource(IAircraftState s)`.

**Design note — why the attitude ball is nested:** the old `ArtificialHorizon`
set rotation and `anchoredPosition` on the *same* RectTransform, so the pitch
slide happened in the unrolled frame and drifted sideways as the aircraft banked.
Two nested transforms fix it: `bankRoot` rotates, and `pitchCard` (its child)
slides inside the already-rolled frame. That is how a real instrument behaves.

- [ ] **Step 1: Write the attitude indicator**

Create `Assets/Scripts/Cockpit/AttitudeIndicator.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Artificial horizon. bankRoot rolls with bank; pitchCard, its child,
    /// slides with pitch inside that rolled frame. Both sit behind a circular
    /// mask, under a fixed aircraft symbol.
    ///
    /// Replaces ArtificialHorizon.
    public class AttitudeIndicator : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform bankRoot;
        [SerializeField] private RectTransform pitchCard;
        [SerializeField] private RectTransform rollPointer;
        [SerializeField] private float pixelsPerPitchDegree = 1.5f;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null) return;

            // The horizon rolls opposite the aircraft, so a right bank tips the
            // horizon line left. Unity's positive Z is counter-clockwise, which
            // is already the direction we want here.
            if (bankRoot != null)
                bankRoot.localEulerAngles = new Vector3(0f, 0f, _state.BankDegrees);

            if (pitchCard != null)
                pitchCard.anchoredPosition =
                    new Vector2(0f, -_state.PitchDegrees * pixelsPerPitchDegree);

            if (rollPointer != null)
                rollPointer.localEulerAngles = new Vector3(0f, 0f, _state.BankDegrees);
        }
    }
}
```

- [ ] **Step 2: Write the heading indicator**

Create `Assets/Scripts/Cockpit/HeadingIndicator.cs`:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Rotating compass card under a fixed lubber line at the top.
    /// Replaces HeadingCompass.
    public class HeadingIndicator : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform card;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null || card == null) return;

            // The card turns opposite the aircraft so the current heading stays
            // under the lubber line. Heading 90 (east) must bring the card's "E"
            // — which sits 90 degrees clockwise on the face — up to 12 o'clock,
            // so the card rotates counter-clockwise by the heading: +Z in Unity.
            card.localEulerAngles = new Vector3(0f, 0f, _state.HeadingDegrees);
        }
    }
}
```

- [ ] **Step 3: Delete the superseded components**

```bash
git rm Assets/Scripts/Hud/ArtificialHorizon.cs Assets/Scripts/Hud/ArtificialHorizon.cs.meta
git rm Assets/Scripts/Hud/HeadingCompass.cs Assets/Scripts/Hud/HeadingCompass.cs.meta
```

The scene still references them at this point; Unity will log missing-script
warnings on `SampleScene` until Task 10 rebuilds the panel. That is expected —
do not "fix" it by re-adding the files.

- [ ] **Step 4: Verify it compiles**

Expected: no *errors* in `mcp__unity__Unity_GetConsoleLogs` (missing-script
*warnings* on the scene are fine and expected). EditMode tests still green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Cockpit/ Assets/Scripts/Hud/
git status --short   # both new .cs.meta staged, both deletions staged
git commit -m "Replace HUD horizon and compass with cockpit instruments"
```

---

## Task 8: `AnnunciatorLamp` and `FuelGauge`

**Files:**
- Create: `Assets/Scripts/Cockpit/LampChannel.cs`
- Create: `Assets/Scripts/Cockpit/AnnunciatorLamp.cs`
- Create: `Assets/Scripts/Cockpit/FuelGauge.cs`

**Interfaces:**
- Consumes: `IAircraftState` including `GearDown` (Task 3).
- Produces: `AnnunciatorLamp` with `public void SetSource(IAircraftState s)`;
  `LampChannel` enum with `AutoLevel`, `GearDown`; `FuelGauge` with
  `public float Level { get; set; }`.

- [ ] **Step 1: Write the lamp channel enum**

Create `Assets/Scripts/Cockpit/LampChannel.cs`:

```csharp
namespace Flusi
{
    /// Which bool an AnnunciatorLamp reads from IAircraftState.
    public enum LampChannel
    {
        AutoLevel,
        GearDown,
    }
}
```

- [ ] **Step 2: Write the lamp**

Create `Assets/Scripts/Cockpit/AnnunciatorLamp.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// A two-state annunciator. Two instances on the panel: ASSIST and GEAR.
    /// Captions and colours are serialized so one component covers both.
    public class AnnunciatorLamp : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private Text caption;
        [SerializeField] private Image lamp;
        [SerializeField] private LampChannel channel = LampChannel.AutoLevel;

        [Header("Captions")]
        [SerializeField] private string onText = "ASSIST ON";
        [SerializeField] private string offText = "ASSIST OFF";

        [Header("Colours")]
        [SerializeField] private Color onColor = new Color(0.20f, 0.90f, 0.30f);
        [SerializeField] private Color offColor = new Color(0.35f, 0.35f, 0.35f);

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null) return;

            bool on = channel switch
            {
                LampChannel.AutoLevel => _state.AutoLevelOn,
                LampChannel.GearDown => _state.GearDown,
                _ => false,
            };

            if (caption != null) caption.text = on ? onText : offText;
            if (lamp != null) lamp.color = on ? onColor : offColor;
        }
    }
}
```

- [ ] **Step 3: Write the fuel gauge**

Create `Assets/Scripts/Cockpit/FuelGauge.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Fuel quantity bar.
    ///
    /// PLACEHOLDER — this is the one instrument on the panel that does not
    /// report anything real. `level` is a static serialized value; there is no
    /// fuel burn in the game, so the needle never moves however far you fly.
    /// This is deliberate and owner-approved, not an oversight: see
    /// docs/superpowers/specs/2026-07-16-cockpit-instruments-design.md 3.5,
    /// which also records why draining fuel is blocked (Specification.md 2
    /// rules out fail states) and the two routes for wiring it up later.
    ///
    /// To make it live, replace the `level` read in Update with a state read.
    /// Nothing else here changes.
    public class FuelGauge : MonoBehaviour
    {
        /// Requires an Image with type = Filled, so fillAmount does something.
        [SerializeField] private Image fillBar;
        [SerializeField, Range(0f, 1f)] private float level = 1f;

        public float Level
        {
            get => level;
            set => level = Mathf.Clamp01(value);
        }

        private void Update()
        {
            if (fillBar != null) fillBar.fillAmount = level;
        }
    }
}
```

- [ ] **Step 4: Verify it compiles**

Expected: no errors. EditMode and PlayMode tests still green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Cockpit/
git status --short   # three new .cs.meta staged
git commit -m "Add annunciator lamps and the fuel placeholder gauge"
```

---

## Task 9: `CockpitPanel`, and retire `HudController`

**Files:**
- Create: `Assets/Scripts/Cockpit/CockpitPanel.cs`
- Delete: `Assets/Scripts/Hud/HudController.cs` (+ `.meta`)
- Delete: the now-empty `Assets/Scripts/Hud/` directory (+ `Hud.meta`)
- Modify: `Assets/Scripts/Cockpit/HudFormat.cs` (drop `Compass` + `Points`)
- Modify: `Assets/Tests/EditMode/HudFormatTests.cs` (drop the 3 Compass tests)

**Interfaces:**
- Consumes: `IAircraftState`, `CameraRig.ViewChanged` and `CameraRig.Current`
  (existing), `ViewMode` (existing), `HudFormat.Altitude` / `HudFormat.Speed`
  (Task 2).
- Produces: `CockpitPanel`.

- [ ] **Step 1: Write the panel controller**

Create `Assets/Scripts/Cockpit/CockpitPanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Owns the cockpit instrument panel: shows it in cockpit view, hides it in
    /// orbit view, and keeps the digital readouts fed.
    ///
    /// The gauges feed themselves from IAircraftState; this only handles what
    /// they cannot — panel visibility and the plain-number readouts.
    ///
    /// Replaces HudController.
    public class CockpitPanel : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private GameObject panelRoot;

        [Header("Digital readouts (spec 2.1)")]
        [SerializeField] private Text altitudeText;
        [SerializeField] private Text speedText;

        private IAircraftState _state;

        private void Awake()
        {
            if (aircraftSource != null) _state = (IAircraftState)aircraftSource;
        }

        private void OnEnable()
        {
            if (cameraRig != null)
            {
                cameraRig.ViewChanged += OnViewChanged;
                OnViewChanged(cameraRig.Current); // sync to whatever view we start in
            }
        }

        private void OnDisable()
        {
            if (cameraRig != null) cameraRig.ViewChanged -= OnViewChanged;
        }

        private void OnViewChanged(ViewMode mode)
        {
            if (panelRoot != null) panelRoot.SetActive(mode == ViewMode.Cockpit);
        }

        private void Update()
        {
            if (_state == null || panelRoot == null || !panelRoot.activeSelf) return;

            if (altitudeText != null)
                altitudeText.text = HudFormat.Altitude(_state.AltitudeMeters);

            if (speedText != null)
                speedText.text = HudFormat.Speed(_state.SpeedMetersPerSecond);
        }
    }
}
```

- [ ] **Step 2: Delete the HUD controller and its folder**

```bash
git rm Assets/Scripts/Hud/HudController.cs Assets/Scripts/Hud/HudController.cs.meta
git rm Assets/Scripts/Hud.meta
rmdir Assets/Scripts/Hud    # must now be empty; if it is not, stop and report
```

- [ ] **Step 3: Now retire the dead `Compass`**

`HudController` was `HudFormat.Compass`'s only caller, and it is gone as of Step
2, so `Compass` is now dead — and only now safe to remove. **This deletion and
Step 2 must land in the same commit**; splitting them breaks the build in between.

From `Assets/Scripts/Cockpit/HudFormat.cs`, delete the `Points` array and the
whole `Compass` method, leaving exactly:

```csharp
using UnityEngine;

namespace Flusi
{
    /// Strings for the panel's digital readouts.
    public static class HudFormat
    {
        public static string Altitude(float metres) => $"{Mathf.RoundToInt(metres)} m";

        public static string Speed(float metresPerSecond)
            => $"{Mathf.RoundToInt(FlightDerivations.SpeedKmh(metresPerSecond))} km/h";
    }
}
```

From `Assets/Tests/EditMode/HudFormatTests.cs`, delete `Compass_North_IsN`,
`Compass_East_IsE` and `Compass_Wraps`, leaving exactly:

```csharp
using NUnit.Framework;

namespace Flusi.Tests
{
    public class HudFormatTests
    {
        [Test]
        public void Altitude_RoundsToWholeMetres()
            => Assert.AreEqual("1235 m", HudFormat.Altitude(1234.6f));

        [Test]
        public void Speed_ConvertsMpsToKmh()
            => Assert.AreEqual("360 km/h", HudFormat.Speed(100f)); // 100 m/s = 360 km/h
    }
}
```

- [ ] **Step 4: Verify it compiles, then test**

Controller: **check `mcp__unity__Unity_GetConsoleLogs` for errors BEFORE trusting
any test result.** When compilation fails, Unity's test runner silently runs the
last successfully-built assemblies and reports a stale pass — this exact trap
produced a bogus green earlier in this plan.

Expected: no errors. Then EditMode `passed=37` — down 3 from 40, which is the
Compass tests going. Missing-script warnings on `SampleScene` remain until Task
10 and are expected.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Cockpit/ Assets/Scripts/Hud.meta Assets/Scripts/Hud/ \
        Assets/Tests/EditMode/HudFormatTests.cs
git status --short
git commit -m "Replace the HUD controller with the cockpit panel"
```

---

## Task 10: Build the six-pack in the scene (controller, via MCP)

**This is a controller task — it drives the Unity Editor and cannot be delegated
to a subagent.** Per project CLAUDE.md, use MCP tools directly rather than
writing scripts into the project to do it.

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via the Editor)

**Interfaces:**
- Consumes: every component from Tasks 4–8.
- Produces: the `CockpitPanel` hierarchy under the existing `HUD` canvas.

- [ ] **Step 1: Read the current HUD hierarchy**

Use `mcp__unity__Unity_RunCommand` to dump the `HUD` GameObject's children and
each one's components, so the rebuild is grounded in what is actually there
rather than in what this plan assumes.

- [ ] **Step 2: Strip the old HUD children**

Delete the four floating `Text` overlays and the old horizon/compass objects from
under `HUD`, clearing the missing-script references left by Tasks 7 and 9.
Keep the `Canvas`, `CanvasScaler` and `GraphicRaycaster`.

- [ ] **Step 3: Create the panel root**

Under `HUD`, create `PanelRoot` (RectTransform + Image, opaque dark grey, e.g.
`RGBA(0.16, 0.17, 0.18, 1)`), anchored to the bottom third exactly:

```csharp
var rt = panelRoot.GetComponent<RectTransform>();
rt.anchorMin = new Vector2(0f, 0f);
rt.anchorMax = new Vector2(1f, 1f / 3f);
rt.offsetMin = Vector2.zero;
rt.offsetMax = Vector2.zero;
```

- [ ] **Step 4: Create the six gauge shells**

Under `PanelRoot`, create `SixPack` (RectTransform) occupying the left two thirds
of the panel, holding six children in a 2×3 grid — top row `Airspeed`,
`Attitude`, `Altimeter`; bottom row `TurnCoordinator`, `Heading`,
`VerticalSpeed`.

Each gauge shell gets a circular face using the built-in circle sprite:

```csharp
var sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
face.GetComponent<UnityEngine.UI.Image>().sprite = sprite;
face.GetComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.06f, 1f);
```

Fully-qualify `UnityEngine.UI.Image` — `Image` alone collides with the
`UnityEngine.UI.Image` / `UnityEngine.Experimental` namespaces and produced
`CS0118: 'Image' is a namespace but is used like a type` in earlier work.

- [ ] **Step 5: Add needles and wire the components**

Give each gauge a `Needle` child (a thin `Image`, pivot at `(0.5, 0)` so it
rotates about the gauge centre), then attach and calibrate:

| Gauge object | Component | Settings |
| --- | --- | --- |
| `Airspeed` | `NeedleGauge` | `channel=AirspeedKmh`, `minValue=0`, `maxValue=500`, `startAngle=0`, `sweepAngle=320` |
| `Attitude` | `AttitudeIndicator` | `bankRoot`, `pitchCard`, `rollPointer`, `pixelsPerPitchDegree=1.5` |
| `Altimeter` | `Altimeter` | `hundredsNeedle`, `thousandsNeedle` (short) |
| `TurnCoordinator` | `NeedleGauge` | `channel=BankDegrees`, `minValue=-55`, `maxValue=55`, `startAngle=-30`, `sweepAngle=60` |
| `Heading` | `HeadingIndicator` | `card` |
| `VerticalSpeed` | `NeedleGauge` | `channel=VerticalSpeed`, `minValue=-100`, `maxValue=100`, `startAngle=185`, `sweepAngle=170` |

Add a `GaugeFaceBuilder` to each face (except `Attitude`, whose face is the ball)
with the matching arc — e.g. Airspeed `startAngle=0, sweepAngle=320,
tickCount=21, majorEvery=4, labelMinValue=0, labelMaxValue=500`.

Add the **slip ball** to `TurnCoordinator`: a small circle `Image` pinned at the
face's centre-bottom inside a shallow tube graphic. **No component, no script** —
it never moves. See spec §3.3: a centred ball is a true statement about this
aircraft, not decoration.

Set every component's `aircraftSource` to the `Aircraft` GameObject's
`AircraftController`. `aircraftSource` is a private `[SerializeField]`, so wire it
with `SerializedObject`/`FindProperty`, as in the MVP's scene work:

```csharp
var so = new UnityEditor.SerializedObject(component);
so.FindProperty("aircraftSource").objectReferenceValue = aircraftController;
so.ApplyModifiedProperties();
```

- [ ] **Step 6: Verify in play mode**

Enter play mode via MCP, wait ~20 s for the domain reload, then assert
functionally: each gauge object exists, each has its component, and each needle's
`localEulerAngles.z` is non-zero and *changing* between two samples a second
apart while the aircraft flies. Exit play mode.

Do not rely on camera capture for verification — it failed in earlier work
(64-bit entity id versus the int32 MCP parameter).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "Assemble the six-pack gauge cluster in the cockpit panel"
```

---

## Task 11: Build the right-hand block (controller, via MCP)

**Controller task.**

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via the Editor)

**Interfaces:**
- Consumes: `AnnunciatorLamp`, `FuelGauge` (Task 8), `CockpitPanel` (Task 9),
  the existing `Minimap` from `Assets/Scripts/World/Minimap.cs`.
- Produces: the completed panel.

- [ ] **Step 1: Create the right block**

Under `PanelRoot`, create `RightBlock` (RectTransform) filling the right third.

- [ ] **Step 2: Reparent the minimap**

Move the existing minimap RectTransform under `RightBlock`. **Reparent it, do not
recreate it** — it already has its `panel`, `planeBlip` and marker prefab
references wired, and rebuilding it would mean redoing all of that.

Use `rt.SetParent(rightBlock, false)` so its local layout is preserved, then
resize to fit.

- [ ] **Step 3: Add the digital readouts**

Under `RightBlock`, add two `Text` objects, `AltitudeText` and `SpeedText`, using
`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. Large and legible —
these exist so a six-year-old who cannot read a clock face can still fly (spec
§2.1).

- [ ] **Step 4: Add the lamps and the fuel bar**

Under `RightBlock`:

- `AssistLamp` — `AnnunciatorLamp`, `channel=AutoLevel`, `onText="ASSIST ON"`,
  `offText="ASSIST OFF"`, with a `Text` caption and an `Image` lamp child.
- `GearLamp` — `AnnunciatorLamp`, `channel=GearDown`, `onText="GEAR DOWN"`,
  `offText="GEAR UP"`, same child structure.
- `FuelBar` — `FuelGauge`, with a `fillBar` child `Image` whose
  `type = Image.Type.Filled`, `fillMethod = Horizontal`, `fillOrigin = 0`.
  `level` stays at `1`. Label it `FUEL  E ▮▮ F`.

Wire each `aircraftSource` via `SerializedObject` as in Task 10.

- [ ] **Step 5: Add and wire `CockpitPanel`**

Add `CockpitPanel` to the `HUD` GameObject. Wire, all via `SerializedObject`:
`aircraftSource` → the `AircraftController`; `cameraRig` → the `CameraRig`
GameObject's component; `panelRoot` → `PanelRoot`; `altitudeText` → `AltitudeText`;
`speedText` → `SpeedText`.

- [ ] **Step 6: Verify in play mode**

Enter play mode and assert: `AltitudeText.text` matches `\d+ m`, `SpeedText.text`
matches `\d+ km/h`, the minimap still shows 3 POI markers plus the plane blip
(the MVP's marker-count check), and the gear lamp caption flips when
`AircraftController.ToggleGear()` is called. Exit play mode.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "Add minimap, readouts, lamps and fuel bar to the panel"
```

---

## Task 12: Letterbox the camera, and PlayMode smoke tests

**Controller task for the scene half; the test file can be delegated.**

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via the Editor)
- Modify: `Assets/Scripts/Cameras/CameraRig.cs`
- Test: `Assets/Tests/PlayMode/CockpitPanelSmokeTests.cs`

**Interfaces:**
- Consumes: everything above.
- Produces: `public void CameraRig.ToggleView()`.

- [ ] **Step 1: Make the view toggle reachable**

`CameraRig` currently has no public way to change view: `Current` has a private
setter and `OnToggleView` is private, reachable only from the input callback. The
smoke test below cannot drive it without dragging in the Input System test
framework.

Apply exactly the same fix Task 3 used for the gear — extract a public method and
have the callback call it. In `Assets/Scripts/Cameras/CameraRig.cs`, replace:

```csharp
        private void OnToggleView(InputAction.CallbackContext _)
        {
            Current = Current == ViewMode.Cockpit ? ViewMode.Orbit : ViewMode.Cockpit;
            Apply();
            ViewChanged?.Invoke(Current);
        }
```

with:

```csharp
        /// Public so tests and future cockpit switches can change view without
        /// synthesising keyboard input. Mirrors AircraftController.ToggleGear.
        public void ToggleView()
        {
            Current = Current == ViewMode.Cockpit ? ViewMode.Orbit : ViewMode.Cockpit;
            Apply();
            ViewChanged?.Invoke(Current);
        }

        private void OnToggleView(InputAction.CallbackContext _) => ToggleView();
```

Nothing else in `CameraRig` changes. `Current` keeps its private setter — the
only way to change view is still to toggle it.

- [ ] **Step 2: Restrict the cockpit camera to the top two thirds**

Set the `CockpitCamera`'s rect so the world becomes a true window rather than
being drawn full-screen and hidden behind the panel:

```csharp
cockpitCamera.rect = new Rect(0f, 1f / 3f, 1f, 2f / 3f);
```

Leave `OrbitCamera` at the full-screen default — the panel is hidden in that view.

**Risk, per spec §7:** this changes the camera's aspect and therefore its
effective horizontal field of view, and `Camera.rect` can interact awkwardly with
URP. If the horizon looks wrong or URP misbehaves, fall back to a full-screen
cockpit camera and let the opaque panel overlay the bottom third — visually near
identical. Record which route was taken in the commit message.

- [ ] **Step 3: Write the smoke tests**

Create `Assets/Tests/PlayMode/CockpitPanelSmokeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Flusi.Tests
{
    public class CockpitPanelSmokeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null; // let Awake/OnEnable settle
        }

        [UnityTest]
        public IEnumerator Panel_Builds_With_All_Six_Gauges()
        {
            yield return null;

            var panel = Object.FindFirstObjectByType<CockpitPanel>();
            Assert.IsNotNull(panel, "CockpitPanel should be in the scene");

            Assert.AreEqual(3, Object.FindObjectsByType<NeedleGauge>(
                FindObjectsSortMode.None).Length,
                "airspeed, turn coordinator and vertical speed");
            Assert.IsNotNull(Object.FindFirstObjectByType<Altimeter>());
            Assert.IsNotNull(Object.FindFirstObjectByType<AttitudeIndicator>());
            Assert.IsNotNull(Object.FindFirstObjectByType<HeadingIndicator>());
        }

        [UnityTest]
        public IEnumerator Panel_Hides_In_Orbit_View_And_Returns()
        {
            yield return null;

            var rig = Object.FindFirstObjectByType<CameraRig>();
            Assert.IsNotNull(rig, "CameraRig should be in the scene");

            var root = GameObject.Find("PanelRoot");
            Assert.IsNotNull(root, "PanelRoot should be in the scene");
            Assert.IsTrue(root.activeSelf, "panel visible in cockpit view");
            Assert.AreEqual(ViewMode.Cockpit, rig.Current);

            rig.ToggleView();
            yield return null;
            Assert.AreEqual(ViewMode.Orbit, rig.Current);
            Assert.IsFalse(root.activeSelf, "panel hidden in orbit view");

            rig.ToggleView();
            yield return null;
            Assert.AreEqual(ViewMode.Cockpit, rig.Current);
            Assert.IsTrue(root.activeSelf, "panel returns in cockpit view");
        }
    }
}
```

`SampleScene` is in Build Settings (verified at plan time), which
`SceneManager.LoadScene` requires in PlayMode tests.

- [ ] **Step 4: Run PlayMode tests**

Expected: `STATUS Passed`, `passed=6` — 1 aircraft smoke + 2 POI + 1 gear + 2
panel. Then run EditMode and confirm all 37 are green, including the 13 untouched
`FlightModelTests`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scenes/SampleScene.unity Assets/Scripts/Cameras/CameraRig.cs \
        Assets/Tests/PlayMode/CockpitPanelSmokeTests.cs
git status --short
git commit -m "Letterbox the cockpit camera and smoke-test the panel"
```

---

## Task 13: Bring `Specification.md` in line

**Files:**
- Modify: `Specification.md`

**Interfaces:** none.

The spec document is the project's source of truth and currently describes
instruments that no longer exist. Spec §9 lists every place that lies.

- [ ] **Step 1: Make the edits**

In `Specification.md`:

1. **Intro (line ~5):** "a 2D instrument HUD" → "a cockpit instrument panel".
2. **§3.3 keyboard table:** add a row — `` `ToggleGear` `` | "Raise / lower the
   landing gear".
3. **§4 Views & Cameras:** in the Cockpit bullet, "The 2D instrument HUD is shown
   in this view." → "The instrument panel fills the bottom third of the screen;
   the world is a window above it." In the External orbit bullet, "HUD is hidden
   in this view." → "The panel is hidden in this view."
4. **§7.1 Aircraft:** in the `IAircraftState` bullet, add `gear state` to the
   exposed list.
5. **§7.3:** retitle "HUD (2D, screen-space)" → "Cockpit panel (2D,
   screen-space)". Replace `HudController` with `CockpitPanel`, and describe the
   gauge components from the design doc §4.4.
6. **§8 MVP Scope item 4:** "**HUD instruments:** artificial horizon, altitude,
   speed, compass/heading, and a minimap…" → describe the six-pack plus minimap,
   lamps and readouts.
7. **§11 Testing:** "HUD reads plausible values" → "the panel builds and reads
   plausible values".

Add a line under the intro's metadata block:

```markdown
- **Amended:** 2026-07-16 — cockpit instrument panel replaces the HUD; see
  `docs/superpowers/specs/2026-07-16-cockpit-instruments-design.md`.
```

- [ ] **Step 2: Check nothing else still says "HUD"**

```bash
grep -ni 'hud' Specification.md
```

Expected: no hits describing the current design. A historical mention is fine
only if it is explicitly marked as superseded.

- [ ] **Step 3: Commit**

```bash
git add Specification.md
git commit -m "Update specification for the cockpit instrument panel"
```

---

## Done

**EditMode: 22 today → 37.**

| | |
| --- | --- |
| 13 | `FlightModelTests` — untouched |
| 8 | `GaugeScaleTests` (new, Task 1) |
| 5 | `AltimeterScaleTests` (new, Task 2) |
| 5 | `FlightDerivationsTests` (new, Task 2) |
| 2 | `HudFormatTests` (was 5; Task 9 retires the 3 Compass tests) |
| 3 | `MinimapProjectionTests` — untouched |
| 1 | `HarnessTest` — untouched |

**PlayMode: 3 today → 6.** 1 aircraft smoke + 2 POI registry (all untouched)
+ 1 gear (Task 3) + 2 panel (Task 12).

`FlightModel.cs` and `FlightConfig.cs` must show zero changes across the whole
branch:

```bash
git diff --stat main..HEAD -- Assets/Scripts/Flight/FlightModel.cs \
                              Assets/Scripts/Flight/FlightConfig.cs
```

Expected: empty output. If it is not, something went wrong.
