using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Appegy.Tessera.Demo
{
    /// <summary>Lets the UI ask whether a screen point is over an interactive UI element.</summary>
    public interface IPointerOverUiProbe
    {
        bool IsPointerOverUI(Vector2 screenPosition);
    }

    /// <summary>
    ///     Headless playground model + driver: owns the grid demos, builds the active one into the
    ///     <see cref="GridView" />, frames the orthographic camera to fit, and drives mouse hover
    ///     highlighting. UI-agnostic: it raises events the UI listens to and queries an optional
    ///     over-UI probe so panel hover does not leak through to the grid. PC / mouse only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private GridView _gridView;

        [Header("Framing")]
        [SerializeField] private float _padding = 0.10f;
        [SerializeField] private float _zoomLerp = 12f;
        // Fraction of screen width covered by the left control panel. The grid is fit and centred
        // into the remaining area to the right so it never sits under the panel.
        [SerializeField] [Range(0f, 0.6f)] private float _panelInset = 0.235f;

        [Header("Appearance")]
        private const float MinLineWidth = 0.01f;
        private const float MaxLineWidth = 0.05f;
        [SerializeField] private float _lineWidth = 0.03f;
        private float _lineWidthScale = 0.5f;
        [SerializeField] private Color _lineColor = new(0.62f, 0.67f, 0.88f, 1f);
        [SerializeField] private Color _hoveredColor = new(0.40f, 0.47f, 0.95f, 0.55f);
        [SerializeField] private Color _neighborColor = new(0.40f, 0.47f, 0.95f, 0.26f);
        [SerializeField] private bool _enableHighlight = true;

        private GridDemo _current;
        private List<GridDemo> _demos;
        private int _lastHovered = -1;
        private IPointerOverUiProbe _overUi;
        private float _targetOrtho;

        private GameObject _backdropGo;
        private Material _backdropMat;
        private static Mesh _quadMesh;

        public IReadOnlyList<GridDemo> Demos => _demos;
        public GridDemo Current => _current;
        public int CellCount => _gridView != null && _gridView.Grid != null ? _gridView.Grid.CellCount : 0;

        /// <summary>Raised after the active grid is (re)built. UI uses it to refresh the cell-count readout.</summary>
        public event Action GridRebuilt;

        /// <summary>Raised when the active demo changes. UI uses it to rebuild the parameter panel.</summary>
        public event Action CurrentChanged;

        // Shareable line-width as a 0..1 scale mapped onto [MinLineWidth, MaxLineWidth]. Lives in the
        // deep-link URL (key "lw"); default 0.5. Changing it live-syncs the URL like a grid parameter.
        public float LineWidthScale
        {
            get => _lineWidthScale;
            set
            {
                _lineWidthScale = Mathf.Clamp01(value);
                _lineWidth = Mathf.Lerp(MinLineWidth, MaxLineWidth, _lineWidthScale);
                if (_gridView != null) { _gridView.LineWidth = _lineWidth; _gridView.RefreshAppearance(); }
                SyncUrl();
            }
        }

        public Color LineColor
        {
            get => _lineColor;
            set
            {
                _lineColor = value;
                if (_gridView != null) { _gridView.LineColor = value; _gridView.RefreshAppearance(); }
            }
        }

        public bool EnableHighlight
        {
            get => _enableHighlight;
            set
            {
                _enableHighlight = value;
                DemoStatePrefs.SaveHighlight(value);
                if (!value && _gridView != null) { _gridView.ClearHighlight(); _lastHovered = -1; }
            }
        }

        public void SetPanelInset(float inset)
        {
            _panelInset = Mathf.Clamp(inset, 0f, 0.6f);
        }

        public void ApplyGridColors(Color background, Color line, Color hovered, Color neighbor)
        {
            _lineColor = line;
            _hoveredColor = hovered;
            _neighborColor = neighbor;
            if (_camera != null) _camera.backgroundColor = background;
            if (_gridView != null)
            {
                _gridView.LineColor = line;
                _gridView.RefreshAppearance();
                _gridView.ClearHighlight();
            }
            _lastHovered = -1;
        }

        private void Awake()
        {
            _demos = GridDemoRegistry.CreateAll();
            foreach (var demo in _demos) DemoStatePrefs.LoadParams(demo);
            _enableHighlight = DemoStatePrefs.LoadHighlight(_enableHighlight);

            // A shared deep-link (?s=...) wins over locally saved prefs so a copied link
            // reproduces the exact example for whoever opens it.
            var shared = DemoUrlState.TryDecode(TesseraWeb.GetQuery(), _demos, out var lineWidthScale);
            LineWidthScale = lineWidthScale;
            Select(shared ?? _demos[DemoStatePrefs.LoadGridIndex(_demos.Count)]);
        }

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
            RecomputeTargetOrtho();
            if (_camera != null)
            {
                _camera.orthographicSize = _targetOrtho;
                ApplyCameraOffset(_targetOrtho);
            }
        }

        public void SetOverUiProbe(IPointerOverUiProbe probe) => _overUi = probe;

        public void Select(GridDemo demo)
        {
            if (demo == null || demo == _current) return;
            if (_current != null)
            {
                _current.Changed -= Rebuild;
                _current.Changed -= SaveCurrentParams;
            }
            _current = demo;
            _current.Changed += Rebuild;
            _current.Changed += SaveCurrentParams;
            DemoStatePrefs.SaveGridIndex(_demos.IndexOf(demo));
            Rebuild();
            SyncUrl();
            CurrentChanged?.Invoke();
        }

        private void SaveCurrentParams()
        {
            if (_current != null) DemoStatePrefs.SaveParams(_current);
            SyncUrl();
        }

        // Live-mirrors the current grid + parameters into the browser address bar so the URL is
        // always a shareable link (no copy button needed). No-op outside WebGL.
        private void SyncUrl()
        {
            if (_current != null) TesseraWeb.ReplaceQuery(DemoUrlState.Encode(_current, _lineWidthScale));
        }

        /// <summary>Rerolls the first seed parameter of the active demo, if it has one.</summary>
        public bool Reroll()
        {
            if (_current == null) return false;
            foreach (var parameter in _current.Parameters)
            {
                if (parameter is SeedParameter seed)
                {
                    seed.Reroll();
                    return true;
                }
            }
            return false;
        }

        // ---- Background image (web build): a textured quad centred on the grid, behind its lines,
        // scaled to COVER the grid bounds (an image with the grid's aspect fits exactly; otherwise one
        // axis overflows). Runtime-only: never persisted, never shared, never in the URL. ----

        public bool HasBackdrop => _backdropGo != null && _backdropGo.activeSelf;

        public void SetBackdrop(Texture2D texture)
        {
            if (texture == null || _gridView == null || _gridView.Material == null) return;
            if (_backdropGo == null)
            {
                if (_quadMesh == null) _quadMesh = CreateQuad();
                _backdropGo = new GameObject("Backdrop") { hideFlags = HideFlags.DontSave };
                _backdropGo.transform.SetParent(_gridView.transform, false);
                _backdropGo.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
                var renderer = _backdropGo.AddComponent<MeshRenderer>();
                _backdropMat = new Material(_gridView.Material);
                renderer.sharedMaterial = _backdropMat;
                renderer.sortingOrder = -1; // behind the highlight fill (0) and edges (1)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var previous = _backdropMat.mainTexture;
            _backdropMat.mainTexture = texture;
            if (previous != null && previous != texture) Destroy(previous);
            _backdropGo.SetActive(true);
            FitBackdrop();
        }

        public void ClearBackdrop()
        {
            if (_backdropGo != null) _backdropGo.SetActive(false);
            if (_backdropMat != null && _backdropMat.mainTexture != null)
            {
                var tex = _backdropMat.mainTexture;
                _backdropMat.mainTexture = null;
                Destroy(tex);
            }
        }

        private void FitBackdrop()
        {
            if (!HasBackdrop || _gridView == null || _gridView.Grid == null || _backdropMat == null) return;
            var tex = _backdropMat.mainTexture;
            if (tex == null) return;
            var size = _gridView.Grid.Bounds.Size;
            var gridW = math.max(0.0001f, size.x);
            var gridH = math.max(0.0001f, size.y);
            var imgAspect = (float)tex.width / math.max(1, tex.height);
            float w, h;
            if (imgAspect > gridW / gridH) { h = gridH; w = gridH * imgAspect; }
            else { w = gridW; h = gridW / imgAspect; }
            _backdropGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            _backdropGo.transform.localScale = new Vector3(w, h, 1f);
        }

        private static Mesh CreateQuad()
        {
            var m = new Mesh { name = "BackdropQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        private void Rebuild()
        {
            if (_gridView == null || _current == null) return;

            ITessellation grid;
            try
            {
                grid = _current.BuildGrid();
            }
            catch (Exception e)
            {
                // A few parameter combinations can still hit degenerate geometry inside a grid
                // builder. Keep the last good grid on screen instead of letting the demo break.
                Debug.LogWarning($"Grid build failed for {_current.DisplayName}: {e.Message}");
                return;
            }

            _gridView.LineWidth = _lineWidth;
            _gridView.LineColor = _lineColor;
            _gridView.SetGrid(grid);
            _lastHovered = -1;
            FitBackdrop();
            RecomputeTargetOrtho();
            GridRebuilt?.Invoke();
        }

        private void RecomputeTargetOrtho()
        {
            if (_camera == null || _gridView == null || _gridView.Grid == null) return;
            var size = _gridView.Grid.Bounds.Size;
            var aspect = _camera.aspect <= 0f ? 16f / 9f : _camera.aspect;
            var available = math.max(0.2f, 1f - _panelInset);
            var halfH = size.y * 0.5f;
            var halfWForAspect = size.x * 0.5f / (aspect * available);
            _targetOrtho = math.max(halfH, halfWForAspect) * (1f + _padding);
            if (_targetOrtho < 0.01f) _targetOrtho = 0.01f;
        }

        // Shift the camera so world origin (grid centre) lands in the middle of the area right of the panel.
        private void ApplyCameraOffset(float orthoSize)
        {
            var aspect = _camera.aspect <= 0f ? 16f / 9f : _camera.aspect;
            var targetFraction = (1f + _panelInset) * 0.5f;
            var viewWorldWidth = 2f * orthoSize * aspect;
            var camX = (0.5f - targetFraction) * viewWorldWidth;
            var pos = _camera.transform.position;
            _camera.transform.position = new Vector3(camX, 0f, pos.z);
        }

        private void Update()
        {
            UpdateCamera();
            UpdateHover();
        }

        private void UpdateCamera()
        {
            if (_camera == null) return;
            RecomputeTargetOrtho();
            var t = 1f - math.exp(-_zoomLerp * Time.unscaledDeltaTime);
            var newSize = math.lerp(_camera.orthographicSize, _targetOrtho, t);
            _camera.orthographicSize = newSize;
            ApplyCameraOffset(newSize);
        }

        private void UpdateHover()
        {
            if (!_enableHighlight || _gridView == null || _gridView.Grid == null || _camera == null) return;

            var pointer = Pointer.current;
            if (pointer == null) return;
            var screen = pointer.position.ReadValue();

            if (_overUi != null && _overUi.IsPointerOverUI(screen))
            {
                if (_lastHovered != -1) { _gridView.ClearHighlight(); _lastHovered = -1; }
                return;
            }

            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            var local = new float2(world.x + _gridView.GridCenter.x, world.y + _gridView.GridCenter.y);
            var id = _gridView.Grid.GetCellAt(local);
            if (id == _lastHovered) return;
            _lastHovered = id;
            if (id == -1) _gridView.ClearHighlight();
            else _gridView.SetHighlight(id, _hoveredColor, _neighborColor);
        }
    }
}