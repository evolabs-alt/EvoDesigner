using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;
using TaggJack.Services;

namespace TaggJack.UI;

public partial class Gs1HistoryWindow : Window {
	private readonly SqliteStore _store = new SqliteStore();

	private readonly ObservableCollection<Gs1JobRecord> _jobs = new ObservableCollection<Gs1JobRecord>();

	private readonly ObservableCollection<Gs1SerialRecord> _serials = new ObservableCollection<Gs1SerialRecord>();

	private List<Gs1SerialRecord> _currentSerials = new List<Gs1SerialRecord>();



	public Gs1HistoryWindow()
	{
		InitializeComponent();
		WindowIconHelper.TrySetWindowIcon(this);
		JobsGrid.ItemsSource = _jobs;
		SerialsGrid.ItemsSource = _serials;
		LoadJobs();
	}

	private void LoadJobs()
	{
		_jobs.Clear();
		foreach (Gs1JobRecord job in _store.GetJobs())
		{
			_jobs.Add(job);
		}
		if (_jobs.Count > 0)
		{
			JobsGrid.SelectedIndex = 0;
		}
	}

	private void JobsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		_serials.Clear();
		_currentSerials.Clear();
		if (!(JobsGrid.SelectedItem is Gs1JobRecord gs1JobRecord))
		{
			return;
		}
		_currentSerials = _store.GetSerialsForJob(gs1JobRecord.Id);
		foreach (Gs1SerialRecord currentSerial in _currentSerials)
		{
			_serials.Add(currentSerial);
		}
	}

	private void ReprintAll_Click(object sender, RoutedEventArgs e)
	{
		Reprint(null);
	}

	private void ReprintLevel1_Click(object sender, RoutedEventArgs e)
	{
		Reprint(1);
	}

	private void ReprintLevel2_Click(object sender, RoutedEventArgs e)
	{
		Reprint(2);
	}

	private void ReprintLevel3_Click(object sender, RoutedEventArgs e)
	{
		Reprint(3);
	}

	private void Reprint(int? level)
	{
		if (!(JobsGrid.SelectedItem is Gs1JobRecord job))
		{
			MessageBox.Show("Select a job to reprint.", "Serial History", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		try
		{
			if (level.HasValue)
			{
				Gs1LabelPrintHelper.PrintJobLevel(job, _currentSerials, level.Value);
			}
			else
			{
				Gs1LabelPrintHelper.PrintAllLevels(job, _currentSerials);
			}
			MessageBox.Show("Reprint sent to printer.", "Serial History", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Serial History", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
