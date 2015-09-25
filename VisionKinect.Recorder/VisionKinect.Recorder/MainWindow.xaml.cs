using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VisionKinect.Core.Device;
using VisionKinect.Core.Device.Adapters;
using VisionKinect.Core.Interop;
using VisionKinect.Core.Interop.Core;
using VisionKinect.Core.Interop.WPF;
using VisionKinect.Core.IO.FileTypes;
using VisionKinect.Core.IO.Stream;
using VisionKinect.Core.PointCloud.IO;
using VisionKinect.Core.PointCloud.Recorder;

namespace VisionKinect.Recorder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, RGBStreamable, MultiSourceStreamable
    {
        IDevice Sensor;
        public KinectSensor Kinect { get { return (KinectSensor)this.Sensor.GetSensor(); } }

        WpfRgbStreamAdapter RgbStreamAdapter;
        WpfMultiFrameStreamAdapter MultiFrameStreamAdapter;

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

        public ImageSource ImageSource { get; set; }

        public MainWindow()
        {
            this.Sensor = new KinectV2();

            #region Initializing Stream Adapters
            this.RgbStreamAdapter = new WpfRgbStreamAdapter(this);
            this.RgbStreamAdapter.Init();

            this.MultiFrameStreamAdapter = new WpfMultiFrameStreamAdapter(this);
            this.MultiFrameStreamAdapter.FrameArrived += MultiFrameStreamAdapter_FrameArrived;
            this.MultiFrameStreamAdapter.Init();
            #endregion

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

            this.Recorder = new PointCloudRecorder(ConfigurationManager.AppSettings["RecordFolder"], new PCL());
            this.Recorder.RecordRGB = false;
            this.Recorder.StateChanged += Recorder_StateChanged;
            this.Recorder.Stopped += Recorder_Stopped;
            this.Recorder.CloudProcessed += Recorder_CloudProcessed;

            this.DataContext = this;
            InitializeComponent();
        }

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
            Application.Current.Dispatcher.Invoke(new Action(() => {
                this.LblState.Content = e.ToString();
            }));
        }

        #region RGB Stream
        IDevice RGBStreamable.Sensor
        {
            get { return this.Sensor; }
        }

        void RGBStreamable.SetColorDisplaySource(object image)
        {
            this.ImageSource = (WriteableBitmap) image;
        }
        #endregion

        #region Multi Frame Stream
        void MultiFrameStreamAdapter_FrameArrived(object sender, object e)
        {
            if (this.Recorder.RecorderState == PointCloudRecorderState.Recording)
            {
                MultiSourceFrame multiSourceFrame = ((MultiSourceFrameArrivedEventArgs)e).FrameReference.AcquireFrame();

                int depthWidth = 0, depthHeight = 0;
                int colorWidth = 0, colorHeight = 0;

                bool multiSourceFrameProcessed = false;
                bool colorFrameProcessed = false;
                bool depthFrameProcessed = false;

                int id = this.Recorder.GetId();

                if (multiSourceFrame != null)
                {
                    #region Acquiring frames
                    using (DepthFrame depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
                    {
                        using (ColorFrame colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
                        {
                            using (BodyIndexFrame bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame())
                            {
                                if (depthFrame != null)
                                {
                                    FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                                    depthWidth = depthFrameDescription.Width;
                                    depthHeight = depthFrameDescription.Height;

                                    if ((depthWidth * depthHeight) == this.depthFrameData.Length)
                                    {
                                        depthFrame.CopyFrameDataToArray(this.depthFrameData);
                                        depthFrameProcessed = true;
                                    }
                                }

                                if (colorFrame != null)
                                {
                                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;
                                    colorWidth = colorFrameDescription.Width;
                                    colorHeight = colorFrameDescription.Height;

                                    if ((colorWidth * colorHeight * this.bytesPerPixel) == this.colorFrameData.Length)
                                    {
                                        if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                                        {
                                            colorFrame.CopyRawFrameDataToArray(this.colorFrameData);
                                        }
                                        else
                                        {
                                            colorFrame.CopyConvertedFrameDataToArray(this.colorFrameData, ColorImageFormat.Bgra);
                                        }

                                        colorFrameProcessed = true;
                                    }
                                }

                                multiSourceFrameProcessed = true;
                            }
                        }
                    }
                    #endregion
                }

                if (multiSourceFrameProcessed && depthFrameProcessed && colorFrameProcessed)
                {
                    this.coordinateMapper.MapDepthFrameToColorSpace(this.depthFrameData, this.colorPoints);
                    this.coordinateMapper.MapDepthFrameToCameraSpace(this.depthFrameData, this.cameraPoints);
                    this.Recorder.AddCloud(new PointCloudTemp()
                    {
                        Id = id,
                        ColorHeight = colorHeight,
                        ColorWidth = colorWidth,
                        DepthHeight = depthHeight,
                        DepthWidth = depthWidth,
                        cameraPoints = (CameraSpacePoint[]) this.cameraPoints.Clone(),
                        colorPoints = (ColorSpacePoint[]) this.colorPoints.Clone(),
                        depthFrameData = (ushort[]) this.depthFrameData.Clone(),
                        colorFrameData = (byte[]) colorFrameData.Clone()
                    });
                }
            }
        }

        IDevice MultiSourceStreamable.Sensor
        {
            get { return this.Sensor; }
        }
        #endregion

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
    }
}
