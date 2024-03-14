using System.Windows.Media;
using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.Abstraction
{
    public class PipelineArgs
    {
        /// <summary>
        /// used to indicate render background
        /// </summary>
        public RenderTargetInfo TargetInfo { get; }

        public DrawingGroup TargetDrawing { get; }

        public PipelineArgs(RenderTargetInfo targetInfo, DrawingGroup targetDrawing)
        {
            TargetInfo = targetInfo;
            TargetDrawing = targetDrawing;
        }
    }
}