using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisionKinect.Core.PointCloud.Recorder;

namespace VisionKinect.Core.IO.FileTypes
{
    public interface FileType
    {
        string Extension();
        string Header(PointCloudTemp temp);
        string FormatLine(int id, Tuple<float, float, float> xyz, Tuple<int, int, int> rgb);
    }
}
