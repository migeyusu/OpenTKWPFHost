﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Platform;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Bitmap;
using OpenTkWPFHost.Configuration;
using OpenTkWPFHost.Core;
using WindowState = System.Windows.WindowState;

namespace OpenTkWPFHost.Control
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class OpenTkControlBase : FrameworkElement, IDisposable
    {
        /// <summary>
        /// Initialize the OpenTk Toolkit
        /// </summary>
        static OpenTkControlBase()
        {
        }

        public event EventHandler<OpenGlErrorArgs> OpenGlErrorReceived;

        public event EventHandler<RenderErrorArgs> RenderErrorReceived;

        /// <summary>
        /// renderer is ready
        /// </summary>
        public event EventHandler<GlRenderEventArgs> BeforeRender;

        /// <summary>
        /// after successfully render
        /// </summary>
        public event EventHandler<GlRenderEventArgs> AfterRender;

        public event EventHandler GlInitialized;


        public static readonly DependencyProperty GlSettingsProperty = DependencyProperty.Register(
            "GlSettings", typeof(GLSettings), typeof(OpenTkControlBase), new PropertyMetadata(new GLSettings()));

        public GLSettings GlSettings
        {
            get { return (GLSettings)GetValue(GlSettingsProperty); }
            set { SetValue(GlSettingsProperty, value); }
        }

        public static readonly DependencyProperty RenderSettingProperty = DependencyProperty.Register(
            "RenderSetting", typeof(RenderSetting), typeof(OpenTkControlBase),
            new PropertyMetadata(new RenderSetting()));

        public RenderSetting RenderSetting
        {
            get { return (RenderSetting)GetValue(RenderSettingProperty); }
            set { SetValue(RenderSettingProperty, value); }
        }

        /// <summary>
        /// renderer 
        /// </summary>
        public static readonly DependencyProperty RendererProperty = DependencyProperty.Register(
            "Renderer", typeof(IRenderer), typeof(OpenTkControlBase), new PropertyMetadata(default(IRenderer)));

        /// <summary>
        /// must be set before render start
        /// </summary>
        public IRenderer Renderer
        {
            get { return (IRenderer)GetValue(RendererProperty); }
            set { SetValue(RendererProperty, value); }
        }

        public static readonly DependencyProperty IsShowFpsProperty =
            DependencyProperty.Register("IsShowFps", typeof(bool), typeof(OpenTkControlBase),
                new PropertyMetadata(false));

        public bool IsShowFps
        {
            get { return (bool)GetValue(IsShowFpsProperty); }
            set { SetValue(IsShowFpsProperty, value); }
        }

        public static readonly DependencyProperty MaxFrameRateLimitProperty = DependencyProperty.Register(
            "MaxFrameRateLimit", typeof(int), typeof(OpenTkControlBase), new PropertyMetadata(-1));

        /// <summary>
        /// if lower than 0, infinity
        /// </summary>
        public int MaxFrameRateLimit
        {
            get { return (int)GetValue(MaxFrameRateLimitProperty); }
            set { SetValue(MaxFrameRateLimitProperty, value); }
        }

        public static readonly DependencyProperty IsRenderStaredProperty = DependencyProperty.Register(
            "IsRenderStared", typeof(bool), typeof(OpenTkControlBase), new PropertyMetadata(default(bool)));


        /// <summary>
        /// indicate whether render is started.
        /// </summary>
        public bool IsRenderStared
        {
            get { return (bool)GetValue(IsRenderStaredProperty); }
            protected set { SetValue(IsRenderStaredProperty, value); }
        }

        /// <summary>
        /// control renderer lifecycle
        /// </summary>
        public ControlLifeCycle LifeCycle
        {
            get { return (ControlLifeCycle)GetValue(LifeCycleProperty); }
            set { SetValue(LifeCycleProperty, value); }
        }

        /// <summary>
        /// default is bound to window as wpf window cannot reuse after close
        /// </summary>
        public static readonly DependencyProperty LifeCycleProperty = DependencyProperty.Register(
            "LifeCycle", typeof(ControlLifeCycle), typeof(OpenTkControlBase),
            new PropertyMetadata(ControlLifeCycle.BoundToWindow));

        public static readonly DependencyProperty IsAutoAttachProperty = DependencyProperty.Register(
            "IsAutoAttach", typeof(bool), typeof(OpenTkControlBase), new PropertyMetadata(false));

        /// <summary>
        /// if set to true, will start rendering when this element is loaded.
        /// </summary>
        public bool IsAutoAttach
        {
            get { return (bool)GetValue(IsAutoAttachProperty); }
            set { SetValue(IsAutoAttachProperty, value); }
        }

        public static readonly DependencyProperty IsRenderContinuouslyProperty = DependencyProperty.Register(
            "IsRenderContinuously", typeof(bool), typeof(OpenTkControlBase), new PropertyMetadata(true));

        /// <summary>
        /// whether render continuous, if not need to manually call update 
        /// </summary>
        public bool IsRenderContinuously
        {
            get { return (bool)GetValue(IsRenderContinuouslyProperty); }
            set { SetValue(IsRenderContinuouslyProperty, value); }
        }

        private volatile bool _isUserVisible;

        /// <summary>
        /// a combination of window closed/minimized, control unloaded/visibility status
        /// indicate whether user can see the control,
        /// </summary>
        protected bool IsUserVisible
        {
            get => _isUserVisible;
            set
            {
                var propertyChangedArgs = new PropertyChangedArgs<bool>(_isUserVisible, value);
                _isUserVisible = value;
                OnUserVisibleChanged(propertyChangedArgs);
            }
        }

        private WindowState _windowState;

        /// <summary>
        /// window visibility
        /// </summary>
        private bool _isWindowVisible;

        private bool _isWindowLoaded;

        private bool _isWindowClosed;

        private bool _isControlLoaded;

        private bool _isControlVisible;

        /// <summary>
        /// True if OnLoaded has already been called
        /// </summary>
        private bool _alreadyLoaded;

        protected volatile bool IsRenderContinuouslyValue;

        protected volatile int FrameGenerateSpan;

        protected volatile bool EnableFrameRateLimit;

        protected bool ShowFps = (bool)IsShowFpsProperty.DefaultMetadata.DefaultValue;

        /// <summary>
        /// Creates the <see cref="OpenTkControlBase"/>/>
        /// </summary>
        protected OpenTkControlBase()
        {
            DebugProc = Callback;
            _debugProcCallbackHandle = GCHandle.Alloc(DebugProc);
            //used for fast read and thread safe
            DependencyPropertyDescriptor.FromProperty(IsShowFpsProperty, typeof(OpenTkControlBase))
                .AddValueChanged(this, (sender, args) => ShowFps = IsShowFps);
            DependencyPropertyDescriptor.FromProperty(IsRenderContinuouslyProperty, typeof(OpenTkControlBase))
                .AddValueChanged(this, (sender, args) =>
                {
                    this.IsRenderContinuouslyValue = IsRenderContinuously;
                    if (this.IsRenderContinuouslyValue)
                    {
                        ResumeRender();
                    }
                });
            DependencyPropertyDescriptor.FromProperty(MaxFrameRateLimitProperty, typeof(OpenTkControlBase))
                .AddValueChanged(this,
                    (sender, args) => { ApplyMaxFrameRate(this.MaxFrameRateLimit); });
            ApplyMaxFrameRate((int)MaxFrameRateLimitProperty.DefaultMetadata.DefaultValue);
            this.IsRenderContinuouslyValue = (bool)IsRenderContinuouslyProperty.DefaultMetadata.DefaultValue;
            Loaded += (sender, args) =>
            {
                if (_alreadyLoaded)
                    return;
                _alreadyLoaded = true;
                OnLoaded(sender, args);
            };
            Unloaded += (sender, args) =>
            {
                if (!_alreadyLoaded)
                    return;

                _alreadyLoaded = false;
                OnUnloaded(sender, args);
            };
            Application.Current.Exit += (sender, args) => { this.Dispose(); };
            this.IsVisibleChanged += OpenTkControlBase_IsVisibleChanged;
        }

        private void ApplyMaxFrameRate(int maxFrameRate)
        {
            if (maxFrameRate < 1)
            {
                EnableFrameRateLimit = false;
                return;
            }

            EnableFrameRateLimit = true;
            FrameGenerateSpan = (int)(1000d / maxFrameRate);
        }

        /// <summary>
        /// resume render procedure
        /// </summary>
        protected abstract void ResumeRender();

        /// <summary>
        /// manually call render loop regardless of double buffer mechanism
        /// </summary>
        public void CallValidRenderOnce()
        {
            if (!IsRenderContinuouslyValue && IsRenderStared && IsUserVisible)
            {
                ResumeRender();
            }
        }

        private void UpdateUserVisible()
        {
            this.IsUserVisible = _windowState != WindowState.Minimized && !_isWindowClosed && _isControlVisible &&
                                 _isWindowVisible && _isWindowLoaded && _isControlLoaded;
        }

        private void OpenTkControlBase_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _isControlVisible = (bool)e.NewValue;
            UpdateUserVisible();
        }

        private void HostWindow_StateChanged(object sender, EventArgs e)
        {
            _windowState = ((Window)sender).WindowState;
            UpdateUserVisible();
        }

        private void HostWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _isWindowVisible = (bool)e.NewValue;
            UpdateUserVisible();
        }

        private void HostWindow_Closed(object sender, EventArgs e)
        {
            _isWindowClosed = true;
            UpdateUserVisible();
            if (LifeCycle == ControlLifeCycle.BoundToWindow)
            {
                Close();
            }
        }

        /// <summary>
        /// explicitly start render procedure
        /// </summary>
        /// <param name="hostWindow"></param>
        public void Start(Window hostWindow)
        {
            if (Renderer == null)
            {
                throw new NotSupportedException($"Can't start render procedure as {nameof(Renderer)} is null!");
            }

            if (GlSettings == null)
            {
                throw new NotSupportedException($"Can't start render procedure as {nameof(GlSettings)} is null!");
            }


            if (hostWindow == null)
            {
                throw new ArgumentNullException(nameof(hostWindow));
            }

            if (IsRenderStared)
            {
                return;
            }

            _isWindowClosed = false;
            _windowState = hostWindow.WindowState;
            _isWindowVisible = hostWindow.IsVisible;
            _isWindowLoaded = hostWindow.IsLoaded;
            _isControlVisible = this.IsVisible;
            _isControlLoaded = this.IsLoaded;
            UpdateUserVisible();
            var baseHandle = new WindowInteropHelper(hostWindow).Handle;
            _hWndSource = new HwndSource(0, 0, 0, 0, 0, "GLWpfControl", baseHandle);
            hostWindow.Closed += HostWindow_Closed;
            hostWindow.IsVisibleChanged += HostWindow_IsVisibleChanged;
            hostWindow.StateChanged += HostWindow_StateChanged;
            this.StartRender();
            this.IsRenderStared = true;
        }

        /// <summary>
        /// explicitly close render procedure, can reopen
        /// <para>will be called in dispose</para>
        /// </summary>
        public void Close()
        {
            if (!IsRenderStared)
            {
                return;
            }

            EndRender();
            this.IsRenderStared = false;
        }

        /// <summary>
        /// close render procedure
        /// only dispose render procedure! 
        /// </summary>
        protected virtual void EndRender()
        {
            _hWndSource?.Dispose();
        }

        /// <summary>
        /// open render procedure
        /// </summary>
        protected abstract void StartRender();

        /// <summary>
        /// after <see cref="IsUserVisible"/> changed
        /// </summary>
        protected abstract void OnUserVisibleChanged(PropertyChangedArgs<bool> args);

        /// <summary>
        /// Check if it is run in designer mode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsDesignMode() => DesignerProperties.GetIsInDesignMode(this);

        protected override void OnRender(DrawingContext drawingContext)
        {
#if DEBUG
            if (IsDesignMode())
            {
                var labelText = this.GetType().Name;
                var width = this.ActualWidth;
                var height = this.ActualHeight;
                var size = 1.5 * Math.Min(width, height) / labelText.Length;
                var tf = new Typeface("Arial");
#pragma warning disable 618
                var ft = new FormattedText(labelText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size,
                    Brushes.Black)
                {
                    TextAlignment = TextAlignment.Center
                };
#pragma warning restore 618
                var y = (height + ft.Height) / 2 + 5;
                drawingContext.DrawLine(new Pen(Brushes.DodgerBlue, 6.0),
                    new Point((width - ft.Width) / 2, y),
                    new Point((width + ft.Width) / 2, y));
                drawingContext.DrawText(ft, new Point(width / 2, (height - ft.Height) / 2));
                return;
            }
#endif
            base.OnRender(drawingContext);

            /*if (!IsRendererOpened)
            {
                UnstartedControlHelper.DrawUnstartedControlHelper(this, drawingContext);
            }*/
        }

        private HwndSource _hWndSource;

        /// <summary>
        /// Get window handle, if null, call <see cref="Start"/>
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="args">Information about the event</param>
        protected virtual void OnLoaded(object sender, RoutedEventArgs args)
        {
            _isControlLoaded = true;
            UpdateUserVisible();
            if (!IsRenderStared && IsAutoAttach)
            {
                var window = Window.GetWindow(this);
                if (window == null)
                {
                    return;
                }

                Start(window);
            }
        }

        /// <summary>
        /// Called when this control is unloaded
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="args">Information about the event</param>
        protected virtual void OnUnloaded(object sender, RoutedEventArgs args)
        {
            _isControlLoaded = false;
            UpdateUserVisible();
            if (IsDesignMode())
            {
                return;
            }

            if (LifeCycle == ControlLifeCycle.Self)
            {
                this.Close();
            }
        }

        private static GCHandle _debugProcCallbackHandle;

        protected readonly DebugProc DebugProc;

        protected void Callback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length,
            IntPtr message, IntPtr userParam)
        {
            OnOpenGlErrorReceived(
                new OpenGlErrorArgs(source, type, id, severity, length, message, userParam));
        }

        protected virtual void OnOpenGlErrorReceived(OpenGlErrorArgs e)
        {
            OpenGlErrorReceived?.Invoke(this, e);
        }

        private bool _isDisposed;

        /// <summary>
        /// can't reopen render procedure
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Close();
            if (_debugProcCallbackHandle.IsAllocated)
            {
                _debugProcCallbackHandle.Free();
            }

            Dispose(true);
            _isDisposed = true;
        }

        protected abstract void Dispose(bool dispose);

        protected virtual void OnAfterRender(GlRenderEventArgs renderEventArgs)
        {
            AfterRender?.Invoke(this, renderEventArgs);
        }

        protected virtual void OnBeforeRender(GlRenderEventArgs renderEventArgs)
        {
            BeforeRender?.Invoke(this, renderEventArgs);
        }

        protected virtual void OnRenderErrorReceived(RenderErrorArgs e)
        {
            RenderErrorReceived?.Invoke(this, e);
        }

        protected virtual void OnGlInitialized()
        {
            GlInitialized?.Invoke(this, EventArgs.Empty);
        }
    }
}