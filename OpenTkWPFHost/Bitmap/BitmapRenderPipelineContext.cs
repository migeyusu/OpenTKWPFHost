using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Bitmap
{
    public class BitmapRenderPipelineContext
    {
        public PixelBufferInfo BufferInfo { get; }

        public RenderingViewTarget ViewTarget { get; }

        private readonly MultiBitmapCanvas _canvas;

        public BitmapRenderPipelineContext(PixelBufferInfo bufferInfo, RenderingViewTarget viewTarget)
        {
            _canvas = viewTarget.Canvas;
            ViewTarget = viewTarget;
            BufferInfo = bufferInfo;
        }

        public BitmapRenderPipelineContext ReadFrames()
        {
            if (BufferInfo.WaitFence())
            {
                return this;
            }

            return null;
        }

        public BitmapCanvas FlushAndSwap()
        {
            var singleBitmapCanvas = _canvas.Swap();
            if (singleBitmapCanvas.TryFlush(BufferInfo))
            {
                return singleBitmapCanvas;
            }

            return null;
        }
    }
}