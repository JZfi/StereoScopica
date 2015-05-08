/*
 * This class is based on the Model class from the SharpDX example / Rastertek tutorial 5
 * Modifications:
 * - Implements IDisposable instead of ICloneable
 * - Removed unnecessary internal methods
 * - Throws more exceptions now
 * - Vertex positions can be updated (vertex count has to stay the same)
 */
using System;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace StereoScopica
{
	public class Plane : IDisposable
	{
        private bool _disposed;
        protected Buffer VertexBuffer;
        protected Buffer IndexBuffer;
        protected int IndexCount;
        private bool _updateVertices;
        private TextureShader.Vertex[] _vertices;
        internal TextureShader.Vertex[] Vertices
        {
            get { return (TextureShader.Vertex[])_vertices.Clone(); }
            set {
                if (value.Length != IndexCount)
                    throw new Exception("The vertex count must stay the same!");
                _vertices = value;
                _updateVertices = true;
            }
        }

        public Plane()
        {
            // Create the vertex array and load it with two triangles to form a plane
            _vertices = new[]
				{
					new TextureShader.Vertex()
					{
						position = new Vector3(-1, 1, 0),
						texture = new Vector2(0, 0)
					},
					new TextureShader.Vertex()
					{
						position = new Vector3(1, 1, 0),
						texture = new Vector2(1, 0)
					},
					new TextureShader.Vertex()
					{
						position = new Vector3(-1, -1, 0),
						texture = new Vector2(0, 1)
					},
                    new TextureShader.Vertex()
					{
						position = new Vector3(-1, -1, 0),
						texture = new Vector2(0, 1)
					},
                    new TextureShader.Vertex()
					{
						position = new Vector3(1, 1, 0),
						texture = new Vector2(1, 0)
					},
                    new TextureShader.Vertex()
					{
						position = new Vector3(1, -1, 0),
						texture = new Vector2(1, 1)
					}
				};
            IndexCount = _vertices.Length;
        }

        // Initialize the vertex and index buffers that hold the geometry for the plane
		public virtual void Initialize(Device device)
		{
			try
			{
				// Create the dynamic vertex buffer description
                var vertexBufferDesc = new BufferDescription()
                {
                    Usage = ResourceUsage.Dynamic,
                    SizeInBytes = Utilities.SizeOf<TextureShader.Vertex>() * IndexCount,
                    BindFlags = BindFlags.VertexBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    OptionFlags = ResourceOptionFlags.None,
                    StructureByteStride = 0
                };
                // Create the dynamic vertex buffer
                VertexBuffer = Buffer.Create(device, _vertices, vertexBufferDesc);

                // Generate an index array for the vertices
                var indices = new int[IndexCount];
                for (var i = 0; i < IndexCount; i++)
                    indices[i] = i;

				// Create the index buffer
				IndexBuffer = Buffer.Create(device, BindFlags.IndexBuffer, indices);
			}
			catch (Exception ex)
			{
                throw new Exception("Could not initialize plane: "+ ex.Message);
			}
		}

        // UpdateVertices updates the changed vertices on the GPU
        protected virtual void UpdateVertices(DeviceContext deviceContext)
        {
            DataStream mappedResource;
            deviceContext.MapSubresource(VertexBuffer, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out mappedResource);
            mappedResource.WriteRange(_vertices);
            deviceContext.UnmapSubresource(VertexBuffer, 0);
            _updateVertices = false;
        }

        // Put the vertex and index buffers on the graphics pipeline to prepare for drawing
		public virtual void Render(DeviceContext deviceContext)
		{
            if (_updateVertices)
                UpdateVertices(deviceContext);
			// Set the vertex buffer to active in the input assembler so it can be rendered.
			deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, Utilities.SizeOf<TextureShader.Vertex>(), 0));
			// Set the index buffer to active in the input assembler so it can be rendered.
			deviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
			// Set the type of the primitive that should be rendered from this vertex buffer, in this case triangles.
			deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
		}

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (IndexBuffer != null)
                    IndexBuffer.Dispose();
                if (VertexBuffer != null)
                    VertexBuffer.Dispose();
            }
            _disposed = true;
        }

        ~Plane()
        {
            Dispose(false);
        }
	}
}