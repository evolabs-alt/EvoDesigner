using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;

namespace TaggJack.UI;

public partial class DataSourceWindow : Window {
	private AppConfig _config;











	public string ConnectionString { get; private set; } = "";

	public string Query => QueryBox.Text;

	public string UpdateQuery => UpdateQueryBox.Text;

	public string SelectedPrinter => PrinterComboBox.SelectedItem?.ToString() ?? "";

	public DataTable? ResultTable { get; private set; }

	public string Host => HostBox.Text;

	public string Port => PortBox.Text;

	public string Database => DatabaseBox.Text;

	public string Username => UsernameBox.Text;

	public string Password => PasswordBox.Password;

	public DataSourceWindow()
	{
		InitializeComponent();
		_config = ConfigService.LoadConfig();
		InitializeFlowModeUI();
		LoadSavedSettings();
	}

	public DataSourceWindow(DataSourceConfig? dataSource)
		: this()
	{
		if (dataSource != null)
		{
			LoadFromDataSource(dataSource);
		}
		else
		{
			ClearFields();
		}
	}

	private void ClearFields()
	{
		HostBox.Text = "";
		PortBox.Text = "";
		DatabaseBox.Text = "";
		UsernameBox.Text = "";
		PasswordBox.Password = "";
		QueryBox.Text = "";
	}

	private void LoadSavedSettings()
	{
		HostBox.Text = "localhost";
		PortBox.Text = "3306";
		DatabaseBox.Text = "";
		UsernameBox.Text = "";
		PasswordBox.Password = "";
		QueryBox.Text = "SELECT * FROM your_table LIMIT 10";
	}

	private void LoadFromDataSource(DataSourceConfig dataSource)
	{
		HostBox.Text = dataSource.Host;
		PortBox.Text = dataSource.Port;
		DatabaseBox.Text = dataSource.Database;
		UsernameBox.Text = dataSource.Username;
		PasswordBox.Password = dataSource.Password;
		QueryBox.Text = dataSource.Query;
		UpdateQueryBox.Text = dataSource.UpdateQuery ?? "";
	}

	private void SaveCurrentSettings()
	{
	}

	private void DisplayFieldsFromResultTable()
	{
		if (ResultTable == null)
		{
			return;
		}
		FieldsList.Items.Clear();
		foreach (DataColumn column in ResultTable.Columns)
		{
			FieldsList.Items.Add(column.ColumnName + " (" + column.DataType.Name + ")");
		}
	}

	private string BuildConnectionString()
	{
		string text = HostBox.Text;
		string text2 = PortBox.Text;
		string text3 = DatabaseBox.Text;
		string text4 = UsernameBox.Text;
		string password = PasswordBox.Password;
		return $"Server={text};Port={text2};Database={text3};Uid={text4};Pwd={password};SslMode=None;AllowPublicKeyRetrieval=True;";
	}

	private void TestConnection_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			ConnectionString = BuildConnectionString();
			MySqlService mySqlService = new MySqlService();
			mySqlService.ConnectionString = ConnectionString;
			mySqlService.ExecuteQuery("SELECT 1 as test");
			MessageBox.Show("Connection successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Connection failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ExecuteQuery_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(Query))
		{
			MessageBox.Show("Please enter a SQL query.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		try
		{
			ConnectionString = BuildConnectionString();
			MySqlService mySqlService = new MySqlService
			{
				ConnectionString = ConnectionString
			};
			ResultTable = mySqlService.ExecuteQuery(Query);
			DisplayFieldsFromResultTable();
			MessageBox.Show($"Query executed successfully! Fetched {ResultTable.Rows.Count} rows with {ResultTable.Columns.Count} columns.", "Success", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Query failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void OK_Click(object sender, RoutedEventArgs e)
	{
		if ((ResultTable != null && ResultTable.Rows.Count != 0) || MessageBox.Show("No data has been fetched. Do you want to continue anyway?", "No Data", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.No)
		{
			ConnectionString = BuildConnectionString();
			SaveCurrentSettings();
			base.DialogResult = true;
			Close();
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	public DataSourceConfig CreateDataSourceConfig()
	{
		return new DataSourceConfig
		{
			Host = Host,
			Port = Port,
			Database = Database,
			Username = Username,
			Password = Password,
			Query = Query,
			DefaultPrinter = SelectedPrinter,
			UpdateQuery = UpdateQuery
		};
	}

	private void InitializeFlowModeUI()
	{
		Console.WriteLine("DEBUG: DataSourceWindow config mode is '" + _config.Mode + "'");
		Console.WriteLine("DEBUG: FORCING PrinterSection to Visible");
		PrinterSection.Visibility = Visibility.Visible;
		LoadPrinters();
	}

	private void LoadPrinters()
	{
		try
		{
			List<string> installedPrinters = PrinterService.GetInstalledPrinters();
			PrinterComboBox.Items.Clear();
			foreach (string item in installedPrinters)
			{
				PrinterComboBox.Items.Add(item);
			}
			if (installedPrinters.Count > 0)
			{
				string defaultPrinter = PrinterService.GetDefaultPrinter();
				if (!string.IsNullOrEmpty(defaultPrinter) && installedPrinters.Contains(defaultPrinter))
				{
					PrinterComboBox.SelectedItem = defaultPrinter;
				}
				else
				{
					PrinterComboBox.SelectedIndex = 0;
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading printers: " + ex.Message, "Printer Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void RefreshPrinters_Click(object sender, RoutedEventArgs e)
	{
		LoadPrinters();
	}
}
