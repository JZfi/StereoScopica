/*
 * This program utilizes the SharpDX's Game toolkit as its base 
 * Oculus Rift integration is handled via the SharpDX compatible SharpOVR library
 * 
 */
// ReSharper disable once RedundantUsingDirective
using System.Diagnostics;
using System;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Toolkit;
using SharpDX.D3DCompiler;
// SharpOVR also extends the Matrix class. The HMD is handled in the Renderer class
using SharpOVR; 

namespace StereoScopica
{
    // Overrides keys defined in windows.forms
    using SharpDX.Toolkit.Input;

    public class StereoScopica : Game
    {
        // DX 11 & HMD handler
        protected Renderer Renderer { get; private set; }
        protected UISettings UISettings { get; private set; }
        // Image planes that show the camera images
        protected TexturedPlane[] ImagePlane { get; private set; }
        // Camera handlers (image sources for the plane)
        protected CameraHandler[] CameraHandler { get; private set; }
        // Camera settings used by the camera handlers
        protected CameraSettings[] CameraSettings { get; private set; }
        // The shader shared by both texture planes
        protected TextureShader TextureShader { get; private set; }
        protected KeyboardManager Keyboard { get; private set; }
        protected GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
        // Logfile writer
        protected FormatStreamWriter LogFile
        {
            // Opens a new file if no stream is present
            get
            {
                return _logFile ??
                       (_logFile = new FormatStreamWriter(
                               Path.Combine(
                                   String.IsNullOrEmpty(UISettings.LogFilePath)
                                       ? Application.StartupPath
                                       : UISettings.LogFilePath,
                                   String.Format("log_{0}.csv", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                               ), CultureInfo.InvariantCulture));
            }
            // Setting the stream to null will close the stream properly
            set
            {
                if (_logFile != null)
                    _logFile.Close();
                 _logFile = value;
            }
        }

        private FormatStreamWriter _logFile;
        private bool _disposed;

        public StereoScopica()
        {
            // Setup the relative directory to the executable directory for loading contents with the ContentManager
            Content.RootDirectory = "Content";

            GraphicsDeviceManager = new GraphicsDeviceManager(this);
#if DEBUG
            // Debug mode to get better error msgs out from DX
            GraphicsDeviceManager.DeviceCreationFlags = DeviceCreationFlags.Debug;
#endif
            Renderer = new Renderer(GraphicsDeviceManager);
            ImagePlane = new TexturedPlane[2];
            CameraHandler = new CameraHandler[2];
            CameraSettings = new CameraSettings[2];
            Keyboard = new KeyboardManager(this);

            // LoadUserSettings() will initialize the imagePlane and cameraSettings arrays
            LoadUserSettingsOrSetDefaults();

            // By initializing the camera handling processes here their windows will be created before the main app window
            // and the focus won't be taken away from the main window
            for (var i = 0; i < CameraHandler.Length; i++)
            {
                CameraHandler[i] = new CameraHandler(CameraSettings[i]);
                CameraHandler[i].ImageUpdated += ImageUpdateEvent;
            }
        }

        protected override void Initialize()
        {
            try
            {
                Renderer.Initialize(GraphicsDevice, ((Control)Window.NativeWindow).Handle);
                RegisterKeyActions();
            }
            catch (Exception ex)
            {
                FatalError("Initialization error: " +ex.Message);
            }
            base.Initialize();
        }

        protected override void LoadContent()
        {
            try
            {
                // Initialize the planes
                foreach (TexturedPlane plane in ImagePlane)
                    plane.Initialize(GraphicsDevice);

                // Initializes & compiles the shader and assigns it to the planes
                ReloadPlaneShaders();
            }
            catch (CompilationException ex)
            {
                FatalError("Could not compile the shader file " + ex.Message);
            }
            catch (Exception ex)
            {
                FatalError(ex.Message);
            }
            base.LoadContent();
        }

        // Loads the serialized classes from the User Settings file or initializes them with the specified values
        protected void LoadUserSettingsOrSetDefaults()
        {
            try
            {
                UISettings = Properties.Settings.Default.UISettings ?? new UISettings
                {
                    //+Z values are "away from the plane looking at the plane", -Z values are behind the plane
                    HeadPositionRelativeToPlane = new Vector3(0f, 0f, 1.5f),
                    PlaneXYNudgeAmount = 0.0025f,
                    HeadZNudgeAmount = 0.025f,
                    BrightnessNudgeAmount = 0.05f,
                    TexturePixelShader = Path.Combine("Shaders", "Texture.ps"),
                    TextureVertexShader = Path.Combine("Shaders", "Texture.vs"),
                    TestImageLeft = Path.Combine("Assets", "TestL.jpg"),
                    TestImageRight = Path.Combine("Assets", "TestR.jpg"),
                    SaveImageLeft = "imageL.jpg",
                    SaveImageRight = "imageR.jpg",
                    LogFilePath = ""
                };
                // Create two camera handlers and image planes
                for (uint i = 0; i < ImagePlane.Length; i++)
                {
                    ImagePlane[i] = ((i == 0)
                        ? Properties.Settings.Default.ImagePlaneLeft
                        : Properties.Settings.Default.ImagePlaneRight) ?? new TexturedPlane();

                    CameraSettings[i] = ((i == 0)
                        ? Properties.Settings.Default.Camera0
                        : Properties.Settings.Default.Camera1) ?? new CameraSettings
                    {
                        DeviceIndex = i,
                        HandlerExecutableName = "CanonHandler.exe",
                        // Use the EXACT strings/values as written in the CanonEDSDKWrapper.cs
                        // Or else you will get wrong states (some default value)
                        WB = 4,
                        ISO = "ISO 1600",
                        Av = "11",
                        Tv = "1/20"
                    };
                }
            }
            catch (Exception ex)
            {
                FatalError(ex.Message + "Error in the user settings file. Please remove the file user.config to recreate it with the default settings. ");
            }
        }

        // Serializes the classes to the User Settings file using the default .NET XmlSerializer
        // The settings file is located under the user profile's Local/Roaming directory
        protected void SaveUserSettings()
        {
            Properties.Settings.Default.UISettings = UISettings;
            Properties.Settings.Default.Camera0 = CameraSettings[0];
            Properties.Settings.Default.Camera1 = CameraSettings[1];
            Properties.Settings.Default.ImagePlaneLeft = ImagePlane[0];
            Properties.Settings.Default.ImagePlaneRight = ImagePlane[1];
            Properties.Settings.Default.Save();
        }

        // (Re)loads the plane shaders
        protected void ReloadPlaneShaders()
        {
            // Set the planes' shader to null to disable the render call to the (nonexistent) shader
            foreach (TexturedPlane plane in ImagePlane)
                plane.TextureShader = null;

            InitializeAndCompileTextureShader();

            // Reset the planes' shader to start drawing again
            foreach (TexturedPlane plane in ImagePlane)
                plane.TextureShader = TextureShader;
        }

        // (Re)initializes the TextureShader and compiles the shader files
        protected void InitializeAndCompileTextureShader()
        {
            // Dispose the old shader resources if we want to reload the shader
            if (TextureShader != null)
            {
                TextureShader.Dispose();
                TextureShader = null; // Needed
            }
            TextureShader = new TextureShader();
            // Compile the texture shader
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile(UISettings.TextureVertexShader, "TextureVertexShader", "vs_4_0"))
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile(UISettings.TexturePixelShader, "TexturePixelShader", "ps_4_0"))
                TextureShader.Initialize(GraphicsDevice, vertexShaderByteCode, pixelShaderByteCode);
        }

        // Register UI key actions
        protected void RegisterKeyActions()
        {
            // Moves the image planes towards or away from each other
            var moveImagePlanes = new Action<Matrix, bool>((translation, inverse) =>
            {
                ImagePlane[0].Transformation *= translation;
                // Inverse the other translation
                if (inverse)
                    ImagePlane[1].Transformation *= Matrix.Translation(translation.TranslationVector * -1f);
                else
                    ImagePlane[1].Transformation *= translation;
            });

            // Exit the application
            UISettings.KeyActions[Keys.Escape] = Exit;
            // Reset HMD relative head position and orientation
            UISettings.KeyActions[Keys.Space] = Renderer.RecenterPose;
            // Toggle window update (if the result is also drawn to the desktop window in direct to rift mode)
            UISettings.KeyActions[Keys.Pause] = Renderer.ToggleDrawToWindow;

            // Reset planes' translation
            UISettings.KeyActions[Keys.Home] = () => { foreach (var plane in ImagePlane) plane.Transformation = Matrix.Translation(0f, 0f, 0f); };
            // Nudge the images away from each other along the X-axis
            UISettings.KeyActions[Keys.Left] = () => moveImagePlanes(Matrix.Translation(-UISettings.PlaneXYNudgeAmount, 0f, 0f), true);
            // Nudge the images towards each other along the X-axis
            UISettings.KeyActions[Keys.Right] = () => moveImagePlanes(Matrix.Translation(UISettings.PlaneXYNudgeAmount, 0f, 0f), true);
            // Nudge the images away from each other along the Y-axis
            UISettings.KeyActions[Keys.Up] = () => moveImagePlanes(Matrix.Translation(0f, UISettings.PlaneXYNudgeAmount, 0f), true);
            // Nudge the images towards each other along the Y-axis
            UISettings.KeyActions[Keys.Down] = () => moveImagePlanes(Matrix.Translation(0f, -UISettings.PlaneXYNudgeAmount, 0f), true);
            // Nudge the images up along the Y-axis
            UISettings.KeyActions[Keys.PageUp] = () => moveImagePlanes(Matrix.Translation(0f, UISettings.PlaneXYNudgeAmount, 0f), false);
            // Nudge the images down along the Y-axis
            UISettings.KeyActions[Keys.PageDown] = () => moveImagePlanes(Matrix.Translation(0f, -UISettings.PlaneXYNudgeAmount, 0f), false);

            // Move the head away from the plane
            UISettings.KeyActions[Keys.Add] = () => UISettings.HeadPositionRelativeToPlane.Z += UISettings.HeadZNudgeAmount;
            // Move the head towards the plane
            UISettings.KeyActions[Keys.Subtract] = () => UISettings.HeadPositionRelativeToPlane.Z -= UISettings.HeadZNudgeAmount;

            // Left image brightness multiplier down
            UISettings.KeyActions[Keys.F1] = () => ImagePlane[0].Brightness -= UISettings.BrightnessNudgeAmount;
            // Left image brightness multiplier up
            UISettings.KeyActions[Keys.F2] = () => ImagePlane[0].Brightness += UISettings.BrightnessNudgeAmount;
            // Reset left image brightness
            UISettings.KeyActions[Keys.F3] = () => ImagePlane[0].Brightness = 1f;
            // Toggle left image mirroring
            UISettings.KeyActions[Keys.F4] = () => ImagePlane[0].IsImageMirrored = !ImagePlane[0].IsImageMirrored;

            // Right image brightness multiplier down
            UISettings.KeyActions[Keys.F5] = () => ImagePlane[1].Brightness -= UISettings.BrightnessNudgeAmount;
            // Right image brightness multiplier up
            UISettings.KeyActions[Keys.F6] = () => ImagePlane[1].Brightness += UISettings.BrightnessNudgeAmount;
            // Reset right image brightness
            UISettings.KeyActions[Keys.F7] = () => ImagePlane[1].Brightness = 1f;
            // Toggle right image mirroring
            UISettings.KeyActions[Keys.F8] = () => ImagePlane[1].IsImageMirrored = !ImagePlane[1].IsImageMirrored;

            // Swap left & right image sources (doesn't work with the test images)
            UISettings.KeyActions[Keys.F9] = () => UISettings.SwapImageSources = !UISettings.SwapImageSources;
            // Toggle calibration mode
            UISettings.KeyActions[Keys.F10] = () => { UISettings.CalibrationMode = !UISettings.CalibrationMode; };
            // Reset the head position and stop updating it (from HMD data)
            UISettings.KeyActions[Keys.F11] = () => { UISettings.DoNotUpdateHeadPositionAndOrientation = !UISettings.DoNotUpdateHeadPositionAndOrientation; };
            // Toggle sensor data logging
            UISettings.KeyActions[Keys.F12] = () =>
            {
                UISettings.LogSensorData = !UISettings.LogSensorData;
                // Close the file if toggled off
                if (!UISettings.LogSensorData)
                    LogFile = null;
            };

            // Reload the shaders
            UISettings.KeyActions[Keys.R] = ReloadPlaneShaders;
            // Save the raw camera images once
            UISettings.KeyActions[Keys.S] = () =>
            {
                foreach (var c in CameraHandler)
                    c.ImageUpdated += SaveImageOnceEvent;
            };
            // Load test images onto the planes
            UISettings.KeyActions[Keys.T] = ShowTestImages;
        }

        //////////////////////////////////////////////////////////////
        /// Game Toolkit's overriden drawing & update events
        //////////////////////////////////////////////////////////////

        protected override bool BeginDraw()
        {
            if (!base.BeginDraw())
                return false;
            // Call the HMD's begin frame
            Renderer.BeginFrame(GraphicsDevice);
            return true;
        }

        protected override void Draw(GameTime time)
        {
            base.Draw(gameTime);
            GraphicsDevice.Clear(Color.Black);
            try
            {
                for (var i = 0; i < ImagePlane.Length; i++)
                {
                    // Transfer the buffered (received) image from the camera to the texture in the GPU's memory
                    ImagePlane[i].RefreshAndTransferTexture(GraphicsDevice);
                    // Set the texture to the specific texture slot for the shader
                    ((DeviceContext) GraphicsDevice).PixelShader.SetShaderResource(i, ImagePlane[i].Texture);
                }

                // Loop over both eyes
                foreach (int eyeIndex in Renderer.GetEyeIndex())
                {
                    // Set Viewport for this eye
                    GraphicsDevice.SetViewport(Renderer.EyeViewport[eyeIndex].ToViewportF());

                    // Get the world and projection data for the plane
                    var world = CalculatePlanePosition(eyeIndex);

                    // Z-near and Z-far values aren't that important in this scene
                    var projection = Renderer.GetEyeProjection(eyeIndex, 0.1f, 1000f);

                    // Shader parameters
                    var brightnessCoeffs = new Vector2(ImagePlane[0].Brightness, ImagePlane[1].Brightness);
                    var flags = (eyeIndex == 1 ? 1 : 0)
                                | (ImagePlane[0].IsImageMirrored ? 2 : 0)
                                | (ImagePlane[1].IsImageMirrored ? 4 : 0)
                                | (UISettings.CalibrationMode ? 8 : 0);

                    // Render the image plane
                    ImagePlane[eyeIndex].Render(GraphicsDevice, world, projection, flags, brightnessCoeffs);
                }
            }
            catch (Exception ex)
            {
                FatalError(ex.Message);
            }
        }

        protected override void EndDraw()
        {
            // Don't call the base method (base.EndDraw()) of the Game class as the present call is made through hmd.EndFrame()
            Renderer.EndFrame();
        }

        protected override void Update(GameTime time)
        {
            base.Update(time);
            // Check pressed keys
            HandleUIEvents();
            // Update title stats
            UpdateWindowTitle();
            if (UISettings.LogSensorData)
                LogSensorData();
        }

        //////////////////////////////////////////////////////////////
        /// Helper methods (run-time use) below
        //////////////////////////////////////////////////////////////
        
        // Returns the plane transform matrix for the given eye (plane position in world space)
        protected Matrix CalculatePlanePosition(int eyeIndex)
        {
            // Skip the calculation and return the default state if the following setting is enabled
            if (UISettings.DoNotUpdateHeadPositionAndOrientation)
                return Matrix.LookAtRH(UISettings.HeadPositionRelativeToPlane, -Vector3.UnitZ, Vector3.UnitY);

            var orientation = Renderer.EyePose[eyeIndex].Orientation.GetMatrix();
            var up = orientation.Transform(Vector3.UnitY);
            // -Z so that headPositionRelativeToPlane can have a positive Z value to indicate "away from the plane"
            var forward = orientation.Transform(-Vector3.UnitZ);
            var shiftedEyePos = UISettings.HeadPositionRelativeToPlane + orientation.Transform(Renderer.EyePose[eyeIndex].Position);
            return Matrix.LookAtRH(shiftedEyePos, shiftedEyePos + forward, up);
        }

        // A CameraHandler has an updated image for us
        protected void ImageUpdateEvent(Object sender, ImageUpdateEventArgs e)
        {
            // Reset stream position if the image has been read (if the any other event handler has been called before this)
            e.Image.Position = 0;
            // Check which CameraHandler gave us the image to update the right image plane (depending on the swap image toggle)
            for (var i = 0; i < CameraHandler.Length; i++)
            {
                if (!CameraHandler[i].Equals(sender)) continue;
                ImagePlane[(UISettings.SwapImageSources == Convert.ToBoolean(i) ? 0 : 1)].SetImage(e.Image);
                break;
            }
        }

        // Save the camera image onto disk and remove the handler from the ImageUpdated event
        protected void SaveImageOnceEvent(Object sender, ImageUpdateEventArgs e)
        {
            // Reset stream position if the image has been read (if the any other event handler has been called before this)
            e.Image.Position = 0;
            // Same logic as in ImageUpdateEvent()
            for (var i = 0; i < CameraHandler.Length; i++)
            {
                if (CameraHandler[i].Equals(sender))
                {
                    // Remove the event handler (self) to save only once
                    CameraHandler[i].ImageUpdated -= SaveImageOnceEvent;
                    try
                    {
                        var fs = new FileStream(
                            (UISettings.SwapImageSources == Convert.ToBoolean(i))
                                ? UISettings.SaveImageLeft
                                : UISettings.SaveImageRight,
                            FileMode.Create,
                            FileAccess.Write
                            );
                        e.Image.CopyTo(fs);
                        fs.Close();
                    }
                    catch (Exception ex)
                    {
                        ShowMessage(ex.Message);
                    }
                }
            }
        }

        // Check input & update the UI (executes the Action delegates)
        protected void HandleUIEvents()
        {
            // Check for pressed keys where the key exits in the KeyActions dictionary (LINQ Where returns only the satisfied elements)
            var keyboardState = Keyboard.GetState();
            foreach (var keyAction in UISettings.KeyActions.Where(keyAction => keyboardState.IsKeyPressed(keyAction.Key)))
                // Call its associated action (the foreach returns a temporary key-value pair)
                keyAction.Value();
        }

        // Test images' filenames are stored in the user settings file
        protected void ShowTestImages()
        {
            try
            {
                for (var i = 0; i < ImagePlane.Length; i++)
                {
                    var fs = new FileStream(
                        (i == 0)
                            ? UISettings.TestImageLeft 
                            : UISettings.TestImageRight,
                        FileMode.Open,
                        FileAccess.Read
                        );
                    ImagePlane[i].SetImage(fs);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        // Log sensor data to a text file
        // The LogFile property getter will create the stream
        protected void LogSensorData()
        {
            try
            {
                var data = Renderer.GetSensorData().RawSensorData;
                LogFile.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                    DateTime.Now.ToString("HH:mm:ss.fff"), data.Accelerometer.X, data.Accelerometer.Y,
                    data.Accelerometer.Z, data.Gyro.X, data.Gyro.Y, data.Gyro.Z);
            }
            catch (Exception ex)
            {
                UISettings.LogSensorData = false;
                ShowMessage("Logging stopped. Error: " + ex.Message);
            }
        }

        // Update window title statistics
        // This is not wise in any way, but the easiest way to get some info onto the screen without DX DirectWrite
        protected void UpdateWindowTitle()
        {

            Window.Title =
                String.Format(CultureInfo.InvariantCulture,
                    "{0} fps: {1}, Image fps: {2}/{3} (L/R), Brightness {4:f}/{5:f} (L/R), Head Z: {6:##.000}, Settings: {7}{8}{9}{10}",
                    Application.ProductName, Renderer.FPS, ImagePlane[0].FPS, ImagePlane[1].FPS,
                    ImagePlane[0].Brightness, ImagePlane[1].Brightness,
                    UISettings.HeadPositionRelativeToPlane.Z,
                    (UISettings.SwapImageSources ? "Swapped " : ""), (UISettings.CalibrationMode ? "Calibration " : ""),
                    (UISettings.DoNotUpdateHeadPositionAndOrientation ? "NoTracking " : ""), (UISettings.LogSensorData ? "Logging" : "")
                );
        }

        // Show message on screen or print it to debug output
        // TODO: Implement DX DirectWrite (sadly, not supported on DX 11.0. Needs 11.1 and windows store app targeting...)
        protected void ShowMessage(string message)
        {
#if DEBUG
            Debug.WriteLine(message);
#else
            // Annoying message box for now
            MessageBox.Show(message);
#endif
        }

        // Show error message and terminate application
        protected void FatalError(string message)
        {
            ShowMessage(message);
            Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                SaveUserSettings();
                foreach (var e in CameraHandler.Where(e => e != null))
                    e.Stop();
                foreach (var e in ImagePlane.Where(e => e != null))
                    e.Dispose();
                if (Keyboard != null)
                    Keyboard.Dispose();
                if (Renderer != null)
                    Renderer.Dispose();
                if (GraphicsDeviceManager != null)
                    GraphicsDeviceManager.Dispose();
                if (TextureShader != null)
                    TextureShader.Dispose();
                // The LogFile setter will close the open handle
                LogFile = null;
            }
            _disposed = true;
            base.Dispose(disposing);
        }
    }

    public class FormatStreamWriter : StreamWriter
    {
        private readonly IFormatProvider _formatProvider;

        public FormatStreamWriter(string path, IFormatProvider formatProvider)
            : base(path)
        {
            _formatProvider = formatProvider;
        }
        public override IFormatProvider FormatProvider
        {
            get
            {
                return _formatProvider;
            }
        }
    }
}