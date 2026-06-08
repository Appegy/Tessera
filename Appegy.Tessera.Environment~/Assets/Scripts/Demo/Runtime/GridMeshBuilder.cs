using System.Collections.Generic;
using UnityEngine;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Pure geometry helpers that turn an <see cref="ITessellation" /> into renderable meshes:
    ///     a mitered triangle strip per cell outline and an ear-clipping triangulation for cell fills.
    ///     Stateless; all working buffers are passed in so callers can pool them across rebuilds.
    /// </summary>
    public static class GridMeshBuilder
    {
        /// <summary>
        ///     Appends one cell's closed outline as a mitered triangle strip. Vertices are emitted in
        ///     grid space shifted by <paramref name="center" />, so a grid centred on its bounds ends up
        ///     centred at the origin.
        /// </summary>
        public static void AppendCellOutline(
            ITessellation grid, int id, Vector2 center, float halfWidth, Color color,
            List<Vector3> vertices, List<int> indices, List<Color> colors)
        {
            var n = grid.GetCornersCount(id);
            if (n < 2) return;

            var baseIdx = vertices.Count;
            for (var i = 0; i < n; i++)
            {
                var prev = grid.GetCorner(id, (i - 1 + n) % n);
                var curr = grid.GetCorner(id, i);
                var next = grid.GetCorner(id, (i + 1) % n);

                var d1x = curr.x - prev.x;
                var d1y = curr.y - prev.y;
                var d1len = Mathf.Sqrt(d1x * d1x + d1y * d1y);
                if (d1len > 0f) { d1x /= d1len; d1y /= d1len; }

                var d2x = next.x - curr.x;
                var d2y = next.y - curr.y;
                var d2len = Mathf.Sqrt(d2x * d2x + d2y * d2y);
                if (d2len > 0f) { d2x /= d2len; d2y /= d2len; }

                // Outward perpendicular for a CW polygon in Y-up: rotate the direction -90 deg.
                var n1x = d1y;
                var n1y = -d1x;
                var n2x = d2y;
                var n2y = -d2x;

                // Miter = halfWidth * (n1 + n2) / (1 + dot(n1, n2)). Denominator capped to avoid
                // spikes at near-180deg turns.
                var sumX = n1x + n2x;
                var sumY = n1y + n2y;
                var dot = n1x * n2x + n1y * n2y;
                var denom = 1f + dot;
                if (denom < 0.05f) denom = 0.05f;
                var mx = sumX * (halfWidth / denom);
                var my = sumY * (halfWidth / denom);

                var ox = curr.x + mx - center.x;
                var oy = curr.y + my - center.y;
                var ix = curr.x - mx - center.x;
                var iy = curr.y - my - center.y;

                vertices.Add(new Vector3(ox, oy, 0f));
                vertices.Add(new Vector3(ix, iy, 0f));
                colors.Add(color);
                colors.Add(color);
            }

            for (var i = 0; i < n; i++)
            {
                var a = baseIdx + i * 2;
                var b = baseIdx + i * 2 + 1;
                var c = baseIdx + (i + 1) % n * 2;
                var d = baseIdx + (i + 1) % n * 2 + 1;
                indices.Add(a);
                indices.Add(c);
                indices.Add(b);
                indices.Add(b);
                indices.Add(c);
                indices.Add(d);
            }
        }

        /// <summary>
        ///     Ear-clipping triangulation of a simple CW polygon (Y-up). Handles concave cells such as
        ///     puzzle pieces where neighbour tabs poke inward. O(n^2), fine for typical corner counts.
        ///     Grows the caller's prev/next scratch arrays as needed so it allocates nothing after warm-up.
        /// </summary>
        public static void Triangulate(Vector2[] poly, int n, List<int> indices, int baseIdx, ref int[] prevBuf, ref int[] nextBuf)
        {
            if (n < 3) return;
            if (n == 3)
            {
                indices.Add(baseIdx);
                indices.Add(baseIdx + 1);
                indices.Add(baseIdx + 2);
                return;
            }

            if (prevBuf.Length < n) prevBuf = new int[n];
            if (nextBuf.Length < n) nextBuf = new int[n];
            var prev = prevBuf;
            var next = nextBuf;
            for (var i = 0; i < n; i++)
            {
                prev[i] = (i - 1 + n) % n;
                next[i] = (i + 1) % n;
            }

            var remaining = n;
            var head = 0;
            var safety = n * 2;
            while (remaining > 3 && safety-- > 0)
            {
                var earFound = false;
                var v = head;
                for (var iter = 0; iter < remaining; iter++)
                {
                    if (IsEar(poly, prev[v], v, next[v], next))
                    {
                        indices.Add(baseIdx + prev[v]);
                        indices.Add(baseIdx + v);
                        indices.Add(baseIdx + next[v]);
                        next[prev[v]] = next[v];
                        prev[next[v]] = prev[v];
                        if (v == head) head = next[v];
                        remaining--;
                        earFound = true;
                        break;
                    }
                    v = next[v];
                }
                if (!earFound) break;
            }

            if (remaining == 3)
            {
                indices.Add(baseIdx + prev[head]);
                indices.Add(baseIdx + head);
                indices.Add(baseIdx + next[head]);
            }
        }

        private static bool IsEar(Vector2[] poly, int pi, int ci, int ni, int[] next)
        {
            var a = poly[pi];
            var b = poly[ci];
            var c = poly[ni];

            // Convex check for CW input (Y-up): reflex when cross(ab, bc) > 0. A scale-relative
            // tolerance treats near-collinear vertices (straight stems/shoulders, duplicates) as
            // zero-area ears, so they get clipped instead of stalling the algorithm.
            var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            var eps = 1e-5f * (Mathf.Abs(b.x - a.x) + Mathf.Abs(b.y - a.y)) * (Mathf.Abs(c.x - b.x) + Mathf.Abs(c.y - b.y));
            if (cross > eps) return false;     // reflex
            if (cross >= -eps) return true;    // ~collinear: zero-area ear, contains nothing

            var v = next[ni];
            while (v != pi)
            {
                if (PointInTriangle(poly[v], a, b, c)) return false;
                v = next[v];
            }
            return true;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            var d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            var d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            var hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}