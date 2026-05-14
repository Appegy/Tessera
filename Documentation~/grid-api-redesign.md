# Tessera Grid API — v2 Redesign

Status: design accepted 2026-04-27, implementation in progress.

Update 2026-05-14: the original v2 locked **alignment** (corners count == neighbours count, edge `i` between corner `i` and corner `(i+1) % N` shared with neighbour `i`). That coupling blocks non-polygonal cells (jigsaw puzzles, curved-edge tilings, future tessellations whose visual outline is richer than the adjacency graph). The contract is **split**: geometry (corners polyline) and topology (neighbour list) are now independent counts, joined by an explicit bridge accessor. Affected sections: `Locked decisions`, `Public API`, `Contracts`. Existing concrete grids (`SquareGrid` / `HexagonalGrid` / `VoronoiGrid`) preserve their previous corner / neighbour layout — the change is purely additive at the interface level for them.

## Working note on flexibility

This spec is a starting point, not a contract. While implementing, if any item below turns out to be unnecessary, redundant, awkward, or simply wrong, **do not force it through**. Skip it or change it. The only obligation is to **flag the deviation back to the user** in the same response: what was changed, why, and what the new shape is. The user decides whether to accept the change and update the spec.

## Goal

Generalise Tessera from "regular tessellations only (square / hex)" to "any 2D grid whose cells form a connected planar graph". First-class targets: square, hexagonal, Voronoi. The current `ITessellation` API (coordinate-based, infinite, fixed corner count) cannot represent Voronoi, so it is replaced wholesale.

## Locked decisions

- **Finite grids only.** No infinite worlds. Construction declares total cell count.
- **Immutable topology.** Once constructed, structure does not change. Per-cell *data* (`PlaneGrid<T>`) is mutable.
- **Identity = `int` id**, dense `[0, CellCount)`. Coordinates (X/Y, axial, ...) are concrete-grid extras, not part of `IGrid`.
- **`IGrid` is an interface, not a Union.** Each implementation decides whether to cache or compute.
- **No precomputed caches for square / hex.** Their geometry is closed-form; computing on demand is cheap. Voronoi caches because it has no formula.
- **Edge-connected only.** Each grid is a planar graph where one topological edge = one neighbour relation. Square is 4-connected (no diagonals). Hexagon is 6-connected (no Even/Odd partial modes). Voronoi: each cell connects to all cells sharing an edge in the diagram.
- **Geometry and topology counts are independent.** Per cell, `GetCornersCount(id)` (the corner-polyline length, geometry) and `GetNeighborCount(id)` (the adjacency-graph degree, topology) are separate. For polygonal grids they happen to be equal; for non-polygonal cells (e.g. jigsaw pieces whose edges are curves sampled as multi-point polylines) `GetCornersCount > GetNeighborCount`. The core API treats variability of both as the norm.
- **Topology-to-geometry bridge.** A topological edge (one neighbour slot) maps to a *contiguous clockwise range of corners*, not necessarily a single segment. `GetNeighborStartCorner(id, j)` returns the corner index where neighbour `j`'s shared boundary begins; the boundary runs (clockwise, with wrap) up to `GetNeighborStartCorner(id, (j+1) % GetNeighborCount(id))` exclusive. For polygonal grids `GetNeighborStartCorner(id, j) == j`; for puzzle-like cells it skips over the curve-sample corners inside each edge.
- **Variable corner / neighbour count per cell.** Voronoi has 5-7 corners (and the same number of neighbours) depending on the cell; square = 4, hex = 6 fixed. Puzzle cells have 4 neighbours but many more corners. Both counts are queried per id.
- **Neighbour index is ordinal, not directional.** "North", "east", ... are concepts of `SquareGrid` / `HexagonalGrid`, not of the core. Voronoi neighbours are stably ordered but carry no semantic direction.
- **Corner ordering** is stable and clockwise around the cell. The starting corner of `SquareGrid` / `HexagonalGrid` is preserved from the legacy `ITessellation` (top-right). For `VoronoiGrid` the starting corner is implementation-defined; only the clockwise direction is guaranteed.
- **Neighbour ordering** is stable and aligned with the clockwise corner walk: as you sweep corners clockwise, the neighbour on the current edge segment doesn't change until you cross a `GetNeighborStartCorner` boundary. Concrete grids choose where neighbour 0 sits. `SquareGrid` keeps neighbour 0 = right (corner 0 = TR, so edge corner 0 → corner 1 is the right edge). `HexagonalGrid` keeps the analogous start (see `AGENTS.md` for the per-layout table). `VoronoiGrid` starts neighbour 0 at corner 0.
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
    Bounds2 Bounds { get; }

    // Geometry — visual shape of the cell (clockwise corner polyline)
    float2 GetCenter(int id);
    int GetCornersCount(int id);
    float2 GetCorner(int id, int cornerIndex);
    void CopyCorners(int id, Span<float2> dest);
    int GetCellAt(float2 point);                       // -1 if outside grid

    // Topology — adjacency graph
    int GetNeighborCount(int id);
    int GetNeighbor(int id, int neighborIndex);        // -1 = boundary slot
    bool AreNeighbors(int a, int b);
    int GetNeighborIndex(int cell, int neighbor);      // -1 if not a neighbour
    int Distance(int a, int b);                        // graph distance

    // Bridge — corner index where neighbour neighborIndex's shared boundary starts.
    // The shared polyline runs clockwise (with wrap) up to and including
    // GetNeighborStartCorner(id, (neighborIndex + 1) % GetNeighborCount(id)),
    // which is also the start corner of the next edge (joint corners are shared).
    // For polygonal grids this always returns neighborIndex.
    int GetNeighborStartCorner(int id, int neighborIndex);
}

public readonly struct Bounds2
{
    public float2 Min { get; }
    public float2 Max { get; }
    public float2 Size   => Max - Min;
    public float2 Center => (Min + Max) * 0.5f;

    public Bounds2(float2 min, float2 max);
    public bool Contains(float2 p);
}

public sealed class PlaneGrid<T> : IReadOnlyCollection<T>
{
    public IGrid Grid { get; }
    public int   Count => Grid.CellCount;

    public T this[int id]    { get; set; }

    public PlaneGrid(IGrid grid);
    public PlaneGrid(IGrid grid, T fill);

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
    public VoronoiGrid(Bounds2 bounds, int cellCount, int seed, int relaxationIterations);
}
```

`HexagonalGridType` (`PointyOdd` / `PointyEven` / `FlatOdd` / `FlatEven`) carries over from current code (it controls the offset layout, not connectivity). The old `HexNeighborMode` enum is **removed**.

## Contracts (must hold for any IGrid implementation)

1. **Corner ordering.** Stable, clockwise around the cell. Square / hex start from top-right (matches the legacy `ITessellation` corner ordering). For Voronoi the starting corner is implementation-defined.
2. **Edge partition.** With `K = GetNeighborCount(id)` and `M = GetCornersCount(id)`:
   - `GetNeighborStartCorner(id, 0) == 0`.
   - `GetNeighborStartCorner(id, j)` is strictly increasing in `j` over `[0, K)`, and all values lie in `[0, M)`.
   - Let `s = GetNeighborStartCorner(id, j)` and `e = GetNeighborStartCorner(id, (j+1) % K)` (the latter wraps to `0` when `j == K-1`, in which case the range walks corners `M-1 -> 0` to close the polygon). The polyline shared with neighbour `j` is corners `s, s+1, ..., e` (clockwise, modulo `M`), both endpoints included. Joint corners are shared by two consecutive edges. Each edge has at least one segment (`e != s`).
3. **Counts.** `M >= K >= 1`. For grids whose every cell is a strict polygon, `M == K` and `GetNeighborStartCorner(id, j) == j`.
4. **Shared edge coherence.** For neighbours `a, b` with `GetNeighbor(a, ja) == b` and `GetNeighbor(b, jb) == a`, the polyline `a` exposes for `ja` is the reverse of the polyline `b` exposes for `jb`. Endpoints (corners at the range boundaries) must agree up to float tolerance. Grids that need crack-free mesh stitching (e.g. future `PuzzleGrid` whose joint corners are touched by 4 pieces) are expected to make them bit-identical by construction; current polygonal grids satisfy the tolerance form, with `SquareGrid` and `VoronoiGrid` matching exactly and `HexagonalGrid` matching up to trig FP noise.
5. **Symmetric neighbourhood.** `AreNeighbors(a, b) == AreNeighbors(b, a)`. If `GetNeighborIndex(a, b) >= 0`, then `GetNeighborIndex(b, a) >= 0`.
6. **Distance metric.** `Distance(a, a) == 0`. `Distance(a, b) > 0` for `a != b`. `Distance(a, b) == Distance(b, a)`.
7. **Centre round-trip.** `GetCellAt(GetCenter(id)) == id` for any valid `id`. Reverse round-trip is not guaranteed exactly because of float rounding at cell boundaries.

## Migration plan

### Phase 1 — new core types
- `Runtime/IGrid.cs`
- `Runtime/Types/Bounds2.cs`
- `Runtime/Utilities/GridExtensions.cs`

### Phase 2 — square
- `Runtime/Grids/Square/SquareGrid.cs` (closed-form, no caching)
- Migrate `SquareTessellationTests` -> `SquareGridTests`
- Migrate square parts of `GetDirectionTests` -> `SquareGridGetNeighborIndexTests`

### Phase 3 — hex
- `Runtime/Grids/Hexagonal/HexagonalGrid.cs` (closed-form, no caching, 6-connected only)
- Migrate `HexagonalTessellationTests`. `HexNeighborModeTests` is **dropped** (Even/Odd modes are gone).
- Migrate hex parts of `GetDirectionTests`. `GetOppositeDirectionTests` mostly disappears; what remains is `GetNeighborIndex(b, a)` round-trip checks.

### Phase 4 — PlaneGrid<T>
- Rewrite `Runtime/PlaneGrid.cs` per the new shape (id-indexed, no Width/Height of its own).
- Rewrite `PlaneGridTests`.

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
  - `_tess.GetNeighbors(cell)` -> `for (i = 0; i < grid.GetCornersCount(id); i++) { var n = grid.GetNeighbor(id, i); if (n != -1) ... }` (or `grid.Neighbors(id)` extension)
  - bounds-check `cell.X < 0 || cell.X >= Width` -> `id == -1`
- Update `CLAUDE.md` and `AGENTS.md` once the dust settles.

### Phase 7 — Voronoi
Implemented. See `Documentation~/voronoi-grid-design.md` and `Documentation~/voronoi-grid-plan.md`.

## Out of scope (v1)

- Pathfinding / A* — future, will use `IGrid.GetNeighbor` and `Distance` (and possibly per-edge cost via a future extension).
- Mesh-generation utilities packaged inside Tessera — future. The debug renderer is the only consumer right now.
- Chunked / streamed worlds.
- Burst-compiled hot paths. `float2` keeps this option open.
- Pluggable point samplers for Voronoi (`IPointSampler`).
- Per-edge / per-corner data layers analogous to `PlaneGrid<T>`.

## Open implementation choices (decide while coding)

Do not affect the public design:

- Internal CSR vs jagged arrays for Voronoi corner / neighbour storage.
- KD-tree vs uniform spatial hash for Voronoi `GetCellAt`.
- `[MethodImpl(AggressiveInlining)]` on hot accessors of square / hex.
- Internal layout of `HexagonalGrid` (offset vs cubic) — keep as in current code unless something needs changing.