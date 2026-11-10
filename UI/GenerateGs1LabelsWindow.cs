using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;
using Microsoft.Win32;

namespace TaggJack.UI;

public partial class GenerateGs1LabelsWindow : Window {
	private readonly AppConfig _config;

	private readonly SqliteStore _store = new SqliteStore();

	private readonly Gs1SerialGenerationService _generationService = new Gs1SerialGenerationService();

	private List<ItemRecord> _items = new List<ItemRecord>();



















	public GenerateGs1LabelsWindow(AppConfig config)
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		_config = config;
		LoadPrinters();
		LoadDefaults();
		ReloadItems();
	}

	private void LoadPrinters()
	{
		List<string> installedPrinters = PrinterService.GetInstalledPrinters();
		Level1PrinterCombo.ItemsSource = installedPrinters;
		Level2PrinterCombo.ItemsSource = installedPrinters;
		Level3PrinterCombo.ItemsSource = installedPrinters;
	}

	private void LoadDefaults()
	{
		Gs1Config gs = _config.Gs1;
		BatchBox.Text = gs.DefaultBatch;
		ProductionDatePicker.SelectedDate = (gs.UseTodayForProduction ? DateTime.Today : (DateTime.TryParse(gs.DefaultProductionDate, out var result) ? result : DateTime.Today));
		if (DateTime.TryParse(gs.DefaultExpiryDate, out var result2))
		{
			ExpiryDatePicker.SelectedDate = result2;
		}
		FromGlnBox.Text = gs.FromGln;
		ToGlnBox.Text = gs.ToGln;
		OrderNumberBox.Text = gs.DefaultOrderNumber;
		Level1TemplateBox.Text = gs.Level1TemplatePath;
		Level2TemplateBox.Text = gs.Level2TemplatePath;
		Level3TemplateBox.Text = gs.Level3TemplatePath;
		Level1PrinterCombo.Text = gs.Level1Printer;
		Level2PrinterCombo.Text = gs.Level2Printer;
		Level3PrinterCombo.Text = gs.Level3Printer;
	}

	private void ReloadItems()
	{
		_items = _store.GetAllItems();
		ItemCombo.ItemsSource = _items;
		if (_items.Count > 0)
		{
			ItemCombo.SelectedIndex = 0;
		}
	}

	private void ItemCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ItemCombo.SelectedItem is ItemRecord itemRecord)
		{
			DescriptionBox.Text = itemRecord.Description;
			GtinBox.Text = itemRecord.Gtin;
		}
		UpdateCounts();
	}

	private void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		UpdateCounts();
	}

	private void UpdateCounts()
	{
		if (!(ItemCombo.SelectedItem is ItemRecord itemRecord))
		{
			Level1CountText.Text = "Level 1 serials: -";
			Level2CountText.Text = "Level 2 serials: -";
			Level3CountText.Text = "Level 3 serials: -";
			return;
		}
		if (!int.TryParse(QuantityBox.Text.Replace(",", "").Replace(" ", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0)
		{
			Level1CountText.Text = "Level 1 serials: -";
			Level2CountText.Text = "Level 2 serials: -";
			Level3CountText.Text = "Level 3 serials: -";
			return;
		}
		try
		{
			(int, int, int) tuple = Gs1SerialPlanner.CalculateSerialCounts(result, itemRecord.Level1, itemRecord.Level2, itemRecord.Level3);
			Level1CountText.Text = $"Level 1 serials: {tuple.Item1:N0}";
			Level2CountText.Text = $"Level 2 serials: {tuple.Item2:N0}";
			Level3CountText.Text = $"Level 3 serials: {tuple.Item3:N0}";
		}
		catch (Exception ex)
		{
			Level1CountText.Text = "Level 1 serials: invalid (" + ex.Message + ")";
			Level2CountText.Text = "Level 2 serials: -";
			Level3CountText.Text = "Level 3 serials: -";
		}
	}

	private async void Submit_Click(object sender, RoutedEventArgs e)
	{
		object selectedItem = ItemCombo.SelectedItem;
		if (!(selectedItem is ItemRecord item))
		{
			MessageBox.Show("Select an item.", "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!int.TryParse(QuantityBox.Text.Replace(",", "").Replace(" ", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
		{
			MessageBox.Show("Enter a valid quantity.", "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (string.IsNullOrWhiteSpace(_config.Gs1.ApiUrl) || string.IsNullOrWhiteSpace(_config.Gs1.BearerToken))
		{
			MessageBox.Show("Configure GS1 API URL and bearer token in GS1 Settings.", "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string production = ProductionDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
		string expiry = ExpiryDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
		if (string.IsNullOrWhiteSpace(expiry))
		{
			MessageBox.Show("Expiry date is required.", "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string fromGln = FromGlnBox.Text.Trim();
		string toGln = ToGlnBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(fromGln) || string.IsNullOrWhiteSpace(toGln))
		{
			MessageBox.Show("From GLN and To GLN are required.", "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		SubmitButton.IsEnabled = false;
		StatusText.Text = "Calling GS1 API and generating labels...";
		try
		{
			Gs1GenerationResult gs1GenerationResult = await _generationService.GenerateAsync(_config, item, qty, BatchBox.Text.Trim(), production, expiry, fromGln, toGln, OrderNumberBox.Text.Trim());
			Gs1JobRecord gs1JobRecord = new Gs1JobRecord
			{
				ItemCode = item.ItemCode,
				Description = item.Description,
				Gtin = item.Gtin,
				Quantity = qty,
				Level1Count = gs1GenerationResult.Level1Count,
				Level2Count = gs1GenerationResult.Level2Count,
				Level3Count = gs1GenerationResult.Level3Count,
				Batch = BatchBox.Text.Trim(),
				Production = production,
				Expiry = expiry,
				Level1TemplatePath = Level1TemplateBox.Text.Trim(),
				Level2TemplatePath = Level2TemplateBox.Text.Trim(),
				Level3TemplatePath = Level3TemplateBox.Text.Trim(),
				Level1Printer = (Level1PrinterCombo.Text?.Trim() ?? ""),
				Level2Printer = (Level2PrinterCombo.Text?.Trim() ?? ""),
				Level3Printer = (Level3PrinterCombo.Text?.Trim() ?? ""),
				RawResponseJson = gs1GenerationResult.RawResponseJson,
				CreatedAt = DateTime.UtcNow
			};
			_store.SaveJob(gs1JobRecord, gs1GenerationResult.AllSerials());
			_config.Gs1.FromGln = fromGln;
			_config.Gs1.ToGln = toGln;
			_config.Gs1.DefaultOrderNumber = OrderNumberBox.Text.Trim();
			_config.Gs1.Level1TemplatePath = gs1JobRecord.Level1TemplatePath;
			_config.Gs1.Level2TemplatePath = gs1JobRecord.Level2TemplatePath;
			_config.Gs1.Level3TemplatePath = gs1JobRecord.Level3TemplatePath;
			_config.Gs1.Level1Printer = gs1JobRecord.Level1Printer;
			_config.Gs1.Level2Printer = gs1JobRecord.Level2Printer;
			_config.Gs1.Level3Printer = gs1JobRecord.Level3Printer;
			ConfigService.SaveConfig(_config);
			Gs1LabelPrintHelper.PrintAllLevels(gs1JobRecord, gs1GenerationResult.AllSerials());
			StatusText.Text = $"Completed: {gs1GenerationResult.Level1Count:N0} L1, {gs1GenerationResult.Level2Count:N0} L2, {gs1GenerationResult.Level3Count:N0} L3 serials generated and sent to printer.";
			MessageBox.Show("GS1 labels generated, saved, and sent to printer.", "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			StatusText.Text = "Generation failed.";
			MessageBox.Show(ex.Message, "Generate GS1 Labels", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			SubmitButton.IsEnabled = true;
		}
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		if (new Gs1SettingsWindow(_config)
		{
			Owner = this
		}.ShowDialog() == true)
		{
			LoadDefaults();
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void BrowseLevel1Template_Click(object sender, RoutedEventArgs e)
	{
		BrowseTemplate(Level1TemplateBox);
	}

	private void BrowseLevel2Template_Click(object sender, RoutedEventArgs e)
	{
		BrowseTemplate(Level2TemplateBox);
	}

	private void BrowseLevel3Template_Click(object sender, RoutedEventArgs e)
	{
		BrowseTemplate(Level3TemplateBox);
	}

	private static void BrowseTemplate(TextBox target)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Label templates (*.xml)|*.xml"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			target.Text = openFileDialog.FileName;
		}
	}
}
