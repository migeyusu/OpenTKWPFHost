using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Graphics.Wgl;
using OpenTK.Platform;
using OpenTK.Platform.Windows;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Configuration;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.DirectX
{
    ///Renderer that uses DX_Interop for a fast-path.
    public class DXProcedure
    {
        private DxGlContext _context;

        private DxGLFramebuffer _frameBuffer => _frameBuffers.GetBackBuffer();

        public bool EnableFlush { get; set; } = true;

        /// The OpenGL framebuffer handle.
        public int FrameBufferHandle => _frameBuffer?.GLFramebufferHandle ?? 0;

        public bool IsInitialized { get; private set; }

        private readonly GenericMultiBuffer<DxGLFramebuffer> _frameBuffers;

        public void Swap()
        {
            _frameBuffers.Swap();
        }


        public DXProcedure()
        {
            _frameBuffers = new GenericMultiBuffer<DxGLFramebuffer>(3);
        }

        /// Sets up the framebuffer, directx stuff for rendering.
        public void PreRender()
        {
            Wgl.DXLockObjectsNV(_context.GlDeviceHandle, 1, new[] { _frameBuffer.DxInteropRegisteredHandle });
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBuffer.GLFramebufferHandle);
        }

        /// Sets up the framebuffer and prepares stuff for usage in directx.
        public DXRenderArgs PostRender()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Wgl.DXUnlockObjectsNV(_context.GlDeviceHandle, 1, new[] { _frameBuffer.DxInteropRegisteredHandle });
            if (EnableFlush)
            {
                GL.Flush();
            }
            //todo
            return new DXRenderArgs(_renderTargetInfo, _frameBuffer.DxRenderTargetHandle,null);
        }

        public GLContextWrapper Initialize(GLSettings settings)
        {
            /*if (IsInitialized)
            {
                throw new NotSupportedException("Initialized already!");
            }

            _context = new DxGlContext(settings);
            IsInitialized = true;
            return null;*/
            throw new NotImplementedException();
        }

        private RenderTargetInfo _renderTargetInfo;


        public void Apply(RenderTargetInfo renderTarget)
        {
            this._renderTargetInfo = renderTarget;
            var renderTargetPixelSize = renderTarget.PixelSize;
            _frameBuffers.Instantiate((i, d) =>
            {
                d?.Release();
                return new DxGLFramebuffer(_context, renderTargetPixelSize);
            });
            _frameBuffers.Swap();
        }

        public void Dispose()
        {
            _frameBuffers.ForEach(((i, frameBuffer) => frameBuffer.Release()));
            _context?.Dispose();
        }

        public DXFrameArgs ReadFrames(DXRenderArgs args)
        {
            return new DXFrameArgs(args.RenderTargetIntPtr, args.TargetInfo,null);
        }
    }
}