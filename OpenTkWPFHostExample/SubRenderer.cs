using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTkWPFHost.Abstraction;
using OpenTkWPFHost.Core;

namespace OpenTkControlExample
{
    public class SubRenderer : IRenderer
    {
        private TendencyChartRenderer _renderer;

        public SubRenderer(TendencyChartRenderer renderer)
        {
            this._renderer = renderer;
        }

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

        public bool IsInitialized => true;

        public void Initialize(IGraphicsContext context)
        {
        }

        public bool PreviewRender()
        {
            return _renderer.IsInitialized;
        }

        private bool _matrixChanged = false;

        private Matrix4 _lastTransformMatrix;

        private Matrix4 _renderingMatrix;

        private readonly int[] _yAxisRaster = new int[300];

        private IReadOnlyCollection<LineRenderer> LineRenderers => _renderer.LineRenderers;

        public void Render(GlRenderEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(BackgroundColor);
            if (LineRenderers.Count == 0)
            {
                return;
            }

            var transformMatrix = TendencyChartRenderer.CalculateTransformMatrix(_currentScrollRange, DefineYAxisValue);
            if (!Equals(transformMatrix, _lastTransformMatrix))
            {
                _lastTransformMatrix = transformMatrix;
                _renderingMatrix = transformMatrix;
                _matrixChanged = true;
            }

            var shader = _renderer.Shader;
            shader.SetMatrix4("transform", _renderingMatrix);
            shader.SetFloat("u_thickness", 2);
            shader.SetVec2("u_resolution", new Vector2(args.Width, args.Height));
            var yAxisSsbo = _renderer.YAxisSsbo;
            if (AutoYAxisApex && _matrixChanged)
            {
                var empty = new int[300];
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, yAxisSsbo);
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, empty.Length * sizeof(int), empty);
            }

            var renderArgs = new LineRenderArgs() { PixelSize = args.PixelSize, LineThickness = 2 };
            foreach (var lineRenderer in LineRenderers)
            {
                lineRenderer.OnRenderFrame(renderArgs);
            }

            if (AutoYAxisApex && _matrixChanged)
            {
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, yAxisSsbo);
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
                    this._renderingMatrix =
                        TendencyChartRenderer.CalculateTransformMatrix(this._currentScrollRange, adjustYAxisValue);
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
        }
    }
}