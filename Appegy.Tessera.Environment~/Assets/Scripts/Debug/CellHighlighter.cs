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
    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private GridDebugView _view;

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
        if (n < 3) return;

        var corners = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            var c = _grid.GetCorner(id, i);
            corners[i] = new Vector2(c.x - _gridCenter.x, c.y - _gridCenter.y);
        }

        var baseIdx = vertices.Count;
        for (var i = 0; i < n; i++)
        {
            vertices.Add(new Vector3(corners[i].x, corners[i].y, 0.01f));
            colors.Add(color);
        }

        TriangulateEarClipping(corners, indices, baseIdx);
    }

    // Ear-clipping for a simple polygon in CW order (Y-up frame). Handles
    // concave polygons such as puzzle pieces where neighbour tabs poke inward.
    // O(n^2) which is fine for typical cell corner counts (<= a few hundred).
    private static void TriangulateEarClipping(Vector2[] poly, List<int> indices, int baseIdx)
    {
        var n = poly.Length;
        if (n < 3) return;
        if (n == 3)
        {
            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            return;
        }

        var prev = new int[n];
        var next = new int[n];
        for (var i = 0; i < n; i++)
        {
            prev[i] = (i - 1 + n) % n;
            next[i] = (i + 1) % n;
        }

        var remaining = n;
        var head = 0;
        var safety = n * 2;
        while (remaining > 3 && safety-- > 0)
        {
            var earFound = false;
            var v = head;
            for (var iter = 0; iter < remaining; iter++)
            {
                if (IsEar(poly, prev[v], v, next[v], next, prev))
                {
                    indices.Add(baseIdx + prev[v]);
                    indices.Add(baseIdx + v);
                    indices.Add(baseIdx + next[v]);
                    next[prev[v]] = next[v];
                    prev[next[v]] = prev[v];
                    if (v == head) head = next[v];
                    remaining--;
                    earFound = true;
                    break;
                }
                v = next[v];
            }
            if (!earFound) break;
        }

        if (remaining == 3)
        {
            indices.Add(baseIdx + prev[head]);
            indices.Add(baseIdx + head);
            indices.Add(baseIdx + next[head]);
        }
    }

    private static bool IsEar(Vector2[] poly, int pi, int ci, int ni, int[] next, int[] prev)
    {
        var a = poly[pi];
        var b = poly[ci];
        var c = poly[ni];

        // Convex check for CW input (Y-up): cross(ab, bc) < 0.
        var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
        if (cross >= 0f) return false;

        // No other remaining polygon vertex lies inside triangle abc.
        var v = next[ni];
        while (v != pi)
        {
            if (PointInTriangle(poly[v], a, b, c)) return false;
            v = next[v];
        }
        return true;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        var d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        var d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        var hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        var hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
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