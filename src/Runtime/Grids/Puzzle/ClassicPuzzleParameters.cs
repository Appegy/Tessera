using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    /// Tab shape parameters for <see cref="ClassicPuzzleGrid"/>. All inputs are normalized to [0, 1]
    /// (clamped; NaN falls back to 0); internal ranges are fractions of the edge length.
    /// </summary>
    public readonly struct ClassicPuzzleParameters
    {
        internal const float MaxBulge = 0.0525f;     // edge bow at Roundness = 1
        internal const float MinRadius = 0.07f;      // head radius range
        internal const float MaxRadius = 0.119f;
        internal const float HeadHeightMax = 1.5f;   // head centre height at TabOffset = 1, in radii
        internal const float NeckWidth = 0.7f;       // neck waist half-width at TabNeck = 0, in radii (< 1 to overhang)
        internal const float NeckWidthMax = 0.9f;    // ...at TabNeck = 1; kept < 1 so the head still overhangs
        internal const float FilletMin = 0.05f;      // fillet floor; below it the tab is a semicircle bump
        internal const float MaxLean = 0.45f;        // deform: along-edge shift per unit protrusion

        internal const int ShoulderSubdivisions = 4;
        internal const int FilletSubdivisions = 6;
        internal const int HeadSubdivisions = 20;

        public float Roundness { get; }
        public float TabRadius { get; }
        public float TabOffset { get; }
        public float TabDeform { get; }
        public float TabNeck { get; }

        public ClassicPuzzleParameters(float roundness, float tabRadius, float tabOffset, float tabDeform = 0f, float tabNeck = 0f)
        {
            Roundness = Normalize(roundness);
            TabRadius = Normalize(tabRadius);
            TabOffset = Normalize(tabOffset);
            TabDeform = Normalize(tabDeform);
            TabNeck = Normalize(tabNeck);
        }

        public static ClassicPuzzleParameters Default => new ClassicPuzzleParameters(0.5f, 0.5f, 0.5f, 0f, 0f);

        public int SamplesPerEdge => 2 * ShoulderSubdivisions + 2 * FilletSubdivisions + HeadSubdivisions + 1;

        internal float ResolvedBulge => Roundness * MaxBulge;
        internal float ResolvedRadius => math.lerp(MinRadius, MaxRadius, TabRadius);
        internal float ResolvedHeadHeight => ResolvedRadius * HeadHeightMax * TabOffset;
        internal float ResolvedDeform => TabDeform;

        // Neck waist half-width (in radii). TabNeck opens it from NeckWidth (today's pinched neck, the
        // floor the user asked for) up to NeckWidthMax. A wider waist also yields a larger fillet below,
        // so the same control reads as "rounder corners" while the neck only ever gets thicker, not thinner.
        internal float ResolvedNeckWidth => math.lerp(NeckWidth, NeckWidthMax, TabNeck);

        // Fillet radius that holds the neck waist at ResolvedNeckWidth * radius (with c = sqrt(1 - n^2),
        // rf = (hh - c*R)/(1 + c) gives (hh - rf)/(R + rf) = c), floored so low TabOffset stays a semicircle.
        internal float ResolvedFillet
        {
            get
            {
                var r = ResolvedRadius;
                var n = ResolvedNeckWidth;
                var c = math.sqrt(1f - n * n);
                var rf = (ResolvedHeadHeight - c * r) / (1f + c);
                return math.max(FilletMin * r, rf);
            }
        }

        private static float Normalize(float v) => float.IsNaN(v) ? 0f : math.clamp(v, 0f, 1f);
    }
}
