using System;
using System.Windows.Media;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.DirectX
{
    public class DXFrameArgs : PipelineArgs
    {
        public IntPtr RenderTargetIntPtr { get; }

        public DXFrameArgs(IntPtr renderTargetIntPtr, RenderTargetInfo renderTargetInfo, DrawingGroup group)
            : base(renderTargetInfo, group)
        {
            RenderTargetIntPtr = renderTargetIntPtr;
        }
    }
}