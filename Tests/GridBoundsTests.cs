using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class GridBoundsTests
    {
        [Test]
        public void Construct_StoresMinAndMax()
        {
            var b = new GridBounds(new float2(1, 2), new float2(5, 7));
            Assert.AreEqual(new float2(1, 2), b.Min);
            Assert.AreEqual(new float2(5, 7), b.Max);
        }

        [Test]
        public void Size_IsMaxMinusMin()
        {
            var b = new GridBounds(new float2(1, 2), new float2(5, 7));
            Assert.AreEqual(new float2(4, 5), b.Size);
        }

        [Test]
        public void Center_IsMidpoint()
        {
            var b = new GridBounds(new float2(1, 2), new float2(5, 8));
            Assert.AreEqual(new float2(3, 5), b.Center);
        }

        [Test]
        public void Contains_PointStrictlyInside_True()
        {
            var b = new GridBounds(new float2(0, 0), new float2(10, 10));
            Assert.IsTrue(b.Contains(new float2(5, 5)));
        }

        [Test]
        public void Contains_PointOnBoundary_True()
        {
            var b = new GridBounds(new float2(0, 0), new float2(10, 10));
            Assert.IsTrue(b.Contains(new float2(0, 0)));
            Assert.IsTrue(b.Contains(new float2(10, 10)));
            Assert.IsTrue(b.Contains(new float2(0, 5)));
            Assert.IsTrue(b.Contains(new float2(10, 5)));
        }

        [Test]
        public void Contains_PointOutside_False()
        {
            var b = new GridBounds(new float2(0, 0), new float2(10, 10));
            Assert.IsFalse(b.Contains(new float2(-0.001f, 5)));
            Assert.IsFalse(b.Contains(new float2(5, -0.001f)));
            Assert.IsFalse(b.Contains(new float2(10.001f, 5)));
            Assert.IsFalse(b.Contains(new float2(5, 10.001f)));
        }
    }
}
