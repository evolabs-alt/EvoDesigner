using System;
using System.Data;
using System.Linq;
using System.Windows;
using EvoDesigner.Models;
using EvoDesigner.Services;

namespace EvoDesigner.UI
{
    /// <summary>
    /// Window to display sample data from the configured data source
    /// </summary>
    public partial class SampleDataWindow : Window
    {
        private readonly LabelTemplate _template;

        public SampleDataWindow(LabelTemplate template)
        {
            InitializeComponent();
            _template = template;
            
            LoadSampleData();
        }

        /// <summary>
        /// Load sample data from the data source
        /// </summary>
        private void LoadSampleData()
        {
            try
            {
                // Check if data source is configured
                if (_template?.DataSource == null || string.IsNullOrWhiteSpace(_template.DataSource.Query))
                {
                    StatusText.Text = "No data source configured. Please setup a data source first.";
                    SubtitleText.Text = "Configure a data source via Data → Setup Data Source";
                    SampleDataGrid.ItemsSource = null;
                    return;
                }

                // Build connection string
                var connectionString = $"Server={_template.DataSource.Host};" +
                                     $"Port={_template.DataSource.Port};" +
                                     $"Database={_template.DataSource.Database};" +
                                     $"Uid={_template.DataSource.Username};" +
                                     $"Pwd={_template.DataSource.Password};";

                // Create MySQL service
                var mysqlService = new MySqlService
                {
                    ConnectionString = connectionString
                };

                // Modify query to limit to 10 records
                string sampleQuery = _template.DataSource.Query.Trim();
                
                // Check if query already has a LIMIT clause
                if (sampleQuery.ToUpper().Contains("LIMIT"))
                {
                    // Use the query as-is if it already has a LIMIT
                    // (user may have intentionally limited it)
                }
                else
                {
                    // Add LIMIT 10 to the query
                    sampleQuery += " LIMIT 10";
                }

                // Execute query
                var dataTable = mysqlService.ExecuteQuery(sampleQuery);

                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    // Bind to DataGrid
                    SampleDataGrid.ItemsSource = dataTable.DefaultView;
                    
                    // Update status
                    int columnCount = dataTable.Columns.Count;
                    int rowCount = dataTable.Rows.Count;
                    StatusText.Text = $"Showing {rowCount} record{(rowCount == 1 ? "" : "s")} with {columnCount} column{(columnCount == 1 ? "" : "s")}";
                    SubtitleText.Text = $"Sample data from: {_template.DataSource.Database} @ {_template.DataSource.Host}";
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
                MessageBox.Show(
                    $"Error loading sample data:\n\n{ex.Message}",
                    "Data Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                StatusText.Text = "Error loading data";
                SubtitleText.Text = ex.Message;
                SampleDataGrid.ItemsSource = null;
            }
        }

        /// <summary>
        /// Refresh button click handler
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSampleData();
        }

        /// <summary>
        /// Close button click handler
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
