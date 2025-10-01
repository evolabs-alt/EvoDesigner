using System.Windows;
using System.Windows.Controls;

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
        
        // Margin properties
        public double NewMarginLeft { get; private set; }
        public double NewMarginRight { get; private set; }
        public double NewMarginTop { get; private set; }
        public double NewMarginBottom { get; private set; }
        
        private double _currentWidth;
        private double _currentHeight;
        
        public LabelResizeWindow(double currentWidth, double currentHeight, double marginLeft = 2, double marginRight = 2, double marginTop = 2, double marginBottom = 2)
        {
            InitializeComponent();
            
            _currentWidth = currentWidth;
            _currentHeight = currentHeight;
            
            // Pre-populate with current dimensions
            WidthTextBox.Text = currentWidth.ToString("F1");
            HeightTextBox.Text = currentHeight.ToString("F1");
            
            // Pre-populate with current margins
            MarginLeftBox.Text = marginLeft.ToString("F1");
            MarginRightBox.Text = marginRight.ToString("F1");
            MarginTopBox.Text = marginTop.ToString("F1");
            MarginBottomBox.Text = marginBottom.ToString("F1");
            
            // Set preset combo to "Custom Size" initially
            PresetSizeComboBox.SelectedIndex = 0;
            
            // Update preview text
            UpdatePreviewText();
            
            // Add event handlers for real-time preview
            WidthTextBox.TextChanged += OnDimensionChanged;
            HeightTextBox.TextChanged += OnDimensionChanged;
        }
        
        private void PresetSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetSizeComboBox.SelectedItem is ComboBoxItem selectedItem && !string.IsNullOrEmpty(selectedItem.Tag?.ToString()))
            {
                var dimensions = selectedItem.Tag.ToString().Split(',');
                if (dimensions.Length == 2)
                {
                    if (double.TryParse(dimensions[0], out double width) && double.TryParse(dimensions[1], out double height))
                    {
                        WidthTextBox.Text = width.ToString("F1");
                        HeightTextBox.Text = height.ToString("F1");
                    }
                }
            }
        }
        
        private void OnDimensionChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreviewText();
        }
        
        private void UpdatePreviewText()
        {
            if (PreviewText == null) return;
            
            if (double.TryParse(WidthTextBox.Text, out double newWidth) && 
                double.TryParse(HeightTextBox.Text, out double newHeight))
            {
                double widthChange = ((newWidth - _currentWidth) / _currentWidth) * 100;
                double heightChange = ((newHeight - _currentHeight) / _currentHeight) * 100;
                
                string changeText = "";
                if (Math.Abs(widthChange) > 0.1 || Math.Abs(heightChange) > 0.1)
                {
                    changeText = $" (Width: {widthChange:+0.0;-0.0;±0.0}%, Height: {heightChange:+0.0;-0.0;±0.0}%)";
                }
                
                PreviewText.Text = $"New size: {newWidth:F1} × {newHeight:F1} mm{changeText}";
            }
            else
            {
                PreviewText.Text = $"Current size: {_currentWidth:F1} × {_currentHeight:F1} mm";
            }
        }
        
        private void ResizeConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Parse the input values
            if (double.TryParse(WidthTextBox.Text, out double width) && 
                double.TryParse(HeightTextBox.Text, out double height) &&
                double.TryParse(MarginLeftBox.Text, out double marginLeft) &&
                double.TryParse(MarginRightBox.Text, out double marginRight) &&
                double.TryParse(MarginTopBox.Text, out double marginTop) &&
                double.TryParse(MarginBottomBox.Text, out double marginBottom))
            {
                if (width <= 0 || height <= 0)
                {
                    MessageBox.Show("Width and height must be greater than zero.", 
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (marginLeft < 0 || marginRight < 0 || marginTop < 0 || marginBottom < 0)
                {
                    MessageBox.Show("Margins cannot be negative.", 
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                NewWidth = width;
                NewHeight = height;
                NewMarginLeft = marginLeft;
                NewMarginRight = marginRight;
                NewMarginTop = marginTop;
                NewMarginBottom = marginBottom;
                
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
                MessageBox.Show("Please enter valid numeric values for all fields.", 
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
