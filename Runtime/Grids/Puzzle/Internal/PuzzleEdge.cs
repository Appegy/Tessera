using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    // One interior puzzle edge: a bowed baseline carrying a tab (round head + two concave neck
    // fillets, all tangent so the outline is C1). TabDeform leans the tab by a per-tab affine shear.
    // Bow and tab directions are independent signs from the per-edge seed (shared by both neighbours).
    internal static class PuzzleEdge
    {
        public static void Generate(
            float2 p0,
            float2 p1,
            uint edgeSeed,
            float bulge,
            float radius,
            float headHeight,
            float fillet,
            float deform,
            Span<float2> dest)
        {
            var sh = ClassicPuzzleParameters.ShoulderSubdivisions;
            var ff = ClassicPuzzleParameters.FilletSubdivisions;
            var hd = ClassicPuzzleParameters.HeadSubdivisions;
            var samples = 2 * sh + 2 * ff + hd + 1;
            if (dest.Length != samples)
            {
                throw new ArgumentException($"dest length must equal 2*Shoulder + 2*Fillet + Head + 1 (= {samples}).", nameof(dest));
            }

            var tangent = p1 - p0;                          // length L
            var perp = new float2(-tangent.y, tangent.x);   // length L, outward for CW traversal

            var rng = new Mulberry32(edgeSeed);
            var sBulge = rng.NextFloat() > 0.5f ? 1f : -1f;
            var sTab = rng.NextFloat() > 0.5f ? 1f : -1f;
            var lean = deform * ClassicPuzzleParameters.MaxLean * rng.Range(-1f, 1f);

            var rr = radius;
            var rf = fillet;
            var hh = headHeight;
            var sum = rr + rf;
            // Attach half-width keeping each fillet tangent to both edge and head; at hh = rf = 0 it is
            // a plain semicircle (dx = radius).
            var dx = math.sqrt(math.max(0f, sum * sum - (hh - rf) * (hh - rf)));
            var xL = 0.5f - dx;
            var xR = 0.5f + dx;
            var yBase = Bulge(bulge, xL);
            var baseOffset = sBulge * yBase;

            var phiHead = math.acos(math.clamp((rf - hh) / math.max(sum, 1e-6f), -1f, 1f));
            var aTanL = math.atan2(hh - rf, dx);
            var aTanR = math.PI - aTanL;
            var aBase = -0.5f * math.PI;

            var idx = 0;

            // Left shoulder.
            for (var k = 0; k <= sh; k++)
            {
                var x = math.lerp(0f, xL, (float)k / sh);
                dest[idx++] = At(p0, tangent, perp, x, sBulge * Bulge(bulge, x));
            }

            // Left fillet (skips the shared attach point).
            for (var k = 1; k <= ff; k++)
            {
                var a = math.lerp(aBase, aTanL, (float)k / ff);
                dest[idx++] = Tab(p0, tangent, perp, xL + rf * math.cos(a), rf + rf * math.sin(a), lean, baseOffset, sTab);
            }

            // Head, over the top.
            for (var k = 1; k <= hd; k++)
            {
                var phi = math.lerp(-phiHead, phiHead, (float)k / hd);
                dest[idx++] = Tab(p0, tangent, perp, 0.5f + rr * math.sin(phi), hh + rr * math.cos(phi), lean, baseOffset, sTab);
            }

            // Right fillet.
            for (var k = 1; k <= ff; k++)
            {
                var a = math.lerp(aTanR, 1.5f * math.PI, (float)k / ff);
                dest[idx++] = Tab(p0, tangent, perp, xR + rf * math.cos(a), rf + rf * math.sin(a), lean, baseOffset, sTab);
            }

            // Right shoulder.
            for (var k = 1; k <= sh; k++)
            {
                var x = math.lerp(xR, 1f, (float)k / sh);
                dest[idx++] = At(p0, tangent, perp, x, sBulge * Bulge(bulge, x));
            }
        }

        private static float Bulge(float bulge, float x) => 4f * bulge * x * (1f - x);

        // Tab-local point (px along, py out) leaned by the shear, then mapped to world.
        private static float2 Tab(float2 p0, float2 tangent, float2 perp, float px, float py, float lean, float baseOffset, float sTab)
            => At(p0, tangent, perp, px + lean * py, baseOffset + sTab * py);

        private static float2 At(float2 p0, float2 tangent, float2 perp, float x, float perpOffset)
            => p0 + x * tangent + perpOffset * perp;
    }
}
