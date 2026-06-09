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
        [SerializeField] private float _lineWidth = 0.05f;
        [SerializeField] private Color _lineColor = new(0.62f, 0.67f, 0.88f, 1f);
        [SerializeField] private Color _hoveredColor = new(0.40f, 0.47f, 0.95f, 0.55f);
        [SerializeField] private Color _neighborColor = new(0.40f, 0.47f, 0.95f, 0.26f);
        [SerializeField] private bool _enableHighlight = true;

        private GridDemo _current;
        private List<GridDemo> _demos;
        private int _lastHovered = -1;
        private IPointerOverUiProbe _overUi;
        private float _targetOrtho;

        public IReadOnlyList<GridDemo> Demos => _demos;
        public GridDemo Current => _current;
        public int CellCount => _gridView != null && _gridView.Grid != null ? _gridView.Grid.CellCount : 0;

        /// <summary>Raised after the active grid is (re)built. UI uses it to refresh the cell-count readout.</summary>
        public event Action GridRebuilt;

        /// <summary>Raised when the active demo changes. UI uses it to rebuild the parameter panel.</summary>
        public event Action CurrentChanged;

        public float LineWidth
        {
            get => _lineWidth;
            set
            {
                _lineWidth = value;
                if (_gridView != null) { _gridView.LineWidth = value; _gridView.RefreshAppearance(); }
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
            var shared = DemoUrlState.TryDecode(TesseraWeb.GetQuery(), _demos);
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
            if (_current != null) TesseraWeb.ReplaceQuery(DemoUrlState.Encode(_current));
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