using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class ClassicPuzzleGridTests
    {
        private const float Eps = 1e-4f;

        private static ClassicPuzzleParameters Params(float roundness, float radius, float offset, float deform = 0f)
            => new ClassicPuzzleParameters(roundness, radius, offset, deform);

        private static float PolygonArea(ClassicPuzzleGrid g, int id)
        {
            var n = g.GetCornersCount(id);
            Span<float2> pts = stackalloc float2[n];
            g.CopyCorners(id, pts);
            var a = 0f;
            for (var i = 0; i < n; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % n];
                a += p.x * q.y - q.x * p.y;
            }
            return 0.5f * math.abs(a);
        }

        // ---- Construction guards ----

        [Test]
        public void Construct_NonPositiveWidth_Throws()
            => Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicPuzzleGrid(0, 4, 1f, 0));

        [Test]
        public void Construct_NonPositiveHeight_Throws()
            => Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicPuzzleGrid(4, 0, 1f, 0));

        [Test]
        public void Construct_NonPositiveCellSize_Throws()
            => Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicPuzzleGrid(4, 4, 0f, 0));

        // ---- Determinism ----

        [Test]
        public void SameSeedAndParams_ProduceIdenticalCorners()
        {
            var p = Params(0.7f, 0.6f, 0.4f);
            var a = new ClassicPuzzleGrid(5, 4, 2f, 123, p);
            var b = new ClassicPuzzleGrid(5, 4, 2f, 123, p);
            for (var id = 0; id < a.CellCount; id++)
            {
                var n = a.GetCornersCount(id);
                Assert.AreEqual(n, b.GetCornersCount(id));
                Span<float2> ca = stackalloc float2[n];
                Span<float2> cb = stackalloc float2[n];
                a.CopyCorners(id, ca);
                b.CopyCorners(id, cb);
                for (var i = 0; i < n; i++) Assert.AreEqual(ca[i], cb[i]);
            }
        }

        // ---- Tiling: shared edges (bow + tab) cancel, total area equals the bounding rectangle ----

        [TestCase(0f, 0f, 0f)]
        [TestCase(0.5f, 0.5f, 0.5f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(0f, 1f, 0f)]
        public void TotalCellArea_EqualsBoundingRectangle(float roundness, float radius, float offset)
        {
            const float s = 1.5f;
            const int w = 6, h = 5;
            var g = new ClassicPuzzleGrid(w, h, s, 42, Params(roundness, radius, offset));
            var sum = 0f;
            for (var id = 0; id < g.CellCount; id++) sum += PolygonArea(g, id);
            Assert.AreEqual(w * h * s * s, sum, 1e-2f);
        }

        // ---- Stitching: a shared edge is identical for both owners ----

        [Test]
        public void SharedVerticalEdge_MatchesBetweenNeighbours()
        {
            var g = new ClassicPuzzleGrid(4, 4, 2f, 77, Params(1f, 1f, 1f));
            var left = g.IdOf(1, 2);
            var right = g.IdOf(2, 2);
            var n = g.GetSidePolylineLength(left, 0);
            Assert.AreEqual(n, g.GetSidePolylineLength(right, 2));
            Span<float2> rightSideOfLeft = stackalloc float2[n];
            Span<float2> leftSideOfRight = stackalloc float2[n];
            g.CopySidePolyline(left, 0, rightSideOfLeft);
            g.CopySidePolyline(right, 2, leftSideOfRight);
            // Same physical edge, opposite traversal direction.
            for (var i = 0; i < n; i++)
            {
                Assert.AreEqual(rightSideOfLeft[i], leftSideOfRight[n - 1 - i]);
            }
        }

        // ---- Every cell polygon is non-degenerate across the parameter range ----

        [TestCase(0f, 0f, 0f)]
        [TestCase(0.5f, 0.5f, 0.5f)]
        [TestCase(1f, 1f, 1f)]
        public void EveryCell_HasPositiveArea(float roundness, float radius, float offset)
        {
            var g = new ClassicPuzzleGrid(7, 7, 1f, 5, Params(roundness, radius, offset));
            for (var id = 0; id < g.CellCount; id++)
            {
                Assert.Greater(PolygonArea(g, id), 0f, $"cell {id} degenerate");
            }
        }

        // ---- A tab actually deforms interior cells away from a plain square ----

        [Test]
        public void InteriorCell_BoundaryLongerThanPlainSquare()
        {
            const float s = 2f;
            var g = new ClassicPuzzleGrid(4, 4, s, 11, Params(0f, 1f, 1f));
            // A centre cell has a tab on all four sides. Whatever way each tab points (so the area can
            // cancel back to the square), every tab is a detour that lengthens the boundary.
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            Span<float2> pts = stackalloc float2[n];
            g.CopyCorners(id, pts);
            var perimeter = 0f;
            for (var i = 0; i < n; i++) perimeter += math.distance(pts[i], pts[(i + 1) % n]);
            Assert.Greater(perimeter, 4f * s);
        }

        // ---- GetCellAt resolves a cell centre to its own id ----

        [Test]
        public void GetCellAt_CellCentre_ReturnsThatCell()
        {
            var g = new ClassicPuzzleGrid(5, 4, 2f, 3, Params(1f, 1f, 1f));
            for (var id = 0; id < g.CellCount; id++)
            {
                Assert.AreEqual(id, g.GetCellAt(g.GetCenter(id)));
            }
        }

        [Test]
        public void GetCellAt_OutsideBounds_ReturnsMinusOne()
        {
            var g = new ClassicPuzzleGrid(4, 4, 2f, 0, Params(0.5f, 0.5f, 0.5f));
            Assert.AreEqual(-1, g.GetCellAt(new float2(-1f, -1f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(100f, 100f)));
        }

        // ---- Head deform keeps the grid valid (tiling, positive area, containment) ----

        [Test]
        public void MaxDeform_PreservesTilingAndContainment()
        {
            const float s = 1.5f;
            const int w = 5, h = 5;
            var g = new ClassicPuzzleGrid(w, h, s, 13, new ClassicPuzzleParameters(1f, 1f, 1f, 1f));
            var sum = 0f;
            for (var id = 0; id < g.CellCount; id++)
            {
                var area = PolygonArea(g, id);
                Assert.Greater(area, 0f, $"cell {id} degenerate");
                Assert.AreEqual(id, g.GetCellAt(g.GetCenter(id)));
                sum += area;
            }
            Assert.AreEqual(w * h * s * s, sum, 1e-2f);
        }

        // ---- Adversarial: the shipping PuzzleEdge.Generate stays a simple, CW, tiling polygon ----

        [Test]
        public void Polygon_HasNoSelfIntersection_FullParameterSweep()
        {
            const int steps = 2; // {0, 0.5, 1} per axis over (roundness, radius, offset, deform)
            for (var a = 0; a <= steps; a++)
            for (var b = 0; b <= steps; b++)
            for (var c = 0; c <= steps; c++)
            for (var d = 0; d <= steps; d++)
            {
                var p = Params((float)a / steps, (float)b / steps, (float)c / steps, (float)d / steps);
                var g = new ClassicPuzzleGrid(3, 3, 1f, 0, p);
                var label = $"R={a} Rad={b} Off={c} Def={d}";
                AssertNoCellSelfIntersection(g, label);
                AssertAllCellsCw(g, label);
            }
        }

        [Test]
        public void Polygon_HasNoSelfIntersection_AllCubeCornersAcrossSeeds()
        {
            for (var mask = 0; mask < 16; mask++)
            {
                var p = Params(mask & 1, (mask >> 1) & 1, (mask >> 2) & 1, (mask >> 3) & 1);
                for (var seed = 0; seed < 8; seed++)
                {
                    var g = new ClassicPuzzleGrid(3, 3, 1f, seed, p);
                    var label = $"mask={mask} seed={seed}";
                    AssertNoCellSelfIntersection(g, label);
                    AssertSharedEdgesAgree(g, label);
                    AssertAllCellsCw(g, label);
                }
            }
        }

        [Test]
        public void Polygon_HasNoSelfIntersection_RandomizedFuzz()
        {
            var rng = new System.Random(20260609);
            for (var trial = 0; trial < 150; trial++)
            {
                var p = Params((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                var seed = rng.Next();
                var g = new ClassicPuzzleGrid(3, 3, 1f, seed, p);
                AssertNoCellSelfIntersection(g, $"trial={trial} seed={seed}");
            }
        }

        [Test]
        public void Polygon_StaysValid_OutOfRangeAndNanInputs()
        {
            var combos = new[]
            {
                new float4(float.NaN, float.NaN, float.NaN, float.NaN),
                new float4(-1f, -1f, -1f, -1f),
                new float4(2f, 2f, 2f, 2f),
                new float4(float.NegativeInfinity, float.PositiveInfinity, float.NaN, 5f),
                new float4(float.NaN, 0.3f, 0.7f, 1f),
                new float4(1.5f, -0.5f, float.NaN, 0.4f),
            };
            foreach (var v in combos)
            {
                var g = new ClassicPuzzleGrid(3, 3, 1f, 42, Params(v.x, v.y, v.z, v.w));
                var label = $"{v.x},{v.y},{v.z},{v.w}";
                AssertNoCellSelfIntersection(g, label);
                AssertPolygonsTileRectangle(g, label);
                AssertCornersFinite(g, label);
            }
        }

        [Test]
        public void Corners_AreAlwaysFinite_CubeCorners()
        {
            for (var mask = 0; mask < 16; mask++)
            {
                var g = new ClassicPuzzleGrid(4, 4, 1f, 42, Params(mask & 1, (mask >> 1) & 1, (mask >> 2) & 1, (mask >> 3) & 1));
                AssertCornersFinite(g, $"mask={mask}");
            }
        }

        [Test]
        public void Polygon_TilesRectangle_RandomizedFuzz()
        {
            var rng = new System.Random(20260610);
            for (var trial = 0; trial < 50; trial++)
            {
                var p = Params((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                var g = new ClassicPuzzleGrid(3, 3, 1f, rng.Next(), p);
                AssertPolygonsTileRectangle(g, $"trial={trial}");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(13)]
        public void SharedEdges_NeighboursAgreeExactly(int seed)
        {
            // Covers both vertical and horizontal interior edges (the reversed CopyCorners branches).
            var g = new ClassicPuzzleGrid(4, 4, 2f, seed, Params(1f, 1f, 1f, 1f));
            AssertSharedEdgesAgree(g, $"seed={seed}");
        }

        // Dense point sweep: every point in the bounds must map to a valid cell (pieces partition the
        // rectangle with no gaps/overlaps), incl. max deform where the lean shifts tabs along the edge.
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f, 0f)]
        [TestCase(1f, 1f, 1f, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0.4f)]
        public void GetCellAt_TilesTheRectangle(float roundness, float radius, float offset, float deform)
        {
            var g = new ClassicPuzzleGrid(3, 3, 1f, 5, Params(roundness, radius, offset, deform));
            const int samplesPerSide = 17;
            var n = g.Width * samplesPerSide;
            var m = g.Height * samplesPerSide;
            for (var iy = 0; iy < m; iy++)
            for (var ix = 0; ix < n; ix++)
            {
                var px = (ix + 0.5f) / n * g.Width * g.CellSize;
                var py = (iy + 0.5f) / m * g.Height * g.CellSize;
                var id = g.GetCellAt(new float2(px, py));
                Assert.GreaterOrEqual(id, 0, $"point ({px}, {py}) unassigned");
                Assert.Less(id, g.CellCount);
            }
        }

        [Test]
        public void DefaultDeform_ProducesValidGrid()
        {
            // The playground/debug default is TabDeform = 0.4; lock that it tiles and stays simple.
            var g = new ClassicPuzzleGrid(5, 5, 1.5f, 3, Params(0.5f, 0.5f, 0.6f, 0.4f));
            AssertNoCellSelfIntersection(g, "default-deform");
            AssertAllCellsCw(g, "default-deform");
            AssertPolygonsTileRectangle(g, "default-deform");
        }

        // ---- helpers ----

        private static void AssertNoCellSelfIntersection(ClassicPuzzleGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var c = new float2[n];
                g.CopyCorners(id, c);
                for (var i = 0; i < n; i++)
                for (var j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    if (SegmentsProperlyIntersect(c[i], c[(i + 1) % n], c[j], c[(j + 1) % n]))
                        Assert.Fail($"{label} cell={id} edges {i} and {j} cross");
                }
            }
        }

        private static void AssertAllCellsCw(ClassicPuzzleGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var c = new float2[n];
                g.CopyCorners(id, c);
                var area = 0f;
                for (var i = 0; i < n; i++) area += c[i].x * c[(i + 1) % n].y - c[(i + 1) % n].x * c[i].y;
                Assert.Less(area, 0f, $"{label} cell={id} not CW (signed area={area})");
            }
        }

        private static void AssertSharedEdgesAgree(ClassicPuzzleGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            for (var k = 0; k < 4; k++)
            {
                var other = g.GetNeighbor(id, k);
                if (other == -1) continue;
                var jSide = g.GetNeighborIndex(other, id);
                var lenA = g.GetSidePolylineLength(id, k);
                Assert.AreEqual(lenA, g.GetSidePolylineLength(other, jSide), $"{label} a={id} k={k}");
                var a = new float2[lenA];
                var b = new float2[lenA];
                g.CopySidePolyline(id, k, a);
                g.CopySidePolyline(other, jSide, b);
                for (var i = 0; i < lenA; i++)
                {
                    Assert.AreEqual(a[i].x, b[lenA - 1 - i].x, Eps, $"{label} cell={id} side={k} i={i} x");
                    Assert.AreEqual(a[i].y, b[lenA - 1 - i].y, Eps, $"{label} cell={id} side={k} i={i} y");
                }
            }
        }

        private static void AssertPolygonsTileRectangle(ClassicPuzzleGrid g, string label)
        {
            var total = 0f;
            for (var id = 0; id < g.CellCount; id++) total += PolygonArea(g, id);
            var expected = (float)g.Width * g.Height * g.CellSize * g.CellSize;
            Assert.AreEqual(expected, total, expected * 1e-3f, label);
        }

        private static void AssertCornersFinite(ClassicPuzzleGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                for (var i = 0; i < n; i++)
                {
                    var c = g.GetCorner(id, i);
                    Assert.IsTrue(math.isfinite(c.x) && math.isfinite(c.y), $"{label} cell={id} corner={i} non-finite");
                }
            }
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
            => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
    }
}
