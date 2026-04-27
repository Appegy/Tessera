using Appegy.Union;

namespace Appegy.Lattice
{
    [Union(typeof(SquareTessellation), typeof(HexagonalTessellation))]
    [Expose(typeof(ITessellation))]
    public partial struct Tessellation
    {
    }
}
