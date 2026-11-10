using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TaggJack.Models;

namespace TaggJack.UI;

public partial class InkjetTemplatePanel : UserControl {
	private sealed class MappingRow : INotifyPropertyChanged
	{
		private int _position;

		private string _fieldName = "";

		private string _printerFieldName = "";

		public int Position
		{
			get
			{
				return _position;
			}
			set
			{
				_position = value;
				OnPropertyChanged("Position");
			}
		}

		public string FieldName
		{
			get
			{
				return _fieldName;
			}
			set
			{
				_fieldName = value ?? "";
				OnPropertyChanged("FieldName");
			}
		}

		public string PrinterFieldName
		{
			get
			{
				return _printerFieldName;
			}
			set
			{
				_printerFieldName = value ?? "";
				OnPropertyChanged("PrinterFieldName");
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? name = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}

	private readonly ObservableCollection<MappingRow> _rows = new ObservableCollection<MappingRow>();

	private List<string> _fieldNames = new List<string>();

	private bool _suppressEvents;

	private TemplateType _vendor = TemplateType.Inkjet;












	public TemplateType Vendor => _vendor;

	public event EventHandler? ConfigChanged;

	public event EventHandler? RunRequested;

	public InkjetTemplatePanel()
	{
		InitializeComponent();
		MappingsGrid.ItemsSource = _rows;
		_rows.CollectionChanged += delegate
		{
			RenumberPositions();
			NotifyChanged();
		};
		ApplyVendorUi();
	}

	public void SetVendor(TemplateType vendor)
	{
		if (!vendor.IsInkjetLike())
		{
			vendor = TemplateType.Inkjet;
		}
		TemplateType vendor2 = _vendor;
		_vendor = vendor;
		if (vendor2 != vendor && !_suppressEvents && int.TryParse(PrinterPortBox.Text, out var result) && result == vendor2.DefaultPrinterPort())
		{
			_suppressEvents = true;
			try
			{
				PrinterPortBox.Text = vendor.DefaultPrinterPort().ToString();
			}
			finally
			{
				_suppressEvents = false;
			}
		}
		ApplyVendorUi();
	}

	public void LoadFromConfig(InkjetConfig? config)
	{
		_suppressEvents = true;
		try
		{
			if (config == null)
			{
				config = new InkjetConfig();
			}
			PrinterHostBox.Text = config.PrinterHost;
			int num = ((config.PrinterPort > 0) ? config.PrinterPort : _vendor.DefaultPrinterPort());
			PrinterPortBox.Text = num.ToString();
			JobNameBox.Text = config.JobName;
			JobIdBox.Text = ((config.JobId <= 0) ? 1 : config.JobId).ToString();
			VarWidthBox.Text = config.VarWidth.ToString();
			_rows.Clear();
			foreach (InkjetPositionMapping item in config.Mappings.OrderBy((InkjetPositionMapping m) => m.Position))
			{
				_rows.Add(new MappingRow
				{
					Position = item.Position,
					FieldName = item.FieldName,
					PrinterFieldName = (item.PrinterFieldName ?? "")
				});
			}
			RenumberPositions();
			ApplyFieldChoices();
			ApplyVendorUi();
		}
		finally
		{
			_suppressEvents = false;
		}
	}

	public InkjetConfig ToConfig()
	{
		int.TryParse(PrinterPortBox.Text, out var result);
		if (result <= 0)
		{
			result = _vendor.DefaultPrinterPort();
		}
		int.TryParse(JobIdBox.Text, out var result2);
		if (result2 <= 0)
		{
			result2 = 1;
		}
		int.TryParse(VarWidthBox.Text, out var result3);
		return new InkjetConfig
		{
			PrinterHost = (PrinterHostBox.Text?.Trim() ?? ""),
			PrinterPort = result,
			JobName = (JobNameBox.Text?.Trim() ?? ""),
			JobId = result2,
			VarWidth = result3,
			Mappings = _rows.Select((MappingRow r) => new InkjetPositionMapping
			{
				Position = r.Position,
				FieldName = (r.FieldName ?? ""),
				PrinterFieldName = (r.PrinterFieldName ?? "")
			}).ToList()
		};
	}

	public void SetAvailableFields(DataTable? table)
	{
		_fieldNames = (from DataColumn c in table?.Columns
			select c.ColumnName).ToList() ?? new List<string>();
		ApplyFieldChoices();
	}

	public void SetStatus(string text)
	{
		StatusText.Text = text;
	}

	public void SetRunEnabled(bool enabled)
	{
		RunInkjetButton.IsEnabled = enabled;
	}

	private void ApplyVendorUi()
	{
		bool flag = _vendor.IsLinx();
		TitleText.Text = _vendor.DisplayName() + " Template";
		SubtitleText.Text = (flag ? "Map data-source fields to Linx remote field names. Schedule runs enqueue all query rows (waits for free buffer slots)." : "Map data-source fields to printer external variable positions. Schedule runs enqueue all query rows via queue load.");
		JobNameLabel.Text = (flag ? "Message Name" : "Job Name");
		JobIdLabel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		JobIdBox.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		PrinterFieldColumn.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void ApplyFieldChoices()
	{
		FieldComboColumn.ItemsSource = _fieldNames;
	}

	private void RenumberPositions()
	{
		for (int i = 0; i < _rows.Count; i++)
		{
			_rows[i].Position = i + 1;
		}
	}

	private void AddRow_Click(object sender, RoutedEventArgs e)
	{
		if (_rows.Count >= 10)
		{
			MessageBox.Show(_vendor.IsLinx() ? "Maximum of 10 remote field mappings." : "Maximum of 10 positions (printer external variables 1..10).", _vendor.DisplayName() + " Mapping", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		_rows.Add(new MappingRow
		{
			Position = _rows.Count + 1,
			FieldName = (_fieldNames.FirstOrDefault() ?? ""),
			PrinterFieldName = ""
		});
	}

	private void DeleteRow_Click(object sender, RoutedEventArgs e)
	{
		if (MappingsGrid.SelectedItem is MappingRow item)
		{
			_rows.Remove(item);
		}
	}

	private void ConfigField_Changed(object sender, TextChangedEventArgs e)
	{
		NotifyChanged();
	}

	private void MappingsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
	{
		NotifyChanged();
	}

	private void RunInkjet_Click(object sender, RoutedEventArgs e)
	{
		this.RunRequested?.Invoke(this, EventArgs.Empty);
	}

	private void NotifyChanged()
	{
		if (!_suppressEvents)
		{
			this.ConfigChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
