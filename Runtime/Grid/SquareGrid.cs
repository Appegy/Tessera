using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Finite square grid, 4-connected (no diagonals).
    ///     Cell <c>(x, y)</c> has id <c>y * Width + x</c> and occupies the rectangle
    ///     <c>[x*CellSize, (x+1)*CellSize] x [y*CellSize, (y+1)*CellSize]</c>.
    /// </summary>
    public sealed class SquareGrid : IGrid
    {
        public SquareGrid(int width, int height, float cellSize)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");

            Width = width;
            Height = height;
            CellSize = cellSize;
            CellCount = width * height;
            Bounds = new bounds2(float2.zero, new float2(width * cellSize, height * cellSize));
        }

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        public int CellCount { get; }
        public bounds2 Bounds { get; }

        public Cell GetCell(int id)
        {
            return new Cell(this, id);
        }

        public float2 GetCenter(int id)
        {
            var (x, y) = XYOf(id);
            return new float2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
        }

        public int GetCornersCount(int id)
        {
            return 4;
        }

        // Corners clockwise from top-right: 0=TR, 1=BR, 2=BL, 3=TL.
        public float2 GetCorner(int id, int cornerIndex)
        {
            var (x, y) = XYOf(id);
            var idx = (cornerIndex % 4 + 4) % 4;
            return idx switch
            {
                0 => new float2((x + 1) * CellSize, (y + 1) * CellSize),
                1 => new float2((x + 1) * CellSize, y * CellSize),
                2 => new float2(x * CellSize, y * CellSize),
                3 => new float2(x * CellSize, (y + 1) * CellSize),
                _ => default
            };
        }

        public void CopyCorners(int id, Span<float2> dest)
        {
            if (dest.Length < 4) throw new ArgumentException("dest must have length >= 4.", nameof(dest));
            var (x, y) = XYOf(id);
            var x0 = x * CellSize;
            var x1 = (x + 1) * CellSize;
            var y0 = y * CellSize;
            var y1 = (y + 1) * CellSize;
            dest[0] = new float2(x1, y1); // TR
            dest[1] = new float2(x1, y0); // BR
            dest[2] = new float2(x0, y0); // BL
            dest[3] = new float2(x0, y1); // TL
        }

        // Edge i (corner i -> corner (i+1)%4) is shared with neighbour i.
        // Edge 0: TR -> BR -> right neighbour  (x+1, y)
        // Edge 1: BR -> BL -> bottom neighbour (x, y-1)
        // Edge 2: BL -> TL -> left neighbour   (x-1, y)
        // Edge 3: TL -> TR -> top neighbour    (x, y+1)
        public int GetNeighbor(int id, int neighborIndex)
        {
            var (x, y) = XYOf(id);
            var idx = (neighborIndex % 4 + 4) % 4;
            int nx = x, ny = y;
            switch (idx)
            {
                case 0: nx = x + 1; break;
                case 1: ny = y - 1; break;
                case 2: nx = x - 1; break;
                case 3: ny = y + 1; break;
            }
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) return -1;
            return IdOf(nx, ny);
        }

        public bool AreNeighbors(int a, int b)
        {
            if (a == b) return false;
            if (a < 0 || a >= CellCount || b < 0 || b >= CellCount) return false;
            var (ax, ay) = XYOf(a);
            var (bx, by) = XYOf(b);
            return Math.Abs(ax - bx) + Math.Abs(ay - by) == 1;
        }

        public int GetNeighborIndex(int cell, int neighbor)
        {
            if (cell == neighbor) return -1;
            if (cell < 0 || cell >= CellCount || neighbor < 0 || neighbor >= CellCount) return -1;
            var (cx, cy) = XYOf(cell);
            var (nx, ny) = XYOf(neighbor);
            var dx = nx - cx;
            var dy = ny - cy;
            if (dx == 1 && dy == 0) return 0;
            if (dx == 0 && dy == -1) return 1;
            if (dx == -1 && dy == 0) return 2;
            if (dx == 0 && dy == 1) return 3;
            return -1;
        }

        public int GetCellAt(float2 point)
        {
            if (point.x < 0 || point.x >= Width * CellSize) return -1;
            if (point.y < 0 || point.y >= Height * CellSize) return -1;
            var x = (int)math.floor(point.x / CellSize);
            var y = (int)math.floor(point.y / CellSize);
            return IdOf(x, y);
        }

        public int Distance(int a, int b)
        {
            var (ax, ay) = XYOf(a);
            var (bx, by) = XYOf(b);
            return Math.Abs(ax - bx) + Math.Abs(ay - by);
        }

        public int IdOf(int x, int y)
        {
            return y * Width + x;
        }

        public (int X, int Y) XYOf(int id)
        {
            return (id % Width, id / Width);
        }
    }
}