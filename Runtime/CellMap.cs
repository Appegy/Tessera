using System;
using System.Collections;
using System.Collections.Generic;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Per-cell data layer over an <see cref="ITessellation" />. Stores one <typeparamref name="T" /> per cell,
    ///     indexed by cell id. The tessellation itself is held by composition via <see cref="Tessellation" />.
    /// </summary>
    public sealed class CellMap<T> : IReadOnlyCollection<T>
    {
        private readonly T[] _data;

        /// <summary>Underlying tessellation topology / geometry.</summary>
        public ITessellation Tessellation { get; }

        /// <summary>Number of cells (== <c>Tessellation.CellCount</c>).</summary>
        public int Count
        {
            get { return Tessellation.CellCount; }
        }

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

        public CellMap(ITessellation tessellation)
        {
            Tessellation = tessellation ?? throw new ArgumentNullException(nameof(tessellation));
            _data = new T[tessellation.CellCount];
        }

        public CellMap(ITessellation tessellation, T fill) : this(tessellation)
        {
            Array.Fill(_data, fill);
        }

        /// <summary>
        ///     Creates a deep copy of <paramref name="source" />: per-cell data is duplicated, while the
        ///     underlying <see cref="Tessellation" /> reference is shared (tessellations are immutable).
        /// </summary>
        public CellMap(CellMap<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Tessellation = source.Tessellation;
            _data = (T[])source._data.Clone();
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _data.Length; i++)
            {
                yield return _data[i];
            }
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
            {
                throw new IndexOutOfRangeException($"Cell id {id} is outside [0, {_data.Length}).");
            }
        }
    }
}
