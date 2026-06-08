using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class ClassicPuzzleGridTests
    {
        private static ClassicPuzzleParameters Params(float roundness, float radius, float offset)
            => new ClassicPuzzleParameters(roundness, radius, offset);

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
    }
}
