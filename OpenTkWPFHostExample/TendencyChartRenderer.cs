using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkControlExample
{
    public struct ScrollRange
    {
        public long Start;
        public long End;

        public ScrollRange(long start, long end)
        {
            Start = start;
            End = end;
        }
    }

    public class TendencyChartRenderer : IDisposable, IRenderer
    {
        public ConcurrentBag<LineRenderer> LineRenderers { get; set; } = new ConcurrentBag<LineRenderer>();

        public Color4 BackgroundColor { get; set; } = Color4.Black;

        public int DefineYAxisValue { get; set; } = 100;

        private float _currentYAxisValue = 100;

        public float CurrentYAxisValue
        {
            get => _currentYAxisValue;
            set
            {
                if (CurrentYAxisValue.Equals(value))
                {
                    return;
                }

                _currentYAxisValue = value;
            }
        }

        private ScrollRange _currentScrollRange;

        public ScrollRange CurrentScrollRange
        {
            get => _currentScrollRange;
            set
            {
                if (_currentScrollRange.Equals(value))
                {
                    return;
                }

                _currentScrollRange = value;
            }
        }

        /// <summary>
        /// 是否自动适配Y轴顶点
        /// </summary>
        public bool AutoYAxisApex { get; set; } = true;

        public Shader Shader => _shader;

        private Shader _shader;

        public static Matrix4 CalculateTransformMatrix(ScrollRange xRange, float yAxisApex)
        {
            var transform = Matrix4.Identity;
            transform *= Matrix4.CreateScale(2f / (xRange.End - xRange.Start),
                2f / yAxisApex, 0f);
            transform *= Matrix4.CreateTranslation(-1, -1, 0);
            return transform;
        }

        public void Add(LineRenderer lineRenderer)
        {
            this.LineRenderers.Add(lineRenderer);
        }

        public void AddRange(IEnumerable<LineRenderer> lineRenderers)
        {
            foreach (var lineRenderer in lineRenderers)
            {
                this.LineRenderers.Add(lineRenderer);
            }
        }

        private int _yAxisSsbo;

        private readonly int[] _yAxisRaster = new int[300];

        public bool IsInitialized { get; private set; }

        public int YAxisSsbo => _yAxisSsbo;

        public TendencyChartRenderer()
        {
        }


        protected int VertexArrayObject;

        public void Initialize(IGraphicsContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
            _shader = new Shader("LineShader/shader.vert",
                "LineShader/shader.frag");
            _shader.Use();
            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);
            _yAxisSsbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _yAxisSsbo);
            GL.BufferData<int>(BufferTarget.ShaderStorageBuffer, _yAxisRaster.Length * sizeof(int), _yAxisRaster,
                BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _yAxisSsbo);

            foreach (var lineRenderer in LineRenderers)
            {
                lineRenderer.Initialize(this._shader);
            }
            // GL.Enable(EnableCap.Multisample);
            /*var lineFloats = new float[2];
            GL.GetFloat(GetPName.LineWidthRange, lineFloats);
            GL.LineWidth(1);*/
            // GL.Enable(EnableCap.Multisample);
            /*GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Fastest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);*/
        }

        public bool PreviewRender()
        {
            return true;
        }

        private bool _matrixChanged = false;

        private Matrix4 _lastTransformMatrix;

        private Matrix4 _renderingMatrix;

        public void Render(GlRenderEventArgs args)
        {
            GL.ClearColor(BackgroundColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            if (LineRenderers.Count == 0)
            {
                return;
            }

            var transformMatrix = CalculateTransformMatrix(_currentScrollRange, DefineYAxisValue);
            if (!Equals(transformMatrix, _lastTransformMatrix))
            {
                _lastTransformMatrix = transformMatrix;
                _renderingMatrix = transformMatrix;
                _matrixChanged = true;
            }

            GL.BindVertexArray(VertexArrayObject);
            _shader.Use();
            _shader.SetMatrix4("transform", _renderingMatrix);
            _shader.SetFloat("u_thickness", 2);
            _shader.SetVec2("u_resolution", new Vector2(args.Width, args.Height));
            if (AutoYAxisApex && _matrixChanged)
            {
                var empty = new int[300];
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _yAxisSsbo);
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, empty.Length * sizeof(int), empty);
            }

            var renderArgs = new LineRenderArgs() { PixelSize = args.PixelSize, LineThickness = 2 };
            foreach (var lineRenderer in LineRenderers)
            {
                lineRenderer.OnRenderFrame(renderArgs);
            }

            if (AutoYAxisApex && _matrixChanged)
            {
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _yAxisSsbo);
                var ptr = GL.MapBuffer(BufferTarget.ShaderStorageBuffer, BufferAccess.ReadOnly);
                Marshal.Copy(ptr, _yAxisRaster, 0, _yAxisRaster.Length);
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
                int i;
                for (i = _yAxisRaster.Length - 1; i > 0; i--)
                {
                    if (_yAxisRaster[i] == 1)
                    {
                        break;
                    }
                }

                var adjustYAxisValue = (i * 1.1f) * CurrentYAxisValue / 200f;
                if (Math.Abs(CurrentYAxisValue - adjustYAxisValue) > CurrentYAxisValue * 0.03f)
                {
                    CurrentYAxisValue = (long)adjustYAxisValue;
                    this._renderingMatrix = CalculateTransformMatrix(this._currentScrollRange, adjustYAxisValue);
                }
                else
                {
                    _matrixChanged = false;
                }
            }
        }

        public void Resize(PixelSize size)
        {
            GL.Viewport(0, 0, size.Width, size.Height);
        }

        public void Uninitialize()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            foreach (var lineRenderer in LineRenderers)
            {
                lineRenderer.Dispose();
            }

            GL.DeleteBuffer(_yAxisSsbo);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
            GL.DeleteProgram(_shader.Handle);
        }

        public void Dispose()
        {
        }
    }
}