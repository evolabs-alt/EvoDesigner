using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TaggJack.Models;
using TaggJack.Services;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace TaggJack.UI;

public partial class PrintPreviewWindow : Window {
	private LabelTemplate template;

	private DataTable? dataTable;

	private int currentRecordIndex;







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
			StackPanel navigationPanel = GetNavigationPanel();
			if (navigationPanel != null)
			{
				navigationPanel.Visibility = Visibility.Visible;
			}
			UpdateNavigationButtons();
			UpdateRecordCounter();
		}
		else
		{
			StackPanel navigationPanel2 = GetNavigationPanel();
			if (navigationPanel2 != null)
			{
				navigationPanel2.Visibility = Visibility.Collapsed;
			}
		}
	}

	private void UpdateNavigationButtons()
	{
		if (dataTable != null)
		{
			Button prevButton = GetPrevButton();
			Button nextButton = GetNextButton();
			if (prevButton != null)
			{
				prevButton.IsEnabled = currentRecordIndex > 0;
			}
			if (nextButton != null)
			{
				nextButton.IsEnabled = currentRecordIndex < dataTable.Rows.Count - 1;
			}
		}
	}

	private void UpdateRecordCounter()
	{
		if (dataTable != null)
		{
			TextBlock recordCounter = GetRecordCounter();
			if (recordCounter != null)
			{
				recordCounter.Text = $"Record {currentRecordIndex + 1} of {dataTable.Rows.Count}";
			}
		}
	}

	private void PrevRecord_Click(object sender, RoutedEventArgs e)
	{
		if (dataTable != null && currentRecordIndex > 0)
		{
			currentRecordIndex--;
			UpdateNavigationButtons();
			UpdateRecordCounter();
			UpdateLayout();
			RenderPreview();
			UpdateLayout();
		}
	}

	private void NextRecord_Click(object sender, RoutedEventArgs e)
	{
		if (dataTable != null && currentRecordIndex < dataTable.Rows.Count - 1)
		{
			currentRecordIndex++;
			UpdateNavigationButtons();
			UpdateRecordCounter();
			UpdateLayout();
			RenderPreview();
			UpdateLayout();
		}
	}

	private string GetElementContent(LabelElement element, int elementIndex)
	{
		if (dataTable == null || template.FieldMappings == null || currentRecordIndex >= dataTable.Rows.Count)
		{
			return element.Content ?? "";
		}
		FieldMapping fieldMapping = template.FieldMappings.FirstOrDefault((FieldMapping m) => m.ElementIndex == elementIndex);
		if (fieldMapping != null && !string.IsNullOrEmpty(fieldMapping.FieldName) && dataTable.Columns.Contains(fieldMapping.FieldName))
		{
			string text = dataTable.Rows[currentRecordIndex][fieldMapping.FieldName]?.ToString() ?? "";
			string text2;
			if (string.IsNullOrEmpty(text))
			{
				text2 = element.Content;
				if (text2 == null)
				{
					return "";
				}
			}
			else
			{
				text2 = text;
			}
			return text2;
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
			TextBlock labelSizeInfo = GetLabelSizeInfo();
			if (labelSizeInfo != null)
			{
				labelSizeInfo.Text = $"Label Size: {template.LabelWidth}mm Ã— {template.LabelHeight}mm";
			}
			Canvas previewCanvas = GetPreviewCanvas();
			Console.WriteLine($"[DEBUG] PreviewCanvas found: {previewCanvas != null}");
			if (previewCanvas == null)
			{
				return;
			}
			previewCanvas.Children.Clear();
			previewCanvas.Background = System.Windows.Media.Brushes.White;
			double num = MeasurementService.MmToScreenPixels(template.LabelWidth);
			double num2 = MeasurementService.MmToScreenPixels(template.LabelHeight);
			Console.WriteLine($"[DEBUG] Canvas size: {num} x {num2} pixels");
			num = Math.Max(num, 200.0);
			num2 = Math.Max(num2, 100.0);
			previewCanvas.Width = num;
			previewCanvas.Height = num2;
			previewCanvas.Clip = new RectangleGeometry(new Rect(0.0, 0.0, num, num2));
			int num3 = 0;
			Console.WriteLine($"[DEBUG] Starting to process {template.Elements.Count} elements");
			foreach (LabelElement element2 in template.Elements)
			{
				Console.WriteLine($"[DEBUG] Processing element {num3}: Type={element2.Type}, Size={element2.Width}x{element2.Height}, Position={element2.X},{element2.Y}");
				UIElement uIElement = null;
				string elementContent = GetElementContent(element2, num3);
				switch (element2.Type)
				{
				case LabelElementType.Text:
				{
					Console.WriteLine($"[DEBUG] Creating text element: '{elementContent}', Size: {element2.Width}x{element2.Height}, FontSize: {element2.FontSize}");
					Border border = new Border
					{
						Width = element2.Width,
						Height = element2.Height,
						Background = System.Windows.Media.Brushes.Transparent
					};
					TextBlock textBlock = new TextBlock
					{
						Text = elementContent,
						FontSize = ((element2.FontSize > 0.0) ? element2.FontSize : 12.0),
						TextWrapping = TextWrapping.Wrap,
						Width = element2.Width,
						Height = element2.Height
					};
					Console.WriteLine($"[DEBUG] TextBlock created with text: '{textBlock.Text}', FontSize: {textBlock.FontSize}");
					try
					{
						if (!string.IsNullOrEmpty(element2.FontFamily))
						{
							textBlock.FontFamily = new System.Windows.Media.FontFamily(element2.FontFamily);
						}
					}
					catch
					{
					}
					string fontWeight = element2.FontWeight;
					textBlock.FontWeight = ((fontWeight != null && fontWeight.Contains("Bold")) ? FontWeights.Bold : FontWeights.Normal);
					string fontStyle = element2.FontStyle;
					textBlock.FontStyle = ((fontStyle != null && fontStyle.Contains("Italic")) ? FontStyles.Italic : FontStyles.Normal);
					try
					{
						if (!string.IsNullOrEmpty(element2.Foreground))
						{
							textBlock.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(element2.Foreground) ?? System.Windows.Media.Brushes.Black);
						}
						else
						{
							textBlock.Foreground = System.Windows.Media.Brushes.Black;
						}
					}
					catch
					{
						textBlock.Foreground = System.Windows.Media.Brushes.Black;
					}
					Console.WriteLine("[DEBUG PRINT] el.TextAlignment = '" + element2.TextAlignment + "'");
					File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] el.TextAlignment = '{element2.TextAlignment}'\n");
					string text = element2.TextAlignment?.ToLower();
					if (!(text == "center"))
					{
						if (text == "right")
						{
							textBlock.TextAlignment = TextAlignment.Right;
							Console.WriteLine("[DEBUG PRINT] Set TextAlignment.Right");
							File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set TextAlignment.Right\n");
						}
						else
						{
							textBlock.TextAlignment = TextAlignment.Left;
							Console.WriteLine("[DEBUG PRINT] Set TextAlignment.Left (default, was: '" + element2.TextAlignment + "')");
							File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set TextAlignment.Left (default, was: '{element2.TextAlignment}')\n");
						}
					}
					else
					{
						textBlock.TextAlignment = TextAlignment.Center;
						Console.WriteLine("[DEBUG PRINT] Set TextAlignment.Center");
						File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set TextAlignment.Center\n");
					}
					Console.WriteLine("[DEBUG PRINT] el.VerticalAlignment = '" + element2.VerticalAlignment + "'");
					File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] el.VerticalAlignment = '{element2.VerticalAlignment}'\n");
					text = element2.VerticalAlignment?.ToLower();
					if (!(text == "center"))
					{
						if (text == "bottom")
						{
							textBlock.VerticalAlignment = VerticalAlignment.Bottom;
							Console.WriteLine("[DEBUG PRINT] Set VerticalAlignment.Bottom");
							File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set VerticalAlignment.Bottom\n");
						}
						else
						{
							textBlock.VerticalAlignment = VerticalAlignment.Top;
							Console.WriteLine("[DEBUG PRINT] Set VerticalAlignment.Top (default, was: '" + element2.VerticalAlignment + "')");
							File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set VerticalAlignment.Top (default, was: '{element2.VerticalAlignment}')\n");
						}
					}
					else
					{
						textBlock.VerticalAlignment = VerticalAlignment.Center;
						Console.WriteLine("[DEBUG PRINT] Set VerticalAlignment.Center");
						File.AppendAllText("debug.log", $"{DateTime.Now}: [DEBUG PRINT] Set VerticalAlignment.Center\n");
					}
					Console.WriteLine($"[DEBUG] Text alignment set: TextAlignment={textBlock.TextAlignment}, VerticalAlignment={textBlock.VerticalAlignment}");
					try
					{
						if (!string.IsNullOrEmpty(element2.TextDecorations))
						{
							switch (element2.TextDecorations.ToLower())
							{
							case "underline":
								textBlock.TextDecorations = TextDecorations.Underline;
								break;
							case "strikethrough":
								textBlock.TextDecorations = TextDecorations.Strikethrough;
								break;
							case "overline":
								textBlock.TextDecorations = TextDecorations.OverLine;
								break;
							case "baseline":
								textBlock.TextDecorations = TextDecorations.Baseline;
								break;
							}
						}
					}
					catch
					{
					}
					border.Child = textBlock;
					uIElement = border;
					Console.WriteLine($"[DEBUG] Text element created, positioned at ({element2.X}, {element2.Y})");
					break;
				}
				case LabelElementType.Image:
					if (!string.IsNullOrEmpty(element2.ImageData))
					{
						try
						{
							using MemoryStream streamSource = new MemoryStream(Convert.FromBase64String(element2.ImageData));
							BitmapImage bitmapImage2 = new BitmapImage();
							bitmapImage2.BeginInit();
							bitmapImage2.StreamSource = streamSource;
							bitmapImage2.CacheOption = BitmapCacheOption.OnLoad;
							bitmapImage2.EndInit();
							bitmapImage2.Freeze();
							ImageBrush imageBrush2 = new ImageBrush();
							imageBrush2.ImageSource = bitmapImage2;
							imageBrush2.Stretch = Stretch.Uniform;
							imageBrush2.AlignmentX = AlignmentX.Center;
							imageBrush2.AlignmentY = AlignmentY.Center;
							uIElement = new System.Windows.Shapes.Rectangle
							{
								Width = element2.Width,
								Height = element2.Height,
								Fill = imageBrush2,
								Stroke = System.Windows.Media.Brushes.DarkGray,
								StrokeThickness = 1.0
							};
						}
						catch (Exception)
						{
							uIElement = new System.Windows.Shapes.Rectangle
							{
								Width = element2.Width,
								Height = element2.Height,
								Fill = System.Windows.Media.Brushes.LightGray,
								Stroke = System.Windows.Media.Brushes.Red,
								StrokeThickness = 2.0
							};
						}
					}
					else
					{
						uIElement = new System.Windows.Shapes.Rectangle
						{
							Width = element2.Width,
							Height = element2.Height,
							Fill = System.Windows.Media.Brushes.LightGray,
							Stroke = System.Windows.Media.Brushes.DarkGray,
							StrokeThickness = 1.0
						};
					}
					break;
				case LabelElementType.LinkedImage:
					if (!string.IsNullOrEmpty(element2.Content))
					{
						try
						{
							BitmapImage bitmapImage = new BitmapImage();
							bitmapImage.BeginInit();
							bitmapImage.UriSource = new Uri(element2.Content, UriKind.Absolute);
							bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
							bitmapImage.EndInit();
							bitmapImage.Freeze();
							ImageBrush imageBrush = new ImageBrush();
							imageBrush.ImageSource = bitmapImage;
							imageBrush.Stretch = Stretch.Uniform;
							imageBrush.AlignmentX = AlignmentX.Center;
							imageBrush.AlignmentY = AlignmentY.Center;
							uIElement = new System.Windows.Shapes.Rectangle
							{
								Width = element2.Width,
								Height = element2.Height,
								Fill = imageBrush,
								Stroke = System.Windows.Media.Brushes.DarkBlue,
								StrokeThickness = 1.0
							};
							Console.WriteLine("[DEBUG PRINT] LinkedImage loaded from URL: " + element2.Content);
						}
						catch (Exception ex)
						{
							uIElement = new System.Windows.Shapes.Rectangle
							{
								Width = element2.Width,
								Height = element2.Height,
								Fill = System.Windows.Media.Brushes.LightBlue,
								Stroke = System.Windows.Media.Brushes.Red,
								StrokeThickness = 2.0
							};
							Console.WriteLine("[DEBUG PRINT] LinkedImage failed to load from URL " + element2.Content + ": " + ex.Message);
						}
					}
					else
					{
						uIElement = new System.Windows.Shapes.Rectangle
						{
							Width = element2.Width,
							Height = element2.Height,
							Fill = System.Windows.Media.Brushes.LightBlue,
							Stroke = System.Windows.Media.Brushes.DarkBlue,
							StrokeThickness = 1.0
						};
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
				case LabelElementType.Barcode13:
				case LabelElementType.Barcode8:
				case LabelElementType.Barcode:
				{
					string barcodeFormatString = GetBarcodeFormatString(element2.Type);
					System.Windows.Controls.Image image = CreateBarcodeImage(barcodeFormatString, elementContent, element2.Width, element2.Height);
					if (image != null)
					{
						uIElement = image;
						break;
					}
					string barcodeTypeName = GetBarcodeTypeName(element2.Type);
					uIElement = new TextBlock
					{
						Text = "[" + barcodeTypeName + "] GENERATION ERROR\nData: " + elementContent,
						FontSize = 8.0,
						Width = element2.Width,
						Height = element2.Height,
						Foreground = System.Windows.Media.Brushes.Red,
						TextWrapping = TextWrapping.Wrap
					};
					break;
				}
				case LabelElementType.QRCode:
				{
					Console.WriteLine($"[DEBUG] Creating QR code, Content: '{elementContent}', Size: {element2.Width}x{element2.Height}");
					System.Windows.Controls.Image image2 = CreateBarcodeImage("QRCODE", elementContent, element2.Width, element2.Height);
					Console.WriteLine("[DEBUG] CreateBarcodeImage for QR returned: " + ((image2 != null) ? "SUCCESS" : "NULL"));
					uIElement = ((image2 == null) ? ((FrameworkElement)new TextBlock
					{
						Text = "[QR Code] GENERATION ERROR\nData: " + elementContent,
						FontSize = 8.0,
						Width = element2.Width,
						Height = element2.Height,
						Foreground = System.Windows.Media.Brushes.Red,
						TextWrapping = TextWrapping.Wrap
					}) : ((FrameworkElement)image2));
					break;
				}
				case LabelElementType.Line:
				{
					Canvas canvas = new Canvas
					{
						Width = element2.Width,
						Height = element2.Height,
						Background = System.Windows.Media.Brushes.Transparent
					};
					Line line = new Line
					{
						X1 = 0.0,
						Y1 = element2.Height / 2.0,
						X2 = element2.Width,
						Y2 = element2.Height / 2.0,
						StrokeThickness = ((element2.BorderWidth > 0.0) ? element2.BorderWidth : 1.0)
					};
					try
					{
						if (!string.IsNullOrEmpty(element2.BorderColor))
						{
							line.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element2.BorderColor) ?? System.Windows.Media.Brushes.Black);
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
					Border border2 = new Border
					{
						Width = element2.Width,
						Height = element2.Height,
						BorderThickness = new Thickness((element2.BorderWidth > 0.0) ? element2.BorderWidth : 1.0),
						Background = System.Windows.Media.Brushes.Transparent,
						CornerRadius = new CornerRadius(element2.HasRoundedCorners ? element2.CornerRadius : 0.0)
					};
					try
					{
						if (!string.IsNullOrEmpty(element2.BorderColor))
						{
							border2.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom(element2.BorderColor) ?? System.Windows.Media.Brushes.Black);
						}
						else
						{
							border2.BorderBrush = System.Windows.Media.Brushes.Black;
						}
					}
					catch
					{
						border2.BorderBrush = System.Windows.Media.Brushes.Black;
					}
					Console.WriteLine($"[DEBUG PRINT] Rectangle: HasRoundedCorners={element2.HasRoundedCorners}, CornerRadius={element2.CornerRadius}");
					uIElement = border2;
					break;
				}
				case LabelElementType.Ellipse:
				{
					Ellipse ellipse = new Ellipse
					{
						Width = element2.Width,
						Height = element2.Height,
						StrokeThickness = ((element2.BorderWidth > 0.0) ? element2.BorderWidth : 1.0),
						Fill = System.Windows.Media.Brushes.Transparent
					};
					try
					{
						if (!string.IsNullOrEmpty(element2.BorderColor))
						{
							ellipse.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element2.BorderColor) ?? System.Windows.Media.Brushes.Black);
						}
						else
						{
							ellipse.Stroke = System.Windows.Media.Brushes.Black;
						}
					}
					catch
					{
						ellipse.Stroke = System.Windows.Media.Brushes.Black;
					}
					Console.WriteLine($"[DEBUG PRINT] Ellipse: Width={element2.Width}, Height={element2.Height}");
					uIElement = ellipse;
					break;
				}
				}
				if (uIElement != null)
				{
					Canvas.SetLeft(uIElement, element2.X);
					Canvas.SetTop(uIElement, element2.Y);
					if (element2.Rotation != 0.0)
					{
						RotateTransform renderTransform = new RotateTransform(element2.Rotation);
						if (uIElement is FrameworkElement frameworkElement)
						{
							frameworkElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
						}
						uIElement.RenderTransform = renderTransform;
					}
					Console.WriteLine($"[DEBUG] Adding element {num3} to preview canvas: {uIElement?.GetType().Name}");
					previewCanvas.Children.Add(uIElement);
					Console.WriteLine($"[DEBUG] Element added successfully. Canvas now has {previewCanvas.Children.Count} children");
				}
				else
				{
					Console.WriteLine($"[DEBUG] Element {num3} created NULL UIElement");
				}
				num3++;
			}
			if (num3 == 0)
			{
				TextBlock element = new TextBlock
				{
					Text = "No elements to preview.\nPlease add text, images, or barcodes to your label design.",
					FontSize = 14.0,
					Foreground = System.Windows.Media.Brushes.Gray,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					TextAlignment = TextAlignment.Center,
					TextWrapping = TextWrapping.Wrap
				};
				Canvas.SetLeft(element, num / 2.0 - 150.0);
				Canvas.SetTop(element, num2 / 2.0 - 20.0);
				previewCanvas.Children.Add(element);
			}
		}
		catch (Exception ex3)
		{
			MessageBox.Show("Error generating print preview: " + ex3.Message, "Preview Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
			LabelElementType.Barcode13 => "EAN13", 
			LabelElementType.Barcode8 => "EAN8", 
			LabelElementType.Barcode => "EAN13", 
			_ => "CODE128", 
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
			_ => "Unknown", 
		};
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
				text = "01234567890123";
				break;
			case "PDF417":
				barcodeFormat = BarcodeFormat.PDF_417;
				text = "Sample PDF417 Data";
				break;
			default:
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "DEFAULT";
				break;
			}
			if (string.IsNullOrWhiteSpace(data))
			{
				data = text;
			}
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
		catch (Exception ex)
		{
			Console.WriteLine("[DEBUG] Barcode generation failed: " + ex.Message);
			TextBlock child = new TextBlock
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
			new Border
			{
				Width = width,
				Height = height,
				Background = System.Windows.Media.Brushes.Transparent,
				Child = child
			};
			Bitmap bitmap2 = new Bitmap(1, 1);
			using MemoryStream memoryStream2 = new MemoryStream();
			bitmap2.Save(memoryStream2, ImageFormat.Png);
			memoryStream2.Position = 0L;
			BitmapImage bitmapImage2 = new BitmapImage();
			bitmapImage2.BeginInit();
			bitmapImage2.StreamSource = memoryStream2;
			bitmapImage2.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage2.EndInit();
			return new System.Windows.Controls.Image
			{
				Source = bitmapImage2,
				Width = width,
				Height = height,
				Opacity = 0.1,
				Tag = "BarcodeError:" + symbology + ":" + data
			};
		}
	}

	private void Print_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Console.WriteLine("[DEBUG] Print_Click started");
			PrintDialog printDialog = new PrintDialog();
			Console.WriteLine("[DEBUG] Showing print dialog");
			if (printDialog.ShowDialog() == true)
			{
				Console.WriteLine("[DEBUG] Printer selected: " + (printDialog.PrintQueue?.Name ?? "Unknown"));
				(bool, int)? tuple = ShowPrintOptionsDialog();
				if (!tuple.HasValue)
				{
					Console.WriteLine("[DEBUG] User cancelled print options dialog");
					return;
				}
				Console.WriteLine($"[DEBUG] Print choice: printAll={tuple.Value.Item1}, copies={tuple.Value.Item2}");
				if (tuple.Value.Item1 && dataTable != null && dataTable.Rows.Count > 1)
				{
					int[] array = Enumerable.Range(0, dataTable.Rows.Count).ToArray();
					Console.WriteLine($"[DEBUG] Printing all {array.Length} records");
					PrintMultipleLabels(printDialog, array, 1);
				}
				else
				{
					Console.WriteLine($"[DEBUG] Printing current record (index {currentRecordIndex}) with {tuple.Value.Item2} copies");
					PrintMultipleLabels(printDialog, new int[1] { currentRecordIndex }, tuple.Value.Item2);
				}
				Console.WriteLine("[DEBUG] Print operation completed");
			}
			else
			{
				Console.WriteLine("[DEBUG] Print dialog cancelled");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DEBUG] Print error: " + ex.Message);
			Console.WriteLine("[DEBUG] Stack trace: " + ex.StackTrace);
			MessageBox.Show("Print error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private (bool printAll, int copies)? ShowPrintOptionsDialog()
	{
		if (dataTable == null || dataTable.Rows.Count <= 1)
		{
			string copiesInput = "1";
			Window dialog = new Window
			{
				Title = "Print Copies",
				Width = 300.0,
				Height = 150.0,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Owner = this
			};
			Grid grid = new Grid();
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			Label element = new Label
			{
				Content = "How many copies do you want to print?",
				Margin = new Thickness(10.0)
			};
			Grid.SetRow(element, 0);
			TextBox textBox = new TextBox
			{
				Text = "1",
				Margin = new Thickness(10.0),
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			Grid.SetRow(textBox, 1);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(10.0)
			};
			Button button = new Button
			{
				Content = "OK",
				Width = 60.0,
				Margin = new Thickness(5.0, 0.0, 5.0, 0.0)
			};
			Button button2 = new Button
			{
				Content = "Cancel",
				Width = 60.0,
				Margin = new Thickness(5.0, 0.0, 5.0, 0.0)
			};
			bool? dialogResult = null;
			button.Click += delegate
			{
				copiesInput = textBox.Text;
				dialogResult = true;
				dialog.Close();
			};
			button2.Click += delegate
			{
				dialogResult = false;
				dialog.Close();
			};
			stackPanel.Children.Add(button);
			stackPanel.Children.Add(button2);
			Grid.SetRow(stackPanel, 2);
			grid.Children.Add(element);
			grid.Children.Add(textBox);
			grid.Children.Add(stackPanel);
			dialog.Content = grid;
			dialog.ShowDialog();
			if (dialogResult != true)
			{
				return null;
			}
			if (int.TryParse(copiesInput, out var result) && result > 0)
			{
				return (false, result);
			}
			MessageBox.Show("Please enter a valid number of copies.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return null;
		}
		return MessageBox.Show($"You have {dataTable.Rows.Count} records.\n\n" + "Click 'Yes' to print all records (one label per record)\nClick 'No' to print only the current record\nClick 'Cancel' to abort printing", "Print Options", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
		{
			MessageBoxResult.Cancel => null, 
			MessageBoxResult.Yes => (true, 1), 
			_ => ShowCopiesDialog(), 
		};
	}

	private (bool printAll, int copies)? ShowCopiesDialog()
	{
		string copiesInput = "1";
		Window dialog = new Window
		{
			Title = "Print Copies",
			Width = 350.0,
			Height = 150.0,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Owner = this
		};
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Label element = new Label
		{
			Content = "How many copies of the current record?",
			Margin = new Thickness(10.0)
		};
		Grid.SetRow(element, 0);
		TextBox textBox = new TextBox
		{
			Text = "1",
			Margin = new Thickness(10.0),
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		Grid.SetRow(textBox, 1);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(10.0)
		};
		Button button = new Button
		{
			Content = "OK",
			Width = 60.0,
			Margin = new Thickness(5.0, 0.0, 5.0, 0.0)
		};
		Button button2 = new Button
		{
			Content = "Cancel",
			Width = 60.0,
			Margin = new Thickness(5.0, 0.0, 5.0, 0.0)
		};
		bool? dialogResult = null;
		button.Click += delegate
		{
			copiesInput = textBox.Text;
			dialogResult = true;
			dialog.Close();
		};
		button2.Click += delegate
		{
			dialogResult = false;
			dialog.Close();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		Grid.SetRow(stackPanel, 2);
		grid.Children.Add(element);
		grid.Children.Add(textBox);
		grid.Children.Add(stackPanel);
		dialog.Content = grid;
		dialog.ShowDialog();
		if (dialogResult != true)
		{
			return null;
		}
		if (int.TryParse(copiesInput, out var result) && result > 0)
		{
			return (false, result);
		}
		MessageBox.Show("Please enter a valid number of copies.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		return null;
	}

	private void PrintMultipleLabels(PrintDialog printDialog, int[] recordIndices, int copiesPerRecord)
	{
		try
		{
			Console.WriteLine($"[DEBUG] PrintMultipleLabels started - {recordIndices.Length} records, {copiesPerRecord} copies each");
			double num = template.LabelWidth * 96.0 / 25.4;
			double num2 = template.LabelHeight * 96.0 / 25.4;
			Console.WriteLine($"[DEBUG] Template label size: {template.LabelWidth}mm x {template.LabelHeight}mm");
			Console.WriteLine($"[DEBUG] Page size: {num} x {num2} DIP");
			FixedDocument fixedDocument = new FixedDocument();
			int num3 = 0;
			foreach (int num4 in recordIndices)
			{
				for (int j = 0; j < copiesPerRecord; j++)
				{
					Console.WriteLine($"[DEBUG] Creating page for record {num4}, copy {j + 1}");
					FixedPage fixedPage = new FixedPage();
					fixedPage.Width = num;
					fixedPage.Height = num2;
					Canvas canvas = CreateSimplePrintCanvas(num4, num, num2);
					Console.WriteLine($"[DEBUG] Print canvas created with {canvas.Children.Count} children");
					fixedPage.Children.Add(canvas);
					PageContent pageContent = new PageContent();
					((IAddChild)pageContent).AddChild((object)fixedPage);
					fixedDocument.Pages.Add(pageContent);
					num3++;
					Console.WriteLine($"[DEBUG] Added page {num3} to document");
				}
			}
			Console.WriteLine($"[DEBUG] Document created with {fixedDocument.Pages.Count} total page(s)");
			Console.WriteLine("[DEBUG] Sending multi-page document to printer...");
			try
			{
				string description = ((recordIndices.Length == 1) ? $"TaggJack Label - Record {recordIndices[0] + 1} ({copiesPerRecord} copies)" : $"TaggJack Labels - {recordIndices.Length} records ({num3} pages)");
				printDialog.PrintDocument(fixedDocument.DocumentPaginator, description);
				Console.WriteLine("[DEBUG] Multi-page print job sent successfully");
			}
			catch (Exception ex)
			{
				Console.WriteLine("[DEBUG] Print command failed: " + ex.Message);
				Console.WriteLine("[DEBUG] Print error stack trace: " + ex.StackTrace);
				throw;
			}
			Console.WriteLine("[DEBUG] All labels sent to printer");
		}
		catch (Exception ex2)
		{
			Console.WriteLine("[DEBUG] PrintMultipleLabels error: " + ex2.Message);
			Console.WriteLine("[DEBUG] Stack trace: " + ex2.StackTrace);
			throw;
		}
	}

	private Canvas CreateSimplePrintCanvas(int recordIndex, double canvasWidth, double canvasHeight)
	{
		Console.WriteLine($"[DEBUG] CreateSimplePrintCanvas: record {recordIndex}, canvas size {canvasWidth}x{canvasHeight}");
		Console.WriteLine($"[DEBUG] Template has {template.Elements.Count} elements");
		Console.WriteLine($"[DEBUG] Template label size: {template.LabelWidth}mm x {template.LabelHeight}mm");
		Canvas canvas = new Canvas();
		canvas.Width = canvasWidth;
		canvas.Height = canvasHeight;
		canvas.Background = System.Windows.Media.Brushes.White;
		Console.WriteLine("[DEBUG] Using NO SCALING - same as preview");
		int num = 0;
		int num2 = 0;
		foreach (LabelElement element in template.Elements)
		{
			Console.WriteLine($"[DEBUG] Processing element {num}: Type={element.Type}, Content='{element.Content}', Position=({element.X}, {element.Y}), Size=({element.Width}x{element.Height})");
			UIElement uIElement = null;
			string elementContentForRecord = GetElementContentForRecord(element, num, recordIndex);
			Console.WriteLine("[DEBUG] Element content: '" + elementContentForRecord + "'");
			switch (element.Type)
			{
			case LabelElementType.Text:
			{
				Border border = new Border
				{
					Width = element.Width,
					Height = element.Height,
					Background = System.Windows.Media.Brushes.Transparent
				};
				TextBlock textBlock = new TextBlock
				{
					Text = elementContentForRecord,
					FontSize = ((element.FontSize > 0.0) ? element.FontSize : 12.0),
					TextWrapping = TextWrapping.Wrap
				};
				try
				{
					if (!string.IsNullOrEmpty(element.FontFamily))
					{
						textBlock.FontFamily = new System.Windows.Media.FontFamily(element.FontFamily);
					}
				}
				catch
				{
				}
				string fontWeight = element.FontWeight;
				textBlock.FontWeight = ((fontWeight != null && fontWeight.Contains("Bold")) ? FontWeights.Bold : FontWeights.Normal);
				string fontStyle = element.FontStyle;
				textBlock.FontStyle = ((fontStyle != null && fontStyle.Contains("Italic")) ? FontStyles.Italic : FontStyles.Normal);
				try
				{
					if (!string.IsNullOrEmpty(element.Foreground))
					{
						textBlock.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.Foreground) ?? System.Windows.Media.Brushes.Black);
					}
					else
					{
						textBlock.Foreground = System.Windows.Media.Brushes.Black;
					}
				}
				catch
				{
					textBlock.Foreground = System.Windows.Media.Brushes.Black;
				}
				string text = element.TextAlignment?.ToLower();
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
				text = element.VerticalAlignment?.ToLower();
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
				border.Child = textBlock;
				Console.WriteLine($"[DEBUG] Created TextBlock with alignment: '{textBlock.Text}', FontSize={textBlock.FontSize}, TextAlign={textBlock.TextAlignment}, VerticalAlign={textBlock.VerticalAlignment}");
				uIElement = border;
				break;
			}
			case LabelElementType.Image:
				if (!string.IsNullOrEmpty(element.ImageData))
				{
					try
					{
						byte[] buffer = Convert.FromBase64String(element.ImageData);
						BitmapImage bitmapImage = new BitmapImage();
						bitmapImage.BeginInit();
						bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
						bitmapImage.StreamSource = new MemoryStream(buffer);
						bitmapImage.EndInit();
						bitmapImage.Freeze();
						System.Windows.Controls.Image image = new System.Windows.Controls.Image
						{
							Source = bitmapImage,
							Width = element.Width,
							Height = element.Height,
							Stretch = Stretch.Uniform,
							StretchDirection = StretchDirection.Both
						};
						Console.WriteLine($"[DEBUG] Created actual Image for printing: {image.Width}x{image.Height}");
						uIElement = image;
					}
					catch (Exception ex)
					{
						Console.WriteLine("[DEBUG] Image load error in print: " + ex.Message);
						uIElement = new System.Windows.Shapes.Rectangle
						{
							Width = element.Width,
							Height = element.Height,
							Fill = System.Windows.Media.Brushes.LightGray,
							Stroke = System.Windows.Media.Brushes.Red,
							StrokeThickness = 2.0
						};
					}
				}
				else
				{
					System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle
					{
						Width = element.Width,
						Height = element.Height,
						Fill = System.Windows.Media.Brushes.LightGray,
						Stroke = System.Windows.Media.Brushes.DarkGray,
						StrokeThickness = 1.0
					};
					Console.WriteLine($"[DEBUG] Created Image placeholder for printing: {rectangle.Width}x{rectangle.Height}");
					uIElement = rectangle;
				}
				break;
			case LabelElementType.LinkedImage:
				if (!string.IsNullOrEmpty(element.Content))
				{
					try
					{
						BitmapImage bitmapImage2 = new BitmapImage();
						bitmapImage2.BeginInit();
						bitmapImage2.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage2.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
						bitmapImage2.UriSource = new Uri(element.Content, UriKind.Absolute);
						bitmapImage2.EndInit();
						bitmapImage2.Freeze();
						System.Windows.Controls.Image obj8 = new System.Windows.Controls.Image
						{
							Source = bitmapImage2,
							Width = element.Width,
							Height = element.Height,
							Stretch = Stretch.Uniform,
							StretchDirection = StretchDirection.Both
						};
						Console.WriteLine("[DEBUG] Created LinkedImage for printing from URL: " + element.Content);
						uIElement = obj8;
					}
					catch (Exception ex2)
					{
						Console.WriteLine("[DEBUG] LinkedImage load error in print from URL " + element.Content + ": " + ex2.Message);
						uIElement = new System.Windows.Shapes.Rectangle
						{
							Width = element.Width,
							Height = element.Height,
							Fill = System.Windows.Media.Brushes.LightBlue,
							Stroke = System.Windows.Media.Brushes.Red,
							StrokeThickness = 2.0
						};
					}
				}
				else
				{
					System.Windows.Shapes.Rectangle rectangle2 = new System.Windows.Shapes.Rectangle
					{
						Width = element.Width,
						Height = element.Height,
						Fill = System.Windows.Media.Brushes.LightBlue,
						Stroke = System.Windows.Media.Brushes.DarkBlue,
						StrokeThickness = 1.0
					};
					Console.WriteLine($"[DEBUG] Created LinkedImage placeholder for printing: {rectangle2.Width}x{rectangle2.Height}");
					uIElement = rectangle2;
				}
				break;
			case LabelElementType.QRCode:
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
			case LabelElementType.Barcode13:
			case LabelElementType.Barcode8:
			case LabelElementType.Barcode:
			{
				System.Windows.Controls.Image image2 = CreateBarcodeImage(GetBarcodeFormatString(element.Type), elementContentForRecord, element.Width, element.Height);
				if (image2 != null)
				{
					Console.WriteLine($"[DEBUG] Created {element.Type} barcode image: {image2.Width}x{image2.Height}");
					uIElement = image2;
					break;
				}
				TextBlock obj6 = new TextBlock
				{
					Text = $"[{element.Type}: {elementContentForRecord}]",
					FontSize = 8.0,
					Width = element.Width,
					Height = element.Height,
					Foreground = System.Windows.Media.Brushes.Black,
					TextAlignment = TextAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				Console.WriteLine($"[DEBUG] Created {element.Type} fallback text");
				uIElement = obj6;
				break;
			}
			case LabelElementType.Line:
			{
				Canvas canvas2 = new Canvas
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
				canvas2.Children.Add(line);
				Console.WriteLine($"[DEBUG] Created Line for printing: {element.Width}x{element.Height}, Thickness: {line.StrokeThickness}");
				uIElement = canvas2;
				break;
			}
			case LabelElementType.Rectangle:
			{
				Border border2 = new Border
				{
					Width = element.Width,
					Height = element.Height,
					BorderThickness = new Thickness((element.BorderWidth > 0.0) ? element.BorderWidth : 1.0),
					Background = System.Windows.Media.Brushes.Transparent,
					CornerRadius = new CornerRadius(element.HasRoundedCorners ? element.CornerRadius : 0.0)
				};
				try
				{
					if (!string.IsNullOrEmpty(element.BorderColor))
					{
						border2.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.BorderColor) ?? System.Windows.Media.Brushes.Black);
					}
					else
					{
						border2.BorderBrush = System.Windows.Media.Brushes.Black;
					}
				}
				catch
				{
					border2.BorderBrush = System.Windows.Media.Brushes.Black;
				}
				Console.WriteLine($"[DEBUG] Created Rectangle for printing: {border2.Width}x{border2.Height}, HasRoundedCorners: {element.HasRoundedCorners}, CornerRadius: {element.CornerRadius}");
				uIElement = border2;
				break;
			}
			case LabelElementType.Ellipse:
			{
				Ellipse ellipse = new Ellipse
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
						ellipse.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom(element.BorderColor) ?? System.Windows.Media.Brushes.Black);
					}
					else
					{
						ellipse.Stroke = System.Windows.Media.Brushes.Black;
					}
				}
				catch
				{
					ellipse.Stroke = System.Windows.Media.Brushes.Black;
				}
				Console.WriteLine($"[DEBUG] Created Ellipse for printing: {ellipse.Width}x{ellipse.Height}");
				uIElement = ellipse;
				break;
			}
			default:
			{
				TextBlock obj = new TextBlock
				{
					Text = $"[Unknown: {element.Type}]",
					FontSize = 8.0,
					Width = element.Width,
					Height = element.Height,
					Foreground = System.Windows.Media.Brushes.Red,
					TextAlignment = TextAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				Console.WriteLine($"[DEBUG] Created unknown element type: {element.Type}");
				uIElement = obj;
				break;
			}
			}
			if (uIElement != null)
			{
				double x = element.X;
				double y = element.Y;
				Canvas.SetLeft(uIElement, x);
				Canvas.SetTop(uIElement, y);
				if (element.Rotation != 0.0)
				{
					RotateTransform renderTransform = new RotateTransform(element.Rotation);
					if (uIElement is FrameworkElement frameworkElement)
					{
						frameworkElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
					}
					uIElement.RenderTransform = renderTransform;
				}
				canvas.Children.Add(uIElement);
				num2++;
				Console.WriteLine($"[DEBUG] Added element {num} ({element.Type}) to canvas at position ({x}, {y}). Canvas now has {canvas.Children.Count} children");
			}
			else
			{
				Console.WriteLine($"[DEBUG] Failed to create UI element for element {num}");
			}
			num++;
		}
		Console.WriteLine($"[DEBUG] Canvas completed: {num2} elements added out of {template.Elements.Count} template elements");
		Console.WriteLine($"[DEBUG] Final canvas children count: {canvas.Children.Count}");
		return canvas;
	}

	private FixedPage CreatePageWithLabels(int[] recordIndices, double labelWidthPx, double labelHeightPx, System.Windows.Size pageSize, int labelsPerRow)
	{
		Console.WriteLine($"[DEBUG] CreatePageWithLabels: {recordIndices.Length} records, page size {labelWidthPx}x{labelHeightPx}");
		FixedPage fixedPage = new FixedPage();
		fixedPage.Width = labelWidthPx;
		fixedPage.Height = labelHeightPx;
		Console.WriteLine($"[DEBUG] FixedPage created with size {fixedPage.Width}x{fixedPage.Height}");
		if (recordIndices.Length != 0)
		{
			Console.WriteLine($"[DEBUG] Creating label canvas for record {recordIndices[0]}");
			Canvas element = CreatePrintCanvas(recordIndices[0], labelWidthPx, labelHeightPx);
			Canvas.SetLeft(element, 0.0);
			Canvas.SetTop(element, 0.0);
			fixedPage.Children.Add(element);
			Console.WriteLine($"[DEBUG] Label canvas added to page. Page now has {fixedPage.Children.Count} children");
		}
		else
		{
			Console.WriteLine("[DEBUG] No record indices provided!");
		}
		return fixedPage;
	}

	private Canvas CreatePrintCanvas(int recordIndex, double canvasWidthPx, double canvasHeightPx)
	{
		Console.WriteLine($"[DEBUG] CreatePrintCanvas: record {recordIndex}, canvas size {canvasWidthPx}x{canvasHeightPx}");
		Canvas canvas = new Canvas();
		canvas.Background = System.Windows.Media.Brushes.White;
		canvas.Width = canvasWidthPx;
		canvas.Height = canvasHeightPx;
		Console.WriteLine($"[DEBUG] Print canvas created with size {canvas.Width}x{canvas.Height}");
		double num = 127.0 / 480.0;
		double num2 = canvasWidthPx / template.LabelWidth * num;
		Console.WriteLine($"[DEBUG] Scale factor: {num2}");
		int num3 = 0;
		foreach (LabelElement element in template.Elements)
		{
			UIElement uIElement = null;
			string elementContentForRecord = GetElementContentForRecord(element, num3, recordIndex);
			switch (element.Type)
			{
			case LabelElementType.Text:
			{
				TextBlock textBlock = new TextBlock
				{
					Text = elementContentForRecord,
					FontSize = ((element.FontSize > 0.0) ? (element.FontSize * num2) : (12.0 * num2)),
					Width = element.Width * num2,
					Height = element.Height * num2,
					TextWrapping = TextWrapping.Wrap
				};
				try
				{
					if (!string.IsNullOrEmpty(element.FontFamily))
					{
						textBlock.FontFamily = new System.Windows.Media.FontFamily(element.FontFamily);
					}
				}
				catch
				{
				}
				string fontWeight = element.FontWeight;
				textBlock.FontWeight = ((fontWeight != null && fontWeight.Contains("Bold")) ? FontWeights.Bold : FontWeights.Normal);
				string fontStyle = element.FontStyle;
				textBlock.FontStyle = ((fontStyle != null && fontStyle.Contains("Italic")) ? FontStyles.Italic : FontStyles.Normal);
				try
				{
					if (!string.IsNullOrEmpty(element.Foreground))
					{
						SolidColorBrush solidColorBrush = new BrushConverter().ConvertFrom(element.Foreground) as SolidColorBrush;
						textBlock.Foreground = solidColorBrush ?? System.Windows.Media.Brushes.Black;
					}
					else
					{
						textBlock.Foreground = System.Windows.Media.Brushes.Black;
					}
				}
				catch
				{
					textBlock.Foreground = System.Windows.Media.Brushes.Black;
				}
				uIElement = textBlock;
				break;
			}
			case LabelElementType.Image:
			{
				System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle
				{
					Width = element.Width * num2,
					Height = element.Height * num2
				};
				try
				{
					if (!string.IsNullOrEmpty(element.ImageData))
					{
						using MemoryStream streamSource = new MemoryStream(Convert.FromBase64String(element.ImageData));
						BitmapImage bitmapImage = new BitmapImage();
						bitmapImage.BeginInit();
						bitmapImage.StreamSource = streamSource;
						bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage.EndInit();
						bitmapImage.Freeze();
						ImageBrush imageBrush = new ImageBrush();
						imageBrush.ImageSource = bitmapImage;
						imageBrush.Stretch = Stretch.Uniform;
						imageBrush.AlignmentX = AlignmentX.Center;
						imageBrush.AlignmentY = AlignmentY.Center;
						rectangle.Fill = imageBrush;
					}
					else if (!string.IsNullOrEmpty(elementContentForRecord) && File.Exists(elementContentForRecord))
					{
						BitmapImage bitmapImage2 = new BitmapImage();
						bitmapImage2.BeginInit();
						bitmapImage2.UriSource = new Uri(elementContentForRecord);
						bitmapImage2.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage2.EndInit();
						bitmapImage2.Freeze();
						ImageBrush imageBrush2 = new ImageBrush();
						imageBrush2.ImageSource = bitmapImage2;
						imageBrush2.Stretch = Stretch.Uniform;
						imageBrush2.AlignmentX = AlignmentX.Center;
						imageBrush2.AlignmentY = AlignmentY.Center;
						rectangle.Fill = imageBrush2;
					}
					else
					{
						rectangle.Fill = System.Windows.Media.Brushes.LightGray;
						rectangle.Stroke = System.Windows.Media.Brushes.DarkGray;
						rectangle.StrokeThickness = 1.0;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("[DEBUG] Error loading image for print: " + ex.Message);
					rectangle.Fill = System.Windows.Media.Brushes.LightGray;
					rectangle.Stroke = System.Windows.Media.Brushes.Red;
					rectangle.StrokeThickness = 2.0;
				}
				uIElement = rectangle;
				break;
			}
			case LabelElementType.LinkedImage:
			{
				System.Windows.Shapes.Rectangle rectangle2 = new System.Windows.Shapes.Rectangle
				{
					Width = element.Width * num2,
					Height = element.Height * num2
				};
				try
				{
					if (!string.IsNullOrEmpty(element.Content))
					{
						BitmapImage bitmapImage3 = new BitmapImage();
						bitmapImage3.BeginInit();
						bitmapImage3.UriSource = new Uri(element.Content, UriKind.Absolute);
						bitmapImage3.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage3.EndInit();
						bitmapImage3.Freeze();
						ImageBrush imageBrush3 = new ImageBrush();
						imageBrush3.ImageSource = bitmapImage3;
						imageBrush3.Stretch = Stretch.Uniform;
						imageBrush3.AlignmentX = AlignmentX.Center;
						imageBrush3.AlignmentY = AlignmentY.Center;
						rectangle2.Fill = imageBrush3;
						Console.WriteLine("[DEBUG] LinkedImage loaded for print from URL: " + element.Content);
					}
					else
					{
						rectangle2.Fill = System.Windows.Media.Brushes.LightBlue;
						rectangle2.Stroke = System.Windows.Media.Brushes.DarkBlue;
						rectangle2.StrokeThickness = 1.0;
					}
				}
				catch (Exception ex2)
				{
					Console.WriteLine("[DEBUG] Error loading LinkedImage for print from URL " + element.Content + ": " + ex2.Message);
					rectangle2.Fill = System.Windows.Media.Brushes.LightBlue;
					rectangle2.Stroke = System.Windows.Media.Brushes.Red;
					rectangle2.StrokeThickness = 2.0;
				}
				uIElement = rectangle2;
				break;
			}
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
			case LabelElementType.Barcode13:
			case LabelElementType.Barcode8:
			case LabelElementType.Barcode:
			{
				System.Windows.Controls.Image image2 = GenerateBarcodeImageForPrint(elementContentForRecord, element.Type, element.Width * num2, element.Height * num2);
				if (image2 != null)
				{
					uIElement = image2;
					break;
				}
				string barcodeTypeName = GetBarcodeTypeName(element.Type);
				uIElement = new TextBlock
				{
					Text = $"[{barcodeTypeName}: {elementContentForRecord}]",
					FontSize = Math.Max(8.0 * num2, 1.0),
					Width = element.Width * num2,
					Height = element.Height * num2,
					Foreground = System.Windows.Media.Brushes.Black
				};
				break;
			}
			case LabelElementType.QRCode:
			{
				System.Windows.Controls.Image image = GenerateQRCodeImageForPrint(elementContentForRecord, element.Width * num2, element.Height * num2);
				uIElement = ((image == null) ? ((FrameworkElement)new TextBlock
				{
					Text = "[QR: " + elementContentForRecord + "]",
					FontSize = Math.Max(8.0 * num2, 1.0),
					Width = element.Width * num2,
					Height = element.Height * num2,
					Foreground = System.Windows.Media.Brushes.Black
				}) : ((FrameworkElement)image));
				break;
			}
			}
			if (uIElement != null)
			{
				double length = element.X * num2;
				double length2 = element.Y * num2;
				Canvas.SetLeft(uIElement, length);
				Canvas.SetTop(uIElement, length2);
				if (element.Rotation != 0.0)
				{
					RotateTransform renderTransform = new RotateTransform(element.Rotation);
					if (uIElement is FrameworkElement frameworkElement)
					{
						frameworkElement.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
					}
					uIElement.RenderTransform = renderTransform;
				}
				canvas.Children.Add(uIElement);
			}
			num3++;
		}
		Console.WriteLine($"[DEBUG] CreatePrintCanvas completed. Canvas has {canvas.Children.Count} elements from {template.Elements.Count} template elements");
		return canvas;
	}

	private System.Windows.Controls.Image? GenerateBarcodeImageForPrint(string content, LabelElementType type, double width, double height)
	{
		string barcodeFormatString = GetBarcodeFormatString(type);
		return CreateBarcodeImage(barcodeFormatString, content, width, height);
	}

	private System.Windows.Controls.Image? GenerateQRCodeImageForPrint(string content, double width, double height)
	{
		return CreateBarcodeImage("QRCODE", content, width, height);
	}

	private string GetElementContentForRecord(LabelElement element, int elementIndex, int recordIndex)
	{
		if (dataTable == null || template.FieldMappings == null || recordIndex >= dataTable.Rows.Count)
		{
			return element.Content ?? "";
		}
		FieldMapping fieldMapping = template.FieldMappings.FirstOrDefault((FieldMapping m) => m.ElementIndex == elementIndex);
		if (fieldMapping != null && dataTable.Columns.Contains(fieldMapping.FieldName))
		{
			return dataTable.Rows[recordIndex][fieldMapping.FieldName]?.ToString() ?? element.Content ?? "";
		}
		return element.Content ?? "";
	}

	private string ValidateBarcodeContent(string content, LabelElementType type)
	{
		if (string.IsNullOrEmpty(content))
		{
			return type switch
			{
				LabelElementType.EAN13 => "4000000000004", 
				LabelElementType.EAN8 => "40000007", 
				LabelElementType.UPCA => "012345678905", 
				LabelElementType.UPCE => "01234565", 
				LabelElementType.Code128 => "HELLO123", 
				LabelElementType.Code39 => "HELLO39", 
				_ => "4000000000004", 
			};
		}
		switch (type)
		{
		case LabelElementType.EAN13:
		case LabelElementType.Barcode13:
		{
			string text2 = new string(content.Where(char.IsDigit).ToArray());
			if (text2.Length > 13)
			{
				return text2.Substring(0, 13);
			}
			if (text2.Length < 12)
			{
				return text2.PadLeft(12, '0');
			}
			return text2;
		}
		case LabelElementType.EAN8:
		case LabelElementType.Barcode8:
		{
			string text3 = new string(content.Where(char.IsDigit).ToArray());
			if (text3.Length > 8)
			{
				return text3.Substring(0, 8);
			}
			if (text3.Length < 7)
			{
				return text3.PadLeft(7, '0');
			}
			return text3;
		}
		case LabelElementType.UPCA:
		{
			string text = new string(content.Where(char.IsDigit).ToArray());
			if (text.Length > 12)
			{
				return text.Substring(0, 12);
			}
			if (text.Length < 11)
			{
				return text.PadLeft(11, '0');
			}
			return text;
		}
		default:
			return content;
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
