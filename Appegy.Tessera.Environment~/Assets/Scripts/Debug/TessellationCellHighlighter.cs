using System.Collections.Generic;
using Appegy.Tessera;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///     Mouse hover highlight for an <see cref="IGrid" />: paints the hovered cell + its neighbours.
///     Owned by TessellationDebugView. Works in Play and Edit modes.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TessellationCellHighlighter : MonoBehaviour
{
    private BoxCollider2D _collider;
    private IGrid _grid;
    private Vector2 _gridCenter;
    private int _lastHovered = -1;
    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private TessellationDebugView _view;

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

    public void Init(TessellationDebugView view, Vector2 gridSize)
    {
        _view = view;
        _grid = view.Grid;
        _gridCenter = view.GridCenter;

        EnsureComponents();

        if (_collider == null) _collider = gameObject.GetComponent<BoxCollider2D>();
        if (_collider == null) _collider = gameObject.AddComponent<BoxCollider2D>();
        _collider.size = gridSize;
        _collider.offset = Vector2.zero;

        _lastHovered = -1;
        ClearHighlight();
    }

#if UNITY_EDITOR
    private void OnSceneGUI(SceneView sceneView)
    {
        if (Application.isPlaying) return;
        if (_view == null) return;

        var e = Event.current;
        if (e == null) return;

        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Mathf.Approximately(ray.direction.z, 0)) return;
        var t = -ray.origin.z / ray.direction.z;
        var worldPoint = ray.origin + ray.direction * t;

        ProcessWorldPoint(new Vector2(worldPoint.x, worldPoint.y));
        sceneView.Repaint();
    }
#endif

    private void ProcessWorldPoint(Vector2 worldPoint)
    {
        // The renderer draws each vertex shifted by -gridCenter so the grid sits centred at the GameObject.
        // To turn a world point back into grid-local pixel space we add gridCenter.
        var local = worldPoint + _gridCenter;
        var id = _grid.GetCellAt(new float2(local.x, local.y));

        if (id == -1)
        {
            if (_lastHovered != -1)
            {
                ClearHighlight();
                _lastHovered = -1;
            }
            return;
        }

        if (id == _lastHovered) return;
        _lastHovered = id;
        RebuildHighlightMesh(id);
    }

    private void RebuildHighlightMesh(int hoveredId)
    {
        EnsureComponents();

        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Color>();

        AddCellFill(hoveredId, _view.HoveredColor, vertices, indices, colors);

        var n = _grid.GetCornersCount(hoveredId);
        for (var i = 0; i < n; i++)
        {
            var nb = _grid.GetNeighbor(hoveredId, i);
            if (nb == -1) continue;
            AddCellFill(nb, _view.NeighborColor, vertices, indices, colors);
        }

        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetColors(colors);
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
    }

    private void AddCellFill(int id, Color color, List<Vector3> vertices, List<int> indices, List<Color> colors)
    {
        var n = _grid.GetCornersCount(id);
        var center = _grid.GetCenter(id);
        var cx = center.x - _gridCenter.x;
        var cy = center.y - _gridCenter.y;

        var baseIdx = vertices.Count;
        vertices.Add(new Vector3(cx, cy, 0.01f));
        colors.Add(color);

        for (var i = 0; i < n; i++)
        {
            var corner = _grid.GetCorner(id, i);
            vertices.Add(new Vector3(corner.x - _gridCenter.x, corner.y - _gridCenter.y, 0.01f));
            colors.Add(color);
        }

        for (var i = 0; i < n; i++)
        {
            indices.Add(baseIdx);
            indices.Add(baseIdx + 1 + i);
            indices.Add(baseIdx + 1 + (i + 1) % n);
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
}