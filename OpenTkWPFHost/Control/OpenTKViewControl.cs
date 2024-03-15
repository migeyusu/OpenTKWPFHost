using System;
using System.Windows;
using System.Windows.Media;
using OpenTkWPFHost.Bitmap;

namespace OpenTkWPFHost.Control
{
    public class OpenTKViewControl : FrameworkElement, IDisposable
    {
        static OpenTKViewControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OpenTKViewControl),
                new FrameworkPropertyMetadata(typeof(OpenTKViewControl)));
        }

        internal RenderingViewTarget ViewTarget { get; set; }

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