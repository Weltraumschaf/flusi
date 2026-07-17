# FluSi

This is a simple flight simulator for my six years old son.

## Controls

Keyboard only. Click inside the Game view first, otherwise the keys go to the
Editor instead of the plane.

| Key         | Action                                          |
| ----------- | ----------------------------------------------- |
| Up arrow    | Nose down                                       |
| Down arrow  | Nose up                                         |
| Left arrow  | Bank and turn left                              |
| Right arrow | Bank and turn right                             |
| `W`         | Faster                                          |
| `S`         | Slower                                          |
| `C`         | Switch between cockpit and external orbit view  |
| `A` / `D`   | Rotate the orbit camera (external view only)    |
| `L`         | Toggle the auto-level assist (starts on)        |
| `G`         | Raise / lower the landing gear                  |

**The pitch keys are inverted on purpose, not by mistake.** The up arrow is the
stick pushed forward, which puts the nose down; the down arrow is the stick
pulled back, which brings the nose up. That is how a real aeroplane works and
how the pilot this was built for already flies. Please do not "correct" it.

Turning is coordinated: the left and right arrows roll the plane into the turn
rather than yawing it flat. With the auto-level assist on, releasing the arrows
brings the wings and nose back to level; with it off, the plane holds whatever
bank and pitch you leave it in.

## Technoligy Stack

- Unity with C#.
- Running on macOS and Linux.

### Setup Tools

```shell
brew install --cask unity-hub
brew install --cask dotnet-sdk
```

## Building a Release

Install the **Mac Build Support (IL2CPP)** and **Linux Build Support (IL2CPP)**
modules for your Unity install via Unity Hub (Installs → gear icon → Add
Modules) — cross-compiling the Linux build from macOS works fine, no Linux
machine needed.

### Via the Editor GUI

1. `File > Build Settings`
2. Confirm `Assets/Scenes/SampleScene.unity` is checked under "Scenes In
   Build"
3. Set **Platform** to `macOS`, click `Switch Platform`, then `Build` and
   choose an output folder (e.g. `Builds/macOS/`)
4. Switch **Platform** to `Linux`, `Switch Platform`, then `Build` into a
   separate output folder (e.g. `Builds/Linux/`)

Switching platforms re-imports platform-specific asset variants, so do the two
builds one at a time.

### Via the command line

`Assets/Editor/BuildScript.cs` exposes `BuildMac()` / `BuildLinux()`, each
building all enabled scenes from `EditorBuildSettings` to `Builds/macOS/` or
`Builds/Linux/`:

```shell
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath /path/to/flusi \
  -executeMethod Flusi.EditorTools.BuildScript.BuildMac \
  -logFile build-mac.log

/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath /path/to/flusi \
  -executeMethod Flusi.EditorTools.BuildScript.BuildLinux \
  -logFile build-linux.log
```

Check the log file if the Unity process exits without producing a build —
`BuildScript` exits with a non-zero code in batch mode on build failure.