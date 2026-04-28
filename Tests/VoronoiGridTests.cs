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

        [Test]
        public void GetCorner_ModWrapsNegativeAndOversizedIndex()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            var n = g.GetCornersCount(0);
            Assert.AreEqual(g.GetCorner(0, 0), g.GetCorner(0, n));
            Assert.AreEqual(g.GetCorner(0, 0), g.GetCorner(0, -n));
            Assert.AreEqual(g.GetCorner(0, 1), g.GetCorner(0, n + 1));
        }

        [Test]
        public void GetNeighbor_ModWrapsNegativeAndOversizedIndex()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            var n = g.GetCornersCount(0);
            Assert.AreEqual(g.GetNeighbor(0, 0), g.GetNeighbor(0, n));
            Assert.AreEqual(g.GetNeighbor(0, 0), g.GetNeighbor(0, -n));
            Assert.AreEqual(g.GetNeighbor(0, 1), g.GetNeighbor(0, n + 1));
        }

        [Test]
        public void AreNeighbors_SelfReturnsFalse()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            for (var id = 0; id < g.CellCount; id++)
                Assert.IsFalse(g.AreNeighbors(id, id));
        }

        [Test]
        public void AreNeighbors_OutOfRangeReturnsFalse()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            Assert.IsFalse(g.AreNeighbors(-1, 0));
            Assert.IsFalse(g.AreNeighbors(0, g.CellCount));
            Assert.IsFalse(g.AreNeighbors(g.CellCount, 0));
        }

        [Test]
        public void GetNeighborIndex_OutOfRangeReturnsMinusOne()
        {
            var g = new VoronoiGrid(Unit, 16, 0, 2);
            Assert.AreEqual(-1, g.GetNeighborIndex(-1, 0));
            Assert.AreEqual(-1, g.GetNeighborIndex(g.CellCount, 0));
        }

        [Test]
        public void Distance_BetweenNonAdjacentCells_IsAtLeastTwo()
        {
            var g = new VoronoiGrid(Unit, 64, 0, 3);
            // Find a cell pair that are not neighbours and assert their hop distance >= 2.
            var found = false;
            for (var a = 0; a < g.CellCount && !found; a++)
            {
                for (var b = a + 1; b < g.CellCount && !found; b++)
                {
                    if (!g.AreNeighbors(a, b))
                    {
                        Assert.GreaterOrEqual(g.Distance(a, b), 2,
                            $"Distance({a}, {b}) should be at least 2 for non-adjacent cells");
                        found = true;
                    }
                }
            }
            Assert.IsTrue(found, "Expected at least one non-adjacent cell pair");
        }

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
    }
}
