﻿using Microsoft.Kinect;
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

        #region Depth Stuff
        private const int MapDepthToByte = 8000 / 256;
        DepthFrameReader depthFrameReader = null;
        FrameDescription depthFrameDescription = null;
        WriteableBitmap depthBitmap = null;
        byte[] depthPixels = null;
        #endregion

        #region Recorder Stuff
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
        #endregion

        public ImageSource ImageSource { get; set; }
        public ImageSource DepthImageSource { get { return this.depthBitmap; } }

        public MainWindow()
        {
            this.Sensor = new KinectV2();

            #region Initializing Stream Adapters
            this.RgbStreamAdapter = new WpfRgbStreamAdapter(this);
            this.RgbStreamAdapter.Init();

            this.MultiFrameStreamAdapter = new WpfMultiFrameStreamAdapter(this);
            this.MultiFrameStreamAdapter.FrameArrived += MultiFrameStreamAdapter_FrameArrived;
            this.MultiFrameStreamAdapter.Init();

            this.depthFrameReader = this.Kinect.DepthFrameSource.OpenReader();
            this.depthFrameReader.FrameArrived += depthFrameReader_FrameArrived;
            this.depthFrameDescription = this.Kinect.DepthFrameSource.FrameDescription;
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            
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

            #region Recorder
            this.Recorder = new PointCloudRecorder(ConfigurationManager.AppSettings["RecordFolder"], new PCL());
            this.Recorder.RecordRGB = false;
            this.Recorder.StateChanged += Recorder_StateChanged;
            this.Recorder.Stopped += Recorder_Stopped;
            this.Recorder.CloudProcessed += Recorder_CloudProcessed;
            #endregion

            this.DataContext = this;
            InitializeComponent();
        }

        #region Depth Rendering
        void depthFrameReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
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
            Application.Current.Dispatcher.Invoke(new Action(() => {
                this.LblState.Content = e.ToString();
            }));
        }
        #endregion

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

                if (multiSourceFrameProcessed && depthFrameProcessed)
                {
                    if (colorFrameProcessed)
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
                        colorPoints = this.colorPoints != null ? (ColorSpacePoint[]) this.colorPoints.Clone() : null,
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

        private void btnDepth_Click(object sender, RoutedEventArgs e)
        {
            this.RgbImage.Visibility = System.Windows.Visibility.Collapsed;
            this.DepthImage.Visibility = System.Windows.Visibility.Visible;
        }
        #endregion

        private void btnRgb_Click(object sender, RoutedEventArgs e)
        {
            this.RgbImage.Visibility = System.Windows.Visibility.Visible;
            this.DepthImage.Visibility = System.Windows.Visibility.Collapsed;
        }
    }
}
