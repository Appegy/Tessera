using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class PolygonClipping
    {
        private const int PlaneCount = 4;

        public static (float2[] corners, int[] neighbors) ClipToBounds(
            float2[] corners,
            int[] neighbors,
            Bounds2 bounds)
        {
            if (corners == null)
                throw new ArgumentNullException(nameof(corners));
            if (neighbors == null)
                throw new ArgumentNullException(nameof(neighbors));
            if (corners.Length != neighbors.Length)
                throw new ArgumentException("Corners and neighbors must have the same length.", nameof(neighbors));

            var inCorners = new List<float2>(corners);
            var inNeighbors = new List<int>(neighbors);
            var outCorners = new List<float2>(corners.Length + 4);
            var outNeighbors = new List<int>(neighbors.Length + 4);

            for (var plane = 0; plane < PlaneCount; plane++)
            {
                if (inCorners.Count == 0)
                    break;

                outCorners.Clear();
                outNeighbors.Clear();

                for (var i = 0; i < inCorners.Count; i++)
                {
                    var current = inCorners[i];
                    var next = inCorners[(i + 1) % inCorners.Count];
                    var tag = inNeighbors[i];
                    var currentInside = IsInside(current, plane, bounds);
                    var nextInside = IsInside(next, plane, bounds);
                    var currentOnPlane = IsOnPlane(current, plane, bounds);
                    var nextOnPlane = IsOnPlane(next, plane, bounds);

                    if (currentInside && nextInside)
                    {
                        outCorners.Add(current);
                        outNeighbors.Add(currentOnPlane && nextOnPlane ? -1 : tag);
                    }
                    else if (currentInside)
                    {
                        outCorners.Add(current);
                        if (currentOnPlane)
                        {
                            outNeighbors.Add(-1);
                        }
                        else
                        {
                            outNeighbors.Add(tag);
                            outCorners.Add(Intersect(current, next, plane, bounds));
                            outNeighbors.Add(-1);
                        }
                    }
                    else if (nextInside)
                    {
                        if (!nextOnPlane)
                        {
                            outCorners.Add(Intersect(current, next, plane, bounds));
                            outNeighbors.Add(tag);
                        }
                    }
                }

                var tempCorners = inCorners;
                inCorners = outCorners;
                outCorners = tempCorners;

                var tempNeighbors = inNeighbors;
                inNeighbors = outNeighbors;
                outNeighbors = tempNeighbors;
            }

            return (inCorners.ToArray(), inNeighbors.ToArray());
        }

        private static bool IsInside(float2 point, int plane, Bounds2 bounds)
        {
            return plane switch
            {
                0 => point.x >= bounds.Min.x,
                1 => point.x <= bounds.Max.x,
                2 => point.y >= bounds.Min.y,
                3 => point.y <= bounds.Max.y,
                _ => true
            };
        }

        private static bool IsOnPlane(float2 point, int plane, Bounds2 bounds)
        {
            return plane switch
            {
                0 => point.x == bounds.Min.x,
                1 => point.x == bounds.Max.x,
                2 => point.y == bounds.Min.y,
                3 => point.y == bounds.Max.y,
                _ => false
            };
        }

        private static float2 Intersect(float2 a, float2 b, int plane, Bounds2 bounds)
        {
            var delta = b - a;
            return plane switch
            {
                0 => new float2(bounds.Min.x, a.y + delta.y * (bounds.Min.x - a.x) / delta.x),
                1 => new float2(bounds.Max.x, a.y + delta.y * (bounds.Max.x - a.x) / delta.x),
                2 => new float2(a.x + delta.x * (bounds.Min.y - a.y) / delta.y, bounds.Min.y),
                3 => new float2(a.x + delta.x * (bounds.Max.y - a.y) / delta.y, bounds.Max.y),
                _ => a
            };
        }
    }
}
