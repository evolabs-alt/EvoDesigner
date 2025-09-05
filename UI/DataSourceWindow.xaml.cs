using System.Data;
using System.Windows;
using EvoDesigner.Services;
using EvoDesigner.Models;

namespace EvoDesigner.UI
{
    public partial class DataSourceWindow : Window
    {
        private AppConfig _config;

        public string ConnectionString { get; private set; } = "";
        public string Query => QueryBox.Text;
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
            // Restore previously entered values from config
            HostBox.Text = _config.MySqlConnection.Host;
            PortBox.Text = _config.MySqlConnection.Port;
            DatabaseBox.Text = _config.MySqlConnection.Database;
            UsernameBox.Text = _config.MySqlConnection.Username;
            PasswordBox.Password = _config.MySqlConnection.Password;
            QueryBox.Text = _config.MySqlConnection.LastQuery;
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
        }

        private void SaveCurrentSettings()
        {
            _config.MySqlConnection.Host = HostBox.Text;
            _config.MySqlConnection.Port = PortBox.Text;
            _config.MySqlConnection.Database = DatabaseBox.Text;
            _config.MySqlConnection.Username = UsernameBox.Text;
            _config.MySqlConnection.Password = PasswordBox.Password;
            _config.MySqlConnection.LastQuery = QueryBox.Text;
            
            ConfigService.SaveConfig(_config);
        }

        private void DisplayFieldsFromResultTable()
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
    }
}
