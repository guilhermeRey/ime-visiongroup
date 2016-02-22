using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VisionKinect.Core.Device;
using VisionKinect.Core.PointCloud.IO;
using VisionKinect.Core.PointCloud.Recorder;

namespace VisionKinect.Recorder
{
    /// <summary>
    /// Interaction logic for Recorder.xaml
    /// </summary>
    public partial class DepthRecorder : Window
    {
        IDevice Sensor;
        public KinectSensor Kinect { get { return (KinectSensor)this.Sensor.GetSensor(); } }

        private const int MapDepthToByte = 8000 / 256;

        DepthFrameReader depthFrameReader = null;

        FrameDescription depthFrameDescription = null;

        WriteableBitmap depthBitmap = null;

        byte[] depthPixels = null;

        PointCloudRecorder Recorder;
        private const int step = 1;
        /// <summary>
        /// Intermediate storage for receiving depth frame data from the sensor
        /// </summary>
        private ushort[] depthFrameData = null;
        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        /// <summary>
        /// Intermediate storage for receiving color frame data from the sensor
        /// </summary>
        private byte[] colorFrameData = null;
        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;
        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorSpacePoint[] colorPoints = null;
        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private CameraSpacePoint[] cameraPoints = null;

        public ImageSource DepthImageSource { get { return this.depthBitmap; } }

        public DepthRecorder()
        {
            InitializeComponent();

            this.Sensor = new KinectV2();
            this.Sensor.Initialize();

            this.depthFrameReader = this.Kinect.DepthFrameSource.OpenReader();
            this.depthFrameReader.FrameArrived += depthFrameReader_FrameArrived;
            this.depthFrameDescription = this.Kinect.DepthFrameSource.FrameDescription;
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            #region Coordinate Mapper Configuration
            this.coordinateMapper = this.Kinect.CoordinateMapper;
            FrameDescription depthFrameDescription = this.Kinect.DepthFrameSource.FrameDescription;

            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;

            // allocate space to put the pixels being received and converted
            this.depthFrameData = new ushort[depthWidth * depthHeight];
            this.colorPoints = new ColorSpacePoint[depthWidth * depthHeight];
            this.cameraPoints = new CameraSpacePoint[depthWidth * depthHeight];
            // get FrameDescription from ColorFrameSource
            FrameDescription colorFrameDescription = this.Kinect.ColorFrameSource.FrameDescription;

            int colorWidth = colorFrameDescription.Width;
            int colorHeight = colorFrameDescription.Height;

            // allocate space to put the pixels being received
            this.colorFrameData = new byte[colorWidth * colorHeight * this.bytesPerPixel];
            #endregion

            #region Recorder
            this.Recorder = new PointCloudRecorder(ConfigurationManager.AppSettings["RecordFolder"], new PCL());
            this.Recorder.RecordRGB = false;
            this.Recorder.StateChanged += Recorder_StateChanged;
            this.Recorder.Stopped += Recorder_Stopped;
            this.Recorder.CloudProcessed += Recorder_CloudProcessed;
            #endregion

            this.Sensor.Open();
            this.DataContext = this;
        }

        #region Depth Rendering
        void depthFrameReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            ushort maxDepth = ushort.MaxValue;
                            //maxDepth = depthFrame.DepthMaxReliableDistance
                            
                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                if (this.Recorder.RecorderState == PointCloudRecorderState.Recording)
                {
                    int id = this.Recorder.GetId();
                    this.coordinateMapper.MapDepthFrameToCameraSpace(this.depthFrameData, this.cameraPoints);

                    this.Recorder.AddCloud(new PointCloudTemp()
                    {
                        Id = id,
                        DepthHeight = this.depthFrameDescription.Height,
                        DepthWidth = this.depthFrameDescription.Width,
                        cameraPoints = (CameraSpacePoint[])this.cameraPoints.Clone(),
                        depthFrameData = (ushort[])this.depthFrameData.Clone()
                    });
                }

                this.RenderDepthPixels();
            }
        }

        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];
                this.depthFrameData[i] = depth;
                
                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }
        #endregion

        #region Recorder
        void Recorder_CloudProcessed(object sender, string e)
        {
            this.LblCloudMessage.Content = e;
        }

        void Recorder_Stopped(object sender, PointCloudRecorder e)
        {
            System.Windows.MessageBox.Show("Recorded :)", "Yay!");
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                this.LblCloudMessage.Content = "";
            }));

            this.Recorder = new PointCloudRecorder(ConfigurationManager.AppSettings["RecordFolder"], new PCL());
            this.Recorder.StateChanged += Recorder_StateChanged;
            this.Recorder.Stopped += Recorder_Stopped;
            this.Recorder.CloudProcessed += Recorder_CloudProcessed;

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                this.BtnRecord.IsEnabled = true;
            }));
        }

        void Recorder_StateChanged(object sender, PointCloudRecorderState e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                this.LblState.Content = e.ToString();
            }));
        }
        #endregion

        #region UI Events
        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            this.Recorder.Record();

            this.BtnRecord.IsEnabled = false;
            this.BtnStop.IsEnabled = true;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            this.Recorder.Stop();

            this.BtnStop.IsEnabled = false;
        }

        private void checkRGB_Checked(object sender, RoutedEventArgs e)
        {
            if (this.Recorder.RecorderState == PointCloudRecorderState.Idle)
                this.Recorder.RecordRGB = checkRGB.IsChecked.Value;
            else
                System.Windows.MessageBox.Show("Cannot change this in state " + this.Recorder.RecorderState.ToString(), "Info");
        }
        #endregion
    }
}
