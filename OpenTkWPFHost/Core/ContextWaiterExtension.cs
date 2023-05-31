using System.Threading.Tasks;
using OpenTK.Graphics;
using OpenTK.Platform;
using OpenTK.Windowing.Common;

namespace OpenTkWPFHost.Core
{
    public static class ContextWaiterExtension
    {
        public static GraphicContextAwaiter Wait(this TaskCompletionEvent contextWaiter, GLContextWrapper glBinding)
        {
            contextWaiter.ResetTask();
            return new GraphicContextAwaiter(contextWaiter.CompletionSource.Task, glBinding);
        }

        public static GraphicContextAwaiter Delay(this GLContextWrapper binding, int millisecondsDelay)
        {
            return new GraphicContextAwaiter(Task.Delay(millisecondsDelay), binding);
        }
    }
}