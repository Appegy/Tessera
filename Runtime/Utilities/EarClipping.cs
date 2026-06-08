using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    /// Ear-clipping triangulation of a simple clockwise (Y-up) polygon. Collapses near-collinear and
    /// duplicate vertices first, then triangulates the rest, so concave cells (inward-poking tabs)
    /// are handled. Triangle indices are appended to <paramref name="indices"/> offset by
    /// <paramref name="baseIndex"/>; the caller's scratch arrays grow as needed.
    /// </summary>
    public static class EarClipping
    {
        public static void Triangulate(float2[] polygon, int count, List<int> indices, int baseIndex, ref int[] prev, ref int[] next)
        {
            if (count < 3) return;
            if (count == 3)
            {
                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
                return;
            }

            if (prev.Length < count) prev = new int[count];
            if (next.Length < count) next = new int[count];
            for (var i = 0; i < count; i++)
            {
                prev[i] = (i - 1 + count) % count;
                next[i] = (i + 1) % count;
            }

            var remaining = count;
            var head = 0;

            // Collapse near-collinear / coincident vertices so the survivors are all genuinely convex
            // or reflex (the dropped ones sit on the survivors' edges, so the filled area is unchanged).
            var changed = true;
            var passes = count;
            while (changed && remaining > 3 && passes-- > 0)
            {
                changed = false;
                var v = head;
                for (var s = remaining; s > 0 && remaining > 3; s--)
                {
                    var nv = next[v];
                    if (IsCollinear(polygon[prev[v]], polygon[v], polygon[next[v]]))
                    {
                        next[prev[v]] = next[v];
                        prev[next[v]] = prev[v];
                        if (v == head) head = next[v];
                        remaining--;
                        changed = true;
                    }
                    v = nv;
                }
            }

            var safety = remaining * 2;
            while (remaining > 3 && safety-- > 0)
            {
                var earFound = false;
                var v = head;
                for (var iter = 0; iter < remaining; iter++)
                {
                    if (IsEar(polygon, prev[v], v, next[v], next))
                    {
                        indices.Add(baseIndex + prev[v]);
                        indices.Add(baseIndex + v);
                        indices.Add(baseIndex + next[v]);
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
                indices.Add(baseIndex + prev[head]);
                indices.Add(baseIndex + head);
                indices.Add(baseIndex + next[head]);
            }
        }

        private static bool IsCollinear(float2 a, float2 b, float2 c)
        {
            var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            var eps = 1e-5f * (math.abs(b.x - a.x) + math.abs(b.y - a.y)) * (math.abs(c.x - b.x) + math.abs(c.y - b.y));
            return math.abs(cross) <= eps;
        }

        private static bool IsEar(float2[] poly, int pi, int ci, int ni, int[] next)
        {
            var a = poly[pi];
            var b = poly[ci];
            var c = poly[ni];

            // CW input (Y-up): an ear apex must be strictly convex, i.e. cross(ab, bc) < 0.
            if ((b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x) >= 0f) return false;

            for (var v = next[ni]; v != pi; v = next[v])
            {
                if (PointInTriangle(poly[v], a, b, c)) return false;
            }
            return true;
        }

        private static bool PointInTriangle(float2 p, float2 a, float2 b, float2 c)
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
