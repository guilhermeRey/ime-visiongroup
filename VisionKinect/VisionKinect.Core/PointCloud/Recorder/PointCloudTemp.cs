using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VisionKinect.Core.PointCloud.Recorder
{
    public class PointCloudTemp
    {
        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        public ColorSpacePoint[] colorPoints { get; set; }
        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        public CameraSpacePoint[] cameraPoints { get; set; }
        /// <summary>
        /// Intermediate storage for receiving depth frame data from the sensor
        /// </summary>
        public ushort[] depthFrameData { get; set; }
        /// <summary>
        /// Intermediate storage for receiving color frame data from the sensor
        /// </summary>
        public byte[] colorFrameData { get; set; }

        public int DepthWidth { get; set; }

        public int DepthHeight { get; set; }

        public int ColorWidth { get; set; }

        public int ColorHeight { get; set; }

        public int Id { get; set; }

        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        public Tuple<float, float, float> XYZ { get; set; }

        public Tuple<int, int, int> RGB { get; set; }

        public int PointCount { get; set; }

        public IEnumerable<PointCloudTemp> ProcessCloud()
        {
            int pointsFoundInFrame = 0;
            int step = 1;
            for (int y = 0; y < DepthHeight; y += step)
            {
                for (int x = 0; x < DepthWidth; x += step)
                {
                    // calculate index into depth array
                    int depthIndex = (y * DepthWidth) + x;

                    CameraSpacePoint p = this.cameraPoints[depthIndex];
                    ColorSpacePoint colorPoint = this.colorPoints[depthIndex];

                    byte r = 0; byte g = 0; byte b = 0;

                    int colorX = (int)Math.Floor(colorPoint.X + 0.5);
                    int colorY = (int)Math.Floor(colorPoint.Y + 0.5);
                    if ((colorX >= 0) && (colorX < ColorWidth) && (colorY >= 0) && (colorY < ColorHeight))
                    {
                        int colorIndex = ((colorY * ColorWidth) + colorX) * this.bytesPerPixel;
                        int displayIndex = depthIndex * this.bytesPerPixel;

                        b = this.colorFrameData[colorIndex++];
                        g = this.colorFrameData[colorIndex++];
                        r = this.colorFrameData[colorIndex++];
                    }

                    if (!(Double.IsInfinity(p.X)) && !(Double.IsInfinity(p.Y)) && !(Double.IsInfinity(p.Z)))
                    {
                        this.XYZ = new Tuple<float, float, float>(p.X, p.Y, p.Z);
                        this.RGB = new Tuple<int, int, int>(r, g, b);
                        pointsFoundInFrame++;

                        yield return this;
                    };
                }
            }
            this.PointCount = pointsFoundInFrame;
            // Debug.WriteLine("Frame " + Id + ", points: " + pointsFoundInFrame);
        }
    }
}
