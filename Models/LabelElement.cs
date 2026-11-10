using System.Xml.Serialization;

namespace TaggJack.Models;

public class LabelElement
{
	[XmlElement]
	public LabelElementType Type { get; set; }

	[XmlElement]
	public double X { get; set; }

	[XmlElement]
	public double Y { get; set; }

	[XmlElement]
	public double Width { get; set; }

	[XmlElement]
	public double Height { get; set; }

	[XmlElement]
	public string Content { get; set; }

	[XmlElement]
	public string BarcodeType { get; set; }

	[XmlElement]
	public string FontFamily { get; set; }

	[XmlElement]
	public double FontSize { get; set; } = 12.0;

	[XmlElement]
	public string FontWeight { get; set; }

	[XmlElement]
	public string FontStyle { get; set; }

	[XmlElement]
	public string Foreground { get; set; }

	[XmlElement]
	public string? TextDecorations { get; set; }

	[XmlElement]
	public string? ImageData { get; set; }

	[XmlElement]
	public double Rotation { get; set; }

	[XmlElement]
	public string? TextAlignment { get; set; } = "Left";

	[XmlElement]
	public string? VerticalAlignment { get; set; } = "Top";

	[XmlElement]
	public double BorderWidth { get; set; } = 1.0;

	[XmlElement]
	public string? BorderColor { get; set; } = "Black";

	[XmlElement]
	public bool HasRoundedCorners { get; set; }

	[XmlElement]
	public double CornerRadius { get; set; } = 10.0;
}
