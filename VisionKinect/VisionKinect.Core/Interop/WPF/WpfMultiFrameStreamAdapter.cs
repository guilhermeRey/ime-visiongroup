using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionKinect.Core.Device;
using VisionKinect.Core.Interop.Core;

namespace VisionKinect.Core.Interop.WPF
{
    public class WpfMultiFrameStreamAdapter
    {
        public IDevice Sensor { get; private set; }
        public MultiSourceStreamable StreamSource { get; private set; }

        private MultiSourceFrameReader MultiFrameSourceReader;

        public delegate void FrameArrivedHandler(object sender, object e);

        public event FrameArrivedHandler FrameArrived;

        protected virtual void OnFrameArrived(object e)
        {
            if (FrameArrived != null)
                FrameArrived(this, e);
        }

        public WpfMultiFrameStreamAdapter(MultiSourceStreamable window)
        {
            this.StreamSource = window;
            this.Sensor = this.StreamSource.Sensor;
        }

        public void Init()
        {
            this.MultiFrameSourceReader = (MultiSourceFrameReader) this.Sensor.OpenMultiFrame();
            this.MultiFrameSourceReader.MultiSourceFrameArrived += MultiFrameSourceReader_MultiSourceFrameArrived;
        }

        void MultiFrameSourceReader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            OnFrameArrived(e);
        }
    }
}
