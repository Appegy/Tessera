using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class VoronoiBuilder
    {
        private const float MatchEpsilonSq = 1e-8f;
        private const float BoundsToleranceFactor = 1e-5f;

        internal readonly struct RawCells
        {
            public readonly float2[][] Corners;
            public readonly int[][] Neighbors;

            public RawCells(float2[][] corners, int[][] neighbors)
            {
                Corners = corners;
                Neighbors = neighbors;
            }
        }

        private readonly struct EdgeAdjacency
        {
            public readonly int A;
            public readonly int B;
            public readonly int T1;
            public readonly int T2;

            public EdgeAdjacency(int a, int b, int t1, int t2)
            {
                A = a;
                B = b;
                T1 = t1;
                T2 = t2;
            }

            public EdgeAdjacency WithSecondTriangle(int t)
            {
                return new EdgeAdjacency(A, B, T1, t);
            }
        }

        private readonly struct CellEdge
        {
            public readonly float2 From;
            public readonly float2 To;
            public readonly int Neighbor;

            public CellEdge(float2 from, float2 to, int neighbor)
            {
                From = from;
                To = to;
                Neighbor = neighbor;
            }
        }

        internal static RawCells ExtractRaw(ReadOnlySpan<float2> seeds, Bounds2 bounds)
        {
            if (seeds.Length < 3)
                throw new InvalidOperationException("Voronoi needs at least 3 seeds.");
            ValidateBounds(bounds);
            ValidateSeeds(seeds, bounds);

            var triangles = BowyerWatson.Triangulate(seeds);
            var triangleCount = triangles.Length / 3;
            var circumcenters = new float2[triangleCount];
            for (var t = 0; t < triangleCount; t++)
            {
                circumcenters[t] = BowyerWatson.Circumcenter(
                    seeds[triangles[3 * t]],
                    seeds[triangles[3 * t + 1]],
                    seeds[triangles[3 * t + 2]]);
            }

            var edgeMap = BuildEdgeMap(triangles, triangleCount);
            var cellEdges = new List<CellEdge>[seeds.Length];
            var hullFarPoints = new List<float2>[seeds.Length];
            for (var i = 0; i < seeds.Length; i++)
            {
                cellEdges[i] = new List<CellEdge>();
                hullFarPoints[i] = new List<float2>(2);
            }

            var farDistance = math.max(bounds.Size.x, bounds.Size.y) * 4f;
            foreach (var edge in edgeMap.Values)
            {
                if (edge.T2 >= 0)
                {
                    AddVoronoiEdge(cellEdges, edge.A, edge.B, circumcenters[edge.T1], circumcenters[edge.T2]);
                    continue;
                }

                var far = CreateHullFarPoint(seeds, triangles, circumcenters, edge, farDistance);
                AddVoronoiEdge(cellEdges, edge.A, edge.B, circumcenters[edge.T1], far);
                AddUnique(hullFarPoints[edge.A], far);
                AddUnique(hullFarPoints[edge.B], far);
            }

            for (var s = 0; s < seeds.Length; s++)
            {
                var farPoints = hullFarPoints[s];
                if (farPoints.Count >= 2)
                {
                    var seed = seeds[s];
                    farPoints.Sort((a, b) => AngleDescendingCompare(seed, a, b));
                    for (var i = 0; i < farPoints.Count; i++)
                        AddCellEdge(cellEdges[s], farPoints[i], farPoints[(i + 1) % farPoints.Count], -1);
                }
            }

            var corners = new float2[seeds.Length][];
            var neighbors = new int[seeds.Length][];
            for (var s = 0; s < seeds.Length; s++)
                BuildCell(seeds[s], cellEdges[s], out corners[s], out neighbors[s]);

            return new RawCells(corners, neighbors);
        }

        private static Dictionary<long, EdgeAdjacency> BuildEdgeMap(int[] triangles, int triangleCount)
        {
            var edgeMap = new Dictionary<long, EdgeAdjacency>(triangleCount * 3);
            for (var t = 0; t < triangleCount; t++)
            {
                AddDelaunayEdge(edgeMap, triangles[3 * t], triangles[3 * t + 1], t);
                AddDelaunayEdge(edgeMap, triangles[3 * t + 1], triangles[3 * t + 2], t);
                AddDelaunayEdge(edgeMap, triangles[3 * t + 2], triangles[3 * t], t);
            }

            return edgeMap;
        }

        private static void AddDelaunayEdge(Dictionary<long, EdgeAdjacency> edgeMap, int a, int b, int triangle)
        {
            var lo = math.min(a, b);
            var hi = math.max(a, b);
            var key = EdgeKey(lo, hi);
            if (edgeMap.TryGetValue(key, out var existing))
                edgeMap[key] = existing.WithSecondTriangle(triangle);
            else
                edgeMap.Add(key, new EdgeAdjacency(lo, hi, triangle, -1));
        }

        private static float2 CreateHullFarPoint(ReadOnlySpan<float2> seeds, int[] triangles, float2[] circumcenters, EdgeAdjacency edge, float farDistance)
        {
            var a = seeds[edge.A];
            var b = seeds[edge.B];
            var midpoint = (a + b) * 0.5f;
            var third = seeds[FindThirdVertex(triangles, edge.T1, edge.A, edge.B)];
            var delta = b - a;
            var outward = math.normalizesafe(new float2(-delta.y, delta.x));
            if (math.dot(third - midpoint, outward) > 0f)
                outward = -outward;

            var farFromMidpoint = midpoint + outward * farDistance;
            var farFromCircumcenter = circumcenters[edge.T1] + outward * farDistance;
            return math.distancesq(farFromMidpoint, midpoint) > math.distancesq(farFromCircumcenter, midpoint)
                ? farFromMidpoint
                : farFromCircumcenter;
        }

        private static int FindThirdVertex(int[] triangles, int triangle, int a, int b)
        {
            for (var i = 0; i < 3; i++)
            {
                var vertex = triangles[3 * triangle + i];
                if (vertex != a && vertex != b)
                    return vertex;
            }

            throw new InvalidOperationException("Delaunay edge does not belong to triangle.");
        }

        private static void AddVoronoiEdge(List<CellEdge>[] cellEdges, int a, int b, float2 from, float2 to)
        {
            AddCellEdge(cellEdges[a], from, to, b);
            AddCellEdge(cellEdges[b], from, to, a);
        }

        private static void AddCellEdge(List<CellEdge> edges, float2 from, float2 to, int neighbor)
        {
            if (SamePoint(from, to))
                return;

            edges.Add(new CellEdge(from, to, neighbor));
        }

        private static void BuildCell(float2 seed, List<CellEdge> edges, out float2[] corners, out int[] neighbors)
        {
            var uniqueCorners = new List<float2>(edges.Count);
            for (var i = 0; i < edges.Count; i++)
            {
                AddUnique(uniqueCorners, edges[i].From);
                AddUnique(uniqueCorners, edges[i].To);
            }

            uniqueCorners.Sort((a, b) => AngleDescendingCompare(seed, a, b));
            corners = uniqueCorners.ToArray();
            neighbors = new int[corners.Length];
            for (var i = 0; i < corners.Length; i++)
                neighbors[i] = GetEdgeNeighbor(edges, corners[i], corners[(i + 1) % corners.Length]);

            if (SignedArea(corners) > 0f)
            {
                Array.Reverse(corners);
                for (var i = 0; i < corners.Length; i++)
                    neighbors[i] = GetEdgeNeighbor(edges, corners[i], corners[(i + 1) % corners.Length]);
            }
        }

        private static int GetEdgeNeighbor(List<CellEdge> edges, float2 from, float2 to)
        {
            if (TryFindEdgeNeighbor(edges, from, to, out var neighbor))
                return neighbor;

            throw new InvalidOperationException("Voronoi raw cell has consecutive corners without a matching edge.");
        }

        private static bool TryFindEdgeNeighbor(List<CellEdge> edges, float2 from, float2 to, out int neighbor)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if ((SamePoint(edge.From, from) && SamePoint(edge.To, to)) ||
                    (SamePoint(edge.From, to) && SamePoint(edge.To, from)))
                {
                    neighbor = edge.Neighbor;
                    return true;
                }
            }

            neighbor = -1;
            return false;
        }

        private static void ValidateBounds(Bounds2 bounds)
        {
            var size = bounds.Size;
            if (!IsFinite(bounds.Min) || !IsFinite(bounds.Max) || !IsFinite(size) || size.x <= 0f || size.y <= 0f)
                throw new InvalidOperationException("Voronoi bounds must have positive finite size.");
        }

        private static void ValidateSeeds(ReadOnlySpan<float2> seeds, Bounds2 bounds)
        {
            var tolerance = math.max(bounds.Size.x, bounds.Size.y) * BoundsToleranceFactor;
            for (var i = 0; i < seeds.Length; i++)
            {
                if (!IsFinite(seeds[i]))
                    throw new InvalidOperationException("Voronoi seeds must be finite.");
                if (!Contains(bounds, seeds[i], tolerance))
                    throw new InvalidOperationException("Voronoi seeds must be inside bounds.");
            }
        }

        private static bool Contains(Bounds2 bounds, float2 point, float tolerance)
        {
            return point.x >= bounds.Min.x - tolerance && point.x <= bounds.Max.x + tolerance &&
                   point.y >= bounds.Min.y - tolerance && point.y <= bounds.Max.y + tolerance;
        }

        private static void AddUnique(List<float2> points, float2 point)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (SamePoint(points[i], point))
                    return;
            }

            points.Add(point);
        }

        private static int AngleDescendingCompare(float2 center, float2 a, float2 b)
        {
            var angleA = math.atan2(a.y - center.y, a.x - center.x);
            var angleB = math.atan2(b.y - center.y, b.x - center.x);
            return angleB.CompareTo(angleA);
        }

        private static float SignedArea(float2[] corners)
        {
            var area = 0f;
            for (var i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                var q = corners[(i + 1) % corners.Length];
                area += p.x * q.y - q.x * p.y;
            }

            return area * 0.5f;
        }

        private static bool SamePoint(float2 a, float2 b)
        {
            return math.distancesq(a, b) <= MatchEpsilonSq;
        }

        private static bool IsFinite(float2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static long EdgeKey(int lo, int hi)
        {
            return ((long)lo << 32) | (uint)hi;
        }
    }
}
