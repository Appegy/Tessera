using System;
using NUnit.Framework;

namespace Appegy.Lattice.Tests
{
    [TestFixture]
    public class GetDirectionTests
    {
        #region Square

        [Test]
        public void Square4_GetDirection_InverseOfGetNeighbor(
            [Values(-2, 0, 3)] int x,
            [Values(-1, 0, 2)] int y)
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: false);
            var cell = (x, y);
            for (var dir = 0; dir < 4; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                Assert.AreEqual(dir, grid.GetDirection(cell, neighbor),
                    $"cell={cell} dir={dir} neighbor={neighbor}");
            }
        }

        [Test]
        public void Square8_GetDirection_InverseOfGetNeighbor(
            [Values(-1, 0, 2)] int x,
            [Values(-1, 0, 1)] int y)
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: true);
            var cell = (x, y);
            for (var dir = 0; dir < 8; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                Assert.AreEqual(dir, grid.GetDirection(cell, neighbor),
                    $"cell={cell} dir={dir} neighbor={neighbor}");
            }
        }

        [Test]
        public void Square_GetDirection_NotNeighbors_Throws()
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: false);
            Assert.Throws<ArgumentException>(() => grid.GetDirection((0, 0), (2, 0)));
            Assert.Throws<ArgumentException>(() => grid.GetDirection((0, 0), (0, 0)));
        }

        [Test]
        public void Square4_GetDirection_DiagonalNotNeighbor_Throws()
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: false);
            Assert.Throws<ArgumentException>(() => grid.GetDirection((0, 0), (1, 1)));
        }

        #endregion

        #region Hex All

        [Test]
        public void HexAll_GetDirection_InverseOfGetNeighbor(
            [Values] HexagonalGridType type,
            [Values(-1, 0, 2)] int x,
            [Values(-1, 0, 1)] int y)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (x, y);
            for (var dir = 0; dir < 6; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                Assert.AreEqual(dir, grid.GetDirection(cell, neighbor),
                    $"type={type} cell={cell} dir={dir} neighbor={neighbor}");
            }
        }

        [Test]
        public void HexAll_GetDirection_NotNeighbors_Throws([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            Assert.Throws<ArgumentException>(() => grid.GetDirection((0, 0), (3, 3)));
            Assert.Throws<ArgumentException>(() => grid.GetDirection((0, 0), (0, 0)));
        }

        #endregion

        #region Hex Even/Odd

        [Test]
        public void HexEven_GetDirection_InverseOfGetNeighbor(
            [Values] HexagonalGridType type,
            [Values(-1, 0, 2)] int x,
            [Values(-1, 0, 1)] int y)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var cell = (x, y);
            for (var dir = 0; dir < 3; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                Assert.AreEqual(dir, grid.GetDirection(cell, neighbor),
                    $"type={type} cell={cell} dir={dir} neighbor={neighbor}");
            }
        }

        [Test]
        public void HexOdd_GetDirection_InverseOfGetNeighbor(
            [Values] HexagonalGridType type,
            [Values(-1, 0, 2)] int x,
            [Values(-1, 0, 1)] int y)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (x, y);
            for (var dir = 0; dir < 3; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                Assert.AreEqual(dir, grid.GetDirection(cell, neighbor),
                    $"type={type} cell={cell} dir={dir} neighbor={neighbor}");
            }
        }

        [Test]
        public void HexEven_GetDirection_OddNeighbor_Throws([Values] HexagonalGridType type)
        {
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (1, 1);

            // Odd neighbors should not be found in Even mode
            for (var dir = 0; dir < 3; dir++)
            {
                var oddNeighbor = odd.GetNeighbor(cell, dir);
                Assert.Throws<ArgumentException>(() => even.GetDirection(cell, oddNeighbor),
                    $"type={type} dir={dir} oddNeighbor={oddNeighbor}");
            }
        }

        #endregion
    }
}
