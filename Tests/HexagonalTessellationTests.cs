using System;
using System.Linq;
using NUnit.Framework;

namespace Appegy.Lattice.Tests
{
    [TestFixture]
    public class HexagonalTessellationTests
    {
        private HexagonalTessellation _pointyOdd;
        private HexagonalTessellation _pointyEven;
        private HexagonalTessellation _flatOdd;
        private HexagonalTessellation _flatEven;

        [SetUp]
        public void Setup()
        {
            _pointyOdd = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd);
            _pointyEven = new HexagonalTessellation(1.0f, HexagonalGridType.PointyEven);
            _flatOdd = new HexagonalTessellation(1.0f, HexagonalGridType.FlatOdd);
            _flatEven = new HexagonalTessellation(1.0f, HexagonalGridType.FlatEven);
        }

        [Test]
        public void DirectionsCount_Returns6()
        {
            Assert.AreEqual(6, _pointyOdd.DirectionsCount);
            Assert.AreEqual(6, _flatEven.DirectionsCount);
        }

        [Test]
        public void CornersCount_Returns6()
        {
            Assert.AreEqual(6, _pointyOdd.CornersCount);
            Assert.AreEqual(6, _flatOdd.CornersCount);
        }

        #region GetNeighbor / GetNeighbors

        [Test]
        public void GetNeighbors_PointyOdd_Origin_Returns6()
        {
            var neighbors = _pointyOdd.GetNeighbors((0, 0)).ToList();
            Assert.AreEqual(6, neighbors.Count);
            Assert.AreEqual(6, neighbors.Distinct().Count());
        }

        [Test]
        public void GetNeighbors_AllTypes_Return6([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            foreach (var cell in new[] { (0, 0), (1, 1), (-1, 2), (3, -1) })
            {
                var neighbors = grid.GetNeighbors(cell).ToList();
                Assert.AreEqual(6, neighbors.Count, $"Cell {cell} type {type}");
                Assert.AreEqual(6, neighbors.Distinct().Count(), $"Cell {cell} type {type} distinct");
            }
        }

        [Test]
        public void GetNeighbor_DirectionWraps([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (1, 1);
            Assert.AreEqual(grid.GetNeighbor(cell, 0), grid.GetNeighbor(cell, 6));
            Assert.AreEqual(grid.GetNeighbor(cell, 1), grid.GetNeighbor(cell, -5));
        }

        [Test]
        public void GetNeighbor_IsSymmetric([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (2, 3);
            for (var dir = 0; dir < 6; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                Assert.IsTrue(grid.AreNeighbors(cell, neighbor), $"dir {dir}: {cell} -> {neighbor}");
                Assert.IsTrue(grid.AreNeighbors(neighbor, cell), $"dir {dir}: {neighbor} -> {cell}");
            }
        }

        #endregion

        #region AreNeighbors

        [Test]
        public void AreNeighbors_Adjacent_ReturnsTrue([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (0, 0);
            foreach (var n in grid.GetNeighbors(cell))
            {
                Assert.IsTrue(grid.AreNeighbors(cell, n));
            }
        }

        [Test]
        public void AreNeighbors_Same_ReturnsFalse([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            Assert.IsFalse(grid.AreNeighbors((0, 0), (0, 0)));
        }

        [Test]
        public void AreNeighbors_Far_ReturnsFalse()
        {
            Assert.IsFalse(_pointyOdd.AreNeighbors((0, 0), (3, 3)));
        }

        #endregion

        #region ToPoint2 / ToCell round-trip

        [Test]
        public void ToPoint2_Origin_ReturnsZero([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var p = grid.ToPoint2((0, 0));
            Assert.AreEqual(0f, p.X, 1e-4f);
            Assert.AreEqual(0f, p.Y, 1e-4f);
        }

        [Test]
        public void RoundTrip_ToCellToPoint2_AllTypes(
            [Values] HexagonalGridType type,
            [Values(-2, 0, 1, 3)] int x,
            [Values(-1, 0, 2)] int y)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (x, y);
            var point = grid.ToPoint2(cell);
            var result = grid.ToCell(point.X, point.Y);
            Assert.AreEqual(cell, result, $"type={type} cell={cell} point={point}");
        }

        #endregion

        #region GetCornerPoint

        [Test]
        public void GetCornerPoint_Returns6Distinct([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var corners = Enumerable.Range(0, 6)
                .Select(i => grid.GetCornerPoint((0, 0), i))
                .ToList();
            // All should be distinct (within float tolerance)
            for (var i = 0; i < 6; i++)
            {
                for (var j = i + 1; j < 6; j++)
                {
                    var dist = Math.Sqrt(
                        Math.Pow(corners[i].X - corners[j].X, 2) +
                        Math.Pow(corners[i].Y - corners[j].Y, 2));
                    Assert.Greater(dist, 0.01, $"Corners {i} and {j} are too close");
                }
            }
        }

        [Test]
        public void GetCornerPoint_WrapsIndex()
        {
            var c0 = _pointyOdd.GetCornerPoint((0, 0), 0);
            var c6 = _pointyOdd.GetCornerPoint((0, 0), 6);
            Assert.AreEqual(c0.X, c6.X, 1e-5f);
            Assert.AreEqual(c0.Y, c6.Y, 1e-5f);
        }

        [Test]
        public void GetCornerPoint_DistanceFromCenter_EqualsDescribedRadius([Values] HexagonalGridType type)
        {
            var r = 1.0f;
            var grid = new HexagonalTessellation(r, type);
            var describedRadius = (float)(r / Math.Cos(Math.PI / 6));
            var center = grid.ToPoint2((0, 0));

            for (var i = 0; i < 6; i++)
            {
                var corner = grid.GetCornerPoint((0, 0), i);
                var dist = Math.Sqrt(
                    Math.Pow(corner.X - center.X, 2) +
                    Math.Pow(corner.Y - center.Y, 2));
                Assert.AreEqual(describedRadius, dist, 1e-4, $"Corner {i}");
            }
        }

        #endregion

        #region Distance

        [Test]
        public void Distance_SameCell_ReturnsZero([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            Assert.AreEqual(0, grid.Distance((0, 0), (0, 0)));
            Assert.AreEqual(0, grid.Distance((3, 2), (3, 2)));
        }

        [Test]
        public void Distance_Neighbor_Returns1([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (0, 0);
            foreach (var n in grid.GetNeighbors(cell))
            {
                Assert.AreEqual(1, grid.Distance(cell, n), $"Neighbor {n}");
            }
        }

        [Test]
        public void Distance_Symmetric([Values] HexagonalGridType type,
            [Values(-2, 0, 3)] int x, [Values(-1, 0, 2)] int y)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var a = (0, 0);
            var b = (x, y);
            Assert.AreEqual(grid.Distance(a, b), grid.Distance(b, a));
        }

        [Test]
        public void Distance_KnownValues_PointyOdd()
        {
            Assert.AreEqual(2, _pointyOdd.Distance((0, 0), (2, 0)));
            Assert.AreEqual(2, _pointyOdd.Distance((0, 0), (0, 2)));
            Assert.AreEqual(3, _pointyOdd.Distance((0, 0), (3, 0)));
        }

        [Test]
        public void Distance_KnownValues_PointyEven()
        {
            Assert.AreEqual(2, _pointyEven.Distance((0, 0), (2, 0)));
            Assert.AreEqual(2, _pointyEven.Distance((0, 0), (0, 2)));
            Assert.AreEqual(1, _pointyEven.Distance((0, 0), (0, 1)));
        }

        [Test]
        public void Distance_KnownValues_FlatOdd()
        {
            Assert.AreEqual(2, _flatOdd.Distance((0, 0), (0, 2)));
            Assert.AreEqual(2, _flatOdd.Distance((0, 0), (2, 0)));
            Assert.AreEqual(1, _flatOdd.Distance((0, 0), (1, 0)));
        }

        [Test]
        public void Distance_KnownValues_FlatEven()
        {
            Assert.AreEqual(2, _flatEven.Distance((0, 0), (0, 2)));
            Assert.AreEqual(2, _flatEven.Distance((0, 0), (2, 0)));
            Assert.AreEqual(3, _flatEven.Distance((0, 0), (0, 3)));
        }

        [Test]
        public void Distance_NegativeCoords([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var d1 = grid.Distance((0, 0), (-2, -1));
            var d2 = grid.Distance((-2, -1), (0, 0));
            Assert.AreEqual(d1, d2);
            Assert.Greater(d1, 0);
        }

        #endregion
    }
}
