using System;
using System.Collections;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D11;
using SharpOVR;

namespace StereoScopica
{
    // These namespaces need to be here to override some classes in SharpDX.Direct3D11
    using SharpDX.Toolkit;
    using SharpDX.Toolkit.Graphics;
    using SharpDX.DXGI;

    public class Renderer : IDisposable
    {
        // Current eye pose provided by the HMD, updated on each draw loop
        public PoseF[] EyePose { get; private set; }
        // IPD values from the HMD profile
        public Vector3[] EyeOffset { get; private set; }
        // Eye viewport
        public Rect[] EyeViewport { get; private set; }
        // FPS counter
        public FPS FPS { get; private set; }
        // Oculus Rift via SharpOVR
        protected HMD HMD { get; private set; }
        // Combined render target for both eyes (one render target texture but the eyes have separate viewports)
        protected RenderTarget2D RenderTarget { get; private set; }
        protected DepthStencilBuffer DepthStencilBuffer { get; private set; }
        // Eye data arrays for the HMD's methods
        protected EyeRenderDesc[] EyeRenderDesc { get; private set; }
        // The result will be drawn on these textures, one for each eye
        protected D3D11TextureData[] EyeTextureData { get; private set; }
        private bool _disposed;

        public Renderer(GraphicsDeviceManager graphicsDeviceManager)
        {
            // Initialize the SharpOVR library
            OVR.Initialize();

            // Create our HMD or if not present, a dummy one
            HMD = OVR.HmdCreate(0) ?? OVR.HmdCreateDebug(HMDType.DK2);

            // Match back buffer size with HMD resolution
            graphicsDeviceManager.PreferredBackBufferWidth = HMD.Resolution.Width;
            graphicsDeviceManager.PreferredBackBufferHeight = HMD.Resolution.Height;
            EyePose = new PoseF[2];
            EyeOffset = new Vector3[2];
            EyeViewport = new Rect[2];
            FPS = new FPS();
        }

        public void Initialize(GraphicsDevice graphicsDevice, IntPtr windowHandle)
        {
            InitializeHMD(graphicsDevice, windowHandle);
            InitializeRendering(graphicsDevice);
        }

        protected void InitializeHMD(GraphicsDevice graphicsDevice, IntPtr windowHandle)
        {
            // Attach the HMD to window to direct back buffer output from the window to the HMD
            HMD.AttachToWindow(windowHandle);

            // Configure DirectX 11
            var device = (SharpDX.Direct3D11.Device)graphicsDevice;
            var d3D11Cfg = new D3D11ConfigData
            {
                Header =
                {
                    API = RenderAPIType.D3D11,
                    BackBufferSize = HMD.Resolution,
                    Multisample = 1
                },
                pDevice = device.NativePointer,
                pDeviceContext = device.ImmediateContext.NativePointer,
                pBackBufferRT = ((RenderTargetView) graphicsDevice.BackBuffer).NativePointer,
                pSwapChain = ((SwapChain) graphicsDevice.Presenter.NativePresenter).NativePointer
            };

            // Configure the HMD's rendering settings
            EyeRenderDesc = new EyeRenderDesc[2];
            if (!HMD.ConfigureRendering(d3D11Cfg, DistortionCapabilities.Chromatic | DistortionCapabilities.TimeWarp, HMD.DefaultEyeFov, EyeRenderDesc))
                throw new Exception("Failed to configure HMD");

            // IPD
            EyeOffset[0] = EyeRenderDesc[0].HmdToEyeViewOffset;
            EyeOffset[1] = EyeRenderDesc[1].HmdToEyeViewOffset;

            // Enable low persistence and dynamic prediction features
            HMD.EnabledCaps = HMDCapabilities.LowPersistence | HMDCapabilities.DynamicPrediction;
            // Enable all DK2 tracking features
            HMD.ConfigureTracking(TrackingCapabilities.Orientation | TrackingCapabilities.Position | TrackingCapabilities.MagYawCorrection, TrackingCapabilities.None);
            // Dismiss the Heatlh and Safety Window
            HMD.DismissHSWDisplay();

            // Get HMD output display
            var adapter = (Adapter)graphicsDevice.Adapter;
            var hmdOutput = adapter.Outputs.FirstOrDefault(o => HMD.DeviceName.StartsWith(o.Description.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (hmdOutput != null)
            {
                // Set game to fullscreen on rift
                var swapChain = (SwapChain)graphicsDevice.Presenter.NativePresenter;
                var description = swapChain.Description.ModeDescription;
                swapChain.ResizeTarget(ref description);
                swapChain.SetFullscreenState(true, hmdOutput);
            }
        }

        protected void InitializeRendering(GraphicsDevice graphicsDevice)
        {
            // Get our initial render target size
            // The scaling factor doesn't seem to affect the image quality. There is no golden value for this as stated in the SDK documentation
            var renderTargetSize = HMD.GetDefaultRenderTargetSize(1.5f);

            // Create the render target texture that is shown on the HMD
            RenderTarget = RenderTarget2D.New(graphicsDevice, renderTargetSize.Width, renderTargetSize.Height, new MipMapCount(1), PixelFormat.R8G8B8A8.UNorm);

            // The created texture can have a different size due to hardware limitations so the render target size needs to be updated
            renderTargetSize.Width = RenderTarget.Width;
            renderTargetSize.Height = RenderTarget.Height;

            DepthStencilBuffer = DepthStencilBuffer.New(graphicsDevice, renderTargetSize.Width, renderTargetSize.Height, DepthFormat.Depth32, true);

            // Compute the virtual cameras' viewports
            EyeViewport[0] = new Rect(0, 0, renderTargetSize.Width / 2, renderTargetSize.Height);
            EyeViewport[1] = new Rect((renderTargetSize.Width + 1) / 2, 0, EyeViewport[0].Width, EyeViewport[0].Height);

            // Create HMD's eye texture data 
            EyeTextureData = new D3D11TextureData[2];
            EyeTextureData[0].Header.API = RenderAPIType.D3D11;
            EyeTextureData[0].Header.TextureSize = renderTargetSize;
            EyeTextureData[0].Header.RenderViewport = EyeViewport[0];
            EyeTextureData[0].pTexture = ((SharpDX.Direct3D11.Texture2D)RenderTarget).NativePointer;
            EyeTextureData[0].pSRView = ((ShaderResourceView)RenderTarget).NativePointer;
            // Right eye holds the same values except for the viewport
            EyeTextureData[1] = EyeTextureData[0];
            EyeTextureData[1].Header.RenderViewport = EyeViewport[1];
        }

        public void BeginFrame(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.SetRenderTargets(DepthStencilBuffer, RenderTarget);
            // Begin oculus rift drawing mode
            HMD.BeginFrame(0);
            // Update the eye's pose from sensor data
            HMD.GetEyePoses(0, EyeOffset, EyePose);
        }

        public void EndFrame()
        {
            HMD.EndFrame(EyePose, EyeTextureData);
            FPS.Tick();
        }

        // Get the optimal rendering order for the eyes (indexes can swap)
        public IEnumerable GetEyeIndex()
        {
            return HMD.EyeRenderOrder.Cast<int>();
        }

        public Matrix GetEyeProjection(int eyeIndex, float zNear, float zFar)
        {
            var projection = OVR.MatrixProjection(EyeRenderDesc[eyeIndex].Fov, zNear, zFar, true);
            projection.Transpose();
            return projection;
        }

        public void RecenterPose()
        {
            HMD.RecenterPose();
        }

        public void ToggleDrawToWindow()
        {
            HMD.EnabledCaps = HMD.EnabledCaps ^ HMDCapabilities.NoMirrorToWindow;
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
                if (DepthStencilBuffer != null)
                    DepthStencilBuffer.Dispose();
                if (RenderTarget != null)
                    RenderTarget.Dispose();
                if (HMD != null)
                    HMD.Dispose();
                OVR.Shutdown();
            }
            _disposed = true;
        }

        ~Renderer()
        {
            Dispose(false);
        }
    }
}
