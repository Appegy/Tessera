using NUnit.Framework;

namespace Appegy.Tessera.Tests
{
    public class DraradechPuzzleParametersTests
    {
        private const float Eps = 1e-6f;

        // ---- Defaults ----

        [Test]
        public void Default_HasMidNormalizedValues()
        {
            var p = DraradechPuzzleParameters.Default;
            Assert.AreEqual(0.5f, p.TabSize);
            Assert.AreEqual(0.5f, p.Variation);
            Assert.AreEqual(0.5f, p.Smoothness);
        }

        [Test]
        public void Default_ResolvesToCanonicalDraradech()
        {
            // Default at midpoint of each physical range maps to the canonical
            // demo values: TabSize=0.10, Jitter=0.04, Subdivisions=10.
            var p = DraradechPuzzleParameters.Default;
            Assert.AreEqual(0.10f, p.ResolvedTabSize, Eps);
            Assert.AreEqual(0.04f, p.ResolvedJitter, Eps);
            Assert.AreEqual(10, p.BezierSubdivisions);
            Assert.AreEqual(31, p.SamplesPerEdge);
        }

        // ---- In-range inputs are stored verbatim ----

        [Test]
        public void Construct_InRangeInputs_StoredVerbatim()
        {
            var p = new DraradechPuzzleParameters(0.3f, 0.7f, 0.2f);
            Assert.AreEqual(0.3f, p.TabSize);
            Assert.AreEqual(0.7f, p.Variation);
            Assert.AreEqual(0.2f, p.Smoothness);
        }

        [Test]
        public void Construct_Zeroes_ResolveToInternalMins()
        {
            var p = new DraradechPuzzleParameters(0f, 0f, 0f);
            Assert.AreEqual(0f, p.TabSize);
            Assert.AreEqual(0f, p.Variation);
            Assert.AreEqual(0f, p.Smoothness);
            Assert.AreEqual(DraradechPuzzleParameters.MinInternalTabSize, p.ResolvedTabSize, Eps);
            Assert.AreEqual(DraradechPuzzleParameters.MinInternalJitter, p.ResolvedJitter, Eps);
            Assert.AreEqual(DraradechPuzzleParameters.MinInternalSubdivisions, p.BezierSubdivisions);
        }

        [Test]
        public void Construct_Ones_ResolveToInternalMaxes()
        {
            var p = new DraradechPuzzleParameters(1f, 1f, 1f);
            Assert.AreEqual(1f, p.TabSize);
            Assert.AreEqual(1f, p.Variation);
            Assert.AreEqual(1f, p.Smoothness);
            Assert.AreEqual(DraradechPuzzleParameters.MaxInternalTabSize, p.ResolvedTabSize, Eps);
            Assert.AreEqual(DraradechPuzzleParameters.MaxInternalJitter, p.ResolvedJitter, Eps);
            Assert.AreEqual(DraradechPuzzleParameters.MaxInternalSubdivisions, p.BezierSubdivisions);
        }

        // ---- Out-of-range inputs are clamped to [0, 1] ----

        [TestCase(-0.1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(-100f, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(1.1f, 1f)]
        [TestCase(2f, 1f)]
        [TestCase(1000f, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void Construct_OutOfRangeInputs_AreClampedToUnitInterval(float input, float expected)
        {
            var pTab = new DraradechPuzzleParameters(input, 0.5f, 0.5f);
            var pVar = new DraradechPuzzleParameters(0.5f, input, 0.5f);
            var pSmo = new DraradechPuzzleParameters(0.5f, 0.5f, input);
            Assert.AreEqual(expected, pTab.TabSize);
            Assert.AreEqual(expected, pVar.Variation);
            Assert.AreEqual(expected, pSmo.Smoothness);
        }

        // ---- NaN falls back to 0.5 (default mid-range) ----

        [Test]
        public void Construct_NaNInputs_FallbackToMidpoint()
        {
            var p = new DraradechPuzzleParameters(float.NaN, float.NaN, float.NaN);
            Assert.AreEqual(0.5f, p.TabSize);
            Assert.AreEqual(0.5f, p.Variation);
            Assert.AreEqual(0.5f, p.Smoothness);
        }

        [Test]
        public void Construct_OnlySomeNaNInputs_ReplacesOnlyNaNs()
        {
            var p = new DraradechPuzzleParameters(float.NaN, 0.3f, 0.7f);
            Assert.AreEqual(0.5f, p.TabSize);
            Assert.AreEqual(0.3f, p.Variation);
            Assert.AreEqual(0.7f, p.Smoothness);
        }

        // ---- Resolution into internal ranges ----

        [Test]
        public void ResolvedTabSize_IsAlwaysInValidRange()
        {
            // 0..1 -> [Min, Max]. Inclusive at both ends. Property covered for many points.
            for (var i = 0; i <= 20; i++)
            {
                var t = i / 20f;
                var p = new DraradechPuzzleParameters(t, 0.5f, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedTabSize, DraradechPuzzleParameters.MinInternalTabSize - Eps);
                Assert.LessOrEqual(p.ResolvedTabSize, DraradechPuzzleParameters.MaxInternalTabSize + Eps);
            }
        }

        [Test]
        public void ResolvedJitter_IsAlwaysInValidRange()
        {
            for (var i = 0; i <= 20; i++)
            {
                var v = i / 20f;
                var p = new DraradechPuzzleParameters(0.5f, v, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedJitter, DraradechPuzzleParameters.MinInternalJitter - Eps);
                Assert.LessOrEqual(p.ResolvedJitter, DraradechPuzzleParameters.MaxInternalJitter + Eps);
            }
        }

        [Test]
        public void BezierSubdivisions_IsAlwaysInValidIntegerRange()
        {
            for (var i = 0; i <= 20; i++)
            {
                var s = i / 20f;
                var p = new DraradechPuzzleParameters(0.5f, 0.5f, s);
                Assert.GreaterOrEqual(p.BezierSubdivisions, DraradechPuzzleParameters.MinInternalSubdivisions);
                Assert.LessOrEqual(p.BezierSubdivisions, DraradechPuzzleParameters.MaxInternalSubdivisions);
            }
        }

        [Test]
        public void SamplesPerEdge_AlwaysGreaterOrEqualThirteen()
        {
            // Internal subdivisions min is 4, so samples-per-edge >= 3*4 + 1 = 13.
            for (var i = 0; i <= 20; i++)
            {
                var s = i / 20f;
                var p = new DraradechPuzzleParameters(0.5f, 0.5f, s);
                Assert.GreaterOrEqual(p.SamplesPerEdge, 13);
                Assert.AreEqual(3 * p.BezierSubdivisions + 1, p.SamplesPerEdge);
            }
        }

        // ---- Monotonicity: bigger normalized => bigger resolved ----

        [Test]
        public void ResolvedTabSize_IsMonotonicallyIncreasing()
        {
            var prev = float.NegativeInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var t = i / 20f;
                var p = new DraradechPuzzleParameters(t, 0.5f, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedTabSize, prev - Eps);
                prev = p.ResolvedTabSize;
            }
        }

        [Test]
        public void ResolvedJitter_IsMonotonicallyIncreasing()
        {
            var prev = float.NegativeInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var v = i / 20f;
                var p = new DraradechPuzzleParameters(0.5f, v, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedJitter, prev - Eps);
                prev = p.ResolvedJitter;
            }
        }

        [Test]
        public void BezierSubdivisions_IsMonotonicallyNonDecreasing()
        {
            var prev = int.MinValue;
            for (var i = 0; i <= 20; i++)
            {
                var s = i / 20f;
                var p = new DraradechPuzzleParameters(0.5f, 0.5f, s);
                Assert.GreaterOrEqual(p.BezierSubdivisions, prev);
                prev = p.BezierSubdivisions;
            }
        }

        // ---- Stability: constructor cannot throw under any input ----

        [TestCase(0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f)]
        [TestCase(-1000f, 1000f, float.NaN)]
        [TestCase(float.NegativeInfinity, float.PositiveInfinity, float.NaN)]
        [TestCase(float.NaN, float.NaN, float.NaN)]
        [TestCase(-0.0001f, 1.0001f, 0.5f)]
        public void Construct_DoesNotThrow(float tabSize, float variation, float smoothness)
        {
            Assert.DoesNotThrow(() => new DraradechPuzzleParameters(tabSize, variation, smoothness));
        }

        // ---- Geometric envelope: invariants the internal ranges must preserve ----

        [Test]
        public void InternalRanges_RespectHeadFitInsideCellEnvelope()
        {
            // For the Bezier tab to stay inside a single cell, the perpendicular
            // head extent (3*TabSize + Jitter) must be strictly less than 0.5 in
            // edge-length units. Worst-case combo is (max TabSize, max Jitter).
            var headDepthMax = 3f * DraradechPuzzleParameters.MaxInternalTabSize + DraradechPuzzleParameters.MaxInternalJitter;
            Assert.Less(headDepthMax, 0.5f, $"head perpendicular extent {headDepthMax} >= 0.5, GetCellAt 5-cell search would miss");
        }

        [Test]
        public void InternalRanges_RespectHeadFitInsideEdgeEnvelope()
        {
            // For the head footprint along the edge to stay inside [0, 1], we need
            // 2*TabSize + 2*Jitter <= 0.5 (so head is centered safely under the edge).
            var headWidthMax = 2f * DraradechPuzzleParameters.MaxInternalTabSize + 2f * DraradechPuzzleParameters.MaxInternalJitter;
            Assert.LessOrEqual(headWidthMax, 0.5f, $"head along-edge footprint {headWidthMax} > 0.5, would reach edge endpoints");
        }
    }
}
