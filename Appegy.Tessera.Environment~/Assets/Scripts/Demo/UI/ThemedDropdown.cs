using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     A dropdown styled entirely by the demo's USS (no native popup). The option list floats in a
    ///     popup added to the themed root so it overlays the panel and inherits the theme variables; a
    ///     full-screen transparent blocker closes it on any outside click. Used for the grid-type
    ///     selector and every choice parameter.
    /// </summary>
    public sealed class ThemedDropdown : VisualElement
    {
        private readonly List<string> _options = new();
        private readonly Label _value;
        private VisualElement _blocker;
        private int _index;
        private Action<int> _onChange;
        private VisualElement _popup;

        public ThemedDropdown()
        {
            AddToClassList("dropdown");

            _value = new Label { pickingMode = PickingMode.Ignore };
            _value.AddToClassList("dropdown-value");

            var arrow = new VisualElement { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("dropdown-arrow");

            Add(_value);
            Add(arrow);
            RegisterCallback<PointerDownEvent>(OnFieldDown);
        }

        public void SetOptions(IEnumerable<string> options, int selected, Action<int> onChange)
        {
            _options.Clear();
            _options.AddRange(options);
            _index = _options.Count > 0 ? Mathf.Clamp(selected, 0, _options.Count - 1) : 0;
            _onChange = onChange;
            _value.text = _options.Count > 0 ? _options[_index] : string.Empty;
        }

        public void SetIndexWithoutNotify(int index)
        {
            if (_options.Count == 0) return;
            _index = Mathf.Clamp(index, 0, _options.Count - 1);
            _value.text = _options[_index];
        }

        private void OnFieldDown(PointerDownEvent e)
        {
            e.StopPropagation();
            if (_popup != null) Close();
            else Open();
        }

        private void Open()
        {
            var overlay = GetOverlayRoot();
            if (overlay == null) return;

            _blocker = new VisualElement { style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } };
            _blocker.RegisterCallback<PointerDownEvent>(_ => Close());
            overlay.Add(_blocker);

            _popup = new VisualElement();
            _popup.AddToClassList("dropdown-popup");
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("dropdown-list");
            for (var i = 0; i < _options.Count; i++)
            {
                var idx = i;
                var item = new Label(_options[i]);
                item.AddToClassList("dropdown-item");
                if (i == _index) item.AddToClassList("dropdown-item--on");
                item.RegisterCallback<PointerDownEvent>(ev =>
                {
                    ev.StopPropagation();
                    Select(idx);
                });
                list.Add(item);
            }
            _popup.Add(list);
            overlay.Add(_popup);

            var field = worldBound;
            var origin = overlay.worldBound;
            _popup.style.position = Position.Absolute;
            _popup.style.left = field.x - origin.x;
            _popup.style.top = field.yMax - origin.y + 4f;
            _popup.style.width = field.width;

            AddToClassList("dropdown--open");
        }

        private void Select(int index)
        {
            _index = index;
            _value.text = _options[index];
            Close();
            _onChange?.Invoke(index);
        }

        private void Close()
        {
            _popup?.RemoveFromHierarchy();
            _blocker?.RemoveFromHierarchy();
            _popup = null;
            _blocker = null;
            RemoveFromClassList("dropdown--open");
        }

        private VisualElement GetOverlayRoot()
        {
            var current = parent;
            VisualElement topmost = this;
            while (current != null)
            {
                if (current.name == "root") return current;
                topmost = current;
                current = current.parent;
            }
            return topmost;
        }
    }
}