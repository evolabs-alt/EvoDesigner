using System;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;

namespace TaggJack.UI;

public partial class LogsWindow : Window {
	private readonly ScheduledPrintingService _scheduledPrintingService;

	private ObservableCollection<ScheduledPrintLogEntry> _logs;





	public LogsWindow(ScheduledPrintingService scheduledPrintingService)
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		_scheduledPrintingService = scheduledPrintingService;
		_logs = new ObservableCollection<ScheduledPrintLogEntry>();
		LogsDataGrid.ItemsSource = _logs;
		LoadLogs();
	}

	private void LoadLogs()
	{
		_logs.Clear();
		foreach (ScheduledPrintLogEntry log in _scheduledPrintingService.GetLogs())
		{
			_logs.Add(log);
		}
		if (_logs.Count == 0)
		{
			LogCountText.Text = "No logs available";
			SubtitleText.Text = "No scheduled print executions have been recorded yet";
			return;
		}
		LogCountText.Text = $"Showing {_logs.Count} log {((_logs.Count == 1) ? "entry" : "entries")}";
		SubtitleText.Text = $"Showing the last {Math.Min(_logs.Count, 50)} scheduled print executions";
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e)
	{
		LoadLogs();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void LogsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (LogsDataGrid.SelectedItem is ScheduledPrintLogEntry scheduledPrintLogEntry)
		{
			if (scheduledPrintLogEntry.HasProtocolLog)
			{
				InkjetProtocolLogWindow inkjetProtocolLogWindow = new InkjetProtocolLogWindow($"Protocol Log - {scheduledPrintLogEntry.TemplateName} ({scheduledPrintLogEntry.FormattedTime})", scheduledPrintLogEntry.ProtocolLog);
				inkjetProtocolLogWindow.Owner = this;
				inkjetProtocolLogWindow.ShowDialog();
			}
			else if (!string.IsNullOrWhiteSpace(scheduledPrintLogEntry.UpdateErrors))
			{
				MessageBox.Show(scheduledPrintLogEntry.UpdateErrors, "Update Errors - " + scheduledPrintLogEntry.TemplateName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				MessageBox.Show("No protocol log or update errors recorded for this print job.", "No Details", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
	}
}
