using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    /// Parameters of the Draradech jigsaw silhouette used by <see cref="DraradechPuzzleGrid"/>.
    /// All inputs are normalized to [0, 1]; out-of-range values are clamped, NaN falls back
    /// to 0.5. Internal physical ranges are chosen so the silhouette stays simple for every
    /// seed across the input cube.
    /// </summary>
    public readonly struct DraradechPuzzleParameters
    {
        internal const float MinInternalTabSize = 0.06f;
        internal const float MaxInternalTabSize = 0.14f;
        internal const float MinInternalJitter = 0.025f;
        internal const float MaxInternalJitter = 0.055f;
        internal const int MinInternalSubdivisions = 4;
        internal const int MaxInternalSubdivisions = 16;

        public float TabSize { get; }
        public float Variation { get; }
        public float Smoothness { get; }

        public DraradechPuzzleParameters(float tabSize, float variation, float smoothness)
        {
            TabSize = Normalize(tabSize);
            Variation = Normalize(variation);
            Smoothness = Normalize(smoothness);
        }

        public static DraradechPuzzleParameters Default => new DraradechPuzzleParameters(0.5f, 0.5f, 0.5f);

        public int SamplesPerEdge => 3 * BezierSubdivisions + 1;

        public int BezierSubdivisions
        {
            get
            {
                var raw = math.lerp((float)MinInternalSubdivisions, (float)MaxInternalSubdivisions, Smoothness);
                return (int)math.round(raw);
            }
        }

        internal float ResolvedTabSize => math.lerp(MinInternalTabSize, MaxInternalTabSize, TabSize);
        internal float ResolvedJitter => math.lerp(MinInternalJitter, MaxInternalJitter, Variation);

        private static float Normalize(float v)
        {
            if (float.IsNaN(v)) return 0.5f;
            return math.clamp(v, 0f, 1f);
        }
    }
}
