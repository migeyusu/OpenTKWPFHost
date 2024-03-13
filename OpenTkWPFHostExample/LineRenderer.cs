using System.Collections.Generic;
using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace OpenTkControlExample
{
    public class LineRenderer
    {
        public int PointCount { get; }

        public Color4 LineColor { get; set; }

        public const int LineWidth = 1;

        private Shader _shader;

        protected int ShaderStorageBufferObject;

        protected int VertexBufferSize { get; }

        protected int VertexArrayObject;

        /// <summary>
        /// demo 不演示循环缓冲
        /// </summary>
        protected float[] RingBuffer { get; set; }

        public LineRenderer(int pointCount)
        {
            this.PointCount = pointCount;
            this.VertexBufferSize = pointCount * 2 + 2;
            this.RingBuffer = new float[this.VertexBufferSize];
        }

        public virtual void Initialize(Shader shader)
        {
            unsafe
            {
                this._shader = shader;
                ShaderStorageBufferObject = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ShaderStorageBufferObject);
                GL.BufferData<float>(BufferTarget.ShaderStorageBuffer, VertexBufferSize * sizeof(float), RingBuffer,
                    BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, ShaderStorageBufferObject);
                VertexArrayObject = GL.GenVertexArray();
                GL.BindVertexArray(VertexArrayObject);
            }
        }

        public virtual void OnRenderFrame(LineRenderArgs args)
        {
            _shader.SetColor("linecolor", LineColor);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ShaderStorageBufferObject);
            GL.BindVertexArray(VertexArrayObject);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6 * PointCount);
            /*var LineCount = 1;
            int[] indirect = new int[LineCount * 4];
            for (int i = 0; i < LineCount; i++)
            {
                indirect[0 + i * 4] = PointCount;
                indirect[1 + i * 4] = 1;
                indirect[2 + i * 4] = PointCount * i;
                indirect[3 + i * 4] = i;
            }*/

            // GL.MultiDrawArraysIndirect(PrimitiveType.LineStrip, indirect, LineCount, 0);
            GL.BindVertexArray(0);
        }

        public void AddPoints(IList<PointF> points)
        {
            var j = 0;
            RingBuffer[j] = 0;
            j++;
            RingBuffer[j] = 0;
            j++;
            foreach (var pointF in points)
            {
                RingBuffer[j] = pointF.X;
                j++;
                RingBuffer[j] = pointF.Y;
                j++;
            }
        }

        public void AddPoint(PointF point)
        {
            /*RingBuffer[0] = point.X;
            RingBuffer[1] = point.Y;*/
        }

        public void Append()
        {
        }

        public void Dispose()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            GL.BindVertexArray(0);
            GL.DeleteBuffer(ShaderStorageBufferObject);
        }
    }
}