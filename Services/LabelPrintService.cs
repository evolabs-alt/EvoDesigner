using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EvoDesigner.Models;

namespace EvoDesigner.Services
{
    /// <summary>
    /// Unified label printing service that ensures consistent output across all printing scenarios
    /// </summary>
    public class LabelPrintService
    {
        /// <summary>
        /// Prints a label template with optional data binding
        /// </summary>
        /// <param name="template">The label template to print</param>
        /// <param name="printerName">Name of the printer to use</param>
        /// <param name="dataTable">Optional data table for field binding</param>
        /// <param name="recordIndex">Record index to print (0 if no data)</param>
        /// <param name="copies">Number of copies to print</param>
        public void Print(LabelTemplate template, string printerName, DataTable? dataTable = null, int recordIndex = 0, int copies = 1)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Starting print for template: {template.TemplateName}");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting print for template: {template.TemplateName}, Printer: {printerName}" + Environment.NewLine);
                
                // Create print dialog with specified printer
                var printDialog = new System.Windows.Controls.PrintDialog();
                
                // Find and set the printer
                var printQueue = FindPrinter(printerName);
                if (printQueue != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Found printer: {printQueue.Name}");
                    System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Found printer: {printQueue.Name}, Status: {printQueue.QueueStatus}" + Environment.NewLine);
                    printDialog.PrintQueue = printQueue;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Printer '{printerName}' not found, using default");
                    System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Printer '{printerName}' not found, using default" + Environment.NewLine);
                }
                
                // Convert mm to DIP (Device Independent Pixels) - 96 DPI standard
                double pageWidth = template.LabelWidth * 96.0 / 25.4;
                double pageHeight = template.LabelHeight * 96.0 / 25.4;
                
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Page size: {pageWidth} x {pageHeight} DIP ({template.LabelWidth} x {template.LabelHeight} mm)");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Page size: {pageWidth} x {pageHeight} DIP ({template.LabelWidth} x {template.LabelHeight} mm)" + Environment.NewLine);
                
                // Create a document with the specified number of copies
                var document = new FixedDocument();
                
                for (int copy = 0; copy < copies; copy++)
                {
                    System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Creating page {copy + 1} of {copies}");
                    
                    // Create a page for this copy
                    var page = new FixedPage
                    {
                        Width = pageWidth,
                        Height = pageHeight
                    };
                    
                    // Create the canvas with label elements
                    var canvas = CreatePrintCanvas(template, dataTable, recordIndex, pageWidth, pageHeight);
                    
                    System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Created canvas with {canvas.Children.Count} elements");
                    
                    // Add canvas to page
                    page.Children.Add(canvas);
                    
                    // Add page to document
                    var pageContent = new PageContent();
                    ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                    document.Pages.Add(pageContent);
                }
                
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Document created with {document.Pages.Count} pages, sending to printer...");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Document created with {document.Pages.Count} pages, sending to printer: {printDialog.PrintQueue?.Name ?? "default"}" + Environment.NewLine);
                
                // Print the document
                printDialog.PrintDocument(document.DocumentPaginator, $"Label: {template.TemplateName}");
                
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Print command completed successfully");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Print command completed successfully" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Print error: {ex.Message}");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                throw new Exception($"Print error: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Prints multiple records from a data table in a single print job (one page per record)
        /// </summary>
        /// <param name="template">The label template to print</param>
        /// <param name="printerName">Name of the printer to use</param>
        /// <param name="dataTable">Data table containing records to print</param>
        public void PrintAllRecords(LabelTemplate template, string printerName, DataTable dataTable)
        {
            try
            {
                int recordCount = dataTable?.Rows.Count ?? 0;
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Starting batch print for {recordCount} records");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting batch print for {recordCount} records" + Environment.NewLine);
                
                if (recordCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LabelPrintService] No records to print");
                    System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No records to print - dataTable is null or empty" + Environment.NewLine);
                    return;
                }
                
                // Log data table details
                if (dataTable != null)
                {
                    System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataTable has {dataTable.Columns.Count} columns: {string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}" + Environment.NewLine);
                }
                
                // Create print dialog with specified printer
                var printDialog = new System.Windows.Controls.PrintDialog();
                
                // Find and set the printer
                var printQueue = FindPrinter(printerName);
                if (printQueue != null)
                {
                    printDialog.PrintQueue = printQueue;
                }
                
                // Convert mm to DIP (Device Independent Pixels) - 96 DPI standard
                double pageWidth = template.LabelWidth * 96.0 / 25.4;
                double pageHeight = template.LabelHeight * 96.0 / 25.4;
                
                // Create ONE document with multiple pages (one page per record)
                var document = new FixedDocument();
                
                for (int recordIndex = 0; recordIndex < recordCount; recordIndex++)
                {
                    System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Creating page {recordIndex + 1} of {recordCount}");
                    
                    // Create a page for this record
                    var page = new FixedPage
                    {
                        Width = pageWidth,
                        Height = pageHeight
                    };
                    
                    // Create the canvas with label elements for this specific record
                    var canvas = CreatePrintCanvas(template, dataTable, recordIndex, pageWidth, pageHeight);
                    
                    // Add canvas to page
                    page.Children.Add(canvas);
                    
                    // Add page to the single document
                    var pageContent = new PageContent();
                    ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                    document.Pages.Add(pageContent);
                }
                
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Document created with {document.Pages.Count} pages, sending to printer...");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Document created with {document.Pages.Count} pages, sending to printer as ONE print job" + Environment.NewLine);
                
                // Print the entire document as ONE print job
                printDialog.PrintDocument(document.DocumentPaginator, $"Label: {template.TemplateName} ({recordCount} records)");
                
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Batch print completed successfully");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Batch print completed successfully" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Batch print error: {ex.Message}");
                System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                throw new Exception($"Batch print error: {ex.Message}", ex);
            }
        }
        
        private System.Printing.PrintQueue? FindPrinter(string printerName)
        {
            try
            {
                var printServer = new System.Printing.LocalPrintServer();
                var queue = printServer.GetPrintQueue(printerName);
                return queue;
            }
            catch
            {
                // If printer not found, return null and let PrintDialog use default
                return null;
            }
        }
        
        private Canvas CreatePrintCanvas(LabelTemplate template, DataTable? dataTable, int recordIndex, double canvasWidth, double canvasHeight)
        {
            var canvas = new Canvas
            {
                Width = canvasWidth,
                Height = canvasHeight,
                Background = Brushes.White
            };
            
            for (int i = 0; i < template.Elements.Count; i++)
            {
                var el = template.Elements[i];
                UIElement? uiEl = null;
                string elementContent = GetElementContentForRecord(el, template, i, dataTable, recordIndex);
                
                switch (el.Type)
                {
                    case LabelElementType.Text:
                        uiEl = CreateTextElement(el, elementContent);
                        break;
                        
                    case LabelElementType.Image:
                        uiEl = CreateImageElement(el);
                        break;
                        
                    case LabelElementType.LinkedImage:
                        uiEl = CreateLinkedImageElement(el, elementContent);
                        break;
                        
                    case LabelElementType.Barcode13:
                    case LabelElementType.Barcode8:
                        uiEl = CreateBarcodeElement(el, elementContent);
                        break;
                        
                    case LabelElementType.QRCode:
                        uiEl = CreateQRCodeElement(el, elementContent);
                        break;
                }
                
                if (uiEl != null)
                {
                    // Apply rotation if specified
                    if (el.Rotation != 0)
                    {
                        var rotateTransform = new RotateTransform(el.Rotation, el.Width / 2, el.Height / 2);
                        uiEl.RenderTransform = rotateTransform;
                    }
                    
                    Canvas.SetLeft(uiEl, el.X);
                    Canvas.SetTop(uiEl, el.Y);
                    canvas.Children.Add(uiEl);
                }
            }
            
            return canvas;
        }
        
        private string GetElementContentForRecord(LabelElement el, LabelTemplate template, int elementIndex, DataTable? dataTable, int recordIndex)
        {
            // If we have a data table and valid record index
            if (dataTable != null && recordIndex >= 0 && recordIndex < dataTable.Rows.Count)
            {
                string content = el.Content ?? "";
                string originalContent = content;
                bool wasReplaced = false;
                
                // FIRST: Check if there's a FieldMapping for this element (old system)
                var fieldMapping = template.FieldMappings?.FirstOrDefault(fm => fm.ElementIndex == elementIndex);
                if (fieldMapping != null && !string.IsNullOrWhiteSpace(fieldMapping.FieldName))
                {
                    // Use the field mapping to get the data
                    if (dataTable.Columns.Contains(fieldMapping.FieldName))
                    {
                        var value = dataTable.Rows[recordIndex][fieldMapping.FieldName];
                        content = value?.ToString() ?? "";
                        wasReplaced = true;
                        
                        System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FieldMapping: Element {elementIndex} mapped to field '{fieldMapping.FieldName}' = '{content}' for record {recordIndex}" + Environment.NewLine);
                    }
                }
                
                // SECOND: Replace all {{FieldName}} placeholders with actual data (new system)
                foreach (DataColumn column in dataTable.Columns)
                {
                    string placeholder = $"{{{{{column.ColumnName}}}}}";
                    if (content.Contains(placeholder))
                    {
                        var value = dataTable.Rows[recordIndex][column.ColumnName];
                        content = content.Replace(placeholder, value?.ToString() ?? "");
                        wasReplaced = true;
                        
                        System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Placeholder: Replaced {placeholder} with '{value}' for record {recordIndex}" + Environment.NewLine);
                    }
                }
                
                if (wasReplaced && originalContent != content)
                {
                    System.IO.File.AppendAllText("print-service-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Content transformed from '{originalContent}' to '{content}'" + Environment.NewLine);
                }
                
                return content;
            }
            
            // No data binding - return original content
            return el.Content ?? "";
        }
        
        private UIElement CreateTextElement(LabelElement el, string content)
        {
            var textContainer = new Border
            {
                Width = el.Width,
                Height = el.Height,
                Background = Brushes.Transparent
            };
            
            var tb = new TextBlock
            {
                Text = content,
                FontSize = el.FontSize > 0 ? el.FontSize : 12,
                TextWrapping = TextWrapping.Wrap
            };
            
            // Font family
            try
            {
                if (!string.IsNullOrEmpty(el.FontFamily))
                    tb.FontFamily = new FontFamily(el.FontFamily);
            }
            catch { }
            
            // Font weight and style
            tb.FontWeight = (el.FontWeight?.Contains("Bold") == true ? FontWeights.Bold : FontWeights.Normal);
            tb.FontStyle = (el.FontStyle?.Contains("Italic") == true ? FontStyles.Italic : FontStyles.Normal);
            
            // Text decorations (underline, strikethrough) - parse from TextDecorations property
            if (!string.IsNullOrEmpty(el.TextDecorations))
            {
                var decorations = new TextDecorationCollection();
                if (el.TextDecorations.Contains("Underline"))
                    decorations.Add(TextDecorations.Underline);
                if (el.TextDecorations.Contains("Strikethrough"))
                    decorations.Add(TextDecorations.Strikethrough);
                tb.TextDecorations = decorations;
            }
            
            // Foreground color
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
            
            // Horizontal text alignment
            switch (el.TextAlignment?.ToLower())
            {
                case "center":
                    tb.TextAlignment = TextAlignment.Center;
                    break;
                case "right":
                    tb.TextAlignment = TextAlignment.Right;
                    break;
                default:
                    tb.TextAlignment = TextAlignment.Left;
                    break;
            }
            
            // Vertical alignment
            switch (el.VerticalAlignment?.ToLower())
            {
                case "center":
                    tb.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case "bottom":
                    tb.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
                default:
                    tb.VerticalAlignment = VerticalAlignment.Top;
                    break;
            }
            
            textContainer.Child = tb;
            return textContainer;
        }
        
        private UIElement CreateImageElement(LabelElement el)
        {
            if (!string.IsNullOrEmpty(el.ImageData))
            {
                try
                {
                    byte[] imageBytes = Convert.FromBase64String(el.ImageData);
                    
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bitmapImage.StreamSource = new System.IO.MemoryStream(imageBytes);
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    return new Image
                    {
                        Source = bitmapImage,
                        Width = el.Width,
                        Height = el.Height,
                        Stretch = Stretch.Uniform
                    };
                }
                catch
                {
                    return CreatePlaceholderRectangle(el.Width, el.Height, Brushes.LightGray, Brushes.Red);
                }
            }
            
            return CreatePlaceholderRectangle(el.Width, el.Height, Brushes.LightGray, Brushes.DarkGray);
        }
        
        private UIElement CreateLinkedImageElement(LabelElement el, string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.UriSource = new Uri(url, UriKind.Absolute);
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    return new Image
                    {
                        Source = bitmapImage,
                        Width = el.Width,
                        Height = el.Height,
                        Stretch = Stretch.Uniform
                    };
                }
                catch
                {
                    return CreatePlaceholderRectangle(el.Width, el.Height, Brushes.LightBlue, Brushes.Blue);
                }
            }
            
            return CreatePlaceholderRectangle(el.Width, el.Height, Brushes.LightBlue, Brushes.DarkGray);
        }
        
        private UIElement CreateBarcodeElement(LabelElement el, string content)
        {
            try
            {
                // Determine barcode format based on element type
                ZXing.BarcodeFormat format;
                string defaultData;
                
                switch (el.Type)
                {
                    case LabelElementType.EAN13:
                    case LabelElementType.Barcode13:
                        format = ZXing.BarcodeFormat.EAN_13;
                        defaultData = "4000000000000";
                        break;
                    case LabelElementType.EAN8:
                    case LabelElementType.Barcode8:
                        format = ZXing.BarcodeFormat.EAN_8;
                        defaultData = "40000007";
                        break;
                    case LabelElementType.UPCA:
                        format = ZXing.BarcodeFormat.UPC_A;
                        defaultData = "012345678905";
                        break;
                    case LabelElementType.UPCE:
                        format = ZXing.BarcodeFormat.UPC_E;
                        defaultData = "01234565";
                        break;
                    case LabelElementType.Code128:
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "HELLO123";
                        break;
                    case LabelElementType.Code39:
                        format = ZXing.BarcodeFormat.CODE_39;
                        defaultData = "HELLO39";
                        break;
                    default:
                        format = ZXing.BarcodeFormat.CODE_128;
                        defaultData = "DEFAULT";
                        break;
                }
                
                // Use provided data, fallback to default if empty
                if (string.IsNullOrWhiteSpace(content))
                    content = defaultData;
                
                // Generate barcode using ZXing
                var writer = new ZXing.BarcodeWriterPixelData();
                writer.Format = format;
                
                var options = new ZXing.Common.EncodingOptions
                {
                    Width = (int)el.Width * 2,
                    Height = (int)el.Height * 2,
                    Margin = 1
                };
                
                writer.Options = options;
                
                var pixelData = writer.Write(content);
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
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memory;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    return new Image
                    {
                        Source = bitmapImage,
                        Width = el.Width,
                        Height = el.Height,
                        Stretch = Stretch.Uniform
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] Barcode generation error: {ex.Message}");
                // Fall back to text representation on error
                var textBlock = new TextBlock
                {
                    Text = content,
                    FontSize = el.FontSize > 0 ? el.FontSize : 12,
                    FontFamily = new FontFamily("Consolas"),
                    Width = el.Width,
                    Height = el.Height,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                return new Border
                {
                    Width = el.Width,
                    Height = el.Height,
                    Child = textBlock,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1)
                };
            }
        }
        
        private UIElement CreateQRCodeElement(LabelElement el, string content)
        {
            try
            {
                // Generate actual QR code using ZXing
                var writer = new ZXing.BarcodeWriterPixelData();
                writer.Format = ZXing.BarcodeFormat.QR_CODE;
                
                var options = new ZXing.Common.EncodingOptions
                {
                    Width = (int)el.Width * 2,
                    Height = (int)el.Height * 2,
                    Margin = 1
                };
                
                writer.Options = options;
                
                var pixelData = writer.Write(string.IsNullOrWhiteSpace(content) ? "QR" : content);
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
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memory;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    return new Image
                    {
                        Source = bitmapImage,
                        Width = el.Width,
                        Height = el.Height,
                        Stretch = Stretch.Uniform
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LabelPrintService] QR code generation error: {ex.Message}");
                // Fall back to placeholder on error
                var textBlock = new TextBlock
                {
                    Text = "QR",
                    FontSize = 20,
                    Width = el.Width,
                    Height = el.Height,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                return new Border
                {
                    Width = el.Width,
                    Height = el.Height,
                    Child = textBlock,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(2)
                };
            }
        }
        
        private Rectangle CreatePlaceholderRectangle(double width, double height, Brush fill, Brush stroke)
        {
            return new Rectangle
            {
                Width = width,
                Height = height,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 1
            };
        }
    }
}
