using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EvoDesigner.Models;
using EvoDesigner.Services;

namespace EvoDesigner.UI
{
    /// <summary>
    /// Window to display scheduled print execution logs
    /// </summary>
    public partial class LogsWindow : Window
    {
        private readonly ScheduledPrintingService _scheduledPrintingService;
        private ObservableCollection<ScheduledPrintLogEntry> _logs;

        public LogsWindow(ScheduledPrintingService scheduledPrintingService)
        {
            InitializeComponent();
            _scheduledPrintingService = scheduledPrintingService;
            _logs = new ObservableCollection<ScheduledPrintLogEntry>();
            LogsDataGrid.ItemsSource = _logs;
            
            LoadLogs();
        }

        /// <summary>
        /// Load logs from the scheduled printing service
        /// </summary>
        private void LoadLogs()
        {
            _logs.Clear();
            
            var logs = _scheduledPrintingService.GetLogs();
            
            foreach (var log in logs)
            {
                _logs.Add(log);
            }
            
            // Update the log count display
            if (_logs.Count == 0)
            {
                LogCountText.Text = "No logs available";
                SubtitleText.Text = "No scheduled print executions have been recorded yet";
            }
            else
            {
                LogCountText.Text = $"Showing {_logs.Count} log {(_logs.Count == 1 ? "entry" : "entries")}";
                SubtitleText.Text = $"Showing the last {Math.Min(_logs.Count, 50)} scheduled print executions";
            }
        }

        /// <summary>
        /// Refresh button click handler
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLogs();
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
