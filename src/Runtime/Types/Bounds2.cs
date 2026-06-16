using Unity.Mathematics;

namespace Appegy.Tessera
{
    public readonly struct Bounds2
    {
        public float2 Min { get; }
        public float2 Max { get; }

        public float2 Size => Max - Min;
        public float2 Center => (Min + Max) * 0.5f;

        public Bounds2(float2 min, float2 max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(float2 p)
        {
            return p.x >= Min.x && p.x <= Max.x
                && p.y >= Min.y && p.y <= Max.y;
        }

        public override string ToString()
        {
            return $"Bounds2[{Min} -> {Max}]";
        }
    }
}