using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkControlExample
{
    public class TestRenderer : IRenderer
    {
        private readonly float[] _vertices =
        {
            -0.5f, -0.5f, 0.0f, // Bottom-left vertex
            0.5f, -0.5f, 0.0f, // Bottom-right vertex
            0.0f, 0.5f, 0.0f // Top vertex
        };

        private int _vertexBufferObject;

        private int _vertexArrayObject;

        private Shader _shader;

        public bool IsInitialized { get; private set; }

        public void Initialize(IGraphicsContext context)
        {
            this.IsInitialized = true;
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices,
                BufferUsageHint.StaticDraw);
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            _shader = new Shader("Shaders/shader.vert", "Shaders/shader.frag");
            _shader.Use();
        }

        public bool PreviewRender()
        {
            return true;
        }

        public void Render(GlRenderEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            _shader.Use();

            GL.BindVertexArray(_vertexArrayObject);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Resize(PixelSize size)
        {
            GL.Viewport(0, 0, size.Width, size.Height);
        }

        public void Uninitialize()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            GL.DeleteBuffer(_vertexBufferObject);
            GL.DeleteVertexArray(_vertexArrayObject);

            GL.DeleteProgram((int)_shader.Handle);
            IsInitialized = false;
        }
    }
}