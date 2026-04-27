using System.Collections.Generic;
using Appegy.Lattice;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Handles mouse input (Play + Edit mode) and renders cell highlight fills.
/// Owned by TessellationDebugView.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TessellationCellHighlighter : MonoBehaviour
{
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private BoxCollider2D _collider;

    private TessellationDebugView _view;
    private Tessellation _tess;
    private Vector2 _gridCenter;
    private (int X, int Y)? _lastHovered;

    public void Init(TessellationDebugView view, Vector2 gridSize)
    {
        _view = view;
        _tess = view.Tessellation;
        _gridCenter = view.GridCenter;

        EnsureComponents();

        // Setup collider to cover the grid area
        if (_collider == null)
            _collider = gameObject.GetComponent<BoxCollider2D>();
        if (_collider == null)
            _collider = gameObject.AddComponent<BoxCollider2D>();

        _collider.size = gridSize;
        _collider.offset = Vector2.zero;

        ClearHighlight();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui += OnSceneGUI;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnSceneGUI;
#endif
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (_view == null) return;

        var cam = Camera.main;
        if (cam == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        var mousePos = mouse.position.ReadValue();
        var worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -cam.transform.position.z));
        ProcessWorldPoint(new Vector2(worldPos.x, worldPos.y));
    }

#if UNITY_EDITOR
    private void OnSceneGUI(SceneView sceneView)
    {
        if (Application.isPlaying) return;
        if (_view == null) return;

        var e = Event.current;
        if (e == null) return;

        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        // For 2D, intersect ray with z=0 plane
        if (Mathf.Approximately(ray.direction.z, 0)) return;
        var t = -ray.origin.z / ray.direction.z;
        var worldPoint = ray.origin + ray.direction * t;

        ProcessWorldPoint(new Vector2(worldPoint.x, worldPoint.y));

        // Request repaint so highlighting updates smoothly
        sceneView.Repaint();
    }
#endif

    private void ProcessWorldPoint(Vector2 worldPoint)
    {
        // Convert world point to tessellation space (add back grid center offset)
        var tessPoint = worldPoint + _gridCenter;
        var cell = _tess.ToCell(tessPoint.x, tessPoint.y);

        // Check if cell is within grid bounds
        if (cell.X < 0 || cell.X >= _view.Width || cell.Y < 0 || cell.Y >= _view.Height)
        {
            if (_lastHovered.HasValue)
            {
                ClearHighlight();
                _lastHovered = null;
            }
            return;
        }

        // Skip rebuild if same cell
        if (_lastHovered.HasValue && _lastHovered.Value.X == cell.X && _lastHovered.Value.Y == cell.Y)
            return;

        _lastHovered = cell;
        RebuildHighlightMesh(cell);
    }

    private void RebuildHighlightMesh((int X, int Y) hoveredCell)
    {
        EnsureComponents();

        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Color>();

        // Fill hovered cell
        AddCellFill(hoveredCell, _view.HoveredColor, vertices, indices, colors);

        // Fill neighbor cells
        foreach (var neighbor in _tess.GetNeighbors(hoveredCell))
        {
            if (neighbor.X < 0 || neighbor.X >= _view.Width || neighbor.Y < 0 || neighbor.Y >= _view.Height)
                continue;
            AddCellFill(neighbor, _view.NeighborColor, vertices, indices, colors);
        }

        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetColors(colors);
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
    }

    private void AddCellFill((int X, int Y) cell, Color color,
        List<Vector3> vertices, List<int> indices, List<Color> colors)
    {
        var cornersCount = _tess.CornersCount;
        var centerPoint = _tess.ToPoint2(cell);
        var cx = centerPoint.X - _gridCenter.x;
        var cy = centerPoint.Y - _gridCenter.y;

        // Triangle fan from center
        var baseIdx = vertices.Count;
        vertices.Add(new Vector3(cx, cy, 0.01f)); // center vertex, slightly behind lines
        colors.Add(color);

        for (var i = 0; i < cornersCount; i++)
        {
            var corner = _tess.GetCornerPoint(cell, i);
            vertices.Add(new Vector3(corner.X - _gridCenter.x, corner.Y - _gridCenter.y, 0.01f));
            colors.Add(color);
        }

        // Fan triangles: center → corner[i] → corner[i+1]
        for (var i = 0; i < cornersCount; i++)
        {
            indices.Add(baseIdx); // center
            indices.Add(baseIdx + 1 + i);
            indices.Add(baseIdx + 1 + (i + 1) % cornersCount);
        }
    }

    private void ClearHighlight()
    {
        EnsureComponents();
        _mesh.Clear();
    }

    private void EnsureComponents()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "CellHighlight" };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        if (_meshRenderer.sharedMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _meshRenderer.sharedMaterial = new Material(shader);
        }
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }

#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnSceneGUI;
#endif
    }
}
