using System.Collections.Generic;
using Unity.Mathematics;

namespace Appegy.Tessera.Demo
{
    public sealed class VoronoiGridDemo : GridDemo<VoronoiGrid>
    {
        private IntParameter _cellCount;
        private FloatParameter _regionHeight;
        private FloatParameter _regionWidth;
        private IntParameter _relaxation;
        private SeedParameter _seed;

        public override string DisplayName => "Voronoi";
        public override string Icon => "scatter_plot";

        protected override void BuildParameters(List<DemoParameter> parameters)
        {
            _regionWidth = new FloatParameter("regionWidth", "Region Width", 2f, 30f, 12f);
            _regionHeight = new FloatParameter("regionHeight", "Region Height", 2f, 30f, 9f);
            _cellCount = new IntParameter("cellCount", "Cell Count", 4, 500, 96);
            _seed = new SeedParameter("seed", "Seed", 0);
            _relaxation = new IntParameter("relaxation", "Relaxation", 0, 16, 3);
            parameters.Add(_regionWidth);
            parameters.Add(_regionHeight);
            parameters.Add(_cellCount);
            parameters.Add(_seed);
            parameters.Add(_relaxation);
        }

        protected override VoronoiGrid Build()
        {
            var bounds = new Bounds2(float2.zero, new float2(_regionWidth.Value, _regionHeight.Value));
            return new VoronoiGrid(bounds, _cellCount.Value, _seed.Value, _relaxation.Value);
        }
    }
}