/* Textured version of the Plane. Converts the given image to a gpu texture and draws it using a shader.
 * The Image is updated via the thread safe SetImage() method. RefreshAndTransferTexture() must be called
 * on each draw loop to transfer the image data to the texture in the gpu's memory. The texture needs to be
 * transfered to the gpu before the rendering phase to prevent a race condition.
 */

using System;
using System.IO;
using System.Xml.Serialization;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Toolkit.Graphics;
using Texture2D = SharpDX.Toolkit.Graphics.Texture2D;

namespace StereoScopica
{
    public class TexturedPlane: Plane
    {
        // Saved (serialized) property values
        // Toggle to mirror the texture. The plane's texture shader does the job (better performance)
        public bool IsImageMirrored { get; set; }

        // Set to adjust the brightness multiplier. The plane's texture shader does the job (better performance)
        public float Brightness
        {
            get { return _brightness; }
            set {
                _brightness = value < 0f ? 0f : value;
            }
        }
        // Image plane position so that the planes' stereo parallax can be adjusted
        public Matrix Transformation { get; set; }

        // Non-serialized public properties
        // Texture update FPS 
        [XmlIgnore]
        public FPS FPS { get; private set; }
        // The plane's image that will be copied to the texture
        [XmlIgnore]
        public Image Image { get; private set; }
        // The plane's texture on the gpu
        [XmlIgnore]
        public Texture2D Texture { get; private set; }
        // Shader instance
        [XmlIgnore]
        public TextureShader TextureShader { get; set; }

        // Internal variables
        private float _brightness;
        // Thread lock used when updating the image and texture properties
        private readonly object _textureLock = new object();
        // Flag to update the texture from the image property
        private bool _imageUpdated;

        private bool _disposed;

        public TexturedPlane()
        {
            FPS = new FPS();
            Transformation = Matrix.Translation(0f, 0f, 0f);
            _brightness = 1.0f;
            IsImageMirrored = false;
        }

        // SetImage() updates the Image property and flags the image as updated (thread safe).
        public void SetImage(Stream img)
        {
            if (img == null || img.Length == 0)
                return;
            try
            {
                lock (_textureLock)
                {
                    if (Image != null)
                        Image.Dispose();
                    Image = Image.Load(img);
                    _imageUpdated = true;
                }
            }
            catch (ArgumentException)
            {
                throw new Exception("Image read/conversion error");
            }
        }

        // RefreshAndTransferTexture() updates the texture on the gpu
        // Call this before the texture is used at the rendering stage or else a race condition will occur
        // If the image dimension change the texture must be recreated or else the directx update call will fail when the image is being transfered to the gpu
        public void RefreshAndTransferTexture(GraphicsDevice gd)
        {
            // Lock for thread safety (used in setimage())
            lock (_textureLock)
            {
                if (_imageUpdated && Image != null)
                {
                    // Destroy the existing texture if the new image does not fit the current texture description
                    if (Texture != null && (Texture.Description.Width != Image.Description.Width
                         || Texture.Description.Height != Image.Description.Height
                         || Texture.Description.ArraySize != Image.Description.ArraySize
                         || Texture.Description.Depth != Image.Description.Depth
                         || Texture.Description.Dimension != Image.Description.Dimension
                         || Texture.Description.Format != Image.Description.Format
                         || Texture.Description.MipLevels != Image.Description.MipLevels))
                    {
                        Texture.Dispose();
                        Texture = null;
                    }
                    // (Re)create the texture context as a dynamic texture (cpu update possible) and store the image dimensions
                    if (Texture == null)
                    {
                        Texture = Texture2D.New(gd, Image, TextureFlags.ShaderResource, ResourceUsage.Dynamic);
                        // Update the image plane size to match the image's aspect ratio
                        UpdatePlaneAspectRatio();
                    }
                    else
                        // Update the texture
                        Texture.SetData(Image);

                    _imageUpdated = false;
                    FPS.Tick();
                }
            }
        }

        // Update the plane aspect ratio. The aspect ratio is calculated from the image dimensions
        // The 1:1 aspect ratio will give the biggest plane, from (-1,-1) to (1,1)
        public virtual void UpdatePlaneAspectRatio()
        {
            var width = (float)Image.Description.Width;
            var height = (float)Image.Description.Height;
            var x = (width > height) ? 1.0f : height / width;
            var y = (width < height) ? 1.0f : height / width;
            // Get the base class vertices (cloned)
            var vertices = Vertices;
            if (vertices == null)
                throw new Exception("Plane is not initialized.");

            // Update the x & y coordinates and preserve the sign
            for (var i = 0; i < IndexCount; i++)
            {
                vertices[i].position.X = x * Math.Sign(vertices[i].position.X);
                vertices[i].position.Y = y * Math.Sign(vertices[i].position.Y);
            }
            // Update the vertices
            Vertices = vertices;
        }

        public void Render(DeviceContext deviceContext, Matrix world, Matrix projection, float flags, Vector2 brightnessCoeffs)
        {
            // Send plane vertices to the GPU
            base.Render(deviceContext);

            // Render the image onto the plane if we have a shader
            if (TextureShader != null)
                TextureShader.Render(deviceContext, IndexCount, Transformation, world, projection, flags, brightnessCoeffs); 
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (Image != null) Image.Dispose();
                if (Texture != null) Texture.Dispose();
                //if (TextureShader != null) TextureShader.Dispose();
                FPS = null;
            }
            _disposed = true;
            base.Dispose(disposing);
        }

    }
}
