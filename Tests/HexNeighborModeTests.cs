using System.Linq;
using NUnit.Framework;

namespace Appegy.Lattice.Tests
{
    [TestFixture]
    public class HexNeighborModeTests
    {
        #region DirectionsCount

        [Test]
        public void DirectionsCount_AllMode_Returns6([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            Assert.AreEqual(6, grid.DirectionsCount);
        }

        [Test]
        public void DirectionsCount_EvenMode_Returns3([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            Assert.AreEqual(3, grid.DirectionsCount);
        }

        [Test]
        public void DirectionsCount_OddMode_Returns3([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            Assert.AreEqual(3, grid.DirectionsCount);
        }

        #endregion

        #region GetNeighbor mapping

        [Test]
        public void EvenMode_Direction0_MapsToPhysical0([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var cell = (2, 3);

            // Even direction 0 → physical 0
            Assert.AreEqual(all.GetNeighbor(cell, 0), even.GetNeighbor(cell, 0));
        }

        [Test]
        public void EvenMode_Direction1_MapsToPhysical2([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var cell = (2, 3);

            // Even direction 1 → physical 2
            Assert.AreEqual(all.GetNeighbor(cell, 2), even.GetNeighbor(cell, 1));
        }

        [Test]
        public void EvenMode_Direction2_MapsToPhysical4([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var cell = (2, 3);

            // Even direction 2 → physical 4
            Assert.AreEqual(all.GetNeighbor(cell, 4), even.GetNeighbor(cell, 2));
        }

        [Test]
        public void OddMode_Direction0_MapsToPhysical1([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (2, 3);

            // Odd direction 0 → physical 1
            Assert.AreEqual(all.GetNeighbor(cell, 1), odd.GetNeighbor(cell, 0));
        }

        [Test]
        public void OddMode_Direction1_MapsToPhysical3([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (2, 3);

            // Odd direction 1 → physical 3
            Assert.AreEqual(all.GetNeighbor(cell, 3), odd.GetNeighbor(cell, 1));
        }

        [Test]
        public void OddMode_Direction2_MapsToPhysical5([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (2, 3);

            // Odd direction 2 → physical 5
            Assert.AreEqual(all.GetNeighbor(cell, 5), odd.GetNeighbor(cell, 2));
        }

        #endregion

        #region GetNeighbors

        [Test]
        public void GetNeighbors_EvenMode_Returns3([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var neighbors = grid.GetNeighbors((1, 1)).ToList();
            Assert.AreEqual(3, neighbors.Count);
            Assert.AreEqual(3, neighbors.Distinct().Count());
        }

        [Test]
        public void GetNeighbors_OddMode_Returns3([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var neighbors = grid.GetNeighbors((1, 1)).ToList();
            Assert.AreEqual(3, neighbors.Count);
            Assert.AreEqual(3, neighbors.Distinct().Count());
        }

        [Test]
        public void GetNeighbors_EvenAndOdd_CoverAll6([Values] HexagonalGridType type)
        {
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);

            var cell = (2, 2);
            var evenNeighbors = even.GetNeighbors(cell).ToHashSet();
            var oddNeighbors = odd.GetNeighbors(cell).ToHashSet();
            var allNeighbors = all.GetNeighbors(cell).ToHashSet();

            // Even and Odd combined should cover all 6
            evenNeighbors.UnionWith(oddNeighbors);
            Assert.IsTrue(allNeighbors.SetEquals(evenNeighbors));
        }

        #endregion

        #region AreNeighbors semantic change

        [Test]
        public void AreNeighbors_ActiveDirection_ReturnsTrue([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var cell = (1, 1);
            foreach (var n in grid.GetNeighbors(cell))
            {
                Assert.IsTrue(grid.AreNeighbors(cell, n));
            }
        }

        [Test]
        public void AreNeighbors_InactiveDirection_ReturnsFalse([Values] HexagonalGridType type)
        {
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (1, 1);

            // Neighbors in Odd mode should NOT be neighbors in Even mode
            foreach (var n in odd.GetNeighbors(cell))
            {
                Assert.IsFalse(even.AreNeighbors(cell, n),
                    $"Cell {n} should not be a neighbor in Even mode");
            }
        }

        [Test]
        public void AreNeighbors_SameCell_ReturnsFalse([Values] HexNeighborMode mode)
        {
            var grid = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd, mode);
            Assert.IsFalse(grid.AreNeighbors((0, 0), (0, 0)));
        }

        #endregion

        #region Direction wrapping

        [Test]
        public void GetNeighbor_WrapsInEvenMode([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);
            var cell = (1, 1);

            // direction 3 should wrap to direction 0 (count=3)
            Assert.AreEqual(grid.GetNeighbor(cell, 0), grid.GetNeighbor(cell, 3));
            // direction -1 should wrap to direction 2
            Assert.AreEqual(grid.GetNeighbor(cell, 2), grid.GetNeighbor(cell, -1));
        }

        [Test]
        public void GetNeighbor_WrapsInOddMode([Values] HexagonalGridType type)
        {
            var grid = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);
            var cell = (1, 1);

            Assert.AreEqual(grid.GetNeighbor(cell, 0), grid.GetNeighbor(cell, 3));
            Assert.AreEqual(grid.GetNeighbor(cell, 2), grid.GetNeighbor(cell, -1));
        }

        #endregion

        #region CornersCount unchanged

        [Test]
        public void CornersCount_AlwaysReturns6([Values] HexagonalGridType type, [Values] HexNeighborMode mode)
        {
            var grid = new HexagonalTessellation(1.0f, type, mode);
            Assert.AreEqual(6, grid.CornersCount);
        }

        #endregion

        #region Default mode is All

        [Test]
        public void DefaultMode_IsAll([Values] HexagonalGridType type)
        {
            var withDefault = new HexagonalTessellation(1.0f, type);
            var withExplicit = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);

            Assert.AreEqual(withExplicit.DirectionsCount, withDefault.DirectionsCount);

            var cell = (2, 3);
            for (var i = 0; i < 6; i++)
            {
                Assert.AreEqual(
                    withExplicit.GetNeighbor(cell, i),
                    withDefault.GetNeighbor(cell, i));
            }
        }

        #endregion

        #region Pixel methods unchanged by mode

        [Test]
        public void ToPoint2_SameRegardlessOfMode([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);

            var cell = (3, 2);
            var pAll = all.ToPoint2(cell);
            var pEven = even.ToPoint2(cell);
            Assert.AreEqual(pAll.X, pEven.X, 1e-5f);
            Assert.AreEqual(pAll.Y, pEven.Y, 1e-5f);
        }

        [Test]
        public void ToCell_SameRegardlessOfMode([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var odd = new HexagonalTessellation(1.0f, type, HexNeighborMode.Odd);

            var p = all.ToPoint2((2, 1));
            Assert.AreEqual(all.ToCell(p.X, p.Y), odd.ToCell(p.X, p.Y));
        }

        [Test]
        public void GetCornerPoint_SameRegardlessOfMode([Values] HexagonalGridType type)
        {
            var all = new HexagonalTessellation(1.0f, type, HexNeighborMode.All);
            var even = new HexagonalTessellation(1.0f, type, HexNeighborMode.Even);

            for (var c = 0; c < 6; c++)
            {
                var cAll = all.GetCornerPoint((1, 1), c);
                var cEven = even.GetCornerPoint((1, 1), c);
                Assert.AreEqual(cAll.X, cEven.X, 1e-5f, $"Corner {c} X");
                Assert.AreEqual(cAll.Y, cEven.Y, 1e-5f, $"Corner {c} Y");
            }
        }

        #endregion
    }
}
