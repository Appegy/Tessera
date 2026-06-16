using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    /// Tab shape parameters for <see cref="GeometricPuzzleGrid"/>. All inputs are normalized to
    /// [0, 1] (clamped; NaN falls back to 0.5); internal physical ranges are fractions of the edge
    /// length. The tab is a 10-vertex polyline with a dovetail head: no curves, so it is the
    /// cheapest puzzle silhouette to mesh.
    ///
    /// Internal ranges are chosen so the entire [0, 1]^4 input cube stays valid for every edge
    /// seed without runtime clamping. Three couplings keep the polygon simple and lockable:
    ///   - corner clearance: inset >= depth + <see cref="CornerMargin"/>, so an inward head never
    ///     overlaps the perpendicular edge's head at a shared cell corner;
    ///   - dovetail/overhang: inset + neck-half &lt;= 0.5 - <see cref="MinShelf"/>, so the head is
    ///     always wider than the neck and the locking shelf is never thinner than MinShelf;
    ///   - per-edge jitter budget: <see cref="MaxInsetJitter"/> + <see cref="MaxNeckJitter"/> are
    ///     reserved out of both bounds, so a jittered edge can never cross either constraint.
    /// </summary>
    public readonly struct GeometricPuzzleParameters
    {
        internal const float MinDepth = 0.06f;       // head protrusion at HeadDepth = 0
        internal const float MaxDepth = 0.18f;       // ...at HeadDepth = 1 (< 0.5 so GetCellAt's 5-cell search holds)
        internal const float InsetMax = 0.32f;       // narrowest head (head spans [inset, 1 - inset])
        internal const float MinNeck = 0.05f;        // neck waist half-width floor
        internal const float MinShelf = 0.05f;       // thinnest locking overhang on each side of the neck
        internal const float CornerMargin = 0.04f;   // inset margin over depth that clears the perpendicular edge's head
        internal const float MaxInsetJitter = 0.03f; // per-edge inset wobble at Variation = 1
        internal const float MaxNeckJitter = 0.03f;  // per-edge neck wobble at Variation = 1
        internal const float ShoulderFraction = 0.55f; // shelf height as a fraction of head depth

        // 10-vertex polyline: 2 endpoints + 8 interior vertices.
        internal const int VertexCount = 10;

        public float HeadDepth { get; }
        public float HeadWidth { get; }
        public float NeckWidth { get; }
        public float Variation { get; }

        public GeometricPuzzleParameters(float headDepth, float headWidth, float neckWidth, float variation)
        {
            HeadDepth = Normalize(headDepth);
            HeadWidth = Normalize(headWidth);
            NeckWidth = Normalize(neckWidth);
            Variation = Normalize(variation);
        }

        public static GeometricPuzzleParameters Default => new GeometricPuzzleParameters(0.5f, 0.5f, 0.5f, 0.5f);

        public int SamplesPerEdge => VertexCount;

        // Head protrusion depth, perpendicular to the edge, in edge-length units.
        internal float ResolvedDepth => math.lerp(MinDepth, MaxDepth, HeadDepth);

        // Along-edge inset where the head starts (head spans [inset, 1 - inset]). HeadWidth = 1 gives
        // the widest head (smallest inset). The floor tracks depth so the head always clears the
        // perpendicular edge's head by CornerMargin even after the worst downward inset jitter.
        internal float ResolvedInset
        {
            get
            {
                var floor = InsetFloor;
                return math.lerp(floor, math.max(floor, InsetMax), 1f - HeadWidth);
            }
        }

        // Neck waist half-width (head locks because head is wider than 2 * neck-half). The ceiling is
        // whatever the budget leaves after inset, the MinShelf overhang, and both jitter reserves, so
        // inset + neck-half + jitter can never eat the shelf.
        internal float ResolvedNeckHalf => math.lerp(MinNeck, NeckCeil, NeckWidth);

        internal float ResolvedInsetJitter => Variation * MaxInsetJitter;
        internal float ResolvedNeckJitter => Variation * MaxNeckJitter;

        private float InsetFloor => ResolvedDepth + CornerMargin + MaxInsetJitter;

        private float NeckCeil => math.max(MinNeck, 0.5f - ResolvedInset - MinShelf - MaxInsetJitter - MaxNeckJitter);

        private static float Normalize(float v) => float.IsNaN(v) ? 0.5f : math.clamp(v, 0f, 1f);
    }
}
