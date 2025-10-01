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
using System;
using System.IO;

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
            
            Console.WriteLine($"[DEBUG] PrintPreviewWindow constructor called with template containing {template.Elements.Count} elements");
            
            SetupNavigation();
            RenderPreview(this.template);
        }

        private Canvas? GetPreviewCanvas()
        {
            return FindName("PreviewCanvas") as Canvas;
        }

        private StackPanel? GetNavigationPanel()
        {
            return FindName("NavigationPanel") as StackPanel;
        }

        private Button? GetPrevButton()
        {
            return FindName("PrevButton") as Button;
        }

        private Button? GetNextButton()
        {
            return FindName("NextButton") as Button;
        }

        private TextBlock? GetRecordCounter()
        {
            return FindName("RecordCounter") as TextBlock;
        }

        private TextBlock? GetLabelSizeInfo()
        {
            return FindName("LabelSizeInfo") as TextBlock;
        }

        private void SetupNavigation()
        {
            if (dataTable != null && dataTable.Rows.Count > 1)
            {
                var navPanel = GetNavigationPanel();
                if (navPanel != null)
                    navPanel.Visibility = Visibility.Visible;
                UpdateNavigationButtons();
                UpdateRecordCounter();
            }
            else
            {
                var navPanel = GetNavigationPanel();
                if (navPanel != null)
                    navPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateNavigationButtons()
        {
            if (dataTable == null) return;
            
            var prevBtn = GetPrevButton();
            var nextBtn = GetNextButton();
            
            if (prevBtn != null)
                prevBtn.IsEnabled = currentRecordIndex > 0;
            if (nextBtn != null)
                nextBtn.IsEnabled = currentRecordIndex < dataTable.Rows.Count - 1;
        }

        private void UpdateRecordCounter()
        {
            if (dataTable == null) return;
            
            var counter = GetRecordCounter();
            if (counter != null)
                counter.Text = $"Record {currentRecordIndex + 1} of {dataTable.Rows.Count}";
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
                
                return !string.IsNullOrEmpty(fieldValue) ? fieldValue : element.Content ?? "";
            }
            
            return element.Content ?? "";
        }

        private void RenderPreview()
        {
            Console.WriteLine("[DEBUG] RenderPreview() parameterless called");
            RenderPreview(template);
        }

        private void RenderPreview(LabelTemplate template)
        {
            try
            {
                Console.WriteLine($"[DEBUG] RenderPreview called with template containing {template.Elements.Count} elements");
                
                // Update label size information in header
                var labelSizeInfo = GetLabelSizeInfo();
                if (labelSizeInfo != null)
                    labelSizeInfo.Text = $"Label Size: {template.LabelWidth}mm × {template.LabelHeight}mm";
                
                var previewCanvas = GetPreviewCanvas();
                Console.WriteLine($"[DEBUG] PreviewCanvas found: {previewCanvas != null}");
                if (previewCanvas == null) return;
                
                previewCanvas.Children.Clear();
                
                // Set canvas background to white for print preview
                previewCanvas.Background = Brushes.White;
                
                // Convert mm to screen pixels for preview
                double canvasWidthPx = MeasurementService.MmToScreenPixels(template.LabelWidth);
                double canvasHeightPx = MeasurementService.MmToScreenPixels(template.LabelHeight);
                
                Console.WriteLine($"[DEBUG] Canvas size: {canvasWidthPx} x {canvasHeightPx} pixels");
                
                // Ensure minimum canvas size
                canvasWidthPx = Math.Max(canvasWidthPx, 200);
                canvasHeightPx = Math.Max(canvasHeightPx, 100);
                
                previewCanvas.Width = canvasWidthPx;
                previewCanvas.Height = canvasHeightPx;
                
                // Set explicit clip geometry to ensure elements outside label bounds are cut off
                previewCanvas.Clip = new RectangleGeometry(new Rect(0, 0, canvasWidthPx, canvasHeightPx));
                
                int elementIndex = 0;
                Console.WriteLine($"[DEBUG] Starting to process {template.Elements.Count} elements");
                foreach (var el in template.Elements)
                {
                    Console.WriteLine($"[DEBUG] Processing element {elementIndex}: Type={el.Type}, Size={el.Width}x{el.Height}, Position={el.X},{el.Y}");
                    UIElement? uiEl = null;
                    string elementContent = GetElementContent(el, elementIndex);
                    
                    switch (el.Type)
                    {
                        case LabelElementType.Text:
                            Console.WriteLine($"[DEBUG] Creating text element: '{elementContent}', Size: {el.Width}x{el.Height}, FontSize: {el.FontSize}");
                            
                            // Create a container without visible border for text positioning
                            var textContainer = new Border
                            {
                                Width = el.Width,
                                Height = el.Height,
                                Background = Brushes.Transparent
                            };
                            
                            var tb = new TextBlock 
                            { 
                                Text = elementContent, 
                                FontSize = el.FontSize > 0 ? el.FontSize : 12, // Use 12 as default if 0 or negative
                                TextWrapping = TextWrapping.Wrap,
                                Width = el.Width  // Keep width for horizontal text alignment, but remove height for vertical alignment
                            };
                            
                            Console.WriteLine($"[DEBUG] TextBlock created with text: '{tb.Text}', FontSize: {tb.FontSize}");
                            
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
                                    tb.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.Foreground) ?? Brushes.Black);
                                else
                                    tb.Foreground = Brushes.Black;
                            }
                            catch 
                            {
                                tb.Foreground = Brushes.Black;
                            }
                            
                            // Text alignment within bounding box
                            Console.WriteLine($"[DEBUG PRINT] el.TextAlignment = '{el.TextAlignment}'");
                            File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] el.TextAlignment = '{el.TextAlignment}'\n");
                            
                            switch (el.TextAlignment?.ToLower())
                            {
                                case "center":
                                    tb.TextAlignment = TextAlignment.Center;
                                    Console.WriteLine($"[DEBUG PRINT] Set TextAlignment.Center");
                                    File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set TextAlignment.Center\n");
                                    break;
                                case "right":
                                    tb.TextAlignment = TextAlignment.Right;
                                    Console.WriteLine($"[DEBUG PRINT] Set TextAlignment.Right");
                                    File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set TextAlignment.Right\n");
                                    break;
                                default: // "left" or null
                                    tb.TextAlignment = TextAlignment.Left;
                                    Console.WriteLine($"[DEBUG PRINT] Set TextAlignment.Left (default, was: '{el.TextAlignment}')");
                                    File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set TextAlignment.Left (default, was: '{el.TextAlignment}')\n");
                                    break;
                            }
                            
                            // Vertical alignment within bounding box
                            Console.WriteLine($"[DEBUG PRINT] el.VerticalAlignment = '{el.VerticalAlignment}'");
                            File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] el.VerticalAlignment = '{el.VerticalAlignment}'\n");
                            
                            switch (el.VerticalAlignment?.ToLower())
                            {
                                case "center":
                                    tb.VerticalAlignment = VerticalAlignment.Center;
                                    Console.WriteLine($"[DEBUG PRINT] Set VerticalAlignment.Center");
                                    File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set VerticalAlignment.Center\n");
                                    break;
                                case "bottom":
                                    tb.VerticalAlignment = VerticalAlignment.Bottom;
                                    Console.WriteLine($"[DEBUG PRINT] Set VerticalAlignment.Bottom");
                                    File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set VerticalAlignment.Bottom\n");
                                    break;
                                default: // "top" or null
                                    tb.VerticalAlignment = VerticalAlignment.Top;
                                    Console.WriteLine($"[DEBUG PRINT] Set VerticalAlignment.Top (default, was: '{el.VerticalAlignment}')");
                                    File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set VerticalAlignment.Top (default, was: '{el.VerticalAlignment}')\n");
                                    break;
                            }
                            
                            Console.WriteLine($"[DEBUG] Text alignment set: TextAlignment={tb.TextAlignment}, VerticalAlignment={tb.VerticalAlignment}");
                            
                            // Safe text decorations setting
                            try
                            {
                                if (!string.IsNullOrEmpty(el.TextDecorations))
                                {
                                    switch (el.TextDecorations.ToLower())
                                    {
                                        case "underline":
                                            tb.TextDecorations = System.Windows.TextDecorations.Underline;
                                            break;
                                        case "strikethrough":
                                            tb.TextDecorations = System.Windows.TextDecorations.Strikethrough;
                                            break;
                                        case "overline":
                                            tb.TextDecorations = System.Windows.TextDecorations.OverLine;
                                            break;
                                        case "baseline":
                                            tb.TextDecorations = System.Windows.TextDecorations.Baseline;
                                            break;
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore invalid text decorations
                            }
                            
                            // Add TextBlock to the container
                            textContainer.Child = tb;
                            uiEl = textContainer;
                            
                            Console.WriteLine($"[DEBUG] Text element created, positioned at ({el.X}, {el.Y})");
                            break;
                            
                        case LabelElementType.Image:
                            if (!string.IsNullOrEmpty(el.ImageData))
                            {
                                try
                                {
                                    byte[] imageBytes = Convert.FromBase64String(el.ImageData);
                                    using (var ms = new System.IO.MemoryStream(imageBytes))
                                    {
                                        var bitmapImage = new BitmapImage();
                                        bitmapImage.BeginInit();
                                        bitmapImage.StreamSource = ms;
                                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmapImage.EndInit();
                                        bitmapImage.Freeze();

                                        var imgBrush = new ImageBrush();
                                        imgBrush.ImageSource = bitmapImage;
                                        imgBrush.Stretch = Stretch.Uniform;
                                        imgBrush.AlignmentX = AlignmentX.Center;
                                        imgBrush.AlignmentY = AlignmentY.Center;

                                        var rect = new Rectangle
                                        {
                                            Width = el.Width,
                                            Height = el.Height,
                                            Fill = imgBrush,
                                            Stroke = Brushes.DarkGray,
                                            StrokeThickness = 1
                                        };
                                        uiEl = rect;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var rect = new Rectangle
                                    {
                                        Width = el.Width,
                                        Height = el.Height,
                                        Fill = Brushes.LightGray,
                                        Stroke = Brushes.Red,
                                        StrokeThickness = 2
                                    };
                                    uiEl = rect;
                                }
                            }
                            else
                            {
                                var rect = new Rectangle
                                {
                                    Width = el.Width,
                                    Height = el.Height,
                                    Fill = Brushes.LightGray,
                                    Stroke = Brushes.DarkGray,
                                    StrokeThickness = 1
                                };
                                uiEl = rect;
                            }
                            break;
                            
                        case LabelElementType.LinkedImage:
                            // LinkedImage loads from URL
                            if (!string.IsNullOrEmpty(el.Content))
                            {
                                try
                                {
                                    // Try to load image from URL
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.UriSource = new Uri(el.Content, UriKind.Absolute);
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    bitmap.Freeze();

                                    var imgBrush = new ImageBrush();
                                    imgBrush.ImageSource = bitmap;
                                    imgBrush.Stretch = Stretch.Uniform;
                                    imgBrush.AlignmentX = AlignmentX.Center;
                                    imgBrush.AlignmentY = AlignmentY.Center;

                                    var rect = new Rectangle
                                    {
                                        Width = el.Width,
                                        Height = el.Height,
                                        Fill = imgBrush,
                                        Stroke = Brushes.DarkBlue,
                                        StrokeThickness = 1
                                    };
                                    uiEl = rect;
                                    
                                    Console.WriteLine($"[DEBUG PRINT] LinkedImage loaded from URL: {el.Content}");
                                }
                                catch (Exception ex)
                                {
                                    // Show error rectangle if URL fails to load
                                    var rect = new Rectangle
                                    {
                                        Width = el.Width,
                                        Height = el.Height,
                                        Fill = Brushes.LightBlue,
                                        Stroke = Brushes.Red,
                                        StrokeThickness = 2
                                    };
                                    uiEl = rect;
                                    Console.WriteLine($"[DEBUG PRINT] LinkedImage failed to load from URL {el.Content}: {ex.Message}");
                                }
                            }
                            else
                            {
                                // No URL provided
                                var rect = new Rectangle
                                {
                                    Width = el.Width,
                                    Height = el.Height,
                                    Fill = Brushes.LightBlue,
                                    Stroke = Brushes.DarkBlue,
                                    StrokeThickness = 1
                                };
                                uiEl = rect;
                            }
                            break;
                            
                        case LabelElementType.Barcode13:
                        case LabelElementType.Barcode8:
                        case LabelElementType.Barcode:
                        case LabelElementType.EAN13:
                        case LabelElementType.EAN8:
                        case LabelElementType.UPCA:
                        case LabelElementType.UPCE:
                        case LabelElementType.Code128:
                        case LabelElementType.Code39:
                        case LabelElementType.Aztec:
                        case LabelElementType.DataMatrix:
                        case LabelElementType.GS1DataBar:
                        case LabelElementType.PDF417:
                            // Generate actual barcode for preview using dynamic content
                            var barcodeFormat = GetBarcodeFormatString(el.Type);
                            var barcodeImg = CreateBarcodeImage(barcodeFormat, elementContent, el.Width, el.Height);
                            if (barcodeImg != null)
                            {
                                uiEl = barcodeImg;
                            }
                            else
                            {
                                // Show error text when barcode generation fails
                                var barcodeTypeName = GetBarcodeTypeName(el.Type);
                                var barcodeTb = new TextBlock 
                                { 
                                    Text = $"[{barcodeTypeName}] GENERATION ERROR\nData: {elementContent}",
                                    FontSize = 8,
                                    Width = el.Width,
                                    Height = el.Height,
                                    Foreground = Brushes.Red,
                                    TextWrapping = TextWrapping.Wrap
                                };
                                uiEl = barcodeTb;
                            }
                            break;
                            
                        case LabelElementType.QRCode:
                            // Generate actual QR code for preview using dynamic content
                            Console.WriteLine($"[DEBUG] Creating QR code, Content: '{elementContent}', Size: {el.Width}x{el.Height}");
                            var qrImg = CreateBarcodeImage("QRCODE", elementContent, el.Width, el.Height);
                            Console.WriteLine($"[DEBUG] CreateBarcodeImage for QR returned: {(qrImg != null ? "SUCCESS" : "NULL")}");
                            if (qrImg != null)
                            {
                                uiEl = qrImg;
                            }
                            else
                            {
                                // Show error text when QR code generation fails
                                var qrTb = new TextBlock 
                                { 
                                    Text = $"[QR Code] GENERATION ERROR\nData: {elementContent}",
                                    FontSize = 8,
                                    Width = el.Width,
                                    Height = el.Height,
                                    Foreground = Brushes.Red,
                                    TextWrapping = TextWrapping.Wrap
                                };
                                uiEl = qrTb;
                            }
                            break;
                            
                        case LabelElementType.Line:
                            // Create a container for the line to ensure proper positioning
                            var linePreviewContainer = new Canvas
                            {
                                Width = el.Width,
                                Height = el.Height,
                                Background = Brushes.Transparent
                            };
                            
                            var line = new System.Windows.Shapes.Line
                            {
                                X1 = 0,
                                Y1 = el.Height / 2,
                                X2 = el.Width,
                                Y2 = el.Height / 2,
                                StrokeThickness = el.BorderWidth > 0 ? el.BorderWidth : 1
                            };
                            
                            // Set line color
                            try
                            {
                                if (!string.IsNullOrEmpty(el.BorderColor))
                                    line.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.BorderColor) ?? Brushes.Black);
                                else
                                    line.Stroke = Brushes.Black;
                            }
                            catch
                            {
                                line.Stroke = Brushes.Black;
                            }
                            
                            linePreviewContainer.Children.Add(line);
                            uiEl = linePreviewContainer;
                            break;
                            
                        case LabelElementType.Rectangle:
                            // Use Border instead of Rectangle to support rounded corners
                            var rectBorder = new Border
                            {
                                Width = el.Width,
                                Height = el.Height,
                                BorderThickness = new Thickness(el.BorderWidth > 0 ? el.BorderWidth : 1),
                                Background = Brushes.Transparent,
                                CornerRadius = new CornerRadius(el.HasRoundedCorners ? el.CornerRadius : 0)
                            };
                            
                            // Set border color
                            try
                            {
                                if (!string.IsNullOrEmpty(el.BorderColor))
                                    rectBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.BorderColor) ?? Brushes.Black);
                                else
                                    rectBorder.BorderBrush = Brushes.Black;
                            }
                            catch
                            {
                                rectBorder.BorderBrush = Brushes.Black;
                            }
                            
                            Console.WriteLine($"[DEBUG PRINT] Rectangle: HasRoundedCorners={el.HasRoundedCorners}, CornerRadius={el.CornerRadius}");
                            
                            uiEl = rectBorder;
                            break;
                            
                        case LabelElementType.Ellipse:
                            var ellipse = new System.Windows.Shapes.Ellipse
                            {
                                Width = el.Width,
                                Height = el.Height,
                                StrokeThickness = el.BorderWidth > 0 ? el.BorderWidth : 1,
                                Fill = Brushes.Transparent
                            };
                            
                            // Set border color
                            try
                            {
                                if (!string.IsNullOrEmpty(el.BorderColor))
                                    ellipse.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.BorderColor) ?? Brushes.Black);
                                else
                                    ellipse.Stroke = Brushes.Black;
                            }
                            catch
                            {
                                ellipse.Stroke = Brushes.Black;
                            }
                            
                            Console.WriteLine($"[DEBUG PRINT] Ellipse: Width={el.Width}, Height={el.Height}");
                            
                            uiEl = ellipse;
                            break;
                    }
                    
                    if (uiEl != null)
                    {
                        Canvas.SetLeft(uiEl, el.X);
                        Canvas.SetTop(uiEl, el.Y);
                        
                        // Apply rotation if specified
                        if (el.Rotation != 0)
                        {
                            var rotateTransform = new RotateTransform(el.Rotation);
                            if (uiEl is FrameworkElement fe)
                            {
                                fe.RenderTransformOrigin = new Point(0.5, 0.5);
                            }
                            uiEl.RenderTransform = rotateTransform;
                        }
                        
                        Console.WriteLine($"[DEBUG] Adding element {elementIndex} to preview canvas: {uiEl?.GetType().Name}");
                        previewCanvas.Children.Add(uiEl);
                        Console.WriteLine($"[DEBUG] Element added successfully. Canvas now has {previewCanvas.Children.Count} children");
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Element {elementIndex} created NULL UIElement");
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
                    previewCanvas.Children.Add(noElementsText);
                }
            }
            catch (System.Exception ex)
            {
                // Show error message instead of crashing
                MessageBox.Show($"Error generating print preview: {ex.Message}", "Preview Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string GetBarcodeFormatString(LabelElementType type)
        {
            return type switch
            {
                LabelElementType.EAN13 => "EAN13",
                LabelElementType.EAN8 => "EAN8",
                LabelElementType.UPCA => "UPCA",
                LabelElementType.UPCE => "UPCE",
                LabelElementType.Code128 => "CODE128",
                LabelElementType.Code39 => "CODE39",
                LabelElementType.Aztec => "AZTEC",
                LabelElementType.DataMatrix => "DATAMATRIX",
                LabelElementType.GS1DataBar => "GS1DATABAR",
                LabelElementType.PDF417 => "PDF417",
                LabelElementType.QRCode => "QRCODE",
                LabelElementType.Barcode13 => "EAN13", // Legacy support
                LabelElementType.Barcode8 => "EAN8",   // Legacy support
                LabelElementType.Barcode => "EAN13",   // Legacy support
                _ => "CODE128"
            };
        }

        private string GetBarcodeTypeName(LabelElementType type)
        {
            return type switch
            {
                LabelElementType.EAN13 => "EAN-13",
                LabelElementType.EAN8 => "EAN-8", 
                LabelElementType.UPCA => "UPC-A",
                LabelElementType.UPCE => "UPC-E",
                LabelElementType.Code128 => "Code 128",
                LabelElementType.Code39 => "Code 39",
                LabelElementType.Aztec => "Aztec",
                LabelElementType.DataMatrix => "Data Matrix",
                LabelElementType.GS1DataBar => "GS1 DataBar",
                LabelElementType.PDF417 => "PDF417",
                LabelElementType.Barcode13 => "EAN-13",
                LabelElementType.Barcode8 => "EAN-8",
                LabelElementType.Barcode => "Barcode",
                _ => "Unknown"
            };
        }

        private Image CreateBarcodeImage(string symbology, string data, double width = 120, double height = 60)
        {
            try
            {
                ZXing.BarcodeFormat format;
                string defaultData;

                switch (symbology.ToUpper())
                {
                    case "EAN13":
                        format = ZXing.BarcodeFormat.EAN_13;
                        defaultData = "4000000000004"; // Valid EAN-13 with checksum
                        break;
                    case "EAN8":
                        format = ZXing.BarcodeFormat.EAN_8;
                        defaultData = "40000007"; // Valid EAN-8 with checksum
                        break;
                    case "UPCA":
                        format = ZXing.BarcodeFormat.UPC_A;
                        defaultData = "012345678905"; // Valid UPC-A with checksum
                        break;
                    case "UPCE":
                        format = ZXing.BarcodeFormat.UPC_E;
                        defaultData = "01234565"; // Valid UPC-E with checksum
                        break;
                    case "CODE128":
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "HELLO123";
                        break;
                    case "CODE39":
                        format = ZXing.BarcodeFormat.CODE_39;
                        defaultData = "HELLO39";
                        break;
                    case "QRCODE":
                        format = ZXing.BarcodeFormat.QR_CODE;
                        defaultData = "Sample QR Data";
                        break;
                    case "AZTEC":
                        format = ZXing.BarcodeFormat.AZTEC;
                        defaultData = "Sample Aztec Data";
                        break;
                    case "DATAMATRIX":
                        format = ZXing.BarcodeFormat.DATA_MATRIX;
                        defaultData = "Sample Data Matrix";
                        break;
                    case "GS1DATABAR":
                        format = ZXing.BarcodeFormat.CODE_128; // Use Code 128 for GS1
                        defaultData = "01234567890123";
                        break;
                    case "PDF417":
                        format = ZXing.BarcodeFormat.PDF_417;
                        defaultData = "Sample PDF417 Data";
                        break;
                    default:
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "DEFAULT";
                        break;
                }

                // Use provided data, fallback to default if empty
                if (string.IsNullOrWhiteSpace(data))
                    data = defaultData;

                var writer = new ZXing.BarcodeWriterPixelData();
                writer.Format = format;
                
                var options = new ZXing.Common.EncodingOptions
                {
                    Width = (int)width * 2,
                    Height = (int)height * 2,
                    Margin = 1
                };

                if (format == ZXing.BarcodeFormat.QR_CODE)
                {
                    options.Margin = 1;
                }
                else if (format == ZXing.BarcodeFormat.PDF_417)
                {
                    options.Margin = 5;
                }

                writer.Options = options;
                
                var pixelData = writer.Write(data);
                var bmp = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height), 
                    System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length);
                bmp.UnlockBits(bmpData);
                
                using (var memory = new MemoryStream())
                {
                    bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    var image = new Image
                    {
                        Source = bitmapImage,
                        Width = width,
                        Height = height,
                        Stretch = Stretch.Uniform,
                        Tag = $"BarcodeData:{symbology}:{data}"
                    };
                    
                    return image;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Barcode generation failed: {ex.Message}");
                // Fallback to text if barcode generation fails
                var fallbackText = new TextBlock
                {
                    Text = $"[{symbology}]",
                    FontSize = 10,
                    Foreground = Brushes.DarkBlue,
                    Width = width,
                    Height = height,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };

                // Create a container to hold the text
                var border = new Border
                {
                    Width = width,
                    Height = height,
                    Background = Brushes.Transparent,
                    Child = fallbackText
                };

                // Since we need to return an Image, create a dummy transparent image
                var dummyBitmap = new System.Drawing.Bitmap(1, 1);
                using (var memory = new MemoryStream())
                {
                    dummyBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    return new Image
                    {
                        Source = bitmapImage,
                        Width = width,
                        Height = height,
                        Opacity = 0.1,
                        Tag = $"BarcodeError:{symbology}:{data}"
                    };
                }
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("[DEBUG] Print_Click started");
                var printDialog = new PrintDialog();
                
                // Show printer selection dialog
                Console.WriteLine("[DEBUG] Showing print dialog");
                if (printDialog.ShowDialog() == true)
                {
                    Console.WriteLine($"[DEBUG] Printer selected: {printDialog.PrintQueue?.Name ?? "Unknown"}");
                    
                    // Ask user how many records to print
                    var printChoice = ShowPrintOptionsDialog();
                    if (printChoice == null) 
                    {
                        Console.WriteLine("[DEBUG] User cancelled print options dialog");
                        return; // User cancelled
                    }
                    
                    Console.WriteLine($"[DEBUG] Print choice: printAll={printChoice.Value.printAll}, copies={printChoice.Value.copies}");
                    
                    // Print based on user choice
                    if (printChoice.Value.printAll && dataTable != null && dataTable.Rows.Count > 1)
                    {
                        // Print all records (1 copy each)
                        var allRecordIndices = Enumerable.Range(0, dataTable.Rows.Count).ToArray();
                        Console.WriteLine($"[DEBUG] Printing all {allRecordIndices.Length} records");
                        PrintMultipleLabels(printDialog, allRecordIndices, 1);
                    }
                    else
                    {
                        // Print current record with specified copies
                        Console.WriteLine($"[DEBUG] Printing current record (index {currentRecordIndex}) with {printChoice.Value.copies} copies");
                        PrintMultipleLabels(printDialog, new[] { currentRecordIndex }, printChoice.Value.copies);
                    }
                    
                    Console.WriteLine("[DEBUG] Print operation completed");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Print dialog cancelled");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[DEBUG] Print error: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
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
            try
            {
                Console.WriteLine($"[DEBUG] PrintMultipleLabels started - {recordIndices.Length} records, {copiesPerRecord} copies each");
                
                // Use the actual label size from template
                double pageWidth = template.LabelWidth * 96.0 / 25.4; // Convert mm to DIP (Device Independent Pixels)
                double pageHeight = template.LabelHeight * 96.0 / 25.4; // Convert mm to DIP
                
                Console.WriteLine($"[DEBUG] Template label size: {template.LabelWidth}mm x {template.LabelHeight}mm");
                Console.WriteLine($"[DEBUG] Page size: {pageWidth} x {pageHeight} DIP");
                
                // Create a single document with multiple pages
                var document = new FixedDocument();
                int totalPages = 0;
                
                foreach (var recordIndex in recordIndices)
                {
                    for (int copy = 0; copy < copiesPerRecord; copy++)
                    {
                        Console.WriteLine($"[DEBUG] Creating page for record {recordIndex}, copy {copy + 1}");
                        
                        // Create a new page for this label
                        var page = new FixedPage();
                        page.Width = pageWidth;
                        page.Height = pageHeight;
                        
                        // Create the print canvas for this record
                        var printCanvas = CreateSimplePrintCanvas(recordIndex, pageWidth, pageHeight);
                        
                        Console.WriteLine($"[DEBUG] Print canvas created with {printCanvas.Children.Count} children");
                        
                        // Add canvas to page
                        page.Children.Add(printCanvas);
                        
                        // Add page to the single document
                        var pageContent = new PageContent();
                        ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                        document.Pages.Add(pageContent);
                        
                        totalPages++;
                        Console.WriteLine($"[DEBUG] Added page {totalPages} to document");
                    }
                }
                
                Console.WriteLine($"[DEBUG] Document created with {document.Pages.Count} total page(s)");
                
                // Send the single multi-page document to printer
                Console.WriteLine("[DEBUG] Sending multi-page document to printer...");
                try 
                {
                    string jobName = recordIndices.Length == 1 ? 
                        $"TaggJack Label - Record {recordIndices[0] + 1} ({copiesPerRecord} copies)" :
                        $"TaggJack Labels - {recordIndices.Length} records ({totalPages} pages)";
                    
                    printDialog.PrintDocument(document.DocumentPaginator, jobName);
                    Console.WriteLine("[DEBUG] Multi-page print job sent successfully");
                }
                catch (Exception printEx)
                {
                    Console.WriteLine($"[DEBUG] Print command failed: {printEx.Message}");
                    Console.WriteLine($"[DEBUG] Print error stack trace: {printEx.StackTrace}");
                    throw; // Re-throw to be caught by outer handler
                }
                
                Console.WriteLine("[DEBUG] All labels sent to printer");
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[DEBUG] PrintMultipleLabels error: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                throw; // Re-throw to be caught by caller
            }
        }

        private Canvas CreateSimplePrintCanvas(int recordIndex, double canvasWidth, double canvasHeight)
        {
            Console.WriteLine($"[DEBUG] CreateSimplePrintCanvas: record {recordIndex}, canvas size {canvasWidth}x{canvasHeight}");
            Console.WriteLine($"[DEBUG] Template has {template.Elements.Count} elements");
            Console.WriteLine($"[DEBUG] Template label size: {template.LabelWidth}mm x {template.LabelHeight}mm");
            
            var canvas = new Canvas();
            canvas.Width = canvasWidth;
            canvas.Height = canvasHeight;
            canvas.Background = Brushes.White;
            
            // Use NO SCALING - same as preview renders elements at their original sizes and positions
            Console.WriteLine($"[DEBUG] Using NO SCALING - same as preview");
            
            int elementIndex = 0;
            int addedElements = 0;
            
            foreach (var el in template.Elements)
            {
                Console.WriteLine($"[DEBUG] Processing element {elementIndex}: Type={el.Type}, Content='{el.Content}', Position=({el.X}, {el.Y}), Size=({el.Width}x{el.Height})");
                
                UIElement? uiEl = null;
                string elementContent = GetElementContentForRecord(el, elementIndex, recordIndex);
                
                Console.WriteLine($"[DEBUG] Element content: '{elementContent}'");
                
                switch (el.Type)
                {
                    case LabelElementType.Text:
                        // Create a border container for print (without visible border)
                        var textContainer = new Border
                        {
                            Width = el.Width,
                            Height = el.Height,
                            Background = Brushes.Transparent
                        };
                        
                        var tb = new TextBlock 
                        { 
                            Text = elementContent, 
                            FontSize = el.FontSize > 0 ? el.FontSize : 12, // Use 12 as default if 0 or negative
                            TextWrapping = TextWrapping.Wrap
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
                                tb.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.Foreground) ?? Brushes.Black);
                            else
                                tb.Foreground = Brushes.Black;
                        }
                        catch 
                        {
                            tb.Foreground = Brushes.Black;
                        }
                        
                        // Text alignment within bounding box
                        switch (el.TextAlignment?.ToLower())
                        {
                            case "center":
                                tb.TextAlignment = TextAlignment.Center;
                                break;
                            case "right":
                                tb.TextAlignment = TextAlignment.Right;
                                break;
                            default: // "left" or null
                                tb.TextAlignment = TextAlignment.Left;
                                break;
                        }
                        
                        // Vertical alignment within bounding box
                        switch (el.VerticalAlignment?.ToLower())
                        {
                            case "center":
                                tb.VerticalAlignment = VerticalAlignment.Center;
                                break;
                            case "bottom":
                                tb.VerticalAlignment = VerticalAlignment.Bottom;
                                break;
                            default: // "top" or null
                                tb.VerticalAlignment = VerticalAlignment.Top;
                                break;
                        }
                        
                        // Add TextBlock to the container
                        textContainer.Child = tb;
                        
                        Console.WriteLine($"[DEBUG] Created TextBlock with alignment: '{tb.Text}', FontSize={tb.FontSize}, TextAlign={tb.TextAlignment}, VerticalAlign={tb.VerticalAlignment}");
                        uiEl = textContainer;
                        break;
                        
                    case LabelElementType.Image:
                        if (!string.IsNullOrEmpty(el.ImageData))
                        {
                            try
                            {
                                // Use a more robust approach: create bitmap from byte array directly
                                byte[] imageBytes = Convert.FromBase64String(el.ImageData);
                                
                                // Create BitmapImage with proper initialization for printing
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                bitmapImage.StreamSource = new System.IO.MemoryStream(imageBytes);
                                bitmapImage.EndInit();
                                bitmapImage.Freeze(); // Important for threading and print context
                                
                                var img = new System.Windows.Controls.Image
                                {
                                    Source = bitmapImage,
                                    Width = el.Width,
                                    Height = el.Height,
                                    Stretch = Stretch.Uniform,
                                    StretchDirection = StretchDirection.Both
                                };
                                
                                Console.WriteLine($"[DEBUG] Created actual Image for printing: {img.Width}x{img.Height}");
                                uiEl = img;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Image load error in print: {ex.Message}");
                                // Fall back to a clear placeholder if image loading fails
                                var rect = new Rectangle
                                {
                                    Width = el.Width,
                                    Height = el.Height,
                                    Fill = Brushes.LightGray,
                                    Stroke = Brushes.Red,
                                    StrokeThickness = 2
                                };
                                uiEl = rect;
                            }
                        }
                        else
                        {
                            // No image data - show placeholder
                            var rect = new Rectangle
                            {
                                Width = el.Width,
                                Height = el.Height,
                                Fill = Brushes.LightGray,
                                Stroke = Brushes.DarkGray,
                                StrokeThickness = 1
                            };
                            Console.WriteLine($"[DEBUG] Created Image placeholder for printing: {rect.Width}x{rect.Height}");
                            uiEl = rect;
                        }
                        break;
                        
                    case LabelElementType.LinkedImage:
                        // LinkedImage loads from URL for printing
                        if (!string.IsNullOrEmpty(el.Content))
                        {
                            try
                            {
                                // Load image from URL for printing
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                bitmapImage.UriSource = new Uri(el.Content, UriKind.Absolute);
                                bitmapImage.EndInit();
                                bitmapImage.Freeze(); // Important for threading and print context
                                
                                var img = new System.Windows.Controls.Image
                                {
                                    Source = bitmapImage,
                                    Width = el.Width,
                                    Height = el.Height,
                                    Stretch = Stretch.Uniform,
                                    StretchDirection = StretchDirection.Both
                                };
                                
                                Console.WriteLine($"[DEBUG] Created LinkedImage for printing from URL: {el.Content}");
                                uiEl = img;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] LinkedImage load error in print from URL {el.Content}: {ex.Message}");
                                // Fall back to a clear placeholder if URL fails
                                var rect = new Rectangle
                                {
                                    Width = el.Width,
                                    Height = el.Height,
                                    Fill = Brushes.LightBlue,
                                    Stroke = Brushes.Red,
                                    StrokeThickness = 2
                                };
                                uiEl = rect;
                            }
                        }
                        else
                        {
                            // No URL provided - show placeholder
                            var rect = new Rectangle
                            {
                                Width = el.Width,
                                Height = el.Height,
                                Fill = Brushes.LightBlue,
                                Stroke = Brushes.DarkBlue,
                                StrokeThickness = 1
                            };
                            Console.WriteLine($"[DEBUG] Created LinkedImage placeholder for printing: {rect.Width}x{rect.Height}");
                            uiEl = rect;
                        }
                        break;
                        
                    case LabelElementType.EAN13:
                    case LabelElementType.EAN8:
                    case LabelElementType.UPCA:
                    case LabelElementType.UPCE:
                    case LabelElementType.Code128:
                    case LabelElementType.Code39:
                    case LabelElementType.Aztec:
                    case LabelElementType.DataMatrix:
                    case LabelElementType.GS1DataBar:
                    case LabelElementType.PDF417:
                    case LabelElementType.QRCode:
                    case LabelElementType.Barcode13:
                    case LabelElementType.Barcode8:
                    case LabelElementType.Barcode:
                        // Create actual barcode using SAME SIZE as preview
                        var barcodeImage = CreateBarcodeImage(GetBarcodeFormatString(el.Type), elementContent, 
                            el.Width, el.Height); // NO SCALING - same as preview
                        
                        if (barcodeImage != null)
                        {
                            Console.WriteLine($"[DEBUG] Created {el.Type} barcode image: {barcodeImage.Width}x{barcodeImage.Height}");
                            uiEl = barcodeImage;
                        }
                        else
                        {
                            // Fallback to text if barcode generation fails - SAME SIZE as preview
                            var barcodeText = new TextBlock 
                            { 
                                Text = $"[{el.Type}: {elementContent}]",
                                FontSize = 8,
                                Width = el.Width,   // NO SCALING - same as preview
                                Height = el.Height, // NO SCALING - same as preview
                                Foreground = Brushes.Black,
                                TextAlignment = TextAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Console.WriteLine($"[DEBUG] Created {el.Type} fallback text");
                            uiEl = barcodeText;
                        }
                        break;
                        
                    case LabelElementType.Line:
                        // Create a container for the line to ensure proper positioning
                        var lineContainer = new Canvas
                        {
                            Width = el.Width,
                            Height = el.Height,
                            Background = Brushes.Transparent
                        };
                        
                        var printLine = new System.Windows.Shapes.Line
                        {
                            X1 = 0,
                            Y1 = el.Height / 2,
                            X2 = el.Width,
                            Y2 = el.Height / 2,
                            StrokeThickness = el.BorderWidth > 0 ? el.BorderWidth : 1
                        };
                        
                        // Set line color
                        try
                        {
                            if (!string.IsNullOrEmpty(el.BorderColor))
                                printLine.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.BorderColor) ?? Brushes.Black);
                            else
                                printLine.Stroke = Brushes.Black;
                        }
                        catch
                        {
                            printLine.Stroke = Brushes.Black;
                        }
                        
                        lineContainer.Children.Add(printLine);
                        Console.WriteLine($"[DEBUG] Created Line for printing: {el.Width}x{el.Height}, Thickness: {printLine.StrokeThickness}");
                        uiEl = lineContainer;
                        break;
                        
                    case LabelElementType.Rectangle:
                        // Use Border instead of Rectangle to support rounded corners in printing
                        var printRectBorder = new Border
                        {
                            Width = el.Width,
                            Height = el.Height,
                            BorderThickness = new Thickness(el.BorderWidth > 0 ? el.BorderWidth : 1),
                            Background = Brushes.Transparent,
                            CornerRadius = new CornerRadius(el.HasRoundedCorners ? el.CornerRadius : 0)
                        };
                        
                        // Set border color
                        try
                        {
                            if (!string.IsNullOrEmpty(el.BorderColor))
                                printRectBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.BorderColor) ?? Brushes.Black);
                            else
                                printRectBorder.BorderBrush = Brushes.Black;
                        }
                        catch
                        {
                            printRectBorder.BorderBrush = Brushes.Black;
                        }
                        
                        Console.WriteLine($"[DEBUG] Created Rectangle for printing: {printRectBorder.Width}x{printRectBorder.Height}, HasRoundedCorners: {el.HasRoundedCorners}, CornerRadius: {el.CornerRadius}");
                        uiEl = printRectBorder;
                        break;
                        
                    case LabelElementType.Ellipse:
                        var printEllipse = new System.Windows.Shapes.Ellipse
                        {
                            Width = el.Width,
                            Height = el.Height,
                            StrokeThickness = el.BorderWidth > 0 ? el.BorderWidth : 1,
                            Fill = Brushes.Transparent
                        };
                        
                        // Set border color
                        try
                        {
                            if (!string.IsNullOrEmpty(el.BorderColor))
                                printEllipse.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.BorderColor) ?? Brushes.Black);
                            else
                                printEllipse.Stroke = Brushes.Black;
                        }
                        catch
                        {
                            printEllipse.Stroke = Brushes.Black;
                        }
                        
                        Console.WriteLine($"[DEBUG] Created Ellipse for printing: {printEllipse.Width}x{printEllipse.Height}");
                        uiEl = printEllipse;
                        break;
                        
                    default:
                        var unknownText = new TextBlock
                        {
                            Text = $"[Unknown: {el.Type}]",
                            FontSize = 8,
                            Width = el.Width,   // NO SCALING - same as preview
                            Height = el.Height, // NO SCALING - same as preview
                            Foreground = Brushes.Red,
                            TextAlignment = TextAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Console.WriteLine($"[DEBUG] Created unknown element type: {el.Type}");
                        uiEl = unknownText;
                        break;
                }
                
                if (uiEl != null)
                {
                    // Use EXACT SAME positioning as preview - NO SCALING
                    double x = el.X;
                    double y = el.Y;
                    
                    Canvas.SetLeft(uiEl, x);
                    Canvas.SetTop(uiEl, y);
                    
                    // Apply rotation if specified - SAME as preview
                    if (el.Rotation != 0)
                    {
                        var rotateTransform = new RotateTransform(el.Rotation);
                        if (uiEl is FrameworkElement fe)
                        {
                            fe.RenderTransformOrigin = new Point(0.5, 0.5);
                        }
                        uiEl.RenderTransform = rotateTransform;
                    }
                    
                    canvas.Children.Add(uiEl);
                    addedElements++;
                    
                    Console.WriteLine($"[DEBUG] Added element {elementIndex} ({el.Type}) to canvas at position ({x}, {y}). Canvas now has {canvas.Children.Count} children");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Failed to create UI element for element {elementIndex}");
                }
                
                elementIndex++;
            }
            
            Console.WriteLine($"[DEBUG] Canvas completed: {addedElements} elements added out of {template.Elements.Count} template elements");
            Console.WriteLine($"[DEBUG] Final canvas children count: {canvas.Children.Count}");
            
            return canvas;
        }

        private FixedPage CreatePageWithLabels(int[] recordIndices, double labelWidthPx, double labelHeightPx, 
            Size pageSize, int labelsPerRow)
        {
            Console.WriteLine($"[DEBUG] CreatePageWithLabels: {recordIndices.Length} records, page size {labelWidthPx}x{labelHeightPx}");
            
            var page = new FixedPage();
            page.Width = labelWidthPx;  // Page size should match label size
            page.Height = labelHeightPx;
            
            Console.WriteLine($"[DEBUG] FixedPage created with size {page.Width}x{page.Height}");
            
            // For single label per page, just add one label at position (0,0)
            if (recordIndices.Length > 0)
            {
                Console.WriteLine($"[DEBUG] Creating label canvas for record {recordIndices[0]}");
                var labelCanvas = CreatePrintCanvas(recordIndices[0], labelWidthPx, labelHeightPx);
                
                // Position the label at top-left of the page
                Canvas.SetLeft(labelCanvas, 0);
                Canvas.SetTop(labelCanvas, 0);
                
                page.Children.Add(labelCanvas);
                Console.WriteLine($"[DEBUG] Label canvas added to page. Page now has {page.Children.Count} children");
            }
            else
            {
                Console.WriteLine("[DEBUG] No record indices provided!");
            }
            
            return page;
        }

        private Canvas CreatePrintCanvas(int recordIndex, double canvasWidthPx, double canvasHeightPx)
        {
            Console.WriteLine($"[DEBUG] CreatePrintCanvas: record {recordIndex}, canvas size {canvasWidthPx}x{canvasHeightPx}");
            
            var printCanvas = new Canvas();
            printCanvas.Background = Brushes.White;
            printCanvas.Width = canvasWidthPx;
            printCanvas.Height = canvasHeightPx;
            
            Console.WriteLine($"[DEBUG] Print canvas created with size {printCanvas.Width}x{printCanvas.Height}");
            
            // Calculate proper scale factor from screen to print
            // Screen measurements in template are in pixels at screen DPI (72)
            // Print measurements need to be in pixels at print DPI
            double screenToMmFactor = 1.0 / MeasurementService.PIXELS_PER_MM_SCREEN; // Convert screen pixels to mm
            double mmToPrintFactor = canvasWidthPx / template.LabelWidth; // Convert mm to print pixels
            double scaleFactor = mmToPrintFactor * screenToMmFactor;
            
            Console.WriteLine($"[DEBUG] Scale factor: {scaleFactor}");
            
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
                            FontSize = el.FontSize > 0 ? (el.FontSize * scaleFactor) : (12 * scaleFactor), // Use 12 as default
                            Width = el.Width * scaleFactor,
                            Height = el.Height * scaleFactor,
                            TextWrapping = TextWrapping.Wrap
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
                            // First check if we have base64 image data
                            if (!string.IsNullOrEmpty(el.ImageData))
                            {
                                // Convert base64 to BitmapImage using simpler approach
                                byte[] imageBytes = Convert.FromBase64String(el.ImageData);
                                using (var ms = new System.IO.MemoryStream(imageBytes))
                                {
                                    var bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = ms;
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmapImage.EndInit();
                                    bitmapImage.Freeze(); // Freeze to make it cross-thread safe and improve performance
                                    
                                    var imgBrush = new ImageBrush();
                                    imgBrush.ImageSource = bitmapImage;
                                    imgBrush.Stretch = Stretch.Uniform;
                                    imgBrush.AlignmentX = AlignmentX.Center;
                                    imgBrush.AlignmentY = AlignmentY.Center;
                                    rect.Fill = imgBrush;
                                }
                            }
                            // Fallback to file path if no base64 data
                            else if (!string.IsNullOrEmpty(elementContent) && System.IO.File.Exists(elementContent))
                            {
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.UriSource = new System.Uri(elementContent);
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze(); // Freeze for performance
                                
                                var imgBrush = new ImageBrush();
                                imgBrush.ImageSource = bitmapImage;
                                imgBrush.Stretch = Stretch.Uniform;
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
                            }
                        } 
                        catch (Exception ex)
                        { 
                            // Log the actual error for debugging
                            Console.WriteLine($"[DEBUG] Error loading image for print: {ex.Message}");
                            rect.Fill = Brushes.LightGray;
                            rect.Stroke = Brushes.Red;
                            rect.StrokeThickness = 2;
                        }
                        uiEl = rect;
                        break;
                        
                    case LabelElementType.LinkedImage:
                        // Create Rectangle for LinkedImage printing
                        var linkedRect = new Rectangle { 
                            Width = el.Width * scaleFactor, 
                            Height = el.Height * scaleFactor 
                        };
                        try 
                        {
                            // Load from URL for LinkedImage
                            if (!string.IsNullOrEmpty(el.Content))
                            {
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.UriSource = new Uri(el.Content, UriKind.Absolute);
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze(); // Freeze for performance and thread safety
                                
                                var imgBrush = new ImageBrush();
                                imgBrush.ImageSource = bitmapImage;
                                imgBrush.Stretch = Stretch.Uniform;
                                imgBrush.AlignmentX = AlignmentX.Center;
                                imgBrush.AlignmentY = AlignmentY.Center;
                                linkedRect.Fill = imgBrush;
                                
                                Console.WriteLine($"[DEBUG] LinkedImage loaded for print from URL: {el.Content}");
                            }
                            else
                            {
                                // No URL provided - show placeholder
                                linkedRect.Fill = Brushes.LightBlue;
                                linkedRect.Stroke = Brushes.DarkBlue;
                                linkedRect.StrokeThickness = 1;
                            }
                        } 
                        catch (Exception ex)
                        { 
                            // Log the actual error for debugging
                            Console.WriteLine($"[DEBUG] Error loading LinkedImage for print from URL {el.Content}: {ex.Message}");
                            linkedRect.Fill = Brushes.LightBlue;
                            linkedRect.Stroke = Brushes.Red;
                            linkedRect.StrokeThickness = 2;
                        }
                        uiEl = linkedRect;
                        break;
                        
                    case LabelElementType.Barcode13:
                    case LabelElementType.Barcode8:
                    case LabelElementType.Barcode:
                    case LabelElementType.EAN13:
                    case LabelElementType.EAN8:
                    case LabelElementType.UPCA:
                    case LabelElementType.UPCE:
                    case LabelElementType.Code128:
                    case LabelElementType.Code39:
                    case LabelElementType.Aztec:
                    case LabelElementType.DataMatrix:
                    case LabelElementType.GS1DataBar:
                    case LabelElementType.PDF417:
                        // Create a completely new barcode image for printing
                        var barcodeImg = GenerateBarcodeImageForPrint(elementContent, el.Type, 
                            el.Width * scaleFactor, el.Height * scaleFactor);
                        if (barcodeImg != null)
                        {
                            uiEl = barcodeImg;
                        }
                        else
                        {
                            // Fallback to text if barcode generation fails
                            var barcodeTypeName = GetBarcodeTypeName(el.Type);
                            var barcodeTb = new TextBlock 
                            { 
                                Text = $"[{barcodeTypeName}: {elementContent}]",
                                FontSize = Math.Max(8 * scaleFactor, 1), // Ensure FontSize is at least 1
                                Width = el.Width * scaleFactor,
                                Height = el.Height * scaleFactor,
                                Foreground = Brushes.Black
                            };
                            uiEl = barcodeTb;
                        }
                        break;
                        
                    case LabelElementType.QRCode:
                        // Create a completely new QR code image for printing
                        var qrImg = GenerateQRCodeImageForPrint(elementContent, 
                            el.Width * scaleFactor, el.Height * scaleFactor);
                        if (qrImg != null)
                        {
                            uiEl = qrImg;
                        }
                        else
                        {
                            // Fallback to text if QR generation fails
                            var qrTb = new TextBlock 
                            { 
                                Text = $"[QR: {elementContent}]",
                                FontSize = Math.Max(8 * scaleFactor, 1), // Ensure FontSize is at least 1
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
                    
                    // Apply rotation if specified
                    if (el.Rotation != 0)
                    {
                        var rotateTransform = new RotateTransform(el.Rotation);
                        if (uiEl is FrameworkElement fe)
                        {
                            fe.RenderTransformOrigin = new Point(0.5, 0.5);
                        }
                        uiEl.RenderTransform = rotateTransform;
                    }
                    
                    printCanvas.Children.Add(uiEl);
                }
                
                elementIndex++;
            }
            
            Console.WriteLine($"[DEBUG] CreatePrintCanvas completed. Canvas has {printCanvas.Children.Count} elements from {template.Elements.Count} template elements");
            
            return printCanvas;
        }

        // Create separate barcode generation method for printing to avoid element reuse
        private Image? GenerateBarcodeImageForPrint(string content, LabelElementType type, double width, double height)
        {
            var barcodeFormat = GetBarcodeFormatString(type);
            return CreateBarcodeImage(barcodeFormat, content, width, height);
        }

        // Create separate QR code generation method for printing to avoid element reuse  
        private Image? GenerateQRCodeImageForPrint(string content, double width, double height)
        {
            return CreateBarcodeImage("QRCODE", content, width, height);
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

        private string ValidateBarcodeContent(string content, LabelElementType type)
        {
            if (string.IsNullOrEmpty(content))
            {
                // Return default valid content for each barcode type
                return type switch
                {
                    LabelElementType.EAN13 => "4000000000004",
                    LabelElementType.EAN8 => "40000007",
                    LabelElementType.UPCA => "012345678905",
                    LabelElementType.UPCE => "01234565",
                    LabelElementType.Code128 => "HELLO123",
                    LabelElementType.Code39 => "HELLO39",
                    _ => "4000000000004"
                };
            }
            
            // Validate and fix content based on barcode type
            switch (type)
            {
                case LabelElementType.EAN13:
                case LabelElementType.Barcode13:
                    // EAN-13 needs exactly 12 or 13 digits
                    string digits = new string(content.Where(char.IsDigit).ToArray());
                    if (digits.Length > 13)
                    {
                        return digits.Substring(0, 13);
                    }
                    else if (digits.Length < 12)
                    {
                        return digits.PadLeft(12, '0');
                    }
                    return digits;
                    
                case LabelElementType.EAN8:
                case LabelElementType.Barcode8:
                    // EAN-8 needs exactly 7 or 8 digits
                    string digits8 = new string(content.Where(char.IsDigit).ToArray());
                    if (digits8.Length > 8)
                    {
                        return digits8.Substring(0, 8);
                    }
                    else if (digits8.Length < 7)
                    {
                        return digits8.PadLeft(7, '0');
                    }
                    return digits8;
                    
                case LabelElementType.UPCA:
                    // UPC-A needs exactly 11 or 12 digits
                    string digitsUPCA = new string(content.Where(char.IsDigit).ToArray());
                    if (digitsUPCA.Length > 12)
                    {
                        return digitsUPCA.Substring(0, 12);
                    }
                    else if (digitsUPCA.Length < 11)
                    {
                        return digitsUPCA.PadLeft(11, '0');
                    }
                    return digitsUPCA;
                    
                default:
                    // For other barcode types, just return the content as-is
                    return content;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
