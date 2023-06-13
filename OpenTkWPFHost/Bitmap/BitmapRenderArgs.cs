using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Bitmap
{
    public class BitmapRenderArgs : RenderArgs
    {
        public PixelBufferInfo BufferInfo { get; set; }

        public BitmapRenderArgs(RenderTargetInfo targetInfo) : base(targetInfo)
        {
        }

        public BitmapFrameArgs ReadFrames()
        {
            var bufferInfo = this.BufferInfo;
            if (bufferInfo.WaitFence())
            {
                return new BitmapFrameArgs(this.TargetInfo, bufferInfo);
            }

            return null;
        }
    }
}