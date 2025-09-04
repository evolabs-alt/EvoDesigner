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

        public DataSourceWindow()
        {
            InitializeComponent();
            _config = ConfigService.LoadConfig();
            LoadSavedSettings();
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
