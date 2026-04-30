using System;
using System.Linq;
using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    public class CellMapTests
    {
        private HexagonalGrid _hex = null!;
        private SquareGrid _square = null!;

        [SetUp]
        public void Setup()
        {
            _square = new SquareGrid(4, 3, 1f);
            _hex = new HexagonalGrid(4, 3, 1f, HexagonalGridType.PointyOdd);
        }

        [Test]
        public void Construct_ExposesTessellationAndCount()
        {
            var data = new CellMap<int>(_square);
            Assert.AreSame(_square, data.Tessellation);
            Assert.AreEqual(_square.CellCount, data.Count);
        }

        [Test]
        public void Construct_WithFill_AllCellsHaveFillValue()
        {
            var data = new CellMap<int>(_square, 7);
            for (var id = 0; id < data.Count; id++)
                Assert.AreEqual(7, data[id]);
        }

        [Test]
        public void Construct_NullTessellation_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new CellMap<int>((ITessellation)null!));
            Assert.Throws<ArgumentNullException>(() => _ = new CellMap<int>(null!, 7));
        }

        [Test]
        public void Construct_FromCopy_SharesTessellationReference()
        {
            var source = new CellMap<int>(_square);
            var copy = new CellMap<int>(source);
            Assert.AreSame(source.Tessellation, copy.Tessellation);
            Assert.AreEqual(source.Count, copy.Count);
        }

        [Test]
        public void Construct_FromCopy_DuplicatesData()
        {
            var source = new CellMap<int>(_square);
            for (var id = 0; id < source.Count; id++) source[id] = id * 10;

            var copy = new CellMap<int>(source);
            for (var id = 0; id < source.Count; id++) Assert.AreEqual(id * 10, copy[id]);
        }

        [Test]
        public void Construct_FromCopy_IsIndependent()
        {
            var source = new CellMap<int>(_square, 1);
            var copy = new CellMap<int>(source);

            source[0] = 99;
            copy[1] = 42;

            Assert.AreEqual(1, copy[0]);
            Assert.AreEqual(1, source[1]);
        }

        [Test]
        public void Construct_FromCopy_NullSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new CellMap<int>((CellMap<int>)null!));
        }

        [Test]
        public void Indexer_GetSet_ById()
        {
            var data = new CellMap<int>(_square);
            data[3] = 42;
            Assert.AreEqual(42, data[3]);
        }

        [Test]
        public void Indexer_DefaultValue()
        {
            var ints = new CellMap<int>(_square);
            Assert.AreEqual(0, ints[0]);

            var strings = new CellMap<string?>(_square);
            Assert.IsNull(strings[0]);
        }

        [Test]
        public void Indexer_OutOfBounds_Throws()
        {
            var data = new CellMap<int>(_square);
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var _ = data[-1];
            });
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var _ = data[data.Count];
            });
            Assert.Throws<IndexOutOfRangeException>(() => data[-1] = 1);
            Assert.Throws<IndexOutOfRangeException>(() => data[data.Count] = 1);
        }

        [Test]
        public void Fill_SetsAllCells()
        {
            var data = new CellMap<string>(_square, "init");
            data.Fill("filled");
            for (var id = 0; id < data.Count; id++)
                Assert.AreEqual("filled", data[id]);
        }

        [Test]
        public void Enumeration_VisitsCellsInOrder()
        {
            var data = new CellMap<int>(_square);
            for (var id = 0; id < data.Count; id++) data[id] = id;
            var result = data.ToList();
            CollectionAssert.AreEqual(Enumerable.Range(0, data.Count).ToList(), result);
        }

        [Test]
        public void Works_WithSquareGrid()
        {
            var data = new CellMap<int>(_square);
            data[_square.IdOf(2, 1)] = 99;
            Assert.AreEqual(99, data[_square.IdOf(2, 1)]);
        }

        [Test]
        public void Works_WithHexGrid()
        {
            var data = new CellMap<int>(_hex);
            Assert.AreEqual(_hex.CellCount, data.Count);
            data[_hex.IdOf(3, 2)] = 99;
            Assert.AreEqual(99, data[_hex.IdOf(3, 2)]);
        }

        [Test]
        public void Works_WithEnum()
        {
            var data = new CellMap<HexagonalGridType>(_square);
            data[0] = HexagonalGridType.PointyOdd;
            data[1] = HexagonalGridType.FlatEven;
            Assert.AreEqual(HexagonalGridType.PointyOdd, data[0]);
            Assert.AreEqual(HexagonalGridType.FlatEven, data[1]);
        }

        [Test]
        public void Works_WithString()
        {
            var data = new CellMap<string?>(_square);
            data[0] = "hello";
            Assert.AreEqual("hello", data[0]);
            Assert.IsNull(data[1]);
        }
    }
}
