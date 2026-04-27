using System;
using Unity.Mathematics;

namespace Appegy.Tessera
{
    /// <summary>
    ///     Irregular cell grid based on a centroidal Voronoi tessellation clipped to a rectangular bounds.
    ///     See <c>Documentation~/voronoi-grid-design.md</c>.
    /// </summary>
    public sealed class VoronoiGrid : IGrid
    {
        public VoronoiGrid(Bounds2 bounds, int cellCount, int seed, int relaxationIterations)
        {
            throw new NotImplementedException();
        }

        public int CellCount => throw new NotImplementedException();
        public Bounds2 Bounds => throw new NotImplementedException();
        public Cell GetCell(int id) => throw new NotImplementedException();
        public float2 GetCenter(int id) => throw new NotImplementedException();
        public int GetCornersCount(int id) => throw new NotImplementedException();
        public float2 GetCorner(int id, int cornerIndex) => throw new NotImplementedException();
        public void CopyCorners(int id, Span<float2> dest) => throw new NotImplementedException();
        public int GetNeighbor(int id, int neighborIndex) => throw new NotImplementedException();
        public bool AreNeighbors(int a, int b) => throw new NotImplementedException();
        public int GetNeighborIndex(int cell, int neighbor) => throw new NotImplementedException();
        public int GetCellAt(float2 point) => throw new NotImplementedException();
        public int Distance(int a, int b) => throw new NotImplementedException();
    }
}
