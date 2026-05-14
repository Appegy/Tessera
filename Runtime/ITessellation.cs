using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     A finite, immutable tessellation of the plane: a collection of cells whose adjacency forms
    ///     a connected planar graph. Cells are identified by dense integer ids in the range
    ///     <c>[0, CellCount)</c>. Geometry (the clockwise corner polyline of each cell) and topology
    ///     (the neighbour graph) are kept conceptually separate: <see cref="GetCornersCount" /> and
    ///     <see cref="GetNeighborCount" /> are independent counts. For currently shipped grids they
    ///     happen to be equal because every cell is a simple polygon.
    /// </summary>
    public interface ITessellation
    {
        /// <summary>Number of cells in the tessellation.</summary>
        int CellCount { get; }

        /// <summary>Axis-aligned rectangle containing all cells in tessellation-local coordinates.</summary>
        Bounds2 Bounds { get; }

        /// <summary>Centre of the cell.</summary>
        float2 GetCenter(int id);

        /// <summary>Number of corner vertices on the cell's clockwise outline polyline.</summary>
        int GetCornersCount(int id);

        /// <summary>Corner vertex by index. Order is stable, clockwise. Wraps via modulo.</summary>
        float2 GetCorner(int id, int cornerIndex);

        /// <summary>
        ///     Copies all corner vertices into <paramref name="dest" />. <c>dest.Length</c> must be at least
        ///     <see cref="GetCornersCount" />.
        /// </summary>
        void CopyCorners(int id, Span<float2> dest);

        /// <summary>
        ///     Number of topological adjacency slots (one slot per shared boundary, including boundary slots
        ///     marked with <c>-1</c>). Always &lt;= <see cref="GetCornersCount" />. For polygonal grids
        ///     (square, hex, Voronoi) it equals the corner count.
        /// </summary>
        int GetNeighborCount(int id);

        /// <summary>
        ///     Neighbour cell across the shared boundary identified by <paramref name="neighborIndex" />.
        ///     Returns <c>-1</c> if that boundary lies on the tessellation edge (no neighbour). Wraps via modulo.
        /// </summary>
        int GetNeighbor(int id, int neighborIndex);

        /// <summary>Returns true iff <paramref name="a" /> and <paramref name="b" /> share an edge.</summary>
        bool AreNeighbors(int a, int b);

        /// <summary>
        ///     Position of <paramref name="neighbor" /> in <paramref name="cell" />'s neighbour list.
        ///     Returns <c>-1</c> if the cells are not neighbours.
        /// </summary>
        int GetNeighborIndex(int cell, int neighbor);

        /// <summary>Returns the id of the cell containing <paramref name="point" />, or <c>-1</c> if outside the tessellation.</summary>
        int GetCellAt(float2 point);

        /// <summary>Minimum number of cell-to-cell hops between <paramref name="a" /> and <paramref name="b" />.</summary>
        int Distance(int a, int b);
    }
}