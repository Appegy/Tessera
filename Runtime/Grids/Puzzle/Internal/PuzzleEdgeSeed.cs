namespace Appegy.Tessera
{
    /// <summary>
    ///     PCG-style hash that turns <c>(puzzleSeed, orientation, x, y)</c> into a
    ///     32-bit RNG seed. Ported from the playground demo so per-edge results
    ///     match between the demo and Unity. Orientation 0 = vertical interior
    ///     edge between cells <c>(x, y)</c> and <c>(x+1, y)</c>; orientation 1 =
    ///     horizontal interior edge between cells <c>(x, y)</c> and <c>(x, y+1)</c>.
    /// </summary>
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
