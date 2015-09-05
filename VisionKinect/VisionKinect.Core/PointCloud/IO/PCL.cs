using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisionKinect.Core.IO.FileTypes;
using VisionKinect.Core.PointCloud.Recorder;

namespace VisionKinect.Core.PointCloud.IO
{
    public class PCL : FileType
    {

        public string Extension()
        {
            return "pcd";
        }

        public string Header(PointCloudTemp temp)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# .PCD v.7 - Point Cloud Data file format");
            sb.AppendLine("VERSION .7");
            sb.AppendLine("FIELDS x y z rgb");
            sb.AppendLine("SIZE 4 4 4 4");
            sb.AppendLine("TYPE F F F F");
            sb.AppendLine("COUNT 1 1 1 1");
            sb.AppendLine("WIDTH " + temp.DepthWidth);
            sb.AppendLine("HEIGHT " + temp.DepthHeight);
            sb.AppendLine("VIEWPOINT 0 0 0 1 0 0 0");
            sb.AppendLine("POINTS " + temp.PointCount);
            sb.AppendLine("DATA ascii");
            return sb.ToString();
        }

        public string FormatLine(int id, Tuple<float, float, float> xyz, Tuple<int, int, int> rgb)
        {
            int _rgb = ((int)rgb.Item1) << 16 | ((int)rgb.Item2) << 8 | ((int)rgb.Item3); 
            return String.Format("{0} {1} {2} {3}\n", xyz.Item1, xyz.Item2, xyz.Item3, _rgb);
        }
    }
}
