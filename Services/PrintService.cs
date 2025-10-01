using System.Drawing;
using System.Drawing.Printing;
using EvoDesigner.Models;

namespace EvoDesigner.Services
{
    public class PrintService
    {
        public void Print(LabelTemplate template, string printerName)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = printerName;
            pd.PrintController = new StandardPrintController();
            pd.DefaultPageSettings.Margins.Left = (int)template.MarginLeft;
            pd.DefaultPageSettings.Margins.Right = (int)template.MarginRight;
            pd.DefaultPageSettings.Margins.Top = (int)template.MarginTop;
            pd.DefaultPageSettings.Margins.Bottom = (int)template.MarginBottom;
            pd.DefaultPageSettings.PaperSize = new PaperSize(template.TemplateName, (int)template.LabelWidth, (int)template.LabelHeight);
            pd.PrintPage += (s, e) => PrintLabel(template, e);
            pd.Print();
        }

        private void PrintLabel(LabelTemplate template, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            foreach (var el in template.Elements)
            {
                switch (el.Type)
                {
                    case LabelElementType.Text:
                        using (var font = new Font(el.FontFamily ?? "Arial", (float)el.FontSize, el.FontWeight == "Bold" ? FontStyle.Bold : FontStyle.Regular))
                        using (var brush = new SolidBrush(ColorTranslator.FromHtml(el.Foreground ?? "#000000")))
                        {
                            // Calculate text position based on alignment
                            var textSize = g.MeasureString(el.Content, font);
                            float x = (float)el.X;
                            float y = (float)el.Y;
                            
                            // Handle horizontal text alignment
                            switch (el.TextAlignment?.ToLower())
                            {
                                case "center":
                                    x = (float)(el.X + (el.Width - textSize.Width) / 2);
                                    break;
                                case "right":
                                    x = (float)(el.X + el.Width - textSize.Width);
                                    break;
                                default: // "left" or null
                                    x = (float)el.X;
                                    break;
                            }
                            
                            // Handle vertical alignment
                            switch (el.VerticalAlignment?.ToLower())
                            {
                                case "center":
                                    y = (float)(el.Y + (el.Height - textSize.Height) / 2);
                                    break;
                                case "bottom":
                                    y = (float)(el.Y + el.Height - textSize.Height);
                                    break;
                                default: // "top" or null
                                    y = (float)el.Y;
                                    break;
                            }
                            
                            g.DrawString(el.Content, font, brush, x, y);
                        }
                        break;
                    case LabelElementType.Image:
                        if (!string.IsNullOrEmpty(el.Content) && System.IO.File.Exists(el.Content))
                        {
                            using (var img = Image.FromFile(el.Content))
                            {
                                g.DrawImage(img, (float)el.X, (float)el.Y, (float)el.Width, (float)el.Height);
                            }
                        }
                        break;
                    case LabelElementType.Barcode13:
                    case LabelElementType.Barcode8:
                    case LabelElementType.QRCode:
                        // For demo, just draw the content as text
                        using (var font = new Font("Arial", (float)el.FontSize))
                        using (var brush = new SolidBrush(Color.DarkBlue))
                        {
                            g.DrawString(el.Content, font, brush, (float)el.X, (float)el.Y);
                        }
                        break;
                }
            }
        }
    }
}
