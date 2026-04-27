using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>Axis-aligned rectangle in grid-local coordinates.</summary>
    public readonly struct GridBounds
    {
        public float2 Min { get; }
        public float2 Max { get; }

        public float2 Size => Max - Min;
        public float2 Center => (Min + Max) * 0.5f;

        public GridBounds(float2 min, float2 max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>Inclusive containment: returns true iff the point lies in the closed rectangle <c>[Min, Max]</c>.</summary>
        public bool Contains(float2 p)
        {
            return p.x >= Min.x && p.x <= Max.x &&
                   p.y >= Min.y && p.y <= Max.y;
        }

        public override string ToString()
        {
            return $"GridBounds[{Min} -> {Max}]";
        }
    }
}