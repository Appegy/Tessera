using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Drives the UI Toolkit playground panel: builds the grid-type selector and the dynamic
    ///     parameter list from <see cref="DemoController.Current" />, binds the appearance controls
    ///     (hover highlight, theme, text size), keeps the info readout current, and answers over-UI
    ///     hit tests so panel hover does not leak through to the grid behind it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlaygroundUI : MonoBehaviour, IPointerOverUiProbe
    {
        private const string ScalePrefKey = "tessera_ui_scale";

        [SerializeField] private UIDocument _document;
        [SerializeField] private DemoController _controller;

        private ThemedDropdown _gridDropdown;
        private Label _info;
        private VisualElement _params;
        private VisualElement _selector;
        private VisualElement _themeRoot;
        private bool _isLight;
        private int _uiScaleIndex = 1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int TesseraGetTheme();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void TesseraRegisterThemeTarget(string name);
#endif

#if UNITY_EDITOR
        // Editor has no web page to drive the theme: press T to toggle dark/light for testing.
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) ApplyTheme(!_isLight);
        }
#endif

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            Build();
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.CurrentChanged -= OnCurrentChanged;
            _controller.GridRebuilt -= UpdateInfo;
        }

        private void Build()
        {
            var root = _document.rootVisualElement;
            if (root == null || _controller == null) return;

            root.pickingMode = PickingMode.Ignore;
            _themeRoot = root.Q("root");
            if (_themeRoot != null) _themeRoot.pickingMode = PickingMode.Ignore;

            _selector = root.Q("selector");
            _params = root.Q("params");
            _info = root.Q<Label>("info");

            _uiScaleIndex = PlayerPrefs.GetInt(ScalePrefKey, 1);

            BuildSelector();
            BuildAppearance(root.Q("appearance"));

            _controller.CurrentChanged += OnCurrentChanged;
            _controller.GridRebuilt += UpdateInfo;
            _controller.SetOverUiProbe(this);

            OnCurrentChanged();
            InitTheme();
            SetUiScale(_uiScaleIndex);
        }

        private void BuildSelector()
        {
            if (_selector == null) return;
            _selector.Clear();
            var names = new List<string>();
            foreach (var demo in _controller.Demos) names.Add(demo.DisplayName);
            _gridDropdown = new ThemedDropdown();
            _gridDropdown.SetOptions(names, IndexOfCurrent(), i => _controller.Select(_controller.Demos[i]));
            _selector.Add(_gridDropdown);
        }

        private int IndexOfCurrent()
        {
            for (var i = 0; i < _controller.Demos.Count; i++)
                if (_controller.Demos[i] == _controller.Current)
                    return i;
            return 0;
        }

        private void BuildAppearance(VisualElement container)
        {
            if (container == null) return;
            container.Clear();

            var highlightRow = new VisualElement();
            highlightRow.AddToClassList("param");
            var highlightToggle = new Toggle("Hover Highlight") { value = _controller.EnableHighlight };
            highlightToggle.AddToClassList("param-toggle");
            highlightToggle.RegisterValueChangedCallback(e => _controller.EnableHighlight = e.newValue);
            highlightRow.Add(highlightToggle);
            container.Add(highlightRow);

            container.Add(BuildLineWidth());

            container.Add(BuildSegmented("Text Size", new[] { "Small", "Medium", "Large" }, _uiScaleIndex, SetUiScale));
        }

        // Line width as a 0..1 scale bound to DemoController.LineWidthScale, which lives in the
        // shareable URL (key "lw", default 0.5). Changing it live-syncs the link.
        private VisualElement BuildLineWidth()
        {
            var row = new VisualElement();
            row.AddToClassList("param");
            var head = new VisualElement();
            head.AddToClassList("param-head");
            var caption = new Label("Line Width");
            caption.AddToClassList("param-label");
            var scale = _controller.LineWidthScale;
            var value = new Label(Mathf.RoundToInt(scale * 100f) + "%");
            value.AddToClassList("param-value");
            head.Add(caption);
            head.Add(value);
            row.Add(head);

            var slider = new Slider(0f, 1f) { value = scale };
            slider.AddToClassList("param-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                var t = Mathf.Clamp01(e.newValue);
                _controller.LineWidthScale = t;
                value.text = Mathf.RoundToInt(t * 100f) + "%";
            });
            row.Add(slider);
            return row;
        }

        private VisualElement BuildSegmented(string label, string[] options, int selected, Action<int> onSelect)
        {
            var row = new VisualElement();
            row.AddToClassList("param");
            var caption = new Label(label);
            caption.AddToClassList("param-label");
            row.Add(caption);

            var seg = new VisualElement();
            seg.AddToClassList("segmented");
            var buttons = new List<Button>();
            for (var i = 0; i < options.Length; i++)
            {
                var idx = i;
                var btn = new Button(() =>
                {
                    for (var j = 0; j < buttons.Count; j++) buttons[j].EnableInClassList("seg-on", j == idx);
                    onSelect(idx);
                });
                btn.text = options[i];
                btn.AddToClassList("seg-btn");
                if (i == selected) btn.AddToClassList("seg-on");
                seg.Add(btn);
                buttons.Add(btn);
            }
            row.Add(seg);
            return row;
        }

        public void ApplyTheme(bool light)
        {
            _isLight = light;
            if (_themeRoot != null)
            {
                _themeRoot.EnableInClassList("theme-light", light);
                _themeRoot.EnableInClassList("theme-dark", !light);
            }

            if (light)
                _controller.ApplyGridColors(
                    new Color(0.93f, 0.94f, 0.965f),
                    new Color(0.34f, 0.38f, 0.52f),
                    new Color(0.30f, 0.38f, 0.90f, 0.34f),
                    new Color(0.30f, 0.38f, 0.90f, 0.16f));
            else
                _controller.ApplyGridColors(
                    new Color(0.105f, 0.113f, 0.152f),
                    new Color(0.62f, 0.67f, 0.88f),
                    new Color(0.40f, 0.47f, 0.95f, 0.55f),
                    new Color(0.40f, 0.47f, 0.95f, 0.26f));

        }

        /// <summary>Called by the web page (SendMessage) when the theme changes. 1 = light, 0 = dark.</summary>
        public void ApplyPageTheme(int isLight) => ApplyTheme(isLight == 1);

        // Theme is driven by the web page now. In WebGL: read the current page theme and register a
        // target the page pushes changes to. In the editor: default to dark (press T to toggle).
        private void InitTheme()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TesseraRegisterThemeTarget(gameObject.name);
            ApplyTheme(TesseraGetTheme() == 1);
#else
            ApplyTheme(false);
#endif
        }

        public void SetUiScale(int index)
        {
            _uiScaleIndex = index;
            Vector2Int reference;
            float insetK;
            switch (index)
            {
                case 0:
                    reference = new Vector2Int(2133, 1200);
                    insetK = 0.9f;
                    break;
                case 2:
                    reference = new Vector2Int(1600, 900);
                    insetK = 1.2f;
                    break;
                default:
                    reference = new Vector2Int(1920, 1080);
                    insetK = 1.0f;
                    break;
            }

            if (_document != null && _document.panelSettings != null)
                _document.panelSettings.referenceResolution = reference;
            _controller.SetPanelInset(0.2125f * insetK + 0.022f);
            PlayerPrefs.SetInt(ScalePrefKey, index);
        }

        private void OnCurrentChanged()
        {
            if (_params != null)
            {
                _params.Clear();
                foreach (var parameter in _controller.Current.Parameters)
                    _params.Add(ParameterControlFactory.Create(parameter));
            }

            _gridDropdown?.SetIndexWithoutNotify(IndexOfCurrent());

            UpdateInfo();
        }

        private void UpdateInfo()
        {
            if (_info == null || _controller.Current == null) return;
            _info.text = _controller.CellCount + " cells";
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            var root = _document != null ? _document.rootVisualElement : null;
            var panel = root?.panel;
            if (panel == null) return false;
            var local = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            return panel.Pick(local) != null;
        }
    }
}