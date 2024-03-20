using System;
using System.Drawing;
using OpenTK.Graphics;
using OpenTK.Mathematics;

namespace OpenTkControlExample
{
    public class TestRendererCase
    {
        public const int LineCount = 10;

        public const int PointsCount = 20;

        private const long MaxYAxis = (long)((1000 + PointsCount) * 0.1);

        private readonly Color4 _lineColor = Color4.White;

        public TendencyChartRenderer Renderer { get; }

        public SubRenderer SubRenderer { get; }
        
        public SubRenderer SubRenderer2 { get; }

        public TestRendererCase()
        {
            var renderer = new TendencyChartRenderer();
            renderer.CurrentScrollRange = new ScrollRange(0, PointsCount);
            renderer.CurrentYAxisValue = MaxYAxis;
            renderer.BackgroundColor = Color4.Black;
            var random = new Random();
            for (int i = 0; i < LineCount; i++)
            {
                var pointFs = new PointF[PointsCount];
                for (int j = 0; j < PointsCount; j++)
                {
                    pointFs[j] = new PointF(j, random.Next(j, 1000 + j) * 0.1f);
                }

                var simpleLineRenderer = new LineRenderer(PointsCount) { LineColor = _lineColor };
                simpleLineRenderer.AddPoints(pointFs);
                renderer.Add(simpleLineRenderer);
            }

            this.Renderer = renderer;
            SubRenderer = new SubRenderer(renderer)
            {
                BackgroundColor = Color4.Orange,
                CurrentYAxisValue = MaxYAxis,
                CurrentScrollRange = new ScrollRange(0, PointsCount * 2)
            };
            SubRenderer2 = new SubRenderer(renderer)
            {
                BackgroundColor = Color4.Yellow,
                CurrentYAxisValue = MaxYAxis,
                CurrentScrollRange = new ScrollRange(0, PointsCount * 3)
            };
        }
    }
}