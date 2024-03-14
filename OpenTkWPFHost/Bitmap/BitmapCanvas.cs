using System;
using System.Diagnostics.CodeAnalysis;
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

        private Rect _dirtRect;

        private Int32Rect _int32Rect;

        private RenderTargetInfo _targetInfo;

        private TransformGroup _transformGroup;

        public bool IsAllocated { get; set; } = false;

        private void Allocate()
        {
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(new ScaleTransform(1, -1));
            _transformGroup.Children.Add(new TranslateTransform(0, _targetInfo.ActualHeight));
            _transformGroup.Freeze();
            _bitmap = new WriteableBitmap(_targetInfo.PixelWidth, _targetInfo.PixelHeight, _targetInfo.DpiX,
                _targetInfo.DpiY, PixelFormats.Pbgra32, null);
            _dirtRect = _targetInfo.Rect;
            _int32Rect = _targetInfo.Int32Rect;
            _displayBuffer = _bitmap.BackBuffer;
            IsAllocated = true;
        }

        private readonly ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

        public bool TryFlush(RenderTargetInfo renderTargetInfo, PixelBufferInfo pixelBufferInfo)
        {
            try
            {
                _readerWriterLockSlim.EnterWriteLock();
                /*
                if (!IsAllocated)
                {
                    _targetInfo = renderTargetInfo;
                    return true;
                }
                */

                if (!Equals(renderTargetInfo, this._targetInfo))
                {
                    _targetInfo = renderTargetInfo;
                    IsAllocated = false;
                    return true;
                }

                if (pixelBufferInfo.CopyTo(this._displayBuffer))
                {
                    return true;
                }

                IsAllocated = false;
                return false;
            }
            finally
            {
                _readerWriterLockSlim.ExitWriteLock();
            }
        }


        public bool Commit(DrawingContext context, PixelBufferInfo bufferInfo)
        {
            bool bitmapLocked = false;
            try
            {
                _readerWriterLockSlim.EnterWriteLock();
                if (!IsAllocated)
                {
                    Allocate();
                    if (!bufferInfo.CopyTo(this._displayBuffer))
                    {
                        return false;
                    }
                }

                _bitmap.Lock();
                bitmapLocked = true;
                _bitmap.AddDirtyRect(_int32Rect);
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
            context.DrawImage(_bitmap, _dirtRect);
            context.Pop();
            return true;
        }
    }
}