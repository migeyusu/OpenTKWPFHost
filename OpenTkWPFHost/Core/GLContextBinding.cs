using System;
using JetBrains.Annotations;
using OpenTK.Graphics;
using OpenTK.Platform;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace OpenTkWPFHost.Core
{
    public class GLContextBinding : IDisposable
    {
        private readonly NativeWindow _nativeWindow;

        public GLContextBinding(NativeWindow nativeWindow)
        {
            _nativeWindow = nativeWindow;
            Context = nativeWindow.Context;
        }

        public IGraphicsContext Context { get; }

        public void BindCurrentThread()
        {
            if (!Context.IsCurrent)
            {
                Context.MakeCurrent();
            }
        }

        public void BindNull()
        {
            Context.MakeNoneCurrent();
        }

        public void Dispose()
        {
            _nativeWindow?.Dispose();
        }
    }
}