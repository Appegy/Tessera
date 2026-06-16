using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    public sealed class DraradechPuzzleGridDemo : GridDemo<DraradechPuzzleGrid>
    {
        private IntParameter _columns;
        private IntParameter _rows;
        private SeedParameter _seed;
        private FloatParameter _smoothness;
        private FloatParameter _tabSize;
        private FloatParameter _variation;

        public override string DisplayName => "Draradech Puzzle";
        public override string UrlId => "draradech";
        public override string Icon => "extension";

        protected override void BuildParameters(List<DemoParameter> parameters)
        {
            _columns = new IntParameter("columns", "Columns", 1, 32, 6);
            _rows = new IntParameter("rows", "Rows", 1, 32, 6);
            _seed = new SeedParameter("seed", "Seed", 0);
            _tabSize = new FloatParameter("tabSize", "Tab Size", 0f, 1f, 0.5f);
            _variation = new FloatParameter("variation", "Variation", 0f, 1f, 0.5f);
            _smoothness = new FloatParameter("smoothness", "Smoothness", 0f, 1f, 0.5f);
            parameters.Add(_columns);
            parameters.Add(_rows);
            parameters.Add(_seed);
            parameters.Add(_tabSize);
            parameters.Add(_variation);
            parameters.Add(_smoothness);
        }

        protected override DraradechPuzzleGrid Build()
        {
            var parameters = new DraradechPuzzleParameters(_tabSize.Value, _variation.Value, _smoothness.Value);
            return new DraradechPuzzleGrid(_columns.Value, _rows.Value, 1f, _seed.Value, parameters);
        }
    }
}