/*
 * The CameraHandler provides an image source via the ImageUpdated event when the camera's image is updated.
 * Change or override this class if you want to implement you own image source or camera api handler. 
 * Scrap the process handling and just call the ImageUpdated() from a separate thread as done below.
 * 
 * - CanonHandler
 * Currently, this class spawns a separate process for the camera communication due to the limitations of the Canon EDSDK.
 * EDSDK supports only one active camera per library instance. .NET loads only one instance of the library per process, 
 * so we need separate processes to load more copies.The images are read from the process over an anonymous memory pipe.
 * A separate thread takes care of this. The CanonHandler exe is called with the provided settings as command line arguments.
 * 
 * The images are transfered using a simple "protocol": int32 [image size in bytes] + size amount of bytes
 * The image is written into an unmanaged memorystream and passed to the image handler (for thread safety). 
 * The reseved memory is freed after the delegate call (event).
 * If the memory pipe read fails or this application is terminated the spawned process is stopped.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace StereoScopica
{
    public class ImageUpdateEventArgs : EventArgs
    {
        public Stream Image { get; set; }
    }
    public delegate void ImageUpdateHandler(Object sender, ImageUpdateEventArgs e);

    public class CameraHandler
    {

        public event ImageUpdateHandler ImageUpdated;

        private readonly CameraSettings _settings;
        private Process _process;
        private readonly Thread _imageReaderThread;

        public CameraHandler(CameraSettings settings)
        {
            _settings = settings;
            // Start a separate thread for the image acquiring business
            _imageReaderThread = new Thread(Start);
            _imageReaderThread.Start();
        }

        // This solution utilizes a separate process for camera handling and uses an anonymous pipe to communicate with it to receive images from the camera
        // The Canon EOS handling is done separately to enable several connected cameras at the same time since the EDSDK DLL is loaded statically and only allows one connected camera
        public virtual void Start()
        {
            // An anonymous pipe is used to get the images from the camerahandler process
            using (var pipeServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable))
            {
                _process = new Process
                {
                    StartInfo =
                    {
                        FileName = _settings.HandlerExecutableName,
                        UseShellExecute = false,
                        Arguments = "--pipe=\"" + pipeServer.GetClientHandleAsString() + "\" " +
                                    "--device=" + _settings.DeviceIndex + " " +
                                    "--av=\"" + _settings.Av + "\" " +
                                    "--tv=\"" + _settings.Tv + "\" " +
                                    "--wb=\"" + _settings.WB + "\" " +
                                    "--iso=\"" + _settings.ISO + "\" "
                    }
                };
                _process.Start();

                //MSDN: The DisposeLocalCopyOfClientHandle method should be called after the client handle has been passed to the client. 
                //      If this method is not called, the AnonymousPipeServerStream object will not receive notice when the client disposes of its PipeStream object.
                pipeServer.DisposeLocalCopyOfClientHandle();

                // Pipe reader (reads the camera images ad infinitum)
                using (var binaryReader = new BinaryReader(pipeServer))
                {
                    try
                    {
                        while (pipeServer.IsConnected)
                        {
                            // Data structure: image size in bytes as int32 and then the image size amount of data
                            var imgSize = binaryReader.ReadInt32();
                            if (imgSize <= 0)
                                continue;
                            byte[] buf = binaryReader.ReadBytes(imgSize);
                            unsafe
                            {
                                var memIntPtr = IntPtr.Zero;
                                try
                                {
                                    // Reserve unamanged memory for the image so that we can use it in another thread safely
                                    memIntPtr = Marshal.AllocHGlobal(imgSize);
                                    var writeStream = new UnmanagedMemoryStream((byte*) memIntPtr.ToPointer(), imgSize,
                                        imgSize, FileAccess.Write);
                                    writeStream.Write(buf, 0, imgSize);
                                    writeStream.Close();

                                    // The stream above could be opened using Read+Write permissions, but it's better to provide a read-only stream to the handler delegate
                                    var readStream = new UnmanagedMemoryStream((byte*) memIntPtr.ToPointer(), imgSize,
                                        imgSize, FileAccess.Read);
                                    // Pass the image to the delegate to call for image update
                                    if (ImageUpdated != null)
                                        ImageUpdated(this, new ImageUpdateEventArgs{Image = readStream});
                                    readStream.Close();
                                }
                                finally
                                {
                                    // Free the unmanaged memory
                                    if (memIntPtr != IntPtr.Zero)
                                        Marshal.FreeHGlobal(memIntPtr);
                                }
                            }
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        CallClose();
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine("CameraHandlerProcess pipe error: " + ex.Message);
                        CallClose();
                    }
                    Debug.WriteLine("CameraHandlerProcess: pipe is no longer connected.");
                }
            }
        }
        
        protected virtual void CallClose()
        {
            if (!_process.HasExited)
                _process.CloseMainWindow();
        }

        public virtual void Stop()
        {
            _imageReaderThread.Abort();
            CallClose();
        }

        ~CameraHandler()
        {
            _process.Close();
            _process.Dispose();
        }
    }
}
