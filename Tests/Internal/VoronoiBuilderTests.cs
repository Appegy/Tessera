using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests.Internal
{
    public class VoronoiBuilderTests
    {
        private static readonly Bounds2 Unit = new Bounds2(new float2(0, 0), new float2(1, 1));

        [Test]
        public void ExtractRaw_FourCornerSeeds_ProducesFourPolygons()
        {
            var seeds = new[]
            {
                new float2(0.1f, 0.1f),
                new float2(0.9f, 0.1f),
                new float2(0.9f, 0.9f),
                new float2(0.1f, 0.9f)
            };

            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);

            Assert.AreEqual(seeds.Length, raw.Corners.Length);
            Assert.AreEqual(seeds.Length, raw.Neighbors.Length);
            for (var i = 0; i < seeds.Length; i++)
            {
                Assert.GreaterOrEqual(raw.Corners[i].Length, 3, $"Cell {i} should have at least 3 corners");
                Assert.AreEqual(raw.Corners[i].Length, raw.Neighbors[i].Length, $"Cell {i} counts must match");
                CollectionAssert.Contains(raw.Neighbors[i], -1, $"Hull cell {i} should expose boundary edges");
            }
        }

        [Test]
        public void ExtractRaw_SymmetricNeighborhood()
        {
            var seeds = CreateRandomSeeds(16, 1);
            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);

            for (var a = 0; a < seeds.Length; a++)
            {
                foreach (var b in raw.Neighbors[a])
                {
                    if (b == -1)
                        continue;

                    var found = false;
                    foreach (var bn in raw.Neighbors[b])
                    {
                        if (bn == a)
                        {
                            found = true;
                            break;
                        }
                    }

                    Assert.IsTrue(found, $"Asymmetric: {a} sees {b} but {b} does not see {a}");
                }
            }
        }

        [Test]
        public void ExtractRaw_NeighborTagsAlignWithReversedEdges()
        {
            var seeds = CreateRandomSeeds(16, 3);
            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);

            AssertNeighborEdgesAreReversed(raw);
        }

        [Test]
        public void ExtractRaw_ThreeSeeds_TagsHullAndInternalEdges()
        {
            var seeds = new[]
            {
                new float2(0.2f, 0.2f),
                new float2(0.8f, 0.2f),
                new float2(0.5f, 0.8f)
            };

            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);

            for (var i = 0; i < seeds.Length; i++)
            {
                Assert.AreEqual(3, raw.Corners[i].Length, $"Cell {i} should be a raw triangle");
                Assert.AreEqual(1, CountNeighbor(raw.Neighbors[i], -1), $"Cell {i} should have one boundary edge");
                Assert.AreEqual(2, CountNonBoundaryNeighbors(raw.Neighbors[i]), $"Cell {i} should have two internal edges");
            }

            AssertNeighborEdgesAreReversed(raw);
        }

        [Test]
        public void ExtractRaw_CornersAreClockwise()
        {
            var seeds = CreateRandomSeeds(16, 2);
            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);

            for (var c = 0; c < seeds.Length; c++)
            {
                var signedArea = SignedArea(raw.Corners[c]);
                Assert.Less(signedArea, 0f, $"Cell {c} should be CW (negative signed area).");
            }
        }

        [Test]
        public void ExtractRaw_CornersDoNotSelfIntersect()
        {
            var seeds = CreateRandomSeeds(16, 4);
            var raw = Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit);

            for (var c = 0; c < seeds.Length; c++)
                AssertNoSelfIntersection(raw.Corners[c], $"Cell {c}");
        }

        [Test]
        public void ExtractRaw_InvalidBounds_Throws()
        {
            var seeds = new[] { new float2(0, 0), new float2(1, 0), new float2(0, 1) };
            Assert.Throws<System.InvalidOperationException>(() =>
                Tessera.VoronoiBuilder.ExtractRaw(seeds, new Bounds2(new float2(0, 0), new float2(0, 1))));
            Assert.Throws<System.InvalidOperationException>(() =>
                Tessera.VoronoiBuilder.ExtractRaw(seeds, new Bounds2(new float2(0, 0), new float2(float.PositiveInfinity, 1))));
            Assert.Throws<System.InvalidOperationException>(() =>
                Tessera.VoronoiBuilder.ExtractRaw(seeds, new Bounds2(new float2(-float.MaxValue, 0), new float2(float.MaxValue, 1))));
        }

        [Test]
        public void ExtractRaw_InvalidSeeds_Throws()
        {
            var seeds = new[] { new float2(0, 0), new float2(float.NaN, 0), new float2(0, 1) };
            Assert.Throws<System.InvalidOperationException>(() => Tessera.VoronoiBuilder.ExtractRaw(seeds, Unit));

            var outsideSeeds = new[] { new float2(0, 0), new float2(1.1f, 0), new float2(0, 1) };
            Assert.Throws<System.InvalidOperationException>(() => Tessera.VoronoiBuilder.ExtractRaw(outsideSeeds, Unit));
        }

        [Test]
        public void Build_DeterministicForSameSeed()
        {
            var b1 = Tessera.VoronoiBuilder.Build(Unit, 32, 42, 3);
            var b2 = Tessera.VoronoiBuilder.Build(Unit, 32, 42, 3);

            Assert.AreEqual(b1.Centers.Length, b2.Centers.Length);
            for (var i = 0; i < b1.Centers.Length; i++)
            {
                Assert.AreEqual(b1.Centers[i].x, b2.Centers[i].x, 0f);
                Assert.AreEqual(b1.Centers[i].y, b2.Centers[i].y, 0f);
                Assert.AreEqual(b1.Corners[i].Length, b2.Corners[i].Length);
                CollectionAssert.AreEqual(b1.Neighbors[i], b2.Neighbors[i]);
            }
        }

        [Test]
        public void Build_AllCornersInsideBounds()
        {
            var r = Tessera.VoronoiBuilder.Build(Unit, 64, 7, 3);

            foreach (var corners in r.Corners)
            {
                foreach (var corner in corners)
                {
                    Assert.GreaterOrEqual(corner.x, -1e-4f);
                    Assert.LessOrEqual(corner.x, 1f + 1e-4f);
                    Assert.GreaterOrEqual(corner.y, -1e-4f);
                    Assert.LessOrEqual(corner.y, 1f + 1e-4f);
                }
            }
        }

        [Test]
        public void Build_AlignmentAndSymmetryHold()
        {
            var r = Tessera.VoronoiBuilder.Build(Unit, 64, 11, 3);

            for (var i = 0; i < r.Centers.Length; i++)
                Assert.AreEqual(r.Corners[i].Length, r.Neighbors[i].Length);

            AssertNeighborEdgesAreReversed(r.Corners, r.Neighbors);
        }

        [Test]
        public void Build_LloydReducesAreaVariance()
        {
            var r0 = Tessera.VoronoiBuilder.Build(Unit, 64, 13, 0);
            var r5 = Tessera.VoronoiBuilder.Build(Unit, 64, 13, 5);

            Assert.Less(AreaVariance(r5), AreaVariance(r0));
        }

        [Test]
        public void Build_SingleCell_CoversBounds()
        {
            var result = Tessera.VoronoiBuilder.Build(Unit, 1, 5, 2);

            Assert.AreEqual(1, result.Centers.Length);
            Assert.AreEqual(4, result.Corners[0].Length);
            Assert.AreEqual(4, result.Neighbors[0].Length);
            CollectionAssert.AreEqual(new[] { -1, -1, -1, -1 }, result.Neighbors[0]);
            Assert.IsTrue(Unit.Contains(result.Centers[0]));
            Assert.AreEqual(1f, math.abs(SignedArea(result.Corners[0])), 1e-5f);
        }

        [Test]
        public void Build_TwoCells_ShareOneEdge()
        {
            var result = Tessera.VoronoiBuilder.Build(Unit, 2, 5, 2);

            Assert.AreEqual(2, result.Centers.Length);
            AssertNeighborEdgesAreReversed(result.Corners, result.Neighbors);
            CollectionAssert.Contains(result.Neighbors[0], 1);
            CollectionAssert.Contains(result.Neighbors[1], 0);
        }

        private static float2[] CreateRandomSeeds(int count, int seed)
        {
            var rng = new System.Random(seed);
            var seeds = new float2[count];
            for (var i = 0; i < seeds.Length; i++)
                seeds[i] = new float2((float)rng.NextDouble(), (float)rng.NextDouble());
            return seeds;
        }

        private static float SignedArea(float2[] corners)
        {
            var signedArea = 0f;
            for (var i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                var q = corners[(i + 1) % corners.Length];
                signedArea += p.x * q.y - q.x * p.y;
            }

            return signedArea * 0.5f;
        }

        private static void AssertNeighborEdgesAreReversed(Tessera.VoronoiBuilder.RawCells raw)
        {
            AssertNeighborEdgesAreReversed(raw.Corners, raw.Neighbors);
        }

        private static void AssertNeighborEdgesAreReversed(float2[][] corners, int[][] neighbors)
        {
            for (var a = 0; a < corners.Length; a++)
            {
                for (var edge = 0; edge < neighbors[a].Length; edge++)
                {
                    var b = neighbors[a][edge];
                    if (b == -1)
                        continue;

                    var from = corners[a][edge];
                    var to = corners[a][(edge + 1) % corners[a].Length];
                    var found = false;
                    for (var otherEdge = 0; otherEdge < neighbors[b].Length; otherEdge++)
                    {
                        if (neighbors[b][otherEdge] != a)
                            continue;

                        var otherFrom = corners[b][otherEdge];
                        var otherTo = corners[b][(otherEdge + 1) % corners[b].Length];
                        if (Close(from, otherTo) && Close(to, otherFrom))
                        {
                            found = true;
                            break;
                        }
                    }

                    Assert.IsTrue(found, $"Edge {a}[{edge}] tagged {b} has no reversed edge in neighbor cell");
                }
            }
        }

        private static float AreaVariance(Tessera.VoronoiBuilder.Result result)
        {
            var areas = new float[result.Corners.Length];
            var sum = 0f;
            for (var i = 0; i < result.Corners.Length; i++)
            {
                areas[i] = math.abs(SignedArea(result.Corners[i]));
                sum += areas[i];
            }

            var mean = sum / areas.Length;
            var variance = 0f;
            for (var i = 0; i < areas.Length; i++)
                variance += (areas[i] - mean) * (areas[i] - mean);

            return variance / areas.Length;
        }

        private static void AssertNoSelfIntersection(float2[] corners, string label)
        {
            for (var a = 0; a < corners.Length; a++)
            {
                var aNext = (a + 1) % corners.Length;
                for (var b = a + 1; b < corners.Length; b++)
                {
                    var bNext = (b + 1) % corners.Length;
                    if (a == bNext || aNext == b)
                        continue;

                    Assert.IsFalse(SegmentsIntersect(corners[a], corners[aNext], corners[b], corners[bNext]),
                        $"{label} edges {a} and {b} should not cross");
                }
            }
        }

        private static bool SegmentsIntersect(float2 a, float2 b, float2 c, float2 d)
        {
            var abC = Cross(b - a, c - a);
            var abD = Cross(b - a, d - a);
            var cdA = Cross(d - c, a - c);
            var cdB = Cross(d - c, b - c);
            return abC * abD < -1e-5f && cdA * cdB < -1e-5f;
        }

        private static int CountNeighbor(int[] neighbors, int target)
        {
            var count = 0;
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (neighbors[i] == target)
                    count++;
            }

            return count;
        }

        private static int CountNonBoundaryNeighbors(int[] neighbors)
        {
            var count = 0;
            for (var i = 0; i < neighbors.Length; i++)
            {
                if (neighbors[i] != -1)
                    count++;
            }

            return count;
        }

        private static bool Close(float2 a, float2 b)
        {
            return math.distancesq(a, b) <= 1e-8f;
        }

        private static float Cross(float2 a, float2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
