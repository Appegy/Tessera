using System;
using System.Collections.Generic;
using Appegy.Tessera;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///     Mouse hover highlight for an <see cref="ITessellation" />: paints the hovered cell + its neighbours.
///     Owned by GridDebugView. Works in Play and Edit modes.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CellHighlighter : MonoBehaviour
{
    private BoxCollider2D _collider;
    private ITessellation _grid;
    private Vector2 _gridCenter;
    private int _lastHovered = -1;
    private Vector2 _lastMousePos = new(float.NaN, float.NaN);
    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Camera _camera;

    // Buffers reused across every RebuildHighlightMesh call so hovering produces
    // zero per-frame GC allocations once the lists / arrays have grown to peak size.
    private readonly List<Vector3> _verticesBuf = new();
    private readonly List<int> _indicesBuf = new();
    private readonly List<Color> _colorsBuf = new();
    private float2[] _cornersBuf = Array.Empty<float2>();
    private int[] _earPrevBuf = Array.Empty<int>();
    private int[] _earNextBuf = Array.Empty<int>();

    // Per-cell triangulation cache: rebuilt once in Init(), reused on every hover.
    // Avoids running ear-clipping ~70k ops per hover change.
    private Vector3[][] _cellVertsCache;
    private int[][] _cellTrisCache;
    private readonly List<int> _trisScratch = new();

    private GridDebugView _view;

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (_view == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        var mousePos = mouse.position.ReadValue();
        // Skip when the cursor hasn't moved since last frame: the cell under it
        // can't have changed either, so all the GetCellAt + rebuild work is wasted.
        if (mousePos == _lastMousePos) return;
        _lastMousePos = mousePos;

        // Camera.main does an internal FindGameObjectWithTag; cache it.
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        var worldPos = _camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -_camera.transform.position.z));
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

    public void Init(GridDebugView view, Vector2 gridSize)
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
        _lastMousePos = new Vector2(float.NaN, float.NaN);
        BuildTriangulationCache();
        ClearHighlight();
    }

    // One-time cost when the grid changes; afterwards every hover just appends
    // precomputed vertices and indices to the mesh buffers.
    private void BuildTriangulationCache()
    {
        var cellCount = _grid.CellCount;
        if (_cellVertsCache == null || _cellVertsCache.Length != cellCount)
        {
            _cellVertsCache = new Vector3[cellCount][];
            _cellTrisCache = new int[cellCount][];
        }
        for (var id = 0; id < cellCount; id++)
        {
            var n = _grid.GetCornersCount(id);
            if (n < 3)
            {
                _cellVertsCache[id] = Array.Empty<Vector3>();
                _cellTrisCache[id] = Array.Empty<int>();
                continue;
            }

            if (_cornersBuf.Length < n) _cornersBuf = new float2[n];
            var verts = new Vector3[n];
            for (var i = 0; i < n; i++)
            {
                var c = _grid.GetCorner(id, i);
                var x = c.x - _gridCenter.x;
                var y = c.y - _gridCenter.y;
                _cornersBuf[i] = new float2(x, y);
                verts[i] = new Vector3(x, y, 0.01f);
            }
            _cellVertsCache[id] = verts;

            _trisScratch.Clear();
            EarClipping.Triangulate(_cornersBuf, n, _trisScratch, 0, ref _earPrevBuf, ref _earNextBuf);
            _cellTrisCache[id] = _trisScratch.ToArray();
        }
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

        _verticesBuf.Clear();
        _indicesBuf.Clear();
        _colorsBuf.Clear();

        AddCellFill(hoveredId, _view.HoveredColor);

        // Walk the neighbour list, not the corner polyline. For non-polygonal grids
        // (DraradechPuzzleGrid) GetCornersCount >> GetNeighborCount; using the former
        // here used to iterate ~120 times and re-render each of the 4 real
        // neighbours 30 times per hover, killing performance.
        var neighborCount = _grid.GetNeighborCount(hoveredId);
        for (var i = 0; i < neighborCount; i++)
        {
            var nb = _grid.GetNeighbor(hoveredId, i);
            if (nb == -1) continue;
            AddCellFill(nb, _view.NeighborColor);
        }

        _mesh.Clear();
        _mesh.SetVertices(_verticesBuf);
        _mesh.SetColors(_colorsBuf);
        _mesh.SetIndices(_indicesBuf, MeshTopology.Triangles, 0);
    }

    private void AddCellFill(int id, Color color)
    {
        if (_cellVertsCache == null || id < 0 || id >= _cellVertsCache.Length) return;
        var verts = _cellVertsCache[id];
        var tris = _cellTrisCache[id];
        if (verts.Length < 3) return;

        var baseIdx = _verticesBuf.Count;
        for (var i = 0; i < verts.Length; i++)
        {
            _verticesBuf.Add(verts[i]);
            _colorsBuf.Add(color);
        }
        for (var i = 0; i < tris.Length; i++)
        {
            _indicesBuf.Add(baseIdx + tris[i]);
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