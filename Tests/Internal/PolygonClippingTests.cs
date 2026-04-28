using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests.Internal
{
    public class PolygonClippingTests
    {
        private static readonly Bounds2 Unit = new Bounds2(new float2(0, 0), new float2(1, 1));

        [Test]
        public void Clip_PolygonInside_Unchanged()
        {
            var corners = new[] { new float2(0.7f, 0.7f), new float2(0.7f, 0.3f), new float2(0.3f, 0.3f), new float2(0.3f, 0.7f) };
            var neighbors = new[] { 10, 20, 30, 40 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            CollectionAssert.AreEqual(corners, oc);
            CollectionAssert.AreEqual(neighbors, on);
        }

        [Test]
        public void Clip_PolygonOutside_Empty()
        {
            var corners = new[] { new float2(2, 2), new float2(3, 2), new float2(3, 3), new float2(2, 3) };
            var neighbors = new[] { 1, 2, 3, 4 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            Assert.AreEqual(0, oc.Length);
            Assert.AreEqual(0, on.Length);
        }

        [Test]
        public void Clip_TriangleOneVertexOutside_FourGon()
        {
            // CW triangle, top vertex at (0.5, 1.5) is outside the unit rect.
            var corners = new[] { new float2(0.5f, 1.5f), new float2(0.9f, 0.1f), new float2(0.1f, 0.1f) };
            var neighbors = new[] { 7, 8, 9 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            Assert.AreEqual(4, oc.Length, "Triangle clipped at top should produce a 4-gon.");
            Assert.AreEqual(4, on.Length);
            AssertBoundaryEdgeHasTag(oc, on, 3, -1);
        }

        [Test]
        public void Clip_OutputCornersInsideBounds()
        {
            var corners = new[] { new float2(-0.5f, 0.5f), new float2(0.5f, -0.5f), new float2(1.5f, 0.5f), new float2(0.5f, 1.5f) };
            var neighbors = new[] { 1, 2, 3, 4 };
            var (oc, _) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);
            foreach (var c in oc)
            {
                Assert.GreaterOrEqual(c.x, -1e-5f);
                Assert.LessOrEqual(c.x, 1f + 1e-5f);
                Assert.GreaterOrEqual(c.y, -1e-5f);
                Assert.LessOrEqual(c.y, 1f + 1e-5f);
            }
        }

        [Test]
        public void Clip_NeighborTagsAlignWithOutputEdges()
        {
            var corners = new[] { new float2(0.5f, 1.5f), new float2(0.9f, 0.1f), new float2(0.1f, 0.1f) };
            var neighbors = new[] { 7, 8, 9 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);

            Assert.AreEqual(new float2(0.64285713f, 1f), oc[0]);
            Assert.AreEqual(new float2(0.9f, 0.1f), oc[1]);
            Assert.AreEqual(new float2(0.1f, 0.1f), oc[2]);
            Assert.AreEqual(new float2(0.35714287f, 1f), oc[3]);
            CollectionAssert.AreEqual(new[] { 7, 8, 9, -1 }, on);
        }

        [Test]
        public void Clip_OriginalBoundaryEdgeAlreadyOnBounds_PreservesTag()
        {
            var corners = new[] { new float2(0.8f, 1f), new float2(0.8f, 0.2f), new float2(0.2f, 0.2f), new float2(0.2f, 1f) };
            var neighbors = new[] { 10, 20, 30, 40 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);

            CollectionAssert.AreEqual(corners, oc);
            CollectionAssert.AreEqual(neighbors, on);
        }

        [Test]
        public void Clip_BoundaryTouchingCorner_DoesNotEmitDuplicateVertices()
        {
            var corners = new[] { new float2(0.2f, 1f), new float2(0.8f, 1.4f), new float2(0.8f, 0.2f), new float2(0.2f, 0.2f) };
            var neighbors = new[] { 10, 20, 30, 40 };
            var (oc, on) = Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit);

            var expectedCorners = new[] { new float2(0.2f, 1f), new float2(0.8f, 1f), new float2(0.8f, 0.2f), new float2(0.2f, 0.2f) };
            CollectionAssert.AreEqual(expectedCorners, oc);
            CollectionAssert.AreEqual(new[] { -1, 20, 30, 40 }, on);
            AssertNoDuplicateConsecutiveCorners(oc);
            AssertBoundaryEdgeHasTag(oc, on, 0, -1);
        }

        [Test]
        public void Clip_MismatchedInputLengths_Throws()
        {
            var corners = new[] { new float2(0, 0), new float2(1, 0), new float2(0, 1) };
            var neighbors = new[] { 1, 2 };
            Assert.Throws<ArgumentException>(() => Tessera.PolygonClipping.ClipToBounds(corners, neighbors, Unit));
        }

        [Test]
        public void Clip_NullInputs_Throw()
        {
            var corners = new[] { new float2(0, 0), new float2(1, 0), new float2(0, 1) };
            var neighbors = new[] { 1, 2, 3 };
            Assert.Throws<ArgumentNullException>(() => Tessera.PolygonClipping.ClipToBounds(null, neighbors, Unit));
            Assert.Throws<ArgumentNullException>(() => Tessera.PolygonClipping.ClipToBounds(corners, null, Unit));
        }

        private static void AssertNoDuplicateConsecutiveCorners(float2[] corners)
        {
            for (var i = 0; i < corners.Length; i++)
                Assert.AreNotEqual(corners[i], corners[(i + 1) % corners.Length], $"Duplicate corner at edge {i}");
        }

        private static void AssertBoundaryEdgeHasTag(float2[] corners, int[] neighbors, int edge, int tag)
        {
            Assert.AreEqual(tag, neighbors[edge]);
            Assert.AreEqual(1f, corners[edge].y, 1e-5f);
            Assert.AreEqual(1f, corners[(edge + 1) % corners.Length].y, 1e-5f);
        }
    }
}
