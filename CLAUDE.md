# Tessera Module

Pure-geometry module for finite 2D grids. No `UnityEngine` dependencies in the runtime assembly.

## Module Parts

```
Runtime/Grid/         ← IGrid + Cell + GridBounds + SquareGrid + HexagonalGrid + TesseraGrid<T>
Runtime/Pathfinding/  ← (planned) A*, line drawing on grids
Runtime/MeshGen/      ← (planned) mesh generation from grid geometry
Tests/                ← NUnit EditMode tests
```

Debug visualization scripts live in `Appegy.Tessera.Environment~/Assets/Scripts/Debug/` (host project, assembly `Appegy.Tessera.Debug`, depends on `UnityEngine`).

## Architecture

`IGrid` is the unified interface (11 methods). It models a finite, immutable 2D grid as a connected planar graph of cells. Cells are identified by dense `int` ids in `[0, CellCount)`. See `Documentation~/grid-api-redesign.md` for full rationale.

Concrete grid types:

| Type | Topology | Distance | Status |
|------|----------|----------|--------|
| `SquareGrid` | 4-connected | Manhattan | shipped |
| `HexagonalGrid` | 6-connected | Cubic | shipped |
| `VoronoiGrid` | edge-connected | BFS | planned |

Supporting types:

- `Cell` — `readonly struct (IGrid grid, int id)`. Thin facade forwarding to `IGrid`. Constructed only by grid implementations (internal ctor).
- `GridBounds` — value-type axis-aligned rectangle returned by `IGrid.Bounds`.
- `TesseraGrid<T>` — per-cell data layer over an `IGrid`. Composition only — does **not** implement `IGrid`. Indexed by `int id` or `Cell`.

Geometry uses `Unity.Mathematics.float2` (package `com.unity.mathematics`).

## Alignment Contract (mandatory for every IGrid implementation)

For any cell `c` with `N = GetCornersCount(c)`:

- There are `N` corners and `N` neighbour slots.
- Edge `i` is the segment from `GetCorner(c, i)` to `GetCorner(c, (i+1) % N)`.
- `GetNeighbor(c, i)` is the neighbour cell across edge `i`, or `-1` if that edge is on the grid boundary.

Mesh-gen, edge-walking, and graph traversal rely on this — keep it.

## Hex Layout (HexagonalGridType)

The enum picks the offset coordinate convention. All four are 6-connected; there are no partial connectivity modes.

| Value | Orientation | Shifted rows / cols |
|-------|-------------|---------------------|
| `PointyOdd` | pointy-top | odd rows shifted right |
| `PointyEven` | pointy-top | odd rows shifted left |
| `FlatOdd` | flat-top | odd cols shifted up |
| `FlatEven` | flat-top | odd cols shifted down |

## Index Ordering (clockwise)

Corners start at the top-right corner. Neighbour order is forced by the alignment contract — neighbour `i` sits across edge `i`.

```
SquareGrid (4-conn):     neighbour 0=right, 1=bottom, 2=left, 3=top
HexagonalGrid pointy:    neighbour 0=R, 1=BR, 2=BL, 3=L, 4=TL, 5=TR
HexagonalGrid flat:      neighbour 0=TR, 1=BR, 2=B, 3=BL, 4=TL, 5=T
```

Corner / neighbour indices wrap via modulo (negative and out-of-range values are valid).

## Cell Geometry

`SquareGrid(width, height, cellSize)`: cell `(x, y)` occupies the axis-aligned rectangle `[x*cellSize, (x+1)*cellSize] x [y*cellSize, (y+1)*cellSize]`. Center at `((x+0.5)*cellSize, (y+0.5)*cellSize)`. `Bounds.Min = (0, 0)`, `Bounds.Max = (width*cellSize, height*cellSize)`.

`HexagonalGrid(width, height, inscribedRadius, type)`: cell `(0, 0)` centre sits at pixel `(0, 0)`. `inscribedRadius` is the apothem (centre-to-edge midpoint); `describedRadius = inscribedRadius / cos(π/6)` is the circumradius (centre-to-corner). `Bounds` is computed by sweeping all corner positions and may have negative `Min` for `PointyEven` / `FlatEven` because their natural row/column staggering puts cells at negative coords.

## How to Add a New IGrid Method

1. Add the method signature to `Runtime/Grid/IGrid.cs`.
2. Implement in every concrete grid class: `SquareGrid`, `HexagonalGrid`, (later) `VoronoiGrid`.
3. Optionally add a forwarding accessor on `Cell` if it makes sense per-cell.
4. Add tests for every grid configuration: `SquareGrid` + 4 hex layouts. Use `[ValueSource(nameof(AllTypes))]` parameterization (see `HexagonalGridTests.cs`).
5. Refresh Unity (`force` mode for new files), run tests.

## Debug Visualization

Scene: `Appegy.Tessera.Environment~/Assets/Scenes/ExampleScene.unity`. Component `TessellationDebugView` on the `Grid Renderer` GameObject.

| Script | Role |
|--------|------|
| `TessellationDebugView` | Root hub: holds settings (kind, size, colours), constructs an `IGrid`, propagates to children. Inspector enum picks Square or one of 4 hex layouts. |
| `TessellationGridRenderer` | Builds quad-based edge mesh from `IGrid` corners (configurable line width). |
| `TessellationCellHighlighter` | Highlights hovered cell + its neighbours. Works in Play (Input System) and Edit (SceneView) modes. Uses `IGrid.GetCellAt` for hit-testing. |

URP is configured for flat 2D — no lighting, no shadows, no post-processing. Colours render as set.

## How to Add a New Module Part

Create a new folder under `Runtime/` (e.g. `Runtime/Pathfinding/`). Keep it in the same `Appegy.Tessera` assembly and namespace. No separate .asmdef needed.
