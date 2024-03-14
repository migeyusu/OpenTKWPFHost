using System;
using System.Windows;

namespace OpenTkWPFHost.Control
{
    public class OpenTKViewControl : FrameworkElement, IDisposable
    {
        static OpenTKViewControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OpenTKViewControl),
                new FrameworkPropertyMetadata(typeof(OpenTKViewControl)));
        }

        
        
        public void Dispose()
        {
            // TODO 在此释放托管资源
        }
    }
}