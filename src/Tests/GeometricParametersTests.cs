using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class GeometricParametersTests
    {
        private const float Eps = 1e-5f;

        // ---- Defaults ----

        [Test]
        public void Default_HasMidNormalizedValues()
        {
            var p = GeometricParameters.Default;
            Assert.AreEqual(0.5f, p.HeadDepth);
            Assert.AreEqual(0.5f, p.HeadWidth);
            Assert.AreEqual(0.5f, p.NeckWidth);
            Assert.AreEqual(0.5f, p.Variation);
        }

        [Test]
        public void Default_ResolvesToMidOfEveryRange()
        {
            var p = GeometricParameters.Default;
            Assert.AreEqual(0.095f, p.ResolvedDepth, Eps);   // mid of [0.05, 0.14]
            Assert.AreEqual(0.265f, p.ResolvedInset, Eps);   // mid of [0.22, 0.31]
            Assert.AreEqual(0.065f, p.ResolvedNeckHalf, Eps);// mid of [0.04, 0.09]
            Assert.AreEqual(0.0125f, p.ResolvedDepthJitter, Eps);
            Assert.AreEqual(0.0125f, p.ResolvedInsetJitter, Eps);
            Assert.AreEqual(0.01f, p.ResolvedNeckJitter, Eps);
            Assert.AreEqual(GeometricParameters.VertexCount, p.SamplesPerEdge);
        }

        // ---- In-range inputs are stored verbatim ----

        [Test]
        public void Construct_InRangeInputs_StoredVerbatim()
        {
            var p = new GeometricParameters(0.3f, 0.7f, 0.2f, 0.9f);
            Assert.AreEqual(0.3f, p.HeadDepth);
            Assert.AreEqual(0.7f, p.HeadWidth);
            Assert.AreEqual(0.2f, p.NeckWidth);
            Assert.AreEqual(0.9f, p.Variation);
        }

        [Test]
        public void Construct_Zeroes_ResolveToShallowNarrowHeadPinchedNeckNoJitter()
        {
            var p = new GeometricParameters(0f, 0f, 0f, 0f);
            Assert.AreEqual(GeometricParameters.MinDepth, p.ResolvedDepth, Eps);
            // HeadWidth = 0 -> narrowest head -> largest inset.
            Assert.AreEqual(GeometricParameters.InsetMax, p.ResolvedInset, Eps);
            Assert.AreEqual(GeometricParameters.NeckMin, p.ResolvedNeckHalf, Eps);
            Assert.AreEqual(0f, p.ResolvedDepthJitter, Eps);
            Assert.AreEqual(0f, p.ResolvedInsetJitter, Eps);
            Assert.AreEqual(0f, p.ResolvedNeckJitter, Eps);
        }

        [Test]
        public void Construct_Ones_ResolveToDeepWideHeadOpenNeckMaxJitter()
        {
            var p = new GeometricParameters(1f, 1f, 1f, 1f);
            Assert.AreEqual(GeometricParameters.MaxDepth, p.ResolvedDepth, Eps);
            // HeadWidth = 1 -> widest head -> smallest inset.
            Assert.AreEqual(GeometricParameters.InsetMin, p.ResolvedInset, Eps);
            Assert.AreEqual(GeometricParameters.NeckMax, p.ResolvedNeckHalf, Eps);
            Assert.AreEqual(GeometricParameters.MaxDepthJitter, p.ResolvedDepthJitter, Eps);
            Assert.AreEqual(GeometricParameters.MaxInsetJitter, p.ResolvedInsetJitter, Eps);
            Assert.AreEqual(GeometricParameters.MaxNeckJitter, p.ResolvedNeckJitter, Eps);
        }

        // ---- Orthogonality: each knob moves exactly one resolved quantity ----
        //
        // This is the contract the demo feedback asked for: moving Head Depth must not change inset,
        // neck, or jitter; moving Head Width must not change depth, neck, or jitter; and so on.

        [Test]
        public void HeadDepth_AffectsOnlyDepth()
        {
            var reference = new GeometricParameters(0f, 0.3f, 0.7f, 0.4f);
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(i / 20f, 0.3f, 0.7f, 0.4f);
                Assert.AreEqual(reference.ResolvedInset, p.ResolvedInset, Eps);
                Assert.AreEqual(reference.ResolvedNeckHalf, p.ResolvedNeckHalf, Eps);
                Assert.AreEqual(reference.ResolvedDepthJitter, p.ResolvedDepthJitter, Eps);
                Assert.AreEqual(reference.ResolvedInsetJitter, p.ResolvedInsetJitter, Eps);
                Assert.AreEqual(reference.ResolvedNeckJitter, p.ResolvedNeckJitter, Eps);
            }
        }

        [Test]
        public void HeadWidth_AffectsOnlyInset()
        {
            var reference = new GeometricParameters(0.3f, 0f, 0.7f, 0.4f);
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(0.3f, i / 20f, 0.7f, 0.4f);
                Assert.AreEqual(reference.ResolvedDepth, p.ResolvedDepth, Eps);
                Assert.AreEqual(reference.ResolvedNeckHalf, p.ResolvedNeckHalf, Eps);
                Assert.AreEqual(reference.ResolvedDepthJitter, p.ResolvedDepthJitter, Eps);
                Assert.AreEqual(reference.ResolvedInsetJitter, p.ResolvedInsetJitter, Eps);
                Assert.AreEqual(reference.ResolvedNeckJitter, p.ResolvedNeckJitter, Eps);
            }
        }

        [Test]
        public void NeckWidth_AffectsOnlyNeck()
        {
            var reference = new GeometricParameters(0.3f, 0.7f, 0f, 0.4f);
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(0.3f, 0.7f, i / 20f, 0.4f);
                Assert.AreEqual(reference.ResolvedDepth, p.ResolvedDepth, Eps);
                Assert.AreEqual(reference.ResolvedInset, p.ResolvedInset, Eps);
                Assert.AreEqual(reference.ResolvedDepthJitter, p.ResolvedDepthJitter, Eps);
                Assert.AreEqual(reference.ResolvedInsetJitter, p.ResolvedInsetJitter, Eps);
                Assert.AreEqual(reference.ResolvedNeckJitter, p.ResolvedNeckJitter, Eps);
            }
        }

        [Test]
        public void Variation_AffectsOnlyJitter()
        {
            var reference = new GeometricParameters(0.3f, 0.7f, 0.4f, 0f);
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(0.3f, 0.7f, 0.4f, i / 20f);
                Assert.AreEqual(reference.ResolvedDepth, p.ResolvedDepth, Eps);
                Assert.AreEqual(reference.ResolvedInset, p.ResolvedInset, Eps);
                Assert.AreEqual(reference.ResolvedNeckHalf, p.ResolvedNeckHalf, Eps);
            }
        }

        // ---- Out-of-range inputs are clamped to [0, 1] ----

        [TestCase(-0.1f, 0f)]
        [TestCase(-100f, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(1.1f, 1f)]
        [TestCase(1000f, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void Construct_OutOfRangeInputs_AreClampedToUnitInterval(float input, float expected)
        {
            var pDepth = new GeometricParameters(input, 0.5f, 0.5f, 0.5f);
            var pWidth = new GeometricParameters(0.5f, input, 0.5f, 0.5f);
            var pNeck = new GeometricParameters(0.5f, 0.5f, input, 0.5f);
            var pVar = new GeometricParameters(0.5f, 0.5f, 0.5f, input);
            Assert.AreEqual(expected, pDepth.HeadDepth);
            Assert.AreEqual(expected, pWidth.HeadWidth);
            Assert.AreEqual(expected, pNeck.NeckWidth);
            Assert.AreEqual(expected, pVar.Variation);
        }

        // ---- NaN falls back to 0.5 (default mid-range) ----

        [Test]
        public void Construct_NaNInputs_FallbackToMidpoint()
        {
            var p = new GeometricParameters(float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.AreEqual(0.5f, p.HeadDepth);
            Assert.AreEqual(0.5f, p.HeadWidth);
            Assert.AreEqual(0.5f, p.NeckWidth);
            Assert.AreEqual(0.5f, p.Variation);
        }

        [Test]
        public void Construct_OnlySomeNaNInputs_ReplacesOnlyNaNs()
        {
            var p = new GeometricParameters(float.NaN, 0.3f, 0.7f, float.NaN);
            Assert.AreEqual(0.5f, p.HeadDepth);
            Assert.AreEqual(0.3f, p.HeadWidth);
            Assert.AreEqual(0.7f, p.NeckWidth);
            Assert.AreEqual(0.5f, p.Variation);
        }

        // ---- Monotonicity: each knob moves its resolved quantity in one direction ----

        [Test]
        public void ResolvedDepth_IsMonotonicallyIncreasingInHeadDepth()
        {
            var prev = float.NegativeInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(i / 20f, 0.5f, 0.5f, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedDepth, prev - Eps);
                prev = p.ResolvedDepth;
            }
        }

        [Test]
        public void ResolvedInset_IsMonotonicallyDecreasingInHeadWidth()
        {
            // Wider head <=> smaller inset.
            var prev = float.PositiveInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(0.5f, i / 20f, 0.5f, 0.5f);
                Assert.LessOrEqual(p.ResolvedInset, prev + Eps);
                prev = p.ResolvedInset;
            }
        }

        [Test]
        public void ResolvedNeckHalf_IsMonotonicallyIncreasingInNeckWidth()
        {
            var prev = float.NegativeInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(0.5f, 0.5f, i / 20f, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedNeckHalf, prev - Eps);
                prev = p.ResolvedNeckHalf;
            }
        }

        [Test]
        public void ResolvedJitter_IsMonotonicallyIncreasingInVariation()
        {
            var prevD = float.NegativeInfinity;
            var prevI = float.NegativeInfinity;
            var prevN = float.NegativeInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricParameters(0.5f, 0.5f, 0.5f, i / 20f);
                Assert.GreaterOrEqual(p.ResolvedDepthJitter, prevD - Eps);
                Assert.GreaterOrEqual(p.ResolvedInsetJitter, prevI - Eps);
                Assert.GreaterOrEqual(p.ResolvedNeckJitter, prevN - Eps);
                prevD = p.ResolvedDepthJitter;
                prevI = p.ResolvedInsetJitter;
                prevN = p.ResolvedNeckJitter;
            }
        }

        // ---- Stability: constructor cannot throw under any input ----

        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0.5f)]
        [TestCase(-1000f, 1000f, float.NaN, -0.0001f)]
        [TestCase(float.NegativeInfinity, float.PositiveInfinity, float.NaN, 1.0001f)]
        [TestCase(float.NaN, float.NaN, float.NaN, float.NaN)]
        public void Construct_DoesNotThrow(float headDepth, float headWidth, float neckWidth, float variation)
        {
            Assert.DoesNotThrow(() => new GeometricParameters(headDepth, headWidth, neckWidth, variation));
        }

        // ---- Always-valid cube: the geometric constraints hold across the whole input cube ----
        //
        // The fixed independent ranges were tuned so even the worst-case corner (widest head +
        // deepest tab + widest neck + max per-edge jitter) stays valid. Checking against the worst
        // jitter draw here means no edge seed can produce a degenerate tab. The grid-level self-
        // intersection sweep in GeometricGridTests is the geometric counterpart.

        [Test]
        public void Invariants_HoldAcrossFullParameterCube()
        {
            const int steps = 8;
            for (var i = 0; i <= steps; i++)
            for (var j = 0; j <= steps; j++)
            for (var k = 0; k <= steps; k++)
            for (var m = 0; m <= steps; m++)
            {
                var p = new GeometricParameters(
                    (float)i / steps, (float)j / steps, (float)k / steps, (float)m / steps);

                var depth = p.ResolvedDepth;
                var inset = p.ResolvedInset;
                var neckHalf = p.ResolvedNeckHalf;
                var dJit = p.ResolvedDepthJitter;
                var iJit = p.ResolvedInsetJitter;
                var nJit = p.ResolvedNeckJitter;
                var label = $"depth={p.HeadDepth} width={p.HeadWidth} neck={p.NeckWidth} var={p.Variation}";

                // Perpendicular reach (even at the deepest jittered head) stays inside half a cell.
                Assert.Less(depth + dJit, 0.5f, $"{label}: max depth {depth + dJit} >= 0.5");
                Assert.Greater(depth - dJit, 0f, $"{label}: min depth {depth - dJit} <= 0");

                // Corner clearance: at the lowest jittered inset against the deepest jittered head,
                // the head still clears the perpendicular edge's head by at least CornerMargin.
                var minInset = inset - iJit;
                var maxDepth = depth + dJit;
                Assert.GreaterOrEqual(minInset, maxDepth + GeometricParameters.CornerMargin - Eps,
                    $"{label}: min inset {minInset} < max depth+margin {maxDepth + GeometricParameters.CornerMargin}");

                // Neck never closes, even at the lowest jittered neck.
                Assert.Greater(neckHalf - nJit, 0f, $"{label}: min neck-half {neckHalf - nJit} <= 0");

                // Dovetail/overhang: at the highest jittered inset+neck the locking shelf on each
                // side stays at least MinShelf wide (so the head is always wider than the neck).
                var minShelf = 0.5f - (inset + iJit) - (neckHalf + nJit);
                Assert.GreaterOrEqual(minShelf, GeometricParameters.MinShelf - Eps,
                    $"{label}: min shelf {minShelf} < {GeometricParameters.MinShelf}");
            }
        }

        [Test]
        public void ResolvedValues_AreAlwaysFiniteAcrossCube()
        {
            const int steps = 6;
            for (var i = 0; i <= steps; i++)
            for (var j = 0; j <= steps; j++)
            for (var k = 0; k <= steps; k++)
            for (var m = 0; m <= steps; m++)
            {
                var p = new GeometricParameters(
                    (float)i / steps, (float)j / steps, (float)k / steps, (float)m / steps);
                Assert.IsTrue(math.isfinite(p.ResolvedDepth));
                Assert.IsTrue(math.isfinite(p.ResolvedInset));
                Assert.IsTrue(math.isfinite(p.ResolvedNeckHalf));
                Assert.IsTrue(math.isfinite(p.ResolvedDepthJitter));
                Assert.IsTrue(math.isfinite(p.ResolvedInsetJitter));
                Assert.IsTrue(math.isfinite(p.ResolvedNeckJitter));
            }
        }
    }
}
