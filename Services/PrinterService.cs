using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;

namespace TaggJack.Services;

public class PrinterService
{
	public static List<string> GetInstalledPrinters()
	{
		List<string> list = new List<string>();
		try
		{
			foreach (string installedPrinter in PrinterSettings.InstalledPrinters)
			{
				list.Add(installedPrinter);
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	public static string GetDefaultPrinter()
	{
		try
		{
			return new PrinterSettings().PrinterName ?? "";
		}
		catch (Exception)
		{
			return "";
		}
	}

	public static bool PrinterExists(string printerName)
	{
		if (string.IsNullOrWhiteSpace(printerName))
		{
			return false;
		}
		return GetInstalledPrinters().Contains<string>(printerName, StringComparer.OrdinalIgnoreCase);
	}

	public static string GetValidPrinter(string preferredPrinter)
	{
		if (!string.IsNullOrWhiteSpace(preferredPrinter) && PrinterExists(preferredPrinter))
		{
			return preferredPrinter;
		}
		string defaultPrinter = GetDefaultPrinter();
		if (!string.IsNullOrWhiteSpace(defaultPrinter) && PrinterExists(defaultPrinter))
		{
			return defaultPrinter;
		}
		return GetInstalledPrinters().FirstOrDefault() ?? "";
	}

	public static PrinterValidationResult ValidatePrinter(string preferredPrinter)
	{
		PrinterValidationResult printerValidationResult = new PrinterValidationResult();
		if (GetInstalledPrinters().Count == 0)
		{
			printerValidationResult.IsValid = false;
			printerValidationResult.SelectedPrinter = "";
			printerValidationResult.Warning = "No printers are installed on the system";
			return printerValidationResult;
		}
		if (string.IsNullOrWhiteSpace(preferredPrinter))
		{
			printerValidationResult.SelectedPrinter = GetDefaultPrinter();
			printerValidationResult.IsValid = true;
			printerValidationResult.Warning = "No preferred printer specified, using system default";
			return printerValidationResult;
		}
		if (PrinterExists(preferredPrinter))
		{
			printerValidationResult.SelectedPrinter = preferredPrinter;
			printerValidationResult.IsValid = true;
			return printerValidationResult;
		}
		printerValidationResult.SelectedPrinter = GetDefaultPrinter();
		printerValidationResult.IsValid = true;
		printerValidationResult.Warning = $"Preferred printer '{preferredPrinter}' not found, using system default '{printerValidationResult.SelectedPrinter}'";
		return printerValidationResult;
	}
}
