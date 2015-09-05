using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VisionKinect.Core.Device
{
    public class KinectV2 : IDevice
    {
        KinectSensor Sensor;

        public void Initialize()
        {
            if (this.Sensor == null)
                this.Sensor = KinectSensor.GetDefault();
        }

        public void Dismiss()
        {
            if (this.Sensor.IsOpen)
                this.Sensor.Close();
        }

        public bool IsActive
        {
            get { return !(this.Sensor == null) || this.Sensor.IsAvailable && this.Sensor.IsOpen; }
        }

        public object GetSensor()
        {
            return this.Sensor;
        }

        object IDevice.OpenRGB()
        {
            if (this.Sensor == null || !this.IsActive)
                throw new InvalidOperationException("You cannot perform OpenRGB without activating the device!");

            return this.Sensor.ColorFrameSource.OpenReader();
        }

        public void Open()
        {
            this.Sensor.Open();
        }


        public object OpenMultiFrame()
        {
            return this.Sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);
        }
    }
}
