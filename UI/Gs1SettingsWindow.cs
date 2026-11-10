using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;
using Microsoft.Win32;

namespace TaggJack.UI;

public partial class Gs1SettingsWindow : Window {
	private readonly AppConfig _config;













	public Gs1SettingsWindow(AppConfig config)
	{
		InitializeComponent();
		_config = config;
		LoadPrinters();
		LoadSettings();
	}

	private void LoadPrinters()
	{
		List<string> installedPrinters = PrinterService.GetInstalledPrinters();
		Level1PrinterCombo.ItemsSource = installedPrinters;
		Level2PrinterCombo.ItemsSource = installedPrinters;
		Level3PrinterCombo.ItemsSource = installedPrinters;
	}

	private void LoadSettings()
	{
		Gs1Config gs = _config.Gs1;
		ApiUrlBox.Text = gs.ApiUrl;
		BearerTokenBox.Password = gs.BearerToken;
		DefaultBatchBox.Text = gs.DefaultBatch;
		UseTodayBox.IsChecked = gs.UseTodayForProduction;
		if (!string.IsNullOrWhiteSpace(gs.DefaultProductionDate) && DateTime.TryParse(gs.DefaultProductionDate, out var result))
		{
			ProductionDatePicker.SelectedDate = result;
		}
		else
		{
			ProductionDatePicker.SelectedDate = DateTime.Today;
		}
		if (!string.IsNullOrWhiteSpace(gs.DefaultExpiryDate) && DateTime.TryParse(gs.DefaultExpiryDate, out var result2))
		{
			ExpiryDatePicker.SelectedDate = result2;
		}
		FromGlnBox.Text = gs.FromGln;
		ToGlnBox.Text = gs.ToGln;
		DefaultOrderNumberBox.Text = gs.DefaultOrderNumber;
		Level1TemplateBox.Text = gs.Level1TemplatePath;
		Level2TemplateBox.Text = gs.Level2TemplatePath;
		Level3TemplateBox.Text = gs.Level3TemplatePath;
		Level1PrinterCombo.Text = gs.Level1Printer;
		Level2PrinterCombo.Text = gs.Level2Printer;
		Level3PrinterCombo.Text = gs.Level3Printer;
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		_config.Gs1.ApiUrl = ApiUrlBox.Text.Trim();
		_config.Gs1.BearerToken = BearerTokenBox.Password;
		_config.Gs1.DefaultBatch = DefaultBatchBox.Text.Trim();
		_config.Gs1.UseTodayForProduction = UseTodayBox.IsChecked == true;
		_config.Gs1.DefaultProductionDate = ProductionDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
		_config.Gs1.DefaultExpiryDate = ExpiryDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
		_config.Gs1.FromGln = FromGlnBox.Text.Trim();
		_config.Gs1.ToGln = ToGlnBox.Text.Trim();
		_config.Gs1.DefaultOrderNumber = DefaultOrderNumberBox.Text.Trim();
		_config.Gs1.Level1TemplatePath = Level1TemplateBox.Text.Trim();
		_config.Gs1.Level2TemplatePath = Level2TemplateBox.Text.Trim();
		_config.Gs1.Level3TemplatePath = Level3TemplateBox.Text.Trim();
		_config.Gs1.Level1Printer = Level1PrinterCombo.Text?.Trim() ?? "";
		_config.Gs1.Level2Printer = Level2PrinterCombo.Text?.Trim() ?? "";
		_config.Gs1.Level3Printer = Level3PrinterCombo.Text?.Trim() ?? "";
		ConfigService.SaveConfig(_config);
		base.DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
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
