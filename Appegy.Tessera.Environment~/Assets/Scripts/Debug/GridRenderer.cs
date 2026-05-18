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

    private readonly List<Vector3> _verticesBuf = new();
    private readonly List<int> _indicesBuf = new();
    private readonly List<Color> _colorsBuf = new();

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
        var halfWidth = view.LineWidth * 0.5f;
        var lineColor = view.LineColor;

        _verticesBuf.Clear();
        _indicesBuf.Clear();
        _colorsBuf.Clear();

        // Each cell's closed outline becomes its own mitered triangle strip. Adjacent
        // cells overdraw their shared edge once each (line color is opaque, the result
        // is identical to a deduped single pass), but consecutive segments share
        // vertices at every polyline corner so the strip has no gaps at bends.
        for (var id = 0; id < grid.CellCount; id++)
        {
            AppendCellOutline(grid, id, center, halfWidth, lineColor, _verticesBuf, _indicesBuf, _colorsBuf);
        }

        if (_mesh == null)
            _mesh = new Mesh { name = "GridEdges" };
        else
            _mesh.Clear();

        _mesh.indexFormat = _verticesBuf.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.SetVertices(_verticesBuf);
        _mesh.SetColors(_colorsBuf);
        _mesh.SetIndices(_indicesBuf, MeshTopology.Triangles, 0);
        _meshFilter.sharedMesh = _mesh;
    }

    private static void AppendCellOutline(
        ITessellation grid, int id, Vector2 center, float halfWidth, Color color,
        List<Vector3> vertices, List<int> indices, List<Color> colors)
    {
        var n = grid.GetCornersCount(id);
        if (n < 2) return;

        var baseIdx = vertices.Count;
        for (var i = 0; i < n; i++)
        {
            var prev = grid.GetCorner(id, (i - 1 + n) % n);
            var curr = grid.GetCorner(id, i);
            var next = grid.GetCorner(id, (i + 1) % n);

            var d1x = curr.x - prev.x;
            var d1y = curr.y - prev.y;
            var d1len = Mathf.Sqrt(d1x * d1x + d1y * d1y);
            if (d1len > 0f) { d1x /= d1len; d1y /= d1len; }

            var d2x = next.x - curr.x;
            var d2y = next.y - curr.y;
            var d2len = Mathf.Sqrt(d2x * d2x + d2y * d2y);
            if (d2len > 0f) { d2x /= d2len; d2y /= d2len; }

            // Outward perpendicular for CW polygon in Y-up: rotate direction -90 deg.
            // For dir = (1, 0) -> outward = (0, -1). For dir = (0, 1) -> outward = (1, 0).
            var n1x = d1y;
            var n1y = -d1x;
            var n2x = d2y;
            var n2y = -d2x;

            // Miter = halfWidth * (n1 + n2) / (1 + dot(n1, n2)). Cap denominator to avoid
            // miter spikes at near-180deg turns (not expected for cell polygons, but cheap insurance).
            var sumX = n1x + n2x;
            var sumY = n1y + n2y;
            var dot = n1x * n2x + n1y * n2y;
            var denom = 1f + dot;
            if (denom < 0.05f) denom = 0.05f;
            var mx = sumX * (halfWidth / denom);
            var my = sumY * (halfWidth / denom);

            var ox = curr.x + mx - center.x;
            var oy = curr.y + my - center.y;
            var ix = curr.x - mx - center.x;
            var iy = curr.y - my - center.y;

            vertices.Add(new Vector3(ox, oy, 0f));
            vertices.Add(new Vector3(ix, iy, 0f));
            colors.Add(color);
            colors.Add(color);
        }

        // Two triangles per segment between consecutive corner pairs. Wraps closed.
        for (var i = 0; i < n; i++)
        {
            var a = baseIdx + i * 2;
            var b = baseIdx + i * 2 + 1;
            var c = baseIdx + (i + 1) % n * 2;
            var d = baseIdx + (i + 1) % n * 2 + 1;
            indices.Add(a);
            indices.Add(c);
            indices.Add(b);
            indices.Add(b);
            indices.Add(c);
            indices.Add(d);
        }
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
}
