using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EvoDesigner.Models;
using EvoDesigner.Services;
using System.Data;
using System.Linq;
using System.Printing;
using System.Windows.Documents;
using System.Windows.Xps;

namespace EvoDesigner.UI
{
    public partial class PrintPreviewWindow : Window
    {
        private LabelTemplate template;
        private DataTable? dataTable;
        private int currentRecordIndex = 0;

        public PrintPreviewWindow(LabelTemplate template, DataTable? dataTable = null)
        {
            InitializeComponent();
            this.template = template;
            this.dataTable = dataTable;
            
            SetupNavigation();
            RenderPreview();
        }

        private void SetupNavigation()
        {
            if (dataTable != null && dataTable.Rows.Count > 1)
            {
                NavigationPanel.Visibility = Visibility.Visible;
                UpdateNavigationButtons();
                UpdateRecordCounter();
            }
            else
            {
                NavigationPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateNavigationButtons()
        {
            if (dataTable == null) return;
            
            PrevButton.IsEnabled = currentRecordIndex > 0;
            NextButton.IsEnabled = currentRecordIndex < dataTable.Rows.Count - 1;
        }

        private void UpdateRecordCounter()
        {
            if (dataTable == null) return;
            
            RecordCounter.Text = $"Record {currentRecordIndex + 1} of {dataTable.Rows.Count}";
        }

        private void PrevRecord_Click(object sender, RoutedEventArgs e)
        {
            if (dataTable == null || currentRecordIndex <= 0) return;
            
            currentRecordIndex--;
            UpdateNavigationButtons();
            UpdateRecordCounter();
            
            // Force immediate UI update
            this.UpdateLayout();
            RenderPreview();
            this.UpdateLayout();
        }

        private void NextRecord_Click(object sender, RoutedEventArgs e)
        {
            if (dataTable == null || currentRecordIndex >= dataTable.Rows.Count - 1) return;
            
            currentRecordIndex++;
            UpdateNavigationButtons();
            UpdateRecordCounter();
            
            // Force immediate UI update
            this.UpdateLayout();
            RenderPreview();
            this.UpdateLayout();
        }

        private string GetElementContent(LabelElement element, int elementIndex)
        {
            // If no data or no field mappings, use original content
            if (dataTable == null || template.FieldMappings == null || currentRecordIndex >= dataTable.Rows.Count)
                return element.Content ?? "";
            
            // Find field mapping for this element
            var mapping = template.FieldMappings.FirstOrDefault(m => m.ElementIndex == elementIndex);
            if (mapping != null && !string.IsNullOrEmpty(mapping.FieldName) && dataTable.Columns.Contains(mapping.FieldName))
            {
                var currentRow = dataTable.Rows[currentRecordIndex];
                var fieldValue = currentRow[mapping.FieldName]?.ToString() ?? "";
                
                // Debug output to console
                System.Diagnostics.Debug.WriteLine($"Element {elementIndex}: Mapping found for field '{mapping.FieldName}', Record {currentRecordIndex}, Value: '{fieldValue}'");
                
                return !string.IsNullOrEmpty(fieldValue) ? fieldValue : element.Content ?? "";
            }
            
            return element.Content ?? "";
        }

        private void RenderPreview()
        {
            RenderPreview(template);
        }

        private void RenderPreview(LabelTemplate template)
        {
            try
            {
                // Debug output to confirm rendering is happening
                System.Diagnostics.Debug.WriteLine($"RenderPreview called for record {currentRecordIndex}");
                
                // Update label size information in header
                LabelSizeInfo.Text = $"Label Size: {template.LabelWidth}mm × {template.LabelHeight}mm";
                
                PreviewCanvas.Children.Clear();
                
                // Convert mm to screen pixels for preview
                double canvasWidthPx = MeasurementService.MmToScreenPixels(template.LabelWidth);
                double canvasHeightPx = MeasurementService.MmToScreenPixels(template.LabelHeight);
                
                // Ensure minimum canvas size
                canvasWidthPx = Math.Max(canvasWidthPx, 200);
                canvasHeightPx = Math.Max(canvasHeightPx, 100);
                
                PreviewCanvas.Width = canvasWidthPx;
                PreviewCanvas.Height = canvasHeightPx;
                
                // Add a debug border to show canvas bounds
                var debugBorder = new Rectangle
                {
                    Width = canvasWidthPx,
                    Height = canvasHeightPx,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(debugBorder, 0);
                Canvas.SetTop(debugBorder, 0);
                PreviewCanvas.Children.Add(debugBorder);
                
                int elementIndex = 0;
                foreach (var el in template.Elements)
                {
                    UIElement? uiEl = null;
                    string elementContent = GetElementContent(el, elementIndex);
                    
                    switch (el.Type)
                    {
                        case LabelElementType.Text:
                            var tb = new TextBlock 
                            { 
                                Text = elementContent, 
                                FontSize = el.FontSize,
                                Width = el.Width,
                                Height = el.Height
                            };
                            
                            // Safe font family setting
                            try 
                            {
                                if (!string.IsNullOrEmpty(el.FontFamily))
                                    tb.FontFamily = new FontFamily(el.FontFamily);
                            }
                            catch { /* Use default font family */ }
                            
                            // Safe font weight setting
                            tb.FontWeight = (el.FontWeight?.Contains("Bold") == true ? FontWeights.Bold : FontWeights.Normal);
                            tb.FontStyle = (el.FontStyle?.Contains("Italic") == true ? FontStyles.Italic : FontStyles.Normal);
                            
                            // Safe color setting
                            try 
                            {
                                if (!string.IsNullOrEmpty(el.Foreground))
                                    tb.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.Foreground));
                                else
                                    tb.Foreground = Brushes.Black;
                            }
                            catch 
                            {
                                tb.Foreground = Brushes.Black;
                            }
                            
                            uiEl = tb;
                            break;
                            
                        case LabelElementType.Image:
                            var rect = new Rectangle { Width = el.Width, Height = el.Height };
                            try 
                            {
                                // First check if we have base64 image data
                                if (!string.IsNullOrEmpty(el.ImageData))
                                {
                                    // Convert base64 to BitmapImage
                                    byte[] imageBytes = Convert.FromBase64String(el.ImageData);
                                    using (var ms = new System.IO.MemoryStream(imageBytes))
                                    {
                                        var bitmapImage = new BitmapImage();
                                        bitmapImage.BeginInit();
                                        bitmapImage.StreamSource = ms;
                                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmapImage.EndInit();
                                        
                                        var imgBrush = new ImageBrush();
                                        imgBrush.ImageSource = bitmapImage;
                                        imgBrush.Stretch = Stretch.Uniform; // Fit image within bounds, maintain aspect ratio
                                        imgBrush.AlignmentX = AlignmentX.Center;
                                        imgBrush.AlignmentY = AlignmentY.Center;
                                        rect.Fill = imgBrush;
                                    }
                                }
                                // Fallback to file path if no base64 data
                                else if (!string.IsNullOrEmpty(el.Content) && System.IO.File.Exists(el.Content))
                                {
                                    var imgBrush = new ImageBrush();
                                    imgBrush.ImageSource = new BitmapImage(new System.Uri(el.Content));
                                    imgBrush.Stretch = Stretch.Uniform; // Fit image within bounds, maintain aspect ratio
                                    imgBrush.AlignmentX = AlignmentX.Center;
                                    imgBrush.AlignmentY = AlignmentY.Center;
                                    rect.Fill = imgBrush;
                                }
                                else
                                {
                                    // No image data available - show placeholder
                                    rect.Fill = Brushes.LightGray;
                                    rect.Stroke = Brushes.DarkGray;
                                    rect.StrokeThickness = 1;
                                    
                                    // Add "No Image" text overlay
                                    var noImageText = new TextBlock
                                    {
                                        Text = "No Image",
                                        FontSize = 10,
                                        Foreground = Brushes.DarkGray,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        TextAlignment = TextAlignment.Center
                                    };
                                    Canvas.SetLeft(noImageText, el.X + el.Width / 2 - 25);
                                    Canvas.SetTop(noImageText, el.Y + el.Height / 2 - 7);
                                    PreviewCanvas.Children.Add(noImageText);
                                }
                            } 
                            catch (Exception ex)
                            { 
                                // Error loading image - show error placeholder
                                rect.Fill = Brushes.LightCoral; 
                                rect.Stroke = Brushes.Red;
                                rect.StrokeThickness = 1;
                                
                                // Add error text overlay
                                var errorText = new TextBlock
                                {
                                    Text = "Image Error",
                                    FontSize = 9,
                                    Foreground = Brushes.DarkRed,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    TextAlignment = TextAlignment.Center
                                };
                                Canvas.SetLeft(errorText, el.X + el.Width / 2 - 30);
                                Canvas.SetTop(errorText, el.Y + el.Height / 2 - 7);
                                PreviewCanvas.Children.Add(errorText);
                            }
                            uiEl = rect;
                            break;
                            
                        case LabelElementType.Barcode13:
                        case LabelElementType.Barcode8:
                        case LabelElementType.Barcode:
                            // Generate actual barcode for preview using dynamic content
                            try
                            {
                                var barcodeImg = GenerateBarcodeImage(elementContent, el.Type, el.Width, el.Height);
                                uiEl = barcodeImg;
                            }
                            catch
                            {
                                // Fallback to text representation
                                var barcodeTb = new TextBlock 
                                { 
                                    Text = $"[{(el.Type == LabelElementType.Barcode13 ? "EAN-13" : el.Type == LabelElementType.Barcode8 ? "EAN-8" : "Barcode")}]\n{elementContent}",
                                    FontSize = Math.Min(el.FontSize, 10),
                                    Width = el.Width,
                                    Height = el.Height,
                                    Foreground = Brushes.DarkBlue,
                                    TextWrapping = TextWrapping.Wrap
                                };
                                uiEl = barcodeTb;
                            }
                            break;
                            
                        case LabelElementType.QRCode:
                            // Generate actual QR code for preview using dynamic content
                            try
                            {
                                var qrImg = GenerateQRCodeImage(elementContent, el.Width, el.Height);
                                uiEl = qrImg;
                            }
                            catch
                            {
                                // Fallback to text representation
                                var qrTb = new TextBlock 
                                { 
                                    Text = $"[QR Code]\n{elementContent}",
                                    FontSize = Math.Min(el.FontSize, 10),
                                    Width = el.Width,
                                    Height = el.Height,
                                    Foreground = Brushes.DarkRed,
                                    TextWrapping = TextWrapping.Wrap
                                };
                                uiEl = qrTb;
                            }
                            break;
                    }
                    
                    if (uiEl != null)
                    {
                        Canvas.SetLeft(uiEl, el.X);
                        Canvas.SetTop(uiEl, el.Y);
                        PreviewCanvas.Children.Add(uiEl);
                    }
                    
                    elementIndex++;
                }
                
                // If no elements were added, show a helpful message
                if (elementIndex == 0)
                {
                    var noElementsText = new TextBlock
                    {
                        Text = "No elements to preview.\nPlease add text, images, or barcodes to your label design.",
                        FontSize = 14,
                        Foreground = Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    Canvas.SetLeft(noElementsText, canvasWidthPx / 2 - 150);
                    Canvas.SetTop(noElementsText, canvasHeightPx / 2 - 20);
                    PreviewCanvas.Children.Add(noElementsText);
                }
            }
            catch (System.Exception ex)
            {
                // Show error message instead of crashing
                MessageBox.Show($"Error generating print preview: {ex.Message}", "Preview Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private Image GenerateBarcodeImage(string content, LabelElementType type, double width, double height)
        {
            var writer = new ZXing.BarcodeWriterPixelData();
            writer.Format = type == LabelElementType.Barcode13 ? ZXing.BarcodeFormat.EAN_13 : ZXing.BarcodeFormat.EAN_8;
            
            var pixelData = writer.Write(content);
            var bmp = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height), 
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length);
            bmp.UnlockBits(bmpData);
            
            var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            
            return new Image 
            { 
                Source = bi, 
                Width = width, 
                Height = height,
                Stretch = Stretch.Uniform
            };
        }

        private Image GenerateQRCodeImage(string content, double width, double height)
        {
            var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCoder.QRCode(qrCodeData);
            
            using (var bitmap = qrCode.GetGraphic(10))
            {
                var ms = new System.IO.MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return new Image
                {
                    Source = bitmapImage,
                    Width = width,
                    Height = height,
                    Stretch = Stretch.Uniform
                };
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new PrintDialog();
                
                // Show printer selection dialog
                if (printDialog.ShowDialog() == true)
                {
                    // Ask user how many records to print
                    var printChoice = ShowPrintOptionsDialog();
                    if (printChoice == null) return; // User cancelled
                    
                    // Print based on user choice
                    if (printChoice.Value.printAll && dataTable != null && dataTable.Rows.Count > 1)
                    {
                        // Print all records (1 copy each)
                        var allRecordIndices = Enumerable.Range(0, dataTable.Rows.Count).ToArray();
                        PrintMultipleLabels(printDialog, allRecordIndices, 1);
                        // Removed success popup - printing completed silently
                    }
                    else
                    {
                        // Print current record with specified copies
                        PrintMultipleLabels(printDialog, new[] { currentRecordIndex }, printChoice.Value.copies);
                        // Removed success popup - printing completed silently
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Print error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (bool printAll, int copies)? ShowPrintOptionsDialog()
        {
            if (dataTable == null || dataTable.Rows.Count <= 1)
            {
                // Only one record, ask for copies
                string copiesInput = "1";
                var dialog = new Window
                {
                    Title = "Print Copies",
                    Width = 300,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var label = new Label { Content = "How many copies do you want to print?", Margin = new Thickness(10) };
                Grid.SetRow(label, 0);
                
                var textBox = new TextBox { Text = "1", Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Stretch };
                Grid.SetRow(textBox, 1);
                
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
                var okButton = new Button { Content = "OK", Width = 60, Margin = new Thickness(5, 0, 5, 0) };
                var cancelButton = new Button { Content = "Cancel", Width = 60, Margin = new Thickness(5, 0, 5, 0) };
                
                bool? dialogResult = null;
                okButton.Click += (s, e) => { copiesInput = textBox.Text; dialogResult = true; dialog.Close(); };
                cancelButton.Click += (s, e) => { dialogResult = false; dialog.Close(); };
                
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                Grid.SetRow(buttonPanel, 2);
                
                grid.Children.Add(label);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);
                dialog.Content = grid;
                
                dialog.ShowDialog();
                
                if (dialogResult != true) return null; // User cancelled
                
                if (int.TryParse(copiesInput, out int copies) && copies > 0)
                {
                    return (false, copies);
                }
                else
                {
                    MessageBox.Show("Please enter a valid number of copies.", "Invalid Input", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }
            
            // Multiple records available
            var result = MessageBox.Show(
                $"You have {dataTable.Rows.Count} records.\n\n" +
                "Click 'Yes' to print all records (one label per record)\n" +
                "Click 'No' to print only the current record\n" +
                "Click 'Cancel' to abort printing",
                "Print Options",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Cancel) return null;
            
            if (result == MessageBoxResult.Yes)
            {
                return (true, 1); // Print all records, one copy each
            }
            else
            {
                // Print current record, ask for copies using the same dialog approach
                return ShowCopiesDialog();
            }
        }

        private (bool printAll, int copies)? ShowCopiesDialog()
        {
            string copiesInput = "1";
            var dialog = new Window
            {
                Title = "Print Copies",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var label = new Label { Content = "How many copies of the current record?", Margin = new Thickness(10) };
            Grid.SetRow(label, 0);
            
            var textBox = new TextBox { Text = "1", Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Stretch };
            Grid.SetRow(textBox, 1);
            
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
            var okButton = new Button { Content = "OK", Width = 60, Margin = new Thickness(5, 0, 5, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 60, Margin = new Thickness(5, 0, 5, 0) };
            
            bool? dialogResult = null;
            okButton.Click += (s, e) => { copiesInput = textBox.Text; dialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, e) => { dialogResult = false; dialog.Close(); };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            
            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);
            dialog.Content = grid;
            
            dialog.ShowDialog();
            
            if (dialogResult != true) return null;
            
            if (int.TryParse(copiesInput, out int copies) && copies > 0)
            {
                return (false, copies);
            }
            else
            {
                MessageBox.Show("Please enter a valid number of copies.", "Invalid Input", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }

        private void PrintMultipleLabels(PrintDialog printDialog, int[] recordIndices, int copiesPerRecord)
        {
            // Use smaller scale for testing - maybe the issue is the DPI conversion
            const double MM_TO_PIXELS = 3.78; // Approximate conversion (96 DPI / 25.4 mm/inch)
            
            // Convert label size from mm to pixels using simpler conversion
            double labelWidthPx = template.LabelWidth * MM_TO_PIXELS;
            double labelHeightPx = template.LabelHeight * MM_TO_PIXELS;
            
            // For label printing, the page size should match the label size (not printer's full area)
            var pageSize = new Size(labelWidthPx, labelHeightPx);
            
            // Each page contains exactly one label
            int labelsPerRow = 1;
            
            // Create list of all labels to print (including copies)
            var allLabels = new List<int>();
            foreach (var recordIndex in recordIndices)
            {
                for (int copy = 0; copy < copiesPerRecord; copy++)
                {
                    allLabels.Add(recordIndex);
                }
            }
            
            // Create document with one page per label
            var document = new FixedDocument();
            
            // Create one page for each label
            foreach (var recordIndex in allLabels)
            {
                var page = CreatePageWithLabels(new[] { recordIndex }, 
                    labelWidthPx, labelHeightPx, pageSize, labelsPerRow);
                
                var pageContent = new PageContent();
                ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                document.Pages.Add(pageContent);
            }
            
            // Print the entire document as one job
            printDialog.PrintDocument(document.DocumentPaginator, 
                $"Labels - {allLabels.Count} label(s) on {document.Pages.Count} page(s)");
        }

        private FixedPage CreatePageWithLabels(int[] recordIndices, double labelWidthPx, double labelHeightPx, 
            Size pageSize, int labelsPerRow)
        {
            var page = new FixedPage();
            page.Width = labelWidthPx;  // Page size should match label size
            page.Height = labelHeightPx;
            
            // For single label per page, just add one label at position (0,0)
            if (recordIndices.Length > 0)
            {
                var labelCanvas = CreatePrintCanvas(recordIndices[0], labelWidthPx, labelHeightPx);
                
                // Position the label at top-left of the page
                Canvas.SetLeft(labelCanvas, 0);
                Canvas.SetTop(labelCanvas, 0);
                
                page.Children.Add(labelCanvas);
            }
            
            return page;
        }

        private Canvas CreatePrintCanvas(int recordIndex, double canvasWidthPx, double canvasHeightPx)
        {
            var printCanvas = new Canvas();
            printCanvas.Background = Brushes.White;
            printCanvas.Width = canvasWidthPx;
            printCanvas.Height = canvasHeightPx;
            
            // Calculate proper scale factor from screen to print
            // Screen measurements in template are in pixels at screen DPI (72)
            // Print measurements need to be in pixels at print DPI
            double screenToMmFactor = 1.0 / MeasurementService.PIXELS_PER_MM_SCREEN; // Convert screen pixels to mm
            double mmToPrintFactor = canvasWidthPx / template.LabelWidth; // Convert mm to print pixels
            double scaleFactor = mmToPrintFactor * screenToMmFactor;
            
            int elementIndex = 0;
            foreach (var el in template.Elements)
            {
                UIElement? uiEl = null;
                string elementContent = GetElementContentForRecord(el, elementIndex, recordIndex);
                
                switch (el.Type)
                {
                    case LabelElementType.Text:
                        // Create a completely new TextBlock for printing
                        var tb = new TextBlock 
                        { 
                            Text = elementContent, 
                            FontSize = el.FontSize * scaleFactor,
                            Width = el.Width * scaleFactor,
                            Height = el.Height * scaleFactor
                        };
                        
                        // Apply font properties
                        try 
                        {
                            if (!string.IsNullOrEmpty(el.FontFamily))
                                tb.FontFamily = new FontFamily(el.FontFamily);
                        }
                        catch { /* Use default font family */ }
                        
                        tb.FontWeight = (el.FontWeight?.Contains("Bold") == true ? FontWeights.Bold : FontWeights.Normal);
                        tb.FontStyle = (el.FontStyle?.Contains("Italic") == true ? FontStyles.Italic : FontStyles.Normal);
                        
                        try 
                        {
                            if (!string.IsNullOrEmpty(el.Foreground))
                            {
                                var brush = (new BrushConverter().ConvertFrom(el.Foreground)) as SolidColorBrush;
                                tb.Foreground = brush ?? Brushes.Black;
                            }
                            else
                                tb.Foreground = Brushes.Black;
                        }
                        catch 
                        {
                            tb.Foreground = Brushes.Black;
                        }
                        
                        uiEl = tb;
                        break;
                        
                    case LabelElementType.Image:
                        // Create a completely new Rectangle for printing
                        var rect = new Rectangle { 
                            Width = el.Width * scaleFactor, 
                            Height = el.Height * scaleFactor 
                        };
                        try 
                        {
                            if (!string.IsNullOrEmpty(elementContent) && System.IO.File.Exists(elementContent))
                            {
                                var imgBrush = new ImageBrush();
                                imgBrush.ImageSource = new BitmapImage(new System.Uri(elementContent));
                                rect.Fill = imgBrush;
                            }
                            else
                            {
                                rect.Fill = Brushes.LightGray;
                                rect.Stroke = Brushes.DarkGray;
                                rect.StrokeThickness = 1;
                            }
                        } 
                        catch 
                        { 
                            rect.Fill = Brushes.LightGray; 
                        }
                        uiEl = rect;
                        break;
                        
                    case LabelElementType.Barcode13:
                    case LabelElementType.Barcode8:
                    case LabelElementType.Barcode:
                        // Create a completely new barcode image for printing
                        try
                        {
                            var barcodeImg = GenerateBarcodeImageForPrint(elementContent, el.Type, 
                                el.Width * scaleFactor, el.Height * scaleFactor);
                            uiEl = barcodeImg;
                        }
                        catch
                        {
                            // Fallback to text if barcode generation fails
                            var barcodeTb = new TextBlock 
                            { 
                                Text = $"[Barcode: {elementContent}]",
                                FontSize = 8 * scaleFactor,
                                Width = el.Width * scaleFactor,
                                Height = el.Height * scaleFactor,
                                Foreground = Brushes.Black
                            };
                            uiEl = barcodeTb;
                        }
                        break;
                        
                    case LabelElementType.QRCode:
                        // Create a completely new QR code image for printing
                        try
                        {
                            var qrImg = GenerateQRCodeImageForPrint(elementContent, 
                                el.Width * scaleFactor, el.Height * scaleFactor);
                            uiEl = qrImg;
                        }
                        catch
                        {
                            // Fallback to text if QR generation fails
                            var qrTb = new TextBlock 
                            { 
                                Text = $"[QR: {elementContent}]",
                                FontSize = 8 * scaleFactor,
                                Width = el.Width * scaleFactor,
                                Height = el.Height * scaleFactor,
                                Foreground = Brushes.Black
                            };
                            uiEl = qrTb;
                        }
                        break;
                }
                
                if (uiEl != null)
                {
                    double printX = el.X * scaleFactor;
                    double printY = el.Y * scaleFactor;
                    
                    Canvas.SetLeft(uiEl, printX);
                    Canvas.SetTop(uiEl, printY);
                    printCanvas.Children.Add(uiEl);
                }
                
                elementIndex++;
            }
            
            return printCanvas;
        }

        // Create separate barcode generation method for printing to avoid element reuse
        private Image GenerateBarcodeImageForPrint(string content, LabelElementType type, double width, double height)
        {
            return GenerateBarcodeImage(content, type, width, height);
        }

        // Create separate QR code generation method for printing to avoid element reuse  
        private Image GenerateQRCodeImageForPrint(string content, double width, double height)
        {
            return GenerateQRCodeImage(content, width, height);
        }

        private string GetElementContentForRecord(LabelElement element, int elementIndex, int recordIndex)
        {
            // If no data or no field mappings, use original content
            if (dataTable == null || template.FieldMappings == null || recordIndex >= dataTable.Rows.Count)
                return element.Content ?? "";
            
            // Find field mapping for this element
            var mapping = template.FieldMappings.FirstOrDefault(m => m.ElementIndex == elementIndex);
            if (mapping != null && dataTable.Columns.Contains(mapping.FieldName))
            {
                var row = dataTable.Rows[recordIndex];
                return row[mapping.FieldName]?.ToString() ?? element.Content ?? "";
            }
            
            return element.Content ?? "";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
