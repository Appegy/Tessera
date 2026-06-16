using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    /// Tab shape parameters for <see cref="GeometricGrid"/>. All inputs are normalized to
    /// [0, 1] (clamped; NaN falls back to 0.5); internal physical ranges are fractions of the edge
    /// length. The tab is a 10-vertex polyline with a dovetail head: no curves, so it is the
    /// cheapest puzzle silhouette to mesh.
    ///
    /// Each knob is orthogonal: it maps to exactly one visual quantity through a fixed independent
    /// range, so moving one slider never disturbs another. The ranges are deliberately chosen so
    /// the worst-case corner of the [0, 1]^4 cube (widest head + deepest tab + widest neck + max
    /// per-edge jitter, all at once) still satisfies every geometric constraint, which is what lets
    /// them stay independent without runtime clamping:
    ///   - corner clearance: inset >= depth + <see cref="CornerMargin"/>, so an inward head never
    ///     overlaps the perpendicular edge's head at a shared cell corner;
    ///   - dovetail/overhang: inset + neck-half &lt;= 0.5 - <see cref="MinShelf"/>, so the head is
    ///     always wider than the neck and the locking shelf is never thinner than MinShelf;
    ///   - per-edge jitter is reserved out of both bounds, so a jittered edge can never cross them.
    ///
    /// <see cref="Variation"/> drives independent per-edge jitter on depth, inset, and neck, so the
    /// pieces look randomized while neighbours still stitch (jitter is deterministic per edge id).
    /// </summary>
    public readonly struct GeometricParameters
    {
        internal const float MinDepth = 0.05f;       // head protrusion at HeadDepth = 0
        internal const float MaxDepth = 0.14f;       // ...at HeadDepth = 1
        internal const float MaxDepthJitter = 0.025f;// per-edge depth wobble at Variation = 1

        internal const float InsetMin = 0.22f;       // widest head (head spans [inset, 1 - inset]) at HeadWidth = 1
        internal const float InsetMax = 0.31f;       // narrowest head at HeadWidth = 0
        internal const float MaxInsetJitter = 0.025f;// per-edge inset wobble at Variation = 1

        internal const float NeckMin = 0.04f;        // pinched neck waist half-width at NeckWidth = 0
        internal const float NeckMax = 0.09f;        // open neck at NeckWidth = 1
        internal const float MaxNeckJitter = 0.02f;  // per-edge neck wobble at Variation = 1

        internal const float MinShelf = 0.05f;       // thinnest locking overhang on each side of the neck
        internal const float CornerMargin = 0.025f;  // inset margin over depth that clears the perpendicular edge's head
        internal const float ShoulderFraction = 0.55f; // shelf height as a fraction of head depth

        // 10-vertex polyline: 2 endpoints + 8 interior vertices.
        internal const int VertexCount = 10;

        public float HeadDepth { get; }
        public float HeadWidth { get; }
        public float NeckWidth { get; }
        public float Variation { get; }

        public GeometricParameters(float headDepth, float headWidth, float neckWidth, float variation)
        {
            HeadDepth = Normalize(headDepth);
            HeadWidth = Normalize(headWidth);
            NeckWidth = Normalize(neckWidth);
            Variation = Normalize(variation);
        }

        public static GeometricParameters Default => new GeometricParameters(0.5f, 0.5f, 0.5f, 0.5f);

        public int SamplesPerEdge => VertexCount;

        // Head protrusion depth, perpendicular to the edge. Depends on HeadDepth only.
        internal float ResolvedDepth => math.lerp(MinDepth, MaxDepth, HeadDepth);

        // Along-edge inset where the head starts (head spans [inset, 1 - inset]). HeadWidth = 1 is
        // the widest head (smallest inset). Depends on HeadWidth only.
        internal float ResolvedInset => math.lerp(InsetMax, InsetMin, HeadWidth);

        // Neck waist half-width (the head locks because it is wider than 2 * neck-half). Depends on
        // NeckWidth only.
        internal float ResolvedNeckHalf => math.lerp(NeckMin, NeckMax, NeckWidth);

        // Per-edge jitter amplitudes. Depend on Variation only.
        internal float ResolvedDepthJitter => Variation * MaxDepthJitter;
        internal float ResolvedInsetJitter => Variation * MaxInsetJitter;
        internal float ResolvedNeckJitter => Variation * MaxNeckJitter;

        private static float Normalize(float v) => float.IsNaN(v) ? 0.5f : math.clamp(v, 0f, 1f);
    }
}
