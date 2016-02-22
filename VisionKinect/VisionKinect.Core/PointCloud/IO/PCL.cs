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

        public string Header(PointCloudTemp temp, PointCloudRecorderOptions options)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# .PCD v.7 - Point Cloud Data file format");
            sb.AppendLine("VERSION .7");
            if (!options.RecordRGB)
            {
                sb.AppendLine("FIELDS x y z");
                sb.AppendLine("SIZE 4 4 4");
                sb.AppendLine("TYPE F F F");
                sb.AppendLine("COUNT 1 1 1");
            }
            else
            {
                sb.AppendLine("FIELDS x y z rgb");
                sb.AppendLine("SIZE 4 4 4 4");
                sb.AppendLine("TYPE F F F F");
                sb.AppendLine("COUNT 1 1 1 1");
            }

            sb.AppendLine("WIDTH 1");
            sb.AppendLine("HEIGHT " + temp.PointCount);
            sb.AppendLine("VIEWPOINT 0 0 0 1 0 0 0");
            sb.AppendLine("POINTS " + temp.PointCount);
            sb.Append("DATA ascii");
            return sb.ToString();
        }

        public string FormatLine(int id, Tuple<float, float, float> xyz, Tuple<int, int, int> rgb)
        {
            if (rgb == null)
            {
                return String.Format("\n{0} {1} {2}",
                                 xyz.Item1.ToString(Cultures.enUS),
                                 xyz.Item2.ToString(Cultures.enUS),
                                 xyz.Item3.ToString(Cultures.enUS));
            }
            else
            {
                int _rgb = ((int)rgb.Item1) << 16 | ((int)rgb.Item2) << 8 | ((int)rgb.Item3);
                return String.Format("\n{0} {1} {2} {3}",
                                     xyz.Item1.ToString(Cultures.enUS),
                                     xyz.Item2.ToString(Cultures.enUS),
                                     xyz.Item3.ToString(Cultures.enUS), _rgb);
            }
        }
    }
}
