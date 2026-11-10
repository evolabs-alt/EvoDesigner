using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;

namespace TaggJack.UI;

public partial class Gs1SampleDataWindow : Window {
	private readonly AppConfig _config;

	private Gs1SampleDataResult? _currentResult;






	public DataTable SampleTable { get; private set; } = new DataTable();

	public int Level { get; private set; } = 1;

	private int SelectedLevel
	{
		get
		{
			if (LevelComboBox.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var result))
			{
				return result;
			}
			return 1;
		}
	}

	public Gs1SampleDataWindow(AppConfig config)
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		_config = config;
		LoadSampleData();
	}

	private void LoadSampleData()
	{
		try
		{
			_currentResult = Gs1SampleDataService.LoadSampleData(SelectedLevel, _config);
			SampleDataGrid.ItemsSource = _currentResult.Table.DefaultView;
			StatusText.Text = _currentResult.SourceDescription;
			SubtitleText.Text = (_currentResult.IsFromLastJob ? "Showing serials from the most recent GS1 generation job" : "Showing synthetic sample data for template design");
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading GS1 sample data:\n\n" + ex.Message, "Data Load Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			StatusText.Text = "Error loading data";
			SubtitleText.Text = ex.Message;
			SampleDataGrid.ItemsSource = null;
		}
	}

	private void LevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (base.IsLoaded)
		{
			LoadSampleData();
		}
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e)
	{
		LoadSampleData();
	}

	private void UseForMappingButton_Click(object sender, RoutedEventArgs e)
	{
		if (_currentResult == null || _currentResult.Table.Rows.Count == 0)
		{
			MessageBox.Show("No sample data available to load.", "No Data", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		SampleTable = _currentResult.Table.Copy();
		Level = _currentResult.Level;
		base.DialogResult = true;
		Close();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}
}
