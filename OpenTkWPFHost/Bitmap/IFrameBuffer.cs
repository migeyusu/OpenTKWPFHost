using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Bitmap
{
    public interface IFrameBuffer
    {
        /// <summary>
        /// FBO
        /// </summary>
        int FrameBufferObject { get; }
        
        int RenderBufferObject { get; }

        internal void Allocate(RenderTargetInfo renderTargetInfo);

        internal void Release();

        internal void PreWrite();

        internal void PostRead();

        internal void Clear();
    }
}