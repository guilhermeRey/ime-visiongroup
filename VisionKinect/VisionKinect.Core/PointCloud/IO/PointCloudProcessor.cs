using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionKinect.Core.IO.FileTypes;
using VisionKinect.Core.PointCloud.Recorder;

namespace VisionKinect.Core.PointCloud.IO
{
    public class PointCloudProcessor : FileType
    {
        string FileType.Extension()
        {
            return "clouds";
        }

        string FileType.Header(PointCloudTemp temp, PointCloudRecorderOptions options)
        {
            return "";
        }

        public string FormatLine(int id, Tuple<float, float, float> xyz, Tuple<int, int, int> rgb)
        {
            return String.Format(CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5} {6}\n", id,
                xyz.Item1, xyz.Item2, xyz.Item3,
                rgb.Item1, rgb.Item2, rgb.Item3);
        }
    }
}
