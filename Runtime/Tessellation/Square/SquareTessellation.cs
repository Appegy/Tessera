using System;
using System.Collections.Generic;

namespace Appegy.Lattice
{
    public readonly struct SquareTessellation : ITessellation
    {
        private readonly float _inscribedRadius;
        private readonly bool _includeDiagonals;

        public SquareTessellation(float inscribedRadius, bool includeDiagonals)
        {
            _inscribedRadius = inscribedRadius;
            _includeDiagonals = includeDiagonals;
        }

        public int DirectionsCount => _includeDiagonals ? 8 : 4;
        public int CornersCount => 4;

        // 4-dir: 0=top, 1=right, 2=bottom, 3=left
        private static readonly (int dx, int dy)[] Offsets4 =
        {
            (0, 1), (1, 0), (0, -1), (-1, 0)
        };

        // 8-dir: 0=top, 1=top-right, 2=right, 3=bottom-right, 4=bottom, 5=bottom-left, 6=left, 7=top-left
        private static readonly (int dx, int dy)[] Offsets8 =
        {
            (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)
        };

        public (int X, int Y) GetNeighbor((int X, int Y) cell, int direction)
        {
            var offsets = _includeDiagonals ? Offsets8 : Offsets4;
            var count = offsets.Length;
            var idx = ((direction % count) + count) % count;
            var d = offsets[idx];
            return (cell.X + d.dx, cell.Y + d.dy);
        }

        public IEnumerable<(int X, int Y)> GetNeighbors((int X, int Y) cell)
        {
            var count = DirectionsCount;
            for (var i = 0; i < count; i++)
            {
                yield return GetNeighbor(cell, i);
            }
        }

        public bool AreNeighbors((int X, int Y) a, (int X, int Y) b)
        {
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            if (dx == 0 && dy == 0) return false;
            if (_includeDiagonals)
                return dx <= 1 && dy <= 1;
            return (dx + dy) == 1;
        }

        public (float X, float Y) ToPoint2((int X, int Y) cell)
        {
            var side = _inscribedRadius * 2;
            return (cell.X * side, cell.Y * side);
        }

        public (int X, int Y) ToCell(float x, float y)
        {
            var side = _inscribedRadius * 2;
            return (
                (int)Math.Floor(x / side + 0.5f),
                (int)Math.Floor(y / side + 0.5f)
            );
        }

        // Corners clockwise from top-right: 0=TR, 1=BR, 2=BL, 3=TL
        public (float X, float Y) GetCornerPoint((int X, int Y) cell, int cornerIndex)
        {
            var idx = ((cornerIndex % 4) + 4) % 4;
            var center = ToPoint2(cell);
            var r = _inscribedRadius;
            switch (idx)
            {
                case 0: return (center.X + r, center.Y + r);
                case 1: return (center.X + r, center.Y - r);
                case 2: return (center.X - r, center.Y - r);
                case 3: return (center.X - r, center.Y + r);
                default: return center; // unreachable
            }
        }

        public int Distance((int X, int Y) a, (int X, int Y) b)
        {
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            if (_includeDiagonals)
                return Math.Max(dx, dy); // Chebyshev
            return dx + dy; // Manhattan
        }

        public int GetDirection((int X, int Y) cell, (int X, int Y) neighbor)
        {
            var count = DirectionsCount;
            for (var i = 0; i < count; i++)
            {
                var n = GetNeighbor(cell, i);
                if (n.X == neighbor.X && n.Y == neighbor.Y)
                    return i;
            }
            throw new ArgumentException($"Cells ({cell.X},{cell.Y}) and ({neighbor.X},{neighbor.Y}) are not neighbors.");
        }

        public int GetOppositeDirection(int direction)
        {
            var count = DirectionsCount;
            var idx = ((direction % count) + count) % count;
            return (idx + count / 2) % count;
        }
    }
}
