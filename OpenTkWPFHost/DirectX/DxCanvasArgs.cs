using System;
using System.Windows.Media;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.DirectX
{
    public class DxCanvasArgs : PipelineArgs
    {
        private readonly DxCanvas _dxCanvas;

        private readonly IntPtr _frameBuffer;

        public DxCanvasArgs(IntPtr frameBuffer, DxCanvas dxCanvas, RenderTargetInfo renderTargetInfo,
            DrawingGroup drawingGroup)
            : base(renderTargetInfo, drawingGroup)
        {
            this._frameBuffer = frameBuffer;
            this._dxCanvas = dxCanvas;
        }

        public bool Commit(DrawingContext context)
        {
            return _dxCanvas.Commit(context, this._frameBuffer, this.TargetInfo);
        }
    }
}