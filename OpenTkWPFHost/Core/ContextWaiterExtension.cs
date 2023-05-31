﻿using System.Threading.Tasks;
using OpenTK.Graphics;
using OpenTK.Platform;
using OpenTK.Windowing.Common;

namespace OpenTkWPFHost.Core
{
    public static class ContextWaiterExtension
    {
        public static GraphicContextAwaiter Wait(this TaskCompletionEvent contextWaiter, GLContextBinding glBinding)
        {
            contextWaiter.ResetTask();
            return new GraphicContextAwaiter(contextWaiter.CompletionSource.Task, glBinding);
        }

        public static GraphicContextAwaiter Delay(this GLContextBinding binding, int millisecondsDelay)
        {
            return new GraphicContextAwaiter(Task.Delay(millisecondsDelay), binding);
        }
    }
}