using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Serialises the shareable state (selected grid + that grid's parameter values) to and from a
    ///     readable query string: <c>?g=&lt;gridUrlId&gt;&amp;&lt;key&gt;=&lt;value&gt;...</c> where each
    ///     <c>key</c> is a short per-parameter code. Keyed by grid <see cref="GridDemo.UrlId" /> and the
    ///     parameter key, so a link survives parameter reordering and grid renames. Theme and other
    ///     viewing prefs are intentionally excluded: the link captures the example, not the viewer's prefs.
    /// </summary>
    public static class DemoUrlState
    {
        private const string GridKey = "g";
        private const string LineWidthKey = "lw";
        private const float DefaultLineWidth = 0.5f;

        // Short, readable query keys per parameter Id (unique within a grid). Unmapped ids fall back to
        // the Id itself. Different grids may reuse a key (e.g. "w") since only one grid is in the URL.
        private static readonly Dictionary<string, string> KeyById = new Dictionary<string, string>
        {
            { "regionWidth", "w" }, { "regionHeight", "h" }, { "cellCount", "n" }, { "relaxation", "relax" },
            { "width", "w" }, { "height", "h" }, { "layout", "layout" },
            { "columns", "cols" }, { "rows", "rows" }, { "roundness", "round" },
            { "tabRadius", "trad" }, { "tabOffset", "toff" }, { "tabDeform", "tdef" },
            { "tabSize", "tab" }, { "variation", "var" }, { "smoothness", "smooth" },
            { "seed", "seed" },
        };

        private static string Key(DemoParameter p) => KeyById.TryGetValue(p.Id, out var k) ? k : p.Id;

        /// <summary>Builds the query body (no leading <c>?</c>) for the demo's current state.</summary>
        public static string Encode(GridDemo demo, float lineWidthScale)
        {
            if (demo == null) return string.Empty;
            var sb = new StringBuilder();
            sb.Append(GridKey).Append('=').Append(demo.UrlId);
            // Only non-default values are written, so a link is short and carries just the delta from
            // defaults (e.g. ?g=voronoi&n=200). TryDecode resets the grid to defaults before applying,
            // so an omitted key means "default", never the viewer's saved value.
            foreach (var parameter in demo.Parameters)
                if (!parameter.IsAtDefault)
                    sb.Append('&').Append(Key(parameter)).Append('=').Append(Format(parameter));
            if (System.Math.Abs(lineWidthScale - DefaultLineWidth) > 0.001f)
                sb.Append('&').Append(LineWidthKey).Append('=').Append(lineWidthScale.ToString("0.###", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        ///     Parses a page query string, applies its values onto the matching grid's parameters, and
        ///     returns that grid. Returns null when the grid token is absent or names an unknown grid, in
        ///     which case the caller falls back to its own defaults/prefs.
        /// </summary>
        public static GridDemo TryDecode(string query, IReadOnlyList<GridDemo> demos, out float lineWidthScale)
        {
            lineWidthScale = DefaultLineWidth;
            if (demos == null) return null;
            var pairs = ParseQuery(query);
            if (pairs.TryGetValue(LineWidthKey, out var lwStr) &&
                float.TryParse(lwStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lw))
                lineWidthScale = lw;
            if (!pairs.TryGetValue(GridKey, out var gridId)) return null;

            GridDemo target = null;
            foreach (var demo in demos)
                if (demo.UrlId == gridId) { target = demo; break; }
            if (target == null) return null;

            // A shared link is a snapshot relative to defaults: reset the grid to defaults, then apply
            // only the keys present, so omitted (default) params don't inherit the viewer's saved prefs.
            foreach (var parameter in target.Parameters)
                parameter.ResetToDefault();
            foreach (var parameter in target.Parameters)
                if (pairs.TryGetValue(Key(parameter), out var value)) Apply(parameter, value);
            return target;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return map;
            var q = query;
            var hash = q.IndexOf('#');
            if (hash >= 0) q = q.Substring(0, hash);
            if (q.Length > 0 && q[0] == '?') q = q.Substring(1);
            foreach (var pair in q.Split('&'))
            {
                if (pair.Length == 0) continue;
                var eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                map[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            return map;
        }

        private static string Format(DemoParameter parameter)
        {
            switch (parameter)
            {
                case FloatParameter f: return f.Value.ToString("0.######", CultureInfo.InvariantCulture);
                case IntParameter i: return i.Value.ToString(CultureInfo.InvariantCulture);
                case SeedParameter s: return s.Value.ToString(CultureInfo.InvariantCulture);
                case BoolParameter b: return b.Value ? "1" : "0";
                case ChoiceParameter c: return c.SelectedIndex.ToString(CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }

        private static void Apply(DemoParameter parameter, string value)
        {
            switch (parameter)
            {
                case FloatParameter f:
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv)) f.Value = fv;
                    break;
                case IntParameter i:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv)) i.Value = iv;
                    break;
                case SeedParameter s:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sv)) s.Value = sv;
                    break;
                case BoolParameter b:
                    b.Value = value == "1";
                    break;
                case ChoiceParameter c:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cv)) c.SelectedIndex = cv;
                    break;
            }
        }
    }
}
