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
using System.Data;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using System;
using System.ComponentModel;
using EvoDesigner.Services;
using EvoDesigner.Models;
using EvoDesigner.Commands;

namespace EvoDesigner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EvoDesigner.Models.LabelTemplate CurrentTemplate = new();
        private Point dragStart;
        private bool isDragging = false;
        private UIElement? selectedElement = null;
        private DataTable? lastResultTable = null;
        private Thumb? resizeThumb = null;
        private AppConfig _appConfig;
        private EvoDesigner.Models.DataSourceConfig? _currentDataSource = null;
        
        // Track unsaved changes
        private bool _isDirty = false;
        private string? _currentFilePath = null;
        
        // Label dimensions (in mm)
        private double _labelWidthMm = 70.0;
        private double _labelHeightMm = 30.0;
        
        // Zoom functionality
        private double _zoomFactor = 1.0;
        private const double MIN_ZOOM = 0.1;
        private const double MAX_ZOOM = 5.0;
        private const double ZOOM_STEP = 0.1;
        
        // Selection rectangle functionality
        private bool isSelecting = false;
        private Point selectionStart;
        private Rectangle? selectionRectangle = null;
        private List<UIElement> selectedElements = new();
        
        // Undo/Redo functionality
        private CommandHistory commandHistory = new CommandHistory();
        
        // Rotation tracking for elements (separate from Tag to avoid conflicts with image data)
        private Dictionary<UIElement, double> elementRotations = new Dictionary<UIElement, double>();
        
        // Clipboard functionality
        private List<EvoDesigner.Models.LabelElement> clipboardElements = new List<EvoDesigner.Models.LabelElement>();

        public MainWindow()
        {
            Console.WriteLine("[DEBUG] MainWindow constructor called");
            InitializeComponent();
            
            // Load configuration on startup
            _appConfig = ConfigService.LoadConfig();
            
            // Attach handlers after the UI is fully loaded
            this.Loaded += MainWindow_Loaded;
            
            LabelCanvas.Drop += LabelCanvas_Drop;
            LabelCanvas.AllowDrop = true;
            
            // Initialize selection functionality
            LabelCanvas.MouseLeftButtonDown += LabelCanvas_MouseLeftButtonDown;
            
            // Add keyboard event handling
            this.KeyDown += MainWindow_KeyDown;

            // Initialize zoom functionality
            InitializeZoomControls();

            // Wire up existing buttons
            var saveBtn = FindButtonByContent("Save Template");
            if (saveBtn != null) saveBtn.Click += SaveTemplate_Click;
            var loadBtn = FindButtonByContent("Load Template");
            if (loadBtn != null) loadBtn.Click += LoadTemplate_Click;
            var previewBtn = FindButtonByContent("Print Preview");
            if (previewBtn != null) previewBtn.Click += PrintPreview_Click;
            var dataSourceBtn = FindButtonByContent("Setup Data Source");
            if (dataSourceBtn != null) dataSourceBtn.Click += DataSource_Click;
            
            // Wire up grid toggle
            ShowGridCheckBox.Checked += ShowGridCheckBox_Changed;
            ShowGridCheckBox.Unchecked += ShowGridCheckBox_Changed;

            // Wire up template property change events (only margins now)
            MarginLeftBox.TextChanged += TemplateDimensions_Changed;
            MarginRightBox.TextChanged += TemplateDimensions_Changed;
            MarginTopBox.TextChanged += TemplateDimensions_Changed;
            MarginBottomBox.TextChanged += TemplateDimensions_Changed;
            
            // Set default template values
            InitializeDefaultTemplate();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Attach toolbox drag handlers after UI is fully loaded
            AttachToolboxDragHandlers();
            
            // Initialize layer control buttons in the LayerControlsContainer
            InitializeLayerControls();
        }
        
        private void InitializeLayerControls()
        {
            // Add layer control buttons to the LayerControlsContainer
            var bringForwardBtn = new Button 
            { 
                Content = "Bring Forward", 
                Margin = new Thickness(0, 0, 5, 0), 
                Height = 28,
                ToolTip = "Bring selected element forward"
            };
            bringForwardBtn.Style = (Style)FindResource("CompactButtonStyle");
            LayerControlsContainer.Children.Add(bringForwardBtn);
            bringForwardBtn.Click += BringForward_Click;

            var sendBackwardBtn = new Button 
            { 
                Content = "Send Backward", 
                Margin = new Thickness(0, 0, 0, 0), 
                Height = 28,
                ToolTip = "Send selected element backward"
            };
            sendBackwardBtn.Style = (Style)FindResource("CompactButtonStyle");
            LayerControlsContainer.Children.Add(sendBackwardBtn);
            sendBackwardBtn.Click += SendBackward_Click;
        }

        private void InitializeDefaultTemplate()
        {
            // Set default label size (common address label: 70mm x 30mm)
            _labelWidthMm = 70.0;
            _labelHeightMm = 30.0;
            UpdateDisplayedDimensions();
            MarginLeftBox.Text = "2";
            MarginRightBox.Text = "2";
            MarginTopBox.Text = "2";
            MarginBottomBox.Text = "2";
            
            UpdateCanvasSize();
        }

        private void UpdateDisplayedDimensions()
        {
            LabelWidthDisplay.Text = $"{_labelWidthMm:F1} mm";
            LabelHeightDisplay.Text = $"{_labelHeightMm:F1} mm";
        }

        private void TemplateDimensions_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateCanvasSize();
            SetDirty(true);
        }

        private void UpdateCanvasSize()
        {
            // Use internal properties instead of textboxes for dimensions
            double widthMm = _labelWidthMm;
            double heightMm = _labelHeightMm;
            double marginLeftMm = double.TryParse(MarginLeftBox.Text, out double ml) ? ml : 2;
            double marginRightMm = double.TryParse(MarginRightBox.Text, out double mr) ? mr : 2;
            double marginTopMm = double.TryParse(MarginTopBox.Text, out double mt) ? mt : 2;
            double marginBottomMm = double.TryParse(MarginBottomBox.Text, out double mb) ? mb : 2;

            // Convert to screen pixels
            double canvasWidth = MeasurementService.MmToScreenPixels(widthMm);
            double canvasHeight = MeasurementService.MmToScreenPixels(heightMm);

            // Update canvas size
            LabelCanvas.Width = canvasWidth;
            LabelCanvas.Height = canvasHeight;
            
            // Set explicit clip geometry to ensure elements outside label bounds are cut off
            LabelCanvas.Clip = new RectangleGeometry(new Rect(0, 0, canvasWidth, canvasHeight));
            
            // Update other canvas layers to match
            GridCanvas.Width = canvasWidth;
            GridCanvas.Height = canvasHeight;
            MarginCanvas.Width = canvasWidth;
            MarginCanvas.Height = canvasHeight;
            
            // Ensure CanvasContainer can accommodate the canvas size
            // Add some padding around the canvas for better usability
            double containerPadding = 100; // 100 pixels padding around the canvas
            CanvasContainer.Width = canvasWidth + containerPadding;
            CanvasContainer.Height = canvasHeight + containerPadding;
            
            // Update template object
            CurrentTemplate.LabelWidth = widthMm;
            CurrentTemplate.LabelHeight = heightMm;
            CurrentTemplate.MarginLeft = marginLeftMm;
            CurrentTemplate.MarginRight = marginRightMm;
            CurrentTemplate.MarginTop = marginTopMm;
            CurrentTemplate.MarginBottom = marginBottomMm;

            // Update visual guidelines
            UpdateMarginGuidelines();
            UpdateRulers();
            UpdateGrid();
        }

        private void UpdateMarginGuidelines()
        {
            MarginCanvas.Children.Clear();
            
            double canvasWidth = LabelCanvas.Width;
            double canvasHeight = LabelCanvas.Height;
            
            double marginLeftPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginLeft);
            double marginRightPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginRight);
            double marginTopPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginTop);
            double marginBottomPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginBottom);

            var marginBrush = new SolidColorBrush(Colors.Red) { Opacity = 0.7 };

            // Left margin line
            if (marginLeftPx > 0)
            {
                var leftLine = new Line
                {
                    X1 = marginLeftPx, Y1 = 0,
                    X2 = marginLeftPx, Y2 = canvasHeight,
                    Stroke = marginBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                MarginCanvas.Children.Add(leftLine);
            }

            // Right margin line
            if (marginRightPx > 0)
            {
                var rightLine = new Line
                {
                    X1 = canvasWidth - marginRightPx, Y1 = 0,
                    X2 = canvasWidth - marginRightPx, Y2 = canvasHeight,
                    Stroke = marginBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                MarginCanvas.Children.Add(rightLine);
            }

            // Top margin line
            if (marginTopPx > 0)
            {
                var topLine = new Line
                {
                    X1 = 0, Y1 = marginTopPx,
                    X2 = canvasWidth, Y2 = marginTopPx,
                    Stroke = marginBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                MarginCanvas.Children.Add(topLine);
            }

            // Bottom margin line
            if (marginBottomPx > 0)
            {
                var bottomLine = new Line
                {
                    X1 = 0, Y1 = canvasHeight - marginBottomPx,
                    X2 = canvasWidth, Y2 = canvasHeight - marginBottomPx,
                    Stroke = marginBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                MarginCanvas.Children.Add(bottomLine);
            }
        }

        private void UpdateRulers()
        {
            UpdateTopRuler();
            UpdateLeftRuler();
        }

        private void UpdateTopRuler()
        {
            TopRuler.Children.Clear();
            // Scale the ruler width to accommodate zoomed content
            TopRuler.Width = LabelCanvas.Width * _zoomFactor;

            var rulerBrush = Brushes.Black;
            double canvasWidthMm = CurrentTemplate.LabelWidth;

            // Major marks every 10mm, minor marks every 1mm
            for (double mm = 0; mm <= canvasWidthMm; mm += 1)
            {
                // Apply zoom factor to positioning but keep visual elements original size
                double x = MeasurementService.MmToScreenPixels(mm) * _zoomFactor;
                bool isMajor = (mm % 10) == 0;
                double markHeight = isMajor ? 15 : 8;

                var line = new Line
                {
                    X1 = x, Y1 = 25 - markHeight,
                    X2 = x, Y2 = 25,
                    Stroke = rulerBrush,
                    StrokeThickness = isMajor ? 1 : 0.5  // Keep original thickness
                };
                TopRuler.Children.Add(line);

                // Add text labels for major marks
                if (isMajor && mm > 0)
                {
                    var text = new TextBlock
                    {
                        Text = mm.ToString(),
                        FontSize = 8,  // Keep original font size
                        Foreground = rulerBrush
                    };
                    Canvas.SetLeft(text, x - 10);
                    Canvas.SetTop(text, 2);
                    TopRuler.Children.Add(text);
                }
            }
        }

        private void UpdateLeftRuler()
        {
            LeftRuler.Children.Clear();
            // Scale the ruler height to accommodate zoomed content
            LeftRuler.Height = LabelCanvas.Height * _zoomFactor;

            var rulerBrush = Brushes.Black;
            double canvasHeightMm = CurrentTemplate.LabelHeight;

            // Major marks every 10mm, minor marks every 1mm
            for (double mm = 0; mm <= canvasHeightMm; mm += 1)
            {
                // Apply zoom factor to positioning but keep visual elements original size
                double y = MeasurementService.MmToScreenPixels(mm) * _zoomFactor;
                bool isMajor = (mm % 10) == 0;
                double markWidth = isMajor ? 15 : 8;

                var line = new Line
                {
                    X1 = 25 - markWidth, Y1 = y,
                    X2 = 25, Y2 = y,
                    Stroke = rulerBrush,
                    StrokeThickness = isMajor ? 1 : 0.5  // Keep original thickness
                };
                LeftRuler.Children.Add(line);

                // Add text labels for major marks
                if (isMajor && mm > 0)
                {
                    var text = new TextBlock
                    {
                        Text = mm.ToString(),
                        FontSize = 8,  // Keep original font size
                        Foreground = rulerBrush,
                        RenderTransform = new RotateTransform(-90)
                    };
                    Canvas.SetLeft(text, 2);
                    Canvas.SetTop(text, y + 10);
                    LeftRuler.Children.Add(text);
                }
            }
        }

        private void UpdateGrid()
        {
            GridCanvas.Children.Clear();
            
            // Ensure GridCanvas matches LabelCanvas dimensions exactly
            GridCanvas.Width = LabelCanvas.Width;
            GridCanvas.Height = LabelCanvas.Height;

            // Only show grid if checkbox is checked
            if (ShowGridCheckBox?.IsChecked != true)
            {
                GridCanvas.Visibility = Visibility.Hidden;
                return;
            }
            
            GridCanvas.Visibility = Visibility.Visible;
            
            // Professional light grey grid colors
            var gridBrush = new SolidColorBrush(Colors.LightGray) { Opacity = 0.4 };
            var majorGridBrush = new SolidColorBrush(Colors.LightGray) { Opacity = 0.6 };

            // Grid lines every 5mm
            double canvasWidthMm = CurrentTemplate.LabelWidth;
            double canvasHeightMm = CurrentTemplate.LabelHeight;

            int lineCount = 0;
            
            // Vertical grid lines
            for (double mm = 5; mm < canvasWidthMm; mm += 5)
            {
                double x = MeasurementService.MmToScreenPixels(mm);
                bool isMajorLine = (mm % 10) == 0; // Every 10mm is a major line
                
                var line = new Line
                {
                    X1 = x, Y1 = 0,
                    X2 = x, Y2 = GridCanvas.Height,
                    Stroke = isMajorLine ? majorGridBrush : gridBrush,
                    StrokeThickness = isMajorLine ? 0.8 : 0.5  // Professional line thickness
                };
                GridCanvas.Children.Add(line);
                lineCount++;
            }

            // Horizontal grid lines
            for (double mm = 5; mm < canvasHeightMm; mm += 5)
            {
                double y = MeasurementService.MmToScreenPixels(mm);
                bool isMajorLine = (mm % 10) == 0; // Every 10mm is a major line
                
                var line = new Line
                {
                    X1 = 0, Y1 = y,
                    X2 = GridCanvas.Width, Y2 = y,
                    Stroke = isMajorLine ? majorGridBrush : gridBrush,
                    StrokeThickness = isMajorLine ? 0.8 : 0.5  // Professional line thickness
                };
                GridCanvas.Children.Add(line);
                lineCount++;
            }
        }

        private void ShowGridCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (GridCanvas != null)
            {
                GridCanvas.Visibility = ShowGridCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private Button FindButtonByContent(string content)
        {
            return FindElementByContent<Button>(Content as DependencyObject, content);
        }

        private T? FindElementByContent<T>(DependencyObject? parent, string content) where T : Control
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button btn && btn.Content?.ToString() == content && child is T control)
                    return control;
                var result = FindElementByContent<T>(child, content);
                if (result != null) return result;
            }
            return null;
        }

        #region Image Base64 Handling

        private string ConvertImageToBase64(string filePath)
        {
            try
            {
                byte[] imageBytes = File.ReadAllBytes(filePath);
                return Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert image to base64: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }

        private BitmapImage ConvertBase64ToImage(string base64String)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64String);
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(imageBytes))
                {
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert base64 to image: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return new BitmapImage();
            }
        }

        private Image CreateImageFromFile(string filePath)
        {
            var image = new Image();
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            
            image.Source = bitmap;
            image.Width = 100; // Default size
            image.Height = 100;
            image.Stretch = Stretch.Uniform;
            
            // Store both file path and base64 data
            string base64Data = ConvertImageToBase64(filePath);
            image.Tag = $"ImageData:{base64Data}";
            
            return image;
        }

        private Image CreateImageFromBase64(string base64Data, double width = 100, double height = 100)
        {
            var image = new Image();
            var bitmap = ConvertBase64ToImage(base64Data);
            
            image.Source = bitmap;
            image.Width = width;
            image.Height = height;
            image.Stretch = Stretch.Uniform;
            image.Tag = $"ImageData:{base64Data}";
            
            return image;
        }

        private string ConvertBitmapSourceToBase64(BitmapSource bitmapSource)
        {
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    byte[] imageBytes = stream.ToArray();
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert bitmap source to base64: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }

        #endregion

        private Image CreateBarcodeImage(string symbology, string data, double width = 120, double height = 60)
        {
            try
            {
                // Determine the barcode format and default data based on symbology
                ZXing.BarcodeFormat format;
                string defaultData;
                string displayText;

                switch (symbology.ToUpper())
                {
                    case "EAN13":
                        format = ZXing.BarcodeFormat.EAN_13;
                        defaultData = "4000000000004"; // Valid EAN-13 with checksum
                        displayText = "[EAN-13]";
                        break;
                    case "EAN8":
                        format = ZXing.BarcodeFormat.EAN_8;
                        defaultData = "40000007"; // Valid EAN-8 with checksum
                        displayText = "[EAN-8]";
                        break;
                    case "UPCA":
                        format = ZXing.BarcodeFormat.UPC_A;
                        defaultData = "012345678905"; // Valid UPC-A with checksum
                        displayText = "[UPC-A]";
                        break;
                    case "UPCE":
                        format = ZXing.BarcodeFormat.UPC_E;
                        defaultData = "01234565"; // Valid UPC-E with checksum
                        displayText = "[UPC-E]";
                        break;
                    case "CODE128":
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "HELLO123";
                        displayText = "[Code 128]";
                        break;
                    case "CODE39":
                        format = ZXing.BarcodeFormat.CODE_39;
                        defaultData = "HELLO39";
                        displayText = "[Code 39]";
                        break;
                    case "QRCODE":
                        format = ZXing.BarcodeFormat.QR_CODE;
                        defaultData = "Sample QR Data";
                        displayText = "[QR Code]";
                        break;
                    case "AZTEC":
                        format = ZXing.BarcodeFormat.AZTEC;
                        defaultData = "Sample Aztec Data";
                        displayText = "[Aztec]";
                        break;
                    case "DATAMATRIX":
                        format = ZXing.BarcodeFormat.DATA_MATRIX;
                        defaultData = "Sample Data Matrix";
                        displayText = "[Data Matrix]";
                        break;
                    case "GS1DATABAR":
                        // Use Code 128 with GS1 formatting since RSS_14 support is limited in ZXing
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "]C101234567890128"; // GS1-128 format with application identifier
                        displayText = "[GS1 DataBar]";
                        break;
                    case "PDF417":
                        format = ZXing.BarcodeFormat.PDF_417;
                        defaultData = "Sample PDF417 Data";
                        displayText = "[PDF417]";
                        break;
                    default:
                        // Fallback to Code128
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "FALLBACK";
                        displayText = "[Barcode]";
                        break;
                }

                // Use default data if none provided
                if (string.IsNullOrEmpty(data))
                    data = defaultData;

                // Validate and adjust data for specific formats
                data = ValidateBarcodeData(symbology, data);

                var writer = new ZXing.BarcodeWriterPixelData();
                writer.Format = format;
                
                // Set encoding options based on symbology type
                var options = new ZXing.Common.EncodingOptions
                {
                    Width = (int)width * 2,
                    Height = (int)height * 2,
                    Margin = 1
                };

                // Special settings for specific formats
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
                
                // Create a visual container for the fallback
                var canvas = new Canvas
                {
                    Width = width,
                    Height = height,
                    Background = Brushes.LightGray
                };
                
                Canvas.SetLeft(fallbackText, 0);
                Canvas.SetTop(fallbackText, height / 2 - 10);
                canvas.Children.Add(fallbackText);
                
                // Convert Canvas to Image
                var renderTarget = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(canvas);
                
                var image = new Image
                {
                    Source = renderTarget,
                    Width = width,
                    Height = height,
                    Tag = $"BarcodeData:{symbology}:{data ?? ""}"
                };
                
                return image;
            }
        }

        private EvoDesigner.Models.LabelElementType GetLabelElementTypeFromBarcodeType(string barcodeType)
        {
            switch (barcodeType.ToUpper())
            {
                case "EAN13": return EvoDesigner.Models.LabelElementType.EAN13;
                case "EAN8": return EvoDesigner.Models.LabelElementType.EAN8;
                case "UPCA": return EvoDesigner.Models.LabelElementType.UPCA;
                case "UPCE": return EvoDesigner.Models.LabelElementType.UPCE;
                case "CODE128": return EvoDesigner.Models.LabelElementType.Code128;
                case "CODE39": return EvoDesigner.Models.LabelElementType.Code39;
                case "AZTEC": return EvoDesigner.Models.LabelElementType.Aztec;
                case "DATAMATRIX": return EvoDesigner.Models.LabelElementType.DataMatrix;
                case "GS1DATABAR": return EvoDesigner.Models.LabelElementType.GS1DataBar;
                case "PDF417": return EvoDesigner.Models.LabelElementType.PDF417;
                case "QRCODE": return EvoDesigner.Models.LabelElementType.QRCode;
                default:
                    // Legacy support
                    return EvoDesigner.Models.LabelElementType.EAN13;
            }
        }

        private string GetBarcodeTypeFromElement(EvoDesigner.Models.LabelElement element)
        {
            switch (element.Type)
            {
                case EvoDesigner.Models.LabelElementType.EAN13:
                case EvoDesigner.Models.LabelElementType.Barcode13:
                    return "EAN13";
                case EvoDesigner.Models.LabelElementType.EAN8:
                case EvoDesigner.Models.LabelElementType.Barcode8:
                    return "EAN8";
                case EvoDesigner.Models.LabelElementType.UPCA:
                    return "UPCA";
                case EvoDesigner.Models.LabelElementType.UPCE:
                    return "UPCE";
                case EvoDesigner.Models.LabelElementType.Code128:
                    return "Code128";
                case EvoDesigner.Models.LabelElementType.Code39:
                    return "Code39";
                case EvoDesigner.Models.LabelElementType.Aztec:
                    return "Aztec";
                case EvoDesigner.Models.LabelElementType.DataMatrix:
                    return "DataMatrix";
                case EvoDesigner.Models.LabelElementType.GS1DataBar:
                    return "GS1DataBar";
                case EvoDesigner.Models.LabelElementType.PDF417:
                    return "PDF417";
                case EvoDesigner.Models.LabelElementType.QRCode:
                    return "QRCode";
                default:
                    return element.BarcodeType ?? "EAN13";
            }
        }

        private string GetDefaultBarcodeData(string barcodeType)
        {
            switch (barcodeType.ToUpper())
            {
                case "EAN13": return "4000000000004"; // Valid EAN-13 with checksum
                case "EAN8": return "40000007"; // Valid EAN-8 with checksum
                case "UPCA": return "012345678905"; // Valid UPC-A with checksum
                case "UPCE": return "01234565"; // Valid UPC-E with checksum
                case "CODE128": return "HELLO123";
                case "CODE39": return "HELLO39";
                case "QRCODE": return "Sample QR Data";
                case "AZTEC": return "Sample Aztec Data";
                case "DATAMATRIX": return "Sample Data Matrix";
                case "GS1DATABAR": return "]C101234567890128"; // GS1-128 format with application identifier
                case "PDF417": return "Sample PDF417 Data";
                default: return "Sample Data";
            }
        }

        private string ValidateBarcodeData(string symbology, string data)
        {
            switch (symbology.ToUpper())
            {
                case "EAN13":
                    // EAN-13 needs exactly 13 digits
                    var ean13 = new string(data.Where(char.IsDigit).ToArray());
                    if (ean13.Length < 12)
                        ean13 = ean13.PadRight(12, '0');
                    else if (ean13.Length > 12)
                        ean13 = ean13.Substring(0, 12);
                    // Calculate checksum
                    var sum = 0;
                    for (int i = 0; i < 12; i++)
                        sum += (ean13[i] - '0') * (i % 2 == 0 ? 1 : 3);
                    var checksum = (10 - (sum % 10)) % 10;
                    return ean13 + checksum;
                    
                case "EAN8":
                    // EAN-8 needs exactly 8 digits
                    var ean8 = new string(data.Where(char.IsDigit).ToArray());
                    if (ean8.Length < 7)
                        ean8 = ean8.PadRight(7, '0');
                    else if (ean8.Length > 7)
                        ean8 = ean8.Substring(0, 7);
                    // Calculate checksum
                    var sum8 = 0;
                    for (int i = 0; i < 7; i++)
                        sum8 += (ean8[i] - '0') * (i % 2 == 0 ? 3 : 1);
                    var checksum8 = (10 - (sum8 % 10)) % 10;
                    return ean8 + checksum8;
                    
                case "GS1DATABAR":
                    // Use GS1-128 format (Code 128 with GS1 application identifier)
                    var gs1 = new string(data.Where(char.IsDigit).ToArray());
                    if (gs1.Length < 14)
                        gs1 = gs1.PadLeft(14, '0');
                    else if (gs1.Length > 14)
                        gs1 = gs1.Substring(0, 14);
                    return "]C1" + gs1; // ]C1 is the GS1-128 application identifier for GTIN
                    
                case "UPCA":
                    // UPC-A needs exactly 12 digits
                    var upca = new string(data.Where(char.IsDigit).ToArray());
                    if (upca.Length < 11)
                        upca = upca.PadRight(11, '0');
                    else if (upca.Length > 11)
                        upca = upca.Substring(0, 11);
                    // Calculate UPC checksum
                    var sumUpc = 0;
                    for (int i = 0; i < 11; i++)
                        sumUpc += (upca[i] - '0') * (i % 2 == 0 ? 3 : 1);
                    var checksumUpc = (10 - (sumUpc % 10)) % 10;
                    return upca + checksumUpc;
                    
                case "UPCE":
                    // UPC-E handling - simplified
                    var upce = new string(data.Where(char.IsDigit).ToArray());
                    if (upce.Length < 8)
                        upce = upce.PadRight(8, '0');
                    else if (upce.Length > 8)
                        upce = upce.Substring(0, 8);
                    return upce;
                    
                default:
                    return data; // No validation for other formats
            }
        }

        private string GetBarcodeDisplayName(string symbology)
        {
            switch (symbology.ToUpper())
            {
                case "EAN13": return "EAN-13";
                case "EAN8": return "EAN-8";
                case "UPCA": return "UPC-A";
                case "UPCE": return "UPC-E";
                case "CODE128": return "Code 128";
                case "CODE39": return "Code 39";
                case "QRCODE": return "QR Code";
                case "AZTEC": return "Aztec";
                case "DATAMATRIX": return "Data Matrix";
                case "GS1DATABAR": return "GS1 DataBar";
                case "PDF417": return "PDF417";
                default: return symbology;
            }
        }

        private void AttachToolboxDragHandlers()
        {
            // Find buttons by content and attach drag handlers
            var buttons = FindElementsByType<Button>(Content as DependencyObject);
            
            foreach (var btn in buttons)
            {
                var content = btn.Content?.ToString();
                
                // Basic elements
                if (content?.Contains("Text") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Text");
                else if (content?.Contains("Image") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Image");
                
                // Drawing tools
                else if (content?.Contains("Line") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Line");
                else if (content?.Contains("Rectangle") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Rectangle");
                
                // Retail checkout - updated to match emoji content
                else if (content?.Contains("EAN-13") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("EAN13");
                else if (content?.Contains("EAN-8") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("EAN8");
                else if (content?.Contains("UPC-A") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("UPCA");
                else if (content?.Contains("UPC-E") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("UPCE");
                
                // Logistics
                else if (content?.Contains("Code 128") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Code128");
                else if (content?.Contains("Code 39") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Code39");
                
                // Mobile/Consumer
                else if (content?.Contains("QR Code") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("QRCode");
                else if (content?.Contains("Aztec") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("Aztec");
                
                // Healthcare/Industrial
                else if (content?.Contains("DataMatrix") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("DataMatrix");
                else if (content?.Contains("GS1 DataBar") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("GS1DataBar");
                
                // Government
                else if (content?.Contains("PDF417") == true)
                    btn.PreviewMouseDown += (s, e) => StartDrag("PDF417");
            }
        }

        private List<T> FindElementsByType<T>(DependencyObject parent) where T : DependencyObject
        {
            var result = new List<T>();
            if (parent == null) return result;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T item) result.Add(item);
                result.AddRange(FindElementsByType<T>(child));
            }
            return result;
        }

        private void StartDrag(string elementType)
        {
            DragDrop.DoDragDrop(this, elementType, DragDropEffects.Copy);
        }

        private void LabelCanvas_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var position = e.GetPosition(LabelCanvas);
                var adjustedPosition = SuggestOptimalPosition(position);

                // Check if files are being dropped (e.g., image files from Explorer)
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0)
                    {
                        string filePath = files[0];
                        string extension = System.IO.Path.GetExtension(filePath).ToLower();
                        
                        // Check if it's an image file
                        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" || extension == ".gif")
                        {
                            try
                            {
                                // Create an image element with base64 storage
                                var image = CreateImageFromFile(filePath);
                                image.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                                
                                // Use command to add the image
                                var addCommand = new AddElementCommand(LabelCanvas, image, adjustedPosition.X, adjustedPosition.Y, "Add image");
                                commandHistory.ExecuteCommand(addCommand);
                                
                                // Mark as dirty since we added an image
                                SetDirty(true);
                                
                                // Show a success message
                                if (adjustedPosition != position)
                                {
                                    ShowPositioningTooltip(image, "Image loaded and positioned within printable area");
                                }
                                else
                                {
                                    ShowPositioningTooltip(image, "Image loaded successfully");
                                }
                                return;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to load image: {ex.Message}", "Error", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                }

                // Handle toolbar element drops (existing functionality)
                var elementType = e.Data.GetData(typeof(string)) as string;
                if (elementType == null) return;

                UIElement newElement = null;
                switch (elementType)
                {
                    case "Text":
                        var textBlock = new TextBlock { Text = "Text", FontSize = 16, Foreground = Brushes.Black };
                        var border = new Border
                        {
                            Child = textBlock,
                            BorderBrush = Brushes.LightGray,
                            BorderThickness = new Thickness(1),
                            Background = Brushes.Transparent,
                            Width = 100,
                            Height = 30
                        };
                        newElement = border;
                        break;
                    case "Image":
                        // Prompt user to select an image file
                        var openFileDialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                            Title = "Select Image File"
                        };
                        
                        if (openFileDialog.ShowDialog() == true)
                        {
                            try
                            {
                                newElement = CreateImageFromFile(openFileDialog.FileName);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to load image: {ex.Message}", "Error", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                // Create placeholder rectangle as fallback
                                newElement = new Rectangle { Width = 60, Height = 60, Fill = Brushes.LightGray, Stroke = Brushes.DarkGray, StrokeThickness = 2, Tag = "No Image Selected" };
                            }
                        }
                        else
                        {
                            // User cancelled - create placeholder rectangle
                            newElement = new Rectangle { Width = 60, Height = 60, Fill = Brushes.LightGray, Stroke = Brushes.DarkGray, StrokeThickness = 2, Tag = "No Image Selected" };
                        }
                        break;
                    
                    case "Line":
                        // Create a container canvas for the line that can be resized
                        var lineContainer = new Canvas
                        {
                            Width = 100,
                            Height = 30,
                            Background = Brushes.Transparent
                        };
                        
                        var line = new System.Windows.Shapes.Line
                        {
                            X1 = 0,
                            Y1 = 15, // Half of container height
                            X2 = 100, // Full container width
                            Y2 = 15,
                            Stroke = Brushes.Black,
                            StrokeThickness = 1
                        };
                        
                        lineContainer.Children.Add(line);
                        newElement = lineContainer;
                        break;
                    
                    case "Rectangle":
                        var rectangle = new System.Windows.Shapes.Rectangle
                        {
                            Width = 100,
                            Height = 60,
                            Stroke = Brushes.Black,
                            StrokeThickness = 1,
                            Fill = Brushes.Transparent
                        };
                        newElement = rectangle;
                        break;
                    
                    // Retail Checkout
                    case "EAN13":
                        newElement = CreateBarcodeImage("EAN13", "4000000000004", 120, 60);
                        break;
                    case "EAN8":
                        newElement = CreateBarcodeImage("EAN8", "40000007", 80, 60);
                        break;
                    case "UPCA":
                        newElement = CreateBarcodeImage("UPCA", "012345678905", 120, 60);
                        break;
                    case "UPCE":
                        newElement = CreateBarcodeImage("UPCE", "01234565", 80, 60);
                        break;
                    
                    // Logistics
                    case "Code128":
                        newElement = CreateBarcodeImage("Code128", "HELLO123", 140, 60);
                        break;
                    case "Code39":
                        newElement = CreateBarcodeImage("Code39", "HELLO39", 140, 60);
                        break;
                    
                    // Mobile/Consumer
                    case "QRCode":
                        newElement = CreateBarcodeImage("QRCode", "Sample QR Data", 80, 80);
                        break;
                    case "Aztec":
                        newElement = CreateBarcodeImage("Aztec", "Sample Aztec Data", 80, 80);
                        break;
                    
                    // Healthcare/Industrial
                    case "DataMatrix":
                        newElement = CreateBarcodeImage("DataMatrix", "Sample Data Matrix", 80, 80);
                        break;
                    case "GS1DataBar":
                        newElement = CreateBarcodeImage("GS1DataBar", "01234567890123", 120, 60);
                        break;
                    
                    // Government
                    case "PDF417":
                        newElement = CreateBarcodeImage("PDF417", "Sample PDF417 Data", 140, 80);
                        break;
                        
                    // Legacy support
                    case "Barcode13":
                        newElement = CreateBarcodeImage("EAN13", "4000000000004", 120, 60);
                        break;
                    case "Barcode8":
                        newElement = CreateBarcodeImage("EAN8", "40000007", 80, 60);
                        break;
                }
                if (newElement != null)
                {
                    newElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    
                    // Use command to add the element
                    var addCommand = new AddElementCommand(LabelCanvas, newElement, adjustedPosition.X, adjustedPosition.Y, $"Add {elementType}");
                    commandHistory.ExecuteCommand(addCommand);
                    
                    // Mark as dirty since we added an element
                    SetDirty(true);
                    
                    // Show a tooltip if the element was repositioned
                    if (adjustedPosition != position)
                    {
                        ShowPositioningTooltip(newElement, "Element positioned within printable area");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Drop operation failed: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Point SuggestOptimalPosition(Point originalPosition)
        {
            double marginLeftPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginLeft);
            double marginTopPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginTop);
            double marginRightPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginRight);
            double marginBottomPx = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginBottom);

            double x = originalPosition.X;
            double y = originalPosition.Y;

            // Keep within printable area bounds
            if (x < marginLeftPx) x = marginLeftPx + 5; // Small offset from margin
            if (y < marginTopPx) y = marginTopPx + 5;
            if (x > LabelCanvas.Width - marginRightPx - 50) x = LabelCanvas.Width - marginRightPx - 55; // Account for element width
            if (y > LabelCanvas.Height - marginBottomPx - 20) y = LabelCanvas.Height - marginBottomPx - 25; // Account for element height

            return new Point(x, y);
        }

        private void ShowPositioningTooltip(UIElement element, string message)
        {
            if (element is FrameworkElement frameworkElement)
            {
                var tooltip = new ToolTip { Content = message, IsOpen = true };
                frameworkElement.ToolTip = tooltip;
                
                // Auto-hide tooltip after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, e) => {
                    tooltip.IsOpen = false;
                    frameworkElement.ToolTip = null;
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UIElement clickedElement = sender as UIElement;
            
            // Check if Ctrl is held for multi-selection
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (selectedElements.Contains(clickedElement))
                {
                    // Remove from selection
                    selectedElements.Remove(clickedElement);
                    HighlightElement(clickedElement, false);
                }
                else
                {
                    // Add to selection
                    selectedElements.Add(clickedElement);
                    HighlightElement(clickedElement, true);
                    
                    // Clear single element selection since we're building a multi-selection
                    if (selectedElement != null)
                    {
                        RemoveResizingHandles();
                        selectedElement = null;
                    }
                }
            }
            else
            {
                // Check if the clicked element is already part of a multi-selection
                if (selectedElements.Contains(clickedElement) && selectedElements.Count > 1)
                {
                    // Don't clear selection - we want to drag all selected elements
                    // Keep the multi-selection intact
                }
                else
                {
                    // Clear existing selection and select this element as single selection
                    ClearSelection();
                    selectedElement = clickedElement;
                    ShowResizingHandles(selectedElement);
                    ShowProperties(selectedElement);
                }
            }
            
            dragStart = e.GetPosition(LabelCanvas);
            isDragging = true;
            LabelCanvas.MouseMove += Canvas_MouseMove;
            LabelCanvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var pos = e.GetPosition(LabelCanvas);
                double dx = pos.X - dragStart.X;
                double dy = pos.Y - dragStart.Y;
                
                // Move single selected element
                if (selectedElement != null)
                {
                    double left = Canvas.GetLeft(selectedElement) + dx;
                    double top = Canvas.GetTop(selectedElement) + dy;
                    Canvas.SetLeft(selectedElement, left);
                    Canvas.SetTop(selectedElement, top);
                }
                
                // Move multiple selected elements
                foreach (var element in selectedElements)
                {
                    double left = Canvas.GetLeft(element) + dx;
                    double top = Canvas.GetTop(element) + dy;
                    Canvas.SetLeft(element, left);
                    Canvas.SetTop(element, top);
                }
                
                dragStart = pos;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                // Mark as dirty since elements were moved
                SetDirty(true);
            }
            
            isDragging = false;
            LabelCanvas.MouseMove -= Canvas_MouseMove;
            LabelCanvas.MouseLeftButtonUp -= Canvas_MouseLeftButtonUp;
            
            // End selection if active
            if (isSelecting)
            {
                EndSelection();
                e.Handled = true;
            }
        }

        private void LabelCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if clicking on the canvas (not on an element)
            if (e.Source == LabelCanvas)
            {
                // Clear existing selection
                ClearSelection();
                
                // Start rectangle selection
                selectionStart = e.GetPosition(LabelCanvas);
                isSelecting = true;
                LabelCanvas.MouseMove += LabelCanvas_SelectionMouseMove;
                LabelCanvas.MouseLeftButtonUp += LabelCanvas_SelectionMouseUp;
                LabelCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void CanvasContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow selection to start from anywhere in the canvas container area
            // We'll be very permissive - allow selection unless it's clearly a draggable label element
            
            var clickedElement = e.Source as UIElement;
            bool shouldStartSelection = true;
            
            // Only prevent selection if we're clicking on a known draggable element
            if (clickedElement is FrameworkElement fe)
            {
                // Check if this element has our custom drag handling attached
                // Label elements are added through our CreateElement methods and have specific types
                if (fe is TextBlock || fe is Image)
                {
                    // Check if this is inside LabelCanvas (a label element)
                    var parent = fe.Parent;
                    while (parent != null)
                    {
                        if (parent == LabelCanvas)
                        {
                            shouldStartSelection = false;
                            break;
                        }
                        if (parent is FrameworkElement parentFe)
                            parent = parentFe.Parent;
                        else
                            break;
                    }
                }
            }
            
            if (shouldStartSelection)
            {
                // Clear existing selection
                ClearSelection();
                
                // Start rectangle selection using container coordinates
                var containerPosition = e.GetPosition(CanvasContainer);
                selectionStart = containerPosition;
                
                isSelecting = true;
                CanvasContainer.MouseMove += CanvasContainer_SelectionMouseMove;
                CanvasContainer.MouseLeftButtonUp += CanvasContainer_SelectionMouseUp;
                CanvasContainer.CaptureMouse();
                e.Handled = true;
            }
        }

        private void CanvasContainer_SelectionMouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                var containerPosition = e.GetPosition(CanvasContainer);
                var canvasPosition = new Point(
                    containerPosition.X - Canvas.GetLeft(LabelCanvas),
                    containerPosition.Y - Canvas.GetTop(LabelCanvas)
                );
                UpdateSelectionRectangle(canvasPosition);
            }
        }

        private void CanvasContainer_SelectionMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isSelecting)
            {
                EndSelection();
                CanvasContainer.MouseMove -= CanvasContainer_SelectionMouseMove;
                CanvasContainer.MouseLeftButtonUp -= CanvasContainer_SelectionMouseUp;
                CanvasContainer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow selection to start from the scroll viewer area (outside canvas container)
            if (e.Source == CanvasScrollViewer)
            {
                // Clear existing selection
                ClearSelection();
                
                // Start rectangle selection using ScrollViewer coordinates but convert to LabelCanvas coordinates
                var scrollPosition = e.GetPosition(CanvasScrollViewer);
                var containerPosition = e.GetPosition(CanvasContainer);
                selectionStart = new Point(
                    containerPosition.X - Canvas.GetLeft(LabelCanvas),
                    containerPosition.Y - Canvas.GetTop(LabelCanvas)
                );
                
                isSelecting = true;
                CanvasScrollViewer.MouseMove += ScrollViewer_SelectionMouseMove;
                CanvasScrollViewer.MouseLeftButtonUp += ScrollViewer_SelectionMouseUp;
                CanvasScrollViewer.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ScrollViewer_SelectionMouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                var containerPosition = e.GetPosition(CanvasContainer);
                var canvasPosition = new Point(
                    containerPosition.X - Canvas.GetLeft(LabelCanvas),
                    containerPosition.Y - Canvas.GetTop(LabelCanvas)
                );
                UpdateSelectionRectangle(canvasPosition);
            }
        }

        private void ScrollViewer_SelectionMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isSelecting)
            {
                EndSelection();
                CanvasScrollViewer.MouseMove -= ScrollViewer_SelectionMouseMove;
                CanvasScrollViewer.MouseLeftButtonUp -= ScrollViewer_SelectionMouseUp;
                CanvasScrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void LabelCanvas_SelectionMouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                UpdateSelectionRectangle(e.GetPosition(LabelCanvas));
            }
        }

        private void LabelCanvas_SelectionMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isSelecting)
            {
                EndSelection();
                LabelCanvas.MouseMove -= LabelCanvas_SelectionMouseMove;
                LabelCanvas.MouseLeftButtonUp -= LabelCanvas_SelectionMouseUp;
                LabelCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void UpdateSelectionRectangle(Point currentPosition)
        {
            if (selectionRectangle == null)
            {
                selectionRectangle = new Rectangle
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 255))
                };
                LabelCanvas.Children.Add(selectionRectangle);
            }

            double left = Math.Min(selectionStart.X, currentPosition.X);
            double top = Math.Min(selectionStart.Y, currentPosition.Y);
            double width = Math.Abs(currentPosition.X - selectionStart.X);
            double height = Math.Abs(currentPosition.Y - selectionStart.Y);

            Canvas.SetLeft(selectionRectangle, left);
            Canvas.SetTop(selectionRectangle, top);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;
        }

        private void EndSelection()
        {
            isSelecting = false;
            
            if (selectionRectangle != null)
            {
                // Get selection bounds
                double left = Canvas.GetLeft(selectionRectangle);
                double top = Canvas.GetTop(selectionRectangle);
                double right = left + selectionRectangle.Width;
                double bottom = top + selectionRectangle.Height;

                // Find elements within selection rectangle
                foreach (UIElement element in LabelCanvas.Children.OfType<UIElement>())
                {
                    if (element == selectionRectangle) continue; // Skip the selection rectangle itself

                    if (element is FrameworkElement fe)
                    {
                        double elementLeft = Canvas.GetLeft(fe);
                        double elementTop = Canvas.GetTop(fe);
                        double elementRight = elementLeft + fe.ActualWidth;
                        double elementBottom = elementTop + fe.ActualHeight;

                        // Check if element is within selection bounds
                        if (elementLeft >= left && elementTop >= top && 
                            elementRight <= right && elementBottom <= bottom)
                        {
                            selectedElements.Add(element);
                            HighlightElement(element, true);
                        }
                    }
                }

                // Remove selection rectangle
                LabelCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
            }
        }

        private void ClearSelection()
        {
            foreach (var element in selectedElements)
            {
                HighlightElement(element, false);
            }
            selectedElements.Clear();
            
            // Also clear single element selection
            if (selectedElement != null)
            {
                HighlightElement(selectedElement, false);
                selectedElement = null;
                RemoveResizingHandles();
            }
        }

        private void HighlightElement(UIElement element, bool highlight)
        {
            if (element is FrameworkElement fe)
            {
                if (highlight)
                {
                    fe.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Blue,
                        BlurRadius = 5,
                        ShadowDepth = 0,
                        Opacity = 0.7
                    };
                }
                else
                {
                    fe.Effect = null;
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                    DeleteSelectedElements();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    ClearSelection();
                    e.Handled = true;
                    break;
                case Key.A when (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)):
                    SelectAllElements();
                    e.Handled = true;
                    break;
                case Key.Z when (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)):
                    Undo();
                    e.Handled = true;
                    break;
                case Key.Y when (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)):
                    Redo();
                    e.Handled = true;
                    break;
                case Key.C when (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)):
                    CopySelectedElements();
                    e.Handled = true;
                    break;
                case Key.V when (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)):
                    PasteElements();
                    e.Handled = true;
                    break;
                case Key.X when (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)):
                    CutSelectedElements();
                    e.Handled = true;
                    break;
                case Key.Left:
                    MoveSelectedElements(-1, 0);
                    e.Handled = true;
                    break;
                case Key.Right:
                    MoveSelectedElements(1, 0);
                    e.Handled = true;
                    break;
                case Key.Up:
                    MoveSelectedElements(0, -1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    MoveSelectedElements(0, 1);
                    e.Handled = true;
                    break;
            }
        }

        private void DeleteSelectedElements()
        {
            var elementsToDelete = new List<UIElement>();
            
            // Collect elements to delete
            if (selectedElement != null)
            {
                elementsToDelete.Add(selectedElement);
                selectedElement = null;
                RemoveResizingHandles();
            }
            
            elementsToDelete.AddRange(selectedElements);
            
            if (elementsToDelete.Count > 0)
            {
                // Create and execute delete command
                var deleteCommand = new DeleteElementsCommand(LabelCanvas, elementsToDelete, 
                    elementsToDelete.Count == 1 ? "Delete element" : $"Delete {elementsToDelete.Count} elements");
                commandHistory.ExecuteCommand(deleteCommand);
                
                // Mark as dirty since we deleted elements
                SetDirty(true);
                
                selectedElements.Clear();
            }
        }

        private void SelectAllElements()
        {
            ClearSelection();
            foreach (UIElement element in LabelCanvas.Children.OfType<UIElement>())
            {
                if (element != selectionRectangle) // Don't select the selection rectangle
                {
                    selectedElements.Add(element);
                    HighlightElement(element, true);
                }
            }
        }

        private void MoveSelectedElements(double deltaX, double deltaY)
        {
            // Determine movement step size (1 pixel for normal, 10 pixels with Ctrl held)
            var stepSize = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) ? 10.0 : 1.0;
            deltaX *= stepSize;
            deltaY *= stepSize;

            var elementsToMove = new List<UIElement>();
            
            // Collect elements to move
            if (selectedElement != null)
            {
                elementsToMove.Add(selectedElement);
            }
            elementsToMove.AddRange(selectedElements);

            if (elementsToMove.Count > 0)
            {
                // Create and execute move command
                var moveCommand = new MoveElementsCommand(elementsToMove, deltaX, deltaY, 
                    $"Move {(elementsToMove.Count == 1 ? "element" : elementsToMove.Count + " elements")}");
                commandHistory.ExecuteCommand(moveCommand);
                
                // Update resize handles if they exist for single selection
                if (selectedElement != null)
                {
                    ShowResizingHandles(selectedElement);
                }
            }
        }

        private void Undo()
        {
            commandHistory.Undo();
        }

        private void Redo()
        {
            commandHistory.Redo();
        }

        private void CopySelectedElements()
        {
            if (selectedElements.Count == 0 && selectedElement == null)
                return;

            clipboardElements.Clear();
            
            // Collect all selected elements
            var elementsToCopy = new List<UIElement>();
            if (selectedElement != null)
                elementsToCopy.Add(selectedElement);
            elementsToCopy.AddRange(selectedElements);

            // Convert each UI element to LabelElement model for clipboard storage
            foreach (UIElement element in elementsToCopy)
            {
                var labelElement = ConvertUIElementToLabelElement(element);
                if (labelElement != null)
                    clipboardElements.Add(labelElement);
            }
        }

        private void CutSelectedElements()
        {
            CopySelectedElements();
            DeleteSelectedElements();
        }

        private void PasteElements()
        {
            if (clipboardElements.Count == 0)
                return;

            var elementsToAdd = new List<UIElement>();
            var addCommands = new List<AddElementCommand>();
            
            // Calculate offset for pasted elements (20 pixels down and right)
            double offsetX = 20;
            double offsetY = 20;

            foreach (var labelElement in clipboardElements)
            {
                // Create a copy of the label element with offset position
                var copiedElement = new EvoDesigner.Models.LabelElement
                {
                    Type = labelElement.Type,
                    X = labelElement.X + offsetX,
                    Y = labelElement.Y + offsetY,
                    Width = labelElement.Width,
                    Height = labelElement.Height,
                    Content = labelElement.Content,
                    BarcodeType = labelElement.BarcodeType,
                    FontFamily = labelElement.FontFamily,
                    FontSize = labelElement.FontSize,
                    FontWeight = labelElement.FontWeight,
                    FontStyle = labelElement.FontStyle,
                    Foreground = labelElement.Foreground,
                    TextDecorations = labelElement.TextDecorations,
                    ImageData = labelElement.ImageData,
                    Rotation = labelElement.Rotation
                };

                // Create UI element from the copied model
                var uiElement = CreateElementFromTemplate(copiedElement);
                if (uiElement != null)
                {
                    // Add the event handler to make the element selectable
                    uiElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    
                    elementsToAdd.Add(uiElement);
                    var addCommand = new AddElementCommand(LabelCanvas, uiElement, copiedElement.X, copiedElement.Y, "Paste element");
                    addCommands.Add(addCommand);
                }
            }

            // Execute all add commands as a batch
            if (addCommands.Count > 0)
            {
                var batchCommand = new BatchCommand(addCommands.Cast<Commands.ICommand>().ToList(), 
                    $"Paste {addCommands.Count} element{(addCommands.Count == 1 ? "" : "s")}");
                commandHistory.ExecuteCommand(batchCommand);
                
                // Select the pasted elements
                ClearSelection();
                foreach (var element in elementsToAdd)
                {
                    selectedElements.Add(element);
                    HighlightElement(element, true);
                }
                
                SetDirty(true);
            }
        }

        private EvoDesigner.Models.LabelElement? ConvertUIElementToLabelElement(UIElement element)
        {
            var labelElement = new EvoDesigner.Models.LabelElement
            {
                X = Canvas.GetLeft(element),
                Y = Canvas.GetTop(element),
                Width = element is FrameworkElement fe ? fe.Width : 100,
                Height = element is FrameworkElement fe2 ? fe2.Height : 30,
                Rotation = elementRotations.ContainsKey(element) ? elementRotations[element] : 
                          (element.RenderTransform is RotateTransform rotateTransform ? rotateTransform.Angle : 0)
            };

            if (element is TextBlock tb)
            {
                if (tb.Text == "[EAN-13]")
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode13;
                else if (tb.Text == "[EAN-8]")
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode8;
                else if (tb.Text == "[QR Code]")
                    labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                else
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                    
                labelElement.Content = tb.Text;
                labelElement.FontSize = tb.FontSize;
                labelElement.FontFamily = tb.FontFamily.ToString();
                labelElement.FontWeight = tb.FontWeight.ToString();
                labelElement.FontStyle = tb.FontStyle.ToString();
                labelElement.Foreground = tb.Foreground.ToString();
                labelElement.TextDecorations = GetTextDecorationsString(tb.TextDecorations);
            }
            else if (element is System.Windows.Shapes.Rectangle rect)
            {
                labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                string tagStr = rect.Tag?.ToString() ?? "";
                
                // Handle file path in rectangle tag - convert to base64
                if (!string.IsNullOrEmpty(tagStr) && tagStr != "Missing Image Data" && tagStr != "No Image Data")
                {
                    if (System.IO.File.Exists(tagStr))
                    {
                        labelElement.ImageData = ConvertImageToBase64(tagStr);
                        labelElement.Content = ""; // Clear file path since we have base64 data
                    }
                    else
                    {
                        labelElement.Content = tagStr; // Keep as fallback
                    }
                }
            }
            else if (element is Image imageElement)
            {
                if (imageElement.Tag?.ToString() == "QRCode" || imageElement.Tag?.ToString()?.StartsWith("QRCodeData:") == true)
                {
                    labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                    // Extract data from tag if available
                    var tagStr = imageElement.Tag?.ToString();
                    if (tagStr?.StartsWith("QRCodeData:") == true)
                        labelElement.Content = tagStr.Substring("QRCodeData:".Length);
                    else
                        labelElement.Content = "Sample QR Data";
                }
                else if (imageElement.Tag?.ToString()?.StartsWith("BarcodeData:") == true)
                {
                    var tagStr = imageElement.Tag?.ToString();
                    if (tagStr != null)
                    {
                        var parts = tagStr.Split(':');
                        if (parts.Length >= 3)
                        {
                            var barcodeType = parts[1];
                            var barcodeData = string.Join(":", parts.Skip(2));
                            
                            // Map barcode type to enum
                            labelElement.Type = GetLabelElementTypeFromBarcodeType(barcodeType);
                            labelElement.Content = barcodeData;
                            labelElement.BarcodeType = barcodeType;
                        }
                        else
                        {
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                            labelElement.Content = "Invalid barcode data";
                        }
                    }
                }
                else
                {
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                    // Try to extract image data
                    if (imageElement.Source is BitmapSource bitmapSource)
                    {
                        labelElement.ImageData = ConvertBitmapSourceToBase64(bitmapSource);
                    }
                }
            }
            else
            {
                // Unsupported element type
                return null;
            }

            return labelElement;
        }

        private void ShowResizingHandles(UIElement element)
        {
            RemoveResizingHandles();
            if (element is FrameworkElement fe)
            {
                resizeThumb = new Thumb
                {
                    Width = 12,
                    Height = 12,
                    Background = Brushes.Blue,
                    Cursor = Cursors.SizeNWSE
                };
                Canvas.SetLeft(resizeThumb, Canvas.GetLeft(fe) + fe.ActualWidth - 6);
                Canvas.SetTop(resizeThumb, Canvas.GetTop(fe) + fe.ActualHeight - 6);
                resizeThumb.DragDelta += (s, e) =>
                {
                    fe.Width = Math.Max(20, fe.ActualWidth + e.HorizontalChange);
                    fe.Height = Math.Max(20, fe.ActualHeight + e.VerticalChange);
                    Canvas.SetLeft(resizeThumb, Canvas.GetLeft(fe) + fe.Width - 6);
                    Canvas.SetTop(resizeThumb, Canvas.GetTop(fe) + fe.Height - 6);
                    
                    // Update line element if this is a line container
                    if (fe is Canvas lineCanvas && lineCanvas.Children.Count > 0 && lineCanvas.Children[0] is System.Windows.Shapes.Line lineElement)
                    {
                        lineElement.X2 = fe.Width;
                        lineElement.Y1 = fe.Height / 2;
                        lineElement.Y2 = fe.Height / 2;
                    }
                };
                LabelCanvas.Children.Add(resizeThumb);
            }
        }

        private void RemoveResizingHandles()
        {
            if (resizeThumb != null)
            {
                LabelCanvas.Children.Remove(resizeThumb);
                resizeThumb = null;
            }
        }

        private void ShowProperties(UIElement element)
        {
            try
            {
                ElementPropertiesPanel.Children.Clear();
                
                // Handle Border containers with TextBlock children (new text elements)
                if (element is Border border && border.Child is TextBlock tb)
                {
                    // Debug: Log current TextBlock properties
                    var debugMsg = $"[DEBUG] ShowProperties for TextBlock: Text='{tb.Text}', FontSize={tb.FontSize}\n";
                    System.IO.File.AppendAllText("debug.log", debugMsg);
                    System.Console.WriteLine($"[DEBUG] ShowProperties for TextBlock: Text='{tb.Text}', FontSize={tb.FontSize}");
                    
                    // Text property
                    var textLabel = new Label { Content = "Text:" };
                    textLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(textLabel);
                    
                    var textBox = new TextBox { Text = tb.Text };
                    textBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    textBox.TextChanged += (s, e) => { 
                        tb.Text = textBox.Text; 
                        SetDirty(true); 
                        var changeMsg = $"[DEBUG] Text changed to: '{tb.Text}'\n";
                        System.IO.File.AppendAllText("debug.log", changeMsg);
                    };
                    ElementPropertiesPanel.Children.Add(textBox);

                    // Font Family property
                    var fontFamilyLabel = new Label { Content = "Font Family:" };
                    fontFamilyLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(fontFamilyLabel);
                    
                    var fontFamilyBox = new ComboBox { 
                        ItemsSource = System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f),
                        SelectedItem = tb.FontFamily.Source 
                    };
                    fontFamilyBox.Style = (Style)FindResource("ModernComboBoxStyle");
                    fontFamilyBox.SelectionChanged += (s, e) => {
                        if (fontFamilyBox.SelectedItem != null) {
                            tb.FontFamily = new FontFamily(fontFamilyBox.SelectedItem.ToString());
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(fontFamilyBox);

                    // Font Size and Text Color properties on same line
                    var fontSizeColorGrid = new Grid();
                    fontSizeColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fontSizeColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fontSizeColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    fontSizeColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    // Labels row
                    var fontSizeLabel = new Label { Content = "Font Size:" };
                    fontSizeLabel.Style = (Style)FindResource("ModernLabelStyle");
                    Grid.SetRow(fontSizeLabel, 0);
                    Grid.SetColumn(fontSizeLabel, 0);
                    fontSizeColorGrid.Children.Add(fontSizeLabel);
                    
                    var colorLabel = new Label { Content = "Text Color:" };
                    colorLabel.Style = (Style)FindResource("ModernLabelStyle");
                    Grid.SetRow(colorLabel, 0);
                    Grid.SetColumn(colorLabel, 1);
                    fontSizeColorGrid.Children.Add(colorLabel);
                    
                    // TextBoxes row
                    var fontSizeBox = new TextBox { Text = tb.FontSize.ToString(), Margin = new Thickness(0, 0, 4, 0) };
                    fontSizeBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    fontSizeBox.TextChanged += (s, e) => {
                        if (double.TryParse(fontSizeBox.Text, out double size)) {
                            tb.FontSize = size;
                            SetDirty(true);
                        }
                    };
                    Grid.SetRow(fontSizeBox, 1);
                    Grid.SetColumn(fontSizeBox, 0);
                    fontSizeColorGrid.Children.Add(fontSizeBox);
                    
                    var currentColor = ((tb.Foreground as SolidColorBrush)?.Color) ?? Colors.Black;
                    var colorPicker = CreateColorPicker(currentColor, (newColor) => {
                        tb.Foreground = new SolidColorBrush(newColor);
                        SetDirty(true);
                    });
                    Grid.SetRow(colorPicker, 1);
                    Grid.SetColumn(colorPicker, 1);
                    fontSizeColorGrid.Children.Add(colorPicker);
                    
                    ElementPropertiesPanel.Children.Add(fontSizeColorGrid);
                }
                // Handle legacy TextBlock elements (for backward compatibility)
                else if (element is TextBlock directTb)
                {
                    // Text property
                    var textLabel = new Label { Content = "Text:" };
                    textLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(textLabel);
                    
                    var textBox = new TextBox { Text = directTb.Text };
                    textBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    textBox.TextChanged += (s, e) => { 
                        directTb.Text = textBox.Text; 
                        SetDirty(true); 
                    };
                    ElementPropertiesPanel.Children.Add(textBox);

                    // Font Family property
                    var fontFamilyLabel = new Label { Content = "Font Family:" };
                    fontFamilyLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(fontFamilyLabel);
                    
                    var fontFamilyBox = new ComboBox { 
                        ItemsSource = System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f),
                        SelectedItem = directTb.FontFamily.Source 
                    };
                    fontFamilyBox.Style = (Style)FindResource("ModernComboBoxStyle");
                    fontFamilyBox.SelectionChanged += (s, e) => {
                        if (fontFamilyBox.SelectedItem != null) {
                            directTb.FontFamily = new FontFamily(fontFamilyBox.SelectedItem.ToString());
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(fontFamilyBox);

                    // Font Size and Text Color properties on same line
                    var fontSizeColorGrid = new Grid();
                    fontSizeColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fontSizeColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fontSizeColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    fontSizeColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    // Labels row
                    var fontSizeLabel = new Label { Content = "Font Size:" };
                    fontSizeLabel.Style = (Style)FindResource("ModernLabelStyle");
                    Grid.SetRow(fontSizeLabel, 0);
                    Grid.SetColumn(fontSizeLabel, 0);
                    fontSizeColorGrid.Children.Add(fontSizeLabel);
                    
                    var colorLabel = new Label { Content = "Text Color:" };
                    colorLabel.Style = (Style)FindResource("ModernLabelStyle");
                    Grid.SetRow(colorLabel, 0);
                    Grid.SetColumn(colorLabel, 1);
                    fontSizeColorGrid.Children.Add(colorLabel);
                    
                    // TextBoxes row
                    var fontSizeBox = new TextBox { Text = directTb.FontSize.ToString(), Margin = new Thickness(0, 0, 4, 0) };
                    fontSizeBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    fontSizeBox.TextChanged += (s, e) => {
                        if (double.TryParse(fontSizeBox.Text, out double size)) {
                            directTb.FontSize = size;
                            SetDirty(true);
                        }
                    };
                    Grid.SetRow(fontSizeBox, 1);
                    Grid.SetColumn(fontSizeBox, 0);
                    fontSizeColorGrid.Children.Add(fontSizeBox);
                    
                    var colorBox = new TextBox { Text = (directTb.Foreground as SolidColorBrush)?.Color.ToString() ?? "#000000", Margin = new Thickness(4, 0, 0, 0) };
                    colorBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    colorBox.TextChanged += (s, e) => {
                        try { 
                            directTb.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(colorBox.Text)); 
                            SetDirty(true);
                        } catch { }
                    };
                    Grid.SetRow(colorBox, 1);
                    Grid.SetColumn(colorBox, 1);
                    fontSizeColorGrid.Children.Add(colorBox);
                    
                    ElementPropertiesPanel.Children.Add(fontSizeColorGrid);
                }
                else if (element is Rectangle rect)
                {
                    // Rectangle properties
                    var rectLabel = new Label { Content = "Rectangle Properties" };
                    rectLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(rectLabel);
                    
                    // Width property
                    var widthLabel = new Label { Content = "Width:" };
                    widthLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(widthLabel);
                    
                    var widthBox = new TextBox { Text = rect.Width.ToString() };
                    widthBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    widthBox.TextChanged += (s, e) => {
                        if (double.TryParse(widthBox.Text, out double w)) {
                            rect.Width = w;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(widthBox);

                    // Height property
                    var heightLabel = new Label { Content = "Height:" };
                    heightLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(heightLabel);
                    
                    var heightBox = new TextBox { Text = rect.Height.ToString() };
                    heightBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    heightBox.TextChanged += (s, e) => {
                        if (double.TryParse(heightBox.Text, out double h)) {
                            rect.Height = h;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(heightBox);
                    
                    // Border Width property
                    var borderWidthLabel = new Label { Content = "Border Width:" };
                    borderWidthLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(borderWidthLabel);
                    
                    var borderWidthBox = new TextBox { Text = rect.StrokeThickness.ToString() };
                    borderWidthBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    borderWidthBox.TextChanged += (s, e) => {
                        if (double.TryParse(borderWidthBox.Text, out double thickness)) {
                            rect.StrokeThickness = thickness;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(borderWidthBox);
                    
                    // Border Color property
                    var borderColorLabel = new System.Windows.Controls.Label { Content = "Border Color:" };
                    borderColorLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(borderColorLabel);
                    
                    var currentBorderColor = ((rect.Stroke as SolidColorBrush)?.Color) ?? Colors.Black;
                    var borderColorPicker = CreateColorPicker(currentBorderColor, (newColor) => {
                        rect.Stroke = new SolidColorBrush(newColor);
                        SetDirty(true);
                    });
                    ElementPropertiesPanel.Children.Add(borderColorPicker);
                }
                else if (element is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is System.Windows.Shapes.Line line)
                {
                    // Line container properties
                    var lineLabel = new Label { Content = "Line Properties" };
                    lineLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(lineLabel);
                    
                    // Length property (controls container width and line X2)
                    var lengthLabel = new Label { Content = "Length:" };
                    lengthLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(lengthLabel);
                    
                    var lengthBox = new TextBox { Text = canvas.Width.ToString() };
                    lengthBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    lengthBox.TextChanged += (s, e) => {
                        if (double.TryParse(lengthBox.Text, out double length)) {
                            canvas.Width = length;
                            line.X2 = length; // Update line end point
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(lengthBox);
                    
                    // Line Thickness property
                    var thicknessLabel = new Label { Content = "Line Width:" };
                    thicknessLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(thicknessLabel);
                    
                    var thicknessBox = new TextBox { Text = line.StrokeThickness.ToString() };
                    thicknessBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    thicknessBox.TextChanged += (s, e) => {
                        if (double.TryParse(thicknessBox.Text, out double thickness)) {
                            line.StrokeThickness = thickness;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(thicknessBox);
                    
                    // Line Color property
                    var lineColorLabel = new System.Windows.Controls.Label { Content = "Line Color:" };
                    lineColorLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(lineColorLabel);
                    
                    var currentLineColor = ((line.Stroke as SolidColorBrush)?.Color) ?? Colors.Black;
                    var lineColorPicker = CreateColorPicker(currentLineColor, (newColor) => {
                        line.Stroke = new SolidColorBrush(newColor);
                        SetDirty(true);
                    });
                    ElementPropertiesPanel.Children.Add(lineColorPicker);
                }
                else if (element is System.Windows.Shapes.Line directLine)
                {
                    // Direct Line properties (legacy support)
                    var lineLabel = new Label { Content = "Line Properties" };
                    lineLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(lineLabel);
                    
                    // Length property
                    var lengthLabel = new Label { Content = "Length:" };
                    lengthLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(lengthLabel);
                    
                    var lengthBox = new TextBox { Text = directLine.Width.ToString() };
                    lengthBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    lengthBox.TextChanged += (s, e) => {
                        if (double.TryParse(lengthBox.Text, out double w)) {
                            directLine.Width = w;
                            directLine.X2 = w; // Update line end point
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(lengthBox);
                    
                    // Line Thickness property
                    var thicknessLabel = new Label { Content = "Line Width:" };
                    thicknessLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(thicknessLabel);
                    
                    var thicknessBox = new TextBox { Text = directLine.StrokeThickness.ToString() };
                    thicknessBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    thicknessBox.TextChanged += (s, e) => {
                        if (double.TryParse(thicknessBox.Text, out double thickness)) {
                            directLine.StrokeThickness = thickness;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(thicknessBox);
                    
                    // Line Color property
                    var lineColorLabel = new System.Windows.Controls.Label { Content = "Line Color:" };
                    lineColorLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(lineColorLabel);
                    
                    var currentDirectLineColor = ((directLine.Stroke as SolidColorBrush)?.Color) ?? Colors.Black;
                    var directLineColorPicker = CreateColorPicker(currentDirectLineColor, (newColor) => {
                        directLine.Stroke = new SolidColorBrush(newColor);
                        SetDirty(true);
                    });
                    ElementPropertiesPanel.Children.Add(directLineColorPicker);
                }
                else if (element is Image img)
                {
                    // Basic image properties
                    var imageLabel = new Label { Content = "Image Properties" };
                    imageLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(imageLabel);
                    
                    // Width property
                    var widthLabel = new Label { Content = "Width:" };
                    widthLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(widthLabel);
                    
                    var widthBox = new TextBox { Text = img.Width.ToString() };
                    widthBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    widthBox.TextChanged += (s, e) => {
                        if (double.TryParse(widthBox.Text, out double w)) {
                            img.Width = w;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(widthBox);

                    // Height property
                    var heightLabel = new Label { Content = "Height:" };
                    heightLabel.Style = (Style)FindResource("ModernLabelStyle");
                    ElementPropertiesPanel.Children.Add(heightLabel);
                    
                    var heightBox = new TextBox { Text = img.Height.ToString() };
                    heightBox.Style = (Style)FindResource("ModernTextBoxStyle");
                    heightBox.TextChanged += (s, e) => {
                        if (double.TryParse(heightBox.Text, out double h)) {
                            img.Height = h;
                            SetDirty(true);
                        }
                    };
                    ElementPropertiesPanel.Children.Add(heightBox);
                }
                
                // Add rotation controls for all elements
                AddRotationControls(element);
            }
            catch (Exception ex)
            {
                // Fallback to prevent crash
                try
                {
                    ElementPropertiesPanel.Children.Clear();
                    ElementPropertiesPanel.Children.Add(new Label { Content = $"Error loading properties: {ex.Message}" });
                }
                catch
                {
                    // Do nothing if even this fails
                }
            }
        }

        private void AddRotationControls(UIElement element)
        {
            // Add a separator before rotation controls
            ElementPropertiesPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
            
            // Add rotation label
            var rotationLabel = new Label 
            { 
                Content = "Rotation:", 
                FontWeight = FontWeights.Bold,
                Style = (Style)FindResource("ModernLabelStyle")
            };
            ElementPropertiesPanel.Children.Add(rotationLabel);
            
            // Get current rotation from dictionary or RenderTransform
            double currentRotation = 0;
            if (elementRotations.ContainsKey(element))
            {
                currentRotation = elementRotations[element];
            }
            else if (element.RenderTransform is RotateTransform rotateTransform)
            {
                currentRotation = rotateTransform.Angle;
            }
            
            // Create rotation slider
            var rotationSlider = new Slider
            {
                Minimum = 0,
                Maximum = 360,
                Value = currentRotation,
                TickFrequency = 1,
                TickPlacement = TickPlacement.BottomRight,
                IsSnapToTickEnabled = false,
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            // Create rotation value display
            var rotationValueText = new TextBlock 
            { 
                Text = $"{currentRotation:F1}°",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            
            // Handle rotation changes
            rotationSlider.ValueChanged += (s, e) =>
            {
                double angle = rotationSlider.Value;
                rotationValueText.Text = $"{angle:F1}°";
                // Apply rotation transform to the element
                var rotateTransform = new RotateTransform(angle);
                if (element is FrameworkElement fe)
                {
                    fe.RenderTransformOrigin = new Point(0.5, 0.5);
                }
                element.RenderTransform = rotateTransform;
                // Store the angle in our rotation dictionary
                elementRotations[element] = angle;
                SetDirty(true);
            };
            
            ElementPropertiesPanel.Children.Add(rotationSlider);
            ElementPropertiesPanel.Children.Add(rotationValueText);
            
            // Add rotation shortcuts
            var shortcutsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            
            var rotate90Btn = new Button 
            { 
                Content = "90°", 
                Width = 50, 
                Margin = new Thickness(0, 0, 5, 0),
                Style = (Style)FindResource("CompactButtonStyle")
            };
            rotate90Btn.Click += (s, e) => { rotationSlider.Value = (rotationSlider.Value + 90) % 360; };
            shortcutsPanel.Children.Add(rotate90Btn);
            
            var rotate180Btn = new Button 
            { 
                Content = "180°", 
                Width = 55, 
                Margin = new Thickness(0, 0, 5, 0),
                Style = (Style)FindResource("CompactButtonStyle")
            };
            rotate180Btn.Click += (s, e) => { rotationSlider.Value = (rotationSlider.Value + 180) % 360; };
            shortcutsPanel.Children.Add(rotate180Btn);
            
            var resetBtn = new Button 
            { 
                Content = "Reset", 
                Width = 55,
                Style = (Style)FindResource("CompactButtonStyle")
            };
            resetBtn.Click += (s, e) => { rotationSlider.Value = 0; };
            shortcutsPanel.Children.Add(resetBtn);
            
            ElementPropertiesPanel.Children.Add(shortcutsPanel);
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Populate template with current canvas content
                CurrentTemplate.Elements.Clear();
                CurrentTemplate.TemplateName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath ?? "Untitled Template");
                
                // Save all canvas elements (excluding resize handles)
                foreach (UIElement child in LabelCanvas.Children)
                {
                    if (child is System.Windows.Controls.Primitives.Thumb) continue; // Skip resize handles
                    
                    var labelElement = new EvoDesigner.Models.LabelElement
                    {
                        X = Canvas.GetLeft(child),
                        Y = Canvas.GetTop(child),
                        Width = child is FrameworkElement fe ? fe.Width : 100,
                        Height = child is FrameworkElement fe2 ? fe2.Height : 30,
                        Rotation = elementRotations.ContainsKey(child) ? elementRotations[child] : 
                                  (child.RenderTransform is RotateTransform rotateTransform ? rotateTransform.Angle : 0)
                    };
                    
                    if (child is TextBlock tb)
                    {
                        if (tb.Text == "[EAN-13]")
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode13;
                        else if (tb.Text == "[EAN-8]")
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode8;
                        else if (tb.Text == "[QR Code]")
                            labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                        else
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                            
                        labelElement.Content = tb.Text;
                        labelElement.FontSize = tb.FontSize;
                        labelElement.FontFamily = tb.FontFamily.ToString();
                        labelElement.FontWeight = tb.FontWeight.ToString();
                        labelElement.FontStyle = tb.FontStyle.ToString();
                        labelElement.Foreground = tb.Foreground.ToString();
                        labelElement.TextDecorations = GetTextDecorationsString(tb.TextDecorations);
                    }
                    else if (child is Border border && border.Child is TextBlock borderText)
                    {
                        // Handle Border elements containing TextBlocks (new text elements created through UI)
                        if (borderText.Text == "[EAN-13]")
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode13;
                        else if (borderText.Text == "[EAN-8]")
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode8;
                        else if (borderText.Text == "[QR Code]")
                            labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                        else
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                            
                        labelElement.Content = borderText.Text;
                        labelElement.FontSize = borderText.FontSize;
                        labelElement.FontFamily = borderText.FontFamily.ToString();
                        labelElement.FontWeight = borderText.FontWeight.ToString();
                        labelElement.FontStyle = borderText.FontStyle.ToString();
                        labelElement.Foreground = borderText.Foreground.ToString();
                        labelElement.TextDecorations = GetTextDecorationsString(borderText.TextDecorations);
                    }
                    else if (child is System.Windows.Shapes.Rectangle rect)
                    {
                        // Check if this is a drawing rectangle or an image placeholder
                        string tagStr = rect.Tag?.ToString() ?? "";
                        
                        // If it's a transparent rectangle with stroke, it's a drawing rectangle
                        if (rect.Fill == Brushes.Transparent && rect.Stroke != null)
                        {
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Rectangle;
                            labelElement.BorderWidth = rect.StrokeThickness;
                            labelElement.BorderColor = (rect.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                        }
                        else
                        {
                            // It's an image placeholder
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                            
                            // Handle file path in rectangle tag - convert to base64
                            if (!string.IsNullOrEmpty(tagStr) && tagStr != "Missing Image Data" && tagStr != "No Image Data")
                            {
                                if (System.IO.File.Exists(tagStr))
                                {
                                    labelElement.ImageData = ConvertImageToBase64(tagStr);
                                    labelElement.Content = ""; // Clear file path since we have base64 data
                                }
                                else
                                {
                                    labelElement.Content = tagStr; // Keep as fallback
                                }
                            }
                        }
                    }
                    else if (child is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is System.Windows.Shapes.Line lineInCanvas)
                    {
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Line;
                        labelElement.BorderWidth = lineInCanvas.StrokeThickness;
                        labelElement.BorderColor = (lineInCanvas.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                    }
                    else if (child is System.Windows.Shapes.Line directLine)
                    {
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Line;
                        labelElement.BorderWidth = directLine.StrokeThickness;
                        labelElement.BorderColor = (directLine.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
                    }
                    else if (child is Image imageElement)
                    {
                        if (imageElement.Tag?.ToString() == "QRCode" || imageElement.Tag?.ToString()?.StartsWith("QRCodeData:") == true)
                        {
                            labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                            // Extract data from tag if available
                            if (imageElement.Tag?.ToString()?.StartsWith("QRCodeData:") == true)
                                labelElement.Content = imageElement.Tag.ToString().Substring("QRCodeData:".Length);
                            else
                                labelElement.Content = "Sample QR Data";
                        }
                        else if (imageElement.Tag?.ToString()?.StartsWith("BarcodeData:") == true)
                        {
                            var tagStr = imageElement.Tag.ToString();
                            var parts = tagStr.Split(':');
                            if (parts.Length >= 3)
                            {
                                var barcodeType = parts[1];
                                var barcodeData = string.Join(":", parts.Skip(2));
                                
                                // Map barcode type to enum
                                labelElement.Type = GetLabelElementTypeFromBarcodeType(barcodeType);
                                labelElement.Content = barcodeData;
                                labelElement.BarcodeType = barcodeType;
                            }
                            else
                            {
                                labelElement.Type = EvoDesigner.Models.LabelElementType.EAN13;
                                labelElement.Content = "4000000000004";
                                labelElement.BarcodeType = "EAN13";
                            }
                        }
                        else
                        {
                            // Regular image element
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                            
                            // Extract base64 image data from tag
                            string tagStr = imageElement.Tag?.ToString() ?? "";
                            if (tagStr.StartsWith("ImageData:"))
                            {
                                // Extract base64 data from tag
                                labelElement.ImageData = tagStr.Substring("ImageData:".Length);
                                labelElement.Content = ""; // Clear file path since we have base64 data
                            }
                            else
                            {
                                // Legacy: file path stored in tag - convert to base64
                                labelElement.Content = tagStr;
                                if (!string.IsNullOrEmpty(tagStr) && System.IO.File.Exists(tagStr))
                                {
                                    labelElement.ImageData = ConvertImageToBase64(tagStr);
                                }
                            }
                        }
                    }
                    
                    CurrentTemplate.Elements.Add(labelElement);
                }

                // Save data source configuration if available
                if (_currentDataSource != null)
                {
                    CurrentTemplate.DataSource = _currentDataSource;
                }

                string saveFilePath;
                
                // If we have an existing file path, save directly to it
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    saveFilePath = _currentFilePath;
                }
                else
                {
                    // Show dialog for new file
                    var dlg = new Microsoft.Win32.SaveFileDialog 
                    { 
                        Filter = "Label Template (*.xml)|*.xml",
                        DefaultExt = "xml",
                        FileName = "MyTemplate"
                    };
                    
                    if (dlg.ShowDialog() != true)
                        return; // User cancelled
                        
                    saveFilePath = dlg.FileName;
                }
                
                // Save the template
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(EvoDesigner.Models.LabelTemplate));
                using var fs = new System.IO.FileStream(saveFilePath, System.IO.FileMode.Create);
                serializer.Serialize(fs, CurrentTemplate);
                
                // Reset dirty flag and update current file path
                _currentFilePath = saveFilePath;
                SetDirty(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template:\n{ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog 
                { 
                    Filter = "Label Template (*.xml)|*.xml",
                    DefaultExt = "xml"
                };
                
                if (dlg.ShowDialog() == true)
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(EvoDesigner.Models.LabelTemplate));
                    using var fs = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Open);
                    CurrentTemplate = (EvoDesigner.Models.LabelTemplate)serializer.Deserialize(fs);
                    
                    // Load data source configuration if available
                    if (CurrentTemplate.DataSource != null)
                    {
                        _currentDataSource = CurrentTemplate.DataSource;
                    }
                    
                    // Restore template dimensions to internal properties and display
                    _labelWidthMm = CurrentTemplate.LabelWidth;
                    _labelHeightMm = CurrentTemplate.LabelHeight;
                    UpdateDisplayedDimensions();
                    MarginLeftBox.Text = CurrentTemplate.MarginLeft.ToString("F1");
                    MarginRightBox.Text = CurrentTemplate.MarginRight.ToString("F1");
                    MarginTopBox.Text = CurrentTemplate.MarginTop.ToString("F1");
                    MarginBottomBox.Text = CurrentTemplate.MarginBottom.ToString("F1");
                    
                    // Update canvas size and guidelines
                    UpdateCanvasSize();
                    
                    // Clear current canvas
                    LabelCanvas.Children.Clear();
                    elementRotations.Clear();
                    RemoveResizingHandles();
                    selectedElement = null;
                    
                    // Recreate elements on canvas
                    foreach (var element in CurrentTemplate.Elements)
                    {
                        UIElement canvasElement = CreateElementFromTemplate(element);
                        if (canvasElement != null)
                        {
                            Canvas.SetLeft(canvasElement, element.X);
                            Canvas.SetTop(canvasElement, element.Y);
                            canvasElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                            LabelCanvas.Children.Add(canvasElement);
                        }
                    }
                    
                    // Reset dirty flag and update current file path
                    _currentFilePath = dlg.FileName;
                    SetDirty(false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading template:\n{ex.Message}", "Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                File.WriteAllText("debug.log", "[DEBUG] ==> Starting CreateLiveTemplate\n");
                Console.WriteLine("[DEBUG] ==> Starting CreateLiveTemplate");
                var liveTemplate = CreateLiveTemplate();
                Console.WriteLine($"[DEBUG] Live template created with {liveTemplate.Elements.Count} elements");
                
                // Debug: Show what elements we have
                for (int i = 0; i < liveTemplate.Elements.Count; i++)
                {
                    var elem = liveTemplate.Elements[i];
                    Console.WriteLine($"[DEBUG] Element {i}: Type={elem.Type}, Content='{elem.Content}', Size={elem.Width}x{elem.Height}, Pos=({elem.X},{elem.Y})");
                }
                
                var previewWin = new EvoDesigner.UI.PrintPreviewWindow(liveTemplate, lastResultTable);
                Console.WriteLine("[DEBUG] PrintPreviewWindow created, showing dialog");
                previewWin.ShowDialog();
                Console.WriteLine("[DEBUG] PrintPreviewWindow dialog closed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ERROR in PrintPreview_Click: {ex.Message}");
                MessageBox.Show($"Error opening print preview: {ex.Message}", "Print Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataSource_Click(object sender, RoutedEventArgs e)
        {
            var win = new EvoDesigner.UI.DataSourceWindow(_currentDataSource);
            if (win.ShowDialog() == true)
            {
                lastResultTable = win.ResultTable;
                
                // Store the data source configuration
                _currentDataSource = new EvoDesigner.Models.DataSourceConfig
                {
                    Host = win.Host,
                    Port = win.Port,
                    Database = win.Database,
                    Username = win.Username,
                    Password = win.Password,
                    Query = win.Query
                };
                
                SetDirty(true);
            }
        }

        private void BringForward_Click(object sender, RoutedEventArgs e)
        {
            if (selectedElement != null)
            {
                int idx = LabelCanvas.Children.IndexOf(selectedElement);
                if (idx < LabelCanvas.Children.Count - 1)
                {
                    LabelCanvas.Children.RemoveAt(idx);
                    LabelCanvas.Children.Insert(idx + 1, selectedElement);
                }
            }
        }

        private void SendBackward_Click(object sender, RoutedEventArgs e)
        {
            if (selectedElement != null)
            {
                int idx = LabelCanvas.Children.IndexOf(selectedElement);
                if (idx > 0)
                {
                    LabelCanvas.Children.RemoveAt(idx);
                    LabelCanvas.Children.Insert(idx - 1, selectedElement);
                }
            }
        }

        private void SetDirty(bool isDirty = true)
        {
            _isDirty = isDirty;
            string baseTitle = "TaggJack - Label Designer";
            this.Title = _isDirty ? $"{baseTitle} *" : baseTitle;
        }

        private void InitializeZoomControls()
        {
            // Wire up zoom button events
            ZoomInButton.Click += (s, e) => ZoomIn();
            ZoomOutButton.Click += (s, e) => ZoomOut();
            ZoomResetButton.Click += (s, e) => ZoomReset();
            ZoomFitButton.Click += (s, e) => ZoomToFit();
            
            UpdateZoomDisplay();
        }

        private void ZoomIn()
        {
            if (_zoomFactor < MAX_ZOOM)
            {
                _zoomFactor = Math.Min(MAX_ZOOM, _zoomFactor + ZOOM_STEP);
                ApplyZoom();
            }
        }

        private void ZoomOut()
        {
            if (_zoomFactor > MIN_ZOOM)
            {
                _zoomFactor = Math.Max(MIN_ZOOM, _zoomFactor - ZOOM_STEP);
                ApplyZoom();
            }
        }

        private void ZoomReset()
        {
            _zoomFactor = 1.0;
            ApplyZoom();
        }

        private void ZoomToFit()
        {
            var scrollViewerSize = new Size(CanvasScrollViewer.ActualWidth - 20, CanvasScrollViewer.ActualHeight - 20);
            var labelSize = new Size(LabelCanvas.Width, LabelCanvas.Height);
            
            if (labelSize.Width > 0 && labelSize.Height > 0)
            {
                double zoomX = scrollViewerSize.Width / labelSize.Width;
                double zoomY = scrollViewerSize.Height / labelSize.Height;
                _zoomFactor = Math.Min(zoomX, zoomY);
                _zoomFactor = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, _zoomFactor));
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            CanvasScaleTransform.ScaleX = _zoomFactor;
            CanvasScaleTransform.ScaleY = _zoomFactor;
            UpdateRulers();
            UpdateZoomDisplay();
        }

        private void UpdateZoomDisplay()
        {
            ZoomTextBlock.Text = $"{(_zoomFactor * 100):F0}%";
        }

        private EvoDesigner.Models.LabelTemplate CreateLiveTemplate()
        {
            var logMessage = "[DEBUG] CreateLiveTemplate called\n";
            File.AppendAllText("debug.log", logMessage);
            Console.WriteLine("[DEBUG] CreateLiveTemplate called");
            var liveTemplate = new EvoDesigner.Models.LabelTemplate
            {
                TemplateName = CurrentTemplate.TemplateName,
                LabelWidth = CurrentTemplate.LabelWidth > 0 ? CurrentTemplate.LabelWidth : _labelWidthMm,
                LabelHeight = CurrentTemplate.LabelHeight > 0 ? CurrentTemplate.LabelHeight : _labelHeightMm,
                MarginLeft = CurrentTemplate.MarginLeft,
                MarginRight = CurrentTemplate.MarginRight,
                MarginTop = CurrentTemplate.MarginTop,
                MarginBottom = CurrentTemplate.MarginBottom
            };

            var labelElements = GetLabelElements();
            var countMessage = $"[DEBUG] Found {labelElements.Count} label elements on canvas\n";
            File.AppendAllText("debug.log", countMessage);
            Console.WriteLine($"[DEBUG] Found {labelElements.Count} label elements on canvas");
            
            foreach (UIElement child in labelElements)
            {
                var childMessage = $"[DEBUG] Processing canvas child: {child.GetType().Name}\n";
                File.AppendAllText("debug.log", childMessage);
                Console.WriteLine($"[DEBUG] Processing canvas child: {child.GetType().Name}");
                var labelElement = new EvoDesigner.Models.LabelElement
                {
                    X = Canvas.GetLeft(child),
                    Y = Canvas.GetTop(child),
                    Width = child is FrameworkElement fe ? fe.Width : 100,
                    Height = child is FrameworkElement fe2 ? fe2.Height : 30
                };
                
                if (child is TextBlock tb)
                {
                    var textMessage = $"[DEBUG] Found TextBlock: '{tb.Text}'\n";
                    File.AppendAllText("debug.log", textMessage);
                    Console.WriteLine($"[DEBUG] Found TextBlock: '{tb.Text}'");
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                    labelElement.Content = tb.Text;
                    labelElement.FontSize = tb.FontSize;
                    labelElement.FontFamily = tb.FontFamily.ToString();
                    labelElement.FontWeight = tb.FontWeight.ToString();
                    labelElement.FontStyle = tb.FontStyle.ToString();
                    labelElement.Foreground = tb.Foreground.ToString();
                    labelElement.TextDecorations = GetTextDecorationsString(tb.TextDecorations);
                }
                else if (child is Image img && img.Tag != null)
                {
                    string tagString = img.Tag.ToString();
                    if (tagString.StartsWith("BarcodeData:"))
                    {
                        // Parse the tag: "BarcodeData:{symbology}:{data}"
                        var parts = tagString.Split(':');
                        if (parts.Length >= 3)
                        {
                            string symbology = parts[1];
                            string data = string.Join(":", parts.Skip(2)); // Rejoin in case data contains colons
                            
                            // Map symbology to element type
                            switch (symbology.ToUpper())
                            {
                                case "EAN13":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.EAN13;
                                    break;
                                case "EAN8":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.EAN8;
                                    break;
                                case "UPCA":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.UPCA;
                                    break;
                                case "UPCE":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.UPCE;
                                    break;
                                case "CODE128":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.Code128;
                                    break;
                                case "CODE39":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.Code39;
                                    break;
                                case "QRCODE":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                                    break;
                                case "AZTEC":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.Aztec;
                                    break;
                                case "DATAMATRIX":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.DataMatrix;
                                    break;
                                case "GS1DATABAR":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.GS1DataBar;
                                    break;
                                case "PDF417":
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.PDF417;
                                    break;
                                default:
                                    labelElement.Type = EvoDesigner.Models.LabelElementType.Barcode;
                                    break;
                            }
                            
                            labelElement.Content = data;
                            labelElement.BarcodeType = symbology;
                        }
                    }
                    else if (tagString.StartsWith("ImageData:"))
                    {
                        // Regular image with base64 data
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                        labelElement.ImageData = tagString.Substring("ImageData:".Length);
                        labelElement.Content = ""; // Clear content since we have ImageData
                    }
                    else
                    {
                        // Regular image with other tag or path
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                        labelElement.Content = tagString;
                    }
                }
                else if (child is Image imgRegular)
                {
                    // Image without barcode tag - treat as regular image
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                    labelElement.Content = imgRegular.Source?.ToString() ?? "";
                }
                else if (child is Canvas canvas)
                {
                    var canvasMessage = $"[DEBUG] Found Canvas with {canvas.Children.Count} children\n";
                    File.AppendAllText("debug.log", canvasMessage);
                    Console.WriteLine($"[DEBUG] Found Canvas with {canvas.Children.Count} children");
                    
                    // Check if this canvas contains a line or rectangle
                    var line = canvas.Children.OfType<System.Windows.Shapes.Line>().FirstOrDefault();
                    var rectangle = canvas.Children.OfType<System.Windows.Shapes.Rectangle>().FirstOrDefault();
                    
                    if (line != null)
                    {
                        var lineMessage = $"[DEBUG] Found Line in Canvas: X1={line.X1}, Y1={line.Y1}, X2={line.X2}, Y2={line.Y2}\n";
                        File.AppendAllText("debug.log", lineMessage);
                        Console.WriteLine($"[DEBUG] Found Line in Canvas: X1={line.X1}, Y1={line.Y1}, X2={line.X2}, Y2={line.Y2}");
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Line;
                        labelElement.BorderWidth = line.StrokeThickness;
                        labelElement.BorderColor = line.Stroke?.ToString() ?? "#000000";
                        labelElement.Content = $"{line.X1},{line.Y1},{line.X2},{line.Y2}";
                    }
                    else if (rectangle != null)
                    {
                        var rectMessage = $"[DEBUG] Found Rectangle in Canvas: Width={rectangle.Width}, Height={rectangle.Height}\n";
                        File.AppendAllText("debug.log", rectMessage);
                        Console.WriteLine($"[DEBUG] Found Rectangle in Canvas: Width={rectangle.Width}, Height={rectangle.Height}");
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Rectangle;
                        labelElement.BorderWidth = rectangle.StrokeThickness;
                        labelElement.BorderColor = rectangle.Stroke?.ToString() ?? "#000000";
                        labelElement.Content = "";
                        labelElement.Width = double.IsNaN(rectangle.Width) ? labelElement.Width : rectangle.Width;
                        labelElement.Height = double.IsNaN(rectangle.Height) ? labelElement.Height : rectangle.Height;
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Canvas with no recognized shapes, skipping");
                        continue; // Skip this element if it's not a recognized shape container
                    }
                }
                else if (child is System.Windows.Shapes.Rectangle directRect)
                {
                    var directRectMessage = $"[DEBUG] Found direct Rectangle: Width={directRect.Width}, Height={directRect.Height}\n";
                    File.AppendAllText("debug.log", directRectMessage);
                    Console.WriteLine($"[DEBUG] Found direct Rectangle: Width={directRect.Width}, Height={directRect.Height}");
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Rectangle;
                    labelElement.BorderWidth = directRect.StrokeThickness;
                    labelElement.BorderColor = directRect.Stroke?.ToString() ?? "#000000";
                    labelElement.Content = "";
                    labelElement.Width = double.IsNaN(directRect.Width) ? labelElement.Width : directRect.Width;
                    labelElement.Height = double.IsNaN(directRect.Height) ? labelElement.Height : directRect.Height;
                }
                else if (child is Border border)
                {
                    var borderMessage = $"[DEBUG] Found Border element\n";
                    File.AppendAllText("debug.log", borderMessage);
                    Console.WriteLine($"[DEBUG] Found Border element");
                    
                    // Debug: Check what's inside the border
                    var borderChildMsg = $"[DEBUG] Border.Child type: {border.Child?.GetType().Name ?? "null"}\n";
                    File.AppendAllText("debug.log", borderChildMsg);
                    Console.WriteLine($"[DEBUG] Border.Child type: {border.Child?.GetType().Name ?? "null"}");
                    
                    // Check if the border contains a TextBlock
                    if (border.Child is TextBlock borderText)
                    {
                        var borderTextMessage = $"[DEBUG] Border contains TextBlock: '{borderText.Text}'\n";
                        File.AppendAllText("debug.log", borderTextMessage);
                        Console.WriteLine($"[DEBUG] Border contains TextBlock: '{borderText.Text}'");
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                        labelElement.Content = borderText.Text;
                        labelElement.FontSize = borderText.FontSize;
                        labelElement.FontFamily = borderText.FontFamily.ToString();
                        labelElement.FontWeight = borderText.FontWeight.ToString();
                        labelElement.FontStyle = borderText.FontStyle.ToString();
                        labelElement.Foreground = borderText.Foreground.ToString();
                        labelElement.TextDecorations = GetTextDecorationsString(borderText.TextDecorations);
                        
                        // Debug: Log what's being saved
                        var saveDebugMsg = $"[DEBUG] SAVING: Text='{borderText.Text}', FontSize={borderText.FontSize}, FontFamily='{borderText.FontFamily}', Color='{borderText.Foreground}'\n";
                        File.AppendAllText("debug.log", saveDebugMsg);
                        Console.WriteLine($"[DEBUG] SAVING: Text='{borderText.Text}', FontSize={borderText.FontSize}, FontFamily='{borderText.FontFamily}', Color='{borderText.Foreground}'");
                        
                        // CRITICAL FIX: Add missing continue statement to prevent falling through to "unrecognized element" logic
                        // Without this, the labelElement gets created but then skipped!
                    }
                    else
                    {
                        var noTextBlockMsg = $"[DEBUG] Border with no TextBlock, skipping\n";
                        File.AppendAllText("debug.log", noTextBlockMsg);
                        Console.WriteLine($"[DEBUG] Border with no TextBlock, skipping");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Unrecognized element type: {child.GetType().Name}, skipping");
                    continue; // Skip unrecognized elements
                }
                
                if (child.RenderTransform is RotateTransform rotateTransform)
                {
                    labelElement.Rotation = rotateTransform.Angle;
                }
                else
                {
                    labelElement.Rotation = 0;
                }
                
                var elemMessage = $"[DEBUG] Created label element: Type={labelElement.Type}, Size={labelElement.Width}x{labelElement.Height}\n";
                File.AppendAllText("debug.log", elemMessage);
                Console.WriteLine($"[DEBUG] Created label element: Type={labelElement.Type}, Size={labelElement.Width}x{labelElement.Height}");
                liveTemplate.Elements.Add(labelElement);
            }
            
            var finalMessage = $"[DEBUG] Final live template has {liveTemplate.Elements.Count} elements\n";
            File.AppendAllText("debug.log", finalMessage);
            Console.WriteLine($"[DEBUG] Final live template has {liveTemplate.Elements.Count} elements");
            liveTemplate.FieldMappings = CurrentTemplate.FieldMappings ?? new List<EvoDesigner.Models.FieldMapping>();
            
            return liveTemplate;
        }

        private List<UIElement> GetLabelElements()
        {
            var labelElements = new List<UIElement>();
            foreach (UIElement child in LabelCanvas.Children)
            {
                if (!(child is System.Windows.Controls.Primitives.Thumb))
                {
                    labelElements.Add(child);
                }
            }
            return labelElements;
        }

        private string GetTextDecorationsString(TextDecorationCollection? decorations)
        {
            if (decorations == null || decorations.Count == 0)
            {
                return "";
            }
                
            if (decorations == System.Windows.TextDecorations.Underline)
            {
                return "Underline";
            }
            else if (decorations == System.Windows.TextDecorations.Strikethrough)
            {
                return "Strikethrough";
            }
            else if (decorations == System.Windows.TextDecorations.OverLine)
            {
                return "Overline";
            }
            else if (decorations == System.Windows.TextDecorations.Baseline)
            {
                return "Baseline";
            }
                
            foreach (var decoration in decorations)
            {
                switch (decoration.Location)
                {
                    case TextDecorationLocation.Underline:
                        return "Underline";
                    case TextDecorationLocation.Strikethrough:
                        return "Strikethrough";
                    case TextDecorationLocation.OverLine:
                        return "Overline";
                    case TextDecorationLocation.Baseline:
                        return "Baseline";
                }
            }
            
            return "";
        }

        private UIElement? CreateElementFromTemplate(EvoDesigner.Models.LabelElement element)
        {
            UIElement? result = null;
            
            // Debug: Log what's being loaded
            var loadDebugMsg = $"[DEBUG] LOADING: Text='{element.Content}', FontSize={element.FontSize}, FontFamily='{element.FontFamily}', Color='{element.Foreground}'\n";
            System.IO.File.AppendAllText("debug.log", loadDebugMsg);
            System.Console.WriteLine($"[DEBUG] LOADING: Text='{element.Content}', FontSize={element.FontSize}, FontFamily='{element.FontFamily}', Color='{element.Foreground}'");
            
            switch (element.Type)
            {
                case EvoDesigner.Models.LabelElementType.Text:
                    var textBlock = new TextBlock
                    {
                        Text = element.Content ?? "Text",
                        FontSize = element.FontSize > 0 ? element.FontSize : 12, // Use 12 as default if 0 or negative
                        Width = element.Width,
                        Height = element.Height
                    };
                    
                    // Apply font properties if available
                    if (!string.IsNullOrEmpty(element.FontFamily))
                        textBlock.FontFamily = new FontFamily(element.FontFamily);
                    if (!string.IsNullOrEmpty(element.FontWeight))
                        textBlock.FontWeight = element.FontWeight.Contains("Bold") ? FontWeights.Bold : FontWeights.Normal;
                    if (!string.IsNullOrEmpty(element.FontStyle))
                        textBlock.FontStyle = element.FontStyle.Contains("Italic") ? FontStyles.Italic : FontStyles.Normal;
                    
                    // Apply foreground color if available
                    if (!string.IsNullOrEmpty(element.Foreground))
                    {
                        try
                        {
                            var colorConverter = new BrushConverter();
                            var brush = (Brush)colorConverter.ConvertFromString(element.Foreground);
                            textBlock.Foreground = brush;
                        }
                        catch
                        {
                            // If color conversion fails, use black as default
                            textBlock.Foreground = Brushes.Black;
                        }
                    }
                    
                    // Apply text alignment if available
                    if (!string.IsNullOrEmpty(element.TextAlignment))
                    {
                        switch (element.TextAlignment.ToLower())
                        {
                            case "center": textBlock.TextAlignment = TextAlignment.Center; break;
                            case "right": textBlock.TextAlignment = TextAlignment.Right; break;
                            default: textBlock.TextAlignment = TextAlignment.Left; break;
                        }
                    }
                    
                    // Apply vertical alignment if available
                    if (!string.IsNullOrEmpty(element.VerticalAlignment))
                    {
                        switch (element.VerticalAlignment.ToLower())
                        {
                            case "center": textBlock.VerticalAlignment = VerticalAlignment.Center; break;
                            case "bottom": textBlock.VerticalAlignment = VerticalAlignment.Bottom; break;
                            default: textBlock.VerticalAlignment = VerticalAlignment.Top; break;
                        }
                    }
                    
                    // Wrap text in a border for alignment support
                    var border = new Border
                    {
                        Child = textBlock,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        Background = Brushes.Transparent,
                        Width = element.Width,
                        Height = element.Height
                    };
                    
                    result = border;
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Image:
                    try
                    {
                        BitmapImage? bitmap = null;
                        
                        // First try to load from ImageData (base64)
                        if (!string.IsNullOrEmpty(element.ImageData))
                        {
                            var imageBytes = Convert.FromBase64String(element.ImageData);
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new MemoryStream(imageBytes);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                        }
                        // Fallback to Content if it's a data URL
                        else if (!string.IsNullOrEmpty(element.Content) && element.Content.StartsWith("data:image"))
                        {
                            var base64Data = element.Content.Split(',')[1];
                            var imageBytes = Convert.FromBase64String(base64Data);
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new MemoryStream(imageBytes);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                        }
                        // Fallback to Content if it's a file path
                        else if (!string.IsNullOrEmpty(element.Content) && System.IO.File.Exists(element.Content))
                        {
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(element.Content);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                        }

                        if (bitmap != null)
                        {
                            var image = new Image
                            {
                                Source = bitmap,
                                Width = element.Width,
                                Height = element.Height,
                                Stretch = Stretch.Uniform,
                                Tag = !string.IsNullOrEmpty(element.ImageData) ? $"ImageData:{element.ImageData}" : element.Content
                            };
                            result = image;
                        }
                        else
                        {
                            // Fallback to rectangle if no valid image data
                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = element.Width,
                                Height = element.Height,
                                Fill = Brushes.LightGray,
                                Stroke = Brushes.DarkGray,
                                StrokeThickness = 2,
                                Tag = element.Content ?? "No Image Data"
                            };
                            result = rect;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fallback to rectangle if image loading fails
                        var rect = new System.Windows.Shapes.Rectangle
                        {
                            Width = element.Width,
                            Height = element.Height,
                            Fill = Brushes.LightGray,
                            Stroke = Brushes.DarkGray,
                            StrokeThickness = 2,
                            Tag = $"Image Load Error: {ex.Message}"
                        };
                        result = rect;
                    }
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Line:
                    // Create a container canvas for the line that can be resized
                    var lineCanvasContainer = new Canvas
                    {
                        Width = element.Width,
                        Height = element.Height,
                        Background = Brushes.Transparent
                    };
                    
                    var lineElement = new System.Windows.Shapes.Line
                    {
                        X1 = 0,
                        Y1 = element.Height / 2,
                        X2 = element.Width,
                        Y2 = element.Height / 2,
                        StrokeThickness = element.BorderWidth > 0 ? element.BorderWidth : 1
                    };
                    
                    // Set line color
                    try
                    {
                        if (!string.IsNullOrEmpty(element.BorderColor))
                            lineElement.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.BorderColor) ?? Brushes.Black);
                        else
                            lineElement.Stroke = Brushes.Black;
                    }
                    catch
                    {
                        lineElement.Stroke = Brushes.Black;
                    }
                    
                    lineCanvasContainer.Children.Add(lineElement);
                    result = lineCanvasContainer;
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Rectangle:
                    var rectangle = new System.Windows.Shapes.Rectangle
                    {
                        Width = element.Width,
                        Height = element.Height,
                        StrokeThickness = element.BorderWidth > 0 ? element.BorderWidth : 1,
                        Fill = Brushes.Transparent  // No fill, just border
                    };
                    
                    // Set border color
                    try
                    {
                        if (!string.IsNullOrEmpty(element.BorderColor))
                            rectangle.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.BorderColor) ?? Brushes.Black);
                        else
                            rectangle.Stroke = Brushes.Black;
                    }
                    catch
                    {
                        rectangle.Stroke = Brushes.Black;
                    }
                    
                    result = rectangle;
                    break;
                    
                case EvoDesigner.Models.LabelElementType.QRCode:
                    result = CreateBarcodeImage("QRCODE", element.Content ?? "Sample QR Data", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.EAN13:
                case EvoDesigner.Models.LabelElementType.Barcode13: // Legacy support
                    result = CreateBarcodeImage("EAN13", element.Content ?? "4000000000004", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.EAN8:
                case EvoDesigner.Models.LabelElementType.Barcode8: // Legacy support
                    result = CreateBarcodeImage("EAN8", element.Content ?? "40000007", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.UPCA:
                    result = CreateBarcodeImage("UPCA", element.Content ?? "012345678905", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.UPCE:
                    result = CreateBarcodeImage("UPCE", element.Content ?? "01234565", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Code128:
                    result = CreateBarcodeImage("CODE128", element.Content ?? "HELLO123", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Code39:
                    result = CreateBarcodeImage("CODE39", element.Content ?? "HELLO39", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Aztec:
                    result = CreateBarcodeImage("AZTEC", element.Content ?? "Sample Aztec Data", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.DataMatrix:
                    result = CreateBarcodeImage("DATAMATRIX", element.Content ?? "Sample Data Matrix", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.GS1DataBar:
                    result = CreateBarcodeImage("GS1DATABAR", element.Content ?? "]C101234567890128", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.PDF417:
                    result = CreateBarcodeImage("PDF417", element.Content ?? "Sample PDF417 Data", element.Width, element.Height);
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Barcode: // Legacy support
                    result = CreateBarcodeImage("CODE128", element.Content ?? "LEGACY", element.Width, element.Height);
                    break;
            }
            
            // Apply rotation if specified
            if (result != null && element.Rotation != 0)
            {
                var rotateTransform = new RotateTransform(element.Rotation);
                if (result is FrameworkElement fe)
                {
                    fe.RenderTransformOrigin = new Point(0.5, 0.5);
                }
                result.RenderTransform = rotateTransform;
                
                // Store rotation in our dictionary
                elementRotations[result] = element.Rotation;
            }
            
            return result;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!PromptSaveChanges())
            {
                e.Cancel = true;
            }
        }

        private bool PromptSaveChanges()
        {
            if (!_isDirty) return true; // No changes to save

            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save them before continuing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    SaveTemplate_Click(this, new RoutedEventArgs());
                    return !_isDirty; // Return true if save was successful
                case MessageBoxResult.No:
                    return true; // Continue without saving
                case MessageBoxResult.Cancel:
                default:
                    return false; // Cancel the operation
            }
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (PromptSaveChanges())
            {
                // Clear the canvas
                LabelCanvas.Children.Clear();
                RemoveResizingHandles();
                selectedElement = null;
                selectedElements.Clear();
                
                // Reset template to defaults
                CurrentTemplate = new EvoDesigner.Models.LabelTemplate();
                _currentFilePath = null;
                
                // Reset UI to default values
                InitializeDefaultTemplate();
                
                // Clear any loaded data
                lastResultTable = null;
                _currentDataSource = null;
                
                // Clear properties panel
                ElementPropertiesPanel.Children.Clear();
                ElementPropertiesPanel.Children.Add(new TextBlock 
                { 
                    Text = "Select an element to view its properties", 
                    Foreground = Brushes.Gray, 
                    FontStyle = FontStyles.Italic, 
                    TextWrapping = TextWrapping.Wrap, 
                    Margin = new Thickness(0,10,0,0) 
                });
                
                // Reset dirty flag
                SetDirty(false);
            }
        }

        private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Populate template with current canvas content
                CurrentTemplate.Elements.Clear();
                CurrentTemplate.TemplateName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath ?? "Untitled Template");
                
                // Save all canvas elements (excluding resize handles)
                foreach (UIElement child in LabelCanvas.Children)
                {
                    if (child is System.Windows.Controls.Primitives.Thumb) continue; // Skip resize handles
                    
                    var labelElement = new EvoDesigner.Models.LabelElement
                    {
                        X = Canvas.GetLeft(child),
                        Y = Canvas.GetTop(child),
                        Width = child is FrameworkElement fe ? fe.Width : 100,
                        Height = child is FrameworkElement fe2 ? fe2.Height : 30,
                        Rotation = elementRotations.ContainsKey(child) ? elementRotations[child] : 
                                  (child.RenderTransform is RotateTransform rotateTransform ? rotateTransform.Angle : 0)
                    };
                    
                    if (child is TextBlock tb)
                    {
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                        labelElement.Content = tb.Text;
                        labelElement.FontSize = tb.FontSize;
                        labelElement.FontFamily = tb.FontFamily.ToString();
                        labelElement.FontWeight = tb.FontWeight.ToString();
                        labelElement.FontStyle = tb.FontStyle.ToString();
                        labelElement.Foreground = tb.Foreground.ToString();
                        labelElement.TextDecorations = GetTextDecorationsString(tb.TextDecorations);
                    }
                    
                    CurrentTemplate.Elements.Add(labelElement);
                }

                // Save data source configuration if available
                if (_currentDataSource != null)
                {
                    CurrentTemplate.DataSource = _currentDataSource;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog 
                { 
                    Filter = "Label Template (*.xml)|*.xml",
                    DefaultExt = "xml",
                    FileName = "MyTemplate"
                };
                
                if (dlg.ShowDialog() == true)
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(EvoDesigner.Models.LabelTemplate));
                    using var fs = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Create);
                    serializer.Serialize(fs, CurrentTemplate);
                    
                    // Reset dirty flag and update current file path
                    _currentFilePath = dlg.FileName;
                    SetDirty(false);
                    
                    MessageBox.Show($"Template saved successfully to:\n{dlg.FileName}", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template:\n{ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MapFields_Click(object sender, RoutedEventArgs e)
        {
            if (lastResultTable == null || LabelCanvas.Children.Count == 0)
            {
                MessageBox.Show("Please fetch data and add label elements to the canvas first.", "No Data or Elements", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Get consistent list of label elements
            var labelElements = GetLabelElements();
            
            if (labelElements.Count == 0)
            {
                MessageBox.Show("No label elements found on the canvas.", "No Elements", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var win = new EvoDesigner.UI.FieldMappingWindow(labelElements, lastResultTable);
            if (win.ShowDialog() == true)
            {
                CurrentTemplate.FieldMappings = win.Mappings;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (PromptSaveChanges())
            {
                Application.Current.Shutdown();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new UI.AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private async void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDataSource == null)
            {
                MessageBox.Show("No data source configured. Please setup a data source first.", "No Data Source", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show refreshing status
            var dataStatusTextBlock = FindName("DataStatusTextBlock") as TextBlock;
            if (dataStatusTextBlock != null)
            {
                dataStatusTextBlock.Text = "Refreshing data...";
                dataStatusTextBlock.FontStyle = FontStyles.Italic;
            }

            // Disable the refresh button during operation
            var refreshButton = sender as Button;
            if (refreshButton != null)
            {
                refreshButton.IsEnabled = false;
            }

            try
            {
                // Add a small delay to ensure UI updates
                await Task.Delay(100);

                // Run the database operation on a background thread
                await Task.Run(() =>
                {
                    // Build connection string from current data source
                    var connectionString = $"Server={_currentDataSource.Host};Port={_currentDataSource.Port};Database={_currentDataSource.Database};Uid={_currentDataSource.Username};Pwd={_currentDataSource.Password};";
                    
                    // Execute the query using MySqlService
                    var service = new EvoDesigner.Services.MySqlService { ConnectionString = connectionString };
                    lastResultTable = service.ExecuteQuery(_currentDataSource.Query);
                });
                
                // Update data status display with final results (back on UI thread)
                UpdateDataStatus();
                
                if (lastResultTable != null)
                {
                    MessageBox.Show($"Data refreshed successfully! Fetched {lastResultTable.Rows.Count} rows with {lastResultTable.Columns.Count} columns.", 
                        "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Data refreshed but no data returned.", "Refresh Complete", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                SetDirty(true); // Mark template as dirty since data has changed
            }
            catch (Exception ex)
            {
                // Reset status on error
                if (dataStatusTextBlock != null)
                {
                    dataStatusTextBlock.Text = "Refresh failed";
                    dataStatusTextBlock.FontStyle = FontStyles.Italic;
                }
                
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Refresh Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable the refresh button
                if (refreshButton != null)
                {
                    refreshButton.IsEnabled = true;
                }
            }
        }

        private void UpdateDataStatus()
        {
            var dataStatusTextBlock = FindName("DataStatusTextBlock") as TextBlock;
            if (dataStatusTextBlock != null)
            {
                if (lastResultTable != null && lastResultTable.Rows.Count > 0)
                {
                    dataStatusTextBlock.Text = $"{lastResultTable.Rows.Count} rows, {lastResultTable.Columns.Count} fields";
                    dataStatusTextBlock.FontStyle = FontStyles.Normal;
                }
                else
                {
                    dataStatusTextBlock.Text = "No data";
                    dataStatusTextBlock.FontStyle = FontStyles.Italic;
                }
            }
        }

        private void ResizeButton_Click(object sender, RoutedEventArgs e)
        {
            var resizeWindow = new UI.LabelResizeWindow(_labelWidthMm, _labelHeightMm);
            if (resizeWindow.ShowDialog() == true)
            {
                // Get the old dimensions for scaling calculations
                double oldWidth = _labelWidthMm;
                double oldHeight = _labelHeightMm;
                
                // Update the label dimensions
                _labelWidthMm = resizeWindow.NewWidth;
                _labelHeightMm = resizeWindow.NewHeight;
                
                // Update the current template
                CurrentTemplate.LabelWidth = _labelWidthMm;
                CurrentTemplate.LabelHeight = _labelHeightMm;
                
                // Calculate scaling factors
                double scaleX = _labelWidthMm / oldWidth;
                double scaleY = _labelHeightMm / oldHeight;
                
                // Apply the resize mode to all elements on the canvas
                foreach (UIElement element in LabelCanvas.Children.OfType<UIElement>().ToList())
                {
                    if (element == null) continue;
                    
                    switch (resizeWindow.SelectedResizeMode)
                    {
                        case UI.LabelResizeMode.ResizeElements:
                            // Scale element size and position proportionally
                            if (element is FrameworkElement fe)
                            {
                                Canvas.SetLeft(element, Canvas.GetLeft(element) * scaleX);
                                Canvas.SetTop(element, Canvas.GetTop(element) * scaleY);
                                fe.Width = fe.Width * scaleX;
                                fe.Height = fe.Height * scaleY;
                                
                                // Scale font size for text elements
                                if (fe is TextBlock tb)
                                {
                                    tb.FontSize = tb.FontSize * Math.Min(scaleX, scaleY);
                                }
                            }
                            break;
                            
                        case UI.LabelResizeMode.RepositionElements:
                            // Only reposition elements proportionally, keep their size
                            Canvas.SetLeft(element, Canvas.GetLeft(element) * scaleX);
                            Canvas.SetTop(element, Canvas.GetTop(element) * scaleY);
                            break;
                            
                        case UI.LabelResizeMode.CanvasOnly:
                            // Don't modify elements at all
                            break;
                    }
                }
                
                // Update the canvas size and related UI
                UpdateCanvasSize();
                UpdateMarginGuidelines();
                UpdateGrid();
                
                // Update the dimension displays
                LabelWidthDisplay.Text = $"{_labelWidthMm:F1} mm";
                LabelHeightDisplay.Text = $"{_labelHeightMm:F1} mm";
                
                // Mark as dirty since we've made changes
                SetDirty(true);
            }
        }

        #region Text Alignment Event Handlers

        private void AlignLeft_Click(object sender, RoutedEventArgs e)
        {
            SetTextAlignment("Left", null);
        }

        private void AlignCenter_Click(object sender, RoutedEventArgs e)
        {
            SetTextAlignment("Center", null);
        }

        private void AlignRight_Click(object sender, RoutedEventArgs e)
        {
            SetTextAlignment("Right", null);
        }

        private void AlignTop_Click(object sender, RoutedEventArgs e)
        {
            SetTextAlignment(null, "Top");
        }

        private void AlignMiddle_Click(object sender, RoutedEventArgs e)
        {
            SetTextAlignment(null, "Center");
        }

        private void AlignBottom_Click(object sender, RoutedEventArgs e)
        {
            SetTextAlignment(null, "Bottom");
        }

        private void SetTextAlignment(string? horizontalAlign, string? verticalAlign)
        {
            // Get all currently selected elements (both single and multi-selection)
            var elementsToAlign = new List<UIElement>();
            
            if (selectedElements.Count > 0)
            {
                elementsToAlign.AddRange(selectedElements);
            }
            else if (selectedElement != null)
            {
                elementsToAlign.Add(selectedElement);
            }
            
            if (elementsToAlign.Count == 0) return;

            foreach (var element in elementsToAlign)
            {
                // Check if this is a text element (Border containing TextBlock)
                if (element is Border border && border.Child is TextBlock textBlock)
                {
                    // Update horizontal alignment
                    if (horizontalAlign != null)
                    {
                        switch (horizontalAlign.ToLower())
                        {
                            case "center": 
                                textBlock.TextAlignment = TextAlignment.Center; 
                                break;
                            case "right": 
                                textBlock.TextAlignment = TextAlignment.Right; 
                                break;
                            default: 
                                textBlock.TextAlignment = TextAlignment.Left; 
                                break;
                        }
                        
                        // Store alignment in the border's tag for persistence
                        var tag = border.Tag as string ?? "";
                        var parts = tag.Split('|');
                        if (parts.Length >= 2)
                        {
                            parts[0] = horizontalAlign;
                            border.Tag = string.Join("|", parts);
                        }
                        else
                        {
                            border.Tag = $"{horizontalAlign}|{parts.ElementAtOrDefault(1) ?? "Top"}";
                        }
                    }

                    // Update vertical alignment
                    if (verticalAlign != null)
                    {
                        switch (verticalAlign.ToLower())
                        {
                            case "center": 
                                textBlock.VerticalAlignment = VerticalAlignment.Center; 
                                break;
                            case "bottom": 
                                textBlock.VerticalAlignment = VerticalAlignment.Bottom; 
                                break;
                            default: 
                                textBlock.VerticalAlignment = VerticalAlignment.Top; 
                                break;
                        }
                        
                        // Store alignment in the border's tag for persistence
                        var tag = border.Tag as string ?? "";
                        var parts = tag.Split('|');
                        if (parts.Length >= 2)
                        {
                            parts[1] = verticalAlign;
                            border.Tag = string.Join("|", parts);
                        }
                        else
                        {
                            border.Tag = $"{parts.ElementAtOrDefault(0) ?? "Left"}|{verticalAlign}";
                        }
                    }

                    // Also try to update the corresponding LabelElement if it exists
                    var labelElement = CurrentTemplate?.Elements?.FirstOrDefault(e => 
                        e.Type == LabelElementType.Text && 
                        Math.Abs(e.X - Canvas.GetLeft(element)) < 1 && 
                        Math.Abs(e.Y - Canvas.GetTop(element)) < 1);

                    if (labelElement != null)
                    {
                        if (horizontalAlign != null) labelElement.TextAlignment = horizontalAlign;
                        if (verticalAlign != null) labelElement.VerticalAlignment = verticalAlign;
                    }

                    SetDirty(true);
                    
                    // Refresh properties panel if this element is selected
                    if (elementsToAlign.Count == 1)
                    {
                        ShowProperties(element);
                    }
                }
            }
        }

        #endregion

        private System.Windows.Controls.Button CreateColorPicker(System.Windows.Media.Color initialColor, Action<System.Windows.Media.Color> onColorChanged)
        {
            var button = new System.Windows.Controls.Button
            {
                Width = 80,
                Height = 25,
                Margin = new Thickness(4, 0, 0, 0),
                Content = "Color",
                Background = new SolidColorBrush(initialColor),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1)
            };

            button.Click += (s, e) =>
            {
                var colorWindow = new System.Windows.Window
                {
                    Title = "Color Wheel Picker",
                    Width = 450,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var mainGrid = new System.Windows.Controls.Grid { Margin = new Thickness(10) };
                mainGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(250) });
                mainGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

                // Color wheel canvas
                var colorWheelCanvas = new System.Windows.Controls.Canvas
                {
                    Width = 200,
                    Height = 200,
                    Background = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(10)
                };
                System.Windows.Controls.Grid.SetColumn(colorWheelCanvas, 0);
                System.Windows.Controls.Grid.SetRow(colorWheelCanvas, 0);

                // Draw color wheel
                var wheelCenter = new System.Windows.Point(100, 100);
                var wheelRadius = 90;
                
                // Create color wheel using rectangles in a circle
                for (int angle = 0; angle < 360; angle += 2)
                {
                    for (int radius = 20; radius < wheelRadius; radius += 2)
                    {
                        var hue = angle;
                        var saturation = (double)radius / wheelRadius;
                        var brightness = 1.0; // Always max brightness for wheel display
                        
                        var color = HsvToRgb(hue, saturation, brightness);
                        var rect = new System.Windows.Shapes.Rectangle
                        {
                            Width = 3,
                            Height = 3,
                            Fill = new SolidColorBrush(color)
                        };
                        
                        var radians = angle * Math.PI / 180;
                        var x = wheelCenter.X + radius * Math.Cos(radians) - 1.5;
                        var y = wheelCenter.Y + radius * Math.Sin(radians) - 1.5;
                        
                        System.Windows.Controls.Canvas.SetLeft(rect, x);
                        System.Windows.Controls.Canvas.SetTop(rect, y);
                        colorWheelCanvas.Children.Add(rect);
                    }
                }

                // Color wheel selector
                var wheelSelector = new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Colors.Transparent)
                };
                colorWheelCanvas.Children.Add(wheelSelector);

                // Current color state - initialize from element's current color
                var currentHsv = RgbToHsv(initialColor);
                var currentColor = initialColor; // Start with the actual current color

                // Update wheel selector position
                Action updateWheelSelector = () =>
                {
                    var selectorRadius = currentHsv.S * wheelRadius;
                    var selectorRadians = currentHsv.H * Math.PI / 180;
                    var selectorX = wheelCenter.X + selectorRadius * Math.Cos(selectorRadians) - 4;
                    var selectorY = wheelCenter.Y + selectorRadius * Math.Sin(selectorRadians) - 4;
                    System.Windows.Controls.Canvas.SetLeft(wheelSelector, selectorX);
                    System.Windows.Controls.Canvas.SetTop(wheelSelector, selectorY);
                };

                updateWheelSelector();

                // Right panel for brightness and preview
                var rightPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
                System.Windows.Controls.Grid.SetColumn(rightPanel, 1);
                System.Windows.Controls.Grid.SetRow(rightPanel, 0);

                // Color preview - initialize with current element color
                var previewRect = new System.Windows.Shapes.Rectangle
                {
                    Width = 100,
                    Height = 60,
                    Fill = new SolidColorBrush(currentColor), // Use currentColor (initialColor)
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 1,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                rightPanel.Children.Add(previewRect);

                // Brightness slider - initialize with current element brightness
                var brightnessLabel = new System.Windows.Controls.Label { Content = "Brightness:" };
                rightPanel.Children.Add(brightnessLabel);

                var brightnessSlider = new System.Windows.Controls.Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = currentHsv.V, // Use actual current brightness
                    Width = 120,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                rightPanel.Children.Add(brightnessSlider);

                // RGB values display - initialize with current element RGB
                var rgbPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                var rgbLabel = new System.Windows.Controls.Label { Content = "RGB Values:" };
                rgbPanel.Children.Add(rgbLabel);

                var rLabel = new System.Windows.Controls.Label { Content = $"R: {currentColor.R}" };
                var gLabel = new System.Windows.Controls.Label { Content = $"G: {currentColor.G}" };
                var bLabel = new System.Windows.Controls.Label { Content = $"B: {currentColor.B}" };
                rgbPanel.Children.Add(rLabel);
                rgbPanel.Children.Add(gLabel);
                rgbPanel.Children.Add(bLabel);
                rightPanel.Children.Add(rgbPanel);

                // Update color function
                Action updateColor = () =>
                {
                    currentColor = HsvToRgb(currentHsv.H, currentHsv.S, currentHsv.V);
                    previewRect.Fill = new SolidColorBrush(currentColor);
                    rLabel.Content = $"R: {currentColor.R}";
                    gLabel.Content = $"G: {currentColor.G}";
                    bLabel.Content = $"B: {currentColor.B}";
                    updateWheelSelector();
                };

                // Initialize the picker with current element color
                updateWheelSelector(); // Position the wheel selector correctly
                // All other components (preview, brightness, RGB) are already initialized correctly

                // Brightness slider event
                brightnessSlider.ValueChanged += (bs, be) =>
                {
                    currentHsv.V = brightnessSlider.Value;
                    updateColor();
                };

                // Color wheel mouse events
                colorWheelCanvas.MouseLeftButtonDown += (cs, ce) =>
                {
                    var position = ce.GetPosition(colorWheelCanvas);
                    var deltaX = position.X - wheelCenter.X;
                    var deltaY = position.Y - wheelCenter.Y;
                    var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    
                    if (distance <= wheelRadius && distance >= 20)
                    {
                        var angle = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
                        if (angle < 0) angle += 360;
                        
                        currentHsv.H = angle;
                        currentHsv.S = Math.Min(distance / wheelRadius, 1.0);
                        updateColor();
                    }
                };

                mainGrid.Children.Add(colorWheelCanvas);
                mainGrid.Children.Add(rightPanel);

                // Buttons
                var buttonPanel = new System.Windows.Controls.StackPanel 
                { 
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);
                System.Windows.Controls.Grid.SetRow(buttonPanel, 1);

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "OK",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5)
                };
                okButton.Click += (os, oe) =>
                {
                    button.Background = new SolidColorBrush(currentColor);
                    onColorChanged(currentColor);
                    colorWindow.Close();
                };

                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5)
                };
                cancelButton.Click += (cs, ce) => colorWindow.Close();

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                mainGrid.Children.Add(buttonPanel);

                colorWindow.Content = mainGrid;
                colorWindow.ShowDialog();
            };

            return button;
        }

        // HSV color structure
        private struct HsvColor
        {
            public double H; // Hue (0-360)
            public double S; // Saturation (0-1)
            public double V; // Value/Brightness (0-1)
        }

        private System.Windows.Media.Color HsvToRgb(double hue, double saturation, double value)
        {
            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;

            double r, g, b;

            if (hue >= 0 && hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue >= 60 && hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue >= 120 && hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue >= 180 && hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue >= 240 && hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            return System.Windows.Media.Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255)
            );
        }

        private HsvColor RgbToHsv(System.Windows.Media.Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            var hsv = new HsvColor();
            hsv.V = max;
            hsv.S = max == 0 ? 0 : delta / max;

            if (delta == 0)
            {
                hsv.H = 0;
            }
            else if (max == r)
            {
                hsv.H = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hsv.H = 60 * ((b - r) / delta + 2);
            }
            else
            {
                hsv.H = 60 * ((r - g) / delta + 4);
            }

            if (hsv.H < 0) hsv.H += 360;

            return hsv;
        }
    }
}
