using System.Collections.Generic;

namespace Appegy.Tessera
{
    /// <summary>
    /// Unified geometry interface for regular tessellations (square, hexagon).
    /// All cell coordinates use Offset system (int X, int Y).
    /// All pixel positions use (float X, float Y).
    /// </summary>
    public interface ITessellation
    {
        /// <summary>
        /// Number of neighbor directions for this tessellation type.
        /// Square=4 or 8 (with diagonals), Hexagon=6 or 3 (Even/Odd mode).
        /// </summary>
        int DirectionsCount { get; }

        /// <summary>
        /// Number of corners per cell.
        /// Square=4, Hexagon=6.
        /// </summary>
        int CornersCount { get; }

        /// <summary>
        /// Returns the neighbor cell in the given direction.
        /// Direction index wraps via modulo (e.g. direction=6 is same as 0 for hexagons).
        /// Direction 0 starts at top/top-right and goes clockwise.
        /// </summary>
        (int X, int Y) GetNeighbor((int X, int Y) cell, int direction);

        /// <summary>
        /// Returns all neighbor cells (count equals DirectionsCount).
        /// </summary>
        IEnumerable<(int X, int Y)> GetNeighbors((int X, int Y) cell);

        /// <summary>
        /// Returns true if cells a and b are direct neighbors. Same cell returns false.
        /// </summary>
        bool AreNeighbors((int X, int Y) a, (int X, int Y) b);

        /// <summary>
        /// Converts cell offset coordinates to pixel position (center of the cell).
        /// </summary>
        (float X, float Y) ToPoint2((int X, int Y) cell);

        /// <summary>
        /// Converts pixel position to the cell that contains this point.
        /// Inverse of ToPoint2: ToCell(ToPoint2(cell)) == cell.
        /// </summary>
        (int X, int Y) ToCell(float x, float y);

        /// <summary>
        /// Returns pixel position of a corner vertex.
        /// Corner index wraps via modulo. Corners are ordered clockwise.
        /// </summary>
        (float X, float Y) GetCornerPoint((int X, int Y) cell, int cornerIndex);

        /// <summary>
        /// Returns the minimum number of steps (through adjacent cells) from a to b.
        /// Square 4-dir uses Manhattan, 8-dir uses Chebyshev.
        /// Hexagon uses cubic distance.
        /// </summary>
        int Distance((int X, int Y) a, (int X, int Y) b);

        /// <summary>
        /// Returns the direction index from cell to neighbor.
        /// Inverse of GetNeighbor: GetDirection(cell, GetNeighbor(cell, dir)) == dir.
        /// Throws ArgumentException if cells are not neighbors.
        /// </summary>
        int GetDirection((int X, int Y) cell, (int X, int Y) neighbor);

        /// <summary>
        /// Returns the opposite direction index.
        /// For Square and Hex All mode: GetNeighbor(GetNeighbor(cell, dir), GetOppositeDirection(dir)) == cell.
        /// For Hex Even/Odd: returns the physical opposite direction (0–5), which belongs to the complementary mode.
        /// </summary>
        int GetOppositeDirection(int direction);
    }
}
