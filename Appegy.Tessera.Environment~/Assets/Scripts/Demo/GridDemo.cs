using System;
using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     A selectable grid in the demo: a display name, an optional icon id, a flat list of tunable
    ///     parameters, and a factory that turns the current parameter values into an
    ///     <see cref="ITessellation" />. The registry and UI work against this non-generic base;
    ///     concrete modules derive from <see cref="GridDemo{T}" /> for a typed build.
    /// </summary>
    public abstract class GridDemo
    {
        private List<DemoParameter> _parameters;

        /// <summary>Human-readable name shown in the grid selector.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Icon id resolved by the UI layer (e.g. a Material Symbols name). Null = no icon.</summary>
        public virtual string Icon => null;

        /// <summary>Tunable inputs, built lazily on first access and cached.</summary>
        public IReadOnlyList<DemoParameter> Parameters
        {
            get
            {
                EnsureInitialized();
                return _parameters;
            }
        }

        /// <summary>Raised when any parameter value changes. The controller rebuilds the grid in response.</summary>
        public event Action Changed;

        /// <summary>
        ///     Idempotently builds the parameter list and wires change forwarding. Called from the
        ///     <see cref="Parameters" /> getter and before any build, so the typed fields a module stores
        ///     in <see cref="BuildParameters" /> are always assigned regardless of call order.
        /// </summary>
        protected void EnsureInitialized()
        {
            if (_parameters != null) return;
            _parameters = new List<DemoParameter>();
            BuildParameters(_parameters);
            foreach (var parameter in _parameters)
                parameter.Changed += RaiseChanged;
        }

        private void RaiseChanged() => Changed?.Invoke();

        /// <summary>Populate the parameter list. Called once; store typed references for use in the builder.</summary>
        protected abstract void BuildParameters(List<DemoParameter> parameters);

        /// <summary>Construct a fresh grid from the current parameter values.</summary>
        public abstract ITessellation BuildGrid();
    }

    /// <summary>
    ///     Typed base for a concrete grid demo. <typeparamref name="T" /> is the concrete grid type so
    ///     the builder is strongly typed and future per-grid features can access it without casting.
    /// </summary>
    public abstract class GridDemo<T> : GridDemo where T : ITessellation
    {
        public sealed override ITessellation BuildGrid()
        {
            EnsureInitialized();
            return Build();
        }

        protected abstract T Build();
    }
}