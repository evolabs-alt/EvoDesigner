using System;
using System.Windows;
using System.Windows.Threading;

namespace EvoDesigner.UI
{
    public partial class SplashScreen : Window
    {
        private DispatcherTimer? _timer;
        private int _minimumDisplayTime = 2000; // milliseconds

        public SplashScreen()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Updates the status text on the splash screen
        /// </summary>
        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
            });
        }

        /// <summary>
        /// Shows the splash screen for a minimum amount of time
        /// </summary>
        public void ShowSplash()
        {
            Show();
            
            // Ensure splash screen shows for at least the minimum time
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_minimumDisplayTime)
            };
            _timer.Tick += (s, e) =>
            {
                _timer?.Stop();
                _timer = null;
            };
            _timer.Start();
        }

        /// <summary>
        /// Closes the splash screen (only if minimum display time has elapsed)
        /// </summary>
        public void CloseSplash()
        {
            if (_timer != null && _timer.IsEnabled)
            {
                // Wait for minimum display time to elapse
                _timer.Tick += (s, e) =>
                {
                    _timer?.Stop();
                    Dispatcher.Invoke(() => Close());
                };
            }
            else
            {
                Dispatcher.Invoke(() => Close());
            }
        }
    }
}
