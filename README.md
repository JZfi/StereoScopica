StereoScopica
=============
Version 1.0.3
- Feature: Sensor logging support. Rift's acc & gyro data can now be logged to a csv file. Toggle logging on/off by pressing *L*. The logfile path can be set in the settings file.
- Feature: Images can be moved up & down in the same direction (page up/down).
- Bugfix: Test images are now shown even if the cameras are active (the images aren't overwritten).
- UI change: Some key mappings changed. Less used functions moved to letter-keys.
- UI change: Window title format change.
- Code change: Some safety checks added.

Version 1.0.2
- Feature: Shaders can now be reloaded (recompiled) by pressing the Delete-key (v1.0.3: *R*-key).
- Bugfix: Chromatic distortion correction was commented out in the Renderer
- Bugfix: The CanonHandler will now properly iterate over all devices and try to connect to them by force. This reduces manual camera reconnecting.
- Cleanup: Shader files had the UTF-8 BOM character in them
- Code change: Shader compilation is now done in the StereoScopica class to remove the D3DCompiler dependency from the TextureShader class. 
- Code change: The TexturedPlane's TextureShader property is now public and its initialization is now done outside the class to support a shared TextureShader instance.
- Code change: The CanonHandler class went through some ReSharping.

Version 1.0.1
- Bugfix: The up-key had a sign error and did the same thing as the down-key.
- All constant values and filenames are now present in the settings file


Wut?
----
StereoScopica shows two image sources as a 3D stereo image on the Oculus Rift. It supports Canon EOS cameras via Canon's EDSDK API. The images are fetched from the cameras using the LiveView mode. This limits the image resolution (depends on camera model) but provides a nice stream of JPEG-images. The images' brightness can be adjusted and they can be mirrored. A calibration mode is implemented to aid the proper alignment of the stereo cameras.


Why?
----
StereoScopica was built for my master's thesis experiment setup where I replicate H. Ehrsson's study on the out-of-body illusion (DOI: 10.1126/science.1142175). 


How to run
----------
Binaries can be downloaded from <http://jz.fi/stereoscopica/>

Requirements
 - .NET Framework 4.0+
 - DirectX 11.0+
 - A graphics card with DX 10 support

This program has been tested on Windows 7 & 8.1. It should work on other Windows versions, but it won't work on other OS platforms. For technical reasons see the additional notes.


How to use
----------
To see some action: Run StereoScopica.exe and press *F12* to load the provided test images.

The cameras will be automatically recognized when you plug them in. If the cameras stop working or are not recognized try to unplug the usb cables (both) or turn the cameras off & on again.

__Note:__ If you factory reset the EOS cameras you must use the Canon's EOS Utility to setup the LiveView mode once. Plug it in and launch the "Camera settings/Remote shooting" window. Click on the "Live View shoot" -button and set the values to Stills+movie and Exposure simulation (these at least work) when it prompts for the settings. If the window does not appear it can be found under the settings tab (tools icon, set-up menu) by clicking "Live View/Movie func. set.".

Keyboard shortcuts (there is no GUI):

    Esc      Exit the application
    Space    Reset HMD relative head position and orientation
    Pause    Toggle window update (if the result is also drawn to the desktop window in direct to rift mode)

    Home     Reset plane's translation values
    Left     Nudge the images away from each other along the X-axis (by 0.025)
    Right    Nudge the images towards each other along the X-axis (by 0.025)
    Up       Nudge the images away from each other along the Y-axis (by 0.025)
    Down     Nudge the images towards each other along the Y-axis (by 0.025)
    PageUp   Nudge the images up along the Y-axis (by 0.025)
    PageDown Nudge the images down along the Y-axis (by 0.025)

    Add      Move the head away from the plane (by 0.05)
    Subtract Move the head towards the plane (by 0.05)

    F1       Left image brightness multiplier down
    F2       Left image brightness multiplier up
    F3       Reset left image brightness
    F4       Toggle left image mirroring

    F5       Right image brightness multiplier down
    F6       Right image brightness multiplier up
    F7       Reset right image brightness
    F8       Toggle right image mirroring

    F9       Swap left & right image sources (doesn't work with the test images)
    F10      Toggle calibration mode
    F11      Reset the head pos&orientation and stop updating it (from HMD data)
    F12      Load test images onto the planes

    L        Toggle sensor data logging (creates a new csv file when toggled on)
    R        Reload the shaders
    S        Save the raw camera images once

The application state is saved to the user settings file on every exit. If you want to reset the settings or ensure
that the same values are used each time replace or delete the file and re-run.
.NET creates this file under %USERPROFILE%\AppData\Local\StereoScopica\


Calibration mode
----------------
The calibration mode can be toggled with the *F10*-key. It shows the color difference between the left & right images (left: L-R, right: R-L). When the images are identical the result is a black image. This can be achieved using a mirror box camera rig and the black result equals to identically aligned cameras. To test if the distance between the cameras equals to the wanted IPD (interpupillary distance) print out a checkerboard pattern with the IPD as the checker size (e.g. 64 mm rectangles). When the black & white borders match (align side to side) the cameras are aligned properly. 

You can use the Checkerboard Maker <http://www.cs.unc.edu/~adyilie/Research/CheckerboardMaker/> to achieve this easily. 


Sensor data logging
-------------------
Rift's acceleration and gyro sensor data can be logged to a csv-file by pressing the *L*-key. A new logfile will be created in the executable's directory every time logging is toggled on. The files are named using the format *log_yyyyMMdd_HHmmss.csv*. The logfile directory can be changed by setting the LogFilePath-key (<LogFilePath>c:\logs</LogFilePath) in the user settings file. An empty value (<LogFilePath />) tells the application to use the executable directory. 

Logfile format: Timestamp(HH:mm:ss.fff),Acc.X,Acc.Y,Acc.Z,Gyro.X,Gyro.Y,Gyro.Z
The sensor data is queried and logged on every Update() call. Sample rate is limited to the drawn frames per second (and will fluctuate).


How to compile
--------------
Dependencies
- Visual Studio (2013)
- SharpDX 2.x (via NuGet)
- SharpOVR (via NuGet)
- Command line parser library (via NuGet)
- Canon EDSDK
- d3dcompiler_43.dll (DirectX End-User Runtimes June 2010)

The shader compilation in SharpDX 2.x currently depends on the older DX 11 D3DCompiler and it requires the file *d3dcompiler_43.dll*. You can grab the dll-file from the precompiled StereoScopica archive and place it in the debug/release directory, or install the legacy DirectX End-User Runtimes available at
<http://www.microsoft.com/en-us/download/details.aspx?id=8109>. 
For more details see <http://blogs.msdn.com/b/chuckw/archive/2012/05/07/hlsl-fxc-and-d3dcompile.aspx>

Place the Canon EDSDK library files *(DPPDLL.dll, DPPLibcom.dll, DPPRSC.dll, EDSDK.dll, EdsImage.dll, Mlib.dll, Ucs32P.dll)* into the debug/release directory. Grab the files from the precompiled StereoScopica archive or download the EDSDK from Canon's website (requires registration). You must register to your local Canon sales area website or Canon will reject your request. See the notes in section 3 at <http://www.usa.canon.com/cusa/consumer/standard_display/sdk_homepage>.

The built-in NuGet package manager in Visual Studio will automatically download the referenced libraries. 


Program structure
-----------------

__StereoScopica__

StereoScopica is based on the SharpDX Toolkit API's Game class. The StereoScopica class contains the main application logic including the default values and hard-coded constants. If you want to modify something start with this class. 

StereoScopica initializes the Renderer, two image planes (TexturedPlane) and two camera handlers (CameraHandler) as image sources for the image planes. The camera images are converted to textures that are drawn using a shader. Image effects are implemented on the shader level and they can be adjusted independently to the left and right image. The shaders are compiled at runtime and the shaders can be modified and reloaded. One TextureShader instance is shared between the two planes to support the calibration mode that blends both images together onto one plane.

The program state is defined by the CameraSettings and TexturedPlane instances and the UISettings instance. The state is loaded from the .NET user settings file at startup. If the file doesn't exist the classes are initialized with the default values and they will be serialized into the settings file on exit.

__Renderer__

The Renderer class takes care of the DirectX and HMD handling using the SharpDX and SharpOVR -libraries.

__TexturedPlane__

The TexturedPlane class represents a textured plane drawn using a given TextureShader instance. It extends the Plane class which is an modified version of the Rastertek's Model class from tutorial 5. The Plane class takes care of the vertex updates and the TexturedPlane transfers the image to the texture resource on the GPU. The plane's vertices are updated to accommodate the image's aspect ratio. The image update mechanism is thread safe as the acquired images from the cameras are updated in separate threads.

__TextureShader__

The TextureShader class takes precompiled shader bytecode, initializes it and the needed buffers. It takes care of the shader buffer & parameter updates. Note: PixelShader.SetShaderResource() is not called by the TextureShader nor TexturedPlane since both textures are visible only at the main app level.

__CameraHandler__

The CameraHandler class provides an image source via the ImageUpdated event. The camera settings are stored in a CameraSettings instance which is passed to the constructor. The current CameraHandler implementation starts a new thread for the image acquisition. The thread spawns a new process for the actual camera handling and reads the images from the process over an anonymous memory pipe. The pipe handle and the camera settings are passed to the executable as command line arguments. A separate process is needed because the Canon EDSDK library can only handle one active camera and the native library is loaded as one static instance (per process in .NET). This limitation is circumvented via the two separate processes (two CameraHandler instances in the main app). The executable filename and camera settings can be changed from the user settings file. Derive and implement your own camera handler by overriding the CameraHandler's methods.

__CanonHandler__

CanonHandler is a separate project in the StereoScopica solution. It produces a command line executable that takes care for the Canon EOS camera handling. Canon's EDSDK library is accessed using the CanonEDSDKWrapper, which is a slightly modified version of Johannes Bildstein's LGPL 3.0 licensed code (link in references).

At startup the CanonHandler tries to open the camera # specified via the command line argument. If the camera is not found it will wait until a camera is plugged in. The camera(s) can be connected or reconnected at runtime. When a camera is plugged the EDSDK will raise an event. Since there is no way of identifying the cameras in the device list in a unique way or to check if they are in use the program will try to open them one at a time (the device list order can change).

The camera settings are passed via the command line. The LiveView-mode is started automatically and after the first image is received from the camera the Depth of Field Preview -mode is toggled on. The images are written to an anonymous memory pipe which handle is passed from the host application via the command line.


Known bugs
----------
- Sometimes the LiveView mode is turned on but no images are received. This is either a bug in the EDSDK library or in the used wrapper code. The library is unstable and sometimes ends up in a state that needs a program restart. This tends to happen if you shutdown the camera while it is plugged in.
- Sometimes the key presses don't respond (every 2nd image save press, logging toggle after an exception). No idea why.


Additional notes
----------------
I carried out some initial testing in Unity 4 but decided to switch to a more native implementation to have better control over the latency. The Canon's EDSDK didn't seem to work on Mono as it only returned EDS_ERR_INTERNAL_ERROR on every call and Oculus Rift didn't have official support on other platforms at that time so I decided to use SharpDX and SharpOVR on .net. The current camera handling implementation uses two separate processes to overcome the one active camera per library instance (EDSDK limitation) and one native library instance per process (.net feature) limitations. This way it could also work on Mono (and Unity) to provide easier portability if the cameras are handled using some other software like gPhoto2. 


References
----------
The shader and plane initialization is based on the excellent tutorial code originally made by Rastertek (<http://www.rastertek.com/>). The C# and SharpDX conversions can be found at:
<http://sharpdx.org/forum/7-documentation/52-rastertek-tutorials-in-sharpdx>
<http://sharpdx-examples.googlecode.com>

The Canon EOS camera handling is done using Johannes Bildstein's LGPL 3.0 licensed Canon EDSDK wrapper code (with some minor modifications) published at:
<http://www.codeproject.com/Articles/688276/Canon-EDSDK-Tutorial-in-Csharp>

The provided test image is taken by Kim Scarborough (cc-by-sa licensed): 
<http://commons.wikimedia.org/wiki/File:Art_Institute_of_Chicago_Lion_Statue_%28cross-eye_stereo_pair%29.jpg>