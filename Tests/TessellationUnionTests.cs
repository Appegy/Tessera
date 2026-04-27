using System;
using System.Linq;
using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    [TestFixture]
    public class TessellationUnionTests
    {
        [Test]
        public void ImplicitConversion_Square()
        {
            var square = new SquareTessellation(1.0f, false);
            Tessellation grid = square;
            Assert.AreEqual(Tessellation.Kind.SquareTessellation, grid.Type);
            Assert.AreEqual(4, grid.DirectionsCount);
        }

        [Test]
        public void ImplicitConversion_Hex()
        {
            var hex = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd);
            Tessellation grid = hex;
            Assert.AreEqual(Tessellation.Kind.HexagonalTessellation, grid.Type);
            Assert.AreEqual(6, grid.DirectionsCount);
        }

        [Test]
        public void ImplicitConversion_HexEvenMode()
        {
            var hex = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd, HexNeighborMode.Even);
            Tessellation grid = hex;
            Assert.AreEqual(Tessellation.Kind.HexagonalTessellation, grid.Type);
            Assert.AreEqual(3, grid.DirectionsCount);
        }

        [Test]
        public void UnifiedAPI_GetNeighbors()
        {
            Tessellation grid = new SquareTessellation(1.0f, false);
            var neighbors = grid.GetNeighbors((0, 0)).ToList();
            Assert.AreEqual(4, neighbors.Count);
        }

        [Test]
        public void UnifiedAPI_ToPoint2_ToCell_RoundTrip()
        {
            Tessellation grid = new HexagonalTessellation(1.0f, HexagonalGridType.FlatEven);
            var cell = (2, 3);
            var point = grid.ToPoint2(cell);
            var result = grid.ToCell(point.X, point.Y);
            Assert.AreEqual(cell, result);
        }

        [Test]
        public void ArrayOfDifferentGrids_RoundTrip()
        {
            var grids = new Tessellation[]
            {
                new SquareTessellation(1.0f, false),
                new HexagonalTessellation(1.5f, HexagonalGridType.FlatOdd),
                new HexagonalTessellation(1.0f, HexagonalGridType.PointyEven, HexNeighborMode.Even),
            };

            foreach (var grid in grids)
            {
                var cell = (2, 1);
                var point = grid.ToPoint2(cell);
                var result = grid.ToCell(point.X, point.Y);
                Assert.AreEqual(cell, result);
            }
        }

        [Test]
        public void UnifiedAPI_AreNeighbors()
        {
            Tessellation grid = new SquareTessellation(1.0f, false);
            Assert.IsTrue(grid.AreNeighbors((0, 0), (1, 0)));
            Assert.IsFalse(grid.AreNeighbors((0, 0), (2, 0)));

            grid = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd);
            var neighbor = grid.GetNeighbor((0, 0), 0);
            Assert.IsTrue(grid.AreNeighbors((0, 0), neighbor));
        }

        [Test]
        public void UnifiedAPI_GetCornerPoint()
        {
            var grids = new Tessellation[]
            {
                new SquareTessellation(1.0f, false),
                new HexagonalTessellation(1.0f, HexagonalGridType.FlatEven),
            };

            foreach (var grid in grids)
            {
                var cornersCount = grid.CornersCount;
                var corners = Enumerable.Range(0, cornersCount)
                    .Select(i => grid.GetCornerPoint((0, 0), i))
                    .ToList();
                Assert.AreEqual(cornersCount, corners.Count);
                // All corners should be distinct
                for (var i = 0; i < cornersCount; i++)
                {
                    for (var j = i + 1; j < cornersCount; j++)
                    {
                        var dist = Math.Sqrt(
                            Math.Pow(corners[i].X - corners[j].X, 2) +
                            Math.Pow(corners[i].Y - corners[j].Y, 2));
                        Assert.Greater(dist, 0.01, $"Corners {i} and {j} overlap");
                    }
                }
            }
        }

        [Test]
        public void UnifiedAPI_Distance()
        {
            Tessellation grid = new SquareTessellation(1.0f, false);
            Assert.AreEqual(0, grid.Distance((0, 0), (0, 0)));
            Assert.AreEqual(2, grid.Distance((0, 0), (1, 1)));

            grid = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd);
            Assert.AreEqual(0, grid.Distance((0, 0), (0, 0)));
            Assert.AreEqual(2, grid.Distance((0, 0), (2, 0)));
        }
    }
}
