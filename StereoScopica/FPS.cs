/*
 * Simple frames per second (FPS) counter
 */

using System;

namespace StereoScopica
{
    public class FPS
    {
        public int Value { get; private set; }
        private int _frameCount;
        private TimeSpan _startTime;

        public FPS()
        {
            Reset();
        }

        public void Reset()
        {
            Value = 0;
            _frameCount = 0;
            _startTime = DateTime.Now.TimeOfDay;
        }

        public void Tick()
        {
            _frameCount++;
            // Don't update other values if one second has not yet passed
            if ((DateTime.Now.TimeOfDay - _startTime).Seconds < 1) return;
            Value = _frameCount;
            _frameCount = 0;
            _startTime = DateTime.Now.TimeOfDay;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
