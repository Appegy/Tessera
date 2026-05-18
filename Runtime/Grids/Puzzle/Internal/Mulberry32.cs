namespace Appegy.Tessera
{
    // Port of mulberry32 PRNG used by the playground demo
    // (Documentation~/puzzle-styles-demo.html) so edge polylines match bit-for-bit.
    internal struct Mulberry32
    {
        private uint _state;

        public Mulberry32(uint seed)
        {
            _state = seed;
        }

        public float NextFloat()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                var v = t ^ (t >> 14);
                return (float)(v / 4294967296.0);
            }
        }

        public float Range(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }
    }
}
