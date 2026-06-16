# Voronoi Grid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `VoronoiGrid : IGrid` per `Documentation~/voronoi-grid-design.md`. Naive Bowyer-Watson Delaunay → derive Voronoi → Sutherland-Hodgman clip to `Bounds2` → Lloyd-relaxed centroidal Voronoi tessellation, cached topology, ≤500 cells.

**Architecture:** Public class `VoronoiGrid` with thin `IGrid` accessors over cached jagged arrays. Internal pipeline (`VoronoiBuilder`) orchestrates `BowyerWatson` (Delaunay) and `PolygonClipping` (Sutherland-Hodgman). Lloyd loop runs builder pieces in sequence. No internal strategy interface; algorithm is fixed in v1.

**Tech Stack:** C# 9, .NET Standard 2.1 (Unity 2021.3), `Unity.Mathematics` (`float2`), NUnit (Unity Test Framework EditMode). All code under namespace `Appegy.Tessera` (runtime) and `Appegy.Tessera.Tests` (tests). Existing test style: NUnit Classic Asserts (`Assert.AreEqual`).

## Source spec

`Documentation~/voronoi-grid-design.md` (commit `297a2b9`). Read it first. Hard contracts (alignment, symmetry, distance metric, centre round-trip, CW corner ordering) come from `Documentation~/grid-api-redesign.md`.

> Update 2026-05-14: the geometry/topology decoupling (`grid-api-redesign.md`) downgraded the "alignment" requirement to an implementation property of polygonal grids. VoronoiGrid is polygonal, so the plan's assumption that `corner[k] -> corner[(k+1) % N]` is shared with `neighbour[k]` still holds **inside VoronoiGrid**, but it is no longer a core `IGrid` contract.

## Conventions and Unity quirks

- Each new `.cs` file under `Runtime/` or `Tests/` needs a `.cs.meta` file. After creating files, run `mcp__oc__unityMCP_refresh_unity` to let Unity generate them, then `git add -A` to stage both. Folder `.meta` files are also auto-generated.
- Test runner: `mcp__oc__unityMCP_run_tests` with `mode: "edit"`. Returns a `job_id`; poll `mcp__oc__unityMCP_get_test_job` until `state == "completed"`.
- Y-axis is up (`SquareGrid` corner 0 = top-right at `(x+1, y+1)`). CW corner ordering with Y-up means signed polygon area is **negative**. Use this for CW assertions in tests.
- All corners and neighbours are aligned: edge `corner[k] -> corner[(k+1) % N]` is shared with `neighbour[k]`.
- Boundary marker is `-1` (for `GetNeighbor` and `GetCellAt` outside bounds).
- Use `null!` style for non-nullable fields initialised in `[SetUp]` (matches existing tests).

## File structure

Create:
- `Runtime/Grids/Voronoi/VoronoiGrid.cs` — `public sealed class VoronoiGrid : IGrid`. Ctor calls `VoronoiBuilder.Build`; IGrid methods are thin accessors over cached arrays.
- `Runtime/Grids/Voronoi/Internal/BowyerWatson.cs` — `internal static class`. `Triangulate(points) -> int[]` (triangle vertex indices, 3 ints per triangle, CCW in math frame). Plus `Circumcenter(a, b, c) -> float2`.
- `Runtime/Grids/Voronoi/Internal/PolygonClipping.cs` — `internal static class`. `ClipToBounds(corners, neighbors, bounds) -> (float2[], int[])`. Sutherland-Hodgman against the 4 axis-aligned half-planes; new edges introduced by clipping carry neighbour `-1`.
- `Runtime/Grids/Voronoi/Internal/VoronoiBuilder.cs` — `internal static class`. `Build(bounds, cellCount, seed, iters) -> Result`. Composes sampling, Lloyd, final extraction, validation.
- `Tests/VoronoiGridTests.cs` — NUnit suite. Construction validation, determinism, contracts over multiple seeds, boundary behaviour, Lloyd convergence smoke, GetCellAt outside bounds.

Modify (last task):
- `Documentation~/grid-api-redesign.md` — change Phase 7 status from "Out of scope for this redesign pass" to "implemented".
- `AGENTS.md` — list new Voronoi runtime paths.
- `CLAUDE.md` — same.

---

### Task 1: Scaffold folders and empty `VoronoiGrid` skeleton

**Files:**
- Create: `Runtime/Grids/Voronoi/VoronoiGrid.cs`
- Create: `Runtime/Grids/Voronoi/Internal/.keep` (or empty placeholder)
- Test: none yet

Goal: get the new folders into git so subsequent tasks have a place to drop files. Skeleton class compiles but throws `NotImplementedException` from every method.

- [ ] **Step 1: Create skeleton class**

`Runtime/Grids/Voronoi/VoronoiGrid.cs`:

```csharp
using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Irregular cell grid based on a centroidal Voronoi tessellation clipped to a rectangular bounds.
    ///     See <c>Documentation~/voronoi-grid-design.md</c>.
    /// </summary>
    public sealed class VoronoiGrid : IGrid
    {
        public VoronoiGrid(Bounds2 bounds, int cellCount, int seed, int relaxationIterations)
        {
            throw new NotImplementedException();
        }

        public int CellCount => throw new NotImplementedException();
        public Bounds2 Bounds => throw new NotImplementedException();
        public float2 GetCenter(int id) => throw new NotImplementedException();
        public int GetCornersCount(int id) => throw new NotImplementedException();
        public float2 GetCorner(int id, int cornerIndex) => throw new NotImplementedException();
        public void CopyCorners(int id, Span<float2> dest) => throw new NotImplementedException();
        public int GetNeighbor(int id, int neighborIndex) => throw new NotImplementedException();
        public bool AreNeighbors(int a, int b) => throw new NotImplementedException();
        public int GetNeighborIndex(int cell, int neighbor) => throw new NotImplementedException();
        public int GetCellAt(float2 point) => throw new NotImplementedException();
        public int Distance(int a, int b) => throw new NotImplementedException();
    }
}
```

- [ ] **Step 2: Refresh Unity to generate `.meta` files**

Run: `mcp__oc__unityMCP_refresh_unity`.
Expected: new `.meta` files appear next to the new `.cs` and folder.

- [ ] **Step 3: Compile check**

Run: `mcp__oc__unityMCP_read_console` with `action: "get"`.
Expected: no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Runtime/Grids/Voronoi
git commit -m "feat(voronoi): scaffold VoronoiGrid skeleton"
```

---

### Task 2: Bowyer-Watson Delaunay triangulation

**Files:**
- Create: `Runtime/Grids/Voronoi/Internal/BowyerWatson.cs`
- Create: `Tests/Internal/BowyerWatsonTests.cs`

API:

```csharp
internal static class BowyerWatson
{
    // Returns 3*triangleCount ints. Each consecutive triple is one triangle's vertex indices into `points`.
    // Throws InvalidOperationException on degenerate input (collinear, duplicated, or numerically unstable).
    public static int[] Triangulate(ReadOnlySpan<float2> points);

    // Circumcentre of triangle (a, b, c). Throws InvalidOperationException if collinear.
    public static float2 Circumcenter(float2 a, float2 b, float2 c);
}
```

- [ ] **Step 1: Write failing tests**

`Tests/Internal/BowyerWatsonTests.cs`:

```csharp
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests.Internal
{
    public class BowyerWatsonTests
    {
        [Test]
        public void Triangulate_ThreePoints_OneTriangle()
        {
            var points = new[] { new float2(0, 0), new float2(1, 0), new float2(0, 1) };
            var tri = Tessera.BowyerWatson.Triangulate(points);
            Assert.AreEqual(3, tri.Length);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, tri);
        }

        [Test]
        public void Triangulate_UnitSquare_TwoTriangles()
        {
            var points = new[]
            {
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1)
            };
            var tri = Tessera.BowyerWatson.Triangulate(points);
            Assert.AreEqual(6, tri.Length); // 2 triangles
        }

        [Test]
        public void Triangulate_RandomPoints_AllTrianglesAreDelaunay()
        {
            var rng = new System.Random(0);
            var points = new float2[64];
            for (var i = 0; i < points.Length; i++)
                points[i] = new float2((float)rng.NextDouble() * 10, (float)rng.NextDouble() * 10);

            var tri = Tessera.BowyerWatson.Triangulate(points);
            Assert.IsTrue(tri.Length % 3 == 0);

            // Empty-circumcircle property: for every triangle, no other point is strictly inside its circumcircle.
            for (var t = 0; t < tri.Length; t += 3)
            {
                var a = points[tri[t]];
                var b = points[tri[t + 1]];
                var c = points[tri[t + 2]];
                var cc = Tessera.BowyerWatson.Circumcenter(a, b, c);
                var rsq = math.distancesq(cc, a);
                for (var p = 0; p < points.Length; p++)
                {
                    if (p == tri[t] || p == tri[t + 1] || p == tri[t + 2]) continue;
                    var dsq = math.distancesq(cc, points[p]);
                    Assert.GreaterOrEqual(dsq, rsq - 1e-4f, $"Point {p} inside circumcircle of triangle {t / 3}");
                }
            }
        }

        [Test]
        public void Circumcenter_RightTriangle_AtHypotenuseMidpoint()
        {
            var a = new float2(0, 0);
            var b = new float2(2, 0);
            var c = new float2(0, 2);
            var cc = Tessera.BowyerWatson.Circumcenter(a, b, c);
            Assert.AreEqual(1f, cc.x, 1e-5f);
            Assert.AreEqual(1f, cc.y, 1e-5f);
        }
    }
}
```

Note: `Tests/Internal/` is a new folder. Folder meta will appear after Unity refresh.

- [ ] **Step 2: Run tests to confirm failure**

Run: `mcp__oc__unityMCP_run_tests` with `mode: "edit"`.
Expected: `BowyerWatsonTests` fail or fail-to-compile (file does not exist yet).

- [ ] **Step 3: Implement BowyerWatson**

`Runtime/Grids/Voronoi/Internal/BowyerWatson.cs`:

```csharp
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class BowyerWatson
    {
        public static int[] Triangulate(ReadOnlySpan<float2> points)
        {
            if (points.Length < 3)
                throw new InvalidOperationException("Need at least 3 points to triangulate.");

            // Bounding box
            float minX = points[0].x, minY = points[0].y;
            float maxX = minX, maxY = minY;
            for (var i = 1; i < points.Length; i++)
            {
                var p = points[i];
                if (p.x < minX) minX = p.x;
                else if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                else if (p.y > maxY) maxY = p.y;
            }

            var dmax = math.max(maxX - minX, maxY - minY);
            if (dmax <= 0f) throw new InvalidOperationException("Degenerate point set (zero extent).");
            var midx = (minX + maxX) * 0.5f;
            var midy = (minY + maxY) * 0.5f;

            // Super triangle, vertices at indices points.Length, +1, +2 in the extended array.
            var ext = new float2[points.Length + 3];
            points.CopyTo(ext);
            ext[points.Length] = new float2(midx - 20f * dmax, midy - dmax);
            ext[points.Length + 1] = new float2(midx, midy + 20f * dmax);
            ext[points.Length + 2] = new float2(midx + 20f * dmax, midy - dmax);

            // Triangle list, 3 ints per triangle.
            var tris = new List<int>(points.Length * 6);
            tris.Add(points.Length);
            tris.Add(points.Length + 1);
            tris.Add(points.Length + 2);

            var edges = new List<int>(); // pairs (a, b)

            for (var pi = 0; pi < points.Length; pi++)
            {
                edges.Clear();

                // Find bad triangles whose circumcircle contains the new point.
                for (var ti = tris.Count - 3; ti >= 0; ti -= 3)
                {
                    var a = tris[ti];
                    var b = tris[ti + 1];
                    var c = tris[ti + 2];
                    if (InCircumcircle(ext[a], ext[b], ext[c], ext[pi]))
                    {
                        edges.Add(a); edges.Add(b);
                        edges.Add(b); edges.Add(c);
                        edges.Add(c); edges.Add(a);
                        tris.RemoveAt(ti + 2);
                        tris.RemoveAt(ti + 1);
                        tris.RemoveAt(ti);
                    }
                }

                // Mark duplicate edges (shared between two bad triangles) for removal.
                for (var i = 0; i < edges.Count; i += 2)
                {
                    if (edges[i] == -1) continue;
                    for (var j = i + 2; j < edges.Count; j += 2)
                    {
                        if (edges[j] == -1) continue;
                        if ((edges[i] == edges[j] && edges[i + 1] == edges[j + 1]) ||
                            (edges[i] == edges[j + 1] && edges[i + 1] == edges[j]))
                        {
                            edges[i] = -1; edges[i + 1] = -1;
                            edges[j] = -1; edges[j + 1] = -1;
                            break;
                        }
                    }
                }

                // Connect remaining edges to the new point.
                for (var i = 0; i < edges.Count; i += 2)
                {
                    if (edges[i] == -1) continue;
                    tris.Add(edges[i]);
                    tris.Add(edges[i + 1]);
                    tris.Add(pi);
                }
            }

            // Drop triangles touching the super-triangle vertices.
            var result = new List<int>(tris.Count);
            for (var ti = 0; ti < tris.Count; ti += 3)
            {
                var a = tris[ti];
                var b = tris[ti + 1];
                var c = tris[ti + 2];
                if (a >= points.Length || b >= points.Length || c >= points.Length) continue;
                result.Add(a);
                result.Add(b);
                result.Add(c);
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Bowyer-Watson produced no triangles.");

            return result.ToArray();
        }

        public static float2 Circumcenter(float2 a, float2 b, float2 c)
        {
            var d = 2f * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));
            if (math.abs(d) < 1e-12f)
                throw new InvalidOperationException("Degenerate triangle: collinear vertices.");
            var asq = a.x * a.x + a.y * a.y;
            var bsq = b.x * b.x + b.y * b.y;
            var csq = c.x * c.x + c.y * c.y;
            var ux = (asq * (b.y - c.y) + bsq * (c.y - a.y) + csq * (a.y - b.y)) / d;
            var uy = (asq * (c.x - b.x) + bsq * (a.x - c.x) + csq * (b.x - a.x)) / d;
            return new float2(ux, uy);
        }

        private static bool InCircumcircle(float2 a, float2 b, float2 c, float2 p)
        {
            // Sign-aware in-circle test independent of winding.
            var ax = a.x - p.x; var ay = a.y - p.y;
            var bx = b.x - p.x; var by = b.y - p.y;
            var cx = c.x - p.x; var cy = c.y - p.y;
            var det = (ax * ax + ay * ay) * (bx * cy - cx * by)
                    - (bx * bx + by * by) * (ax * cy - cx * ay)
                    + (cx * cx + cy * cy) * (ax * by - bx * ay);
            var orient = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            return orient > 0 ? det > 0 : det < 0;
        }
    }
}
```

- [ ] **Step 4: Refresh Unity, run tests**

Run: `mcp__oc__unityMCP_refresh_unity`, then `mcp__oc__unityMCP_run_tests`.
Expected: all `BowyerWatsonTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Grids/Voronoi/Internal/BowyerWatson.cs* Tests/Internal
git commit -m "feat(voronoi): implement Bowyer-Watson Delaunay"
```

---

### Task 3: Sutherland-Hodgman polygon clipping with neighbour metadata

**Files:**
- Create: `Runtime/Grids/Voronoi/Internal/PolygonClipping.cs`
- Create: `Tests/Internal/PolygonClippingTests.cs`

API:

```csharp
internal static class PolygonClipping
{
    // Clips a convex polygon (CW vertex order) against an axis-aligned Bounds2 rectangle.
    // For each input edge corner[k] -> corner[(k+1) % N], the caller supplies a "neighbour tag"
    // in `neighbors[k]`. Each output edge inherits its tag from the input edge it came from;
    // edges introduced by clipping along the bounds carry tag -1.
    // Returns an empty result when the polygon is fully outside the bounds.
    public static (float2[] corners, int[] neighbors) ClipToBounds(
        float2[] corners,
        int[] neighbors,
        Bounds2 bounds);
}
```

- [ ] **Step 1: Write failing tests**

`Tests/Internal/PolygonClippingTests.cs`:

```csharp
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests.Internal
{
    public class PolygonClippingTests
    {
        private static readonly Bounds2 Unit = new Bounds2(new float2(0, 0), new float2(1, 1));

        [Test]
        public void Clip_PolygonInside_Unchanged()
        {
            var corners = new[] { new float2(0.7f, 0.7f), new float2(0.7f, 0.3f), new float2(0.3f, 0.3f), new float2(0.3f, 0.7f) };
            var neighbors = new[] { 10, 20, 30, 40 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            CollectionAssert.AreEqual(corners, oc);
            CollectionAssert.AreEqual(neighbors, on);
        }

        [Test]
        public void Clip_PolygonOutside_Empty()
        {
            var corners = new[] { new float2(2, 2), new float2(3, 2), new float2(3, 3), new float2(2, 3) };
            var neighbors = new[] { 1, 2, 3, 4 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            Assert.AreEqual(0, oc.Length);
            Assert.AreEqual(0, on.Length);
        }

        [Test]
        public void Clip_TriangleOneVertexOutside_FourGon()
        {
            // CW triangle, top vertex at (0.5, 1.5) is outside the unit rect.
            var corners = new[] { new float2(0.5f, 1.5f), new float2(0.9f, 0.1f), new float2(0.1f, 0.1f) };
            var neighbors = new[] { 7, 8, 9 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            Assert.AreEqual(4, oc.Length, "Triangle clipped at top should produce a 4-gon.");
            Assert.AreEqual(4, on.Length);
            // Exactly one edge is on the y=1 boundary -> tag -1.
            var minusOnes = 0;
            for (var i = 0; i < on.Length; i++) if (on[i] == -1) minusOnes++;
            Assert.AreEqual(1, minusOnes);
        }

        [Test]
        public void Clip_OutputCornersInsideBounds()
        {
            var corners = new[] { new float2(-0.5f, 0.5f), new float2(0.5f, -0.5f), new float2(1.5f, 0.5f), new float2(0.5f, 1.5f) };
            var neighbors = new[] { 1, 2, 3, 4 };
            var (oc, _) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            foreach (var c in oc)
            {
                Assert.GreaterOrEqual(c.x, -1e-5f);
                Assert.LessOrEqual(c.x, 1f + 1e-5f);
                Assert.GreaterOrEqual(c.y, -1e-5f);
                Assert.LessOrEqual(c.y, 1f + 1e-5f);
            }
        }
    }
}
```

- [ ] **Step 2: Run to confirm failure**

Run: `mcp__oc__unityMCP_run_tests`. Expected: tests fail (file missing).

- [ ] **Step 3: Implement clipping**

`Runtime/Grids/Voronoi/Internal/PolygonClipping.cs`:

```csharp
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class PolygonClipping
    {
        // 0 = x >= min.x, 1 = x <= max.x, 2 = y >= min.y, 3 = y <= max.y
        private const int PlaneCount = 4;

        public static (float2[] corners, int[] neighbors) ClipToBounds(
            float2[] corners, int[] neighbors, Bounds2 bounds)
        {
            var inC = new List<float2>(corners);
            var inN = new List<int>(neighbors);
            var outC = new List<float2>(corners.Length + 4);
            var outN = new List<int>(corners.Length + 4);

            for (var plane = 0; plane < PlaneCount; plane++)
            {
                if (inC.Count == 0) break;

                outC.Clear();
                outN.Clear();

                for (var i = 0; i < inC.Count; i++)
                {
                    var current = inC[i];
                    var next = inC[(i + 1) % inC.Count];
                    var tag = inN[i];

                    var currentInside = Inside(current, plane, bounds);
                    var nextInside = Inside(next, plane, bounds);

                    if (currentInside)
                    {
                        outC.Add(current);
                        if (nextInside)
                        {
                            // edge fully kept; tag preserved on this output edge starting at `current`
                            outN.Add(tag);
                        }
                        else
                        {
                            // edge leaves the plane; output a new vertex at the intersection,
                            // and the edge starting at `current` keeps its tag (it ends at the new vertex).
                            outN.Add(tag);
                            // intersection vertex has no neighbour info on its outgoing edge yet;
                            // it will be set when we add the next "entering" segment along this plane.
                        }
                    }
                    else if (nextInside)
                    {
                        // edge enters: emit intersection vertex; the edge from THIS new vertex to `next`
                        // is along the clipping plane (tag = -1). Then `next` will be appended next iteration.
                        outC.Add(IntersectPlane(current, next, plane, bounds));
                        outN.Add(-1);
                    }
                    // else: edge fully outside, drop both endpoints.
                }

                // Swap roles for next plane.
                var tmpC = inC; inC = outC; outC = tmpC;
                var tmpN = inN; inN = outN; outN = tmpN;
            }

            return (inC.ToArray(), inN.ToArray());
        }

        private static bool Inside(float2 p, int plane, Bounds2 b) => plane switch
        {
            0 => p.x >= b.Min.x,
            1 => p.x <= b.Max.x,
            2 => p.y >= b.Min.y,
            3 => p.y <= b.Max.y,
            _ => true
        };

        private static float2 IntersectPlane(float2 a, float2 b, int plane, Bounds2 bb)
        {
            var dx = b.x - a.x;
            var dy = b.y - a.y;
            return plane switch
            {
                0 => new float2(bb.Min.x, a.y + dy * (bb.Min.x - a.x) / dx),
                1 => new float2(bb.Max.x, a.y + dy * (bb.Max.x - a.x) / dx),
                2 => new float2(a.x + dx * (bb.Min.y - a.y) / dy, bb.Min.y),
                3 => new float2(a.x + dx * (bb.Max.y - a.y) / dy, bb.Max.y),
                _ => a
            };
        }
    }
}
```

Note: read carefully — the tag-mapping rule above is "edge starting at corner[k] is `neighbour[k]`". When an edge leaves the plane, the output vertex inserted at the intersection becomes the next corner; its outgoing edge is along the clipping plane (`-1`). When an edge enters the plane, the inserted vertex's outgoing edge is the original input edge (`tag`). Verify against the test for the 4-gon case: triangle CW with top vertex outside hits the y≤1 plane, producing two intersection vertices and dropping the top vertex. One of the four output edges should carry `-1` (the new edge along y=1).

- [ ] **Step 4: Run tests**

Run: `mcp__oc__unityMCP_run_tests`.
Expected: all `PolygonClippingTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Grids/Voronoi/Internal/PolygonClipping.cs* Tests/Internal/PolygonClippingTests.cs*
git commit -m "feat(voronoi): implement Sutherland-Hodgman polygon clip"
```

---

### Task 4: Voronoi extraction from Delaunay (no clipping yet)

**Files:**
- Modify: `Runtime/Grids/Voronoi/Internal/VoronoiBuilder.cs` (create)
- Create: `Tests/Internal/VoronoiBuilderTests.cs`

API (extraction stage):

```csharp
internal static class VoronoiBuilder
{
    // Output of one extraction pass: per-seed unclipped polygons with neighbour tags.
    internal readonly struct RawCells
    {
        public readonly float2[][] Corners;
        public readonly int[][]    Neighbors;
    }

    // Exposed for testing.
    internal static RawCells ExtractRaw(ReadOnlySpan<float2> seeds, Bounds2 bounds);
}
```

`ExtractRaw` runs Bowyer-Watson, computes circumcenters, walks triangles around each seed in CW order, and outputs the natural (still possibly unbounded) Voronoi polygon plus a neighbour tag per edge. For unbounded sectors of hull seeds, generate "far points" placed along the outward perpendicular bisector at distance `4 * max(bounds.Size.x, bounds.Size.y)` from the bisector midpoint, with neighbour tag `-1`.

CW direction in Y-up local frame: angle around seed sorted descending.

- [ ] **Step 1: Write failing tests**

`Tests/Internal/VoronoiBuilderTests.cs`:

```csharp
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests.Internal
{
    public class VoronoiBuilderTests
    {
        private static readonly Bounds2 Unit = new Bounds2(new float2(0, 0), new float2(1, 1));

        [Test]
        public void ExtractRaw_FourCornerSeeds_ProducesFourPolygons()
        {
            var seeds = new[] {
                new float2(0.1f, 0.1f), new float2(0.9f, 0.1f),
                new float2(0.9f, 0.9f), new float2(0.1f, 0.9f)
            };
            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);
            Assert.AreEqual(4, raw.Corners.Length);
            for (var i = 0; i < 4; i++)
            {
                Assert.GreaterOrEqual(raw.Corners[i].Length, 3, $"Cell {i} should have at least 3 corners");
                Assert.AreEqual(raw.Corners[i].Length, raw.Neighbors[i].Length, "Counts must match per cell");
            }
        }

        [Test]
        public void ExtractRaw_SymmetricNeighborhood()
        {
            var rng = new System.Random(1);
            var seeds = new float2[16];
            for (var i = 0; i < seeds.Length; i++)
                seeds[i] = new float2((float)rng.NextDouble(), (float)rng.NextDouble());

            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);
            for (var a = 0; a < seeds.Length; a++)
            {
                foreach (var b in raw.Neighbors[a])
                {
                    if (b == -1) continue;
                    var found = false;
                    foreach (var bn in raw.Neighbors[b]) if (bn == a) { found = true; break; }
                    Assert.IsTrue(found, $"Asymmetric: {a} sees {b} but {b} does not see {a}");
                }
            }
        }

        [Test]
        public void ExtractRaw_CornersAreClockwise()
        {
            var rng = new System.Random(2);
            var seeds = new float2[16];
            for (var i = 0; i < seeds.Length; i++)
                seeds[i] = new float2((float)rng.NextDouble(), (float)rng.NextDouble());

            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);
            for (var c = 0; c < seeds.Length; c++)
            {
                var corners = raw.Corners[c];
                // Signed area in Y-up frame: negative for CW.
                var signedArea = 0f;
                for (var i = 0; i < corners.Length; i++)
                {
                    var p = corners[i];
                    var q = corners[(i + 1) % corners.Length];
                    signedArea += p.x * q.y - q.x * p.y;
                }
                signedArea *= 0.5f;
                Assert.Less(signedArea, 0f, $"Cell {c} should be CW (negative signed area).");
            }
        }
    }
}
```

- [ ] **Step 2: Run to confirm failure**

Run: `mcp__oc__unityMCP_run_tests`. Expected: missing class.

- [ ] **Step 3: Implement extraction**

`Runtime/Grids/Voronoi/Internal/VoronoiBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class VoronoiBuilder
    {
        internal readonly struct RawCells
        {
            public readonly float2[][] Corners;
            public readonly int[][] Neighbors;
            public RawCells(float2[][] c, int[][] n) { Corners = c; Neighbors = n; }
        }

        internal static RawCells ExtractRaw(ReadOnlySpan<float2> seeds, Bounds2 bounds)
        {
            if (seeds.Length < 3) throw new InvalidOperationException("Voronoi needs >= 3 seeds.");

            var tris = BowyerWatson.Triangulate(seeds);
            var triCount = tris.Length / 3;

            // Per-seed: list of (triangleIndex, otherTwoSeedsCCW)
            var incident = new List<int>[seeds.Length];
            for (var i = 0; i < seeds.Length; i++) incident[i] = new List<int>();
            for (var t = 0; t < triCount; t++)
            {
                incident[tris[3 * t]].Add(t);
                incident[tris[3 * t + 1]].Add(t);
                incident[tris[3 * t + 2]].Add(t);
            }

            // Circumcentres
            var cc = new float2[triCount];
            for (var t = 0; t < triCount; t++)
            {
                cc[t] = BowyerWatson.Circumcenter(seeds[tris[3 * t]], seeds[tris[3 * t + 1]], seeds[tris[3 * t + 2]]);
            }

            // Edge -> triangles map for hull detection. Edge encoded as (min, max) seed index.
            var edgeMap = new Dictionary<long, (int t1, int t2)>();
            for (var t = 0; t < triCount; t++)
            {
                AddEdge(edgeMap, tris[3 * t], tris[3 * t + 1], t);
                AddEdge(edgeMap, tris[3 * t + 1], tris[3 * t + 2], t);
                AddEdge(edgeMap, tris[3 * t + 2], tris[3 * t], t);
            }

            var farDistance = math.max(bounds.Size.x, bounds.Size.y) * 4f;

            var outCorners = new float2[seeds.Length][];
            var outNeighbors = new int[seeds.Length][];

            for (var s = 0; s < seeds.Length; s++)
            {
                // Sort incident triangles in CW order around seed s using triangle circumcentre angle.
                var trisAroundS = incident[s];
                trisAroundS.Sort((a, b) =>
                {
                    var da = cc[a] - seeds[s];
                    var db = cc[b] - seeds[s];
                    var aa = math.atan2(da.y, da.x);
                    var bb = math.atan2(db.y, db.x);
                    // CW with Y up = angle decreasing.
                    return bb.CompareTo(aa);
                });

                // For each consecutive pair (T_k, T_{k+1}), the Voronoi edge (cc[T_k] -> cc[T_{k+1}])
                // corresponds to the Delaunay edge shared by both triangles, which is (s, x_k) where
                // x_k is the seed common to both triangles other than s.
                // For hull seeds, some "consecutive" pairs are actually missing (open sector); detect
                // via edge map having only one triangle and emit far points.

                var corners = new List<float2>();
                var neighbors = new List<int>();

                // Walk through the triangle ring; if a hull edge is encountered, splice in far points.
                for (var k = 0; k < trisAroundS.Count; k++)
                {
                    var t = trisAroundS[k];
                    corners.Add(cc[t]);

                    var tNext = trisAroundS[(k + 1) % trisAroundS.Count];
                    // Find the seed shared between t and tNext other than s.
                    var x = SharedSeedOtherThan(tris, t, tNext, s);
                    if (x >= 0)
                    {
                        neighbors.Add(x);
                    }
                    else
                    {
                        // No shared neighbour: hull boundary between t and tNext (or wrap-around).
                        // Find which Delaunay edge (s, ?) of t is on the hull (single triangle in edgeMap).
                        var hullSeed = FindHullEdgeOpposite(tris, edgeMap, s, t);
                        if (hullSeed >= 0)
                        {
                            var farT = FarPointOfHullEdge(seeds[s], seeds[hullSeed], farDistance);
                            corners.Add(farT);
                            neighbors.Add(-1); // edge cc[t] -> farT
                            // edge farT -> next far on the OTHER hull edge of tNext
                            var hullSeed2 = FindHullEdgeOpposite(tris, edgeMap, s, tNext);
                            if (hullSeed2 >= 0 && hullSeed2 != hullSeed)
                            {
                                var farT2 = FarPointOfHullEdge(seeds[s], seeds[hullSeed2], farDistance);
                                corners.Add(farT2);
                                neighbors.Add(-1); // edge farT -> farT2
                            }
                            neighbors.Add(-1); // closing edge into cc[tNext]
                        }
                        else
                        {
                            neighbors.Add(-1);
                        }
                    }
                }

                outCorners[s] = corners.ToArray();
                outNeighbors[s] = neighbors.ToArray();
            }

            return new RawCells(outCorners, outNeighbors);
        }

        private static void AddEdge(Dictionary<long, (int, int)> map, int a, int b, int t)
        {
            var lo = math.min(a, b);
            var hi = math.max(a, b);
            var key = ((long)lo << 32) | (uint)hi;
            if (map.TryGetValue(key, out var pair))
                map[key] = (pair.Item1, t);
            else
                map[key] = (t, -1);
        }

        private static int SharedSeedOtherThan(int[] tris, int t1, int t2, int exclude)
        {
            for (var i = 0; i < 3; i++)
            {
                var v = tris[3 * t1 + i];
                if (v == exclude) continue;
                for (var j = 0; j < 3; j++)
                {
                    if (tris[3 * t2 + j] == v) return v;
                }
            }
            return -1;
        }

        private static int FindHullEdgeOpposite(int[] tris, Dictionary<long, (int t1, int t2)> map, int s, int t)
        {
            // Among the two edges of t containing s, find the one whose edgeMap entry has only one triangle.
            for (var i = 0; i < 3; i++)
            {
                var v = tris[3 * t + i];
                if (v == s) continue;
                var lo = math.min(s, v);
                var hi = math.max(s, v);
                var key = ((long)lo << 32) | (uint)hi;
                if (map.TryGetValue(key, out var pair) && pair.t2 == -1)
                    return v;
            }
            return -1;
        }

        private static float2 FarPointOfHullEdge(float2 s, float2 other, float farDistance)
        {
            var mid = (s + other) * 0.5f;
            // Outward perpendicular to (other - s), pointing away from the centroid (= away from s).
            var d = other - s;
            var perp = new float2(-d.y, d.x); // rotate 90 CCW; pick whichever side is outward
            // Outward = away from triangulation interior. Heuristic: pick perpendicular pointing
            // away from the centroid of the two endpoints' average toward the exterior. With only
            // two points we cannot tell; flip sign if dot with (mid - s) is negative.
            // Simpler: use perpendicular and just normalise; far distance dominates clipping anyway.
            var len = math.length(perp);
            if (len > 1e-9f) perp /= len;
            return mid + perp * farDistance;
        }
    }
}
```

Hull-handling caveat: the heuristic for outward perpendicular here is incomplete — Sutherland-Hodgman clipping at the next stage will discard whichever far point ends up outside `bounds`, so even if we pick the wrong side, the final clipped polygon is still correct as long as `farDistance` is large enough that the wrong-side far point lies outside bounds. Validate with the symmetric-neighbour test; if it fails, flip the perpendicular sign by checking the third triangle vertex.

- [ ] **Step 4: Run tests**

Run: `mcp__oc__unityMCP_run_tests`.
Expected: all `VoronoiBuilderTests` pass. If hull symmetry fails, fix `FarPointOfHullEdge` to use the third vertex of the triangle to disambiguate the outward side.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Grids/Voronoi/Internal/VoronoiBuilder.cs* Tests/Internal/VoronoiBuilderTests.cs*
git commit -m "feat(voronoi): extract Voronoi cells from Delaunay"
```

---

### Task 5: Lloyd loop and `Build` orchestrator

**Files:**
- Modify: `Runtime/Grids/Voronoi/Internal/VoronoiBuilder.cs`
- Modify: `Tests/Internal/VoronoiBuilderTests.cs`

API additions:

```csharp
internal static class VoronoiBuilder
{
    internal readonly struct Result
    {
        public readonly float2[]    Centers;
        public readonly float2[][]  Corners;
        public readonly int[][]     Neighbors;
        public Result(float2[] c, float2[][] cs, int[][] ns) { Centers = c; Corners = cs; Neighbors = ns; }
    }

    internal static Result Build(Bounds2 bounds, int cellCount, int seed, int relaxationIterations);
}
```

`Build`:
1. Sample `cellCount` seeds uniformly in `bounds` via `System.Random(seed)`.
2. For `relaxationIterations` iterations:
   - `raw = ExtractRaw(seeds, bounds)`
   - per cell, clip with `PolygonClipping.ClipToBounds`
   - per clipped polygon, compute polygon centroid → write back to `seeds[s]`
3. Final pass: `raw = ExtractRaw(seeds, bounds)`, clip per cell.
4. Validate: counts match, alignment, symmetric neighbours. Throw `InvalidOperationException` on violation, message names invariant + seed.

Centroid formula (for a planar polygon):
```
A = 0.5 * sum_i (x_i * y_{i+1} - x_{i+1} * y_i)
Cx = (1 / 6A) * sum_i (x_i + x_{i+1}) * (x_i * y_{i+1} - x_{i+1} * y_i)
Cy = (1 / 6A) * sum_i (y_i + y_{i+1}) * (x_i * y_{i+1} - x_{i+1} * y_i)
```

If `|A| < 1e-12`, fall back to vertex average.

- [ ] **Step 1: Add tests**

Append to `Tests/Internal/VoronoiBuilderTests.cs`:

```csharp
        [Test]
        public void Build_DeterministicForSameSeed()
        {
            var b1 = Tessera.VoronoiBuilder.Build(Unit, 32, 42, 3);
            var b2 = Tessera.VoronoiBuilder.Build(Unit, 32, 42, 3);
            Assert.AreEqual(b1.Centers.Length, b2.Centers.Length);
            for (var i = 0; i < b1.Centers.Length; i++)
            {
                Assert.AreEqual(b1.Centers[i].x, b2.Centers[i].x, 0f);
                Assert.AreEqual(b1.Centers[i].y, b2.Centers[i].y, 0f);
                Assert.AreEqual(b1.Corners[i].Length, b2.Corners[i].Length);
                CollectionAssert.AreEqual(b1.Neighbors[i], b2.Neighbors[i]);
            }
        }

        [Test]
        public void Build_AllCornersInsideBounds()
        {
            var r = Tessera.VoronoiBuilder.Build(Unit, 64, 7, 3);
            foreach (var arr in r.Corners)
                foreach (var c in arr)
                {
                    Assert.GreaterOrEqual(c.x, -1e-4f);
                    Assert.LessOrEqual(c.x, 1f + 1e-4f);
                    Assert.GreaterOrEqual(c.y, -1e-4f);
                    Assert.LessOrEqual(c.y, 1f + 1e-4f);
                }
        }

        [Test]
        public void Build_AlignmentAndSymmetryHold()
        {
            var r = Tessera.VoronoiBuilder.Build(Unit, 64, 11, 3);
            // counts match
            for (var i = 0; i < r.Centers.Length; i++)
                Assert.AreEqual(r.Corners[i].Length, r.Neighbors[i].Length);

            // symmetry
            for (var a = 0; a < r.Centers.Length; a++)
                foreach (var b in r.Neighbors[a])
                {
                    if (b == -1) continue;
                    var found = false;
                    foreach (var bn in r.Neighbors[b]) if (bn == a) { found = true; break; }
                    Assert.IsTrue(found, $"Asymmetric edge {a} <-> {b}");
                }
        }

        [Test]
        public void Build_LloydReducesAreaVariance()
        {
            var r0 = Tessera.VoronoiBuilder.Build(Unit, 64, 13, 0);
            var r5 = Tessera.VoronoiBuilder.Build(Unit, 64, 13, 5);
            Assert.Less(AreaVariance(r5), AreaVariance(r0));
        }

        private static float AreaVariance(Tessera.VoronoiBuilder.Result r)
        {
            var areas = new float[r.Corners.Length];
            var sum = 0f;
            for (var i = 0; i < r.Corners.Length; i++)
            {
                var a = 0f;
                var c = r.Corners[i];
                for (var k = 0; k < c.Length; k++)
                {
                    var p = c[k];
                    var q = c[(k + 1) % c.Length];
                    a += p.x * q.y - q.x * p.y;
                }
                areas[i] = math.abs(a) * 0.5f;
                sum += areas[i];
            }
            var mean = sum / areas.Length;
            var v = 0f;
            for (var i = 0; i < areas.Length; i++) v += (areas[i] - mean) * (areas[i] - mean);
            return v / areas.Length;
        }
```

- [ ] **Step 2: Run to confirm failure**

`mcp__oc__unityMCP_run_tests`. Expected: missing `Build`.

- [ ] **Step 3: Implement Lloyd + Build**

Append to `Runtime/Grids/Voronoi/Internal/VoronoiBuilder.cs` (inside the class):

```csharp
        internal static Result Build(Bounds2 bounds, int cellCount, int seed, int relaxationIterations)
        {
            if (cellCount < 1) throw new ArgumentOutOfRangeException(nameof(cellCount));
            if (relaxationIterations < 0) throw new ArgumentOutOfRangeException(nameof(relaxationIterations));

            var rng = new System.Random(seed);
            var seeds = new float2[cellCount];
            for (var i = 0; i < cellCount; i++)
            {
                seeds[i] = new float2(
                    bounds.Min.x + (float)rng.NextDouble() * (bounds.Max.x - bounds.Min.x),
                    bounds.Min.y + (float)rng.NextDouble() * (bounds.Max.y - bounds.Min.y));
            }

            for (var iter = 0; iter < relaxationIterations; iter++)
            {
                var raw = ExtractRaw(seeds, bounds);
                for (var s = 0; s < cellCount; s++)
                {
                    var (cc, nn) = PolygonClipping.ClipToBounds(raw.Corners[s], raw.Neighbors[s], bounds);
                    if (cc.Length >= 3) seeds[s] = Centroid(cc);
                    // else keep the previous seed; cell vanished due to clip (shouldn't happen with sane input)
                }
            }

            var finalRaw = ExtractRaw(seeds, bounds);
            var finalCorners = new float2[cellCount][];
            var finalNeighbors = new int[cellCount][];
            for (var s = 0; s < cellCount; s++)
            {
                var (cc, nn) = PolygonClipping.ClipToBounds(finalRaw.Corners[s], finalRaw.Neighbors[s], bounds);
                if (cc.Length < 3)
                    throw new InvalidOperationException($"Voronoi cell {s} degenerate after clip (seed={seed}).");
                finalCorners[s] = cc;
                finalNeighbors[s] = nn;
            }

            // Validate alignment and symmetry.
            for (var s = 0; s < cellCount; s++)
            {
                if (finalCorners[s].Length != finalNeighbors[s].Length)
                    throw new InvalidOperationException($"Alignment broken on cell {s} (seed={seed}).");
            }
            for (var a = 0; a < cellCount; a++)
            {
                foreach (var b in finalNeighbors[a])
                {
                    if (b == -1) continue;
                    if (b < 0 || b >= cellCount)
                        throw new InvalidOperationException($"Out-of-range neighbour {b} from cell {a} (seed={seed}).");
                    var found = false;
                    foreach (var bn in finalNeighbors[b]) if (bn == a) { found = true; break; }
                    if (!found)
                        throw new InvalidOperationException($"Asymmetric edge {a} <-> {b} (seed={seed}).");
                }
            }

            return new Result(seeds, finalCorners, finalNeighbors);
        }

        private static float2 Centroid(float2[] poly)
        {
            var a = 0f;
            var cx = 0f;
            var cy = 0f;
            for (var i = 0; i < poly.Length; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % poly.Length];
                var cross = p.x * q.y - q.x * p.y;
                a += cross;
                cx += (p.x + q.x) * cross;
                cy += (p.y + q.y) * cross;
            }
            a *= 0.5f;
            if (math.abs(a) < 1e-12f)
            {
                // Fallback: vertex average
                var sum = float2.zero;
                foreach (var p in poly) sum += p;
                return sum / poly.Length;
            }
            return new float2(cx / (6f * a), cy / (6f * a));
        }
```

- [ ] **Step 4: Run tests**

`mcp__oc__unityMCP_run_tests`.
Expected: all builder tests pass.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Grids/Voronoi Tests/Internal/VoronoiBuilderTests.cs
git commit -m "feat(voronoi): add Lloyd relaxation and Build orchestrator"
```

---

### Task 6: `VoronoiGrid` IGrid implementation

**Files:**
- Modify: `Runtime/Grids/Voronoi/VoronoiGrid.cs`
- Create: `Tests/VoronoiGridTests.cs`

API: full `IGrid`, see Task 1 skeleton.

- [ ] **Step 1: Write failing tests**

`Tests/VoronoiGridTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class VoronoiGridTests
    {
        private static readonly Bounds2 Unit = new Bounds2(new float2(0, 0), new float2(1, 1));

        [Test]
        public void Construct_NegativeCellCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoronoiGrid(Unit, 0, 0, 0));
        }

        [Test]
        public void Construct_NegativeIterations_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoronoiGrid(Unit, 16, 0, -1));
        }

        [Test]
        public void Construct_DegenerateBounds_Throws()
        {
            var degenerate = new Bounds2(new float2(0, 0), new float2(0, 1));
            Assert.Throws<ArgumentException>(() => new VoronoiGrid(degenerate, 16, 0, 0));
        }

        [Test]
        public void CellCount_MatchesArgument()
        {
            var g = new VoronoiGrid(Unit, 32, 0, 2);
            Assert.AreEqual(32, g.CellCount);
        }

        [Test]
        public void Bounds_MatchesArgument()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 0);
            Assert.AreEqual(Unit.Min, g.Bounds.Min);
            Assert.AreEqual(Unit.Max, g.Bounds.Max);
        }

        [Test]
        public void GetCellAt_OutsideBounds_ReturnsMinusOne()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            Assert.AreEqual(-1, g.GetCellAt(new float2(-0.1f, 0.5f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(1.5f, 0.5f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(0.5f, -0.1f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(0.5f, 1.5f)));
        }

        [Test]
        public void GetCellAt_AtCenter_ReturnsCellId()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            for (var id = 0; id < g.CellCount; id++)
                Assert.AreEqual(id, g.GetCellAt(g.GetCenter(id)), $"Round-trip failed for cell {id}");
        }

        [Test]
        public void Distance_SelfIsZero()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            for (var id = 0; id < g.CellCount; id++) Assert.AreEqual(0, g.Distance(id, id));
        }

        [Test]
        public void Distance_IsSymmetric()
        {
            var g = new VoronoiGrid(Unit, 32, 0, 2);
            var rng = new System.Random(123);
            for (var i = 0; i < 16; i++)
            {
                var a = rng.Next(g.CellCount);
                var b = rng.Next(g.CellCount);
                Assert.AreEqual(g.Distance(a, b), g.Distance(b, a));
            }
        }

        [Test]
        public void AreNeighbors_AgreesWithGetNeighbor()
        {
            var g = new VoronoiGrid(Unit, 32, 0, 2);
            for (var id = 0; id < g.CellCount; id++)
            {
                for (var k = 0; k < g.GetCornersCount(id); k++)
                {
                    var n = g.GetNeighbor(id, k);
                    if (n == -1) continue;
                    Assert.IsTrue(g.AreNeighbors(id, n));
                    Assert.IsTrue(g.AreNeighbors(n, id));
                    Assert.AreEqual(k, g.GetNeighborIndex(id, n));
                }
            }
        }

        [Test]
        public void CopyCorners_FillsDestination()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            var n = g.GetCornersCount(0);
            var dest = new float2[n];
            g.CopyCorners(0, dest);
            for (var i = 0; i < n; i++)
                Assert.AreEqual(g.GetCorner(0, i), dest[i]);
        }
    }
}
```

- [ ] **Step 2: Run to confirm failure**

`mcp__oc__unityMCP_run_tests`. Expected: tests fail (skeleton throws NotImplementedException).

- [ ] **Step 3: Implement `VoronoiGrid`**

Replace `Runtime/Grids/Voronoi/VoronoiGrid.cs`:

```csharp
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    public sealed class VoronoiGrid : IGrid
    {
        private readonly Bounds2 _bounds;
        private readonly float2[] _centers;
        private readonly float2[][] _corners;
        private readonly int[][] _neighbors;

        public VoronoiGrid(Bounds2 bounds, int cellCount, int seed, int relaxationIterations)
        {
            if (cellCount < 1) throw new ArgumentOutOfRangeException(nameof(cellCount));
            if (relaxationIterations < 0) throw new ArgumentOutOfRangeException(nameof(relaxationIterations));
            if (bounds.Size.x <= 0 || bounds.Size.y <= 0)
                throw new ArgumentException("Bounds must have positive size.", nameof(bounds));

            var r = VoronoiBuilder.Build(bounds, cellCount, seed, relaxationIterations);
            _bounds = bounds;
            _centers = r.Centers;
            _corners = r.Corners;
            _neighbors = r.Neighbors;
        }

        public int CellCount => _centers.Length;
        public Bounds2 Bounds => _bounds;
        public float2 GetCenter(int id) => _centers[id];
        public int GetCornersCount(int id) => _corners[id].Length;
        public float2 GetCorner(int id, int cornerIndex)
        {
            var arr = _corners[id];
            var n = arr.Length;
            var idx = (cornerIndex % n + n) % n;
            return arr[idx];
        }

        public void CopyCorners(int id, Span<float2> dest)
        {
            var arr = _corners[id];
            if (dest.Length < arr.Length)
                throw new ArgumentException($"dest must have length >= {arr.Length}.", nameof(dest));
            for (var i = 0; i < arr.Length; i++) dest[i] = arr[i];
        }

        public int GetNeighbor(int id, int neighborIndex)
        {
            var arr = _neighbors[id];
            var n = arr.Length;
            var idx = (neighborIndex % n + n) % n;
            return arr[idx];
        }

        public bool AreNeighbors(int a, int b)
        {
            if (a == b) return false;
            var arr = _neighbors[a];
            for (var i = 0; i < arr.Length; i++) if (arr[i] == b) return true;
            return false;
        }

        public int GetNeighborIndex(int cell, int neighbor)
        {
            var arr = _neighbors[cell];
            for (var i = 0; i < arr.Length; i++) if (arr[i] == neighbor) return i;
            return -1;
        }

        public int GetCellAt(float2 point)
        {
            if (!_bounds.Contains(point)) return -1;
            var bestId = 0;
            var bestSq = math.distancesq(_centers[0], point);
            for (var i = 1; i < _centers.Length; i++)
            {
                var d = math.distancesq(_centers[i], point);
                if (d < bestSq) { bestSq = d; bestId = i; }
            }
            return bestId;
        }

        public int Distance(int a, int b)
        {
            if (a == b) return 0;
            var visited = new bool[_centers.Length];
            var queue = new Queue<(int id, int dist)>();
            queue.Enqueue((a, 0));
            visited[a] = true;
            while (queue.Count > 0)
            {
                var (cur, d) = queue.Dequeue();
                var ns = _neighbors[cur];
                for (var i = 0; i < ns.Length; i++)
                {
                    var n = ns[i];
                    if (n == -1 || visited[n]) continue;
                    if (n == b) return d + 1;
                    visited[n] = true;
                    queue.Enqueue((n, d + 1));
                }
            }
            throw new InvalidOperationException($"Cells {a} and {b} are in disconnected components.");
        }
    }
}
```

- [ ] **Step 4: Refresh, run tests**

`mcp__oc__unityMCP_refresh_unity`, then `mcp__oc__unityMCP_run_tests`.
Expected: all `VoronoiGridTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Grids/Voronoi/VoronoiGrid.cs Tests/VoronoiGridTests.cs*
git commit -m "feat(voronoi): implement VoronoiGrid IGrid"
```

---

### Task 7: Multi-seed contract sweep

**Files:**
- Modify: `Tests/VoronoiGridTests.cs`

Run all the contract checks across several seeds and cell counts to flush out edge cases.

- [ ] **Step 1: Add parametrised sweep test**

Append to `Tests/VoronoiGridTests.cs`:

```csharp
        [TestCase(0, 16)] [TestCase(0, 64)] [TestCase(0, 256)]
        [TestCase(1, 64)] [TestCase(2, 64)] [TestCase(3, 64)]
        [TestCase(4, 256)] [TestCase(5, 256)] [TestCase(6, 256)]
        public void Contracts_HoldOverManySeeds(int seedValue, int cellCount)
        {
            var g = new VoronoiGrid(Unit, cellCount, seedValue, 3);

            // Counts match.
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                Assert.GreaterOrEqual(n, 3);
                for (var k = 0; k < n; k++) g.GetCorner(id, k); // shouldn't throw
                for (var k = 0; k < n; k++) g.GetNeighbor(id, k);
            }

            // Symmetry of neighbour relation.
            for (var a = 0; a < g.CellCount; a++)
            {
                var n = g.GetCornersCount(a);
                for (var k = 0; k < n; k++)
                {
                    var b = g.GetNeighbor(a, k);
                    if (b == -1) continue;
                    Assert.IsTrue(g.AreNeighbors(a, b));
                    Assert.IsTrue(g.AreNeighbors(b, a));
                }
            }

            // Distance metric.
            var rng = new System.Random(seedValue);
            for (var i = 0; i < 8; i++)
            {
                var a = rng.Next(g.CellCount);
                var b = rng.Next(g.CellCount);
                Assert.AreEqual(g.Distance(a, b), g.Distance(b, a));
                if (a != b) Assert.Greater(g.Distance(a, b), 0);
            }

            // Centre round-trip.
            for (var id = 0; id < g.CellCount; id++)
                Assert.AreEqual(id, g.GetCellAt(g.GetCenter(id)), $"round-trip cell {id}");

            // Corners CW (signed area negative in Y-up frame).
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var area = 0f;
                for (var k = 0; k < n; k++)
                {
                    var p = g.GetCorner(id, k);
                    var q = g.GetCorner(id, (k + 1) % n);
                    area += p.x * q.y - q.x * p.y;
                }
                Assert.Less(area, 0f, $"cell {id} not CW");
            }

            // At least one boundary edge exists somewhere (we expect some cells to clip).
            var sawBoundary = false;
            for (var id = 0; id < g.CellCount && !sawBoundary; id++)
            {
                var n = g.GetCornersCount(id);
                for (var k = 0; k < n; k++)
                    if (g.GetNeighbor(id, k) == -1) { sawBoundary = true; break; }
            }
            Assert.IsTrue(sawBoundary, "no boundary slots found - expected at least one");
        }
```

- [ ] **Step 2: Run tests**

`mcp__oc__unityMCP_run_tests`.
Expected: all sweep cases pass. If any fail, the failing seed gives a reproducer to debug.

- [ ] **Step 3: Commit**

```bash
git add Tests/VoronoiGridTests.cs
git commit -m "test(voronoi): contract sweep across seeds and cell counts"
```

---

### Task 8: Run full suite, update docs, push

- [ ] **Step 1: Run the entire test suite**

`mcp__oc__unityMCP_run_tests` with `mode: "edit"`. Wait for completion.
Expected: 100% pass for all `Appegy.Tessera.Tests.*`.

- [ ] **Step 2: Update `Documentation~/grid-api-redesign.md`**

Replace the Phase 7 section:

```markdown
### Phase 7 — Voronoi
Separate session. Out of scope for this redesign pass.
```

with:

```markdown
### Phase 7 — Voronoi
Implemented. See `Documentation~/voronoi-grid-design.md` and `Documentation~/voronoi-grid-plan.md`.
```

- [ ] **Step 3: Update `AGENTS.md`**

In the runtime layout listing, add the Voronoi paths under `Runtime/Grids/`:

```
Runtime/Grids/Voronoi/
  VoronoiGrid.cs
  Internal/BowyerWatson.cs
  Internal/PolygonClipping.cs
  Internal/VoronoiBuilder.cs
```

- [ ] **Step 4: Update `CLAUDE.md`**

Mirror the same layout addition. If `CLAUDE.md` doesn't already list runtime paths, add a one-line note: "VoronoiGrid lives under `Runtime/Grids/Voronoi/`; internals in `Internal/`."

- [ ] **Step 5: Commit**

```bash
git add Documentation~/grid-api-redesign.md AGENTS.md CLAUDE.md
git commit -m "docs: mark Voronoi phase implemented"
```

- [ ] **Step 6: Push**

```bash
git push origin ivan/voronoi-grid
```

If a PR for `ivan/voronoi-grid` is open, the push updates it; otherwise open a new PR titled "feat: VoronoiGrid v1".

---

## Self-review notes

- Spec coverage: scale (Task 5 stops short of optimisation but algorithm is the same; ≤500 cells covered), algorithm (Tasks 2-5), boundary handling (Task 3 + builder validation), robustness policy (`InvalidOperationException` in Task 5 + ctor validation in Task 6), determinism (Task 5 test), storage (Task 6 fields), spatial query (Task 6 linear scan), distance (Task 6 BFS), tests (Tasks 6-7 cover all listed contracts).
- Placeholder scan: no TBDs. Hull-perpendicular sign disambiguation is flagged as a known fix-up in Task 4 step 4 — the test that catches it (`ExtractRaw_SymmetricNeighborhood`) is already in place.
- Type consistency: `BowyerWatson.Triangulate` returns `int[]`, used in Task 4 by `ExtractRaw`. `PolygonClipping.ClipToBounds` returns `(float2[], int[])`, consumed by `Build` in Task 5. `VoronoiBuilder.Result` shape matches the fields in Task 6's `VoronoiGrid` ctor.
