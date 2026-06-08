using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    public sealed class SquareGridDemo : GridDemo<SquareGrid>
    {
        private IntParameter _height;
        private IntParameter _width;

        public override string DisplayName => "Square";
        public override string Icon => "grid_view";

        protected override void BuildParameters(List<DemoParameter> parameters)
        {
            _width = new IntParameter("width", "Width", 1, 60, 12);
            _height = new IntParameter("height", "Height", 1, 60, 8);
            parameters.Add(_width);
            parameters.Add(_height);
        }

        // Cell size is fixed: the camera auto-fits the grid, so absolute scale has no visible effect.
        protected override SquareGrid Build()
            => new SquareGrid(_width.Value, _height.Value, 1f);
    }
}