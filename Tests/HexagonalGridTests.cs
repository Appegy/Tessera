using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class HexagonalGridTests
    {
        private static readonly HexagonalGridType[] AllTypes =
        {
            HexagonalGridType.PointyOdd,
            HexagonalGridType.PointyEven,
            HexagonalGridType.FlatOdd,
            HexagonalGridType.FlatEven
        };

        [Test]
        public void Construct_StoresParameters([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 3, 1f, type);
            Assert.AreEqual(4, grid.Width);
            Assert.AreEqual(3, grid.Height);
            Assert.AreEqual(1f, grid.InscribedRadius);
            Assert.AreEqual(type, grid.Type);
            Assert.AreEqual(12, grid.CellCount);
        }

        [Test]
        public void IdOf_XYOf_RoundTrip([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 3, 1f, type);
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width;  x++)
            {
                var id = grid.IdOf(x, y);
                Assert.AreEqual((x, y), grid.XYOf(id));
            }
        }

        [Test]
        public void GetCornersCount_IsAlways6([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 3, 1f, type);
            for (int id = 0; id < grid.CellCount; id++)
                Assert.AreEqual(6, grid.GetCornersCount(id));
        }

        [Test]
        public void Cell00_CenterAtOrigin([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 4, 1f, type);
            var c = grid.GetCenter(grid.IdOf(0, 0));
            Assert.AreEqual(0f, c.x, 1e-5f);
            Assert.AreEqual(0f, c.y, 1e-5f);
        }

        [Test]
        public void GetCorner_WrapsIndex([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 4, 1f, type);
            var id = grid.IdOf(1, 1);
            Assert.AreEqual(grid.GetCorner(id, 0), grid.GetCorner(id, 6));
            Assert.AreEqual(grid.GetCorner(id, 1), grid.GetCorner(id, -5));
            Assert.AreEqual(grid.GetCorner(id, 5), grid.GetCorner(id, -1));
        }

        [Test]
        public void CopyCorners_FillsAllSix([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 4, 1f, type);
            var id = grid.IdOf(1, 1);
            Span<float2> buf = stackalloc float2[6];
            grid.CopyCorners(id, buf);
            for (int i = 0; i < 6; i++)
            {
                var direct = grid.GetCorner(id, i);
                Assert.AreEqual(direct.x, buf[i].x, 1e-5f);
                Assert.AreEqual(direct.y, buf[i].y, 1e-5f);
            }
        }

        [Test]
        public void CopyCorners_ThrowsIfBufferTooSmall([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 4, 1f, type);
            Assert.Throws<ArgumentException>(() =>
            {
                Span<float2> buf = stackalloc float2[5];
                grid.CopyCorners(grid.IdOf(1, 1), buf);
            });
        }

        [Test]
        public void Alignment_NeighbourSitsAcrossCorrespondingEdge([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            // Big enough so the central cell has all 6 neighbours.
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var id = grid.IdOf(3, 3);
            var center = grid.GetCenter(id);
            for (int i = 0; i < 6; i++)
            {
                var c0 = grid.GetCorner(id, i);
                var c1 = grid.GetCorner(id, (i + 1) % 6);
                var midpoint = (c0 + c1) * 0.5f;
                var nb = grid.GetNeighbor(id, i);
                Assert.AreNotEqual(-1, nb, $"central cell of {type}: neighbour {i} should exist");
                var expected = 2f * midpoint - center;
                var actual = grid.GetCenter(nb);
                Assert.AreEqual(expected.x, actual.x, 1e-4f, $"x mismatch type={type} i={i}");
                Assert.AreEqual(expected.y, actual.y, 1e-4f, $"y mismatch type={type} i={i}");
            }
        }

        [Test]
        public void GetNeighbor_BoundaryReturnsMinusOne([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(2, 2, 1f, type);
            var id = grid.IdOf(0, 0);
            int boundaryCount = 0;
            for (int i = 0; i < 6; i++)
                if (grid.GetNeighbor(id, i) == -1) boundaryCount++;
            Assert.Greater(boundaryCount, 0,
                $"corner cell of 2x2 {type} should have at least one boundary slot");
        }

        [Test]
        public void GetNeighbor_IndexWraps([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var id = grid.IdOf(3, 3);
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(grid.GetNeighbor(id, i), grid.GetNeighbor(id, i + 6));
        }

        [Test]
        public void GetNeighborIndex_RoundTripsWithGetNeighbor([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var id = grid.IdOf(3, 3);
            for (int i = 0; i < 6; i++)
            {
                var nb = grid.GetNeighbor(id, i);
                Assert.AreEqual(i, grid.GetNeighborIndex(id, nb));
            }
        }

        [Test]
        public void GetNeighborIndex_BidirectionallyConsistent([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var a = grid.IdOf(3, 3);
            for (int i = 0; i < 6; i++)
            {
                var b = grid.GetNeighbor(a, i);
                var indexFromB = grid.GetNeighborIndex(b, a);
                Assert.AreNotEqual(-1, indexFromB, $"reverse index missing for type={type} i={i}");
                Assert.AreEqual(a, grid.GetNeighbor(b, indexFromB));
            }
        }

        [Test]
        public void GetNeighborIndex_NotNeighbor_MinusOne([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var a = grid.IdOf(0, 0);
            var farAway = grid.IdOf(5, 5);
            Assert.AreEqual(-1, grid.GetNeighborIndex(a, farAway));
            Assert.AreEqual(-1, grid.GetNeighborIndex(a, a));
        }

        [Test]
        public void AreNeighbors_True_ForActualNeighbours([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var a = grid.IdOf(3, 3);
            for (int i = 0; i < 6; i++)
            {
                var b = grid.GetNeighbor(a, i);
                Assert.IsTrue(grid.AreNeighbors(a, b), $"failed for type={type} i={i}");
                Assert.IsTrue(grid.AreNeighbors(b, a), $"failed reverse for type={type} i={i}");
            }
        }

        [Test]
        public void AreNeighbors_FalseForSelfAndFar([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var a = grid.IdOf(3, 3);
            Assert.IsFalse(grid.AreNeighbors(a, a));
            Assert.IsFalse(grid.AreNeighbors(grid.IdOf(0, 0), grid.IdOf(6, 6)));
        }

        [Test]
        public void Distance_SelfIsZero([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(5, 5, 1f, type);
            Assert.AreEqual(0, grid.Distance(grid.IdOf(2, 2), grid.IdOf(2, 2)));
        }

        [Test]
        public void Distance_NeighbourIsOne([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(7, 7, 1f, type);
            var a = grid.IdOf(3, 3);
            for (int i = 0; i < 6; i++)
            {
                var b = grid.GetNeighbor(a, i);
                Assert.AreEqual(1, grid.Distance(a, b), $"failed for type={type} i={i}");
            }
        }

        [Test]
        public void Distance_Symmetric([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(5, 5, 1f, type);
            var a = grid.IdOf(0, 0);
            var b = grid.IdOf(3, 4);
            Assert.AreEqual(grid.Distance(a, b), grid.Distance(b, a));
        }

        [Test]
        public void GetCellAt_RoundTripsCenter([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(4, 4, 1f, type);
            for (int id = 0; id < grid.CellCount; id++)
            {
                var center = grid.GetCenter(id);
                Assert.AreEqual(id, grid.GetCellAt(center),
                    $"failed for type={type}, id={id}, center={center}");
            }
        }

        [Test]
        public void GetCellAt_OutsideGrid_MinusOne([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(2, 2, 1f, type);
            Assert.AreEqual(-1, grid.GetCellAt(new float2(100f, 100f)));
            Assert.AreEqual(-1, grid.GetCellAt(new float2(-100f, -100f)));
        }

        [Test]
        public void Bounds_ContainsAllCorners([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(3, 3, 1f, type);
            Span<float2> buf = stackalloc float2[6];
            for (int id = 0; id < grid.CellCount; id++)
            {
                grid.CopyCorners(id, buf);
                for (int c = 0; c < 6; c++)
                {
                    var corner = buf[c];
                    Assert.IsTrue(grid.Bounds.Contains(corner),
                        $"corner {c} of cell {id} ({corner}) outside bounds {grid.Bounds} for type={type}");
                }
            }
        }

        [Test]
        public void Cell_ForwardsToGrid([ValueSource(nameof(AllTypes))] HexagonalGridType type)
        {
            var grid = new HexagonalGrid(3, 3, 1f, type);
            var cellId = grid.IdOf(1, 1);
            var cell = grid.GetCell(cellId);
            Assert.AreEqual(cellId, cell.Id);
            var c = grid.GetCenter(cellId);
            Assert.AreEqual(c.x, cell.Center.x);
            Assert.AreEqual(c.y, cell.Center.y);
            Assert.AreEqual(6, cell.CornersCount);
        }

        [Test]
        public void Constructor_RejectsBadArgs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexagonalGrid(0, 1, 1f, HexagonalGridType.PointyOdd));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexagonalGrid(1, 0, 1f, HexagonalGridType.PointyOdd));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexagonalGrid(1, 1, 0f, HexagonalGridType.PointyOdd));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexagonalGrid(1, 1, -1f, HexagonalGridType.PointyOdd));
        }
    }
}
