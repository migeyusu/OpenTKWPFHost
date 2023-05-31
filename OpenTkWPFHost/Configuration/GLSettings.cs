using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using OpenTK;
using OpenTK.Graphics.Wgl;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Configuration
{
    public sealed class GLSettings
    {
        /// May be null. If defined, an external context will be used, of which the caller is responsible
        /// for managing the lifetime and disposal of.
        public IGraphicsContext ContextToUse { get; set; }

        public ContextFlags GraphicsContextFlags { get; set; } = ContextFlags.Offscreen;

        public ContextProfile GraphicsProfile { get; set; } = ContextProfile.Core;

        /// May be null. If so, default bindings context will be used.
        public IBindingsContext BindingsContext { get; set; }

// =GraphicsMode.Default;
        public int MajorVersion { get; set; } = 4;

        public int MinorVersion { get; set; } = 3;

        /// If we are using an external context for the control.
        public bool IsUsingExternalContext => ContextToUse != null;

        public int MSAASamples { get; set; } = 4;

        /// Determines if two settings would result in the same context being created.
        [Pure]
        internal static bool WouldResultInSameContext([NotNull] GLSettings a, [NotNull] GLSettings b)
        {
            if (a.MajorVersion != b.MajorVersion)
            {
                return false;
            }

            if (a.MinorVersion != b.MinorVersion)
            {
                return false;
            }

            if (a.GraphicsProfile != b.GraphicsProfile)
            {
                return false;
            }

            if (a.GraphicsContextFlags != b.GraphicsContextFlags)
            {
                return false;
            }

            return true;
        }

        public GLContextWrapper NewContext(IGraphicsContext graphicsContext = null)
        {
            var nws = NativeWindowSettings.Default;
            nws.StartFocused = false;
            nws.StartVisible = false;
            nws.NumberOfSamples = this.MSAASamples;
            // if we ask GLFW for 1.0, we should get the highest level context available with full compat.
            nws.APIVersion = new Version(this.MajorVersion, this.MinorVersion);
            nws.Flags = ContextFlags.Offscreen | this.GraphicsContextFlags;
            // we have to ask for any compat in this case.
            nws.Profile = this.GraphicsProfile;
            if (graphicsContext != null)
            {
                nws.SharedContext = (IGLFWGraphicsContext)graphicsContext;
            }

            nws.WindowBorder = WindowBorder.Hidden;
            nws.WindowState = OpenTK.Windowing.Common.WindowState.Minimized;
            var glfwWindow = new NativeWindow(nws);
            var provider = this.BindingsContext ?? new GLFWBindingsContext();
            Wgl.LoadBindings(provider);
            glfwWindow.Context.MakeCurrent();
            return new GLContextWrapper(glfwWindow, this);
        }
    }
}