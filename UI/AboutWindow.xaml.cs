using System.Diagnostics;
using System.Windows;

namespace EvoDesigner.UI
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }
        
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        // Handle website link clicks
        private void Website_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.evolabs.com") { UseShellExecute = true });
            }
            catch
            {
                // Silently fail if unable to open browser
            }
        }
        
        // Handle email link clicks  
        private void Email_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("mailto:support@evolabs.com") { UseShellExecute = true });
            }
            catch
            {
                // Silently fail if unable to open email client
            }
        }
    }
}
