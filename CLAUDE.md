# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

FluSi — a simple flight simulator built as a gift for the author's six-year-old son. Runs on macOS and Linux.

## Tech Stack

- Unity **6000.5.4f1** (Unity 6) with C#, Universal Render Pipeline (URP).
- `Packages/manifest.json` and `packages-lock.json` are NOT tracked by git (a global `packages/` gitignore rule swallows them) — package changes are invisible to `git log`/`git diff`; check the files on disk directly.
- New Input System (`com.unity.inputsystem`). The game's input asset is `Assets/Input/FlightControls.inputactions`, generating the wrapper `Assets/Scripts/Flight/FlightControls.cs` (that path is deliberate — it must compile into the `Flusi` assembly). Never hand-edit the wrapper. `Assets/InputSystem_Actions.inputactions` is an unused stock-template leftover.
- Test Framework (`com.unity.test-framework`) for EditMode/PlayMode tests.

## MCP Tools

The installed Unity MCP server exposes ONLY: `Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`, `Unity_SceneView_Capture2DScene`, `Unity_SceneView_CaptureMultiAngleSceneView`, `Unity_AssetGeneration_*`.

There is no `refresh_unity`, `read_console`, `manage_scene`, `manage_components`, `find_gameobjects`, `manage_prefabs`, `batch_execute`, `validate_script` or `editor_selection` — do not plan around them. Everything else is done by writing C# into `Unity_RunCommand`.

- The running Unity Editor has exactly one project open at a time — creating a git worktree gives you a *different directory* the Editor isn't open in, so `Unity_RunCommand` keeps operating on the original directory regardless of `cwd`. For work needing live Editor access, skip worktree isolation (branch/work directly in this directory) unless the user physically relaunches the Editor from the worktree path.
- Do NOT call any refresh tool after editing a script — Unity auto-detects the change. Wait ~30 s; the bridge drops during domain reload. `"Unity not detected"` means a reload is in progress: retry with exponential backoff.
- `"Named pipe socket file not found"` (not `"Unity not detected"`) means the Editor process itself isn't running — Unity Hub alone doesn't count. Check with `ps aux | grep "Unity.app/Contents/MacOS/Unity "`; if absent, the user must launch the Editor before any MCP tool call will work, no amount of retrying helps.
- A freshly-opened Editor may have a scene other than `MainScene.unity` loaded (e.g. a blank default scene). Check `SceneManager.GetActiveScene().path` before querying/editing scene state; if wrong, `EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single)`.
- The Editor's main thread can block indefinitely on a native modal (e.g. an unsaved-scene save dialog) triggered by a prior `Unity_RunCommand`. Every subsequent MCP call — even a trivial one — then queues forever with no error. There is no remote way to detect or dismiss this; ask the user to check the Editor window physically.
- Before investigating any bug, call `Unity_GetConsoleLogs` (`logTypes: "error"`) first. The logs almost always contain the cause.
- Prefer querying scene state via `Unity_RunCommand` over reading source files to infer it.
- Scene edits need `EditorSceneManager.MarkSceneDirty` + `SaveScene` to reach disk — both throw `InvalidOperationException` in Play mode. Check `EditorApplication.isPlaying` first; it can be left `true` from a prior session. Setting it to `false` is deferred a frame — poll (e.g. `sleep` + recheck) before the next `Unity_RunCommand`, don't assume it's already Edit mode in the same call.
- Private `[SerializeField]` wiring needs `SerializedObject` / `FindProperty` / `ApplyModifiedProperties`. Use the exact C# field name, not the Inspector display name.
- Do scene work with throwaway C# inside `Unity_RunCommand`; do not add scripts to the project to do it.
- Camera capture is unreliable here (64-bit entity id vs the int32 MCP parameter), and screen-space-overlay UI does not appear in scene captures. Verify functionally; ask the owner for visual review.
- Overlay UI CAN be captured despite the above: `EditorApplication.ExecuteMenuItem("Window/General/Game")` to force the Game view open, then `ScreenCapture.CaptureScreenshot(path)` in Play mode — poll the file a couple seconds later. Ignore the MCP camera-capture tools for this; use plain `ScreenCapture`.
- Both of the above silently fail to produce a file if the Editor's Game view window isn't actually focused/visible on screen (e.g. remote/unattended session) — Unity simply stops advancing frames (`Time.frameCount` stays fixed) so the screenshot never fires. `screencapture` (macOS CLI) fails loudly instead (`could not create image from display`) if the calling process lacks Screen Recording permission, which it typically does in an agent session. When both are blocked, ask the owner to look/screenshot instead of trusting a silent "success".
- The MCP scene-view capture tools (`Capture2DScene`, `CaptureMultiAngleSceneView`) share this staleness failure mode — they can return a byte-identical frame across separate calls with no error. Don't trust any capture tool's output as fresh until content plausibly differs between calls.

## Unity_RunCommand sandbox

- `CommandScript` must implement `IRunCommand` and NOTHING else, and be the only top-level class. An extra interface or a second class fails with `COMPILATION_FAILED` and an EMPTY `compilationLogs` — that is a sandbox rejection, not a syntax error. Empty log => simplify, don't debug.
- Nested classes are hoisted to namespace level by the harness rewriter (a `private` one then fails CS1527).
- `System.Reflection` and `System.Text.RegularExpressions` are unavailable.
- Fully qualify `UnityEngine.UI.Image`; bare `Image` raises CS0118.
- `result.Log` ignores format specifiers — `{0:F2}` prints literally. Pre-format with `StringBuilder.AppendFormat`.
- Never call `FlightControls.Dispose()` in an edit-mode probe (Destroy-in-edit-mode error).
- An `EditorApplication.update`-driven coroutine (to hold input or wait several frames) does not survive the sandbox teardown after `Execute()` returns — it silently never fires again. Do it in one synchronous pass. `ScreenCapture.CaptureScreenshot` is fine as a single fire-and-forget call since Unity's own engine finishes the write, not our delegate.
- `Object.GetInstanceID()` is obsolete in this Editor version (`CS0619`: "use GetEntityId instead") — don't reach for it as a capture-focus workaround; it won't compile in the sandbox, consistent with the existing 64-bit-entity-id/int32-param capture limitation.

## Asset generation

- `Unity_AssetGeneration_GenerateAsset` does not overwrite an existing file at `savePath` — it silently appends `" 1"` instead. To regenerate in place: save to a new/temp path, copy the bytes over the target path, then `AssetDatabase.MoveAssetToTrash` the temp asset (plain `File.Delete` on a tracked asset triggers a blocked interactive dialog).
- The copy-dance above is only for fresh generation. `EditImageWithPrompt` with `targetAssetPath` set edits that asset IN PLACE — no temp path needed.
- `"There are interrupted asset generations..."` means a prior session crashed mid-generation; pass `forceGeneration: true` to bypass.
- `AssetDatabase.CreateAsset` fails ("Parent directory must exist") if the target folder doesn't exist yet — `AssetDatabase.CreateFolder` first for any new `Assets/<Category>/` path.
- `hand-painted-textures-2-0` is a good model choice for this project's stylized/tileable texture needs (terrain layers, water) — matches the "simple/stylized, not photorealistic" look used throughout.
- The asset-generation consent gate needs the real project owner's confirmation — a subagent dispatched via the `Agent` tool can't get that (its conversation is isolated from the owner). In subagent-driven-development, have the controller call `Unity_AssetGeneration_GenerateAsset` directly rather than delegating it to an implementer subagent.
- Generated PNGs have repeatedly come back with NO real alpha channel despite the prompt explicitly requesting a transparent background — the model ignores this often. An opaque light/white/gray background is visually indistinguishable from genuine checkerboard transparency in a casual preview; this went undetected through five separate reviews in one session. Always verify by sampling actual pixel alpha in C# (`Texture2D.LoadImage` + `GetPixel().a`), never by eyeballing the Read-tool preview. Sanity-check any fix by comparing the opaque-pixel fraction against the expected geometry (e.g. a filled circle of radius `r` in a canvas of half-width `R` should be ≈ `π(r/R)²/4` opaque) — a plausible-looking image can still be wrong.

## Tests

- Run via `Flusi.EditorTools.FlusiTestRunner.RunEditMode()` / `RunPlayMode()` inside `Unity_RunCommand`; poll `Temp/flusi-tests.txt` for `STATUS Passed`, detail in `Temp/flusi-tests-failures.txt`. Baseline: **EditMode 46, PlayMode 6** (grep `Assets/Tests/EditMode/*.cs` for `[Test]`/`[UnityTest]` to confirm current count if in doubt — it has drifted before).
- **A green is worthless until proven live.** When compilation fails the test runner does NOT fail — it silently runs the last good assemblies and reports a STALE pass with a plausible count. Always check console errors AND prove the symbol under test is in the loaded assembly. An unexpected count, even a passing one, means read the console.
- The run can also silently STALL — `Temp/flusi-tests.txt` stuck at `RUNNING X` indefinitely — if `EditorApplication.isPlaying` is unexpectedly `true`. Check `isPlaying`, exit Play mode, wait a few seconds, retry; don't keep polling a stuck run.
- Poll with one foreground blocking Bash call in the same turn that kicks off the run: `until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt`. A subagent that instead uses `Monitor` or a background wait for the result has its turn end before the result lands.
- Live-assembly proof (reflection is blocked): bind the symbol to a delegate — `System.Action t = rig.ToggleView;` compiles only if it really exists. Pair with `EditorApplication.isCompiling == false`.
- Drive the aircraft from a probe with the runtime Input System, no test framework needed: `InputSystem.QueueStateEvent(kb, new KeyboardState(Key.UpArrow)); InputSystem.Update();`. The synthetic state may not survive to the next frame — queue, pump and read in the same script.
- PlayMode tests that `LoadScene` leave the scene loaded, and `PointOfInterestRegistry` is static: tests depending on an empty registry must clear it in `[SetUp]`, not only `[TearDown]`.
- Never call `RunEditMode()` and `RunPlayMode()` in the same `Unity_RunCommand` script — firing them back-to-back corrupts the test runner's internal state (`InvalidOperationException: This cannot be used during play mode`, `Too many instant steps in test execution mode`) and leaves the status file stuck `RUNNING` forever, which looks like the documented stall but isn't fixed by the `isPlaying` check. Call them in **separate** `Unity_RunCommand` invocations, polling each to `STATUS` before starting the next.

## Terrain sculpting

- `TerrainData.SetHeights`/`GetHeights`/`SetAlphamaps`/`GetAlphamaps` all index arrays `[z, x]` (row-major, z first), not `[x, z]`.
- `GetInterpolatedHeight`/`GetSteepness` take NORMALIZED `[0,1]` terrain-fraction coordinates; `SampleHeight(worldPos)` and `GetHeight(int, int)` don't. Don't mix conventions across scripts touching the same terrain.
- There is no `GetInterpolatedAlphamapValue` API (`CS1061`) — read a texel's layer weights via `GetAlphamaps(x, y, 1, 1)` at heightmap-index coordinates, not normalized ones.
- Slope is ~0 exactly AT a smooth radial-falloff peak (it's a local maximum, zero gradient) — the steep terrain is on the flanks a few hundred meters out, not the summit. When tuning slope-based rock/texture blending, sample a radius/angle sweep around a peak, not just the peak's own coordinate, or you'll wrongly conclude the blend rule found no steep ground.

## UI gotchas

Each of these fails silently with an empty console — the worst kind to rediscover.

- A Screen-Space-Camera canvas is confined to its bound camera's own `rect`; a
  letterboxed world camera squeezes the WHOLE canvas into that letterbox, not
  just the 3D view. Don't stack a second Base UI camera to work around it either
  — cross-camera "don't clear" compositing is undefined on Metal, the same
  fragility class as the stencil-`Mask` bug below. If the panel is opaque
  anyway, just make the world camera full-screen instead.
- Siblings positioned by anchor **fraction** (e.g. the six-pack's 1/6, 1/2, 5/6
  columns) reflow across resolutions; a sibling positioned by a fixed pixel
  `anchoredPosition` instead (e.g. a caption meant to sit under one) does NOT,
  and drifts off at any window size other than the one it was tuned against.
  Read the target's actual `anchorMin`/`anchorMax` and copy it — don't hardcode
  a rounded literal (`0.17` when the real value is `1/6 = 0.16666667`).
- Two different built-in stores: fonts via `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`; sprites via `AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")` (circle) or `"UI/Skin/UISprite.psd"` (9-sliced rect). `Resources.GetBuiltinResource<Sprite>` returns NULL.
- `Image.Type.Filled` needs BOTH the type AND a non-null sprite: `OnPopulateMesh` returns early on a null sprite and ignores `type` entirely. Either mistake yields a permanently-full bar and nothing in the console.
- `MonoBehaviour.Awake()` runs regardless of the component's own `enabled` flag — setting `enabled = false` to retire a component does NOT stop its `Awake` logic from running. Guard the body explicitly: `void Awake() { if (enabled) Build(); }`.
- The HUD canvas is `ScaleWithScreenSize` 1920x1080 `match=WIDTH`, so reference HEIGHT varies with aspect (1080@16:9, 1440@4:3). Use fractional anchors for anything that must track the panel.
- Gauge faces are built at `Awake`: bare in the edit-mode scene view, populated only in play mode and builds. Expected, not a bug.
- To inspect HUD layout, dump the RectTransform tree (`anchorMin`/`anchorMax`/`anchoredPosition`/`sizeDelta`) via `Unity_RunCommand`. Containers built with fractional-anchored children (e.g. the six-pack gauges) reflow automatically when the container itself is resized — no need to touch each child.
- A compound instrument (e.g. HeadingIndicator's static bezel + rotating card) can have the same baked-checkerboard-ring defect independently on each layered sprite — fixing the base layer can still leave it visible via a sprite stacked on top. Check every layer.
- `GaugeFaceBuilder`'s tick/label radius fields are absolute pixels, not relative to the parent gauge's `sizeDelta`. Resizing a gauge's RectTransform does not rescale its procedural ticks — they'll misalign against the baked face art unless the builder's fields are retuned too.
- Unity UI renders in sibling order, depth-first: everything under an earlier sibling renders behind everything under a later sibling, regardless of nesting depth. In a dense panel a later gauge's opaque face can silently paint over an earlier gauge's caption wherever the bounding boxes overlap, even with correct anchors.
- A stencil-based UGUI `Mask` can silently no-op (clips nothing, full unclipped content shows) specifically in a **standalone macOS/Metal build** while working fine in the Editor and on Linux — the Editor's Game view always has a stencil-capable render target, a Screen-Space-Overlay canvas's raw window backbuffer on Metal apparently doesn't. Fix: put the canvas in Screen Space - Camera mode instead of Overlay (routes through the camera's render target, sidesteps the gap). If multiple cameras are toggled on/off (e.g. cockpit/orbit view, only one `Camera.enabled` at a time), the canvas's `worldCamera` must be repointed on every toggle or the whole canvas goes blank whenever the assigned camera is the disabled one.

## Building

- Build output lands at `Builds/macOS/flusi.app` / `Builds/Linux/flusi` (see
  `BuildScript.cs`), not `Builds/mac/`.
- A build-in-place does not update the `.app` bundle directory's own mtime;
  check `Builds/macOS/flusi.app/Contents/MacOS/flusi`'s mtime instead to
  confirm a rebuild actually happened.
- `task build` / `task build:linux` (see `Taskfile.yml`, `README.md`): the Linux build is flaky by a known Unity 6000.5.4f1 bug — switching the active build target from macOS→Linux races the Editor's IL2CPP sysroot/toolchain discovery. `task build:linux` retries automatically (up to 5x); one retry before green is expected, not a regression.
- Batchmode `task build:*` refuses to run ("another Unity instance is running with this project open") whenever the Editor is already open — Unity won't let two processes share one project. With the Editor open, build in-place instead: call `Flusi.EditorTools.BuildScript.BuildMac()` (or `BuildLinux`) via `Unity_RunCommand`. That call reports MCP-level `"failed"` on ordinary shader compile warnings (there are always some, from URP/Sentis packages) even though the build succeeded — don't trust the tool's pass/fail, check `Builds/<platform>/` for the produced app/binary instead.

## Git

- `ProjectSettings/UnityConnectSettings.asset` (`m_Enabled`) gets toggled by
  ordinary Editor/test-runner activity, unrelated to any real edit — check
  `git diff` before staging and leave it out if it's just this.
- Commit messages: use the `write-commit-messages` skill — <=50-char imperative subject, body wrapped at 72, explaining what and why. No trailers, matching the existing history.
- A new asset's `.meta` must land in the SAME commit as the asset, or a checkout of that commit has an asset with no GUID.
- Interactive rebase is blocked in this environment: use `git commit --fixup=<sha>` + `GIT_SEQUENCE_EDITOR=true git rebase --autosquash <base>`.
- `git commit --amend --no-edit` is wrong when the amend changes what the commit does — the stale message then contradicts the code.
- `git commit` hanging ~2 min with no output usually means `commit.gpgsign=true` is waiting on a pinentry prompt the tool can't see. Check `git config --get commit.gpgsign`; don't add `--no-gpg-sign` — ask the user to unlock via `! git commit` or explicitly authorize a bypass.

## Efficiency

- Do not read the entire codebase before acting. If the user asks for a specific change, make that change.
- Initially, only read scripts that are directly relevant to the task.

## Error Handling

- If a script edit causes compilation errors, fix them immediately. Do not stack further changes on a broken compilation state.

## General

- Target platform is macOS and Linux standalone unless otherwise stated.
- Prefer clear, readable C# over clever abstractions.
- Do not add packages or dependencies without asking first.

## Architecture Notes

- Game code is in `Assets/Scripts/`: `Flight/` (`FlightModel`, `AircraftController`, `IAircraftState`, `AircraftStateRef`), `Cockpit/` (the instrument panel), `Cameras/`, `World/` (POI registry, minimap). Tests in `Assets/Tests/{EditMode,PlayMode}/`, the test-runner harness in `Assets/Editor/`. Scenes in `Assets/Scenes/` (`MainScene.unity` is the entry scene), render/quality config in `Assets/Settings/`.
- Namespace is flat `Flusi` (`Flusi.Tests` for tests); folders are organisational only.
- The pattern throughout: pure static maths (`GaugeScale`, `AltimeterScale`, `FlightDerivations`, `HudFormat`, `MinimapProjection`) behind thin MonoBehaviours, so the logic is EditMode-testable. Follow it.
- Instruments and the minimap read the aircraft ONLY through the read-only `IAircraftState` seam, never flight internals. Guard per-frame reads with `AircraftStateRef.IsAlive` — a destroyed aircraft does not compare equal to null through an interface-typed field.
- `FlightModel.cs` / `FlightConfig.cs` encode feel tuned against a real six-year-old. Do not change them incidentally; presentation work goes through the `IAircraftState` seam.
- The spec is `docs/Specification.md` (NOT the repo root), amended by the design docs in `docs/superpowers/specs/`.
- `Assets/TutorialInfo/` is a leftover Unity sample `Readme` viewer and can be deleted.
- Every asset has a paired `.meta` file holding its GUID; references between assets are by GUID. Always let Unity move/rename/delete assets (or `git mv` the `.meta` alongside) so references don't break, and commit `.meta` files.
- `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/`, and build output are gitignored regenerable caches — never edit or rely on them.
