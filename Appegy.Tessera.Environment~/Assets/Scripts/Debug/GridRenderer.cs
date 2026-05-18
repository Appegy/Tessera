using System.Collections.Generic;
using Appegy.Tessera;
using UnityEngine;

/// <summary>
///     Builds the grid edge mesh from an <see cref="ITessellation" />.
///     Owned by GridDebugView.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridRenderer : MonoBehaviour
{
    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }

    public void Rebuild(GridDebugView view)
    {
        EnsureComponents();

        var grid = view.Grid;
        var center = view.GridCenter;
        var edges = CollectEdges(grid);

        var halfWidth = view.LineWidth * 0.5f;
        var vertices = new List<Vector3>(edges.Count * 4);
        var indices = new List<int>(edges.Count * 6);
        var colors = new List<Color>(edges.Count * 4);
        var lineColor = view.LineColor;

        foreach (var (a, b) in edges)
        {
            var p0 = new Vector2(a.x - center.x, a.y - center.y);
            var p1 = new Vector2(b.x - center.x, b.y - center.y);

            var dir = (p1 - p0).normalized;
            var perp = new Vector2(-dir.y, dir.x) * halfWidth;

            var idx = vertices.Count;
            vertices.Add(new Vector3(p0.x + perp.x, p0.y + perp.y, 0));
            vertices.Add(new Vector3(p0.x - perp.x, p0.y - perp.y, 0));
            vertices.Add(new Vector3(p1.x + perp.x, p1.y + perp.y, 0));
            vertices.Add(new Vector3(p1.x - perp.x, p1.y - perp.y, 0));

            indices.Add(idx);
            indices.Add(idx + 2);
            indices.Add(idx + 1);
            indices.Add(idx + 1);
            indices.Add(idx + 2);
            indices.Add(idx + 3);

            colors.Add(lineColor);
            colors.Add(lineColor);
            colors.Add(lineColor);
            colors.Add(lineColor);
        }

        if (_mesh == null)
            _mesh = new Mesh { name = "GridEdges" };
        else
            _mesh.Clear();

        _mesh.SetVertices(vertices);
        _mesh.SetColors(colors);
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        _meshFilter.sharedMesh = _mesh;
    }

    private void EnsureComponents()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshRenderer.sharedMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _meshRenderer.sharedMaterial = new Material(shader);
        }
    }

    // Each interior edge is reported by both cells that share it. We dedupe by the
    // (sorted) pair of endpoint coordinates. The previous version rounded to 1/1000
    // before hashing, which collapses too aggressively at very small cellSize and
    // could merge distinct segments. Now we match endpoints bit-exactly: for grids
    // whose adjacent cells produce identical corner values (square, hex, puzzle —
    // both cells reference the same float2[] for the shared edge) this dedupes
    // cleanly. For grids whose endpoints differ by epsilon (Voronoi seam vertices)
    // dedup misses and the edge gets drawn twice — visually identical.
    private static List<(Vector2, Vector2)> CollectEdges(ITessellation grid)
    {
        var seen = new HashSet<(float, float, float, float)>();
        var edges = new List<(Vector2, Vector2)>();
        for (var id = 0; id < grid.CellCount; id++)
        {
            var n = grid.GetCornersCount(id);
            for (var c = 0; c < n; c++)
            {
                var p1 = grid.GetCorner(id, c);
                var p2 = grid.GetCorner(id, (c + 1) % n);
                var key = p1.x < p2.x || p1.x == p2.x && p1.y < p2.y
                    ? (p1.x, p1.y, p2.x, p2.y)
                    : (p2.x, p2.y, p1.x, p1.y);
                if (seen.Add(key))
                {
                    edges.Add((new Vector2(key.Item1, key.Item2), new Vector2(key.Item3, key.Item4)));
                }
            }
        }
        return edges;
    }
}