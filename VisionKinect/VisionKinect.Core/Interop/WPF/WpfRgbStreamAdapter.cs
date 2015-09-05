using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisionKinect.Core.Interop.Core;
namespace VisionKinect.Core.Device.Adapters
{
    public class WpfRgbStreamAdapter : SensorStream
    {
        public IDevice Sensor { get; private set; }
        public RGBStreamable Window { get; private set; }

        ColorFrameReader ColorReader;
        FrameDescription ColorFrameDescription;

        public WriteableBitmap ColorImage { get; private set; }

        public delegate void FrameArrivedHandler(object sender, object e);

        public event FrameArrivedHandler FrameArrived;

        protected virtual void OnFrameArrived(object e)
        {
            if (FrameArrived != null)
                FrameArrived(this, e);
        }

        public WpfRgbStreamAdapter(IDevice device)
        {
            this.Sensor = device;
        }

        public WpfRgbStreamAdapter(RGBStreamable window)
        {
            this.Window = window;
            this.Sensor = window.Sensor;
            this.Sensor.Initialize();
        }

        private KinectSensor Kinect
        {
            get
            {
                return (KinectSensor)this.Sensor.GetSensor();
            }
        }

        public void Init()
        {
            this.ColorReader = (ColorFrameReader) this.Sensor.OpenRGB();
            this.ColorReader.FrameArrived += ColorReader_FrameArrived;
            this.ColorFrameDescription = this.Kinect.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
            
            this.ColorImage = new WriteableBitmap(this.ColorFrameDescription.Width, this.ColorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            Window.SetColorDisplaySource(this.ColorImage);
            this.Sensor.Open();
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (ColorFrame frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    FrameDescription colorFrameDescription = frame.FrameDescription;

                    using (KinectBuffer colorBuffer = frame.LockRawImageBuffer())
                    {
                        this.ColorImage.Lock();

                        if ((colorFrameDescription.Width == this.ColorImage.PixelWidth) && (colorFrameDescription.Height == this.ColorImage.PixelHeight))
                        {
                            frame.CopyConvertedFrameDataToIntPtr(
                                this.ColorImage.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.ColorImage.AddDirtyRect(new Int32Rect(0, 0, this.ColorImage.PixelWidth, this.ColorImage.PixelHeight));
                        }

                        OnFrameArrived(e);

                        this.ColorImage.Unlock();
                    }
                }
            }
        }
    }
}
