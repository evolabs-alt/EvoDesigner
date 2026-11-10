using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TaggJack.Models;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace TaggJack.Services;

public class LabelPrintService
{
	private const int DefaultThermalDpi = 203;

	/// <summary>Cap WPF raster DPI so drivers reporting 600+ do not explode bitmap size/spool cost.</summary>
	private const int MaxRasterDpi = 300;

	private const bool EnableVerboseFieldLogging = false;

	private static readonly ConcurrentDictionary<string, object> DebugLogLocks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Job-scoped decoded image cache so logos/linked images are not reloaded every page.</summary>
	private sealed class PrintImageCache
	{
		private readonly Dictionary<string, BitmapSource> _embedded = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
		private readonly Dictionary<string, BitmapSource> _linked = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

		public BitmapSource GetOrAddEmbedded(string imageData, Func<BitmapSource> factory)
		{
			if (_embedded.TryGetValue(imageData, out BitmapSource? cached))
			{
				return cached;
			}
			BitmapSource source = factory();
			_embedded[imageData] = source;
			return source;
		}

		public BitmapSource GetOrAddLinked(string url, Func<BitmapSource> factory)
		{
			if (_linked.TryGetValue(url, out BitmapSource? cached))
			{
				return cached;
			}
			BitmapSource source = factory();
			_linked[url] = source;
			return source;
		}
	}

	public void Print(LabelTemplate template, string printerName, DataTable? dataTable = null, int recordIndex = 0, int copies = 1)
	{
		try
		{
			AppendDebugLog(printerName, "Starting print for template: " + template.TemplateName + ", Printer: " + printerName);
			double pageWidth = template.LabelWidth * 96.0 / 25.4;
			double pageHeight = template.LabelHeight * 96.0 / 25.4;
			int num = Math.Max(1, copies);
			AppendDebugLog(printerName, $"Page size: {pageWidth} x {pageHeight} DIP ({template.LabelWidth} x {template.LabelHeight} mm)");
			AppendDebugLog(printerName, $"Document created with {num} pages, sending to printer: {printerName}");
			PrintImageCache imageCache = new PrintImageCache();
			PrintRasterizedPages(template, printerName, num, "Label: " + template.TemplateName, pageWidth, pageHeight, (int pageIndex) => CreatePrintCanvas(template, dataTable, recordIndex, pageWidth, pageHeight, imageCache));
			AppendDebugLog(printerName, "Print command completed successfully");
		}
		catch (Exception ex)
		{
			AppendDebugLog(printerName, "ERROR: " + ex.Message + Environment.NewLine + ex.StackTrace);
			throw new Exception("Print error: " + ex.Message, ex);
		}
	}

	public void PrintAllRecords(LabelTemplate template, string printerName, DataTable dataTable)
	{
		try
		{
			int num = dataTable?.Rows.Count ?? 0;
			AppendDebugLog(printerName, $"Starting batch print for {num} records");
			if (num == 0)
			{
				AppendDebugLog(printerName, "No records to print - dataTable is null or empty");
				return;
			}
			if (dataTable != null)
			{
				AppendDebugLog(printerName, $"DataTable has {dataTable.Columns.Count} columns: {string.Join(", ", from DataColumn c in dataTable.Columns
					select c.ColumnName)}");
			}
			double pageWidth = template.LabelWidth * 96.0 / 25.4;
			double pageHeight = template.LabelHeight * 96.0 / 25.4;
			AppendDebugLog(printerName, $"Document created with {num} pages, sending to printer as ONE print job");
			PrintImageCache imageCache = new PrintImageCache();
			PrintRasterizedPages(template, printerName, num, $"Label: {template.TemplateName} ({num} records)", pageWidth, pageHeight, (int pageIndex) => CreatePrintCanvas(template, dataTable, pageIndex, pageWidth, pageHeight, imageCache));
			AppendDebugLog(printerName, "Batch print completed successfully");
		}
		catch (Exception ex)
		{
			AppendDebugLog(printerName, "ERROR: " + ex.Message + Environment.NewLine + ex.StackTrace);
			throw new Exception("Batch print error: " + ex.Message, ex);
		}
	}

	private void PrintRasterizedPages(LabelTemplate template, string printerName, int pageCount, string jobName, double pageWidthDip, double pageHeightDip, Func<int, Canvas> createCanvasForPage)
	{
		if (pageCount <= 0)
		{
			return;
		}
		PrinterSettings printerSettings = ResolvePrinterSettings(printerName);
		var (dpiX, dpiY, driverDpiX, driverDpiY) = ResolvePrinterDpi(printerSettings);
		AppendDebugLog(printerName, $"Using printer: {printerSettings.PrinterName}, raster DPI: {dpiX}x{dpiY} (driver {driverDpiX}x{driverDpiY}), IsValid={printerSettings.IsValid}");
		int paperWidthHundredths = Math.Max(1, (int)Math.Round(template.LabelWidth / 25.4 * 100.0));
		int paperHeightHundredths = Math.Max(1, (int)Math.Round(template.LabelHeight / 25.4 * 100.0));
		using PrintDocument printDocument = new PrintDocument();
		printDocument.DocumentName = jobName;
		printDocument.PrinterSettings.PrinterName = printerSettings.PrinterName;
		printDocument.PrintController = new StandardPrintController();
		printDocument.OriginAtMargins = false;
		printDocument.DefaultPageSettings.Landscape = false;
		printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
		printDocument.DefaultPageSettings.PaperSize = ResolvePaperSize(printerName, printDocument.PrinterSettings, template.TemplateName, paperWidthHundredths, paperHeightHundredths);
		AppendDebugLog(printerName, $"PaperSize: '{printDocument.DefaultPageSettings.PaperSize.PaperName}' {printDocument.DefaultPageSettings.PaperSize.Width}x{printDocument.DefaultPageSettings.PaperSize.Height} hundredths-inch (template {template.LabelWidth}x{template.LabelHeight} mm)");
		int currentPage = 0;
		long totalCanvasMs = 0L;
		long totalRasterizeMs = 0L;
		int bitmapWidth = 0;
		int bitmapHeight = 0;
		printDocument.PrintPage += delegate(object sender, PrintPageEventArgs e)
		{
			if (e.Graphics == null)
			{
				throw new InvalidOperationException("Print graphics context is unavailable.");
			}
			Stopwatch canvasWatch = Stopwatch.StartNew();
			Canvas canvas = createCanvasForPage(currentPage);
			canvasWatch.Stop();
			totalCanvasMs += canvasWatch.ElapsedMilliseconds;
			Stopwatch rasterizeWatch = Stopwatch.StartNew();
			using Bitmap image = RasterizeCanvas(canvas, pageWidthDip, pageHeightDip, dpiX, dpiY);
			rasterizeWatch.Stop();
			totalRasterizeMs += rasterizeWatch.ElapsedMilliseconds;
			bitmapWidth = image.Width;
			bitmapHeight = image.Height;
			e.Graphics.TranslateTransform(0f - e.PageSettings.HardMarginX, 0f - e.PageSettings.HardMarginY);
			e.Graphics.DrawImage(image, 0, 0, paperWidthHundredths, paperHeightHundredths);
			currentPage++;
			e.HasMorePages = currentPage < pageCount;
		};
		Stopwatch printWatch = Stopwatch.StartNew();
		try
		{
			printDocument.Print();
		}
		catch (Win32Exception ex)
		{
			string value = DescribeStartDocFailure(printDocument.PrinterSettings.PrinterName, ex);
			AppendDebugLog(printerName, $"ERROR: StartDoc/Print failed for '{printDocument.PrinterSettings.PrinterName}': {ex.Message} (native {ex.NativeErrorCode}). {value}");
			throw new Exception($"Cannot start print job on '{printDocument.PrinterSettings.PrinterName}': {ex.Message}. {value}", ex);
		}
		printWatch.Stop();
		long printElapsedMs = printWatch.ElapsedMilliseconds;
		long spoolRemainderMs = Math.Max(0L, printElapsedMs - totalCanvasMs - totalRasterizeMs);
		double avgCanvas = (double)totalCanvasMs / (double)pageCount;
		double avgRasterize = (double)totalRasterizeMs / (double)pageCount;
		double avgSpoolRemainder = (double)spoolRemainderMs / (double)pageCount;
		AppendDebugLog(printerName, $"Timing: pages={pageCount}, dpi={dpiX}x{dpiY}, bitmap={bitmapWidth}x{bitmapHeight}, avgCanvas={avgCanvas:F1}ms, avgRasterize={avgRasterize:F1}ms, avgSpoolRemainder={avgSpoolRemainder:F1}ms, totalCanvas={totalCanvasMs}ms, totalRasterize={totalRasterizeMs}ms, totalPrint={printElapsedMs}ms");
	}

	private static PrinterSettings ResolvePrinterSettings(string printerName)
	{
		PrinterSettings printerSettings = new PrinterSettings();
		if (string.IsNullOrWhiteSpace(printerName))
		{
			AppendDebugLog(printerName, "Printer name empty, using default: " + printerSettings.PrinterName);
			return printerSettings;
		}
		printerSettings.PrinterName = printerName;
		if (printerSettings.IsValid)
		{
			AppendDebugLog(printerName, "Found printer: " + printerSettings.PrinterName);
			return printerSettings;
		}
		AppendDebugLog(printerName, $"Printer '{printerName}' not found or not accessible (IsValid=false), using default '{new PrinterSettings().PrinterName}'");
		return new PrinterSettings();
	}

	private static PaperSize ResolvePaperSize(string printerName, PrinterSettings printerSettings, string? templateName, int paperWidthHundredths, int paperHeightHundredths)
	{
		PaperSize paperSize = null;
		int num = int.MaxValue;
		foreach (PaperSize paperSize2 in printerSettings.PaperSizes)
		{
			int val = Math.Abs(paperSize2.Width - paperWidthHundredths) + Math.Abs(paperSize2.Height - paperHeightHundredths);
			int val2 = Math.Abs(paperSize2.Width - paperHeightHundredths) + Math.Abs(paperSize2.Height - paperWidthHundredths);
			int num2 = Math.Min(val, val2);
			if (num2 < num)
			{
				num = num2;
				paperSize = paperSize2;
			}
		}
		if (paperSize != null && num <= 10)
		{
			AppendDebugLog(printerName, $"Matched driver PaperSize '{paperSize.PaperName}' (delta={num})");
			return paperSize;
		}
		PaperSize result = new PaperSize(string.IsNullOrWhiteSpace(templateName) ? "Label" : templateName, paperWidthHundredths, paperHeightHundredths)
		{
			RawKind = 0
		};
		AppendDebugLog(printerName, (paperSize == null) ? "No driver PaperSizes available; using custom size" : $"No close driver PaperSize (best '{paperSize.PaperName}' delta={num}); using custom size");
		return result;
	}

	private static string DescribeStartDocFailure(string printerName, Win32Exception ex)
	{
		return "Check that the printer is online, not paused, reachable from this machine/account, and that a Windows test page succeeds. " + $"NativeErrorCode={ex.NativeErrorCode}.";
	}

	private static (int dpiX, int dpiY, int driverDpiX, int driverDpiY) ResolvePrinterDpi(PrinterSettings settings)
	{
		PrinterResolution printerResolution = settings.DefaultPageSettings.PrinterResolution;
		int driverDpiX = printerResolution.X;
		int driverDpiY = printerResolution.Y;
		if (driverDpiX <= 0 && driverDpiY <= 0)
		{
			return (dpiX: DefaultThermalDpi, dpiY: DefaultThermalDpi, driverDpiX: DefaultThermalDpi, driverDpiY: DefaultThermalDpi);
		}
		if (driverDpiX <= 0)
		{
			driverDpiX = driverDpiY;
		}
		if (driverDpiY <= 0)
		{
			driverDpiY = driverDpiX;
		}
		int dpiX = Math.Min(driverDpiX, MaxRasterDpi);
		int dpiY = Math.Min(driverDpiY, MaxRasterDpi);
		return (dpiX, dpiY, driverDpiX, driverDpiY);
	}

	private static Bitmap RasterizeCanvas(Canvas canvas, double pageWidthDip, double pageHeightDip, int dpiX, int dpiY)
	{
		System.Windows.Size size = new System.Windows.Size(pageWidthDip, pageHeightDip);
		canvas.Measure(size);
		canvas.Arrange(new Rect(size));
		canvas.UpdateLayout();
		int num = Math.Max(1, (int)Math.Ceiling(pageWidthDip * (double)dpiX / 96.0));
		int num2 = Math.Max(1, (int)Math.Ceiling(pageHeightDip * (double)dpiY / 96.0));
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num, num2, dpiX, dpiY, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(canvas);
		Bitmap bitmap = new Bitmap(num, num2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, num, num2), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		try
		{
			renderTargetBitmap.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Stride * num2, bitmapData.Stride);
		}
		finally
		{
			bitmap.UnlockBits(bitmapData);
		}
		return bitmap;
	}

	private static void AppendDebugLog(string? printerName, string message)
	{
		try
		{
			string path = "print-service-debug-" + RunNowDebugLog.SanitizePrinterName(printerName) + ".txt";
			lock (DebugLogLocks.GetOrAdd(path, (string _) => new object()))
			{
				File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}

	private Canvas CreatePrintCanvas(LabelTemplate template, DataTable? dataTable, int recordIndex, double canvasWidth, double canvasHeight, PrintImageCache imageCache)
	{
		Canvas canvas = new Canvas
		{
			Width = canvasWidth,
			Height = canvasHeight,
			Background = System.Windows.Media.Brushes.White
		};
		for (int i = 0; i < template.Elements.Count; i++)
		{
			LabelElement labelElement = template.Elements[i];
			UIElement uIElement = null;
			string elementContentForRecord = GetElementContentForRecord(labelElement, template, i, dataTable, recordIndex);
			switch (labelElement.Type)
			{
			case LabelElementType.Text:
				uIElement = CreateTextElement(labelElement, elementContentForRecord);
				break;
			case LabelElementType.Image:
				uIElement = CreateImageElement(labelElement, imageCache);
				break;
			case LabelElementType.LinkedImage:
				uIElement = CreateLinkedImageElement(labelElement, elementContentForRecord, imageCache);
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
				uIElement = CreateBarcodeElement(labelElement, elementContentForRecord);
				break;
			case LabelElementType.Line:
				uIElement = CreateLineElement(labelElement);
				break;
			case LabelElementType.Rectangle:
				uIElement = CreateRectangleElement(labelElement);
				break;
			case LabelElementType.Ellipse:
				uIElement = CreateEllipseElement(labelElement);
				break;
			}
			if (uIElement != null)
			{
				if (labelElement.Rotation != 0.0)
				{
					RotateTransform renderTransform = new RotateTransform(labelElement.Rotation, labelElement.Width / 2.0, labelElement.Height / 2.0);
					uIElement.RenderTransform = renderTransform;
				}
				Canvas.SetLeft(uIElement, labelElement.X);
				Canvas.SetTop(uIElement, labelElement.Y);
				canvas.Children.Add(uIElement);
			}
		}
		return canvas;
	}

	private string GetElementContentForRecord(LabelElement el, LabelTemplate template, int elementIndex, DataTable? dataTable, int recordIndex)
	{
		if (dataTable != null && recordIndex >= 0 && recordIndex < dataTable.Rows.Count)
		{
			string text = el.Content ?? "";
			string text2 = null;
			FieldMapping fieldMapping = template.FieldMappings?.FirstOrDefault((FieldMapping fm) => fm.ElementIndex == elementIndex);
			if (fieldMapping != null && !string.IsNullOrWhiteSpace(fieldMapping.FieldName) && dataTable.Columns.Contains(fieldMapping.FieldName))
			{
				text = dataTable.Rows[recordIndex][fieldMapping.FieldName]?.ToString() ?? "";
			}
			{
				foreach (DataColumn column in dataTable.Columns)
				{
					string text3 = "{{" + column.ColumnName + "}}";
					if (text.Contains(text3))
					{
						text = text.Replace(text3, dataTable.Rows[recordIndex][column.ColumnName]?.ToString() ?? "");
					}
				}
				return text;
			}
		}
		return el.Content ?? "";
	}

	private UIElement CreateTextElement(LabelElement el, string content)
	{
		Border border = new Border
		{
			Width = el.Width,
			Height = el.Height,
			Background = System.Windows.Media.Brushes.Transparent
		};
		TextBlock textBlock = new TextBlock
		{
			Text = content,
			FontSize = ((el.FontSize > 0.0) ? el.FontSize : 12.0),
			TextWrapping = TextWrapping.Wrap
		};
		try
		{
			if (!string.IsNullOrEmpty(el.FontFamily))
			{
				textBlock.FontFamily = new System.Windows.Media.FontFamily(el.FontFamily);
			}
		}
		catch
		{
		}
		string fontWeight = el.FontWeight;
		textBlock.FontWeight = ((fontWeight != null && fontWeight.Contains("Bold")) ? FontWeights.Bold : FontWeights.Normal);
		string fontStyle = el.FontStyle;
		textBlock.FontStyle = ((fontStyle != null && fontStyle.Contains("Italic")) ? FontStyles.Italic : FontStyles.Normal);
		if (!string.IsNullOrEmpty(el.TextDecorations))
		{
			TextDecorationCollection textDecorationCollection = new TextDecorationCollection();
			if (el.TextDecorations.Contains("Underline"))
			{
				textDecorationCollection.Add(TextDecorations.Underline);
			}
			if (el.TextDecorations.Contains("Strikethrough"))
			{
				textDecorationCollection.Add(TextDecorations.Strikethrough);
			}
			textBlock.TextDecorations = textDecorationCollection;
		}
		try
		{
			if (!string.IsNullOrEmpty(el.Foreground))
			{
				textBlock.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom(el.Foreground) ?? System.Windows.Media.Brushes.Black);
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
		string text = el.TextAlignment?.ToLower();
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
		text = el.VerticalAlignment?.ToLower();
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
		return border;
	}

	private UIElement CreateImageElement(LabelElement el, PrintImageCache imageCache)
	{
		if (!string.IsNullOrEmpty(el.ImageData))
		{
			try
			{
				BitmapSource source = imageCache.GetOrAddEmbedded(el.ImageData, () =>
				{
					byte[] buffer = Convert.FromBase64String(el.ImageData);
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
					bitmapImage.StreamSource = new MemoryStream(buffer);
					bitmapImage.EndInit();
					bitmapImage.Freeze();
					return bitmapImage;
				});
				return new System.Windows.Controls.Image
				{
					Source = source,
					Width = el.Width,
					Height = el.Height,
					Stretch = Stretch.Uniform
				};
			}
			catch
			{
				return CreatePlaceholderRectangle(el.Width, el.Height, System.Windows.Media.Brushes.LightGray, System.Windows.Media.Brushes.Red);
			}
		}
		return CreatePlaceholderRectangle(el.Width, el.Height, System.Windows.Media.Brushes.LightGray, System.Windows.Media.Brushes.DarkGray);
	}

	private UIElement CreateLinkedImageElement(LabelElement el, string url, PrintImageCache imageCache)
	{
		if (!string.IsNullOrEmpty(url))
		{
			try
			{
				BitmapSource source = imageCache.GetOrAddLinked(url, () =>
				{
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.UriSource = new Uri(url, UriKind.Absolute);
					bitmapImage.EndInit();
					bitmapImage.Freeze();
					return bitmapImage;
				});
				return new System.Windows.Controls.Image
				{
					Source = source,
					Width = el.Width,
					Height = el.Height,
					Stretch = Stretch.Uniform
				};
			}
			catch
			{
				return CreatePlaceholderRectangle(el.Width, el.Height, System.Windows.Media.Brushes.LightBlue, System.Windows.Media.Brushes.Blue);
			}
		}
		return CreatePlaceholderRectangle(el.Width, el.Height, System.Windows.Media.Brushes.LightBlue, System.Windows.Media.Brushes.DarkGray);
	}

	private UIElement CreateBarcodeElement(LabelElement el, string content)
	{
		try
		{
			BarcodeFormat barcodeFormat;
			string text;
			switch (el.Type)
			{
			case LabelElementType.EAN13:
			case LabelElementType.Barcode13:
				barcodeFormat = BarcodeFormat.EAN_13;
				text = "4000000000000";
				break;
			case LabelElementType.EAN8:
			case LabelElementType.Barcode8:
				barcodeFormat = BarcodeFormat.EAN_8;
				text = "40000007";
				break;
			case LabelElementType.UPCA:
				barcodeFormat = BarcodeFormat.UPC_A;
				text = "012345678905";
				break;
			case LabelElementType.UPCE:
				barcodeFormat = BarcodeFormat.UPC_E;
				text = "01234565";
				break;
			case LabelElementType.Code128:
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "HELLO123";
				break;
			case LabelElementType.Code39:
				barcodeFormat = BarcodeFormat.CODE_39;
				text = "HELLO39";
				break;
			case LabelElementType.QRCode:
				barcodeFormat = BarcodeFormat.QR_CODE;
				text = "Sample QR Data";
				break;
			case LabelElementType.Aztec:
				barcodeFormat = BarcodeFormat.AZTEC;
				text = "Sample Aztec Data";
				break;
			case LabelElementType.DataMatrix:
				barcodeFormat = BarcodeFormat.DATA_MATRIX;
				text = "Sample Data Matrix";
				break;
			case LabelElementType.GS1DataBar:
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "01234567890123";
				break;
			case LabelElementType.PDF417:
				barcodeFormat = BarcodeFormat.PDF_417;
				text = "Sample PDF417 Data";
				break;
			case LabelElementType.Barcode:
				barcodeFormat = BarcodeFormat.EAN_13;
				text = "4000000000004";
				break;
			default:
				barcodeFormat = BarcodeFormat.CODE_128;
				text = "DEFAULT";
				break;
			}
			if (string.IsNullOrWhiteSpace(content))
			{
				content = text;
			}
			BarcodeWriterPixelData obj = new BarcodeWriterPixelData
			{
				Format = barcodeFormat
			};
			EncodingOptions options = new EncodingOptions
			{
				Width = (int)el.Width * 2,
				Height = (int)el.Height * 2,
				Margin = ((barcodeFormat != BarcodeFormat.PDF_417) ? 1 : 5)
			};
			obj.Options = options;
			PixelData pixelData = obj.Write(content);
			return new System.Windows.Controls.Image
			{
				Source = CreateBitmapSourceFromPixelData(pixelData),
				Width = el.Width,
				Height = el.Height,
				Stretch = Stretch.Uniform
			};
		}
		catch (Exception)
		{
			TextBlock child = new TextBlock
			{
				Text = content,
				FontSize = ((el.FontSize > 0.0) ? el.FontSize : 12.0),
				FontFamily = new System.Windows.Media.FontFamily("Consolas"),
				Width = el.Width,
				Height = el.Height,
				TextAlignment = TextAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			return new Border
			{
				Width = el.Width,
				Height = el.Height,
				Child = child,
				BorderBrush = System.Windows.Media.Brushes.Black,
				BorderThickness = new Thickness(1.0)
			};
		}
	}

	private UIElement CreateQRCodeElement(LabelElement el, string content)
	{
		try
		{
			BarcodeWriterPixelData obj = new BarcodeWriterPixelData
			{
				Format = BarcodeFormat.QR_CODE
			};
			EncodingOptions options = new EncodingOptions
			{
				Width = (int)el.Width * 2,
				Height = (int)el.Height * 2,
				Margin = 1
			};
			obj.Options = options;
			PixelData pixelData = obj.Write(string.IsNullOrWhiteSpace(content) ? "QR" : content);
			return new System.Windows.Controls.Image
			{
				Source = CreateBitmapSourceFromPixelData(pixelData),
				Width = el.Width,
				Height = el.Height,
				Stretch = Stretch.Uniform
			};
		}
		catch (Exception)
		{
			TextBlock child = new TextBlock
			{
				Text = "QR",
				FontSize = 20.0,
				Width = el.Width,
				Height = el.Height,
				TextAlignment = TextAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			return new Border
			{
				Width = el.Width,
				Height = el.Height,
				Child = child,
				BorderBrush = System.Windows.Media.Brushes.Black,
				BorderThickness = new Thickness(2.0)
			};
		}
	}

	/// <summary>Copy ZXing pixel buffer straight into a frozen WriteableBitmap — no PNG encode/decode.</summary>
	private static BitmapSource CreateBitmapSourceFromPixelData(PixelData pixelData)
	{
		WriteableBitmap writeableBitmap = new WriteableBitmap(pixelData.Width, pixelData.Height, 96.0, 96.0, PixelFormats.Bgra32, null);
		writeableBitmap.WritePixels(new Int32Rect(0, 0, pixelData.Width, pixelData.Height), pixelData.Pixels, pixelData.Width * 4, 0);
		writeableBitmap.Freeze();
		return writeableBitmap;
	}

	private UIElement CreateLineElement(LabelElement el)
	{
		Canvas obj = new Canvas
		{
			Width = el.Width,
			Height = el.Height,
			Background = System.Windows.Media.Brushes.Transparent
		};
		Line line = new Line
		{
			X1 = 0.0,
			Y1 = el.Height / 2.0,
			X2 = el.Width,
			Y2 = el.Height / 2.0,
			StrokeThickness = GetStrokeThickness(el)
		};
		line.Stroke = ParseStrokeBrush(el.BorderColor);
		obj.Children.Add(line);
		return obj;
	}

	private UIElement CreateRectangleElement(LabelElement el)
	{
		return new Border
		{
			Width = el.Width,
			Height = el.Height,
			BorderThickness = new Thickness(GetStrokeThickness(el)),
			BorderBrush = ParseStrokeBrush(el.BorderColor),
			Background = System.Windows.Media.Brushes.Transparent,
			CornerRadius = new CornerRadius(el.HasRoundedCorners ? el.CornerRadius : 0.0)
		};
	}

	private UIElement CreateEllipseElement(LabelElement el)
	{
		return new Ellipse
		{
			Width = el.Width,
			Height = el.Height,
			StrokeThickness = GetStrokeThickness(el),
			Stroke = ParseStrokeBrush(el.BorderColor),
			Fill = System.Windows.Media.Brushes.Transparent
		};
	}

	private static double GetStrokeThickness(LabelElement el)
	{
		if (!(el.BorderWidth > 0.0))
		{
			return 1.0;
		}
		return el.BorderWidth;
	}

	private static System.Windows.Media.Brush ParseStrokeBrush(string? colorValue)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(colorValue) && new BrushConverter().ConvertFrom(colorValue) is System.Windows.Media.Brush result)
			{
				return result;
			}
		}
		catch
		{
		}
		return System.Windows.Media.Brushes.Black;
	}

	private System.Windows.Shapes.Rectangle CreatePlaceholderRectangle(double width, double height, System.Windows.Media.Brush fill, System.Windows.Media.Brush stroke)
	{
		return new System.Windows.Shapes.Rectangle
		{
			Width = width,
			Height = height,
			Fill = fill,
			Stroke = stroke,
			StrokeThickness = 1.0
		};
	}
}
