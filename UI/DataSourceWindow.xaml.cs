using System.Data;
using System.Windows;
using EvoDesigner.Services;
using EvoDesigner.Models;
using System.Linq;
using System;

namespace EvoDesigner.UI
{
    public partial class DataSourceWindow : Window
    {
        private AppConfig _config;

        public string ConnectionString { get; private set; } = "";
        public string Query => QueryBox.Text;
        public string UpdateQuery => UpdateQueryBox.Text; // New property for update query
        public string SelectedPrinter => PrinterComboBox.SelectedItem?.ToString() ?? "";
        public DataTable? ResultTable { get; private set; }
        
        // Expose individual connection properties
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

        public DataSourceWindow(EvoDesigner.Models.DataSourceConfig? dataSource) : this()
        {
            // If a data source is provided, use it instead of saved settings
            if (dataSource != null)
            {
                LoadFromDataSource(dataSource);
            }
            else
            {
                // For new templates, start with empty fields
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
            // For new templates, start with default values
            HostBox.Text = "localhost";
            PortBox.Text = "3306";
            DatabaseBox.Text = "";
            UsernameBox.Text = "";
            PasswordBox.Password = "";
            QueryBox.Text = "SELECT * FROM your_table LIMIT 10";
        }

        private void LoadFromDataSource(EvoDesigner.Models.DataSourceConfig dataSource)
        {
            // Load from template data source configuration
            HostBox.Text = dataSource.Host;
            PortBox.Text = dataSource.Port;
            DatabaseBox.Text = dataSource.Database;
            UsernameBox.Text = dataSource.Username;
            PasswordBox.Password = dataSource.Password;
            QueryBox.Text = dataSource.Query;
            UpdateQueryBox.Text = dataSource.UpdateQuery ?? ""; // Load update query
        }

        private void SaveCurrentSettings()
        {
            // Note: Connection settings are now saved per-template, not globally
            // This method is kept for backward compatibility but does nothing
        }        private void DisplayFieldsFromResultTable()
        {
            if (ResultTable != null)
            {
                FieldsList.Items.Clear();
                foreach (DataColumn column in ResultTable.Columns)
                {
                    FieldsList.Items.Add($"{column.ColumnName} ({column.DataType.Name})");
                }
            }
        }

        private string BuildConnectionString()
        {
            var host = HostBox.Text;
            var port = PortBox.Text;
            var database = DatabaseBox.Text;
            var username = UsernameBox.Text;
            var password = PasswordBox.Password;

            return $"Server={host};Port={port};Database={database};Uid={username};Pwd={password};";
        }

        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectionString = BuildConnectionString();
                var service = new MySqlService { ConnectionString = ConnectionString };
                
                // Test connection with a simple query
                var testResult = service.ExecuteQuery("SELECT 1 as test");
                MessageBox.Show("Connection successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteQuery_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                MessageBox.Show("Please enter a SQL query.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ConnectionString = BuildConnectionString();
                var service = new MySqlService { ConnectionString = ConnectionString };
                ResultTable = service.ExecuteQuery(Query);
                
                // Display field names using the helper method
                DisplayFieldsFromResultTable();
                
                MessageBox.Show($"Query executed successfully! Fetched {ResultTable.Rows.Count} rows with {ResultTable.Columns.Count} columns.", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Query failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                var result = MessageBox.Show("No data has been fetched. Do you want to continue anyway?", 
                    "No Data", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;
            }

            ConnectionString = BuildConnectionString();
            
            // Save the current settings for next time
            SaveCurrentSettings();
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Helper method to create DataSourceConfig - used by MainWindow
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
                UpdateQuery = UpdateQuery // Include update query for marking records as printed
            };
        }

        private void InitializeFlowModeUI()
        {
            // Show/hide printer section based on mode
            System.Diagnostics.Debug.WriteLine($"DataSourceWindow: Config mode is '{_config.Mode}'");
            Console.WriteLine($"DEBUG: DataSourceWindow config mode is '{_config.Mode}'");
            
            // FORCE VISIBLE FOR DEBUGGING
            Console.WriteLine("DEBUG: FORCING PrinterSection to Visible");
            PrinterSection.Visibility = Visibility.Visible;
            LoadPrinters();
            
            /*
            if (_config.Mode == "flow")
            {
                System.Diagnostics.Debug.WriteLine("DataSourceWindow: Showing printer section for flow mode");
                Console.WriteLine("DEBUG: Setting PrinterSection to Visible");
                PrinterSection.Visibility = Visibility.Visible;
                LoadPrinters();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("DataSourceWindow: Hiding printer section for designer mode");
                Console.WriteLine("DEBUG: Setting PrinterSection to Collapsed");
                PrinterSection.Visibility = Visibility.Collapsed;
            }
            */
        }

        private void LoadPrinters()
        {
            try
            {
                var printers = PrinterService.GetInstalledPrinters();
                PrinterComboBox.Items.Clear();
                
                foreach (var printer in printers)
                {
                    PrinterComboBox.Items.Add(printer);
                }

                // Select default printer if available
                if (printers.Count > 0)
                {
                    var defaultPrinter = PrinterService.GetDefaultPrinter();
                    if (!string.IsNullOrEmpty(defaultPrinter) && printers.Contains(defaultPrinter))
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
                MessageBox.Show($"Error loading printers: {ex.Message}", "Printer Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshPrinters_Click(object sender, RoutedEventArgs e)
        {
            LoadPrinters();
        }
    }
}
