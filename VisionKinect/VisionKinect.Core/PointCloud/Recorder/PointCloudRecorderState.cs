using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionKinect.Core.PointCloud.Recorder
{
    public enum PointCloudRecorderState
    {
        Idle,
        Recording,
        Stopping,
        ProcessingClouds,
        WritingFile,
        Finishing
    }
}
