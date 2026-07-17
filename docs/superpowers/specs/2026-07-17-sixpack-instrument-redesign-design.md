# Six-Pack Instrument Visual Redesign — Design

Give the six round gauges real faces. Today `GaugeFaceBuilder` draws bare white
`Image` tick rectangles and `Text` numbers with no bezel, no colour arcs, no
background — functional but not a plane. This redesign gives each gauge a
generated background texture in the style of a real light-aircraft panel,
simplified for readability at HUD scale by a six-year-old.

- **Date:** 2026-07-17
- **Builds on:** `docs/superpowers/specs/2026-07-16-cockpit-instruments-design.md`
  (architecture, calibration numbers, data flow — unchanged here).
- **Status:** approved, ready for planning.
- **Reference images:** `workfiles/screenshots/cockpit-instruments/` (real
  Cessna-style six-pack photos and a diagram labelling all six gauges).

---

## 1. Motivation

The current gauges are calibrated correctly (`GaugeScale`, `AltimeterScale`,
`FlightDerivations` are all done and tested) but look like debug placeholders:
plain white ticks on the panel's flat background colour, no bezel ring, no
colour-coded operating ranges, no horizon art on the attitude indicator, no
compass rose art on the heading card.

This is a **presentation-only** change. No `IAircraftState` field, no gauge
channel, no calibration number, no data-binding script changes. Every needle,
card, and pointer keeps rotating exactly as it does today
(`NeedleGauge`, `Altimeter`, `AttitudeIndicator`, `HeadingIndicator` are
untouched). Only what each gauge draws *underneath* the moving parts changes.

---

## 2. Style target

**Simplified realistic** — not full photoreal, not flat cartoon.

- Real six-pack layout, real colour language (green/yellow/red arcs, blue-over-
  brown attitude horizon, black gauge faces with white markings).
- Cleaner than the reference photos: no worn metal texture, no screw heads, no
  glare, no small print. Ticks and numerals sized to read clearly at the HUD's
  actual on-screen size, not at photo-reference density.
- Still unmistakably "real aeroplane instrument," not an abstracted icon.

---

## 3. Build method

Each gauge gets one (or two, for the attitude indicator) **generated
background sprite** via `Unity_AssetGeneration_GenerateAsset`
(`GenerateSprite`/`GenerateImage`), produced against this doc's style target
and the reference screenshots. Bezel ring, static tick marks, static numerals,
and colour arcs are baked into that background art.

The **moving parts stay procedural**: needles, the attitude ball/pitch card,
and the heading compass card remain plain `Image` rectangles (or, for the
compass card, generated art that itself rotates — see §4.5) driven by the
existing unchanged scripts. This keeps needle motion pixel-crisp at any
resolution and keeps the calibration numbers in `NeedleGauge`/`Altimeter`/
`AttitudeIndicator`/`HeadingIndicator` as the single source of truth — art
never encodes a calibration value that C# also encodes.

`GaugeFaceBuilder` is disabled (component removed or `enabled = false`) on any
gauge whose background art now carries baked ticks/labels, to avoid drawing
duplicate ticks on top of the art. It is not deleted from the codebase: if a
future gauge wants procedural ticks instead of generated art, the component
still works.

---

## 4. Per-instrument specification

Order matches the real six-pack layout and the existing scene hierarchy names
(`Airspeed`, `Attitude`, `Altimeter`, `TurnCoordinator`, `Heading`,
`VerticalSpeed`), done one at a time with an in-Editor Play-mode look after
each before moving to the next.

### 4.1 Airspeed Indicator

- Background: black face, white ticks and numerals baked from 0–460 in
  km/h steps matching the existing `NeedleGauge` calibration
  (`GaugeChannel.AirspeedKmh`, 0–500 range, 0°/+320° sweep — spec
  2026-07-16 §3.1/§3.2).
- Colour arc baked into the same texture: green ~150–400 km/h (normal
  operating range), yellow 400–460 km/h (caution), a red radial line at
  468 km/h (`FlightConfig.MaxSpeed` × 3.6 = Vne).
- Moving part: needle only, unchanged.

### 4.2 Attitude Indicator

Two art pieces, both static textures, both already matched to the existing
rotation/translation math (`AttitudeIndicator.cs` — `bankRoot` rotates,
`pitchCard` translates inside it):

- **Pitch card** (assigned as the `pitchCard` sprite): blue-over-brown horizon
  with a straight horizon line and pitch-ladder ticks at ±10/20/30°, generously
  oversized vertically so it never runs out of texture at the ±45° pitch
  extreme (`FlightConfig.MaxPitchDeg`).
- **Bezel** (fixed background, does not rotate): ring with bank-angle ticks at
  0/10/20/30/60° either side, plus a fixed miniature-aircraft symbol (two
  short wings and a dot) overlaid on top so it reads as "the aeroplane" against
  the moving horizon.
- Roll pointer (`rollPointer`, currently rotating with bank against the fixed
  bezel) keeps its existing behaviour; only its sprite gets a small
  triangle-pointer look instead of a bare rectangle.

### 4.3 Altimeter

- Background: black face, ticks and numerals 0–9 baked (one revolution per
  1000 m per the long needle — spec 2026-07-16 §4.2), matching
  `AltimeterScale`.
- No colour arc — real altimeters don't carry one.
- Moving parts: hundreds needle and thousands needle, unchanged.

### 4.4 Turn Coordinator

Real turn coordinators have no numbered face — this is the one gauge whose
background is mostly text/reference marks, not a scale:

- Background: "L"/"R" labels, two standard-turn reference marks (used in real
  aircraft to judge a 2-minute turn), a small "NO PITCH INFORMATION" caption.
- Moving part: today's plain needle sprite is replaced with a small
  airplane-silhouette sprite (wings + tail, viewed from behind), still driven
  by the same `GaugeChannel.BankDegrees` value and the same needle-rotation
  code. This remains a simplified bank-mimic, not a true turn-rate gauge — no
  turn-rate data exists on `IAircraftState`, and spec 2026-07-16 §3.1 already
  established that bank is an accurate proxy for this flight model. The slip
  ball stays a static centred graphic per that same spec (§3.3) — accurate,
  since this flight model has no sideslip by construction.

### 4.5 Heading Indicator

The one gauge where the *rotating* part carries the numbered art, not the
background:

- Background (fixed): bezel ring plus a fixed lubber-line marker (small
  triangle) at 12 o'clock and a small fixed aircraft-symbol dot at centre.
- **Compass card** (assigned as the `card` sprite, the thing that rotates):
  generated art with N/E/S/W cardinal letters and 30°-spaced tick numerals
  (03, 06, 09 … in the usual compass-card shorthand, or plain 30/60/90…,
  whichever reads more clearly to a six-year-old — decide by eye at
  generation time). `HeadingIndicator.cs`'s rotation math is unchanged.

### 4.6 Vertical Speed Indicator

- Background: black face, ticks at 0/20/40/60/80/100 m/s up and down (matching
  the ±100 m/s range from spec 2026-07-16 §3.1/§3.2), "UP"/"DOWN" labels, a
  small caption.
- Moving part: needle, unchanged.

---

## 5. Workflow

Per instrument, in the §4 order:

1. `Unity_AssetGeneration_GetModels` → pick a model suited to flat,
   vector-leaning gauge-face art (not a photoreal model) — done once, reused
   across all six.
2. Generate the background sprite(s) for that instrument against its §4 spec
   and the reference screenshots. Iterate if the result reads too busy, too
   photoreal, or miscalibrated (wrong arc position, wrong tick count).
3. Save into `Assets/Sprites/Cockpit/`. New asset + its `.meta` land in the
   same commit (CLAUDE.md rule).
4. Wire via `Unity_RunCommand`: swap the `Image.sprite` reference
   (`SerializedObject`/`FindProperty`/`ApplyModifiedProperties`, private
   field), resize the `RectTransform` to match the art's native proportions,
   disable/remove `GaugeFaceBuilder`-authored tick/label children on that
   gauge if superseded.
5. Dump the gauge's `RectTransform` tree and ask the project owner for an
   in-Editor Play-mode look (camera capture doesn't see overlay UI — CLAUDE.md)
   before starting the next instrument.

Six independent passes; a bad result on one instrument does not block the
others, and each is a small, revertable commit.

---

## 6. Testing

No logic or data changes, so no new EditMode/PlayMode tests are written.

The existing EditMode 41 / PlayMode 6 baseline (spec 2026-07-16 §5, plus
anything added since) must still pass after each instrument's wiring change,
as a regression check that the `SerializedObject` edits didn't accidentally
touch a data-binding field. Verified live per CLAUDE.md's stale-pass caveat —
console-clean plus the usual symbol-in-loaded-assembly check if any script is
touched.

Manual verification is the primary signal here: this is a visual task, and the
target reviewer is the project owner (and eventually the six-year-old).

---

## 7. Non-Goals

- No new instrument data (no turn-rate, no sideslip, no RPM — all previously
  excluded in spec 2026-07-16 §3.6 for lack of underlying simulation).
- No changes to `FlightModel`, `FlightConfig`, or any calibration constant.
- No changes to panel layout, camera rect, digital readouts, minimap, fuel
  gauge, or annunciator lamps — those are out of scope for this pass.
- No 3D instrument geometry — gauges stay flat 2D sprites under rotating UI
  elements, consistent with spec 2026-07-16 §8.
