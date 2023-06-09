using System;
using System.Diagnostics.CodeAnalysis;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics.Wgl;
using OpenTK.Platform.Windows;
using OpenTkWPFHost.Core;
using OpenTkWPFHost.Interop;

namespace OpenTkWPFHost.DirectX
{
    /// Class containing the DirectX Render Surface and OpenGL Framebuffer Object
    /// Instances of this class are created and deleted as required by the renderer.
    /// Note that this does not implement the full <see cref="IDisposable"/> pattern,
    /// as OpenGL resources cannot be freed from the finalizer thread.
    /// The calling class must correctly dispose of this by calling <see cref="Release"/>
    /// Prior to releasing references. 
    internal sealed class DxGLFramebuffer
    {
        private readonly PixelSize _pixelSize;

        /// The width of this buffer in pixels
        public int FramebufferWidth { get; }

        /// The height of this buffer in pixels
        public int FramebufferHeight { get; }

        /*/// The width of the element in device-independent pixels
        public int Width { get; }

        /// The height of the element in device-independent pixels
        public int Height { get; }*/

        /// The DirectX Render target (framebuffer) handle.
        public IntPtr DxRenderTargetHandle { get; }

        /// The OpenGL Framebuffer handle
        public int GLFramebufferHandle { get; }

        /// The OpenGL shared texture handle (with DX)
        private int GLSharedTextureHandle { get; }

        /// The OpenGL depth render buffer handle.
        private int GLDepthRenderBufferHandle { get; }

        /// Specific wgl_dx_interop handle that marks the framebuffer as ready for interop.
        public IntPtr DxInteropRegisteredHandle { get; }

        public PixelSize PixelSize => _pixelSize;
        
        public DxGLFramebuffer([NotNull] DxGlContext context, PixelSize pixelSize)
        {
            _pixelSize = pixelSize;
            FramebufferWidth = pixelSize.Width;
            FramebufferHeight = pixelSize.Height;
            var dxSharedHandle = IntPtr.Zero; // Unused windows-vista legacy sharing handle. Must always be null.
            DXInterop.CreateRenderTarget(
                context.DxDeviceHandle,
                FramebufferWidth,
                FramebufferHeight,
                Format.X8R8G8B8, // this is like A8 R8 G8 B8, but avoids issues with Gamma correction being applied twice.
                MultisampleType.None,
                0,
                false,
                out var dxRenderTargetHandle,
                ref dxSharedHandle);

            DxRenderTargetHandle = dxRenderTargetHandle;
            Wgl.DXSetResourceShareHandleNV(dxRenderTargetHandle, dxSharedHandle);
            GLFramebufferHandle = GL.GenFramebuffer();
            GLSharedTextureHandle = GL.GenTexture();

            var genHandle = Wgl.DXRegisterObjectNV(
                context.GlDeviceHandle,
                dxRenderTargetHandle,
                (uint) GLSharedTextureHandle,
                (uint) TextureTarget.Texture2D,
                WGL_NV_DX_interop.AccessReadWrite);
            DxInteropRegisteredHandle = genHandle;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, GLFramebufferHandle);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                GLSharedTextureHandle, 0);

            GLDepthRenderBufferHandle = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, GLDepthRenderBufferHandle);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24,
                FramebufferWidth, FramebufferHeight);
            GL.FramebufferRenderbuffer(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer,
                GLDepthRenderBufferHandle);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        
        public void Release()
        {
            GL.DeleteFramebuffer(GLFramebufferHandle);
            GL.DeleteRenderbuffer(GLDepthRenderBufferHandle);
            GL.DeleteTexture(GLSharedTextureHandle);
            // Wgl.DXUnregisterObjectNV(DxGlContext.GlDeviceHandle, DxInteropRegisteredHandle);
            DXInterop.Release(DxRenderTargetHandle);
        }
    }
}