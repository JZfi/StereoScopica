StereoScopica
=============

Wut?
----
StereoScopica shows two image sources as a 3D stereo image on the Oculus Rift. It supports Canon EOS cameras via Canon's EDSDK API.


Why?
----
StereoScopica was built for my master's thesis experiment setup where I replicate H. Ehrsson's study on the out-of-body illusion (DOI: 10.1126/science.1142175). 


How to run
----------
Binaries can be downloaded from <http://jz.fi/stereoscopica/>

Requirements
 - .NET Framework 4.0+
 - DirectX 11.0+

The shader runtime compilation requires the DirectX End-User Runtimes (June 2010, d3dcompiler_43.dll) found at:
<http://www.microsoft.com/en-us/download/details.aspx?id=8109>

This program has been tested on Windows 7 & 8.1. It should work on other Windows versions, but it won't work on other OS platforms. For technical reasons see the additional notes.


How to use
----------
To see some action: Run StereoScopica.exe and press F11 to load the provided test images.

The cameras will be automatically recognized when you plug them in. If the cameras stop working or are not recognized try to unplug the usb cables (both) or turn the cameras off & on again. Sometimes the LiveView mode is turned on but no images are received. This is either a bug in the EDSDK library or in the used wrapper code. The library is unstable and sometimes ends up in a state that needs a program restart. This tends to happen if you shutdown the camera while it is plugged in.

Note: If you factory reset the EOS cameras you must use the Canon's EOS Utility to setup the LiveView mode once. Plug it in and launch the "Camera settings/Remote shooting" window. Click on the "Live View shoot" -button and set the values to Stills+movie and Exposure simulation (these at least work) when it prompts for the settings. If the window does not appear it can be found under the settings tab (tools icon, set-up menu) by clicking "Live View/Movie func. set.".

Keyboard shortcuts (there is no GUI):

    Esc      Exit the application
    Space    Reset HMD relative head position and orientation
    Home     Toggle calibration mode
    End      Reset the head position and stop updating it (from HMD data)

    Left     Nudge the images away from each other along the X-axis (by 0.025)
    Right    Nudge the images towards each other along the X-axis (by 0.025)
    Up       Nudge the images away from each other along the Y-axis (by 0.025)
    Down     Nudge the images towards each other along the Y-axis (by 0.025)
    PageUp   Move the head away from the plane (by 0.05)
    PageDown Move the head towards the plane (by 0.05)

    F1       Left image brightness multiplier down
    F2       Left image brightness multiplier up
    F3       Reset left image brightness
    F4       Toggle left image mirroring

    F5       Right image brightness multiplier down
    F6       Right image brightness multiplier up
    F7       Reset right image brightness
    F8       Toggle right image mirroring

    F9       Swap left & right image sources (doesn't work with the test images)
    F10      Toggle window update (if the result is also drawn to the desktop window in direct to rift mode)
    F11      Load test images onto the planes
    F12      Save the raw camera images once

The application state is saved to the user settings file on every exit. If you want to reset the settings or ensure
that the same values are used each time replace or delete the file and re-run.
.NET creates this file under %USERPROFILE%\AppData\Local\StereoScopica\


How to compile
--------------
Dependencies
- Visual Studio (2013)
- SharpDX 2.x (via NuGet)
- SharpOVR (via NuGet)
- Comman line parser library (via NuGet)
- Canon EDSDK

The Canon EDSDK must be downloaded from Canon's websites (requires registration). You must register to your local Canon sales area website or Canon will reject your request. See the notes in section 3 at <http://www.usa.canon.com/cusa/consumer/standard_display/sdk_homepage>

The built-in NuGet package manager in Visual Studio will automatically download the referenced libraries. Just place the Canon's EDSK dll files into the debug/release directory.


Program structure
-----------------
*TODO*

The shader compilation is done during runtime via the legacy DX api in SharpDX.
<http://blogs.msdn.com/b/chuckw/archive/2012/05/07/hlsl-fxc-and-d3dcompile.aspx>


Known bugs
----------
- Test images don't work if the cameras are in use (pressing F11 triggers one image frame update, but the camera's next image update will override it).
- Unplugging one of the active cameras and replugging it sometimes results in EDS_ERR_COMM_PORT_IS_IN_USE. This is due to the way the cameras are indexed. The EDSDK library doesn't provide any unique ID for the cameras, so they are handled as array elements. If you unplug camera #0 the old #1 will be first in the array and will now be #0 even if the handler dedicated to #1 is still using it. The workaround is to unplug both and plug in the left one first.


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