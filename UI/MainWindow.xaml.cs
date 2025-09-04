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
using System.IO;
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

        public MainWindow()
        {
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

            // Add new buttons to UI
            var bringForwardBtn = new Button { Content = "Bring Forward", Margin = new Thickness(0, 0, 10, 0), Width = 110 };
            ((StackPanel)((Grid)((DockPanel)Content).Children[1]).Children[0]).Children.Add(bringForwardBtn);
            bringForwardBtn.Click += BringForward_Click;

            var sendBackwardBtn = new Button { Content = "Send Backward", Margin = new Thickness(0, 0, 10, 0), Width = 110 };
            ((StackPanel)((Grid)((DockPanel)Content).Children[1]).Children[0]).Children.Add(sendBackwardBtn);
            sendBackwardBtn.Click += SendBackward_Click;

            // Wire up template property change events
            LabelWidthBox.TextChanged += TemplateDimensions_Changed;
            LabelHeightBox.TextChanged += TemplateDimensions_Changed;
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
        }

        private void InitializeDefaultTemplate()
        {
            // Set default label size (common address label: 70mm x 30mm)
            LabelWidthBox.Text = "70";
            LabelHeightBox.Text = "30";
            MarginLeftBox.Text = "2";
            MarginRightBox.Text = "2";
            MarginTopBox.Text = "2";
            MarginBottomBox.Text = "2";
            TemplateNameBox.Text = "New Label Template";
            
            UpdateCanvasSize();
        }

        private void TemplateDimensions_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateCanvasSize();
        }

        private void UpdateCanvasSize()
        {
            // Get values from UI (default to reasonable values if parsing fails)
            double widthMm = double.TryParse(LabelWidthBox.Text, out double w) ? w : 70;
            double heightMm = double.TryParse(LabelHeightBox.Text, out double h) ? h : 30;
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

            // Ensure MarginCanvas matches LabelCanvas dimensions exactly
            MarginCanvas.Width = LabelCanvas.Width;
            MarginCanvas.Height = LabelCanvas.Height;
            
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
                else if (content?.Contains("Data Matrix") == true)
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
                                // Create an actual Image element with the dropped file
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
                                image.Tag = filePath; // Store file path for later reference
                                
                                image.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                                
                                // Use command to add the image
                                var addCommand = new AddElementCommand(LabelCanvas, image, adjustedPosition.X, adjustedPosition.Y, "Add image");
                                commandHistory.ExecuteCommand(addCommand);
                                
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
                        newElement = new TextBlock { Text = "Text", FontSize = 16, Foreground = Brushes.Black };
                        break;
                    case "Image":
                        newElement = new Rectangle { Width = 60, Height = 60, Fill = Brushes.LightGray, Stroke = Brushes.DarkGray, StrokeThickness = 2 };
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
            ElementPropertiesPanel.Children.Clear();
            if (element is TextBlock tb)
            {
                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Text:" });
                var textBox = new TextBox { Text = tb.Text };
                textBox.TextChanged += (s, e) => tb.Text = textBox.Text;
                ElementPropertiesPanel.Children.Add(textBox);

                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Font Family:" });
                var fontFamilyBox = new ComboBox { 
                    ItemsSource = System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f),
                    SelectedItem = tb.FontFamily.Source 
                };
                fontFamilyBox.SelectionChanged += (s, e) => {
                    if (fontFamilyBox.SelectedItem != null)
                        tb.FontFamily = new FontFamily(fontFamilyBox.SelectedItem.ToString());
                };
                ElementPropertiesPanel.Children.Add(fontFamilyBox);

                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Font Size:" });
                var fontSizeBox = new TextBox { Text = tb.FontSize.ToString() };
                fontSizeBox.TextChanged += (s, e) => {
                    if (double.TryParse(fontSizeBox.Text, out double size)) tb.FontSize = size;
                };
                ElementPropertiesPanel.Children.Add(fontSizeBox);

                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Text Formatting:" });
                
                // Create formatting buttons panel
                var formattingPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 5) };
                
                // Bold button
                var boldButton = new System.Windows.Controls.Primitives.ToggleButton { 
                    Content = "B", 
                    FontWeight = FontWeights.Bold,
                    Width = 30, 
                    Height = 25,
                    Margin = new Thickness(0, 0, 2, 0),
                    IsChecked = tb.FontWeight == FontWeights.Bold
                };
                boldButton.Checked += (s, e) => tb.FontWeight = FontWeights.Bold;
                boldButton.Unchecked += (s, e) => tb.FontWeight = FontWeights.Normal;
                formattingPanel.Children.Add(boldButton);
                
                // Italic button
                var italicButton = new System.Windows.Controls.Primitives.ToggleButton { 
                    Content = "I", 
                    FontStyle = FontStyles.Italic,
                    Width = 30, 
                    Height = 25,
                    Margin = new Thickness(0, 0, 2, 0),
                    IsChecked = tb.FontStyle == FontStyles.Italic
                };
                italicButton.Checked += (s, e) => tb.FontStyle = FontStyles.Italic;
                italicButton.Unchecked += (s, e) => tb.FontStyle = FontStyles.Normal;
                formattingPanel.Children.Add(italicButton);
                
                // Underline button
                var underlineButton = new System.Windows.Controls.Primitives.ToggleButton { 
                    Content = "U", 
                    Width = 30, 
                    Height = 25,
                    IsChecked = tb.TextDecorations.Count > 0 && tb.TextDecorations.Contains(TextDecorations.Underline[0])
                };
                underlineButton.Checked += (s, e) => tb.TextDecorations.Add(TextDecorations.Underline);
                underlineButton.Unchecked += (s, e) => tb.TextDecorations.Clear();
                formattingPanel.Children.Add(underlineButton);
                
                ElementPropertiesPanel.Children.Add(formattingPanel);

                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Text Color:" });
                var colorBox = new TextBox { Text = (tb.Foreground as SolidColorBrush)?.Color.ToString() ?? "#000000" };
                colorBox.TextChanged += (s, e) => {
                    try { tb.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(colorBox.Text)); } catch { }
                };
                ElementPropertiesPanel.Children.Add(colorBox);
            }
            else if (element is Rectangle rect)
            {
                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Image" });
                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Width:" });
                var widthBox = new TextBox { Text = rect.Width.ToString() };
                widthBox.TextChanged += (s, e) => {
                    if (double.TryParse(widthBox.Text, out double w)) rect.Width = w;
                };
                ElementPropertiesPanel.Children.Add(widthBox);

                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Height:" });
                var heightBox = new TextBox { Text = rect.Height.ToString() };
                heightBox.TextChanged += (s, e) => {
                    if (double.TryParse(heightBox.Text, out double h)) rect.Height = h;
                };
                ElementPropertiesPanel.Children.Add(heightBox);

                ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Source:" });
                var sourceBox = new TextBox { Text = rect.Tag as string ?? "" };
                ElementPropertiesPanel.Children.Add(sourceBox);
                var browseBtn = new Button { Content = "Browse...", Margin = new Thickness(0,5,0,0) };
                browseBtn.Click += (s, e) => {
                    var dlg = new Microsoft.Win32.OpenFileDialog();
                    dlg.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                    if (dlg.ShowDialog() == true)
                    {
                        sourceBox.Text = dlg.FileName;
                        rect.Tag = dlg.FileName;
                        var imgBrush = new ImageBrush();
                        imgBrush.ImageSource = new BitmapImage(new System.Uri(dlg.FileName));
                        rect.Fill = imgBrush;
                    }
                };
                ElementPropertiesPanel.Children.Add(browseBtn);

                sourceBox.TextChanged += (s, e) => {
                    rect.Tag = sourceBox.Text;
                    try {
                        var imgBrush = new ImageBrush();
                        imgBrush.ImageSource = new BitmapImage(new System.Uri(sourceBox.Text));
                        rect.Fill = imgBrush;
                    } catch { rect.Fill = Brushes.LightGray; }
                };
            }
            else if (element is Image img)
            {
                // Handle Image elements (including QR codes and barcodes)
                bool isQRCode = img.Tag?.ToString() == "QRCode" || img.Tag?.ToString()?.StartsWith("QRCodeData:") == true;
                bool isBarcode = img.Tag?.ToString() == "Barcode13" || img.Tag?.ToString() == "Barcode8" || 
                                img.Tag?.ToString()?.StartsWith("BarcodeData:") == true;
                
                if (isQRCode)
                {
                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = "QR Code Data:" });
                    var dataBox = new TextBox { Text = img.Tag?.ToString()?.StartsWith("QRCodeData:") == true ? 
                        img.Tag.ToString().Substring("QRCodeData:".Length) : "Sample QR Data" };
                    ElementPropertiesPanel.Children.Add(dataBox);
                    
                    var previewImage = new Image { Width = 80, Height = 80, Margin = new Thickness(0,5,0,0) };
                    ElementPropertiesPanel.Children.Add(previewImage);
                    
                    dataBox.TextChanged += (s, e) => {
                        try {
                            var newQRImage = CreateBarcodeImage("QRCode", dataBox.Text, img.Width, img.Height);
                            img.Source = newQRImage.Source;
                            previewImage.Source = newQRImage.Source;
                            img.Tag = $"QRCodeData:{dataBox.Text}";
                        } catch { 
                            // Keep current image if generation fails
                        }
                    };
                    
                    // Set initial preview
                    previewImage.Source = img.Source;
                }
                else if (isBarcode)
                {
                    // Determine barcode type from tag
                    string barcodeType = "EAN13"; // default
                    string barcodeData = "";
                    
                    var tagStr = img.Tag?.ToString() ?? "";
                    if (tagStr.StartsWith("BarcodeData:"))
                    {
                        var parts = tagStr.Split(':');
                        if (parts.Length >= 3)
                        {
                            barcodeType = parts[1];
                            barcodeData = string.Join(":", parts.Skip(2));
                        }
                    }
                    else if (tagStr == "Barcode13")
                    {
                        barcodeType = "EAN13";
                        barcodeData = "4000000000004";
                    }
                    else if (tagStr == "Barcode8")
                    {
                        barcodeType = "EAN8";
                        barcodeData = "40000007";
                    }
                    
                    // Get display name for the barcode type
                    string displayName = GetBarcodeDisplayName(barcodeType);
                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = $"{displayName} Data:" });
                    
                    var dataBox = new TextBox { Text = barcodeData };
                    ElementPropertiesPanel.Children.Add(dataBox);
                    
                    var previewImage = new Image { Width = 120, Height = 60, Margin = new Thickness(0,5,0,0) };
                    ElementPropertiesPanel.Children.Add(previewImage);
                    
                    dataBox.TextChanged += (s, e) => {
                        try {
                            var newBarcodeImage = CreateBarcodeImage(barcodeType, dataBox.Text, img.Width, img.Height);
                            img.Source = newBarcodeImage.Source;
                            previewImage.Source = newBarcodeImage.Source;
                            img.Tag = $"BarcodeData:{barcodeType}:{dataBox.Text}";
                        } catch { 
                            // Keep current image if generation fails
                        }
                    };
                    
                    // Set initial preview
                    previewImage.Source = img.Source;
                }
                else
                {
                    // Handle regular images
                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Image Properties:" });
                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Width:" });
                    var widthBox = new TextBox { Text = img.Width.ToString() };
                    widthBox.TextChanged += (s, e) => {
                        if (double.TryParse(widthBox.Text, out double w)) img.Width = w;
                    };
                    ElementPropertiesPanel.Children.Add(widthBox);

                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Height:" });
                    var heightBox = new TextBox { Text = img.Height.ToString() };
                    heightBox.TextChanged += (s, e) => {
                        if (double.TryParse(heightBox.Text, out double h)) img.Height = h;
                    };
                    ElementPropertiesPanel.Children.Add(heightBox);
                }
            }
            else if (element is TextBlock barcode)
            {
                if (barcode.Text == "[EAN-13]" || barcode.Text == "[EAN-8]")
                {
                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = "Barcode Data:" });
                    var dataBox = new TextBox { Text = "" };
                    ElementPropertiesPanel.Children.Add(dataBox);
                    var barcodeImg = new Image { Width = 120, Height = 40, Margin = new Thickness(0,5,0,0) };
                    ElementPropertiesPanel.Children.Add(barcodeImg);
                    dataBox.TextChanged += (s, e) => {
                        barcode.Text = dataBox.Text;
                        try {
                            var writer = new ZXing.BarcodeWriterPixelData();
                            writer.Format = barcode.Text == "[EAN-13]" ? ZXing.BarcodeFormat.EAN_13 : ZXing.BarcodeFormat.EAN_8;
                            var pixelData = writer.Write(dataBox.Text);
                            var bmp = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                            var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
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
                            barcodeImg.Source = bi;
                        } catch { barcodeImg.Source = null; }
                    };
                }
                else if (barcode.Text == "[QR Code]")
                {
                    ElementPropertiesPanel.Children.Add(new TextBlock { Text = "QR Data:" });
                    var dataBox = new TextBox { Text = "" };
                    ElementPropertiesPanel.Children.Add(dataBox);
                    var qrImg = new Image { Width = 80, Height = 80, Margin = new Thickness(0,5,0,0) };
                    ElementPropertiesPanel.Children.Add(qrImg);
                    dataBox.TextChanged += (s, e) => {
                        barcode.Text = dataBox.Text;
                        try {
                            var qrGen = new QRCoder.QRCodeGenerator();
                            var qrData = qrGen.CreateQrCode(dataBox.Text, QRCoder.QRCodeGenerator.ECCLevel.Q);
                            var qrCode = new QRCoder.QRCode(qrData);
                            using (var bmp = qrCode.GetGraphic(10))
                            {
                                var ms = new System.IO.MemoryStream();
                                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;
                                var bi = new BitmapImage();
                                bi.BeginInit();
                                bi.StreamSource = ms;
                                bi.CacheOption = BitmapCacheOption.OnLoad;
                                bi.EndInit();
                                qrImg.Source = bi;
                            }
                        } catch { qrImg.Source = null; }
                    };
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedElement != null)
            {
                LabelCanvas.Children.Remove(selectedElement);
                RemoveResizingHandles();
                selectedElement = null;
                ElementPropertiesPanel.Children.Clear();
                ElementPropertiesPanel.Children.Add(new TextBlock 
                { 
                    Text = "Select an element to view its properties", 
                    Foreground = Brushes.Gray, 
                    FontStyle = FontStyles.Italic, 
                    TextWrapping = TextWrapping.Wrap, 
                    Margin = new Thickness(0,10,0,0) 
                });
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

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Populate template with current canvas content
                CurrentTemplate.Elements.Clear();
                CurrentTemplate.TemplateName = TemplateNameBox.Text ?? "Untitled Template";
                
                // Save all canvas elements (excluding resize handles)
                int elementIndex = 0;
                foreach (UIElement child in LabelCanvas.Children)
                {
                    if (child is System.Windows.Controls.Primitives.Thumb) continue; // Skip resize handles
                    
                    var labelElement = new EvoDesigner.Models.LabelElement
                    {
                        X = Canvas.GetLeft(child),
                        Y = Canvas.GetTop(child),
                        Width = child is FrameworkElement fe ? fe.Width : 100,
                        Height = child is FrameworkElement fe2 ? fe2.Height : 30
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
                    }
                    else if (child is System.Windows.Shapes.Rectangle rect)
                    {
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                        labelElement.Content = rect.Tag?.ToString() ?? "";
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
                            labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                            labelElement.Content = imageElement.Tag?.ToString() ?? "";
                        }
                    }
                    
                    CurrentTemplate.Elements.Add(labelElement);
                    elementIndex++;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog 
                { 
                    Filter = "Label Template (*.xml)|*.xml",
                    DefaultExt = "xml",
                    FileName = string.IsNullOrEmpty(CurrentTemplate.TemplateName) ? "MyTemplate" : CurrentTemplate.TemplateName
                };
                
                if (dlg.ShowDialog() == true)
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(EvoDesigner.Models.LabelTemplate));
                    using var fs = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Create);
                    serializer.Serialize(fs, CurrentTemplate);
                    
                    MessageBox.Show($"Template saved successfully to:\n{dlg.FileName}", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Access denied. Please choose a different location or run as administrator.", 
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (DirectoryNotFoundException)
            {
                MessageBox.Show("The specified directory was not found. Please choose a valid location.", 
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    
                    // Update template name textbox
                    TemplateNameBox.Text = CurrentTemplate.TemplateName ?? "Loaded Template";
                    
                    // Restore template dimensions to UI
                    LabelWidthBox.Text = CurrentTemplate.LabelWidth.ToString("F1");
                    LabelHeightBox.Text = CurrentTemplate.LabelHeight.ToString("F1");
                    MarginLeftBox.Text = CurrentTemplate.MarginLeft.ToString("F1");
                    MarginRightBox.Text = CurrentTemplate.MarginRight.ToString("F1");
                    MarginTopBox.Text = CurrentTemplate.MarginTop.ToString("F1");
                    MarginBottomBox.Text = CurrentTemplate.MarginBottom.ToString("F1");
                    
                    // Update canvas size and guidelines
                    UpdateCanvasSize();
                    
                    // Clear current canvas
                    LabelCanvas.Children.Clear();
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
                    
                    MessageBox.Show($"Template loaded successfully:\n{CurrentTemplate.TemplateName}", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("The selected file was not found.", "Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Access denied to the selected file.", "Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Invalid template file format:\n{ex.Message}", "Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading template:\n{ex.Message}", "Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private UIElement? CreateElementFromTemplate(EvoDesigner.Models.LabelElement element)
        {
            UIElement? result = null;
            
            switch (element.Type)
            {
                case EvoDesigner.Models.LabelElementType.Text:
                    var textBlock = new TextBlock
                    {
                        Text = element.Content ?? "Text",
                        FontSize = element.FontSize,
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
                    
                    result = textBlock;
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Barcode13:
                case EvoDesigner.Models.LabelElementType.Barcode8:
                case EvoDesigner.Models.LabelElementType.Barcode:
                case EvoDesigner.Models.LabelElementType.EAN13:
                case EvoDesigner.Models.LabelElementType.EAN8:
                case EvoDesigner.Models.LabelElementType.UPCA:
                case EvoDesigner.Models.LabelElementType.UPCE:
                case EvoDesigner.Models.LabelElementType.Code128:
                case EvoDesigner.Models.LabelElementType.Code39:
                case EvoDesigner.Models.LabelElementType.Aztec:
                case EvoDesigner.Models.LabelElementType.DataMatrix:
                case EvoDesigner.Models.LabelElementType.GS1DataBar:
                case EvoDesigner.Models.LabelElementType.PDF417:
                    var barcodeType = GetBarcodeTypeFromElement(element);
                    var barcodeData = element.Content ?? GetDefaultBarcodeData(barcodeType);
                    var barcodeImage = CreateBarcodeImage(barcodeType, barcodeData, element.Width, element.Height);
                    barcodeImage.Tag = $"BarcodeData:{barcodeType}:{barcodeData}";
                    result = barcodeImage;
                    break;
                    
                case EvoDesigner.Models.LabelElementType.QRCode:
                    var qrImage = CreateBarcodeImage("QRCode", element.Content ?? "Sample QR Data", element.Width, element.Height);
                    qrImage.Tag = $"QRCodeData:{element.Content ?? "Sample QR Data"}";
                    result = qrImage;
                    break;
                    
                case EvoDesigner.Models.LabelElementType.Image:
                    // Try to create an actual Image element if we have image data
                    if (!string.IsNullOrEmpty(element.Content))
                    {
                        try
                        {
                            var image = new Image();
                            
                            // Check if the content is a file path
                            if (System.IO.File.Exists(element.Content))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(element.Content);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                image.Source = bitmap;
                            }
                            else
                            {
                                // If file doesn't exist, try to create from URI anyway
                                try
                                {
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.UriSource = new Uri(element.Content);
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    image.Source = bitmap;
                                }
                                catch
                                {
                                    // If all else fails, create a placeholder rectangle
                                    var rect = new System.Windows.Shapes.Rectangle
                                    {
                                        Width = element.Width,
                                        Height = element.Height,
                                        Fill = Brushes.LightGray,
                                        Stroke = Brushes.DarkGray,
                                        StrokeThickness = 2,
                                        Tag = element.Content
                                    };
                                    result = rect;
                                    break;
                                }
                            }
                            
                            image.Width = element.Width;
                            image.Height = element.Height;
                            image.Stretch = Stretch.Uniform;
                            image.Tag = element.Content;
                            result = image;
                        }
                        catch
                        {
                            // Fallback to rectangle if image loading fails
                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = element.Width,
                                Height = element.Height,
                                Fill = Brushes.LightGray,
                                Stroke = Brushes.DarkGray,
                                StrokeThickness = 2,
                                Tag = element.Content
                            };
                            result = rect;
                        }
                    }
                    else
                    {
                        // No content, create placeholder rectangle
                        var rect = new System.Windows.Shapes.Rectangle
                        {
                            Width = element.Width,
                            Height = element.Height,
                            Fill = Brushes.LightGray,
                            Stroke = Brushes.DarkGray,
                            StrokeThickness = 2,
                            Tag = element.Content
                        };
                        result = rect;
                    }
                    break;
            }
            
            return result;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            // Use live template for immediate printing
            var liveTemplate = CreateLiveTemplate();
            var win = new EvoDesigner.UI.PrintPreviewWindow(liveTemplate, lastResultTable);
            win.ShowDialog();
        }

        private void PrintPreview_Click(object sender, RoutedEventArgs e)
        {
            var liveTemplate = CreateLiveTemplate();
            var win = new EvoDesigner.UI.PrintPreviewWindow(liveTemplate, lastResultTable);
            win.ShowDialog();
        }

        private EvoDesigner.Models.LabelTemplate CreateLiveTemplate()
        {
            var liveTemplate = new EvoDesigner.Models.LabelTemplate
            {
                TemplateName = CurrentTemplate.TemplateName,
                LabelWidth = CurrentTemplate.LabelWidth,
                LabelHeight = CurrentTemplate.LabelHeight,
                MarginLeft = CurrentTemplate.MarginLeft,
                MarginRight = CurrentTemplate.MarginRight,
                MarginTop = CurrentTemplate.MarginTop,
                MarginBottom = CurrentTemplate.MarginBottom
            };

            // Convert current canvas elements to template elements
            int elementIndex = 0;
            foreach (UIElement child in LabelCanvas.Children)
            {
                if (child is System.Windows.Controls.Primitives.Thumb) continue; // Skip resize handles
                
                var labelElement = new EvoDesigner.Models.LabelElement
                {
                    X = Canvas.GetLeft(child),
                    Y = Canvas.GetTop(child),
                    Width = child is FrameworkElement fe ? fe.Width : 100,
                    Height = child is FrameworkElement fe2 ? fe2.Height : 30
                };
                
                if (child is TextBlock tb)
                {
                    // Check if this is a barcode fallback display
                    if (tb.Text.StartsWith("[") && tb.Text.EndsWith("]"))
                    {
                        var barcodeDisplayText = tb.Text.Trim('[', ']');
                        switch (barcodeDisplayText)
                        {
                            case "EAN-13":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.EAN13;
                                labelElement.Content = "4000000000004";
                                labelElement.BarcodeType = "EAN13";
                                break;
                            case "EAN-8":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.EAN8;
                                labelElement.Content = "40000007";
                                labelElement.BarcodeType = "EAN8";
                                break;
                            case "UPC-A":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.UPCA;
                                labelElement.Content = "012345678905";
                                labelElement.BarcodeType = "UPCA";
                                break;
                            case "UPC-E":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.UPCE;
                                labelElement.Content = "01234565";
                                labelElement.BarcodeType = "UPCE";
                                break;
                            case "Code 128":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.Code128;
                                labelElement.Content = "HELLO123";
                                labelElement.BarcodeType = "Code128";
                                break;
                            case "Code 39":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.Code39;
                                labelElement.Content = "HELLO39";
                                labelElement.BarcodeType = "Code39";
                                break;
                            case "QR Code":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.QRCode;
                                labelElement.Content = "Sample QR Data";
                                labelElement.BarcodeType = "QRCode";
                                break;
                            case "Aztec":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.Aztec;
                                labelElement.Content = "Sample Aztec Data";
                                labelElement.BarcodeType = "Aztec";
                                break;
                            case "Data Matrix":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.DataMatrix;
                                labelElement.Content = "Sample Data Matrix";
                                labelElement.BarcodeType = "DataMatrix";
                                break;
                            case "GS1 DataBar":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.GS1DataBar;
                                labelElement.Content = "01234567890123";
                                labelElement.BarcodeType = "GS1DataBar";
                                break;
                            case "PDF417":
                                labelElement.Type = EvoDesigner.Models.LabelElementType.PDF417;
                                labelElement.Content = "Sample PDF417 Data";
                                labelElement.BarcodeType = "PDF417";
                                break;
                            default:
                                labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                                labelElement.Content = tb.Text;
                                break;
                        }
                    }
                    else
                    {
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Text;
                        labelElement.Content = tb.Text;
                    }
                        
                    labelElement.FontSize = tb.FontSize;
                    labelElement.FontFamily = tb.FontFamily.ToString();
                    labelElement.FontWeight = tb.FontWeight.ToString();
                    labelElement.FontStyle = tb.FontStyle.ToString();
                    labelElement.Foreground = tb.Foreground.ToString();
                }
                else if (child is System.Windows.Shapes.Rectangle rect)
                {
                    labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                    labelElement.Content = rect.Tag?.ToString() ?? "";
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
                        labelElement.Type = EvoDesigner.Models.LabelElementType.Image;
                        labelElement.Content = imageElement.Tag?.ToString() ?? "";
                    }
                }
                
                liveTemplate.Elements.Add(labelElement);
                elementIndex++;
            }

            return liveTemplate;
        }

        private void DataSource_Click(object sender, RoutedEventArgs e)
        {
            var win = new EvoDesigner.UI.DataSourceWindow();
            if (win.ShowDialog() == true)
            {
                lastResultTable = win.ResultTable;
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
            
            // Convert canvas children to a list (excluding resize handles)
            var labelElements = new List<UIElement>();
            foreach (UIElement child in LabelCanvas.Children)
            {
                // Skip resize handles (Thumb controls)
                if (!(child is System.Windows.Controls.Primitives.Thumb))
                {
                    labelElements.Add(child);
                }
            }
            
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
                ApplyDataToElements();
            }
        }

        private void ApplyDataToElements()
        {
            if (lastResultTable == null || CurrentTemplate.FieldMappings.Count == 0) return;
            var row = lastResultTable.Rows[0];
            foreach (var mapping in CurrentTemplate.FieldMappings)
            {
                if (mapping.ElementIndex < 0 || mapping.ElementIndex >= LabelCanvas.Children.Count) continue;
                var el = LabelCanvas.Children[mapping.ElementIndex];
                var value = row[mapping.FieldName]?.ToString();
                
                if (el is TextBlock tb) 
                {
                    tb.Text = value;
                }
                else if (el is Rectangle rect) 
                {
                    rect.Tag = value;
                }
                else if (el is Image imageElement)
                {
                    if (imageElement.Tag?.ToString() == "QRCode" || imageElement.Tag?.ToString()?.StartsWith("QRCodeData:") == true)
                    {
                        // This is a QR code - generate new QR code with the data
                        if (!string.IsNullOrEmpty(value))
                        {
                            var newQRImage = CreateBarcodeImage("QRCode", value, imageElement.Width, imageElement.Height);
                            imageElement.Source = newQRImage.Source;
                            imageElement.Tag = $"QRCodeData:{value}";
                        }
                    }
                    else if (imageElement.Tag?.ToString()?.StartsWith("BarcodeData:") == true)
                    {
                        // This is a barcode - generate new barcode with the data
                        if (!string.IsNullOrEmpty(value))
                        {
                            var tagStr = imageElement.Tag.ToString();
                            var parts = tagStr.Split(':');
                            if (parts.Length >= 2)
                            {
                                var barcodeType = parts[1]; // Get the actual barcode type
                                var newBarcodeImage = CreateBarcodeImage(barcodeType, value, imageElement.Width, imageElement.Height);
                                imageElement.Source = newBarcodeImage.Source;
                                imageElement.Tag = $"BarcodeData:{barcodeType}:{value}";
                            }
                        }
                    }
                    else
                    {
                        // Regular image
                        imageElement.Tag = value;
                    }
                }
            }
        }

        #region Zoom Functionality

        private void InitializeZoomControls()
        {
            // Wire up zoom button events
            ZoomInButton.Click += (s, e) => ZoomIn();
            ZoomOutButton.Click += (s, e) => ZoomOut();
            ZoomResetButton.Click += (s, e) => ZoomReset();
            ZoomFitButton.Click += (s, e) => ZoomToFit();
            
            // Add mouse wheel zoom support
            CanvasScrollViewer.PreviewMouseWheel += CanvasScrollViewer_PreviewMouseWheel;
            
            UpdateZoomDisplay();
        }

        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Check if Ctrl key is pressed for zoom
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                
                if (e.Delta > 0)
                    ZoomIn();
                else
                    ZoomOut();
            }
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
            // Calculate zoom to fit the label in the visible area
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
            // Scale the canvas content
            CanvasScaleTransform.ScaleX = _zoomFactor;
            CanvasScaleTransform.ScaleY = _zoomFactor;
            
            // Don't scale rulers - instead update them to account for zoom in their content generation
            // This keeps line thickness and text size constant
            
            // Update rulers to generate marks with proper spacing for current zoom
            UpdateRulers();
            
            UpdateZoomDisplay();
        }

        private void UpdateZoomDisplay()
        {
            ZoomTextBlock.Text = $"{(_zoomFactor * 100):F0}%";
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Evolabs IT Solutions Ltd (c) 2025", "About EvoDesigner", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
