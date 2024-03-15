using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Media;
using OpenTK.Graphics.OpenGL4;
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
     3. 独立的两个渲染线程，线程同步的复杂度大幅提升
     最终采用方案3*/

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

        private readonly FpsCounter _glFps = new FpsCounter() { Title = "GL_FPS" };

        private readonly FpsCounter _controlFps = new FpsCounter() { Title = "ControlFPS" };

        public BitmapOpenTkControl() : base()
        {
            _localViewTarget = new RenderingViewTarget(this);
            RenderingViewTargets = new List<RenderingViewTarget>() { _localViewTarget };
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

        private readonly Point _startPoint = new Point(10, 10);

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            try
            {
                _localViewTarget.RenderCanvas?.Commit(drawingContext);
                // drawingContext.DrawDrawing(_localViewTarget.TargetDrawing);
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
                _localViewTarget.TryReleaseSync();
            }
        }

        private int _maxParallelism;

        private RenderingViewTarget _localViewTarget;

        protected override void StartRender()
        {
            _renderTokenSource = new CancellationTokenSource();
            var renderer = this.Renderer;
            var glSettings = this.GlSettings;
            var renderSetting = this.RenderSetting;
            _maxParallelism = renderSetting.GetParallelism();
            _multiPixelBuffer = new MultiPixelBuffer(_maxParallelism * 3);
            _mainContextWrapper = glSettings.NewContext();
            _mainContextWrapper.MakeCurrent();
            foreach (var viewTarget in RenderingViewTargets)
            {
                viewTarget.Initialize(renderSetting, glSettings, _maxParallelism);
            }

            GL.Enable(EnableCap.DebugOutput);
            GL.DebugMessageCallback(DebugProc, IntPtr.Zero);
            _taskScheduler = new GLTaskScheduler(_mainContextWrapper, DebugProc);
            var pipeline = BuildPipeline(_taskScheduler);
            _renderTask = Task.Run(async () =>
            {
                using (_renderTokenSource)
                {
                    await RenderThread(_renderTokenSource.Token, _mainContextWrapper, pipeline, renderer);
                }
            });
        }

        private Pipeline<BitmapRenderPipelineContext> BuildPipeline(TaskScheduler glContextTaskScheduler)
        {
            // run on gl thread, read buffer from pbo.
            var renderBlock = new TransformBlock<BitmapRenderPipelineContext, BitmapRenderPipelineContext>(
                args => { return args.ReadFrames(); },
                new ExecutionDataflowBlockOptions()
                {
                    SingleProducerConstrained = true,
                    TaskScheduler = glContextTaskScheduler,
                    MaxDegreeOfParallelism = 1,
                });
            //copy buffer to image source
            var frameBlock = new ActionBlock<BitmapRenderPipelineContext>(args =>
                {
                    if (args == null)
                    {
                        return;
                    }

                    var canvas = args.FlushAndSwap();
                    if (canvas != null)
                    {
                        args.ViewTarget.RenderCanvas = canvas;
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = (int)_maxParallelism,
                    SingleProducerConstrained = true,
                });
            renderBlock.LinkTo(frameBlock, new DataflowLinkOptions() { PropagateCompletion = true });
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
            return new Pipeline<BitmapRenderPipelineContext>(renderBlock, frameBlock);
        }

        private GLTaskScheduler _taskScheduler;

        private GLContextWrapper _mainContextWrapper;

        private MultiPixelBuffer _multiPixelBuffer;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        internal IList<RenderingViewTarget> RenderingViewTargets { get; set; }
            = new List<RenderingViewTarget>();

        private async Task RenderThread(CancellationToken token, GLContextWrapper mainContextWrapper,
            Pipeline<BitmapRenderPipelineContext> pipeline, IRenderer renderer)
        {
            _stopwatch.Start();
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
                        bool rendered = false;
                        var previewRender = renderer.PreviewRender();
                        foreach (var viewTarget in RenderingViewTargets)
                        {
                            if ((viewTarget.CheckSizeChange() || previewRender) && viewTarget.IsValid())
                            {
                                rendered = true;
                                var renderEventArgs = viewTarget.RenderArgs;
                                OnBeforeRender(renderEventArgs);
                                viewTarget.Render(renderer);
                                OnAfterRender(renderEventArgs);
                                var pixelBufferInfo = _multiPixelBuffer.ReadPixelAndSwap(viewTarget.TargetInfo);
                                var context = viewTarget.GetRenderContext(pixelBufferInfo);
                                pipeline.Post(context); // pipeline.SendAsync(postRender, token).Wait(token);
                                viewTarget.TryWaitSync(token);
                            }
                        }

                        if (rendered)
                        {
                            if (ShowFps)
                            {
                                _glFps.Increment();
                            }
                        }
                        else
                        {
                            Thread.Sleep(30);
                            // await mainContextBinding.Delay(30);
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

                _multiPixelBuffer?.Release();
                _multiPixelBuffer?.Dispose();
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
                // _renderSyncWaiter.ForceSet();
                await _renderTask;
                _taskScheduler.Dispose();
                _mainContextWrapper.Dispose();
            }
        }

        protected override async void EndRender()
        {
            await CloseRenderThread();
            base.EndRender();
        }

        protected override void OnUserVisibleChanged(PropertyChangedArgs<bool> args)
        {
            this._localViewTarget.Visible = args.NewValue;
        }

        /*public BitmapSource CreateSnapshot()
        {
            var targetBitmap = _recentTarget.CreateRenderTargetBitmap();
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawDrawing(_drawingGroup);
            }

            targetBitmap.Render(drawingVisual);
            return targetBitmap;
        }*/

        protected override void Dispose(bool dispose)
        {
            foreach (var renderingViewTarget in RenderingViewTargets)
            {
                renderingViewTarget.Dispose();
            }
            this._controlFps.Dispose();
            this._glFps.Dispose();
        }


        public static readonly DependencyProperty BindViewProperty = DependencyProperty.RegisterAttached(
            "BindView", typeof(BitmapOpenTkControl), typeof(BitmapOpenTkControl),
            new PropertyMetadata(default(BitmapOpenTkControl), (BindViewChangedCallback)));

        private static void BindViewChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is OpenTKViewControl viewControl)
            {
                viewControl.ViewTarget = null;
                if (e.OldValue is BitmapOpenTkControl bitmapOpenTkControl)
                {
                    var viewTarget =
                        bitmapOpenTkControl.RenderingViewTargets.FirstOrDefault(
                            target => target.Element == viewControl);
                    if (viewTarget != null)
                    {
                        viewTarget.Dispose();
                        bitmapOpenTkControl.RenderingViewTargets.Remove(viewTarget);
                    }
                }

                if (e.NewValue is BitmapOpenTkControl control)
                {
                    var renderingViewTarget = new RenderingViewTarget(viewControl);
                    control.RenderingViewTargets.Add(renderingViewTarget);
                    viewControl.ViewTarget = renderingViewTarget;
                }
            }
        }

        public static void SetBindView(OpenTKViewControl element, BitmapOpenTkControl value)
        {
            element.SetValue(BindViewProperty, value);
        }

        public static BitmapOpenTkControl GetBindView(OpenTKViewControl element)
        {
            return (BitmapOpenTkControl)element.GetValue(BindViewProperty);
        }
    }
}