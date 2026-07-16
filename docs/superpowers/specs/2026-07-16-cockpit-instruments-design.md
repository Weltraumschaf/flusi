# Cockpit Instrument Panel — Design

Replace the floating 2D HUD text overlays with an opaque cockpit instrument
panel carrying real analogue gauges, in the style of Sublogic's *Flight
Simulator II* (reference: `docs/cockpit-view-amiga-game.png`) but rendered
cleanly rather than in 16-colour pixels.

- **Date:** 2026-07-16
- **Supersedes:** `Specification.md` §4, §7.3, §8.4 (see §9 below).
- **Status:** approved, ready for planning.

---

## 1. Motivation

The MVP shipped its instruments as a transparent heads-up display: four text
labels and a sliding horizon floating over a full-screen view. It reads as
*wearing a HUD*, not as *sitting in an aeroplane*.

The requested change is structural, not cosmetic. In the reference the panel is
**opaque and occupies real estate**; the outside world becomes a letterboxed
window above it. That framing is what sells the cockpit.

This is a **presentation-only change**. It consumes the existing read-only
`IAircraftState` seam and touches no flight code. `FlightModel`,
`AircraftController`, `FlightConfig` and all 22 EditMode flight tests are
unchanged.

---

## 2. Layout

Screen splits horizontally:

- **Top two thirds:** the cockpit camera's viewport (the "window").
- **Bottom third:** the opaque instrument panel.

```
┌────────────────────────────────────────────────────────┐
│                                                        │
│                  cockpit window (2/3)                  │
│                                                        │
├─────────────────────────────┬──────────────────────────┤
│  ASI     ATTITUDE     ALT   │   ALT      3 500 m       │
│                             │   SPD        320 km/h    │
│  TURN    HEADING      VSI   │  ┌──────────┐            │
│                             │  │ MINIMAP  │   ASSIST   │
└─────────────────────────────┴──────────────────────────┘
        left ~2/3 of panel          right ~1/3 of panel
```

The left block is the standard light-aircraft **"six pack"**: airspeed,
attitude, altimeter on the top row; turn coordinator, heading, vertical speed
on the bottom. This is the real arrangement, and it is why the grid is 2×3
rather than the reference's 2×4 — the reference pads its rows with VOR dials we
have no data for.

The minimap inherits the rectangular area the reference gives to its radio
stack.

### 2.1 Digital readouts

Altitude and speed additionally appear as large plain numbers on the right.

Rationale: an altimeter *is* a clock face, and the target player is six and
cannot yet read one. Authentic dials to look at, plain numbers to fly by —
which is what modern glass cockpits do anyway. If they crowd the panel in
playtest, deleting them is a one-line change.

Speed is **km/h**, not the reference's knots, for the same reason.

### 2.2 Camera

`CockpitCamera.rect` becomes `Rect(0, 1/3, 1, 2/3)` so the world is a true
window and the horizon sits centred in it, rather than being drawn full-screen
and then hidden behind the panel.

Note this changes the camera's aspect and therefore its effective horizontal
field of view. Confirm in playtest; `fieldOfView` is the tuning knob.

`OrbitCamera` keeps a full-screen rect and is unaffected.

---

## 3. Instruments

All six gauges are driven by real state. **Nothing on this panel is a prop.**

| Gauge | Source | Range | Needle sweep |
| --- | --- | --- | --- |
| Airspeed (ASI) | `SpeedMetersPerSecond` × 3.6 | 0–500 km/h | 0° at 12 o'clock, +320° clockwise |
| Attitude (AI) | `PitchDegrees`, `BankDegrees` | ±45° pitch, ±55° bank | n/a — ball rolls and slides |
| Altimeter (ALT) | `AltitudeMeters` | 0–1000 m per revolution | two needles, see §4.2 |
| Turn coordinator (TC) | `BankDegrees` | ±55° | ±30° of symbol roll |
| Heading (HI) | `HeadingDegrees` | 0–360° | card rotates by −heading |
| Vertical speed (VSI) | derived, see §4.3 | ±100 m/s | 0 at 9 o'clock, ±85° |

Plus: minimap (unchanged), an **ASSIST** lamp reading `AutoLevelOn`, and the two
digital readouts.

### 3.1 Calibration notes

- **Airspeed:** the flight envelope is 40–130 m/s = 144–468 km/h, so a 0–500
  dial covers it with headroom. Minor ticks every 25, labels every 100.
- **Altimeter:** long needle = hundreds (one revolution per 1000 m), short
  needle = thousands (one revolution per 10 000 m). Face labelled 0–9.
- **Turn coordinator:** driven directly by bank rather than by a computed turn
  rate. In this flight model heading rate is strictly proportional to bank
  (`heading += (bank / MaxBankDeg) * TurnRateDegAtMaxBank * dt`), so banking the
  symbol proportionally to bank conveys exactly the same information without
  duplicating `FlightConfig` values into the display layer.
- **Vertical speed:** the envelope is ±130 × sin(45°) ≈ ±92 m/s, so ±100 covers
  it. Ticks every 20.

Every one of these numbers is a serialized field on the component, tunable in
the Inspector while the game runs.

### 3.2 Concrete `GaugeScale` arguments

Using the §4.1 convention (0° = 12 o'clock, positive = clockwise):

| Gauge | value range | `startAngle` | `sweepAngle` | Reads |
| --- | --- | --- | --- | --- |
| Airspeed | 0 → 500 km/h | 0° | +320° | 0 at 12 o'clock, full scale at 8 o'clock |
| Vertical speed | −100 → +100 m/s | 185° | +170° | full descent just past 6 o'clock, zero at 9 o'clock (270°), full climb just before 12 |
| Turn coordinator | −55 → +55° bank | −30° | +60° | symbol level at zero bank, ±30° of roll at full bank |

The altimeter and heading indicator do not use `GaugeScale` — see §4.2 and §4.4.

### 3.3 Deliberately excluded

- **RPM / power.** The model's throttle is an accelerator, not a lever
  (`Speed += Throttle * ThrottleAccel * dt`), so there is no persistent throttle
  position to display. An RPM gauge would either track key presses or shadow the
  airspeed needle exactly. Adding a real throttle lever to the flight model was
  considered and rejected — it would change how the aeroplane feels and force a
  re-tune with the player.
- **Slip ball.** Turns are always coordinated by construction, so it would never
  leave centre.
- **Fuel, oil temperature, oil pressure, gear, flaps, mags, NAV/COM/DME/ADF,
  VOR/OBI.** No engine, systems, or navigation-radio simulation exists to drive
  them.

---

## 4. Architecture

Follows the established pattern: pure static logic (EditMode-testable, like
`HudFormat` and `MinimapProjection`) behind thin MonoBehaviours.

### 4.1 `GaugeScale` — pure

```csharp
public static float ValueToAngle(float value, float minValue, float maxValue,
                                 float startAngle, float sweepAngle)
```

Maps a value onto a needle angle, clamped at both ends. Drives every needle
gauge. Angle convention: **degrees, 0 = needle points to 12 o'clock, positive =
clockwise**; the component applies `localEulerAngles.z = -angle` because Unity's
Z rotation runs counter-clockwise.

### 4.2 `AltimeterScale` — pure

```csharp
public static float HundredsAngle(float altitudeMetres);   // Repeat(alt, 1000)  / 1000  * 360
public static float ThousandsAngle(float altitudeMetres);  // Repeat(alt, 10000) / 10000 * 360
```

### 4.3 `FlightDerivations` — pure

```csharp
public static float VerticalSpeed(float speedMetresPerSecond, float pitchDegrees);
public static float SpeedKmh(float metresPerSecond);
```

`VerticalSpeed` is `speed * sin(pitch)`. This is not an approximation: the model
integrates position along `Quaternion.Euler(-Pitch, Heading, 0) * Vector3.forward`,
whose Y component is exactly `sin(pitch)`. The gauge therefore reports precisely
what the aeroplane does.

### 4.4 MonoBehaviours

| Unit | Responsibility |
| --- | --- |
| `CockpitPanel` | Owns the panel root; shows in cockpit view, hides in orbit. Replaces `HudController`'s role. |
| `NeedleGauge` | One component, several instances, distinguished by a `GaugeChannel` enum (`Airspeed`, `VerticalSpeed`, `Bank`). Reads `IAircraftState`, calls `GaugeScale`, rotates its needle. |
| `Altimeter` | Two needles via `AltimeterScale`. |
| `HeadingIndicator` | Rotating compass card. Evolved from `HeadingCompass`. |
| `AttitudeIndicator` | Rolling/sliding ball under a circular mask, fixed aircraft symbol, roll pointer. Evolved from `ArtificialHorizon`. |
| `GaugeFaceBuilder` | Places tick marks and labels around a circle at `Awake`. |
| `AnnunciatorLamp` | ASSIST on/off. |
| `Minimap` | Unchanged; reparented into the panel. |

`NeedleGauge` uses an enum-and-switch rather than an abstract value-source
hierarchy: fewer types, readable, and Inspector-friendly — which matters because
calibration happens live.

### 4.5 Art

No texture assets and no new dependencies. Faces are assembled from Unity
built-ins:

- Circle face and bezel: `Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd")`.
- Circular mask for the attitude ball: `Mask` + the same sprite.
- Labels: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`, already used
  in this project.
- Ticks and needles: plain `Image` rectangles positioned by `GaugeFaceBuilder`.

### 4.6 Panel anchoring

The panel root anchors `anchorMin = (0, 0)`, `anchorMax = (1, 1/3)` with zero
offsets, so it is exactly the bottom third at any resolution and lines up with
the camera rect by construction.

### 4.7 Data flow

```
AircraftController ──exposes──> IAircraftState   (unchanged)
                                      │
                                      ▼
                                CockpitPanel
                                      │
     ┌──────────┬──────────┬──────────┼──────────┬──────────┐
     ▼          ▼          ▼          ▼          ▼          ▼
 NeedleGauge  Altimeter  Heading  Attitude   Minimap   Annunciator
     │          │        Indicator Indicator
     ▼          ▼
 GaugeScale  AltimeterScale
 FlightDerivations
```

---

## 5. Testing

**EditMode (pure units):**

- `GaugeScale`: angle at min, at max, at midpoint; clamping below min and above
  max; a negative sweep (counter-clockwise gauge).
- `AltimeterScale`: 0 m; 500 m; wrap at exactly 1000 m; 1500 m; a negative
  altitude (`Mathf.Repeat` must not produce a negative angle).
- `FlightDerivations`: zero vertical speed in level flight; positive nose-up;
  negative nose-down; the sin relationship at 30°; `SpeedKmh` conversion.

**PlayMode (smoke):**

- The panel builds without throwing and all six gauges are present.
- The panel hides when toggling to orbit view and returns on toggling back.

**Manual:** the six-year-old. Gauge calibration, panel height and camera field of
view are tuned in the Editor while flying.

---

## 6. Migration

**Removed:** `HudController` and the four floating `Text` overlays.
`ArtificialHorizon` and `HeadingCompass` are superseded by `AttitudeIndicator`
and `HeadingIndicator`.

**Kept unchanged:** `IAircraftState`, `Minimap`, `MinimapProjection` (+ its 3
tests), `HudFormat` (+ its 5 tests — the digital readouts still use it), and the
entire `Flight` namespace.

---

## 7. Risks

- **Camera rect and URP.** Restricting `Camera.rect` changes aspect and can
  interact awkwardly with URP post-processing. Fallback: render the camera
  full-screen and let the opaque panel overlay the bottom third. Visually near
  identical; decide in playtest.
- **Object count.** Six gauges × (face + bezel + ~24 ticks + ~10 labels + needle)
  is roughly 250 UI objects. Fine at this scale, but if the canvas rebuilds
  become a cost, ticks and labels are static and can be moved onto a
  non-interactive sub-canvas.
- **Readability.** The digital readouts are the mitigation; if the panel still
  reads as busy to a six-year-old, drop gauges rather than shrink them.

---

## 8. Non-Goals

- No 3D cockpit geometry, window frame, or modelled flight deck. The panel stays
  2D screen-space. (Considered and deferred.)
- No visible yoke or throttle levers.
- No changes to the flight model, input map, or control scheme.
- No authentic Concorde flight deck. The reference is a light-aircraft panel and
  that is what we build.

---

## 9. Specification.md Updates

`Specification.md` currently describes the instruments as a HUD and must be
brought in line as part of this work:

- Intro (line ~5): "a 2D instrument HUD" → cockpit instrument panel.
- §4 Views & Cameras: the cockpit view description and the "HUD is hidden in
  this view" note.
- §7.3 "HUD (2D, screen-space)": retitle and replace with the §4.4 units above.
- §8 MVP Scope item 4: reword the instrument list.
- §11 Testing: the HUD bullet.
