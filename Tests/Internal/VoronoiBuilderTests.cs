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
    }
}
