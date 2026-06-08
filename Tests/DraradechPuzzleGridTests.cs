using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class DraradechPuzzleGridTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void Construct_NegativeWidth_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DraradechPuzzleGrid(0, 4, 1f, 0));
        }

        [Test]
        public void Construct_NegativeHeight_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DraradechPuzzleGrid(4, 0, 1f, 0));
        }

        [Test]
        public void Construct_NegativeCellSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DraradechPuzzleGrid(4, 4, 0f, 0));
        }

        [Test]
        public void Construct_StoresParameters()
        {
            var g = new DraradechPuzzleGrid(4, 3, 2f, 7);
            Assert.AreEqual(4, g.Width);
            Assert.AreEqual(3, g.Height);
            Assert.AreEqual(2f, g.CellSize);
            Assert.AreEqual(7, g.Seed);
            Assert.AreEqual(12, g.CellCount);
            Assert.AreEqual(0.5f, g.Parameters.TabSize);
            Assert.AreEqual(0.5f, g.Parameters.Variation);
            Assert.AreEqual(0.5f, g.Parameters.Smoothness);
            // Default Smoothness=0.5 -> 10 subdivisions -> 31 samples per edge.
            Assert.AreEqual(31, g.SamplesPerEdge);
        }

        [Test]
        public void Bounds_AnchoredAtOriginExtendsToWidthHeight()
        {
            var g = new DraradechPuzzleGrid(4, 3, 2f, 0);
            Assert.AreEqual(new float2(0, 0), g.Bounds.Min);
            Assert.AreEqual(new float2(8, 6), g.Bounds.Max);
        }

        [Test]
        public void GetCenter_IsHalfCellOffset()
        {
            var g = new DraradechPuzzleGrid(4, 3, 2f, 0);
            Assert.AreEqual(new float2(1f, 1f), g.GetCenter(g.IdOf(0, 0)));
            Assert.AreEqual(new float2(3f, 3f), g.GetCenter(g.IdOf(1, 1)));
            Assert.AreEqual(new float2(7f, 5f), g.GetCenter(g.IdOf(3, 2)));
        }

        [Test]
        public void GetNeighborCount_IsFour()
        {
            var g = new DraradechPuzzleGrid(4, 3, 1f, 0);
            for (var id = 0; id < g.CellCount; id++) Assert.AreEqual(4, g.GetNeighborCount(id));
        }

        [Test]
        public void GetNeighbor_OnBoundary_ReturnsMinusOne()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            // (0, 0): left and bottom are boundaries.
            var id = g.IdOf(0, 0);
            Assert.AreEqual(-1, g.GetNeighbor(id, 1)); // bottom
            Assert.AreEqual(-1, g.GetNeighbor(id, 2)); // left
            Assert.AreNotEqual(-1, g.GetNeighbor(id, 0)); // right
            Assert.AreNotEqual(-1, g.GetNeighbor(id, 3)); // top
        }

        [Test]
        public void GetNeighbor_WrapsIndex()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            Assert.AreEqual(g.GetNeighbor(id, 0), g.GetNeighbor(id, 4));
            Assert.AreEqual(g.GetNeighbor(id, 0), g.GetNeighbor(id, -4));
        }

        [Test]
        public void AreNeighbors_AgreesWithGetNeighbor()
        {
            var g = new DraradechPuzzleGrid(4, 4, 1f, 0);
            for (var id = 0; id < g.CellCount; id++)
            {
                for (var k = 0; k < 4; k++)
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
        public void AreNeighbors_SelfReturnsFalse()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            for (var id = 0; id < g.CellCount; id++) Assert.IsFalse(g.AreNeighbors(id, id));
        }

        [Test]
        public void AreNeighbors_OutOfRangeReturnsFalse()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            Assert.IsFalse(g.AreNeighbors(-1, 0));
            Assert.IsFalse(g.AreNeighbors(0, g.CellCount));
            Assert.IsFalse(g.AreNeighbors(g.CellCount, 0));
        }

        [Test]
        public void GetNeighborIndex_OutOfRangeReturnsMinusOne()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            Assert.AreEqual(-1, g.GetNeighborIndex(-1, 0));
            Assert.AreEqual(-1, g.GetNeighborIndex(g.CellCount, 0));
            Assert.AreEqual(-1, g.GetNeighborIndex(0, 0));
        }

        [Test]
        public void Distance_IsManhattan()
        {
            var g = new DraradechPuzzleGrid(4, 4, 1f, 0);
            Assert.AreEqual(0, g.Distance(g.IdOf(1, 1), g.IdOf(1, 1)));
            Assert.AreEqual(1, g.Distance(g.IdOf(1, 1), g.IdOf(2, 1)));
            Assert.AreEqual(2, g.Distance(g.IdOf(1, 1), g.IdOf(2, 2)));
            Assert.AreEqual(6, g.Distance(g.IdOf(0, 0), g.IdOf(3, 3)));
        }

        [Test]
        public void GetCornersCount_OneByOne_IsFour()
        {
            // No interior edges, all 4 sides straight.
            var g = new DraradechPuzzleGrid(1, 1, 1f, 0);
            Assert.AreEqual(4, g.GetCornersCount(0));
        }

        [Test]
        public void GetCornersCount_InteriorCellHasFourFullSides()
        {
            // 3x3 grid: cell (1, 1) has all 4 sides interior.
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var n = g.Parameters.SamplesPerEdge - 2;
            Assert.AreEqual(4 + 4 * n, g.GetCornersCount(g.IdOf(1, 1)));
        }

        [Test]
        public void GetCornersCount_BoundaryCells_HaveFewerCorners()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var n = g.Parameters.SamplesPerEdge - 2;
            // (0, 0): 2 interior sides (right, top), 2 boundary.
            Assert.AreEqual(4 + 2 * n, g.GetCornersCount(g.IdOf(0, 0)));
            // (1, 0): 3 interior sides (right, left, top), 1 boundary (bottom).
            Assert.AreEqual(4 + 3 * n, g.GetCornersCount(g.IdOf(1, 0)));
        }

        [Test]
        public void GetCorner_AtRectCornersMatchesRectLayout()
        {
            // 1x1 grid (no puzzle edges) collapses to plain square corners.
            var g = new DraradechPuzzleGrid(1, 1, 2f, 0);
            Assert.AreEqual(new float2(2, 2), g.GetCorner(0, 0)); // TR
            Assert.AreEqual(new float2(2, 0), g.GetCorner(0, 1)); // BR
            Assert.AreEqual(new float2(0, 0), g.GetCorner(0, 2)); // BL
            Assert.AreEqual(new float2(0, 2), g.GetCorner(0, 3)); // TL
        }

        [Test]
        public void GetCorner_WrapsIndex()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            Assert.AreEqual(g.GetCorner(id, 0), g.GetCorner(id, n));
            Assert.AreEqual(g.GetCorner(id, 0), g.GetCorner(id, -n));
            Assert.AreEqual(g.GetCorner(id, 1), g.GetCorner(id, n + 1));
        }

        [Test]
        public void CopyCorners_FillsDestination()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            var dest = new float2[n];
            g.CopyCorners(id, dest);
            for (var i = 0; i < n; i++) Assert.AreEqual(g.GetCorner(id, i), dest[i]);
        }

        [Test]
        public void CopyCorners_ThrowsIfDestinationTooSmall()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            var dest = new float2[n - 1];
            Assert.Throws<ArgumentException>(() => g.CopyCorners(id, dest));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(-7)]
        public void Corners_AreClockwise(int seed)
        {
            // CW polyline in Y-up frame has negative signed area.
            var g = new DraradechPuzzleGrid(4, 4, 1f, seed);
            AssertAllCellsCw(g, seed);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(123)]
        public void SharedEdge_NeighboursAgreeOnPolylineExactly(int seed)
        {
            // Cell A side s and cell B's matching side must be identical polylines in
            // reverse order. By construction (both look up the same _vertEdges /
            // _horizEdges slot), they share the same underlying float2[].
            var g = new DraradechPuzzleGrid(4, 4, 1f, seed);
            AssertSharedEdgesAgree(g, seed);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(42)]
        public void Polygon_HasNoSelfIntersection_Defaults(int seed)
        {
            // For each cell, every pair of non-adjacent polygon edges must NOT cross.
            // Adjacent edges share an endpoint and are excluded.
            var g = new DraradechPuzzleGrid(4, 4, 1f, seed);
            AssertNoCellSelfIntersection(g, seed.ToString());
        }

        // Exhaustive sweep across the public parameter cube. Verifies the polygon is
        // simple for any (TabSize, Variation, Smoothness) the user can dial in. Six
        // evenly-spaced samples per axis (including 0 and 1) gives 216 combos; tied
        // to a single seed to keep run time bounded.
        [Test]
        public void Polygon_HasNoSelfIntersection_FullParameterSweep()
        {
            const int steps = 5;
            for (var i = 0; i <= steps; i++)
            for (var j = 0; j <= steps; j++)
            for (var k = 0; k <= steps; k++)
            {
                var t = (float)i / steps;
                var v = (float)j / steps;
                var s = (float)k / steps;
                var p = new DraradechPuzzleParameters(t, v, s);
                var g = new DraradechPuzzleGrid(3, 3, 1f, 0, p);
                var label = $"T={t:F2} V={v:F2} S={s:F2}";
                AssertNoCellSelfIntersection(g, label);
                AssertAllCellsCw(g, 0);
            }
        }

        // Every corner of the public parameter cube (eight combinations of
        // {0, 1} per axis) across multiple seeds. The strictest boundary
        // coverage we can do without resorting to randomization.
        [TestCase(0f, 0f, 0f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, 1f, 1f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(1f, 1f, 0f)]
        [TestCase(1f, 1f, 1f)]
        public void Polygon_HasNoSelfIntersection_AllCubeCorners(float tabSize, float variation, float smoothness)
        {
            for (var seed = 0; seed < 8; seed++)
            {
                var p = new DraradechPuzzleParameters(tabSize, variation, smoothness);
                var g = new DraradechPuzzleGrid(3, 3, 1f, seed, p);
                AssertNoCellSelfIntersection(g, $"T={tabSize} V={variation} S={smoothness} seed={seed}");
                AssertSharedEdgesAgree(g, seed);
                AssertAllCellsCw(g, seed);
            }
        }

        // Randomized fuzz over the full (T, V, S, seed) input space. Deterministic
        // via a fixed PRNG seed so failures are reproducible. Catches anything the
        // 216-point sweep might miss between grid samples.
        [Test]
        public void Polygon_HasNoSelfIntersection_RandomizedFuzz()
        {
            var rng = new System.Random(20260518);
            for (var trial = 0; trial < 200; trial++)
            {
                var t = (float)rng.NextDouble();
                var v = (float)rng.NextDouble();
                var s = (float)rng.NextDouble();
                var seed = rng.Next();
                var p = new DraradechPuzzleParameters(t, v, s);
                var g = new DraradechPuzzleGrid(3, 3, 1f, seed, p);
                AssertNoCellSelfIntersection(g, $"trial={trial} T={t:F3} V={v:F3} S={s:F3} seed={seed}");
            }
        }

        // Out-of-range and NaN inputs get silently clamped to [0, 1] in
        // DraradechPuzzleParameters, so the resulting grid must still produce a
        // simple polygon. Lock this behavior in.
        [Test]
        public void Polygon_HasNoSelfIntersection_OutOfRangeAndNanInputs()
        {
            var combos = new (float t, float v, float s)[]
            {
                (float.NaN, float.NaN, float.NaN),
                (-1f, -1f, -1f),
                (2f, 2f, 2f),
                (float.NegativeInfinity, float.PositiveInfinity, float.NaN),
                (-1000f, 1000f, 0.5f),
                (float.NaN, 0.3f, 0.7f),
                (1.5f, -0.5f, float.NaN),
            };
            foreach (var (t, v, s) in combos)
            {
                var p = new DraradechPuzzleParameters(t, v, s);
                var g = new DraradechPuzzleGrid(3, 3, 1f, 42, p);
                AssertNoCellSelfIntersection(g, $"T={t} V={v} S={s}");
                AssertPolygonsTileRectangle(g, $"T={t} V={v} S={s}");
            }
        }

        // Sanity: even at extreme inputs the polygon coordinates are always
        // finite numbers (no NaN / Infinity leaking through).
        [TestCase(0f, 0f, 0f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, 1f, 1f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(1f, 1f, 0f)]
        [TestCase(1f, 1f, 1f)]
        public void Corners_AreAlwaysFinite(float tabSize, float variation, float smoothness)
        {
            var p = new DraradechPuzzleParameters(tabSize, variation, smoothness);
            var g = new DraradechPuzzleGrid(4, 4, 1f, 42, p);
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                for (var k = 0; k < n; k++)
                {
                    var c = g.GetCorner(id, k);
                    Assert.IsTrue(math.isfinite(c.x), $"T={tabSize} V={variation} S={smoothness} cell={id} corner={k} non-finite x: {c.x}");
                    Assert.IsTrue(math.isfinite(c.y), $"T={tabSize} V={variation} S={smoothness} cell={id} corner={k} non-finite y: {c.y}");
                }
            }
        }

        // Tiling invariant under random parameters: total polygon area must
        // exactly equal grid rectangle area regardless of inputs.
        [Test]
        public void Polygon_TilesRectangle_RandomizedFuzz()
        {
            var rng = new System.Random(20260519);
            for (var trial = 0; trial < 50; trial++)
            {
                var t = (float)rng.NextDouble();
                var v = (float)rng.NextDouble();
                var s = (float)rng.NextDouble();
                var seed = rng.Next();
                var p = new DraradechPuzzleParameters(t, v, s);
                var g = new DraradechPuzzleGrid(3, 3, 1f, seed, p);
                AssertPolygonsTileRectangle(g, $"trial={trial} T={t:F3} V={v:F3} S={s:F3} seed={seed}");
            }
        }

        // Pieces must continue to tile the rectangle exactly even at extreme
        // parameters. Total absolute signed area of all polygons must equal grid area.
        [TestCase(0f, 0f, 0f)]
        [TestCase(0.25f, 0.25f, 0.25f)]
        [TestCase(0.5f, 0.5f, 0.5f)]
        [TestCase(0.75f, 0.75f, 0.75f)]
        [TestCase(1f, 1f, 1f)]
        public void Polygon_TilesRectangle_ParameterExtremes(float tabSize, float variation, float smoothness)
        {
            var p = new DraradechPuzzleParameters(tabSize, variation, smoothness);
            var g = new DraradechPuzzleGrid(4, 3, 1f, 5, p);
            AssertPolygonsTileRectangle(g, $"T={tabSize} V={variation} S={smoothness}");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        public void GetCellAt_OutsideBounds_ReturnsMinusOne(int seed)
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, seed);
            Assert.AreEqual(-1, g.GetCellAt(new float2(-0.5f, 0.5f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(0.5f, -0.5f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(3.5f, 1f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(1f, 3.5f)));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(13)]
        public void GetCellAt_AtCenter_ReturnsCellId(int seed)
        {
            var g = new DraradechPuzzleGrid(4, 4, 1f, seed);
            for (var id = 0; id < g.CellCount; id++)
            {
                Assert.AreEqual(id, g.GetCellAt(g.GetCenter(id)), $"seed={seed} round-trip failed for cell {id}");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void GetCellAt_TilesTheRectangle(int seed)
        {
            // Sample a dense grid of points across the bounds. Every point must map
            // to a valid cell id (puzzle pieces partition the rect with no gaps).
            var g = new DraradechPuzzleGrid(3, 3, 1f, seed);
            const int samplesPerCellSide = 17;
            var n = g.Width * samplesPerCellSide;
            var m = g.Height * samplesPerCellSide;
            for (var iy = 0; iy < m; iy++)
            {
                for (var ix = 0; ix < n; ix++)
                {
                    var px = (ix + 0.5f) / n * g.Width * g.CellSize;
                    var py = (iy + 0.5f) / m * g.Height * g.CellSize;
                    var id = g.GetCellAt(new float2(px, py));
                    Assert.GreaterOrEqual(id, 0, $"seed={seed} point ({px}, {py}) not assigned to any cell");
                    Assert.Less(id, g.CellCount);
                }
            }
        }

        [Test]
        public void Construction_IsDeterministicForSameSeed()
        {
            var g1 = new DraradechPuzzleGrid(4, 4, 1f, 42);
            var g2 = new DraradechPuzzleGrid(4, 4, 1f, 42);
            for (var id = 0; id < g1.CellCount; id++)
            {
                var n = g1.GetCornersCount(id);
                Assert.AreEqual(n, g2.GetCornersCount(id));
                for (var i = 0; i < n; i++) Assert.AreEqual(g1.GetCorner(id, i), g2.GetCorner(id, i));
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentCorners()
        {
            var g1 = new DraradechPuzzleGrid(4, 4, 1f, 1);
            var g2 = new DraradechPuzzleGrid(4, 4, 1f, 2);
            var anyDifferent = false;
            for (var id = 0; id < g1.CellCount && !anyDifferent; id++)
            {
                var n = g1.GetCornersCount(id);
                for (var i = 0; i < n && !anyDifferent; i++)
                {
                    if (!Equals(g1.GetCorner(id, i), g2.GetCorner(id, i))) anyDifferent = true;
                }
            }
            Assert.IsTrue(anyDifferent, "two distinct seeds produced identical corners");
        }

        [Test]
        public void GetSidePolylineLength_MatchesContract()
        {
            var g = new DraradechPuzzleGrid(3, 3, 1f, 0);
            var n = g.Parameters.SamplesPerEdge;
            // Interior cell: every side full polyline.
            var idInterior = g.IdOf(1, 1);
            for (var s = 0; s < 4; s++) Assert.AreEqual(n, g.GetSidePolylineLength(idInterior, s));
            // (0, 0): right and top are full, bottom and left have only 2 endpoints.
            var idCorner = g.IdOf(0, 0);
            Assert.AreEqual(n, g.GetSidePolylineLength(idCorner, 0));
            Assert.AreEqual(2, g.GetSidePolylineLength(idCorner, 1));
            Assert.AreEqual(2, g.GetSidePolylineLength(idCorner, 2));
            Assert.AreEqual(n, g.GetSidePolylineLength(idCorner, 3));
        }

        [Test]
        public void CopySidePolyline_BoundarySideIsStraightLine()
        {
            var g = new DraradechPuzzleGrid(3, 3, 2f, 0);
            // (0, 0): bottom side BR=(2, 0) -> BL=(0, 0).
            var dest = new float2[2];
            g.CopySidePolyline(g.IdOf(0, 0), 1, dest);
            Assert.AreEqual(new float2(2, 0), dest[0]);
            Assert.AreEqual(new float2(0, 0), dest[1]);
        }

        [TestCase(2, 2)] [TestCase(3, 1)] [TestCase(1, 3)] [TestCase(5, 4)]
        public void Polygon_Stitches_PiecesCoverRectangleArea(int width, int height)
        {
            // Total signed-area magnitude of all cell polygons must equal the grid
            // rectangle area exactly. Since each polygon is CW (negative signed area),
            // sum of |signed area| equals grid area.
            var g = new DraradechPuzzleGrid(width, height, 1f, 11);
            AssertPolygonsTileRectangle(g, $"{width}x{height}");
        }

        // ---- helpers ----

        private static void AssertNoCellSelfIntersection(DraradechPuzzleGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var corners = new float2[n];
                g.CopyCorners(id, corners);
                for (var i = 0; i < n; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % n];
                    for (var j = i + 2; j < n; j++)
                    {
                        if (i == 0 && j == n - 1) continue;
                        var c = corners[j];
                        var d = corners[(j + 1) % n];
                        if (SegmentsProperlyIntersect(a, b, c, d))
                            Assert.Fail($"{label} cell={id} edges {i}->{(i + 1) % n} and {j}->{(j + 1) % n} cross");
                    }
                }
            }
        }

        private static void AssertAllCellsCw(DraradechPuzzleGrid g, int seed)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var corners = new float2[n];
                g.CopyCorners(id, corners);
                var area = 0f;
                for (var i = 0; i < n; i++)
                {
                    var p = corners[i];
                    var q = corners[(i + 1) % n];
                    area += p.x * q.y - q.x * p.y;
                }
                Assert.Less(area, 0f, $"seed={seed} cell={id} polygon not CW (signed area={area})");
            }
        }

        private static void AssertSharedEdgesAgree(DraradechPuzzleGrid g, int seed)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                for (var k = 0; k < 4; k++)
                {
                    var other = g.GetNeighbor(id, k);
                    if (other == -1) continue;
                    var jSide = g.GetNeighborIndex(other, id);
                    Assert.AreNotEqual(-1, jSide);

                    var lenA = g.GetSidePolylineLength(id, k);
                    var lenB = g.GetSidePolylineLength(other, jSide);
                    Assert.AreEqual(lenA, lenB, $"seed={seed} a={id} k={k} b={other} jSide={jSide}");

                    var a = new float2[lenA];
                    var b = new float2[lenB];
                    g.CopySidePolyline(id, k, a);
                    g.CopySidePolyline(other, jSide, b);

                    for (var i = 0; i < lenA; i++)
                    {
                        Assert.AreEqual(a[i].x, b[lenA - 1 - i].x, Eps, $"seed={seed} cell={id} side={k} i={i}");
                        Assert.AreEqual(a[i].y, b[lenA - 1 - i].y, Eps, $"seed={seed} cell={id} side={k} i={i}");
                    }
                }
            }
        }

        private static void AssertPolygonsTileRectangle(DraradechPuzzleGrid g, string label)
        {
            var totalArea = 0f;
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var corners = new float2[n];
                g.CopyCorners(id, corners);
                var area = 0f;
                for (var i = 0; i < n; i++)
                {
                    var p = corners[i];
                    var q = corners[(i + 1) % n];
                    area += p.x * q.y - q.x * p.y;
                }
                totalArea += math.abs(area) * 0.5f;
            }
            var expected = (float)g.Width * g.Height * g.CellSize * g.CellSize;
            Assert.AreEqual(expected, totalArea, expected * 1e-4f, label);
        }

        private static bool SegmentsProperlyIntersect(float2 a, float2 b, float2 c, float2 d)
        {
            var d1 = Cross(c, d, a);
            var d2 = Cross(c, d, b);
            var d3 = Cross(a, b, c);
            var d4 = Cross(a, b, d);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                   ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross(float2 p, float2 q, float2 r)
        {
            return (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
        }
    }
}
