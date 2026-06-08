using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    public class ClassicPuzzleParametersTests
    {
        private const float Eps = 1e-6f;

        [Test]
        public void Default_HasMidValues()
        {
            var p = ClassicPuzzleParameters.Default;
            Assert.AreEqual(0.5f, p.Roundness);
            Assert.AreEqual(0.5f, p.TabRadius);
            Assert.AreEqual(0.5f, p.TabOffset);
            Assert.AreEqual(0f, p.TabDeform);
        }

        [Test]
        public void Construct_InRangeInputs_StoredVerbatim()
        {
            var p = new ClassicPuzzleParameters(0.3f, 0.6f, 0.9f);
            Assert.AreEqual(0.3f, p.Roundness);
            Assert.AreEqual(0.6f, p.TabRadius);
            Assert.AreEqual(0.9f, p.TabOffset);
        }

        [TestCase(-0.1f, 0f)]
        [TestCase(-100f, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(1.1f, 1f)]
        [TestCase(1000f, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void Construct_OutOfRangeInputs_AreClamped(float input, float expected)
        {
            var pR = new ClassicPuzzleParameters(input, 0.5f, 0.5f);
            var pRad = new ClassicPuzzleParameters(0.5f, input, 0.5f);
            var pOff = new ClassicPuzzleParameters(0.5f, 0.5f, input);
            var pDef = new ClassicPuzzleParameters(0.5f, 0.5f, 0.5f, input);
            Assert.AreEqual(expected, pR.Roundness);
            Assert.AreEqual(expected, pRad.TabRadius);
            Assert.AreEqual(expected, pOff.TabOffset);
            Assert.AreEqual(expected, pDef.TabDeform);
        }

        [Test]
        public void Construct_NaN_FallsBackToZero()
        {
            var p = new ClassicPuzzleParameters(float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.AreEqual(0f, p.Roundness);
            Assert.AreEqual(0f, p.TabRadius);
            Assert.AreEqual(0f, p.TabOffset);
            Assert.AreEqual(0f, p.TabDeform);
        }

        [Test]
        public void SamplesPerEdge_MatchesShoulderFilletHeadLayout()
        {
            var p = new ClassicPuzzleParameters(0.5f, 0.5f, 0.5f);
            var expected = 2 * ClassicPuzzleParameters.ShoulderSubdivisions
                           + 2 * ClassicPuzzleParameters.FilletSubdivisions
                           + ClassicPuzzleParameters.HeadSubdivisions + 1;
            Assert.AreEqual(expected, p.SamplesPerEdge);
        }

        [Test]
        public void Resolved_StayInsideTheirRanges()
        {
            for (var i = 0; i <= 20; i++)
            {
                var t = i / 20f;
                var p = new ClassicPuzzleParameters(t, t, t, t);
                Assert.GreaterOrEqual(p.ResolvedRadius, ClassicPuzzleParameters.MinRadius - Eps);
                Assert.LessOrEqual(p.ResolvedRadius, ClassicPuzzleParameters.MaxRadius + Eps);
                Assert.GreaterOrEqual(p.ResolvedHeadHeight, -Eps);
                Assert.LessOrEqual(p.ResolvedHeadHeight, p.ResolvedRadius * ClassicPuzzleParameters.HeadHeightMax + Eps);
                Assert.GreaterOrEqual(p.ResolvedFillet, p.ResolvedRadius * ClassicPuzzleParameters.FilletMin - Eps);
                Assert.LessOrEqual(p.ResolvedFillet, p.ResolvedRadius + Eps);
                Assert.GreaterOrEqual(p.ResolvedDeform, -Eps);
                Assert.LessOrEqual(p.ResolvedDeform, 1f + Eps);
                Assert.GreaterOrEqual(p.ResolvedBulge, 0f);
                Assert.LessOrEqual(p.ResolvedBulge, ClassicPuzzleParameters.MaxBulge + Eps);
            }
        }

        [Test]
        public void TabOffsetZero_CollapsesNeck()
        {
            // At TabOffset = 0 the head sits on the edge (no lift = no neck): a semicircle bump. The
            // fillet keeps its small floor so the base rounds slightly instead of degenerating.
            var p = new ClassicPuzzleParameters(0.5f, 1f, 0f);
            Assert.AreEqual(0f, p.ResolvedHeadHeight, Eps);
            Assert.AreEqual(p.ResolvedRadius * ClassicPuzzleParameters.FilletMin, p.ResolvedFillet, Eps);
        }

        [Test]
        public void NeckRatios_AreSane()
        {
            // The head must overhang the neck (waist < head radius), and the floor must be positive.
            Assert.Greater(ClassicPuzzleParameters.NeckWidth, 0f);
            Assert.Less(ClassicPuzzleParameters.NeckWidth, 1f);
            Assert.Greater(ClassicPuzzleParameters.FilletMin, 0f);
        }

        [Test]
        public void TabStaysInsideNeighbourEnvelope()
        {
            // Perpendicular reach (bow + tab height) must stay below 0.5, or GetCellAt's 5-cell
            // search would miss. Deform is a pure along-edge lean, so it does not change the reach.
            var tabHeight = (ClassicPuzzleParameters.HeadHeightMax + 1f) * ClassicPuzzleParameters.MaxRadius;
            var tip = ClassicPuzzleParameters.MaxBulge + tabHeight;
            Assert.Less(tip, 0.5f);
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f)]
        [TestCase(float.NaN, -1000f, 1000f)]
        public void Construct_DoesNotThrow(float roundness, float radius, float offset)
        {
            Assert.DoesNotThrow(() => new ClassicPuzzleParameters(roundness, radius, offset));
        }
    }
}
