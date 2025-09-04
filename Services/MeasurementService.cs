using System;

namespace EvoDesigner.Services
{
    public static class MeasurementService
    {
        // Constants for DPI conversion
        public const double SCREEN_DPI = 96.0; // Standard Windows DPI
        public const double PRINT_DPI = 204.0; // Common thermal printer DPI
        public const double MM_TO_INCH = 1.0 / 25.4; // 25.4 mm per inch
        
        // Pixels per millimeter at screen resolution
        public const double PIXELS_PER_MM_SCREEN = SCREEN_DPI * MM_TO_INCH;
        
        // Pixels per millimeter at print resolution
        public const double PIXELS_PER_MM_PRINT = PRINT_DPI * MM_TO_INCH;
        
        // Scale factor from screen to print
        public const double PRINT_SCALE_FACTOR = PRINT_DPI / SCREEN_DPI;

        /// <summary>
        /// Convert millimeters to screen pixels
        /// </summary>
        public static double MmToScreenPixels(double mm)
        {
            return mm * PIXELS_PER_MM_SCREEN;
        }

        /// <summary>
        /// Convert screen pixels to millimeters
        /// </summary>
        public static double ScreenPixelsToMm(double pixels)
        {
            return pixels / PIXELS_PER_MM_SCREEN;
        }

        /// <summary>
        /// Convert millimeters to print pixels
        /// </summary>
        public static double MmToPrintPixels(double mm)
        {
            return mm * PIXELS_PER_MM_PRINT;
        }

        /// <summary>
        /// Convert print pixels to millimeters
        /// </summary>
        public static double PrintPixelsToMm(double pixels)
        {
            return pixels / PIXELS_PER_MM_PRINT;
        }

        /// <summary>
        /// Convert screen pixels to print pixels
        /// </summary>
        public static double ScreenToPrintPixels(double screenPixels)
        {
            return screenPixels * PRINT_SCALE_FACTOR;
        }

        /// <summary>
        /// Convert print pixels to screen pixels
        /// </summary>
        public static double PrintToScreenPixels(double printPixels)
        {
            return printPixels / PRINT_SCALE_FACTOR;
        }

        /// <summary>
        /// Format millimeters for display (with appropriate precision)
        /// </summary>
        public static string FormatMm(double mm)
        {
            return $"{mm:F1} mm";
        }
    }
}
