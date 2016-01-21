using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading;
using Windows.Devices.Sensors;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CoffeeTimer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        // make this a dependency property so that we can data bind it to the XAML controls 
        public static readonly DependencyProperty NumberOfSecondsProperty = 
            DependencyProperty.Register(
                "NumberOfSeconds",
                typeof(uint),
                typeof(MainPage),
                new PropertyMetadata((uint)0)
                );

        public uint NumberOfSeconds
        {
            get { return (uint)GetValue(MainPage.NumberOfSecondsProperty); }
            set { SetValue(MainPage.NumberOfSecondsProperty, value); }
        }

        // this type of timer runs on the UI thread
        DispatcherTimer SecondTimer;
        DispatcherTimer PumpOffTimer;

        // use the accelerometer to detect if the brew pump is on
        Accelerometer Accel;

        public MainPage()
        {
            this.InitializeComponent();
            // initialize but don't start our 1 second timer
            this.SecondTimer = new DispatcherTimer();
            this.SecondTimer.Interval = TimeSpan.FromSeconds(1);
            this.SecondTimer.Tick += SecondTimer_Tick;

            // initliaze but don't start our pump off detection - use 1 second
            this.PumpOffTimer = new DispatcherTimer();
            this.PumpOffTimer.Interval = TimeSpan.FromSeconds(1);
            this.PumpOffTimer.Tick += PumpOffTimer_Tick;

            // see if we have an accelerometer - it will return null if there isn't one
            this.Accel = Accelerometer.GetDefault();
            if (this.Accel != null)
            {
                // use the default interval
                this.Accel.ReportInterval = 0;
                // register for change events
                this.Accel.ReadingChanged += Accel_ReadingChanged;
            }
        }

        private void PumpOffTimer_Tick(object sender, object e)
        {
            // the pump just turned off, stop the timer
            //
            this.StopButton_Click(null, null);
            // and stop the timer
            //
            this.PumpOffTimer.Stop();
        }

        private struct SavedXYZ
        {
            public double d;
            public SavedXYZ(double d)
            {
                this.d = d;
            }
        }
        private struct SavedTuple
        {
            public static double DeltaD = 0.005;
            public SavedXYZ x;
            public SavedXYZ y;
            public SavedXYZ z;
            public string s;
            public SavedTuple(double x, double y, double z)
            {
                this.x = new SavedXYZ(x);
                this.y = new SavedXYZ(y);
                this.z = new SavedXYZ(z);
                this.s = String.Format("{0,6:0.000} {1,6:0.000} {2,6:0.000}", x, y, z);
            }
            public bool PumpMoved(SavedTuple CurrentTuple, out SavedTuple DeltaTuple)
            {
                DeltaTuple = new SavedTuple(
                    Math.Abs(this.x.d - CurrentTuple.x.d),
                    Math.Abs(this.y.d - CurrentTuple.y.d),
                    Math.Abs(this.z.d - CurrentTuple.z.d)
                    );

                if (DeltaTuple.x.d >= SavedTuple.DeltaD ||
                    DeltaTuple.y.d >= SavedTuple.DeltaD ||
                    DeltaTuple.z.d >= SavedTuple.DeltaD)
                {
                    return true;
                }
                return false;
            }
        }
        private SavedTuple LastTuple;
        private async void Accel_ReadingChanged(object sender, AccelerometerReadingChangedEventArgs e)
        {
            // get ourselves back to UI thread
            //
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // sweet, now we can modify the UI elements
                //

                // fetch the accelerometer settings
                //
                AccelerometerReading reading = e.Reading;
                SavedTuple CurrentTuple = new SavedTuple(reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ);
                SavedTuple DeltaTuple = new SavedTuple(0,0,0);

                // did the pump just move?
                //
                bool PumpMoved = LastTuple.PumpMoved(CurrentTuple, out DeltaTuple);

                // is the clock going?
                //
                if (this.SecondTimer.IsEnabled)
                {
                    // and the pump is still going?
                    //
                    if (PumpMoved)
                    {
                        // make sure we aren't going to stop things
                        //
                        if (this.PumpOffTimer.IsEnabled)
                        {
                            this.PumpOffTimer.Stop();
                        }
                    }
                    else
                    {
                        // the pump just stopped for a bit, start watching to see if it stays stopped
                        //
                        if (this.PumpOffTimer.IsEnabled == false)
                        {
                            this.PumpOffTimer.Start();
                        }
                    }                       
                }
                // update things
                //
                this.TupleText.Text = CurrentTuple.s;
                this.DeltaText.Text = DeltaTuple.s;
                LastTuple = CurrentTuple;
            });
        }

        private void SecondTimer_Tick(object sender, object e)
        {
            this.NumberOfSeconds += 1;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            this.NumberOfSeconds = 0;
            this.SecondTimer.Start();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.SecondTimer.Stop();
        }

        private void PlusButton_Click(object sender, RoutedEventArgs e)
        {
            // get the current number
            int n = Int32.Parse(this.NumberButton.Content.ToString());
            // increment
            n += 1;
            // set it
            this.NumberButton.Content = n.ToString();
            SavedTuple.DeltaD = n * 0.001;    
        }

        private void MinusButton_Click(object sender, RoutedEventArgs e)
        {
            // get the current number
            int n = Int32.Parse(this.NumberButton.Content.ToString());
            // decrement
            if (n > 0)
            {
                n -= 1;
            }
            // set it
            this.NumberButton.Content = n.ToString();
            SavedTuple.DeltaD = n * 0.001;
        }

        private void textBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // and now navigate to our test page
            this.Frame.Navigate(typeof(BlankPage1));
        }
    }
}
