using System.Runtime.InteropServices;
using UnityEngine;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Browser-side bridge for the WebGL build: reads the page query string for boot-time deep-link
    ///     restore and live-mirrors the current state into the address bar via <c>history.replaceState</c>
    ///     (no reload), so the URL is always a shareable link with no copy button needed. Every method
    ///     degrades to a safe no-op outside WebGL (e.g. in the editor), so callers stay platform-agnostic.
    /// </summary>
    internal static class TesseraWeb
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void TesseraReplaceQuery(string query);
#endif

        /// <summary>The page query string including the leading <c>?</c>, or empty outside a browser.</summary>
        public static string GetQuery()
        {
            var url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return string.Empty;
            var q = url.IndexOf('?');
            if (q < 0) return string.Empty;
            var hash = url.IndexOf('#', q);
            return hash >= 0 ? url.Substring(q, hash - q) : url.Substring(q);
        }

        /// <summary>Reflect the given query body into the address bar without reloading (keeps the hash).</summary>
        public static void ReplaceQuery(string query)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TesseraReplaceQuery(query ?? string.Empty);
#endif
        }
    }
}
