using System.Windows;

namespace EvoDesigner.UI
{
    public enum LabelResizeMode
    {
        ResizeElements,
        RepositionElements,
        CanvasOnly
    }
    
    public partial class LabelResizeWindow : Window
    {
        public double NewWidth { get; private set; }
        public double NewHeight { get; private set; }
        public LabelResizeMode SelectedResizeMode { get; private set; }
        
        public LabelResizeWindow(double currentWidth, double currentHeight)
        {
            InitializeComponent();
            
            // Pre-populate with current dimensions
            WidthTextBox.Text = currentWidth.ToString("F1");
            HeightTextBox.Text = currentHeight.ToString("F1");
        }
        
        private void ResizeConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Parse the input values
            if (double.TryParse(WidthTextBox.Text, out double width) && 
                double.TryParse(HeightTextBox.Text, out double height))
            {
                NewWidth = width;
                NewHeight = height;
                
                // Determine the selected resize mode
                if (ResizeElementsRadio.IsChecked == true)
                    SelectedResizeMode = LabelResizeMode.ResizeElements;
                else if (RepositionElementsRadio.IsChecked == true)
                    SelectedResizeMode = LabelResizeMode.RepositionElements;
                else
                    SelectedResizeMode = LabelResizeMode.CanvasOnly;
                
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter valid numeric values for width and height.", 
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
