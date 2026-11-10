using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using TaggJack.Models;

namespace TaggJack.Services;

public class PrintService
{
	public void Print(LabelTemplate template, string printerName)
	{
		PrintDocument printDocument = new PrintDocument();
		printDocument.PrinterSettings.PrinterName = printerName;
		printDocument.PrintController = new StandardPrintController();
		printDocument.DefaultPageSettings.Margins.Left = (int)template.MarginLeft;
		printDocument.DefaultPageSettings.Margins.Right = (int)template.MarginRight;
		printDocument.DefaultPageSettings.Margins.Top = (int)template.MarginTop;
		printDocument.DefaultPageSettings.Margins.Bottom = (int)template.MarginBottom;
		printDocument.DefaultPageSettings.PaperSize = new PaperSize(template.TemplateName, (int)template.LabelWidth, (int)template.LabelHeight);
		printDocument.PrintPage += delegate(object s, PrintPageEventArgs e)
		{
			PrintLabel(template, e);
		};
		printDocument.Print();
	}

	private void PrintLabel(LabelTemplate template, PrintPageEventArgs e)
	{
		Graphics graphics = e.Graphics;
		foreach (LabelElement element in template.Elements)
		{
			switch (element.Type)
			{
			case LabelElementType.Text:
			{
				using (Font font2 = new Font(element.FontFamily ?? "Arial", (float)element.FontSize, (element.FontWeight == "Bold") ? FontStyle.Bold : FontStyle.Regular))
				{
					using SolidBrush brush2 = new SolidBrush(ColorTranslator.FromHtml(element.Foreground ?? "#000000"));
					SizeF sizeF = graphics.MeasureString(element.Content, font2);
					float num = (float)element.X;
					float num2 = (float)element.Y;
					string text = element.TextAlignment?.ToLower();
					num = ((text == "center") ? ((float)(element.X + (element.Width - (double)sizeF.Width) / 2.0)) : ((!(text == "right")) ? ((float)element.X) : ((float)(element.X + element.Width - (double)sizeF.Width))));
					text = element.VerticalAlignment?.ToLower();
					graphics.DrawString(y: (text == "center") ? ((float)(element.Y + (element.Height - (double)sizeF.Height) / 2.0)) : ((!(text == "bottom")) ? ((float)element.Y) : ((float)(element.Y + element.Height - (double)sizeF.Height))), s: element.Content, font: font2, brush: brush2, x: num);
				}
				break;
			}
			case LabelElementType.Image:
				if (!string.IsNullOrEmpty(element.Content) && File.Exists(element.Content))
				{
					using Image image = Image.FromFile(element.Content);
					graphics.DrawImage(image, (float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
				}
				break;
			case LabelElementType.QRCode:
			case LabelElementType.Barcode13:
			case LabelElementType.Barcode8:
			{
				using (Font font = new Font("Arial", (float)element.FontSize))
				{
					using SolidBrush brush = new SolidBrush(Color.DarkBlue);
					graphics.DrawString(element.Content, font, brush, (float)element.X, (float)element.Y);
				}
				break;
			}
			}
		}
	}
}
