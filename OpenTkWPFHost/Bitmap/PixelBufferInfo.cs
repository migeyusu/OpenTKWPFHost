using System;
using System.Diagnostics;
using System.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTkWPFHost.Core;
using Buffer = System.Buffer;

namespace OpenTkWPFHost.Bitmap
{
    public class PixelBufferInfo : IDisposable
    {
        private PixelSize _pixelSize;

        private int _bufferSize;

        private volatile int _glBufferPointer;

        public bool HasBuffer
        {
            get { return _hasBuffer; }
        }

        public int BufferSize => _bufferSize;

        public PixelSize PixelSize => _pixelSize;

        public RenderTargetInfo RenderTarget => _renderTarget;

        private volatile bool _hasBuffer;

        /// <summary>
        /// barrier
        /// </summary>
        private volatile IntPtr _fence;

        private IntPtr _mapBufferIntPtr;

        private readonly ReaderWriterLockSlim _lockSlim = new ReaderWriterLockSlim();

        private const BufferAccessMask AccessMask = BufferAccessMask.MapWriteBit | BufferAccessMask.MapCoherentBit |
                                                    BufferAccessMask.MapPersistentBit;

        private const BufferStorageFlags StorageFlags = BufferStorageFlags.MapWriteBit |
                                                        BufferStorageFlags.MapPersistentBit |
                                                        BufferStorageFlags.MapCoherentBit;


        private RenderTargetInfo _renderTarget = new RenderTargetInfo(0, 0, 1, 1);

        public void AddFence(RenderTargetInfo renderTarget)
        {
            try
            {
                _lockSlim.EnterWriteLock();
                if (!renderTarget.Equals(_renderTarget))
                {
                    ReleaseInternal();
                    Allocate(renderTarget);
                }

                GL.BindBuffer(BufferTarget.PixelPackBuffer, this._glBufferPointer);
                GL.ReadPixels(0, 0, _pixelSize.Width, _pixelSize.Height, PixelFormat.Bgra, PixelType.UnsignedByte,
                    IntPtr.Zero);
                this._fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
                GL.Finish();
                this._hasBuffer = true;
            }
            catch (Exception)
            {
                Debugger.Break();
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        private void Allocate(RenderTargetInfo renderTarget)
        {
            var pixelSize = renderTarget.PixelSize;
            var pixelBufferSize = renderTarget.BufferSize;
            var writeBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelPackBuffer, writeBuffer);
            GL.BufferStorage(BufferTarget.PixelPackBuffer, pixelBufferSize, IntPtr.Zero, StorageFlags);
            var mapBufferRange = GL.MapNamedBufferRange(writeBuffer, IntPtr.Zero,
                pixelBufferSize, AccessMask);
            this._bufferSize = pixelBufferSize;
            this._glBufferPointer = writeBuffer;
            this._renderTarget = renderTarget;
            this._mapBufferIntPtr = mapBufferRange;
            this._pixelSize = pixelSize;
        }

        public bool WaitFence()
        {
            try
            {
                _lockSlim.EnterWriteLock();
                var fence = _fence;
                if (this._hasBuffer)
                {
                    var clientWaitSync = GL.ClientWaitSync(fence, ClientWaitSyncFlags.SyncFlushCommandsBit, 0);
                    if (clientWaitSync == WaitSyncStatus.AlreadySignaled ||
                        clientWaitSync == WaitSyncStatus.ConditionSatisfied)
                    {
                        this._fence = IntPtr.Zero;
                        GL.DeleteSync(fence);
                        return true;
                    }
/*#if DEBUG
                    GL.GetSync(fence, SyncParameterName.SyncStatus, 1, out int length, out int status);
                    if (status == (int)GLSignalStatus.UnSignaled)
                    {
                        var errorCode = GL.GetError();
                        Debug.WriteLine(errorCode.ToString());
                    }

                    Debug.WriteLine(clientWaitSync.ToString());
#endif*/
                }

                return false;
            }
            catch (Exception exception)
            {
                Debugger.Break();
                return false;
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public bool TryCopyTo(BitmapCanvas canvas)
        {
            try
            {
                _lockSlim.EnterReadLock();
                if (!this._hasBuffer) return false;
                var bufferSize = this._bufferSize;
                if (canvas.TryAllocate(_renderTarget, _mapBufferIntPtr))
                {
                    unsafe
                    {
                        Buffer.MemoryCopy(this._mapBufferIntPtr.ToPointer(),
                            canvas.DisplayBuffer.ToPointer(),
                            bufferSize, bufferSize);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Debugger.Break();
                return false;
            }
            finally
            {
                this._hasBuffer = false;
                _lockSlim.ExitReadLock();
            }
        }

        private void ReleaseInternal()
        {
            try
            {
                var intPtr = this._fence;
                if (intPtr.Equals(IntPtr.Zero))
                {
                    GL.DeleteSync(intPtr);
                }

                var writeBuffer = this._glBufferPointer;
                if (writeBuffer != 0)
                {
                    GL.UnmapNamedBuffer(writeBuffer);
                    GL.DeleteBuffer(writeBuffer); //todo: release是否要删除fence?
                }
            }
            finally
            {
                this._hasBuffer = false;
            }
        }

        public void Release()
        {
            try
            {
                _lockSlim.EnterWriteLock();
                ReleaseInternal();
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _lockSlim.Dispose();
        }
    }
}