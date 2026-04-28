using System.Collections.Generic;
using Appegy.Tessera;
using UnityEngine;

/// <summary>
///     Builds the grid edge mesh from an <see cref="IGrid" />.
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

    private static HashSet<(Vector2, Vector2)> CollectEdges(IGrid grid)
    {
        var edges = new HashSet<(Vector2, Vector2)>();
        for (var id = 0; id < grid.CellCount; id++)
        {
            var n = grid.GetCornersCount(id);
            for (var c = 0; c < n; c++)
            {
                var p1 = grid.GetCorner(id, c);
                var p2 = grid.GetCorner(id, (c + 1) % n);

                var a = Round(new Vector2(p1.x, p1.y));
                var b = Round(new Vector2(p2.x, p2.y));

                var edge = a.x < b.x || Mathf.Approximately(a.x, b.x) && a.y < b.y
                    ? (a, b)
                    : (b, a);
                edges.Add(edge);
            }
        }
        return edges;
    }

    private static Vector2 Round(Vector2 v)
    {
        return new Vector2(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f);
    }
}