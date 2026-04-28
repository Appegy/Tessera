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
