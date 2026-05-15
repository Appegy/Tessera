using System;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Parameters of the Classic (Draradech) jigsaw silhouette used by
    ///     <see cref="ClassicPuzzleGrid" />. Each interior edge is a polyline of three
    ///     cubic Beziers built from 10 control points; per-edge variation comes from
    ///     a deterministic jitter range <c>j</c> derived from these parameters.
    ///     Values are fractions of edge length. The constructor enforces a safe
    ///     envelope so the generated cell polygon is simple for every seed.
    /// </summary>
    public readonly struct ClassicPuzzleParameters
    {
        public const float DefaultTabSize = 0.10f;
        public const float DefaultHeadMax = 0.28f;
        public const int DefaultBezierSubdivisions = 10;

        public const float MinTabSize = 0.05f;
        public const float MaxTabSize = 0.12f;
        public const float MinHeadMax = 0.20f;
        public const float MaxHeadMax = 0.36f;
        public const int MinBezierSubdivisions = 3;
        public const int MaxBezierSubdivisions = 32;

        /// <summary>Tab tip offset as a fraction of edge length. Larger value = deeper tabs.</summary>
        public float TabSize { get; }

        /// <summary>
        ///     Half-footprint of the head along the edge as a fraction of edge length:
        ///     the head x-extent is bounded by <c>0.5 +/- HeadMax</c>. Larger value =
        ///     more per-edge variation in head shape.
        /// </summary>
        public float HeadMax { get; }

        /// <summary>
        ///     Cubic Bezier subdivisions per silhouette segment. The polyline of every
        ///     interior edge has <c>3 * BezierSubdivisions + 1</c> points including both
        ///     endpoints.
        /// </summary>
        public int BezierSubdivisions { get; }

        /// <summary>Total points per interior edge polyline including both endpoints. Equals <c>3 * BezierSubdivisions + 1</c>.</summary>
        public int SamplesPerEdge => 3 * BezierSubdivisions + 1;

        public ClassicPuzzleParameters(float tabSize, float headMax, int bezierSubdivisions)
        {
            if (tabSize < MinTabSize || tabSize > MaxTabSize)
            {
                throw new ArgumentOutOfRangeException(nameof(tabSize), tabSize, $"Must be in [{MinTabSize}, {MaxTabSize}].");
            }
            if (headMax < MinHeadMax || headMax > MaxHeadMax)
            {
                throw new ArgumentOutOfRangeException(nameof(headMax), headMax, $"Must be in [{MinHeadMax}, {MaxHeadMax}].");
            }
            // Required so the per-edge jitter range j = (HeadMax - 2*TabSize) / 2 stays
            // at least 0.02. The bound also guarantees the head footprint along the edge
            // (2*TabSize + 2*j = HeadMax) stays strictly inside [0, 1].
            if (headMax < 2f * tabSize + 0.04f)
            {
                throw new ArgumentException($"HeadMax ({headMax}) must be at least 2 * TabSize + 0.04 ({2f * tabSize + 0.04f}) so per-edge jitter is positive.", nameof(headMax));
            }
            if (bezierSubdivisions < MinBezierSubdivisions || bezierSubdivisions > MaxBezierSubdivisions)
            {
                throw new ArgumentOutOfRangeException(nameof(bezierSubdivisions), bezierSubdivisions, $"Must be in [{MinBezierSubdivisions}, {MaxBezierSubdivisions}].");
            }

            TabSize = tabSize;
            HeadMax = headMax;
            BezierSubdivisions = bezierSubdivisions;
        }

        public static ClassicPuzzleParameters Default => new ClassicPuzzleParameters(DefaultTabSize, DefaultHeadMax, DefaultBezierSubdivisions);
    }
}
