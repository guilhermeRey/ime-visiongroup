using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VisionKinect.Core.Device
{
    public interface IDevice
    {
        void Initialize();
        void Open();
        void Dismiss();
        bool IsActive { get; }
        object OpenRGB();
        object OpenMultiFrame();
        object GetSensor();
    }
}
