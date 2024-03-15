using System;
using System.Threading;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Bitmap
{
    /// <summary>
    /// highest performance, but possibly cause stuck on low end cpu (2 physical core)
    /// </summary>
    public class MultiPixelBuffer : IDisposable
    {
        private readonly int _bufferCount;

        public MultiPixelBuffer(int bufferCount)
        {
            if (bufferCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCount));
            }

            _bufferCount = bufferCount;
            _bufferInfos = new PixelBufferInfo[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                _bufferInfos[i] = new PixelBufferInfo();
            }
        }

        public MultiPixelBuffer() : this(3)
        {
        }

        private readonly PixelBufferInfo[] _bufferInfos;

        /// <summary>
        /// 先写入缓冲，然后才能读取，所以写入缓冲=读取缓冲+1
        /// </summary>
        private long _currentWriteBufferIndex = 0;

        public void Release()
        {
            foreach (var bufferInfo in _bufferInfos)
            {
                bufferInfo.Release();
            }
        }

        private SpinWait _spinWait = new SpinWait();


        /// <summary>
        /// write current frame to buffer
        /// </summary>
        /// <param name="renderTargetInfo"></param>
        public PixelBufferInfo ReadPixelAndSwap(RenderTargetInfo renderTargetInfo)
        {
            var writeBufferIndex = _currentWriteBufferIndex % _bufferCount;
            var writePixelBufferInfo = _bufferInfos[writeBufferIndex];
            while (writePixelBufferInfo.HasBuffer)
            {
                _spinWait.SpinOnce();
            }

            writePixelBufferInfo.AddFence(renderTargetInfo);
            _currentWriteBufferIndex++;
            return writePixelBufferInfo;
        }

        public void Dispose()
        {
            foreach (var pixelBufferInfo in _bufferInfos)
            {
                pixelBufferInfo.Dispose();
            }
        }
    }
}