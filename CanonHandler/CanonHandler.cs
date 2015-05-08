/*
 * Canon EOS camera handler. Uses Canon's EDSDK via the CanonEDSDKWrapper.
 * Automatically starts the Live View -mode and transmits the images over an anonymous pipe to the host process.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CanonEDSDKWrapper;
using EDSDKLib;

namespace CanonHandler
{
    public class CanonHandler : IDisposable
    {
        // Needed to catch the console app's closing event
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        private readonly EventHandler _closeHandler;

        // Silence ReSharper enum complaints
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        enum CtrlType
        {
          CTRL_C_EVENT = 0,
          CTRL_BREAK_EVENT = 1,
          CTRL_CLOSE_EVENT = 2,
          CTRL_LOGOFF_EVENT = 5,
          CTRL_SHUTDOWN_EVENT = 6
        }

        private readonly EDSDKWrapper _cameraWrapper;
        private readonly PipeStream _pipeClient;
        private readonly BinaryWriter _pipeWriter;
        private readonly Settings _settings;
        private readonly bool _initialized;
        private bool _liveViewUpdating;
        private bool _disposed;

        public CanonHandler(Settings settings)
        {
            _settings = settings;
            // safeguard against array index error (since the command line parser can't use uint fields (easily at least)
            if (settings.DeviceIndex < 0)
                settings.DeviceIndex = 0;

            // Register the console close event handler
            _closeHandler += CloseHandler;
            SetConsoleCtrlHandler(_closeHandler, true);

            try
            {
                // Open the anonymous pipe to the host application for image transmission
                _pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, settings.PipeHandle);
                // Write wrapper
                _pipeWriter = new BinaryWriter(_pipeClient);
            }
            catch (IOException)
            {
                Console.WriteLine("Error: Cannot open pipe handle \"" + settings.PipeHandle + "\".");
                return;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Error: Cannot open pipe handle \""+ settings.PipeHandle +"\".");
                return;
            }
            try
            {
                // Initialize the canon sdk wrapper
                _cameraWrapper = new EDSDKWrapper();
                _cameraWrapper.CameraAdded += SearchForCamera;
                _cameraWrapper.LiveViewUpdated += LiveViewUpdated;
                _cameraWrapper.CameraHasShutdown += CameraHasShutdown;
                _initialized = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e);
            }
        }

        // Start(): Program start. Starts the main application loop (message pump)
        public void Start()
        {
            if (!_initialized)
                return;
            Console.WriteLine("Canon EOS camera handler for StereoScopica.");
            Console.WriteLine("Exit the main program to automatically shut down this handler.");
            Console.WriteLine();
            Console.WriteLine("Searching for cameras...");

            SearchForCamera();

            // Enter the main application loop to enable event processing
            Application.Run();

        }

        // SearchForCamera() iterates over the connected cameras, searches for the camera index (provided at the command line)
        // and initializes the found camera using InitializeCamera()
        private void SearchForCamera()
        {
            // Skip the event call if we have found our camera already
            if (_cameraWrapper.CameraSessionOpen)
                return;

            List<Camera> camList = _cameraWrapper.GetCameraList();
            Console.WriteLine("Found {0} camera(s).", camList.Count);
            for (int i = 0; i < camList.Count; i++)
            {
                Console.WriteLine("Camera #{0}: \"{1}\"", i, camList[i].Info.szDeviceDescription);
            }
            if (camList.Count > _settings.DeviceIndex)
            {
                Console.WriteLine("Initializing camera #{0}...", _settings.DeviceIndex);
                InitializeCamera(camList[_settings.DeviceIndex]);
            }
            else
                Console.WriteLine("Waiting for camera #{0}...", _settings.DeviceIndex);
        }

        // InitializeCamera() opens the camera session via the canon sdk wrapper, sets the right modes and starts the live view image feed
        private void InitializeCamera(Camera camera)
        {
            try
            {
                _cameraWrapper.OpenSession(camera);
                _cameraWrapper.SetSetting(EDSDK.PropID_Av, CameraValues.AV(_settings.Av));
                _cameraWrapper.SetSetting(EDSDK.PropID_Tv, CameraValues.TV(_settings.Tv));
                _cameraWrapper.SetSetting(EDSDK.PropID_ISOSpeed, CameraValues.ISO(_settings.ISO));
                _cameraWrapper.SetSetting(EDSDK.PropID_WhiteBalance, (uint)_settings.WB);

                Console.WriteLine("Starting LiveView...");
                _liveViewUpdating = false;
                _cameraWrapper.StartLiveView();

                if (!_cameraWrapper.IsLiveViewOn)
                    Console.WriteLine("Error: LiveView dit not start for some reason.");
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e);
            }
        }

        // LiveViewUpdated() is called from the canon sdk wrapper when a new image is read from the camera and is ready for processing
        // LiveViewUpdated() writes the image to the anonymous memory pipe.
        // An IO error will terminate the application.
        // Depth Of Field Preview mode is also enabled after the first received image (to avoid a black screen)
        private void LiveViewUpdated(Stream img)
        {
            // Check if first image
            if (!_liveViewUpdating)
            {
                Console.WriteLine("Received first image from camera. Enabling Depth of Field Preview.");
                // Enable Depth of Field Preview mode for auto focus
                // Enabling it now seems to work better than right after calling StartLiveView() (for some reason)
                _cameraWrapper.SetSetting(EDSDK.PropID_Evf_DepthOfFieldPreview, 1);
                _liveViewUpdating = true;
            }
            if (_pipeClient.IsConnected)
            {
                try
                {
                    // Protocol: int32 [image length] + length amount of image data in bytes
                    int written;
                    int length = (int)img.Length;
                    if (length <= 0)
                        return;
                    byte[] buffer = new byte[length];
                    _pipeWriter.Write(length);
                    while ((written = img.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        _pipeWriter.Write(buffer, 0, written);
                    }
                    _pipeWriter.Flush();
                }
                catch (IOException e)
                {
                    Console.WriteLine("Pipe error: {0}", e.Message);
                    CloseCamera();
                    Application.Exit();
                }
            }
            else
            {
                Console.WriteLine("Pipe is disconnected during image update, exiting...");
                CloseCamera();
                Application.Exit();
            }
        }

        // CameraHasShutdown() is called from the canon sdk wrapper
        private void CameraHasShutdown(object sender, EventArgs e)
        {
            Console.WriteLine("The camera has shutdown.");
            _liveViewUpdating = false;
        }

        // Disconnect camera
        private void CloseCamera()
        {
            if (!_cameraWrapper.CameraSessionOpen)
                return;
            if (!_cameraWrapper.IsLiveViewOn)
                return;
            // Unset Depth of Field Preview to avoid errorous states in the camera
            _cameraWrapper.SetSetting(EDSDK.PropID_Evf_DepthOfFieldPreview, 0);
            _liveViewUpdating = false;
            // Don't call CloseSession() here! The SetSetting call above will result in a exception in a different thread in the CanonEDSDKWrapper
            // The wrapper will call closesession when it is needed
        }

        // Windows close event handler: the application is terminating -> free memory
        private bool CloseHandler(CtrlType sig)
        {
            Dispose();
            return false;
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
            if (disposing && _initialized)
            {
                try {
                    _cameraWrapper.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine("EDSDK shutdown error: " + e);
                }
                _pipeWriter.Dispose();
                _pipeClient.Dispose();
            }
            _disposed = true;
        }

        ~CanonHandler()
        {
            Dispose(false);
        }
    }
}
