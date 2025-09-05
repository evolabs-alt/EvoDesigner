using System.Windows;

namespace EvoDesigner.Models
{
    public enum LabelElementType 
    { 
        Text, 
        Image, 
        QRCode,
        // Retail Checkout
        EAN13,
        EAN8, 
        UPCA,
        UPCE,
        // Logistics
        Code128,
        Code39,
        // Mobile/Consumer  
        Aztec,
        // Healthcare/Industrial
        DataMatrix,
        GS1DataBar,
        // Government
        PDF417,
        // Legacy (for backward compatibility)
        Barcode13,
        Barcode8,
        Barcode
    }

    public class LabelElement
    {
    [System.Xml.Serialization.XmlElement]
    public LabelElementType Type { get; set; }
    [System.Xml.Serialization.XmlElement]
    public double X { get; set; }
    [System.Xml.Serialization.XmlElement]
    public double Y { get; set; }
    [System.Xml.Serialization.XmlElement]
    public double Width { get; set; }
    [System.Xml.Serialization.XmlElement]
    public double Height { get; set; }
    [System.Xml.Serialization.XmlElement]
    public string Content { get; set; } // Text, image path, barcode data, etc.
    [System.Xml.Serialization.XmlElement]
    public string BarcodeType { get; set; } // EAN13, EAN8, etc.
    [System.Xml.Serialization.XmlElement]
    public string FontFamily { get; set; }
    [System.Xml.Serialization.XmlElement]
    public double FontSize { get; set; }
    [System.Xml.Serialization.XmlElement]
    public string FontWeight { get; set; }
    [System.Xml.Serialization.XmlElement]
    public string FontStyle { get; set; }
    [System.Xml.Serialization.XmlElement]
    public string Foreground { get; set; }
    [System.Xml.Serialization.XmlElement]
    public string? TextDecorations { get; set; } // Underline, Strikethrough, etc.
    [System.Xml.Serialization.XmlElement]
    public string? ImageData { get; set; } // Base64 encoded image data for portability
    [System.Xml.Serialization.XmlElement]
    public double Rotation { get; set; } = 0; // Rotation angle in degrees (0-360)
    }
}
