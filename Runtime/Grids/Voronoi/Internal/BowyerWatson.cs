using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class BowyerWatson
    {
        public static int[] Triangulate(ReadOnlySpan<float2> points)
        {
            if (points.Length < 3)
                throw new InvalidOperationException("Need at least 3 points to triangulate.");

            // Bounding box
            float minX = points[0].x, minY = points[0].y;
            float maxX = minX, maxY = minY;
            for (var i = 1; i < points.Length; i++)
            {
                var p = points[i];
                if (p.x < minX) minX = p.x;
                else if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                else if (p.y > maxY) maxY = p.y;
            }

            var dmax = math.max(maxX - minX, maxY - minY);
            if (dmax <= 0f) throw new InvalidOperationException("Degenerate point set (zero extent).");
            var midx = (minX + maxX) * 0.5f;
            var midy = (minY + maxY) * 0.5f;

            // Super triangle, vertices at indices points.Length, +1, +2 in the extended array.
            var ext = new float2[points.Length + 3];
            points.CopyTo(ext);
            ext[points.Length] = new float2(midx - 20f * dmax, midy - dmax);
            ext[points.Length + 1] = new float2(midx, midy + 20f * dmax);
            ext[points.Length + 2] = new float2(midx + 20f * dmax, midy - dmax);

            // Triangle list, 3 ints per triangle.
            var tris = new List<int>(points.Length * 6);
            tris.Add(points.Length);
            tris.Add(points.Length + 1);
            tris.Add(points.Length + 2);

            var edges = new List<int>(); // pairs (a, b)

            for (var pi = 0; pi < points.Length; pi++)
            {
                edges.Clear();

                // Find bad triangles whose circumcircle contains the new point.
                for (var ti = tris.Count - 3; ti >= 0; ti -= 3)
                {
                    var a = tris[ti];
                    var b = tris[ti + 1];
                    var c = tris[ti + 2];
                    if (InCircumcircle(ext[a], ext[b], ext[c], ext[pi]))
                    {
                        edges.Add(a); edges.Add(b);
                        edges.Add(b); edges.Add(c);
                        edges.Add(c); edges.Add(a);
                        tris.RemoveAt(ti + 2);
                        tris.RemoveAt(ti + 1);
                        tris.RemoveAt(ti);
                    }
                }

                // Mark duplicate edges (shared between two bad triangles) for removal.
                for (var i = 0; i < edges.Count; i += 2)
                {
                    if (edges[i] == -1) continue;
                    for (var j = i + 2; j < edges.Count; j += 2)
                    {
                        if (edges[j] == -1) continue;
                        if ((edges[i] == edges[j] && edges[i + 1] == edges[j + 1]) ||
                            (edges[i] == edges[j + 1] && edges[i + 1] == edges[j]))
                        {
                            edges[i] = -1; edges[i + 1] = -1;
                            edges[j] = -1; edges[j + 1] = -1;
                            break;
                        }
                    }
                }

                // Connect remaining edges to the new point.
                for (var i = 0; i < edges.Count; i += 2)
                {
                    if (edges[i] == -1) continue;
                    tris.Add(edges[i]);
                    tris.Add(edges[i + 1]);
                    tris.Add(pi);
                }
            }

            // Drop triangles touching the super-triangle vertices.
            var result = new List<int>(tris.Count);
            for (var ti = 0; ti < tris.Count; ti += 3)
            {
                var a = tris[ti];
                var b = tris[ti + 1];
                var c = tris[ti + 2];
                if (a >= points.Length || b >= points.Length || c >= points.Length) continue;
                result.Add(a);
                result.Add(b);
                result.Add(c);
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Bowyer-Watson produced no triangles.");

            return result.ToArray();
        }

        public static float2 Circumcenter(float2 a, float2 b, float2 c)
        {
            var d = 2f * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));
            if (math.abs(d) < 1e-12f)
                throw new InvalidOperationException("Degenerate triangle: collinear vertices.");
            var asq = a.x * a.x + a.y * a.y;
            var bsq = b.x * b.x + b.y * b.y;
            var csq = c.x * c.x + c.y * c.y;
            var ux = (asq * (b.y - c.y) + bsq * (c.y - a.y) + csq * (a.y - b.y)) / d;
            var uy = (asq * (c.x - b.x) + bsq * (a.x - c.x) + csq * (b.x - a.x)) / d;
            return new float2(ux, uy);
        }

        private static bool InCircumcircle(float2 a, float2 b, float2 c, float2 p)
        {
            // Sign-aware in-circle test independent of winding.
            var ax = a.x - p.x; var ay = a.y - p.y;
            var bx = b.x - p.x; var by = b.y - p.y;
            var cx = c.x - p.x; var cy = c.y - p.y;
            var det = (ax * ax + ay * ay) * (bx * cy - cx * by)
                    - (bx * bx + by * by) * (ax * cy - cx * ay)
                    + (cx * cx + cy * cy) * (ax * by - bx * ay);
            var orient = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            return orient > 0 ? det > 0 : det < 0;
        }
    }
}
