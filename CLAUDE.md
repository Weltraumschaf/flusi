# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

FluSi — a simple flight simulator built as a gift for the author's six-year-old son. Runs on macOS and Linux.

## Tech Stack

- Unity **6000.5.2f1** (Unity 6) with C#, Universal Render Pipeline (URP).
- `Packages/manifest.json` and `packages-lock.json` are NOT tracked by git (a global `packages/` gitignore rule swallows them) — package changes are invisible to `git log`/`git diff`; check the files on disk directly.
- New Input System (`com.unity.inputsystem`). The game's input asset is `Assets/Input/FlightControls.inputactions`, generating the wrapper `Assets/Scripts/Flight/FlightControls.cs` (that path is deliberate — it must compile into the `Flusi` assembly). Never hand-edit the wrapper. `Assets/InputSystem_Actions.inputactions` is an unused stock-template leftover.
- Test Framework (`com.unity.test-framework`) for EditMode/PlayMode tests.

## MCP Tools

The installed Unity MCP server exposes ONLY: `Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`, `Unity_SceneView_Capture2DScene`, `Unity_SceneView_CaptureMultiAngleSceneView`, `Unity_AssetGeneration_*`.

There is no `refresh_unity`, `read_console`, `manage_scene`, `manage_components`, `find_gameobjects`, `manage_prefabs`, `batch_execute`, `validate_script` or `editor_selection` — do not plan around them. Everything else is done by writing C# into `Unity_RunCommand`.

- Do NOT call any refresh tool after editing a script — Unity auto-detects the change. Wait ~30 s; the bridge drops during domain reload. `"Unity not detected"` means a reload is in progress: retry with exponential backoff.
- Before investigating any bug, call `Unity_GetConsoleLogs` (`logTypes: "error"`) first. The logs almost always contain the cause.
- Prefer querying scene state via `Unity_RunCommand` over reading source files to infer it.
- Scene edits need `EditorSceneManager.MarkSceneDirty` + `SaveScene` to reach disk — both throw `InvalidOperationException` in Play mode. Check `EditorApplication.isPlaying` first; it can be left `true` from a prior session. Setting it to `false` is deferred a frame — poll (e.g. `sleep` + recheck) before the next `Unity_RunCommand`, don't assume it's already Edit mode in the same call.
- Private `[SerializeField]` wiring needs `SerializedObject` / `FindProperty` / `ApplyModifiedProperties`. Use the exact C# field name, not the Inspector display name.
- Do scene work with throwaway C# inside `Unity_RunCommand`; do not add scripts to the project to do it.
- Camera capture is unreliable here (64-bit entity id vs the int32 MCP parameter), and screen-space-overlay UI does not appear in scene captures. Verify functionally; ask the owner for visual review.
- Overlay UI CAN be captured despite the above: `EditorApplication.ExecuteMenuItem("Window/General/Game")` to force the Game view open, then `ScreenCapture.CaptureScreenshot(path)` in Play mode — poll the file a couple seconds later. Ignore the MCP camera-capture tools for this; use plain `ScreenCapture`.

## Unity_RunCommand sandbox

- `CommandScript` must implement `IRunCommand` and NOTHING else, and be the only top-level class. An extra interface or a second class fails with `COMPILATION_FAILED` and an EMPTY `compilationLogs` — that is a sandbox rejection, not a syntax error. Empty log => simplify, don't debug.
- Nested classes are hoisted to namespace level by the harness rewriter (a `private` one then fails CS1527).
- `System.Reflection` and `System.Text.RegularExpressions` are unavailable.
- Fully qualify `UnityEngine.UI.Image`; bare `Image` raises CS0118.
- `result.Log` ignores format specifiers — `{0:F2}` prints literally. Pre-format with `StringBuilder.AppendFormat`.
- Never call `FlightControls.Dispose()` in an edit-mode probe (Destroy-in-edit-mode error).
- An `EditorApplication.update`-driven coroutine (to hold input or wait several frames) does not survive the sandbox teardown after `Execute()` returns — it silently never fires again. Do it in one synchronous pass. `ScreenCapture.CaptureScreenshot` is fine as a single fire-and-forget call since Unity's own engine finishes the write, not our delegate.

## Asset generation

- `Unity_AssetGeneration_GenerateAsset` does not overwrite an existing file at `savePath` — it silently appends `" 1"` instead. To regenerate in place: save to a new/temp path, copy the bytes over the target path, then `AssetDatabase.MoveAssetToTrash` the temp asset (plain `File.Delete` on a tracked asset triggers a blocked interactive dialog).
- The copy-dance above is only for fresh generation. `EditImageWithPrompt` with `targetAssetPath` set edits that asset IN PLACE — no temp path needed.
- `"There are interrupted asset generations..."` means a prior session crashed mid-generation; pass `forceGeneration: true` to bypass.
- Generated PNGs have repeatedly come back with NO real alpha channel despite the prompt explicitly requesting a transparent background — the model ignores this often. An opaque light/white/gray background is visually indistinguishable from genuine checkerboard transparency in a casual preview; this went undetected through five separate reviews in one session. Always verify by sampling actual pixel alpha in C# (`Texture2D.LoadImage` + `GetPixel().a`), never by eyeballing the Read-tool preview. Sanity-check any fix by comparing the opaque-pixel fraction against the expected geometry (e.g. a filled circle of radius `r` in a canvas of half-width `R` should be ≈ `π(r/R)²/4` opaque) — a plausible-looking image can still be wrong.

## Tests

- Run via `Flusi.EditorTools.FlusiTestRunner.RunEditMode()` / `RunPlayMode()` inside `Unity_RunCommand`; poll `Temp/flusi-tests.txt` for `STATUS Passed`, detail in `Temp/flusi-tests-failures.txt`. Baseline: **EditMode 41, PlayMode 6**.
- **A green is worthless until proven live.** When compilation fails the test runner does NOT fail — it silently runs the last good assemblies and reports a STALE pass with a plausible count. Always check console errors AND prove the symbol under test is in the loaded assembly. An unexpected count, even a passing one, means read the console.
- The run can also silently STALL — `Temp/flusi-tests.txt` stuck at `RUNNING X` indefinitely — if `EditorApplication.isPlaying` is unexpectedly `true`. Check `isPlaying`, exit Play mode, wait a few seconds, retry; don't keep polling a stuck run.
- Poll with one foreground blocking Bash call in the same turn that kicks off the run: `until grep -q STATUS Temp/flusi-tests.txt; do sleep 2; done; cat Temp/flusi-tests.txt`. A subagent that instead uses `Monitor` or a background wait for the result has its turn end before the result lands.
- Live-assembly proof (reflection is blocked): bind the symbol to a delegate — `System.Action t = rig.ToggleView;` compiles only if it really exists. Pair with `EditorApplication.isCompiling == false`.
- Drive the aircraft from a probe with the runtime Input System, no test framework needed: `InputSystem.QueueStateEvent(kb, new KeyboardState(Key.UpArrow)); InputSystem.Update();`. The synthetic state may not survive to the next frame — queue, pump and read in the same script.
- PlayMode tests that `LoadScene` leave the scene loaded, and `PointOfInterestRegistry` is static: tests depending on an empty registry must clear it in `[SetUp]`, not only `[TearDown]`.

## UI gotchas

Each of these fails silently with an empty console — the worst kind to rediscover.

- Two different built-in stores: fonts via `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`; sprites via `AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")` (circle) or `"UI/Skin/UISprite.psd"` (9-sliced rect). `Resources.GetBuiltinResource<Sprite>` returns NULL.
- `Image.Type.Filled` needs BOTH the type AND a non-null sprite: `OnPopulateMesh` returns early on a null sprite and ignores `type` entirely. Either mistake yields a permanently-full bar and nothing in the console.
- `MonoBehaviour.Awake()` runs regardless of the component's own `enabled` flag — setting `enabled = false` to retire a component does NOT stop its `Awake` logic from running. Guard the body explicitly: `void Awake() { if (enabled) Build(); }`.
- The HUD canvas is `ScaleWithScreenSize` 1920x1080 `match=WIDTH`, so reference HEIGHT varies with aspect (1080@16:9, 1440@4:3). Use fractional anchors for anything that must track the panel.
- Gauge faces are built at `Awake`: bare in the edit-mode scene view, populated only in play mode and builds. Expected, not a bug.
- To inspect HUD layout, dump the RectTransform tree (`anchorMin`/`anchorMax`/`anchoredPosition`/`sizeDelta`) via `Unity_RunCommand`. Containers built with fractional-anchored children (e.g. the six-pack gauges) reflow automatically when the container itself is resized — no need to touch each child.
- A compound instrument (e.g. HeadingIndicator's static bezel + rotating card) can have the same baked-checkerboard-ring defect independently on each layered sprite — fixing the base layer can still leave it visible via a sprite stacked on top. Check every layer.
- `GaugeFaceBuilder`'s tick/label radius fields are absolute pixels, not relative to the parent gauge's `sizeDelta`. Resizing a gauge's RectTransform does not rescale its procedural ticks — they'll misalign against the baked face art unless the builder's fields are retuned too.
- Unity UI renders in sibling order, depth-first: everything under an earlier sibling renders behind everything under a later sibling, regardless of nesting depth. In a dense panel a later gauge's opaque face can silently paint over an earlier gauge's caption wherever the bounding boxes overlap, even with correct anchors.

## Building

- `task build` / `task build:linux` (see `Taskfile.yml`, `README.md`): the Linux build is flaky by a known Unity 6000.5.2f1 bug — switching the active build target from macOS→Linux races the Editor's IL2CPP sysroot/toolchain discovery. `task build:linux` retries automatically (up to 5x); one retry before green is expected, not a regression.

## Git

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
