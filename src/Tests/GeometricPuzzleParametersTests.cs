using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class GeometricPuzzleParametersTests
    {
        private const float Eps = 1e-5f;

        // ---- Defaults ----

        [Test]
        public void Default_HasMidNormalizedValues()
        {
            var p = GeometricPuzzleParameters.Default;
            Assert.AreEqual(0.5f, p.HeadDepth);
            Assert.AreEqual(0.5f, p.HeadWidth);
            Assert.AreEqual(0.5f, p.NeckWidth);
            Assert.AreEqual(0.5f, p.Variation);
        }

        [Test]
        public void Default_ResolvesToScreenshotShape()
        {
            // Midpoint of every axis maps to the silhouette used in the demo screenshot:
            // depth 0.12, inset 0.255, neck-half 0.0925, jitter 0.015.
            var p = GeometricPuzzleParameters.Default;
            Assert.AreEqual(0.12f, p.ResolvedDepth, Eps);
            Assert.AreEqual(0.255f, p.ResolvedInset, Eps);
            Assert.AreEqual(0.0925f, p.ResolvedNeckHalf, Eps);
            Assert.AreEqual(0.015f, p.ResolvedInsetJitter, Eps);
            Assert.AreEqual(0.015f, p.ResolvedNeckJitter, Eps);
            Assert.AreEqual(GeometricPuzzleParameters.VertexCount, p.SamplesPerEdge);
        }

        // ---- In-range inputs are stored verbatim ----

        [Test]
        public void Construct_InRangeInputs_StoredVerbatim()
        {
            var p = new GeometricPuzzleParameters(0.3f, 0.7f, 0.2f, 0.9f);
            Assert.AreEqual(0.3f, p.HeadDepth);
            Assert.AreEqual(0.7f, p.HeadWidth);
            Assert.AreEqual(0.2f, p.NeckWidth);
            Assert.AreEqual(0.9f, p.Variation);
        }

        [Test]
        public void Construct_Zeroes_ResolveToShallowNarrowHeadPinchedNeck()
        {
            var p = new GeometricPuzzleParameters(0f, 0f, 0f, 0f);
            Assert.AreEqual(GeometricPuzzleParameters.MinDepth, p.ResolvedDepth, Eps);
            // HeadWidth = 0 -> narrowest head -> largest inset (InsetMax).
            Assert.AreEqual(GeometricPuzzleParameters.InsetMax, p.ResolvedInset, Eps);
            // NeckWidth = 0 -> neck floor.
            Assert.AreEqual(GeometricPuzzleParameters.MinNeck, p.ResolvedNeckHalf, Eps);
            // Variation = 0 -> no per-edge wobble.
            Assert.AreEqual(0f, p.ResolvedInsetJitter, Eps);
            Assert.AreEqual(0f, p.ResolvedNeckJitter, Eps);
        }

        [Test]
        public void Construct_Ones_ResolveToDeepWideHead()
        {
            var p = new GeometricPuzzleParameters(1f, 1f, 1f, 1f);
            Assert.AreEqual(GeometricPuzzleParameters.MaxDepth, p.ResolvedDepth, Eps);
            // HeadWidth = 1 -> widest head -> inset at the depth-driven floor.
            var expectedInset = GeometricPuzzleParameters.MaxDepth
                + GeometricPuzzleParameters.CornerMargin
                + GeometricPuzzleParameters.MaxInsetJitter;
            Assert.AreEqual(expectedInset, p.ResolvedInset, Eps);
            Assert.AreEqual(GeometricPuzzleParameters.MaxInsetJitter, p.ResolvedInsetJitter, Eps);
            Assert.AreEqual(GeometricPuzzleParameters.MaxNeckJitter, p.ResolvedNeckJitter, Eps);
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
            var pDepth = new GeometricPuzzleParameters(input, 0.5f, 0.5f, 0.5f);
            var pWidth = new GeometricPuzzleParameters(0.5f, input, 0.5f, 0.5f);
            var pNeck = new GeometricPuzzleParameters(0.5f, 0.5f, input, 0.5f);
            var pVar = new GeometricPuzzleParameters(0.5f, 0.5f, 0.5f, input);
            Assert.AreEqual(expected, pDepth.HeadDepth);
            Assert.AreEqual(expected, pWidth.HeadWidth);
            Assert.AreEqual(expected, pNeck.NeckWidth);
            Assert.AreEqual(expected, pVar.Variation);
        }

        // ---- NaN falls back to 0.5 (default mid-range) ----

        [Test]
        public void Construct_NaNInputs_FallbackToMidpoint()
        {
            var p = new GeometricPuzzleParameters(float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.AreEqual(0.5f, p.HeadDepth);
            Assert.AreEqual(0.5f, p.HeadWidth);
            Assert.AreEqual(0.5f, p.NeckWidth);
            Assert.AreEqual(0.5f, p.Variation);
        }

        [Test]
        public void Construct_OnlySomeNaNInputs_ReplacesOnlyNaNs()
        {
            var p = new GeometricPuzzleParameters(float.NaN, 0.3f, 0.7f, float.NaN);
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
                var p = new GeometricPuzzleParameters(i / 20f, 0.5f, 0.5f, 0.5f);
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
                var p = new GeometricPuzzleParameters(0.5f, i / 20f, 0.5f, 0.5f);
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
                var p = new GeometricPuzzleParameters(0.5f, 0.5f, i / 20f, 0.5f);
                Assert.GreaterOrEqual(p.ResolvedNeckHalf, prev - Eps);
                prev = p.ResolvedNeckHalf;
            }
        }

        [Test]
        public void ResolvedJitter_IsMonotonicallyIncreasingInVariation()
        {
            var prevInset = float.NegativeInfinity;
            var prevNeck = float.NegativeInfinity;
            for (var i = 0; i <= 20; i++)
            {
                var p = new GeometricPuzzleParameters(0.5f, 0.5f, 0.5f, i / 20f);
                Assert.GreaterOrEqual(p.ResolvedInsetJitter, prevInset - Eps);
                Assert.GreaterOrEqual(p.ResolvedNeckJitter, prevNeck - Eps);
                prevInset = p.ResolvedInsetJitter;
                prevNeck = p.ResolvedNeckJitter;
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
            Assert.DoesNotThrow(() => new GeometricPuzzleParameters(headDepth, headWidth, neckWidth, variation));
        }

        // ---- Always-valid cube: the three couplings hold across the whole input cube ----
        //
        // These are the invariants the internal constants were tuned to guarantee. They are
        // checked against the worst-case per-edge jitter (the resolved jitter amplitude), so if
        // they pass here, no edge seed can produce a degenerate tab. The grid-level self-
        // intersection sweep in GeometricPuzzleGridTests is the geometric counterpart.

        [Test]
        public void Invariants_HoldAcrossFullParameterCube()
        {
            const int steps = 8;
            for (var i = 0; i <= steps; i++)
            for (var j = 0; j <= steps; j++)
            for (var k = 0; k <= steps; k++)
            for (var m = 0; m <= steps; m++)
            {
                var p = new GeometricPuzzleParameters(
                    (float)i / steps, (float)j / steps, (float)k / steps, (float)m / steps);

                var depth = p.ResolvedDepth;
                var inset = p.ResolvedInset;
                var neckHalf = p.ResolvedNeckHalf;
                var iJit = p.ResolvedInsetJitter;
                var nJit = p.ResolvedNeckJitter;
                var label = $"depth={p.HeadDepth} width={p.HeadWidth} neck={p.NeckWidth} var={p.Variation}";

                // Perpendicular reach stays inside half a cell (GetCellAt 5-cell search assumption).
                Assert.Less(depth, 0.5f, $"{label}: depth {depth} >= 0.5");

                // Corner clearance: even at the lowest jittered inset, the head clears the
                // perpendicular edge's head by at least CornerMargin.
                var minInset = inset - iJit;
                Assert.GreaterOrEqual(minInset, depth + GeometricPuzzleParameters.CornerMargin - Eps,
                    $"{label}: min inset {minInset} < depth+margin {depth + GeometricPuzzleParameters.CornerMargin}");

                // Neck never closes, even at the lowest jittered neck.
                var minNeck = neckHalf - nJit;
                Assert.Greater(minNeck, 0f, $"{label}: min neck-half {minNeck} <= 0");

                // Dovetail/overhang: even at the highest jittered inset+neck, the locking shelf on
                // each side stays at least MinShelf wide (so the head is always wider than the neck).
                var minShelf = 0.5f - (inset + iJit) - (neckHalf + nJit);
                Assert.GreaterOrEqual(minShelf, GeometricPuzzleParameters.MinShelf - Eps,
                    $"{label}: min shelf {minShelf} < {GeometricPuzzleParameters.MinShelf}");
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
                var p = new GeometricPuzzleParameters(
                    (float)i / steps, (float)j / steps, (float)k / steps, (float)m / steps);
                Assert.IsTrue(math.isfinite(p.ResolvedDepth));
                Assert.IsTrue(math.isfinite(p.ResolvedInset));
                Assert.IsTrue(math.isfinite(p.ResolvedNeckHalf));
                Assert.IsTrue(math.isfinite(p.ResolvedInsetJitter));
                Assert.IsTrue(math.isfinite(p.ResolvedNeckJitter));
            }
        }
    }
}
