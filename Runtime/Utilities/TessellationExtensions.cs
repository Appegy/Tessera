using System.Collections.Generic;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Convenience helpers on top of <see cref="ITessellation" />.
    ///     Members are added when concrete consumers demand them, not speculatively.
    /// </summary>
    public static class TessellationExtensions
    {
        /// <summary>Yields all valid neighbours of <paramref name="id" />, skipping boundary slots (the <c>-1</c>s).</summary>
        public static IEnumerable<int> Neighbors(this ITessellation tessellation, int id)
        {
            var n = tessellation.GetNeighborCount(id);
            for (var i = 0; i < n; i++)
            {
                var nb = tessellation.GetNeighbor(id, i);
                if (nb >= 0) yield return nb;
            }
        }
    }
}
