using System;
using System.Linq;
using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    [TestFixture]
    public class TesseraGridTests
    {
        private Tessellation _square;
        private Tessellation _hex;

        [SetUp]
        public void Setup()
        {
            _square = new SquareTessellation(1.0f, false);
            _hex = new HexagonalTessellation(1.0f, HexagonalGridType.PointyOdd);
        }

        #region Construction

        [Test]
        public void Constructor_SetsProperties()
        {
            var grid = new TesseraGrid<int>(_square, 5, 3);
            Assert.AreEqual(5, grid.Width);
            Assert.AreEqual(3, grid.Height);
            Assert.AreEqual(15, grid.Count);
        }

        [Test]
        public void Constructor_StoresTessellation()
        {
            var grid = new TesseraGrid<int>(_hex, 4, 4);
            Assert.AreEqual(6, grid.Tessellation.DirectionsCount);
        }

        [Test]
        public void Constructor_InvalidWidth_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TesseraGrid<int>(_square, 0, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TesseraGrid<int>(_square, -1, 5));
        }

        [Test]
        public void Constructor_InvalidHeight_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TesseraGrid<int>(_square, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TesseraGrid<int>(_square, 5, -1));
        }

        [Test]
        public void Constructor_From2DArray()
        {
            var data = new int[3, 2]; // width=3, height=2
            data[0, 0] = 10;
            data[2, 1] = 42;

            var grid = new TesseraGrid<int>(_square, data);
            Assert.AreEqual(3, grid.Width);
            Assert.AreEqual(2, grid.Height);
            Assert.AreEqual(10, grid[0, 0]);
            Assert.AreEqual(42, grid[2, 1]);
        }

        #endregion

        #region Indexer

        [Test]
        public void Indexer_GetSet_Works()
        {
            var grid = new TesseraGrid<int>(_square, 3, 3);
            grid[0, 0] = 1;
            grid[2, 2] = 9;
            grid[1, 0] = 5;

            Assert.AreEqual(1, grid[0, 0]);
            Assert.AreEqual(9, grid[2, 2]);
            Assert.AreEqual(5, grid[1, 0]);
        }

        [Test]
        public void Indexer_DefaultValue_IsDefault()
        {
            var intGrid = new TesseraGrid<int>(_square, 3, 3);
            Assert.AreEqual(0, intGrid[0, 0]);
            Assert.AreEqual(0, intGrid[2, 2]);

            var stringGrid = new TesseraGrid<string>(_square, 2, 2);
            Assert.IsNull(stringGrid[0, 0]);
        }

        [Test]
        public void Indexer_OutOfBounds_Throws()
        {
            var grid = new TesseraGrid<int>(_square, 3, 3);

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = grid[-1, 0]; });
            Assert.Throws<IndexOutOfRangeException>(() => { var _ = grid[0, -1]; });
            Assert.Throws<IndexOutOfRangeException>(() => { var _ = grid[3, 0]; });
            Assert.Throws<IndexOutOfRangeException>(() => { var _ = grid[0, 3]; });
        }

        [Test]
        public void Indexer_SetOutOfBounds_Throws()
        {
            var grid = new TesseraGrid<int>(_square, 3, 3);

            Assert.Throws<IndexOutOfRangeException>(() => grid[-1, 0] = 1);
            Assert.Throws<IndexOutOfRangeException>(() => grid[3, 0] = 1);
        }

        [Test]
        public void Indexer_AllCells_Writable()
        {
            var grid = new TesseraGrid<int>(_square, 4, 3);
            var counter = 0;
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 4; x++)
                grid[x, y] = ++counter;

            counter = 0;
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 4; x++)
                Assert.AreEqual(++counter, grid[x, y]);
        }

        #endregion

        #region Contains

        [Test]
        public void Contains_ValidCoords_ReturnsTrue()
        {
            var grid = new TesseraGrid<int>(_square, 3, 3);
            Assert.IsTrue(grid.Contains(0, 0));
            Assert.IsTrue(grid.Contains(2, 2));
            Assert.IsTrue(grid.Contains(1, 1));
        }

        [Test]
        public void Contains_InvalidCoords_ReturnsFalse()
        {
            var grid = new TesseraGrid<int>(_square, 3, 3);
            Assert.IsFalse(grid.Contains(-1, 0));
            Assert.IsFalse(grid.Contains(0, -1));
            Assert.IsFalse(grid.Contains(3, 0));
            Assert.IsFalse(grid.Contains(0, 3));
        }

        #endregion

        #region Enumeration

        [Test]
        public void Enumeration_IteratesAllElements()
        {
            var grid = new TesseraGrid<int>(_square, 3, 2);
            grid.Fill(7);

            var items = grid.ToList();
            Assert.AreEqual(6, items.Count);
            Assert.IsTrue(items.All(x => x == 7));
        }

        [Test]
        public void Enumeration_RowByRow()
        {
            var grid = new TesseraGrid<int>(_square, 2, 2);
            grid[0, 0] = 1;
            grid[1, 0] = 2;
            grid[0, 1] = 3;
            grid[1, 1] = 4;

            var items = grid.ToList();
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, items.ToArray());
        }

        [Test]
        public void Count_MatchesWidthTimesHeight()
        {
            var grid = new TesseraGrid<int>(_hex, 7, 5);
            Assert.AreEqual(35, grid.Count);
            Assert.AreEqual(35, grid.Count());
        }

        #endregion

        #region Fill

        [Test]
        public void Fill_SetsAllCells()
        {
            var grid = new TesseraGrid<string>(_square, 3, 3);
            grid.Fill("x");

            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                Assert.AreEqual("x", grid[x, y]);
        }

        #endregion

        #region Different types

        [Test]
        public void Works_WithEnum()
        {
            var grid = new TesseraGrid<HexNeighborMode>(_hex, 2, 2);
            grid[0, 0] = HexNeighborMode.Even;
            grid[1, 1] = HexNeighborMode.Odd;

            Assert.AreEqual(HexNeighborMode.Even, grid[0, 0]);
            Assert.AreEqual(HexNeighborMode.Odd, grid[1, 1]);
            Assert.AreEqual(HexNeighborMode.All, grid[1, 0]); // default
        }

        [Test]
        public void Works_WithString()
        {
            var grid = new TesseraGrid<string>(_square, 2, 2);
            grid[0, 0] = "hello";
            Assert.AreEqual("hello", grid[0, 0]);
            Assert.IsNull(grid[1, 1]);
        }

        #endregion

        #region Different tessellations

        [Test]
        public void Works_WithHexTessellation()
        {
            var hex = new HexagonalTessellation(1.0f, HexagonalGridType.FlatEven, HexNeighborMode.Even);
            var grid = new TesseraGrid<int>(hex, 4, 4);
            Assert.AreEqual(3, grid.Tessellation.DirectionsCount);
            grid[3, 3] = 42;
            Assert.AreEqual(42, grid[3, 3]);
        }

        [Test]
        public void Works_WithSquare8Tessellation()
        {
            Tessellation sq8 = new SquareTessellation(1.0f, true);
            var grid = new TesseraGrid<int>(sq8, 5, 5);
            Assert.AreEqual(8, grid.Tessellation.DirectionsCount);
        }

        #endregion
    }
}
