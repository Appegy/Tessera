using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Appegy.Tessera.Tests
{
    public class GeometricGridTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void Construct_NegativeWidth_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeometricGrid(0, 4, 1f, 0));
        }

        [Test]
        public void Construct_NegativeHeight_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeometricGrid(4, 0, 1f, 0));
        }

        [Test]
        public void Construct_NegativeCellSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeometricGrid(4, 4, 0f, 0));
        }

        [Test]
        public void Construct_StoresParameters()
        {
            var g = new GeometricGrid(4, 3, 2f, 7);
            Assert.AreEqual(4, g.Width);
            Assert.AreEqual(3, g.Height);
            Assert.AreEqual(2f, g.CellSize);
            Assert.AreEqual(7, g.Seed);
            Assert.AreEqual(12, g.CellCount);
            Assert.AreEqual(0.5f, g.Parameters.HeadDepth);
            Assert.AreEqual(0.5f, g.Parameters.HeadWidth);
            Assert.AreEqual(0.5f, g.Parameters.NeckWidth);
            Assert.AreEqual(0.5f, g.Parameters.Variation);
            // Polygonal tab is a fixed 10-vertex polyline.
            Assert.AreEqual(10, g.SamplesPerEdge);
        }

        [Test]
        public void Bounds_AnchoredAtOriginExtendsToWidthHeight()
        {
            var g = new GeometricGrid(4, 3, 2f, 0);
            Assert.AreEqual(new float2(0, 0), g.Bounds.Min);
            Assert.AreEqual(new float2(8, 6), g.Bounds.Max);
        }

        [Test]
        public void GetCenter_IsHalfCellOffset()
        {
            var g = new GeometricGrid(4, 3, 2f, 0);
            Assert.AreEqual(new float2(1f, 1f), g.GetCenter(g.IdOf(0, 0)));
            Assert.AreEqual(new float2(3f, 3f), g.GetCenter(g.IdOf(1, 1)));
            Assert.AreEqual(new float2(7f, 5f), g.GetCenter(g.IdOf(3, 2)));
        }

        [Test]
        public void GetNeighborCount_IsFour()
        {
            var g = new GeometricGrid(4, 3, 1f, 0);
            for (var id = 0; id < g.CellCount; id++) Assert.AreEqual(4, g.GetNeighborCount(id));
        }

        [Test]
        public void GetNeighbor_OnBoundary_ReturnsMinusOne()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var id = g.IdOf(0, 0);
            Assert.AreEqual(-1, g.GetNeighbor(id, 1)); // bottom
            Assert.AreEqual(-1, g.GetNeighbor(id, 2)); // left
            Assert.AreNotEqual(-1, g.GetNeighbor(id, 0)); // right
            Assert.AreNotEqual(-1, g.GetNeighbor(id, 3)); // top
        }

        [Test]
        public void GetNeighbor_WrapsIndex()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            Assert.AreEqual(g.GetNeighbor(id, 0), g.GetNeighbor(id, 4));
            Assert.AreEqual(g.GetNeighbor(id, 0), g.GetNeighbor(id, -4));
        }

        [Test]
        public void AreNeighbors_AgreesWithGetNeighbor()
        {
            var g = new GeometricGrid(4, 4, 1f, 0);
            for (var id = 0; id < g.CellCount; id++)
            {
                for (var k = 0; k < 4; k++)
                {
                    var n = g.GetNeighbor(id, k);
                    if (n == -1) continue;
                    Assert.IsTrue(g.AreNeighbors(id, n));
                    Assert.IsTrue(g.AreNeighbors(n, id));
                    Assert.AreEqual(k, g.GetNeighborIndex(id, n));
                }
            }
        }

        [Test]
        public void AreNeighbors_SelfReturnsFalse()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            for (var id = 0; id < g.CellCount; id++) Assert.IsFalse(g.AreNeighbors(id, id));
        }

        [Test]
        public void AreNeighbors_OutOfRangeReturnsFalse()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            Assert.IsFalse(g.AreNeighbors(-1, 0));
            Assert.IsFalse(g.AreNeighbors(0, g.CellCount));
            Assert.IsFalse(g.AreNeighbors(g.CellCount, 0));
        }

        [Test]
        public void GetNeighborIndex_OutOfRangeReturnsMinusOne()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            Assert.AreEqual(-1, g.GetNeighborIndex(-1, 0));
            Assert.AreEqual(-1, g.GetNeighborIndex(g.CellCount, 0));
            Assert.AreEqual(-1, g.GetNeighborIndex(0, 0));
        }

        [Test]
        public void Distance_IsManhattan()
        {
            var g = new GeometricGrid(4, 4, 1f, 0);
            Assert.AreEqual(0, g.Distance(g.IdOf(1, 1), g.IdOf(1, 1)));
            Assert.AreEqual(1, g.Distance(g.IdOf(1, 1), g.IdOf(2, 1)));
            Assert.AreEqual(2, g.Distance(g.IdOf(1, 1), g.IdOf(2, 2)));
            Assert.AreEqual(6, g.Distance(g.IdOf(0, 0), g.IdOf(3, 3)));
        }

        [Test]
        public void GetCornersCount_OneByOne_IsFour()
        {
            var g = new GeometricGrid(1, 1, 1f, 0);
            Assert.AreEqual(4, g.GetCornersCount(0));
        }

        [Test]
        public void GetCornersCount_InteriorCellHasFourFullSides()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var n = g.Parameters.SamplesPerEdge - 2;
            Assert.AreEqual(4 + 4 * n, g.GetCornersCount(g.IdOf(1, 1)));
        }

        [Test]
        public void GetCornersCount_BoundaryCells_HaveFewerCorners()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var n = g.Parameters.SamplesPerEdge - 2;
            Assert.AreEqual(4 + 2 * n, g.GetCornersCount(g.IdOf(0, 0)));
            Assert.AreEqual(4 + 3 * n, g.GetCornersCount(g.IdOf(1, 0)));
        }

        [Test]
        public void GetCorner_AtRectCornersMatchesRectLayout()
        {
            var g = new GeometricGrid(1, 1, 2f, 0);
            Assert.AreEqual(new float2(2, 2), g.GetCorner(0, 0)); // TR
            Assert.AreEqual(new float2(2, 0), g.GetCorner(0, 1)); // BR
            Assert.AreEqual(new float2(0, 0), g.GetCorner(0, 2)); // BL
            Assert.AreEqual(new float2(0, 2), g.GetCorner(0, 3)); // TL
        }

        [Test]
        public void GetCorner_WrapsIndex()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            Assert.AreEqual(g.GetCorner(id, 0), g.GetCorner(id, n));
            Assert.AreEqual(g.GetCorner(id, 0), g.GetCorner(id, -n));
            Assert.AreEqual(g.GetCorner(id, 1), g.GetCorner(id, n + 1));
        }

        [Test]
        public void CopyCorners_FillsDestination()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            var dest = new float2[n];
            g.CopyCorners(id, dest);
            for (var i = 0; i < n; i++) Assert.AreEqual(g.GetCorner(id, i), dest[i]);
        }

        [Test]
        public void CopyCorners_ThrowsIfDestinationTooSmall()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var id = g.IdOf(1, 1);
            var n = g.GetCornersCount(id);
            var dest = new float2[n - 1];
            Assert.Throws<ArgumentException>(() => g.CopyCorners(id, dest));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(-7)]
        public void Corners_AreClockwise(int seed)
        {
            var g = new GeometricGrid(4, 4, 1f, seed);
            AssertAllCellsCw(g, seed);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(123)]
        public void SharedEdge_NeighboursAgreeOnPolylineExactly(int seed)
        {
            var g = new GeometricGrid(4, 4, 1f, seed);
            AssertSharedEdgesAgree(g, seed);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(42)]
        public void Polygon_HasNoSelfIntersection_Defaults(int seed)
        {
            var g = new GeometricGrid(4, 4, 1f, seed);
            AssertNoCellSelfIntersection(g, seed.ToString());
        }

        // Exhaustive sweep across the public parameter cube. Verifies the polygon is simple for
        // any (HeadDepth, HeadWidth, NeckWidth, Variation) the user can dial in. Five steps per
        // axis (including 0 and 1) gives 6^4 = 1296 combos, tied to one seed to bound run time.
        [Test]
        public void Polygon_HasNoSelfIntersection_FullParameterSweep()
        {
            const int steps = 5;
            for (var i = 0; i <= steps; i++)
            for (var j = 0; j <= steps; j++)
            for (var k = 0; k <= steps; k++)
            for (var m = 0; m <= steps; m++)
            {
                var p = new GeometricParameters(
                    (float)i / steps, (float)j / steps, (float)k / steps, (float)m / steps);
                var g = new GeometricGrid(3, 3, 1f, 0, p);
                var label = $"D={i / (float)steps:F2} W={j / (float)steps:F2} N={k / (float)steps:F2} V={m / (float)steps:F2}";
                AssertNoCellSelfIntersection(g, label);
                AssertAllCellsCw(g, 0);
            }
        }

        // Every corner of the public parameter cube (sixteen combinations of {0, 1} per axis)
        // across multiple seeds. The strictest boundary coverage without randomization.
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(0f, 0f, 0f, 1f)]
        [TestCase(0f, 0f, 1f, 0f)]
        [TestCase(0f, 0f, 1f, 1f)]
        [TestCase(0f, 1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f, 1f)]
        [TestCase(0f, 1f, 1f, 0f)]
        [TestCase(0f, 1f, 1f, 1f)]
        [TestCase(1f, 0f, 0f, 0f)]
        [TestCase(1f, 0f, 0f, 1f)]
        [TestCase(1f, 0f, 1f, 0f)]
        [TestCase(1f, 0f, 1f, 1f)]
        [TestCase(1f, 1f, 0f, 0f)]
        [TestCase(1f, 1f, 0f, 1f)]
        [TestCase(1f, 1f, 1f, 0f)]
        [TestCase(1f, 1f, 1f, 1f)]
        public void Polygon_HasNoSelfIntersection_AllCubeCorners(float depth, float width, float neck, float variation)
        {
            for (var seed = 0; seed < 8; seed++)
            {
                var p = new GeometricParameters(depth, width, neck, variation);
                var g = new GeometricGrid(3, 3, 1f, seed, p);
                AssertNoCellSelfIntersection(g, $"D={depth} W={width} N={neck} V={variation} seed={seed}");
                AssertSharedEdgesAgree(g, seed);
                AssertAllCellsCw(g, seed);
            }
        }

        // Randomized fuzz over the full (D, W, N, V, seed) input space. Deterministic via a fixed
        // PRNG seed so failures are reproducible. Catches anything the grid sweep might miss.
        [Test]
        public void Polygon_HasNoSelfIntersection_RandomizedFuzz()
        {
            var rng = new System.Random(20260616);
            for (var trial = 0; trial < 300; trial++)
            {
                var d = (float)rng.NextDouble();
                var w = (float)rng.NextDouble();
                var n = (float)rng.NextDouble();
                var v = (float)rng.NextDouble();
                var seed = rng.Next();
                var p = new GeometricParameters(d, w, n, v);
                var g = new GeometricGrid(3, 3, 1f, seed, p);
                AssertNoCellSelfIntersection(g, $"trial={trial} D={d:F3} W={w:F3} N={n:F3} V={v:F3} seed={seed}");
            }
        }

        // Out-of-range and NaN inputs get silently clamped to [0, 1], so the resulting grid must
        // still produce a simple, tiling polygon.
        [Test]
        public void Polygon_HasNoSelfIntersection_OutOfRangeAndNanInputs()
        {
            var combos = new (float d, float w, float n, float v)[]
            {
                (float.NaN, float.NaN, float.NaN, float.NaN),
                (-1f, -1f, -1f, -1f),
                (2f, 2f, 2f, 2f),
                (float.NegativeInfinity, float.PositiveInfinity, float.NaN, 0.5f),
                (-1000f, 1000f, 0.5f, float.NaN),
                (float.NaN, 0.3f, 0.7f, 1.5f),
                (1.5f, -0.5f, float.NaN, -0.5f),
            };
            foreach (var (d, w, n, v) in combos)
            {
                var p = new GeometricParameters(d, w, n, v);
                var g = new GeometricGrid(3, 3, 1f, 42, p);
                AssertNoCellSelfIntersection(g, $"D={d} W={w} N={n} V={v}");
                AssertPolygonsTileRectangle(g, $"D={d} W={w} N={n} V={v}");
            }
        }

        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(0f, 1f, 1f, 1f)]
        [TestCase(1f, 0f, 1f, 0f)]
        [TestCase(1f, 1f, 0f, 1f)]
        [TestCase(1f, 1f, 1f, 1f)]
        public void Corners_AreAlwaysFinite(float depth, float width, float neck, float variation)
        {
            var p = new GeometricParameters(depth, width, neck, variation);
            var g = new GeometricGrid(4, 4, 1f, 42, p);
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                for (var k = 0; k < n; k++)
                {
                    var c = g.GetCorner(id, k);
                    Assert.IsTrue(math.isfinite(c.x), $"D={depth} W={width} N={neck} V={variation} cell={id} corner={k} non-finite x");
                    Assert.IsTrue(math.isfinite(c.y), $"D={depth} W={width} N={neck} V={variation} cell={id} corner={k} non-finite y");
                }
            }
        }

        [Test]
        public void Polygon_TilesRectangle_RandomizedFuzz()
        {
            var rng = new System.Random(20260617);
            for (var trial = 0; trial < 60; trial++)
            {
                var d = (float)rng.NextDouble();
                var w = (float)rng.NextDouble();
                var n = (float)rng.NextDouble();
                var v = (float)rng.NextDouble();
                var seed = rng.Next();
                var p = new GeometricParameters(d, w, n, v);
                var g = new GeometricGrid(3, 3, 1f, seed, p);
                AssertPolygonsTileRectangle(g, $"trial={trial} D={d:F3} W={w:F3} N={n:F3} V={v:F3} seed={seed}");
            }
        }

        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(0.25f, 0.25f, 0.25f, 0.25f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0.5f)]
        [TestCase(0.75f, 0.75f, 0.75f, 0.75f)]
        [TestCase(1f, 1f, 1f, 1f)]
        public void Polygon_TilesRectangle_ParameterExtremes(float depth, float width, float neck, float variation)
        {
            var p = new GeometricParameters(depth, width, neck, variation);
            var g = new GeometricGrid(4, 3, 1f, 5, p);
            AssertPolygonsTileRectangle(g, $"D={depth} W={width} N={neck} V={variation}");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        public void GetCellAt_OutsideBounds_ReturnsMinusOne(int seed)
        {
            var g = new GeometricGrid(3, 3, 1f, seed);
            Assert.AreEqual(-1, g.GetCellAt(new float2(-0.5f, 0.5f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(0.5f, -0.5f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(3.5f, 1f)));
            Assert.AreEqual(-1, g.GetCellAt(new float2(1f, 3.5f)));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(13)]
        public void GetCellAt_AtCenter_ReturnsCellId(int seed)
        {
            var g = new GeometricGrid(4, 4, 1f, seed);
            for (var id = 0; id < g.CellCount; id++)
            {
                Assert.AreEqual(id, g.GetCellAt(g.GetCenter(id)), $"seed={seed} round-trip failed for cell {id}");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void GetCellAt_TilesTheRectangle(int seed)
        {
            var g = new GeometricGrid(3, 3, 1f, seed);
            const int samplesPerCellSide = 17;
            var n = g.Width * samplesPerCellSide;
            var m = g.Height * samplesPerCellSide;
            for (var iy = 0; iy < m; iy++)
            {
                for (var ix = 0; ix < n; ix++)
                {
                    var px = (ix + 0.5f) / n * g.Width * g.CellSize;
                    var py = (iy + 0.5f) / m * g.Height * g.CellSize;
                    var id = g.GetCellAt(new float2(px, py));
                    Assert.GreaterOrEqual(id, 0, $"seed={seed} point ({px}, {py}) not assigned to any cell");
                    Assert.Less(id, g.CellCount);
                }
            }
        }

        [Test]
        public void Construction_IsDeterministicForSameSeed()
        {
            var g1 = new GeometricGrid(4, 4, 1f, 42);
            var g2 = new GeometricGrid(4, 4, 1f, 42);
            for (var id = 0; id < g1.CellCount; id++)
            {
                var n = g1.GetCornersCount(id);
                Assert.AreEqual(n, g2.GetCornersCount(id));
                for (var i = 0; i < n; i++) Assert.AreEqual(g1.GetCorner(id, i), g2.GetCorner(id, i));
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentCorners()
        {
            var g1 = new GeometricGrid(4, 4, 1f, 1);
            var g2 = new GeometricGrid(4, 4, 1f, 2);
            var anyDifferent = false;
            for (var id = 0; id < g1.CellCount && !anyDifferent; id++)
            {
                var n = g1.GetCornersCount(id);
                for (var i = 0; i < n && !anyDifferent; i++)
                {
                    if (!Equals(g1.GetCorner(id, i), g2.GetCorner(id, i))) anyDifferent = true;
                }
            }
            Assert.IsTrue(anyDifferent, "two distinct seeds produced identical corners");
        }

        [Test]
        public void GetSidePolylineLength_MatchesContract()
        {
            var g = new GeometricGrid(3, 3, 1f, 0);
            var n = g.Parameters.SamplesPerEdge;
            var idInterior = g.IdOf(1, 1);
            for (var s = 0; s < 4; s++) Assert.AreEqual(n, g.GetSidePolylineLength(idInterior, s));
            var idCorner = g.IdOf(0, 0);
            Assert.AreEqual(n, g.GetSidePolylineLength(idCorner, 0));
            Assert.AreEqual(2, g.GetSidePolylineLength(idCorner, 1));
            Assert.AreEqual(2, g.GetSidePolylineLength(idCorner, 2));
            Assert.AreEqual(n, g.GetSidePolylineLength(idCorner, 3));
        }

        [Test]
        public void CopySidePolyline_BoundarySideIsStraightLine()
        {
            var g = new GeometricGrid(3, 3, 2f, 0);
            var dest = new float2[2];
            g.CopySidePolyline(g.IdOf(0, 0), 1, dest);
            Assert.AreEqual(new float2(2, 0), dest[0]);
            Assert.AreEqual(new float2(0, 0), dest[1]);
        }

        [Test]
        public void Head_IsAlwaysWiderThanNeck_LockingDovetail()
        {
            // The defining property of a locking dovetail: the head (at full depth) overhangs the
            // neck opening on both sides. Verify on every interior edge that the head's along-edge
            // span is strictly wider than the neck opening for a spread of parameters and seeds.
            var combos = new (float d, float w, float n, float v)[]
            {
                (0f, 0f, 1f, 1f),   // shallow, narrowest head, widest neck, max jitter: tightest case
                (1f, 1f, 1f, 1f),   // deep, widest head, widest neck, max jitter
                (0.5f, 0.5f, 0.5f, 0.5f),
                (1f, 0f, 0f, 1f),
            };
            foreach (var (d, w, n, v) in combos)
            {
                var p = new GeometricParameters(d, w, n, v);
                for (var seed = 0; seed < 6; seed++)
                {
                    var g = new GeometricGrid(4, 4, 1f, seed, p);
                    AssertHeadWiderThanNeck(g, $"D={d} W={w} N={n} V={v} seed={seed}");
                }
            }
        }

        [TestCase(2, 2)] [TestCase(3, 1)] [TestCase(1, 3)] [TestCase(5, 4)]
        public void Polygon_Stitches_PiecesCoverRectangleArea(int width, int height)
        {
            var g = new GeometricGrid(width, height, 1f, 11);
            AssertPolygonsTileRectangle(g, $"{width}x{height}");
        }

        // ---- Per-edge randomization (Variation) ----

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(42)]
        public void Variation_Max_ProducesDistinctPerEdgeTabs(int seed)
        {
            // With Variation = 1 the tabs must look randomized, not stamped: depth, inset, and the
            // poke direction should all vary across the interior edges.
            var p = new GeometricParameters(0.5f, 0.5f, 0.5f, 1f);
            var g = new GeometricGrid(6, 6, 1f, seed, p);

            var depths = new System.Collections.Generic.HashSet<int>();
            var insets = new System.Collections.Generic.HashSet<int>();
            var sawOut = false;
            var sawIn = false;

            for (var id = 0; id < g.CellCount; id++)
            {
                if (g.GetNeighbor(id, 0) == -1) continue;
                var (depth, inset, signed) = RightEdgeTabMetrics(g, id);
                depths.Add(Quantize(depth));
                insets.Add(Quantize(inset));
                if (signed > 0f) sawOut = true;
                if (signed < 0f) sawIn = true;
            }

            Assert.GreaterOrEqual(depths.Count, 3, $"seed={seed}: only {depths.Count} distinct tab depths - looks stamped");
            Assert.GreaterOrEqual(insets.Count, 3, $"seed={seed}: only {insets.Count} distinct tab insets - looks stamped");
            Assert.IsTrue(sawOut && sawIn, $"seed={seed}: tabs do not poke in both directions");
        }

        [Test]
        public void Variation_Zero_AllTabsShareIdenticalShape()
        {
            // With Variation = 0 every interior tab is the same silhouette (only the random poke
            // direction differs), so depth and inset are identical to within float tolerance.
            var p = new GeometricParameters(0.5f, 0.5f, 0.5f, 0f);
            var g = new GeometricGrid(6, 6, 1f, 7, p);

            float? depth0 = null;
            float? inset0 = null;
            for (var id = 0; id < g.CellCount; id++)
            {
                if (g.GetNeighbor(id, 0) == -1) continue;
                var (depth, inset, _) = RightEdgeTabMetrics(g, id);
                depth0 ??= depth;
                inset0 ??= inset;
                Assert.AreEqual(depth0.Value, depth, Eps, $"cell={id} depth differs at Variation=0");
                Assert.AreEqual(inset0.Value, inset, Eps, $"cell={id} inset differs at Variation=0");
            }
            Assert.IsTrue(depth0.HasValue, "no interior edges sampled");
        }

        // ---- helpers ----

        // Depth (perpendicular reach of the head apex), inset (along-edge position of the apex), and
        // signed perpendicular (poke direction) for a cell's right-side interior edge polyline.
        private static (float depth, float inset, float signed) RightEdgeTabMetrics(GeometricGrid g, int id)
        {
            var len = g.GetSidePolylineLength(id, 0);
            var poly = new float2[len];
            g.CopySidePolyline(id, 0, poly);
            var start = poly[0];
            var dir = math.normalize(poly[len - 1] - start);
            var normal = new float2(-dir.y, dir.x);
            var apex = poly[4]; // first head-top vertex (at full depth)
            var signed = math.dot(apex - start, normal);
            var along = math.dot(apex - start, dir);
            return (math.abs(signed), along, signed);
        }

        private static int Quantize(float v) => (int)math.round(v * 1000f);

        private static void AssertNoCellSelfIntersection(GeometricGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var corners = new float2[n];
                g.CopyCorners(id, corners);
                for (var i = 0; i < n; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % n];
                    for (var j = i + 2; j < n; j++)
                    {
                        if (i == 0 && j == n - 1) continue;
                        var c = corners[j];
                        var d = corners[(j + 1) % n];
                        if (SegmentsProperlyIntersect(a, b, c, d))
                            Assert.Fail($"{label} cell={id} edges {i}->{(i + 1) % n} and {j}->{(j + 1) % n} cross");
                    }
                }
            }
        }

        private static void AssertAllCellsCw(GeometricGrid g, int seed)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var corners = new float2[n];
                g.CopyCorners(id, corners);
                var area = SignedArea(corners);
                Assert.Less(area, 0f, $"seed={seed} cell={id} polygon not CW (signed area={area})");
            }
        }

        private static void AssertSharedEdgesAgree(GeometricGrid g, int seed)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                for (var k = 0; k < 4; k++)
                {
                    var other = g.GetNeighbor(id, k);
                    if (other == -1) continue;
                    var jSide = g.GetNeighborIndex(other, id);
                    Assert.AreNotEqual(-1, jSide);

                    var lenA = g.GetSidePolylineLength(id, k);
                    var lenB = g.GetSidePolylineLength(other, jSide);
                    Assert.AreEqual(lenA, lenB, $"seed={seed} a={id} k={k} b={other} jSide={jSide}");

                    var a = new float2[lenA];
                    var b = new float2[lenB];
                    g.CopySidePolyline(id, k, a);
                    g.CopySidePolyline(other, jSide, b);

                    for (var i = 0; i < lenA; i++)
                    {
                        Assert.AreEqual(a[i].x, b[lenA - 1 - i].x, Eps, $"seed={seed} cell={id} side={k} i={i}");
                        Assert.AreEqual(a[i].y, b[lenA - 1 - i].y, Eps, $"seed={seed} cell={id} side={k} i={i}");
                    }
                }
            }
        }

        private static void AssertPolygonsTileRectangle(GeometricGrid g, string label)
        {
            var totalArea = 0f;
            for (var id = 0; id < g.CellCount; id++)
            {
                var n = g.GetCornersCount(id);
                var corners = new float2[n];
                g.CopyCorners(id, corners);
                totalArea += math.abs(SignedArea(corners)) * 0.5f;
            }
            var expected = (float)g.Width * g.Height * g.CellSize * g.CellSize;
            Assert.AreEqual(expected, totalArea, expected * 1e-4f, label);
        }

        // For each interior vertical edge, the head (the two vertices at full depth) must span a
        // strictly wider along-edge range than the neck opening (the two baseline neck vertices).
        // The polyline order is fixed: [0]=start, [1]/[8]=neck mouth, [3..6]=head, [9]=end.
        private static void AssertHeadWiderThanNeck(GeometricGrid g, string label)
        {
            for (var id = 0; id < g.CellCount; id++)
            {
                if (g.GetNeighbor(id, 0) == -1) continue; // right side has an interior edge
                var len = g.GetSidePolylineLength(id, 0);
                var poly = new float2[len];
                g.CopySidePolyline(id, 0, poly);
                Assert.AreEqual(10, len, label);

                // Along-edge coordinate = distance from the edge start (poly[0]).
                var start = poly[0];
                var dir = math.normalize(poly[len - 1] - start);
                float Along(float2 v) => math.dot(v - start, dir);

                var neckLo = Along(poly[1]);
                var neckHi = Along(poly[8]);
                var headLo = Along(poly[4]);
                var headHi = Along(poly[5]);

                Assert.Less(headLo, neckLo, $"{label} cell={id}: head-left {headLo} not left of neck {neckLo}");
                Assert.Greater(headHi, neckHi, $"{label} cell={id}: head-right {headHi} not right of neck {neckHi}");
            }
        }

        private static float SignedArea(float2[] poly)
        {
            var area = 0f;
            var n = poly.Length;
            for (var i = 0; i < n; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }
            return area;
        }

        private static bool SegmentsProperlyIntersect(float2 a, float2 b, float2 c, float2 d)
        {
            var d1 = Cross(c, d, a);
            var d2 = Cross(c, d, b);
            var d3 = Cross(a, b, c);
            var d4 = Cross(a, b, d);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                   ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross(float2 p, float2 q, float2 r)
        {
            return (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
        }
    }
}
