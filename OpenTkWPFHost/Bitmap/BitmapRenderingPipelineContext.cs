using System.Windows;
using System.Windows.Media;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Bitmap
{
    /// <summary>
    /// 渲染视图目标
    /// </summary>
    public class RenderingViewTarget
    {
        private bool _targetChanged = false;

        public GlRenderEventArgs RenderArgs { get; private set; }

        private RenderTargetInfo _targetInfo;

        /// <summary>
        /// 当前渲染信息
        /// </summary>
        public RenderTargetInfo TargetInfo
        {
            get => _targetInfo;
            set
            {
                if (_targetInfo.Equals(value))
                {
                    return;
                }

                _targetInfo = value;
                _targetChanged = true;
            }
        }


        public IFrameBuffer FrameBuffer { get; set; }

        public bool CheckSize()
        {
            if (_targetChanged && !_targetInfo.IsEmpty)
            {
                RenderArgs = _targetInfo.GetRenderEventArgs();
                FrameBuffer.Release();
                FrameBuffer.Allocate(_targetInfo);
                _targetChanged = false;
                return true;
            }

            return false;
        }

        public void Render(IRenderer renderer)
        {
            FrameBuffer.PreWrite();
            renderer.Resize(_targetInfo.PixelSize);
            renderer.Render(RenderArgs);
            FrameBuffer.PostRead();
        }

        public static RenderingViewTarget CreateFromElement(FrameworkElement element)
        {
        }
    }

    public class BitmapRenderingPipelineContext
    {
        /// <summary>
        /// used to indicate render background
        /// </summary>
        public RenderTargetInfo TargetInfo { get; }

        public DrawingGroup TargetDrawing { get; }

        public PixelBufferInfo BufferInfo { get; }

        private readonly MultiBitmapCanvas _canvas;

        public BitmapCanvas PreCanvas { get; private set; }

        /// <summary>
        /// 目标帧缓冲
        /// </summary>
        public IFrameBuffer FrameBuffer { get; set; }

        public BitmapRenderingPipelineContext(RenderTargetInfo targetInfo, DrawingGroup targetDrawing,
            PixelBufferInfo bufferInfo, MultiBitmapCanvas canvas)
        {
            TargetInfo = targetInfo;
            TargetDrawing = targetDrawing;
            BufferInfo = bufferInfo;
            _canvas = canvas;
        }

        public BitmapRenderingPipelineContext ReadFrames()
        {
            if (BufferInfo.WaitFence())
            {
                return this;
            }

            return null;
        }

        public BitmapRenderingPipelineContext FlushAndSwap()
        {
            var singleBitmapCanvas = _canvas.Swap();
            if (singleBitmapCanvas.TryFlush(TargetInfo, BufferInfo))
            {
                this.PreCanvas = singleBitmapCanvas;
                return this;
            }

            return null;
        }


        /// <summary>
        /// 提交渲染
        /// </summary>
        /// <returns>是否成功提交</returns>
        public bool Commit()
        {
            using (var drawingContext = TargetDrawing.Open())
            {
                return PreCanvas.Commit(drawingContext, this.BufferInfo);
            }
        }
    }
}