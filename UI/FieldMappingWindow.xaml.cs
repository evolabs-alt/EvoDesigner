using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using EvoDesigner.Models;
using System.Data;
using System.Linq;

namespace EvoDesigner.UI
{
    public partial class FieldMappingWindow : Window
    {
        public List<UIElement> LabelElements { get; set; }
        public DataTable ResultTable { get; set; }
        public List<FieldMapping> Mappings { get; set; } = new();
        
        private List<ComboBox> fieldComboBoxes = new();

        public FieldMappingWindow(List<UIElement> labelElements, DataTable resultTable)
        {
            InitializeComponent();
            LabelElements = labelElements;
            ResultTable = resultTable;
            
            CreateMappingInterface();
        }

        private void CreateMappingInterface()
        {
            MappingPanel.Children.Clear();
            fieldComboBoxes.Clear();

            // Create field options list (None + all database fields)
            var fieldOptions = new List<string> { "[None - Keep Current Text]" };
            foreach (DataColumn column in ResultTable.Columns)
            {
                fieldOptions.Add($"{column.ColumnName} ({column.DataType.Name})");
            }

            // Create mapping controls for each label element
            for (int i = 0; i < LabelElements.Count; i++)
            {
                var element = LabelElements[i];
                string elementDescription = GetElementDescription(element, i);

                // Create a border for each mapping row
                var border = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(10)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

                // Element description
                var elementLabel = new TextBlock
                {
                    Text = elementDescription,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(elementLabel, 0);

                // Field selection combobox
                var fieldComboBox = new ComboBox
                {
                    ItemsSource = fieldOptions,
                    SelectedIndex = 0, // Default to "[None]"
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = i // Store element index
                };
                Grid.SetColumn(fieldComboBox, 1);
                fieldComboBoxes.Add(fieldComboBox);

                grid.Children.Add(elementLabel);
                grid.Children.Add(fieldComboBox);
                border.Child = grid;
                MappingPanel.Children.Add(border);
            }
        }

        private string GetElementDescription(UIElement element, int index)
        {
            string elementType = "Unknown";
            string currentContent = "";

            if (element is TextBlock tb)
            {
                if (tb.Text.StartsWith("[") && tb.Text.EndsWith("]"))
                {
                    elementType = tb.Text; // [EAN-13], [QR Code], etc.
                }
                else
                {
                    elementType = "Text";
                    currentContent = $" (Current: \"{tb.Text}\")";
                }
            }
            else if (element is Image imageElement)
            {
                if (imageElement.Tag?.ToString() == "QRCode" || imageElement.Tag?.ToString()?.StartsWith("QRCodeData:") == true)
                {
                    elementType = "[QR Code]";
                    if (imageElement.Tag?.ToString()?.StartsWith("QRCodeData:") == true)
                        currentContent = $" (Current: \"{imageElement.Tag.ToString().Substring("QRCodeData:".Length)}\")";
                }
                else
                {
                    elementType = "Image";
                    currentContent = imageElement.Tag != null ? $" (Source: {imageElement.Tag})" : "";
                }
            }
            else if (element is System.Windows.Shapes.Rectangle)
            {
                elementType = "Image";
            }

            return $"Element {index + 1}: {elementType}{currentContent}";
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Build mappings from the selected fields
            Mappings.Clear();
            
            for (int i = 0; i < fieldComboBoxes.Count; i++)
            {
                var comboBox = fieldComboBoxes[i];
                var selectedIndex = comboBox.SelectedIndex;
                
                // If not "[None]" (index 0), create a mapping
                if (selectedIndex > 0)
                {
                    var fieldName = ResultTable.Columns[selectedIndex - 1].ColumnName; // -1 because index 0 is "[None]"
                    Mappings.Add(new FieldMapping 
                    { 
                        ElementIndex = i, 
                        FieldName = fieldName 
                    });
                }
            }
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
