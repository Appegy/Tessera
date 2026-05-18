namespace Appegy.Tessera
{
    // PCG-style hash of (puzzleSeed, orientation, x, y) -> 32-bit RNG seed.
    // Orientation 0 = vertical edge between (x, y) and (x+1, y);
    // orientation 1 = horizontal edge between (x, y) and (x, y+1).
    internal static class PuzzleEdgeSeed
    {
        public static uint Compute(int puzzleSeed, int orientation, int x, int y)
        {
            unchecked
            {
                var h = (uint)puzzleSeed;
                h = (h ^ (uint)(orientation * 2654435761)) * 0x85EBCA6Bu;
                h ^= h >> 13;
                h = (h ^ (uint)(x * 374761393)) * 0xC2B2AE35u;
                h ^= h >> 16;
                h = (h ^ (uint)(y * 668265263)) * 0x27D4EB2Fu;
                h ^= h >> 15;
                return h == 0u ? 1u : h;
            }
        }
    }
}
