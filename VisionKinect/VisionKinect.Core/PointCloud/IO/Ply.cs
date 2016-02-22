using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VisionKinect.Core.PointCloud.Recorder;

namespace VisionKinect.Core.IO.FileTypes
{
    public class Ply : FileType
    {
        public string Extension()
        {
            return "ply";
        }

        public string Header(PointCloudTemp temp, PointCloudRecorderOptions options)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ply");
            sb.AppendLine("format ascii 1.0");
            sb.AppendLine(String.Concat("element vertex ", temp.PointCount));
            sb.AppendLine("property float x");
            sb.AppendLine("property float y");
            sb.AppendLine("property float z");
            sb.AppendLine("property uchar red");
            sb.AppendLine("property uchar green");
            sb.AppendLine("property uchar blue");
            sb.AppendLine("end_header");

            return sb.ToString();
        }


        public string FormatLine(int id, Tuple<float, float, float> xyz, Tuple<int, int, int> rgb)
        {
            return String.Format(CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5}\n",
                xyz.Item1, xyz.Item2, xyz.Item3,
                rgb.Item1, rgb.Item2, rgb.Item3);
        }
    }
}
