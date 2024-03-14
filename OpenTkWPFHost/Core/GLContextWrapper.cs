using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTkWPFHost.Configuration;

namespace OpenTkWPFHost.Core
{
    public class GLContextWrapper : IDisposable
    {
        private readonly NativeWindow _window;

        private GLSettings _glSettings;

        public GLContextWrapper(NativeWindow window, GLSettings glSettings)
        {
            _window = window;
            _glSettings = glSettings;
            this.Context = window.Context;
        }

        public IGraphicsContext Context { get; private set; }

        public virtual void Dispose()
        {
            _window.Dispose();
        }

        //todo:是否需要？
        public void SwapBuffers()
        {
            Context.SwapBuffers();
        }

        public void MakeCurrent()
        {
            if (!Context.IsCurrent)
            {
                Context.MakeCurrent();
            }
        }

        public void MakeNoneCurrent()
        {
            Context.MakeNoneCurrent();
        }

        public GLContextWrapper CreateNewContextWrapper()
        {
            return _glSettings.NewContext(this.Context);
        }
    }
}