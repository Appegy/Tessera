using System;
using System.Collections;
using System.Collections.Generic;

namespace Appegy.Tessera
{
    public sealed class CellMap<T> : IReadOnlyCollection<T>
    {
        private readonly T[] _data;

        public ITessellation Tessellation { get; }

        public int Count => Tessellation.CellCount;

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
