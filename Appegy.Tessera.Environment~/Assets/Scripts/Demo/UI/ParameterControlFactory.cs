using System.Globalization;
using UnityEngine.UIElements;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Maps a <see cref="DemoParameter" /> to a two-way-bound UI Toolkit control. This is the single
    ///     point that knows how each parameter kind looks; adding a new kind only touches this factory.
    ///     Controls write back to the parameter (which raises <see cref="DemoParameter.Changed" /> and so
    ///     rebuilds the grid) and also reflect external changes (e.g. a seed reroll) back into the widget.
    /// </summary>
    public static class ParameterControlFactory
    {
        public static VisualElement Create(DemoParameter parameter)
        {
            switch (parameter)
            {
                case FloatParameter f: return BuildFloat(f);
                case IntParameter i: return BuildInt(i);
                case ChoiceParameter c: return BuildChoice(c);
                case SeedParameter s: return BuildSeed(s);
                case BoolParameter b: return BuildBool(b);
                default: return new VisualElement();
            }
        }

        private static VisualElement Row(string label, out Label value)
        {
            var row = new VisualElement();
            row.AddToClassList("param");
            var head = new VisualElement();
            head.AddToClassList("param-head");
            var caption = new Label(label);
            caption.AddToClassList("param-label");
            value = new Label();
            value.AddToClassList("param-value");
            head.Add(caption);
            head.Add(value);
            row.Add(head);
            return row;
        }

        private static VisualElement BuildFloat(FloatParameter p)
        {
            var row = Row(p.Label, out var value);
            value.text = Fmt(p.Value);
            var slider = new Slider(p.Min, p.Max) { value = p.Value };
            slider.AddToClassList("param-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                p.Value = e.newValue;
                value.text = Fmt(p.Value);
            });
            p.Changed += () =>
            {
                slider.SetValueWithoutNotify(p.Value);
                value.text = Fmt(p.Value);
            };
            row.Add(slider);
            return row;
        }

        private static VisualElement BuildInt(IntParameter p)
        {
            var row = Row(p.Label, out var value);
            value.text = p.Value.ToString(CultureInfo.InvariantCulture);
            var slider = new SliderInt(p.Min, p.Max) { value = p.Value };
            slider.AddToClassList("param-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                p.Value = e.newValue;
                value.text = p.Value.ToString(CultureInfo.InvariantCulture);
            });
            p.Changed += () =>
            {
                slider.SetValueWithoutNotify(p.Value);
                value.text = p.Value.ToString(CultureInfo.InvariantCulture);
            };
            row.Add(slider);
            return row;
        }

        private static VisualElement BuildChoice(ChoiceParameter p)
        {
            var row = new VisualElement();
            row.AddToClassList("param");
            var caption = new Label(p.Label);
            caption.AddToClassList("param-label");
            row.Add(caption);

            var dropdown = new ThemedDropdown();
            dropdown.SetOptions(p.OptionLabels, p.SelectedIndex, i => p.SelectedIndex = i);
            p.Changed += () => dropdown.SetIndexWithoutNotify(p.SelectedIndex);
            row.Add(dropdown);
            return row;
        }

        private static VisualElement BuildSeed(SeedParameter p)
        {
            var row = new VisualElement();
            row.AddToClassList("param");
            var caption = new Label(p.Label);
            caption.AddToClassList("param-label");
            row.Add(caption);

            var controls = new VisualElement();
            controls.AddToClassList("seed-row");

            var field = new IntegerField { value = p.Value };
            field.AddToClassList("seed-field");
            field.RegisterValueChangedCallback(e => p.Value = e.newValue);

            var reroll = new Button(p.Reroll);
            reroll.AddToClassList("reroll-btn");
            var icon = new VisualElement();
            icon.AddToClassList("icon");
            icon.AddToClassList("icon-casino");
            reroll.Add(icon);

            p.Changed += () => field.SetValueWithoutNotify(p.Value);

            controls.Add(field);
            controls.Add(reroll);
            row.Add(controls);
            return row;
        }

        private static VisualElement BuildBool(BoolParameter p)
        {
            var row = new VisualElement();
            row.AddToClassList("param");
            var toggle = new Toggle(p.Label) { value = p.Value };
            toggle.AddToClassList("param-toggle");
            toggle.RegisterValueChangedCallback(e => p.Value = e.newValue);
            p.Changed += () => toggle.SetValueWithoutNotify(p.Value);
            row.Add(toggle);
            return row;
        }

        private static string Fmt(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}