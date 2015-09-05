using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionKinect.Core.Device;

namespace VisionKinect.Core.Interop.Core
{
    public interface RGBStreamable
    {
        IDevice Sensor { get; }

        void SetColorDisplaySource(object image);
    }
}
