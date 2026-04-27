# Tessera — Agent Notes

`com.appegy.tessera` is a Unity Package Manager (UPM) package; the package root *is* the repo root (`package.json` here, not in a subfolder).

Architecture, public API, alignment contract, and the "how to add a new IGrid method" recipe are documented in `CLAUDE.md`. The full design rationale is in `Documentation~/grid-api-redesign.md`. Read both before changing grid code. The notes below are things those documents do not cover.

## Repo layout — what is what

- `Runtime/Grid/` — package code, assembly `Appegy.Tessera`. `noEngineReferences: true` (no `UnityEngine`) and `-nullable:enable` (`Runtime/csc.rsp`). Do not add `UnityEngine` references here. References `Unity.Mathematics` for `float2`.
- `Tests/` — EditMode NUnit tests, assembly `Appegy.Tessera.Tests`. `includePlatforms: ["Editor"]`, `optionalUnityReferences: ["TestAssemblies"]`, `noEngineReferences: true`. References `Appegy.Tessera` and `Unity.Mathematics`.
- `Appegy.Tessera.Environment~/` — Unity 2021.3 host project used to open the package in the editor and run tests. The trailing `~` is required: Unity ignores the folder when the package is consumed downstream. Do not rename. Generated `.csproj` files and `Library/`, `Logs/`, `Temp/`, `obj/` live here and are disposable.
- `Appegy.Tessera.Environment~/Assets/Scripts/Debug/` — debug visualization scripts (`TessellationDebugView`, `TessellationGridRenderer`, `TessellationCellHighlighter`) in assembly `Appegy.Tessera.Debug`. They depend on `UnityEngine`, which is why they live in the host project, not in the package.
- The debug scene is `Appegy.Tessera.Environment~/Assets/Scenes/ExampleScene.unity`.
- `Documentation~/grid-api-redesign.md` — design spec / record of decisions. The trailing `~` keeps it out of downstream UPM consumers' Asset import.

## Build / test / run

There is no CLI build, no CI workflow, no formatter script. Unity is the build system.

- Open the project at `Appegy.Tessera.Environment~/` in Unity 2021.3+.
- Run tests via `Window → General → Test Runner → EditMode`, or in this environment via the `unityMCP_run_tests` tool (the `com.coplaydev.unity-mcp` package is installed; see `Packages/manifest.json`).
- After editing C# under `Runtime/` or `Tests/`, refresh the editor.
  - **For new `.cs` files use force mode** (`unityMCP_refresh_unity` with `mode: "force"`). The default `if_dirty` does not pick up newly created files — they will silently fail to compile into the assembly and the test runner will report 0 of those tests ran.
  - For edits to existing files, normal refresh is enough.

## Style (from `.editorconfig`, non-default bits)

- 4-space indent, LF, `insert_final_newline=false` (no trailing newline at EOF — do not add one when editing).
- Private instance fields: `_camelCase`. Private `static readonly`: also `_camelCase`. Private `const`: `PascalCase`. Local functions: `camelCase`.
- `var` is preferred everywhere (built-in types, apparent types, elsewhere).

## Agent / IDE config

`.gitignore` excludes `.claude/`, `.codex/`, `.agents/`, and `opencode.json`. Agent-side config is per-developer; do not commit it. `CLAUDE.md` and `AGENTS.md` are checked in and authoritative for shared instructions — keep them in sync when changing architecture.

## Test conventions

Tests live as flat `*.cs` files under `Tests/` (no subfolders). When adding behaviour:

- For hex-related changes, parameterise the test over all four `HexagonalGridType` values (`PointyOdd`, `PointyEven`, `FlatOdd`, `FlatEven`). See `HexagonalGridTests.cs` for the `[ValueSource(nameof(AllTypes))]` pattern.
- For square, single-grid tests are usually enough — `SquareGrid` is 4-connected only at the `IGrid` level (no diagonal mode).
- Use `float2` for geometry assertions (matches the public API). Tolerance `1e-4f` to `1e-5f` is fine for hex math.
- The alignment contract is a behaviour worth testing for new grid types: `2 * edge_midpoint - cell_center == neighbour_center` for each neighbour. See `Alignment_NeighbourSitsAcrossCorrespondingEdge` in `SquareGridTests.cs` / `HexagonalGridTests.cs`.
