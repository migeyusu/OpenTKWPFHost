using OpenTkWPFHost.Core;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;

namespace OpenTkWPFHost.Configuration
{
    public class RenderSetting : MarkupExtension
    {
        public RenderTactic RenderTactic { get; set; } = RenderTactic.Default;

        /// If this is set to false, the control will render without any DPI scaling.
        /// This will result in higher performance and a worse image quality on systems with >100% DPI settings, such as 'Retina' laptop screens with 4K UHD at small sizes.
        /// This setting may be useful to get extra performance on mobile platforms.
        public bool UseDeviceDpi { get; set; } = false;

        public RenderTargetInfo CreateRenderTarget(FrameworkElement element)
        {
            if (!UseDeviceDpi)
            {
                return new RenderTargetInfo((int)element.ActualWidth, (int)element.ActualHeight, 1, 1);
            }

            var dpiScaleX = 1.0;
            var dpiScaleY = 1.0;
            var presentationSource = PresentationSource.FromVisual(element);
            // this can be null in the case of not having any visual on screen, such as a tabbed view.
            if (presentationSource != null)
            {
                Debug.Assert(presentationSource.CompositionTarget != null,
                    "presentationSource.CompositionTarget != null");
                var transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
                dpiScaleX = transformToDevice.M11;
                dpiScaleY = transformToDevice.M22;
            }

            return new RenderTargetInfo((int)element.ActualWidth, (int)element.ActualHeight, dpiScaleX, dpiScaleY);
        }

        public int GetParallelism()
        {
            var maxParallelism = Environment.ProcessorCount / 2; //hyper threading or P-E cores?
            switch (this.RenderTactic)
            {
                case RenderTactic.Default:
                    maxParallelism = 1;
                    break;
                case RenderTactic.ThroughoutPriority:
                    if (maxParallelism > 3)
                    {
                        maxParallelism = 3;
                    }

                    break;
                case RenderTactic.LatencyPriority:
                    if (maxParallelism > 3)
                    {
                        maxParallelism = 3;
                    }

                    break;
                case RenderTactic.MaxThroughout:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return maxParallelism;
        }

        public bool GetRenderSyncEnable()
        {
            if (this.RenderTactic == RenderTactic.Default || this.RenderTactic == RenderTactic.LatencyPriority)
            {
                return true;
            }

            return false;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}