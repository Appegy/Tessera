using System.Collections.Generic;

namespace Appegy.Tessera.Demo
{
    public sealed class ClassicPuzzleGridDemo : GridDemo<ClassicPuzzleGrid>
    {
        private IntParameter _columns;
        private IntParameter _rows;
        private SeedParameter _seed;
        private FloatParameter _roundness;
        private FloatParameter _tabRadius;
        private FloatParameter _tabOffset;

        public override string DisplayName => "Classic Puzzle";
        public override string Icon => "extension";

        protected override void BuildParameters(List<DemoParameter> parameters)
        {
            _columns = new IntParameter("columns", "Columns", 1, 32, 6);
            _rows = new IntParameter("rows", "Rows", 1, 32, 6);
            _seed = new SeedParameter("seed", "Seed", 0);
            _roundness = new FloatParameter("roundness", "Roundness", 0f, 1f, 0.5f);
            _tabRadius = new FloatParameter("tabRadius", "Tab Radius", 0f, 1f, 0.5f);
            _tabOffset = new FloatParameter("tabOffset", "Tab Offset", 0f, 1f, 0.5f);
            parameters.Add(_columns);
            parameters.Add(_rows);
            parameters.Add(_seed);
            parameters.Add(_roundness);
            parameters.Add(_tabRadius);
            parameters.Add(_tabOffset);
        }

        protected override ClassicPuzzleGrid Build()
        {
            var parameters = new ClassicPuzzleParameters(_roundness.Value, _tabRadius.Value, _tabOffset.Value);
            return new ClassicPuzzleGrid(_columns.Value, _rows.Value, 1f, _seed.Value, parameters);
        }
    }
}
