using SharpDX;
using SharpDX.Toolkit.Input;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace StereoScopica
{
    public class UISettings
    {
        // Base head position relative to the image plane. The plane is centered at origo
        // +Z values are "away from the plane looking at the plane", -Z values are behind the plane
        public Vector3 HeadPositionRelativeToPlane;

        // Swap the left right image plane image sources (camera handlers) toggle
        public bool SwapImageSources;
        // Camera calibration mode (shows the diff. between the images)
        public bool CalibrationMode;
        // Stop moving the virtual head from HMD data
        public bool DoNotUpdateHeadPositionAndOrientation;
        public string TestImageLeft;
        public string TestImageRight;

        // UI key bindings
        [XmlIgnore]
        public Dictionary<Keys, Action> KeyActions = new Dictionary<Keys, Action>();
    }
}
