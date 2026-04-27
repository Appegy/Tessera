using Appegy.Union;

namespace Appegy.Tessera
{
    [Union(typeof(SquareTessellation), typeof(HexagonalTessellation))]
    [Expose(typeof(ITessellation))]
    public partial struct Tessellation
    {
    }
}
