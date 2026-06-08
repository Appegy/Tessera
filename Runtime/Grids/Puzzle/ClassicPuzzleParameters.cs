using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    /// Parameters of the constructive jigsaw silhouette used by <see cref="ClassicPuzzleGrid"/>.
    /// Every value is normalized to [0, 1]; out-of-range values are clamped, NaN falls back to 0.
    /// Internal ranges are expressed as fractions of the edge length, so the shape scales with
    /// cell size.
    /// </summary>
    public readonly struct ClassicPuzzleParameters
    {
        // --- Body bulge (Roundness) ---
        // Edge bow at Roundness = 1, as a fraction of edge length (a subtle ~5% bow). Only the
        // direction (out/in) varies per edge.
        internal const float MaxBulge = 0.0525f;

        // --- Tab ("pipka"): a round head joined to the body by two concave neck fillets ---
        // TabOffset morphs the tab: at 0 the head centre sits on the body edge with no fillet, so
        // the tab is a plain 180-degree semicircle bump (no neck); as TabOffset grows, the head
        // lifts and concave fillets appear, giving a C1-smooth knob with a neck and overhang. Both
        // the head height and the fillet radius scale with TabOffset (vanishing together at 0).
        // Head radius, as a fraction of edge length.
        internal const float MinRadius = 0.07f;
        internal const float MaxRadius = 0.119f;
        // Head centre height above the body at TabOffset = 1, as a fraction of head radius.
        internal const float HeadHeightMax = 1.5f;
        // Neck fillet radius at TabOffset = 1, as a fraction of head radius. Kept below HeadHeightMax
        // so the head always sits above the fillets and the attach width stays real.
        internal const float FilletMax = 0.35f;
        // Floor for the fillet radius (fraction of head radius) so the fillet never collapses to
        // coincident points (which would pinch the mitered outline). At TabOffset = 0 this leaves a
        // semicircle bump with a barely-rounded base instead of degenerate duplicates.
        internal const float FilletMin = 0.05f;

        // Curve resolution. Smoothness is internal, not a user slider.
        internal const int ShoulderSubdivisions = 4;
        internal const int FilletSubdivisions = 6;
        internal const int HeadSubdivisions = 20;

        public float Roundness { get; }
        public float TabRadius { get; }
        public float TabOffset { get; }

        public ClassicPuzzleParameters(float roundness, float tabRadius, float tabOffset)
        {
            Roundness = Normalize(roundness);
            TabRadius = Normalize(tabRadius);
            TabOffset = Normalize(tabOffset);
        }

        public static ClassicPuzzleParameters Default => new ClassicPuzzleParameters(0.5f, 0.5f, 0.5f);

        // p0 + left shoulder (Sh + 1) + left fillet (Fillet) + head (Head) + right fillet (Fillet) + right shoulder (Sh).
        public int SamplesPerEdge => 2 * ShoulderSubdivisions + 2 * FilletSubdivisions + HeadSubdivisions + 1;

        // Resolved fractions of edge length.
        internal float ResolvedBulge => Roundness * MaxBulge;
        internal float ResolvedRadius => math.lerp(MinRadius, MaxRadius, TabRadius);
        internal float ResolvedHeadHeight => ResolvedRadius * HeadHeightMax * TabOffset;
        internal float ResolvedFillet => ResolvedRadius * math.max(FilletMin, FilletMax * TabOffset);

        private static float Normalize(float v)
        {
            if (float.IsNaN(v)) return 0f;
            return math.clamp(v, 0f, 1f);
        }
    }
}
