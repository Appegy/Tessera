using System.Collections.Generic;

namespace Appegy.Tessera
{
    public static class TessellationExtensions
    {
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
