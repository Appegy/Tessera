# Tessera Grid API — v2 Redesign

Status: design accepted 2026-04-27, implementation in progress.

## Working note on flexibility

This spec is a starting point, not a contract. While implementing, if any item below turns out to be unnecessary, redundant, awkward, or simply wrong, **do not force it through**. Skip it or change it. The only obligation is to **flag the deviation back to the user** in the same response: what was changed, why, and what the new shape is. The user decides whether to accept the change and update the spec.

## Goal

Generalise Tessera from "regular tessellations only (square / hex)" to "any 2D grid whose cells form a connected planar graph". First-class targets: square, hexagonal, Voronoi. The current `ITessellation` API (coordinate-based, infinite, fixed corner count) cannot represent Voronoi, so it is replaced wholesale.

## Locked decisions

- **Finite grids only.** No infinite worlds. Construction declares total cell count.
- **Immutable topology.** Once constructed, structure does not change. Per-cell *data* (`TesseraGrid<T>`) is mutable.
- **Identity = `int` id**, dense `[0, CellCount)`. Coordinates (X/Y, axial, ...) are concrete-grid extras, not part of `IGrid`.
- **`IGrid` is an interface, not a Union.** Each implementation decides whether to cache or compute.
- **No precomputed caches for square / hex.** Their geometry is closed-form; computing on demand is cheap. Voronoi caches because it has no formula.
- **`Cell` is a thin `readonly struct`** holding `(IGrid grid, int id)`. All accessors forward to the grid. Virtual dispatch per call is acceptable at this scale.
- **Edge-connected only.** Each grid is a planar graph where one polygon edge = one neighbour relation. Square is 4-connected (no diagonals). Hexagon is 6-connected (no Even/Odd partial modes). Voronoi: each cell connects to all cells sharing an edge in the diagram.
- **Variable corner / neighbour count per cell.** Voronoi has 5-7 corners depending on the cell; square = 4, hex = 6 fixed. The core API treats variability as the norm.
- **Corners count == neighbours count.** Each polygon edge has exactly one neighbour slot. A boundary edge stores `-1` in that slot; the corners are still present.
- **Alignment contract.** For any cell `c` with `N = GetCornersCount(c)`, the edge from `GetCorner(c, i)` to `GetCorner(c, (i+1) % N)` is shared with `GetNeighbor(c, i)`. Implementations MUST satisfy this.
- **Neighbour index is ordinal, not directional.** "North", "east", ... are concepts of `SquareGrid` / `HexagonalGrid`, not of the core. Voronoi neighbours are stably ordered but carry no semantic direction.
- **Corner / neighbour ordering** is stable and clockwise. The starting corner of `SquareGrid` / `HexagonalGrid` is preserved from current `ITessellation` (top-right). Neighbour indices are *forced by the alignment contract*: edge 0 is `corner[0] -> corner[1]`, so neighbour 0 sits on that edge. For square 4-dir this means `neighbour 0 = right neighbour` (not "top" as in the old `ITessellation`). For hex it shifts accordingly. For `VoronoiGrid` the starting corner is implementation-defined; only the clockwise direction is guaranteed.
- **Boundary marker = `-1`** for `GetNeighbor` (no neighbour on that side) and `GetCellAt` (point outside grid).
- **Geometry type = `Unity.Mathematics.float2`** (`com.unity.mathematics` 1.3.2, already added to `package.json` and `Appegy.Tessera.asmdef`).
- **`Distance` is a graph metric.** Square / hex use closed form. Voronoi falls back to BFS — O(N) per query. No precomputed pairwise matrix. Caller is responsible for batch-caching if needed.
- **`Bounds`** is computed in the constructor for square / hex (from `width x height x cellSize`). For Voronoi it is an *input* (the clip rectangle).
- **No external positioning.** A grid lives in its own local coordinate system anchored at `(0, 0)`. The consumer translates it into world space.
- **Old code goes.** `ITessellation`, `Tessellation` Union, `SquareTessellation`, `HexagonalTessellation`, their tests, and the debug scripts all migrate or are deleted. The package is not yet consumed anywhere; no API to preserve.
- **`com.appegy.union` dependency** is dropped once `Tessellation` Union is removed.

## Public API

```csharp
public interface IGrid
{
    int CellCount { get; }
    bounds2 Bounds { get; }

    Cell GetCell(int id);

    // Geometry
    float2 GetCenter(int id);
    int GetCornersCount(int id);
    float2 GetCorner(int id, int cornerIndex);
    void CopyCorners(int id, Span<float2> dest);

    // Neighbourhood
    int GetNeighbor(int id, int neighborIndex);       // -1 = boundary
    bool AreNeighbors(int a, int b);
    int GetNeighborIndex(int cell, int neighbor);     // -1 if not a neighbour

    // Spatial / metric
    int GetCellAt(float2 point);                       // -1 if outside grid
    int Distance(int a, int b);                        // graph distance
}

public readonly struct Cell
{
    private readonly IGrid _grid;
    public int Id { get; }

    internal Cell(IGrid grid, int id) { _grid = grid; Id = id; }

    public float2 Center                       => _grid.GetCenter(Id);
    public int    CornersCount                 => _grid.GetCornersCount(Id);
    public float2 GetCorner(int i)             => _grid.GetCorner(Id, i);
    public int    GetNeighbor(int i)           => _grid.GetNeighbor(Id, i);
    public int    GetNeighborIndex(int other)  => _grid.GetNeighborIndex(Id, other);
    public int    DistanceTo(int other)        => _grid.Distance(Id, other);
    public void   CopyCorners(Span<float2> d)  => _grid.CopyCorners(Id, d);
}

public readonly struct bounds2
{
    public float2 Min { get; }
    public float2 Max { get; }
    public float2 Size   => Max - Min;
    public float2 Center => (Min + Max) * 0.5f;

    public bounds2(float2 min, float2 max);
    public bool Contains(float2 p);
}

public sealed class TesseraGrid<T> : IReadOnlyCollection<T>
{
    public IGrid Grid { get; }
    public int   Count => Grid.CellCount;

    public T this[int id]    { get; set; }
    public T this[Cell cell] { get; set; }   // forwards to this[cell.Id]

    public TesseraGrid(IGrid grid);
    public TesseraGrid(IGrid grid, T fill);
    public TesseraGrid(IGrid grid, T[] data); // length must equal grid.CellCount

    public void Fill(T value);
    public IEnumerator<T> GetEnumerator();
}
```

Common helpers live as extension methods on `IGrid` in `GridExtensions.cs`. The first one is:

```csharp
public static IEnumerable<int> Neighbors(this IGrid grid, int id);  // skips -1 slots
```

More are added when concrete consumers demand them — not speculatively.

## Concrete grid types

```csharp
public sealed class SquareGrid : IGrid
{
    public SquareGrid(int width, int height, float cellSize);

    public int   Width    { get; }
    public int   Height   { get; }
    public float CellSize { get; }

    public int       IdOf(int x, int y);
    public (int X, int Y) XYOf(int id);
}

public sealed class HexagonalGrid : IGrid
{
    public HexagonalGrid(int width, int height, float inscribedRadius, HexagonalGridType type);

    public int               Width           { get; }
    public int               Height          { get; }
    public float             InscribedRadius { get; }
    public HexagonalGridType Type            { get; }

    public int       IdOf(int x, int y);
    public (int X, int Y) XYOf(int id);
}

public sealed class VoronoiGrid : IGrid
{
    // v1: uniform random sampling of seed points + Lloyd relaxation. No IPointSampler injection.
    public VoronoiGrid(bounds2 bounds, int cellCount, int seed, int relaxationIterations);
}
```

`HexagonalGridType` (`PointyOdd` / `PointyEven` / `FlatOdd` / `FlatEven`) carries over from current code (it controls the offset layout, not connectivity). The old `HexNeighborMode` enum is **removed**.

## Contracts (must hold for any IGrid implementation)

1. **Alignment.** Edge `GetCorner(c, i) -> GetCorner(c, (i+1) % N)` is shared with `GetNeighbor(c, i)`. Always.
2. **Counts match.** `GetCornersCount(c)` = number of edges = number of neighbour slots.
3. **Symmetric neighbourhood.** `AreNeighbors(a, b) == AreNeighbors(b, a)`. If `GetNeighborIndex(a, b) >= 0`, then `GetNeighborIndex(b, a) >= 0`.
4. **Distance metric.** `Distance(a, a) == 0`. `Distance(a, b) > 0` for `a != b`. `Distance(a, b) == Distance(b, a)`.
5. **Centre round-trip.** `GetCellAt(GetCenter(id)) == id` for any valid `id`. Reverse round-trip is not guaranteed exactly because of float rounding at cell boundaries.
6. **Corner ordering.** Stable, clockwise. Square / hex start from top-right (matches current `ITessellation` corner ordering). Neighbour ordering follows the alignment contract: neighbour 0 sits on edge 0 (corner 0 -> corner 1), so for square 4-dir `neighbour 0 = right`. For Voronoi the starting corner is implementation-defined.

## Migration plan

### Phase 1 — new core types
- `Runtime/Grid/IGrid.cs`
- `Runtime/Grid/Cell.cs`
- `Runtime/Grid/bounds2.cs`
- `Runtime/Grid/GridExtensions.cs`

### Phase 2 — square
- `Runtime/Grid/SquareGrid.cs` (closed-form, no caching)
- Migrate `SquareTessellationTests` -> `SquareGridTests`
- Migrate square parts of `GetDirectionTests` -> `SquareGridGetNeighborIndexTests`

### Phase 3 — hex
- `Runtime/Grid/HexagonalGrid.cs` (closed-form, no caching, 6-connected only)
- Migrate `HexagonalTessellationTests`. `HexNeighborModeTests` is **dropped** (Even/Odd modes are gone).
- Migrate hex parts of `GetDirectionTests`. `GetOppositeDirectionTests` mostly disappears; what remains is `GetNeighborIndex(b, a)` round-trip checks.

### Phase 4 — TesseraGrid<T>
- Rewrite `Runtime/Grid/TesseraGrid.cs` per the new shape (id-indexed, no Width/Height of its own).
- Rewrite `TesseraGridTests`.

### Phase 5 — delete old
- Remove `Runtime/Tessellation/*` entirely.
- Remove `TessellationUnionTests`.
- Drop `com.appegy.union` from `package.json` and `Appegy.Tessera.asmdef`.

### Phase 6 — debug scripts
- Update `TessellationDebugView` -> `GridDebugView`. The inspector enum still picks a grid kind; constructor produces an `IGrid`.
- Update `TessellationGridRenderer` and `TessellationCellHighlighter` to walk `IGrid`. Per the design discussion the migration is mechanical:
  - `_tess.ToCell(x, y)` -> `grid.GetCellAt(point)`
  - `_tess.ToPoint2(cell)` -> `grid.GetCenter(id)`
  - `_tess.GetCornerPoint(cell, i)` -> `grid.GetCorner(id, i)`
  - `_tess.GetNeighbors(cell)` -> `for (i = 0; i < cell.CornersCount; i++) { var n = cell.GetNeighbor(i); if (n != -1) ... }` (or `grid.Neighbors(id)` extension)
  - bounds-check `cell.X < 0 || cell.X >= Width` -> `id == -1`
- Update `CLAUDE.md` and `AGENTS.md` once the dust settles.

### Phase 7 — Voronoi
Separate session. Out of scope for this redesign pass.

## Out of scope (v1)

- Pathfinding / A* — future, will use `IGrid.GetNeighbor` and `Distance` (and possibly per-edge cost via a future extension).
- Mesh-generation utilities packaged inside Tessera — future. The debug renderer is the only consumer right now.
- Chunked / streamed worlds.
- Burst-compiled hot paths. `float2` keeps this option open.
- Pluggable point samplers for Voronoi (`IPointSampler`).
- Per-edge / per-corner data layers analogous to `TesseraGrid<T>`.

## Open implementation choices (decide while coding)

Do not affect the public design:

- Internal CSR vs jagged arrays for Voronoi corner / neighbour storage.
- KD-tree vs uniform spatial hash for Voronoi `GetCellAt`.
- `[MethodImpl(AggressiveInlining)]` on hot accessors of square / hex.
- Internal layout of `HexagonalGrid` (offset vs cubic) — keep as in current code unless something needs changing.
