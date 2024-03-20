using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Bitmap
{
    public class BitmapCanvas
    {
        /// <summary>
        /// The source of the internal Image
        /// </summary>
        private WriteableBitmap _bitmap;

        private IntPtr _displayBuffer;

        private Rect DirtRect => _currentTargetInfo.Rect;

        private Int32Rect Int32Rect => _currentTargetInfo.Int32Rect;

        private int BufferSize => _currentTargetInfo.BufferSize;

        private RenderTargetInfo _currentTargetInfo;

        private TransformGroup _transformGroup;

        public bool IsAllocated { get; private set; } = false;

        public IntPtr DisplayBuffer => _displayBuffer;

        //allocate必须在UI线程上完成，否则将需要频繁地冻结以跨线程，性能损失较大
        private void Allocate(RenderTargetInfo targetInfo)
        {
            _currentTargetInfo = targetInfo;
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(new ScaleTransform(1, -1));
            _transformGroup.Children.Add(new TranslateTransform(0, targetInfo.ActualHeight));
            _transformGroup.Freeze();
            _bitmap = new WriteableBitmap(targetInfo.PixelWidth, targetInfo.PixelHeight, targetInfo.DpiX,
                targetInfo.DpiY, PixelFormats.Pbgra32, null);
            _displayBuffer = _bitmap.BackBuffer;
            IsAllocated = true;
            unsafe
            {
                fixed (byte* source = _buffer)
                {
                    Buffer.MemoryCopy(source, _displayBuffer.ToPointer(), BufferSize, BufferSize);
                }
            }
        }

        private byte[] _buffer = null;

        /// <summary>
        /// 中间缓存大小
        /// </summary>
        private int _intermidiateBufferSize = 0;

        private RenderTargetInfo _preAllocateTargetInfo;

        /// <summary>
        /// 预写入数据
        /// </summary>
        public bool TryAllocate(RenderTargetInfo targetInfo, IntPtr source)
        {
            if (Equals(targetInfo, _currentTargetInfo))
            {
                return true;
            }

            //pre allocate
            _preAllocateTargetInfo = targetInfo;
            var bufferSize = targetInfo.BufferSize;
            if (!_intermidiateBufferSize.Equals(bufferSize))
            {
                _buffer = new byte[bufferSize];
                _intermidiateBufferSize = bufferSize;
            }

            unsafe
            {
                fixed (byte* pointer = _buffer)
                {
                    Buffer.MemoryCopy(source.ToPointer(), pointer, bufferSize, bufferSize);
                }
            }

            IsAllocated = false;
            return false;
        }

        private readonly ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

        public bool TryFlush(PixelBufferInfo pixelBufferInfo)
        {
            try
            {
                _readerWriterLockSlim.EnterWriteLock();
                if (pixelBufferInfo.TryCopyTo(this))
                {
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Trace.Write(exception);
                return false;
            }
            finally
            {
                _readerWriterLockSlim.ExitWriteLock();
            }
        }

        public void Commit(DrawingContext context)
        {
            bool bitmapLocked = false;
            try
            {
                _readerWriterLockSlim.EnterWriteLock();
                if (!IsAllocated)
                {
                    if (_buffer == null)
                    {
                        return;
                    }

                    Allocate(_preAllocateTargetInfo);
                }

                _bitmap.Lock();
                bitmapLocked = true;
                _bitmap.AddDirtyRect(Int32Rect);
            }
            catch (Exception exception)
            {
                Trace.Write(exception);
            }
            finally
            {
                if (bitmapLocked)
                {
                    _bitmap.Unlock();
                }

                _readerWriterLockSlim.ExitWriteLock();
            }

            context.PushTransform(_transformGroup);
            context.DrawImage(_bitmap, DirtRect);
            context.Pop();
        }
    }
}