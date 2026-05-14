using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Irregular cell grid based on a centroidal Voronoi tessellation clipped to a rectangular bounds.
    ///     See <c>Documentation~/voronoi-grid-design.md</c>.
    /// </summary>
    public sealed class VoronoiGrid : ITessellation
    {
        private readonly Bounds2 _bounds;
        private readonly float2[] _centers;
        private readonly float2[][] _corners;
        private readonly int[][] _neighbors;

        public VoronoiGrid(Bounds2 bounds, int cellCount, int seed, int relaxationIterations)
        {
            if (cellCount < 1) throw new ArgumentOutOfRangeException(nameof(cellCount));
            if (relaxationIterations < 0) throw new ArgumentOutOfRangeException(nameof(relaxationIterations));
            if (bounds.Size.x <= 0 || bounds.Size.y <= 0)
            {
                throw new ArgumentException("Bounds must have positive size.", nameof(bounds));
            }

            var r = VoronoiBuilder.Build(bounds, cellCount, seed, relaxationIterations);
            _bounds = bounds;
            _centers = r.Centers;
            _corners = r.Corners;
            _neighbors = r.Neighbors;
        }

        public int CellCount => _centers.Length;
        public Bounds2 Bounds => _bounds;

        public float2 GetCenter(int id) => _centers[id];
        public int GetCornersCount(int id) => _corners[id].Length;

        public float2 GetCorner(int id, int cornerIndex)
        {
            var arr = _corners[id];
            var n = arr.Length;
            var idx = (cornerIndex % n + n) % n;
            return arr[idx];
        }

        public void CopyCorners(int id, Span<float2> dest)
        {
            var arr = _corners[id];
            if (dest.Length < arr.Length)
            {
                throw new ArgumentException($"dest must have length >= {arr.Length}.", nameof(dest));
            }
            for (var i = 0; i < arr.Length; i++) dest[i] = arr[i];
        }

        public int GetNeighborCount(int id) => _neighbors[id].Length;

        // GetNeighborStartCorner(id, j) == j: VoronoiGrid is polygonal, so corners and neighbour slots align.
        public int GetNeighborStartCorner(int id, int neighborIndex)
        {
            var n = _neighbors[id].Length;
            return (neighborIndex % n + n) % n;
        }

        public int GetNeighbor(int id, int neighborIndex)
        {
            var arr = _neighbors[id];
            var n = arr.Length;
            var idx = (neighborIndex % n + n) % n;
            return arr[idx];
        }

        public bool AreNeighbors(int a, int b)
        {
            if (a < 0 || a >= CellCount || b < 0 || b >= CellCount) return false;
            if (a == b) return false;
            var arr = _neighbors[a];
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i] == b)
                {
                    return true;
                }
            }
            return false;
        }

        public int GetNeighborIndex(int cell, int neighbor)
        {
            if (cell < 0 || cell >= CellCount) return -1;
            var arr = _neighbors[cell];
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i] == neighbor)
                {
                    return i;
                }
            }
            return -1;
        }

        public int GetCellAt(float2 point)
        {
            if (!_bounds.Contains(point)) return -1;
            var bestId = 0;
            var bestSq = math.distancesq(_centers[0], point);
            for (var i = 1; i < _centers.Length; i++)
            {
                var d = math.distancesq(_centers[i], point);
                if (d < bestSq)
                {
                    bestSq = d;
                    bestId = i;
                }
            }
            return bestId;
        }

        public int Distance(int a, int b)
        {
            if (a == b) return 0;
            var visited = new bool[_centers.Length];
            var queue = new Queue<(int id, int dist)>();
            queue.Enqueue((a, 0));
            visited[a] = true;
            while (queue.Count > 0)
            {
                var (cur, d) = queue.Dequeue();
                var ns = _neighbors[cur];
                for (var i = 0; i < ns.Length; i++)
                {
                    var n = ns[i];
                    if (n == -1 || visited[n]) continue;
                    if (n == b) return d + 1;
                    visited[n] = true;
                    queue.Enqueue((n, d + 1));
                }
            }
            // Defensive guard. The dual graph of a Voronoi tessellation clipped to a
            // convex Bounds2 is connected by construction (every cell shares at least one
            // edge with another cell or with the bounds). Reaching this point implies
            // internal corruption of _neighbors and is not part of the public contract.
            throw new InvalidOperationException($"Cells {a} and {b} are in disconnected components.");
        }
    }
}