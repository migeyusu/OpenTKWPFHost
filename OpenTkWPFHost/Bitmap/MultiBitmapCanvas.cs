﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using OpenTkWPFHost.Abstraction;

namespace OpenTkWPFHost.Bitmap
{
    /// <summary>
    /// 可绘制多画布
    /// </summary>
    public class MultiBitmapCanvas
    {
        private readonly int _bufferCount;

        private readonly BitmapCanvas[] _bitmapCanvasCollection;

        /// <summary>
        /// 先写入缓冲，然后才能读取，所以写入缓冲=读取缓冲+1
        /// </summary>
        private volatile int _currentWriteCanvasIndex = 0;

        public MultiBitmapCanvas() : this(2)
        {
        }

        public MultiBitmapCanvas(int bufferSize)
        {
            if (bufferSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            _bufferCount = bufferSize;
            _bitmapCanvasCollection = new BitmapCanvas[bufferSize];
            for (int i = 0; i < bufferSize; i++)
            {
                _bitmapCanvasCollection[i] = new BitmapCanvas();
            }
        }

        public BitmapCanvas Swap()
        {
            var writeBufferIndex = Interlocked.Increment(ref _currentWriteCanvasIndex);
            writeBufferIndex %= _bufferCount;
            return _bitmapCanvasCollection[writeBufferIndex];
        }
    }
}