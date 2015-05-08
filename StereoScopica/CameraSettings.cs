/*
 * Camera settings class. Provides the camera settings for the CameraHandler.
 * This class is serialized to the user settings file.
 */

namespace StereoScopica
{
    public class CameraSettings
    {
        public uint DeviceIndex { get; set; }
        public string HandlerExecutableName { get; set; }
        public uint WB { get; set; }
        public string ISO { get; set; }
        public string Av { get; set; }
        public string Tv { get; set; } 
    }
}
