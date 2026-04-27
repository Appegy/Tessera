using System;
using System.Collections;
using System.Collections.Generic;

namespace Appegy.Tessera
{
    /// <summary>
    /// Generic 2D grid collection backed by a flat array with tessellation-aware metadata.
    /// Coordinates use (X, Y) where X is column and Y is row.
    /// </summary>
    public class TesseraGrid<T> : IReadOnlyCollection<T>, IEnumerable<T>
    {
        private readonly T[] _data;

        /// <summary>Tessellation geometry associated with this grid.</summary>
        public Tessellation Tessellation { get; }

        /// <summary>Number of columns (X dimension).</summary>
        public int Width { get; }

        /// <summary>Number of rows (Y dimension).</summary>
        public int Height { get; }

        /// <summary>Total number of cells (Width * Height).</summary>
        public int Count => Width * Height;

        /// <summary>Creates an empty grid with default values for all cells.</summary>
        public TesseraGrid(Tessellation tessellation, int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");

            Tessellation = tessellation;
            Width = width;
            Height = height;
            _data = new T[width * height];
        }

        /// <summary>Creates a grid from a 2D array where data[x, y] (dimension 0 = width, dimension 1 = height).</summary>
        public TesseraGrid(Tessellation tessellation, T[,] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Tessellation = tessellation;
            Width = data.GetLength(0);
            Height = data.GetLength(1);
            _data = new T[Width * Height];

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _data[y * Width + x] = data[x, y];
                }
            }
        }

        /// <summary>Gets or sets the value at (x, y). Throws IndexOutOfRangeException if out of bounds.</summary>
        public T this[int x, int y]
        {
            get
            {
                ValidateBounds(x, y);
                return _data[y * Width + x];
            }
            set
            {
                ValidateBounds(x, y);
                _data[y * Width + x] = value;
            }
        }

        /// <summary>Returns true if (x, y) is within grid bounds.</summary>
        public bool Contains(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>Sets all cells to the specified value.</summary>
        public void Fill(T value)
        {
            Array.Fill(_data, value);
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

        private void ValidateBounds(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new IndexOutOfRangeException($"Cell ({x}, {y}) is outside grid bounds ({Width}x{Height})");
        }
    }
}
