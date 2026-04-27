using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Generic grid: a finite, immutable collection of cells whose adjacency forms a connected planar graph.
    ///     Cells are identified by dense integer ids in the range <c>[0, CellCount)</c>.
    ///     See <c>Documentation~/grid-api-redesign.md</c> for the full contract and rationale.
    /// </summary>
    public interface IGrid
    {
        /// <summary>Number of cells in the grid.</summary>
        int CellCount { get; }

        /// <summary>Axis-aligned rectangle containing all cells in grid-local coordinates.</summary>
        GridBounds Bounds { get; }

        /// <summary>Wraps an id into a <see cref="Cell" /> facade for ergonomic access.</summary>
        Cell GetCell(int id);

        /// <summary>Centre of the cell.</summary>
        float2 GetCenter(int id);

        /// <summary>Number of corner vertices. Equals the number of edges and the number of neighbour slots.</summary>
        int GetCornersCount(int id);

        /// <summary>Corner vertex by index. Order is stable, clockwise.</summary>
        float2 GetCorner(int id, int cornerIndex);

        /// <summary>
        ///     Copies all corner vertices into <paramref name="dest" />. <c>dest.Length</c> must be at least
        ///     <see cref="GetCornersCount" />.
        /// </summary>
        void CopyCorners(int id, Span<float2> dest);

        /// <summary>
        ///     Neighbour cell across the edge from corner <paramref name="neighborIndex" /> to corner
        ///     <c>(neighborIndex + 1) % GetCornersCount(id)</c>. Returns <c>-1</c> if that edge is on the
        ///     grid boundary (no neighbour).
        /// </summary>
        int GetNeighbor(int id, int neighborIndex);

        /// <summary>Returns true iff <paramref name="a" /> and <paramref name="b" /> share an edge.</summary>
        bool AreNeighbors(int a, int b);

        /// <summary>
        ///     Position of <paramref name="neighbor" /> in <paramref name="cell" />'s neighbour list.
        ///     Returns <c>-1</c> if the cells are not neighbours.
        /// </summary>
        int GetNeighborIndex(int cell, int neighbor);

        /// <summary>Returns the id of the cell containing <paramref name="point" />, or <c>-1</c> if outside the grid.</summary>
        int GetCellAt(float2 point);

        /// <summary>Minimum number of cell-to-cell hops between <paramref name="a" /> and <paramref name="b" />.</summary>
        int Distance(int a, int b);
    }
}