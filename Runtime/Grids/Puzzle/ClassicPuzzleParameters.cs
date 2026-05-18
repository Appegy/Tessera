using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Parameters of the Classic (Draradech) jigsaw silhouette used by
    ///     <see cref="ClassicPuzzleGrid" />. Inputs are normalized [0, 1]; values
    ///     outside that range are silently clamped, <c>NaN</c> falls back to
    ///     <c>0.5</c>. The constructor never throws: every combination of inputs
    ///     produces a valid, simple cell polygon for every seed. Internal physical
    ///     ranges are chosen so the constraint
    ///     <c>3 * tabSize + jitter &lt; 0.5</c> (head depth stays inside one cell)
    ///     and the constraint <c>2 * tabSize + 2 * jitter &lt; 0.5</c> (head
    ///     footprint stays inside half an edge) hold at the corners of the input
    ///     cube. See <c>ClassicPuzzleParametersTests</c> and
    ///     <c>ClassicPuzzleGridTests.Polygon_HasNoSelfIntersection</c>.
    /// </summary>
    public readonly struct ClassicPuzzleParameters
    {
        // Physical ranges. Defaults map to the canonical Draradech demo values
        // (TabSize=0.10, Jitter=0.04, Subdivisions=10) at the midpoint of each range
        // so each slider is symmetric around the canonical look.
        internal const float MinInternalTabSize = 0.06f;
        internal const float MaxInternalTabSize = 0.14f;
        internal const float MinInternalJitter = 0.02f;
        internal const float MaxInternalJitter = 0.06f;
        internal const int MinInternalSubdivisions = 4;
        internal const int MaxInternalSubdivisions = 16;

        /// <summary>Normalized tab depth in [0, 1]. 0 = shallowest, 1 = deepest tabs.</summary>
        public float TabSize { get; }

        /// <summary>Normalized per-edge shape variation in [0, 1]. 0 = uniform, 1 = highly varied.</summary>
        public float Variation { get; }

        /// <summary>Normalized polyline smoothness in [0, 1]. 0 = coarse, 1 = fine. Drives the number of cubic Bezier subdivisions per silhouette segment.</summary>
        public float Smoothness { get; }

        /// <summary>
        ///     Constructs parameters from normalized inputs. Out-of-range values are
        ///     clamped to [0, 1]; <c>NaN</c> falls back to 0.5. Never throws.
        /// </summary>
        public ClassicPuzzleParameters(float tabSize, float variation, float smoothness)
        {
            TabSize = Normalize(tabSize);
            Variation = Normalize(variation);
            Smoothness = Normalize(smoothness);
        }

        /// <summary>Default parameters: all inputs at 0.5, which maps to the canonical Draradech silhouette.</summary>
        public static ClassicPuzzleParameters Default => new ClassicPuzzleParameters(0.5f, 0.5f, 0.5f);

        /// <summary>Total points per interior edge polyline including both endpoints. Equals <c>3 * BezierSubdivisions + 1</c>.</summary>
        public int SamplesPerEdge => 3 * BezierSubdivisions + 1;

        /// <summary>Cubic Bezier subdivisions per silhouette segment, resolved from <see cref="Smoothness" />.</summary>
        public int BezierSubdivisions
        {
            get
            {
                var raw = math.lerp((float)MinInternalSubdivisions, (float)MaxInternalSubdivisions, Smoothness);
                return (int)math.round(raw);
            }
        }

        /// <summary>Tab tip offset as a fraction of edge length, resolved from <see cref="TabSize" />.</summary>
        internal float ResolvedTabSize => math.lerp(MinInternalTabSize, MaxInternalTabSize, TabSize);

        /// <summary>Per-edge jitter range as a fraction of edge length, resolved from <see cref="Variation" />.</summary>
        internal float ResolvedJitter => math.lerp(MinInternalJitter, MaxInternalJitter, Variation);

        private static float Normalize(float v)
        {
            // NaN check: any comparison with NaN is false, so the saturate would propagate NaN.
            if (float.IsNaN(v)) return 0.5f;
            return math.clamp(v, 0f, 1f);
        }
    }
}
