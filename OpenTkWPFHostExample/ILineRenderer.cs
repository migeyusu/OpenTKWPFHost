using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenTkControlExample
{
    public interface ILineRenderer : IDisposable
    {
        void Initialize(Shader shader);
        void OnRenderFrame(LineRenderArgs args);
        void AddPoints(IList<PointF> points);

        void AddPoint(PointF point);
    }
}