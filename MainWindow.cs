using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;
using TaggJack.Commands;
using TaggJack.Models;
using TaggJack.Services;
using TaggJack.Services.Inkjet;
using TaggJack.Services.Inkjet.Linx;
using TaggJack.Services.License;
using TaggJack.UI;
using Material.Icons;
using Material.Icons.WPF;
using Microsoft.Win32;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace TaggJack;

public partial class MainWindow : Window {
	private struct HsvColor
	{
		public double H;

		public double S;

		public double V;
	}

	private LabelTemplate CurrentTemplate = new LabelTemplate();

	private System.Windows.Point dragStart;

	private bool isDragging;

	private bool isDragInProgress;

	private DateTime lastElementCreationTime = DateTime.MinValue;

	private string lastElementType = "";

	private System.Windows.Point lastElementPosition = new System.Windows.Point(-1000.0, -1000.0);

	private UIElement? selectedElement;

	private DataTable? lastResultTable;

	private bool _gs1SampleActive;

	private int _gs1SampleLevel;

	private Thumb? resizeThumb;

	private AppConfig _appConfig;

	private DataSourceConfig? _currentDataSource;

	private bool _isExplicitExit;

	private bool _isDirty;

	private string? _currentFilePath;

	private double _labelWidthMm = 70.0;

	private double _labelHeightMm = 30.0;

	private double _zoomFactor = 1.0;

	private const double MIN_ZOOM = 0.1;

	private const double MAX_ZOOM = 5.0;

	private const double ZOOM_STEP = 0.1;

	private bool isSelecting;

	private System.Windows.Point selectionStart;

	private System.Windows.Shapes.Rectangle? selectionRectangle;

	private List<UIElement> selectedElements = new List<UIElement>();

	private CommandHistory commandHistory = new CommandHistory();

	private readonly InkjetPrintService _inkjetPrintService = new InkjetPrintService();

	private readonly LinxPrintService _linxPrintService = new LinxPrintService();

	private InkjetProtocolLogWindow? _inkjetProtocolLogWindow;

	private bool _inkjetRunInProgress;

	private CancellationTokenSource? _inkjetRunCts;

	private Dictionary<UIElement, double> elementRotations = new Dictionary<UIElement, double>();

	private List<LabelElement> clipboardElements = new List<LabelElement>();






















































	internal static bool ApplicationIsExiting { get; private set; }

	public MainWindow()
	{
		string text = Guid.NewGuid().ToString().Substring(0, 8);
		Console.WriteLine("[DEBUG] MainWindow constructor called - Instance: " + text);
		try
		{
			File.AppendAllText("mainwindow-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow constructor - Instance: {text}{Environment.NewLine}");
			File.AppendAllText("mainwindow-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stack trace:{Environment.NewLine}{Environment.StackTrace}{Environment.NewLine}");
		}
		catch
		{
		}
		InitializeComponent();
		_appConfig = ConfigService.LoadConfig();
		RefreshLicenseBanner();
		StartLicenseRefreshTimer();
		base.Loaded += async (_, _) =>
		{
			try
			{
				await PortalLicenseService.Instance.CheckWithPortalAsync();
				RefreshLicenseBanner();
			}
			catch
			{
			}
		};
		base.Loaded += MainWindow_Loaded;
		LabelCanvas.Drop += LabelCanvas_Drop;
		LabelCanvas.AllowDrop = true;
		LabelCanvas.MouseLeftButtonDown += LabelCanvas_MouseLeftButtonDown;
		base.PreviewKeyDown += MainWindow_KeyDown;
		InitializeZoomControls();
		Button button = FindButtonByContent("Save Template");
		if (button != null)
		{
			button.Click += SaveTemplate_Click;
		}
		Button button2 = FindButtonByContent("Load Template");
		if (button2 != null)
		{
			button2.Click += LoadTemplate_Click;
		}
		Button button3 = FindButtonByContent("Print Preview");
		if (button3 != null)
		{
			button3.Click += PrintPreview_Click;
		}
		Button button4 = FindButtonByContent("Setup Data Source");
		if (button4 != null)
		{
			button4.Click += DataSource_Click;
		}
		InitializeDefaultTemplate();
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		AttachToolboxDragHandlers();
		InitializeLayerControls();
		if (InkjetPanel != null)
		{
			InkjetPanel.ConfigChanged += delegate
			{
				if (CurrentTemplate.TemplateType.IsInkjetLike())
				{
					CurrentTemplate.InkjetConfig = InkjetPanel.ToConfig();
					SetDirty();
				}
			};
			InkjetPanel.RunRequested += async delegate
			{
				await RunInkjetTemplateAsync();
			};
		}
		ApplyTemplateTypeUi(CurrentTemplate.TemplateType);
		ConfigureGs1MenuVisibility();
		if (_appConfig.Mode == "flow")
		{
			InitializeFlowModeBehavior();
		}
	}

	private void ConfigureGs1MenuVisibility()
	{
		_appConfig = ConfigService.LoadConfig();
		bool flag = FeatureFlags.IsGs1Enabled(_appConfig);
		bool flag2 = FeatureFlags.IsErpImportEnabled(_appConfig);
		Gs1Menu.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		Gs1SampleDataMenuItem.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		ErpImportMenuItem.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void ItemMaster_Click(object sender, RoutedEventArgs e)
	{
		ItemMasterWindow itemMasterWindow = new ItemMasterWindow();
		itemMasterWindow.Owner = this;
		itemMasterWindow.ShowDialog();
	}

	private void ErpImport_Click(object sender, RoutedEventArgs e)
	{
		_appConfig = ConfigService.LoadConfig();
		if (FeatureFlags.IsErpImportEnabled(_appConfig))
		{
			ErpImportWizardWindow erpImportWizardWindow = new ErpImportWizardWindow(_appConfig);
			erpImportWizardWindow.Owner = this;
			erpImportWizardWindow.ShowDialog();
		}
	}

	private void GenerateGs1_Click(object sender, RoutedEventArgs e)
	{
		_appConfig = ConfigService.LoadConfig();
		GenerateGs1LabelsWindow generateGs1LabelsWindow = new GenerateGs1LabelsWindow(_appConfig);
		generateGs1LabelsWindow.Owner = this;
		generateGs1LabelsWindow.ShowDialog();
	}

	private void Gs1History_Click(object sender, RoutedEventArgs e)
	{
		Gs1HistoryWindow gs1HistoryWindow = new Gs1HistoryWindow();
		gs1HistoryWindow.Owner = this;
		gs1HistoryWindow.ShowDialog();
	}

	private void Gs1Settings_Click(object sender, RoutedEventArgs e)
	{
		_appConfig = ConfigService.LoadConfig();
		Gs1SettingsWindow gs1SettingsWindow = new Gs1SettingsWindow(_appConfig);
		gs1SettingsWindow.Owner = this;
		gs1SettingsWindow.ShowDialog();
	}

	private void InitializeLayerControls()
	{
		if (LayerControlsContainer.Children.Count <= 0)
		{
			Button button = new Button
			{
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
				Height = 28.0,
				ToolTip = "Bring selected element forward"
			};
			button.Style = (Style)FindResource("CompactButtonStyle");
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			MaterialIcon element = new MaterialIcon
			{
				Kind = MaterialIconKind.ArrangeBringForward,
				Width = 16.0,
				Height = 16.0,
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0)
			};
			TextBlock element2 = new TextBlock
			{
				Text = "Bring Forward",
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(element);
			stackPanel.Children.Add(element2);
			button.Content = stackPanel;
			LayerControlsContainer.Children.Add(button);
			button.Click += BringForward_Click;
			Button button2 = new Button
			{
				Margin = new Thickness(0.0, 0.0, 0.0, 0.0),
				Height = 28.0,
				ToolTip = "Send selected element backward"
			};
			button2.Style = (Style)FindResource("CompactButtonStyle");
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			MaterialIcon element3 = new MaterialIcon
			{
				Kind = MaterialIconKind.ArrangeSendBackward,
				Width = 16.0,
				Height = 16.0,
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0)
			};
			TextBlock element4 = new TextBlock
			{
				Text = "Send Backward",
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel2.Children.Add(element3);
			stackPanel2.Children.Add(element4);
			button2.Content = stackPanel2;
			LayerControlsContainer.Children.Add(button2);
			button2.Click += SendBackward_Click;
		}
	}

	private void InitializeDefaultTemplate()
	{
		_labelWidthMm = 70.0;
		_labelHeightMm = 30.0;
		UpdateDisplayedDimensions();
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
		SetDirty();
	}

	private void UpdateCanvasSize()
	{
		double labelWidthMm = _labelWidthMm;
		double labelHeightMm = _labelHeightMm;
		double marginLeft = 2.0;
		double marginRight = 2.0;
		double marginTop = 2.0;
		double marginBottom = 2.0;
		double num = MeasurementService.MmToScreenPixels(labelWidthMm);
		double num2 = MeasurementService.MmToScreenPixels(labelHeightMm);
		LabelCanvas.Width = num;
		LabelCanvas.Height = num2;
		LabelCanvas.Clip = new RectangleGeometry(new Rect(0.0, 0.0, num, num2));
		GridCanvas.Width = num;
		GridCanvas.Height = num2;
		MarginCanvas.Width = num;
		MarginCanvas.Height = num2;
		double num3 = 100.0;
		CanvasContainer.Width = num + num3;
		CanvasContainer.Height = num2 + num3;
		CurrentTemplate.LabelWidth = labelWidthMm;
		CurrentTemplate.LabelHeight = labelHeightMm;
		CurrentTemplate.MarginLeft = marginLeft;
		CurrentTemplate.MarginRight = marginRight;
		CurrentTemplate.MarginTop = marginTop;
		CurrentTemplate.MarginBottom = marginBottom;
		UpdateMarginGuidelines();
		UpdateRulers();
		UpdateGrid();
	}

	private void UpdateMarginGuidelines()
	{
		MarginCanvas.Children.Clear();
		double width = LabelCanvas.Width;
		double height = LabelCanvas.Height;
		double num = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginLeft);
		double num2 = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginRight);
		double num3 = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginTop);
		double num4 = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginBottom);
		SolidColorBrush stroke = new SolidColorBrush(Colors.Red)
		{
			Opacity = 0.7
		};
		if (num > 0.0)
		{
			Line element = new Line
			{
				X1 = num,
				Y1 = 0.0,
				X2 = num,
				Y2 = height,
				Stroke = stroke,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 2.0 }
			};
			MarginCanvas.Children.Add(element);
		}
		if (num2 > 0.0)
		{
			Line element2 = new Line
			{
				X1 = width - num2,
				Y1 = 0.0,
				X2 = width - num2,
				Y2 = height,
				Stroke = stroke,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 2.0 }
			};
			MarginCanvas.Children.Add(element2);
		}
		if (num3 > 0.0)
		{
			Line element3 = new Line
			{
				X1 = 0.0,
				Y1 = num3,
				X2 = width,
				Y2 = num3,
				Stroke = stroke,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 2.0 }
			};
			MarginCanvas.Children.Add(element3);
		}
		if (num4 > 0.0)
		{
			Line element4 = new Line
			{
				X1 = 0.0,
				Y1 = height - num4,
				X2 = width,
				Y2 = height - num4,
				Stroke = stroke,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 2.0 }
			};
			MarginCanvas.Children.Add(element4);
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
		TopRuler.Width = LabelCanvas.Width * _zoomFactor;
		SolidColorBrush black = System.Windows.Media.Brushes.Black;
		double labelWidth = CurrentTemplate.LabelWidth;
		for (double num = 0.0; num <= labelWidth; num += 1.0)
		{
			double num2 = MeasurementService.MmToScreenPixels(num) * _zoomFactor;
			bool flag = num % 10.0 == 0.0;
			double num3 = (flag ? 15 : 8);
			Line element = new Line
			{
				X1 = num2,
				Y1 = 25.0 - num3,
				X2 = num2,
				Y2 = 25.0,
				Stroke = black,
				StrokeThickness = (flag ? 1.0 : 0.5)
			};
			TopRuler.Children.Add(element);
			if (flag && num > 0.0)
			{
				TextBlock element2 = new TextBlock
				{
					Text = num.ToString(),
					FontSize = 8.0,
					Foreground = black
				};
				Canvas.SetLeft(element2, num2 - 10.0);
				Canvas.SetTop(element2, 2.0);
				TopRuler.Children.Add(element2);
			}
		}
	}

	private void UpdateLeftRuler()
	{
		LeftRuler.Children.Clear();
		LeftRuler.Height = LabelCanvas.Height * _zoomFactor;
		SolidColorBrush black = System.Windows.Media.Brushes.Black;
		double labelHeight = CurrentTemplate.LabelHeight;
		for (double num = 0.0; num <= labelHeight; num += 1.0)
		{
			double num2 = MeasurementService.MmToScreenPixels(num) * _zoomFactor;
			bool flag = num % 10.0 == 0.0;
			double num3 = (flag ? 15 : 8);
			Line element = new Line
			{
				X1 = 25.0 - num3,
				Y1 = num2,
				X2 = 25.0,
				Y2 = num2,
				Stroke = black,
				StrokeThickness = (flag ? 1.0 : 0.5)
			};
			LeftRuler.Children.Add(element);
			if (flag && num > 0.0)
			{
				TextBlock element2 = new TextBlock
				{
					Text = num.ToString(),
					FontSize = 8.0,
					Foreground = black,
					RenderTransform = new RotateTransform(-90.0)
				};
				Canvas.SetLeft(element2, 2.0);
				Canvas.SetTop(element2, num2 + 10.0);
				LeftRuler.Children.Add(element2);
			}
		}
	}

	private void UpdateGrid()
	{
		GridCanvas.Children.Clear();
		GridCanvas.Width = LabelCanvas.Width;
		GridCanvas.Height = LabelCanvas.Height;
		GridCanvas.Visibility = Visibility.Visible;
		SolidColorBrush solidColorBrush = new SolidColorBrush(Colors.LightGray)
		{
			Opacity = 0.4
		};
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(Colors.LightGray)
		{
			Opacity = 0.6
		};
		double labelWidth = CurrentTemplate.LabelWidth;
		double labelHeight = CurrentTemplate.LabelHeight;
		int num = 0;
		for (double num2 = 5.0; num2 < labelWidth; num2 += 5.0)
		{
			double num3 = MeasurementService.MmToScreenPixels(num2);
			bool flag = num2 % 10.0 == 0.0;
			Line element = new Line
			{
				X1 = num3,
				Y1 = 0.0,
				X2 = num3,
				Y2 = GridCanvas.Height,
				Stroke = (flag ? solidColorBrush2 : solidColorBrush),
				StrokeThickness = (flag ? 0.8 : 0.5)
			};
			GridCanvas.Children.Add(element);
			num++;
		}
		for (double num4 = 5.0; num4 < labelHeight; num4 += 5.0)
		{
			double num5 = MeasurementService.MmToScreenPixels(num4);
			bool flag2 = num4 % 10.0 == 0.0;
			Line element2 = new Line
			{
				X1 = 0.0,
				Y1 = num5,
				X2 = GridCanvas.Width,
				Y2 = num5,
				Stroke = (flag2 ? solidColorBrush2 : solidColorBrush),
				StrokeThickness = (flag2 ? 0.8 : 0.5)
			};
			GridCanvas.Children.Add(element2);
			num++;
		}
	}

	private void ShowGridCheckBox_Changed(object sender, RoutedEventArgs e)
	{
	}

	private Button FindButtonByContent(string content)
	{
		return FindElementByContent<Button>(base.Content as DependencyObject, content);
	}

	private T? FindElementByContent<T>(DependencyObject? parent, string content) where T : Control
	{
		if (parent == null)
		{
			return null;
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is Button { Content: var content2 } && content2?.ToString() == content && child is T result)
			{
				return result;
			}
			T val = FindElementByContent<T>(child, content);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	private string ConvertImageToBase64(string filePath)
	{
		try
		{
			return Convert.ToBase64String(File.ReadAllBytes(filePath));
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to convert image to base64: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			return string.Empty;
		}
	}

	private BitmapImage ConvertBase64ToImage(string base64String)
	{
		try
		{
			byte[] buffer = Convert.FromBase64String(base64String);
			BitmapImage bitmapImage = new BitmapImage();
			using (MemoryStream streamSource = new MemoryStream(buffer))
			{
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = streamSource;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
			}
			return bitmapImage;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to convert base64 to image: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			return new BitmapImage();
		}
	}

	private System.Windows.Controls.Image CreateImageFromFile(string filePath)
	{
		System.Windows.Controls.Image image = new System.Windows.Controls.Image();
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.UriSource = new Uri(filePath);
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.EndInit();
		image.Source = bitmapImage;
		image.Width = 100.0;
		image.Height = 100.0;
		image.Stretch = Stretch.Uniform;
		string text = ConvertImageToBase64(filePath);
		image.Tag = "ImageData:" + text;
		return image;
	}

	private System.Windows.Controls.Image CreateImageFromBase64(string base64Data, double width = 100.0, double height = 100.0)
	{
		System.Windows.Controls.Image image = new System.Windows.Controls.Image();
		BitmapImage source = ConvertBase64ToImage(base64Data);
		image.Source = source;
		image.Width = width;
		image.Height = height;
		image.Stretch = Stretch.Uniform;
		image.Tag = "ImageData:" + base64Data;
		return image;
	}

	private string ConvertBitmapSourceToBase64(BitmapSource bitmapSource)
	{
		try
		{
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
			using MemoryStream memoryStream = new MemoryStream();
			pngBitmapEncoder.Save(memoryStream);
			return Convert.ToBase64String(memoryStream.ToArray());
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to convert bitmap source to base64: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			return string.Empty;
		}
	}

	private System.Windows.Controls.Image CreateBarcodeImage(string symbology, string data, double width = 120.0, double height = 60.0)
	{
		try
		{
			BarcodeFormat barcodeFormat;
			string text;
			switch (symbology.ToUpper())
			{
			case "EAN13":
				barcodeFormat = BarcodeFormat.EAN_13;
				text = "4000000000004";
				break;
			case "EAN8":
				barcodeFormat = BarcodeFormat.EAN_8;
				text = "40000007";
				break;
			case "UPCA":
				barcodeFormat = BarcodeFormat.UPC_A;
				text = "012345678905";
				break;
			case "UPCE":
				barcodeFormat = BarcodeFormat.UPC_E;
				text = "01234565";
				break;
			case "CODE128":
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "HELLO123";
				break;
			case "CODE39":
				barcodeFormat = BarcodeFormat.CODE_39;
				text = "HELLO39";
				break;
			case "QRCODE":
				barcodeFormat = BarcodeFormat.QR_CODE;
				text = "Sample QR Data";
				break;
			case "AZTEC":
				barcodeFormat = BarcodeFormat.AZTEC;
				text = "Sample Aztec Data";
				break;
			case "DATAMATRIX":
				barcodeFormat = BarcodeFormat.DATA_MATRIX;
				text = "Sample Data Matrix";
				break;
			case "GS1DATABAR":
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "]C101234567890128";
				break;
			case "PDF417":
				barcodeFormat = BarcodeFormat.PDF_417;
				text = "Sample PDF417 Data";
				break;
			default:
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "FALLBACK";
				break;
			}
			if (string.IsNullOrEmpty(data))
			{
				data = text;
			}
			data = ValidateBarcodeData(symbology, data);
			BarcodeWriterPixelData obj = new BarcodeWriterPixelData
			{
				Format = barcodeFormat
			};
			EncodingOptions encodingOptions = new EncodingOptions
			{
				Width = (int)width * 2,
				Height = (int)height * 2,
				Margin = 1
			};
			switch (barcodeFormat)
			{
			case BarcodeFormat.QR_CODE:
				encodingOptions.Margin = 1;
				break;
			case BarcodeFormat.PDF_417:
				encodingOptions.Margin = 5;
				break;
			}
			obj.Options = encodingOptions;
			PixelData pixelData = obj.Write(data);
			Bitmap bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
			BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
			Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
			bitmap.UnlockBits(bitmapData);
			using MemoryStream memoryStream = new MemoryStream();
			bitmap.Save(memoryStream, ImageFormat.Png);
			memoryStream.Position = 0L;
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.StreamSource = memoryStream;
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			return new System.Windows.Controls.Image
			{
				Source = bitmapImage,
				Width = width,
				Height = height,
				Stretch = Stretch.Uniform,
				Tag = "BarcodeData:" + symbology + ":" + data
			};
		}
		catch (Exception)
		{
			TextBlock element = new TextBlock
			{
				Text = "[" + symbology + "]",
				FontSize = 10.0,
				Foreground = System.Windows.Media.Brushes.DarkBlue,
				Width = width,
				Height = height,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap
			};
			Canvas canvas = new Canvas
			{
				Width = width,
				Height = height,
				Background = System.Windows.Media.Brushes.LightGray
			};
			Canvas.SetLeft(element, 0.0);
			Canvas.SetTop(element, height / 2.0 - 10.0);
			canvas.Children.Add(element);
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)width, (int)height, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(canvas);
			return new System.Windows.Controls.Image
			{
				Source = renderTargetBitmap,
				Width = width,
				Height = height,
				Tag = "BarcodeData:" + symbology + ":" + data
			};
		}
	}

	private LabelElementType GetLabelElementTypeFromBarcodeType(string barcodeType)
	{
		return barcodeType.ToUpper() switch
		{
			"EAN13" => LabelElementType.EAN13, 
			"EAN8" => LabelElementType.EAN8, 
			"UPCA" => LabelElementType.UPCA, 
			"UPCE" => LabelElementType.UPCE, 
			"CODE128" => LabelElementType.Code128, 
			"CODE39" => LabelElementType.Code39, 
			"AZTEC" => LabelElementType.Aztec, 
			"DATAMATRIX" => LabelElementType.DataMatrix, 
			"GS1DATABAR" => LabelElementType.GS1DataBar, 
			"PDF417" => LabelElementType.PDF417, 
			"QRCODE" => LabelElementType.QRCode, 
			_ => LabelElementType.EAN13, 
		};
	}

	private string GetBarcodeTypeFromElement(LabelElement element)
	{
		switch (element.Type)
		{
		case LabelElementType.EAN13:
		case LabelElementType.Barcode13:
			return "EAN13";
		case LabelElementType.EAN8:
		case LabelElementType.Barcode8:
			return "EAN8";
		case LabelElementType.UPCA:
			return "UPCA";
		case LabelElementType.UPCE:
			return "UPCE";
		case LabelElementType.Code128:
			return "Code128";
		case LabelElementType.Code39:
			return "Code39";
		case LabelElementType.Aztec:
			return "Aztec";
		case LabelElementType.DataMatrix:
			return "DataMatrix";
		case LabelElementType.GS1DataBar:
			return "GS1DataBar";
		case LabelElementType.PDF417:
			return "PDF417";
		case LabelElementType.QRCode:
			return "QRCode";
		default:
			return element.BarcodeType ?? "EAN13";
		}
	}

	private string GetDefaultBarcodeData(string barcodeType)
	{
		return barcodeType.ToUpper() switch
		{
			"EAN13" => "4000000000004", 
			"EAN8" => "40000007", 
			"UPCA" => "012345678905", 
			"UPCE" => "01234565", 
			"CODE128" => "HELLO123", 
			"CODE39" => "HELLO39", 
			"QRCODE" => "Sample QR Data", 
			"AZTEC" => "Sample Aztec Data", 
			"DATAMATRIX" => "Sample Data Matrix", 
			"GS1DATABAR" => "]C101234567890128", 
			"PDF417" => "Sample PDF417 Data", 
			_ => "Sample Data", 
		};
	}

	private string ValidateBarcodeData(string symbology, string data)
	{
		switch (symbology.ToUpper())
		{
		case "EAN13":
		{
			string text5 = new string(data.Where(char.IsDigit).ToArray());
			if (text5.Length < 12)
			{
				text5 = text5.PadRight(12, '0');
			}
			else if (text5.Length > 12)
			{
				text5 = text5.Substring(0, 12);
			}
			int num3 = 0;
			for (int k = 0; k < 12; k++)
			{
				num3 += (text5[k] - 48) * ((k % 2 == 0) ? 1 : 3);
			}
			return text5 + (10 - num3 % 10) % 10;
		}
		case "EAN8":
		{
			string text3 = new string(data.Where(char.IsDigit).ToArray());
			if (text3.Length < 7)
			{
				text3 = text3.PadRight(7, '0');
			}
			else if (text3.Length > 7)
			{
				text3 = text3.Substring(0, 7);
			}
			int num2 = 0;
			for (int j = 0; j < 7; j++)
			{
				num2 += (text3[j] - 48) * ((j % 2 != 0) ? 1 : 3);
			}
			return text3 + (10 - num2 % 10) % 10;
		}
		case "GS1DATABAR":
		{
			string text4 = new string(data.Where(char.IsDigit).ToArray());
			if (text4.Length < 14)
			{
				text4 = text4.PadLeft(14, '0');
			}
			else if (text4.Length > 14)
			{
				text4 = text4.Substring(0, 14);
			}
			return "]C1" + text4;
		}
		case "UPCA":
		{
			string text2 = new string(data.Where(char.IsDigit).ToArray());
			if (text2.Length < 11)
			{
				text2 = text2.PadRight(11, '0');
			}
			else if (text2.Length > 11)
			{
				text2 = text2.Substring(0, 11);
			}
			int num = 0;
			for (int i = 0; i < 11; i++)
			{
				num += (text2[i] - 48) * ((i % 2 != 0) ? 1 : 3);
			}
			return text2 + (10 - num % 10) % 10;
		}
		case "UPCE":
		{
			string text = new string(data.Where(char.IsDigit).ToArray());
			if (text.Length < 8)
			{
				text = text.PadRight(8, '0');
			}
			else if (text.Length > 8)
			{
				text = text.Substring(0, 8);
			}
			return text;
		}
		default:
			return data;
		}
	}

	private string GetBarcodeDisplayName(string symbology)
	{
		return symbology.ToUpper() switch
		{
			"EAN13" => "EAN-13", 
			"EAN8" => "EAN-8", 
			"UPCA" => "UPC-A", 
			"UPCE" => "UPC-E", 
			"CODE128" => "Code 128", 
			"CODE39" => "Code 39", 
			"QRCODE" => "QR Code", 
			"AZTEC" => "Aztec", 
			"DATAMATRIX" => "Data Matrix", 
			"GS1DATABAR" => "GS1 DataBar", 
			"PDF417" => "PDF417", 
			_ => symbology, 
		};
	}

	private void AttachToolboxDragHandlers()
	{
		List<Button> list = FindElementsByType<Button>(base.Content as DependencyObject);
		Canvas canvas = LabelCanvas;
		foreach (Button item in list)
		{
			string text = item.Content?.ToString();
			string elementType = null;
			if (text != null && text.Contains("Text") && text != null && !text.Contains("Linked"))
			{
				elementType = "Text";
			}
			else if (text != null && text.Contains("Image") && text != null && !text.Contains("Linked"))
			{
				elementType = "Image";
			}
			else if (text != null && text.Contains("Linked Image"))
			{
				elementType = "LinkedImage";
			}
			else if (text != null && text.Contains("Line"))
			{
				elementType = "Line";
			}
			else if (text != null && text.Contains("Rectangle"))
			{
				elementType = "Rectangle";
			}
			else if (text != null && text.Contains("Ellipse"))
			{
				elementType = "Ellipse";
			}
			else if (text != null && text.Contains("EAN-13"))
			{
				elementType = "EAN13";
			}
			else if (text != null && text.Contains("EAN-8"))
			{
				elementType = "EAN8";
			}
			else if (text != null && text.Contains("UPC-A"))
			{
				elementType = "UPCA";
			}
			else if (text != null && text.Contains("UPC-E"))
			{
				elementType = "UPCE";
			}
			else if (text != null && text.Contains("Code 128"))
			{
				elementType = "Code128";
			}
			else if (text != null && text.Contains("Code 39"))
			{
				elementType = "Code39";
			}
			else if (text != null && text.Contains("QR Code"))
			{
				elementType = "QRCode";
			}
			else if (text != null && text.Contains("Aztec"))
			{
				elementType = "Aztec";
			}
			else if (text != null && text.Contains("DataMatrix"))
			{
				elementType = "DataMatrix";
			}
			else if (text != null && text.Contains("GS1 DataBar"))
			{
				elementType = "GS1DataBar";
			}
			else if (text != null && text.Contains("PDF417"))
			{
				elementType = "PDF417";
			}
			if (elementType != null)
			{
				item.PreviewMouseDown += delegate
				{
					StartDrag(elementType);
				};
				item.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e)
				{
					double num = canvas.Width / 2.0;
					double num2 = canvas.Height / 2.0;
					CreateElementFromDrop(elementType, new System.Windows.Point(num, num2));
					e.Handled = true;
					File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Button double-click created {elementType} at center ({num}, {num2}) via drop logic\n");
				};
			}
		}
	}

	private List<T> FindElementsByType<T>(DependencyObject parent) where T : DependencyObject
	{
		List<T> list = new List<T>();
		if (parent == null)
		{
			return list;
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T item)
			{
				list.Add(item);
			}
			list.AddRange(FindElementsByType<T>(child));
		}
		return list;
	}

	private void StartDrag(string elementType)
	{
		try
		{
			isDragInProgress = true;
			DragDrop.DoDragDrop(this, elementType, DragDropEffects.Copy);
		}
		finally
		{
			Task.Delay(100).ContinueWith((Task _) => isDragInProgress = false);
		}
	}

	private void LabelCanvas_Drop(object sender, DragEventArgs e)
	{
		try
		{
			System.Windows.Point position = e.GetPosition(LabelCanvas);
			System.Windows.Point point = SuggestOptimalPosition(position);
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (array.Length != 0)
				{
					string text = array[0];
					switch (System.IO.Path.GetExtension(text).ToLower())
					{
					case ".png":
					case ".jpg":
					case ".jpeg":
					case ".bmp":
					case ".gif":
						try
						{
							System.Windows.Controls.Image image = CreateImageFromFile(text);
							image.MouseLeftButtonDown += Element_MouseLeftButtonDown;
							AddElementCommand command = new AddElementCommand(LabelCanvas, image, point.X, point.Y, "Add image");
							commandHistory.ExecuteCommand(command);
							SetDirty();
							if (point != position)
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
							MessageBox.Show("Failed to load image: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
							return;
						}
					}
				}
			}
			if (!(e.Data.GetData(typeof(string)) is string text2))
			{
				return;
			}
			TimeSpan timeSpan = DateTime.Now - lastElementCreationTime;
			double num = Math.Sqrt(Math.Pow(point.X - lastElementPosition.X, 2.0) + Math.Pow(point.Y - lastElementPosition.Y, 2.0));
			if (timeSpan.TotalMilliseconds < 200.0 && num < 10.0 && text2 == lastElementType)
			{
				File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Duplicate element creation prevented - {text2} at {point}\n");
				return;
			}
			lastElementCreationTime = DateTime.Now;
			lastElementType = text2;
			lastElementPosition = point;
			UIElement uIElement = null;
			switch (text2)
			{
			case "Text":
			{
				TextBlock child = new TextBlock
				{
					Text = "Text",
					FontSize = 16.0,
					Foreground = System.Windows.Media.Brushes.Black
				};
				uIElement = new Border
				{
					Child = child,
					BorderBrush = System.Windows.Media.Brushes.LightGray,
					BorderThickness = new Thickness(1.0),
					Background = System.Windows.Media.Brushes.Transparent,
					Width = 100.0,
					Height = 30.0
				};
				break;
			}
			case "Image":
			{
				OpenFileDialog openFileDialog = new OpenFileDialog
				{
					Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
					Title = "Select Image File"
				};
				if (openFileDialog.ShowDialog() == true)
				{
					try
					{
						uIElement = CreateImageFromFile(openFileDialog.FileName);
					}
					catch (Exception ex2)
					{
						MessageBox.Show("Failed to load image: " + ex2.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
						uIElement = new System.Windows.Shapes.Rectangle
						{
							Width = 60.0,
							Height = 60.0,
							Fill = System.Windows.Media.Brushes.LightGray,
							Stroke = System.Windows.Media.Brushes.DarkGray,
							StrokeThickness = 2.0,
							Tag = "No Image Selected"
						};
					}
				}
				else
				{
					uIElement = new System.Windows.Shapes.Rectangle
					{
						Width = 60.0,
						Height = 60.0,
						Fill = System.Windows.Media.Brushes.LightGray,
						Stroke = System.Windows.Media.Brushes.DarkGray,
						StrokeThickness = 2.0,
						Tag = "No Image Selected"
					};
				}
				break;
			}
			case "LinkedImage":
				uIElement = new Border
				{
					Width = 100.0,
					Height = 75.0,
					BorderBrush = System.Windows.Media.Brushes.Blue,
					BorderThickness = new Thickness(2.0),
					Background = System.Windows.Media.Brushes.LightBlue,
					Child = new TextBlock
					{
						Text = "LinkedImage\n[URL]",
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						TextAlignment = TextAlignment.Center,
						FontSize = 10.0,
						Foreground = System.Windows.Media.Brushes.DarkBlue
					},
					Tag = "LinkedImage:"
				};
				break;
			case "Line":
			{
				Canvas obj = new Canvas
				{
					Width = 100.0,
					Height = 30.0,
					Background = System.Windows.Media.Brushes.Transparent
				};
				Line element = new Line
				{
					X1 = 0.0,
					Y1 = 15.0,
					X2 = 100.0,
					Y2 = 15.0,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 1.0
				};
				obj.Children.Add(element);
				uIElement = obj;
				break;
			}
			case "Rectangle":
				uIElement = new System.Windows.Shapes.Rectangle
				{
					Width = 100.0,
					Height = 60.0,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 1.0,
					Fill = System.Windows.Media.Brushes.Transparent
				};
				break;
			case "Ellipse":
				uIElement = new Ellipse
				{
					Width = 100.0,
					Height = 75.0,
					Fill = System.Windows.Media.Brushes.Transparent,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 2.0
				};
				break;
			case "EAN13":
				uIElement = CreateBarcodeImage("EAN13", "4000000000004");
				break;
			case "EAN8":
				uIElement = CreateBarcodeImage("EAN8", "40000007", 80.0);
				break;
			case "UPCA":
				uIElement = CreateBarcodeImage("UPCA", "012345678905");
				break;
			case "UPCE":
				uIElement = CreateBarcodeImage("UPCE", "01234565", 80.0);
				break;
			case "Code128":
				uIElement = CreateBarcodeImage("Code128", "HELLO123", 140.0);
				break;
			case "Code39":
				uIElement = CreateBarcodeImage("Code39", "HELLO39", 140.0);
				break;
			case "QRCode":
				uIElement = CreateBarcodeImage("QRCode", "Sample QR Data", 80.0, 80.0);
				break;
			case "Aztec":
				uIElement = CreateBarcodeImage("Aztec", "Sample Aztec Data", 80.0, 80.0);
				break;
			case "DataMatrix":
				uIElement = CreateBarcodeImage("DataMatrix", "Sample Data Matrix", 80.0, 80.0);
				break;
			case "GS1DataBar":
				uIElement = CreateBarcodeImage("GS1DataBar", "01234567890123");
				break;
			case "PDF417":
				uIElement = CreateBarcodeImage("PDF417", "Sample PDF417 Data", 140.0, 80.0);
				break;
			case "Barcode13":
				uIElement = CreateBarcodeImage("EAN13", "4000000000004");
				break;
			case "Barcode8":
				uIElement = CreateBarcodeImage("EAN8", "40000007", 80.0);
				break;
			}
			if (uIElement != null)
			{
				uIElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
				AddElementCommand command2 = new AddElementCommand(LabelCanvas, uIElement, point.X, point.Y, "Add " + text2);
				commandHistory.ExecuteCommand(command2);
				SetDirty();
				if (point != position)
				{
					ShowPositioningTooltip(uIElement, "Element positioned within printable area");
				}
			}
		}
		catch (Exception ex3)
		{
			MessageBox.Show("Drop operation failed: " + ex3.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void CreateElementFromDrop(string elementType, System.Windows.Point position)
	{
		try
		{
			System.Windows.Point point = SuggestOptimalPosition(position);
			TimeSpan timeSpan = DateTime.Now - lastElementCreationTime;
			double num = Math.Sqrt(Math.Pow(point.X - lastElementPosition.X, 2.0) + Math.Pow(point.Y - lastElementPosition.Y, 2.0));
			if (timeSpan.TotalMilliseconds < 200.0 && num < 10.0 && elementType == lastElementType)
			{
				File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Duplicate element creation prevented - {elementType} at {point}\n");
				return;
			}
			lastElementCreationTime = DateTime.Now;
			lastElementType = elementType;
			lastElementPosition = point;
			UIElement uIElement = null;
			switch (elementType)
			{
			case "Text":
			{
				TextBlock child = new TextBlock
				{
					Text = "Text",
					FontSize = 16.0,
					Foreground = System.Windows.Media.Brushes.Black
				};
				uIElement = new Border
				{
					Child = child,
					BorderBrush = System.Windows.Media.Brushes.LightGray,
					BorderThickness = new Thickness(1.0),
					Background = System.Windows.Media.Brushes.Transparent,
					Width = 100.0,
					Height = 30.0
				};
				break;
			}
			case "Image":
			{
				OpenFileDialog openFileDialog = new OpenFileDialog
				{
					Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
					Title = "Select Image File"
				};
				if (openFileDialog.ShowDialog() == true)
				{
					try
					{
						uIElement = CreateImageFromFile(openFileDialog.FileName);
					}
					catch (Exception ex)
					{
						MessageBox.Show("Failed to load image: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
						uIElement = new System.Windows.Shapes.Rectangle
						{
							Width = 60.0,
							Height = 60.0,
							Fill = System.Windows.Media.Brushes.LightGray,
							Stroke = System.Windows.Media.Brushes.DarkGray,
							StrokeThickness = 2.0,
							Tag = "No Image Selected"
						};
					}
				}
				else
				{
					uIElement = new System.Windows.Shapes.Rectangle
					{
						Width = 60.0,
						Height = 60.0,
						Fill = System.Windows.Media.Brushes.LightGray,
						Stroke = System.Windows.Media.Brushes.DarkGray,
						StrokeThickness = 2.0,
						Tag = "No Image Selected"
					};
				}
				break;
			}
			case "LinkedImage":
				uIElement = new Border
				{
					Width = 100.0,
					Height = 75.0,
					BorderBrush = System.Windows.Media.Brushes.Blue,
					BorderThickness = new Thickness(2.0),
					Background = System.Windows.Media.Brushes.LightBlue,
					Child = new TextBlock
					{
						Text = "LinkedImage\n[URL]",
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						TextAlignment = TextAlignment.Center,
						FontSize = 10.0,
						Foreground = System.Windows.Media.Brushes.DarkBlue
					},
					Tag = "LinkedImage:"
				};
				break;
			case "Line":
			{
				Canvas obj = new Canvas
				{
					Width = 100.0,
					Height = 30.0,
					Background = System.Windows.Media.Brushes.Transparent
				};
				Line element = new Line
				{
					X1 = 0.0,
					Y1 = 15.0,
					X2 = 100.0,
					Y2 = 15.0,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 1.0
				};
				obj.Children.Add(element);
				uIElement = obj;
				break;
			}
			case "Rectangle":
				uIElement = new System.Windows.Shapes.Rectangle
				{
					Width = 100.0,
					Height = 60.0,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 1.0,
					Fill = System.Windows.Media.Brushes.Transparent
				};
				break;
			case "EAN13":
				uIElement = CreateBarcodeImage("EAN13", "4000000000004");
				break;
			case "EAN8":
				uIElement = CreateBarcodeImage("EAN8", "40000007", 80.0);
				break;
			case "UPCA":
				uIElement = CreateBarcodeImage("UPCA", "012345678905");
				break;
			case "UPCE":
				uIElement = CreateBarcodeImage("UPCE", "01234565", 80.0);
				break;
			case "Code128":
				uIElement = CreateBarcodeImage("Code128", "HELLO123", 140.0);
				break;
			case "Code39":
				uIElement = CreateBarcodeImage("Code39", "HELLO39", 140.0);
				break;
			case "QRCode":
				uIElement = CreateBarcodeImage("QRCode", "Sample QR Data", 80.0, 80.0);
				break;
			case "Aztec":
				uIElement = CreateBarcodeImage("Aztec", "Sample Aztec Data", 80.0, 80.0);
				break;
			case "DataMatrix":
				uIElement = CreateBarcodeImage("DataMatrix", "Sample Data Matrix", 80.0, 80.0);
				break;
			case "GS1DataBar":
				uIElement = CreateBarcodeImage("GS1DataBar", "01234567890123");
				break;
			case "PDF417":
				uIElement = CreateBarcodeImage("PDF417", "Sample PDF417 Data");
				break;
			case "Ellipse":
				uIElement = new Ellipse
				{
					Width = 100.0,
					Height = 75.0,
					Fill = System.Windows.Media.Brushes.Transparent,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 2.0
				};
				break;
			default:
				uIElement = new System.Windows.Shapes.Rectangle
				{
					Width = 100.0,
					Height = 60.0,
					Fill = System.Windows.Media.Brushes.LightGray,
					Stroke = System.Windows.Media.Brushes.DarkGray,
					StrokeThickness = 2.0,
					Tag = "Unknown: " + elementType
				};
				break;
			}
			if (uIElement != null)
			{
				uIElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
				AddElementCommand command = new AddElementCommand(LabelCanvas, uIElement, point.X, point.Y, "Add " + elementType);
				commandHistory.ExecuteCommand(command);
				SetDirty();
				if (point != position)
				{
					ShowPositioningTooltip(uIElement, "Element positioned within printable area");
				}
			}
		}
		catch (Exception ex2)
		{
			MessageBox.Show("Element creation failed: " + ex2.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private System.Windows.Point SuggestOptimalPosition(System.Windows.Point originalPosition)
	{
		double num = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginLeft);
		double num2 = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginTop);
		double num3 = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginRight);
		double num4 = MeasurementService.MmToScreenPixels(CurrentTemplate.MarginBottom);
		double num5 = originalPosition.X;
		double num6 = originalPosition.Y;
		if (num5 < num)
		{
			num5 = num + 5.0;
		}
		if (num6 < num2)
		{
			num6 = num2 + 5.0;
		}
		if (num5 > LabelCanvas.Width - num3 - 50.0)
		{
			num5 = LabelCanvas.Width - num3 - 55.0;
		}
		if (num6 > LabelCanvas.Height - num4 - 20.0)
		{
			num6 = LabelCanvas.Height - num4 - 25.0;
		}
		return new System.Windows.Point(num5, num6);
	}

	private void ShowPositioningTooltip(UIElement element, string message)
	{
		FrameworkElement frameworkElement = element as FrameworkElement;
		if (frameworkElement != null)
		{
			ToolTip tooltip = new ToolTip
			{
				Content = message,
				IsOpen = true
			};
			frameworkElement.ToolTip = tooltip;
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(3L)
			};
			timer.Tick += delegate
			{
				tooltip.IsOpen = false;
				frameworkElement.ToolTip = null;
				timer.Stop();
			};
			timer.Start();
		}
	}

	private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		UIElement uIElement = sender as UIElement;
		LabelCanvas.Focus();
		if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
		{
			if (selectedElements.Contains(uIElement))
			{
				selectedElements.Remove(uIElement);
				HighlightElement(uIElement, highlight: false);
			}
			else
			{
				selectedElements.Add(uIElement);
				HighlightElement(uIElement, highlight: true);
				if (selectedElement != null)
				{
					RemoveResizingHandles();
					selectedElement = null;
				}
			}
		}
		else if (!selectedElements.Contains(uIElement) || selectedElements.Count <= 1)
		{
			ClearSelection();
			selectedElement = uIElement;
			ShowResizingHandles(selectedElement);
			ShowProperties(selectedElement);
		}
		dragStart = e.GetPosition(LabelCanvas);
		isDragging = true;
		LabelCanvas.MouseMove += Canvas_MouseMove;
		LabelCanvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
		e.Handled = true;
	}

	private void Canvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (!isDragging)
		{
			return;
		}
		System.Windows.Point position = e.GetPosition(LabelCanvas);
		double num = position.X - dragStart.X;
		double num2 = position.Y - dragStart.Y;
		if (selectedElement != null)
		{
			double length = Canvas.GetLeft(selectedElement) + num;
			double length2 = Canvas.GetTop(selectedElement) + num2;
			Canvas.SetLeft(selectedElement, length);
			Canvas.SetTop(selectedElement, length2);
		}
		foreach (UIElement selectedElement in selectedElements)
		{
			double length3 = Canvas.GetLeft(selectedElement) + num;
			double length4 = Canvas.GetTop(selectedElement) + num2;
			Canvas.SetLeft(selectedElement, length3);
			Canvas.SetTop(selectedElement, length4);
		}
		dragStart = position;
	}

	private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (isDragging)
		{
			SetDirty();
		}
		isDragging = false;
		LabelCanvas.MouseMove -= Canvas_MouseMove;
		LabelCanvas.MouseLeftButtonUp -= Canvas_MouseLeftButtonUp;
		if (isSelecting)
		{
			EndSelection();
			e.Handled = true;
		}
	}

	private void LabelCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		LabelCanvas.Focus();
		if (e.Source == LabelCanvas)
		{
			ClearSelection();
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
		UIElement obj = e.Source as UIElement;
		bool flag = true;
		if (obj is FrameworkElement frameworkElement && (frameworkElement is TextBlock || frameworkElement is System.Windows.Controls.Image))
		{
			for (DependencyObject parent = frameworkElement.Parent; parent != null;)
			{
				if (parent == LabelCanvas)
				{
					flag = false;
					break;
				}
				if (parent is not FrameworkElement frameworkElement2)
				{
					break;
				}
				parent = frameworkElement2.Parent;
			}
		}
		if (flag)
		{
			ClearSelection();
			System.Windows.Point position = e.GetPosition(CanvasContainer);
			selectionStart = position;
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
			System.Windows.Point position = e.GetPosition(CanvasContainer);
			System.Windows.Point currentPosition = new System.Windows.Point(position.X - Canvas.GetLeft(LabelCanvas), position.Y - Canvas.GetTop(LabelCanvas));
			UpdateSelectionRectangle(currentPosition);
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
		if (e.Source == CanvasScrollViewer)
		{
			ClearSelection();
			e.GetPosition(CanvasScrollViewer);
			System.Windows.Point position = e.GetPosition(CanvasContainer);
			selectionStart = new System.Windows.Point(position.X - Canvas.GetLeft(LabelCanvas), position.Y - Canvas.GetTop(LabelCanvas));
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
			System.Windows.Point position = e.GetPosition(CanvasContainer);
			System.Windows.Point currentPosition = new System.Windows.Point(position.X - Canvas.GetLeft(LabelCanvas), position.Y - Canvas.GetTop(LabelCanvas));
			UpdateSelectionRectangle(currentPosition);
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

	private void UpdateSelectionRectangle(System.Windows.Point currentPosition)
	{
		if (selectionRectangle == null)
		{
			selectionRectangle = new System.Windows.Shapes.Rectangle
			{
				Stroke = System.Windows.Media.Brushes.Blue,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 5.0, 3.0 },
				Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 0, byte.MaxValue))
			};
			LabelCanvas.Children.Add(selectionRectangle);
		}
		double length = Math.Min(selectionStart.X, currentPosition.X);
		double length2 = Math.Min(selectionStart.Y, currentPosition.Y);
		double width = Math.Abs(currentPosition.X - selectionStart.X);
		double height = Math.Abs(currentPosition.Y - selectionStart.Y);
		Canvas.SetLeft(selectionRectangle, length);
		Canvas.SetTop(selectionRectangle, length2);
		selectionRectangle.Width = width;
		selectionRectangle.Height = height;
	}

	private void EndSelection()
	{
		isSelecting = false;
		if (selectionRectangle == null)
		{
			return;
		}
		double left = Canvas.GetLeft(selectionRectangle);
		double top = Canvas.GetTop(selectionRectangle);
		double num = left + selectionRectangle.Width;
		double num2 = top + selectionRectangle.Height;
		foreach (UIElement item in LabelCanvas.Children.OfType<UIElement>())
		{
			if (item != selectionRectangle && item is FrameworkElement frameworkElement)
			{
				double left2 = Canvas.GetLeft(frameworkElement);
				double top2 = Canvas.GetTop(frameworkElement);
				double num3 = left2 + frameworkElement.ActualWidth;
				double num4 = top2 + frameworkElement.ActualHeight;
				if (left2 >= left && top2 >= top && num3 <= num && num4 <= num2)
				{
					selectedElements.Add(item);
					HighlightElement(item, highlight: true);
				}
			}
		}
		LabelCanvas.Children.Remove(selectionRectangle);
		selectionRectangle = null;
	}

	private void ClearSelection()
	{
		foreach (UIElement selectedElement in selectedElements)
		{
			HighlightElement(selectedElement, highlight: false);
		}
		selectedElements.Clear();
		if (this.selectedElement != null)
		{
			HighlightElement(this.selectedElement, highlight: false);
			this.selectedElement = null;
			RemoveResizingHandles();
		}
	}

	private void HighlightElement(UIElement element, bool highlight)
	{
		if (element is FrameworkElement frameworkElement)
		{
			if (highlight)
			{
				frameworkElement.Effect = new DropShadowEffect
				{
					Color = Colors.Blue,
					BlurRadius = 5.0,
					ShadowDepth = 0.0,
					Opacity = 0.7
				};
			}
			else
			{
				frameworkElement.Effect = null;
			}
		}
	}

	private void MainWindow_KeyDown(object sender, KeyEventArgs e)
	{
		string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainWindow_KeyDown: Key={e.Key}, Ctrl={Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)}, OriginalSource={e.OriginalSource?.GetType().Name}";
		File.AppendAllText("keyboard-debug.log", text + Environment.NewLine);
		if (e.OriginalSource is TextBox { IsFocused: not false } textBox)
		{
			string text2 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] TextBox has focus: {textBox.Name}";
			File.AppendAllText("keyboard-debug.log", text2 + Environment.NewLine);
			DependencyObject dependencyObject = textBox;
			bool flag = false;
			while (dependencyObject != null)
			{
				if (dependencyObject is StackPanel { Name: "ElementPropertiesPanel" })
				{
					flag = true;
					break;
				}
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			string text3 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Is property panel TextBox: {flag}";
			File.AppendAllText("keyboard-debug.log", text3 + Environment.NewLine);
			if (flag)
			{
				File.AppendAllText("keyboard-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Letting TextBox handle the key{Environment.NewLine}");
				return;
			}
		}
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
		case Key.A:
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				SelectAllElements();
				e.Handled = true;
			}
			break;
		case Key.Z:
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				Undo();
				e.Handled = true;
			}
			break;
		case Key.Y:
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				Redo();
				e.Handled = true;
			}
			break;
		case Key.C:
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				File.AppendAllText("keyboard-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Ctrl+C detected - calling CopySelectedElements{Environment.NewLine}");
				CopySelectedElements();
				e.Handled = true;
			}
			break;
		case Key.V:
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				File.AppendAllText("keyboard-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Ctrl+V detected - calling PasteElements{Environment.NewLine}");
				PasteElements();
				e.Handled = true;
			}
			break;
		case Key.X:
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				CutSelectedElements();
				e.Handled = true;
			}
			break;
		case Key.Left:
			MoveSelectedElements(-1.0, 0.0);
			e.Handled = true;
			break;
		case Key.Right:
			MoveSelectedElements(1.0, 0.0);
			e.Handled = true;
			break;
		case Key.Up:
			MoveSelectedElements(0.0, -1.0);
			e.Handled = true;
			break;
		case Key.Down:
			MoveSelectedElements(0.0, 1.0);
			e.Handled = true;
			break;
		}
	}

	private void DeleteSelectedElements()
	{
		List<UIElement> list = new List<UIElement>();
		if (selectedElement != null)
		{
			list.Add(selectedElement);
			selectedElement = null;
			RemoveResizingHandles();
		}
		list.AddRange(selectedElements);
		if (list.Count > 0)
		{
			DeleteElementsCommand command = new DeleteElementsCommand(LabelCanvas, list, (list.Count == 1) ? "Delete element" : $"Delete {list.Count} elements");
			commandHistory.ExecuteCommand(command);
			SetDirty();
			selectedElements.Clear();
		}
	}

	private void SelectAllElements()
	{
		ClearSelection();
		foreach (UIElement item in LabelCanvas.Children.OfType<UIElement>())
		{
			if (item != selectionRectangle)
			{
				selectedElements.Add(item);
				HighlightElement(item, highlight: true);
			}
		}
	}

	private void MoveSelectedElements(double deltaX, double deltaY)
	{
		double num = ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) ? 10.0 : 1.0);
		deltaX *= num;
		deltaY *= num;
		List<UIElement> list = new List<UIElement>();
		if (selectedElement != null)
		{
			list.Add(selectedElement);
		}
		list.AddRange(selectedElements);
		if (list.Count > 0)
		{
			MoveElementsCommand command = new MoveElementsCommand(list, deltaX, deltaY, "Move " + ((list.Count == 1) ? "element" : (list.Count + " elements")));
			commandHistory.ExecuteCommand(command);
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
		string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CopySelectedElements: selectedElement={selectedElement}, selectedElements.Count={selectedElements.Count}";
		File.AppendAllText("keyboard-debug.log", text + Environment.NewLine);
		if (selectedElements.Count == 0 && selectedElement == null)
		{
			File.AppendAllText("keyboard-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Nothing selected - returning{Environment.NewLine}");
			return;
		}
		clipboardElements.Clear();
		List<UIElement> list = new List<UIElement>();
		if (selectedElement != null)
		{
			list.Add(selectedElement);
		}
		list.AddRange(selectedElements);
		File.AppendAllText("keyboard-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Elements to copy: {list.Count}{Environment.NewLine}");
		foreach (UIElement item in list)
		{
			LabelElement labelElement = ConvertUIElementToLabelElement(item);
			if (labelElement != null)
			{
				clipboardElements.Add(labelElement);
			}
		}
		string text2 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Copied {clipboardElements.Count} elements to clipboard";
		File.AppendAllText("keyboard-debug.log", text2 + Environment.NewLine);
	}

	private void CutSelectedElements()
	{
		CopySelectedElements();
		DeleteSelectedElements();
	}

	private void PasteElements()
	{
		if (clipboardElements.Count == 0)
		{
			return;
		}
		List<UIElement> list = new List<UIElement>();
		List<AddElementCommand> list2 = new List<AddElementCommand>();
		double num = 20.0;
		double num2 = 20.0;
		foreach (LabelElement clipboardElement in clipboardElements)
		{
			LabelElement labelElement = new LabelElement
			{
				Type = clipboardElement.Type,
				X = clipboardElement.X + num,
				Y = clipboardElement.Y + num2,
				Width = clipboardElement.Width,
				Height = clipboardElement.Height,
				Content = clipboardElement.Content,
				BarcodeType = clipboardElement.BarcodeType,
				FontFamily = clipboardElement.FontFamily,
				FontSize = clipboardElement.FontSize,
				FontWeight = clipboardElement.FontWeight,
				FontStyle = clipboardElement.FontStyle,
				Foreground = clipboardElement.Foreground,
				TextDecorations = clipboardElement.TextDecorations,
				ImageData = clipboardElement.ImageData,
				Rotation = clipboardElement.Rotation
			};
			UIElement uIElement = CreateElementFromTemplate(labelElement);
			if (uIElement != null)
			{
				uIElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
				list.Add(uIElement);
				AddElementCommand item = new AddElementCommand(LabelCanvas, uIElement, labelElement.X, labelElement.Y, "Paste element");
				list2.Add(item);
			}
		}
		if (list2.Count <= 0)
		{
			return;
		}
		BatchCommand command = new BatchCommand(list2.Cast<TaggJack.Commands.ICommand>().ToList(), $"Paste {list2.Count} element{((list2.Count == 1) ? "" : "s")}");
		commandHistory.ExecuteCommand(command);
		ClearSelection();
		foreach (UIElement item2 in list)
		{
			selectedElements.Add(item2);
			HighlightElement(item2, highlight: true);
		}
		SetDirty();
	}

	private LabelElement? ConvertUIElementToLabelElement(UIElement element)
	{
		LabelElement labelElement = new LabelElement
		{
			X = Canvas.GetLeft(element),
			Y = Canvas.GetTop(element),
			Width = ((element is FrameworkElement frameworkElement) ? frameworkElement.Width : 100.0),
			Height = ((element is FrameworkElement frameworkElement2) ? frameworkElement2.Height : 30.0),
			Rotation = (elementRotations.ContainsKey(element) ? elementRotations[element] : ((element.RenderTransform is RotateTransform rotateTransform) ? rotateTransform.Angle : 0.0))
		};
		if (element is TextBlock textBlock)
		{
			if (textBlock.Text == "[EAN-13]")
			{
				labelElement.Type = LabelElementType.Barcode13;
			}
			else if (textBlock.Text == "[EAN-8]")
			{
				labelElement.Type = LabelElementType.Barcode8;
			}
			else if (textBlock.Text == "[QR Code]")
			{
				labelElement.Type = LabelElementType.QRCode;
			}
			else
			{
				labelElement.Type = LabelElementType.Text;
			}
			labelElement.Content = textBlock.Text;
			labelElement.FontSize = textBlock.FontSize;
			labelElement.FontFamily = textBlock.FontFamily.ToString();
			labelElement.FontWeight = textBlock.FontWeight.ToString();
			labelElement.FontStyle = textBlock.FontStyle.ToString();
			labelElement.Foreground = textBlock.Foreground.ToString();
			labelElement.TextDecorations = GetTextDecorationsString(textBlock.TextDecorations);
		}
		else if (element is System.Windows.Shapes.Rectangle rectangle)
		{
			labelElement.Type = LabelElementType.Image;
			string text = rectangle.Tag?.ToString() ?? "";
			if (!string.IsNullOrEmpty(text) && text != "Missing Image Data" && text != "No Image Data")
			{
				if (File.Exists(text))
				{
					labelElement.ImageData = ConvertImageToBase64(text);
					labelElement.Content = "";
				}
				else
				{
					labelElement.Content = text;
				}
			}
		}
		else if (element is System.Windows.Controls.Image { Tag: var tag } image)
		{
			if (!(tag?.ToString() == "QRCode"))
			{
				object tag2 = image.Tag;
				if (tag2 == null || tag2.ToString()?.StartsWith("QRCodeData:") != true)
				{
					object tag3 = image.Tag;
					if (tag3 != null && tag3.ToString()?.StartsWith("BarcodeData:") == true)
					{
						string text2 = image.Tag?.ToString();
						if (text2 != null)
						{
							string[] array = text2.Split(':');
							if (array.Length >= 3)
							{
								string barcodeType = array[1];
								string content = string.Join(":", array.Skip(2));
								labelElement.Type = GetLabelElementTypeFromBarcodeType(barcodeType);
								labelElement.Content = content;
								labelElement.BarcodeType = barcodeType;
							}
							else
							{
								labelElement.Type = LabelElementType.Image;
								labelElement.Content = "Invalid barcode data";
							}
						}
					}
					else
					{
						labelElement.Type = LabelElementType.Image;
						if (image.Source is BitmapSource bitmapSource)
						{
							labelElement.ImageData = ConvertBitmapSourceToBase64(bitmapSource);
						}
					}
					goto IL_0591;
				}
			}
			labelElement.Type = LabelElementType.QRCode;
			string text3 = image.Tag?.ToString();
			if (text3 != null && text3.StartsWith("QRCodeData:"))
			{
				labelElement.Content = text3.Substring("QRCodeData:".Length);
			}
			else
			{
				labelElement.Content = "Sample QR Data";
			}
		}
		else
		{
			if (!(element is Border border))
			{
				return null;
			}
			if (!(border.Child is TextBlock textBlock2))
			{
				return null;
			}
			if (textBlock2.Text == "[EAN-13]")
			{
				labelElement.Type = LabelElementType.Barcode13;
			}
			else if (textBlock2.Text == "[EAN-8]")
			{
				labelElement.Type = LabelElementType.Barcode8;
			}
			else if (textBlock2.Text == "[QR Code]")
			{
				labelElement.Type = LabelElementType.QRCode;
			}
			else
			{
				labelElement.Type = LabelElementType.Text;
			}
			labelElement.Content = textBlock2.Text;
			labelElement.FontSize = textBlock2.FontSize;
			labelElement.FontFamily = textBlock2.FontFamily.ToString();
			labelElement.FontWeight = textBlock2.FontWeight.ToString();
			labelElement.FontStyle = textBlock2.FontStyle.ToString();
			labelElement.Foreground = textBlock2.Foreground.ToString();
			labelElement.TextDecorations = GetTextDecorationsString(textBlock2.TextDecorations);
			LabelElement labelElement2 = labelElement;
			labelElement2.TextAlignment = textBlock2.TextAlignment switch
			{
				TextAlignment.Center => "Center", 
				TextAlignment.Right => "Right", 
				TextAlignment.Left => "Left", 
				_ => "Left", 
			};
			labelElement2 = labelElement;
			labelElement2.VerticalAlignment = textBlock2.VerticalAlignment switch
			{
				VerticalAlignment.Center => "Center", 
				VerticalAlignment.Bottom => "Bottom", 
				VerticalAlignment.Top => "Top", 
				_ => "Top", 
			};
		}
		goto IL_0591;
		IL_0591:
		return labelElement;
	}

	private void ShowResizingHandles(UIElement element)
	{
		RemoveResizingHandles();
		FrameworkElement fe = element as FrameworkElement;
		if (fe == null)
		{
			return;
		}
		resizeThumb = new Thumb
		{
			Width = 12.0,
			Height = 12.0,
			Background = System.Windows.Media.Brushes.Blue,
			Cursor = Cursors.SizeNWSE
		};
		Canvas.SetLeft(resizeThumb, Canvas.GetLeft(fe) + fe.ActualWidth - 6.0);
		Canvas.SetTop(resizeThumb, Canvas.GetTop(fe) + fe.ActualHeight - 6.0);
		resizeThumb.DragDelta += delegate(object s, DragDeltaEventArgs e)
		{
			fe.Width = Math.Max(20.0, fe.ActualWidth + e.HorizontalChange);
			fe.Height = Math.Max(20.0, fe.ActualHeight + e.VerticalChange);
			Canvas.SetLeft(resizeThumb, Canvas.GetLeft(fe) + fe.Width - 6.0);
			Canvas.SetTop(resizeThumb, Canvas.GetTop(fe) + fe.Height - 6.0);
			if (fe is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Line line)
			{
				line.X2 = fe.Width;
				line.Y1 = fe.Height / 2.0;
				line.Y2 = fe.Height / 2.0;
			}
		};
		LabelCanvas.Children.Add(resizeThumb);
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
			Border linkedBorder = element as Border;
			if (linkedBorder != null)
			{
				object tag = linkedBorder.Tag;
				if (tag != null && tag.ToString()?.StartsWith("LinkedImage:") == true)
				{
					string text = linkedBorder.Tag.ToString();
					Label label = new Label
					{
						Content = "Linked Image Properties"
					};
					label.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label);
					Label label2 = new Label
					{
						Content = "URL:"
					};
					label2.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label2);
					string currentUrl = text.Substring("LinkedImage:".Length);
					TextBox urlBox = new TextBox
					{
						Text = currentUrl
					};
					urlBox.Style = (Style)FindResource("ModernTextBoxStyle");
					urlBox.LostFocus += delegate
					{
						if (!string.IsNullOrWhiteSpace(urlBox.Text) && urlBox.Text != currentUrl)
						{
							LoadLinkedImageFromUrl(linkedBorder, urlBox.Text);
						}
					};
					urlBox.KeyDown += delegate(object s, KeyEventArgs e)
					{
						if (e.Key == Key.Return)
						{
							LoadLinkedImageFromUrl(linkedBorder, urlBox.Text);
						}
					};
					ElementPropertiesPanel.Children.Add(urlBox);
					AddLinkedImageSizeProperties(linkedBorder, linkedBorder.Width, linkedBorder.Height);
					goto IL_1d7a;
				}
			}
			System.Windows.Controls.Image linkedImage = element as System.Windows.Controls.Image;
			if (linkedImage != null)
			{
				object tag2 = linkedImage.Tag;
				if (tag2 != null && tag2.ToString()?.StartsWith("LinkedImage:") == true)
				{
					string text2 = linkedImage.Tag.ToString();
					Label label3 = new Label
					{
						Content = "Linked Image Properties"
					};
					label3.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label3);
					Label label4 = new Label
					{
						Content = "URL:"
					};
					label4.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label4);
					string currentUrl2 = text2.Substring("LinkedImage:".Length);
					TextBox urlBox2 = new TextBox
					{
						Text = currentUrl2
					};
					urlBox2.Style = (Style)FindResource("ModernTextBoxStyle");
					urlBox2.LostFocus += delegate
					{
						if (!string.IsNullOrWhiteSpace(urlBox2.Text) && urlBox2.Text != currentUrl2)
						{
							UpdateLinkedImageUrl(linkedImage, urlBox2.Text);
						}
					};
					urlBox2.KeyDown += delegate(object s, KeyEventArgs e)
					{
						if (e.Key == Key.Return)
						{
							UpdateLinkedImageUrl(linkedImage, urlBox2.Text);
						}
					};
					ElementPropertiesPanel.Children.Add(urlBox2);
					AddLinkedImageSizeProperties(linkedImage, linkedImage.Width, linkedImage.Height);
					goto IL_1d7a;
				}
			}
			if (element is Border { Child: var child } border)
			{
				TextBlock tb = child as TextBlock;
				if (tb != null)
				{
					string contents = $"[DEBUG] ShowProperties for TextBlock: Text='{tb.Text}', FontSize={tb.FontSize}\n";
					File.AppendAllText("debug.log", contents);
					Console.WriteLine($"[DEBUG] ShowProperties for TextBlock: Text='{tb.Text}', FontSize={tb.FontSize}");
					Label label5 = new Label
					{
						Content = "Text:"
					};
					label5.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label5);
					TextBox textBox = new TextBox
					{
						Text = tb.Text,
						AcceptsReturn = true,
						TextWrapping = TextWrapping.Wrap,
						MinHeight = 60.0,
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto
					};
					textBox.Style = (Style)FindResource("ModernTextBoxStyle");
					textBox.TextChanged += delegate
					{
						tb.Text = textBox.Text;
						SetDirty();
						string contents2 = "[DEBUG] Text changed to: '" + tb.Text + "'\n";
						File.AppendAllText("debug.log", contents2);
					};
					ElementPropertiesPanel.Children.Add(textBox);
					Label label6 = new Label
					{
						Content = "Font Family:"
					};
					label6.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label6);
					ComboBox fontFamilyBox = new ComboBox
					{
						ItemsSource = from f in Fonts.SystemFontFamilies
							select f.Source into f
							orderby f
							select f,
						SelectedItem = tb.FontFamily.Source
					};
					fontFamilyBox.Style = (Style)FindResource("ModernComboBoxStyle");
					fontFamilyBox.SelectionChanged += delegate
					{
						if (fontFamilyBox.SelectedItem != null)
						{
							tb.FontFamily = new System.Windows.Media.FontFamily(fontFamilyBox.SelectedItem.ToString());
							SetDirty();
						}
					};
					ElementPropertiesPanel.Children.Add(fontFamilyBox);
					Grid grid = new Grid();
					grid.ColumnDefinitions.Add(new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					});
					grid.ColumnDefinitions.Add(new ColumnDefinition
					{
						Width = new GridLength(1.0, GridUnitType.Star)
					});
					grid.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
					grid.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
					Label label7 = new Label
					{
						Content = "Font Size:"
					};
					label7.Style = (Style)FindResource("ModernLabelStyle");
					Grid.SetRow(label7, 0);
					Grid.SetColumn(label7, 0);
					grid.Children.Add(label7);
					Label label8 = new Label
					{
						Content = "Text Color:"
					};
					label8.Style = (Style)FindResource("ModernLabelStyle");
					Grid.SetRow(label8, 0);
					Grid.SetColumn(label8, 1);
					grid.Children.Add(label8);
					TextBox fontSizeBox = new TextBox
					{
						Text = tb.FontSize.ToString(),
						Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
					};
					fontSizeBox.Style = (Style)FindResource("ModernTextBoxStyle");
					fontSizeBox.TextChanged += delegate
					{
						if (double.TryParse(fontSizeBox.Text, out var result))
						{
							tb.FontSize = result;
							SetDirty();
						}
					};
					Grid.SetRow(fontSizeBox, 1);
					Grid.SetColumn(fontSizeBox, 0);
					grid.Children.Add(fontSizeBox);
					System.Windows.Media.Color initialColor = (tb.Foreground as SolidColorBrush)?.Color ?? Colors.Black;
					Button element2 = CreateColorPicker(initialColor, delegate(System.Windows.Media.Color newColor)
					{
						tb.Foreground = new SolidColorBrush(newColor);
						SetDirty();
					});
					Grid.SetRow(element2, 1);
					Grid.SetColumn(element2, 1);
					grid.Children.Add(element2);
					ElementPropertiesPanel.Children.Add(grid);
					AddTextFormattingControls(tb, border);
					AddDataFieldMappingSelector(element);
					goto IL_1d7a;
				}
			}
			TextBlock directTb = element as TextBlock;
			if (directTb != null)
			{
				Label label9 = new Label
				{
					Content = "Text:"
				};
				label9.Style = (Style)FindResource("ModernLabelStyle");
				ElementPropertiesPanel.Children.Add(label9);
				TextBox textBox2 = new TextBox
				{
					Text = directTb.Text,
					AcceptsReturn = true,
					TextWrapping = TextWrapping.Wrap,
					MinHeight = 60.0,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto
				};
				textBox2.Style = (Style)FindResource("ModernTextBoxStyle");
				textBox2.TextChanged += delegate
				{
					directTb.Text = textBox2.Text;
					SetDirty();
				};
				ElementPropertiesPanel.Children.Add(textBox2);
				Label label10 = new Label
				{
					Content = "Font Family:"
				};
				label10.Style = (Style)FindResource("ModernLabelStyle");
				ElementPropertiesPanel.Children.Add(label10);
				ComboBox fontFamilyBox2 = new ComboBox
				{
					ItemsSource = from f in Fonts.SystemFontFamilies
						select f.Source into f
						orderby f
						select f,
					SelectedItem = directTb.FontFamily.Source
				};
				fontFamilyBox2.Style = (Style)FindResource("ModernComboBoxStyle");
				fontFamilyBox2.SelectionChanged += delegate
				{
					if (fontFamilyBox2.SelectedItem != null)
					{
						directTb.FontFamily = new System.Windows.Media.FontFamily(fontFamilyBox2.SelectedItem.ToString());
						SetDirty();
					}
				};
				ElementPropertiesPanel.Children.Add(fontFamilyBox2);
				Grid grid2 = new Grid();
				grid2.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid2.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid2.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				grid2.RowDefinitions.Add(new RowDefinition
				{
					Height = GridLength.Auto
				});
				Label label11 = new Label
				{
					Content = "Font Size:"
				};
				label11.Style = (Style)FindResource("ModernLabelStyle");
				Grid.SetRow(label11, 0);
				Grid.SetColumn(label11, 0);
				grid2.Children.Add(label11);
				Label label12 = new Label
				{
					Content = "Text Color:"
				};
				label12.Style = (Style)FindResource("ModernLabelStyle");
				Grid.SetRow(label12, 0);
				Grid.SetColumn(label12, 1);
				grid2.Children.Add(label12);
				TextBox fontSizeBox2 = new TextBox
				{
					Text = directTb.FontSize.ToString(),
					Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
				};
				fontSizeBox2.Style = (Style)FindResource("ModernTextBoxStyle");
				fontSizeBox2.TextChanged += delegate
				{
					if (double.TryParse(fontSizeBox2.Text, out var result))
					{
						directTb.FontSize = result;
						SetDirty();
					}
				};
				Grid.SetRow(fontSizeBox2, 1);
				Grid.SetColumn(fontSizeBox2, 0);
				grid2.Children.Add(fontSizeBox2);
				TextBox colorBox = new TextBox
				{
					Text = ((directTb.Foreground as SolidColorBrush)?.Color.ToString() ?? "#000000"),
					Margin = new Thickness(4.0, 0.0, 0.0, 0.0)
				};
				colorBox.Style = (Style)FindResource("ModernTextBoxStyle");
				colorBox.TextChanged += delegate
				{
					try
					{
						directTb.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(colorBox.Text);
						SetDirty();
					}
					catch
					{
					}
				};
				Grid.SetRow(colorBox, 1);
				Grid.SetColumn(colorBox, 1);
				grid2.Children.Add(colorBox);
				ElementPropertiesPanel.Children.Add(grid2);
			}
			else
			{
				System.Windows.Shapes.Rectangle rect = element as System.Windows.Shapes.Rectangle;
				if (rect != null)
				{
					Label label13 = new Label
					{
						Content = "Rectangle Properties"
					};
					label13.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label13);
					Label label14 = new Label
					{
						Content = "Width:"
					};
					label14.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label14);
					TextBox widthBox = new TextBox
					{
						Text = rect.Width.ToString()
					};
					widthBox.Style = (Style)FindResource("ModernTextBoxStyle");
					widthBox.TextChanged += delegate
					{
						if (double.TryParse(widthBox.Text, out var result))
						{
							rect.Width = result;
							SetDirty();
						}
					};
					ElementPropertiesPanel.Children.Add(widthBox);
					Label label15 = new Label
					{
						Content = "Height:"
					};
					label15.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label15);
					TextBox heightBox = new TextBox
					{
						Text = rect.Height.ToString()
					};
					heightBox.Style = (Style)FindResource("ModernTextBoxStyle");
					heightBox.TextChanged += delegate
					{
						if (double.TryParse(heightBox.Text, out var result))
						{
							rect.Height = result;
							SetDirty();
						}
					};
					ElementPropertiesPanel.Children.Add(heightBox);
					Label label16 = new Label
					{
						Content = "Border Width:"
					};
					label16.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label16);
					TextBox borderWidthBox = new TextBox
					{
						Text = rect.StrokeThickness.ToString()
					};
					borderWidthBox.Style = (Style)FindResource("ModernTextBoxStyle");
					borderWidthBox.TextChanged += delegate
					{
						if (double.TryParse(borderWidthBox.Text, out var result))
						{
							rect.StrokeThickness = result;
							SetDirty();
						}
					};
					ElementPropertiesPanel.Children.Add(borderWidthBox);
					Label label17 = new Label
					{
						Content = "Border Color:"
					};
					label17.Style = (Style)FindResource("ModernLabelStyle");
					ElementPropertiesPanel.Children.Add(label17);
					System.Windows.Media.Color initialColor2 = (rect.Stroke as SolidColorBrush)?.Color ?? Colors.Black;
					Button element3 = CreateColorPicker(initialColor2, delegate(System.Windows.Media.Color newColor)
					{
						rect.Stroke = new SolidColorBrush(newColor);
						SetDirty();
					});
					ElementPropertiesPanel.Children.Add(element3);
				}
				else
				{
					Ellipse ellipse = element as Ellipse;
					if (ellipse != null)
					{
						Label label18 = new Label
						{
							Content = "Ellipse Properties"
						};
						label18.Style = (Style)FindResource("ModernLabelStyle");
						ElementPropertiesPanel.Children.Add(label18);
						Label label19 = new Label
						{
							Content = "Width:"
						};
						label19.Style = (Style)FindResource("ModernLabelStyle");
						ElementPropertiesPanel.Children.Add(label19);
						TextBox widthBox2 = new TextBox
						{
							Text = ellipse.Width.ToString()
						};
						widthBox2.Style = (Style)FindResource("ModernTextBoxStyle");
						widthBox2.TextChanged += delegate
						{
							if (double.TryParse(widthBox2.Text, out var result))
							{
								ellipse.Width = result;
								SetDirty();
							}
						};
						ElementPropertiesPanel.Children.Add(widthBox2);
						Label label20 = new Label
						{
							Content = "Height:"
						};
						label20.Style = (Style)FindResource("ModernLabelStyle");
						ElementPropertiesPanel.Children.Add(label20);
						TextBox heightBox2 = new TextBox
						{
							Text = ellipse.Height.ToString()
						};
						heightBox2.Style = (Style)FindResource("ModernTextBoxStyle");
						heightBox2.TextChanged += delegate
						{
							if (double.TryParse(heightBox2.Text, out var result))
							{
								ellipse.Height = result;
								SetDirty();
							}
						};
						ElementPropertiesPanel.Children.Add(heightBox2);
						Label label21 = new Label
						{
							Content = "Border Width:"
						};
						label21.Style = (Style)FindResource("ModernLabelStyle");
						ElementPropertiesPanel.Children.Add(label21);
						TextBox borderWidthBox2 = new TextBox
						{
							Text = ellipse.StrokeThickness.ToString()
						};
						borderWidthBox2.Style = (Style)FindResource("ModernTextBoxStyle");
						borderWidthBox2.TextChanged += delegate
						{
							if (double.TryParse(borderWidthBox2.Text, out var result))
							{
								ellipse.StrokeThickness = result;
								SetDirty();
							}
						};
						ElementPropertiesPanel.Children.Add(borderWidthBox2);
						Label label22 = new Label
						{
							Content = "Border Color:"
						};
						label22.Style = (Style)FindResource("ModernLabelStyle");
						ElementPropertiesPanel.Children.Add(label22);
						System.Windows.Media.Color initialColor3 = (ellipse.Stroke as SolidColorBrush)?.Color ?? Colors.Black;
						Button element4 = CreateColorPicker(initialColor3, delegate(System.Windows.Media.Color newColor)
						{
							ellipse.Stroke = new SolidColorBrush(newColor);
							SetDirty();
						});
						ElementPropertiesPanel.Children.Add(element4);
					}
					else
					{
						Canvas canvas = element as Canvas;
						if (canvas != null && canvas.Children.Count > 0)
						{
							UIElement uIElement = canvas.Children[0];
							Line line = uIElement as Line;
							if (line != null)
							{
								Label label23 = new Label
								{
									Content = "Line Properties"
								};
								label23.Style = (Style)FindResource("ModernLabelStyle");
								ElementPropertiesPanel.Children.Add(label23);
								Label label24 = new Label
								{
									Content = "Length:"
								};
								label24.Style = (Style)FindResource("ModernLabelStyle");
								ElementPropertiesPanel.Children.Add(label24);
								TextBox lengthBox = new TextBox
								{
									Text = canvas.Width.ToString()
								};
								lengthBox.Style = (Style)FindResource("ModernTextBoxStyle");
								lengthBox.TextChanged += delegate
								{
									if (double.TryParse(lengthBox.Text, out var result))
									{
										canvas.Width = result;
										line.X2 = result;
										SetDirty();
									}
								};
								ElementPropertiesPanel.Children.Add(lengthBox);
								Label label25 = new Label
								{
									Content = "Line Width:"
								};
								label25.Style = (Style)FindResource("ModernLabelStyle");
								ElementPropertiesPanel.Children.Add(label25);
								TextBox thicknessBox = new TextBox
								{
									Text = line.StrokeThickness.ToString()
								};
								thicknessBox.Style = (Style)FindResource("ModernTextBoxStyle");
								thicknessBox.TextChanged += delegate
								{
									if (double.TryParse(thicknessBox.Text, out var result))
									{
										line.StrokeThickness = result;
										SetDirty();
									}
								};
								ElementPropertiesPanel.Children.Add(thicknessBox);
								Label label26 = new Label
								{
									Content = "Line Color:"
								};
								label26.Style = (Style)FindResource("ModernLabelStyle");
								ElementPropertiesPanel.Children.Add(label26);
								System.Windows.Media.Color initialColor4 = (line.Stroke as SolidColorBrush)?.Color ?? Colors.Black;
								Button element5 = CreateColorPicker(initialColor4, delegate(System.Windows.Media.Color newColor)
								{
									line.Stroke = new SolidColorBrush(newColor);
									SetDirty();
								});
								ElementPropertiesPanel.Children.Add(element5);
								goto IL_1d7a;
							}
						}
						Line directLine = element as Line;
						if (directLine != null)
						{
							Label label27 = new Label
							{
								Content = "Line Properties"
							};
							label27.Style = (Style)FindResource("ModernLabelStyle");
							ElementPropertiesPanel.Children.Add(label27);
							Label label28 = new Label
							{
								Content = "Length:"
							};
							label28.Style = (Style)FindResource("ModernLabelStyle");
							ElementPropertiesPanel.Children.Add(label28);
							TextBox lengthBox2 = new TextBox
							{
								Text = directLine.Width.ToString()
							};
							lengthBox2.Style = (Style)FindResource("ModernTextBoxStyle");
							lengthBox2.TextChanged += delegate
							{
								if (double.TryParse(lengthBox2.Text, out var result))
								{
									directLine.Width = result;
									directLine.X2 = result;
									SetDirty();
								}
							};
							ElementPropertiesPanel.Children.Add(lengthBox2);
							Label label29 = new Label
							{
								Content = "Line Width:"
							};
							label29.Style = (Style)FindResource("ModernLabelStyle");
							ElementPropertiesPanel.Children.Add(label29);
							TextBox thicknessBox2 = new TextBox
							{
								Text = directLine.StrokeThickness.ToString()
							};
							thicknessBox2.Style = (Style)FindResource("ModernTextBoxStyle");
							thicknessBox2.TextChanged += delegate
							{
								if (double.TryParse(thicknessBox2.Text, out var result))
								{
									directLine.StrokeThickness = result;
									SetDirty();
								}
							};
							ElementPropertiesPanel.Children.Add(thicknessBox2);
							Label label30 = new Label
							{
								Content = "Line Color:"
							};
							label30.Style = (Style)FindResource("ModernLabelStyle");
							ElementPropertiesPanel.Children.Add(label30);
							System.Windows.Media.Color initialColor5 = (directLine.Stroke as SolidColorBrush)?.Color ?? Colors.Black;
							Button element6 = CreateColorPicker(initialColor5, delegate(System.Windows.Media.Color newColor)
							{
								directLine.Stroke = new SolidColorBrush(newColor);
								SetDirty();
							});
							ElementPropertiesPanel.Children.Add(element6);
						}
						else
						{
							System.Windows.Controls.Image img = element as System.Windows.Controls.Image;
							if (img != null)
							{
								string text3 = img.Tag?.ToString() ?? "";
								if (text3 == "QRCode" || text3.StartsWith("QRCodeData:"))
								{
									Label label31 = new Label
									{
										Content = "QR Code Properties"
									};
									label31.Style = (Style)FindResource("ModernLabelStyle");
									ElementPropertiesPanel.Children.Add(label31);
									Label label32 = new Label
									{
										Content = "Data:"
									};
									label32.Style = (Style)FindResource("ModernLabelStyle");
									ElementPropertiesPanel.Children.Add(label32);
									string text4 = (text3.StartsWith("QRCodeData:") ? text3.Substring("QRCodeData:".Length) : "Sample QR Data");
									TextBox dataBox = new TextBox
									{
										Text = text4
									};
									dataBox.Style = (Style)FindResource("ModernTextBoxStyle");
									dataBox.TextChanged += delegate
									{
										img.Tag = "QRCodeData:" + dataBox.Text;
										try
										{
											PixelData pixelData = new BarcodeWriterPixelData
											{
												Format = BarcodeFormat.QR_CODE,
												Options = new EncodingOptions
												{
													Height = (int)img.Height,
													Width = (int)img.Width
												}
											}.Write(dataBox.Text);
											WriteableBitmap writeableBitmap = new WriteableBitmap(pixelData.Width, pixelData.Height, 96.0, 96.0, PixelFormats.Bgra32, null);
											writeableBitmap.WritePixels(new Int32Rect(0, 0, pixelData.Width, pixelData.Height), pixelData.Pixels, pixelData.Width * 4, 0);
											img.Source = writeableBitmap;
											SetDirty();
										}
										catch
										{
										}
									};
									ElementPropertiesPanel.Children.Add(dataBox);
									AddImageSizeProperties(img);
									AddDataFieldMappingSelector(element);
								}
								else if (text3.StartsWith("BarcodeData:"))
								{
									string[] array = text3.Split(':');
									string barcodeType = ((array.Length >= 2) ? array[1] : "CODE128");
									string text5 = ((array.Length >= 3) ? string.Join(":", array.Skip(2)) : "");
									Label label33 = new Label
									{
										Content = barcodeType + " Barcode Properties"
									};
									label33.Style = (Style)FindResource("ModernLabelStyle");
									ElementPropertiesPanel.Children.Add(label33);
									Label label34 = new Label
									{
										Content = "Symbology:"
									};
									label34.Style = (Style)FindResource("ModernLabelStyle");
									ElementPropertiesPanel.Children.Add(label34);
									TextBlock element7 = new TextBlock
									{
										Text = barcodeType,
										FontWeight = FontWeights.Bold,
										Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 94, 92)),
										Margin = new Thickness(0.0, 5.0, 0.0, 10.0)
									};
									ElementPropertiesPanel.Children.Add(element7);
									Label label35 = new Label
									{
										Content = "Data:"
									};
									label35.Style = (Style)FindResource("ModernLabelStyle");
									ElementPropertiesPanel.Children.Add(label35);
									TextBox dataBox2 = new TextBox
									{
										Text = text5
									};
									dataBox2.Style = (Style)FindResource("ModernTextBoxStyle");
									dataBox2.TextChanged += delegate
									{
										img.Tag = "BarcodeData:" + barcodeType + ":" + dataBox2.Text;
										try
										{
											System.Windows.Controls.Image image = CreateBarcodeImage(barcodeType, dataBox2.Text, img.Width, img.Height);
											if (image?.Source != null)
											{
												img.Source = image.Source;
												SetDirty();
											}
										}
										catch
										{
										}
									};
									ElementPropertiesPanel.Children.Add(dataBox2);
									AddImageSizeProperties(img);
									AddDataFieldMappingSelector(element);
								}
								else
								{
									Label label36 = new Label
									{
										Content = "Image Properties"
									};
									label36.Style = (Style)FindResource("ModernLabelStyle");
									ElementPropertiesPanel.Children.Add(label36);
									AddImageSizeProperties(img);
								}
							}
						}
					}
				}
			}
			goto IL_1d7a;
			IL_1d7a:
			AddRotationControls(element);
		}
		catch (Exception ex)
		{
			try
			{
				ElementPropertiesPanel.Children.Clear();
				ElementPropertiesPanel.Children.Add(new Label
				{
					Content = "Error loading properties: " + ex.Message
				});
			}
			catch
			{
			}
		}
	}

	private void AddLinkedImageSizeProperties(FrameworkElement element, double width, double height)
	{
		Label label = new Label
		{
			Content = "Width:"
		};
		label.Style = (Style)FindResource("ModernLabelStyle");
		ElementPropertiesPanel.Children.Add(label);
		TextBox widthBox = new TextBox
		{
			Text = width.ToString()
		};
		widthBox.Style = (Style)FindResource("ModernTextBoxStyle");
		widthBox.TextChanged += delegate
		{
			if (double.TryParse(widthBox.Text, out var result))
			{
				element.Width = result;
				SetDirty();
			}
		};
		ElementPropertiesPanel.Children.Add(widthBox);
		Label label2 = new Label
		{
			Content = "Height:"
		};
		label2.Style = (Style)FindResource("ModernLabelStyle");
		ElementPropertiesPanel.Children.Add(label2);
		TextBox heightBox = new TextBox
		{
			Text = height.ToString()
		};
		heightBox.Style = (Style)FindResource("ModernTextBoxStyle");
		heightBox.TextChanged += delegate
		{
			if (double.TryParse(heightBox.Text, out var result))
			{
				element.Height = result;
				SetDirty();
			}
		};
		ElementPropertiesPanel.Children.Add(heightBox);
	}

	private void AddImageSizeProperties(System.Windows.Controls.Image img)
	{
		Label label = new Label
		{
			Content = "Width:"
		};
		label.Style = (Style)FindResource("ModernLabelStyle");
		ElementPropertiesPanel.Children.Add(label);
		TextBox widthBox = new TextBox
		{
			Text = img.Width.ToString()
		};
		widthBox.Style = (Style)FindResource("ModernTextBoxStyle");
		widthBox.TextChanged += delegate
		{
			if (double.TryParse(widthBox.Text, out var result))
			{
				img.Width = result;
				SetDirty();
			}
		};
		ElementPropertiesPanel.Children.Add(widthBox);
		Label label2 = new Label
		{
			Content = "Height:"
		};
		label2.Style = (Style)FindResource("ModernLabelStyle");
		ElementPropertiesPanel.Children.Add(label2);
		TextBox heightBox = new TextBox
		{
			Text = img.Height.ToString()
		};
		heightBox.Style = (Style)FindResource("ModernTextBoxStyle");
		heightBox.TextChanged += delegate
		{
			if (double.TryParse(heightBox.Text, out var result))
			{
				img.Height = result;
				SetDirty();
			}
		};
		ElementPropertiesPanel.Children.Add(heightBox);
	}

	private void UpdateLinkedImageUrl(System.Windows.Controls.Image linkedImage, string newUrl)
	{
		if (string.IsNullOrWhiteSpace(newUrl))
		{
			return;
		}
		try
		{
			linkedImage.Tag = "LinkedImage:" + newUrl;
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(newUrl, UriKind.Absolute);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			linkedImage.Source = bitmapImage;
			SetDirty();
			File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Updated LinkedImage URL to: {newUrl}\n");
		}
		catch (Exception ex)
		{
			File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Failed to update LinkedImage URL '{newUrl}': {ex.Message}\n");
			MessageBox.Show("Failed to load image from URL:\n" + ex.Message, "Image Load Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void AddTextFormattingControls(TextBlock textBlock, Border border)
	{
		ElementPropertiesPanel.Children.Add(new Separator
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 5.0)
		});
		Label element = new Label
		{
			Content = "Text Formatting:",
			FontWeight = FontWeights.SemiBold,
			Style = (Style)FindResource("ModernLabelStyle")
		};
		ElementPropertiesPanel.Children.Add(element);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		CheckBox checkBox = new CheckBox
		{
			Content = "Bold",
			IsChecked = (textBlock.FontWeight == FontWeights.Bold),
			Margin = new Thickness(0.0, 5.0, 5.0, 5.0)
		};
		checkBox.Checked += delegate
		{
			textBlock.FontWeight = FontWeights.Bold;
			SetDirty();
		};
		checkBox.Unchecked += delegate
		{
			textBlock.FontWeight = FontWeights.Normal;
			SetDirty();
		};
		Grid.SetRow(checkBox, 0);
		Grid.SetColumn(checkBox, 0);
		grid.Children.Add(checkBox);
		CheckBox checkBox2 = new CheckBox
		{
			Content = "Italic",
			IsChecked = (textBlock.FontStyle == FontStyles.Italic),
			Margin = new Thickness(5.0, 5.0, 0.0, 5.0)
		};
		checkBox2.Checked += delegate
		{
			textBlock.FontStyle = FontStyles.Italic;
			SetDirty();
		};
		checkBox2.Unchecked += delegate
		{
			textBlock.FontStyle = FontStyles.Normal;
			SetDirty();
		};
		Grid.SetRow(checkBox2, 0);
		Grid.SetColumn(checkBox2, 1);
		grid.Children.Add(checkBox2);
		CheckBox checkBox3 = new CheckBox
		{
			Content = "Underline",
			IsChecked = textBlock.TextDecorations.Contains(TextDecorations.Underline[0]),
			Margin = new Thickness(0.0, 5.0, 5.0, 5.0)
		};
		checkBox3.Checked += delegate
		{
			if (!textBlock.TextDecorations.Contains(TextDecorations.Underline[0]))
			{
				textBlock.TextDecorations.Add(TextDecorations.Underline[0]);
			}
			SetDirty();
		};
		checkBox3.Unchecked += delegate
		{
			textBlock.TextDecorations.Remove(TextDecorations.Underline[0]);
			SetDirty();
		};
		Grid.SetRow(checkBox3, 1);
		Grid.SetColumn(checkBox3, 0);
		grid.Children.Add(checkBox3);
		CheckBox checkBox4 = new CheckBox
		{
			Content = "Multiline",
			IsChecked = (textBlock.TextWrapping == TextWrapping.Wrap),
			Margin = new Thickness(5.0, 5.0, 0.0, 5.0)
		};
		checkBox4.Checked += delegate
		{
			textBlock.TextWrapping = TextWrapping.Wrap;
			SetDirty();
		};
		checkBox4.Unchecked += delegate
		{
			textBlock.TextWrapping = TextWrapping.NoWrap;
			SetDirty();
		};
		Grid.SetRow(checkBox4, 1);
		Grid.SetColumn(checkBox4, 1);
		grid.Children.Add(checkBox4);
		ElementPropertiesPanel.Children.Add(grid);
	}

	private void AddDataFieldMappingSelector(UIElement element)
	{
		if (lastResultTable == null || lastResultTable.Columns.Count == 0)
		{
			return;
		}
		ElementPropertiesPanel.Children.Add(new Separator
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 10.0)
		});
		Label element2 = new Label
		{
			Content = "Data Field Mapping:",
			FontWeight = FontWeights.Bold,
			Style = (Style)FindResource("ModernLabelStyle")
		};
		ElementPropertiesPanel.Children.Add(element2);
		int elementIndex = GetElementIndex(element);
		FieldMapping fieldMapping = CurrentTemplate?.FieldMappings?.FirstOrDefault((FieldMapping fm) => fm.ElementIndex == elementIndex);
		ComboBox fieldCombo = new ComboBox
		{
			Style = (Style)FindResource("ModernComboBoxStyle"),
			Margin = new Thickness(0.0, 5.0, 0.0, 5.0)
		};
		fieldCombo.Items.Add("(None)");
		foreach (DataColumn column in lastResultTable.Columns)
		{
			fieldCombo.Items.Add(column.ColumnName);
		}
		if (fieldMapping != null && !string.IsNullOrEmpty(fieldMapping.FieldName))
		{
			fieldCombo.SelectedItem = fieldMapping.FieldName;
		}
		else
		{
			fieldCombo.SelectedIndex = 0;
		}
		fieldCombo.SelectionChanged += delegate
		{
			if (fieldCombo.SelectedItem != null)
			{
				string text = fieldCombo.SelectedItem.ToString();
				if (text == "(None)")
				{
					FieldMapping fieldMapping2 = CurrentTemplate.FieldMappings?.FirstOrDefault((FieldMapping fm) => fm.ElementIndex == elementIndex);
					if (fieldMapping2 != null)
					{
						CurrentTemplate.FieldMappings.Remove(fieldMapping2);
						SetDirty();
					}
				}
				else
				{
					if (CurrentTemplate.FieldMappings == null)
					{
						CurrentTemplate.FieldMappings = new List<FieldMapping>();
					}
					FieldMapping fieldMapping3 = CurrentTemplate.FieldMappings.FirstOrDefault((FieldMapping fm) => fm.ElementIndex == elementIndex);
					if (fieldMapping3 != null)
					{
						fieldMapping3.FieldName = text;
					}
					else
					{
						CurrentTemplate.FieldMappings.Add(new FieldMapping
						{
							ElementIndex = elementIndex,
							FieldName = text
						});
					}
					SetDirty();
				}
			}
		};
		ElementPropertiesPanel.Children.Add(fieldCombo);
		TextBlock element3 = new TextBlock
		{
			Text = "Maps this element to a database field. Data will be replaced when printing from a query.",
			TextWrapping = TextWrapping.Wrap,
			FontSize = 10.0,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 128, 128)),
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
		};
		ElementPropertiesPanel.Children.Add(element3);
	}

	private int GetElementIndex(UIElement element)
	{
		int num = 0;
		foreach (UIElement child in LabelCanvas.Children)
		{
			if (!(child is Thumb))
			{
				if (child == element)
				{
					return num;
				}
				num++;
			}
		}
		return -1;
	}

	private void AddRotationControls(UIElement element)
	{
		ElementPropertiesPanel.Children.Add(new Separator
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 10.0)
		});
		Label element2 = new Label
		{
			Content = "Rotation:",
			FontWeight = FontWeights.Bold,
			Style = (Style)FindResource("ModernLabelStyle")
		};
		ElementPropertiesPanel.Children.Add(element2);
		double value = 0.0;
		if (elementRotations.ContainsKey(element))
		{
			value = elementRotations[element];
		}
		else if (element.RenderTransform is RotateTransform rotateTransform)
		{
			value = rotateTransform.Angle;
		}
		Slider rotationSlider = new Slider
		{
			Minimum = 0.0,
			Maximum = 360.0,
			Value = value,
			TickFrequency = 1.0,
			TickPlacement = TickPlacement.BottomRight,
			IsSnapToTickEnabled = false,
			Margin = new Thickness(0.0, 5.0, 0.0, 5.0)
		};
		TextBlock rotationValueText = new TextBlock
		{
			Text = $"{value:F1}Â°",
			HorizontalAlignment = HorizontalAlignment.Center,
			FontWeight = FontWeights.SemiBold
		};
		rotationSlider.ValueChanged += delegate
		{
			double value2 = rotationSlider.Value;
			rotationValueText.Text = $"{value2:F1}Â°";
			RotateTransform renderTransform = new RotateTransform(value2);
			if (element is FrameworkElement frameworkElement)
			{
				frameworkElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
			}
			element.RenderTransform = renderTransform;
			elementRotations[element] = value2;
			SetDirty();
		};
		ElementPropertiesPanel.Children.Add(rotationSlider);
		ElementPropertiesPanel.Children.Add(rotationValueText);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "90Â°",
			Width = 50.0,
			Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
			Style = (Style)FindResource("CompactButtonStyle")
		};
		button.Click += delegate
		{
			rotationSlider.Value = (rotationSlider.Value + 90.0) % 360.0;
		};
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "180Â°",
			Width = 55.0,
			Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
			Style = (Style)FindResource("CompactButtonStyle")
		};
		button2.Click += delegate
		{
			rotationSlider.Value = (rotationSlider.Value + 180.0) % 360.0;
		};
		stackPanel.Children.Add(button2);
		Button button3 = new Button
		{
			Content = "Reset",
			Width = 55.0,
			Style = (Style)FindResource("CompactButtonStyle")
		};
		button3.Click += delegate
		{
			rotationSlider.Value = 0.0;
		};
		stackPanel.Children.Add(button3);
		ElementPropertiesPanel.Children.Add(stackPanel);
	}

	private void SaveTemplate_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			CurrentTemplate.TemplateName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath ?? "Untitled Template");
			if (CurrentTemplate.TemplateType.IsInkjetLike())
			{
				CurrentTemplate.Elements.Clear();
				CurrentTemplate.FieldMappings.Clear();
				CurrentTemplate.InkjetConfig = InkjetPanel.ToConfig();
			}
			else
			{
				CurrentTemplate.InkjetConfig = null;
				CurrentTemplate.Elements.Clear();
				foreach (UIElement child2 in LabelCanvas.Children)
				{
					if (child2 is Thumb)
					{
						continue;
					}
					LabelElement labelElement = new LabelElement
					{
						X = Canvas.GetLeft(child2),
						Y = Canvas.GetTop(child2),
						Width = ((child2 is FrameworkElement frameworkElement) ? frameworkElement.Width : 100.0),
						Height = ((child2 is FrameworkElement frameworkElement2) ? frameworkElement2.Height : 30.0),
						Rotation = (elementRotations.ContainsKey(child2) ? elementRotations[child2] : ((child2.RenderTransform is RotateTransform rotateTransform) ? rotateTransform.Angle : 0.0))
					};
					if (child2 is TextBlock textBlock)
					{
						if (textBlock.Text == "[EAN-13]")
						{
							labelElement.Type = LabelElementType.Barcode13;
						}
						else if (textBlock.Text == "[EAN-8]")
						{
							labelElement.Type = LabelElementType.Barcode8;
						}
						else if (textBlock.Text == "[QR Code]")
						{
							labelElement.Type = LabelElementType.QRCode;
						}
						else
						{
							labelElement.Type = LabelElementType.Text;
						}
						labelElement.Content = textBlock.Text;
						labelElement.FontSize = textBlock.FontSize;
						labelElement.FontFamily = textBlock.FontFamily.ToString();
						labelElement.FontWeight = textBlock.FontWeight.ToString();
						labelElement.FontStyle = textBlock.FontStyle.ToString();
						labelElement.Foreground = textBlock.Foreground.ToString();
						labelElement.TextDecorations = GetTextDecorationsString(textBlock.TextDecorations);
						LabelElement labelElement2 = labelElement;
						labelElement2.TextAlignment = textBlock.TextAlignment switch
						{
							TextAlignment.Center => "Center", 
							TextAlignment.Right => "Right", 
							TextAlignment.Left => "Left", 
							_ => "Left", 
						};
						labelElement2 = labelElement;
						labelElement2.VerticalAlignment = textBlock.VerticalAlignment switch
						{
							VerticalAlignment.Center => "Center", 
							VerticalAlignment.Bottom => "Bottom", 
							VerticalAlignment.Top => "Top", 
							_ => "Top", 
						};
					}
					else if (child2 is Border { Child: TextBlock child })
					{
						if (child.Text == "[EAN-13]")
						{
							labelElement.Type = LabelElementType.Barcode13;
						}
						else if (child.Text == "[EAN-8]")
						{
							labelElement.Type = LabelElementType.Barcode8;
						}
						else if (child.Text == "[QR Code]")
						{
							labelElement.Type = LabelElementType.QRCode;
						}
						else
						{
							labelElement.Type = LabelElementType.Text;
						}
						labelElement.Content = child.Text;
						labelElement.FontSize = child.FontSize;
						labelElement.FontFamily = child.FontFamily.ToString();
						labelElement.FontWeight = child.FontWeight.ToString();
						labelElement.FontStyle = child.FontStyle.ToString();
						labelElement.Foreground = child.Foreground.ToString();
						labelElement.TextDecorations = GetTextDecorationsString(child.TextDecorations);
						LabelElement labelElement2 = labelElement;
						labelElement2.TextAlignment = child.TextAlignment switch
						{
							TextAlignment.Center => "Center", 
							TextAlignment.Right => "Right", 
							TextAlignment.Left => "Left", 
							_ => "Left", 
						};
						labelElement2 = labelElement;
						labelElement2.VerticalAlignment = child.VerticalAlignment switch
						{
							VerticalAlignment.Center => "Center", 
							VerticalAlignment.Bottom => "Bottom", 
							VerticalAlignment.Top => "Top", 
							_ => "Top", 
						};
					}
					else if (child2 is System.Windows.Shapes.Rectangle { Tag: var tag } rectangle)
					{
						string text = tag?.ToString() ?? "";
						if (rectangle.Fill == System.Windows.Media.Brushes.Transparent && rectangle.Stroke != null)
						{
							labelElement.Type = LabelElementType.Rectangle;
							labelElement.BorderWidth = rectangle.StrokeThickness;
							labelElement.BorderColor = (rectangle.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
						}
						else
						{
							labelElement.Type = LabelElementType.Image;
							if (!string.IsNullOrEmpty(text) && text != "Missing Image Data" && text != "No Image Data")
							{
								if (File.Exists(text))
								{
									labelElement.ImageData = ConvertImageToBase64(text);
									labelElement.Content = "";
								}
								else
								{
									labelElement.Content = text;
								}
							}
						}
					}
					else if (child2 is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Line line)
					{
						labelElement.Type = LabelElementType.Line;
						labelElement.BorderWidth = line.StrokeThickness;
						labelElement.BorderColor = (line.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
					}
					else if (child2 is Line line2)
					{
						labelElement.Type = LabelElementType.Line;
						labelElement.BorderWidth = line2.StrokeThickness;
						labelElement.BorderColor = (line2.Stroke as SolidColorBrush)?.Color.ToString() ?? "Black";
					}
					else if (child2 is System.Windows.Controls.Image { Tag: var tag2 } image)
					{
						if (!(tag2?.ToString() == "QRCode"))
						{
							object tag3 = image.Tag;
							if (tag3 == null || tag3.ToString()?.StartsWith("QRCodeData:") != true)
							{
								object tag4 = image.Tag;
								if (tag4 != null && tag4.ToString()?.StartsWith("BarcodeData:") == true)
								{
									string[] array = image.Tag.ToString().Split(':');
									if (array.Length >= 3)
									{
										string barcodeType = array[1];
										string content = string.Join(":", array.Skip(2));
										labelElement.Type = GetLabelElementTypeFromBarcodeType(barcodeType);
										labelElement.Content = content;
										labelElement.BarcodeType = barcodeType;
									}
									else
									{
										labelElement.Type = LabelElementType.EAN13;
										labelElement.Content = "4000000000004";
										labelElement.BarcodeType = "EAN13";
									}
								}
								else
								{
									labelElement.Type = LabelElementType.Image;
									string text2 = image.Tag?.ToString() ?? "";
									if (text2.StartsWith("ImageData:"))
									{
										labelElement.ImageData = text2.Substring("ImageData:".Length);
										labelElement.Content = "";
									}
									else
									{
										labelElement.Content = text2;
										if (!string.IsNullOrEmpty(text2) && File.Exists(text2))
										{
											labelElement.ImageData = ConvertImageToBase64(text2);
										}
									}
								}
								goto IL_08d7;
							}
						}
						labelElement.Type = LabelElementType.QRCode;
						object tag5 = image.Tag;
						if (tag5 != null && tag5.ToString()?.StartsWith("QRCodeData:") == true)
						{
							labelElement.Content = image.Tag.ToString().Substring("QRCodeData:".Length);
						}
						else
						{
							labelElement.Content = "Sample QR Data";
						}
					}
					goto IL_08d7;
					IL_08d7:
					CurrentTemplate.Elements.Add(labelElement);
				}
			}
			if (_currentDataSource != null)
			{
				CurrentTemplate.DataSource = _currentDataSource;
			}
			string text3;
			if (!string.IsNullOrEmpty(_currentFilePath))
			{
				text3 = _currentFilePath;
			}
			else
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Filter = "Label Template (*.xml)|*.xml",
					DefaultExt = "xml",
					FileName = "MyTemplate"
				};
				if (saveFileDialog.ShowDialog() != true)
				{
					return;
				}
				text3 = saveFileDialog.FileName;
			}
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(LabelTemplate));
			using FileStream stream = new FileStream(text3, FileMode.Create);
			xmlSerializer.Serialize(stream, CurrentTemplate);
			_currentFilePath = text3;
			SetDirty(isDirty: false);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error saving template:\n" + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void LoadTemplate_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Label Template (*.xml)|*.xml",
				DefaultExt = "xml"
			};
			if (openFileDialog.ShowDialog() != true)
			{
				return;
			}
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(LabelTemplate));
			using FileStream stream = new FileStream(openFileDialog.FileName, FileMode.Open);
			CurrentTemplate = (LabelTemplate)xmlSerializer.Deserialize(stream);
			if (CurrentTemplate.DataSource != null)
			{
				_currentDataSource = CurrentTemplate.DataSource;
			}
			_labelWidthMm = CurrentTemplate.LabelWidth;
			_labelHeightMm = CurrentTemplate.LabelHeight;
			UpdateDisplayedDimensions();
			UpdateCanvasSize();
			LabelCanvas.Children.Clear();
			elementRotations.Clear();
			RemoveResizingHandles();
			selectedElement = null;
			if (!CurrentTemplate.TemplateType.IsInkjetLike())
			{
				foreach (LabelElement element in CurrentTemplate.Elements)
				{
					UIElement uIElement = CreateElementFromTemplate(element);
					if (uIElement != null)
					{
						Canvas.SetLeft(uIElement, element.X);
						Canvas.SetTop(uIElement, element.Y);
						uIElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
						LabelCanvas.Children.Add(uIElement);
					}
				}
			}
			ApplyTemplateTypeUi(CurrentTemplate.TemplateType);
			if (CurrentTemplate.TemplateType.IsInkjetLike())
			{
				InkjetPanel.SetVendor(CurrentTemplate.TemplateType);
				InkjetPanel.LoadFromConfig(CurrentTemplate.InkjetConfig);
				InkjetPanel.SetAvailableFields(lastResultTable);
			}
			_currentFilePath = openFileDialog.FileName;
			SetDirty(isDirty: false);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading template:\n" + ex.Message, "Load Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void RefreshLicenseBanner()
	{
		try
		{
			LicenseEvaluation eval = PortalLicenseService.Instance.GetStatus();
			if (eval.Status == LicenseStatus.Expiring)
			{
				LicenseBanner.Visibility = Visibility.Visible;
				LicenseBanner.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 243, 205));
				LicenseBanner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
				LicenseBannerText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(133, 100, 4));
				LicenseBannerText.Text = "License expiring soon" +
					(string.IsNullOrWhiteSpace(eval.LicenseExpiresAt) ? "." : $" ({eval.LicenseExpiresAt}). Contact Evolabs.");
			}
			else if (eval.Status == LicenseStatus.Grace)
			{
				LicenseBanner.Visibility = Visibility.Visible;
				LicenseBanner.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 215, 218));
				LicenseBanner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69));
				LicenseBannerText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(114, 28, 36));
				LicenseBannerText.Text = eval.Message ?? "Offline grace — reconnect to admin.evolabs.co";
			}
			else
			{
				LicenseBanner.Visibility = Visibility.Collapsed;
			}
		}
		catch
		{
			LicenseBanner.Visibility = Visibility.Collapsed;
		}
	}

	private void StartLicenseRefreshTimer()
	{
		var timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromHours(LicenseConstants.RefreshHours)
		};
		timer.Tick += async (_, _) =>
		{
			try
			{
				await PortalLicenseService.Instance.ScheduledRefreshAsync();
				RefreshLicenseBanner();
			}
			catch
			{
			}
		};
		timer.Start();
	}

	private void PrintPreview_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			PortalLicenseService.Instance.RequireLicensed();
		}
		catch (LicenseDeniedException ex)
		{
			MessageBox.Show(ex.Message, "License Required", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (CurrentTemplate.TemplateType.IsInkjetLike())
		{
			RunInkjetTemplateAsync();
			return;
		}
		try
		{
			File.WriteAllText("debug.log", "[DEBUG] ==> Starting CreateLiveTemplate\n");
			Console.WriteLine("[DEBUG] ==> Starting CreateLiveTemplate");
			LabelTemplate labelTemplate = CreateLiveTemplate();
			Console.WriteLine($"[DEBUG] Live template created with {labelTemplate.Elements.Count} elements");
			for (int i = 0; i < labelTemplate.Elements.Count; i++)
			{
				LabelElement labelElement = labelTemplate.Elements[i];
				Console.WriteLine($"[DEBUG] Element {i}: Type={labelElement.Type}, Content='{labelElement.Content}', Size={labelElement.Width}x{labelElement.Height}, Pos=({labelElement.X},{labelElement.Y})");
			}
			PrintPreviewWindow printPreviewWindow = new PrintPreviewWindow(labelTemplate, lastResultTable);
			Console.WriteLine("[DEBUG] PrintPreviewWindow created, showing dialog");
			printPreviewWindow.ShowDialog();
			Console.WriteLine("[DEBUG] PrintPreviewWindow dialog closed");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DEBUG] ERROR in PrintPreview_Click: " + ex.Message);
			MessageBox.Show("Error opening print preview: " + ex.Message, "Print Preview Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void DataSource_Click(object sender, RoutedEventArgs e)
	{
		DataSourceWindow dataSourceWindow = new DataSourceWindow(_currentDataSource);
		if (dataSourceWindow.ShowDialog() == true)
		{
			lastResultTable = dataSourceWindow.ResultTable;
			_gs1SampleActive = false;
			_gs1SampleLevel = 0;
			_currentDataSource = dataSourceWindow.CreateDataSourceConfig();
			if (CurrentTemplate.TemplateType.IsInkjetLike())
			{
				InkjetPanel.SetAvailableFields(lastResultTable);
			}
			SetDirty();
		}
	}

	private void BringForward_Click(object sender, RoutedEventArgs e)
	{
		if (selectedElement != null)
		{
			int num = LabelCanvas.Children.IndexOf(selectedElement);
			if (num < LabelCanvas.Children.Count - 1)
			{
				LabelCanvas.Children.RemoveAt(num);
				LabelCanvas.Children.Insert(num + 1, selectedElement);
			}
		}
	}

	private void SendBackward_Click(object sender, RoutedEventArgs e)
	{
		if (selectedElement != null)
		{
			int num = LabelCanvas.Children.IndexOf(selectedElement);
			if (num > 0)
			{
				LabelCanvas.Children.RemoveAt(num);
				LabelCanvas.Children.Insert(num - 1, selectedElement);
			}
		}
	}

	private void SetDirty(bool isDirty = true)
	{
		_isDirty = isDirty;
		string text = "TaggJack - Label Designer";
		base.Title = (_isDirty ? (text + " *") : text);
	}

	private void LoadLinkedImageFromUrl(Border linkedBorder, string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return;
		}
		try
		{
			linkedBorder.Tag = "LinkedImage:" + url;
			System.Windows.Controls.Image image = new System.Windows.Controls.Image
			{
				Width = linkedBorder.Width,
				Height = linkedBorder.Height,
				Stretch = Stretch.Uniform,
				Tag = "LinkedImage:" + url
			};
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(url, UriKind.Absolute);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			image.Source = bitmapImage;
			image.MouseLeftButtonDown += Element_MouseLeftButtonDown;
			if (linkedBorder.Parent is Canvas canvas)
			{
				System.Windows.Point point = new System.Windows.Point(Canvas.GetLeft(linkedBorder), Canvas.GetTop(linkedBorder));
				canvas.Children.Remove(linkedBorder);
				Canvas.SetLeft(image, point.X);
				Canvas.SetTop(image, point.Y);
				canvas.Children.Add(image);
				selectedElement = image;
				ShowProperties(image);
			}
			SetDirty();
			File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Successfully loaded LinkedImage from URL: {url}\n");
		}
		catch (Exception ex)
		{
			File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Failed to load LinkedImage from URL '{url}': {ex.Message}\n");
			MessageBox.Show("Failed to load image from URL:\n" + ex.Message, "Image Load Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			linkedBorder.Tag = "LinkedImage:" + url;
			SetDirty();
		}
	}

	private void InitializeZoomControls()
	{
		ZoomInButton.Click += delegate
		{
			ZoomIn();
		};
		ZoomOutButton.Click += delegate
		{
			ZoomOut();
		};
		ZoomResetButton.Click += delegate
		{
			ZoomReset();
		};
		ZoomFitButton.Click += delegate
		{
			ZoomToFit();
		};
		UpdateZoomDisplay();
	}

	private void ZoomIn()
	{
		if (_zoomFactor < 5.0)
		{
			_zoomFactor = Math.Min(5.0, _zoomFactor + 0.1);
			ApplyZoom();
		}
	}

	private void ZoomOut()
	{
		if (_zoomFactor > 0.1)
		{
			_zoomFactor = Math.Max(0.1, _zoomFactor - 0.1);
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
		System.Windows.Size size = new System.Windows.Size(CanvasScrollViewer.ActualWidth - 20.0, CanvasScrollViewer.ActualHeight - 20.0);
		System.Windows.Size size2 = new System.Windows.Size(LabelCanvas.Width, LabelCanvas.Height);
		if (size2.Width > 0.0 && size2.Height > 0.0)
		{
			double val = size.Width / size2.Width;
			double val2 = size.Height / size2.Height;
			_zoomFactor = Math.Min(val, val2);
			_zoomFactor = Math.Max(0.1, Math.Min(5.0, _zoomFactor));
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
		ZoomTextBlock.Text = $"{_zoomFactor * 100.0:F0}%";
	}

	private LabelTemplate CreateLiveTemplate()
	{
		string contents = "[DEBUG] CreateLiveTemplate called\n";
		File.AppendAllText("debug.log", contents);
		Console.WriteLine("[DEBUG] CreateLiveTemplate called");
		LabelTemplate labelTemplate = new LabelTemplate
		{
			TemplateName = CurrentTemplate.TemplateName,
			LabelWidth = ((CurrentTemplate.LabelWidth > 0.0) ? CurrentTemplate.LabelWidth : _labelWidthMm),
			LabelHeight = ((CurrentTemplate.LabelHeight > 0.0) ? CurrentTemplate.LabelHeight : _labelHeightMm),
			MarginLeft = CurrentTemplate.MarginLeft,
			MarginRight = CurrentTemplate.MarginRight,
			MarginTop = CurrentTemplate.MarginTop,
			MarginBottom = CurrentTemplate.MarginBottom
		};
		List<UIElement> labelElements = GetLabelElements();
		string contents2 = $"[DEBUG] Found {labelElements.Count} label elements on canvas\n";
		File.AppendAllText("debug.log", contents2);
		Console.WriteLine($"[DEBUG] Found {labelElements.Count} label elements on canvas");
		foreach (UIElement item in labelElements)
		{
			string contents3 = "[DEBUG] Processing canvas child: " + item.GetType().Name + "\n";
			File.AppendAllText("debug.log", contents3);
			Console.WriteLine("[DEBUG] Processing canvas child: " + item.GetType().Name);
			LabelElement labelElement = new LabelElement
			{
				X = Canvas.GetLeft(item),
				Y = Canvas.GetTop(item),
				Width = ((item is FrameworkElement frameworkElement) ? frameworkElement.Width : 100.0),
				Height = ((item is FrameworkElement frameworkElement2) ? frameworkElement2.Height : 30.0)
			};
			if (item is TextBlock textBlock)
			{
				string contents4 = "[DEBUG] Found TextBlock: '" + textBlock.Text + "'\n";
				File.AppendAllText("debug.log", contents4);
				Console.WriteLine("[DEBUG] Found TextBlock: '" + textBlock.Text + "'");
				labelElement.Type = LabelElementType.Text;
				labelElement.Content = textBlock.Text;
				labelElement.FontSize = textBlock.FontSize;
				labelElement.FontFamily = textBlock.FontFamily.ToString();
				labelElement.FontWeight = textBlock.FontWeight.ToString();
				labelElement.FontStyle = textBlock.FontStyle.ToString();
				labelElement.Foreground = textBlock.Foreground.ToString();
				labelElement.TextDecorations = GetTextDecorationsString(textBlock.TextDecorations);
			}
			else if (item is System.Windows.Controls.Image { Tag: not null } image)
			{
				string text = image.Tag.ToString();
				if (text.StartsWith("BarcodeData:"))
				{
					string[] array = text.Split(':');
					if (array.Length >= 3)
					{
						string text2 = array[1];
						string content = string.Join(":", array.Skip(2));
						switch (text2.ToUpper())
						{
						case "EAN13":
							labelElement.Type = LabelElementType.EAN13;
							break;
						case "EAN8":
							labelElement.Type = LabelElementType.EAN8;
							break;
						case "UPCA":
							labelElement.Type = LabelElementType.UPCA;
							break;
						case "UPCE":
							labelElement.Type = LabelElementType.UPCE;
							break;
						case "CODE128":
							labelElement.Type = LabelElementType.Code128;
							break;
						case "CODE39":
							labelElement.Type = LabelElementType.Code39;
							break;
						case "QRCODE":
							labelElement.Type = LabelElementType.QRCode;
							break;
						case "AZTEC":
							labelElement.Type = LabelElementType.Aztec;
							break;
						case "DATAMATRIX":
							labelElement.Type = LabelElementType.DataMatrix;
							break;
						case "GS1DATABAR":
							labelElement.Type = LabelElementType.GS1DataBar;
							break;
						case "PDF417":
							labelElement.Type = LabelElementType.PDF417;
							break;
						default:
							labelElement.Type = LabelElementType.Barcode;
							break;
						}
						labelElement.Content = content;
						labelElement.BarcodeType = text2;
					}
				}
				else if (text.StartsWith("ImageData:"))
				{
					labelElement.Type = LabelElementType.Image;
					labelElement.ImageData = text.Substring("ImageData:".Length);
					labelElement.Content = "";
				}
				else if (text.StartsWith("LinkedImage:"))
				{
					labelElement.Type = LabelElementType.LinkedImage;
					labelElement.Content = text.Substring("LinkedImage:".Length);
					File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: CreateLiveTemplate - Found LinkedImage with URL: {labelElement.Content}\n");
				}
				else
				{
					labelElement.Type = LabelElementType.Image;
					labelElement.Content = text;
				}
			}
			else if (item is System.Windows.Controls.Image image2)
			{
				labelElement.Type = LabelElementType.Image;
				labelElement.Content = image2.Source?.ToString() ?? "";
			}
			else if (item is Canvas canvas)
			{
				string contents5 = $"[DEBUG] Found Canvas with {canvas.Children.Count} children\n";
				File.AppendAllText("debug.log", contents5);
				Console.WriteLine($"[DEBUG] Found Canvas with {canvas.Children.Count} children");
				Line line = canvas.Children.OfType<Line>().FirstOrDefault();
				System.Windows.Shapes.Rectangle rectangle = canvas.Children.OfType<System.Windows.Shapes.Rectangle>().FirstOrDefault();
				if (line != null)
				{
					string contents6 = $"[DEBUG] Found Line in Canvas: X1={line.X1}, Y1={line.Y1}, X2={line.X2}, Y2={line.Y2}\n";
					File.AppendAllText("debug.log", contents6);
					Console.WriteLine($"[DEBUG] Found Line in Canvas: X1={line.X1}, Y1={line.Y1}, X2={line.X2}, Y2={line.Y2}");
					labelElement.Type = LabelElementType.Line;
					labelElement.BorderWidth = line.StrokeThickness;
					labelElement.BorderColor = line.Stroke?.ToString() ?? "#000000";
					labelElement.Content = $"{line.X1},{line.Y1},{line.X2},{line.Y2}";
				}
				else
				{
					if (rectangle == null)
					{
						Console.WriteLine("[DEBUG] Canvas with no recognized shapes, skipping");
						continue;
					}
					string contents7 = $"[DEBUG] Found Rectangle in Canvas: Width={rectangle.Width}, Height={rectangle.Height}\n";
					File.AppendAllText("debug.log", contents7);
					Console.WriteLine($"[DEBUG] Found Rectangle in Canvas: Width={rectangle.Width}, Height={rectangle.Height}");
					labelElement.Type = LabelElementType.Rectangle;
					labelElement.BorderWidth = rectangle.StrokeThickness;
					labelElement.BorderColor = rectangle.Stroke?.ToString() ?? "#000000";
					labelElement.Content = "";
					labelElement.Width = (double.IsNaN(rectangle.Width) ? labelElement.Width : rectangle.Width);
					labelElement.Height = (double.IsNaN(rectangle.Height) ? labelElement.Height : rectangle.Height);
				}
			}
			else if (item is System.Windows.Shapes.Rectangle rectangle2)
			{
				string contents8 = $"[DEBUG] Found direct Rectangle: Width={rectangle2.Width}, Height={rectangle2.Height}\n";
				File.AppendAllText("debug.log", contents8);
				Console.WriteLine($"[DEBUG] Found direct Rectangle: Width={rectangle2.Width}, Height={rectangle2.Height}");
				labelElement.Type = LabelElementType.Rectangle;
				labelElement.BorderWidth = rectangle2.StrokeThickness;
				labelElement.BorderColor = rectangle2.Stroke?.ToString() ?? "#000000";
				labelElement.Content = "";
				labelElement.Width = (double.IsNaN(rectangle2.Width) ? labelElement.Width : rectangle2.Width);
				labelElement.Height = (double.IsNaN(rectangle2.Height) ? labelElement.Height : rectangle2.Height);
			}
			else if (item is Ellipse ellipse)
			{
				string contents9 = $"[DEBUG] Found direct Ellipse: Width={ellipse.Width}, Height={ellipse.Height}\n";
				File.AppendAllText("debug.log", contents9);
				Console.WriteLine($"[DEBUG] Found direct Ellipse: Width={ellipse.Width}, Height={ellipse.Height}");
				labelElement.Type = LabelElementType.Ellipse;
				labelElement.BorderWidth = ellipse.StrokeThickness;
				labelElement.BorderColor = ellipse.Stroke?.ToString() ?? "#000000";
				labelElement.Content = "";
				labelElement.Width = (double.IsNaN(ellipse.Width) ? labelElement.Width : ellipse.Width);
				labelElement.Height = (double.IsNaN(ellipse.Height) ? labelElement.Height : ellipse.Height);
			}
			else
			{
				if (!(item is Border border))
				{
					Console.WriteLine("[DEBUG] Unrecognized element type: " + item.GetType().Name + ", skipping");
					continue;
				}
				string contents10 = "[DEBUG] Found Border element\n";
				File.AppendAllText("debug.log", contents10);
				Console.WriteLine("[DEBUG] Found Border element");
				string contents11 = "[DEBUG] Border.Child type: " + (border.Child?.GetType().Name ?? "null") + "\n";
				File.AppendAllText("debug.log", contents11);
				Console.WriteLine("[DEBUG] Border.Child type: " + (border.Child?.GetType().Name ?? "null"));
				if (!(border.Child is TextBlock textBlock2))
				{
					string contents12 = "[DEBUG] Border with no TextBlock, skipping\n";
					File.AppendAllText("debug.log", contents12);
					Console.WriteLine("[DEBUG] Border with no TextBlock, skipping");
					continue;
				}
				object tag = border.Tag;
				if (tag != null && tag.ToString()?.StartsWith("LinkedImage:") == true)
				{
					string contents13 = $"[DEBUG] Border is LinkedImage placeholder with tag: {border.Tag}\n";
					File.AppendAllText("debug.log", contents13);
					Console.WriteLine($"[DEBUG] Border is LinkedImage placeholder with tag: {border.Tag}");
					labelElement.Type = LabelElementType.LinkedImage;
					string text3 = border.Tag.ToString();
					labelElement.Content = text3.Substring("LinkedImage:".Length);
					File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: CreateLiveTemplate - Found LinkedImage Border placeholder with URL: {labelElement.Content}\n");
				}
				else
				{
					string contents14 = "[DEBUG] Border contains TextBlock: '" + textBlock2.Text + "'\n";
					File.AppendAllText("debug.log", contents14);
					Console.WriteLine("[DEBUG] Border contains TextBlock: '" + textBlock2.Text + "'");
					labelElement.Type = LabelElementType.Text;
					labelElement.Content = textBlock2.Text;
					labelElement.FontSize = textBlock2.FontSize;
					labelElement.FontFamily = textBlock2.FontFamily.ToString();
					labelElement.FontWeight = textBlock2.FontWeight.ToString();
					labelElement.FontStyle = textBlock2.FontStyle.ToString();
					labelElement.Foreground = textBlock2.Foreground.ToString();
					labelElement.TextDecorations = GetTextDecorationsString(textBlock2.TextDecorations);
					LabelElement labelElement2 = labelElement;
					labelElement2.TextAlignment = textBlock2.TextAlignment switch
					{
						TextAlignment.Center => "Center", 
						TextAlignment.Right => "Right", 
						TextAlignment.Left => "Left", 
						_ => "Left", 
					};
					labelElement2 = labelElement;
					labelElement2.VerticalAlignment = textBlock2.VerticalAlignment switch
					{
						VerticalAlignment.Center => "Center", 
						VerticalAlignment.Bottom => "Bottom", 
						VerticalAlignment.Top => "Top", 
						_ => "Top", 
					};
					string contents15 = $"[DEBUG] SAVING: Text='{textBlock2.Text}', FontSize={textBlock2.FontSize}, FontFamily='{textBlock2.FontFamily}', Color='{textBlock2.Foreground}', TextAlign='{labelElement.TextAlignment}', VerticalAlign='{labelElement.VerticalAlignment}'\n";
					File.AppendAllText("debug.log", contents15);
					Console.WriteLine($"[DEBUG] SAVING: Text='{textBlock2.Text}', FontSize={textBlock2.FontSize}, FontFamily='{textBlock2.FontFamily}', Color='{textBlock2.Foreground}', TextAlign='{labelElement.TextAlignment}', VerticalAlign='{labelElement.VerticalAlignment}'");
				}
			}
			if (item.RenderTransform is RotateTransform rotateTransform)
			{
				labelElement.Rotation = rotateTransform.Angle;
			}
			else
			{
				labelElement.Rotation = 0.0;
			}
			string contents16 = $"[DEBUG] Created label element: Type={labelElement.Type}, Size={labelElement.Width}x{labelElement.Height}\n";
			File.AppendAllText("debug.log", contents16);
			Console.WriteLine($"[DEBUG] Created label element: Type={labelElement.Type}, Size={labelElement.Width}x{labelElement.Height}");
			labelTemplate.Elements.Add(labelElement);
		}
		string contents17 = $"[DEBUG] Final live template has {labelTemplate.Elements.Count} elements\n";
		File.AppendAllText("debug.log", contents17);
		Console.WriteLine($"[DEBUG] Final live template has {labelTemplate.Elements.Count} elements");
		labelTemplate.FieldMappings = CurrentTemplate.FieldMappings ?? new List<FieldMapping>();
		return labelTemplate;
	}

	private List<UIElement> GetLabelElements()
	{
		List<UIElement> list = new List<UIElement>();
		foreach (UIElement child in LabelCanvas.Children)
		{
			if (!(child is Thumb))
			{
				list.Add(child);
			}
		}
		return list;
	}

	private string GetTextDecorationsString(TextDecorationCollection? decorations)
	{
		if (decorations == null || decorations.Count == 0)
		{
			return "";
		}
		if (decorations == TextDecorations.Underline)
		{
			return "Underline";
		}
		if (decorations == TextDecorations.Strikethrough)
		{
			return "Strikethrough";
		}
		if (decorations == TextDecorations.OverLine)
		{
			return "Overline";
		}
		if (decorations == TextDecorations.Baseline)
		{
			return "Baseline";
		}
		using (TextDecorationCollection.Enumerator enumerator = decorations.GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				switch (enumerator.Current.Location)
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
		}
		return "";
	}

	private UIElement? CreateElementFromTemplate(LabelElement element)
	{
		UIElement uIElement = null;
		string contents = $"[DEBUG] LOADING: Text='{element.Content}', FontSize={element.FontSize}, FontFamily='{element.FontFamily}', Color='{element.Foreground}'\n";
		File.AppendAllText("debug.log", contents);
		Console.WriteLine($"[DEBUG] LOADING: Text='{element.Content}', FontSize={element.FontSize}, FontFamily='{element.FontFamily}', Color='{element.Foreground}'");
		switch (element.Type)
		{
		case LabelElementType.Text:
		{
			TextBlock textBlock = new TextBlock
			{
				Text = (element.Content ?? "Text"),
				FontSize = ((element.FontSize > 0.0) ? element.FontSize : 12.0),
				Width = element.Width,
				Height = element.Height
			};
			if (!string.IsNullOrEmpty(element.FontFamily))
			{
				textBlock.FontFamily = new System.Windows.Media.FontFamily(element.FontFamily);
			}
			if (!string.IsNullOrEmpty(element.FontWeight))
			{
				textBlock.FontWeight = (element.FontWeight.Contains("Bold") ? FontWeights.Bold : FontWeights.Normal);
			}
			if (!string.IsNullOrEmpty(element.FontStyle))
			{
				textBlock.FontStyle = (element.FontStyle.Contains("Italic") ? FontStyles.Italic : FontStyles.Normal);
			}
			if (!string.IsNullOrEmpty(element.Foreground))
			{
				try
				{
					System.Windows.Media.Brush foreground = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(element.Foreground);
					textBlock.Foreground = foreground;
				}
				catch
				{
					textBlock.Foreground = System.Windows.Media.Brushes.Black;
				}
			}
			if (!string.IsNullOrEmpty(element.TextAlignment))
			{
				string text = element.TextAlignment.ToLower();
				if (!(text == "center"))
				{
					if (text == "right")
					{
						textBlock.TextAlignment = TextAlignment.Right;
					}
					else
					{
						textBlock.TextAlignment = TextAlignment.Left;
					}
				}
				else
				{
					textBlock.TextAlignment = TextAlignment.Center;
				}
			}
			if (!string.IsNullOrEmpty(element.VerticalAlignment))
			{
				string text = element.VerticalAlignment.ToLower();
				if (!(text == "center"))
				{
					if (text == "bottom")
					{
						textBlock.VerticalAlignment = VerticalAlignment.Bottom;
					}
					else
					{
						textBlock.VerticalAlignment = VerticalAlignment.Top;
					}
				}
				else
				{
					textBlock.VerticalAlignment = VerticalAlignment.Center;
				}
			}
			uIElement = new Border
			{
				Child = textBlock,
				BorderBrush = System.Windows.Media.Brushes.LightGray,
				BorderThickness = new Thickness(1.0),
				Background = System.Windows.Media.Brushes.Transparent,
				Width = element.Width,
				Height = element.Height
			};
			break;
		}
		case LabelElementType.Image:
			try
			{
				BitmapImage bitmapImage = null;
				if (!string.IsNullOrEmpty(element.ImageData))
				{
					byte[] buffer = Convert.FromBase64String(element.ImageData);
					bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.StreamSource = new MemoryStream(buffer);
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
				}
				else if (!string.IsNullOrEmpty(element.Content) && element.Content.StartsWith("data:image"))
				{
					byte[] buffer2 = Convert.FromBase64String(element.Content.Split(',')[1]);
					bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.StreamSource = new MemoryStream(buffer2);
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
				}
				else if (!string.IsNullOrEmpty(element.Content) && File.Exists(element.Content))
				{
					bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.UriSource = new Uri(element.Content);
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
				}
				uIElement = ((bitmapImage == null) ? ((FrameworkElement)new System.Windows.Shapes.Rectangle
				{
					Width = element.Width,
					Height = element.Height,
					Fill = System.Windows.Media.Brushes.LightGray,
					Stroke = System.Windows.Media.Brushes.DarkGray,
					StrokeThickness = 2.0,
					Tag = (element.Content ?? "No Image Data")
				}) : ((FrameworkElement)new System.Windows.Controls.Image
				{
					Source = bitmapImage,
					Width = element.Width,
					Height = element.Height,
					Stretch = Stretch.Uniform,
					Tag = ((!string.IsNullOrEmpty(element.ImageData)) ? ("ImageData:" + element.ImageData) : element.Content)
				}));
			}
			catch (Exception ex)
			{
				uIElement = new System.Windows.Shapes.Rectangle
				{
					Width = element.Width,
					Height = element.Height,
					Fill = System.Windows.Media.Brushes.LightGray,
					Stroke = System.Windows.Media.Brushes.DarkGray,
					StrokeThickness = 2.0,
					Tag = "Image Load Error: " + ex.Message
				};
			}
			break;
		case LabelElementType.LinkedImage:
		{
			Border border = new Border
			{
				Width = element.Width,
				Height = element.Height,
				BorderBrush = System.Windows.Media.Brushes.Blue,
				BorderThickness = new Thickness(2.0),
				Background = System.Windows.Media.Brushes.LightBlue,
				Tag = "LinkedImage:" + element.Content
			};
			if (!string.IsNullOrEmpty(element.Content))
			{
				LoadLinkedImageFromUrl(border, element.Content);
			}
			else
			{
				border.Tag = "LinkedImage:";
				border.Child = new TextBlock
				{
					Text = "LinkedImage\n[URL]",
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					TextAlignment = TextAlignment.Center,
					FontSize = 10.0,
					Foreground = System.Windows.Media.Brushes.DarkBlue
				};
			}
			uIElement = border;
			break;
		}
		case LabelElementType.Line:
		{
			Canvas canvas = new Canvas
			{
				Width = element.Width,
				Height = element.Height,
				Background = System.Windows.Media.Brushes.Transparent
			};
			Line line = new Line
			{
				X1 = 0.0,
				Y1 = element.Height / 2.0,
				X2 = element.Width,
				Y2 = element.Height / 2.0,
				StrokeThickness = ((element.BorderWidth > 0.0) ? element.BorderWidth : 1.0)
			};
			try
			{
				if (!string.IsNullOrEmpty(element.BorderColor))
				{
					line.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.BorderColor) ?? System.Windows.Media.Brushes.Black);
				}
				else
				{
					line.Stroke = System.Windows.Media.Brushes.Black;
				}
			}
			catch
			{
				line.Stroke = System.Windows.Media.Brushes.Black;
			}
			canvas.Children.Add(line);
			uIElement = canvas;
			break;
		}
		case LabelElementType.Rectangle:
		{
			System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle
			{
				Width = element.Width,
				Height = element.Height,
				StrokeThickness = ((element.BorderWidth > 0.0) ? element.BorderWidth : 1.0),
				Fill = System.Windows.Media.Brushes.Transparent
			};
			try
			{
				if (!string.IsNullOrEmpty(element.BorderColor))
				{
					rectangle.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.BorderColor) ?? System.Windows.Media.Brushes.Black);
				}
				else
				{
					rectangle.Stroke = System.Windows.Media.Brushes.Black;
				}
			}
			catch
			{
				rectangle.Stroke = System.Windows.Media.Brushes.Black;
			}
			uIElement = rectangle;
			break;
		}
		case LabelElementType.QRCode:
			uIElement = CreateBarcodeImage("QRCODE", element.Content ?? "Sample QR Data", element.Width, element.Height);
			break;
		case LabelElementType.EAN13:
		case LabelElementType.Barcode13:
			uIElement = CreateBarcodeImage("EAN13", element.Content ?? "4000000000004", element.Width, element.Height);
			break;
		case LabelElementType.EAN8:
		case LabelElementType.Barcode8:
			uIElement = CreateBarcodeImage("EAN8", element.Content ?? "40000007", element.Width, element.Height);
			break;
		case LabelElementType.UPCA:
			uIElement = CreateBarcodeImage("UPCA", element.Content ?? "012345678905", element.Width, element.Height);
			break;
		case LabelElementType.UPCE:
			uIElement = CreateBarcodeImage("UPCE", element.Content ?? "01234565", element.Width, element.Height);
			break;
		case LabelElementType.Code128:
			uIElement = CreateBarcodeImage("CODE128", element.Content ?? "HELLO123", element.Width, element.Height);
			break;
		case LabelElementType.Code39:
			uIElement = CreateBarcodeImage("CODE39", element.Content ?? "HELLO39", element.Width, element.Height);
			break;
		case LabelElementType.Aztec:
			uIElement = CreateBarcodeImage("AZTEC", element.Content ?? "Sample Aztec Data", element.Width, element.Height);
			break;
		case LabelElementType.DataMatrix:
			uIElement = CreateBarcodeImage("DATAMATRIX", element.Content ?? "Sample Data Matrix", element.Width, element.Height);
			break;
		case LabelElementType.GS1DataBar:
			uIElement = CreateBarcodeImage("GS1DATABAR", element.Content ?? "]C101234567890128", element.Width, element.Height);
			break;
		case LabelElementType.PDF417:
			uIElement = CreateBarcodeImage("PDF417", element.Content ?? "Sample PDF417 Data", element.Width, element.Height);
			break;
		case LabelElementType.Barcode:
			uIElement = CreateBarcodeImage("CODE128", element.Content ?? "LEGACY", element.Width, element.Height);
			break;
		}
		if (uIElement != null && element.Rotation != 0.0)
		{
			RotateTransform renderTransform = new RotateTransform(element.Rotation);
			if (uIElement is FrameworkElement frameworkElement)
			{
				frameworkElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
			}
			uIElement.RenderTransform = renderTransform;
			elementRotations[uIElement] = element.Rotation;
		}
		return uIElement;
	}

	private void MainWindow_Closing(object? sender, CancelEventArgs e)
	{
		if (_appConfig.Mode == "flow" && !_isExplicitExit && !ApplicationIsExiting)
		{
			e.Cancel = true;
			Hide();
			return;
		}
		if (_isExplicitExit)
		{
			CancelInkjetRunIfNeeded();
			return;
		}
		if (_inkjetRunInProgress)
		{
			if (MessageBox.Show("A printer queue load is in progress. Cancel it and close?", "Queue Load In Progress", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
			{
				e.Cancel = true;
				return;
			}
			CancelInkjetRunIfNeeded();
		}
		if (!PromptSaveChanges())
		{
			e.Cancel = true;
		}
	}

	private void InitializeFlowModeBehavior()
	{
		try
		{
			File.AppendAllText("mainwindow-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] InitializeFlowModeBehavior called for window: {base.Title}{Environment.NewLine}");
		}
		catch
		{
		}
		base.Title = "TaggJack - Flow Mode";
		try
		{
			File.AppendAllText("mainwindow-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Title changed to: {base.Title}{Environment.NewLine}");
		}
		catch
		{
		}
	}

	public void ExplicitExit()
	{
		if (PromptSaveChanges())
		{
			ApplicationIsExiting = true;
			_isExplicitExit = true;
			if (Application.Current.MainWindow is MainWindow mainWindow && mainWindow != this)
			{
				mainWindow.PrepareForExplicitExit();
			}
			Application.Current.Shutdown();
		}
	}

	internal void PrepareForExplicitExit()
	{
		_isExplicitExit = true;
	}

	private bool PromptSaveChanges()
	{
		if (!_isDirty)
		{
			return true;
		}
		switch (DialogHelper.ShowMessageBox("You have unsaved changes. Do you want to save them before continuing?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, base.IsVisible ? this : null))
		{
		case MessageBoxResult.Yes:
			SaveTemplate_Click(this, new RoutedEventArgs());
			return !_isDirty;
		case MessageBoxResult.No:
			return true;
		default:
			return false;
		}
	}

	private void New_Click(object sender, RoutedEventArgs e)
	{
		if (PromptSaveChanges())
		{
			LabelCanvas.Children.Clear();
			RemoveResizingHandles();
			selectedElement = null;
			selectedElements.Clear();
			CurrentTemplate = new LabelTemplate();
			_currentFilePath = null;
			InitializeDefaultTemplate();
			lastResultTable = null;
			_gs1SampleActive = false;
			_gs1SampleLevel = 0;
			_currentDataSource = null;
			ApplyTemplateTypeUi(TemplateType.Standard);
			ElementPropertiesPanel.Children.Clear();
			ElementPropertiesPanel.Children.Add(new TextBlock
			{
				Text = "Select an element to view its properties",
				Foreground = System.Windows.Media.Brushes.Gray,
				FontStyle = FontStyles.Italic,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
			});
			SetDirty(isDirty: false);
		}
	}

	private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			CurrentTemplate.TemplateName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath ?? "Untitled Template");
			if (CurrentTemplate.TemplateType.IsInkjetLike())
			{
				CurrentTemplate.Elements.Clear();
				CurrentTemplate.FieldMappings.Clear();
				CurrentTemplate.InkjetConfig = InkjetPanel.ToConfig();
			}
			else
			{
				CurrentTemplate.InkjetConfig = null;
				CurrentTemplate.Elements.Clear();
				foreach (UIElement child in LabelCanvas.Children)
				{
					if (!(child is Thumb))
					{
						LabelElement labelElement = new LabelElement
						{
							X = Canvas.GetLeft(child),
							Y = Canvas.GetTop(child),
							Width = ((child is FrameworkElement frameworkElement) ? frameworkElement.Width : 100.0),
							Height = ((child is FrameworkElement frameworkElement2) ? frameworkElement2.Height : 30.0),
							Rotation = (elementRotations.ContainsKey(child) ? elementRotations[child] : ((child.RenderTransform is RotateTransform rotateTransform) ? rotateTransform.Angle : 0.0))
						};
						if (child is TextBlock textBlock)
						{
							labelElement.Type = LabelElementType.Text;
							labelElement.Content = textBlock.Text;
							labelElement.FontSize = textBlock.FontSize;
							labelElement.FontFamily = textBlock.FontFamily.ToString();
							labelElement.FontWeight = textBlock.FontWeight.ToString();
							labelElement.FontStyle = textBlock.FontStyle.ToString();
							labelElement.Foreground = textBlock.Foreground.ToString();
							labelElement.TextDecorations = GetTextDecorationsString(textBlock.TextDecorations);
						}
						CurrentTemplate.Elements.Add(labelElement);
					}
				}
			}
			if (_currentDataSource != null)
			{
				CurrentTemplate.DataSource = _currentDataSource;
			}
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Label Template (*.xml)|*.xml",
				DefaultExt = "xml",
				FileName = "MyTemplate"
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				XmlSerializer xmlSerializer = new XmlSerializer(typeof(LabelTemplate));
				using FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create);
				xmlSerializer.Serialize(stream, CurrentTemplate);
				_currentFilePath = saveFileDialog.FileName;
				SetDirty(isDirty: false);
				MessageBox.Show("Template saved successfully to:\n" + saveFileDialog.FileName, "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error saving template:\n" + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void MapFields_Click(object sender, RoutedEventArgs e)
	{
		if (lastResultTable == null || LabelCanvas.Children.Count == 0)
		{
			MessageBox.Show(FeatureFlags.IsGs1Enabled(_appConfig) ? "Please load GS1 sample data, fetch data, and add label elements to the canvas first." : "Please fetch data and add label elements to the canvas first.", "No Data or Elements", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		List<UIElement> labelElements = GetLabelElements();
		if (labelElements.Count == 0)
		{
			MessageBox.Show("No label elements found on the canvas.", "No Elements", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		FieldMappingWindow fieldMappingWindow = new FieldMappingWindow(labelElements, lastResultTable);
		if (fieldMappingWindow.ShowDialog() == true)
		{
			CurrentTemplate.FieldMappings = fieldMappingWindow.Mappings;
		}
	}

	private void Gs1SampleData_Click(object sender, RoutedEventArgs e)
	{
		_appConfig = ConfigService.LoadConfig();
		if (FeatureFlags.IsGs1Enabled(_appConfig))
		{
			Gs1SampleDataWindow gs1SampleDataWindow = new Gs1SampleDataWindow(_appConfig)
			{
				Owner = this
			};
			if (gs1SampleDataWindow.ShowDialog() == true)
			{
				lastResultTable = gs1SampleDataWindow.SampleTable;
				_gs1SampleActive = true;
				_gs1SampleLevel = gs1SampleDataWindow.Level;
				UpdateDataStatus();
			}
		}
	}

	private void SampleData_Click(object sender, RoutedEventArgs e)
	{
		if (CurrentTemplate?.DataSource == null || string.IsNullOrWhiteSpace(CurrentTemplate.DataSource.Query))
		{
			MessageBox.Show("No data source configured.\n\nPlease setup a data source first via Data â†’ Setup Data Source.", "No Data Source", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		SampleDataWindow sampleDataWindow = new SampleDataWindow(CurrentTemplate);
		sampleDataWindow.Owner = this;
		sampleDataWindow.ShowDialog();
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		ExplicitExit();
	}

	private async void RefreshLicense_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			await PortalLicenseService.Instance.CheckWithPortalAsync();
			RefreshLicenseBanner();
			LicenseEvaluation eval = PortalLicenseService.Instance.GetStatus();
			MessageBox.Show(
				$"Status: {eval.Status}\nExpires: {eval.LicenseExpiresAt ?? "—"}\n{eval.Message}",
				"License refreshed",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Could not refresh license:\n" + ex.Message, "License", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void About_Click(object sender, RoutedEventArgs e)
	{
		AboutWindow aboutWindow = new AboutWindow();
		aboutWindow.Owner = this;
		aboutWindow.ShowDialog();
	}

	private async void RefreshData_Click(object sender, RoutedEventArgs e)
	{
		if (_currentDataSource == null)
		{
			MessageBox.Show("No data source configured. Please setup a data source first.", "No Data Source", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		TextBlock dataStatusTextBlock = FindName("DataStatusTextBlock") as TextBlock;
		if (dataStatusTextBlock != null)
		{
			dataStatusTextBlock.Text = "Refreshing data...";
			dataStatusTextBlock.FontStyle = FontStyles.Italic;
		}
		Button refreshButton = sender as Button;
		if (refreshButton != null)
		{
			refreshButton.IsEnabled = false;
		}
		try
		{
			await Task.Delay(100);
			await Task.Run(delegate
			{
				string connectionString = $"Server={_currentDataSource.Host};Port={_currentDataSource.Port};Database={_currentDataSource.Database};Uid={_currentDataSource.Username};Pwd={_currentDataSource.Password};SslMode=None;AllowPublicKeyRetrieval=True;";
				MySqlService mySqlService = new MySqlService
				{
					ConnectionString = connectionString
				};
				lastResultTable = mySqlService.ExecuteQuery(_currentDataSource.Query);
				if (CurrentTemplate.TemplateType.IsInkjetLike())
				{
					InkjetPanel.SetAvailableFields(lastResultTable);
				}
				_gs1SampleActive = false;
				_gs1SampleLevel = 0;
			});
			UpdateDataStatus();
			if (lastResultTable != null)
			{
				MessageBox.Show($"Data refreshed successfully! Fetched {lastResultTable.Rows.Count} rows with {lastResultTable.Columns.Count} columns.", "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			else
			{
				MessageBox.Show("Data refreshed but no data returned.", "Refresh Complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			SetDirty();
		}
		catch (Exception ex)
		{
			if (dataStatusTextBlock != null)
			{
				dataStatusTextBlock.Text = "Refresh failed";
				dataStatusTextBlock.FontStyle = FontStyles.Italic;
			}
			MessageBox.Show("Failed to refresh data: " + ex.Message, "Refresh Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			if (refreshButton != null)
			{
				refreshButton.IsEnabled = true;
			}
		}
	}

	private void UpdateDataStatus()
	{
		if (FindName("DataStatusTextBlock") is TextBlock textBlock)
		{
			if (lastResultTable != null && lastResultTable.Rows.Count > 0)
			{
				string value = ((_gs1SampleActive && _gs1SampleLevel > 0) ? $"GS1 L{_gs1SampleLevel} Â· " : "");
				textBlock.Text = $"{value}{lastResultTable.Rows.Count} rows, {lastResultTable.Columns.Count} fields";
				textBlock.FontStyle = FontStyles.Normal;
			}
			else
			{
				textBlock.Text = "No data";
				textBlock.FontStyle = FontStyles.Italic;
			}
		}
	}

	private void ResizeButton_Click(object sender, RoutedEventArgs e)
	{
		LabelResizeWindow labelResizeWindow = new LabelResizeWindow(_labelWidthMm, _labelHeightMm);
		if (labelResizeWindow.ShowDialog() != true)
		{
			return;
		}
		double labelWidthMm = _labelWidthMm;
		double labelHeightMm = _labelHeightMm;
		_labelWidthMm = labelResizeWindow.NewWidth;
		_labelHeightMm = labelResizeWindow.NewHeight;
		CurrentTemplate.LabelWidth = _labelWidthMm;
		CurrentTemplate.LabelHeight = _labelHeightMm;
		double num = _labelWidthMm / labelWidthMm;
		double num2 = _labelHeightMm / labelHeightMm;
		foreach (UIElement item in LabelCanvas.Children.OfType<UIElement>().ToList())
		{
			if (item == null)
			{
				continue;
			}
			switch (labelResizeWindow.SelectedResizeMode)
			{
			case LabelResizeMode.ResizeElements:
				if (item is FrameworkElement frameworkElement)
				{
					Canvas.SetLeft(item, Canvas.GetLeft(item) * num);
					Canvas.SetTop(item, Canvas.GetTop(item) * num2);
					frameworkElement.Width *= num;
					frameworkElement.Height *= num2;
					if (frameworkElement is TextBlock textBlock)
					{
						textBlock.FontSize *= Math.Min(num, num2);
					}
				}
				break;
			case LabelResizeMode.RepositionElements:
				Canvas.SetLeft(item, Canvas.GetLeft(item) * num);
				Canvas.SetTop(item, Canvas.GetTop(item) * num2);
				break;
			}
		}
		UpdateCanvasSize();
		UpdateMarginGuidelines();
		UpdateGrid();
		LabelWidthDisplay.Text = $"{_labelWidthMm:F1} mm";
		LabelHeightDisplay.Text = $"{_labelHeightMm:F1} mm";
		SetDirty();
	}

	private void AlignLeft_Click(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("[DEBUG ALIGN BUTTON] AlignLeft_Click called");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN BUTTON] AlignLeft_Click called\n");
		SetTextAlignment("Left", null);
	}

	private void AlignCenter_Click(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("[DEBUG ALIGN BUTTON] AlignCenter_Click called");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN BUTTON] AlignCenter_Click called\n");
		SetTextAlignment("Center", null);
	}

	private void AlignRight_Click(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("[DEBUG ALIGN BUTTON] AlignRight_Click called");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN BUTTON] AlignRight_Click called\n");
		SetTextAlignment("Right", null);
	}

	private void AlignTop_Click(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("[DEBUG ALIGN BUTTON] AlignTop_Click called");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN BUTTON] AlignTop_Click called\n");
		SetTextAlignment(null, "Top");
	}

	private void AlignMiddle_Click(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("[DEBUG ALIGN BUTTON] AlignMiddle_Click called");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN BUTTON] AlignMiddle_Click called\n");
		SetTextAlignment(null, "Center");
	}

	private void AlignBottom_Click(object sender, RoutedEventArgs e)
	{
		Console.WriteLine("[DEBUG ALIGN BUTTON] AlignBottom_Click called");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN BUTTON] AlignBottom_Click called\n");
		SetTextAlignment(null, "Bottom");
	}

	private void SetTextAlignment(string? horizontalAlign, string? verticalAlign)
	{
		Console.WriteLine("[DEBUG ALIGN] SetTextAlignment called: H=" + horizontalAlign + ", V=" + verticalAlign);
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN] SetTextAlignment called: H={horizontalAlign}, V={verticalAlign}\n");
		List<UIElement> list = new List<UIElement>();
		if (selectedElements.Count > 0)
		{
			list.AddRange(selectedElements);
		}
		else if (selectedElement != null)
		{
			list.Add(selectedElement);
		}
		Console.WriteLine($"[DEBUG ALIGN] Elements to align: {list.Count}");
		File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN] Elements to align: {list.Count}\n");
		if (list.Count == 0)
		{
			return;
		}
		foreach (UIElement element in list)
		{
			if (!(element is Border { Child: TextBlock child } border))
			{
				continue;
			}
			Console.WriteLine("[DEBUG ALIGN] Found Border with TextBlock, current text: '" + child.Text + "'");
			File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN] Found Border with TextBlock, current text: '{child.Text}'\n");
			if (horizontalAlign != null)
			{
				string text = horizontalAlign.ToLower();
				if (!(text == "center"))
				{
					if (text == "right")
					{
						child.TextAlignment = TextAlignment.Right;
						Console.WriteLine("[DEBUG ALIGN] Set TextAlignment.Right");
						File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN] Set TextAlignment.Right\n");
					}
					else
					{
						child.TextAlignment = TextAlignment.Left;
						Console.WriteLine("[DEBUG ALIGN] Set TextAlignment.Left");
						File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN] Set TextAlignment.Left\n");
					}
				}
				else
				{
					child.TextAlignment = TextAlignment.Center;
					Console.WriteLine("[DEBUG ALIGN] Set TextAlignment.Center");
					File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG ALIGN] Set TextAlignment.Center\n");
				}
				string[] array = ((border.Tag as string) ?? "").Split('|');
				if (array.Length >= 2)
				{
					array[0] = horizontalAlign;
					border.Tag = string.Join("|", array);
				}
				else
				{
					border.Tag = horizontalAlign + "|" + (array.ElementAtOrDefault(1) ?? "Top");
				}
			}
			if (verticalAlign != null)
			{
				string text = verticalAlign.ToLower();
				if (!(text == "center"))
				{
					if (text == "bottom")
					{
						child.VerticalAlignment = VerticalAlignment.Bottom;
					}
					else
					{
						child.VerticalAlignment = VerticalAlignment.Top;
					}
				}
				else
				{
					child.VerticalAlignment = VerticalAlignment.Center;
				}
				string[] array2 = ((border.Tag as string) ?? "").Split('|');
				if (array2.Length >= 2)
				{
					array2[1] = verticalAlign;
					border.Tag = string.Join("|", array2);
				}
				else
				{
					border.Tag = (array2.ElementAtOrDefault(0) ?? "Left") + "|" + verticalAlign;
				}
			}
			LabelElement labelElement = CurrentTemplate?.Elements?.FirstOrDefault((LabelElement e) => e.Type == LabelElementType.Text && Math.Abs(e.X - Canvas.GetLeft(element)) < 1.0 && Math.Abs(e.Y - Canvas.GetTop(element)) < 1.0);
			if (labelElement != null)
			{
				if (horizontalAlign != null)
				{
					labelElement.TextAlignment = horizontalAlign;
				}
				if (verticalAlign != null)
				{
					labelElement.VerticalAlignment = verticalAlign;
				}
			}
			SetDirty();
			if (list.Count == 1)
			{
				ShowProperties(element);
			}
		}
	}

	private void ShowGrid_Click(object sender, RoutedEventArgs e)
	{
		if (GridCanvas != null)
		{
			GridCanvas.Visibility = ((GridCanvas.Visibility == Visibility.Visible) ? Visibility.Hidden : Visibility.Visible);
		}
	}

	private void MenuElement_Click(object sender, RoutedEventArgs e)
	{
		if (isDragInProgress)
		{
			File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: MenuElement_Click blocked during drag operation\n");
		}
		else if (sender is MenuItem { Tag: string tag })
		{
			System.Windows.Point position = Mouse.GetPosition(LabelCanvas);
			CreateElementByType(tag, position.X, position.Y);
		}
	}

	private void CreateElementByType(string elementType, double x, double y)
	{
		try
		{
			File.AppendAllText("linkedimage-debug.log", "CreateElementByType called with: '" + elementType + "'\n");
			TimeSpan timeSpan = DateTime.Now - lastElementCreationTime;
			double num = Math.Sqrt(Math.Pow(x - lastElementPosition.X, 2.0) + Math.Pow(y - lastElementPosition.Y, 2.0));
			if (timeSpan.TotalMilliseconds < 200.0 && num < 10.0 && elementType == lastElementType)
			{
				File.AppendAllText("linkedimage-debug.log", $"{DateTime.Now}: Duplicate element creation prevented in CreateElementByType - {elementType} at ({x}, {y})\n");
				return;
			}
			lastElementCreationTime = DateTime.Now;
			lastElementType = elementType;
			lastElementPosition = new System.Windows.Point(x, y);
			UIElement uIElement = null;
			switch (elementType)
			{
			case "Text":
			{
				TextBlock child = new TextBlock
				{
					Text = "Sample Text",
					FontSize = 12.0,
					Width = 100.0,
					Height = 20.0
				};
				uIElement = new Border
				{
					Child = child,
					BorderBrush = System.Windows.Media.Brushes.LightGray,
					BorderThickness = new Thickness(1.0),
					Background = System.Windows.Media.Brushes.Transparent,
					Width = 100.0,
					Height = 20.0
				};
				break;
			}
			case "LinkedImage":
				File.AppendAllText("linkedimage-debug.log", "LinkedImage case matched! Creating element...\n");
				uIElement = new Border
				{
					Width = 100.0,
					Height = 75.0,
					BorderBrush = System.Windows.Media.Brushes.Blue,
					BorderThickness = new Thickness(2.0),
					Background = System.Windows.Media.Brushes.LightBlue,
					Child = new TextBlock
					{
						Text = "LinkedImage\n[URL]",
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						TextAlignment = TextAlignment.Center,
						FontSize = 10.0,
						Foreground = System.Windows.Media.Brushes.DarkBlue
					},
					Tag = "LinkedImage:"
				};
				break;
			case "Line":
			{
				Line element2 = new Line
				{
					X1 = 0.0,
					Y1 = 0.0,
					X2 = 100.0,
					Y2 = 0.0,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 2.0
				};
				uIElement = new Canvas
				{
					Width = 100.0,
					Height = 2.0,
					Background = System.Windows.Media.Brushes.Transparent,
					Children = { (UIElement)element2 }
				};
				break;
			}
			case "Rectangle":
				uIElement = new System.Windows.Shapes.Rectangle
				{
					Width = 100.0,
					Height = 75.0,
					Fill = System.Windows.Media.Brushes.LightGray,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 2.0
				};
				break;
			case "Ellipse":
				uIElement = new Ellipse
				{
					Width = 100.0,
					Height = 75.0,
					Fill = System.Windows.Media.Brushes.LightGray,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 2.0
				};
				break;
			default:
			{
				uIElement = new System.Windows.Shapes.Rectangle
				{
					Width = 150.0,
					Height = 50.0,
					Fill = System.Windows.Media.Brushes.White,
					Stroke = System.Windows.Media.Brushes.Black,
					StrokeThickness = 1.0
				};
				TextBlock element = new TextBlock
				{
					Text = elementType,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				uIElement = new Grid
				{
					Width = 150.0,
					Height = 50.0,
					Children = 
					{
						uIElement,
						(UIElement)element
					}
				};
				break;
			}
			}
			if (uIElement != null)
			{
				uIElement.MouseLeftButtonDown += Element_MouseLeftButtonDown;
				Canvas.SetLeft(uIElement, x);
				Canvas.SetTop(uIElement, y);
				AddElementCommand command = new AddElementCommand(LabelCanvas, uIElement, x, y, "Add " + elementType);
				commandHistory.ExecuteCommand(command);
				File.AppendAllText("linkedimage-debug.log", $"Successfully added {elementType} to canvas at {x},{y}\n");
				SetDirty();
			}
		}
		catch (Exception ex)
		{
			File.AppendAllText("linkedimage-debug.log", "Error in CreateElementByType: " + ex.Message + "\n");
			MessageBox.Show("Error creating " + elementType + ": " + ex.Message);
		}
	}

	private Button CreateColorPicker(System.Windows.Media.Color initialColor, Action<System.Windows.Media.Color> onColorChanged)
	{
		Button button = new Button
		{
			Width = 80.0,
			Height = 25.0,
			Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
			Content = "Color",
			Background = new SolidColorBrush(initialColor),
			BorderBrush = new SolidColorBrush(Colors.Gray),
			BorderThickness = new Thickness(1.0)
		};
		button.Click += delegate
		{
			Window colorWindow = new Window
			{
				Title = "Color Wheel Picker",
				Width = 450.0,
				Height = 400.0,
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				ResizeMode = ResizeMode.NoResize
			};
			Grid grid = new Grid
			{
				Margin = new Thickness(10.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(250.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			Canvas colorWheelCanvas = new Canvas
			{
				Width = 200.0,
				Height = 200.0,
				Background = new SolidColorBrush(Colors.White),
				Margin = new Thickness(10.0)
			};
			Grid.SetColumn(colorWheelCanvas, 0);
			Grid.SetRow(colorWheelCanvas, 0);
			System.Windows.Point wheelCenter = new System.Windows.Point(100.0, 100.0);
			int wheelRadius = 90;
			for (int i = 0; i < 360; i += 2)
			{
				for (int j = 20; j < wheelRadius; j += 2)
				{
					int num = i;
					double saturation = (double)j / (double)wheelRadius;
					double value = 1.0;
					System.Windows.Media.Color color = HsvToRgb(num, saturation, value);
					System.Windows.Shapes.Rectangle element = new System.Windows.Shapes.Rectangle
					{
						Width = 3.0,
						Height = 3.0,
						Fill = new SolidColorBrush(color)
					};
					double num2 = (double)i * Math.PI / 180.0;
					double length = wheelCenter.X + (double)j * Math.Cos(num2) - 1.5;
					double length2 = wheelCenter.Y + (double)j * Math.Sin(num2) - 1.5;
					Canvas.SetLeft(element, length);
					Canvas.SetTop(element, length2);
					colorWheelCanvas.Children.Add(element);
				}
			}
			Ellipse wheelSelector = new Ellipse
			{
				Width = 8.0,
				Height = 8.0,
				Stroke = new SolidColorBrush(Colors.White),
				StrokeThickness = 2.0,
				Fill = new SolidColorBrush(Colors.Transparent)
			};
			colorWheelCanvas.Children.Add(wheelSelector);
			HsvColor currentHsv = RgbToHsv(initialColor);
			currentHsv.V = 1.0;
			System.Windows.Media.Color currentColor = HsvToRgb(currentHsv.H, currentHsv.S, currentHsv.V);
			Action updateWheelSelector = delegate
			{
				double num3 = currentHsv.S * (double)wheelRadius;
				double num4 = currentHsv.H * Math.PI / 180.0;
				double length3 = wheelCenter.X + num3 * Math.Cos(num4) - 4.0;
				double length4 = wheelCenter.Y + num3 * Math.Sin(num4) - 4.0;
				Canvas.SetLeft(wheelSelector, length3);
				Canvas.SetTop(wheelSelector, length4);
			};
			updateWheelSelector();
			StackPanel stackPanel = new StackPanel
			{
				Margin = new Thickness(10.0)
			};
			Grid.SetColumn(stackPanel, 1);
			Grid.SetRow(stackPanel, 0);
			System.Windows.Shapes.Rectangle previewRect = new System.Windows.Shapes.Rectangle
			{
				Width = 100.0,
				Height = 60.0,
				Fill = new SolidColorBrush(currentColor),
				Stroke = new SolidColorBrush(Colors.Black),
				StrokeThickness = 1.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 20.0)
			};
			stackPanel.Children.Add(previewRect);
			Label element2 = new Label
			{
				Content = "Brightness:"
			};
			stackPanel.Children.Add(element2);
			Slider brightnessSlider = new Slider
			{
				Minimum = 0.0,
				Maximum = 1.0,
				Value = 1.0,
				Width = 120.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
			};
			stackPanel.Children.Add(brightnessSlider);
			StackPanel stackPanel2 = new StackPanel
			{
				Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
			};
			Label element3 = new Label
			{
				Content = "RGB Values:"
			};
			stackPanel2.Children.Add(element3);
			Label rLabel = new Label
			{
				Content = $"R: {currentColor.R}"
			};
			Label gLabel = new Label
			{
				Content = $"G: {currentColor.G}"
			};
			Label bLabel = new Label
			{
				Content = $"B: {currentColor.B}"
			};
			stackPanel2.Children.Add(rLabel);
			stackPanel2.Children.Add(gLabel);
			stackPanel2.Children.Add(bLabel);
			stackPanel.Children.Add(stackPanel2);
			Action updateColor = delegate
			{
				currentColor = HsvToRgb(currentHsv.H, currentHsv.S, currentHsv.V);
				previewRect.Fill = new SolidColorBrush(currentColor);
				rLabel.Content = $"R: {currentColor.R}";
				gLabel.Content = $"G: {currentColor.G}";
				bLabel.Content = $"B: {currentColor.B}";
				updateWheelSelector();
			};
			updateWheelSelector();
			brightnessSlider.ValueChanged += delegate
			{
				currentHsv.V = brightnessSlider.Value;
				updateColor();
			};
			colorWheelCanvas.MouseLeftButtonDown += delegate(object cs, MouseButtonEventArgs ce)
			{
				System.Windows.Point position = ce.GetPosition(colorWheelCanvas);
				double num3 = position.X - wheelCenter.X;
				double num4 = position.Y - wheelCenter.Y;
				double num5 = Math.Sqrt(num3 * num3 + num4 * num4);
				if (num5 <= (double)wheelRadius && num5 >= 20.0)
				{
					double num6 = Math.Atan2(num4, num3) * 180.0 / Math.PI;
					if (num6 < 0.0)
					{
						num6 += 360.0;
					}
					currentHsv.H = num6;
					currentHsv.S = Math.Min(num5 / (double)wheelRadius, 1.0);
					updateColor();
				}
			};
			grid.Children.Add(colorWheelCanvas);
			grid.Children.Add(stackPanel);
			StackPanel stackPanel3 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
			};
			Grid.SetColumnSpan(stackPanel3, 2);
			Grid.SetRow(stackPanel3, 1);
			Button button2 = new Button
			{
				Content = "OK",
				Width = 80.0,
				Height = 30.0,
				Margin = new Thickness(5.0)
			};
			button2.Click += delegate
			{
				button.Background = new SolidColorBrush(currentColor);
				onColorChanged(currentColor);
				colorWindow.Close();
			};
			Button button3 = new Button
			{
				Content = "Cancel",
				Width = 80.0,
				Height = 30.0,
				Margin = new Thickness(5.0)
			};
			button3.Click += delegate
			{
				colorWindow.Close();
			};
			stackPanel3.Children.Add(button2);
			stackPanel3.Children.Add(button3);
			grid.Children.Add(stackPanel3);
			colorWindow.Content = grid;
			colorWindow.ShowDialog();
		};
		return button;
	}

	private System.Windows.Media.Color HsvToRgb(double hue, double saturation, double value)
	{
		double num = value * saturation;
		double num2 = num * (1.0 - Math.Abs(hue / 60.0 % 2.0 - 1.0));
		double num3 = value - num;
		double num4;
		double num5;
		double num6;
		if (hue >= 0.0 && hue < 60.0)
		{
			num4 = num;
			num5 = num2;
			num6 = 0.0;
		}
		else if (hue >= 60.0 && hue < 120.0)
		{
			num4 = num2;
			num5 = num;
			num6 = 0.0;
		}
		else if (hue >= 120.0 && hue < 180.0)
		{
			num4 = 0.0;
			num5 = num;
			num6 = num2;
		}
		else if (hue >= 180.0 && hue < 240.0)
		{
			num4 = 0.0;
			num5 = num2;
			num6 = num;
		}
		else if (hue >= 240.0 && hue < 300.0)
		{
			num4 = num2;
			num5 = 0.0;
			num6 = num;
		}
		else
		{
			num4 = num;
			num5 = 0.0;
			num6 = num2;
		}
		return System.Windows.Media.Color.FromRgb((byte)Math.Round((num4 + num3) * 255.0), (byte)Math.Round((num5 + num3) * 255.0), (byte)Math.Round((num6 + num3) * 255.0));
	}

	private HsvColor RgbToHsv(System.Windows.Media.Color color)
	{
		double num = (double)(int)color.R / 255.0;
		double num2 = (double)(int)color.G / 255.0;
		double num3 = (double)(int)color.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = num4 - num5;
		HsvColor result = new HsvColor
		{
			V = num4,
			S = ((num4 == 0.0) ? 0.0 : (num6 / num4))
		};
		if (num6 == 0.0)
		{
			result.H = 0.0;
		}
		else if (num4 == num)
		{
			result.H = 60.0 * ((num2 - num3) / num6 % 6.0);
		}
		else if (num4 == num2)
		{
			result.H = 60.0 * ((num3 - num) / num6 + 2.0);
		}
		else
		{
			result.H = 60.0 * ((num - num2) / num6 + 4.0);
		}
		if (result.H < 0.0)
		{
			result.H += 360.0;
		}
		return result;
	}

	private void TemplateTypeStandard_Click(object sender, RoutedEventArgs e)
	{
		SwitchTemplateType(TemplateType.Standard);
	}

	private void TemplateTypeMarkem_Click(object sender, RoutedEventArgs e)
	{
		SwitchTemplateType(TemplateType.Inkjet);
	}

	private void TemplateTypeLinx_Click(object sender, RoutedEventArgs e)
	{
		SwitchTemplateType(TemplateType.Linx);
	}

	private void SwitchTemplateType(TemplateType newType)
	{
		if (CurrentTemplate.TemplateType == newType)
		{
			ApplyTemplateTypeUi(newType);
			return;
		}
		if (_inkjetRunInProgress)
		{
			if (MessageBox.Show("A printer queue load is in progress. Cancel it and switch template type?", "Queue Load In Progress", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
			{
				ApplyTemplateTypeUi(CurrentTemplate.TemplateType);
				return;
			}
			CancelInkjetRunIfNeeded();
		}
		bool num = CurrentTemplate.TemplateType.IsInkjetLike();
		bool flag = newType.IsInkjetLike();
		if (num && flag)
		{
			CurrentTemplate.InkjetConfig = InkjetPanel.ToConfig();
			InkjetPanel.SetVendor(newType);
			CurrentTemplate.InkjetConfig = InkjetPanel.ToConfig();
			CurrentTemplate.TemplateType = newType;
			ApplyTemplateTypeUi(newType);
			SetDirty();
			return;
		}
		string messageBoxText = ((!flag) ? "Switching to Standard will clear inkjet printer settings and position mappings. Continue?" : ("Switching to " + newType.DisplayName() + " will clear the canvas and element field mappings. Continue?"));
		if (MessageBox.Show(messageBoxText, "Change Template Type", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
		{
			ApplyTemplateTypeUi(CurrentTemplate.TemplateType);
			return;
		}
		if (flag)
		{
			LabelCanvas.Children.Clear();
			elementRotations.Clear();
			RemoveResizingHandles();
			selectedElement = null;
			selectedElements.Clear();
			CurrentTemplate.Elements.Clear();
			CurrentTemplate.FieldMappings.Clear();
			CurrentTemplate.InkjetConfig = new InkjetConfig
			{
				PrinterPort = newType.DefaultPrinterPort()
			};
			InkjetPanel.SetVendor(newType);
			InkjetPanel.LoadFromConfig(CurrentTemplate.InkjetConfig);
			InkjetPanel.SetAvailableFields(lastResultTable);
		}
		else
		{
			CurrentTemplate.InkjetConfig = null;
		}
		CurrentTemplate.TemplateType = newType;
		ApplyTemplateTypeUi(newType);
		SetDirty();
	}

	private void ApplyTemplateTypeUi(TemplateType type)
	{
		bool flag = type.IsInkjetLike();
		CurrentTemplate.TemplateType = type;
		TemplateTypeStandardMenuItem.IsChecked = type == TemplateType.Standard;
		TemplateTypeMarkemMenuItem.IsChecked = type.IsMarkemImaje();
		TemplateTypeLinxMenuItem.IsChecked = type.IsLinx();
		InkjetHostBorder.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		MarkemMenu.Visibility = ((!type.IsMarkemImaje()) ? Visibility.Collapsed : Visibility.Visible);
		LinxMenu.Visibility = ((!type.IsLinx()) ? Visibility.Collapsed : Visibility.Visible);
		StandardToolbarBorder.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		ToolboxBorder.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		RulerCanvasGrid.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		PropertiesBorder.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		ElementsMenu.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		MapFieldsMenuItem.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		PrintPreviewMenuItem.Header = (flag ? ("_Run " + type.DisplayName() + "...") : "_Print...");
		if (flag)
		{
			InkjetPanel.SetVendor(type);
			InkjetPanel.LoadFromConfig(CurrentTemplate.InkjetConfig ?? new InkjetConfig
			{
				PrinterPort = type.DefaultPrinterPort()
			});
			InkjetPanel.SetAvailableFields(lastResultTable);
			EnsureInkjetProtocolLogWindow();
		}
	}

	private InkjetConfig GetCurrentInkjetConfig()
	{
		InkjetConfig inkjetConfig = InkjetPanel.ToConfig();
		CurrentTemplate.InkjetConfig = inkjetConfig;
		return inkjetConfig;
	}

	private void EnsureInkjetProtocolLogWindow()
	{
		// A closed Window cannot be shown again — recreate after Closed clears the reference.
		if (_inkjetProtocolLogWindow == null)
		{
			InkjetProtocolLogWindow window = new InkjetProtocolLogWindow
			{
				Owner = this
			};
			window.Closed += delegate
			{
				if (_inkjetProtocolLogWindow == window)
				{
					_inkjetProtocolLogWindow = null;
				}
			};
			_inkjetProtocolLogWindow = window;
		}
		_inkjetProtocolLogWindow.SetVendorTitle(CurrentTemplate.TemplateType.DisplayName());
		if (!_inkjetProtocolLogWindow.IsVisible)
		{
			_inkjetProtocolLogWindow.Show();
		}
	}

	private void AppendInkjetProtocolLog(string text)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.Invoke(() => AppendInkjetProtocolLog(text));
			return;
		}
		EnsureInkjetProtocolLogWindow();
		_inkjetProtocolLogWindow?.Append(text);
	}

	private void CancelInkjetRunIfNeeded()
	{
		try
		{
			_inkjetRunCts?.Cancel();
		}
		catch
		{
		}
	}

	private async Task RunInkjetTemplateAsync()
	{
		if (_inkjetRunInProgress)
		{
			InkjetPanel.SetStatus("Queue load already in progress");
			return;
		}
		TemplateType vendor = CurrentTemplate.TemplateType;
		string vendorName = vendor.DisplayName();
		_inkjetRunInProgress = true;
		InkjetPanel.SetRunEnabled(enabled: false);
		_inkjetRunCts = new CancellationTokenSource();
		CancellationToken cancellationToken = _inkjetRunCts.Token;
		try
		{
			InkjetConfig config = GetCurrentInkjetConfig();
			if (_currentDataSource == null || string.IsNullOrWhiteSpace(_currentDataSource.Query))
			{
				MessageBox.Show("Configure a data source (Data â†’ Setup Data Source) before running.", vendorName + " Run", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			InkjetPanel.SetStatus("Querying data...");
			MySqlService mysqlService = new MySqlService
			{
				ConnectionString = $"Server={_currentDataSource.Host};Port={_currentDataSource.Port};Database={_currentDataSource.Database};Uid={_currentDataSource.Username};Pwd={_currentDataSource.Password};SslMode=None;AllowPublicKeyRetrieval=True;"
			};
			DataTable dataTable = (lastResultTable = await Task.Run(() => mysqlService.ExecuteQuery(_currentDataSource.Query), cancellationToken));
			InkjetPanel.SetAvailableFields(dataTable);
			if (dataTable == null || dataTable.Rows.Count == 0)
			{
				InkjetPanel.SetStatus("No records to print");
				MessageBox.Show("No records returned by the query.", vendorName + " Run", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			InkjetPanel.SetStatus($"Queue loading {dataTable.Rows.Count} record(s)...");
			EnsureInkjetProtocolLogWindow();
			InkjetOperationResult result;
			if (vendor.IsLinx())
			{
				Progress<string> progress = new Progress<string>(delegate(string line)
				{
					AppendInkjetProtocolLog(line);
					if (line.Contains("ACCEPTED", StringComparison.OrdinalIgnoreCase) || line.Contains("buffer full", StringComparison.OrdinalIgnoreCase) || line.Contains("Waiting", StringComparison.OrdinalIgnoreCase))
					{
						InkjetPanel.SetStatus(line.Trim());
					}
				});
				result = await _linxPrintService.QueueLoadAsync(config, dataTable, cancellationToken, progress);
			}
			else
			{
				result = await _inkjetPrintService.QueueLoadAsync(config, dataTable, cancellationToken);
				AppendInkjetProtocolLog(result.ProtocolLog);
			}
			InkjetPanel.SetStatus(result.Success ? result.Message : ("Failed: " + result.Message));
			if (!result.Success)
			{
				if (!cancellationToken.IsCancellationRequested)
				{
					MessageBox.Show(result.Message, vendorName + " Run Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				return;
			}
			if (!string.IsNullOrWhiteSpace(_currentDataSource.UpdateQuery))
			{
				foreach (DataRow row in dataTable.Rows)
				{
					string query = _currentDataSource.UpdateQuery;
					foreach (DataColumn column in dataTable.Columns)
					{
						query = query.Replace("{" + column.ColumnName + "}", row[column]?.ToString() ?? "");
					}
					await Task.Run(() => mysqlService.ExecuteNonQuery(query), cancellationToken);
				}
			}
			MessageBox.Show(result.Message, vendorName + " Run", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (OperationCanceledException)
		{
			InkjetPanel.SetStatus("Cancelled");
			AppendInkjetProtocolLog("Queue load cancelled.");
		}
		catch (Exception ex2)
		{
			InkjetPanel.SetStatus("Error: " + ex2.Message);
			MessageBox.Show(ex2.Message, vendorName + " Run Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			_inkjetRunInProgress = false;
			InkjetPanel.SetRunEnabled(enabled: true);
			_inkjetRunCts?.Dispose();
			_inkjetRunCts = null;
		}
	}

	private void InkjetShowProtocolLog_Click(object sender, RoutedEventArgs e)
	{
		EnsureInkjetProtocolLogWindow();
	}

	private async void InkjetPing_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.PingAsync(cfg));
	}

	private async void InkjetSelect_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.SelectJobAsync(cfg));
	}

	private async void InkjetList_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.ListJobsAsync(cfg));
	}

	private async void InkjetVars_Click(object sender, RoutedEventArgs e)
	{
		InkjetConfig currentInkjetConfig = GetCurrentInkjetConfig();
		if (currentInkjetConfig.Mappings.Count == 0)
		{
			MessageBox.Show("Add at least one position mapping first.", "Send Vars", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		List<(byte Index, string Value)> vars = new List<(byte, string)>();
		if (lastResultTable != null && lastResultTable.Rows.Count > 0)
		{
			DataRow dataRow = lastResultTable.Rows[0];
			foreach (InkjetPositionMapping item in currentInkjetConfig.Mappings.OrderBy((InkjetPositionMapping m) => m.Position))
			{
				string value = (lastResultTable.Columns.Contains(item.FieldName) ? (dataRow[item.FieldName]?.ToString() ?? "") : "");
				vars.Add(((byte)item.Position, InkjetPrintService.PadVar(value, currentInkjetConfig.VarWidth)));
			}
		}
		else
		{
			foreach (InkjetPositionMapping item2 in currentInkjetConfig.Mappings.OrderBy((InkjetPositionMapping m) => m.Position))
			{
				vars.Add(((byte)item2.Position, InkjetPrintService.PadVar(item2.FieldName, currentInkjetConfig.VarWidth)));
			}
		}
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.SendVarsAsync(cfg, vars));
	}

	private async void InkjetDump_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.DumpJobAsync(cfg));
	}

	private async void InkjetMakeExternal_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.MakeExternalAsync(cfg));
	}

	private async void InkjetQueueEnable_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.QueueEnableAsync(cfg, enabled: true));
	}

	private async void InkjetQueueDisable_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.QueueDisableAsync(cfg));
	}

	private async void InkjetQueueReset_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.QueueResetAsync(cfg));
	}

	private async void InkjetQueueStatus_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.QueueStatusAsync(cfg));
	}

	private async void InkjetJetStart_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.JetAsync(cfg, start: true));
	}

	private async void InkjetJetStop_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.JetAsync(cfg, start: false));
	}

	private async void InkjetTrigger_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.TriggerAsync(cfg));
	}

	private async void InkjetAckFaults_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.AckFaultsAsync(cfg));
	}

	private async void InkjetPushLibrary_Click(object sender, RoutedEventArgs e)
	{
		if (MessageBox.Show("Library write (9Bh) may cause fault 5140 on the printer. Force push anyway?", "Force Library Write", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
		{
			await RunInkjetUtilityAsync((InkjetConfig cfg) => _inkjetPrintService.PushLibraryAsync(cfg, "DEMO", forceLibrary: true));
		}
	}

	private async void LinxStatus_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.StatusAsync(cfg));
	}

	private async void LinxSelect_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.SelectMessageAsync(cfg));
	}

	private async void LinxList_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.ListMessagesAsync(cfg));
	}

	private async void LinxRemoteFields_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.RemoteFieldsAsync(cfg));
	}

	private async void LinxQueueClear_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.QueueClearAsync(cfg));
	}

	private async void LinxQueueMode_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.QueueModeAsync(cfg));
	}

	private async void LinxJetStart_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.JetAsync(cfg, start: true));
	}

	private async void LinxJetStop_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.JetAsync(cfg, start: false));
	}

	private async void LinxStartPrint_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.PrintControlAsync(cfg, start: true));
	}

	private async void LinxStopPrint_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.PrintControlAsync(cfg, start: false));
	}

	private async void LinxTrigger_Click(object sender, RoutedEventArgs e)
	{
		await RunInkjetUtilityAsync((InkjetConfig cfg) => _linxPrintService.TriggerAsync(cfg));
	}

	private async Task RunInkjetUtilityAsync(Func<InkjetConfig, Task<InkjetOperationResult>> action)
	{
		try
		{
			InkjetConfig currentInkjetConfig = GetCurrentInkjetConfig();
			InkjetPanel.SetStatus("Running...");
			InkjetOperationResult inkjetOperationResult = await action(currentInkjetConfig);
			AppendInkjetProtocolLog(inkjetOperationResult.ProtocolLog);
			InkjetPanel.SetStatus(inkjetOperationResult.Success ? inkjetOperationResult.Message : ("Failed: " + inkjetOperationResult.Message));
		}
		catch (Exception ex)
		{
			AppendInkjetProtocolLog("Error: " + ex.Message);
			InkjetPanel.SetStatus("Error: " + ex.Message);
			MessageBox.Show(ex.Message, CurrentTemplate.TemplateType.DisplayName() + " Tool", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}
}
