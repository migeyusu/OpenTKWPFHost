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
using Color = System.Drawing.Color;
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
            _localViewTarget = new RenderViewTarget(this);
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

        private readonly RenderViewTarget _localViewTarget;

        protected override void StartRender()
        {
            _renderTokenSource = new CancellationTokenSource();
            var glSettings = this.GlSettings;
            var renderSetting = this.RenderSetting;
            _maxParallelism = renderSetting.GetParallelism();
            _multiPixelBuffer = new MultiPixelBuffer(_maxParallelism * 3);
            var mainContextWrapper = glSettings.NewContext();
            mainContextWrapper.MakeCurrent();
            RenderingViewTargets.Clear();
            _localViewTarget.Renderer = this.Renderer;
            RenderingViewTargets.Add(_localViewTarget);
            foreach (var control in _registedSubControls.Where(control => control.Renderer != null))
            {
                var renderViewTarget = new RenderViewTarget(control) { Renderer = control.Renderer };
                control.ViewTarget = renderViewTarget;
                RenderingViewTargets.Add(renderViewTarget);
            }

            foreach (var viewTarget in RenderingViewTargets)
            {
                viewTarget.Initialize(renderSetting, glSettings, _maxParallelism);
            }

            GL.Enable(EnableCap.DebugOutput);
            GL.DebugMessageCallback(DebugProc, IntPtr.Zero);
            OnGlInitialized(mainContextWrapper.Context);
            _taskScheduler = new GLTaskScheduler(mainContextWrapper, DebugProc);
            var pipeline = BuildPipeline(_taskScheduler);
            _renderTask = Task.Run(async () =>
            {
                using (_renderTokenSource)
                {
                    await RenderThread(_renderTokenSource.Token, mainContextWrapper, pipeline);
                }
            });
        }

        private Pipeline<BitmapRenderPipelineContext> BuildPipeline(TaskScheduler glContextTaskScheduler)
        {
            // run on gl thread, read buffer from pbo.
            var renderBlock = new TransformBlock<BitmapRenderPipelineContext, BitmapRenderPipelineContext>(
                args => args.ReadFrames(),
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
                    MaxDegreeOfParallelism = _maxParallelism,
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

        private MultiPixelBuffer _multiPixelBuffer;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        internal IList<RenderViewTarget> RenderingViewTargets { get; set; }
            = new List<RenderViewTarget>();

        private async Task RenderThread(CancellationToken token, GLContextWrapper mainContextWrapper,
            Pipeline<BitmapRenderPipelineContext> pipeline)
        {
            _stopwatch.Start();
            try
            {
                mainContextWrapper.MakeCurrent();
                var graphicsContext = mainContextWrapper.Context;
                foreach (var viewTarget in RenderingViewTargets)
                {
                    viewTarget.Initialize(graphicsContext);
                }

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        bool rendered = false;
                        foreach (var viewTarget in RenderingViewTargets)
                        {
                            var previewRender = viewTarget.Renderer.PreviewRender();
                            if ((viewTarget.CheckSizeChange() || previewRender) && viewTarget.IsValid())
                            {
                                rendered = true;
                                var renderEventArgs = viewTarget.RenderArgs;
                                OnBeforeRender(renderEventArgs);
                                viewTarget.Render();
                                OnAfterRender(renderEventArgs);
                                var pixelBufferInfo = _multiPixelBuffer.ReadPixelAndSwap(viewTarget.TargetInfo);
                                var context = viewTarget.CreateRenderContext(pixelBufferInfo);
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

                OnGlDisposing(graphicsContext);
            }
            catch (Exception e)
            {
                OnRenderErrorReceived(new RenderErrorArgs(e));
            }
            finally
            {
                _stopwatch.Stop();
                foreach (var viewTarget in RenderingViewTargets)
                {
                    viewTarget.Dispose();
                }

                if (pipeline != null)
                {
                    await pipeline.Finish().ConfigureAwait(true);
                }

                _multiPixelBuffer?.Release();
                _multiPixelBuffer?.Dispose();
                _taskScheduler.Dispose();
                mainContextWrapper.Dispose();
            }
        }

        protected override async void EndRender()
        {
            try
            {
                _renderTokenSource.Cancel();
            }
            finally
            {
                // _renderSyncWaiter.ForceSet();
                await _renderTask;
            }
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
            this._controlFps.Dispose();
            this._glFps.Dispose();
        }

        private readonly List<OpenTKSubControl> _registedSubControls = new List<OpenTKSubControl>(3);

        public static readonly DependencyProperty BindViewProperty = DependencyProperty.RegisterAttached(
            "BindView", typeof(BitmapOpenTkControl), typeof(BitmapOpenTkControl),
            new PropertyMetadata(default(BitmapOpenTkControl), (BindViewChangedCallback)));

        private static void BindViewChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is OpenTKSubControl viewControl)
            {
                viewControl.ViewTarget = null;
                if (e.OldValue is BitmapOpenTkControl bitmapOpenTkControl)
                {
                    bitmapOpenTkControl._registedSubControls.Remove(viewControl);
                }

                if (e.NewValue is BitmapOpenTkControl control)
                {
                    control._registedSubControls.Add(viewControl);
                }
            }
        }

        public static void SetBindView(OpenTKSubControl element, BitmapOpenTkControl value)
        {
            element.SetValue(BindViewProperty, value);
        }

        public static BitmapOpenTkControl GetBindView(OpenTKSubControl element)
        {
            return (BitmapOpenTkControl)element.GetValue(BindViewProperty);
        }
    }
}