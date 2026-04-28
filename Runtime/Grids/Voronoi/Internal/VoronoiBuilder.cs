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

        internal readonly struct Result
        {
            public readonly float2[] Centers;
            public readonly float2[][] Corners;
            public readonly int[][] Neighbors;

            public Result(float2[] c, float2[][] cs, int[][] ns)
            {
                Centers = c;
                Corners = cs;
                Neighbors = ns;
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

        internal static Result Build(Bounds2 bounds, int cellCount, int seed, int relaxationIterations)
        {
            if (cellCount < 1)
                throw new ArgumentOutOfRangeException(nameof(cellCount));
            if (relaxationIterations < 0)
                throw new ArgumentOutOfRangeException(nameof(relaxationIterations));
            ValidateBounds(bounds);

            var seeds = SampleSeeds(bounds, cellCount, seed);
            if (cellCount < 3)
                return BuildSmall(bounds, seeds, seed, relaxationIterations);

            for (var iteration = 0; iteration < relaxationIterations; iteration++)
            {
                var raw = ExtractRaw(seeds, bounds);
                for (var i = 0; i < seeds.Length; i++)
                {
                    var clipped = PolygonClipping.ClipToBounds(raw.Corners[i], raw.Neighbors[i], bounds);
                    if (clipped.corners.Length >= 3)
                        seeds[i] = ClampToBounds(Centroid(clipped.corners), bounds);
                }
            }

            var finalRaw = ExtractRaw(seeds, bounds);
            var finalCorners = new float2[cellCount][];
            var finalNeighbors = new int[cellCount][];
            for (var i = 0; i < cellCount; i++)
            {
                var clipped = PolygonClipping.ClipToBounds(finalRaw.Corners[i], finalRaw.Neighbors[i], bounds);
                finalCorners[i] = clipped.corners;
                finalNeighbors[i] = clipped.neighbors;
            }

            ValidateResult(seeds, finalCorners, finalNeighbors, seed);
            return new Result(seeds, finalCorners, finalNeighbors);
        }

        private static float2[] SampleSeeds(Bounds2 bounds, int cellCount, int seed)
        {
            var random = new System.Random(seed);
            var seeds = new float2[cellCount];
            for (var i = 0; i < seeds.Length; i++)
            {
                seeds[i] = new float2(
                    bounds.Min.x + (float)random.NextDouble() * bounds.Size.x,
                    bounds.Min.y + (float)random.NextDouble() * bounds.Size.y);
            }

            return seeds;
        }

        private static Result BuildSmall(Bounds2 bounds, float2[] seeds, int seed, int relaxationIterations)
        {
            for (var iteration = 0; iteration < relaxationIterations; iteration++)
            {
                BuildSmallCells(bounds, seeds, out var corners, out _);
                for (var i = 0; i < seeds.Length; i++)
                {
                    if (corners[i].Length >= 3)
                        seeds[i] = ClampToBounds(Centroid(corners[i]), bounds);
                }
            }

            BuildSmallCells(bounds, seeds, out var finalCorners, out var finalNeighbors);
            ValidateResult(seeds, finalCorners, finalNeighbors, seed);
            return new Result(seeds, finalCorners, finalNeighbors);
        }

        private static void BuildSmallCells(Bounds2 bounds, float2[] seeds, out float2[][] corners, out int[][] neighbors)
        {
            corners = new float2[seeds.Length][];
            neighbors = new int[seeds.Length][];
            if (seeds.Length == 1)
            {
                corners[0] = BoundsCorners(bounds);
                neighbors[0] = BoundaryNeighbors();
                return;
            }

            for (var i = 0; i < seeds.Length; i++)
            {
                var other = 1 - i;
                var clipped = ClipToCloserSeed(BoundsCorners(bounds), BoundaryNeighbors(), seeds[i], seeds[other], other);
                corners[i] = clipped.corners;
                neighbors[i] = clipped.neighbors;
            }
        }

        private static float2[] BoundsCorners(Bounds2 bounds)
        {
            return new[]
            {
                new float2(bounds.Max.x, bounds.Max.y),
                new float2(bounds.Max.x, bounds.Min.y),
                new float2(bounds.Min.x, bounds.Min.y),
                new float2(bounds.Min.x, bounds.Max.y)
            };
        }

        private static int[] BoundaryNeighbors()
        {
            return new[] { -1, -1, -1, -1 };
        }

        private static (float2[] corners, int[] neighbors) ClipToCloserSeed(
            float2[] corners,
            int[] neighbors,
            float2 owner,
            float2 other,
            int otherIndex)
        {
            var outCorners = new List<float2>(corners.Length + 1);
            var outNeighbors = new List<int>(neighbors.Length + 1);
            for (var i = 0; i < corners.Length; i++)
            {
                var current = corners[i];
                var next = corners[(i + 1) % corners.Length];
                var currentInside = IsCloserOrEqual(current, owner, other);
                var nextInside = IsCloserOrEqual(next, owner, other);

                if (currentInside && nextInside)
                {
                    AddClippedVertex(outCorners, outNeighbors, current, neighbors[i]);
                }
                else if (currentInside)
                {
                    AddClippedVertex(outCorners, outNeighbors, current, neighbors[i]);
                    AddClippedVertex(outCorners, outNeighbors, IntersectBisector(current, next, owner, other), otherIndex);
                }
                else if (nextInside)
                {
                    AddClippedVertex(outCorners, outNeighbors, IntersectBisector(current, next, owner, other), neighbors[i]);
                }
            }

            if (outCorners.Count > 1 && SamePoint(outCorners[0], outCorners[outCorners.Count - 1]))
            {
                outNeighbors[0] = outNeighbors[outNeighbors.Count - 1];
                outCorners.RemoveAt(outCorners.Count - 1);
                outNeighbors.RemoveAt(outNeighbors.Count - 1);
            }

            return (outCorners.ToArray(), outNeighbors.ToArray());
        }

        private static void AddClippedVertex(List<float2> corners, List<int> neighbors, float2 corner, int neighbor)
        {
            if (corners.Count > 0 && SamePoint(corners[corners.Count - 1], corner))
            {
                neighbors[neighbors.Count - 1] = neighbor;
                return;
            }

            corners.Add(corner);
            neighbors.Add(neighbor);
        }

        private static bool IsCloserOrEqual(float2 point, float2 owner, float2 other)
        {
            return math.distancesq(point, owner) <= math.distancesq(point, other) + 1e-6f;
        }

        private static float2 IntersectBisector(float2 a, float2 b, float2 owner, float2 other)
        {
            var fa = math.distancesq(a, owner) - math.distancesq(a, other);
            var fb = math.distancesq(b, owner) - math.distancesq(b, other);
            var denom = fa - fb;
            if (math.abs(denom) < 1e-12f)
                return (a + b) * 0.5f;
            var t = fa / denom;
            return a + (b - a) * math.clamp(t, 0f, 1f);
        }

        private static float2 Centroid(float2[] polygon)
        {
            var twiceArea = 0f;
            var cx = 0f;
            var cy = 0f;
            for (var i = 0; i < polygon.Length; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Length];
                var cross = p.x * q.y - q.x * p.y;
                twiceArea += cross;
                cx += (p.x + q.x) * cross;
                cy += (p.y + q.y) * cross;
            }

            var area = twiceArea * 0.5f;
            if (math.abs(area) < 1e-12f)
            {
                var average = float2.zero;
                for (var i = 0; i < polygon.Length; i++)
                    average += polygon[i];
                return average / polygon.Length;
            }

            return new float2(cx / (6f * area), cy / (6f * area));
        }

        private static float2 ClampToBounds(float2 point, Bounds2 bounds)
        {
            return math.clamp(point, bounds.Min, bounds.Max);
        }

        private static void ValidateResult(float2[] centers, float2[][] corners, int[][] neighbors, int seed)
        {
            if (corners.Length != centers.Length || neighbors.Length != centers.Length)
                throw new InvalidOperationException($"Voronoi result count mismatch (seed={seed}).");

            for (var i = 0; i < centers.Length; i++)
            {
                if (corners[i].Length < 3)
                    throw new InvalidOperationException($"Voronoi cell {i} has fewer than 3 corners (seed={seed}).");
                if (corners[i].Length != neighbors[i].Length)
                    throw new InvalidOperationException($"Voronoi cell {i} corner/neighbor count mismatch (seed={seed}).");
            }

            for (var a = 0; a < neighbors.Length; a++)
            {
                for (var edge = 0; edge < neighbors[a].Length; edge++)
                {
                    var b = neighbors[a][edge];
                    if (b == -1)
                        continue;
                    if (b < 0 || b >= neighbors.Length)
                        throw new InvalidOperationException($"Voronoi cell {a} has out-of-range neighbor {b} (seed={seed}).");
                    if (!HasReversedNeighborEdge(corners, neighbors, a, edge, b))
                        throw new InvalidOperationException($"Voronoi neighbor symmetry failed for {a}->{b} (seed={seed}).");
                }
            }
        }

        private static bool HasReversedNeighborEdge(float2[][] corners, int[][] neighbors, int a, int edge, int b)
        {
            var from = corners[a][edge];
            var to = corners[a][(edge + 1) % corners[a].Length];
            for (var otherEdge = 0; otherEdge < neighbors[b].Length; otherEdge++)
            {
                if (neighbors[b][otherEdge] != a)
                    continue;

                var otherFrom = corners[b][otherEdge];
                var otherTo = corners[b][(otherEdge + 1) % corners[b].Length];
                if (SamePoint(from, otherTo) && SamePoint(to, otherFrom))
                    return true;
            }

            return false;
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
