using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;

namespace EvoDesigner.Services
{
    public class PrinterService
    {
        /// <summary>
        /// Gets all installed printers on the system
        /// </summary>
        /// <returns>List of printer names</returns>
        public static List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            
            try
            {
                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printerName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting installed printers: {ex.Message}");
            }
            
            return printers;
        }

        /// <summary>
        /// Gets the default printer name
        /// </summary>
        /// <returns>Default printer name or empty string if none</returns>
        public static string GetDefaultPrinter()
        {
            try
            {
                var settings = new PrinterSettings();
                return settings.PrinterName ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting default printer: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Checks if a printer exists in the system
        /// </summary>
        /// <param name="printerName">Name of the printer to check</param>
        /// <returns>True if printer exists, false otherwise</returns>
        public static bool PrinterExists(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return false;

            return GetInstalledPrinters().Contains(printerName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a valid printer name - uses specified printer if available, otherwise default
        /// </summary>
        /// <param name="preferredPrinter">Preferred printer name</param>
        /// <returns>Valid printer name or empty string if no printers available</returns>
        public static string GetValidPrinter(string preferredPrinter)
        {
            // Check if preferred printer exists
            if (!string.IsNullOrWhiteSpace(preferredPrinter) && PrinterExists(preferredPrinter))
            {
                return preferredPrinter;
            }

            // Fall back to default printer
            string defaultPrinter = GetDefaultPrinter();
            if (!string.IsNullOrWhiteSpace(defaultPrinter) && PrinterExists(defaultPrinter))
            {
                return defaultPrinter;
            }

            // Return first available printer
            var printers = GetInstalledPrinters();
            return printers.FirstOrDefault() ?? "";
        }

        /// <summary>
        /// Validates printer configuration and returns result with fallback info
        /// </summary>
        /// <param name="preferredPrinter">Preferred printer name</param>
        /// <returns>Validation result with selected printer and warnings</returns>
        public static PrinterValidationResult ValidatePrinter(string preferredPrinter)
        {
            var result = new PrinterValidationResult();
            var installedPrinters = GetInstalledPrinters();

            if (installedPrinters.Count == 0)
            {
                result.IsValid = false;
                result.SelectedPrinter = "";
                result.Warning = "No printers are installed on the system";
                return result;
            }

            if (string.IsNullOrWhiteSpace(preferredPrinter))
            {
                result.SelectedPrinter = GetDefaultPrinter();
                result.IsValid = true;
                result.Warning = "No preferred printer specified, using system default";
                return result;
            }

            if (PrinterExists(preferredPrinter))
            {
                result.SelectedPrinter = preferredPrinter;
                result.IsValid = true;
                return result;
            }

            // Preferred printer not found, fall back
            result.SelectedPrinter = GetDefaultPrinter();
            result.IsValid = true;
            result.Warning = $"Preferred printer '{preferredPrinter}' not found, using system default '{result.SelectedPrinter}'";
            
            return result;
        }
    }

    public class PrinterValidationResult
    {
        public bool IsValid { get; set; }
        public string SelectedPrinter { get; set; } = "";
        public string Warning { get; set; } = "";
    }
}