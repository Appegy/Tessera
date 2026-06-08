using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    public sealed class HexagonalGridDemo : GridDemo<HexagonalGrid>
    {
        private IntParameter _height;
        private FloatParameter _inscribedRadius;
        private ChoiceParameter<HexagonalGridType> _layout;
        private IntParameter _width;

        public override string DisplayName => "Hexagonal";
        public override string Icon => "hexagon";

        protected override void BuildParameters(List<DemoParameter> parameters)
        {
            _width = new IntParameter("width", "Width", 1, 60, 12);
            _height = new IntParameter("height", "Height", 1, 60, 10);
            _inscribedRadius = new FloatParameter("inscribedRadius", "Inscribed Radius", 0.25f, 1.5f, 0.5f);
            _layout = new ChoiceParameter<HexagonalGridType>(
                "layout", "Layout",
                new[] { HexagonalGridType.PointyOdd, HexagonalGridType.PointyEven, HexagonalGridType.FlatOdd, HexagonalGridType.FlatEven },
                new[] { "Pointy Odd", "Pointy Even", "Flat Odd", "Flat Even" },
                0);
            parameters.Add(_width);
            parameters.Add(_height);
            parameters.Add(_inscribedRadius);
            parameters.Add(_layout);
        }

        protected override HexagonalGrid Build()
            => new HexagonalGrid(_width.Value, _height.Value, _inscribedRadius.Value, _layout.Selected);
    }
}