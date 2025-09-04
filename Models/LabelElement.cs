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
        public LabelElementType Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Content { get; set; } // Text, image path, barcode data, etc.
        public string BarcodeType { get; set; } // EAN13, EAN8, etc.
        public string FontFamily { get; set; }
        public double FontSize { get; set; }
        public string FontWeight { get; set; }
        public string FontStyle { get; set; }
        public string Foreground { get; set; }
    }
}
