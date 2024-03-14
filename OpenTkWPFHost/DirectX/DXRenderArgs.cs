using System;
using System.Windows.Media;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.DirectX
{
    public class DXRenderArgs : PipelineArgs
    {
        public IntPtr RenderTargetIntPtr { get; }

        public DXRenderArgs(RenderTargetInfo targetInfo, IntPtr renderTargetIntPtr,
            DrawingGroup drawingGroup) : base(targetInfo, drawingGroup)
        {
            RenderTargetIntPtr = renderTargetIntPtr;
        }
    }
}