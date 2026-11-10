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

public partial class SampleDataWindow : Window {
	private readonly LabelTemplate _template;





	public SampleDataWindow(LabelTemplate template)
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		_template = template;
		LoadSampleData();
	}

	private void LoadSampleData()
	{
		try
		{
			if (_template?.DataSource == null || string.IsNullOrWhiteSpace(_template.DataSource.Query))
			{
				StatusText.Text = "No data source configured. Please setup a data source first.";
				SubtitleText.Text = "Configure a data source via Data â†’ Setup Data Source";
				SampleDataGrid.ItemsSource = null;
				return;
			}
			string connectionString = $"Server={_template.DataSource.Host};Port={_template.DataSource.Port};Database={_template.DataSource.Database};Uid={_template.DataSource.Username};Pwd={_template.DataSource.Password};SslMode=None;AllowPublicKeyRetrieval=True;";
			MySqlService obj = new MySqlService
			{
				ConnectionString = connectionString
			};
			string text = _template.DataSource.Query.Trim();
			if (!text.ToUpper().Contains("LIMIT"))
			{
				text += " LIMIT 10";
			}
			DataTable dataTable = obj.ExecuteQuery(text);
			if (dataTable != null && dataTable.Rows.Count > 0)
			{
				SampleDataGrid.ItemsSource = dataTable.DefaultView;
				int count = dataTable.Columns.Count;
				int count2 = dataTable.Rows.Count;
				StatusText.Text = $"Showing {count2} record{((count2 == 1) ? "" : "s")} with {count} column{((count == 1) ? "" : "s")}";
				SubtitleText.Text = "Sample data from: " + _template.DataSource.Database + " @ " + _template.DataSource.Host;
			}
			else
			{
				SampleDataGrid.ItemsSource = null;
				StatusText.Text = "Query returned no records";
				SubtitleText.Text = "The data source query did not return any data";
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading sample data:\n\n" + ex.Message, "Data Load Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			StatusText.Text = "Error loading data";
			SubtitleText.Text = ex.Message;
			SampleDataGrid.ItemsSource = null;
		}
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e)
	{
		LoadSampleData();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
