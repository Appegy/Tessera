using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    // Polygonal dovetail tab: a 10-vertex polyline, no curves. Layout in (u, w) edge-local
    // coordinates (u along the edge in [0, 1], w perpendicular, +w out of a CW-traversed cell):
    //
    //                4 ___________ 5      w = depth      (head top)
    //          3 ____/             \____ 6   w = shoulder (overhang shelf)
    //           |                       |
    //   0_______2                       7_______9   w = 0   (baseline)
    //           1                       8
    //         neck-left              neck-right
    //
    // The head spans [inset, 1 - inset] and the neck opening spans [0.5 - neckHalf, 0.5 + neckHalf];
    // inset < 0.5 - neckHalf makes the head overhang the neck on both sides, so pieces lock. All
    // bounds and the per-edge jitter come pre-resolved from GeometricPuzzleParameters so any value
    // here already keeps the polygon simple and lockable.
    internal static class GeometricTab
    {
        public static void Generate(
            float2 p0,
            float2 p1,
            uint edgeSeed,
            float depth,
            float inset,
            float neckHalf,
            float depthJitter,
            float insetJitter,
            float neckJitter,
            Span<float2> dest)
        {
            if (dest.Length != GeometricPuzzleParameters.VertexCount)
            {
                throw new ArgumentException(
                    $"dest length must equal {GeometricPuzzleParameters.VertexCount}.", nameof(dest));
            }

            // Local frame: u along edge, w perpendicular (+90deg, Y-up). For a CW-traversed cell
            // +w points OUT of the cell.
            var tangent = p1 - p0;
            var perp = new float2(-tangent.y, tangent.x);

            var rng = new Mulberry32(edgeSeed);
            // Random sign: tab pokes outward (+) or inward (-). Both neighbours read the same
            // polyline; only the traversal direction differs.
            var s = rng.NextFloat() > 0.5f ? 1f : -1f;
            // Per-edge jitter on every dimension so the tabs look randomized, not stamped.
            var d = depth + rng.Range(-depthJitter, depthJitter);
            var a = inset + rng.Range(-insetJitter, insetJitter);
            var nh = neckHalf + rng.Range(-neckJitter, neckJitter);

            var shoulder = s * d * GeometricPuzzleParameters.ShoulderFraction;
            var head = s * d;

            dest[0] = At(p0, tangent, perp, 0f, 0f);
            dest[1] = At(p0, tangent, perp, 0.5f - nh, 0f);
            dest[2] = At(p0, tangent, perp, 0.5f - nh, shoulder);
            dest[3] = At(p0, tangent, perp, a, shoulder);
            dest[4] = At(p0, tangent, perp, a, head);
            dest[5] = At(p0, tangent, perp, 1f - a, head);
            dest[6] = At(p0, tangent, perp, 1f - a, shoulder);
            dest[7] = At(p0, tangent, perp, 0.5f + nh, shoulder);
            dest[8] = At(p0, tangent, perp, 0.5f + nh, 0f);
            dest[9] = At(p0, tangent, perp, 1f, 0f);
        }

        private static float2 At(float2 origin, float2 tangent, float2 perp, float u, float w)
        {
            return origin + u * tangent + w * perp;
        }
    }
}
