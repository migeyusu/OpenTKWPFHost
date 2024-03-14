using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Graphics.Wgl;
using OpenTK.Platform;
using OpenTK.Windowing.Common;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Bitmap;
using OpenTkWPFHost.Configuration;
using OpenTkWPFHost.Core;
using Point = System.Windows.Point;

namespace OpenTkWPFHost.Control
{
    /*UI线程和opengl线程的同步方式有三种：
     1. UI线程驱动，每次CompositionTarget发出渲染请求时会释放opengl，此方法性能较好，但是opengl的帧率无法超过wpf
     2. opengl驱动，每次产生新的帧就发出渲染请求，当然请求速率不超过ui，缺点是当opengl的帧率较低时，ui的帧数也较低（这个实际并非缺点）
     并且线程模型简单
     3. 独立的两个渲染线程，线程同步的复杂度大幅提升*/

    /// <summary>
    /// 使用bitmap进行渲染
    /// </summary>
    public class BitmapOpenTkControl : OpenTkControlBase
    {
        public static readonly DependencyProperty FpsBrushProperty = DependencyProperty.Register(
            "FpsBrush", typeof(Brush), typeof(BitmapOpenTkControl), new PropertyMetadata(Brushes.Black));

        public Brush FpsBrush
        {
            get { return (Brush)GetValue(FpsBrushProperty); }
            set { SetValue(FpsBrushProperty, value); }
        }

        /// <summary>
        /// The Thread object for the rendering thread， use origin thread but not task lest context switch
        /// </summary>
        private Task _renderTask;

        /// <summary>
        /// The CTS used to stop the thread when this control is unloaded
        /// </summary>
        private CancellationTokenSource _renderTokenSource;

        private readonly FpsCounter _glFps = new FpsCounter() { Title = "GLFps" };

        private readonly FpsCounter _controlFps = new FpsCounter() { Title = "ControlFps" };

        private volatile RenderTargetInfo _recentTarget = new RenderTargetInfo(0, 0, 96, 96);

        private readonly EventWaiter _userVisibleResetEvent = new EventWaiter();

        public BitmapOpenTkControl() : base()
        {
            this.SizeChanged += ThreadOpenTkControl_SizeChanged;
        }

        protected override void OnLoaded(object sender, RoutedEventArgs args)
        {
            base.OnLoaded(sender, args);
            DependencyPropertyDescriptor.FromProperty(FpsBrushProperty, typeof(BitmapOpenTkControl))
                .AddValueChanged(this, FpsBrushHandler);
            FpsBrushHandler(null, null);
        }

        protected override void OnUnloaded(object sender, RoutedEventArgs args)
        {
            base.OnUnloaded(sender, args);
            DependencyPropertyDescriptor.FromProperty(FpsBrushProperty, typeof(BitmapOpenTkControl))
                .RemoveValueChanged(this, FpsBrushHandler);
        }

        private void FpsBrushHandler(object sender, EventArgs e)
        {
            var fpsBrush = this.FpsBrush;
            this._glFps.Brush = fpsBrush;
            this._controlFps.Brush = fpsBrush;
        }

        private TimeSpan _lastRenderTime = TimeSpan.FromSeconds(-1);

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            var renderingTime = ((RenderingEventArgs)e)?.RenderingTime;
            if (renderingTime == _lastRenderTime)
            {
                return;
            }

            _lastRenderTime = renderingTime.Value;
            this.InvalidateVisual();
        }


        private void ThreadOpenTkControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _recentTarget = this.RenderSetting.CreateRenderTarget(this);
            if (!_recentTarget.IsEmpty)
            {
                _sizeNotEmptyWaiter.TrySet();
            }

            CallValidRenderOnce();
        }

        private readonly DrawingGroup _drawingGroup = new DrawingGroup();

        // private readonly TaskCompletionEvent _renderSyncWaiter = new TaskCompletionEvent();

        private readonly EventWaiter _sizeNotEmptyWaiter = new EventWaiter();

        private readonly EventWaiter _renderContinuousWaiter = new EventWaiter();

        private readonly Point _startPoint = new Point(10, 10);

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            try
            {
                drawingContext.DrawDrawing(_drawingGroup);
                // _renderSyncWaiter.TrySet();
                if (ShowFps)
                {
                    _controlFps.Increment();
                    var d = _glFps.DrawFps(drawingContext, _startPoint);
                    _controlFps.DrawFps(drawingContext, new Point(10, d + 10));
                }
            }
            finally
            {
                if (_renderSemaphoreEnable && _semaphoreSlim.CurrentCount < MaxSemaphoreCount)
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        private bool _renderSemaphoreEnable = false;

        private bool _isInternalTrigger = false;

        public const int MaxSemaphoreCount = 3;

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, MaxSemaphoreCount);

        private MultiBitmapCanvas _renderCanvas;

        private uint _maxParallelism;

        protected override void StartRender()
        {
            _renderTokenSource = new CancellationTokenSource();
            var renderer = this.Renderer;
            var glSettings = this.GlSettings;
            var workingRenderSetting = this.RenderSetting;
            Initialize(workingRenderSetting);
            _recentTarget = workingRenderSetting.CreateRenderTarget(this);
            _renderCanvas = new MultiBitmapCanvas((int)(_maxParallelism * 3));
            _multiStoragePixelBuffer = new MultiPixelBuffer(_maxParallelism * 3);
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _mainContextWrapper = glSettings.NewContext();
            _mainContextWrapper.MakeCurrent();
            var samples = glSettings.MSAASamples;
            if (samples > 1)
            {
                this._frameBuffer = new OffScreenMSAAFrameBuffer(samples);
                GL.Enable(EnableCap.Multisample);
            }
            else
            {
                this._frameBuffer = new SimpleFrameBuffer();
            }

            GL.Enable(EnableCap.DebugOutput);
            GL.DebugMessageCallback(DebugProc, IntPtr.Zero);
            _taskScheduler = new GLTaskScheduler(_mainContextWrapper, DebugProc);
            var pipeline = BuildPipeline(_taskScheduler, scheduler);
            _renderTask = Task.Run(async () =>
            {
                using (_renderTokenSource)
                {
                    await RenderThread(_renderTokenSource.Token, _mainContextWrapper, pipeline, renderer,
                        _renderSemaphoreEnable);
                }
            });
        }

        private void Initialize(RenderSetting workingRenderSetting)
        {
            _maxParallelism = (uint)Environment.ProcessorCount / 2; //hyper threading or P-E cores?
            switch (workingRenderSetting.RenderTactic)
            {
                case RenderTactic.Default:
                    _isInternalTrigger = false;
                    CompositionTarget.Rendering += CompositionTarget_Rendering;
                    _renderSemaphoreEnable = true;
                    _maxParallelism = 1;
                    break;
                case RenderTactic.ThroughoutPriority:
                    if (_maxParallelism > 3)
                    {
                        _maxParallelism = 3;
                    }

                    _isInternalTrigger = true;
                    _renderSemaphoreEnable = false;
                    break;
                case RenderTactic.LatencyPriority:
                    _isInternalTrigger = true;
                    if (_maxParallelism > 3)
                    {
                        _maxParallelism = 3;
                    }

                    _renderSemaphoreEnable = true;
                    break;
                case RenderTactic.MaxThroughout:
                    _isInternalTrigger = true;
                    _renderSemaphoreEnable = false;
                    if (_maxParallelism < 1)
                    {
                        _maxParallelism = 1;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private Pipeline<BitmapRenderingPipelineContext> BuildPipeline(TaskScheduler glContextTaskScheduler,
            TaskScheduler uiScheduler)
        {
            // run on gl thread, read buffer from pbo.
            var renderBlock = new TransformBlock<BitmapRenderingPipelineContext, BitmapRenderingPipelineContext>(
                args =>
                {
                    if (ShowFps)
                    {
                        _glFps.Increment();
                    }

                    return args.ReadFrames();
                },
                new ExecutionDataflowBlockOptions()
                {
                    SingleProducerConstrained = true,
                    TaskScheduler = glContextTaskScheduler,
                    MaxDegreeOfParallelism = 1,
                });
            //copy buffer to image source
            var frameBlock = new TransformBlock<BitmapRenderingPipelineContext, BitmapRenderingPipelineContext>(args =>
                {
                    if (args == null)
                    {
                        return null;
                    }

                    return args.FlushAndSwap();
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = (int)_maxParallelism,
                    SingleProducerConstrained = true,
                });
            renderBlock.LinkTo(frameBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            //call render 
            var canvasBlock = new ActionBlock<BitmapRenderingPipelineContext>(args =>
            {
                if (args == null)
                {
                    return;
                }

                if (args.Commit())
                {
                    if (_isInternalTrigger)
                    {
                        this.InvalidateVisual();
                    }
                }
            }, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 1,
                SingleProducerConstrained = true,
                TaskScheduler = uiScheduler,
            });
            frameBlock.LinkTo(canvasBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            renderBlock.Completion.ContinueWith((task =>
            {
                if (task.IsFaulted)
                {
                    ((IDataflowBlock)frameBlock).Fault(task.Exception!);
                }
                else
                {
                    frameBlock.Complete();
                }
            }));
            frameBlock.Completion.ContinueWith((task =>
            {
                if (task.IsFaulted)
                {
                    ((IDataflowBlock)canvasBlock).Fault(task.Exception!);
                }
                else
                {
                    canvasBlock.Complete();
                }
            }));
            return new Pipeline<BitmapRenderingPipelineContext>(renderBlock, canvasBlock);
        }

        private GLTaskScheduler _taskScheduler;

        private GLContextWrapper _mainContextWrapper;

        private IFrameBuffer _frameBuffer;

        private MultiPixelBuffer _multiStoragePixelBuffer;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        public IList<RenderingViewTarget> RenderingViewTargets { get; set; }
            = new List<RenderingViewTarget>();

        private async Task RenderThread(CancellationToken token, GLContextWrapper mainContextWrapper,
            Pipeline<BitmapRenderingPipelineContext> pipeline, IRenderer renderer, bool syncPipeline)
        {
            _stopwatch.Start();
            RenderTargetInfo targetInfo = null;
            GlRenderEventArgs renderEventArgs = null;
            try
            {
                OnGlInitialized();
                mainContextWrapper.MakeCurrent();
                if (!renderer.IsInitialized)
                {
                    renderer.Initialize(mainContextWrapper.Context);
                }

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (!IsUserVisible)
                        {
                            _userVisibleResetEvent.WaitInfinity();
                        }

                        if (!IsRenderContinuouslyValue)
                        {
                            _renderContinuousWaiter.WaitInfinity();
                        }

                        if (_recentTarget.IsEmpty)
                        {
                            _sizeNotEmptyWaiter.WaitInfinity();
                            continue;
                        }

                        var sizeChanged = false;
                        if (!Equals(targetInfo, _recentTarget) && !_recentTarget.IsEmpty)
                        {
                            targetInfo = _recentTarget;
                            renderEventArgs = targetInfo.GetRenderEventArgs();
                            _frameBuffer.Release();
                            _frameBuffer.Allocate(targetInfo);
                            sizeChanged = true;
                        }

                        var previewRender = renderer.PreviewRender();
                        foreach (var viewTarget in RenderingViewTargets)
                        {
                            if (viewTarget.CheckSize() || previewRender)
                            {
                                viewTarget.Render(renderer);
                            }
                        }

                        if (!previewRender && !sizeChanged)
                        {
                            Thread.Sleep(30);
                            // await mainContextBinding.Delay(30);
                            continue;
                        }

                        _frameBuffer.PreWrite();
                        if (sizeChanged)
                        {
                            renderer.Resize(targetInfo.PixelSize);
                        }

                        OnBeforeRender(renderEventArgs);
                        renderer.Render(renderEventArgs);
                        OnAfterRender(renderEventArgs);
                        _frameBuffer.PostRead();
                        var pixelBufferInfo = _multiStoragePixelBuffer.ReadPixelAndSwap(targetInfo);
                        pipeline.Post(new BitmapRenderingPipelineContext(targetInfo, _drawingGroup,
                            pixelBufferInfo, _renderCanvas)); // pipeline.SendAsync(postRender, token).Wait(token);
                        if (syncPipeline)
                        {
                            // ReSharper disable once MethodHasAsyncOverload
                            _semaphoreSlim.Wait(token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception exception)
                    {
                        OnRenderErrorReceived(new RenderErrorArgs(exception));
                    }

                    if (EnableFrameRateLimit)
                    {
                        var renderMinus = FrameGenerateSpan - _stopwatch.ElapsedMilliseconds;
                        if (renderMinus > 5)
                        {
                            await mainContextWrapper.Delay((int)renderMinus);
                        }
                        else if (renderMinus > 0)
                        {
                            Thread.Sleep((int)renderMinus);
                        }

                        _stopwatch.Restart();
                    }
                }
            }
            catch (Exception e)
            {
                OnRenderErrorReceived(new RenderErrorArgs(e));
            }
            finally
            {
                _stopwatch.Stop();
                renderer.Uninitialize();
                if (pipeline != null)
                {
                    await pipeline.Finish().ConfigureAwait(true);
                }

                _multiStoragePixelBuffer?.Release();
                _multiStoragePixelBuffer?.Dispose();
                _frameBuffer?.Release();
            }
        }

        private async Task CloseRenderThread()
        {
            try
            {
                _renderTokenSource.Cancel();
            }
            finally
            {
                _sizeNotEmptyWaiter.ForceSet();
                // _renderSyncWaiter.ForceSet();
                _userVisibleResetEvent.ForceSet();
                _renderContinuousWaiter.ForceSet();
                await _renderTask;
                _taskScheduler.Dispose();
                _mainContextWrapper.Dispose();
                if (!_isInternalTrigger)
                {
                    CompositionTarget.Rendering -= CompositionTarget_Rendering;
                }
            }
        }

        protected override void ResumeRender()
        {
            _renderContinuousWaiter.TrySet();
        }

        protected override async void EndRender()
        {
            await CloseRenderThread();
            base.EndRender();
        }

        protected override void OnUserVisibleChanged(PropertyChangedArgs<bool> args)
        {
            if (args.NewValue)
            {
                _userVisibleResetEvent.TrySet();
            }
        }

        public BitmapSource CreateSnapshot()
        {
            var targetBitmap = _recentTarget.CreateRenderTargetBitmap();
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawDrawing(_drawingGroup);
            }

            targetBitmap.Render(drawingVisual);
            return targetBitmap;
        }

        protected override void Dispose(bool dispose)
        {
            this._semaphoreSlim.Dispose();
            this._controlFps.Dispose();
            this._glFps.Dispose();
            this._userVisibleResetEvent.Dispose();
            this._renderContinuousWaiter.Dispose();
        }
    }
}