using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Renders an <see cref="ITessellation" /> as two meshes: crisp edge outlines and a translucent
    ///     fill used to highlight a cell and its neighbours. Runtime-only (no editor callbacks). Owns
    ///     two child mesh objects so a scene only needs this component plus a material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField] private Material _material;

        private Vector2 _center;
        private ITessellation _grid;

        private Mesh _edgeMesh;
        private MeshFilter _edgeFilter;
        private Mesh _fillMesh;
        private MeshFilter _fillFilter;

        private readonly List<Vector3> _verts = new();
        private readonly List<int> _indices = new();
        private readonly List<Color> _colors = new();
        private int[] _prevBuf = Array.Empty<int>();
        private int[] _nextBuf = Array.Empty<int>();
        private float2[] _cornersBuf = Array.Empty<float2>();

        // Per-cell triangulation cache, rebuilt once per grid and reused on every highlight change.
        private Vector3[][] _cellVerts;
        private int[][] _cellTris;
        private readonly List<int> _trisScratch = new();

        private int _highlighted = -1;

        public float LineWidth { get; set; } = 0.04f;
        public Color LineColor { get; set; } = Color.white;

        public ITessellation Grid => _grid;
        public Vector2 GridCenter => _center;

        private void Awake() => EnsureChildren();

        private void OnDestroy()
        {
            DestroyMesh(_edgeMesh);
            DestroyMesh(_fillMesh);
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        /// <summary>Switch to a new grid: rebuild the edge mesh, the triangulation cache, and clear the highlight.</summary>
        public void SetGrid(ITessellation grid)
        {
            _grid = grid;
            var bounds = grid.Bounds;
            _center = new Vector2(bounds.Center.x, bounds.Center.y);
            RebuildEdges();
            BuildTriangulationCache();
            ClearHighlight();
        }

        /// <summary>Rebuild only the edge mesh, e.g. after line width or colour changed.</summary>
        public void RefreshAppearance()
        {
            if (_grid != null) RebuildEdges();
        }

        public void SetHighlight(int id, Color hoveredColor, Color neighborColor)
        {
            if (_grid == null) return;
            if (id == _highlighted) return;
            _highlighted = id;

            EnsureChildren();
            _verts.Clear();
            _indices.Clear();
            _colors.Clear();

            if (id >= 0)
            {
                AppendFill(id, hoveredColor);
                var neighbors = _grid.GetNeighborCount(id);
                for (var i = 0; i < neighbors; i++)
                {
                    var nb = _grid.GetNeighbor(id, i);
                    if (nb != -1) AppendFill(nb, neighborColor);
                }
            }

            _fillMesh.Clear();
            _fillMesh.SetVertices(_verts);
            _fillMesh.SetColors(_colors);
            _fillMesh.SetIndices(_indices, MeshTopology.Triangles, 0);
        }

        public void ClearHighlight()
        {
            _highlighted = -1;
            EnsureChildren();
            _fillMesh.Clear();
        }

        private void RebuildEdges()
        {
            EnsureChildren();
            _verts.Clear();
            _indices.Clear();
            _colors.Clear();

            var halfWidth = LineWidth * 0.5f;
            for (var id = 0; id < _grid.CellCount; id++)
                GridMeshBuilder.AppendCellOutline(_grid, id, _center, halfWidth, LineColor, _verts, _indices, _colors);

            _edgeMesh.Clear();
            _edgeMesh.indexFormat = _verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _edgeMesh.SetVertices(_verts);
            _edgeMesh.SetColors(_colors);
            _edgeMesh.SetIndices(_indices, MeshTopology.Triangles, 0);
        }

        private void BuildTriangulationCache()
        {
            var count = _grid.CellCount;
            if (_cellVerts == null || _cellVerts.Length != count)
            {
                _cellVerts = new Vector3[count][];
                _cellTris = new int[count][];
            }

            for (var id = 0; id < count; id++)
            {
                var n = _grid.GetCornersCount(id);
                if (n < 3)
                {
                    _cellVerts[id] = Array.Empty<Vector3>();
                    _cellTris[id] = Array.Empty<int>();
                    continue;
                }

                if (_cornersBuf.Length < n) _cornersBuf = new float2[n];
                var verts = new Vector3[n];
                for (var i = 0; i < n; i++)
                {
                    var c = _grid.GetCorner(id, i);
                    var x = c.x - _center.x;
                    var y = c.y - _center.y;
                    _cornersBuf[i] = new float2(x, y);
                    verts[i] = new Vector3(x, y, -0.01f);
                }
                _cellVerts[id] = verts;

                _trisScratch.Clear();
                EarClipping.Triangulate(_cornersBuf, n, _trisScratch, 0, ref _prevBuf, ref _nextBuf);
                _cellTris[id] = _trisScratch.ToArray();
            }
        }

        private void AppendFill(int id, Color color)
        {
            if (_cellVerts == null || id < 0 || id >= _cellVerts.Length) return;
            var verts = _cellVerts[id];
            var tris = _cellTris[id];
            if (verts.Length < 3) return;

            var baseIdx = _verts.Count;
            for (var i = 0; i < verts.Length; i++)
            {
                _verts.Add(verts[i]);
                _colors.Add(color);
            }
            for (var i = 0; i < tris.Length; i++)
                _indices.Add(baseIdx + tris[i]);
        }

        private void EnsureChildren()
        {
            if (_edgeFilter == null)
            {
                _edgeMesh = new Mesh { name = "GridEdges" };
                _edgeFilter = CreateChild("Edges", sortingOrder: 1, out _);
                _edgeFilter.sharedMesh = _edgeMesh;
            }
            if (_fillFilter == null)
            {
                _fillMesh = new Mesh { name = "GridHighlight" };
                _fillMesh.MarkDynamic();
                _fillFilter = CreateChild("Highlight", sortingOrder: 0, out _);
                _fillFilter.sharedMesh = _fillMesh;
            }
        }

        private MeshFilter CreateChild(string childName, int sortingOrder, out MeshRenderer renderer)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSave;
            var filter = go.AddComponent<MeshFilter>();
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return filter;
        }
    }
}