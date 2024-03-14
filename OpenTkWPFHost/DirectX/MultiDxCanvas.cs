using OpenTkWPFHost.Core;

namespace OpenTkWPFHost.DirectX
{
    /// <summary>
    /// 由于d3dimage的机制是设置背缓冲后响应Dispatcher的提交。
    /// 如果背缓冲未提交会阻塞锁定操作，增加cpu占用，故使用双缓冲
    /// </summary>
    public class MultiDxCanvas : GenericMultiBuffer<DxCanvas>
    {
        public MultiDxCanvas(int bufferCount = 3) : base(bufferCount, ((i, canvas) => new DxCanvas()))
        {
        }

        public bool CanAsyncFlush { get; } = true;

        public bool Ready => this.GetBackBuffer().Ready;

        public DxCanvasArgs Flush(DXFrameArgs frame)
        {
            //todo
            return new DxCanvasArgs(frame.RenderTargetIntPtr, this.GetBackBuffer(), frame.TargetInfo,null);
        }
    }
}