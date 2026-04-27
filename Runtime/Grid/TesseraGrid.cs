using System;
using System.Collections;
using System.Collections.Generic;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Per-cell data layer over an <see cref="IGrid" />. Stores one <typeparamref name="T" /> per cell,
    ///     indexed by cell id (or <see cref="Cell" />). The grid itself is held by composition via <see cref="Grid" />.
    /// </summary>
    public sealed class TesseraGrid<T> : IReadOnlyCollection<T>
    {
        private readonly T[] _data;

        public TesseraGrid(IGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            Grid = grid;
            _data = new T[grid.CellCount];
        }

        public TesseraGrid(IGrid grid, T fill) : this(grid)
        {
            Array.Fill(_data, fill);
        }

        /// <summary>Creates a grid initialised from <paramref name="data" />. Source array is copied.</summary>
        public TesseraGrid(IGrid grid, T[] data)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length != grid.CellCount)
                throw new ArgumentException(
                    $"data length ({data.Length}) must equal grid.CellCount ({grid.CellCount}).",
                    nameof(data));
            Grid = grid;
            _data = (T[])data.Clone();
        }

        /// <summary>Underlying grid topology / geometry.</summary>
        public IGrid Grid { get; }

        public T this[int id]
        {
            get
            {
                ValidateId(id);
                return _data[id];
            }
            set
            {
                ValidateId(id);
                _data[id] = value;
            }
        }

        public T this[Cell cell]
        {
            get => this[cell.Id];
            set => this[cell.Id] = value;
        }

        /// <summary>Number of cells (== <c>Grid.CellCount</c>).</summary>
        public int Count => Grid.CellCount;

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _data.Length; i++)
                yield return _data[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Fill(T value)
        {
            Array.Fill(_data, value);
        }

        private void ValidateId(int id)
        {
            if (id < 0 || id >= _data.Length)
                throw new IndexOutOfRangeException($"Cell id {id} is outside [0, {_data.Length}).");
        }
    }
}