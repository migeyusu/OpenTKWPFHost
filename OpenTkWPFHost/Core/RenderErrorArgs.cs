using System;
using OpenTK.Graphics.OpenGL4;
using OpenTkWPFHost.Configuration;

namespace OpenTkWPFHost.Core
{
    public class RenderErrorArgs : EventArgs
    {
        public RenderErrorArgs(Exception exception)
        {
            Exception = exception;
        }
        
        public Exception Exception { get; }
    }
}