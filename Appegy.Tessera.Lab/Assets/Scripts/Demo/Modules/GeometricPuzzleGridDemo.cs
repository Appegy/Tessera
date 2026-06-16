using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    public sealed class GeometricPuzzleGridDemo : GridDemo<GeometricPuzzleGrid>
    {
        private IntParameter _columns;
        private IntParameter _rows;
        private SeedParameter _seed;
        private FloatParameter _headDepth;
        private FloatParameter _headWidth;
        private FloatParameter _neckWidth;
        private FloatParameter _variation;

        public override string DisplayName => "Geometric Puzzle";
        public override string UrlId => "geometric";
        public override string Icon => "extension";

        protected override void BuildParameters(List<DemoParameter> parameters)
        {
            _columns = new IntParameter("columns", "Columns", 1, 32, 6);
            _rows = new IntParameter("rows", "Rows", 1, 32, 6);
            _seed = new SeedParameter("seed", "Seed", 0);
            _headDepth = new FloatParameter("headDepth", "Head Depth", 0f, 1f, 0.5f);
            _headWidth = new FloatParameter("headWidth", "Head Width", 0f, 1f, 0.5f);
            _neckWidth = new FloatParameter("neckWidth", "Neck Width", 0f, 1f, 0.5f);
            _variation = new FloatParameter("variation", "Variation", 0f, 1f, 0.5f);
            parameters.Add(_columns);
            parameters.Add(_rows);
            parameters.Add(_seed);
            parameters.Add(_headDepth);
            parameters.Add(_headWidth);
            parameters.Add(_neckWidth);
            parameters.Add(_variation);
        }

        protected override GeometricPuzzleGrid Build()
        {
            var parameters = new GeometricPuzzleParameters(_headDepth.Value, _headWidth.Value, _neckWidth.Value, _variation.Value);
            return new GeometricPuzzleGrid(_columns.Value, _rows.Value, 1f, _seed.Value, parameters);
        }
    }
}
