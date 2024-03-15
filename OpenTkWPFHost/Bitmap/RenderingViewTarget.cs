using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Configuration;
using OpenTkWPFHost.Core;
using OpenTK.Graphics.OpenGL4;

namespace OpenTkWPFHost.Bitmap
{
    /// <summary>
    /// 渲染视图目标
    /// </summary>
    public class RenderingViewTarget : IDisposable
    {
        public GlRenderEventArgs RenderArgs { get; private set; }

        public BitmapCanvas RenderCanvas { get; set; }

        public bool Visible { get; set; }

        public MultiBitmapCanvas Canvas { get; set; }

        private RenderTargetInfo _lastTargetInfo;

        private RenderTargetInfo _targetInfo;

        /// <summary>
        /// 当前渲染信息
        /// </summary>
        public RenderTargetInfo TargetInfo
        {
            get => _targetInfo;
            set
            {
                if (Equals(_targetInfo, value))
                {
                    return;
                }

                _targetInfo = value;
            }
        }


        public IFrameBuffer FrameBuffer { get; private set; }

        public FrameworkElement Element => _element;

        public bool RenderSyncEnable { get; set; } = false;

        private bool _isVisible = false;

        public const int MaxSemaphoreCount = 3;

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, MaxSemaphoreCount);

        public RenderingViewTarget(FrameworkElement element)
        {
            _element = element;
        }

        public bool IsValid()
        {
            return _targetInfo is { IsEmpty: false } && _isVisible;
        }

        public bool CheckSizeChange()
        {
            if (!Equals(_targetInfo, _lastTargetInfo) && IsValid())
            {
                _lastTargetInfo = _targetInfo;
                RenderArgs = _targetInfo.GetRenderEventArgs();
                FrameBuffer.Release();
                FrameBuffer.Allocate(_targetInfo);
                return true;
            }

            return false;
        }

        public void Render(IRenderer renderer)
        {
            if (_lastTargetInfo == null || _lastTargetInfo.IsEmpty)
            {
                return;
            }

            FrameBuffer.PreWrite();
            renderer.Resize(_lastTargetInfo.PixelSize);
            renderer.Render(RenderArgs);
            FrameBuffer.PostRead();
        }
        
        private readonly FrameworkElement _element;

        private RenderSetting _renderSetting;

        public void Initialize(RenderSetting renderSetting, GLSettings glSettings, int parallelism)
        {
            _element.SizeChanged += OnSizeChanged;
            _element.IsVisibleChanged += OnIsVisibleChanged;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            this.RenderSyncEnable = renderSetting.GetRenderSyncEnable();
            this._isVisible = _element.IsVisible;
            this.TargetInfo = renderSetting.CreateRenderTarget(_element);
            this._renderSetting = renderSetting;
            this.TargetInfo = renderSetting.CreateRenderTarget(_element);
            this.Canvas = new MultiBitmapCanvas(parallelism * 3);
            var samples = glSettings.MSAASamples;
            IFrameBuffer frameBuffer;
            if (samples > 1)
            {
                frameBuffer = new MSAAFrameBuffer(samples);
                GL.Enable(EnableCap.Multisample);
            }
            else
            {
                frameBuffer = new SimpleFrameBuffer();
            }

            this.FrameBuffer = frameBuffer;
        }

        public void TryWaitSync(CancellationToken token)
        {
            if (RenderSyncEnable)
            {
                // ReSharper disable once MethodHasAsyncOverload
                _semaphoreSlim.Wait(token);
            }
        }

        public void TryReleaseSync()
        {
            if (RenderSyncEnable && _semaphoreSlim.CurrentCount < MaxSemaphoreCount)
            {
                _semaphoreSlim.Release();
            }
        }


        public BitmapRenderPipelineContext GetRenderContext(PixelBufferInfo pbo)
        {
            return new BitmapRenderPipelineContext(pbo, this);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this._isVisible = (bool)e.NewValue;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.TargetInfo = _renderSetting.CreateRenderTarget(_element);
        }

        private TimeSpan? _lastRenderTime = TimeSpan.FromSeconds(-1);

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            var renderingTime = ((RenderingEventArgs)e)?.RenderingTime;
            if (Equals(renderingTime, _lastRenderTime))
            {
                return;
            }

            _lastRenderTime = renderingTime;
            _element.InvalidateVisual();
        }

        public void Dispose()
        {
            if (_element != null)
            {
                _element.SizeChanged -= OnSizeChanged;
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                _element.IsVisibleChanged -= OnIsVisibleChanged;
                this._semaphoreSlim.Dispose();
            }
        }
    }
}