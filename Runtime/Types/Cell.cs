using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Lightweight facade around <c>(IGrid, int id)</c> for ergonomic per-cell access.
    ///     All operations forward to the parent grid.
    /// </summary>
    public readonly struct Cell
    {
        private readonly IGrid _grid;

        public int Id { get; }

        internal Cell(IGrid grid, int id)
        {
            _grid = grid;
            Id = id;
        }

        public float2 Center => _grid.GetCenter(Id);

        public int CornersCount => _grid.GetCornersCount(Id);

        public float2 GetCorner(int i)
        {
            return _grid.GetCorner(Id, i);
        }

        public int GetNeighbor(int i)
        {
            return _grid.GetNeighbor(Id, i);
        }

        public int GetNeighborIndex(int other)
        {
            return _grid.GetNeighborIndex(Id, other);
        }

        public int DistanceTo(int other)
        {
            return _grid.Distance(Id, other);
        }

        public void CopyCorners(Span<float2> d)
        {
            _grid.CopyCorners(Id, d);
        }

        public override string ToString()
        {
            return $"Cell#{Id}";
        }
    }
}