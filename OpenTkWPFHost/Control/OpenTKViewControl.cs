using System;
using System.Windows;
using System.Windows.Media;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Bitmap;

namespace OpenTkWPFHost.Control
{
    /// <summary>
    /// <see cref="OpenTkControlBase"/>的附属（Subordinate）控件，使用绑定的<see cref="OpenTkControlBase"/>的渲染循环。
    /// <para>可以在一个OpenGL上下文渲染多个Control，解决了OpenGL不能共享上下文的问题</para> 
    /// </summary>
    public class OpenTKSubControl : FrameworkElement, IDisposable
    {
        static OpenTKSubControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OpenTKSubControl),
                new FrameworkPropertyMetadata(typeof(OpenTKSubControl)));
        }

        internal RenderViewTarget ViewTarget { get; set; }

        public static readonly DependencyProperty RendererProperty = DependencyProperty.Register(
            nameof(Renderer), typeof(IRenderer), typeof(OpenTKSubControl), new PropertyMetadata(default(IRenderer)));

        public IRenderer Renderer
        {
            get { return (IRenderer)GetValue(RendererProperty); }
            set { SetValue(RendererProperty, value); }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ViewTarget != null)
            {
                try
                {
                    ViewTarget.RenderCanvas?.Commit(drawingContext);
                }
                finally
                {
                    ViewTarget.TryReleaseSync();
                }
            }
        }

        public void Dispose()
        {
        }
    }
}