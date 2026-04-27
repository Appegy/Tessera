using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    [TestFixture]
    public class GetOppositeDirectionTests
    {
        #region Square

        [Test]
        public void Square4_OppositeRoundTrip(
            [Values(-2, 0, 3)] int x,
            [Values(-1, 0, 2)] int y)
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: false);
            var cell = (x, y);
            for (var dir = 0; dir < 4; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                var opposite = grid.GetOppositeDirection(dir);
                var back = grid.GetNeighbor(neighbor, opposite);
                Assert.AreEqual(cell, back,
                    $"cell={cell} dir={dir} neighbor={neighbor} opposite={opposite}");
            }
        }

        [Test]
        public void Square8_OppositeRoundTrip(
            [Values(-1, 0, 2)] int x,
            [Values(-1, 0, 1)] int y)
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: true);
            var cell = (x, y);
            for (var dir = 0; dir < 8; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                var opposite = grid.GetOppositeDirection(dir);
                var back = grid.GetNeighbor(neighbor, opposite);
                Assert.AreEqual(cell, back,
                    $"cell={cell} dir={dir} neighbor={neighbor} opposite={opposite}");
            }
        }

        [Test]
        public void Square4_OppositeValues()
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: false);
            // 0=top→2=bottom, 1=right→3=left
            Assert.AreEqual(2, grid.GetOppositeDirection(0));
            Assert.AreEqual(3, grid.GetOppositeDirection(1));
            Assert.AreEqual(0, grid.GetOppositeDirection(2));
            Assert.AreEqual(1, grid.GetOppositeDirection(3));
        }

        [Test]
        public void Square8_OppositeValues()
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: true);
            // 0=top→4=bottom, 1=TR→5=BL, etc.
            Assert.AreEqual(4, grid.GetOppositeDirection(0));
            Assert.AreEqual(5, grid.GetOppositeDirection(1));
            Assert.AreEqual(6, grid.GetOppositeDirection(2));
            Assert.AreEqual(7, grid.GetOppositeDirection(3));
            Assert.AreEqual(0, grid.GetOppositeDirection(4));
            Assert.AreEqual(1, grid.GetOppositeDirection(5));
            Assert.AreEqual(2, grid.GetOppositeDirection(6));
            Assert.AreEqual(3, grid.GetOppositeDirection(7));
        }

        [Test]
        public void Square_OppositeDirection_WrapsInput()
        {
            var grid = new SquareTessellation(1.0f, includeDiagonals: false);
            Assert.AreEqual(grid.GetOppositeDirection(0), grid.GetOppositeDirection(4));
            Assert.AreEqual(grid.GetOppositeDirection(1), grid.GetOppositeDirection(-3));
        }

        #endregion

        #region Hex All

        [Test]
        public void HexAll_OppositeRoundTrip(
            [Values] HexagonalGridType type,
            [Values(-1, 0, 2)] int x,
            [Values(-1, 0, 1)] int y)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            var cell = (x, y);
            for (var dir = 0; dir < 6; dir++)
            {
                var neighbor = grid.GetNeighbor(cell, dir);
                var opposite = grid.GetOppositeDirection(dir);
                var back = grid.GetNeighbor(neighbor, opposite);
                Assert.AreEqual(cell, back,
                    $"type={type} cell={cell} dir={dir} opposite={opposite}");
            }
        }

        [Test]
        public void HexAll_OppositeValues()
        {
            var grid = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd);
            Assert.AreEqual(3, grid.GetOppositeDirection(0));
            Assert.AreEqual(4, grid.GetOppositeDirection(1));
            Assert.AreEqual(5, grid.GetOppositeDirection(2));
            Assert.AreEqual(0, grid.GetOppositeDirection(3));
            Assert.AreEqual(1, grid.GetOppositeDirection(4));
            Assert.AreEqual(2, grid.GetOppositeDirection(5));
        }

        [Test]
        public void HexAll_OppositeDirection_WrapsInput([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            Assert.AreEqual(grid.GetOppositeDirection(0), grid.GetOppositeDirection(6));
            Assert.AreEqual(grid.GetOppositeDirection(1), grid.GetOppositeDirection(-5));
        }

        #endregion

        #region Hex Even/Odd — physical opposite

        [Test]
        public void HexEven_OppositeReturnsPhysicalDirection([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            // Even: remapped 0→phys 0, 1→phys 2, 2→phys 4
            // Opposite: phys 0→3, phys 2→5, phys 4→1
            Assert.AreEqual(3, grid.GetOppositeDirection(0));
            Assert.AreEqual(5, grid.GetOppositeDirection(1));
            Assert.AreEqual(1, grid.GetOppositeDirection(2));
        }

        [Test]
        public void HexOdd_OppositeReturnsPhysicalDirection([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            // Odd: remapped 0→phys 1, 1→phys 3, 2→phys 5
            // Opposite: phys 1→4, phys 3→0, phys 5→2
            Assert.AreEqual(4, grid.GetOppositeDirection(0));
            Assert.AreEqual(0, grid.GetOppositeDirection(1));
            Assert.AreEqual(2, grid.GetOppositeDirection(2));
        }

        [Test]
        public void HexEven_OppositeIsInOddSet([Values] HexagonalGridType type)
        {
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var allGrid = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var oddGrid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);

            var cell = (2, 3);
            for (var dir = 0; dir < 3; dir++)
            {
                var neighbor = even.GetNeighbor(cell, dir);
                var oppositePhys = even.GetOppositeDirection(dir);

                // The physical opposite should navigate back through All mode
                var back = allGrid.GetNeighbor(neighbor, oppositePhys);
                Assert.AreEqual(cell, back,
                    $"type={type} dir={dir} oppositePhys={oppositePhys}");

                // The opposite physical direction should be reachable from Odd mode
                // (Odd physical dirs: 1, 3, 5)
                Assert.IsTrue(oppositePhys == 1 || oppositePhys == 3 || oppositePhys == 5,
                    $"Even opposite {oppositePhys} should be an Odd physical direction");
            }
        }

        [Test]
        public void HexOdd_OppositeIsInEvenSet([Values] HexagonalGridType type)
        {
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var allGrid = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);

            var cell = (2, 3);
            for (var dir = 0; dir < 3; dir++)
            {
                var neighbor = odd.GetNeighbor(cell, dir);
                var oppositePhys = odd.GetOppositeDirection(dir);

                var back = allGrid.GetNeighbor(neighbor, oppositePhys);
                Assert.AreEqual(cell, back,
                    $"type={type} dir={dir} oppositePhys={oppositePhys}");

                // The opposite physical direction should be in Even set (0, 2, 4)
                Assert.IsTrue(oppositePhys == 0 || oppositePhys == 2 || oppositePhys == 4,
                    $"Odd opposite {oppositePhys} should be an Even physical direction");
            }
        }

        #endregion

        #region Double opposite = identity

        [Test]
        public void Square_DoubleOpposite_IsIdentity()
        {
            var grid4 = new SquareTessellation(1.0f, includeDiagonals: false);
            var grid8 = new SquareTessellation(1.0f, includeDiagonals: true);
            for (var d = 0; d < 4; d++)
                Assert.AreEqual(d, grid4.GetOppositeDirection(grid4.GetOppositeDirection(d)));
            for (var d = 0; d < 8; d++)
                Assert.AreEqual(d, grid8.GetOppositeDirection(grid8.GetOppositeDirection(d)));
        }

        [Test]
        public void HexAll_DoubleOpposite_IsIdentity([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type);
            for (var d = 0; d < 6; d++)
                Assert.AreEqual(d, grid.GetOppositeDirection(grid.GetOppositeDirection(d)));
        }

        #endregion
    }
}
