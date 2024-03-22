using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using OpenTkWPFHost.Core;


namespace OpenTkControlExample
{
    public partial class MainWindow
    {
        TestRendererCase testRendererCase = new TestRendererCase();
        
        
        public MainWindow()
        {
            this.InitializeComponent();
            Loaded += MainWindow_Loaded;
            this.OpenTkControl.Renderer = testRendererCase.Renderer;
            this.OpenTkControl.OpenGlErrorReceived += OpenTkControl_OpenGlErrorReceived;
            this.SubControl.Renderer = testRendererCase.SubRenderer;
            this.SubControl2.Renderer = testRendererCase.SubRenderer2;
            /*GlWpfControl.Start(new GLWpfControlSettings()
            {
                MajorVersion = 4,
                MinorVersion = 3,
                GraphicsProfile = ContextProfile.Core,
            });*/
        }

        protected void Callback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length,
            IntPtr message, IntPtr userParam)
        {
            var openGlErrorArgs = new OpenGlErrorArgs(source, type, id, severity, length, message, userParam);
            Debug.WriteLine(openGlErrorArgs);
        }

        private static void OpenTkControl_OpenGlErrorReceived(object sender, OpenGlErrorArgs e)
        {
            if (e.Severity != DebugSeverity.DebugSeverityHigh)
            {
                return;
            }

            var s = e.ToString();
            Debug.WriteLine(s);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            /*GlWpfControl.Start(new GLWpfControlSettings()
            {
                MajorVersion = 4, MinorVersion = 3, GraphicsProfile = ContextProfile.Core,
                RenderContinuously = true,
            });*/
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            this.OpenTkControl.Start(this);
        }


        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            this.OpenTkControl.Close();
        }

        private void Test_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private void FrameRate_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            OpenTkControl.MaxFrameRateLimit = (int)e.NewValue;
        }

        private readonly TestRenderer _emptyRenderer = new TestRenderer();

        private void GLWpfControl_OnRender(TimeSpan obj)
        {
            var renderer = testRendererCase.Renderer;
            if (!renderer.IsInitialized)
            {
                renderer.Initialize(null);
            }
            if (renderer.PreviewRender())
            {
                renderer.Render(new GlRenderEventArgs((int)this.ActualWidth, (int)this.ActualHeight, false, null));
            }

            /*var subRenderer = testRendererCase.SubRenderer;
            if (subRenderer.PreviewRender())
            {
                subRenderer.Render(new GlRenderEventArgs((int)this.ActualWidth, (int)this.ActualHeight, false, null));
            }*/
        }
    }
}