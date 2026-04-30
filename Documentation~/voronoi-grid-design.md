# Tessera VoronoiGrid — v1 Design

Status: design accepted 2026-04-28, implementation pending.

Builds on `grid-api-redesign.md`. This document covers only the Voronoi-specific decisions; everything in the v2 redesign (`IGrid`, `Bounds2`, `PlaneGrid<T>`, contracts, alignment rule, boundary marker `-1`) applies as-is.

## Working note on flexibility

Same rule as the parent spec: if any item below turns out wrong, awkward, or unnecessary while implementing, deviate and flag the change back in the response. The spec is a starting point, not a contract.

## Goal

Implement the third concrete `IGrid`: an irregular cell grid based on a centroidal Voronoi tessellation clipped to a rectangular `Bounds2`. v1 covers the simplest viable shape; broader scale, alternative algorithms, and pluggable point samplers are explicitly out of scope.

## Locked decisions

- **Scale target**: up to ~500 cells. The algorithm is chosen for simplicity, not asymptotic optimality. Larger grids will require a faster algorithm later.
- **Algorithm**: naive Bowyer-Watson incremental Delaunay → derive Voronoi from triangle circumcenters → Sutherland-Hodgman clip to `Bounds2`. No internal strategy interface; if a faster algorithm appears later it replaces or shadows this one.
- **Sampling**: `System.Random(seed)` uniform inside `bounds`. No `IPointSampler` injection.
- **Relaxation**: Lloyd. `relaxationIterations` rounds. Each round computes a clipped diagram and replaces every seed with the centroid of its clipped polygon. After relaxation, one extra clean Delaunay → Voronoi → clip pass produces the cached topology.
- **Robustness policy**: trust-and-throw. If construction violates an internal invariant (degenerate triangle, broken alignment, asymmetric neighbour graph), the constructor raises `InvalidOperationException` naming the failing invariant and the seed. The caller picks another seed. Auto-jitter / robust predicates are deferred.
- **Determinism**: identical `(bounds, cellCount, seed, relaxationIterations)` produces an identical grid (same id ordering, same geometry, same neighbour graph). `System.Random` is the only randomness source. No wall-clock or environment dependence.
- **Boundary**: cells whose natural Voronoi region escapes `bounds` are clipped. Clipped edges that lie on `bounds` carry `GetNeighbor = -1`. The cell's corner count reflects the post-clip polygon (typically 4-7 for boundary cells, 5-7 for interior cells).
- **Storage**: jagged `float2[][]` for corners and jagged `int[][]` for neighbours, both aligned per cell. Single `float2[]` for centers. CSR is an open internal upgrade if profiling demands it.
- **Spatial query**: `GetCellAt` is a linear nearest-seed scan over `_centers`. For ≤500 cells this is fast enough. Spatial index is a future internal upgrade.
- **Distance**: BFS over the neighbour graph per query. No precomputed pairwise matrix. Caller is responsible for batch-caching if needed.

## Public API addition

```csharp
public sealed class VoronoiGrid : IGrid
{
    public VoronoiGrid(Bounds2 bounds, int cellCount, int seed, int relaxationIterations);

    // full IGrid implementation
}
```

No new public types. `Bounds2`, `PlaneGrid<T>`, `IGrid` are reused unchanged.

Constructor argument validation:
- `cellCount >= 1` else `ArgumentOutOfRangeException`
- `relaxationIterations >= 0` else `ArgumentOutOfRangeException`
- `bounds.Size.x > 0 && bounds.Size.y > 0` else `ArgumentException`

## Construction pipeline

1. **Sample.** `System.Random(seed)` produces `cellCount` `float2` points uniformly inside `bounds`. No coincidence check (probability ≈ 0 for double-precision sampling).
2. **Relax.** For `relaxationIterations` iterations: build Delaunay (Bowyer-Watson), derive Voronoi (circumcenters), clip each cell against `bounds` (Sutherland-Hodgman), replace each seed with the centroid of its clipped polygon.
3. **Final build.** One more clean Delaunay → Voronoi → clip pass on the relaxed seeds. This pass is not a relaxation step; its only role is to produce the cached topology.
4. **Cache populate.** Walk the final diagram, fill `_centers`, `_corners` (CW), `_neighbors` (aligned). Validate invariants; throw on violation.

Time complexity: O(iterations · cellCount²) Bowyer-Watson dominates. For n=500, iters=10, expected wall time well under 100 ms on a developer machine.

## Internal data layout

```csharp
private readonly Bounds2     _bounds;
private readonly float2[]    _centers;       // length = cellCount
private readonly float2[][]  _corners;       // [id][k], CW
private readonly int[][]     _neighbors;     // [id][k], aligned to _corners
```

For each cell `id` with `N = _corners[id].Length`:
- `_neighbors[id].Length == N`
- the edge `_corners[id][k] -> _corners[id][(k+1) % N]` is shared with cell `_neighbors[id][k]` if that value is `>= 0`, otherwise it lies on `bounds`.

## IGrid method strategy

| Method | Strategy |
|---|---|
| `CellCount` | `_centers.Length` |
| `Bounds` | `_bounds` |
| `GetCenter(id)` | `_centers[id]` |
| `GetCornersCount(id)` | `_corners[id].Length` |
| `GetCorner(id, k)` | `_corners[id][k]` |
| `CopyCorners(id, dest)` | copy `_corners[id]` into `dest` |
| `GetNeighbor(id, k)` | `_neighbors[id][k]` |
| `AreNeighbors(a, b)` | scan `_neighbors[a]` for `b` |
| `GetNeighborIndex(c, n)` | scan `_neighbors[c]` for `n`, return index or `-1` |
| `GetCellAt(p)` | if `!_bounds.Contains(p)` return `-1`; else id minimising `distancesq(_centers[id], p)` |
| `Distance(a, b)` | BFS over `_neighbors`, ignoring `-1` slots |

## File layout

```
Runtime/Grids/Voronoi/
  VoronoiGrid.cs               # public, IGrid implementation
  Internal/
    BowyerWatson.cs            # Delaunay builder
    PolygonClipping.cs         # Sutherland-Hodgman against Bounds2
    VoronoiBuilder.cs          # orchestrates pipeline, produces final caches
Tests/
  VoronoiGridTests.cs
```

All internal builders are `internal static` — no public surface beyond `VoronoiGrid`.

## Tests

`Tests/VoronoiGridTests.cs`. Coverage:

1. **Construction validation.** Each invalid-argument case from "Public API addition" throws the documented exception type.
2. **Determinism.** Same `(bounds, cellCount, seed, iters)` → identical centers / corners / neighbours arrays.
3. **Contracts**, parametrised over multiple seeds (e.g. 0..9) and cell counts (e.g. 64, 256):
   - alignment: edge `corner[k] -> corner[(k+1) % N]` is shared with `neighbour[k]` (or that neighbour slot is `-1` and the edge lies on `bounds`).
   - counts match: `GetCornersCount(id) == neighbour count`.
   - symmetry: `AreNeighbors(a, b) == AreNeighbors(b, a)`; `GetNeighborIndex` agrees both ways.
   - distance metric: `Distance(a, a) == 0`, `Distance(a, b) > 0` for `a != b`, symmetric.
   - centre round-trip: `GetCellAt(GetCenter(id)) == id`.
   - corner ordering: stable CW (verified via signed-area sign).
   - all corners lie inside or on `bounds`.
4. **Boundary behaviour.** At least one cell has at least one neighbour slot equal to `-1`.
5. **Lloyd convergence smoke.** With `relaxationIterations >= 5`, variance of cell areas decreases versus `relaxationIterations = 0` for the same seed.
6. **`GetCellAt` outside bounds returns `-1`** for points beyond min/max.

Tests rely on fixed seeds; no randomness mocking.

## Migration plan

Voronoi is additive. Nothing existing changes during implementation.

1. `Internal/BowyerWatson.cs` — Delaunay builder. Unit-test in isolation on small fixture inputs (3-5 points).
2. `Internal/PolygonClipping.cs` — Sutherland-Hodgman vs `Bounds2`. Unit-test on hand-picked polygons.
3. `Internal/VoronoiBuilder.cs` — orchestrates the full pipeline, returns the arrays in the shape `VoronoiGrid` expects.
4. `VoronoiGrid.cs` — ctor calls the builder; IGrid methods are thin accessors.
5. `VoronoiGridTests.cs`.
6. `Documentation~/grid-api-redesign.md` — mark Phase 7 as implemented.
7. `AGENTS.md` and `CLAUDE.md` — list Voronoi paths under runtime layout.

## Out of scope (v1)

- KD-tree / spatial hash for `GetCellAt`.
- CSR storage layout.
- `IPointSampler` for non-uniform seed distributions.
- Robust adaptive-precision predicates (Shewchuk-style).
- Burst / Job-friendly storage layout.
- Auto-jitter retry on construction failure.
- Faster Delaunay implementations (Fortune, sweep-hull) — added later as a swap-in.

## Open implementation choices (decide while coding)

These do not affect the public design:

- Super-triangle size for Bowyer-Watson initialisation.
- Lloyd centroid formula (uniform polygon centroid vs area-weighted) — start with the standard polygon centroid; note in code if anything else is used.
- BFS scratch buffer: alloc per call vs reuse. Start with alloc; revisit only if profiled.
- Whether to expose seeds / final triangulation through a debug accessor — only if `GridDebugView` actually needs it.
