# FluSi — Specification

A simple flight simulator built as a gift for a six-year-old. The player flies a
Concorde over a handcrafted island in a calm, no-pressure sandbox, with an
easy keyboard control scheme, a 2D instrument HUD, and switchable cockpit and
external views.

- **Engine:** Unity 6 (6000.3.19f1), C#, Universal Render Pipeline (URP).
- **Input:** New Input System (`com.unity.inputsystem`), keyboard only in v1.
- **Platforms:** macOS and Linux standalone.
- **Date:** 2026-07-15.

---

## 1. Design Pillars

1. **Easy for a six-year-old.** Few keys, forgiving behaviour, no way to "lose."
2. **Cool to look at.** A recognisable Concorde, a cockpit view with instruments,
   and an external view to admire the plane.
3. **A world worth exploring.** Mountain, coastline, city, islands, airports, and
   landmarks to fly toward.

---

## 2. Gameplay

- **Free-flight sandbox.** No missions, no scoring, no fail states.
- **Starts airborne** over the island so there is no tricky ground taxi or takeoff.
- **Forgiving landing is possible:** flying low, slow, and level near a runway
  counts as a landing (optional friendly acknowledgement, e.g. a "Nice landing!"
  message — post-MVP polish). A rough approach carries no penalty.
- **No crashes.** Terrain and ground contact are handled gently (see §6).

---

## 3. Controls & Flight Model

### 3.1 Flight model — kinematic arcade

The plane is **not** a physics `Rigidbody`. A pure-logic `FlightModel` computes the
plane's new velocity and orientation each fixed step from the current control
inputs and applies the result directly to the transform.

Rationale: deterministic, easy to keep gentle (speed clamps, guaranteed
self-levelling, soft terrain contact), and unit-testable. Realistic aerodynamics
would fight the "easy and forgiving" pillar. Real aerodynamics/`Rigidbody` forces
are an explicit non-goal (§9).

Arcade rules the model enforces:

- **Coordinated turning:** the `Turn` input banks the plane and turns it together
  (roll into the turn), rather than raw yaw — this reads as natural flight.
- **Speed clamp:** airspeed is bounded to a comfortable min/max; the plane cannot
  stall or overspeed.
- **Tuned cruise speed:** cruise is a gentle *arcade* speed, **not** Mach 2, so a
  modestly sized world still feels like a real flight (see §5).
- **Stabilization term:** a self-levelling force that pulls pitch and bank back
  toward level. This term is enabled/disabled by the auto-level toggle — it is one
  flight model with stabilization on or off, not two separate models.

### 3.2 Auto-level assist (toggle)

- Acts like training wheels. When **on**, releasing the controls returns the plane
  smoothly to level flight; the player cannot get badly disoriented.
- **Default: ON.** A single key toggles it off for full manual control and back on
  again. The current state is exposed to the HUD (§4).

### 3.3 Keyboard mapping (New Input System)

A single `Flight` action map. Bindings are keyboard in v1; because it uses the
Input System, a gamepad or other device can be added later without code changes.

| Action            | Purpose                                            |
| ----------------- | -------------------------------------------------- |
| `Pitch`           | Nose up / down                                     |
| `Turn`            | Bank-and-turn left / right (coordinated)           |
| `Throttle`        | Increase / decrease speed                          |
| `ToggleAutoLevel` | Enable/disable the self-levelling assist           |
| `ToggleView`      | Switch between cockpit and external orbit view     |
| `OrbitLook`       | Rotate the external orbit camera around the plane  |

(Concrete key choices are finalised during implementation and tuned in playtest.)

---

## 4. Views & Cameras

Two views, toggled with `ToggleView`:

- **Cockpit (first-person):** view from the nose of the plane. The 2D instrument
  HUD is shown in this view.
- **External orbit:** camera tracks the plane's position while `OrbitLook` rotates
  the viewing angle around it. Works both in flight and parked, for admiring the
  Concorde. HUD is hidden in this view.

A `CameraRig` owns the switch; `CockpitCamera` and `OrbitCamera` are separate
units. Chase and cinematic/flyby cameras are **post-MVP** (§8).

---

## 5. World

One **handcrafted** island region — no procedural generation, no real-world data.

- **Size:** approximately **80–100 km square**, centred on the world origin so the
  plane stays within a float-precision-comfortable radius (~50 km of origin). See
  §7 for the precision rationale.
- **Terrain:** Unity `Terrain` with a central mountain, a rolling coastline, a few
  smaller offshore islands, and a sea plane at altitude 0.
- **Placed content:**
  - 1–2 **airports** (runway meshes) each carrying an `Airport` marker.
  - A small **city** cluster of buildings.
  - A few distinctive **landmarks** (e.g. a lighthouse, a large bridge) each
    carrying a `Landmark` marker.
- **Boundary:** no hard wall. Flying past the coast leads to open ocean; auto-level
  (and optionally a gentle turn-back nudge) keeps things calm. No death, no reset.

### 5.1 Art pipeline

- **Source:** free/paid **Unity Asset Store** models (low-poly Concorde and scenery
  packs). Modelling from scratch is out of scope; the author has no modelling
  background.
- **Placeholders first:** gameplay is built against simple primitive placeholders
  (a stretched capsule "Concorde", box buildings). Real models are swapped in later
  by replacing the mesh under the same GameObjects, so gameplay is never blocked on
  finding assets.

---

## 6. Collision & Ground Contact

Handled by the kinematic model, always gentle:

- The model samples terrain height beneath the plane. If the plane would descend
  below the ground, altitude is clamped and the plane levels out — a soft "skim,"
  never a crash or explosion.
- Landing is simply low, slow, level flight near a runway (§2). There is no fail
  case anywhere in v1.

---

## 7. Architecture

Each unit has one responsibility. Instruments and the minimap depend on a
**read-only state interface**, never on flight internals.

### 7.1 Aircraft

- **`FlightModel`** — pure logic. Input: control values + `autoLevel` flag + `dt`.
  Output: new position, orientation, velocity. Holds all arcade rules (§3.1). No
  scene dependencies beyond math, so it is EditMode unit-testable.
- **`AircraftController`** (`MonoBehaviour`) — reads the Input System, calls
  `FlightModel` each `FixedUpdate`, applies the result to the transform, and owns
  the auto-level toggle state.
- **`IAircraftState`** — read-only interface exposing altitude, speed, heading,
  pitch, bank, world position, and auto-level state. Implemented by the controller;
  consumed by the HUD and minimap so they never touch flight internals.

### 7.2 Cameras

- **`CameraRig`** — switches the active view on `ToggleView`.
- **`CockpitCamera`** — first-person view from the nose.
- **`OrbitCamera`** — tracks plane position; `OrbitLook` rotates the angle.

### 7.3 HUD (2D, screen-space)

- **`HudController`** — owns the instrument widgets and shows/hides the HUD per
  view (visible in cockpit, hidden in orbit).
- Each **instrument widget** reads `IAircraftState`. See §8 for the v1 set.

### 7.4 World services

- **`Airport`** / **`Landmark`** components self-register into a
  **`PointOfInterestRegistry`**. The minimap reads the registry to draw markers, so
  adding a landmark is just dropping a component into the scene — no minimap code
  change.

### 7.5 Data flow (per frame)

```
Input System ──> AircraftController ──(controls + autoLevel + dt)──> FlightModel
                        │                                                │
                        │<────────── new pos/orientation/velocity ───────┘
                        ▼
                 transform updated
                        │
              exposes IAircraftState
                        ▼
        ┌───────────────┴───────────────┐
        ▼                               ▼
   HUD instruments              minimap (+ POI registry)

CameraRig ──reads──> transform (cockpit follows nose / orbit tracks position)
```

---

## 8. MVP Scope

The first playable milestone — build this first:

1. Keyboard `Flight` action map + kinematic `FlightModel` with the auto-level toggle.
2. Placeholder Concorde flying over a placeholder island (terrain + sea + one mountain).
3. Cockpit view and external orbit view, toggled with a key.
4. **HUD instruments:** artificial horizon, altitude, speed, compass/heading, and a
   minimap showing the plane, airports, and landmarks (the minimap serves as the
   "radar").
5. One airport plus a couple of landmarks registered on the minimap.
6. Soft terrain/ground contact (no crashes).

### Post-MVP polish (after the MVP works)

- Real Asset Store models swapped in for placeholders.
- City detail and additional landmarks.
- Audio: engine loop pitched by throttle, wind ambience.
- Additional external cameras: chase cam, cinematic/flyby.
- "Nice landing!" acknowledgement.
- Additional input devices (e.g. gamepad) via the existing action map.

---

## 9. Non-Goals (v1)

Explicitly out of scope, to keep the first version focused:

- Realistic aerodynamics, stalls, or spins.
- Full taxi + takeoff from a parked start; landing-gear physics.
- Combat, enemies, weapons.
- Missions, objectives, scoring.
- Multiplayer.
- Weather, day/night cycle.
- Save/load or persistence.
- Floating-origin / streaming huge world (see §10).
- Mobile or Windows builds.

---

## 10. Documented Upgrade Path

If a genuinely huge, Concorde-scale world is wanted later, add **floating origin**
(periodically re-centre the world on the plane to keep coordinates near origin)
together with **terrain streaming**. This is deliberately excluded from v1 because
the ~80–100 km handcrafted island plus tuned arcade speed delivers the "real
flight" feeling without that engineering. The read-only `IAircraftState` and the
POI registry are designed so this can be added without reworking the HUD or gameplay.

---

## 11. Testing

- **EditMode unit tests** on `FlightModel` (pure logic, fast, deterministic):
  - Releasing controls with auto-level ON converges toward level flight.
  - Airspeed clamps at min and max.
  - `Turn` produces a coordinated bank.
  - Terrain-height clamp prevents descending below the ground.
- **PlayMode smoke tests:** scene loads, plane flies, view toggles, HUD reads
  plausible values (mostly "nothing threw").
- **Manual playtest:** the real judge is a six-year-old. Speed, turn rate, and
  auto-level strength are tuned iteratively in the Editor.
