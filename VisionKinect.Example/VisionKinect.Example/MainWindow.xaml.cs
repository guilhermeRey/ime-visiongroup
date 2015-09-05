using System;
using System.Collections.Generic;
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
using VisionKinect.Core.Interop.Core;

namespace VisionKinect.Example
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, RGBStreamable // Any StreamAdapter should have a common type
    {
        // Sensor that will be used. The ideia is that we can change this anytime.
        IDevice Sensor;
        // An adapter to use for RGB streaming view
        WpfRgbStreamAdapter RgbStreamAdapter;
        // The binding used for rgb stream display
        public ImageSource ImageSource { get; set; }

        public MainWindow()
        {
            this.Sensor = new KinectV2();

            this.RgbStreamAdapter = new WpfRgbStreamAdapter(this);
            this.RgbStreamAdapter.Init();

            this.DataContext = this;
            InitializeComponent();
        }

        IDevice RGBStreamable.Sensor
        {
            get { return this.Sensor; }
        }

        void RGBStreamable.SetColorDisplaySource(object image)
        {
            this.ImageSource = (WriteableBitmap)image;
        }
    }
}
