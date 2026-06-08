using System.Collections.Generic;
using UnityEngine;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Builds a mitered triangle strip for a cell outline. Cell fills are triangulated with the
    ///     shared <see cref="EarClipping" />. Stateless; working buffers are passed in by the caller.
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
    }
}