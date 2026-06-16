using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     The set of grid demos available in the playground. Adding a new grid type is one class plus
    ///     one line here. The list is explicit (not reflection) so IL2CPP / WebGL code stripping can
    ///     never drop a module that is only referenced dynamically.
    /// </summary>
    public static class GridDemoRegistry
    {
        public static List<GridDemo> CreateAll()
            => new List<GridDemo>
            {
                new SquareGridDemo(),
                new HexagonalGridDemo(),
                new VoronoiGridDemo(),
                new ClassicPuzzleGridDemo(),
                new DraradechPuzzleGridDemo()
            };
    }
}