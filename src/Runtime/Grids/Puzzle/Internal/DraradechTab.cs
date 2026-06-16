using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    internal static class DraradechTab
    {
        public static void Generate(
            float2 p0,
            float2 p1,
            uint edgeSeed,
            float tabSize,
            float jitter,
            int bezierSubdivisions,
            Span<float2> dest)
        {
            var samples = 3 * bezierSubdivisions + 1;
            if (dest.Length != samples)
            {
                throw new ArgumentException($"dest length must equal 3 * bezierSubdivisions + 1 (= {samples}).", nameof(dest));
            }

            // Local frame: u along edge, w perpendicular (+90deg, Y-up).
            // For a CW-traversed cell +w points OUT of the cell.
            var tangent = p1 - p0;
            var perp = new float2(-tangent.y, tangent.x);

            var rng = new Mulberry32(edgeSeed);
            var t = tabSize;
            var j = jitter;
            var a = rng.Range(-j, j);
            var b = rng.Range(-j, j);
            var c = rng.Range(-j, j);
            var d = rng.Range(-j, j);
            var e = rng.Range(-j, j);
            // Random sign: tab pokes outward (+) or inward (-). Both neighbours see the
            // same polyline; only traversal direction differs.
            var s = rng.NextFloat() > 0.5f ? 1f : -1f;

            // 10 control points in (u, w) coordinates, matching demo's genJigsaw.
            Span<float2> ctrl = stackalloc float2[10];
            ctrl[0] = At(p0, tangent, perp, 0f, 0f);
            ctrl[1] = At(p0, tangent, perp, 0.2f, s * a);
            ctrl[2] = At(p0, tangent, perp, 0.5f + b + d, s * (-t + c));
            ctrl[3] = At(p0, tangent, perp, 0.5f - t + b, s * (t + c));
            ctrl[4] = At(p0, tangent, perp, 0.5f - 2f * t + b - d, s * (3f * t + c));
            ctrl[5] = At(p0, tangent, perp, 0.5f + 2f * t + b - d, s * (3f * t + c));
            ctrl[6] = At(p0, tangent, perp, 0.5f + t + b, s * (t + c));
            ctrl[7] = At(p0, tangent, perp, 0.5f + b + d, s * (-t + c));
            ctrl[8] = At(p0, tangent, perp, 0.8f, s * e);
            ctrl[9] = At(p0, tangent, perp, 1f, 0f);

            var idx = 0;
            SampleCubic(ctrl[0], ctrl[1], ctrl[2], ctrl[3], bezierSubdivisions, dest, ref idx, includeStart: true);
            SampleCubic(ctrl[3], ctrl[4], ctrl[5], ctrl[6], bezierSubdivisions, dest, ref idx, includeStart: false);
            SampleCubic(ctrl[6], ctrl[7], ctrl[8], ctrl[9], bezierSubdivisions, dest, ref idx, includeStart: false);
        }

        private static float2 At(float2 origin, float2 tangent, float2 perp, float u, float w)
        {
            return origin + u * tangent + w * perp;
        }

        private static void SampleCubic(
            float2 p0,
            float2 p1,
            float2 p2,
            float2 p3,
            int subdivisions,
            Span<float2> dest,
            ref int idx,
            bool includeStart)
        {
            var startK = includeStart ? 0 : 1;
            for (var k = startK; k <= subdivisions; k++)
            {
                var t = (float)k / subdivisions;
                var u = 1f - t;
                var b0 = u * u * u;
                var b1 = 3f * u * u * t;
                var b2 = 3f * u * t * t;
                var b3 = t * t * t;
                dest[idx++] = b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
            }
        }
    }
}
