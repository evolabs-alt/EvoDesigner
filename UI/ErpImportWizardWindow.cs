using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;

namespace TaggJack.UI;

public partial class ErpImportWizardWindow : Window {
	private readonly AppConfig _config;

	private readonly ErpQueryService _erpService = new ErpQueryService();

	private readonly SqliteStore _store = new SqliteStore();

	private int _step = 1;

	private DataTable? _previewTable;

























	public ErpImportWizardWindow(AppConfig config)
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		_config = config;
		DatabaseTypeCombo.ItemsSource = ErpQueryService.GetDatabaseTypes();
		DatabaseTypeCombo.SelectedItem = (string.IsNullOrWhiteSpace(_config.ErpImport.DatabaseType) ? "MySQL" : _config.ErpImport.DatabaseType);
		LoadConnectionFromConfig();
		UpdateStepUi();
	}

	private void LoadConnectionFromConfig()
	{
		ErpImportConfig erpImport = _config.ErpImport;
		HostBox.Text = erpImport.Host;
		PortBox.Text = erpImport.Port;
		DatabaseBox.Text = erpImport.Database;
		UsernameBox.Text = erpImport.Username;
		PasswordBox.Password = erpImport.Password;
		QueryBox.Text = erpImport.Query;
	}

	private ErpImportConfig BuildConfig()
	{
		return new ErpImportConfig
		{
			DatabaseType = (DatabaseTypeCombo.SelectedItem?.ToString() ?? "MySQL"),
			Host = HostBox.Text.Trim(),
			Port = PortBox.Text.Trim(),
			Database = DatabaseBox.Text.Trim(),
			Username = UsernameBox.Text.Trim(),
			Password = PasswordBox.Password,
			Query = QueryBox.Text.Trim(),
			ItemCodeField = (ItemCodeFieldCombo.SelectedItem?.ToString() ?? ""),
			GtinField = (GtinFieldCombo.SelectedItem?.ToString() ?? ""),
			DescriptionField = (DescriptionFieldCombo.SelectedItem?.ToString() ?? ""),
			Level1Field = (Level1FieldCombo.SelectedItem?.ToString() ?? ""),
			Level2Field = (Level2FieldCombo.SelectedItem?.ToString() ?? ""),
			Level3Field = (Level3FieldCombo.SelectedItem?.ToString() ?? "")
		};
	}

	private void SaveConnectionToConfig()
	{
		ErpImportConfig erpImport = BuildConfig();
		_config.ErpImport = erpImport;
		ConfigService.SaveConfig(_config);
	}

	private void UpdateStepUi()
	{
		Step1Panel.Visibility = ((_step != 1) ? Visibility.Collapsed : Visibility.Visible);
		Step2Panel.Visibility = ((_step != 2) ? Visibility.Collapsed : Visibility.Visible);
		Step3Panel.Visibility = ((_step != 3) ? Visibility.Collapsed : Visibility.Visible);
		Step4Panel.Visibility = ((_step != 4) ? Visibility.Collapsed : Visibility.Visible);
		Step5Panel.Visibility = ((_step != 5) ? Visibility.Collapsed : Visibility.Visible);
		StepIndicatorText.Text = $"Step {_step} of 5";
		BackButton.IsEnabled = _step > 1;
		NextButton.Visibility = ((_step >= 5) ? Visibility.Collapsed : Visibility.Visible);
		ImportButton.Visibility = ((_step != 5) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void Back_Click(object sender, RoutedEventArgs e)
	{
		if (_step > 1)
		{
			_step--;
			UpdateStepUi();
		}
	}

	private void Next_Click(object sender, RoutedEventArgs e)
	{
		if (_step == 1 && DatabaseTypeCombo.SelectedItem == null)
		{
			MessageBox.Show("Select a database type.", "ERP Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (_step == 2)
		{
			try
			{
				_erpService.TestConnection(BuildConfig());
			}
			catch (Exception ex)
			{
				MessageBox.Show("Connection failed: " + ex.Message, "ERP Import", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			SaveConnectionToConfig();
		}
		if (_step == 3)
		{
			if (string.IsNullOrWhiteSpace(QueryBox.Text))
			{
				MessageBox.Show("Enter a SQL query.", "ERP Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			SaveConnectionToConfig();
			try
			{
				_previewTable = _erpService.ExecuteQuery(BuildConfig(), QueryBox.Text, 1);
				PopulateFieldCombos(_previewTable);
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Query failed: " + ex2.Message, "ERP Import", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
		}
		if (_step == 4)
		{
			if (string.IsNullOrWhiteSpace(ItemCodeFieldCombo.SelectedItem?.ToString()))
			{
				MessageBox.Show("Map the Item Code field.", "ERP Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			SaveConnectionToConfig();
		}
		if (_step < 5)
		{
			_step++;
			UpdateStepUi();
		}
	}

	private void TestConnection_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			_erpService.TestConnection(BuildConfig());
			MessageBox.Show("Connection successful.", "ERP Import", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Connection failed: " + ex.Message, "ERP Import", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void LoadTables_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			List<string> tables = _erpService.GetTables(BuildConfig());
			TablesCombo.ItemsSource = tables;
			if (tables.Count > 0)
			{
				TablesCombo.SelectedIndex = 0;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Could not load tables: " + ex.Message, "ERP Import", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void TablesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (TablesCombo.SelectedItem is string text)
		{
			QueryBox.Text = "SELECT * FROM " + text;
		}
	}

	private void PopulateFieldCombos(DataTable table)
	{
		List<string> list = (from DataColumn c in table.Columns
			select c.ColumnName).ToList();
		ItemCodeFieldCombo.ItemsSource = list;
		GtinFieldCombo.ItemsSource = list.ToList();
		DescriptionFieldCombo.ItemsSource = list.ToList();
		Level1FieldCombo.ItemsSource = list.ToList();
		Level2FieldCombo.ItemsSource = list.ToList();
		Level3FieldCombo.ItemsSource = list.ToList();
		SelectIfExists(ItemCodeFieldCombo, _config.ErpImport.ItemCodeField);
		SelectIfExists(GtinFieldCombo, _config.ErpImport.GtinField);
		SelectIfExists(DescriptionFieldCombo, _config.ErpImport.DescriptionField);
		SelectIfExists(Level1FieldCombo, _config.ErpImport.Level1Field);
		SelectIfExists(Level2FieldCombo, _config.ErpImport.Level2Field);
		SelectIfExists(Level3FieldCombo, _config.ErpImport.Level3Field);
	}

	private static void SelectIfExists(ComboBox combo, string value)
	{
		if (!string.IsNullOrWhiteSpace(value) && combo.Items.Contains(value))
		{
			combo.SelectedItem = value;
		}
	}

	private void LoadPreview_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			_previewTable = _erpService.ExecuteQuery(BuildConfig(), QueryBox.Text, 20);
			PreviewGrid.ItemsSource = _previewTable.DefaultView;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Preview failed: " + ex.Message, "ERP Import", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void Import_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			SaveConnectionToConfig();
			ErpImportConfig config = BuildConfig();
			DataTable table = _erpService.ExecuteQuery(config, QueryBox.Text);
			List<ItemRecord> list = _erpService.MapRowsToItems(table, config);
			int num = 0;
			foreach (ItemRecord item in list)
			{
				_store.UpsertItem(item);
				num++;
			}
			MessageBox.Show($"Imported {num} item(s) into Item Master.", "ERP Import", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			Close();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Import failed: " + ex.Message, "ERP Import", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
