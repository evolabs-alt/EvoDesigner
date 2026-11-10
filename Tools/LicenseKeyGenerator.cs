using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using TaggJack.Models;
using Microsoft.Win32;

namespace TaggJack.Tools;

public partial class LicenseKeyGenerator : Window {
	private ObservableCollection<LicenseRecord> licenseHistory;

	private string historyFilePath;

	private string currentGeneratedKey;















	public LicenseKeyGenerator()
	{
		InitializeComponent();
		List<string> featuresForLicenseType = LicenseFeatures.GetFeaturesForLicenseType(LicenseType.Standard);
		FeaturesTextBlock.Text = string.Join("\n", featuresForLicenseType.Select((string f) => "âœ“ " + GetFeatureFriendlyName(f)));
		ExpiryDatePicker.SelectedDate = DateTime.Now.AddYears(1);
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaggJack");
		Directory.CreateDirectory(text);
		historyFilePath = Path.Combine(text, "license-generation-history.json");
		licenseHistory = new ObservableCollection<LicenseRecord>();
		LoadHistory();
		HistoryDataGrid.ItemsSource = licenseHistory;
		UpdateHistoryCount();
	}

	private void PerpetualCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (PerpetualCheckBox != null && ExpiryDatePicker != null)
		{
			ExpiryDatePicker.IsEnabled = PerpetualCheckBox.IsChecked != true;
		}
	}

	private string GetFeatureFriendlyName(string feature)
	{
		return feature switch
		{
			"BasicEditing" => "Basic Label Editing", 
			"AdvancedEditing" => "Advanced Editing Tools", 
			"DatabaseIntegration" => "MySQL Database Integration", 
			"ScheduledPrinting" => "Scheduled Printing", 
			"BatchPrinting" => "Batch Printing", 
			"TemplateImportExport" => "Template Import/Export", 
			"BarcodeGeneration" => "Barcode Generation (EAN-13/EAN-8)", 
			"QRCodeGeneration" => "QR Code Generation", 
			_ => feature, 
		};
	}

	private void GenerateKeyButton_Click(object sender, RoutedEventArgs e)
	{
		string text = "ST";
		string text2 = "FFFF";
		if (PerpetualCheckBox.IsChecked != true && ExpiryDatePicker.SelectedDate.HasValue)
		{
			DateTime dateTime = new DateTime(2025, 1, 1);
			int num = (int)(ExpiryDatePicker.SelectedDate.Value - dateTime).TotalDays;
			if (num < 0 || num > 65535)
			{
				ShowStatus("âš \ufe0f Expiry date must be between 2025-01-01 and 2204-01-01", Colors.Red);
				return;
			}
			text2 = num.ToString("X4");
		}
		string text3 = GenerateRandomSegments(8);
		string key = "TJ11" + text + text2 + text3;
		string value = GenerateChecksum(key);
		currentGeneratedKey = $"TJ11-{text}{text2.Substring(0, 2)}-{text2.Substring(2, 2)}{value}-{text3.Substring(0, 4)}-{text3.Substring(4, 4)}";
		GeneratedKeyTextBox.Text = currentGeneratedKey;
		CopyKeyButton.IsEnabled = true;
		SaveRecordButton.IsEnabled = true;
		string text4 = ((text2 == "FFFF") ? "perpetual" : $"expires {ExpiryDatePicker.SelectedDate:yyyy-MM-dd}");
		ShowStatus("âœ… Standard license key generated successfully (" + text4 + ")!", Colors.Green);
	}

	private string GenerateRandomSegments(int length)
	{
		byte[] array = new byte[length];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		StringBuilder stringBuilder = new StringBuilder(length);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append("ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[b % "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Length]);
		}
		return stringBuilder.ToString();
	}

	private string GenerateChecksum(string key)
	{
		using SHA256 sHA = SHA256.Create();
		byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(key + "TaggJackChecksum2025"));
		return new string(new char[2]
		{
			"ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[array[0] % "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Length],
			"ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[array[1] % "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Length]
		});
	}

	private string FormatLicenseKey(string key)
	{
		key = key.Replace("-", "");
		if (key.Length >= 20)
		{
			return $"{key.Substring(0, 4)}-{key.Substring(4, 4)}-{key.Substring(8, 4)}-{key.Substring(12, 4)}-{key.Substring(16, 4)}";
		}
		return key;
	}

	private void CopyKeyButton_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrEmpty(currentGeneratedKey))
		{
			Clipboard.SetText(currentGeneratedKey);
			ShowStatus("\ud83d\udccb License key copied to clipboard!", Colors.Blue);
		}
	}

	private void SaveRecordButton_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(currentGeneratedKey))
		{
			ShowStatus("No license key to save", Colors.Red);
			return;
		}
		string expiryDate = null;
		if (PerpetualCheckBox.IsChecked != true && ExpiryDatePicker.SelectedDate.HasValue)
		{
			expiryDate = ExpiryDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
		}
		LicenseRecord item = new LicenseRecord
		{
			LicenseKey = currentGeneratedKey,
			LicenseType = "Standard",
			CustomerName = CustomerNameTextBox.Text,
			CustomerEmail = CustomerEmailTextBox.Text,
			Company = CompanyTextBox.Text,
			Notes = NotesTextBox.Text,
			GeneratedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
			MaxActivations = 1,
			ExpiryDate = expiryDate
		};
		licenseHistory.Insert(0, item);
		SaveHistory();
		UpdateHistoryCount();
		ShowStatus($"\ud83d\udcbe License record saved successfully! ({licenseHistory.Count} total records)", Colors.Green);
		CustomerNameTextBox.Text = "";
		CustomerEmailTextBox.Text = "";
		CompanyTextBox.Text = "";
		NotesTextBox.Text = "";
	}

	private void ExportButton_Click(object sender, RoutedEventArgs e)
	{
		if (licenseHistory.Count == 0)
		{
			MessageBox.Show("No license records to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
			FileName = $"TaggJack-Licenses-{DateTime.Now:yyyy-MM-dd}.csv"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Generated Date,License Key,Type,Max Activations,Customer Name,Customer Email,Company,Notes");
			foreach (LicenseRecord item in licenseHistory)
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(21, 8, stringBuilder2);
				handler.AppendLiteral("\"");
				handler.AppendFormatted(item.GeneratedDate);
				handler.AppendLiteral("\",\"");
				handler.AppendFormatted(item.LicenseKey);
				handler.AppendLiteral("\",\"");
				handler.AppendFormatted(item.LicenseType);
				handler.AppendLiteral("\",");
				handler.AppendFormatted(item.MaxActivations);
				handler.AppendLiteral(",\"");
				handler.AppendFormatted(item.CustomerName);
				handler.AppendLiteral("\",\"");
				handler.AppendFormatted(item.CustomerEmail);
				handler.AppendLiteral("\",\"");
				handler.AppendFormatted(item.Company);
				handler.AppendLiteral("\",\"");
				handler.AppendFormatted(EscapeCsv(item.Notes));
				handler.AppendLiteral("\"");
				stringBuilder2.AppendLine(ref handler);
			}
			File.WriteAllText(saveFileDialog.FileName, stringBuilder.ToString());
			ShowStatus($"\ud83d\udcca Exported {licenseHistory.Count} records to CSV successfully!", Colors.Green);
			MessageBox.Show($"Exported {licenseHistory.Count} license records to:\n{saveFileDialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			ShowStatus("Export failed: " + ex.Message, Colors.Red);
			MessageBox.Show("Failed to export: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private string EscapeCsv(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
	}

	private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
	{
		if (licenseHistory.Count == 0)
		{
			MessageBox.Show("No history to clear.", "Clear History", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		else if (MessageBox.Show($"Are you sure you want to clear all {licenseHistory.Count} license generation records?\n\n" + "This action cannot be undone. Consider exporting to CSV first.", "Clear History", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
		{
			licenseHistory.Clear();
			SaveHistory();
			UpdateHistoryCount();
			ShowStatus("\ud83d\uddd1\ufe0f History cleared successfully", Colors.Orange);
		}
	}

	private void LoadHistory()
	{
		try
		{
			if (!File.Exists(historyFilePath))
			{
				return;
			}
			List<LicenseRecord> list = JsonSerializer.Deserialize<List<LicenseRecord>>(File.ReadAllText(historyFilePath));
			if (list == null)
			{
				return;
			}
			foreach (LicenseRecord item in list)
			{
				licenseHistory.Add(item);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to load history: " + ex.Message, "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void SaveHistory()
	{
		try
		{
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				WriteIndented = true
			};
			string contents = JsonSerializer.Serialize(licenseHistory.ToList(), options);
			File.WriteAllText(historyFilePath, contents);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to save history: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void UpdateHistoryCount()
	{
		if (HistoryCountTextBlock != null)
		{
			HistoryCountTextBlock.Text = ((licenseHistory.Count == 1) ? "1 license generated" : $"{licenseHistory.Count} licenses generated");
		}
	}

	private void ShowStatus(string message, Color color)
	{
		StatusMessageTextBlock.Text = message;
		StatusMessageTextBlock.Foreground = new SolidColorBrush(color);
	}
}
