# FluSi

This is a simple flight simulator for my six years old son.

## User Manual

### Controls

Keyboard only. Click inside the Game view first, otherwise the keys go to the
Editor instead of the plane.

| Key       | Action                                            |
|-----------|---------------------------------------------------|
| ⬆️        | 🛬 Nose down                                      |
| ⬇️        | 🛫 Nose up                                        |
| ⬅️        | ← Bank and turn left                              |
| ➡️        | → Bank and turn right                             |
| `W`       | 🐇 Faster                                         |
| `S`       | 🐢 Slower                                         |
| `C`       | 🎥 Switch between cockpit and external orbit view |
| `A` / `D` | 🎥 ↔ Rotate the orbit camera (external view only) |
| `L`       | Toggle the auto-level assist (starts on)          |
| `G`       | 🛞 ↕ Raise/lower the landing gear                 |

**The pitch keys are inverted on purpose, not by mistake.** The up arrow is the
stick pushed forward, which puts the nose down; the down arrow is the stick
pulled back, which brings the nose up. That is how a real aeroplane works and
how the pilot this was built for already flies. Please do not "correct" it.

Turning is coordinated: the left and right arrows roll the plane into the turn
rather than yawing it flat. With the auto-level assist on, releasing the arrows
brings the wings and nose back to level; with it off, the plane holds whatever
bank and pitch you leave it in.

### Instruments 

The cockpit instruments are divided into three main sections:

```text
┌───────────────────────────────────┐┌─────────────────┐┌───────────────────────────────────┐
│                                   ││                 ││                                   │
│                                   ││                 ││                                   │
│                                   ││                 ││                                   │
│              six-pack             ││     minimap     ││             others                │
│                                   ││                 ││                                   │
│                                   ││                 ││                                   │
│                                   ││                 ││                                   │
└───────────────────────────────────┘└─────────────────┘└───────────────────────────────────┘
```

#### The Six-Pack

The six gauges on the lift side are a typical Cesna style _six-pack_:

```text
┌────────────────────────┐ ┌────────────────────────┐ ┌─────────────────────────┐
│          ASI           │ │          AI            │ │          ALT            │
│                        │ │                        │ │                         │
│   Airspeed Indicator   │ │   Attidude Indicator   │ │        Altimeter        │
└───────────────┴────────┘ └────────────────────────┘ └─────────────────────────┘
┌────────────────────────┐ ┌────────────────────────┐ ┌─────────────────────────┐
│          TC            │ │          HI            │ │          VSI            │
│                        │ │                        │ │                         │
│    Turn Coordinator    │ │   Heading Indicator    │ │ Vertic  Speed Indicatore│
└────────────────────────┘ └────────────────────────┘ └─────────────────────────┘
```

**Top row (left → right)**:

- **ASI**: Airspeed Indicator (knots, with color arcs for flap/normal/never-exceed ranges)
- **AI**: Attitude Indicator, aka artificial horizon (pitch + bank)
- **ALT**: Altimeter (altitude, with Kollsman window for pressure setting)

**Bottom row (left → right)**:

- **TC**: Turn Coordinator (rate of turn + inclinometer "ball" for coordination); older aircraft have a T&B, Turn & Bank indicator
- **HI**: Heading Indicator, also called DG (Directional Gyro) — magnetic heading, drifts and must be reset against the compass
- **VSI**: Vertical Speed Indicator (feet per minute, climb/descent)

### Minimap

- A small static map of the world. The plane moves on the map as it flies.

### Others

Here are all other indicators:

- Altitude as number
- Speed in km/h
- Fuel
- Assist on/off
- Gear down

## Specification

See `docs/Specification.md`.

## Technology Stack

- Unity with C#.
- Running on macOS and Linux.

### Setup Tools

```shell
brew install --cask unity-hub
brew install --cask dotnet-sdk
```

## Building a Release

Install the **Mac Build Support (IL2CPP)**, **Linux Build Support (IL2CPP)**,
and **Linux Dedicated Server Build Support** modules for your Unity install via
Unity Hub (Installs → gear icon → Add Modules) — cross-compiling the Linux
build from macOS works fine, no Linux machine needed. The Server module isn't
used directly, but without it the Editor fails the desktop Linux build with
"No Linux sysroot found" / "Unable to find an Linux Sysroot" even though the
Linux sysroot/toolchain packages are installed (a known Unity bug tying their
discovery to the Server component).

### Via the Editor GUI

1. `File > Build Settings`
2. Confirm `Assets/Scenes/MainScene.unity` is checked under "Scenes In
   Build"
3. Set **Platform** to `macOS`, click `Switch Platform`, then `Build` and
   choose an output folder (e.g. `Builds/macOS/`)
4. Switch **Platform** to `Linux`, `Switch Platform`, then `Build` into a
   separate output folder (e.g. `Builds/Linux/`)

Switching platforms re-imports platform-specific asset variants, so do the two
builds one at a time.

### Via Taskfile

Install [go-task](https://taskfile.dev) (`brew install go-task`), then from
the project root:

```shell
task build:mac    # macOS only, to Builds/macOS/
task build:linux  # Linux only, to Builds/Linux/
task build        # both (also the default: `task` alone runs this)
task clean        # remove Builds/ and the build-*.log files
```

The Unity Editor must not already be open on this project — a running Editor
instance holds the project lock and the build will fail with "another Unity
instance is running with this project open".

`Taskfile.yml` assumes Unity Hub's default install location and the version in
`ProjectSettings/ProjectVersion.txt`; override with e.g.
`task build:mac UNITY_VERSION=6000.3.19f1` if you have a different version
installed.

### Via the raw command line

`Taskfile.yml` just wraps `Assets/Editor/BuildScript.cs`'s `BuildMac()` /
`BuildLinux()` methods, each building all enabled scenes from
`EditorBuildSettings`:

```shell
/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath /path/to/flusi \
  -executeMethod Flusi.EditorTools.BuildScript.BuildMac \
  -logFile build-mac.log

/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath /path/to/flusi \
  -executeMethod Flusi.EditorTools.BuildScript.BuildLinux \
  -logFile build-linux.log
```

Check the log file if the Unity process exits without producing a build —
`BuildScript` exits with a non-zero code in batch mode on build failure.