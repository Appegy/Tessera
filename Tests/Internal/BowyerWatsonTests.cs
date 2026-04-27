using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests.Internal
{
    public class BowyerWatsonTests
    {
        [Test]
        public void Triangulate_ThreePoints_OneTriangle()
        {
            var points = new[] { new float2(0, 0), new float2(1, 0), new float2(0, 1) };
            var tri = Tessera.BowyerWatson.Triangulate(points);
            Assert.AreEqual(3, tri.Length);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, tri);
        }

        [Test]
        public void Triangulate_UnitSquare_TwoTriangles()
        {
            var points = new[]
            {
                new float2(0, 0), new float2(1, 0), new float2(1, 1), new float2(0, 1)
            };
            var tri = Tessera.BowyerWatson.Triangulate(points);
            Assert.AreEqual(6, tri.Length); // 2 triangles
        }

        [Test]
        public void Triangulate_RandomPoints_AllTrianglesAreDelaunay()
        {
            var rng = new System.Random(0);
            var points = new float2[64];
            for (var i = 0; i < points.Length; i++)
                points[i] = new float2((float)rng.NextDouble() * 10, (float)rng.NextDouble() * 10);

            var tri = Tessera.BowyerWatson.Triangulate(points);
            Assert.IsTrue(tri.Length % 3 == 0);

            // Empty-circumcircle property: for every triangle, no other point is strictly inside its circumcircle.
            for (var t = 0; t < tri.Length; t += 3)
            {
                var a = points[tri[t]];
                var b = points[tri[t + 1]];
                var c = points[tri[t + 2]];
                var cc = Tessera.BowyerWatson.Circumcenter(a, b, c);
                var rsq = math.distancesq(cc, a);
                for (var p = 0; p < points.Length; p++)
                {
                    if (p == tri[t] || p == tri[t + 1] || p == tri[t + 2]) continue;
                    var dsq = math.distancesq(cc, points[p]);
                    Assert.GreaterOrEqual(dsq, rsq - 1e-4f, $"Point {p} inside circumcircle of triangle {t / 3}");
                }
            }
        }

        [Test]
        public void Circumcenter_RightTriangle_AtHypotenuseMidpoint()
        {
            var a = new float2(0, 0);
            var b = new float2(2, 0);
            var c = new float2(0, 2);
            var cc = Tessera.BowyerWatson.Circumcenter(a, b, c);
            Assert.AreEqual(1f, cc.x, 1e-5f);
            Assert.AreEqual(1f, cc.y, 1e-5f);
        }
    }
}
