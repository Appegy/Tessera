using System;
using System.Collections.Generic;
using UnityEngine;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     One tunable input of a <see cref="GridDemo" />. Holds its own current value (single source
    ///     of truth) and raises <see cref="Changed" /> when that value is mutated. The UI layer renders
    ///     a control per concrete type; the demo module reads the value back in its grid builder.
    /// </summary>
    public abstract class DemoParameter
    {
        protected DemoParameter(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }

        /// <summary>True when the current value equals the parameter's initial (default) value.</summary>
        public abstract bool IsAtDefault { get; }

        /// <summary>Restores the value to the parameter's initial (default) value.</summary>
        public abstract void ResetToDefault();

        public event Action Changed;
        protected void RaiseChanged() => Changed?.Invoke();
    }

    /// <summary>Continuous value in <c>[Min, Max]</c>. Rendered as a slider.</summary>
    public sealed class FloatParameter : DemoParameter
    {
        private readonly float _default;
        private float _value;

        public FloatParameter(string id, string label, float min, float max, float value) : base(id, label)
        {
            Min = min;
            Max = max;
            _value = Mathf.Clamp(value, min, max);
            _default = _value;
        }

        public float Min { get; }
        public float Max { get; }

        public float Value
        {
            get => _value;
            set
            {
                var clamped = Mathf.Clamp(value, Min, Max);
                if (clamped == _value) return;
                _value = clamped;
                RaiseChanged();
            }
        }

        public override bool IsAtDefault => Mathf.Approximately(_value, _default);
        public override void ResetToDefault() => Value = _default;
    }

    /// <summary>Integer value in <c>[Min, Max]</c>. Rendered as a stepped slider.</summary>
    public sealed class IntParameter : DemoParameter
    {
        private readonly int _default;
        private int _value;

        public IntParameter(string id, string label, int min, int max, int value) : base(id, label)
        {
            Min = min;
            Max = max;
            _value = Mathf.Clamp(value, min, max);
            _default = _value;
        }

        public int Min { get; }
        public int Max { get; }

        public int Value
        {
            get => _value;
            set
            {
                var clamped = Mathf.Clamp(value, Min, Max);
                if (clamped == _value) return;
                _value = clamped;
                RaiseChanged();
            }
        }

        public override bool IsAtDefault => _value == _default;
        public override void ResetToDefault() => Value = _default;
    }

    /// <summary>Boolean toggle.</summary>
    public sealed class BoolParameter : DemoParameter
    {
        private readonly bool _default;
        private bool _value;

        public BoolParameter(string id, string label, bool value) : base(id, label)
        {
            _value = value;
            _default = value;
        }

        public bool Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                _value = value;
                RaiseChanged();
            }
        }

        public override bool IsAtDefault => _value == _default;
        public override void ResetToDefault() => Value = _default;
    }

    /// <summary>
    ///     Arbitrary integer seed. Rendered as a number field plus a reroll button rather than a
    ///     bounded slider, because the meaningful range is the whole <see cref="int" /> domain.
    /// </summary>
    public sealed class SeedParameter : DemoParameter
    {
        private static readonly System.Random _rng = new System.Random();
        private readonly int _default;
        private int _value;

        public SeedParameter(string id, string label, int value) : base(id, label)
        {
            _value = value;
            _default = value;
        }

        public int Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                _value = value;
                RaiseChanged();
            }
        }

        public void Reroll() => Value = _rng.Next(int.MinValue, int.MaxValue);

        public override bool IsAtDefault => _value == _default;
        public override void ResetToDefault() => Value = _default;
    }

    /// <summary>
    ///     Non-generic view of a one-of-N choice. The UI binds to <see cref="OptionLabels" /> and
    ///     <see cref="SelectedIndex" /> without knowing the underlying value type.
    /// </summary>
    public abstract class ChoiceParameter : DemoParameter
    {
        protected ChoiceParameter(string id, string label) : base(id, label)
        {
        }

        public abstract IReadOnlyList<string> OptionLabels { get; }
        public abstract int SelectedIndex { get; set; }
    }

    /// <summary>Typed one-of-N choice. The demo module reads <see cref="Selected" /> to build its grid.</summary>
    public sealed class ChoiceParameter<T> : ChoiceParameter
    {
        private readonly string[] _labels;
        private readonly T[] _values;
        private readonly int _default;
        private int _index;

        public ChoiceParameter(string id, string label, T[] values, string[] labels, int selectedIndex) : base(id, label)
        {
            if (values == null || values.Length == 0) throw new ArgumentException("values must be non-empty.", nameof(values));
            if (labels == null || labels.Length != values.Length) throw new ArgumentException("labels must match values length.", nameof(labels));
            _values = values;
            _labels = labels;
            _index = Mathf.Clamp(selectedIndex, 0, values.Length - 1);
            _default = _index;
        }

        public override IReadOnlyList<string> OptionLabels => _labels;

        public override int SelectedIndex
        {
            get => _index;
            set
            {
                var clamped = Mathf.Clamp(value, 0, _values.Length - 1);
                if (clamped == _index) return;
                _index = clamped;
                RaiseChanged();
            }
        }

        public T Selected => _values[_index];

        public override bool IsAtDefault => _index == _default;
        public override void ResetToDefault() => SelectedIndex = _default;
    }
}
