using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    // Builds one interior puzzle edge: a gently bowed baseline (Roundness) carrying a tab at its
    // midpoint. The tab is a round head joined to the body by two concave neck fillets, each tangent
    // to the body edge and to the head circle, so the outline is C1-smooth. TabOffset morphs the tab
    // from a plain semicircle bump (no neck) to a lifted knob with a neck and overhang.
    //
    // TabDeform leans the whole tab by a per-tab shear (anchored at the body): every tab point is
    // shifted along the edge by lean * protrusion, so the head leans while the base stays put. Being
    // affine it preserves smoothness and the fillet tangencies and never self-intersects, and tab
    // size stays constant (only the lean varies). Body bow and tab poke directions are independent
    // random signs. All randomness comes from the per-edge seed, shared by both neighbours.
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
            var sBulge = rng.NextFloat() > 0.5f ? 1f : -1f; // body bow direction
            var sTab = rng.NextFloat() > 0.5f ? 1f : -1f;   // tab poke direction (independent of bow)
            // Per-tab lean (drawn even when deform is 0 so the sign draws above stay stable).
            var leanJit = rng.Range(-1f, 1f);
            var lean = deform * ClassicPuzzleParameters.MaxLean * leanJit;

            var rr = radius;                                // head radius (fraction of L)
            var rf = fillet;                                // neck fillet radius
            var hh = headHeight;                            // head centre height above the body
            var sum = rr + rf;
            // Attach half-width that keeps each fillet tangent to both the body edge and the head.
            // At headHeight = fillet = 0 this gives dx = radius: a plain 180-degree semicircle bump.
            var dx = math.sqrt(math.max(0f, sum * sum - (hh - rf) * (hh - rf)));
            var xL = 0.5f - dx;
            var xR = 0.5f + dx;
            var yBase = Bulge(bulge, xL);                   // bow height under the attach (same at xR)
            var baseOffset = sBulge * yBase;

            var phiHead = math.acos(math.clamp((rf - hh) / math.max(sum, 1e-6f), -1f, 1f));
            var aTanL = math.atan2(hh - rf, dx);
            var aTanR = math.PI - aTanL;
            var aBase = -0.5f * math.PI;

            var idx = 0;

            // Left shoulder: p0 -> left attach, following the bow.
            for (var k = 0; k <= sh; k++)
            {
                var x = math.lerp(0f, xL, (float)k / sh);
                dest[idx++] = At(p0, tangent, perp, x, sBulge * Bulge(bulge, x));
            }

            // Left fillet: attach -> head junction (concave; skip the shared attach point).
            for (var k = 1; k <= ff; k++)
            {
                var a = math.lerp(aBase, aTanL, (float)k / ff);
                dest[idx++] = Tab(p0, tangent, perp, xL + rf * math.cos(a), rf + rf * math.sin(a), lean, baseOffset, sTab);
            }

            // Head: left junction -> over the top -> right junction (skip the shared junction).
            for (var k = 1; k <= hd; k++)
            {
                var phi = math.lerp(-phiHead, phiHead, (float)k / hd);
                dest[idx++] = Tab(p0, tangent, perp, 0.5f + rr * math.sin(phi), hh + rr * math.cos(phi), lean, baseOffset, sTab);
            }

            // Right fillet: head junction -> right attach (concave; skip the shared junction).
            for (var k = 1; k <= ff; k++)
            {
                var a = math.lerp(aTanR, 1.5f * math.PI, (float)k / ff);
                dest[idx++] = Tab(p0, tangent, perp, xR + rf * math.cos(a), rf + rf * math.sin(a), lean, baseOffset, sTab);
            }

            // Right shoulder: right attach -> p1 (skip the shared attach point).
            for (var k = 1; k <= sh; k++)
            {
                var x = math.lerp(xR, 1f, (float)k / sh);
                dest[idx++] = At(p0, tangent, perp, x, sBulge * Bulge(bulge, x));
            }
        }

        // Symmetric parabolic bow: 0 at the ends, 'bulge' (fraction of L) at the midpoint.
        private static float Bulge(float bulge, float x) => 4f * bulge * x * (1f - x);

        // A tab-local point (px along edge, py protrusion above the body) leaned by the shear
        // (anchored at py = 0, so the base stays put and the head leans) and mapped to world.
        private static float2 Tab(float2 p0, float2 tangent, float2 perp, float px, float py, float lean, float baseOffset, float sTab)
        {
            var px2 = px + lean * py;
            return At(p0, tangent, perp, px2, baseOffset + sTab * py);
        }

        // Map (x along edge, signed perpendicular offset), both fractions of L, into world space.
        private static float2 At(float2 p0, float2 tangent, float2 perp, float x, float perpOffset)
            => p0 + x * tangent + perpOffset * perp;
    }
}
