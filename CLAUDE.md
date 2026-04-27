# Tessera Module

Pure-geometry module for infinite 2D grids. No UnityEngine dependencies.

## Module Parts

```
Runtime/Tessellation/   ← geometry: neighbors, distance, pixel coords, corners
Runtime/Grid/           ← (planned) sized grid with per-cell data storage
Runtime/Pathfinding/    ← (planned) A*, line drawing on grids
Runtime/MeshGen/        ← (planned) mesh generation from grid geometry
Tests/                  ← NUnit EditMode tests
Debug/                  ← Unity debug visualization (scene: TessellationTestScene)
```

## Architecture

`ITessellation` is the unified interface (11 methods). Two readonly struct implementations:

| Type | Neighbors | Distance | Orientation |
|------|-----------|----------|-------------|
| `SquareTessellation` | 4 (cardinal) or 8 (+ diagonals) | Manhattan / Chebyshev | Always same |
| `HexagonalTessellation` | 6 (All), 3 (Even: phys 0,2,4) or 3 (Odd: phys 1,3,5) | Cubic distance | Always same, rows shift by parity |

`Tessellation` is a Union struct that wraps both — zero-allocation polymorphism via `[Union]` + `[Expose(typeof(ITessellation))]`. It is `partial struct` (NOT readonly) because the generator creates mutable fields.

## Hex Neighbor Modes

`HexNeighborMode` enum controls which directions are active:

| Mode | DirectionsCount | Physical directions | Use case |
|------|-----------------|-------------------|----------|
| `All` | 6 | 0,1,2,3,4,5 | Standard hex grid |
| `Even` | 3 | 0,2,4 → remapped to 0,1,2 | Triangle-like gameplay |
| `Odd` | 3 | 1,3,5 → remapped to 0,1,2 | Triangle-like gameplay |

In Even/Odd modes: `AreNeighbors` returns `false` for physically adjacent cells in inactive directions. `CornersCount`, `ToPoint2`, `ToCell`, `GetCornerPoint`, `Distance` are unchanged.

## Direction Ordering (clockwise, starting from top/top-right)

```
Square 4-dir:  0=top, 1=right, 2=bottom, 3=left
Square 8-dir:  0=top, 1=TR, 2=right, 3=BR, 4=bottom, 5=BL, 6=left, 7=TL
Hex pointy:    0=TR, 1=R, 2=BR, 3=BL, 4=L, 5=TL
Hex flat:      0=T, 1=TR, 2=BR, 3=B, 4=BL, 5=TL
```

All direction/corner indices wrap via modulo — negative and out-of-range values are valid.

## Corner Ordering (clockwise)

```
Square:        0=TR, 1=BR, 2=BL, 3=TL
Hex pointy:    corners at -30°, 30°, 90°, 150°, 210°, 270°
Hex flat:      corners at 0°, 60°, 120°, 180°, 240°, 300°
```

## Key Math (inscribedRadius = r)

**Square**: side = 2r. Cell center at `(X * 2r, Y * 2r)`.

**Hexagon**: describedRadius = `r / cos(π/6)` = side. Internally converts Offset↔Cubic for all calculations. Offset neighbor tables are different for even/odd rows (pointy) or columns (flat) — 8 static lookup tables total.

## inscribedRadius Note

`inscribedRadius` only affects pixel-space methods: `ToPoint2`, `ToCell`, `GetCornerPoint`. Neighbor and distance calculations are purely integer and size-independent.

## How to Add a New ITessellation Method

1. Add method signature to `ITessellation.cs`
2. Implement in both structs: `SquareTessellation`, `HexagonalTessellation`
3. Union auto-generates dispatch via `[Expose]` — no changes needed in `Tessellation.cs`
4. Add tests for all grid types/configurations in Tests/
5. Refresh Unity, run tests

## Debug Visualization

Scene `Scenes/TessellationTestScene.unity` with a `TessellationDebugView` component on `Grid Renderer` GameObject.

**3 scripts** in `Debug/` (not in Tessera assembly — they depend on UnityEngine):

| Script | Role |
|--------|------|
| `TessellationDebugView` | Root hub: holds all settings (type, size, colors), creates `Tessellation`, propagates to children. Public setters for programmatic control. |
| `TessellationGridRenderer` | Builds quad-based edge mesh (configurable line width) |
| `TessellationCellHighlighter` | Highlights hovered cell + neighbors. Works in Play Mode (Input System) and Edit Mode (SceneView) |

`TessellationDebugView.TessellationType` enum covers: Square4, Square8, and all 12 hex combinations (4 grid types × 3 neighbor modes).

To test: open `TessellationTestScene`, select `Grid Renderer`, change settings in Inspector. Hover mouse over grid in Scene View to see cell highlighting.

URP is configured for flat 2D — no lighting, no shadows, no post-processing. Colors render exactly as set.

## How to Add a New Module Part

Create a new folder under `Runtime/` (e.g. `Runtime/Grid/`). Keep it in the same `Appegy.Tessera` assembly and namespace. No separate .asmdef needed.
