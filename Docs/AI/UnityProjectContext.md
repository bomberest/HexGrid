# HexGrid Unity project context

Inspected on 2026-10-26. Workspace: `/Users/andrewshut/Projects/HexGrid`.

- Unity Editor: **6000.6.0f1**, revision `f7f8ed4d1e24` (`ProjectSettings/ProjectVersion.txt`).
- Starting project: Unity 2D template. Existing first-party assets are `Assets/Scenes/SampleScene.unity`, URP 2D settings and template welcome content. No established game architecture or runtime game code was found before package implementation.
- Rendering: Universal Render Pipeline **17.6.0**, with `Assets/Settings/UniversalRP.asset` and `Renderer2D.asset`. The hex package is independent of rendering.
- Input: Input System **1.20.0**; `activeInputHandler: 1`; actions in `Assets/Settings/InputSystem_Actions.inputactions`. The package has no input dependency.
- Build scene: only `Assets/Scenes/SampleScene.unity` is enabled in `EditorBuildSettings.asset`.
- Tests: Unity Test Framework **1.8.0** is installed. Package tests live in `Anvelop/HexGrid/Tests/Editor` and should be enabled through the host project's `testables` manifest entry.
- Deliverable: local UPM package `com.anvelop.hexgrid` at `Anvelop/HexGrid`, namespace `Anvelop.HexGrid`. Runtime code is value-based hex math plus operations using caller-owned Unity native buffers. No GameObjects, scene wiring, models or meshes are required.
- Package docs: `Anvelop/HexGrid/README.md` and `Documentation~/coverage.md`. Portable Codex skill: `Skills~/anvelop-hexgrid/SKILL.md` inside the package.
- Unity MCP tools were advertised but returned unavailable during initial inspection. Use actual Editor/CLI results as validation evidence; do not infer a successful import or Burst compile from source inspection.
- No `.git` repository or repository-specific `AGENTS.md` was found at initial inspection. Do not assume Git commands, branch state or project conventions exist.

The template's scenes, renderer assets and input settings do not need modification to use the package. Check the current package manifest and test output for exact dependency versions and current validation status.
