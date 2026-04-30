using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Finite hexagonal grid, 6-connected. Layout (offset coordinate system) controlled by
    ///     <see cref="HexagonalGridType" />: pointy-top vs flat-top, with odd or even row/column shifted.
    ///     Cell <c>(x, y)</c> has id <c>y * Width + x</c>. Cell (0, 0) center sits at pixel (0, 0).
    ///     Note: <c>Bounds.Min</c> is not necessarily at the origin; consult <c>Bounds</c> directly.
    /// </summary>
    public sealed class HexagonalGrid : ITessellation
    {
        private static readonly float _sqrt3 = math.sqrt(3f);

        // Neighbor offsets in the new aligned ordering (CW; edge[i] shared with neighbor[i]).
        // Pointy: 0=R, 1=BR, 2=BL, 3=L, 4=TL, 5=TR.
        // Flat:   0=TR, 1=BR, 2=B, 3=BL, 4=TL, 5=T.
        // Two tables per orientation: A = unshifted row/col (or shifted-left in PointyEven), B = the other.
        private static readonly (int dx, int dy)[] _pointyA = { (+1, 0), (0, -1), (-1, -1), (-1, 0), (-1, +1), (0, +1) };
        private static readonly (int dx, int dy)[] _pointyB = { (+1, 0), (+1, -1), (0, -1), (-1, 0), (0, +1), (+1, +1) };
        private static readonly (int dx, int dy)[] _flatA = { (+1, 0), (+1, -1), (0, -1), (-1, -1), (-1, 0), (0, +1) };
        private static readonly (int dx, int dy)[] _flatB = { (+1, +1), (+1, 0), (0, -1), (-1, 0), (-1, +1), (0, +1) };

        private readonly float _describedRadius;
        private readonly bool _isPointy;

        public HexagonalGrid(int width, int height, float inscribedRadius, HexagonalGridType type)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            if (inscribedRadius <= 0) throw new ArgumentOutOfRangeException(nameof(inscribedRadius), "Inscribed radius must be positive.");

            Width = width;
            Height = height;
            InscribedRadius = inscribedRadius;
            Type = type;
            CellCount = width * height;

            _describedRadius = inscribedRadius / math.cos(math.PI / 6f); // = 2/sqrt(3) * inscribedRadius
            _isPointy = type is HexagonalGridType.PointyOdd or HexagonalGridType.PointyEven;

            Bounds = ComputeBoundsBySweep();
        }

        public int Width { get; }
        public int Height { get; }
        public float InscribedRadius { get; }
        public HexagonalGridType Type { get; }

        public int CellCount { get; }
        public Bounds2 Bounds { get; }

        public float2 GetCenter(int id)
        {
            var (x, y) = XYOf(id);
            return CenterOf(x, y);
        }

        public int GetCornersCount(int id)
        {
            return 6;
        }

        // Pointy: corner[i] at angle 30° - 60°·i (CW from TR corner at 30°).
        // Flat:   corner[i] at angle 60° - 60°·i (CW from TR corner at 60°).
        public float2 GetCorner(int id, int cornerIndex)
        {
            var idx = (cornerIndex % 6 + 6) % 6;
            var center = GetCenter(id);
            var baseDeg = _isPointy ? 30f : 60f;
            var angleRad = math.radians(baseDeg - 60f * idx);
            return center + _describedRadius * new float2(math.cos(angleRad), math.sin(angleRad));
        }

        public void CopyCorners(int id, Span<float2> dest)
        {
            if (dest.Length < 6) throw new ArgumentException("dest must have length >= 6.", nameof(dest));
            var center = GetCenter(id);
            var baseDeg = _isPointy ? 30f : 60f;
            for (var i = 0; i < 6; i++)
            {
                var angleRad = math.radians(baseDeg - 60f * i);
                dest[i] = center + _describedRadius * new float2(math.cos(angleRad), math.sin(angleRad));
            }
        }

        public int GetNeighbor(int id, int neighborIndex)
        {
            var (x, y) = XYOf(id);
            var idx = (neighborIndex % 6 + 6) % 6;
            var d = OffsetsFor(x, y)[idx];
            int nx = x + d.dx, ny = y + d.dy;
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) return -1;
            return IdOf(nx, ny);
        }

        public bool AreNeighbors(int a, int b)
        {
            if (a == b) return false;
            if (a < 0 || a >= CellCount || b < 0 || b >= CellCount) return false;
            var (ax, ay) = XYOf(a);
            var offsets = OffsetsFor(ax, ay);
            for (var i = 0; i < 6; i++)
            {
                int nx = ax + offsets[i].dx, ny = ay + offsets[i].dy;
                if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                if (IdOf(nx, ny) == b) return true;
            }
            return false;
        }

        public int GetNeighborIndex(int cell, int neighbor)
        {
            if (cell == neighbor) return -1;
            if (cell < 0 || cell >= CellCount || neighbor < 0 || neighbor >= CellCount) return -1;
            var (cx, cy) = XYOf(cell);
            var offsets = OffsetsFor(cx, cy);
            for (var i = 0; i < 6; i++)
            {
                int nx = cx + offsets[i].dx, ny = cy + offsets[i].dy;
                if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                if (IdOf(nx, ny) == neighbor) return i;
            }
            return -1;
        }

        public int GetCellAt(float2 point)
        {
            // Convert pixel to fractional cubic, round to nearest integer cubic, convert back to offset.
            float fq, fr;
            if (_isPointy)
            {
                fq = (point.x * _sqrt3 / 3f - point.y / 3f) / _describedRadius;
                fr = point.y * 2f / 3f / _describedRadius;
            }
            else
            {
                fq = point.x * 2f / 3f / _describedRadius;
                fr = (-point.x / 3f + _sqrt3 / 3f * point.y) / _describedRadius;
            }

            var fs = -fq - fr;
            var rq = (int)math.round(fq);
            var rr = (int)math.round(fr);
            var rs = (int)math.round(fs);

            var qDiff = math.abs(rq - fq);
            var rDiff = math.abs(rr - fr);
            var sDiff = math.abs(rs - fs);

            if (qDiff > rDiff && qDiff > sDiff)
            {
                rq = -rr - rs;
            }
            else if (rDiff > sDiff)
            {
                rr = -rq - rs;
            }
            // else rs is correct (no need to adjust)
            var (ox, oy) = CubicToOffset(rq, rr);
            if (ox < 0 || ox >= Width || oy < 0 || oy >= Height) return -1;
            return IdOf(ox, oy);
        }

        public int Distance(int a, int b)
        {
            var (ax, ay) = XYOf(a);
            var (bx, by) = XYOf(b);
            var (acx, acy, acz) = OffsetToCubic(ax, ay);
            var (bcx, bcy, bcz) = OffsetToCubic(bx, by);
            return (Math.Abs(acx - bcx) + Math.Abs(acy - bcy) + Math.Abs(acz - bcz)) / 2;
        }

        public int IdOf(int x, int y)
        {
            return y * Width + x;
        }

        public (int X, int Y) XYOf(int id)
        {
            return (id % Width, id / Width);
        }

        private float2 CenterOf(int x, int y)
        {
            if (_isPointy)
            {
                var oddRow = (y & 1) == 1;
                var xShift = oddRow
                    ? Type == HexagonalGridType.PointyOdd ? +InscribedRadius : -InscribedRadius
                    : 0f;
                return new float2(x * 2f * InscribedRadius + xShift, y * 1.5f * _describedRadius);
            }
            var oddCol = (x & 1) == 1;
            var yShift = oddCol
                ? Type == HexagonalGridType.FlatOdd ? +InscribedRadius : -InscribedRadius
                : 0f;
            return new float2(x * 1.5f * _describedRadius, y * 2f * InscribedRadius + yShift);
        }

        private (int dx, int dy)[] OffsetsFor(int x, int y)
        {
            if (_isPointy)
            {
                var oddRow = (y & 1) == 1;
                // PointyOdd: odd row → B (shifted right). Even row → A.
                // PointyEven: odd row → A (shifted left). Even row → B.
                return Type == HexagonalGridType.PointyOdd
                    ? oddRow ? _pointyB : _pointyA
                    : oddRow
                        ? _pointyA
                        : _pointyB;
            }
            var oddCol = (x & 1) == 1;
            return Type == HexagonalGridType.FlatOdd
                ? oddCol ? _flatB : _flatA
                : oddCol
                    ? _flatA
                    : _flatB;
        }

        private (int X, int Y, int Z) OffsetToCubic(int ox, int oy)
        {
            int x, y, z;
            switch (Type)
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

        private (int X, int Y) CubicToOffset(int cx, int cz)
        {
            switch (Type)
            {
                case HexagonalGridType.PointyOdd: return (cx + (cz - (cz & 1)) / 2, cz);
                case HexagonalGridType.PointyEven: return (cx + (cz + (cz & 1)) / 2, cz);
                case HexagonalGridType.FlatOdd: return (cx, cz + (cx - (cx & 1)) / 2);
                case HexagonalGridType.FlatEven: return (cx, cz + (cx + (cx & 1)) / 2);
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private Bounds2 ComputeBoundsBySweep()
        {
            var min = new float2(float.MaxValue);
            var max = new float2(float.MinValue);
            for (var id = 0; id < CellCount; id++)
            {
                for (var c = 0; c < 6; c++)
                {
                    var corner = GetCorner(id, c);
                    min = math.min(min, corner);
                    max = math.max(max, corner);
                }
            }
            return new Bounds2(min, max);
        }
    }
}