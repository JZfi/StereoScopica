/*
 * This class is based on the TextureShader class from the SharpDX example / Rastertek tutorial 5
 * Modifications:
 * - A new structure for shader settings (for flags and other values)
 * - Implements IDisposable instead of ICloneable
 * - Removed unnecessary internal methods
 * - Throws more exceptions now
 */

using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace StereoScopica
{
	public class TextureShader : IDisposable
	{
        private bool _disposed;
		[StructLayout(LayoutKind.Sequential)]
		internal struct Vertex
		{
			public static int AppendAlignedElement = 12;
			public Vector3 position;
			public Vector2 texture;
		}

        [StructLayout(LayoutKind.Explicit, Size = 192)]
        internal struct MatrixBuffer
        {
            [FieldOffset(0)]
            public Matrix world;
            [FieldOffset(64)]
            public Matrix view;
            [FieldOffset(128)]
            public Matrix projection;
        }
        // buffer size must be a multiplier of 16
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        internal struct SettingsBuffer
        {
            [FieldOffset(0)]
            public float flags;
            [FieldOffset(4)]
            public float unused;
            [FieldOffset(8)]
            public Vector2 brightnessCoeffs;
        }

		VertexShader VertexShader { get; set; }
		PixelShader PixelShader { get; set; }
		InputLayout Layout { get; set; }
		Buffer ConstantMatrixBuffer { get; set; }
		SamplerState SampleState { get; set; }
        Buffer ConstantSettingsBuffer { get; set; }

	    private bool _initialized;

	    public void Initialize(Device device, byte[] vertexShaderByteCode, byte[] pixelShaderByteCode)
        {
            try
            {
                // Create the vertex shader from the buffer.
                VertexShader = new VertexShader(device, vertexShaderByteCode);
                // Create the pixel shader from the buffer.
                PixelShader = new PixelShader(device, pixelShaderByteCode);
                
                // Now setup the layout of the data that goes into the shader.
                // This setup needs to match the VertexType structure in the Model and in the shader.
                var inputElements = new[]
				{
					new InputElement()
					{
						SemanticName = "POSITION",
						SemanticIndex = 0,
						Format = Format.R32G32B32_Float,
						Slot = 0,
						AlignedByteOffset = 0,
						Classification = InputClassification.PerVertexData,
						InstanceDataStepRate = 0
					},
					new InputElement()
					{
						SemanticName = "TEXCOORD",
						SemanticIndex = 0,
						Format = Format.R32G32_Float,
						Slot = 0,
						AlignedByteOffset = Vertex.AppendAlignedElement,
						Classification = InputClassification.PerVertexData,
						InstanceDataStepRate = 0
					}
				};

                // Create the vertex input the layout.
                Layout = new InputLayout(device, vertexShaderByteCode, inputElements);

                // Setup the description of the dynamic matrix constant buffer that is in the vertex shader.
                var matrixBufferDesc = new BufferDescription()
                {
                    Usage = ResourceUsage.Dynamic,
                    SizeInBytes = Utilities.SizeOf<MatrixBuffer>(),
                    BindFlags = BindFlags.ConstantBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    OptionFlags = ResourceOptionFlags.None,
                    StructureByteStride = 0
                };

                // Create the constant buffer pointer so we can access the vertex shader constant buffer from within this class.
                ConstantMatrixBuffer = new Buffer(device, matrixBufferDesc);

                // Create a texture sampler state description.
                var samplerDesc = new SamplerStateDescription()
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Wrap,
                    AddressV = TextureAddressMode.Wrap,
                    AddressW = TextureAddressMode.Wrap,
                    MipLodBias = 0,
                    MaximumAnisotropy = 1,
                    ComparisonFunction = Comparison.Always,
                    BorderColor = new Color4(0, 0, 0, 0),
                    MinimumLod = 0,
                    MaximumLod = 0
                };

                // Create the texture sampler state.
                SampleState = new SamplerState(device, samplerDesc);

                // Setup the description of the settings constant buffer that is in the pixel shader.
                var settingsBufferDesc = new BufferDescription()
                {
                    Usage = ResourceUsage.Dynamic,
                    SizeInBytes = Utilities.SizeOf<SettingsBuffer>(),
                    BindFlags = BindFlags.ConstantBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    OptionFlags = ResourceOptionFlags.None,
                    StructureByteStride = 0
                };

                // Create the constant buffer pointer so we can access the pixel shader constant buffer from within this class.
                ConstantSettingsBuffer = new Buffer(device, settingsBufferDesc);

                _initialized = true;
            }
            catch (Exception ex)
            {
                throw new Exception("Could not initialize the texture shader: " + ex.Message);
            }
        }

        public void Render(DeviceContext deviceContext, int indexCount, Matrix world, Matrix view, Matrix projection, float flags, Vector2 brightnessCoeffs)
        {
            if (!_initialized)
                throw new Exception("The shader has not been initialized.");

            // Set the shader parameters that it will use for rendering.
            SetShaderParameters(deviceContext, world, view, projection, flags, brightnessCoeffs);

            // Now render the prepared buffers with the shader.
            RenderShader(deviceContext, indexCount);
        }

		private void RenderShader(DeviceContext deviceContext, int indexCount)
		{
			// Set the vertex input layout.
			deviceContext.InputAssembler.InputLayout = Layout;

			// Set the vertex and pixel shaders that will be used to render this triangle.
			deviceContext.VertexShader.Set(VertexShader);
			deviceContext.PixelShader.Set(PixelShader);

			// Set the sampler state in the pixel shader.
			deviceContext.PixelShader.SetSampler(0, SampleState);

			// Render the triangle.
			deviceContext.DrawIndexed(indexCount, 0, 0);
		}

        private void SetShaderParameters(DeviceContext deviceContext, Matrix world, Matrix view, Matrix projection, float flags, Vector2 brightnessCoeffs)
		{
			try
			{
				// Transpose the matrices to prepare them for shader.
				world.Transpose();
				view.Transpose();
				projection.Transpose();
                
				// Lock the constant buffer so it can be written to.
				DataStream mappedResource;
				deviceContext.MapSubresource(ConstantMatrixBuffer, MapMode.WriteDiscard, MapFlags.None, out mappedResource);

				// Copy the matrices into the constant buffer.
				var matrixBuffer = new MatrixBuffer()
				{
					world = world,
					view = view,
					projection = projection
				};

				mappedResource.Write(matrixBuffer);

				// Unlock the constant buffer.
				deviceContext.UnmapSubresource(ConstantMatrixBuffer, 0);

				// Set the position of the constant buffer in the vertex shader.
				var bufferNumber = 0;

				// Finally set the constant buffer in the vertex shader with the updated values.
				deviceContext.VertexShader.SetConstantBuffer(bufferNumber, ConstantMatrixBuffer);

                // Lock the settings constant buffer so it can be written to.
                deviceContext.MapSubresource(ConstantSettingsBuffer, MapMode.WriteDiscard, MapFlags.None, out mappedResource);

                // Copy the values into the settings buffer.
                var settingsBuffer = new SettingsBuffer()
                {
                    flags = flags,
                    brightnessCoeffs = brightnessCoeffs
                };
                mappedResource.Write(settingsBuffer);

                // Unlock the constant buffer
                deviceContext.UnmapSubresource(ConstantSettingsBuffer, 0);

                // Set the position of the constant buffer in the pixel shader.
                bufferNumber = 0;

                // Finally set the constant buffer in the pixel shader with the updated values.
                deviceContext.PixelShader.SetConstantBuffer(bufferNumber, ConstantSettingsBuffer);
			}
			catch (Exception ex)
			{
                throw new Exception("Could not set shader parameters: " + ex.Message);
			}
		}

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (ConstantSettingsBuffer != null)
                        ConstantSettingsBuffer.Dispose();
			        if (SampleState != null)
				        SampleState.Dispose();
			        if (ConstantMatrixBuffer != null)
				        ConstantMatrixBuffer.Dispose();
			        if (Layout != null)
				        Layout.Dispose();
			        if (PixelShader != null)
				        PixelShader.Dispose();
			        if (VertexShader != null)
				        VertexShader.Dispose();
                }
                _disposed = true;
            }
        }
        ~TextureShader()
        {
            Dispose(false);
        }
	}
}
