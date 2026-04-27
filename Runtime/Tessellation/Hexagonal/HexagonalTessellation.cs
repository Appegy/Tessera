using System;
using System.Collections.Generic;

namespace Appegy.Tessera
{
    public readonly struct HexagonalTessellation : ITessellation
    {
        private static readonly float Sqrt3 = (float)Math.Sqrt(3);

        private static readonly int[] AllDirections = { 0, 1, 2, 3, 4, 5 };
        private static readonly int[] EvenDirections = { 0, 2, 4 };
        private static readonly int[] OddDirections = { 1, 3, 5 };

        private readonly float _inscribedRadius;
        private readonly HexagonalGridType _gridType;
        private readonly HexNeighborMode _neighborMode;
        private readonly float _describedRadius;

        public HexagonalTessellation(float inscribedRadius, HexagonalGridType gridType, HexNeighborMode neighborMode = HexNeighborMode.All)
        {
            _inscribedRadius = inscribedRadius;
            _gridType = gridType;
            _neighborMode = neighborMode;
            _describedRadius = (float)(inscribedRadius / Math.Cos(Math.PI / 6));
        }

        private int[] ActiveDirections => _neighborMode switch
        {
            HexNeighborMode.Even => EvenDirections,
            HexNeighborMode.Odd => OddDirections,
            _ => AllDirections
        };

        public int DirectionsCount => ActiveDirections.Length;
        public int CornersCount => 6;

        private float Side => _describedRadius;

        private bool IsPointy => _gridType == HexagonalGridType.PointyOdd || _gridType == HexagonalGridType.PointyEven;

        #region Offset <-> Cubic conversions

        private (int X, int Y, int Z) OffsetToCubic(int ox, int oy)
        {
            int x, y, z;
            switch (_gridType)
            {
                case HexagonalGridType.PointyOdd:
                    x = ox - (oy - (oy & 1)) / 2;
                    z = oy;
                    y = -x - z;
                    break;
                case HexagonalGridType.PointyEven:
                    x = ox - (oy + (oy & 1)) / 2;
                    z = oy;
                    y = -x - z;
                    break;
                case HexagonalGridType.FlatOdd:
                    x = ox;
                    z = oy - (ox - (ox & 1)) / 2;
                    y = -x - z;
                    break;
                case HexagonalGridType.FlatEven:
                    x = ox;
                    z = oy - (ox + (ox & 1)) / 2;
                    y = -x - z;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return (x, y, z);
        }

        private (int X, int Y) CubicToOffset(int cx, int cy, int cz)
        {
            switch (_gridType)
            {
                case HexagonalGridType.PointyOdd:
                    return (cx + (cz - (cz & 1)) / 2, cz);
                case HexagonalGridType.PointyEven:
                    return (cx + (cz + (cz & 1)) / 2, cz);
                case HexagonalGridType.FlatOdd:
                    return (cx, cz + (cx - (cx & 1)) / 2);
                case HexagonalGridType.FlatEven:
                    return (cx, cz + (cx + (cx & 1)) / 2);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Neighbors

        // Pointy-top: 0=TR, 1=R, 2=BR, 3=BL, 4=L, 5=TL (clockwise from top-right)
        // Odd rows shifted right
        private static readonly (int dx, int dy)[] PointyOddEvenRow =
        {
            (1, 0), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1)
        };
        private static readonly (int dx, int dy)[] PointyOddOddRow =
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (0, 1), (1, 1)
        };

        // PointyEven
        private static readonly (int dx, int dy)[] PointyEvenEvenRow =
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (0, 1), (1, 1)
        };
        private static readonly (int dx, int dy)[] PointyEvenOddRow =
        {
            (1, 0), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1)
        };

        // Flat-top: 0=T, 1=TR, 2=BR, 3=B, 4=BL, 5=TL (clockwise from top)
        // Odd columns shifted down
        private static readonly (int dx, int dy)[] FlatOddEvenCol =
        {
            (0, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0)
        };
        private static readonly (int dx, int dy)[] FlatOddOddCol =
        {
            (0, 1), (1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1)
        };

        // FlatEven
        private static readonly (int dx, int dy)[] FlatEvenEvenCol =
        {
            (0, 1), (1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1)
        };
        private static readonly (int dx, int dy)[] FlatEvenOddCol =
        {
            (0, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0)
        };

        private (int dx, int dy)[] GetNeighborOffsets(int ox, int oy)
        {
            switch (_gridType)
            {
                case HexagonalGridType.PointyOdd:
                    return Math.Abs(oy % 2) == 0 ? PointyOddEvenRow : PointyOddOddRow;
                case HexagonalGridType.PointyEven:
                    return Math.Abs(oy % 2) == 1 ? PointyEvenOddRow : PointyEvenEvenRow;
                case HexagonalGridType.FlatOdd:
                    return Math.Abs(ox % 2) == 0 ? FlatOddEvenCol : FlatOddOddCol;
                case HexagonalGridType.FlatEven:
                    return Math.Abs(ox % 2) == 1 ? FlatEvenOddCol : FlatEvenEvenCol;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        private static int NormalizeIndex(int index, int count)
        {
            index = index % count;
            if (index < 0) index += count;
            return index;
        }

        public (int X, int Y) GetNeighbor((int X, int Y) cell, int direction)
        {
            var active = ActiveDirections;
            var idx = NormalizeIndex(direction, active.Length);
            var physicalDir = active[idx];
            var offsets = GetNeighborOffsets(cell.X, cell.Y);
            var d = offsets[physicalDir];
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
            var count = DirectionsCount;
            for (var i = 0; i < count; i++)
            {
                var n = GetNeighbor(a, i);
                if (n.X == b.X && n.Y == b.Y) return true;
            }
            return false;
        }

        public (float X, float Y) ToPoint2((int X, int Y) cell)
        {
            var cubic = OffsetToCubic(cell.X, cell.Y);
            // axial: q = cubic.X, r = cubic.Z
            var q = cubic.X;
            var r = cubic.Z;

            if (IsPointy)
            {
                var x = Side * (Sqrt3 * q + Sqrt3 / 2f * r);
                var y = Side * (1.5f * r);
                return (x, y);
            }
            else
            {
                var x = Side * (1.5f * q);
                var y = Side * (Sqrt3 / 2f * q + Sqrt3 * r);
                return (x, y);
            }
        }

        public (int X, int Y) ToCell(float x, float y)
        {
            float fq, fr;
            if (IsPointy)
            {
                fq = (x * Sqrt3 / 3f - y / 3f) / Side;
                fr = y * 2f / 3f / Side;
            }
            else
            {
                fq = x * 2f / 3f / Side;
                fr = (-x / 3f + Sqrt3 / 3f * y) / Side;
            }

            // Cube round
            var fs = -fq - fr;
            var rq = (int)Math.Round(fq);
            var rr = (int)Math.Round(fr);
            var rs = (int)Math.Round(fs);

            var qDiff = Math.Abs(rq - fq);
            var rDiff = Math.Abs(rr - fr);
            var sDiff = Math.Abs(rs - fs);

            if (qDiff > rDiff && qDiff > sDiff)
                rq = -rr - rs;
            else if (rDiff > sDiff)
                rr = -rq - rs;
            // else rs = -rq - rr; (not needed)

            return CubicToOffset(rq, -rq - rr, rr);
        }

        public (float X, float Y) GetCornerPoint((int X, int Y) cell, int cornerIndex)
        {
            var idx = NormalizeIndex(cornerIndex, 6);
            var angleDeg = 60 * idx;
            if (IsPointy)
                angleDeg -= 30;

            var center = ToPoint2(cell);
            var angleRad = Math.PI / 180.0 * angleDeg;
            var cx = (float)(center.X + _describedRadius * Math.Cos(angleRad));
            var cy = (float)(center.Y + _describedRadius * Math.Sin(angleRad));
            return (cx, cy);
        }

        public int Distance((int X, int Y) a, (int X, int Y) b)
        {
            var ca = OffsetToCubic(a.X, a.Y);
            var cb = OffsetToCubic(b.X, b.Y);
            return (Math.Abs(ca.X - cb.X) + Math.Abs(ca.Y - cb.Y) + Math.Abs(ca.Z - cb.Z)) / 2;
        }

        public int GetDirection((int X, int Y) cell, (int X, int Y) neighbor)
        {
            var active = ActiveDirections;
            for (var i = 0; i < active.Length; i++)
            {
                var n = GetNeighbor(cell, i);
                if (n.X == neighbor.X && n.Y == neighbor.Y)
                    return i;
            }
            throw new ArgumentException($"Cells ({cell.X},{cell.Y}) and ({neighbor.X},{neighbor.Y}) are not neighbors.");
        }

        public int GetOppositeDirection(int direction)
        {
            var active = ActiveDirections;
            var idx = NormalizeIndex(direction, active.Length);
            var physicalDir = active[idx];
            return (physicalDir + 3) % 6;
        }
    }
}
