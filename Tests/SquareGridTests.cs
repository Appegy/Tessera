using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class SquareGridTests
    {
        // 4 x 3 grid with cellSize = 1.
        // Cell ids by (x, y):
        //   y=2: 8  9 10 11
        //   y=1: 4  5  6  7
        //   y=0: 0  1  2  3
        private SquareGrid _grid = null!;

        [SetUp]
        public void Setup()
        {
            _grid = new SquareGrid(4, 3, 1f);
        }

        [Test]
        public void Construct_StoresParameters()
        {
            Assert.AreEqual(4, _grid.Width);
            Assert.AreEqual(3, _grid.Height);
            Assert.AreEqual(1f, _grid.CellSize);
            Assert.AreEqual(12, _grid.CellCount);
        }

        [Test]
        public void Bounds_AnchoredAtOriginExtendsToWidthHeight()
        {
            Assert.AreEqual(new float2(0, 0), _grid.Bounds.Min);
            Assert.AreEqual(new float2(4, 3), _grid.Bounds.Max);
            Assert.AreEqual(new float2(4, 3), _grid.Bounds.Size);
        }

        [Test]
        public void IdOf_XYOf_RoundTrip()
        {
            for (var y = 0; y < _grid.Height; y++)
                for (var x = 0; x < _grid.Width; x++)
                {
                    var id = _grid.IdOf(x, y);
                    Assert.AreEqual((x, y), _grid.XYOf(id));
                }
        }

        [Test]
        public void GetCenter_IsHalfCellOffsetFromCorner()
        {
            Assert.AreEqual(new float2(0.5f, 0.5f), _grid.GetCenter(_grid.IdOf(0, 0)));
            Assert.AreEqual(new float2(1.5f, 1.5f), _grid.GetCenter(_grid.IdOf(1, 1)));
            Assert.AreEqual(new float2(3.5f, 2.5f), _grid.GetCenter(_grid.IdOf(3, 2)));
        }

        [Test]
        public void GetCornersCount_IsAlways4()
        {
            for (var id = 0; id < _grid.CellCount; id++)
                Assert.AreEqual(4, _grid.GetCornersCount(id));
        }

        [Test]
        public void GetCorner_ClockwiseFromTopRight()
        {
            // cell (1, 1): TR(2,2), BR(2,1), BL(1,1), TL(1,2)
            var id = _grid.IdOf(1, 1);
            Assert.AreEqual(new float2(2, 2), _grid.GetCorner(id, 0));
            Assert.AreEqual(new float2(2, 1), _grid.GetCorner(id, 1));
            Assert.AreEqual(new float2(1, 1), _grid.GetCorner(id, 2));
            Assert.AreEqual(new float2(1, 2), _grid.GetCorner(id, 3));
        }

        [Test]
        public void GetCorner_WrapsIndex()
        {
            var id = _grid.IdOf(1, 1);
            Assert.AreEqual(_grid.GetCorner(id, 0), _grid.GetCorner(id, 4));
            Assert.AreEqual(_grid.GetCorner(id, 1), _grid.GetCorner(id, -3));
            Assert.AreEqual(_grid.GetCorner(id, 3), _grid.GetCorner(id, -1));
        }

        [Test]
        public void CopyCorners_FillsAllFour()
        {
            var id = _grid.IdOf(1, 1);
            Span<float2> buf = stackalloc float2[4];
            _grid.CopyCorners(id, buf);
            Assert.AreEqual(new float2(2, 2), buf[0]);
            Assert.AreEqual(new float2(2, 1), buf[1]);
            Assert.AreEqual(new float2(1, 1), buf[2]);
            Assert.AreEqual(new float2(1, 2), buf[3]);
        }

        [Test]
        public void CopyCorners_ThrowsIfBufferTooSmall()
        {
            var id = _grid.IdOf(1, 1);
            Assert.Throws<ArgumentException>(() =>
            {
                Span<float2> buf = stackalloc float2[3];
                _grid.CopyCorners(id, buf);
            });
        }

        [Test]
        public void GetNeighbor_AlignedToEdges()
        {
            // edge 0 (TR->BR) = right edge -> right neighbour
            // edge 1 (BR->BL) = bottom edge -> bottom neighbour
            // edge 2 (BL->TL) = left edge -> left neighbour
            // edge 3 (TL->TR) = top edge -> top neighbour
            var id = _grid.IdOf(1, 1);
            Assert.AreEqual(_grid.IdOf(2, 1), _grid.GetNeighbor(id, 0));
            Assert.AreEqual(_grid.IdOf(1, 0), _grid.GetNeighbor(id, 1));
            Assert.AreEqual(_grid.IdOf(0, 1), _grid.GetNeighbor(id, 2));
            Assert.AreEqual(_grid.IdOf(1, 2), _grid.GetNeighbor(id, 3));
        }

        [Test]
        public void GetNeighbor_BoundaryReturnsMinusOne()
        {
            var bottomLeft = _grid.IdOf(0, 0);
            Assert.AreEqual(-1, _grid.GetNeighbor(bottomLeft, 1)); // bottom
            Assert.AreEqual(-1, _grid.GetNeighbor(bottomLeft, 2)); // left

            var topRight = _grid.IdOf(3, 2);
            Assert.AreEqual(-1, _grid.GetNeighbor(topRight, 0)); // right
            Assert.AreEqual(-1, _grid.GetNeighbor(topRight, 3)); // top
        }

        [Test]
        public void GetNeighbor_IndexWraps()
        {
            var id = _grid.IdOf(1, 1);
            Assert.AreEqual(_grid.GetNeighbor(id, 0), _grid.GetNeighbor(id, 4));
            Assert.AreEqual(_grid.GetNeighbor(id, 1), _grid.GetNeighbor(id, -3));
        }

        [Test]
        public void Alignment_NeighbourSitsAcrossCorrespondingEdge()
        {
            // For every cell with full neighbourhood, the i-th neighbour's centre is the reflection
            // of the cell centre across the midpoint of edge i (corner i -> corner (i+1) mod N).
            var id = _grid.IdOf(1, 1);
            var center = _grid.GetCenter(id);
            for (var i = 0; i < 4; i++)
            {
                var c0 = _grid.GetCorner(id, i);
                var c1 = _grid.GetCorner(id, (i + 1) % 4);
                var midpoint = (c0 + c1) * 0.5f;
                var nb = _grid.GetNeighbor(id, i);
                Assert.AreNotEqual(-1, nb);
                var expected = 2f * midpoint - center;
                var actual = _grid.GetCenter(nb);
                Assert.AreEqual(expected.x, actual.x, 1e-5f);
                Assert.AreEqual(expected.y, actual.y, 1e-5f);
            }
        }

        [Test]
        public void AreNeighbors_OrthogonalAdjacent_True()
        {
            Assert.IsTrue(_grid.AreNeighbors(_grid.IdOf(0, 0), _grid.IdOf(1, 0)));
            Assert.IsTrue(_grid.AreNeighbors(_grid.IdOf(0, 0), _grid.IdOf(0, 1)));
        }

        [Test]
        public void AreNeighbors_Diagonal_False()
        {
            Assert.IsFalse(_grid.AreNeighbors(_grid.IdOf(0, 0), _grid.IdOf(1, 1)));
        }

        [Test]
        public void AreNeighbors_Same_False()
        {
            Assert.IsFalse(_grid.AreNeighbors(0, 0));
        }

        [Test]
        public void AreNeighbors_Far_False()
        {
            Assert.IsFalse(_grid.AreNeighbors(_grid.IdOf(0, 0), _grid.IdOf(2, 0)));
        }

        [Test]
        public void GetNeighborIndex_RoundTripsWithGetNeighbor()
        {
            var id = _grid.IdOf(1, 1);
            for (var i = 0; i < 4; i++)
            {
                var nb = _grid.GetNeighbor(id, i);
                Assert.AreEqual(i, _grid.GetNeighborIndex(id, nb));
            }
        }

        [Test]
        public void GetNeighborIndex_BidirectionallyConsistent()
        {
            // a's right is b -> b's left is a.
            var a = _grid.IdOf(1, 1);
            var b = _grid.IdOf(2, 1);
            Assert.AreEqual(0, _grid.GetNeighborIndex(a, b));
            Assert.AreEqual(2, _grid.GetNeighborIndex(b, a));
        }

        [Test]
        public void GetNeighborIndex_NotNeighbor_MinusOne()
        {
            Assert.AreEqual(-1, _grid.GetNeighborIndex(_grid.IdOf(0, 0), _grid.IdOf(2, 2)));
            Assert.AreEqual(-1, _grid.GetNeighborIndex(_grid.IdOf(0, 0), _grid.IdOf(0, 0)));
        }

        [Test]
        public void GetCellAt_ReturnsCellContainingPoint()
        {
            Assert.AreEqual(_grid.IdOf(0, 0), _grid.GetCellAt(new float2(0.5f, 0.5f)));
            Assert.AreEqual(_grid.IdOf(1, 2), _grid.GetCellAt(new float2(1.5f, 2.5f)));
            Assert.AreEqual(_grid.IdOf(3, 0), _grid.GetCellAt(new float2(3.99f, 0.01f)));
        }

        [Test]
        public void GetCellAt_OutsideGrid_MinusOne()
        {
            Assert.AreEqual(-1, _grid.GetCellAt(new float2(-0.1f, 0.5f)));
            Assert.AreEqual(-1, _grid.GetCellAt(new float2(0.5f, -0.1f)));
            Assert.AreEqual(-1, _grid.GetCellAt(new float2(4.1f, 0.5f)));
            Assert.AreEqual(-1, _grid.GetCellAt(new float2(0.5f, 3.1f)));
        }

        [Test]
        public void GetCellAt_RoundTripsCenter()
        {
            for (var id = 0; id < _grid.CellCount; id++)
                Assert.AreEqual(id, _grid.GetCellAt(_grid.GetCenter(id)));
        }

        [Test]
        public void Distance_Manhattan()
        {
            Assert.AreEqual(0, _grid.Distance(_grid.IdOf(0, 0), _grid.IdOf(0, 0)));
            Assert.AreEqual(1, _grid.Distance(_grid.IdOf(0, 0), _grid.IdOf(1, 0)));
            Assert.AreEqual(2, _grid.Distance(_grid.IdOf(0, 0), _grid.IdOf(1, 1)));
            Assert.AreEqual(4, _grid.Distance(_grid.IdOf(0, 0), _grid.IdOf(2, 2)));
            Assert.AreEqual(5, _grid.Distance(_grid.IdOf(0, 0), _grid.IdOf(3, 2)));
        }

        [Test]
        public void Distance_Symmetric()
        {
            var a = _grid.IdOf(0, 0);
            var b = _grid.IdOf(2, 2);
            Assert.AreEqual(_grid.Distance(a, b), _grid.Distance(b, a));
        }

        [Test]
        public void Cell_ForwardsToGrid()
        {
            var cell = _grid.GetCell(_grid.IdOf(1, 1));
            Assert.AreEqual(_grid.IdOf(1, 1), cell.Id);
            Assert.AreEqual(new float2(1.5f, 1.5f), cell.Center);
            Assert.AreEqual(4, cell.CornersCount);
            Assert.AreEqual(_grid.IdOf(2, 1), cell.GetNeighbor(0));
            Assert.AreEqual(2, cell.DistanceTo(_grid.IdOf(2, 2)));
        }

        [Test]
        public void Constructor_RejectsBadArgs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquareGrid(0, 1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquareGrid(1, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquareGrid(1, 1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquareGrid(1, 1, -1f));
        }
    }
}