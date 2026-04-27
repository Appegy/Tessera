using System;
using System.Linq;
using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    [TestFixture]
    public class SquareTessellationTests
    {
        private SquareTessellation _grid4;
        private SquareTessellation _grid8;

        [SetUp]
        public void Setup()
        {
            _grid4 = new SquareTessellation(1.0f, includeDiagonals: false);
            _grid8 = new SquareTessellation(1.0f, includeDiagonals: true);
        }

        [Test]
        public void DirectionsCount_4Mode_Returns4()
        {
            Assert.AreEqual(4, _grid4.DirectionsCount);
        }

        [Test]
        public void DirectionsCount_8Mode_Returns8()
        {
            Assert.AreEqual(8, _grid8.DirectionsCount);
        }

        [Test]
        public void CornersCount_Returns4()
        {
            Assert.AreEqual(4, _grid4.CornersCount);
            Assert.AreEqual(4, _grid8.CornersCount);
        }

        #region GetNeighbor / GetNeighbors

        [Test]
        public void GetNeighbor_4Dir_ReturnsCorrectNeighbors()
        {
            var cell = (2, 3);
            // 0=top, 1=right, 2=bottom, 3=left
            Assert.AreEqual((2, 4), _grid4.GetNeighbor(cell, 0));
            Assert.AreEqual((3, 3), _grid4.GetNeighbor(cell, 1));
            Assert.AreEqual((2, 2), _grid4.GetNeighbor(cell, 2));
            Assert.AreEqual((1, 3), _grid4.GetNeighbor(cell, 3));
        }

        [Test]
        public void GetNeighbor_8Dir_ReturnsCorrectNeighbors()
        {
            var cell = (0, 0);
            // 0=top, 1=TR, 2=right, 3=BR, 4=bottom, 5=BL, 6=left, 7=TL
            Assert.AreEqual((0, 1), _grid8.GetNeighbor(cell, 0));
            Assert.AreEqual((1, 1), _grid8.GetNeighbor(cell, 1));
            Assert.AreEqual((1, 0), _grid8.GetNeighbor(cell, 2));
            Assert.AreEqual((1, -1), _grid8.GetNeighbor(cell, 3));
            Assert.AreEqual((0, -1), _grid8.GetNeighbor(cell, 4));
            Assert.AreEqual((-1, -1), _grid8.GetNeighbor(cell, 5));
            Assert.AreEqual((-1, 0), _grid8.GetNeighbor(cell, 6));
            Assert.AreEqual((-1, 1), _grid8.GetNeighbor(cell, 7));
        }

        [Test]
        public void GetNeighbor_DirectionWraps()
        {
            var cell = (0, 0);
            Assert.AreEqual(_grid4.GetNeighbor(cell, 0), _grid4.GetNeighbor(cell, 4));
            Assert.AreEqual(_grid4.GetNeighbor(cell, 1), _grid4.GetNeighbor(cell, -3));
        }

        [Test]
        public void GetNeighbors_4Dir_Returns4Cells()
        {
            var neighbors = _grid4.GetNeighbors((0, 0)).ToList();
            Assert.AreEqual(4, neighbors.Count);
            CollectionAssert.Contains(neighbors, (0, 1));
            CollectionAssert.Contains(neighbors, (1, 0));
            CollectionAssert.Contains(neighbors, (0, -1));
            CollectionAssert.Contains(neighbors, (-1, 0));
        }

        [Test]
        public void GetNeighbors_8Dir_Returns8Cells()
        {
            var neighbors = _grid8.GetNeighbors((0, 0)).ToList();
            Assert.AreEqual(8, neighbors.Count);
        }

        #endregion

        #region AreNeighbors

        [Test]
        public void AreNeighbors_4Dir_Adjacent_ReturnsTrue()
        {
            Assert.IsTrue(_grid4.AreNeighbors((0, 0), (0, 1)));
            Assert.IsTrue(_grid4.AreNeighbors((0, 0), (1, 0)));
        }

        [Test]
        public void AreNeighbors_4Dir_Diagonal_ReturnsFalse()
        {
            Assert.IsFalse(_grid4.AreNeighbors((0, 0), (1, 1)));
        }

        [Test]
        public void AreNeighbors_8Dir_Diagonal_ReturnsTrue()
        {
            Assert.IsTrue(_grid8.AreNeighbors((0, 0), (1, 1)));
            Assert.IsTrue(_grid8.AreNeighbors((0, 0), (-1, -1)));
        }

        [Test]
        public void AreNeighbors_Same_ReturnsFalse()
        {
            Assert.IsFalse(_grid4.AreNeighbors((0, 0), (0, 0)));
            Assert.IsFalse(_grid8.AreNeighbors((0, 0), (0, 0)));
        }

        [Test]
        public void AreNeighbors_Far_ReturnsFalse()
        {
            Assert.IsFalse(_grid4.AreNeighbors((0, 0), (2, 0)));
            Assert.IsFalse(_grid8.AreNeighbors((0, 0), (2, 0)));
        }

        #endregion

        #region ToPoint2 / ToCell round-trip

        [Test]
        public void ToPoint2_Origin_ReturnsZero()
        {
            var p = _grid4.ToPoint2((0, 0));
            Assert.AreEqual(0f, p.X, 1e-5f);
            Assert.AreEqual(0f, p.Y, 1e-5f);
        }

        [Test]
        public void ToPoint2_Cell_CorrectPosition()
        {
            // inscribedRadius=1, side=2, center of (1,2) = (2, 4)
            var p = _grid4.ToPoint2((1, 2));
            Assert.AreEqual(2f, p.X, 1e-5f);
            Assert.AreEqual(4f, p.Y, 1e-5f);
        }

        [Test]
        public void RoundTrip_ToCellToPoint2([Values(-3, -1, 0, 1, 5)] int x, [Values(-2, 0, 3)] int y)
        {
            var cell = (x, y);
            var point = _grid4.ToPoint2(cell);
            var result = _grid4.ToCell(point.X, point.Y);
            Assert.AreEqual(cell, result);
        }

        [Test]
        public void RoundTrip_8Dir([Values(-3, 0, 4)] int x, [Values(-2, 0, 3)] int y)
        {
            var cell = (x, y);
            var point = _grid8.ToPoint2(cell);
            var result = _grid8.ToCell(point.X, point.Y);
            Assert.AreEqual(cell, result);
        }

        #endregion

        #region GetCornerPoint

        [Test]
        public void GetCornerPoint_Origin_CorrectPositions()
        {
            // Cell (0,0) center at (0,0), r=1
            // 0=TR(1,1), 1=BR(1,-1), 2=BL(-1,-1), 3=TL(-1,1)
            var c0 = _grid4.GetCornerPoint((0, 0), 0);
            var c1 = _grid4.GetCornerPoint((0, 0), 1);
            var c2 = _grid4.GetCornerPoint((0, 0), 2);
            var c3 = _grid4.GetCornerPoint((0, 0), 3);

            Assert.AreEqual(1f, c0.X, 1e-5f); Assert.AreEqual(1f, c0.Y, 1e-5f);
            Assert.AreEqual(1f, c1.X, 1e-5f); Assert.AreEqual(-1f, c1.Y, 1e-5f);
            Assert.AreEqual(-1f, c2.X, 1e-5f); Assert.AreEqual(-1f, c2.Y, 1e-5f);
            Assert.AreEqual(-1f, c3.X, 1e-5f); Assert.AreEqual(1f, c3.Y, 1e-5f);
        }

        [Test]
        public void GetCornerPoint_CountEquals4()
        {
            var corners = Enumerable.Range(0, 4)
                .Select(i => _grid4.GetCornerPoint((0, 0), i))
                .ToList();
            Assert.AreEqual(4, corners.Distinct().Count());
        }

        [Test]
        public void GetCornerPoint_WrapsIndex()
        {
            var c0 = _grid4.GetCornerPoint((0, 0), 0);
            var c4 = _grid4.GetCornerPoint((0, 0), 4);
            Assert.AreEqual(c0.X, c4.X, 1e-5f);
            Assert.AreEqual(c0.Y, c4.Y, 1e-5f);
        }

        #endregion

        #region Distance

        [Test]
        public void Distance_4Dir_Manhattan()
        {
            Assert.AreEqual(0, _grid4.Distance((0, 0), (0, 0)));
            Assert.AreEqual(1, _grid4.Distance((0, 0), (1, 0)));
            Assert.AreEqual(1, _grid4.Distance((0, 0), (0, 1)));
            Assert.AreEqual(2, _grid4.Distance((0, 0), (1, 1)));
            Assert.AreEqual(5, _grid4.Distance((0, 0), (2, 3)));
        }

        [Test]
        public void Distance_8Dir_Chebyshev()
        {
            Assert.AreEqual(0, _grid8.Distance((0, 0), (0, 0)));
            Assert.AreEqual(1, _grid8.Distance((0, 0), (1, 0)));
            Assert.AreEqual(1, _grid8.Distance((0, 0), (1, 1)));
            Assert.AreEqual(3, _grid8.Distance((0, 0), (2, 3)));
        }

        [Test]
        public void Distance_Symmetric([Values(-2, 0, 3)] int x, [Values(-1, 0, 2)] int y)
        {
            var a = (0, 0);
            var b = (x, y);
            Assert.AreEqual(_grid4.Distance(a, b), _grid4.Distance(b, a));
            Assert.AreEqual(_grid8.Distance(a, b), _grid8.Distance(b, a));
        }

        #endregion
    }
}
