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
        protected delegate bool EventHandler(CtrlType sig);
        protected EventHandler CloseHandler { get; set; }

        // Silence ReSharper enum complaints
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        protected enum CtrlType
        {
          CTRL_C_EVENT = 0,
          CTRL_BREAK_EVENT = 1,
          CTRL_CLOSE_EVENT = 2,
          CTRL_LOGOFF_EVENT = 5,
          CTRL_SHUTDOWN_EVENT = 6
        }

        protected EDSDKWrapper CameraWrapper { get; set; }
        protected PipeStream PipeClient { get; set; }
        protected BinaryWriter PipeWriter { get; set; }
        protected Settings Settings { get; set; }
        protected bool LiveViewUpdating { get; set; }
        protected bool Initialized { get; set; }
        private bool _disposed;

        public CanonHandler(Settings settings)
        {
            Settings = settings;
            // safeguard against array index error (since the command line parser can't use uint fields (easily at least)
            if (settings.DeviceIndex < 0)
                settings.DeviceIndex = 0;

            // Register the console close event handler
            CloseHandler += CloseEventHandler;
            SetConsoleCtrlHandler(CloseHandler, true);

            try
            {
                // Open the anonymous pipe to the host application for image transmission
                PipeClient = new AnonymousPipeClientStream(PipeDirection.Out, settings.PipeHandle);
                // Write wrapper
                PipeWriter = new BinaryWriter(PipeClient);
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
                CameraWrapper = new EDSDKWrapper();
                CameraWrapper.CameraAdded += CameraAdded;
                CameraWrapper.LiveViewUpdated += LiveViewUpdated;
                CameraWrapper.CameraHasShutdown += CameraHasShutdown;
                Initialized = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e);
            }
        }

        // Start(): Program start. Starts the main application loop (message pump)
        public void Start()
        {
            if (!Initialized)
                return;
            Console.WriteLine("Canon EOS camera handler for StereoScopica.");
            Console.WriteLine("Exit the main program to automatically shut down this handler.");
            Console.WriteLine();

            // Only try to initialize the preferred device index at startup
            List<Camera> camList = CameraWrapper.GetCameraList();
            if (camList.Count > Settings.DeviceIndex)
            {
                Console.Write("Initializing camera #{0}...", Settings.DeviceIndex);
                if (!InitializeCamera(camList[Settings.DeviceIndex]))
                    Console.WriteLine("Error!\n-> Could not initialize the camera. Try to plug in the camera(s) again.");
            }
            else
                Console.WriteLine("Not enough cameras plugged in. Waiting for camera #{0}.", Settings.DeviceIndex);

            // Enter the main application loop to enable event processing
            Application.Run();

        }

        // CameraAdded() iterates over the connected cameras and tries to initialize each (it can be in use)
        private void CameraAdded()
        {
            // Skip the event call if we have found our camera already
            if (CameraWrapper.CameraSessionOpen)
                return;

            List<Camera> camList = CameraWrapper.GetCameraList();
            Console.WriteLine("A camera was plugged in! Found {0} camera(s).", camList.Count);

            for (int i = 0; i < camList.Count; i++)
            {
                Console.Write("Trying to initialize camera {0} \"{1}\"...", i+1, camList[i].Info.szDeviceDescription);
                if (InitializeCamera(camList[i]))
                    break;
            }
        }

        // InitializeCamera() opens the camera session via the canon sdk wrapper, sets the right modes and starts the live view image feed
        private bool InitializeCamera(Camera camera)
        {
            try
            {
                CameraWrapper.OpenSession(camera);
                Console.Write("Done.\nAdjusting settings...");
                CameraWrapper.SetSetting(EDSDK.PropID_Av, CameraValues.AV(Settings.Av));
                CameraWrapper.SetSetting(EDSDK.PropID_Tv, CameraValues.TV(Settings.Tv));
                CameraWrapper.SetSetting(EDSDK.PropID_ISOSpeed, CameraValues.ISO(Settings.ISO));
                CameraWrapper.SetSetting(EDSDK.PropID_WhiteBalance, (uint)Settings.WB);

                Console.Write("Done.\nStarting LiveView...");
                LiveViewUpdating = false;
                CameraWrapper.StartLiveView();

                if (CameraWrapper.IsLiveViewOn)
                {
                    Console.WriteLine("Done.");
                    return true;
                }
                Console.WriteLine("Error!\n-> LiveView dit not start for some reason.");

            }
            catch (Exception e)
            {
                Console.WriteLine("Error!\n-> " + e.Message);
            }
            return false;
        }

        // LiveViewUpdated() is called from the canon sdk wrapper when a new image is read from the camera and is ready for processing
        // LiveViewUpdated() writes the image to the anonymous memory pipe.
        // An IO error will terminate the application.
        // Depth Of Field Preview mode is also enabled after the first received image (to avoid a black screen)
        private void LiveViewUpdated(Stream img)
        {
            // Check if first image
            if (!LiveViewUpdating)
            {
                Console.WriteLine("Received first image from camera. Enabling Depth of Field Preview.");
                // Enable Depth of Field Preview mode for auto focus
                // Enabling it now seems to work better than right after calling StartLiveView() (for some reason)
                CameraWrapper.SetSetting(EDSDK.PropID_Evf_DepthOfFieldPreview, 1);
                LiveViewUpdating = true;
            }
            if (PipeClient.IsConnected)
            {
                try
                {
                    // Protocol: int32 [image length] + length amount of image data in bytes
                    int written;
                    int length = (int)img.Length;
                    if (length <= 0)
                        return;
                    byte[] buffer = new byte[length];
                    PipeWriter.Write(length);
                    while ((written = img.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        PipeWriter.Write(buffer, 0, written);
                    }
                    PipeWriter.Flush();
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
                Console.WriteLine("Pipe disconnected during image update, exiting...");
                CloseCamera();
                Application.Exit();
            }
        }

        // CameraHasShutdown() is called from the canon sdk wrapper
        private void CameraHasShutdown(object sender, EventArgs e)
        {
            Console.WriteLine("The camera was disconnected.");
            LiveViewUpdating = false;
        }

        // Disconnect camera
        private void CloseCamera()
        {
            if (!CameraWrapper.CameraSessionOpen)
                return;
            if (!CameraWrapper.IsLiveViewOn)
                return;
            // Unset Depth of Field Preview to avoid errorous states in the camera
            CameraWrapper.SetSetting(EDSDK.PropID_Evf_DepthOfFieldPreview, 0);
            LiveViewUpdating = false;
            // Don't call CloseSession() here! The SetSetting call above will result in a exception in a different thread in the CanonEDSDKWrapper
            // The wrapper will call closesession when it is needed
        }

        // Windows close event handler: the application is terminating -> free memory
        private bool CloseEventHandler(CtrlType sig)
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
            if (disposing && Initialized)
            {
                try {
                    CameraWrapper.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine("EDSDK shutdown error: " + e);
                }
                PipeWriter.Dispose();
                PipeClient.Dispose();
            }
            _disposed = true;
        }

        ~CanonHandler()
        {
            Dispose(false);
        }
    }
}
